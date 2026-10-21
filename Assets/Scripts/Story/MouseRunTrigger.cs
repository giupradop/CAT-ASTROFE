using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class MouseRunTrigger : MonoBehaviour
{
    [Header("Mouse")]
    [SerializeField] MouseRunner mouse;
    [SerializeField] Transform[] path;
    [SerializeField] bool jumpFirstSegment;
    [SerializeField] bool hideMouseAtEnd;

    [Header("Cat Reaction")]
    [SerializeField] GameObject exclamation;
    [SerializeField] float exclamationDuration = 1.5f;

    [Header("Leaves")]
    [SerializeField] ParticleSystem leaves;
    [SerializeField] int leafCount = 8;

    bool triggered;

    void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void Awake()
    {
        if (exclamation != null)
            exclamation.SetActive(false);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered || other.GetComponent<PlayerController2D>() == null) return;
        triggered = true;

        if (leaves != null)
            leaves.Emit(leafCount);
        if (exclamation != null)
            StartCoroutine(ShowExclamation());
        if (mouse != null)
            mouse.Run(path, jumpFirstSegment, hideMouseAtEnd);
    }

    IEnumerator ShowExclamation()
    {
        exclamation.SetActive(true);
        yield return new WaitForSeconds(exclamationDuration);
        exclamation.SetActive(false);
    }
}
