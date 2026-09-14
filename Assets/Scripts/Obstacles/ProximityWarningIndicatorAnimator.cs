using UnityEngine;

[DisallowMultipleComponent]
public class ProximityWarningIndicatorAnimator : MonoBehaviour
{
    private const string DefaultBlinkPropertyName = "_BlinkFactor";

    [Header("Renderer")]
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private string blinkPropertyName = DefaultBlinkPropertyName;

    [Header("Idle Pulse")]
    [Min(0f)] [SerializeField] private float maxAnimationDistance = 24f;
    [Min(0f)] [SerializeField] private float scaleMultiplier = 1.25f;
    [Min(0.01f)] [SerializeField] private float farCycleDuration = 0.8f;
    [Min(0.01f)] [SerializeField] private float nearCycleDuration = 0.22f;
    [Min(0f)] [SerializeField] private float warningJitterDistance = 0.06f;
    [Min(0f)] [SerializeField] private float warningJitterFrequency = 32f;
    [Range(0f, 1f)] [SerializeField] private float minBlinkFactor = 0f;
    [Range(0f, 1f)] [SerializeField] private float maxBlinkFactor = 0.8f;

    [Header("Final Warning")]
    [Min(0f)] [SerializeField] private float finalWarningScaleMultiplier = 1.8f;
    [Min(0f)] [SerializeField] private float finalWarningJitterDistance = 0.12f;
    [Min(0f)] [SerializeField] private float finalWarningJitterDuration = 0.25f;
    [Min(0f)] [SerializeField] private float finalWarningJitterFrequency = 45f;

    private Vector3 originalLocalPosition;
    private Vector3 originalLocalScale;
    private Material runtimeMaterial;
    private int blinkPropertyId;
    private float elapsed;
    private float threatProgress;
    private float finalWarningElapsed;
    private float finalWarningJitterSampleElapsed;
    private float warningJitterSampleElapsed;
    private Vector2 finalWarningJitterOffset;
    private Vector2 warningJitterOffset;
    private bool hasBlinkProperty;
    private bool warnedMissingBlinkProperty;
    private bool playingFinalWarning;

    private void Awake()
    {
        CaptureOriginalTransform();
        ResolveRenderer();
        InitializeMaterial();
    }

    private void OnEnable()
    {
        CaptureOriginalTransform();
        elapsed = 0f;
        finalWarningElapsed = 0f;
        warningJitterSampleElapsed = 0f;
        warningJitterOffset = Vector2.zero;
        playingFinalWarning = false;
    }

    private void OnValidate()
    {
        maxAnimationDistance = Mathf.Max(0f, maxAnimationDistance);
        scaleMultiplier = Mathf.Max(0f, scaleMultiplier);
        farCycleDuration = Mathf.Max(0.01f, farCycleDuration);
        nearCycleDuration = Mathf.Max(0.01f, nearCycleDuration);
        warningJitterDistance = Mathf.Max(0f, warningJitterDistance);
        warningJitterFrequency = Mathf.Max(0f, warningJitterFrequency);
        minBlinkFactor = Mathf.Clamp01(minBlinkFactor);
        maxBlinkFactor = Mathf.Clamp01(maxBlinkFactor);
        finalWarningScaleMultiplier = Mathf.Max(0f, finalWarningScaleMultiplier);
        finalWarningJitterDistance = Mathf.Max(0f, finalWarningJitterDistance);
        finalWarningJitterDuration = Mathf.Max(0f, finalWarningJitterDuration);
        finalWarningJitterFrequency = Mathf.Max(0f, finalWarningJitterFrequency);
    }

    private void Update()
    {
        if (playingFinalWarning)
        {
            UpdateFinalWarning();
            return;
        }

        UpdateIdlePulse();
    }

    private void OnDestroy()
    {
        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
            runtimeMaterial = null;
        }
    }

    public void SetThreatProgress(float progress)
    {
        threatProgress = Mathf.Clamp01(progress);
    }

    public void SetThreatDistance(float xDistance, float triggerDistance)
    {
        float safeTriggerDistance = Mathf.Max(0f, triggerDistance);
        float safeMaxAnimationDistance = Mathf.Max(
            safeTriggerDistance + 0.01f,
            maxAnimationDistance);
        float safeXDistance = Mathf.Max(0f, xDistance);
        SetThreatProgress(Mathf.InverseLerp(
            safeMaxAnimationDistance,
            safeTriggerDistance,
            safeXDistance));
    }

    public void PlayFinalWarningAndDestroy()
    {
        if (playingFinalWarning)
        {
            return;
        }

        playingFinalWarning = true;
        finalWarningElapsed = 0f;
        finalWarningJitterSampleElapsed = 0f;
        finalWarningJitterOffset = Random.insideUnitCircle * finalWarningJitterDistance;
        transform.localScale = originalLocalScale * finalWarningScaleMultiplier;
        SetBlinkFactor(1f);
    }

    private void UpdateIdlePulse()
    {
        float cycleDuration = Mathf.Lerp(
            farCycleDuration,
            nearCycleDuration,
            threatProgress);
        elapsed += Time.deltaTime;

        float cycleT = Mathf.Repeat(elapsed / cycleDuration, 1f);
        float pulse = Mathf.Sin(cycleT * Mathf.PI);
        float easedPulse = Mathf.SmoothStep(0f, 1f, pulse);

        UpdateWarningJitterOffset();
        transform.localPosition =
            originalLocalPosition + new Vector3(
                warningJitterOffset.x,
                warningJitterOffset.y,
                0f);
        transform.localScale = Vector3.Lerp(
            originalLocalScale,
            originalLocalScale * scaleMultiplier,
            easedPulse);
        SetBlinkFactor(Mathf.Lerp(minBlinkFactor, maxBlinkFactor, easedPulse));
    }

    private void UpdateFinalWarning()
    {
        if (finalWarningJitterDuration <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        finalWarningElapsed += Time.deltaTime;
        if (finalWarningElapsed >= finalWarningJitterDuration)
        {
            Destroy(gameObject);
            return;
        }

        UpdateFinalWarningJitterOffset();
        transform.localPosition =
            originalLocalPosition + new Vector3(
                finalWarningJitterOffset.x,
                finalWarningJitterOffset.y,
                0f);
        transform.localScale = originalLocalScale * finalWarningScaleMultiplier;
        SetBlinkFactor(1f);
    }

    private void UpdateFinalWarningJitterOffset()
    {
        if (finalWarningJitterDistance <= 0f || finalWarningJitterFrequency <= 0f)
        {
            finalWarningJitterOffset = Vector2.zero;
            return;
        }

        finalWarningJitterSampleElapsed += Time.deltaTime;
        float sampleInterval = 1f / finalWarningJitterFrequency;
        if (finalWarningJitterSampleElapsed < sampleInterval)
        {
            return;
        }

        finalWarningJitterSampleElapsed = Mathf.Repeat(
            finalWarningJitterSampleElapsed,
            sampleInterval);
        finalWarningJitterOffset =
            Random.insideUnitCircle * finalWarningJitterDistance;
    }

    private void UpdateWarningJitterOffset()
    {
        if (warningJitterDistance <= 0f ||
            warningJitterFrequency <= 0f ||
            threatProgress <= 0f)
        {
            warningJitterOffset = Vector2.zero;
            warningJitterSampleElapsed = 0f;
            return;
        }

        warningJitterSampleElapsed += Time.deltaTime;
        float sampleInterval = 1f / warningJitterFrequency;
        if (warningJitterSampleElapsed < sampleInterval)
        {
            return;
        }

        warningJitterSampleElapsed = Mathf.Repeat(
            warningJitterSampleElapsed,
            sampleInterval);
        warningJitterOffset =
            Random.insideUnitCircle * warningJitterDistance * threatProgress;
    }

    private void CaptureOriginalTransform()
    {
        originalLocalPosition = transform.localPosition;
        originalLocalScale = transform.localScale;
    }

    private void ResolveRenderer()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponentInChildren<SpriteRenderer>();
        }
    }

    private void InitializeMaterial()
    {
        ResolveRenderer();
        if (targetRenderer == null)
        {
            return;
        }

        if (runtimeMaterial == null)
        {
            Material sourceMaterial = targetRenderer.sharedMaterial;
            if (sourceMaterial == null)
            {
                return;
            }

            runtimeMaterial = new Material(sourceMaterial);
            targetRenderer.material = runtimeMaterial;
        }

        string propertyName = string.IsNullOrWhiteSpace(blinkPropertyName)
            ? DefaultBlinkPropertyName
            : blinkPropertyName;
        blinkPropertyId = Shader.PropertyToID(propertyName);
        hasBlinkProperty = runtimeMaterial.HasProperty(blinkPropertyId);
        if (!hasBlinkProperty && !warnedMissingBlinkProperty)
        {
            Debug.LogWarning(
                $"[ProximityWarningIndicatorAnimator] Indicator material has no '{propertyName}' property.",
                this);
            warnedMissingBlinkProperty = true;
        }
    }

    private void SetBlinkFactor(float blinkFactor)
    {
        if (runtimeMaterial == null)
        {
            InitializeMaterial();
        }

        if (runtimeMaterial == null || !hasBlinkProperty)
        {
            return;
        }

        runtimeMaterial.SetFloat(blinkPropertyId, Mathf.Clamp01(blinkFactor));
    }
}
