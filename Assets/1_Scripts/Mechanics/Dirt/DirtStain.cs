using UnityEngine;
using Mirror;

namespace VacuumProtocol.Mechanics.Dirt
{
    /// <summary>
    /// Représente une tache de saleté sur le mur. 
    /// Le système d'aspiration la détecte et la vide progressivement au lieu de la déplacer.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(Collider))]
    public class DirtStain : NetworkBehaviour
    {
        [Header("Dirt Settings")]
        [Tooltip("La quantité totale de poussière/saleté que contient cette tache.")]
        public float MaxDirtAmount = 1000f;
        
        [Tooltip("Les sprites de dégradation (du plus sale au plus propre).")]
        public Sprite[] DegradationSprites;

        [SyncVar(hook = nameof(OnDirtAmountChanged))]
        private float _currentDirtAmount;

        private SpriteRenderer _spriteRenderer;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        public override void OnStartServer()
        {
            _currentDirtAmount = MaxDirtAmount;
        }

        /// <summary>
        /// Appelée par le Serveur lorsqu'un joueur aspire cette tache.
        /// Réduit la quantité et renvoie la quantité réellement aspirée.
        /// </summary>
        [Server]
        public float DrainDirt(float requestedAmount, NetworkConnectionToClient playerConn)
        {
            if (_currentDirtAmount <= 0) return 0f;

            float drained = Mathf.Min(requestedAmount, _currentDirtAmount);
            _currentDirtAmount -= drained;

            // Ajoute le score au joueur via le Manager Global
            if (DirtMetricsManager.Instance != null)
            {
                DirtMetricsManager.Instance.AddDirtToPlayer(playerConn, drained);
            }

            if (_currentDirtAmount <= 0)
            {
                NetworkServer.Destroy(gameObject);
            }

            return drained;
        }

        /// <summary>
        /// Hook réseau : Met à jour visuellement le sprite selon l'état d'aspiration.
        /// </summary>
        private void OnDirtAmountChanged(float oldVal, float newVal)
        {
            if (DegradationSprites == null || DegradationSprites.Length == 0) return;

            float ratio = newVal / MaxDirtAmount; // 1.0 (plein) à 0.0 (vide)
            
            // On map le ratio sur les index du tableau
            int index = Mathf.FloorToInt((1f - ratio) * DegradationSprites.Length);
            index = Mathf.Clamp(index, 0, DegradationSprites.Length - 1);
            
            _spriteRenderer.sprite = DegradationSprites[index];
        }
    }
}
