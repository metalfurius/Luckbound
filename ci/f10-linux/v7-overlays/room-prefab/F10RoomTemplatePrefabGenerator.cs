using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Luckbound.App;
using Luckbound.Content;
using Luckbound.Core.Expeditions;
using Luckbound.Gameplay.Rooms;
using Luckbound.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Luckbound.Editor
{
    /// <summary>
    /// Builds room-only prefabs from the authoritative JSON. It never creates a player, camera, HUD,
    /// RoomLifetimeScope, or floor Tile asset; callers supply an imported 16 PPU Tile.
    /// </summary>
    public static class F10RoomTemplatePrefabGenerator
    {
        public const string RouteDefinitionPath = "Assets/Resources/Luckbound/Data/Rooms/F10SampleRoute.json";
        public const string OutputFolder = "Assets/Resources/Luckbound/Rooms/F10";

        private const string DefinitionFolder = "Assets/Resources/Luckbound/Data/Rooms";
        private const string RoomsResourceOutputRoot = "Assets/Resources/Luckbound/Rooms";
        private const string DiagnosticEditorOutputRoot = "Assets/Luckbound/Editor/Diagnostic/F10";

        private static readonly RoomSpec[] F10Rooms =
        {
            new RoomSpec("F10TutorialRoom.json", "room.sample.tutorial", "room.sample.tutorial.prefab"),
            new RoomSpec("F10PatioCombatC1Room.json", "room.patio.c1", "room.patio.c1.prefab"),
            new RoomSpec("F10PatioExplorationE1Room.json", "room.patio.e1", "room.patio.e1.prefab"),
            new RoomSpec("F10PatioGuardianRoom.json", "room.patio.guardian", "room.patio.guardian.prefab"),
            new RoomSpec("F10SafeExtractionRoom.json", "room.sample.extraction", "room.sample.extraction.prefab")
        };

        [MenuItem("Luckbound/F10/Generate Room Prefabs From Selected Tile")]
        private static void GenerateFromSelectedTile()
        {
            Tile floorTile = Selection.activeObject as Tile;
            if (floorTile == null)
            {
                Debug.LogError("Select a Tile asset with a 16 PPU sprite, then run Luckbound/F10/Generate Room Prefabs From Selected Tile.");
                return;
            }

            try
            {
                GenerateAll(floorTile);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                throw;
            }
        }

        /// <summary>Generates the five room prefabs after validating the fixed F10 route and each definition.</summary>
        public static void GenerateAll(Tile floorTile)
        {
            PreparedRoom[] rooms = PreflightAll(floorTile);
            EnsureAssetFolder(OutputFolder);

            foreach (PreparedRoom room in rooms)
            {
                if (room.ExistingPrefab != null) continue;
                CreateNewPrefab(room.Definition, room.Data, floorTile, room.PrefabPath);
            }
        }

        private static void ValidateRouteRoomDefinition(RoomTemplateData data, RoomSpec spec)
        {
            if (!string.Equals(data.templateId, spec.TemplateId, StringComparison.Ordinal))
                throw new InvalidOperationException("Room source " + spec.DefinitionFileName + " must define " + spec.TemplateId + ".");
            if (data.ports.Length != 2 ||
                data.ports.Count(port => string.Equals(port.id, "port.entry.west", StringComparison.Ordinal)) != 1 ||
                data.ports.Count(port => string.Equals(port.id, "port.exit.east", StringComparison.Ordinal)) != 1)
                throw new InvalidOperationException("Room source " + spec.DefinitionFileName +
                    " must contain exactly port.entry.west and port.exit.east.");

            RoomPortData entry = data.ports.Single(port => string.Equals(port.id, "port.entry.west", StringComparison.Ordinal));
            RoomPortData exit = data.ports.Single(port => string.Equals(port.id, "port.exit.east", StringComparison.Ordinal));
            if (entry.type != "entry" || entry.side != "west" || entry.tileX != 0 ||
                exit.type != "exit" || exit.side != "east" || exit.tileX != data.widthTiles - 1)
                throw new InvalidOperationException("Room source " + spec.DefinitionFileName +
                    " must retain the fixed west-entry/east-exit route ports.");
        }

        /// <summary>
        /// Reusable entry point for one JSON room. Output is restricted to Resources/Luckbound/Rooms so
        /// generated room content cannot overwrite scenes, scripts, or unrelated project assets.
        /// </summary>
        public static GameObject GeneratePrefab(TextAsset sourceDefinition, Tile floorTile, string prefabPath)
        {
            if (sourceDefinition == null) throw new ArgumentNullException(nameof(sourceDefinition));
            ValidateFloorTile(floorTile);
            ValidateImportedTextAsset(sourceDefinition, "room JSON source");
            RoomTemplateData data = RoomTemplateJsonLoader.Load(sourceDefinition.text);
            string normalizedPrefabPath = ValidatePrefabPath(prefabPath);
            GameObject existing = PreflightDestination(normalizedPrefabPath, sourceDefinition, floorTile, data);
            if (existing != null) return existing;

            EnsureAssetFolder(GetAssetDirectory(normalizedPrefabPath));
            return CreateNewPrefab(sourceDefinition, data, floorTile, normalizedPrefabPath);
        }

        private static GameObject CreateNewPrefab(TextAsset sourceDefinition, RoomTemplateData data,
            Tile floorTile, string prefabPath)
        {
            GameObject root = new GameObject("Room." + data.templateId);
            try
            {
                RoomTemplatePrefab2D template = root.AddComponent<RoomTemplatePrefab2D>();
                CreateCameraBounds(root.transform, data);
                Tilemap groundTilemap = CreateGroundTilemap(root.transform, data, floorTile,
                    out TilemapCollider2D groundCollider);
                CreatePortMarkers(root.transform, data);
                CreatePositionMarkers(root.transform, data);
                template.Configure(sourceDefinition,
                    root.transform.Find("CameraBounds").GetComponent<BoxCollider2D>(), groundTilemap, groundCollider);
                ValidateGeneratedPrefabStructure(root, sourceDefinition, floorTile, data, "Room." + data.templateId);
                template.ValidateGeneratedPrefab();

                if (PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool saved) == null || !saved)
                    throw new IOException("Unity did not save room prefab " + prefabPath + ".");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceSynchronousImport);
            GameObject savedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            ValidateSavedPrefab(savedPrefab, sourceDefinition, floorTile, data, prefabPath);
            return savedPrefab;
        }

        private static PreparedRoom[] PreflightAll(Tile floorTile)
        {
            ValidateFloorTile(floorTile);
            TextAsset routeAsset = LoadImportedTextAsset(RouteDefinitionPath, "F10 route JSON");
            ValidateRouteContract(routeAsset);

            var prepared = new PreparedRoom[F10Rooms.Length];
            for (int index = 0; index < F10Rooms.Length; index++)
            {
                RoomSpec spec = F10Rooms[index];
                string definitionPath = DefinitionFolder + "/" + spec.DefinitionFileName;
                TextAsset definition = LoadImportedTextAsset(definitionPath, "F10 room JSON");
                RoomTemplateData data = RoomTemplateJsonLoader.Load(definition.text);
                ValidateRouteRoomDefinition(data, spec);
                string prefabPath = ValidatePrefabPath(OutputFolder + "/" + spec.PrefabFileName);
                GameObject existing = PreflightDestination(prefabPath, definition, floorTile, data);
                prepared[index] = new PreparedRoom(definition, data, prefabPath, existing);
            }
            return prepared;
        }

        private static void ValidateRouteContract(TextAsset routeAsset)
        {
            ExpeditionSampleRouteDefinition route = ExpeditionSampleRouteJsonLoader.Load(routeAsset.text);
            if (route.Nodes.Count != F10Rooms.Length || route.Connections.Count != F10Rooms.Length - 1)
                throw new InvalidOperationException("F10 room generation requires the fixed five-node linear route.");

            for (int index = 0; index < F10Rooms.Length; index++)
            {
                if (!string.Equals(route.Nodes[index].TemplateId, F10Rooms[index].TemplateId, StringComparison.Ordinal) ||
                    !string.Equals(route.Nodes[index].EntryPortId, "port.entry.west", StringComparison.Ordinal) ||
                    !string.Equals(route.Nodes[index].ExitPortId, "port.exit.east", StringComparison.Ordinal))
                    throw new InvalidOperationException("F10SampleRoute.json no longer matches the room generator contract at node " + index + ".");
            }
        }

        private static void ValidateFloorTile(Tile floorTile)
        {
            if (floorTile == null) throw new ArgumentNullException(nameof(floorTile));
            if (!AssetDatabase.Contains(floorTile))
                throw new InvalidOperationException("The supplied floor Tile must be a saved Unity asset.");
            string tilePath = AssetDatabase.GetAssetPath(floorTile);
            if (string.IsNullOrWhiteSpace(tilePath) || !File.Exists(GetProjectPath(tilePath)) ||
                !File.Exists(GetProjectPath(tilePath + ".meta")))
                throw new InvalidOperationException("The supplied floor Tile must be an imported asset with its existing .meta sidecar.");
            if (floorTile.sprite == null || Mathf.Abs(floorTile.sprite.pixelsPerUnit - RoomCameraRig2D.AssetsPixelsPerUnit) > 0.001f)
                throw new InvalidOperationException("The supplied floor Tile must use a sprite imported at exactly 16 PPU.");
            if (floorTile.colliderType != Tile.ColliderType.Grid)
                throw new InvalidOperationException("The supplied floor Tile must use Tile.ColliderType.Grid.");
            if (LayerMask.NameToLayer("Ground") < 0)
                throw new InvalidOperationException("Generated rooms require the configured Ground layer.");
        }

        private static TextAsset LoadImportedTextAsset(string assetPath, string description)
        {
            string fullPath = GetProjectPath(assetPath);
            if (!File.Exists(fullPath) || !File.Exists(fullPath + ".meta"))
                throw new FileNotFoundException(description + " must already be imported with its .meta sidecar: " + assetPath + ".", fullPath);

            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            if (asset == null || !string.Equals(AssetDatabase.GetAssetPath(asset), assetPath, StringComparison.Ordinal))
                throw new InvalidOperationException(description + " is not imported at the expected asset path " + assetPath + ".");
            return asset;
        }

        private static void ValidateImportedTextAsset(TextAsset asset, string description)
        {
            string assetPath = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrWhiteSpace(assetPath))
                throw new InvalidOperationException(description + " must be an imported project asset.");
            string fullPath = GetProjectPath(assetPath);
            if (!File.Exists(fullPath) || !File.Exists(fullPath + ".meta"))
                throw new FileNotFoundException(description + " must retain its imported file and .meta sidecar.", fullPath);
        }

        private static GameObject PreflightDestination(string prefabPath, TextAsset sourceDefinition,
            Tile floorTile, RoomTemplateData data)
        {
            string fullPath = GetProjectPath(prefabPath);
            bool prefabFileExists = File.Exists(fullPath);
            bool sidecarExists = File.Exists(fullPath + ".meta");
            if (!prefabFileExists && !sidecarExists)
            {
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(prefabPath) != null)
                    throw new InvalidOperationException("Destination is registered in the AssetDatabase without matching files; preserving it: " + prefabPath + ".");
                return null;
            }

            if (!prefabFileExists || !sidecarExists)
                throw new InvalidOperationException("Destination has a partial or unimported prefab/sidecar; preserving it: " + prefabPath + ".");

            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing == null || !string.Equals(AssetDatabase.GetAssetPath(existing), prefabPath, StringComparison.Ordinal))
                throw new InvalidOperationException("Destination exists on disk but is not an imported room prefab; preserving it: " + prefabPath + ".");

            ValidateSavedPrefab(existing, sourceDefinition, floorTile, data, prefabPath);
            return existing;
        }

        private static string GetProjectPath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                assetPath.Contains("..") || assetPath.Contains(":") || assetPath.Contains("\\"))
                throw new ArgumentException("Expected a safe Unity Assets/ path.", nameof(assetPath));
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string fullPath = Path.GetFullPath(Path.Combine(projectRoot, assetPath));
            string assetsRoot = Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Asset path escaped the project Assets folder.", nameof(assetPath));
            return fullPath;
        }

        private static string ValidatePrefabPath(string prefabPath)
        {
            if (string.IsNullOrWhiteSpace(prefabPath)) throw new ArgumentException("A prefab asset path is required.", nameof(prefabPath));
            string path = prefabPath.Replace('\\', '/');
            bool underResourceRooms = path.StartsWith(RoomsResourceOutputRoot + "/", StringComparison.Ordinal);
            bool underEditorDiagnostic = path.StartsWith(DiagnosticEditorOutputRoot + "/", StringComparison.Ordinal);
            if ((!underResourceRooms && !underEditorDiagnostic) ||
                !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) || path.Contains("..") ||
                path.Contains(":") || path.Contains("\\"))
                throw new ArgumentException("Room prefabs must be saved under the approved room Resources or Editor Diagnostic folder.", nameof(prefabPath));

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string fullPath = Path.GetFullPath(Path.Combine(projectRoot, path));
            string outputRoot = underEditorDiagnostic ? DiagnosticEditorOutputRoot : RoomsResourceOutputRoot;
            string allowedRoot = Path.GetFullPath(Path.Combine(projectRoot, outputRoot)) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Prefab output escaped its approved room output folder.", nameof(prefabPath));
            return path;
        }

        private static bool UsesExactTile(Tilemap tilemap, RoomTemplateData data, Tile floorTile)
        {
            if (tilemap == null) return false;
            for (int y = 0; y < data.heightTiles; y++)
            {
                for (int x = 0; x < data.widthTiles; x++)
                {
                    bool solid = RoomTemplateJsonLoader.GetCell(data, x, y) == '#';
                    Tile tile = tilemap.GetTile<Tile>(new Vector3Int(x, y, 0));
                    if (solid ? tile != floorTile : tile != null) return false;
                }
            }
            return true;
        }

        private static void ValidateSavedPrefab(GameObject prefab, TextAsset sourceDefinition, Tile floorTile,
            RoomTemplateData data, string prefabPath)
        {
            if (prefab == null) throw new InvalidOperationException("Generated room prefab could not be loaded from its saved path.");
            if (!File.Exists(GetProjectPath(prefabPath)) || !File.Exists(GetProjectPath(prefabPath + ".meta")))
                throw new InvalidOperationException("Generated prefab must have both its asset file and .meta sidecar.");

            GameObject contents = null;
            try
            {
                contents = PrefabUtility.LoadPrefabContents(prefabPath);
                RoomTemplatePrefab2D template = contents.GetComponent<RoomTemplatePrefab2D>();
                if (template == null || template.SourceDefinition != sourceDefinition)
                    throw new InvalidOperationException("Prefab source does not match the exact room JSON asset.");
                if (!string.Equals(RoomTemplateJsonLoader.Load(template.SourceDefinition.text).templateId,
                        data.templateId, StringComparison.Ordinal))
                    throw new InvalidOperationException("Prefab source template ID does not match the expected room definition.");
                ValidateGeneratedPrefabStructure(contents, sourceDefinition, floorTile, data, Path.GetFileNameWithoutExtension(prefabPath));
                template.ValidateGeneratedPrefab();
                if (!UsesExactTile(template.GroundTilemap, data, floorTile))
                    throw new InvalidOperationException("Prefab does not use the supplied floor Tile in every solid cell.");
            }
            finally
            {
                if (contents != null) PrefabUtility.UnloadPrefabContents(contents);
            }

            var entry = new ExpeditionRoomPrefabEntry { templateId = data.templateId, prefab = prefab };
            if (!ExpeditionRoomPrefabRegistry.TryResolve(data.templateId, new[] { entry }, out GameObject resolved, out string error) ||
                resolved != prefab)
                throw new InvalidOperationException("Room prefab failed ExpeditionRoomPrefabRegistry validation: " + error);
        }

        private static void ValidateGeneratedPrefabStructure(GameObject root, TextAsset sourceDefinition,
            Tile floorTile, RoomTemplateData data, string expectedRootName)
        {
            if (root == null)
                throw new InvalidOperationException("Generated room prefab root is null.");
            if (!string.Equals(root.name, expectedRootName, StringComparison.Ordinal))
                throw new InvalidOperationException("Room prefab root name '" + root.name + "' does not match expected '" +
                    expectedRootName + "' for template ID '" + data.templateId + "'.");
            RequireExactComponents(root, typeof(Transform), typeof(RoomTemplatePrefab2D));
            if (root.transform.localPosition.sqrMagnitude > 0.000001f ||
                Quaternion.Angle(root.transform.localRotation, Quaternion.identity) > 0.001f ||
                (root.transform.localScale - Vector3.one).sqrMagnitude > 0.000001f)
                throw new InvalidOperationException("Room prefab root transform must be identity.");

            var expectedChildren = new HashSet<string>(StringComparer.Ordinal) { "CameraBounds", "RoomGrid" };
            foreach (RoomPortData port in data.ports) expectedChildren.Add("Port_" + port.id);
            foreach (RoomPositionMarkerData marker in data.markers) expectedChildren.Add("Marker_" + marker.id);
            for (int index = 0; index < root.transform.childCount; index++)
            {
                Transform child = root.transform.GetChild(index);
                if (!expectedChildren.Remove(child.name))
                    throw new InvalidOperationException("Unexpected or duplicate room prefab child: " + child.name + ".");
            }
            if (expectedChildren.Count != 0)
                throw new InvalidOperationException("Room prefab is missing generated children: " + string.Join(", ", expectedChildren) + ".");

            Transform boundsTransform = root.transform.Find("CameraBounds");
            RequireExactComponents(boundsTransform.gameObject, typeof(Transform), typeof(BoxCollider2D));
            if (!boundsTransform.GetComponent<BoxCollider2D>().enabled || !boundsTransform.GetComponent<BoxCollider2D>().isTrigger)
                throw new InvalidOperationException("CameraBounds must contain only its enabled trigger BoxCollider2D.");

            Transform gridTransform = root.transform.Find("RoomGrid");
            RequireIdentityTransform(gridTransform, "RoomGrid");
            RequireExactComponents(gridTransform.gameObject, typeof(Transform), typeof(Grid));
            if (gridTransform.childCount != 1 || gridTransform.GetChild(0).name != "GroundTilemap")
                throw new InvalidOperationException("RoomGrid must contain exactly one GroundTilemap child.");

            Transform groundTransform = gridTransform.GetChild(0);
            RequireIdentityTransform(groundTransform, "GroundTilemap");
            RequireExactComponents(groundTransform.gameObject, typeof(Transform), typeof(Tilemap),
                typeof(TilemapRenderer), typeof(TilemapCollider2D));
            Tilemap ground = groundTransform.GetComponent<Tilemap>();
            TilemapCollider2D collider = groundTransform.GetComponent<TilemapCollider2D>();
            if (ground == null || collider == null || !collider.enabled || collider.isTrigger ||
                !UsesExactTile(ground, data, floorTile))
                throw new InvalidOperationException("GroundTilemap must contain only the supplied solid tiles and its enabled solid collider.");

            foreach (RoomPortData port in data.ports)
                RequireExactComponents(root.transform.Find("Port_" + port.id).gameObject,
                    typeof(Transform), typeof(RoomPortMarker2D));
            foreach (RoomPositionMarkerData marker in data.markers)
                RequireExactComponents(root.transform.Find("Marker_" + marker.id).gameObject,
                    typeof(Transform), typeof(RoomPositionMarker2D));

            RoomTemplatePrefab2D template = root.GetComponent<RoomTemplatePrefab2D>();
            if (template == null || template.SourceDefinition != sourceDefinition)
                throw new InvalidOperationException("RoomTemplatePrefab2D must reference the exact source JSON asset.");
        }

        private static void RequireExactComponents(GameObject target, params Type[] expectedTypes)
        {
            if (target == null) throw new InvalidOperationException("Generated room structure is missing an expected object.");
            Component[] components = target.GetComponents<Component>();
            if (components.Length != expectedTypes.Length || expectedTypes.Any(type => target.GetComponent(type) == null))
                throw new InvalidOperationException("Generated room object " + target.name + " has unexpected or missing components.");
        }

        private static void RequireIdentityTransform(Transform transform, string objectName)
        {
            if (transform == null || transform.localPosition.sqrMagnitude > 0.000001f ||
                Quaternion.Angle(transform.localRotation, Quaternion.identity) > 0.001f ||
                (transform.localScale - Vector3.one).sqrMagnitude > 0.000001f)
                throw new InvalidOperationException(objectName + " must have an identity local transform.");
        }

        private static void CreateCameraBounds(Transform parent, RoomTemplateData data)
        {
            var boundsObject = new GameObject("CameraBounds");
            boundsObject.transform.SetParent(parent, false);
            boundsObject.transform.localPosition = new Vector3(data.widthTiles * data.tileSize * 0.5f,
                data.heightTiles * data.tileSize * 0.5f, 0f);
            BoxCollider2D bounds = boundsObject.AddComponent<BoxCollider2D>();
            bounds.size = new Vector2(data.widthTiles * data.tileSize, data.heightTiles * data.tileSize);
            bounds.isTrigger = true;
        }

        private static Tilemap CreateGroundTilemap(Transform parent, RoomTemplateData data, Tile floorTile,
            out TilemapCollider2D groundCollider)
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
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
                    if (RoomTemplateJsonLoader.GetCell(data, tileX, tileY) == '#')
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
                    port.tileY * data.tileSize, 0f);
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
                markerObject.transform.localPosition = new Vector3(
                    (marker.tileX + 0.5f) * data.tileSize, marker.tileY * data.tileSize, 0f);
                markerObject.AddComponent<RoomPositionMarker2D>().Configure(
                    marker.id, marker.kind, marker.tileX, marker.tileY);
            }
        }

        private static void EnsureAssetFolder(string folder)
        {
            string normalized = folder.Replace('\\', '/').TrimEnd('/');
            string[] segments = normalized.Split('/');
            if (segments.Length == 0 || segments[0] != "Assets")
                throw new ArgumentException("Asset folders must be rooted under Assets/.", nameof(folder));

            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    string guid = AssetDatabase.CreateFolder(current, segments[index]);
                    if (string.IsNullOrEmpty(guid) || !AssetDatabase.IsValidFolder(next))
                        throw new IOException("Unity could not create asset folder " + next + ".");
                }
                current = next;
            }
        }

        private static string GetAssetDirectory(string assetPath)
        {
            int separator = assetPath.LastIndexOf('/');
            if (separator <= 0) throw new ArgumentException("Asset path must include a folder.", nameof(assetPath));
            return assetPath.Substring(0, separator);
        }

        private sealed class RoomSpec
        {
            internal RoomSpec(string definitionFileName, string templateId, string prefabFileName)
            {
                DefinitionFileName = definitionFileName;
                TemplateId = templateId;
                PrefabFileName = prefabFileName;
            }

            internal string DefinitionFileName { get; }
            internal string TemplateId { get; }
            internal string PrefabFileName { get; }
        }

        private sealed class PreparedRoom
        {
            internal PreparedRoom(TextAsset definition, RoomTemplateData data, string prefabPath,
                GameObject existingPrefab)
            {
                Definition = definition;
                Data = data;
                PrefabPath = prefabPath;
                ExistingPrefab = existingPrefab;
            }

            internal TextAsset Definition { get; }
            internal RoomTemplateData Data { get; }
            internal string PrefabPath { get; }
            internal GameObject ExistingPrefab { get; }
        }
    }
}
