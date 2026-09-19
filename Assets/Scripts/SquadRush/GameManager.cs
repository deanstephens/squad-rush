using UnityEngine;
using UnityEngine.SceneManagement;

namespace SquadRush
{
    public enum GameState
    {
        Menu,
        Playing,
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
        public float speedPerMeter = 0.01f;
        public float maxSpeed = 13f;
        [HideInInspector] public float treadmillSpeedMultiplier = 1f;

        public GameState State { get; private set; } = GameState.Menu;
        public float Distance { get; private set; }
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

            Treadmill.Speed = Mathf.Min(maxSpeed, baseSpeed + Distance * speedPerMeter) * treadmillSpeedMultiplier;
            Distance += Treadmill.Speed * Time.deltaTime;
            ui.UpdateHud();
        }

        public void StartRun()
        {
            if (State == GameState.Playing) return;
            State = GameState.Playing;
            Distance = 0f;
            CoinsThisRun = 0;
            ScrapThisRun = 0;
            Treadmill.Speed = baseSpeed;
            Treadmill.Running = true;
            director.BeginRun();
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

        public void OnBossKilled()
        {
            if (State != GameState.Playing) return;
            State = GameState.PerkChoice;
            Treadmill.Running = false;
            Time.timeScale = 0f;
            ui.ShowPerkChoice(PerkLibrary.RollThree());
        }

        public void ChoosePerk(Perk perk)
        {
            if (State != GameState.PerkChoice) return;
            perk.Apply(this);
            squad.NotifyChanged();
            Time.timeScale = 1f;
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
            ui.ShowGameOver(Distance, CoinsThisRun, ScrapThisRun, MetaProgression.BestDistance);
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
