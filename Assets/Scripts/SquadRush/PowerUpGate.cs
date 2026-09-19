using TMPro;
using UnityEngine;

namespace SquadRush
{
    public enum PowerUpType
    {
        AddUnits,
        Damage,
        FireRate,
        MoveSpeed,
        ProjectileSpeed,
    }

    /// <summary>
    /// A gate the squad can run through to change its stats. Positive gates are blue,
    /// negative gates are red. Shooting a gate improves its value before you reach it.
    /// </summary>
    [RequireComponent(typeof(ScrollingObject))]
    public class PowerUpGate : MonoBehaviour
    {
        [Header("Wiring")]
        public Renderer panel;
        public TMP_Text label;
        public Color goodColor = new Color(0.25f, 0.55f, 1f, 0.55f);
        public Color badColor = new Color(1f, 0.25f, 0.3f, 0.55f);

        public PowerUpType Type { get; private set; }
        public float Value { get; private set; }
        public float HalfWidth { get; private set; } = 1.3f;

        float shotStep;
        float shotCap;
        Material mat;
        bool collected;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public bool IsPositive => Value > 0f;

        public void Setup(PowerUpType type, float value, float halfWidth)
        {
            Type = type;
            Value = value;
            HalfWidth = halfWidth;
            shotStep = type == PowerUpType.AddUnits ? 0.1f : 0.25f;
            // Shooting nudges a good gate up by at most half again, or drags a bad one up to neutral.
            shotCap = value > 0f ? value * 1.5f : 0f;
            mat = panel.material;
            Refresh();
        }

        /// <summary>Bullets that hit the gate nudge its value in the player's favour.</summary>
        public void OnShot()
        {
            if (collected || Value >= shotCap) return;
            Value = Mathf.Min(Value + shotStep, shotCap);
            Refresh();
        }

        void Refresh()
        {
            if (mat != null) mat.SetColor(BaseColorId, IsPositive ? goodColor : badColor);
            if (label != null) label.text = Describe();
        }

        string Describe()
        {
            switch (Type)
            {
                case PowerUpType.AddUnits:
                {
                    int v = Mathf.RoundToInt(Value);
                    return v > 0 ? "+" + v : v.ToString();
                }
                case PowerUpType.Damage:
                    return "DMG " + Pct();
                case PowerUpType.FireRate:
                    return "RATE " + Pct();
                case PowerUpType.MoveSpeed:
                    return "SPEED " + Pct();
                case PowerUpType.ProjectileSpeed:
                    return "BULLET " + Pct();
            }
            return "";
        }

        string Pct()
        {
            int v = Mathf.RoundToInt(Value);
            return (v > 0 ? "+" : "") + v + "%";
        }

        public void Collect(Squad squad)
        {
            if (collected) return;
            collected = true;

            switch (Type)
            {
                case PowerUpType.AddUnits: squad.AddUnits(Mathf.RoundToInt(Value)); break;
                case PowerUpType.Damage: squad.damageBonus = Mathf.Max(-0.5f, squad.damageBonus + Value / 100f); break;
                case PowerUpType.FireRate: squad.fireRateBonus = Mathf.Max(-0.5f, squad.fireRateBonus + Value / 100f); break;
                case PowerUpType.MoveSpeed: squad.moveSpeedBonus += Value / 100f; break;
                case PowerUpType.ProjectileSpeed: squad.projectileSpeedBonus += Value / 100f; break;
            }
            squad.NotifyChanged();

            Fx.Burst(panel.transform.position, IsPositive ? goodColor : badColor, 10, 0.14f);
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (mat != null) Destroy(mat);
        }
    }
}
