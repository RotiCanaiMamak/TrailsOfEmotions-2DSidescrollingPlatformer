using UnityEngine;

[CreateAssetMenu(fileName = "NewTerrainChunk", menuName = "Game/Terrain Chunk Definition")]
public class TerrainChunkDefinition : ScriptableObject
{
    public enum HeightBehavior
    {
        Flat,
        Rise,
        Drop,
        Gap
    }

    [Header("Display")]
    public string displayName = "New Terrain Chunk";

    [Header("Height")]
    public HeightBehavior heightBehavior = HeightBehavior.Flat;
    [Min(0f)] public float minVerticalAmount;
    [Min(0f)] public float maxVerticalAmount;

    [Header("Ability Orbs")]
    [Tooltip("Use this chunk's orb spacing and surface height instead of TerrainManager's defaults.")]
    [SerializeField] private bool overrideAbilityOrbTrailSettings;
    [Min(0.1f)] [SerializeField] private float abilityOrbSpacing = 3f;
    [Min(0f)] [SerializeField] private float abilityOrbHeight = 1.25f;

    public bool OverridesAbilityOrbTrailSettings => overrideAbilityOrbTrailSettings;
    public float AbilityOrbSpacing => Mathf.Max(0.1f, abilityOrbSpacing);
    public float AbilityOrbHeight => Mathf.Max(0f, abilityOrbHeight);

    private void OnValidate()
    {
        minVerticalAmount = Mathf.Max(0f, minVerticalAmount);
        maxVerticalAmount = Mathf.Max(minVerticalAmount, maxVerticalAmount);
        abilityOrbSpacing = Mathf.Max(0.1f, abilityOrbSpacing);
        abilityOrbHeight = Mathf.Max(0f, abilityOrbHeight);
    }

    public float RollVerticalAmount()
    {
        return Random.Range(Mathf.Min(minVerticalAmount, maxVerticalAmount), Mathf.Max(minVerticalAmount, maxVerticalAmount));
    }
}
