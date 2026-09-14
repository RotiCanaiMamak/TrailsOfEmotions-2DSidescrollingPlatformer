using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerCrackedTrapStatus : MonoBehaviour, IManualStatusEscapeSource
{
    [Header("Character Visual Sink")]
    [Tooltip("Visual-only child that contains the character artwork. Never assign the Player/physics root.")]
    [SerializeField] private Transform characterVisualRoot;
    [Tooltip("Local distance the character artwork moves into the terrain.")]
    [Min(0f)] [SerializeField] private float sinkDistance = 1f;
    [Tooltip("Seconds used to animate the character artwork into the cracked ground when trapped.")]
    [Min(0f)] [SerializeField] private float initialSinkDuration = 0.25f;
    [Tooltip("Seconds used to return the character artwork to its original position when released.")]
    [Min(0f)] [SerializeField] private float releaseDuration = 0.15f;
    [SerializeField] private AnimationCurve sinkMotionCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Escape Jitter")]
    [Min(0f)] [SerializeField] private float escapeJitterAmplitude = 0.05f;
    [Min(0f)] [SerializeField] private float escapeJitterFrequency = 35f;
    [Tooltip("Seconds each jump tap shakes the stuck character visual.")]
    [Min(0f)] [SerializeField] private float tapShakeDuration = 0.08f;

    private PlayerController player;
    private PlayerStatusManager statusManager;
    private CrackedTrapObstacle activeTrap;
    private Coroutine visualMotionRoutine;
    private Vector3 visualRestLocalPosition;
    private float requiredProgress;
    private float tapProgressAmount;
    private float decayPerSecond;
    private float escapeProgress;
    private float escapeJitterSeed;
    private float tapShakeElapsed;
    private bool isTrapped;
    private bool isTapShaking;
    private bool visualRestPositionCached;

    public bool IsTrapped => isTrapped;
    public float EscapeProgress01 => requiredProgress <= 0f
        ? (isTrapped ? 1f : 0f)
        : Mathf.Clamp01(escapeProgress / requiredProgress);
    public bool IsManualEscapeActive => isTrapped;
    public bool IsManualEscapeVisible => isTrapped;
    public float ManualEscapeProgress01 => EscapeProgress01;

    private void Awake()
    {
        ResolvePlayer();
        ResolveCharacterVisualRoot();
        CacheVisualRestPosition();
        RestoreVisualImmediately();
    }

    private void OnValidate()
    {
        sinkDistance = Mathf.Max(0f, sinkDistance);
        initialSinkDuration = Mathf.Max(0f, initialSinkDuration);
        releaseDuration = Mathf.Max(0f, releaseDuration);
        escapeJitterAmplitude = Mathf.Max(0f, escapeJitterAmplitude);
        escapeJitterFrequency = Mathf.Max(0f, escapeJitterFrequency);
        tapShakeDuration = Mathf.Max(0f, tapShakeDuration);
    }

    private void Update()
    {
        if (!isTrapped)
        {
            return;
        }

        if (activeTrap == null)
        {
            ClearTrapGameplayState();
            return;
        }

        UpdateEscapeProgress();
        if (isTrapped)
        {
            UpdateTapShake();
        }
    }

    private void OnDisable()
    {
        ClearTrapAndNotifyObstacle();
    }

    private void OnDestroy()
    {
        ClearTrapAndNotifyObstacle();
    }

    public bool TryTrap(
        CrackedTrapObstacle source,
        float requiredEscapeProgress,
        float progressPerTap,
        float progressDecayPerSecond)
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

        activeTrap = source;
        requiredProgress = Mathf.Max(0f, requiredEscapeProgress);
        tapProgressAmount = Mathf.Max(0f, progressPerTap);
        decayPerSecond = Mathf.Max(0f, progressDecayPerSecond);
        escapeProgress = 0f;
        escapeJitterSeed = Random.value * 1000f;
        tapShakeElapsed = 0f;
        isTrapped = true;
        isTapShaking = false;

        ResolveCharacterVisualRoot();
        CacheVisualRestPosition();
        player.SetMovementRooted(this, true);
        BeginSinkVisual();
        return true;
    }

    public void ReleaseFrom(CrackedTrapObstacle source)
    {
        if (!isTrapped || source == null || activeTrap != source)
        {
            return;
        }

        ClearTrapGameplayState();
    }

    private void UpdateEscapeProgress()
    {
        if (requiredProgress <= 0f)
        {
            CompleteEscape();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            escapeProgress = Mathf.Min(
                requiredProgress,
                escapeProgress + tapProgressAmount);
            BeginTapShake();

            if (escapeProgress >= requiredProgress)
            {
                CompleteEscape();
                return;
            }
        }

        if (escapeProgress > 0f && decayPerSecond > 0f)
        {
            escapeProgress = Mathf.Max(
                0f,
                escapeProgress - decayPerSecond * Time.deltaTime);
        }
    }

    private void CompleteEscape()
    {
        if (!isTrapped)
        {
            return;
        }

        CrackedTrapObstacle releasedTrap = activeTrap;
        ClearTrapGameplayState(true);
        releasedTrap?.NotifyTrapReleased(this);
    }

    private void ClearTrapAndNotifyObstacle(bool animateVisual = false)
    {
        if (!isTrapped)
        {
            StopVisualMotion();
            RestoreVisualImmediately();
            return;
        }

        CrackedTrapObstacle releasedTrap = activeTrap;
        ClearTrapGameplayState(animateVisual);
        releasedTrap?.NotifyTrapReleased(this);
    }

    private void ClearTrapGameplayState(bool animateVisual = true)
    {
        if (player != null)
        {
            player.SetMovementRooted(this, false);
        }

        activeTrap = null;
        requiredProgress = 0f;
        tapProgressAmount = 0f;
        decayPerSecond = 0f;
        escapeProgress = 0f;
        tapShakeElapsed = 0f;
        isTrapped = false;
        isTapShaking = false;
        ReleaseSinkVisual(animateVisual);
    }

    private void BeginSinkVisual()
    {
        if (!PrepareVisualRoot())
        {
            return;
        }

        StartVisualMotion(
            visualRestLocalPosition,
            GetSunkVisualPosition(),
            initialSinkDuration,
            null);
    }

    private void ReleaseSinkVisual(bool animate)
    {
        if (!HasValidVisualRoot() || !visualRestPositionCached)
        {
            StopVisualMotion();
            return;
        }

        if (animate && isActiveAndEnabled && releaseDuration > 0f)
        {
            StartVisualMotion(
                characterVisualRoot.localPosition,
                visualRestLocalPosition,
                releaseDuration,
                null);
            return;
        }

        StopVisualMotion();
        RestoreVisualImmediately();
    }

    private bool PrepareVisualRoot()
    {
        if (!HasValidVisualRoot())
        {
            return false;
        }

        CacheVisualRestPosition();
        characterVisualRoot.localPosition = visualRestLocalPosition;
        return true;
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

    private void StopVisualMotion()
    {
        if (visualMotionRoutine == null)
        {
            return;
        }

        StopCoroutine(visualMotionRoutine);
        visualMotionRoutine = null;
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

    private void BeginTapShake()
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

        StopVisualMotion();
        tapShakeElapsed = 0f;
        isTapShaking = true;
        ApplySunkJitter();
    }

    private void UpdateTapShake()
    {
        if (!isTapShaking)
        {
            return;
        }

        if (!HasValidVisualRoot() || !visualRestPositionCached)
        {
            isTapShaking = false;
            return;
        }

        if (tapShakeDuration <= 0f)
        {
            isTapShaking = false;
            SetSunkVisualImmediately();
            return;
        }

        tapShakeElapsed += Time.deltaTime;
        if (tapShakeElapsed >= tapShakeDuration)
        {
            isTapShaking = false;
            SetSunkVisualImmediately();
            return;
        }

        ApplySunkJitter();
    }

    private void ApplySunkJitter()
    {
        characterVisualRoot.localPosition =
            GetSunkVisualPosition() + GetEscapeJitterOffset();
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

    private void RestoreVisualImmediately()
    {
        if (HasValidVisualRoot() && visualRestPositionCached)
        {
            characterVisualRoot.localPosition = visualRestLocalPosition;
        }
    }

    private void SetSunkVisualImmediately()
    {
        if (HasValidVisualRoot() && visualRestPositionCached)
        {
            characterVisualRoot.localPosition = GetSunkVisualPosition();
        }
    }

}
