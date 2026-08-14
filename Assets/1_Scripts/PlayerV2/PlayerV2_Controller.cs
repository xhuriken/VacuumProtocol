using UnityEngine;
using Mirror;

namespace VacuumProtocol.PlayerV2
{
    /// <summary>
    /// Description: Hub principal pour le joueur V2 (Multi-Body).
    /// Context: Attaché à la racine du Prefab Player_V2.
    /// Justification: Centralise les références critiques pour éviter que les sous-scripts fassent des GetComponent redondants ou complexes.
    /// </summary>
    public class PlayerV2_Controller : NetworkBehaviour
    {
        [Tooltip("Role: The network connection ID of this client.\nUse Case: Mirror syncing.\nJustification: Allows scripts like the voice chat system to map this specific avatar to a UniVoice network stream.")]
        [SyncVar] public int ConnectionId = -1;

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
        }
    }
}
