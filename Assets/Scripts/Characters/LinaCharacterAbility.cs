using UnityEngine;

[RequireComponent(typeof(LinaBackpackBulwark))]
public class LinaCharacterAbility : CharacterAbility
{
    [Header("Passive")]
    [Range(0f, 100f)] [SerializeField] private float passiveEmotionLimit = 50f;
    [Min(0f)] [SerializeField] private float passivePenaltyFadeDuration = 3f;

    [Header("Broken State")]
    [Min(0f)] [SerializeField] private float brokenSpeedMultiplier = 1.25f;
    [Min(0f)] [SerializeField] private float brokenBloatSpeedMultiplier = 2f;

    [Header("Backpack Bulwark")]
    [Min(0f)] [SerializeField] private float activeDuration = 5f;
    [SerializeField] private LinaBackpackBulwark backpackBulwark;

    private float biomePenaltyBlend = 1f;

    public bool IsBroken { get; private set; }
    public bool IsPassiveProtecting { get; private set; }
    public bool IsActiveReady =>
        !IsActiveAbilityBlocked &&
        IsActiveFullyCharged &&
        (backpackBulwark == null || !backpackBulwark.IsActive);

    protected override void OnInitialized()
    {
        ResolveReferences();
        RefreshState(true);
    }

    protected override void OnTick()
    {
        RefreshState(false);
        HandleActiveInput();
    }

    protected override void OnCleanup()
    {
        if (Player != null)
        {
            Player.RemoveMovementModifier(this);
        }

        if (EmotionMeter.Instance != null)
        {
            EmotionMeter.Instance.RemoveEmotionModifier(this);
        }
    }

    public override void ModifyBiomeMovement(ref CharacterMovementModifiers modifiers)
    {
        modifiers.horizontalSpeedMultiplier = ApplyPassiveToMultiplier(modifiers.horizontalSpeedMultiplier, true);
        modifiers.glideGravityMultiplier = ApplyPassiveToMultiplier(modifiers.glideGravityMultiplier, false);
        modifiers.airborneGravityMultiplier = ApplyPassiveToMultiplier(modifiers.airborneGravityMultiplier, false);

        if (IsBroken)
        {
            modifiers.horizontalSpeedMultiplier *= brokenSpeedMultiplier;
        }
    }

    public override float ModifyBiomeMultiplier(float multiplier, bool lowerThanOneIsHarmful)
    {
        return ApplyPassiveToMultiplier(multiplier, lowerThanOneIsHarmful);
    }

    private void ResolveReferences()
    {
        if (backpackBulwark == null)
        {
            backpackBulwark = GetComponent<LinaBackpackBulwark>();
        }
    }

    private void RefreshState(bool instant)
    {
        IsBroken = Runtime != null
            && (EmotionValue >= Runtime.BrokenEmotionThreshold || Runtime.IsWeaknessBiome(ActiveBiome));
        IsPassiveProtecting = !IsBroken && EmotionValue <= passiveEmotionLimit;

        if (instant || IsPassiveProtecting)
        {
            biomePenaltyBlend = IsPassiveProtecting ? 0f : 1f;
        }
        else if (passivePenaltyFadeDuration <= 0f)
        {
            biomePenaltyBlend = 1f;
        }
        else
        {
            biomePenaltyBlend = Mathf.MoveTowards(
                biomePenaltyBlend,
                1f,
                Time.deltaTime / passivePenaltyFadeDuration);
        }

        ApplyBrokenEmotionModifier();
    }

    private void ApplyBrokenEmotionModifier()
    {
        if (EmotionMeter.Instance == null)
        {
            return;
        }

        EmotionMeter.Instance.SetEmotionModifier(
            this,
            IsBroken ? brokenBloatSpeedMultiplier : 1f,
            1f);
    }

    private void HandleActiveInput()
    {
        bool shiftPressed = IsShiftPressed();
        if ((shiftPressed || IsBroken) && IsActiveReady)
        {
            ActivateBackpackBulwark();
        }
    }

    private bool IsShiftPressed()
    {
        if (Player != null)
        {
            return Player.GetDelayedKeyDown(KeyCode.LeftShift) ||
                Player.GetDelayedKeyDown(KeyCode.RightShift);
        }

        return Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift);
    }

    private void ActivateBackpackBulwark()
    {
        if (backpackBulwark == null)
        {
            Debug.LogWarning("[LinaCharacterAbility] Backpack Bulwark is ready, but no LinaBackpackBulwark component is assigned.", this);
            return;
        }

        if (!TryConsumeFullActiveCharge())
        {
            return;
        }

        backpackBulwark.Activate(activeDuration);
    }

    private float ApplyPassiveToMultiplier(float rawMultiplier, bool lowerThanOneIsHarmful)
    {
        float multiplier = Mathf.Max(0f, rawMultiplier);
        bool isHarmful = lowerThanOneIsHarmful ? multiplier < 1f : multiplier > 1f;
        if (!isHarmful)
        {
            return multiplier;
        }

        return IsPassiveProtecting ? 1f : Mathf.Lerp(1f, multiplier, biomePenaltyBlend);
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        passiveEmotionLimit = Mathf.Clamp(passiveEmotionLimit, 0f, 100f);
        passivePenaltyFadeDuration = Mathf.Max(0f, passivePenaltyFadeDuration);
        brokenSpeedMultiplier = Mathf.Max(0f, brokenSpeedMultiplier);
        brokenBloatSpeedMultiplier = Mathf.Max(0f, brokenBloatSpeedMultiplier);
        activeDuration = Mathf.Max(0f, activeDuration);
    }
}
