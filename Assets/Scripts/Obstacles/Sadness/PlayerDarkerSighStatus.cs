using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerDarkerSighStatus : MonoBehaviour
{
    [Header("Effect")]
    [SerializeField] private ParticleSystem slowdownParticleSystem;

    [Header("Effect Timing")]
    [Min(0f)] [SerializeField] private float fadeInDuration = 0.25f;
    [Min(0f)] [SerializeField] private float fadeOutDuration = 0.6f;

    private PlayerController player;
    private PlayerStatusManager statusManager;
    private ParticleStatusFadeEffect particleFadeEffect;
    private Coroutine effectLifetimeRoutine;
    private bool warnedMissingEffect;

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

    private void OnDisable()
    {
        ClearImmediately();
    }

    private void OnDestroy()
    {
        ClearImmediately();
        particleFadeEffect?.Release();
    }

    public void RefreshSlow(float movementMultiplier, float duration)
    {
        ResolvePlayer();
        if (player == null)
        {
            return;
        }

        float clampedDuration = Mathf.Max(0f, duration);
        player.ApplyTimedMovementModifier(
            this,
            Mathf.Clamp01(movementMultiplier),
            1f,
            clampedDuration);

        RestartEffectLifetime(clampedDuration);
        ShowEffect();
    }

    private void RestartEffectLifetime(float duration)
    {
        StopEffectLifetimeRoutine();
        effectLifetimeRoutine =
            StartCoroutine(EffectLifetimeRoutine(duration));
    }

    private IEnumerator EffectLifetimeRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        effectLifetimeRoutine = null;

        if (particleFadeEffect != null)
        {
            particleFadeEffect.Hide(fadeOutDuration);
        }
    }

    private void ShowEffect()
    {
        if (slowdownParticleSystem == null)
        {
            WarnMissingEffect();
            return;
        }

        InitializeParticleFadeEffect();
        particleFadeEffect.Show(fadeInDuration);
    }

    private void ClearImmediately()
    {
        if (player != null)
        {
            player.RemoveMovementModifier(this);
        }

        StopEffectLifetimeRoutine();
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
        particleFadeEffect.Configure(slowdownParticleSystem);
    }

    private void StopEffectLifetimeRoutine()
    {
        if (effectLifetimeRoutine == null)
        {
            return;
        }

        StopCoroutine(effectLifetimeRoutine);
        effectLifetimeRoutine = null;
    }

    private void WarnMissingEffect()
    {
        if (warnedMissingEffect)
        {
            return;
        }

        Debug.LogWarning(
            "[PlayerDarkerSighStatus] Assign the player's Darker Sigh particle system.",
            this);
        warnedMissingEffect = true;
    }
}
