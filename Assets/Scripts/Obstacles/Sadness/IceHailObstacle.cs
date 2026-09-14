using UnityEngine;

[DisallowMultipleComponent]
public class IceHailObstacle : MonoBehaviour
{
    private const string PlayerTag = "Player";

    [Header("Prefabs")]
    [SerializeField] private GameObject indicatorPrefab;
    [SerializeField] private GameObject iceHailPrefab;

    [Header("Indicator")]
    [SerializeField] private float indicatorHeightOffset = 0f;

    [Header("Drop")]
    [Min(0f)] [SerializeField] private float spawnHeightAboveSurface = 12f;
    [Min(0f)] [SerializeField] private float triggerDistance = 8f;
    [Min(0f)] [SerializeField] private float fallSpeed = 14f;
    [SerializeField] private bool hideIndicatorOnDrop = true;

    [Header("Impact")]
    [Min(0)] [SerializeField] private int activeChargeDrain = 3;
    [Range(0f, 1f)] [SerializeField] private float movementSpeedMultiplier = 0.45f;
    [Min(0f)] [SerializeField] private float slowdownDuration = 3f;
    [Min(0f)] [SerializeField] private float impactCheckRadius = 0.75f;
    [Min(0f)] [SerializeField] private float landingCleanupDelay = 0.5f;

    [Header("Emotion")]
    [Min(0f)] [SerializeField] private float emotionIncreaseOnHit = 1f;

    private Transform player;
    private GameObject indicatorInstance;
    private ProximityWarningIndicatorAnimator indicatorAnimator;
    private TerrainFeatureBottomRoot bottomRoot;
    private Vector3 impactPosition;
    private Vector3 impactNormal = Vector3.up;
    private bool dropped;
    private bool warnedMissingPlayerTag;
    private bool warnedMissingHailProjectile;

    private void Awake()
    {
        bottomRoot = GetComponentInChildren<TerrainFeatureBottomRoot>();
    }

    private void Start()
    {
        ResolveImpactPoint();
        SpawnIndicator();
    }

    private void OnValidate()
    {
        spawnHeightAboveSurface = Mathf.Max(0f, spawnHeightAboveSurface);
        triggerDistance = Mathf.Max(0f, triggerDistance);
        fallSpeed = Mathf.Max(0f, fallSpeed);
        activeChargeDrain = Mathf.Max(0, activeChargeDrain);
        movementSpeedMultiplier = Mathf.Clamp01(movementSpeedMultiplier);
        slowdownDuration = Mathf.Max(0f, slowdownDuration);
        impactCheckRadius = Mathf.Max(0f, impactCheckRadius);
        landingCleanupDelay = Mathf.Max(0f, landingCleanupDelay);
        emotionIncreaseOnHit = Mathf.Max(0f, emotionIncreaseOnHit);
    }

    private void Update()
    {
        if (dropped)
        {
            return;
        }

        ResolvePlayer();
        if (player == null)
        {
            return;
        }

        ResolveImpactPoint();
        UpdateIndicatorThreatProgress();
        if (player.position.x >= impactPosition.x - triggerDistance)
        {
            DropHail();
        }
    }

    private void OnDestroy()
    {
        if (indicatorInstance != null)
        {
            Destroy(indicatorInstance);
        }
    }

    private void ResolveImpactPoint()
    {
        if (bottomRoot != null)
        {
            impactPosition = bottomRoot.transform.position;
            impactNormal = ResolveSurfaceNormal(
                impactPosition,
                bottomRoot.transform.up);
            return;
        }

        Vector3 fallbackPosition = GetFallbackImpactPosition();
        if (TerrainManager.Instance != null &&
            TerrainManager.Instance.TryGetSurfaceAtX(
                fallbackPosition.x,
                out Vector3 surfacePosition,
                out Vector3 surfaceNormal))
        {
            impactPosition = surfacePosition;
            impactNormal = surfaceNormal.sqrMagnitude > 0.0001f
                ? surfaceNormal.normalized
                : Vector3.up;
            return;
        }

        impactPosition = fallbackPosition;
        impactNormal = transform.up.sqrMagnitude > 0.0001f
            ? transform.up.normalized
            : Vector3.up;
    }

    private Vector3 ResolveSurfaceNormal(
        Vector3 surfaceReferencePosition,
        Vector3 fallbackNormal)
    {
        if (TerrainManager.Instance != null &&
            TerrainManager.Instance.TryGetSurfaceAtX(
                surfaceReferencePosition.x,
                out _,
                out Vector3 surfaceNormal) &&
            surfaceNormal.sqrMagnitude > 0.0001f)
        {
            return surfaceNormal.normalized;
        }

        return fallbackNormal.sqrMagnitude > 0.0001f
            ? fallbackNormal.normalized
            : Vector3.up;
    }

    private Vector3 GetFallbackImpactPosition()
    {
        return bottomRoot != null ? bottomRoot.transform.position : transform.position;
    }

    private void SpawnIndicator()
    {
        if (indicatorPrefab == null)
        {
            return;
        }

        ResolveImpactPoint();
        Vector3 indicatorPosition =
            impactPosition + Vector3.up * indicatorHeightOffset;
        indicatorInstance = Instantiate(
            indicatorPrefab,
            indicatorPosition,
            GetSurfaceRotation(),
            transform);
        ConfigureIndicatorAnimator();
    }

    private void DropHail()
    {
        dropped = true;
        if (hideIndicatorOnDrop)
        {
            PlayIndicatorDropWarning();
        }
        else if (indicatorAnimator != null)
        {
            indicatorAnimator.SetThreatProgress(1f);
        }

        if (iceHailPrefab == null)
        {
            Debug.LogWarning("[IceHailObstacle] No iceHailPrefab assigned.", this);
            return;
        }

        ResolveImpactPoint();
        Vector3 spawnPosition =
            impactPosition + Vector3.up * spawnHeightAboveSurface;
        Transform hailParent = transform.parent != null ? transform.parent : transform;
        GameObject hailObject = Instantiate(
            iceHailPrefab,
            spawnPosition,
            iceHailPrefab.transform.rotation,
            hailParent);

        IceHailProjectile projectile =
            hailObject.GetComponent<IceHailProjectile>();
        if (projectile == null)
        {
            projectile = hailObject.AddComponent<IceHailProjectile>();
        }

        if (projectile == null)
        {
            WarnMissingHailProjectile(hailObject);
            return;
        }

        projectile.Initialize(
            impactPosition,
            fallSpeed,
            activeChargeDrain,
            movementSpeedMultiplier,
            slowdownDuration,
            impactCheckRadius,
            landingCleanupDelay,
            emotionIncreaseOnHit);
    }

    private Quaternion GetSurfaceRotation()
    {
        ResolveImpactPoint();
        return Quaternion.Euler(
            0f,
            0f,
            Mathf.Atan2(impactNormal.x, impactNormal.y) * Mathf.Rad2Deg * -1f);
    }

    private void ConfigureIndicatorAnimator()
    {
        if (indicatorInstance == null)
        {
            indicatorAnimator = null;
            return;
        }

        indicatorAnimator =
            indicatorInstance.GetComponent<ProximityWarningIndicatorAnimator>();
        if (indicatorAnimator == null)
        {
            indicatorAnimator =
                indicatorInstance.AddComponent<ProximityWarningIndicatorAnimator>();
        }
    }

    private void UpdateIndicatorThreatProgress()
    {
        if (indicatorAnimator == null || player == null)
        {
            return;
        }

        float distanceToImpact = Mathf.Max(0f, impactPosition.x - player.position.x);
        indicatorAnimator.SetThreatDistance(distanceToImpact, triggerDistance);
    }

    private void PlayIndicatorDropWarning()
    {
        if (indicatorInstance == null)
        {
            return;
        }

        if (indicatorAnimator == null)
        {
            indicatorAnimator =
                indicatorInstance.GetComponent<ProximityWarningIndicatorAnimator>();
        }

        if (indicatorAnimator != null)
        {
            indicatorAnimator.PlayFinalWarningAndDestroy();
            return;
        }

        Destroy(indicatorInstance);
        indicatorInstance = null;
    }

    private void ResolvePlayer()
    {
        if (player != null)
        {
            return;
        }

        try
        {
            GameObject playerObject =
                GameObject.FindGameObjectWithTag(PlayerTag);
            player = playerObject != null
                ? playerObject.transform
                : null;
        }
        catch (UnityException)
        {
            if (warnedMissingPlayerTag)
            {
                return;
            }

            Debug.LogWarning(
                $"[IceHailObstacle] No Unity tag named '{PlayerTag}' exists.",
                this);
            warnedMissingPlayerTag = true;
        }
    }

    private void WarnMissingHailProjectile(GameObject hailObject)
    {
        if (warnedMissingHailProjectile)
        {
            return;
        }

        Debug.LogWarning(
            "[IceHailObstacle] Ice hail prefab must be able to use IceHailProjectile.",
            hailObject != null ? hailObject : gameObject);
        warnedMissingHailProjectile = true;
    }

}
