using UnityEngine;
using UnityEngine.Serialization;

public class ParallaxScroller : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField] private Transform cameraTransform;

    [Header("Parallax")]
    [Tooltip("0 = stays fixed in world space, 1 = follows the camera exactly.")]
    [SerializeField] private Vector2 parallaxMultiplier = new Vector2(0.5f, 1f);

    [Header("Infinite Scroll")]
    [SerializeField] private Transform leftBackground;
    [SerializeField] private Transform centerBackground;
    [SerializeField] private Transform rightBackground;
    [FormerlySerializedAs("loopSizeX")]
    [Min(0f)]
    [SerializeField] private float tileWidth = 0f;

    private Vector3 startPosition;
    private Vector3 cameraStartPosition;
    private bool warnedInvalidSetup;

    private void Awake()
    {
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        startPosition = transform.position;

        if (cameraTransform != null)
        {
            cameraStartPosition = cameraTransform.position;
        }

        AlignBackgrounds();
    }

    private void LateUpdate()
    {
        if (cameraTransform == null)
        {
            return;
        }

        Vector3 cameraDelta = cameraTransform.position - cameraStartPosition;
        Vector3 targetPosition = startPosition + new Vector3(
            cameraDelta.x * parallaxMultiplier.x,
            cameraDelta.y * parallaxMultiplier.y,
            0f
        );

        transform.position = new Vector3(targetPosition.x, targetPosition.y, startPosition.z);

        RecycleBackgrounds();
    }

    public void OnWorldShift(Vector3 shift)
    {
        transform.position -= shift;
        startPosition -= shift;
        cameraStartPosition -= shift;
    }

    private void AlignBackgrounds()
    {
        if (!HasValidScrollSetup())
        {
            return;
        }

        SetLocalX(leftBackground, centerBackground.localPosition.x - tileWidth);
        SetLocalX(rightBackground, centerBackground.localPosition.x + tileWidth);
    }

    private void RecycleBackgrounds()
    {
        if (!HasValidScrollSetup())
        {
            return;
        }

        float cameraLocalX = transform.InverseTransformPoint(cameraTransform.position).x;

        while (cameraLocalX > GetRightmostBackground().localPosition.x)
        {
            Transform leftmost = GetLeftmostBackground();
            Transform rightmost = GetRightmostBackground();
            SetLocalX(leftmost, rightmost.localPosition.x + tileWidth);
        }

        while (cameraLocalX < GetLeftmostBackground().localPosition.x)
        {
            Transform leftmost = GetLeftmostBackground();
            Transform rightmost = GetRightmostBackground();
            SetLocalX(rightmost, leftmost.localPosition.x - tileWidth);
        }
    }

    private bool HasValidScrollSetup()
    {
        if (tileWidth > 0f && leftBackground != null && centerBackground != null && rightBackground != null)
        {
            return true;
        }

        if (!warnedInvalidSetup)
        {
            Debug.LogWarning($"{nameof(ParallaxScroller)} on {name} needs left, center, right backgrounds and a tile width greater than 0 to recycle tiles.", this);
            warnedInvalidSetup = true;
        }

        return false;
    }

    private Transform GetLeftmostBackground()
    {
        Transform leftmost = leftBackground;

        if (centerBackground.localPosition.x < leftmost.localPosition.x)
        {
            leftmost = centerBackground;
        }

        if (rightBackground.localPosition.x < leftmost.localPosition.x)
        {
            leftmost = rightBackground;
        }

        return leftmost;
    }

    private Transform GetRightmostBackground()
    {
        Transform rightmost = leftBackground;

        if (centerBackground.localPosition.x > rightmost.localPosition.x)
        {
            rightmost = centerBackground;
        }

        if (rightBackground.localPosition.x > rightmost.localPosition.x)
        {
            rightmost = rightBackground;
        }

        return rightmost;
    }

    private void SetLocalX(Transform target, float x)
    {
        Vector3 localPosition = target.localPosition;
        localPosition.x = x;
        target.localPosition = localPosition;
    }
}
