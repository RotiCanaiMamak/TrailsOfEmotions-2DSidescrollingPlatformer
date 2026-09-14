using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class ParticleStatusFadeEffect : MonoBehaviour
{
    private sealed class FadeTarget
    {
        public ParticleSystem particle;
        public ParticleSystemRenderer renderer;
        public Material originalMaterial;
        public Material runtimeMaterial;
        public Color baseColor = Color.white;
        public int colorPropertyId;
        public float currentAlpha;
        public bool hasColorProperty;
        public bool originalRendererEnabled;
        public bool originalObjectActive;
    }

    private static readonly int BaseColorId =
        Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId =
        Shader.PropertyToID("_Color");
    private static readonly int TintColorId =
        Shader.PropertyToID("_TintColor");

    private readonly List<FadeTarget> targets = new List<FadeTarget>();
    private readonly HashSet<ParticleSystem> uniqueParticles =
        new HashSet<ParticleSystem>();

    private Coroutine fadeRoutine;
    private bool warnedMissingRendererOrMaterial;
    private bool warnedMissingColorProperty;

    public void Configure(params ParticleSystem[] particles)
    {
        Release();
        CacheTargets(particles);
        CreateRuntimeMaterials();
        ClearImmediately();
    }

    public void Show(float duration)
    {
        StopFadeRoutine();
        CreateRuntimeMaterials();

        for (int i = 0; i < targets.Count; i++)
        {
            FadeTarget target = targets[i];
            SetTargetObjectActive(target, true);
            SetTargetRendererEnabled(target, true);

            if (target.particle != null && !target.particle.isEmitting)
            {
                target.particle.Play(true);
            }
        }

        WarnForUnsupportedTargets();
        if (HasFadeableTarget())
        {
            StartFade(true, duration);
        }
    }

    public void Hide(float duration)
    {
        StopFadeRoutine();

        for (int i = 0; i < targets.Count; i++)
        {
            FadeTarget target = targets[i];
            if (target.particle != null)
            {
                target.particle.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmitting);
            }

            if (!target.hasColorProperty)
            {
                SetTargetRendererEnabled(target, false);
            }
        }

        if (HasFadeableTarget())
        {
            StartFade(false, duration);
        }
        else
        {
            ClearImmediately();
        }
    }

    public void ClearImmediately()
    {
        StopFadeRoutine();
        SetAllFadeableAlphas(0f);

        for (int i = 0; i < targets.Count; i++)
        {
            FadeTarget target = targets[i];
            if (target.particle != null)
            {
                target.particle.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            SetTargetObjectActive(target, false);
        }
    }

    public void Release()
    {
        ClearImmediately();
        RestoreRuntimeMaterials();

        for (int i = 0; i < targets.Count; i++)
        {
            FadeTarget target = targets[i];
            if (target == null || target.particle == null)
            {
                continue;
            }

            GameObject particleObject = target.particle.gameObject;
            if (particleObject != gameObject &&
                particleObject.activeSelf != target.originalObjectActive)
            {
                particleObject.SetActive(target.originalObjectActive);
            }
        }

        targets.Clear();
        uniqueParticles.Clear();
    }

    private void OnDisable()
    {
        ClearImmediately();
        RestoreRuntimeMaterials();
    }

    private void OnDestroy()
    {
        Release();
    }

    private void CacheTargets(ParticleSystem[] particles)
    {
        if (particles == null)
        {
            return;
        }

        for (int i = 0; i < particles.Length; i++)
        {
            ParticleSystem particle = particles[i];
            if (particle == null || !uniqueParticles.Add(particle))
            {
                continue;
            }

            ParticleSystemRenderer renderer =
                particle.GetComponent<ParticleSystemRenderer>();
            targets.Add(new FadeTarget
            {
                particle = particle,
                renderer = renderer,
                originalMaterial =
                    renderer != null ? renderer.sharedMaterial : null,
                originalRendererEnabled =
                    renderer != null && renderer.enabled,
                originalObjectActive = particle.gameObject.activeSelf,
                currentAlpha = 0f,
            });
        }

        uniqueParticles.Clear();
    }

    private void CreateRuntimeMaterials()
    {
        for (int i = 0; i < targets.Count; i++)
        {
            FadeTarget target = targets[i];
            if (target == null ||
                target.runtimeMaterial != null ||
                target.renderer == null ||
                target.originalMaterial == null)
            {
                continue;
            }

            target.runtimeMaterial = new Material(target.originalMaterial);
            target.renderer.sharedMaterial = target.runtimeMaterial;
            target.hasColorProperty =
                TryCacheColorProperty(target, BaseColorId) ||
                TryCacheColorProperty(target, ColorId) ||
                TryCacheColorProperty(target, TintColorId);

            if (target.hasColorProperty)
            {
                SetAlpha(target, target.currentAlpha);
            }
        }
    }

    private static bool TryCacheColorProperty(
        FadeTarget target,
        int propertyId)
    {
        if (target.runtimeMaterial == null ||
            !target.runtimeMaterial.HasProperty(propertyId))
        {
            return false;
        }

        target.colorPropertyId = propertyId;
        target.baseColor =
            target.runtimeMaterial.GetColor(propertyId);
        return true;
    }

    private void StartFade(bool showing, float duration)
    {
        fadeRoutine = StartCoroutine(
            FadeRoutine(showing, Mathf.Max(0f, duration)));
    }

    private IEnumerator FadeRoutine(bool showing, float duration)
    {
        float[] startAlphas = GetCurrentAlphas();

        if (duration <= 0f)
        {
            SetFadeAlphas(showing, 1f, startAlphas);
            FinishFade(showing);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += GetDeltaTime();
            SetFadeAlphas(
                showing,
                Mathf.Clamp01(elapsed / duration),
                startAlphas);
            yield return null;
        }

        SetFadeAlphas(showing, 1f, startAlphas);
        FinishFade(showing);
    }

    private float[] GetCurrentAlphas()
    {
        float[] alphas = new float[targets.Count];
        for (int i = 0; i < targets.Count; i++)
        {
            FadeTarget target = targets[i];
            alphas[i] = target != null ? target.currentAlpha : 0f;
        }

        return alphas;
    }

    private void SetFadeAlphas(
        bool showing,
        float progress,
        float[] startAlphas)
    {
        for (int i = 0; i < targets.Count; i++)
        {
            FadeTarget target = targets[i];
            if (target == null || !target.hasColorProperty)
            {
                continue;
            }

            float targetAlpha = showing ? target.baseColor.a : 0f;
            float startAlpha =
                startAlphas != null && i < startAlphas.Length
                    ? startAlphas[i]
                    : target.currentAlpha;

            SetAlpha(
                target,
                Mathf.Lerp(startAlpha, targetAlpha, progress));
        }
    }

    private void FinishFade(bool showing)
    {
        fadeRoutine = null;
        if (!showing)
        {
            ClearImmediately();
        }
    }

    private void SetAllFadeableAlphas(float alpha)
    {
        for (int i = 0; i < targets.Count; i++)
        {
            FadeTarget target = targets[i];
            if (target != null && target.hasColorProperty)
            {
                SetAlpha(target, alpha);
            }
        }
    }

    private static void SetAlpha(FadeTarget target, float alpha)
    {
        if (target == null ||
            target.runtimeMaterial == null ||
            !target.hasColorProperty)
        {
            return;
        }

        target.currentAlpha = Mathf.Clamp01(alpha);
        Color color = target.baseColor;
        color.a = target.currentAlpha;
        target.runtimeMaterial.SetColor(target.colorPropertyId, color);
    }

    private void RestoreRuntimeMaterials()
    {
        for (int i = 0; i < targets.Count; i++)
        {
            FadeTarget target = targets[i];
            if (target == null)
            {
                continue;
            }

            if (target.renderer != null)
            {
                if (target.renderer.sharedMaterial ==
                    target.runtimeMaterial)
                {
                    target.renderer.sharedMaterial =
                        target.originalMaterial;
                }

                target.renderer.enabled =
                    target.originalRendererEnabled;
            }

            if (target.runtimeMaterial != null)
            {
                Destroy(target.runtimeMaterial);
            }

            target.runtimeMaterial = null;
            target.hasColorProperty = false;
            target.currentAlpha = 0f;
        }
    }

    private void SetTargetObjectActive(
        FadeTarget target,
        bool active)
    {
        if (target == null || target.particle == null)
        {
            return;
        }

        GameObject particleObject = target.particle.gameObject;
        if (particleObject == gameObject)
        {
            SetTargetRendererEnabled(target, active);
            return;
        }

        if (particleObject.activeSelf != active)
        {
            particleObject.SetActive(active);
        }
    }

    private static void SetTargetRendererEnabled(
        FadeTarget target,
        bool enabled)
    {
        if (target != null && target.renderer != null)
        {
            target.renderer.enabled =
                enabled ? target.originalRendererEnabled : false;
        }
    }

    private bool HasFadeableTarget()
    {
        for (int i = 0; i < targets.Count; i++)
        {
            FadeTarget target = targets[i];
            if (target != null && target.hasColorProperty)
            {
                return true;
            }
        }

        return false;
    }

    private void WarnForUnsupportedTargets()
    {
        bool missingRendererOrMaterial = false;
        bool missingColorProperty = false;

        for (int i = 0; i < targets.Count; i++)
        {
            FadeTarget target = targets[i];
            if (target == null)
            {
                continue;
            }

            if (target.renderer == null ||
                target.originalMaterial == null)
            {
                missingRendererOrMaterial = true;
            }
            else if (!target.hasColorProperty)
            {
                missingColorProperty = true;
            }
        }

        if (missingRendererOrMaterial &&
            !warnedMissingRendererOrMaterial)
        {
            Debug.LogWarning(
                "[ParticleStatusFadeEffect] One or more particles are missing a renderer or material and cannot fade.",
                this);
            warnedMissingRendererOrMaterial = true;
        }

        if (missingColorProperty && !warnedMissingColorProperty)
        {
            Debug.LogWarning(
                "[ParticleStatusFadeEffect] Particle materials need _BaseColor, _Color, or _TintColor for fade animation.",
                this);
            warnedMissingColorProperty = true;
        }
    }

    private void StopFadeRoutine()
    {
        if (fadeRoutine == null)
        {
            return;
        }

        StopCoroutine(fadeRoutine);
        fadeRoutine = null;
    }

    private float GetDeltaTime()
    {
        return Time.deltaTime;
    }
}
