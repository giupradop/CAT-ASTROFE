using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneTransition : MonoBehaviour
{
    static SceneTransition instance;

    CanvasGroup group;
    bool busy;
    float masterVolume = 1f;

    static SceneTransition Instance
    {
        get
        {
            if (instance == null)
                new GameObject("SceneTransition").AddComponent<SceneTransition>();
            return instance;
        }
    }


    public static void LoadScene(string sceneName, float delay = 0f, float fadeDuration = 0.6f, bool stopAudio = true)
    {
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"SceneTransition: scene '{sceneName}' is not in the Build Settings.");
            return;
        }

        var transition = Instance;
        if (transition.busy) return;
        transition.StartCoroutine(transition.Run(sceneName, delay, fadeDuration, stopAudio));
    }

    public static void Quit(float delay = 0f, float fadeDuration = 0.6f, bool fadeAudio = true)
    {
        var transition = Instance;
        if (transition.busy) return;
        transition.StartCoroutine(transition.RunQuit(delay, fadeDuration, fadeAudio));
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        BuildOverlay();
    }

    void OnDestroy()
    {
        if (busy)
            AudioListener.volume = masterVolume;
    }

    void BuildOverlay()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        gameObject.AddComponent<GraphicRaycaster>();

        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;

        var blackObject = new GameObject("Black", typeof(RectTransform), typeof(Image));
        blackObject.transform.SetParent(transform, false);

        var rect = (RectTransform)blackObject.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        blackObject.GetComponent<Image>().color = Color.black;
    }

    IEnumerator Run(string sceneName, float delay, float fadeDuration, bool stopAudio)
    {
        busy = true;

        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        masterVolume = AudioListener.volume;

        group.blocksRaycasts = true;
        yield return Fade(1f, fadeDuration, stopAudio);

        if (stopAudio)
        {
            foreach (var source in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                source.Stop();
        }

        var load = SceneManager.LoadSceneAsync(sceneName);
        while (!load.isDone)
            yield return null;

        yield return Fade(0f, fadeDuration, stopAudio);

        group.blocksRaycasts = false;
        busy = false;
    }

    IEnumerator RunQuit(float delay, float fadeDuration, bool fadeAudio)
    {
        busy = true;

        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        masterVolume = AudioListener.volume;

        group.blocksRaycasts = true;
        yield return Fade(1f, fadeDuration, fadeAudio);

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    IEnumerator Fade(float targetAlpha, float duration, bool fadeAudio)
    {
        duration = Mathf.Max(duration, 0.01f);
        float start = group.alpha;
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / duration)
        {
            group.alpha = Mathf.Lerp(start, targetAlpha, t);
            if (fadeAudio)
                AudioListener.volume = masterVolume * (1f - group.alpha);
            yield return null;
        }

        group.alpha = targetAlpha;
        if (fadeAudio)
            AudioListener.volume = masterVolume * (1f - targetAlpha);
    }
}
