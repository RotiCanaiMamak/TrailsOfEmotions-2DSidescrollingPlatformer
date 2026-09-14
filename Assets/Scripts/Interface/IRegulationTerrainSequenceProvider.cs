public interface IRegulationTerrainSequenceProvider
{
    bool TryPickRegulationSequence(out TerrainSequenceDefinition sequence);
}
