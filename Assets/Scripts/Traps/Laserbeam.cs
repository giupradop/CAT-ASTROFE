using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;


[RequireComponent(typeof(Collider2D))]
public class LaserBeam : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField] float onDuration = 2f;
    [SerializeField] float offDuration = 1.5f;
    [SerializeField] float startDelay = 1.5f;
    [SerializeField] float randomStartDelay = 2f;

    [Header("Extras")]
    [SerializeField] GameObject[] toggledWithBeam;

    [Header("Audio")]
    public AudioSource turnOnSound;   
    public AudioSource turnOffSound;  
    public AudioSource loopSound;     

    [Header("Death")]
    public AudioSource deathSound;
    [SerializeField] float restartDelay = 0.4f;
    [SerializeField] float fadeDuration = 0.4f;

    Collider2D beamCollider;
    SpriteRenderer beamRenderer;
    bool killed;

    void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void Awake()
    {
        beamCollider = GetComponent<Collider2D>();
        beamRenderer = GetComponent<SpriteRenderer>();
        beamCollider.isTrigger = true;

        Prepare(turnOnSound, false);
        Prepare(turnOffSound, false);
        Prepare(loopSound, true);
    }

    void OnEnable()
    {
        SetBeam(false);
        StartCoroutine(Cycle());
    }

    void OnDisable()
    {
        if (loopSound != null)
            loopSound.Stop();
    }

    IEnumerator Cycle()
    {
        float wait = startDelay + Random.Range(0f, randomStartDelay);
        if (wait > 0f)
            yield return new WaitForSeconds(wait);

        while (true)
        {
            SetBeam(true);
            PlaySound(turnOnSound);
            StartLoop();
            yield return new WaitForSeconds(onDuration);

            SetBeam(false);
            StopLoop();
            PlaySound(turnOffSound);
            yield return new WaitForSeconds(offDuration);
        }
    }

    void SetBeam(bool on)
    {
        beamCollider.enabled = on;
        if (beamRenderer != null)
            beamRenderer.enabled = on;

        if (toggledWithBeam == null) return;
        foreach (var extra in toggledWithBeam)
            if (extra != null)
                extra.SetActive(on);
    }

    static void Prepare(AudioSource source, bool loop)
    {
        if (source == null) return;
        source.playOnAwake = false;
        source.loop = loop;
    }

    static void PlaySound(AudioSource source)
    {
        if (source != null)
            source.Play();
    }

    void StartLoop()
    {
        if (loopSound != null && !loopSound.isPlaying)
            loopSound.Play();
    }

    void StopLoop()
    {
        if (loopSound != null)
            loopSound.Stop();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryKill(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        TryKill(other);
    }

    void TryKill(Collider2D other)
    {
        if (killed) return;

        var player = other.GetComponentInParent<PlayerController2D>();
        if (player == null) return;

        killed = true;
        player.SetControlEnabled(false);
        if (deathSound != null)
            deathSound.Play();

        SceneTransition.LoadScene(SceneManager.GetActiveScene().name, restartDelay, fadeDuration);
    }
}