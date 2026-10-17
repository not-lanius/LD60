using UnityEngine;

[RequireComponent(typeof(DummyTarget))]
public class ShootingEnemy : MonoBehaviour
{
    [Header("Firing")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform target;
    [SerializeField] private float fireInterval = 1.5f;
    [SerializeField] private float firstShotDelay = 1.2f;
    [SerializeField] private float range = 20f;

    [Header("Aim")]
    [SerializeField] private float muzzleHeight = 1.25f;
    [SerializeField] private float aimHeight = 0.5f;
    [SerializeField] private float muzzleOffset = 0.9f;

    private DummyTarget dummy;
    private float cooldown;

    public float FireInterval
    {
        get => fireInterval;
        set => fireInterval = Mathf.Max(0.1f, value);
    }

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
        if (bulletPrefab == null || target == null)
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

        Vector3 muzzle = transform.position + Vector3.up * muzzleHeight;
        Vector3 aimPoint = GetAimPoint();
        Vector3 delta = aimPoint - muzzle;

        if (delta.magnitude > range)
        {
            return;
        }

        Fire(delta.normalized);
        cooldown = fireInterval;
    }

    private Vector3 GetAimPoint()
    {
        Collider2D targetCollider = target.GetComponentInChildren<Collider2D>();
        if (targetCollider != null)
        {
            return targetCollider.bounds.center;
        }

        return target.position + Vector3.up * aimHeight;
    }

    private Vector3 GetSpawnPosition(Vector3 direction)
    {
        Vector3 muzzle = transform.position + Vector3.up * muzzleHeight;
        Collider2D selfCollider = GetComponent<Collider2D>();

        if (selfCollider != null)
        {
            Ray ray = new Ray(muzzle, direction);
            if (selfCollider.bounds.IntersectRay(ray, out float exitDistance))
            {
                return muzzle + direction * (exitDistance + 0.3f);
            }
        }

        return muzzle + direction * muzzleOffset;
    }

    private void Fire(Vector3 direction)
    {
        Vector3 spawnPosition = GetSpawnPosition(direction);
        GameObject bulletObject = Instantiate(bulletPrefab, spawnPosition, Quaternion.identity);
        EnemyBullet bullet = bulletObject.GetComponent<EnemyBullet>();
        if (bullet != null)
        {
            bullet.Launch(direction);
        }

        Collider2D bulletCollider = bulletObject.GetComponent<Collider2D>();
        Collider2D selfCollider = GetComponent<Collider2D>();
        if (bulletCollider != null && selfCollider != null)
        {
            Physics2D.IgnoreCollision(bulletCollider, selfCollider, true);
        }

        ImpactFx.Spawn(spawnPosition, new Color(1f, 0.45f, 0.25f, 0.9f), 0.5f);
    }
}
