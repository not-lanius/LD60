using UnityEngine;

public class PlayerVisual2D : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private float bobAmount = 0.035f;
    [SerializeField] private float bobSpeed = 9f;
    [SerializeField] private float leanAngle = 7f;
    [SerializeField] private float leanSharpness = 12f;
    [SerializeField] private float squashSharpness = 16f;

    private Vector3 baseLocalPosition;
    private Vector3 baseLocalScale;
    private float bobPhase;
    private float currentLean;
    private float squash = 1f;
    private float speed01;
    private float moveSpeedReference = 8f;
    private bool grounded = true;
    private bool dodging;
    private int facing = 1;

    private void Awake()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        baseLocalPosition = transform.localPosition;
        baseLocalScale = transform.localScale;
    }

    public void SetFacing(int newFacing)
    {
        facing = newFacing;
        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = newFacing < 0;
        }
    }

    public void SetMotion(float speed, bool isGrounded, bool isDodging)
    {
        speed01 = Mathf.Clamp01(speed / moveSpeedReference);
        grounded = isGrounded;
        dodging = isDodging;
    }

    public void Land()
    {
        squash = 0.78f;
    }

    public void Jump()
    {
        squash = 1.12f;
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f)
        {
            return;
        }

        float lean = -facing * leanAngle * speed01;
        if (dodging)
        {
            lean *= 2.2f;
        }

        currentLean = Mathf.Lerp(currentLean, lean, 1f - Mathf.Exp(-leanSharpness * dt));

        squash = Mathf.Lerp(squash, 1f, 1f - Mathf.Exp(-squashSharpness * dt));

        float bobOffset = 0f;
        if (grounded)
        {
            bobPhase += dt * bobSpeed * (0.45f + speed01);
            bobOffset = Mathf.Sin(bobPhase) * bobAmount * (0.35f + speed01 * 0.65f);
        }

        Vector3 scale = baseLocalScale;
        scale.x *= 1f + (1f - squash) * 0.6f;
        scale.y *= squash;

        transform.localScale = scale;
        transform.localPosition = baseLocalPosition + Vector3.up * bobOffset;
        transform.localRotation = Quaternion.Euler(0f, 0f, currentLean);
    }
}
