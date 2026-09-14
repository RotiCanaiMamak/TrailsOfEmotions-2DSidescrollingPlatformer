using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class DroppingDebrisObstacle : MonoBehaviour
{
    [Header("Collision")]
    [SerializeField] private Collider2D debrisCollider;

    [Header("Movement")]
    [Min(0f)] [SerializeField] private float leftSpeed = 4f;
    [SerializeField] private float rotationSpeed = 360f;

    [Header("Slope Limit")]
    [SerializeField] private bool followGeneratedTerrain = true;
    [Range(0f, 89f)] [SerializeField] private float maxUphillSlopeAngle = 25f;
    [Min(0.01f)] [SerializeField] private float uphillLookAheadDistance = 0.5f;

    [Header("Slowdown")]
    [Min(0f)] [SerializeField] private float speedMultiplier = 0.35f;
    [Min(0f)] [SerializeField] private float slowDuration = 0.5f;

    [Header("Impact Shake")]
    [SerializeField] private bool shakeCameraOnHit;
    [Min(0f)] [SerializeField] private float hitShakeAmplitude = 0.9f;
    [Min(0f)] [SerializeField] private float hitShakeDuration = 0.32f;

    [Header("Emotion")]
    [Min(0f)] [SerializeField] private float emotionIncreaseOnHit = 1f;

    [Header("Destroy Effects")]
    [SerializeField] private ObstacleDestroyEffects destroyEffects;

    [Header("Sprite Variant")]
    [SerializeField] private ObstacleSpriteVariantRandomizer spriteVariantRandomizer;

    private bool consumed;
    private TerrainFeatureBottomRoot bottomRoot;
    private Vector3 terrainSurfaceOffset;
    private bool hasTerrainSurfaceOffset;

    private void Reset()
    {
        ResolveCollider();
        ResolveDestroyEffects();
        ResolveSpriteVariantRandomizer();
        ResolveBottomRoot();
    }

    private void Awake()
    {
        ResolveCollider();
        ResolveDestroyEffects();
        ResolveSpriteVariantRandomizer();
        ResolveBottomRoot();
    }

    private void OnValidate()
    {
        leftSpeed = Mathf.Max(0f, leftSpeed);
        speedMultiplier = Mathf.Max(0f, speedMultiplier);
        slowDuration = Mathf.Max(0f, slowDuration);
        hitShakeAmplitude = Mathf.Max(0f, hitShakeAmplitude);
        hitShakeDuration = Mathf.Max(0f, hitShakeDuration);
        emotionIncreaseOnHit = Mathf.Max(0f, emotionIncreaseOnHit);
        uphillLookAheadDistance = Mathf.Max(0.01f, uphillLookAheadDistance);
        ResolveCollider();
        ResolveDestroyEffects();
        ResolveSpriteVariantRandomizer();
        ResolveBottomRoot();
    }

    private void Update()
    {
        if (consumed)
        {
            return;
        }

        float deltaTime = Time.deltaTime;
        float moveDistance = leftSpeed * deltaTime;
        if (moveDistance <= 0f)
        {
            return;
        }

        if (followGeneratedTerrain && TryMoveAlongGeneratedTerrain(moveDistance, deltaTime))
        {
            return;
        }

        transform.position += Vector3.left * moveDistance;
        Roll(deltaTime);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleTouch(collision.collider);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        HandleTouch(collision.collider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleTouch(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        HandleTouch(other);
    }

    internal void HandleTouch(Collider2D other)
    {
        if (consumed || other == null)
        {
            return;
        }

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || !player.IsCharacterCollider(other))
        {
            return;
        }

        player.ApplyTimedMovementModifier(
            this,
            Mathf.Max(0f, speedMultiplier),
            1f,
            Mathf.Max(0f, slowDuration));

        ShakeCameraOnHit();
        AddHitEmotion();
        Consume();
    }

    private void Consume()
    {
        consumed = true;
        SetDebrisColliderEnabled(false);
        HideDebrisRenderers();
        PlayDestroyEffects();
    }

    private void AddHitEmotion()
    {
        if (EmotionMeter.Instance != null && emotionIncreaseOnHit > 0f)
        {
            EmotionMeter.Instance.AddObstacleEmotion(emotionIncreaseOnHit);
        }
    }

    private void ShakeCameraOnHit()
    {
        if (!shakeCameraOnHit)
        {
            return;
        }

        Camera mainCamera = Camera.main;
        mainCamera?.GetComponent<CameraFollow>()?.Shake(hitShakeAmplitude, hitShakeDuration);
    }

    private void SetDebrisColliderEnabled(bool enabled)
    {
        if (debrisCollider != null)
        {
            debrisCollider.enabled = enabled;
        }
    }

    private void PlayDestroyEffects()
    {
        destroyEffects?.PlayAtAssignedTransforms();
    }

    private bool TryMoveAlongGeneratedTerrain(float moveDistance, float deltaTime)
    {
        if (TerrainManager.Instance == null)
        {
            return false;
        }

        Vector3 contactPoint = GetContactPoint();
        if (!TerrainManager.Instance.TryGetSurfaceAtX(contactPoint.x, out Vector3 currentSurface, out _))
        {
            return false;
        }

        CaptureTerrainSurfaceOffset(currentSurface);

        float lookAheadDistance = Mathf.Max(moveDistance, uphillLookAheadDistance);
        float lookAheadX = contactPoint.x - lookAheadDistance;
        if (TerrainManager.Instance.TryGetSurfaceAtX(lookAheadX, out Vector3 lookAheadSurface, out _) &&
            IsBlockedByUphillSlope(currentSurface, lookAheadSurface, lookAheadDistance))
        {
            return true;
        }

        float nextContactX = contactPoint.x - moveDistance;
        if (!TerrainManager.Instance.TryGetSurfaceAtX(nextContactX, out Vector3 nextSurface, out _))
        {
            return false;
        }

        MoveContactPointToSurface(nextSurface);
        Roll(deltaTime);
        MoveContactPointToSurface(nextSurface);
        return true;
    }

    private bool IsBlockedByUphillSlope(
        Vector3 currentSurface,
        Vector3 nextSurface,
        float horizontalDistance)
    {
        float uphillDelta = nextSurface.y - currentSurface.y;
        if (uphillDelta <= 0f)
        {
            return false;
        }

        float uphillAngle = Mathf.Atan2(uphillDelta, Mathf.Max(0.01f, horizontalDistance)) * Mathf.Rad2Deg;
        return uphillAngle > maxUphillSlopeAngle;
    }

    private Vector3 GetContactPoint()
    {
        return bottomRoot != null ? bottomRoot.transform.position : transform.position;
    }

    private void MoveContactPointToSurface(Vector3 surfacePosition)
    {
        if (bottomRoot != null)
        {
            transform.position += surfacePosition - bottomRoot.transform.position;
            return;
        }

        transform.position = surfacePosition + terrainSurfaceOffset;
    }

    private void CaptureTerrainSurfaceOffset(Vector3 surfacePosition)
    {
        if (bottomRoot != null || hasTerrainSurfaceOffset)
        {
            return;
        }

        terrainSurfaceOffset = transform.position - surfacePosition;
        hasTerrainSurfaceOffset = true;
    }

    private void Roll(float deltaTime)
    {
        transform.Rotate(0f, 0f, rotationSpeed * deltaTime);
    }

    private void HideDebrisRenderers()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer rendererToHide = renderers[i];
            if (rendererToHide == null || IsDestroyEffectChild(rendererToHide.transform))
            {
                continue;
            }

            rendererToHide.enabled = false;
        }
    }

    private bool IsDestroyEffectChild(Transform candidate)
    {
        return destroyEffects != null && destroyEffects.IsEffectChild(candidate);
    }

    private void ResolveCollider()
    {
        if (debrisCollider == null)
        {
            debrisCollider = GetComponent<Collider2D>();
        }
    }

    private void ResolveDestroyEffects()
    {
        if (destroyEffects == null)
        {
            destroyEffects = GetComponent<ObstacleDestroyEffects>();
        }
    }

    private void ResolveSpriteVariantRandomizer()
    {
        if (spriteVariantRandomizer == null)
        {
            spriteVariantRandomizer =
                GetComponentInChildren<ObstacleSpriteVariantRandomizer>(true);
        }
    }

    private void ResolveBottomRoot()
    {
        if (bottomRoot == null)
        {
            bottomRoot = GetComponentInChildren<TerrainFeatureBottomRoot>();
        }
    }
}
