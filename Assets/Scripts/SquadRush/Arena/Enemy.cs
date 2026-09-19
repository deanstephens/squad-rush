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

        EnemyDef def;
        float hp, maxHp, speed;
        float contactCooldown;
        float flashTimer;
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
            Dead = false;

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
            rb.linearVelocity = dir * speed;
            if (dir.sqrMagnitude > 0.01f) rb.MoveRotation(Quaternion.LookRotation(dir, Vector3.up));
        }

        void Update()
        {
            if (flashTimer > 0f)
            {
                flashTimer -= Time.deltaTime;
                if (flashTimer <= 0f) SetColor(def.Color);
            }

            var am = ArenaManager.Instance;
            if (Dead || am == null || am.State != ArenaState.Playing || player == null) return;

            contactCooldown -= Time.deltaTime;
            if (contactCooldown > 0f) return;

            float reach = Radius + player.Radius + 0.15f;
            Vector3 d = player.transform.position - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude <= reach * reach)
            {
                player.TakeDamage(def.ContactDamage);
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
