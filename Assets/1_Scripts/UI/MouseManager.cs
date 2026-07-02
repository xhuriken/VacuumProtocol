using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Reusable global helper to fetch clean mouse positioning coordinates.
/// Fully compatible with the Unity New Input System.
/// </summary>
public class MouseManager : MonoBehaviour
{
    /// <summary>
    /// Static singleton instance of the MouseManager for global accessibility.
    /// </summary>
    public static MouseManager Instance { get; private set; }

    /// <summary>
    /// Unity Awake callback. Establishes the singleton pattern.
    /// </summary>
    [Header("Settings")]
    [Tooltip("If true, the default system cursor will be hidden when the custom cursor is active.")]
    [SerializeField] private bool _hideHardwareCursor = true;

    /// <summary>
    /// Gets whether the custom cursor should be visible based on lockstate.
    /// True if mouse is unlocked (UI menus), false if locked (FPS gameplay).
    /// </summary>
    public bool ShouldShowCursor { get; private set; } = true;

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
    /// Evaluates Unity's cursor lockstate and visibility context.
    /// Hide hardware cursor underneath the custom cursor disk.
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
    /// Gets the current screen space position of the mouse pointer.
    /// Retrieves coordinates from the New Input System package.
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
