using System.Collections.Generic;
using Bundos.WaterSystem;
using UnityEngine;

public interface IAbilityOrbObstacleProfile
{
    float TargetOrbSpacing { get; }
    float CurveHeight { get; }
    float CurveLength { get; }
    float CenterOffsetX { get; }
}

public class TerrainFeatureSpawner : MonoBehaviour
{
    private int obstacleGapChunksRemaining;

    public readonly struct SpawnedChunkFeatures
    {
        public readonly GameObject[] GroundObstacles;

        public SpawnedChunkFeatures(GameObject[] groundObstacles)
        {
            GroundObstacles = groundObstacles;
        }
    }

    internal sealed class PreparedObstacle
    {
        public BiomeData.ObstacleEntry Entry;
        public float SurfaceT;
        public bool IsPuddle;
        public Vector2 PuddleSize;
        public float PuddleEdgeMargin;
    }

    public sealed class PreparedChunkFeatures
    {
        internal readonly List<PreparedObstacle> Obstacles = new List<PreparedObstacle>();
        public bool HasPuddle { get; internal set; }
    }

    public PreparedChunkFeatures PrepareForChunk(TerrainChunkDefinition chunkDefinition, BiomeData biome)
    {
        PreparedChunkFeatures prepared = new PreparedChunkFeatures();

        if (obstacleGapChunksRemaining > 0)
        {
            obstacleGapChunksRemaining--;
            return prepared;
        }

        if (biome == null || biome.obstacleSpawnRules == null || !biome.obstacleSpawnRules.Allows(chunkDefinition))
        {
            return prepared;
        }
        if (biome.obstacles == null || biome.obstacles.Length == 0 || biome.candidatesPerChunk <= 0)
        {
            return prepared;
        }

        int spawnCount = Mathf.Min(PoissonSample(biome.obstacleSpawnChance), biome.candidatesPerChunk);
        for (int i = 0; i < spawnCount; i++)
        {
            float slotWidth = 1f / spawnCount;
            float slotCentre = (i + 0.5f) * slotWidth;
            float jitter = slotWidth * 0.2f;
            float surfaceT = Mathf.Clamp(slotCentre + Random.Range(-jitter, jitter), 0.05f, 0.95f);

            BiomeData.ObstacleEntry entry = biome.PickObstacleEntry();
            if (entry == null || entry.prefab == null)
            {
                continue;
            }

            TerrainPuddleObstacle puddleMarker = entry.prefab.GetComponent<TerrainPuddleObstacle>();
            if (puddleMarker != null)
            {
                if (prepared.HasPuddle)
                {
                    continue;
                }

                prepared.HasPuddle = true;
                prepared.Obstacles.Add(new PreparedObstacle
                {
                    Entry = entry,
                    SurfaceT = surfaceT,
                    IsPuddle = true,
                    PuddleSize = puddleMarker.RollSize(),
                    PuddleEdgeMargin = puddleMarker.EdgeMargin
                });
                continue;
            }

            prepared.Obstacles.Add(new PreparedObstacle
            {
                Entry = entry,
                SurfaceT = surfaceT
            });
        }

        if (prepared.Obstacles.Count > 0)
        {
            obstacleGapChunksRemaining = Mathf.Max(0, biome.minimumObstacleGapChunks);
        }

        return prepared;
    }

    public void ResetObstacleGapCooldown()
    {
        obstacleGapChunksRemaining = 0;
    }

    public void ConfigureChunkBeforeBuild(TerrainChunk chunk, PreparedChunkFeatures prepared)
    {
        if (chunk == null || prepared == null || !prepared.HasPuddle)
        {
            return;
        }

        foreach (PreparedObstacle obstacle in prepared.Obstacles)
        {
            if (!obstacle.IsPuddle)
            {
                continue;
            }

            chunk.ConfigurePuddle(
                obstacle.SurfaceT,
                obstacle.PuddleSize.x,
                obstacle.PuddleSize.y,
                obstacle.PuddleEdgeMargin);
            return;
        }
    }

    public SpawnedChunkFeatures SpawnPreparedForChunk(
        TerrainChunk chunk,
        TerrainChunkDefinition chunkDefinition,
        BiomeData biome,
        PreparedChunkFeatures prepared)
    {
        if (chunk == null || biome == null)
        {
            return default;
        }

        GameObject[] groundObstacles = SpawnPreparedObstacles(chunk, prepared);
        if (!chunk.HasPuddle)
        {
            SpawnDecorations(chunk, chunkDefinition, biome);
        }

        return new SpawnedChunkFeatures(groundObstacles);
    }

    public void SpawnForegroundDecorations(TerrainChunk chunk, TerrainChunkDefinition chunkDefinition, BiomeData biome)
    {
        if (chunk == null || biome == null)
        {
            return;
        }
        if (biome.foregroundDecorationSpawnRules == null || !biome.foregroundDecorationSpawnRules.Allows(chunkDefinition))
        {
            return;
        }
        if (biome.foregroundDecorations == null || biome.foregroundDecorations.Length == 0)
        {
            return;
        }
        if (biome.foregroundDecorationCandidatesPerChunk <= 0)
        {
            return;
        }

        int spawnCount = Mathf.Min(PoissonSample(biome.foregroundDecorationSpawnChance), biome.foregroundDecorationCandidatesPerChunk);
        if (spawnCount <= 0)
        {
            return;
        }

        for (int i = 0; i < spawnCount; i++)
        {
            float slotWidth = 1f / spawnCount;
            float slotCentre = (i + 0.5f) * slotWidth;
            float jitter = slotWidth * 0.2f;
            float t = Mathf.Clamp(slotCentre + Random.Range(-jitter, jitter), 0.05f, 0.95f);

            if (!chunk.EvaluateSurface(t, out Vector3 worldPos, out Vector3 worldNormal))
            {
                continue;
            }

            BiomeData.DecorationEntry entry = biome.PickForegroundDecorationEntry();
            if (entry == null || entry.prefab == null)
            {
                continue;
            }

            float surfaceAngle = Mathf.Atan2(worldNormal.x, worldNormal.y) * Mathf.Rad2Deg * -1f;
            Quaternion rotation = Quaternion.Euler(0f, 0f, surfaceAngle);

            GameObject decoration = Instantiate(entry.prefab, worldPos, rotation, chunk.transform);
            decoration.transform.localScale = RandomUniformScale(entry.minScale, entry.maxScale);

            SnapBottomRootToSurface(decoration, worldPos);
        }
    }

    private GameObject[] SpawnPreparedObstacles(TerrainChunk chunk, PreparedChunkFeatures prepared)
    {
        if (chunk == null || prepared == null)
        {
            return System.Array.Empty<GameObject>();
        }

        List<GameObject> groundObstacles = new List<GameObject>();

        foreach (PreparedObstacle preparedObstacle in prepared.Obstacles)
        {
            BiomeData.ObstacleEntry entry = preparedObstacle.Entry;
            if (entry == null || entry.prefab == null)
            {
                continue;
            }

            if (preparedObstacle.IsPuddle)
            {
                AddGroundObstacle(groundObstacles, SpawnPuddle(chunk, entry.prefab));
                continue;
            }

            SinkingShadowObstacle shadowPrefab =
                entry.prefab.GetComponent<SinkingShadowObstacle>();
            if (shadowPrefab != null)
            {
                AddGroundObstacle(
                    groundObstacles,
                    SpawnSinkingShadow(
                        chunk,
                        preparedObstacle.SurfaceT,
                        entry,
                        transform));
                continue;
            }

            if (!chunk.EvaluateSurface(preparedObstacle.SurfaceT, out Vector3 worldPos, out Vector3 worldNormal))
            {
                continue;
            }

            NoisyLeftMover moverPrefab = entry.prefab.GetComponent<NoisyLeftMover>();
            bool isAirObstacle = moverPrefab != null && moverPrefab.SpawnsAboveSurface;
            Quaternion rotation = isAirObstacle
                ? entry.prefab.transform.rotation
                : Quaternion.Euler(0f, 0f, Mathf.Atan2(worldNormal.x, worldNormal.y) * Mathf.Rad2Deg * -1f);

            GameObject obstacle = Instantiate(entry.prefab, worldPos, rotation, chunk.transform);
            obstacle.transform.localScale = RandomUniformScale(entry.minScale, entry.maxScale);

            SnapBottomRootToSurface(obstacle, worldPos);

            NoisyLeftMover mover = obstacle.GetComponent<NoisyLeftMover>();
            if (mover != null && mover.SpawnsAboveSurface)
            {
                mover.PlaceAboveSurface(obstacle.transform.position);
                continue;
            }

            AddGroundObstacle(groundObstacles, obstacle);
        }

        groundObstacles.Sort((left, right) =>
            left.transform.position.x.CompareTo(right.transform.position.x));
        return groundObstacles.ToArray();
    }

    private static GameObject SpawnSinkingShadow(
        TerrainChunk chunk,
        float surfaceT,
        BiomeData.ObstacleEntry entry,
        Transform parent)
    {
        if (chunk == null ||
            entry == null ||
            entry.prefab == null ||
            !chunk.EvaluateSurface(surfaceT, out Vector3 surfacePos, out _) ||
            !chunk.EvaluateBottom(surfaceT, out Vector3 bottomPos))
        {
            return null;
        }

        GameObject shadow = Instantiate(
            entry.prefab,
            bottomPos,
            entry.prefab.transform.rotation,
            parent);
        shadow.transform.localScale = RandomUniformScale(entry.minScale, entry.maxScale);

        SinkingShadowObstacle obstacle = shadow.GetComponent<SinkingShadowObstacle>();
        obstacle?.InitializeOnChunk(chunk, surfaceT, bottomPos, surfacePos);
        return shadow;
    }

    private static GameObject SpawnPuddle(TerrainChunk chunk, GameObject prefab)
    {
        if (chunk == null || prefab == null || !chunk.TryGetPuddlePlacement(
                out Vector3 localBottomLeft,
                out float width,
                out float depth))
        {
            return null;
        }

        GameObject puddle = Instantiate(prefab, chunk.transform);
        puddle.tag = "Obstacle";
        Vector3 localPosition = puddle.transform.localPosition;
        puddle.transform.localPosition = new Vector3(localBottomLeft.x, localBottomLeft.y, localPosition.z);

        Water water = puddle.GetComponent<Water>();
        float nativeWidth = water != null ? Mathf.Max(1f, water.numSprings - 1f) : 1f;
        Vector3 localScale = puddle.transform.localScale;
        puddle.transform.localScale = new Vector3(width / nativeWidth, depth, localScale.z);
        return puddle;
    }

    private static void AddGroundObstacle(List<GameObject> obstacles, GameObject candidate)
    {
        if (candidate == null)
        {
            return;
        }

        obstacles.Add(candidate);
    }

    private void SpawnDecorations(TerrainChunk chunk, TerrainChunkDefinition chunkDefinition, BiomeData biome)
    {
        if (chunk == null || biome == null)
        {
            return;
        }
        if (biome.decorationSpawnRules == null || !biome.decorationSpawnRules.Allows(chunkDefinition))
        {
            return;
        }
        if (biome.decorations == null || biome.decorations.Length == 0)
        {
            return;
        }
        if (biome.decorationCandidatesPerChunk <= 0)
        {
            return;
        }

        int spawnCount = Mathf.Min(PoissonSample(biome.decorationSpawnChance), biome.decorationCandidatesPerChunk);
        if (spawnCount <= 0)
        {
            return;
        }

        for (int i = 0; i < spawnCount; i++)
        {
            float slotWidth = 1f / spawnCount;
            float slotCentre = (i + 0.5f) * slotWidth;
            float jitter = slotWidth * 0.2f;
            float t = Mathf.Clamp(slotCentre + Random.Range(-jitter, jitter), 0.05f, 0.95f);

            if (!chunk.EvaluateSurface(t, out Vector3 worldPos, out Vector3 worldNormal))
            {
                continue;
            }

            BiomeData.DecorationEntry entry = biome.PickDecorationEntry();
            if (entry == null || entry.prefab == null)
            {
                continue;
            }

            float surfaceAngle = Mathf.Atan2(worldNormal.x, worldNormal.y) * Mathf.Rad2Deg * -1f;
            Quaternion rotation = Quaternion.Euler(0f, 0f, surfaceAngle);

            GameObject decoration = Instantiate(entry.prefab, worldPos, rotation, chunk.transform);
            decoration.transform.localScale = RandomUniformScale(entry.minScale, entry.maxScale);

            SnapBottomRootToSurface(decoration, worldPos);
        }
    }

    private static void SnapBottomRootToSurface(GameObject featureInstance, Vector3 surfacePosition)
    {
        if (featureInstance == null)
        {
            return;
        }

        TerrainFeatureBottomRoot bottomRoot = featureInstance.GetComponentInChildren<TerrainFeatureBottomRoot>();
        if (bottomRoot == null)
        {
            return;
        }

        Vector3 correction = surfacePosition - bottomRoot.transform.position;
        featureInstance.transform.position += correction;
    }

    private static int PoissonSample(float lambda)
    {
        float l = Mathf.Exp(-lambda);
        int k = 0;
        float p = 1f;
        do
        {
            k++;
            p *= Random.value;
        } while (p > l);
        return k - 1;
    }

    private static Vector3 RandomUniformScale(float minScale, float maxScale)
    {
        minScale = Mathf.Max(0f, minScale);
        maxScale = Mathf.Max(0f, maxScale);

        float lower = Mathf.Min(minScale, maxScale);
        float upper = Mathf.Max(minScale, maxScale);
        float scale = Random.Range(lower, upper);
        return new Vector3(scale, scale, scale);
    }
}
