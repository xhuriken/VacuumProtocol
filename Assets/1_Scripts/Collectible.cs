using UnityEngine;

public class Collectible : MonoBehaviour, IEntity
{
    public string Name { get; set; } = "Collectible";
    public int PriorityLevel { get; set; } = 2;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
