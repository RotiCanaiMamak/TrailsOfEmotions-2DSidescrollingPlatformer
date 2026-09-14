using UnityEngine;

public class MistVignetteManager : MonoBehaviour
{
    public static MistVignetteManager Instance { get; private set; }

    [Header("Vignette")]
    [SerializeField] private ParticleVignetteEffect mistVignetteEffect;

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
        vignetteEffect = mistVignetteEffect;
        return vignetteEffect != null;
    }
}
