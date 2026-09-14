using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class SinkingShadowObstacle : MonoBehaviour
{
    private const string PlayerTag = "Player";

    [Header("Collision")]
    [FormerlySerializedAs("shadowTrigger")]
    [SerializeField] private Collider2D groundSurfaceCheckCollider;
    [SerializeField] private Collider2D playerCheckCollider;

    [SerializeField] private Rigidbody2D shadowBody;

    [Header("Particles")]
    [SerializeField] private ParticleSystem[] shadowParticles;

    [Header("Motion")]
    [Min(0f)] [SerializeField] private float xSpeed = 8f;
    [Min(0f)] [SerializeField] private float ySpeed = 8f;
    [Min(0f)] [SerializeField] private float maxLifetime = 12f;

    [Header("Escape")]
    [Min(0f)] [SerializeField] private float escapeHoldDuration = 2f;

    [Header("Emotion")]
    [Min(0f)] [SerializeField] private float emotionIncreaseOnHit = 1f;

    [Header("Fade")]
    [Min(0f)] [SerializeField] private float particleFadeOutDuration = 0.6f;

    [Header("Audio")]
    [SerializeField] private AudioClip obstacleCollisionClip;
    [Range(0f, 1f)] [SerializeField] private float obstacleCollisionVolume = 1f;

    private Transform player;
    private ParticleFadeOutEffect particleFadeEffect;
    private PlayerSinkingShadowStatus trappedStatus;
    private float lifetimeElapsed;
    private bool initialized;
    private bool fading;
    private bool warnedMissingPlayerTag;
    private bool warnedMissingSinkingShadowStatus;

    private void Awake()
    {
        ResolveColliders();
        ResolveBody();
        ResolveParticles();
        ConfigureBody();
        ConfigureColliders();
    }

    private void OnValidate()
    {
        xSpeed = Mathf.Max(0f, xSpeed);
        ySpeed = Mathf.Max(0f, ySpeed);
        maxLifetime = Mathf.Max(0f, maxLifetime);
        escapeHoldDuration = Mathf.Max(0f, escapeHoldDuration);
        emotionIncreaseOnHit = Mathf.Max(0f, emotionIncreaseOnHit);
        particleFadeOutDuration = Mathf.Max(0f, particleFadeOutDuration);
        obstacleCollisionVolume = Mathf.Clamp01(obstacleCollisionVolume);
        ConfigureBody();
        ConfigureColliders();
    }

    private void Update()
    {
        if (fading)
        {
            return;
        }

        if (!initialized)
        {
            InitializeFromCurrentPlacement();
        }

        if (trappedStatus != null)
        {
            return;
        }

        UpdateTracking();
        UpdateLifetime();
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
        if (TryHandleSurfaceContact(other))
        {
            return;
        }

        TryTrapPlayer(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (TryHandleSurfaceContact(other))
        {
            return;
        }

        TryTrapPlayer(other);
    }

    public void InitializeOnChunk(
        TerrainChunk chunk,
        float initialSurfaceT,
        Vector3 bottomPosition,
        Vector3 surfacePosition)
    {
        transform.position = bottomPosition;
        lifetimeElapsed = 0f;
        initialized = true;
    }

    public void NotifyTrapReleased(PlayerSinkingShadowStatus status)
    {
        if (trappedStatus != status)
        {
            return;
        }

        trappedStatus = null;
        Destroy(gameObject);
    }

    private void InitializeFromCurrentPlacement()
    {
        lifetimeElapsed = 0f;
        initialized = true;
    }

    private void UpdateTracking()
    {
        ResolvePlayer();
        if (player == null)
        {
            return;
        }

        Vector3 currentPosition = transform.position;
        Vector3 targetPosition = player.position;

        transform.position = new Vector3(
            Mathf.MoveTowards(currentPosition.x, targetPosition.x, xSpeed * Time.deltaTime),
            Mathf.MoveTowards(currentPosition.y, targetPosition.y, ySpeed * Time.deltaTime),
            currentPosition.z);
    }

    private void UpdateLifetime()
    {
        if (maxLifetime <= 0f)
        {
            return;
        }

        lifetimeElapsed += Time.deltaTime;
        if (lifetimeElapsed >= maxLifetime)
        {
            FadeAndDestroy();
        }
    }

    private void TryTrapPlayer(Collider2D other)
    {
        if (fading || trappedStatus != null || other == null)
        {
            return;
        }

        PlayerController playerController = other.GetComponentInParent<PlayerController>();
        if (playerController == null || !playerController.IsCharacterCollider(other))
        {
            return;
        }

        if (!IsPlayerCheckTouching(other))
        {
            return;
        }

        PlayerSinkingShadowStatus status =
            playerController.Statuses != null
                ? playerController.Statuses.SinkingShadowStatus
                : null;
        if (status == null)
        {
            WarnMissingSinkingShadowStatus(playerController);
            return;
        }

        if (!status.TryTrap(this, escapeHoldDuration))
        {
            return;
        }

        trappedStatus = status;
        AddHitEmotion();
        AudioManager.Instance?.PlaySfxOneShot(obstacleCollisionClip, obstacleCollisionVolume);
        SetCollidersEnabled(false);
        FadeVisuals();
    }

    private void AddHitEmotion()
    {
        if (EmotionMeter.Instance != null && emotionIncreaseOnHit > 0f)
        {
            EmotionMeter.Instance.AddObstacleEmotion(emotionIncreaseOnHit);
        }
    }

    private bool TryHandleSurfaceContact(Collider2D other)
    {
        if (fading || trappedStatus != null || other == null)
        {
            return false;
        }

        if (other.GetComponent("TerrainSurfaceShadowCollider") == null)
        {
            return false;
        }

        if (!IsGroundSurfaceCheckTouching(other))
        {
            return false;
        }

        FadeAndDestroy();
        return true;
    }

    private void FadeAndDestroy()
    {
        if (fading)
        {
            return;
        }

        fading = true;
        SetCollidersEnabled(false);
        ReleaseTrappedPlayer();
        FadeVisuals();

        Destroy(gameObject, particleFadeOutDuration + 0.05f);
    }

    private void FadeVisuals()
    {
        ResolveParticleFadeEffect();
        if (particleFadeEffect != null && shadowParticles != null && shadowParticles.Length > 0)
        {
            particleFadeEffect.Play(particleFadeOutDuration, shadowParticles);
        }
    }

    private void ReleaseTrappedPlayer()
    {
        PlayerSinkingShadowStatus status = trappedStatus;
        trappedStatus = null;
        status?.ReleaseFrom(this);
    }

    private void ResolveColliders()
    {
        if (groundSurfaceCheckCollider == null)
        {
            groundSurfaceCheckCollider = GetComponent<Collider2D>();
        }

        if (playerCheckCollider == null)
        {
            playerCheckCollider = groundSurfaceCheckCollider;
        }
    }

    private void ResolveBody()
    {
        if (shadowBody == null)
        {
            shadowBody = GetComponent<Rigidbody2D>();
        }

        if (shadowBody == null)
        {
            shadowBody = gameObject.AddComponent<Rigidbody2D>();
        }
    }

    private void ResolveParticles()
    {
        if (shadowParticles == null || shadowParticles.Length == 0)
        {
            shadowParticles = GetComponentsInChildren<ParticleSystem>(true);
        }
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

    private void ResolvePlayer()
    {
        if (player != null)
        {
            return;
        }

        try
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag(PlayerTag);
            player = playerObject != null ? playerObject.transform : null;
        }
        catch (UnityException)
        {
            if (warnedMissingPlayerTag)
            {
                return;
            }

            Debug.LogWarning(
                $"[SinkingShadowObstacle] No Unity tag named '{PlayerTag}' exists.",
                this);
            warnedMissingPlayerTag = true;
        }
    }

    private void WarnMissingSinkingShadowStatus(PlayerController playerController)
    {
        if (warnedMissingSinkingShadowStatus)
        {
            return;
        }

        Debug.LogWarning(
            "[SinkingShadowObstacle] Assign PlayerSinkingShadowStatus on the player's PlayerStatusManager before using sinking shadows.",
            playerController);
        warnedMissingSinkingShadowStatus = true;
    }

    private void ConfigureColliders()
    {
        ConfigureTrigger(groundSurfaceCheckCollider);
        ConfigureTrigger(playerCheckCollider);
    }

    private static void ConfigureTrigger(Collider2D target)
    {
        if (target != null)
        {
            target.isTrigger = true;
        }
    }

    private void ConfigureBody()
    {
        if (shadowBody == null)
        {
            return;
        }

        shadowBody.bodyType = RigidbodyType2D.Kinematic;
        shadowBody.gravityScale = 0f;
        shadowBody.freezeRotation = true;
    }

    private bool IsGroundSurfaceCheckTouching(Collider2D other)
    {
        return IsColliderTouching(groundSurfaceCheckCollider, other);
    }

    private bool IsPlayerCheckTouching(Collider2D other)
    {
        Collider2D target = playerCheckCollider != null
            ? playerCheckCollider
            : groundSurfaceCheckCollider;
        return IsColliderTouching(target, other);
    }

    private static bool IsColliderTouching(Collider2D source, Collider2D other)
    {
        return source != null &&
            source.enabled &&
            other != null &&
            other.enabled &&
            source.IsTouching(other);
    }

    private void SetCollidersEnabled(bool enabled)
    {
        if (groundSurfaceCheckCollider != null)
        {
            groundSurfaceCheckCollider.enabled = enabled;
        }

        if (playerCheckCollider != null && playerCheckCollider != groundSurfaceCheckCollider)
        {
            playerCheckCollider.enabled = enabled;
        }
    }
}
