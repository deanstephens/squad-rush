using UnityEngine;

namespace SquadRush.Arena
{
    /// <summary>Streams enemies in from just outside the camera's view, ramping up with survival time.</summary>
    public class EnemySpawner : MonoBehaviour
    {
        public Enemy enemyPrefab;
        public float spawnRadius = 19f;
        public float arenaHalfSize = 29f;
        public int maxAlive = 220;
        public float eliteEvery = 60f;

        [Header("Ramp")]
        public float startInterval = 1.1f;
        public float minInterval = 0.12f;
        public float intervalDropPerSecond = 0.011f;
        public float hpGrowthPerMinute = 0.55f;

        float timer;
        float nextElite;
        bool running;
        Transform root;
        ArenaPlayer player;

        public void Begin(ArenaPlayer player)
        {
            this.player = player;
            if (root == null) root = new GameObject("Enemies").transform;
            foreach (Transform t in root) Destroy(t.gameObject);
            timer = 0.5f;
            nextElite = eliteEvery;
            running = true;
        }

        void Update()
        {
            var am = ArenaManager.Instance;
            if (!running || am == null || am.State != ArenaState.Playing) return;

            float t = am.TimeSurvived;
            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                timer = Mathf.Max(minInterval, startInterval - t * intervalDropPerSecond);
                int burst = 1 + Mathf.FloorToInt(t / 60f);
                for (int i = 0; i < burst; i++) SpawnOne(RollType(t), t);
            }

            if (t >= nextElite)
            {
                nextElite += eliteEvery;
                SpawnOne(EnemyLibrary.Elite, t);
            }
        }

        EnemyDef RollType(float t)
        {
            float r = Random.value;
            if (t > 40f && r < 0.12f + t * 0.0008f) return EnemyLibrary.Brute;
            if (t > 20f && r < 0.45f) return EnemyLibrary.Runner;
            return EnemyLibrary.Grunt;
        }

        void SpawnOne(EnemyDef def, float t)
        {
            if (Enemy.All.Count >= maxAlive) return;

            Vector2 dir = Random.insideUnitCircle.normalized;
            Vector3 pos = player.transform.position + new Vector3(dir.x, 0f, dir.y) * (spawnRadius + Random.Range(0f, 3f));
            pos.x = Mathf.Clamp(pos.x, -arenaHalfSize, arenaHalfSize);
            pos.z = Mathf.Clamp(pos.z, -arenaHalfSize, arenaHalfSize);
            pos.y = 0f;

            float hpMult = 1f + t / 60f * hpGrowthPerMinute;
            var e = Instantiate(enemyPrefab, pos, Quaternion.identity, root);
            e.Setup(def, hpMult, player);
        }
    }
}
