using UnityEngine;

public interface IGroundPoundTarget
{
    bool TryHandleGroundPound(PlayerController player, Vector2 impactPoint);
}
