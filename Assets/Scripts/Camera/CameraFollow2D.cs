using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollow2D : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector2 offset = new Vector2(0f, 1.5f);
    [SerializeField] private float smoothTime = 0.16f;
    [SerializeField] private Vector2 lookAhead = new Vector2(1.6f, 0.8f);
    [SerializeField] private bool clampToBounds = true;
    [SerializeField] private Vector2 boundsMin = new Vector2(-10.5f, 2.2f);
    [SerializeField] private Vector2 boundsMax = new Vector2(10.5f, 5f);

    private Rigidbody2D targetBody;
    private Vector3 smoothVelocity;

    private void Awake()
    {
        if (target != null)
        {
            targetBody = target.GetComponent<Rigidbody2D>();
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector2 velocity = targetBody != null ? targetBody.velocity : Vector2.zero;
        Vector2 targetLookAhead = new Vector2(
            Mathf.Clamp(velocity.x * 0.16f, -lookAhead.x, lookAhead.x),
            Mathf.Clamp(velocity.y * 0.08f, -lookAhead.y, lookAhead.y));

        Vector3 desired = target.position + (Vector3)(offset + targetLookAhead);
        desired.z = transform.position.z;

        if (clampToBounds)
        {
            desired.x = Mathf.Clamp(desired.x, boundsMin.x, boundsMax.x);
            desired.y = Mathf.Clamp(desired.y, boundsMin.y, boundsMax.y);
        }

        transform.position = Vector3.SmoothDamp(transform.position, desired, ref smoothVelocity, smoothTime);
    }
}
