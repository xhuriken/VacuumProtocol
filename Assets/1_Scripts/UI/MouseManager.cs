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
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
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
