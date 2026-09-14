using System.Collections.Generic;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class IceHailProjectile : MonoBehaviour
{
    [Header("Collision")]
    [SerializeField] private Collider2D hailCollider;
    [SerializeField] private Rigidbody2D hailBody;
    [Min(0f)] [SerializeField] private float impactPulseDuration = 0.05f;

    [Header("Visuals")]
    [SerializeField] private ObstacleSpriteVariantRandomizer spriteVariantRandomizer;
    [SerializeField] private SpriteRenderer[] projectileRenderers;

    [Header("Destroy Effects")]
    [SerializeField] private ObstacleDestroyEffects destroyEffects;

    [Header("Audio")]
    [SerializeField] private AudioClip obstacleCollisionClip;
    [Range(0f, 1f)] [SerializeField] private float obstacleCollisionVolume = 1f;

    [Header("Recoil")]
    [Min(0f)] [SerializeField] private float recoilHorizontalSpeed = 6f;
    [SerializeField] private float recoilMaxVerticalVelocity = 2f;
    [Min(0f)] [SerializeField] private float recoilDuration = 0.12f;

    private readonly HashSet<PlayerController> affectedPlayers =
        new HashSet<PlayerController>();
    private readonly Collider2D[] impactHits = new Collider2D[32];

    private Vector3 targetPosition;
    private float fallSpeed;
    private int activeChargeDrain;
    private float movementSpeedMultiplier;
    private float slowdownDuration;
    private float impactCheckRadius;
    private float landingCleanupDelay;
    private float emotionIncreaseOnHit;
    private bool initialized;
    private bool landed;
    private bool impactPulseActive;
    private bool hasPlayedCollisionSound;
    private bool warnedMissingIceHailStatus;
    private Vector3 impactPoint;
    private Coroutine impactPulseRoutine;

    private void Reset()
    {
        ResolveCollider();
        ResolveBody();
        ResolveSpriteVariantRandomizer();
        ResolveProjectileRenderers();
        ResolveDestroyEffects();
    }

    private void Awake()
    {
        ResolveCollider();
        ResolveBody();
        ResolveSpriteVariantRandomizer();
        ResolveProjectileRenderers();
        ResolveDestroyEffects();
        ConfigureCollider();
        ConfigureBody();
    }

    private void OnValidate()
    {
        fallSpeed = Mathf.Max(0f, fallSpeed);
        activeChargeDrain = Mathf.Max(0, activeChargeDrain);
        movementSpeedMultiplier = Mathf.Clamp01(movementSpeedMultiplier);
        slowdownDuration = Mathf.Max(0f, slowdownDuration);
        impactCheckRadius = Mathf.Max(0f, impactCheckRadius);
        landingCleanupDelay = Mathf.Max(0f, landingCleanupDelay);
        emotionIncreaseOnHit = Mathf.Max(0f, emotionIncreaseOnHit);
        impactPulseDuration = Mathf.Max(0f, impactPulseDuration);
        recoilHorizontalSpeed = Mathf.Max(0f, recoilHorizontalSpeed);
        recoilDuration = Mathf.Max(0f, recoilDuration);
        obstacleCollisionVolume = Mathf.Clamp01(obstacleCollisionVolume);
        ResolveSpriteVariantRandomizer();
        ResolveProjectileRenderers();
        ResolveDestroyEffects();
        ConfigureCollider();
        ConfigureBody();
    }

    public void Initialize(
        Vector3 targetPosition,
        float fallSpeed,
        int activeChargeDrain,
        float movementSpeedMultiplier,
        float slowdownDuration,
        float impactCheckRadius,
        float landingCleanupDelay,
        float emotionIncreaseOnHit)
    {
        this.targetPosition = targetPosition;
        this.fallSpeed = Mathf.Max(0f, fallSpeed);
        this.activeChargeDrain = Mathf.Max(0, activeChargeDrain);
        this.movementSpeedMultiplier = Mathf.Clamp01(movementSpeedMultiplier);
        this.slowdownDuration = Mathf.Max(0f, slowdownDuration);
        this.impactCheckRadius = Mathf.Max(0f, impactCheckRadius);
        this.landingCleanupDelay = Mathf.Max(0f, landingCleanupDelay);
        this.emotionIncreaseOnHit = Mathf.Max(0f, emotionIncreaseOnHit);
        initialized = true;
        landed = false;
        impactPulseActive = false;
        hasPlayedCollisionSound = false;
        affectedPlayers.Clear();

        ResolveCollider();
        ResolveBody();
        ConfigureBody();
        ShowProjectileRenderers();
        SetColliderEnabled(true);
    }

    private void Update()
    {
        if (!initialized || landed)
        {
            return;
        }

        if (fallSpeed <= 0f)
        {
            Land(targetPosition);
            return;
        }

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            fallSpeed * Time.deltaTime);

        if ((transform.position - targetPosition).sqrMagnitude <= 0.0001f)
        {
            Land(targetPosition);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleTrigger(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        HandleTrigger(other);
    }

    private void Land(Vector3 impactPosition)
    {
        if (landed)
        {
            return;
        }

        landed = true;
        impactPoint = impactPosition;
        transform.position = impactPoint;
        Physics2D.SyncTransforms();
        HideProjectileRenderers();
        PlayDestroyEffects();
        StartImpactPulse();

        if (landingCleanupDelay <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        Destroy(gameObject, landingCleanupDelay);
    }

    private void HandleTrigger(Collider2D other)
    {
        if (other == null)
        {
            return;
        }

        if (!landed)
        {
            if (IsGroundSurface(other))
            {
                Land(GetImpactPoint(other));
            }

            return;
        }

        if (impactPulseActive)
        {
            HandlePlayerTouch(other);
        }
    }

    private void StartImpactPulse()
    {
        SetColliderEnabled(true);
        impactPulseActive = true;
        ApplyImpactOverlap();

        if (impactPulseRoutine != null)
        {
            StopCoroutine(impactPulseRoutine);
        }

        impactPulseRoutine = StartCoroutine(ImpactPulseRoutine());
    }

    private IEnumerator ImpactPulseRoutine()
    {
        if (impactPulseDuration > 0f)
        {
            yield return new WaitForSeconds(impactPulseDuration);
        }

        impactPulseActive = false;
        SetColliderEnabled(false);
        impactPulseRoutine = null;
    }

    private void ApplyImpactOverlap()
    {
        if (hailCollider == null)
        {
            return;
        }

        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = true;

        int hitCount = hailCollider.Overlap(filter, impactHits);
        for (int i = 0; i < hitCount; i++)
        {
            HandlePlayerTouch(impactHits[i]);
        }
    }

    private void HandlePlayerTouch(Collider2D other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || !player.IsCharacterCollider(other))
        {
            return;
        }

        ApplyEffect(player);
    }

    private void ApplyEffect(PlayerController player)
    {
        if (player == null || affectedPlayers.Contains(player))
        {
            return;
        }

        affectedPlayers.Add(player);
        AddHitEmotion();
        PlayCollisionSoundOnce();
        ApplyRecoil(player);

        CharacterAbility ability =
            player.Statuses != null ? player.Statuses.Ability : null;
        if (ability != null)
        {
            ability.TryDrainActiveCharges(activeChargeDrain);
        }

        PlayerIceHailStatus status =
            player.Statuses != null ? player.Statuses.IceHailStatus : null;
        if (status == null)
        {
            WarnMissingIceHailStatus(player);
            return;
        }

        status.RefreshSlow(movementSpeedMultiplier, slowdownDuration);
    }

    private void ApplyRecoil(PlayerController player)
    {
        if (player == null || recoilHorizontalSpeed <= 0f || recoilDuration <= 0f)
        {
            return;
        }

        Vector2 direction = (Vector2)(player.transform.position - impactPoint);
        if (Mathf.Abs(direction.x) <= 0.01f)
        {
            direction.x = -1f;
        }

        player.ApplyObstacleRecoil(
            direction,
            recoilHorizontalSpeed,
            recoilMaxVerticalVelocity,
            recoilDuration);
    }

    private void PlayCollisionSoundOnce()
    {
        if (hasPlayedCollisionSound)
        {
            return;
        }

        hasPlayedCollisionSound = true;
        AudioManager.Instance?.PlaySfxOneShot(obstacleCollisionClip, obstacleCollisionVolume);
    }

    private void AddHitEmotion()
    {
        if (EmotionMeter.Instance != null && emotionIncreaseOnHit > 0f)
        {
            EmotionMeter.Instance.AddObstacleEmotion(emotionIncreaseOnHit);
        }
    }

    private void PlayDestroyEffects()
    {
        destroyEffects?.Play(impactPoint);
    }

    private Vector3 GetImpactPoint(Collider2D other)
    {
        if (hailCollider == null || other == null)
        {
            return transform.position;
        }

        Vector2 closestPoint = other.ClosestPoint(transform.position);
        return closestPoint;
    }

    private static bool IsGroundSurface(Collider2D other)
    {
        return other != null &&
            other.GetComponent<TerrainSurfaceShadowCollider>() != null;
    }

    private void WarnMissingIceHailStatus(PlayerController player)
    {
        if (warnedMissingIceHailStatus)
        {
            return;
        }

        Debug.LogWarning(
            "[IceHailProjectile] Assign PlayerIceHailStatus on the player's PlayerStatusManager before using ice hail.",
            player);
        warnedMissingIceHailStatus = true;
    }

    private void ResolveCollider()
    {
        if (hailCollider == null)
        {
            hailCollider = GetComponent<Collider2D>();
        }
    }

    private void ResolveBody()
    {
        if (hailBody == null)
        {
            hailBody = GetComponent<Rigidbody2D>();
        }

        if (hailBody == null)
        {
            hailBody = gameObject.AddComponent<Rigidbody2D>();
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

    private void ResolveProjectileRenderers()
    {
        if (projectileRenderers == null || projectileRenderers.Length == 0)
        {
            projectileRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        }
    }

    private void ResolveDestroyEffects()
    {
        if (destroyEffects == null)
        {
            destroyEffects = GetComponentInChildren<ObstacleDestroyEffects>(true);
        }
    }

    private void ConfigureCollider()
    {
        if (hailCollider != null)
        {
            hailCollider.isTrigger = true;
        }
    }

    private void ConfigureBody()
    {
        if (hailBody == null)
        {
            return;
        }

        hailBody.bodyType = RigidbodyType2D.Kinematic;
        hailBody.gravityScale = 0f;
        hailBody.freezeRotation = true;
    }

    private void SetColliderEnabled(bool enabled)
    {
        if (hailCollider != null)
        {
            hailCollider.enabled = enabled;
        }
    }

    private void HideProjectileRenderers()
    {
        SetProjectileRenderersEnabled(false);
    }

    private void ShowProjectileRenderers()
    {
        SetProjectileRenderersEnabled(true);
    }

    private void SetProjectileRenderersEnabled(bool enabled)
    {
        if (projectileRenderers == null)
        {
            return;
        }

        for (int i = 0; i < projectileRenderers.Length; i++)
        {
            SpriteRenderer projectileRenderer = projectileRenderers[i];
            if (projectileRenderer != null)
            {
                projectileRenderer.enabled = enabled;
            }
        }
    }
}
