using UnityEngine;

public class ObstacleDestroyEffects : MonoBehaviour
{
    [SerializeField] private ParticleSystem[] particles;
    [SerializeField] private AudioClip[] oneShotClips;
    [Range(0f, 1f)] [SerializeField] private float oneShotVolume = 1f;

    private void OnValidate()
    {
        oneShotVolume = Mathf.Clamp01(oneShotVolume);
    }

    public void Play(Vector3 position)
    {
        PlayParticles(position);
        PlayOneShotClips();
    }

    public void PlayAtAssignedTransforms()
    {
        PlayParticles();
        PlayOneShotClips();
    }

    public bool IsEffectChild(Transform candidate)
    {
        if (candidate == null || particles == null)
        {
            return false;
        }

        for (int i = 0; i < particles.Length; i++)
        {
            ParticleSystem particle = particles[i];
            if (particle != null && candidate.IsChildOf(particle.transform))
            {
                return true;
            }
        }

        return false;
    }

    private void PlayParticles(Vector3 position)
    {
        if (particles == null)
        {
            return;
        }

        for (int i = 0; i < particles.Length; i++)
        {
            ParticleSystem particle = particles[i];
            if (particle == null)
            {
                continue;
            }

            particle.transform.position = position;
            particle.Play();
        }
    }

    private void PlayParticles()
    {
        if (particles == null)
        {
            return;
        }

        for (int i = 0; i < particles.Length; i++)
        {
            ParticleSystem particle = particles[i];
            if (particle == null)
            {
                continue;
            }

            particle.Play();
        }
    }

    private void PlayOneShotClips()
    {
        if (oneShotClips == null)
        {
            return;
        }

        for (int i = 0; i < oneShotClips.Length; i++)
        {
            AudioClip clip = oneShotClips[i];
            if (clip == null)
            {
                continue;
            }

            AudioManager.Instance?.PlaySfxOneShot(clip, oneShotVolume);
        }
    }
}
