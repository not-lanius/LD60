using UnityEngine;

public class EnemyBullet : MonoBehaviour
{
    [SerializeField] private float speed = 10f;
    [SerializeField] private int damage = 8;
    [SerializeField] private float knockback = 6f;
    [SerializeField] private float lifetime = 4f;
    [SerializeField] private float spinSpeed = 720f;
    [SerializeField] private LayerMask obstacleMask;

    private Rigidbody2D body;
    private Vector2 direction = Vector2.right;
    private float age;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    public void Configure(float launchSpeed, int hitDamage, float hitKnockback, float maxDistance)
    {
        speed = Mathf.Max(0.1f, launchSpeed);
        damage = Mathf.Max(1, hitDamage);
        knockback = Mathf.Max(0f, hitKnockback);
        lifetime = Mathf.Max(0.1f, maxDistance / speed);
    }

    public void Launch(Vector2 launchDirection)
    {
        direction = launchDirection.sqrMagnitude > 0.0001f ? launchDirection.normalized : Vector2.right;
        transform.right = direction;

        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

        if (body != null)
        {
            body.velocity = direction * speed;
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
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerHealth player = other.GetComponentInParent<PlayerHealth>();
        if (player != null)
        {
            player.TakeDamage(damage, direction * knockback);
            ImpactFx.Spawn(transform.position, new Color(1f, 0.35f, 0.25f, 0.95f), 0.7f);
            Destroy(gameObject);
            return;
        }

        if (other.GetComponentInParent<ShieldSoldier>() != null
            || other.GetComponentInParent<RangedSoldier>() != null)
        {
            ImpactFx.Spawn(transform.position, new Color(0.7f, 0.82f, 1f, 0.9f), 0.6f);
            Destroy(gameObject);
            return;
        }

        IDamageable damageable = other.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(damage, transform.position, direction);
            ImpactFx.Spawn(transform.position, new Color(1f, 0.5f, 0.25f, 0.9f), 0.7f);
            Destroy(gameObject);
            return;
        }

        if (((1 << other.gameObject.layer) & obstacleMask) != 0)
        {
            ImpactFx.Spawn(transform.position, new Color(0.7f, 0.65f, 0.6f, 0.8f), 0.6f);
            Destroy(gameObject);
        }
    }
}
