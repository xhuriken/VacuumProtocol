using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Description: Reusable global helper to fetch clean mouse positioning coordinates.
/// Context: Persists across scenes.
/// Justification: Provides a single source of truth for mouse state, fully compatible with the Unity New Input System.
/// </summary>
public class MouseManager : MonoBehaviour
{
    /// <summary>
    /// Description: Static singleton instance of the MouseManager.
    /// Context: Global accessibility.
    /// Justification: Required so local CustomCursorFollower instances can query the mouse.
    /// </summary>
    public static MouseManager Instance { get; private set; }

    [Header("Settings")]
    [Tooltip("Role: Hides the default OS cursor.\nUse Case: Custom UI.\nJustification: Prevents seeing double cursors when a custom one is drawn.")]
    [SerializeField] private bool _hideHardwareCursor = true;

    /// <summary>
    /// Description: Gets whether the custom cursor should be visible based on lockstate.
    /// Context: Property read by cursor followers.
    /// Justification: Hides custom UI cursors if the player is in locked-camera gameplay.
    /// </summary>
    public bool ShouldShowCursor { get; private set; } = true;

    /// <summary>
    /// Description: Unity Awake callback. Establishes the singleton pattern.
    /// Context: Initialization.
    /// Justification: Ensures only one MouseManager exists across scene loads.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Persist mouse manager across scenes
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        UpdateCursorState();
    }

    private void Start()
    {
        UpdateCursorState();
    }

    private void Update()
    {
        UpdateCursorState();
    }

    /// <summary>
    /// Description: Evaluates Unity's cursor lockstate and visibility context.
    /// Context: Called during initialization and frame loop.
    /// Justification: Required to hide the hardware cursor underneath the custom cursor disk.
    /// </summary>
    private void UpdateCursorState()
    {
        // If the system cursor is locked (e.g., first-person gameplay controls are active)
        // we should hide custom cursor disks.
        bool isCursorLocked = Cursor.lockState == CursorLockMode.Locked;
        ShouldShowCursor = !isCursorLocked;

        if (ShouldShowCursor)
        {
            // Hide the default operating system mouse cursor underneath our disk
            if (_hideHardwareCursor && Cursor.visible)
            {
                Cursor.visible = false;
            }
        }
    }

    /// <summary>
    /// Description: Gets the current screen space position of the mouse pointer.
    /// Context: Property accessor.
    /// Justification: Wraps the New Input System call (Mouse.current.position.ReadValue()) safely.
    /// </summary>
    public Vector2 MousePosition
    {
        get
        {
            if (Mouse.current != null)
            {
                return Mouse.current.position.ReadValue();
            }
            return Vector2.zero;
        }
    }
}
