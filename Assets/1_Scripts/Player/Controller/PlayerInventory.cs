using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// Manages the player's collected items inventory.
/// Handles item storage (deactivation) and launching (spitting) over the network.
/// </summary>
public class PlayerInventory : NetworkBehaviour
{
    [Header("Inventory Settings")]
    [Tooltip("Maximum number of items the inventory can hold.")]
    [SerializeField]
    private int _maxCapacity = 10;

    [Tooltip("Force applied when spitting an item out.")]
    [SerializeField]
    private float _spitForce = 15f;

    [Header("Debug & Diagnostics")]
    [Tooltip("Enables debug logs for inventory actions.")]
    [SerializeField]
    private bool _enableDebugLogs = true;

    // Server-side storage for vacuumed GameObjects
    private readonly List<GameObject> _storedItems = new List<GameObject>();

    // Synced inventory count for client-side queries
    [SyncVar]
    private int _syncedItemCount = 0;

    /// <summary>
    /// Gets the current number of items in the inventory.
    /// </summary>
    public int ItemCount
    {
        get
        {
            return isServer ? _storedItems.Count : _syncedItemCount;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the inventory is full.
    /// </summary>
    public bool IsFull
    {
        get
        {
            return ItemCount >= _maxCapacity;
        }
    }

    /// <summary>
    /// Adds an object to the inventory. Must be called on the server.
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
    /// Spits out the last absorbed item from the inventory in the specified direction.
    /// Must be called on the server.
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
