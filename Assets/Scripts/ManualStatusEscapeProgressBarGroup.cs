using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ManualStatusEscapeProgressBarGroup : MonoBehaviour
{
    [Serializable]
    private sealed class EscapeBar
    {
        [Tooltip("Progress bar root shown while this slot has an active manual status.")]
        [SerializeField] private GameObject root;
        [Tooltip("Horizontal Filled Image used to display manual status removal progress.")]
        [SerializeField] private Image fill;
        [Tooltip("Optional CanvasGroup used to fade this bar in when assigned a status.")]
        [SerializeField] private CanvasGroup canvasGroup;

        private IManualStatusEscapeSource assignedSource;
        private bool warnedMissingRoot;
        private bool warnedMissingFill;

        public IManualStatusEscapeSource AssignedSource => assignedSource;
        public bool HasRoot => root != null;

        public void Assign(
            IManualStatusEscapeSource source,
            ManualStatusEscapeProgressBarGroup owner,
            float fadeInDuration,
            Vector2 stackPosition)
        {
            SetStackPosition(stackPosition);

            if (assignedSource != source)
            {
                assignedSource = source;
                Show(owner, fadeInDuration);
            }

            SetProgress(source.ManualEscapeProgress01, owner);
        }

        public void Clear(ManualStatusEscapeProgressBarGroup owner)
        {
            assignedSource = null;
            SetProgress(0f, owner);
            Hide(owner);
        }

        private void Show(
            ManualStatusEscapeProgressBarGroup owner,
            float fadeInDuration)
        {
            if (root == null)
            {
                WarnMissingRoot(owner);
                return;
            }

            if (root != owner.gameObject && !root.activeSelf)
            {
                root.SetActive(true);
            }

            if (canvasGroup == null)
            {
                return;
            }

            if (fadeInDuration <= 0f)
            {
                canvasGroup.alpha = 1f;
            }
            else
            {
                canvasGroup.alpha = 0f;
            }
        }

        private void Hide(ManualStatusEscapeProgressBarGroup owner)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }

            if (root != null &&
                !ReferenceEquals(root, null) &&
                root != owner.gameObject &&
                root.activeSelf)
            {
                root.SetActive(false);
            }
        }

        public void UpdateFade(float fadeProgress)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Clamp01(fadeProgress);
            }
        }

        public bool TryGetStackPosition(out Vector2 stackPosition)
        {
            stackPosition = Vector2.zero;
            if (root == null)
            {
                return false;
            }

            RectTransform rectTransform = root.transform as RectTransform;
            if (rectTransform != null)
            {
                stackPosition = rectTransform.anchoredPosition;
                return true;
            }

            Vector3 localPosition = root.transform.localPosition;
            stackPosition = new Vector2(localPosition.x, localPosition.y);
            return true;
        }

        private void SetStackPosition(Vector2 stackPosition)
        {
            if (root == null)
            {
                return;
            }

            RectTransform rectTransform = root.transform as RectTransform;
            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = stackPosition;
                return;
            }

            Transform rootTransform = root.transform;
            Vector3 localPosition = rootTransform.localPosition;
            rootTransform.localPosition = new Vector3(
                stackPosition.x,
                stackPosition.y,
                localPosition.z);
        }

        private void SetProgress(
            float progress,
            ManualStatusEscapeProgressBarGroup owner)
        {
            if (fill == null)
            {
                WarnMissingFill(owner);
                return;
            }

            fill.fillAmount = Mathf.Clamp01(progress);
        }

        private void WarnMissingRoot(ManualStatusEscapeProgressBarGroup owner)
        {
            if (warnedMissingRoot)
            {
                return;
            }

            Debug.LogWarning(
                "[ManualStatusEscapeProgressBarGroup] Assign a root GameObject for every escape bar slot.",
                owner);
            warnedMissingRoot = true;
        }

        private void WarnMissingFill(ManualStatusEscapeProgressBarGroup owner)
        {
            if (warnedMissingFill)
            {
                return;
            }

            Debug.LogWarning(
                "[ManualStatusEscapeProgressBarGroup] Assign a horizontal Filled Image for every escape bar slot.",
                owner);
            warnedMissingFill = true;
        }
    }

    [Header("Sources")]
    [Tooltip("Optional manual status components in display order. Leave empty to auto-resolve from Source Root.")]
    [SerializeField] private MonoBehaviour[] escapeSources;
    [Tooltip("Player or PlayerStatusManager GameObject used to auto-resolve manual status sources when Escape Sources is empty.")]
    [SerializeField] private GameObject sourceRoot;

    [Header("Bars")]
    [Tooltip("Progress bar visuals in top-to-bottom display order.")]
    [SerializeField] private EscapeBar[] escapeBars;
    [Tooltip("Position offset applied upward for each extra active status bar.")]
    [SerializeField] private Vector2 stackOffset = new Vector2(0f, 24f);
    [Tooltip("Seconds used to fade in a progress bar when a status begins using that slot.")]
    [Min(0f)] [SerializeField] private float fadeInDuration = 0.1f;

    private float[] fadeElapsedByBar;
    private readonly IManualStatusEscapeSource[] autoEscapeSources =
        new IManualStatusEscapeSource[6];
    private Vector2 stackOrigin;
    private bool warnedOverflow;
    private bool warnedInvalidSource;
    private bool warnedMissingStackOrigin;

    private void Awake()
    {
        EnsureFadeState();
        ResolveAutoEscapeSources();
        CacheStackOrigin();
        HideAllBars();
    }

    private void OnValidate()
    {
        fadeInDuration = Mathf.Max(0f, fadeInDuration);
    }

    private void Update()
    {
        EnsureFadeState();
        ResolveAutoEscapeSources();
        CacheStackOrigin();

        int barIndex = 0;
        int stackSlotIndex = 0;
        int activeVisibleSourceCount = 0;
        int barCount = escapeBars != null ? escapeBars.Length : 0;

        for (int i = 0; i < GetSourceCount(); i++)
        {
            if (!TryGetActiveVisibleSource(GetSourceAt(i), out IManualStatusEscapeSource source))
            {
                continue;
            }

            activeVisibleSourceCount++;
            while (barIndex < barCount && !HasUsableBar(escapeBars[barIndex]))
            {
                fadeElapsedByBar[barIndex] = 0f;
                barIndex++;
            }

            if (barIndex >= barCount)
            {
                continue;
            }

            EscapeBar bar = escapeBars[barIndex];
            if (bar.AssignedSource != source)
            {
                fadeElapsedByBar[barIndex] = 0f;
            }

            bar.Assign(
                source,
                this,
                fadeInDuration,
                stackOrigin + stackOffset * stackSlotIndex);
            UpdateBarFade(bar, barIndex);
            barIndex++;
            stackSlotIndex++;
        }

        for (int i = barIndex; i < barCount; i++)
        {
            if (escapeBars[i] != null)
            {
                escapeBars[i].Clear(this);
            }

            fadeElapsedByBar[i] = 0f;
        }

        UpdateOverflowWarning(activeVisibleSourceCount);
    }

    private void OnDisable()
    {
        HideAllBars();
    }

    private bool TryGetActiveVisibleSource(
        IManualStatusEscapeSource candidate,
        out IManualStatusEscapeSource source)
    {
        source = null;
        if (candidate == null)
        {
            return false;
        }

        source = candidate;
        return source.IsManualEscapeActive && source.IsManualEscapeVisible;
    }

    private IManualStatusEscapeSource GetSourceAt(int index)
    {
        if (HasAssignedEscapeSources())
        {
            MonoBehaviour candidate = escapeSources[index];
            IManualStatusEscapeSource source =
                candidate as IManualStatusEscapeSource;
            if (candidate != null && source == null)
            {
                WarnInvalidSource(candidate);
            }

            return source;
        }

        return autoEscapeSources[index];
    }

    private int GetSourceCount()
    {
        if (HasAssignedEscapeSources())
        {
            return escapeSources.Length;
        }

        return autoEscapeSources.Length;
    }

    private bool HasAssignedEscapeSources()
    {
        if (escapeSources == null)
        {
            return false;
        }

        for (int i = 0; i < escapeSources.Length; i++)
        {
            if (escapeSources[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private void ResolveAutoEscapeSources()
    {
        if (HasAssignedEscapeSources())
        {
            return;
        }

        PlayerStatusManager statusManager = ResolveSourceStatusManager();
        autoEscapeSources[0] =
            statusManager != null ? statusManager.SunkenPlatformStatus : null;
        autoEscapeSources[1] =
            statusManager != null ? statusManager.BurnStatus : null;
        autoEscapeSources[2] =
            statusManager != null ? statusManager.SootStatus : null;
        autoEscapeSources[3] =
            statusManager != null ? statusManager.DriftingWispStatus : null;
        autoEscapeSources[4] =
            statusManager != null ? statusManager.SinkingShadowStatus : null;
        autoEscapeSources[5] =
            statusManager != null ? statusManager.CrackedTrapStatus : null;
    }

    private PlayerStatusManager ResolveSourceStatusManager()
    {
        GameObject resolvedSourceRoot = sourceRoot != null ? sourceRoot : gameObject;
        PlayerStatusManager statusManager =
            resolvedSourceRoot.GetComponent<PlayerStatusManager>();
        if (statusManager != null)
        {
            return statusManager;
        }

        PlayerController player = resolvedSourceRoot.GetComponent<PlayerController>();
        if (player != null)
        {
            return player.Statuses;
        }

        player = resolvedSourceRoot.GetComponentInParent<PlayerController>();
        return player != null ? player.Statuses : null;
    }

    private void UpdateBarFade(EscapeBar bar, int barIndex)
    {
        if (fadeInDuration <= 0f)
        {
            bar.UpdateFade(1f);
            return;
        }

        fadeElapsedByBar[barIndex] = Mathf.Min(
            fadeInDuration,
            fadeElapsedByBar[barIndex] + Time.deltaTime);
        bar.UpdateFade(fadeElapsedByBar[barIndex] / fadeInDuration);
    }

    private void EnsureFadeState()
    {
        int barCount = escapeBars != null ? escapeBars.Length : 0;
        if (fadeElapsedByBar != null && fadeElapsedByBar.Length == barCount)
        {
            return;
        }

        fadeElapsedByBar = new float[barCount];
    }

    private void HideAllBars()
    {
        EnsureFadeState();
        if (escapeBars == null)
        {
            return;
        }

        for (int i = 0; i < escapeBars.Length; i++)
        {
            if (escapeBars[i] != null)
            {
                escapeBars[i].Clear(this);
            }

            fadeElapsedByBar[i] = 0f;
        }
    }

    private void UpdateOverflowWarning(int activeVisibleSourceCount)
    {
        bool hasOverflow = activeVisibleSourceCount > GetAvailableBarCount();
        if (!hasOverflow)
        {
            warnedOverflow = false;
            return;
        }

        if (warnedOverflow)
        {
            return;
        }

        Debug.LogWarning(
            "[ManualStatusEscapeProgressBarGroup] More manual statuses are active than assigned progress bars. Add more escape bar slots.",
            this);
        warnedOverflow = true;
    }

    private int GetAvailableBarCount()
    {
        if (escapeBars == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < escapeBars.Length; i++)
        {
            if (HasUsableBar(escapeBars[i]))
            {
                count++;
            }
        }

        return count;
    }

    private bool HasUsableBar(EscapeBar bar)
    {
        return bar != null && bar.HasRoot;
    }

    private void CacheStackOrigin()
    {
        if (TryGetStackOrigin(out Vector2 currentStackOrigin))
        {
            stackOrigin = currentStackOrigin;
            return;
        }

        WarnMissingStackOrigin();
    }

    private bool TryGetStackOrigin(out Vector2 currentStackOrigin)
    {
        currentStackOrigin = Vector2.zero;
        if (escapeBars == null || escapeBars.Length == 0 || escapeBars[0] == null)
        {
            return false;
        }

        return escapeBars[0].TryGetStackPosition(out currentStackOrigin);
    }

    private void WarnInvalidSource(MonoBehaviour candidate)
    {
        if (warnedInvalidSource)
        {
            return;
        }

        Debug.LogWarning(
            $"[ManualStatusEscapeProgressBarGroup] {candidate.GetType().Name} must implement IManualStatusEscapeSource.",
            candidate);
        warnedInvalidSource = true;
    }

    private void WarnMissingStackOrigin()
    {
        if (warnedMissingStackOrigin)
        {
            return;
        }

        Debug.LogWarning(
            "[ManualStatusEscapeProgressBarGroup] Assign escapeBars[0] so the stacked progress bars have a base position.",
            this);
        warnedMissingStackOrigin = true;
    }
}
