using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerDriftingWispStatus : MonoBehaviour, IManualStatusEscapeSource
{
    [Header("Effect")]
    [SerializeField] private ParticleSystem wispParticle;

    [Header("Effect Timing")]
    [Min(0f)] [SerializeField] private float fadeInDuration = 0.25f;
    [Min(0f)] [SerializeField] private float fadeOutDuration = 0.6f;

    private PlayerController player;
    private PlayerStatusManager statusManager;
    private CharacterAbility affectedAbility;
    private ParticleStatusFadeEffect particleFadeEffect;
    private bool isActive;
    private bool warnedMissingParticle;
    private int activeChargeDrain;
    private float activeChargeDrainInterval;
    private float glideCleanseDuration;
    private float nextDrainTime;
    private float emotionIncreasePerTick;
    private float emotionTickInterval;
    private float nextEmotionTickTime;
    private float glideCleanseTimer;

    public bool IsActive => isActive;
    public bool IsManualEscapeActive => isActive;
    public bool IsManualEscapeVisible => isActive;
    public float ManualEscapeProgress01 => glideCleanseDuration <= 0f
        ? (isActive ? 1f : 0f)
        : Mathf.Clamp01(glideCleanseTimer / glideCleanseDuration);

    private void Awake()
    {
        ResolvePlayer();
        InitializeParticleFadeEffect();
    }

    private void OnValidate()
    {
        fadeInDuration = Mathf.Max(0f, fadeInDuration);
        fadeOutDuration = Mathf.Max(0f, fadeOutDuration);
    }

    private void Update()
    {
        if (!isActive)
        {
            return;
        }

        if (player == null)
        {
            ResolvePlayer();
            if (player == null)
            {
                ClearEffect();
                return;
            }
        }

        if (UpdateGlideCleanse())
        {
            return;
        }

        DrainActiveChargeWhenDue();
        AddEmotionWhenDue();
    }

    private void OnDisable()
    {
        ClearEffectImmediately();
    }

    private void OnDestroy()
    {
        ClearEffectImmediately();
        particleFadeEffect?.Release();
    }

    public bool TryApplyWispEffect(
        float movementMultiplier,
        int chargeDrain,
        float drainInterval,
        float glideDuration,
        float emotionIncrease,
        float tickInterval)
    {
        if (isActive)
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
        glideCleanseDuration = Mathf.Max(0f, glideDuration);
        nextDrainTime = Time.time + activeChargeDrainInterval;
        emotionIncreasePerTick = Mathf.Max(0f, emotionIncrease);
        emotionTickInterval = Mathf.Max(0.01f, tickInterval);
        nextEmotionTickTime = Time.time + emotionTickInterval;
        glideCleanseTimer = 0f;
        isActive = true;

        player.AddMovementModifier(
            this,
            Mathf.Clamp01(movementMultiplier),
            1f);
        ResolveAbility();
        ShowParticle();
        return true;
    }

    private bool UpdateGlideCleanse()
    {
        if (!player.IsGliding)
        {
            glideCleanseTimer = 0f;
            return false;
        }

        if (glideCleanseDuration <= 0f)
        {
            ClearEffect();
            return true;
        }

        glideCleanseTimer += Time.deltaTime;
        if (glideCleanseTimer < glideCleanseDuration)
        {
            return false;
        }

        ClearEffect();
        return true;
    }

    private void DrainActiveChargeWhenDue()
    {
        if (Time.time < nextDrainTime)
        {
            return;
        }

        ResolveAbility();
        if (activeChargeDrain > 0)
        {
            affectedAbility?.TryDrainActiveCharges(activeChargeDrain);
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

    private void ClearEffect()
    {
        ClearGameplayState();

        if (particleFadeEffect != null)
        {
            particleFadeEffect.Hide(fadeOutDuration);
        }
    }

    private void ClearEffectImmediately()
    {
        ClearGameplayState();
        particleFadeEffect?.ClearImmediately();
    }

    private void ClearGameplayState()
    {
        if (player != null)
        {
            player.RemoveMovementModifier(this);
        }

        affectedAbility = null;
        activeChargeDrain = 0;
        activeChargeDrainInterval = 0f;
        glideCleanseDuration = 0f;
        nextDrainTime = 0f;
        emotionIncreasePerTick = 0f;
        emotionTickInterval = 0f;
        nextEmotionTickTime = 0f;
        glideCleanseTimer = 0f;
        isActive = false;
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
        if (affectedAbility != null || player == null)
        {
            return;
        }

        ResolveStatusManager();
        affectedAbility = statusManager != null ? statusManager.Ability : null;
    }

    private void ShowParticle()
    {
        if (wispParticle == null)
        {
            WarnMissingParticle();
            return;
        }

        InitializeParticleFadeEffect();
        particleFadeEffect.Show(fadeInDuration);
    }

    private void InitializeParticleFadeEffect()
    {
        if (particleFadeEffect != null)
        {
            return;
        }

        particleFadeEffect =
            gameObject.AddComponent<ParticleStatusFadeEffect>();
        particleFadeEffect.Configure(wispParticle);
    }

    private void WarnMissingParticle()
    {
        if (warnedMissingParticle)
        {
            return;
        }

        Debug.LogWarning(
            "[PlayerDriftingWispStatus] Assign the player's drifting-wisp ParticleSystem.",
            this);
        warnedMissingParticle = true;
    }
}
