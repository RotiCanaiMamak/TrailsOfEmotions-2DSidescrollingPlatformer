using UnityEngine;

public enum SmokeVignetteSize
{
    Small,
    Big
}

public class SmokeVignetteManager : MonoBehaviour
{
    public static SmokeVignetteManager Instance { get; private set; }

    [Header("Vignettes")]
    [SerializeField] private ParticleVignetteEffect smallVignetteEffect;
    [SerializeField] private ParticleVignetteEffect bigVignetteEffect;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            return;
        }

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public bool TryGetVignette(SmokeVignetteSize size, out ParticleVignetteEffect vignetteEffect)
    {
        vignetteEffect = size == SmokeVignetteSize.Small
            ? smallVignetteEffect
            : bigVignetteEffect;

        return vignetteEffect != null;
    }
}
