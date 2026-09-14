using UnityEngine;

/// <summary>
/// Attach to the door/portal prefab assigned to TerrainManager.transitionDoorPrefab.
/// TerrainManager spawns this on the most recently generated chunk whenever
/// BiomeManager queues an inter-family transition (e.g. Normal -> Anger-Pre, or
/// Anger-Peaked -> Normal). Touching it kicks off the white-flash sequence that
/// actually swaps the biome and rebuilds the terrain.
///
/// Expected prefab setup: a Collider2D with "Is Trigger" checked, sized to span
/// the chunk so the player can't easily jump over/around it.
/// </summary>
public class BiomeTransitionDoor : MonoBehaviour
{
    private const string PlayerTag = "Player";

    [Header("Transition Slowdown")]
    [Range(0f, 1f)]
    [Tooltip("Movement speed multiplier applied as soon as the player touches the door. 1 = no slowdown, 0 = stop.")]
    public float transitionSpeedMultiplier = 0.35f;

    private bool triggered;
    private PlayerController triggeredPlayer;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered || !other.CompareTag(PlayerTag))
        {
            return;
        }

        triggered = true;
        triggeredPlayer = other.GetComponentInParent<PlayerController>();
        AudioManager.Instance?.PlayTransitionSound();
        ApplyTransitionSlowdown();

        ScreenFader fader = ScreenFader.Instance;
        if (fader != null)
        {
            fader.BeginBiomeTransition();
        }
        else
        {
            Debug.LogWarning("[BiomeTransitionDoor] No ScreenFader found in the scene; the biome transition cannot run.");
            ClearTransitionSlowdown();
        }

        // Stop the door from being usable again while the fade/reset plays out.
        // TerrainManager destroys the GameObject itself once the reset actually
        // runs, so this is just about preventing a second trigger in the meantime.
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.enabled = false;
        }

        foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>())
        {
            renderer.enabled = false;
        }
    }

    private void ApplyTransitionSlowdown()
    {
        if (triggeredPlayer != null)
        {
            triggeredPlayer.AddMovementModifier(this, transitionSpeedMultiplier, 1f, 1f);
        }
    }

    private void ClearTransitionSlowdown()
    {
        if (triggeredPlayer != null)
        {
            triggeredPlayer.RemoveMovementModifier(this);
        }
    }

    private void OnDestroy()
    {
        ClearTransitionSlowdown();
    }
}
