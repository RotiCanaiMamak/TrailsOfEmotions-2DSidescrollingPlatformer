using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerBubbleStatus : MonoBehaviour
{
    [Header("Bubble Effect")]
    [SerializeField] private BubbleAmbientMotion bubbleMotion;
    [Range(0f, 1f)] [SerializeField] private float growStartScale = 0.1f;
    [Min(0f)] [SerializeField] private float growDuration = 0.35f;

    [Header("Release Effect")]
    [SerializeField] private ParticleSystem releaseBurstParticle;

    private PlayerController player;
    private PlayerStatusManager statusManager;
    private CharacterAbility trappedAbility;
    private int activeChargeDrain;
    private float activeChargeDrainInterval;
    private float nextDrainTime;
    private float emotionIncreasePerTick;
    private float emotionTickInterval;
    private float nextEmotionTickTime;
    private bool isTrapped;
    private bool warnedMissingBubbleMotion;
    private bool warnedMissingReleaseBurst;

    public bool IsTrapped => isTrapped;

    private void Awake()
    {
        ResolvePlayer();
        StopVisualsImmediately();
    }

    private void OnValidate()
    {
        growStartScale = Mathf.Clamp01(growStartScale);
        growDuration = Mathf.Max(0f, growDuration);
    }

    private void Update()
    {
        if (!isTrapped)
        {
            return;
        }

        if (player == null)
        {
            ResolvePlayer();
        }

        if (player == null)
        {
            ClearTrapGameplayState();
            StopBubbleImmediately();
            return;
        }

        DrainActiveChargeWhenDue();
        AddEmotionWhenDue();
    }

    private void OnDisable()
    {
        ClearTrapGameplayState();
        StopVisualsImmediately();
    }

    private void OnDestroy()
    {
        ClearTrapGameplayState();
        StopVisualsImmediately();
    }

    public bool TryTrap(
        int chargeDrain,
        float drainInterval,
        float emotionIncrease,
        float tickInterval)
    {
        if (isTrapped)
        {
            return false;
        }

        ResolvePlayer();
        if (player == null)
        {
            return false;
        }

        activeChargeDrain = Mathf.Max(0, chargeDrain);
        activeChargeDrainInterval = Mathf.Max(0.01f, drainInterval);
        nextDrainTime = Time.time + activeChargeDrainInterval;
        emotionIncreasePerTick = Mathf.Max(0f, emotionIncrease);
        emotionTickInterval = Mathf.Max(0.01f, tickInterval);
        nextEmotionTickTime = Time.time + emotionTickInterval;
        isTrapped = true;

        ResolveAbility();
        trappedAbility?.SetActiveAbilityBlocked(this, true);
        player.GroundPoundLanded += OnGroundPoundLanded;

        StopReleaseBurstImmediately();
        PlayBubbleGrowIn();
        return true;
    }

    private void OnGroundPoundLanded(PlayerController landedPlayer)
    {
        if (!isTrapped || landedPlayer == null || landedPlayer != player)
        {
            return;
        }

        ClearTrapGameplayState();
        StopBubbleImmediately();
        PlayReleaseBurst();
    }

    private void DrainActiveChargeWhenDue()
    {
        if (Time.time < nextDrainTime)
        {
            return;
        }

        if (trappedAbility == null)
        {
            ResolveAbility();
            trappedAbility?.SetActiveAbilityBlocked(this, true);
        }

        if (activeChargeDrain > 0)
        {
            trappedAbility?.TryDrainActiveCharges(activeChargeDrain);
        }

        nextDrainTime = Time.time + activeChargeDrainInterval;
    }

    private void AddEmotionWhenDue()
    {
        if (EmotionMeter.Instance == null ||
            emotionIncreasePerTick <= 0f ||
            Time.time < nextEmotionTickTime)
        {
            return;
        }

        EmotionMeter.Instance.AddObstacleEmotion(emotionIncreasePerTick);
        nextEmotionTickTime = Time.time + emotionTickInterval;
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

    private void ResolveAbility()
    {
        if (player == null)
        {
            return;
        }

        ResolveStatusManager();
        trappedAbility = statusManager != null ? statusManager.Ability : null;
    }

    private void ClearTrapGameplayState()
    {
        if (player != null)
        {
            player.GroundPoundLanded -= OnGroundPoundLanded;
        }

        trappedAbility?.SetActiveAbilityBlocked(this, false);

        trappedAbility = null;
        activeChargeDrain = 0;
        activeChargeDrainInterval = 0f;
        nextDrainTime = 0f;
        emotionIncreasePerTick = 0f;
        emotionTickInterval = 0f;
        nextEmotionTickTime = 0f;
        isTrapped = false;
    }

    private void PlayBubbleGrowIn()
    {
        if (bubbleMotion == null)
        {
            WarnMissingBubbleMotion();
            return;
        }

        bubbleMotion.PlayGrowIn(growStartScale, growDuration);
    }

    private void StopBubbleImmediately()
    {
        if (bubbleMotion != null)
        {
            bubbleMotion.StopImmediately();
        }
    }

    private void PlayReleaseBurst()
    {
        if (releaseBurstParticle == null)
        {
            WarnMissingReleaseBurst();
            return;
        }

        if (!releaseBurstParticle.gameObject.activeSelf)
        {
            releaseBurstParticle.gameObject.SetActive(true);
        }

        releaseBurstParticle.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear);
        releaseBurstParticle.Play(true);
    }

    private void StopReleaseBurstImmediately()
    {
        if (releaseBurstParticle == null)
        {
            return;
        }

        releaseBurstParticle.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void StopVisualsImmediately()
    {
        StopBubbleImmediately();
        StopReleaseBurstImmediately();
    }

    private void WarnMissingBubbleMotion()
    {
        if (warnedMissingBubbleMotion)
        {
            return;
        }

        Debug.LogWarning(
            "[PlayerBubbleStatus] Assign the player's BubbleAmbientMotion before using bubble obstacles.",
            this);
        warnedMissingBubbleMotion = true;
    }

    private void WarnMissingReleaseBurst()
    {
        if (warnedMissingReleaseBurst)
        {
            return;
        }

        Debug.LogWarning(
            "[PlayerBubbleStatus] Assign a player-side release burst ParticleSystem.",
            this);
        warnedMissingReleaseBurst = true;
    }
}
