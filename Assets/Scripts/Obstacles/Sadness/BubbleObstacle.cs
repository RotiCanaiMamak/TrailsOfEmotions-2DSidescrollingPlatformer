using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class BubbleObstacle : MonoBehaviour, IGroundPoundTarget, IBackpackAbsorbable, IRegulationClearable
{
    [Header("Collision")]
    [SerializeField] private Collider2D bubbleCollider;

    [Header("Ability Drain")]
    [Min(0)] [SerializeField] private int activeChargeDrain = 1;
    [Min(0.01f)] [SerializeField] private float activeChargeDrainInterval = 1f;

    [Header("Emotion")]
    [FormerlySerializedAs("emotionIncreaseOnHit")]
    [Min(0f)] [SerializeField] private float emotionIncreasePerTick = 1f;
    [Min(0.01f)] [SerializeField] private float emotionTickInterval = 0.5f;

    [Header("Burst Effects")]
    [SerializeField] private ObstacleDestroyEffects destroyEffects;
    [Min(0f)] [SerializeField] private float cleanupDelay = 1f;

    [Header("Audio")]
    [SerializeField] private AudioClip obstacleCollisionClip;
    [Range(0f, 1f)] [SerializeField] private float obstacleCollisionVolume = 1f;

    private bool consumed;
    private bool warnedMissingBubbleStatus;

    private void Awake()
    {
        ResolveCollider();
    }

    private void OnValidate()
    {
        activeChargeDrain = Mathf.Max(0, activeChargeDrain);
        activeChargeDrainInterval = Mathf.Max(0.01f, activeChargeDrainInterval);
        emotionIncreasePerTick = Mathf.Max(0f, emotionIncreasePerTick);
        emotionTickInterval = Mathf.Max(0.01f, emotionTickInterval);
        cleanupDelay = Mathf.Max(0f, cleanupDelay);
        obstacleCollisionVolume = Mathf.Clamp01(obstacleCollisionVolume);
        ResolveCollider();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleTouch(collision != null ? collision.collider : null);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        HandleTouch(collision != null ? collision.collider : null);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleTouch(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        HandleTouch(other);
    }

    public bool TryHandleGroundPound(PlayerController player, Vector2 impactPoint)
    {
        if (consumed)
        {
            return false;
        }

        ConsumeAt(transform.position);
        return true;
    }

    public bool TryAbsorbByBackpack(LinaBackpackBulwark bulwark)
    {
        if (consumed)
        {
            return false;
        }

        ConsumeAt(transform.position);
        return true;
    }

    public bool TryClearByRegulation(Object source)
    {
        return TryAbsorbByBackpack(null);
    }

    private void HandleTouch(Collider2D other)
    {
        if (consumed || other == null)
        {
            return;
        }

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null ||
            !player.IsCharacterCollider(other) ||
            player.IsGroundPounding)
        {
            return;
        }

        PlayerBubbleStatus bubbleStatus =
            player.Statuses != null ? player.Statuses.BubbleStatus : null;
        if (bubbleStatus == null)
        {
            WarnMissingBubbleStatus(player);
            return;
        }

        bubbleStatus.TryTrap(
            activeChargeDrain,
            activeChargeDrainInterval,
            emotionIncreasePerTick,
            emotionTickInterval);
        AudioManager.Instance?.PlaySfxOneShot(obstacleCollisionClip, obstacleCollisionVolume);
        ConsumeAt(transform.position);
    }

    private void ConsumeAt(Vector3 effectPosition)
    {
        consumed = true;
        SetBubbleColliderEnabled(false);
        HideBubbleRenderers();
        destroyEffects?.Play(effectPosition);
        Destroy(gameObject, cleanupDelay);
    }

    private void ResolveCollider()
    {
        if (bubbleCollider == null)
        {
            bubbleCollider = GetComponent<Collider2D>();
        }
    }

    private void SetBubbleColliderEnabled(bool enabled)
    {
        if (bubbleCollider != null)
        {
            bubbleCollider.enabled = enabled;
        }
    }

    private void HideBubbleRenderers()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer rendererToHide = renderers[i];
            if (rendererToHide == null || IsDestroyEffectChild(rendererToHide.transform))
            {
                continue;
            }

            rendererToHide.enabled = false;
        }
    }

    private bool IsDestroyEffectChild(Transform candidate)
    {
        return destroyEffects != null && destroyEffects.IsEffectChild(candidate);
    }

    private void WarnMissingBubbleStatus(PlayerController player)
    {
        if (warnedMissingBubbleStatus)
        {
            return;
        }

        Debug.LogWarning(
            "[BubbleObstacle] Assign PlayerBubbleStatus on the player's PlayerStatusManager before using bubble obstacles.",
            player);
        warnedMissingBubbleStatus = true;
    }
}
