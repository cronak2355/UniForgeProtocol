using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Uniforge.FastTrack.Runtime;

namespace Uniforge.FastTrack.Editor.Importers
{
    /// <summary>
    /// Processes prefab assets and creates Unity prefabs.
    /// </summary>
    public static class PrefabProcessor
    {
        private static string PrefabsPath = "Assets/Uniforge_FastTrack/Prefabs";

        /// <summary>
        /// Result of processing a prefab.
        /// </summary>
        public class PrefabResult
        {
            public string AssetId;
            public string AssetName;
            public GameObject Prefab;
            public GameObject Instance;
            public bool Success;
        }

        /// <summary>
        /// Processes all prefab assets and creates Unity prefabs.
        /// </summary>
        public static async Task<List<PrefabResult>> ProcessPrefabs(
            List<AssetDetailJSON> assets,
            Dictionary<string, string> assetMap,
            Dictionary<string, Sprite> textureCache,
            Transform parent)
        {
            var results = new List<PrefabResult>();

            if (assets == null) return results;

            Debug.Log($"<color=cyan>[PrefabProcessor]</color> Processing {assets.Count} assets for prefabs...");

            // Create prefab root (hidden from view)
            GameObject prefabRoot = new GameObject("Uniforge_Prefabs");
            prefabRoot.transform.SetParent(parent);
            prefabRoot.transform.position = new Vector3(-9999, -9999, 0);

            // Ensure prefabs directory exists
            if (!Directory.Exists(PrefabsPath))
            {
                Directory.CreateDirectory(PrefabsPath);
            }

            foreach (var asset in assets)
            {
                if (asset.tag != "Prefab" || asset.metadata == null)
                    continue;

                var result = await ProcessSinglePrefab(asset, assetMap, textureCache, assets, prefabRoot.transform);
                if (result.Success)
                {
                    results.Add(result);
                }
            }

            return results;
        }

        /// <summary>
        /// Processes a single prefab asset.
        /// </summary>
        private static async Task<PrefabResult> ProcessSinglePrefab(
            AssetDetailJSON asset,
            Dictionary<string, string> assetMap,
            Dictionary<string, Sprite> textureCache,
            List<AssetDetailJSON> allAssets,
            Transform parent)
        {
            var result = new PrefabResult
            {
                AssetId = asset.id,
                AssetName = asset.name,
                Success = false
            };

            try
            {
                Debug.Log($"<color=yellow>[PrefabProcessor]</color> Processing Prefab: {asset.name} (ID: {asset.id})");

                // Parse prefab data from metadata
                JObject metadataJson = null;
                if (asset.metadata is JObject jo)
                    metadataJson = jo;
                else if (asset.metadata != null)
                    metadataJson = JObject.FromObject(asset.metadata);

                if (metadataJson == null) return result;

                var prefabJson = metadataJson["prefab"] as JObject;
                if (prefabJson == null) return result;

                var prefabEntity = prefabJson.ToObject<EntityJSON>();
                if (prefabEntity == null) return result;

                // Override ID with asset ID for consistent referencing
                prefabEntity.id = asset.id;

                // Create GameObject
                GameObject prefabGo = new GameObject(asset.name ?? prefabEntity.name ?? "Prefab");
                prefabGo.transform.SetParent(parent);
                prefabGo.transform.localPosition = Vector3.zero;
                result.Instance = prefabGo;

                // Setup sprite
                await SetupPrefabSprite(prefabGo, prefabEntity, assetMap, textureCache);

                // Generate Animations
                var animController = Uniforge.FastTrack.Editor.AnimationGenerator.GenerateForEntity(prefabEntity, allAssets);
                if (animController != null)
                {
                    var animator = prefabGo.GetComponent<Animator>();
                    if (animator == null) animator = prefabGo.AddComponent<Animator>();
                    animator.runtimeAnimatorController = animController;
                }

                // Add UniforgeEntity component
                var ufe = prefabGo.AddComponent<Uniforge.FastTrack.Runtime.UniforgeEntity>();
                ufe.EntityId = asset.id;
                ufe.Role = prefabEntity.role ?? "neutral";
                if (prefabEntity.tags != null) ufe.Tags = new List<string>(prefabEntity.tags);

                // Generate script
                Uniforge.FastTrack.Editor.UniforgeScriptGenerator.Generate(prefabEntity);

                // Save as Unity Prefab
                string safeName = SanitizeFileName(asset.name ?? prefabEntity.name ?? asset.id);
                string prefabPath = $"{PrefabsPath}/{safeName}.prefab";

                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(prefabGo, prefabPath);
                if (savedPrefab != null)
                {
                    result.Prefab = savedPrefab;
                    Debug.Log($"<color=green>[PrefabProcessor]</color> Prefab saved: {prefabPath}");
                }
                else
                {
                    Debug.LogWarning($"[PrefabProcessor] Failed to save prefab: {prefabPath}");
                }

                result.Success = true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PrefabProcessor] Failed to process prefab {asset.id}: {e.Message}");
            }

            return result;
        }

        /// <summary>
        /// Sets up sprite for a prefab.
        /// </summary>
        private static async Task SetupPrefabSprite(
            GameObject prefabGo,
            EntityJSON prefabEntity,
            Dictionary<string, string> assetMap,
            Dictionary<string, Sprite> textureCache)
        {
            if (string.IsNullOrEmpty(prefabEntity.texture)) return;

            string texUrl = assetMap.ContainsKey(prefabEntity.texture) ? assetMap[prefabEntity.texture] : prefabEntity.texture;
            Sprite sprite = null;

            if (textureCache.ContainsKey(texUrl))
            {
                sprite = textureCache[texUrl];
            }
            else if (texUrl.StartsWith("http") || texUrl.StartsWith("data:"))
            {
                sprite = await AssetDownloader.DownloadTexture(texUrl);
                if (sprite != null) textureCache[texUrl] = sprite;
            }

            if (sprite != null)
            {
                var sr = prefabGo.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                var collider = prefabGo.AddComponent<BoxCollider2D>();
                collider.isTrigger = true;
                collider.size = sprite.bounds.size;
            }
        }

        /// <summary>
        /// Registers prefabs to the PrefabRegistry.
        /// </summary>
        public static void RegisterPrefabsToRegistry(List<PrefabResult> prefabResults)
        {
            if (prefabResults == null || prefabResults.Count == 0) return;

            var registry = UnityEngine.Object.FindFirstObjectByType<Uniforge.FastTrack.Runtime.PrefabRegistry>();
            if (registry == null)
            {
                var runtimeGo = GameObject.Find("[UniforgeRuntime]");
                if (runtimeGo != null)
                {
                    registry = runtimeGo.GetComponent<Uniforge.FastTrack.Runtime.PrefabRegistry>();
                }
            }

            if (registry != null)
            {
                registry.Prefabs.Clear();

                foreach (var result in prefabResults)
                {
                    if (!result.Success || result.Prefab == null) continue;

                    // Register by ID
                    registry.Prefabs.Add(new Uniforge.FastTrack.Runtime.PrefabEntry
                    {
                        Id = result.AssetId,
                        Prefab = result.Prefab
                    });

                    // Also register by name if different
                    if (!string.IsNullOrEmpty(result.AssetName) && result.AssetName != result.AssetId)
                    {
                        registry.Prefabs.Add(new Uniforge.FastTrack.Runtime.PrefabEntry
                        {
                            Id = result.AssetName,
                            Prefab = result.Prefab
                        });
                    }

                    Debug.Log($"<color=green>[PrefabProcessor]</color> Registered: {result.AssetId}");
                }

                EditorUtility.SetDirty(registry);
            }
            else
            {
                Debug.LogWarning("[PrefabProcessor] PrefabRegistry not found.");
            }
        }

        /// <summary>
        /// Sanitizes a string for use as a filename.
        /// </summary>
        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Unnamed";

            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                name = name.Replace(c, '_');
            }

            name = name.Replace(' ', '_').Replace('-', '_');
            return name;
        }

        /// <summary>
        /// Clears all generated prefabs.
        /// </summary>
        public static void ClearGeneratedPrefabs()
        {
            if (!Directory.Exists(PrefabsPath))
            {
                Debug.Log("[PrefabProcessor] No prefabs folder found.");
                return;
            }

            var files = Directory.GetFiles(PrefabsPath, "*.prefab");
            int count = 0;

            foreach (var file in files)
            {
                AssetDatabase.DeleteAsset(file.Replace("\\", "/"));
                count++;
            }

            AssetDatabase.Refresh();
            Debug.Log($"[PrefabProcessor] Cleared {count} prefab assets.");
        }
    }
}
