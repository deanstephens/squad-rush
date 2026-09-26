using System.Collections.Generic;
using UnityEngine;

namespace SquadRush
{
    /// <summary>Names of the generated clips in Assets/Audio (see Tools/audio/generate_sfx.py).</summary>
    public static class SfxId
    {
        public const string UiClick = "ui_click", Purchase = "purchase", PerkPick = "perk_pick", GameOver = "game_over";
        public const string SquadShot = "squad_shot", EnemyHit = "enemy_hit", EnemyDie = "enemy_die";
        public const string GateGood = "gate_good", GateBad = "gate_bad", UnitLost = "unit_lost";
        public const string BossSpawn = "boss_spawn", BossBite = "boss_bite", LevelClear = "level_clear";
        public const string Explosion = "explosion", XpPickup = "xp_pickup", LevelUp = "level_up", PlayerHurt = "player_hurt";
        public const string WaveStart = "wave_start", WaveClear = "wave_clear", BossHorn = "boss_horn", Enrage = "enrage";
        public static string Gun(string gunId) => "gun_" + gunId;
    }

    /// <summary>
    /// One per scene. Pooled 2D sources, per-clip rate limiting (a 30-unit volley or a minigun must not stack
    /// hundreds of voices), pitch jitter, a looping music track and a persisted mute toggle.
    /// </summary>
    public class AudioHub : MonoBehaviour
    {
        public static AudioHub Instance { get; private set; }
        const string MuteKey = "sr_muted";

        public AudioClip[] clips;
        public AudioClip music;
        [Range(0f, 1f)] public float musicVolume = 0.35f;
        [Range(0f, 1f)] public float sfxVolume = 0.8f;
        public int voices = 16;
        [Tooltip("Minimum seconds between two plays of the same clip.")]
        public float minInterval = 0.045f;

        readonly Dictionary<string, AudioClip> byName = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
        AudioSource[] pool;
        AudioSource musicSource;
        int next;

        public static bool Muted
        {
            get => PlayerPrefs.GetInt(MuteKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(MuteKey, value ? 1 : 0);
                PlayerPrefs.Save();
                AudioListener.volume = value ? 0f : 1f;
            }
        }

        void Awake()
        {
            Instance = this;
            AudioListener.volume = Muted ? 0f : 1f;
            foreach (var c in clips)
                if (c != null) byName[c.name] = c;

            pool = new AudioSource[voices];
            for (int i = 0; i < voices; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                pool[i] = s;
            }
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.volume = musicVolume;
        }

        void Start()
        {
            if (music != null)
            {
                musicSource.clip = music;
                musicSource.Play();
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void PlayClip(string name, float volume, float pitchJitter, float pitch)
        {
            if (!byName.TryGetValue(name, out var clip)) return;
            float now = Time.unscaledTime;
            if (lastPlayed.TryGetValue(name, out var last) && now - last < minInterval) return;
            lastPlayed[name] = now;

            var src = pool[next];
            next = (next + 1) % pool.Length;
            src.pitch = pitch * (1f + Random.Range(-pitchJitter, pitchJitter));
            src.PlayOneShot(clip, volume * sfxVolume);
        }

        public void SetMusicVolume(float v)
        {
            musicVolume = v;
            if (musicSource != null) musicSource.volume = v;
        }
    }

    /// <summary>Static front door so gameplay code does not need a reference to the hub.</summary>
    public static class Sfx
    {
        public static void Play(string name, float volume = 1f, float pitchJitter = 0.06f, float pitch = 1f)
        {
            var hub = AudioHub.Instance;
            if (hub != null) hub.PlayClip(name, volume, pitchJitter, pitch);
        }
    }
}
