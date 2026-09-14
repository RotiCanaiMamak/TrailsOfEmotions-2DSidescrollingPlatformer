using Bundos.WaterSystem;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Water))]
public class TerrainPuddleObstacle : MonoBehaviour, IGroundPoundTarget, IBackpackAbsorbable, IRegulationClearable
{
    [Header("Basin Size")]
    [Min(0.1f)] [SerializeField] private float minWidth = 3f;
    [Min(0.1f)] [SerializeField] private float maxWidth = 4f;
    [Min(0.1f)] [SerializeField] private float minDepth = 0.75f;
    [Min(0.1f)] [SerializeField] private float maxDepth = 1.25f;
    [Min(0f)] [SerializeField] private float edgeMargin = 0.75f;

    [Header("Mud Gameplay")]
    [Min(0f)] [SerializeField] private float speedMultiplier = 0.55f;
    [Min(0f)] [SerializeField] private float jumpForceMultiplier = 0.65f;
    [Min(0f)] [SerializeField] private float normalSplashCooldown = 0.35f;
    [Min(0f)] [SerializeField] private float characterContactTolerance = 0.05f;
    [Min(1)] [SerializeField] private int groundPoundSplashCount = 6;
    [Min(0f)] [SerializeField] private float groundPoundSplashSpread = 0.75f;

    [Header("Emotion")]
    [Min(0f)] [SerializeField] private float emotionIncreaseOnHit = 1f;

    [Header("Audio")]
    [SerializeField] private AudioClip obstacleCollisionClip;
    [Range(0f, 1f)] [SerializeField] private float obstacleCollisionVolume = 1f;
    [SerializeField] private AudioClip groundPoundClip;
    [Range(0f, 1f)] [SerializeField] private float groundPoundVolume = 1f;

    private Water water;
    private Collider2D waterCollider;
    private BuoyancyEffector2D buoyancyEffector;
    private MeshRenderer waterRenderer;
    private Camera reflectionCamera;
    private float nextNormalSplashTime;
    private bool isDepleted;
    private bool hasPlayedCollisionSound;
    private bool warnedMissingMudStatus;
    private ContactFilter2D normalContactFilter;
    private readonly Collider2D[] normalContactHits = new Collider2D[8];

    public float EdgeMargin => Mathf.Max(0f, edgeMargin);

    private void Awake()
    {
        water = GetComponent<Water>();
        waterCollider = GetComponent<Collider2D>();
        buoyancyEffector = GetComponent<BuoyancyEffector2D>();
        waterRenderer = GetComponent<MeshRenderer>();
        reflectionCamera = GetComponentInChildren<Camera>(true);

        // The marker owns puddle splashes so player helper colliders cannot make
        // Water spawn additional, unsorted particles.
        if (water != null)
        {
            water.interactive = false;
        }

        normalContactFilter.useTriggers = true;
    }

    private void OnValidate()
    {
        minWidth = Mathf.Max(0.1f, minWidth);
        maxWidth = Mathf.Max(minWidth, maxWidth);
        minDepth = Mathf.Max(0.1f, minDepth);
        maxDepth = Mathf.Max(minDepth, maxDepth);
        edgeMargin = Mathf.Max(0f, edgeMargin);
        speedMultiplier = Mathf.Max(0f, speedMultiplier);
        jumpForceMultiplier = Mathf.Max(0f, jumpForceMultiplier);
        normalSplashCooldown = Mathf.Max(0f, normalSplashCooldown);
        characterContactTolerance = Mathf.Max(0f, characterContactTolerance);
        groundPoundSplashCount = Mathf.Max(1, groundPoundSplashCount);
        groundPoundSplashSpread = Mathf.Max(0f, groundPoundSplashSpread);
        emotionIncreaseOnHit = Mathf.Max(0f, emotionIncreaseOnHit);
        obstacleCollisionVolume = Mathf.Clamp01(obstacleCollisionVolume);
        groundPoundVolume = Mathf.Clamp01(groundPoundVolume);
    }

    public Vector2 RollSize()
    {
        float width = Random.Range(Mathf.Min(minWidth, maxWidth), Mathf.Max(minWidth, maxWidth));
        float depth = Random.Range(Mathf.Min(minDepth, maxDepth), Mathf.Max(minDepth, maxDepth));
        return new Vector2(width, depth);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryHandleNormalContact(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryHandleNormalContact(other);
    }

    private void FixedUpdate()
    {
        if (isDepleted || waterCollider == null || !waterCollider.enabled ||
            Time.time < nextNormalSplashTime)
        {
            return;
        }

        int hitCount = waterCollider.Overlap(normalContactFilter, normalContactHits);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = normalContactHits[i];
            if (hit == null)
            {
                continue;
            }

            PlayerController player = hit.GetComponentInParent<PlayerController>();
            if (player == null || !player.IsCharacterCollider(hit))
            {
                continue;
            }

            TryHandleNormalContact(hit);
            return;
        }
    }

    public bool TryHandleGroundPound(PlayerController player, Vector2 impactPoint)
    {
        if (isDepleted || player == null)
        {
            return false;
        }

        DepleteWithSplash(GetWaterlinePosition(impactPoint));
        return true;
    }

    public bool TryAbsorbByBackpack(LinaBackpackBulwark bulwark)
    {
        if (isDepleted)
        {
            return false;
        }

        Vector3 absorbPosition = bulwark != null ? bulwark.transform.position : GetWaterlineCenterPosition();
        DepleteWithSplash(GetWaterlinePosition(absorbPosition));
        return true;
    }

    public bool TryClearByRegulation(Object source)
    {
        return TryAbsorbByBackpack(null);
    }

    private void TryHandleNormalContact(Collider2D other)
    {
        if (isDepleted || other == null || Time.time < nextNormalSplashTime)
        {
            return;
        }

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || player.IsGroundPounding)
        {
            return;
        }

        bool characterContact = player.IsCharacterCollider(other) ||
            player.IsCharacterTouching(waterCollider, characterContactTolerance);
        if (!characterContact)
        {
            return;
        }

        nextNormalSplashTime = Time.time + normalSplashCooldown;
        SpawnSplash(GetWaterlineCenterPosition());
        if (ApplyMudModifier(player))
        {
            AddHitEmotion();
            PlayCollisionSoundOnce();
        }
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

    private bool ApplyMudModifier(PlayerController player)
    {
        PlayerMudStatus mudStatus =
            player.Statuses != null ? player.Statuses.MudStatus : null;
        if (mudStatus != null)
        {
            mudStatus.RefreshMudEffect(
                speedMultiplier,
                jumpForceMultiplier);
            return true;
        }

        WarnMissingMudStatus(player);
        return false;
    }

    private void AddHitEmotion()
    {
        if (EmotionMeter.Instance != null && emotionIncreaseOnHit > 0f)
        {
            EmotionMeter.Instance.AddObstacleEmotion(emotionIncreaseOnHit);
        }
    }

    private void WarnMissingMudStatus(PlayerController player)
    {
        if (warnedMissingMudStatus)
        {
            return;
        }

        Debug.LogWarning(
            "[TerrainPuddleObstacle] Assign PlayerMudStatus on the player's PlayerStatusManager to show the mud slow effect.",
            player);
        warnedMissingMudStatus = true;
    }

    private Vector3 GetWaterlinePosition(Vector2 worldPosition)
    {
        if (water == null)
        {
            return new Vector3(worldPosition.x, transform.position.y, transform.position.z);
        }

        Vector3 localPosition = transform.InverseTransformPoint(worldPosition);
        localPosition.x = Mathf.Clamp(localPosition.x, 0f, Mathf.Max(0f, water.numSprings - 1f));
        localPosition.y = 1f;
        localPosition.z = 0f;
        return transform.TransformPoint(localPosition);
    }

    private Vector3 GetWaterlineCenterPosition()
    {
        if (water == null)
        {
            return transform.position;
        }

        float centerSpringIndex = Mathf.Max(0f, water.numSprings - 1f) * 0.5f;
        return transform.TransformPoint(new Vector3(centerSpringIndex, 1f, 0f));
    }

    private void SpawnSplash(Vector3 position)
    {
        if (water == null || water.splashParticle == null)
        {
            return;
        }

        GameObject splash = Instantiate(water.splashParticle, position, Quaternion.identity);
        Renderer[] renderers = splash.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer splashRenderer in renderers)
        {
            splashRenderer.sortingLayerName = "Effects";
        }
    }

    private void DepleteWithSplash(Vector3 burstCenter)
    {
        for (int i = 0; i < groundPoundSplashCount; i++)
        {
            Vector3 splashPosition = burstCenter + Vector3.right * Random.Range(
                -groundPoundSplashSpread,
                groundPoundSplashSpread);
            SpawnSplash(splashPosition);
        }

        DepleteWater();
        AudioManager.Instance?.PlaySfxOneShot(groundPoundClip, groundPoundVolume);
    }

    private void DepleteWater()
    {
        isDepleted = true;

        if (waterCollider != null)
        {
            waterCollider.enabled = false;
        }

        if (water != null)
        {
            water.enabled = false;
        }

        if (buoyancyEffector != null)
        {
            buoyancyEffector.enabled = false;
        }

        if (waterRenderer != null)
        {
            waterRenderer.enabled = false;
        }

        if (reflectionCamera != null)
        {
            reflectionCamera.enabled = false;
        }
    }
}
