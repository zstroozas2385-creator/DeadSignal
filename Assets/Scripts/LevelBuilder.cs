using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds the station from a text file so the level is easy to edit,
/// and provides simple grid pathfinding (BFS) for the enemies.
///
/// Level legend:
///   #  wall                 X  damaged equipment (blocks movement)
///   .  floor                P  player start
///   S  survivor             E  escape pod
///   H  Hunter enemy         D  Drifter enemy
///
/// Grid (x, y) maps to world (x, -y) so the text file reads top-to-bottom like the game.
/// </summary>
public class LevelBuilder : MonoBehaviour
{
    public static LevelBuilder Instance { get; private set; }

    [Header("Level Data")]
    public TextAsset levelFile;
    public float tileSize = 1f;

    [Header("Prefabs")]
    public GameObject floorPrefab;     // optional - leave empty for a pure black floor
    public GameObject wallPrefab;
    public GameObject debrisPrefab;
    public GameObject playerPrefab;
    public GameObject survivorPrefab;
    public GameObject escapePodPrefab;
    public GameObject hunterPrefab;
    public GameObject drifterPrefab;

    private bool[,] walkable;
    private int width;
    private int height;
    private readonly List<Vector2Int> walkableTiles = new List<Vector2Int>();

    // The four directions enemies can step in.
    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    private void Awake()
    {
        Instance = this;
        BuildLevel();
    }

    private void BuildLevel()
    {
        // Strip Windows line endings and blank lines.
        string[] lines = levelFile.text.Replace("\r", "").Trim().Split('\n');

        height = lines.Length;
        width = 0;
        foreach (string line in lines) width = Mathf.Max(width, line.Length);
        walkable = new bool[width, height];

        // Parent objects keep the Hierarchy tidy.
        Transform tileParent = new GameObject("Tiles").transform;
        Transform actorParent = new GameObject("Actors").transform;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Short lines are treated as wall so nothing can walk off the map.
                char c = x < lines[y].Length ? lines[y][x] : '#';
                Vector2 pos = GridToWorld(new Vector2Int(x, y));

                bool isSolid = c == '#' || c == 'X';
                walkable[x, y] = !isSolid;
                if (!isSolid)
                {
                    walkableTiles.Add(new Vector2Int(x, y));
                    if (floorPrefab != null) Instantiate(floorPrefab, pos, Quaternion.identity, tileParent);
                }

                switch (c)
                {
                    case '#': Spawn(wallPrefab, pos, tileParent); break;
                    case 'X': Spawn(debrisPrefab, pos, tileParent); break;
                    case 'P': Spawn(playerPrefab, pos, actorParent); break;
                    case 'S': Spawn(survivorPrefab, pos, actorParent); break;
                    case 'E': Spawn(escapePodPrefab, pos, actorParent); break;
                    case 'H': Spawn(hunterPrefab, pos, actorParent); break;
                    case 'D': Spawn(drifterPrefab, pos, actorParent); break;
                }
            }
        }
    }

    private void Spawn(GameObject prefab, Vector2 pos, Transform parent)
    {
        if (prefab == null)
        {
            Debug.LogWarning("LevelBuilder: a prefab slot is empty, skipping a tile.");
            return;
        }
        Instantiate(prefab, pos, Quaternion.identity, parent);
    }

    // ---------------- Grid helpers ----------------

    public Vector2 GridToWorld(Vector2Int cell)
    {
        return new Vector2(cell.x * tileSize, -cell.y * tileSize);
    }

    public Vector2Int WorldToGrid(Vector2 worldPos)
    {
        return new Vector2Int(Mathf.RoundToInt(worldPos.x / tileSize), Mathf.RoundToInt(-worldPos.y / tileSize));
    }

    public bool IsWalkable(Vector2Int cell)
    {
        return cell.x >= 0 && cell.y >= 0 && cell.x < width && cell.y < height && walkable[cell.x, cell.y];
    }

    public Vector2 GetRandomWalkablePosition()
    {
        return GridToWorld(walkableTiles[Random.Range(0, walkableTiles.Count)]);
    }

    /// <summary>A random floor tile within 'radius' of a point (used by Hunters guarding the pod).</summary>
    public Vector2 GetRandomWalkablePositionNear(Vector2 center, float radius)
    {
        var nearby = new List<Vector2>();
        foreach (Vector2Int tile in walkableTiles)
        {
            Vector2 pos = GridToWorld(tile);
            if (Vector2.Distance(pos, center) <= radius) nearby.Add(pos);
        }
        return nearby.Count > 0 ? nearby[Random.Range(0, nearby.Count)] : center;
    }

    /// <summary>
    /// Breadth-first search across the tile grid. Returns world-space waypoints from
    /// (but not including) the start tile to the goal tile. Empty list = no path.
    /// </summary>
    public List<Vector2> FindPath(Vector2 fromWorld, Vector2 toWorld)
    {
        var result = new List<Vector2>();
        Vector2Int start = WorldToGrid(fromWorld);
        Vector2Int goal = WorldToGrid(toWorld);
        if (!IsWalkable(start) || !IsWalkable(goal)) return result;

        // cameFrom remembers how we reached each tile so we can walk the path backwards.
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var frontier = new Queue<Vector2Int>();
        frontier.Enqueue(start);
        cameFrom[start] = start;

        while (frontier.Count > 0)
        {
            Vector2Int current = frontier.Dequeue();
            if (current == goal) break;

            foreach (Vector2Int dir in Directions)
            {
                Vector2Int next = current + dir;
                if (IsWalkable(next) && !cameFrom.ContainsKey(next))
                {
                    cameFrom[next] = current;
                    frontier.Enqueue(next);
                }
            }
        }

        if (!cameFrom.ContainsKey(goal)) return result; // unreachable

        // Walk back from the goal to the start, then flip the list.
        Vector2Int step = goal;
        while (step != start)
        {
            result.Add(GridToWorld(step));
            step = cameFrom[step];
        }
        result.Reverse();
        return result;
    }
}
