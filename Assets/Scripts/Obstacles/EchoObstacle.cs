using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class EchoObstacle : MonoBehaviour
{
    [Header("Collision")]
    [SerializeField] private Collider2D echoCollider;

    [Header("Emotion")]
    [Min(0f)] [SerializeField] private float emotionIncreaseOnHit;

    [Header("Destroy Effects")]
    [SerializeField] private ObstacleDestroyEffects destroyEffects;
    [Min(0f)] [SerializeField] private float cleanupDelay = 1f;

    [Header("Audio")]
    [SerializeField] private AudioClip obstacleCollisionClip;
    [Range(0f, 1f)] [SerializeField] private float obstacleCollisionVolume = 1f;

    private bool consumed;
    private bool warnedMissingStatus;

    private void Reset()
    {
        ResolveReferences();
        ConfigureCollider();
    }

    private void Awake()
    {
        ResolveReferences();
        ConfigureCollider();
    }

    private void OnEnable()
    {
        consumed = false;
        SetColliderEnabled(true);
        SetRenderersEnabled(true);
    }

    private void OnValidate()
    {
        emotionIncreaseOnHit = Mathf.Max(0f, emotionIncreaseOnHit);
        cleanupDelay = Mathf.Max(0f, cleanupDelay);
        obstacleCollisionVolume = Mathf.Clamp01(obstacleCollisionVolume);
        ResolveReferences();
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

        PlayerEchoInputDelayStatus status =
            player.Statuses != null ? player.Statuses.EchoInputDelayStatus : null;
        if (status == null)
        {
            WarnMissingStatus(player);
            return;
        }

        if (!status.TryRefreshEchoEffect())
        {
            return;
        }

        AddHitEmotion();
        AudioManager.Instance?.PlaySfxOneShot(obstacleCollisionClip, obstacleCollisionVolume);
        ConsumeAt(transform.position);
    }

    private void AddHitEmotion()
    {
        if (EmotionMeter.Instance != null && emotionIncreaseOnHit > 0f)
        {
            EmotionMeter.Instance.AddObstacleEmotion(emotionIncreaseOnHit);
        }
    }

    private void ConsumeAt(Vector3 effectPosition)
    {
        consumed = true;
        SetColliderEnabled(false);
        SetRenderersEnabled(false);
        destroyEffects?.Play(effectPosition);

        if (cleanupDelay > 0f)
        {
            Destroy(gameObject, cleanupDelay);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void ResolveReferences()
    {
        if (echoCollider == null)
        {
            echoCollider = GetComponent<Collider2D>();
        }

        if (destroyEffects == null)
        {
            destroyEffects = GetComponentInChildren<ObstacleDestroyEffects>(true);
        }
    }

    private void ConfigureCollider()
    {
        if (echoCollider != null)
        {
            echoCollider.isTrigger = true;
        }
    }

    private void SetColliderEnabled(bool enabled)
    {
        if (echoCollider != null)
        {
            echoCollider.enabled = enabled;
        }
    }

    private void SetRenderersEnabled(bool enabled)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer targetRenderer = renderers[i];
            if (targetRenderer == null || IsDestroyEffectChild(targetRenderer.transform))
            {
                continue;
            }

            targetRenderer.enabled = enabled;
        }
    }

    private bool IsDestroyEffectChild(Transform candidate)
    {
        return destroyEffects != null && destroyEffects.IsEffectChild(candidate);
    }

    private void WarnMissingStatus(PlayerController player)
    {
        if (warnedMissingStatus)
        {
            return;
        }

        Debug.LogWarning(
            "[EchoObstacle] Assign PlayerEchoInputDelayStatus on the player's PlayerStatusManager before using Echo obstacles.",
            player);
        warnedMissingStatus = true;
    }
}
