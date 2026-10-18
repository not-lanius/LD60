using System.Collections.Generic;
using UnityEngine;

public class LightningField : MonoBehaviour
{
    [Header("Area")]
    [SerializeField] private Vector2 size = new Vector2(7f, 5.2f);
    [SerializeField] private float startOffset = 0.35f;
    [SerializeField] private float bottomOffset = 0.1f;

    [Header("Damage")]
    [SerializeField] private float damagePerTick = 18f;
    [SerializeField] private int tickCount = 3;
    [SerializeField] private float tickInterval = 0.12f;

    [Header("Visuals")]
    [SerializeField] private int boltCount = 8;
    [SerializeField] private int segmentsPerBolt = 9;
    [SerializeField] private float boltWidth = 0.08f;
    [SerializeField] private float flickerInterval = 0.06f;
    [SerializeField] private float flickerJitter = 0.35f;
    [SerializeField] private float duration = 0.5f;
    [SerializeField] private Color boltColor = new Color(0.62f, 0.95f, 1f, 1f);
    [SerializeField] private Color fieldColor = new Color(0.45f, 0.85f, 1f, 0.12f);

    private static Material boltMaterial;
    private static Sprite whiteSprite;

    private readonly List<LineRenderer> bolts = new List<LineRenderer>();
    private LineRenderer outline;
    private SpriteRenderer fieldRenderer;
    private int facing = 1;
    private Vector2 center;
    private float age;
    private int ticksDone;
    private float flickerTimer;

    public void Begin(Vector2 origin, int facingDirection)
    {
        facing = facingDirection >= 0 ? 1 : -1;
        center = new Vector2(
            origin.x + facing * (startOffset + size.x * 0.5f),
            origin.y + size.y * 0.5f - bottomOffset);
        transform.position = center;

        if (AudioDirector.Instance != null)
        {
            AudioDirector.Instance.PlayLightning();
        }

        BuildVisuals();
        RefreshBolts();
        SetAlpha(1f);

        ApplyTick();
        ticksDone = 1;
    }

    private void Update()
    {
        age += Time.deltaTime;
        flickerTimer -= Time.deltaTime;

        if (ticksDone < tickCount && age >= ticksDone * tickInterval)
        {
            ApplyTick();
            ticksDone++;
        }

        if (flickerTimer <= 0f)
        {
            flickerTimer = flickerInterval;
            RefreshBolts();
        }

        SetAlpha(Mathf.Clamp01(1f - age / Mathf.Max(0.01f, duration)));

        if (age >= duration)
        {
            Destroy(gameObject);
        }
    }

    private void ApplyTick()
    {
        Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f);
        HashSet<IDamageable> damaged = new HashSet<IDamageable>();

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

            Vector2 point = hit.bounds.center;
            Vector2 direction = (point - center).sqrMagnitude > 0.0001f ? (point - center).normalized : Vector2.up;
            DamageApplier.Apply(target, damagePerTick, point, direction, DamageType.Spell);
            PlayerMana.ReportCombatActivity();
        }
    }

    private void BuildVisuals()
    {
        GameObject fillObject = new GameObject("FieldFlash");
        fillObject.transform.SetParent(transform, false);
        fillObject.transform.localScale = new Vector3(size.x, size.y, 1f);
        fieldRenderer = fillObject.AddComponent<SpriteRenderer>();
        fieldRenderer.sprite = GetWhiteSprite();
        fieldRenderer.color = fieldColor;
        fieldRenderer.sortingOrder = 24;

        outline = CreateLine("FieldOutline", 4, 0.05f);
        outline.loop = true;
        Vector3[] corners =
        {
            new Vector3(-size.x * 0.5f, -size.y * 0.5f, 0f),
            new Vector3(-size.x * 0.5f, size.y * 0.5f, 0f),
            new Vector3(size.x * 0.5f, size.y * 0.5f, 0f),
            new Vector3(size.x * 0.5f, -size.y * 0.5f, 0f)
        };
        outline.SetPositions(corners);

        for (int i = 0; i < boltCount; i++)
        {
            LineRenderer bolt = CreateLine($"Bolt{i}", segmentsPerBolt + 1, boltWidth * Random.Range(0.6f, 1.2f));
            bolts.Add(bolt);
        }
    }

    private LineRenderer CreateLine(string lineName, int positions, float width)
    {
        GameObject lineObject = new GameObject(lineName);
        lineObject.transform.SetParent(transform, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.positionCount = positions;
        line.startWidth = width;
        line.endWidth = width;
        line.numCapVertices = 2;
        line.material = GetBoltMaterial();
        line.startColor = boltColor;
        line.endColor = boltColor;
        line.sortingOrder = 25;
        return line;
    }

    private void RefreshBolts()
    {
        float halfWidth = size.x * 0.5f;
        float halfHeight = size.y * 0.5f;

        foreach (LineRenderer bolt in bolts)
        {
            if (bolt == null)
            {
                continue;
            }

            float baseX = Random.Range(-halfWidth + 0.1f, halfWidth - 0.1f);
            for (int i = 0; i < bolt.positionCount; i++)
            {
                float t = bolt.positionCount > 1 ? i / (float)(bolt.positionCount - 1) : 0f;
                float y = Mathf.Lerp(-halfHeight, halfHeight, t);
                float jitter = i == 0 || i == bolt.positionCount - 1 ? 0f : Random.Range(-flickerJitter, flickerJitter);
                float x = Mathf.Clamp(baseX + jitter, -halfWidth, halfWidth);
                bolt.SetPosition(i, new Vector3(x, y, 0f));
            }
        }
    }

    private void SetAlpha(float alpha)
    {
        Color bolt = boltColor;
        bolt.a *= alpha;
        foreach (LineRenderer line in bolts)
        {
            if (line != null)
            {
                line.startColor = bolt;
                line.endColor = bolt;
            }
        }

        if (outline != null)
        {
            outline.startColor = bolt;
            outline.endColor = bolt;
        }

        if (fieldRenderer != null)
        {
            Color fill = fieldColor;
            fill.a *= alpha;
            fieldRenderer.color = fill;
        }
    }

    private static Material GetBoltMaterial()
    {
        if (boltMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            }

            if (shader != null)
            {
                boltMaterial = new Material(shader)
                {
                    name = "LightningBoltMaterial"
                };
            }
        }

        return boltMaterial;
    }

    private static Sprite GetWhiteSprite()
    {
        if (whiteSprite == null)
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            whiteSprite.name = "LightningFieldFill";
        }

        return whiteSprite;
    }
}
