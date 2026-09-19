using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace SquadRush
{
    public class TreadmillEnemyDef
    {
        public string Name;
        public float Hp, WalkSpeed, LateralSpeed, Scale, ContactUnits;
        public int Coins, Scrap;
        public Color Color;
    }

    public static class TreadmillEnemyLibrary
    {
        public static readonly TreadmillEnemyDef Grunt  = new TreadmillEnemyDef { Name = "Grunt",  Hp = 6f,   WalkSpeed = 1.2f, LateralSpeed = 1.6f, Scale = 0.9f,  ContactUnits = 1f, Coins = 1, Scrap = 0, Color = new Color(0.95f, 0.45f, 0.3f) };
        public static readonly TreadmillEnemyDef Runner = new TreadmillEnemyDef { Name = "Runner", Hp = 4f,   WalkSpeed = 3.2f, LateralSpeed = 2.6f, Scale = 0.7f,  ContactUnits = 1f, Coins = 1, Scrap = 0, Color = new Color(1f, 0.78f, 0.3f) };
        public static readonly TreadmillEnemyDef Tank   = new TreadmillEnemyDef { Name = "Tank",   Hp = 36f,  WalkSpeed = 0.5f, LateralSpeed = 0.4f, Scale = 1.7f,  ContactUnits = 4f, Coins = 4, Scrap = 3, Color = new Color(0.6f, 0.25f, 0.6f) };
        public static readonly TreadmillEnemyDef Boss   = new TreadmillEnemyDef { Name = "Boss",   Hp = 800f, WalkSpeed = 0.8f, LateralSpeed = 0.6f, Scale = 2.8f,  ContactUnits = 2f, Coins = 30, Scrap = 15, Color = new Color(0.15f, 0.15f, 0.2f) };
    }

    /// <summary>
    /// A treadmill-mode enemy. Rides the belt toward the squad while also walking and drifting
    /// sideways to line up with it. Contact removes units. The boss parks in front of the squad
    /// and bites on a timer until it is killed.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class TreadmillEnemy : MonoBehaviour
    {
        public static readonly List<TreadmillEnemy> All = new List<TreadmillEnemy>();
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        [Header("Wiring")]
        public Transform body;
        public Renderer bodyRenderer;
        public TMP_Text label;

        public float MaxHealth { get; private set; }
        public float Health { get; private set; }
        public bool IsBoss { get; private set; }
        public bool Dead => dead;

        TreadmillEnemyDef def;
        float contactUnits;
        int coins, scrap;
        float lateralSpeed;
        Color baseColor;
        Material mat;
        float flashTimer;
        float bob;
        float biteTimer;
        bool dead;
        bool holding;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public void Setup(TreadmillEnemyDef def, float hpMult, float contactUnitsOverride = -1f)
        {
            this.def = def;
            MaxHealth = Health = def.Hp * hpMult;
            IsBoss = def == TreadmillEnemyLibrary.Boss;
            contactUnits = contactUnitsOverride > 0f ? contactUnitsOverride : def.ContactUnits;
            coins = def.Coins;
            scrap = def.Scrap;
            lateralSpeed = def.LateralSpeed;
            baseColor = def.Color;
            bob = Random.value * 6f;

            transform.localScale = Vector3.one * def.Scale;
            if (label != null)
            {
                label.transform.localScale = Vector3.one / def.Scale;
                label.transform.localPosition = new Vector3(0f, 1.25f + 0.2f / def.Scale, 0f);
            }

            var rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            mat = bodyRenderer.material;
            mat.SetColor(BaseColorId, baseColor);
            RefreshLabel();
        }

        void Update()
        {
            if (dead) return;
            var gm = GameManager.Instance;
            float dt = Time.deltaTime;

            if (flashTimer > 0f)
            {
                flashTimer -= dt;
                if (flashTimer <= 0f && mat != null) mat.SetColor(BaseColorId, baseColor);
            }

            if (gm == null || gm.State != GameState.Playing) return;

            var squad = gm.squad;
            Vector3 p = transform.position;

            if (IsBoss)
            {
                float holdZ = squad.transform.position.z + 2.2f;
                if (p.z > holdZ)
                {
                    p.z -= Treadmill.Delta + def.WalkSpeed * dt;
                    if (p.z <= holdZ) { p.z = holdZ; holding = true; }
                }
                else holding = true;
            }
            else
            {
                p.z -= Treadmill.Delta + def.WalkSpeed * dt;
            }

            // Drift toward the squad's lane position once reasonably close.
            if (p.z - squad.transform.position.z < 18f)
                p.x = Mathf.MoveTowards(p.x, squad.transform.position.x, lateralSpeed * dt);

            transform.position = p;

            if (body != null)
            {
                float walk = holding ? 0f : Mathf.Abs(Mathf.Sin(Time.time * 10f + bob)) * 0.06f;
                body.localPosition = new Vector3(0f, 0.55f + walk, 0f);
            }

            if (holding)
            {
                biteTimer -= dt;
                if (biteTimer <= 0f)
                {
                    biteTimer = 1.5f;
                    squad.TakeHit(Mathf.CeilToInt(contactUnits));
                    Fx.Burst(squad.transform.position + Vector3.up * 0.6f, new Color(1f, 0.3f, 0.3f), 5, 0.14f);
                }
            }

            if (p.z < -8f) Destroy(gameObject);
        }

        public void TakeDamage(float amount)
        {
            if (dead) return;
            Health -= amount;
            flashTimer = 0.07f;
            if (mat != null) mat.SetColor(BaseColorId, Color.white);
            RefreshLabel();
            if (Health <= 0f) Kill();
        }

        void RefreshLabel()
        {
            if (label != null) label.text = Mathf.Max(0, Mathf.CeilToInt(Health)).ToString();
        }

        void Kill()
        {
            dead = true;
            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.AddCoins(coins);
                gm.AddScrap(scrap);
            }
            Fx.Burst(body != null ? body.position : transform.position, baseColor, IsBoss ? 24 : 6, IsBoss ? 0.32f : 0.16f * def.Scale);
            if (IsBoss && gm != null) gm.OnBossKilled();
            Destroy(gameObject);
        }

        /// <summary>Called by the squad when a non-boss enemy runs into it.</summary>
        public void OnSquadContact(Squad squad)
        {
            if (dead || IsBoss) return;
            dead = true;
            squad.TakeHit(Mathf.CeilToInt(contactUnits));
            Fx.Burst(body != null ? body.position : transform.position, baseColor, 5, 0.14f);
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (mat != null) Destroy(mat);
        }
    }
}
