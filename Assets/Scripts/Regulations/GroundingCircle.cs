using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GroundingCircle : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Collider2D triggerCollider;

    private GroundingRegulation owner;
    private SpriteRenderer[] spriteRenderers;
    private Color[] baseSpriteColors;
    private Coroutine fadeRoutine;
    private PlayerController holdPlayer;
    private int circleIndex;
    private bool requiresHold;
    private bool completed;
    private float fadeDuration;
    private float holdDuration;
    private float holdProgress;
    private float baseScaleMultiplier = 1f;
    private float heldScaleMultiplier = 1f;
    private Vector3 prefabLocalScale;

    public int CircleIndex => circleIndex;

    private void Awake()
    {
        ResolveReferences();
        CacheVisualState();
    }

    private void OnDisable()
    {
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }
    }

    public void Initialize(
        GroundingRegulation owner,
        int circleIndex,
        bool requiresHold,
        float fadeDuration,
        float holdDuration,
        float baseScaleMultiplier,
        float heldScaleMultiplier)
    {
        this.owner = owner;
        this.circleIndex = circleIndex;
        this.requiresHold = requiresHold;
        this.fadeDuration = Mathf.Max(0f, fadeDuration);
        this.holdDuration = Mathf.Max(0f, holdDuration);
        this.baseScaleMultiplier = Mathf.Max(0f, baseScaleMultiplier);
        this.heldScaleMultiplier = Mathf.Max(this.baseScaleMultiplier, heldScaleMultiplier);
        completed = false;
        holdProgress = 0f;
        holdPlayer = null;

        ResolveReferences();
        CacheVisualState();
        ApplyScale(0f);
        SetAlpha(0f);
        SetTriggerEnabled(true);
        StartFade(1f, false);
    }

    private void Update()
    {
        if (!requiresHold || completed)
        {
            return;
        }

        if (!IsHoldStillValid())
        {
            ResetHold();
            return;
        }

        holdProgress += Time.deltaTime;
        ApplyScale(GetHoldProgress01());
        if (holdProgress >= holdDuration)
        {
            Complete(holdPlayer);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryHandleTouch(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryHandleTouch(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!requiresHold || holdPlayer == null || other == null)
        {
            return;
        }

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == holdPlayer && player.IsCharacterCollider(other))
        {
            ResetHold();
        }
    }

    private void TryHandleTouch(Collider2D other)
    {
        if (completed || other == null)
        {
            return;
        }

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (!IsValidGlidingCharacterTouch(player, other))
        {
            return;
        }

        if (!requiresHold)
        {
            Complete(player);
            return;
        }

        if (holdPlayer != player)
        {
            holdPlayer = player;
            holdProgress = 0f;
        }
    }

    private bool IsValidGlidingCharacterTouch(PlayerController player, Collider2D other)
    {
        return player != null &&
            player.IsGliding &&
            player.IsCharacterCollider(other);
    }

    private bool IsHoldStillValid()
    {
        return holdPlayer != null &&
            holdPlayer.IsGliding &&
            triggerCollider != null &&
            holdPlayer.IsCharacterTouching(triggerCollider, 0f);
    }

    private void Complete(PlayerController player)
    {
        if (completed)
        {
            return;
        }

        completed = true;
        SetTriggerEnabled(false);
        owner?.HandleCircleCompleted(this, player, transform.position);
        StartFade(0f, true);
    }

    private void ResetHold()
    {
        holdPlayer = null;
        holdProgress = 0f;
        ApplyScale(0f);
    }

    private float GetHoldProgress01()
    {
        return holdDuration <= 0f ? 1f : Mathf.Clamp01(holdProgress / holdDuration);
    }

    private void ApplyScale(float holdProgress01)
    {
        transform.localScale = prefabLocalScale *
            Mathf.Lerp(baseScaleMultiplier, heldScaleMultiplier, Mathf.Clamp01(holdProgress01));
    }

    private void StartFade(float targetAlpha, bool destroyAfterFade)
    {
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        fadeRoutine = StartCoroutine(FadeRoutine(Mathf.Clamp01(targetAlpha), destroyAfterFade));
    }

    private IEnumerator FadeRoutine(float targetAlpha, bool destroyAfterFade)
    {
        float startAlpha = GetCurrentAlpha();
        if (fadeDuration <= 0f)
        {
            SetAlpha(targetAlpha);
        }
        else
        {
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                float smoothT = t * t * (3f - 2f * t);
                SetAlpha(Mathf.Lerp(startAlpha, targetAlpha, smoothT));
                yield return null;
            }

            SetAlpha(targetAlpha);
        }

        fadeRoutine = null;
        if (destroyAfterFade)
        {
            Destroy(gameObject);
        }
    }

    private float GetCurrentAlpha()
    {
        if (spriteRenderers != null && spriteRenderers.Length > 0 && spriteRenderers[0] != null)
        {
            return spriteRenderers[0].color.a;
        }

        return 1f;
    }

    private void SetAlpha(float alpha)
    {
        if (spriteRenderers == null || baseSpriteColors == null)
        {
            return;
        }

        float safeAlpha = Mathf.Clamp01(alpha);
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[i];
            if (spriteRenderer == null)
            {
                continue;
            }

            Color color = i < baseSpriteColors.Length ? baseSpriteColors[i] : spriteRenderer.color;
            color.a *= safeAlpha;
            spriteRenderer.color = color;
        }
    }

    private void SetTriggerEnabled(bool enabled)
    {
        if (triggerCollider != null)
        {
            triggerCollider.enabled = enabled;
            triggerCollider.isTrigger = true;
        }
    }

    private void ResolveReferences()
    {
        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<Collider2D>();
        }
    }

    private void CacheVisualState()
    {
        if (prefabLocalScale == Vector3.zero)
        {
            prefabLocalScale = transform.localScale;
        }

        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        baseSpriteColors = new Color[spriteRenderers.Length];
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            baseSpriteColors[i] = spriteRenderers[i] != null ? spriteRenderers[i].color : Color.white;
        }
    }

    private void OnValidate()
    {
        ResolveReferences();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }
}
