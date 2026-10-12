using UnityEngine;

/// <summary>
/// Slides open when enough inputs (light receivers) are active.
/// Hook LightReceiver.onActivated -> AddInput and onDeactivated -> RemoveInput.
/// Set requiredInputs to 2+ for puzzles with several receivers.
/// </summary>
public class PuzzleDoor : MonoBehaviour
{
    [SerializeField] private int requiredInputs = 1;
    [Tooltip("How far the door moves (in its local space) when open. Default: slides up 3 units.")]
    [SerializeField] private Vector3 openOffset = new Vector3(0f, 3f, 0f);
    [SerializeField] private float speed = 2f;

    [Header("Optional sounds")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip openClip;
    [SerializeField] private AudioClip closeClip;

    private int activeInputs;
    private Vector3 closedPos;
    private Vector3 openPos;
    private bool wasOpen;

    private void Awake()
    {
        closedPos = transform.localPosition;
        openPos = closedPos + openOffset;
    }

    public void AddInput()
    {
        activeInputs++;
        CheckSound();
    }

    public void RemoveInput()
    {
        activeInputs = Mathf.Max(0, activeInputs - 1);
        CheckSound();
    }

    private bool ShouldBeOpen => activeInputs >= requiredInputs;

    private void CheckSound()
    {
        if (ShouldBeOpen == wasOpen) return;
        wasOpen = ShouldBeOpen;

        if (audioSource != null)
        {
            AudioClip clip = wasOpen ? openClip : closeClip;
            if (clip != null) audioSource.PlayOneShot(clip);
        }
    }

    private void Update()
    {
        Vector3 target = ShouldBeOpen ? openPos : closedPos;
        transform.localPosition = Vector3.MoveTowards(transform.localPosition, target, speed * Time.deltaTime);
    }
}
