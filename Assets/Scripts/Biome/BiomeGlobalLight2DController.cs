using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

/// <summary>
/// Applies each active biome's persistent 2D global light colour and intensity.
/// </summary>
public class BiomeGlobalLight2DController : MonoBehaviour
{
    [System.Serializable]
    public class LightSetting
    {
        public Light2D light = null;

        public Color color = Color.white;

        [Min(0f)] public float intensity = 1f;
    }

    [System.Serializable]
    public class BiomeLightMapping
    {
        public BiomeData biome = null;

        public List<LightSetting> lights = new List<LightSetting>();
    }

    private struct LightTarget
    {
        public readonly Light2D Light;
        public readonly Color StartColor;
        public readonly float StartIntensity;
        public readonly Color TargetColor;
        public readonly float TargetIntensity;

        public LightTarget(Light2D light, Color targetColor, float targetIntensity)
        {
            Light = light;
            StartColor = light != null ? light.color : Color.white;
            StartIntensity = light != null ? light.intensity : 0f;
            TargetColor = targetColor;
            TargetIntensity = Mathf.Max(0f, targetIntensity);
        }
    }

    [Header("Fallback Global Light")]
    [SerializeField] private Light2D globalLight;

    [Header("Biome Light Mappings")]
    [SerializeField] private List<BiomeLightMapping> biomeLightMappings = new List<BiomeLightMapping>();

    [Header("Transitions")]
    [FormerlySerializedAs("preToMidLerpDuration")]
    [Min(0f)] [SerializeField] private float sameFamilyLerpDuration = 1f;

    private BiomeManager biomeManager;
    private bool startupApplied;
    private BiomeData activeBiome;
    private readonly List<LightTarget> lightTargets = new List<LightTarget>();
    private Coroutine activeLightLerp;

    private void Start()
    {
        if (globalLight == null)
        {
            globalLight = GetComponent<Light2D>();
        }

        WarnInvalidMappings();

        biomeManager = BiomeManager.Instance;
        if (biomeManager == null)
        {
            Debug.LogWarning("[BiomeGlobalLight2DController] No BiomeManager instance found.", this);
            return;
        }

        biomeManager.EnsureInitialized();
        ApplyBiomeLighting(biomeManager.CurrentBiome, true);
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

        StopActiveLightLerp();
    }

    private void OnValidate()
    {
        sameFamilyLerpDuration = Mathf.Max(0f, sameFamilyLerpDuration);
        ValidateMappings();
    }

    private void OnBiomeChanged(BiomeData biome)
    {
        bool shouldLerp = startupApplied && IsSameFamily(activeBiome, biome);

        ApplyBiomeLighting(biome, !shouldLerp);
        activeBiome = biome;
        startupApplied = true;
    }

    private void ApplyBiomeLighting(BiomeData biome, bool instant)
    {
        StopActiveLightLerp();

        if (biome == null)
        {
            return;
        }

        BuildLightTargets(biome);
        if (lightTargets.Count == 0)
        {
            return;
        }

        LightTarget[] targets = lightTargets.ToArray();
        if (instant || sameFamilyLerpDuration <= 0f)
        {
            ApplyTargets(targets, 1f);
            return;
        }

        activeLightLerp = StartCoroutine(LerpTargets(targets));
    }

    private void BuildLightTargets(BiomeData biome)
    {
        lightTargets.Clear();

        BiomeLightMapping mapping = GetMappingForBiome(biome);
        if (mapping != null)
        {
            AddMappingTargets(mapping);
            return;
        }

        if (globalLight == null)
        {
            Debug.LogWarning($"[BiomeGlobalLight2DController] No light mapping for biome '{biome.biomeName}' and no fallback Global Light assigned.", this);
            return;
        }

        lightTargets.Add(new LightTarget(
            globalLight,
            biome.globalLightColor,
            biome.globalLightIntensity));
    }

    private void AddMappingTargets(BiomeLightMapping mapping)
    {
        if (mapping.lights == null || mapping.lights.Count == 0)
        {
            string biomeName = mapping.biome != null ? mapping.biome.biomeName : "None";
            Debug.LogWarning($"[BiomeGlobalLight2DController] Biome light mapping for '{biomeName}' has no lights assigned.", this);
            return;
        }

        for (int i = 0; i < mapping.lights.Count; i++)
        {
            LightSetting setting = mapping.lights[i];
            if (setting == null)
            {
                continue;
            }

            if (setting.light == null)
            {
                string biomeName = mapping.biome != null ? mapping.biome.biomeName : "None";
                Debug.LogWarning($"[BiomeGlobalLight2DController] Biome light mapping for '{biomeName}' has a missing Light2D at index {i}.", this);
                continue;
            }

            lightTargets.Add(new LightTarget(setting.light, setting.color, setting.intensity));
        }
    }

    private BiomeLightMapping GetMappingForBiome(BiomeData biome)
    {
        if (biomeLightMappings == null || biome == null)
        {
            return null;
        }

        BiomeLightMapping match = null;
        for (int i = 0; i < biomeLightMappings.Count; i++)
        {
            BiomeLightMapping mapping = biomeLightMappings[i];
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
                Debug.LogWarning($"[BiomeGlobalLight2DController] Duplicate light mapping for biome '{biome.biomeName}'. The first mapping will be used.", this);
                break;
            }
        }

        return match;
    }

    private void ApplyTargets(LightTarget[] targets, float progress)
    {
        progress = Mathf.Clamp01(progress);
        for (int i = 0; i < targets.Length; i++)
        {
            LightTarget target = targets[i];
            if (target.Light == null)
            {
                continue;
            }

            target.Light.color = Color.Lerp(target.StartColor, target.TargetColor, progress);
            target.Light.intensity = Mathf.Lerp(target.StartIntensity, target.TargetIntensity, progress);
        }
    }

    private IEnumerator LerpTargets(LightTarget[] targets)
    {
        float elapsed = 0f;
        while (elapsed < sameFamilyLerpDuration)
        {
            elapsed += Time.deltaTime;
            ApplyTargets(targets, Mathf.Clamp01(elapsed / sameFamilyLerpDuration));
            yield return null;
        }

        ApplyTargets(targets, 1f);
        activeLightLerp = null;
    }

    private void WarnInvalidMappings()
    {
        if (biomeLightMappings == null || biomeLightMappings.Count == 0)
        {
            if (globalLight == null)
            {
                Debug.LogWarning("[BiomeGlobalLight2DController] No biome light mappings or fallback Global Light assigned.", this);
            }

            return;
        }

        HashSet<BiomeData> seenBiomes = new HashSet<BiomeData>();
        for (int i = 0; i < biomeLightMappings.Count; i++)
        {
            BiomeLightMapping mapping = biomeLightMappings[i];
            if (mapping == null)
            {
                continue;
            }

            if (mapping.biome == null)
            {
                Debug.LogWarning($"[BiomeGlobalLight2DController] Biome light mapping {i} has no BiomeData assigned.", this);
            }
            else if (!seenBiomes.Add(mapping.biome))
            {
                Debug.LogWarning($"[BiomeGlobalLight2DController] Duplicate light mapping for biome '{mapping.biome.biomeName}'. The first mapping will be used.", this);
            }

            WarnInvalidLightSettings(mapping, i);
        }
    }

    private void WarnInvalidLightSettings(BiomeLightMapping mapping, int mappingIndex)
    {
        if (mapping == null || mapping.lights == null)
        {
            return;
        }

        for (int i = 0; i < mapping.lights.Count; i++)
        {
            LightSetting setting = mapping.lights[i];
            if (setting == null)
            {
                continue;
            }

            if (setting.light == null)
            {
                Debug.LogWarning($"[BiomeGlobalLight2DController] Biome light mapping {mappingIndex} has no Light2D assigned at light index {i}.", this);
            }
        }
    }

    private void ValidateMappings()
    {
        if (biomeLightMappings == null)
        {
            return;
        }

        for (int i = 0; i < biomeLightMappings.Count; i++)
        {
            BiomeLightMapping mapping = biomeLightMappings[i];
            if (mapping == null || mapping.lights == null)
            {
                continue;
            }

            for (int lightIndex = 0; lightIndex < mapping.lights.Count; lightIndex++)
            {
                LightSetting setting = mapping.lights[lightIndex];
                if (setting == null)
                {
                    continue;
                }

                setting.intensity = Mathf.Max(0f, setting.intensity);
            }
        }
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

    private void StopActiveLightLerp()
    {
        if (activeLightLerp == null)
        {
            return;
        }

        StopCoroutine(activeLightLerp);
        activeLightLerp = null;
    }
}
