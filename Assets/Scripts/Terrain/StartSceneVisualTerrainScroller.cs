using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(TerrainFeatureSpawner))]
public class StartSceneVisualTerrainScroller : MonoBehaviour
{
    private sealed class VisualChunkPair
    {
        public TerrainChunk Terrain;
        public TerrainChunk Foreground;
    }

    [Header("Biome")]
    [SerializeField] private BiomeData visualBiome;

    [Header("Terrain")]
    [Min(0.01f)]
    [SerializeField] private float chunkWidth = 10f;
    [Min(0.01f)]
    [SerializeField] private float chunkDepth = 20f;
    [Min(1)]
    [SerializeField] private int visibleChunkCount = 8;

    [Header("Placement")]
    [SerializeField] private Transform visualPlacementTarget;
    [SerializeField] private Vector2 visualTerrainOffsetFromTarget = new Vector2(-10f, 0f);
    [SerializeField] private float foregroundOffsetY = -5f;

    [Header("Scrolling")]
    [Min(0f)]
    [SerializeField] private float scrollSpeed = 4f;
    [Min(0f)]
    [SerializeField] private float recyclePadding = 12f;

    [Header("Parallax")]
    [SerializeField] private ParallaxScroller[] parallaxLayers;

    private readonly List<VisualChunkPair> activeChunks = new List<VisualChunkPair>();
    private TerrainFeatureSpawner featureSpawner;
    private BiomeData resolvedBiome;
    private TerrainSequencePlanner sequencePlanner = new TerrainSequencePlanner();
    private TerrainChunk lastSpawnedTerrainChunk;
    private Vector3 previousSlopeVector = Vector3.right;
    private float nextSpawnX;
    private float currentEndY;
    private float resolvedStartY;
    private float resolvedRecycleOriginX;
    private bool shouldSpawnStartupSequence;
    private bool loggedPlacement;

    private void Awake()
    {
        featureSpawner = GetComponent<TerrainFeatureSpawner>();
    }

    private void Start()
    {
        ResetScroller();
    }

    private void Update()
    {
        float scrollDelta = scrollSpeed * Time.deltaTime;
        if (scrollDelta > 0f)
        {
            ScrollVisuals(scrollDelta);
            ScrollParallax(scrollDelta);
        }

        RecycleExpiredChunks();
    }

    private void OnDisable()
    {
        ClearChunks();
    }

    private void OnValidate()
    {
        chunkWidth = Mathf.Max(0.01f, chunkWidth);
        chunkDepth = Mathf.Max(0.01f, chunkDepth);
        visibleChunkCount = Mathf.Max(1, visibleChunkCount);
        scrollSpeed = Mathf.Max(0f, scrollSpeed);
        recyclePadding = Mathf.Max(0f, recyclePadding);
    }

    public void ResetScroller()
    {
        ClearChunks();
        ResolveRuntimeReferences();

        if (visualPlacementTarget == null)
        {
            Debug.LogWarning("[StartSceneVisualTerrainScroller] Assign visualPlacementTarget before spawning visual terrain.", this);
            return;
        }

        ResolvePlacement();
        ResetGenerationState();

        for (int i = 0; i < visibleChunkCount; i++)
        {
            if (!SpawnNextChunkPair())
            {
                break;
            }
        }
    }

    /// <summary>
    /// Samples the generated visual terrain surface at a given world X position.
    /// Returns the exact spline position/normal used to build the main terrain chunk.
    /// </summary>
    public bool TryGetSurfaceAtX(float worldX, out Vector3 worldPos, out Vector3 worldNormal)
    {
        for (int i = 0; i < activeChunks.Count; i++)
        {
            TerrainChunk chunk = activeChunks[i] != null ? activeChunks[i].Terrain : null;
            if (chunk == null)
            {
                continue;
            }

            float chunkX = chunk.transform.position.x;
            float chunkEndX = chunkX + chunk.chunkWidth;

            if (worldX < chunkX - 0.001f || worldX > chunkEndX + 0.001f)
            {
                continue;
            }

            float t = 1f - Mathf.InverseLerp(chunkX, chunkEndX, worldX);
            return chunk.EvaluateSurface(t, out worldPos, out worldNormal);
        }

        worldPos = default;
        worldNormal = Vector3.up;
        return false;
    }

    private void ResolveRuntimeReferences()
    {
        if (featureSpawner == null)
        {
            featureSpawner = GetComponent<TerrainFeatureSpawner>();
        }

        resolvedBiome = visualBiome;
        if (resolvedBiome == null && BiomeManager.Instance != null)
        {
            BiomeManager.Instance.EnsureInitialized();
            resolvedBiome = BiomeManager.Instance.CurrentBiome;
        }

        shouldSpawnStartupSequence = true;
    }

    private void ResolvePlacement()
    {
        Vector3 targetPosition = visualPlacementTarget.position;
        nextSpawnX = targetPosition.x + visualTerrainOffsetFromTarget.x;
        resolvedRecycleOriginX = nextSpawnX;
        resolvedStartY = targetPosition.y + visualTerrainOffsetFromTarget.y;

        if (!loggedPlacement)
        {
            Debug.Log(
                $"[StartSceneVisualTerrainScroller] Placement target '{visualPlacementTarget.name}' at {targetPosition}; " +
                $"offset {visualTerrainOffsetFromTarget}; first spawn ({nextSpawnX}, {resolvedStartY}).",
                this);
            loggedPlacement = true;
        }
    }

    private void ResetGenerationState()
    {
        featureSpawner?.ResetObstacleGapCooldown();
        sequencePlanner = new TerrainSequencePlanner();
        currentEndY = resolvedStartY;
        previousSlopeVector = Vector3.right;
        lastSpawnedTerrainChunk = null;
    }

    private void ScrollVisuals(float scrollDelta)
    {
        Vector3 movement = Vector3.left * scrollDelta;
        for (int i = activeChunks.Count - 1; i >= 0; i--)
        {
            VisualChunkPair pair = activeChunks[i];
            if (pair == null)
            {
                activeChunks.RemoveAt(i);
                continue;
            }

            MoveChunk(pair.Terrain, movement);
            MoveChunk(pair.Foreground, movement);
        }

        nextSpawnX -= scrollDelta;
    }

    private void ScrollParallax(float scrollDelta)
    {
        if (parallaxLayers == null || parallaxLayers.Length == 0)
        {
            return;
        }

        Vector3 shift = new Vector3(scrollDelta, 0f, 0f);
        for (int i = 0; i < parallaxLayers.Length; i++)
        {
            if (parallaxLayers[i] != null)
            {
                parallaxLayers[i].OnWorldShift(shift);
            }
        }
    }

    private void RecycleExpiredChunks()
    {
        float leftRecycleX = resolvedRecycleOriginX - recyclePadding;
        while (activeChunks.Count > 0)
        {
            VisualChunkPair oldest = activeChunks[0];
            TerrainChunk terrain = oldest != null ? oldest.Terrain : null;
            if (terrain != null && terrain.transform.position.x + chunkWidth >= leftRecycleX)
            {
                break;
            }

            DestroyPair(oldest);
            activeChunks.RemoveAt(0);

            if (oldest != null && oldest.Terrain == lastSpawnedTerrainChunk)
            {
                lastSpawnedTerrainChunk = null;
            }

            if (activeChunks.Count == 0)
            {
                currentEndY = resolvedStartY;
                previousSlopeVector = Vector3.right;
            }

            if (!SpawnNextChunkPair())
            {
                break;
            }
        }
    }

    private bool SpawnNextChunkPair()
    {
        PlannedTerrainChunk plannedChunk = GetNextTerrainChunk();
        TerrainChunkDefinition chunkDefinition = plannedChunk.Chunk;
        if (chunkDefinition == null)
        {
            Debug.LogWarning("[StartSceneVisualTerrainScroller] No valid terrain chunk definition found on the visual biome.", this);
            return false;
        }

        bool isGapChunk = chunkDefinition.heightBehavior == TerrainChunkDefinition.HeightBehavior.Gap;
        TerrainChunk terrainPrefab = resolvedBiome != null ? resolvedBiome.GetChunkPrefab(chunkDefinition) : null;
        if (terrainPrefab == null)
        {
            Debug.LogWarning("[StartSceneVisualTerrainScroller] Assign a terrain chunk prefab on the resolved BiomeData.", this);
            return false;
        }

        float startY = currentEndY;
        float endY = ResolveEndY(chunkDefinition, startY);
        Vector3 slopeVector = NormalizedOrRight(new Vector3(chunkWidth, endY - startY, 0f));
        Vector3 entryTangent = NormalizedOrRight((previousSlopeVector + slopeVector) * 0.5f);

        if (lastSpawnedTerrainChunk != null)
        {
            lastSpawnedTerrainChunk.RebuildExitTangent(entryTangent);
        }

        TerrainChunk terrain = SpawnDefinedChunk(terrainPrefab, nextSpawnX, startY, endY, entryTangent, slopeVector);
        if (terrain == null)
        {
            return false;
        }

        if (featureSpawner != null)
        {
            featureSpawner.SpawnPreparedForChunk(terrain, chunkDefinition, resolvedBiome, null);
        }

        TerrainChunk foreground = null;
        TerrainChunk foregroundPrefab = resolvedBiome != null ? resolvedBiome.GetForegroundChunkPrefab() : null;
        if (!isGapChunk && foregroundPrefab != null)
        {
            foreground = SpawnDefinedChunk(
                foregroundPrefab,
                nextSpawnX,
                startY + foregroundOffsetY,
                endY + foregroundOffsetY,
                entryTangent,
                slopeVector);
            if (foreground != null && featureSpawner != null)
            {
                featureSpawner.SpawnForegroundDecorations(foreground, chunkDefinition, resolvedBiome);
            }
            DisableColliders(foreground != null ? foreground.gameObject : null);
        }

        activeChunks.Add(new VisualChunkPair
        {
            Terrain = terrain,
            Foreground = foreground
        });

        currentEndY = terrain.RightEdgeY;
        previousSlopeVector = slopeVector;
        lastSpawnedTerrainChunk = terrain;
        nextSpawnX += chunkWidth;
        return true;
    }

    private PlannedTerrainChunk GetNextTerrainChunk()
    {
        if (shouldSpawnStartupSequence)
        {
            shouldSpawnStartupSequence = false;
            TerrainSequenceDefinition startupSequence = resolvedBiome != null
                ? resolvedBiome.GetStartupTerrainSequence()
                : null;
            if (startupSequence != null && startupSequence.HasChunks)
            {
                return sequencePlanner.StartSequenceNow(startupSequence);
            }
        }

        PlannedTerrainChunk plannedChunk = sequencePlanner.NextTerrainChunk(PickTerrainSequence);
        if (plannedChunk.Chunk != null)
        {
            return plannedChunk;
        }

        return default;
    }

    private TerrainSequenceDefinition PickTerrainSequence()
    {
        return resolvedBiome != null ? resolvedBiome.PickNormalTerrainSequence() : null;
    }

    private static float ResolveEndY(TerrainChunkDefinition chunkDefinition, float startY)
    {
        if (chunkDefinition == null)
        {
            return startY;
        }

        switch (chunkDefinition.heightBehavior)
        {
            case TerrainChunkDefinition.HeightBehavior.Rise:
                return startY + chunkDefinition.RollVerticalAmount();
            case TerrainChunkDefinition.HeightBehavior.Drop:
                return startY - chunkDefinition.RollVerticalAmount();
            case TerrainChunkDefinition.HeightBehavior.Gap:
            case TerrainChunkDefinition.HeightBehavior.Flat:
            default:
                return startY;
        }
    }

    private TerrainChunk SpawnDefinedChunk(
        TerrainChunk prefab,
        float spawnX,
        float startY,
        float endY,
        Vector3 entryTangent,
        Vector3 exitTangent)
    {
        if (prefab == null)
        {
            return null;
        }

        TerrainChunk chunk = Instantiate(prefab, transform);
        chunk.transform.position = new Vector3(spawnX, startY, transform.position.z);
        chunk.chunkWidth = chunkWidth;
        chunk.startY = startY;
        chunk.endY = endY;
        chunk.depth = Mathf.Max(chunkDepth, Mathf.Max(0f, startY - endY));
        chunk.cliffHeight = 0f;
        chunk.activeBiome = resolvedBiome;
        chunk.SetSinkingShadowSurfaceColliderEnabled(false);
        chunk.Build(entryTangent, exitTangent);
        return chunk;
    }

    private static Vector3 NormalizedOrRight(Vector3 value)
    {
        return value.sqrMagnitude > 0.0001f ? value.normalized : Vector3.right;
    }

    private static void MoveChunk(TerrainChunk chunk, Vector3 movement)
    {
        if (chunk != null)
        {
            chunk.transform.position += movement;
        }
    }

    private static void DisableColliders(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        Collider2D[] colliders2D = root.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders2D.Length; i++)
        {
            colliders2D[i].enabled = false;
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }
    }

    private void ClearChunks()
    {
        for (int i = activeChunks.Count - 1; i >= 0; i--)
        {
            DestroyPair(activeChunks[i]);
        }

        activeChunks.Clear();
    }

    private static void DestroyPair(VisualChunkPair pair)
    {
        if (pair == null)
        {
            return;
        }

        DestroyChunk(pair.Terrain);
        DestroyChunk(pair.Foreground);
    }

    private static void DestroyChunk(TerrainChunk chunk)
    {
        if (chunk != null)
        {
            Destroy(chunk.gameObject);
        }
    }
}
