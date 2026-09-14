using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerTumbleweedStatus : MonoBehaviour
{
    private PlayerController player;
    private PlayerStatusManager statusManager;
    private bool hasActiveEffect;
    private float effectDuration;
    private float effectTimer;

    private void Awake()
    {
        ResolvePlayer();
    }

    private void Update()
    {
        if (!hasActiveEffect)
        {
            return;
        }

        if (effectDuration <= 0f)
        {
            ClearGameplayState();
            return;
        }

        effectTimer += Time.deltaTime;
        if (effectTimer >= effectDuration)
        {
            ClearGameplayState();
        }
    }

    private void OnDisable()
    {
        ClearGameplayState();
    }

    private void OnDestroy()
    {
        ClearGameplayState();
    }

    public void RefreshTumbleweedHit(float speedMultiplier, float duration)
    {
        ResolvePlayer();
        if (player == null)
        {
            return;
        }

        effectDuration = Mathf.Max(0f, duration);
        if (effectDuration <= 0f)
        {
            ClearGameplayState();
            return;
        }

        player.AddMovementModifier(
            this,
            Mathf.Max(0f, speedMultiplier),
            1f);
        player.SetJumpBlocked(this, true);

        hasActiveEffect = true;
        effectTimer = 0f;
    }

    private void ClearGameplayState()
    {
        if (player != null)
        {
            player.RemoveMovementModifier(this);
            player.SetJumpBlocked(this, false);
        }

        hasActiveEffect = false;
        effectDuration = 0f;
        effectTimer = 0f;
    }

    private void ResolvePlayer()
    {
        if (player != null)
        {
            return;
        }

        ResolveStatusManager();
        player = statusManager != null ? statusManager.Player : null;
    }

    private void ResolveStatusManager()
    {
        if (statusManager == null)
        {
            statusManager = GetComponent<PlayerStatusManager>();
        }
    }
}
