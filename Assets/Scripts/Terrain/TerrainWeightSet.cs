using UnityEngine;

public static class TerrainWeightSet
{
    public static TerrainSequenceDefinition PickSequence(
        BiomeData.TerrainWeightEntry[] entries,
        TerrainSequenceDefinition fallback = null)
    {
        if (entries == null || entries.Length == 0)
        {
            return fallback;
        }

        float total = 0f;
        foreach (BiomeData.TerrainWeightEntry entry in entries)
        {
            if (entry == null || entry.sequence == null || !entry.sequence.HasChunks)
            {
                continue;
            }

            total += Mathf.Max(0f, entry.weight);
        }

        if (total <= 0f)
        {
            return FirstValidSequence(entries) ?? fallback;
        }

        float roll = Random.Range(0f, total);
        foreach (BiomeData.TerrainWeightEntry entry in entries)
        {
            if (entry == null || entry.sequence == null || !entry.sequence.HasChunks)
            {
                continue;
            }

            roll -= Mathf.Max(0f, entry.weight);
            if (roll <= 0f)
            {
                return entry.sequence;
            }
        }

        return FirstValidSequence(entries) ?? fallback;
    }

    private static TerrainSequenceDefinition FirstValidSequence(BiomeData.TerrainWeightEntry[] entries)
    {
        foreach (BiomeData.TerrainWeightEntry entry in entries)
        {
            if (entry != null && entry.sequence != null && entry.sequence.HasChunks)
            {
                return entry.sequence;
            }
        }

        return null;
    }
}
