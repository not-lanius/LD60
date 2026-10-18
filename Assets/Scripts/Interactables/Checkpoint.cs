using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class Checkpoint : MonoBehaviour
{
    [Header("Respawn")]
    [SerializeField] private float respawnDistance = 1.6f;
    [SerializeField] private float respawnHeight = 0.76f;

    [Header("Trigger")]
    [SerializeField] private Vector2 triggerSize = new Vector2(3f, 3f);
    [SerializeField] private float triggerOffsetY = 1f;

    private bool activated;
    private int lastSide = 1;

    public bool Activated => activated;
    public Vector2 LastSpawnPoint { get; private set; }

    private void Reset()
    {
        ApplyTrigger();
    }

    private void Awake()
    {
        ApplyTrigger();
    }

    private void ApplyTrigger()
    {
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box == null)
        {
            return;
        }

        box.isTrigger = true;
        box.size = triggerSize;
        box.offset = new Vector2(0f, triggerOffsetY);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        UpdateCheckpoint(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        UpdateCheckpoint(other);
    }

    private void UpdateCheckpoint(Collider2D other)
    {
        PlayerController2D controller = other.GetComponentInParent<PlayerController2D>();
        if (controller == null)
        {
            return;
        }

        int side = controller.transform.position.x >= transform.position.x ? 1 : -1;
        if (activated && side == lastSide)
        {
            return;
        }

        lastSide = side;
        LastSpawnPoint = new Vector2(
            transform.position.x + side * respawnDistance,
            transform.position.y + respawnHeight);
        controller.SetSpawnPoint(LastSpawnPoint);

        if (activated)
        {
            return;
        }

        activated = true;
        Debug.Log($"Checkpoint activated: {name} at {LastSpawnPoint}");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.25f, 1f, 0.45f, 0.9f);
        Gizmos.DrawWireCube(
            transform.position + Vector3.up * triggerOffsetY,
            new Vector3(triggerSize.x, triggerSize.y, 0.1f));

        Gizmos.color = new Color(1f, 0.9f, 0.2f, 1f);
        Gizmos.DrawSphere(transform.position + new Vector3(-respawnDistance, respawnHeight, 0f), 0.18f);
        Gizmos.DrawSphere(transform.position + new Vector3(respawnDistance, respawnHeight, 0f), 0.18f);
    }
}
