using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SpikeTrap : MonoBehaviour
{
    [SerializeField] private int damage = 25;
    [SerializeField] private float horizontalKnockback = 11f;
    [SerializeField] private float verticalKnockback = 7f;
    [SerializeField] private float enemyDamageInterval = 0.5f;

    private readonly Dictionary<int, float> enemyNextHitTimes = new Dictionary<int, float>();

    private void OnTriggerStay2D(Collider2D other)
    {
        PlayerHealth health = other.GetComponent<PlayerHealth>();
        if (health != null)
        {
            Vector2 delta = other.transform.position - transform.position;
            float direction = Mathf.Abs(delta.x) > 0.01f ? Mathf.Sign(delta.x) : 1f;
            health.TakeDamage(damage, new Vector2(direction * horizontalKnockback, verticalKnockback));
            return;
        }

        IDamageable target = other.GetComponentInParent<IDamageable>();
        if (target == null || target is RockWall)
        {
            return;
        }

        int targetId = GetTargetId(target, other);
        if (enemyNextHitTimes.TryGetValue(targetId, out float nextHit) && Time.time < nextHit)
        {
            return;
        }

        enemyNextHitTimes[targetId] = Time.time + Mathf.Max(0.05f, enemyDamageInterval);

        Vector2 enemyDelta = other.bounds.center - transform.position;
        float enemyDirection = Mathf.Abs(enemyDelta.x) > 0.01f ? Mathf.Sign(enemyDelta.x) : 1f;
        Vector2 hitDirection = new Vector2(enemyDirection, 0.35f).normalized;
        DamageApplier.Apply(target, damage, other.bounds.center, hitDirection, DamageType.Environment);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        IDamageable target = other.GetComponentInParent<IDamageable>();
        if (target != null)
        {
            enemyNextHitTimes.Remove(GetTargetId(target, other));
        }
    }

    private static int GetTargetId(IDamageable target, Collider2D other)
    {
        if (target is Component component && component != null)
        {
            return component.GetInstanceID();
        }

        return other.GetInstanceID();
    }
}
