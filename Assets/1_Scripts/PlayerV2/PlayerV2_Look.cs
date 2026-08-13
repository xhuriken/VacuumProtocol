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
        public float MinPitch = -85f;
        public float MaxPitch = 85f;

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
            
            _torsoYaw += lookInput.x * Sensitivity;
            _cameraPitch -= lookInput.y * Sensitivity;
            _cameraPitch = Mathf.Clamp(_cameraPitch, MinPitch, MaxPitch);
        }

        private void LateUpdate()
        {
            if (!isOwned) return;
            if (_controller.CameraTransform != null)
            {
                // La caméra gère le pitch (haut/bas) localement.
                // Le yaw est géré par le TorsoRigidbody, donc la caméra hérite du yaw.
                _controller.CameraTransform.localRotation = Quaternion.Euler(_cameraPitch, 0f, 0f);
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
