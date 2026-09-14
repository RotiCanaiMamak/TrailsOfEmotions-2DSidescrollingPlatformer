using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class AnxietyBlurPulseMaterialManager : MonoBehaviour
{
    [Serializable]
    public struct BlurPulseSettings
    {
        [Min(0f)] public float minBlurSize;
        [Min(0f)] public float maxBlurSize;
        [Min(0f)] public float frequency;
        [Min(0f)] public float cooldownSeconds;

        public BlurPulseSettings(float minBlurSize, float maxBlurSize, float frequency, float cooldownSeconds)
        {
            this.minBlurSize = minBlurSize;
            this.maxBlurSize = maxBlurSize;
            this.frequency = frequency;
            this.cooldownSeconds = cooldownSeconds;
        }
    }

    [Header("Target")]
    [SerializeField] private RawImage targetImage;
    [SerializeField] private string anxietyFamilyId = "Anxiety";
    [SerializeField] private string blurSizeProperty = "_BlurSize";

    [Header("Pulse Targets")]
    [SerializeField] private BlurPulseSettings midPulse = new BlurPulseSettings(0.15f, 0.55f, 0.65f, 1f);
    [SerializeField] private BlurPulseSettings peakedPulse = new BlurPulseSettings(0.35f, 1.1f, 1.15f, 0.5f);

    [Header("Timing")]
    [Min(0f)] [SerializeField] private float clearBlurSize = 0f;
    [Min(0f)] [SerializeField] private float transitionDuration = 0.35f;

    private BiomeManager biomeManager;
    private Material sourceMaterial;
    private Material runtimeMaterial;
    private readonly Dictionary<UnityEngine.Object, float> additiveBlurBoosts =
        new Dictionary<UnityEngine.Object, float>();
    private readonly List<UnityEngine.Object> additiveBlurBoostKeys =
        new List<UnityEngine.Object>();
    private bool subscribedToBiomeManager;
    private bool shouldPulse;
    private bool warnedMissingBiomeManager;
    private bool warnedMissingTargetImage;
    private bool warnedMissingMaterial;
    private bool warnedEmptyBlurSizeProperty;
    private bool warnedMissingBlurSizeProperty;
    private float pulseTime;
    private float cooldownTime;
    private float currentBlurSize;
    private int blurSizePropertyId;
    private string cachedBlurSizeProperty;
    private bool pulseInCooldown;

    private void Awake()
    {
        ResolveTargetImage();
        EnsureRuntimeMaterial();
        CacheCurrentBlurSize();
    }

    private void OnEnable()
    {
        ResolveTargetImage();
        EnsureRuntimeMaterial();
        TrySubscribeToBiomeManager();
        RefreshPulseState();
        CacheCurrentBlurSize();
    }

    private void Start()
    {
        if (!TrySubscribeToBiomeManager())
        {
            WarnMissingBiomeManager();
        }

        RefreshPulseState();
    }

    private void Update()
    {
        if (!subscribedToBiomeManager && TrySubscribeToBiomeManager())
        {
            RefreshPulseState();
        }

        if (!EnsureRuntimeMaterial() || !HasRequiredProperty())
        {
            return;
        }

        float targetBlurSize = (shouldPulse ? GetPulseBlurSize() : clearBlurSize) +
            GetAdditiveBlurBoost();
        ApplyBlurSize(targetBlurSize);
    }

    private void OnDisable()
    {
        ResetPulseCycle();
        ClearBlurImmediately();
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
        midPulse = ClampPulseSettings(midPulse);
        peakedPulse = ClampPulseSettings(peakedPulse);
        clearBlurSize = Mathf.Max(0f, clearBlurSize);
        transitionDuration = Mathf.Max(0f, transitionDuration);
    }

    private void OnBiomeChanged(BiomeData biome)
    {
        ResetPulseCycle();
        RefreshPulseState();
    }

    private void OnBiomePhaseChanged(int phaseIndex)
    {
        ResetPulseCycle();
        RefreshPulseState();
    }

    public void SetAdditiveBlurBoost(UnityEngine.Object source, float blurBoost)
    {
        if (ReferenceEquals(source, null))
        {
            return;
        }

        float boost = Mathf.Max(0f, blurBoost);
        if (boost <= 0f)
        {
            RemoveAdditiveBlurBoost(source);
            return;
        }

        additiveBlurBoosts[source] = boost;
    }

    public void RemoveAdditiveBlurBoost(UnityEngine.Object source)
    {
        if (ReferenceEquals(source, null))
        {
            return;
        }

        additiveBlurBoosts.Remove(source);
    }

    private void RefreshPulseState()
    {
        bool nextShouldPulse = TryGetCurrentPulse(out _);
        if (shouldPulse != nextShouldPulse)
        {
            ResetPulseCycle();
        }

        shouldPulse = nextShouldPulse;
    }

    private float GetPulseBlurSize()
    {
        if (!TryGetCurrentPulse(out BlurPulseSettings pulse))
        {
            shouldPulse = false;
            return clearBlurSize;
        }

        if (pulseInCooldown)
        {
            cooldownTime += Time.deltaTime;
            if (cooldownTime < pulse.cooldownSeconds)
            {
                return clearBlurSize;
            }

            ResetPulseCycle();
        }

        pulseTime += Time.deltaTime;
        float pulseDuration = 1f / Mathf.Max(0.0001f, pulse.frequency);
        float normalizedPulseTime = Mathf.Clamp01(pulseTime / pulseDuration);
        float sine = Mathf.Sin(normalizedPulseTime * Mathf.PI);
        float smoothed = Mathf.SmoothStep(0f, 1f, sine);

        if (pulseTime >= pulseDuration)
        {
            pulseInCooldown = true;
            cooldownTime = 0f;
            return clearBlurSize;
        }

        return Mathf.Lerp(pulse.minBlurSize, pulse.maxBlurSize, smoothed);
    }

    private void ApplyBlurSize(float targetBlurSize)
    {
        targetBlurSize = Mathf.Max(0f, targetBlurSize);
        if (transitionDuration <= 0f)
        {
            currentBlurSize = targetBlurSize;
        }
        else
        {
            float maxDelta = Mathf.Max(0.0001f, Mathf.Abs(targetBlurSize - currentBlurSize)) *
                Time.deltaTime / transitionDuration;
            currentBlurSize = Mathf.MoveTowards(currentBlurSize, targetBlurSize, maxDelta);
        }

        SetBlurSize(currentBlurSize);
    }

    private void SetBlurSize(float blurSize)
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        runtimeMaterial.SetFloat(blurSizePropertyId, blurSize);
        if (targetImage != null)
        {
            targetImage.SetMaterialDirty();
        }
    }

    private bool TryGetCurrentPulse(out BlurPulseSettings pulse)
    {
        pulse = default;

        biomeManager = BiomeManager.Instance != null ? BiomeManager.Instance : biomeManager;
        if (biomeManager == null)
        {
            WarnMissingBiomeManager();
            return false;
        }

        biomeManager.EnsureInitialized();
        if (!IsAnxietyBiome(biomeManager.CurrentBiome))
        {
            return false;
        }

        switch (biomeManager.CurrentPhase)
        {
            case BiomePhase.Mid:
                pulse = ClampPulseSettings(midPulse);
                return true;
            case BiomePhase.Peaked:
                pulse = ClampPulseSettings(peakedPulse);
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
            WarnOnce(ref warnedMissingTargetImage, "[AnxietyBlurPulseMaterialManager] Assign a RawImage target.");
            return false;
        }

        Material imageMaterial = targetImage.material;
        if (imageMaterial == null)
        {
            WarnOnce(ref warnedMissingMaterial, "[AnxietyBlurPulseMaterialManager] Target RawImage has no material assigned.");
            return false;
        }

        sourceMaterial = imageMaterial;
        runtimeMaterial = new Material(sourceMaterial)
        {
            name = $"{sourceMaterial.name} (Runtime)"
        };
        targetImage.material = runtimeMaterial;
        cachedBlurSizeProperty = null;
        return true;
    }

    private bool HasRequiredProperty()
    {
        if (runtimeMaterial == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(blurSizeProperty))
        {
            WarnOnce(ref warnedEmptyBlurSizeProperty, "[AnxietyBlurPulseMaterialManager] Blur size property is empty.");
            return false;
        }

        if (cachedBlurSizeProperty != blurSizeProperty)
        {
            blurSizePropertyId = Shader.PropertyToID(blurSizeProperty);
            cachedBlurSizeProperty = blurSizeProperty;
            warnedMissingBlurSizeProperty = false;
        }

        if (!runtimeMaterial.HasProperty(blurSizePropertyId))
        {
            WarnOnce(ref warnedMissingBlurSizeProperty, $"[AnxietyBlurPulseMaterialManager] Target material has no {blurSizeProperty} property.");
            return false;
        }

        return true;
    }

    private void CacheCurrentBlurSize()
    {
        if (runtimeMaterial != null && HasRequiredProperty())
        {
            currentBlurSize = Mathf.Max(0f, runtimeMaterial.GetFloat(blurSizePropertyId));
        }
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
        warnedMissingBiomeManager = false;
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
        cachedBlurSizeProperty = null;
    }

    private void ClearBlurImmediately()
    {
        if (runtimeMaterial == null || !HasRequiredProperty())
        {
            return;
        }

        currentBlurSize = Mathf.Max(0f, clearBlurSize);
        SetBlurSize(currentBlurSize);
    }

    private float GetAdditiveBlurBoost()
    {
        if (additiveBlurBoosts.Count == 0)
        {
            return 0f;
        }

        PruneAdditiveBlurBoosts();
        float boost = 0f;
        foreach (float value in additiveBlurBoosts.Values)
        {
            boost += Mathf.Max(0f, value);
        }

        return boost;
    }

    private void PruneAdditiveBlurBoosts()
    {
        additiveBlurBoostKeys.Clear();
        foreach (KeyValuePair<UnityEngine.Object, float> entry in additiveBlurBoosts)
        {
            if (entry.Key == null || entry.Value <= 0f)
            {
                additiveBlurBoostKeys.Add(entry.Key);
            }
        }

        for (int i = 0; i < additiveBlurBoostKeys.Count; i++)
        {
            additiveBlurBoosts.Remove(additiveBlurBoostKeys[i]);
        }

        additiveBlurBoostKeys.Clear();
    }

    private void ResetPulseCycle()
    {
        pulseTime = 0f;
        cooldownTime = 0f;
        pulseInCooldown = false;
    }

    private void ResolveTargetImage()
    {
        if (targetImage == null)
        {
            targetImage = GetComponent<RawImage>();
        }
    }

    private void WarnMissingBiomeManager()
    {
        if (warnedMissingBiomeManager)
        {
            return;
        }

        WarnOnce(ref warnedMissingBiomeManager, "[AnxietyBlurPulseMaterialManager] No BiomeManager instance found.");
        warnedMissingBiomeManager = true;
    }

    private void WarnOnce(ref bool warned, string message)
    {
        if (warned)
        {
            return;
        }

        Debug.LogWarning(message, this);
        warned = true;
    }

    private bool IsAnxietyBiome(BiomeData biome)
    {
        if (biome == null || string.IsNullOrWhiteSpace(anxietyFamilyId))
        {
            return false;
        }

        string targetName = anxietyFamilyId.Trim();
        return MatchesName(biome.familyId, targetName) || MatchesName(biome.biomeName, targetName);
    }

    private static bool MatchesName(string value, string targetName)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               string.Equals(value.Trim(), targetName, StringComparison.OrdinalIgnoreCase);
    }

    private static BlurPulseSettings ClampPulseSettings(BlurPulseSettings pulse)
    {
        pulse.minBlurSize = Mathf.Max(0f, pulse.minBlurSize);
        pulse.maxBlurSize = Mathf.Max(pulse.minBlurSize, pulse.maxBlurSize);
        pulse.frequency = Mathf.Max(0f, pulse.frequency);
        pulse.cooldownSeconds = Mathf.Max(0f, pulse.cooldownSeconds);
        return pulse;
    }
}
