using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GroundingRegulation : MonoBehaviour, IRegulationSpawnPlacement
{
    [Header("Circle Prefab")]
    [SerializeField] private GroundingCircle circlePrefab;

    [Header("First Circle Placement")]
    [Range(0f, 1f)] [SerializeField] private float firstCircleSurfaceT = 0.5f;
    [Tooltip("World-space X offset from the sampled terrain point for circle 1. Positive moves right, negative moves left.")]
    [SerializeField] private float firstCircleWorldXOffset;
    [Min(0f)] [SerializeField] private float firstCircleHeightAboveSurface = 3f;

    [Header("Next Circle Placement")]
    [SerializeField] private Vector2 nextCircleOffsetFromPlayer = new Vector2(6f, 2f);

    [Header("Circle Sequence")]
    [Min(1)] [SerializeField] private int circleCount = 5;
    [Min(0f)] [SerializeField] private float fadeDuration = 0.2f;
    [Min(0f)] [SerializeField] private float fifthCircleBaseScaleMultiplier = 1.5f;

    [Header("Launch")]
    [SerializeField] private float[] circleForwardLaunchSpeeds = { 12f, 14f, 16f, 18f };
    [SerializeField] private float[] circleUpwardLaunchSpeeds = { 8f, 9f, 10f, 11f };
    [Min(0f)] [SerializeField] private float circleLaunchAdhesionLockDuration = 0.12f;
    [Min(0f)] [SerializeField] private float finalForwardLaunchSpeed = 24f;
    [Min(0f)] [SerializeField] private float finalUpwardLaunchSpeed = 10f;
    [Min(0f)] [SerializeField] private float finalLaunchAdhesionLockDuration = 0.12f;

    [Header("Emotion")]
    [SerializeField] private float[] emotionReductions = { 4f, 5f, 6f, 7f, 8f };

    [Header("Final Clear")]
    [Min(0f)] [SerializeField] private float obstacleClearRadius = 4f;
    [SerializeField] private LayerMask obstacleClearLayerMask = ~0;
    [Min(1)] [SerializeField] private int obstacleClearBufferSize = 32;

    private readonly HashSet<int> clearedObjectIds = new HashSet<int>();
    private Collider2D[] obstacleClearHits;
    private int nextCircleIndex;
    private bool completed;

    private void OnValidate()
    {
        firstCircleSurfaceT = Mathf.Clamp01(firstCircleSurfaceT);
        firstCircleHeightAboveSurface = Mathf.Max(0f, firstCircleHeightAboveSurface);
        circleCount = Mathf.Max(1, circleCount);
        fadeDuration = Mathf.Max(0f, fadeDuration);
        fifthCircleBaseScaleMultiplier = Mathf.Max(0f, fifthCircleBaseScaleMultiplier);
        circleLaunchAdhesionLockDuration = Mathf.Max(0f, circleLaunchAdhesionLockDuration);
        finalForwardLaunchSpeed = Mathf.Max(0f, finalForwardLaunchSpeed);
        finalUpwardLaunchSpeed = Mathf.Max(0f, finalUpwardLaunchSpeed);
        finalLaunchAdhesionLockDuration = Mathf.Max(0f, finalLaunchAdhesionLockDuration);
        obstacleClearRadius = Mathf.Max(0f, obstacleClearRadius);
        obstacleClearBufferSize = Mathf.Max(1, obstacleClearBufferSize);
    }

    public bool PlaceOnChunk(TerrainChunk chunk)
    {
        if (chunk == null || circlePrefab == null)
        {
            return false;
        }

        if (!chunk.EvaluateSurface(firstCircleSurfaceT, out Vector3 surfacePosition, out Vector3 surfaceNormal))
        {
            return false;
        }

        Vector3 circleSurfacePosition = surfacePosition;
        Vector3 circleSurfaceNormal = surfaceNormal;
        if (!Mathf.Approximately(firstCircleWorldXOffset, 0f))
        {
            float targetWorldX = surfacePosition.x + firstCircleWorldXOffset;
            if (TerrainManager.Instance != null &&
                TerrainManager.Instance.TryGetSurfaceAtX(targetWorldX, out Vector3 offsetSurfacePosition, out Vector3 offsetSurfaceNormal))
            {
                circleSurfacePosition = offsetSurfacePosition;
                circleSurfaceNormal = offsetSurfaceNormal;
            }
            else
            {
                circleSurfacePosition = surfacePosition + Vector3.right * firstCircleWorldXOffset;
            }
        }

        transform.position = circleSurfacePosition;
        transform.rotation = Quaternion.identity;
        nextCircleIndex = 0;
        completed = false;

        Vector3 circlePosition = circleSurfacePosition + SafeNormal(circleSurfaceNormal) * firstCircleHeightAboveSurface;
        SpawnCircle(circlePosition, chunk.transform);
        return true;
    }

    public void HandleCircleCompleted(GroundingCircle circle, PlayerController player, Vector3 circlePosition)
    {
        if (completed || circle == null || player == null)
        {
            return;
        }

        int completedIndex = circle.CircleIndex;
        ReduceEmotion(completedIndex);

        if (completedIndex >= circleCount - 1)
        {
            CompleteSequence(player, circlePosition);
            return;
        }

        Vector3 nextCirclePosition = GetNextCirclePosition(player);
        Transform nextParent = ResolveCircleParent(nextCirclePosition);
        ApplyCircleLaunch(player, completedIndex);
        SpawnCircle(nextCirclePosition, nextParent);
    }

    private void SpawnCircle(Vector3 worldPosition, Transform parent)
    {
        if (circlePrefab == null || nextCircleIndex >= circleCount)
        {
            return;
        }

        GroundingCircle circle = Instantiate(
            circlePrefab,
            worldPosition,
            circlePrefab.transform.rotation,
            parent != null ? parent : transform);

        bool isFinalCircle = nextCircleIndex == circleCount - 1;
        float baseScale = isFinalCircle ? fifthCircleBaseScaleMultiplier : 1f;
        circle.Initialize(
            this,
            nextCircleIndex,
            false,
            fadeDuration,
            0f,
            baseScale,
            baseScale);

        nextCircleIndex++;
    }

    private Vector3 GetNextCirclePosition(PlayerController player)
    {
        Vector3 playerPosition = player != null ? player.transform.position : transform.position;
        return playerPosition + new Vector3(nextCircleOffsetFromPlayer.x, nextCircleOffsetFromPlayer.y, 0f);
    }

    private Transform ResolveCircleParent(Vector3 worldPosition)
    {
        TerrainChunk chunk = FindSurfaceChunkAtX(worldPosition.x);
        return chunk != null ? chunk.transform : transform;
    }

    private TerrainChunk FindSurfaceChunkAtX(float worldX)
    {
        TerrainManager terrainManager = TerrainManager.Instance;
        if (terrainManager == null)
        {
            return null;
        }

        TerrainChunk[] chunks = terrainManager.GetComponentsInChildren<TerrainChunk>();
        for (int i = 0; i < chunks.Length; i++)
        {
            TerrainChunk chunk = chunks[i];
            if (chunk == null || !chunk.ProvidesGameplaySurface || chunk.chunkWidth <= 0f)
            {
                continue;
            }

            float left = chunk.transform.position.x;
            float right = left + chunk.chunkWidth;
            if (worldX >= left - 0.001f && worldX <= right + 0.001f)
            {
                return chunk;
            }
        }

        return null;
    }

    private void ReduceEmotion(int circleIndex)
    {
        float reduction = GetConfiguredValue(emotionReductions, circleIndex, 0f);
        if (EmotionMeter.Instance != null && reduction > 0f)
        {
            EmotionMeter.Instance.AddEmotion(-reduction);
        }
    }

    private void ApplyCircleLaunch(PlayerController player, int circleIndex)
    {
        if (player == null)
        {
            return;
        }

        float forwardSpeed = GetConfiguredValue(circleForwardLaunchSpeeds, circleIndex, 0f);
        float upwardSpeed = GetConfiguredValue(circleUpwardLaunchSpeeds, circleIndex, 0f);
        player.Launch(upwardSpeed, forwardSpeed, circleLaunchAdhesionLockDuration, false);
    }

    private void CompleteSequence(PlayerController player, Vector3 circlePosition)
    {
        completed = true;
        PlayShockwave(circlePosition);
        ClearNearbyObstacles(circlePosition);
        player.Launch(finalUpwardLaunchSpeed, finalForwardLaunchSpeed, finalLaunchAdhesionLockDuration, false);
        Destroy(gameObject, fadeDuration + 0.05f);
    }

    private void PlayShockwave(Vector3 worldPosition)
    {
        if (LandmineShockwaveManager.Instance != null)
        {
            LandmineShockwaveManager.Instance.PlayAt(worldPosition);
        }
    }

    private void ClearNearbyObstacles(Vector3 center)
    {
        if (obstacleClearRadius <= 0f)
        {
            return;
        }

        EnsureClearBuffer();
        clearedObjectIds.Clear();

        ContactFilter2D clearFilter = new ContactFilter2D();
        clearFilter.SetLayerMask(obstacleClearLayerMask);
        clearFilter.useTriggers = true;

        int hitCount = Physics2D.OverlapCircle(
            center,
            obstacleClearRadius,
            clearFilter,
            obstacleClearHits);

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = obstacleClearHits[i];
            if (hit == null || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            IRegulationClearable clearable = hit.GetComponentInParent<IRegulationClearable>();
            Object clearableObject = clearable as Object;
            if (clearableObject == null || !clearedObjectIds.Add(clearableObject.GetInstanceID()))
            {
                continue;
            }

            clearable.TryClearByRegulation(this);
        }
    }

    private void EnsureClearBuffer()
    {
        if (obstacleClearHits == null || obstacleClearHits.Length != obstacleClearBufferSize)
        {
            obstacleClearHits = new Collider2D[obstacleClearBufferSize];
        }
    }

    private static float GetConfiguredValue(float[] values, int index, float fallback)
    {
        if (values == null || values.Length == 0)
        {
            return fallback;
        }

        int clampedIndex = Mathf.Clamp(index, 0, values.Length - 1);
        return Mathf.Max(0f, values[clampedIndex]);
    }

    private static Vector3 SafeNormal(Vector3 normal)
    {
        return normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
    }
}
