using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.Animations;

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
        /// Generate all animations for an entity based on its texture asset.
        /// </summary>
        public static RuntimeAnimatorController GenerateForEntity(EntityJSON entity, List<AssetDetailJSON> assets)
        {
            if (string.IsNullOrEmpty(entity.texture)) return null;

            var asset = assets?.FirstOrDefault(a => a.name == entity.texture || a.id == entity.texture);
            if (asset == null || asset.metadata == null) return null;

            return GenerateFromAsset(asset);
        }

        /// <summary>
        /// Generate AnimatorController from asset metadata.
        /// </summary>
        public static RuntimeAnimatorController GenerateFromAsset(AssetDetailJSON asset)
        {
            if (asset.metadata == null) return null;
            if (asset.metadata.frameCount <= 1 && 
                (asset.metadata.animations == null || asset.metadata.animations.Count == 0))
                return null;

            // Ensure directory exists
            if (!Directory.Exists(GeneratedAnimPath))
            {
                Directory.CreateDirectory(GeneratedAnimPath);
                AssetDatabase.Refresh();
            }

            string safeName = SanitizeName(asset.name);
            string controllerPath = $"{GeneratedAnimPath}/{safeName}_Controller.controller";

            // Check if already exists
            var existingController = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (existingController != null) return existingController;

            // Create new controller
            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var rootStateMachine = controller.layers[0].stateMachine;

            // Load the sprite sheet
            var sprites = LoadSpritesFromAsset(asset);
            if (sprites == null || sprites.Length == 0)
            {
                Debug.LogWarning($"[AnimationGenerator] No sprites loaded for: {asset.name}");
                return null;
            }

            // Create animations from metadata
            if (asset.metadata.animations != null && asset.metadata.animations.Count > 0)
            {
                bool isFirstState = true;
                foreach (var kvp in asset.metadata.animations)
                {
                    var clip = CreateAnimationClip(safeName, kvp.Key, kvp.Value, sprites);
                    if (clip != null)
                    {
                        var state = rootStateMachine.AddState(kvp.Key);
                        state.motion = clip;
                        
                        if (isFirstState)
                        {
                            rootStateMachine.defaultState = state;
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
                    endFrame = asset.metadata.frameCount - 1,
                    frameRate = 12,
                    loop = true
                };
                var clip = CreateAnimationClip(safeName, "default", defaultDef, sprites);
                if (clip != null)
                {
                    var state = rootStateMachine.AddState("default");
                    state.motion = clip;
                    rootStateMachine.defaultState = state;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[AnimationGenerator] Created controller: {controllerPath}");

            return controller;
        }

        private static Sprite[] LoadSpritesFromAsset(AssetDetailJSON asset)
        {
            // Try to find already imported texture in project
            string[] guids = AssetDatabase.FindAssets($"t:Texture2D {asset.name}");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var sprites = AssetDatabase.LoadAllAssetsAtPath(path)
                    .OfType<Sprite>()
                    .OrderBy(s => s.name)
                    .ToArray();
                
                if (sprites.Length > 0) return sprites;
            }

            // If not found, we need to download and slice the texture
            // This is handled by UniforgeImporter - return null for now
            Debug.LogWarning($"[AnimationGenerator] Texture not found in project: {asset.name}. Import assets first.");
            return null;
        }

        private static AnimationClip CreateAnimationClip(string assetName, string animName, AnimationDefJSON def, Sprite[] sprites)
        {
            if (sprites == null || sprites.Length == 0) return null;

            int start = Mathf.Clamp(def.startFrame, 0, sprites.Length - 1);
            int end = Mathf.Clamp(def.endFrame, start, sprites.Length - 1);
            int frameRate = def.frameRate > 0 ? def.frameRate : 12;

            var clip = new AnimationClip();
            clip.name = $"{assetName}_{animName}";
            clip.frameRate = frameRate;

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = def.loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            // Create keyframes
            var keyframes = new List<ObjectReferenceKeyframe>();
            float frameDuration = 1f / frameRate;

            for (int i = start; i <= end; i++)
            {
                keyframes.Add(new ObjectReferenceKeyframe
                {
                    time = (i - start) * frameDuration,
                    value = sprites[i]
                });
            }

            // Create binding
            var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes.ToArray());

            // Save clip
            string clipPath = $"{GeneratedAnimPath}/{clip.name}.anim";
            AssetDatabase.CreateAsset(clip, clipPath);

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
    }
}
