using UnityEngine;
using UnityEngine.Tilemaps;

/// Generates cave-like tilemaps using cellular automata seeded with white noise and Perlin-controlled kernel regions.
[RequireComponent(typeof(Tilemap))]
public class CellularAutomata : MonoBehaviour {
    /// Supported neighborhood kernel sizes for cellular automata smoothing.
    private enum KernelType {
        Size3 = 3,
        Size5 = 5,
        Size7 = 7,
        Size9 = 9
    }
    
    /// Rule configuration for a single kernel size.
    [System.Serializable]
    private struct KernelRule {
        /// Kernel size used when evaluating this rule.
        public KernelType kernelType;
        /// Neighbor count required to turn an empty cell solid.
        [Range(0, 80)] public int birthLimit;
        /// Neighbor count required to keep a cell solid.
        [Range(0, 80)] public int deathLimit;

        public int KernelSize => Mathf.Clamp((int)kernelType, 3, 9);
        public int Radius => Mathf.Max(1, (KernelSize - 1) / 2);
    }

    [Header("Tilemap Output")]
    
    [SerializeField] private Tilemap targetTilemap;
    [SerializeField] private TileBase solidTile; /// Tile to paint.
    [SerializeField] private Vector3Int tileOrigin = Vector3Int.zero;
    [SerializeField] private bool clearBeforePainting = true;

    [Header("Map Settings")]
    
    [SerializeField] private Vector2Int mapSize = new(512, 512);
    [SerializeField] private bool wrapHorizontalEdges;
    [SerializeField] private bool wrapVerticalEdges;
    [SerializeField] private bool keepBorderWalls = true;

    [Header("White Noise Seeding")]
    
    [SerializeField, Range(0f, 1f)] private float initialRockThreshold = 0.5f;
    [SerializeField] private int seed = 0;
    [SerializeField] private bool randomizeSeedOnGenerate = true;

    [Header("Cellular Automata Rules")]
    
    [SerializeField, Range(0.001f, 1f)]
    private float kernelNoiseScale = 0.01f; /// Perlin's frequency. Responsible for kernels distribution.
    [SerializeField] private Vector2 kernelNoiseOffset = Vector2.zero; /// Perlin's offset. Determined by the seed.
    [SerializeField] private KernelRule[] kernelRules = {
        new() { kernelType = KernelType.Size3, birthLimit = 5, deathLimit = 4 },
        new() { kernelType = KernelType.Size5, birthLimit = 15, deathLimit = 9 },
        new() { kernelType = KernelType.Size7, birthLimit = 27, deathLimit = 21 },
        new() { kernelType = KernelType.Size9, birthLimit = 38, deathLimit = 36 }
    };

    private const int DefaultBirthLimit = 4;
    private const int DefaultDeathLimit = 3;

    [SerializeField, Range(0, 10)] private int smoothingIterations = 9;
    [SerializeField] private bool autoGenerateOnStart = true;

    private bool[,] lastGeneratedMap;

    /// Gets the configured map dimensions.
    public Vector2Int MapSize => mapSize;
    /// Gets the map produced during the last generation pass.
    public bool[,] LastGeneratedMap => lastGeneratedMap;
    /// Gets the tile origin used when writing to the target tilemap.
    public Vector3Int TileOrigin => tileOrigin;
    /// Gets the tilemap this generator paints into.
    public Tilemap TargetTilemap => targetTilemap;

    /// Optionally generates and paints caves on startup.
    private void Start() {
        if (!targetTilemap)
            targetTilemap = GetComponent<Tilemap>();
        if (autoGenerateOnStart)
            GenerateCaves();
    }

    /// Generates cave data and writes the result to the configured tilemap.
    public void GenerateCaves() {
        var map = GenerateCaveData();

        if (targetTilemap == null) {
            Debug.LogWarning($"{nameof(CellularAutomata)} on {name} has no Tilemap assigned.");
            return;
        }

        if (clearBeforePainting)
            targetTilemap.ClearAllTiles();

        if (solidTile == null)
            Debug.LogWarning($"{nameof(CellularAutomata)} on {name} has no wall tile assigned.");

        PaintToTilemap(map);
    }
    
    /// Builds a cave map using white-noise seeding and region-specific cellular automata kernels.
    /// <returns>The generated cave map, where true represents a wall/solid.</returns>
    public bool[,] GenerateCaveData() {
        Vector2Int clampedSize = new Vector2Int(Mathf.Max(1, mapSize.x), Mathf.Max(1, mapSize.y));

        int width = clampedSize.x;
        int height = clampedSize.y;

        bool[,] map = new bool[width, height];
        bool[,] buffer = new bool[width, height];
        KernelRule[,] kernelMap = new KernelRule[width, height];

        int generationSeed = randomizeSeedOnGenerate ? Random.Range(int.MinValue, int.MaxValue) : seed;
        var prng = new System.Random(generationSeed);
        KernelRule[] sortedKernelRules = GetKernelRulesSortedBySize();
        float effectiveKernelScale = Mathf.Max(0.0001f, kernelNoiseScale);
        Vector2 kernelOffset = kernelNoiseOffset + new Vector2(prng.Next(-100000, 100000), prng.Next(-100000, 100000));

        for (int x = 0; x < width; x++) {
            for (int y = 0; y < height; y++) {
                float kernelSampleX = (x + kernelOffset.x) * effectiveKernelScale;
                float kernelSampleY = (y + kernelOffset.y) * effectiveKernelScale;
                float kernelNoise = Mathf.PerlinNoise(kernelSampleX, kernelSampleY);
                kernelMap[x, y] = GetRuleForNoise(kernelNoise, sortedKernelRules);

                bool borderCell = keepBorderWalls && (x == 0 || y == 0 || x == width - 1 || y == height - 1);

                if (borderCell) {
                    map[x, y] = true;
                    continue;
                }

                double sample = prng.NextDouble();
                map[x, y] = sample >= initialRockThreshold;
            }
        }

        for (int i = 0; i < smoothingIterations; i++) {
            for (int x = 0; x < width; x++) {
                for (int y = 0; y < height; y++) {
                    if (keepBorderWalls && (x == 0 || y == 0 || x == width - 1 || y == height - 1)) {
                        buffer[x, y] = true;
                        continue;
                    }

                    KernelRule rule = kernelMap[x, y];
                    int radius = rule.Radius;
                    int neighbors = CountSolidNeighbors(map, x, y, width, height, radius);
                    bool isSolid = map[x, y];
                    int maxNeighbors = (rule.KernelSize * rule.KernelSize) - 1;
                    int localDeathLimit = Mathf.Clamp(rule.deathLimit, 0, maxNeighbors);
                    int localBirthLimit = Mathf.Clamp(rule.birthLimit, 0, maxNeighbors);

                    if (isSolid) {
                        buffer[x, y] = neighbors >= localDeathLimit;
                    }
                    else {
                        buffer[x, y] = neighbors > localBirthLimit;
                    }
                }
            }

            // Swap buffers
            (map, buffer) = (buffer, map);
        }

        lastGeneratedMap = map;
        return map;
    }
    
    /// Paints solid cells from the generated cave map onto the target tilemap.
    private void PaintToTilemap(bool[,] map) {
        int width = map.GetLength(0);
        int height = map.GetLength(1);

        for (int x = 0; x < width; x++) {
            for (int y = 0; y < height; y++) {
                if (map[x, y]) {
                    Vector3Int position = new Vector3Int(tileOrigin.x + x, tileOrigin.y + y, tileOrigin.z);
                    targetTilemap.SetTile(position, solidTile);
                }
            }
        }

        targetTilemap.RefreshAllTiles();
    }
    
    /// Counts the number of solid neighbors around a position using the supplied kernel radius.
    /// <param name="map">The source map to inspect.</param>
    /// <param name="x">Cell x index.</param>
    /// <param name="y">Cell y index.</param>
    /// <param name="mapWidth">Map width.</param>
    /// <param name="mapHeight">Map height.</param>
    /// <param name="radius">Radius of the kernel (1 for 3x3, etc.).</param>
    /// <returns>Total solid neighbor count.</returns>
    private int CountSolidNeighbors(bool[,] map, int x, int y, int mapWidth, int mapHeight, int radius) {
        int count = 0;

        for (int nx = -radius; nx <= radius; nx++) {
            for (int ny = -radius; ny <= radius; ny++) {
                if (nx == 0 && ny == 0) {
                    continue;
                }

                int sampleX = x + nx;
                int sampleY = y + ny;

                if (wrapHorizontalEdges && mapWidth > 0) {
                    if (sampleX < 0 || sampleX >= mapWidth) {
                        sampleX = ((sampleX % mapWidth) + mapWidth) % mapWidth;
                    }
                }

                if (wrapVerticalEdges && mapHeight > 0) {
                    if (sampleY < 0 || sampleY >= mapHeight) {
                        sampleY = ((sampleY % mapHeight) + mapHeight) % mapHeight;
                    }
                }

                bool isOutside = sampleX < 0 || sampleY < 0 || sampleX >= mapWidth || sampleY >= mapHeight;

                if (isOutside) {
                    count++;
                }
                else if (map[sampleX, sampleY]) {
                    count++;
                }
            }
        }
        return count;
    }
    
    /// Returns the configured kernel rules sorted by kernel size, or defaults if none are provided.
    private KernelRule[] GetKernelRulesSortedBySize() {
        if (kernelRules == null || kernelRules.Length <= 0)
            return new[] {
                new KernelRule {
                    kernelType = KernelType.Size3,
                    birthLimit = DefaultBirthLimit,
                    deathLimit = DefaultDeathLimit
                }
            };
        KernelRule[] copy = (KernelRule[])kernelRules.Clone();
        System.Array.Sort(copy, (a, b) => a.KernelSize.CompareTo(b.KernelSize));
        return copy;
    }
    
    /// Selects a kernel rule based on the supplied Perlin noise sample.
    /// <param name="noiseValue">Normalized noise value in the range [0,1].</param>
    /// <param name="sortedKernelRules">Kernel rules sorted by kernel size.</param>
    /// <returns>The rule that corresponds to the noise sample.</returns>
    private KernelRule GetRuleForNoise(float noiseValue, KernelRule[] sortedKernelRules) {
        if (sortedKernelRules == null || sortedKernelRules.Length == 0) {
            return new KernelRule {
                kernelType = KernelType.Size3,
                birthLimit = DefaultBirthLimit,
                deathLimit = DefaultDeathLimit
            };
        }

        float clampedNoise = Mathf.Clamp01(noiseValue);
        int index = Mathf.Clamp(Mathf.FloorToInt(clampedNoise * sortedKernelRules.Length), 0,
            sortedKernelRules.Length - 1);
        return sortedKernelRules[index];
    }
}
