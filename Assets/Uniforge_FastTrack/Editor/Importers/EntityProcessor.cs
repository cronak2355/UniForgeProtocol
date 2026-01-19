using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Uniforge.FastTrack.Runtime;

namespace Uniforge.FastTrack.Editor.Importers
{
    /// <summary>
    /// Processes entity JSON data and creates GameObjects in the scene.
    /// Handles transform, sprites, animations, and component setup.
    /// </summary>
    public static class EntityProcessor
    {
        /// <summary>
        /// Result of processing an entity.
        /// </summary>
        public class EntityResult
        {
            public GameObject GameObject;
            public string EntityId;
            public bool Success;
        }

        /// <summary>
        /// Processes all entities from a scene and creates GameObjects.
        /// </summary>
        public static async Task<List<EntityResult>> ProcessEntities(
            List<EntityJSON> entities,
            List<AssetDetailJSON> assets,
            Dictionary<string, string> assetMap,
            Dictionary<string, Sprite> textureCache,
            Transform parent,
            Action<string, float> progressCallback = null)
        {
            var results = new List<EntityResult>();

            if (entities == null || entities.Count == 0)
                return results;

            int totalEntities = entities.Count;
            int current = 0;

            foreach (var entity in entities)
            {
                current++;
                progressCallback?.Invoke($"Processing {entity.name} ({current}/{totalEntities})", (float)current / totalEntities);

                // Skip tile entities (handled by TilemapProcessor)
                if (IsTileEntity(entity))
                {
                    continue;
                }

                var result = await ProcessSingleEntity(entity, assets, assetMap, textureCache, parent);
                if (result.Success)
                {
                    results.Add(result);
                }
            }

            return results;
        }

        /// <summary>
        /// Checks if an entity is a tile (should be skipped by entity processor).
        /// </summary>
        public static bool IsTileEntity(EntityJSON entity)
        {
            // Check Type
            if (!string.IsNullOrEmpty(entity.type))
            {
                if (entity.type.Equals("Tile", StringComparison.OrdinalIgnoreCase) ||
                    entity.type.Equals("TileSprite", StringComparison.OrdinalIgnoreCase) ||
                    entity.type.Equals("TileMapLayer", StringComparison.OrdinalIgnoreCase) ||
                    entity.type.Equals("Ground", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // Check Name
            if (entity.name.StartsWith("tile_", StringComparison.OrdinalIgnoreCase) ||
                entity.name.StartsWith("Tile_", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Processes a single entity and creates a GameObject.
        /// </summary>
        public static async Task<EntityResult> ProcessSingleEntity(
            EntityJSON entity,
            List<AssetDetailJSON> assets,
            Dictionary<string, string> assetMap,
            Dictionary<string, Sprite> textureCache,
            Transform parent)
        {
            var result = new EntityResult
            {
                EntityId = entity.id,
                Success = false
            };

            try
            {
                Debug.Log($"<color=cyan>[EntityProcessor]</color> Processing: {entity.name} (id={entity.id})");

                // Create GameObject
                GameObject go = new GameObject(entity.name);
                go.transform.SetParent(parent);
                result.GameObject = go;

                // Setup Transform
                SetupTransform(go, entity);

                // Setup Camera Follow (first entity)
                if (Camera.main != null)
                {
                    // This is handled by the orchestrator if needed
                }

                // Process animations first (may slice sprite sheets)
                var animController = Uniforge.FastTrack.Editor.AnimationGenerator.GenerateForEntity(entity, assets);

                // Setup Sprite
                await SetupSprite(go, entity, assets, assetMap, textureCache);

                // Assign Animator
                if (animController != null)
                {
                    var animator = go.GetComponent<Animator>();
                    if (animator == null) animator = go.AddComponent<Animator>();
                    animator.runtimeAnimatorController = animController;
                    Debug.Log($"<color=green>[EntityProcessor]</color> Assigned AnimatorController to '{entity.name}'");
                }

                // Add UniforgeEntity component
                var ufe = go.AddComponent<Uniforge.FastTrack.Runtime.UniforgeEntity>();
                ufe.EntityId = entity.id;
                ufe.Role = entity.role ?? "";
                if (entity.tags != null) ufe.Tags = new List<string>(entity.tags);

                // Generate Script
                Uniforge.FastTrack.Editor.UniforgeScriptGenerator.Generate(entity);

                result.Success = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EntityProcessor] Failed to process entity {entity.name}: {ex.Message}\n{ex.StackTrace}");
            }

            return result;
        }

        /// <summary>
        /// Sets up the transform (position, rotation, scale) for an entity.
        /// </summary>
        private static void SetupTransform(GameObject go, EntityJSON entity)
        {
            const float PPU = 100f;

            // Position: Convert web coordinates to Unity (Y inverted)
            Vector3 pos = new Vector3(entity.x / PPU, -entity.y / PPU, 0);
            go.transform.position = pos;

            // Rotation: Handle polymorphic type (float vs object)
            float rot = 0f;
            try
            {
                if (entity.rotation is Newtonsoft.Json.Linq.JObject jo)
                    rot = jo["z"]?.ToObject<float>() ?? 0f;
                else
                    rot = Convert.ToSingle(entity.rotation);
            }
            catch { rot = 0f; }

            go.transform.rotation = Quaternion.Euler(0, 0, rot);

            // Scale
            go.transform.localScale = new Vector3(entity.scaleX, entity.scaleY, 1);
        }

        /// <summary>
        /// Sets up the sprite renderer and collider for an entity.
        /// </summary>
        private static async Task SetupSprite(
            GameObject go,
            EntityJSON entity,
            List<AssetDetailJSON> assets,
            Dictionary<string, string> assetMap,
            Dictionary<string, Sprite> textureCache)
        {
            if (string.IsNullOrEmpty(entity.texture)) return;

            Sprite sprite = null;
            string url = assetMap.ContainsKey(entity.texture) ? assetMap[entity.texture] : entity.texture;

            // Find the matching asset - check name, id, and url
            var asset = FindAssetByReference(assets, entity.texture);

            Debug.Log($"<color=cyan>[EntityProcessor]</color> Looking for asset: texture='{entity.texture}', found={asset != null}, assetName={asset?.name}, assetId={asset?.id}");

            // Try to get first frame from sliced sprites
            if (asset != null)
            {
                var slicedSprites = Uniforge.FastTrack.Editor.AnimationGenerator.GetSpritesForAsset(asset);
                if (slicedSprites != null && slicedSprites.Length > 0)
                {
                    sprite = slicedSprites[0];
                    Debug.Log($"<color=cyan>[EntityProcessor]</color> Using first frame from {slicedSprites.Length} sliced sprites for '{entity.name}'");
                }

                // Update URL to use asset's actual URL for cache lookup
                if (!string.IsNullOrEmpty(asset.url))
                {
                    url = asset.url;
                }
            }

            // Fallback to cached or downloaded sprite
            if (sprite == null)
            {
                if (textureCache.ContainsKey(url))
                {
                    sprite = textureCache[url];
                }
                else if (url.StartsWith("http") || url.StartsWith("data:"))
                {
                    sprite = await AssetDownloader.DownloadTexture(url);
                    if (sprite != null) textureCache[url] = sprite;
                }
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
        }

        /// <summary>
        /// Finds an asset by reference (can be name, id, or url).
        /// </summary>
        public static AssetDetailJSON FindAssetByReference(List<AssetDetailJSON> assets, string reference)
        {
            if (assets == null || string.IsNullOrEmpty(reference)) return null;

            // Exact match on name first (most common)
            var asset = assets.FirstOrDefault(a => a.name == reference);
            if (asset != null) return asset;

            // Then try id
            asset = assets.FirstOrDefault(a => a.id == reference);
            if (asset != null) return asset;

            // Then try url
            asset = assets.FirstOrDefault(a => a.url == reference);
            if (asset != null) return asset;

            // Case-insensitive name match
            asset = assets.FirstOrDefault(a =>
                !string.IsNullOrEmpty(a.name) && a.name.Equals(reference, StringComparison.OrdinalIgnoreCase));
            if (asset != null) return asset;

            return null;
        }

        /// <summary>
        /// Gets the default sprite for an entity (first frame if animated).
        /// </summary>
        public static Sprite GetDefaultSprite(EntityJSON entity, List<AssetDetailJSON> assets, Dictionary<string, Sprite> textureCache, Dictionary<string, string> assetMap)
        {
            if (string.IsNullOrEmpty(entity.texture)) return null;

            // Try sliced sprites first
            var asset = FindAssetByReference(assets, entity.texture);
            if (asset != null)
            {
                var slicedSprites = Uniforge.FastTrack.Editor.AnimationGenerator.GetSpritesForAsset(asset);
                if (slicedSprites != null && slicedSprites.Length > 0)
                {
                    return slicedSprites[0];
                }
            }

            // Fallback to cache
            string url = assetMap.ContainsKey(entity.texture) ? assetMap[entity.texture] : entity.texture;
            if (textureCache.ContainsKey(url))
            {
                return textureCache[url];
            }

            return null;
        }
    }
}
