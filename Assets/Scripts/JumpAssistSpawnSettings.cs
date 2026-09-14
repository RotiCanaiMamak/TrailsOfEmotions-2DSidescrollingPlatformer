using UnityEngine;

[System.Serializable]
public sealed class JumpAssistSpawnSettings
{
    [Header("Ramp Placement")]
    [SerializeField] private bool alignRampToSurface = true;

    [Header("Orb Pattern Prefabs")]
    [SerializeField] private GameObject[] orbPatternPrefabs;
    [SerializeField] private bool alignOrbPatternToSurface;
    [Tooltip("X is a world-space offset from the ramp surface X. Y is height from the resolved terrain surface.")]
    [SerializeField] private Vector2 orbPatternLocalOffset = new Vector2(0f, 2f);
    [SerializeField] private bool addPickupToDirectOrbChildren = true;

    public float OrbPatternWorldXOffset => orbPatternLocalOffset.x;

    public bool TrySpawn(
        TerrainChunk ownerChunk,
        GameObject rampPrefab,
        Vector3 surfacePosition,
        Vector3 surfaceNormal,
        out GameObject ramp,
        out GameObject orbPattern,
        bool allowRamp = true,
        JumpAssistRampMarker.Source rampSource = JumpAssistRampMarker.Source.Obstacle,
        bool useOrbSurfaceOverride = false,
        TerrainChunk orbOwnerChunk = null,
        Vector3 orbSurfacePosition = default,
        Vector3 orbSurfaceNormal = default)
    {
        ramp = null;
        orbPattern = null;
        if (ownerChunk == null)
        {
            return false;
        }

        Transform parent = ownerChunk.transform;
        Vector3 safeNormal = SafeSurfaceNormal(surfaceNormal);
        Quaternion surfaceRotation = SurfaceRotation(safeNormal);

        GameObject selectedRampPrefab = allowRamp ? rampPrefab : null;
        if (selectedRampPrefab != null)
        {
            Vector3 rampPosition = surfacePosition;
            Quaternion rampRotation = alignRampToSurface
                ? surfaceRotation
                : selectedRampPrefab.transform.rotation;

            ramp = Object.Instantiate(selectedRampPrefab, rampPosition, rampRotation, parent);
            SnapBottomRootToPosition(ramp, rampPosition);
            EnsureRampSurface(ramp);
            EnsureRampMarker(ramp, rampSource);
        }

        GameObject orbPatternPrefab = PickPrefab(orbPatternPrefabs);
        if (orbPatternPrefab != null)
        {
            TerrainChunk patternOwnerChunk = useOrbSurfaceOverride && orbOwnerChunk != null
                ? orbOwnerChunk
                : ownerChunk;
            Transform patternParent = patternOwnerChunk.transform;
            Vector3 patternPosition;
            Vector3 patternNormal;
            if (useOrbSurfaceOverride)
            {
                patternNormal = SafeSurfaceNormal(orbSurfaceNormal);
                patternPosition = orbSurfacePosition + patternNormal * orbPatternLocalOffset.y;
            }
            else
            {
                Vector3 patternAnchor = ramp != null ? ramp.transform.position : surfacePosition;
                patternNormal = safeNormal;
                patternPosition = patternAnchor + SurfaceOffset(orbPatternLocalOffset, safeNormal);
            }

            Quaternion patternRotation = alignOrbPatternToSurface
                ? SurfaceRotation(patternNormal)
                : orbPatternPrefab.transform.rotation;

            orbPattern = Object.Instantiate(orbPatternPrefab, patternPosition, patternRotation, patternParent);
            if (addPickupToDirectOrbChildren)
            {
                EnsurePickupOnDirectOrbChildren(orbPattern);
            }
        }

        return ramp != null || orbPattern != null;
    }

    public void OnValidate()
    {
        if (orbPatternPrefabs == null)
        {
            orbPatternPrefabs = System.Array.Empty<GameObject>();
        }
    }

    private static GameObject PickPrefab(GameObject[] prefabs)
    {
        int validCount = 0;
        if (prefabs == null)
        {
            return null;
        }

        for (int i = 0; i < prefabs.Length; i++)
        {
            if (prefabs[i] != null)
            {
                validCount++;
            }
        }

        if (validCount <= 0)
        {
            return null;
        }

        int selectedIndex = Random.Range(0, validCount);
        for (int i = 0; i < prefabs.Length; i++)
        {
            if (prefabs[i] == null)
            {
                continue;
            }

            if (selectedIndex == 0)
            {
                return prefabs[i];
            }

            selectedIndex--;
        }

        return null;
    }

    private static Vector3 SurfaceOffset(Vector2 localOffset, Vector3 surfaceNormal)
    {
        Vector3 normal = SafeSurfaceNormal(surfaceNormal);
        Vector3 forward = new Vector3(normal.y, -normal.x, 0f);
        if (forward.x < 0f)
        {
            forward = -forward;
        }

        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.right;
        }

        return forward.normalized * localOffset.x + normal * localOffset.y;
    }

    private static Vector3 SafeSurfaceNormal(Vector3 surfaceNormal)
    {
        return surfaceNormal.sqrMagnitude > 0.0001f ? surfaceNormal.normalized : Vector3.up;
    }

    private static Quaternion SurfaceRotation(Vector3 surfaceNormal)
    {
        return Quaternion.Euler(
            0f,
            0f,
            Mathf.Atan2(surfaceNormal.x, surfaceNormal.y) * Mathf.Rad2Deg * -1f);
    }

    private static void SnapBottomRootToPosition(GameObject instance, Vector3 targetPosition)
    {
        if (instance == null)
        {
            return;
        }

        TerrainFeatureBottomRoot bottomRoot = instance.GetComponentInChildren<TerrainFeatureBottomRoot>();
        if (bottomRoot == null)
        {
            return;
        }

        instance.transform.position += targetPosition - bottomRoot.transform.position;
    }

    private static void EnsureRampSurface(GameObject ramp)
    {
        if (ramp == null || ramp.GetComponentInChildren<RampSurface>(true) != null)
        {
            return;
        }

        ramp.AddComponent<RampSurface>();
    }

    private static void EnsureRampMarker(GameObject ramp, JumpAssistRampMarker.Source rampSource)
    {
        if (ramp == null)
        {
            return;
        }

        JumpAssistRampMarker marker = ramp.GetComponent<JumpAssistRampMarker>();
        if (marker == null)
        {
            marker = ramp.AddComponent<JumpAssistRampMarker>();
        }

        marker.Initialize(rampSource);
    }

    private static void EnsurePickupOnDirectOrbChildren(GameObject patternRoot)
    {
        if (patternRoot == null)
        {
            return;
        }

        if (patternRoot.transform.childCount == 0)
        {
            AbilityOrbPickup.EnsureOn(patternRoot);
            return;
        }

        for (int i = 0; i < patternRoot.transform.childCount; i++)
        {
            Transform child = patternRoot.transform.GetChild(i);
            if (child != null)
            {
                AbilityOrbPickup.EnsureOn(child.gameObject);
            }
        }
    }
}
