using UnityEngine;

/// <summary>
/// Put this on the root of a Rope Chunk prefab. It tells ZiplineBuilder how long the
/// chunk's art is along its local +X axis (at localScale.x = 1), and where the
/// prefab's pivot sits relative to that art. With this information the builder can
/// stretch each spawned chunk's localScale.x to exactly fill its segment of the sag
/// curve, with no gaps or overlaps between neighbouring chunks.
/// </summary>
public class RopeChunkSegment : MonoBehaviour
{
    public enum Pivot
    {
        /// <summary>Prefab's pivot/origin sits in the middle of the rope art (most sprites).</summary>
        Center,
        /// <summary>Prefab's pivot/origin sits at the left edge of the rope art (chain-link style assets).</summary>
        LeftEdge
    }

    [Tooltip("Length of this chunk's art along local +X when localScale.x = 1.")]
    public float nativeLength = 1f;

    [Tooltip("Where the prefab's pivot sits relative to its art. Must match how the art was authored.")]
    public Pivot pivot = Pivot.Center;
}