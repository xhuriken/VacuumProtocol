using UnityEngine;
using Mirror;
using System.Collections.Generic;

namespace VacuumProtocol.Mechanics.Dirt
{
    /// <summary>
    /// Gère les statistiques de poussière de l'équipe et des joueurs.
    /// Doit être placé sur un objet de la scène ou le NetworkManager.
    /// </summary>
    public class DirtMetricsManager : NetworkBehaviour
    {
        public static DirtMetricsManager Instance { get; private set; }

        [Header("Metrics")]
        [SyncVar]
        private float _teamTotalDirt;
        
        public float TeamTotalDirt => _teamTotalDirt;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        /// <summary>
        /// Appelée par le serveur lorsqu'une tache est aspirée.
        /// </summary>
        [Server]
        public void AddDirtToPlayer(NetworkConnectionToClient conn, float amount)
        {
            _teamTotalDirt += amount;
            TargetSaveDirtLocal(conn, amount);
        }

        /// <summary>
        /// RPC envoyée spécifiquement au client qui a aspiré pour qu'il sauvegarde en local.
        /// (Plus tard, on pourra l'envoyer à une API serveur au lieu des PlayerPrefs).
        /// </summary>
        [TargetRpc]
        private void TargetSaveDirtLocal(NetworkConnectionToClient target, float amount)
        {
            float currentLocal = PlayerPrefs.GetFloat("MyTotalDirt", 0f);
            float newTotal = currentLocal + amount;
            
            PlayerPrefs.SetFloat("MyTotalDirt", newTotal);
            PlayerPrefs.Save();
            
            Debug.Log($"[DirtMetrics] Tache aspirée ! +{amount} saleté. Total perso : {newTotal}");
        }
    }
}
