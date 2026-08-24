using Mirror;
using UnityEngine;

/// <summary>
/// Description: Hub principal pour le joueur V2 (Multi-Body).
/// Context: Attaché à la racine du Prefab Player_V2.
/// Justification: Centralise les références critiques pour éviter que les sous-scripts fassent des GetComponent redondants ou complexes.
/// </summary>
public class PlayerV2_Controller : NetworkBehaviour, IEntity
{

    #region Entity
    [Header("Entity Settings")]
    [Tooltip("Role: The display name of the collectible.\nUse Case: UI Label rendering.\nJustification: Gives the player readable text when looking at the object.")]
    [SerializeField]
    private string _name = "PlayerName";

    [Tooltip("Role: Priority level for entity detection systems.\nUse Case: Vision targeting.\nJustification: Allows important items to take focus over generic clutter.")]
    [SerializeField]
    private int _priorityLevel = 1;

    /// <summary>
    /// Description: Gets or sets the display name of the entity.
    /// Context: Satisfies IEntity interface. Used by UI to render text.
    /// Justification: Wrapped in a property to allow potential future localization hooks.
    /// </summary>
    public string Name
    {
        get
        {
            return _name;
        }
        set
        {
            _name = value;
        }
    }

    /// <summary>
    /// Description: Gets or sets the priority level for detection (higher means more important).
    /// Context: Satisfies IEntity interface. Used by PlayerV2_Look.
    /// Justification: Resolves conflicts when multiple objects are within the player's field of view.
    /// </summary>
    public int PriorityLevel
    {
        get
        {
            return _priorityLevel;
        }
        set
        {
            _priorityLevel = value;
        }
    }

    [Tooltip("Role: Transform target for eye-tracking.\nUse Case: Look direction.\nJustification: Specifies exactly where other entities should focus when looking at this player (usually the camera/head).")]
    [SerializeField] private Transform _lookAtPoint;

    /// <summary>
    /// Description: Gets the point to look at on this entity.
    /// Context: IEntity implementation.
    /// Justification: Assigned dynamically to the camera for local players, and falls back to a prefab transform for remote players.
    /// </summary>
    public Transform LookAtPoint
    {

        get => _lookAtPoint;

        private set => _lookAtPoint = value;

    }
    #endregion

    [Tooltip("Role: The network connection ID of this client.\nUse Case: Mirror syncing.\nJustification: Allows scripts like the voice chat system to map this specific avatar to a UniVoice network stream.")]
    [SyncVar] public int ConnectionId = -1;

    [Header("Network States")]
    [SyncVar(hook = nameof(OnCrouchStateChanged))] 
    public bool IsCrouching;

    [Command]
    public void CmdSetCrouching(bool crouchState)
    {
        IsCrouching = crouchState;
    }

    private void OnCrouchStateChanged(bool oldVal, bool newVal)
    {
        if (HeadController != null && ArmsController != null)
        {
            if (newVal)
            {
                float crouchOffset = -0.5f;
                var movement = GetComponent<PlayerV2_Movement>();
                if (movement != null) crouchOffset = movement.CrouchHeadOffset;

                HeadController.SetHeadHeightOffset(crouchOffset);
                ArmsController.SetArmRetraction(true);
            }
            else
            {
                HeadController.SetHeadHeightOffset(0f);
                ArmsController.SetArmRetraction(false);
            }
        }
    }

    [Header("Physics Bodies")]
    [Tooltip("Role: Rigidbody de la base (mouvement).\nUse Case: Déplacement et attache des roues.")]
    public Rigidbody HipsRigidbody;

    [Tooltip("Role: Rigidbody de la tourelle (vue).\nUse Case: Rotation infinie libre de la caméra.")]
    public Rigidbody TorsoRigidbody;

    [Header("Camera & Visuals")]
    [Tooltip("Role: La caméra du joueur.\nUse Case: Assignée au script de Look.")]
    public Transform CameraTransform;

    [Tooltip("Role: Contrôleur de la tête et du cou.\nUse Case: Transmission du pitch calculé par le Look.")]
    public PlayerV2_Head HeadController;

    [Header("Arms System")]
    [Tooltip("Role: Racine physique du bras gauche.\nUse Case: Traversal et distance max.")]
    public Transform LeftArmRoot;

    [Tooltip("Role: Racine physique du bras droit.\nUse Case: Traversal et distance max.")]
    public Transform RightArmRoot;

    [Tooltip("Role: Épaule gauche visuelle/physique.\nUse Case: Rotation à 90° en extension.")]
    public Transform LeftShoulder;

    [Tooltip("Role: Épaule droite visuelle/physique.\nUse Case: Rotation à -90° en extension.")]
    public Transform RightShoulder;

    [Tooltip("Role: Contrôleur physique des bras.\nUse Case: Référence centralisée pour d'autres systèmes.")]
    public PlayerV2_Arms ArmsController;

    private void Start()
    {
        if (HipsRigidbody == null || TorsoRigidbody == null)
        {
            Debug.LogError("[PlayerV2_Controller] HipsRigidbody ou TorsoRigidbody manquants ! Assignez-les dans l'inspecteur.");
        }

        // Désactiver la caméra et l'AudioListener pour les joueurs distants
        if (!isOwned && CameraTransform != null)
        {
            Camera cam = CameraTransform.GetComponent<Camera>();
            if (cam != null) cam.enabled = false;


            AudioListener listener = CameraTransform.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = false;
        }
    }
}

