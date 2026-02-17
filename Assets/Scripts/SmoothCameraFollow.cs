using UnityEngine;

public class SmoothCameraFollow : MonoBehaviour
{
    [Header("Target & Offset")]
    public Transform target;          // The target the camera should follow
    public Vector3 offset;            // The offset from the target's position
    public bool autoFindPlayerTarget = true;
    public bool alwaysFollowPrimaryPlayer = true;
    public bool snapToTargetOnAcquire = true;

    [Header("Smooth Movement")]
    [Tooltip("Time it takes for the camera to reach the target position")]
    public float smoothTime = 0.3f;     // Smoothing time for the camera movement
    private Vector3 velocity = Vector3.zero;  // Used by SmoothDamp for velocity tracking

    [Header("Look-Ahead Settings")]
    [Tooltip("Enable to allow the camera to look ahead in the player's movement direction")]
    public bool enableLookAhead = true;
    [Tooltip("How far ahead of the target to look based on movement direction")]
    public float lookAheadDistance = 2f;
    [Tooltip("Speed at which the camera's look-ahead offset adjusts")]
    public float lookAheadSpeed = 5f;
    private Vector3 currentLookAhead = Vector3.zero;  // Current look-ahead offset
    private Vector3 lastTargetPosition;               // Stores the target's last frame position
    private float fallbackZ;

    private void Start()
    {
        fallbackZ = transform.position.z;
        TryResolveTarget();
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

        // Determine how much the target has moved since the last frame.
        Vector3 targetDelta = target.position - lastTargetPosition;
        lastTargetPosition = target.position;

        // Calculate look-ahead offset if enabled.
        if (enableLookAhead)
        {
            Vector3 desiredLookAhead = targetDelta.normalized * lookAheadDistance;
            currentLookAhead = Vector3.Lerp(currentLookAhead, desiredLookAhead, Time.deltaTime * lookAheadSpeed);
        }
        else
        {
            currentLookAhead = Vector3.zero;
        }

        // Compute the desired camera position with offset and look-ahead.
        Vector3 desiredPosition = target.position + offset + currentLookAhead;
        desiredPosition.z = GetDesiredZ();

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

        if (target == null)
        {
            return;
        }

        OnTargetAcquired();
    }

    private void OnTargetAcquired()
    {
        lastTargetPosition = target.position;
        currentLookAhead = Vector3.zero;
        velocity = Vector3.zero;

        if (snapToTargetOnAcquire)
        {
            Vector3 snappedPosition = target.position + offset;
            snappedPosition.z = GetDesiredZ();
            transform.position = snappedPosition;
        }
    }

    private bool IsTargetValid()
    {
        if (target == null)
        {
            return false;
        }

        if (!target.gameObject.activeInHierarchy)
        {
            return false;
        }

        PlayerController targetPlayer = target.GetComponent<PlayerController>() ??
                                        target.GetComponentInParent<PlayerController>();

        if (targetPlayer != null && !targetPlayer.HasInputAuthority)
        {
            return false;
        }

        return true;
    }

    private void ForcePrimaryTargetIfNeeded()
    {
        PlayerController primaryPlayer = PlayerController.FindPrimary();
        if (primaryPlayer == null)
        {
            return;
        }

        if (target == primaryPlayer.transform)
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
}
