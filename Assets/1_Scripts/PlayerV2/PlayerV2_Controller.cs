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
        [Header("Physics Bodies")]
        [Tooltip("Role: Rigidbody de la base (mouvement).\nUse Case: Déplacement et attache des roues.")]
        public Rigidbody HipsRigidbody;

        [Tooltip("Role: Rigidbody de la tourelle (vue).\nUse Case: Rotation infinie libre de la caméra.")]
        public Rigidbody TorsoRigidbody;

        [Header("Camera & Visuals")]
        [Tooltip("Role: La caméra du joueur.\nUse Case: Assignée au script de Look.")]
        public Transform CameraTransform;

        private void Start()
        {
            if (HipsRigidbody == null || TorsoRigidbody == null)
            {
                Debug.LogError("[PlayerV2_Controller] HipsRigidbody ou TorsoRigidbody manquants ! Assignez-les dans l'inspecteur.");
            }
        }
    }
}
