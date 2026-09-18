using System;
using System.Collections.Generic;
using UnityEngine;

namespace SquadRush
{
    /// <summary>
    /// A bullet fired by the squad. Uses a sphere-cast each frame instead of trigger colliders
    /// so fast bullets never tunnel through thin obstacles.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        float speed;
        float damage;
        float life;
        int pierce;
        float radius = 0.11f;
        Action<Projectile> onFinished;

        readonly List<Obstacle> alreadyHit = new List<Obstacle>();
        static readonly RaycastHit[] Hits = new RaycastHit[24];
        static readonly HitDistanceComparer Comparer = new HitDistanceComparer();

        public void Launch(Vector3 position, float speed, float damage, int pierce, float scale, float life, Action<Projectile> onFinished)
        {
            transform.position = position;
            transform.localScale = Vector3.one * 0.22f * scale;
            radius = 0.11f * scale;
            this.speed = speed;
            this.damage = damage;
            this.pierce = pierce;
            this.life = life;
            this.onFinished = onFinished;
            alreadyHit.Clear();
            gameObject.SetActive(true);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            float step = speed * dt;
            Vector3 from = transform.position;

            int n = Physics.SphereCastNonAlloc(from, radius, Vector3.forward, Hits, step, ~0, QueryTriggerInteraction.Collide);
            if (n > 0)
            {
                Array.Sort(Hits, 0, n, Comparer);
                for (int i = 0; i < n; i++)
                {
                    var col = Hits[i].collider;

                    var gate = col.GetComponentInParent<PowerUpGate>();
                    if (gate != null)
                    {
                        gate.OnShot();
                        Finish();
                        return;
                    }

                    var ob = col.GetComponentInParent<Obstacle>();
                    if (ob == null || alreadyHit.Contains(ob)) continue;

                    alreadyHit.Add(ob);
                    ob.TakeDamage(damage);
                    if (pierce <= 0)
                    {
                        Finish();
                        return;
                    }
                    pierce--;
                }
            }

            transform.position = from + Vector3.forward * step;
            life -= dt;
            if (life <= 0f) Finish();
        }

        void Finish()
        {
            gameObject.SetActive(false);
            onFinished?.Invoke(this);
        }

        class HitDistanceComparer : IComparer<RaycastHit>
        {
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }
    }
}
