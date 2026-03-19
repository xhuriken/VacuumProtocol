using Mirror;
using UnityEngine;

public class Player : NetworkBehaviour, IEntity
{
    public string Name { get; set; } = "Player";
    public int PriorityLevel { get; set; } = 3;

    /// <summary>
    /// Initialise les actions et le nom de l'objet.
    /// </summary>
    public override void OnStartClient()
    {
        string status = isLocalPlayer ? "local" : "remote";
        name = $"Player[{netId}|{status}]";
    }

    /// <summary>
    /// Initialise le nom côté serveur.
    /// </summary>
    public override void OnStartServer()
    {
        name = $"Player[{netId}|server]";
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
