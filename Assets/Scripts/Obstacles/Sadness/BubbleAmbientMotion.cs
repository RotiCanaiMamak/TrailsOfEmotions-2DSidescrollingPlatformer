using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BubbleAmbientMotion : MonoBehaviour
{
    private const float MinimumFrequency = 0.01f;

    [Header("Particle")]
    [Tooltip("Assign the main enclosing bubble ParticleSystem. No automatic lookup is performed.")]
    [SerializeField] private ParticleSystem particleTarget;

    [Header("Breathing")]
    [Tooltip("Uniform size change applied while the bubble breathes.")]
    [Range(0f, 0.25f)] [SerializeField] private float breathingAmount = 0.03f;
    [Tooltip("Number of breathing cycles per second.")]
    [Min(MinimumFrequency)] [SerializeField] private float breathingFrequency = 0.35f;

    [Header("Squash And Stretch")]
    [Tooltip("Alternating horizontal and vertical deformation applied to the bubble.")]
    [Range(0f, 0.25f)] [SerializeField] private float squashAmount = 0.08f;
    [Tooltip("Number of squash-and-stretch cycles per second.")]
    [Min(MinimumFrequency)] [SerializeField] private float squashFrequency = 0.55f;

    private readonly Dictionary<uint, Vector3> baselineSizes =
        new Dictionary<uint, Vector3>();
    private readonly HashSet<uint> liveParticleSeeds =
        new HashSet<uint>();
    private readonly List<uint> expiredParticleSeeds =
        new List<uint>();

    private ParticleSystem.Particle[] particleBuffer;
    private ParticleSystem capturedTarget;
    private bool originalStartSize3D;
    private bool hasCapturedTarget;
    private bool warnedMissingParticleTarget;
    private float animationTime;
    private float growStartScale = 1f;
    private float growDuration;
    private float growElapsed;
    private float growMultiplier = 1f;
    private bool isGrowing;

    private void OnEnable()
    {
        animationTime = 0f;
        PrepareParticleTarget();
    }

    private void OnDisable()
    {
        RestoreCapturedTarget();
        ResetGrowState();
    }

    private void OnValidate()
    {
        breathingAmount = Mathf.Clamp(breathingAmount, 0f, 0.25f);
        breathingFrequency = Mathf.Max(MinimumFrequency, breathingFrequency);
        squashAmount = Mathf.Clamp(squashAmount, 0f, 0.25f);
        squashFrequency = Mathf.Max(MinimumFrequency, squashFrequency);
    }

    private void LateUpdate()
    {
        if (particleTarget == null)
        {
            WarnMissingParticleTarget();
            return;
        }

        if (!hasCapturedTarget || capturedTarget != particleTarget)
        {
            RestoreCapturedTarget();
            PrepareParticleTarget();
        }

        if (!hasCapturedTarget)
        {
            return;
        }

        animationTime += Time.deltaTime;
        UpdateGrowIn();
        AnimateParticles();
    }

    public bool PlayGrowIn(float startScale = 0.1f, float duration = 0.35f)
    {
        if (particleTarget == null)
        {
            WarnMissingParticleTarget();
            return false;
        }

        if (!particleTarget.gameObject.activeSelf)
        {
            particleTarget.gameObject.SetActive(true);
        }

        if (!hasCapturedTarget || capturedTarget != particleTarget)
        {
            RestoreCapturedTarget();
            PrepareParticleTarget();
        }

        if (!hasCapturedTarget)
        {
            return false;
        }

        particleTarget.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear);
        ClearParticleBaselines();

        growStartScale = Mathf.Clamp01(startScale);
        growDuration = Mathf.Max(0f, duration);
        growElapsed = 0f;
        growMultiplier = growDuration <= 0f ? 1f : growStartScale;
        isGrowing = growDuration > 0f && growStartScale < 1f;

        particleTarget.Play(true);
        return true;
    }

    public void StopImmediately()
    {
        if (particleTarget == null)
        {
            WarnMissingParticleTarget();
            return;
        }

        particleTarget.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear);
        ClearParticleBaselines();
        ResetGrowState();
    }

    private void PrepareParticleTarget()
    {
        if (particleTarget == null)
        {
            WarnMissingParticleTarget();
            return;
        }

        capturedTarget = particleTarget;
        ParticleSystem.MainModule main = capturedTarget.main;
        originalStartSize3D = main.startSize3D;
        main.startSize3D = true;

        baselineSizes.Clear();
        liveParticleSeeds.Clear();
        expiredParticleSeeds.Clear();
        EnsureParticleBuffer();

        hasCapturedTarget = true;
        warnedMissingParticleTarget = false;
    }

    private void AnimateParticles()
    {
        EnsureParticleBuffer();
        if (particleBuffer == null || particleBuffer.Length == 0)
        {
            return;
        }

        int particleCount = capturedTarget.GetParticles(particleBuffer);
        liveParticleSeeds.Clear();

        float breathingMultiplier = 1f +
            Mathf.Sin(animationTime * Mathf.PI * 2f * breathingFrequency) *
            breathingAmount;
        float squashOffset =
            Mathf.Sin(animationTime * Mathf.PI * 2f * squashFrequency) *
            squashAmount;
        float horizontalMultiplier = breathingMultiplier * (1f + squashOffset);
        float verticalMultiplier = breathingMultiplier * (1f - squashOffset);
        horizontalMultiplier *= growMultiplier;
        verticalMultiplier *= growMultiplier;

        for (int i = 0; i < particleCount; i++)
        {
            ParticleSystem.Particle particle = particleBuffer[i];
            uint seed = particle.randomSeed;
            liveParticleSeeds.Add(seed);

            if (!baselineSizes.TryGetValue(seed, out Vector3 baselineSize))
            {
                baselineSize = originalStartSize3D
                    ? particle.startSize3D
                    : Vector3.one * particle.startSize;
                baselineSizes.Add(seed, baselineSize);
            }

            particle.startSize3D = new Vector3(
                baselineSize.x * horizontalMultiplier,
                baselineSize.y * verticalMultiplier,
                baselineSize.z);
            particleBuffer[i] = particle;
        }

        if (particleCount > 0)
        {
            capturedTarget.SetParticles(particleBuffer, particleCount);
        }

        RemoveExpiredParticleBaselines();
    }

    private void UpdateGrowIn()
    {
        if (!isGrowing)
        {
            growMultiplier = 1f;
            return;
        }

        growElapsed += Time.deltaTime;
        float progress = growDuration <= 0f
            ? 1f
            : Mathf.Clamp01(growElapsed / growDuration);
        float easedProgress = progress * progress * (3f - (2f * progress));
        growMultiplier = Mathf.Lerp(growStartScale, 1f, easedProgress);

        if (progress >= 1f)
        {
            growMultiplier = 1f;
            isGrowing = false;
        }
    }

    private void RestoreCapturedTarget()
    {
        if (!hasCapturedTarget || capturedTarget == null)
        {
            ClearCapturedState();
            return;
        }

        RestoreLiveParticleSizes();

        ParticleSystem.MainModule main = capturedTarget.main;
        main.startSize3D = originalStartSize3D;
        ClearCapturedState();
    }

    private void RestoreLiveParticleSizes()
    {
        EnsureParticleBuffer();
        if (particleBuffer == null || particleBuffer.Length == 0)
        {
            return;
        }

        int particleCount = capturedTarget.GetParticles(particleBuffer);
        bool changedAnyParticle = false;

        for (int i = 0; i < particleCount; i++)
        {
            ParticleSystem.Particle particle = particleBuffer[i];
            if (!baselineSizes.TryGetValue(particle.randomSeed, out Vector3 baselineSize))
            {
                continue;
            }

            particle.startSize3D = baselineSize;
            particleBuffer[i] = particle;
            changedAnyParticle = true;
        }

        if (changedAnyParticle)
        {
            capturedTarget.SetParticles(particleBuffer, particleCount);
        }
    }

    private void RemoveExpiredParticleBaselines()
    {
        expiredParticleSeeds.Clear();

        foreach (uint seed in baselineSizes.Keys)
        {
            if (!liveParticleSeeds.Contains(seed))
            {
                expiredParticleSeeds.Add(seed);
            }
        }

        for (int i = 0; i < expiredParticleSeeds.Count; i++)
        {
            baselineSizes.Remove(expiredParticleSeeds[i]);
        }
    }

    private void EnsureParticleBuffer()
    {
        if (capturedTarget == null)
        {
            return;
        }

        int requiredCapacity = Mathf.Max(1, capturedTarget.main.maxParticles);
        if (particleBuffer == null || particleBuffer.Length < requiredCapacity)
        {
            particleBuffer = new ParticleSystem.Particle[requiredCapacity];
        }
    }

    private void ClearCapturedState()
    {
        ClearParticleBaselines();
        capturedTarget = null;
        hasCapturedTarget = false;
    }

    private void ClearParticleBaselines()
    {
        baselineSizes.Clear();
        liveParticleSeeds.Clear();
        expiredParticleSeeds.Clear();
    }

    private void ResetGrowState()
    {
        growStartScale = 1f;
        growDuration = 0f;
        growElapsed = 0f;
        growMultiplier = 1f;
        isGrowing = false;
    }

    private void WarnMissingParticleTarget()
    {
        if (warnedMissingParticleTarget)
        {
            return;
        }

        Debug.LogWarning(
            $"[{nameof(BubbleAmbientMotion)}] Assign the main bubble ParticleSystem to Particle Target.",
            this);
        warnedMissingParticleTarget = true;
    }
}
