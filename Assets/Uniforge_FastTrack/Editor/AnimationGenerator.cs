using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.Animations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace Uniforge.FastTrack.Editor
{
    /// <summary>
    /// Creates AnimatorControllers and AnimationClips from sprite sheet assets.
    /// Called during import to enable automatic animation playback.
    /// </summary>
    public static class AnimationGenerator
    {
        private static string GeneratedAnimPath = "Assets/Uniforge_FastTrack/Generated/Animations";

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
        /// Generate all animations for an entity based on its texture asset.
        /// Extracts required animation names from entity logic and ensures all states exist.
        /// </summary>
        public static RuntimeAnimatorController GenerateForEntity(EntityJSON entity, List<AssetDetailJSON> assets)
        {
            Debug.Log($"[AnimationGenerator] GenerateForEntity called for '{entity.name}' (id={entity.id}, texture={entity.texture})");

            if (string.IsNullOrEmpty(entity.texture))
            {
                Debug.Log($"[AnimationGenerator] Entity '{entity.name}' has no texture, skipping animation generation");
                return null;
            }

            var asset = FindAssetByReference(assets, entity.texture);
            if (asset == null)
            {
                Debug.LogWarning($"[AnimationGenerator] Asset not found for texture '{entity.texture}' in entity '{entity.name}'. Available assets: {string.Join(", ", assets?.Select(a => $"{a.name}({a.id})") ?? new string[0])}");
                return null;
            }
            Debug.Log($"[AnimationGenerator] Found asset: name='{asset.name}', id='{asset.id}' for texture='{entity.texture}'");

            // Check if entity implementation needs animation
            bool force = HasAnimationUsage(entity);
            Debug.Log($"[AnimationGenerator] Entity '{entity.name}' force={force} (has PlayAnimation usage)");

            // Extract all animation names used by PlayAnimation actions in entity logic
            var requiredAnimations = ExtractRequiredAnimations(entity);
            Debug.Log($"[AnimationGenerator] Entity '{entity.name}' requires animations: [{string.Join(", ", requiredAnimations)}]");

            // Force generation if there are required animations
            if (requiredAnimations.Count > 0)
            {
                force = true;
            }

            // Try with metadata first, then try heuristic
            var controller = GenerateFromAsset(asset, force, requiredAnimations);
            if (controller != null)
            {
                Debug.Log($"[AnimationGenerator] Successfully created controller for '{entity.name}'");
            }
            else
            {
                Debug.LogWarning($"[AnimationGenerator] Failed to create controller for '{entity.name}'");
            }
            return controller;
        }

        /// <summary>
        /// Extracts all animation names used by PlayAnimation actions in entity logic.
        /// </summary>
        public static HashSet<string> ExtractRequiredAnimations(EntityJSON entity)
        {
            var result = new HashSet<string>();

            // Extract from events
            if (entity.events != null)
            {
                foreach (var evt in entity.events)
                {
                    ExtractAnimationsFromActions(evt.actions, result);
                    ExtractAnimationsFromActions(evt.elseActions, result);

                    // Single action format
                    if (evt.action == "PlayAnimation" && evt.@params != null)
                    {
                        var animName = ParameterHelper.GetParamString(evt.@params, "animationName");
                        if (!string.IsNullOrEmpty(animName)) result.Add(animName);
                    }
                }
            }

            // Extract from logic items (Frontend format)
            if (entity.logic != null)
            {
                foreach (var item in entity.logic)
                {
                    ComponentJSON comp = item.component ?? new ComponentJSON
                    {
                        actions = item.actions
                    };
                    if (comp?.actions != null)
                    {
                        ExtractAnimationsFromActions(comp.actions, result);
                    }
                }
            }

            // Extract from components
            if (entity.components != null)
            {
                foreach (var comp in entity.components)
                {
                    // Extract from Logic component actions
                    ExtractAnimationsFromActions(comp.actions, result);
                    ExtractAnimationsFromActions(comp.elseActions, result);

                    // Extract from PlayAnimation component type (auto-play animation)
                    if (comp.type != null)
                    {
                        string typeLC = comp.type.ToLowerInvariant();
                        if (typeLC == "playanimation" || typeLC == "animation")
                        {
                            // PlayAnimation component specifies an animation to play on start
                            // Use GetAllParams to get both eventParams and AdditionalData
                            var p = comp.GetAllParams();
                            var animName = ParameterHelper.GetParamString(p, "animationName");
                            if (string.IsNullOrEmpty(animName))
                                animName = ParameterHelper.GetParamString(p, "animation");
                            if (string.IsNullOrEmpty(animName))
                                animName = ParameterHelper.GetParamString(p, "name");
                            if (string.IsNullOrEmpty(animName))
                                animName = ParameterHelper.GetParamString(p, "state");

                            Debug.Log($"[AnimationGenerator] PlayAnimation component params: [{string.Join(", ", p.Select(kv => $"{kv.Key}={kv.Value}"))}]");

                            if (!string.IsNullOrEmpty(animName))
                            {
                                result.Add(animName);
                                Debug.Log($"[AnimationGenerator] Found PlayAnimation component animation: {animName}");
                            }
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Extracts animation names from a list of actions, including nested If actions.
        /// </summary>
        private static void ExtractAnimationsFromActions(List<ActionJSON> actions, HashSet<string> result)
        {
            if (actions == null) return;

            foreach (var action in actions)
            {
                if (action.type == "PlayAnimation")
                {
                    var p = action.GetAllParams();
                    // Try multiple parameter name variations (order by likelihood)
                    var animName = ParameterHelper.GetParamString(p, "animationName");
                    if (string.IsNullOrEmpty(animName))
                        animName = ParameterHelper.GetParamString(p, "animName");
                    if (string.IsNullOrEmpty(animName))
                        animName = ParameterHelper.GetParamString(p, "animation");
                    if (string.IsNullOrEmpty(animName))
                        animName = ParameterHelper.GetParamString(p, "name");
                    if (string.IsNullOrEmpty(animName))
                        animName = ParameterHelper.GetParamString(p, "anim");
                    if (string.IsNullOrEmpty(animName))
                        animName = ParameterHelper.GetParamString(p, "clip");
                    if (string.IsNullOrEmpty(animName))
                        animName = ParameterHelper.GetParamString(p, "state");

                    if (!string.IsNullOrEmpty(animName))
                    {
                        result.Add(animName);
                        Debug.Log($"[AnimationGenerator] Found PlayAnimation: {animName}");
                    }
                    else
                    {
                        Debug.LogWarning($"[AnimationGenerator] PlayAnimation action found but no animation name in params: {string.Join(", ", p.Keys)}");
                    }
                }
                else if (action.type == "If")
                {
                    // Check nested actions in If blocks
                    var p = action.GetAllParams();
                    if (p.TryGetValue("then", out var thenObj) && thenObj is JArray thenArr)
                    {
                        var thenActions = thenArr.ToObject<List<ActionJSON>>();
                        ExtractAnimationsFromActions(thenActions, result);
                    }
                    if (p.TryGetValue("else", out var elseObj) && elseObj is JArray elseArr)
                    {
                        var elseActions = elseArr.ToObject<List<ActionJSON>>();
                        ExtractAnimationsFromActions(elseActions, result);
                    }
                }
            }
        }

        private static bool HasAnimationUsage(EntityJSON entity)
        {
            if (entity.logic == null && (entity.events == null || entity.events.Count == 0)) return false;
            
            // Quick string search for PlayAnimation action type
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(entity);
            return json.Contains("\"PlayAnimation\"") || json.Contains("'PlayAnimation'");
        }

        /// <summary>
        /// Convert metadata object to AssetMetadataJSON
        /// </summary>
        private static AssetMetadataJSON GetMetadata(object metadata)
        {
            if (metadata == null) return null;
            if (metadata is AssetMetadataJSON typed) return typed;
            if (metadata is JObject jo) return jo.ToObject<AssetMetadataJSON>();
            try { return JObject.FromObject(metadata).ToObject<AssetMetadataJSON>(); }
            catch { return null; }
        }

        /// <summary>
        /// Generate AnimatorController from asset metadata or heuristic detection.
        /// </summary>
        /// <param name="asset">Asset to generate animation for</param>
        /// <param name="force">Force generation even without metadata</param>
        /// <param name="requiredAnimations">Animation names required by PlayAnimation actions</param>
        public static RuntimeAnimatorController GenerateFromAsset(AssetDetailJSON asset, bool force = false, HashSet<string> requiredAnimations = null)
        {
            var meta = GetMetadata(asset.metadata);
            requiredAnimations = requiredAnimations ?? new HashSet<string>();

            // [DEBUG] Log metadata parsing result
            Debug.Log($"<color=cyan>[AnimationGenerator]</color> GenerateFromAsset: {asset.name}");
            Debug.Log($"  - Raw metadata is null: {asset.metadata == null}");
            Debug.Log($"  - Parsed meta is null: {meta == null}");
            if (meta != null)
            {
                Debug.Log($"  - meta.animations is null: {meta.animations == null}");
                Debug.Log($"  - meta.animations count: {meta.animations?.Count ?? 0}");
                if (meta.animations != null)
                {
                    foreach (var kvp in meta.animations)
                    {
                        Debug.Log($"    - Animation '{kvp.Key}': frames={kvp.Value?.frames?.Length ?? 0}, fps={kvp.Value?.fps}, loop={kvp.Value?.loop}");
                    }
                }
            }
            Debug.Log($"  - requiredAnimations: [{string.Join(", ", requiredAnimations)}]");

            // Ensure directory exists
            if (!Directory.Exists(GeneratedAnimPath))
            {
                Directory.CreateDirectory(GeneratedAnimPath);
                AssetDatabase.Refresh();
            }

            string safeName = SanitizeName(asset.name);
            string controllerPath = $"{GeneratedAnimPath}/{safeName}_Controller.controller";

            // Check if already exists - but may need to update if required animations or sprites changed
            var existingController = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (existingController != null)
            {
                // Check if all required animations exist as states
                bool needsUpdate = false;
                var stateMachine = existingController.layers[0].stateMachine;
                var existingStates = new HashSet<string>(stateMachine.states.Select(s => s.state.name));

                Debug.Log($"[AnimationGenerator] Existing controller states: [{string.Join(", ", existingStates)}]");

                // Check for missing required states
                foreach (var animName in requiredAnimations)
                {
                    if (!existingStates.Contains(animName))
                    {
                        needsUpdate = true;
                        Debug.Log($"[AnimationGenerator] Controller missing required state: {animName}");
                    }
                }

                // [FIX] Also check if sprite count has changed (indicates re-slicing)
                var currentSprites = LoadSpritesFromAsset(asset);
                if (currentSprites != null && currentSprites.Length > 0)
                {
                    // Get sprite count from existing default state's clip
                    var defaultState = existingStates.Contains("default") 
                        ? stateMachine.states.First(s => s.state.name == "default").state 
                        : (stateMachine.states.Length > 0 ? stateMachine.states[0].state : null);
                    
                    if (defaultState?.motion is AnimationClip existingClip)
                    {
                        var bindings = AnimationUtility.GetObjectReferenceCurveBindings(existingClip);
                        if (bindings.Length > 0)
                        {
                            var keyframes = AnimationUtility.GetObjectReferenceCurve(existingClip, bindings[0]);
                            int existingFrameCount = keyframes?.Length ?? 0;
                            
                            // If sprite count differs significantly, regenerate
                            if (currentSprites.Length != existingFrameCount && currentSprites.Length > 1)
                            {
                                needsUpdate = true;
                                Debug.Log($"[AnimationGenerator] Sprite count changed: {existingFrameCount} -> {currentSprites.Length}, regenerating");
                            }
                        }
                    }
                }

                if (!needsUpdate && requiredAnimations.Count > 0)
                {
                    Debug.Log($"[AnimationGenerator] All required states exist, using existing controller");
                    return existingController;
                }
                else if (!needsUpdate)
                {
                    // No required animations specified - check if force is needed
                    if (!force)
                    {
                        return existingController;
                    }
                }

                // Delete existing controller to regenerate
                AssetDatabase.DeleteAsset(controllerPath);
                Debug.Log($"[AnimationGenerator] Regenerating controller for {asset.name}");
            }

            // Load sprites - try existing sliced sprites first
            var sprites = LoadSpritesFromAsset(asset);

            // If no sliced sprites and no metadata, try heuristic slicing
            if ((sprites == null || sprites.Length <= 1) && meta == null)
            {
                var heuristicResult = TryHeuristicSlicing(asset);
                if (heuristicResult.sprites != null && heuristicResult.sprites.Length > 1)
                {
                    sprites = heuristicResult.sprites;
                    meta = heuristicResult.metadata;
                    Debug.Log($"[AnimationGenerator] Heuristic slicing applied: {asset.name} -> {sprites.Length} frames");
                }
            }

            // If still no valid sprites or metadata, skip unless forced
            if ((sprites == null || sprites.Length == 0) && !force)
            {
                Debug.Log($"[AnimationGenerator] No sprites found for {asset.name}, skipping (force={force})");
                return null;
            }

            // Handle case where we have no sprites but need to create controller anyway (force=true)
            if (sprites == null || sprites.Length == 0)
            {
                Debug.Log($"[AnimationGenerator] No sprites but force=true, creating empty controller with required states");
                return CreateEmptyControllerWithStates(controllerPath, requiredAnimations);
            }

            if (meta == null)
            {
                // Create default metadata from sprite count
                meta = new AssetMetadataJSON
                {
                    frameCount = sprites.Length,
                    frameWidth = (int)sprites[0].rect.width,
                    frameHeight = (int)sprites[0].rect.height
                };
            }

            // Skip if only 1 frame and no explicit animations, unless forced
            if (!force && meta.frameCount <= 1 && (meta.animations == null || meta.animations.Count == 0))
            {
                Debug.Log($"[AnimationGenerator] Only 1 frame and no animations defined, skipping (force={force})");
                return null;
            }

            // If we have required animations but only 1 sprite, still create controller
            if (force && sprites.Length == 1 && requiredAnimations != null && requiredAnimations.Count > 0)
            {
                Debug.Log($"[AnimationGenerator] Only 1 sprite but required animations exist, creating controller with single-frame states");
            }

            // Create new controller
            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var rootStateMachine = controller.layers[0].stateMachine;

            // Track created states and clips for reuse
            var createdStates = new Dictionary<string, AnimatorState>();
            AnimationClip defaultClip = null;

            // Create animations from metadata
            if (meta.animations != null && meta.animations.Count > 0)
            {
                bool isFirstState = true;
                foreach (var kvp in meta.animations)
                {
                    var clip = CreateAnimationClip(safeName, kvp.Key, kvp.Value, sprites);
                    if (clip != null)
                    {
                        var state = rootStateMachine.AddState(kvp.Key);
                        state.motion = clip;
                        createdStates[kvp.Key] = state;

                        if (isFirstState)
                        {
                            rootStateMachine.defaultState = state;
                            defaultClip = clip;
                            isFirstState = false;
                        }
                    }
                }
            }
            else
            {
                // Create default animation using all frames
                var defaultDef = new AnimationDefJSON
                {
                    startFrame = 0,
                    endFrame = meta.frameCount - 1,
                    frameRate = 12,
                    loop = true
                };
                var clip = CreateAnimationClip(safeName, "default", defaultDef, sprites);
                if (clip != null)
                {
                    var state = rootStateMachine.AddState("default");
                    state.motion = clip;
                    rootStateMachine.defaultState = state;
                    createdStates["default"] = state;
                    defaultClip = clip;
                }
            }

            // Add missing required animations as alias states using default clip
            // This ensures PlayAnimation("walk") works even if "walk" isn't in metadata
            if (requiredAnimations != null && requiredAnimations.Count > 0 && defaultClip != null)
            {
                foreach (var animName in requiredAnimations)
                {
                    if (!createdStates.ContainsKey(animName))
                    {
                        // Create a state that uses the default clip as fallback
                        var aliasState = rootStateMachine.AddState(animName);
                        aliasState.motion = defaultClip;
                        createdStates[animName] = aliasState;
                        Debug.Log($"[AnimationGenerator] Created alias state '{animName}' using default animation for {asset.name}");
                    }
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[AnimationGenerator] Created controller: {controllerPath}");

            return controller;
        }

        /// <summary>
        /// Try to detect horizontal strip and slice automatically.
        /// Returns sliced sprites and generated metadata.
        /// </summary>
        private static (Sprite[] sprites, AssetMetadataJSON metadata) TryHeuristicSlicing(AssetDetailJSON asset)
        {
            string texturePath = null;
            Texture2D texture = null;

            // Try to find the texture in project by name first
            string[] guids = AssetDatabase.FindAssets($"t:Texture2D {asset.name}");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("Uniforge_FastTrack/Textures"))
                {
                    texturePath = path;
                    texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                    break;
                }
            }

            // Fallback: Try to find by URL hash (files are saved as asset_XXXX.png)
            if (texture == null && !string.IsNullOrEmpty(asset.url))
            {
                int urlHash = Math.Abs(asset.url.GetHashCode());
                string expectedFileName = $"asset_{urlHash}";
                
                guids = AssetDatabase.FindAssets($"t:Texture2D {expectedFileName}");
                foreach (var guid in guids)
                {
                    texturePath = AssetDatabase.GUIDToAssetPath(guid);
                    texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                    if (texture != null)
                    {
                        Debug.Log($"[AnimationGenerator] Found texture by URL hash for heuristic slicing: {texturePath}");
                        break;
                    }
                }
            }

            // Last resort: Search all textures in Textures folder
            if (texture == null)
            {
                string texturesPath = "Assets/Uniforge_FastTrack/Textures";
                if (Directory.Exists(texturesPath))
                {
                    var allTextures = AssetDatabase.FindAssets("t:Texture2D", new[] { texturesPath });
                    foreach (var guid in allTextures)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        if (!string.IsNullOrEmpty(asset.url))
                        {
                            int urlHash = Math.Abs(asset.url.GetHashCode());
                            if (path.Contains(urlHash.ToString()))
                            {
                                texturePath = path;
                                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                                Debug.Log($"[AnimationGenerator] Found texture by direct search: {texturePath}");
                                break;
                            }
                        }
                    }
                }
            }

            if (texture == null) 
            {
                Debug.LogWarning($"[AnimationGenerator] Could not find texture for heuristic slicing: {asset.name} (url={asset.url?.Substring(0, Math.Min(30, asset.url?.Length ?? 0))}...)");
                return (null, null);
            }

            int width = texture.width;
            int height = texture.height;

            // Heuristic: horizontal strip detection
            // width > height AND width is divisible by height AND ratio > 1
            if (width > height && width % height == 0 && width / height > 1)
            {
                int frameCount = width / height;
                int frameWidth = height; // Square frames
                int frameHeight = height;

                // Modify texture import settings to slice as sprite sheet
                var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
                if (importer != null && importer.spriteImportMode != SpriteImportMode.Multiple)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Multiple;
                    importer.filterMode = FilterMode.Point;

                    // Create sprite sheet metadata (using legacy API with warning suppression)
#pragma warning disable CS0618 // Type or member is obsolete
                    var spritesheet = new List<SpriteMetaData>();
                    for (int i = 0; i < frameCount; i++)
                    {
                        spritesheet.Add(new SpriteMetaData
                        {
                            name = $"{asset.name}_{i}",
                            rect = new Rect(i * frameWidth, 0, frameWidth, frameHeight),
                            pivot = new Vector2(0.5f, 0.5f),
                            alignment = (int)SpriteAlignment.Center
                        });
                    }
                    importer.spritesheet = spritesheet.ToArray();
#pragma warning restore CS0618

                    // Reimport
                    AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
                    AssetDatabase.Refresh();

                    Debug.Log($"[AnimationGenerator] Sliced texture: {asset.name} into {frameCount} frames");
                }

                // Load the newly sliced sprites
                var sprites = AssetDatabase.LoadAllAssetsAtPath(texturePath)
                    .OfType<Sprite>()
                    .OrderBy(s => s.name)
                    .ToArray();

                var metadata = new AssetMetadataJSON
                {
                    frameWidth = frameWidth,
                    frameHeight = frameHeight,
                    frameCount = frameCount
                };

                return (sprites, metadata);
            }

            return (null, null);
        }

        /// <summary>
        /// Public method to get sprites for an asset (used by UniforgeImporter to get first frame).
        /// </summary>
        public static Sprite[] GetSpritesForAsset(AssetDetailJSON asset)
        {
            if (asset == null) return null;
            
            // First try to load already sliced sprites
            var sprites = LoadSpritesFromAsset(asset);
            if (sprites != null && sprites.Length > 0)
            {
                return sprites;
            }
            
            // If no sliced sprites, try heuristic slicing
            var (heuristicSprites, _) = TryHeuristicSlicing(asset);
            return heuristicSprites;
        }

        private static Sprite[] LoadSpritesFromAsset(AssetDetailJSON asset)
        {
            Debug.Log($"[AnimationGenerator] LoadSpritesFromAsset: name={asset.name}, id={asset.id}, url={(asset.url?.Substring(0, Math.Min(80, asset.url?.Length ?? 0)))}...");

            string texturesPath = "Assets/Uniforge_FastTrack/Textures";

            // Method 1: Try direct path by URL hash (primary method - this is how files are saved)
            if (!string.IsNullOrEmpty(asset.url))
            {
                int urlHash = Math.Abs(asset.url.GetHashCode());
                string directPath = $"{texturesPath}/asset_{urlHash}.png";

                if (File.Exists(directPath))
                {
                    var sprites = AssetDatabase.LoadAllAssetsAtPath(directPath)
                        .OfType<Sprite>()
                        .OrderBy(s => s.name)
                        .ToArray();

                    if (sprites.Length > 0)
                    {
                        Debug.Log($"[AnimationGenerator] Found {sprites.Length} sprites by direct URL hash path: {directPath}");
                        return sprites;
                    }
                }
            }

            // Method 2: Try to find by asset name in Textures folder
            if (!string.IsNullOrEmpty(asset.name) && Directory.Exists(texturesPath))
            {
                // Check for exact filename match
                string[] possiblePaths = new[]
                {
                    $"{texturesPath}/{asset.name}.png",
                    $"{texturesPath}/{asset.name}",
                    $"{texturesPath}/{asset.id}.png"
                };

                foreach (var path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        var sprites = AssetDatabase.LoadAllAssetsAtPath(path)
                            .OfType<Sprite>()
                            .OrderBy(s => s.name)
                            .ToArray();

                        if (sprites.Length > 0)
                        {
                            Debug.Log($"[AnimationGenerator] Found {sprites.Length} sprites by name path: {path}");
                            return sprites;
                        }
                    }
                }
            }

            // Method 3: AssetDatabase search by name
            string[] guids = AssetDatabase.FindAssets($"t:Texture2D {asset.name}", new[] { texturesPath });
            Debug.Log($"[AnimationGenerator] Search by name '{asset.name}': found {guids.Length} results");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var sprites = AssetDatabase.LoadAllAssetsAtPath(path)
                    .OfType<Sprite>()
                    .OrderBy(s => s.name)
                    .ToArray();

                if (sprites.Length > 0)
                {
                    Debug.Log($"[AnimationGenerator] Found {sprites.Length} sprites by AssetDatabase search: {path}");
                    return sprites;
                }
            }

            // Method 4: Scan all textures in folder and match by URL hash in filename
            if (Directory.Exists(texturesPath) && !string.IsNullOrEmpty(asset.url))
            {
                int urlHash = Math.Abs(asset.url.GetHashCode());
                var allTextures = AssetDatabase.FindAssets("t:Texture2D", new[] { texturesPath });

                foreach (var guid in allTextures)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.Contains(urlHash.ToString()))
                    {
                        var sprites = AssetDatabase.LoadAllAssetsAtPath(path)
                            .OfType<Sprite>()
                            .OrderBy(s => s.name)
                            .ToArray();

                        if (sprites.Length > 0)
                        {
                            Debug.Log($"[AnimationGenerator] Found {sprites.Length} sprites by URL hash scan: {path}");
                            return sprites;
                        }
                    }
                }
            }

            // Method 5: Fuzzy name match
            if (Directory.Exists(texturesPath) && !string.IsNullOrEmpty(asset.name))
            {
                var allTextures = AssetDatabase.FindAssets("t:Texture2D", new[] { texturesPath });
                string searchName = asset.name.ToLower();
                foreach (var guid in allTextures)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    string fileName = Path.GetFileNameWithoutExtension(path).ToLower();
                    
                    if (fileName.Contains(searchName) || searchName.Contains(fileName))
                    {
                        var sprites = AssetDatabase.LoadAllAssetsAtPath(path)
                            .OfType<Sprite>()
                            .OrderBy(s => s.name)
                            .ToArray();
                        if (sprites.Length > 1) // Multiple sprites = sprite sheet
                        {
                            Debug.Log($"[AnimationGenerator] Found sprite by name match: {path} ({sprites.Length} sprites)");
                            return sprites;
                        }
                    }
                }
                
                // [REMOVED] Dangerous "last resort" fallback that could return wrong asset's sprites.
                // If we reach here, no matching sprites were found - this is safer than returning wrong ones.
                Debug.LogWarning($"[AnimationGenerator] Fuzzy match failed for '{asset.name}'. Searched {allTextures.Length} textures in {texturesPath}.");
            }

            Debug.LogWarning($"[AnimationGenerator] Could not find sprites for asset: {asset.name}");
            return null;
        }

        private static AnimationClip CreateAnimationClip(string assetName, string animName, AnimationDefJSON def, Sprite[] sprites)
        {
            if (sprites == null || sprites.Length == 0) return null;

            // Determine frame rate (frontend uses 'fps', legacy uses 'frameRate')
            int frameRate = def.fps > 0 ? def.fps : (def.frameRate > 0 ? def.frameRate : 12);

            var clip = new AnimationClip();
            clip.name = $"{assetName}_{animName}";
            clip.frameRate = frameRate;

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = def.loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            // Create keyframes
            var keyframes = new List<ObjectReferenceKeyframe>();
            float frameDuration = 1f / frameRate;

            // Check if using frontend format (frames array) or legacy format (startFrame/endFrame)
            if (def.frames != null && def.frames.Length > 0)
            {
                // Frontend format: use frames array directly
                Debug.Log($"[AnimationGenerator] Using frames array format for '{animName}': [{string.Join(", ", def.frames)}], fps={frameRate}");
                
                for (int i = 0; i < def.frames.Length; i++)
                {
                    int frameIndex = def.frames[i];
                    if (frameIndex >= 0 && frameIndex < sprites.Length)
                    {
                        keyframes.Add(new ObjectReferenceKeyframe
                        {
                            time = i * frameDuration,
                            value = sprites[frameIndex]
                        });
                    }
                    else
                    {
                        Debug.LogWarning($"[AnimationGenerator] Frame index {frameIndex} out of range for animation '{animName}' (sprites.Length={sprites.Length})");
                    }
                }
            }
            else
            {
                // Legacy format: use startFrame/endFrame
                int start = Mathf.Clamp(def.startFrame, 0, sprites.Length - 1);
                int end = Mathf.Clamp(def.endFrame, start, sprites.Length - 1);
                
                Debug.Log($"[AnimationGenerator] Using legacy format for '{animName}': startFrame={start}, endFrame={end}, frameRate={frameRate}");

                for (int i = start; i <= end; i++)
                {
                    keyframes.Add(new ObjectReferenceKeyframe
                    {
                        time = (i - start) * frameDuration,
                        value = sprites[i]
                    });
                }
            }

            if (keyframes.Count == 0)
            {
                Debug.LogWarning($"[AnimationGenerator] No keyframes created for animation '{animName}'");
                return null;
            }

            // Create binding
            var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes.ToArray());

            // Save clip
            string clipPath = $"{GeneratedAnimPath}/{clip.name}.anim";
            AssetDatabase.CreateAsset(clip, clipPath);

            Debug.Log($"[AnimationGenerator] Created animation clip '{animName}' with {keyframes.Count} frames at {frameRate} FPS");
            return clip;
        }

        private static string SanitizeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Unnamed";
            var result = new System.Text.StringBuilder();
            foreach (char c in name)
            {
                if (char.IsLetterOrDigit(c) || c == '_')
                    result.Append(c);
                else
                    result.Append('_');
            }
            return result.ToString();
        }

        /// <summary>
        /// Creates an empty AnimatorController with states for required animations.
        /// Used when sprites aren't available but we need animation states.
        /// </summary>
        private static RuntimeAnimatorController CreateEmptyControllerWithStates(string controllerPath, HashSet<string> requiredAnimations)
        {
            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var rootStateMachine = controller.layers[0].stateMachine;

            // Create an empty animation clip for placeholder
            string clipPath = controllerPath.Replace(".controller", "_empty.anim");
            var emptyClip = new AnimationClip();
            emptyClip.name = "empty";
            emptyClip.frameRate = 12;
            AssetDatabase.CreateAsset(emptyClip, clipPath);

            bool isFirst = true;
            foreach (var animName in requiredAnimations)
            {
                var state = rootStateMachine.AddState(animName);
                state.motion = emptyClip;

                if (isFirst)
                {
                    rootStateMachine.defaultState = state;
                    isFirst = false;
                }
                Debug.Log($"[AnimationGenerator] Created empty state '{animName}'");
            }

            // If no required animations, create a default state
            if (requiredAnimations == null || requiredAnimations.Count == 0)
            {
                var defaultState = rootStateMachine.AddState("default");
                defaultState.motion = emptyClip;
                rootStateMachine.defaultState = defaultState;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[AnimationGenerator] Created empty controller with {requiredAnimations?.Count ?? 0} states: {controllerPath}");

            return controller;
        }

        /// <summary>
        /// Menu item to clear all generated animation controllers.
        /// Use this when animation states are out of sync.
        /// </summary>
        [UnityEditor.MenuItem("Uniforge/Clear Generated Animations")]
        public static void ClearGeneratedAnimations()
        {
            if (!Directory.Exists(GeneratedAnimPath))
            {
                Debug.Log("[AnimationGenerator] No generated animations folder found.");
                return;
            }

            // Delete all .controller and .anim files
            var controllerFiles = Directory.GetFiles(GeneratedAnimPath, "*.controller");
            var animFiles = Directory.GetFiles(GeneratedAnimPath, "*.anim");

            int count = 0;
            foreach (var file in controllerFiles)
            {
                AssetDatabase.DeleteAsset(file.Replace("\\", "/"));
                count++;
            }
            foreach (var file in animFiles)
            {
                AssetDatabase.DeleteAsset(file.Replace("\\", "/"));
                count++;
            }

            AssetDatabase.Refresh();
            Debug.Log($"[AnimationGenerator] Cleared {count} generated animation files. Re-import your game JSON to regenerate.");
        }
    }
}
