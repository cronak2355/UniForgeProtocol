using System;
using System.Collections.Generic;

namespace Uniforge.FastTrack.Editor
{
    [Serializable]
    public class GameDataJSON
    {
        public string projectType;
        public List<SceneJSON> scenes;
        public List<AssetDetailJSON> assets;
        public GlobalConfigJSON config;
    }

    [Serializable]
    public class SceneJSON
    {
        public string id;
        public string name;
        public List<EntityJSON> entities;
    }

    [Serializable]
    public class EntityJSON
    {
        public string id;
        public string name;
        public string type;
        
        // Flattened structure as per request
        public float x;
        public float y;
        public float rotation;
        public float scaleX = 1f;
        public float scaleY = 1f;
        
        // "entity.texture" as per request
        public string texture;
        
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
        public string url; // Changed from path to url to match JSON
    }

    [Serializable]
    public class GlobalConfigJSON
    {
        public string startSceneId;
    }
}
