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

        switch (glyph)
        {
            case "火":
                descriptionText.text = GameCopy.FireDescription;
                hintText.text = GameCopy.FireHintShort;
                break;

            case "山":
                descriptionText.text = GameCopy.MountainDescription;
                hintText.text = GameCopy.MountainHintShort;
                break;

            case "电":
                descriptionText.text = GameCopy.LightningDescription;
                hintText.text = GameCopy.LightningHintShort;
                break;

            default:
                descriptionText.text = GameCopy.GenericDescription;
                hintText.text = GameCopy.GenericHint;
                break;
        }
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
