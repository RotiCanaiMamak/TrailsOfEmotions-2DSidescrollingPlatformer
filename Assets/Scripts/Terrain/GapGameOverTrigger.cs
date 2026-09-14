using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class GapGameOverTrigger : MonoBehaviour
{
    [Header("Collision")]
    [SerializeField] private Collider2D gapCollider;

    private bool consumed;
    private bool warnedMissingCameraFollow;
    private bool warnedMissingGameOverCutsceneController;
    private bool warnedMissingGameManager;

    private void Awake()
    {
        ResolveCollider();
        ConfigureCollider();
    }

    private void OnValidate()
    {
        ResolveCollider();
        ConfigureCollider();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleTouch(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        HandleTouch(other);
    }

    private void HandleTouch(Collider2D other)
    {
        if (consumed || other == null)
        {
            return;
        }

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || !player.IsCharacterCollider(other))
        {
            return;
        }

        consumed = true;
        SetGapColliderEnabled(false);
        LockCamera();
        StartGameOverAfterFade();
    }

    private void LockCamera()
    {
        Camera mainCamera = Camera.main;
        CameraFollow cameraFollow = mainCamera != null ? mainCamera.GetComponent<CameraFollow>() : null;
        if (cameraFollow == null)
        {
            WarnMissingCameraFollow();
            return;
        }

        cameraFollow.SetFollowLocked(this, true);
    }

    private void StartGameOverAfterFade()
    {
        GameOverCutsceneController cutsceneController =
            FindFirstObjectByType<GameOverCutsceneController>();
        if (cutsceneController == null)
        {
            WarnMissingGameOverCutsceneController();
            TriggerGameOverFallback();
            return;
        }

        cutsceneController.StartGameOverAfterFadeOnly();
    }

    private void TriggerGameOverFallback()
    {
        if (GameManager.Instance == null)
        {
            WarnMissingGameManager();
            return;
        }

        GameManager.Instance.TriggerGameOver();
    }

    private void ResolveCollider()
    {
        if (gapCollider == null)
        {
            gapCollider = GetComponent<Collider2D>();
        }
    }

    private void ConfigureCollider()
    {
        if (gapCollider != null)
        {
            gapCollider.isTrigger = true;
        }
    }

    private void SetGapColliderEnabled(bool enabled)
    {
        if (gapCollider != null)
        {
            gapCollider.enabled = enabled;
        }
    }

    private void WarnMissingGameManager()
    {
        if (warnedMissingGameManager)
        {
            return;
        }

        Debug.LogWarning(
            "[GapGameOverTrigger] No GameManager found; camera was locked but game over could not trigger.",
            this);
        warnedMissingGameManager = true;
    }

    private void WarnMissingCameraFollow()
    {
        if (warnedMissingCameraFollow)
        {
            return;
        }

        Debug.LogWarning(
            "[GapGameOverTrigger] No CameraFollow found on Camera.main; camera could not be locked before game over.",
            this);
        warnedMissingCameraFollow = true;
    }

    private void WarnMissingGameOverCutsceneController()
    {
        if (warnedMissingGameOverCutsceneController)
        {
            return;
        }

        Debug.LogWarning(
            "[GapGameOverTrigger] No GameOverCutsceneController found; falling back to immediate game over.",
            this);
        warnedMissingGameOverCutsceneController = true;
    }
}
