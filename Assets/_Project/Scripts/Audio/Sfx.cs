using System.Collections.Generic;
using RedZone.Core;
using UnityEngine;

namespace RedZone.Audio
{
    /// <summary>
    /// Sounds are AudioClips under Audio/Resources (Sfx/, Music/) loaded by name (the Wilkins Pest Control model).
    /// A missing clip logs once and stays silent. Music has two tracks that crossfade (menu, game).
    /// </summary>
    public static class Sfx
    {
        static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
        static readonly HashSet<string> missing = new HashSet<string>();
        static readonly Dictionary<AudioClip, float> lastStart = new Dictionary<AudioClip, float>();
        static readonly List<AudioSource> pool = new List<AudioSource>();
        static GameObject root;
        static AudioSource musicA, musicB;
        static string currentMusic;
        static float musicFade = 1f, duck = 1f;

        public static AudioClip Clip(string path)
        {
            if (cache.TryGetValue(path, out var c)) return c;
            c = Resources.Load<AudioClip>(path);
            if (c == null && missing.Add(path)) Debug.LogWarning("[Sfx] missing clip " + path);
            cache[path] = c;
            return c;
        }

        static void EnsureRoot()
        {
            if (root != null) return;
            root = new GameObject("[Sfx]");
            Object.DontDestroyOnLoad(root);
            root.AddComponent<MusicDriver>();
            musicA = NewSource("music A"); musicB = NewSource("music B");
            musicA.loop = musicB.loop = true;
        }

        static AudioSource NewSource(string name)
        {
            var go = new GameObject(name); go.transform.SetParent(root.transform, false);
            var s = go.AddComponent<AudioSource>(); s.playOnAwake = false; s.spatialBlend = 0f;
            return s;
        }

        static AudioSource Source()
        {
            EnsureRoot();
            foreach (var s in pool) if (s && !s.isPlaying) return s;
            if (pool.Count >= 24) return pool[0];
            var src = NewSource("voice " + pool.Count);
            pool.Add(src);
            return src;
        }

        public static void Play(string name, float volume = 1f, float pitch = 1f)
        {
            var clip = Clip("Sfx/" + name);
            if (clip == null) return;
            float now = Time.unscaledTime;
            if (lastStart.TryGetValue(clip, out var t) && now - t < 0.04f) return;
            lastStart[clip] = now;
            var s = Source();
            s.pitch = pitch;
            s.volume = volume * Settings.EffectsVol;
            s.clip = clip;
            s.Play();
        }

        /// <summary>Starts (or crossfades to) a music loop by name under Music/.</summary>
        public static void Music(string name)
        {
            EnsureRoot();
            if (currentMusic == name) return;
            currentMusic = name;
            var clip = Clip("Music/" + name);
            // the idle source takes the new track; MusicDriver fades the other one out
            var tmp = musicA; musicA = musicB; musicB = tmp;
            musicA.clip = clip;
            musicA.volume = 0f;
            if (clip) musicA.Play();
            musicFade = 0f;
        }

        /// <summary>0..1 dip of the music (pause screen, results).</summary>
        public static void Duck(float level) => duck = Mathf.Clamp01(level);

        sealed class MusicDriver : MonoBehaviour
        {
            float duckNow = 1f;
            void Update()
            {
                musicFade = Mathf.MoveTowards(musicFade, 1f, Time.unscaledDeltaTime / 1.2f);
                duckNow = Mathf.MoveTowards(duckNow, duck, Time.unscaledDeltaTime * 2f);
                float v = Settings.MusicVol * 0.7f * duckNow;
                if (musicA) musicA.volume = v * musicFade;
                if (musicB)
                {
                    musicB.volume = v * (1f - musicFade);
                    if (musicFade >= 1f && musicB.isPlaying) musicB.Stop();
                }
            }
        }
    }
}
