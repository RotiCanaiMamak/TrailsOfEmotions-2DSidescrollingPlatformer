using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerStatusManager : MonoBehaviour
{
    [Header("Core")]
    [SerializeField] private PlayerController player;
    [SerializeField] private CharacterRuntime runtime;
    [SerializeField] private CharacterAbility ability;
    [SerializeField] private Animator animator;

    [Header("Anger Statuses")]
    [SerializeField] private PlayerBurnStatus burnStatus;
    [SerializeField] private PlayerSootStatus sootStatus;
    [SerializeField] private PlayerDriftingWispStatus driftingWispStatus;
    [SerializeField] private PlayerAngerRegulationStatus angerRegulationStatus;

    [Header("Sadness Statuses")]
    [SerializeField] private PlayerBubbleStatus bubbleStatus;
    [SerializeField] private PlayerDarkerSighStatus darkerSighStatus;
    [SerializeField] private PlayerIceHailStatus iceHailStatus;
    [SerializeField] private PlayerMistSlowStatus mistSlowStatus;
    [SerializeField] private PlayerMudStatus mudStatus;
    [SerializeField] private PlayerSinkingShadowStatus sinkingShadowStatus;
    [SerializeField] private PlayerSunkenPlatformStatus sunkenPlatformStatus;
    [SerializeField] private PlayerCrackedTrapStatus crackedTrapStatus;
    [SerializeField] private PlayerTumbleweedStatus tumbleweedStatus;
    [SerializeField] private PlayerEchoInputDelayStatus echoInputDelayStatus;

    public PlayerController Player => player;
    public CharacterRuntime Runtime => runtime;
    public CharacterAbility Ability => ability;
    public Animator Animator => animator;

    public PlayerBurnStatus BurnStatus => burnStatus;
    public PlayerSootStatus SootStatus => sootStatus;
    public PlayerDriftingWispStatus DriftingWispStatus => driftingWispStatus;
    public PlayerAngerRegulationStatus AngerRegulationStatus => angerRegulationStatus;
    public PlayerBubbleStatus BubbleStatus => bubbleStatus;
    public PlayerDarkerSighStatus DarkerSighStatus => darkerSighStatus;
    public PlayerIceHailStatus IceHailStatus => iceHailStatus;
    public PlayerMistSlowStatus MistSlowStatus => mistSlowStatus;
    public PlayerMudStatus MudStatus => mudStatus;
    public PlayerSinkingShadowStatus SinkingShadowStatus => sinkingShadowStatus;
    public PlayerSunkenPlatformStatus SunkenPlatformStatus => sunkenPlatformStatus;
    public PlayerCrackedTrapStatus CrackedTrapStatus => crackedTrapStatus;
    public PlayerTumbleweedStatus TumbleweedStatus => tumbleweedStatus;
    public PlayerEchoInputDelayStatus EchoInputDelayStatus => echoInputDelayStatus;

    public bool TryGetStatus<T>(out T status) where T : Component
    {
        status = GetStatus<T>();
        return status != null;
    }

    public T GetStatus<T>() where T : Component
    {
        if (typeof(T) == typeof(PlayerBurnStatus))
        {
            return burnStatus as T;
        }

        if (typeof(T) == typeof(PlayerSootStatus))
        {
            return sootStatus as T;
        }

        if (typeof(T) == typeof(PlayerDriftingWispStatus))
        {
            return driftingWispStatus as T;
        }

        if (typeof(T) == typeof(PlayerAngerRegulationStatus))
        {
            return angerRegulationStatus as T;
        }

        if (typeof(T) == typeof(PlayerBubbleStatus))
        {
            return bubbleStatus as T;
        }

        if (typeof(T) == typeof(PlayerDarkerSighStatus))
        {
            return darkerSighStatus as T;
        }

        if (typeof(T) == typeof(PlayerIceHailStatus))
        {
            return iceHailStatus as T;
        }

        if (typeof(T) == typeof(PlayerMistSlowStatus))
        {
            return mistSlowStatus as T;
        }

        if (typeof(T) == typeof(PlayerMudStatus))
        {
            return mudStatus as T;
        }

        if (typeof(T) == typeof(PlayerSinkingShadowStatus))
        {
            return sinkingShadowStatus as T;
        }

        if (typeof(T) == typeof(PlayerSunkenPlatformStatus))
        {
            return sunkenPlatformStatus as T;
        }

        if (typeof(T) == typeof(PlayerCrackedTrapStatus))
        {
            return crackedTrapStatus as T;
        }

        if (typeof(T) == typeof(PlayerTumbleweedStatus))
        {
            return tumbleweedStatus as T;
        }

        if (typeof(T) == typeof(PlayerEchoInputDelayStatus))
        {
            return echoInputDelayStatus as T;
        }

        return null;
    }
}
