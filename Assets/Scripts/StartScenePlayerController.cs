using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class StartScenePlayerController : MonoBehaviour
{
    [Header("Terrain")]
    [SerializeField] private StartSceneVisualTerrainScroller terrainScroller;

    [Header("Ground Check")]
    public Vector2 groundCheckOffset = new Vector2(0f, -0.55f);
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer;

    [Header("Surface Adhesion")]
    public float raycastDistance = 3f;
    public float adhesionForce = 10f;

    private Rigidbody2D rb;
    private bool isGrounded;
    private Vector2 surfaceNormal = Vector2.up;
    private float initialGravityScale;

    public bool IsGrounded => isGrounded;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        initialGravityScale = rb.gravityScale;
        SetRotationLocked(true);
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        ResolveTerrainScroller();
    }

    private void Update()
    {
        CheckGrounded();
    }

    private void FixedUpdate()
    {
        if (isGrounded)
        {
            SetRotationLocked(true);
            ScanSurface();
            AlignToSurface();
            ApplySurfaceAdhesion();
            return;
        }

        rb.gravityScale = initialGravityScale;
        SetRotationLocked(true);
    }

    private void OnValidate()
    {
        groundCheckRadius = Mathf.Max(0f, groundCheckRadius);
        raycastDistance = Mathf.Max(0f, raycastDistance);
        adhesionForce = Mathf.Max(0f, adhesionForce);
    }

    private void ResolveTerrainScroller()
    {
        if (terrainScroller == null)
        {
            terrainScroller = FindFirstObjectByType<StartSceneVisualTerrainScroller>();
        }
    }

    private void ScanSurface()
    {
        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, raycastDistance, groundLayer);
        ResolveTerrainScroller();

        if (terrainScroller != null &&
            terrainScroller.TryGetSurfaceAtX(transform.position.x, out _, out Vector3 exactNormal))
        {
            surfaceNormal = exactNormal;
            return;
        }

        surfaceNormal = hit.collider != null ? hit.normal : Vector2.up;
    }

    private void CheckGrounded()
    {
        bool wasGrounded = isGrounded;
        Vector2 checkPos = (Vector2)transform.position + groundCheckOffset;
        bool rawGrounded = false;
        Collider2D[] groundHits = Physics2D.OverlapCircleAll(checkPos, groundCheckRadius, groundLayer);

        for (int i = 0; i < groundHits.Length; i++)
        {
            if (groundHits[i] == null)
            {
                continue;
            }

            rawGrounded = true;
            break;
        }

        isGrounded = rawGrounded;

        if (!wasGrounded && isGrounded)
        {
            OnLanded();
        }
    }

    private void OnLanded()
    {
        ScanSurface();
        SnapToSurfaceIfPossible();
    }

    private void ApplySurfaceAdhesion()
    {
        rb.gravityScale = 0f;

        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, raycastDistance, groundLayer);
        if (hit.collider == null)
        {
            return;
        }

        float targetY = hit.point.y - groundCheckOffset.y;
        float error = targetY - transform.position.y;
        float adhesionAcceleration = Mathf.Clamp(error / Time.fixedDeltaTime, -Mathf.Max(0f, adhesionForce), 2f);
        rb.AddForce(Vector2.up * adhesionAcceleration * rb.mass, ForceMode2D.Force);
    }

    private void AlignToSurface()
    {
        float targetAngle = Mathf.Atan2(surfaceNormal.x, surfaceNormal.y) * Mathf.Rad2Deg * -1f;
        SetPlayerRotation(targetAngle);
    }

    private void SnapToSurfaceIfPossible()
    {
        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, raycastDistance, groundLayer);
        if (hit.collider == null)
        {
            return;
        }

        float targetY = hit.point.y - groundCheckOffset.y;
        rb.position = new Vector2(rb.position.x, targetY);
    }

    private void SetRotationLocked(bool locked)
    {
        if (rb == null)
        {
            return;
        }

        rb.freezeRotation = locked;
        if (locked)
        {
            rb.angularVelocity = 0f;
        }
    }

    private void SetPlayerRotation(float zAngle)
    {
        transform.rotation = Quaternion.Euler(0f, 0f, zAngle);
    }
}
