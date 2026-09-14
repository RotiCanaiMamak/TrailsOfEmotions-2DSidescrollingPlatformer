using UnityEngine;

[DisallowMultipleComponent]
public sealed class AngerEmotionBehaviour : MonoBehaviour
{
    private const string PlayerTag = "Player";

    [Header("References")]
    [SerializeField] private PlayerController player;

    [Header("Anger Speed")]
    [SerializeField] private string angerBiomeNameOrFamily = "Anger";
    [Range(0f, 100f)] [SerializeField] private float angerStartThreshold = 80f;
    [Min(0f)] [SerializeField] private float angerMaxSpeed = 30f;
    [Min(0f)] [SerializeField] private float airborneSpeedLerpDuration = 0f;

    private EmotionMeter emotionMeter;
    private BiomeManager biomeManager;
    private BiomeData activeBiome;
    private PlayerController angerModifiedPlayer;
    private bool angerModifierApplied;
    private bool warnedMissingPlayerTag;

    private void Awake()
    {
        ResolvePlayerReference();
    }

    private void OnEnable()
    {
        ResolvePlayerReference();
        RefreshSubscriptions();
        RefreshAngerSpeedEffect();
    }

    private void Update()
    {
        ResolvePlayerReference();
        RefreshSubscriptions();
        RefreshAngerSpeedEffect();
    }

    private void OnDisable()
    {
        ClearAngerSpeedEffect();
        UnsubscribeFromEmotionMeter();
        UnsubscribeFromBiomeManager();
    }

    private void OnDestroy()
    {
        ClearAngerSpeedEffect();
    }

    private void OnValidate()
    {
        angerStartThreshold = Mathf.Clamp(angerStartThreshold, 0f, 100f);
        angerMaxSpeed = Mathf.Max(0f, angerMaxSpeed);
        airborneSpeedLerpDuration = Mathf.Max(0f, airborneSpeedLerpDuration);
    }

    private void RefreshSubscriptions()
    {
        if (emotionMeter != EmotionMeter.Instance)
        {
            UnsubscribeFromEmotionMeter();
            SubscribeToEmotionMeter(EmotionMeter.Instance);
        }

        if (biomeManager != BiomeManager.Instance)
        {
            UnsubscribeFromBiomeManager();
            SubscribeToBiomeManager(BiomeManager.Instance);
        }
    }

    private void SubscribeToEmotionMeter(EmotionMeter meter)
    {
        emotionMeter = meter;
        if (emotionMeter == null)
        {
            return;
        }

        emotionMeter.onValueChanged.AddListener(OnEmotionValueChanged);
    }

    private void UnsubscribeFromEmotionMeter()
    {
        if (emotionMeter != null)
        {
            emotionMeter.onValueChanged.RemoveListener(OnEmotionValueChanged);
        }

        emotionMeter = null;
    }

    private void SubscribeToBiomeManager(BiomeManager manager)
    {
        biomeManager = manager;
        if (biomeManager == null)
        {
            activeBiome = null;
            return;
        }

        activeBiome = biomeManager.CurrentBiome;
        biomeManager.onBiomeChanged.AddListener(OnBiomeChanged);
    }

    private void UnsubscribeFromBiomeManager()
    {
        if (biomeManager != null)
        {
            biomeManager.onBiomeChanged.RemoveListener(OnBiomeChanged);
        }

        biomeManager = null;
        activeBiome = null;
    }

    private void OnEmotionValueChanged(float value)
    {
        RefreshAngerSpeedEffect();
    }

    private void OnBiomeChanged(BiomeData biome)
    {
        activeBiome = biome;
        RefreshAngerSpeedEffect();
    }

    private void RefreshAngerSpeedEffect()
    {
        if (!CanApplyAngerSpeedEffect())
        {
            ClearAngerSpeedEffect();
            return;
        }

        float baseSpeed = player.BaseMoveSpeed;
        if (baseSpeed <= 0f)
        {
            ClearAngerSpeedEffect();
            return;
        }

        if (angerModifierApplied && angerModifiedPlayer != null && angerModifiedPlayer != player)
        {
            ClearAngerSpeedEffect();
        }

        float t = angerStartThreshold >= 100f
            ? 1f
            : Mathf.InverseLerp(angerStartThreshold, 100f, emotionMeter.Value);
        float effectiveMaxSpeed = Mathf.Max(baseSpeed, angerMaxSpeed);
        float targetSpeed = Mathf.Lerp(baseSpeed, effectiveMaxSpeed, t);
        float horizontalMultiplier = Mathf.Max(0f, targetSpeed / baseSpeed);

        player.AddMovementModifier(
            this,
            horizontalMultiplier,
            1f,
            1f,
            airborneSpeedLerpDuration);

        angerModifiedPlayer = player;
        angerModifierApplied = true;
    }

    private bool CanApplyAngerSpeedEffect()
    {
        return player != null &&
            emotionMeter != null &&
            emotionMeter.Value >= angerStartThreshold &&
            IsAngerBiome(activeBiome);
    }

    private void ClearAngerSpeedEffect()
    {
        if (!angerModifierApplied)
        {
            return;
        }

        if (angerModifiedPlayer != null)
        {
            angerModifiedPlayer.RemoveMovementModifier(this);
        }
        else if (player != null)
        {
            player.RemoveMovementModifier(this);
        }

        angerModifiedPlayer = null;
        angerModifierApplied = false;
    }

    private bool IsAngerBiome(BiomeData biome)
    {
        if (biome == null)
        {
            return false;
        }

        return MatchesName(biome.familyId, angerBiomeNameOrFamily) ||
            MatchesName(biome.biomeName, angerBiomeNameOrFamily);
    }

    private void ResolvePlayerReference()
    {
        if (player != null)
        {
            return;
        }

        GameObject playerObject = FindPlayerObject();
        if (playerObject != null)
        {
            player = playerObject.GetComponent<PlayerController>();
            if (player == null)
            {
                player = playerObject.GetComponentInParent<PlayerController>();
            }

            if (player == null)
            {
                player = playerObject.GetComponentInChildren<PlayerController>();
            }
        }

        if (player == null)
        {
            player = FindFirstObjectByType<PlayerController>();
        }
    }

    private GameObject FindPlayerObject()
    {
        try
        {
            return GameObject.FindGameObjectWithTag(PlayerTag);
        }
        catch (UnityException)
        {
            if (!warnedMissingPlayerTag)
            {
                Debug.LogWarning($"[AngerEmotionBehaviour] No Unity tag named '{PlayerTag}' exists.", this);
                warnedMissingPlayerTag = true;
            }

            return null;
        }
    }

    private static bool MatchesName(string value, string expected)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            !string.IsNullOrWhiteSpace(expected) &&
            string.Equals(value.Trim(), expected.Trim(), System.StringComparison.OrdinalIgnoreCase);
    }
}
