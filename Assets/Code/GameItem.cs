using System;
using UnityEngine;

public enum ItemType
{
    Heal,
    Hint,
    Key,
    Treasure,
    Custom
}

/// <summary>
/// A collectable item. Spawned by ItemGenerator, or put on any object by hand.
/// Needs a trigger Collider; the player (tag below) collects it by walking into it.
/// Listen to OnCollected from your own scripts (health, UI, doors) to react to pickups.
/// </summary>
public class GameItem : MonoBehaviour
{
    /// <summary>Raised when the player collects any item. Hook health / hint UI / inventory up to this.</summary>
    public static event Action<GameItem, GameObject> OnCollected;

    [Header("Item")]
    public ItemType type = ItemType.Heal;
    public string itemName = "Item";
    [Tooltip("Heal: HP restored. Treasure: score value. Ignored by Hint.")]
    public float amount = 25f;
    [Tooltip("Hint: text shown to the player.")]
    [TextArea] public string hintText;
    [Tooltip("Key: id that matches a door / lock, e.g. \"RedKey\".")]
    public string keyId;
    [Tooltip("Custom: free-form id for your own puzzle logic.")]
    public string customId;

    [Header("Collecting")]
    public string playerTag = "Player";
    public bool destroyOnCollect = true;

    [Header("Idle Animation")]
    public bool bob = true;
    public float bobHeight = 0.15f;
    public float bobSpeed = 2f;
    public float spinSpeed = 60f;

    private Vector3 startPos;

    void Start()
    {
        startPos = transform.position;
        if (GetComponentInChildren<Collider>(true) == null)
            Debug.LogWarning($"GameItem '{itemName}' has no Collider, so it can't be picked up. Add one (tick Is Trigger).", this);
    }

    void Update()
    {
        if (spinSpeed != 0f) transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
        if (bob)
        {
            var p = startPos;
            p.y += Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = p;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        // Works when the collider is NOT a trigger: touching it picks it up too.
        if (!PlayerFinder.IsPlayer(collision.collider, playerTag)) return;
        Collect(collision.gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!PlayerFinder.IsPlayer(other, playerTag)) return;
        Collect(other.gameObject);
    }

    public void Collect(GameObject collector)
    {
        switch (type)
        {
            case ItemType.Heal:     Debug.Log($"{itemName}: heal {amount}"); break;
            case ItemType.Hint:     Debug.Log($"{itemName}: hint \"{hintText}\""); break;
            case ItemType.Key:      Debug.Log($"{itemName}: key \"{keyId}\""); break;
            case ItemType.Treasure: Debug.Log($"{itemName}: treasure worth {amount}"); break;
            case ItemType.Custom:   Debug.Log($"{itemName}: custom \"{customId}\""); break;
        }

        OnCollected?.Invoke(this, collector);
        if (destroyOnCollect) Destroy(gameObject);
    }
}
