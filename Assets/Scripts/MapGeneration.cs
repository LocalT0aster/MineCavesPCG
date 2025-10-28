using UnityEngine;

/// Coordinates cellular automata cave generation followed by drunkard-carved corridors and rail tile cleanup.
public class MapGeneration : MonoBehaviour {
    [Header("Map Settings")]
    [SerializeField] private Vector2Int mapSize = new(512, 512);

    [Header("Generation Steps")]
    [SerializeField] private CellularAutomata cellularAutomata;
    [SerializeField] private DrunkardsWalk drunkardsWalk;
    [SerializeField] private RailTileReplacer railTileReplacer;

    [Header("Execution Settings")]
    [SerializeField] private bool generateOnStart = true;
    [SerializeField] private bool randomizeDrunkardSeed = true;
    [SerializeField] private int drunkardSeed = 1;

    private void Start() {
        if (generateOnStart) {
            Generate();
        }
    }

    /// Runs the full map generation pipeline.
    public void Generate() {
        if (cellularAutomata == null) {
            Debug.LogWarning($"{nameof(MapGeneration)} on {name} has no {nameof(cellularAutomata)} assigned.");
            return;
        }

        if (drunkardsWalk == null) {
            Debug.LogWarning($"{nameof(MapGeneration)} on {name} has no {nameof(drunkardsWalk)} assigned.");
            return;
        }

        cellularAutomata.MapSize = mapSize;
        cellularAutomata.GenerateCaves();

        bool[,] map = cellularAutomata.LastGeneratedMap;
        if (map == null) {
            Debug.LogWarning($"{nameof(MapGeneration)} on {name} could not obtain cave data from {nameof(CellularAutomata)}.");
            return;
        }

        if (drunkardsWalk.CaveTilemap == null) {
            drunkardsWalk.SetCaveTilemap(cellularAutomata.TargetTilemap);
        }

        System.Random rng = randomizeDrunkardSeed
            ? new System.Random(UnityEngine.Random.Range(int.MinValue, int.MaxValue))
            : new System.Random(drunkardSeed);

        drunkardsWalk.CarveCorridors(map, cellularAutomata.TileOrigin, rng);

        if (railTileReplacer != null && railTileReplacer.isActiveAndEnabled) {
            if (railTileReplacer.RailTilemap == null) {
                railTileReplacer.SetRailTilemap(drunkardsWalk.RailTilemap);
            }

            railTileReplacer.UpdateRailTiles();
        }
    }
}
