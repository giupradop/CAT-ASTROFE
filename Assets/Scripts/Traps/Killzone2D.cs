using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider2D))]
public class KillZone2D : MonoBehaviour
{
    [Header("Death")]
    [SerializeField] float restartDelay = 0.4f; 
    [SerializeField] float fadeDuration = 0.4f;

    bool killed;

    void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
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

        SceneTransition.LoadScene(SceneManager.GetActiveScene().name, restartDelay, fadeDuration);
    }
}