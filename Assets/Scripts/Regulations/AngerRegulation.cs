using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(PolygonCollider2D))]
public class AngerRegulation : MonoBehaviour, IRegulationSpawnPlacement
{
    [Header("Spawn Placement")]
    public float heightAbovePeak = 0f;
    [Min(0f)] public float slidePeakRightOffset = 3f;

    [Header("Wave Shape")]
    [Min(0.1f)] public float length = 8f;
    [Min(0f)] public float amplitude = 1.2f;
    [Min(0.1f)] public float cycles = 2f;
    [Min(0.1f)] public float waveGap = 2.8f;
    [Min(0f)] public float colliderPadding = 0.6f;
    [Range(8, 128)] public int samples = 48;

    [Header("Sparkles")]
    [SerializeField] private ParticleSystem sparkleParticles;
    [FormerlySerializedAs("sparkleCount")]
    [Range(8, 256)] public int sparklesPerWave = 64;
    [Min(0f)] public float sparkleJitter = 0.08f;
    [Min(0f)] public float sparklePulseSpeed = 4f;

    [Header("Emotion")]
    [Min(0f)] public float emotionReductionPerSecond = 12f;

    [Header("Movement Modifiers")]
    [Range(0f, 1f)] public float horizontalSpeedMultiplier = 0.65f;
    [Min(0f)]
    [Tooltip("Seconds it takes airborne horizontal speed to lerp to the reduced speed inside AngerRegulation.")]
    public float horizontalSpeedLerpDuration = 0.75f;
    [Tooltip("Gravity multiplier applied while airborne inside the wave, including the released descent.")]
    [FormerlySerializedAs("glideAccelerationMultiplier")]
    [Range(0f, 1f)] public float glideGravityMultiplier = 0.55f;

    [Header("Breathing Lift")]
    [Min(0f)] public float waveLiftAcceleration = 18f;
    [Min(0f)] public float maxWaveLiftSpeed = 10f;

    private readonly HashSet<PlayerController> playersInside = new HashSet<PlayerController>();
    private readonly Dictionary<PlayerController, int> playerColliderCounts = new Dictionary<PlayerController, int>();
    private PolygonCollider2D waveCollider;
    private ParticleSystem.Particle[] particles;
    private Vector2[] centerCurvePoints;
    private bool warnedMissingAngerRegulationStatus;

    private void Awake()
    {
        BuildWave();
    }

    private void OnEnable()
    {
        BuildWave();
    }

    private void LateUpdate()
    {
        RefreshSparkles();
    }

    private void OnDisable()
    {
        foreach (PlayerController player in playersInside)
        {
            if (player != null)
            {
                player.RemoveRegulationMovementModifier(this);
                NotifyPlayerExitedRegulation(player);
            }
        }

        playersInside.Clear();
        playerColliderCounts.Clear();
    }

    private void OnValidate()
    {
        length = Mathf.Max(0.1f, length);
        slidePeakRightOffset = Mathf.Max(0f, slidePeakRightOffset);
        amplitude = Mathf.Max(0f, amplitude);
        cycles = Mathf.Max(0.1f, cycles);
        waveGap = Mathf.Max(0.1f, waveGap);
        colliderPadding = Mathf.Max(0f, colliderPadding);
        samples = Mathf.Clamp(samples, 8, 128);
        sparklesPerWave = Mathf.Clamp(sparklesPerWave, 8, 256);
        sparkleJitter = Mathf.Max(0f, sparkleJitter);
        sparklePulseSpeed = Mathf.Max(0f, sparklePulseSpeed);
        horizontalSpeedMultiplier = Mathf.Clamp01(horizontalSpeedMultiplier);
        horizontalSpeedLerpDuration = Mathf.Max(0f, horizontalSpeedLerpDuration);
        waveLiftAcceleration = Mathf.Max(0f, waveLiftAcceleration);
        maxWaveLiftSpeed = Mathf.Max(0f, maxWaveLiftSpeed);

        if (Application.isPlaying)
        {
            BuildWave();
        }
    }

    public bool PlaceAfterSlidePeak(TerrainChunk chunk)
    {
        return PlaceOnChunk(chunk);
    }

    public bool PlaceOnChunk(TerrainChunk chunk)
    {
        if (chunk == null)
        {
            return false;
        }

        float peakX = chunk.transform.position.x;
        float peakY = chunk.startY;
        transform.position = new Vector3(
            peakX + slidePeakRightOffset,
            peakY + heightAbovePeak,
            transform.position.z);
        transform.rotation = Quaternion.identity;
        return true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null)
        {
            return;
        }

        RegisterPlayerCollider(player);
        ApplyModifier(player);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null)
        {
            return;
        }

        if (!playersInside.Contains(player))
        {
            RegisterRecoveredPlayer(player);
        }

        ApplyModifier(player);

        if (!player.IsAirborne)
        {
            return;
        }

        if (player.IsGlideInputHeld)
        {
            player.ApplyRegulationLift(waveLiftAcceleration, maxWaveLiftSpeed);
        }

        if (EmotionMeter.Instance != null)
        {
            EmotionMeter.Instance.AddEmotion(-emotionReductionPerSecond * Time.fixedDeltaTime);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null)
        {
            return;
        }

        UnregisterPlayerCollider(player);
    }

    private void BuildWave()
    {
        EnsureCollider();
        EnsureParticles();
        BuildCurvePoints();
        BuildColliderPath();
        RefreshSparkles();
    }

    private void EnsureCollider()
    {
        if (waveCollider == null)
        {
            waveCollider = GetComponent<PolygonCollider2D>();
        }

        waveCollider.isTrigger = true;
    }

    private void EnsureParticles()
    {
        if (sparkleParticles == null)
        {
            sparkleParticles = GetComponentInChildren<ParticleSystem>();
        }

        if (sparkleParticles == null)
        {
            GameObject particlesObject = new GameObject("Regulation Sparkles");
            particlesObject.transform.SetParent(transform, false);
            sparkleParticles = particlesObject.AddComponent<ParticleSystem>();
        }

        ParticleSystem.MainModule main = sparkleParticles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = GetParticleCount();
        main.startLifetime = 1f;
        main.startSpeed = 0f;

        ParticleSystem.EmissionModule emission = sparkleParticles.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = sparkleParticles.shape;
        shape.enabled = false;

        ParticleSystemRenderer renderer = sparkleParticles.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = 20;
        }

        int particleCount = GetParticleCount();
        if (particles == null || particles.Length != particleCount)
        {
            particles = new ParticleSystem.Particle[particleCount];
        }
    }

    private void BuildCurvePoints()
    {
        if (centerCurvePoints == null || centerCurvePoints.Length != samples)
        {
            centerCurvePoints = new Vector2[samples];
        }

        for (int i = 0; i < samples; i++)
        {
            float t = samples == 1 ? 0f : i / (samples - 1f);
            centerCurvePoints[i] = EvaluateCenterWave(t);
        }
    }

    private void BuildColliderPath()
    {
        if (waveCollider == null || centerCurvePoints == null || centerCurvePoints.Length < 2)
        {
            return;
        }

        Vector2[] path = new Vector2[centerCurvePoints.Length * 2];
        float halfGap = waveGap * 0.5f;
        float outerOffset = halfGap + colliderPadding;

        for (int i = 0; i < centerCurvePoints.Length; i++)
        {
            Vector2 center = centerCurvePoints[i];
            path[i] = center + Vector2.up * outerOffset;
            path[path.Length - 1 - i] = center - Vector2.up * outerOffset;
        }

        waveCollider.pathCount = 1;
        waveCollider.SetPath(0, path);
    }

    private void RefreshSparkles()
    {
        if (sparkleParticles == null || centerCurvePoints == null || centerCurvePoints.Length == 0)
        {
            return;
        }

        int particleCount = GetParticleCount();
        if (particles == null || particles.Length != particleCount)
        {
            particles = new ParticleSystem.Particle[particleCount];
        }

        ParticleSystem.MainModule main = sparkleParticles.main;
        float time = Application.isPlaying ? Time.time : 0f;
        float halfGap = waveGap * 0.5f;

        for (int i = 0; i < sparklesPerWave; i++)
        {
            float t = sparklesPerWave == 1 ? 0f : i / (sparklesPerWave - 1f);
            Vector2 centerPoint = EvaluateCenterWave(t);

            ConfigureParticle(
                i,
                centerPoint + Vector2.up * halfGap,
                t,
                time,
                main);

            ConfigureParticle(
                i + sparklesPerWave,
                centerPoint - Vector2.up * halfGap,
                t,
                time + 0.31f,
                main);
        }

        sparkleParticles.SetParticles(particles, particles.Length);
    }

    private void ConfigureParticle(
        int particleIndex,
        Vector2 point,
        float normalizedPosition,
        float time,
        ParticleSystem.MainModule main)
    {
        float jitterX = Mathf.Sin(particleIndex * 12.9898f) * sparkleJitter;
        float jitterY = Mathf.Cos(particleIndex * 78.233f) * sparkleJitter;
        float pulse = (Mathf.Sin(time * sparklePulseSpeed + particleIndex * 0.73f) + 1f) * 0.5f;

        particles[particleIndex].position = new Vector3(point.x + jitterX, point.y + jitterY, 0f);
        particles[particleIndex].startColor = main.startColor.Evaluate(normalizedPosition, pulse);
        particles[particleIndex].startSize = main.startSize.Evaluate(normalizedPosition, pulse);
        particles[particleIndex].startLifetime = 1f;
        particles[particleIndex].remainingLifetime = 1f;
        particles[particleIndex].velocity = Vector3.zero;
    }

    private Vector2 EvaluateCenterWave(float t)
    {
        float x = Mathf.Lerp(-length * 0.5f, length * 0.5f, t);
        float y = Mathf.Sin(t * cycles * Mathf.PI * 2f) * amplitude;
        return new Vector2(x, y);
    }

    private int GetParticleCount()
    {
        return Mathf.Max(1, sparklesPerWave * 2);
    }

    private void ApplyModifier(PlayerController player)
    {
        player.AddRegulationMovementModifier(
            this,
            horizontalSpeedMultiplier,
            1f,
            glideGravityMultiplier,
            horizontalSpeedLerpDuration);
    }

    private void RegisterPlayerCollider(PlayerController player)
    {
        if (player == null)
        {
            return;
        }

        playerColliderCounts.TryGetValue(player, out int colliderCount);
        playerColliderCounts[player] = colliderCount + 1;

        if (colliderCount > 0)
        {
            return;
        }

        playersInside.Add(player);
        NotifyPlayerEnteredRegulation(player);
    }

    private void RegisterRecoveredPlayer(PlayerController player)
    {
        if (player == null)
        {
            return;
        }

        playerColliderCounts[player] = Mathf.Max(1, playerColliderCounts.TryGetValue(player, out int count) ? count : 1);
        playersInside.Add(player);
        NotifyPlayerEnteredRegulation(player);
    }

    private void UnregisterPlayerCollider(PlayerController player)
    {
        if (player == null || !playerColliderCounts.TryGetValue(player, out int colliderCount))
        {
            return;
        }

        colliderCount = Mathf.Max(0, colliderCount - 1);
        if (colliderCount > 0)
        {
            playerColliderCounts[player] = colliderCount;
            return;
        }

        playerColliderCounts.Remove(player);
        playersInside.Remove(player);
        player.RemoveRegulationMovementModifier(this);
        NotifyPlayerExitedRegulation(player);
    }

    private void NotifyPlayerEnteredRegulation(PlayerController player)
    {
        PlayerAngerRegulationStatus status =
            player.Statuses != null ? player.Statuses.AngerRegulationStatus : null;
        if (status != null)
        {
            status.EnterRegulation(this);
            return;
        }

        WarnMissingAngerRegulationStatus(player);
    }

    private void NotifyPlayerExitedRegulation(PlayerController player)
    {
        PlayerAngerRegulationStatus status =
            player.Statuses != null ? player.Statuses.AngerRegulationStatus : null;
        if (status != null)
        {
            status.ExitRegulation(this);
        }
    }

    private void WarnMissingAngerRegulationStatus(PlayerController player)
    {
        if (warnedMissingAngerRegulationStatus)
        {
            return;
        }

        Debug.LogWarning(
            "[AngerRegulation] Assign PlayerAngerRegulationStatus on the player's PlayerStatusManager to enable anger regulation visuals.",
            player);
        warnedMissingAngerRegulationStatus = true;
    }
}
