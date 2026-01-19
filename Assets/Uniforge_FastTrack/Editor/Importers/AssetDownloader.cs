using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using Uniforge.FastTrack.Editor;

namespace Uniforge.FastTrack.Editor.Importers
{
    /// <summary>
    /// Handles downloading and caching of texture assets from URLs.
    /// Supports HTTP URLs, data URIs, and proxy fallback.
    /// </summary>
    public static class AssetDownloader
    {
        private static string TexturesPath = "Assets/Uniforge_FastTrack/Textures";

        /// <summary>
        /// Downloads a texture from URL and returns a Sprite.
        /// Handles proxy logic, data URIs, and caching.
        /// </summary>
        public static async Task<Sprite> DownloadTexture(string originalUrl)
        {
            if (string.IsNullOrEmpty(originalUrl)) return null;

            // Simple Local/Direct check
            if (originalUrl.Contains("localhost") || originalUrl.Contains("127.0.0.1") || !originalUrl.Contains("/api/assets/"))
            {
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
                    try
                    {
                        return SaveAndLoadTexture(uwr.downloadHandler.data, originalUrl);
                    }
                    catch
                    {
                        return await DownloadTextureDirect(originalUrl);
                    }
                }
                else
                {
                    return await DownloadTextureDirect(originalUrl);
                }
            }
        }

        /// <summary>
        /// Downloads texture directly without proxy.
        /// </summary>
        public static async Task<Sprite> DownloadTextureDirect(string url)
        {
            if (url.StartsWith("data:image"))
            {
                try
                {
                    // Extract MIME type to determine format
                    string mimeType = "png";
                    int mimeStart = url.IndexOf("data:image/") + 11;
                    int mimeEnd = url.IndexOf(";", mimeStart);
                    if (mimeEnd > mimeStart)
                    {
                        mimeType = url.Substring(mimeStart, mimeEnd - mimeStart);
                    }

                    Debug.Log($"[AssetDownloader] Processing Data URI, detected format: {mimeType}");

                    string base64 = url.Substring(url.IndexOf(",") + 1);
                    byte[] bytes = Convert.FromBase64String(base64);

                    string fileName = "asset_" + Math.Abs(url.GetHashCode()) + ".png";
                    return SaveAndLoadTexture(bytes, fileName);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[AssetDownloader] Failed to decode Data URI: {ex.Message}");
                    return null;
                }
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
                    Debug.Log($"[AssetDownloader] Downloaded: {url.Substring(0, Math.Min(50, url.Length))}... ({uwr.downloadHandler.data.Length} bytes)");
                    return SaveAndLoadTexture(uwr.downloadHandler.data, url);
                }
                else
                {
                    Debug.LogWarning($"[AssetDownloader] Failed to download: {url} - {uwr.error}");
                }
                return null;
            }
        }

        /// <summary>
        /// Saves texture bytes to disk and loads as Sprite.
        /// Handles WebP detection and conversion.
        /// </summary>
        public static Sprite SaveAndLoadTexture(byte[] data, string originalUrl)
        {
            string fileName = GetSanitizedFileName(originalUrl);

            if (!Directory.Exists(TexturesPath)) Directory.CreateDirectory(TexturesPath);

            string filePath = Path.Combine(TexturesPath, fileName);

            // Detect WebP format by magic bytes
            bool isWebP = data.Length > 12 &&
                          data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46 &&
                          data[8] == 0x57 && data[9] == 0x45 && data[10] == 0x42 && data[11] == 0x50;

            if (isWebP)
            {
                Debug.LogWarning($"[AssetDownloader] WebP format detected for {fileName}");
            }

            // Convert any image format to PNG
            Texture2D tempTexture = new Texture2D(2, 2);
            if (tempTexture.LoadImage(data))
            {
                Debug.Log($"[AssetDownloader] Decoded image: {tempTexture.width}x{tempTexture.height} -> {fileName}");

                byte[] pngData = tempTexture.EncodeToPNG();
                File.WriteAllBytes(filePath, pngData);
                UnityEngine.Object.DestroyImmediate(tempTexture);
            }
            else
            {
                Debug.LogError($"[AssetDownloader] Failed to decode image ({data.Length} bytes, WebP={isWebP})");

                // Create placeholder pink texture
                Texture2D placeholder = new Texture2D(32, 32);
                Color[] pixels = new Color[32 * 32];
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = new Color(1f, 0f, 1f, 1f);
                }
                placeholder.SetPixels(pixels);
                placeholder.Apply();
                byte[] pngData = placeholder.EncodeToPNG();
                File.WriteAllBytes(filePath, pngData);
                UnityEngine.Object.DestroyImmediate(placeholder);
                UnityEngine.Object.DestroyImmediate(tempTexture);
            }

            AssetDatabase.ImportAsset(filePath, ImportAssetOptions.ForceUpdate);

            ConfigureTextureImporter(filePath);

            // Load sprites
            var sprites = AssetDatabase.LoadAllAssetsAtPath(filePath).OfType<Sprite>().OrderBy(s => s.name).ToArray();
            Debug.Log($"[AssetDownloader] Loaded {sprites.Length} sprites from {filePath}");
            return sprites.Length > 0 ? sprites[0] : null;
        }

        /// <summary>
        /// Configures texture importer settings including sprite sheet slicing.
        /// </summary>
        private static void ConfigureTextureImporter(string filePath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(filePath) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(filePath);
            if (texture != null)
            {
                int width = texture.width;
                int height = texture.height;

                // Heuristic: horizontal strip detection
                if (width > height && width % height == 0 && width / height > 1)
                {
                    int frameCount = width / height;
                    int frameSize = height;

                    importer.spriteImportMode = SpriteImportMode.Multiple;

#pragma warning disable CS0618
                    var spritesheet = new List<SpriteMetaData>();
                    string baseName = Path.GetFileNameWithoutExtension(filePath);
                    for (int i = 0; i < frameCount; i++)
                    {
                        spritesheet.Add(new SpriteMetaData
                        {
                            name = $"{baseName}_{i}",
                            rect = new Rect(i * frameSize, 0, frameSize, frameSize),
                            pivot = new Vector2(0.5f, 0.5f),
                            alignment = (int)SpriteAlignment.Center
                        });
                    }
                    importer.spritesheet = spritesheet.ToArray();
#pragma warning restore CS0618

                    Debug.Log($"[AssetDownloader] Auto-sliced: {filePath} into {frameCount} frames ({frameSize}x{frameSize})");
                }
                else
                {
                    importer.spriteImportMode = SpriteImportMode.Single;
                }
            }
            else
            {
                importer.spriteImportMode = SpriteImportMode.Single;
            }

            importer.SaveAndReimport();
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Generates a sanitized filename from URL.
        /// </summary>
        public static string GetSanitizedFileName(string url)
        {
            try
            {
                if (url.StartsWith("data:") || !url.StartsWith("http"))
                    return "asset_" + Math.Abs(url.GetHashCode()) + ".png";

                Uri uri = new Uri(url);
                string path = uri.AbsolutePath;
                string fileName = Path.GetFileName(path);

                if (fileName.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                    fileName = Path.ChangeExtension(fileName, ".png");
                else if (!Path.HasExtension(fileName))
                    fileName += ".png";

                return Uri.UnescapeDataString(fileName);
            }
            catch
            {
                return "asset_" + Math.Abs(url.GetHashCode()) + ".png";
            }
        }

        /// <summary>
        /// Pre-downloads all assets in parallel and returns a cache dictionary.
        /// </summary>
        public static async Task<Dictionary<string, Sprite>> PredownloadAssets(List<AssetDetailJSON> assets, Action<string, float> progressCallback = null)
        {
            var textureCache = new Dictionary<string, Sprite>();

            if (assets == null || assets.Count == 0) return textureCache;

            var uniqueUrls = assets.Select(a => a.url).Where(u => !string.IsNullOrEmpty(u)).Distinct().ToList();
            Debug.Log($"[AssetDownloader] Pre-downloading {uniqueUrls.Count} unique textures...");

            progressCallback?.Invoke($"Downloading {uniqueUrls.Count} textures...", 0f);

            var downloadTasks = uniqueUrls.Select(url => DownloadTexture(url)).ToArray();
            var results = await Task.WhenAll(downloadTasks);

            for (int i = 0; i < uniqueUrls.Count; i++)
            {
                if (results[i] != null)
                {
                    textureCache[uniqueUrls[i]] = results[i];
                }
            }

            Debug.Log($"[AssetDownloader] Pre-download complete! Cached {textureCache.Count}/{uniqueUrls.Count} textures.");
            return textureCache;
        }

        /// <summary>
        /// Builds a mapping from asset ID/Name to URL.
        /// </summary>
        public static Dictionary<string, string> BuildAssetUrlMap(List<AssetDetailJSON> assets)
        {
            var assetMap = new Dictionary<string, string>();
            if (assets == null) return assetMap;

            foreach (var asset in assets)
            {
                if (!string.IsNullOrEmpty(asset.id))
                    assetMap[asset.id] = asset.url;
                if (!string.IsNullOrEmpty(asset.name) && !assetMap.ContainsKey(asset.name))
                    assetMap.Add(asset.name, asset.url);
            }

            return assetMap;
        }
    }
}
