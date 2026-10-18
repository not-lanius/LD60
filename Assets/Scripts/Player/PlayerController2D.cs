using System.Collections.Generic;
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
    [SerializeField] private float maxFallSpeed = 22f;
    [SerializeField] private float jumpCutMultiplier = 0.45f;
    [SerializeField] private float coyoteTime = 0.1f;
    [SerializeField] private float jumpBufferTime = 0.12f;

    [Header("Dodge")]
    [SerializeField] private float dodgeSpeed = 22f;
    [SerializeField] private float dodgeDuration = 0.16f;
    [SerializeField] private float dodgeCooldown = 0.5f;
    [SerializeField] private bool canAirDodge = true;

    [Header("Ladder")]
    [SerializeField] private float climbSpeed = 5f;
    [SerializeField] private float ladderHorizontalSpeed = 3.5f;
    [SerializeField] private float ladderJumpVelocity = 14f;
    [SerializeField] private float ladderGrabCooldown = 0.25f;

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float groundCheckDistance = 0.08f;

    [Header("References")]
    [SerializeField] private PlayerVisual2D visual;

    private Rigidbody2D body;
    private Collider2D bodyCollider;

    private readonly List<Ladder> nearbyLadders = new List<Ladder>();

    private float moveInput;
    private float climbInput;
    private float coyoteTimer;
    private float jumpBufferTimer;
    private float dodgeTimer;
    private float dodgeCooldownTimer;
    private float dodgeDirection;
    private float knockbackTimer;
    private float ladderGrabCooldownTimer;
    private int facing = 1;
    private bool wasGrounded;
    private bool isDodging;
    private bool isClimbing;
    private bool suppressLadderGrab;
    private Vector2 spawnPoint;
    private Ladder activeLadder;
    private Bounds activeLadderBounds;

    public bool IsGrounded { get; private set; }
    public bool IsDodging => isDodging;
    public bool IsClimbing => isClimbing;
    public bool IsKnockedBack => knockbackTimer > 0f;
    public Vector2 Velocity => body != null ? body.velocity : Vector2.zero;
    public Vector2 SpawnPoint => spawnPoint;
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
        climbInput = Input.GetAxisRaw("Vertical");

        if (moveInput != 0f)
        {
            facing = moveInput > 0f ? 1 : -1;
        }

        Ladder ladder = FindClosestLadder(out Bounds ladderBounds);
        if (ladder == null)
        {
            suppressLadderGrab = false;
        }

        TryStartClimbing(ladder, ladderBounds);

        bool jumpPressed = Input.GetKeyDown(KeyCode.Space);
        bool upPressed = Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow);

        if (jumpPressed)
        {
            if (isClimbing)
            {
                JumpOffLadder();
            }
            else
            {
                jumpBufferTimer = jumpBufferTime;
            }
        }
        else if (upPressed && !isClimbing && ladder == null)
        {
            jumpBufferTimer = jumpBufferTime;
        }

        if ((Input.GetKeyUp(KeyCode.Space) || Input.GetKeyUp(KeyCode.W) || Input.GetKeyUp(KeyCode.UpArrow))
            && !isClimbing)
        {
            if (body.velocity.y > 0f && !isDodging)
            {
                body.velocity = new Vector2(body.velocity.x, body.velocity.y * jumpCutMultiplier);
            }
        }

        if ((Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.K))
            && dodgeCooldownTimer <= 0f
            && knockbackTimer <= 0f
            && !isDodging
            && !isClimbing
            && (IsGrounded || canAirDodge))
        {
            StartDodge();
        }

        if (visual != null)
        {
            visual.SetFacing(facing);
            visual.SetMotion(
                isClimbing ? 0f : Mathf.Abs(body.velocity.x),
                isClimbing || IsGrounded,
                isDodging);
        }
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - dt);
        dodgeCooldownTimer = Mathf.Max(0f, dodgeCooldownTimer - dt);
        knockbackTimer = Mathf.Max(0f, knockbackTimer - dt);
        ladderGrabCooldownTimer = Mathf.Max(0f, ladderGrabCooldownTimer - dt);

        UpdateGrounded();

        if (IsGrounded)
        {
            coyoteTimer = coyoteTime;
        }
        else
        {
            coyoteTimer = Mathf.Max(0f, coyoteTimer - dt);
        }

        if (isClimbing)
        {
            if (activeLadder == null || !activeLadder.HasPlayer(this))
            {
                StopClimbing();
            }
            else
            {
                ClimbMovement(dt);
                NotifyLanding();
                return;
            }
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
            if (knockbackTimer <= 0f)
            {
                ApplyHorizontalMovement(dt);

                if (jumpBufferTimer > 0f && coyoteTimer > 0f)
                {
                    Jump();
                }
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
        suppressLadderGrab = true;
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

        bool slowMotion = Time.timeScale < 0.99f;
        body.gravityScale = !slowMotion && body.velocity.y < 0f ? gravityScale * fallGravityMultiplier : gravityScale;

        if (body.velocity.y < -maxFallSpeed)
        {
            body.velocity = new Vector2(body.velocity.x, -maxFallSpeed);
        }
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
            suppressLadderGrab = false;
            if (visual != null)
            {
                visual.Land();
            }
        }

        wasGrounded = IsGrounded;
    }

    public void RegisterLadder(Ladder ladder)
    {
        if (ladder != null && !nearbyLadders.Contains(ladder))
        {
            nearbyLadders.Add(ladder);
        }
    }

    public void UnregisterLadder(Ladder ladder)
    {
        nearbyLadders.Remove(ladder);

        if (activeLadder == ladder)
        {
            StopClimbing();
        }
    }

    private Ladder FindClosestLadder(out Bounds ladderBounds)
    {
        Ladder closest = null;
        ladderBounds = default;
        float closestDistance = float.MaxValue;
        Bounds playerBounds = bodyCollider.bounds;

        for (int i = nearbyLadders.Count - 1; i >= 0; i--)
        {
            Ladder ladder = nearbyLadders[i];
            if (ladder == null)
            {
                nearbyLadders.RemoveAt(i);
                continue;
            }

            if (!ladder.HasPlayer(this))
            {
                continue;
            }

            if (!ladder.TryGetClimbBounds(playerBounds, out Bounds segmentBounds))
            {
                continue;
            }

            float distance = Mathf.Abs(segmentBounds.center.x - playerBounds.center.x);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = ladder;
                ladderBounds = segmentBounds;
            }
        }

        return closest;
    }

    private void TryStartClimbing(Ladder ladder, Bounds ladderBounds)
    {
        if (ladder == null || isClimbing || isDodging || knockbackTimer > 0f
            || ladderGrabCooldownTimer > 0f || suppressLadderGrab)
        {
            return;
        }

        Bounds bounds = bodyCollider.bounds;
        float feetY = bounds.min.y;
        bool centerInsideLadder = Contains2D(ladderBounds, bounds.center);

        if (!IsGrounded)
        {
            if (centerInsideLadder)
            {
                StartClimbing(ladder, ladderBounds);
            }

            return;
        }

        bool wantsUp = climbInput > 0.1f && feetY < ladderBounds.max.y - 0.65f;
        if (wantsUp && centerInsideLadder)
        {
            StartClimbing(ladder, ladderBounds);
        }
    }

    private static bool Contains2D(Bounds bounds, Vector2 point)
    {
        return point.x >= bounds.min.x
            && point.x <= bounds.max.x
            && point.y >= bounds.min.y
            && point.y <= bounds.max.y;
    }

    private void StartClimbing(Ladder ladder, Bounds ladderBounds)
    {
        isClimbing = true;
        activeLadder = ladder;
        activeLadderBounds = ladderBounds;
        isDodging = false;
        jumpBufferTimer = 0f;
        coyoteTimer = 0f;
        body.gravityScale = 0f;
        body.velocity = Vector2.zero;
    }

    private void ClimbMovement(float dt)
    {
        if (activeLadder == null)
        {
            return;
        }

        body.gravityScale = 0f;

        Bounds climbBounds = activeLadderBounds;
        float minY = climbBounds.min.y + 0.05f;
        float maxY = climbBounds.max.y - 0.05f;
        float minX = climbBounds.min.x + 0.05f;
        float maxX = climbBounds.max.x - 0.05f;
        float horizontalVelocity = moveInput * ladderHorizontalSpeed;

        if ((body.position.x <= minX && horizontalVelocity < 0f)
            || (body.position.x >= maxX && horizontalVelocity > 0f))
        {
            horizontalVelocity = 0f;
        }

        body.velocity = new Vector2(horizontalVelocity, climbInput * climbSpeed);

        if (body.position.y <= minY)
        {
            body.velocity = new Vector2(body.velocity.x, Mathf.Max(0f, body.velocity.y));
        }

        if (body.position.y >= maxY)
        {
            body.velocity = new Vector2(body.velocity.x, Mathf.Min(0f, body.velocity.y));
        }

        float feetY = bodyCollider.bounds.min.y;
        if (climbInput < -0.1f && IsGrounded && feetY <= climbBounds.min.y + 0.45f)
        {
            StopClimbing();
        }
    }

    private void JumpOffLadder()
    {
        if (!isClimbing)
        {
            return;
        }

        StopClimbing();
        suppressLadderGrab = true;
        ladderGrabCooldownTimer = ladderGrabCooldown;
        body.velocity = new Vector2(moveInput * moveSpeed, ladderJumpVelocity);
        if (visual != null)
        {
            visual.Jump();
        }
    }

    private void StopClimbing()
    {
        if (!isClimbing)
        {
            activeLadder = null;
            activeLadderBounds = default;
            return;
        }

        isClimbing = false;
        activeLadder = null;
        activeLadderBounds = default;
        body.gravityScale = gravityScale;
        body.velocity = new Vector2(body.velocity.x, 0f);
    }

    public void ApplyKnockback(Vector2 impulse, float lockDuration)
    {
        StopClimbing();
        isDodging = false;
        suppressLadderGrab = true;
        knockbackTimer = Mathf.Max(knockbackTimer, lockDuration);
        jumpBufferTimer = 0f;
        coyoteTimer = 0f;
        ladderGrabCooldownTimer = ladderGrabCooldown;
        body.gravityScale = gravityScale;
        body.velocity = impulse;
    }

    public void RespawnAtSpawnPoint()
    {
        StopClimbing();
        isDodging = false;
        dodgeCooldownTimer = 0f;
        dodgeTimer = 0f;
        knockbackTimer = 0f;
        ladderGrabCooldownTimer = 0f;
        suppressLadderGrab = false;
        body.velocity = Vector2.zero;
        body.gravityScale = gravityScale;
        transform.position = spawnPoint;
        body.position = spawnPoint;
    }
}
