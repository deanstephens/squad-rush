using TMPro;
using UnityEngine;

namespace SquadRush
{
    /// <summary>
    /// A block coming down the treadmill. Shoot it down before it reaches the squad;
    /// if it makes contact, the squad loses units proportional to the obstacle's remaining health.
    /// </summary>
    [RequireComponent(typeof(ScrollingObject))]
    public class Obstacle : MonoBehaviour
    {
        [Header("Wiring")]
        public Transform body;
        public Renderer bodyRenderer;
        public TMP_Text label;

        [Header("Tuning")]
        [Tooltip("Units lost on contact = remaining health * this (minimum 1).")]
        public float contactUnitsPerHealth = 0.25f;

        public float MaxHealth { get; private set; }
        public float Health { get; private set; }
        public bool IsBoss { get; private set; }
        public int CoinValue { get; private set; }

        Color baseColor;
        Material mat;
        float flashTimer;
        bool dead;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public void Setup(float health, Vector3 size, Color color, bool isBoss, int coins)
        {
            MaxHealth = Health = health;
            IsBoss = isBoss;
            CoinValue = coins;
            baseColor = color;

            body.localScale = size;
            body.localPosition = new Vector3(0f, size.y * 0.5f, 0f);
            if (label != null)
                label.transform.localPosition = new Vector3(0f, size.y + 0.7f, 0f);

            mat = bodyRenderer.material; // per-instance so each block can flash independently
            mat.SetColor(BaseColorId, color);
            RefreshLabel();
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

        void Update()
        {
            if (flashTimer > 0f)
            {
                flashTimer -= Time.deltaTime;
                if (flashTimer <= 0f && mat != null) mat.SetColor(BaseColorId, baseColor);
            }
        }

        void RefreshLabel()
        {
            if (label != null) label.text = Mathf.Max(0, Mathf.CeilToInt(Health)).ToString();
        }

        void Kill()
        {
            dead = true;
            var gm = GameManager.Instance;
            if (gm != null) gm.AddCoins(CoinValue);
            Fx.Burst(body.position, baseColor, IsBoss ? 20 : 8, IsBoss ? 0.3f : 0.18f);
            if (IsBoss && gm != null) gm.OnBossKilled();
            Destroy(gameObject);
        }

        /// <summary>Called by the squad when this obstacle reaches it.</summary>
        public void OnSquadContact(Squad squad)
        {
            if (dead) return;
            dead = true;
            int lost = Mathf.Max(1, Mathf.CeilToInt(Health * contactUnitsPerHealth));
            squad.TakeHit(lost);
            Fx.Burst(body.position, baseColor, 6);
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (mat != null) Destroy(mat);
        }
    }
}
