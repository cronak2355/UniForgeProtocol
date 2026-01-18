using UnityEngine;
using System.Collections.Generic;

namespace Uniforge.FastTrack.Runtime
{
    /// <summary>
    /// Manages audio playback for UniForge games.
    /// Supports both one-shot and looping sounds.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        [Header("Settings")]
        public int MaxConcurrentSounds = 8;

        private AudioSource[] _sources;
        private Dictionary<string, AudioClip> _clipCache = new Dictionary<string, AudioClip>();
        private int _nextSourceIndex = 0;

        void Awake()
        {
            _sources = new AudioSource[MaxConcurrentSounds];
            for (int i = 0; i < MaxConcurrentSounds; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                _sources[i] = source;
            }
        }

        /// <summary>
        /// Play a sound by ID. Loads from Resources/Sounds/{soundId}
        /// </summary>
        public void Play(string soundId, float volume = 1f, float pitch = 1f)
        {
            var clip = GetClip(soundId);
            if (clip == null)
            {
                Debug.LogWarning($"[AudioManager] Sound not found: {soundId}");
                return;
            }

            var source = GetNextSource();
            source.clip = clip;
            source.volume = volume;
            source.pitch = pitch;
            source.loop = false;
            source.Play();
        }

        /// <summary>
        /// Play a sound at a specific world position.
        /// </summary>
        public void PlayAtPosition(string soundId, Vector3 position, float volume = 1f)
        {
            var clip = GetClip(soundId);
            if (clip == null)
            {
                Debug.LogWarning($"[AudioManager] Sound not found: {soundId}");
                return;
            }
            AudioSource.PlayClipAtPoint(clip, position, volume);
        }

        /// <summary>
        /// Static shortcut for generated scripts.
        /// </summary>
        public static void PlayStatic(string soundId)
        {
            if (UniforgeRuntime.Instance?.Audio != null)
                UniforgeRuntime.Instance.Audio.Play(soundId);
            else
                Debug.LogWarning($"[AudioManager] Runtime not initialized. Cannot play: {soundId}");
        }

        private AudioClip GetClip(string soundId)
        {
            if (_clipCache.TryGetValue(soundId, out var cached))
                return cached;

            var clip = Resources.Load<AudioClip>($"Sounds/{soundId}");
            if (clip != null)
                _clipCache[soundId] = clip;
            return clip;
        }

        private AudioSource GetNextSource()
        {
            var source = _sources[_nextSourceIndex];
            _nextSourceIndex = (_nextSourceIndex + 1) % MaxConcurrentSounds;
            return source;
        }
    }
}
