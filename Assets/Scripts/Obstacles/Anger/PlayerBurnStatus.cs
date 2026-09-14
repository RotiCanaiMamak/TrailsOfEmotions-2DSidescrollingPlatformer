using UnityEngine;

public class PlayerBurnStatus : MonoBehaviour, IManualStatusEscapeSource
{
    [Header("Effect")]
    [SerializeField] private ParticleSystem fireParticleSystem1;
    [SerializeField] private ParticleSystem fireParticleSystem2;
    [SerializeField] private ParticleSystem fireParticleSystem3;

    [Header("Effect Timing")]
    [Min(0f)] [SerializeField] private float fadeInDuration = 0.25f;
    [Min(0f)] [SerializeField] private float fadeOutDuration = 0.6f;

    [Header("Extinguish")]
    [Min(0f)] [SerializeField] private float extinguishGlideDuration = 0.6f;

    private PlayerController player;
    private PlayerStatusManager statusManager;
    private ParticleStatusFadeEffect particleFadeEffect;
    private bool isBurning;
    private bool warnedMissingEffect;
    private float emotionIncreasePerTick;
    private float emotionTickInterval;
    private float nextEmotionTickTime;
    private float glideExtinguishTimer;

    public bool IsBurning => isBurning;
    public bool IsManualEscapeActive => isBurning;
    public bool IsManualEscapeVisible => isBurning;
    public float ManualEscapeProgress01 => extinguishGlideDuration <= 0f
        ? (isBurning ? 1f : 0f)
        : Mathf.Clamp01(glideExtinguishTimer / extinguishGlideDuration);

    private void Reset()
    {
        ResolvePlayer();
    }

    private void Awake()
    {
        ResolvePlayer();
        InitializeParticleFadeEffect();
    }

    private void OnValidate()
    {
        fadeInDuration = Mathf.Max(0f, fadeInDuration);
        fadeOutDuration = Mathf.Max(0f, fadeOutDuration);
        extinguishGlideDuration = Mathf.Max(0f, extinguishGlideDuration);
    }

    private void Update()
    {
        if (!isBurning)
        {
            return;
        }

        ResolvePlayer();
        if (player == null)
        {
            return;
        }

        UpdateGlideExtinguish();
        if (!isBurning)
        {
            return;
        }

        AddEmotionWhenDue();
    }

    private void UpdateGlideExtinguish()
    {
        if (!player.IsGliding)
        {
            glideExtinguishTimer = 0f;
            return;
        }

        if (extinguishGlideDuration <= 0f)
        {
            Extinguish();
            return;
        }

        glideExtinguishTimer += Time.deltaTime;
        if (glideExtinguishTimer >= extinguishGlideDuration)
        {
            Extinguish();
        }
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

    private void OnDisable()
    {
        ClearBurnStateImmediately();
    }

    private void OnDestroy()
    {
        ClearBurnStateImmediately();
        particleFadeEffect?.Release();
    }

    public void Ignite(
        float burnSpeedMultiplier,
        float jumpForwardMultiplier,
        float emotionIncrease,
        float tickInterval)
    {
        ResolvePlayer();
        if (player == null)
        {
            return;
        }

        isBurning = true;
        glideExtinguishTimer = 0f;
        emotionIncreasePerTick = Mathf.Max(0f, emotionIncrease);
        emotionTickInterval = Mathf.Max(0.01f, tickInterval);
        nextEmotionTickTime = Time.time + emotionTickInterval;
        player.AddMovementModifier(
            this,
            burnSpeedMultiplier,
            1f,
            1f,
            0f,
            jumpForwardMultiplier);

        PlayFireEffect();
    }

    public void Extinguish()
    {
        ClearBurnGameplayState();

        if (particleFadeEffect != null)
        {
            particleFadeEffect.Hide(fadeOutDuration);
        }
    }

    private void PlayFireEffect()
    {
        if (GetAssignedParticleCount() == 0)
        {
            WarnMissingEffect();
            return;
        }

        InitializeParticleFadeEffect();
        particleFadeEffect.Show(fadeInDuration);
    }

    private void ClearBurnGameplayState()
    {
        if (player != null)
        {
            player.RemoveMovementModifier(this);
        }

        isBurning = false;
        glideExtinguishTimer = 0f;
        emotionIncreasePerTick = 0f;
        emotionTickInterval = 0f;
        nextEmotionTickTime = 0f;
    }

    private void ClearBurnStateImmediately()
    {
        ClearBurnGameplayState();
        particleFadeEffect?.ClearImmediately();
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

    private void InitializeParticleFadeEffect()
    {
        if (particleFadeEffect != null)
        {
            return;
        }

        particleFadeEffect =
            gameObject.AddComponent<ParticleStatusFadeEffect>();
        particleFadeEffect.Configure(
            fireParticleSystem1,
            fireParticleSystem2,
            fireParticleSystem3);
    }

    private int GetAssignedParticleCount()
    {
        int count = 0;
        if (fireParticleSystem1 != null)
        {
            count++;
        }

        if (fireParticleSystem2 != null)
        {
            count++;
        }

        if (fireParticleSystem3 != null)
        {
            count++;
        }

        return count;
    }

    private void WarnMissingEffect()
    {
        if (warnedMissingEffect)
        {
            return;
        }

        Debug.LogWarning(
            "[PlayerBurnStatus] Assign at least one fire particle system before igniting.",
            this);
        warnedMissingEffect = true;
    }
}
