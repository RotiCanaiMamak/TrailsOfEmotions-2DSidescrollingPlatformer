using UnityEngine;

[DisallowMultipleComponent]
public sealed class JumpAssistObstacleSpawner : MonoBehaviour
{
    [Header("Placement")]
    [Tooltip("World-space X offset from this obstacle used to place the ramp. Negative values can place it on a previous chunk.")]
    [SerializeField] private float rampSurfaceXOffset = -1.5f;

    [Header("Ramp Suppression")]
    [SerializeField] private bool suppressRampOnDownSlopeBehind = true;
    [Min(0.01f)] [SerializeField] private float downSlopeSampleDistance = 1f;
    [Min(0f)] [SerializeField] private float downSlopeMinDrop = 0.05f;
    [SerializeField] private bool suppressRampWhenObstacleRampBehind = true;
    [Min(0)] [SerializeField] private int obstacleRampLookbackChunks = 2;

    [Header("Spawns")]
    [SerializeField] private JumpAssistSpawnSettings spawnSettings = new JumpAssistSpawnSettings();

    private bool hasSpawned;

    private void Start()
    {
        TrySpawnJumpAssist();
    }

    private void OnValidate()
    {
        downSlopeSampleDistance = Mathf.Max(0.01f, downSlopeSampleDistance);
        downSlopeMinDrop = Mathf.Max(0f, downSlopeMinDrop);
        obstacleRampLookbackChunks = Mathf.Max(0, obstacleRampLookbackChunks);
        spawnSettings?.OnValidate();
    }

    private void TrySpawnJumpAssist()
    {
        if (hasSpawned || spawnSettings == null)
        {
            return;
        }

        TerrainChunk ownerChunk = GetComponentInParent<TerrainChunk>();
        if (ownerChunk == null)
        {
            return;
        }

        float surfaceWorldX = transform.position.x + rampSurfaceXOffset;

        if (!TryEvaluateTerrainSurfaceAtWorldX(
                ownerChunk,
                surfaceWorldX,
                out TerrainChunk surfaceChunk,
                out Vector3 surfacePosition,
                out Vector3 surfaceNormal))
        {
            return;
        }

        bool allowRamp = ShouldAllowRamp(ownerChunk, surfaceWorldX);
        GameObject rampPrefab = ResolveJumpAssistRampPrefab(surfaceChunk, ownerChunk);
        float orbSurfaceWorldX = surfaceWorldX + spawnSettings.OrbPatternWorldXOffset;
        bool useOrbSurfaceOverride = TryEvaluateTerrainSurfaceAtWorldX(
            ownerChunk,
            orbSurfaceWorldX,
            out TerrainChunk orbSurfaceChunk,
            out Vector3 orbSurfacePosition,
            out Vector3 orbSurfaceNormal);

        hasSpawned = spawnSettings.TrySpawn(
            surfaceChunk,
            rampPrefab,
            surfacePosition,
            surfaceNormal,
            out _,
            out _,
            allowRamp,
            JumpAssistRampMarker.Source.Obstacle,
            useOrbSurfaceOverride,
            orbSurfaceChunk,
            orbSurfacePosition,
            orbSurfaceNormal);
    }

    private static GameObject ResolveJumpAssistRampPrefab(TerrainChunk surfaceChunk, TerrainChunk ownerChunk)
    {
        BiomeData biome = surfaceChunk != null && surfaceChunk.activeBiome != null
            ? surfaceChunk.activeBiome
            : ownerChunk != null ? ownerChunk.activeBiome : null;

        return biome != null ? biome.jumpAssistRampPrefab : null;
    }

    private bool ShouldAllowRamp(TerrainChunk ownerChunk, float surfaceWorldX)
    {
        if (suppressRampOnDownSlopeBehind && HasDownSlopeBehind(ownerChunk, surfaceWorldX))
        {
            return false;
        }

        return !suppressRampWhenObstacleRampBehind || !HasObstacleRampBehind(ownerChunk);
    }

    private bool HasDownSlopeBehind(TerrainChunk ownerChunk, float surfaceWorldX)
    {
        float sampleDistance = Mathf.Max(0.01f, downSlopeSampleDistance);
        if (!TrySampleTerrainSurfaceY(
                ownerChunk,
                surfaceWorldX - sampleDistance,
                out float leftY) ||
            !TrySampleTerrainSurfaceY(
                ownerChunk,
                surfaceWorldX + sampleDistance,
                out float rightY))
        {
            return false;
        }

        return leftY - rightY >= Mathf.Max(0f, downSlopeMinDrop);
    }

    private bool HasObstacleRampBehind(TerrainChunk ownerChunk)
    {
        if (ownerChunk == null || obstacleRampLookbackChunks <= 0)
        {
            return false;
        }

        float lookbackDistance = Mathf.Max(0f, ownerChunk.chunkWidth) * obstacleRampLookbackChunks;
        if (lookbackDistance <= 0f)
        {
            return false;
        }

        float obstacleX = transform.position.x;
        JumpAssistRampMarker[] rampMarkers =
            Object.FindObjectsByType<JumpAssistRampMarker>(FindObjectsSortMode.None);

        for (int i = 0; i < rampMarkers.Length; i++)
        {
            JumpAssistRampMarker marker = rampMarkers[i];
            if (marker == null || marker.RampSource != JumpAssistRampMarker.Source.Obstacle)
            {
                continue;
            }

            float rampX = marker.transform.position.x;
            float distanceBehind = obstacleX - rampX;
            if (distanceBehind > 0f && distanceBehind <= lookbackDistance)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TrySampleTerrainSurfaceY(
        TerrainChunk fallbackChunk,
        float worldX,
        out float surfaceY)
    {
        TerrainManager terrainManager = fallbackChunk != null
            ? fallbackChunk.GetComponentInParent<TerrainManager>()
            : TerrainManager.Instance;

        if (terrainManager != null &&
            terrainManager.TryGetSurfaceAtX(worldX, out Vector3 managerPosition, out _))
        {
            surfaceY = managerPosition.y;
            return true;
        }

        if (TryEvaluateTerrainSurfaceAtWorldX(
                fallbackChunk,
                worldX,
                out _,
                out Vector3 surfacePosition,
                out _))
        {
            surfaceY = surfacePosition.y;
            return true;
        }

        surfaceY = 0f;
        return false;
    }

    private static bool TryEvaluateTerrainSurfaceAtWorldX(
        TerrainChunk fallbackChunk,
        float worldX,
        out TerrainChunk surfaceChunk,
        out Vector3 surfacePosition,
        out Vector3 surfaceNormal)
    {
        surfaceChunk = FindSurfaceChunkAtWorldX(fallbackChunk, worldX);
        if (surfaceChunk == null)
        {
            surfacePosition = default;
            surfaceNormal = Vector3.up;
            return false;
        }

        float chunkLeft = surfaceChunk.transform.position.x;
        float chunkRight = chunkLeft + surfaceChunk.chunkWidth;
        float surfaceT = 1f - Mathf.InverseLerp(chunkLeft, chunkRight, worldX);
        return surfaceChunk.EvaluateSurface(surfaceT, out surfacePosition, out surfaceNormal);
    }

    private static TerrainChunk FindSurfaceChunkAtWorldX(TerrainChunk fallbackChunk, float worldX)
    {
        TerrainManager terrainManager = fallbackChunk != null
            ? fallbackChunk.GetComponentInParent<TerrainManager>()
            : TerrainManager.Instance;

        if (terrainManager != null)
        {
            TerrainChunk[] chunks = terrainManager.GetComponentsInChildren<TerrainChunk>();
            for (int i = 0; i < chunks.Length; i++)
            {
                if (ContainsWorldX(chunks[i], worldX))
                {
                    return chunks[i];
                }
            }
        }

        return ContainsWorldX(fallbackChunk, worldX) ? fallbackChunk : null;
    }

    private static bool ContainsWorldX(TerrainChunk chunk, float worldX)
    {
        if (chunk == null || !chunk.ProvidesGameplaySurface || chunk.chunkWidth <= 0f)
        {
            return false;
        }

        const float EdgeTolerance = 0.001f;
        float chunkLeft = chunk.transform.position.x;
        float chunkRight = chunkLeft + chunk.chunkWidth;
        return worldX >= chunkLeft - EdgeTolerance && worldX <= chunkRight + EdgeTolerance;
    }
}
