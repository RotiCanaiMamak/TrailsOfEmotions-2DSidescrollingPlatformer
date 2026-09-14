using UnityEngine;

[RequireComponent(typeof(Camera))]
public class StartGameCameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Offset")]
    public float offsetX = 3f;
    public float offsetY = 1f;

    [Header("Zoom")]
    [Min(0.1f)]
    public float orthographicSize = 16f;

    [Header("Smoothing")]
    public float smoothSpeed = 8f;

    private Camera cam;
    private Vector3 targetPos;
    private bool warnedMissingTarget;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        ApplyOrthographicSize();
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            WarnMissingTargetOnce();
            return;
        }

        targetPos = GetTargetPosition(target);
        transform.position = Vector3.Lerp(transform.position, targetPos, smoothSpeed * Time.deltaTime);

        ApplyOrthographicSize();
    }

    public void SetTarget(Transform newTarget, bool snapImmediately = false)
    {
        target = newTarget;
        if (target != null)
        {
            warnedMissingTarget = false;
        }

        if (snapImmediately)
        {
            SnapToTarget();
        }
    }

    public void SnapToTarget(Transform snapTarget = null)
    {
        Transform resolvedTarget = snapTarget != null ? snapTarget : target;
        if (resolvedTarget == null)
        {
            WarnMissingTargetOnce();
            return;
        }

        targetPos = GetTargetPosition(resolvedTarget);
        transform.position = targetPos;
        ApplyOrthographicSize();
    }

    public void OnWorldShift(Vector3 shiftAmount)
    {
        transform.position -= shiftAmount;
        targetPos -= shiftAmount;
    }

    public void SetOrthographicSize(float size)
    {
        orthographicSize = Mathf.Max(0.1f, size);
        ApplyOrthographicSize();
    }

    private Vector3 GetTargetPosition(Transform followTarget)
    {
        return new Vector3(followTarget.position.x + offsetX, followTarget.position.y + offsetY, transform.position.z);
    }

    private void ApplyOrthographicSize()
    {
        if (cam != null && cam.orthographic)
        {
            cam.orthographicSize = orthographicSize;
        }
    }

    private void WarnMissingTargetOnce()
    {
        if (warnedMissingTarget)
        {
            return;
        }

        warnedMissingTarget = true;
        Debug.LogWarning("[StartGameCameraFollow] Assign a target dummy character for the start game camera.", this);
    }
}
