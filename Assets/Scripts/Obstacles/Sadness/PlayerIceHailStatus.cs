using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerIceHailStatus : MonoBehaviour
{
    [Header("Effect")]
    [SerializeField] private ParticleSystem slowdownParticleSystem;

    [Header("Effect Timing")]
    [Min(0f)] [SerializeField] private float fadeInDuration = 0.25f;
    [Min(0f)] [SerializeField] private float fadeOutDuration = 0.6f;

    private PlayerController player;
    private PlayerStatusManager statusManager;
    private ParticleStatusFadeEffect particleFadeEffect;
    private ParticleVignetteEffect iceHailVignetteEffect;
    private Coroutine effectLifetimeRoutine;
    private bool isHoldingVignette;
    private bool warnedMissingVignette;

    private static readonly Dictionary<ParticleVignetteEffect, int> activeVignetteCounts =
        new Dictionary<ParticleVignetteEffect, int>();

    private void Awake()
    {
        ResolvePlayer();
        InitializeParticleFadeEffect();
    }

    private void OnValidate()
    {
        fadeInDuration = Mathf.Max(0f, fadeInDuration);
        fadeOutDuration = Mathf.Max(0f, fadeOutDuration);
    }

    private void OnDisable()
    {
        ClearImmediately();
    }

    private void OnDestroy()
    {
        ClearImmediately();
        particleFadeEffect?.Release();
    }

    public void RefreshSlow(float movementMultiplier, float duration)
    {
        ResolvePlayer();
        if (player == null)
        {
            return;
        }

        float clampedDuration = Mathf.Max(0f, duration);
        player.ApplyTimedMovementModifier(
            this,
            Mathf.Clamp01(movementMultiplier),
            1f,
            clampedDuration);

        RestartEffectLifetime(clampedDuration);
        AcquireVignette();
        ShowEffect();
    }

    private void RestartEffectLifetime(float duration)
    {
        StopEffectLifetimeRoutine();
        effectLifetimeRoutine =
            StartCoroutine(EffectLifetimeRoutine(duration));
    }

    private IEnumerator EffectLifetimeRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        effectLifetimeRoutine = null;

        if (particleFadeEffect != null)
        {
            particleFadeEffect.Hide(fadeOutDuration);
        }

        ReleaseVignette();
    }

    private void ShowEffect()
    {
        if (slowdownParticleSystem == null)
        {
            return;
        }

        InitializeParticleFadeEffect();
        particleFadeEffect.Show(fadeInDuration);
    }

    private void ClearImmediately()
    {
        if (player != null)
        {
            player.RemoveMovementModifier(this);
        }

        StopEffectLifetimeRoutine();
        ReleaseVignette();
        particleFadeEffect?.ClearImmediately();
    }

    private void AcquireVignette()
    {
        if (isHoldingVignette)
        {
            return;
        }

        if (IceHailVignetteManager.Instance == null)
        {
            WarnMissingVignette("No IceHailVignetteManager was found in the scene.");
            return;
        }

        if (!IceHailVignetteManager.Instance.TryGetVignette(out iceHailVignetteEffect))
        {
            WarnMissingVignette("No ice hail vignette effect is assigned on IceHailVignetteManager.");
            return;
        }

        if (!iceHailVignetteEffect.gameObject.activeSelf)
        {
            iceHailVignetteEffect.gameObject.SetActive(true);
        }

        activeVignetteCounts.TryGetValue(iceHailVignetteEffect, out int activeCount);
        activeVignetteCounts[iceHailVignetteEffect] = activeCount + 1;
        isHoldingVignette = true;

        if (activeCount == 0)
        {
            iceHailVignetteEffect.Show();
        }
    }

    private void ReleaseVignette()
    {
        if (!isHoldingVignette)
        {
            return;
        }

        isHoldingVignette = false;
        if (iceHailVignetteEffect == null)
        {
            return;
        }

        activeVignetteCounts.TryGetValue(iceHailVignetteEffect, out int activeCount);
        activeCount = Mathf.Max(0, activeCount - 1);

        if (activeCount > 0)
        {
            activeVignetteCounts[iceHailVignetteEffect] = activeCount;
        }
        else
        {
            activeVignetteCounts.Remove(iceHailVignetteEffect);
            iceHailVignetteEffect.Hide();
        }

        iceHailVignetteEffect = null;
    }

    private void ResolvePlayer()
    {
        if (player == null)
        {
            ResolveStatusManager();
            player = statusManager != null ? statusManager.Player : null;
        }
    }

    private void ResolveStatusManager()
    {
        if (statusManager == null)
        {
            statusManager = GetComponent<PlayerStatusManager>();
        }
    }

    private void InitializeParticleFadeEffect()
    {
        if (particleFadeEffect != null || slowdownParticleSystem == null)
        {
            return;
        }

        particleFadeEffect =
            gameObject.AddComponent<ParticleStatusFadeEffect>();
        particleFadeEffect.Configure(slowdownParticleSystem);
    }

    private void StopEffectLifetimeRoutine()
    {
        if (effectLifetimeRoutine == null)
        {
            return;
        }

        StopCoroutine(effectLifetimeRoutine);
        effectLifetimeRoutine = null;
    }

    private void WarnMissingVignette(string message)
    {
        if (warnedMissingVignette)
        {
            return;
        }

        Debug.LogWarning($"[PlayerIceHailStatus] {message}", this);
        warnedMissingVignette = true;
    }
}
