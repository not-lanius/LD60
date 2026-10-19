using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class PlayerMelee : MonoBehaviour
{
    [Header("Attack")]
    [SerializeField] private float damage = 35f;
    [SerializeField] private float cooldown = 0.35f;
    [SerializeField] private float attackOffset = 0.95f;
    [SerializeField] private Vector2 attackSize = new Vector2(1.8f, 1.6f);

    [Header("Mana")]
    [SerializeField] private int manaPerHit = 12;

    [Header("References")]
    [SerializeField] private PlayerSwordVisual swordVisual;

    [Header("Visuals")]
    [SerializeField] private Color slashColor = new Color(1f, 0.95f, 0.7f, 0.95f);
    [SerializeField] private float slashRadius = 1.15f;
    [SerializeField] private float slashDuration = 0.12f;
    [SerializeField] private int slashSegments = 12;

    private static Material slashMaterial;

    private PlayerController2D controller;
    private PlayerHealth health;
    private PlayerMana mana;
    private Collider2D body;
    private float cooldownTimer;

    private void Awake()
    {
        controller = GetComponent<PlayerController2D>();
        health = GetComponent<PlayerHealth>();
        mana = GetComponent<PlayerMana>();
        body = GetComponent<Collider2D>();

        if (swordVisual == null)
        {
            swordVisual = GetComponentInChildren<PlayerSwordVisual>();
        }
    }

    private void Update()
    {
        if (TutorialUI.BlocksGameplay)
        {
            return;
        }

        cooldownTimer = Mathf.Max(0f, cooldownTimer - Time.deltaTime);

        if (health != null && health.IsDead)
        {
            return;
        }

        if (cooldownTimer > 0f || !Input.GetMouseButtonDown(0))
        {
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        Attack();
    }

    private void Attack()
    {
        cooldownTimer = cooldown;

        if (mana != null)
        {
            mana.NotifyCombatActivity();
        }

        if (swordVisual != null)
        {
            swordVisual.PlaySwing();
        }

        if (AudioDirector.Instance != null)
        {
            AudioDirector.Instance.PlayMelee();
        }

        int facing = controller != null ? controller.Facing : 1;
        Vector2 center = (Vector2)transform.position + new Vector2(facing * attackOffset, 0f);
        if (body != null)
        {
            center = (Vector2)body.bounds.center + new Vector2(facing * attackOffset, 0f);
        }

        Collider2D[] hits = Physics2D.OverlapBoxAll(center, attackSize, 0f);
        HashSet<IDamageable> damaged = new HashSet<IDamageable>();
        int hitCount = 0;

        foreach (Collider2D hit in hits)
        {
            if (hit == null || hit.GetComponentInParent<PlayerHealth>() != null || hit.GetComponentInParent<RockWall>() != null)
            {
                continue;
            }

            IDamageable target = hit.GetComponentInParent<IDamageable>();
            if (target == null || !damaged.Add(target))
            {
                continue;
            }

            DamageApplier.Apply(target, damage, hit.bounds.center, new Vector2(facing, 0f), DamageType.Physical);
            ImpactFx.Spawn(hit.bounds.center, new Color(1f, 0.9f, 0.55f, 0.95f), 0.8f);
            hitCount++;
        }

        if (hitCount > 0 && mana != null)
        {
            mana.Restore(manaPerHit * hitCount);
        }

        StartCoroutine(SlashRoutine(facing));
    }

    private IEnumerator SlashRoutine(int facing)
    {
        GameObject slashObject = new GameObject("MeleeSlash");
        LineRenderer line = slashObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = Mathf.Max(2, slashSegments);
        line.startWidth = 0.18f;
        line.endWidth = 0.03f;
        line.numCapVertices = 2;
        line.material = GetSlashMaterial();
        line.sortingOrder = 27;

        Vector2 center = body != null
            ? (Vector2)body.bounds.center
            : (Vector2)transform.position + new Vector2(0f, 0.5f);

        for (int i = 0; i < line.positionCount; i++)
        {
            float t = line.positionCount > 1 ? i / (float)(line.positionCount - 1) : 0f;
            float angle = Mathf.Lerp(-75f, 75f, t) * Mathf.Deg2Rad;
            Vector2 offset = new Vector2(Mathf.Cos(angle) * facing, Mathf.Sin(angle) * 0.9f) * slashRadius;
            line.SetPosition(i, new Vector3(center.x + offset.x, center.y + offset.y, 0f));
        }

        float age = 0f;
        while (age < slashDuration)
        {
            age += Time.deltaTime;
            float alpha = Mathf.Clamp01(1f - age / slashDuration);
            Color color = slashColor;
            color.a *= alpha;
            line.startColor = color;
            line.endColor = color;
            yield return null;
        }

        Destroy(slashObject);
    }

    private static Material GetSlashMaterial()
    {
        if (slashMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            }

            if (shader != null)
            {
                slashMaterial = new Material(shader)
                {
                    name = "MeleeSlashMaterial"
                };
            }
        }

        return slashMaterial;
    }
}
