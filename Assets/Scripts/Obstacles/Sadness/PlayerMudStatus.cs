using UnityEngine;
using UnityEngine.Serialization;

public class PlayerMudStatus : MonoBehaviour
{
    [Header("Effect")]
    [SerializeField] private ParticleSystem mudParticleSystem;

    [Header("Effect Timing")]
    [Min(0f)] [SerializeField] private float fadeInDuration = 0.25f;
    [Min(0f)] [SerializeField] private float fadeOutDuration = 0.6f;

    [Header("Duration")]
    [Tooltip("Seconds the mud slowdown remains active after the latest mud contact.")]
    [FormerlySerializedAs("glideCleanseDuration")]
    [Min(0f)] [SerializeField] private float effectDuration = 3f;

    private PlayerController player;
    private PlayerStatusManager statusManager;
    private ParticleStatusFadeEffect particleFadeEffect;
    private bool warnedMissingEffect;
    private bool hasActiveEffect;
    private float effectTimer;

    private void Awake()
    {
        ResolvePlayer();
        InitializeParticleFadeEffect();
    }

    private void OnValidate()
    {
        fadeInDuration = Mathf.Max(0f, fadeInDuration);
        fadeOutDuration = Mathf.Max(0f, fadeOutDuration);
        effectDuration = Mathf.Max(0f, effectDuration);
    }

    private void Update()
    {
        if (!hasActiveEffect)
        {
            return;
        }

        if (effectDuration <= 0f)
        {
            Cleanse();
            return;
        }

        effectTimer += Time.deltaTime;
        if (effectTimer >= effectDuration)
        {
            Cleanse();
        }
    }

    private void OnDisable()
    {
        ClearImmediately();
    }

    private void OnDestroy()
    {
        ClearImmediately();
        particleFadeEffect?.Release();
    }

    public void RefreshMudEffect(
        float speedMultiplier,
        float jumpForceMultiplier)
    {
        ResolvePlayer();
        if (player == null)
        {
            return;
        }

        player.AddMovementModifier(
            this,
            Mathf.Max(0f, speedMultiplier),
            1f,
            jumpForceMultiplier: Mathf.Max(0f, jumpForceMultiplier));

        hasActiveEffect = true;
        effectTimer = 0f;

        if (mudParticleSystem == null)
        {
            WarnMissingEffect();
            return;
        }

        InitializeParticleFadeEffect();
        particleFadeEffect.Show(fadeInDuration);
    }

    private void Cleanse()
    {
        ClearGameplayState();

        if (particleFadeEffect != null)
        {
            particleFadeEffect.Hide(fadeOutDuration);
        }
    }

    private void ClearGameplayState()
    {
        if (player != null)
        {
            player.RemoveMovementModifier(this);
        }

        hasActiveEffect = false;
        effectTimer = 0f;
    }

    private void ClearImmediately()
    {
        ClearGameplayState();
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
        particleFadeEffect.Configure(mudParticleSystem);
    }

    private void WarnMissingEffect()
    {
        if (warnedMissingEffect)
        {
            return;
        }

        Debug.LogWarning(
            "[PlayerMudStatus] Assign one mud particle system before using mud puddles.",
            this);
        warnedMissingEffect = true;
    }
}
