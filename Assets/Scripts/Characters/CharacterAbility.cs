using System.Collections.Generic;
using UnityEngine;

public struct CharacterMovementModifiers
{
    public float horizontalSpeedMultiplier;
    public float glideGravityMultiplier;
    public float airborneGravityMultiplier;
    public float horizontalSpeedLerpDuration;

    public CharacterMovementModifiers(BiomeData biome)
    {
        horizontalSpeedMultiplier = biome != null ? biome.horizontalSpeedMultiplier : 1f;
        glideGravityMultiplier = biome != null ? biome.glideGravityMultiplier : 1f;
        airborneGravityMultiplier = biome != null ? biome.airborneGravityMultiplier : 1f;
        horizontalSpeedLerpDuration = biome != null ? biome.horizontalSpeedLerpDuration : 0f;
    }
}

public abstract class CharacterAbility : MonoBehaviour
{
    [Header("Active Ability Charge")]
    [Min(1)] [SerializeField] private int maxActiveCharge = 10;

    private int activeChargeCount;
    private readonly HashSet<Object> activeAbilityBlockers = new HashSet<Object>();

    protected CharacterRuntime Runtime { get; private set; }
    protected PlayerController Player => Runtime != null ? Runtime.Player : null;
    protected BiomeData ActiveBiome => Runtime != null ? Runtime.ActiveBiome : null;
    protected float EmotionValue => Runtime != null ? Runtime.EmotionValue : 0f;

    public int ActiveChargeCount => activeChargeCount;
    public int MaxActiveCharge => Mathf.Max(1, maxActiveCharge);
    public bool IsActiveFullyCharged => activeChargeCount >= MaxActiveCharge;
    public bool IsActiveAbilityBlocked
    {
        get
        {
            activeAbilityBlockers.RemoveWhere(blocker => blocker == null);
            return activeAbilityBlockers.Count > 0;
        }
    }

    public void InitializeAbility(CharacterRuntime runtime)
    {
        Runtime = runtime;
        ClampActiveCharge();
        OnInitialized();
    }

    public void TickAbility()
    {
        OnTick();
    }

    public void CleanupAbility()
    {
        OnCleanup();
        activeAbilityBlockers.Clear();
    }

    public void SetActiveAbilityBlocked(Object source, bool blocked)
    {
        if (source == null)
        {
            return;
        }

        if (blocked)
        {
            activeAbilityBlockers.Add(source);
        }
        else
        {
            activeAbilityBlockers.Remove(source);
        }
    }

    public bool TryAddActiveCharge(int amount = 1)
    {
        if (amount <= 0 || IsActiveFullyCharged)
        {
            return false;
        }

        int previousCharge = activeChargeCount;
        activeChargeCount = Mathf.Min(MaxActiveCharge, activeChargeCount + amount);
        return activeChargeCount > previousCharge;
    }

    public bool TryDrainActiveCharges(int amount = 1)
    {
        if (amount <= 0 || activeChargeCount <= 0)
        {
            return false;
        }

        int previousCharge = activeChargeCount;
        activeChargeCount = Mathf.Max(0, activeChargeCount - amount);
        return activeChargeCount < previousCharge;
    }

    public bool TryConsumeFullActiveCharge()
    {
        if (IsActiveAbilityBlocked || !IsActiveFullyCharged)
        {
            return false;
        }

        activeChargeCount = 0;
        return true;
    }

    public virtual void ModifyBiomeMovement(ref CharacterMovementModifiers modifiers)
    {
    }

    public virtual float ModifyBiomeMultiplier(float multiplier, bool lowerThanOneIsHarmful)
    {
        return Mathf.Max(0f, multiplier);
    }

    protected virtual void OnInitialized()
    {
    }

    protected virtual void OnTick()
    {
    }

    protected virtual void OnCleanup()
    {
    }

    protected virtual void OnValidate()
    {
        maxActiveCharge = Mathf.Max(1, maxActiveCharge);
        ClampActiveCharge();
    }

    private void ClampActiveCharge()
    {
        activeChargeCount = Mathf.Clamp(activeChargeCount, 0, MaxActiveCharge);
    }
}
