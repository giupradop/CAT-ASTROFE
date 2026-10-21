using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider2D))]
public class VanCaptureTrigger : MonoBehaviour
{
    [Header("Van")]
    [SerializeField] Transform van;
    [SerializeField] Transform vanStopPoint;
    [SerializeField] float driveInDuration = 1.5f;

    [Header("Capture")]
    [SerializeField] float catRunOnDuration = 0.5f;
    [SerializeField] float delayBeforeFade = 0.8f;
    [SerializeField] ScreenFader fader;
    [SerializeField] float fadeDuration = 1f;
    [SerializeField] string nextSceneName;
    [SerializeField] float delayBeforeNextScene = 2f;

    bool triggered;

    void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered) return;
        var player = other.GetComponent<PlayerController2D>();
        if (player == null) return;

        triggered = true;
        StartCoroutine(Capture(player));
    }

    IEnumerator Capture(PlayerController2D player)
    {
        player.SetControlEnabled(false);
        StartCoroutine(RunOn(player));

        if (van != null && vanStopPoint != null)
            yield return DriveVanIn();

        yield return new WaitForSeconds(delayBeforeFade);

        if (fader != null)
            yield return fader.FadeTo(1f, fadeDuration);

        if (!string.IsNullOrEmpty(nextSceneName))
        {
            yield return new WaitForSeconds(delayBeforeNextScene);
            SceneManager.LoadScene(nextSceneName);
        }
    }

    // Keeps the cat running a few steps in the direction it was going, so it doesn't stop dead.
    IEnumerator RunOn(PlayerController2D player)
    {
        float velocityX = player.GetComponent<Rigidbody2D>().linearVelocity.x;
        if (Mathf.Abs(velocityX) < 0.01f) yield break;

        player.SetForcedMove(Mathf.Sign(velocityX));
        yield return new WaitForSeconds(catRunOnDuration);
        player.SetForcedMove(0f);
    }

    IEnumerator DriveVanIn()
    {
        Vector3 start = van.position;
        Vector3 end = new Vector3(vanStopPoint.position.x, start.y, start.z); // stays in its lane, only X changes
        for (float t = 0f; t < 1f; t += Time.deltaTime / driveInDuration)
        {
            float eased = 1f - Mathf.Pow(1f - t, 3f); // fast at first, brakes into the stop
            van.position = Vector3.Lerp(start, end, eased);
            yield return null;
        }
        van.position = end;
    }
}
