using System.Collections.Generic;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    private const string PlayerTag = "Player";

    [Header("Offset")]
    public float offsetX = 3f;
    public float offsetY = 1f;

    [Header("Zoom")]
    [Min(0.1f)]
    public float orthographicSize = 16f;

    [Header("Smoothing")]
    public float smoothSpeed = 8f;

    [Header("Shake")]
    [Min(0f)]
    [SerializeField] private float shakeDuration = 0.18f;
    [Min(0f)]
    [SerializeField] private float shakeAmplitude = 0.35f;
    [Min(0f)]
    [SerializeField] private float shakeFrequency = 38f;

    [Header("Anxiety Tremble")]
    [SerializeField] private string anxietyBiomeNameOrFamily = "Anxiety";
    [SerializeField] private AudioClip anxietyTrembleClip;
    [Range(0f, 1f)] [SerializeField] private float anxietyTrembleVolumeScale = 1f;
    [Min(0f)] [SerializeField] private float anxietyTrembleMinInterval = 7f;
    [Min(0f)] [SerializeField] private float anxietyTrembleMaxInterval = 14f;
    [Min(0f)] [SerializeField] private float anxietyTrembleDuration = 0.14f;
    [Min(0f)] [SerializeField] private float anxietyTrembleAmplitude = 0.08f;

    private Camera cam;
    private Transform target;
    private Vector3 targetPos;
    private Vector3 shakeOffset;
    private float activeShakeDuration;
    private float activeShakeAmplitude;
    private float shakeTimer;
    private float continuousShakeTime;
    private BiomeManager subscribedBiomeManager;
    private BiomeData activeBiome;
    private float anxietyTrembleTimer = -1f;
    private readonly Dictionary<Object, float> continuousShakeAmplitudes = new Dictionary<Object, float>();
    private readonly List<Object> continuousShakeKeys = new List<Object>();
    private readonly HashSet<Object> followLockSources = new HashSet<Object>();

    void Awake()
    {
        cam = GetComponent<Camera>();
        ResolveTargetReference();
    }

    private void OnEnable()
    {
        TrySubscribeToBiomeManager();
    }

    private void OnDisable()
    {
        UnsubscribeFromBiomeManager();
        activeBiome = null;
        anxietyTrembleTimer = -1f;
        continuousShakeAmplitudes.Clear();
        continuousShakeKeys.Clear();
        continuousShakeTime = 0f;
    }

    private void OnValidate()
    {
        orthographicSize = Mathf.Max(0.1f, orthographicSize);
        shakeDuration = Mathf.Max(0f, shakeDuration);
        shakeAmplitude = Mathf.Max(0f, shakeAmplitude);
        shakeFrequency = Mathf.Max(0f, shakeFrequency);
        anxietyTrembleVolumeScale = Mathf.Clamp01(anxietyTrembleVolumeScale);
        anxietyTrembleMinInterval = Mathf.Max(0f, anxietyTrembleMinInterval);
        anxietyTrembleMaxInterval = Mathf.Max(anxietyTrembleMinInterval, anxietyTrembleMaxInterval);
        anxietyTrembleDuration = Mathf.Max(0f, anxietyTrembleDuration);
        anxietyTrembleAmplitude = Mathf.Max(0f, anxietyTrembleAmplitude);
    }

    void LateUpdate()
    {
        TrySubscribeToBiomeManager();

        if (IsFollowLocked())
        {
            return;
        }

        ResolveTargetReference();
        if (target == null)
        {
            return;
        }

        targetPos = GetTargetPosition(target);

        Vector3 basePosition = transform.position - shakeOffset;
        basePosition = Vector3.Lerp(basePosition, targetPos, smoothSpeed * Time.deltaTime);
        shakeOffset = GetShakeOffset();
        transform.position = basePosition + shakeOffset;

        UpdateAnxietyTremble();
        ApplyOrthographicSize();
    }

    public void SnapToTarget(Transform snapTarget = null)
    {
        Transform resolvedTarget = snapTarget != null ? snapTarget : target;
        if (resolvedTarget == null)
        {
            ResolveTargetReference();
            resolvedTarget = target;
        }

        if (resolvedTarget == null)
        {
            return;
        }

        targetPos = GetTargetPosition(resolvedTarget);
        ResetShake();
        transform.position = targetPos;
        ApplyOrthographicSize();
    }

    public void OnWorldShift(Vector3 shiftAmount)
    {
        transform.position -= shiftAmount;
        targetPos -= shiftAmount;
    }

    public void SetFollowLocked(Object source, bool locked)
    {
        if (ReferenceEquals(source, null))
        {
            return;
        }

        bool wasLocked = IsFollowLocked();
        if (locked)
        {
            followLockSources.Add(source);
        }
        else
        {
            followLockSources.Remove(source);
        }

        if (!wasLocked && IsFollowLocked())
        {
            ResetShake();
        }
    }

    public void Shake(float amplitude = -1f, float duration = -1f)
    {
        activeShakeAmplitude = amplitude >= 0f ? amplitude : shakeAmplitude;
        activeShakeDuration = duration >= 0f ? duration : shakeDuration;

        if (activeShakeAmplitude <= 0f || activeShakeDuration <= 0f)
        {
            ResetShake();
            return;
        }

        shakeTimer = activeShakeDuration;
    }

    public void SetContinuousShake(Object source, float amplitude)
    {
        if (ReferenceEquals(source, null))
        {
            return;
        }

        float clampedAmplitude = Mathf.Max(0f, amplitude);
        if (clampedAmplitude <= 0f)
        {
            RemoveContinuousShake(source);
            return;
        }

        continuousShakeAmplitudes[source] = clampedAmplitude;
    }

    public void RemoveContinuousShake(Object source)
    {
        if (ReferenceEquals(source, null))
        {
            return;
        }

        continuousShakeAmplitudes.Remove(source);
        if (continuousShakeAmplitudes.Count == 0)
        {
            continuousShakeTime = 0f;
        }
    }

    public void SetOrthographicSize(float size)
    {
        orthographicSize = Mathf.Max(0.1f, size);

        if (cam != null && cam.orthographic)
        {
            cam.orthographicSize = orthographicSize;
        }
    }

    private Vector3 GetTargetPosition(Transform followTarget)
    {
        return new Vector3(followTarget.position.x + offsetX, followTarget.position.y + offsetY, transform.position.z);
    }

    private void TrySubscribeToBiomeManager()
    {
        if (subscribedBiomeManager == BiomeManager.Instance)
        {
            return;
        }

        UnsubscribeFromBiomeManager();

        subscribedBiomeManager = BiomeManager.Instance;
        if (subscribedBiomeManager == null)
        {
            activeBiome = null;
            anxietyTrembleTimer = -1f;
            return;
        }

        subscribedBiomeManager.EnsureInitialized();
        activeBiome = subscribedBiomeManager.CurrentBiome;
        subscribedBiomeManager.onBiomeChanged.AddListener(OnBiomeChanged);
        RefreshAnxietyTrembleTimer();
    }

    private void UnsubscribeFromBiomeManager()
    {
        if (subscribedBiomeManager != null)
        {
            subscribedBiomeManager.onBiomeChanged.RemoveListener(OnBiomeChanged);
        }

        subscribedBiomeManager = null;
    }

    private void OnBiomeChanged(BiomeData biome)
    {
        activeBiome = biome;
        RefreshAnxietyTrembleTimer();
    }

    private void UpdateAnxietyTremble()
    {
        if (!IsAnxietyBiome(activeBiome))
        {
            anxietyTrembleTimer = -1f;
            return;
        }

        if (anxietyTrembleTimer < 0f)
        {
            ScheduleNextAnxietyTremble();
            return;
        }

        anxietyTrembleTimer -= Time.deltaTime;
        if (anxietyTrembleTimer > 0f)
        {
            return;
        }

        Shake(anxietyTrembleAmplitude, anxietyTrembleDuration);
        AudioManager.Instance?.PlaySfxOneShot(anxietyTrembleClip, anxietyTrembleVolumeScale);
        ScheduleNextAnxietyTremble();
    }

    private void RefreshAnxietyTrembleTimer()
    {
        if (IsAnxietyBiome(activeBiome))
        {
            ScheduleNextAnxietyTremble();
        }
        else
        {
            anxietyTrembleTimer = -1f;
        }
    }

    private void ScheduleNextAnxietyTremble()
    {
        anxietyTrembleTimer = Random.Range(anxietyTrembleMinInterval, Mathf.Max(anxietyTrembleMinInterval, anxietyTrembleMaxInterval));
    }

    private bool IsAnxietyBiome(BiomeData biome)
    {
        if (biome == null)
        {
            return false;
        }

        return MatchesName(biome.familyId, anxietyBiomeNameOrFamily) ||
            MatchesName(biome.biomeName, anxietyBiomeNameOrFamily);
    }

    private void ResolveTargetReference()
    {
        if (target == null)
        {
            GameObject playerObject = FindPlayerObject();
            if (playerObject == null)
            {
                return;
            }

            PlayerController playerController = playerObject.GetComponent<PlayerController>();
            if (playerController == null)
            {
                playerController = playerObject.GetComponentInParent<PlayerController>();
            }
            if (playerController == null)
            {
                playerController = playerObject.GetComponentInChildren<PlayerController>();
            }

            target = playerController != null ? playerController.transform : playerObject.transform;
        }
    }

    private GameObject FindPlayerObject()
    {
        try
        {
            return GameObject.FindGameObjectWithTag(PlayerTag);
        }
        catch (UnityException)
        {
            Debug.LogWarning($"[CameraFollow] No Unity tag named '{PlayerTag}' exists.", this);
            return null;
        }
    }

    private Vector3 GetShakeOffset()
    {
        return GetImpulseShakeOffset() + GetContinuousShakeOffset();
    }

    private Vector3 GetImpulseShakeOffset()
    {
        if (shakeTimer <= 0f)
        {
            shakeTimer = 0f;
            return Vector3.zero;
        }

        float elapsed = activeShakeDuration - shakeTimer;
        float angle = elapsed * shakeFrequency * Mathf.PI * 2f;
        float falloff = activeShakeDuration > 0f ? shakeTimer / activeShakeDuration : 0f;
        float amplitude = activeShakeAmplitude * falloff * falloff;

        shakeTimer = Mathf.Max(0f, shakeTimer - Time.deltaTime);

        return new Vector3(
            Mathf.Sin(angle),
            Mathf.Cos(angle * 1.37f),
            0f) * amplitude;
    }

    private Vector3 GetContinuousShakeOffset()
    {
        float amplitude = GetContinuousShakeAmplitude();
        if (amplitude <= 0f)
        {
            return Vector3.zero;
        }

        continuousShakeTime += Time.deltaTime;
        float angle = continuousShakeTime * shakeFrequency * Mathf.PI * 2f;
        return new Vector3(
            Mathf.Sin(angle * 0.83f),
            Mathf.Cos(angle * 1.19f),
            0f) * amplitude;
    }

    private float GetContinuousShakeAmplitude()
    {
        if (continuousShakeAmplitudes.Count == 0)
        {
            return 0f;
        }

        PruneContinuousShakeSources();

        float amplitude = 0f;
        foreach (float value in continuousShakeAmplitudes.Values)
        {
            amplitude = Mathf.Max(amplitude, value);
        }

        return amplitude;
    }

    private void PruneContinuousShakeSources()
    {
        continuousShakeKeys.Clear();
        foreach (KeyValuePair<Object, float> entry in continuousShakeAmplitudes)
        {
            if (entry.Key == null || entry.Value <= 0f)
            {
                continuousShakeKeys.Add(entry.Key);
            }
        }

        for (int i = 0; i < continuousShakeKeys.Count; i++)
        {
            continuousShakeAmplitudes.Remove(continuousShakeKeys[i]);
        }

        continuousShakeKeys.Clear();
        if (continuousShakeAmplitudes.Count == 0)
        {
            continuousShakeTime = 0f;
        }
    }

    private void ResetShake()
    {
        shakeOffset = Vector3.zero;
        activeShakeDuration = 0f;
        activeShakeAmplitude = 0f;
        shakeTimer = 0f;
    }

    private bool IsFollowLocked()
    {
        followLockSources.RemoveWhere(source => source == null);
        return followLockSources.Count > 0;
    }

    private void ApplyOrthographicSize()
    {
        if (cam != null && cam.orthographic)
        {
            cam.orthographicSize = orthographicSize;
        }
    }

    private static bool MatchesName(string value, string expected)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            !string.IsNullOrWhiteSpace(expected) &&
            string.Equals(value.Trim(), expected.Trim(), System.StringComparison.OrdinalIgnoreCase);
    }
}
