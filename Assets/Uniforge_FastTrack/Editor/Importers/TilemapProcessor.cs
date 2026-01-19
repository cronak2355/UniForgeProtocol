using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using Uniforge.FastTrack.Editor;

namespace Uniforge.FastTrack.Editor.Importers
{
    /// <summary>
    /// Processes tile data and creates Unity Tilemaps.
    /// </summary>
    public static class TilemapProcessor
    {
        private static string TilesPath = "Assets/Uniforge_FastTrack/Generated/Tiles";

        /// <summary>
        /// Processes tiles and creates a Tilemap in the scene.
        /// </summary>
        public static GameObject ProcessTilemap(
            List<TileJSON> tiles,
            List<AssetDetailJSON> assets,
            Dictionary<string, Sprite> textureCache,
            Transform parent)
        {
            if (tiles == null || tiles.Count == 0)
                return null;

            Debug.Log($"<color=yellow>[TilemapProcessor]</color> Processing {tiles.Count} tiles...");

            // Create Grid object
            GameObject gridGo = new GameObject("Uniforge_TileGrid");
            gridGo.transform.SetParent(parent);
            var grid = gridGo.AddComponent<Grid>();
            grid.cellSize = new Vector3(1, 1, 0);

            // Create Tilemap object
            GameObject tilemapGo = new GameObject("Uniforge_Tilemap");
            tilemapGo.transform.SetParent(gridGo.transform);
            var tilemap = tilemapGo.AddComponent<Tilemap>();
            var tilemapRenderer = tilemapGo.AddComponent<TilemapRenderer>();
            tilemapRenderer.sortingOrder = -100;

            // Build idx -> Asset mapping
            var idxToAsset = BuildIndexToAssetMap(assets);

            // Cache for generated Tile assets
            var tileCache = new Dictionary<int, UnityEngine.Tilemaps.Tile>();

            // Ensure Tiles directory exists
            if (!Directory.Exists(TilesPath))
            {
                Directory.CreateDirectory(TilesPath);
                AssetDatabase.Refresh();
            }

            float tileSize = 1f;

            // Process each tile
            foreach (var tile in tiles)
            {
                if (!idxToAsset.TryGetValue(tile.idx, out var asset))
                {
                    Debug.LogWarning($"[TilemapProcessor] Tile at ({tile.x}, {tile.y}) references unknown asset idx: {tile.idx}");
                    continue;
                }

                // Get or create Tile asset
                if (!tileCache.TryGetValue(tile.idx, out var unityTile))
                {
                    unityTile = CreateTileAsset(tile.idx, asset, textureCache, out float detectedTileSize);
                    if (unityTile != null)
                    {
                        tileCache[tile.idx] = unityTile;
                        tileSize = detectedTileSize;
                    }
                    else
                    {
                        continue;
                    }
                }

                // Convert coordinates
                int gridX = tile.x;
                int gridY = -tile.y; // Flip Y for Unity

                Vector3Int cellPosition = new Vector3Int(gridX, gridY, 0);
                tilemap.SetTile(cellPosition, unityTile);
            }

            // Update Grid cell size
            grid.cellSize = new Vector3(tileSize, tileSize, 0);

            AssetDatabase.SaveAssets();
            Debug.Log($"<color=green>[TilemapProcessor]</color> Tilemap created with {tileCache.Count} unique tiles, {tiles.Count} placed");

            return gridGo;
        }

        /// <summary>
        /// Builds a mapping from tile index to asset data.
        /// </summary>
        private static Dictionary<int, AssetDetailJSON> BuildIndexToAssetMap(List<AssetDetailJSON> assets)
        {
            var map = new Dictionary<int, AssetDetailJSON>();

            if (assets == null) return map;

            foreach (var asset in assets)
            {
                if (asset.idx >= 0)
                {
                    map[asset.idx] = asset;
                }
            }

            return map;
        }

        /// <summary>
        /// Creates a Unity Tile asset from an asset definition.
        /// </summary>
        private static UnityEngine.Tilemaps.Tile CreateTileAsset(
            int idx,
            AssetDetailJSON asset,
            Dictionary<string, Sprite> textureCache,
            out float tileSize)
        {
            tileSize = 1f;

            // Find sprite
            Sprite tileSprite = null;
            string url = asset.url;

            if (textureCache.TryGetValue(url, out var cachedSprite))
            {
                tileSprite = cachedSprite;
            }
            else
            {
                // Try to load from Textures folder
                int urlHash = Math.Abs(url.GetHashCode());
                string texturePath = $"Assets/Uniforge_FastTrack/Textures/asset_{urlHash}.png";
                tileSprite = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
            }

            if (tileSprite == null)
            {
                Debug.LogWarning($"[TilemapProcessor] Could not find sprite for tile idx {idx}");
                return null;
            }

            // Create Tile asset
            var unityTile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
            unityTile.sprite = tileSprite;
            unityTile.color = Color.white;

            // Save Tile asset
            string tilePath = $"{TilesPath}/Tile_{idx}.asset";
            AssetDatabase.CreateAsset(unityTile, tilePath);

            // Calculate tile size
            float pixelsPerUnit = tileSprite.pixelsPerUnit;
            tileSize = tileSprite.rect.width / pixelsPerUnit;

            Debug.Log($"[TilemapProcessor] Created Tile asset: {tilePath} (size={tileSize})");

            return unityTile;
        }

        /// <summary>
        /// Clears all generated tile assets.
        /// </summary>
        public static void ClearGeneratedTiles()
        {
            if (!Directory.Exists(TilesPath))
            {
                Debug.Log("[TilemapProcessor] No tiles folder found.");
                return;
            }

            var files = Directory.GetFiles(TilesPath, "*.asset");
            int count = 0;

            foreach (var file in files)
            {
                AssetDatabase.DeleteAsset(file.Replace("\\", "/"));
                count++;
            }

            AssetDatabase.Refresh();
            Debug.Log($"[TilemapProcessor] Cleared {count} tile assets.");
        }
    }
}
