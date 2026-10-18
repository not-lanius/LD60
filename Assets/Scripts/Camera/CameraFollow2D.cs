using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollow2D : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector2 offset = new Vector2(0f, 1.5f);
    [SerializeField] private float smoothTime = 0.16f;
    [SerializeField] private Vector2 lookAhead = new Vector2(1.6f, 0.8f);
    [SerializeField] private float verticalDeadZone;
    [SerializeField] private bool clampToBounds = true;
    [SerializeField] private Vector2 boundsMin = new Vector2(-10.5f, -3f);
    [SerializeField] private Vector2 boundsMax = new Vector2(10.5f, 8f);

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

        Vector2 anchor = (Vector2)target.position + offset + targetLookAhead;
        float desiredY = anchor.y;

        if (verticalDeadZone > 0f)
        {
            float verticalDelta = anchor.y - transform.position.y;
            if (verticalDelta > verticalDeadZone)
            {
                desiredY = anchor.y - verticalDeadZone;
            }
            else if (verticalDelta < -verticalDeadZone)
            {
                desiredY = anchor.y + verticalDeadZone;
            }
            else
            {
                desiredY = transform.position.y;
            }
        }

        Vector3 desired = new Vector3(anchor.x, desiredY, transform.position.z);

        if (clampToBounds)
        {
            desired.x = Mathf.Clamp(desired.x, boundsMin.x, boundsMax.x);
            desired.y = Mathf.Clamp(desired.y, boundsMin.y, boundsMax.y);
        }

        transform.position = Vector3.SmoothDamp(transform.position, desired, ref smoothVelocity, smoothTime);
    }
}
