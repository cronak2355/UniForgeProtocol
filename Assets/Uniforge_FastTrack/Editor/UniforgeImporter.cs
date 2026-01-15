using UnityEngine;
using UnityEditor;
using Newtonsoft.Json;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace Uniforge.FastTrack.Editor
{
    public static class UniforgeImporter
    {
        public static async void ImportFromJson(string json)
        {
            try
            {
                // Save to file for inspection
                string savePath = System.IO.Path.Combine(Application.streamingAssetsPath, "project.json");
                if (!System.IO.Directory.Exists(Application.streamingAssetsPath))
                    System.IO.Directory.CreateDirectory(Application.streamingAssetsPath);
                
                System.IO.File.WriteAllText(savePath, json);
                Debug.Log($"<color=green>[UniforgeImporter]</color> JSON 저장 완료: {savePath}");

                var gameData = JsonConvert.DeserializeObject<GameDataJSON>(json);
                if (gameData == null)
                {
                    Debug.LogError($"<color=red>[UniforgeImporter]</color> JSON Parsing returned null.");
                    return;
                }

                int sceneCount = gameData.scenes != null ? gameData.scenes.Count : 0;
                Debug.Log($"<color=green>[UniforgeImporter]</color> 데이터 수신 성공: 씬 개수 {sceneCount}개");

                // Start Processing
                await ProcessGameData(gameData);
            }
            catch (JsonException je)
            {
                Debug.LogError($"<color=red>[UniforgeImporter]</color> JSON Format Error: {je.Message}");
            }
            catch (Exception e)
            {
                Debug.LogError($"<color=red>[UniforgeImporter]</color> Import Failed: {e.Message}\n{e.StackTrace}");
            }
        }

        private static async Task ProcessGameData(GameDataJSON data)
        {
            // 1. Root Object Management
            string rootName = "Uniforge_World";
            GameObject existingRoot = GameObject.Find(rootName);
            if (existingRoot != null)
            {
                GameObject.DestroyImmediate(existingRoot);
            }
            GameObject root = new GameObject(rootName);
            
            // 2. Asset Mapping (Key: Asset Name OR ID, Value: URL)
            // The Web Editor seems to send Asset Names as texture references, so we map Names to URLs.
            Dictionary<string, string> assetMap = new Dictionary<string, string>();
            if (data.assets != null)
            {
                foreach (var asset in data.assets)
                {
                    // Map by ID (Standard)
                    if (!assetMap.ContainsKey(asset.id))
                        assetMap.Add(asset.id, asset.url);

                    // Map by Name (Fallback / Observed behavior)
                    if (!string.IsNullOrEmpty(asset.name) && !assetMap.ContainsKey(asset.name))
                        assetMap.Add(asset.name, asset.url);
                }
            }

            // 3. Entity Creation Loop (First Scene)
            if (data.scenes == null || data.scenes.Count == 0) return;

            var scene = data.scenes[0];
            if (scene.entities == null) return;

            int totalEntities = scene.entities.Count;
            int current = 0;

            foreach (var entity in scene.entities)
            {
                current++;
                EditorUtility.DisplayProgressBar("Uniforge Import", $"Processing {entity.name} ({current}/{totalEntities})", (float)current / totalEntities);

                // Create GameObject
                // Create GameObject
                GameObject go = new GameObject(entity.name);
                go.transform.SetParent(root.transform);
                
                // [Coordinate Fix] Web coordinates are in Pixels. Unity uses Units (PPU 100).
                // We divide by 100 to normalize to Unity standard size. (Assuming 100 Pixels = 1 Unity Unit)
                float ppx = 100f; 
                Vector3 pos = new Vector3(entity.x / ppx, entity.y / ppx, 0);
                go.transform.position = pos;
                
                // Also apply rotation/scale if available
                go.transform.rotation = Quaternion.Euler(0, 0, entity.rotation);
                go.transform.localScale = new Vector3(entity.scaleX, entity.scaleY, 1);
                
                // Track Camera Target
                if (current == 1) // Simple "Focus on first entity" for now (or calculate average)
                {
                   if (Camera.main != null)
                   {
                       Camera.main.transform.position = new Vector3(pos.x, pos.y, -10);
                       Debug.Log($"[UniforgeImporter] Camera centered on {entity.name} at {pos}");
                   }
                }

                // 4. Image Download and Apply
                if (!string.IsNullOrEmpty(entity.texture))
                {
                    if (assetMap.ContainsKey(entity.texture))
                    {
                        string url = assetMap[entity.texture];
                        Debug.Log($"[UniforgeImporter] Downloading texture for {entity.name}: {url}");
                        
                        Texture2D texture = await DownloadTexture(url);
                        
                        if (texture != null)
                        {
                            Debug.Log($"[UniforgeImporter] Texture downloaded: {texture.width}x{texture.height}");
                            Sprite sprite = Sprite.Create(
                                texture, 
                                new Rect(0, 0, texture.width, texture.height), 
                                new Vector2(0.5f, 0.5f) // Center pivot
                            );
                            
                            var sr = go.AddComponent<SpriteRenderer>();
                            sr.sprite = sprite;
                            Debug.Log($"[UniforgeImporter] Sprite assigned to {go.name}");
                        }
                        else
                        {
                            Debug.LogError($"[UniforgeImporter] Download returned null for {url}");
                        }
                    }
                    else
                    {
                         Debug.LogWarning($"[UniforgeImporter] Asset ID not found in map: {entity.texture}");
                    }
                }
                else
                {
                    Debug.Log($"[UniforgeImporter] Entity {entity.name} has no texture defined.");
                }
                // 5. Generate Script
                UniforgeScriptGenerator.Generate(entity);
            }

            EditorUtility.ClearProgressBar();
            Debug.Log("<color=cyan>[UniforgeImporter]</color> Scene Generation Complete! Refeshing Assets for Compilation...");
            
            // Trigger Compilation to allow DidReloadScripts to attach components
            AssetDatabase.Refresh();
        }

        private static async Task<Texture2D> DownloadTexture(string originalUrl)
        {
            // [Compatibility Fix] Unity LoadImage has issues with some WebP variants.
            // We use wsrv.nl to convert to PNG on the fly for 100% compatibility.
            string proxyUrl = $"https://wsrv.nl/?url={originalUrl}&output=png";
            
            Debug.Log($"[UniforgeImporter] Downloading via Proxy: {proxyUrl}");

            using (UnityWebRequest uwr = UnityWebRequest.Get(proxyUrl))
            {
                uwr.downloadHandler = new DownloadHandlerBuffer();
                var operation = uwr.SendWebRequest(); // Wait for download

                while (!operation.isDone)
                    await Task.Yield();

                if (uwr.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[UniforgeImporter] Proxy Download Failed: {proxyUrl} ({uwr.error})");
                    // Fallback to original if proxy fails (though unlikely to work if WebP)
                    return null;
                }
                else
                {
                    try 
                    {
                        byte[] data = uwr.downloadHandler.data;
                        Texture2D texture = new Texture2D(2, 2);
                        if (texture.LoadImage(data)) 
                        {
                            return texture;
                        }
                        else
                        {
                            Debug.LogError($"[UniforgeImporter] LoadImage failed even after PNG conversion.");
                            return null;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[UniforgeImporter] Texture decoding error: {ex.Message}");
                        return null;
                    }
                }
            }
        }
    }
}
