using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class EmotionMeterUI : MonoBehaviour
{
    [Header("Meter")]
    [Tooltip("Horizontal Filled Image used to display the emotion meter value and biome colour.")]
    [SerializeField] private Image fillImage;

    [Header("Colour Timing")]
    [Min(0f)] [SerializeField] private float sameFamilyColorLerpDuration = 0.5f;

    private EmotionMeter emotionMeter;
    private BiomeManager biomeManager;
    private BiomeData activeBiome;
    private Coroutine runningColorLerp;
    private bool warnedMissingFillImage;

    private void Start()
    {
        ResolveFillImage();
        SubscribeToEmotionMeter();
        SubscribeToBiomeManager();
        RefreshValue();
        RefreshBiomeColor(true);
    }

    private void OnDestroy()
    {
        UnsubscribeFromEmotionMeter();
        UnsubscribeFromBiomeManager();
        StopColorLerp();
    }

    private void OnValidate()
    {
        sameFamilyColorLerpDuration = Mathf.Max(0f, sameFamilyColorLerpDuration);
    }

    private void OnEmotionValueChanged(float value)
    {
        SetFillAmount(value);
    }

    private void OnBiomeChanged(BiomeData biome)
    {
        bool sameFamilyTransition = IsSameFamily(activeBiome, biome);
        ApplyBiomeColor(biome, !sameFamilyTransition);
        activeBiome = biome;
    }

    private void SubscribeToEmotionMeter()
    {
        emotionMeter = EmotionMeter.Instance;
        if (emotionMeter == null)
        {
            Debug.LogWarning("[EmotionMeterUI] No EmotionMeter instance found.", this);
            return;
        }

        emotionMeter.onValueChanged.AddListener(OnEmotionValueChanged);
    }

    private void UnsubscribeFromEmotionMeter()
    {
        if (emotionMeter == null)
        {
            return;
        }

        emotionMeter.onValueChanged.RemoveListener(OnEmotionValueChanged);
        emotionMeter = null;
    }

    private void SubscribeToBiomeManager()
    {
        biomeManager = BiomeManager.Instance;
        if (biomeManager == null)
        {
            Debug.LogWarning("[EmotionMeterUI] No BiomeManager instance found.", this);
            return;
        }

        biomeManager.EnsureInitialized();
        activeBiome = biomeManager.CurrentBiome;
        biomeManager.onBiomeChanged.AddListener(OnBiomeChanged);
    }

    private void UnsubscribeFromBiomeManager()
    {
        if (biomeManager == null)
        {
            return;
        }

        biomeManager.onBiomeChanged.RemoveListener(OnBiomeChanged);
        biomeManager = null;
    }

    private void RefreshValue()
    {
        if (emotionMeter != null)
        {
            SetFillAmount(emotionMeter.Value);
        }
        else
        {
            SetFillAmount(0f);
        }
    }

    private void RefreshBiomeColor(bool instant)
    {
        if (biomeManager == null)
        {
            return;
        }

        ApplyBiomeColor(biomeManager.CurrentBiome, instant);
        activeBiome = biomeManager.CurrentBiome;
    }

    private void ApplyBiomeColor(BiomeData biome, bool instant)
    {
        if (!TryGetFillImage(out Image target) || biome == null)
        {
            return;
        }

        StopColorLerp();

        Color targetColor = biome.emotionMeterColor;
        if (instant || sameFamilyColorLerpDuration <= 0f)
        {
            target.color = targetColor;
            return;
        }

        runningColorLerp = StartCoroutine(LerpColor(target, target.color, targetColor));
    }

    private IEnumerator LerpColor(Image target, Color from, Color to)
    {
        float elapsed = 0f;
        while (elapsed < sameFamilyColorLerpDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / sameFamilyColorLerpDuration);
            if (target != null)
            {
                target.color = Color.Lerp(from, to, progress);
            }

            yield return null;
        }

        if (target != null)
        {
            target.color = to;
        }

        runningColorLerp = null;
    }

    private void SetFillAmount(float value)
    {
        if (!TryGetFillImage(out Image target))
        {
            return;
        }

        target.fillAmount = Mathf.Clamp01(value / 100f);
    }

    private bool TryGetFillImage(out Image target)
    {
        ResolveFillImage();
        target = fillImage;
        if (target != null)
        {
            return true;
        }

        WarnMissingFillImage();
        return false;
    }

    private void ResolveFillImage()
    {
        if (fillImage == null)
        {
            fillImage = GetComponent<Image>();
        }
    }

    private void StopColorLerp()
    {
        if (runningColorLerp == null)
        {
            return;
        }

        StopCoroutine(runningColorLerp);
        runningColorLerp = null;
    }

    private void WarnMissingFillImage()
    {
        if (warnedMissingFillImage)
        {
            return;
        }

        Debug.LogWarning("[EmotionMeterUI] Assign a horizontal Filled Image for the emotion meter.", this);
        warnedMissingFillImage = true;
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
               string.Equals(previousFamily, nextFamily, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeFamilyId(string familyId)
    {
        return string.IsNullOrWhiteSpace(familyId) ? "" : familyId.Trim();
    }
}
