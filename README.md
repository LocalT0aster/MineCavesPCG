# Mine Caves PCG - Report
by Danil Nesterov <d.nesterov@innopolis.university>

<https://github.com/LocalT0aster/MineCavesPCG>

## Overview
This project combines two complementary procedural content generation techniques to build tile-based cave systems for a top-down mining prototype. A cellular automata (CA) pass creates large-scale cavern shapes, while a constrained Drunkard's Walk (DW) digger overlays structured corridors with rail lines for traversal guidance. I was inspired by Minecraft's cave systems with a bit spontaneous Mineshafts. It kind of resembles how dwarves would dig the tunnels.

## Applied Algorithms

### Cellular Automata

The map begins as white noise seeded by a deterministic PRNG. Each cell is marked solid or empty according to a configurable rock threshold. During smoothing iterations the CA evaluates neighbourhood kernels whose size and birth/death limits are chosen via a low-frequency Perlin mask, yielding regions with distinct density biases (e.g., 3x3 kernels in open areas, 9x9 in heavy rock zones). Border preservation, optional edge wrapping, and tilemap painting occur in `Assets/Scripts/CellularAutomata.cs`. This stage produces organic caves that still retain reproducibility through exposed seed parameters.

### Drunkard's Walk

After the base cavern is carved, `Assets/Scripts/DrunkardsWalk.cs` launches several token-limited walkers. Start sites are sampled near the map centre but must pass a large-radius solidity check to avoid overlapping entrances. Each walker:
- Prefers stepping into solid tiles to open new corridors; moving through existing voids incurs a sharper token cost.
- Carves three-tile-wide passages per step, writing floor tiles and recording rail orientation data.
- Rejects directions that would run a rail parallel to an existing one, but allows perpendicular crossings to form junctions.
Once walks finish, `Assets/Scripts/RailTileReplacer.cs` inspects neighbours and swaps each placeholder rail for a vertical, horizontal, or intersection tile, ensuring visual coherence.

## Evaluation

### Pros

- Region-weighted CA kernels create macro-variation (dense pockets vs. open caverns) with a single seed value.
- Drunkard constraints (token budgets, orientation checks) produce readable corridors without manual room markers.
- Rail replacement and automatic player placement reduce setup friction and guarantee playable spawns.

### Cons

- CA smoothing iterations are CPU-bound on large maps; runtime generation may need throttling or Burst jobs.
- Drunkard walkers can still backtrack excessively before consuming tokens, occasionally leaving stub tunnels.
- Reliance on Perlin for kernel selection may introduce repeatable banding if noise frequency is not tuned per map size.

## Future Work

1. **Look-Ahead Digger / Blind Digger Hybrids:** Add secondary diggers to expand off the main rail lines into ore rooms, leveraging look-ahead heuristics for chamber placement.
2. **Quadtree or BSP Post-Processing:** Partition the CA result to tag regions for biome dressing or enemy spawns.
3. **Dijkstra Maps:** Generate navigation heatmaps from rail stations to support AI pathing or loot distribution.

## Implementation Notes

- Generation orchestration lives in `Assets/Scripts/MapGeneration.cs`, which wires CA, DW, rail replacement, and positions the player on the first empty tile.
- Player movement/mining (`Assets/Scripts/PlayerController.cs`) and camera zoom (`Assets/Scripts/CameraFollowZoom.cs`) use Unity's Input System to remain compatible with Unity 6 projects.
## Screenshots
![](img/0.jpg)
1. After CA + Perlin
![](img/1.jpg)
2. After Drunkard's Walk & Rail direction fixup
![](img/2.jpg)
3. Closeup of the cave
![](img/3.jpg)
4. Happy cube mining through the rock with a pickaxe

## References

- Bob Nystrom, "Cellular Automata Method for Generating Cave-like Levels." *RogueBasin*. <http://www.roguebasin.com/index.php?title=Cellular_Automata_Method_for_Generating_Random_Cave-Like_Levels>
- Amit Patel, "Drunkard's Walk / Random Walks." *Red Blob Games*. <https://www.redblobgames.com/articles/probability/dungeon-drunks.html>
- Unity Technologies, "2D Tilemap Extras and Rule Tiles." <https://docs.unity3d.com/Manual/Tilemap.html>
