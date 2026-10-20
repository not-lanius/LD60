using UnityEngine;
using UnityEngine.UI;

public class SpellInfoUI : MonoBehaviour
{
    public static SpellInfoUI Instance { get; private set; }

    [SerializeField] private TalismanDrawingSystem drawing;
    [SerializeField] private Text descriptionText;
    [SerializeField] private Text hintText;

    [Header("Reference Glyphs")]
    [SerializeField] private GameObject fireGlyph;
    [SerializeField] private GameObject mountainGlyph;
    [SerializeField] private GameObject lightningGlyph;

    private string currentGlyph = string.Empty;
    private string pendingGlyph;
    private bool glyphApplyPending;

    private void Awake()
    {
        Instance = this;
        SetGlyphVisible(fireGlyph, false);
        SetGlyphVisible(mountainGlyph, false);
        SetGlyphVisible(lightningGlyph, false);
    }

    private void Start()
    {
        SetGlyph(string.Empty);
    }

    private void Update()
    {
        if (glyphApplyPending)
        {
            glyphApplyPending = false;
            ApplyGlyph(pendingGlyph);
        }

        if (drawing == null)
        {
            return;
        }

        string last = drawing.LastCharacter;
        if (!string.IsNullOrEmpty(last) && last != currentGlyph)
        {
            SetGlyph(last);
        }
    }

    public void SetGlyph(string glyph)
    {
        if (glyph == currentGlyph)
        {
            return;
        }

        currentGlyph = glyph;

        if (!isActiveAndEnabled)
        {
            ApplyGlyph(glyph);
            return;
        }

        pendingGlyph = glyph;
        glyphApplyPending = true;
    }

    private void ApplyGlyph(string glyph)
    {
        string description;
        string hint;

        switch (glyph)
        {
            case "火":
                description = GameCopy.FireDescription;
                hint = GameCopy.FireHintShort;
                break;

            case "山":
                description = GameCopy.MountainDescription;
                hint = GameCopy.MountainHintShort;
                break;

            case "电":
                description = GameCopy.LightningDescription;
                hint = GameCopy.LightningHintShort;
                break;

            default:
                description = GameCopy.GenericDescription;
                hint = GameCopy.GenericHint;
                break;
        }

        SetText(descriptionText, description);
        SetText(hintText, hint);
    }

    private static void SetText(Text target, string value)
    {
        if (target == null || target.text == value)
        {
            return;
        }

        target.text = value;
    }

    public void UnlockGlyph(string glyph)
    {
        switch (glyph)
        {
            case "火":
                SetGlyphVisible(fireGlyph, true);
                break;

            case "山":
                SetGlyphVisible(mountainGlyph, true);
                break;

            case "电":
                SetGlyphVisible(lightningGlyph, true);
                break;
        }
    }

    private static void SetGlyphVisible(GameObject glyph, bool visible)
    {
        if (glyph != null)
        {
            glyph.SetActive(visible);
        }
    }
}
