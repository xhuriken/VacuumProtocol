using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
    /// Centralized input handler that processes Unity Input System callbacks.
    /// Acts as a bridge for other modular player components.
    /// </summary>
    public class PlayerInputHandler : NetworkBehaviour
    {
        public Vector2 MoveInput { get; private set; }
        public Vector2 LookInput { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool JumpTriggered { get; private set; }
        public bool LeftArmPressed { get; private set; }
        public bool RightArmPressed { get; private set; }
        
        /// <summary>
        /// True if both arm buttons are held simultaneously.
        /// </summary>
        public bool IsVacuuming => LeftArmPressed && RightArmPressed;

        [Header("Debug")]
        [SerializeField] private bool _showDebugLogs = false;

        private bool _isInitialized = false;

        public override void OnStartLocalPlayer()
        {
            _isInitialized = true;
        }

        public void OnMove(InputAction.CallbackContext context)
        {
            if (!_isInitialized) return;
            MoveInput = context.ReadValue<Vector2>();
        }

        public void OnLook(InputAction.CallbackContext context)
        {
            if (!_isInitialized) return;
            LookInput = context.ReadValue<Vector2>();
        }

        public void OnSprint(InputAction.CallbackContext context)
        {
            if (!_isInitialized) return;
            IsSprinting = context.ReadValueAsButton();
        }

        public void OnJump(InputAction.CallbackContext context)
        {
            if (!_isInitialized) return;
            if (context.performed) JumpTriggered = true;
        }

        public void OnLeftArm(InputAction.CallbackContext context)
        {
            if (!_isInitialized) return;
            LeftArmPressed = context.ReadValueAsButton();
            if (_showDebugLogs && LeftArmPressed) Debug.Log("[Input] Left Arm Pressed");
        }

        public void OnRightArm(InputAction.CallbackContext context)
        {
            if (!_isInitialized) return;
            RightArmPressed = context.ReadValueAsButton();
            if (_showDebugLogs && RightArmPressed) Debug.Log("[Input] Right Arm Pressed");
        }

        private void LateUpdate()
        {
            if (!_isInitialized) return;
            
            // Reset triggers at the end of the frame
            JumpTriggered = false;
        }
    }
