using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class GameOverCutsceneController : MonoBehaviour
{
    private const string PlayerTag = "Player";
    private const float MissingEmotionMeterWarningDelay = 1f;

    [Header("References")]
    [SerializeField] private PlayerController player;
    [SerializeField] private GameObject[] gameplayUiRoots;
    [SerializeField] private CanvasGroup blackFadeOverlay;
    [SerializeField] private GameObject gameOverPageRoot;
    [SerializeField] private TMP_Text finalScoreText;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button homeButton;

    [Header("Timing")]
    [Min(0f)] [SerializeField] private float cutsceneDuration = 3f;
    [Min(0f)] [SerializeField] private float fadeInDuration = 0.6f;

    [Header("Score")]
    [SerializeField] private string finalScoreFormat = "Score {0}";

    private EmotionMeter emotionMeter;
    private Coroutine runningSequence;
    private bool gameOverStarted;
    private bool cutsceneSpeedModifierApplied;
    private bool anxietyBlurBoostApplied;
    private bool anxietyCameraShakeApplied;
    private bool warnedMissingPlayerTag;
    private bool warnedMissingEmotionMeter;
    private float missingEmotionMeterElapsed;
    private AnxietyBlurPulseMaterialManager anxietyBlurManager;
    private CameraFollow anxietyCameraFollow;

    private void Awake()
    {
        ResolvePlayerReference();
        WireButtons();
        HideGameOverPage();
        SetFadeAlpha(0f);
    }

    private void OnEnable()
    {
        ResolvePlayerReference();
        SubscribeToEmotionMeter();
        WireButtons();
    }

    private void Start()
    {
        SubscribeToEmotionMeter();
    }

    private void Update()
    {
        if (gameOverStarted)
        {
            return;
        }

        SubscribeToEmotionMeter();
        if (emotionMeter != null)
        {
            TryStartGameOver(emotionMeter.Value);
        }
    }

    private void OnDisable()
    {
        UnsubscribeFromEmotionMeter();
        StopRunningSequence();
        ClearCutsceneEffects();
        SetPlayerInputBlocked(false);
        SetPauseBlocked(false);
    }

    private void OnDestroy()
    {
        UnsubscribeFromEmotionMeter();
        ClearCutsceneEffects();
        SetPlayerInputBlocked(false);
        SetPauseBlocked(false);
    }

    private void OnValidate()
    {
        cutsceneDuration = Mathf.Max(0f, cutsceneDuration);
        fadeInDuration = Mathf.Max(0f, fadeInDuration);
        if (string.IsNullOrEmpty(finalScoreFormat))
        {
            finalScoreFormat = "{0}";
        }
    }

    private void SubscribeToEmotionMeter()
    {
        EmotionMeter currentMeter = ResolveEmotionMeter();
        if (emotionMeter != null && emotionMeter == currentMeter)
        {
            return;
        }

        UnsubscribeFromEmotionMeter();
        emotionMeter = currentMeter;
        if (emotionMeter == null)
        {
            UpdateMissingEmotionMeterWarning();
            return;
        }

        warnedMissingEmotionMeter = false;
        missingEmotionMeterElapsed = 0f;
        emotionMeter.onValueChanged.AddListener(OnEmotionValueChanged);
        TryStartGameOver(emotionMeter.Value);
    }

    private void UnsubscribeFromEmotionMeter()
    {
        if (emotionMeter != null)
        {
            emotionMeter.onValueChanged.RemoveListener(OnEmotionValueChanged);
        }

        emotionMeter = null;
    }

    private void OnEmotionValueChanged(float value)
    {
        TryStartGameOver(value);
    }

    public void StartGameOverAfterFadeOnly()
    {
        if (gameOverStarted)
        {
            return;
        }

        gameOverStarted = true;
        Debug.Log("[GameOverCutsceneController] Gap game over started.", this);
        runningSequence = StartCoroutine(RunFadeOnlyGameOverSequence());
    }

    private void TryStartGameOver(float value)
    {
        if (gameOverStarted || value < 100f)
        {
            return;
        }

        gameOverStarted = true;
        LogGameOverStart();
        runningSequence = StartCoroutine(RunGameOverSequence());
    }

    private IEnumerator RunGameOverSequence()
    {
        SetPauseBlocked(true);
        SetPlayerInputBlocked(true);
        HideGameplayUi();
        AudioManager.Instance?.PlayGameOverMusic();
        BiomeData activeBiome = GetActiveBiome();

        yield return RunCutscene(activeBiome);
        yield return FadeToBlack();

        ClearCutsceneEffects();
        CompleteGameOverAfterFade();
        runningSequence = null;
    }

    private IEnumerator RunFadeOnlyGameOverSequence()
    {
        SetPauseBlocked(true);
        SetPlayerInputBlocked(true);
        HideGameplayUi();
        AudioManager.Instance?.PlayGameOverMusic();
        yield return FadeToBlack();

        CompleteGameOverAfterFade();
        runningSequence = null;
    }

    private IEnumerator RunCutscene(BiomeData biome)
    {
        float duration = Mathf.Max(0f, cutsceneDuration);
        if (duration <= 0f)
        {
            ApplyCutsceneFrame(biome, 1f);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            ApplyCutsceneFrame(biome, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        ApplyCutsceneFrame(biome, 1f);
    }

    private void ApplyCutsceneFrame(BiomeData biome, float progress)
    {
        if (biome == null)
        {
            return;
        }

        switch (biome.gameOverCutscene)
        {
            case GameOverCutsceneType.AngerSpeedUp:
                ApplyAngerCutsceneSpeed(biome, progress);
                break;
            case GameOverCutsceneType.SadnessSlowDown:
                ApplySadnessCutsceneSpeed(biome, progress);
                break;
            case GameOverCutsceneType.AnxietyBlurShake:
                ApplyAnxietyCutsceneEffects(biome, progress);
                break;
        }
    }

    private void ApplyAngerCutsceneSpeed(BiomeData biome, float progress)
    {
        ResolvePlayerReference();
        if (player == null)
        {
            return;
        }

        float from = Mathf.Max(0f, biome.angerGameOverStartSpeedMultiplier);
        float to = Mathf.Max(0f, biome.angerGameOverEndSpeedMultiplier);
        float multiplier = Mathf.Lerp(from, to, Mathf.Clamp01(progress));
        player.AddMovementModifier(this, multiplier, 1f, 1f);
        cutsceneSpeedModifierApplied = true;
    }

    private void ApplySadnessCutsceneSpeed(BiomeData biome, float progress)
    {
        ResolvePlayerReference();
        if (player == null)
        {
            return;
        }

        float from = Mathf.Max(0f, biome.sadnessGameOverStartSpeedMultiplier);
        float multiplier = Mathf.Lerp(from, 0f, Mathf.Clamp01(progress));
        player.AddMovementModifier(this, multiplier, 1f, 1f);
        cutsceneSpeedModifierApplied = true;
    }

    private void ApplyAnxietyCutsceneEffects(BiomeData biome, float progress)
    {
        float clampedProgress = Mathf.Clamp01(progress);

        ResolveAnxietyBlurManager();
        if (anxietyBlurManager != null)
        {
            float startBlurBoost = Mathf.Max(0f, biome.anxietyGameOverStartBlurBoost);
            float endBlurBoost = Mathf.Max(0f, biome.anxietyGameOverEndBlurBoost);
            float blurBoost = Mathf.Lerp(startBlurBoost, endBlurBoost, clampedProgress);
            anxietyBlurManager.SetAdditiveBlurBoost(this, blurBoost);
            anxietyBlurBoostApplied = blurBoost > 0f;
        }

        ResolveAnxietyCameraFollow();
        if (anxietyCameraFollow != null)
        {
            float startShakeAmplitude = Mathf.Max(0f, biome.anxietyGameOverStartShakeAmplitude);
            float endShakeAmplitude = Mathf.Max(0f, biome.anxietyGameOverEndShakeAmplitude);
            float shakeAmplitude = Mathf.Lerp(startShakeAmplitude, endShakeAmplitude, clampedProgress);
            anxietyCameraFollow.SetContinuousShake(this, shakeAmplitude);
            anxietyCameraShakeApplied = shakeAmplitude > 0f;
        }
    }

    private IEnumerator FadeToBlack()
    {
        if (blackFadeOverlay == null)
        {
            yield break;
        }

        blackFadeOverlay.gameObject.SetActive(true);
        blackFadeOverlay.interactable = false;
        blackFadeOverlay.blocksRaycasts = false;

        if (fadeInDuration <= 0f)
        {
            SetFadeAlpha(1f);
            yield break;
        }

        float startAlpha = blackFadeOverlay.alpha;
        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            SetFadeAlpha(Mathf.Lerp(startAlpha, 1f, Mathf.Clamp01(elapsed / fadeInDuration)));
            yield return null;
        }

        SetFadeAlpha(1f);
    }

    private void HideGameplayUi()
    {
        if (gameplayUiRoots == null)
        {
            return;
        }

        for (int i = 0; i < gameplayUiRoots.Length; i++)
        {
            if (gameplayUiRoots[i] != null)
            {
                gameplayUiRoots[i].SetActive(false);
            }
        }
    }

    private void ShowGameOverPage()
    {
        EnsureEventSystem();
        WireButtons();

        if (gameOverPageRoot != null)
        {
            gameOverPageRoot.SetActive(true);
            gameOverPageRoot.transform.SetAsLastSibling();

            CanvasGroup pageCanvasGroup = gameOverPageRoot.GetComponent<CanvasGroup>();
            if (pageCanvasGroup != null)
            {
                pageCanvasGroup.alpha = 1f;
                pageCanvasGroup.interactable = true;
                pageCanvasGroup.blocksRaycasts = true;
            }
        }

        SetGameOverButtonsInteractable(true);
    }

    private void HideGameOverPage()
    {
        if (gameOverPageRoot != null)
        {
            gameOverPageRoot.SetActive(false);
        }

        SetGameOverButtonsInteractable(false);
    }

    private void RefreshFinalScore()
    {
        if (finalScoreText == null)
        {
            return;
        }

        int score = ScoreManager.Instance != null ? ScoreManager.Instance.Score : 0;
        finalScoreText.text = string.Format(finalScoreFormat, score);
    }

    private void WireButtons()
    {
        WireButton(retryButton, RetryCurrentScene);
        WireButton(homeButton, ReturnHome);
    }

    private void CompleteGameOverAfterFade()
    {
        GameManager.Instance?.TriggerGameOver();
        RefreshFinalScore();
        ShowGameOverPage();
        Time.timeScale = 0f;
    }

    private void SetGameOverButtonsInteractable(bool interactable)
    {
        if (retryButton != null)
        {
            retryButton.interactable = interactable;
        }

        if (homeButton != null)
        {
            homeButton.interactable = interactable;
        }
    }

    private static void WireButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private static void EnsureEventSystem()
    {
        EventSystem eventSystem =
            EventSystem.current != null
                ? EventSystem.current
                : FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);

        if (eventSystem != null)
        {
            return;
        }

        new GameObject(
            "EventSystem",
            typeof(EventSystem),
            typeof(StandaloneInputModule));
    }

    private void RetryCurrentScene()
    {
        Time.timeScale = 1f;
        Scene activeScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(activeScene.name);
    }

    private void ReturnHome()
    {
        Time.timeScale = 1f;
        GameManager.Instance?.ReturnHome();
    }

    private void StopRunningSequence()
    {
        if (runningSequence == null)
        {
            return;
        }

        StopCoroutine(runningSequence);
        runningSequence = null;
    }

    private void SetPauseBlocked(bool blocked)
    {
        GameManager.Instance?.SetPauseBlocked(this, blocked);
    }

    private void SetPlayerInputBlocked(bool blocked)
    {
        ResolvePlayerReference();
        if (player != null)
        {
            player.SetInputBlocked(this, blocked);
        }
    }

    private EmotionMeter ResolveEmotionMeter()
    {
        return EmotionMeter.Instance != null
            ? EmotionMeter.Instance
            : FindFirstObjectByType<EmotionMeter>();
    }

    private void UpdateMissingEmotionMeterWarning()
    {
        if (warnedMissingEmotionMeter)
        {
            return;
        }

        missingEmotionMeterElapsed += Time.deltaTime;
        if (missingEmotionMeterElapsed < MissingEmotionMeterWarningDelay)
        {
            return;
        }

        Debug.LogWarning("[GameOverCutsceneController] No EmotionMeter found; game over cannot trigger.", this);
        warnedMissingEmotionMeter = true;
    }

    private void LogGameOverStart()
    {
        BiomeData activeBiome = GetActiveBiome();
        string biomeName = activeBiome != null ? activeBiome.biomeName : "None";
        string cutsceneName = activeBiome != null ? activeBiome.gameOverCutscene.ToString() : GameOverCutsceneType.None.ToString();
        Debug.Log($"[GameOverCutsceneController] Game over started. Biome: {biomeName}, Cutscene: {cutsceneName}.", this);
    }

    private void ClearCutsceneSpeedModifier()
    {
        if (!cutsceneSpeedModifierApplied)
        {
            return;
        }

        if (player != null)
        {
            player.RemoveMovementModifier(this);
        }

        cutsceneSpeedModifierApplied = false;
    }

    private void ClearCutsceneEffects()
    {
        ClearCutsceneSpeedModifier();
        ClearAnxietyCutsceneEffects();
    }

    private void ClearAnxietyCutsceneEffects()
    {
        if (anxietyBlurBoostApplied && anxietyBlurManager != null)
        {
            anxietyBlurManager.RemoveAdditiveBlurBoost(this);
        }

        if (anxietyCameraShakeApplied && anxietyCameraFollow != null)
        {
            anxietyCameraFollow.RemoveContinuousShake(this);
        }

        anxietyBlurBoostApplied = false;
        anxietyCameraShakeApplied = false;
    }

    private void SetFadeAlpha(float alpha)
    {
        if (blackFadeOverlay == null)
        {
            return;
        }

        blackFadeOverlay.alpha = Mathf.Clamp01(alpha);
        blackFadeOverlay.interactable = false;
        blackFadeOverlay.blocksRaycasts = false;
    }

    private BiomeData GetActiveBiome()
    {
        if (BiomeManager.Instance != null)
        {
            return BiomeManager.Instance.CurrentBiome;
        }

        return TerrainManager.Instance != null ? TerrainManager.Instance.CurrentBiome : null;
    }

    private void ResolvePlayerReference()
    {
        if (player != null)
        {
            return;
        }

        GameObject playerObject = FindPlayerObject();
        if (playerObject != null)
        {
            player = playerObject.GetComponent<PlayerController>();
            if (player == null)
            {
                player = playerObject.GetComponentInParent<PlayerController>();
            }

            if (player == null)
            {
                player = playerObject.GetComponentInChildren<PlayerController>();
            }
        }

        if (player == null)
        {
            player = FindFirstObjectByType<PlayerController>();
        }
    }

    private void ResolveAnxietyBlurManager()
    {
        if (anxietyBlurManager != null)
        {
            return;
        }

        anxietyBlurManager = FindFirstObjectByType<AnxietyBlurPulseMaterialManager>();
    }

    private void ResolveAnxietyCameraFollow()
    {
        if (anxietyCameraFollow != null)
        {
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            anxietyCameraFollow = mainCamera.GetComponent<CameraFollow>();
        }

        if (anxietyCameraFollow == null)
        {
            anxietyCameraFollow = FindFirstObjectByType<CameraFollow>();
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
            if (!warnedMissingPlayerTag)
            {
                Debug.LogWarning($"[GameOverCutsceneController] No Unity tag named '{PlayerTag}' exists.", this);
                warnedMissingPlayerTag = true;
            }

            return null;
        }
    }
}
