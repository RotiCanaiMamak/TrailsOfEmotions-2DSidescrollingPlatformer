using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Serialization;

[RequireComponent(typeof(TerrainFeatureSpawner))]
public class TerrainManager : MonoBehaviour
{
    public static TerrainManager Instance { get; private set; }

    public readonly struct SpawnedChunkInfo
    {
        public readonly TerrainChunk Chunk;
        public readonly TerrainSequenceDefinition SequenceDefinition;
        public readonly TerrainChunkDefinition ChunkDefinition;
        public readonly BiomeData Biome;
        public readonly float ChunkWidth;
        public readonly float Depth;
        public readonly Vector3 EntryTangent;
        public readonly Vector3 ExitTangent;

        public SpawnedChunkInfo(
            TerrainChunk chunk,
            TerrainSequenceDefinition sequenceDefinition,
            TerrainChunkDefinition chunkDefinition,
            BiomeData biome,
            float chunkWidth,
            float depth,
            Vector3 entryTangent,
            Vector3 exitTangent)
        {
            Chunk = chunk;
            SequenceDefinition = sequenceDefinition;
            ChunkDefinition = chunkDefinition;
            Biome = biome;
            ChunkWidth = chunkWidth;
            Depth = depth;
            EntryTangent = entryTangent;
            ExitTangent = exitTangent;
        }
    }

    public event System.Action<SpawnedChunkInfo> ChunkSpawned;
    public event System.Action<TerrainChunk> ChunkDestroyed;

    private const string PlayerTag = "Player";

    private Transform player;

    [Header("Chunk Settings")]
    public int visibleChunksAhead = 10;
    public float chunkWidth = 10f;
    [Min(1)]
    public int chunksBehindPlayer = 2;
    [Min(0f)]
    [Tooltip("World units behind the player where the startup terrain sequence begins at run/reset start.")]
    public float startupTerrainBackOffsetX = 10f;

    [Header("Depth")]
    public float baseDepth = 20f;
    public float depthSafetyMargin = 10f;

    [Header("Biome Transitions")]
    public GameObject transitionDoorPrefab;


    private GameObject pendingTransitionDoor;
    private readonly Queue<TerrainChunk> activeChunks = new Queue<TerrainChunk>();
    private readonly HashSet<TerrainChunk> activeGapChunks = new HashSet<TerrainChunk>();
    private float nextSpawnX;
    private float currentEndY;
    private Vector3 prevSlopeVector = Vector3.right;
    private TerrainChunk lastSpawnedChunk;
    private TerrainChunkDefinition lastSpawnedChunkDefinition;
    private bool hasLastSpawnedChunkDefinition;
    [SerializeField] private TerrainFeatureSpawner featureSpawner;
    [SerializeField] private BiomeManager biomeManager;
    private TerrainSequencePlanner sequencePlanner = new TerrainSequencePlanner();

    [Header("Ziplines")]
    [Min(0)]
    public int ziplineSpawnDelayChunks = 20;

    [Header("Ability Orbs")]
    [SerializeField] private GameObject abilityOrbPrefab;
    [FormerlySerializedAs("abilityOrbMinBatchSize")]
    [Min(1)] [SerializeField] private int ziplineAbilityOrbMinCount = 5;
    [FormerlySerializedAs("abilityOrbMaxBatchSize")]
    [Min(1)] [SerializeField] private int ziplineAbilityOrbMaxCount = 6;
    [Min(1)] [SerializeField] private int terrainAbilityOrbMinCount = 5;
    [Min(1)] [SerializeField] private int terrainAbilityOrbMaxCount = 6;
    [Min(0.1f)] [SerializeField] private float terrainAbilityOrbSpacing = 3f;
    [Range(8, 128)] [SerializeField] private int terrainAbilityOrbPathSamples = 32;
    [Min(0)] [SerializeField] private int abilityOrbMinGapChunks = 8;
    [Min(0)] [SerializeField] private int abilityOrbMaxGapChunks = 12;
    [Range(0f, 0.45f)] [SerializeField] private float abilityOrbEndpointPadding = 0.1f;
    [Min(0f)] [SerializeField] private float terrainAbilityOrbHeight = 1.25f;
    [Min(0f)] [SerializeField] private float ziplineAbilityOrbHeight = 0.8f;

    private const int ZiplineMaxExtendChunks = 8;
    private const float ZiplineAnchorSurfaceT = 0.5f;

    private bool ziplineActive;
    private ZiplineBuilder pendingZipline;
    private ZiplineBuilder lastCompletedZipline;
    private Vector3 lastSafeZiplineSurfacePos;
    private bool hasLastSafeZiplineSurfacePos;
    private int ziplineChunksRemaining;
    private int ziplineExtendGuard;
    private int ziplineSequenceSpansRemaining;
    private int ziplineSequenceRequiredSpans;
    private int ziplineSequenceCompletedSpans;
    private int postZiplineSafeBufferChunks;
    private int ziplineSpawnDelayChunksRemaining;
    private int regulationCooldownChunksRemaining;
    private bool hasPendingRegulationSequence;
    private TerrainSequenceDefinition pendingRegulationSequence;
    private BiomeData pendingRegulationBiome;
    private GameObject pendingRegulationPrefab;
    private bool regulationSequenceActive;
    private TerrainSequenceDefinition activeRegulationSequence;
    private BiomeData activeRegulationBiome;
    private GameObject activeRegulationPrefab;
    private SadnessRegulation activeSadnessRegulation;
    private readonly List<ZiplineBuilder> activeZiplines = new List<ZiplineBuilder>();
    private readonly List<SadnessRegulation> activeSadnessRegulations = new List<SadnessRegulation>();
    private RigidbodyInterpolation2D playerInterpolationBeforeShift;
    private bool hasPlayerInterpolationToRestore;
    private bool restorePlayerInterpolationOnNextFixedUpdate;
    private int abilityOrbChunksUntilBatch;
    private bool abilityOrbBatchPending;
    private int abilityOrbTerrainOrbsRemaining;
    private float abilityOrbDistanceToNextTerrainOrb;

    private sealed class ObstacleArcDescriptor
    {
        public float StartX;
        public float EndX;
        public float PeakX;
        public float PeakY;
        public float TargetSpacing;
        public Vector3 StartPosition;
        public Vector3 EndPosition;
    }

    private readonly struct ResolvedObstacleArc
    {
        public readonly float StartX;
        public readonly float EndX;
        public readonly float PeakX;
        public readonly float PeakY;
        public readonly float TargetSpacing;
        public readonly Vector3 StartPosition;
        public readonly Vector3 EndPosition;

        public ResolvedObstacleArc(
            float startX,
            float endX,
            float peakX,
            float peakY,
            float targetSpacing,
            Vector3 startPosition,
            Vector3 endPosition)
        {
            StartX = startX;
            EndX = endX;
            PeakX = peakX;
            PeakY = peakY;
            TargetSpacing = targetSpacing;
            StartPosition = startPosition;
            EndPosition = endPosition;
        }
    }

    /// <summary>Returns the biome that is currently generating terrain.</summary>
    public BiomeData CurrentBiome => biomeManager != null ? biomeManager.CurrentBiome : BiomeManager.Instance != null ? BiomeManager.Instance.CurrentBiome : null;

    /// <summary>
    /// Samples the generated terrain surface at a given world X position.
    /// Returns the exact spline position/normal used to build the chunk.
    /// </summary>
    public bool TryGetSurfaceAtX(float worldX, out Vector3 worldPos, out Vector3 worldNormal)
    {
        foreach (TerrainChunk chunk in activeChunks)
        {
            if (chunk == null || !chunk.ProvidesGameplaySurface)
            {
                continue;
            }

            float chunkX = chunk.transform.position.x;
            float chunkEndX = chunkX + chunkWidth;

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

    public bool IsGapNearX(float worldX, float paddingX)
    {
        if (activeGapChunks.Count == 0)
        {
            return false;
        }

        paddingX = Mathf.Max(0f, paddingX);
        activeGapChunks.RemoveWhere(chunk => chunk == null);

        foreach (TerrainChunk chunk in activeGapChunks)
        {
            float chunkX = chunk.transform.position.x;
            float chunkEndX = chunkX + Mathf.Max(chunk.chunkWidth, chunkWidth);

            if (worldX >= chunkX - paddingX && worldX <= chunkEndX + paddingX)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryGetZiplineSurfacePoint(TerrainChunk chunk, out Vector3 worldPos)
    {
        if (chunk != null && chunk.ProvidesGameplaySurface)
        {
            return chunk.EvaluateSurface(ZiplineAnchorSurfaceT, out worldPos, out _);
        }

        worldPos = default;
        return false;
    }

    private int GetChunksToKeepBehindPlayer()
    {
        return Mathf.Max(0, chunksBehindPlayer - 1);
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (featureSpawner == null)
        {
            featureSpawner = GetComponent<TerrainFeatureSpawner>();
        }

        if (featureSpawner == null)
        {
            Debug.LogWarning("[TerrainManager] Assign a TerrainFeatureSpawner in the scene to enable procedural feature spawning.", this);
        }

        ResolveBiomeManager();
        ResolvePlayerReference();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void OnValidate()
    {
        visibleChunksAhead = Mathf.Max(1, visibleChunksAhead);
        chunkWidth = Mathf.Max(0.01f, chunkWidth);
        chunksBehindPlayer = Mathf.Max(1, chunksBehindPlayer);
        startupTerrainBackOffsetX = Mathf.Max(0f, startupTerrainBackOffsetX);
        ziplineSpawnDelayChunks = Mathf.Max(0, ziplineSpawnDelayChunks);
        ziplineAbilityOrbMinCount = Mathf.Max(1, ziplineAbilityOrbMinCount);
        ziplineAbilityOrbMaxCount = Mathf.Max(ziplineAbilityOrbMinCount, ziplineAbilityOrbMaxCount);
        terrainAbilityOrbMinCount = Mathf.Max(1, terrainAbilityOrbMinCount);
        terrainAbilityOrbMaxCount = Mathf.Max(terrainAbilityOrbMinCount, terrainAbilityOrbMaxCount);
        terrainAbilityOrbSpacing = Mathf.Max(0.1f, terrainAbilityOrbSpacing);
        terrainAbilityOrbPathSamples = Mathf.Clamp(terrainAbilityOrbPathSamples, 8, 128);
        abilityOrbMinGapChunks = Mathf.Max(0, abilityOrbMinGapChunks);
        abilityOrbMaxGapChunks = Mathf.Max(abilityOrbMinGapChunks, abilityOrbMaxGapChunks);
        abilityOrbEndpointPadding = Mathf.Clamp(abilityOrbEndpointPadding, 0f, 0.45f);
        terrainAbilityOrbHeight = Mathf.Max(0f, terrainAbilityOrbHeight);
        ziplineAbilityOrbHeight = Mathf.Max(0f, ziplineAbilityOrbHeight);
    }

    void Start()
    {
        ResolvePlayerReference();
        ResolveBiomeManager();
        biomeManager?.EnsureInitialized();

        ResetGenerationState();
        SpawnStartupTerrain();
        FillTerrainAhead();
    }

    void Update()
    {
        ResolvePlayerReference();
        if (player == null)
        {
            return;
        }

        FillTerrainAhead();

        float chunkAliveThreshold = player.position.x - GetChunksToKeepBehindPlayer() * chunkWidth;
        while (activeChunks.Count > 0 && activeChunks.Peek().transform.position.x + chunkWidth < chunkAliveThreshold)
        {
            TerrainChunk expiredChunk = activeChunks.Dequeue();
            if (expiredChunk == lastSpawnedChunk)
            {
                lastSpawnedChunk = null;
            }
            DestroyChunk(expiredChunk);
        }

        for (int i = activeZiplines.Count - 1; i >= 0; i--)
        {
            ZiplineBuilder zb = activeZiplines[i];
            if (zb == null)
            {
                RemoveActiveZiplineAt(i);
                continue;
            }
            // Only consider a zipline for cleanup once it's fully built (TotalLength > 0) -
            // a still-in-progress one (waiting on a safe chunk to end on) must stay alive.
            if (zb.TotalLength > 0f && zb.EndAnchorWorld.x < chunkAliveThreshold)
            {
                DestroyZipline(zb);
                RemoveActiveZiplineAt(i);
            }
        }
    }

    private void RemoveActiveZiplineAt(int index)
    {
        ZiplineBuilder removedZipline = activeZiplines[index];
        activeZiplines.RemoveAt(index);
        ClearRemovedZiplineReferences(removedZipline);
    }

    private void ClearRemovedZiplineReferences(ZiplineBuilder removedZipline)
    {
        if (lastCompletedZipline == removedZipline)
        {
            lastCompletedZipline = null;
            ResetZiplineSequenceProgress();
        }

        if (pendingZipline == removedZipline)
        {
            ResetActiveZipline(true);
        }
    }


    private void ResolveBiomeManager()
    {
        if (biomeManager != null)
        {
            return;
        }

        biomeManager = BiomeManager.Instance;

        if (biomeManager == null)
        {
            Debug.LogWarning("[TerrainManager] Assign a BiomeManager reference or ensure BiomeManager.Instance is initialized in the scene.", this);
        }
    }

    private void SpawnStartupTerrain()
    {
        TerrainSequenceDefinition startupSequence =
            CurrentBiome != null ? CurrentBiome.startupTerrain : null;

        if (startupSequence != null && startupSequence.HasChunks && SpawnTerrainSequence(startupSequence))
        {
            return;
        }

        SpawnChunk(GetStartupTerrainChunk());
    }

    private bool SpawnTerrainSequence(TerrainSequenceDefinition sequence)
    {
        if (sequence == null || sequence.chunks == null)
        {
            return false;
        }

        int validChunkCount = 0;
        for (int i = 0; i < sequence.chunks.Length; i++)
        {
            if (sequence.chunks[i] != null)
            {
                validChunkCount++;
            }
        }

        if (validChunkCount == 0)
        {
            return false;
        }

        int sequenceChunkIndex = 0;
        for (int i = 0; i < sequence.chunks.Length; i++)
        {
            TerrainChunkDefinition chunkDefinition = sequence.chunks[i];
            if (chunkDefinition == null)
            {
                continue;
            }

            SpawnChunk(new PlannedTerrainChunk(
                sequence,
                chunkDefinition,
                sequenceChunkIndex,
                validChunkCount));
            sequenceChunkIndex++;
        }

        return true;
    }

    private void FillTerrainAhead()
    {
        if (player == null)
        {
            return;
        }

        float spawnThreshold = player.position.x + visibleChunksAhead * chunkWidth;
        while (nextSpawnX < spawnThreshold)
        {
            float previousSpawnX = nextSpawnX;
            SpawnChunk(GetNextTerrainChunk());

            if (nextSpawnX <= previousSpawnX + 0.0001f)
            {
                Debug.LogWarning("[TerrainManager] Stopped terrain fill because spawning did not advance nextSpawnX.", this);
                break;
            }
        }
    }

    private PlannedTerrainChunk GetNextTerrainChunk()
    {
        ClearInvalidRegulationSequenceRequest(CurrentBiome);
        if (ShouldForceZiplineSafeTerrain())
        {
            TryQueueRegulationSequenceRequest(CurrentBiome);
        }

        if (hasPendingRegulationSequence && !ShouldForceZiplineSafeTerrain())
        {
            return sequencePlanner.StartSequenceNow(BeginPendingRegulationSequence());
        }

        PlannedTerrainChunk plannedChunk = sequencePlanner.NextTerrainChunk(PickTerrainSequence);
        return ApplyZiplineTerrainOverride(plannedChunk);
    }

    private PlannedTerrainChunk GetStartupTerrainChunk()
    {
        TerrainSequenceDefinition startupSequence = CurrentBiome != null ? CurrentBiome.GetStartupTerrainSequence() : null;
        return new PlannedTerrainChunk(startupSequence, startupSequence != null ? startupSequence.FirstChunk : null);
    }

    /// <summary>
    /// Legacy UnityEvent hook retained for existing scene references. Biome transitions
    /// now run through ScreenFader immediately instead of spawning a door.
    /// </summary>
    public void PrepareBiomeTransitionDoor()
    {
    }

    /// <summary>Legacy UnityEvent hook retained for existing scene references.</summary>
    public void CancelBiomeTransitionDoor()
    {
        ClearPendingTransitionDoor();
    }

    /// <summary>
    /// Performs the actual biome swap: commits BiomeManager's pending transition, wipes
    /// all currently generated runtime terrain/features, and rebuilds
    /// from scratch using the newly committed biome. Intended to be called while the screen
    /// is fully white (see ScreenFader) so the player never sees the reset happen.
    /// </summary>
    public void ExecuteBiomeTransitionReset()
    {
        ClearPendingTransitionDoor();

        ResolveBiomeManager();
        biomeManager?.CommitPendingTransition();

        ClearRandomEventObstacles();
        ClearActiveChunks();
        ClearActiveZiplines();
        ResetZiplineRuntimeState();

        // Fresh planner so no sequencing state leaks over from the old biome.
        sequencePlanner = new TerrainSequencePlanner();

        TeleportPersistentObjectsForBiomeTransition();
        ResetGenerationState();
        SpawnStartupTerrain();

        FillTerrainAhead();
        biomeManager?.QueueTransitionFromCurrentEmotionIfNeeded();
        SnapCameraToPlayer();
    }


    private void TeleportPersistentObjectsForBiomeTransition()
    {
        ResolvePlayerReference();
        if (player == null)
        {
            return;
        }

        Vector3 shift = GetPlayerShift();

        ShiftPlayer(shift);

        CameraFollow cam = GetMainCameraFollow();
        if (cam != null)
        {
            cam.OnWorldShift(shift);
        }

        ParallaxScroller[] parallaxLayers = FindObjectsByType<ParallaxScroller>(FindObjectsSortMode.None);
        for (int i = 0; i < parallaxLayers.Length; i++)
        {
            parallaxLayers[i].OnWorldShift(shift);
        }

        Physics2D.SyncTransforms();
    }

    private void ShiftPlayer(Vector3 shift)
    {
        PlayerController playerController = player.GetComponent<PlayerController>();
        Rigidbody2D playerRb = player.GetComponent<Rigidbody2D>();
        playerController?.CaptureWorldShiftState();

        if (playerRb == null)
        {
            player.position -= shift;
            playerController?.OnWorldShift();
            return;
        }

        Quaternion playerRotationBeforeShift = player.rotation;
        float playerRotationZBeforeShift = NormalizeAngle(playerRotationBeforeShift.eulerAngles.z);

        if (!hasPlayerInterpolationToRestore)
        {
            playerInterpolationBeforeShift = playerRb.interpolation;
            hasPlayerInterpolationToRestore = true;
        }

        playerRb.interpolation = RigidbodyInterpolation2D.None;

        // playerRb.position is the physics-simulation position (last set at the
        // previous FixedUpdate tick). player.position is the rendered transform,
        // which under RigidbodyInterpolation2D.Interpolate can sit slightly ahead
        // of or behind rb.position (sub-frame interpolation lag). The camera and
        // parallax layers are moved by a pure delta - "position -= shift" on
        // whatever they're currently showing - so they carry no visible jump.
        // Snapping player.position to exactly
        // (rb.position - shift) would silently discard that lag, moving the
        // player by a *different* amount than the camera/parallax moved by on this
        // exact frame and producing the 1-frame micro pop. Capture the lag first
        // and re-apply it so the player shifts by the same delta as everything else.
        Vector2 visualLag = (Vector2)player.position - playerRb.position;

        Vector2 shiftedPosition = playerRb.position - new Vector2(shift.x, shift.y);
        playerRb.position = shiftedPosition;
        player.position = new Vector3(
            shiftedPosition.x + visualLag.x,
            shiftedPosition.y + visualLag.y,
            player.position.z);
        player.rotation = playerRotationBeforeShift;
        playerRb.rotation = playerRotationZBeforeShift;

        // Note: no Physics2D.SyncTransforms() here - the transition teleport makes
        // one consolidated sync call after persistent objects have been moved.

        // Restore on the next physics tick (see FixedUpdate below) rather than after
        // a render-frame-counted delay - the two clocks aren't the same rate.
        restorePlayerInterpolationOnNextFixedUpdate = true;

        playerController?.OnWorldShift();
    }

    private void ResetGenerationState()
    {
        featureSpawner?.ResetObstacleGapCooldown();
        activeGapChunks.Clear();
        currentEndY = GetPlayerFootY();
        nextSpawnX = player != null ? player.position.x - startupTerrainBackOffsetX : 0f;
        prevSlopeVector = Vector3.right;
        lastSpawnedChunk = null;
        lastSpawnedChunkDefinition = null;
        hasLastSpawnedChunkDefinition = false;
        ResetZiplineSpawnDelay();
        regulationCooldownChunksRemaining = 0;
        ClearRegulationSequenceRuntimeState(true);
        ResetAbilityOrbSchedule();
    }

    private float GetPlayerFootY()
    {
        if (player == null)
        {
            return 0f;
        }

        PlayerController playerController = player.GetComponent<PlayerController>();
        return player.position.y + (playerController != null ? playerController.groundCheckOffset.y : 0f);
    }

    private void ResetAbilityOrbSchedule()
    {
        abilityOrbBatchPending = false;
        abilityOrbTerrainOrbsRemaining = 0;
        abilityOrbDistanceToNextTerrainOrb = 0f;
        abilityOrbChunksUntilBatch = RollAbilityOrbGap();
    }

    private void AdvanceAbilityOrbSchedule()
    {
        if (abilityOrbPrefab == null || abilityOrbBatchPending || abilityOrbTerrainOrbsRemaining > 0)
        {
            return;
        }

        if (abilityOrbChunksUntilBatch > 0)
        {
            abilityOrbChunksUntilBatch--;
            return;
        }

        abilityOrbBatchPending = true;
    }

    private int RollAbilityOrbGap()
    {
        int minGap = Mathf.Max(0, abilityOrbMinGapChunks);
        int maxGap = Mathf.Max(minGap, abilityOrbMaxGapChunks);
        return Random.Range(minGap, maxGap + 1);
    }

    private int RollZiplineAbilityOrbCount()
    {
        int minCount = Mathf.Max(1, ziplineAbilityOrbMinCount);
        int maxCount = Mathf.Max(minCount, ziplineAbilityOrbMaxCount);
        return Random.Range(minCount, maxCount + 1);
    }

    private int RollTerrainAbilityOrbCount()
    {
        int minCount = Mathf.Max(1, terrainAbilityOrbMinCount);
        int maxCount = Mathf.Max(minCount, terrainAbilityOrbMaxCount);
        return Random.Range(minCount, maxCount + 1);
    }

    private void CompleteAbilityOrbBatch()
    {
        abilityOrbBatchPending = false;
        abilityOrbTerrainOrbsRemaining = 0;
        abilityOrbDistanceToNextTerrainOrb = 0f;
        abilityOrbChunksUntilBatch = RollAbilityOrbGap();
    }

    private void TrySpawnPendingAbilityOrbBatch(
        TerrainChunk chunk,
        TerrainChunkDefinition chunkDefinition,
        GameObject[] groundObstacles,
        bool ziplineOccupiedChunk)
    {
        if (abilityOrbPrefab == null || chunk == null)
        {
            return;
        }

        if (abilityOrbBatchPending)
        {
            // A batch that becomes due on a zipline is reserved for that cable until
            // its latest span completes. Otherwise it becomes a terrain streak.
            if (ziplineOccupiedChunk)
            {
                return;
            }

            abilityOrbBatchPending = false;
            abilityOrbTerrainOrbsRemaining = RollTerrainAbilityOrbCount();
            abilityOrbDistanceToNextTerrainOrb = ResolveTerrainAbilityOrbSpacing(chunkDefinition) * 0.5f;
        }

        if (abilityOrbTerrainOrbsRemaining <= 0 || ziplineOccupiedChunk)
        {
            return;
        }

        if (!TrySpawnTerrainAbilityOrbsOnChunk(
                chunk,
                chunkDefinition,
                groundObstacles,
                out int spawnedCount,
                out bool completedEncounter))
        {
            return;
        }

        if (completedEncounter)
        {
            CompleteAbilityOrbBatch();
            return;
        }

        abilityOrbTerrainOrbsRemaining -= spawnedCount;
        if (abilityOrbTerrainOrbsRemaining <= 0)
        {
            CompleteAbilityOrbBatch();
        }
    }

    private void TrySpawnPendingAbilityOrbBatchOnZipline(ZiplineBuilder zipline)
    {
        if (!abilityOrbBatchPending || abilityOrbPrefab == null || zipline == null)
        {
            return;
        }

        int spawnedCount = zipline.SpawnPrefabsAlongLatestSpan(
            abilityOrbPrefab,
            RollZiplineAbilityOrbCount(),
            abilityOrbEndpointPadding,
            ziplineAbilityOrbHeight);

        if (spawnedCount > 0)
        {
            CompleteAbilityOrbBatch();
        }
    }

    private bool TrySpawnTerrainAbilityOrbsOnChunk(
        TerrainChunk chunk,
        TerrainChunkDefinition chunkDefinition,
        GameObject[] groundObstacles,
        out int spawnedCount,
        out bool completedEncounter)
    {
        spawnedCount = 0;
        completedEncounter = false;
        if (!TryBuildTerrainAbilityOrbPath(
                chunk,
                chunkDefinition,
                groundObstacles,
                out List<ResolvedObstacleArc> obstacleArcs,
                out Vector3[] pathPositions,
                out float[] cumulativeLengths,
                out float[] segmentSpacings,
                out float pathLength))
        {
            return false;
        }

        if (obstacleArcs.Count > 0)
        {
            spawnedCount = SpawnFittedObstacleAbilityOrbs(chunk, obstacleArcs);
            completedEncounter = spawnedCount > 0;
            return spawnedCount > 0;
        }

        float nextDistance = Mathf.Max(0f, abilityOrbDistanceToNextTerrainOrb);

        while (spawnedCount < abilityOrbTerrainOrbsRemaining && nextDistance <= pathLength + 0.001f)
        {
            Vector3 spawnPosition = PositionAtPathDistance(
                pathPositions,
                cumulativeLengths,
                segmentSpacings,
                pathLength,
                nextDistance,
                out float spacing);
            SpawnAbilityOrb(spawnPosition, chunk.transform);
            spawnedCount++;
            nextDistance += Mathf.Max(0.1f, spacing);
        }

        abilityOrbDistanceToNextTerrainOrb = Mathf.Max(0f, nextDistance - pathLength);
        return true;
    }

    private bool TryBuildTerrainAbilityOrbPath(
        TerrainChunk chunk,
        TerrainChunkDefinition chunkDefinition,
        GameObject[] groundObstacles,
        out List<ResolvedObstacleArc> obstacleArcs,
        out Vector3[] pathPositions,
        out float[] cumulativeLengths,
        out float[] segmentSpacings,
        out float pathLength)
    {
        obstacleArcs = null;
        pathPositions = null;
        cumulativeLengths = null;
        segmentSpacings = null;
        pathLength = 0f;

        if (chunk == null || chunk.chunkWidth <= 0f)
        {
            return false;
        }

        float chunkLeft = chunk.transform.position.x;
        float chunkRight = chunkLeft + chunk.chunkWidth;
        float terrainSpacing = ResolveTerrainAbilityOrbSpacing(chunkDefinition);
        float terrainHeight = ResolveTerrainAbilityOrbHeight(chunkDefinition);
        obstacleArcs = BuildObstacleArcs(
            groundObstacles,
            chunkLeft,
            chunkRight);

        int sampleCount = Mathf.Clamp(terrainAbilityOrbPathSamples, 8, 128);
        List<float> sampleWorldXs = new List<float>(sampleCount + obstacleArcs.Count * 3 + 1);
        for (int i = 0; i <= sampleCount; i++)
        {
            sampleWorldXs.Add(Mathf.Lerp(chunkLeft, chunkRight, i / (float)sampleCount));
        }

        for (int i = 0; i < obstacleArcs.Count; i++)
        {
            ResolvedObstacleArc arc = obstacleArcs[i];
            sampleWorldXs.Add(arc.StartX);
            sampleWorldXs.Add(arc.PeakX);
            sampleWorldXs.Add(arc.EndX);
        }

        sampleWorldXs.Sort();
        for (int i = sampleWorldXs.Count - 1; i > 0; i--)
        {
            if (Mathf.Abs(sampleWorldXs[i] - sampleWorldXs[i - 1]) <= 0.0001f)
            {
                sampleWorldXs.RemoveAt(i);
            }
        }

        pathPositions = new Vector3[sampleWorldXs.Count];
        cumulativeLengths = new float[sampleWorldXs.Count];
        segmentSpacings = new float[Mathf.Max(0, sampleWorldXs.Count - 1)];

        for (int i = 0; i < sampleWorldXs.Count; i++)
        {
            float worldX = sampleWorldXs[i];
            Vector3 orbPosition;

            int obstacleArcIndex = FindObstacleArcAtX(obstacleArcs, worldX);
            if (obstacleArcIndex >= 0)
            {
                ResolvedObstacleArc arc = obstacleArcs[obstacleArcIndex];
                float arcT = Mathf.InverseLerp(arc.StartX, arc.EndX, worldX);
                orbPosition = PositionOnObstacleArc(arc, arcT);
            }
            else
            {
                if (!TryEvaluateChunkSurfaceAtWorldX(
                        chunk,
                        worldX,
                        out Vector3 surfacePosition,
                        out Vector3 surfaceNormal))
                {
                    pathPositions = null;
                    cumulativeLengths = null;
                    segmentSpacings = null;
                    pathLength = 0f;
                    return false;
                }

                Vector3 safeNormal = surfaceNormal.sqrMagnitude > 0.0001f
                    ? surfaceNormal.normalized
                    : Vector3.up;
                orbPosition = surfacePosition + safeNormal * terrainHeight;
            }

            pathPositions[i] = orbPosition;
            if (i > 0)
            {
                cumulativeLengths[i] = cumulativeLengths[i - 1]
                    + Vector3.Distance(pathPositions[i - 1], pathPositions[i]);

                float segmentWorldX = (sampleWorldXs[i - 1] + sampleWorldXs[i]) * 0.5f;
                int segmentArcIndex = FindObstacleArcAtX(obstacleArcs, segmentWorldX);
                segmentSpacings[i - 1] = segmentArcIndex >= 0
                    ? obstacleArcs[segmentArcIndex].TargetSpacing
                    : terrainSpacing;
            }
        }

        pathLength = cumulativeLengths[cumulativeLengths.Length - 1];
        return pathLength > 0.0001f;
    }

    private int SpawnFittedObstacleAbilityOrbs(
        TerrainChunk chunk,
        List<ResolvedObstacleArc> obstacleArcs)
    {
        if (chunk == null || obstacleArcs == null || obstacleArcs.Count == 0)
        {
            return 0;
        }

        int spawnedCount = 0;
        for (int arcIndex = 0; arcIndex < obstacleArcs.Count; arcIndex++)
        {
            ResolvedObstacleArc arc = obstacleArcs[arcIndex];
            if (!TryBuildObstacleArcPath(
                    arc,
                    out Vector3[] arcPositions,
                    out float[] cumulativeLengths,
                    out float arcLength,
                    out float peakDistance))
            {
                continue;
            }

            float spacing = Mathf.Max(0.1f, arc.TargetSpacing);
            List<float> spawnDistances = new List<float> { peakDistance };

            for (float distance = peakDistance - spacing;
                 distance >= -0.0001f;
                 distance -= spacing)
            {
                spawnDistances.Add(Mathf.Max(0f, distance));
            }

            for (float distance = peakDistance + spacing;
                 distance <= arcLength + 0.0001f;
                 distance += spacing)
            {
                spawnDistances.Add(Mathf.Min(arcLength, distance));
            }

            spawnDistances.Sort();
            for (int distanceIndex = 0; distanceIndex < spawnDistances.Count; distanceIndex++)
            {
                Vector3 spawnPosition = PositionAtPathDistance(
                    arcPositions,
                    cumulativeLengths,
                    arcLength,
                    spawnDistances[distanceIndex]);
                SpawnAbilityOrb(spawnPosition, chunk.transform);
                spawnedCount++;
            }
        }

        return spawnedCount;
    }

    private bool TryBuildObstacleArcPath(
        ResolvedObstacleArc arc,
        out Vector3[] pathPositions,
        out float[] cumulativeLengths,
        out float pathLength,
        out float peakDistance)
    {
        int sampleCount = Mathf.Clamp(terrainAbilityOrbPathSamples, 8, 128);
        List<float> sampleTs = new List<float>(sampleCount + 2);
        for (int i = 0; i <= sampleCount; i++)
        {
            sampleTs.Add(i / (float)sampleCount);
        }

        float peakT = Mathf.InverseLerp(arc.StartX, arc.EndX, arc.PeakX);
        peakT = Mathf.Clamp(peakT, 0.0001f, 0.9999f);
        sampleTs.Add(peakT);
        sampleTs.Sort();
        for (int i = sampleTs.Count - 1; i > 0; i--)
        {
            if (Mathf.Abs(sampleTs[i] - sampleTs[i - 1]) <= 0.0001f)
            {
                sampleTs.RemoveAt(i);
            }
        }

        pathPositions = new Vector3[sampleTs.Count];
        cumulativeLengths = new float[sampleTs.Count];
        peakDistance = 0f;
        for (int i = 0; i < sampleTs.Count; i++)
        {
            pathPositions[i] = PositionOnObstacleArc(arc, sampleTs[i]);
            if (i > 0)
            {
                cumulativeLengths[i] = cumulativeLengths[i - 1]
                    + Vector3.Distance(pathPositions[i - 1], pathPositions[i]);
            }

            if (Mathf.Abs(sampleTs[i] - peakT) <= 0.0001f)
            {
                peakDistance = cumulativeLengths[i];
            }
        }

        pathLength = cumulativeLengths[cumulativeLengths.Length - 1];
        return pathLength > 0.0001f;
    }

    private static Vector3 PositionOnObstacleArc(ResolvedObstacleArc arc, float arcT)
    {
        arcT = Mathf.Clamp01(arcT);
        Vector3 position = Vector3.Lerp(arc.StartPosition, arc.EndPosition, arcT);
        float peakT = Mathf.InverseLerp(arc.StartX, arc.EndX, arc.PeakX);
        peakT = Mathf.Clamp(peakT, 0.0001f, 0.9999f);

        if (arcT <= peakT)
        {
            float remaining = 1f - arcT / peakT;
            position.y = arc.PeakY
                + (arc.StartPosition.y - arc.PeakY) * remaining * remaining;
        }
        else
        {
            float elapsed = (arcT - peakT) / (1f - peakT);
            position.y = arc.PeakY
                + (arc.EndPosition.y - arc.PeakY) * elapsed * elapsed;
        }

        return position;
    }

    private float ResolveTerrainAbilityOrbSpacing(TerrainChunkDefinition chunkDefinition)
    {
        return chunkDefinition != null && chunkDefinition.OverridesAbilityOrbTrailSettings
            ? chunkDefinition.AbilityOrbSpacing
            : Mathf.Max(0.1f, terrainAbilityOrbSpacing);
    }

    private float ResolveTerrainAbilityOrbHeight(TerrainChunkDefinition chunkDefinition)
    {
        return chunkDefinition != null && chunkDefinition.OverridesAbilityOrbTrailSettings
            ? chunkDefinition.AbilityOrbHeight
            : Mathf.Max(0f, terrainAbilityOrbHeight);
    }

    private List<ResolvedObstacleArc> BuildObstacleArcs(
        GameObject[] groundObstacles,
        float chunkLeft,
        float chunkRight)
    {
        List<ObstacleArcDescriptor> candidates = new List<ObstacleArcDescriptor>();
        if (groundObstacles != null)
        {
            for (int i = 0; i < groundObstacles.Length; i++)
            {
                GameObject obstacle = groundObstacles[i];
                if (obstacle == null)
                {
                    continue;
                }

                if (!TryGetAbilityOrbObstacleProfile(
                        obstacle,
                        out IAbilityOrbObstacleProfile profile))
                {
                    continue;
                }

                Vector3 obstaclePosition = obstacle.transform.position;
                float peakX = obstaclePosition.x + profile.CenterOffsetX;
                if (peakX < chunkLeft - 0.0001f || peakX > chunkRight + 0.0001f)
                {
                    continue;
                }

                float halfSpan = Mathf.Max(0.1f, profile.CurveLength) * 0.5f;
                float startX = peakX - halfSpan;
                float endX = peakX + halfSpan;
                if (endX - startX <= 0.01f)
                {
                    continue;
                }

                ObstacleArcDescriptor candidate = new ObstacleArcDescriptor
                {
                    StartX = startX,
                    EndX = endX,
                    PeakX = peakX,
                    PeakY = obstaclePosition.y + Mathf.Max(0f, profile.CurveHeight),
                    TargetSpacing = Mathf.Max(0.1f, profile.TargetOrbSpacing),
                    StartPosition = new Vector3(startX, obstaclePosition.y, obstaclePosition.z),
                    EndPosition = new Vector3(endX, obstaclePosition.y, obstaclePosition.z)
                };
                candidates.Add(candidate);
            }
        }

        candidates.Sort((left, right) => left.StartX.CompareTo(right.StartX));
        List<ObstacleArcDescriptor> merged = MergeOverlappingObstacleArcs(candidates);
        List<ResolvedObstacleArc> resolved = new List<ResolvedObstacleArc>(merged.Count);
        for (int i = 0; i < merged.Count; i++)
        {
            if (TryResolveObstacleArc(merged[i], out ResolvedObstacleArc arc))
            {
                resolved.Add(arc);
            }
        }

        return resolved;
    }

    private static List<ObstacleArcDescriptor> MergeOverlappingObstacleArcs(
        List<ObstacleArcDescriptor> candidates)
    {
        List<ObstacleArcDescriptor> merged = new List<ObstacleArcDescriptor>();
        for (int i = 0; i < candidates.Count; i++)
        {
            ObstacleArcDescriptor candidate = candidates[i];
            if (merged.Count == 0 || candidate.StartX > merged[merged.Count - 1].EndX + 0.0001f)
            {
                merged.Add(candidate);
                continue;
            }

            ObstacleArcDescriptor current = merged[merged.Count - 1];
            if (candidate.EndX > current.EndX)
            {
                current.EndX = candidate.EndX;
                current.EndPosition = candidate.EndPosition;
            }

            current.TargetSpacing = Mathf.Min(current.TargetSpacing, candidate.TargetSpacing);
            if (candidate.PeakY > current.PeakY)
            {
                current.PeakX = candidate.PeakX;
                current.PeakY = candidate.PeakY;
            }
        }

        return merged;
    }

    private static bool TryResolveObstacleArc(
        ObstacleArcDescriptor descriptor,
        out ResolvedObstacleArc resolved)
    {
        resolved = default;
        float peakX = Mathf.Clamp(descriptor.PeakX, descriptor.StartX, descriptor.EndX);

        resolved = new ResolvedObstacleArc(
            descriptor.StartX,
            descriptor.EndX,
            peakX,
            descriptor.PeakY,
            Mathf.Max(0.1f, descriptor.TargetSpacing),
            descriptor.StartPosition,
            descriptor.EndPosition);
        return true;
    }

    private static bool TryGetAbilityOrbObstacleProfile(
        GameObject obstacle,
        out IAbilityOrbObstacleProfile profile)
    {
        profile = null;
        if (obstacle == null)
        {
            return false;
        }

        MonoBehaviour[] behaviours = obstacle.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IAbilityOrbObstacleProfile candidate)
            {
                profile = candidate;
                return true;
            }
        }

        return false;
    }

    private static int FindObstacleArcAtX(List<ResolvedObstacleArc> arcs, float worldX)
    {
        for (int i = 0; i < arcs.Count; i++)
        {
            if (worldX >= arcs[i].StartX - 0.0001f && worldX <= arcs[i].EndX + 0.0001f)
            {
                return i;
            }
        }

        return -1;
    }

    private static Vector3 PositionAtPathDistance(
        Vector3[] positions,
        float[] cumulativeLengths,
        float pathLength,
        float distance)
    {
        if (positions == null
            || positions.Length == 0
            || cumulativeLengths == null
            || cumulativeLengths.Length == 0)
        {
            return Vector3.zero;
        }

        distance = Mathf.Clamp(distance, 0f, pathLength);
        int low = 0;
        int high = cumulativeLengths.Length - 1;
        while (low < high)
        {
            int mid = (low + high) / 2;
            if (cumulativeLengths[mid] < distance)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }

        int index = Mathf.Clamp(low, 1, cumulativeLengths.Length - 1);
        float segmentLength = cumulativeLengths[index] - cumulativeLengths[index - 1];
        float segmentT = segmentLength > 0.0001f
            ? (distance - cumulativeLengths[index - 1]) / segmentLength
            : 0f;
        return Vector3.Lerp(positions[index - 1], positions[index], segmentT);
    }

    private static Vector3 PositionAtPathDistance(
        Vector3[] positions,
        float[] cumulativeLengths,
        float[] segmentSpacings,
        float pathLength,
        float distance,
        out float spacing)
    {
        spacing = 0.1f;
        if (positions == null
            || positions.Length == 0
            || cumulativeLengths == null
            || cumulativeLengths.Length == 0
            || segmentSpacings == null
            || segmentSpacings.Length == 0)
        {
            return Vector3.zero;
        }

        distance = Mathf.Clamp(distance, 0f, pathLength);
        int low = 0;
        int high = cumulativeLengths.Length - 1;
        while (low < high)
        {
            int mid = (low + high) / 2;
            if (cumulativeLengths[mid] < distance)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }

        int index = Mathf.Clamp(low, 1, cumulativeLengths.Length - 1);
        float segmentLength = cumulativeLengths[index] - cumulativeLengths[index - 1];
        float segmentT = segmentLength > 0.0001f
            ? (distance - cumulativeLengths[index - 1]) / segmentLength
            : 0f;

        int spacingIndex = index - 1;
        if (Mathf.Abs(distance - cumulativeLengths[index]) <= 0.0001f
            && index < segmentSpacings.Length)
        {
            spacingIndex = index;
        }

        spacing = segmentSpacings[Mathf.Clamp(spacingIndex, 0, segmentSpacings.Length - 1)];
        return Vector3.Lerp(positions[index - 1], positions[index], segmentT);
    }

    private bool TryEvaluateChunkSurfaceAtWorldX(
        TerrainChunk chunk,
        float worldX,
        out Vector3 worldPosition,
        out Vector3 worldNormal)
    {
        if (chunk == null || !chunk.ProvidesGameplaySurface || chunk.chunkWidth <= 0f)
        {
            worldPosition = default;
            worldNormal = Vector3.up;
            return false;
        }

        float chunkLeft = chunk.transform.position.x;
        float chunkRight = chunkLeft + chunk.chunkWidth;
        float surfaceT = 1f - Mathf.InverseLerp(chunkLeft, chunkRight, worldX);
        return chunk.EvaluateSurface(surfaceT, out worldPosition, out worldNormal);
    }

    private void SpawnAbilityOrb(Vector3 worldPosition, Transform parent)
    {
        GameObject orb = Instantiate(
            abilityOrbPrefab,
            worldPosition,
            abilityOrbPrefab.transform.rotation,
            parent);
        AbilityOrbPickup.EnsureOn(orb);
    }

    private TerrainChunk GetNewestActiveChunk()
    {
        TerrainChunk newestChunk = null;
        foreach (TerrainChunk chunk in activeChunks)
        {
            if (chunk != null && chunk.ProvidesGameplaySurface)
            {
                newestChunk = chunk;
            }
        }

        return newestChunk;
    }

    private void ClearPendingTransitionDoor()
    {
        if (pendingTransitionDoor == null)
        {
            return;
        }

        Destroy(pendingTransitionDoor);
        pendingTransitionDoor = null;
    }

    private void ClearActiveChunks()
    {
        while (activeChunks.Count > 0)
        {
            DestroyChunk(activeChunks.Dequeue());
        }

        lastSpawnedChunk = null;
    }

    private void ClearRandomEventObstacles()
    {
        TimedLaneObstacleSpawner[] spawners =
            FindObjectsByType<TimedLaneObstacleSpawner>(FindObjectsSortMode.None);
        for (int i = 0; i < spawners.Length; i++)
        {
            spawners[i].ClearActiveRandomEventObjects();
        }
    }

    private void DestroyChunk(TerrainChunk chunk)
    {
        if (chunk != null)
        {
            activeGapChunks.Remove(chunk);
            ChunkDestroyed?.Invoke(chunk);
            Destroy(chunk.gameObject);
        }
    }

    private void ClearActiveZiplines()
    {
        foreach (ZiplineBuilder zipline in activeZiplines)
        {
            DestroyZipline(zipline);
        }

        activeZiplines.Clear();
    }

    private void DestroyZipline(ZiplineBuilder zipline)
    {
        if (zipline != null)
        {
            Destroy(zipline.gameObject);
        }
    }

    private void SnapCameraToPlayer()
    {
        if (player == null)
        {
            return;
        }

        CameraFollow cam = GetMainCameraFollow();
        if (cam != null)
        {
            cam.SnapToTarget(player);
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            mainCamera.transform.position = new Vector3(
                player.position.x,
                player.position.y,
                mainCamera.transform.position.z);
        }
    }

    private CameraFollow GetMainCameraFollow()
    {
        Camera mainCamera = Camera.main;
        return mainCamera != null ? mainCamera.GetComponent<CameraFollow>() : null;
    }

    private void ResolvePlayerReference()
    {
        if (player == null)
        {
            GameObject playerObject = FindPlayerObject();
            if (playerObject == null)
            {
                return;
            }

            PlayerController playerController = playerObject.GetComponent<PlayerController>();
            if (playerController == null)
            {
                playerController = playerObject.GetComponentInParent<PlayerController>();
            }
            if (playerController == null)
            {
                playerController = playerObject.GetComponentInChildren<PlayerController>();
            }

            player = playerController != null ? playerController.transform : playerObject.transform;
        }
    }

    private GameObject FindPlayerObject()
    {
        try
        {
            return GameObject.FindGameObjectWithTag(PlayerTag);
        }
        catch (UnityException)
        {
            Debug.LogWarning($"[TerrainManager] No Unity tag named '{PlayerTag}' exists.", this);
            return null;
        }
    }

    private float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f)
        {
            angle -= 360f;
        }
        else if (angle < -180f)
        {
            angle += 360f;
        }

        return angle;
    }

    private Vector3 GetPlayerShift()
    {
        Rigidbody2D playerRb = player.GetComponent<Rigidbody2D>();
        if (playerRb != null)
        {
            return new Vector3(playerRb.position.x, playerRb.position.y, 0f);
        }

        return new Vector3(player.position.x, player.position.y, 0f);
    }

    void FixedUpdate()
    {
        if (!restorePlayerInterpolationOnNextFixedUpdate)
        {
            return;
        }

        // This FixedUpdate call is the first physics tick since the shift. Script
        // FixedUpdate callbacks run BEFORE the engine's internal step for this same
        // tick, so flipping interpolation back on here is still safe: the upcoming
        // step folds the already-teleported position into both the rigidbody's
        // "previous" and "current" interpolation samples, so they're consistent
        // (post-shift) by the time this frame renders. Render frame count is the
        // wrong clock for this - fixedDeltaTime (50Hz by default) is almost always
        // slower than the display's render rate, so several Update/render frames can
        // pass per physics tick. A coroutine timed off frames (yield return null,
        // WaitForEndOfFrame) can resume before this tick has actually run, turning
        // interpolation back on while the previous/current samples are still
        // mismatched - which is exactly what produces the slide/snap jitter.
        restorePlayerInterpolationOnNextFixedUpdate = false;

        if (!hasPlayerInterpolationToRestore || player == null)
        {
            hasPlayerInterpolationToRestore = false;
            return;
        }

        Rigidbody2D playerRb = player.GetComponent<Rigidbody2D>();
        if (playerRb != null)
        {
            playerRb.interpolation = playerInterpolationBeforeShift;
        }

        hasPlayerInterpolationToRestore = false;
    }


    void SpawnChunk(PlannedTerrainChunk plannedChunk)
    {
        if (!plannedChunk.IsValid)
        {
            Debug.LogError("[TerrainManager] No valid terrain sequence/chunk definition available to spawn.");
            return;
        }

        bool isPostZiplineBufferChunk = postZiplineSafeBufferChunks > 0;

        ResolveBiomeManager();
        biomeManager?.ConsumeChunkAndAdvanceIfNeeded();

        BiomeData biome = CurrentBiome;
        TerrainChunkDefinition chunkDefinition = plannedChunk.Chunk;
        bool isGapChunk = IsGapChunk(chunkDefinition);
        TerrainChunk prefabToUse = biome != null ? biome.GetChunkPrefab(chunkDefinition) : null;
        if (prefabToUse == null)
        {
            Debug.LogError("[TerrainManager] No TerrainChunk prefab assigned on the active BiomeData.");
            return;
        }

        // Biome-owned cutoff chunks end the rope on the previous safe chunk before they spawn.
        ForceZiplineEndBeforeCutoffChunk(chunkDefinition, biome);

        TerrainFeatureSpawner.PreparedChunkFeatures preparedFeatures = featureSpawner != null
            && !isGapChunk
            ? featureSpawner.PrepareForChunk(chunkDefinition, biome)
            : null;
        bool forceFlatPuddleChunk = preparedFeatures != null && preparedFeatures.HasPuddle;

        TerrainChunk chunk = Instantiate(prefabToUse, transform);
        chunk.transform.position = new Vector3(nextSpawnX, currentEndY, 0f);
        chunk.chunkWidth = chunkWidth;
        chunk.startY = currentEndY;
        chunk.cliffHeight = 0f;
        chunk.activeBiome = biome;

        float verticalDrop = 0f;
        float requiredDepth = baseDepth;

        ApplyChunkDefinitionHeight(
            chunk,
            chunkDefinition,
            forceFlatPuddleChunk,
            ref verticalDrop,
            ref requiredDepth);

        chunk.depth = Mathf.Max(requiredDepth, verticalDrop + depthSafetyMargin);

        if (forceFlatPuddleChunk)
        {
            featureSpawner.ConfigureChunkBeforeBuild(chunk, preparedFeatures);
        }

        Vector3 slopeVector = new Vector3(chunkWidth, chunk.endY - chunk.startY, 0f).normalized;
        Vector3 entryTangent = ((prevSlopeVector + slopeVector) * 0.5f).normalized;
        if (lastSpawnedChunk != null)
        {
            lastSpawnedChunk.RebuildExitTangent(entryTangent);
        }

        chunk.Build(entryTangent, slopeVector);
        prevSlopeVector = slopeVector;

        TerrainFeatureSpawner.SpawnedChunkFeatures spawnedFeatures = default;
        bool canSpawnGroundFeatures = !isGapChunk && chunk.ProvidesGameplaySurface;
        if (featureSpawner != null && canSpawnGroundFeatures)
        {
            spawnedFeatures = featureSpawner.SpawnPreparedForChunk(chunk, chunkDefinition, biome, preparedFeatures);
        }

        bool isActiveRegulationSequenceChunk = IsActiveRegulationSequenceChunk(plannedChunk);
        bool completedRegulationSequenceThisChunk = false;
        if (isActiveRegulationSequenceChunk)
        {
            completedRegulationSequenceThisChunk = HandleActiveRegulationSequenceChunk(plannedChunk, chunk);
        }
        else if (canSpawnGroundFeatures)
        {
            TrySpawnRegulationMethod(chunk, chunkDefinition, biome);
        }

        AdvanceAbilityOrbSchedule();

        bool isZiplineSafeSequence = IsZiplineSafeSequence(plannedChunk.Sequence, biome);
        bool isZiplineSafeSurface = isZiplineSafeSequence && canSpawnGroundFeatures;
        if (isZiplineSafeSurface && TryGetZiplineSurfacePoint(chunk, out Vector3 safeSurfacePos))
        {
            lastSafeZiplineSurfacePos = safeSurfacePos;
            hasLastSafeZiplineSurfacePos = true;
        }
        bool ziplineOccupiedChunk = ziplineActive;
        UpdateZiplineForChunk(chunk, biome, isZiplineSafeSurface);
        ziplineOccupiedChunk |= ziplineActive;
        if (canSpawnGroundFeatures)
        {
            TrySpawnPendingAbilityOrbBatch(
                chunk,
                chunkDefinition,
                spawnedFeatures.GroundObstacles,
                ziplineOccupiedChunk);
        }
        if (isPostZiplineBufferChunk)
        {
            postZiplineSafeBufferChunks = Mathf.Max(0, postZiplineSafeBufferChunks - 1);
        }

        currentEndY = chunk.RightEdgeY;
        nextSpawnX += chunkWidth;
        activeChunks.Enqueue(chunk);
        if (isGapChunk)
        {
            activeGapChunks.Add(chunk);
        }
        lastSpawnedChunk = chunk;
        lastSpawnedChunkDefinition = chunkDefinition;
        hasLastSpawnedChunkDefinition = true;
        AdvanceZiplineSpawnDelay();
        if (!completedRegulationSequenceThisChunk)
        {
            AdvanceRegulationCooldown();
        }
        ChunkSpawned?.Invoke(new SpawnedChunkInfo(
            chunk,
            plannedChunk.Sequence,
            chunkDefinition,
            biome,
            chunkWidth,
            chunk.depth,
            entryTangent,
            slopeVector));
    }

    private void ApplyChunkDefinitionHeight(
        TerrainChunk chunk,
        TerrainChunkDefinition chunkDefinition,
        bool forceFlat,
        ref float verticalDrop,
        ref float requiredDepth)
    {
        if (forceFlat)
        {
            chunk.endY = currentEndY;
            verticalDrop = 0f;
            return;
        }

        if (chunkDefinition == null)
        {
            chunk.endY = currentEndY;
            return;
        }

        switch (chunkDefinition.heightBehavior)
        {
            case TerrainChunkDefinition.HeightBehavior.Rise:
                chunk.endY = currentEndY + chunkDefinition.RollVerticalAmount();
                break;
            case TerrainChunkDefinition.HeightBehavior.Drop:
                chunk.endY = currentEndY - chunkDefinition.RollVerticalAmount();
                break;
            case TerrainChunkDefinition.HeightBehavior.Gap:
            case TerrainChunkDefinition.HeightBehavior.Flat:
            default:
                chunk.endY = currentEndY;
                break;
        }

        verticalDrop = Mathf.Max(0f, currentEndY - chunk.endY);

    }

    private static bool IsGapChunk(TerrainChunkDefinition chunkDefinition)
    {
        return chunkDefinition != null &&
            chunkDefinition.heightBehavior == TerrainChunkDefinition.HeightBehavior.Gap;
    }

    private void TrySpawnRegulationMethod(TerrainChunk chunk, TerrainChunkDefinition chunkDefinition, BiomeData biome)
    {
        if (chunk == null || biome == null || biome.regulationMethodPrefab == null)
        {
            return;
        }
        if (biome.regulationMethodPrefab.GetComponent<IRegulationTerrainSequenceProvider>() != null)
        {
            return;
        }
        if (!biome.AllowsRegulationSpawn(chunkDefinition))
        {
            return;
        }
        if (hasLastSpawnedChunkDefinition && biome.AllowsRegulationSpawn(lastSpawnedChunkDefinition))
        {
            return;
        }
        if (biomeManager == null || biomeManager.CurrentPhase != BiomePhase.Peaked)
        {
            return;
        }
        if (regulationCooldownChunksRemaining > 0)
        {
            return;
        }
        if (Random.value >= biome.regulationSpawnChance)
        {
            return;
        }

        GameObject regulationObject = Instantiate(biome.regulationMethodPrefab, chunk.transform);
        IRegulationSpawnPlacement placement = regulationObject.GetComponent<IRegulationSpawnPlacement>();
        if (placement != null && !placement.PlaceOnChunk(chunk))
        {
            Destroy(regulationObject);
            return;
        }

        Debug.Log($"[TerrainManager] Spawned regulation method for {biome.biomeName} at {regulationObject.transform.position}.");
        regulationCooldownChunksRemaining = Mathf.Max(0, biome.regulationCooldownChunks);
    }

    private bool TryQueueRegulationSequenceRequest(BiomeData biome)
    {
        if (hasPendingRegulationSequence || regulationSequenceActive || regulationCooldownChunksRemaining > 0)
        {
            return false;
        }
        if (biome == null || biome.regulationMethodPrefab == null)
        {
            return false;
        }
        if (biomeManager == null || biomeManager.CurrentPhase != BiomePhase.Peaked)
        {
            return false;
        }

        IRegulationTerrainSequenceProvider sequenceProvider =
            biome.regulationMethodPrefab.GetComponent<IRegulationTerrainSequenceProvider>();
        if (sequenceProvider == null)
        {
            return false;
        }
        if (Random.value >= biome.regulationSpawnChance)
        {
            return false;
        }
        if (!sequenceProvider.TryPickRegulationSequence(out TerrainSequenceDefinition sequence) || sequence == null || !sequence.HasChunks)
        {
            Debug.LogWarning($"[TerrainManager] Regulation spawn rolled for {biome.biomeName}, but no valid regulation sequence was available.");
            return false;
        }

        hasPendingRegulationSequence = true;
        pendingRegulationSequence = sequence;
        pendingRegulationBiome = biome;
        pendingRegulationPrefab = biome.regulationMethodPrefab;
        Debug.Log($"[TerrainManager] Queued regulation sequence {sequence.displayName} for {biome.biomeName}.");
        return true;
    }

    private TerrainSequenceDefinition BeginPendingRegulationSequence()
    {
        TerrainSequenceDefinition sequence = pendingRegulationSequence;
        regulationSequenceActive = true;
        activeRegulationSequence = pendingRegulationSequence;
        activeRegulationBiome = pendingRegulationBiome;
        activeRegulationPrefab = pendingRegulationPrefab;

        hasPendingRegulationSequence = false;
        pendingRegulationSequence = null;
        pendingRegulationBiome = null;
        pendingRegulationPrefab = null;

        Debug.Log($"[TerrainManager] Starting regulation sequence {sequence.displayName}.");
        return sequence;
    }

    private void ClearInvalidRegulationSequenceRequest(BiomeData currentBiome)
    {
        if (!hasPendingRegulationSequence)
        {
            return;
        }
        if (biomeManager != null && biomeManager.CurrentPhase == BiomePhase.Peaked && currentBiome == pendingRegulationBiome)
        {
            return;
        }

        hasPendingRegulationSequence = false;
        pendingRegulationSequence = null;
        pendingRegulationBiome = null;
        pendingRegulationPrefab = null;
    }

    private bool IsActiveRegulationSequenceChunk(PlannedTerrainChunk plannedChunk)
    {
        return regulationSequenceActive &&
            plannedChunk.IsValid &&
            plannedChunk.Sequence == activeRegulationSequence;
    }

    private bool HandleActiveRegulationSequenceChunk(PlannedTerrainChunk plannedChunk, TerrainChunk chunk)
    {
        SadnessRegulation controller = EnsureActiveSadnessRegulationController();
        if (controller != null)
        {
            controller.CreateLandmineForChunk(chunk, plannedChunk.SequenceChunkIndex);
        }

        if (!plannedChunk.IsLastChunkInSequence)
        {
            return false;
        }

        controller?.MarkSequenceComplete();
        regulationCooldownChunksRemaining = Mathf.Max(0, activeRegulationBiome != null ? activeRegulationBiome.regulationCooldownChunks : 0);
        regulationSequenceActive = false;
        activeRegulationSequence = null;
        activeRegulationBiome = null;
        activeRegulationPrefab = null;
        activeSadnessRegulation = null;
        return true;
    }

    private SadnessRegulation EnsureActiveSadnessRegulationController()
    {
        if (activeSadnessRegulation != null)
        {
            return activeSadnessRegulation;
        }
        if (activeRegulationPrefab == null)
        {
            return null;
        }

        GameObject regulationObject = Instantiate(activeRegulationPrefab, transform);
        activeSadnessRegulation = regulationObject.GetComponent<SadnessRegulation>();
        if (activeSadnessRegulation == null)
        {
            Debug.LogWarning("[TerrainManager] Active regulation sequence prefab does not contain SadnessRegulation.", regulationObject);
            Destroy(regulationObject);
            return null;
        }

        activeSadnessRegulations.Add(activeSadnessRegulation);
        return activeSadnessRegulation;
    }

    private void ClearRegulationSequenceRuntimeState(bool destroyControllers)
    {
        hasPendingRegulationSequence = false;
        pendingRegulationSequence = null;
        pendingRegulationBiome = null;
        pendingRegulationPrefab = null;
        regulationSequenceActive = false;
        activeRegulationSequence = null;
        activeRegulationBiome = null;
        activeRegulationPrefab = null;
        activeSadnessRegulation = null;

        if (!destroyControllers)
        {
            return;
        }

        for (int i = activeSadnessRegulations.Count - 1; i >= 0; i--)
        {
            if (activeSadnessRegulations[i] != null)
            {
                Destroy(activeSadnessRegulations[i].gameObject);
            }
        }

        activeSadnessRegulations.Clear();
    }

    private void AdvanceRegulationCooldown()
    {
        if (regulationCooldownChunksRemaining > 0)
        {
            regulationCooldownChunksRemaining--;
        }
    }


    private void UpdateZiplineForChunk(TerrainChunk chunk, BiomeData biome, bool isSafeType)
    {
        if (!ziplineActive)
        {
            TryStartZipline(chunk, biome, isSafeType);
            return;
        }

        if (ziplineChunksRemaining > 0)
        {
            ziplineChunksRemaining--;
        }

        if (ziplineChunksRemaining > 0)
        {
            return;
        }

        if (isSafeType)
        {
            EndZipline(chunk);
            return;
        }

        // The planned span is over but we're still in a chasm sequence - keep the rope growing
        // until we reach solid ground so the end pole never lands inside the gap. Capped so a
        // pathological run of unsafe chunks can't keep a zipline alive forever.
        ziplineExtendGuard++;
        if (ziplineExtendGuard >= ZiplineMaxExtendChunks)
        {
            Debug.LogWarning("[TerrainManager] Zipline exceeded its safety extension cap; ending it early.");
            EndZipline(chunk);
        }
    }

    private bool ShouldForceZiplineSafeTerrain()
    {
        bool sequenceStillBuilding = ziplineSequenceRequiredSpans > 0
            && ziplineSequenceCompletedSpans < ziplineSequenceRequiredSpans
            && (ziplineActive || ziplineSequenceSpansRemaining > 0);
        return sequenceStillBuilding || postZiplineSafeBufferChunks > 0;
    }

    private PlannedTerrainChunk ApplyZiplineTerrainOverride(PlannedTerrainChunk plannedChunk)
    {
        BiomeData biome = CurrentBiome;
        if (!ShouldForceZiplineSafeTerrain() || IsZiplineSafeSequence(plannedChunk.Sequence, biome))
        {
            return plannedChunk;
        }

        TryQueueRegulationSequenceRequest(biome);
        TerrainSequenceDefinition safeSequence = PickZiplineSafeTerrainSequence();
        return sequencePlanner.StartSequenceNow(safeSequence);
    }

    private TerrainSequenceDefinition PickZiplineSafeTerrainSequence()
    {
        return CurrentBiome != null ? CurrentBiome.PickZiplineSafeTerrainSequence() : null;
    }

    private void ResetZiplineSequenceProgress()
    {
        ziplineSequenceRequiredSpans = 0;
        ziplineSequenceCompletedSpans = 0;
        ziplineSequenceSpansRemaining = 0;
    }

    private void ResetActiveZipline(bool resetSequenceProgress)
    {
        ziplineActive = false;
        pendingZipline = null;
        ziplineChunksRemaining = 0;
        ziplineExtendGuard = 0;

        if (resetSequenceProgress)
        {
            ResetZiplineSequenceProgress();
        }
    }

    private void ResetZiplineRuntimeState()
    {
        ResetActiveZipline(true);
        lastCompletedZipline = null;
        hasLastSafeZiplineSurfacePos = false;
        postZiplineSafeBufferChunks = 0;
    }

    private void ResetZiplineSpawnDelay()
    {
        ziplineSpawnDelayChunksRemaining = Mathf.Max(0, ziplineSpawnDelayChunks);
    }

    private void AdvanceZiplineSpawnDelay()
    {
        if (ziplineSpawnDelayChunksRemaining > 0)
        {
            ziplineSpawnDelayChunksRemaining--;
        }
    }

    private void StartPostZiplineBuffer()
    {
        postZiplineSafeBufferChunks = 2;
        ResetZiplineSequenceProgress();
    }

    private void ForceZiplineEndBeforeCutoffChunk(TerrainChunkDefinition chunkDefinition, BiomeData biome)
    {
        if (!ziplineActive || pendingZipline == null || biome == null || !biome.IsZiplineCutoffChunk(chunkDefinition))
        {
            return;
        }

        if (!hasLastSafeZiplineSurfacePos)
        {
            Debug.LogWarning("[TerrainManager] No safe chunk was cached for a hard slide cutoff.");
            return;
        }

        EndZiplineAtSurface(lastSafeZiplineSurfacePos);
        // [Zipline Slide Prevention] A hard slide is a chain boundary. The active
        // span may end on the preceding safe chunk, but the next zipline must not
        // reuse that pole and create a segment across the slide.
        lastCompletedZipline = null;
        postZiplineSafeBufferChunks = 0;
        ResetZiplineSequenceProgress();
    }

    private void TryStartZipline(TerrainChunk chunk, BiomeData biome, bool isSafeType)
    {
        if (!isSafeType || postZiplineSafeBufferChunks > 0 || ziplineSpawnDelayChunksRemaining > 0)
        {
            return;
        }

        bool continuingSequence = ziplineSequenceSpansRemaining > 0;
        if (continuingSequence)
        {
            // [Zipline Chain] Continuations are forced by the rolled sequence length,
            // so connected spans do not depend on another spawn-chance roll.
            if (!TryBeginChainedZiplineSegment())
            {
                ResetZiplineSequenceProgress();
                return;
            }

            ziplineSequenceSpansRemaining--;
        }
        else
        {
            if (biome == null || biome.ziplinePrefab == null)
            {
                return;
            }
            if (Random.value >= biome.ziplineSpawnChance)
            {
                return;
            }

            if (!TryGetZiplineSurfacePoint(chunk, out Vector3 startSurfacePos))
            {
                return;
            }

            pendingZipline = Instantiate(biome.ziplinePrefab, transform);
            pendingZipline.buildOnStart = false;
            pendingZipline.BeginAt(startSurfacePos);
            activeZiplines.Add(pendingZipline);

            ziplineSequenceRequiredSpans = RollZiplineSequenceLength(biome);
            ziplineSequenceCompletedSpans = 0;
            ziplineSequenceSpansRemaining = ziplineSequenceRequiredSpans - 1;
        }

        int minSpan = Mathf.Max(1, biome != null ? biome.ziplineMinSpanChunks : 3);
        int maxSpan = Mathf.Max(minSpan, biome != null ? biome.ziplineMaxSpanChunks : 6);
        ziplineChunksRemaining = Random.Range(minSpan, maxSpan + 1);
        ziplineExtendGuard = 0;
        ziplineActive = true;
    }

    private int RollZiplineSequenceLength(BiomeData biome)
    {
        int minSequence = Mathf.Max(1, biome != null ? biome.ziplineMinSequence : 1);
        int maxSequence = Mathf.Max(minSequence, biome != null ? biome.ziplineMaxSequence : minSequence);
        return Random.Range(minSequence, maxSequence + 1);
    }

    private bool TryBeginChainedZiplineSegment()
    {
        if (lastCompletedZipline == null || lastCompletedZipline.gameObject == null)
        {
            lastCompletedZipline = null;
            return false;
        }

        pendingZipline = lastCompletedZipline;
        pendingZipline.buildOnStart = false;

        if (!pendingZipline.BeginChainedSegment())
        {
            pendingZipline = null;
            lastCompletedZipline = null;
            return false;
        }

        return true;
    }

    private void EndZipline(TerrainChunk chunk)
    {
        if (pendingZipline == null)
        {
            ResetActiveZipline(true);
            return;
        }

        if (TryGetZiplineSurfacePoint(chunk, out Vector3 endSurfacePos))
        {
            EndZiplineAtSurface(endSurfacePos);
        }
        else
        {
            Debug.LogWarning("[TerrainManager] Could not evaluate surface to end zipline; leaving start pole only.");
            lastCompletedZipline = null;
            ResetZiplineSequenceProgress();
            ResetActiveZipline(false);
        }
    }

    private void EndZiplineAtSurface(Vector3 endSurfacePos)
    {
        if (pendingZipline == null)
        {
            ResetActiveZipline(true);
            return;
        }

        pendingZipline.CompleteAt(endSurfacePos);
        TrySpawnPendingAbilityOrbBatchOnZipline(pendingZipline);
        lastCompletedZipline = pendingZipline.TotalLength > 0f ? pendingZipline : null;
        if (lastCompletedZipline != null && ziplineSequenceRequiredSpans > 0)
        {
            ziplineSequenceCompletedSpans++;
            if (ziplineSequenceCompletedSpans >= ziplineSequenceRequiredSpans)
            {
                lastCompletedZipline = null;
                StartPostZiplineBuffer();
            }
        }
        else if (lastCompletedZipline == null)
        {
            ResetZiplineSequenceProgress();
        }

        ResetActiveZipline(false);
    }

    private TerrainSequenceDefinition PickTerrainSequence()
    {
        BiomeData biome = CurrentBiome;
        ClearInvalidRegulationSequenceRequest(biome);

        if (ShouldForceZiplineSafeTerrain())
        {
            TryQueueRegulationSequenceRequest(biome);
            return PickZiplineSafeTerrainSequence();
        }

        if (hasPendingRegulationSequence)
        {
            return BeginPendingRegulationSequence();
        }

        if (!ziplineActive && TryQueueRegulationSequenceRequest(biome))
        {
            return BeginPendingRegulationSequence();
        }

        return ziplineActive
            ? PickZiplineSafeTerrainSequence()
            : biome != null ? biome.PickNormalTerrainSequence() : null;
    }

    private bool IsZiplineSafeSequence(TerrainSequenceDefinition sequence, BiomeData biome)
    {
        return biome != null && biome.IsZiplineSafeSequence(sequence);
    }
}
