using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Description: Centralized input handler that processes Unity Input System callbacks.
/// Context: Attached to the player prefab.
/// Justification: Acts as a bridge for other modular player components. They read from this script rather than binding directly to the input system, ensuring a single source of truth for input state (useful for pausing, disabling input, or testing).
/// </summary>
    public class PlayerInputHandler : NetworkBehaviour
    {
        /// <summary>Gets the continuous movement vector (X, Y) from WASD or left stick.</summary>
        public Vector2 MoveInput { get; private set; }
        
        /// <summary>Gets the continuous look delta from mouse movement or right stick.</summary>
        public Vector2 LookInput { get; private set; }
        
        /// <summary>Gets a value indicating whether the sprint button is currently held down.</summary>
        public bool IsSprinting { get; private set; }
        
        /// <summary>Gets a value indicating whether the jump button was pressed this frame.</summary>
        public bool JumpTriggered { get; private set; }
        
        /// <summary>Gets a value indicating whether the left arm (primary) action button is held.</summary>
        public bool LeftArmPressed { get; private set; }
        
        /// <summary>Gets a value indicating whether the right arm (secondary) action button is held.</summary>
        public bool RightArmPressed { get; private set; }
        
        /// <summary>
        /// True if both arm buttons are held simultaneously.
        /// </summary>
        public bool IsVacuuming => LeftArmPressed && RightArmPressed;

        [Header("Debug")]
        [Tooltip("Role: Enable input console logs.\nUse Case: Debugging binding issues.\nJustification: Used to trace hardware failures or rebinding errors.")]
        [SerializeField] private bool _showDebugLogs = false;

        private bool _isInitialized = false;

        /// <summary>
        /// Description: Unlocks input listening.
        /// Context: Mirror NetworkBehaviour callback fired when this client assumes authority.
        /// Justification: We drop all input callbacks until the local player is fully authorized.
        /// </summary>
        public override void OnStartLocalPlayer()
        {
            _isInitialized = true;
        }

        /// <summary>
        /// Description: Input callback for movement.
        /// Context: Bound via Unity PlayerInput events.
        /// Justification: Reads continuous vector data.
        /// </summary>
        public void OnMove(InputAction.CallbackContext context)
        {
            if (!_isInitialized) return;
            MoveInput = context.ReadValue<Vector2>();
        }

        /// <summary>
        /// Description: Input callback for camera looking.
        /// Context: Bound via Unity PlayerInput events.
        /// Justification: Reads delta mouse vectors.
        /// </summary>
        public void OnLook(InputAction.CallbackContext context)
        {
            if (!_isInitialized) return;
            LookInput = context.ReadValue<Vector2>();
        }

        /// <summary>
        /// Description: Input callback for sprinting.
        /// Context: Bound via Unity PlayerInput events.
        /// Justification: Interpreted as a held button state.
        /// </summary>
        public void OnSprint(InputAction.CallbackContext context)
        {
            if (!_isInitialized) return;
            IsSprinting = context.ReadValueAsButton();
        }

        /// <summary>
        /// Description: Input callback for jumping.
        /// Context: Bound via Unity PlayerInput events.
        /// Justification: Triggers a single-frame flag.
        /// </summary>
        public void OnJump(InputAction.CallbackContext context)
        {
            if (!_isInitialized) return;
            if (context.performed) JumpTriggered = true;
        }

        /// <summary>
        /// Description: Input callback for the left arm.
        /// Context: Bound via Unity PlayerInput events.
        /// Justification: Interpreted as a held button state.
        /// </summary>
        public void OnLeftArm(InputAction.CallbackContext context)
        {
            if (!_isInitialized) return;
            LeftArmPressed = context.ReadValueAsButton();
            if (_showDebugLogs && LeftArmPressed) Debug.Log("[Input] Left Arm Pressed");
        }

        /// <summary>
        /// Description: Input callback for the right arm.
        /// Context: Bound via Unity PlayerInput events.
        /// Justification: Interpreted as a held button state.
        /// </summary>
        public void OnRightArm(InputAction.CallbackContext context)
        {
            if (!_isInitialized) return;
            RightArmPressed = context.ReadValueAsButton();
            if (_showDebugLogs && RightArmPressed) Debug.Log("[Input] Right Arm Pressed");
        }

        /// <summary>
        /// Description: Cleans up single-frame trigger flags.
        /// Context: LateUpdate lifecycle event.
        /// Justification: Ensures that triggers like 'Jump' do not span across multiple frames and cause double-jumping.
        /// </summary>
        private void LateUpdate()
        {
            if (!_isInitialized) return;
            
            // Reset triggers at the end of the frame
            JumpTriggered = false;
        }
    }
