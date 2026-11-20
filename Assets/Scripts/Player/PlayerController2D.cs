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

    [Header("Run Stamina")]
    [Tooltip("Seconds of running (Shift held while moving) a full bar lasts.")]
    [SerializeField, Min(0.1f)] float runDuration = 2f;
    [Tooltip("Seconds to fill the bar from empty to full.")]
    [SerializeField, Min(0.1f)] float refillDuration = 3f;
    [Tooltip("Seconds after Shift is released before the bar starts to fill.")]
    [SerializeField, Min(0f)] float refillDelay = 1f;
    [Tooltip("Position of the bar relative to Bigode's pivot.")]
    [SerializeField] Vector2 barOffset = new Vector2(0f, 1.4f);
    [SerializeField] Vector2 barSize = new Vector2(1f, 0.12f);
    [SerializeField, Min(0f)] float barBorder = 0.02f;
    [SerializeField] Color barFillColor = Color.white;
    [SerializeField] Color barBackColor = Color.black;
    [SerializeField] int barSortingOrder = 110;
    [Tooltip("Optional. Empty uses the unlit Sprites/Default shader, so the bar stays bright in dark scenes.")]
    [SerializeField] Material barMaterial;

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

    [Header("Audio")]
    [Tooltip("Jump sounds (there are 5). One random clip plays on every jump, never the same twice in a row.")]
    [SerializeField] AudioClip[] jumpClips = new AudioClip[5];
    [SerializeField, Range(0f, 1f)] float jumpVolume = 0.8f;
    [Tooltip("Played when Bigode touches the ground again after being in the air.")]
    [SerializeField] AudioClip landClip;
    [SerializeField, Range(0f, 1f)] float landVolume = 0.8f;
    [Tooltip("Shortest time in the air (seconds) that counts as a landing. Avoids a sound on tiny bumps.")]
    [SerializeField] float landMinAirTime = 0.15f;

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

    AudioSource sfxSource;
    int lastJump = -1;
    float airTime;

    // Run stamina: 1 = full bar, 0 = empty.
    float stamina = 1f;
    float refillDelayTimer;
    bool running;
    GameObject barRoot;
    Transform barFill;
    Texture2D barTexture;
    Sprite barSprite;
    Material barRuntimeMaterial;

    public float StaminaNormalized => stamina;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.spatialBlend = 0f;
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        animator = GetComponentInChildren<Animator>();
        defaultGravityScale = rb.gravityScale;
        idleTimer = idleTimeBeforeSit;

        groundFilter.SetLayerMask(groundLayer);
        groundFilter.SetNormalAngle(90f - maxGroundSlopeAngle, 90f + maxGroundSlopeAngle);

        CreateStaminaBar(); // last, so GetComponentInChildren above never finds the bar's sprites
    }

    void Update()
    {
        ReadInput();
        UpdateStamina();
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

    // ---------- Run stamina ----------

    void UpdateStamina()
    {
        bool moving = Mathf.Abs(moveInput) > 0.01f;
        running = runHeld && moving && stamina > 0f;

        if (running)
        {
            // Draining: a full bar lasts runDuration seconds of running.
            stamina = Mathf.Max(0f, stamina - Time.deltaTime / runDuration);
            refillDelayTimer = refillDelay;
        }
        else if (runHeld)
        {
            // Shift is still held (standing still, or the bar is empty): the bar does not fill yet.
            refillDelayTimer = refillDelay;
        }
        else if (refillDelayTimer > 0f)
        {
            // Shift released: wait refillDelay seconds first.
            refillDelayTimer -= Time.deltaTime;
        }
        else
        {
            stamina = Mathf.Min(1f, stamina + Time.deltaTime / refillDuration);
        }

        UpdateStaminaBar();
    }

    void CreateStaminaBar()
    {
        // A 1x1 white pixel stretched into the bar. The pivot is on the left, so scaling X empties the bar from the right.
        barTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        barTexture.SetPixel(0, 0, Color.white);
        barTexture.Apply();
        barSprite = Sprite.Create(barTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0f, 0.5f), 1f);

        Material material = barMaterial;
        if (material == null)
        {
            Shader shader = Shader.Find("Sprites/Default"); // unlit: not darkened by the scene's 2D lights
            if (shader != null)
            {
                barRuntimeMaterial = new Material(shader);
                material = barRuntimeMaterial;
            }
        }

        barRoot = new GameObject("StaminaBar");
        barRoot.transform.SetParent(transform, false);
        barRoot.transform.localPosition = barOffset;

        var back = new GameObject("Back").AddComponent<SpriteRenderer>();
        back.transform.SetParent(barRoot.transform, false);
        back.sprite = barSprite;
        back.color = barBackColor;
        back.sortingOrder = barSortingOrder;
        back.transform.localPosition = new Vector3(-barSize.x * 0.5f - barBorder, 0f, 0f);
        back.transform.localScale = new Vector3(barSize.x + 2f * barBorder, barSize.y + 2f * barBorder, 1f);

        var fill = new GameObject("Fill").AddComponent<SpriteRenderer>();
        fill.transform.SetParent(barRoot.transform, false);
        fill.sprite = barSprite;
        fill.color = barFillColor;
        fill.sortingOrder = barSortingOrder + 1;
        fill.transform.localPosition = new Vector3(-barSize.x * 0.5f, 0f, 0f);
        fill.transform.localScale = new Vector3(barSize.x, barSize.y, 1f);
        barFill = fill.transform;

        if (material != null)
        {
            back.sharedMaterial = material;
            fill.sharedMaterial = material;
        }

        barRoot.SetActive(false);
    }

    // The bar only shows while it is not full.
    void UpdateStaminaBar()
    {
        if (barRoot == null) return;

        bool visible = stamina < 0.999f;
        if (barRoot.activeSelf != visible)
            barRoot.SetActive(visible);

        if (visible)
            barFill.localScale = new Vector3(barSize.x * stamina, barSize.y, 1f);
    }

    void OnDestroy()
    {
        if (barSprite != null) Destroy(barSprite);
        if (barTexture != null) Destroy(barTexture);
        if (barRuntimeMaterial != null) Destroy(barRuntimeMaterial);
    }

    void CheckGrounded()
    {
        if (rb.IsTouching(groundFilter))
        {
            if (!isGrounded && airTime >= landMinAirTime)
                PlayLandSound();
            airTime = 0f;

            isGrounded = true;
            timeSinceGroundOverlap = 0f;
            coyoteTimer = coyoteTime;
        }
        else
        {
            timeSinceGroundOverlap += Time.fixedDeltaTime;
            if (timeSinceGroundOverlap >= groundLossGracePeriod)
            {
                isGrounded = false;
                airTime += Time.fixedDeltaTime;
            }
        }
    }

    void ApplyHorizontalMovement()
    {
        float targetSpeed = standUpLockTimer > 0f
            ? 0f
            : moveInput * moveSpeed * (running ? runSpeedMultiplier : 1f);
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
            PlayJumpSound();
        }
    }

    void PlayJumpSound()
    {
        AudioClip clip = PickClip(jumpClips, ref lastJump);
        if (clip != null)
            sfxSource.PlayOneShot(clip, jumpVolume);
    }

    void PlayLandSound()
    {
        if (landClip != null)
            sfxSource.PlayOneShot(landClip, landVolume);
    }

    static AudioClip PickClip(AudioClip[] clips, ref int last)
    {
        if (clips == null || clips.Length == 0) return null;

        int filled = 0;
        for (int i = 0; i < clips.Length; i++)
            if (clips[i] != null) filled++;
        if (filled == 0) return null;

        int pick;
        int safety = 0;
        do
        {
            pick = Random.Range(0, clips.Length);
            safety++;
        }
        while ((clips[pick] == null || (filled > 1 && pick == last)) && safety < 50);

        if (clips[pick] == null) return null;
        last = pick;
        return clips[pick];
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