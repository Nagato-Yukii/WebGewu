using UnityEngine;

public class DynamicCameraTracker : MonoBehaviour
{
    [Tooltip("Current follow target.")]
    public Transform target;

    [Tooltip("Smoothing time for camera translation.")]
    [Min(0.001f)] public float smoothingTime = 0.15f;

    [Tooltip("Keep camera world Y fixed while following XZ.")]
    public bool lockWorldY = true;

    [Tooltip("Rotate camera to look at the target while following.")]
    public bool rotateToTarget = true;

    [Tooltip("How quickly the camera rotates toward the target.")]
    [Min(0f)] public float rotationSmoothing = 12f;

    [Tooltip("If retarget distance is large, snap camera to desired position.")]
    [Min(0f)] public float snapDistance = 4f;

    [Tooltip("Keep previous offset when switching target.")]
    public bool keepOffsetOnRetarget = true;

    private Vector3 offset;
    private Vector3 velocity;
    private bool hasOffset;

    private void Start()
    {
        RebuildOffsetIfNeeded();
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        RebuildOffsetIfNeeded();

        Vector3 desired = target.position + offset;
        if (lockWorldY)
        {
            desired.y = transform.position.y;
        }

        if (snapDistance > 0f && Vector3.Distance(transform.position, desired) > snapDistance)
        {
            transform.position = desired;
            velocity = Vector3.zero;
            RotateTowardsTarget(Time.deltaTime);
            return;
        }

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desired,
            ref velocity,
            smoothingTime,
            Mathf.Infinity,
            Time.deltaTime);

        RotateTowardsTarget(Time.deltaTime);
    }

    public void SetTarget(Transform newTarget, bool rebuildOffset = false)
    {
        if (rebuildOffset)
        {
            hasOffset = false;
        }

        target = newTarget;
        velocity = Vector3.zero;

        if (target == null)
        {
            return;
        }

        if (!hasOffset || !keepOffsetOnRetarget)
        {
            offset = transform.position - target.position;
            hasOffset = true;
        }

        Vector3 desired = target.position + offset;
        if (lockWorldY)
        {
            desired.y = transform.position.y;
        }

        if (snapDistance > 0f)
        {
            transform.position = desired;
        }

        RotateTowardsTarget(Time.deltaTime);
    }

    private void RebuildOffsetIfNeeded()
    {
        if (hasOffset || target == null)
        {
            return;
        }

        offset = transform.position - target.position;
        hasOffset = true;
    }

    private void RotateTowardsTarget(float deltaTime)
    {
        if (!rotateToTarget || target == null)
        {
            return;
        }

        Vector3 direction = target.position - transform.position;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion desiredRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        if (rotationSmoothing <= 0f)
        {
            transform.rotation = desiredRotation;
            return;
        }

        float t = 1f - Mathf.Exp(-rotationSmoothing * Mathf.Max(deltaTime, 0f));
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, t);
    }
}
