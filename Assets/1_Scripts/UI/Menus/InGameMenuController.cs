using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Description: Opens and closes the in-game menu Canvas GameObject directly with the Escape key.
/// Context: Attached to a manager GameObject in the scene (e.g. InGameMenuManager).
/// Justification: Toggles the whole Canvas GameObject, which keeps the state of every sub-panel intact without touching the children.
/// </summary>
public class InGameMenuController : MonoBehaviour
{
    [Header("Canvas Reference")]
    [Tooltip("Role: The pause Canvas GameObject that is activated/deactivated directly.")]
    [SerializeField] private GameObject _menuCanvasObject;

    [Tooltip("Role: Root Canvas used for camera binding (optional if _menuCanvasObject is assigned).")]
    [SerializeField] private Canvas _menuCanvas;

    [Tooltip("Role: Optional navigation group that handles going back (e.g. Settings -> Menu).")]
    [SerializeField] private UINavigationGroup _navigationGroup;

    [Header("State")]
    [Tooltip("Role: Whether the menu is open when the scene starts.")]
    [SerializeField] private bool _startOpened = false;

    [Header("Debug")]
    [Tooltip("Role: Prints this component's informational Debug.Log messages. Warnings and errors always print.")]
    [SerializeField] private bool _enableDebugLogs = false;

    private bool _isMenuOpened = false;

    /// <summary>
    /// Description: True while the pause menu is open.
    /// </summary>
    public bool IsMenuOpened => _isMenuOpened;

    private void Awake()
    {
        if (_menuCanvasObject == null && _menuCanvas != null)
        {
            _menuCanvasObject = _menuCanvas.gameObject;
        }
        else if (_menuCanvas == null && _menuCanvasObject != null)
        {
            _menuCanvas = _menuCanvasObject.GetComponent<Canvas>();
        }

        if (_navigationGroup == null && _menuCanvasObject != null)
        {
            _navigationGroup = _menuCanvasObject.GetComponentInChildren<UINavigationGroup>(true);
        }
    }

    private void Start()
    {
        SetMenuState(_startOpened);

        // If the local player has not spawned yet (NetworkManager HUD / Host / Client), keep the mouse free and visible
        if (!_startOpened && PlayerV2_Controller.LocalInstance == null)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void Update()
    {
        bool escapePressed = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            escapePressed = true;
        }
#else
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            escapePressed = true;
        }
#endif

        if (escapePressed)
        {
            HandleEscape();
        }
    }

    private void HandleEscape()
    {
        if (_isMenuOpened)
        {
            // Inside a sub-panel (e.g. Settings) that can go back: go back first
            if (_navigationGroup != null && _navigationGroup.CanGoBack)
            {
                _navigationGroup.GoBack();
                return;
            }

            // Otherwise close the whole menu
            SetMenuState(false);
        }
        else
        {
            // Open the menu
            SetMenuState(true);
        }
    }

    /// <summary>
    /// Description: Opens the in-game pause menu if closed, closes it if open.
    /// </summary>
    public void TogglePauseMenu()
    {
        SetMenuState(!_isMenuOpened);
    }

    /// <summary>
    /// Description: Activates or deactivates the Canvas GameObject and sets up the cursor and the local player's pause state.
    /// </summary>
    /// <param name="opened">True to open the menu, false to close it.</param>
    public void SetMenuState(bool opened)
    {
        _isMenuOpened = opened;

        // 1. Activate / deactivate the whole Canvas GameObject (children untouched)
        if (_menuCanvasObject != null)
        {
            _menuCanvasObject.SetActive(opened);
        }
        else if (_menuCanvas != null)
        {
            _menuCanvas.gameObject.SetActive(opened);
        }

        if (opened)
        {
            BindCanvasCamera();
        }

        // 2. Cursor and player pause handling
        if (opened)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = false;

            if (PlayerV2_Controller.LocalInstance != null)
            {
                PlayerV2_Controller.LocalInstance.SetPaused(true);
            }
        }
        else
        {
            if (PlayerV2_Controller.LocalInstance != null)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                PlayerV2_Controller.LocalInstance.SetPaused(false);
            }
            else
            {
                // No spawned player -> keep the cursor usable for the Network HUD
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }

    /// <summary>Binds the local player's camera to the menu Canvas and makes sure UI raycasts and an EventSystem work.</summary>
    private void BindCanvasCamera()
    {
        Canvas targetCanvas = _menuCanvas != null ? _menuCanvas : GetComponentInParent<Canvas>();
        if (targetCanvas == null) targetCanvas = GetComponent<Canvas>();

        if (_enableDebugLogs) Debug.Log($"[InGameMenuController] BindCanvasCamera() called. TargetCanvas={(targetCanvas != null ? targetCanvas.name : "NULL")}, RenderMode={(targetCanvas != null ? targetCanvas.renderMode.ToString() : "N/A")}");

        if (targetCanvas != null)
        {
            Camera cam = null;
            if (PlayerV2_Controller.LocalInstance != null)
            {
                if (PlayerV2_Controller.LocalInstance.CameraTransform != null)
                {
                    cam = PlayerV2_Controller.LocalInstance.CameraTransform.GetComponentInChildren<Camera>(true);
                    if (_enableDebugLogs) Debug.Log($"[InGameMenuController] Camera lookup via LocalInstance.CameraTransform ({PlayerV2_Controller.LocalInstance.CameraTransform.name}) -> Result: {(cam != null ? cam.name : "NULL")}");
                }
                if (cam == null)
                {
                    cam = PlayerV2_Controller.LocalInstance.GetComponentInChildren<Camera>(true);
                    if (_enableDebugLogs) Debug.Log($"[InGameMenuController] Camera lookup via LocalInstance root ({PlayerV2_Controller.LocalInstance.name}) -> Result: {(cam != null ? cam.name : "NULL")}");
                }
            }
            else
            {
                Debug.LogWarning("[InGameMenuController] PlayerV2_Controller.LocalInstance is NULL when BindCanvasCamera runs!");
            }

            if (cam == null)
            {
                cam = Camera.main;
                if (_enableDebugLogs) Debug.Log($"[InGameMenuController] Fallback lookup Camera.main -> Result: {(cam != null ? cam.name : "NULL")}");
            }

            if (cam != null)
            {
                targetCanvas.worldCamera = cam;
                targetCanvas.planeDistance = 0.5f;
                if (_enableDebugLogs) Debug.Log($"[InGameMenuController] -> SUCCESS: Camera '{cam.name}' bound to Canvas '{targetCanvas.name}' with planeDistance=0.5");
            }
            else
            {
                Debug.LogError($"[InGameMenuController] ERROR: No camera could be found to bind Canvas '{targetCanvas.name}'!");
            }

            // Make sure the Canvas GraphicRaycaster is not blocked by the player's 3D colliders (head, torso, arms)
            UnityEngine.UI.GraphicRaycaster raycaster = targetCanvas.GetComponent<UnityEngine.UI.GraphicRaycaster>();
            if (raycaster != null)
            {
                raycaster.blockingObjects = UnityEngine.UI.GraphicRaycaster.BlockingObjects.None;
            }

            // Make sure an active EventSystem exists in the scene
            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                var existingEs = FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
                if (existingEs == null)
                {
                    GameObject esObj = new GameObject("EventSystem");
                    esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
#if ENABLE_INPUT_SYSTEM
                    esObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
                    esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif
                    Debug.LogWarning("[InGameMenuController] EventSystem created automatically to handle UI clicks/hover.");
                }
            }
        }
    }
}
