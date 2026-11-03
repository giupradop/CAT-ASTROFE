using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuController : MonoBehaviour
{

    [Header("Audio")]
    public AudioSource backgroundMusic;
    public AudioSource clickSound;

    private void Start()
    {

        if (backgroundMusic != null && !backgroundMusic.isPlaying)
        {
            backgroundMusic.loop = true;
            backgroundMusic.Play();
        }
    }

    public void PlayGame()
    {
        PlayClickSound();
        SceneTransition.LoadScene("TutQuarto", 1f);
    }

    public void QuitGame()
    {
        PlayClickSound();
        SceneTransition.Quit(1f);
    }

    private void PlayClickSound()
    {
        if (clickSound != null)
            clickSound.Play();
    }
}