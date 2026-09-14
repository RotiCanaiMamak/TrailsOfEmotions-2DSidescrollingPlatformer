using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class SadnessParticleController : MonoBehaviour
{
    [Serializable]
    public struct EmissionTarget
    {
        [Min(0f)] public float particleSystem1;
        [Min(0f)] public float particleSystem2;
        [Min(0f)] public float particleSystem3;

        public EmissionTarget(float particleSystem1, float particleSystem2, float particleSystem3)
        {
            this.particleSystem1 = particleSystem1;
            this.particleSystem2 = particleSystem2;
            this.particleSystem3 = particleSystem3;
        }
    }

    [Header("Particle Systems")]
    [SerializeField] private ParticleSystem particleSystem1;
    [SerializeField] private ParticleSystem particleSystem2;
    [SerializeField] private ParticleSystem particleSystem3;

    [Header("Biome")]
    [SerializeField] private string sadnessFamilyId = "Sadness";

    [Header("Emission Targets")]
    [SerializeField] private EmissionTarget preTarget = new EmissionTarget(10f, 10f, 10f);
    [SerializeField] private EmissionTarget midTarget = new EmissionTarget(25f, 25f, 25f);
    [SerializeField] private EmissionTarget peakedTarget = new EmissionTarget(50f, 50f, 50f);

    [Header("Timing")]
    [Min(0f)] [SerializeField] private float lerpDuration = 1f;

    private BiomeManager biomeManager;
    private Coroutine runningLerp;
    private bool subscribedToBiomeManager;
    private bool hasAppliedTarget;
    private EmissionTarget lastAppliedTarget;

    private void OnEnable()
    {
        TrySubscribeToBiomeManager();
        ApplyCurrentState(true);
    }

    private void Start()
    {
        if (!TrySubscribeToBiomeManager())
        {
            Debug.LogWarning("[SadnessParticleController] No BiomeManager instance found.", this);
            return;
        }

        ApplyCurrentState(true);
    }

    private void OnDisable()
    {
        StopRunningLerp();
        UnsubscribeFromBiomeManager();
    }

    private void OnDestroy()
    {
        UnsubscribeFromBiomeManager();
    }

    private void OnValidate()
    {
        preTarget = ClampTarget(preTarget);
        midTarget = ClampTarget(midTarget);
        peakedTarget = ClampTarget(peakedTarget);
        lerpDuration = Mathf.Max(0f, lerpDuration);
    }

    private void OnBiomeChanged(BiomeData biome)
    {
        ApplyCurrentState(false);
    }

    private void OnBiomePhaseChanged(int phaseIndex)
    {
        ApplyCurrentState(false);
    }

    private void ApplyCurrentState(bool instant)
    {
        if (!TryGetCurrentTarget(out EmissionTarget target))
        {
            StopRunningLerp();
            hasAppliedTarget = false;
            return;
        }

        target = ClampTarget(target);
        if (!instant && hasAppliedTarget && ApproximatelySame(lastAppliedTarget, target))
        {
            return;
        }

        StopRunningLerp();
        if (instant || lerpDuration <= 0f)
        {
            SetEmissionRates(target);
        }
        else
        {
            runningLerp = StartCoroutine(LerpEmissionRates(target));
        }

        lastAppliedTarget = target;
        hasAppliedTarget = true;
    }

    private IEnumerator LerpEmissionRates(EmissionTarget target)
    {
        EmissionTarget start = new EmissionTarget(
            GetEmissionRate(particleSystem1),
            GetEmissionRate(particleSystem2),
            GetEmissionRate(particleSystem3));
        float elapsed = 0f;

        while (elapsed < lerpDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / lerpDuration);
            SetEmissionRates(new EmissionTarget(
                Mathf.Lerp(start.particleSystem1, target.particleSystem1, progress),
                Mathf.Lerp(start.particleSystem2, target.particleSystem2, progress),
                Mathf.Lerp(start.particleSystem3, target.particleSystem3, progress)));
            yield return null;
        }

        SetEmissionRates(target);
        runningLerp = null;
    }

    private void SetEmissionRates(EmissionTarget target)
    {
        SetEmissionRate(particleSystem1, target.particleSystem1);
        SetEmissionRate(particleSystem2, target.particleSystem2);
        SetEmissionRate(particleSystem3, target.particleSystem3);
    }

    private static float GetEmissionRate(ParticleSystem particles)
    {
        if (particles == null)
        {
            return 0f;
        }

        ParticleSystem.EmissionModule emission = particles.emission;
        return emission.rateOverTime.constant;
    }

    private static void SetEmissionRate(ParticleSystem particles, float rate)
    {
        if (particles == null)
        {
            return;
        }

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = Mathf.Max(0f, rate);
    }

    private bool TryGetCurrentTarget(out EmissionTarget target)
    {
        target = default;

        biomeManager = BiomeManager.Instance != null ? BiomeManager.Instance : biomeManager;
        if (biomeManager == null)
        {
            return false;
        }

        biomeManager.EnsureInitialized();
        if (!IsSadnessBiome(biomeManager.CurrentBiome))
        {
            return false;
        }

        switch (biomeManager.CurrentPhase)
        {
            case BiomePhase.Pre:
                target = preTarget;
                return true;
            case BiomePhase.Mid:
                target = midTarget;
                return true;
            case BiomePhase.Peaked:
                target = peakedTarget;
                return true;
            default:
                return false;
        }
    }

    private bool TrySubscribeToBiomeManager()
    {
        if (subscribedToBiomeManager)
        {
            return true;
        }

        biomeManager = BiomeManager.Instance;
        if (biomeManager == null)
        {
            return false;
        }

        biomeManager.onBiomeChanged.AddListener(OnBiomeChanged);
        biomeManager.onBiomePhaseChanged.AddListener(OnBiomePhaseChanged);
        subscribedToBiomeManager = true;
        return true;
    }

    private void UnsubscribeFromBiomeManager()
    {
        if (!subscribedToBiomeManager || biomeManager == null)
        {
            subscribedToBiomeManager = false;
            return;
        }

        biomeManager.onBiomeChanged.RemoveListener(OnBiomeChanged);
        biomeManager.onBiomePhaseChanged.RemoveListener(OnBiomePhaseChanged);
        subscribedToBiomeManager = false;
    }

    private void StopRunningLerp()
    {
        if (runningLerp == null)
        {
            return;
        }

        StopCoroutine(runningLerp);
        runningLerp = null;
    }

    private bool IsSadnessBiome(BiomeData biome)
    {
        if (biome == null || string.IsNullOrWhiteSpace(sadnessFamilyId))
        {
            return false;
        }

        string targetName = sadnessFamilyId.Trim();
        return MatchesName(biome.familyId, targetName) || MatchesName(biome.biomeName, targetName);
    }

    private static bool MatchesName(string value, string targetName)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               string.Equals(value.Trim(), targetName, StringComparison.OrdinalIgnoreCase);
    }

    private static EmissionTarget ClampTarget(EmissionTarget target)
    {
        target.particleSystem1 = Mathf.Max(0f, target.particleSystem1);
        target.particleSystem2 = Mathf.Max(0f, target.particleSystem2);
        target.particleSystem3 = Mathf.Max(0f, target.particleSystem3);
        return target;
    }

    private static bool ApproximatelySame(EmissionTarget a, EmissionTarget b)
    {
        return Mathf.Approximately(a.particleSystem1, b.particleSystem1) &&
               Mathf.Approximately(a.particleSystem2, b.particleSystem2) &&
               Mathf.Approximately(a.particleSystem3, b.particleSystem3);
    }
}
