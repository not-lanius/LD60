using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SpikeTrap : MonoBehaviour
{
    [SerializeField] private int damage = 25;
    [SerializeField] private float horizontalKnockback = 11f;
    [SerializeField] private float verticalKnockback = 7f;

    private void OnTriggerStay2D(Collider2D other)
    {
        PlayerHealth health = other.GetComponent<PlayerHealth>();
        if (health == null)
        {
            return;
        }

        Vector2 delta = other.transform.position - transform.position;
        float direction = Mathf.Abs(delta.x) > 0.01f ? Mathf.Sign(delta.x) : 1f;
        health.TakeDamage(damage, new Vector2(direction * horizontalKnockback, verticalKnockback));
    }
}
