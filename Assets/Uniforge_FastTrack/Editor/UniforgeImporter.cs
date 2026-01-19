using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Uniforge.FastTrack.Editor.Importers;

namespace Uniforge.FastTrack.Editor
{
    /// <summary>
    /// Main orchestrator for importing Uniforge scene data into Unity.
    /// Coordinates the import process by delegating to specialized processors.
    /// </summary>
    public static class UniforgeImporter
    {
        private static Dictionary<string, GameObject> PendingScriptAttachments = new Dictionary<string, GameObject>();
        private const string PendingAttachmentsKey = "Uniforge_PendingAttachments";

        #region Public API

        [MenuItem("Uniforge/Import Scene JSON (Fast Track)")]
        public static void ImportScene()
        {
            ImportFromJson(null);
        }

        /// <summary>
        /// Imports a scene from JSON string or opens a file dialog.
        /// </summary>
        public static async void ImportFromJson(string jsonOverride = null)
        {
            string json = jsonOverride;
            if (string.IsNullOrEmpty(json))
            {
                string path = EditorUtility.OpenFilePanel("Select Uniforge Scene JSON", "", "json");
                if (string.IsNullOrEmpty(path)) return;
                json = File.ReadAllText(path);
            }

            SaveJsonToStreamingAssets(json);

            try
            {
                var data = ParseGameData(json);
                if (data == null)
                {
                    Debug.LogError("[UniforgeImporter] JSON Parsing Failed.");
                    return;
                }

                // Clean previous generated assets before importing new data
                CleanGeneratedAssets();
                
                ConfigureInputSystem();
                await ProcessGameData(data);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UniforgeImporter] Import Failed: {ex.Message}\n{ex.StackTrace}");
            }
        }

        #endregion

        #region JSON Parsing

        /// <summary>
        /// Parses JSON into GameDataJSON, handling both Frontend and standard formats.
        /// </summary>
        private static GameDataJSON ParseGameData(string json)
        {
            JObject rawJson = JObject.Parse(json);

            // Check if this is Frontend single-scene format
            if (rawJson["entities"] != null && rawJson["scenes"] == null)
            {
                Debug.Log("<color=cyan>[UniforgeImporter]</color> Detected Frontend single-scene format, converting...");
                return ConvertFrontendFormat(rawJson);
            }

            return JsonConvert.DeserializeObject<GameDataJSON>(json);
        }

        /// <summary>
        /// Converts Frontend single-scene format to GameDataJSON.
        /// </summary>
        private static GameDataJSON ConvertFrontendFormat(JObject rawJson)
        {
            var sceneData = new SceneJSON
            {
                id = rawJson["sceneId"]?.ToString() ?? "scene_0",
                sceneId = rawJson["sceneId"]?.ToString(),
                name = rawJson["name"]?.ToString() ?? "Scene",
                entities = rawJson["entities"]?.ToObject<List<EntityJSON>>(),
                tiles = rawJson["tiles"]?.ToObject<List<TileJSON>>(),
                assets = rawJson["assets"]?.ToObject<List<AssetDetailJSON>>()
            };

            var data = new GameDataJSON
            {
                scenes = new List<SceneJSON> { sceneData },
                assets = rawJson["assets"]?.ToObject<List<AssetDetailJSON>>()
            };

            Debug.Log($"<color=cyan>[UniforgeImporter]</color> Converted: {sceneData.entities?.Count ?? 0} entities, {data.assets?.Count ?? 0} assets");
            return data;
        }

        #endregion

        #region Main Processing Pipeline

        /// <summary>
        /// Main processing pipeline - orchestrates all import stages.
        /// </summary>
        private static async Task ProcessGameData(GameDataJSON data)
        {
            Debug.Log($"<color=cyan>[UniforgeImporter]</color> === Import Started ===");
            Debug.Log($"<color=cyan>[UniforgeImporter]</color> Scenes: {data.scenes?.Count ?? 0}, Assets: {data.assets?.Count ?? 0}");

            // Create root object
            GameObject root = new GameObject($"Uniforge_Scene_{DateTime.Now:HHmmss}");

            // Ensure runtime exists
            EnsureRuntimeExists();

            // Build asset maps
            var assetMap = AssetDownloader.BuildAssetUrlMap(data.assets);

            // Pre-download textures
            var textureCache = await AssetDownloader.PredownloadAssets(
                data.assets,
                (msg, progress) => EditorUtility.DisplayProgressBar("Uniforge Import", msg, progress)
            );

            // Process scene
            if (data.scenes == null || data.scenes.Count == 0)
            {
                EditorUtility.ClearProgressBar();
                return;
            }

            var scene = data.scenes[0];
            PendingScriptAttachments.Clear();

            // Stage 1: Process Entities
            if (scene.entities != null)
            {
                var entityResults = await EntityProcessor.ProcessEntities(
                    scene.entities,
                    data.assets,
                    assetMap,
                    textureCache,
                    root.transform,
                    (msg, progress) => EditorUtility.DisplayProgressBar("Uniforge Import", msg, progress)
                );

                foreach (var result in entityResults)
                {
                    if (result.Success)
                    {
                        PendingScriptAttachments[result.EntityId] = result.GameObject;
                    }
                }

                // Setup camera follow on first entity
                if (entityResults.Count > 0 && entityResults[0].Success && Camera.main != null)
                {
                    var pos = entityResults[0].GameObject.transform.position;
                    Camera.main.transform.position = new Vector3(pos.x, pos.y, -10);
                }
            }

            // Stage 2: Process Tilemap
            if (scene.tiles != null && scene.tiles.Count > 0)
            {
                TilemapProcessor.ProcessTilemap(scene.tiles, data.assets, textureCache, root.transform);
            }

            // Stage 3: Process Prefabs
            if (data.assets != null)
            {
                var prefabResults = await PrefabProcessor.ProcessPrefabs(
                    data.assets,
                    assetMap,
                    textureCache,
                    root.transform
                );

                foreach (var result in prefabResults)
                {
                    if (result.Success)
                    {
                        PendingScriptAttachments[result.AssetId] = result.Instance;
                    }
                }

                PrefabProcessor.RegisterPrefabsToRegistry(prefabResults);
            }

            EditorUtility.ClearProgressBar();

            // Save pending attachments for post-compilation
            if (PendingScriptAttachments.Count > 0)
            {
                SavePendingAttachments(PendingScriptAttachments.Keys.ToList());
            }

            Debug.Log("<color=green>[UniforgeImporter]</color> Import complete! Triggering asset refresh...");
            AssetDatabase.Refresh();
        }

        #endregion

        #region Runtime Setup

        /// <summary>
        /// Ensures the UniforgeRuntime object exists in the scene.
        /// </summary>
        private static void EnsureRuntimeExists()
        {
            var existingRuntime = GameObject.Find("[UniforgeRuntime]");
            if (existingRuntime == null)
            {
                var runtimeGo = new GameObject("[UniforgeRuntime]");
                runtimeGo.AddComponent<Uniforge.FastTrack.Runtime.UniforgeRuntime>();
                runtimeGo.AddComponent<Uniforge.FastTrack.Runtime.AudioManager>();
                runtimeGo.AddComponent<Uniforge.FastTrack.Runtime.ParticleManager>();
                runtimeGo.AddComponent<Uniforge.FastTrack.Runtime.PrefabRegistry>();
                runtimeGo.AddComponent<Uniforge.FastTrack.Runtime.ProjectileManager>();
                Debug.Log("<color=green>[UniforgeImporter]</color> Created UniforgeRuntime");
            }
        }

        #endregion

        #region Script Attachment

        /// <summary>
        /// Called after script compilation to attach generated scripts.
        /// </summary>
        [UnityEditor.Callbacks.DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            EditorApplication.delayCall += AttachScriptsToAllEntities;
        }

        /// <summary>
        /// Attaches generated scripts to all UniforgeEntity objects.
        /// </summary>
        private static void AttachScriptsToAllEntities()
        {
            var allEntities = GameObject.FindObjectsByType<Uniforge.FastTrack.Runtime.UniforgeEntity>(FindObjectsSortMode.None);
            if (allEntities.Length == 0) return;

            Debug.Log($"<color=cyan>[UniforgeImporter]</color> Checking {allEntities.Length} entities for script attachment...");

            int attachedCount = 0;
            foreach (var entity in allEntities)
            {
                if (string.IsNullOrEmpty(entity.EntityId)) continue;

                string className = $"Gen_{entity.EntityId.Replace("-", "_")}";

                try
                {
                    var assembly = System.Reflection.Assembly.Load("Assembly-CSharp");
                    if (assembly == null) continue;

                    var type = assembly.GetType(className);
                    if (type == null) continue;

                    if (entity.gameObject.GetComponent(type) != null) continue;

                    entity.gameObject.AddComponent(type);
                    attachedCount++;
                    Debug.Log($"<color=green>[UniforgeImporter]</color> Attached {className} to {entity.gameObject.name}");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[UniforgeImporter] Failed to attach {className}: {ex.Message}");
                }
            }

            if (attachedCount > 0)
            {
                Debug.Log($"<color=green>[UniforgeImporter]</color> Total {attachedCount} scripts attached!");
            }
        }

        /// <summary>
        /// Saves pending script attachments to EditorPrefs.
        /// </summary>
        private static void SavePendingAttachments(List<string> entityIds)
        {
            if (entityIds == null || entityIds.Count == 0) return;
            string json = JsonConvert.SerializeObject(entityIds);
            EditorPrefs.SetString(PendingAttachmentsKey, json);
            Debug.Log($"<color=cyan>[UniforgeImporter]</color> {entityIds.Count} entities pending for script attachment");
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Saves the imported JSON to StreamingAssets for runtime access.
        /// </summary>
        private static void SaveJsonToStreamingAssets(string json)
        {
            try
            {
                string streamingAssetsPath = Application.streamingAssetsPath;
                string uniforgeDataPath = Path.Combine(streamingAssetsPath, "UniforgeData");

                if (!Directory.Exists(streamingAssetsPath))
                    Directory.CreateDirectory(streamingAssetsPath);
                if (!Directory.Exists(uniforgeDataPath))
                    Directory.CreateDirectory(uniforgeDataPath);

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"scene_{timestamp}.json";
                string filePath = Path.Combine(uniforgeDataPath, fileName);

                File.WriteAllText(filePath, json);

                string latestPath = Path.Combine(uniforgeDataPath, "latest.json");
                File.WriteAllText(latestPath, json);

                Debug.Log($"<color=green>[UniforgeImporter]</color> JSON saved to: {filePath}");
                AssetDatabase.Refresh();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UniforgeImporter] Failed to save JSON: {ex.Message}");
            }
        }

        /// <summary>
        /// Configures the Input System to Both mode.
        /// </summary>
        private static void ConfigureInputSystem()
        {
#if UNITY_EDITOR
            try
            {
                var playerSettings = typeof(UnityEditor.PlayerSettings);
                var prop = playerSettings.GetProperty("activeInputHandler", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                if (prop != null)
                {
                    int val = (int)prop.GetValue(null);
                    if (val != 2) prop.SetValue(null, 2);
                }
            }
            catch { }
#endif
        }

        #endregion

        #region Clean Generated Assets

        /// <summary>
        /// Cleans all previously generated assets before a new import.
        /// Removes generated scripts, animations, tiles, and optionally textures.
        /// </summary>
        private static void CleanGeneratedAssets()
        {
            Debug.Log("<color=yellow>[UniforgeImporter]</color> Cleaning previous generated assets...");

            string generatedPath = "Assets/Uniforge_FastTrack/Generated";
            string texturesPath = "Assets/Uniforge_FastTrack/Textures";

            int deletedCount = 0;

            // Clean Generated folder (scripts, animations, tiles)
            if (Directory.Exists(generatedPath))
            {
                // Delete all .cs files in Generated root
                foreach (var file in Directory.GetFiles(generatedPath, "*.cs"))
                {
                    AssetDatabase.DeleteAsset(file.Replace("\\", "/"));
                    deletedCount++;
                }

                // Clean Animations subfolder
                string animPath = Path.Combine(generatedPath, "Animations");
                if (Directory.Exists(animPath))
                {
                    foreach (var file in Directory.GetFiles(animPath, "*.*"))
                    {
                        if (!file.EndsWith(".meta"))
                        {
                            AssetDatabase.DeleteAsset(file.Replace("\\", "/"));
                            deletedCount++;
                        }
                    }
                }

                // Clean Tiles subfolder
                string tilesPath = Path.Combine(generatedPath, "Tiles");
                if (Directory.Exists(tilesPath))
                {
                    foreach (var file in Directory.GetFiles(tilesPath, "*.*"))
                    {
                        if (!file.EndsWith(".meta"))
                        {
                            AssetDatabase.DeleteAsset(file.Replace("\\", "/"));
                            deletedCount++;
                        }
                    }
                }
            }

            // Clean Textures folder (downloaded images)
            if (Directory.Exists(texturesPath))
            {
                foreach (var file in Directory.GetFiles(texturesPath, "*.*"))
                {
                    if (!file.EndsWith(".meta"))
                    {
                        AssetDatabase.DeleteAsset(file.Replace("\\", "/"));
                        deletedCount++;
                    }
                }
            }

            // Destroy scene objects from previous import
            var oldScenes = GameObject.FindObjectsOfType<Transform>()
                .Where(t => t.parent == null && t.name.StartsWith("Uniforge_Scene_"))
                .ToArray();
            foreach (var oldScene in oldScenes)
            {
                GameObject.DestroyImmediate(oldScene.gameObject);
                deletedCount++;
            }

            AssetDatabase.Refresh();
            Debug.Log($"<color=yellow>[UniforgeImporter]</color> Cleaned {deletedCount} assets/objects");
        }

        /// <summary>
        /// Menu item to manually clean all generated assets.
        /// </summary>
        [MenuItem("Uniforge/Clean Generated Assets")]
        public static void MenuCleanGeneratedAssets()
        {
            if (EditorUtility.DisplayDialog(
                "Clean Generated Assets",
                "This will delete all previously generated scripts, animations, tiles, and textures. Continue?",
                "Yes, Clean All",
                "Cancel"))
            {
                CleanGeneratedAssets();
                Debug.Log("<color=green>[UniforgeImporter]</color> Clean complete!");
            }
        }

        #endregion
    }
}
