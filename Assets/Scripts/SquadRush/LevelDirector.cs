using UnityEngine;

namespace SquadRush
{
    /// <summary>
    /// Procedurally streams obstacle patterns, power-up gate pairs and bosses onto the treadmill.
    /// Difficulty scales with distance travelled.
    /// </summary>
    public class LevelDirector : MonoBehaviour
    {
        [Header("Prefabs")]
        public Obstacle obstaclePrefab;
        public PowerUpGate gatePrefab;
        public Material debrisMaterial;

        [Header("Layout")]
        public float spawnZ = 55f;
        public float chunkLength = 9f;
        public float laneHalfWidth = 2.6f;
        public int gateEvery = 3;
        public int bossEvery = 9;

        [Header("Difficulty")]
        public float baseGruntHealth = 10f;
        public float healthPerMeter = 0.012f;
        public float healthPerChunk = 0.03f;

        [Header("Colors")]
        public Color gruntColor = new Color(0.95f, 0.45f, 0.3f);
        public Color wallColor = new Color(0.9f, 0.3f, 0.35f);
        public Color tankColor = new Color(0.6f, 0.25f, 0.6f);
        public Color bossColor = new Color(0.15f, 0.15f, 0.2f);

        Transform root;
        float accumulator;
        int chunkIndex;
        bool running;

        void Awake()
        {
            Fx.DebrisMaterial = debrisMaterial;
            root = new GameObject("Lane").transform;
        }

        public void BeginRun()
        {
            foreach (Transform t in root) Destroy(t.gameObject);
            chunkIndex = 0;
            accumulator = 0f;
            running = true;

            // A gentle opener: one gate pair, then a couple of easy chunks already on the belt.
            SpawnGatePair(spawnZ * 0.45f);
            SpawnSingle(spawnZ * 0.75f, 0.6f);
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (!running || gm == null || gm.State != GameState.Playing) return;

            accumulator += Treadmill.Delta;
            while (accumulator >= chunkLength)
            {
                accumulator -= chunkLength;
                SpawnChunk();
            }
        }

        float HealthMult
        {
            get
            {
                float dist = GameManager.Instance != null ? GameManager.Instance.Distance : 0f;
                return 1f + dist * healthPerMeter + chunkIndex * healthPerChunk;
            }
        }

        void SpawnChunk()
        {
            chunkIndex++;
            float z = spawnZ + Random.Range(0f, 2f);

            if (chunkIndex % bossEvery == 0) { SpawnBoss(z); return; }
            if (chunkIndex % gateEvery == 0) { SpawnGatePair(z); return; }

            int pattern = Random.Range(0, 5);
            switch (pattern)
            {
                case 0: SpawnSingle(z, 1f); break;
                case 1: SpawnSingle(z, 1f); SpawnSingle(z + 3f, 1f); break;
                case 2: SpawnWall(z); break;
                case 3: SpawnTank(z); break;
                default: SpawnSingle(z, 1.4f); break;
            }
        }

        float RandomLaneX() => Random.Range(-1, 2) * (laneHalfWidth * 0.65f);

        void SpawnSingle(float z, float hpScale)
        {
            float hp = baseGruntHealth * hpScale * HealthMult;
            Spawn(new Vector3(RandomLaneX(), 0f, z), hp, Vector3.one, gruntColor, false);
        }

        void SpawnWall(float z)
        {
            // Three blocks across; one is left out so the wall can be dodged as well as shot.
            int gap = Random.Range(0, 3);
            for (int i = 0; i < 3; i++)
            {
                if (i == gap) continue;
                float x = (i - 1) * laneHalfWidth * 0.66f;
                float hp = baseGruntHealth * 0.9f * HealthMult;
                Spawn(new Vector3(x, 0f, z), hp, new Vector3(1.6f, 1f, 1f), wallColor, false);
            }
        }

        void SpawnTank(float z)
        {
            float hp = baseGruntHealth * 4.5f * HealthMult;
            Spawn(new Vector3(RandomLaneX(), 0f, z), hp, Vector3.one * 1.7f, tankColor, false);
        }

        void SpawnBoss(float z)
        {
            float hp = baseGruntHealth * 18f * HealthMult;
            Spawn(new Vector3(0f, 0f, z), hp, new Vector3(3f, 2.6f, 2.6f), bossColor, true);
        }

        void Spawn(Vector3 pos, float hp, Vector3 size, Color color, bool boss)
        {
            var ob = Instantiate(obstaclePrefab, pos, Quaternion.identity, root);
            int coins = Mathf.Max(1, Mathf.RoundToInt(hp / 8f));
            ob.Setup(hp, size, color, boss, coins);
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

        (PowerUpType type, float value) RollGate(bool positive)
        {
            float scale = 1f + chunkIndex * 0.08f;
            if (positive)
            {
                int r = Random.Range(0, 10);
                if (r < 4) return (PowerUpType.AddUnits, Mathf.Round(Random.Range(3f, 7f) * scale));
                if (r < 5) return (PowerUpType.MultiplyUnits, Random.value < 0.3f ? 2f : 1.5f);
                if (r < 7) return (PowerUpType.Damage, Random.Range(20f, 40f));
                if (r < 9) return (PowerUpType.FireRate, Random.Range(15f, 30f));
                return (Random.value < 0.5f ? PowerUpType.MoveSpeed : PowerUpType.ProjectileSpeed, 20f);
            }
            else
            {
                int r = Random.Range(0, 3);
                if (r == 0) return (PowerUpType.AddUnits, -Mathf.Round(Random.Range(3f, 6f) * scale));
                if (r == 1) return (PowerUpType.Damage, -20f);
                return (PowerUpType.FireRate, -15f);
            }
        }

        void SpawnGate(Vector3 pos, PowerUpType type, float value, float halfWidth)
        {
            var g = Instantiate(gatePrefab, pos, Quaternion.identity, root);
            g.Setup(type, value, halfWidth);
        }
    }
}
