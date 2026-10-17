using System.Collections.Generic;
using UnityEngine;

public class Bomb : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] private int damage = 12;
    [SerializeField] private float explosionRadius = 1.8f;
    [SerializeField] private float knockback = 6f;

    [Header("Life")]
    [SerializeField] private float lifetime = 8f;
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] private float spinSpeed = 180f;

    private Rigidbody2D body;
    private float age;
    private bool exploded;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    public void LaunchTo(Vector2 target, float flightTime)
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

        if (body == null)
        {
            return;
        }

        Vector2 start = transform.position;
        float gravity = Mathf.Abs(Physics2D.gravity.y) * Mathf.Max(0.01f, body.gravityScale);
        float time = Mathf.Max(0.3f, flightTime);
        float vx = (target.x - start.x) / time;
        float vy = (target.y - start.y + 0.5f * gravity * time * time) / time;
        body.velocity = new Vector2(vx, vy);
    }

    private void Update()
    {
        age += Time.deltaTime;
        transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);

        if (age >= lifetime)
        {
            Explode();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (exploded || other == null)
        {
            return;
        }

        if (other.GetComponentInParent<PlayerHealth>() != null)
        {
            Explode();
            return;
        }

        if (((1 << other.gameObject.layer) & obstacleMask) != 0)
        {
            Explode();
        }
    }

    private void Explode()
    {
        if (exploded)
        {
            return;
        }

        exploded = true;

        ImpactFx.Spawn(transform.position, new Color(1f, 0.6f, 0.22f, 1f), 2f);
        ImpactFx.Spawn(transform.position + Vector3.up * 0.3f, new Color(1f, 0.85f, 0.45f, 0.9f), 1.2f);

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius);
        HashSet<PlayerHealth> damagedPlayers = new HashSet<PlayerHealth>();

        foreach (Collider2D hit in hits)
        {
            if (hit == null)
            {
                continue;
            }

            PlayerHealth player = hit.GetComponentInParent<PlayerHealth>();
            if (player == null || !damagedPlayers.Add(player))
            {
                continue;
            }

            Vector2 direction = (Vector2)hit.bounds.center - (Vector2)transform.position;
            Vector2 impulse = (direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up) * knockback;
            player.TakeDamage(damage, impulse);
        }

        Destroy(gameObject);
    }
}
