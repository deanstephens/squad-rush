using System;
using System.Collections.Generic;
using UnityEngine;

namespace SquadRush.Arena
{
    /// <summary>
    /// A gun projectile. No physics: each frame it sweeps a segment against the live enemy list,
    /// which is far cheaper than colliders when hundreds of bullets and enemies are alive.
    /// </summary>
    public class ArenaBullet : MonoBehaviour
    {
        Gun gun;
        Vector3 dir;
        float speed, life, radius, splash;
        int pierce, bounces, pierced;
        Action<ArenaBullet> onFinished;
        Renderer rend;
        readonly List<Enemy> hit = new List<Enemy>();
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static MaterialPropertyBlock mpb;

        public void Launch(Gun gun, Vector3 pos, Vector3 direction, float speed, int pierce, float life, float scale, float splash, int bounces, Color color, Action<ArenaBullet> onFinished)
        {
            this.gun = gun;
            transform.position = pos;
            transform.localScale = Vector3.one * 0.25f * scale;
            radius = 0.125f * scale;
            dir = direction.normalized;
            this.speed = speed;
            this.pierce = pierce;
            this.life = life;
            this.splash = splash;
            this.bounces = bounces;
            this.onFinished = onFinished;
            pierced = 0;
            hit.Clear();

            if (rend == null) rend = GetComponent<Renderer>();
            if (mpb == null) mpb = new MaterialPropertyBlock();
            mpb.SetColor(BaseColorId, color);
            rend.SetPropertyBlock(mpb);
            gameObject.SetActive(true);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 from = transform.position;
            Vector3 to = from + dir * (speed * dt);
            // Hit-test on the ground plane: bullets fly at gun height, enemies are positioned at their feet.
            Vector3 flatFrom = new Vector3(from.x, 0f, from.z);
            Vector3 seg = new Vector3(to.x - from.x, 0f, to.z - from.z);
            float segLen2 = seg.sqrMagnitude;

            var all = Enemy.All;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                var e = all[i];
                if (e == null || e.Dead || hit.Contains(e)) continue;

                Vector3 ep = e.transform.position;
                ep.y = 0f;
                float t = segLen2 > 0.0001f ? Mathf.Clamp01(Vector3.Dot(ep - flatFrom, seg) / segLen2) : 0f;
                Vector3 closest = flatFrom + seg * t;
                float r = e.Radius + radius;
                if ((ep - closest).sqrMagnitude > r * r) continue;

                hit.Add(e);

                if (splash > 0f)
                {
                    gun.ApplySplash(closest, splash);
                    Finish();
                    return;
                }

                gun.ApplyHit(e, dir, pierced);
                pierced++;

                if (bounces > 0)
                {
                    var next = Enemy.NearestExcluding(closest, 7f, hit);
                    if (next != null)
                    {
                        bounces--;
                        Vector3 nd = next.transform.position - closest;
                        nd.y = 0f;
                        dir = nd.normalized;
                        transform.position = new Vector3(closest.x, from.y, closest.z);
                        life = Mathf.Max(life, 8f / speed);
                        return;
                    }
                }

                if (pierce <= 0)
                {
                    Finish();
                    return;
                }
                pierce--;
            }

            transform.position = to;
            life -= dt;
            if (life <= 0f) Finish();
        }

        void Finish()
        {
            gameObject.SetActive(false);
            onFinished?.Invoke(this);
        }
    }
}
