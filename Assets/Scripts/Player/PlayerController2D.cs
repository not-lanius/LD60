using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PlayerController2D : MonoBehaviour
{
    [Header("Run")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float groundAcceleration = 80f;
    [SerializeField] private float groundDeceleration = 100f;
    [SerializeField] private float airAcceleration = 45f;

    [Header("Jump")]
    [SerializeField] private float jumpVelocity = 16f;
    [SerializeField] private float gravityScale = 4f;
    [SerializeField] private float fallGravityMultiplier = 1.7f;
    [SerializeField] private float jumpCutMultiplier = 0.45f;
    [SerializeField] private float coyoteTime = 0.1f;
    [SerializeField] private float jumpBufferTime = 0.12f;

    [Header("Dodge")]
    [SerializeField] private float dodgeSpeed = 22f;
    [SerializeField] private float dodgeDuration = 0.16f;
    [SerializeField] private float dodgeCooldown = 0.5f;
    [SerializeField] private bool canAirDodge = true;

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float groundCheckDistance = 0.08f;

    [Header("Fall")]
    [SerializeField] private float killY = -12f;

    [Header("References")]
    [SerializeField] private PlayerVisual2D visual;

    private Rigidbody2D body;
    private Collider2D bodyCollider;

    private float moveInput;
    private float coyoteTimer;
    private float jumpBufferTimer;
    private float dodgeTimer;
    private float dodgeCooldownTimer;
    private float dodgeDirection;
    private int facing = 1;
    private bool wasGrounded;
    private bool isDodging;
    private Vector2 spawnPoint;

    public bool IsGrounded { get; private set; }
    public bool IsDodging => isDodging;
    public Vector2 Velocity => body != null ? body.velocity : Vector2.zero;
    public int Facing => facing;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        body.sleepMode = RigidbodySleepMode2D.NeverSleep;
        spawnPoint = transform.position;
    }

    private void Update()
    {
        moveInput = Input.GetAxisRaw("Horizontal");
        if (moveInput != 0f)
        {
            facing = moveInput > 0f ? 1 : -1;
        }

        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
        {
            jumpBufferTimer = jumpBufferTime;
        }

        if (Input.GetKeyUp(KeyCode.Space) || Input.GetKeyUp(KeyCode.W) || Input.GetKeyUp(KeyCode.UpArrow))
        {
            if (body.velocity.y > 0f && !isDodging)
            {
                body.velocity = new Vector2(body.velocity.x, body.velocity.y * jumpCutMultiplier);
            }
        }

        if ((Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.K))
            && dodgeCooldownTimer <= 0f
            && !isDodging
            && (IsGrounded || canAirDodge))
        {
            StartDodge();
        }

        if (transform.position.y < killY)
        {
            Respawn();
        }

        if (visual != null)
        {
            visual.SetFacing(facing);
            visual.SetMotion(Mathf.Abs(body.velocity.x), IsGrounded, isDodging);
        }
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - dt);
        dodgeCooldownTimer = Mathf.Max(0f, dodgeCooldownTimer - dt);

        UpdateGrounded();

        if (IsGrounded)
        {
            coyoteTimer = coyoteTime;
        }
        else
        {
            coyoteTimer = Mathf.Max(0f, coyoteTimer - dt);
        }

        if (isDodging)
        {
            dodgeTimer -= dt;
            body.velocity = new Vector2(dodgeDirection * dodgeSpeed, 0f);
            if (dodgeTimer <= 0f)
            {
                EndDodge();
            }
        }
        else
        {
            ApplyHorizontalMovement(dt);

            if (jumpBufferTimer > 0f && coyoteTimer > 0f)
            {
                Jump();
            }
        }

        ApplyGravity();
        NotifyLanding();
    }

    private void UpdateGrounded()
    {
        Bounds bounds = bodyCollider.bounds;
        Vector2 origin = new Vector2(bounds.center.x, bounds.min.y + 0.02f);
        Vector2 size = new Vector2(bounds.size.x * 0.8f, 0.04f);
        RaycastHit2D hit = Physics2D.BoxCast(origin, size, 0f, Vector2.down, groundCheckDistance, groundMask);
        IsGrounded = hit.collider != null && body.velocity.y <= 0.1f;
    }

    private void ApplyHorizontalMovement(float dt)
    {
        float targetSpeed = moveInput * moveSpeed;
        float acceleration;

        if (moveInput != 0f)
        {
            acceleration = IsGrounded ? groundAcceleration : airAcceleration;
        }
        else
        {
            acceleration = IsGrounded ? groundDeceleration : airAcceleration * 0.6f;
        }

        float newX = Mathf.MoveTowards(body.velocity.x, targetSpeed, acceleration * dt);
        body.velocity = new Vector2(newX, body.velocity.y);
    }

    private void Jump()
    {
        jumpBufferTimer = 0f;
        coyoteTimer = 0f;
        body.velocity = new Vector2(body.velocity.x, jumpVelocity);
        if (visual != null)
        {
            visual.Jump();
        }
    }

    private void ApplyGravity()
    {
        if (isDodging)
        {
            body.gravityScale = 0f;
            return;
        }

        body.gravityScale = body.velocity.y < 0f ? gravityScale * fallGravityMultiplier : gravityScale;
    }

    private void StartDodge()
    {
        isDodging = true;
        dodgeTimer = dodgeDuration;
        dodgeCooldownTimer = dodgeDuration + dodgeCooldown;
        dodgeDirection = moveInput != 0f ? Mathf.Sign(moveInput) : facing;
        facing = dodgeDirection > 0f ? 1 : -1;
        body.velocity = new Vector2(dodgeDirection * dodgeSpeed, 0f);
    }

    private void EndDodge()
    {
        isDodging = false;
        body.gravityScale = gravityScale;
        body.velocity = new Vector2(dodgeDirection * moveSpeed * 0.6f, body.velocity.y);
    }

    private void NotifyLanding()
    {
        if (IsGrounded && !wasGrounded)
        {
            if (visual != null)
            {
                visual.Land();
            }
        }

        wasGrounded = IsGrounded;
    }

    private void Respawn()
    {
        isDodging = false;
        dodgeCooldownTimer = 0f;
        body.velocity = Vector2.zero;
        body.gravityScale = gravityScale;
        transform.position = spawnPoint;
    }
}
