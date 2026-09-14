using System.Collections;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BiomeTransitionTextBanner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text targetText;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Timing")]
    [Min(0f)] [SerializeField] private float fadeInDuration = 0.25f;
    [Min(0f)] [SerializeField] private float holdDuration = 1f;
    [Min(0f)] [SerializeField] private float fadeOutDuration = 0.45f;

    private BiomeManager biomeManager;
    private Coroutine fadeRoutine;
    private bool subscribedToBiomeManager;
    private bool showNextCommittedBiomeChange;
    private bool warnedMissingText;
    private bool warnedMissingCanvasGroup;

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
        HideImmediately();
    }

    private void OnEnable()
    {
        ResolveReferences();
        TrySubscribeToBiomeManager();
        HideImmediately();
    }

    private void Update()
    {
        if (!subscribedToBiomeManager)
        {
            TrySubscribeToBiomeManager();
        }
    }

    private void OnDisable()
    {
        StopFadeRoutine();
        HideImmediately();
        UnsubscribeFromBiomeManager();
    }

    private void OnDestroy()
    {
        UnsubscribeFromBiomeManager();
    }

    private void OnValidate()
    {
        fadeInDuration = Mathf.Max(0f, fadeInDuration);
        holdDuration = Mathf.Max(0f, holdDuration);
        fadeOutDuration = Mathf.Max(0f, fadeOutDuration);
    }

    private void OnBiomeChanged(BiomeData biome)
    {
        if (!showNextCommittedBiomeChange)
        {
            return;
        }

        showNextCommittedBiomeChange = false;

        if (!TryGetText(out TMP_Text text) || !TryGetCanvasGroup(out CanvasGroup group))
        {
            return;
        }

        string label = GetDisplayName(biome);
        if (string.IsNullOrEmpty(label))
        {
            return;
        }

        text.text = label;
        StopFadeRoutine();
        fadeRoutine = StartCoroutine(FadeSequence(group));
    }

    private void OnInterFamilyTransitionRequested()
    {
        showNextCommittedBiomeChange = true;
    }

    private IEnumerator FadeSequence(CanvasGroup group)
    {
        yield return Fade(group, group.alpha, 1f, fadeInDuration);

        if (holdDuration > 0f)
        {
            yield return new WaitForSeconds(holdDuration);
        }

        yield return Fade(group, group.alpha, 0f, fadeOutDuration);
        fadeRoutine = null;
    }

    private IEnumerator Fade(CanvasGroup group, float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            SetAlpha(group, to);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetAlpha(group, Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        SetAlpha(group, to);
    }

    private void TrySubscribeToBiomeManager()
    {
        if (subscribedToBiomeManager)
        {
            return;
        }

        biomeManager = BiomeManager.Instance;
        if (biomeManager == null)
        {
            return;
        }

        biomeManager.EnsureInitialized();
        biomeManager.onInterFamilyTransitionRequested.AddListener(OnInterFamilyTransitionRequested);
        biomeManager.onBiomeChanged.AddListener(OnBiomeChanged);
        subscribedToBiomeManager = true;
    }

    private void UnsubscribeFromBiomeManager()
    {
        if (!subscribedToBiomeManager || biomeManager == null)
        {
            subscribedToBiomeManager = false;
            biomeManager = null;
            return;
        }

        biomeManager.onInterFamilyTransitionRequested.RemoveListener(OnInterFamilyTransitionRequested);
        biomeManager.onBiomeChanged.RemoveListener(OnBiomeChanged);
        subscribedToBiomeManager = false;
        biomeManager = null;
        showNextCommittedBiomeChange = false;
    }

    private void ResolveReferences()
    {
        if (targetText == null)
        {
            targetText = GetComponent<TMP_Text>();
        }

        if (targetText == null)
        {
            targetText = GetComponentInChildren<TMP_Text>(true);
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
    }

    private bool TryGetText(out TMP_Text text)
    {
        ResolveReferences();
        text = targetText;
        if (text != null)
        {
            return true;
        }

        if (!warnedMissingText)
        {
            Debug.LogWarning("[BiomeTransitionTextBanner] Assign a TMP_Text target.", this);
            warnedMissingText = true;
        }

        return false;
    }

    private bool TryGetCanvasGroup(out CanvasGroup group)
    {
        ResolveReferences();
        group = canvasGroup;
        if (group != null)
        {
            return true;
        }

        if (!warnedMissingCanvasGroup)
        {
            Debug.LogWarning("[BiomeTransitionTextBanner] Assign a CanvasGroup for fading.", this);
            warnedMissingCanvasGroup = true;
        }

        return false;
    }

    private void StopFadeRoutine()
    {
        if (fadeRoutine == null)
        {
            return;
        }

        StopCoroutine(fadeRoutine);
        fadeRoutine = null;
    }

    private void HideImmediately()
    {
        if (canvasGroup == null)
        {
            return;
        }

        SetAlpha(canvasGroup, 0f);
    }

    private static void SetAlpha(CanvasGroup group, float alpha)
    {
        if (group == null)
        {
            return;
        }

        group.alpha = Mathf.Clamp01(alpha);
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    private static string GetDisplayName(BiomeData biome)
    {
        if (biome == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(biome.familyId))
        {
            return biome.familyId.Trim();
        }

        return !string.IsNullOrWhiteSpace(biome.biomeName)
            ? biome.biomeName.Trim()
            : string.Empty;
    }
}
