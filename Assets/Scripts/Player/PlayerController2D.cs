using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class PlayerController2D : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] float moveSpeed = 7f;
    [SerializeField] float runSpeedMultiplier = 1.5f;
    [SerializeField] float groundAcceleration = 60f;
    [SerializeField] float groundDeceleration = 70f;
    [SerializeField] float airAcceleration = 30f;
    [SerializeField] float airDeceleration = 30f;

    [Header("Jump")]
    [SerializeField] float jumpForce = 14f;
    [SerializeField] float coyoteTime = 0.1f;
    [SerializeField] float jumpBufferTime = 0.1f;
    [SerializeField] float fallGravityMultiplier = 2f;

    [Header("Ground Check")]
    [SerializeField] LayerMask groundLayer;
    [SerializeField] float maxGroundSlopeAngle = 45f;
    [SerializeField] float groundLossGracePeriod = 0.06f;

    [Header("Sitting")]
    [SerializeField] float idleTimeBeforeSit = 3f;
    [SerializeField] float standUpLockDuration = 1f;

    static readonly int SpeedParam = Animator.StringToHash("Speed");
    static readonly int GroundedParam = Animator.StringToHash("Grounded");
    static readonly int VerticalVelocityParam = Animator.StringToHash("VerticalVelocity");
    static readonly int SittingParam = Animator.StringToHash("Sitting");

    Rigidbody2D rb;
    SpriteRenderer spriteRenderer;
    Animator animator;
    ContactFilter2D groundFilter;

    float moveInput;
    bool runHeld;
    float coyoteTimer;
    float jumpBufferTimer;
    bool isGrounded;
    float timeSinceGroundOverlap;
    float defaultGravityScale;
    float idleTimer;
    bool sitting;
    float standUpLockTimer;
    bool controlEnabled = true;
    float forcedMoveInput;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        animator = GetComponentInChildren<Animator>();
        defaultGravityScale = rb.gravityScale;
        idleTimer = idleTimeBeforeSit;

        // Only contacts whose normal points roughly up count as ground (flat floor = 90°).
        groundFilter.SetLayerMask(groundLayer);
        groundFilter.SetNormalAngle(90f - maxGroundSlopeAngle, 90f + maxGroundSlopeAngle);
    }

    void Update()
    {
        ReadInput();
        coyoteTimer -= Time.deltaTime;
        jumpBufferTimer -= Time.deltaTime;
        UpdateIdleTimer();
        FlipSprite();
        UpdateAnimator();
    }

    void FixedUpdate()
    {
        CheckGrounded();
        ApplyHorizontalMovement();
        ApplyJump();
        ApplyFallGravity();
    }

    public void SetControlEnabled(bool enabled)
    {
        controlEnabled = enabled;
    }

    // Movement used while control is disabled (cutscenes): -1 left, 0 stop, 1 right.
    public void SetForcedMove(float direction)
    {
        forcedMoveInput = Mathf.Clamp(direction, -1f, 1f);
    }

    void ReadInput()
    {
        moveInput = 0f;
        runHeld = false;

        if (!controlEnabled)
        {
            moveInput = forcedMoveInput;
            return;
        }

        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) moveInput -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) moveInput += 1f;

        runHeld = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;

        if (keyboard.spaceKey.wasPressedThisFrame)
            jumpBufferTimer = jumpBufferTime;

        if (keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame)
            idleTimer = idleTimeBeforeSit;
    }

    void CheckGrounded()
    {
        if (rb.IsTouching(groundFilter))
        {
            isGrounded = true;
            timeSinceGroundOverlap = 0f;
            coyoteTimer = coyoteTime;
        }
        else
        {
            timeSinceGroundOverlap += Time.fixedDeltaTime;
            if (timeSinceGroundOverlap >= groundLossGracePeriod)
                isGrounded = false;
        }
    }

    void ApplyHorizontalMovement()
    {
        float targetSpeed = standUpLockTimer > 0f
            ? 0f
            : moveInput * moveSpeed * (runHeld ? runSpeedMultiplier : 1f);
        bool accelerating = Mathf.Abs(targetSpeed) > 0.01f;
        float accelRate = isGrounded
            ? (accelerating ? groundAcceleration : groundDeceleration)
            : (accelerating ? airAcceleration : airDeceleration);

        float newSpeedX = Mathf.MoveTowards(rb.linearVelocity.x, targetSpeed, accelRate * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector2(newSpeedX, rb.linearVelocity.y);
    }

    void ApplyJump()
    {
        if (coyoteTimer > 0f && jumpBufferTimer > 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            coyoteTimer = 0f;
            jumpBufferTimer = 0f;
        }
    }

    void ApplyFallGravity()
    {
        rb.gravityScale = rb.linearVelocity.y < 0f
            ? defaultGravityScale * fallGravityMultiplier
            : defaultGravityScale;
    }

    void FlipSprite()
    {
        if (spriteRenderer == null || Mathf.Abs(moveInput) < 0.01f) return;
        spriteRenderer.flipX = moveInput < 0f;
    }

    void UpdateIdleTimer()
    {
        if (Mathf.Abs(moveInput) > 0.01f || !isGrounded)
            idleTimer = 0f;
        else
            idleTimer += Time.deltaTime;

        bool wasSitting = sitting;
        sitting = idleTimer >= idleTimeBeforeSit;
        if (wasSitting && !sitting)
            standUpLockTimer = standUpLockDuration;

        if (standUpLockTimer > 0f)
            standUpLockTimer -= Time.deltaTime;
    }

    void UpdateAnimator()
    {
        if (animator == null) return;
        animator.SetFloat(SpeedParam, Mathf.Abs(rb.linearVelocity.x));
        animator.SetBool(GroundedParam, isGrounded);
        animator.SetFloat(VerticalVelocityParam, rb.linearVelocity.y);
        animator.SetBool(SittingParam, sitting);
    }
}
