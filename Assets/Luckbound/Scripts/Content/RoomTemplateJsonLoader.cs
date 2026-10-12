using System;
using System.Collections.Generic;
using UnityEngine;

namespace Luckbound.Content
{
    /// <summary>Parses and validates the JSON source for a generated horizontal room template.</summary>
    public static class RoomTemplateJsonLoader
    {
        private const int MaximumDimensionTiles = 128;
        private const string EmptyCell = ".";
        private const string SolidCell = "#";
        private const string HazardCell = "!";

        public static RoomTemplateData Load(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("Room template JSON cannot be empty.", nameof(json));
            }

            RoomTemplateData data;
            try
            {
                data = JsonUtility.FromJson<RoomTemplateData>(json);
            }
            catch (Exception exception)
            {
                throw new FormatException("Room template JSON could not be parsed.", exception);
            }

            IReadOnlyList<string> errors = Validate(data);
            if (errors.Count > 0)
            {
                throw new FormatException("Invalid room template: " + string.Join("; ", errors));
            }

            return data;
        }

        public static IReadOnlyList<string> Validate(RoomTemplateData data)
        {
            var errors = new List<string>();
            if (data == null)
            {
                errors.Add("document is missing");
                return errors;
            }

            if (data.schemaVersion != 1) errors.Add("schemaVersion must be 1");
            if (!IsStableTemplateId(data.templateId)) errors.Add("templateId must use room.<sector>.<code> lowercase segments");
            if (data.widthTiles < 20 || data.widthTiles > MaximumDimensionTiles) errors.Add("widthTiles must be between 20 and 128");
            if (data.heightTiles < 12 || data.heightTiles > MaximumDimensionTiles) errors.Add("heightTiles must be between 12 and 128");
            if (data.tileSize <= 0f || float.IsNaN(data.tileSize) || float.IsInfinity(data.tileSize)) errors.Add("tileSize must be a finite positive world-unit value");
            else if (data.widthTiles * data.tileSize < 20f || data.heightTiles * data.tileSize < 11.25f) errors.Add("room world bounds must contain the full 20 by 11.25 unit camera frame");
            if (data.minimumPortClearanceTiles < 3 || data.minimumPortClearanceTiles > 8) errors.Add("minimumPortClearanceTiles must be between 3 and 8");
            if (data.minimumEnemyDistanceTiles < 8 || data.minimumEnemyDistanceTiles > MaximumDimensionTiles) errors.Add("minimumEnemyDistanceTiles must be at least 8");

            bool gridValid = ValidateGrid(data, errors);
            ValidatePorts(data, gridValid, errors);
            ValidateMarkers(data, gridValid, errors);
            return errors;
        }

        public static char GetCell(RoomTemplateData data, int tileX, int tileY)
        {
            if (data == null || data.gridRows == null || tileY < 0 || tileY >= data.gridRows.Length ||
                tileX < 0 || tileX >= data.widthTiles || string.IsNullOrEmpty(data.gridRows[tileY]) || tileX >= data.gridRows[tileY].Length)
            {
                return '\0';
            }

            return data.gridRows[tileY][tileX];
        }

        private static bool ValidateGrid(RoomTemplateData data, List<string> errors)
        {
            if (data.widthTiles < 1 || data.heightTiles < 1 || data.gridRows == null)
            {
                errors.Add("gridRows are required after valid dimensions are defined");
                return false;
            }

            bool valid = true;
            if (data.gridRows.Length != data.heightTiles)
            {
                errors.Add("gridRows length must equal heightTiles, ordered from bottom to top");
                valid = false;
            }

            for (int rowIndex = 0; rowIndex < data.gridRows.Length; rowIndex++)
            {
                string row = data.gridRows[rowIndex];
                if (row == null || row.Length != data.widthTiles)
                {
                    errors.Add("gridRows[" + rowIndex + "] must contain exactly widthTiles cells");
                    valid = false;
                    continue;
                }

                for (int column = 0; column < row.Length; column++)
                {
                    char cell = row[column];
                    if (cell != EmptyCell[0] && cell != SolidCell[0] && cell != HazardCell[0])
                    {
                        errors.Add("gridRows[" + rowIndex + "] contains unsupported cell '" + cell + "'");
                        valid = false;
                    }
                }
            }

            return valid;
        }

        private static void ValidatePorts(RoomTemplateData data, bool gridValid, List<string> errors)
        {
            if (data.ports == null)
            {
                errors.Add("ports are required");
                return;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            int entryCount = 0;
            int exitCount = 0;
            for (int index = 0; index < data.ports.Length; index++)
            {
                RoomPortData port = data.ports[index];
                if (port == null)
                {
                    errors.Add("ports[" + index + "] is missing");
                    continue;
                }

                if (!IsStableMarkerId(port.id) || !ids.Add(port.id)) errors.Add("port IDs must be unique lowercase identifiers: " + port.id);
                if (port.type == "entry") entryCount++;
                else if (port.type == "exit") exitCount++;
                else if (port.type != "connection") errors.Add("port " + port.id + " has unsupported type " + port.type);

                bool west = port.side == "west";
                bool east = port.side == "east";
                if (!west && !east)
                {
                    errors.Add("port " + port.id + " must use a west or east side");
                    continue;
                }

                if (port.tileX != (west ? 0 : data.widthTiles - 1)) errors.Add("port " + port.id + " must lie on its declared room edge");
                if (port.clearanceTiles < data.minimumPortClearanceTiles) errors.Add("port " + port.id + " has less than the required headroom");
                if (port.tileY < 1 || port.clearanceTiles < 1 || port.tileY + port.clearanceTiles > data.heightTiles)
                {
                    errors.Add("port " + port.id + " has an opening outside the room");
                    continue;
                }

                if (!gridValid) continue;
                int inwardX = west ? 1 : data.widthTiles - 2;
                if (GetCell(data, port.tileX, port.tileY - 1) != SolidCell[0] || GetCell(data, inwardX, port.tileY - 1) != SolidCell[0])
                {
                    errors.Add("port " + port.id + " must land on solid floor at the edge and one tile inward");
                }

                for (int headroom = 0; headroom < Math.Max(port.clearanceTiles, data.minimumPortClearanceTiles); headroom++)
                {
                    int y = port.tileY + headroom;
                    if (y >= data.heightTiles || GetCell(data, port.tileX, y) != EmptyCell[0] || GetCell(data, inwardX, y) != EmptyCell[0])
                    {
                        errors.Add("port " + port.id + " must keep its opening clear for " + data.minimumPortClearanceTiles + " tiles");
                        break;
                    }
                }
            }

            if (entryCount != 1) errors.Add("exactly one entry port is required");
            if (exitCount != 1) errors.Add("exactly one exit port is required");
        }

        private static void ValidateMarkers(RoomTemplateData data, bool gridValid, List<string> errors)
        {
            if (data.markers == null)
            {
                errors.Add("markers are required");
                return;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var playerSpawns = new List<RoomPositionMarkerData>();
            var enemySpawns = new List<RoomPositionMarkerData>();
            for (int index = 0; index < data.markers.Length; index++)
            {
                RoomPositionMarkerData marker = data.markers[index];
                if (marker == null)
                {
                    errors.Add("markers[" + index + "] is missing");
                    continue;
                }

                if (!IsStableMarkerId(marker.id) || !ids.Add(marker.id)) errors.Add("marker IDs must be unique lowercase identifiers: " + marker.id);
                if (!IsSupportedMarkerKind(marker.kind)) errors.Add("marker " + marker.id + " has unsupported kind " + marker.kind);
                if (marker.tileX < 0 || marker.tileX >= data.widthTiles || marker.tileY < 1 || marker.tileY >= data.heightTiles)
                {
                    errors.Add("marker " + marker.id + " lies outside a valid room position");
                    continue;
                }

                if (gridValid)
                {
                    char markerCell = GetCell(data, marker.tileX, marker.tileY);
                    char supportCell = GetCell(data, marker.tileX, marker.tileY - 1);
                    if (marker.kind == "hazard")
                    {
                        if (markerCell != HazardCell[0]) errors.Add("hazard marker " + marker.id + " must align to a hazard grid cell");
                    }
                    else if (markerCell != EmptyCell[0] || supportCell != SolidCell[0])
                    {
                        errors.Add("marker " + marker.id + " must be in open space directly above solid floor");
                    }
                }

                if (marker.kind == "player_spawn") playerSpawns.Add(marker);
                if (marker.kind == "enemy_spawn") enemySpawns.Add(marker);
            }

            if (playerSpawns.Count != 1) errors.Add("exactly one player_spawn marker is required");
            if (playerSpawns.Count == 1)
            {
                foreach (RoomPositionMarkerData enemy in enemySpawns)
                {
                    float dx = enemy.tileX - playerSpawns[0].tileX;
                    float dy = enemy.tileY - playerSpawns[0].tileY;
                    float distanceTiles = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (distanceTiles < data.minimumEnemyDistanceTiles)
                    {
                        errors.Add("enemy marker " + enemy.id + " is closer than minimumEnemyDistanceTiles to player_spawn");
                    }
                }
            }
        }

        private static bool IsStableTemplateId(string value)
        {
            if (string.IsNullOrEmpty(value) || !value.StartsWith("room.", StringComparison.Ordinal)) return false;
            string[] segments = value.Split('.');
            if (segments.Length != 3 || segments[1].Length == 0 || segments[2].Length == 0) return false;
            for (int segmentIndex = 1; segmentIndex < segments.Length; segmentIndex++)
            {
                if (!IsIdentifierSegment(segments[segmentIndex])) return false;
            }

            return true;
        }

        private static bool IsStableMarkerId(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            string[] segments = value.Split('.');
            if (segments.Length == 0) return false;
            foreach (string segment in segments)
            {
                if (!IsIdentifierSegment(segment)) return false;
            }

            return true;
        }

        private static bool IsIdentifierSegment(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            foreach (char character in value)
            {
                if (!(character >= 'a' && character <= 'z') && !(character >= '0' && character <= '9') && character != '_') return false;
            }

            return true;
        }

        private static bool IsSupportedMarkerKind(string kind)
        {
            return kind == "player_spawn" || kind == "enemy_spawn" || kind == "exit_trigger" ||
                kind == "encounter_anchor" || kind == "treasure" || kind == "narrative" ||
                kind == "altar" || kind == "rest" || kind == "hazard" || kind == "shop" ||
                kind == "crafting" || kind == "extraction" || kind == "boss_spawn";
        }
    }

    [Serializable]
    public sealed class RoomTemplateData
    {
        public int schemaVersion;
        public string templateId;
        public int widthTiles;
        public int heightTiles;
        /// <summary>World units per grid cell; room rows are serialized bottom-to-top with row zero at the floor.</summary>
        public float tileSize;
        public int minimumPortClearanceTiles;
        public int minimumEnemyDistanceTiles;
        /// <summary>Cells use '.' for open space, '#' for solid, and '!' for hazards; gridRows[0] is the lowest row.</summary>
        public string[] gridRows;
        public RoomPortData[] ports;
        /// <summary>Coordinates are tile columns and floor-top rows; generated x positions use each cell center.</summary>
        public RoomPositionMarkerData[] markers;
    }

    [Serializable]
    public sealed class RoomPortData
    {
        public string id;
        public string type;
        /// <summary>Horizontal platformer ports use west/east room boundaries.</summary>
        public string side;
        /// <summary>tileX is the boundary cell; tileY is its walking surface and the first clear row.</summary>
        public int tileX;
        public int tileY;
        public int clearanceTiles;
    }

    [Serializable]
    public sealed class RoomPositionMarkerData
    {
        public string id;
        public string kind;
        public int tileX;
        public int tileY;
    }
}
