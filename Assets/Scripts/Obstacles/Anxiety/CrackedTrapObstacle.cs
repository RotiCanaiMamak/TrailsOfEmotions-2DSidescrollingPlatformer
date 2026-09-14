using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class CrackedTrapObstacle : MonoBehaviour
{
    [Header("Collision")]
    [Tooltip("Trigger collider that catches the grounded player.")]
    [SerializeField] private Collider2D trapTrigger;

    [Header("Escape")]
    [Tooltip("Progress needed before repeated jump taps break the player free.")]
    [Min(0f)] [SerializeField] private float requiredProgress = 1f;
    [Tooltip("Progress added by each jump tap.")]
    [Min(0f)] [SerializeField] private float tapProgressAmount = 0.18f;
    [Tooltip("Progress lost per second while the player remains trapped.")]
    [Min(0f)] [SerializeField] private float decayPerSecond = 0.35f;

    [Header("Emotion")]
    [Min(0f)] [SerializeField] private float emotionIncreaseOnHit = 1f;

    [Header("Audio")]
    [SerializeField] private AudioClip obstacleCollisionClip;
    [Range(0f, 1f)] [SerializeField] private float obstacleCollisionVolume = 1f;

    [Header("Sprite Variant")]
    [SerializeField] private ObstacleSpriteVariantRandomizer spriteVariantRandomizer;

    private PlayerCrackedTrapStatus trappedStatus;
    private bool consumed;
    private bool warnedMissingStatus;

    public bool IsConsumed => consumed;

    private void Reset()
    {
        ResolveTrigger();
        ResolveSpriteVariantRandomizer();
    }

    private void Awake()
    {
        ResolveTrigger();
        ResolveSpriteVariantRandomizer();
        ConfigureTrigger();
    }

    private void OnValidate()
    {
        requiredProgress = Mathf.Max(0f, requiredProgress);
        tapProgressAmount = Mathf.Max(0f, tapProgressAmount);
        decayPerSecond = Mathf.Max(0f, decayPerSecond);
        emotionIncreaseOnHit = Mathf.Max(0f, emotionIncreaseOnHit);
        obstacleCollisionVolume = Mathf.Clamp01(obstacleCollisionVolume);
        ResolveTrigger();
        ResolveSpriteVariantRandomizer();
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

    public void NotifyTrapReleased(PlayerCrackedTrapStatus status)
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

        PlayerCrackedTrapStatus status =
            player.Statuses != null ? player.Statuses.CrackedTrapStatus : null;
        if (status == null)
        {
            WarnMissingStatus(player);
            return;
        }

        if (!status.TryTrap(
                this,
                requiredProgress,
                tapProgressAmount,
                decayPerSecond))
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
        PlayerCrackedTrapStatus status = trappedStatus;
        trappedStatus = null;
        status?.ReleaseFrom(this);
    }

    private void ResolveTrigger()
    {
        if (trapTrigger == null)
        {
            trapTrigger = GetComponent<Collider2D>();
        }
    }

    private void ResolveSpriteVariantRandomizer()
    {
        if (spriteVariantRandomizer == null)
        {
            spriteVariantRandomizer =
                GetComponentInChildren<ObstacleSpriteVariantRandomizer>(true);
        }
    }

    private void ConfigureTrigger()
    {
        if (trapTrigger != null)
        {
            trapTrigger.isTrigger = true;
        }
    }

    private void SetTriggerEnabled(bool enabled)
    {
        if (trapTrigger != null)
        {
            trapTrigger.enabled = enabled;
        }
    }

    private void WarnMissingStatus(PlayerController player)
    {
        if (warnedMissingStatus)
        {
            return;
        }

        Debug.LogWarning(
            "[CrackedTrapObstacle] Assign PlayerCrackedTrapStatus on the player's PlayerStatusManager before using cracked ground traps.",
            player);
        warnedMissingStatus = true;
    }
}
