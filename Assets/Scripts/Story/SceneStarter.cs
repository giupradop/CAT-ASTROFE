using UnityEngine;

public class SceneStarter : MonoBehaviour
{
    public AudioSource startSound;
    void Start()
    {
        startSound.Play();
    }
}
