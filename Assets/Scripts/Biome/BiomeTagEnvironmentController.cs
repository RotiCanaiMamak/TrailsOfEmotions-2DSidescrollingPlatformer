using System.Collections.Generic;
using UnityEngine;

public class BiomeTagEnvironmentController : MonoBehaviour
{
    [System.Serializable]
    public class BiomeTagMapping
    {
        public BiomeData biome;

        public List<string> unityTags = new List<string>();

        [HideInInspector]
        public string unityTag;

        public IEnumerable<string> GetUnityTags()
        {
            if (unityTags != null)
            {
                foreach (string tag in unityTags)
                {
                    if (!string.IsNullOrWhiteSpace(tag))
                    {
                        yield return tag.Trim();
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(unityTag))
            {
                yield return unityTag.Trim();
            }
        }

        public void MigrateLegacyTag()
        {
            if (string.IsNullOrWhiteSpace(unityTag))
            {
                return;
            }

            if (unityTags == null)
            {
                unityTags = new List<string>();
            }

            string legacyTag = unityTag.Trim();
            bool alreadyAdded = false;
            foreach (string tag in unityTags)
            {
                if (tag != null && tag.Trim() == legacyTag)
                {
                    alreadyAdded = true;
                    break;
                }
            }

            if (!alreadyAdded)
            {
                unityTags.Add(legacyTag);
            }

            unityTag = "";
        }
    }

    [Header("Biome Tag Mappings")]
    [SerializeField] private List<BiomeTagMapping> biomeTagMappings = new List<BiomeTagMapping>();

    private readonly Dictionary<BiomeData, HashSet<GameObject>> cachedBiomeObjects = new Dictionary<BiomeData, HashSet<GameObject>>();
    private readonly HashSet<string> emptyTagWarnings = new HashSet<string>();
    private BiomeData activeBiome;
    private HashSet<GameObject> activeObjects;

    private void Awake()
    {
        BuildCache();
        activeBiome = null;
        activeObjects = null;
        SetCachedObjectsActive(false);
    }

    private void OnValidate()
    {
        MigrateLegacyTags();
    }

    private void Start()
    {
        if (BiomeManager.Instance == null)
        {
            Debug.LogWarning("[BiomeTagEnvironmentController] No BiomeManager instance found.");
            return;
        }

        BiomeManager.Instance.onBiomeChanged.AddListener(OnBiomeChanged);
        BiomeManager.Instance.EnsureInitialized();

        if (activeBiome != BiomeManager.Instance.CurrentBiome)
        {
            OnBiomeChanged(BiomeManager.Instance.CurrentBiome);
        }
    }

    private void OnDestroy()
    {
        if (BiomeManager.Instance != null)
        {
            BiomeManager.Instance.onBiomeChanged.RemoveListener(OnBiomeChanged);
        }
    }

    private void BuildCache()
    {
        cachedBiomeObjects.Clear();
        MigrateLegacyTags();

        foreach (BiomeTagMapping mapping in biomeTagMappings)
        {
            if (mapping == null || mapping.biome == null)
            {
                continue;
            }

            HashSet<GameObject> biomeObjects = GetBiomeObjectCache(mapping.biome);
            foreach (string unityTag in mapping.GetUnityTags())
            {
                GameObject[] taggedObjects;
                try
                {
                    taggedObjects = GameObject.FindGameObjectsWithTag(unityTag);
                }
                catch (UnityException exception)
                {
                    Debug.LogWarning($"[BiomeTagEnvironmentController] Could not find objects for tag '{unityTag}': {exception.Message}", this);
                    continue;
                }

                if (taggedObjects.Length == 0)
                {
                    string warningKey = $"{mapping.biome.GetInstanceID()}:{unityTag}";
                    if (emptyTagWarnings.Add(warningKey))
                    {
                        Debug.LogWarning($"[BiomeTagEnvironmentController] No scene objects found for tag '{unityTag}' on biome '{mapping.biome.biomeName}'.", this);
                    }
                }

                AddObjects(biomeObjects, taggedObjects);
            }
        }
    }

    private void OnBiomeChanged(BiomeData nextBiome)
    {
        HashSet<GameObject> previousObjects = activeObjects;
        BiomeData previousBiome = activeBiome;

        activeBiome = null;
        activeObjects = null;

        if (nextBiome == null || !cachedBiomeObjects.TryGetValue(nextBiome, out HashSet<GameObject> nextObjects))
        {
            SetObjectsActive(previousObjects, false);
            string nextName = nextBiome != null ? nextBiome.biomeName : "None";
            Debug.Log($"[BiomeTagEnvironmentController] Committed biome '{nextName}' has no mapped environment objects.", this);
            return;
        }

        SetObjectsActive(previousObjects, false, nextObjects);
        activeBiome = nextBiome;
        activeObjects = nextObjects;
        SetObjectsActive(activeObjects, true);

        string previousName = previousBiome != null ? previousBiome.biomeName : "None";
        Debug.Log($"[BiomeTagEnvironmentController] Environment synced: {previousName} -> {nextBiome.biomeName}; active mapped objects: {activeObjects.Count}.", this);
    }

    private HashSet<GameObject> GetBiomeObjectCache(BiomeData biome)
    {
        if (cachedBiomeObjects.TryGetValue(biome, out HashSet<GameObject> objects))
        {
            return objects;
        }

        objects = new HashSet<GameObject>();
        cachedBiomeObjects.Add(biome, objects);
        return objects;
    }

    private void SetCachedObjectsActive(bool active)
    {
        foreach (HashSet<GameObject> objects in cachedBiomeObjects.Values)
        {
            SetObjectsActive(objects, active);
        }
    }

    private static void AddObjects(HashSet<GameObject> destination, GameObject[] source)
    {
        if (destination == null || source == null)
        {
            return;
        }

        foreach (GameObject obj in source)
        {
            if (obj != null)
            {
                destination.Add(obj);
            }
        }
    }

    private static void SetObjectsActive(HashSet<GameObject> objects, bool active, HashSet<GameObject> except = null)
    {
        if (objects == null)
        {
            return;
        }

        foreach (GameObject obj in objects)
        {
            if (obj != null)
            {
                if (except != null && except.Contains(obj))
                {
                    continue;
                }

                obj.SetActive(active);
            }
        }
    }

    private void MigrateLegacyTags()
    {
        if (biomeTagMappings == null)
        {
            return;
        }

        foreach (BiomeTagMapping mapping in biomeTagMappings)
        {
            mapping?.MigrateLegacyTag();
        }
    }
}
