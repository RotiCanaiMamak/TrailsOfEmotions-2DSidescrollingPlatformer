using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(NoisyLeftMover))]
public sealed class DriftingWisp : MonoBehaviour, IBackpackAbsorbable, IRegulationClearable
{
    private enum WispState
    {
        Seeking,
        Removed
    }

    [Header("Collision")]
    [SerializeField] private Collider2D wispCollider;

    [Header("Player Effect")]
    [Range(0f, 1f)] [SerializeField] private float movementSpeedMultiplier = 0.75f;
    [Min(0)] [SerializeField] private int activeChargeDrain = 1;
    [Min(0.01f)] [SerializeField] private float activeChargeDrainInterval = 1f;
    [Min(0f)] [SerializeField] private float glideCleanseDuration = 1f;

    [Header("Emotion")]
    [FormerlySerializedAs("emotionIncreaseOnHit")]
    [Min(0f)] [SerializeField] private float emotionIncreasePerTick = 1f;
    [Min(0.01f)] [SerializeField] private float emotionTickInterval = 0.5f;

    [Header("Particle Fade")]
    [SerializeField] private ParticleSystem wispParticle;
    [Min(0f)] [SerializeField] private float particleFadeOutDuration = 0.6f;

    [Header("Audio")]
    [SerializeField] private AudioClip obstacleCollisionClip;
    [Range(0f, 1f)] [SerializeField] private float obstacleCollisionVolume = 1f;

    private WispState state;
    private ParticleFadeOutEffect particleFadeEffect;
    private bool warnedMissingPlayerStatus;

    private void Awake()
    {
        ResolveCollider();
        ConfigureCollider();
        state = WispState.Seeking;
    }

    private void OnValidate()
    {
        movementSpeedMultiplier = Mathf.Clamp01(movementSpeedMultiplier);
        activeChargeDrain = Mathf.Max(0, activeChargeDrain);
        activeChargeDrainInterval = Mathf.Max(0.01f, activeChargeDrainInterval);
        glideCleanseDuration = Mathf.Max(0f, glideCleanseDuration);
        emotionIncreasePerTick = Mathf.Max(0f, emotionIncreasePerTick);
        emotionTickInterval = Mathf.Max(0.01f, emotionTickInterval);
        particleFadeOutDuration = Mathf.Max(0f, particleFadeOutDuration);
        obstacleCollisionVolume = Mathf.Clamp01(obstacleCollisionVolume);

        ResolveCollider();
        ConfigureCollider();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (state != WispState.Seeking || other == null)
        {
            return;
        }

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || !player.IsCharacterCollider(other))
        {
            return;
        }

        PlayerDriftingWispStatus status =
            player.Statuses != null ? player.Statuses.DriftingWispStatus : null;
        if (status == null)
        {
            WarnMissingPlayerStatus(player);
            return;
        }

        if (!status.TryApplyWispEffect(
                movementSpeedMultiplier,
                activeChargeDrain,
                activeChargeDrainInterval,
                glideCleanseDuration,
                emotionIncreasePerTick,
                emotionTickInterval))
        {
            return;
        }

        AudioManager.Instance?.PlaySfxOneShot(obstacleCollisionClip, obstacleCollisionVolume);
        Consume();
    }

    public bool TryAbsorbByBackpack(LinaBackpackBulwark bulwark)
    {
        if (state != WispState.Seeking)
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

    private void Consume()
    {
        state = WispState.Removed;
        FadeParticle();
    }

    private void ResolveCollider()
    {
        if (wispCollider == null)
        {
            wispCollider = GetComponent<Collider2D>();
        }
    }

    private void ConfigureCollider()
    {
        if (wispCollider != null)
        {
            wispCollider.isTrigger = true;
        }
    }

    private void FadeParticle()
    {
        ResolveParticleFadeEffect();
        particleFadeEffect.Play(
            particleFadeOutDuration,
            ResolveWispParticle());
    }

    private ParticleSystem ResolveWispParticle()
    {
        if (wispParticle == null)
        {
            wispParticle = GetComponentInChildren<ParticleSystem>(true);
        }

        return wispParticle;
    }

    private void ResolveParticleFadeEffect()
    {
        if (particleFadeEffect == null)
        {
            particleFadeEffect = GetComponent<ParticleFadeOutEffect>();
        }

        if (particleFadeEffect == null)
        {
            particleFadeEffect =
                gameObject.AddComponent<ParticleFadeOutEffect>();
        }
    }

    private void WarnMissingPlayerStatus(PlayerController player)
    {
        if (warnedMissingPlayerStatus)
        {
            return;
        }

        Debug.LogWarning(
            "[DriftingWisp] Assign PlayerDriftingWispStatus on the player's PlayerStatusManager and assign its particle.",
            player);
        warnedMissingPlayerStatus = true;
    }
}
