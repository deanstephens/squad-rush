using System.Collections.Generic;
using UnityEngine;

namespace SquadRush.Arena
{
    public enum WavePhase
    {
        Idle,
        Countdown,
        Active,
        Boss,
    }

    /// <summary>
    /// Wave-based arena spawning. Each wave bursts in its roster, then the arena stays quiet until the wave
    /// is cleared (or the cap expires). Every fifth wave is a boss: the next wave waits for it to die, and it
    /// enrages after a while, so it is a damage check rather than a slog.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        public Enemy enemyPrefab;
        public float spawnRadius = 19f;
        public float arenaHalfSize = 29f;
        public int maxAlive = 220;

        [Header("Waves")]
        public float firstCountdown = 3f;
        public float betweenWaves = 3f;
        public float waveTimeCap = 45f;
        public int baseWaveSize = 6;
        public int sizePerWave = 3;
        public float hpPerWave = 0.12f;
        public float spawnSpacing = 0.15f;

        [Header("Boss waves")]
        public int bossEvery = 5;
        public float bossHpPerBoss = 0.7f;
        public float bossEnrageAfter = 30f;
        public int bossEscorts = 4;

        public WavePhase Phase { get; private set; } = WavePhase.Idle;
        public int WaveNumber { get; set; }
        public float PhaseTimer { get; private set; }
        public bool NextIsBoss => (WaveNumber + 1) % bossEvery == 0;
        public Enemy CurrentBoss { get; private set; }
        public float EnrageIn => CurrentBoss != null ? Mathf.Max(0f, bossEnrageAfter - PhaseTimer) : 0f;
        public int Remaining => Enemy.All.Count;

        readonly Queue<EnemyDef> spawnQueue = new Queue<EnemyDef>();
        float spawnTimer;
        float waveHpMult = 1f;
        bool bossEnraged;
        Transform root;
        ArenaPlayer player;

        public void Begin(ArenaPlayer player)
        {
            this.player = player;
            if (root == null) root = new GameObject("Enemies").transform;
            foreach (Transform t in root) Destroy(t.gameObject);
            spawnQueue.Clear();
            WaveNumber = 0;
            CurrentBoss = null;
            Phase = WavePhase.Countdown;
            PhaseTimer = firstCountdown;
        }

        void Update()
        {
            var am = ArenaManager.Instance;
            if (am == null || am.State != ArenaState.Playing || player == null) return;
            float dt = Time.deltaTime;

            switch (Phase)
            {
                case WavePhase.Countdown:
                    PhaseTimer -= dt;
                    if (PhaseTimer <= 0f) StartWave(WaveNumber + 1);
                    break;

                case WavePhase.Active:
                    PhaseTimer += dt;
                    PumpQueue(dt);
                    if (spawnQueue.Count == 0 && Remaining == 0) EndWave();
                    else if (PhaseTimer >= waveTimeCap) EndWave();   // stragglers do not stall the run
                    break;

                case WavePhase.Boss:
                    PhaseTimer += dt;
                    PumpQueue(dt);
                    if (CurrentBoss == null || CurrentBoss.Dead)
                    {
                        CurrentBoss = null;
                        EndWave();
                    }
                    else if (!bossEnraged && PhaseTimer >= bossEnrageAfter)
                    {
                        bossEnraged = true;
                        CurrentBoss.Enrage();
                    }
                    break;
            }
        }

        void StartWave(int n)
        {
            WaveNumber = n;
            PhaseTimer = 0f;
            spawnQueue.Clear();
            waveHpMult = 1f + (n - 1) * hpPerWave;
            bossEnraged = false;

            if (n % bossEvery == 0)
            {
                Phase = WavePhase.Boss;
                int bossIndex = n / bossEvery;
                float bossMult = waveHpMult * (1f + (bossIndex - 1) * bossHpPerBoss);
                CurrentBoss = SpawnOne(EnemyLibrary.Elite, bossMult);
                for (int i = 0; i < bossEscorts + bossIndex; i++) spawnQueue.Enqueue(EnemyLibrary.Grunt);
                return;
            }

            Phase = WavePhase.Active;
            int size = baseWaveSize + (n - 1) * sizePerWave;
            for (int i = 0; i < size; i++) spawnQueue.Enqueue(RollType(n));
        }

        void EndWave()
        {
            Phase = WavePhase.Countdown;
            PhaseTimer = betweenWaves;
            spawnQueue.Clear();
        }

        void PumpQueue(float dt)
        {
            spawnTimer -= dt;
            while (spawnTimer <= 0f && spawnQueue.Count > 0)
            {
                spawnTimer += spawnSpacing;
                SpawnOne(spawnQueue.Dequeue(), waveHpMult);
            }
        }

        EnemyDef RollType(int wave)
        {
            float r = Random.value;
            if (wave >= 5 && r < 0.08f + wave * 0.012f) return EnemyLibrary.Brute;
            if (wave >= 3 && r < 0.45f) return EnemyLibrary.Runner;
            return EnemyLibrary.Grunt;
        }

        Enemy SpawnOne(EnemyDef def, float hpMult)
        {
            if (Enemy.All.Count >= maxAlive) return null;

            Vector2 dir = Random.insideUnitCircle.normalized;
            Vector3 pos = player.transform.position + new Vector3(dir.x, 0f, dir.y) * (spawnRadius + Random.Range(0f, 3f));
            pos.x = Mathf.Clamp(pos.x, -arenaHalfSize, arenaHalfSize);
            pos.z = Mathf.Clamp(pos.z, -arenaHalfSize, arenaHalfSize);
            pos.y = 0f;

            var e = Instantiate(enemyPrefab, pos, Quaternion.identity, root);
            e.Setup(def, hpMult, player);
            return e;
        }
    }
}
