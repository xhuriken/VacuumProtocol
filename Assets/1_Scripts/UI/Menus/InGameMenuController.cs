using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Description: Contrôle l'activation/désactivation directe du GameObject du Canvas de menu en jeu via la touche Échap.
/// Context: Attaché à un GameObject gestionnaire dans la scène (ex: InGameMenuManager).
/// Justification: Active et désactive le GameObject du Canvas complet, préservant ainsi l'état intact de tous les sous-panels sans altérer les enfants.
/// </summary>
public class InGameMenuController : MonoBehaviour
{
    [Header("Canvas Reference")]
    [Tooltip("Role: Le GameObject du Canvas de pause à activer/désactiver directement.")]
    [SerializeField] private GameObject _menuCanvasObject;

    [Tooltip("Role: Canvas racine pour la liaison de la caméra (optionnel si _menuCanvasObject est assigné).")]
    [SerializeField] private Canvas _menuCanvas;

    [Tooltip("Role: Groupe de navigation optionnel pour gérer les retours arrière (ex: Settings -> Menu).")]
    [SerializeField] private UINavigationGroup _navigationGroup;

    [Header("State")]
    [Tooltip("Role: État d'ouverture au lancement de la scène.")]
    [SerializeField] private bool _startOpened = false;

    private bool _isMenuOpened = false;

    /// <summary>
    /// Description: Indique si le menu pause est actuellement ouvert.
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

        // Si le joueur local n'a pas encore spawn (NetworkManager HUD / Host / Client), laisser la souris libre et visible
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
            // Si on est dans un sous-panel (ex: Settings) et qu'on peut faire Retour, on fait Retour d'abord
            if (_navigationGroup != null && _navigationGroup.CanGoBack)
            {
                _navigationGroup.GoBack();
                return;
            }

            // Sinon on ferme le menu complet
            SetMenuState(false);
        }
        else
        {
            // On ouvre le menu
            SetMenuState(true);
        }
    }

    /// <summary>
    /// Description: Ouvre ou ferme le menu de pause en jeu.
    /// </summary>
    public void TogglePauseMenu()
    {
        SetMenuState(!_isMenuOpened);
    }

    /// <summary>
    /// Description: Active ou désactive le GameObject du Canvas et configure les inputs et curseurs.
    /// </summary>
    public void SetMenuState(bool opened)
    {
        _isMenuOpened = opened;

        // 1. Activer / Désactiver le GameObject du Canvas complet (sans toucher aux enfants)
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

        // 2. Gestion du curseur et des pupilles
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
                // Pas de joueur spawn -> laisser le curseur accessible pour le Network HUD
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }

    private void BindCanvasCamera()
    {
        Canvas targetCanvas = _menuCanvas != null ? _menuCanvas : GetComponentInParent<Canvas>();
        if (targetCanvas == null) targetCanvas = GetComponent<Canvas>();

        Debug.Log($"[InGameMenuController] BindCanvasCamera() appelé. TargetCanvas={(targetCanvas != null ? targetCanvas.name : "NULL")}, RenderMode={(targetCanvas != null ? targetCanvas.renderMode.ToString() : "N/A")}");

        if (targetCanvas != null)
        {
            Camera cam = null;
            if (PlayerV2_Controller.LocalInstance != null)
            {
                if (PlayerV2_Controller.LocalInstance.CameraTransform != null)
                {
                    cam = PlayerV2_Controller.LocalInstance.CameraTransform.GetComponentInChildren<Camera>(true);
                    Debug.Log($"[InGameMenuController] Recherche caméra via LocalInstance.CameraTransform ({PlayerV2_Controller.LocalInstance.CameraTransform.name}) -> Résultat: {(cam != null ? cam.name : "NULL")}");
                }
                if (cam == null)
                {
                    cam = PlayerV2_Controller.LocalInstance.GetComponentInChildren<Camera>(true);
                    Debug.Log($"[InGameMenuController] Recherche caméra via LocalInstance root ({PlayerV2_Controller.LocalInstance.name}) -> Résultat: {(cam != null ? cam.name : "NULL")}");
                }
            }
            else
            {
                Debug.LogWarning("[InGameMenuController] PlayerV2_Controller.LocalInstance est NULL au moment de BindCanvasCamera !");
            }

            if (cam == null)
            {
                cam = Camera.main;
                Debug.Log($"[InGameMenuController] Recherche fallback Camera.main -> Résultat: {(cam != null ? cam.name : "NULL")}");
            }

            if (cam != null)
            {
                targetCanvas.worldCamera = cam;
                targetCanvas.planeDistance = 0.5f;
                Debug.Log($"[InGameMenuController] -> SUCCÈS : Caméra '{cam.name}' liée au Canvas '{targetCanvas.name}' avec planeDistance=0.5");
            }
            else
            {
                Debug.LogError($"[InGameMenuController] ERREUR : Aucune caméra n'a pu être trouvée pour lier le Canvas '{targetCanvas.name}' !");
            }

            // Assurer que le GraphicRaycaster du Canvas ne soit pas bloqué par les colliders 3D du joueur (tête, torse, bras)
            UnityEngine.UI.GraphicRaycaster raycaster = targetCanvas.GetComponent<UnityEngine.UI.GraphicRaycaster>();
            if (raycaster != null)
            {
                raycaster.blockingObjects = UnityEngine.UI.GraphicRaycaster.BlockingObjects.None;
            }

            // Assurer la présence d'un EventSystem actif dans la scène
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
                    Debug.Log("[InGameMenuController] EventSystem créé automatiquement pour la gestion des clics/hover UI.");
                }
            }
        }
    }
}
