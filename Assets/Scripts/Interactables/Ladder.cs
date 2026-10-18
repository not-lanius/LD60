using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(BoxCollider2D))]
public class Ladder : MonoBehaviour
{
    [Header("Tilemap")]
    [SerializeField] private Tilemap ladderTilemap;
    [SerializeField] private BoxCollider2D climbArea;

    [Header("Climb Area Padding")]
    [SerializeField] private float sidePadding = 0.35f;
    [SerializeField] private float bottomPadding = 0.1f;
    [SerializeField] private float topPadding = 0.6f;

    private readonly HashSet<PlayerController2D> players = new HashSet<PlayerController2D>();
    private readonly List<Bounds> climbSegments = new List<Bounds>();

    public Bounds ClimbBounds => climbArea != null ? climbArea.bounds : new Bounds(transform.position, Vector3.one);
    public float CenterX => ClimbBounds.center.x;
    public float Bottom => ClimbBounds.min.y;
    public float Top => ClimbBounds.max.y;

    private void Awake()
    {
        if (ladderTilemap == null)
        {
            ladderTilemap = GetComponent<Tilemap>();
        }

        if (climbArea == null)
        {
            climbArea = GetComponent<BoxCollider2D>();
        }

        RefreshClimbArea();
    }

    [ContextMenu("Refresh Climb Area")]
    public void RefreshClimbArea()
    {
        if (ladderTilemap == null || climbArea == null)
        {
            climbSegments.Clear();
            return;
        }

        ladderTilemap.CompressBounds();
        Bounds localBounds = ladderTilemap.localBounds;

        if (localBounds.size.x <= 0.0001f || localBounds.size.y <= 0.0001f)
        {
            climbArea.enabled = false;
            climbSegments.Clear();
            return;
        }

        climbArea.enabled = true;
        climbArea.isTrigger = true;
        climbArea.size = new Vector2(
            localBounds.size.x + sidePadding * 2f,
            localBounds.size.y + bottomPadding + topPadding);
        climbArea.offset = new Vector2(
            localBounds.center.x,
            localBounds.center.y + (topPadding - bottomPadding) * 0.5f);

        RebuildClimbSegments();
    }

    private void RebuildClimbSegments()
    {
        climbSegments.Clear();

        if (ladderTilemap == null)
        {
            return;
        }

        HashSet<Vector3Int> ladderCells = new HashSet<Vector3Int>();
        BoundsInt cellBounds = ladderTilemap.cellBounds;

        foreach (Vector3Int cell in cellBounds.allPositionsWithin)
        {
            if (ladderTilemap.HasTile(cell))
            {
                ladderCells.Add(cell);
            }
        }

        HashSet<Vector3Int> visited = new HashSet<Vector3Int>();
        Queue<Vector3Int> queue = new Queue<Vector3Int>();
        Vector3Int[] directions =
        {
            Vector3Int.up,
            Vector3Int.down,
            Vector3Int.left,
            Vector3Int.right
        };

        foreach (Vector3Int start in ladderCells)
        {
            if (!visited.Add(start))
            {
                continue;
            }

            queue.Clear();
            queue.Enqueue(start);

            int minX = start.x;
            int maxX = start.x;
            int minY = start.y;
            int maxY = start.y;

            while (queue.Count > 0)
            {
                Vector3Int cell = queue.Dequeue();
                minX = Mathf.Min(minX, cell.x);
                maxX = Mathf.Max(maxX, cell.x);
                minY = Mathf.Min(minY, cell.y);
                maxY = Mathf.Max(maxY, cell.y);

                foreach (Vector3Int direction in directions)
                {
                    Vector3Int next = cell + direction;
                    if (ladderCells.Contains(next) && visited.Add(next))
                    {
                        queue.Enqueue(next);
                    }
                }
            }

            Vector3 minWorld = ladderTilemap.CellToWorld(new Vector3Int(minX, minY, cellBounds.zMin));
            Vector3 maxWorld = ladderTilemap.CellToWorld(new Vector3Int(maxX + 1, maxY + 1, cellBounds.zMin));

            Bounds segment = new Bounds();
            segment.SetMinMax(
                minWorld + new Vector3(-sidePadding, -bottomPadding, 0f),
                maxWorld + new Vector3(sidePadding, topPadding, 0f));
            climbSegments.Add(segment);
        }
    }

    public bool TryGetClimbBounds(Bounds playerBounds, out Bounds segmentBounds)
    {
        segmentBounds = default;
        float closestDistance = float.MaxValue;
        bool found = false;

        foreach (Bounds segment in climbSegments)
        {
            if (!Overlaps2D(segment, playerBounds))
            {
                continue;
            }

            float distance = Mathf.Abs(segment.center.x - playerBounds.center.x);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                segmentBounds = segment;
                found = true;
            }
        }

        return found;
    }

    private static bool Overlaps2D(Bounds a, Bounds b)
    {
        return a.max.x >= b.min.x
            && a.min.x <= b.max.x
            && a.max.y >= b.min.y
            && a.min.y <= b.max.y;
    }

    public bool HasPlayer(PlayerController2D player)
    {
        return player != null && players.Contains(player);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        PlayerController2D player = other.GetComponentInParent<PlayerController2D>();
        if (player == null)
        {
            return;
        }

        if (players.Add(player))
        {
            player.RegisterLadder(this);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerController2D player = other.GetComponentInParent<PlayerController2D>();
        if (player == null)
        {
            return;
        }

        players.Remove(player);
        player.UnregisterLadder(this);
    }

    private void OnDisable()
    {
        foreach (PlayerController2D player in players)
        {
            if (player != null)
            {
                player.UnregisterLadder(this);
            }
        }

        players.Clear();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            return;
        }

        if (ladderTilemap == null)
        {
            ladderTilemap = GetComponent<Tilemap>();
        }

        if (climbArea == null)
        {
            climbArea = GetComponent<BoxCollider2D>();
        }

        RefreshClimbArea();
    }
#endif
}
