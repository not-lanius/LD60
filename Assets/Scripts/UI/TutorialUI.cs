using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class TutorialUI : MonoBehaviour
{
    public static TutorialUI Instance { get; private set; }

    [SerializeField] private CanvasGroup promptGroup;
    [SerializeField] private Text promptText;
    [SerializeField] private CanvasGroup dialogueGroup;
    [SerializeField] private Text dialogueText;
    [SerializeField] private float fadeSpeed = 7f;

    private Coroutine promptRoutine;
    private Coroutine dialogueRoutine;

    private void Awake()
    {
        Instance = this;
        SetAlpha(promptGroup, 0f);
        SetAlpha(dialogueGroup, 0f);
    }

    public void ShowPrompt(string text, float duration)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (promptRoutine != null)
        {
            StopCoroutine(promptRoutine);
        }

        promptText.text = text;
        promptRoutine = StartCoroutine(ShowRoutine(promptGroup, duration));
    }

    public void ShowDialogue(string text, float duration)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (dialogueRoutine != null)
        {
            StopCoroutine(dialogueRoutine);
        }

        dialogueText.text = text;
        dialogueRoutine = StartCoroutine(ShowRoutine(dialogueGroup, duration));
    }

    public void HidePrompt()
    {
        if (promptRoutine != null)
        {
            StopCoroutine(promptRoutine);
            promptRoutine = null;
        }

        SetAlpha(promptGroup, 0f);
    }

    public void HideDialogue()
    {
        if (dialogueRoutine != null)
        {
            StopCoroutine(dialogueRoutine);
            dialogueRoutine = null;
        }

        SetAlpha(dialogueGroup, 0f);
    }

    private IEnumerator ShowRoutine(CanvasGroup group, float duration)
    {
        while (group != null && group.alpha < 1f)
        {
            group.alpha = Mathf.MoveTowards(group.alpha, 1f, fadeSpeed * Time.unscaledDeltaTime);
            yield return null;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        while (group != null && group.alpha > 0f)
        {
            group.alpha = Mathf.MoveTowards(group.alpha, 0f, fadeSpeed * Time.unscaledDeltaTime);
            yield return null;
        }
    }

    private static void SetAlpha(CanvasGroup group, float alpha)
    {
        if (group != null)
        {
            group.alpha = alpha;
        }
    }
}
