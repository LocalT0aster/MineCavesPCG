using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// Replaces placeholder rail tiles with orientation-specific tiles based on neighbouring rail cells.
public class RailTileReplacer : MonoBehaviour {
    [Header("Tilemap")]
    [SerializeField] private Tilemap railTilemap;

    [Header("Tiles")]
    [SerializeField] private TileBase verticalTile;
    [SerializeField] private TileBase horizontalTile;
    [SerializeField] private TileBase intersectionTile;

    [Header("Execution")]
    [SerializeField] private bool updateOnStart = false;

    private readonly List<Vector3Int> _buffer = new();

    private void Start() {
        if (updateOnStart) {
            UpdateRailTiles();
        }
    }

    /// Replaces every rail tile on the tilemap with the correct variant.
    public void UpdateRailTiles() {
        if (railTilemap == null) {
            Debug.LogWarning($"{nameof(RailTileReplacer)} on {name} has no rail tilemap assigned.");
            return;
        }

        GatherRailCells();
        ApplyTiles();
        railTilemap.RefreshAllTiles();
    }

    /// Assigns the tilemap to operate on if not already configured.
    /// <param name="tilemap">Tilemap containing the rail cells.</param>
    public void SetRailTilemap(Tilemap tilemap) {
        railTilemap = tilemap;
    }

    /// Returns the tilemap currently targeted by this replacer.
    public Tilemap RailTilemap => railTilemap;

    private void GatherRailCells() {
        _buffer.Clear();

        BoundsInt bounds = railTilemap.cellBounds;
        foreach (Vector3Int position in bounds.allPositionsWithin) {
            if (railTilemap.HasTile(position)) {
                _buffer.Add(position);
            }
        }
    }

    private void ApplyTiles() {
        foreach (Vector3Int cell in _buffer) {
            bool up = railTilemap.HasTile(cell + Vector3Int.up);
            bool down = railTilemap.HasTile(cell + Vector3Int.down);
            bool left = railTilemap.HasTile(cell + Vector3Int.left);
            bool right = railTilemap.HasTile(cell + Vector3Int.right);

            bool vertical = up || down;
            bool horizontal = left || right;

            TileBase tileToUse = ChooseTile(vertical, horizontal);
            if (tileToUse != null) {
                railTilemap.SetTile(cell, tileToUse);
            }
        }
    }

    private TileBase ChooseTile(bool vertical, bool horizontal) {
        if (vertical && horizontal) {
            return intersectionTile ? intersectionTile : verticalTile ?? horizontalTile;
        }

        if (vertical) {
            return verticalTile ?? intersectionTile ?? horizontalTile;
        }

        if (horizontal) {
            return horizontalTile ?? intersectionTile ?? verticalTile;
        }

        // Isolated piece defaults to intersection if available.
        return intersectionTile ?? verticalTile ?? horizontalTile;
    }
}
