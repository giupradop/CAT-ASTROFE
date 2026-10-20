using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class TutorialHint : MonoBehaviour
{
    [SerializeField] Sprite image;
    [SerializeField, TextArea] string message;

    void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<PlayerController2D>() != null && TutorialUI.Instance != null)
            TutorialUI.Instance.Show(this, message, image);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponent<PlayerController2D>() != null && TutorialUI.Instance != null)
            TutorialUI.Instance.Hide(this);
    }
}
