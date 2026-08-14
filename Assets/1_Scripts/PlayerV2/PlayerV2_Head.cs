using Mirror;
using UnityEngine;

namespace VacuumProtocol.PlayerV2
{
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

        public float CurrentTargetPitch { get; private set; }

        [Header("Spring Settings (Muscles)")]
        [Tooltip("Force qui pousse la tête vers la cible. Haut = Muscle fort.")]
        public float SpringForce = 5000f;
        [Tooltip("Amortissement. Haut = Moins de rebond/balancement (muscle tendu).")]
        public float SpringDamper = 500f;
        public float MaxForce = 10000f;

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
            if (!isOwned) return;

            // Configure automatiquement les ressorts des joints au démarrage
            if (NeckJoints != null && NeckJoints.Length > 0)
            {
                JointDrive drive = new JointDrive
                {
                    positionSpring = SpringForce,
                    positionDamper = SpringDamper,
                    maximumForce = MaxForce
                };

                SoftJointLimit limit = new SoftJointLimit { limit = JointAngleLimit };

                _headRb = NeckJoints[NeckJoints.Length - 1].GetComponent<Rigidbody>();

                foreach (var joint in NeckJoints)
                {
                    if (joint != null)
                    {
                        // On utilise SlerpDrive pour un comportement de ressort sur les 3 axes
                        joint.rotationDriveMode = RotationDriveMode.Slerp;
                        joint.slerpDrive = drive;

                        // Force le blocage des positions pour que le cou ne s'étire pas
                        joint.xMotion = ConfigurableJointMotion.Locked;
                        joint.yMotion = ConfigurableJointMotion.Locked;
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

        /// <summary>
        /// Répartit le pitch désiré sur tous les os du cou de manière équitable.
        /// </summary>
        /// <param name="targetPitch">L'angle total de pitch (haut/bas) désiré</param>
        public void SetTargetPitch(float targetPitch)
        {
            if (NeckJoints == null || NeckJoints.Length == 0) return;
            CurrentTargetPitch = targetPitch;

            // Diviser l'angle par le nombre d'articulations pour une courbe fluide
            float pitchPerJoint = targetPitch / NeckJoints.Length;

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
}
