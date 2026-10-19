using UnityEngine;
using UnityEngine.UI;

public class RecognitionPanel : MonoBehaviour
{
    [SerializeField] private TalismanDrawingSystem drawing;
    [SerializeField] private Text dataText;
    [SerializeField] private Color successColor = new Color(0.45f, 1f, 0.45f, 1f);
    [SerializeField] private Color failColor = new Color(1f, 0.45f, 0.4f, 1f);

    private void Update()
    {
        if (drawing == null || dataText == null)
        {
            return;
        }

        string character = drawing.LastCharacter;
        bool hasCharacter = !string.IsNullOrEmpty(character);
        bool detected = hasCharacter && drawing.LastNeatness >= drawing.AcceptThreshold;
        string predicted = hasCharacter ? $"{character} ({GetEnglishName(character)})" : "—";
        string state = detected ? "YES" : "NO";
        string stateColor = ColorUtility.ToHtmlStringRGB(detected ? successColor : failColor);

        dataText.text = $"Neatness: {drawing.LastNeatness:P0}\n"
                      + $"Predicted: {predicted}\n"
                      + $"Detected: <color=#{stateColor}>{state}</color>";
    }

    private static string GetEnglishName(string character)
    {
        switch (character)
        {
            case "火":
                return "FIRE";
            case "山":
                return "MOUNTAIN";
            case "电":
                return "LIGHTNING";
            default:
                return "UNKNOWN";
        }
    }
}
