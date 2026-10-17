using System;
using System.Collections;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private float invulnerabilityDuration = 0.9f;
    [SerializeField] private float respawnDelay = 0.7f;
    [SerializeField] private float flashInterval = 0.08f;

    public event Action<int, int> HealthChanged;

    private int currentHealth;
    private float invulnerabilityTimer;
    private float flashTimer;
    private bool flashOn;
    private bool isDead;
    private PlayerController2D controller;
    private SpriteRenderer spriteRenderer;
    private Color baseColor = Color.white;

    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    public bool IsDead => isDead;
    public bool IsInvulnerable => invulnerabilityTimer > 0f;

    private void Awake()
    {
        currentHealth = maxHealth;
        controller = GetComponent<PlayerController2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            baseColor = spriteRenderer.color;
        }
    }

    private void Start()
    {
        NotifyHealthChanged();
    }

    private void Update()
    {
        if (isDead)
        {
            SetFlash(false);
            return;
        }

        if (invulnerabilityTimer > 0f)
        {
            invulnerabilityTimer -= Time.deltaTime;
            flashTimer -= Time.deltaTime;

            if (flashTimer <= 0f)
            {
                flashOn = !flashOn;
                flashTimer = flashInterval;
                SetFlash(flashOn);
            }

            if (invulnerabilityTimer <= 0f)
            {
                SetFlash(false);
            }
        }
    }

    public bool TakeDamage(int amount, Vector2 knockback)
    {
        if (isDead || amount <= 0 || invulnerabilityTimer > 0f)
        {
            return false;
        }

        currentHealth = Mathf.Max(0, currentHealth - amount);
        NotifyHealthChanged();
        invulnerabilityTimer = invulnerabilityDuration;
        flashTimer = 0f;

        if (controller != null)
        {
            controller.ApplyKnockback(knockback, 0.18f);
        }

        if (currentHealth <= 0)
        {
            StartCoroutine(DieRoutine());
        }

        return true;
    }

    public void Heal(int amount)
    {
        if (isDead || amount <= 0)
        {
            return;
        }

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        NotifyHealthChanged();
    }

    private IEnumerator DieRoutine()
    {
        isDead = true;
        SetFlash(false);

        if (controller != null)
        {
            controller.enabled = false;
        }

        yield return new WaitForSeconds(respawnDelay);

        if (controller != null)
        {
            controller.enabled = true;
            controller.RespawnAtSpawnPoint();
        }

        currentHealth = maxHealth;
        invulnerabilityTimer = 1.2f;
        flashTimer = 0f;
        isDead = false;
        NotifyHealthChanged();
    }

    private void SetFlash(bool on)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = on ? new Color(1f, 0.35f, 0.3f, 1f) : baseColor;
        }
    }

    private void NotifyHealthChanged()
    {
        HealthChanged?.Invoke(currentHealth, maxHealth);
    }
}
