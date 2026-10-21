using System.Collections;
using UnityEngine;

public class MouseRunner : MonoBehaviour
{
    [SerializeField] float runSpeed = 6f;
    [SerializeField] float jumpHeight = 1.5f;
    [SerializeField] float jumpDuration = 0.6f;
    [SerializeField] bool spriteFacesRight = true;

    static readonly int RunningParam = Animator.StringToHash("Correndo");

    SpriteRenderer spriteRenderer;
    Animator animator;
    Coroutine running;

    void Awake()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        animator = GetComponentInChildren<Animator>();
    }

    public void Run(Transform[] path, bool jumpFirstSegment, bool hideAtEnd)
    {
        if (running != null)
            StopCoroutine(running);
        running = StartCoroutine(FollowPath(path, jumpFirstSegment, hideAtEnd));
    }

    IEnumerator FollowPath(Transform[] path, bool jumpFirstSegment, bool hideAtEnd)
    {
        SetRunning(true);

        for (int i = 0; i < path.Length; i++)
        {
            Vector3 target = path[i].position;
            target.z = transform.position.z;
            Face(target.x - transform.position.x);

            if (i == 0 && jumpFirstSegment)
                yield return JumpTo(target);
            else
                yield return RunTo(target);
        }

        SetRunning(false);
        running = null;

        if (hideAtEnd)
            gameObject.SetActive(false);
    }

    IEnumerator RunTo(Vector3 target)
    {
        while ((transform.position - target).sqrMagnitude > 0.0001f)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, runSpeed * Time.deltaTime);
            yield return null;
        }
    }

    IEnumerator JumpTo(Vector3 target)
    {
        Vector3 start = transform.position;
        for (float t = 0f; t < 1f; t += Time.deltaTime / jumpDuration)
        {
            Vector3 position = Vector3.Lerp(start, target, t);
            position.y += jumpHeight * 4f * t * (1f - t); // parabola: 0 at both ends, jumpHeight at the middle
            transform.position = position;
            yield return null;
        }
        transform.position = target;
    }

    void Face(float directionX)
    {
        if (spriteRenderer == null || Mathf.Abs(directionX) < 0.01f) return;
        spriteRenderer.flipX = spriteFacesRight ? directionX < 0f : directionX > 0f;
    }

    void SetRunning(bool value)
    {
        if (animator != null)
            animator.SetBool(RunningParam, value);
    }
}
