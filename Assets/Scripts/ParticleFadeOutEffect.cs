using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ParticleFadeOutEffect : MonoBehaviour
{
    private sealed class FadeTarget
    {
        public ParticleSystem particle;
        public ParticleSystemRenderer renderer;
        public Material originalMaterial;
        public Material runtimeMaterial;
        public int colorPropertyId;
        public Color startColor;
        public ParticleSystem.Particle[] particleBuffer;
        public Dictionary<uint, byte> startAlphaByRandomSeed;
    }

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int TintColorId = Shader.PropertyToID("_TintColor");

    private readonly List<FadeTarget> fadeTargets = new List<FadeTarget>();
    private readonly HashSet<ParticleSystem> uniqueParticles = new HashSet<ParticleSystem>();
    private Coroutine fadeRoutine;
    private bool warnedMissingRendererOrMaterial;

    public void Play(float duration, params ParticleSystem[] particles)
    {
        CancelFade(false);
        CacheFadeTargets(particles);

        if (fadeTargets.Count == 0)
        {
            return;
        }

        fadeRoutine = StartCoroutine(FadeRoutine(Mathf.Max(0f, duration)));
    }

    private void OnDisable()
    {
        CancelFade(true);
    }

    private void OnDestroy()
    {
        CancelFade(true);
    }

    private IEnumerator FadeRoutine(float duration)
    {
        if (duration <= 0f)
        {
            SetAlpha(0f);
            FinishFade();
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetAlpha(1f - Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        SetAlpha(0f);
        FinishFade();
    }

    private void CacheFadeTargets(ParticleSystem[] particles)
    {
        uniqueParticles.Clear();
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

            particle.Stop(false, ParticleSystemStopBehavior.StopEmitting);

            ParticleSystemRenderer particleRenderer = particle.GetComponent<ParticleSystemRenderer>();
            Material sharedMaterial = particleRenderer != null ? particleRenderer.sharedMaterial : null;
            if (particleRenderer == null || sharedMaterial == null)
            {
                HideImmediately(particle, particleRenderer);
                WarnMissingRendererOrMaterial();
                continue;
            }

            Material runtimeMaterial = new Material(sharedMaterial);
            if (!TryGetColorProperty(runtimeMaterial, out int colorPropertyId))
            {
                Destroy(runtimeMaterial);
                particleRenderer.enabled = true;
                fadeTargets.Add(CreateParticleColorFadeTarget(
                    particle,
                    particleRenderer,
                    sharedMaterial));
                continue;
            }

            particleRenderer.sharedMaterial = runtimeMaterial;
            particleRenderer.enabled = true;

            fadeTargets.Add(new FadeTarget
            {
                particle = particle,
                renderer = particleRenderer,
                originalMaterial = sharedMaterial,
                runtimeMaterial = runtimeMaterial,
                colorPropertyId = colorPropertyId,
                startColor = runtimeMaterial.GetColor(colorPropertyId),
            });
        }

        uniqueParticles.Clear();
    }

    private static FadeTarget CreateParticleColorFadeTarget(
        ParticleSystem particle,
        ParticleSystemRenderer particleRenderer,
        Material sharedMaterial)
    {
        int particleCount = particle.particleCount;
        ParticleSystem.Particle[] particleBuffer =
            new ParticleSystem.Particle[Mathf.Max(1, particleCount)];
        int liveParticleCount = particle.GetParticles(particleBuffer);
        Dictionary<uint, byte> startAlphaByRandomSeed =
            new Dictionary<uint, byte>(liveParticleCount);

        for (int i = 0; i < liveParticleCount; i++)
        {
            ParticleSystem.Particle liveParticle = particleBuffer[i];
            startAlphaByRandomSeed[liveParticle.randomSeed] =
                liveParticle.startColor.a;
        }

        return new FadeTarget
        {
            particle = particle,
            renderer = particleRenderer,
            originalMaterial = sharedMaterial,
            particleBuffer = particleBuffer,
            startAlphaByRandomSeed = startAlphaByRandomSeed,
        };
    }

    private static bool TryGetColorProperty(Material material, out int colorPropertyId)
    {
        if (material.HasProperty(BaseColorId))
        {
            colorPropertyId = BaseColorId;
            return true;
        }

        if (material.HasProperty(ColorId))
        {
            colorPropertyId = ColorId;
            return true;
        }

        if (material.HasProperty(TintColorId))
        {
            colorPropertyId = TintColorId;
            return true;
        }

        colorPropertyId = 0;
        return false;
    }

    private void SetAlpha(float alphaMultiplier)
    {
        float clampedMultiplier = Mathf.Clamp01(alphaMultiplier);
        for (int i = 0; i < fadeTargets.Count; i++)
        {
            FadeTarget target = fadeTargets[i];
            if (target == null)
            {
                continue;
            }

            if (target.runtimeMaterial != null)
            {
                Color color = target.startColor;
                color.a = target.startColor.a * clampedMultiplier;
                target.runtimeMaterial.SetColor(target.colorPropertyId, color);
                continue;
            }

            SetParticleAlpha(target, clampedMultiplier);
        }
    }

    private static void SetParticleAlpha(FadeTarget target, float alphaMultiplier)
    {
        if (target.particle == null ||
            target.startAlphaByRandomSeed == null)
        {
            return;
        }

        int particleCount = target.particle.particleCount;
        if (target.particleBuffer == null ||
            target.particleBuffer.Length < particleCount)
        {
            target.particleBuffer =
                new ParticleSystem.Particle[Mathf.Max(1, particleCount)];
        }

        int liveParticleCount =
            target.particle.GetParticles(target.particleBuffer);
        for (int i = 0; i < liveParticleCount; i++)
        {
            ParticleSystem.Particle liveParticle = target.particleBuffer[i];
            if (!target.startAlphaByRandomSeed.TryGetValue(
                    liveParticle.randomSeed,
                    out byte startAlpha))
            {
                startAlpha = liveParticle.startColor.a;
                target.startAlphaByRandomSeed[liveParticle.randomSeed] =
                    startAlpha;
            }

            Color32 particleColor = liveParticle.startColor;
            particleColor.a = (byte)Mathf.RoundToInt(
                startAlpha * alphaMultiplier);
            liveParticle.startColor = particleColor;
            target.particleBuffer[i] = liveParticle;
        }

        target.particle.SetParticles(
            target.particleBuffer,
            liveParticleCount);
    }

    private void FinishFade()
    {
        fadeRoutine = null;
        ReleaseTargets(true);
    }

    private void CancelFade(bool hideParticles)
    {
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }

        ReleaseTargets(hideParticles);
    }

    private void ReleaseTargets(bool hideParticles)
    {
        for (int i = 0; i < fadeTargets.Count; i++)
        {
            FadeTarget target = fadeTargets[i];
            if (target == null)
            {
                continue;
            }

            if (hideParticles && target.particle != null)
            {
                target.particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            if (target.renderer != null)
            {
                if (target.renderer.sharedMaterial == target.runtimeMaterial)
                {
                    target.renderer.sharedMaterial = target.originalMaterial;
                }

                if (hideParticles)
                {
                    target.renderer.enabled = false;
                }
            }

            if (target.runtimeMaterial != null)
            {
                Destroy(target.runtimeMaterial);
            }
        }

        fadeTargets.Clear();
        uniqueParticles.Clear();
    }

    private static void HideImmediately(
        ParticleSystem particle,
        ParticleSystemRenderer particleRenderer)
    {
        if (particle != null)
        {
            particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (particleRenderer != null)
        {
            particleRenderer.enabled = false;
        }
    }

    private void WarnMissingRendererOrMaterial()
    {
        if (warnedMissingRendererOrMaterial)
        {
            return;
        }

        Debug.LogWarning(
            "[ParticleFadeOutEffect] One or more particle systems are missing a renderer or material. Those particles were hidden immediately.",
            this);
        warnedMissingRendererOrMaterial = true;
    }
}
