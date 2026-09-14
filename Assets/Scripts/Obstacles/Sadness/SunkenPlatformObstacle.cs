using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class SunkenPlatformObstacle : MonoBehaviour
{
    [Header("Collision")]
    [Tooltip("Invisible stationary trigger. The seamless terrain remains the real ground.")]
    [SerializeField] private Collider2D platformTrigger;

    [Header("Escape")]
    [Tooltip("Seconds Space must be held continuously to escape.")]
    [Min(0f)] [SerializeField] private float escapeHoldDuration = 2f;

    [Header("Ability Drain")]
    [Min(0)] [SerializeField] private int activeChargeDrain = 1;
    [Min(0.01f)] [SerializeField] private float activeChargeDrainInterval = 1f;

    [Header("Emotion")]
    [Min(0f)] [SerializeField] private float emotionIncreaseOnHit = 1f;

    [Header("Audio")]
    [SerializeField] private AudioClip obstacleCollisionClip;
    [Range(0f, 1f)] [SerializeField] private float obstacleCollisionVolume = 1f;

    private PlayerSunkenPlatformStatus trappedStatus;
    private bool consumed;
    private bool warnedMissingStatus;

    public bool IsConsumed => consumed;

    private void Awake()
    {
        ResolveTrigger();
        ConfigureTrigger();
    }

    private void OnValidate()
    {
        escapeHoldDuration = Mathf.Max(0f, escapeHoldDuration);
        activeChargeDrain = Mathf.Max(0, activeChargeDrain);
        activeChargeDrainInterval = Mathf.Max(0.01f, activeChargeDrainInterval);
        emotionIncreaseOnHit = Mathf.Max(0f, emotionIncreaseOnHit);
        obstacleCollisionVolume = Mathf.Clamp01(obstacleCollisionVolume);
        ResolveTrigger();
        ConfigureTrigger();
    }

    private void OnDisable()
    {
        ReleaseTrappedPlayer();
    }

    private void OnDestroy()
    {
        ReleaseTrappedPlayer();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryTrapPlayer(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryTrapPlayer(other);
    }

    public void NotifyTrapReleased(PlayerSunkenPlatformStatus status)
    {
        if (trappedStatus == status)
        {
            trappedStatus = null;
        }
    }

    private void TryTrapPlayer(Collider2D other)
    {
        if (consumed || other == null)
        {
            return;
        }

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null ||
            !player.IsCharacterCollider(other) ||
            player.IsGroundPounding ||
            player.IsAirborne)
        {
            return;
        }

        PlayerSunkenPlatformStatus status =
            player.Statuses != null ? player.Statuses.SunkenPlatformStatus : null;
        if (status == null)
        {
            WarnMissingStatus(player);
            return;
        }

        if (!status.TryTrap(
                this,
                activeChargeDrain,
                activeChargeDrainInterval,
                escapeHoldDuration))
        {
            return;
        }

        consumed = true;
        trappedStatus = status;
        AddHitEmotion();
        AudioManager.Instance?.PlaySfxOneShot(obstacleCollisionClip, obstacleCollisionVolume);
        SetTriggerEnabled(false);
    }

    private void AddHitEmotion()
    {
        if (EmotionMeter.Instance != null && emotionIncreaseOnHit > 0f)
        {
            EmotionMeter.Instance.AddObstacleEmotion(emotionIncreaseOnHit);
        }
    }

    private void ReleaseTrappedPlayer()
    {
        PlayerSunkenPlatformStatus status = trappedStatus;
        trappedStatus = null;
        status?.ReleaseFrom(this);
    }

    private void ResolveTrigger()
    {
        if (platformTrigger == null)
        {
            platformTrigger = GetComponent<Collider2D>();
        }
    }

    private void ConfigureTrigger()
    {
        if (platformTrigger != null)
        {
            platformTrigger.isTrigger = true;
        }
    }

    private void SetTriggerEnabled(bool enabled)
    {
        if (platformTrigger != null)
        {
            platformTrigger.enabled = enabled;
        }
    }

    private void WarnMissingStatus(PlayerController player)
    {
        if (warnedMissingStatus)
        {
            return;
        }

        Debug.LogWarning(
            "[SunkenPlatformObstacle] Assign PlayerSunkenPlatformStatus on the player's PlayerStatusManager before using sunken terrain traps.",
            player);
        warnedMissingStatus = true;
    }
}
