using UnityEngine;

[DisallowMultipleComponent]
public sealed class EchoMaterialBlurPulse : MonoBehaviour
{
    private const string DefaultBlurPropertyName = "_BlurSize";

    [Header("Renderer")]
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private string blurPropertyName = DefaultBlurPropertyName;

    [Header("Blur Pulse")]
    [Min(0f)] [SerializeField] private float minBlur = 0.1f;
    [Min(0f)] [SerializeField] private float maxBlur = 0.7f;
    [Min(0.01f)] [SerializeField] private float cycleDuration = 1f;
    [Range(0f, 1f)] [SerializeField] private float phaseOffset;
    [SerializeField] private bool randomizeStartPhase = true;

    private Material sourceMaterial;
    private Material runtimeMaterial;
    private int blurPropertyId;
    private float elapsed;
    private bool hasBlurProperty;
    private bool warnedMissingRenderer;
    private bool warnedMissingMaterial;
    private bool warnedMissingBlurProperty;

    private void Awake()
    {
        ResolveRenderer();
        InitializeMaterial();
    }

    private void OnEnable()
    {
        ResolveRenderer();
        InitializeMaterial();
        elapsed = GetInitialElapsed();
    }

    private void Update()
    {
        if (runtimeMaterial == null)
        {
            InitializeMaterial();
        }

        if (runtimeMaterial == null || !hasBlurProperty)
        {
            return;
        }

        elapsed += Time.deltaTime;
        float cycleT = Mathf.Repeat(elapsed / Mathf.Max(0.01f, cycleDuration), 1f);
        float pulse = Mathf.PingPong(cycleT * 2f, 1f);
        float smoothedPulse = Mathf.SmoothStep(0f, 1f, pulse);
        runtimeMaterial.SetFloat(
            blurPropertyId,
            Mathf.Lerp(minBlur, maxBlur, smoothedPulse));
    }

    private void OnDisable()
    {
        SetBlur(minBlur);
    }

    private void OnDestroy()
    {
        ReleaseRuntimeMaterial();
    }

    private void OnValidate()
    {
        minBlur = Mathf.Max(0f, minBlur);
        maxBlur = Mathf.Max(minBlur, maxBlur);
        cycleDuration = Mathf.Max(0.01f, cycleDuration);
        phaseOffset = Mathf.Clamp01(phaseOffset);
    }

    private void ResolveRenderer()
    {
        if (targetRenderer != null)
        {
            return;
        }

        targetRenderer = GetComponentInChildren<Renderer>();
        if (targetRenderer == null)
        {
            WarnMissingRenderer();
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
            sourceMaterial = targetRenderer.sharedMaterial;
            if (sourceMaterial == null)
            {
                WarnMissingMaterial();
                return;
            }

            runtimeMaterial = new Material(sourceMaterial)
            {
                name = $"{sourceMaterial.name} (Echo Runtime)"
            };
            targetRenderer.material = runtimeMaterial;
        }

        string propertyName = string.IsNullOrWhiteSpace(blurPropertyName)
            ? DefaultBlurPropertyName
            : blurPropertyName;
        blurPropertyId = Shader.PropertyToID(propertyName);
        hasBlurProperty = runtimeMaterial.HasProperty(blurPropertyId);

        if (!hasBlurProperty)
        {
            WarnMissingBlurProperty(propertyName);
            return;
        }

        SetBlur(minBlur);
    }

    private float GetInitialElapsed()
    {
        float phase = randomizeStartPhase ? Random.value : phaseOffset;
        return Mathf.Clamp01(phase) * Mathf.Max(0.01f, cycleDuration);
    }

    private void SetBlur(float blur)
    {
        if (runtimeMaterial == null || !hasBlurProperty)
        {
            return;
        }

        runtimeMaterial.SetFloat(blurPropertyId, Mathf.Max(0f, blur));
    }

    private void ReleaseRuntimeMaterial()
    {
        if (targetRenderer != null &&
            runtimeMaterial != null &&
            targetRenderer.sharedMaterial == runtimeMaterial)
        {
            targetRenderer.sharedMaterial = sourceMaterial;
        }

        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
        }

        runtimeMaterial = null;
        sourceMaterial = null;
        hasBlurProperty = false;
    }

    private void WarnMissingRenderer()
    {
        if (warnedMissingRenderer)
        {
            return;
        }

        Debug.LogWarning("[EchoMaterialBlurPulse] Assign a target renderer.", this);
        warnedMissingRenderer = true;
    }

    private void WarnMissingMaterial()
    {
        if (warnedMissingMaterial)
        {
            return;
        }

        Debug.LogWarning("[EchoMaterialBlurPulse] Target renderer has no material.", this);
        warnedMissingMaterial = true;
    }

    private void WarnMissingBlurProperty(string propertyName)
    {
        if (warnedMissingBlurProperty)
        {
            return;
        }

        Debug.LogWarning(
            $"[EchoMaterialBlurPulse] Target material has no '{propertyName}' property.",
            this);
        warnedMissingBlurProperty = true;
    }
}
