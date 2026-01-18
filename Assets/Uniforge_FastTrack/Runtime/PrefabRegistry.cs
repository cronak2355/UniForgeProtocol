using UnityEngine;
using System.Collections.Generic;

namespace Uniforge.FastTrack.Runtime
{
    /// <summary>
    /// Registry for entity prefabs, enabling SpawnEntity functionality.
    /// Maps entity IDs/names to prefab instances.
    /// </summary>
    public class PrefabRegistry : MonoBehaviour
    {
        [Header("Registered Prefabs")]
        [Tooltip("Manually assign prefabs or they will be auto-registered during import")]
        public List<PrefabEntry> Prefabs = new List<PrefabEntry>();

        private Dictionary<string, GameObject> _registry = new Dictionary<string, GameObject>();
        private Dictionary<string, GameObject> _runtimeTemplates = new Dictionary<string, GameObject>();

        void Awake()
        {
            // Build registry from inspector list
            foreach (var entry in Prefabs)
            {
                if (entry.Prefab != null && !string.IsNullOrEmpty(entry.Id))
                {
                    _registry[entry.Id] = entry.Prefab;
                }
            }
        }

        /// <summary>
        /// Register a runtime entity as a spawn template.
        /// </summary>
        public void RegisterTemplate(string id, GameObject template)
        {
            _runtimeTemplates[id] = template;
        }

        /// <summary>
        /// Spawn an entity by template ID.
        /// </summary>
        public GameObject Spawn(string templateId, Vector3 position, Quaternion rotation = default)
        {
            if (rotation == default) rotation = Quaternion.identity;

            GameObject prefab = GetPrefab(templateId);
            if (prefab == null)
            {
                Debug.LogWarning($"[PrefabRegistry] Template not found: {templateId}");
                return null;
            }

            return Instantiate(prefab, position, rotation);
        }

        /// <summary>
        /// Spawn self (clone the calling entity).
        /// </summary>
        public GameObject SpawnSelf(GameObject self, Vector3 position)
        {
            return Instantiate(self, position, self.transform.rotation);
        }

        /// <summary>
        /// Static shortcut for generated scripts.
        /// </summary>
        public static GameObject SpawnStatic(string templateId, Vector3 position)
        {
            if (UniforgeRuntime.Instance?.Prefabs != null)
                return UniforgeRuntime.Instance.Prefabs.Spawn(templateId, position);
            Debug.LogWarning($"[PrefabRegistry] Runtime not initialized. Cannot spawn: {templateId}");
            return null;
        }

        public static GameObject SpawnSelfStatic(GameObject self, Vector3 position)
        {
            if (UniforgeRuntime.Instance?.Prefabs != null)
                return UniforgeRuntime.Instance.Prefabs.SpawnSelf(self, position);
            return Instantiate(self, position, self.transform.rotation);
        }

        private GameObject GetPrefab(string templateId)
        {
            // Check runtime templates first (entities registered during play)
            if (_runtimeTemplates.TryGetValue(templateId, out var runtimePrefab))
                return runtimePrefab;

            // Check inspector-assigned prefabs
            if (_registry.TryGetValue(templateId, out var prefab))
                return prefab;

            // Try to find by name in scene
            var found = GameObject.Find(templateId);
            return found;
        }
    }

    [System.Serializable]
    public class PrefabEntry
    {
        public string Id;
        public GameObject Prefab;
    }
}
