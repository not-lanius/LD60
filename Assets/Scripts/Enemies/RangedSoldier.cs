using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class RangedSoldier : MonoBehaviour, IDamageable
{
    public enum State
    {
        Patrol,
        Retreat,
        Aim,
        TurnFollow,
        Recover,
        Dead
    }

    [Header("Perception")]
    [SerializeField] private Transform target;
    [SerializeField] private float detectRange = 16f;
    [SerializeField] private float verticalDetectTolerance = 3f;
    [SerializeField] private bool rockWallsDoNotBlockAim = true;

    [Header("Patrol")]
    [SerializeField] private float patrolSpeed = 12f;
    [SerializeField] private float patrolRange = 2f;

    [Header("Retreat")]
    [SerializeField] private float retreatTriggerRange = 4f;
    [SerializeField] private float retreatDistance = 6f;
    [SerializeField] private float retreatSpeed = 20f;
    [SerializeField] private float retreatCooldown = 1.5f;

    [Header("Shooting")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float aimWindup = 0.8f;
    [SerializeField] private float aimTrackingSplit = 0.7f;
    [SerializeField] private float bulletSpeed = 25f;
    [SerializeField] private float maxFireRange = 16f;
    [SerializeField] private float recoverDuration = 0.8f;
    [SerializeField] private float fireCooldown = 1f;
    [SerializeField] private int bulletDamage = 15;
    [SerializeField] private float bulletKnockback = 6f;
    [SerializeField] private float muzzleHeight = 1.25f;
    [SerializeField] private float muzzleForward = 0.85f;
    [SerializeField] private float shotCheckRadius = 0.08f;

    [Header("Defense")]
    [SerializeField] private float maxHealth = 30f;
    [SerializeField] private int defense = 10;
    [SerializeField] private float hitStunDuration = 0.1f;

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundMask = 1 << 6;
    [SerializeField] private LayerMask wallMask = (1 << 6) | 1;
    [SerializeField] private LayerMask sightMask = (1 << 6) | 1;
    [SerializeField] private float wallCheckDistance = 0.55f;
    [SerializeField] private float ledgeCheckAhead = 0.55f;
    [SerializeField] private float ledgeCheckDepth = 0.9f;
    [SerializeField] private float fallRecoveryDepth = 5f;

    [Header("Visuals")]
    [SerializeField] private Sprite bodySprite;
    [SerializeField] private float bodySpriteScale = 0.52f;
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private Transform weaponRoot;
    [SerializeField] private Transform healthBarRoot;
    [SerializeField] private Transform healthBarFill;
    [SerializeField] private float barWidth = 1.3f;
    [SerializeField] private Color flashColor = Color.white;
    [SerializeField] private float flashDuration = 0.1f;
    [SerializeField] private bool logStateChanges;

    private static Sprite whiteSprite;
    private static Material aimLineMaterial;
    private static RangedSoldier lastTouched;

    private Rigidbody2D body;
    private Collider2D bodyCollider;
    private PlayerHealth playerHealth;
    private SpriteRenderer[] bodyParts;
    private Color[] bodyPartColors;
    private Color bodyBaseColor;
    private Vector3 bodyBaseScale;
    private LineRenderer aimLine;

    private State state;
    private float stateTimer;
    private float shotCooldownTimer;
    private float retreatCooldownTimer;
    private float hitStunTimer;
    private float flashTimer;
    private float health;
    private Vector2 spawnPoint;
    private Vector2 retreatStartPosition;
    private int facing = -1;
    private int patrolDirection = 1;
    private int retreatDirection = 1;

    public bool IsDead => state == State.Dead;
    public float CurrentHealth => health;
    public float MaxHealth => maxHealth;
    public string CurrentStateName => state.ToString();

    public static string DescribeLatest()
    {
        if (lastTouched == null)
        {
            return "远程兵: 未激活";
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
        shotCooldownTimer = Mathf.Max(0f, shotCooldownTimer - dt);
        retreatCooldownTimer = Mathf.Max(0f, retreatCooldownTimer - dt);
        hitStunTimer = Mathf.Max(0f, hitStunTimer - dt);

        UpdateAimLine();
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
            case State.Retreat:
                RetreatMovement();
                break;
            case State.Aim:
            case State.TurnFollow:
            case State.Recover:
                StopHorizontal();
                break;
        }
    }

    private void EvaluateState()
    {
        switch (state)
        {
            case State.Patrol:
                EvaluatePatrol();
                break;

            case State.Retreat:
                if (stateTimer <= 0f
                    || Mathf.Abs(transform.position.x - retreatStartPosition.x) >= retreatDistance)
                {
                    EndRetreat();
                }
                break;

            case State.Aim:
                if (!CanSeeTarget())
                {
                    ChangeState(State.Patrol);
                }
                else if (HorizontalDistanceToTarget() < retreatTriggerRange
                    && retreatCooldownTimer <= 0f)
                {
                    BeginRetreat();
                }
                else
                {
                    FaceTarget();
                    if (stateTimer <= 0f)
                    {
                        ChangeState(State.TurnFollow);
                        stateTimer = aimWindup * (1f - aimTrackingSplit);
                    }
                }
                break;

            case State.TurnFollow:
                if (!CanSeeTarget())
                {
                    ChangeState(State.Patrol);
                }
                else
                {
                    FaceTarget();
                    if (stateTimer <= 0f)
                    {
                        FireOnce();
                        ChangeState(State.Recover);
                        stateTimer = recoverDuration;
                    }
                }
                break;

            case State.Recover:
                if (stateTimer <= 0f)
                {
                    ChangeState(State.Patrol);
                }
                break;
        }
    }

    private void EvaluatePatrol()
    {
        if (!CanSeeTarget())
        {
            return;
        }

        float distance = HorizontalDistanceToTarget();
        if (distance < retreatTriggerRange && retreatCooldownTimer <= 0f)
        {
            BeginRetreat();
        }
        else if (distance >= retreatTriggerRange && shotCooldownTimer <= 0f)
        {
            BeginAim();
        }
    }

    private void BeginAim()
    {
        PlayerMana.ReportCombatActivity();
        FaceTarget();
        StopHorizontal();
        ChangeState(State.Aim);
        stateTimer = aimWindup * aimTrackingSplit;

        if (AudioDirector.Instance != null)
        {
            AudioDirector.Instance.PlayEnemyWindup();
        }
    }

    private void BeginRetreat()
    {
        retreatDirection = target != null && target.position.x >= transform.position.x ? -1 : 1;
        retreatStartPosition = transform.position;
        retreatCooldownTimer = retreatCooldown;
        FaceTarget();
        StopHorizontal();
        ChangeState(State.Retreat);
        stateTimer = retreatDistance / Mathf.Max(0.1f, retreatSpeed);
    }

    private void EndRetreat()
    {
        StopHorizontal();
        if (CanSeeTarget() && shotCooldownTimer <= 0f)
        {
            BeginAim();
        }
        else
        {
            ChangeState(State.Patrol);
        }
    }

    private void FireOnce()
    {
        if (bulletPrefab == null || target == null || shotCooldownTimer > 0f)
        {
            return;
        }

        Vector2 muzzle = GetMuzzlePosition();
        if (!TryGetClearShot(out _, out Vector2 direction, out _))
        {
            return;
        }

        GameObject bulletObject = Instantiate(bulletPrefab, muzzle, Quaternion.identity);
        EnemyBullet bullet = bulletObject.GetComponent<EnemyBullet>();
        if (bullet != null)
        {
            bullet.Configure(bulletSpeed, bulletDamage, bulletKnockback, maxFireRange);
            bullet.Launch(direction);
        }

        Collider2D bulletCollider = bulletObject.GetComponent<Collider2D>();
        Collider2D selfCollider = GetComponent<Collider2D>();
        if (bulletCollider != null && selfCollider != null)
        {
            Physics2D.IgnoreCollision(bulletCollider, selfCollider, true);
        }

        shotCooldownTimer = fireCooldown;
        ImpactFx.Spawn(muzzle, new Color(1f, 0.72f, 0.25f, 1f), 0.55f);
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

    private void RetreatMovement()
    {
        if (WallAhead(retreatDirection) || !GroundAhead(retreatDirection))
        {
            EndRetreat();
            return;
        }

        FaceTarget();
        body.velocity = new Vector2(retreatDirection * retreatSpeed, body.velocity.y);
    }

    private bool CanSeeTarget()
    {
        if (target == null || (playerHealth != null && playerHealth.IsDead))
        {
            return false;
        }

        Vector2 targetPosition = target.position;
        Vector2 origin = bodyCollider != null ? (Vector2)bodyCollider.bounds.center : (Vector2)transform.position;
        if (Mathf.Abs(targetPosition.x - origin.x) > detectRange
            || Mathf.Abs(targetPosition.y - origin.y) > verticalDetectTolerance)
        {
            return false;
        }

        return HasLineOfSight();
    }

    private bool HasLineOfSight()
    {
        Vector2 aimPoint;
        Vector2 direction;
        float distance;
        return TryGetClearShot(out aimPoint, out direction, out distance);
    }

    private bool TryGetClearShot(out Vector2 aimPoint, out Vector2 direction, out float distance)
    {
        aimPoint = GetAimPoint();
        Vector2 muzzle = GetMuzzlePosition();
        direction = aimPoint - muzzle;
        distance = direction.magnitude;

        if (distance <= 0.05f || distance > maxFireRange)
        {
            return false;
        }

        direction /= distance;
        Vector2 castSize = new Vector2(shotCheckRadius * 2f, shotCheckRadius * 2f);
        RaycastHit2D[] hits = Physics2D.BoxCastAll(muzzle, castSize, 0f, direction, distance, sightMask);
        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null || hit.collider.isTrigger || hit.collider.transform.IsChildOf(transform))
            {
                continue;
            }

            if (hit.collider.GetComponentInParent<PlayerHealth>() != null)
            {
                continue;
            }

            if (rockWallsDoNotBlockAim && hit.collider.GetComponentInParent<RockWall>() != null)
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private Vector2 GetAimPoint()
    {
        if (target == null)
        {
            return transform.position;
        }

        Collider2D targetCollider = target.GetComponentInChildren<Collider2D>();
        return targetCollider != null ? (Vector2)targetCollider.bounds.center : (Vector2)target.position;
    }

    private Vector2 GetMuzzlePosition()
    {
        return (Vector2)transform.position + new Vector2(facing * muzzleForward, muzzleHeight);
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

        if (weaponRoot != null)
        {
            Vector3 position = weaponRoot.localPosition;
            position.x = Mathf.Abs(position.x) * facing;
            weaponRoot.localPosition = position;
            weaponRoot.localScale = new Vector3(Mathf.Abs(weaponRoot.localScale.x) * facing, weaponRoot.localScale.y, 1f);
        }

        if (bodyRenderer != null)
        {
            bodyRenderer.flipX = facing > 0;
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
            if (hit.collider == null || hit.collider.isTrigger || hit.collider.transform.IsChildOf(transform))
            {
                continue;
            }

            if (hit.collider.GetComponentInParent<PlayerHealth>() != null)
            {
                continue;
            }

            if (hit.collider.GetComponentInParent<RangedSoldier>() != null
                || hit.collider.GetComponentInParent<ShieldSoldier>() != null)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private void StopHorizontal()
    {
        if (body != null)
        {
            body.velocity = new Vector2(0f, body.velocity.y);
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

        if (logStateChanges)
        {
            Debug.Log($"[RangedSoldier] {name}: {state}", this);
        }
    }

    public void TakeDamage(float amount, Vector2 hitPoint, Vector2 hitDirection)
    {
        if (state == State.Dead || amount <= 0f)
        {
            return;
        }

        lastTouched = this;
        float appliedDamage = Mathf.Max(0f, amount - defense);
        if (appliedDamage <= 0.01f)
        {
            ImpactFx.Spawn(hitPoint, new Color(0.65f, 0.85f, 1f, 0.8f), 0.5f);
            return;
        }

        health = Mathf.Max(0f, health - appliedDamage);
        flashTimer = flashDuration;
        UpdateHealthBar();

        if (AudioDirector.Instance != null)
        {
            AudioDirector.Instance.PlayEnemyHurt();
        }

        ImpactFx.Spawn(hitPoint, new Color(1f, 0.45f, 0.3f, 0.95f), 0.65f);

        if (state == State.Patrol && CanSeeTarget())
        {
            BeginAim();
        }
        else if (state != State.Retreat)
        {
            hitStunTimer = Mathf.Max(hitStunTimer, hitStunDuration);
        }

        if (health <= 0f)
        {
            Die();
        }
    }

    private void Die()
    {
        ChangeState(State.Dead);
        health = 0f;
        UpdateHealthBar();
        SetAimLineVisible(false);

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
        float duration = 0.7f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            for (int i = 0; i < bodyParts.Length; i++)
            {
                Color color = bodyPartColors[i];
                color.a = 1f - t;
                bodyParts[i].color = color;
            }

            if (bodyRenderer != null)
            {
                bodyRenderer.transform.localScale = bodyBaseScale * (1f - 0.25f * t);
                bodyRenderer.transform.Rotate(0f, 0f, facing * 90f * Time.deltaTime);
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
        }
        else
        {
            for (int i = 0; i < bodyParts.Length; i++)
            {
                bodyParts[i].color = bodyPartColors[i];
            }
        }
    }

    private void UpdateAimLine()
    {
        bool aiming = state == State.Aim || state == State.TurnFollow;
        SetAimLineVisible(aiming);
        if (!aiming || aimLine == null)
        {
            return;
        }

        Vector2 muzzle = GetMuzzlePosition();
        Vector2 aimPoint = GetAimPoint();
        Vector2 lineEnd = GetAimLineEnd(muzzle, aimPoint);
        aimLine.SetPosition(0, new Vector3(muzzle.x, muzzle.y, 0f));
        aimLine.SetPosition(1, new Vector3(lineEnd.x, lineEnd.y, 0f));

        float progress = state == State.Aim
            ? 1f - Mathf.Clamp01(stateTimer / Mathf.Max(0.01f, aimWindup * aimTrackingSplit))
            : 1f - Mathf.Clamp01(stateTimer / Mathf.Max(0.01f, aimWindup * (1f - aimTrackingSplit)));
        Color color = Color.Lerp(new Color(1f, 0.9f, 0.35f, 0.25f), new Color(1f, 0.2f, 0.12f, 0.9f), progress);
        aimLine.startColor = color;
        aimLine.endColor = color;
    }

    private Vector2 GetAimLineEnd(Vector2 muzzle, Vector2 aimPoint)
    {
        Vector2 delta = aimPoint - muzzle;
        float distance = delta.magnitude;
        if (distance <= 0.05f)
        {
            return aimPoint;
        }

        Vector2 direction = delta / distance;
        RaycastHit2D[] hits = Physics2D.BoxCastAll(
            muzzle,
            new Vector2(shotCheckRadius * 2f, shotCheckRadius * 2f),
            0f,
            direction,
            distance,
            sightMask);

        float nearestDistance = distance;
        Vector2 lineEnd = aimPoint;
        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null || hit.collider.isTrigger || hit.collider.transform.IsChildOf(transform))
            {
                continue;
            }

            if (hit.collider.GetComponentInParent<PlayerHealth>() != null)
            {
                continue;
            }

            if (rockWallsDoNotBlockAim && hit.collider.GetComponentInParent<RockWall>() != null)
            {
                continue;
            }

            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
                lineEnd = hit.point;
            }
        }

        return lineEnd;
    }

    private void SetAimLineVisible(bool visible)
    {
        if (aimLine != null && aimLine.enabled != visible)
        {
            aimLine.enabled = visible;
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

    private void RecoverFromFall()
    {
        body.velocity = Vector2.zero;
        body.position = spawnPoint;
        transform.position = spawnPoint;
        hitStunTimer = 0f;
        shotCooldownTimer = 0f;
        retreatCooldownTimer = 0f;
        ChangeState(State.Patrol);
    }

    private void BuildVisuals()
    {
        if (bodyRenderer != null)
        {
            bodyBaseColor = bodyRenderer.color;
            bodyBaseScale = bodyRenderer.transform.localScale;
            bodyParts = new[] { bodyRenderer };
            bodyPartColors = new[] { bodyBaseColor };
            SetFacing(facing);
            return;
        }

        float barY = 2.55f;

        if (bodySprite != null)
        {
            bodyRenderer = CreatePart("Body", transform, new Vector3(0f, 1.02f, 0f), Vector3.one * bodySpriteScale, Color.white, 14, bodySprite);
            barY = 1.02f + bodySprite.bounds.size.y * bodySpriteScale * 0.5f + 0.22f;
        }
        else
        {
            bodyRenderer = CreatePart("Body", transform, new Vector3(0f, 1.02f, 0f), new Vector3(0.76f, 1.5f, 1f), new Color(0.25f, 0.34f, 0.24f, 1f), 14);
            CreatePart("Head", transform, new Vector3(0f, 1.9f, 0f), new Vector3(0.44f, 0.44f, 1f), new Color(0.82f, 0.65f, 0.49f, 1f), 15);
            CreatePart("Helmet", transform, new Vector3(0f, 2.1f, 0f), new Vector3(0.56f, 0.2f, 1f), new Color(0.12f, 0.18f, 0.12f, 1f), 16);
            CreatePart("Visor", transform, new Vector3(facing * 0.1f, 1.92f, 0f), new Vector3(0.36f, 0.1f, 1f), new Color(0.05f, 0.08f, 0.05f, 1f), 17);
            CreatePart("Vest", transform, new Vector3(0f, 1.12f, 0f), new Vector3(0.86f, 0.58f, 1f), new Color(0.16f, 0.22f, 0.15f, 1f), 16);

            GameObject weaponObject = new GameObject("Weapon");
            weaponRoot = weaponObject.transform;
            weaponRoot.SetParent(transform, false);
            weaponRoot.localPosition = new Vector3(0.58f, 1.12f, 0f);
            CreatePart("Rifle", weaponRoot, Vector3.zero, new Vector3(1.15f, 0.12f, 1f), new Color(0.1f, 0.11f, 0.1f, 1f), 18);
            CreatePart("Muzzle", weaponRoot, new Vector3(0.58f, 0f, 0f), new Vector3(0.12f, 0.18f, 1f), new Color(0.35f, 0.32f, 0.22f, 1f), 19);
        }

        GameObject barObject = new GameObject("HealthBar");
        healthBarRoot = barObject.transform;
        healthBarRoot.SetParent(transform, false);
        healthBarRoot.localPosition = new Vector3(0f, barY, 0f);
        CreatePart("Back", healthBarRoot, Vector3.zero, new Vector3(1.5f, 0.16f, 1f), new Color(0.08f, 0.08f, 0.09f, 0.9f), 30);
        SpriteRenderer fill = CreatePart("Fill", healthBarRoot, Vector3.zero, new Vector3(barWidth, 0.09f, 1f), new Color(0.86f, 0.22f, 0.18f, 1f), 31);
        healthBarFill = fill.transform;

        bodyBaseColor = bodyRenderer.color;
        bodyBaseScale = bodyRenderer.transform.localScale;
        SpriteRenderer[] allParts = GetComponentsInChildren<SpriteRenderer>();
        List<SpriteRenderer> filtered = new List<SpriteRenderer>();
        foreach (SpriteRenderer part in allParts)
        {
            if (!part.transform.IsChildOf(healthBarRoot))
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

        GameObject aimObject = new GameObject("AimLine");
        aimObject.transform.SetParent(transform, false);
        aimLine = aimObject.AddComponent<LineRenderer>();
        aimLine.useWorldSpace = true;
        aimLine.positionCount = 2;
        aimLine.startWidth = 0.035f;
        aimLine.endWidth = 0.025f;
        aimLine.numCapVertices = 2;
        aimLine.material = GetAimLineMaterial();
        aimLine.sortingOrder = 28;
        aimLine.enabled = false;

        SetFacing(facing);
    }

    private static SpriteRenderer CreatePart(string partName, Transform parent, Vector3 localPosition, Vector3 localScale, Color color, int sortingOrder, Sprite sprite = null)
    {
        GameObject part = new GameObject(partName);
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;

        SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite != null ? sprite : GetWhiteSprite();
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
            whiteSprite.name = "RangedSoldierWhite";
        }

        return whiteSprite;
    }

    private static Material GetAimLineMaterial()
    {
        if (aimLineMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            }

            if (shader != null)
            {
                aimLineMaterial = new Material(shader)
                {
                    name = "RangedAimLineMaterial"
                };
            }
        }

        return aimLineMaterial;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.75f);
        Gizmos.DrawWireSphere(transform.position, detectRange);
        Gizmos.color = new Color(1f, 0.35f, 0.2f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, retreatTriggerRange);
    }
}
