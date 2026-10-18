using Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
public class GalleryCameraDirector : MonoBehaviour
{
    private enum GalleryCameraState
    {
        Follow,
        Room1,
        Room2,
        Room3
    }

    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private CinemachineVirtualCamera followCamera;
    [SerializeField] private CinemachineVirtualCamera room1Camera;
    [SerializeField] private CinemachineVirtualCamera room2Camera;
    [SerializeField] private CinemachineVirtualCamera room3Camera;

    [Header("Room Bounds")]
    [SerializeField] private Rect room1Bounds = new Rect(111.45f, 11.1f, 21f, 10.9f);
    [SerializeField] private Rect room2Bounds = new Rect(111.45f, 22f, 21f, 10f);
    [SerializeField] private Rect room3Bounds = new Rect(83.45f, 12.2f, 28f, 25.8f);
    [SerializeField] private Rect galleryBounds = new Rect(82.45f, 10.5f, 51f, 28f);

    [Header("Priorities")]
    [SerializeField] private int activePriority = 30;
    [SerializeField] private int idlePriority = 5;

    private GalleryCameraState currentState = GalleryCameraState.Follow;
    private bool hasEnteredRoom2;

    public string CurrentStateName => currentState.ToString();

    private void Start()
    {
        if (player == null)
        {
            PlayerController2D controller = FindObjectOfType<PlayerController2D>();
            if (controller != null)
            {
                player = controller.transform;
            }
        }

        ApplyState(GalleryCameraState.Follow, true);
    }

    private void Update()
    {
        if (player == null)
        {
            return;
        }

        Vector2 position = player.position;

        if (!galleryBounds.Contains(position))
        {
            hasEnteredRoom2 = false;
            ApplyState(GalleryCameraState.Follow);
            return;
        }

        if (room2Bounds.Contains(position))
        {
            hasEnteredRoom2 = true;
            ApplyState(GalleryCameraState.Room2);
            return;
        }

        if (room1Bounds.Contains(position))
        {
            ApplyState(GalleryCameraState.Room1);
            return;
        }

        if (room3Bounds.Contains(position))
        {
            ApplyState(hasEnteredRoom2 ? GalleryCameraState.Room3 : GalleryCameraState.Follow);
            return;
        }

        ApplyState(GalleryCameraState.Follow);
    }

    private void ApplyState(GalleryCameraState state, bool force = false)
    {
        if (!force && currentState == state)
        {
            return;
        }

        currentState = state;
        SetCameraPriorities(state);

        if (AudioDirector.Instance != null)
        {
            switch (state)
            {
                case GalleryCameraState.Room1:
                    AudioDirector.Instance.PlayGalleryRoom1();
                    break;
                case GalleryCameraState.Room2:
                    AudioDirector.Instance.PlayGalleryRoom2();
                    break;
                default:
                    AudioDirector.Instance.StopGalleryAmbience();
                    break;
            }
        }
    }

    private void SetCameraPriorities(GalleryCameraState state)
    {
        SetPriority(followCamera, state == GalleryCameraState.Follow ? activePriority : idlePriority);
        SetPriority(room1Camera, state == GalleryCameraState.Room1 ? activePriority : idlePriority);
        SetPriority(room2Camera, state == GalleryCameraState.Room2 ? activePriority : idlePriority);
        SetPriority(room3Camera, state == GalleryCameraState.Room3 ? activePriority : idlePriority);
    }

    private static void SetPriority(CinemachineVirtualCamera camera, int priority)
    {
        if (camera != null)
        {
            camera.Priority = priority;
        }
    }

    private void OnDrawGizmosSelected()
    {
        DrawBounds(room1Bounds, new Color(0.2f, 0.75f, 1f, 0.8f));
        DrawBounds(room2Bounds, new Color(1f, 0.75f, 0.2f, 0.8f));
        DrawBounds(room3Bounds, new Color(0.45f, 1f, 0.35f, 0.8f));
        DrawBounds(galleryBounds, new Color(1f, 0.3f, 0.3f, 0.8f));
    }

    private static void DrawBounds(Rect bounds, Color color)
    {
        Gizmos.color = color;
        Vector3 bottomLeft = new Vector3(bounds.xMin, bounds.yMin, 0f);
        Vector3 topLeft = new Vector3(bounds.xMin, bounds.yMax, 0f);
        Vector3 topRight = new Vector3(bounds.xMax, bounds.yMax, 0f);
        Vector3 bottomRight = new Vector3(bounds.xMax, bounds.yMin, 0f);
        Gizmos.DrawLine(bottomLeft, topLeft);
        Gizmos.DrawLine(topLeft, topRight);
        Gizmos.DrawLine(topRight, bottomRight);
        Gizmos.DrawLine(bottomRight, bottomLeft);
    }
}
