using UnityEngine;

/// <summary>
/// The expanding ring you see when you pulse. Drawn with a LineRenderer circle,
/// grows to the reveal radius, fades out, then destroys itself.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class PulseRing : MonoBehaviour
{
    public int segments = 64;
    public float lineWidth = 0.12f;

    private LineRenderer line;
    private float maxRadius;
    private float speed;
    private float radius;
    private Color startColor;

    private void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.positionCount = segments;
        line.loop = true;
        line.useWorldSpace = false;
        line.startWidth = line.endWidth = lineWidth;
        startColor = line.startColor;
    }

    /// <summary>Called by the player right after spawning the ring.</summary>
    public void Init(float maxRadius, float speed)
    {
        this.maxRadius = maxRadius;
        this.speed = speed;
        radius = 0f;
        DrawCircle();
    }

    private void Update()
    {
        radius += speed * Time.deltaTime;

        // Fade out as it reaches the edge.
        float progress = radius / maxRadius;
        Color c = startColor;
        c.a = startColor.a * (1f - progress);
        line.startColor = line.endColor = c;

        DrawCircle();

        if (progress >= 1f) Destroy(gameObject);
    }

    private void DrawCircle()
    {
        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius);
        }
    }
}
