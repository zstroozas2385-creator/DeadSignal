using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scatters health packs on random floor tiles at the start of the game,
/// then adds another one every so often (up to a maximum on the map at once).
/// </summary>
public class PickupSpawner : MonoBehaviour
{
    public HealthPack healthPackPrefab;
    public int startingCount = 4;
    public int maxOnMap = 4;
    public float respawnInterval = 40f;
    public float minDistanceFromPlayer = 6f;    // don't drop one right next to the player
    public float minDistanceBetweenPacks = 3f;  // spread them out

    private readonly List<HealthPack> activePacks = new List<HealthPack>();
    private float respawnTimer;

    private void Start()
    {
        for (int i = 0; i < startingCount; i++) SpawnOne();
        respawnTimer = respawnInterval;
    }

    private void Update()
    {
        if (!GameManager.Instance.IsPlaying) return;

        // Picked-up packs are destroyed, so drop them from the list.
        activePacks.RemoveAll(pack => pack == null);

        respawnTimer -= Time.deltaTime;
        if (respawnTimer <= 0f)
        {
            respawnTimer = respawnInterval;
            if (activePacks.Count < maxOnMap) SpawnOne();
        }
    }

    private void SpawnOne()
    {
        // Try a handful of random tiles and take the first one that's far enough from everything.
        for (int attempt = 0; attempt < 30; attempt++)
        {
            Vector2 pos = LevelBuilder.Instance.GetRandomWalkablePosition();
            if (IsGoodSpot(pos))
            {
                activePacks.Add(Instantiate(healthPackPrefab, pos, Quaternion.identity, transform));
                return;
            }
        }
    }

    private bool IsGoodSpot(Vector2 pos)
    {
        if (PlayerSignal.Instance != null &&
            Vector2.Distance(pos, PlayerSignal.Instance.transform.position) < minDistanceFromPlayer)
            return false;

        foreach (HealthPack pack in activePacks)
        {
            if (pack != null && Vector2.Distance(pos, pack.transform.position) < minDistanceBetweenPacks)
                return false;
        }
        return true;
    }
}
