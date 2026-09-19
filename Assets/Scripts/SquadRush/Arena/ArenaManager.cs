using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SquadRush.Arena
{
    public enum ArenaState
    {
        Loadout,
        Playing,
        LevelUp,
        GameOver,
    }

    /// <summary>Owns the arena run: loadout, timer, XP and level-ups, bullet pool, results.</summary>
    public class ArenaManager : MonoBehaviour
    {
        public static ArenaManager Instance { get; private set; }
        static bool autoStartNextLoad;

        [Header("Refs")]
        public ArenaPlayer player;
        public EnemySpawner spawner;
        public ArenaUI ui;
        public ArenaBullet bulletPrefab;
        public XpGem gemPrefab;
        public Material debrisMaterial;

        [Header("Leveling")]
        public float firstLevelXp = 8f;
        public float xpGrowthPerLevel = 5f;

        public ArenaState State { get; private set; } = ArenaState.Loadout;
        public float TimeSurvived { get; private set; }
        public int Kills { get; private set; }
        public int CoinsThisRun { get; private set; }
        public int Level { get; private set; } = 1;
        public float Xp { get; private set; }
        public float XpToNext { get; private set; }

        readonly Stack<ArenaBullet> bulletPool = new Stack<ArenaBullet>();
        readonly Dictionary<string, int> taken = new Dictionary<string, int>();
        Transform bulletRoot, gemRoot;
        ArenaUpgrade[] offered;

        public int TimesTaken(string id) => taken.TryGetValue(id, out var n) ? n : 0;

        void Awake()
        {
            Instance = this;
            Application.targetFrameRate = 60;
            Time.timeScale = 1f;
            Enemy.All.Clear();
            Fx.DebrisMaterial = debrisMaterial;
            bulletRoot = new GameObject("Bullets").transform;
            gemRoot = new GameObject("Gems").transform;
        }

        void Start()
        {
            if (autoStartNextLoad)
            {
                autoStartNextLoad = false;
                StartRun();
            }
            else
            {
                State = ArenaState.Loadout;
                ui.ShowLoadout();
            }
        }

        void Update()
        {
            if (State != ArenaState.Playing) return;
            TimeSurvived += Time.deltaTime;
            ui.UpdateHud();
        }

        // ---------------------------------------------------------------- run flow

        public void StartRun()
        {
            if (State == ArenaState.Playing) return;

            var def = GunLibrary.Get(MetaProgression.SelectedGun) ?? GunLibrary.Get(GunLibrary.DefaultGunId);
            var stats = GunLibrary.BuildMetaStats(def);
            taken.Clear();

            TimeSurvived = 0f;
            Kills = 0;
            CoinsThisRun = 0;
            Level = 1;
            Xp = 0f;
            XpToNext = firstLevelXp;

            player.hp = player.maxHp;
            player.EquipGun(def, stats);
            spawner.Begin(player);

            State = ArenaState.Playing;
            Time.timeScale = 1f;
            ui.ShowHud();
        }

        public void OnEnemyKilled(int coins)
        {
            if (State != ArenaState.Playing) return;
            Kills++;
            CoinsThisRun += coins;
        }

        public void SpawnGem(Vector3 pos, float value)
        {
            if (gemPrefab == null) return;
            var g = Instantiate(gemPrefab, new Vector3(pos.x, 0.35f, pos.z), Quaternion.identity, gemRoot);
            g.value = value;
            g.transform.localScale = Vector3.one * (value >= 20f ? 0.6f : value >= 5f ? 0.45f : 0.3f);
        }

        public void AddXp(float v)
        {
            if (State != ArenaState.Playing) return;
            Xp += v;
            if (Xp >= XpToNext)
            {
                Xp -= XpToNext;
                Level++;
                XpToNext = firstLevelXp + xpGrowthPerLevel * (Level - 1);
                offered = ArenaUpgrades.RollThree(this);
                State = ArenaState.LevelUp;
                Time.timeScale = 0f;
                ui.ShowLevelUp(offered);
            }
            ui.UpdateHud();
        }

        public void ChooseUpgrade(int index)
        {
            if (State != ArenaState.LevelUp || offered == null || index < 0 || index >= offered.Length) return;
            var u = offered[index];
            u.Apply(this);
            taken[u.Id] = TimesTaken(u.Id) + 1;
            offered = null;
            Time.timeScale = 1f;
            State = ArenaState.Playing;
            ui.ShowHud();
        }

        public void NotifyHud() => ui.UpdateHud();

        public void GameOver()
        {
            if (State == ArenaState.GameOver) return;
            State = ArenaState.GameOver;
            Time.timeScale = 1f;

            MetaProgression.AddCoins(CoinsThisRun);
            MetaProgression.RecordArenaTime(TimeSurvived);
            ui.ShowGameOver(TimeSurvived, Kills, CoinsThisRun, MetaProgression.BestArenaTime);
        }

        public void Retry()
        {
            autoStartNextLoad = true;
            Reload();
        }

        public void BackToArmory()
        {
            autoStartNextLoad = false;
            Reload();
        }

        public void BackToHub()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("Game");
        }

        void Reload()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        // ---------------------------------------------------------------- bullet pool

        public ArenaBullet GetBullet()
        {
            if (bulletPool.Count > 0) return bulletPool.Pop();
            return Instantiate(bulletPrefab, bulletRoot);
        }

        public void ReturnBullet(ArenaBullet b) => bulletPool.Push(b);

        public static string FormatTime(float seconds)
        {
            int s = Mathf.FloorToInt(seconds);
            return (s / 60).ToString("0") + ":" + (s % 60).ToString("00");
        }
    }
}
