using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class StartGameTextAnimator : MonoBehaviour
{
    [Header("Text")]
    [SerializeField] private TMP_Text targetText;
    [SerializeField] private bool setTextOnStart = true;
    [SerializeField] private string startGameText = "Start Game";

    [Header("Pulse")]
    [Min(1f)] [SerializeField] private float scaleMultiplier = 1.12f;
    [Min(0.01f)] [SerializeField] private float cycleDuration = 0.8f;
    [SerializeField] private Color flashColor = Color.white;

    private Vector3 originalLocalScale;
    private Color originalColor;
    private float elapsed;
    private bool hasOriginalState;
    private bool warnedMissingText;

    private void Awake()
    {
        ResolveText();
    }

    private void OnEnable()
    {
        ResolveText();
        CacheOriginalState();
        elapsed = 0f;

        if (setTextOnStart && targetText != null)
        {
            targetText.text = startGameText;
        }
    }

    private void Update()
    {
        if (!TryGetText(out TMP_Text text))
        {
            return;
        }

        if (!hasOriginalState)
        {
            CacheOriginalState();
        }

        elapsed += Time.deltaTime;

        float cycleT = Mathf.Repeat(elapsed / cycleDuration, 1f);
        float pulse = Mathf.Sin(cycleT * Mathf.PI);
        float easedPulse = Mathf.SmoothStep(0f, 1f, pulse);

        transform.localScale = Vector3.Lerp(
            originalLocalScale,
            originalLocalScale * scaleMultiplier,
            easedPulse);
        text.color = Color.Lerp(originalColor, flashColor, easedPulse);
    }

    private void OnDisable()
    {
        RestoreOriginalState();
    }

    private void OnDestroy()
    {
        RestoreOriginalState();
    }

    private void OnValidate()
    {
        scaleMultiplier = Mathf.Max(1f, scaleMultiplier);
        cycleDuration = Mathf.Max(0.01f, cycleDuration);

        if (string.IsNullOrEmpty(startGameText))
        {
            startGameText = "Start Game";
        }
    }

    private bool TryGetText(out TMP_Text text)
    {
        ResolveText();
        text = targetText;
        if (text != null)
        {
            return true;
        }

        WarnMissingText();
        return false;
    }

    private void ResolveText()
    {
        if (targetText == null)
        {
            targetText = GetComponent<TMP_Text>();
        }
    }

    private void CacheOriginalState()
    {
        if (targetText == null)
        {
            return;
        }

        originalLocalScale = transform.localScale;
        originalColor = targetText.color;
        hasOriginalState = true;
    }

    private void RestoreOriginalState()
    {
        if (!hasOriginalState)
        {
            return;
        }

        transform.localScale = originalLocalScale;

        if (targetText != null)
        {
            targetText.color = originalColor;
        }
    }

    private void WarnMissingText()
    {
        if (warnedMissingText)
        {
            return;
        }

        Debug.LogWarning(
            "[StartGameTextAnimator] Assign a TMP_Text component for the Start Game text.",
            this);
        warnedMissingText = true;
    }
}
