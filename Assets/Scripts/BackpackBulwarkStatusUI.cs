using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BackpackBulwarkStatusUI : MonoBehaviour
{
    private const string PlayerTag = "Player";

    [Header("Sources")]
    [SerializeField] private LinaCharacterAbility ability;
    [SerializeField] private LinaBackpackBulwark bulwark;
    [Tooltip("Optional player root used to auto-resolve Backpack Bulwark references.")]
    [SerializeField] private GameObject sourceRoot;

    [Header("UI")]
    [SerializeField] private Image blackOverlay;

    [Header("Overlay")]
    [SerializeField] private Color overlayColor = new Color(0f, 0f, 0f, 0.5f);

    private bool warnedMissingOverlay;
    private bool warnedMissingSource;

    private void Awake()
    {
        ResolveReferences();
        ResolveOverlayImage();
        ApplyOverlayColor();
        RefreshOverlay(IsBulwarkActive());
    }

    private void OnEnable()
    {
        ResolveReferences();
        ResolveOverlayImage();
        ApplyOverlayColor();
        RefreshOverlay(IsBulwarkActive());
    }

    private void Update()
    {
        ResolveReferences();
        ResolveOverlayImage();
        ApplyOverlayColor();

        bool isBulwarkActive = IsBulwarkActive();
        RefreshOverlay(isBulwarkActive);
    }

    private void OnValidate()
    {
        ResolveOverlayImage();
        ApplyOverlayColor();
    }

    private void ResolveReferences()
    {
        if (ability != null && bulwark != null)
        {
            return;
        }

        if (TryResolveFrom(sourceRoot))
        {
            return;
        }

        if (TryResolveFrom(gameObject))
        {
            return;
        }

        Transform parent = transform.parent;
        while (parent != null)
        {
            if (TryResolveFrom(parent.gameObject))
            {
                return;
            }

            parent = parent.parent;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag(PlayerTag);
        if (!TryResolveFrom(playerObject))
        {
            WarnMissingSource();
        }
    }

    private bool TryResolveFrom(GameObject root)
    {
        if (root == null)
        {
            return false;
        }

        if (ability == null)
        {
            ability = root.GetComponent<LinaCharacterAbility>();
        }

        if (bulwark == null)
        {
            bulwark = root.GetComponent<LinaBackpackBulwark>();
        }

        if (ability == null)
        {
            ability = root.GetComponentInParent<LinaCharacterAbility>();
        }

        if (bulwark == null)
        {
            bulwark = root.GetComponentInParent<LinaBackpackBulwark>();
        }

        if (ability == null)
        {
            ability = root.GetComponentInChildren<LinaCharacterAbility>();
        }

        if (bulwark == null)
        {
            bulwark = root.GetComponentInChildren<LinaBackpackBulwark>();
        }

        return ability != null || bulwark != null;
    }

    private void ResolveOverlayImage()
    {
        if (blackOverlay == null)
        {
            blackOverlay = GetComponent<Image>();
        }
    }

    private void ApplyOverlayColor()
    {
        if (blackOverlay == null)
        {
            return;
        }

        blackOverlay.color = overlayColor;
        blackOverlay.raycastTarget = false;
    }

    private void RefreshOverlay(bool isBulwarkActive)
    {
        if (blackOverlay == null)
        {
            WarnMissingOverlay();
            return;
        }

        blackOverlay.fillAmount = isBulwarkActive ? 0f : 1f - GetChargeProgress();
    }

    private float GetChargeProgress()
    {
        if (ability == null)
        {
            return 0f;
        }

        return Mathf.Clamp01(ability.ActiveChargeCount / (float)ability.MaxActiveCharge);
    }

    private bool IsBulwarkActive()
    {
        return bulwark != null && bulwark.IsActive;
    }

    private void WarnMissingOverlay()
    {
        if (warnedMissingOverlay)
        {
            return;
        }

        Debug.LogWarning("[BackpackBulwarkStatusUI] Assign a UI Image for the black overlay.", this);
        warnedMissingOverlay = true;
    }

    private void WarnMissingSource()
    {
        if (warnedMissingSource)
        {
            return;
        }

        Debug.LogWarning(
            "[BackpackBulwarkStatusUI] Assign a LinaCharacterAbility or LinaBackpackBulwark source, source root, or tagged player.",
            this);
        warnedMissingSource = true;
    }

}
