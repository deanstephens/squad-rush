using UnityEngine;
using UnityEngine.SceneManagement;

namespace SquadRush
{
    public enum GameState
    {
        Menu,
        Playing,
        LevelClear,
        PerkChoice,
        GameOver,
    }

    /// <summary>Owns run state, the treadmill speed curve, coins and the perk/game-over flow.</summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        /// <summary>Set by "Retry" so the reloaded scene skips the menu.</summary>
        static bool autoStartNextLoad;

        [Header("Refs")]
        public Squad squad;
        public LevelDirector director;
        public GameUI ui;

        [Header("Treadmill")]
        public float baseSpeed = 6f;
        public float speedPerLevel = 0.35f;
        public float speedPerMeter = 0.004f;
        public float maxSpeed = 12f;
        [HideInInspector] public float treadmillSpeedMultiplier = 1f;

        public GameState State { get; private set; } = GameState.Menu;
        public float Distance { get; private set; }
        /// <summary>Distance into the current level; the boss arrives at the director's LevelLength.</summary>
        public float LevelDistance { get; private set; }
        public int Level { get; private set; } = 1;
        public int CoinsThisRun { get; private set; }
        public int ScrapThisRun { get; private set; }

        void Awake()
        {
            Instance = this;
            Application.targetFrameRate = 60;
            Time.timeScale = 1f;
            Treadmill.Reset();
        }

        void Start()
        {
            squad.ApplyMeta();
            if (autoStartNextLoad)
            {
                autoStartNextLoad = false;
                StartRun();
            }
            else
            {
                State = GameState.Menu;
                ui.ShowMenu();
            }
        }

        void Update()
        {
            if (State != GameState.Playing) return;

            Treadmill.Speed = Mathf.Min(maxSpeed, baseSpeed + (Level - 1) * speedPerLevel + LevelDistance * speedPerMeter) * treadmillSpeedMultiplier;
            float step = Treadmill.Speed * Time.deltaTime;
            Distance += step;
            if (!director.BossSpawned) LevelDistance += step;
            ui.UpdateHud();
        }

        public void StartRun()
        {
            if (State == GameState.Playing) return;
            State = GameState.Playing;
            Distance = 0f;
            LevelDistance = 0f;
            Level = MetaProgression.CurrentLevel;
            CoinsThisRun = 0;
            ScrapThisRun = 0;
            Treadmill.Speed = baseSpeed;
            Treadmill.Running = true;
            director.BeginLevel(Level);
            ui.ShowHud();
        }

        public void AddCoins(int n)
        {
            if (State != GameState.Playing) return;
            CoinsThisRun += n;
        }

        public void AddScrap(int n)
        {
            if (State != GameState.Playing) return;
            ScrapThisRun += n;
        }

        /// <summary>Boss down: bank the level's earnings, save progress, show the clear screen.</summary>
        public void OnBossKilled()
        {
            if (State != GameState.Playing) return;
            State = GameState.LevelClear;
            Treadmill.Running = false;

            ScrapThisRun += 5 + Level * 2;
            int coins = CoinsThisRun, scrap = ScrapThisRun;
            MetaProgression.AddCoins(coins);
            MetaProgression.AddScrap(scrap);
            MetaProgression.RecordDistance(Distance);
            CoinsThisRun = 0;
            ScrapThisRun = 0;
            MetaProgression.CurrentLevel = Level + 1;

            director.ClearLane();
            ui.ShowLevelClear(Level, coins, scrap);
        }

        /// <summary>From the clear screen: pick a perk, then the next level starts.</summary>
        public void ContinueToNextLevel()
        {
            if (State != GameState.LevelClear) return;
            State = GameState.PerkChoice;
            Time.timeScale = 0f;
            ui.ShowPerkChoice(PerkLibrary.RollThree());
        }

        public void ChoosePerk(Perk perk)
        {
            if (State != GameState.PerkChoice) return;
            perk.Apply(this);
            squad.NotifyChanged();
            Time.timeScale = 1f;

            Level++;
            LevelDistance = 0f;
            director.BeginLevel(Level);
            Treadmill.Running = true;
            State = GameState.Playing;
            ui.ShowHud();
        }

        public void GameOver()
        {
            if (State == GameState.GameOver) return;
            State = GameState.GameOver;
            Treadmill.Running = false;
            Time.timeScale = 1f;

            ScrapThisRun += Mathf.FloorToInt(Distance / 25f);
            MetaProgression.AddCoins(CoinsThisRun);
            MetaProgression.AddScrap(ScrapThisRun);
            MetaProgression.RecordDistance(Distance);
            ui.ShowGameOver(Level, Distance, CoinsThisRun, ScrapThisRun, MetaProgression.BestDistance);
        }

        public void Retry()
        {
            autoStartNextLoad = true;
            Reload();
        }

        public void OpenArena()
        {
            Time.timeScale = 1f;
            Treadmill.Reset();
            SceneManager.LoadScene("Arena");
        }

        public void BackToMenu()
        {
            autoStartNextLoad = false;
            Reload();
        }

        void Reload()
        {
            Time.timeScale = 1f;
            Treadmill.Reset();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
