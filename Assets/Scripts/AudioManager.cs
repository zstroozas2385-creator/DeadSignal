using UnityEngine;

/// <summary>
/// One place to hold every sound effect. Other scripts call
/// AudioManager.Instance.Play(AudioManager.Instance.someSound).
/// Empty clip slots are skipped, so a missing sound never crashes the game.
/// Also owns the looping "signal hum" that gets louder and more distorted as energy runs out.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Player")]
    public AudioClip pulseSound;
    public AudioClip damageSound;
    public AudioClip lowEnergySound;
    public AudioClip healSound;

    [Header("Enemies")]
    public AudioClip enemyDetectSound;
    public AudioClip enemyWarningSound;   // proximity alarm beep

    [Header("Objectives")]
    public AudioClip rescueSound;
    public AudioClip survivorLostSound;
    public AudioClip alarmSound;
    public AudioClip podLockedSound;
    public AudioClip podActivateSound;
    public AudioClip podLaunchSound;

    [Header("End Screens")]
    public AudioClip winSound;
    public AudioClip gameOverSound;

    [Header("Low-Energy Signal Hum")]
    public AudioSource humSource;                 // looping static, on its own child object
    public AudioDistortionFilter humDistortion;
    [Range(0f, 1f)] public float humStartsAt = 0.5f;  // hum fades in once energy drops below this
    [Range(0f, 1f)] public float maxHumVolume = 0.45f;

    private AudioSource source;

    private void Awake()
    {
        Instance = this;
        source = GetComponent<AudioSource>();
    }

    /// <summary>
    /// Plays a one-shot sound. Pitch lets one clip sound like several.
    /// Pitched sounds get their own temporary AudioSource so they don't
    /// change the pitch of other sounds that are already playing.
    /// </summary>
    public void Play(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (clip == null) return;

        if (Mathf.Approximately(pitch, 1f))
        {
            source.PlayOneShot(clip, volume);
            return;
        }

        AudioSource temp = new GameObject("TempSound_" + clip.name).AddComponent<AudioSource>();
        temp.transform.SetParent(transform);
        temp.clip = clip;
        temp.volume = volume;
        temp.pitch = pitch;
        temp.Play();
        Destroy(temp.gameObject, clip.length / pitch + 0.1f);
    }

    /// <summary>
    /// Called whenever energy changes. Silent above humStartsAt, then gets louder,
    /// lower-pitched and more distorted the closer the signal gets to 0.
    /// </summary>
    public void UpdateSignalHum(float energyNormalized)
    {
        if (humSource == null) return;

        // 0 = signal is fine, 1 = signal is about to die
        float danger = Mathf.InverseLerp(humStartsAt, 0f, energyNormalized);
        humSource.volume = danger * maxHumVolume;
        humSource.pitch = Mathf.Lerp(1f, 0.75f, danger);
        if (humDistortion != null) humDistortion.distortionLevel = Mathf.Lerp(0f, 0.85f, danger);
    }

    public void StopSignalHum()
    {
        if (humSource != null) humSource.Stop();
    }
}
