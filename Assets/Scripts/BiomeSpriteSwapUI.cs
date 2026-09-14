using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BiomeSpriteSwapUI : MonoBehaviour
{
    [System.Serializable]
    public class BiomeSpriteEntry
    {
        public BiomeData biome;
        public Sprite sprite;
    }

    [Header("UI")]
    [SerializeField] private Image targetImage;

    [Header("Sprites")]
    [SerializeField] private BiomeSpriteEntry[] biomeSprites;
    [SerializeField] private Sprite fallbackSprite;
    [SerializeField] private bool clearSpriteWhenNoMatch;

    private BiomeManager biomeManager;
    private Coroutine phaseRefreshRoutine;
    private bool subscribedToBiomeManager;
    private bool warnedMissingImage;
    private bool warnedMissingBiomeManager;

    private void Reset()
    {
        ResolveTargetImage();
    }

    private void Awake()
    {
        ResolveTargetImage();
    }

    private void OnEnable()
    {
        ResolveTargetImage();
        TrySubscribeToBiomeManager();
    }

    private void Update()
    {
        if (!subscribedToBiomeManager)
        {
            TrySubscribeToBiomeManager();
        }
    }

    private void OnDisable()
    {
        StopPhaseRefreshRoutine();
        UnsubscribeFromBiomeManager();
    }

    private void OnDestroy()
    {
        StopPhaseRefreshRoutine();
        UnsubscribeFromBiomeManager();
    }

    private void OnValidate()
    {
        ResolveTargetImage();
    }

    private void OnBiomeChanged(BiomeData biome)
    {
        ApplyBiomeSprite(biome);
    }

    private void OnBiomePhaseChanged(int phaseIndex)
    {
        StopPhaseRefreshRoutine();
        phaseRefreshRoutine = StartCoroutine(ApplySpriteAfterBiomeAdvance());
    }

    private void TrySubscribeToBiomeManager()
    {
        if (subscribedToBiomeManager)
        {
            return;
        }

        biomeManager = BiomeManager.Instance;
        if (biomeManager == null)
        {
            WarnMissingBiomeManager();
            return;
        }

        biomeManager.EnsureInitialized();
        biomeManager.onBiomePhaseChanged.AddListener(OnBiomePhaseChanged);
        biomeManager.onBiomeChanged.AddListener(OnBiomeChanged);
        subscribedToBiomeManager = true;
        warnedMissingBiomeManager = false;

        ApplyBiomeSprite(biomeManager.CurrentBiome);
    }

    private void UnsubscribeFromBiomeManager()
    {
        if (!subscribedToBiomeManager || biomeManager == null)
        {
            subscribedToBiomeManager = false;
            biomeManager = null;
            return;
        }

        biomeManager.onBiomeChanged.RemoveListener(OnBiomeChanged);
        biomeManager.onBiomePhaseChanged.RemoveListener(OnBiomePhaseChanged);
        subscribedToBiomeManager = false;
        biomeManager = null;
    }

    private IEnumerator ApplySpriteAfterBiomeAdvance()
    {
        yield return null;

        if (subscribedToBiomeManager && biomeManager != null)
        {
            ApplyBiomeSprite(biomeManager.CurrentBiome);
        }

        phaseRefreshRoutine = null;
    }

    private void ApplyBiomeSprite(BiomeData biome)
    {
        if (!TryGetTargetImage(out Image image))
        {
            return;
        }

        if (TryGetSpriteForBiome(biome, out Sprite sprite))
        {
            image.sprite = sprite;
            return;
        }

        if (fallbackSprite != null)
        {
            image.sprite = fallbackSprite;
            return;
        }

        if (clearSpriteWhenNoMatch)
        {
            image.sprite = null;
        }
    }

    private bool TryGetSpriteForBiome(BiomeData biome, out Sprite sprite)
    {
        sprite = null;
        if (biome == null || biomeSprites == null)
        {
            return false;
        }

        for (int i = 0; i < biomeSprites.Length; i++)
        {
            BiomeSpriteEntry entry = biomeSprites[i];
            if (entry == null || entry.biome != biome)
            {
                continue;
            }

            sprite = entry.sprite;
            return sprite != null;
        }

        return false;
    }

    private bool TryGetTargetImage(out Image image)
    {
        ResolveTargetImage();
        image = targetImage;
        if (image != null)
        {
            return true;
        }

        WarnMissingImage();
        return false;
    }

    private void ResolveTargetImage()
    {
        if (targetImage == null)
        {
            targetImage = GetComponent<Image>();
        }
    }

    private void WarnMissingImage()
    {
        if (warnedMissingImage)
        {
            return;
        }

        Debug.LogWarning("[BiomeSpriteSwapUI] Assign a UI Image target.", this);
        warnedMissingImage = true;
    }

    private void StopPhaseRefreshRoutine()
    {
        if (phaseRefreshRoutine == null)
        {
            return;
        }

        StopCoroutine(phaseRefreshRoutine);
        phaseRefreshRoutine = null;
    }

    private void WarnMissingBiomeManager()
    {
        if (warnedMissingBiomeManager)
        {
            return;
        }

        Debug.LogWarning("[BiomeSpriteSwapUI] No BiomeManager instance found.", this);
        warnedMissingBiomeManager = true;
    }
}
