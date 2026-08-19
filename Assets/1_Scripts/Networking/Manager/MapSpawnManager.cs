using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Description: Gère dynamiquement les points d'apparition (spawn) des joueurs dans une carte.
/// Context: Script à placer dans la scène de jeu (Level).
/// Justification: Permet de définir facilement les positions et orientations de spawn pour chaque joueur depuis l'inspecteur, jusqu'à 10 joueurs.
/// </summary>
public class MapSpawnManager : MonoBehaviour
{
    /// <summary>
    /// Description: Instance statique (Singleton) pour un accès global facile depuis le NetworkManager.
    /// </summary>
    public static MapSpawnManager Instance { get; private set; }

    [Tooltip("Role: Liste des points d'apparition disponibles dans la scène.\nUse Case: Assigner les Transforms de spawn (position et rotation).\nJustification: Permet un spawn dynamique selon le nombre de joueurs.")]
    [SerializeField]

    private List<Transform> _spawnPoints = new List<Transform>();

    private int _nextSpawnIndex = 0;

    private void Awake()
    {
        // Initialize the Singleton instance
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }

        Instance = this;

        // Auto-register spawn points for standard Mirror NetworkManager (e.g. HUD testing)
        foreach (Transform spawnPoint in _spawnPoints)
        {
            if (spawnPoint != null)
            {
                Mirror.NetworkManager.RegisterStartPosition(spawnPoint);
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        // Unregister spawn points
        foreach (Transform spawnPoint in _spawnPoints)
        {
            if (spawnPoint != null)
            {
                Mirror.NetworkManager.UnRegisterStartPosition(spawnPoint);
            }
        }
    }

    /// <summary>
    /// Description: Récupère le prochain point d'apparition disponible.
    /// Context: Appelé par le NetworkManager lors du spawn d'un joueur en jeu.
    /// Justification: Distribue les joueurs sur les différents points de spawn de manière circulaire.
    /// </summary>
    /// <returns>Transform du point d'apparition, ou null si aucun n'est configuré.</returns>
    public Transform GetNextSpawnPoint()
    {
        if (_spawnPoints == null || _spawnPoints.Count == 0)
        {
            Debug.LogError("[MapSpawnManager] Aucun point de spawn n'est configuré dans la liste _spawnPoints !");
            return null;
        }

        Transform spawnPoint = _spawnPoints[_nextSpawnIndex];

        // Increment the index circularly to distribute players

        _nextSpawnIndex = (_nextSpawnIndex + 1) % _spawnPoints.Count;

        return spawnPoint;
    }
}
