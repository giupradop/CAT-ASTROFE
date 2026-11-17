using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Collider2D))]
public class NarratorTrigger : MonoBehaviour
{
    [Header("Text")]
    [SerializeField, TextArea(3, 8)] string narration = "Write what the narrator says here.";

    [Header("Typewriter")]
    [Tooltip("Seconds between two letters. Smaller = faster.")]
    [SerializeField, Min(0.005f)] float letterDelay = 0.04f;
    [Tooltip("Extra seconds of pause after . , ! ? : ;")]
    [SerializeField, Min(0f)] float punctuationPause = 0.15f;

    [Header("Voice")]
    [Tooltip("AudioSource with the voice blip, played for every letter (spaces and punctuation stay silent). Set the clip, volume and base pitch on the AudioSource itself.")]
    public AudioSource voiceSource;
    [Tooltip("Each letter gets a random pitch from (the AudioSource pitch - this) to (the AudioSource pitch + this). 0 = always the same pitch.")]
    [SerializeField, Range(0f, 0.5f)] float pitchVariation = 0.15f;
    [Tooltip("1 = a blip on every letter, 2 = every second letter, and so on. Useful when the text is typed very fast.")]
    [SerializeField, Min(1)] int soundEveryNLetters = 1;

    [Header("Look")]
    [SerializeField] float fontSize = 36f;      
    [SerializeField] Color textColor = Color.white;
    [SerializeField] Color glowColor = new Color(1f, 1f, 1f, 0.6f);
    [SerializeField, Range(0f, 1f)] float glowSize = 0.35f;
    [SerializeField] Color backgroundColor = new Color(0.05f, 0.07f, 0.12f, 0.55f);

    [Header("Font")]
    [SerializeField] TMP_FontAsset font;        
    [SerializeField] Font pixelFont;            
    [SerializeField, Range(1f, 8f)] float pixelScale = 3f; 

    [Header("Timing")]
    [SerializeField] float hideDelay = 1f;     
    [SerializeField] float fadeDuration = 0.4f;
    [SerializeField] bool showOnlyOnce = false;

    readonly HashSet<Collider2D> inside = new HashSet<Collider2D>();
    bool shown;
    float basePitch = 1f;

    void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;

        if (voiceSource != null)
        {
            basePitch = voiceSource.pitch;
            voiceSource.playOnAwake = false;
            voiceSource.loop = false;
        }
    }

    void OnDisable()
    {
        inside.Clear();
        NarratorUI.Release(this, 0f);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerController2D>() == null) return;
        if (showOnlyOnce && shown) return;

        inside.Add(other);
        shown = true;

        var typing = new NarratorUI.TypeSettings
        {
            letterDelay = letterDelay,
            punctuationPause = punctuationPause,
            voiceSource = voiceSource,
            basePitch = basePitch,
            pitchVariation = pitchVariation,
            soundEveryNLetters = Mathf.Max(1, soundEveryNLetters)
        };

        NarratorUI.Show(this, narration, typing, fontSize, textColor, glowColor, glowSize, backgroundColor,
                        font, pixelFont, pixelScale, fadeDuration);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!inside.Remove(other)) return;
        if (inside.Count == 0)
            NarratorUI.Release(this, hideDelay);
    }
}
public class NarratorUI : MonoBehaviour
{
    public struct TypeSettings
    {
        public float letterDelay;
        public float punctuationPause;
        public AudioSource voiceSource;
        public float basePitch;
        public float pitchVariation;
        public int soundEveryNLetters;
    }

    const float PaddingX = 60f;
    const float PaddingY = 28f;
    const string HiddenTagOpen = "<color=#00000000>"; 
    const string HiddenTagClose = "</color>";

    static NarratorUI instance;

    CanvasGroup group;
    RectTransform panel;
    VerticalLayoutGroup layout;
    ContentSizeFitter fitter;
    Image background;
    TextMeshProUGUI label;   
    Text pixelLabel;        
    Texture2D texture;
    Sprite sprite;

    object owner;
    bool wanted;
    float hideAt;
    float releaseDelay;
    float fadeDuration = 0.4f;
    bool pixelMode;
    float pixelScale = 3f;
    float lastPanelWidth;

    string fullText;
    char[] letters;
    int revealed;
    int letterCount;
    bool typing;
    float typeTimer;
    float currentDelay;
    TypeSettings settings;

    public static void Show(object who, string text, TypeSettings typeSettings, float size, Color color, Color glow,
                            float glowSize, Color backgroundColor, TMP_FontAsset font, Font pixelFont,
                            float pixelScale, float fade)
    {
        Ensure();
        instance.Apply(who, text, typeSettings, size, color, glow, glowSize, backgroundColor, font, pixelFont,
                       pixelScale, fade);
    }

    public static void Release(object who, float delay)
    {
        if (instance == null || instance.owner != who) return;
        instance.wanted = false;
        instance.releaseDelay = delay;
        instance.hideAt = Time.unscaledTime + delay;
    }

    static void Ensure()
    {
        if (instance != null) return;
        var go = new GameObject("NarratorUI");
        instance = go.AddComponent<NarratorUI>();
        instance.Build();
    }

    void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        const int w = 128, h = 64;
        texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float ex = Mathf.Clamp01(Mathf.Min(x, w - 1 - x) / 24f);
                float ey = Mathf.Clamp01(Mathf.Min(y, h - 1 - y) / 20f);
                float a = Mathf.SmoothStep(0f, 1f, ex) * Mathf.SmoothStep(0f, 1f, ey);
                pixels[y * w + x] = new Color(1f, 1f, 1f, a);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();
        sprite = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);

        var panelObject = new GameObject("Panel", typeof(RectTransform), typeof(Image),
                                         typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panelObject.transform.SetParent(transform, false);
        panel = (RectTransform)panelObject.transform;
        panel.anchorMin = new Vector2(0.1f, 0f);
        panel.anchorMax = new Vector2(0.9f, 0f);
        panel.pivot = new Vector2(0.5f, 0f);
        panel.anchoredPosition = new Vector2(0f, 40f);

        background = panelObject.GetComponent<Image>();
        background.sprite = sprite;
        background.raycastTarget = false;

        layout = panelObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset((int)PaddingX, (int)PaddingX, (int)PaddingY, (int)PaddingY);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        fitter = panelObject.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var labelObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(panel, false);
        label = labelObject.GetComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.Normal; 
        label.raycastTarget = false;


        var pixelObject = new GameObject("PixelText", typeof(RectTransform), typeof(Text));
        pixelObject.transform.SetParent(panel, false);
        pixelLabel = pixelObject.GetComponent<Text>();
        pixelLabel.alignment = TextAnchor.UpperCenter;
        pixelLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
        pixelLabel.verticalOverflow = VerticalWrapMode.Overflow;
        pixelLabel.supportRichText = true;
        pixelLabel.raycastTarget = false;
        pixelObject.AddComponent<Outline>();
        pixelObject.AddComponent<Shadow>();
        pixelObject.SetActive(false);
    }

    void Apply(object who, string text, TypeSettings typeSettings, float size, Color color, Color glow,
               float glowSize, Color backgroundColor, TMP_FontAsset font, Font pixelFont, float pixelScale, float fade)
    {
        if (owner == who && fullText == text)
        {
            wanted = true;
            return;
        }

        owner = who;
        wanted = true;
        fadeDuration = Mathf.Max(0.01f, fade);
        background.color = backgroundColor;
        settings = typeSettings;
        fullText = text;

        pixelMode = pixelFont != null;
        label.gameObject.SetActive(!pixelMode);
        pixelLabel.gameObject.SetActive(pixelMode);
        layout.enabled = !pixelMode;
        fitter.enabled = !pixelMode;

        if (pixelMode)
        {
            this.pixelScale = pixelScale;
            pixelLabel.font = pixelFont;
            pixelLabel.color = color;

            var outline = pixelLabel.GetComponent<Outline>();
            outline.effectColor = new Color(glow.r, glow.g, glow.b, glow.a * 0.5f);
            outline.effectDistance = Vector2.one * Mathf.Max(1f, glowSize * 4f);
            var shadow = pixelLabel.GetComponent<Shadow>();
            shadow.effectColor = new Color(glow.r, glow.g, glow.b, glow.a * 0.25f);
            shadow.effectDistance = Vector2.one * Mathf.Max(2f, glowSize * 9f);

            letters = text.ToCharArray();
            ShowRevealed(0);

            lastPanelWidth = -1f;
            LayoutPixelText();
        }
        else
        {
            label.text = text;
            label.fontSize = size;
            label.color = color;
            if (font != null) label.font = font;
            label.maxVisibleCharacters = 0;

            Material material = label.fontMaterial;
            if (material.HasProperty(ShaderUtilities.ID_UnderlayColor))
            {
                material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                material.SetColor(ShaderUtilities.ID_UnderlayColor, glow);
                material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
                material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, 0f);
                material.SetFloat(ShaderUtilities.ID_UnderlayDilate, glowSize);
                material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, Mathf.Clamp01(glowSize + 0.25f));
                label.UpdateMeshPadding();
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);

            label.ForceMeshUpdate();
            int count = label.textInfo.characterCount;
            letters = new char[count];
            for (int i = 0; i < count; i++)
                letters[i] = label.textInfo.characterInfo[i].character;
        }

        revealed = 0;
        letterCount = 0;
        typeTimer = 0f;
        currentDelay = 0f;
        typing = letters.Length > 0;
    }

    void ShowRevealed(int count)
    {
        if (pixelMode)
        {
            pixelLabel.text = count >= fullText.Length
                ? fullText
                : fullText.Substring(0, count) + HiddenTagOpen + fullText.Substring(count) + HiddenTagClose;
        }
        else
        {
            label.maxVisibleCharacters = count;
        }
    }

    void RevealNext()
    {
        revealed++;
        char c = letters[revealed - 1];
        ShowRevealed(revealed);

        if (char.IsLetterOrDigit(c))
        {
            letterCount++;
            if (letterCount % settings.soundEveryNLetters == 0)
                PlayVoice();
        }

        bool pause = c == '.' || c == ',' || c == '!' || c == '?' || c == ':' || c == ';';
        currentDelay = Mathf.Max(0.005f, settings.letterDelay) + (pause ? settings.punctuationPause : 0f);

        if (revealed >= letters.Length)
            typing = false;
    }

    void PlayVoice()
    {
        AudioSource source = settings.voiceSource;
        if (source == null) return;

        float pitch = settings.basePitch + Random.Range(-settings.pitchVariation, settings.pitchVariation);
        source.pitch = Mathf.Max(0.1f, pitch);
        source.Play();
    }

    void LayoutPixelText()
    {
        float panelWidth = panel.rect.width;
        if (panelWidth <= 0f)
        {
            Canvas.ForceUpdateCanvases();
            panelWidth = panel.rect.width;
        }
        lastPanelWidth = panelWidth;

        var rect = (RectTransform)pixelLabel.transform;
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.localScale = Vector3.one * pixelScale;
        rect.sizeDelta = new Vector2((panelWidth - 2f * PaddingX) / pixelScale, 10f);
        rect.anchoredPosition = new Vector2(0f, -PaddingY);

        float textHeight = pixelLabel.preferredHeight;
        rect.sizeDelta = new Vector2(rect.sizeDelta.x, textHeight);
        panel.sizeDelta = new Vector2(0f, textHeight * pixelScale + 2f * PaddingY);
    }

    void Update()
    {
        if (pixelMode && !Mathf.Approximately(panel.rect.width, lastPanelWidth))
            LayoutPixelText();

        if (typing)
        {
            typeTimer += Time.unscaledDeltaTime;
            while (typing && typeTimer >= currentDelay)
            {
                typeTimer -= currentDelay;
                RevealNext();
            }

            if (!wanted)
                hideAt = Time.unscaledTime + releaseDelay;
        }

        bool visible = wanted || typing || Time.unscaledTime < hideAt;
        float target = visible ? 1f : 0f;
        group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime / fadeDuration);

        if (!visible && group.alpha <= 0f)
        {
            owner = null;
            fullText = null;
        }
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        if (sprite != null) Destroy(sprite);
        if (texture != null) Destroy(texture);
    }
}