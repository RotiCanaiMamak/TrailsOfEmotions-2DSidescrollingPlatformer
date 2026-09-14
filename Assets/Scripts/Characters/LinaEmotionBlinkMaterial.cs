using UnityEngine;

[DisallowMultipleComponent]
public sealed class LinaEmotionBlinkMaterial : MonoBehaviour
{
    private const string DefaultBlinkPropertyName = "_BlinkFactor";

    [Header("Renderer")]
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private string blinkPropertyName = DefaultBlinkPropertyName;

    [Header("Blink")]
    [Min(0f)] [SerializeField] private float resetDuration = 0.2f;

    private EmotionMeter subscribedMeter;
    private Material sourceMaterial;
    private Material runtimeMaterial;
    private int blinkPropertyId;
    private float blinkFactor;
    private bool hasBlinkProperty;
    private bool warnedMissingBlinkProperty;

    private void Awake()
    {
        ResolveRenderer();
        InitializeMaterial();
    }

    private void OnEnable()
    {
        ResolveRenderer();
        InitializeMaterial();
        SubscribeToEmotionMeter();
    }

    private void OnValidate()
    {
        resetDuration = Mathf.Max(0f, resetDuration);
    }

    private void Update()
    {
        if (subscribedMeter == null)
        {
            SubscribeToEmotionMeter();
        }

        if (blinkFactor <= 0f)
        {
            return;
        }

        float nextBlinkFactor = resetDuration <= 0f
            ? 0f
            : Mathf.MoveTowards(blinkFactor, 0f, Time.deltaTime / resetDuration);

        SetBlinkFactor(nextBlinkFactor);
    }

    private void OnDisable()
    {
        UnsubscribeFromEmotionMeter();
    }

    private void OnDestroy()
    {
        ReleaseRuntimeMaterial();
    }

    private void HandleObstacleEmotionAdded(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        SetBlinkFactor(1f);
    }

    private void ResolveRenderer()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponentInChildren<Renderer>();
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
                $"[LinaEmotionBlinkMaterial] Target material has no '{propertyName}' property.",
                this);
            warnedMissingBlinkProperty = true;
        }

        if (hasBlinkProperty)
        {
            runtimeMaterial.SetFloat(blinkPropertyId, blinkFactor);
        }
    }

    private void SubscribeToEmotionMeter()
    {
        EmotionMeter meter = EmotionMeter.Instance;
        if (subscribedMeter == meter)
        {
            return;
        }

        UnsubscribeFromEmotionMeter();
        subscribedMeter = meter;
        if (subscribedMeter != null)
        {
            subscribedMeter.onObstacleEmotionAdded.AddListener(HandleObstacleEmotionAdded);
        }
    }

    private void UnsubscribeFromEmotionMeter()
    {
        if (subscribedMeter == null)
        {
            return;
        }

        subscribedMeter.onObstacleEmotionAdded.RemoveListener(HandleObstacleEmotionAdded);
        subscribedMeter = null;
    }

    private void SetBlinkFactor(float value)
    {
        if (runtimeMaterial == null)
        {
            InitializeMaterial();
        }

        blinkFactor = Mathf.Clamp01(value);
        if (runtimeMaterial == null || !hasBlinkProperty)
        {
            return;
        }

        runtimeMaterial.SetFloat(blinkPropertyId, blinkFactor);
    }

    private void ReleaseRuntimeMaterial()
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        if (targetRenderer != null && targetRenderer.sharedMaterial == runtimeMaterial)
        {
            targetRenderer.sharedMaterial = sourceMaterial;
        }

        Destroy(runtimeMaterial);
        runtimeMaterial = null;
        sourceMaterial = null;
    }
}
