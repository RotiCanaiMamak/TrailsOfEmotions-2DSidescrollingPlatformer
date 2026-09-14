using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SootObstacle : MonoBehaviour, IBackpackAbsorbable, IRegulationClearable
{
    [Header("Collision")]
    [SerializeField] private Collider2D sootCollider;

    [Header("Emotion")]
    [Min(0f)] [SerializeField] private float emotionIncreasePerTick = 1f;
    [Min(0.01f)] [SerializeField] private float emotionTickInterval = 0.5f;

    [Header("Absorb Fade")]
    [SerializeField] private ParticleSystem absorbFadeParticle;
    [Min(0f)] [SerializeField] private float particleFadeOutDuration = 0.6f;

    [Header("Audio")]
    [SerializeField] private AudioClip obstacleCollisionClip;
    [Range(0f, 1f)] [SerializeField] private float obstacleCollisionVolume = 1f;

    private bool consumed;
    private bool warnedMissingSootStatus;
    private ParticleFadeOutEffect particleFadeEffect;

    private void Awake()
    {
        ResolveCollider();
        ConfigureCollider();
    }

    private void OnValidate()
    {
        emotionIncreasePerTick = Mathf.Max(0f, emotionIncreasePerTick);
        emotionTickInterval = Mathf.Max(0.01f, emotionTickInterval);
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

        PlayerSootStatus sootStatus =
            player.Statuses != null ? player.Statuses.SootStatus : null;
        if (sootStatus != null)
        {
            sootStatus.RefreshSootEffect(
                emotionIncreasePerTick,
                emotionTickInterval);
        }
        else
        {
            WarnMissingSootStatus(player);
        }

        AudioManager.Instance?.PlaySfxOneShot(obstacleCollisionClip, obstacleCollisionVolume);
        Consume();
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
        if (sootCollider != null)
        {
            sootCollider.isTrigger = true;
        }
    }

    private void ResolveCollider()
    {
        if (sootCollider == null)
        {
            sootCollider = GetComponent<Collider2D>();
        }
    }

    private void WarnMissingSootStatus(PlayerController player)
    {
        if (warnedMissingSootStatus)
        {
            return;
        }

        Debug.LogWarning(
            "[SootObstacle] Assign PlayerSootStatus on the player's PlayerStatusManager to enable the lingering soot effect.",
            player);
        warnedMissingSootStatus = true;
    }

    private void Consume()
    {
        consumed = true;
        SetSootColliderEnabled(false);
        FadeParticleEffects();
    }

    private void SetSootColliderEnabled(bool enabled)
    {
        if (sootCollider != null)
        {
            sootCollider.enabled = enabled;
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
