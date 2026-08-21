using Mirror;
using UnityEngine;
// Pour PlayerInputHandler

/// <summary>
/// Description: Gère le déplacement de la base (Hips) et le saut.
/// Context: Attaché au Player_V2.
/// Justification: Sépare le mouvement continu (FixedUpdate) des impulsions instantanées comme le saut (Update) pour une réactivité parfaite aux inputs.
/// </summary>
[RequireComponent(typeof(PlayerV2_Controller))]
public class PlayerV2_Movement : NetworkBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("La vitesse cible du joueur.")]
    public float MoveSpeed = 5f;

    [Tooltip("Vitesse maximale absolue (sécurité pour brider les forces externes).")]
    public float MaxSpeed = 10f;

    [Tooltip("Force d'accélération pour atteindre la vitesse cible.")]
    public float Acceleration = 15f;

    [Tooltip("Force de freinage lorsqu'il n'y a pas d'input.")]
    public float Deceleration = 15f;

    [Header("Sprint Settings")]
    [Tooltip("Multiplicateur de vitesse lorsque le joueur sprinte.")]
    public float SprintSpeedMultiplier = 1.5f;

    [Header("Crouch Settings")]
    [Tooltip("Multiplicateur de vitesse lorsque le joueur s'accroupit.")]
    public float CrouchSpeedMultiplier = 0.5f;
    [Tooltip("Décalage vertical total de la tête lors de l'accroupissement.")]
    public float CrouchHeadOffset = -0.3f;

    [Header("Jump Settings")]
    [Tooltip("Vélocité instantanée (indépendante de la masse) appliquée aux Hips lors du saut.")]
    public float JumpForce = 8f;

    [Header("Gravity Settings")]
    [Tooltip("Multiplicateur de gravité en chute (rend la chute plus lourde, style Hollow Knight).")]
    public float FallGravityMultiplier = 3f;

    [Tooltip("Rayon de la sphère de détection (doit être environ la taille de ta roue).")]
    public float WheelRadius = 0.15f;

    [Tooltip("Layer du sol pour le Ground Check.")]
    public LayerMask GroundLayer;

    [Header("Jump Compression Scaling")]
    [Tooltip("Distance Y (Hips -> Roue) quand la suspension est au max de son extension (Ressort détendu = Saut Faible).")]
    public float JumpMaxExtensionDistance = 0.6f;
    
    [Tooltip("Distance Y (Hips -> Roue) quand la suspension est écrasée (Ressort compressé = Saut Fort).")]
    public float JumpMinCompressionDistance = 0.2f;

    [Header("Debug")]
    public bool EnableDebugLogs = false;

    private PlayerV2_Controller _controller;
    private PlayerInputHandler _input;
    private PlayerV2_Suspension _suspension;
    
    private bool _isGrounded;
    private bool _wasGrounded;
    private float _lastVerticalVelocity;
    private float _currentCompressionMultiplier;

    private void Awake()
    {
        _controller = GetComponent<PlayerV2_Controller>();
        _input = GetComponent<PlayerInputHandler>();
        _suspension = GetComponent<PlayerV2_Suspension>();
    }

    private void Start()
    {
        if (_controller != null && _controller.HipsRigidbody != null)
        {
            // On fige complètement la rotation des Hips pour qu'elle ne tourne JAMAIS
            _controller.HipsRigidbody.constraints |= RigidbodyConstraints.FreezeRotation;
        }
    }

    private void Update()
    {
        if (!isOwned) return;
        if (_input == null || _controller.HipsRigidbody == null) return;

        // Le GroundCheck est fait dans l'Update pour être parfaitement synchro avec l'Input (qui dure 1 frame)
        UpdateGroundCheckAndCompression();

        // --- Crouch (Head & Arms Retraction) ---
        if (_controller.HeadController != null && _controller.ArmsController != null)
        {
            if (_input.IsCrouching)
            {
                _controller.HeadController.SetHeadHeightOffset(CrouchHeadOffset);
                _controller.ArmsController.SetArmRetraction(true);
            }
            else
            {
                _controller.HeadController.SetHeadHeightOffset(0f);
                _controller.ArmsController.SetArmRetraction(false);
            }
        }

        // --- Jump ---
        if (_input.JumpTriggered)
        {
            if (_isGrounded)
            {
                float finalJumpForce = JumpForce * _currentCompressionMultiplier;
                
                // Sécurité : Ne saute pas si la force est trop faible (roues pendouillantes)
                if (finalJumpForce > 1f)
                {
                    Vector3 currentVel = _controller.HipsRigidbody.linearVelocity;
                    
                    // Calcul de la force à appliquer pour atteindre EXACTEMENT la vélocité désirée.
                    // Cela empêche le joueur de cumuler le rebond de la suspension et le saut pour s'envoler.
                    float forceToApply = finalJumpForce - currentVel.y;

                    if (forceToApply > 0)
                    {
                        _controller.HipsRigidbody.AddForce(Vector3.up * forceToApply, ForceMode.VelocityChange);
                    }
                    
                    if (_suspension != null)
                    {
                        _suspension.TriggerJumpRetraction();
                    }
                }
            }
        }
    }

    private void UpdateGroundCheckAndCompression()
    {
        _wasGrounded = _isGrounded;
        _isGrounded = false;
        float totalWheelY = 0f;
        int wheelCount = 0;

        if (_suspension != null && _suspension.WheelJoints != null)
        {
            foreach (var joint in _suspension.WheelJoints)
            {
                if (joint == null) continue;
                
                Vector3 wheelPos = joint.transform.position;
                // CheckSphere à la position exacte de la roue
                if (Physics.CheckSphere(wheelPos, WheelRadius, GroundLayer))
                {
                    _isGrounded = true;
                }
                
                totalWheelY += wheelPos.y;
                wheelCount++;
            }
        }

        _currentCompressionMultiplier = 0f;
        if (wheelCount > 0)
        {
            float avgWheelY = totalWheelY / wheelCount;
            float distanceToWheels = _controller.HipsRigidbody.position.y - avgWheelY;
            
            // InverseLerp : si distance = MaxExtension, multiplier = 0. Si distance = MinCompression, multiplier = 1.
            _currentCompressionMultiplier = Mathf.InverseLerp(JumpMaxExtensionDistance, JumpMinCompressionDistance, distanceToWheels);
        }

        // Détection de l'atterrissage
        if (_isGrounded && !_wasGrounded)
        {
            if (_lastVerticalVelocity < -2f)
            {
                if (_suspension != null)
                {
                    _suspension.OnHardLanding(Mathf.Abs(_lastVerticalVelocity));
                }
            }
        }
    }

    private void FixedUpdate()
    {
        if (!isOwned) return;
        if (_input == null || _controller.HipsRigidbody == null) return;

        if (_controller.CameraTransform == null)
        {
            Debug.LogError("[PlayerV2_Movement] Erreur Critique : CameraTransform n'est pas assigné dans le PlayerV2_Controller !");
            return;
        }

        Vector2 moveInput = _input.MoveInput;

        // FIX: Ne SURTOUT PAS utiliser _controller.TorsoRigidbody.transform.forward, 
        // car la physique des bras tire sur le Torso et le fait vriller, ce qui dévie le mouvement !
        // On récupère le yaw purement mathématique du composant Look pour une trajectoire parfaite, comme dans la V1.
        PlayerV2_Look look = GetComponent<PlayerV2_Look>();
        float yaw = look != null ? look.CurrentYaw : _controller.TorsoRigidbody.transform.eulerAngles.y;
        
        Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        Vector3 right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;

        Vector3 moveDirection = (forward * moveInput.y + right * moveInput.x).normalized;
        Vector3 currentVelocity = _controller.HipsRigidbody.linearVelocity;
        Vector3 horizontalVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);

        if (moveInput.sqrMagnitude > 0.01f)
        {
            // Calcul de la vitesse modifiée par le Sprint / Crouch
            float currentMoveSpeed = MoveSpeed;
            if (_input.IsCrouching)
            {
                currentMoveSpeed *= CrouchSpeedMultiplier;
            }
            else if (_input.IsSprinting)
            {
                currentMoveSpeed *= SprintSpeedMultiplier;
            }

            // Mouvement basé sur l'accélération vers une vitesse cible
            Vector3 targetVelocity = moveDirection * currentMoveSpeed;
            Vector3 velocityChange = targetVelocity - horizontalVelocity;

            // Application des forces continues dans FixedUpdate
            _controller.HipsRigidbody.AddForce(velocityChange * Acceleration, ForceMode.Acceleration);
        }
        else
        {
            // Freinage contrôlé
            if (horizontalVelocity.magnitude > 0.1f)
            {
                Vector3 brakingForce = -horizontalVelocity * Deceleration;
                _controller.HipsRigidbody.AddForce(brakingForce, ForceMode.Acceleration);
            }
            else
            {
                // Arrêt total pour contrer l'absence de friction
                _controller.HipsRigidbody.linearVelocity = new Vector3(0f, currentVelocity.y, 0f);
            }
        }

        // Application stricte de la MaxSpeed pour limiter la vitesse absolue
        currentVelocity = _controller.HipsRigidbody.linearVelocity;
        horizontalVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
        if (horizontalVelocity.magnitude > MaxSpeed)
        {
            horizontalVelocity = horizontalVelocity.normalized * MaxSpeed;
            _controller.HipsRigidbody.linearVelocity = new Vector3(horizontalVelocity.x, currentVelocity.y, horizontalVelocity.z);
        }

        // Effet de chute plus lourde (Hollow Knight style)
        if (currentVelocity.y < 0 && !_isGrounded)
        {
            _controller.HipsRigidbody.AddForce(Physics.gravity * (FallGravityMultiplier - 1f), ForceMode.Acceleration);
        }

        _lastVerticalVelocity = currentVelocity.y;
    }
    
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;
        if (_controller == null || _controller.HipsRigidbody == null) return;
        if (_suspension == null || _suspension.WheelJoints == null) return;

        // Gizmos Ground Check des roues
        foreach (var joint in _suspension.WheelJoints)
        {
            if (joint == null) continue;
            Vector3 wheelPos = joint.transform.position;
            
            bool isWheelGrounded = Physics.CheckSphere(wheelPos, WheelRadius, GroundLayer);
            Gizmos.color = isWheelGrounded ? new Color(0, 1, 0, 0.4f) : new Color(1, 0, 0, 0.4f);
            Gizmos.DrawSphere(wheelPos, WheelRadius);
        }

        // Gizmo pour afficher le multiplicateur de compression (Jump)
        Vector3 hipsPos = _controller.HipsRigidbody.position;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(hipsPos, hipsPos + Vector3.up * _currentCompressionMultiplier);
    }
}

