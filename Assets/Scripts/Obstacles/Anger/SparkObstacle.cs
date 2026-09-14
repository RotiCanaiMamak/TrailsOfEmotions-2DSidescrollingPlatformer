using UnityEngine;

public class SparkObstacle : MonoBehaviour, IBackpackAbsorbable, IRegulationClearable
{
    [Header("Collision")]
    [SerializeField] private Collider2D sparkCollider;

    [Header("Disruption")]
    [SerializeField] private float verticalVelocityAfterHit = 0f;
    [Min(0f)] [SerializeField] private float adhesionLockDuration = 0.08f;
    [Min(0f)] [SerializeField] private float emotionIncreaseOnHit = 1f;

    [Header("Destroy Effects")]
    [SerializeField] private ObstacleDestroyEffects destroyEffects;

    private bool consumed;

    private void OnValidate()
    {
        adhesionLockDuration = Mathf.Max(0f, adhesionLockDuration);
        emotionIncreaseOnHit = Mathf.Max(0f, emotionIncreaseOnHit);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleTouch(collision.collider);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        HandleTouch(collision.collider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleTouch(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        HandleTouch(other);
    }

    public bool TryAbsorbByBackpack(LinaBackpackBulwark bulwark)
    {
        if (consumed)
        {
            return false;
        }

        Consume();
        return true;
    }

    public bool TryClearByRegulation(Object source)
    {
        return TryAbsorbByBackpack(null);
    }

    internal void HandleTouch(Collider2D other)
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

        player.ApplySparkDisruption(verticalVelocityAfterHit, adhesionLockDuration);
        AddHitEmotion();
        Consume();
    }

    private void AddHitEmotion()
    {
        if (EmotionMeter.Instance != null && emotionIncreaseOnHit > 0f)
        {
            EmotionMeter.Instance.AddObstacleEmotion(emotionIncreaseOnHit);
        }
    }

    private void Consume()
    {
        consumed = true;
        SetSparkColliderEnabled(false);
        HideSparkRenderers();
        PlayDestroyEffects();
    }

    private void SetSparkColliderEnabled(bool enabled)
    {
        if (sparkCollider != null)
        {
            sparkCollider.enabled = enabled;
        }
    }

    private void PlayDestroyEffects()
    {
        destroyEffects?.PlayAtAssignedTransforms();
    }

    private void HideSparkRenderers()
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
}
