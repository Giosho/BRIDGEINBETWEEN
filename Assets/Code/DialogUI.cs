using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Caption bar at the bottom of the screen. Created automatically the first time it is needed,
/// no scene setup required. Left mouse click shows the next line; after the last line it closes.
/// While open, IsOpen is true so movement / mouse look can freeze.
/// </summary>
public class DialogUI : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    private static DialogUI instance;
    public static DialogUI Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("DialogUI");
                instance = go.AddComponent<DialogUI>();
                instance.Build();
            }
            return instance;
        }
    }

    private GameObject panel;
    private Text captionText;
    private Text promptText;
    private Text noticeText;
    private string speaker;
    private string[] lines;
    private int index;
    private System.Action onClosed;
    private int openedFrame;
    private float noticeUntil;

    void Build()
    {
        var canvasGo = new GameObject("Canvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Caption bar under the screen
        panel = new GameObject("CaptionPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasGo.transform, false);
        var rt = (RectTransform)panel.transform;
        rt.anchorMin = new Vector2(0.1f, 0f);
        rt.anchorMax = new Vector2(0.9f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 40f);
        rt.sizeDelta = new Vector2(0f, 190f);
        panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);
        captionText = MakeText("Caption", panel.transform, font, 36, TextAnchor.MiddleCenter);
        Stretch((RectTransform)captionText.transform, 30f);
        panel.SetActive(false);

        // "Press E to talk" prompt
        promptText = MakeText("Prompt", canvasGo.transform, font, 32, TextAnchor.MiddleCenter);
        var prt = (RectTransform)promptText.transform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.35f);
        prt.sizeDelta = new Vector2(800f, 60f);
        promptText.gameObject.SetActive(false);

        // Short notice, e.g. "Received: Key Piece"
        noticeText = MakeText("Notice", canvasGo.transform, font, 34, TextAnchor.MiddleCenter);
        noticeText.color = new Color(1f, 0.9f, 0.4f);
        var nrt = (RectTransform)noticeText.transform;
        nrt.anchorMin = nrt.anchorMax = new Vector2(0.5f, 0.8f);
        nrt.sizeDelta = new Vector2(1000f, 60f);
        noticeText.gameObject.SetActive(false);
    }

    static Text MakeText(string name, Transform parent, Font font, int size, TextAnchor anchor)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = font; t.fontSize = size; t.alignment = anchor; t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        var shadow = go.AddComponent<Shadow>();
        shadow.effectDistance = new Vector2(2f, -2f);
        return t;
    }

    static void Stretch(RectTransform rt, float pad)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(pad, pad); rt.offsetMax = new Vector2(-pad, -pad);
    }

    /// <summary>Show lines one after another. onClosed runs after the last one.</summary>
    public void Show(string speakerName, string[] dialogLines, System.Action closed = null)
    {
        if (dialogLines == null || dialogLines.Length == 0) { closed?.Invoke(); return; }
        speaker = speakerName;
        lines = dialogLines;
        index = 0;
        onClosed = closed;
        IsOpen = true;
        openedFrame = Time.frameCount;
        panel.SetActive(true);
        SetPrompt(null);
        Display();
    }

    void Display()
    {
        string line = lines[index];
        captionText.text = string.IsNullOrEmpty(speaker) ? line : $"<b>{speaker}</b>\n{line}";
        captionText.supportRichText = true;
    }

    void Update()
    {
        if (IsOpen && Time.frameCount != openedFrame && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            index++;
            if (index < lines.Length) Display();
            else Close();
        }

        if (noticeText.gameObject.activeSelf && Time.time > noticeUntil)
            noticeText.gameObject.SetActive(false);
    }

    void Close()
    {
        IsOpen = false;
        panel.SetActive(false);
        var cb = onClosed;
        onClosed = null;
        cb?.Invoke();
    }

    void OnDestroy()
    {
        if (instance == this) { instance = null; IsOpen = false; }
    }

    /// <summary>Show or hide the small hint text (null / empty hides it).</summary>
    public void SetPrompt(string text)
    {
        bool show = !string.IsNullOrEmpty(text) && !IsOpen;
        promptText.gameObject.SetActive(show);
        if (show) promptText.text = text;
    }

    public void ShowNotice(string text, float seconds = 3f)
    {
        noticeText.text = text;
        noticeText.gameObject.SetActive(true);
        noticeUntil = Time.time + seconds;
    }
}
