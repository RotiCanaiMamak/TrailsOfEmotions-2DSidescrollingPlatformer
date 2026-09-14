using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds a zipline between two arbitrary world points (either may be higher or lower
/// than the other). Spawns a Start Pole and an End Pole, then fills the gap between
/// them with a dynamic number of Rope Chunk prefabs bent along a sagging curve.
///
/// Usage:
///   1. Assign startPolePrefab / endPolePrefab / ropeChunkPrefab.
///   2. Set start/end via the Vector3 fields or SetEndpoints.
///   3. Call Build() (or use the "Build Zipline" context menu entry in the editor),
///      or just leave buildOnStart on for runtime-spawned ziplines.
///
/// Optional prefab setup for best results:
///   - Add a ZiplineAnchorPoint child to your pole prefabs to mark the exact rope
///     attachment point (e.g. top of the pole). Without one, the builder falls back
///     to poleRootPosition + Vector3.up * fallbackPoleHeight.
///   - Add a ZiplinePoleBaseAnchor child to your pole prefabs to mark the point that
///     should rest on the ground (the pole's feet). Without one, the builder plants
///     the prefab's root Transform directly at the ground position, which only looks
///     right if the prefab's pivot is already at its base.
///   - Add a RopeChunkSegment to your rope chunk prefab so the builder knows its
///     native length and pivot, letting it stretch chunks to fit without gaps.
/// </summary>
public class ZiplineBuilder : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject startPolePrefab;
    public GameObject endPolePrefab;
    public GameObject ropeChunkPrefab;

    [Header("Endpoints")]
    public Vector3 startPoint = Vector3.zero;
    public Vector3 endPoint = new Vector3(10f, -4f, 0f);

    [Header("Poles")]
    [Tooltip("Used only as a fallback when a pole prefab has no ZiplineAnchorPoint child.")]
    public float fallbackPoleHeight = 2f;
    public float startPoleZRotation = 0f;
    public float endPoleZRotation = 0f;

    [Header("Sag Curve")]
    [Tooltip("Vertical sag at the curve's deepest point, in world units. Ignored if scaleSagWithDistance is on.")]
    public float sagAmount = 2f;
    [Tooltip("If on, sag scales with the straight-line span instead of using a fixed amount.")]
    public bool scaleSagWithDistance = false;
    [Tooltip("Sag = span distance * this factor, when scaleSagWithDistance is on.")]
    public float sagFactorOfDistance = 0.12f;
    [Range(0.05f, 0.95f)]
    [Tooltip("Where along the rope (0 = start, 1 = end) the curve dips deepest. 0.5 = symmetric. " +
             "Nudge towards the lower endpoint for a slightly more authentic hanging-cable look.")]
    public float sagPeakBias = 0.5f;
    [Tooltip("Samples used to build the curve's arc-length lookup table. Higher = smoother chunk fit on long/steep ziplines.")]
    [Range(8, 256)]
    public int curveSamples = 48;

    [Header("Rope Chunks")]
    [Tooltip("Approximate desired length of each rope chunk before the count is finalised.")]
    public float targetChunkLength = 1.5f;
    public int minChunkCount = 3;
    public int maxChunkCount = 60;

    [Header("Runtime")]
    public bool buildOnStart = true;

    private sealed class ZiplineSpan
    {
        public Vector3 startAnchorWorld;
        public Vector3 endAnchorWorld;
        public Vector3[] samplePos;
        public float[] cumLen;
        public float totalLength;
    }

    private readonly List<GameObject> spawned = new List<GameObject>();
    private readonly List<ZiplineSpan> spans = new List<ZiplineSpan>();
    private Vector3[] samplePos;
    private float[] cumLen;
    private float totalLength;
    private Vector3 p0, p1;
    private bool openSegment;
    private float chainTotalLength;

    public float TotalLength => openSegment ? 0f : chainTotalLength;
    public Vector3 StartAnchorWorld => spans.Count > 0 ? spans[0].startAnchorWorld : p0;
    public Vector3 EndAnchorWorld => spans.Count > 0 ? spans[spans.Count - 1].endAnchorWorld : p1;

    void OnValidate()
    {
        minChunkCount = Mathf.Max(1, minChunkCount);
        maxChunkCount = Mathf.Max(minChunkCount, maxChunkCount);
        targetChunkLength = Mathf.Max(0.05f, targetChunkLength);
    }

    void Start()
    {
        if (buildOnStart)
        {
            Build();
        }
    }

    /// <summary>Sets new world-space endpoints and immediately rebuilds.</summary>
    public void SetEndpoints(Vector3 newStart, Vector3 newEnd)
    {
        startPoint = newStart;
        endPoint = newEnd;
        Build();
    }

    /// <summary>Copies the given Transforms' current positions as endpoints and immediately rebuilds.</summary>
    public void SetEndpoints(Transform newStartAnchor, Transform newEndAnchor)
    {
        if (newStartAnchor != null)
        {
            startPoint = newStartAnchor.position;
        }

        if (newEndAnchor != null)
        {
            endPoint = newEndAnchor.position;
        }

        Build();
    }

    [ContextMenu("Build Zipline")]
    public void Build()
    {
        ClearSpawned();

        if (ropeChunkPrefab == null)
        {
            Debug.LogError("[ZiplineBuilder] No ropeChunkPrefab assigned.");
            return;
        }

        Vector3 startBase = startPoint;
        Vector3 endBase = endPoint;

        BeginAt(startBase);
        CompleteAt(endBase);
    }

    /// <summary>
    /// Starts a fresh chain and spawns the first Start Pole at the supplied world position.
    /// </summary>
    public void BeginAt(Vector3 startWorldPos)
    {
        ClearSpawned();
        ResetSpanState();

        startPoint = startWorldPos;
        endPoint = startWorldPos;
        openSegment = true;

        GameObject startPoleGO = SpawnPole(startPolePrefab, startWorldPos, startPoleZRotation);
        p0 = ResolveAnchor(startPoleGO, startWorldPos);
    }

    /// <summary>
    /// Starts a new span that reuses the previous span's end pole as its start pole.
    /// </summary>
    // [Zipline Chain] Reuse the previous span's end pole as the start anchor for the next span.
    public bool BeginChainedSegment()
    {
        if (openSegment)
        {
            Debug.LogWarning("[ZiplineBuilder] BeginChainedSegment was called while a span was already open.");
            return false;
        }

        if (spans.Count == 0)
        {
            Debug.LogWarning("[ZiplineBuilder] Cannot begin a chained segment before the first span exists.");
            return false;
        }

        Vector3 chainedStart = EndAnchorWorld;

        ClearCurrentSpanSamples();
        p0 = chainedStart;
        p1 = chainedStart;
        startPoint = chainedStart;
        endPoint = chainedStart;
        openSegment = true;
        return true;
    }

    /// <summary>
    /// Completes the currently open span by spawning the End Pole and rope chunks.
    /// </summary>
    public void CompleteAt(Vector3 endWorldPos)
    {
        if (ropeChunkPrefab == null)
        {
            Debug.LogError("[ZiplineBuilder] No ropeChunkPrefab assigned.");
            return;
        }

        if (!openSegment)
        {
            Debug.LogWarning("[ZiplineBuilder] CompleteAt was called without an open span.");
            return;
        }

        endPoint = endWorldPos;
        GameObject endPoleGO = SpawnPole(endPolePrefab, endWorldPos, endPoleZRotation);
        p1 = ResolveAnchor(endPoleGO, endWorldPos);

        BuildLUT();
        SpawnRopeChunks();
        StoreCompletedSpan();

        ClearCurrentSpanSamples();
        openSegment = false;
    }

    [ContextMenu("Clear Zipline")]
    public void Clear()
    {
        ClearSpawned();
        ResetSpanState();
    }

    private void ResetSpanState()
    {
        spans.Clear();
        chainTotalLength = 0f;
        ClearCurrentSpanSamples();
        p0 = Vector3.zero;
        p1 = Vector3.zero;
        openSegment = false;
    }

    private void ClearCurrentSpanSamples()
    {
        samplePos = null;
        cumLen = null;
        totalLength = 0f;
    }

    private void ClearSpawned()
    {
        for (int i = spawned.Count - 1; i >= 0; i--)
        {
            GameObject go = spawned[i];
            if (go == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(go);
            }
            else
            {
                DestroyImmediate(go);
            }
        }

        spawned.Clear();
    }

    private void RegisterSpawned(GameObject go)
    {
        if (go != null && !spawned.Contains(go))
        {
            spawned.Add(go);
        }
    }

    private GameObject SpawnPole(GameObject prefab, Vector3 groundPosition, float zRotation)
    {
        if (prefab == null)
        {
            return null;
        }

        GameObject go = Instantiate(prefab, groundPosition, Quaternion.Euler(0f, 0f, zRotation), transform);
        RegisterSpawned(go);

        // If the prefab marks where its base/feet sit, shift the whole instance so that
        // point - rather than the prefab's root pivot - lands on the ground position.
        // This is measured post-spawn so it already accounts for zRotation correctly,
        // regardless of where the marker sits in the prefab's hierarchy.
        ZiplinePoleBaseAnchor baseAnchor = go.GetComponentInChildren<ZiplinePoleBaseAnchor>();
        if (baseAnchor != null)
        {
            Vector3 correction = groundPosition - baseAnchor.transform.position;
            go.transform.position += correction;
        }

        return go;
    }

    private Vector3 ResolveAnchor(GameObject poleInstance, Vector3 fallbackBase)
    {
        if (poleInstance != null)
        {
            ZiplineAnchorPoint marker = poleInstance.GetComponentInChildren<ZiplineAnchorPoint>();
            if (marker != null)
            {
                return marker.transform.position;
            }
        }

        return fallbackBase + Vector3.up * fallbackPoleHeight;
    }

    /// <summary>
    /// Sag curve: straight-line interpolation between a and b, plus a downward dip that is
    /// zero at both ends and reaches peakSag at t = bias. This is an artistic approximation
    /// of a hanging cable (not a true catenary) but looks correct for any combination of
    /// start/end heights since it always sags straight down from the direct line.
    /// </summary>
    private Vector3 EvaluateCurve(Vector3 a, Vector3 b, float t)
    {
        float dist = Vector3.Distance(a, b);
        float peakSag = scaleSagWithDistance ? dist * sagFactorOfDistance : sagAmount;
        Vector3 straight = Vector3.Lerp(a, b, t);
        float dip = ComputeDip(t, peakSag, sagPeakBias);
        return straight + Vector3.down * dip;
    }

    private static float ComputeDip(float t, float peakSag, float bias)
    {
        bias = Mathf.Clamp(bias, 0.05f, 0.95f);
        if (peakSag <= 0f)
        {
            return 0f;
        }

        if (t <= bias)
        {
            return peakSag * Mathf.Sin((t / bias) * (Mathf.PI * 0.5f));
        }

        return peakSag * Mathf.Sin(((1f - t) / (1f - bias)) * (Mathf.PI * 0.5f));
    }

    private void BuildLUT()
    {
        int n = Mathf.Max(8, curveSamples);
        samplePos = new Vector3[n + 1];
        cumLen = new float[n + 1];

        samplePos[0] = EvaluateCurve(p0, p1, 0f);
        cumLen[0] = 0f;

        for (int i = 1; i <= n; i++)
        {
            float t = (float)i / n;
            samplePos[i] = EvaluateCurve(p0, p1, t);
            cumLen[i] = cumLen[i - 1] + Vector3.Distance(samplePos[i - 1], samplePos[i]);
        }

        totalLength = cumLen[n];
    }

    private static Vector3 PositionAtDistance(Vector3[] positions, float[] cumulativeLengths, float segmentLength, float dist)
    {
        if (positions == null || positions.Length == 0 || cumulativeLengths == null || cumulativeLengths.Length == 0)
        {
            return Vector3.zero;
        }

        if (positions.Length == 1)
        {
            return positions[0];
        }

        dist = Mathf.Clamp(dist, 0f, segmentLength);
        int n = cumulativeLengths.Length - 1;

        int lo = 0, hi = n;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (cumulativeLengths[mid] < dist)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        int idx = Mathf.Clamp(lo, 1, n);
        float segLen = cumulativeLengths[idx] - cumulativeLengths[idx - 1];
        float segT = segLen > 1e-6f ? (dist - cumulativeLengths[idx - 1]) / segLen : 0f;
        return Vector3.Lerp(positions[idx - 1], positions[idx], segT);
    }

    private Vector3 PositionAtDistance(float dist)
    {
        return PositionAtDistance(samplePos, cumLen, totalLength, dist);
    }

    private void SpawnRopeChunks()
    {
        if (totalLength <= 0.0001f)
        {
            return;
        }

        int rawCount = Mathf.RoundToInt(totalLength / targetChunkLength);
        int count = Mathf.Clamp(rawCount, minChunkCount, maxChunkCount);
        float segLen = totalLength / count;

        RopeChunkSegment prefabSeg = ropeChunkPrefab.GetComponent<RopeChunkSegment>();
        float nativeLen = (prefabSeg != null && prefabSeg.nativeLength > 0.0001f) ? prefabSeg.nativeLength : 1f;
        RopeChunkSegment.Pivot pivot = prefabSeg != null ? prefabSeg.pivot : RopeChunkSegment.Pivot.Center;

        for (int i = 0; i < count; i++)
        {
            Vector3 a = PositionAtDistance(i * segLen);
            Vector3 b = PositionAtDistance((i + 1) * segLen);

            Vector3 dir = b - a;
            float length = dir.magnitude;
            if (length < 0.0001f)
            {
                continue;
            }

            float angleDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            Vector3 spawnPos = pivot == RopeChunkSegment.Pivot.Center ? (a + b) * 0.5f : a;

            GameObject chunkGO = Instantiate(ropeChunkPrefab, spawnPos, Quaternion.Euler(0f, 0f, angleDeg), transform);
            RegisterSpawned(chunkGO);

            Vector3 scale = chunkGO.transform.localScale;
            scale.x = length / nativeLen;
            chunkGO.transform.localScale = scale;
        }
    }

    private void StoreCompletedSpan()
    {
        if (samplePos == null || cumLen == null || samplePos.Length < 2 || totalLength <= 0.0001f)
        {
            return;
        }

        spans.Add(new ZiplineSpan
        {
            startAnchorWorld = p0,
            endAnchorWorld = p1,
            samplePos = (Vector3[])samplePos.Clone(),
            cumLen = (float[])cumLen.Clone(),
            totalLength = totalLength
        });
        chainTotalLength += totalLength;
    }

    /// <summary>Returns the world position at the given arc-length distance along the built chain.</summary>
    public bool EvaluateZipline(float t, out Vector3 worldPos, out Vector3 worldTangent)
    {
        if (spans.Count == 0 || chainTotalLength <= 0.0001f)
        {
            worldPos = transform.position;
            worldTangent = Vector3.right;
            return false;
        }

        t = Mathf.Clamp01(t);
        float dist = t * chainTotalLength;
        float travelled = 0f;

        for (int i = 0; i < spans.Count; i++)
        {
            ZiplineSpan span = spans[i];
            if (span == null || span.totalLength <= 0.0001f)
            {
                continue;
            }

            if (dist <= travelled + span.totalLength || i == spans.Count - 1)
            {
                float localDist = Mathf.Clamp(dist - travelled, 0f, span.totalLength);
                worldPos = PositionAtDistance(span.samplePos, span.cumLen, span.totalLength, localDist);

                float h = Mathf.Max(0.01f, span.totalLength * 0.001f);
                Vector3 a = PositionAtDistance(span.samplePos, span.cumLen, span.totalLength, Mathf.Max(0f, localDist - h));
                Vector3 b = PositionAtDistance(span.samplePos, span.cumLen, span.totalLength, Mathf.Min(span.totalLength, localDist + h));
                worldTangent = (b - a).normalized;
                if (worldTangent.sqrMagnitude < 0.0001f)
                {
                    worldTangent = Vector3.right;
                }

                return true;
            }

            travelled += span.totalLength;
        }

        worldPos = EndAnchorWorld;
        worldTangent = Vector3.right;
        return true;
    }

    /// <summary>
    /// Spawns and tracks evenly spaced prefab instances along the most recently completed span.
    /// Tracked instances are cleared and shifted together with the rest of the zipline.
    /// </summary>
    public int SpawnPrefabsAlongLatestSpan(
        GameObject prefab,
        int count,
        float endpointPadding,
        float verticalOffset)
    {
        if (prefab == null || count <= 0 || spans.Count == 0)
        {
            return 0;
        }

        ZiplineSpan span = spans[spans.Count - 1];
        if (span == null || span.totalLength <= 0.0001f)
        {
            return 0;
        }

        float padding = Mathf.Clamp(endpointPadding, 0f, 0.49f);
        float height = Mathf.Max(0f, verticalOffset);
        int spawnedCount = 0;

        for (int i = 0; i < count; i++)
        {
            float normalizedIndex = count == 1 ? 0.5f : i / (count - 1f);
            float t = Mathf.Lerp(padding, 1f - padding, normalizedIndex);
            Vector3 spawnPosition = PositionAtDistance(
                span.samplePos,
                span.cumLen,
                span.totalLength,
                t * span.totalLength) + Vector3.up * height;

            GameObject instance = Instantiate(prefab, spawnPosition, prefab.transform.rotation, transform);
            AbilityOrbPickup.EnsureOn(instance);
            RegisterSpawned(instance);
            spawnedCount++;
        }

        return spawnedCount;
    }

    /// <summary>
    /// Call this from a world-shift / floating-origin system
    /// whenever the world is translated, so the zipline moves with everything else without a
    /// full, more expensive rebuild.
    /// </summary>
    public void OnWorldShift(Vector3 shift)
    {
        p0 -= shift;
        p1 -= shift;
        startPoint -= shift;
        endPoint -= shift;

        if (samplePos != null)
        {
            for (int i = 0; i < samplePos.Length; i++)
            {
                samplePos[i] -= shift;
            }
        }

        for (int i = 0; i < spans.Count; i++)
        {
            ZiplineSpan span = spans[i];
            if (span == null)
            {
                continue;
            }

            span.startAnchorWorld -= shift;
            span.endAnchorWorld -= shift;

            if (span.samplePos != null)
            {
                for (int j = 0; j < span.samplePos.Length; j++)
                {
                    span.samplePos[j] -= shift;
                }
            }
        }

        for (int i = 0; i < spawned.Count; i++)
        {
            if (spawned[i] != null)
            {
                spawned[i].transform.position -= shift;
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;

        if (spans.Count > 0)
        {
            for (int i = 0; i < spans.Count; i++)
            {
                DrawSpanGizmo(spans[i].startAnchorWorld, spans[i].endAnchorWorld);
            }
        }
        else
        {
            DrawSpanGizmo(startPoint, endPoint);
        }

        Gizmos.color = Color.red;
        if (spans.Count > 0)
        {
            Gizmos.DrawSphere(StartAnchorWorld, 0.15f);
            Gizmos.DrawSphere(EndAnchorWorld, 0.15f);
        }
        else
        {
            Gizmos.DrawSphere(startPoint, 0.15f);
            Gizmos.DrawSphere(endPoint, 0.15f);
        }
    }

    private void DrawSpanGizmo(Vector3 a, Vector3 b)
    {
        int steps = 32;
        Vector3 prev = EvaluateCurve(a, b, 0f);
        for (int i = 1; i <= steps; i++)
        {
            float t = (float)i / steps;
            Vector3 cur = EvaluateCurve(a, b, t);
            Gizmos.DrawLine(prev, cur);
            prev = cur;
        }
    }
}
