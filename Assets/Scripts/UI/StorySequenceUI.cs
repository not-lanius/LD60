using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StorySequenceUI : MonoBehaviour
{
    public static StorySequenceUI Instance { get; private set; }

    [Header("References")]
    [SerializeField] private CanvasGroup group;
    [SerializeField] private Image background;
    [SerializeField] private Text bodyText;
    [SerializeField] private Text skipHint;

    [Header("Flow")]
    [SerializeField] private bool playOpeningOnStart = true;
    [SerializeField] private float charactersPerSecond = 32f;
    [SerializeField] private float lineHold = 2.5f;
    [SerializeField] private float fadeDuration = 0.45f;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color finalColor = new Color(0.95f, 0.18f, 0.12f);

    [Header("Ending")]
    [SerializeField] private string menuSceneName = "menu";
    [SerializeField] private float returnToMenuDelay = 0.4f;
    [SerializeField] private AudioClip endingBiteClip;
    [SerializeField] private float endingBiteVolume = 0.85f;

    private Coroutine routine;
    private bool playing;
    private float savedTimeScale = 1f;
    private Vector2 bodyHome;
    private AudioSource sequenceAudio;
    private static AudioClip generatedBiteClip;

    public bool IsPlaying => playing;

    private void Awake()
    {
        Instance = this;

        if (group == null)
        {
            group = GetComponent<CanvasGroup>();
        }

        if (bodyText != null)
        {
            bodyHome = bodyText.rectTransform.anchoredPosition;
        }

        sequenceAudio = GetComponent<AudioSource>();
        if (sequenceAudio == null)
        {
            sequenceAudio = gameObject.AddComponent<AudioSource>();
            sequenceAudio.playOnAwake = false;
            sequenceAudio.spatialBlend = 0f;
        }

        if (group != null)
        {
            group.alpha = 0f;
        }

        if (background != null)
        {
            background.enabled = false;
        }

        if (bodyText != null)
        {
            bodyText.text = string.Empty;
        }

        if (skipHint != null)
        {
            skipHint.text = string.Empty;
        }
    }

    private void Start()
    {
        if (playOpeningOnStart)
        {
            PlayOpening();
        }
    }

    public void PlayOpening()
    {
        Play(GameCopy.OpeningLines, true);
    }

    public void PlayEnding()
    {
        Play(GameCopy.EndingLines, true, true);
    }

    public void Play(string[] lines, bool freezeGame)
    {
        Play(lines, freezeGame, false);
    }

    private void Play(string[] lines, bool freezeGame, bool returnToMenu)
    {
        if (lines == null || lines.Length == 0)
        {
            return;
        }

        if (routine != null)
        {
            StopCoroutine(routine);
        }

        routine = StartCoroutine(PlayRoutine(lines, freezeGame, returnToMenu));
    }

    public void FinishImmediately()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        EndSequence(true);
    }

    private IEnumerator PlayRoutine(string[] lines, bool freezeGame, bool returnToMenu)
    {
        playing = true;
        savedTimeScale = Time.timeScale;

        if (freezeGame)
        {
            Time.timeScale = 0f;
        }

        if (group != null)
        {
            group.alpha = 0f;
        }

        if (background != null)
        {
            background.enabled = true;
        }

        if (bodyText != null)
        {
            bodyText.text = string.Empty;
            bodyText.rectTransform.anchoredPosition = bodyHome;
        }

        if (skipHint != null)
        {
            skipHint.text = "Press any key to continue";
        }

        float fade = 0f;
        while (fade < fadeDuration)
        {
            fade += Time.unscaledDeltaTime;
            if (group != null)
            {
                group.alpha = Mathf.Clamp01(fade / fadeDuration);
            }
            yield return null;
        }

        if (group != null)
        {
            group.alpha = 1f;
        }

        yield return new WaitForSecondsRealtime(0.25f);

        if (returnToMenu)
        {
            PlayEndingBite();
        }

        for (int i = 0; i < lines.Length; i++)
        {
            bool lastLine = i == lines.Length - 1;
            string line = lines[i];

            if (bodyText != null)
            {
                bodyText.color = lastLine ? finalColor : normalColor;
                bodyText.text = string.Empty;
                bodyText.rectTransform.anchoredPosition = bodyHome;
            }

            bool reveal = false;
            for (int c = 0; c < line.Length; c++)
            {
                if (Input.anyKeyDown)
                {
                    reveal = true;
                }

                if (reveal)
                {
                    break;
                }

                if (bodyText != null)
                {
                    bodyText.text = line.Substring(0, c + 1);
                }

                if (c % 3 == 0 && AudioDirector.Instance != null)
                {
                    AudioDirector.Instance.PlayWriting();
                }

                yield return new WaitForSecondsRealtime(1f / Mathf.Max(1f, charactersPerSecond));
            }

            if (bodyText != null)
            {
                bodyText.text = line;
            }

            float elapsed = 0f;
            while (elapsed < lineHold)
            {
                elapsed += Time.unscaledDeltaTime;
                if (Input.anyKeyDown)
                {
                    break;
                }
                yield return null;
            }

            if (lastLine)
            {
                if (AudioDirector.Instance != null)
                {
                    AudioDirector.Instance.PlayMelee();
                }

                yield return ShakeText(0.35f);
            }

            yield return null;
        }

        yield return new WaitForSecondsRealtime(0.15f);

        float outFade = 0f;
        while (outFade < fadeDuration)
        {
            outFade += Time.unscaledDeltaTime;
            if (group != null)
            {
                group.alpha = 1f - Mathf.Clamp01(outFade / fadeDuration);
            }
            yield return null;
        }

        EndSequence(freezeGame);

        if (returnToMenu)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, returnToMenuDelay));

            if (AudioDirector.Instance != null)
            {
                AudioDirector.Instance.SetBulletTime(false);
                AudioDirector.Instance.StopGalleryAmbience();
            }

            Time.timeScale = 1f;

            if (!string.IsNullOrWhiteSpace(menuSceneName))
            {
                SceneManager.LoadScene(menuSceneName);
            }
        }
    }

    private void PlayEndingBite()
    {
        if (sequenceAudio == null)
        {
            return;
        }

        AudioClip clip = endingBiteClip != null ? endingBiteClip : GetGeneratedBiteClip();
        if (clip != null)
        {
            sequenceAudio.PlayOneShot(clip, endingBiteVolume);
        }
    }

    private static AudioClip GetGeneratedBiteClip()
    {
        if (generatedBiteClip != null)
        {
            return generatedBiteClip;
        }

        const int sampleRate = 44100;
        int sampleCount = Mathf.RoundToInt(sampleRate * 0.22f);
        float[] samples = new float[sampleCount];
        System.Random random = new System.Random(60);

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float noise = (float)(random.NextDouble() * 2.0 - 1.0);
            float crack = Mathf.Sin(2f * Mathf.PI * 1500f * t) * 0.45f
                        + Mathf.Sin(2f * Mathf.PI * 2400f * t) * 0.25f;
            float envelope = Mathf.Exp(-t * 22f);
            float snap = i < sampleRate * 0.012f ? 0.8f : 0f;
            samples[i] = ((noise * 0.75f + crack) * envelope + noise * snap) * 0.6f;
        }

        generatedBiteClip = AudioClip.Create("EndingBitePlaceholder", sampleCount, 1, sampleRate, false);
        generatedBiteClip.SetData(samples, 0);
        return generatedBiteClip;
    }

    private IEnumerator ShakeText(float duration)
    {
        if (bodyText == null)
        {
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            bodyText.rectTransform.anchoredPosition = bodyHome + Random.insideUnitCircle * 7f;
            yield return null;
        }

        bodyText.rectTransform.anchoredPosition = bodyHome;
    }

    private void EndSequence(bool restoreTime)
    {
        playing = false;
        routine = null;

        if (background != null)
        {
            background.enabled = false;
        }

        if (group != null)
        {
            group.alpha = 0f;
        }

        if (bodyText != null)
        {
            bodyText.text = string.Empty;
            bodyText.rectTransform.anchoredPosition = bodyHome;
        }

        if (skipHint != null)
        {
            skipHint.text = string.Empty;
        }

        if (restoreTime)
        {
            Time.timeScale = savedTimeScale > 0.001f ? savedTimeScale : 1f;
        }
    }
}
