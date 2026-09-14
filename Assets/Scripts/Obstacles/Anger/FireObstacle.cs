using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Collider2D))]
public class FireObstacle : MonoBehaviour, IGroundPoundTarget, IBackpackAbsorbable, IRegulationClearable
{
    private const string PlayerTag = "Player";

    [Header("Collision")]
    [SerializeField] private Collider2D fireCollider;

    [Header("Indicator")]
    [SerializeField] private GameObject indicatorPrefab;
    [Min(0f)] [SerializeField] private float triggerDistance = 8f;
    [SerializeField] private float indicatorHeightOffset = 0f;

    [Header("Movement")]
    [Min(0f)] [SerializeField] private float burnSpeedMultiplier = 1.35f;
    [Min(0f)] [SerializeField] private float jumpForwardMultiplier = 1.75f;

    [Header("Emotion")]
    [FormerlySerializedAs("emotionIncreaseOnHit")]
    [Min(0f)] [SerializeField] private float emotionIncreasePerTick = 1f;
    [Min(0.01f)] [SerializeField] private float emotionTickInterval = 0.5f;

    [Header("Particles")]
    [SerializeField] private ParticleSystem fireParticle1;
    [SerializeField] private ParticleSystem fireParticle2;
    [SerializeField] private ParticleSystem fireParticle3;
    [SerializeField] private ParticleSystem fireParticle4;
    [Min(0f)] [SerializeField] private float particleFadeOutDuration = 0.6f;

    [Header("Audio")]
    [SerializeField] private AudioClip activationOneShotClip;
    [Range(0f, 1f)] [SerializeField] private float activationOneShotVolume = 1f;

    [Header("Destroy Effects")]
    [SerializeField] private ObstacleDestroyEffects destroyEffects;

    private bool consumed;
    private bool fireActivated;
    private bool warnedMissingBurnStatus;
    private bool warnedMissingPlayerTag;
    private Transform player;
    private GameObject indicatorInstance;
    private ProximityWarningIndicatorAnimator indicatorAnimator;
    private TerrainFeatureBottomRoot bottomRoot;
    private ParticleFadeOutEffect particleFadeEffect;

    private void Awake()
    {
        ResolveCollider();
        bottomRoot = GetComponentInChildren<TerrainFeatureBottomRoot>();
        StopFireParticles();
    }

    private void Start()
    {
        fireActivated = false;
        SetFireColliderEnabled(false);
        StopFireParticles();
        SpawnIndicator();
    }

    private void Update()
    {
        if (consumed || fireActivated)
        {
            return;
        }

        ResolvePlayer();
        if (player == null)
        {
            return;
        }

        UpdateIndicatorThreatProgress();
        Vector3 indicatorPosition = GetIndicatorWorldPosition();
        if (player.position.x >= indicatorPosition.x - triggerDistance)
        {
            ActivateFire();
        }
    }

    private void OnDestroy()
    {
        if (indicatorInstance != null)
        {
            Destroy(indicatorInstance);
        }
    }

    private void OnValidate()
    {
        triggerDistance = Mathf.Max(0f, triggerDistance);
        burnSpeedMultiplier = Mathf.Max(0f, burnSpeedMultiplier);
        jumpForwardMultiplier = Mathf.Max(0f, jumpForwardMultiplier);
        emotionIncreasePerTick = Mathf.Max(0f, emotionIncreasePerTick);
        emotionTickInterval = Mathf.Max(0.01f, emotionTickInterval);
        particleFadeOutDuration = Mathf.Max(0f, particleFadeOutDuration);
        activationOneShotVolume = Mathf.Clamp01(activationOneShotVolume);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleTouch(collision.collider);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        HandleTouch(collision.collider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleTouch(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        HandleTouch(other);
    }

    internal void HandleTouch(Collider2D other)
    {
        if (consumed || !fireActivated || other == null)
        {
            return;
        }

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || !player.IsCharacterCollider(other) || player.IsGroundPounding)
        {
            return;
        }

        PlayerBurnStatus burnStatus =
            player.Statuses != null ? player.Statuses.BurnStatus : null;
        if (burnStatus == null)
        {
            WarnMissingBurnStatus(player);
            return;
        }

        burnStatus.Ignite(
            burnSpeedMultiplier,
            jumpForwardMultiplier,
            emotionIncreasePerTick,
            emotionTickInterval);
        Consume();
    }

    public bool TryHandleGroundPound(PlayerController player, Vector2 impactPoint)
    {
        if (consumed || !fireActivated)
        {
            return false;
        }

        Consume();
        return true;
    }

    public bool TryAbsorbByBackpack(LinaBackpackBulwark bulwark)
    {
        if (consumed || !fireActivated)
        {
            return false;
        }

        Consume();
        return true;
    }

    public bool TryClearByRegulation(Object source)
    {
        return TryAbsorbByBackpack(null);
    }

    private void Consume()
    {
        consumed = true;
        SetFireColliderEnabled(false);
        DestroyIndicatorImmediately();
        HideObstacleRenderers();
        FadeFireParticles();
        PlayDestroyEffects();
        PlayChildAudio();
    }

    private void ActivateFire()
    {
        if (fireActivated || consumed)
        {
            return;
        }

        fireActivated = true;
        PlayIndicatorFinalWarning();
        PlayFireParticles();
        PlayActivationAudio();
        SetFireColliderEnabled(true);
    }

    private void ResolveCollider()
    {
        if (fireCollider == null)
        {
            fireCollider = GetComponent<Collider2D>();
        }
    }

    private void SetFireColliderEnabled(bool enabled)
    {
        if (fireCollider != null)
        {
            fireCollider.enabled = enabled;
        }
    }

    private void SpawnIndicator()
    {
        if (indicatorPrefab == null)
        {
            return;
        }

        Vector3 indicatorPosition =
            GetIndicatorWorldPosition() + Vector3.up * indicatorHeightOffset;
        indicatorInstance = Instantiate(
            indicatorPrefab,
            indicatorPosition,
            GetIndicatorRotation(),
            transform);
        ConfigureIndicatorAnimator();
    }

    private void ConfigureIndicatorAnimator()
    {
        if (indicatorInstance == null)
        {
            indicatorAnimator = null;
            return;
        }

        indicatorAnimator =
            indicatorInstance.GetComponent<ProximityWarningIndicatorAnimator>();
        if (indicatorAnimator == null)
        {
            indicatorAnimator =
                indicatorInstance.AddComponent<ProximityWarningIndicatorAnimator>();
        }
    }

    private void UpdateIndicatorThreatProgress()
    {
        if (indicatorAnimator == null || player == null)
        {
            return;
        }

        Vector3 indicatorPosition = GetIndicatorWorldPosition();
        float distanceToIndicator = Mathf.Max(0f, indicatorPosition.x - player.position.x);
        indicatorAnimator.SetThreatDistance(distanceToIndicator, triggerDistance);
    }

    private Vector3 GetIndicatorWorldPosition()
    {
        if (bottomRoot != null)
        {
            return bottomRoot.transform.position;
        }

        Vector3 fallbackPosition = transform.position;
        if (TerrainManager.Instance != null &&
            TerrainManager.Instance.TryGetSurfaceAtX(
                fallbackPosition.x,
                out Vector3 surfacePosition,
                out _))
        {
            return surfacePosition;
        }

        return fallbackPosition;
    }

    private Quaternion GetIndicatorRotation()
    {
        Vector3 fallbackPosition = bottomRoot != null
            ? bottomRoot.transform.position
            : transform.position;
        if (TerrainManager.Instance != null &&
            TerrainManager.Instance.TryGetSurfaceAtX(
                fallbackPosition.x,
                out _,
                out Vector3 surfaceNormal))
        {
            return SurfaceNormalToRotation(surfaceNormal);
        }

        Vector3 fallbackNormal = bottomRoot != null
            ? bottomRoot.transform.up
            : transform.up;
        return SurfaceNormalToRotation(fallbackNormal);
    }

    private static Quaternion SurfaceNormalToRotation(Vector3 surfaceNormal)
    {
        Vector3 safeNormal = surfaceNormal.sqrMagnitude > 0.0001f
            ? surfaceNormal.normalized
            : Vector3.up;
        return Quaternion.Euler(
            0f,
            0f,
            Mathf.Atan2(safeNormal.x, safeNormal.y) * Mathf.Rad2Deg * -1f);
    }

    private void PlayIndicatorFinalWarning()
    {
        if (indicatorInstance == null)
        {
            return;
        }

        if (indicatorAnimator == null)
        {
            indicatorAnimator =
                indicatorInstance.GetComponent<ProximityWarningIndicatorAnimator>();
        }

        if (indicatorAnimator != null)
        {
            indicatorAnimator.PlayFinalWarningAndDestroy();
            return;
        }

        DestroyIndicatorImmediately();
    }

    private void DestroyIndicatorImmediately()
    {
        if (indicatorInstance == null)
        {
            return;
        }

        Destroy(indicatorInstance);
        indicatorInstance = null;
        indicatorAnimator = null;
    }

    private void StopFireParticles()
    {
        StopFireParticle(fireParticle1);
        StopFireParticle(fireParticle2);
        StopFireParticle(fireParticle3);
        StopFireParticle(fireParticle4);
    }

    private static void StopFireParticle(ParticleSystem particle)
    {
        if (particle == null)
        {
            return;
        }

        particle.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void PlayFireParticles()
    {
        PlayFireParticle(fireParticle1);
        PlayFireParticle(fireParticle2);
        PlayFireParticle(fireParticle3);
        PlayFireParticle(fireParticle4);
    }

    private static void PlayFireParticle(ParticleSystem particle)
    {
        if (particle == null)
        {
            return;
        }

        particle.Play(true);
    }

    private void PlayActivationAudio()
    {
        AudioManager.Instance?.PlaySfxOneShot(
            activationOneShotClip,
            activationOneShotVolume);
    }

    private void HideObstacleRenderers()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer rendererToHide = renderers[i];
            if (rendererToHide == null || ShouldKeepRendererVisible(rendererToHide))
            {
                continue;
            }

            rendererToHide.enabled = false;
        }
    }

    private bool ShouldKeepRendererVisible(Renderer rendererToHide)
    {
        if (rendererToHide == null)
        {
            return false;
        }

        if (rendererToHide is ParticleSystemRenderer)
        {
            return true;
        }

        return IsDestroyEffectChild(rendererToHide.transform);
    }

    private void PlayDestroyEffects()
    {
        destroyEffects?.PlayAtAssignedTransforms();
    }

    private bool IsDestroyEffectChild(Transform candidate)
    {
        return destroyEffects != null && destroyEffects.IsEffectChild(candidate);
    }

    private void FadeFireParticles()
    {
        ResolveParticleFadeEffect();
        particleFadeEffect.Play(
            particleFadeOutDuration,
            fireParticle1,
            fireParticle2,
            fireParticle3,
            fireParticle4);
    }

    private void ResolveParticleFadeEffect()
    {
        if (particleFadeEffect == null)
        {
            particleFadeEffect = GetComponent<ParticleFadeOutEffect>();
        }

        if (particleFadeEffect == null)
        {
            particleFadeEffect = gameObject.AddComponent<ParticleFadeOutEffect>();
        }
    }

    private void PlayChildAudio()
    {
        AudioSource[] audioSources = GetComponentsInChildren<AudioSource>();
        for (int i = 0; i < audioSources.Length; i++)
        {
            if (audioSources[i] != null)
            {
                audioSources[i].Play();
            }
        }
    }

    private void ResolvePlayer()
    {
        if (player != null)
        {
            return;
        }

        try
        {
            GameObject playerObject =
                GameObject.FindGameObjectWithTag(PlayerTag);
            player = playerObject != null
                ? playerObject.transform
                : null;
        }
        catch (UnityException)
        {
            if (warnedMissingPlayerTag)
            {
                return;
            }

            Debug.LogWarning(
                $"[FireObstacle] No Unity tag named '{PlayerTag}' exists.",
                this);
            warnedMissingPlayerTag = true;
        }
    }

    private void WarnMissingBurnStatus(PlayerController player)
    {
        if (warnedMissingBurnStatus)
        {
            return;
        }

        Debug.LogWarning(
            "[FireObstacle] Assign PlayerBurnStatus on the player's PlayerStatusManager before using fire obstacles.",
            player);
        warnedMissingBurnStatus = true;
    }
}
