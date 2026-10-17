using UnityEngine;
using UnityEngine.UI;

public class HealthBarUI : MonoBehaviour
{
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private Image fillImage;
    [SerializeField] private Image delayedFillImage;
    [SerializeField] private Image damageVignette;
    [SerializeField] private float delayedFillSpeed = 0.55f;
    [SerializeField] private float vignetteFadeSpeed = 2.5f;
    [SerializeField] private float vignetteAlpha = 0.28f;

    private float targetFill = 1f;
    private float delayedFill = 1f;
    private float currentVignetteAlpha;

    private void Start()
    {
        if (playerHealth != null)
        {
            playerHealth.HealthChanged += HandleHealthChanged;
            HandleHealthChanged(playerHealth.CurrentHealth, playerHealth.MaxHealth);
        }
    }

    private void OnDestroy()
    {
        if (playerHealth != null)
        {
            playerHealth.HealthChanged -= HandleHealthChanged;
        }
    }

    private void HandleHealthChanged(int current, int max)
    {
        float fill = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
        bool lostHealth = fill < targetFill - 0.001f;
        targetFill = fill;

        if (fillImage != null)
        {
            fillImage.fillAmount = fill;
        }

        if (delayedFillImage != null)
        {
            if (!lostHealth)
            {
                delayedFill = fill;
            }

            delayedFillImage.fillAmount = delayedFill;
        }

        if (lostHealth && damageVignette != null)
        {
            currentVignetteAlpha = vignetteAlpha;
            Color color = damageVignette.color;
            color.a = currentVignetteAlpha;
            damageVignette.color = color;
        }
    }

    private void Update()
    {
        if (delayedFillImage != null)
        {
            delayedFill = Mathf.MoveTowards(delayedFill, targetFill, delayedFillSpeed * Time.unscaledDeltaTime);
            delayedFillImage.fillAmount = delayedFill;
        }

        if (damageVignette != null && currentVignetteAlpha > 0f)
        {
            currentVignetteAlpha = Mathf.MoveTowards(currentVignetteAlpha, 0f, vignetteFadeSpeed * Time.unscaledDeltaTime);
            Color color = damageVignette.color;
            color.a = currentVignetteAlpha;
            damageVignette.color = color;
        }
    }
}
