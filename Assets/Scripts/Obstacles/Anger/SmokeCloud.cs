using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SmokeCloud : MonoBehaviour, IBackpackAbsorbable, IRegulationClearable
{
    [Header("Collision")]
    [SerializeField] private Collider2D cloudCollider;

    [Header("Emotion")]
    [Min(0f)] [SerializeField] private float emotionIncreasePerTick = 1f;
    [Min(0.01f)] [SerializeField] private float emotionTickInterval = 0.5f;

    [Header("Absorb Fade")]
    [SerializeField] private ParticleSystem absorbFadeParticle;
    [Min(0f)] [SerializeField] private float particleFadeOutDuration = 0.6f;

    [Header("Vignette")]
    [SerializeField] private SmokeVignetteSize vignetteSize = SmokeVignetteSize.Small;

    [Header("Audio")]
    [SerializeField] private AudioClip obstacleCollisionClip;
    [Range(0f, 1f)] [SerializeField] private float obstacleCollisionVolume = 1f;

    private static readonly Dictionary<ParticleVignetteEffect, int> activeVignetteCounts = new Dictionary<ParticleVignetteEffect, int>();

    private readonly HashSet<Collider2D> playerCollidersInside = new HashSet<Collider2D>();
    private ParticleVignetteEffect particleVignetteEffect;
    private bool warnedMissingEffect;
    private bool isShowingVignette;
    private bool consumed;
    private bool hasPlayedCollisionSound;
    private float nextEmotionTickTime;
    private ParticleFadeOutEffect particleFadeEffect;

    private void Awake()
    {
        ConfigureCollider();
    }

    private void OnValidate()
    {
        emotionIncreasePerTick = Mathf.Max(0f, emotionIncreasePerTick);
        emotionTickInterval = Mathf.Max(0.01f, emotionTickInterval);
        particleFadeOutDuration = Mathf.Max(0f, particleFadeOutDuration);
        obstacleCollisionVolume = Mathf.Clamp01(obstacleCollisionVolume);
    }

    private void Update()
    {
        if (consumed || playerCollidersInside.Count <= 0 || EmotionMeter.Instance == null || emotionIncreasePerTick <= 0f)
        {
            return;
        }

        if (Time.time < nextEmotionTickTime)
        {
            return;
        }

        EmotionMeter.Instance.AddObstacleEmotion(emotionIncreasePerTick);
        nextEmotionTickTime = Time.time + emotionTickInterval;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed || other == null)
        {
            return;
        }

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null)
        {
            return;
        }

        if (!playerCollidersInside.Add(other))
        {
            return;
        }

        if (playerCollidersInside.Count == 1)
        {
            nextEmotionTickTime = Time.time + emotionTickInterval;
        }

        PlayCollisionSoundOnce();

        if (isShowingVignette)
        {
            return;
        }

        ShowVignette();
    }

    private void PlayCollisionSoundOnce()
    {
        if (hasPlayedCollisionSound)
        {
            return;
        }

        hasPlayedCollisionSound = true;
        AudioManager.Instance?.PlaySfxOneShot(obstacleCollisionClip, obstacleCollisionVolume);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other == null || other.GetComponentInParent<PlayerController>() == null)
        {
            return;
        }

        if (!playerCollidersInside.Remove(other) || playerCollidersInside.Count > 0)
        {
            return;
        }

        ReleaseVignette();
    }

    private void OnDisable()
    {
        playerCollidersInside.Clear();
        ReleaseVignette();
    }

    public bool TryAbsorbByBackpack(LinaBackpackBulwark bulwark)
    {
        if (consumed)
        {
            return false;
        }

        Consume();
        return true;
    }

    public bool TryClearByRegulation(Object source)
    {
        return TryAbsorbByBackpack(null);
    }

    private void ConfigureCollider()
    {
        if (cloudCollider != null)
        {
            cloudCollider.isTrigger = true;
        }
    }

    private void ResolveParticleVignetteEffect()
    {
        particleVignetteEffect = GetSelectedVignetteEffect();
        warnedMissingEffect = particleVignetteEffect == null;
    }

    private void ShowVignette()
    {
        if (particleVignetteEffect == null)
        {
            ResolveParticleVignetteEffect();
        }

        if (particleVignetteEffect == null)
        {
            return;
        }

        SetSelectedVignetteActive(true);
        isShowingVignette = true;

        int activeCount = 0;
        activeVignetteCounts.TryGetValue(particleVignetteEffect, out activeCount);
        activeVignetteCounts[particleVignetteEffect] = activeCount + 1;

        if (activeCount == 0)
        {
            particleVignetteEffect.Show();
        }
    }

    private ParticleVignetteEffect GetSelectedVignetteEffect()
    {
        if (SmokeVignetteManager.Instance == null)
        {
            WarnMissingEffect("No SmokeVignetteManager was found in the scene.");
            return null;
        }

        if (!SmokeVignetteManager.Instance.TryGetVignette(vignetteSize, out ParticleVignetteEffect selectedEffect))
        {
            WarnMissingEffect($"No {vignetteSize} vignette effect is assigned on SmokeVignetteManager.");
        }

        return selectedEffect;
    }

    private void SetSelectedVignetteActive(bool active)
    {
        if (particleVignetteEffect != null && particleVignetteEffect.gameObject.activeSelf != active)
        {
            particleVignetteEffect.gameObject.SetActive(active);
        }
    }

    private void WarnMissingEffect(string message)
    {
        if (warnedMissingEffect)
        {
            return;
        }

        Debug.LogWarning($"[SmokeCloud] {message}", this);
        warnedMissingEffect = true;
    }

    private void ReleaseVignette()
    {
        if (!isShowingVignette)
        {
            return;
        }

        isShowingVignette = false;

        if (particleVignetteEffect != null)
        {
            int activeCount = 0;
            activeVignetteCounts.TryGetValue(particleVignetteEffect, out activeCount);
            activeCount = Mathf.Max(0, activeCount - 1);

            if (activeCount > 0)
            {
                activeVignetteCounts[particleVignetteEffect] = activeCount;
                return;
            }

            activeVignetteCounts.Remove(particleVignetteEffect);
            particleVignetteEffect.Hide();
        }
    }

    private void Consume()
    {
        consumed = true;
        SetCloudColliderEnabled(false);
        ReleaseVignette();
        playerCollidersInside.Clear();
        FadeParticleEffects();
    }

    private void SetCloudColliderEnabled(bool enabled)
    {
        if (cloudCollider != null)
        {
            cloudCollider.enabled = enabled;
        }
    }

    private void FadeParticleEffects()
    {
        ResolveParticleFadeEffect();
        particleFadeEffect.Play(
            particleFadeOutDuration,
            absorbFadeParticle);
    }

    private void ResolveParticleFadeEffect()
    {
        if (particleFadeEffect == null)
        {
            particleFadeEffect = GetComponent<ParticleFadeOutEffect>();
        }

        if (particleFadeEffect == null)
        {
            particleFadeEffect = gameObject.AddComponent<ParticleFadeOutEffect>();
        }
    }

}
