using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Uniforge.FastTrack.Editor
{
    public static class UniforgeImporter
    {
        private static Dictionary<string, GameObject> PendingScriptAttachments = new Dictionary<string, GameObject>();

        [MenuItem("Uniforge/Import Scene JSON (Fast Track)")]
        public static void ImportScene()
        {
            ImportFromJson(null);
        }

        public static async void ImportFromJson(string jsonOverride = null)
        {
            string json = jsonOverride;
            if (string.IsNullOrEmpty(json))
            {
                string path = EditorUtility.OpenFilePanel("Select Uniforge Scene JSON", "", "json");
                if (string.IsNullOrEmpty(path)) return;
                json = File.ReadAllText(path);
            }

            try
            {
                GameDataJSON data = JsonConvert.DeserializeObject<GameDataJSON>(json);
                if (data == null)
                {
                    Debug.LogError("[UniforgeImporter] JSON Parsing Failed.");
                    return;
                }
                
                ConfigureInputSystem();
                await ProcessGameData(data);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UniforgeImporter] Import Failed: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static async Task ProcessGameData(GameDataJSON data)
        {
            GameObject root = new GameObject($"Uniforge_Scene_{DateTime.Now:HHmmss}");
            
            // Map Asset IDs/Names to URLs
            Dictionary<string, string> assetMap = new Dictionary<string, string>();
            if (data.assets != null)
            {
                foreach (var asset in data.assets)
                {
                    if (!string.IsNullOrEmpty(asset.id))
                        assetMap[asset.id] = asset.url;
                    if (!string.IsNullOrEmpty(asset.name) && !assetMap.ContainsKey(asset.name))
                        assetMap.Add(asset.name, asset.url);
                }
            }

            // Sprite Cache (Key: URL, Value: Internal Sprite Asset)
            Dictionary<string, Sprite> textureCache = new Dictionary<string, Sprite>();

            // [Optimization] Pre-download all unique URLs in parallel
            if (data.assets != null && data.assets.Count > 0)
            {
                var uniqueUrls = data.assets.Select(a => a.url).Where(u => !string.IsNullOrEmpty(u)).Distinct().ToList();
                Debug.Log($"<color=yellow>[UniforgeImporter]</color> Pre-downloading {uniqueUrls.Count} unique textures in parallel...");
                
                EditorUtility.DisplayProgressBar("Uniforge Import", $"Downloading {uniqueUrls.Count} textures...", 0f);
                
                var downloadTasks = uniqueUrls.Select(url => DownloadTexture(url)).ToArray();
                var results = await Task.WhenAll(downloadTasks);
                
                for (int i = 0; i < uniqueUrls.Count; i++)
                {
                    if (results[i] != null)
                    {
                        textureCache[uniqueUrls[i]] = results[i];
                    }
                }
                
                Debug.Log($"<color=green>[UniforgeImporter]</color> Pre-download complete! Cached {textureCache.Count}/{uniqueUrls.Count} textures.");
            }

            // Scene Processing
            if (data.scenes == null || data.scenes.Count == 0) return;
            var scene = data.scenes[0];

            if (scene.entities != null)
            {
                int totalEntities = scene.entities.Count;
                int current = 0;

                foreach (var entity in scene.entities)
                {
                    current++;
                    EditorUtility.DisplayProgressBar("Uniforge Import", $"Processing {entity.name} ({current}/{totalEntities})", (float)current / totalEntities);

                    GameObject go = new GameObject(entity.name);
                    go.transform.SetParent(root.transform);
                    
                    // Coordinate Conversion
                    float ppx = 100f; 
                    Vector3 pos = new Vector3(entity.x / ppx, -entity.y / ppx, 0);
                    go.transform.position = pos;
                    
                    // Rotation Fix for Polymorphic Type (float vs object)
                    float rot = 0f;
                    try {
                        if (entity.rotation is JObject jo) 
                            rot = jo["z"]?.Value<float>() ?? 0f;
                        else 
                            rot = Convert.ToSingle(entity.rotation);
                    } catch { rot = 0f; }
                    
                    go.transform.rotation = Quaternion.Euler(0, 0, rot);
                    go.transform.localScale = new Vector3(entity.scaleX, entity.scaleY, 1);
                    
                    // Camera Follow Setup
                    if (current == 1 && Camera.main != null)
                    {
                        Camera.main.transform.position = new Vector3(pos.x, pos.y, -10);
                    }

                    // Sprite Assignment
                    if (!string.IsNullOrEmpty(entity.texture))
                    {
                        Sprite sprite = null;
                        string url = assetMap.ContainsKey(entity.texture) ? assetMap[entity.texture] : entity.texture;

                        if (textureCache.ContainsKey(url))
                        {
                            sprite = textureCache[url];
                        }
                        else if (url.StartsWith("http") || url.StartsWith("data:"))
                        {
                            sprite = await DownloadTexture(url);
                            if (sprite != null) textureCache[url] = sprite;
                        }

                        if (sprite != null)
                        {
                            var sr = go.AddComponent<SpriteRenderer>();
                            sr.sprite = sprite;
                            sr.sortingOrder = 1;
                            
                            var collider = go.AddComponent<BoxCollider2D>();
                            collider.isTrigger = true; 
                            collider.size = sprite.bounds.size; 
                        }
                        else
                        {
                            // Warn but don't crash
                            // Debug.LogWarning($"Failed to resolve sprite for {entity.name}");
                        }
                    }

                    // Animator
                    var animController = AnimationGenerator.GenerateForEntity(entity, data.assets);
                    if (animController != null)
                    {
                        var animator = go.GetComponent<Animator>();
                        if (animator == null) animator = go.AddComponent<Animator>();
                        animator.runtimeAnimatorController = animController;
                    }

                    // UniforgeEntity
                    var ufe = go.AddComponent<Uniforge.FastTrack.Runtime.UniforgeEntity>();
                    ufe.EntityId = entity.id;
                    ufe.Role = entity.role ?? "";
                    if (entity.tags != null) ufe.Tags = new List<string>(entity.tags);

                    // Script Generation
                    UniforgeScriptGenerator.Generate(entity);
                    PendingScriptAttachments[entity.id] = go;
                }
            }

            // Tile Creation
            if (scene.tiles != null)
            {
                GameObject tileRoot = new GameObject("Uniforge_Tiles");
                tileRoot.transform.SetParent(root.transform);
                
                // Index Map for Tiles
                Dictionary<int, string> assetIndexMap = new Dictionary<int, string>();
                if (data.assets != null) {
                    foreach (var asset in data.assets)
                        if (!assetIndexMap.ContainsKey(asset.idx)) assetIndexMap.Add(asset.idx, asset.url);
                }

                float tileSize = 0.32f;

                foreach (var tile in scene.tiles)
                {
                    if (assetIndexMap.ContainsKey(tile.idx))
                    {
                        string url = assetIndexMap[tile.idx];
                        GameObject tObj = new GameObject($"Tile_{tile.x}_{tile.y}");
                        tObj.transform.SetParent(tileRoot.transform);
                        tObj.transform.position = new Vector3(tile.x * tileSize, -tile.y * tileSize, 0);

                        Sprite sprite = null;
                        if (textureCache.ContainsKey(url)) sprite = textureCache[url];
                        else {
                            sprite = await DownloadTexture(url);
                            if (sprite != null) textureCache[url] = sprite;
                        }

                        if (sprite != null)
                        {
                            var sr = tObj.AddComponent<SpriteRenderer>();
                            sr.sprite = sprite;
                        }
                    }
                }
            }

            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();
        }

        // --- Helper Methods ---

        private static async Task<Sprite> DownloadTexture(string originalUrl)
        {
            if (string.IsNullOrEmpty(originalUrl)) return null;

            // Simple Local/Direct check
            if (originalUrl.Contains("localhost") || originalUrl.Contains("127.0.0.1") || !originalUrl.Contains("/api/assets/"))
            {
                // Fallback or local
                return await DownloadTextureDirect(originalUrl);
            }

            // Proxy logic
            if (originalUrl.StartsWith("/")) originalUrl = "https://uniforge.kr" + originalUrl;
            
            // Bypass Data URI (handled in Direct)
            if (originalUrl.StartsWith("data:")) return await DownloadTextureDirect(originalUrl);

            string proxyUrl = $"https://images.weserv.nl/?url={Uri.EscapeDataString(originalUrl)}&output=png&n=-1";

            using (UnityWebRequest uwr = UnityWebRequest.Get(proxyUrl))
            {
                uwr.downloadHandler = new DownloadHandlerBuffer();
                uwr.timeout = 10;
                var op = uwr.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (uwr.result == UnityWebRequest.Result.Success)
                {
                    try {
                        return SaveAndLoadTexture(uwr.downloadHandler.data, originalUrl);
                    } catch { return await DownloadTextureDirect(originalUrl); }
                }
                else
                {
                    return await DownloadTextureDirect(originalUrl);
                }
            }
        }

        private static async Task<Sprite> DownloadTextureDirect(string url)
        {
            if (url.StartsWith("data:image"))
            {
                try {
                    string base64 = url.Substring(url.IndexOf(",") + 1);
                    byte[] bytes = Convert.FromBase64String(base64);
                    return SaveAndLoadTexture(bytes, "asset_" + url.GetHashCode() + ".webp");
                } catch { return null; }
            }

            using (UnityWebRequest uwr = UnityWebRequest.Get(url))
            {
                uwr.downloadHandler = new DownloadHandlerBuffer();
                uwr.redirectLimit = 10;
                uwr.timeout = 10;
                var op = uwr.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (uwr.result == UnityWebRequest.Result.Success)
                {
                    return SaveAndLoadTexture(uwr.downloadHandler.data, url);
                }
                return null;
            }
        }

        private static Sprite SaveAndLoadTexture(byte[] data, string originalUrl)
        {
            string fileName = GetSanitizedFileName(originalUrl);
            string dirPath = "Assets/Uniforge_FastTrack/Textures";
            if (!Directory.Exists(dirPath)) Directory.CreateDirectory(dirPath);
            
            string filePath = Path.Combine(dirPath, fileName);
            File.WriteAllBytes(filePath, data);
            
            AssetDatabase.ImportAsset(filePath, ImportAssetOptions.ForceUpdate);
            
            TextureImporter importer = AssetImporter.GetAtPath(filePath) as TextureImporter;
            if (importer != null) {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            
            return AssetDatabase.LoadAssetAtPath<Sprite>(filePath);
        }

        private static string GetSanitizedFileName(string url)
        {
            try
            {
                if (url.StartsWith("data:") || !url.StartsWith("http")) return "asset_" + Math.Abs(url.GetHashCode()) + ".webp";
                
                Uri uri = new Uri(url);
                string path = uri.AbsolutePath;
                string fileName = Path.GetFileName(path);
                if (!Path.HasExtension(fileName)) fileName += ".webp";
                return Uri.UnescapeDataString(fileName);
            }
            catch
            {
                return "asset_" + Math.Abs(url.GetHashCode()) + ".webp";
            }
        }

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
            catch {}
            #endif
        }
        
        // Callback Handler
        [UnityEditor.Callbacks.DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            if (PendingScriptAttachments.Count > 0)
            {
                var copy = new Dictionary<string, GameObject>(PendingScriptAttachments);
                PendingScriptAttachments.Clear();
                
                foreach (var kvp in copy)
                {
                    UniforgeScriptGenerator.AttachScript(kvp.Value, kvp.Key);
                }
            }
        }
    }
}
