using System.Collections;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class Door : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField] private SpriteRenderer doorRenderer;
    [SerializeField] private Sprite[] frames = new Sprite[7];
    [SerializeField] private float frameDuration = 0.08f;

    [Header("Interaction")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private float interactRange = 2.2f;

    [Header("Collision")]
    [SerializeField] private Collider2D blockingCollider;

    private PlayerController2D player;
    private PlayerHealth playerHealth;
    private bool isOpening;
    private bool isOpen;

    public bool IsOpen => isOpen;
    public bool IsOpening => isOpening;
    public bool CanInteract => !isOpen && !isOpening;
    public int CurrentFrame { get; private set; } = -1;

    private void Awake()
    {
        if (doorRenderer == null)
        {
            doorRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (blockingCollider == null)
        {
            blockingCollider = GetComponent<Collider2D>();
        }

        FindPlayer();
        ApplyFrame(0);
    }

    private void Update()
    {
        if (TutorialUI.BlocksGameplay)
        {
            return;
        }

        if (!CanInteract)
        {
            return;
        }

        if (player == null)
        {
            FindPlayer();
        }

        if (player == null || (playerHealth != null && playerHealth.IsDead))
        {
            return;
        }

        if (IsPlayerInRange() && Input.GetKeyDown(interactKey))
        {
            Open();
        }
    }

    public void Open()
    {
        if (!CanInteract)
        {
            return;
        }

        StartCoroutine(OpenRoutine());
    }

    private IEnumerator OpenRoutine()
    {
        isOpening = true;

        if (AudioDirector.Instance != null)
        {
            AudioDirector.Instance.PlayDoorOpen();
        }

        int frameCount = frames != null ? frames.Length : 0;
        for (int i = 0; i < frameCount; i++)
        {
            ApplyFrame(i);

            if (i < frameCount - 1 && frameDuration > 0f)
            {
                yield return new WaitForSeconds(frameDuration);
            }
        }

        SetBlocking(false);
        isOpening = false;
        isOpen = true;
    }

    private void ApplyFrame(int index)
    {
        if (frames == null || index < 0 || index >= frames.Length || frames[index] == null)
        {
            return;
        }

        CurrentFrame = index;

        if (doorRenderer != null)
        {
            doorRenderer.sprite = frames[index];
        }
    }

    private void SetBlocking(bool blocked)
    {
        if (blockingCollider != null)
        {
            blockingCollider.enabled = blocked;
        }
    }

    private bool IsPlayerInRange()
    {
        Vector2 doorCenter = blockingCollider != null
            ? blockingCollider.bounds.center
            : (Vector2)transform.position + Vector2.up;
        return Vector2.Distance(player.transform.position, doorCenter) <= interactRange;
    }

    private void FindPlayer()
    {
        player = FindObjectOfType<PlayerController2D>();
        playerHealth = player != null ? player.GetComponent<PlayerHealth>() : null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying || doorRenderer == null || frames == null || frames.Length == 0)
        {
            return;
        }

        ApplyFrame(0);
    }
#endif
}
