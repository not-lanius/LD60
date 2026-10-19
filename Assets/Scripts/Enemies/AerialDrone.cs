using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class AerialDrone : MonoBehaviour, IDamageable
{
    public enum State
    {
        Sleep,
        Chase,
        HoverCharge,
        MeleeAttack,
        Recover,
        Cooldown,
        Dead
    }

    [Header("Perception")]
    [SerializeField] private Transform target;
    [SerializeField] private float detectRange = 10f;

    [Header("Movement")]
    [SerializeField] private float chaseSpeed = 17f;
    [SerializeField] private float hoverBobHeight = 0.08f;
    [SerializeField] private float hoverBobSpeed = 3f;

    [Header("Attack")]
    [SerializeField] private float attackTriggerRadius = 2.5f;
    [SerializeField] private float windupDuration = 1.4f;
    [SerializeField] private float attackRange = 4f;
    [SerializeField] private float attackSpeed = 18f;
    [SerializeField] private float attackHitRadius = 0.65f;
    [SerializeField] private float recoverDuration = 1f;
    [SerializeField] private float cooldownDuration = 0.5f;
    [SerializeField] private int attackDamage = 20;
    [SerializeField] private float attackKnockback = 8f;

    [Header("Defense")]
    [SerializeField] private float maxHealth = 10f;
    [SerializeField] private int defense = 10;

    [Header("Visuals")]
    [SerializeField] private Sprite bodySprite;
    [SerializeField] private float bodySpriteScale = 0.26f;
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Transform rotorLeft;
    [SerializeField] private Transform rotorRight;
    [SerializeField] private Transform meleeBlades;
    [SerializeField] private Transform healthBarRoot;
    [SerializeField] private Transform healthBarFill;
    [SerializeField] private float barWidth = 1.3f;
    [SerializeField] private Color flashColor = Color.white;
    [SerializeField] private float flashDuration = 0.1f;
    [SerializeField] private bool logStateChanges;

    private static Sprite whiteSprite;
    private static Material lineMaterial;
    private static AerialDrone lastTouched;

    private Rigidbody2D body;
    private Collider2D bodyCollider;
    private PlayerHealth playerHealth;
    private SpriteRenderer[] bodyParts;
    private Color[] bodyPartColors;
    private Color bodyBaseColor;
    private Vector3 visualBasePosition;
    private LineRenderer telegraphLine;

    private State state;
    private float stateTimer;
    private float flashTimer;
    private float health;
    private float bobTime;
    private float attackTraveled;
    private Vector2 spawnPoint;
    private Vector2 attackStartPosition;
    private Vector2 attackDirection;
    private bool attackHitPlayer;

    public bool IsDead => state == State.Dead;
    public float CurrentHealth => health;
    public float MaxHealth => maxHealth;
    public string CurrentStateName => state.ToString();

    public static string DescribeLatest()
    {
        if (lastTouched == null)
        {
            return "无人机: 未激活";
        }

        return $"{lastTouched.name}: {lastTouched.state} {lastTouched.health:0}/{lastTouched.maxHealth:0}";
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.sleepMode = RigidbodySleepMode2D.NeverSleep;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
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
        ChangeState(State.Sleep);
    }

    private void Update()
    {
        bobTime += Time.deltaTime;
        UpdateVisuals();
        UpdateFlash();
        UpdateTelegraph();

        if (state == State.Dead)
        {
            return;
        }

        stateTimer = Mathf.Max(0f, stateTimer - Time.deltaTime);
        EvaluateState();
    }

    private void FixedUpdate()
    {
        if (state == State.Dead || body == null)
        {
            return;
        }

        switch (state)
        {
            case State.Chase:
                ChaseMovement();
                break;
            case State.MeleeAttack:
                AttackMovement();
                break;
            case State.Sleep:
            case State.HoverCharge:
            case State.Recover:
            case State.Cooldown:
                StopMovement();
                break;
        }
    }

    private void EvaluateState()
    {
        switch (state)
        {
            case State.Sleep:
                if (CanSeeTarget())
                {
                    ChangeState(State.Chase);
                }
                break;

            case State.Chase:
                if (!CanSeeTarget())
                {
                    ChangeState(State.Sleep);
                }
                else if (Vector2.Distance(transform.position, GetAimPoint()) <= attackTriggerRadius)
                {
                    BeginHoverCharge();
                }
                break;

            case State.HoverCharge:
                if (!CanSeeTarget())
                {
                    ChangeState(State.Sleep);
                }
                else if (stateTimer <= 0f)
                {
                    BeginAttack();
                }
                break;

            case State.MeleeAttack:
                if (stateTimer <= 0f || attackTraveled >= attackRange)
                {
                    EndAttack();
                }
                break;

            case State.Recover:
                if (stateTimer <= 0f)
                {
                    ChangeState(State.Cooldown);
                    stateTimer = cooldownDuration;
                }
                break;

            case State.Cooldown:
                if (stateTimer <= 0f)
                {
                    ChangeState(CanSeeTarget() ? State.Chase : State.Sleep);
                }
                break;
        }
    }

    private void ChaseMovement()
    {
        if (target == null)
        {
            StopMovement();
            return;
        }

        Vector2 delta = GetAimPoint() - body.position;
        if (delta.sqrMagnitude <= attackTriggerRadius * attackTriggerRadius)
        {
            StopMovement();
            return;
        }

        Vector2 direction = delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector2.down;
        body.velocity = direction * chaseSpeed;
    }

    private void AttackMovement()
    {
        body.velocity = attackDirection * attackSpeed;
        attackTraveled = Vector2.Distance(body.position, attackStartPosition);

        if (TryHitPlayer())
        {
            EndAttack();
        }
    }

    private bool TryHitPlayer()
    {
        if (attackHitPlayer || attackDirection.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        float stepDistance = attackSpeed * Time.fixedDeltaTime;
        RaycastHit2D[] hits = Physics2D.CircleCastAll(
            body.position,
            attackHitRadius,
            attackDirection,
            stepDistance);

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null)
            {
                continue;
            }

            PlayerHealth player = hit.collider.GetComponentInParent<PlayerHealth>();
            if (player == null || player.IsDead)
            {
                continue;
            }

            player.TakeDamage(attackDamage, attackDirection * attackKnockback + Vector2.up * 2f);
            ImpactFx.Spawn(hit.collider.bounds.center, new Color(1f, 0.45f, 0.2f, 1f), 1.1f);
            attackHitPlayer = true;
            return true;
        }

        return false;
    }

    private void BeginHoverCharge()
    {
        PlayerMana.ReportCombatActivity();
        StopMovement();
        ChangeState(State.HoverCharge);
        stateTimer = windupDuration;

        if (AudioDirector.Instance != null)
        {
            AudioDirector.Instance.PlayEnemyWindup();
        }
    }

    private void BeginAttack()
    {
        Vector2 aimPoint = GetAimPoint();
        Vector2 delta = aimPoint - (Vector2)transform.position;
        attackDirection = delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector2.down;
        attackStartPosition = transform.position;
        attackTraveled = 0f;
        attackHitPlayer = false;
        ChangeState(State.MeleeAttack);
        stateTimer = attackRange / Mathf.Max(0.1f, attackSpeed) + 0.2f;
    }

    private void EndAttack()
    {
        StopMovement();
        ChangeState(State.Recover);
        stateTimer = recoverDuration;
    }

    private bool CanSeeTarget()
    {
        if (target == null || (playerHealth != null && playerHealth.IsDead))
        {
            return false;
        }

        return Vector2.Distance(transform.position, GetAimPoint()) <= detectRange;
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

    private void StopMovement()
    {
        if (body != null)
        {
            body.velocity = Vector2.zero;
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
            Debug.Log($"[AerialDrone] {name}: {state}", this);
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

        ImpactFx.Spawn(hitPoint, new Color(1f, 0.55f, 0.25f, 0.95f), 0.6f);

        if (state == State.Sleep && CanSeeTarget())
        {
            ChangeState(State.Chase);
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
        StopMovement();

        if (bodyCollider != null)
        {
            bodyCollider.enabled = false;
        }

        if (body != null)
        {
            body.simulated = false;
        }

        if (healthBarRoot != null)
        {
            healthBarRoot.gameObject.SetActive(false);
        }

        if (telegraphLine != null)
        {
            telegraphLine.enabled = false;
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

            if (visualRoot != null)
            {
                visualRoot.localPosition = visualBasePosition + new Vector3(0f, -1.2f * t, 0f);
                visualRoot.Rotate(0f, 0f, 220f * Time.deltaTime);
            }

            yield return null;
        }

        Destroy(gameObject);
    }

    private void UpdateVisuals()
    {
        if (visualRoot != null)
        {
            float bob = state == State.Dead
                ? 0f
                : Mathf.Sin(bobTime * hoverBobSpeed) * hoverBobHeight;
            visualRoot.localPosition = visualBasePosition + Vector3.up * bob;

            if (state == State.HoverCharge)
            {
                float pulse = 1f + Mathf.Sin(bobTime * 18f) * 0.05f;
                visualRoot.localScale = Vector3.one * pulse;
            }
            else if (state != State.Dead)
            {
                visualRoot.localScale = Vector3.one;
            }
        }

        float spin = 1080f * Time.deltaTime;
        if (rotorLeft != null)
        {
            rotorLeft.Rotate(0f, 0f, spin);
        }

        if (rotorRight != null)
        {
            rotorRight.Rotate(0f, 0f, -spin);
        }

        if (meleeBlades != null)
        {
            float bladeSpeed = state == State.HoverCharge || state == State.MeleeAttack ? 1440f : 90f;
            meleeBlades.Rotate(0f, 0f, bladeSpeed * Time.deltaTime);
        }
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

    private void UpdateTelegraph()
    {
        if (telegraphLine == null)
        {
            return;
        }

        bool charging = state == State.HoverCharge;
        bool attacking = state == State.MeleeAttack;
        telegraphLine.enabled = charging || attacking;

        if (charging)
        {
            Vector2 start = transform.position;
            Vector2 end = GetAimPoint();
            telegraphLine.startWidth = 0.045f;
            telegraphLine.endWidth = 0.02f;
            telegraphLine.SetPosition(0, new Vector3(start.x, start.y, 0f));
            telegraphLine.SetPosition(1, new Vector3(end.x, end.y, 0f));

            float progress = 1f - Mathf.Clamp01(stateTimer / Mathf.Max(0.01f, windupDuration));
            Color color = Color.Lerp(new Color(1f, 0.85f, 0.3f, 0.3f), new Color(1f, 0.15f, 0.1f, 0.95f), progress);
            telegraphLine.startColor = color;
            telegraphLine.endColor = color;
        }
        else if (attacking)
        {
            Vector2 start = attackStartPosition;
            Vector2 end = transform.position;
            telegraphLine.startWidth = 0.18f;
            telegraphLine.endWidth = 0.03f;
            telegraphLine.SetPosition(0, new Vector3(start.x, start.y, 0f));
            telegraphLine.SetPosition(1, new Vector3(end.x, end.y, 0f));
            telegraphLine.startColor = new Color(1f, 0.35f, 0.12f, 0.9f);
            telegraphLine.endColor = new Color(1f, 0.8f, 0.3f, 0.25f);
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
            bodyParts = new[] { bodyRenderer };
            bodyPartColors = new[] { bodyBaseColor };
            visualBasePosition = visualRoot != null ? visualRoot.localPosition : Vector3.zero;
            return;
        }

        GameObject visualObject = new GameObject("Visuals");
        visualRoot = visualObject.transform;
        visualRoot.SetParent(transform, false);
        visualBasePosition = visualRoot.localPosition;

        float barY = 1.05f;

        if (bodySprite != null)
        {
            bodyRenderer = CreatePart("Body", visualRoot, Vector3.zero, Vector3.one * bodySpriteScale, Color.white, 20, bodySprite);
            barY = bodySprite.bounds.size.y * bodySpriteScale * 0.5f + 0.18f;
        }
        else
        {
            bodyRenderer = CreatePart("Body", visualRoot, Vector3.zero, new Vector3(0.9f, 0.56f, 1f), new Color(0.26f, 0.3f, 0.34f, 1f), 20);
            CreatePart("TopPlate", visualRoot, new Vector3(0f, 0.2f, 0f), new Vector3(0.56f, 0.18f, 1f), new Color(0.17f, 0.2f, 0.23f, 1f), 21);
            CreatePart("Eye", visualRoot, new Vector3(0f, 0f, 0f), new Vector3(0.22f, 0.16f, 1f), new Color(0.95f, 0.18f, 0.12f, 1f), 22);

            GameObject leftRotorObject = new GameObject("RotorLeft");
            rotorLeft = leftRotorObject.transform;
            rotorLeft.SetParent(visualRoot, false);
            rotorLeft.localPosition = new Vector3(-0.65f, 0.12f, 0f);
            CreatePart("LeftBlade", rotorLeft, Vector3.zero, new Vector3(0.62f, 0.06f, 1f), new Color(0.65f, 0.7f, 0.72f, 1f), 19);

            GameObject rightRotorObject = new GameObject("RotorRight");
            rotorRight = rightRotorObject.transform;
            rotorRight.SetParent(visualRoot, false);
            rotorRight.localPosition = new Vector3(0.65f, 0.12f, 0f);
            CreatePart("RightBlade", rotorRight, Vector3.zero, new Vector3(0.62f, 0.06f, 1f), new Color(0.65f, 0.7f, 0.72f, 1f), 19);

            GameObject bladesObject = new GameObject("MeleeBlades");
            meleeBlades = bladesObject.transform;
            meleeBlades.SetParent(visualRoot, false);
            CreatePart("BladeHorizontal", meleeBlades, Vector3.zero, new Vector3(1.28f, 0.07f, 1f), new Color(0.82f, 0.88f, 0.9f, 1f), 23);
            CreatePart("BladeVertical", meleeBlades, Vector3.zero, new Vector3(0.07f, 0.98f, 1f), new Color(0.82f, 0.88f, 0.9f, 1f), 23);
        }

        GameObject barObject = new GameObject("HealthBar");
        healthBarRoot = barObject.transform;
        healthBarRoot.SetParent(transform, false);
        healthBarRoot.localPosition = new Vector3(0f, barY, 0f);
        CreatePart("Back", healthBarRoot, Vector3.zero, new Vector3(1.5f, 0.16f, 1f), new Color(0.08f, 0.08f, 0.09f, 0.9f), 30);
        SpriteRenderer fill = CreatePart("Fill", healthBarRoot, Vector3.zero, new Vector3(barWidth, 0.09f, 1f), new Color(0.86f, 0.22f, 0.18f, 1f), 31);
        healthBarFill = fill.transform;

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

        GameObject lineObject = new GameObject("TelegraphLine");
        lineObject.transform.SetParent(transform, false);
        telegraphLine = lineObject.AddComponent<LineRenderer>();
        telegraphLine.useWorldSpace = true;
        telegraphLine.positionCount = 2;
        telegraphLine.numCapVertices = 2;
        telegraphLine.material = GetLineMaterial();
        telegraphLine.sortingOrder = 28;
        telegraphLine.enabled = false;

        bodyBaseColor = bodyRenderer.color;
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
            whiteSprite.name = "AerialDroneWhite";
        }

        return whiteSprite;
    }

    private static Material GetLineMaterial()
    {
        if (lineMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            }

            if (shader != null)
            {
                lineMaterial = new Material(shader)
                {
                    name = "AerialDroneLineMaterial"
                };
            }
        }

        return lineMaterial;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.7f);
        Gizmos.DrawWireSphere(transform.position, detectRange);
        Gizmos.color = new Color(1f, 0.25f, 0.15f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, attackTriggerRadius);
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
