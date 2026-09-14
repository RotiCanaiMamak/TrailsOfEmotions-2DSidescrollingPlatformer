using UnityEngine;

public sealed class AngerRegulationVignetteManager : MonoBehaviour
{
    public static AngerRegulationVignetteManager Instance { get; private set; }

    [Header("Vignette")]
    [SerializeField] private ParticleVignetteEffect angerRegulationVignette;

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

    public bool TryGetVignette(out ParticleVignetteEffect vignette)
    {
        vignette = angerRegulationVignette;
        return vignette != null;
    }
}
