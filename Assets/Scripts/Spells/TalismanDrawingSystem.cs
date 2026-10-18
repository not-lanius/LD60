using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TalismanDrawingSystem : MonoBehaviour
{
    [Header("Drawing")]
    [SerializeField] private RectTransform drawingRect;
    [SerializeField] private RawImage inkDisplay;
    [SerializeField] private int textureWidth = 256;
    [SerializeField] private float brushRadiusPixels = 5f;

    [Header("Casting")]
    [SerializeField] private Transform player;
    [SerializeField] private GameObject fireballPrefab;
    [SerializeField] private float acceptThreshold = 0.6f;
    [SerializeField] private float fireballSpawnDistance = 0.95f;
    [SerializeField] private float fireballSpawnHeight = 0.3f;
    [SerializeField] private GameObject rockWallPrefab;
    [SerializeField] private float wallSpawnDistance = 1.25f;
    [SerializeField] private LayerMask wallGroundMask;
    [SerializeField] private GameObject lightningPrefab;

    [Header("Mana Cost")]
    [SerializeField] private PlayerMana playerMana;
    [SerializeField] private int fireballManaCost = 20;
    [SerializeField] private int rockWallManaCost = 30;
    [SerializeField] private int lightningManaCost = 40;

    [Header("Prediction")]
    [SerializeField] private float predictionThreshold = 0.6f;

    [Header("Bullet Time")]
    [SerializeField] private float bulletTimeScale = 0.25f;
    [SerializeField] private float bulletTimeGrace = 0.2f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugKeys = true;

    private Texture2D inkTexture;
    private Color32[] inkPixels;
    private int texWidth;
    private int texHeight;
    private readonly List<List<Vector2>> strokes = new List<List<Vector2>>();
    private List<Vector2> currentStroke;
    private bool isDrawing;
    private float firstStrokeTime;
    private float lastStrokeEndTime;
    private float lastDrawActivityTime;
    private float savedTimeScale = 1f;
    private bool bulletTimeActive;
    private int expectedStrokeCount;
    private string expectedCharacter = string.Empty;
    private float lastPrefixScore;

    public string LastCharacter { get; private set; } = string.Empty;
    public float LastNeatness { get; private set; }
    public int LastStrokeCount { get; private set; }
    public float LastTotalLength { get; private set; }
    public float LastDuration { get; private set; }
    public string Status { get; private set; } = "等待绘制";
    public bool IsBulletTimeActive => bulletTimeActive;
    public float AcceptThreshold => acceptThreshold;

    private static readonly Color32 InkColor = new Color32(28, 18, 10, 255);
    private static readonly Color32 ClearColor = new Color32(0, 0, 0, 0);

    private void Start()
    {
        HandwritingRecognizer.PrefixThreshold = predictionThreshold;
        CreateInkTexture();

        if (playerMana == null)
        {
            playerMana = player != null ? player.GetComponent<PlayerMana>() : FindObjectOfType<PlayerMana>();
        }
    }

    private void OnDisable()
    {
        RestoreTimeScale();
    }

    private void OnDestroy()
    {
        RestoreTimeScale();

        if (inkTexture != null)
        {
            Destroy(inkTexture);
        }
    }

    private void CreateInkTexture()
    {
        texWidth = Mathf.Max(64, textureWidth);

        float aspect = 2f;
        if (drawingRect != null && drawingRect.rect.width > 1f && drawingRect.rect.height > 1f)
        {
            aspect = drawingRect.rect.height / drawingRect.rect.width;
        }

        texHeight = Mathf.Clamp(Mathf.RoundToInt(texWidth * aspect), 64, 2048);

        if (inkTexture != null)
        {
            Destroy(inkTexture);
        }

        inkTexture = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false);
        inkTexture.wrapMode = TextureWrapMode.Clamp;
        inkTexture.filterMode = FilterMode.Bilinear;
        inkPixels = new Color32[texWidth * texHeight];
        ClearPixels();

        if (inkDisplay != null)
        {
            inkDisplay.texture = inkTexture;
        }
    }

    private void Update()
    {
        if (drawingRect == null || inkDisplay == null || inkTexture == null)
        {
            return;
        }

        HandlePointer();
        UpdateBulletTime();

        if (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Return))
        {
            CastCurrentTalisman();
        }

        if (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Delete))
        {
            ClearTalisman(true);
        }

        if (enableDebugKeys)
        {
            if (Input.GetKeyDown(KeyCode.T))
            {
                LoadTemplateForDebug("火");
            }

            if (Input.GetKeyDown(KeyCode.G))
            {
                LoadTemplateForDebug("山");
            }

            if (Input.GetKeyDown(KeyCode.H))
            {
                LoadTemplateForDebug("电");
            }

            if (Input.GetKeyDown(KeyCode.Y))
            {
                LoadScribbleForDebug();
            }

            if (Input.GetKeyDown(KeyCode.LeftBracket))
            {
                AdjustAcceptThreshold(-0.02f);
            }

            if (Input.GetKeyDown(KeyCode.RightBracket))
            {
                AdjustAcceptThreshold(0.02f);
            }

            if (Input.GetKeyDown(KeyCode.Backslash))
            {
                acceptThreshold = 0.6f;
                Status = $"施法阈值重置: {acceptThreshold:P0}";
            }
        }
    }

    public string GetDebugText()
    {
        string recognized = string.IsNullOrEmpty(LastCharacter) ? "—" : LastCharacter;
        string bulletTime = bulletTimeActive ? $"开 {Time.timeScale:0.00}x" : "关";
        string judge = string.IsNullOrEmpty(LastCharacter) ? "无法术" : (LastNeatness >= acceptThreshold ? "可施法" : "工整度不足");
        string prefix = expectedStrokeCount > 0
            ? $"{expectedCharacter} {expectedStrokeCount}笔 ({lastPrefixScore:P0})"
            : (lastPrefixScore > 0f ? $"未识别 ({lastPrefixScore:P0})" : "—");
        return $"识别结果: {recognized}\n工整度: {LastNeatness:P0} (阈值 {acceptThreshold:P0})\n判定: {judge}\n逐笔预测: {prefix}\n笔画数: {LastStrokeCount}\n总笔长: {LastTotalLength:0.00}\n绘制用时: {LastDuration:0.00}s\n状态: {Status}\n子弹时间: {bulletTime}";
    }

    public void AdjustAcceptThreshold(float delta)
    {
        acceptThreshold = Mathf.Clamp(acceptThreshold + delta, 0.2f, 0.99f);
        Status = $"施法阈值: {acceptThreshold:P0}";
    }

    public bool CastCurrentTalisman()
    {
        if (strokes.Count == 0)
        {
            Status = "没有符纸内容";
            return false;
        }

        RecognizeAndUpdate();

        if (string.IsNullOrEmpty(LastCharacter) || LastNeatness < acceptThreshold)
        {
            Status = "施法失败: 未识别(鬼画符)";
            return false;
        }

        int manaCost = GetManaCost(LastCharacter);
        if (playerMana != null && !playerMana.HasMana(manaCost))
        {
            Status = $"法力不足: {LastCharacter} 需要 {manaCost}";
            return false;
        }

        bool casted;
        switch (LastCharacter)
        {
            case "火":
                casted = CastFireball();
                if (casted)
                {
                    Status = "施法成功: 火球";
                }
                break;
            case "山":
                casted = CastRockWall();
                if (casted)
                {
                    Status = "施法成功: 岩壁";
                }
                break;
            case "电":
                casted = CastLightning();
                if (casted)
                {
                    Status = "施法成功: 雷击";
                }
                break;
            default:
                Status = "施法失败: 未知法术";
                return false;
        }

        if (!casted)
        {
            return false;
        }

        if (playerMana != null)
        {
            playerMana.TrySpend(manaCost);
        }

        ClearTalisman(false);
        return true;
    }

    private int GetManaCost(string character)
    {
        switch (character)
        {
            case "火":
                return fireballManaCost;
            case "山":
                return rockWallManaCost;
            case "电":
                return lightningManaCost;
            default:
                return 0;
        }
    }

    public void ClearTalisman(bool resetStats)
    {
        strokes.Clear();
        currentStroke = null;
        isDrawing = false;
        expectedCharacter = string.Empty;
        expectedStrokeCount = 0;
        lastPrefixScore = 0f;
        ClearPixels();
        RestoreTimeScale();

        if (resetStats)
        {
            LastCharacter = string.Empty;
            LastNeatness = 0f;
            LastStrokeCount = 0;
            LastTotalLength = 0f;
            LastDuration = 0f;
            firstStrokeTime = 0f;
            lastStrokeEndTime = 0f;
            Status = "等待绘制";
        }
    }

    private void UpdateBulletTime()
    {
        int completedStrokes = isDrawing ? Mathf.Max(0, strokes.Count - 1) : strokes.Count;
        bool characterComplete = expectedStrokeCount > 0 && completedStrokes >= expectedStrokeCount;
        bool activelyDrawing = lastDrawActivityTime > 0f && Time.unscaledTime - lastDrawActivityTime <= bulletTimeGrace;
        bool wantsSlowMotion = activelyDrawing && !characterComplete;

        if (wantsSlowMotion && !bulletTimeActive)
        {
            savedTimeScale = Time.timeScale > 0.001f ? Time.timeScale : 1f;
            Time.timeScale = Mathf.Clamp(bulletTimeScale, 0.01f, 1f);
            bulletTimeActive = true;
        }
        else if (!wantsSlowMotion && bulletTimeActive)
        {
            RestoreTimeScale();
        }
    }

    private void RestoreTimeScale()
    {
        if (!bulletTimeActive)
        {
            return;
        }

        Time.timeScale = savedTimeScale > 0f ? savedTimeScale : 1f;
        bulletTimeActive = false;
    }

    private void HandlePointer()
    {
        Camera eventCamera = GetEventCamera();
        bool overArea = RectTransformUtility.RectangleContainsScreenPoint(drawingRect, Input.mousePosition, eventCamera);

        if (!isDrawing && Input.GetMouseButtonDown(1) && overArea)
        {
            BeginStroke(Input.mousePosition, eventCamera);
        }

        if (!isDrawing)
        {
            return;
        }

        if (Input.GetMouseButton(1))
        {
            if (TryGetNormalizedPoint(Input.mousePosition, eventCamera, out Vector2 point))
            {
                AddPoint(point);
            }
        }
        else
        {
            EndStroke();
        }
    }

    private void BeginStroke(Vector2 screenPoint, Camera eventCamera)
    {
        if (!TryGetNormalizedPoint(screenPoint, eventCamera, out Vector2 point))
        {
            return;
        }

        currentStroke = new List<Vector2> { point };
        strokes.Add(currentStroke);
        isDrawing = true;
        lastStrokeEndTime = 0f;
        lastDrawActivityTime = Time.unscaledTime;

        if (firstStrokeTime <= 0f)
        {
            firstStrokeTime = Time.unscaledTime;
        }

        LastCharacter = string.Empty;
        LastNeatness = 0f;
        LastStrokeCount = strokes.Count;
        Status = "绘制中";
        PaintDot(point);
    }

    private void AddPoint(Vector2 point)
    {
        if (currentStroke == null || currentStroke.Count == 0)
        {
            return;
        }

        Vector2 last = currentStroke[currentStroke.Count - 1];
        if (Vector2.Distance(last, point) < 0.0025f)
        {
            return;
        }

        lastDrawActivityTime = Time.unscaledTime;
        PaintSegment(last, point);
        currentStroke.Add(point);
    }

    private void EndStroke()
    {
        isDrawing = false;

        if (currentStroke != null && currentStroke.Count == 0)
        {
            strokes.Remove(currentStroke);
        }

        currentStroke = null;
        lastStrokeEndTime = Time.unscaledTime;
        lastDrawActivityTime = Time.unscaledTime;
        RecognizeAndUpdate();
        UpdatePrediction();
    }

    private float DrawingAspect
    {
        get
        {
            if (drawingRect == null)
            {
                return 1f;
            }

            Rect rect = drawingRect.rect;
            return rect.height > 1f ? Mathf.Max(0.05f, rect.width / rect.height) : 1f;
        }
    }
    private void UpdatePrediction()
    {
        if (strokes.Count == 0)
        {
            expectedCharacter = string.Empty;
            expectedStrokeCount = 0;
            lastPrefixScore = 0f;
            return;
        }

        HandwritingRecognizer.PrefixThreshold = predictionThreshold;
        HandwritingResult prefix = HandwritingRecognizer.IdentifyPrefix(strokes, DrawingAspect);
        lastPrefixScore = prefix.Score;

        if (!string.IsNullOrEmpty(prefix.Character))
        {
            expectedCharacter = prefix.Character;
            expectedStrokeCount = HandwritingRecognizer.GetStrokeCount(prefix.Character);
            return;
        }

        if (expectedStrokeCount > 0 && strokes.Count > expectedStrokeCount)
        {
            expectedCharacter = string.Empty;
            expectedStrokeCount = 0;
        }
    }
    private bool TryGetNormalizedPoint(Vector2 screenPoint, Camera eventCamera, out Vector2 normalized)
    {
        normalized = Vector2.zero;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(drawingRect, screenPoint, eventCamera, out Vector2 local))
        {
            return false;
        }

        Rect rect = drawingRect.rect;
        float u = Mathf.InverseLerp(rect.xMin, rect.xMax, local.x);
        float v = Mathf.InverseLerp(rect.yMin, rect.yMax, local.y);
        normalized = new Vector2(Mathf.Clamp01(u), Mathf.Clamp01(v));
        return true;
    }

    private Camera GetEventCamera()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        return canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera ? canvas.worldCamera : null;
    }

    private void RecognizeAndUpdate()
    {
        HandwritingResult result = HandwritingRecognizer.Recognize(strokes, DrawingAspect);
        LastCharacter = result.Character;
        LastNeatness = result.Score;
        LastStrokeCount = result.StrokeCount;
        LastTotalLength = result.TotalLength;

        if (firstStrokeTime > 0f)
        {
            float endTime = lastStrokeEndTime > 0f ? lastStrokeEndTime : Time.unscaledTime;
            LastDuration = Mathf.Max(0f, endTime - firstStrokeTime);
        }
        else
        {
            LastDuration = 0f;
        }

        if (string.IsNullOrEmpty(LastCharacter))
        {
            Status = strokes.Count > 0 ? "未识别" : "等待绘制";
        }
        else
        {
            Status = $"识别: {LastCharacter} {LastNeatness:P0}";
        }
    }

    private bool CastFireball()
    {
        if (player == null || fireballPrefab == null)
        {
            Status = "缺少火球资源";
            return false;
        }

        PlayerController2D controller = player.GetComponent<PlayerController2D>();
        int facing = controller != null ? controller.Facing : 1;
        Vector3 spawnPosition = player.position
            + Vector3.right * (facing * fireballSpawnDistance)
            + Vector3.up * fireballSpawnHeight;

        GameObject fireballObject = Instantiate(fireballPrefab, spawnPosition, Quaternion.identity);
        Fireball fireball = fireballObject.GetComponent<Fireball>();
        if (fireball != null)
        {
            fireball.Launch(new Vector2(facing, 0f));
        }

        return true;
    }

    private bool CastRockWall()
    {
        if (player == null || rockWallPrefab == null)
        {
            Status = "缺少岩壁资源";
            return false;
        }

        PlayerController2D controller = player.GetComponent<PlayerController2D>();
        int facing = controller != null ? controller.Facing : 1;
        Collider2D body = player.GetComponent<Collider2D>();

        float x = player.position.x + facing * wallSpawnDistance;
        float feetY = body != null ? body.bounds.min.y : player.position.y - 0.9f;

        RaycastHit2D hit = Physics2D.Raycast(new Vector2(x, player.position.y + 2.5f), Vector2.down, 8f, wallGroundMask);
        float groundY = hit.collider != null ? hit.point.y : feetY;

        Instantiate(rockWallPrefab, new Vector3(x, groundY, 0f), Quaternion.identity);
        return true;
    }

    private bool CastLightning()
    {
        if (player == null || lightningPrefab == null)
        {
            Status = "缺少雷电资源";
            return false;
        }

        PlayerController2D controller = player.GetComponent<PlayerController2D>();
        int facing = controller != null ? controller.Facing : 1;
        Collider2D body = player.GetComponent<Collider2D>();
        float feetY = body != null ? body.bounds.min.y : player.position.y - 0.9f;

        GameObject fieldObject = Instantiate(lightningPrefab, Vector3.zero, Quaternion.identity);
        LightningField field = fieldObject.GetComponent<LightningField>();
        if (field != null)
        {
            field.Begin(new Vector2(player.position.x, feetY), facing);
        }

        return true;
    }

    private void LoadTemplateForDebug(string character)
    {
        IReadOnlyList<List<Vector2>> template = HandwritingRecognizer.GetTemplate(character);
        if (template == null)
        {
            Status = "没有模板: " + character;
            return;
        }

        ClearTalisman(true);
        firstStrokeTime = Time.unscaledTime;
        lastStrokeEndTime = Time.unscaledTime;
        lastDrawActivityTime = Time.unscaledTime;

        foreach (List<Vector2> sourceStroke in template)
        {
            List<Vector2> copy = MapDebugStrokes(sourceStroke);
            strokes.Add(copy);
            PaintStroke(copy);
        }

        RecognizeAndUpdate();
        Status = "调试: 已载入 " + character + " 模板";
    }

    private void LoadScribbleForDebug()
    {
        ClearTalisman(true);
        firstStrokeTime = Time.unscaledTime;
        lastStrokeEndTime = Time.unscaledTime;
        lastDrawActivityTime = Time.unscaledTime;

        List<List<Vector2>> scribble = new List<List<Vector2>>
        {
            new List<Vector2> { new Vector2(0.15f, 0.20f), new Vector2(0.85f, 0.80f) },
            new List<Vector2> { new Vector2(0.15f, 0.80f), new Vector2(0.85f, 0.20f) },
            new List<Vector2> { new Vector2(0.50f, 0.10f), new Vector2(0.50f, 0.90f) }
        };

        foreach (List<Vector2> sourceStroke in scribble)
        {
            List<Vector2> copy = MapDebugStrokes(sourceStroke);
            strokes.Add(copy);
            PaintStroke(copy);
        }

        RecognizeAndUpdate();
        Status = "调试: 随机涂鸦";
    }

    private List<Vector2> MapDebugStrokes(List<Vector2> sourceStroke)
    {
        float aspect = texHeight > 0 ? (float)texWidth / texHeight : 1f;
        float width = 0.72f;
        float height = Mathf.Min(0.9f, width * aspect);
        List<Vector2> mapped = new List<Vector2>(sourceStroke.Count);

        foreach (Vector2 point in sourceStroke)
        {
            mapped.Add(new Vector2((point.x - 0.5f) * width + 0.5f, (point.y - 0.5f) * height + 0.5f));
        }

        return mapped;
    }

    private void PaintStroke(List<Vector2> points)
    {
        if (points.Count == 0)
        {
            return;
        }

        if (points.Count == 1)
        {
            PaintDot(points[0]);
            return;
        }

        for (int i = 1; i < points.Count; i++)
        {
            PaintSegment(points[i - 1], points[i]);
        }
    }

    private void PaintDot(Vector2 point)
    {
        int px = Mathf.RoundToInt(point.x * (texWidth - 1));
        int py = Mathf.RoundToInt(point.y * (texHeight - 1));
        int radius = Mathf.CeilToInt(brushRadiusPixels);
        float radiusSquared = brushRadiusPixels * brushRadiusPixels;

        for (int y = py - radius; y <= py + radius; y++)
        {
            if (y < 0 || y >= texHeight)
            {
                continue;
            }

            for (int x = px - radius; x <= px + radius; x++)
            {
                if (x < 0 || x >= texWidth)
                {
                    continue;
                }

                if ((x - px) * (x - px) + (y - py) * (y - py) <= radiusSquared)
                {
                    inkPixels[y * texWidth + x] = InkColor;
                }
            }
        }

        ApplyInk();
    }

    private void PaintSegment(Vector2 from, Vector2 to)
    {
        Vector2 a = new Vector2(from.x * (texWidth - 1), from.y * (texHeight - 1));
        Vector2 b = new Vector2(to.x * (texWidth - 1), to.y * (texHeight - 1));
        float radius = brushRadiusPixels;
        float radiusSquared = radius * radius;

        int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - radius));
        int maxX = Mathf.Min(texWidth - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + radius));
        int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - radius));
        int maxY = Mathf.Min(texHeight - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + radius));

        Vector2 segment = b - a;
        float segmentLengthSquared = segment.sqrMagnitude;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 point = new Vector2(x, y);
                float t = segmentLengthSquared > 0.0001f
                    ? Mathf.Clamp01(Vector2.Dot(point - a, segment) / segmentLengthSquared)
                    : 0f;
                Vector2 closest = a + segment * t;
                if ((point - closest).sqrMagnitude <= radiusSquared)
                {
                    inkPixels[y * texWidth + x] = InkColor;
                }
            }
        }

        ApplyInk();
    }

    private void ClearPixels()
    {
        if (inkPixels == null)
        {
            return;
        }

        for (int i = 0; i < inkPixels.Length; i++)
        {
            inkPixels[i] = ClearColor;
        }

        ApplyInk();
    }

    private void ApplyInk()
    {
        if (inkTexture == null || inkPixels == null)
        {
            return;
        }

        inkTexture.SetPixels32(inkPixels);
        inkTexture.Apply(false);
    }
}
