using UnityEngine;
using UnityEngine.UI;

public class HudFontBootstrap : MonoBehaviour
{
    [SerializeField] private string[] preferredFonts = { "Microsoft YaHei", "SimHei", "Noto Sans CJK SC", "Arial" };
    [SerializeField] private int fontSize = 42;

    private void Awake()
    {
        Font font = Font.CreateDynamicFontFromOSFont(preferredFonts, fontSize);
        if (font == null)
        {
            return;
        }

        Text[] texts = GetComponentsInChildren<Text>(true);
        foreach (Text text in texts)
        {
            text.font = font;
        }
    }
}
