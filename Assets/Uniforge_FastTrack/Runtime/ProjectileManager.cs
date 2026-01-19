using UnityEngine;

namespace Uniforge.FastTrack.Runtime
{
    public class ProjectileManager : MonoBehaviour
    {
        [SerializeField] private GameObject _defaultProjectilePrefab;

        public GameObject SpawnProjectile(string prefabId, Vector3 position, Vector3 direction, float speed)
        {
            var projectile = PrefabRegistry.SpawnStatic(prefabId, position);
            if (projectile != null)
            {
                var rb = projectile.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.linearVelocity = direction.normalized * speed;
                }
            }
            return projectile;
        }

        public static GameObject SpawnProjectileStatic(string prefabId, Vector3 position, Vector3 direction, float speed)
        {
            if (UniforgeRuntime.Instance?.Projectiles != null)
                return UniforgeRuntime.Instance.Projectiles.SpawnProjectile(prefabId, position, direction, speed);
            return null;
        }

        /// <summary>
        /// Fires a projectile in a direction with specified speed and damage.
        /// Called by generated FireProjectile action code.
        /// </summary>
        public static void FireDirectionStatic(Vector3 position, Vector3 direction, float speed, float damage)
        {
            Debug.Log($"[ProjectileManager] FireDirectionStatic: pos={position}, dir={direction}, speed={speed}, damage={damage}");
            
            // Create a simple projectile GameObject
            var projectileGO = new GameObject("Projectile");
            projectileGO.transform.position = position;
            
            // Add components
            var sr = projectileGO.AddComponent<SpriteRenderer>();
            sr.color = Color.yellow;
            sr.sortingOrder = 100;
            
            var rb = projectileGO.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0;
            rb.linearVelocity = direction.normalized * speed;
            
            var collider = projectileGO.AddComponent<CircleCollider2D>();
            collider.radius = 0.1f;
            collider.isTrigger = true;
            
            // Add projectile behavior
            var proj = projectileGO.AddComponent<ProjectileBehavior>();
            proj.Damage = damage;
            proj.Lifetime = 5f;
        }
    }

    /// <summary>
    /// Simple projectile behavior component.
    /// Deals damage on collision and destroys after lifetime.
    /// </summary>
    public class ProjectileBehavior : MonoBehaviour
    {
        public float Damage = 10f;
        public float Lifetime = 5f;
        
        private float _spawnTime;

        void Start()
        {
            _spawnTime = Time.time;
        }

        void Update()
        {
            if (Time.time > _spawnTime + Lifetime)
            {
                Destroy(gameObject);
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            // Don't hit the shooter (assumes projectile spawns inside or near shooter)
            if (Time.time < _spawnTime + 0.1f) return;
            
            other.SendMessage("OnTakeDamage", Damage, SendMessageOptions.DontRequireReceiver);
            ParticleManager.PlayStatic("hit_spark", transform.position, 0.5f);
            Destroy(gameObject);
        }
    }
}
