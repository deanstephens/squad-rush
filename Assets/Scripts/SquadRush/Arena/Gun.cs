using UnityEngine;

namespace SquadRush.Arena
{
    /// <summary>A gun mounted on the arena player. Auto-aims at the nearest enemy in range and fires on its own timer.</summary>
    public class Gun : MonoBehaviour
    {
        public GunDef Def { get; private set; }

        // Run-only modifiers granted by level-ups.
        public float damageMult = 1f;
        public float fireRateMult = 1f;
        public int extraBullets;
        public int extraPierce;

        ArenaPlayer owner;
        Transform muzzle;
        float timer;

        public void Init(GunDef def, ArenaPlayer owner, Transform muzzle)
        {
            Def = def;
            this.owner = owner;
            this.muzzle = muzzle;
            timer = Random.value * 0.3f;
        }

        void Update()
        {
            var am = ArenaManager.Instance;
            if (am == null || am.State != ArenaState.Playing || Def == null) return;

            timer -= Time.deltaTime;
            if (timer > 0f) return;

            var target = Enemy.Nearest(transform.position, Def.Range);
            if (target == null)
            {
                timer = 0.05f;
                return;
            }

            timer = 1f / Mathf.Max(0.1f, Def.FireRate * fireRateMult * owner.fireRateMult);

            Vector3 dir = target.transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) dir = transform.forward;
            dir.Normalize();
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

            Fire(dir);
        }

        void Fire(Vector3 dir)
        {
            int n = Def.BulletsPerShot + extraBullets;
            float dmg = Def.Damage * damageMult * owner.damageMult;
            int pierce = Def.Pierce + extraPierce + owner.extraPierce;
            float life = Def.Range / Def.BulletSpeed;
            Vector3 pos = muzzle != null ? muzzle.position : transform.position + Vector3.up * 0.5f;

            for (int i = 0; i < n; i++)
            {
                float angle;
                if (n == 1) angle = Random.Range(-Def.Spread * 0.5f, Def.Spread * 0.5f);
                else
                {
                    float spread = Mathf.Max(Def.Spread, 6f * (n - 1));
                    angle = -spread * 0.5f + spread * i / (n - 1) + Random.Range(-2f, 2f);
                }
                Vector3 d = Quaternion.Euler(0f, angle, 0f) * dir;
                var b = ArenaManager.Instance.GetBullet();
                b.Launch(pos, d, Def.BulletSpeed, dmg, pierce, life, Def.BulletScale, Def.SplashRadius, Def.Color, ArenaManager.Instance.ReturnBullet);
            }
        }
    }
}
