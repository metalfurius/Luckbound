using UnityEngine;

namespace Luckbound.Gameplay.Rooms
{
    /// <summary>Serialized lateral connection point derived from a room-template JSON port.</summary>
    public sealed class RoomPortMarker2D : MonoBehaviour
    {
        [SerializeField] private string _portId;
        [SerializeField] private string _type;
        [SerializeField] private string _side;
        [SerializeField] private int _tileX;
        [SerializeField] private int _tileY;
        [SerializeField] private int _clearanceTiles;

        public string PortId => _portId;
        public string Type => _type;
        public string Side => _side;
        public Vector2Int TilePosition => new Vector2Int(_tileX, _tileY);
        public int ClearanceTiles => _clearanceTiles;

        public void Configure(string portId, string type, string side, int tileX, int tileY, int clearanceTiles)
        {
            _portId = portId;
            _type = type;
            _side = side;
            _tileX = tileX;
            _tileY = tileY;
            _clearanceTiles = clearanceTiles;
        }
    }
}
