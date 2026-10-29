using UnityEngine;

/// <summary>
/// DRIFTER (purple): slowly wanders the station on its own.
/// If it SEES you (short range, blocked by walls) it chases.
/// If it HEARS a pulse it drifts over to investigate, a little faster than normal.
/// Lesson for the player: even if you never pulse, the station isn't safe.
/// </summary>
public class Drifter : EnemyBase
{
    private enum State { Wandering, Investigating, Chasing }

    [Header("Drifter Settings")]
    public float wanderSpeed = 1.3f;
    public float investigateSpeed = 2.2f;
    public float chaseSpeed = 2.5f;
    public float sightRange = 4f;
    public float giveUpTime = 3f;            // seconds without seeing you before it stops chasing
    public float repathInterval = 0.4f;

    private State state = State.Wandering;
    private float lostSightTimer;
    private float repathTimer;

    protected override void OnHeardPulse(Vector2 pulsePosition)
    {
        if (state == State.Chasing) return; // already locked on
        state = State.Investigating;
        SetDestination(pulsePosition);
    }

    protected override void Think()
    {
        // Seeing the player beats anything else it was doing.
        if (state != State.Chasing && CanSeePlayer(sightRange))
        {
            state = State.Chasing;
            lostSightTimer = giveUpTime;
            AudioManager.Instance.Play(AudioManager.Instance.enemyDetectSound, 1f, 0.8f); // lower pitch than Hunter
        }

        switch (state)
        {
            case State.Wandering:
                if (FollowPath(wanderSpeed))
                {
                    SetDestination(LevelBuilder.Instance.GetRandomWalkablePosition());
                }
                break;

            case State.Investigating:
                if (FollowPath(investigateSpeed))
                {
                    state = State.Wandering;
                }
                break;

            case State.Chasing:
                lostSightTimer = CanSeePlayer(sightRange * 1.5f) ? giveUpTime : lostSightTimer - Time.fixedDeltaTime;
                if (lostSightTimer <= 0f)
                {
                    state = State.Wandering;
                    path.Clear();
                    break;
                }

                repathTimer -= Time.fixedDeltaTime;
                if (repathTimer <= 0f)
                {
                    SetDestination(Player.position);
                    repathTimer = repathInterval;
                }
                FollowPath(chaseSpeed);
                break;
        }
    }
}
