using System;
using System.IO;
using Luckbound.Content;
using Luckbound.Gameplay.Rooms;
using Luckbound.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Luckbound.Editor
{
    /// <summary>Creates the D01 room prefab from its authoritative JSON definition.</summary>
    public static class RoomTemplateScaffoldGenerator
    {
        public const string DefinitionPath = "Assets/Resources/Luckbound/Data/Rooms/D01PrototypeRoom.json";
        public const string PrefabPath = "Assets/Resources/Luckbound/Rooms/PrototypeRoom.prefab";
        public const string FloorTileAssetPath = "Assets/Resources/Luckbound/Rooms/PrototypeFloorTile.asset";
        private const string GroundSpritePath = "Assets/Luckbound/Art/Prototypes/MovementDemo/StonePrototype.png";

        [MenuItem("Luckbound/Generate D01 Room Template")]
        public static void Generate()
        {
            EnsureGenerated(true);
        }

        public static void EnsureGenerated()
        {
            EnsureGenerated(false);
        }

        private static void EnsureGenerated(bool force)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            Directory.CreateDirectory(Path.Combine(projectRoot, "Assets/Resources/Luckbound/Rooms"));
            Directory.CreateDirectory(Path.Combine(projectRoot, "Assets/Resources/Luckbound/Data/Rooms"));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            TextAsset source = AssetDatabase.LoadAssetAtPath<TextAsset>(DefinitionPath);
            if (source == null) throw new InvalidOperationException("Missing room template source JSON at " + DefinitionPath + ".");
            RoomTemplateData data = RoomTemplateJsonLoader.Load(source.text);
            Sprite groundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(GroundSpritePath);
            if (groundSprite == null || !Mathf.Approximately(groundSprite.pixelsPerUnit, RoomCameraRig2D.AssetsPixelsPerUnit))
            {
                throw new InvalidOperationException("D01 requires the existing 16 PPU StonePrototype sprite at " + GroundSpritePath + ".");
            }
            Tile floorTile = EnsureFloorTileAsset(groundSprite);

            if (!force && ExistingPrefabMatchesSource()) return;

            GameObject root = new GameObject("PrototypeRoom");
            try
            {
                root.AddComponent<RoomTemplatePrefab2D>();
                CreateCameraBounds(root.transform, data);
                Tilemap groundTilemap = CreateGroundTilemap(root.transform, data, floorTile, out TilemapCollider2D groundCollider);
                CreatePortMarkers(root.transform, data);
                CreatePositionMarkers(root.transform, data);
                RoomTemplatePrefab2D template = root.GetComponent<RoomTemplatePrefab2D>();
                template.Configure(source, root.transform.Find("CameraBounds").GetComponent<BoxCollider2D>(), groundTilemap, groundCollider);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceSynchronousImport);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            if (!ExistingPrefabMatchesSource()) throw new InvalidOperationException("Generated D01 room prefab failed its own source/geometry validation.");
        }

        private static bool ExistingPrefabMatchesSource()
        {
            if (!File.Exists(PrefabPath)) return false;

            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(PrefabPath);
                RoomTemplatePrefab2D template = root.GetComponent<RoomTemplatePrefab2D>();
                if (template == null) return false;
                template.ValidateGeneratedPrefab();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                if (root != null) PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void CreateCameraBounds(Transform parent, RoomTemplateData data)
        {
            var boundsObject = new GameObject("CameraBounds");
            boundsObject.transform.SetParent(parent, false);
            boundsObject.transform.localPosition = new Vector3(data.widthTiles * data.tileSize * 0.5f, data.heightTiles * data.tileSize * 0.5f, 0f);
            BoxCollider2D bounds = boundsObject.AddComponent<BoxCollider2D>();
            bounds.size = new Vector2(data.widthTiles * data.tileSize, data.heightTiles * data.tileSize);
            bounds.isTrigger = true;
        }

        private static Tile EnsureFloorTileAsset(Sprite groundSprite)
        {
            Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(FloorTileAssetPath);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                tile.name = "PrototypeFloorTile";
                tile.sprite = groundSprite;
                tile.color = new Color32(157, 161, 176, 255);
                tile.colliderType = Tile.ColliderType.Grid;
                AssetDatabase.CreateAsset(tile, FloorTileAssetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                tile = AssetDatabase.LoadAssetAtPath<Tile>(FloorTileAssetPath);
            }
            else if (tile.sprite != groundSprite || tile.colliderType != Tile.ColliderType.Grid || tile.color != new Color32(157, 161, 176, 255))
            {
                tile.sprite = groundSprite;
                tile.color = new Color32(157, 161, 176, 255);
                tile.colliderType = Tile.ColliderType.Grid;
                EditorUtility.SetDirty(tile);
                AssetDatabase.SaveAssets();
            }

            return tile;
        }

        private static Tilemap CreateGroundTilemap(Transform parent, RoomTemplateData data, Tile floorTile, out TilemapCollider2D groundCollider)
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer < 0) throw new InvalidOperationException("The D01 room prefab requires the Ground layer.");

            var gridObject = new GameObject("RoomGrid");
            gridObject.transform.SetParent(parent, false);
            Grid grid = gridObject.AddComponent<Grid>();
            grid.cellLayout = GridLayout.CellLayout.Rectangle;
            grid.cellSize = new Vector3(data.tileSize, data.tileSize, 1f);

            var groundObject = new GameObject("GroundTilemap");
            groundObject.layer = groundLayer;
            groundObject.transform.SetParent(gridObject.transform, false);
            Tilemap tilemap = groundObject.AddComponent<Tilemap>();
            tilemap.tileAnchor = new Vector3(0.5f, 0.5f, 0f);
            TilemapRenderer renderer = groundObject.AddComponent<TilemapRenderer>();
            renderer.sortOrder = TilemapRenderer.SortOrder.TopRight;
            groundCollider = groundObject.AddComponent<TilemapCollider2D>();

            for (int tileY = 0; tileY < data.heightTiles; tileY++)
            {
                for (int tileX = 0; tileX < data.widthTiles; tileX++)
                {
                    if (RoomTemplateJsonLoader.GetCell(data, tileX, tileY) != '#') continue;
                    tilemap.SetTile(new Vector3Int(tileX, tileY, 0), floorTile);
                }
            }

            tilemap.CompressBounds();
            return tilemap;
        }

        private static void CreatePortMarkers(Transform parent, RoomTemplateData data)
        {
            foreach (RoomPortData port in data.ports)
            {
                var markerObject = new GameObject("Port_" + port.id);
                markerObject.transform.SetParent(parent, false);
                markerObject.transform.localPosition = new Vector3(
                    (port.side == "west" ? 0f : data.widthTiles) * data.tileSize,
                    port.tileY * data.tileSize,
                    0f);
                markerObject.AddComponent<RoomPortMarker2D>().Configure(
                    port.id, port.type, port.side, port.tileX, port.tileY, port.clearanceTiles);
            }
        }

        private static void CreatePositionMarkers(Transform parent, RoomTemplateData data)
        {
            foreach (RoomPositionMarkerData marker in data.markers)
            {
                var markerObject = new GameObject("Marker_" + marker.id);
                markerObject.transform.SetParent(parent, false);
                markerObject.transform.localPosition = new Vector3((marker.tileX + 0.5f) * data.tileSize, marker.tileY * data.tileSize, 0f);
                markerObject.AddComponent<RoomPositionMarker2D>().Configure(marker.id, marker.kind, marker.tileX, marker.tileY);
            }
        }
    }
}
