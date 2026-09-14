using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class AngerHeatWaveMaterialManager : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private RawImage targetImage;
    [SerializeField] private string angerFamilyId = "Anger";
    [SerializeField] private string distortionProperty = "_Distortion";

    [Header("Distortion Targets")]
    [Min(0f)] [SerializeField] private float preDistortion = 0.002f;
    [Min(0f)] [SerializeField] private float midDistortion = 0.004f;
    [Min(0f)] [SerializeField] private float peakedDistortion = 0.006f;

    [Header("Timing")]
    [Min(0f)] [SerializeField] private float lerpDuration = 1f;

    private BiomeManager biomeManager;
    private Material sourceMaterial;
    private Material runtimeMaterial;
    private Coroutine runningLerp;
    private bool subscribedToBiomeManager;
    private bool hasAppliedTarget;
    private float lastAppliedTarget;
    private int distortionPropertyId;
    private string cachedDistortionProperty;

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
            Debug.LogWarning("[AngerHeatWaveMaterialManager] No BiomeManager instance found.", this);
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
        preDistortion = Mathf.Max(0f, preDistortion);
        midDistortion = Mathf.Max(0f, midDistortion);
        peakedDistortion = Mathf.Max(0f, peakedDistortion);
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
        if (!TryGetCurrentTarget(out float targetDistortion))
        {
            StopRunningLerp();
            hasAppliedTarget = false;
            return;
        }

        if (!EnsureRuntimeMaterial() || !HasRequiredProperty())
        {
            return;
        }

        targetDistortion = Mathf.Max(0f, targetDistortion);
        if (!instant && hasAppliedTarget && Mathf.Approximately(lastAppliedTarget, targetDistortion))
        {
            return;
        }

        StopRunningLerp();
        if (instant || lerpDuration <= 0f)
        {
            SetDistortion(targetDistortion);
        }
        else
        {
            runningLerp = StartCoroutine(LerpDistortion(targetDistortion));
        }

        lastAppliedTarget = targetDistortion;
        hasAppliedTarget = true;
    }

    private IEnumerator LerpDistortion(float targetDistortion)
    {
        float startDistortion = runtimeMaterial.GetFloat(distortionPropertyId);
        float elapsed = 0f;

        while (elapsed < lerpDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / lerpDuration);
            SetDistortion(Mathf.Lerp(startDistortion, targetDistortion, progress));
            yield return null;
        }

        SetDistortion(targetDistortion);
        runningLerp = null;
    }

    private void SetDistortion(float distortion)
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        runtimeMaterial.SetFloat(distortionPropertyId, distortion);
        if (targetImage != null)
        {
            targetImage.SetMaterialDirty();
        }
    }

    private bool TryGetCurrentTarget(out float targetDistortion)
    {
        targetDistortion = 0f;

        biomeManager = BiomeManager.Instance != null ? BiomeManager.Instance : biomeManager;
        if (biomeManager == null)
        {
            return false;
        }

        biomeManager.EnsureInitialized();
        if (!IsAngerBiome(biomeManager.CurrentBiome))
        {
            return false;
        }

        switch (biomeManager.CurrentPhase)
        {
            case BiomePhase.Pre:
                targetDistortion = preDistortion;
                return true;
            case BiomePhase.Mid:
                targetDistortion = midDistortion;
                return true;
            case BiomePhase.Peaked:
                targetDistortion = peakedDistortion;
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
            Debug.LogWarning("[AngerHeatWaveMaterialManager] Assign a RawImage target.", this);
            return false;
        }

        Material imageMaterial = targetImage.material;
        if (imageMaterial == null)
        {
            Debug.LogWarning("[AngerHeatWaveMaterialManager] Target RawImage has no material assigned.", this);
            return false;
        }

        sourceMaterial = imageMaterial;
        runtimeMaterial = new Material(sourceMaterial)
        {
            name = $"{sourceMaterial.name} (Runtime)"
        };
        targetImage.material = runtimeMaterial;
        cachedDistortionProperty = null;
        return true;
    }

    private bool HasRequiredProperty()
    {
        if (runtimeMaterial == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(distortionProperty))
        {
            Debug.LogWarning("[AngerHeatWaveMaterialManager] Distortion property is empty.", this);
            return false;
        }

        if (cachedDistortionProperty != distortionProperty)
        {
            distortionPropertyId = Shader.PropertyToID(distortionProperty);
            cachedDistortionProperty = distortionProperty;
        }

        if (!runtimeMaterial.HasProperty(distortionPropertyId))
        {
            Debug.LogWarning($"[AngerHeatWaveMaterialManager] Target material has no {distortionProperty} property.", this);
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
        cachedDistortionProperty = null;
    }

    private void ResolveTargetImage()
    {
        if (targetImage == null)
        {
            targetImage = GetComponent<RawImage>();
        }
    }

    private bool IsAngerBiome(BiomeData biome)
    {
        if (biome == null || string.IsNullOrWhiteSpace(angerFamilyId))
        {
            return false;
        }

        string targetName = angerFamilyId.Trim();
        return MatchesName(biome.familyId, targetName) || MatchesName(biome.biomeName, targetName);
    }

    private static bool MatchesName(string value, string targetName)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               string.Equals(value.Trim(), targetName, StringComparison.OrdinalIgnoreCase);
    }
}
