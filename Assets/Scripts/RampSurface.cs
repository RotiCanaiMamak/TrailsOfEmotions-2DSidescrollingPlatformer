using UnityEngine;

[DisallowMultipleComponent]
public sealed class RampSurface : MonoBehaviour
{
    [SerializeField] private float minimumNormalY = 0.1f;

    public bool IsWalkable(RaycastHit2D hit)
    {
        return hit.collider != null && hit.normal.y >= minimumNormalY;
    }

    private void OnValidate()
    {
        minimumNormalY = Mathf.Clamp(minimumNormalY, -1f, 1f);
    }
}
