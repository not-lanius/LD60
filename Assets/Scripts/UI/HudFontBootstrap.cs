using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HudFontBootstrap : MonoBehaviour
{
    [SerializeField] private string[] preferredFonts = { "Microsoft YaHei", "SimHei", "Noto Sans CJK SC", "Arial" };
    [SerializeField] private int fontSize = 42;

    private void Awake()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return;
#else
        Font font = Font.CreateDynamicFontFromOSFont(preferredFonts, fontSize);
        if (font == null)
        {
            return;
        }

        Text[] texts = GetComponentsInChildren<Text>(true);
        foreach (Text text in texts)
        {
            float originalCenter = GetVisualCenterY(text);
            text.font = font;
            float newCenter = GetVisualCenterY(text);

            if (Mathf.Abs(originalCenter - newCenter) < 0.01f)
            {
                continue;
            }

            Vector2 position = text.rectTransform.anchoredPosition;
            position.y += originalCenter - newCenter;
            text.rectTransform.anchoredPosition = position;
            text.SetAllDirty();
        }
#endif
    }

    private static float GetVisualCenterY(Text text)
    {
        if (text == null || string.IsNullOrEmpty(text.text))
        {
            return 0f;
        }

        TextGenerationSettings settings = text.GetGenerationSettings(text.rectTransform.rect.size);
        TextGenerator generator = text.cachedTextGeneratorForLayout;
        generator.Populate(text.text, settings);
        IList<UIVertex> vertices = generator.verts;
        if (vertices.Count == 0)
        {
            return 0f;
        }

        float minY = float.MaxValue;
        float maxY = float.MinValue;
        for (int i = 0; i < vertices.Count; i++)
        {
            float y = vertices[i].position.y;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }

        return (minY + maxY) * 0.5f;
    }
}
