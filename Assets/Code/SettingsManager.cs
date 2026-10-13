using System;
using UnityEngine;

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance;

    [Range(0f, 1f)]
    public float Volume = 1f;

    [Range(0.1f, 200f)]
    public float Sensitivity = 100f;

    /// <summary>How far you can see in metres. Zero or less = not chosen yet, VisibilityCuller uses its own value.</summary>
    public float ViewDistance = -1f;

    /// <summary>Raised when the view distance changes (VisibilityCuller listens to this).</summary>
    public static event Action<float> OnViewDistanceChanged;

    private void Awake()
    {
        // --- Singleton setup ---
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);   // makes the object survive all scenes

        // Load saved values
        Volume = PlayerPrefs.GetFloat("Volume", 1f);
        Sensitivity = PlayerPrefs.GetFloat("Sensitivity", 100f);
        ViewDistance = PlayerPrefs.GetFloat("ViewDistance", -1f);

        ApplySettings();
    }

    public void SetVolume(float value)
    {
        Volume = value;
        AudioListener.volume = value;
        PlayerPrefs.SetFloat("Volume", value);
    }

    public void SetSensitivity(float value)
    {
        Sensitivity = value;
        PlayerPrefs.SetFloat("Sensitivity", value);

        
    }

    public void SetViewDistance(float value)
    {
        ViewDistance = value;
        PlayerPrefs.SetFloat("ViewDistance", value);
        OnViewDistanceChanged?.Invoke(value);
    }

    public void ApplySettings()
    {
        AudioListener.volume = Volume;

        
    }
}
