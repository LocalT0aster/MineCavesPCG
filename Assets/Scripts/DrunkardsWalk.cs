using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;


/// Carves three-wide corridors through a cave map using a constrained drunkard's walk and paints rails along the centre lane.
[DisallowMultipleComponent]
public class DrunkardsWalk : MonoBehaviour {
    private static readonly Vector2Int[] CardinalDirections = {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };
    [Header("Map Settings")]
    
    public Vector2Int MapSize = new(512, 512);

    [Header("Tilemap Output")]
    [SerializeField] private Tilemap caveTilemap;
    [SerializeField] private TileBase corridorTile = null;
    [SerializeField] private Tilemap railTilemap;
    [SerializeField] private TileBase railTile;
    [SerializeField] private bool clearRailLayerOnGenerate = true;

    [Header("Drunkard Settings")]
    [SerializeField, Min(1)] private int drunkardCount = 3;
    [SerializeField, Min(1)] private int tokensPerDrunkard = 600;
    [SerializeField, Min(1)] private int solidStepCost = 1;
    [SerializeField, Min(2)] private int nonSolidPenalty = 6;
    [SerializeField, Min(0)] private int minimumStartSpacing = 12;

    [Header("Start Placement")]
    [SerializeField, Min(1)] private int startKernelRadius = 6;
    [SerializeField, Min(0)] private int startKernelThreshold = 80;
    [SerializeField, Min(0)] private int startSearchAttempts = 512;

    [Header("Direction Weights")]
    [SerializeField, Min(1)] private int solidPreferenceWeight = 3;
    [SerializeField, Min(1)] private int openPreferenceWeight = 1;
    [SerializeField, Min(0)] private int forwardPreferenceBonus = 2;

    private readonly Dictionary<Vector2Int, Vector2Int> railOrientation = new();
    /// Provides access to the currently assigned cave tilemap.
    public Tilemap CaveTilemap => caveTilemap;
    /// Provides access to the rail tilemap used for painting tracks.
    public Tilemap RailTilemap => railTilemap;

    /// Assigns the tilemap used when carving corridors if it is not already configured.
    /// <param name="tilemap">Tilemap that contains the cellular automata output.</param>
    public void SetCaveTilemap(Tilemap tilemap) {
        caveTilemap = tilemap;
    }

    /// Assigns the tilemap used for rails.
    /// <param name="tilemap">Tilemap that should receive rail tiles.</param>
    public void SetRailTilemap(Tilemap tilemap) {
        railTilemap = tilemap;
    }

    /// Executes the drunkard's walk, carving three-tile-wide corridors through the supplied map data.
    /// <param name="map">Map data where true indicates a solid cell.</param>
    /// <param name="tileOrigin">Origin offset applied when writing to tilemaps.</param>
    /// <param name="rng">Optional random source; if omitted a new <see cref="System.Random"/> is created.</param>
    public void CarveCorridors(bool[,] map, Vector3Int tileOrigin, System.Random rng = null) {
        if (map == null) {
            Debug.LogWarning($"{nameof(DrunkardsWalk)} on {name} received a null map.");
            return;
        }

        if (caveTilemap == null) {
            Debug.LogWarning($"{nameof(DrunkardsWalk)} on {name} has no cave tilemap assigned.");
            return;
        }

        if (railTilemap == null) {
            Debug.LogWarning($"{nameof(DrunkardsWalk)} on {name} has no rail tilemap assigned.");
            return;
        }

        if (railTile == null) {
            Debug.LogWarning($"{nameof(DrunkardsWalk)} on {name} has no rail tile assigned.");
            return;
        }

        rng ??= new System.Random();

        if (clearRailLayerOnGenerate) {
            railTilemap.ClearAllTiles();
        }

        railOrientation.Clear();
        
        var usedStarts = new List<Vector2Int>();

        for (int i = 0; i < drunkardCount; i++) {
            if (!TryFindStart(map, usedStarts, rng, out Vector2Int start)) {
                break;
            }

            usedStarts.Add(start);

            Vector2Int initialDirection = SelectInitialDirection(start, map, rng);
            CarveCorridorAt(start, initialDirection, map, tileOrigin);
            WalkDrunkard(start, initialDirection, map, tileOrigin, rng);
        }

        caveTilemap.RefreshAllTiles();
        railTilemap.RefreshAllTiles();
    }

    private void WalkDrunkard(Vector2Int start, Vector2Int direction, bool[,] map, Vector3Int origin, System.Random rng) {
        Vector2Int current = start;
        Vector2Int previousDirection = direction;
        int tokens = tokensPerDrunkard;

        while (tokens > 0) {
            if (!TrySelectDirection(current, previousDirection, map, rng, out DirectionChoice choice)) {
                break;
            }

            Vector2Int next = choice.Next;
            bool wasSolid = choice.NextIsSolid;

            current = next;
            previousDirection = choice.Direction;

            CarveCorridorAt(current, previousDirection, map, origin);

            int cost = wasSolid ? solidStepCost : nonSolidPenalty;
            tokens -= cost;
        }
    }

    private bool TrySelectDirection(Vector2Int current, Vector2Int previousDirection, bool[,] map, System.Random rng, out DirectionChoice choice) {
        var candidates = new List<DirectionChoice>();

        foreach (Vector2Int direction in CardinalDirections) {
            Vector2Int next = current + direction;
            if (!IsInside(next, MapSize)) {
                continue;
            }

            if (IsParallelToRails(current, next, direction)) {
                continue;
            }

            bool nextIsSolid = map[next.x, next.y];
            int weight = nextIsSolid ? solidPreferenceWeight : openPreferenceWeight;

            if (direction == previousDirection) {
                weight += forwardPreferenceBonus;
            }

            if (weight <= 0) {
                continue;
            }

            candidates.Add(new DirectionChoice(direction, next, nextIsSolid, weight));
        }

        if (candidates.Count == 0) {
            choice = default;
            return false;
        }

        int totalWeight = 0;
        foreach (DirectionChoice candidate in candidates) {
            totalWeight += candidate.Weight;
        }

        int roll = rng.Next(totalWeight);
        foreach (DirectionChoice candidate in candidates) {
            if (roll < candidate.Weight) {
                choice = candidate;
                return true;
            }

            roll -= candidate.Weight;
        }

        choice = candidates[candidates.Count - 1];
        return true;
    }

    private bool TryFindStart(bool[,] map, List<Vector2Int> usedStarts, System.Random rng, out Vector2Int start) {
        Vector2Int center = new(MapSize.x / 2, MapSize.y / 2);
        int searchRadiusX = MapSize.x / 2;
        int searchRadiusY = MapSize.y / 2;

        for (int attempt = 0; attempt < startSearchAttempts; attempt++) {
            int x = Mathf.Clamp(center.x + rng.Next(-searchRadiusX, searchRadiusX + 1), 0, MapSize.x - 1);
            int y = Mathf.Clamp(center.y + rng.Next(-searchRadiusY, searchRadiusY + 1), 0, MapSize.y - 1);
            Vector2Int candidate = new(x, y);

            if (!map[candidate.x, candidate.y]) {
                continue;
            }

            if (!HasMinimumDistance(candidate, usedStarts, minimumStartSpacing)) {
                continue;
            }

            int neighborCount = CountSolidNeighbors(map, candidate, MapSize, startKernelRadius);
            if (neighborCount < startKernelThreshold) {
                continue;
            }

            start = candidate;
            return true;
        }

        start = default;
        return false;
    }

    private Vector2Int SelectInitialDirection(Vector2Int start, bool[,] map, System.Random rng) {
        var candidates = new List<DirectionChoice>();

        foreach (Vector2Int direction in CardinalDirections) {
            Vector2Int next = start + direction;
            if (!IsInside(next, MapSize)) {
                continue;
            }

            bool nextIsSolid = map[next.x, next.y];
            int weight = nextIsSolid ? solidPreferenceWeight : openPreferenceWeight;

            if (weight > 0) {
                candidates.Add(new DirectionChoice(direction, next, nextIsSolid, weight));
            }
        }

        if (candidates.Count == 0) {
            return Vector2Int.up;
        }

        int totalWeight = 0;
        foreach (DirectionChoice candidate in candidates) {
            totalWeight += candidate.Weight;
        }

        int roll = rng.Next(totalWeight);
        foreach (DirectionChoice candidate in candidates) {
            if (roll < candidate.Weight) {
                return candidate.Direction;
            }

            roll -= candidate.Weight;
        }

        return candidates[candidates.Count - 1].Direction;
    }

    private void CarveCorridorAt(Vector2Int centre, Vector2Int direction, bool[,] map, Vector3Int origin) {
        Vector2Int perpendicular = new(-direction.y, direction.x);
        Vector2Int[] offsets = {
            Vector2Int.zero,
            perpendicular,
            -perpendicular
        };

        foreach (Vector2Int offset in offsets) {
            Vector2Int cell = centre + offset;
            if (!IsInside(cell, MapSize)) {
                continue;
            }

            map[cell.x, cell.y] = false;
            WriteCorridorTile(cell, origin);
        }

        WriteRailTile(centre, origin, direction);
    }

    private void WriteCorridorTile(Vector2Int cell, Vector3Int origin) {
        if (caveTilemap == null) {
            return;
        }

        Vector3Int tilePosition = origin + new Vector3Int(cell.x, cell.y, 0);
        caveTilemap.SetTile(tilePosition, corridorTile);
    }

    private void WriteRailTile(Vector2Int cell, Vector3Int origin, Vector2Int direction) {
        if (railOrientation.TryGetValue(cell, out Vector2Int existing)) {
            if (existing != Vector2Int.zero && existing != direction && existing != -direction) {
                railOrientation[cell] = Vector2Int.zero;
            }
        }
        else {
            railOrientation[cell] = direction;
        }

        if (railTilemap == null) {
            return;
        }

        Vector3Int tilePosition = origin + new Vector3Int(cell.x, cell.y, 0);
        railTilemap.SetTile(tilePosition, railTile);
    }

    private bool IsParallelToRails(Vector2Int current, Vector2Int next, Vector2Int direction) {
        Vector2Int perpendicular = new(-direction.y, direction.x);
        Vector2Int[] cellsToCheck = {
            next,
            next + perpendicular,
            next - perpendicular,
            current + perpendicular,
            current - perpendicular
        };

        foreach (Vector2Int cell in cellsToCheck) {
            if (!railOrientation.TryGetValue(cell, out Vector2Int orientation)) {
                continue;
            }

            if (cell == current) {
                continue;
            }

            if (orientation == Vector2Int.zero) {
                continue;
            }

            int alignment = Mathf.Abs(orientation.x * direction.x + orientation.y * direction.y);
            if (alignment == 1) {
                return true;
            }
        }

        return false;
    }

    private static bool IsInside(Vector2Int cell, Vector2Int size) {
        return cell.x >= 0 && cell.x < size.x && cell.y >= 0 && cell.y < size.y;
    }

    private static bool HasMinimumDistance(Vector2Int candidate, List<Vector2Int> points, int minimumDistance) {
        if (minimumDistance <= 0) {
            return true;
        }

        int minDistanceSquared = minimumDistance * minimumDistance;
        foreach (Vector2Int point in points) {
            if ((candidate - point).sqrMagnitude < minDistanceSquared) {
                return false;
            }
        }

        return true;
    }

    private int CountSolidNeighbors(bool[,] map, Vector2Int centre, Vector2Int size, int radius) {
        int count = 0;

        for (int dx = -radius; dx <= radius; dx++) {
            for (int dy = -radius; dy <= radius; dy++) {
                if (dx == 0 && dy == 0) {
                    continue;
                }

                int sampleX = centre.x + dx;
                int sampleY = centre.y + dy;

                if (sampleX < 0 || sampleX >= size.x || sampleY < 0 || sampleY >= size.y) {
                    continue;
                }

                if (map[sampleX, sampleY]) {
                    count++;
                }
            }
        }

        return count;
    }

    private readonly struct DirectionChoice {
        public DirectionChoice(Vector2Int direction, Vector2Int next, bool nextIsSolid, int weight) {
            Direction = direction;
            Next = next;
            NextIsSolid = nextIsSolid;
            Weight = weight;
        }

        public Vector2Int Direction { get; }
        public Vector2Int Next { get; }
        public bool NextIsSolid { get; }
        public int Weight { get; }
    }
}
