using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Very small item bag for puzzle items (key pieces, keys...). Add it to the player.
/// Picks up Key items automatically (GameItem with type Key adds its keyId).
/// NPCs and locks use Add / Has / Remove.
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory Instance { get; private set; }

    /// <summary>Raised when something is added (true) or removed (false).</summary>
    public event Action<string, bool> OnChanged;

    [SerializeField] private List<string> startingItems = new List<string>();
    private readonly Dictionary<string, int> items = new Dictionary<string, int>();

    void Awake()
    {
        Instance = this;
        foreach (var id in startingItems) Add(id);
    }

    void OnEnable() { GameItem.OnCollected += HandleCollected; }
    void OnDisable() { GameItem.OnCollected -= HandleCollected; }

    void HandleCollected(GameItem item, GameObject collector)
    {
        if (item.type == ItemType.Key && !string.IsNullOrEmpty(item.keyId) && collector.transform.IsChildOf(transform))
            Add(item.keyId);
    }

    public void Add(string id, int amount = 1)
    {
        if (string.IsNullOrEmpty(id)) return;
        items[id] = Count(id) + amount;
        OnChanged?.Invoke(id, true);
    }

    public bool Has(string id) => Count(id) > 0;

    public int Count(string id) => items.TryGetValue(id, out int n) ? n : 0;

    public bool Remove(string id, int amount = 1)
    {
        if (Count(id) < amount) return false;
        items[id] = Count(id) - amount;
        OnChanged?.Invoke(id, false);
        return true;
    }
}
