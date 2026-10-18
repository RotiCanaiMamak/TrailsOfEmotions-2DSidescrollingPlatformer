using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class LandmineShockwaveManager : MonoBehaviour
{
    private const string DefaultWaveDistancePropertyName = "_WaveDistanceFromCenter";

    private sealed class ShockwaveSlot
    {
        public SpriteRenderer Renderer;
        public Material SourceMaterial;
        public Material RuntimeMaterial;
        public Coroutine Routine;
        public float StartedAt;
    }

    public static LandmineShockwaveManager Instance { get; private set; }

    [Header("Shockwave Sprites")]
    [SerializeField] private SpriteRenderer[] shockwaveSprites;
    [Tooltip("When all sprites are busy, restart the oldest wave. Otherwise skip the new trigger so active waves finish.")]
    [SerializeField] private bool restartOldestWhenFull = false;

    [Header("Wave")]
    [Min(0f)] [SerializeField] private float shockwaveDuration = 0.6f;
    [SerializeField] private string waveDistancePropertyName = DefaultWaveDistancePropertyName;

    private ShockwaveSlot[] slots;
    private int waveDistancePropertyId;
    private bool warnedMissingSprites;
    private bool warnedMissingMaterialProperty;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        CacheWaveDistanceProperty();
        BuildSlots();
        HideAllSprites();
    }

    private void OnValidate()
    {
        shockwaveDuration = Mathf.Max(0f, shockwaveDuration);
        if (string.IsNullOrWhiteSpace(waveDistancePropertyName))
        {
            waveDistancePropertyName = DefaultWaveDistancePropertyName;
        }

        CacheWaveDistanceProperty();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        ReleaseRuntimeMaterials();
    }

    private void OnDisable()
    {
        if (slots != null)
        {
            foreach (ShockwaveSlot slot in slots)
            {
                StopSlot(slot);
            }
        }

        HideAllSprites();
    }

    public void PlayAt(Vector2 worldPosition)
    {
        PlayAtInternal(worldPosition, false);
    }

    public void PlayAtWithPriority(Vector2 worldPosition)
    {
        PlayAtInternal(worldPosition, true);
    }

    private void PlayAtInternal(Vector2 worldPosition, bool forceRestartOldest)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        ShockwaveSlot slot = PickSlot(forceRestartOldest);
        if (slot == null || slot.Renderer == null)
        {
            return;
        }

        StopSlot(slot);
        EnsureRuntimeMaterial(slot);

        Transform spriteTransform = slot.Renderer.transform;
        spriteTransform.position = new Vector3(
            worldPosition.x,
            worldPosition.y,
            spriteTransform.position.z);

        slot.Renderer.enabled = true;
        slot.StartedAt = Time.time;

        if (shockwaveDuration <= 0f)
        {
            SetWaveDistance(slot, 1f);
            slot.Renderer.enabled = false;
            return;
        }

        SetWaveDistance(slot, 0f);
        slot.Routine = StartCoroutine(PlayWave(slot, shockwaveDuration));
    }

    private IEnumerator PlayWave(ShockwaveSlot slot, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            yield return null;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetWaveDistance(slot, t);
        }

        SetWaveDistance(slot, 1f);
        if (slot.Renderer != null)
        {
            slot.Renderer.enabled = false;
        }

        slot.Routine = null;
    }

    private ShockwaveSlot PickSlot(bool forceRestartOldest)
    {
        if (slots == null || slots.Length != (shockwaveSprites != null ? shockwaveSprites.Length : 0))
        {
            BuildSlots();
            HideAllSprites();
        }

        if (slots == null || slots.Length == 0)
        {
            WarnMissingSprites();
            return null;
        }

        ShockwaveSlot oldestActiveSlot = null;
        for (int i = 0; i < slots.Length; i++)
        {
            ShockwaveSlot slot = slots[i];
            if (slot == null || slot.Renderer == null || !slot.Renderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (slot.Routine == null)
            {
                return slot;
            }

            if (oldestActiveSlot == null || slot.StartedAt < oldestActiveSlot.StartedAt)
            {
                oldestActiveSlot = slot;
            }
        }

        if (oldestActiveSlot == null)
        {
            WarnMissingSprites();
        }

        return (restartOldestWhenFull || forceRestartOldest) ? oldestActiveSlot : null;
    }

    private void StopSlot(ShockwaveSlot slot)
    {
        if (slot == null || slot.Routine == null)
        {
            return;
        }

        StopCoroutine(slot.Routine);
        slot.Routine = null;
    }

    private void SetWaveDistance(ShockwaveSlot slot, float distance)
    {
        if (slot == null || slot.RuntimeMaterial == null)
        {
            return;
        }

        if (!slot.RuntimeMaterial.HasProperty(waveDistancePropertyId))
        {
            WarnMissingMaterialProperty();
            return;
        }

        slot.RuntimeMaterial.SetFloat(waveDistancePropertyId, Mathf.Clamp01(distance));
    }

    private void BuildSlots()
    {
        ReleaseRuntimeMaterials();

        if (shockwaveSprites == null || shockwaveSprites.Length == 0)
        {
            slots = System.Array.Empty<ShockwaveSlot>();
            return;
        }

        slots = new ShockwaveSlot[shockwaveSprites.Length];
        for (int i = 0; i < shockwaveSprites.Length; i++)
        {
            SpriteRenderer sprite = shockwaveSprites[i];
            if (sprite == null)
            {
                continue;
            }

            // One renderer can display only one wave, even if assigned more than once.
            bool duplicate = false;
            for (int j = 0; j < i; j++)
            {
                if (shockwaveSprites[j] == sprite)
                {
                    duplicate = true;
                    break;
                }
            }

            if (duplicate)
            {
                Debug.LogWarning("[LandmineShockwaveManager] Duplicate shockwave sprite ignored. Assign a different SpriteRenderer for each simultaneous wave.", this);
                continue;
            }

            ShockwaveSlot slot = new ShockwaveSlot
            {
                Renderer = sprite
            };

            EnsureRuntimeMaterial(slot);
            slots[i] = slot;
        }
    }

    private void EnsureRuntimeMaterial(ShockwaveSlot slot)
    {
        if (slot == null || slot.Renderer == null || slot.RuntimeMaterial != null)
        {
            return;
        }

        slot.SourceMaterial = slot.Renderer.sharedMaterial;
        if (slot.SourceMaterial == null)
        {
            return;
        }

        slot.RuntimeMaterial = new Material(slot.SourceMaterial)
        {
            name = $"{slot.SourceMaterial.name} ({slot.Renderer.name} Runtime)"
        };
        slot.Renderer.material = slot.RuntimeMaterial;
    }

    private void HideAllSprites()
    {
        if (slots == null)
        {
            return;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            ShockwaveSlot slot = slots[i];
            if (slot == null || slot.Renderer == null)
            {
                continue;
            }

            slot.Renderer.enabled = false;
        }
    }

    private void CacheWaveDistanceProperty()
    {
        if (string.IsNullOrWhiteSpace(waveDistancePropertyName))
        {
            waveDistancePropertyName = DefaultWaveDistancePropertyName;
        }

        waveDistancePropertyId = Shader.PropertyToID(waveDistancePropertyName);
    }

    private void ReleaseRuntimeMaterials()
    {
        if (slots == null)
        {
            return;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            ShockwaveSlot slot = slots[i];
            if (slot == null)
            {
                continue;
            }

            StopSlot(slot);
            if (slot.Renderer != null && slot.Renderer.sharedMaterial == slot.RuntimeMaterial)
            {
                slot.Renderer.sharedMaterial = slot.SourceMaterial;
            }

            if (slot.RuntimeMaterial != null)
            {
                Destroy(slot.RuntimeMaterial);
                slot.RuntimeMaterial = null;
            }

            slot.SourceMaterial = null;
        }
    }

    private void WarnMissingSprites()
    {
        if (warnedMissingSprites)
        {
            return;
        }

        Debug.LogWarning("[LandmineShockwaveManager] Assign at least one shockwave SpriteRenderer.", this);
        warnedMissingSprites = true;
    }

    private void WarnMissingMaterialProperty()
    {
        if (warnedMissingMaterialProperty)
        {
            return;
        }

        Debug.LogWarning(
            $"[LandmineShockwaveManager] Shockwave material has no '{waveDistancePropertyName}' property.",
            this);
        warnedMissingMaterialProperty = true;
    }
}
