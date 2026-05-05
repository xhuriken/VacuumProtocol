using UnityEngine;

public interface IEntity
{
    public string Name { get; set; }
    public int PriorityLevel { get; set; }
    GameObject gameObject { get; }
    Transform LookAtPoint { get; }
}
