using UnityEngine;
using System.Collections.Generic;

namespace Uniforge.FastTrack.Runtime
{
    /// <summary>
    /// Manages projectile spawning and pooling for FireProjectile action.
    /// </summary>
    public class ProjectileManager : MonoBehaviour
    {
        [Header("Pool Settings")]
        public int PoolSize = 30;
        public GameObject DefaultProjectilePrefab;

        private Queue<Projectile> _pool = new Queue<Projectile>();

        void Awake()
        {
            // Create pool
            for (int i = 0; i < PoolSize; i++)
            {
                var proj = CreateProjectile();
                proj.gameObject.SetActive(false);
                _pool.Enqueue(proj);
            }
        }

        /// <summary>
        /// Fire a projectile towards closest enemy.
        /// </summary>
        public void Fire(Vector3 origin, string targetRole, float speed, float damage)
        {
            // Find closest target with role
            GameObject target = FindClosestWithRole(origin, targetRole);
            if (target == null)
            {
                Debug.Log($"[ProjectileManager] No target found with role: {targetRole}");
                return;
            }

            Vector3 direction = (target.transform.position - origin).normalized;
            Fire(origin, direction, speed, damage);
        }

        /// <summary>
        /// Fire a projectile in a specific direction.
        /// </summary>
        public void Fire(Vector3 origin, Vector3 direction, float speed, float damage)
        {
            var proj = GetProjectile();
            proj.transform.position = origin;
            proj.Initialize(direction, speed, damage);
            proj.gameObject.SetActive(true);
        }

        /// <summary>
        /// Static shortcut for generated scripts.
        /// </summary>
        public static void FireStatic(Vector3 origin, string targetRole, float speed, float damage)
        {
            if (UniforgeRuntime.Instance?.Projectiles != null)
                UniforgeRuntime.Instance.Projectiles.Fire(origin, targetRole, speed, damage);
            else
                Debug.LogWarning("[ProjectileManager] Runtime not initialized.");
        }

        public void ReturnToPool(Projectile proj)
        {
            proj.gameObject.SetActive(false);
            _pool.Enqueue(proj);
        }

        private Projectile GetProjectile()
        {
            if (_pool.Count > 0)
                return _pool.Dequeue();
            return CreateProjectile();
        }

        private Projectile CreateProjectile()
        {
            GameObject go;
            if (DefaultProjectilePrefab != null)
            {
                go = Instantiate(DefaultProjectilePrefab, transform);
            }
            else
            {
                // Create simple projectile
                go = new GameObject("Projectile");
                go.transform.SetParent(transform);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.color = Color.yellow;

                var collider = go.AddComponent<CircleCollider2D>();
                collider.radius = 0.1f;
                collider.isTrigger = true;

                var rb = go.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0;
            }

            var proj = go.GetComponent<Projectile>() ?? go.AddComponent<Projectile>();
            proj.Manager = this;
            return proj;
        }

        private GameObject FindClosestWithRole(Vector3 origin, string role)
        {
            // Optimized lookup using static registry
            var entities = UniforgeEntity.RegisteredEntities;
            GameObject closest = null;
            float closestDist = float.MaxValue;

            foreach (var entity in entities)
            {
                if (entity.Role == role)
                {
                    float dist = Vector3.Distance(origin, entity.transform.position);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closest = entity.gameObject;
                    }
                }
            }

            return closest;
        }
    }

    /// <summary>
    /// Projectile behavior component.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        public ProjectileManager Manager;
        public float Damage { get; private set; }
        public float Speed { get; private set; }
        public float MaxLifetime = 5f;

        private Vector3 _direction;
        private float _lifetime;

        public void Initialize(Vector3 direction, float speed, float damage)
        {
            _direction = direction.normalized;
            Speed = speed;
            Damage = damage;
            _lifetime = 0;
        }

        void Update()
        {
            transform.Translate(_direction * Speed * Time.deltaTime, Space.World);

            _lifetime += Time.deltaTime;
            if (_lifetime >= MaxLifetime)
            {
                Manager?.ReturnToPool(this);
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            // Deal damage
            other.SendMessage("OnTakeDamage", Damage, SendMessageOptions.DontRequireReceiver);

            // Return to pool
            Manager?.ReturnToPool(this);
        }
    }

    /// <summary>
    /// Component attached to imported entities for role-based targeting.
    /// </summary>
    public class UniforgeEntity : MonoBehaviour
    {
        public static readonly HashSet<UniforgeEntity> RegisteredEntities = new HashSet<UniforgeEntity>();

        public string EntityId;
        public string Role;
        public List<string> Tags = new List<string>();

        void OnEnable()
        {
            RegisteredEntities.Add(this);
        }

        void OnDisable()
        {
            RegisteredEntities.Remove(this);
        }

        /// <summary>
        /// Called when taking damage from projectiles or attacks.
        /// </summary>
        public void OnTakeDamage(float damage)
        {
            Debug.Log($"[{name}] Took {damage} damage");
            // Broadcast to generated scripts
            SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
        }
    }
}
