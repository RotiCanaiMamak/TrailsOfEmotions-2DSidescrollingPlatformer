using System.Collections;
using UnityEngine;

public class AbilityOrbPickup : MonoBehaviour
{
    [Min(0.01f)] [SerializeField] private float fallbackTriggerRadius = 0.5f;
    [Min(0f)] [SerializeField] private float absorbDuration = 0.2f;
    [Range(0f, 1f)] [SerializeField] private float absorbedScaleMultiplier = 0.05f;

    private bool consumed;

    public static AbilityOrbPickup EnsureOn(GameObject instance)
    {
        if (instance == null)
        {
            return null;
        }

        AbilityOrbPickup pickup = instance.GetComponent<AbilityOrbPickup>();
        return pickup != null ? pickup : instance.AddComponent<AbilityOrbPickup>();
    }

    private void Awake()
    {
        Collider2D pickupCollider = GetComponent<Collider2D>();
        if (pickupCollider == null)
        {
            CircleCollider2D circle = gameObject.AddComponent<CircleCollider2D>();
            circle.radius = fallbackTriggerRadius;
            pickupCollider = circle;
        }

        pickupCollider.isTrigger = true;
        ConfigureParticleScaling();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed || other == null)
        {
            return;
        }

        CharacterRuntime runtime = other.GetComponentInParent<CharacterRuntime>();
        if (runtime == null || runtime.Player == null || !runtime.Player.IsCharacterCollider(other))
        {
            return;
        }

        runtime.TryAddActiveAbilityCharge();
        ScoreManager.Instance?.AddOrbPickupScore();
        BeginAbsorb();
    }

    private void BeginAbsorb()
    {
        consumed = true;
        AudioManager.Instance?.PlayOrbCollectionSound();

        Collider2D[] pickupColliders = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < pickupColliders.Length; i++)
        {
            if (pickupColliders[i] != null)
            {
                pickupColliders[i].enabled = false;
            }
        }

        StartCoroutine(ShrinkAndDestroyRoutine());
    }

    private IEnumerator ShrinkAndDestroyRoutine()
    {
        Vector3 startingScale = transform.localScale;
        Vector3 targetScale = startingScale * absorbedScaleMultiplier;

        if (absorbDuration <= 0f)
        {
            transform.localScale = targetScale;
            Destroy(gameObject);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < absorbDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / absorbDuration);
            float smoothT = t * t * (3f - 2f * t);
            transform.localScale = Vector3.LerpUnclamped(startingScale, targetScale, smoothT);
            yield return null;
        }

        transform.localScale = targetScale;
        Destroy(gameObject);
    }

    private void ConfigureParticleScaling()
    {
        ParticleSystem[] particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            if (particleSystems[i] == null)
            {
                continue;
            }

            ParticleSystem.MainModule main = particleSystems[i].main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }
    }

    private void OnValidate()
    {
        fallbackTriggerRadius = Mathf.Max(0.01f, fallbackTriggerRadius);
        absorbDuration = Mathf.Max(0f, absorbDuration);
        absorbedScaleMultiplier = Mathf.Clamp01(absorbedScaleMultiplier);
    }
}
