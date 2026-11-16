using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuController : MonoBehaviour
{

    [Header("Audio")]
    public AudioSource backgroundMusic;
    public AudioSource inSound;
    public AudioSource outSound;

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
        PlayInSound();
        SceneTransition.LoadScene("TutQuarto", 1f);
    }

    public void QuitGame()
    {
        PlayOutSound();
        SceneTransition.Quit(1f);
    }

    private void PlayInSound()
    {
        if (inSound != null)
            inSound.Play();
    }

    private void PlayOutSound()
    {
        if (outSound != null)
            outSound.Play();
    }
}