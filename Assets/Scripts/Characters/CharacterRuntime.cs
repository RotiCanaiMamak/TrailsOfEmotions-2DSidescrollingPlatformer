using UnityEngine;

[RequireComponent(typeof(PlayerController))]
public class CharacterRuntime : MonoBehaviour
{
    [Header("Character")]
    [SerializeField] private CharacterData characterData;
    [SerializeField] private CharacterAbility characterAbility;

    [Header("Fallback Character Defaults")]
    [Min(0f)] [SerializeField] private float fallbackJumpForce = 24f;
    [Range(0f, 1f)] [SerializeField] private float fallbackGlideGravityMultiplier = 0.35f;
    [Min(0f)] [SerializeField] private float fallbackBloatSpeedMultiplier = 1f;
    [Min(0f)] [SerializeField] private float fallbackEmotionAcceptanceMultiplier = 1f;
    [SerializeField] private string fallbackWeaknessBiomeFamilyName = "Overwhelm";
    [Range(0f, 100f)] [SerializeField] private float fallbackBrokenEmotionThreshold = 80f;

    public CharacterData Data => characterData;
    public PlayerController Player => player;
    public CharacterAbility Ability => characterAbility;
    public BiomeData ActiveBiome { get; private set; }
    public float EmotionValue => EmotionMeter.Instance != null ? EmotionMeter.Instance.Value : 0f;
    public string WeaknessBiomeFamilyName => characterData != null ? characterData.weaknessBiomeFamilyName : fallbackWeaknessBiomeFamilyName;
    public float BrokenEmotionThreshold => characterData != null ? characterData.brokenEmotionThreshold : fallbackBrokenEmotionThreshold;

    private PlayerController player;
    private bool reportedMissingCharacterData;

    private void Awake()
    {
        ResolveReferences();
        InitializeAbility();
        RefreshRuntime();
    }

    private void OnEnable()
    {
        ResolveReferences();
        InitializeAbility();
        RefreshRuntime();
    }

    private void Update()
    {
        RefreshRuntime();
    }

    private void OnDisable()
    {
        if (player != null)
        {
            player.RemoveMovementModifier(this);
        }

        if (EmotionMeter.Instance != null)
        {
            EmotionMeter.Instance.RemoveEmotionModifier(this);
        }

        characterAbility?.CleanupAbility();
    }

    public float GetBiomeJumpForwardMultiplier(BiomeData biome)
    {
        return ModifyBiomeMultiplier(biome != null ? biome.jumpForwardMultiplier : 1f, true);
    }

    public float GetBiomeJumpForceMultiplier(BiomeData biome)
    {
        return ModifyBiomeMultiplier(biome != null ? biome.jumpForceMultiplier : 1f, true);
    }

    public bool IsWeaknessBiome(BiomeData biome)
    {
        if (biome == null || string.IsNullOrWhiteSpace(WeaknessBiomeFamilyName))
        {
            return false;
        }

        return MatchesName(biome.biomeName, WeaknessBiomeFamilyName)
            || MatchesName(biome.familyId, WeaknessBiomeFamilyName);
    }

    public bool TryAddActiveAbilityCharge(int amount = 1)
    {
        return characterAbility != null && characterAbility.TryAddActiveCharge(amount);
    }

    private void ResolveReferences()
    {
        if (player == null)
        {
            player = GetComponent<PlayerController>();
        }

        if (characterAbility == null)
        {
            characterAbility = GetComponent<CharacterAbility>();
        }
    }

    private void InitializeAbility()
    {
        if (characterAbility != null)
        {
            characterAbility.InitializeAbility(this);
        }
    }

    private void RefreshRuntime()
    {
        ActiveBiome = GetActiveBiome();
        ApplyBaseStats();
        ApplySharedEmotionState();
        characterAbility?.TickAbility();
        ApplyBiomeMovementState();
    }

    private void ApplyBaseStats()
    {
        if (player == null)
        {
            return;
        }

        float moveSpeed = 0f;
        if (characterData == null)
        {
            if (!reportedMissingCharacterData)
            {
                Debug.LogError(
                    "[CharacterRuntime] No CharacterData is assigned. Assign a CharacterData asset to configure base movement speed.",
                    this);
                reportedMissingCharacterData = true;
            }
        }
        else
        {
            reportedMissingCharacterData = false;
            moveSpeed = characterData.moveSpeed;
        }

        player.SetBaseMovementStats(
            moveSpeed,
            GetJumpForce(),
            GetGlideGravityMultiplier());
    }

    private void ApplySharedEmotionState()
    {
        if (EmotionMeter.Instance == null)
        {
            return;
        }

        EmotionMeter.Instance.SetEmotionModifier(
            this,
            GetBloatSpeedMultiplier(),
            GetEmotionAcceptanceMultiplier());
    }

    private void ApplyBiomeMovementState()
    {
        if (player == null)
        {
            return;
        }

        CharacterMovementModifiers modifiers = new CharacterMovementModifiers(ActiveBiome);
        characterAbility?.ModifyBiomeMovement(ref modifiers);

        player.AddMovementModifier(
            this,
            modifiers.horizontalSpeedMultiplier,
            modifiers.glideGravityMultiplier,
            modifiers.airborneGravityMultiplier,
            modifiers.horizontalSpeedLerpDuration);
    }

    private float ModifyBiomeMultiplier(float rawMultiplier, bool lowerThanOneIsHarmful)
    {
        float multiplier = Mathf.Max(0f, rawMultiplier);
        return characterAbility != null
            ? characterAbility.ModifyBiomeMultiplier(multiplier, lowerThanOneIsHarmful)
            : multiplier;
    }

    private static BiomeData GetActiveBiome()
    {
        if (BiomeManager.Instance != null)
        {
            return BiomeManager.Instance.CurrentBiome;
        }

        return TerrainManager.Instance != null ? TerrainManager.Instance.CurrentBiome : null;
    }

    private static bool MatchesName(string value, string expected)
    {
        return !string.IsNullOrWhiteSpace(value)
            && string.Equals(value.Trim(), expected.Trim(), System.StringComparison.OrdinalIgnoreCase);
    }

    private float GetJumpForce() => characterData != null ? characterData.jumpForce : fallbackJumpForce;
    private float GetGlideGravityMultiplier() => characterData != null ? characterData.glideGravityMultiplier : fallbackGlideGravityMultiplier;
    private float GetBloatSpeedMultiplier() => characterData != null ? characterData.bloatSpeedMultiplier : fallbackBloatSpeedMultiplier;
    private float GetEmotionAcceptanceMultiplier() => characterData != null ? characterData.emotionAcceptanceMultiplier : fallbackEmotionAcceptanceMultiplier;
}
