using UnityEngine;

public enum GameOverCutsceneType
{
    None,
    AngerSpeedUp,
    SadnessSlowDown,
    AnxietyBlurShake
}

[CreateAssetMenu(fileName = "NewBiome", menuName = "Game/Biome Data")]
public class BiomeData : ScriptableObject
{
    [System.Serializable]
    public class ObstacleEntry
    {
        public GameObject prefab;

        [Min(0f)] public float weight = 1f;

        [Min(0f)]
        [Tooltip("Minimum uniform local scale for this obstacle prefab.")]
        public float minScale = 1f;

        [Min(0f)]
        [Tooltip("Maximum uniform local scale for this obstacle prefab.")]
        public float maxScale = 1f;
    }

    [System.Serializable]
    public class DecorationEntry
    {
        public GameObject prefab;

        [Min(0f)] public float weight = 1f;

        [Min(0f)]
        [Tooltip("Minimum uniform local scale for this decoration prefab.")]
        public float minScale = 1f;

        [Min(0f)]
        [Tooltip("Maximum uniform local scale for this decoration prefab.")]
        public float maxScale = 1f;
    }

    [System.Serializable]
    public class TerrainWeightEntry
    {
        public TerrainSequenceDefinition sequence;

        [Min(0f)] public float weight = 1f;
    }

    [System.Serializable]
    public class TerrainChunkPrefabOverride
    {
        public TerrainChunkDefinition chunkDefinition;
        public TerrainChunk prefab;
    }

    [System.Serializable]
    public class TerrainChunkSpawnRules
    {
        [Tooltip("Drag the chunk definitions this rule is allowed to spawn on. Empty means allow none.")]
        public TerrainChunkDefinition[] allowedChunks;

        public bool Allows(TerrainChunkDefinition chunkDefinition)
        {
            return ContainsChunk(allowedChunks, chunkDefinition);
        }
    }

    [Header("Biome Name")]
    public string biomeName = "Unnamed Biome";

    [Header("Biome Family")]
    public string familyId = "";

    [Header("Biome Weight")]
    [Min(0f)] public float selectionWeight = 1f;

    [Header("Lighting")]
    public Color globalLightColor = Color.white;
    [Min(0f)] public float globalLightIntensity = 1f;

    [Header("Emotion Meter UI")]
    public Color emotionMeterColor = Color.white;

    [Header("Game Over Cutscene")]
    public GameOverCutsceneType gameOverCutscene = GameOverCutsceneType.None;
    [Min(0f)]
    [Tooltip("Starting horizontal movement multiplier for Anger's game-over cutscene.")]
    public float angerGameOverStartSpeedMultiplier = 1f;
    [Min(0f)]
    [Tooltip("Final horizontal movement multiplier for Anger's game-over cutscene.")]
    public float angerGameOverEndSpeedMultiplier = 3f;
    [Min(0f)]
    [Tooltip("Starting horizontal movement multiplier for Sadness's game-over cutscene. It always fades to 0.")]
    public float sadnessGameOverStartSpeedMultiplier = 1f;
    [Min(0f)]
    [Tooltip("Starting additive blur boost for Anxiety's game-over cutscene.")]
    public float anxietyGameOverStartBlurBoost = 0.25f;
    [Min(0f)]
    [Tooltip("Final additive blur boost for Anxiety's game-over cutscene.")]
    public float anxietyGameOverEndBlurBoost = 1.4f;
    [Min(0f)]
    [Tooltip("Starting continuous camera shake amplitude for Anxiety's game-over cutscene.")]
    public float anxietyGameOverStartShakeAmplitude = 0.08f;
    [Min(0f)]
    [Tooltip("Final continuous camera shake amplitude for Anxiety's game-over cutscene.")]
    public float anxietyGameOverEndShakeAmplitude = 0.55f;

    [Header("Audio")]
    public AudioClip backgroundMusic;
    [Range(0f, 1f)] public float backgroundMusicVolume = 1f;
    public AudioClip ambientSound;
    [Range(0f, 1f)] public float ambientSoundVolume = 1f;

    [Header("Terrain Chunk Prefabs")]
    public TerrainChunk chunkPrefab;
    public TerrainChunk foregroundChunkPrefab;
    [Tooltip("Optional biome-specific prefab overrides for individual terrain chunk definitions, such as physical gaps.")]
    public TerrainChunkPrefabOverride[] chunkPrefabOverrides;

    [Header("Terrain Sequences")]
    [Tooltip("Used for startup/backfill flat terrain. If empty, the first normal terrain sequence is used.")]
    public TerrainSequenceDefinition startupTerrain;
    public TerrainWeightEntry[] normalTerrain;
    public TerrainWeightEntry[] ziplineSafeTerrain;

    [Header("Terrain Rules")]
    [Tooltip("Chunks that should end an active zipline on the previous safe chunk before they spawn.")]
    public TerrainChunkDefinition[] ziplineCutoffChunks;
    [Tooltip("Chunks that may spawn this biome's regulation method while the biome phase is Peaked.")]
    public TerrainChunkDefinition[] regulationSpawnChunks;

    [Header("Obstacles")]
    public ObstacleEntry[] obstacles;
    public TerrainChunkSpawnRules obstacleSpawnRules = new TerrainChunkSpawnRules();

    [Range(0f, 1f)]
    public float obstacleSpawnChance = 0.3f;

    [Min(0)]
    [Tooltip("Minimum number of obstacle-free chunks after a chunk that spawns one or more obstacles. Random obstacle spawn rolls resume after this gap.")]
    public int minimumObstacleGapChunks = 0;

    [Min(1)]
    [Tooltip("Hard cap on obstacles attempted per chunk.")]
    public int candidatesPerChunk = 1;

    [HideInInspector]
    public Vector3 minObstacleScale = Vector3.one;
    [HideInInspector]
    public Vector3 maxObstacleScale = Vector3.one;

    [Header("Decorations")]
    [Tooltip("Decoration prefabs can include a TerrainFeatureBottomRoot child to mark the point that should sit on the terrain surface.")]
    public DecorationEntry[] decorations;

    [Tooltip("Drag the terrain chunk definitions that are allowed to spawn decorations for this biome.")]
    public TerrainChunkSpawnRules decorationSpawnRules = new TerrainChunkSpawnRules();

    [Min(0f)]
    [Tooltip("Average number of decorations to attempt per chunk.")]
    public float decorationSpawnChance = 1f;

    [Min(1)]
    [Tooltip("Hard cap on decorations attempted per chunk.")]
    public int decorationCandidatesPerChunk = 3;

    [HideInInspector]
    public Vector3 minDecorationScale = Vector3.one;
    [HideInInspector]
    public Vector3 maxDecorationScale = Vector3.one;

    [Header("Foreground Decorations")]
    [Tooltip("Foreground decoration prefabs can include a TerrainFeatureBottomRoot child to mark the point that should sit on the foreground terrain surface.")]
    public DecorationEntry[] foregroundDecorations;

    [Tooltip("Drag the terrain chunk definitions that are allowed to spawn foreground decorations for this biome.")]
    public TerrainChunkSpawnRules foregroundDecorationSpawnRules = new TerrainChunkSpawnRules();

    [Min(0f)]
    [Tooltip("Average number of foreground decorations to attempt per foreground chunk.")]
    public float foregroundDecorationSpawnChance = 1f;

    [Min(1)]
    [Tooltip("Hard cap on foreground decorations attempted per foreground chunk.")]
    public int foregroundDecorationCandidatesPerChunk = 3;

    [HideInInspector]
    public Vector3 minForegroundDecorationScale = Vector3.one;
    [HideInInspector]
    public Vector3 maxForegroundDecorationScale = Vector3.one;

    [Header("Player Physics Modifiers")]
    [Min(0f)]
    [Tooltip("Multiplier applied to the player's forward jump speed while this biome is active.")]
    public float jumpForwardMultiplier = 1f;
    [Min(0f)]
    [Tooltip("Multiplier applied to PlayerController.jumpForce while this biome is active.")]
    public float jumpForceMultiplier = 1f;
    [Min(0f)]
    [Tooltip("Multiplier applied to the player's horizontal movement while this biome is active.")]
    public float horizontalSpeedMultiplier = 1f;
    [Min(0f)]
    [Tooltip("Multiplier applied to the player's glide gravity while this biome is active. Values above 1 make the player drop faster.")]
    public float glideGravityMultiplier = 1f;
    [Min(0f)]
    [Tooltip("Multiplier applied to airborne gravity while this biome is active.")]
    public float airborneGravityMultiplier = 1f;
    [Min(0f)]
    [Tooltip("Seconds used to ease airborne horizontal speed toward this biome's speed multiplier.")]
    public float horizontalSpeedLerpDuration = 0f;

    [Header("Jump Assist")]
    [Tooltip("Biome-specific ramp spawned for obstacle and gap jump assists.")]
    public GameObject jumpAssistRampPrefab;

    [Header("Zipline")]
    public ZiplineBuilder ziplinePrefab;
    [Range(0f, 1f)]
    public float ziplineSpawnChance = 0.05f;
    [Min(1)]
    public int ziplineMinSpanChunks = 3;
    [Min(1)]
    public int ziplineMaxSpanChunks = 6;
    [Min(1)]
    public int ziplineMinSequence = 1;
    [Min(1)]
    public int ziplineMaxSequence = 1;

    [Header("Regulation")]
    [Tooltip("Prefab for this biome's regulation method. Spawned only while the committed biome phase is Peaked.")]
    public GameObject regulationMethodPrefab;
    [Range(0f, 1f)]
    public float regulationSpawnChance = 0.35f;
    [Min(0)]
    public int regulationCooldownChunks = 6;

    private void OnValidate()
    {
        minObstacleScale = UniformScaleVector(minObstacleScale);
        maxObstacleScale = UniformScaleVector(maxObstacleScale);
        minDecorationScale = UniformScaleVector(minDecorationScale);
        maxDecorationScale = UniformScaleVector(maxDecorationScale);
        minForegroundDecorationScale = UniformScaleVector(minForegroundDecorationScale);
        maxForegroundDecorationScale = UniformScaleVector(maxForegroundDecorationScale);
        ValidateObstacleEntries(obstacles);
        ValidateDecorationEntries(decorations);
        ValidateDecorationEntries(foregroundDecorations);
        globalLightIntensity = Mathf.Max(0f, globalLightIntensity);
        backgroundMusicVolume = Mathf.Clamp01(backgroundMusicVolume);
        ambientSoundVolume = Mathf.Clamp01(ambientSoundVolume);
        angerGameOverStartSpeedMultiplier = Mathf.Max(0f, angerGameOverStartSpeedMultiplier);
        angerGameOverEndSpeedMultiplier = Mathf.Max(0f, angerGameOverEndSpeedMultiplier);
        sadnessGameOverStartSpeedMultiplier = Mathf.Max(0f, sadnessGameOverStartSpeedMultiplier);
        jumpForwardMultiplier = Mathf.Max(0f, jumpForwardMultiplier);
        jumpForceMultiplier = Mathf.Max(0f, jumpForceMultiplier);
        horizontalSpeedMultiplier = Mathf.Max(0f, horizontalSpeedMultiplier);
        glideGravityMultiplier = Mathf.Max(0f, glideGravityMultiplier);
        airborneGravityMultiplier = Mathf.Max(0f, airborneGravityMultiplier);
        horizontalSpeedLerpDuration = Mathf.Max(0f, horizontalSpeedLerpDuration);
        minimumObstacleGapChunks = Mathf.Max(0, minimumObstacleGapChunks);
        regulationCooldownChunks = Mathf.Max(0, regulationCooldownChunks);
        ziplineMinSpanChunks = Mathf.Max(1, ziplineMinSpanChunks);
        ziplineMaxSpanChunks = Mathf.Max(ziplineMinSpanChunks, ziplineMaxSpanChunks);
        ziplineMinSequence = Mathf.Max(1, ziplineMinSequence);
        ziplineMaxSequence = Mathf.Max(ziplineMinSequence, ziplineMaxSequence);
    }

    private static Vector3 UniformScaleVector(Vector3 value)
    {
        return new Vector3(value.x, value.x, value.x);
    }

    private static void ValidateObstacleEntries(ObstacleEntry[] entries)
    {
        if (entries == null)
        {
            return;
        }

        foreach (ObstacleEntry entry in entries)
        {
            if (entry == null)
            {
                continue;
            }

            ValidateFeatureScaleRange(ref entry.minScale, ref entry.maxScale);
        }
    }

    private static void ValidateDecorationEntries(DecorationEntry[] entries)
    {
        if (entries == null)
        {
            return;
        }

        foreach (DecorationEntry entry in entries)
        {
            if (entry == null)
            {
                continue;
            }

            ValidateFeatureScaleRange(ref entry.minScale, ref entry.maxScale);
        }
    }

    private static void ValidateFeatureScaleRange(ref float minScale, ref float maxScale)
    {
        minScale = Mathf.Max(0f, minScale);
        maxScale = Mathf.Max(0f, maxScale);
    }

    public TerrainSequenceDefinition PickNormalTerrainSequence()
    {
        return TerrainWeightSet.PickSequence(normalTerrain, GetStartupTerrainSequence());
    }

    public TerrainSequenceDefinition PickZiplineSafeTerrainSequence()
    {
        return TerrainWeightSet.PickSequence(ziplineSafeTerrain, GetStartupTerrainSequence());
    }

    public TerrainSequenceDefinition GetStartupTerrainSequence()
    {
        if (startupTerrain != null && startupTerrain.HasChunks)
        {
            return startupTerrain;
        }

        return FirstValidSequence(normalTerrain);
    }

    public bool IsZiplineSafeSequence(TerrainSequenceDefinition sequence)
    {
        return ContainsSequence(ziplineSafeTerrain, sequence);
    }

    public bool IsZiplineCutoffChunk(TerrainChunkDefinition chunkDefinition)
    {
        return ContainsChunk(ziplineCutoffChunks, chunkDefinition);
    }

    public bool AllowsRegulationSpawn(TerrainChunkDefinition chunkDefinition)
    {
        return ContainsChunk(regulationSpawnChunks, chunkDefinition);
    }

    public TerrainChunk GetChunkPrefab(TerrainChunkDefinition chunkDefinition)
    {
        if (chunkPrefabOverrides != null && chunkDefinition != null)
        {
            foreach (TerrainChunkPrefabOverride prefabOverride in chunkPrefabOverrides)
            {
                if (prefabOverride != null &&
                    prefabOverride.chunkDefinition == chunkDefinition &&
                    prefabOverride.prefab != null)
                {
                    return prefabOverride.prefab;
                }
            }
        }

        return chunkPrefab;
    }

    public TerrainChunk GetForegroundChunkPrefab()
    {
        return foregroundChunkPrefab;
    }

    public GameObject PickObstacle()
    {
        ObstacleEntry entry = PickObstacleEntry();
        return entry != null ? entry.prefab : null;
    }

    public ObstacleEntry PickObstacleEntry()
    {
        if (obstacles == null || obstacles.Length == 0)
        {
            return null;
        }

        float total = 0f;
        foreach (ObstacleEntry e in obstacles)
        {
            total += Mathf.Max(0f, e.weight);
        }

        if (total <= 0f)
        {
            return obstacles[0];
        }

        float roll = Random.Range(0f, total);
        foreach (ObstacleEntry e in obstacles)
        {
            roll -= Mathf.Max(0f, e.weight);
            if (roll <= 0f)
            {
                return e;
            }
        }
        return obstacles[obstacles.Length - 1];
    }

    public GameObject PickDecoration()
    {
        DecorationEntry entry = PickDecorationEntry();
        return entry != null ? entry.prefab : null;
    }

    public DecorationEntry PickDecorationEntry()
    {
        if (decorations == null || decorations.Length == 0)
        {
            return null;
        }

        float total = 0f;
        foreach (DecorationEntry e in decorations)
        {
            total += Mathf.Max(0f, e.weight);
        }

        if (total <= 0f)
        {
            return decorations[0];
        }

        float roll = Random.Range(0f, total);
        foreach (DecorationEntry e in decorations)
        {
            roll -= Mathf.Max(0f, e.weight);
            if (roll <= 0f)
            {
                return e;
            }
        }
        return decorations[decorations.Length - 1];
    }

    public GameObject PickForegroundDecoration()
    {
        DecorationEntry entry = PickForegroundDecorationEntry();
        return entry != null ? entry.prefab : null;
    }

    public DecorationEntry PickForegroundDecorationEntry()
    {
        if (foregroundDecorations == null || foregroundDecorations.Length == 0)
        {
            return null;
        }

        float total = 0f;
        foreach (DecorationEntry e in foregroundDecorations)
        {
            total += Mathf.Max(0f, e.weight);
        }

        if (total <= 0f)
        {
            return foregroundDecorations[0];
        }

        float roll = Random.Range(0f, total);
        foreach (DecorationEntry e in foregroundDecorations)
        {
            roll -= Mathf.Max(0f, e.weight);
            if (roll <= 0f)
            {
                return e;
            }
        }
        return foregroundDecorations[foregroundDecorations.Length - 1];
    }

    private static TerrainSequenceDefinition FirstValidSequence(TerrainWeightEntry[] entries)
    {
        if (entries == null)
        {
            return null;
        }

        foreach (TerrainWeightEntry entry in entries)
        {
            if (entry != null && entry.sequence != null && entry.sequence.HasChunks)
            {
                return entry.sequence;
            }
        }

        return null;
    }

    private static bool ContainsSequence(TerrainWeightEntry[] entries, TerrainSequenceDefinition sequence)
    {
        if (entries == null || sequence == null)
        {
            return false;
        }

        foreach (TerrainWeightEntry entry in entries)
        {
            if (entry != null && entry.sequence == sequence)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsChunk(TerrainChunkDefinition[] chunks, TerrainChunkDefinition chunkDefinition)
    {
        if (chunks == null || chunkDefinition == null)
        {
            return false;
        }

        foreach (TerrainChunkDefinition candidate in chunks)
        {
            if (candidate == chunkDefinition)
            {
                return true;
            }
        }

        return false;
    }
}
