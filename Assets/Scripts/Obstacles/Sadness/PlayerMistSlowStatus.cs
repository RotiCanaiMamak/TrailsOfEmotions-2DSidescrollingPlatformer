using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerMistSlowStatus : MonoBehaviour
{
    private struct MistSlowSource
    {
        public readonly float movementMultiplier;
        public readonly float animationMultiplier;

        public MistSlowSource(float movementMultiplier, float animationMultiplier)
        {
            this.movementMultiplier = Mathf.Clamp01(movementMultiplier);
            this.animationMultiplier = Mathf.Clamp01(animationMultiplier);
        }
    }

    private const float DestroyedSourceRecoveryDuration = 0.5f;

    private static readonly Dictionary<ParticleVignetteEffect, int> activeVignetteCounts =
        new Dictionary<ParticleVignetteEffect, int>();

    private readonly Dictionary<Object, MistSlowSource> activeSources =
        new Dictionary<Object, MistSlowSource>();
    private readonly List<Object> invalidSources = new List<Object>();

    private PlayerController player;
    private PlayerStatusManager statusManager;
    private Animator animator;
    private ParticleVignetteEffect mistVignetteEffect;
    private Animator capturedAnimator;
    private float capturedAnimatorSpeed = 1f;
    private float currentMovementMultiplier = 1f;
    private float currentAnimationMultiplier = 1f;
    private float recoveryStartMovementMultiplier = 1f;
    private float recoveryStartAnimationMultiplier = 1f;
    private float recoveryDuration;
    private float recoveryElapsed;
    private bool isRecovering;
    private bool isHoldingVignette;
    private bool warnedMissingVignette;

    private void Awake()
    {
        ResolvePlayer();
    }

    private void Update()
    {
        PruneDestroyedSources();

        if (!isRecovering)
        {
            return;
        }

        recoveryElapsed += Time.deltaTime;
        float progress = recoveryDuration <= 0f
            ? 1f
            : Mathf.Clamp01(recoveryElapsed / recoveryDuration);

        ApplyMultipliers(
            Mathf.Lerp(recoveryStartMovementMultiplier, 1f, progress),
            Mathf.Lerp(recoveryStartAnimationMultiplier, 1f, progress));

        if (progress >= 1f)
        {
            FinishRecovery();
        }
    }

    private void OnDisable()
    {
        ClearImmediately();
    }

    private void OnDestroy()
    {
        ClearImmediately();
    }

    public void EnterMist(
        Object source,
        float movementMultiplier,
        float animationMultiplier)
    {
        if (source == null)
        {
            return;
        }

        ResolvePlayer();
        ResolveAnimator();
        activeSources[source] = new MistSlowSource(
            movementMultiplier,
            animationMultiplier);

        AcquireVignette();
        isRecovering = false;
        ApplyStrongestActiveSlowdown();
    }

    public void ExitMist(Object source, float recoveryDuration)
    {
        if (ReferenceEquals(source, null) || !activeSources.Remove(source))
        {
            return;
        }

        if (activeSources.Count > 0)
        {
            isRecovering = false;
            ApplyStrongestActiveSlowdown();
            return;
        }

        ReleaseVignette();
        BeginRecovery(recoveryDuration);
    }

    private void AcquireVignette()
    {
        if (isHoldingVignette)
        {
            return;
        }

        if (MistVignetteManager.Instance == null)
        {
            WarnMissingVignette("No MistVignetteManager was found in the scene.");
            return;
        }

        if (!MistVignetteManager.Instance.TryGetVignette(out mistVignetteEffect))
        {
            WarnMissingVignette("No mist vignette effect is assigned on MistVignetteManager.");
            return;
        }

        if (!mistVignetteEffect.gameObject.activeSelf)
        {
            mistVignetteEffect.gameObject.SetActive(true);
        }

        activeVignetteCounts.TryGetValue(mistVignetteEffect, out int activeCount);
        activeVignetteCounts[mistVignetteEffect] = activeCount + 1;
        isHoldingVignette = true;

        if (activeCount == 0)
        {
            mistVignetteEffect.Show();
        }
    }

    private void ReleaseVignette()
    {
        if (!isHoldingVignette)
        {
            return;
        }

        isHoldingVignette = false;
        if (mistVignetteEffect == null)
        {
            return;
        }

        activeVignetteCounts.TryGetValue(mistVignetteEffect, out int activeCount);
        activeCount = Mathf.Max(0, activeCount - 1);

        if (activeCount > 0)
        {
            activeVignetteCounts[mistVignetteEffect] = activeCount;
        }
        else
        {
            activeVignetteCounts.Remove(mistVignetteEffect);
            mistVignetteEffect.Hide();
        }

        mistVignetteEffect = null;
    }

    private void ApplyStrongestActiveSlowdown()
    {
        float movementMultiplier = 1f;
        float animationMultiplier = 1f;

        foreach (MistSlowSource source in activeSources.Values)
        {
            movementMultiplier = Mathf.Min(movementMultiplier, source.movementMultiplier);
            animationMultiplier = Mathf.Min(animationMultiplier, source.animationMultiplier);
        }

        ApplyMultipliers(movementMultiplier, animationMultiplier);
    }

    private void BeginRecovery(float duration)
    {
        recoveryStartMovementMultiplier = currentMovementMultiplier;
        recoveryStartAnimationMultiplier = currentAnimationMultiplier;
        recoveryDuration = Mathf.Max(0f, duration);
        recoveryElapsed = 0f;
        isRecovering = true;

        if (recoveryDuration <= 0f)
        {
            ApplyMultipliers(1f, 1f);
            FinishRecovery();
        }
    }

    private void ApplyMultipliers(float movementMultiplier, float animationMultiplier)
    {
        currentMovementMultiplier = Mathf.Clamp01(movementMultiplier);
        currentAnimationMultiplier = Mathf.Clamp01(animationMultiplier);

        if (player != null)
        {
            player.AddMovementModifier(
                this,
                currentMovementMultiplier,
                1f);
        }

        ResolveAnimator();
        CaptureAnimatorSpeedIfNeeded();
        if (animator != null && capturedAnimator == animator)
        {
            animator.speed = capturedAnimatorSpeed * currentAnimationMultiplier;
        }
    }

    private void FinishRecovery()
    {
        isRecovering = false;
        recoveryElapsed = 0f;
        currentMovementMultiplier = 1f;
        currentAnimationMultiplier = 1f;

        if (player != null)
        {
            player.RemoveMovementModifier(this);
        }

        RestoreAnimatorSpeed();
    }

    private void ClearImmediately()
    {
        activeSources.Clear();
        invalidSources.Clear();
        isRecovering = false;
        ReleaseVignette();

        if (player != null)
        {
            player.RemoveMovementModifier(this);
        }

        RestoreAnimatorSpeed();
        currentMovementMultiplier = 1f;
        currentAnimationMultiplier = 1f;
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

    private void ResolveAnimator()
    {
        if (animator == null)
        {
            ResolveStatusManager();
            animator = statusManager != null ? statusManager.Animator : null;
        }
    }

    private void CaptureAnimatorSpeedIfNeeded()
    {
        if (animator == null || capturedAnimator == animator)
        {
            return;
        }

        capturedAnimator = animator;
        capturedAnimatorSpeed = animator.speed;
    }

    private void RestoreAnimatorSpeed()
    {
        if (capturedAnimator != null)
        {
            capturedAnimator.speed = capturedAnimatorSpeed;
        }

        capturedAnimator = null;
        capturedAnimatorSpeed = 1f;
    }

    private void PruneDestroyedSources()
    {
        if (activeSources.Count == 0)
        {
            return;
        }

        invalidSources.Clear();
        foreach (Object source in activeSources.Keys)
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
        if (activeSources.Count > 0)
        {
            ApplyStrongestActiveSlowdown();
        }
        else
        {
            ReleaseVignette();
            BeginRecovery(DestroyedSourceRecoveryDuration);
        }
    }

    private void WarnMissingVignette(string message)
    {
        if (warnedMissingVignette)
        {
            return;
        }

        Debug.LogWarning($"[PlayerMistSlowStatus] {message}", this);
        warnedMissingVignette = true;
    }
}
