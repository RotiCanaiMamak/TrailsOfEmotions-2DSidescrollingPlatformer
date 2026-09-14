using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerParticleTrailManager : MonoBehaviour
{
    [Serializable]
    public sealed class BiomeParticleMaterialMapping
    {
        public string familyId = "";
        public Material runMaterial;
        public Material groundPoundMaterial;
    }

    [Header("Core")]
    [SerializeField] private PlayerController player;

    [Header("Particles")]
    [SerializeField] private ParticleSystem runParticleSystem;
    [SerializeField] private ParticleSystem groundPoundParticleSystem;
    [SerializeField] private ParticleSystem ziplineParticleSystem;

    [Header("Biome Materials")]
    [SerializeField] private BiomeParticleMaterialMapping[] biomeMaterialMappings;

    [Header("Warnings")]
    [SerializeField] private bool warnIfMissingReferences = true;

    private readonly HashSet<string> warnedMissingMappings =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> warnedMissingRunMaterials =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> warnedMissingGroundPoundMaterials =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> warnedDuplicateMappings =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private BiomeManager biomeManager;
    private BiomeData activeBiome;
    private ParticleSystemRenderer runRenderer;
    private ParticleSystemRenderer groundPoundRenderer;
    private bool subscribedToPlayer;
    private bool subscribedToBiomeManager;
    private bool warnedMissingPlayer;
    private bool warnedMissingRunParticle;
    private bool warnedMissingGroundPoundParticle;
    private bool warnedMissingZiplineParticle;
    private bool warnedMissingRunRenderer;
    private bool warnedMissingGroundPoundRenderer;

    private void Awake()
    {
        ResolveReferences();
        CacheParticleRenderers();
    }

    private void OnEnable()
    {
        ResolveReferences();
        CacheParticleRenderers();
        SubscribeToPlayer();
        TrySubscribeToBiomeManager();
        RefreshActiveBiome();
        ApplyRunTrailState();
    }

    private void Update()
    {
        ResolveReferences();
        CacheParticleRenderers();
        SubscribeToPlayer();

        if (!TrySubscribeToBiomeManager() && activeBiome == null)
        {
            RefreshActiveBiome();
        }

        ApplyRunTrailState();
    }

    private void OnDisable()
    {
        StopRunTrail(true);
        StopZiplineTrail(true);
        UnsubscribeFromPlayer();
        UnsubscribeFromBiomeManager();
    }

    private void OnDestroy()
    {
        UnsubscribeFromPlayer();
        UnsubscribeFromBiomeManager();
    }

    private void OnValidate()
    {
        CacheParticleRenderers();
    }

    private void OnBiomeChanged(BiomeData biome)
    {
        activeBiome = biome;
        ApplyRunTrailMaterial();
    }

    private void OnGroundPoundLanded(PlayerController landedPlayer)
    {
        if (landedPlayer != player)
        {
            return;
        }

        PlayGroundPoundParticle(landedPlayer.LastGroundPoundImpactPoint);
    }

    private void ApplyRunTrailState()
    {
        if (player == null)
        {
            WarnMissingPlayer();
            StopRunTrail(true);
            StopZiplineTrail(true);
            return;
        }

        if (player.IsOnZipline)
        {
            StopRunTrail(false);
            ApplyZiplineTrailState();
            return;
        }

        StopZiplineTrail(false);

        if (runParticleSystem == null)
        {
            WarnMissingRunParticle();
            return;
        }

        bool shouldPlay = player.IsGrounded;
        if (!shouldPlay)
        {
            StopRunTrail(false);
            return;
        }

        if (!ApplyRunTrailMaterial())
        {
            StopRunTrail(true);
            return;
        }

        if (!runParticleSystem.isPlaying)
        {
            runParticleSystem.Play(true);
        }
    }

    private void ApplyZiplineTrailState()
    {
        if (ziplineParticleSystem == null)
        {
            WarnMissingZiplineParticle();
            return;
        }

        if (!ziplineParticleSystem.isPlaying)
        {
            ziplineParticleSystem.Play(true);
        }
    }

    private bool ApplyRunTrailMaterial()
    {
        if (runParticleSystem == null)
        {
            WarnMissingRunParticle();
            return false;
        }

        if (runRenderer == null)
        {
            WarnMissingRunRenderer();
            return false;
        }

        BiomeParticleMaterialMapping mapping = GetMappingForActiveBiome();
        if (mapping == null)
        {
            WarnMissingMapping(activeBiome);
            return false;
        }

        if (mapping.runMaterial == null)
        {
            WarnMissingRunMaterial(activeBiome);
            return false;
        }

        if (runRenderer.sharedMaterial != mapping.runMaterial)
        {
            runRenderer.sharedMaterial = mapping.runMaterial;
        }

        return true;
    }

    private void PlayGroundPoundParticle(Vector2 impactPoint)
    {
        if (groundPoundParticleSystem == null)
        {
            WarnMissingGroundPoundParticle();
            return;
        }

        if (!ApplyGroundPoundMaterial())
        {
            return;
        }

        groundPoundParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        groundPoundParticleSystem.transform.position = impactPoint;
        groundPoundParticleSystem.Play(true);
    }

    private bool ApplyGroundPoundMaterial()
    {
        if (groundPoundParticleSystem == null)
        {
            WarnMissingGroundPoundParticle();
            return false;
        }

        if (groundPoundRenderer == null)
        {
            WarnMissingGroundPoundRenderer();
            return false;
        }

        BiomeParticleMaterialMapping mapping = GetMappingForActiveBiome();
        if (mapping == null)
        {
            WarnMissingMapping(activeBiome);
            return false;
        }

        if (mapping.groundPoundMaterial == null)
        {
            WarnMissingGroundPoundMaterial(activeBiome);
            return false;
        }

        if (groundPoundRenderer.sharedMaterial != mapping.groundPoundMaterial)
        {
            groundPoundRenderer.sharedMaterial = mapping.groundPoundMaterial;
        }

        return true;
    }

    private BiomeParticleMaterialMapping GetMappingForActiveBiome()
    {
        RefreshActiveBiome();
        return GetMappingForFamilyId(activeBiome != null ? activeBiome.familyId : null);
    }

    private BiomeParticleMaterialMapping GetMappingForFamilyId(string familyId)
    {
        string activeFamilyId = NormalizeFamilyId(familyId);
        if (biomeMaterialMappings == null || string.IsNullOrEmpty(activeFamilyId))
        {
            return null;
        }

        BiomeParticleMaterialMapping match = null;
        for (int i = 0; i < biomeMaterialMappings.Length; i++)
        {
            BiomeParticleMaterialMapping mapping = biomeMaterialMappings[i];
            if (mapping == null ||
                !string.Equals(
                    NormalizeFamilyId(mapping.familyId),
                    activeFamilyId,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (match == null)
            {
                match = mapping;
            }
            else
            {
                WarnDuplicateMapping(activeFamilyId);
                break;
            }
        }

        return match;
    }

    private void RefreshActiveBiome()
    {
        if (biomeManager != null)
        {
            biomeManager.EnsureInitialized();
            activeBiome = biomeManager.CurrentBiome;
            return;
        }

        if (BiomeManager.Instance != null)
        {
            biomeManager = BiomeManager.Instance;
            biomeManager.EnsureInitialized();
            activeBiome = biomeManager.CurrentBiome;
            return;
        }

        if (TerrainManager.Instance != null)
        {
            activeBiome = TerrainManager.Instance.CurrentBiome;
        }
    }

    private void ResolveReferences()
    {
        if (player == null)
        {
            player = GetComponent<PlayerController>();
        }

        if (player == null)
        {
            player = GetComponentInParent<PlayerController>();
        }

        ResolveParticleReferences();
    }

    private void ResolveParticleReferences()
    {
        if (runParticleSystem != null && groundPoundParticleSystem != null && ziplineParticleSystem != null)
        {
            return;
        }

        ParticleSystem[] particles = GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
        {
            ParticleSystem particle = particles[i];
            if (particle == null)
            {
                continue;
            }

            string particleName = particle.name;
            if (ziplineParticleSystem == null && IsZiplineParticleName(particleName))
            {
                ziplineParticleSystem = particle;
            }
            else if (!IsZiplineParticleName(particleName) &&
                runParticleSystem == null &&
                IsRunParticleName(particleName))
            {
                runParticleSystem = particle;
            }
            else if (groundPoundParticleSystem == null && IsGroundPoundParticleName(particleName))
            {
                groundPoundParticleSystem = particle;
            }
        }
    }

    private void CacheParticleRenderers()
    {
        runRenderer = runParticleSystem != null
            ? runParticleSystem.GetComponent<ParticleSystemRenderer>()
            : null;
        groundPoundRenderer = groundPoundParticleSystem != null
            ? groundPoundParticleSystem.GetComponent<ParticleSystemRenderer>()
            : null;
    }

    private void SubscribeToPlayer()
    {
        if (subscribedToPlayer || player == null)
        {
            return;
        }

        player.GroundPoundLanded += OnGroundPoundLanded;
        subscribedToPlayer = true;
    }

    private void UnsubscribeFromPlayer()
    {
        if (!subscribedToPlayer || player == null)
        {
            subscribedToPlayer = false;
            return;
        }

        player.GroundPoundLanded -= OnGroundPoundLanded;
        subscribedToPlayer = false;
    }

    private bool TrySubscribeToBiomeManager()
    {
        if (subscribedToBiomeManager)
        {
            return true;
        }

        biomeManager = BiomeManager.Instance;
        if (biomeManager == null)
        {
            return false;
        }

        biomeManager.onBiomeChanged.AddListener(OnBiomeChanged);
        subscribedToBiomeManager = true;
        RefreshActiveBiome();
        return true;
    }

    private void UnsubscribeFromBiomeManager()
    {
        if (!subscribedToBiomeManager || biomeManager == null)
        {
            subscribedToBiomeManager = false;
            return;
        }

        biomeManager.onBiomeChanged.RemoveListener(OnBiomeChanged);
        subscribedToBiomeManager = false;
    }

    private void StopRunTrail(bool clear)
    {
        if (runParticleSystem == null)
        {
            return;
        }

        runParticleSystem.Stop(
            true,
            clear
                ? ParticleSystemStopBehavior.StopEmittingAndClear
                : ParticleSystemStopBehavior.StopEmitting);
    }

    private void StopZiplineTrail(bool clear)
    {
        if (ziplineParticleSystem == null)
        {
            return;
        }

        ziplineParticleSystem.Stop(
            true,
            clear
                ? ParticleSystemStopBehavior.StopEmittingAndClear
                : ParticleSystemStopBehavior.StopEmitting);
    }

    private void WarnMissingPlayer()
    {
        if (!warnIfMissingReferences || warnedMissingPlayer)
        {
            return;
        }

        Debug.LogWarning("[PlayerParticleTrailManager] Assign a PlayerController reference.", this);
        warnedMissingPlayer = true;
    }

    private void WarnMissingRunParticle()
    {
        if (!warnIfMissingReferences || warnedMissingRunParticle)
        {
            return;
        }

        Debug.LogWarning("[PlayerParticleTrailManager] Assign a run ParticleSystem.", this);
        warnedMissingRunParticle = true;
    }

    private void WarnMissingGroundPoundParticle()
    {
        if (!warnIfMissingReferences || warnedMissingGroundPoundParticle)
        {
            return;
        }

        Debug.LogWarning("[PlayerParticleTrailManager] Assign a ground-pound ParticleSystem.", this);
        warnedMissingGroundPoundParticle = true;
    }

    private void WarnMissingZiplineParticle()
    {
        if (!warnIfMissingReferences || warnedMissingZiplineParticle)
        {
            return;
        }

        Debug.LogWarning("[PlayerParticleTrailManager] Assign a zipline ParticleSystem.", this);
        warnedMissingZiplineParticle = true;
    }

    private void WarnMissingRunRenderer()
    {
        if (!warnIfMissingReferences || warnedMissingRunRenderer)
        {
            return;
        }

        Debug.LogWarning("[PlayerParticleTrailManager] Run ParticleSystem has no ParticleSystemRenderer.", this);
        warnedMissingRunRenderer = true;
    }

    private void WarnMissingGroundPoundRenderer()
    {
        if (!warnIfMissingReferences || warnedMissingGroundPoundRenderer)
        {
            return;
        }

        Debug.LogWarning("[PlayerParticleTrailManager] Ground-pound ParticleSystem has no ParticleSystemRenderer.", this);
        warnedMissingGroundPoundRenderer = true;
    }

    private void WarnMissingMapping(BiomeData biome)
    {
        string familyId = GetActiveFamilyWarningKey(biome);
        if (!warnIfMissingReferences || !warnedMissingMappings.Add(familyId))
        {
            return;
        }

        Debug.LogWarning($"[PlayerParticleTrailManager] No particle material mapping assigned for family ID '{familyId}'.", this);
    }

    private void WarnMissingRunMaterial(BiomeData biome)
    {
        string familyId = GetActiveFamilyWarningKey(biome);
        if (!warnIfMissingReferences || !warnedMissingRunMaterials.Add(familyId))
        {
            return;
        }

        Debug.LogWarning($"[PlayerParticleTrailManager] Family ID '{familyId}' has no run particle material assigned.", this);
    }

    private void WarnMissingGroundPoundMaterial(BiomeData biome)
    {
        string familyId = GetActiveFamilyWarningKey(biome);
        if (!warnIfMissingReferences || !warnedMissingGroundPoundMaterials.Add(familyId))
        {
            return;
        }

        Debug.LogWarning($"[PlayerParticleTrailManager] Family ID '{familyId}' has no ground-pound particle material assigned.", this);
    }

    private void WarnDuplicateMapping(string familyId)
    {
        familyId = NormalizeFamilyId(familyId);
        if (!warnIfMissingReferences || !warnedDuplicateMappings.Add(familyId))
        {
            return;
        }

        Debug.LogWarning($"[PlayerParticleTrailManager] Duplicate particle material mapping for family ID '{familyId}'. The first mapping will be used.", this);
    }

    private static string GetActiveFamilyWarningKey(BiomeData biome)
    {
        string familyId = biome != null ? NormalizeFamilyId(biome.familyId) : string.Empty;
        if (!string.IsNullOrEmpty(familyId))
        {
            return familyId;
        }

        return "<empty>";
    }

    private static string NormalizeFamilyId(string familyId)
    {
        return string.IsNullOrWhiteSpace(familyId) ? string.Empty : familyId.Trim();
    }

    private static bool IsRunParticleName(string particleName)
    {
        if (string.IsNullOrWhiteSpace(particleName))
        {
            return false;
        }

        return particleName.IndexOf("run", StringComparison.OrdinalIgnoreCase) >= 0 ||
            particleName.IndexOf("trail", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsZiplineParticleName(string particleName)
    {
        if (string.IsNullOrWhiteSpace(particleName))
        {
            return false;
        }

        return particleName.IndexOf("zipline", StringComparison.OrdinalIgnoreCase) >= 0 ||
            particleName.IndexOf("zip line", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsGroundPoundParticleName(string particleName)
    {
        if (string.IsNullOrWhiteSpace(particleName))
        {
            return false;
        }

        return particleName.IndexOf("groundpound", StringComparison.OrdinalIgnoreCase) >= 0 ||
            particleName.IndexOf("ground pound", StringComparison.OrdinalIgnoreCase) >= 0 ||
            particleName.IndexOf("pound", StringComparison.OrdinalIgnoreCase) >= 0 ||
            particleName.IndexOf("impact", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
