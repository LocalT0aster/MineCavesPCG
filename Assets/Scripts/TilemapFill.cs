using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Tilemap))]
public class TilemapFill : MonoBehaviour {
    private Tilemap _tilemap;
    [SerializeField] private TileBase fillTile;
    [SerializeField] private bool fillByDefault;
    [SerializeField] private Vector2Int fillFrom = Vector2Int.zero;
    [SerializeField] private Vector2Int fillTo = Vector2Int.one;

    private void Start() {
        _tilemap = GetComponent<Tilemap>();
        if (fillByDefault)
            Fill(fillFrom, fillTo);
    }

    public void Fill(Vector2Int from, Vector2Int to) {
        if (_tilemap == null) {
            Debug.LogWarning($"{nameof(TilemapFill)} on {name} has no Tilemap assigned.");
            return;
        }

        if (fillTile == null) {
            Debug.LogWarning($"{nameof(TilemapFill)} on {name} has no fill tile assigned.");
            return;
        }

        int minX = Mathf.Min(from.x, to.x);
        int maxX = Mathf.Max(from.x, to.x);
        int minY = Mathf.Min(from.y, to.y);
        int maxY = Mathf.Max(from.y, to.y);

        for (int x = minX; x <= maxX; x++) {
            for (int y = minY; y <= maxY; y++) {
                _tilemap.SetTile(new Vector3Int(x, y, 0), fillTile);
            }
        }

        _tilemap.RefreshAllTiles();
    }
}
