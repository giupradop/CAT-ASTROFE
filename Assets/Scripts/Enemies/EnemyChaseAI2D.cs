using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class EnemyChaseAI2D : MonoBehaviour
{
    enum State { Patrol, Chase }

    [Header("Patrol")]
    [SerializeField] Transform[] patrolPoints;
    [SerializeField] float patrolSpeed = 2f;

    [Header("Chase")]
    [SerializeField] Transform player;
    [SerializeField] float chaseSpeed = 4.5f;
    [SerializeField] float detectionRadius = 5f;
    [SerializeField] float loseRadius = 8f;
    [SerializeField] float catchDistance = 0.6f;
    [SerializeField] float chaseStopDistanceX = 0.4f;

    [Header("Obstacle Jump")]
    [SerializeField] float jumpForce = 12f;
    [Tooltip("The obstacle check is a box (width x height) centered on the Obstacle Check transform, in front of the dog. Move the transform forward to look farther ahead.")]
    [SerializeField] Transform obstacleCheck;
    [SerializeField] Vector2 obstacleCheckSize = new Vector2(0.4f, 0.8f);
    [Tooltip("If the dog is stuck in the air against an obstacle, it may jump again after this many seconds since its last jump.")]
    [SerializeField, Min(0f)] float retryJumpDelay = 0.3f;
    [Tooltip("How many extra jumps in the air it may use before touching the ground again. 0 = never jump again in the air.")]
    [SerializeField, Min(0)] int maxMidairJumps = 2;
    [SerializeField] Transform groundCheck;
    [SerializeField] Vector2 groundCheckSize = new Vector2(0.9f, 0.1f);
    [SerializeField] LayerMask groundLayer;
    [SerializeField] LayerMask obstacleLayer;

    [Header("Audio")]
    [Tooltip("Plays when Bigode is caught (the dog also barks at that moment).")]
    public AudioSource deathSound;
    [Tooltip("The bark. Plays when the dog first sees Bigode, every few seconds during the chase, and when it catches him.")]
    public AudioSource barkSound;
    [Tooltip("Average seconds between barks while chasing.")]
    [SerializeField, Min(0.1f)] float barkInterval = 3f;
    [Tooltip("0 = a bark exactly every 'Bark Interval' seconds. 1 = the time between barks varies a lot, from almost 0 to twice the interval.")]
    [SerializeField, Range(0f, 1f)] float barkRandomness = 0.4f;

    [Header("Fear - Camera Zoom")]
    [Tooltip("The Cinemachine camera to zoom. Empty = the first CinemachineCamera found in the scene.")]
    [SerializeField] CinemachineCamera virtualCamera;
    [Tooltip("Distance to Bigode (X) at which the camera starts to zoom in.")]
    [SerializeField, Min(0f)] float zoomStartDistance = 6f;
    [Tooltip("Distance at which the zoom reaches its maximum.")]
    [SerializeField, Min(0f)] float zoomFullDistance = 1.5f;
    [Tooltip("How much the view closes at the maximum zoom. 0.3 = the visible area becomes 30% smaller.")]
    [SerializeField, Range(0f, 0.8f)] float zoomInAmount = 0.3f;
    [Tooltip("Seconds the zoom takes to follow. Bigger = slower, smoother.")]
    [SerializeField, Min(0.01f)] float zoomSmoothTime = 0.3f;

    [Header("Fear - Slowdown")]
    [Tooltip("Distance to Bigode (Y) at which the dog starts to slow down while chasing.")]
    [SerializeField, Min(0f)] float slowStartDistance = 4f;
    [Tooltip("Distance at which the slowdown reaches its maximum.")]
    [SerializeField, Min(0f)] float slowFullDistance = 1.5f;
    [Tooltip("Largest speed loss (Z), reached at 'Slow Full Distance'. 0.3 = the dog runs 30% slower. 0 = no slowdown.")]
    [SerializeField, Range(0f, 0.9f)] float maxSlowdown = 0.3f;

    [Header("Death")]
    [Tooltip("Wait before the screen starts to fade after Bigode is caught.")]
    [SerializeField] float restartDelay = 0.4f;
    [SerializeField] float fadeDuration = 0.4f;

    static readonly int SpeedParam = Animator.StringToHash("Speed");
    static readonly int GroundedParam = Animator.StringToHash("Grounded");
    static readonly int VerticalVelocityParam = Animator.StringToHash("VerticalVelocity");
    static readonly int SittingParam = Animator.StringToHash("Sitting");

    Rigidbody2D rb;
    SpriteRenderer spriteRenderer;
    Animator animator;

    State state = State.Patrol;
    int patrolTargetIndex;
    bool isGrounded;
    int facing = 1;
    bool hasCaughtPlayer;
    float barkTimer;
    float lastJumpTime = float.NegativeInfinity;
    int midairJumpsUsed;

    float distanceToPlayer = float.MaxValue;
    float baseOrthoSize;
    float currentOrthoSize;
    float zoomVelocity;
    bool zoomApplied;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        // The sounds must never start by themselves or repeat on their own.
        if (deathSound != null) { deathSound.playOnAwake = false; deathSound.loop = false; }
        if (barkSound != null) { barkSound.playOnAwake = false; barkSound.loop = false; }
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        animator = GetComponentInChildren<Animator>();
    }

    void Start()
    {
        if (virtualCamera == null)
            virtualCamera = FindFirstObjectByType<CinemachineCamera>();

        if (virtualCamera != null)
        {
            baseOrthoSize = virtualCamera.Lens.OrthographicSize;
            currentOrthoSize = baseOrthoSize;
        }
    }

    void OnDisable()
    {
        RestoreZoom();
    }

    void Update()
    {
        UpdateState();
        UpdateBarking();
        FlipSprite();
        UpdateAnimator();
    }

    void FixedUpdate()
    {
        CheckGrounded();
        Move();
        CheckObstacleJump();
    }

    void UpdateState()
    {
        if (player == null || hasCaughtPlayer) return;
        float distance = Vector2.Distance(transform.position, player.position);
        distanceToPlayer = distance;

        if (distance <= catchDistance)
        {
            CatchPlayer();
            return;
        }

        if (state == State.Patrol && distance <= detectionRadius)
        {
            state = State.Chase;
            Bark(); // just saw Bigode: bark right away
            barkTimer = NextBarkInterval();
        }
        else if (state == State.Chase && distance >= loseRadius)
            state = State.Patrol;
    }

    // While chasing, barks again every barkInterval seconds (varied by barkRandomness).
    void UpdateBarking()
    {
        if (state != State.Chase || hasCaughtPlayer) return;

        barkTimer -= Time.deltaTime;
        if (barkTimer <= 0f)
        {
            Bark();
            barkTimer = NextBarkInterval();
        }
    }

    float NextBarkInterval()
    {
        float spread = barkInterval * barkRandomness;
        return Mathf.Max(0.1f, barkInterval + Random.Range(-spread, spread));
    }

    void Bark()
    {
        if (barkSound != null)
            barkSound.Play();
    }

    void CatchPlayer()
    {
        hasCaughtPlayer = true;

        Bark(); // barks when it catches Bigode, on top of the death sound
        if (deathSound != null)
            deathSound.Play();

        var playerController = player.GetComponent<PlayerController2D>();
        if (playerController != null)
            playerController.enabled = false;

        var playerRb = player.GetComponent<Rigidbody2D>();
        if (playerRb != null)
            playerRb.linearVelocity = Vector2.zero;

        var playerAnimator = player.GetComponentInChildren<Animator>();
        if (playerAnimator != null)
        {
            playerAnimator.SetFloat(SpeedParam, 0f);
            playerAnimator.SetBool(SittingParam, true);
        }

        Debug.Log("Bigode foi capturado pelo Trovao!");

        // Waits restartDelay, fades to black, reloads this same scene, fades back in.
        // The scene has to be in the Build Settings, or SceneTransition logs an error and nothing reloads.
        SceneTransition.LoadScene(SceneManager.GetActiveScene().name, restartDelay, fadeDuration);
    }

    void Move()
    {
        if (hasCaughtPlayer)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        float targetSpeed = state == State.Chase
            ? MoveTowardX(player.position.x, chaseStopDistanceX) * chaseSpeed * ChaseSpeedFactor()
            : MoveTowardPatrolPoint() * patrolSpeed;

        rb.linearVelocity = new Vector2(targetSpeed, rb.linearVelocity.y);
        if (Mathf.Abs(targetSpeed) > 0.01f)
            facing = targetSpeed > 0f ? 1 : -1;
    }

    // 1 = full speed. It falls to (1 - maxSlowdown) as the dog gets from slowStartDistance to slowFullDistance of Bigode.
    float ChaseSpeedFactor()
    {
        float t = ProximityFactor(slowStartDistance, slowFullDistance);
        return 1f - maxSlowdown * t;
    }

    // 0 when the player is at or beyond 'startDistance', 1 when at or closer than 'fullDistance', smooth in between.
    float ProximityFactor(float startDistance, float fullDistance)
    {
        if (startDistance <= fullDistance)
            return distanceToPlayer <= fullDistance ? 1f : 0f;

        float t = Mathf.InverseLerp(startDistance, fullDistance, distanceToPlayer);
        return t * t * (3f - 2f * t);
    }

    // The camera zooms in as the dog gets close, for the feeling of fear.
    void LateUpdate()
    {
        if (virtualCamera == null) return;

        float t = ProximityFactor(zoomStartDistance, zoomFullDistance);
        float target = baseOrthoSize * (1f - zoomInAmount * t);
        currentOrthoSize = Mathf.SmoothDamp(currentOrthoSize, target, ref zoomVelocity, zoomSmoothTime);

        bool atBase = Mathf.Abs(currentOrthoSize - baseOrthoSize) < 0.001f && t <= 0f;
        if (atBase)
        {
            if (zoomApplied)
                RestoreZoom(); // back to the original size exactly, then stop touching the camera
            return;
        }

        SetOrthoSize(currentOrthoSize);
        zoomApplied = true;
    }

    void RestoreZoom()
    {
        if (virtualCamera != null && zoomApplied)
            SetOrthoSize(baseOrthoSize);
        currentOrthoSize = baseOrthoSize;
        zoomVelocity = 0f;
        zoomApplied = false;
    }

    void SetOrthoSize(float size)
    {
        LensSettings lens = virtualCamera.Lens;
        lens.OrthographicSize = size;
        virtualCamera.Lens = lens;
    }

    float MoveTowardX(float targetX, float deadZone = 0f)
    {
        float dx = targetX - transform.position.x;
        if (Mathf.Abs(dx) <= deadZone)
            return 0f;
        return Mathf.Sign(dx);
    }

    float MoveTowardPatrolPoint()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) return 0f;

        Transform target = patrolPoints[patrolTargetIndex];
        if (Mathf.Abs(target.position.x - transform.position.x) < 0.2f)
            patrolTargetIndex = (patrolTargetIndex + 1) % patrolPoints.Length;

        return MoveTowardX(target.position.x);
    }

    void CheckGrounded()
    {
        isGrounded = groundCheck != null &&
            Physics2D.OverlapBox(groundCheck.position, groundCheckSize, 0f, groundLayer);
    }

    void CheckObstacleJump()
    {
        if (obstacleCheck == null) return;

        if (isGrounded)
            midairJumpsUsed = 0;

        bool obstacleAhead = Physics2D.OverlapBox(GetObstacleCheckCenter(), obstacleCheckSize, 0f, obstacleLayer);
        if (!obstacleAhead) return;

        if (isGrounded)
        {
            Jump();
            return;
        }

        // Stuck in the air against the obstacle (it did not clear it): try again, a limited number of times.
        bool cooldownOver = Time.time - lastJumpTime >= retryJumpDelay;
        bool notRising = rb.linearVelocity.y <= 0.5f; // at the top of the jump or already falling
        if (cooldownOver && notRising && midairJumpsUsed < maxMidairJumps)
        {
            midairJumpsUsed++;
            Jump();
        }
    }

    void Jump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        lastJumpTime = Time.time;
    }

    // The box is always in front of the dog: if the transform was placed on one side, it is mirrored to the facing side.
    Vector2 GetObstacleCheckCenter()
    {
        float offsetX = Mathf.Abs(obstacleCheck.position.x - transform.position.x) * facing;
        return new Vector2(transform.position.x + offsetX, obstacleCheck.position.y);
    }

    void FlipSprite()
    {
        if (spriteRenderer != null)
            spriteRenderer.flipX = facing < 0f;
    }

    void UpdateAnimator()
    {
        if (animator == null) return;
        animator.SetFloat(SpeedParam, Mathf.Abs(rb.linearVelocity.x));
        animator.SetBool(GroundedParam, isGrounded);
        animator.SetFloat(VerticalVelocityParam, rb.linearVelocity.y);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, loseRadius);

        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, zoomStartDistance);   // the camera starts to zoom
        Gizmos.color = new Color(0.7f, 0.3f, 1f);
        Gizmos.DrawWireSphere(transform.position, slowStartDistance);   // the dog starts to slow down

        if (groundCheck != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(groundCheck.position, groundCheckSize);
        }
        if (obstacleCheck != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(GetObstacleCheckCenter(), obstacleCheckSize);
        }
    }
}