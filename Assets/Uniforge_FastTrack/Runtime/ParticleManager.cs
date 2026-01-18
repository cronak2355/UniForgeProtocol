using UnityEngine;
using System.Collections.Generic;

namespace Uniforge.FastTrack.Runtime
{
    /// <summary>
    /// Manages particle effects for UniForge games.
    /// Supports built-in presets and custom particle assets.
    /// </summary>
    public class ParticleManager : MonoBehaviour
    {
        [Header("Particle Pool")]
        public int PoolSize = 20;

        private Dictionary<string, ParticleSystem> _prefabCache = new Dictionary<string, ParticleSystem>();
        private Queue<ParticleSystem> _pool = new Queue<ParticleSystem>();

        void Awake()
        {
            // Pre-warm pool with generic particles
            for (int i = 0; i < PoolSize; i++)
            {
                var ps = CreateGenericParticle();
                ps.gameObject.SetActive(false);
                _pool.Enqueue(ps);
            }
        }

        /// <summary>
        /// Play a particle effect at position.
        /// </summary>
        public void Play(string preset, Vector3 position, float scale = 1f)
        {
            var ps = GetParticle();
            ps.transform.position = position;
            ps.transform.localScale = Vector3.one * scale;

            ConfigurePreset(ps, preset);

            ps.gameObject.SetActive(true);
            ps.Play();

            // Auto return to pool
            StartCoroutine(ReturnToPoolAfter(ps, ps.main.duration + ps.main.startLifetime.constantMax));
        }

        /// <summary>
        /// Static shortcut for generated scripts.
        /// </summary>
        public static void PlayStatic(string preset, Vector3 position, float scale = 1f)
        {
            if (UniforgeRuntime.Instance?.Particles != null)
                UniforgeRuntime.Instance.Particles.Play(preset, position, scale);
            else
                Debug.LogWarning($"[ParticleManager] Runtime not initialized. Cannot play: {preset}");
        }

        private ParticleSystem GetParticle()
        {
            if (_pool.Count > 0)
            {
                var ps = _pool.Dequeue();
                ps.gameObject.SetActive(true);
                return ps;
            }
            return CreateGenericParticle();
        }

        private ParticleSystem CreateGenericParticle()
        {
            var go = new GameObject("UniforgeParticle");
            go.transform.SetParent(transform);
            var ps = go.AddComponent<ParticleSystem>();
            
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 0.5f;
            main.startLifetime = 0.5f;
            main.startSpeed = 3f;
            main.startSize = 0.1f;
            main.startColor = Color.white;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 10) });

            return ps;
        }

        private void ConfigurePreset(ParticleSystem ps, string preset)
        {
            var main = ps.main;
            var emission = ps.emission;

            switch (preset)
            {
                case "hit_spark":
                    main.startColor = new Color(1f, 0.9f, 0.3f);
                    main.startSpeed = 5f;
                    main.startSize = 0.05f;
                    emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 15) });
                    break;

                case "explosion":
                    main.startColor = new Color(1f, 0.5f, 0f);
                    main.startSpeed = 8f;
                    main.startSize = 0.2f;
                    main.startLifetime = 0.8f;
                    emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 30) });
                    break;

                case "blood":
                    main.startColor = new Color(0.8f, 0.1f, 0.1f);
                    main.startSpeed = 4f;
                    main.startSize = 0.08f;
                    main.gravityModifier = 1f;
                    emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 20) });
                    break;

                case "heal":
                    main.startColor = new Color(0.3f, 1f, 0.5f);
                    main.startSpeed = 2f;
                    main.startSize = 0.15f;
                    main.gravityModifier = -0.5f;
                    emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });
                    break;

                case "magic":
                    main.startColor = new Color(0.6f, 0.3f, 1f);
                    main.startSpeed = 3f;
                    main.startSize = 0.12f;
                    emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 25) });
                    break;

                case "sparkle":
                    main.startColor = Color.white;
                    main.startSpeed = 1f;
                    main.startSize = 0.05f;
                    main.startLifetime = 1f;
                    emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 8) });
                    break;

                case "dust":
                    main.startColor = new Color(0.6f, 0.5f, 0.4f, 0.5f);
                    main.startSpeed = 1f;
                    main.startSize = 0.15f;
                    emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 5) });
                    break;

                default:
                    // Check for custom particle (custom:assetId format)
                    if (preset.StartsWith("custom:"))
                    {
                        string assetId = preset.Substring(7);
                        LoadCustomParticle(ps, assetId);
                    }
                    break;
            }
        }

        private void LoadCustomParticle(ParticleSystem ps, string assetId)
        {
            // TODO: Load custom particle texture from asset registry
            Debug.Log($"[ParticleManager] Custom particle: {assetId}");
        }

        private System.Collections.IEnumerator ReturnToPoolAfter(ParticleSystem ps, float delay)
        {
            yield return new WaitForSeconds(delay);
            ps.Stop();
            ps.gameObject.SetActive(false);
            _pool.Enqueue(ps);
        }
    }
}
