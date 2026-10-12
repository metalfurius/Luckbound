using UnityEngine;

namespace Luckbound.Gameplay.Rooms
{
    /// <summary>Named gameplay position on a walkable floor, derived from room-template JSON.</summary>
    public sealed class RoomPositionMarker2D : MonoBehaviour
    {
        [SerializeField] private string _markerId;
        [SerializeField] private string _kind;
        [SerializeField] private int _tileX;
        [SerializeField] private int _tileY;

        public string MarkerId => _markerId;
        public string Kind => _kind;
        public Vector2Int TilePosition => new Vector2Int(_tileX, _tileY);

        public void Configure(string markerId, string kind, int tileX, int tileY)
        {
            _markerId = markerId;
            _kind = kind;
            _tileX = tileX;
            _tileY = tileY;
        }
    }
}
