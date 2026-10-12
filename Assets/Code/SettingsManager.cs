using UnityEngine;

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance;

    [Range(0f, 1f)]
    public float Volume = 1f;

    [Range(0.1f, 200f)]
    public float Sensitivity = 100f;

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

    public void ApplySettings()
    {
        AudioListener.volume = Volume;

        
    }
}
