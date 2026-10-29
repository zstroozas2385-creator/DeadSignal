using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Put this on anything that should be hidden in the dark until a pulse reveals it
/// (walls, debris, survivors, enemies, the pod).
///
/// Visibility = the brightest of:
///   - a pulse reveal (full brightness for revealDuration, then fades)
///   - the player's small passive glow
///   - "memory" (a faint outline of things you've already seen; 0 for enemies)
/// </summary>
public class Revealable : MonoBehaviour
{
    // Every Revealable in the scene, so a pulse can find them without physics queries.
    private static readonly List<Revealable> All = new List<Revealable>();

    [Tooltip("Seconds the object stays fully visible after a pulse hits it.")]
    public float revealDuration = 2.5f;

    [Tooltip("How fast the object fades in/out (alpha per second).")]
    public float fadeSpeed = 2f;

    [Tooltip("Faint alpha kept after it has been seen once. Use 0 for enemies.")]
    [Range(0f, 1f)] public float memoryAlpha = 0.15f;

    [Tooltip("If true the object is never hidden (e.g. the powered-on escape pod).")]
    public bool alwaysVisible;

    private SpriteRenderer[] renderers;
    private float[] baseAlphas;          // each sprite's original alpha, so we scale instead of overwrite
    private float revealStartTime = -99f;
    private bool hasBeenSeen;
    private float passiveAlpha;
    private float currentAlpha;

    private void Awake()
    {
        renderers = GetComponentsInChildren<SpriteRenderer>();
        baseAlphas = new float[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) baseAlphas[i] = renderers[i].color.a;
        ApplyAlpha(0f);
    }

    private void OnEnable() { All.Add(this); }
    private void OnDisable() { All.Remove(this); }

    /// <summary>
    /// Reveal everything inside the radius. Objects farther away light up later,
    /// so the reveal follows the expanding ring.
    /// </summary>
    public static void RevealInRadius(Vector2 center, float radius, float waveSpeed)
    {
        foreach (Revealable r in All)
        {
            float distance = Vector2.Distance(center, r.transform.position);
            if (distance <= radius)
            {
                r.revealStartTime = Time.time + distance / waveSpeed;
            }
        }
    }

    /// <summary>Instantly reveals just this object (e.g. an enemy that just hit you).</summary>
    public void RevealNow()
    {
        revealStartTime = Time.time;
    }

    /// <summary>The player's small always-on glow. Called every frame by the player.</summary>
    public static void ShowPassive(Vector2 center, float radius)
    {
        foreach (Revealable r in All)
        {
            float distance = Vector2.Distance(center, r.transform.position);
            if (distance < radius)
            {
                r.passiveAlpha = 1f - distance / radius;
            }
        }
    }

    private void LateUpdate()
    {
        float target = 0f;

        // Inside the pulse window? (start time can be in the future while the ring travels)
        bool pulsed = Time.time >= revealStartTime && Time.time < revealStartTime + revealDuration;
        if (pulsed)
        {
            target = 1f;
            hasBeenSeen = true;
        }
        else if (hasBeenSeen)
        {
            target = memoryAlpha;
        }

        if (passiveAlpha > 0.5f) hasBeenSeen = true;
        target = Mathf.Max(target, passiveAlpha);
        if (alwaysVisible) target = 1f;

        currentAlpha = Mathf.MoveTowards(currentAlpha, target, fadeSpeed * Time.deltaTime);
        ApplyAlpha(currentAlpha);

        passiveAlpha = 0f; // the player sets this again next frame if still close
    }

    private void ApplyAlpha(float alpha)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            Color c = renderers[i].color;
            c.a = baseAlphas[i] * alpha;
            renderers[i].color = c;
        }
    }
}
