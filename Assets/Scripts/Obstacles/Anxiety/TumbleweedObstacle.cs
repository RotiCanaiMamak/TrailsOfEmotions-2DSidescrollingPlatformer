using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class TumbleweedObstacle : MonoBehaviour
{
    public enum TumbleweedVariant
    {
        Solid,
        HalfTransparent
    }

    [Header("Variant")]
    [SerializeField] private TumbleweedVariant variant = TumbleweedVariant.Solid;

    [Header("Collision")]
    [SerializeField] private Collider2D tumbleweedCollider;

    [Header("Movement")]
    [Min(0f)] [SerializeField] private float leftSpeed = 4f;
    [SerializeField] private Transform rollRoot;
    [SerializeField] private float rollSpeed = 360f;

    [Header("Solid Hit")]
    [Range(0f, 1f)] [SerializeField] private float playerSpeedMultiplier = 0.35f;
    [Min(0f)] [SerializeField] private float effectDuration = 1.5f;
    [Min(0f)] [SerializeField] private float emotionIncreaseOnHit;

    [Header("Half Transparent")]
    [Range(0f, 1f)] [SerializeField] private float halfTransparentAlpha = 0.5f;
    [SerializeField] private SpriteRenderer[] alphaRenderers;

    [Header("Variant Collision Effects")]
    [Tooltip("Particle/audio effects played only when the solid tumbleweed hits the player.")]
    [SerializeField] private ObstacleDestroyEffects solidHitEffects;
    [Tooltip("Particle/audio effects played only when the half-transparent tumbleweed disappears on player touch.")]
    [SerializeField] private ObstacleDestroyEffects halfTransparentDisappearEffects;

    private bool consumed;
    private bool warnedMissingStatus;

    public TumbleweedVariant Variant => variant;
    public float LeftSpeed => leftSpeed;

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
        ApplyVariantVisuals();
    }

    private void OnEnable()
    {
        consumed = false;
        SetColliderEnabled(true);
        SetRenderersEnabled(true);
        ApplyVariantVisuals();
    }

    private void OnValidate()
    {
        leftSpeed = Mathf.Max(0f, leftSpeed);
        playerSpeedMultiplier = Mathf.Clamp01(playerSpeedMultiplier);
        effectDuration = Mathf.Max(0f, effectDuration);
        emotionIncreaseOnHit = Mathf.Max(0f, emotionIncreaseOnHit);
        halfTransparentAlpha = Mathf.Clamp01(halfTransparentAlpha);
        ResolveReferences();
        ApplyVariantVisuals();
    }

    private void Update()
    {
        if (consumed)
        {
            return;
        }

        float deltaTime = Time.deltaTime;
        transform.position += Vector3.left * leftSpeed * deltaTime;
        Roll(deltaTime);
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

    public void ConfigureVariant(TumbleweedVariant selectedVariant)
    {
        variant = selectedVariant;
        ApplyVariantVisuals();
    }

    public void SetRuntimeSpeed(float speed)
    {
        leftSpeed = Mathf.Max(0f, speed);
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

        if (variant == TumbleweedVariant.HalfTransparent)
        {
            HandleHalfTransparentTouch();
            return;
        }

        HandleSolidTouch(player);
    }

    private void HandleSolidTouch(PlayerController player)
    {
        ApplySolidHit(player);
        AddHitEmotion();
        ConsumeWithVariantEffects(TumbleweedVariant.Solid);
    }

    private void HandleHalfTransparentTouch()
    {
        ConsumeWithVariantEffects(TumbleweedVariant.HalfTransparent);
    }

    private void ApplySolidHit(PlayerController player)
    {
        PlayerTumbleweedStatus status =
            player.Statuses != null ? player.Statuses.TumbleweedStatus : null;
        if (status != null)
        {
            status.RefreshTumbleweedHit(playerSpeedMultiplier, effectDuration);
            return;
        }

        WarnMissingStatus(player);
    }

    private void AddHitEmotion()
    {
        if (EmotionMeter.Instance != null && emotionIncreaseOnHit > 0f)
        {
            EmotionMeter.Instance.AddObstacleEmotion(emotionIncreaseOnHit);
        }
    }

    private void ConsumeWithVariantEffects(TumbleweedVariant consumedVariant)
    {
        consumed = true;
        SetColliderEnabled(false);
        SetRenderersEnabled(false);
        ObstacleDestroyEffects effects = GetCollisionEffects(consumedVariant);
        effects?.PlayAtAssignedTransforms();
    }

    private ObstacleDestroyEffects GetCollisionEffects(TumbleweedVariant effectVariant)
    {
        return effectVariant == TumbleweedVariant.HalfTransparent
            ? halfTransparentDisappearEffects
            : solidHitEffects;
    }

    private void Roll(float deltaTime)
    {
        Transform target = rollRoot != null ? rollRoot : transform;
        target.Rotate(0f, 0f, rollSpeed * deltaTime);
    }

    private void ApplyVariantVisuals()
    {
        SpriteRenderer[] renderers = GetAlphaRenderers();
        float alpha = variant == TumbleweedVariant.HalfTransparent
            ? halfTransparentAlpha
            : 1f;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer targetRenderer = renderers[i];
            if (targetRenderer == null || IsEffectChild(targetRenderer.transform))
            {
                continue;
            }

            Color color = targetRenderer.color;
            color.a = alpha;
            targetRenderer.color = color;
        }
    }

    private SpriteRenderer[] GetAlphaRenderers()
    {
        if (alphaRenderers != null && alphaRenderers.Length > 0)
        {
            return alphaRenderers;
        }

        return GetComponentsInChildren<SpriteRenderer>(true);
    }

    private void SetColliderEnabled(bool enabled)
    {
        if (tumbleweedCollider != null)
        {
            tumbleweedCollider.enabled = enabled;
        }
    }

    private void SetRenderersEnabled(bool enabled)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer targetRenderer = renderers[i];
            if (targetRenderer == null || IsEffectChild(targetRenderer.transform))
            {
                continue;
            }

            targetRenderer.enabled = enabled;
        }
    }

    private bool IsEffectChild(Transform candidate)
    {
        return (solidHitEffects != null && solidHitEffects.IsEffectChild(candidate)) ||
            (halfTransparentDisappearEffects != null && halfTransparentDisappearEffects.IsEffectChild(candidate));
    }

    private void ResolveReferences()
    {
        if (tumbleweedCollider == null)
        {
            tumbleweedCollider = GetComponent<Collider2D>();
        }
    }

    private void WarnMissingStatus(PlayerController player)
    {
        if (warnedMissingStatus)
        {
            return;
        }

        Debug.LogWarning(
            "[TumbleweedObstacle] Assign PlayerTumbleweedStatus on the player's PlayerStatusManager before using solid tumbleweeds.",
            player);
        warnedMissingStatus = true;
    }
}
