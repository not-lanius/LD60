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
            return;
        }

        ladderTilemap.CompressBounds();
        Bounds localBounds = ladderTilemap.localBounds;

        if (localBounds.size.x <= 0.0001f || localBounds.size.y <= 0.0001f)
        {
            climbArea.enabled = false;
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
