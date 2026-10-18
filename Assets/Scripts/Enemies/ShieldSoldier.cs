using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class ShieldSoldier : MonoBehaviour, IDamageable, ITypedDamageable
{
    public enum State
    {
        Patrol,
        ShieldChase,
        Guard,
        ChargeWindup,
        Charging,
        Recover,
        Stagger,
        Dead
    }

    [Header("Perception")]
    [SerializeField] private Transform target;
    [SerializeField] private float detectRange = 12f;
    [SerializeField] private float verticalDetectTolerance = 2.5f;
    [SerializeField] private float attackTriggerRange = 6f;

    [Header("Movement")]
    [SerializeField] private float patrolSpeed = 1.5f;
    [SerializeField] private float chaseSpeed = 3.2f;
    [SerializeField] private float patrolRange = 2.5f;

    [Header("Shield")]
    [Range(0f, 1f)]
    [SerializeField] private float physicalDamageReduction = 1f;
    [Range(0f, 1f)]
    [SerializeField] private float spellDamageReduction = 0.5f;
    [Range(1f, 360f)]
    [SerializeField] private float shieldReductionAngle = 180f;

    [Header("Charge")]
    [SerializeField] private float windupDuration = 0.6f;
    [SerializeField] private float chargeSpeed = 25f;
    [SerializeField] private float chargeMaxDistance = 10f;
    [SerializeField] private float chargeMaxDuration = 0.6f;
    [SerializeField] private float recoverDuration = 1.2f;
    [SerializeField] private float chargeCooldown = 2f;
    [SerializeField] private float chargeDamage = 18f;
    [SerializeField] private float chargeKnockback = 10f;
    [SerializeField] private float wallStaggerDuration = 0.6f;

    [Header("Defense")]
    [SerializeField] private float maxHealth = 150f;
    [SerializeField] private float hitStunDuration = 0.12f;

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundMask = 1 << 6;
    [SerializeField] private LayerMask wallMask = (1 << 6) | 1;
    [SerializeField] private float wallCheckDistance = 0.55f;
    [SerializeField] private float ledgeCheckAhead = 0.55f;
    [SerializeField] private float ledgeCheckDepth = 0.9f;
    [SerializeField] private float fallRecoveryDepth = 5f;

    [Header("Visuals")]
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private Transform shieldRoot;
    [SerializeField] private SpriteRenderer shieldRenderer;
    [SerializeField] private Transform healthBarRoot;
    [SerializeField] private Transform healthBarFill;
    [SerializeField] private float barWidth = 1.3f;
    [SerializeField] private Color flashColor = Color.white;
    [SerializeField] private float flashDuration = 0.1f;
    [SerializeField] private bool logStateChanges;

    private static Sprite whiteSprite;
    private static ShieldSoldier lastTouched;

    private Rigidbody2D body;
    private Collider2D bodyCollider;
    private PlayerHealth playerHealth;
    private SpriteRenderer[] bodyParts;
    private Color[] bodyPartColors;
    private Color bodyBaseColor;
    private Vector3 bodyBaseScale;

    private State state;
    private float stateTimer;
    private float chargeCooldownTimer;
    private float hitStunTimer;
    private float flashTimer;
    private float health;
    private float chargeElapsed;
    private Vector2 spawnPoint;
    private Vector2 chargeStartPosition;
    private int facing = -1;
    private int patrolDirection = 1;
    private bool chargeHitPlayer;

    public bool IsDead => state == State.Dead;
    public float CurrentHealth => health;
    public float MaxHealth => maxHealth;
    public string CurrentStateName => state.ToString();
    public bool IsShieldRaised => state == State.ShieldChase
        || state == State.Guard
        || state == State.ChargeWindup
        || state == State.Charging;

    public static string DescribeLatest()
    {
        if (lastTouched == null)
        {
            return "盾兵: 未激活";
        }

        return $"{lastTouched.name}: {lastTouched.state} {lastTouched.health:0}/{lastTouched.maxHealth:0}";
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        body.bodyType = RigidbodyType2D.Dynamic;
        body.freezeRotation = true;
        body.sleepMode = RigidbodySleepMode2D.NeverSleep;
        lastTouched = this;
        spawnPoint = transform.position;
        health = maxHealth;

        if (target == null)
        {
            PlayerController2D player = FindObjectOfType<PlayerController2D>();
            if (player != null)
            {
                target = player.transform;
                playerHealth = player.GetComponent<PlayerHealth>();
            }
        }
        else
        {
            playerHealth = target.GetComponent<PlayerHealth>();
        }

        BuildVisuals();
        UpdateHealthBar();
        SetShieldRaised(false);
        ChangeState(State.Patrol);
    }

    private void Update()
    {
        UpdateFlash();

        if (state == State.Dead)
        {
            return;
        }

        if (transform.position.y < spawnPoint.y - fallRecoveryDepth)
        {
            RecoverFromFall();
            return;
        }

        float dt = Time.deltaTime;
        stateTimer = Mathf.Max(0f, stateTimer - dt);
        chargeCooldownTimer = Mathf.Max(0f, chargeCooldownTimer - dt);
        hitStunTimer = Mathf.Max(0f, hitStunTimer - dt);

        UpdateShieldVisual(dt);
        EvaluateState();
    }

    private void FixedUpdate()
    {
        if (state == State.Dead || body == null)
        {
            return;
        }

        if (hitStunTimer > 0f)
        {
            StopHorizontal();
            return;
        }

        switch (state)
        {
            case State.Patrol:
                PatrolMovement();
                break;
            case State.ShieldChase:
                ChaseMovement();
                break;
            case State.Guard:
            case State.ChargeWindup:
            case State.Recover:
            case State.Stagger:
                StopHorizontal();
                break;
            case State.Charging:
                ChargeMovement();
                break;
        }
    }

    private void EvaluateState()
    {
        switch (state)
        {
            case State.Patrol:
                if (CanSeeTarget())
                {
                    ChangeState(State.ShieldChase);
                }
                break;

            case State.ShieldChase:
                if (!CanSeeTarget())
                {
                    ChangeState(State.Patrol);
                }
                else if (HorizontalDistanceToTarget() <= attackTriggerRange)
                {
                    if (chargeCooldownTimer <= 0f)
                    {
                        BeginChargeWindup();
                    }
                    else
                    {
                        ChangeState(State.Guard);
                    }
                }
                break;

            case State.Guard:
                if (!CanSeeTarget())
                {
                    ChangeState(State.Patrol);
                }
                else if (HorizontalDistanceToTarget() > attackTriggerRange * 1.1f)
                {
                    ChangeState(State.ShieldChase);
                }
                else if (chargeCooldownTimer <= 0f)
                {
                    BeginChargeWindup();
                }
                break;

            case State.ChargeWindup:
                FaceTarget();
                if (stateTimer <= 0f)
                {
                    BeginCharge();
                }
                break;

            case State.Charging:
                chargeElapsed += Time.deltaTime;
                if (stateTimer <= 0f)
                {
                    FinishCharge(false);
                }
                break;

            case State.Recover:
            case State.Stagger:
                if (stateTimer <= 0f)
                {
                    ChangeState(CanSeeTarget() ? State.ShieldChase : State.Patrol);
                }
                break;
        }
    }

    private void PatrolMovement()
    {
        if (WallAhead(patrolDirection) || !GroundAhead(patrolDirection))
        {
            patrolDirection *= -1;
        }

        float nextX = body.position.x + patrolDirection * patrolSpeed * Time.fixedDeltaTime;
        if (nextX < spawnPoint.x - patrolRange || nextX > spawnPoint.x + patrolRange)
        {
            patrolDirection *= -1;
        }

        SetFacing(patrolDirection);
        body.velocity = new Vector2(patrolDirection * patrolSpeed, body.velocity.y);
    }

    private void ChaseMovement()
    {
        int direction = DirectionToTarget();
        SetFacing(direction);

        if (!GroundAhead(direction))
        {
            StopHorizontal();
            return;
        }

        body.velocity = new Vector2(direction * chaseSpeed, body.velocity.y);
    }

    private void ChargeMovement()
    {
        if (stateTimer <= 0f)
        {
            FinishCharge(false);
            return;
        }

        if (WallAhead(facing))
        {
            ImpactFx.Spawn(transform.position + Vector3.up * 1f, new Color(0.8f, 0.86f, 1f, 0.9f), 0.8f);
            FinishCharge(true);
            return;
        }

        float traveled = Mathf.Abs(body.position.x - chargeStartPosition.x);
        if (traveled >= chargeMaxDistance)
        {
            FinishCharge(false);
            return;
        }

        if (chargeElapsed > 0.12f && Mathf.Abs(body.velocity.x) < chargeSpeed * 0.2f)
        {
            FinishCharge(true);
            return;
        }

        body.velocity = new Vector2(facing * chargeSpeed, body.velocity.y);
        TryHitPlayerDuringCharge();
    }

    private void TryHitPlayerDuringCharge()
    {
        if (chargeHitPlayer)
        {
            return;
        }

        Bounds bounds = bodyCollider.bounds;
        Vector2 center = new Vector2(
            bounds.center.x + facing * (bounds.extents.x + 0.45f),
            bounds.center.y);
        Collider2D[] hits = Physics2D.OverlapBoxAll(center, new Vector2(1.2f, bounds.size.y * 0.9f), 0f);

        foreach (Collider2D hit in hits)
        {
            if (hit == null)
            {
                continue;
            }

            PlayerHealth player = hit.GetComponentInParent<PlayerHealth>();
            if (player == null || player.IsDead)
            {
                continue;
            }

            player.TakeDamage(Mathf.RoundToInt(chargeDamage), new Vector2(facing * chargeKnockback, 5f));
            ImpactFx.Spawn(hit.bounds.center, new Color(1f, 0.72f, 0.2f, 1f), 1.1f);
            chargeHitPlayer = true;
            FinishCharge(false);
            return;
        }
    }

    private void BeginChargeWindup()
    {
        FaceTarget();
        StopHorizontal();
        ChangeState(State.ChargeWindup);
        stateTimer = windupDuration;
    }

    private void BeginCharge()
    {
        ChangeState(State.Charging);
        stateTimer = chargeMaxDuration;
        chargeElapsed = 0f;
        chargeHitPlayer = false;
        chargeStartPosition = body.position;
        body.velocity = new Vector2(facing * chargeSpeed, body.velocity.y);
    }

    private void FinishCharge(bool hitWall)
    {
        StopHorizontal();
        chargeCooldownTimer = chargeCooldown;

        if (hitWall)
        {
            ChangeState(State.Stagger);
            stateTimer = wallStaggerDuration;
            hitStunTimer = wallStaggerDuration;
        }
        else
        {
            ChangeState(State.Recover);
            stateTimer = recoverDuration;
        }
    }

    private void ChangeState(State nextState)
    {
        if (state == nextState)
        {
            return;
        }

        state = nextState;
        stateTimer = 0f;
        SetShieldRaised(IsShieldRaised);

        if (logStateChanges)
        {
            Debug.Log($"[ShieldSoldier] {name}: {state}", this);
        }
    }

    private void SetShieldRaised(bool raised)
    {
        if (shieldRenderer != null)
        {
            shieldRenderer.color = raised
                ? new Color(0.58f, 0.68f, 0.82f, 1f)
                : new Color(0.38f, 0.45f, 0.56f, 1f);
        }
    }

    private void UpdateShieldVisual(float dt)
    {
        if (shieldRoot == null)
        {
            return;
        }

        bool raised = IsShieldRaised;
        float targetX = facing * 0.62f;
        float targetY = raised ? 1.08f : 0.58f;
        float targetAngle = raised ? 0f : facing * 78f;
        float blend = 1f - Mathf.Exp(-16f * dt);

        shieldRoot.localPosition = Vector3.Lerp(
            shieldRoot.localPosition,
            new Vector3(targetX, targetY, 0f),
            blend);
        shieldRoot.localRotation = Quaternion.Lerp(
            shieldRoot.localRotation,
            Quaternion.Euler(0f, 0f, targetAngle),
            blend);
    }

    private bool CanSeeTarget()
    {
        if (target == null || (playerHealth != null && playerHealth.IsDead))
        {
            return false;
        }

        Vector2 targetPosition = target.position;
        Vector2 origin = bodyCollider != null ? (Vector2)bodyCollider.bounds.center : (Vector2)transform.position;
        return Mathf.Abs(targetPosition.x - origin.x) <= detectRange
            && Mathf.Abs(targetPosition.y - origin.y) <= verticalDetectTolerance;
    }

    private float HorizontalDistanceToTarget()
    {
        if (target == null)
        {
            return float.PositiveInfinity;
        }

        return Mathf.Abs(target.position.x - transform.position.x);
    }

    private int DirectionToTarget()
    {
        if (target == null)
        {
            return facing;
        }

        float delta = target.position.x - transform.position.x;
        return Mathf.Abs(delta) < 0.01f ? facing : (delta > 0f ? 1 : -1);
    }

    private void FaceTarget()
    {
        SetFacing(DirectionToTarget());
    }

    private void SetFacing(int direction)
    {
        if (direction != 0)
        {
            facing = direction > 0 ? 1 : -1;
        }
    }

    private bool GroundAhead(int direction)
    {
        if (bodyCollider == null)
        {
            return true;
        }

        Bounds bounds = bodyCollider.bounds;
        Vector2 origin = new Vector2(
            bounds.center.x + direction * (bounds.extents.x + ledgeCheckAhead),
            bounds.min.y + 0.05f);
        return Physics2D.Raycast(origin, Vector2.down, ledgeCheckDepth, groundMask).collider != null;
    }

    private bool WallAhead(int direction)
    {
        if (bodyCollider == null)
        {
            return false;
        }

        Bounds bounds = bodyCollider.bounds;
        Vector2 origin = new Vector2(bounds.center.x, bounds.center.y);
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, new Vector2(direction, 0f), wallCheckDistance, wallMask);

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null)
            {
                continue;
            }

            if (hit.collider.transform.IsChildOf(transform))
            {
                continue;
            }

            if (hit.collider.GetComponentInParent<PlayerHealth>() != null)
            {
                continue;
            }

            if (hit.collider.GetComponentInParent<ShieldSoldier>() != null)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private void RecoverFromFall()
    {
        body.velocity = Vector2.zero;
        body.position = spawnPoint;
        transform.position = spawnPoint;
        hitStunTimer = 0f;
        chargeCooldownTimer = 0f;
        ChangeState(State.Patrol);
    }

    private void StopHorizontal()
    {
        if (body != null)
        {
            body.velocity = new Vector2(0f, body.velocity.y);
        }
    }

    public void TakeDamage(float amount, Vector2 hitPoint, Vector2 hitDirection)
    {
        TakeDamage(amount, hitPoint, hitDirection, DamageType.Physical);
    }

    public void TakeDamage(float amount, Vector2 hitPoint, Vector2 hitDirection, DamageType damageType)
    {
        if (state == State.Dead || amount <= 0f)
        {
            return;
        }

        lastTouched = this;
        bool blocked = IsFrontalHit(hitPoint, hitDirection);
        float reduction = blocked
            ? (damageType == DamageType.Spell ? spellDamageReduction : physicalDamageReduction)
            : 0f;
        float appliedDamage = amount * (1f - Mathf.Clamp01(reduction));

        flashTimer = flashDuration;

        if (appliedDamage <= 0.01f)
        {
            ImpactFx.Spawn(hitPoint, new Color(0.65f, 0.85f, 1f, 1f), 0.8f);
            return;
        }

        health = Mathf.Max(0f, health - appliedDamage);
        UpdateHealthBar();
        ImpactFx.Spawn(hitPoint, blocked
            ? new Color(0.55f, 0.8f, 1f, 0.95f)
            : new Color(1f, 0.45f, 0.3f, 0.95f), 0.65f);

        if (state == State.Patrol && CanSeeTarget())
        {
            ChangeState(State.ShieldChase);
        }

        if (state != State.Charging && state != State.ChargeWindup && !blocked)
        {
            hitStunTimer = Mathf.Max(hitStunTimer, hitStunDuration);
        }

        if (health <= 0f)
        {
            Die();
        }
    }

    private bool IsFrontalHit(Vector2 hitPoint, Vector2 hitDirection)
    {
        if (!IsShieldRaised || bodyCollider == null)
        {
            return false;
        }

        Vector2 shieldCenter = bodyCollider.bounds.center;
        Vector2 toAttacker = hitPoint - shieldCenter;
        if (toAttacker.sqrMagnitude < 0.0001f)
        {
            toAttacker = -hitDirection;
        }

        if (toAttacker.sqrMagnitude < 0.0001f)
        {
            return true;
        }

        float minimumDot = Mathf.Cos(shieldReductionAngle * 0.5f * Mathf.Deg2Rad);
        return Vector2.Dot(new Vector2(facing, 0f), toAttacker.normalized) >= minimumDot;
    }

    private void Die()
    {
        ChangeState(State.Dead);
        health = 0f;
        UpdateHealthBar();

        if (bodyCollider != null)
        {
            bodyCollider.enabled = false;
        }

        if (body != null)
        {
            body.velocity = Vector2.zero;
            body.simulated = false;
        }

        if (healthBarRoot != null)
        {
            healthBarRoot.gameObject.SetActive(false);
        }

        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        float duration = 0.8f;
        float elapsed = 0f;
        Vector3 shieldStart = shieldRoot != null ? shieldRoot.localPosition : Vector3.zero;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (shieldRoot != null)
            {
                shieldRoot.localPosition = shieldStart + new Vector3(facing * 0.55f * t, -0.65f * t, 0f);
                shieldRoot.localRotation = Quaternion.Euler(0f, 0f, facing * 82f * t);
            }

            for (int i = 0; i < bodyParts.Length; i++)
            {
                Color color = bodyPartColors[i];
                color.a = 1f - t;
                bodyParts[i].color = color;
            }

            if (shieldRenderer != null)
            {
                Color color = shieldRenderer.color;
                color.a = 1f - t;
                shieldRenderer.color = color;
            }

            yield return null;
        }

        Destroy(gameObject);
    }

    private void UpdateFlash()
    {
        if (bodyRenderer == null || bodyParts == null)
        {
            return;
        }

        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            float t = Mathf.Clamp01(flashTimer / flashDuration);
            for (int i = 0; i < bodyParts.Length; i++)
            {
                bodyParts[i].color = Color.Lerp(bodyPartColors[i], flashColor, t * 0.8f);
            }

            bodyRenderer.transform.localScale = bodyBaseScale * (1f + 0.06f * t);
        }
        else
        {
            for (int i = 0; i < bodyParts.Length; i++)
            {
                bodyParts[i].color = bodyPartColors[i];
            }

            bodyRenderer.transform.localScale = bodyBaseScale;
        }
    }

    private void UpdateHealthBar()
    {
        if (healthBarFill == null)
        {
            return;
        }

        float ratio = maxHealth > 0f ? Mathf.Clamp01(health / maxHealth) : 0f;
        healthBarFill.localScale = new Vector3(barWidth * ratio, 0.09f, 1f);
        healthBarFill.localPosition = new Vector3(-barWidth * (1f - ratio) * 0.5f, 0f, 0f);
    }

    private void BuildVisuals()
    {
        if (bodyRenderer != null)
        {
            bodyBaseColor = bodyRenderer.color;
            bodyBaseScale = bodyRenderer.transform.localScale;
            bodyParts = new[] { bodyRenderer };
            bodyPartColors = new[] { bodyBaseColor };
            return;
        }

        bodyRenderer = CreatePart("Body", transform, new Vector3(0f, 1.02f, 0f), new Vector3(0.82f, 1.5f, 1f), new Color(0.27f, 0.36f, 0.49f, 1f), 14);
        CreatePart("Head", transform, new Vector3(0f, 1.9f, 0f), new Vector3(0.46f, 0.44f, 1f), new Color(0.82f, 0.65f, 0.49f, 1f), 15);
        CreatePart("Helmet", transform, new Vector3(0f, 2.1f, 0f), new Vector3(0.58f, 0.2f, 1f), new Color(0.16f, 0.22f, 0.3f, 1f), 16);
        CreatePart("Visor", transform, new Vector3(facing * 0.1f, 1.92f, 0f), new Vector3(0.38f, 0.1f, 1f), new Color(0.08f, 0.11f, 0.15f, 1f), 17);
        CreatePart("Belt", transform, new Vector3(0f, 0.75f, 0f), new Vector3(0.88f, 0.12f, 1f), new Color(0.1f, 0.12f, 0.16f, 1f), 16);

        GameObject shieldObject = new GameObject("Shield");
        shieldRoot = shieldObject.transform;
        shieldRoot.SetParent(transform, false);
        shieldRoot.localPosition = new Vector3(facing * 0.62f, 1.08f, 0f);

        shieldRenderer = CreatePart("ShieldFace", shieldRoot, Vector3.zero, new Vector3(0.18f, 1.55f, 1f), new Color(0.58f, 0.68f, 0.82f, 1f), 18);
        CreatePart("ShieldBoss", shieldRoot, Vector3.zero, new Vector3(0.3f, 0.24f, 1f), new Color(0.18f, 0.23f, 0.31f, 1f), 19);

        GameObject barObject = new GameObject("HealthBar");
        healthBarRoot = barObject.transform;
        healthBarRoot.SetParent(transform, false);
        healthBarRoot.localPosition = new Vector3(0f, 2.55f, 0f);
        CreatePart("Back", healthBarRoot, Vector3.zero, new Vector3(1.5f, 0.16f, 1f), new Color(0.08f, 0.08f, 0.09f, 0.9f), 30);
        SpriteRenderer fill = CreatePart("Fill", healthBarRoot, Vector3.zero, new Vector3(barWidth, 0.09f, 1f), new Color(0.86f, 0.22f, 0.18f, 1f), 31);
        healthBarFill = fill.transform;

        bodyBaseColor = bodyRenderer.color;
        bodyBaseScale = bodyRenderer.transform.localScale;
        bodyParts = GetComponentsInChildren<SpriteRenderer>();
        List<SpriteRenderer> filtered = new List<SpriteRenderer>();
        foreach (SpriteRenderer part in bodyParts)
        {
            if (part != shieldRenderer && !part.transform.IsChildOf(healthBarRoot))
            {
                filtered.Add(part);
            }
        }

        bodyParts = filtered.ToArray();
        bodyPartColors = new Color[bodyParts.Length];
        for (int i = 0; i < bodyParts.Length; i++)
        {
            bodyPartColors[i] = bodyParts[i].color;
        }
    }

    private static SpriteRenderer CreatePart(string partName, Transform parent, Vector3 localPosition, Vector3 localScale, Color color, int sortingOrder)
    {
        GameObject part = new GameObject(partName);
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;

        SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
        renderer.sprite = GetWhiteSprite();
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    private static Sprite GetWhiteSprite()
    {
        if (whiteSprite == null)
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            whiteSprite.name = "ShieldSoldierWhite";
        }

        return whiteSprite;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.75f);
        Gizmos.DrawWireSphere(transform.position, detectRange);
        Gizmos.color = new Color(1f, 0.35f, 0.2f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, attackTriggerRange);

        Gizmos.color = new Color(0.5f, 0.8f, 1f, 0.9f);
        Vector3 start = transform.position + Vector3.up * 0.15f;
        Gizmos.DrawLine(start, start + Vector3.right * facing * chargeMaxDistance);
    }
}
