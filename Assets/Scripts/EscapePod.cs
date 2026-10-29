using System.Collections;
using UnityEngine;

/// <summary>
/// The exit, and the safe drop-off point.
/// Touching it drops off every survivor following the player (they're safe for good once inside).
/// Once every survivor has been found, it powers on and sets off an alarm that sends the Hunters to guard it.
/// It launches as soon as all survivors are safe inside.
/// </summary>
public class EscapePod : MonoBehaviour
{
    public static EscapePod Instance { get; private set; }

    [Header("Launch")]
    public float activationDelay = 1f;      // pause between "activate" and "launch" sounds
    public float launchDuration = 1.5f;
    public float launchDistance = 6f;

    [Header("Powered-On Look")]
    public SpriteRenderer podSprite;
    public Color poweredColor = new Color(0.4f, 1f, 0.6f);

    private Revealable revealable;
    private bool poweredOn;
    private bool launching;
    private float lockedMessageCooldown;

    private void Awake()
    {
        Instance = this;
        revealable = GetComponent<Revealable>();
    }

    /// <summary>Called by GameManager once every survivor has been found. Only happens once.</summary>
    public void PowerOn()
    {
        if (poweredOn) return;
        poweredOn = true;

        if (revealable != null) revealable.alwaysVisible = true; // so the player can find it
        if (podSprite != null) podSprite.color = poweredColor;
        AudioManager.Instance.Play(AudioManager.Instance.podActivateSound);

        // The catch: powering up the pod sets off the station alarm.
        AudioManager.Instance.Play(AudioManager.Instance.alarmSound);
        EnemyBase.SoundAlarm(transform.position);
        GameManager.Instance.ShowHint("ALARM! THE HUNTERS ARE HEADING FOR THE POD", 3f);
        GameManager.Instance.SetObjective("Get the last survivors to the escape pod - the Hunters are guarding it!");
        if (CameraFollow.Instance != null) CameraFollow.Instance.Shake(0.6f, 0.12f);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (launching || !other.TryGetComponent(out PlayerSignal player)) return;

        // Drop off everyone who is following the player.
        int dropped = player.DepositFollowers();
        if (dropped > 0)
        {
            AudioManager.Instance.Play(AudioManager.Instance.rescueSound);
            GameManager.Instance.DepositSurvivors(dropped);
        }

        if (GameManager.Instance.AllSurvivorsSafe)
        {
            StartCoroutine(LaunchSequence(player));
            return;
        }

        // Nobody to drop off and people still missing: the pod stays locked.
        if (dropped == 0)
        {
            // Don't spam the message if the player wiggles on the pod.
            if (Time.time < lockedMessageCooldown) return;
            lockedMessageCooldown = Time.time + 2f;

            int left = GameManager.Instance.TotalSurvivors - GameManager.Instance.SafeSurvivors;
            GameManager.Instance.ShowHint($"POD LOCKED - {left} survivor(s) still out there", 2f);
            AudioManager.Instance.Play(AudioManager.Instance.podLockedSound);
        }
    }

    private IEnumerator LaunchSequence(PlayerSignal player)
    {
        launching = true;
        GameManager.Instance.BeginEnding(); // freezes enemies and input
        player.LockControls();

        // Snap the player into the pod and carry it along.
        player.transform.position = transform.position;
        player.transform.SetParent(transform);

        AudioManager.Instance.Play(AudioManager.Instance.podActivateSound);
        yield return new WaitForSeconds(activationDelay);

        AudioManager.Instance.Play(AudioManager.Instance.podLaunchSound);
        if (CameraFollow.Instance != null) CameraFollow.Instance.Shake(launchDuration, 0.15f);

        Vector3 start = transform.position;
        Vector3 end = start + Vector3.up * launchDistance;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / launchDuration;
            // Ease-in so it starts slow and rockets away.
            transform.position = Vector3.Lerp(start, end, t * t);
            yield return null;
        }

        GameManager.Instance.Win();
    }
}
