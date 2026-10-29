using UnityEngine;

/// <summary>
/// HUNTER (red): blind and dormant until it HEARS a pulse.
/// Then it sprints to where the pulse came from, searches, and goes back to its default behavior.
/// If you get close while it's awake, it chases.
///
/// Its default behavior gets worse as the game goes on:
///   - Normally it goes back to sleep (Dormant).
///   - After too many pulses it stays awake and prowls the station (Prowling).
///   - Once the escape pod alarm sounds, it guards the area around the pod (Guarding).
/// </summary>
public class Hunter : EnemyBase
{
    private enum State { Dormant, Investigating, Searching, Chasing, Prowling, Guarding }

    [Header("Hunter Settings")]
    public float huntSpeed = 3.1f;          // slower than the player (4), about even with a player leading 5 followers
    public float prowlSpeed = 1.6f;
    public float guardSpeed = 1.8f;
    public float searchTime = 2.5f;          // how long it lingers at the pulse spot
    public float closeSenseRange = 2.5f;     // it can sense you if you're right next to it
    public float guardRadius = 4f;           // how far from the pod it patrols once the alarm sounds
    public float repathInterval = 0.4f;

    private State state = State.Dormant;
    private float stateTimer;
    private float repathTimer;
    private bool alarmActive;
    private Vector2 alarmPosition;

    protected override void OnHeardPulse(Vector2 pulsePosition)
    {
        if (state == State.Dormant)
        {
            AudioManager.Instance.Play(AudioManager.Instance.enemyDetectSound);
        }
        state = State.Investigating;
        SetDestination(pulsePosition);
    }

    protected override void OnAlarm(Vector2 position)
    {
        alarmActive = true;
        alarmPosition = position;

        // Rush to the pod right away (unless it's already chasing the player).
        if (state != State.Chasing)
        {
            state = State.Investigating;
            SetDestination(position);
        }
    }

    protected override void Think()
    {
        switch (state)
        {
            case State.Dormant:
                // Wakes up on its own once the player has made too much noise.
                if (PlayerSignal.Instance.HuntersRestless) state = State.Prowling;
                break;

            case State.Investigating:
                if (TryStartChase()) break;
                if (FollowPath(huntSpeed))
                {
                    state = State.Searching;
                    stateTimer = searchTime;
                }
                break;

            case State.Searching:
                if (TryStartChase()) break;
                stateTimer -= Time.fixedDeltaTime;
                if (stateTimer <= 0f) ReturnToDefault();
                break;

            case State.Chasing:
                UpdateChase();
                break;

            case State.Prowling:
                if (TryStartChase()) break;
                if (FollowPath(prowlSpeed))
                {
                    SetDestination(LevelBuilder.Instance.GetRandomWalkablePosition());
                }
                break;

            case State.Guarding:
                if (TryStartChase()) break;
                if (FollowPath(guardSpeed))
                {
                    SetDestination(LevelBuilder.Instance.GetRandomWalkablePositionNear(alarmPosition, guardRadius));
                }
                break;
        }
    }

    /// <summary>Starts a chase if the player is close and visible. Returns true if it did.</summary>
    private bool TryStartChase()
    {
        if (!CanSeePlayer(closeSenseRange)) return false;

        state = State.Chasing;
        repathTimer = 0f;
        AudioManager.Instance.Play(AudioManager.Instance.enemyDetectSound);
        return true;
    }

    private void UpdateChase()
    {
        if (!CanSeePlayer(closeSenseRange * 1.5f))
        {
            state = State.Searching;
            stateTimer = searchTime;
            return;
        }

        repathTimer -= Time.fixedDeltaTime;
        if (repathTimer <= 0f)
        {
            SetDestination(Player.position);
            repathTimer = repathInterval;
        }
        FollowPath(huntSpeed);
    }

    /// <summary>Picks what to do when there's nothing to investigate.</summary>
    private void ReturnToDefault()
    {
        path.Clear();
        if (alarmActive) state = State.Guarding;
        else if (PlayerSignal.Instance.HuntersRestless) state = State.Prowling;
        else state = State.Dormant;
    }
}
