using UnityEngine;

namespace SquadRush
{
    /// <summary>
    /// Runs one treadmill level: streams enemy hordes and gate pairs for a set distance,
    /// then sends in the boss. Difficulty scales with the level number and distance into the level.
    /// </summary>
    public class LevelDirector : MonoBehaviour
    {
        [Header("Prefabs")]
        public TreadmillEnemy enemyPrefab;
        public PowerUpGate gatePrefab;
        public Material debrisMaterial;

        [Header("Layout")]
        public float spawnZ = 55f;
        public float chunkLength = 13f;
        public float laneHalfWidth = 2.6f;
        public int gateEvery = 3;

        [Header("Level length")]
        public float baseLevelLength = 220f;
        public float lengthPerLevel = 40f;
        public float maxLevelLength = 600f;

        [Header("Difficulty")]
        [Tooltip("Enemy HP multiplier added per level above 1.")]
        public float hpPerLevel = 0.22f;
        [Tooltip("Enemy HP multiplier added per metre into the level.")]
        public float hpPerMeter = 0.0008f;
        public float bossHpPerLevel = 0.45f;
        [Tooltip("Units the boss bites off per attack, plus this per level.")]
        public float bossBitePerLevel = 0.5f;

        public int Level { get; private set; } = 1;
        public float LevelLength { get; private set; }
        public bool BossSpawned { get; private set; }

        Transform root;
        float accumulator;
        int chunkIndex;
        bool running;

        void Awake()
        {
            Fx.DebrisMaterial = debrisMaterial;
            root = new GameObject("Lane").transform;
        }

        public void ClearLane()
        {
            foreach (Transform t in root) Destroy(t.gameObject);
        }

        public void BeginLevel(int level)
        {
            ClearLane();
            Level = Mathf.Max(1, level);
            LevelLength = Mathf.Min(maxLevelLength, baseLevelLength + lengthPerLevel * (Level - 1));
            chunkIndex = 0;
            accumulator = 0f;
            BossSpawned = false;
            running = true;

            SpawnGatePair(spawnZ * 0.45f);
            SpawnPack(spawnZ * 0.85f, 2, TreadmillEnemyLibrary.Grunt);
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (!running || gm == null || gm.State != GameState.Playing) return;

            if (BossSpawned) return;

            if (gm.LevelDistance >= LevelLength)
            {
                SpawnBoss();
                return;
            }

            accumulator += Treadmill.Delta;
            while (accumulator >= chunkLength)
            {
                accumulator -= chunkLength;
                SpawnChunk();
            }
        }

        float HpMult
        {
            get
            {
                float dist = GameManager.Instance != null ? GameManager.Instance.LevelDistance : 0f;
                return 1f + (Level - 1) * hpPerLevel + dist * hpPerMeter;
            }
        }

        void SpawnChunk()
        {
            chunkIndex++;
            float z = spawnZ + Random.Range(0f, 2f);
            if (chunkIndex % gateEvery == 0) { SpawnGatePair(z); return; }

            // Wave size grows slowly with level; the nastier patterns only appear from level 2 and 3.
            int extra = (Level - 1) / 2;
            int patterns = Level >= 3 ? 7 : Level >= 2 ? 6 : 4;
            int pattern = Random.Range(0, patterns);
            switch (pattern)
            {
                case 0: SpawnPack(z, 2 + extra, TreadmillEnemyLibrary.Grunt); break;
                case 1: SpawnPack(z, 3 + extra, TreadmillEnemyLibrary.Grunt); break;
                case 2: SpawnPack(z, 2 + extra, TreadmillEnemyLibrary.Runner); break;
                case 3: SpawnPack(z, 2 + extra, TreadmillEnemyLibrary.Grunt); SpawnPack(z + 5f, 2 + extra, TreadmillEnemyLibrary.Grunt); break;
                case 4: SpawnLine(z, 4 + Mathf.Min(extra, 3), TreadmillEnemyLibrary.Grunt); break;
                case 5: SpawnOne(z, TreadmillEnemyLibrary.Tank); SpawnPack(z + 4f, 2 + extra, TreadmillEnemyLibrary.Grunt); break;
                default: SpawnLine(z, 4, TreadmillEnemyLibrary.Runner); SpawnOne(z + 6f, TreadmillEnemyLibrary.Tank); break;
            }
        }

        float RandomLaneX() => Random.Range(-laneHalfWidth * 0.75f, laneHalfWidth * 0.75f);

        void SpawnOne(float z, TreadmillEnemyDef def)
        {
            Spawn(new Vector3(RandomLaneX(), 0f, z), def, HpMult);
        }

        /// <summary>A loose cluster around a random lane position.</summary>
        void SpawnPack(float z, int count, TreadmillEnemyDef def)
        {
            float cx = RandomLaneX();
            for (int i = 0; i < count; i++)
            {
                Vector2 o = Random.insideUnitCircle * 1.3f;
                float x = Mathf.Clamp(cx + o.x, -laneHalfWidth, laneHalfWidth);
                Spawn(new Vector3(x, 0f, z + o.y * 1.5f), def, HpMult);
            }
        }

        /// <summary>A row across the whole lane; there is no way around it, only through it.</summary>
        void SpawnLine(float z, int count, TreadmillEnemyDef def)
        {
            for (int i = 0; i < count; i++)
            {
                float x = count == 1 ? 0f : Mathf.Lerp(-laneHalfWidth * 0.9f, laneHalfWidth * 0.9f, i / (float)(count - 1));
                Spawn(new Vector3(x, 0f, z + Random.Range(-0.3f, 0.3f)), def, HpMult);
            }
        }

        void SpawnBoss()
        {
            BossSpawned = true;
            float hpMult = 1f + (Level - 1) * bossHpPerLevel;
            float bite = TreadmillEnemyLibrary.Boss.ContactUnits + bossBitePerLevel * (Level - 1);
            Spawn(new Vector3(0f, 0f, spawnZ), TreadmillEnemyLibrary.Boss, hpMult, bite);
        }

        void Spawn(Vector3 pos, TreadmillEnemyDef def, float hpMult, float contactOverride = -1f)
        {
            var e = Instantiate(enemyPrefab, pos, Quaternion.identity, root);
            e.Setup(def, hpMult, contactOverride);
        }

        // ---------------------------------------------------------------- gates

        void SpawnGatePair(float z)
        {
            float half = laneHalfWidth * 0.5f;
            var a = RollGate(true);
            var b = Random.value < 0.5f ? RollGate(true) : RollGate(false);
            if (Random.value < 0.5f) { var t = a; a = b; b = t; }
            SpawnGate(new Vector3(-half, 0f, z), a.type, a.value, half);
            SpawnGate(new Vector3(half, 0f, z), b.type, b.value, half);
        }

        /// <summary>Gate values are deliberately small and flat so growth is gradual.</summary>
        (PowerUpType type, float value) RollGate(bool positive)
        {
            if (positive)
            {
                int r = Random.Range(0, 10);
                if (r < 5) return (PowerUpType.AddUnits, Mathf.Round(Random.Range(2f, 4f) + Level * 0.5f));
                if (r < 7) return (PowerUpType.Damage, Mathf.Round(Random.Range(8f, 15f)));
                if (r < 9) return (PowerUpType.FireRate, Mathf.Round(Random.Range(6f, 12f)));
                return (Random.value < 0.5f ? PowerUpType.MoveSpeed : PowerUpType.ProjectileSpeed, 10f);
            }
            int n = Random.Range(0, 3);
            if (n == 0) return (PowerUpType.AddUnits, -Mathf.Round(Random.Range(2f, 3f) + Level * 0.3f));
            if (n == 1) return (PowerUpType.Damage, -10f);
            return (PowerUpType.FireRate, -8f);
        }

        void SpawnGate(Vector3 pos, PowerUpType type, float value, float halfWidth)
        {
            var g = Instantiate(gatePrefab, pos, Quaternion.identity, root);
            g.Setup(type, value, halfWidth);
        }
    }
}
