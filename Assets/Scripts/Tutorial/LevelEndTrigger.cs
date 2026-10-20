using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider2D))]
public class LevelEndTrigger : MonoBehaviour
{
    [Header("Prompt")]
    [SerializeField] Sprite promptImage;
    [SerializeField, TextArea] string promptMessage = "Pressione E para interagir";

    [Header("End")]
    [SerializeField] Sprite endImage;
    [SerializeField, TextArea] string endMessage = "Bigode escapou!";
    [SerializeField] string nextSceneName;
    [SerializeField] float delayBeforeNextScene = 3f;

    PlayerController2D playerInside;
    bool finished;

    void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void Update()
    {
        if (finished || playerInside == null) return;

        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
            Finish();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (finished) return;
        var player = other.GetComponent<PlayerController2D>();
        if (player == null) return;

        playerInside = player;
        if (TutorialUI.Instance != null)
            TutorialUI.Instance.Show(this, promptMessage, promptImage);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (finished || other.GetComponent<PlayerController2D>() == null) return;

        playerInside = null;
        if (TutorialUI.Instance != null)
            TutorialUI.Instance.Hide(this);
    }

    void Finish()
    {
        finished = true;
        playerInside.SetControlEnabled(false);

        if (TutorialUI.Instance != null)
            TutorialUI.Instance.Show(this, endMessage, endImage);

        if (!string.IsNullOrEmpty(nextSceneName))
            StartCoroutine(LoadNextScene());
    }

    IEnumerator LoadNextScene()
    {
        yield return new WaitForSeconds(delayBeforeNextScene);
        SceneManager.LoadScene(nextSceneName);
    }
}
