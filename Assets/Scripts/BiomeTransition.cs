using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Drives a biome transition by flashing the assigned URP 2D Global Light.
/// This is separate from ScreenFader and does not depend on any UI overlay.
/// </summary>
public class BiomeTransition : MonoBehaviour
{
    public static BiomeTransition Instance { get; private set; }

    [Header("Light")]
    [SerializeField] private Light2D globalLight;
    [Min(0f)] public float flashIntensity = 3f;

    [Header("Timing")]
    [Min(0f)] public float fadeInDuration = 0.15f;
    [Min(0f)] public float holdFlashDuration = 0.1f;
    [Min(0f)] public float fadeOutDuration = 0.35f;

    private Coroutine runningTransition;

    public bool IsTransitioning => runningTransition != null;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void BeginBiomeTransition()
    {
        if (IsTransitioning)
        {
            return;
        }

        if (globalLight == null)
        {
            Debug.LogWarning("[BiomeTransition] No 2D Global Light assigned; the biome transition cannot run.", this);
            return;
        }

        runningTransition = StartCoroutine(RunBiomeTransition());
    }

    private IEnumerator RunBiomeTransition()
    {
        float originalIntensity = globalLight.intensity;

        yield return FadeLight(originalIntensity, flashIntensity, fadeInDuration);

        if (holdFlashDuration > 0f)
        {
            yield return new WaitForSeconds(holdFlashDuration);
        }

        if (TerrainManager.Instance != null)
        {
            TerrainManager.Instance.ExecuteBiomeTransitionReset();
        }
        else
        {
            Debug.LogWarning("[BiomeTransition] No TerrainManager found; the biome could not be swapped.", this);
        }

        yield return FadeLight(flashIntensity, originalIntensity, fadeOutDuration);

        runningTransition = null;
    }

    private IEnumerator FadeLight(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            SetLightIntensity(to);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetLightIntensity(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        SetLightIntensity(to);
    }

    private void SetLightIntensity(float intensity)
    {
        if (globalLight != null)
        {
            globalLight.intensity = intensity;
        }
    }
}
