using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerGlideVisualEffect : MonoBehaviour
{
    [Header("Core")]
    [SerializeField] private PlayerController player;

    [Header("Particles")]
    [SerializeField] private ParticleSystem glideParticleSystem;

    [Header("Cloud")]
    [SerializeField] private Transform cloudRoot;
    [SerializeField] private SpriteRenderer[] cloudRenderers;
    [SerializeField] private Vector3 cloudTargetScale = Vector3.one;
    [Min(0f)] [SerializeField] private float cloudAppearDuration = 0.18f;
    [SerializeField] private AnimationCurve cloudFadeCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private Color[] cloudRendererBaseColors;
    private Vector3 cloudBaseScale = Vector3.one;
    private bool wasGliding;
    private bool cloudDefaultsCached;
    private float cloudVisibilityProgress;

    private void Awake()
    {
        ResolveReferences();
        CacheCloudDefaults();
        ResetCloud();
    }

    private void OnEnable()
    {
        ResolveReferences();
        CacheCloudDefaults();
        ApplyGlideState(true);
    }

    private void Update()
    {
        ResolveReferences();
        CacheCloudDefaults();
        ApplyGlideState(false);
        UpdateCloudAnimation();
    }

    private void OnDisable()
    {
        StopGlideParticles();
        ResetCloud();
        wasGliding = false;
    }

    private void OnValidate()
    {
        if (cloudAppearDuration < 0f)
        {
            cloudAppearDuration = 0f;
        }

        if (cloudFadeCurve == null)
        {
            cloudFadeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }
    }

    private void ResolveReferences()
    {
        if (player == null)
        {
            player = GetComponent<PlayerController>();
        }

        if (player == null)
        {
            player = GetComponentInParent<PlayerController>();
        }

        if (cloudRoot == null && cloudRenderers != null && cloudRenderers.Length > 0)
        {
            for (int i = 0; i < cloudRenderers.Length; i++)
            {
                if (cloudRenderers[i] != null)
                {
                    cloudRoot = cloudRenderers[i].transform;
                    break;
                }
            }
        }

        if (cloudRoot != null && (cloudRenderers == null || cloudRenderers.Length == 0))
        {
            cloudRenderers = cloudRoot.GetComponentsInChildren<SpriteRenderer>(true);
            cloudDefaultsCached = false;
        }
    }

    private void CacheCloudDefaults()
    {
        if (cloudDefaultsCached || cloudRoot == null)
        {
            return;
        }

        cloudBaseScale = cloudRoot.localScale;

        if (cloudRenderers == null || cloudRenderers.Length == 0)
        {
            cloudRendererBaseColors = new Color[0];
            cloudDefaultsCached = true;
            return;
        }

        cloudRendererBaseColors = new Color[cloudRenderers.Length];
        for (int i = 0; i < cloudRenderers.Length; i++)
        {
            cloudRendererBaseColors[i] = cloudRenderers[i] != null
                ? cloudRenderers[i].color
                : Color.white;
        }

        cloudDefaultsCached = true;
    }

    private void ApplyGlideState(bool force)
    {
        bool isGliding = player != null && player.IsGliding;
        if (!force && isGliding == wasGliding)
        {
            return;
        }

        wasGliding = isGliding;
        if (isGliding)
        {
            StartGlideEffects();
        }
        else
        {
            StopGlideEffects();
        }
    }

    private void StartGlideEffects()
    {
        if (glideParticleSystem != null && !glideParticleSystem.isPlaying)
        {
            glideParticleSystem.Play(true);
        }

        StartCloud();
    }

    private void StopGlideEffects()
    {
        StopGlideParticles();
    }

    private void StopGlideParticles()
    {
        if (glideParticleSystem == null)
        {
            return;
        }

        glideParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private void StartCloud()
    {
        if (cloudRoot == null)
        {
            return;
        }

        CacheCloudDefaults();
        if (!cloudRoot.gameObject.activeSelf)
        {
            cloudRoot.gameObject.SetActive(true);
        }

        UpdateCloudAnimation();
    }

    private void UpdateCloudAnimation()
    {
        if (cloudRoot == null)
        {
            return;
        }

        float targetProgress = wasGliding ? 1f : 0f;
        if (cloudAppearDuration <= 0f)
        {
            cloudVisibilityProgress = targetProgress;
        }
        else
        {
            cloudVisibilityProgress = Mathf.MoveTowards(
                cloudVisibilityProgress,
                targetProgress,
                Time.deltaTime / cloudAppearDuration);
        }

        if (cloudVisibilityProgress <= 0f && !wasGliding)
        {
            ResetCloud();
            return;
        }

        if (!cloudRoot.gameObject.activeSelf)
        {
            cloudRoot.gameObject.SetActive(true);
        }

        ApplyCloudVisual(cloudVisibilityProgress);
    }

    private void ApplyCloudVisual(float progress)
    {
        float easedProgress = cloudFadeCurve != null
            ? cloudFadeCurve.Evaluate(progress)
            : progress;

        Vector3 targetScale = Vector3.Scale(cloudBaseScale, cloudTargetScale);
        cloudRoot.localScale = Vector3.LerpUnclamped(Vector3.zero, targetScale, easedProgress);
        SetCloudAlpha(easedProgress);
    }

    private void ResetCloud()
    {
        if (cloudRoot == null)
        {
            return;
        }

        CacheCloudDefaults();
        cloudVisibilityProgress = 0f;
        cloudRoot.localScale = cloudBaseScale;
        SetCloudAlpha(0f);
        cloudRoot.gameObject.SetActive(false);
    }

    private void SetCloudAlpha(float alphaMultiplier)
    {
        if (cloudRenderers == null || cloudRendererBaseColors == null)
        {
            return;
        }

        int count = Mathf.Min(cloudRenderers.Length, cloudRendererBaseColors.Length);
        for (int i = 0; i < count; i++)
        {
            SpriteRenderer cloudRenderer = cloudRenderers[i];
            if (cloudRenderer == null)
            {
                continue;
            }

            Color color = cloudRendererBaseColors[i];
            color.a *= Mathf.Clamp01(alphaMultiplier);
            cloudRenderer.color = color;
        }
    }
}
