using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Collider2D))]
public class VaporCloud : MonoBehaviour, IBackpackAbsorbable, IRegulationClearable
{
    [Header("Collision")]
    [SerializeField] private Collider2D cloudCollider;

    [Header("Ability Drain")]
    [FormerlySerializedAs("cooldownPenaltySeconds")]
    [Min(0)] [SerializeField] private int activeChargeDrain = 1;

    [Header("Emotion")]
    [Min(0f)] [SerializeField] private float emotionIncreaseOnHit = 1f;

    [Header("Absorb Fade")]
    [SerializeField] private ParticleSystem absorbFadeParticle;
    [Min(0f)] [SerializeField] private float particleFadeOutDuration = 0.6f;

    [Header("Audio")]
    [SerializeField] private AudioClip obstacleCollisionClip;
    [Range(0f, 1f)] [SerializeField] private float obstacleCollisionVolume = 1f;

    private ParticleFadeOutEffect particleFadeEffect;
    private bool consumed;

    private void Awake()
    {
        ResolveCollider();
        ConfigureCollider();
    }

    private void OnValidate()
    {
        activeChargeDrain = Mathf.Max(0, activeChargeDrain);
        emotionIncreaseOnHit = Mathf.Max(0f, emotionIncreaseOnHit);
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

        CharacterAbility ability =
            player.Statuses != null ? player.Statuses.Ability : null;
        if (ability != null)
        {
            ability.TryDrainActiveCharges(activeChargeDrain);
        }

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
