using UnityEngine;

public class ImpactFx : MonoBehaviour
{
    private const float Duration = 0.22f;

    private static Sprite cachedSprite;

    private SpriteRenderer spriteRenderer;
    private Color startColor;
    private float size = 1f;
    private float age;

    public static void Spawn(Vector2 position, Color? tint = null, float scale = 1f)
    {
        if (cachedSprite == null)
        {
            cachedSprite = Resources.Load<Sprite>("ImpactRing");
        }

        if (cachedSprite == null)
        {
            return;
        }

        GameObject instance = new GameObject("ImpactFx");
        instance.transform.position = position;
        instance.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        instance.transform.localScale = Vector3.one * 0.35f * scale;

        ImpactFx fx = instance.AddComponent<ImpactFx>();
        fx.spriteRenderer = instance.AddComponent<SpriteRenderer>();
        fx.spriteRenderer.sprite = cachedSprite;
        fx.spriteRenderer.sortingOrder = 30;
        fx.startColor = tint ?? new Color(1f, 0.85f, 0.45f, 0.95f);
        fx.spriteRenderer.color = fx.startColor;
        fx.size = Mathf.Max(0.1f, scale);
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / Duration);

        transform.localScale = Vector3.one * Mathf.Lerp(0.35f, 1.45f, t) * size;

        if (spriteRenderer != null)
        {
            Color color = startColor;
            color.a = startColor.a * (1f - t);
            spriteRenderer.color = color;
        }

        if (age >= Duration)
        {
            Destroy(gameObject);
        }
    }
}
