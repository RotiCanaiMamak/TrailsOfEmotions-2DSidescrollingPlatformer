using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    private const string PlayerTag = "Player";
    private const string HomeSceneName = "StartScene";
    private const string PauseMenuControllerTypeName = "PauseMenuController";

    private PlayerController player;
    private MonoBehaviour pauseMenuController;
    private bool warnedMissingPauseMenuController;

    [Header("Movement Gate")]
    [Tooltip("Delay after the game scene starts before the player begins moving.")]
    [Min(0f)]
    public float playerMovementStartDelay = 0f;

    [Header("Game Over")]
    public UnityEvent onGameOver;

    public bool IsRunning { get; private set; }
    public bool IsWaitingToMove { get; private set; }
    public bool IsPaused { get; private set; }

    private Coroutine startDelayRoutine;
    private float timeScaleBeforePause = 1f;
    private readonly HashSet<UnityEngine.Object> pauseBlockSources = new HashSet<UnityEngine.Object>();

    public bool IsPauseBlocked => PruneAndCheckPauseBlocks();

    private void Awake()
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

        ResolvePlayerReference();
        ResolvePauseMenuController();
    }

    private void Start()
    {
        ResolvePlayerReference();
        ResolvePauseMenuController();
        StartGame();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            HandleEscapePressed();
        }
    }

    private void OnDestroy()
    {
        if (Instance != this)
        {
            return;
        }

        if (IsPaused)
        {
            Time.timeScale = timeScaleBeforePause;
        }

        Instance = null;
    }

    public void StartGame()
    {
        ScoreManager.Instance?.ResetScore();
        IsRunning = true;

        if (startDelayRoutine != null)
        {
            StopCoroutine(startDelayRoutine);
        }

        startDelayRoutine = StartCoroutine(StartGameAfterMovementDelay());
    }

    public void TogglePause()
    {
        if (IsPaused)
        {
            ResumeGame();
        }
        else
        {
            PauseGame();
        }
    }

    public void PauseGame()
    {
        if (IsPaused || !IsRunning || IsPauseBlocked)
        {
            return;
        }

        ResolvePauseMenuController();
        if (pauseMenuController == null)
        {
            WarnMissingPauseMenuController();
            return;
        }

        timeScaleBeforePause = Time.timeScale;
        if (timeScaleBeforePause <= 0f)
        {
            timeScaleBeforePause = 1f;
        }

        IsPaused = true;
        Time.timeScale = 0f;
        SetPlayerMovementPaused(true);
        InvokePauseMenuMethod("ShowPauseMenu");
    }

    public void ResumeGame()
    {
        if (!IsPaused)
        {
            return;
        }

        IsPaused = false;
        Time.timeScale = timeScaleBeforePause;
        InvokePauseMenuMethod("HidePauseMenu");

        if (IsRunning && !IsWaitingToMove)
        {
            SetPlayerMovementPaused(false);
        }
    }

    public void SetPauseBlocked(UnityEngine.Object source, bool blocked)
    {
        if (source == null)
        {
            return;
        }

        if (blocked)
        {
            pauseBlockSources.Add(source);
            if (IsPaused)
            {
                ResumeGame();
            }
        }
        else
        {
            pauseBlockSources.Remove(source);
        }
    }

    public void ReturnHome()
    {
        IsPaused = false;
        IsRunning = false;
        IsWaitingToMove = false;
        Time.timeScale = 1f;

        if (startDelayRoutine != null)
        {
            StopCoroutine(startDelayRoutine);
            startDelayRoutine = null;
        }

        SceneManager.LoadScene(HomeSceneName);
    }

    public void TriggerGameOver()
    {
        if (!IsRunning)
        {
            return;
        }

        IsRunning = false;
        IsWaitingToMove = false;
        if (startDelayRoutine != null)
        {
            StopCoroutine(startDelayRoutine);
            startDelayRoutine = null;
        }

        if (IsPaused)
        {
            ResumeGame();
        }

        SetPlayerMovementPaused(true);
        onGameOver?.Invoke();
        Debug.Log("[GameManager] Game Over");
    }

    private IEnumerator StartGameAfterMovementDelay()
    {
        IsWaitingToMove = playerMovementStartDelay > 0f;
        SetPlayerMovementPaused(true);

        if (playerMovementStartDelay > 0f)
        {
            yield return new WaitForSeconds(playerMovementStartDelay);
        }

        while (IsRunning && IsPaused)
        {
            yield return null;
        }

        if (!IsRunning)
        {
            startDelayRoutine = null;
            yield break;
        }

        IsWaitingToMove = false;
        SetPlayerMovementPaused(false);
        startDelayRoutine = null;
    }

    private void HandleEscapePressed()
    {
        if (IsPauseBlocked)
        {
            return;
        }

        ResolvePauseMenuController();
        if (TryHandlePauseMenuEscape())
        {
            return;
        }

        TogglePause();
    }

    private bool PruneAndCheckPauseBlocks()
    {
        pauseBlockSources.RemoveWhere(source => source == null);
        return pauseBlockSources.Count > 0;
    }

    private void SetPlayerMovementPaused(bool paused)
    {
        ResolvePlayerReference();
        if (player != null)
        {
            player.SetMovementPaused(paused);
        }
    }

    private void ResolvePlayerReference()
    {
        if (player != null)
        {
            return;
        }

        GameObject playerObject = FindPlayerObject();
        if (playerObject == null)
        {
            return;
        }

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

    private void ResolvePauseMenuController()
    {
        if (pauseMenuController != null)
        {
            return;
        }

        MonoBehaviour[] candidates = FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < candidates.Length; i++)
        {
            MonoBehaviour candidate = candidates[i];
            if (candidate != null &&
                candidate.GetType().Name == PauseMenuControllerTypeName)
            {
                pauseMenuController = candidate;
                return;
            }
        }
    }

    private bool TryHandlePauseMenuEscape()
    {
        ResolvePauseMenuController();
        if (pauseMenuController == null)
        {
            return false;
        }

        MethodInfo method = pauseMenuController
            .GetType()
            .GetMethod("HandleEscapePressed", BindingFlags.Instance | BindingFlags.Public);

        return method != null &&
               method.Invoke(pauseMenuController, null) is bool handled &&
               handled;
    }

    private void InvokePauseMenuMethod(string methodName)
    {
        if (pauseMenuController == null)
        {
            return;
        }

        MethodInfo method = pauseMenuController
            .GetType()
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);

        method?.Invoke(pauseMenuController, null);
    }

    private void WarnMissingPauseMenuController()
    {
        if (warnedMissingPauseMenuController)
        {
            return;
        }

        Debug.LogWarning(
            "[GameManager] Add PauseMenuController to the scene and assign its pause menu UI references.",
            this);
        warnedMissingPauseMenuController = true;
    }

    private GameObject FindPlayerObject()
    {
        try
        {
            return GameObject.FindGameObjectWithTag(PlayerTag);
        }
        catch (UnityException)
        {
            Debug.LogWarning($"[GameManager] No Unity tag named '{PlayerTag}' exists.", this);
            return null;
        }
    }
}
