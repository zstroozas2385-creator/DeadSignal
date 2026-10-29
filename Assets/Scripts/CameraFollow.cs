using UnityEngine;

/// <summary>
/// Smoothly follows the player and supports a simple screen shake.
/// Put this on the Main Camera.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    public static CameraFollow Instance { get; private set; }

    public float smoothTime = 0.15f;

    private Vector3 velocity;
    private float shakeTimer;
    private float shakeStrength;
    private bool snappedToPlayer;

    private void Awake()
    {
        Instance = this;
    }

    private void LateUpdate()
    {
        if (PlayerSignal.Instance == null) return;

        Vector3 target = PlayerSignal.Instance.transform.position;
        target.z = transform.position.z; // keep the camera's own depth

        // First frame: jump straight to the player instead of sweeping across the map.
        if (!snappedToPlayer)
        {
            transform.position = target;
            snappedToPlayer = true;
        }

        Vector3 pos = Vector3.SmoothDamp(transform.position, target, ref velocity, smoothTime);

        if (shakeTimer > 0f)
        {
            shakeTimer -= Time.deltaTime;
            pos += (Vector3)Random.insideUnitCircle * shakeStrength;
        }

        transform.position = pos;
    }

    public void Shake(float duration, float strength)
    {
        shakeTimer = duration;
        shakeStrength = strength;
    }
}
