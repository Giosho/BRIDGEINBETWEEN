using UnityEngine;

/// <summary>
/// Finds the 3D player without requiring the "Player" tag:
/// 1) object with the tag, 2) the 3D PlayerMovement, 3) the object holding the main camera's look script.
/// </summary>
public static class PlayerFinder
{
    public static Transform FindPlayer(string tag = "Player")
    {
        if (!string.IsNullOrEmpty(tag))
        {
            try
            {
                var tagged = GameObject.FindGameObjectWithTag(tag);
                if (tagged != null) return tagged.transform;
            }
            catch (UnityException) { /* tag not defined in this project */ }
        }

        foreach (var pm in Object.FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
            if (!pm.is2DPlayer) return pm.transform;

        var look = Object.FindFirstObjectByType<MouseLook3D>();
        if (look != null && look.playerBody != null) return look.playerBody;

        return null;
    }

    /// <summary>The camera the player looks through: one under the player, else Camera.main.</summary>
    public static Camera FindCamera(Transform player)
    {
        if (player != null)
        {
            var cam = player.GetComponentInChildren<Camera>(false);
            if (cam != null) return cam;
        }
        return Camera.main;
    }

    /// <summary>True if this collider belongs to the player (tag or PlayerMovement in parents).</summary>
    public static bool IsPlayer(Collider other, string tag = "Player")
    {
        if (!string.IsNullOrEmpty(tag))
        {
            try { if (other.CompareTag(tag)) return true; }
            catch (UnityException) { }
        }
        var pm = other.GetComponentInParent<PlayerMovement>();
        return pm != null && !pm.is2DPlayer;
    }
}
