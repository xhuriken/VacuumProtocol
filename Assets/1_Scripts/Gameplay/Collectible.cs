using UnityEngine;

/// <summary>
/// Represents a generic item in the world that can be detected by entities like the player.
/// </summary>
public class Collectible : MonoBehaviour, IEntity
{
    public string Name { get; set; } = "Collectible";
    public int PriorityLevel { get; set; } = 2;
    public Transform LookAtPoint => transform;

    void Start()
    {
    }

    void Update()
    {
    }
}
