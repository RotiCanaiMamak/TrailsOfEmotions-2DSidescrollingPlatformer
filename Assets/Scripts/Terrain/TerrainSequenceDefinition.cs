using UnityEngine;

[CreateAssetMenu(fileName = "NewTerrainSequence", menuName = "Game/Terrain Sequence Definition")]
public class TerrainSequenceDefinition : ScriptableObject
{
    [Header("Display")]
    public string displayName = "New Terrain Sequence";

    [Header("Chunks")]
    [Tooltip("Generated in order when this sequence is rolled by a biome.")]
    public TerrainChunkDefinition[] chunks;

    public bool HasChunks => chunks != null && chunks.Length > 0;

    public TerrainChunkDefinition FirstChunk => HasChunks ? chunks[0] : null;
}
