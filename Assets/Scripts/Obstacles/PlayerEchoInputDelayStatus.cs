using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerEchoInputDelayStatus : MonoBehaviour
{
    [Header("Input Delay")]
    [Min(0f)] [SerializeField] private float inputDelayPerTouch = 0.2f;
    [Min(0f)] [SerializeField] private float maxInputDelay = 0.8f;
    [Min(0f)] [SerializeField] private float effectDuration = 3f;

    [Header("Screen Distortion")]
    [Min(0f)] [SerializeField] private float blurBoost = 0.35f;
    [Range(0f, 1f)] [SerializeField] private float vignetteIntensityBoost = 0.2f;

    private PlayerController player;
    private PlayerStatusManager statusManager;
    private AnxietyBlurPulseMaterialManager blurManager;
    private BiomeGlobalVolumeController volumeController;
    private bool hasActiveEffect;
    private bool warnedMissingBlurManager;
    private bool warnedMissingVolumeController;
    private float activeInputDelay;
    private float effectTimer;

    private void Awake()
    {
        ResolvePlayer();
    }

    private void OnValidate()
    {
        inputDelayPerTouch = Mathf.Max(0f, inputDelayPerTouch);
        maxInputDelay = Mathf.Max(0f, maxInputDelay);
        effectDuration = Mathf.Max(0f, effectDuration);
        blurBoost = Mathf.Max(0f, blurBoost);
        vignetteIntensityBoost = Mathf.Clamp01(vignetteIntensityBoost);
    }

    private void Update()
    {
        if (!hasActiveEffect)
        {
            return;
        }

        ApplyVisualBoosts();

        if (effectDuration <= 0f)
        {
            ClearGameplayState();
            return;
        }

        effectTimer += Time.deltaTime;
        if (effectTimer >= effectDuration)
        {
            ClearGameplayState();
        }
    }

    private void OnDisable()
    {
        ClearGameplayState();
    }

    private void OnDestroy()
    {
        ClearGameplayState();
    }

    public bool TryRefreshEchoEffect()
    {
        ResolvePlayer();
        if (player == null)
        {
            return false;
        }

        activeInputDelay = Mathf.Min(
            Mathf.Max(0f, maxInputDelay),
            activeInputDelay + Mathf.Max(0f, inputDelayPerTouch));
        effectTimer = 0f;
        hasActiveEffect = true;

        player.SetInputDelayModifier(this, activeInputDelay);
        ApplyVisualBoosts();

        if (effectDuration <= 0f)
        {
            ClearGameplayState();
        }

        return true;
    }

    private void ClearGameplayState()
    {
        if (player != null)
        {
            player.RemoveInputDelayModifier(this);
        }

        if (blurManager != null)
        {
            blurManager.RemoveAdditiveBlurBoost(this);
        }

        if (volumeController != null)
        {
            volumeController.RemoveVignetteIntensityBoost(this);
        }

        hasActiveEffect = false;
        activeInputDelay = 0f;
        effectTimer = 0f;
    }

    private void ApplyVisualBoosts()
    {
        if (blurBoost > 0f)
        {
            ResolveBlurManager();
            if (blurManager != null)
            {
                blurManager.SetAdditiveBlurBoost(this, blurBoost);
            }
        }
        else if (blurManager != null)
        {
            blurManager.RemoveAdditiveBlurBoost(this);
        }

        if (vignetteIntensityBoost > 0f)
        {
            ResolveVolumeController();
            if (volumeController != null)
            {
                volumeController.SetVignetteIntensityBoost(this, vignetteIntensityBoost);
            }
        }
        else if (volumeController != null)
        {
            volumeController.RemoveVignetteIntensityBoost(this);
        }
    }

    private void ResolvePlayer()
    {
        if (player != null)
        {
            return;
        }

        ResolveStatusManager();
        player = statusManager != null ? statusManager.Player : null;
    }

    private void ResolveStatusManager()
    {
        if (statusManager == null)
        {
            statusManager = GetComponent<PlayerStatusManager>();
        }
    }

    private void ResolveBlurManager()
    {
        if (blurManager != null)
        {
            return;
        }

        blurManager = FindFirstObjectByType<AnxietyBlurPulseMaterialManager>();
        if (blurManager == null)
        {
            WarnMissingBlurManager();
        }
        else
        {
            warnedMissingBlurManager = false;
        }
    }

    private void ResolveVolumeController()
    {
        if (volumeController != null)
        {
            return;
        }

        volumeController = FindFirstObjectByType<BiomeGlobalVolumeController>();
        if (volumeController == null)
        {
            WarnMissingVolumeController();
        }
        else
        {
            warnedMissingVolumeController = false;
        }
    }

    private void WarnMissingBlurManager()
    {
        if (warnedMissingBlurManager)
        {
            return;
        }

        Debug.LogWarning(
            "[PlayerEchoInputDelayStatus] No AnxietyBlurPulseMaterialManager found for Echo blur boost.",
            this);
        warnedMissingBlurManager = true;
    }

    private void WarnMissingVolumeController()
    {
        if (warnedMissingVolumeController)
        {
            return;
        }

        Debug.LogWarning(
            "[PlayerEchoInputDelayStatus] No BiomeGlobalVolumeController found for Echo vignette boost.",
            this);
        warnedMissingVolumeController = true;
    }
}
