using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerSootStatus : MonoBehaviour, IManualStatusEscapeSource
{
    [Header("Effect")]
    [SerializeField] private ParticleSystem sootParticleSystem;

    [Header("Cleanse")]
    [Tooltip("Seconds the player must glide continuously to remove the soot effect.")]
    [Min(0f)] [SerializeField] private float glideCleanseDuration = 3f;

    private static readonly Dictionary<ParticleVignetteEffect, int> activeVignetteCounts =
        new Dictionary<ParticleVignetteEffect, int>();

    private PlayerController player;
    private PlayerStatusManager statusManager;
    private ParticleVignetteEffect sootVignette;
    private bool hasActiveEffect;
    private bool isHoldingVignette;
    private bool warnedMissingParticle;
    private bool warnedMissingVignette;
    private float emotionIncreasePerTick;
    private float emotionTickInterval;
    private float nextEmotionTickTime;
    private float glideCleanseTimer;

    public bool IsManualEscapeActive => hasActiveEffect;
    public bool IsManualEscapeVisible => hasActiveEffect;
    public float ManualEscapeProgress01 => glideCleanseDuration <= 0f
        ? (hasActiveEffect ? 1f : 0f)
        : Mathf.Clamp01(glideCleanseTimer / glideCleanseDuration);

    private void Awake()
    {
        ResolvePlayer();
        ClearSootParticlesImmediately();
    }

    private void OnValidate()
    {
        glideCleanseDuration = Mathf.Max(0f, glideCleanseDuration);
    }

    private void Update()
    {
        if (!hasActiveEffect)
        {
            return;
        }

        if (player == null)
        {
            ResolvePlayer();
        }

        if (player != null)
        {
            if (!player.IsGliding)
            {
                glideCleanseTimer = 0f;
            }
            else if (glideCleanseDuration <= 0f)
            {
                ClearEffect();
                return;
            }
            else
            {
                glideCleanseTimer += Time.deltaTime;
                if (glideCleanseTimer >= glideCleanseDuration)
                {
                    ClearEffect();
                    return;
                }
            }
        }

        if (EmotionMeter.Instance == null || emotionIncreasePerTick <= 0f || Time.time < nextEmotionTickTime)
        {
            return;
        }

        EmotionMeter.Instance.AddObstacleEmotion(emotionIncreasePerTick);
        nextEmotionTickTime = Time.time + emotionTickInterval;
    }

    private void OnDisable()
    {
        ClearEffect(false);
    }

    private void OnDestroy()
    {
        ClearEffect(false);
    }

    public void RefreshSootEffect(
        float emotionIncrease,
        float tickInterval)
    {
        ResolvePlayer();
        emotionIncreasePerTick = Mathf.Max(0f, emotionIncrease);
        emotionTickInterval = Mathf.Max(0.01f, tickInterval);
        nextEmotionTickTime = Time.time + emotionTickInterval;
        hasActiveEffect = true;
        glideCleanseTimer = 0f;

        AcquireVignette();
        PlaySootParticles();
        AudioManager.Instance?.PlaySootEffectLoop();
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

    private void AcquireVignette()
    {
        if (isHoldingVignette)
        {
            return;
        }

        if (SootVignetteManager.Instance == null)
        {
            WarnMissingVignette("No SootVignetteManager was found in the scene.");
            return;
        }

        if (!SootVignetteManager.Instance.TryGetVignette(out sootVignette))
        {
            WarnMissingVignette("No soot vignette is assigned on SootVignetteManager.");
            return;
        }

        if (!sootVignette.gameObject.activeSelf)
        {
            sootVignette.gameObject.SetActive(true);
        }

        activeVignetteCounts.TryGetValue(sootVignette, out int activeCount);
        activeVignetteCounts[sootVignette] = activeCount + 1;
        isHoldingVignette = true;

        if (activeCount == 0)
        {
            sootVignette.Show();
        }
    }

    private void ReleaseVignette()
    {
        if (!isHoldingVignette)
        {
            return;
        }

        isHoldingVignette = false;
        if (sootVignette == null)
        {
            return;
        }

        activeVignetteCounts.TryGetValue(sootVignette, out int activeCount);
        activeCount = Mathf.Max(0, activeCount - 1);

        if (activeCount > 0)
        {
            activeVignetteCounts[sootVignette] = activeCount;
        }
        else
        {
            activeVignetteCounts.Remove(sootVignette);
            sootVignette.Hide();
        }

        sootVignette = null;
    }

    private void PlaySootParticles()
    {
        if (sootParticleSystem == null)
        {
            WarnMissingParticle();
            return;
        }

        if (!sootParticleSystem.isPlaying)
        {
            sootParticleSystem.Play(true);
        }
    }

    private void StopSootParticles()
    {
        if (sootParticleSystem == null)
        {
            return;
        }

        sootParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private void ClearSootParticlesImmediately()
    {
        if (sootParticleSystem != null)
        {
            sootParticleSystem.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void ClearEffect(bool fadeAudio = true)
    {
        hasActiveEffect = false;
        glideCleanseTimer = 0f;
        emotionIncreasePerTick = 0f;
        nextEmotionTickTime = 0f;
        StopSootParticles();
        ReleaseVignette();

        if (fadeAudio)
        {
            AudioManager.Instance?.StopSootEffectLoop();
        }
        else
        {
            AudioManager.Instance?.StopSootEffectLoop(false);
        }
    }

    private void WarnMissingParticle()
    {
        if (warnedMissingParticle)
        {
            return;
        }

        Debug.LogWarning(
            "[PlayerSootStatus] Assign a soot particle system on the player before using soot obstacles.",
            this);
        warnedMissingParticle = true;
    }

    private void WarnMissingVignette(string message)
    {
        if (warnedMissingVignette)
        {
            return;
        }

        Debug.LogWarning($"[PlayerSootStatus] {message}", this);
        warnedMissingVignette = true;
    }
}
