using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TimedLaneObstacleSpawner : MonoBehaviour
{
    [Serializable]
    public sealed class BiomeObstacleSet
    {
        public BiomeData biome;
        public GameObject[] obstaclePrefabs;
    }

    private sealed class PlannedSpawn
    {
        public GameObject ObstaclePrefab;
        public GameObject Indicator;
        public ProximityWarningIndicatorAnimator IndicatorAnimator;
        public int SlotIndex;
    }

    [Header("Markers")]
    [SerializeField] private Transform indicatorXMarker;
    [SerializeField] private Transform spawnXMarker;
    [SerializeField] private Transform minYMarker;
    [SerializeField] private Transform maxYMarker;

    [Header("Prefabs")]
    [SerializeField] private GameObject indicatorPrefab;
    [SerializeField] private BiomeObstacleSet[] biomeObstacleSets;

    [Header("Random Event")]
    [Min(0.01f)] [SerializeField] private float eventCheckInterval = 5f;
    [Range(0f, 1f)] [SerializeField] private float eventChance = 0.25f;

    [Header("Warning")]
    [Min(0f)] [SerializeField] private float warningDuration = 1.5f;

    [Header("Slots")]
    [Min(1)] [SerializeField] private int maxSlotCount = 6;
    [Min(0)] [SerializeField] private int minObstaclesPerWave = 1;
    [Min(0)] [SerializeField] private int maxObstaclesPerWave = 5;

    [Header("Spawning")]
    [Min(0f)] [SerializeField] private float staggerDelay = 1f;
    [Min(0f)] [SerializeField] private float spawnedObstacleLifetime = 8f;

    private readonly HashSet<BiomeData> warnedMissingBiomeSets =
        new HashSet<BiomeData>();
    private readonly List<GameObject> activeIndicators = new List<GameObject>();
    private readonly List<GameObject> activeSpawnedObstacles = new List<GameObject>();
    private readonly List<Coroutine> activePlannedSpawnRoutines =
        new List<Coroutine>();

    private Coroutine eventRoutine;
    private Coroutine waveRoutine;
    private int activePlannedSpawnRoutineCount;
    private bool warnedMissingBiomeManager;
    private bool warnedMissingCurrentBiome;
    private bool warnedMissingSetup;

    public bool IsWaveRunning => waveRoutine != null;

    private void OnEnable()
    {
        eventRoutine = StartCoroutine(RandomEventRoutine());
    }

    private void OnDisable()
    {
        StopRoutine(ref eventRoutine);
        StopRoutine(ref waveRoutine);
        StopPlannedSpawnRoutines();
        DestroyActiveIndicators();
        DestroyActiveSpawnedObstacles();
    }

    private void OnValidate()
    {
        eventCheckInterval = Mathf.Max(0.01f, eventCheckInterval);
        eventChance = Mathf.Clamp01(eventChance);
        warningDuration = Mathf.Max(0f, warningDuration);
        maxSlotCount = Mathf.Max(1, maxSlotCount);
        minObstaclesPerWave = Mathf.Max(0, minObstaclesPerWave);
        maxObstaclesPerWave = Mathf.Max(minObstaclesPerWave, maxObstaclesPerWave);
        staggerDelay = Mathf.Max(0f, staggerDelay);
        spawnedObstacleLifetime = Mathf.Max(0f, spawnedObstacleLifetime);
    }

    public void TriggerWave()
    {
        if (IsWaveRunning || !isActiveAndEnabled)
        {
            return;
        }

        waveRoutine = StartCoroutine(WaveRoutine());
    }

    public void ClearActiveRandomEventObjects()
    {
        StopRoutine(ref waveRoutine);
        StopPlannedSpawnRoutines();
        DestroyActiveIndicators();
        DestroyActiveSpawnedObstacles();
    }

    private IEnumerator RandomEventRoutine()
    {
        while (enabled)
        {
            yield return new WaitForSeconds(eventCheckInterval);

            if (!IsWaveRunning && UnityEngine.Random.value <= eventChance)
            {
                TriggerWave();
            }
        }
    }

    private IEnumerator WaveRoutine()
    {
        List<PlannedSpawn> plannedSpawns = BuildWavePlan();
        if (plannedSpawns.Count <= 0)
        {
            waveRoutine = null;
            yield break;
        }

        yield return SpawnOneByOne(plannedSpawns);

        waveRoutine = null;
    }

    private List<PlannedSpawn> BuildWavePlan()
    {
        List<PlannedSpawn> plannedSpawns = new List<PlannedSpawn>();
        if (!HasRequiredSetup())
        {
            return plannedSpawns;
        }

        GameObject obstaclePrefab = PickCurrentBiomeObstaclePrefab();
        if (obstaclePrefab == null)
        {
            return plannedSpawns;
        }

        int slotCount = Mathf.Max(1, maxSlotCount);
        int obstacleCount = RollObstacleCount(slotCount);
        if (obstacleCount <= 0)
        {
            return plannedSpawns;
        }

        List<int> selectedSlots = PickDistinctSlots(slotCount, obstacleCount);
        selectedSlots.Sort();

        for (int i = 0; i < selectedSlots.Count; i++)
        {
            plannedSpawns.Add(new PlannedSpawn
            {
                ObstaclePrefab = obstaclePrefab,
                SlotIndex = selectedSlots[i]
            });
        }

        return plannedSpawns;
    }

    private bool HasRequiredSetup()
    {
        bool hasRequiredSetup =
            indicatorXMarker != null &&
            spawnXMarker != null &&
            minYMarker != null &&
            maxYMarker != null &&
            indicatorPrefab != null;

        if (!hasRequiredSetup && !warnedMissingSetup)
        {
            Debug.LogWarning(
                $"[{nameof(TimedLaneObstacleSpawner)}] Assign indicator/spawn/min/max markers and an indicator prefab.",
                this);
            warnedMissingSetup = true;
        }

        return hasRequiredSetup;
    }

    private GameObject PickCurrentBiomeObstaclePrefab()
    {
        BiomeData currentBiome = GetCurrentBiome();
        if (currentBiome == null)
        {
            return null;
        }

        BiomeObstacleSet obstacleSet = FindObstacleSet(currentBiome);
        if (obstacleSet == null || obstacleSet.obstaclePrefabs == null)
        {
            WarnMissingBiomeSet(currentBiome);
            return null;
        }

        int validPrefabCount = CountValidPrefabs(obstacleSet.obstaclePrefabs);
        if (validPrefabCount <= 0)
        {
            WarnMissingBiomeSet(currentBiome);
            return null;
        }

        int chosenValidIndex = UnityEngine.Random.Range(0, validPrefabCount);
        int validIndex = 0;
        for (int i = 0; i < obstacleSet.obstaclePrefabs.Length; i++)
        {
            GameObject prefab = obstacleSet.obstaclePrefabs[i];
            if (prefab == null)
            {
                continue;
            }

            if (validIndex == chosenValidIndex)
            {
                return prefab;
            }

            validIndex++;
        }

        return null;
    }

    private BiomeData GetCurrentBiome()
    {
        if (BiomeManager.Instance == null)
        {
            if (!warnedMissingBiomeManager)
            {
                Debug.LogWarning(
                    $"[{nameof(TimedLaneObstacleSpawner)}] No BiomeManager instance found.",
                    this);
                warnedMissingBiomeManager = true;
            }

            return null;
        }

        BiomeData currentBiome = BiomeManager.Instance.CurrentBiome;
        if (currentBiome == null && !warnedMissingCurrentBiome)
        {
            Debug.LogWarning(
                $"[{nameof(TimedLaneObstacleSpawner)}] BiomeManager has no current biome.",
                this);
            warnedMissingCurrentBiome = true;
        }

        return currentBiome;
    }

    private BiomeObstacleSet FindObstacleSet(BiomeData biome)
    {
        if (biome == null || biomeObstacleSets == null)
        {
            return null;
        }

        for (int i = 0; i < biomeObstacleSets.Length; i++)
        {
            BiomeObstacleSet obstacleSet = biomeObstacleSets[i];
            if (obstacleSet != null && obstacleSet.biome == biome)
            {
                return obstacleSet;
            }
        }

        return null;
    }

    private static int CountValidPrefabs(GameObject[] prefabs)
    {
        if (prefabs == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < prefabs.Length; i++)
        {
            if (prefabs[i] != null)
            {
                count++;
            }
        }

        return count;
    }

    private void WarnMissingBiomeSet(BiomeData biome)
    {
        if (biome == null || warnedMissingBiomeSets.Contains(biome))
        {
            return;
        }

        warnedMissingBiomeSets.Add(biome);
        Debug.LogWarning(
            $"[{nameof(TimedLaneObstacleSpawner)}] No obstacle prefabs assigned for biome '{biome.biomeName}'.",
            this);
    }

    private int RollObstacleCount(int slotCount)
    {
        int safeSlotCount = Mathf.Max(1, slotCount);
        int minCount = Mathf.Clamp(minObstaclesPerWave, 0, safeSlotCount);
        int maxCount = Mathf.Clamp(maxObstaclesPerWave, minCount, safeSlotCount);
        return UnityEngine.Random.Range(minCount, maxCount + 1);
    }

    private static List<int> PickDistinctSlots(int slotCount, int count)
    {
        List<int> slots = new List<int>();
        for (int i = 0; i < slotCount; i++)
        {
            slots.Add(i);
        }

        for (int i = 0; i < slots.Count; i++)
        {
            int swapIndex = UnityEngine.Random.Range(i, slots.Count);
            int current = slots[i];
            slots[i] = slots[swapIndex];
            slots[swapIndex] = current;
        }

        int removeCount = Mathf.Max(0, slots.Count - count);
        if (removeCount > 0)
        {
            slots.RemoveRange(count, removeCount);
        }

        return slots;
    }

    private float GetSlotCenterY(int slotIndex)
    {
        int slotCount = Mathf.Max(1, maxSlotCount);
        int safeSlotIndex = Mathf.Clamp(slotIndex, 0, slotCount - 1);
        float minY = Mathf.Min(minYMarker.position.y, maxYMarker.position.y);
        float maxY = Mathf.Max(minYMarker.position.y, maxYMarker.position.y);
        float slotHeight = (maxY - minY) / slotCount;
        float slotCenterY = minY + slotHeight * (safeSlotIndex + 0.5f);
        return Mathf.Clamp(slotCenterY, minY, maxY);
    }

    private GameObject SpawnIndicator(PlannedSpawn plannedSpawn)
    {
        if (plannedSpawn == null)
        {
            return null;
        }

        Vector3 markerPosition = indicatorXMarker.position;
        float slotY = GetSlotCenterY(plannedSpawn.SlotIndex);
        Vector3 indicatorPosition =
            new Vector3(markerPosition.x, slotY, markerPosition.z);
        GameObject indicator = Instantiate(
            indicatorPrefab,
            indicatorPosition,
            indicatorPrefab.transform.rotation,
            indicatorXMarker);
        activeIndicators.Add(indicator);
        plannedSpawn.Indicator = indicator;
        plannedSpawn.IndicatorAnimator = ResolveIndicatorAnimator(indicator);
        return indicator;
    }

    private static ProximityWarningIndicatorAnimator ResolveIndicatorAnimator(
        GameObject indicator)
    {
        if (indicator == null)
        {
            return null;
        }

        ProximityWarningIndicatorAnimator animator =
            indicator.GetComponent<ProximityWarningIndicatorAnimator>();
        if (animator != null)
        {
            return animator;
        }

        return indicator.GetComponentInChildren<ProximityWarningIndicatorAnimator>();
    }

    private IEnumerator RunWarningTimer(PlannedSpawn plannedSpawn)
    {
        if (warningDuration <= 0f)
        {
            SetIndicatorProgress(plannedSpawn, 1f);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < warningDuration)
        {
            elapsed += Time.deltaTime;
            SetIndicatorProgress(plannedSpawn, elapsed / warningDuration);
            yield return null;
        }

        SetIndicatorProgress(plannedSpawn, 1f);
    }

    private static void SetIndicatorProgress(PlannedSpawn plannedSpawn, float progress)
    {
        if (plannedSpawn == null || plannedSpawn.IndicatorAnimator == null)
        {
            return;
        }

        plannedSpawn.IndicatorAnimator.SetThreatProgress(progress);
    }

    private IEnumerator SpawnOneByOne(List<PlannedSpawn> plannedSpawns)
    {
        for (int i = 0; i < plannedSpawns.Count; i++)
        {
            PlannedSpawn plannedSpawn = plannedSpawns[i];
            activePlannedSpawnRoutineCount++;
            Coroutine plannedSpawnRoutine =
                StartCoroutine(RunPlannedSpawn(plannedSpawn));
            activePlannedSpawnRoutines.Add(plannedSpawnRoutine);

            if (i < plannedSpawns.Count - 1 && staggerDelay > 0f)
            {
                yield return new WaitForSeconds(staggerDelay);
            }
        }

        while (activePlannedSpawnRoutineCount > 0)
        {
            yield return null;
        }

        activePlannedSpawnRoutines.Clear();
    }

    private IEnumerator RunPlannedSpawn(PlannedSpawn plannedSpawn)
    {
        SpawnIndicator(plannedSpawn);
        yield return RunWarningTimer(plannedSpawn);
        SpawnObstacle(plannedSpawn);
        PlayIndicatorFinalWarning(plannedSpawn);
        activePlannedSpawnRoutineCount =
            Mathf.Max(0, activePlannedSpawnRoutineCount - 1);
    }

    private void SpawnObstacle(PlannedSpawn plannedSpawn)
    {
        if (plannedSpawn == null || plannedSpawn.ObstaclePrefab == null)
        {
            return;
        }

        Vector3 markerPosition = spawnXMarker.position;
        float spawnY = GetSlotCenterY(plannedSpawn.SlotIndex);
        Vector3 spawnPosition =
            new Vector3(markerPosition.x, spawnY, markerPosition.z);
        GameObject obstacle = Instantiate(
            plannedSpawn.ObstaclePrefab,
            spawnPosition,
            plannedSpawn.ObstaclePrefab.transform.rotation);
        activeSpawnedObstacles.Add(obstacle);
        if (spawnedObstacleLifetime > 0f)
        {
            Destroy(obstacle, spawnedObstacleLifetime);
        }
    }

    private void PlayIndicatorFinalWarning(PlannedSpawn plannedSpawn)
    {
        if (plannedSpawn == null || plannedSpawn.Indicator == null)
        {
            return;
        }

        activeIndicators.Remove(plannedSpawn.Indicator);
        if (plannedSpawn.IndicatorAnimator != null)
        {
            plannedSpawn.IndicatorAnimator.PlayFinalWarningAndDestroy();
        }
        else
        {
            Destroy(plannedSpawn.Indicator);
        }

        plannedSpawn.Indicator = null;
        plannedSpawn.IndicatorAnimator = null;
    }

    private void DestroyActiveIndicators()
    {
        for (int i = activeIndicators.Count - 1; i >= 0; i--)
        {
            GameObject indicator = activeIndicators[i];
            if (indicator != null)
            {
                Destroy(indicator);
            }
        }

        activeIndicators.Clear();
    }

    private void DestroyActiveSpawnedObstacles()
    {
        for (int i = activeSpawnedObstacles.Count - 1; i >= 0; i--)
        {
            GameObject obstacle = activeSpawnedObstacles[i];
            if (obstacle != null)
            {
                Destroy(obstacle);
            }
        }

        activeSpawnedObstacles.Clear();
    }

    private void StopRoutine(ref Coroutine routine)
    {
        if (routine == null)
        {
            return;
        }

        StopCoroutine(routine);
        routine = null;
    }

    private void StopPlannedSpawnRoutines()
    {
        for (int i = activePlannedSpawnRoutines.Count - 1; i >= 0; i--)
        {
            Coroutine routine = activePlannedSpawnRoutines[i];
            if (routine != null)
            {
                StopCoroutine(routine);
            }
        }

        activePlannedSpawnRoutines.Clear();
        activePlannedSpawnRoutineCount = 0;
    }
}
