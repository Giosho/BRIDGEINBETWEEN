using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Generates game items (heal, hint, key, treasure, custom) in the scene.
/// Add it to an empty object, fill in the Item Types list, pick a spawn mode, and press Play
/// (or use the "Spawn Items Now" / "Clear Items" entries in the component's context menu).
/// </summary>
public class ItemGenerator : MonoBehaviour
{
    [Serializable]
    public class ItemDefinition
    {
        public string name = "Item";
        public ItemType type = ItemType.Heal;
        [Tooltip("Your own prefab. Leave empty to use a coloured placeholder shape.")]
        public GameObject prefab;
        public Color placeholderColor = Color.white;
        [Tooltip("Higher weight = spawns more often in Random mode.")]
        [Min(0f)] public float weight = 1f;
        [Tooltip("Heal: HP restored. Treasure: score value.")]
        public float amount = 25f;
        [TextArea] public string hintText;
        public string keyId;
        public string customId;
    }

    public enum SpawnMode
    {
        /// <summary>One of each definition, placed on the spawn points in order.</summary>
        EachOnSpawnPoints,
        /// <summary>Weighted random picks, placed on random spawn points.</summary>
        RandomOnSpawnPoints,
        /// <summary>Weighted random picks, placed randomly inside the area box.</summary>
        RandomInArea
    }

    [Header("Items")]
    public List<ItemDefinition> itemTypes = new List<ItemDefinition>
    {
        new ItemDefinition { name = "Water Jar", type = ItemType.Heal, placeholderColor = new Color(0.3f, 0.8f, 1f), amount = 25f, weight = 3f },
        new ItemDefinition { name = "Papyrus Hint", type = ItemType.Hint, placeholderColor = new Color(0.95f, 0.85f, 0.5f), hintText = "Follow the light.", weight = 2f },
        new ItemDefinition { name = "Golden Key", type = ItemType.Key, placeholderColor = new Color(1f, 0.8f, 0.1f), keyId = "GoldKey", weight = 1f },
    };

    [Header("Spawning")]
    public SpawnMode mode = SpawnMode.RandomInArea;
    public bool spawnOnStart = true;
    [Min(1)] public int count = 5;
    [Tooltip("Spawn points for the *OnSpawnPoints modes. Falls back to this object's children.")]
    public List<Transform> spawnPoints = new List<Transform>();
    [Tooltip("Random in Area: box centred on this object.")]
    public Vector3 areaSize = new Vector3(10f, 0f, 10f);
    [Tooltip("Random in Area: raycast down to the ground so items rest on the floor.")]
    public bool snapToGround = true;
    public float groundOffset = 0.5f;
    public LayerMask groundMask = ~0;
    [Tooltip("Optional seed (0 = random every run) so a level can spawn the same layout each time.")]
    public int seed = 0;

    private readonly List<GameObject> spawned = new List<GameObject>();

    void Start()
    {
        if (spawnOnStart) SpawnItems();
    }

    [ContextMenu("Spawn Items Now")]
    public void SpawnItems()
    {
        ClearItems();
        if (itemTypes.Count == 0) { Debug.LogWarning("ItemGenerator: no item types defined.", this); return; }

        var prevState = Random.state;
        if (seed != 0) Random.InitState(seed);

        switch (mode)
        {
            case SpawnMode.EachOnSpawnPoints:
                var points = GetSpawnPoints();
                for (int i = 0; i < itemTypes.Count && i < points.Count; i++)
                    Spawn(itemTypes[i], points[i].position);
                break;

            case SpawnMode.RandomOnSpawnPoints:
                var pool = GetSpawnPoints();
                for (int i = 0; i < count && pool.Count > 0; i++)
                {
                    int idx = Random.Range(0, pool.Count);
                    Spawn(PickWeighted(), pool[idx].position);
                    pool.RemoveAt(idx); // one item per point
                }
                break;

            case SpawnMode.RandomInArea:
                for (int i = 0; i < count; i++)
                    Spawn(PickWeighted(), RandomAreaPoint());
                break;
        }

        if (seed != 0) Random.state = prevState;
    }

    [ContextMenu("Clear Items")]
    public void ClearItems()
    {
        foreach (var go in spawned)
        {
            if (go == null) continue;
            if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
        }
        spawned.Clear();
    }

    /// <summary>Spawn one specific item definition at a world position.</summary>
    public GameItem Spawn(ItemDefinition def, Vector3 position)
    {
        GameObject go;
        if (def.prefab != null)
        {
            go = Instantiate(def.prefab, position, Quaternion.identity, transform);
        }
        else
        {
            go = BuildPlaceholder(def);
            go.transform.SetPositionAndRotation(position, Quaternion.identity);
            go.transform.SetParent(transform, true);
        }
        go.name = def.name;

        var item = go.GetComponent<GameItem>();
        if (item == null) item = go.AddComponent<GameItem>();
        item.type = def.type;
        item.itemName = def.name;
        item.amount = def.amount;
        item.hintText = def.hintText;
        item.keyId = def.keyId;
        item.customId = def.customId;

        var col = go.GetComponent<Collider>();
        if (col == null) col = go.AddComponent<SphereCollider>();
        col.isTrigger = true;

        spawned.Add(go);
        return item;
    }

    GameObject BuildPlaceholder(ItemDefinition def)
    {
        PrimitiveType shape;
        switch (def.type)
        {
            case ItemType.Heal:     shape = PrimitiveType.Capsule; break;
            case ItemType.Hint:     shape = PrimitiveType.Cube;    break;
            case ItemType.Key:      shape = PrimitiveType.Cylinder; break;
            default:                shape = PrimitiveType.Sphere;  break;
        }
        var go = GameObject.CreatePrimitive(shape);
        go.transform.localScale = Vector3.one * 0.5f;
        go.GetComponent<Renderer>().material.color = def.placeholderColor;
        return go;
    }

    ItemDefinition PickWeighted()
    {
        float total = 0f;
        foreach (var d in itemTypes) total += d.weight;
        if (total <= 0f) return itemTypes[Random.Range(0, itemTypes.Count)];

        float roll = Random.value * total;
        foreach (var d in itemTypes)
        {
            roll -= d.weight;
            if (roll <= 0f) return d;
        }
        return itemTypes[itemTypes.Count - 1];
    }

    List<Transform> GetSpawnPoints()
    {
        var list = new List<Transform>();
        foreach (var p in spawnPoints) if (p != null) list.Add(p);
        if (list.Count == 0)
            foreach (Transform child in transform) if (!spawned.Contains(child.gameObject)) list.Add(child);
        return list;
    }

    Vector3 RandomAreaPoint()
    {
        Vector3 half = areaSize * 0.5f;
        Vector3 local = new Vector3(
            Random.Range(-half.x, half.x),
            Random.Range(-half.y, half.y),
            Random.Range(-half.z, half.z));
        Vector3 pos = transform.TransformPoint(local);

        if (snapToGround && Physics.Raycast(pos + Vector3.up * 50f, Vector3.down, out RaycastHit hit, 200f, groundMask, QueryTriggerInteraction.Ignore))
            pos = hit.point + Vector3.up * groundOffset;

        return pos;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        if (mode == SpawnMode.RandomInArea)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, areaSize);
        }
        else
        {
            Gizmos.matrix = Matrix4x4.identity;
            foreach (var p in GetSpawnPoints()) Gizmos.DrawWireSphere(p.position, 0.3f);
        }
    }
}
