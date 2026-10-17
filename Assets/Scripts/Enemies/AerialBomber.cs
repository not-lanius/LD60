using UnityEngine;

[RequireComponent(typeof(DummyTarget))]
public class AerialBomber : MonoBehaviour
{
    [Header("Firing")]
    [SerializeField] private GameObject bombPrefab;
    [SerializeField] private Transform target;
    [SerializeField] private float fireInterval = 2.4f;
    [SerializeField] private float firstShotDelay = 1.6f;
    [SerializeField] private float range = 24f;

    [Header("Arc")]
    [SerializeField] private float launchSpeedReference = 8f;
    [SerializeField] private float minFlightTime = 1f;
    [SerializeField] private float maxFlightTime = 2.2f;
    [SerializeField] private float spawnClearance = 0.3f;

    private DummyTarget dummy;
    private float cooldown;

    private void Awake()
    {
        dummy = GetComponent<DummyTarget>();
        cooldown = firstShotDelay;

        if (target == null)
        {
            PlayerController2D player = FindObjectOfType<PlayerController2D>();
            if (player != null)
            {
                target = player.transform;
            }
        }
    }

    private void Update()
    {
        if (bombPrefab == null || target == null)
        {
            return;
        }

        if (dummy != null && dummy.IsBroken)
        {
            return;
        }

        cooldown -= Time.deltaTime;
        if (cooldown > 0f)
        {
            return;
        }

        Vector2 aimPoint = GetAimPoint();
        Vector2 origin = GetSpawnPosition();
        float distance = Vector2.Distance(origin, aimPoint);

        if (distance > range)
        {
            return;
        }

        Fire(origin, aimPoint, distance);
        cooldown = fireInterval;
    }

    private Vector2 GetAimPoint()
    {
        Collider2D targetCollider = target.GetComponentInChildren<Collider2D>();
        if (targetCollider != null)
        {
            return targetCollider.bounds.center;
        }

        return target.position;
    }

    private Vector2 GetSpawnPosition()
    {
        Collider2D selfCollider = GetComponent<Collider2D>();
        if (selfCollider != null)
        {
            return new Vector2(transform.position.x, selfCollider.bounds.max.y + spawnClearance);
        }

        return (Vector2)transform.position + Vector2.up * 0.9f;
    }

    private void Fire(Vector2 origin, Vector2 aimPoint, float distance)
    {
        float flightTime = Mathf.Clamp(distance / Mathf.Max(0.1f, launchSpeedReference), minFlightTime, maxFlightTime);

        GameObject bombObject = Instantiate(bombPrefab, origin, Quaternion.identity);
        Collider2D bombCollider = bombObject.GetComponent<Collider2D>();
        Collider2D selfCollider = GetComponent<Collider2D>();
        if (bombCollider != null && selfCollider != null)
        {
            Physics2D.IgnoreCollision(bombCollider, selfCollider, true);
        }

        Bomb bomb = bombObject.GetComponent<Bomb>();
        if (bomb != null)
        {
            bomb.LaunchTo(aimPoint, flightTime);
        }

        ImpactFx.Spawn(origin, new Color(1f, 0.7f, 0.3f, 0.9f), 0.6f);
    }
}
