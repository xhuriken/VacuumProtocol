using UnityEngine;
using Mirror;
using VacuumProtocol.Player; // Pour PlayerInputHandler si on le garde

namespace VacuumProtocol.PlayerV2
{
    /// <summary>
    /// Description: Gère la rotation de la tourelle (Torso) et de la caméra (Pitch).
    /// Context: Attaché au Player_V2.
    /// Justification: Tourne le Torso via la physique (MoveRotation) ce qui permet au joint Torso-Hips de gérer la liaison sans bug.
    /// </summary>
    [RequireComponent(typeof(PlayerV2_Controller))]
    public class PlayerV2_Look : NetworkBehaviour
    {
        [Header("Settings")]
        public float Sensitivity = 0.15f;
        [Tooltip("Vitesse de rotation maximale (en degrés par seconde) pour éviter que le torse ne tourne trop vite pour la physique.")]
        public float MaxTurnSpeed = 720f;
        public float MinPitch = -85f;
        public float MaxPitch = 85f;

        [Header("Camera Fluidity")]
        [Tooltip("0 = Caméra purement stable (mouvement direct souris). 1 = Caméra attachée au rebond physique de la tête.")]
        [Range(0f, 1f)]
        public float HeadMovementBlend = 0f;

        private PlayerV2_Controller _controller;
        private PlayerInputHandler _input; // On réutilise l'input handler existant
        
        private float _cameraPitch;
        private float _torsoYaw;

        private void Awake()
        {
            _controller = GetComponent<PlayerV2_Controller>();
            _input = GetComponent<PlayerInputHandler>();
        }

        public override void OnStartLocalPlayer()
        {
            Cursor.lockState = CursorLockMode.Locked;
            // Initialiser le yaw avec la rotation actuelle du Torso
            if (_controller.TorsoRigidbody != null)
            {
                _torsoYaw = _controller.TorsoRigidbody.rotation.eulerAngles.y;
            }
        }

        private void Update()
        {
            if (!isOwned) return;
            if (_input == null) return;

            Vector2 lookInput = _input.LookInput;
            
            // Calcul du mouvement désiré
            float targetYawDelta = lookInput.x * Sensitivity;
            float targetPitchDelta = lookInput.y * Sensitivity;

            // Limitation de la vitesse de rotation pour que la physique (tête/cou) puisse suivre
            // On convertit la vitesse max (degrés/seconde) en delta maximum par frame
            float maxDeltaPerFrame = MaxTurnSpeed * Time.deltaTime;
            float clampedYawDelta = Mathf.Clamp(targetYawDelta, -maxDeltaPerFrame, maxDeltaPerFrame);
            float clampedPitchDelta = Mathf.Clamp(targetPitchDelta, -maxDeltaPerFrame, maxDeltaPerFrame);

            _torsoYaw += clampedYawDelta;
            _cameraPitch -= clampedPitchDelta;
            _cameraPitch = Mathf.Clamp(_cameraPitch, MinPitch, MaxPitch);
        }

        private void LateUpdate()
        {
            if (!isOwned) return;

            if (_controller.CameraTransform != null)
            {
                Quaternion finalTargetRot;

                // Cible 0: Totalement stable, pure input mathématique
                Quaternion targetStable = Quaternion.Euler(_cameraPitch, _torsoYaw, 0f);

                if (_controller.HeadController != null && _controller.HeadController.NeckJoints.Length > 0)
                {
                    // La tête prend 70% en physique
                    _controller.HeadController.SetTargetPitch(_cameraPitch);

                    // Cible 1: Rebond physique de la tête
                    Rigidbody headRb = _controller.HeadController.NeckJoints[_controller.HeadController.NeckJoints.Length - 1].GetComponent<Rigidbody>();
                    
                    float headPitch = _cameraPitch * _controller.HeadController.PitchMultiplier;
                    float remainingPitch = _cameraPitch - headPitch;
                    
                    // On compense les 30% restants sur le transform physique de la tête
                    Quaternion targetHead = headRb.rotation * Quaternion.Euler(remainingPitch, 0f, 0f);

                    // Mix entre "FPS Pur" et "Corps physique" (Totalement linéaire)
                    finalTargetRot = Quaternion.Slerp(targetStable, targetHead, HeadMovementBlend);
                }
                else
                {
                    finalTargetRot = targetStable;
                }

                // Application directe de la rotation mixée. 
                // Ne JAMAIS utiliser de Slerp avec Time.deltaTime ici, sinon la caméra "traîne" derrière la physique et crée des saccades.
                _controller.CameraTransform.rotation = finalTargetRot;
            }
        }

        private void FixedUpdate()
        {
            if (!isOwned) return;
            if (_controller.TorsoRigidbody != null)
            {
                // Rotation physique du Torso. Le ConfigurableJoint avec les Hips gardera le torse attaché.
                // S'il y a une pente, le joint forcera l'inclinaison, mais on donne la direction de base.
                Quaternion targetRot = Quaternion.Euler(0f, _torsoYaw, 0f);
                _controller.TorsoRigidbody.MoveRotation(targetRot);
            }
        }
    }
}
