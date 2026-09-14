using UnityEngine;

public class SadnessRegulationLandmine : MonoBehaviour, IGroundPoundTarget
{
    private const string LandmineShockwaveManagerTypeName = "LandmineShockwaveManager";

    [Header("Trigger")]
    [SerializeField] private Collider2D triggerCollider;

    [Header("Bounce")]
    [SerializeField] private Transform bounceRoot;
    [Min(0f)] [SerializeField] private float bounceDuration = 0.22f;
    [Min(0f)] [SerializeField] private float bounceHeight = 0.35f;
    [SerializeField] private Vector2 peakScale = new Vector2(1.08f, 0.9f);
    [SerializeField] private AnimationCurve bounceCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 0f);

    [Header("Audio")]
    [SerializeField] private AudioClip groundPoundClip;
    [Range(0f, 1f)] [SerializeField] private float groundPoundVolume = 1f;

    private SadnessRegulation owner;
    private int mineIndex;
    private bool triggered;
    private Coroutine bounceRoutine;
    private MonoBehaviour shockwaveManager;
    private bool warnedMissingShockwaveManager;

    public int MineIndex => mineIndex;

    private void OnValidate()
    {
        bounceDuration = Mathf.Max(0f, bounceDuration);
        bounceHeight = Mathf.Max(0f, bounceHeight);
        peakScale.x = Mathf.Max(0f, peakScale.x);
        peakScale.y = Mathf.Max(0f, peakScale.y);
        groundPoundVolume = Mathf.Clamp01(groundPoundVolume);
    }

    public void Initialize(SadnessRegulation owner, int mineIndex)
    {
        this.owner = owner;
        this.mineIndex = mineIndex;
        triggered = false;

        if (triggerCollider != null)
        {
            triggerCollider.enabled = true;
            triggerCollider.isTrigger = true;
        }
        else
        {
            Debug.LogWarning($"{nameof(SadnessRegulationLandmine)} needs a trigger collider assigned.", this);
        }
    }

    public bool TryHandleGroundPound(PlayerController player, Vector2 impactPoint)
    {
        if (triggered || owner == null)
        {
            return false;
        }

        triggered = true;
        if (triggerCollider != null)
        {
            triggerCollider.enabled = false;
        }

        PlayBounce();
        PlayShockwave(impactPoint);
        AudioManager.Instance?.PlaySfxOneShot(groundPoundClip, groundPoundVolume);
        owner.TriggerLandmine(this, player, impactPoint);
        return true;
    }

    private void PlayBounce()
    {
        if (bounceRoot == null || bounceDuration <= 0f)
        {
            return;
        }

        if (bounceRoutine != null)
        {
            StopCoroutine(bounceRoutine);
        }

        bounceRoutine = StartCoroutine(BounceRoutine());
    }

    private System.Collections.IEnumerator BounceRoutine()
    {
        Vector3 startLocalPosition = bounceRoot.localPosition;
        Vector3 startLocalScale = bounceRoot.localScale;
        Vector3 peakLocalScale = new Vector3(
            startLocalScale.x * peakScale.x,
            startLocalScale.y * peakScale.y,
            startLocalScale.z);

        float elapsed = 0f;
        while (elapsed < bounceDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / bounceDuration);
            float bounce = bounceCurve != null ? bounceCurve.Evaluate(t) : Mathf.Sin(t * Mathf.PI);
            float scaleT = Mathf.Sin(t * Mathf.PI);

            bounceRoot.localPosition = startLocalPosition + Vector3.up * bounceHeight * bounce;
            bounceRoot.localScale = Vector3.Lerp(startLocalScale, peakLocalScale, scaleT);
            yield return null;
        }

        bounceRoot.localPosition = startLocalPosition;
        bounceRoot.localScale = startLocalScale;
        bounceRoutine = null;
    }

    private void PlayShockwave(Vector2 impactPoint)
    {
        MonoBehaviour manager = ResolveShockwaveManager();
        if (manager != null)
        {
            manager.SendMessage("PlayAt", impactPoint, SendMessageOptions.DontRequireReceiver);
            return;
        }

        if (warnedMissingShockwaveManager)
        {
            return;
        }

        Debug.LogWarning(
            $"[{nameof(SadnessRegulationLandmine)}] No {LandmineShockwaveManagerTypeName} found in the scene.",
            this);
        warnedMissingShockwaveManager = true;
    }

    private MonoBehaviour ResolveShockwaveManager()
    {
        if (shockwaveManager != null)
        {
            return shockwaveManager;
        }

        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour != null && behaviour.GetType().Name == LandmineShockwaveManagerTypeName)
            {
                shockwaveManager = behaviour;
                return shockwaveManager;
            }
        }

        return null;
    }
}
