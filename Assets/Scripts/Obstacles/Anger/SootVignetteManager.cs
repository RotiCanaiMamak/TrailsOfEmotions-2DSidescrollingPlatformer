using UnityEngine;

public class SootVignetteManager : MonoBehaviour
{
    public static SootVignetteManager Instance { get; private set; }

    [Header("Vignette")]
    [SerializeField] private ParticleVignetteEffect sootVignette;

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
        vignette = sootVignette;
        return vignette != null;
    }
}
