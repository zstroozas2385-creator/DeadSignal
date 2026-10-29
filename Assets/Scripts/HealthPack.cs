using UnityEngine;

/// <summary>
/// A medkit lying on the floor. Hidden in the dark like everything else (uses Revealable).
/// Walking over it heals the player a little, unless they're already at full health.
/// </summary>
public class HealthPack : MonoBehaviour
{
    public int healAmount = 15;   // 15% of 100 max health

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!other.TryGetComponent(out PlayerSignal player)) return;
        if (player.IsFullHealth) return; // leave it for later

        player.Heal(healAmount);
        AudioManager.Instance.Play(AudioManager.Instance.healSound);
        GameManager.Instance.ShowHint($"+{healAmount} HEALTH", 1.2f);
        Destroy(gameObject);
    }
}
