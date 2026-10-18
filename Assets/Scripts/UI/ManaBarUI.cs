using UnityEngine;
using UnityEngine.UI;

public class ManaBarUI : MonoBehaviour
{
    [SerializeField] private PlayerMana playerMana;
    [SerializeField] private Image fillImage;
    [SerializeField] private Text valueText;
    [SerializeField] private float smoothSpeed = 10f;

    private float targetFill = 1f;

    private void Start()
    {
        if (playerMana == null)
        {
            playerMana = FindObjectOfType<PlayerMana>();
        }

        if (playerMana == null)
        {
            return;
        }

        playerMana.ManaChanged += HandleManaChanged;
        HandleManaChanged(playerMana.CurrentMana, playerMana.MaxMana);

        if (fillImage != null)
        {
            fillImage.fillAmount = targetFill;
        }
    }

    private void OnDestroy()
    {
        if (playerMana != null)
        {
            playerMana.ManaChanged -= HandleManaChanged;
        }
    }

    private void HandleManaChanged(int current, int max)
    {
        targetFill = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;

        if (valueText != null)
        {
            valueText.text = $"{current}/{max}";
        }
    }

    private void Update()
    {
        if (fillImage == null)
        {
            return;
        }

        fillImage.fillAmount = Mathf.MoveTowards(fillImage.fillAmount, targetFill, smoothSpeed * Time.unscaledDeltaTime);
    }
}
