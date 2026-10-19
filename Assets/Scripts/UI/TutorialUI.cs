using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class TutorialUI : MonoBehaviour
{
    public static TutorialUI Instance { get; private set; }

    [Header("References")]
    [SerializeField] private CanvasGroup promptGroup;
    [SerializeField] private Text promptText;
    [SerializeField] private CanvasGroup dialogueGroup;
    [SerializeField] private Text dialogueText;
    [SerializeField] private float fadeSpeed = 7f;

    [Header("Tutorial Sequence")]
    [SerializeField] private Color dimColor = new Color(0f, 0f, 0f, 0.62f);
    [SerializeField] private float dimFadeDuration = 0.25f;
    [SerializeField] private float inputGuardRealtime = 0.22f;
    [SerializeField] private string continueHint = "[Space] Continue";
    [SerializeField] private string resumeHint = "[Space] Resume";

    private Coroutine promptRoutine;
    private Coroutine dialogueRoutine;
    private Coroutine sequenceRoutine;
    private Image dimImage;
    private float savedTimeScale = 1f;

    public static bool BlocksGameplay { get; private set; }

    private void Awake()
    {
        Instance = this;
        SetAlpha(promptGroup, 0f);
        SetAlpha(dialogueGroup, 0f);
        EnsureDimOverlay();
        SetDimAlpha(0f);
    }

    private void OnDisable()
    {
        if (sequenceRoutine != null)
        {
            StopCoroutine(sequenceRoutine);
            sequenceRoutine = null;
        }

        if (BlocksGameplay)
        {
            BlocksGameplay = false;
            Time.timeScale = savedTimeScale > 0.001f ? savedTimeScale : 1f;
        }

        SetDimAlpha(0f);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void ShowPrompt(string text, float duration)
    {
        if (string.IsNullOrEmpty(text) || sequenceRoutine != null)
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
        if (string.IsNullOrEmpty(text) || sequenceRoutine != null)
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

    public void PlaySequence(string[] lines)
    {
        if (lines == null || lines.Length == 0 || sequenceRoutine != null)
        {
            return;
        }

        sequenceRoutine = StartCoroutine(SequenceRoutine(lines));
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

    private IEnumerator SequenceRoutine(string[] lines)
    {
        BlocksGameplay = true;
        savedTimeScale = Time.timeScale > 0.001f ? Time.timeScale : 1f;
        Time.timeScale = 0f;

        HidePrompt();
        HideDialogue();

        EnsureDimOverlay();
        float startAlpha = dimImage != null ? dimImage.color.a : 0f;
        float fade = 0f;
        while (fade < dimFadeDuration)
        {
            fade += Time.unscaledDeltaTime;
            SetDimAlpha(Mathf.Lerp(startAlpha, dimColor.a, Mathf.Clamp01(fade / dimFadeDuration)));
            yield return null;
        }

        SetDimAlpha(dimColor.a);

        while (dialogueGroup != null && dialogueGroup.alpha < 1f)
        {
            dialogueGroup.alpha = Mathf.MoveTowards(dialogueGroup.alpha, 1f, fadeSpeed * Time.unscaledDeltaTime);
            yield return null;
        }

        for (int i = 0; i < lines.Length; i++)
        {
            bool lastLine = i == lines.Length - 1;
            if (dialogueText != null)
            {
                string hint = lastLine ? resumeHint : continueHint;
                dialogueText.text = string.IsNullOrEmpty(hint)
                    ? lines[i]
                    : lines[i] + "\n\n<size=18><color=#E6C15A>" + hint + "</color></size>";
            }

            float guard = 0f;
            while (guard < inputGuardRealtime)
            {
                guard += Time.unscaledDeltaTime;
                yield return null;
            }

            while (!Input.GetKeyDown(KeyCode.Space))
            {
                yield return null;
            }

            yield return null;
        }

        float outFade = 0f;
        while (outFade < dimFadeDuration)
        {
            outFade += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(outFade / dimFadeDuration);
            SetDimAlpha(Mathf.Lerp(dimColor.a, 0f, t));

            if (dialogueGroup != null)
            {
                dialogueGroup.alpha = 1f - t;
            }

            yield return null;
        }

        EndSequence();
    }

    private void EndSequence()
    {
        sequenceRoutine = null;
        BlocksGameplay = false;
        Time.timeScale = savedTimeScale > 0.001f ? savedTimeScale : 1f;
        SetDimAlpha(0f);
        SetAlpha(dialogueGroup, 0f);

        if (dialogueText != null)
        {
            dialogueText.text = string.Empty;
        }
    }

    private void EnsureDimOverlay()
    {
        if (dimImage != null)
        {
            return;
        }

        GameObject overlay = new GameObject("TutorialDim", typeof(RectTransform), typeof(Image));
        RectTransform rect = overlay.GetComponent<RectTransform>();
        rect.SetParent(transform, false);

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        dimImage = overlay.GetComponent<Image>();
        dimImage.color = new Color(dimColor.r, dimColor.g, dimColor.b, 0f);
        dimImage.raycastTarget = false;

        overlay.transform.SetAsFirstSibling();
    }

    private void SetDimAlpha(float alpha)
    {
        if (dimImage == null)
        {
            return;
        }

        Color color = dimImage.color;
        color.a = Mathf.Clamp01(alpha);
        dimImage.color = color;
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
