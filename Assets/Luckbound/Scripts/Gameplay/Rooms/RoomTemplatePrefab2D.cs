using System;
using System.Linq;
using Luckbound.Content;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Luckbound.Gameplay.Rooms
{
    /// <summary>Connects generated room geometry and markers to their authoritative JSON definition.</summary>
    public sealed class RoomTemplatePrefab2D : MonoBehaviour
    {
        private const float PositionTolerance = 0.001f;
        [SerializeField] private TextAsset _sourceDefinition;
        [SerializeField] private BoxCollider2D _cameraBounds;
        [SerializeField] private Tilemap _groundTilemap;
        [SerializeField] private TilemapCollider2D _groundCollider;

        public TextAsset SourceDefinition => _sourceDefinition;
        public BoxCollider2D CameraBounds => _cameraBounds;
        public Tilemap GroundTilemap => _groundTilemap;
        public TilemapCollider2D GroundCollider => _groundCollider;

        public void Configure(TextAsset sourceDefinition, BoxCollider2D cameraBounds, Tilemap groundTilemap, TilemapCollider2D groundCollider)
        {
            _sourceDefinition = sourceDefinition;
            _cameraBounds = cameraBounds;
            _groundTilemap = groundTilemap;
            _groundCollider = groundCollider;
        }

        public RoomTemplateData ValidateGeneratedPrefab()
        {
            if (_sourceDefinition == null) throw new InvalidOperationException("Room prefab is missing its JSON source definition.");
            RoomTemplateData data = RoomTemplateJsonLoader.Load(_sourceDefinition.text);
            if (_cameraBounds == null || !_cameraBounds.isTrigger || !IsNear(_cameraBounds.transform.localPosition,
                new Vector3(data.widthTiles * data.tileSize * 0.5f, data.heightTiles * data.tileSize * 0.5f, 0f)) ||
                !IsNear(_cameraBounds.size, new Vector2(data.widthTiles * data.tileSize, data.heightTiles * data.tileSize)))
            {
                throw new InvalidOperationException("Room camera bounds do not match the template dimensions.");
            }

            ValidateGroundTilemap(data);
            ValidatePorts(data);
            ValidatePositionMarkers(data);
            return data;
        }

        private void ValidateGroundTilemap(RoomTemplateData data)
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            if (_groundTilemap == null || _groundCollider == null || _groundCollider.isTrigger ||
                _groundCollider.gameObject != _groundTilemap.gameObject || _groundTilemap.gameObject.layer != groundLayer ||
                !IsNear(_groundTilemap.transform.localPosition, Vector3.zero) ||
                Quaternion.Angle(_groundTilemap.transform.localRotation, Quaternion.identity) > PositionTolerance ||
                !IsNear(_groundTilemap.transform.localScale, Vector3.one) ||
                !IsNear(new Vector2(_groundTilemap.cellSize.x, _groundTilemap.cellSize.y), new Vector2(data.tileSize, data.tileSize)) ||
                !IsNear(_groundTilemap.tileAnchor, new Vector3(0.5f, 0.5f, 0f)) || groundLayer < 0)
            {
                throw new InvalidOperationException("Room ground must use an unscaled Ground-layer Tilemap with a solid TilemapCollider2D and template-sized cells.");
            }

            for (int y = 0; y < data.heightTiles; y++)
            {
                for (int x = 0; x < data.widthTiles; x++)
                {
                    bool shouldHaveTile = RoomTemplateJsonLoader.GetCell(data, x, y) == '#';
                    bool hasTile = _groundTilemap.HasTile(new Vector3Int(x, y, 0));
                    Tile tile = _groundTilemap.GetTile<Tile>(new Vector3Int(x, y, 0));
                    if (shouldHaveTile != hasTile || (shouldHaveTile &&
                        (tile == null || tile.sprite == null || tile.colliderType != Tile.ColliderType.Grid)))
                    {
                        throw new InvalidOperationException("Ground Tilemap cells must match the solid cells and collision settings in the room JSON.");
                    }
                }
            }

            BoundsInt tileBounds = _groundTilemap.cellBounds;
            foreach (Vector3Int cell in tileBounds.allPositionsWithin)
            {
                if (_groundTilemap.HasTile(cell) && RoomTemplateJsonLoader.GetCell(data, cell.x, cell.y) != '#')
                {
                    throw new InvalidOperationException("Ground Tilemap contains a tile outside the room JSON grid.");
                }
            }
        }

        private void ValidatePorts(RoomTemplateData data)
        {
            RoomPortMarker2D[] markers = GetComponentsInChildren<RoomPortMarker2D>(true);
            if (markers.Length != data.ports.Length) throw new InvalidOperationException("Generated port markers do not match the JSON ports.");
            foreach (RoomPortData port in data.ports)
            {
                RoomPortMarker2D marker = markers.SingleOrDefault(item => item.PortId == port.id);
                Vector3 expectedPosition = new Vector3(
                    (port.side == "west" ? 0f : data.widthTiles) * data.tileSize,
                    port.tileY * data.tileSize,
                    0f);
                if (marker == null || marker.Type != port.type || marker.Side != port.side ||
                    marker.TilePosition != new Vector2Int(port.tileX, port.tileY) || marker.ClearanceTiles != port.clearanceTiles ||
                    !IsNear(marker.transform.localPosition, expectedPosition))
                {
                    throw new InvalidOperationException("Generated port marker " + port.id + " does not match its JSON definition.");
                }
            }
        }

        private void ValidatePositionMarkers(RoomTemplateData data)
        {
            RoomPositionMarker2D[] markers = GetComponentsInChildren<RoomPositionMarker2D>(true);
            if (markers.Length != data.markers.Length) throw new InvalidOperationException("Generated position markers do not match the JSON markers.");
            foreach (RoomPositionMarkerData definition in data.markers)
            {
                RoomPositionMarker2D marker = markers.SingleOrDefault(item => item.MarkerId == definition.id);
                Vector3 expectedPosition = new Vector3((definition.tileX + 0.5f) * data.tileSize, definition.tileY * data.tileSize, 0f);
                if (marker == null || marker.Kind != definition.kind ||
                    marker.TilePosition != new Vector2Int(definition.tileX, definition.tileY) ||
                    !IsNear(marker.transform.localPosition, expectedPosition))
                {
                    throw new InvalidOperationException("Generated position marker " + definition.id + " does not match its JSON definition.");
                }
            }
        }

        private static bool IsNear(Vector3 actual, Vector3 expected)
        {
            return (actual - expected).sqrMagnitude <= PositionTolerance * PositionTolerance;
        }

        private static bool IsNear(Vector2 actual, Vector2 expected)
        {
            return (actual - expected).sqrMagnitude <= PositionTolerance * PositionTolerance;
        }
    }
}
