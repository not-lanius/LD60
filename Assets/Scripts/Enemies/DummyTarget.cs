using System.Collections;
using UnityEngine;

public class DummyTarget : MonoBehaviour, IDamageable
{
    [Header("Stats")]
    [SerializeField] private float maxHealth = 120f;
    [SerializeField] private float respawnDelay = 2.5f;

    [Header("Visuals")]
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private Transform healthBarRoot;
    [SerializeField] private Transform healthBarFill;
    [SerializeField] private float barWidth = 1.2f;
    [SerializeField] private Color flashColor = Color.white;
    [SerializeField] private float flashDuration = 0.1f;

    private static DummyTarget lastTouched;

    private float health;
    private Color baseColor;
    private Vector3 bodyScale;
    private float flashTimer;
    private bool isDead;
    private Collider2D bodyCollider;

    private void Awake()
    {
        health = maxHealth;
        bodyCollider = GetComponent<Collider2D>();
        if (bodyRenderer != null)
        {
            baseColor = bodyRenderer.color;
            bodyScale = bodyRenderer.transform.localScale;
        }

        UpdateBar();
    }

    public bool IsBroken => isDead;

    private string DisplayName => name.Replace("DummyTarget_", "靶子");

    public static string DescribeLatest()
    {
        if (lastTouched == null)
        {
            return "靶子: 待机";
        }

        if (lastTouched.isDead)
        {
            return $"{lastTouched.DisplayName}: 已击破, {lastTouched.respawnDelay:0.0}s 后复位";
        }

        return $"{lastTouched.DisplayName}: {lastTouched.health:0}/{lastTouched.maxHealth:0}";
    }

    public void TakeDamage(float amount, Vector2 hitPoint, Vector2 hitDirection)
    {
        if (isDead || amount <= 0f)
        {
            return;
        }

        lastTouched = this;
        health = Mathf.Max(0f, health - amount);
        flashTimer = flashDuration;
        UpdateBar();

        if (health <= 0f)
        {
            StartCoroutine(DeathRoutine());
        }
    }

    private void Update()
    {
        if (bodyRenderer == null)
        {
            return;
        }

        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            float t = Mathf.Clamp01(flashTimer / flashDuration);
            bodyRenderer.color = Color.Lerp(baseColor, flashColor, t);
            bodyRenderer.transform.localScale = bodyScale * (1f + 0.07f * t);
        }
        else if (bodyRenderer.color != baseColor)
        {
            bodyRenderer.color = baseColor;
            bodyRenderer.transform.localScale = bodyScale;
        }
    }

    private void UpdateBar()
    {
        if (healthBarFill == null)
        {
            return;
        }

        float ratio = maxHealth > 0f ? Mathf.Clamp01(health / maxHealth) : 0f;
        Vector3 scale = healthBarFill.localScale;
        healthBarFill.localScale = new Vector3(barWidth * ratio, scale.y, 1f);
        healthBarFill.localPosition = new Vector3(-barWidth * (1f - ratio) * 0.5f, 0f, 0f);
    }

    private IEnumerator DeathRoutine()
    {
        isDead = true;

        if (healthBarRoot != null)
        {
            healthBarRoot.gameObject.SetActive(false);
        }

        if (bodyCollider != null)
        {
            bodyCollider.enabled = false;
        }

        float duration = 0.35f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            if (bodyRenderer != null)
            {
                bodyRenderer.transform.localScale = bodyScale * (1f - 0.45f * t);
                Color color = baseColor;
                color.a = 1f - t;
                bodyRenderer.color = color;
            }

            yield return null;
        }

        if (bodyRenderer != null)
        {
            bodyRenderer.gameObject.SetActive(false);
        }

        yield return new WaitForSeconds(respawnDelay);

        health = maxHealth;
        isDead = false;
        flashTimer = 0f;

        if (bodyRenderer != null)
        {
            bodyRenderer.gameObject.SetActive(true);
            bodyRenderer.transform.localScale = bodyScale;
            bodyRenderer.color = baseColor;
        }

        if (bodyCollider != null)
        {
            bodyCollider.enabled = true;
        }

        if (healthBarRoot != null)
        {
            healthBarRoot.gameObject.SetActive(true);
        }

        UpdateBar();
    }
}
