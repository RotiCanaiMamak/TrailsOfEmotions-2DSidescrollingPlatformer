using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;

[RequireComponent(typeof(SpriteShapeController))]
public class TerrainChunk : MonoBehaviour
{
    private const string ShadowSurfaceColliderObjectName = "Sinking Shadow Surface Trigger";
    private const string ShadowSurfaceColliderMarkerTypeName = "TerrainSurfaceShadowCollider";
    private const int ShadowSurfaceColliderSampleCount = 32;

    [HideInInspector]
    public float depth = 15f;

    [Header("Curve Smoothing")]
    [Range(0.1f, 0.6f)]
    public float surfaceCurveStrength = 0.3f;
    [Range(0f, 1f)]
    public float cornerRoundness = 0.65f;
    [Range(0f, 1f)]
    public float bottomCurveStrength = 0.25f;

    [Header("Sinking Shadows")]
    [SerializeField] private bool generateSinkingShadowSurfaceCollider = true;

    [Header("Gameplay Surface")]
    [Tooltip("Disable for visual gap chunks that should not ground the player or be sampled as terrain.")]
    [SerializeField] private bool providesGameplaySurface = true;

    [HideInInspector] public float chunkWidth;
    [HideInInspector] public float startY;
    [HideInInspector] public float endY;
    [HideInInspector] public float cliffHeight;
    [HideInInspector] public BiomeData activeBiome;
    private SpriteShapeController ssc;

    public bool HasPuddle => _hasPuddle;
    public bool ProvidesGameplaySurface => providesGameplaySurface;

    public float RightEdgeY
    {
        get
        {
            if (cliffHeight > 0f)
            {
                return startY - cliffHeight;
            }
            return endY;
        }
    }


    private struct SurfaceSegment
    {
        public Vector3 p0;
        public Vector3 p1;
        public Vector3 p2;
        public Vector3 p3;

        public SurfaceSegment(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            this.p0 = p0;
            this.p1 = p1;
            this.p2 = p2;
            this.p3 = p3;
        }
    }

    private readonly List<SurfaceSegment> surfaceSegments = new List<SurfaceSegment>();
    private bool _bezierReady;
    private Vector3 _entryTangent = Vector3.right;
    private bool _hasPuddle;
    private float _puddleCenterX;
    private float _puddleWidth;
    private float _puddleDepth;

    void Awake()
    {
        ssc = GetComponent<SpriteShapeController>();
    }

    public void Build(Vector3 entryTangent, Vector3 exitTangent)
    {
        ssc.splineDetail = 64;
        entryTangent = NormalizedOrRight(entryTangent);
        exitTangent = NormalizedOrRight(exitTangent);
        _entryTangent = entryTangent;

        Spline spline = ssc.spline;
        spline.Clear();
        spline.isOpenEnded = false;

        float x0 = 0f;
        float x3 = chunkWidth;
        float localTopLeft = 0f;
        float localTopRight = RightEdgeY - startY;
        float tLen = chunkWidth * Mathf.Clamp(surfaceCurveStrength, 0.1f, 0.6f);
        float sideTLen = Mathf.Min(depth, tLen) * Mathf.Clamp01(cornerRoundness) * 0.55f;
        float bottomTLen = chunkWidth * Mathf.Clamp01(bottomCurveStrength) * 0.25f;

        BuildSurfaceSegments(entryTangent, exitTangent, tLen, localTopRight);

        spline.InsertPointAt(0, new Vector3(x3, localTopRight, 0f));
        spline.SetTangentMode(0, ShapeTangentMode.Broken);  
        SurfaceSegment lastSurfaceSegment = surfaceSegments[surfaceSegments.Count - 1];
        spline.SetLeftTangent(0, lastSurfaceSegment.p2 - lastSurfaceSegment.p3);
        spline.SetRightTangent(0, Vector3.down * sideTLen);

        spline.InsertPointAt(1, new Vector3(x3, localTopRight - depth, 0f));
        spline.SetTangentMode(1, ShapeTangentMode.Broken);
        spline.SetLeftTangent(1, Vector3.up * sideTLen);
        spline.SetRightTangent(1, Vector3.left * bottomTLen);

        spline.InsertPointAt(2, new Vector3(x0, localTopLeft - depth, 0f));
        spline.SetTangentMode(2, ShapeTangentMode.Broken);
        spline.SetLeftTangent(2, Vector3.right * bottomTLen);
        spline.SetRightTangent(2, Vector3.up * sideTLen);

        spline.InsertPointAt(3, new Vector3(x0, localTopLeft, 0f));
        spline.SetTangentMode(3, ShapeTangentMode.Broken);
        spline.SetLeftTangent(3, Vector3.down * sideTLen);
        spline.SetRightTangent(3, surfaceSegments[0].p1 - surfaceSegments[0].p0);

        for (int i = 0; i < surfaceSegments.Count - 1; i++)
        {
            SurfaceSegment incoming = surfaceSegments[i];
            SurfaceSegment outgoing = surfaceSegments[i + 1];
            int pointIndex = 4 + i;
            spline.InsertPointAt(pointIndex, incoming.p3);
            spline.SetTangentMode(pointIndex, ShapeTangentMode.Broken);
            spline.SetLeftTangent(pointIndex, incoming.p2 - incoming.p3);
            spline.SetRightTangent(pointIndex, outgoing.p1 - outgoing.p0);
        }

        ConfigureGameplaySurfaceCollision();
        ssc.RefreshSpriteShape();
        _bezierReady = true;
        RefreshShadowSurfaceCollider();
    }

    public void RebuildExitTangent(Vector3 exitTangent)
    {
        Build(_entryTangent, exitTangent);
    }

    public void SetSinkingShadowSurfaceColliderEnabled(bool enabled)
    {
        generateSinkingShadowSurfaceCollider = enabled;
    }

    private void ConfigureGameplaySurfaceCollision()
    {
        if (ssc == null)
        {
            return;
        }

        ssc.autoUpdateCollider = providesGameplaySurface;

        EdgeCollider2D edgeCollider = ssc.edgeCollider;
        if (edgeCollider != null)
        {
            edgeCollider.enabled = providesGameplaySurface;
        }

        PolygonCollider2D polygonCollider = ssc.polygonCollider;
        if (polygonCollider != null)
        {
            polygonCollider.enabled = providesGameplaySurface;
        }
    }

    public void ConfigurePuddle(float surfaceT, float requestedWidth, float requestedDepth, float requestedEdgeMargin)
    {
        float safeChunkWidth = Mathf.Max(0.1f, chunkWidth);
        float edgeMargin = Mathf.Clamp(requestedEdgeMargin, 0f, Mathf.Max(0f, (safeChunkWidth - 0.1f) * 0.5f));
        float availableWidth = Mathf.Max(0.1f, safeChunkWidth - edgeMargin * 2f);

        _puddleWidth = Mathf.Clamp(requestedWidth, 0.1f, availableWidth);
        _puddleDepth = Mathf.Max(0.1f, requestedDepth);

        float halfWidth = _puddleWidth * 0.5f;
        float desiredCenterX = Mathf.Lerp(safeChunkWidth, 0f, Mathf.Clamp01(surfaceT));
        float minCenterX = edgeMargin + halfWidth;
        float maxCenterX = safeChunkWidth - edgeMargin - halfWidth;
        _puddleCenterX = minCenterX <= maxCenterX
            ? Mathf.Clamp(desiredCenterX, minCenterX, maxCenterX)
            : safeChunkWidth * 0.5f;
        _hasPuddle = true;
    }

    public bool TryGetPuddlePlacement(out Vector3 localBottomLeft, out float width, out float puddleDepth)
    {
        if (!_hasPuddle)
        {
            localBottomLeft = Vector3.zero;
            width = 0f;
            puddleDepth = 0f;
            return false;
        }

        localBottomLeft = new Vector3(_puddleCenterX - _puddleWidth * 0.5f, -_puddleDepth, 0f);
        width = _puddleWidth;
        puddleDepth = _puddleDepth;
        return true;
    }

    private static Vector3 NormalizedOrRight(Vector3 value)
    {
        return value.sqrMagnitude > 0.0001f ? value.normalized : Vector3.right;
    }

    /// <summary>
    /// Evaluates the surface curve at normalized horizontal position <paramref name="t"/> in [0, 1].
    /// The existing terrain convention is preserved: 0 is the right edge and 1 is the left edge.
    /// Outputs world-space position and upward normal.
    /// </summary>
    public bool EvaluateSurface(float t, out Vector3 worldPos, out Vector3 worldNormal)
    {
        if (!_bezierReady)
        {
            worldPos = transform.position;
            worldNormal = Vector3.up;
            return false;
        }

        float localX = Mathf.Lerp(chunkWidth, 0f, Mathf.Clamp01(t));
        EvaluateSurfaceAtLocalX(localX, out Vector3 localPos, out Vector3 deriv);

        // Surface segments travel left to right, so rotate their tangent counter-clockwise.
        Vector3 localNormal = new Vector3(-deriv.y, deriv.x, 0f).normalized;
        if (localNormal.sqrMagnitude < 0.01f)
        {
            localNormal = Vector3.up;
        }

        worldPos = transform.TransformPoint(localPos);
        worldNormal = transform.TransformDirection(localNormal);
        return true;
    }

    /// <summary>
    /// Evaluates the bottom curve at normalized horizontal position <paramref name="t"/> in [0, 1].
    /// Uses the same convention as EvaluateSurface: 0 is the right edge and 1 is the left edge.
    /// </summary>
    public bool EvaluateBottom(float t, out Vector3 worldPos)
    {
        if (!_bezierReady)
        {
            worldPos = transform.position;
            return false;
        }

        float localX = Mathf.Lerp(chunkWidth, 0f, Mathf.Clamp01(t));
        EvaluateBottomAtLocalX(localX, out Vector3 localPos);
        worldPos = transform.TransformPoint(localPos);
        return true;
    }

    private void BuildSurfaceSegments(
        Vector3 entryTangent,
        Vector3 exitTangent,
        float tangentLength,
        float localTopRight)
    {
        surfaceSegments.Clear();

        Vector3 leftEdge = Vector3.zero;
        Vector3 rightEdge = new Vector3(chunkWidth, localTopRight, 0f);
        if (!_hasPuddle)
        {
            surfaceSegments.Add(new SurfaceSegment(
                leftEdge,
                leftEdge + entryTangent * tangentLength,
                rightEdge - exitTangent * tangentLength,
                rightEdge));
            return;
        }

        float halfWidth = _puddleWidth * 0.5f;
        Vector3 leftRim = new Vector3(_puddleCenterX - halfWidth, 0f, 0f);
        Vector3 basinBottom = new Vector3(_puddleCenterX, -_puddleDepth, 0f);
        Vector3 rightRim = new Vector3(_puddleCenterX + halfWidth, 0f, 0f);

        float leftOuterSpan = Mathf.Max(0.01f, leftRim.x - leftEdge.x);
        float rightOuterSpan = Mathf.Max(0.01f, rightEdge.x - rightRim.x);
        float leftEntryHandle = Mathf.Min(tangentLength, leftOuterSpan * 0.4f);
        float rightExitHandle = Mathf.Min(tangentLength, rightOuterSpan * 0.4f);
        float leftRimHandle = leftOuterSpan * 0.3f;
        float rightRimHandle = rightOuterSpan * 0.3f;
        float bankHandle = halfWidth * 0.45f;

        surfaceSegments.Add(new SurfaceSegment(
            leftEdge,
            leftEdge + entryTangent * leftEntryHandle,
            leftRim - Vector3.right * leftRimHandle,
            leftRim));
        surfaceSegments.Add(new SurfaceSegment(
            leftRim,
            leftRim + Vector3.right * bankHandle,
            basinBottom - Vector3.right * bankHandle,
            basinBottom));
        surfaceSegments.Add(new SurfaceSegment(
            basinBottom,
            basinBottom + Vector3.right * bankHandle,
            rightRim - Vector3.right * bankHandle,
            rightRim));
        surfaceSegments.Add(new SurfaceSegment(
            rightRim,
            rightRim + Vector3.right * rightRimHandle,
            rightEdge - exitTangent * rightExitHandle,
            rightEdge));
    }

    private void EvaluateSurfaceAtLocalX(float localX, out Vector3 localPos, out Vector3 derivative)
    {
        localX = Mathf.Clamp(localX, 0f, chunkWidth);
        if (localX <= 0.0001f)
        {
            SurfaceSegment first = surfaceSegments[0];
            localPos = first.p0;
            derivative = EvaluateCubicDerivative(first, 0f);
            return;
        }
        if (localX >= chunkWidth - 0.0001f)
        {
            SurfaceSegment last = surfaceSegments[surfaceSegments.Count - 1];
            localPos = last.p3;
            derivative = EvaluateCubicDerivative(last, 1f);
            return;
        }

        SurfaceSegment segment = surfaceSegments[surfaceSegments.Count - 1];
        foreach (SurfaceSegment candidate in surfaceSegments)
        {
            segment = candidate;
            if (localX <= candidate.p3.x + 0.0001f)
            {
                break;
            }
        }

        float low = 0f;
        float high = 1f;
        for (int i = 0; i < 14; i++)
        {
            float mid = (low + high) * 0.5f;
            if (EvaluateCubic(segment, mid).x < localX)
            {
                low = mid;
            }
            else
            {
                high = mid;
            }
        }

        float u = (low + high) * 0.5f;
        localPos = EvaluateCubic(segment, u);
        derivative = EvaluateCubicDerivative(segment, u);
    }

    private void EvaluateBottomAtLocalX(float localX, out Vector3 localPos)
    {
        localX = Mathf.Clamp(localX, 0f, chunkWidth);

        float localTopRight = RightEdgeY - startY;
        float bottomTLen = chunkWidth * Mathf.Clamp01(bottomCurveStrength) * 0.25f;
        SurfaceSegment bottomSegment = new SurfaceSegment(
            new Vector3(chunkWidth, localTopRight - depth, 0f),
            new Vector3(chunkWidth - bottomTLen, localTopRight - depth, 0f),
            new Vector3(bottomTLen, -depth, 0f),
            new Vector3(0f, -depth, 0f));

        if (localX <= 0.0001f)
        {
            localPos = bottomSegment.p3;
            return;
        }
        if (localX >= chunkWidth - 0.0001f)
        {
            localPos = bottomSegment.p0;
            return;
        }

        float low = 0f;
        float high = 1f;
        for (int i = 0; i < 14; i++)
        {
            float mid = (low + high) * 0.5f;
            if (EvaluateCubic(bottomSegment, mid).x > localX)
            {
                low = mid;
            }
            else
            {
                high = mid;
            }
        }

        localPos = EvaluateCubic(bottomSegment, (low + high) * 0.5f);
    }

    private void RefreshShadowSurfaceCollider()
    {
        if (!generateSinkingShadowSurfaceCollider)
        {
            return;
        }

        if (!_bezierReady)
        {
            return;
        }

        Transform surfaceColliderTransform =
            transform.Find(ShadowSurfaceColliderObjectName);
        if (surfaceColliderTransform == null)
        {
            GameObject surfaceColliderObject =
                new GameObject(ShadowSurfaceColliderObjectName);
            surfaceColliderTransform = surfaceColliderObject.transform;
            surfaceColliderTransform.SetParent(transform, false);
        }

        surfaceColliderTransform.localPosition = Vector3.zero;
        surfaceColliderTransform.localRotation = Quaternion.identity;
        surfaceColliderTransform.localScale = Vector3.one;

        EdgeCollider2D surfaceCollider =
            surfaceColliderTransform.GetComponent<EdgeCollider2D>();
        if (surfaceCollider == null)
        {
            surfaceCollider =
                surfaceColliderTransform.gameObject.AddComponent<EdgeCollider2D>();
        }

        surfaceCollider.isTrigger = true;
        surfaceCollider.points = BuildShadowSurfaceColliderPoints(
            surfaceColliderTransform);
        EnsureShadowSurfaceMarker(surfaceColliderTransform.gameObject);
    }

    private Vector2[] BuildShadowSurfaceColliderPoints(Transform targetTransform)
    {
        int sampleCount = Mathf.Max(2, ShadowSurfaceColliderSampleCount);
        Vector2[] points = new Vector2[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float surfaceT = 1f - i / (float)(sampleCount - 1);
            if (!EvaluateSurface(surfaceT, out Vector3 worldPos, out _))
            {
                points[i] = Vector2.zero;
                continue;
            }

            Vector3 localPos = targetTransform.InverseTransformPoint(worldPos);
            points[i] = new Vector2(localPos.x, localPos.y);
        }

        return points;
    }

    private static void EnsureShadowSurfaceMarker(GameObject target)
    {
        if (target == null ||
            target.GetComponent(ShadowSurfaceColliderMarkerTypeName) != null)
        {
            return;
        }

        System.Type markerType =
            System.Type.GetType(ShadowSurfaceColliderMarkerTypeName) ??
            System.Type.GetType($"{ShadowSurfaceColliderMarkerTypeName}, Assembly-CSharp");
        if (markerType == null || !typeof(Component).IsAssignableFrom(markerType))
        {
            return;
        }

        target.AddComponent(markerType);
    }

    private static Vector3 EvaluateCubic(SurfaceSegment segment, float t)
    {
        float u = 1f - t;
        return
            u * u * u * segment.p0
            + 3f * u * u * t * segment.p1
            + 3f * u * t * t * segment.p2
            + t * t * t * segment.p3;
    }

    private static Vector3 EvaluateCubicDerivative(SurfaceSegment segment, float t)
    {
        float u = 1f - t;
        return 3f * (
            u * u * (segment.p1 - segment.p0)
            + 2f * u * t * (segment.p2 - segment.p1)
            + t * t * (segment.p3 - segment.p2));
    }
}
