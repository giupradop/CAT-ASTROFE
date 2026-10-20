using UnityEngine;

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
    [SerializeField] float obstacleCheckDistance = 0.6f;
    [SerializeField] Transform obstacleCheck;
    [SerializeField] Transform groundCheck;
    [SerializeField] Vector2 groundCheckSize = new Vector2(0.9f, 0.1f);
    [SerializeField] LayerMask groundLayer;
    [SerializeField] LayerMask obstacleLayer;

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

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        UpdateState();
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

        if (distance <= catchDistance)
        {
            CatchPlayer();
            return;
        }

        if (state == State.Patrol && distance <= detectionRadius)
            state = State.Chase;
        else if (state == State.Chase && distance >= loseRadius)
            state = State.Patrol;
    }

    void CatchPlayer()
    {
        hasCaughtPlayer = true;

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
    }

    void Move()
    {
        if (hasCaughtPlayer)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        float targetSpeed = state == State.Chase
            ? MoveTowardX(player.position.x, chaseStopDistanceX) * chaseSpeed
            : MoveTowardPatrolPoint() * patrolSpeed;

        rb.linearVelocity = new Vector2(targetSpeed, rb.linearVelocity.y);
        if (Mathf.Abs(targetSpeed) > 0.01f)
            facing = targetSpeed > 0f ? 1 : -1;
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
        if (!isGrounded || obstacleCheck == null) return;

        Vector2 direction = new Vector2(facing, 0f);
        bool obstacleAhead = Physics2D.Raycast(obstacleCheck.position, direction, obstacleCheckDistance, obstacleLayer);
        if (obstacleAhead)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
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

        if (groundCheck != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(groundCheck.position, groundCheckSize);
        }
        if (obstacleCheck != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(obstacleCheck.position, obstacleCheck.position + Vector3.right * facing * obstacleCheckDistance);
        }
    }
}
