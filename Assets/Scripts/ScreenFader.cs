using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the white-flash sequence that hides a biome transition. Put this on a
/// full-screen UI Image (white, stretched to the canvas) alongside a CanvasGroup.
/// Assign one ScreenFader in the scene; transition triggers use Instance directly.
///
/// Sequence: fade to white -> (optional hold) -> TerrainManager.ExecuteBiomeTransitionReset() -> fade back out.
/// </summary>
[RequireComponent(typeof(CanvasGroup), typeof(Image))]
public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance { get; private set; }

    [Header("Fade Timing")]
    [Min(0f)] public float fadeInDuration = 0.35f;
    [Min(0f)] public float fadeOutDuration = 0.35f;
    [Min(0f)] public float holdWhiteDuration = 0.1f;

    [Header("Overlay")]
    public bool forceTopmostCanvas = true;
    public int sortingOrder = short.MaxValue;

    private CanvasGroup canvasGroup;
    private Image fadeImage;
    private bool transitionInProgress;
    private bool transitionQueued;
    private bool currentTransitionResetComplete;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        EnsureInitialized();
        SetAlpha(0f);
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void BeginBiomeTransition()
    {
        if (transitionInProgress)
        {
            if (currentTransitionResetComplete)
            {
                transitionQueued = true;
            }

            return;
        }

        EnsureInitialized();
        StartCoroutine(RunBiomeTransition());
    }

    private IEnumerator RunBiomeTransition()
    {
        transitionInProgress = true;

        do
        {
            transitionQueued = false;
            currentTransitionResetComplete = false;

            yield return Fade(0f, 1f, fadeInDuration);

            if (holdWhiteDuration > 0f)
            {
                yield return new WaitForSeconds(holdWhiteDuration);
            }

            // Screen is fully white here - safe to destroy/rebuild terrain unseen.
            currentTransitionResetComplete = true;

            if (TerrainManager.Instance != null)
            {
                TerrainManager.Instance.ExecuteBiomeTransitionReset();
            }
            else
            {
                Debug.LogWarning("[ScreenFader] No TerrainManager found; the biome could not be swapped.");
            }

            yield return Fade(1f, 0f, fadeOutDuration);
        }
        while (transitionQueued);

        transitionInProgress = false;
        currentTransitionResetComplete = false;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            SetAlpha(to);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        SetAlpha(to);
    }

    private void EnsureInitialized()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        fadeImage = GetComponent<Image>();

        if (fadeImage != null)
        {
            fadeImage.color = Color.white;
            fadeImage.raycastTarget = false;
        }

        RectTransform rectTransform = transform as RectTransform;
        if (rectTransform != null)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null && forceTopmostCanvas)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;
        }

        transform.SetAsLastSibling();
    }

    private void SetAlpha(float alpha)
    {
        if (canvasGroup == null)
        {
            return;
        }

        if (fadeImage != null)
        {
            fadeImage.enabled = alpha > 0.001f;
        }

        canvasGroup.alpha = alpha;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = alpha > 0.99f;
    }
}
