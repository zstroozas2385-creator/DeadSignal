using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A stranded survivor.
///   Waiting   - hidden in the dark at its starting spot until the player touches it.
///   Following - walks behind the player in a line (using the player's breadcrumb trail).
///   Returning - an enemy grabbed it, so it runs back to where it was found.
///   Safe      - dropped off at the escape pod (hidden inside, can't be snatched anymore).
/// </summary>
public class Survivor : MonoBehaviour
{
    private enum State { Waiting, Following, Returning, Safe }

    [Header("Idle Bob")]
    public float bobHeight = 0.08f;
    public float bobSpeed = 2f;

    [Header("Movement")]
    public float followSpeed = 6f;     // fast enough to keep up with the trail
    public float returnSpeed = 3f;     // speed when running back home after being snatched

    private State state = State.Waiting;
    private Vector2 homePosition;
    private Revealable revealable;
    private List<Vector2> returnPath = new List<Vector2>();
    private int returnIndex;

    private void Start()
    {
        homePosition = transform.position;
        revealable = GetComponent<Revealable>();
        GameManager.Instance.RegisterSurvivor();
    }

    private void Update()
    {
        switch (state)
        {
            case State.Waiting:
                // Small up/down bob so survivors feel alive once revealed.
                transform.position = homePosition + Vector2.up * Mathf.Sin(Time.time * bobSpeed) * bobHeight;
                break;

            case State.Following:
                PlayerSignal player = PlayerSignal.Instance;
                Vector2 target = player.GetFollowPoint(player.Followers.IndexOf(this));
                transform.position = Vector2.MoveTowards(transform.position, target, followSpeed * Time.deltaTime);
                break;

            case State.Returning:
                UpdateReturning();
                break;
        }
    }

    private void UpdateReturning()
    {
        if (returnIndex >= returnPath.Count)
        {
            // Made it home - wait to be rescued again.
            state = State.Waiting;
            if (revealable != null) revealable.alwaysVisible = false;
            return;
        }

        transform.position = Vector2.MoveTowards(transform.position, returnPath[returnIndex], returnSpeed * Time.deltaTime);
        if (Vector2.Distance(transform.position, returnPath[returnIndex]) < 0.05f) returnIndex++;
    }

    // Stay (not Enter) so a survivor that just got home under the player can still be picked up.
    private void OnTriggerStay2D(Collider2D other)
    {
        if (state != State.Waiting || !other.TryGetComponent(out PlayerSignal player)) return;

        state = State.Following;
        if (revealable != null) revealable.alwaysVisible = true; // followers are always visible

        player.AddFollower(this);
        AudioManager.Instance.Play(AudioManager.Instance.rescueSound);
        GameManager.Instance.RescueSurvivor();
    }

    /// <summary>
    /// Called when an enemy touches this survivor. Only followers can be snatched.
    /// Returns true if it worked.
    /// </summary>
    public bool Snatch()
    {
        if (state != State.Following) return false;

        state = State.Returning;
        PlayerSignal.Instance.RemoveFollower(this);

        // Path home around the walls. Stays visible so the player can see where it ran.
        returnPath = LevelBuilder.Instance.FindPath(transform.position, homePosition);
        returnIndex = 0;

        AudioManager.Instance.Play(AudioManager.Instance.survivorLostSound);
        GameManager.Instance.SurvivorLost();
        return true;
    }

    /// <summary>Called when the player drops this survivor off at the pod. It hides inside, safe for good.</summary>
    public void EnterPod()
    {
        state = State.Safe;
        gameObject.SetActive(false);
    }
}
