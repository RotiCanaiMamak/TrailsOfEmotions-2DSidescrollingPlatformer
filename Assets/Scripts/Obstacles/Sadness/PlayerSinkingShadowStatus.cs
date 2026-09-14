using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerSinkingShadowStatus : MonoBehaviour, IManualStatusEscapeSource
{
    [Header("Effect")]
    [SerializeField] private ParticleSystem[] trappedParticles;

    [Header("Effect Timing")]
    [Min(0f)] [SerializeField] private float fadeInDuration = 0.25f;
    [Min(0f)] [SerializeField] private float fadeOutDuration = 0.6f;

    [Header("Escape Jitter")]
    [Tooltip("Visual-only child that contains the character artwork. Never assign the Player/physics root.")]
    [SerializeField] private Transform characterVisualRoot;
    [Min(0f)] [SerializeField] private float escapeJitterAmplitude = 0.06f;
    [Min(0f)] [SerializeField] private float escapeJitterFrequency = 35f;

    private PlayerController player;
    private PlayerStatusManager statusManager;
    private SinkingShadowObstacle activeShadow;
    private ParticleStatusFadeEffect particleFadeEffect;
    private Vector3 visualRestLocalPosition;
    private float escapeHoldDuration;
    private float escapeHoldProgress;
    private float escapeJitterSeed;
    private bool isTrapped;
    private bool visualRestPositionCached;
    private bool warnedMissingEffect;

    public bool IsTrapped => isTrapped;
    public float EscapeProgress01 => escapeHoldDuration <= 0f
        ? (isTrapped ? 1f : 0f)
        : Mathf.Clamp01(escapeHoldProgress / escapeHoldDuration);
    public bool IsManualEscapeActive => isTrapped;
    public bool IsManualEscapeVisible => isTrapped;
    public float ManualEscapeProgress01 => EscapeProgress01;

    private void Awake()
    {
        ResolvePlayer();
        ResolveCharacterVisualRoot();
        CacheVisualRestPosition();
        RestoreVisualImmediately();
        InitializeParticleFadeEffect();
    }

    private void OnValidate()
    {
        fadeInDuration = Mathf.Max(0f, fadeInDuration);
        fadeOutDuration = Mathf.Max(0f, fadeOutDuration);
        escapeJitterAmplitude = Mathf.Max(0f, escapeJitterAmplitude);
        escapeJitterFrequency = Mathf.Max(0f, escapeJitterFrequency);
    }

    private void Update()
    {
        if (!isTrapped)
        {
            return;
        }

        if (activeShadow == null)
        {
            ClearTrapGameplayState();
            return;
        }

        UpdateEscapeProgress();
    }

    private void OnDisable()
    {
        ClearTrapAndNotifyShadow();
        ClearTrappedEffectImmediately();
    }

    private void OnDestroy()
    {
        ClearTrapAndNotifyShadow();
        ClearTrappedEffectImmediately();
        particleFadeEffect?.Release();
    }

    public bool TryTrap(SinkingShadowObstacle source, float requiredEscapeHoldDuration)
    {
        if (isTrapped || source == null)
        {
            return false;
        }

        ResolvePlayer();
        if (player == null)
        {
            return false;
        }

        activeShadow = source;
        escapeHoldDuration = Mathf.Max(0f, requiredEscapeHoldDuration);
        escapeHoldProgress = 0f;
        escapeJitterSeed = Random.value * 1000f;
        isTrapped = true;
        ResolveCharacterVisualRoot();
        CacheVisualRestPosition();
        player.SetMovementRooted(this, true);
        ShowTrappedEffect();
        return true;
    }

    public void ReleaseFrom(SinkingShadowObstacle source)
    {
        if (!isTrapped || source == null || activeShadow != source)
        {
            return;
        }

        ClearTrapGameplayState();
    }

    private void UpdateEscapeProgress()
    {
        if (escapeHoldDuration <= 0f)
        {
            CompleteEscape();
            return;
        }

        if (Input.GetKey(KeyCode.Space))
        {
            escapeHoldProgress += Time.deltaTime;
            ApplyEscapeJitter();
        }
        else if (escapeHoldProgress > 0f)
        {
            escapeHoldProgress = 0f;
            RestoreVisualImmediately();
            return;
        }

        if (escapeHoldProgress >= escapeHoldDuration)
        {
            CompleteEscape();
        }
    }

    private void CompleteEscape()
    {
        if (!isTrapped)
        {
            return;
        }

        SinkingShadowObstacle releasedShadow = activeShadow;
        RestoreVisualImmediately();
        ClearTrapGameplayState(true);
        releasedShadow?.NotifyTrapReleased(this);
    }

    private void ClearTrapAndNotifyShadow()
    {
        if (!isTrapped)
        {
            return;
        }

        SinkingShadowObstacle releasedShadow = activeShadow;
        ClearTrapGameplayState(false);
        releasedShadow?.NotifyTrapReleased(this);
    }

    private void ClearTrapGameplayState(bool fadeOutEffect)
    {
        if (player != null)
        {
            player.SetMovementRooted(this, false);
        }

        RestoreVisualImmediately();
        activeShadow = null;
        escapeHoldDuration = 0f;
        escapeHoldProgress = 0f;
        isTrapped = false;

        if (fadeOutEffect)
        {
            HideTrappedEffect();
        }
        else
        {
            ClearTrappedEffectImmediately();
        }
    }

    private void ClearTrapGameplayState()
    {
        ClearTrapGameplayState(true);
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

    private void ResolveCharacterVisualRoot()
    {
        if (HasValidVisualRoot())
        {
            return;
        }

        ResolveStatusManager();
        PlayerSunkenPlatformStatus sunkenStatus =
            statusManager != null ? statusManager.SunkenPlatformStatus : null;
        if (sunkenStatus != null)
        {
            characterVisualRoot = sunkenStatus.CharacterVisualRoot;
        }
    }

    private bool HasValidVisualRoot()
    {
        return characterVisualRoot != null &&
            player != null &&
            characterVisualRoot != player.transform &&
            characterVisualRoot.IsChildOf(player.transform);
    }

    private void CacheVisualRestPosition()
    {
        if (!HasValidVisualRoot() || visualRestPositionCached)
        {
            return;
        }

        visualRestLocalPosition = characterVisualRoot.localPosition;
        visualRestPositionCached = true;
    }

    private void ApplyEscapeJitter()
    {
        if (!HasValidVisualRoot())
        {
            ResolveCharacterVisualRoot();
        }

        CacheVisualRestPosition();
        if (!HasValidVisualRoot() || !visualRestPositionCached)
        {
            return;
        }

        characterVisualRoot.localPosition =
            visualRestLocalPosition + GetEscapeJitterOffset();
    }

    private Vector3 GetEscapeJitterOffset()
    {
        if (escapeJitterAmplitude <= 0f || escapeJitterFrequency <= 0f)
        {
            return Vector3.zero;
        }

        float sample = Time.time * escapeJitterFrequency;
        float x = Mathf.PerlinNoise(escapeJitterSeed, sample) * 2f - 1f;
        float y = Mathf.PerlinNoise(escapeJitterSeed + 31.37f, sample) * 2f - 1f;
        return new Vector3(x, y, 0f) * escapeJitterAmplitude;
    }

    private void RestoreVisualImmediately()
    {
        if (HasValidVisualRoot() && visualRestPositionCached)
        {
            characterVisualRoot.localPosition = visualRestLocalPosition;
        }
    }

    private void ShowTrappedEffect()
    {
        if (GetAssignedTrappedParticleCount() == 0)
        {
            WarnMissingEffect();
            return;
        }

        InitializeParticleFadeEffect();
        particleFadeEffect.Show(fadeInDuration);
    }

    private void HideTrappedEffect()
    {
        if (particleFadeEffect != null)
        {
            particleFadeEffect.Hide(fadeOutDuration);
        }
    }

    private void ClearTrappedEffectImmediately()
    {
        particleFadeEffect?.ClearImmediately();
    }

    private void InitializeParticleFadeEffect()
    {
        if (particleFadeEffect != null)
        {
            return;
        }

        particleFadeEffect =
            gameObject.AddComponent<ParticleStatusFadeEffect>();
        particleFadeEffect.Configure(trappedParticles);
    }

    private int GetAssignedTrappedParticleCount()
    {
        if (trappedParticles == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < trappedParticles.Length; i++)
        {
            if (trappedParticles[i] != null)
            {
                count++;
            }
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
            "[PlayerSinkingShadowStatus] Assign the player's sinking-shadow trapped ParticleSystem.",
            this);
        warnedMissingEffect = true;
    }
}
