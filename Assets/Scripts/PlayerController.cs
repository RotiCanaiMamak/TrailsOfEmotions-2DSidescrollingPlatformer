using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class PlayerController : MonoBehaviour
{
    private struct RegulationMovementModifier
    {
        public float horizontalSpeedMultiplier;
        public float targetHorizontalSpeedMultiplier;
        public float horizontalSpeedLerpStartMultiplier;
        public float horizontalSpeedLerpElapsed;
        public float glideGravityMultiplier;
        public float airborneGravityMultiplier;
        public float horizontalSpeedLerpDuration;
        public float jumpForwardImpulseMultiplier;
        public float jumpForceMultiplier;

        public RegulationMovementModifier(
            float horizontalSpeedMultiplier,
            float glideGravityMultiplier,
            float airborneGravityMultiplier,
            float horizontalSpeedLerpDuration,
            float jumpForwardImpulseMultiplier,
            float jumpForceMultiplier,
            bool startAtTarget = false)
        {
            this.targetHorizontalSpeedMultiplier = Mathf.Max(0f, horizontalSpeedMultiplier);
            this.horizontalSpeedMultiplier = startAtTarget || horizontalSpeedLerpDuration <= 0f
                ? this.targetHorizontalSpeedMultiplier
                : 1f;
            this.horizontalSpeedLerpStartMultiplier = this.horizontalSpeedMultiplier;
            this.horizontalSpeedLerpElapsed = 0f;
            this.glideGravityMultiplier = Mathf.Max(0f, glideGravityMultiplier);
            this.airborneGravityMultiplier = Mathf.Max(0f, airborneGravityMultiplier);
            this.horizontalSpeedLerpDuration = Mathf.Max(0f, horizontalSpeedLerpDuration);
            this.jumpForwardImpulseMultiplier = Mathf.Max(0f, jumpForwardImpulseMultiplier);
            this.jumpForceMultiplier = Mathf.Max(0f, jumpForceMultiplier);
        }

        public void SetTarget(
            float horizontalSpeedMultiplier,
            float glideGravityMultiplier,
            float airborneGravityMultiplier,
            float horizontalSpeedLerpDuration,
            float jumpForwardImpulseMultiplier,
            float jumpForceMultiplier)
        {
            float newHorizontalSpeedMultiplier = Mathf.Max(0f, horizontalSpeedMultiplier);
            float newHorizontalSpeedLerpDuration = Mathf.Max(0f, horizontalSpeedLerpDuration);

            if (!Mathf.Approximately(targetHorizontalSpeedMultiplier, newHorizontalSpeedMultiplier))
            {
                horizontalSpeedLerpStartMultiplier = this.horizontalSpeedMultiplier;
                horizontalSpeedLerpElapsed = 0f;
            }

            targetHorizontalSpeedMultiplier = newHorizontalSpeedMultiplier;
            this.horizontalSpeedLerpDuration = newHorizontalSpeedLerpDuration;
            this.glideGravityMultiplier = Mathf.Max(0f, glideGravityMultiplier);
            this.airborneGravityMultiplier = Mathf.Max(0f, airborneGravityMultiplier);
            this.jumpForwardImpulseMultiplier = Mathf.Max(0f, jumpForwardImpulseMultiplier);
            this.jumpForceMultiplier = Mathf.Max(0f, jumpForceMultiplier);

            if (this.horizontalSpeedLerpDuration <= 0f)
            {
                this.horizontalSpeedMultiplier = targetHorizontalSpeedMultiplier;
                horizontalSpeedLerpStartMultiplier = this.horizontalSpeedMultiplier;
                horizontalSpeedLerpElapsed = 0f;
            }
        }

        public void TickHorizontalSpeedLerp(float deltaTime)
        {
            if (horizontalSpeedLerpDuration <= 0f)
            {
                horizontalSpeedMultiplier = targetHorizontalSpeedMultiplier;
                return;
            }

            if (Mathf.Approximately(horizontalSpeedMultiplier, targetHorizontalSpeedMultiplier))
            {
                horizontalSpeedMultiplier = targetHorizontalSpeedMultiplier;
                return;
            }

            horizontalSpeedLerpElapsed = Mathf.Min(
                horizontalSpeedLerpDuration,
                horizontalSpeedLerpElapsed + Mathf.Max(0f, deltaTime));

            float t = Mathf.Clamp01(horizontalSpeedLerpElapsed / horizontalSpeedLerpDuration);
            horizontalSpeedMultiplier = Mathf.Lerp(
                horizontalSpeedLerpStartMultiplier,
                targetHorizontalSpeedMultiplier,
                t);
        }
    }

    private struct DelayedInputSnapshot
    {
        public readonly float time;
        public readonly bool spaceHeld;
        public readonly bool groundPoundHeld;
        public readonly bool leftShiftHeld;
        public readonly bool rightShiftHeld;

        public DelayedInputSnapshot(float time)
        {
            this.time = time;
            spaceHeld = Input.GetKey(KeyCode.Space);
            groundPoundHeld = Input.GetKey(KeyCode.G);
            leftShiftHeld = Input.GetKey(KeyCode.LeftShift);
            rightShiftHeld = Input.GetKey(KeyCode.RightShift);
        }

        public bool GetKey(KeyCode keyCode)
        {
            return keyCode switch
            {
                KeyCode.Space => spaceHeld,
                KeyCode.G => groundPoundHeld,
                KeyCode.LeftShift => leftShiftHeld,
                KeyCode.RightShift => rightShiftHeld,
                _ => Input.GetKey(keyCode),
            };
        }
    }

    private const float InputSnapshotHistorySeconds = 2f;
    private const float InputSnapshotRetentionPadding = 0.25f;

    [Header("Movement")]
    // Runtime copy of CharacterData.moveSpeed. This is intentionally not serialized so
    // Character Data remains the only Inspector-editable source for base movement speed.
    private float moveSpeed;
    public float gravityScale = 5f;
    [Tooltip("Lowest forward speed while grounded.")]
    public float minGroundSpeed = 9f;
    [Tooltip("Hard maximum forward speed while grounded.")]
    public float maxGroundSpeed = 44f;

    [Header("Slope Momentum")]
    [Tooltip("Acceleration strength applied along slopes. Downhill adds speed and uphill removes it.")]
    public float slopeAccelerationStrength = 48f;
    [Tooltip("How quickly carried speed returns to cruising speed on flat ground.")]
    public float flatGroundSpeedReturnRate = 8f;

    [Header("Launch")]
    public float jumpForce = 24f;
    [Tooltip("Forward launch impulse added from current ground speed.")]
    public float jumpForwardImpulseMultiplier = 0.18f;
    [Tooltip("Minimum forward jump speed as a fraction of cruising speed when the player is stopped.")]
    [Range(0f, 1f)]
    public float stoppedJumpForwardSpeedMultiplier = 0.25f;
    public float uphillRampLaunchMultiplier = 0.9f;
    public float uphillRampLaunchStartAngle = 5f;
    public float uphillRampLaunchMaxAngle = 30f;
    [Tooltip("Maximum additional upward speed granted by an uphill jump.")]
    public float maxUphillRampLaunchBoost = 30f;

    [Header("Slope")]
    [Tooltip("How far left/right from the player to sample the terrain slope.")]
    [FormerlySerializedAs("downhillSampleDistance")]
    public float slopeSampleDistance = 0.5f;

    [Header("Ground Check")]
    public Vector2 groundCheckOffset = new Vector2(0f, -0.55f);
    public float groundCheckRadius = 0.2f;
    [Tooltip("Extra distance above the sampled surface that still counts as grounded for jump input.")]
    public float jumpGroundedTolerance = 0.25f;
    public LayerMask groundLayer;

    [Header("Surface Adhesion")]
    public float raycastDistance = 3f;
    public float adhesionForce = 10f;

    [Header("Glide")]
    [Tooltip("Gravity multiplier used while gliding. Lower values make the player fall more slowly.")]
    [Range(0f, 1f)]
    public float glideGravityMultiplier = 0.35f;
    [Tooltip("Seconds used to blend into and out of glide gravity so repeated glide presses do not snap gravity.")]
    [Min(0f)] [SerializeField] private float glideGravityBlendTime = 0.06f;

    [Header("Ground Pound")]
    public float groundPoundSpeed = 25f;
    [SerializeField] private Collider2D characterCollider;
    [SerializeField] private Collider2D groundPoundEffectCollider;
    [SerializeField] private Collider2D groundPoundLegCollider;
    [SerializeField] private string groundPoundObstacleTag = "Obstacle";
    [SerializeField] private string groundPoundRegulationTag = "Regulation";

    [Header("Statuses")]
    [SerializeField] private PlayerStatusManager statuses;

    private Rigidbody2D rb;
    private bool isGrounded;
    private bool isGliding;
    private bool isGroundPounding;
    private bool landedFromGroundPoundThisFrame;
    private bool isOnZiplineSurface;
    private bool movementPaused;
    private float glideGravityBlend;
    private Vector2 surfaceNormal = Vector2.up;
    private float surfaceAdhesionLockTimer;
    private float groundForwardSpeed;
    private float obstacleRecoilTimer;
    private Vector2 lastGroundPoundImpactPoint;
    private readonly Collider2D[] groundPoundEffectHits = new Collider2D[32];

    private int groundEventSuppressionFrames;
    private const int WorldShiftGroundEventSuppressionFrames = 4;

    private bool hasWorldShiftSnapshot;
    private bool worldShiftGrounded;
    private bool worldShiftGliding;
    private bool worldShiftGroundPounding;
    private bool worldShiftOnZiplineSurface;
    private float worldShiftGlideGravityBlend;
    private Vector2 worldShiftSurfaceNormal;
    private float worldShiftSurfaceAdhesionLockTimer;
    private Vector2 worldShiftVelocity;
    private float worldShiftAngularVelocity;
    private float worldShiftGravityScale;
    private Quaternion worldShiftRotation;
    private float worldShiftGroundForwardSpeed;
    private float worldShiftObstacleRecoilTimer;
    private readonly Dictionary<Object, RegulationMovementModifier> regulationMovementModifiers = new Dictionary<Object, RegulationMovementModifier>();
    private readonly Dictionary<Object, Coroutine> timedMovementModifierRoutines = new Dictionary<Object, Coroutine>();
    private readonly Dictionary<Object, float> inputDelayModifiers = new Dictionary<Object, float>();
    private readonly HashSet<Object> movementRootSources = new HashSet<Object>();
    private readonly HashSet<Object> inputBlockSources = new HashSet<Object>();
    private readonly HashSet<Object> jumpBlockSources = new HashSet<Object>();
    private readonly List<Object> movementModifierUpdateKeys = new List<Object>();
    private readonly List<Object> inputDelayModifierUpdateKeys = new List<Object>();
    private readonly List<DelayedInputSnapshot> delayedInputSnapshots = new List<DelayedInputSnapshot>();
    private readonly Dictionary<KeyCode, int> consumedDelayedKeyDownSnapshots = new Dictionary<KeyCode, int>();
    private Object airborneHorizontalSpeedLerpSource;
    private float airborneHorizontalSpeedLerpStartSpeed;
    private float airborneHorizontalSpeedLerpElapsed;
    private int delayedInputSnapshotFrame = -1;
    private CharacterRuntime characterRuntime;

    public event System.Action<PlayerController> Landed;
    public event System.Action<PlayerController> GroundPoundLanded;

    public bool IsGliding => isGliding;
    public bool IsGrounded => isGrounded;
    public bool IsGroundPounding => isGroundPounding;
    public bool IsOnZipline => isGrounded && isOnZiplineSurface;
    public bool IsAirborne => !isGrounded && !isGroundPounding;
    public bool IsInputBlocked => PruneAndCheckInputBlocks();
    public bool IsJumpBlocked => PruneAndCheckJumpBlocks();
    public bool IsGlideInputHeld => !IsInputBlocked && GetDelayedKey(KeyCode.Space);
    public bool IsMovementRooted => PruneAndCheckMovementRoots();
    public float BaseMoveSpeed => moveSpeed;
    public float ProgressSpeed => movementPaused || IsMovementRooted
        ? 0f
        : Mathf.Max(0f, rb != null ? rb.linearVelocity.x : groundForwardSpeed);
    public Vector2 LastGroundPoundImpactPoint => lastGroundPoundImpactPoint;
    public PlayerStatusManager Statuses => statuses;

    public bool IsCharacterCollider(Collider2D other)
    {
        return other != null && other == characterCollider;
    }

    public bool IsCharacterTouching(Collider2D target, float tolerance)
    {
        if (characterCollider == null || target == null ||
            !characterCollider.enabled || !target.enabled)
        {
            return false;
        }

        ColliderDistance2D distance = characterCollider.Distance(target);
        return distance.isValid &&
            (distance.isOverlapped || distance.distance <= Mathf.Max(0f, tolerance));
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        characterRuntime = GetComponent<CharacterRuntime>();
        SetRotationLocked(true);
        rb.gravityScale = gravityScale;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        groundForwardSpeed = Mathf.Max(minGroundSpeed, moveSpeed);
    }

    private void Update()
    {
        CaptureDelayedInputSnapshot();
        landedFromGroundPoundThisFrame = false;

        if (movementPaused)
        {
            return;
        }

        CheckGrounded();
        if (IsMovementRooted)
        {
            CancelGlide();
            isGroundPounding = false;
            return;
        }

        bool jumpedThisFrame = HandleJump();
        HandleGlide(jumpedThisFrame);
        HandleGroundPound();
    }

    private void FixedUpdate()
    {
        UpdateTimers();

        if (movementPaused)
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.gravityScale = 0f;
            }

            return;
        }

        if (isGrounded && !isGroundPounding)
        {
            ClearAirborneHorizontalSpeedLerp();
            SetRotationLocked(true);
            ScanSurface();

            if (obstacleRecoilTimer > 0f)
            {
                AlignToSurface();
                if (surfaceAdhesionLockTimer <= 0f)
                {
                    ApplySurfaceAdhesion();
                }
                return;
            }

            ApplyGroundMovement();
            AlignToSurface();

            if (surfaceAdhesionLockTimer <= 0f)
            {
                ApplySurfaceAdhesion();
            }
        }
        else if (!isGrounded)
        {
            isOnZiplineSurface = false;
            ApplyAirbornePhysics();
        }
    }

    private void ScanSurface()
    {
        isOnZiplineSurface = false;

        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, raycastDistance, groundLayer);
        if (IsZiplineSurface(hit.collider))
        {
            isOnZiplineSurface = true;
            surfaceNormal = hit.normal;
            return;
        }

        if (IsRampSurfaceHit(hit))
        {
            surfaceNormal = hit.normal;
            return;
        }

        if (TerrainManager.Instance != null && TerrainManager.Instance.TryGetSurfaceAtX(transform.position.x, out _, out Vector3 exactNormal))
        {
            surfaceNormal = exactNormal;
            return;
        }

        surfaceNormal = hit.collider != null ? hit.normal : Vector2.up;
    }

    private bool IsZiplineSurface(Collider2D surfaceCollider)
    {
        return surfaceCollider != null && surfaceCollider.GetComponent<ZiplineSurface>() != null;
    }

    private static bool IsRampSurfaceHit(RaycastHit2D hit)
    {
        RampSurface rampSurface = GetRampSurface(hit.collider);
        return rampSurface != null && rampSurface.IsWalkable(hit);
    }

    private static RampSurface GetRampSurface(Collider2D surfaceCollider)
    {
        return surfaceCollider != null ? surfaceCollider.GetComponentInParent<RampSurface>() : null;
    }

    private void CheckGrounded()
    {
        bool wasGrounded = isGrounded;

        if (groundEventSuppressionFrames > 0)
        {
            groundEventSuppressionFrames--;
            isGrounded = wasGrounded;
            return;
        }

        if (surfaceAdhesionLockTimer > 0f)
        {
            isGrounded = false;
            return;
        }

        Vector2 checkPos = (Vector2)transform.position + groundCheckOffset;
        bool rawGrounded = false;
        bool isTouchingZipline = false;
        Collider2D[] groundHits = Physics2D.OverlapCircleAll(checkPos, groundCheckRadius, groundLayer);
        for (int i = 0; i < groundHits.Length; i++)
        {
            Collider2D groundHit = groundHits[i];
            if (groundHit == null)
            {
                continue;
            }

            if (IsZiplineSurface(groundHit))
            {
                isTouchingZipline = true;
                continue;
            }

            rawGrounded = true;
            break;
        }

        if (!rawGrounded && isTouchingZipline)
        {
            rawGrounded = IsStandingOnZiplineSurface();
        }

        isGrounded = rawGrounded;

        if (!wasGrounded && isGrounded)
        {
            OnLanded();
        }
    }

    /// <summary>
    /// Captures gameplay and Rigidbody2D state before TerrainManager performs a
    /// floating-origin teleport. OnWorldShift() restores this snapshot after the
    /// transform move so the shift cannot masquerade as a jump or landing.
    /// </summary>
    public void CaptureWorldShiftState()
    {
        hasWorldShiftSnapshot = true;
        worldShiftGrounded = isGrounded;
        worldShiftGliding = isGliding;
        worldShiftGroundPounding = isGroundPounding;
        worldShiftOnZiplineSurface = isOnZiplineSurface;
        worldShiftGlideGravityBlend = glideGravityBlend;
        worldShiftSurfaceNormal = surfaceNormal;
        worldShiftSurfaceAdhesionLockTimer = surfaceAdhesionLockTimer;
        worldShiftRotation = transform.rotation;

        if (rb != null)
        {
            worldShiftVelocity = rb.linearVelocity;
            worldShiftAngularVelocity = rb.angularVelocity;
            worldShiftGravityScale = rb.gravityScale;
            worldShiftGroundForwardSpeed = groundForwardSpeed;
            worldShiftObstacleRecoilTimer = obstacleRecoilTimer;
        }
    }

    public void OnWorldShift()
    {
        groundEventSuppressionFrames = Mathf.Max(
            groundEventSuppressionFrames,
            WorldShiftGroundEventSuppressionFrames);

        if (!hasWorldShiftSnapshot)
        {
            CaptureWorldShiftState();
        }

        RestoreWorldShiftState();
    }

    private void RestoreWorldShiftState()
    {
        isGrounded = worldShiftGrounded;
        isGliding = worldShiftGliding;
        isGroundPounding = worldShiftGroundPounding;
        isOnZiplineSurface = worldShiftOnZiplineSurface;
        glideGravityBlend = worldShiftGlideGravityBlend;
        surfaceNormal = worldShiftSurfaceNormal;
        surfaceAdhesionLockTimer = worldShiftSurfaceAdhesionLockTimer;
        groundForwardSpeed = worldShiftGroundForwardSpeed;
        obstacleRecoilTimer = worldShiftObstacleRecoilTimer;
        SetPlayerRotation(NormalizeAngle(worldShiftRotation.eulerAngles.z));

        if (rb != null)
        {
            rb.linearVelocity = worldShiftVelocity;
            rb.angularVelocity = worldShiftAngularVelocity;
            rb.gravityScale = worldShiftGravityScale;
            rb.freezeRotation = true;
        }

        hasWorldShiftSnapshot = false;
    }

    private bool IsStandingOnZiplineSurface()
    {
        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, raycastDistance, groundLayer);
        return IsZiplineSurface(hit.collider) && hit.normal.y > 0.1f;
    }

    private void OnLanded()
    {
        bool landedFromGroundPound = isGroundPounding;

        ScanSurface();
        CheckLandingOrientation();

        Vector2 tangent = GetForwardSurfaceTangent();
        float forwardSpeed = Mathf.Max(0f, Vector2.Dot(rb.linearVelocity, tangent));

        CancelGlide();
        isGroundPounding = false;
        landedFromGroundPoundThisFrame = landedFromGroundPound;
        SetRotationLocked(true);

        groundForwardSpeed = Mathf.Clamp(
            forwardSpeed,
            GetMinimumGroundSpeed(),
            GetMaximumGroundSpeed());

        SnapToSurfaceIfPossible();
        rb.linearVelocity = tangent * groundForwardSpeed;

        if (landedFromGroundPound)
        {
            lastGroundPoundImpactPoint = (Vector2)transform.position + groundCheckOffset;
            Camera mainCamera = Camera.main;
            mainCamera?.GetComponent<CameraFollow>()?.Shake();
            AudioManager.Instance?.PlayGroundPoundSound();
            TriggerGroundPoundEffects(lastGroundPoundImpactPoint);
            GroundPoundLanded?.Invoke(this);
        }

        if (isGrounded)
        {
            Landed?.Invoke(this);
        }
    }

    private void ApplySurfaceAdhesion()
    {
        rb.gravityScale = 0f;

        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, raycastDistance, groundLayer);
        if (hit.collider == null)
        {
            return;
        }

        float targetY = hit.point.y - groundCheckOffset.y;
        float error = targetY - transform.position.y;
        float adhesionAcceleration = Mathf.Clamp(error / Time.fixedDeltaTime, -Mathf.Max(0f, adhesionForce), 2f);
        rb.AddForce(Vector2.up * adhesionAcceleration * rb.mass, ForceMode2D.Force);
    }

    private void ApplyGroundMovement()
    {
        if (IsMovementRooted)
        {
            groundForwardSpeed = 0f;
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 tangent = GetForwardSurfaceTangent();
        GetActiveMovementMultipliers(
            out float horizontalSpeedMultiplier,
            out _,
            out _);

        float deltaTime = Time.fixedDeltaTime;
        float targetRunSpeed = GetTargetRunSpeed(horizontalSpeedMultiplier);
        float minimumSpeed = GetMinimumGroundSpeed(horizontalSpeedMultiplier);
        float maximumSpeed = GetMaximumGroundSpeed(horizontalSpeedMultiplier);

        if (TryGetSlopeAngle(out float slopeAngle) && Mathf.Abs(slopeAngle) > 2f)
        {
            groundForwardSpeed += GetSlopeAcceleration(slopeAngle) * deltaTime;
        }
        else
        {
            groundForwardSpeed = Mathf.MoveTowards(
                groundForwardSpeed,
                targetRunSpeed,
                Mathf.Max(0f, flatGroundSpeedReturnRate) * deltaTime);
        }

        groundForwardSpeed = Mathf.Clamp(groundForwardSpeed, minimumSpeed, maximumSpeed);
        rb.linearVelocity = tangent * groundForwardSpeed;
    }

    private float GetSlopeAcceleration(float slopeAngle)
    {
        float slopeRadians = slopeAngle * Mathf.Deg2Rad;
        return -Mathf.Sin(slopeRadians) * Mathf.Max(0f, slopeAccelerationStrength);
    }

    private void AlignToSurface()
    {
        float targetAngle = Mathf.Atan2(surfaceNormal.x, surfaceNormal.y) * Mathf.Rad2Deg * -1f;
        SetPlayerRotation(targetAngle);
    }

    private void ApplyAirbornePhysics()
    {
        SetRotationLocked(true);

        if (isGroundPounding)
        {
            ClearAirborneHorizontalSpeedLerp();
            ResetGlideGravityBlend();
            rb.gravityScale = gravityScale;
            return;
        }

        GetActiveMovementMultipliers(
            out float activeHorizontalSpeedMultiplier,
            out float activeGlideGravityMultiplier,
            out float activeAirborneGravityMultiplier);

        UpdateGlideGravityBlend();
        float glideGravityScale = Mathf.Lerp(
            1f,
            Mathf.Max(0f, glideGravityMultiplier) * activeGlideGravityMultiplier,
            glideGravityBlend);
        rb.gravityScale = gravityScale * activeAirborneGravityMultiplier * glideGravityScale;

        float horizontalSpeedLerpDuration = GetActiveHorizontalSpeedLerpDuration(out Object horizontalSpeedLerpSource);
        ApplyAirborneHorizontalSpeedTransition(activeHorizontalSpeedMultiplier, horizontalSpeedLerpDuration, horizontalSpeedLerpSource);
    }

    public void AddRegulationMovementModifier(Object source, float horizontalSpeedMultiplier, float glideGravityMultiplier)
    {
        AddMovementModifier(source, horizontalSpeedMultiplier, glideGravityMultiplier);
    }

    public void AddRegulationMovementModifier(
        Object source,
        float horizontalSpeedMultiplier,
        float glideGravityMultiplier,
        float airborneGravityMultiplier)
    {
        AddMovementModifier(
            source,
            horizontalSpeedMultiplier,
            glideGravityMultiplier,
            airborneGravityMultiplier,
            0f);
    }

    public void AddRegulationMovementModifier(
        Object source,
        float horizontalSpeedMultiplier,
        float glideGravityMultiplier,
        float airborneGravityMultiplier,
        float horizontalSpeedLerpDuration)
    {
        AddMovementModifier(
            source,
            horizontalSpeedMultiplier,
            glideGravityMultiplier,
            airborneGravityMultiplier,
            horizontalSpeedLerpDuration);
    }

    public void AddMovementModifier(
        Object source,
        float horizontalSpeedMultiplier,
        float glideGravityMultiplier,
        float airborneGravityMultiplier = 1f,
        float horizontalSpeedLerpDuration = 0f,
        float jumpForwardImpulseMultiplier = 1f,
        float jumpForceMultiplier = 1f)
    {
        if (source == null)
        {
            return;
        }

        if (regulationMovementModifiers.TryGetValue(source, out RegulationMovementModifier existingModifier))
        {
            existingModifier.SetTarget(
                horizontalSpeedMultiplier,
                glideGravityMultiplier,
                airborneGravityMultiplier,
                horizontalSpeedLerpDuration,
                jumpForwardImpulseMultiplier,
                jumpForceMultiplier);
            regulationMovementModifiers[source] = existingModifier;
        }
        else
        {
            regulationMovementModifiers[source] = new RegulationMovementModifier(
                horizontalSpeedMultiplier,
                glideGravityMultiplier,
                airborneGravityMultiplier,
                horizontalSpeedLerpDuration,
                jumpForwardImpulseMultiplier,
                jumpForceMultiplier,
                source == characterRuntime || source is CharacterRuntime);
        }
    }

    public void ApplyTimedMovementModifier(
        Object source,
        float horizontalSpeedMultiplier,
        float jumpForceMultiplier,
        float duration)
    {
        if (source == null)
        {
            return;
        }

        if (timedMovementModifierRoutines.TryGetValue(source, out Coroutine existingRoutine))
        {
            StopCoroutine(existingRoutine);
        }

        AddMovementModifier(
            source,
            horizontalSpeedMultiplier,
            1f,
            1f,
            jumpForceMultiplier: jumpForceMultiplier);

        timedMovementModifierRoutines[source] = StartCoroutine(
            RemoveMovementModifierAfterDelay(source, Mathf.Max(0f, duration)));
    }

    public void RemoveRegulationMovementModifier(Object source)
    {
        RemoveMovementModifier(source);
    }

    public void RemoveMovementModifier(Object source)
    {
        if (ReferenceEquals(source, null))
        {
            return;
        }

        if (timedMovementModifierRoutines.TryGetValue(source, out Coroutine timedRoutine))
        {
            StopCoroutine(timedRoutine);
            timedMovementModifierRoutines.Remove(source);
        }

        regulationMovementModifiers.Remove(source);
        if (source == airborneHorizontalSpeedLerpSource)
        {
            ClearAirborneHorizontalSpeedLerp();
        }
    }

    private IEnumerator RemoveMovementModifierAfterDelay(Object source, float duration)
    {
        yield return new WaitForSeconds(duration);
        timedMovementModifierRoutines.Remove(source);
        regulationMovementModifiers.Remove(source);
        if (source == airborneHorizontalSpeedLerpSource)
        {
            ClearAirborneHorizontalSpeedLerp();
        }
    }

    public void LaunchVertically(float upwardSpeed, float adhesionLockDuration = 0.12f)
    {
        float horizontalSpeed = rb != null ? rb.linearVelocity.x : 0f;
        Launch(upwardSpeed, horizontalSpeed, adhesionLockDuration, true);
    }

    public void Launch(float upwardSpeed, float horizontalSpeed, float adhesionLockDuration = 0.12f, bool cancelGlide = true)
    {
        if (rb == null)
        {
            return;
        }

        isGrounded = false;
        if (cancelGlide)
        {
            CancelGlide();
        }

        isGroundPounding = false;
        landedFromGroundPoundThisFrame = false;
        isOnZiplineSurface = false;
        surfaceAdhesionLockTimer = Mathf.Max(surfaceAdhesionLockTimer, Mathf.Max(0f, adhesionLockDuration));
        rb.gravityScale = gravityScale;
        rb.linearVelocity = new Vector2(horizontalSpeed, Mathf.Max(0f, upwardSpeed));
        SetRotationLocked(true);
    }

    public void ApplySparkDisruption(float verticalVelocityAfterHit = 0f, float adhesionLockDuration = 0.08f)
    {
        if (rb == null)
        {
            return;
        }

        Vector2 velocity = rb.linearVelocity;
        velocity.y = verticalVelocityAfterHit;

        CancelGlide();
        isGroundPounding = false;
        landedFromGroundPoundThisFrame = false;
        isOnZiplineSurface = false;
        surfaceAdhesionLockTimer = Mathf.Max(surfaceAdhesionLockTimer, Mathf.Max(0f, adhesionLockDuration));
        rb.gravityScale = gravityScale;
        rb.linearVelocity = velocity;
        SetRotationLocked(true);
    }

    public void ApplyObstacleRecoil(Vector2 direction, float horizontalSpeed, float maxVerticalVelocity, float duration)
    {
        if (rb == null)
        {
            return;
        }

        float directionX = Mathf.Abs(direction.x) > 0.01f ? Mathf.Sign(direction.x) : -1f;
        float speed = Mathf.Max(0f, horizontalSpeed);
        Vector2 velocity = rb.linearVelocity;
        velocity.x = directionX * speed;
        velocity.y = Mathf.Min(velocity.y, maxVerticalVelocity);

        CancelGlide();
        isGroundPounding = false;
        landedFromGroundPoundThisFrame = false;
        obstacleRecoilTimer = Mathf.Max(obstacleRecoilTimer, Mathf.Max(0f, duration));
        groundForwardSpeed = Mathf.Min(groundForwardSpeed, GetMinimumGroundSpeed());
        rb.gravityScale = gravityScale;
        rb.linearVelocity = velocity;
        SetRotationLocked(true);
    }

    private void SetRotationLocked(bool locked)
    {
        if (rb == null)
        {
            return;
        }

        rb.freezeRotation = locked;
        if (locked)
        {
            rb.angularVelocity = 0f;
        }
    }

    private void ApplyAirborneHorizontalSpeedTransition(float horizontalSpeedMultiplier, float horizontalSpeedLerpDuration, Object horizontalSpeedLerpSource)
    {
        Vector2 velocity = rb.linearVelocity;
        if (horizontalSpeedLerpDuration > 0f && horizontalSpeedLerpSource != null)
        {
            if (airborneHorizontalSpeedLerpSource != horizontalSpeedLerpSource)
            {
                airborneHorizontalSpeedLerpSource = horizontalSpeedLerpSource;
                airborneHorizontalSpeedLerpStartSpeed = velocity.x;
                airborneHorizontalSpeedLerpElapsed = 0f;
            }

            float reducedTargetForwardSpeed = Mathf.Max(0f, moveSpeed * horizontalSpeedMultiplier);
            airborneHorizontalSpeedLerpElapsed = Mathf.Min(
                horizontalSpeedLerpDuration,
                airborneHorizontalSpeedLerpElapsed + Time.fixedDeltaTime);

            float t = Mathf.Clamp01(airborneHorizontalSpeedLerpElapsed / horizontalSpeedLerpDuration);
            velocity.x = Mathf.Lerp(airborneHorizontalSpeedLerpStartSpeed, reducedTargetForwardSpeed, t);
            rb.linearVelocity = velocity;
            return;
        }

        ClearAirborneHorizontalSpeedLerp();
    }

    private void GetActiveMovementMultipliers(
        out float horizontalSpeedMultiplier,
        out float activeGlideGravityMultiplier,
        out float activeAirborneGravityMultiplier)
    {
        horizontalSpeedMultiplier = 1f;
        activeGlideGravityMultiplier = 1f;
        activeAirborneGravityMultiplier = 1f;

        foreach (RegulationMovementModifier modifier in regulationMovementModifiers.Values)
        {
            horizontalSpeedMultiplier *= modifier.horizontalSpeedMultiplier;
            activeGlideGravityMultiplier *= modifier.glideGravityMultiplier;
            activeAirborneGravityMultiplier *= modifier.airborneGravityMultiplier;
        }
    }

    private void GetActiveAdvancedMovementMultipliers(
        out float jumpForwardImpulseMultiplier,
        out float jumpForceMultiplier)
    {
        jumpForwardImpulseMultiplier = 1f;
        jumpForceMultiplier = 1f;

        foreach (RegulationMovementModifier modifier in regulationMovementModifiers.Values)
        {
            jumpForwardImpulseMultiplier *= modifier.jumpForwardImpulseMultiplier;
            jumpForceMultiplier *= modifier.jumpForceMultiplier;
        }
    }

    private float GetActiveHorizontalSpeedLerpDuration(out Object source)
    {
        source = null;
        float horizontalSpeedLerpDuration = 0f;
        foreach (KeyValuePair<Object, RegulationMovementModifier> entry in regulationMovementModifiers)
        {
            if (entry.Value.horizontalSpeedLerpDuration <= 0f)
            {
                continue;
            }

            if (source == null || entry.Value.horizontalSpeedLerpDuration < horizontalSpeedLerpDuration)
            {
                source = entry.Key;
                horizontalSpeedLerpDuration = entry.Value.horizontalSpeedLerpDuration;
            }
        }

        return horizontalSpeedLerpDuration;
    }

    private void ClearAirborneHorizontalSpeedLerp()
    {
        airborneHorizontalSpeedLerpSource = null;
        airborneHorizontalSpeedLerpStartSpeed = 0f;
        airborneHorizontalSpeedLerpElapsed = 0f;
    }

    public void ApplyRegulationLift(float liftAcceleration, float maxUpwardSpeed)
    {
        if (!IsAirborne || rb == null)
        {
            return;
        }

        float acceleration = Mathf.Max(0f, liftAcceleration);
        if (acceleration <= 0f)
        {
            return;
        }

        Vector2 velocity = rb.linearVelocity;
        float speedCap = Mathf.Max(0f, maxUpwardSpeed);
        if (speedCap > 0f && velocity.y >= speedCap)
        {
            return;
        }

        velocity.y += acceleration * Time.fixedDeltaTime;
        if (speedCap > 0f)
        {
            velocity.y = Mathf.Min(velocity.y, speedCap);
        }

        rb.linearVelocity = velocity;
    }

    private bool HandleJump()
    {
        if (IsInputBlocked || IsJumpBlocked || IsMovementRooted || isGroundPounding || landedFromGroundPoundThisFrame)
        {
            return false;
        }

        if (GetDelayedKeyDown(KeyCode.Space) && CanJumpFromGround(out Vector2 jumpSurfaceNormal))
        {
            surfaceNormal = jumpSurfaceNormal;
            surfaceAdhesionLockTimer = 0.12f;
            rb.gravityScale = gravityScale;

            rb.linearVelocity = GetJumpLaunchVelocity();

            isGrounded = false;
            CancelGlide();
            isGroundPounding = false;
            AudioManager.Instance?.PlayPlayerJumpSound();
            return true;
        }

        return false;
    }

    private bool CanJumpFromGround(out Vector2 jumpSurfaceNormal)
    {
        jumpSurfaceNormal = surfaceNormal.sqrMagnitude > 0.01f ? surfaceNormal.normalized : Vector2.up;
        if (isGrounded)
        {
            return true;
        }

        if (surfaceAdhesionLockTimer > 0f)
        {
            return false;
        }

        Vector2 footPos = (Vector2)transform.position + groundCheckOffset;
        return TryGetNearbyJumpSurface(footPos, out jumpSurfaceNormal);
    }

    private bool TryGetNearbyJumpSurface(Vector2 footPos, out Vector2 jumpSurfaceNormal)
    {
        jumpSurfaceNormal = Vector2.up;

        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, raycastDistance, groundLayer);
        if (IsRampSurfaceHit(hit) && IsFootCloseEnoughToJumpSurface(footPos.y, hit.point.y))
        {
            jumpSurfaceNormal = hit.normal.sqrMagnitude > 0.01f ? hit.normal.normalized : Vector2.up;
            return true;
        }

        if (TerrainManager.Instance != null &&
            TerrainManager.Instance.TryGetSurfaceAtX(transform.position.x, out Vector3 surfacePos, out Vector3 exactNormal) &&
            IsFootCloseEnoughToJumpSurface(footPos.y, surfacePos.y))
        {
            jumpSurfaceNormal = exactNormal.sqrMagnitude > 0.01f ? ((Vector2)exactNormal).normalized : Vector2.up;
            return true;
        }

        if (hit.collider != null && IsFootCloseEnoughToJumpSurface(footPos.y, hit.point.y))
        {
            jumpSurfaceNormal = hit.normal.sqrMagnitude > 0.01f ? hit.normal.normalized : Vector2.up;
            return !IsZiplineSurface(hit.collider) || hit.normal.y > 0.1f;
        }

        return false;
    }

    private bool IsFootCloseEnoughToJumpSurface(float footY, float surfaceY)
    {
        float verticalGap = footY - surfaceY;
        return verticalGap >= -groundCheckRadius && verticalGap <= jumpGroundedTolerance;
    }

    private Vector2 GetJumpLaunchVelocity()
    {
        Vector2 normal = surfaceNormal.sqrMagnitude > 0.01f ? surfaceNormal.normalized : Vector2.up;
        Vector2 tangent = GetForwardSurfaceTangent();
        BiomeData activeBiome = GetActiveBiome();
        float forwardMultiplier = activeBiome != null ? activeBiome.jumpForwardMultiplier : 1f;
        float verticalMultiplier = activeBiome != null ? activeBiome.jumpForceMultiplier : 1f;
        if (characterRuntime != null)
        {
            forwardMultiplier = characterRuntime.GetBiomeJumpForwardMultiplier(activeBiome);
            verticalMultiplier = characterRuntime.GetBiomeJumpForceMultiplier(activeBiome);
        }

        float tangentSpeed = Mathf.Max(groundForwardSpeed, Vector2.Dot(rb.linearVelocity, tangent));
        GetActiveAdvancedMovementMultipliers(
            out float activeJumpForwardImpulseMultiplier,
            out float activeJumpForceMultiplier);
        float momentumLaunchSpeed = Mathf.Max(
            0f,
            tangentSpeed + tangentSpeed * jumpForwardImpulseMultiplier * forwardMultiplier * activeJumpForwardImpulseMultiplier);
        float stoppedJumpForwardSpeed = Mathf.Max(0f, moveSpeed) * Mathf.Clamp01(stoppedJumpForwardSpeedMultiplier);
        float launchForwardSpeed = Mathf.Max(momentumLaunchSpeed, stoppedJumpForwardSpeed);
        float rampBoost = GetUphillRampLaunchBoost();

        return tangent * launchForwardSpeed +
            normal * (jumpForce * verticalMultiplier * activeJumpForceMultiplier) +
            Vector2.up * rampBoost;
    }

    private Vector2 GetForwardSurfaceTangent()
    {
        Vector2 tangent = new Vector2(surfaceNormal.y, -surfaceNormal.x).normalized;
        return tangent.x < 0f ? -tangent : tangent;
    }

    private float GetUphillRampLaunchBoost()
    {
        if (!TryGetSlopeAngle(out float slopeAngle))
        {
            return 0f;
        }

        float uphillAngle = Mathf.Max(0f, slopeAngle);
        if (uphillAngle <= uphillRampLaunchStartAngle)
        {
            return 0f;
        }

        float normalizedUphill = Mathf.InverseLerp(uphillRampLaunchStartAngle, uphillRampLaunchMaxAngle, uphillAngle);
        float tangentSpeed = Mathf.Max(groundForwardSpeed, Vector2.Dot(rb.linearVelocity, GetForwardSurfaceTangent()));
        float boost = tangentSpeed * Mathf.Max(0f, uphillRampLaunchMultiplier) * normalizedUphill;
        return Mathf.Min(boost, Mathf.Max(0f, maxUphillRampLaunchBoost));
    }

    private void HandleGlide(bool jumpedThisFrame)
    {
        if (IsInputBlocked || IsMovementRooted || isGrounded || isGroundPounding || jumpedThisFrame)
        {
            CancelGlide();
            return;
        }

        isGliding = IsGlideInputHeld;
    }

    private void HandleGroundPound()
    {
        if (!IsInputBlocked && !IsMovementRooted && !isGrounded && GetDelayedKeyDown(KeyCode.G))
        {
            CancelGlide();
            SetRotationLocked(true);
            isGroundPounding = true;
            rb.gravityScale = gravityScale;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -groundPoundSpeed);
        }
    }

    private void CheckLandingOrientation()
    {
        float surfaceAngle = Mathf.Atan2(surfaceNormal.x, surfaceNormal.y) * Mathf.Rad2Deg * -1f;
        SetPlayerRotation(surfaceAngle);
    }

    private void SetPlayerRotation(float zAngle)
    {
        transform.rotation = Quaternion.Euler(0f, 0f, zAngle);

        if (rb != null)
        {
            rb.rotation = zAngle;
        }
    }

    private float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f)
        {
            angle -= 360f;
        }
        else if (angle < -180f)
        {
            angle += 360f;
        }

        return angle;
    }

    private void UpdateTimers()
    {
        UpdateMovementModifierTransitions();

        if (surfaceAdhesionLockTimer > 0f)
        {
            surfaceAdhesionLockTimer = Mathf.Max(0f, surfaceAdhesionLockTimer - Time.fixedDeltaTime);
        }

        if (obstacleRecoilTimer > 0f)
        {
            obstacleRecoilTimer = Mathf.Max(0f, obstacleRecoilTimer - Time.fixedDeltaTime);
        }
    }

    private void UpdateMovementModifierTransitions()
    {
        if (regulationMovementModifiers.Count == 0)
        {
            return;
        }

        movementModifierUpdateKeys.Clear();
        foreach (Object source in regulationMovementModifiers.Keys)
        {
            movementModifierUpdateKeys.Add(source);
        }

        for (int i = 0; i < movementModifierUpdateKeys.Count; i++)
        {
            Object source = movementModifierUpdateKeys[i];
            if (!regulationMovementModifiers.TryGetValue(source, out RegulationMovementModifier modifier))
            {
                continue;
            }

            modifier.TickHorizontalSpeedLerp(Time.fixedDeltaTime);
            regulationMovementModifiers[source] = modifier;
        }

        movementModifierUpdateKeys.Clear();
    }

    private void UpdateGlideGravityBlend()
    {
        if (glideGravityBlendTime <= 0f)
        {
            glideGravityBlend = isGliding ? 1f : 0f;
            return;
        }

        float targetBlend = isGliding ? 1f : 0f;
        glideGravityBlend = Mathf.MoveTowards(
            glideGravityBlend,
            targetBlend,
            Time.fixedDeltaTime / glideGravityBlendTime);
    }

    private void CancelGlide()
    {
        isGliding = false;
        ResetGlideGravityBlend();
    }

    private void ResetGlideGravityBlend()
    {
        glideGravityBlend = 0f;
    }

    private void TriggerGroundPoundEffects(Vector2 impactPoint)
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = true;

        HashSet<Component> triggeredTargets = new HashSet<Component>();

        TriggerGroundPoundEffectsForCollider(
            groundPoundEffectCollider,
            filter,
            triggeredTargets,
            impactPoint,
            groundPoundObstacleTag);
        TriggerGroundPoundEffectsForCollider(
            groundPoundLegCollider,
            filter,
            triggeredTargets,
            impactPoint,
            groundPoundRegulationTag);
    }

    private void TriggerGroundPoundEffectsForCollider(
        Collider2D effectCollider,
        ContactFilter2D filter,
        HashSet<Component> triggeredTargets,
        Vector2 impactPoint,
        string allowedTag)
    {
        if (effectCollider == null)
        {
            return;
        }

        int hitCount = effectCollider.Overlap(filter, groundPoundEffectHits);
        if (hitCount <= 0)
        {
            return;
        }

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = groundPoundEffectHits[i];
            if (hit == null)
            {
                continue;
            }

            IGroundPoundTarget target = hit.GetComponentInParent<IGroundPoundTarget>();
            if (target == null)
            {
                continue;
            }

            Component targetComponent = target as Component;
            if (targetComponent == null || !MatchesGroundPoundTag(hit, targetComponent, allowedTag))
            {
                continue;
            }

            if (!triggeredTargets.Add(targetComponent))
            {
                continue;
            }

            if (target.TryHandleGroundPound(this, impactPoint))
            {
                ScoreManager.Instance?.AddGroundPoundClearScore();
            }
        }
    }

    private bool MatchesGroundPoundTag(Collider2D hit, Component targetComponent, string allowedTag)
    {
        if (string.IsNullOrEmpty(allowedTag))
        {
            return true;
        }

        return (hit != null && hit.CompareTag(allowedTag)) ||
            (targetComponent != null && targetComponent.CompareTag(allowedTag));
    }

    private BiomeData GetActiveBiome()
    {
        if (BiomeManager.Instance != null)
        {
            return BiomeManager.Instance.CurrentBiome;
        }

        return TerrainManager.Instance != null ? TerrainManager.Instance.CurrentBiome : null;
    }

    private bool TryGetSlopeAngle(out float slopeAngle)
    {
        float sampleDistance = Mathf.Max(0.05f, slopeSampleDistance);
        float leftX = transform.position.x - sampleDistance;
        float rightX = transform.position.x + sampleDistance;

        if (TryGetSurfaceHeightAtX(leftX, out float leftY) && TryGetSurfaceHeightAtX(rightX, out float rightY))
        {
            slopeAngle = Mathf.Atan2(rightY - leftY, sampleDistance * 2f) * Mathf.Rad2Deg;
            return true;
        }

        slopeAngle = Mathf.Atan2(surfaceNormal.x, surfaceNormal.y) * Mathf.Rad2Deg * -1f;
        return true;
    }

    private bool TryGetSurfaceHeightAtX(float worldX, out float surfaceY)
    {
        Vector2 origin = new Vector2(worldX, transform.position.y + raycastDistance);
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, raycastDistance * 2f, groundLayer);
        if (isOnZiplineSurface && IsZiplineSurface(hit.collider))
        {
            surfaceY = hit.point.y;
            return true;
        }

        if (IsRampSurfaceHit(hit))
        {
            surfaceY = hit.point.y;
            return true;
        }

        if (TerrainManager.Instance != null && TerrainManager.Instance.TryGetSurfaceAtX(worldX, out Vector3 surfacePos, out _))
        {
            surfaceY = surfacePos.y;
            return true;
        }

        if (hit.collider != null)
        {
            surfaceY = hit.point.y;
            return true;
        }

        surfaceY = 0f;
        return false;
    }

    private void SnapToSurfaceIfPossible()
    {
        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, raycastDistance, groundLayer);
        if (hit.collider == null)
        {
            return;
        }

        float targetY = hit.point.y - groundCheckOffset.y;
        rb.position = new Vector2(rb.position.x, targetY);
    }

    private float GetTargetRunSpeed()
    {
        GetActiveMovementMultipliers(out float horizontalSpeedMultiplier, out _, out _);
        return GetTargetRunSpeed(horizontalSpeedMultiplier);
    }

    private float GetTargetRunSpeed(float horizontalSpeedMultiplier)
    {
        return Mathf.Max(0f, moveSpeed * horizontalSpeedMultiplier);
    }

    private float GetMinimumGroundSpeed()
    {
        GetActiveMovementMultipliers(out float horizontalSpeedMultiplier, out _, out _);
        return GetMinimumGroundSpeed(horizontalSpeedMultiplier);
    }

    private float GetMinimumGroundSpeed(float horizontalSpeedMultiplier)
    {
        return Mathf.Min(
            Mathf.Max(0f, minGroundSpeed * horizontalSpeedMultiplier),
            GetMaximumGroundSpeed(horizontalSpeedMultiplier));
    }

    private float GetMaximumGroundSpeed()
    {
        GetActiveMovementMultipliers(out float horizontalSpeedMultiplier, out _, out _);
        return GetMaximumGroundSpeed(horizontalSpeedMultiplier);
    }

    private float GetMaximumGroundSpeed(float horizontalSpeedMultiplier)
    {
        float targetRunSpeed = GetTargetRunSpeed(horizontalSpeedMultiplier);
        return Mathf.Max(targetRunSpeed, Mathf.Max(0f, maxGroundSpeed * horizontalSpeedMultiplier));
    }

    public void SetBaseMovementStats(float speed, float jump, float glideMultiplier)
    {
        moveSpeed = Mathf.Max(0f, speed);
        jumpForce = Mathf.Max(0f, jump);
        glideGravityMultiplier = Mathf.Clamp01(glideMultiplier);
        if (!IsMovementRooted)
        {
            groundForwardSpeed = Mathf.Max(groundForwardSpeed, GetTargetRunSpeed());
        }
    }

    public void SetMovementRooted(Object source, bool rooted)
    {
        if (ReferenceEquals(source, null))
        {
            return;
        }

        bool wasRooted = PruneAndCheckMovementRoots();
        if (rooted)
        {
            movementRootSources.Add(source);
        }
        else
        {
            movementRootSources.Remove(source);
        }

        bool isRooted = PruneAndCheckMovementRoots();
        if (!wasRooted && isRooted)
        {
            EnterMovementRoot();
        }
        else if (wasRooted && !isRooted)
        {
            ExitMovementRoot();
        }
    }

    public void SetInputDelayModifier(Object source, float delaySeconds)
    {
        if (ReferenceEquals(source, null))
        {
            return;
        }

        float delay = Mathf.Max(0f, delaySeconds);
        if (delay <= 0f)
        {
            RemoveInputDelayModifier(source);
            return;
        }

        inputDelayModifiers[source] = delay;
        PruneInputDelayModifiers();
    }

    public void RemoveInputDelayModifier(Object source)
    {
        if (ReferenceEquals(source, null))
        {
            return;
        }

        inputDelayModifiers.Remove(source);
    }

    public bool GetDelayedKey(KeyCode keyCode)
    {
        float delay = GetActiveInputDelay();
        if (delay <= 0f || !IsBufferedInputKey(keyCode))
        {
            return Input.GetKey(keyCode);
        }

        CaptureDelayedInputSnapshot();
        return TryGetDelayedInputSnapshot(Time.time - delay, out DelayedInputSnapshot snapshot, out _)
            && snapshot.GetKey(keyCode);
    }

    public bool GetDelayedKeyDown(KeyCode keyCode)
    {
        float delay = GetActiveInputDelay();
        if (delay <= 0f || !IsBufferedInputKey(keyCode))
        {
            return Input.GetKeyDown(keyCode);
        }

        CaptureDelayedInputSnapshot();
        if (!TryGetDelayedInputSnapshot(Time.time - delay, out DelayedInputSnapshot snapshot, out int snapshotIndex))
        {
            return false;
        }

        if (!snapshot.GetKey(keyCode) || WasDelayedKeyHeldInPreviousSnapshot(keyCode, snapshotIndex))
        {
            return false;
        }

        if (consumedDelayedKeyDownSnapshots.TryGetValue(keyCode, out int consumedIndex) &&
            consumedIndex == snapshotIndex)
        {
            return false;
        }

        consumedDelayedKeyDownSnapshots[keyCode] = snapshotIndex;
        return true;
    }

    public void SetInputBlocked(Object source, bool blocked)
    {
        if (ReferenceEquals(source, null))
        {
            return;
        }

        bool wasBlocked = PruneAndCheckInputBlocks();
        if (blocked)
        {
            inputBlockSources.Add(source);
        }
        else
        {
            inputBlockSources.Remove(source);
        }

        bool isBlocked = PruneAndCheckInputBlocks();
        if (!wasBlocked && isBlocked)
        {
            CancelGlide();
            isGroundPounding = false;
        }
    }

    public void SetJumpBlocked(Object source, bool blocked)
    {
        if (ReferenceEquals(source, null))
        {
            return;
        }

        if (blocked)
        {
            jumpBlockSources.Add(source);
        }
        else
        {
            jumpBlockSources.Remove(source);
        }

        PruneAndCheckJumpBlocks();
    }

    private bool PruneAndCheckInputBlocks()
    {
        inputBlockSources.RemoveWhere(source => source == null);
        return inputBlockSources.Count > 0;
    }

    private float GetActiveInputDelay()
    {
        if (inputDelayModifiers.Count == 0)
        {
            return 0f;
        }

        PruneInputDelayModifiers();
        float activeDelay = 0f;
        foreach (float delay in inputDelayModifiers.Values)
        {
            activeDelay += Mathf.Max(0f, delay);
        }

        return activeDelay;
    }

    private void PruneInputDelayModifiers()
    {
        if (inputDelayModifiers.Count == 0)
        {
            return;
        }

        inputDelayModifierUpdateKeys.Clear();
        foreach (KeyValuePair<Object, float> entry in inputDelayModifiers)
        {
            if (entry.Key == null || entry.Value <= 0f)
            {
                inputDelayModifierUpdateKeys.Add(entry.Key);
            }
        }

        for (int i = 0; i < inputDelayModifierUpdateKeys.Count; i++)
        {
            inputDelayModifiers.Remove(inputDelayModifierUpdateKeys[i]);
        }

        inputDelayModifierUpdateKeys.Clear();
    }

    private void CaptureDelayedInputSnapshot()
    {
        if (delayedInputSnapshotFrame == Time.frameCount)
        {
            return;
        }

        delayedInputSnapshotFrame = Time.frameCount;
        delayedInputSnapshots.Add(new DelayedInputSnapshot(Time.time));
        TrimDelayedInputSnapshots();
    }

    private void TrimDelayedInputSnapshots()
    {
        float retentionSeconds = Mathf.Max(
            InputSnapshotHistorySeconds,
            GetActiveInputDelay() + InputSnapshotRetentionPadding);
        float oldestTime = Time.time - retentionSeconds;
        int removeCount = 0;
        while (removeCount < delayedInputSnapshots.Count &&
            delayedInputSnapshots[removeCount].time < oldestTime)
        {
            removeCount++;
        }

        if (removeCount > 0)
        {
            delayedInputSnapshots.RemoveRange(0, removeCount);
            consumedDelayedKeyDownSnapshots.Clear();
        }
    }

    private bool TryGetDelayedInputSnapshot(float targetTime, out DelayedInputSnapshot snapshot, out int snapshotIndex)
    {
        for (int i = delayedInputSnapshots.Count - 1; i >= 0; i--)
        {
            DelayedInputSnapshot candidate = delayedInputSnapshots[i];
            if (candidate.time <= targetTime)
            {
                snapshot = candidate;
                snapshotIndex = i;
                return true;
            }
        }

        snapshot = default;
        snapshotIndex = -1;
        return false;
    }

    private bool WasDelayedKeyHeldInPreviousSnapshot(KeyCode keyCode, int snapshotIndex)
    {
        int previousIndex = snapshotIndex - 1;
        return previousIndex >= 0 &&
            previousIndex < delayedInputSnapshots.Count &&
            delayedInputSnapshots[previousIndex].GetKey(keyCode);
    }

    private static bool IsBufferedInputKey(KeyCode keyCode)
    {
        return keyCode == KeyCode.Space ||
            keyCode == KeyCode.G ||
            keyCode == KeyCode.LeftShift ||
            keyCode == KeyCode.RightShift;
    }

    private bool PruneAndCheckJumpBlocks()
    {
        jumpBlockSources.RemoveWhere(source => source == null);
        return jumpBlockSources.Count > 0;
    }

    private bool PruneAndCheckMovementRoots()
    {
        bool hadRoots = movementRootSources.Count > 0;
        movementRootSources.RemoveWhere(source => source == null);
        bool hasRoots = movementRootSources.Count > 0;

        if (hadRoots && !hasRoots)
        {
            ExitMovementRoot();
        }

        return hasRoots;
    }

    private void EnterMovementRoot()
    {
        groundForwardSpeed = 0f;
        CancelGlide();
        isGroundPounding = false;

        if (rb != null && isGrounded)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    private void ExitMovementRoot()
    {
        if (movementPaused)
        {
            return;
        }

        groundForwardSpeed = Mathf.Max(groundForwardSpeed, GetTargetRunSpeed());
    }

    public void SetMovementPaused(bool paused)
    {
        if (movementPaused == paused)
        {
            return;
        }

        movementPaused = paused;
        if (rb == null)
        {
            return;
        }

        if (movementPaused)
        {
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale = 0f;
            CancelGlide();
            isGroundPounding = false;
        }
        else
        {
            rb.gravityScale = gravityScale;
            groundForwardSpeed = Mathf.Max(groundForwardSpeed, GetTargetRunSpeed());
        }
    }
}
