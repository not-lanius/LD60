using UnityEngine;

public class PlayerSwordVisual : MonoBehaviour
{
    [Header("Idle")]
    [SerializeField] private Vector2 handOffset = new Vector2(0.55f, 0.22f);
    [SerializeField] private float idleAngle = -40f;

    [Header("Swing")]
    [SerializeField] private float windupAngle = 70f;
    [SerializeField] private float followThroughAngle = -65f;
    [SerializeField] private float windupPortion = 0.25f;
    [SerializeField] private float slashPortion = 0.4f;
    [SerializeField] private float swingDuration = 0.28f;

    private PlayerController2D controller;
    private float swingTimer = -1f;
    private int facing = 1;
    private float currentAngle;

    private void Awake()
    {
        controller = GetComponentInParent<PlayerController2D>();
        currentAngle = idleAngle;
        ApplyPose(idleAngle);
    }

    public void PlaySwing()
    {
        swingTimer = 0f;
    }

    private void Update()
    {
        if (controller != null)
        {
            facing = controller.Facing;
        }

        float dt = Time.deltaTime;
        if (swingTimer >= 0f)
        {
            swingTimer += dt;
            float t = Mathf.Clamp01(swingTimer / Mathf.Max(0.01f, swingDuration));
            currentAngle = EvaluateSwingAngle(t);

            if (t >= 1f)
            {
                swingTimer = -1f;
                currentAngle = idleAngle;
            }
        }
        else
        {
            currentAngle = Mathf.Lerp(currentAngle, idleAngle, 1f - Mathf.Exp(-14f * dt));
        }

        ApplyPose(currentAngle);
    }

    private float EvaluateSwingAngle(float t)
    {
        float slashEnd = Mathf.Clamp01(windupPortion + slashPortion);
        if (t < windupPortion)
        {
            return Mathf.Lerp(idleAngle, windupAngle, SmoothStep(t / windupPortion));
        }

        if (t < slashEnd)
        {
            return Mathf.Lerp(windupAngle, followThroughAngle, SmoothStep((t - windupPortion) / Mathf.Max(0.01f, slashPortion)));
        }

        return Mathf.Lerp(followThroughAngle, idleAngle, SmoothStep((t - slashEnd) / Mathf.Max(0.01f, 1f - slashEnd)));
    }

    private static float SmoothStep(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private void ApplyPose(float angle)
    {
        transform.localPosition = new Vector3(handOffset.x * facing, handOffset.y, 0f);
        transform.localScale = new Vector3(facing, 1f, 1f);
        transform.localRotation = Quaternion.Euler(0f, 0f, angle);
    }
}
