using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Put this on an empty object with a Collider2D (box). While Bigode is inside, the narrator text shows at the
// top of the screen. When he leaves, it stays 1 second (hideDelay) and then fades out.
// The text box is built in code and shared by every trigger, so there is nothing else to add to the scene.
//
// Two font modes:
//  - Pixel Font empty: TextMeshPro text (real glow).
//  - Pixel Font set (a legacy bitmap font such as Tripfive.fontsettings): old UI Text, with an outline halo as glow.
[RequireComponent(typeof(Collider2D))]
public class NarratorTrigger : MonoBehaviour
{
    [Header("Text")]
    [SerializeField, TextArea(3, 8)] string narration = "Write what the narrator says here.";

    [Header("Look")]
    [SerializeField] float fontSize = 36f;      // TextMeshPro mode only
    [SerializeField] Color textColor = Color.white;
    [SerializeField] Color glowColor = new Color(1f, 1f, 1f, 0.6f);
    [SerializeField, Range(0f, 1f)] float glowSize = 0.35f;
    [SerializeField] Color backgroundColor = new Color(0.05f, 0.07f, 0.12f, 0.55f);

    [Header("Font")]
    [SerializeField] TMP_FontAsset font;        // optional TextMeshPro font
    [SerializeField] Font pixelFont;            // optional legacy bitmap font (e.g. Tripfive); wins over the one above
    [SerializeField, Range(1f, 8f)] float pixelScale = 3f; // pixel font only: each font pixel becomes this many screen pixels

    [Header("Timing")]
    [SerializeField] float hideDelay = 1f;      // seconds the text stays after Bigode leaves
    [SerializeField] float fadeDuration = 0.4f;
    [SerializeField] bool showOnlyOnce = false; // true: after it has appeared once, this trigger never shows again

    readonly HashSet<Collider2D> inside = new HashSet<Collider2D>();
    bool shown;

    void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
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
        NarratorUI.Show(this, narration, fontSize, textColor, glowColor, glowSize, backgroundColor,
                        font, pixelFont, pixelScale, fadeDuration);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!inside.Remove(other)) return;
        if (inside.Count == 0)
            NarratorUI.Release(this, hideDelay);
    }
}

// The on-screen narrator box. Created by the first trigger that needs it; one per scene.
public class NarratorUI : MonoBehaviour
{
    const float PaddingX = 60f;
    const float PaddingY = 28f;

    static NarratorUI instance;

    CanvasGroup group;
    RectTransform panel;
    VerticalLayoutGroup layout;
    ContentSizeFitter fitter;
    Image background;
    TextMeshProUGUI label;   // TextMeshPro mode
    Text pixelLabel;         // legacy bitmap font mode
    Texture2D texture;
    Sprite sprite;

    object owner;
    bool wanted;
    float hideAt;            // unscaled time at which the text starts fading out
    float fadeDuration = 0.4f;
    bool pixelMode;
    float pixelScale = 3f;
    float lastPanelWidth;

    public static void Show(object who, string text, float size, Color color, Color glow, float glowSize,
                            Color backgroundColor, TMP_FontAsset font, Font pixelFont, float pixelScale, float fade)
    {
        Ensure();
        instance.Apply(who, text, size, color, glow, glowSize, backgroundColor, font, pixelFont, pixelScale, fade);
    }

    public static void Release(object who, float delay)
    {
        if (instance == null || instance.owner != who) return;
        instance.wanted = false;
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
        canvas.sortingOrder = 400; // under the stealth overlay (500) and SceneTransition (999)

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        // Soft-edged background texture made in code (clear at the borders, solid in the middle).
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

        // Panel: 80% of the screen width, anchored to the bottom centre (like a subtitle).
        var panelObject = new GameObject("Panel", typeof(RectTransform), typeof(Image),
                                         typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panelObject.transform.SetParent(transform, false);
        panel = (RectTransform)panelObject.transform;
        panel.anchorMin = new Vector2(0.1f, 0f);
        panel.anchorMax = new Vector2(0.9f, 0f);
        panel.pivot = new Vector2(0.5f, 0f);
        panel.anchoredPosition = new Vector2(0f, 40f); // distance from the bottom edge

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

        // TextMeshPro label (used when no pixel font is given)
        var labelObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(panel, false);
        label = labelObject.GetComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.Normal; // breaks the line only at the panel edge (80% of the screen)
        label.raycastTarget = false;

        // Legacy label (used with a bitmap pixel font). Laid out by hand, see LayoutPixelText.
        var pixelObject = new GameObject("PixelText", typeof(RectTransform), typeof(Text));
        pixelObject.transform.SetParent(panel, false);
        pixelLabel = pixelObject.GetComponent<Text>();
        pixelLabel.alignment = TextAnchor.UpperCenter;
        pixelLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
        pixelLabel.verticalOverflow = VerticalWrapMode.Overflow;
        pixelLabel.supportRichText = false;
        pixelLabel.raycastTarget = false;
        pixelObject.AddComponent<Outline>();       // inner halo
        pixelObject.AddComponent<Shadow>();        // outer, softer halo
        pixelObject.SetActive(false);
    }

    void Apply(object who, string text, float size, Color color, Color glow, float glowSize,
               Color backgroundColor, TMP_FontAsset font, Font pixelFont, float pixelScale, float fade)
    {
        owner = who;
        wanted = true;
        fadeDuration = Mathf.Max(0.01f, fade);
        background.color = backgroundColor;

        pixelMode = pixelFont != null;
        label.gameObject.SetActive(!pixelMode);
        pixelLabel.gameObject.SetActive(pixelMode);
        layout.enabled = !pixelMode;
        fitter.enabled = !pixelMode;

        if (pixelMode)
        {
            this.pixelScale = pixelScale;
            pixelLabel.font = pixelFont;
            pixelLabel.text = text;
            pixelLabel.color = color;

            // Fake glow: two translucent halos around the letters, in screen pixels of the scaled text.
            var outline = pixelLabel.GetComponent<Outline>();
            outline.effectColor = new Color(glow.r, glow.g, glow.b, glow.a * 0.5f);
            outline.effectDistance = Vector2.one * Mathf.Max(1f, glowSize * 4f);
            var shadow = pixelLabel.GetComponent<Shadow>();
            shadow.effectColor = new Color(glow.r, glow.g, glow.b, glow.a * 0.25f);
            shadow.effectDistance = Vector2.one * Mathf.Max(2f, glowSize * 9f);

            lastPanelWidth = -1f;
            LayoutPixelText();
            return;
        }

        label.text = text;
        label.fontSize = size;
        label.color = color;
        if (font != null) label.font = font;

        // Glow: the TextMeshPro "underlay" with no offset, dilated and softened, shows up as a halo.
        Material material = label.fontMaterial; // an instance, so other texts are not touched
        if (material.HasProperty(ShaderUtilities.ID_UnderlayColor))
        {
            material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            material.SetColor(ShaderUtilities.ID_UnderlayColor, glow);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, 0f);
            material.SetFloat(ShaderUtilities.ID_UnderlayDilate, glowSize);
            material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, Mathf.Clamp01(glowSize + 0.25f));
            label.UpdateMeshPadding(); // keeps the halo from being clipped at the glyph edges
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
    }

    // A bitmap font has a fixed size, so the text is enlarged by scaling its RectTransform.
    // The width is divided by the scale, so the line still breaks at the panel edge (80% of the screen).
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

        float textHeight = pixelLabel.preferredHeight; // uses the width set above
        rect.sizeDelta = new Vector2(rect.sizeDelta.x, textHeight);
        panel.sizeDelta = new Vector2(0f, textHeight * pixelScale + 2f * PaddingY);
    }

    void Update()
    {
        // Keeps the pixel text correct if the window size changes while it is on screen.
        if (pixelMode && !Mathf.Approximately(panel.rect.width, lastPanelWidth))
            LayoutPixelText();

        bool visible = wanted || Time.unscaledTime < hideAt;
        float target = visible ? 1f : 0f;
        group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime / fadeDuration);

        if (!visible && group.alpha <= 0f)
            owner = null;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        if (sprite != null) Destroy(sprite);
        if (texture != null) Destroy(texture);
    }
}