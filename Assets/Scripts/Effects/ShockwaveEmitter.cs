using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ShockwaveEmitter : MonoBehaviour
{
    private static readonly int WaveDistanceFromCenterId = Shader.PropertyToID("_WaveDistanceFromCenter");
    private static readonly int ShockWaveStrengthId = Shader.PropertyToID("_ShockWaveStrength");
    private static readonly int RingSpawnPositionId = Shader.PropertyToID("_RingSpawnPosition");
    private static readonly int SizeId = Shader.PropertyToID("_Size");
    private static readonly int XSizeRatioId = Shader.PropertyToID("_XSizeRatio");

    [Header("Target")]
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private Graphic targetGraphic;
    [SerializeField] private bool playOnEnable = true;

    [Header("Wave")]
    [Min(0f)]
    [SerializeField] private float duration = 0.6f;
    [SerializeField] private float strength = -0.1f;
    [SerializeField] private float size = 0.05f;
    [SerializeField] private Vector2 ringSpawnPosition = new Vector2(0.5f, 0.5f);
    [SerializeField] private float xSizeRatio = 1.777f;
    [SerializeField] private AnimationCurve strengthCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    private MaterialPropertyBlock propertyBlock;
    private Material runtimeGraphicMaterial;
    private Coroutine runningWave;

    private void Reset()
    {
        targetRenderer = GetComponent<Renderer>();
        targetGraphic = GetComponent<Graphic>();

        Camera camera = Camera.main;
        if (camera != null)
        {
            xSizeRatio = (float)Screen.width / Screen.height;
        }
    }

    private void Awake()
    {
        targetRenderer ??= GetComponent<Renderer>();
        targetGraphic ??= GetComponent<Graphic>();

        if (targetGraphic != null && targetGraphic.material != null)
        {
            runtimeGraphicMaterial = new Material(targetGraphic.material);
            targetGraphic.material = runtimeGraphicMaterial;
        }

        SetNeutral();
    }

    private void OnEnable()
    {
        if (playOnEnable)
        {
            Play();
        }
    }

    private void OnDisable()
    {
        if (runningWave != null)
        {
            StopCoroutine(runningWave);
            runningWave = null;
        }

        SetNeutral();
    }

    private void OnDestroy()
    {
        if (runtimeGraphicMaterial != null)
        {
            Destroy(runtimeGraphicMaterial);
        }
    }

    public void Play()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (runningWave != null)
        {
            StopCoroutine(runningWave);
            runningWave = null;
        }

        ApplyWave(0f, strength * strengthCurve.Evaluate(0f));

        if (duration <= 0f)
        {
            SetNeutral();
            return;
        }

        runningWave = StartCoroutine(PlayWave(duration));
    }

    public void PlayAtScreenPosition(Vector2 normalizedScreenPosition)
    {
        ringSpawnPosition = normalizedScreenPosition;
        Play();
    }

    public void PlayAtWorldPosition(Vector3 worldPosition, Camera camera)
    {
        if (camera == null)
        {
            camera = Camera.main;
        }

        if (camera == null)
        {
            Play();
            return;
        }

        Vector3 viewportPosition = camera.WorldToViewportPoint(worldPosition);
        PlayAtScreenPosition(new Vector2(viewportPosition.x, viewportPosition.y));
    }

    private IEnumerator PlayWave(float waveDuration)
    {
        float elapsed = 0f;

        while (elapsed < waveDuration)
        {
            // Keep the initial value at zero until the next frame.
            yield return null;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / waveDuration);
            float strengthT = strengthCurve.Evaluate(t);

            ApplyWave(
                Mathf.Lerp(0f, 1f, t),
                strength * strengthT);
        }

        ApplyWave(1f, 0f);
        runningWave = null;
    }

    private void SetNeutral()
    {
        ApplyWave(1f, 0f);
    }

    private void ApplyWave(float waveDistance, float waveStrength)
    {
        if (targetRenderer != null)
        {
            propertyBlock ??= new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(WaveDistanceFromCenterId, waveDistance);
            propertyBlock.SetFloat(ShockWaveStrengthId, waveStrength);
            propertyBlock.SetVector(RingSpawnPositionId, ringSpawnPosition);
            propertyBlock.SetFloat(SizeId, size);
            propertyBlock.SetFloat(XSizeRatioId, xSizeRatio);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }

        Material graphicMaterial = runtimeGraphicMaterial != null ? runtimeGraphicMaterial : targetGraphic != null ? targetGraphic.material : null;
        if (graphicMaterial != null)
        {
            graphicMaterial.SetFloat(WaveDistanceFromCenterId, waveDistance);
            graphicMaterial.SetFloat(ShockWaveStrengthId, waveStrength);
            graphicMaterial.SetVector(RingSpawnPositionId, ringSpawnPosition);
            graphicMaterial.SetFloat(SizeId, size);
            graphicMaterial.SetFloat(XSizeRatioId, xSizeRatio);

            if (targetGraphic != null)
            {
                targetGraphic.SetMaterialDirty();
            }
        }
    }
}
