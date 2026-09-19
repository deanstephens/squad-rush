using System.Collections.Generic;
using UnityEngine;

namespace SquadRush.Arena
{
    /// <summary>The arena player's weapon. Auto-aims at the nearest enemy in range, fires on its own timer,
    /// and resolves every bullet hit so gun-specific mechanics (crits, focus, burn, ricochet...) live in one place.</summary>
    public class Gun : MonoBehaviour
    {
        public GunDef Def { get; private set; }
        public GunStats Stats { get; private set; } = new GunStats();

        ArenaPlayer owner;
        Transform muzzle;
        float timer;
        float heat;
        bool firedThisFrame;
        Enemy focusTarget;
        int focusStacks;

        public void Init(GunDef def, GunStats stats, ArenaPlayer owner, Transform muzzle)
        {
            Def = def;
            Stats = stats;
            this.owner = owner;
            this.muzzle = muzzle;
            timer = 0.1f;
        }

        public float Range => Def.Range * Stats.rangeMult;

        void Update()
        {
            var am = ArenaManager.Instance;
            if (am == null || am.State != ArenaState.Playing || Def == null) return;

            float dt = Time.deltaTime;
            firedThisFrame = false;
            timer -= dt;

            var target = timer <= 0f ? Enemy.Nearest(transform.position, Range) : null;
            if (target != null)
            {
                float rate = Def.FireRate * Stats.fireRateMult * owner.fireRateMult * (1f + heat);
                timer = 1f / Mathf.Max(0.1f, rate);

                Vector3 dir = target.transform.position - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.001f) dir = transform.forward;
                dir.Normalize();
                transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
                Fire(dir);
                firedThisFrame = true;
            }

            // Heat: ramps while a target keeps the gun busy, cools quickly when idle.
            if (Stats.heatMax > 0f)
            {
                bool busy = firedThisFrame || Enemy.Nearest(transform.position, Range) != null;
                heat = busy ? Mathf.Min(Stats.heatMax, heat + Stats.heatRampPerSec * dt)
                            : Mathf.Max(0f, heat - Stats.heatMax * 1.5f * dt);
            }
        }

        void Fire(Vector3 dir)
        {
            int n = Def.BulletsPerShot + Stats.extraBullets;
            int pierce = Def.Pierce + Stats.extraPierce + owner.extraPierce;
            float speed = Def.BulletSpeed * Stats.bulletSpeedMult;
            float life = Range / speed;
            float scale = Def.BulletScale * Stats.bulletScaleMult;
            float spread = Def.Spread * Stats.spreadMult;
            Vector3 pos = muzzle != null ? muzzle.position : transform.position + Vector3.up * 0.5f;

            for (int i = 0; i < n; i++)
            {
                float angle;
                if (n == 1) angle = Random.Range(-spread * 0.5f, spread * 0.5f);
                else
                {
                    float cone = Mathf.Max(spread, 3f * (n - 1));
                    angle = -cone * 0.5f + cone * i / (n - 1) + Random.Range(-1.5f, 1.5f);
                }
                Vector3 d = Quaternion.Euler(0f, angle, 0f) * dir;
                var b = ArenaManager.Instance.GetBullet();
                b.Launch(this, pos, d, speed, pierce, life, scale, Def.SplashRadius * Stats.splashRadiusMult, Stats.ricochet, Def.Color, ArenaManager.Instance.ReturnBullet);
            }
        }

        // ---------------------------------------------------------------- hit resolution

        public float ComputeDamage(Enemy e, int piercedSoFar)
        {
            float d = Def.Damage * Stats.damageMult * owner.damageMult;

            if (Stats.focusStackPct > 0f)
            {
                if (e == focusTarget) focusStacks++;
                else { focusTarget = e; focusStacks = 0; }
                d *= 1f + Mathf.Min(focusStacks * Stats.focusStackPct, Stats.focusMaxPct);
            }
            if (Stats.lowHpBonus > 0f && e.HpFraction < 0.3f) d *= 1f + Stats.lowHpBonus;
            if (Stats.fullHpBonus > 0f && e.HpFraction >= 0.999f) d *= 1f + Stats.fullHpBonus;
            if (Stats.pointBlankBonus > 0f)
            {
                Vector3 to = e.transform.position - owner.transform.position;
                to.y = 0f;
                if (to.sqrMagnitude < 3.5f * 3.5f) d *= 1f + Stats.pointBlankBonus;
            }
            if (Stats.piercedBonusPct > 0f && piercedSoFar > 0) d *= 1f + piercedSoFar * Stats.piercedBonusPct;
            if (Stats.critChance > 0f && Random.value < Stats.critChance) d *= Stats.critMult;
            return d;
        }

        /// <summary>Damage one enemy and apply this gun's on-hit effects.</summary>
        public void ApplyHit(Enemy e, Vector3 pushDir, int piercedSoFar)
        {
            float dmg = ComputeDamage(e, piercedSoFar);
            if (Stats.burnPct > 0f) e.ApplyBurn(dmg * Stats.burnPct, Stats.burnDuration);
            if (Stats.slowPct > 0f) e.ApplySlow(Stats.slowPct, Stats.slowDuration);
            if (Stats.knockback > 0f) e.Knockback(pushDir * Stats.knockback);
            e.TakeDamage(dmg);
        }

        static readonly List<Enemy> splashBuffer = new List<Enemy>();

        /// <summary>Explosion at a point: hits everything in the radius, then any cluster bomblets.</summary>
        public void ApplySplash(Vector3 point, float radius)
        {
            Enemy.CollectInRadius(point, radius, splashBuffer);
            foreach (var e in splashBuffer)
            {
                Vector3 push = e.transform.position - point;
                push.y = 0f;
                ApplyHit(e, push.sqrMagnitude > 0.001f ? push.normalized : Vector3.forward, 0);
            }
            Fx.Burst(point + Vector3.up * 0.5f, new Color(1f, 0.6f, 0.2f), 8, 0.22f);

            for (int i = 0; i < Stats.clusterCount; i++)
            {
                Vector2 off = Random.insideUnitCircle * radius * 1.1f;
                Vector3 p = point + new Vector3(off.x, 0f, off.y);
                float r = radius * 0.6f;
                Enemy.CollectInRadius(p, r, splashBuffer);
                float dmg = Def.Damage * Stats.damageMult * owner.damageMult * 0.4f;
                foreach (var e in splashBuffer) e.TakeDamage(dmg);
                Fx.Burst(p + Vector3.up * 0.4f, new Color(1f, 0.75f, 0.3f), 4, 0.16f);
            }
        }
    }
}
