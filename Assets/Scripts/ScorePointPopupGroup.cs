using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ScorePointPopupGroup : MonoBehaviour
{
    [Serializable]
    private sealed class PopupSlot
    {
        [Tooltip("Popup root GameObject shown while this slot is active.")]
        [SerializeField] private GameObject root;
        [Tooltip("Text component used to display the awarded points.")]
        [SerializeField] private TMP_Text text;
        [Tooltip("Optional CanvasGroup used to fade this popup out.")]
        [SerializeField] private CanvasGroup canvasGroup;

        private bool active;
        private float elapsed;
        private int sequence;
        private int stackIndex;
        private bool warnedMissingRoot;
        private bool warnedMissingText;

        public bool IsActive => active;
        public int Sequence => sequence;
        public int StackIndex => stackIndex;

        public void Initialize(ScorePointPopupGroup owner)
        {
            ResolveReferences();
            Hide(owner);
        }

        public void Show(
            ScorePointPopupGroup owner,
            int points,
            string pointFormat,
            Vector2 stackPosition,
            int sequence,
            int stackIndex)
        {
            ResolveReferences();
            if (root == null)
            {
                WarnMissingRoot(owner);
                return;
            }

            if (text == null)
            {
                WarnMissingText(owner);
                return;
            }

            this.sequence = sequence;
            this.stackIndex = stackIndex;
            elapsed = 0f;
            active = true;

            if (!root.activeSelf)
            {
                root.SetActive(true);
            }

            text.text = string.Format(pointFormat, points);
            SetStackPosition(stackPosition, stackIndex);
            SetAlpha(1f);
        }

        public bool Tick(ScorePointPopupGroup owner, float deltaTime, float showDuration, float fadeOutDuration)
        {
            if (!active)
            {
                return false;
            }

            elapsed += Mathf.Max(0f, deltaTime);
            if (elapsed <= showDuration)
            {
                SetAlpha(1f);
                return false;
            }

            float fadeElapsed = elapsed - showDuration;
            if (fadeOutDuration <= 0f || fadeElapsed >= fadeOutDuration)
            {
                Hide(owner);
                return true;
            }

            SetAlpha(1f - fadeElapsed / fadeOutDuration);
            return false;
        }

        public void SetStackPosition(Vector2 stackPosition, int stackIndex)
        {
            this.stackIndex = stackIndex;
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

        public void Hide(ScorePointPopupGroup owner)
        {
            active = false;
            elapsed = 0f;
            SetAlpha(0f);

            if (root != null && root.activeSelf && root != owner.gameObject)
            {
                root.SetActive(false);
            }
        }

        private void ResolveReferences()
        {
            if (root == null && text != null)
            {
                root = text.gameObject;
            }

            if (root == null)
            {
                return;
            }

            if (text == null)
            {
                text = root.GetComponent<TMP_Text>();
                if (text == null)
                {
                    text = root.GetComponentInChildren<TMP_Text>(true);
                }
            }

            if (canvasGroup == null)
            {
                canvasGroup = root.GetComponent<CanvasGroup>();
            }
        }

        private void SetAlpha(float alpha)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Clamp01(alpha);
                return;
            }

            if (text != null)
            {
                Color color = text.color;
                color.a = Mathf.Clamp01(alpha);
                text.color = color;
            }
        }

        private void WarnMissingRoot(ScorePointPopupGroup owner)
        {
            if (warnedMissingRoot)
            {
                return;
            }

            Debug.LogWarning("[ScorePointPopupGroup] Assign a root GameObject for every popup slot.", owner);
            warnedMissingRoot = true;
        }

        private void WarnMissingText(ScorePointPopupGroup owner)
        {
            if (warnedMissingText)
            {
                return;
            }

            Debug.LogWarning("[ScorePointPopupGroup] Assign a TMP_Text component for every popup slot.", owner);
            warnedMissingText = true;
        }
    }

    [Header("Popups")]
    [Tooltip("Popup visuals in reuse order. The first slot's starting position is used as the stack origin.")]
    [SerializeField] private PopupSlot[] popupSlots;
    [SerializeField] private string pointFormat = "+{0}";
    [SerializeField] private Vector2 stackOffset = new Vector2(0f, -28f);

    [Header("Timing")]
    [Min(0f)] [SerializeField] private float showDuration = 0.8f;
    [Min(0f)] [SerializeField] private float fadeOutDuration = 0.4f;
    [Min(0f)] [SerializeField] private float restackDelay = 0.2f;

    private readonly List<PopupSlot> activeSlots = new List<PopupSlot>();
    private ScoreManager scoreManager;
    private Vector2 stackOrigin;
    private int nextSequence;
    private bool pendingRestack;
    private float restackDelayRemaining;
    private bool hasStackOrigin;
    private bool warnedMissingStackOrigin;
    private bool warnedMissingSlots;

    private void Awake()
    {
        InitializeSlots();
    }

    private void OnEnable()
    {
        TrySubscribeToScoreManager();
    }

    private void Start()
    {
        TrySubscribeToScoreManager();
    }

    private void Update()
    {
        TrySubscribeToScoreManager();
        TickActiveSlots();
        UpdatePendingRestack();
    }

    private void OnDisable()
    {
        UnsubscribeFromScoreManager();
        HideAllSlots();
    }

    private void OnDestroy()
    {
        UnsubscribeFromScoreManager();
    }

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(pointFormat))
        {
            pointFormat = "+{0}";
        }

        showDuration = Mathf.Max(0f, showDuration);
        fadeOutDuration = Mathf.Max(0f, fadeOutDuration);
        restackDelay = Mathf.Max(0f, restackDelay);
    }

    private void OnPointsAwarded(int points)
    {
        if (points <= 0)
        {
            return;
        }

        CacheStackOrigin();
        PopupSlot slot = GetReusableSlot();
        if (slot == null)
        {
            WarnMissingSlots();
            return;
        }

        activeSlots.Remove(slot);
        int stackIndex = pendingRestack ? GetNextVisualStackIndex() : activeSlots.Count;
        slot.Show(
            this,
            points,
            pointFormat,
            stackOrigin + stackOffset * stackIndex,
            nextSequence++,
            stackIndex);

        if (slot.IsActive)
        {
            activeSlots.Add(slot);
            if (!pendingRestack)
            {
                RestackActiveSlots();
            }
        }
    }

    private void TickActiveSlots()
    {
        for (int i = activeSlots.Count - 1; i >= 0; i--)
        {
            PopupSlot slot = activeSlots[i];
            if (slot == null || !slot.IsActive)
            {
                activeSlots.RemoveAt(i);
                ScheduleRestack();
                continue;
            }

            if (slot.Tick(this, Time.deltaTime, showDuration, fadeOutDuration))
            {
                activeSlots.RemoveAt(i);
                ScheduleRestack();
            }
        }
    }

    private void UpdatePendingRestack()
    {
        if (!pendingRestack)
        {
            return;
        }

        restackDelayRemaining -= Time.deltaTime;
        if (restackDelayRemaining > 0f)
        {
            return;
        }

        pendingRestack = false;
        restackDelayRemaining = 0f;
        RestackActiveSlots();
    }

    private PopupSlot GetReusableSlot()
    {
        if (popupSlots == null || popupSlots.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < popupSlots.Length; i++)
        {
            PopupSlot slot = popupSlots[i];
            if (slot != null && !slot.IsActive)
            {
                return slot;
            }
        }

        return GetOldestActiveSlot();
    }

    private PopupSlot GetOldestActiveSlot()
    {
        PopupSlot oldest = null;
        for (int i = 0; i < activeSlots.Count; i++)
        {
            PopupSlot candidate = activeSlots[i];
            if (candidate == null)
            {
                continue;
            }

            if (oldest == null || candidate.Sequence < oldest.Sequence)
            {
                oldest = candidate;
            }
        }

        return oldest;
    }

    private void RestackActiveSlots()
    {
        activeSlots.Sort((left, right) => left.Sequence.CompareTo(right.Sequence));
        for (int i = 0; i < activeSlots.Count; i++)
        {
            activeSlots[i]?.SetStackPosition(stackOrigin + stackOffset * i, i);
        }
    }

    private int GetNextVisualStackIndex()
    {
        int nextStackIndex = 0;
        for (int i = 0; i < activeSlots.Count; i++)
        {
            PopupSlot slot = activeSlots[i];
            if (slot != null && slot.IsActive)
            {
                nextStackIndex = Mathf.Max(nextStackIndex, slot.StackIndex + 1);
            }
        }

        return nextStackIndex;
    }

    private void InitializeSlots()
    {
        if (popupSlots == null)
        {
            return;
        }

        for (int i = 0; i < popupSlots.Length; i++)
        {
            popupSlots[i]?.Initialize(this);
        }

        activeSlots.Clear();
        pendingRestack = false;
        restackDelayRemaining = 0f;
        CacheStackOrigin();
    }

    private void HideAllSlots()
    {
        activeSlots.Clear();
        pendingRestack = false;
        restackDelayRemaining = 0f;
        if (popupSlots == null)
        {
            return;
        }

        for (int i = 0; i < popupSlots.Length; i++)
        {
            popupSlots[i]?.Hide(this);
        }
    }

    private void CacheStackOrigin()
    {
        if (hasStackOrigin)
        {
            return;
        }

        if (popupSlots == null || popupSlots.Length == 0 || popupSlots[0] == null)
        {
            return;
        }

        if (popupSlots[0].TryGetStackPosition(out Vector2 origin))
        {
            stackOrigin = origin;
            hasStackOrigin = true;
            return;
        }

        if (!warnedMissingStackOrigin)
        {
            Debug.LogWarning("[ScorePointPopupGroup] Assign popupSlots[0] so the point popup stack has a base position.", this);
            warnedMissingStackOrigin = true;
        }
    }

    private void TrySubscribeToScoreManager()
    {
        if (scoreManager != null)
        {
            return;
        }

        scoreManager = ScoreManager.Instance;
        if (scoreManager == null)
        {
            return;
        }

        scoreManager.onPointsAwarded.AddListener(OnPointsAwarded);
    }

    private void UnsubscribeFromScoreManager()
    {
        if (scoreManager == null)
        {
            return;
        }

        scoreManager.onPointsAwarded.RemoveListener(OnPointsAwarded);
        scoreManager = null;
    }

    private void WarnMissingSlots()
    {
        if (warnedMissingSlots)
        {
            return;
        }

        Debug.LogWarning("[ScorePointPopupGroup] Assign at least one popup slot.", this);
        warnedMissingSlots = true;
    }

    private void ScheduleRestack()
    {
        if (restackDelay <= 0f)
        {
            pendingRestack = false;
            restackDelayRemaining = 0f;
            RestackActiveSlots();
            return;
        }

        pendingRestack = true;
        restackDelayRemaining = restackDelay;
    }
}
