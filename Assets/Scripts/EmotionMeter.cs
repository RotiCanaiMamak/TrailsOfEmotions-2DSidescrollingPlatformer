using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

public class EmotionMeter : MonoBehaviour
{
    private struct EmotionModifier
    {
        public readonly float bloatSpeedMultiplier;
        public readonly float emotionAcceptanceMultiplier;

        public EmotionModifier(float bloatSpeedMultiplier, float emotionAcceptanceMultiplier)
        {
            this.bloatSpeedMultiplier = Mathf.Max(0f, bloatSpeedMultiplier);
            this.emotionAcceptanceMultiplier = Mathf.Max(0f, emotionAcceptanceMultiplier);
        }
    }

    public static EmotionMeter Instance { get; private set; }

    [Header("Fill Rate")]
    public float fillRatePerSecond = 1f;
    public bool loopOnFull = false;

    [Header("Thresholds")]
    [Tooltip("Normal -> Pre")]
    [Range(1f, 97f)] public float preThreshold = 20f;
    [Tooltip("Pre -> Mid")]
    [Range(2f, 98f)] public float midThreshold = 40f;
    [Tooltip("Mid -> Peaked")]
    [Range(3f, 99f)] public float peakedThreshold = 70f;

    [Header("Events")]
    public UnityEvent<float> onValueChanged;
    public UnityEvent<int> onStateChanged;
    public UnityEvent<float> onObstacleEmotionAdded = new UnityEvent<float>();


    public float Value { get; private set; }
    public int StateIndex { get; private set; }
    public float BloatSpeedMultiplier { get; private set; } = 1f;
    public float EmotionAcceptanceMultiplier { get; private set; } = 1f;

    private readonly Dictionary<Object, EmotionModifier> emotionModifiers = new Dictionary<Object, EmotionModifier>();

    void Awake()
    {
        if (onObstacleEmotionAdded == null)
        {
            onObstacleEmotionAdded = new UnityEvent<float>();
        }

        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void Update()
    {
        // Only fill while the game is actively running
        if (GameManager.Instance == null || !GameManager.Instance.IsRunning)
        {
            return;
        }

        ApplyDelta(fillRatePerSecond * BloatSpeedMultiplier * Time.deltaTime, false);
    }


    public void AddEmotion(float amount) => ApplyDelta(amount, true);

    public void AddRawEmotion(float amount) => ApplyDelta(amount, false);

    public void AddObstacleEmotion(float amount)
    {
        float appliedDelta = ApplyDelta(amount, true);
        if (appliedDelta > 0f)
        {
            onObstacleEmotionAdded?.Invoke(appliedDelta);
        }
    }

    public void SetEmotionModifier(Object source, float bloatSpeedMultiplier, float emotionAcceptanceMultiplier)
    {
        if (source == null)
        {
            return;
        }

        emotionModifiers[source] = new EmotionModifier(bloatSpeedMultiplier, emotionAcceptanceMultiplier);
        RecalculateEmotionModifiers();
    }

    public void RemoveEmotionModifier(Object source)
    {
        if (source == null || !emotionModifiers.Remove(source))
        {
            return;
        }

        RecalculateEmotionModifiers();
    }

    public void SetValue(float value)
    {
        int prev = StateIndex;
        Value = Mathf.Clamp(value, 0f, 100f);
        onValueChanged?.Invoke(Value);
        RefreshState(prev);
    }

    public void ResetMeter() => SetValue(0f);


    private float ApplyDelta(float delta, bool applyEmotionAcceptance)
    {
        int prev = StateIndex;
        float previousValue = Value;
        if (applyEmotionAcceptance && delta < 0f)
        {
            delta *= EmotionAcceptanceMultiplier;
        }

        float newValue = Value + delta;

        if (loopOnFull && newValue >= 100f)
        {
            newValue -= 100f;
        }

        Value = Mathf.Clamp(newValue, 0f, 100f);
        onValueChanged?.Invoke(Value);
        RefreshState(prev);
        return Value - previousValue;
    }

    private void RecalculateEmotionModifiers()
    {
        BloatSpeedMultiplier = 1f;
        EmotionAcceptanceMultiplier = 1f;

        foreach (EmotionModifier modifier in emotionModifiers.Values)
        {
            BloatSpeedMultiplier *= modifier.bloatSpeedMultiplier;
            EmotionAcceptanceMultiplier *= modifier.emotionAcceptanceMultiplier;
        }
    }

    private void RefreshState(int previousState)
    {
        // Keep normal < pre < mid < peaked so thresholds never invert.
        float pre = preThreshold;
        float mid = Mathf.Max(midThreshold, pre + 0.01f);
        float peaked = Mathf.Max(peakedThreshold, mid + 0.01f);

        int newState;
        if (Value >= peaked)
        {
            newState = 3;
        }
        else if (Value >= mid)
        {
            newState = 2;
        }
        else if (Value >= pre)
        {
            newState = 1;
        }
        else
        {
            newState = 0;
        }
        StateIndex = newState;

        if (newState != previousState)
        {
            onStateChanged?.Invoke(newState);
            Debug.Log($"[EmotionMeter] {IndexToName(previousState)} -> " + $"{IndexToName(newState)}  (value = {Value:F1})");
        }
    }

    private static string IndexToName(int idx) => idx switch
    {
        1 => "Pre",
        2 => "Mid",
        3 => "Peaked",
        _ => "Normal",
    };
}
