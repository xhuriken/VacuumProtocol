using Mirror;
using UnityEngine;

/// <summary>
/// Description: Gère le mouvement physique du cou et de la tête comme un ressort de torsion.
/// Context: Attaché au joueur (PlayerV2), s'intègre avec le script de Look pour piloter le pitch.
/// Justification: Permet à la tête d'être affectée par la physique (forces externes) tout en suivant la direction du regard du joueur.
/// </summary>
public class PlayerV2_Head : NetworkBehaviour
{
    [Header("References")]
    [Tooltip("Role: Liste des articulations du cou jusqu'à la tête.\nUse Case: Répartir le pitch équitablement sur l'ensemble.")]
    public ConfigurableJoint[] NeckJoints;

    [Header("Alignment Settings (Physics)")]
    public bool UseAlignmentForce = true;
    [Tooltip("Force de rotation pour garder la tête alignée sur le torse. Même principe que les bras.")]
    public float HeadAlignmentTorque = 1500f;
    public float HeadAlignmentDamping = 100f;

    private PlayerV2_Controller _controller;
    private Rigidbody _headRb;

    [Header("Pitch Scaling")]
    [Tooltip("Le pourcentage de l'input de pitch que la tête physique va suivre (ex: 0.7 = 70%).")]
    public float PitchMultiplier = 0.7f;

    [SyncVar(hook = nameof(OnPitchChanged))]
    public float CurrentTargetPitch;

    private float _lastSentPitch = -999f;

    [Header("Spring Settings (Muscles)")]
    [Tooltip("Force de rotation qui pousse la tête vers la cible. Haut = Muscle fort.")]
    public float SpringForce = 5000f;
    [Tooltip("Amortissement en rotation. Haut = Moins de balancement.")]
    public float SpringDamper = 500f;
    public float MaxForce = 10000f;

    [Header("Spring Settings (Vertical Boing)")]
    [Tooltip("Force du ressort vertical (Y). Plus c'est bas, plus la tête rebondit (Boing-Boing).")]
    public float YSpringForce = 1500f;
    [Tooltip("Amortissement vertical. Empêche la tête de rebondir comme un yoyo infini.")]
    public float YSpringDamper = 50f;
    [Tooltip("Distance maximale (en mètres) dont le cou peut s'étirer ou s'écraser.")]
    public float YLimit = 0.15f;

    [Header("Limits & Tracking")]
    [Tooltip("Limite d'angle pour chaque os (en degrés)")]
    public float JointAngleLimit = 30f;

    private void Awake()
    {
        _controller = GetComponentInParent<PlayerV2_Controller>();
        if (_controller == null) _controller = GetComponent<PlayerV2_Controller>();
    }

    private void Start()
    {
        // Configure automatiquement les ressorts des joints au démarrage
        if (NeckJoints != null && NeckJoints.Length > 0)
        {
            JointDrive drive = new JointDrive
            {
                positionSpring = SpringForce,
                positionDamper = SpringDamper,
                maximumForce = MaxForce
            };

            JointDrive yDrive = new JointDrive
            {
                positionSpring = YSpringForce,
                positionDamper = YSpringDamper,
                maximumForce = MaxForce
            };

            SoftJointLimit linearLimit = new SoftJointLimit { limit = YLimit };
            SoftJointLimit limit = new SoftJointLimit { limit = JointAngleLimit };

            _headRb = NeckJoints[NeckJoints.Length - 1].GetComponent<Rigidbody>();

            foreach (var joint in NeckJoints)
            {
                if (joint != null)
                {
                    // On utilise SlerpDrive pour un comportement de ressort sur les 3 axes
                    joint.rotationDriveMode = RotationDriveMode.Slerp;
                    joint.slerpDrive = drive;

                    // Configuration du ressort vertical (Axe Y local)
                    joint.yDrive = yDrive;
                    joint.linearLimit = linearLimit;

                    // Débloque l'axe Y pour permettre le rebond (Boing-Boing) et le Crouch
                    joint.xMotion = ConfigurableJointMotion.Locked;
                    joint.yMotion = ConfigurableJointMotion.Limited;
                    joint.zMotion = ConfigurableJointMotion.Locked;

                    // Limite les rotations pour éviter que le cou se torde dans tous les sens
                    joint.angularXMotion = ConfigurableJointMotion.Limited;
                    joint.angularYMotion = ConfigurableJointMotion.Locked;
                    joint.angularZMotion = ConfigurableJointMotion.Locked;

                    joint.lowAngularXLimit = new SoftJointLimit { limit = -JointAngleLimit };
                    joint.highAngularXLimit = limit;

                    // Empêche les deux os connectés de se repousser physiquement (peut causer l'étirement)
                    joint.enableCollision = false;
                }
            }
        }
    }

    [Command]
    private void CmdSetPitch(float targetPitch)
    {
        CurrentTargetPitch = targetPitch;
    }

    /// <summary>
    /// Répartit le pitch désiré sur tous les os du cou de manière équitable.
    /// </summary>
    /// <param name="targetPitch">L'angle total de pitch (haut/bas) désiré</param>
    public void SetTargetPitch(float targetPitch)
    {
        if (NeckJoints == null || NeckJoints.Length == 0) return;
        
        float newPitch = targetPitch * PitchMultiplier;

        if (isOwned)
        {
            // Appliquer localement immédiatement (Prédiction)
            CurrentTargetPitch = newPitch;
            ApplyPitchToJoints(newPitch);

            // N'envoie la commande que si la différence est notable (0.5 degré)
            if (Mathf.Abs(newPitch - _lastSentPitch) > 0.5f)
            {
                CmdSetPitch(newPitch);
                _lastSentPitch = newPitch;
            }
        }
    }

    private void OnPitchChanged(float oldPitch, float newPitch)
    {
        if (!isOwned)
        {
            ApplyPitchToJoints(newPitch);
        }
    }

    private void ApplyPitchToJoints(float pitch)
    {
        if (NeckJoints == null || NeckJoints.Length == 0) return;
        
        // Diviser l'angle par le nombre d'articulations pour une courbe fluide
        float pitchPerJoint = pitch / NeckJoints.Length;

        // Attention: ConfigurableJoint.targetRotation est inversé dans Unity par rapport au repère local
        // On inverse le signe ici pour corriger le mouvement de la souris.
        Quaternion targetRot = Quaternion.Euler(-pitchPerJoint, 0f, 0f);

        foreach (var joint in NeckJoints)
        {
            if (joint != null)
            {
                joint.targetRotation = targetRot;
            }
        }
    }

    /// <summary>
    /// Règle la position de repos du ressort vertical pour chaque articulation (utilisé pour s'accroupir).
    /// </summary>
    /// <param name="offsetY">Le décalage vertical total souhaité (ex: -0.3m pour baisser la tête)</param>
    public void SetHeadHeightOffset(float offsetY)
    {
        if (NeckJoints == null || NeckJoints.Length == 0) return;

        // Répartir le décalage équitablement sur chaque articulation
        float offsetPerJoint = offsetY / NeckJoints.Length;
        Vector3 targetPos = new Vector3(0f, offsetPerJoint, 0f);

        foreach (var joint in NeckJoints)
        {
            if (joint != null)
            {
                joint.targetPosition = targetPos;
            }
        }
    }

    private void FixedUpdate()
    {
        if (!isOwned || !UseAlignmentForce || _headRb == null || _controller == null || _controller.TorsoRigidbody == null) return;

        // La cible: Rotation du Torso (Yaw) + Pitch désiré (X)
        Quaternion torsoRot = _controller.TorsoRigidbody.rotation;
        Quaternion targetRotation = torsoRot * Quaternion.Euler(CurrentTargetPitch, 0f, 0f);

        // Calcul de la différence de rotation
        Quaternion deltaRotation = targetRotation * Quaternion.Inverse(_headRb.rotation);
        deltaRotation.ToAngleAxis(out float angle, out Vector3 axis);

        if (!float.IsNaN(axis.x) && !float.IsNaN(axis.y) && !float.IsNaN(axis.z) && axis.sqrMagnitude > 0.001f)
        {
            if (angle > 180f) angle -= 360f;
            
            // Application de la force (torque)
            Vector3 alignmentTorque = axis * (angle * HeadAlignmentTorque * Mathf.Deg2Rad);
            Vector3 rotationalDamping = -_headRb.angularVelocity * HeadAlignmentDamping;
            Vector3 netTorque = (alignmentTorque + rotationalDamping) * _headRb.mass;
            
            _headRb.AddTorque(netTorque, ForceMode.Force);
        }
    }
}

