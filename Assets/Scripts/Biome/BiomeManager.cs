using System;
using UnityEngine;
using UnityEngine.Events;

public enum BiomePhase
{
    Normal,
    Pre,
    Mid,
    Peaked
}

public class BiomeManager : MonoBehaviour
{
    public static BiomeManager Instance { get; private set; }

    [Header("Biome Pools")]
    [Tooltip("Biomes available while EmotionMeter is in Normal state.")]
    public BiomeData[] normalBiomes;
    [Tooltip("Biomes available while EmotionMeter is in Pre state.")]
    public BiomeData[] preBiomes;
    [Tooltip("Biomes available while EmotionMeter is in Mid state.")]
    public BiomeData[] midBiomes;
    [Tooltip("Biomes available while EmotionMeter is in Peaked state.")]
    public BiomeData[] peakedBiomes;

    [Header("Fallback")]
    [Tooltip("Used when the current emotion phase has no assigned biome.")]
    public BiomeData fallbackBiome;

    [Header("Events")]
    public UnityEvent<BiomeData> onBiomeChanged;
    public UnityEvent<int> onBiomePhaseChanged;
    [Tooltip("Fired the moment an inter-family transition is queued (i.e. a transition flash should begin). " +
             "CurrentBiome has NOT changed yet at this point - that only happens once " +
             "CommitPendingTransition() runs during the hidden reset).")]
    public UnityEvent onInterFamilyTransitionRequested;

    private int currentBiomeIndex = -1;
    private bool initialized;
    private string activeFamilyId;
    private string lastCompletedEmotionFamilyId;
    private bool hasPendingTransition;
    private BiomePhase pendingPhase = BiomePhase.Normal;

    public BiomeData CurrentBiome { get; private set; }
    public BiomePhase CurrentPhase { get; private set; } = BiomePhase.Normal;
    public int CurrentPhaseIndex => (int)CurrentPhase;

    /// <summary>
    /// The family ID locked in for the current emotion spike, or null if no family
    /// is currently locked (e.g. while in Normal, or mid-spike on a family-less biome).
    /// </summary>
    public string ActiveFamilyId => activeFamilyId;

    /// <summary>True while a flash/reset transition has been queued but not yet committed.</summary>
    public bool HasPendingTransition => hasPendingTransition;

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
    }

    void Start()
    {
        EnsureInitialized();

        if (EmotionMeter.Instance != null)
        {
            EmotionMeter.Instance.onStateChanged.AddListener(OnEmotionStateChanged);
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (EmotionMeter.Instance != null)
        {
            EmotionMeter.Instance.onStateChanged.RemoveListener(OnEmotionStateChanged);
        }
    }

    public void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        CurrentPhase = GetPhaseFromEmotion();
        onBiomePhaseChanged?.Invoke(CurrentPhaseIndex);
        AdvanceBiome();
    }

    /// <summary>
    /// Called per spawned chunk. Biome changes are now driven entirely by EmotionMeter
    /// phase transitions (see OnEmotionStateChanged), so under normal operation this is
    /// just a safety net that recovers CurrentBiome if it was ever left null.
    /// </summary>
    public void ConsumeChunkAndAdvanceIfNeeded()
    {
        EnsureInitialized();

        if (CurrentBiome == null)
        {
            AdvanceBiome();
        }
    }

    /// <summary>
    /// Called by TerrainManager during the hidden reset. Performs the deferred
    /// phase/biome swap that OnEmotionStateChanged queued up earlier - this is
    /// the only place CurrentBiome changes for an inter-family transition.
    /// </summary>
    public void CommitPendingTransition()
    {
        if (!hasPendingTransition)
        {
            return;
        }

        hasPendingTransition = false;
        BiomePhase committedPhase = pendingPhase;

        if (committedPhase == BiomePhase.Normal)
        {
            lastCompletedEmotionFamilyId = GetCurrentEmotionFamilyId();
            activeFamilyId = null;
        }

        CurrentPhase = committedPhase;
        currentBiomeIndex = -1;
        onBiomePhaseChanged?.Invoke(CurrentPhaseIndex);
        AdvanceBiome();
    }

    private void OnEmotionStateChanged(int newStateIndex)
    {
        BiomePhase nextPhase = StateIndexToPhase(newStateIndex);

        if (hasPendingTransition)
        {
            // A transition flash/reset is already queued. Keep the original
            // pendingPhase even if the meter moves again before the reset commits.
            return;
        }

        if (nextPhase == CurrentPhase)
        {
            return;
        }

        if (ShouldQueueBoundaryTransition(nextPhase))
        {
            // Don't touch CurrentPhase/CurrentBiome yet - terrain generation keeps
            // using whatever is already active until CommitPendingTransition() runs.
            QueueBoundaryTransition(nextPhase);
            return;
        }

        CurrentPhase = nextPhase;
        currentBiomeIndex = -1;
        onBiomePhaseChanged?.Invoke(CurrentPhaseIndex);
        AdvanceBiome(false);
    }

    public void QueueTransitionFromCurrentEmotionIfNeeded()
    {
        if (hasPendingTransition || CurrentPhase != BiomePhase.Normal)
        {
            return;
        }

        BiomePhase emotionPhase = GetPhaseFromEmotion();
        if (emotionPhase == BiomePhase.Normal)
        {
            return;
        }

        QueueBoundaryTransition(emotionPhase);
    }

    private void QueueBoundaryTransition(BiomePhase nextPhase)
    {
        pendingPhase = nextPhase;
        hasPendingTransition = true;

        string currentBiomeName = CurrentBiome != null ? CurrentBiome.biomeName : "None";
        Debug.Log($"[BiomeManager] {CurrentPhase} -> {nextPhase} queued behind transition flash; committed biome remains {currentBiomeName}.");

        onInterFamilyTransitionRequested?.Invoke();

        ScreenFader fader = ScreenFader.Instance;
        if (fader != null)
        {
            fader.BeginBiomeTransition();
        }
        else
        {
            Debug.LogWarning("[BiomeManager] No ScreenFader found; the queued biome transition cannot run.");
        }
    }

    private void AdvanceBiome(bool allowFallback = true)
    {
        BiomeData[] pool = GetEffectivePool();
        BiomeData chosen = PickBiome(pool);
        if (chosen == null && allowFallback)
        {
            chosen = fallbackBiome;
        }

        CurrentBiome = chosen;

        // Lock in the family the first time we pick a tagged biome during a spike.
        // Once locked, it stays locked (even through Peaked -> Mid -> Pre dips) until
        // a committed return to Normal clears it.
        if (CurrentPhase != BiomePhase.Normal && activeFamilyId == null &&
            CurrentBiome != null && !string.IsNullOrWhiteSpace(CurrentBiome.familyId))
        {
            activeFamilyId = CurrentBiome.familyId.Trim();
        }

        if (CurrentBiome != null)
        {
            onBiomeChanged?.Invoke(CurrentBiome);
            string familySuffix = activeFamilyId != null ? $" [family: {activeFamilyId}]" : "";
            Debug.Log($"[BiomeManager] {CurrentPhase} biome -> {CurrentBiome.biomeName}{familySuffix}");
        }
        else
        {
            string familySuffix = activeFamilyId != null ? $" matching family '{activeFamilyId}'" : "";
            Debug.LogWarning($"[BiomeManager] No biome assigned for {CurrentPhase}{familySuffix}.");
        }
    }

    private BiomeData PickBiome(BiomeData[] pool)
    {
        if (pool == null || pool.Length == 0)
        {
            currentBiomeIndex = -1;
            return null;
        }

        if (pool.Length == 1)
        {
            currentBiomeIndex = 0;
            return pool[0];
        }

        float total = 0f;
        for (int i = 0; i < pool.Length; i++)
        {
            if (i == currentBiomeIndex)
            {
                continue;
            }

            if (pool[i] != null)
            {
                total += Mathf.Max(0f, pool[i].selectionWeight);
            }
        }

        if (total <= 0f)
        {
            currentBiomeIndex = NextValidIndex(pool);
            return currentBiomeIndex >= 0 ? pool[currentBiomeIndex] : null;
        }

        float roll = UnityEngine.Random.Range(0f, total);
        for (int i = 0; i < pool.Length; i++)
        {
            if (i == currentBiomeIndex)
            {
                continue;
            }

            if (pool[i] == null)
            {
                continue;
            }

            roll -= Mathf.Max(0f, pool[i].selectionWeight);
            if (roll <= 0f)
            {
                currentBiomeIndex = i;
                return pool[i];
            }
        }

        currentBiomeIndex = NextValidIndex(pool);
        return currentBiomeIndex >= 0 ? pool[currentBiomeIndex] : null;
    }

    private int NextValidIndex(BiomeData[] pool)
    {
        for (int offset = 1; offset <= pool.Length; offset++)
        {
            int index = currentBiomeIndex < 0 ? offset - 1 : (currentBiomeIndex + offset) % pool.Length;
            if (index == currentBiomeIndex)
            {
                continue;
            }

            if (pool[index] != null)
            {
                return index;
            }
        }

        return -1;
    }

    private BiomeData[] GetCurrentPool()
    {
        return GetPoolForPhase(CurrentPhase);
    }

    private BiomeData[] GetPoolForPhase(BiomePhase phase)
    {
        return phase switch
        {
            BiomePhase.Pre => preBiomes,
            BiomePhase.Mid => midBiomes,
            BiomePhase.Peaked => peakedBiomes,
            _ => normalBiomes,
        };
    }

    /// <summary>
    /// Same as GetCurrentPool, but once a family is locked in, restricts the pool
    /// to only the biomes belonging to that family. Normal always ignores family
    /// locking, since lineage only applies during an emotion spike.
    /// </summary>
    private BiomeData[] GetEffectivePool()
    {
        BiomeData[] basePool = GetCurrentPool();

        if (CurrentPhase == BiomePhase.Normal)
        {
            return basePool;
        }

        if (activeFamilyId != null)
        {
            return FilterByFamily(basePool, activeFamilyId);
        }

        if (lastCompletedEmotionFamilyId == null)
        {
            return basePool;
        }

        BiomeData[] filteredPool = ExcludeFamily(basePool, lastCompletedEmotionFamilyId);
        if (filteredPool.Length > 0)
        {
            return filteredPool;
        }

        Debug.LogWarning($"[BiomeManager] No {CurrentPhase} biome outside family '{lastCompletedEmotionFamilyId}' was available; allowing repeat family.");
        return basePool;
    }

    private bool ShouldQueueBoundaryTransition(BiomePhase nextPhase)
    {
        if (nextPhase == BiomePhase.Normal)
        {
            return true;
        }

        if (CurrentPhase == BiomePhase.Normal)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(activeFamilyId))
        {
            Debug.LogWarning($"[BiomeManager] {CurrentPhase} -> {nextPhase} has no locked family; queueing transition flash instead of treating it as same-family.");
            return true;
        }

        return false;
    }

    private static BiomeData[] FilterByFamily(BiomeData[] pool, string familyId)
    {
        if (pool == null || pool.Length == 0)
        {
            return pool;
        }

        int count = 0;
        for (int i = 0; i < pool.Length; i++)
        {
            if (pool[i] != null && IsFamilyMatch(pool[i].familyId, familyId))
            {
                count++;
            }
        }

        if (count == 0)
        {
            return Array.Empty<BiomeData>();
        }

        BiomeData[] filtered = new BiomeData[count];
        int writeIndex = 0;
        for (int i = 0; i < pool.Length; i++)
        {
            if (pool[i] != null && IsFamilyMatch(pool[i].familyId, familyId))
            {
                filtered[writeIndex++] = pool[i];
            }
        }

        return filtered;
    }

    private static BiomeData[] ExcludeFamily(BiomeData[] pool, string familyId)
    {
        if (pool == null || pool.Length == 0)
        {
            return Array.Empty<BiomeData>();
        }

        int count = 0;
        for (int i = 0; i < pool.Length; i++)
        {
            if (pool[i] != null && !IsFamilyMatch(pool[i].familyId, familyId))
            {
                count++;
            }
        }

        if (count == 0)
        {
            return Array.Empty<BiomeData>();
        }

        BiomeData[] filtered = new BiomeData[count];
        int writeIndex = 0;
        for (int i = 0; i < pool.Length; i++)
        {
            if (pool[i] != null && !IsFamilyMatch(pool[i].familyId, familyId))
            {
                filtered[writeIndex++] = pool[i];
            }
        }

        return filtered;
    }

    private string GetCurrentEmotionFamilyId()
    {
        string normalizedActiveFamilyId = NormalizeFamilyId(activeFamilyId);
        if (!string.IsNullOrEmpty(normalizedActiveFamilyId))
        {
            return normalizedActiveFamilyId;
        }

        return CurrentBiome != null ? NormalizeFamilyId(CurrentBiome.familyId) : null;
    }

    private static bool IsFamilyMatch(string candidateFamilyId, string familyId)
    {
        string normalizedCandidate = NormalizeFamilyId(candidateFamilyId);
        string normalizedFamily = NormalizeFamilyId(familyId);
        return !string.IsNullOrEmpty(normalizedCandidate) &&
               string.Equals(normalizedCandidate, normalizedFamily, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeFamilyId(string familyId)
    {
        return string.IsNullOrWhiteSpace(familyId) ? null : familyId.Trim();
    }

    private BiomePhase GetPhaseFromEmotion()
    {
        return EmotionMeter.Instance != null ? StateIndexToPhase(EmotionMeter.Instance.StateIndex) : BiomePhase.Normal;
    }

    private static BiomePhase StateIndexToPhase(int stateIndex)
    {
        return stateIndex switch
        {
            1 => BiomePhase.Pre,
            2 => BiomePhase.Mid,
            3 => BiomePhase.Peaked,
            _ => BiomePhase.Normal,
        };
    }
}
