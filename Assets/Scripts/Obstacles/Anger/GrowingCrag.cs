using System.Collections;
using UnityEngine;

public class GrowingCrag : MonoBehaviour, IGroundPoundTarget, IBackpackAbsorbable, IRegulationClearable
{
    [Header("Markers")]
    [SerializeField] private Transform topMarker;
    [SerializeField] private Transform bottomMarker;
    [SerializeField] private Collider2D cragCollider;

    [Header("Movement")]
    [Min(0f)] public float riseDuration = 0.6f;
    [SerializeField] private AnimationCurve riseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Normal Hit")]
    [Min(0f)] public float recoilHorizontalSpeed = 12f;
    public float recoilMaxVerticalVelocity = 0f;
    [Min(0f)] public float recoilLockDuration = 0.18f;
    [Min(0f)] [SerializeField] private float emotionIncreaseOnHit = 1f;

    [Header("Destroy Effects")]
    [SerializeField] private ObstacleDestroyEffects destroyEffects;

    private Vector3 hiddenRootPosition;
    private Vector3 raisedRootPosition;
    private Coroutine movementRoutine;
    private bool positionsCached;
    private bool cracked;

    private void Start()
    {
        if (!TryCachePositions())
        {
            enabled = false;
            return;
        }

        movementRoutine = StartCoroutine(MoveRoot(transform.position, raisedRootPosition, riseDuration, riseCurve));
    }

    private void OnValidate()
    {
        riseDuration = Mathf.Max(0f, riseDuration);
        recoilHorizontalSpeed = Mathf.Max(0f, recoilHorizontalSpeed);
        recoilLockDuration = Mathf.Max(0f, recoilLockDuration);
        emotionIncreaseOnHit = Mathf.Max(0f, emotionIncreaseOnHit);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleNormalCollision(collision.collider);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        HandleNormalCollision(collision.collider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleNormalCollision(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        HandleNormalCollision(other);
    }

    public bool TryCrackFromGroundPound(PlayerController player, Vector2 impactPoint)
    {
        if (cracked)
        {
            return false;
        }

        cracked = true;
        SetCragColliderEnabled(false);
        HideCragRenderers();
        PlayDestroyEffects(impactPoint);

        if (movementRoutine != null)
        {
            StopCoroutine(movementRoutine);
        }

        movementRoutine = null;
        return true;
    }

    public bool TryHandleGroundPound(PlayerController player, Vector2 impactPoint)
    {
        return TryCrackFromGroundPound(player, impactPoint);
    }

    public bool TryAbsorbByBackpack(LinaBackpackBulwark bulwark)
    {
        if (cracked)
        {
            return false;
        }

        cracked = true;
        SetCragColliderEnabled(false);
        HideCragRenderers();
        PlayDestroyEffects(bulwark != null ? bulwark.transform.position : transform.position);

        if (movementRoutine != null)
        {
            StopCoroutine(movementRoutine);
        }

        movementRoutine = null;
        return true;
    }

    public bool TryClearByRegulation(Object source)
    {
        return TryAbsorbByBackpack(null);
    }

    internal void HandleNormalCollision(Collider2D other)
    {
        if (cracked || other == null)
        {
            return;
        }

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || !player.IsCharacterCollider(other) || player.IsGroundPounding)
        {
            return;
        }

        cracked = true;
        Vector2 recoilDirection = player.transform.position - transform.position;
        player.ApplyObstacleRecoil(
            recoilDirection,
            recoilHorizontalSpeed,
            recoilMaxVerticalVelocity,
            recoilLockDuration);
        AddHitEmotion();
        SetCragColliderEnabled(false);
        HideCragRenderers();

        if (movementRoutine != null)
        {
            StopCoroutine(movementRoutine);
        }

        movementRoutine = null;
        PlayDestroyEffects(player.transform.position);
    }

    private void AddHitEmotion()
    {
        if (EmotionMeter.Instance != null && emotionIncreaseOnHit > 0f)
        {
            EmotionMeter.Instance.AddObstacleEmotion(emotionIncreaseOnHit);
        }
    }

    private bool TryCachePositions()
    {
        if (positionsCached)
        {
            return true;
        }

        if (topMarker == null || bottomMarker == null)
        {
            Debug.LogWarning($"[{nameof(GrowingCrag)}] Missing top or bottom marker on {name}.", this);
            return false;
        }

        hiddenRootPosition = transform.position;
        raisedRootPosition = hiddenRootPosition + (topMarker.position - bottomMarker.position);
        positionsCached = true;
        return true;
    }

    private IEnumerator MoveRoot(Vector3 from, Vector3 to, float duration, AnimationCurve curve)
    {
        if (duration <= 0f)
        {
            transform.position = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = curve != null ? curve.Evaluate(t) : t;
            transform.position = Vector3.LerpUnclamped(from, to, eased);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = to;
    }

    private void PlayDestroyEffects(Vector3 effectPosition)
    {
        if (topMarker != null)
        {
            effectPosition = topMarker.position;
        }

        destroyEffects?.Play(effectPosition);
    }

    private void SetCragColliderEnabled(bool isEnabled)
    {
        if (cragCollider != null)
        {
            cragCollider.enabled = isEnabled;
        }
    }

    private void HideCragRenderers()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer rendererToHide = renderers[i];
            if (rendererToHide == null ||
                (destroyEffects != null && destroyEffects.IsEffectChild(rendererToHide.transform)))
            {
                continue;
            }

            rendererToHide.enabled = false;
        }
    }

}
