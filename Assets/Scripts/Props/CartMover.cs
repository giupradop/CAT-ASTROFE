using UnityEngine;
[RequireComponent(typeof(Rigidbody2D))]
public class CoverCart2D : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0f)] float speed = 1.2f;
    [SerializeField] bool startMovingRight = true;
    [SerializeField, Min(0f)] float pauseAtEnds = 0f;

    [Header("Collision")]
    [SerializeField] LayerMask obstacleLayers;
    [SerializeField, Min(0f)] float skin = 0.02f;
    public AudioSource impactSound;

    Rigidbody2D rb;
    ContactFilter2D filter;
    readonly RaycastHit2D[] hits = new RaycastHit2D[8];
    int direction;
    float pauseTimer;

    void Reset()
    {
        var body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.freezeRotation = true;

        filter = new ContactFilter2D();
        filter.SetLayerMask(obstacleLayers);
        filter.useTriggers = false;

        direction = startMovingRight ? 1 : -1;
    }

    void FixedUpdate()
    {
        if (pauseTimer > 0f)
        {
            pauseTimer -= Time.fixedDeltaTime;
            return;
        }

        float step = speed * Time.fixedDeltaTime;
        Vector2 move = Vector2.right * direction;

        int count = rb.Cast(move, filter, hits, step + skin);

        if (count > 0)
        {
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
                nearest = Mathf.Min(nearest, hits[i].distance);

            float allowed = Mathf.Clamp(nearest - skin, 0f, step);
            rb.MovePosition(rb.position + move * allowed);

            if (impactSound != null)
                impactSound.Play();
            direction = -direction;
            pauseTimer = pauseAtEnds;
            return;
        }

        rb.MovePosition(rb.position + move * step);
    }
}