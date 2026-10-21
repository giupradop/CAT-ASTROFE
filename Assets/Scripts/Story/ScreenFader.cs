using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class ScreenFader : MonoBehaviour
{
    CanvasGroup group;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
    }

    public IEnumerator FadeTo(float alpha, float duration)
    {
        float start = group.alpha;
        for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
        {
            group.alpha = Mathf.Lerp(start, alpha, t);
            yield return null;
        }
        group.alpha = alpha;
    }
}
