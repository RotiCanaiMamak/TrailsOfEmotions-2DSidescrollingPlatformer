using UnityEngine;

public class NoisyLeftMover : MonoBehaviour
{
    private const string PlayerTag = "Player";

    [Header("Movement")]
    [Min(0f)] [SerializeField] private float leftSpeed = 2f;

    [Header("Player Tracking")]
    [SerializeField] private bool trackPlayerWhileApproaching;
    [Min(0f)] [SerializeField] private float verticalTrackingSpeed = 2f;

    [Header("Spawn Height")]
    [Min(0f)] [SerializeField] private float minSpawnHeight;
    [Min(0f)] [SerializeField] private float maxSpawnHeight;

    [Header("Noise")]
    [Min(0f)] [SerializeField] private float verticalNoiseAmplitude = 0.5f;
    [Min(0f)] [SerializeField] private float verticalNoiseFrequency = 1f;

    private Vector3 originLocalPosition;
    private float elapsed;
    private float noiseSeed;
    private float verticalCenterLocalY;
    private bool shouldCaptureOrigin;
    private Transform player;
    private bool warnedMissingPlayerTag;

    public bool SpawnsAboveSurface => maxSpawnHeight > 0f;

    private void OnEnable()
    {
        elapsed = 0f;
        noiseSeed = Random.value * 1000f;
        shouldCaptureOrigin = true;
        player = null;
    }

    private void OnValidate()
    {
        leftSpeed = Mathf.Max(0f, leftSpeed);
        verticalTrackingSpeed = Mathf.Max(0f, verticalTrackingSpeed);
        minSpawnHeight = Mathf.Max(0f, minSpawnHeight);
        maxSpawnHeight = Mathf.Max(minSpawnHeight, maxSpawnHeight);
        verticalNoiseAmplitude = Mathf.Max(0f, verticalNoiseAmplitude);
        verticalNoiseFrequency = Mathf.Max(0f, verticalNoiseFrequency);
    }

    public void PlaceAboveSurface(Vector3 groundedPosition)
    {
        minSpawnHeight = Mathf.Max(0f, minSpawnHeight);
        maxSpawnHeight = Mathf.Max(minSpawnHeight, maxSpawnHeight);

        float height = Random.Range(minSpawnHeight, maxSpawnHeight);
        transform.position = groundedPosition + Vector3.up * height;
    }

    private void Update()
    {
        if (shouldCaptureOrigin)
        {
            originLocalPosition = transform.localPosition;
            verticalCenterLocalY = originLocalPosition.y;
            shouldCaptureOrigin = false;
        }

        elapsed += Time.deltaTime;
        UpdatePlayerTracking();

        Vector3 nextPosition = originLocalPosition;
        nextPosition.x -= leftSpeed * elapsed;
        nextPosition.y = verticalCenterLocalY + GetVerticalNoiseOffset();
        transform.localPosition = nextPosition;
    }

    private void UpdatePlayerTracking()
    {
        if (!trackPlayerWhileApproaching)
        {
            return;
        }

        ResolvePlayer();
        if (player == null || transform.position.x <= player.position.x)
        {
            return;
        }

        Vector3 playerLocalPosition = transform.parent != null
            ? transform.parent.InverseTransformPoint(player.position)
            : player.position;
        verticalCenterLocalY = Mathf.MoveTowards(
            verticalCenterLocalY,
            playerLocalPosition.y,
            verticalTrackingSpeed * Time.deltaTime);
    }

    private void ResolvePlayer()
    {
        if (player != null)
        {
            return;
        }

        try
        {
            GameObject playerObject =
                GameObject.FindGameObjectWithTag(PlayerTag);
            player = playerObject != null
                ? playerObject.transform
                : null;
        }
        catch (UnityException)
        {
            if (warnedMissingPlayerTag)
            {
                return;
            }

            Debug.LogWarning(
                $"[NoisyLeftMover] No Unity tag named '{PlayerTag}' exists.",
                this);
            warnedMissingPlayerTag = true;
        }
    }

    private float GetVerticalNoiseOffset()
    {
        if (verticalNoiseAmplitude <= 0f || verticalNoiseFrequency <= 0f)
        {
            return 0f;
        }

        float noise = Mathf.PerlinNoise(noiseSeed, elapsed * verticalNoiseFrequency);
        return (noise - 0.5f) * 2f * verticalNoiseAmplitude;
    }
}
