using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// Description: Manages the player's collected items inventory.
/// Context: Attached to the player prefab.
/// Justification: Centralizes state for vacuumed items. Handles server-side authority for deactivating absorbed items and launching (spitting) them back into the world.
/// </summary>
public class PlayerInventory : NetworkBehaviour
{
    [Header("Inventory Settings")]
    [Tooltip("Role: Maximum number of items the inventory can hold.\nUse Case: Capacity limit.\nJustification: Prevents players from hoovering up the entire map, enforcing inventory management mechanics.")]
    [SerializeField]
    private int _maxCapacity = 10;

    [Tooltip("Role: Force applied when spitting an item out.\nUse Case: Physics impulse.\nJustification: Determines how far an item shoots out when expelled.")]
    [SerializeField]
    private float _spitForce = 15f;

    [Header("Debug & Diagnostics")]
    [Tooltip("Role: Enables debug logs for inventory actions.\nUse Case: Network tracing.\nJustification: Helps diagnose desync issues where an item is absorbed locally but not on the server.")]
    [SerializeField]
    private bool _enableDebugLogs = true;

    // Server-side storage for vacuumed GameObjects
    private readonly List<GameObject> _storedItems = new List<GameObject>();

    // Synced inventory count for client-side queries
    [SyncVar]
    private int _syncedItemCount = 0;

    /// <summary>
    /// Description: Gets the current number of items in the inventory.
    /// Context: Used by UI and internal logic.
    /// Justification: Checks local server list directly on the server, but relies on a SyncVar for clients to ensure accurate counts without replicating the full GameObject list over the network.
    /// </summary>
    public int ItemCount
    {
        get
        {
            return isServer ? _storedItems.Count : _syncedItemCount;
        }
    }

    /// <summary>
    /// Description: Gets a value indicating whether the inventory is full.
    /// Context: Checked before allowing new items to be vacuumed.
    /// Justification: Enforces the _maxCapacity limit dynamically.
    /// </summary>
    public bool IsFull
    {
        get
        {
            return ItemCount >= _maxCapacity;
        }
    }

    /// <summary>
    /// Description: Adds an object to the inventory.
    /// Context: Must be called on the server by the PlayerVacuumController.
    /// Justification: Security. Only the server can validate capacity limits and permanently deactivate networked objects.
    /// </summary>
    /// <param name="item">The GameObject to add.</param>
    /// <returns>True if successfully added; false otherwise.</returns>
    [Server]
    public bool AddItem(GameObject item)
    {
        if (item == null || IsFull)
        {
            return false;
        }

        _storedItems.Add(item);
        _syncedItemCount = _storedItems.Count;

        // Parent the item to the player so it moves with them, and deactivate it
        item.transform.SetParent(transform);
        item.transform.localPosition = Vector3.zero;
        item.SetActive(false);

        if (_enableDebugLogs)
        {
            Debug.Log($"[PlayerInventory] Absorbed item '{item.name}'. Inventory Count: {_storedItems.Count}/{_maxCapacity}");
        }

        return true;
    }

    /// <summary>
    /// Description: Spits out the last absorbed item from the inventory in the specified direction.
    /// Context: Must be called on the server when the player triggers the spit action.
    /// Justification: Employs a Last-In-First-Out (LIFO) stack mechanic for spitting. Detaches the object from the player, reactivates it on the network, and applies physics impulse.
    /// </summary>
    /// <param name="spawnPosition">World position where the item should be activated.</param>
    /// <param name="spitDirection">World direction to launch the item.</param>
    /// <returns>True if an item was successfully spat; false otherwise.</returns>
    [Server]
    public bool SpitItem(Vector3 spawnPosition, Vector3 spitDirection)
    {
        if (_storedItems.Count == 0)
        {
            return false;
        }

        // Retrieve the last item (LIFO behavior)
        int lastIndex = _storedItems.Count - 1;
        GameObject item = _storedItems[lastIndex];
        _storedItems.RemoveAt(lastIndex);
        _syncedItemCount = _storedItems.Count;

        if (item == null)
        {
            return false;
        }

        // Detach the item, position it, and reactivate it
        item.transform.SetParent(null);
        item.transform.position = spawnPosition;
        item.SetActive(true);

        // Reset scale in case it was stored mid-shrink
        Collectible collectible = item.GetComponent<Collectible>();
        if (collectible != null)
        {
            collectible.ResetScale();
        }

        // Apply spit force to the Rigidbody
        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Reset velocities
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.AddForce(spitDirection * _spitForce, ForceMode.Impulse);
        }

        if (_enableDebugLogs)
        {
            Debug.Log($"[PlayerInventory] Spat out item '{item.name}'. Inventory Count: {_storedItems.Count}/{_maxCapacity}");
        }

        return true;
    }
}
