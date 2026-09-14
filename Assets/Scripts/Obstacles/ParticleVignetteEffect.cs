using System.Collections;
using UnityEngine;

public class ParticleVignetteEffect : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [Header("References")]
    [SerializeField] private ParticleSystem vignetteParticles;
    [SerializeField] private ParticleSystemRenderer particleRenderer;

    [Header("Timing")]
    [Min(0f)] [SerializeField] private float fadeInDuration = 0.25f;
    [Min(0f)] [SerializeField] private float fadeOutDuration = 0.6f;

    private Coroutine fadeRoutine;
    private Material runtimeMaterial;
    private Color baseColor = Color.white;
    private int colorPropertyId;
    private float currentAlpha;
    private bool hasColorProperty;

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
        CreateRuntimeMaterial();
        HideImmediately();
    }

    private void OnValidate()
    {
        fadeInDuration = Mathf.Max(0f, fadeInDuration);
        fadeOutDuration = Mathf.Max(0f, fadeOutDuration);
    }

    private void OnDestroy()
    {
        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
        }
    }

    public void Show()
    {
        ResolveReferences();
        CreateRuntimeMaterial();

        if (runtimeMaterial == null || !hasColorProperty)
        {
            Debug.LogWarning("[ParticleVignetteEffect] Assign a particle material with _BaseColor or _Color before showing.", this);
            return;
        }

        StartFade(GetVisibleAlpha(), fadeInDuration, true);
    }

    public void Hide()
    {
        ResolveReferences();
        CreateRuntimeMaterial();

        if (runtimeMaterial == null || !hasColorProperty)
        {
            return;
        }

        StartFade(0f, fadeOutDuration, false);
    }

    private void StartFade(float targetAlpha, float duration, bool keepActiveAfterFade)
    {
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        SetParticleObjectActive(true);
        fadeRoutine = StartCoroutine(FadeRoutine(targetAlpha, duration, keepActiveAfterFade));
    }

    private IEnumerator FadeRoutine(float targetAlpha, float duration, bool keepActiveAfterFade)
    {
        float startAlpha = currentAlpha;

        if (duration <= 0f)
        {
            SetAlpha(targetAlpha);
            FinishFade(keepActiveAfterFade);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += GetDeltaTime();
            float progress = Mathf.Clamp01(elapsed / duration);
            SetAlpha(Mathf.Lerp(startAlpha, targetAlpha, progress));
            yield return null;
        }

        SetAlpha(targetAlpha);
        FinishFade(keepActiveAfterFade);
    }

    private void FinishFade(bool keepActive)
    {
        fadeRoutine = null;

        if (!keepActive)
        {
            SetParticleObjectActive(false);
        }
    }

    private void HideImmediately()
    {
        SetAlpha(0f);
        SetParticleObjectActive(false);
    }

    private void SetAlpha(float alpha)
    {
        if (runtimeMaterial == null || !hasColorProperty)
        {
            return;
        }

        currentAlpha = Mathf.Clamp01(alpha);

        Color color = baseColor;
        color.a = currentAlpha;
        runtimeMaterial.SetColor(colorPropertyId, color);
    }

    private void SetParticleObjectActive(bool active)
    {
        if (vignetteParticles == null)
        {
            return;
        }

        GameObject particleObject = vignetteParticles.gameObject;
        if (particleObject.activeSelf != active)
        {
            particleObject.SetActive(active);
        }
    }

    private float GetVisibleAlpha()
    {
        return baseColor.a;
    }

    private void CreateRuntimeMaterial()
    {
        if (runtimeMaterial != null || particleRenderer == null || particleRenderer.sharedMaterial == null)
        {
            return;
        }

        runtimeMaterial = new Material(particleRenderer.sharedMaterial);
        particleRenderer.material = runtimeMaterial;

        hasColorProperty = TryCacheColorProperty(BaseColorId) || TryCacheColorProperty(ColorId);
        if (hasColorProperty)
        {
            currentAlpha = baseColor.a;
        }
    }

    private bool TryCacheColorProperty(int propertyId)
    {
        if (!runtimeMaterial.HasProperty(propertyId))
        {
            return false;
        }

        colorPropertyId = propertyId;
        baseColor = runtimeMaterial.GetColor(propertyId);
        return true;
    }

    private float GetDeltaTime()
    {
        return Time.deltaTime;
    }

    private void ResolveReferences()
    {
        if (vignetteParticles == null)
        {
            vignetteParticles = GetComponentInChildren<ParticleSystem>(true);
        }

        if (particleRenderer == null && vignetteParticles != null)
        {
            particleRenderer = vignetteParticles.GetComponent<ParticleSystemRenderer>();
        }
    }
}
