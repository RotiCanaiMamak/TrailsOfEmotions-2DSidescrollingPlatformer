using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerAngerRegulationStatus : MonoBehaviour
{
    private static readonly Dictionary<ParticleVignetteEffect, int> activeVignetteCounts =
        new Dictionary<ParticleVignetteEffect, int>();

    [Header("Trail")]
    [SerializeField] private ParticleSystem trailParticleSystem;

    [Header("Warnings")]
    [SerializeField] private bool warnIfMissingReferences = true;

    private readonly HashSet<Object> activeSources = new HashSet<Object>();
    private readonly List<Object> invalidSources = new List<Object>();

    private ParticleVignetteEffect vignetteEffect;
    private bool isHoldingVignette;
    private bool warnedMissingTrail;
    private bool warnedMissingVignette;

    private void Awake()
    {
        ClearTrailImmediately();
    }

    private void Update()
    {
        PruneDestroyedSources();
    }

    private void OnDisable()
    {
        ClearImmediately();
    }

    private void OnDestroy()
    {
        ClearImmediately();
    }

    public void EnterRegulation(Object source)
    {
        if (source == null)
        {
            return;
        }

        bool wasActive = activeSources.Count > 0;
        activeSources.Add(source);

        if (wasActive)
        {
            return;
        }

        PlayTrail();
        AcquireVignette();
    }

    public void ExitRegulation(Object source)
    {
        if (ReferenceEquals(source, null) || !activeSources.Remove(source))
        {
            return;
        }

        if (activeSources.Count > 0)
        {
            return;
        }

        StopTrail();
        ReleaseVignette();
    }

    private void PlayTrail()
    {
        if (trailParticleSystem == null)
        {
            WarnMissingTrail();
            return;
        }

        if (!trailParticleSystem.isPlaying)
        {
            trailParticleSystem.Play(true);
        }
    }

    private void StopTrail()
    {
        if (trailParticleSystem == null)
        {
            return;
        }

        trailParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private void ClearTrailImmediately()
    {
        if (trailParticleSystem == null)
        {
            return;
        }

        trailParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void AcquireVignette()
    {
        if (isHoldingVignette)
        {
            return;
        }

        if (AngerRegulationVignetteManager.Instance == null)
        {
            WarnMissingVignette("No AngerRegulationVignetteManager was found in the scene.");
            return;
        }

        if (!AngerRegulationVignetteManager.Instance.TryGetVignette(out vignetteEffect))
        {
            WarnMissingVignette("No anger regulation vignette effect is assigned on AngerRegulationVignetteManager.");
            return;
        }

        if (!vignetteEffect.gameObject.activeSelf)
        {
            vignetteEffect.gameObject.SetActive(true);
        }

        activeVignetteCounts.TryGetValue(vignetteEffect, out int activeCount);
        activeVignetteCounts[vignetteEffect] = activeCount + 1;
        isHoldingVignette = true;

        if (activeCount == 0)
        {
            vignetteEffect.Show();
        }
    }

    private void ReleaseVignette()
    {
        if (!isHoldingVignette)
        {
            return;
        }

        isHoldingVignette = false;
        if (vignetteEffect == null)
        {
            return;
        }

        activeVignetteCounts.TryGetValue(vignetteEffect, out int activeCount);
        activeCount = Mathf.Max(0, activeCount - 1);

        if (activeCount > 0)
        {
            activeVignetteCounts[vignetteEffect] = activeCount;
        }
        else
        {
            activeVignetteCounts.Remove(vignetteEffect);
            vignetteEffect.Hide();
        }

        vignetteEffect = null;
    }

    private void PruneDestroyedSources()
    {
        if (activeSources.Count == 0)
        {
            return;
        }

        invalidSources.Clear();
        foreach (Object source in activeSources)
        {
            if (source == null)
            {
                invalidSources.Add(source);
            }
        }

        if (invalidSources.Count == 0)
        {
            return;
        }

        for (int i = 0; i < invalidSources.Count; i++)
        {
            activeSources.Remove(invalidSources[i]);
        }

        invalidSources.Clear();
        if (activeSources.Count == 0)
        {
            StopTrail();
            ReleaseVignette();
        }
    }

    private void ClearImmediately()
    {
        activeSources.Clear();
        invalidSources.Clear();
        ClearTrailImmediately();
        ReleaseVignette();
    }

    private void WarnMissingTrail()
    {
        if (!warnIfMissingReferences || warnedMissingTrail)
        {
            return;
        }

        Debug.LogWarning(
            "[PlayerAngerRegulationStatus] Assign the player's anger regulation trail ParticleSystem.",
            this);
        warnedMissingTrail = true;
    }

    private void WarnMissingVignette(string message)
    {
        if (!warnIfMissingReferences || warnedMissingVignette)
        {
            return;
        }

        Debug.LogWarning($"[PlayerAngerRegulationStatus] {message}", this);
        warnedMissingVignette = true;
    }
}
