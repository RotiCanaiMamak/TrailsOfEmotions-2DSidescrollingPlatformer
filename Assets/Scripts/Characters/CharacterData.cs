using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacter", menuName = "Game/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("Identity")]
    public string characterName = "Lina";

    [Header("Stats")]
    [Tooltip("Authoritative base movement speed applied to PlayerController at runtime.")]
    [Min(0f)] public float moveSpeed = 20f;
    [Min(0f)] public float jumpForce = 24f;
    [Tooltip("Lower values fall more slowly while gliding. Higher values drop faster.")]
    [Range(0f, 1f)] public float glideGravityMultiplier = 0.35f;
    [Min(0f)] public float bloatSpeedMultiplier = 1f;
    [Min(0f)] public float emotionAcceptanceMultiplier = 1f;

    [Header("Weakness")]
    public string weaknessBiomeFamilyName = "Overwhelm";
    [Range(0f, 100f)] public float brokenEmotionThreshold = 80f;

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        jumpForce = Mathf.Max(0f, jumpForce);
        glideGravityMultiplier = Mathf.Clamp01(glideGravityMultiplier);
        bloatSpeedMultiplier = Mathf.Max(0f, bloatSpeedMultiplier);
        emotionAcceptanceMultiplier = Mathf.Max(0f, emotionAcceptanceMultiplier);
        brokenEmotionThreshold = Mathf.Clamp(brokenEmotionThreshold, 0f, 100f);
    }
}
