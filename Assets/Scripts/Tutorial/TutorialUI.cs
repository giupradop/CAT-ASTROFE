using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class TutorialUI : MonoBehaviour
{
    public static TutorialUI Instance { get; private set; }

    [SerializeField] TMP_Text messageText;
    [SerializeField] Image hintImage;
    [SerializeField] float fadeSpeed = 4f;

    CanvasGroup group;
    float targetAlpha;
    object currentOwner;

    void Awake()
    {
        Instance = this;
        group = GetComponent<CanvasGroup>();
        group.alpha = 0f;
        if (hintImage != null)
            hintImage.preserveAspect = true;
    }

    void Update()
    {
        group.alpha = Mathf.MoveTowards(group.alpha, targetAlpha, fadeSpeed * Time.deltaTime);
    }

    public void Show(object owner, string message, Sprite image)
    {
        currentOwner = owner;
        bool useImage = image != null && hintImage != null;

        if (hintImage != null)
        {
            hintImage.enabled = useImage;
            hintImage.sprite = image;
        }
        if (messageText != null)
        {
            messageText.enabled = !useImage;
            messageText.text = message;
        }

        targetAlpha = 1f;
    }

    // Only the zone that is currently showing can hide it, so leaving one zone right after
    // entering the next doesn't erase the new hint.
    public void Hide(object owner)
    {
        if (currentOwner == owner)
            targetAlpha = 0f;
    }
}
