using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerSunkenPlatformStatus : MonoBehaviour, IManualStatusEscapeSource
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int TintColorId = Shader.PropertyToID("_TintColor");

    [Header("Character Visual Sink")]
    [Tooltip("Visual-only child that contains the character artwork. Never assign the Player/physics root.")]
    [SerializeField] private Transform characterVisualRoot;
    [Tooltip("Local distance the character artwork moves into the terrain.")]
    [Min(0f)] [SerializeField] private float sinkDistance = 1f;
    [Tooltip("Seconds used to animate the character artwork into the terrain when trapped.")]
    [Min(0f)] [SerializeField] private float initialSinkDuration = 0.5f;
    [Tooltip("Seconds used to return to the fully sunk position after Space is released.")]
    [Min(0f)] [SerializeField] private float resetSinkDuration = 0.15f;
    [SerializeField] private AnimationCurve sinkMotionCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Escape Jitter")]
    [Min(0f)] [SerializeField] private float escapeJitterAmplitude = 0.06f;
    [Min(0f)] [SerializeField] private float escapeJitterFrequency = 35f;

    [Header("Stuck Colour")]
    [Tooltip("Renderer tinted while trapped. If empty, the first renderer under Character Visual Root is used.")]
    [SerializeField] private Renderer stuckColorRenderer;
    [SerializeField] private Color stuckColor = new Color(0.35f, 0.22f, 0.12f, 1f);
    [Min(0f)] [SerializeField] private float colorFadeDuration = 0.15f;

    [Header("Emotion")]
    [Min(0f)] [SerializeField] private float emotionIncreasePerTick = 1f;
    [Min(0.01f)] [SerializeField] private float emotionTickInterval = 0.5f;

    private PlayerController player;
    private PlayerStatusManager statusManager;
    private CharacterAbility trappedAbility;
    private SunkenPlatformObstacle activePlatform;
    private Coroutine visualMotionRoutine;
    private Coroutine stuckColorRoutine;
    private SpriteRenderer stuckSpriteRenderer;
    private Material stuckSourceMaterial;
    private Material stuckRuntimeMaterial;
    private Vector3 visualRestLocalPosition;
    private Color originalStuckColor;
    private Color currentStuckColor;
    private int activeChargeDrain;
    private int stuckColorPropertyId;
    private float activeChargeDrainInterval;
    private float escapeHoldDuration;
    private float escapeHoldProgress;
    private float nextDrainTime;
    private float nextEmotionTickTime;
    private float escapeJitterSeed;
    private bool visualRestPositionCached;
    private bool hasStuckColorTarget;
    private bool hasCachedOriginalStuckColor;
    private bool isTrapped;
    private bool isSinkingIn;
    private bool isReturningToSink;
    private bool warnedInvalidVisualRoot;
    private bool warnedMissingStuckColorTarget;
    private bool warnedMissingStuckColorProperty;

    public bool IsTrapped => isTrapped;
    public bool IsSinkingIn => isSinkingIn;
    public bool IsReturningToSink => isReturningToSink;
    public float EscapeProgress01 => escapeHoldDuration <= 0f
        ? (isTrapped ? 1f : 0f)
        : Mathf.Clamp01(escapeHoldProgress / escapeHoldDuration);
    public bool IsManualEscapeActive => isTrapped;
    public bool IsManualEscapeVisible => isTrapped && !isSinkingIn && !isReturningToSink;
    public float ManualEscapeProgress01 => EscapeProgress01;
    public Transform CharacterVisualRoot => characterVisualRoot;

    private void Awake()
    {
        ResolvePlayer();
        CacheVisualRestPosition();
        RestoreVisualImmediately();
    }

    private void OnValidate()
    {
        sinkDistance = Mathf.Max(0f, sinkDistance);
        initialSinkDuration = Mathf.Max(0f, initialSinkDuration);
        resetSinkDuration = Mathf.Max(0f, resetSinkDuration);
        escapeJitterAmplitude = Mathf.Max(0f, escapeJitterAmplitude);
        escapeJitterFrequency = Mathf.Max(0f, escapeJitterFrequency);
        colorFadeDuration = Mathf.Max(0f, colorFadeDuration);
        emotionIncreasePerTick = Mathf.Max(0f, emotionIncreasePerTick);
        emotionTickInterval = Mathf.Max(0.01f, emotionTickInterval);
    }

    private void Update()
    {
        if (!isTrapped)
        {
            return;
        }

        if (activePlatform == null)
        {
            ClearTrapGameplayState();
            return;
        }

        DrainActiveChargeWhenDue();
        AddEmotionWhenDue();

        if (isSinkingIn || isReturningToSink)
        {
            return;
        }

        UpdateEscapeProgress();
    }

    private void OnDisable()
    {
        ClearTrapAndNotifyPlatform();
        RestoreStuckColorImmediately();
    }

    private void OnDestroy()
    {
        ClearTrapAndNotifyPlatform();
        ReleaseStuckColorMaterial();
    }

    public bool TryTrap(
        SunkenPlatformObstacle source,
        int chargeDrain,
        float drainInterval,
        float requiredEscapeHoldDuration)
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

        activePlatform = source;
        activeChargeDrain = Mathf.Max(0, chargeDrain);
        activeChargeDrainInterval = Mathf.Max(0.01f, drainInterval);
        escapeHoldDuration = Mathf.Max(0f, requiredEscapeHoldDuration);
        escapeHoldProgress = 0f;
        nextDrainTime = Time.time + activeChargeDrainInterval;
        nextEmotionTickTime = Time.time + emotionTickInterval;
        escapeJitterSeed = Random.value * 1000f;
        isTrapped = true;
        isSinkingIn = false;
        isReturningToSink = false;

        ResolveAbility();
        player.SetMovementRooted(this, true);
        trappedAbility?.SetActiveAbilityBlocked(this, true);
        BeginSinkVisual();
        ApplyStuckColor();
        return true;
    }

    public void ReleaseFrom(SunkenPlatformObstacle source)
    {
        if (!isTrapped || source == null || activePlatform != source)
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
            ApplyVisualProgress(EscapeProgress01, true);
        }
        else if (escapeHoldProgress > 0f)
        {
            escapeHoldProgress = 0f;
            ApplyVisualProgress(0f);
            BeginReturnToSink();
            return;
        }

        if (escapeHoldProgress >= escapeHoldDuration)
        {
            CompleteEscape();
        }
    }

    private void DrainActiveChargeWhenDue()
    {
        if (Time.time < nextDrainTime)
        {
            return;
        }

        if (trappedAbility == null)
        {
            ResolveAbility();
            trappedAbility?.SetActiveAbilityBlocked(this, true);
        }

        if (activeChargeDrain > 0)
        {
            trappedAbility?.TryDrainActiveCharges(activeChargeDrain);
        }

        nextDrainTime = Time.time + activeChargeDrainInterval;
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

    private void BeginSinkVisual()
    {
        isSinkingIn = true;
        if (!PrepareVisualRoot())
        {
            FinishInitialSink();
            return;
        }

        StartVisualMotion(
            visualRestLocalPosition,
            GetSunkVisualPosition(),
            initialSinkDuration,
            FinishInitialSink);
    }

    private void FinishInitialSink()
    {
        if (HasValidVisualRoot())
        {
            characterVisualRoot.localPosition = GetSunkVisualPosition();
        }

        isSinkingIn = false;
    }

    private void BeginReturnToSink()
    {
        if (!isTrapped || isReturningToSink)
        {
            return;
        }

        isReturningToSink = true;
        if (!HasValidVisualRoot())
        {
            FinishReturnToSink();
            return;
        }

        StartVisualMotion(
            characterVisualRoot.localPosition,
            GetSunkVisualPosition(),
            resetSinkDuration,
            FinishReturnToSink);
    }

    private void FinishReturnToSink()
    {
        if (HasValidVisualRoot())
        {
            characterVisualRoot.localPosition = GetSunkVisualPosition();
        }

        isReturningToSink = false;
    }

    private void ApplyVisualProgress(float normalizedProgress, bool applyJitter = false)
    {
        if (!HasValidVisualRoot() || !visualRestPositionCached)
        {
            return;
        }

        Vector3 basePosition = Vector3.Lerp(
            GetSunkVisualPosition(),
            visualRestLocalPosition,
            Mathf.Clamp01(normalizedProgress));
        characterVisualRoot.localPosition = applyJitter
            ? basePosition + GetEscapeJitterOffset()
            : basePosition;
    }

    private Vector3 GetSunkVisualPosition()
    {
        return visualRestLocalPosition + Vector3.down * sinkDistance;
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

    private void CompleteEscape()
    {
        if (!isTrapped)
        {
            return;
        }

        StopVisualMotion();
        isSinkingIn = false;
        isReturningToSink = false;
        activeChargeDrain = 0;
        nextDrainTime = 0f;
        nextEmotionTickTime = 0f;
        escapeHoldProgress = escapeHoldDuration;
        ApplyVisualProgress(1f);
        trappedAbility?.SetActiveAbilityBlocked(this, false);

        SunkenPlatformObstacle releasedPlatform = activePlatform;
        ClearTrapGameplayState();
        releasedPlatform?.NotifyTrapReleased(this);
    }

    private bool PrepareVisualRoot()
    {
        if (!HasValidVisualRoot())
        {
            WarnInvalidVisualRoot();
            return false;
        }

        CacheVisualRestPosition();
        characterVisualRoot.localPosition = visualRestLocalPosition;
        return true;
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

    private void StartVisualMotion(
        Vector3 from,
        Vector3 to,
        float duration,
        System.Action onComplete)
    {
        StopVisualMotion();

        if (!HasValidVisualRoot())
        {
            onComplete?.Invoke();
            return;
        }

        if (duration <= 0f)
        {
            characterVisualRoot.localPosition = to;
            onComplete?.Invoke();
            return;
        }

        visualMotionRoutine = StartCoroutine(
            MoveVisualRoutine(from, to, duration, onComplete));
    }

    private IEnumerator MoveVisualRoutine(
        Vector3 from,
        Vector3 to,
        float duration,
        System.Action onComplete)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (!HasValidVisualRoot())
            {
                visualMotionRoutine = null;
                onComplete?.Invoke();
                yield break;
            }

            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float curvedProgress = sinkMotionCurve != null
                ? sinkMotionCurve.Evaluate(progress)
                : progress;
            characterVisualRoot.localPosition = Vector3.LerpUnclamped(
                from,
                to,
                curvedProgress);
            yield return null;
        }

        characterVisualRoot.localPosition = to;
        visualMotionRoutine = null;
        onComplete?.Invoke();
    }

    private void ClearTrapAndNotifyPlatform()
    {
        if (!isTrapped)
        {
            StopVisualMotion();
            RestoreVisualImmediately();
            return;
        }

        SunkenPlatformObstacle releasedPlatform = activePlatform;
        ClearTrapGameplayState();
        releasedPlatform?.NotifyTrapReleased(this);
    }

    private void ClearTrapGameplayState()
    {
        StopVisualMotion();

        if (player != null)
        {
            player.SetMovementRooted(this, false);
        }

        trappedAbility?.SetActiveAbilityBlocked(this, false);

        trappedAbility = null;
        activePlatform = null;
        activeChargeDrain = 0;
        activeChargeDrainInterval = 0f;
        escapeHoldDuration = 0f;
        escapeHoldProgress = 0f;
        nextDrainTime = 0f;
        nextEmotionTickTime = 0f;
        isTrapped = false;
        isSinkingIn = false;
        isReturningToSink = false;

        RestoreVisualImmediately();
        RestoreStuckColor();
    }

    private void StopVisualMotion()
    {
        if (visualMotionRoutine == null)
        {
            return;
        }

        StopCoroutine(visualMotionRoutine);
        visualMotionRoutine = null;
    }

    private void ApplyStuckColor()
    {
        if (!PrepareStuckColorTarget())
        {
            return;
        }

        StartStuckColorFade(stuckColor);
    }

    private void RestoreStuckColor()
    {
        if (!hasCachedOriginalStuckColor)
        {
            StopStuckColorFade();
            return;
        }

        StartStuckColorFade(originalStuckColor);
    }

    private bool PrepareStuckColorTarget()
    {
        ResolveStuckColorRenderer();
        if (stuckColorRenderer == null)
        {
            WarnMissingStuckColorTarget();
            return false;
        }

        stuckSpriteRenderer = stuckColorRenderer as SpriteRenderer;
        if (stuckSpriteRenderer != null)
        {
            if (!hasCachedOriginalStuckColor)
            {
                originalStuckColor = stuckSpriteRenderer.color;
                currentStuckColor = originalStuckColor;
                hasCachedOriginalStuckColor = true;
            }

            hasStuckColorTarget = true;
            return true;
        }

        if (stuckRuntimeMaterial == null)
        {
            stuckSourceMaterial = stuckColorRenderer.sharedMaterial;
            if (stuckSourceMaterial == null)
            {
                WarnMissingStuckColorTarget();
                return false;
            }

            if (!TryCacheColorProperty(stuckSourceMaterial, out stuckColorPropertyId))
            {
                WarnMissingStuckColorProperty();
                return false;
            }

            stuckRuntimeMaterial = new Material(stuckSourceMaterial);
            stuckColorRenderer.material = stuckRuntimeMaterial;
        }
        else if (!TryCacheColorProperty(stuckRuntimeMaterial, out stuckColorPropertyId))
        {
            WarnMissingStuckColorProperty();
            return false;
        }

        if (!hasCachedOriginalStuckColor)
        {
            originalStuckColor = stuckRuntimeMaterial.GetColor(stuckColorPropertyId);
            currentStuckColor = originalStuckColor;
            hasCachedOriginalStuckColor = true;
        }

        hasStuckColorTarget = true;
        return true;
    }

    private void ResolveStuckColorRenderer()
    {
        if (stuckColorRenderer != null || characterVisualRoot == null)
        {
            return;
        }

        stuckColorRenderer = characterVisualRoot.GetComponentInChildren<Renderer>(true);
    }

    private static bool TryCacheColorProperty(Material material, out int propertyId)
    {
        if (material != null && material.HasProperty(BaseColorId))
        {
            propertyId = BaseColorId;
            return true;
        }

        if (material != null && material.HasProperty(ColorId))
        {
            propertyId = ColorId;
            return true;
        }

        if (material != null && material.HasProperty(TintColorId))
        {
            propertyId = TintColorId;
            return true;
        }

        propertyId = 0;
        return false;
    }

    private void StartStuckColorFade(Color targetColor)
    {
        StopStuckColorFade();

        if (!hasStuckColorTarget)
        {
            return;
        }

        if (colorFadeDuration <= 0f)
        {
            SetStuckColor(targetColor);
            return;
        }

        stuckColorRoutine = StartCoroutine(
            FadeStuckColorRoutine(currentStuckColor, targetColor, colorFadeDuration));
    }

    private IEnumerator FadeStuckColorRoutine(Color from, Color to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            SetStuckColor(Color.Lerp(from, to, progress));
            yield return null;
        }

        SetStuckColor(to);
        stuckColorRoutine = null;
    }

    private void SetStuckColor(Color color)
    {
        currentStuckColor = color;
        if (stuckSpriteRenderer != null)
        {
            stuckSpriteRenderer.color = color;
            return;
        }

        if (stuckRuntimeMaterial != null)
        {
            stuckRuntimeMaterial.SetColor(stuckColorPropertyId, color);
        }
    }

    private void StopStuckColorFade()
    {
        if (stuckColorRoutine == null)
        {
            return;
        }

        StopCoroutine(stuckColorRoutine);
        stuckColorRoutine = null;
    }

    private void RestoreStuckColorImmediately()
    {
        StopStuckColorFade();
        if (hasCachedOriginalStuckColor)
        {
            SetStuckColor(originalStuckColor);
        }
    }

    private void ReleaseStuckColorMaterial()
    {
        StopStuckColorFade();

        RestoreStuckColorImmediately();

        if (stuckRuntimeMaterial != null)
        {
            if (stuckColorRenderer != null &&
                stuckColorRenderer.sharedMaterial == stuckRuntimeMaterial)
            {
                stuckColorRenderer.sharedMaterial = stuckSourceMaterial;
            }

            Destroy(stuckRuntimeMaterial);
            stuckRuntimeMaterial = null;
            stuckSourceMaterial = null;
        }

        hasStuckColorTarget = false;
        hasCachedOriginalStuckColor = false;
        stuckSpriteRenderer = null;
    }

    private void RestoreVisualImmediately()
    {
        if (HasValidVisualRoot() && visualRestPositionCached)
        {
            characterVisualRoot.localPosition = visualRestLocalPosition;
        }
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

    private void ResolveAbility()
    {
        ResolvePlayer();
        if (player == null)
        {
            return;
        }

        ResolveStatusManager();
        trappedAbility = statusManager != null ? statusManager.Ability : null;
    }

    private void WarnMissingStuckColorTarget()
    {
        if (warnedMissingStuckColorTarget)
        {
            return;
        }

        Debug.LogWarning(
            "[PlayerSunkenPlatformStatus] Assign a stuck colour renderer or a character visual root with a child Renderer.",
            this);
        warnedMissingStuckColorTarget = true;
    }

    private void WarnMissingStuckColorProperty()
    {
        if (warnedMissingStuckColorProperty)
        {
            return;
        }

        Debug.LogWarning(
            "[PlayerSunkenPlatformStatus] Stuck colour renderer material needs _BaseColor, _Color, or _TintColor.",
            this);
        warnedMissingStuckColorProperty = true;
    }

    private void WarnInvalidVisualRoot()
    {
        if (warnedInvalidVisualRoot)
        {
            return;
        }

        Debug.LogWarning(
            "[PlayerSunkenPlatformStatus] Assign a visual-only child Transform. Do not assign the Player/physics root.",
            this);
        warnedInvalidVisualRoot = true;
    }
}
