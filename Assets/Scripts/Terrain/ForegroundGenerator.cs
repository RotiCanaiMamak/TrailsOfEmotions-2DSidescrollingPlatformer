using System.Collections.Generic;
using UnityEngine;

public class ForegroundGenerator : MonoBehaviour
{
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float verticalOffset = 5f;
    [Min(0f)]
    [SerializeField] private float verticalParallaxStrength = 0.2f;
    [Header("Gap Hide")]
    [Min(0f)]
    [SerializeField] private float gapDetectionDistanceX = 2f;
    [Min(0f)]
    [SerializeField] private float gapHideOffsetY = 8f;
    [Min(0f)]
    [SerializeField] private float gapHideSmoothTime = 0.12f;

    private readonly Dictionary<TerrainChunk, TerrainChunk> foregroundBySource = new Dictionary<TerrainChunk, TerrainChunk>();
    private readonly List<TerrainChunk> sourceChunks = new List<TerrainChunk>();
    private TerrainManager terrainManager;
    private TerrainFeatureSpawner featureSpawner;
    private TerrainChunk lastForegroundChunk;
    private float currentGapHideOffsetY;
    private float gapHideVelocityY;

    private void Awake()
    {
        ResolveCamera();
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void Start()
    {
        ResolveCamera();
        TrySubscribe();
    }

    private void LateUpdate()
    {
        UpdateGapHideOffset();
        UpdateForegroundPositions();
    }

    private void OnDisable()
    {
        Unsubscribe();
        ClearForegroundChunks();
    }

    private void TrySubscribe()
    {
        if (terrainManager != null)
        {
            return;
        }

        terrainManager = TerrainManager.Instance;
        if (terrainManager == null)
        {
            return;
        }

        terrainManager.ChunkSpawned += HandleChunkSpawned;
        terrainManager.ChunkDestroyed += HandleChunkDestroyed;
        featureSpawner = terrainManager.GetComponent<TerrainFeatureSpawner>();
    }

    private void Unsubscribe()
    {
        if (terrainManager == null)
        {
            return;
        }

        terrainManager.ChunkSpawned -= HandleChunkSpawned;
        terrainManager.ChunkDestroyed -= HandleChunkDestroyed;
        terrainManager = null;
        featureSpawner = null;
    }

    private void ResolveCamera()
    {
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    private void HandleChunkSpawned(TerrainManager.SpawnedChunkInfo spawnInfo)
    {
        if (spawnInfo.ChunkDefinition != null &&
            spawnInfo.ChunkDefinition.heightBehavior == TerrainChunkDefinition.HeightBehavior.Gap)
        {
            return;
        }

        TerrainChunk foregroundChunkPrefab = spawnInfo.Biome != null ? spawnInfo.Biome.GetForegroundChunkPrefab() : null;
        if (foregroundChunkPrefab == null || spawnInfo.Chunk == null)
        {
            return;
        }

        TerrainChunk foregroundChunk = Instantiate(foregroundChunkPrefab, transform);
        foregroundChunk.chunkWidth = spawnInfo.ChunkWidth;
        foregroundChunk.startY = spawnInfo.Chunk.startY;
        foregroundChunk.endY = spawnInfo.Chunk.endY;
        foregroundChunk.depth = spawnInfo.Depth;
        foregroundChunk.cliffHeight = spawnInfo.Chunk.cliffHeight;
        foregroundChunk.activeBiome = spawnInfo.Biome;
        foregroundChunk.SetSinkingShadowSurfaceColliderEnabled(false);

        if (lastForegroundChunk != null)
        {
            lastForegroundChunk.RebuildExitTangent(spawnInfo.EntryTangent);
        }

        foregroundChunk.Build(spawnInfo.EntryTangent, spawnInfo.ExitTangent);
        if (featureSpawner != null)
        {
            featureSpawner.SpawnForegroundDecorations(foregroundChunk, spawnInfo.ChunkDefinition, spawnInfo.Biome);
        }

        foregroundBySource[spawnInfo.Chunk] = foregroundChunk;
        sourceChunks.Add(spawnInfo.Chunk);
        lastForegroundChunk = foregroundChunk;
        UpdateForegroundPosition(spawnInfo.Chunk, foregroundChunk);
    }

    private void HandleChunkDestroyed(TerrainChunk sourceChunk)
    {
        if (sourceChunk == null || !foregroundBySource.TryGetValue(sourceChunk, out TerrainChunk foregroundChunk))
        {
            return;
        }

        foregroundBySource.Remove(sourceChunk);
        sourceChunks.Remove(sourceChunk);

        if (lastForegroundChunk == foregroundChunk)
        {
            lastForegroundChunk = GetLastForegroundChunk();
        }

        if (foregroundChunk != null)
        {
            Destroy(foregroundChunk.gameObject);
        }

    }

    private void UpdateForegroundPositions()
    {
        for (int i = sourceChunks.Count - 1; i >= 0; i--)
        {
            TerrainChunk sourceChunk = sourceChunks[i];
            if (sourceChunk == null || !foregroundBySource.TryGetValue(sourceChunk, out TerrainChunk foregroundChunk) || foregroundChunk == null)
            {
                if (sourceChunk != null)
                {
                    foregroundBySource.Remove(sourceChunk);
                }

                sourceChunks.RemoveAt(i);
                continue;
            }

            UpdateForegroundPosition(sourceChunk, foregroundChunk);
        }

        lastForegroundChunk = GetLastForegroundChunk();
    }

    private void UpdateForegroundPosition(TerrainChunk sourceChunk, TerrainChunk foregroundChunk)
    {
        float parallaxOffsetY = GetCameraParallaxOffsetY();
        Vector3 sourcePosition = sourceChunk.transform.position;
        foregroundChunk.transform.position = new Vector3(
            sourcePosition.x,
            sourcePosition.y - verticalOffset - parallaxOffsetY - currentGapHideOffsetY,
            sourcePosition.z);
    }

    private void UpdateGapHideOffset()
    {
        float targetOffsetY = IsCameraNearGap() ? gapHideOffsetY : 0f;
        if (gapHideSmoothTime <= 0f)
        {
            currentGapHideOffsetY = targetOffsetY;
            gapHideVelocityY = 0f;
            return;
        }

        currentGapHideOffsetY = Mathf.SmoothDamp(
            currentGapHideOffsetY,
            targetOffsetY,
            ref gapHideVelocityY,
            gapHideSmoothTime);
    }

    private bool IsCameraNearGap()
    {
        ResolveCamera();
        return cameraTransform != null &&
            terrainManager != null &&
            terrainManager.IsGapNearX(cameraTransform.position.x, gapDetectionDistanceX);
    }

    private float GetCameraParallaxOffsetY()
    {
        ResolveCamera();
        if (cameraTransform == null || terrainManager == null)
        {
            return 0f;
        }

        if (!terrainManager.TryGetSurfaceAtX(cameraTransform.position.x, out Vector3 surfacePos, out _))
        {
            return 0f;
        }

        float cameraDeltaY = cameraTransform.position.y - surfacePos.y;
        return Mathf.Abs(cameraDeltaY) * verticalParallaxStrength;
    }

    private TerrainChunk GetLastForegroundChunk()
    {
        for (int i = sourceChunks.Count - 1; i >= 0; i--)
        {
            TerrainChunk sourceChunk = sourceChunks[i];
            if (sourceChunk != null && foregroundBySource.TryGetValue(sourceChunk, out TerrainChunk foregroundChunk) && foregroundChunk != null)
            {
                return foregroundChunk;
            }
        }

        return null;
    }

    private void ClearForegroundChunks()
    {
        foreach (TerrainChunk foregroundChunk in foregroundBySource.Values)
        {
            if (foregroundChunk != null)
            {
                Destroy(foregroundChunk.gameObject);
            }
        }

        sourceChunks.Clear();
        foregroundBySource.Clear();
        lastForegroundChunk = null;
    }
}
