using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared code for both enemy types: path following, line of sight,
/// hearing pulses, hurting the player, and snatching survivors out of the line.
/// Hunter and Drifter inherit from this and only decide WHERE to go.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public abstract class EnemyBase : MonoBehaviour
{
    private static readonly List<EnemyBase> AllEnemies = new List<EnemyBase>();

    [Header("Combat")]
    public int contactDamage = 25;     // out of 100 health: 4 hits from full
    public float stunAfterHit = 2.5f;     // enemy freezes after landing a hit so the player can escape
    public float snatchRange = 0.6f;      // how close it must get to grab a following survivor

    [Header("Sight")]
    public LayerMask wallLayer;           // set to the "Walls" layer so walls block vision

    [Header("Pathing")]
    public float waypointReachDistance = 0.05f;

    protected Rigidbody2D rb;
    protected List<Vector2> path = new List<Vector2>();
    protected int pathIndex;
    private float stunnedUntil;
    private Revealable revealable;

    protected bool HasPath => pathIndex < path.Count;
    protected Transform Player => PlayerSignal.Instance != null ? PlayerSignal.Instance.transform : null;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic; // we move it ourselves along the path
        revealable = GetComponent<Revealable>();
    }

    private void OnEnable() { AllEnemies.Add(this); }
    private void OnDisable() { AllEnemies.Remove(this); }

    private void FixedUpdate()
    {
        if (!GameManager.Instance.IsPlaying) return;
        if (Time.time < stunnedUntil) return; // recovering after a hit or a snatch

        TrySnatchFollower();
        Think();
    }

    /// <summary>Each enemy type decides what to do every physics step.</summary>
    protected abstract void Think();

    /// <summary>Called when a pulse goes off within hearing range.</summary>
    protected abstract void OnHeardPulse(Vector2 pulsePosition);

    /// <summary>Called when the escape pod's alarm goes off. Enemies ignore it unless they override this.</summary>
    protected virtual void OnAlarm(Vector2 alarmPosition) { }

    // ---------------- Static helpers ----------------

    public static void AlertEnemiesInRadius(Vector2 position, float radius)
    {
        foreach (EnemyBase enemy in AllEnemies)
        {
            if (Vector2.Distance(position, enemy.transform.position) <= radius)
            {
                enemy.OnHeardPulse(position);
            }
        }
    }

    /// <summary>The pod alarm can be heard by every enemy on the station.</summary>
    public static void SoundAlarm(Vector2 position)
    {
        foreach (EnemyBase enemy in AllEnemies)
        {
            enemy.OnAlarm(position);
        }
    }

    public static float DistanceToNearestEnemy(Vector2 position)
    {
        float nearest = float.MaxValue;
        foreach (EnemyBase enemy in AllEnemies)
        {
            nearest = Mathf.Min(nearest, Vector2.Distance(position, enemy.transform.position));
        }
        return nearest;
    }

    // ---------------- Movement ----------------

    protected void SetDestination(Vector2 target)
    {
        path = LevelBuilder.Instance.FindPath(rb.position, target);
        pathIndex = 0;
    }

    /// <summary>Moves toward the next waypoint. Returns true when the path is finished.</summary>
    protected bool FollowPath(float speed)
    {
        if (!HasPath) return true;

        Vector2 next = Vector2.MoveTowards(rb.position, path[pathIndex], speed * Time.fixedDeltaTime);
        rb.MovePosition(next);

        if (Vector2.Distance(next, path[pathIndex]) <= waypointReachDistance)
        {
            pathIndex++;
        }
        return !HasPath;
    }

    // ---------------- Senses ----------------

    /// <summary>True if the player is within range and no wall is in the way.</summary>
    protected bool CanSeePlayer(float range)
    {
        if (Player == null) return false;

        Vector2 toPlayer = (Vector2)Player.position - rb.position;
        if (toPlayer.magnitude > range) return false;

        RaycastHit2D hit = Physics2D.Linecast(rb.position, Player.position, wallLayer);
        return hit.collider == null;
    }

    // ---------------- Attacks ----------------

    /// <summary>If a survivor in the player's line is close enough, knock it loose.</summary>
    private void TrySnatchFollower()
    {
        if (PlayerSignal.Instance == null) return;

        List<Survivor> followers = PlayerSignal.Instance.Followers;
        for (int i = 0; i < followers.Count; i++)
        {
            if (Vector2.Distance(rb.position, followers[i].transform.position) <= snatchRange)
            {
                followers[i].Snatch();
                Stun(); // only one survivor per grab
                return;  // the list just changed, so stop looping
            }
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (Time.time < stunnedUntil || !other.TryGetComponent(out PlayerSignal player)) return;

        // The player handles its own invulnerability window; only react if the hit landed.
        if (player.TakeDamage(contactDamage))
        {
            Stun();
        }
    }

    /// <summary>Freeze briefly and light up so the player can see what just attacked.</summary>
    private void Stun()
    {
        stunnedUntil = Time.time + stunAfterHit;
        if (revealable != null) revealable.RevealNow();
    }
}
