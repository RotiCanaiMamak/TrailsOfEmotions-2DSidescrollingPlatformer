using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class MistObstacle : MonoBehaviour, IBackpackAbsorbable, IRegulationClearable
{
    [Header("Collision")]
    [SerializeField] private Collider2D mistCollider;

    [Header("Emotion")]
    [Min(0f)] [SerializeField] private float emotionIncreasePerTick = 1f;
    [Min(0.01f)] [SerializeField] private float emotionTickInterval = 0.5f;

    [Header("Slowdown")]
    [Range(0f, 1f)] [SerializeField] private float movementSpeedMultiplier = 0.6f;
    [Range(0f, 1f)] [SerializeField] private float animationSpeedMultiplier = 0.6f;
    [Min(0f)] [SerializeField] private float recoveryDuration = 0.5f;

    [Header("Absorb Fade")]
    [SerializeField] private ParticleSystem absorbFadeParticle;
    [Min(0f)] [SerializeField] private float particleFadeOutDuration = 0.6f;

    [Header("Audio")]
    [SerializeField] private AudioClip obstacleCollisionClip;
    [Range(0f, 1f)] [SerializeField] private float obstacleCollisionVolume = 1f;

    private readonly HashSet<Collider2D> playerCollidersInside =
        new HashSet<Collider2D>();
    private readonly HashSet<PlayerMistSlowStatus> affectedPlayers =
        new HashSet<PlayerMistSlowStatus>();

    private ParticleFadeOutEffect particleFadeEffect;
    private float nextEmotionTickTime;
    private bool consumed;
    private bool hasPlayedCollisionSound;
    private bool warnedMissingMistStatus;

    private void Awake()
    {
        ResolveCollider();
        ConfigureCollider();
    }

    private void OnValidate()
    {
        emotionIncreasePerTick = Mathf.Max(0f, emotionIncreasePerTick);
        emotionTickInterval = Mathf.Max(0.01f, emotionTickInterval);
        movementSpeedMultiplier = Mathf.Clamp01(movementSpeedMultiplier);
        animationSpeedMultiplier = Mathf.Clamp01(animationSpeedMultiplier);
        recoveryDuration = Mathf.Max(0f, recoveryDuration);
        particleFadeOutDuration = Mathf.Max(0f, particleFadeOutDuration);
        obstacleCollisionVolume = Mathf.Clamp01(obstacleCollisionVolume);
        ConfigureCollider();
    }

    private void Update()
    {
        if (consumed || playerCollidersInside.Count == 0 ||
            EmotionMeter.Instance == null || emotionIncreasePerTick <= 0f)
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
        ApplyMistEffects(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        ApplyMistEffects(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        RemoveMistEffects(other);
    }

    private void OnDisable()
    {
        ReleaseAffectedPlayers();
        playerCollidersInside.Clear();
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

    private void ApplyMistEffects(Collider2D other)
    {
        if (consumed || other == null)
        {
            return;
        }

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || !player.IsCharacterCollider(other))
        {
            return;
        }

        bool firstPlayerContact = playerCollidersInside.Count == 0;
        playerCollidersInside.Add(other);
        if (firstPlayerContact)
        {
            nextEmotionTickTime = Time.time + emotionTickInterval;
        }

        PlayerMistSlowStatus mistStatus =
            player.Statuses != null ? player.Statuses.MistSlowStatus : null;
        if (mistStatus == null)
        {
            WarnMissingMistStatus(player);
            return;
        }

        affectedPlayers.Add(mistStatus);
        mistStatus.EnterMist(
            this,
            movementSpeedMultiplier,
            animationSpeedMultiplier);
        PlayCollisionSoundOnce();
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

    private void RemoveMistEffects(Collider2D other)
    {
        if (other == null)
        {
            return;
        }

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || !player.IsCharacterCollider(other) ||
            !playerCollidersInside.Remove(other))
        {
            return;
        }

        PlayerMistSlowStatus mistStatus =
            player.Statuses != null ? player.Statuses.MistSlowStatus : null;
        if (mistStatus == null)
        {
            return;
        }

        affectedPlayers.Remove(mistStatus);
        mistStatus.ExitMist(this, recoveryDuration);
    }

    private void ReleaseAffectedPlayers()
    {
        foreach (PlayerMistSlowStatus mistStatus in affectedPlayers)
        {
            if (mistStatus != null)
            {
                mistStatus.ExitMist(this, recoveryDuration);
            }
        }

        affectedPlayers.Clear();
    }

    private void ResolveCollider()
    {
        if (mistCollider == null)
        {
            mistCollider = GetComponent<Collider2D>();
        }
    }

    private void ConfigureCollider()
    {
        if (mistCollider != null)
        {
            mistCollider.isTrigger = true;
        }
    }

    private void Consume()
    {
        consumed = true;
        ReleaseAffectedPlayers();
        playerCollidersInside.Clear();
        SetMistColliderEnabled(false);
        FadeParticleEffects();
    }

    private void SetMistColliderEnabled(bool enabled)
    {
        if (mistCollider != null)
        {
            mistCollider.enabled = enabled;
        }
    }

    private void FadeParticleEffects()
    {
        ResolveParticleFadeEffect();
        particleFadeEffect.Play(
            particleFadeOutDuration,
            ResolveAbsorbFadeParticle());
    }

    private ParticleSystem ResolveAbsorbFadeParticle()
    {
        if (absorbFadeParticle == null)
        {
            absorbFadeParticle = GetComponentInChildren<ParticleSystem>(true);
        }

        return absorbFadeParticle;
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

    private void WarnMissingMistStatus(PlayerController player)
    {
        if (warnedMissingMistStatus)
        {
            return;
        }

        Debug.LogWarning(
            "[MistObstacle] Assign PlayerMistSlowStatus on the player's PlayerStatusManager before using mist obstacles.",
            player);
        warnedMissingMistStatus = true;
    }

}
