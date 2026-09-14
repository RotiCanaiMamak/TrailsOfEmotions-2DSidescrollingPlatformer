using UnityEngine;

public class GrowingSteamVent : MonoBehaviour, IGroundPoundTarget, IBackpackAbsorbable, IRegulationClearable
{
    [Header("Collision")]
    [SerializeField] private Collider2D ventCollider;

    [Header("Launch")]
    [Min(0f)] [SerializeField] private float upwardLaunchSpeed = 18f;
    [SerializeField] private float horizontalLaunchSpeed = 0f;
    [Min(0f)] [SerializeField] private float adhesionLockDuration = 0.12f;
    [SerializeField] private bool disableGlideOnLaunch = true;
    [Min(0f)] [SerializeField] private float emotionIncreaseOnHit = 1f;

    [Header("Effects")]
    [SerializeField] private ParticleSystem eruptionParticles;
    [SerializeField] private ObstacleDestroyEffects destroyEffects;
    [SerializeField] private ObstacleSpriteVariantRandomizer spriteVariantRandomizer;

    [Header("Audio")]
    [SerializeField] private AudioClip eruptionOneShotClip;
    [Range(0f, 1f)] [SerializeField] private float eruptionOneShotVolume = 1f;

    private bool spent;

    private void Awake()
    {
        StopParticleEffect(eruptionParticles);
    }

    private void OnValidate()
    {
        upwardLaunchSpeed = Mathf.Max(0f, upwardLaunchSpeed);
        adhesionLockDuration = Mathf.Max(0f, adhesionLockDuration);
        emotionIncreaseOnHit = Mathf.Max(0f, emotionIncreaseOnHit);
        eruptionOneShotVolume = Mathf.Clamp01(eruptionOneShotVolume);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleNormalCollision(collision.collider);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        HandleNormalCollision(collision.collider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleNormalCollision(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        HandleNormalCollision(other);
    }

    public bool TryHandleGroundPound(PlayerController player, Vector2 impactPoint)
    {
        if (spent)
        {
            return false;
        }

        spent = true;
        SetVentColliderEnabled(false);
        PlayDestroyEffects();
        SwitchToActivatedSpriteVariant();
        return true;
    }

    public bool TryAbsorbByBackpack(LinaBackpackBulwark bulwark)
    {
        if (spent)
        {
            return false;
        }

        spent = true;
        SetVentColliderEnabled(false);
        PlayDestroyEffects();
        SwitchToActivatedSpriteVariant();
        return true;
    }

    public bool TryClearByRegulation(Object source)
    {
        return TryAbsorbByBackpack(null);
    }

    internal void HandleNormalCollision(Collider2D other)
    {
        if (spent || other == null)
        {
            return;
        }

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || !player.IsCharacterCollider(other) || player.IsGroundPounding)
        {
            return;
        }

        spent = true;
        SetVentColliderEnabled(false);
        PlayEruptionEffects();
        player.Launch(upwardLaunchSpeed, horizontalLaunchSpeed, adhesionLockDuration, disableGlideOnLaunch);
        SwitchToActivatedSpriteVariant();
        AddHitEmotion();
    }

    private void AddHitEmotion()
    {
        if (EmotionMeter.Instance != null && emotionIncreaseOnHit > 0f)
        {
            EmotionMeter.Instance.AddObstacleEmotion(emotionIncreaseOnHit);
        }
    }

    private void PlayEruptionEffects()
    {
        if (eruptionParticles != null)
        {
            eruptionParticles.Play();
        }

        AudioManager.Instance?.PlaySfxOneShot(eruptionOneShotClip, eruptionOneShotVolume);
    }

    private void PlayDestroyEffects()
    {
        destroyEffects?.PlayAtAssignedTransforms();
    }

    private void SwitchToActivatedSpriteVariant()
    {
        spriteVariantRandomizer?.SwitchToActivatedVariant();
    }

    private void StopParticleEffect(ParticleSystem particles)
    {
        if (particles == null)
        {
            return;
        }

        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void SetVentColliderEnabled(bool enabled)
    {
        if (ventCollider != null)
        {
            ventCollider.enabled = enabled;
        }
    }

}
