using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class IrisTransition : MonoBehaviour
{
    private const float MinimumIrisScale = 0.001f;

    private sealed class PreservedRootState
    {
        public GameObject root;
        public Transform originalParent;
        public int originalSiblingIndex;
    }

    [Header("UI References")]
    [SerializeField] private RectTransform irisMask;
    [SerializeField] private RectTransform renderTextureView;
    [SerializeField] private RectTransform shockwaveMask;
    [SerializeField] private RectTransform shockwaveEffect;

    [Header("Additive Scene")]
    [SerializeField] private string targetSceneName = "GameScene";
    [SerializeField] private GameObject startMenuCanvas;
    [SerializeField] private GameObject startBackgroundRoot;
    [SerializeField] private string previewCameraName = "GameTransitionPreviewCamera";

    [Header("Scale")]
    [Min(MinimumIrisScale)]
    [SerializeField] private float closedScale = MinimumIrisScale;
    [Min(MinimumIrisScale)]
    [SerializeField] private float openScale = 8f;

    [Header("Timing")]
    [Min(0f)]
    [SerializeField] private float revealDuration = 0.45f;
    [SerializeField] private AnimationCurve easing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Shockwave")]
    [Min(0f)]
    [SerializeField] private float shockwaveScaleMultiplier = 1.12f;

    [Header("Persistence")]
    [SerializeField] private GameObject persistentRoot;

    [Header("Events")]
    [SerializeField] private UnityEvent onRevealComplete;

    private Coroutine runningTransition;
    private GameObject transitionRoot;
    private GameObject preservedStartBackgroundRoot;
    private Transform startBackgroundOriginalParent;
    private int startBackgroundOriginalSiblingIndex;
    private readonly List<PreservedRootState> preservedStartDisplayRoots = new List<PreservedRootState>();
    private readonly List<Camera> disabledGameDisplayCameras = new List<Camera>();
    private string loadingTransitionSceneName;
    private bool sceneLoadedHandlerRegistered;

    public bool IsTransitioning => runningTransition != null;

    private void Reset()
    {
        RectTransform rectTransform = transform as RectTransform;
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private void Awake()
    {
        PreserveTransitionRoot();
        DisableTransitionRaycasts();
        PrepareShockwaveHierarchy();
        SetIrisScale(closedScale);
        SetPreviewVisible(false);
    }

    public void RevealTargetSceneAdditively()
    {
        RevealSceneAdditively(targetSceneName);
    }

    public void RevealSceneAdditively(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("[IrisTransition] Cannot reveal an empty scene name.", this);
            return;
        }

        if (runningTransition != null)
        {
            StopCoroutine(runningTransition);
            AbortTransition();
        }

        runningTransition = StartCoroutine(RevealSceneAdditivelyRoutine(sceneName));
    }

    private IEnumerator RevealSceneAdditivelyRoutine(string sceneName)
    {
        if (!HasValidPreviewReferences())
        {
            runningTransition = null;
            yield break;
        }

        AudioManager.Instance?.FadeOutStartSceneMusic();

        if (startMenuCanvas != null)
        {
            startMenuCanvas.SetActive(false);
        }

        Scene startScene = SceneManager.GetActiveScene();
        PreserveStartBackground();
        PreserveStartDisplayCameras(startScene);
        SetPreviewVisible(true);
        SetIrisScale(closedScale);

        AsyncOperation loadOperation;
        try
        {
            RegisterSceneLoadedHandler(sceneName);
            loadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        }
        catch (System.Exception exception)
        {
            UnregisterSceneLoadedHandler();
            Debug.LogWarning($"[IrisTransition] Could not load scene '{sceneName}' additively. {exception.Message}", this);
            AbortTransition();
            yield break;
        }

        while (loadOperation != null && !loadOperation.isDone)
        {
            yield return null;
        }

        UnregisterSceneLoadedHandler();
        Scene gameScene = SceneManager.GetSceneByName(sceneName);
        if (!gameScene.IsValid() || !gameScene.isLoaded)
        {
            Debug.LogWarning($"[IrisTransition] Scene '{sceneName}' was not found after loading.", this);
            AbortTransition();
            yield break;
        }

        Camera previewCamera = FindCameraInScene(gameScene, previewCameraName);

        if (previewCamera == null)
        {
            Debug.LogWarning($"[IrisTransition] Preview camera not found in '{sceneName}'.", this);
            AbortTransition();
            yield break;
        }

        DisableGameDisplayCameras(gameScene, previewCamera);
        EnableCameraHierarchy(previewCamera);
        previewCamera.enabled = true;
        previewCamera.Render();

        yield return null;
        yield return AnimateIris(closedScale, openScale, revealDuration);

        SceneManager.SetActiveScene(gameScene);
        onRevealComplete?.Invoke();

        if (startScene.IsValid() && startScene != gameScene)
        {
            AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(startScene);
            while (unloadOperation != null && !unloadOperation.isDone)
            {
                yield return null;
            }

            yield return null;
        }

        CleanupPreservedStartVisuals();
        RestoreGameDisplayCameras();
        previewCamera.enabled = false;
        SetPreviewVisible(false);
        runningTransition = null;
        DestroyTransitionRoot();
    }

    private bool HasValidPreviewReferences()
    {
        if (irisMask == null || renderTextureView == null)
        {
            Debug.LogWarning("[IrisTransition] Assign Iris Mask and Render Texture View.", this);
            return false;
        }

        if (renderTextureView.GetComponent<RawImage>() == null)
        {
            Debug.LogWarning("[IrisTransition] Render Texture View needs a RawImage component.", this);
            return false;
        }

        return true;
    }

    private IEnumerator AnimateIris(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            SetIrisScale(to);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = easing != null ? easing.Evaluate(t) : t;
            SetIrisScale(Mathf.Lerp(from, to, easedT));
            yield return null;
        }

        SetIrisScale(to);
    }

    private void SetIrisScale(float scale)
    {
        scale = Mathf.Max(scale, MinimumIrisScale);

        if (irisMask != null)
        {
            irisMask.localScale = new Vector3(scale, scale, 1f);
        }

        KeepShockwaveSlightlyLarger(scale);
        KeepRenderTextureFixed(scale);
    }

    private void KeepRenderTextureFixed(float maskScale)
    {
        if (renderTextureView == null)
        {
            return;
        }

        float inverseScale = 1f / Mathf.Max(maskScale, MinimumIrisScale);
        renderTextureView.localScale = new Vector3(inverseScale, inverseScale, 1f);

        if (irisMask != null)
        {
            renderTextureView.anchoredPosition = -irisMask.anchoredPosition * inverseScale;
        }
    }

    private void SetPreviewVisible(bool visible)
    {
        if (renderTextureView != null)
        {
            renderTextureView.gameObject.SetActive(visible);
        }
    }

    private void AbortTransition()
    {
        UnregisterSceneLoadedHandler();

        if (startMenuCanvas != null)
        {
            startMenuCanvas.SetActive(true);
        }

        RestorePreservedStartBackground();
        RestorePreservedStartDisplayCameras();
        RestoreGameDisplayCameras();

        SetPreviewVisible(false);
        runningTransition = null;
    }

    private void PreserveStartBackground()
    {
        if (startBackgroundRoot == null)
        {
            preservedStartBackgroundRoot = null;
            startBackgroundOriginalParent = null;
            startBackgroundOriginalSiblingIndex = 0;
            return;
        }

        preservedStartBackgroundRoot = startBackgroundRoot;
        Transform backgroundTransform = preservedStartBackgroundRoot.transform;
        startBackgroundOriginalParent = backgroundTransform.parent;
        startBackgroundOriginalSiblingIndex = backgroundTransform.GetSiblingIndex();

        preservedStartBackgroundRoot.SetActive(true);
        backgroundTransform.SetParent(null, true);
        DontDestroyOnLoad(preservedStartBackgroundRoot);
    }

    private void RestorePreservedStartBackground()
    {
        GameObject backgroundToRestore = preservedStartBackgroundRoot != null
            ? preservedStartBackgroundRoot
            : startBackgroundRoot;

        if (backgroundToRestore == null)
        {
            return;
        }

        Transform backgroundTransform = backgroundToRestore.transform;
        if (startBackgroundOriginalParent != null)
        {
            backgroundTransform.SetParent(startBackgroundOriginalParent, true);
            backgroundTransform.SetSiblingIndex(startBackgroundOriginalSiblingIndex);
        }
        else
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && backgroundTransform.parent == null)
            {
                SceneManager.MoveGameObjectToScene(backgroundToRestore, activeScene);
            }
        }

        backgroundToRestore.SetActive(true);
        preservedStartBackgroundRoot = null;
        startBackgroundOriginalParent = null;
        startBackgroundOriginalSiblingIndex = 0;
    }

    private void PreserveStartDisplayCameras(Scene startScene)
    {
        preservedStartDisplayRoots.Clear();

        if (!startScene.IsValid() || !startScene.isLoaded)
        {
            return;
        }

        foreach (GameObject rootObject in startScene.GetRootGameObjects())
        {
            foreach (Camera camera in rootObject.GetComponentsInChildren<Camera>(true))
            {
                if (camera == null || !camera.enabled || camera.targetTexture != null)
                {
                    continue;
                }

                PreserveStartDisplayRoot(rootObject);
                break;
            }
        }
    }

    private void PreserveStartDisplayRoot(GameObject rootObject)
    {
        if (rootObject == null || rootObject == transitionRoot)
        {
            return;
        }

        for (int i = 0; i < preservedStartDisplayRoots.Count; i++)
        {
            if (preservedStartDisplayRoots[i].root == rootObject)
            {
                return;
            }
        }

        Transform rootTransform = rootObject.transform;
        PreservedRootState state = new PreservedRootState
        {
            root = rootObject,
            originalParent = rootTransform.parent,
            originalSiblingIndex = rootTransform.GetSiblingIndex()
        };

        rootObject.SetActive(true);
        rootTransform.SetParent(null, true);
        DontDestroyOnLoad(rootObject);
        preservedStartDisplayRoots.Add(state);
    }

    private void RestorePreservedStartDisplayCameras()
    {
        Scene activeScene = SceneManager.GetActiveScene();

        for (int i = 0; i < preservedStartDisplayRoots.Count; i++)
        {
            PreservedRootState state = preservedStartDisplayRoots[i];
            if (state.root == null)
            {
                continue;
            }

            Transform rootTransform = state.root.transform;
            if (state.originalParent != null)
            {
                rootTransform.SetParent(state.originalParent, true);
                rootTransform.SetSiblingIndex(state.originalSiblingIndex);
            }
            else if (activeScene.IsValid() && rootTransform.parent == null)
            {
                SceneManager.MoveGameObjectToScene(state.root, activeScene);
            }

            state.root.SetActive(true);
        }

        preservedStartDisplayRoots.Clear();
    }

    private void CleanupPreservedStartVisuals()
    {
        if (preservedStartBackgroundRoot != null)
        {
            Destroy(preservedStartBackgroundRoot);
            preservedStartBackgroundRoot = null;
        }

        for (int i = 0; i < preservedStartDisplayRoots.Count; i++)
        {
            if (preservedStartDisplayRoots[i].root != null)
            {
                Destroy(preservedStartDisplayRoots[i].root);
            }
        }

        preservedStartDisplayRoots.Clear();
        startBackgroundOriginalParent = null;
        startBackgroundOriginalSiblingIndex = 0;
    }

    private void RegisterSceneLoadedHandler(string sceneName)
    {
        UnregisterSceneLoadedHandler();
        loadingTransitionSceneName = sceneName;
        SceneManager.sceneLoaded += OnTransitionSceneLoaded;
        sceneLoadedHandlerRegistered = true;
    }

    private void UnregisterSceneLoadedHandler()
    {
        if (!sceneLoadedHandlerRegistered)
        {
            return;
        }

        SceneManager.sceneLoaded -= OnTransitionSceneLoaded;
        sceneLoadedHandlerRegistered = false;
        loadingTransitionSceneName = null;
    }

    private void OnTransitionSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!string.IsNullOrEmpty(loadingTransitionSceneName) && scene.name != loadingTransitionSceneName)
        {
            return;
        }

        Camera previewCamera = FindCameraInScene(scene, previewCameraName);
        DisableGameDisplayCameras(scene, previewCamera);
    }

    private void DisableGameDisplayCameras(Scene gameScene, Camera previewCamera)
    {
        if (!gameScene.IsValid() || !gameScene.isLoaded)
        {
            return;
        }

        foreach (GameObject rootObject in gameScene.GetRootGameObjects())
        {
            foreach (Camera camera in rootObject.GetComponentsInChildren<Camera>(true))
            {
                if (camera == null || camera == previewCamera || !camera.enabled || camera.targetTexture != null)
                {
                    continue;
                }

                camera.enabled = false;
                disabledGameDisplayCameras.Add(camera);
            }
        }
    }

    private void RestoreGameDisplayCameras()
    {
        for (int i = 0; i < disabledGameDisplayCameras.Count; i++)
        {
            Camera camera = disabledGameDisplayCameras[i];
            if (camera != null)
            {
                camera.enabled = true;
            }
        }

        disabledGameDisplayCameras.Clear();
    }

    private void PrepareShockwaveHierarchy()
    {
        if (irisMask == null || shockwaveMask == null || shockwaveEffect == null)
        {
            Debug.LogWarning("[IrisTransition] Assign Iris Mask, Shockwave Mask, and Shockwave Effect.", this);
            return;
        }

        Image irisImage = irisMask.GetComponent<Image>();
        if (irisImage == null)
        {
            Debug.LogWarning("[IrisTransition] Iris Mask needs an Image component.", this);
            return;
        }

        Image shockwaveMaskImage = shockwaveMask.GetComponent<Image>();
        if (shockwaveMaskImage == null)
        {
            Debug.LogWarning("[IrisTransition] Shockwave Mask needs an Image component.", this);
            return;
        }

        Mask shockwaveUiMask = shockwaveMask.GetComponent<Mask>();
        if (shockwaveUiMask == null)
        {
            Debug.LogWarning("[IrisTransition] Shockwave Mask needs a Mask component.", this);
            return;
        }

        shockwaveMaskImage.sprite = irisImage.sprite;
        shockwaveMaskImage.type = irisImage.type;
        shockwaveMaskImage.preserveAspect = irisImage.preserveAspect;
        shockwaveMaskImage.fillCenter = irisImage.fillCenter;
        shockwaveMaskImage.color = Color.white;
        shockwaveMaskImage.raycastTarget = false;
        shockwaveUiMask.showMaskGraphic = false;

        AlignShockwaveHierarchy();
    }

    private void KeepShockwaveSlightlyLarger(float irisScale)
    {
        if (shockwaveMask == null || shockwaveEffect == null)
        {
            return;
        }

        float scale = Mathf.Max(irisScale, MinimumIrisScale) * Mathf.Max(0f, shockwaveScaleMultiplier);
        shockwaveMask.localScale = new Vector3(scale, scale, 1f);
        shockwaveEffect.localScale = Vector3.one;
    }

    private void AlignShockwaveHierarchy()
    {
        if (shockwaveMask == null || shockwaveEffect == null || irisMask == null)
        {
            return;
        }

        Transform shockwaveParent = irisMask.parent;
        if (shockwaveParent != null)
        {
            shockwaveMask.SetParent(shockwaveParent, false);
        }

        shockwaveMask.anchorMin = irisMask.anchorMin;
        shockwaveMask.anchorMax = irisMask.anchorMax;
        shockwaveMask.pivot = irisMask.pivot;
        shockwaveMask.anchoredPosition = irisMask.anchoredPosition;
        shockwaveMask.sizeDelta = irisMask.sizeDelta;
        shockwaveMask.localRotation = irisMask.localRotation;
        shockwaveMask.SetSiblingIndex(irisMask.GetSiblingIndex() + 1);

        shockwaveEffect.SetParent(shockwaveMask, false);
        shockwaveEffect.anchorMin = new Vector2(0.5f, 0.5f);
        shockwaveEffect.anchorMax = new Vector2(0.5f, 0.5f);
        shockwaveEffect.pivot = new Vector2(0.5f, 0.5f);
        shockwaveEffect.anchoredPosition = Vector2.zero;
        shockwaveEffect.sizeDelta = irisMask.rect.size;
        shockwaveEffect.localRotation = Quaternion.identity;
        shockwaveEffect.SetAsLastSibling();
        float currentIrisScale = Mathf.Max(irisMask.localScale.x, MinimumIrisScale);
        KeepShockwaveSlightlyLarger(currentIrisScale);
    }

    private void PreserveTransitionRoot()
    {
        Canvas parentCanvas = GetComponentInParent<Canvas>();
        transitionRoot = persistentRoot != null
            ? persistentRoot
            : parentCanvas != null
                ? parentCanvas.gameObject
                : gameObject;

        if (transitionRoot.transform.parent != null)
        {
            transitionRoot.transform.SetParent(null, true);
        }

        DontDestroyOnLoad(transitionRoot);
    }

    private void DestroyTransitionRoot()
    {
        GameObject rootToDestroy = transitionRoot != null ? transitionRoot : gameObject;

        if (rootToDestroy != null)
        {
            Destroy(rootToDestroy);
        }
    }

    private void DisableTransitionRaycasts()
    {
        DisableRaycasts(irisMask);
        DisableRaycasts(renderTextureView);
        DisableRaycasts(shockwaveMask);
        DisableRaycasts(shockwaveEffect);
    }

    private static void DisableRaycasts(RectTransform root)
    {
        if (root == null)
        {
            return;
        }

        foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
        {
            graphic.raycastTarget = false;
        }
    }

    private Camera FindCameraInScene(Scene scene, string cameraName)
    {
        Camera fallbackCamera = null;

        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            foreach (Camera candidate in rootObject.GetComponentsInChildren<Camera>(true))
            {
                if (MatchesCameraName(candidate, cameraName))
                {
                    return candidate;
                }

                if (fallbackCamera == null)
                {
                    fallbackCamera = candidate;
                }
            }
        }

        return fallbackCamera;
    }

    private static bool MatchesCameraName(Camera cameraToCheck, string cameraName)
    {
        if (cameraToCheck == null || string.IsNullOrWhiteSpace(cameraName))
        {
            return false;
        }

        Transform current = cameraToCheck.transform;
        while (current != null)
        {
            if (current.gameObject.name == cameraName)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private void EnableCameraHierarchy(Camera cameraToEnable)
    {
        Transform current = cameraToEnable.transform;
        while (current != null)
        {
            current.gameObject.SetActive(true);
            current = current.parent;
        }
    }
}
