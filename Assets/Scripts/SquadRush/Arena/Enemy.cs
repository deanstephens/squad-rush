using System.Collections.Generic;
using UnityEngine;

namespace SquadRush.Arena
{
    public class EnemyDef
    {
        public string Name;
        public float Hp, Speed, ContactDamage, Scale, Xp;
        public int Coins;
        public Color Color;
    }

    public static class EnemyLibrary
    {
        public static readonly EnemyDef Grunt  = new EnemyDef { Name = "Grunt",  Hp = 10f,  Speed = 3.2f, ContactDamage = 8f,  Scale = 0.85f, Xp = 1f,  Coins = 1,  Color = new Color(0.95f, 0.4f, 0.35f) };
        public static readonly EnemyDef Runner = new EnemyDef { Name = "Runner", Hp = 6f,   Speed = 5.2f, ContactDamage = 5f,  Scale = 0.65f, Xp = 1f,  Coins = 1,  Color = new Color(1f, 0.75f, 0.3f) };
        public static readonly EnemyDef Brute  = new EnemyDef { Name = "Brute",  Hp = 70f,  Speed = 2.1f, ContactDamage = 18f, Scale = 1.6f,  Xp = 6f,  Coins = 5,  Color = new Color(0.6f, 0.25f, 0.6f) };
        public static readonly EnemyDef Elite  = new EnemyDef { Name = "Elite",  Hp = 450f, Speed = 2.5f, ContactDamage = 35f, Scale = 2.4f,  Xp = 30f, Coins = 25, Color = new Color(0.15f, 0.15f, 0.2f) };
    }

    /// <summary>A swarm enemy: walks straight at the player, hurts on contact, drops XP and coins when killed.</summary>
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public class Enemy : MonoBehaviour
    {
        public static readonly List<Enemy> All = new List<Enemy>();

        public Renderer bodyRenderer;

        public float Radius { get; private set; } = 0.45f;
        public bool Dead { get; private set; }
        public float HpFraction => maxHp > 0f ? hp / maxHp : 0f;

        EnemyDef def;
        float hp, maxHp, speed, contactDamage;
        bool enraged;
        float contactCooldown;
        float flashTimer;
        float burnDps, burnTimer, burnTick;
        float slowPct, slowTimer;
        Vector3 knock;
        Rigidbody rb;
        ArenaPlayer player;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static MaterialPropertyBlock mpb;

        public void Setup(EnemyDef def, float hpMult, ArenaPlayer player)
        {
            this.def = def;
            this.player = player;
            maxHp = hp = def.Hp * hpMult;
            speed = def.Speed;
            contactDamage = def.ContactDamage;
            enraged = false;
            Dead = false;
            burnDps = burnTimer = slowPct = slowTimer = 0f;
            knock = Vector3.zero;

            transform.localScale = Vector3.one * def.Scale;
            Radius = 0.45f * def.Scale;

            rb = GetComponent<Rigidbody>();
            rb.mass = def.Scale * def.Scale;

            SetColor(def.Color);
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void FixedUpdate()
        {
            var am = ArenaManager.Instance;
            if (Dead || am == null || am.State != ArenaState.Playing || player == null)
            {
                if (rb != null) rb.linearVelocity = Vector3.zero;
                return;
            }

            Vector3 dir = player.transform.position - transform.position;
            dir.y = 0f;
            float dist = dir.magnitude;
            if (dist > 0.01f) dir /= dist;
            float slow = slowTimer > 0f ? 1f - slowPct : 1f;
            rb.linearVelocity = dir * (speed * slow) + knock;
            knock *= Mathf.Exp(-8f * Time.fixedDeltaTime);
            if (dir.sqrMagnitude > 0.01f) rb.MoveRotation(Quaternion.LookRotation(dir, Vector3.up));
        }

        void Update()
        {
            if (flashTimer > 0f)
            {
                flashTimer -= Time.deltaTime;
                if (flashTimer <= 0f) SetColor(enraged ? EnragedColor : def.Color);
            }

            var am = ArenaManager.Instance;
            if (Dead || am == null || am.State != ArenaState.Playing || player == null) return;

            float dt = Time.deltaTime;
            if (slowTimer > 0f) slowTimer -= dt;
            if (burnTimer > 0f)
            {
                burnTimer -= dt;
                burnTick -= dt;
                if (burnTick <= 0f)
                {
                    burnTick = 0.25f;
                    hp -= burnDps * 0.25f;
                    SetColor(new Color(1f, 0.5f, 0.1f));
                    flashTimer = 0.08f;
                    if (hp <= 0f) { Die(); return; }
                }
            }

            contactCooldown -= dt;
            if (contactCooldown > 0f) return;

            float reach = Radius + player.Radius + 0.15f;
            Vector3 d = player.transform.position - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude <= reach * reach)
            {
                player.TakeDamage(contactDamage);
                contactCooldown = 0.7f;
            }
        }

        public void TakeDamage(float amount)
        {
            if (Dead) return;
            hp -= amount;
            flashTimer = 0.06f;
            SetColor(Color.white);
            if (hp <= 0f) Die();
        }

        static readonly Color EnragedColor = new Color(0.9f, 0.1f, 0.1f);

        /// <summary>Boss timeout: faster, twice the bite, and unmistakably red.</summary>
        public void Enrage()
        {
            if (Dead || enraged) return;
            enraged = true;
            speed *= 1.6f;
            contactDamage *= 2f;
            SetColor(EnragedColor);
            Fx.Burst(transform.position + Vector3.up * 1f, EnragedColor, 16, 0.25f);
        }

        public void ApplyBurn(float dps, float duration)
        {
            if (Dead) return;
            burnDps = Mathf.Max(burnDps * Mathf.Clamp01(burnTimer / Mathf.Max(duration, 0.01f)), dps);
            burnTimer = Mathf.Max(burnTimer, duration);
        }

        public void ApplySlow(float pct, float duration)
        {
            if (Dead) return;
            slowPct = Mathf.Max(slowPct, pct);
            slowTimer = Mathf.Max(slowTimer, duration);
        }

        public void Knockback(Vector3 impulse)
        {
            if (Dead) return;
            knock += impulse / Mathf.Max(0.5f, def.Scale * def.Scale);
        }

        void Die()
        {
            Dead = true;
            var am = ArenaManager.Instance;
            if (am != null)
            {
                am.OnEnemyKilled(def.Coins);
                am.SpawnGem(transform.position, def.Xp);
            }
            Fx.Burst(transform.position + Vector3.up * 0.4f, def.Color, def.Scale > 1.5f ? 14 : 5, 0.16f * def.Scale);
            Destroy(gameObject);
        }

        void SetColor(Color c)
        {
            if (bodyRenderer == null) return;
            if (mpb == null) mpb = new MaterialPropertyBlock();
            mpb.SetColor(BaseColorId, c);
            bodyRenderer.SetPropertyBlock(mpb);
        }

        // ---------------------------------------------------------------- queries

        public static Enemy Nearest(Vector3 pos, float range)
        {
            Enemy best = null;
            float bestD = range * range;
            for (int i = 0; i < All.Count; i++)
            {
                var e = All[i];
                if (e == null || e.Dead) continue;
                float d = (e.transform.position - pos).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = e;
                }
            }
            return best;
        }

        public static Enemy NearestExcluding(Vector3 pos, float range, List<Enemy> exclude)
        {
            Enemy best = null;
            float bestD = range * range;
            for (int i = 0; i < All.Count; i++)
            {
                var e = All[i];
                if (e == null || e.Dead || exclude.Contains(e)) continue;
                Vector3 d = e.transform.position - pos;
                d.y = 0f;
                float d2 = d.sqrMagnitude;
                if (d2 < bestD)
                {
                    bestD = d2;
                    best = e;
                }
            }
            return best;
        }

        public static void CollectInRadius(Vector3 pos, float radius, List<Enemy> into)
        {
            into.Clear();
            float r2 = radius * radius;
            for (int i = 0; i < All.Count; i++)
            {
                var e = All[i];
                if (e == null || e.Dead) continue;
                Vector3 d = e.transform.position - pos;
                d.y = 0f;
                if (d.sqrMagnitude <= r2) into.Add(e);
            }
        }

        public static void DamageInRadius(Vector3 pos, float radius, float damage)
        {
            float r2 = radius * radius;
            for (int i = All.Count - 1; i >= 0; i--)
            {
                var e = All[i];
                if (e == null || e.Dead) continue;
                Vector3 d = e.transform.position - pos;
                d.y = 0f;
                if (d.sqrMagnitude <= r2) e.TakeDamage(damage);
            }
        }
    }
}
