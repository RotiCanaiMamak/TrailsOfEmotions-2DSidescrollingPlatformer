using System.Collections.Generic;
using UnityEngine;

public class SadnessRegulation : MonoBehaviour, IRegulationSpawnPlacement, IRegulationTerrainSequenceProvider
{
    private sealed class ChainState
    {
        public int chainCount;
        public bool waitingForNextLandmine;
    }

    [Header("Regulation Terrain")]
    [SerializeField] private TerrainSequenceDefinition[] regulationTerrainSequences;

    [Header("Mine Placement")]
    [SerializeField] private SadnessRegulationLandmine landminePrefab;
    [SerializeField, Min(1)]
    [Tooltip("Spawns on the first regulation-sequence chunk, then once every this many chunks.")]
    private int landmineIntervalChunks = 1;
    [Range(0f, 1f)] public float surfaceT = 0.5f;

    [Header("Launch")]
    [Min(0f)] public float baseUpwardLaunchSpeed = 15f;
    [Min(0f)] public float upwardLaunchBonusPerChain = 1.5f;
    [Min(0f)] public float maxUpwardLaunchSpeed = 32f;
    [Min(0f)] public float launchAdhesionLockDuration = 0.12f;

    [Header("Emotion")]
    [Min(0f)] public float emotionReductionPerLandmine = 8f;

    private readonly Dictionary<PlayerController, ChainState> chainStates = new Dictionary<PlayerController, ChainState>();
    private readonly Dictionary<TerrainChunk, SadnessRegulationLandmine> landminesByChunk = new Dictionary<TerrainChunk, SadnessRegulationLandmine>();
    private bool sequenceComplete;
    private bool subscribedToChunkDestroyed;

    private void OnValidate()
    {
        landmineIntervalChunks = Mathf.Max(1, landmineIntervalChunks);
        surfaceT = Mathf.Clamp01(surfaceT);
        baseUpwardLaunchSpeed = Mathf.Max(0f, baseUpwardLaunchSpeed);
        upwardLaunchBonusPerChain = Mathf.Max(0f, upwardLaunchBonusPerChain);
        maxUpwardLaunchSpeed = Mathf.Max(0f, maxUpwardLaunchSpeed);
        launchAdhesionLockDuration = Mathf.Max(0f, launchAdhesionLockDuration);
        emotionReductionPerLandmine = Mathf.Max(0f, emotionReductionPerLandmine);
    }

    private void OnDisable()
    {
        UnsubscribeFromChunkDestroyed();

        foreach (PlayerController player in new List<PlayerController>(chainStates.Keys))
        {
            UnregisterPlayer(player);
        }
    }

    public bool PlaceOnChunk(TerrainChunk chunk)
    {
        if (chunk == null)
        {
            return false;
        }

        CreateLandmineForChunk(chunk, 0);
        return true;
    }

    public bool TryPickRegulationSequence(out TerrainSequenceDefinition sequence)
    {
        sequence = null;
        if (regulationTerrainSequences == null || regulationTerrainSequences.Length == 0)
        {
            return false;
        }

        int validCount = 0;
        for (int i = 0; i < regulationTerrainSequences.Length; i++)
        {
            TerrainSequenceDefinition candidate = regulationTerrainSequences[i];
            if (candidate != null && candidate.HasChunks)
            {
                validCount++;
            }
        }

        if (validCount <= 0)
        {
            return false;
        }

        int target = Random.Range(0, validCount);
        for (int i = 0; i < regulationTerrainSequences.Length; i++)
        {
            TerrainSequenceDefinition candidate = regulationTerrainSequences[i];
            if (candidate == null || !candidate.HasChunks)
            {
                continue;
            }

            if (target == 0)
            {
                sequence = candidate;
                return true;
            }

            target--;
        }

        return false;
    }

    public void CreateLandmineForChunk(TerrainChunk chunk, int sequenceIndex)
    {
        if (chunk == null ||
            sequenceIndex % landmineIntervalChunks != 0 ||
            landminesByChunk.ContainsKey(chunk))
        {
            return;
        }

        if (landminePrefab == null)
        {
            Debug.LogWarning($"{nameof(SadnessRegulation)} cannot create a landmine because no landmine prefab is assigned.", this);
            return;
        }

        SubscribeToChunkDestroyed();

        if (!chunk.EvaluateSurface(surfaceT, out Vector3 surfacePosition, out Vector3 surfaceNormal))
        {
            return;
        }

        Quaternion rotation = Quaternion.Euler(
            0f,
            0f,
            Mathf.Atan2(surfaceNormal.x, surfaceNormal.y) * Mathf.Rad2Deg * -1f);

        SadnessRegulationLandmine landmine = Instantiate(
            landminePrefab,
            surfacePosition,
            rotation,
            chunk.transform);
        landmine.name = $"Golden Landmine {sequenceIndex + 1}";
        SnapBottomRootToSurface(landmine.gameObject, surfacePosition);
        landmine.Initialize(this, sequenceIndex);
        landminesByChunk[chunk] = landmine;
    }

    public void MarkSequenceComplete()
    {
        sequenceComplete = true;
        DestroyIfFinished();
    }

    public void TriggerLandmine(SadnessRegulationLandmine landmine, PlayerController player, Vector2 impactPoint)
    {
        if (landmine == null || player == null)
        {
            return;
        }

        ChainState state = RegisterPlayer(player);
        int nextChainCount = state.waitingForNextLandmine ? state.chainCount + 1 : 1;

        state.chainCount = nextChainCount;
        state.waitingForNextLandmine = true;

        if (EmotionMeter.Instance != null)
        {
            EmotionMeter.Instance.AddEmotion(-emotionReductionPerLandmine);
        }

        float upwardLaunchSpeed = baseUpwardLaunchSpeed + upwardLaunchBonusPerChain * (nextChainCount - 1);
        if (maxUpwardLaunchSpeed > 0f)
        {
            upwardLaunchSpeed = Mathf.Min(upwardLaunchSpeed, maxUpwardLaunchSpeed);
        }

        player.LaunchVertically(upwardLaunchSpeed, launchAdhesionLockDuration);
    }

    private void OnChunkDestroyed(TerrainChunk chunk)
    {
        if (chunk == null || !landminesByChunk.ContainsKey(chunk))
        {
            return;
        }

        landminesByChunk.Remove(chunk);
        DestroyIfFinished();
    }

    private void SubscribeToChunkDestroyed()
    {
        if (subscribedToChunkDestroyed || TerrainManager.Instance == null)
        {
            return;
        }

        TerrainManager.Instance.ChunkDestroyed += OnChunkDestroyed;
        subscribedToChunkDestroyed = true;
    }

    private void UnsubscribeFromChunkDestroyed()
    {
        if (!subscribedToChunkDestroyed || TerrainManager.Instance == null)
        {
            subscribedToChunkDestroyed = false;
            return;
        }

        TerrainManager.Instance.ChunkDestroyed -= OnChunkDestroyed;
        subscribedToChunkDestroyed = false;
    }

    private void DestroyIfFinished()
    {
        if (!sequenceComplete || landminesByChunk.Count > 0)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(gameObject);
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

    private ChainState RegisterPlayer(PlayerController player)
    {
        if (!chainStates.TryGetValue(player, out ChainState state))
        {
            state = new ChainState();
            chainStates[player] = state;
            player.Landed += OnPlayerLanded;
        }

        return state;
    }

    private void UnregisterPlayer(PlayerController player)
    {
        if (player == null || !chainStates.Remove(player))
        {
            return;
        }

        player.Landed -= OnPlayerLanded;
    }

    private void OnPlayerLanded(PlayerController player)
    {
        if (player == null || !chainStates.TryGetValue(player, out ChainState state))
        {
            return;
        }

        if (!state.waitingForNextLandmine)
        {
            return;
        }

        state.chainCount = 0;
        state.waitingForNextLandmine = false;
        UnregisterPlayer(player);
    }
}
