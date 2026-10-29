using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The player: an astronaut carrying the glowing signal orb.
/// Handles movement, the signal pulse, energy, health, the enemy proximity alarm,
/// and the line of rescued survivors that follows behind.
///
/// Core risk/reward: a pulse reveals the station around you, but enemies
/// hear it from even farther away (pulseNoiseRadius > pulseRevealRadius).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerSignal : MonoBehaviour
{
    public static PlayerSignal Instance { get; private set; }

    [Header("Movement")]
    public float moveSpeed = 4f;
    [Range(0f, 0.2f)] public float slowdownPerFollower = 0.05f; // each survivor in the line slows you 5%

    [Header("Energy")]
    public float maxEnergy = 100f;
    public float passiveDrainPerSecond = 0.5f;   // the signal always costs a little to keep alive
    public float pulseCost = 7f;
    public float energyPerRescue = 20f;          // survivors share their suit batteries
    [Range(0f, 1f)] public float lowEnergyThreshold = 0.25f;

    [Header("Health")]
    public int maxHealth = 100;                  // enemies deal 25, so 4 hits from full
    public float invulnerableTime = 2f;          // grace period after getting hit

    [Header("Pulse")]
    public float pulseRevealRadius = 7f;          // how far you can SEE
    public float pulseNoiseRadius = 12f;          // how far enemies can HEAR it
    public float pulseSpeed = 14f;                // how fast the ring travels (units/sec)
    public float pulseCooldown = 0.75f;
    public int restlessPulseCount = 6;            // after this many pulses, Hunters stop going back to sleep
    public PulseRing pulseRingPrefab;

    [Header("Passive Sight")]
    public float passiveSightRadius = 2.5f;       // small glow so you're never fully blind

    [Header("Proximity Alarm")]
    public float alarmRange = 6f;                 // enemies closer than this make the alarm beep
    public float alarmFastInterval = 0.2f;        // beep rate when an enemy is right on you
    public float alarmSlowInterval = 1.2f;        // beep rate at the edge of the range

    [Header("Follower Trail")]
    public float trailPointSpacing = 0.3f;        // drop a breadcrumb every time we move this far
    public int pointsBetweenFollowers = 3;        // gap in the line, measured in breadcrumbs

    [Header("Visuals")]
    public SpriteRenderer bodySprite;

    public float EnergyNormalized => energy / maxEnergy;
    public int PulseCount { get; private set; }
    public bool HuntersRestless => PulseCount >= restlessPulseCount;
    public List<Survivor> Followers { get; } = new List<Survivor>();

    private Rigidbody2D rb;
    private Vector2 moveInput;
    private float energy;
    private int health;
    private float lastPulseTime = -99f;
    private float invulnerableUntil;
    private float nextAlarmTime;
    private float nextLowEnergyBeep;
    private bool controlsLocked;

    // Breadcrumbs: trail[0] is the newest (closest to the player).
    private readonly List<Vector2> trail = new List<Vector2>();

    private void Awake()
    {
        Instance = this;
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.sleepMode = RigidbodySleepMode2D.NeverSleep; // keeps enemy contact damage working while standing still

        energy = maxEnergy;
        health = maxHealth;
        trail.Add(transform.position);
    }

    private void Start()
    {
        GameManager.Instance.UpdateEnergyUI(EnergyNormalized);
        GameManager.Instance.UpdateHealthUI(health, maxHealth);
    }

    private void Update()
    {
        if (!GameManager.Instance.IsPlaying || controlsLocked)
        {
            moveInput = Vector2.zero;
            return;
        }

        // "Horizontal"/"Vertical" already map to both WASD and the arrow keys.
        moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            TryPulse();
        }

        DrainEnergy(passiveDrainPerSecond * Time.deltaTime);
        Revealable.ShowPassive(transform.position, passiveSightRadius);

        UpdateTrail();
        UpdateProximityAlarm();
        UpdateLowEnergyWarning();
        UpdateDamageFlicker();
    }

    private void FixedUpdate()
    {
        // Every survivor in the line slows the player down a little.
        float speed = moveSpeed * (1f - slowdownPerFollower * Followers.Count);
        rb.linearVelocity = moveInput * speed;
    }

    // ---------------- Pulse ----------------

    private void TryPulse()
    {
        if (Time.time < lastPulseTime + pulseCooldown) return;
        lastPulseTime = Time.time;
        PulseCount++;

        Vector2 origin = transform.position;

        // Visual ring + reveal objects as the ring passes over them.
        if (pulseRingPrefab != null)
        {
            PulseRing ring = Instantiate(pulseRingPrefab, origin, Quaternion.identity);
            ring.Init(pulseRevealRadius, pulseSpeed);
        }
        Revealable.RevealInRadius(origin, pulseRevealRadius, pulseSpeed);

        // The catch: enemies hear the pulse from farther than you can see.
        EnemyBase.AlertEnemiesInRadius(origin, pulseNoiseRadius);

        AudioManager.Instance.Play(AudioManager.Instance.pulseSound, 1f, Random.Range(0.95f, 1.05f));
        DrainEnergy(pulseCost);

        // Warn the player the moment the Hunters become restless.
        if (PulseCount == restlessPulseCount)
        {
            GameManager.Instance.ShowHint("Too much noise... something is stirring.", 2.5f);
            AudioManager.Instance.Play(AudioManager.Instance.enemyDetectSound, 1f, 0.6f);
        }
    }

    // ---------------- Followers ----------------

    /// <summary>Drops a breadcrumb whenever the player has moved far enough.</summary>
    private void UpdateTrail()
    {
        Vector2 pos = transform.position;
        if (Vector2.Distance(pos, trail[0]) < trailPointSpacing) return;

        trail.Insert(0, pos);

        // Only keep enough breadcrumbs for the longest possible line.
        int maxPoints = (GameManager.Instance.TotalSurvivors + 1) * pointsBetweenFollowers + 1;
        if (trail.Count > maxPoints) trail.RemoveAt(trail.Count - 1);
    }

    /// <summary>Where follower number 'index' (0 = first in line) should walk to.</summary>
    public Vector2 GetFollowPoint(int index)
    {
        int i = Mathf.Min((index + 1) * pointsBetweenFollowers, trail.Count - 1);
        return trail[i];
    }

    public void AddFollower(Survivor survivor)
    {
        Followers.Add(survivor);
        energy = Mathf.Min(maxEnergy, energy + energyPerRescue);
        GameManager.Instance.UpdateEnergyUI(EnergyNormalized);
        AudioManager.Instance.UpdateSignalHum(EnergyNormalized);
    }

    public void RemoveFollower(Survivor survivor)
    {
        Followers.Remove(survivor);
    }

    /// <summary>Everyone in the line climbs into the escape pod. Returns how many were dropped off.</summary>
    public int DepositFollowers()
    {
        int count = Followers.Count;
        foreach (Survivor s in Followers) s.EnterPod();
        Followers.Clear();
        return count;
    }

    // ---------------- Energy / Health ----------------

    private void DrainEnergy(float amount)
    {
        energy = Mathf.Max(0f, energy - amount);
        GameManager.Instance.UpdateEnergyUI(EnergyNormalized);
        AudioManager.Instance.UpdateSignalHum(EnergyNormalized); // static gets worse as the signal dies

        if (energy <= 0f)
        {
            GameManager.Instance.Lose("Your signal faded into static.");
        }
    }

    /// <summary>
    /// Called by enemies on contact. Returns true if the hit actually landed
    /// (false while invulnerable), so the enemy knows whether to back off.
    /// </summary>
    public bool TakeDamage(int amount)
    {
        if (!GameManager.Instance.IsPlaying || Time.time < invulnerableUntil) return false;

        health = Mathf.Max(0, health - amount);
        invulnerableUntil = Time.time + invulnerableTime;

        GameManager.Instance.UpdateHealthUI(health, maxHealth);
        AudioManager.Instance.Play(AudioManager.Instance.damageSound);
        if (CameraFollow.Instance != null) CameraFollow.Instance.Shake(0.25f, 0.2f);

        if (health <= 0)
        {
            GameManager.Instance.Lose("The creatures tore the signal apart.");
        }
        return true;
    }

    public bool IsFullHealth => health >= maxHealth;

    /// <summary>Called by health packs. Never goes above max.</summary>
    public void Heal(int amount)
    {
        health = Mathf.Min(maxHealth, health + amount);
        GameManager.Instance.UpdateHealthUI(health, maxHealth);
    }

    /// <summary>Freezes the player in place (used during the escape pod launch).</summary>
    public void LockControls()
    {
        controlsLocked = true;
        moveInput = Vector2.zero;
        rb.linearVelocity = Vector2.zero;
        rb.simulated = false; // let the escape pod carry us without physics fighting it
    }

    // ---------------- Feedback ----------------

    /// <summary>Beeps faster the closer the nearest enemy is.</summary>
    private void UpdateProximityAlarm()
    {
        float nearest = EnemyBase.DistanceToNearestEnemy(transform.position);
        if (nearest > alarmRange || Time.time < nextAlarmTime) return;

        float t = nearest / alarmRange; // 0 = touching, 1 = edge of range
        nextAlarmTime = Time.time + Mathf.Lerp(alarmFastInterval, alarmSlowInterval, t);
        AudioManager.Instance.Play(AudioManager.Instance.enemyWarningSound, Mathf.Lerp(1f, 0.35f, t));
    }

    private void UpdateLowEnergyWarning()
    {
        if (EnergyNormalized > lowEnergyThreshold || Time.time < nextLowEnergyBeep) return;
        nextLowEnergyBeep = Time.time + 1.5f;
        AudioManager.Instance.Play(AudioManager.Instance.lowEnergySound, 0.7f);
    }

    /// <summary>Blink the astronaut while invulnerable so the player knows they were hit.</summary>
    private void UpdateDamageFlicker()
    {
        if (bodySprite == null) return;
        bool flickering = Time.time < invulnerableUntil;
        bodySprite.enabled = !flickering || Mathf.FloorToInt(Time.time * 12f) % 2 == 0;
    }
}
