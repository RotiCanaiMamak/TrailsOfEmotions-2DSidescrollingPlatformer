using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class DarkerSighs : MonoBehaviour, IBackpackAbsorbable, IRegulationClearable
{
    [Header("Collision")]
    [SerializeField] private Collider2D cloudCollider;

    [Header("Ability Drain")]
    [Min(0)] [SerializeField] private int activeChargeDrain = 2;

    [Header("Emotion")]
    [Min(0f)] [SerializeField] private float emotionIncreaseOnHit = 1f;

    [Header("Slowdown")]
    [Range(0f, 1f)] [SerializeField] private float movementSpeedMultiplier = 0.6f;
    [Min(0f)] [SerializeField] private float slowdownDuration = 3f;

    [Header("Absorb Fade")]
    [SerializeField] private ParticleSystem absorbFadeParticle;
    [Min(0f)] [SerializeField] private float particleFadeOutDuration = 0.6f;

    [Header("Audio")]
    [SerializeField] private AudioClip obstacleCollisionClip;
    [Range(0f, 1f)] [SerializeField] private float obstacleCollisionVolume = 1f;

    private ParticleFadeOutEffect particleFadeEffect;
    private bool consumed;
    private bool warnedMissingDarkerSighStatus;

    private void Awake()
    {
        ResolveCollider();
        ConfigureCollider();
    }

    private void OnValidate()
    {
        activeChargeDrain = Mathf.Max(0, activeChargeDrain);
        emotionIncreaseOnHit = Mathf.Max(0f, emotionIncreaseOnHit);
        movementSpeedMultiplier = Mathf.Clamp01(movementSpeedMultiplier);
        slowdownDuration = Mathf.Max(0f, slowdownDuration);
        particleFadeOutDuration = Mathf.Max(0f, particleFadeOutDuration);
        obstacleCollisionVolume = Mathf.Clamp01(obstacleCollisionVolume);
        ConfigureCollider();
    }

    private void OnTriggerEnter2D(Collider2D other)
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

        DrainActiveAbility(player);
        ApplySlowdown(player);
        AddHitEmotion();
        AudioManager.Instance?.PlaySfxOneShot(obstacleCollisionClip, obstacleCollisionVolume);
        Consume();
    }

    private void AddHitEmotion()
    {
        if (EmotionMeter.Instance != null && emotionIncreaseOnHit > 0f)
        {
            EmotionMeter.Instance.AddObstacleEmotion(emotionIncreaseOnHit);
        }
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

    private void DrainActiveAbility(PlayerController player)
    {
        CharacterAbility ability =
            player.Statuses != null ? player.Statuses.Ability : null;
        if (ability != null)
        {
            ability.TryDrainActiveCharges(activeChargeDrain);
        }
    }

    private void ApplySlowdown(PlayerController player)
    {
        PlayerDarkerSighStatus status =
            player.Statuses != null ? player.Statuses.DarkerSighStatus : null;
        if (status == null)
        {
            WarnMissingDarkerSighStatus(player);
            return;
        }

        status.RefreshSlow(movementSpeedMultiplier, slowdownDuration);
    }

    private void WarnMissingDarkerSighStatus(PlayerController player)
    {
        if (warnedMissingDarkerSighStatus)
        {
            return;
        }

        Debug.LogWarning(
            "[DarkerSighs] Assign PlayerDarkerSighStatus on the player's PlayerStatusManager before using darker sigh clouds.",
            player);
        warnedMissingDarkerSighStatus = true;
    }

    private void ResolveCollider()
    {
        if (cloudCollider == null)
        {
            cloudCollider = GetComponent<Collider2D>();
        }
    }

    private void ConfigureCollider()
    {
        if (cloudCollider != null)
        {
            cloudCollider.isTrigger = true;
        }
    }

    private void Consume()
    {
        consumed = true;
        SetCloudColliderEnabled(false);
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
