using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Displays jump lock and input delay indicators around the player.</summary>
[DisallowMultipleComponent]
public sealed class PlayerStatusUI : MonoBehaviour
{
    [SerializeField] private PlayerStatusManager statusManager;
    [Header("Status Indicators")]
    [SerializeField] private RectTransform spacebarUI;
    [SerializeField] private RectTransform delayUI;
    [Min(0f)] [SerializeField] private float indicatorSpacing = 100f;
    [Min(0f)] [SerializeField] private float fadeInDuration = 0.2f;
    [Min(0f)] [SerializeField] private float fadeOutDuration = 0.2f;
    [Range(0f, 1f)] [SerializeField] private float hiddenScale = 0.8f;
    [Header("Zoom Pulse")]
    [Tooltip("Seconds for one complete zoom in and out. Set to zero to disable.")]
    [Min(0f)] [SerializeField] private float pulseDuration = 1f;
    [Tooltip("Extra scale at the peak of each pulse (0.08 means 8% larger).")]
    [Range(0f, 0.5f)] [SerializeField] private float pulseAmount = 0.08f;
    [Header("Orbit Around Player")]
    [SerializeField] private bool orbitEnabled = true;
    [Tooltip("Optional target. Defaults to the player from the status manager.")]
    [SerializeField] private Transform orbitTarget;
    [SerializeField] private Camera orbitCamera;
    [Min(0f)] [SerializeField] private float orbitRadius = 1.2f;
    [SerializeField] private float orbitHeight = 1.5f;
    [Tooltip("Degrees per second around the world's vertical axis.")]
    [SerializeField] private float orbitSpeed = 90f;
    [Header("In-Game Display")]
    [Tooltip("Creates a world-space canvas at startup that follows the orbit position.")]
    [SerializeField] private bool useWorldSpace = true;
    [Tooltip("World units per UI pixel. Increase to make the prompt larger.")]
    [Min(0.0001f)] [SerializeField] private float worldScale = 0.005f;
    [Tooltip("Keeps the icons readable when the gameplay camera zooms out. Zero disables this minimum.")]
    [Min(0f)] [SerializeField] private float minimumIconPixels = 32f;
    [SerializeField] private string worldSortingLayer = "Canvas";
    [SerializeField] private int worldSortingOrder = 10;


    private StatusVisual spacebarVisual;
    private StatusVisual delayVisual;
    private CanvasGroup visibilityGroup;
    private Vector3 originalScale;
    private float visibility;
    private float pulseElapsed;
    private float orbitAngle;
    private RectTransform uiTransform;
    private Canvas canvas;
    private Vector3 originalPosition;
    private bool orbitVisible = true;
    private RectTransform worldCanvasTransform;
    private float iconLocalHeight = 1f;

    private sealed class StatusVisual
    {
        private readonly RectTransform root;
        private readonly CanvasGroup group;
        private readonly Vector3 scale;
        private float progress;

        public StatusVisual(RectTransform root)
        {
            this.root = root;
            if (root == null) return;
            scale = root.localScale;
            group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            root.gameObject.SetActive(true);
        }

        public void Tick(bool show, float duration, float hiddenScale)
        {
            if (root == null) return;
            progress = duration <= 0f ? (show ? 1f : 0f) :
                Mathf.MoveTowards(progress, show ? 1f : 0f, Time.unscaledDeltaTime / duration);
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            group.alpha = eased;
            root.localScale = scale * Mathf.Lerp(hiddenScale, 1f, eased);
        }

        public void Dispose()
        {
            if (root != null) root.localScale = scale;
            if (group != null) Object.Destroy(group);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterScenes()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (RectTransform candidate in root.GetComponentsInChildren<RectTransform>(true))
            {
                if (candidate.name == "PlayerStatusUI" && candidate.GetComponent<PlayerStatusUI>() == null)
                {
                    candidate.gameObject.AddComponent<PlayerStatusUI>();
                }
            }
        }
    }

    private void Awake()
    {
        originalScale = transform.localScale;
        originalPosition = transform.localPosition;
        uiTransform = transform as RectTransform;
        canvas = GetComponentInParent<Canvas>();
        // A separate group preserves any opacity set by the scene reveal or other UI scripts.
        visibilityGroup = gameObject.AddComponent<CanvasGroup>();
        visibilityGroup.interactable = false;
        visibilityGroup.blocksRaycasts = false;
        foreach (RectTransform child in GetComponentsInChildren<RectTransform>(true))
        {
            if (spacebarUI == null && child.name == "SpacebarUI") spacebarUI = child;
            if (delayUI == null && child.name == "DelayUI") delayUI = child;
        }
        spacebarVisual = new StatusVisual(spacebarUI);
        delayVisual = new StatusVisual(delayUI);
        float spacebarHeight = spacebarUI != null ? Mathf.Abs(spacebarUI.rect.height * spacebarUI.localScale.y) : 0f;
        float delayHeight = delayUI != null ? Mathf.Abs(delayUI.rect.height * delayUI.localScale.y) : 0f;
        iconLocalHeight = Mathf.Max(1f, spacebarHeight > 0f && delayHeight > 0f
            ? Mathf.Min(spacebarHeight, delayHeight) : Mathf.Max(spacebarHeight, delayHeight));
        if (useWorldSpace && uiTransform != null)
        {
            CreateWorldCanvas();
        }
        ApplyVisibility();
    }

    private void CreateWorldCanvas()
    {
        GameObject canvasObject = new GameObject("Player Status World Canvas", typeof(RectTransform), typeof(Canvas));
        canvasObject.layer = gameObject.layer;
        SceneManager.MoveGameObjectToScene(canvasObject, gameObject.scene);
        worldCanvasTransform = canvasObject.GetComponent<RectTransform>();
        Canvas worldCanvas = canvasObject.GetComponent<Canvas>();
        worldCanvas.renderMode = RenderMode.WorldSpace;
        worldCanvas.sortingLayerID = SortingLayer.NameToID(worldSortingLayer);
        worldCanvas.sortingOrder = worldSortingOrder;
        worldCanvasTransform.sizeDelta = uiTransform.rect.size;
        worldCanvasTransform.localScale = Vector3.one * Mathf.Max(0.0001f, worldScale);
        uiTransform.SetParent(worldCanvasTransform, false);
        uiTransform.localPosition = Vector3.zero;
        canvas = worldCanvas;
    }

    private void LateUpdate()
    {
        if (statusManager == null)
        {
            statusManager = FindFirstObjectByType<PlayerStatusManager>();
        }

        PlayerController player = statusManager != null ? statusManager.Player : null;
        bool spacebarLocked = player != null && (player.IsJumpBlocked || player.IsInputBlocked);
        bool inputsDelayed = player != null && player.IsInputDelayed;
        spacebarVisual.Tick(spacebarLocked, spacebarLocked ? fadeInDuration : fadeOutDuration, hiddenScale);
        delayVisual.Tick(inputsDelayed, inputsDelayed ? fadeInDuration : fadeOutDuration, hiddenScale);
        bool show = spacebarLocked || inputsDelayed;
        if (worldCanvasTransform != null)
        {
            if (spacebarUI != null)
                spacebarUI.localPosition = new Vector3(inputsDelayed ? -indicatorSpacing * 0.5f : 0f, 0f, 0f);
            if (delayUI != null)
                delayUI.localPosition = new Vector3(spacebarLocked ? indicatorSpacing * 0.5f : 0f, 0f, 0f);
        }

        float duration = show ? fadeInDuration : fadeOutDuration;
        visibility = duration <= 0f ? (show ? 1f : 0f) :
            Mathf.MoveTowards(visibility, show ? 1f : 0f, Time.unscaledDeltaTime / duration);
        if (visibility > 0f && pulseDuration > 0f)
        {
            pulseElapsed = Mathf.Repeat(pulseElapsed + Time.unscaledDeltaTime, pulseDuration);
        }
        else
        {
            pulseElapsed = 0f;
        }
        UpdateOrbit();
        ApplyVisibility();
    }

    private void UpdateOrbit()
    {
        orbitVisible = true;
        if (!orbitEnabled && worldCanvasTransform == null)
        {
            transform.localPosition = originalPosition;
            return;
        }

        Transform target = orbitTarget;
        if (target == null && statusManager != null)
        {
            target = statusManager.Player != null
                ? statusManager.Player.transform : statusManager.transform;
        }

        if (orbitCamera == null)
        {
            orbitCamera = Camera.main;
        }

        if (target == null || orbitCamera == null || uiTransform == null || canvas == null)
        {
            return;
        }

        if (visibility > 0f && orbitEnabled)
        {
            orbitAngle = Mathf.Repeat(orbitAngle + orbitSpeed * Time.unscaledDeltaTime, 360f);
        }

        float radians = orbitAngle * Mathf.Deg2Rad;
        Vector3 worldPosition = target.position + Vector3.up * orbitHeight +
            new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians)) * orbitRadius;
        if (worldCanvasTransform != null)
        {
            worldCanvasTransform.position = worldPosition;
            worldCanvasTransform.rotation = orbitCamera.transform.rotation;
            float unitsPerPixel = orbitCamera.orthographic
                ? orbitCamera.orthographicSize * 2f / Mathf.Max(1, orbitCamera.pixelHeight)
                : 2f * Mathf.Max(orbitCamera.nearClipPlane,
                    Vector3.Dot(worldPosition - orbitCamera.transform.position, orbitCamera.transform.forward)) *
                    Mathf.Tan(orbitCamera.fieldOfView * Mathf.Deg2Rad * 0.5f) / Mathf.Max(1, orbitCamera.pixelHeight);
            float readableScale = minimumIconPixels * unitsPerPixel /
                (iconLocalHeight * Mathf.Max(0.0001f, Mathf.Abs(originalScale.y)));
            worldCanvasTransform.localScale = Vector3.one * Mathf.Max(0.0001f, worldScale, readableScale);
            canvas.sortingLayerID = SortingLayer.NameToID(worldSortingLayer);
            canvas.sortingOrder = worldSortingOrder;
            canvas.worldCamera = orbitCamera;
            return;
        }
        Vector3 screenPosition = orbitCamera.WorldToScreenPoint(worldPosition);
        orbitVisible = screenPosition.z > 0f;
        RectTransform parent = uiTransform.parent as RectTransform;
        Canvas rootCanvas = canvas.rootCanvas;
        Camera uiCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null : rootCanvas.worldCamera;
        if (parent != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parent, screenPosition, uiCamera, out Vector2 localPoint))
        {
            uiTransform.localPosition = new Vector3(localPoint.x, localPoint.y, originalPosition.z);
        }
    }

    private void ApplyVisibility()
    {
        float eased = Mathf.SmoothStep(0f, 1f, visibility);
        visibilityGroup.alpha = orbitVisible ? eased : 0f;
        float pulse = pulseDuration > 0f
            ? (1f - Mathf.Cos(pulseElapsed / pulseDuration * Mathf.PI * 2f)) * 0.5f
            : 0f;
        float scale = Mathf.Lerp(hiddenScale, 1f, eased);
        transform.localScale = originalScale * scale * (1f + pulse * pulseAmount * eased);
    }

    private void OnDisable()
    {
        visibility = 0f;
        pulseElapsed = 0f;
        spacebarVisual?.Tick(false, 0f, hiddenScale);
        delayVisual?.Tick(false, 0f, hiddenScale);
        if (visibilityGroup != null)
        {
            ApplyVisibility();
        }
    }

    private void OnDestroy()
    {
        spacebarVisual?.Dispose();
        delayVisual?.Dispose();
        if (worldCanvasTransform != null)
        {
            // Unity may already be destroying this panel or its scene hierarchy.
            // Never reparent objects from OnDestroy.
            Destroy(worldCanvasTransform.gameObject);
        }
        if (visibilityGroup != null)
        {
            Destroy(visibilityGroup);
        }
    }
}
