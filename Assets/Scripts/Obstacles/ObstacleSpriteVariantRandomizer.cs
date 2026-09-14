using UnityEngine;

public class ObstacleSpriteVariantRandomizer : MonoBehaviour
{
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private Sprite[] variants;
    [SerializeField] private Sprite[] activatedVariants;

    private bool warnedMissingActivatedVariant;

    public int SelectedVariantIndex { get; private set; } = -1;
    public SpriteRenderer TargetRenderer => targetRenderer;

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
        PickRandomVariant();
    }

    public bool SwitchToActivatedVariant()
    {
        ResolveReferences();

        if (targetRenderer == null)
        {
            WarnMissingActivatedVariant("No target renderer is assigned.");
            return false;
        }

        int variantIndex = SelectedVariantIndex >= 0 ? SelectedVariantIndex : FindCurrentVariantIndex();
        if (variantIndex < 0)
        {
            WarnMissingActivatedVariant("Could not match the current sprite to a base variant.");
            return false;
        }

        if (activatedVariants == null || variantIndex >= activatedVariants.Length || activatedVariants[variantIndex] == null)
        {
            WarnMissingActivatedVariant($"No activated variant sprite is assigned for variant index {variantIndex}.");
            return false;
        }

        targetRenderer.sprite = activatedVariants[variantIndex];
        return true;
    }

    private void ResolveReferences()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }
    }

    private void PickRandomVariant()
    {
        if (targetRenderer == null || variants == null)
        {
            return;
        }

        int validVariantCount = CountValidVariants();
        if (validVariantCount <= 0)
        {
            return;
        }

        int validVariantIndex = Random.Range(0, validVariantCount);
        SelectedVariantIndex = GetValidVariantRawIndex(validVariantIndex);

        if (SelectedVariantIndex >= 0)
        {
            targetRenderer.sprite = variants[SelectedVariantIndex];
        }
    }

    private int CountValidVariants()
    {
        int count = 0;
        for (int i = 0; i < variants.Length; i++)
        {
            if (variants[i] != null)
            {
                count++;
            }
        }

        return count;
    }

    private int GetValidVariantRawIndex(int index)
    {
        for (int i = 0; i < variants.Length; i++)
        {
            Sprite variant = variants[i];
            if (variant == null)
            {
                continue;
            }

            if (index <= 0)
            {
                return i;
            }

            index--;
        }

        return -1;
    }

    private int FindCurrentVariantIndex()
    {
        if (targetRenderer == null || variants == null)
        {
            return -1;
        }

        for (int i = 0; i < variants.Length; i++)
        {
            if (variants[i] != null && targetRenderer.sprite == variants[i])
            {
                SelectedVariantIndex = i;
                return i;
            }
        }

        return -1;
    }

    private void WarnMissingActivatedVariant(string message)
    {
        if (warnedMissingActivatedVariant)
        {
            return;
        }

        Debug.LogWarning($"[{nameof(ObstacleSpriteVariantRandomizer)}] {message}", this);
        warnedMissingActivatedVariant = true;
    }
}
