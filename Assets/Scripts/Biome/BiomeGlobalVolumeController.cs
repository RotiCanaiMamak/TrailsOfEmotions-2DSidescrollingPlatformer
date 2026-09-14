using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Applies each active biome family's global volume and controls its vignette colour and intensity.
/// Same-family biome changes lerp vignette settings; inter-family changes apply instantly.
/// </summary>
public class BiomeGlobalVolumeController : MonoBehaviour
{
    [System.Serializable]
    public class BiomeVolumeMapping
    {
        public BiomeData biome = null;

        [Range(0f, 1f)] public float vignetteIntensity = 0f;

        public Color vignetteColor = Color.black;
    }

    private struct VignetteTarget
    {
        public readonly Vignette Vignette;
        public readonly Color StartColor;
        public readonly float StartIntensity;
        public readonly Color TargetColor;
        public readonly float TargetIntensity;

        public VignetteTarget(Vignette vignette, Color targetColor, float targetIntensity, float activeIntensityBoost)
        {
            Vignette = vignette;
            StartColor = vignette != null ? vignette.color.value : Color.black;
            StartIntensity = vignette != null
                ? Mathf.Clamp01(vignette.intensity.value - Mathf.Max(0f, activeIntensityBoost))
                : 0f;
            TargetColor = targetColor;
            TargetIntensity = Mathf.Clamp01(targetIntensity);
        }
    }

    [Header("Global Volume")]
    [SerializeField] private Volume globalVolume;

    [Header("Biome Volume Mappings")]
    [SerializeField] private List<BiomeVolumeMapping> biomeVolumeMappings = new List<BiomeVolumeMapping>();

    private BiomeManager biomeManager;
    private EmotionMeter emotionMeter;
    private readonly Dictionary<UnityEngine.Object, float> vignetteIntensityBoosts =
        new Dictionary<UnityEngine.Object, float>();
    private readonly List<UnityEngine.Object> vignetteIntensityBoostKeys =
        new List<UnityEngine.Object>();
    private bool startupApplied;
    private BiomeData activeBiome;
    private VignetteTarget activeEmotionTarget;
    private bool hasActiveEmotionTarget;

    private void Start()
    {
        if (globalVolume == null)
        {
            globalVolume = FindFirstObjectByType<Volume>();
        }

        if (globalVolume == null)
        {
            Debug.LogWarning("[BiomeGlobalVolumeController] No shared Global Volume assigned or found in the scene.", this);
            return;
        }

        WarnInvalidMappings();

        biomeManager = BiomeManager.Instance;
        if (biomeManager == null)
        {
            Debug.LogWarning("[BiomeGlobalVolumeController] No BiomeManager instance found.", this);
            return;
        }

        biomeManager.EnsureInitialized();
        SubscribeToEmotionMeter();
        globalVolume.weight = 0f;
        ApplyBiomeVolume(biomeManager.CurrentBiome, true);
        activeBiome = biomeManager.CurrentBiome;
        startupApplied = true;
        biomeManager.onBiomeChanged.AddListener(OnBiomeChanged);
    }

    private void OnDestroy()
    {
        if (biomeManager != null)
        {
            biomeManager.onBiomeChanged.RemoveListener(OnBiomeChanged);
        }

        if (emotionMeter != null)
        {
            emotionMeter.onValueChanged.RemoveListener(OnEmotionValueChanged);
            emotionMeter = null;
        }
    }

    private void OnValidate()
    {
        ValidateMappings();
    }

    private void OnBiomeChanged(BiomeData biome)
    {
        bool sameFamilyTransition = startupApplied && IsSameFamily(activeBiome, biome);
        ApplyBiomeVolume(biome, !sameFamilyTransition);
        activeBiome = biome;
        startupApplied = true;
    }

    private void ApplyBiomeVolume(BiomeData biome, bool instant)
    {
        if (biome == null)
        {
            hasActiveEmotionTarget = false;
            return;
        }

        BiomeVolumeMapping nextMapping = GetMappingForBiome(biome);
        if (!IsUsableMapping(nextMapping, biome))
        {
            hasActiveEmotionTarget = false;
            return;
        }

        if (instant)
        {
            hasActiveEmotionTarget = false;
            ApplyInstant(nextMapping);
            return;
        }

        if (!TryGetVignette(nextMapping, out Vignette vignette))
        {
            hasActiveEmotionTarget = false;
            return;
        }

        globalVolume.weight = 1f;
        activeEmotionTarget = new VignetteTarget(
            vignette,
            nextMapping.vignetteColor,
            nextMapping.vignetteIntensity,
            GetVignetteIntensityBoost());
        hasActiveEmotionTarget = true;
        ApplyTarget(activeEmotionTarget, GetEmotionPhaseProgress());
    }

    private void ApplyInstant(BiomeVolumeMapping activeMapping)
    {
        if (!TryGetVignette(activeMapping, out Vignette vignette))
        {
            return;
        }

        globalVolume.weight = 1f;
        vignette.color.value = activeMapping.vignetteColor;
        vignette.intensity.value = GetBoostedVignetteIntensity(activeMapping.vignetteIntensity);
    }

    private void OnEmotionValueChanged(float value)
    {
        if (!hasActiveEmotionTarget)
        {
            return;
        }

        ApplyTarget(activeEmotionTarget, GetEmotionPhaseProgress());
    }

    public void SetVignetteIntensityBoost(UnityEngine.Object source, float intensityBoost)
    {
        if (ReferenceEquals(source, null))
        {
            return;
        }

        float boost = Mathf.Max(0f, intensityBoost);
        if (boost <= 0f)
        {
            RemoveVignetteIntensityBoost(source);
            return;
        }

        vignetteIntensityBoosts[source] = boost;
        ReapplyCurrentVignette();
    }

    public void RemoveVignetteIntensityBoost(UnityEngine.Object source)
    {
        if (ReferenceEquals(source, null))
        {
            return;
        }

        vignetteIntensityBoosts.Remove(source);
        ReapplyCurrentVignette();
    }

    private void ApplyTarget(VignetteTarget target, float progress)
    {
        if (target.Vignette == null)
        {
            return;
        }

        progress = Mathf.Clamp01(progress);
        target.Vignette.color.value = Color.Lerp(target.StartColor, target.TargetColor, progress);
        target.Vignette.intensity.value = GetBoostedVignetteIntensity(
            Mathf.Lerp(target.StartIntensity, target.TargetIntensity, progress));
    }

    private void ReapplyCurrentVignette()
    {
        if (activeBiome == null)
        {
            return;
        }

        BiomeVolumeMapping activeMapping = GetMappingForBiome(activeBiome);
        if (activeMapping == null || !TryGetVignette(activeMapping, out Vignette vignette))
        {
            return;
        }

        if (hasActiveEmotionTarget)
        {
            ApplyTarget(activeEmotionTarget, GetEmotionPhaseProgress());
            return;
        }

        vignette.intensity.value = GetBoostedVignetteIntensity(activeMapping.vignetteIntensity);
    }

    private float GetBoostedVignetteIntensity(float baseIntensity)
    {
        return Mathf.Clamp01(Mathf.Clamp01(baseIntensity) + GetVignetteIntensityBoost());
    }

    private float GetVignetteIntensityBoost()
    {
        if (vignetteIntensityBoosts.Count == 0)
        {
            return 0f;
        }

        PruneVignetteIntensityBoosts();
        float boost = 0f;
        foreach (float value in vignetteIntensityBoosts.Values)
        {
            boost += Mathf.Max(0f, value);
        }

        return Mathf.Clamp01(boost);
    }

    private void PruneVignetteIntensityBoosts()
    {
        vignetteIntensityBoostKeys.Clear();
        foreach (KeyValuePair<UnityEngine.Object, float> entry in vignetteIntensityBoosts)
        {
            if (entry.Key == null || entry.Value <= 0f)
            {
                vignetteIntensityBoostKeys.Add(entry.Key);
            }
        }

        for (int i = 0; i < vignetteIntensityBoostKeys.Count; i++)
        {
            vignetteIntensityBoosts.Remove(vignetteIntensityBoostKeys[i]);
        }

        vignetteIntensityBoostKeys.Clear();
    }

    private BiomeVolumeMapping GetMappingForBiome(BiomeData biome)
    {
        if (biomeVolumeMappings == null || biome == null)
        {
            return null;
        }

        BiomeVolumeMapping match = null;
        for (int i = 0; i < biomeVolumeMappings.Count; i++)
        {
            BiomeVolumeMapping mapping = biomeVolumeMappings[i];
            if (mapping == null || mapping.biome != biome)
            {
                continue;
            }

            if (match == null)
            {
                match = mapping;
            }
            else
            {
                Debug.LogWarning($"[BiomeGlobalVolumeController] Duplicate volume mapping for biome '{biome.biomeName}'. The first mapping will be used.", this);
                break;
            }
        }

        return match;
    }

    private bool IsUsableMapping(BiomeVolumeMapping mapping, BiomeData biome)
    {
        string biomeName = biome != null ? biome.biomeName : "None";
        if (mapping == null)
        {
            Debug.LogWarning($"[BiomeGlobalVolumeController] No volume mapping for biome '{biomeName}'.", this);
            return false;
        }

        return true;
    }

    private bool TryGetVignette(BiomeVolumeMapping mapping, out Vignette vignette)
    {
        vignette = null;
        string biomeName = mapping != null && mapping.biome != null ? mapping.biome.biomeName : "None";
        if (mapping == null)
        {
            return false;
        }

        if (globalVolume == null)
        {
            Debug.LogWarning("[BiomeGlobalVolumeController] No shared Global Volume assigned or found in the scene.", this);
            return false;
        }

        if (globalVolume.profile == null)
        {
            Debug.LogWarning("[BiomeGlobalVolumeController] The shared Global Volume has no VolumeProfile assigned.", this);
            return false;
        }

        if (!globalVolume.profile.TryGet(out vignette) || vignette == null)
        {
            Debug.LogWarning($"[BiomeGlobalVolumeController] The shared Global Volume has no Vignette override for biome '{biomeName}'.", this);
            return false;
        }

        return true;
    }

    private void WarnInvalidMappings()
    {
        if (biomeVolumeMappings == null || biomeVolumeMappings.Count == 0)
        {
            Debug.LogWarning("[BiomeGlobalVolumeController] No biome volume mappings assigned.", this);
            return;
        }

        HashSet<BiomeData> seenBiomes = new HashSet<BiomeData>();
        for (int i = 0; i < biomeVolumeMappings.Count; i++)
        {
            BiomeVolumeMapping mapping = biomeVolumeMappings[i];
            if (mapping == null)
            {
                continue;
            }

            if (mapping.biome == null)
            {
                Debug.LogWarning($"[BiomeGlobalVolumeController] Biome volume mapping {i} has no BiomeData assigned.", this);
            }
            else if (!seenBiomes.Add(mapping.biome))
            {
                Debug.LogWarning($"[BiomeGlobalVolumeController] Duplicate volume mapping for biome '{mapping.biome.biomeName}'. The first mapping will be used.", this);
            }
        }
    }

    private void ValidateMappings()
    {
        if (biomeVolumeMappings == null)
        {
            return;
        }

        for (int i = 0; i < biomeVolumeMappings.Count; i++)
        {
            BiomeVolumeMapping mapping = biomeVolumeMappings[i];
            if (mapping == null)
            {
                continue;
            }

            mapping.vignetteIntensity = Mathf.Clamp01(mapping.vignetteIntensity);
        }
    }

    private void SubscribeToEmotionMeter()
    {
        emotionMeter = EmotionMeter.Instance;
        if (emotionMeter != null)
        {
            emotionMeter.onValueChanged.AddListener(OnEmotionValueChanged);
        }
    }

    private float GetEmotionPhaseProgress()
    {
        if (emotionMeter == null)
        {
            return 1f;
        }

        float pre = emotionMeter.preThreshold;
        float mid = Mathf.Max(emotionMeter.midThreshold, pre + 0.01f);
        float peaked = Mathf.Max(emotionMeter.peakedThreshold, mid + 0.01f);
        float value = emotionMeter.Value;

        BiomePhase phase = biomeManager != null ? biomeManager.CurrentPhase : BiomePhase.Normal;
        return phase switch
        {
            BiomePhase.Pre => GetRangeProgress(value, pre, mid),
            BiomePhase.Mid => GetRangeProgress(value, mid, peaked),
            BiomePhase.Peaked => GetRangeProgress(value, peaked, 100f),
            _ => GetRangeProgress(value, 0f, pre),
        };
    }

    private static float GetRangeProgress(float value, float start, float end)
    {
        if (end <= start)
        {
            return value >= end ? 1f : 0f;
        }

        return Mathf.Clamp01((value - start) / (end - start));
    }

    private static bool IsSameFamily(BiomeData previousBiome, BiomeData nextBiome)
    {
        if (previousBiome == null || nextBiome == null)
        {
            return false;
        }

        string previousFamily = NormalizeFamilyId(previousBiome.familyId);
        string nextFamily = NormalizeFamilyId(nextBiome.familyId);
        return !string.IsNullOrEmpty(previousFamily) &&
               string.Equals(previousFamily, nextFamily, System.StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeFamilyId(string familyId)
    {
        return string.IsNullOrWhiteSpace(familyId) ? "" : familyId.Trim();
    }
}
