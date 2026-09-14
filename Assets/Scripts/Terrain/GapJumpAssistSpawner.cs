using UnityEngine;

[DisallowMultipleComponent]
public sealed class GapJumpAssistSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TerrainManager terrainManager;

    [Header("Gap Placement")]
    [Tooltip("Surface position on the last non-gap chunk. TerrainChunk uses 0 = right edge and 1 = left edge.")]
    [Range(0f, 1f)] [SerializeField] private float previousChunkSurfaceT = 0.1f;

    [Header("Spawns")]
    [SerializeField] private JumpAssistSpawnSettings spawnSettings = new JumpAssistSpawnSettings();

    private TerrainChunk lastNonGapChunk;
    private bool spawnedForCurrentGapRun;
    private bool subscribed;

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void Start()
    {
        TrySubscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnValidate()
    {
        previousChunkSurfaceT = Mathf.Clamp01(previousChunkSurfaceT);
        spawnSettings?.OnValidate();
    }

    private void TrySubscribe()
    {
        if (subscribed)
        {
            return;
        }

        if (terrainManager == null)
        {
            terrainManager = GetComponent<TerrainManager>();
        }

        if (terrainManager == null)
        {
            terrainManager = TerrainManager.Instance;
        }

        if (terrainManager == null)
        {
            return;
        }

        terrainManager.ChunkSpawned += HandleChunkSpawned;
        terrainManager.ChunkDestroyed += HandleChunkDestroyed;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || terrainManager == null)
        {
            subscribed = false;
            terrainManager = null;
            return;
        }

        terrainManager.ChunkSpawned -= HandleChunkSpawned;
        terrainManager.ChunkDestroyed -= HandleChunkDestroyed;
        subscribed = false;
        terrainManager = null;
    }

    private void HandleChunkSpawned(TerrainManager.SpawnedChunkInfo spawnInfo)
    {
        if (spawnInfo.ChunkDefinition == null || spawnInfo.Chunk == null)
        {
            return;
        }

        if (spawnInfo.ChunkDefinition.heightBehavior == TerrainChunkDefinition.HeightBehavior.Gap)
        {
            TrySpawnForGap(spawnInfo.Biome);
            spawnedForCurrentGapRun = true;
            return;
        }

        lastNonGapChunk = spawnInfo.Chunk;
        spawnedForCurrentGapRun = false;
    }

    private void HandleChunkDestroyed(TerrainChunk chunk)
    {
        if (chunk == lastNonGapChunk)
        {
            lastNonGapChunk = null;
        }
    }

    private void TrySpawnForGap(BiomeData biome)
    {
        if (spawnedForCurrentGapRun || lastNonGapChunk == null || spawnSettings == null)
        {
            return;
        }

        if (!lastNonGapChunk.EvaluateSurface(
                previousChunkSurfaceT,
                out Vector3 surfacePosition,
                out Vector3 surfaceNormal))
        {
            return;
        }

        GameObject rampPrefab = biome != null ? biome.jumpAssistRampPrefab : null;
        spawnSettings.TrySpawn(
            lastNonGapChunk,
            rampPrefab,
            surfacePosition,
            surfaceNormal,
            out _,
            out _,
            rampSource: JumpAssistRampMarker.Source.Gap);
    }
}
