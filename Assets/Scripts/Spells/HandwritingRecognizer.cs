using System.Collections.Generic;
using UnityEngine;

public struct HandwritingResult
{
    public string Character;
    public float Score;
    public int StrokeCount;
    public float TotalLength;
}

public static class HandwritingRecognizer
{
    private const int Resolution = 64;
    private const float InkRadius = 1.6f;
    public static float PrefixThreshold { get; set; } = 0.6f;

    private static readonly Dictionary<string, List<List<Vector2>>> Templates = new Dictionary<string, List<List<Vector2>>>
    {
        {
            "火",
            new List<List<Vector2>>
            {
                new List<Vector2> { new Vector2(0.18f, 0.68f), new Vector2(0.28f, 0.58f) },
                new List<Vector2> { new Vector2(0.82f, 0.68f), new Vector2(0.72f, 0.58f) },
                new List<Vector2> { new Vector2(0.50f, 0.92f), new Vector2(0.47f, 0.60f), new Vector2(0.22f, 0.08f) },
                new List<Vector2> { new Vector2(0.50f, 0.92f), new Vector2(0.53f, 0.60f), new Vector2(0.78f, 0.08f) }
            }
        },
        {
            "山",
            new List<List<Vector2>>
            {
                new List<Vector2> { new Vector2(0.50f, 0.90f), new Vector2(0.50f, 0.10f) },
                new List<Vector2> { new Vector2(0.18f, 0.80f), new Vector2(0.18f, 0.10f), new Vector2(0.82f, 0.10f) },
                new List<Vector2> { new Vector2(0.82f, 0.80f), new Vector2(0.82f, 0.10f) }
            }
        },
        {
            "电",
            new List<List<Vector2>>
            {
                new List<Vector2> { new Vector2(0.28f, 0.88f), new Vector2(0.28f, 0.12f) },
                new List<Vector2> { new Vector2(0.28f, 0.88f), new Vector2(0.78f, 0.88f), new Vector2(0.78f, 0.12f) },
                new List<Vector2> { new Vector2(0.28f, 0.55f), new Vector2(0.78f, 0.55f) },
                new List<Vector2> { new Vector2(0.28f, 0.12f), new Vector2(0.78f, 0.12f) },
                new List<Vector2> { new Vector2(0.53f, 0.82f), new Vector2(0.53f, 0.02f), new Vector2(0.74f, 0.02f) }
            }
        }
    };

    public static IReadOnlyList<List<Vector2>> GetTemplate(string character)
    {
        return Templates.TryGetValue(character, out List<List<Vector2>> template) ? template : null;
    }

    public static HandwritingResult Recognize(IReadOnlyList<List<Vector2>> strokes, float aspect = 1f)
    {
        HandwritingResult result = new HandwritingResult
        {
            Character = string.Empty,
            Score = 0f,
            StrokeCount = strokes != null ? strokes.Count : 0,
            TotalLength = strokes != null ? ComputeLength(strokes) : 0f
        };

        if (strokes == null || strokes.Count < 2)
        {
            return result;
        }

        GetBounds(strokes, out Vector2 min, out Vector2 max);
        Vector2 size = max - min;
        if (Mathf.Max(size.x, size.y) < 0.05f)
        {
            return result;
        }

        List<List<Vector2>> normalized = Normalize(strokes, min, max);
        bool[] drawnMask = Rasterize(normalized);
        int drawnCount = CountInk(drawnMask);
        if (drawnCount == 0)
        {
            return result;
        }

        bool[] drawnDilated = Dilate(drawnMask, 1);
        float bestScore = 0f;
        string bestCharacter = string.Empty;

        foreach (KeyValuePair<string, List<List<Vector2>>> pair in Templates)
        {
            GetBounds(pair.Value, out Vector2 templateMin, out Vector2 templateMax);
            List<List<Vector2>> templateNormalized = Normalize(pair.Value, templateMin, templateMax);
            bool[] templateMask = Rasterize(templateNormalized);
            bool[] templateDilated = Dilate(templateMask, 1);

            int templateCount = CountInk(templateMask);
            if (templateCount == 0)
            {
                continue;
            }

            int coveredTemplate = CountOverlap(templateMask, drawnDilated);
            int coveredDrawing = CountOverlap(drawnMask, templateDilated);

            float recall = (float)coveredTemplate / templateCount;
            float precision = (float)coveredDrawing / drawnCount;
            float f1 = precision + recall > 0f ? 2f * precision * recall / (precision + recall) : 0f;

            int expectedStrokes = pair.Value.Count;
            float strokePenalty = Mathf.Clamp01(1f - 0.06f * Mathf.Abs(result.StrokeCount - expectedStrokes));
            f1 *= strokePenalty;

            if (f1 > bestScore)
            {
                bestScore = f1;
                bestCharacter = pair.Key;
            }
        }

        result.Score = Mathf.Clamp01(bestScore);
        result.Character = result.Score >= 0.3f ? bestCharacter : string.Empty;
        return result;
    }

    public static HandwritingResult IdentifyPrefix(IReadOnlyList<List<Vector2>> strokes, float aspect = 1f)
    {
        HandwritingResult result = new HandwritingResult
        {
            Character = string.Empty,
            Score = 0f,
            StrokeCount = strokes != null ? strokes.Count : 0
        };

        int drawnStrokes = strokes != null ? strokes.Count : 0;
        if (drawnStrokes == 0)
        {
            return result;
        }

        List<List<Vector2>> prefix = new List<List<Vector2>>(drawnStrokes);
        for (int i = 0; i < drawnStrokes; i++)
        {
            prefix.Add(strokes[i]);
        }

        GetBounds(prefix, out Vector2 min, out Vector2 max);
        if (Mathf.Max(max.x - min.x, max.y - min.y) < 0.05f)
        {
            return result;
        }

        List<List<Vector2>> normalized = Normalize(prefix, min, max);
        bool[] drawnMask = Rasterize(normalized);
        int drawnCount = CountInk(drawnMask);
        if (drawnCount == 0)
        {
            return result;
        }

        bool[] drawnDilated = Dilate(drawnMask, 1);
        float bestScore = 0f;
        string bestCharacter = string.Empty;
        int[] indices = new int[drawnStrokes];

        foreach (KeyValuePair<string, List<List<Vector2>>> pair in Templates)
        {
            if (pair.Value.Count < drawnStrokes)
            {
                continue;
            }

            int budget = 500;
            float bestForCharacter = 0f;
            EnumerateCombinations(pair.Value, drawnStrokes, 0, 0, indices, ref bestForCharacter, ref budget, drawnMask, drawnCount, drawnDilated);

            if (bestForCharacter > bestScore)
            {
                bestScore = bestForCharacter;
                bestCharacter = pair.Key;
            }
        }

        result.Score = Mathf.Clamp01(bestScore);
        result.Character = result.Score >= PrefixThreshold ? bestCharacter : string.Empty;
        return result;
    }

    private static void EnumerateCombinations(List<List<Vector2>> template, int length, int start, int depth, int[] indices, ref float best, ref int budget, bool[] drawnMask, int drawnCount, bool[] drawnDilated)
    {
        if (budget <= 0)
        {
            return;
        }

        if (depth == length)
        {
            budget--;
            List<List<Vector2>> subset = new List<List<Vector2>>(length);
            for (int i = 0; i < length; i++)
            {
                subset.Add(template[indices[i]]);
            }

            float score = ComputeMatch(subset, drawnMask, drawnCount, drawnDilated);
            if (score > best)
            {
                best = score;
            }

            return;
        }

        for (int i = start; i <= template.Count - (length - depth); i++)
        {
            indices[depth] = i;
            EnumerateCombinations(template, length, i + 1, depth + 1, indices, ref best, ref budget, drawnMask, drawnCount, drawnDilated);
        }
    }
    public static int GetStrokeCount(string character)
    {
        return Templates.TryGetValue(character, out List<List<Vector2>> template) ? template.Count : 0;
    }

    private static float ComputeMatch(IReadOnlyList<List<Vector2>> candidate, bool[] drawnMask, int drawnCount, bool[] drawnDilated)
    {
        GetBounds(candidate, out Vector2 min, out Vector2 max);
        if (Mathf.Max(max.x - min.x, max.y - min.y) < 0.05f)
        {
            return 0f;
        }

        List<List<Vector2>> normalized = Normalize(candidate, min, max);
        bool[] mask = Rasterize(normalized);
        int count = CountInk(mask);
        if (count == 0)
        {
            return 0f;
        }

        bool[] dilated = Dilate(mask, 1);
        int coveredTemplate = CountOverlap(mask, drawnDilated);
        int coveredDrawing = CountOverlap(drawnMask, dilated);
        float recall = (float)coveredTemplate / count;
        float precision = (float)coveredDrawing / drawnCount;
        return precision + recall > 0f ? 2f * precision * recall / (precision + recall) : 0f;
    }
    private static List<List<Vector2>> ApplyAspect(IReadOnlyList<List<Vector2>> strokes, float aspect)
    {
        List<List<Vector2>> corrected = new List<List<Vector2>>(strokes.Count);

        foreach (List<Vector2> stroke in strokes)
        {
            List<Vector2> copy = new List<Vector2>(stroke.Count);
            foreach (Vector2 point in stroke)
            {
                copy.Add(new Vector2(point.x * aspect, point.y));
            }

            corrected.Add(copy);
        }

        return corrected;
    }
    private static float ComputeLength(IReadOnlyList<List<Vector2>> strokes)
    {
        float length = 0f;
        for (int i = 0; i < strokes.Count; i++)
        {
            List<Vector2> stroke = strokes[i];
            for (int j = 1; j < stroke.Count; j++)
            {
                length += Vector2.Distance(stroke[j - 1], stroke[j]);
            }
        }

        return length;
    }

    private static void GetBounds(IReadOnlyList<List<Vector2>> strokes, out Vector2 min, out Vector2 max)
    {
        min = new Vector2(float.MaxValue, float.MaxValue);
        max = new Vector2(float.MinValue, float.MinValue);

        foreach (List<Vector2> stroke in strokes)
        {
            foreach (Vector2 point in stroke)
            {
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
        }
    }

    private static List<List<Vector2>> Normalize(IReadOnlyList<List<Vector2>> strokes, Vector2 min, Vector2 max)
    {
        Vector2 center = (min + max) * 0.5f;
        float extent = Mathf.Max(max.x - min.x, max.y - min.y);
        float scale = extent > 0.0001f ? 0.84f / extent : 1f;
        List<List<Vector2>> normalized = new List<List<Vector2>>(strokes.Count);

        foreach (List<Vector2> stroke in strokes)
        {
            List<Vector2> copy = new List<Vector2>(stroke.Count);
            foreach (Vector2 point in stroke)
            {
                copy.Add((point - center) * scale + new Vector2(0.5f, 0.5f));
            }

            normalized.Add(copy);
        }

        return normalized;
    }

    private static bool[] Rasterize(IReadOnlyList<List<Vector2>> strokes)
    {
        bool[] mask = new bool[Resolution * Resolution];
        foreach (List<Vector2> stroke in strokes)
        {
            if (stroke.Count == 1)
            {
                PaintDot(mask, stroke[0]);
                continue;
            }

            for (int i = 1; i < stroke.Count; i++)
            {
                PaintSegment(mask, stroke[i - 1], stroke[i]);
            }
        }

        return mask;
    }

    private static void PaintDot(bool[] mask, Vector2 point)
    {
        int px = Mathf.Clamp(Mathf.RoundToInt(point.x * (Resolution - 1)), 0, Resolution - 1);
        int py = Mathf.Clamp(Mathf.RoundToInt(point.y * (Resolution - 1)), 0, Resolution - 1);
        int radius = Mathf.CeilToInt(InkRadius);

        for (int y = py - radius; y <= py + radius; y++)
        {
            if (y < 0 || y >= Resolution)
            {
                continue;
            }

            for (int x = px - radius; x <= px + radius; x++)
            {
                if (x < 0 || x >= Resolution)
                {
                    continue;
                }

                if ((x - px) * (x - px) + (y - py) * (y - py) <= InkRadius * InkRadius)
                {
                    mask[y * Resolution + x] = true;
                }
            }
        }
    }

    private static void PaintSegment(bool[] mask, Vector2 from, Vector2 to)
    {
        Vector2 a = from * (Resolution - 1);
        Vector2 b = to * (Resolution - 1);
        float radius = InkRadius;

        int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - radius));
        int maxX = Mathf.Min(Resolution - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + radius));
        int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - radius));
        int maxY = Mathf.Min(Resolution - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + radius));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 point = new Vector2(x, y);
                float distance = DistanceToSegment(point, a, b);
                if (distance <= radius)
                {
                    mask[y * Resolution + x] = true;
                }
            }
        }
    }

    private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lengthSquared = ab.sqrMagnitude;
        if (lengthSquared <= 0.0001f)
        {
            return Vector2.Distance(point, a);
        }

        float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSquared);
        Vector2 closest = a + ab * t;
        return Vector2.Distance(point, closest);
    }

    private static bool[] Dilate(bool[] mask, int radius)
    {
        bool[] dilated = new bool[mask.Length];
        for (int y = 0; y < Resolution; y++)
        {
            for (int x = 0; x < Resolution; x++)
            {
                if (!mask[y * Resolution + x])
                {
                    continue;
                }

                for (int dy = -radius; dy <= radius; dy++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        int nx = x + dx;
                        int ny = y + dy;
                        if (nx >= 0 && nx < Resolution && ny >= 0 && ny < Resolution)
                        {
                            dilated[ny * Resolution + nx] = true;
                        }
                    }
                }
            }
        }

        return dilated;
    }

    private static int CountInk(bool[] mask)
    {
        int count = 0;
        for (int i = 0; i < mask.Length; i++)
        {
            if (mask[i])
            {
                count++;
            }
        }

        return count;
    }

    private static int CountOverlap(bool[] mask, bool[] other)
    {
        int count = 0;
        for (int i = 0; i < mask.Length; i++)
        {
            if (mask[i] && other[i])
            {
                count++;
            }
        }

        return count;
    }
}
