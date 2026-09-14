using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public sealed class AbilityOrbObstacleProfile : MonoBehaviour, IAbilityOrbObstacleProfile
{
    [Header("Ability Orb Path")]
    [FormerlySerializedAs("orbSpacing")]
    [Tooltip("Arc-length distance between ability orbs on this obstacle's jump path.")]
    [Min(0.1f)] [SerializeField] private float targetOrbSpacing = 2f;
    [Tooltip("Horizontal distance from the start of the jump path to the end, centered on this obstacle.")]
    [Min(0.1f)] [SerializeField] private float curveLength = 6f;
    [Tooltip("Vertical distance from this obstacle's transform position to the jump path's apex.")]
    [Min(0f)] [SerializeField] private float curveHeight = 3f;
    [Tooltip("Horizontal offset applied to the center of this obstacle's jump path.")]
    [SerializeField] private float centerOffsetX = 0f;

    public float TargetOrbSpacing => Mathf.Max(0.1f, targetOrbSpacing);
    public float CurveHeight => Mathf.Max(0f, curveHeight);
    public float CurveLength => Mathf.Max(0.1f, curveLength);
    public float CenterOffsetX => centerOffsetX;

    private void OnValidate()
    {
        targetOrbSpacing = Mathf.Max(0.1f, targetOrbSpacing);
        curveLength = Mathf.Max(0.1f, curveLength);
        curveHeight = Mathf.Max(0f, curveHeight);
    }
}
