using UnityEngine;

public class RockWall : MonoBehaviour, IDamageable
{
    [Header("Stats")]
    [SerializeField] private float maxHealth = 150f;
    [SerializeField] private float lifetime = 12f;

    [Header("Explosion")]
    [SerializeField] private float explosionRadius = 2.8f;
    [SerializeField] private float explosionDamage = 50f;
    [SerializeField] private bool damagePlayer;

    [Header("Visuals")]
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private Color flashColor = Color.white;
    [SerializeField] private float flashDuration = 0.1f;

    private float health;
    private float age;
    private float flashTimer;
    private bool isBreaking;
    private Color baseColor;
    private Vector3 baseScale;

    private void Awake()
    {
        health = maxHealth;

        if (bodyRenderer == null)
        {
            bodyRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (bodyRenderer != null)
        {
            baseColor = bodyRenderer.color;
            baseScale = bodyRenderer.transform.localScale;
        }
    }

    private void Start()
    {
        if (AudioDirector.Instance != null)
        {
            AudioDirector.Instance.PlayRockRise();
        }
    }

    public void TakeDamage(float amount, Vector2 hitPoint, Vector2 hitDirection)
    {
        if (isBreaking || amount <= 0f)
        {
            return;
        }

        PlayerMana.ReportCombatActivity();
        health -= amount;
        flashTimer = flashDuration;

        if (health <= 0f)
        {
            Break();
        }
    }

    private void Update()
    {
        if (isBreaking)
        {
            return;
        }

        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            float t = Mathf.Clamp01(flashTimer / flashDuration);
            if (bodyRenderer != null)
            {
                bodyRenderer.color = Color.Lerp(baseColor, flashColor, t);
                bodyRenderer.transform.localScale = baseScale * (1f + 0.03f * t);
            }
        }
        else if (bodyRenderer != null && bodyRenderer.color != baseColor)
        {
            bodyRenderer.color = baseColor;
            bodyRenderer.transform.localScale = baseScale;
        }

        age += Time.deltaTime;
        if (age >= lifetime)
        {
            Break();
        }
    }

    private void Break()
    {
        if (isBreaking)
        {
            return;
        }

        isBreaking = true;

        if (AudioDirector.Instance != null)
        {
            AudioDirector.Instance.PlayRockExplosion();
        }

        Explode();
        Destroy(gameObject);
    }

    private void Explode()
    {
        Vector2 center = transform.position + Vector3.up * 1.2f;
        ImpactFx.Spawn(center, new Color(1f, 0.62f, 0.2f, 1f), 2.4f);
        ImpactFx.Spawn(center + new Vector2(-0.5f, 0.4f), new Color(1f, 0.8f, 0.4f, 1f), 1.4f);
        ImpactFx.Spawn(center + new Vector2(0.6f, -0.3f), new Color(1f, 0.5f, 0.15f, 1f), 1.6f);

        Collider2D[] hits = Physics2D.OverlapCircleAll(center, explosionRadius);
        foreach (Collider2D hit in hits)
        {
            if (hit == null)
            {
                continue;
            }

            if (!damagePlayer && hit.GetComponentInParent<PlayerHealth>() != null)
            {
                continue;
            }

            IDamageable target = hit.GetComponentInParent<IDamageable>();
            if (target == null || ReferenceEquals(target, this))
            {
                continue;
            }

            Vector2 direction = ((Vector2)hit.bounds.center - center).normalized;
            DamageApplier.Apply(target, explosionDamage, hit.bounds.center, direction, DamageType.Spell);
            PlayerMana.ReportCombatActivity();
        }
    }
}
