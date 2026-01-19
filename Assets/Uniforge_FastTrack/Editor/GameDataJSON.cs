using System;
using System.Collections.Generic;

namespace Uniforge.FastTrack.Editor
{
    [Serializable]
    public class GameDataJSON
    {
        // Frontend 호환 필드
        public int formatVersion;
        public string activeSceneId;

        // 기존 필드 (하위 호환)
        public string projectType;
        public List<SceneJSON> scenes;
        public List<AssetDetailJSON> assets;
        public GlobalConfigJSON config;

        // Particle Systems (custom particle definitions)
        public List<ParticleSystemJSON> particles;
    }

    [Serializable]
    public class SceneJSON
    {
        public string id;
        public string sceneId;  // Frontend 호환 (sceneId ?? id 로 사용)
        public string name;
        public List<EntityJSON> entities;
        public List<TileJSON> tiles;
        public List<AssetDetailJSON> assets; // Scene-level assets
    }

    [Serializable]
    public class EntityJSON
    {
        public string id;
        public string name;
        public string type;

        // Transform
        public float x;
        public float y;
        public float z; // Layer/depth (Frontend compatibility)
        public object rotation; // Changed to object to support both float and {z: float} format
        public float rotationX; // 3D rotation X
        public float rotationY; // 3D rotation Y
        public float rotationZ; // 3D rotation Z (alternative)
        public float scaleX = 1f;
        public float scaleY = 1f;

        // Visual
        public string texture;

        // Metadata
        public string role;
        public List<string> tags;

        // Variables
        public List<VariableJSON> variables;

        // Runtime Logic
        public List<EventJSON> events;
        public List<ModuleJSON> modules;
        public List<ComponentJSON> components; // Frontend component system
        public List<LogicItemJSON> logic; // Frontend unified logic items

        // Particle Emitters attached to this entity
        public List<ParticleEmitterJSON> emitters;
    }

    [Serializable]
    public class EventJSON
    {
        public string id;
        public string trigger; // e.g., "OnUpdate", "OnStart", "OnTimer", "OnKeyDown", etc.
        public Dictionary<string, object> triggerParams;
        public string conditionLogic; // "AND", "OR", "BRANCH"
        public List<ConditionJSON> conditions;
        public string action; // Single action type (e.g., "Rotate")
        public Dictionary<string, object> @params; // Action parameters
        public List<ActionJSON> actions; // Multiple actions (Frontend compatibility)
        public List<ActionJSON> elseActions; // Else branch actions
    }

    [Serializable]
    public class ActionJSON
    {
        public string type;
        public Dictionary<string, object> @params;

        // Support flat action format from Frontend
        // Frontend sends: { "type": "SpawnEntity", "templateId": "abc", ... }
        // Instead of: { "type": "SpawnEntity", "params": { "templateId": "abc", ... } }
        [Newtonsoft.Json.JsonExtensionData]
        public Dictionary<string, object> AdditionalData { get; set; }

        /// <summary>
        /// Gets all parameters, merging @params and flat fields
        /// </summary>
        public Dictionary<string, object> GetAllParams()
        {
            var result = new Dictionary<string, object>();

            // First add @params if exists
            if (@params != null)
            {
                foreach (var kvp in @params)
                {
                    result[kvp.Key] = kvp.Value;
                }
            }

            // Then add flat fields (AdditionalData from JsonExtensionData)
            if (AdditionalData != null)
            {
                foreach (var kvp in AdditionalData)
                {
                    if (kvp.Key != "type" && !result.ContainsKey(kvp.Key))
                    {
                        result[kvp.Key] = kvp.Value;
                    }
                }
            }

            return result;
        }
    }

    [Serializable]
    public class ConditionJSON
    {
        public string type;
        public string key; // For InputDown/InputUp conditions (e.g., "KeyA")
        public string name; // Variable name (alias for params.name)
        public object value; // Comparison value (alias for params.value)
        public Dictionary<string, object> @params;
        public List<ActionJSON> then; // For BRANCH conditionLogic

        // Support flat condition format from Frontend
        [Newtonsoft.Json.JsonExtensionData]
        public Dictionary<string, object> AdditionalData { get; set; }

        /// <summary>
        /// Gets all parameters, merging @params and flat fields
        /// </summary>
        public Dictionary<string, object> GetAllParams()
        {
            var result = new Dictionary<string, object>();

            // First add @params if exists
            if (@params != null)
            {
                foreach (var kvp in @params)
                {
                    result[kvp.Key] = kvp.Value;
                }
            }

            // Add explicitly declared fields
            if (!string.IsNullOrEmpty(key)) result["key"] = key;
            if (!string.IsNullOrEmpty(name)) result["name"] = name;
            if (value != null) result["value"] = value;

            // Then add flat fields (AdditionalData from JsonExtensionData)
            if (AdditionalData != null)
            {
                foreach (var kvp in AdditionalData)
                {
                    if (kvp.Key != "type" && !result.ContainsKey(kvp.Key))
                    {
                        result[kvp.Key] = kvp.Value;
                    }
                }
            }

            return result;
        }
    }

    [Serializable]
    public class ComponentJSON
    {
        public string id;
        public string type; // "Transform", "Render", "Logic", "Signal", "playanimation", "particle", etc.
        public string @event; // Trigger event type
        public Dictionary<string, object> eventParams;
        public List<ConditionJSON> conditions;
        public string conditionLogic;
        public List<ActionJSON> actions;
        public List<ActionJSON> elseActions;

        // Capture additional flat properties (for playanimation, particle, etc.)
        [Newtonsoft.Json.JsonExtensionData]
        public Dictionary<string, object> AdditionalData { get; set; }

        /// <summary>
        /// Gets all parameters, merging eventParams and flat fields
        /// </summary>
        public Dictionary<string, object> GetAllParams()
        {
            var result = new Dictionary<string, object>();

            // First add eventParams if exists
            if (eventParams != null)
            {
                foreach (var kvp in eventParams)
                {
                    result[kvp.Key] = kvp.Value;
                }
            }

            // Then add flat fields (AdditionalData from JsonExtensionData)
            if (AdditionalData != null)
            {
                foreach (var kvp in AdditionalData)
                {
                    if (kvp.Key != "type" && kvp.Key != "id" && !result.ContainsKey(kvp.Key))
                    {
                        result[kvp.Key] = kvp.Value;
                    }
                }
            }

            return result;
        }
    }

    [Serializable]
    public class LogicItemJSON
    {
        // Frontend unified format: { kind: "component", component: {...} }
        public string kind; // "component"
        public ComponentJSON component; // Nested component data

        // Direct fields (alternative format)
        public string id;
        public string type; // Logic component type
        public string @event;
        public Dictionary<string, object> eventParams;
        public List<ConditionJSON> conditions;
        public string conditionLogic;
        public List<ActionJSON> actions;
    }

    [Serializable]
    public class ModuleJSON
    {
        public string id;
        public string name;
        public string entryNodeId;
        public List<NodeJSON> nodes;
        public List<EdgeJSON> edges;
    }

    [Serializable]
    public class NodeJSON
    {
        public string id;
        public string kind; // "Entry", "Action", "Condition"
        public float x;
        public float y;
        public string action; // If kind == Action
        public Dictionary<string, object> @params;
    }

    [Serializable]
    public class EdgeJSON
    {
        public string id;
        public string fromNodeId;
        public string fromPort;
        public string toNodeId;
        public string toPort;
    }

    [Serializable]
    public class AssetDetailJSON
    {
        public string id;
        public string name;
        public string type;
        public string tag; // Frontend uses "tag" for asset type (Tile, Character, Prefab)
        public string url;
        public int idx;

        // Sprite Sheet Metadata (can also contain prefab data for tag="Prefab")
        // Use object to support both AssetMetadataJSON and complex prefab structures
        public object metadata;
    }

    [Serializable]
    public class AssetMetadataJSON
    {
        public int frameWidth;
        public int frameHeight;
        public int frameCount;
        public int columns;
        public int rows;
        public Dictionary<string, AnimationDefJSON> animations;
    }

    [Serializable]
    public class AnimationDefJSON
    {
        // Legacy format (startFrame/endFrame)
        public int startFrame;
        public int endFrame;
        public int frameRate;
        public bool loop;
        
        // Frontend format (frames array + fps)
        public int[] frames;  // Array of frame indices [0, 1, 2, 3...]
        public int fps;       // Frames per second (frontend uses 'fps' instead of 'frameRate')
    }

    [Serializable]
    public class TileJSON
    {
        public int x;
        public int y;
        public int idx; // Maps to Asset.idx
    }

    [Serializable]
    public class GlobalConfigJSON
    {
        public string startSceneId;
    }

    [Serializable]
    public class VariableJSON
    {
        public string id;
        public string name;
        public string type; // "int", "float", "string", "bool", "vector2"
        public object value;
    }

    // ==============================
    // Particle System Protocol
    // ==============================

    [Serializable]
    public class ParticleSystemJSON
    {
        public string id;
        public string name;

        // Emission
        public float emissionRate = 10f;      // Particles per second
        public int burstCount = 0;            // Burst particle count
        public float burstInterval = 0f;      // Time between bursts

        // Lifetime
        public float lifetime = 1f;           // Particle lifetime in seconds
        public float lifetimeVariance = 0f;   // Random variance (+/-)

        // Size
        public float startSize = 10f;         // Start size in pixels
        public float endSize = 10f;           // End size in pixels
        public float sizeVariance = 0f;       // Random variance (+/-)

        // Speed
        public float startSpeed = 100f;       // Start speed in pixels/sec
        public float endSpeed = 100f;         // End speed
        public float speedVariance = 0f;      // Random variance (+/-)

        // Direction (degrees, 0 = right, 90 = up)
        public float angle = 90f;             // Emission direction
        public float angleVariance = 45f;     // Spread angle (+/-)

        // Gravity
        public float gravityX = 0f;           // Horizontal gravity
        public float gravityY = 0f;           // Vertical gravity (positive = down in web coords)

        // Color
        public ParticleColorJSON startColor;  // Start color
        public ParticleColorJSON endColor;    // End color (for gradient)

        // Alpha (transparency)
        public float startAlpha = 1f;         // Start opacity (0-1)
        public float endAlpha = 0f;           // End opacity (0-1)

        // Rotation
        public float startRotation = 0f;      // Start rotation in degrees
        public float rotationSpeed = 0f;      // Rotation speed in degrees/sec
        public float rotationVariance = 0f;   // Random variance

        // Shape
        public string shape = "point";        // "point", "circle", "rectangle", "line"
        public float shapeWidth = 0f;         // Shape width (for rectangle/line)
        public float shapeHeight = 0f;        // Shape height (for rectangle)
        public float shapeRadius = 0f;        // Shape radius (for circle)
        public bool shapeEdgeOnly = false;    // Emit from edge only

        // Visual
        public string texture;                // Particle texture/sprite asset name
        public string blendMode = "normal";   // "normal", "additive", "multiply"

        // Behavior
        public int maxParticles = 100;        // Maximum active particles
        public bool loop = true;              // Loop emission
        public float duration = 0f;           // Duration (0 = infinite)
        public bool playOnAwake = true;       // Start emitting automatically
    }

    [Serializable]
    public class ParticleColorJSON
    {
        public int r = 255;
        public int g = 255;
        public int b = 255;
        public float a = 1f;

        // Alternatively, can use hex string
        public string hex;
    }

    [Serializable]
    public class ParticleEmitterJSON
    {
        public string id;
        public string particleSystemId;       // Reference to ParticleSystemJSON
        public float x;                       // Position relative to entity
        public float y;
        public bool attachToEntity = true;    // Follow entity movement
    }
}
