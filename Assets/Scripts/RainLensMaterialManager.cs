using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class RainLensMaterialManager : MonoBehaviour
{
    [Serializable]
    public struct RainLensDensityTarget
    {
        [Range(0f, 4f)] public float dropletDensity;
        [Range(0f, 4f)] public float streakDensity;

        public RainLensDensityTarget(float dropletDensity, float streakDensity)
        {
            this.dropletDensity = dropletDensity;
            this.streakDensity = streakDensity;
        }
    }

    private static readonly int DropletDensityId = Shader.PropertyToID("_DropletDensity");
    private static readonly int StreakDensityId = Shader.PropertyToID("_StreakDensity");

    [Header("Target")]
    [SerializeField] private RawImage targetImage;
    [SerializeField] private string sadnessFamilyId = "Sadness";

    [Header("Density Targets")]
    [SerializeField] private RainLensDensityTarget preTarget = new RainLensDensityTarget(0.75f, 0.75f);
    [SerializeField] private RainLensDensityTarget midTarget = new RainLensDensityTarget(2f, 2f);
    [SerializeField] private RainLensDensityTarget peakedTarget = new RainLensDensityTarget(3.25f, 3.25f);

    [Header("Timing")]
    [Min(0f)] [SerializeField] private float lerpDuration = 1f;

    private BiomeManager biomeManager;
    private Material sourceMaterial;
    private Material runtimeMaterial;
    private Coroutine runningLerp;
    private bool subscribedToBiomeManager;
    private bool hasAppliedTarget;
    private RainLensDensityTarget lastAppliedTarget;

    private void Awake()
    {
        ResolveTargetImage();
        EnsureRuntimeMaterial();
    }

    private void OnEnable()
    {
        ResolveTargetImage();
        EnsureRuntimeMaterial();
        TrySubscribeToBiomeManager();
        ApplyCurrentState(true);
    }

    private void Start()
    {
        if (!TrySubscribeToBiomeManager())
        {
            Debug.LogWarning("[RainLensMaterialManager] No BiomeManager instance found.", this);
            return;
        }

        ApplyCurrentState(true);
    }

    private void OnDisable()
    {
        StopRunningLerp();
        UnsubscribeFromBiomeManager();
    }

    private void OnDestroy()
    {
        UnsubscribeFromBiomeManager();
        ReleaseRuntimeMaterial();
    }

    private void OnValidate()
    {
        ResolveTargetImage();
        preTarget = ClampTarget(preTarget);
        midTarget = ClampTarget(midTarget);
        peakedTarget = ClampTarget(peakedTarget);
        lerpDuration = Mathf.Max(0f, lerpDuration);
    }

    private void OnBiomeChanged(BiomeData biome)
    {
        ApplyCurrentState(false);
    }

    private void OnBiomePhaseChanged(int phaseIndex)
    {
        ApplyCurrentState(false);
    }

    private void ApplyCurrentState(bool instant)
    {
        if (!TryGetCurrentTarget(out RainLensDensityTarget target))
        {
            StopRunningLerp();
            hasAppliedTarget = false;
            return;
        }

        if (!EnsureRuntimeMaterial() || !HasRequiredProperties())
        {
            return;
        }

        target = ClampTarget(target);
        if (!instant && hasAppliedTarget && ApproximatelySame(lastAppliedTarget, target))
        {
            return;
        }

        StopRunningLerp();
        if (instant || lerpDuration <= 0f)
        {
            SetDensity(target);
        }
        else
        {
            runningLerp = StartCoroutine(LerpDensity(target));
        }

        lastAppliedTarget = target;
        hasAppliedTarget = true;
    }

    private IEnumerator LerpDensity(RainLensDensityTarget target)
    {
        float startDropletDensity = runtimeMaterial.GetFloat(DropletDensityId);
        float startStreakDensity = runtimeMaterial.GetFloat(StreakDensityId);
        float elapsed = 0f;

        while (elapsed < lerpDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / lerpDuration);
            runtimeMaterial.SetFloat(DropletDensityId, Mathf.Lerp(startDropletDensity, target.dropletDensity, progress));
            runtimeMaterial.SetFloat(StreakDensityId, Mathf.Lerp(startStreakDensity, target.streakDensity, progress));
            yield return null;
        }

        SetDensity(target);
        runningLerp = null;
    }

    private void SetDensity(RainLensDensityTarget target)
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        runtimeMaterial.SetFloat(DropletDensityId, target.dropletDensity);
        runtimeMaterial.SetFloat(StreakDensityId, target.streakDensity);
    }

    private bool TryGetCurrentTarget(out RainLensDensityTarget target)
    {
        target = default;

        biomeManager = BiomeManager.Instance != null ? BiomeManager.Instance : biomeManager;
        if (biomeManager == null)
        {
            return false;
        }

        biomeManager.EnsureInitialized();
        if (!IsSadnessBiome(biomeManager.CurrentBiome))
        {
            return false;
        }

        switch (biomeManager.CurrentPhase)
        {
            case BiomePhase.Pre:
                target = preTarget;
                return true;
            case BiomePhase.Mid:
                target = midTarget;
                return true;
            case BiomePhase.Peaked:
                target = peakedTarget;
                return true;
            default:
                return false;
        }
    }

    private bool EnsureRuntimeMaterial()
    {
        if (runtimeMaterial != null)
        {
            return true;
        }

        if (targetImage == null)
        {
            Debug.LogWarning("[RainLensMaterialManager] Assign a RawImage target.", this);
            return false;
        }

        Material imageMaterial = targetImage.material;
        if (imageMaterial == null)
        {
            Debug.LogWarning("[RainLensMaterialManager] Target RawImage has no material assigned.", this);
            return false;
        }

        sourceMaterial = imageMaterial;
        runtimeMaterial = new Material(sourceMaterial)
        {
            name = $"{sourceMaterial.name} (Runtime)"
        };
        targetImage.material = runtimeMaterial;
        return true;
    }

    private bool HasRequiredProperties()
    {
        if (runtimeMaterial == null)
        {
            return false;
        }

        if (!runtimeMaterial.HasProperty(DropletDensityId))
        {
            Debug.LogWarning("[RainLensMaterialManager] Target material has no _DropletDensity property.", this);
            return false;
        }

        if (!runtimeMaterial.HasProperty(StreakDensityId))
        {
            Debug.LogWarning("[RainLensMaterialManager] Target material has no _StreakDensity property.", this);
            return false;
        }

        return true;
    }

    private bool TrySubscribeToBiomeManager()
    {
        if (subscribedToBiomeManager)
        {
            return true;
        }

        biomeManager = BiomeManager.Instance;
        if (biomeManager == null)
        {
            return false;
        }

        biomeManager.onBiomeChanged.AddListener(OnBiomeChanged);
        biomeManager.onBiomePhaseChanged.AddListener(OnBiomePhaseChanged);
        subscribedToBiomeManager = true;
        return true;
    }

    private void UnsubscribeFromBiomeManager()
    {
        if (!subscribedToBiomeManager || biomeManager == null)
        {
            subscribedToBiomeManager = false;
            return;
        }

        biomeManager.onBiomeChanged.RemoveListener(OnBiomeChanged);
        biomeManager.onBiomePhaseChanged.RemoveListener(OnBiomePhaseChanged);
        subscribedToBiomeManager = false;
    }

    private void StopRunningLerp()
    {
        if (runningLerp == null)
        {
            return;
        }

        StopCoroutine(runningLerp);
        runningLerp = null;
    }

    private void ReleaseRuntimeMaterial()
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        if (targetImage != null && targetImage.material == runtimeMaterial)
        {
            targetImage.material = sourceMaterial;
        }

        Destroy(runtimeMaterial);
        runtimeMaterial = null;
        sourceMaterial = null;
    }

    private void ResolveTargetImage()
    {
        if (targetImage == null)
        {
            targetImage = GetComponent<RawImage>();
        }
    }

    private bool IsSadnessBiome(BiomeData biome)
    {
        if (biome == null || string.IsNullOrWhiteSpace(sadnessFamilyId))
        {
            return false;
        }

        string targetName = sadnessFamilyId.Trim();
        return MatchesName(biome.familyId, targetName) || MatchesName(biome.biomeName, targetName);
    }

    private static bool MatchesName(string value, string targetName)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               string.Equals(value.Trim(), targetName, StringComparison.OrdinalIgnoreCase);
    }

    private static RainLensDensityTarget ClampTarget(RainLensDensityTarget target)
    {
        target.dropletDensity = Mathf.Clamp(target.dropletDensity, 0f, 4f);
        target.streakDensity = Mathf.Clamp(target.streakDensity, 0f, 4f);
        return target;
    }

    private static bool ApproximatelySame(RainLensDensityTarget a, RainLensDensityTarget b)
    {
        return Mathf.Approximately(a.dropletDensity, b.dropletDensity) &&
               Mathf.Approximately(a.streakDensity, b.streakDensity);
    }
}
