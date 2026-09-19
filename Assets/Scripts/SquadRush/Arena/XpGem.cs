using UnityEngine;

namespace SquadRush.Arena
{
    /// <summary>Experience dropped by enemies. Flies to the player once inside their pickup radius.</summary>
    public class XpGem : MonoBehaviour
    {
        public float value = 1f;
        public float driftSpeed = 1.2f;
        float pull;
        float bob;

        void Awake() => bob = Random.value * 6f;

        void Update()
        {
            var am = ArenaManager.Instance;
            if (am == null || am.State != ArenaState.Playing) return;
            var player = am.player;
            if (player == null) return;

            Vector3 to = player.transform.position - transform.position;
            to.y = 0f;
            float dist = to.magnitude;

            // Outside the pickup radius gems still creep toward the player so a careful player is not starved of XP.
            if (dist >= player.pickupRadius && pull <= 0f && dist > 0.01f)
                transform.position += to / dist * (driftSpeed * Time.deltaTime);

            if (dist < player.pickupRadius || pull > 0f)
            {
                pull += Time.deltaTime * 30f;
                float step = Mathf.Min(dist, (6f + pull) * Time.deltaTime);
                transform.position += to / Mathf.Max(dist, 0.001f) * step;
                if (dist < 0.5f)
                {
                    am.AddXp(value);
                    Destroy(gameObject);
                    return;
                }
            }

            var p = transform.position;
            p.y = 0.35f + Mathf.Sin(Time.time * 4f + bob) * 0.08f;
            transform.position = p;
            transform.Rotate(0f, 120f * Time.deltaTime, 0f, Space.World);
        }
    }
}
