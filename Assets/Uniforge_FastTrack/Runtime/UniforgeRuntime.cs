using UnityEngine;
using System;
using System.Collections.Generic;

namespace Uniforge.FastTrack.Runtime
{
    /// <summary>
    /// Central runtime manager for UniForge games.
    /// Provides singleton access to all runtime services.
    /// </summary>
    public class UniforgeRuntime : MonoBehaviour
    {
        public static UniforgeRuntime Instance { get; private set; }

        [Header("Runtime Services")]
        public AudioManager Audio;
        public ParticleManager Particles;
        public PrefabRegistry Prefabs;
        public ProjectileManager Projectiles;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Initialize services if not assigned
            if (Audio == null) Audio = GetComponentInChildren<AudioManager>() ?? gameObject.AddComponent<AudioManager>();
            if (Particles == null) Particles = GetComponentInChildren<ParticleManager>() ?? gameObject.AddComponent<ParticleManager>();
            if (Prefabs == null) Prefabs = GetComponentInChildren<PrefabRegistry>() ?? gameObject.AddComponent<PrefabRegistry>();
            if (Projectiles == null) Projectiles = GetComponentInChildren<ProjectileManager>() ?? gameObject.AddComponent<ProjectileManager>();
        }

        public static void EnsureExists()
        {
            if (Instance == null)
            {
                var go = new GameObject("[UniforgeRuntime]");
                var runtime = go.AddComponent<UniforgeRuntime>();
                // Force initialization immediately
                if (runtime.Particles == null) runtime.Particles = go.AddComponent<ParticleManager>();
                Instance = runtime;
            }
        }
    }

    public static class EventBus
    {
        private static Dictionary<string, Action<object>> _listeners = new Dictionary<string, Action<object>>();

        public static void Emit(string signalKey, object data = null)
        {
            if (_listeners.TryGetValue(signalKey, out var action))
            {
                action?.Invoke(data);
            }
        }

        public static void Subscribe(string signalKey, Action<object> callback)
        {
            if (!_listeners.ContainsKey(signalKey))
                _listeners[signalKey] = callback;
            else
                _listeners[signalKey] += callback;
        }

        public static void Unsubscribe(string signalKey, Action<object> callback)
        {
            if (_listeners.ContainsKey(signalKey))
                _listeners[signalKey] -= callback;
        }

        public static void Clear(string signalKey)
        {
            if (_listeners.ContainsKey(signalKey))
                _listeners.Remove(signalKey);
        }

        public static void ClearAll()
        {
            _listeners.Clear();
        }
    }

    public static class DialogueManager
    {
        public static event Action<string> OnDialogueShow;

        public static void Show(string text)
        {
            Debug.Log($"[Dialogue] {text}");
            OnDialogueShow?.Invoke(text);
        }
    }

    public static class CooldownManager
    {
        private static Dictionary<string, float> _cooldowns = new Dictionary<string, float>();

        /// <summary>
        /// Checks if an action is ready to use based on cooldown.
        /// Returns true if ready (and resets timer), false if cooling down.
        /// </summary>
        public static bool TryUse(string key, float duration)
        {
            if (duration <= 0) return true; // No cooldown

            if (!_cooldowns.TryGetValue(key, out float lastTime))
            {
                // First use: Always allow
                _cooldowns[key] = Time.time;
                return true;
            }

            if (Time.time - lastTime < duration)
            {
                return false; // Cooldown active
            }

            _cooldowns[key] = Time.time;
            return true;
        }

        public static void ClearAll()
        {
            _cooldowns.Clear();
        }
    }
}
