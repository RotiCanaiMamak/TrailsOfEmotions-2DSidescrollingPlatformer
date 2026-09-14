using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LinaBackpackBulwark : MonoBehaviour
{
    [Header("Absorption")]
    [SerializeField] private Collider2D absorbCollider;
    [Min(0f)] [SerializeField] private float emotionReductionPerAbsorb = 5f;

    [Header("Visual")]
    [SerializeField] private Transform backpackRoot;
    [SerializeField] private SpriteRenderer backpackRenderer;
    [SerializeField] private Vector3 activeLocalOffset = new Vector3(1.5f, 0f, 0f);
    [Tooltip("Leave at zero to use the backpack object's initial local scale.")]
    [SerializeField] private Vector3 activeLocalScale = Vector3.zero;
    [Min(0f)] [SerializeField] private float flyOutDuration = 0.2f;
    [Min(0f)] [SerializeField] private float returnDuration = 0.2f;
    [Min(0f)] [SerializeField] private float hoverAmplitude = 0.03f;
    [Min(0f)] [SerializeField] private float hoverFrequency = 2f;

    [Header("Shield Particles")]
    [SerializeField] private ParticleSystem shieldParticles;
    [Min(0f)] [SerializeField] private float particleBreathMinMultiplier = 0.85f;
    [Min(0f)] [SerializeField] private float particleBreathMaxMultiplier = 1.2f;
    [Min(0.01f)] [SerializeField] private float particleBreathInterval = 1f;
    [Min(0f)] [SerializeField] private float particleSizeRampDuration = 0.2f;

    private readonly Collider2D[] absorbHits = new Collider2D[32];
    private readonly HashSet<int> absorbedObjects = new HashSet<int>();
    private ContactFilter2D absorbFilter;
    private Coroutine activeRoutine;
    private Vector3 backpackBaseLocalPosition;
    private Vector3 backpackTargetScale;
    private bool hasBackpackBaseValues;
    private ParticleSystem cachedShieldParticles;
    private ParticleSystem.MinMaxCurve baseSizeOverLifetime;
    private ParticleSystem.MinMaxCurve baseParticleStartLifetime;
    private bool baseSizeOverLifetimeEnabled;
    private bool hasSizeOverLifetimeBaseValues;

    public bool IsActive => activeRoutine != null;

    private void Awake()
    {
        ConfigureAbsorbFilter();
        CacheBackpackBaseValues();
        ResetBackpackVisual();
    }

    private void OnDisable()
    {
        StopActiveRoutine();
    }

    public void Activate(float duration)
    {
        StopActiveRoutine();
        activeRoutine = StartCoroutine(ActiveRoutine(Mathf.Max(0f, duration)));
    }

    private IEnumerator ActiveRoutine(float duration)
    {
        absorbedObjects.Clear();
        PrepareBackpackActivation();
        StartShieldParticles(duration);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            UpdateBackpackActivePosition(elapsed);
            AbsorbNearbyHazards();
            elapsed += Time.deltaTime;
            yield return null;
        }

        UpdateBackpackActivePosition(duration);
        AbsorbNearbyHazards();
        StopShieldParticles();

        yield return ReturnBackpack();
        activeRoutine = null;
    }

    private void StopActiveRoutine()
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        ResetBackpackVisual();
        StopShieldParticles();
        absorbedObjects.Clear();
    }

    private void AbsorbNearbyHazards()
    {
        if (absorbCollider == null)
        {
            return;
        }

        ConfigureAbsorbFilter();
        int hitCount = absorbCollider.Overlap(absorbFilter, absorbHits);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = absorbHits[i];
            if (hit == null || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            IBackpackAbsorbable absorbable = hit.GetComponentInParent<IBackpackAbsorbable>();
            Object absorbKey = absorbable as Object;
            if (absorbKey == null || !absorbedObjects.Add(absorbKey.GetInstanceID()))
            {
                continue;
            }

            if (absorbable.TryAbsorbByBackpack(this))
            {
                ReduceEmotion();
                AddActiveAbilityCharge();
                ScoreManager.Instance?.AddAbsorptionScore();
            }
        }
    }

    private void ConfigureAbsorbFilter()
    {
        absorbFilter.useTriggers = true;
    }

    private void ReduceEmotion()
    {
        if (EmotionMeter.Instance != null && emotionReductionPerAbsorb > 0f)
        {
            EmotionMeter.Instance.AddEmotion(-emotionReductionPerAbsorb);
        }
    }

    private void AddActiveAbilityCharge()
    {
        CharacterRuntime runtime = GetComponent<CharacterRuntime>();
        runtime?.TryAddActiveAbilityCharge();
    }

    private void CacheBackpackBaseValues()
    {
        ResolveBackpackReferences();
        if (backpackRoot == null)
        {
            return;
        }

        if (!hasBackpackBaseValues)
        {
            backpackBaseLocalPosition = backpackRoot.localPosition;
            backpackTargetScale = activeLocalScale != Vector3.zero
                ? activeLocalScale
                : backpackRoot.localScale;
            hasBackpackBaseValues = true;
        }
    }

    private void PrepareBackpackActivation()
    {
        CacheBackpackBaseValues();
        if (backpackRoot == null)
        {
            return;
        }

        SetBackpackVisible(true);
        backpackRoot.localPosition = backpackBaseLocalPosition;
        backpackRoot.localScale = Vector3.zero;
    }

    private void UpdateBackpackActivePosition(float elapsed)
    {
        if (backpackRoot == null)
        {
            return;
        }

        if (flyOutDuration > 0f && elapsed < flyOutDuration)
        {
            float flyT = SmoothCosine(elapsed / flyOutDuration);
            backpackRoot.localPosition = Vector3.Lerp(
                backpackBaseLocalPosition,
                GetBackpackActiveBasePosition(),
                flyT);
            backpackRoot.localScale = Vector3.Lerp(Vector3.zero, backpackTargetScale, flyT);
            return;
        }

        backpackRoot.localPosition = GetBackpackActiveBasePosition() + GetBackpackHoverOffset(elapsed);
        backpackRoot.localScale = backpackTargetScale;
    }

    private IEnumerator ReturnBackpack()
    {
        if (backpackRoot == null)
        {
            yield break;
        }

        Vector3 returnStartPosition = backpackRoot.localPosition;
        Vector3 returnStartScale = backpackRoot.localScale;
        float elapsed = 0f;
        while (elapsed < returnDuration)
        {
            float returnT = returnDuration > 0f
                ? SmoothCosine(elapsed / returnDuration)
                : 1f;
            backpackRoot.localPosition = Vector3.Lerp(
                returnStartPosition,
                backpackBaseLocalPosition,
                returnT);
            backpackRoot.localScale = Vector3.Lerp(returnStartScale, Vector3.zero, returnT);

            elapsed += Time.deltaTime;
            yield return null;
        }

        ResetBackpackVisual();
    }

    private Vector3 GetBackpackActiveBasePosition()
    {
        return backpackBaseLocalPosition + activeLocalOffset;
    }

    private Vector3 GetBackpackHoverOffset(float elapsed)
    {
        if (hoverAmplitude <= 0f || hoverFrequency <= 0f)
        {
            return Vector3.zero;
        }

        float hoverElapsed = Mathf.Max(0f, elapsed - flyOutDuration);
        float yOffset = Mathf.Sin(hoverElapsed * hoverFrequency * Mathf.PI * 2f) * hoverAmplitude;
        return Vector3.up * yOffset;
    }

    private void ResetBackpackVisual()
    {
        CacheBackpackBaseValues();
        if (backpackRoot != null)
        {
            backpackRoot.localPosition = backpackBaseLocalPosition;
            backpackRoot.localScale = Vector3.zero;
        }

        SetBackpackVisible(false);
    }

    private void ResolveBackpackReferences()
    {
        if (backpackRoot == null && backpackRenderer != null)
        {
            backpackRoot = backpackRenderer.transform;
        }

        if (backpackRenderer == null && backpackRoot != null && backpackRoot != transform)
        {
            backpackRenderer = backpackRoot.GetComponentInChildren<SpriteRenderer>(true);
        }
    }

    private void SetBackpackVisible(bool visible)
    {
        if (backpackRoot != null && backpackRoot != transform)
        {
            backpackRoot.gameObject.SetActive(visible);
        }

        if (backpackRenderer != null)
        {
            backpackRenderer.enabled = visible;
        }
    }

    private void StartShieldParticles(float duration)
    {
        ResolveShieldParticles();
        if (shieldParticles == null)
        {
            return;
        }

        CacheShieldParticleBaseValues();
        ApplyShieldParticleBreathCurve(duration);

        if (!shieldParticles.isPlaying)
        {
            shieldParticles.Play(true);
        }
    }

    private void StopShieldParticles()
    {
        if (shieldParticles == null)
        {
            return;
        }

        RestoreShieldParticleBaseValues();
        shieldParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void ResolveShieldParticles()
    {
        if (shieldParticles == null)
        {
            shieldParticles = GetComponentInChildren<ParticleSystem>();
        }
    }

    private void CacheShieldParticleBaseValues()
    {
        if (shieldParticles == null
            || (hasSizeOverLifetimeBaseValues && cachedShieldParticles == shieldParticles))
        {
            return;
        }

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = shieldParticles.sizeOverLifetime;
        ParticleSystem.MainModule main = shieldParticles.main;

        cachedShieldParticles = shieldParticles;
        baseSizeOverLifetime = sizeOverLifetime.size;
        baseParticleStartLifetime = main.startLifetime;
        baseSizeOverLifetimeEnabled = sizeOverLifetime.enabled;
        hasSizeOverLifetimeBaseValues = true;
    }

    private void RestoreShieldParticleBaseValues()
    {
        if (shieldParticles == null || !hasSizeOverLifetimeBaseValues || cachedShieldParticles != shieldParticles)
        {
            return;
        }

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = shieldParticles.sizeOverLifetime;
        ParticleSystem.MainModule main = shieldParticles.main;

        sizeOverLifetime.size = baseSizeOverLifetime;
        sizeOverLifetime.enabled = baseSizeOverLifetimeEnabled;
        main.startLifetime = baseParticleStartLifetime;
        hasSizeOverLifetimeBaseValues = false;
    }

    private void ApplyShieldParticleBreathCurve(float duration)
    {
        if (shieldParticles == null)
        {
            return;
        }

        float safeDuration = Mathf.Max(0.01f, duration);
        ParticleSystem.MainModule main = shieldParticles.main;
        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = shieldParticles.sizeOverLifetime;

        main.startLifetime = safeDuration;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, BuildBreathCurve(safeDuration));
    }

    private AnimationCurve BuildBreathCurve(float duration)
    {
        float safeInterval = Mathf.Max(0.01f, particleBreathInterval);
        float safeDuration = Mathf.Max(0.01f, duration);
        float rampDuration = Mathf.Min(particleSizeRampDuration, safeDuration * 0.5f);
        float cycles = safeDuration / safeInterval;
        int sampleCount = Mathf.Max(17, Mathf.CeilToInt(cycles * 8f) + 5);
        Keyframe[] keys = new Keyframe[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (sampleCount - 1f);
            float elapsed = t * safeDuration;
            float multiplier = EvaluateBreathSize(elapsed, safeDuration, rampDuration, safeInterval);
            keys[i] = new Keyframe(t, multiplier);
        }

        AnimationCurve curve = new AnimationCurve(keys);
        for (int i = 0; i < keys.Length; i++)
        {
            curve.SmoothTangents(i, 0f);
        }

        return curve;
    }

    private float EvaluateBreathSize(float elapsed, float duration, float rampDuration, float breathInterval)
    {
        if (duration <= 0f)
        {
            return 0f;
        }

        if (rampDuration > 0f && elapsed < rampDuration)
        {
            float rampT = elapsed / rampDuration;
            return Mathf.Lerp(
                0f,
                particleBreathMinMultiplier,
                SmoothCosine(rampT));
        }

        float rampOutStart = duration - rampDuration;
        if (rampDuration > 0f && elapsed > rampOutStart)
        {
            float rampT = (elapsed - rampOutStart) / rampDuration;
            return Mathf.Lerp(
                EvaluateBreathPulse(rampOutStart, rampDuration, breathInterval),
                0f,
                SmoothCosine(rampT));
        }

        return EvaluateBreathPulse(elapsed, rampDuration, breathInterval);
    }

    private float EvaluateBreathPulse(float elapsed, float rampDuration, float breathInterval)
    {
        float breathElapsed = Mathf.Max(0f, elapsed - rampDuration);
        float pulse = (1f - Mathf.Cos(breathElapsed / breathInterval * Mathf.PI * 2f)) * 0.5f;
        return Mathf.Lerp(particleBreathMinMultiplier, particleBreathMaxMultiplier, pulse);
    }

    private static float SmoothCosine(float t)
    {
        return (1f - Mathf.Cos(Mathf.Clamp01(t) * Mathf.PI)) * 0.5f;
    }

    private void OnValidate()
    {
        particleBreathMinMultiplier = Mathf.Max(0f, particleBreathMinMultiplier);
        particleBreathMaxMultiplier = Mathf.Max(particleBreathMinMultiplier, particleBreathMaxMultiplier);
        particleBreathInterval = Mathf.Max(0.01f, particleBreathInterval);
        particleSizeRampDuration = Mathf.Max(0f, particleSizeRampDuration);
        flyOutDuration = Mathf.Max(0f, flyOutDuration);
        returnDuration = Mathf.Max(0f, returnDuration);
        hoverAmplitude = Mathf.Max(0f, hoverAmplitude);
        hoverFrequency = Mathf.Max(0f, hoverFrequency);
    }

    private void OnDrawGizmosSelected()
    {
        if (absorbCollider == null)
        {
            return;
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(absorbCollider.bounds.center, absorbCollider.bounds.size);
    }
}
