using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Uniforge.FastTrack.Runtime
{
    public class UniforgeEntity : MonoBehaviour
    {
        public string EntityId;
        public string EntityName;
        public string Role = "neutral";
        public List<string> Tags = new List<string>();

        // === Static Entity Registry ===
        public static List<UniforgeEntity> RegisteredEntities { get; private set; } = new List<UniforgeEntity>();

        void OnEnable()
        {
            if (!RegisteredEntities.Contains(this))
                RegisteredEntities.Add(this);
        }

        void OnDisable()
        {
            RegisteredEntities.Remove(this);
        }

        // === Static Helper Methods ===

        /// <summary>
        /// Find the nearest entity with a specific role.
        /// </summary>
        public static UniforgeEntity FindNearestWithRole(string role, Vector3 fromPosition)
        {
            UniforgeEntity nearest = null;
            float minDist = float.MaxValue;

            foreach (var entity in RegisteredEntities)
            {
                if (entity.Role == role)
                {
                    float dist = Vector3.Distance(fromPosition, entity.transform.position);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        nearest = entity;
                    }
                }
            }
            return nearest;
        }

        /// <summary>
        /// Get distance to a specific entity by ID.
        /// </summary>
        public static float GetDistanceToEntity(Transform from, string entityId)
        {
            var target = RegisteredEntities.FirstOrDefault(e => e.EntityId == entityId || e.gameObject.name == entityId);
            if (target == null) return float.MaxValue;
            return Vector3.Distance(from.position, target.transform.position);
        }

        /// <summary>
        /// Get distance to the nearest entity with a specific role.
        /// </summary>
        public static float GetDistanceToNearestWithRole(Transform from, string role)
        {
            float minDist = float.MaxValue;
            foreach (var entity in RegisteredEntities)
            {
                if (entity.Role == role)
                {
                    float dist = Vector3.Distance(from.position, entity.transform.position);
                    if (dist < minDist) minDist = dist;
                }
            }
            return minDist;
        }

        /// <summary>
        /// Get distance to the nearest entity (any role, excluding self if called from an entity).
        /// </summary>
        public static float GetDistanceToNearest(Transform from)
        {
            float minDist = float.MaxValue;
            foreach (var entity in RegisteredEntities)
            {
                if (entity.transform == from) continue; // Skip self
                float dist = Vector3.Distance(from.position, entity.transform.position);
                if (dist < minDist) minDist = dist;
            }
            return minDist;
        }
    }
}
