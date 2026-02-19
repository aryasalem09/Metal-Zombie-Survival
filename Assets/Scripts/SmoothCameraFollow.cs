using UnityEngine;
using UnityEngine.Tilemaps;

public class SmoothCameraFollow : MonoBehaviour
{
    [Header("Target & Offset")]
    public Transform target;
    public Vector3 offset;
    public bool autoFindPlayerTarget = true;
    public bool alwaysFollowPrimaryPlayer = true;
    public bool snapToTargetOnAcquire = true;

    [Header("Smooth Movement")]
    [Tooltip("Time it takes for the camera to reach the target position.")]
    public float smoothTime = 0.3f;

    [Header("Look-Ahead")]
    public bool enableLookAhead = true;
    public bool forceCenterOnTarget = true;
    public bool hardLockToPlayerCenter = true;
    public float lookAheadDistance = 2f;
    public float lookAheadSpeed = 5f;

    [Header("Bounds")]
    public bool constrainToLevelBounds = true;
    public bool autoDetectBoundsFromTilemaps = true;
    public Bounds manualCameraBounds = new Bounds(Vector3.zero, new Vector3(120f, 80f, 0f));
    [Min(0f)] public float boundsPadding = 0.15f;

    private Vector3 velocity = Vector3.zero;
    private Vector3 currentLookAhead = Vector3.zero;
    private Vector3 lastTargetPosition;
    private float fallbackZ;

    private bool hasResolvedBounds;
    private bool hasValidBounds;
    private Bounds resolvedBounds;

    private void Awake()
    {
        DisableCinemachineBrainIfPresent();
    }

    private void Start()
    {
        fallbackZ = transform.position.z;
        TryResolveTarget();
        ResolveBoundsIfNeeded();
    }

    private void LateUpdate()
    {
        if (alwaysFollowPrimaryPlayer)
        {
            ForcePrimaryTargetIfNeeded();
        }

        if (!IsTargetValid())
        {
            target = null;
            TryResolveTarget();
        }

        if (!IsTargetValid())
        {
            return;
        }

        if (hardLockToPlayerCenter)
        {
            Vector3 centeredPosition = target.position + offset;
            centeredPosition.z = GetDesiredZ();
            transform.position = centeredPosition;
            currentLookAhead = Vector3.zero;
            velocity = Vector3.zero;
            return;
        }

        ResolveBoundsIfNeeded();

        Vector3 targetDelta = target.position - lastTargetPosition;
        lastTargetPosition = target.position;

        if (forceCenterOnTarget || !enableLookAhead)
        {
            currentLookAhead = Vector3.zero;
        }
        else
        {
            Vector3 desiredLookAhead = targetDelta.sqrMagnitude > 0.0001f
                ? targetDelta.normalized * lookAheadDistance
                : Vector3.zero;
            currentLookAhead = Vector3.Lerp(currentLookAhead, desiredLookAhead, Time.deltaTime * lookAheadSpeed);
        }

        Vector3 desiredPosition = target.position + offset + currentLookAhead;
        desiredPosition.z = GetDesiredZ();
        desiredPosition = ClampToBounds(desiredPosition);

        if (smoothTime <= 0.0001f)
        {
            transform.position = desiredPosition;
            velocity = Vector3.zero;
            return;
        }

        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, smoothTime);
    }

    private void TryResolveTarget()
    {
        if (target != null || !autoFindPlayerTarget)
        {
            return;
        }

        PlayerController primaryPlayer = PlayerController.FindPrimary();
        if (primaryPlayer != null)
        {
            target = primaryPlayer.transform;
        }
        else
        {
            GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            if (taggedPlayer != null)
            {
                target = taggedPlayer.transform;
            }
        }

        if (target != null)
        {
            OnTargetAcquired();
        }
    }

    private void OnTargetAcquired()
    {
        if (target == null)
        {
            return;
        }

        lastTargetPosition = target.position;
        currentLookAhead = Vector3.zero;
        velocity = Vector3.zero;

        if (snapToTargetOnAcquire)
        {
            Vector3 snappedPosition = target.position + offset;
            snappedPosition.z = GetDesiredZ();
            snappedPosition = ClampToBounds(snappedPosition);
            transform.position = snappedPosition;
        }
    }

    private bool IsTargetValid()
    {
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            return false;
        }

        PlayerController targetPlayer = target.GetComponent<PlayerController>() ??
                                        target.GetComponentInParent<PlayerController>();

        return targetPlayer == null || targetPlayer.HasInputAuthority;
    }

    private void ForcePrimaryTargetIfNeeded()
    {
        PlayerController primaryPlayer = PlayerController.FindPrimary();
        if (primaryPlayer == null || target == primaryPlayer.transform)
        {
            return;
        }

        target = primaryPlayer.transform;
        OnTargetAcquired();
    }

    private float GetDesiredZ()
    {
        if (target != null && Mathf.Abs(offset.z) > 0.0001f)
        {
            return target.position.z + offset.z;
        }

        return fallbackZ;
    }

    private void ResolveBoundsIfNeeded()
    {
        if (hasResolvedBounds)
        {
            return;
        }

        hasResolvedBounds = true;
        hasValidBounds = false;

        if (!constrainToLevelBounds)
        {
            return;
        }

        if (autoDetectBoundsFromTilemaps && TryCalculateBoundsFromTilemaps(out Bounds tilemapBounds))
        {
            resolvedBounds = tilemapBounds;
            hasValidBounds = true;
            return;
        }

        if (manualCameraBounds.size.sqrMagnitude > 0.001f)
        {
            resolvedBounds = manualCameraBounds;
            hasValidBounds = true;
        }
    }

    private static bool TryCalculateBoundsFromTilemaps(out Bounds bounds)
    {
        TilemapRenderer[] renderers = FindObjectsOfType<TilemapRenderer>();
        if (renderers == null || renderers.Length == 0)
        {
            bounds = default;
            return false;
        }

        bool hasAnyBounds = false;
        bounds = default;

        for (int i = 0; i < renderers.Length; i++)
        {
            TilemapRenderer renderer = renderers[i];
            if (renderer == null || !renderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!hasAnyBounds)
            {
                bounds = renderer.bounds;
                hasAnyBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasAnyBounds;
    }

    private Vector3 ClampToBounds(Vector3 desiredPosition)
    {
        if (!constrainToLevelBounds || !hasValidBounds)
        {
            return desiredPosition;
        }

        Camera activeCamera = GetComponent<Camera>();
        if (activeCamera == null || !activeCamera.orthographic)
        {
            return desiredPosition;
        }

        float verticalHalfSize = activeCamera.orthographicSize + boundsPadding;
        float horizontalHalfSize = verticalHalfSize * activeCamera.aspect + boundsPadding;

        float minX = resolvedBounds.min.x + horizontalHalfSize;
        float maxX = resolvedBounds.max.x - horizontalHalfSize;
        float minY = resolvedBounds.min.y + verticalHalfSize;
        float maxY = resolvedBounds.max.y - verticalHalfSize;

        if (minX > maxX)
        {
            desiredPosition.x = resolvedBounds.center.x;
        }
        else
        {
            desiredPosition.x = Mathf.Clamp(desiredPosition.x, minX, maxX);
        }

        if (minY > maxY)
        {
            desiredPosition.y = resolvedBounds.center.y;
        }
        else
        {
            desiredPosition.y = Mathf.Clamp(desiredPosition.y, minY, maxY);
        }

        return desiredPosition;
    }

    private void DisableCinemachineBrainIfPresent()
    {
        Behaviour brain = GetComponent("CinemachineBrain") as Behaviour;
        if (brain != null && brain.enabled)
        {
            brain.enabled = false;
        }
    }
}
