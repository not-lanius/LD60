using UnityEngine;

public class Fireball : MonoBehaviour
{
    [SerializeField] private float speed = 14f;
    [SerializeField] private float lifetime = 0.9f;
    [SerializeField] private float damage = 30f;
    [SerializeField] private float spinSpeed = 540f;
    [SerializeField] private float baseScale = 1.15f;
    [SerializeField] private LayerMask obstacleMask;

    private Rigidbody2D body;
    private float age;

    public float Damage => damage;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    public void Launch(Vector2 direction)
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

        Vector2 normalizedDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        transform.right = normalizedDirection;
        if (body != null)
        {
            body.velocity = normalizedDirection * speed;
        }
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
        float scale = baseScale + Mathf.Sin(age * 30f) * 0.05f;
        transform.localScale = Vector3.one * scale;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<PlayerHealth>() != null)
        {
            return;
        }

        IDamageable damageable = other.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            DamageApplier.Apply(damageable, damage, transform.position, transform.right, DamageType.Spell);
            ImpactFx.Spawn(transform.position);
            Destroy(gameObject);
            return;
        }

        if (((1 << other.gameObject.layer) & obstacleMask) != 0)
        {
            ImpactFx.Spawn(transform.position, new Color(0.85f, 0.8f, 0.7f, 0.8f));
            Destroy(gameObject);
        }
    }
}
