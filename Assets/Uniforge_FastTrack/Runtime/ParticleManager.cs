using UnityEngine;
using System.Collections.Generic;

namespace Uniforge.FastTrack.Runtime
{
    public class ParticleManager : MonoBehaviour
    {
        [Header("Particle Pool")]
        public int PoolSize = 20;

        private Dictionary<string, ParticleSystem> _prefabCache = new Dictionary<string, ParticleSystem>();
        private Queue<ParticleSystem> _pool = new Queue<ParticleSystem>();

        void Awake()
        {
            for (int i = 0; i < PoolSize; i++)
            {
                var ps = CreateGenericParticle();
                ps.gameObject.SetActive(false);
                _pool.Enqueue(ps);
            }
        }

        public void Play(string preset, Vector3 position, float scale = 1f)
        {
            // Debug.Log($"[ParticleManager] Playing particle '{preset}' at {position}");
            var ps = GetParticle();
            ps.transform.position = position;
            ps.transform.localScale = Vector3.one * scale;
            ConfigurePreset(ps, preset);
            ps.gameObject.SetActive(true);
            ps.Play();
            StartCoroutine(ReturnToPoolAfter(ps, ps.main.duration + ps.main.startLifetime.constantMax));
        }

        public static void PlayStatic(string preset, Vector3 position, float scale = 1f)
        {
            // Ensure runtime exists
            if (UniforgeRuntime.Instance == null)
            {
                UniforgeRuntime.EnsureExists();
            }
            
            if (UniforgeRuntime.Instance?.Particles != null)
            {
                // Debug.Log($"[ParticleManager] PlayStatic: {preset} at {position}");
                UniforgeRuntime.Instance.Particles.Play(preset, position, scale);
            }
            else
            {
                Debug.LogWarning($"[ParticleManager] Cannot play '{preset}' - Particles manager is null");
            }
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
            
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            
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

            // Set default material to prevent pink/purple particles
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.material = new Material(Shader.Find("Sprites/Default"));
                renderer.sortingOrder = 2000;
            }

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
                default:
                    break;
            }
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
