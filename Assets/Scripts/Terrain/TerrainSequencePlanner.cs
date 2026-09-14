using System;
using System.Collections.Generic;

public readonly struct PlannedTerrainChunk
{
    public readonly TerrainSequenceDefinition Sequence;
    public readonly TerrainChunkDefinition Chunk;
    public readonly int SequenceChunkIndex;
    public readonly int SequenceChunkCount;

    public PlannedTerrainChunk(TerrainSequenceDefinition sequence, TerrainChunkDefinition chunk)
        : this(sequence, chunk, 0, 1)
    {
    }

    public PlannedTerrainChunk(
        TerrainSequenceDefinition sequence,
        TerrainChunkDefinition chunk,
        int sequenceChunkIndex,
        int sequenceChunkCount)
    {
        Sequence = sequence;
        Chunk = chunk;
        SequenceChunkIndex = sequenceChunkIndex;
        SequenceChunkCount = sequenceChunkCount;
    }

    public bool IsValid => Sequence != null && Chunk != null;
    public bool IsLastChunkInSequence => SequenceChunkCount > 0 && SequenceChunkIndex >= SequenceChunkCount - 1;
}

public class TerrainSequencePlanner
{
    private readonly Queue<PlannedTerrainChunk> queuedChunks = new Queue<PlannedTerrainChunk>();

    public PlannedTerrainChunk NextTerrainChunk(Func<TerrainSequenceDefinition> pickTerrainSequence)
    {
        if (queuedChunks.Count > 0)
        {
            return DequeueNext();
        }

        QueueSequence(pickTerrainSequence?.Invoke());
        return DequeueNext();
    }

    public PlannedTerrainChunk StartSequenceNow(TerrainSequenceDefinition sequence)
    {
        ClearQueuedSequence();
        QueueSequence(sequence);
        return DequeueNext();
    }

    public void ClearQueuedSequence()
    {
        queuedChunks.Clear();
    }

    private void QueueSequence(TerrainSequenceDefinition sequence)
    {
        ClearQueuedSequence();
        if (sequence == null || sequence.chunks == null)
        {
            return;
        }

        int sequenceChunkCount = 0;
        foreach (TerrainChunkDefinition chunk in sequence.chunks)
        {
            if (chunk != null)
            {
                sequenceChunkCount++;
            }
        }

        int sequenceChunkIndex = 0;
        foreach (TerrainChunkDefinition chunk in sequence.chunks)
        {
            if (chunk != null)
            {
                queuedChunks.Enqueue(new PlannedTerrainChunk(sequence, chunk, sequenceChunkIndex, sequenceChunkCount));
                sequenceChunkIndex++;
            }
        }
    }

    private PlannedTerrainChunk DequeueNext()
    {
        if (queuedChunks.Count == 0)
        {
            return default;
        }

        PlannedTerrainChunk plannedChunk = queuedChunks.Dequeue();
        return plannedChunk;
    }
}
