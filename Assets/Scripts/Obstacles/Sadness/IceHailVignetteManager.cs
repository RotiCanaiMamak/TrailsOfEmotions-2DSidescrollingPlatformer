using UnityEngine;

public class IceHailVignetteManager : MonoBehaviour
{
    public static IceHailVignetteManager Instance { get; private set; }

    [Header("Vignette")]
    [SerializeField] private ParticleVignetteEffect iceHailVignetteEffect;

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

    public bool TryGetVignette(out ParticleVignetteEffect vignetteEffect)
    {
        vignetteEffect = iceHailVignetteEffect;
        return vignetteEffect != null;
    }
}
