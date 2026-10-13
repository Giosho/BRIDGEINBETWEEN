using UnityEngine;
using UnityEngine.UI;

public class OptionsMenu : MonoBehaviour
{
    [Header("UI Elements")]
    public Slider volumeSlider;        // assign in Inspector OR auto-find
    public Slider sensitivitySlider;   // assign in Inspector OR auto-find
    [Tooltip("Optional. How far you can see. Name the slider 'ViewDistanceSlider' or assign it here.")]
    public Slider viewDistanceSlider;
    public float minViewDistance = 20f;
    public float maxViewDistance = 500f;

    private void Awake()
    {
        // Try to automatically find sliders if not assigned in Inspector
        if (volumeSlider == null)
        {
            volumeSlider = transform.Find("VolumeSlider")?.GetComponent<Slider>();
            if (volumeSlider == null)
                Debug.LogWarning("VolumeSlider not found! Assign it in the Inspector or name the slider 'VolumeSlider'.");
        }

        if (sensitivitySlider == null)
        {
            sensitivitySlider = transform.Find("SensitivitySlider")?.GetComponent<Slider>();
            if (sensitivitySlider == null)
                Debug.LogWarning("SensitivitySlider not found! Assign it in the Inspector or name the slider 'SensitivitySlider'.");
        }
    }

    private void Start()
    {
        SetupViewDistanceSlider();

        if (SettingsManager.Instance == null)
        {
            Debug.LogWarning("SettingsManager not found in scene!");
            return;
        }

        // Initialize sliders with current settings
        if (volumeSlider != null)
        {
            volumeSlider.value = SettingsManager.Instance.Volume;
            volumeSlider.onValueChanged.RemoveAllListeners();
            volumeSlider.onValueChanged.AddListener(SettingsManager.Instance.SetVolume);
        }

        if (sensitivitySlider != null)
        {
            sensitivitySlider.value = SettingsManager.Instance.Sensitivity;
            sensitivitySlider.onValueChanged.RemoveAllListeners();
            sensitivitySlider.onValueChanged.AddListener(SettingsManager.Instance.SetSensitivity);
        }
    }

    private void SetupViewDistanceSlider()
    {
        if (viewDistanceSlider == null)
            viewDistanceSlider = transform.Find("ViewDistanceSlider")?.GetComponent<Slider>();
        if (viewDistanceSlider == null || SettingsManager.Instance == null) return;

        viewDistanceSlider.minValue = minViewDistance;
        viewDistanceSlider.maxValue = maxViewDistance;

        float current = SettingsManager.Instance.ViewDistance;
        if (current <= 0f)
        {
            var culler = FindFirstObjectByType<VisibilityCuller>();
            current = culler != null ? culler.viewDistance : 150f;
        }

        viewDistanceSlider.SetValueWithoutNotify(Mathf.Clamp(current, minViewDistance, maxViewDistance));
        viewDistanceSlider.onValueChanged.RemoveAllListeners();
        viewDistanceSlider.onValueChanged.AddListener(SettingsManager.Instance.SetViewDistance);
    }
}
