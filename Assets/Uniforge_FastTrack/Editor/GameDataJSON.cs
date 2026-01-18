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
    }

    [Serializable]
    public class SceneJSON
    {
        public string id;
        public string sceneId;  // Frontend 호환 (sceneId ?? id 로 사용)
        public string name;
        public List<EntityJSON> entities;
        public List<TileJSON> tiles;
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
        public object rotation; // Changed to object to support both float and {z: float} format
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
    }

    [Serializable]
    public class EventJSON
    {
        public string id;
        public string trigger; // e.g., "OnUpdate", "OnStart"
        public Dictionary<string, object> triggerParams;
        public string conditionLogic; // "AND", "OR"
        public List<ConditionJSON> conditions;
        public string action; // e.g., "Rotate"
        public Dictionary<string, object> @params; // Action parameters
    }

    [Serializable]
    public class ConditionJSON
    {
        public string type;
        public string key; // For InputDown/InputUp conditions (e.g., "KeyA")
        public Dictionary<string, object> @params;
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
        public string url; 
        public int idx;
        
        // Sprite Sheet Metadata
        public AssetMetadataJSON metadata;
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
        public int startFrame;
        public int endFrame;
        public int frameRate;
        public bool loop;
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
}
