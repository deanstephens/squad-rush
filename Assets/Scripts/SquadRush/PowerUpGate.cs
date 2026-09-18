using TMPro;
using UnityEngine;

namespace SquadRush
{
    public enum PowerUpType
    {
        AddUnits,
        MultiplyUnits,
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
        Material mat;
        bool collected;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public bool IsPositive => Type == PowerUpType.MultiplyUnits ? Value >= 1f : Value > 0f;

        public void Setup(PowerUpType type, float value, float halfWidth)
        {
            Type = type;
            Value = value;
            HalfWidth = halfWidth;
            shotStep = type switch
            {
                PowerUpType.AddUnits => 0.25f,
                PowerUpType.MultiplyUnits => 0.02f,
                _ => 0.5f,
            };
            mat = panel.material;
            Refresh();
        }

        /// <summary>Bullets that hit the gate nudge its value in the player's favour.</summary>
        public void OnShot()
        {
            if (collected) return;
            Value += shotStep;
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
                    return (v >= 0 ? "+" : "") + v;
                }
                case PowerUpType.MultiplyUnits:
                    return "x" + Value.ToString("0.0");
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
            return (v >= 0 ? "+" : "") + v + "%";
        }

        public void Collect(Squad squad)
        {
            if (collected) return;
            collected = true;

            switch (Type)
            {
                case PowerUpType.AddUnits: squad.AddUnits(Mathf.RoundToInt(Value)); break;
                case PowerUpType.MultiplyUnits: squad.MultiplyUnits(Value); break;
                case PowerUpType.Damage: squad.damage *= 1f + Value / 100f; break;
                case PowerUpType.FireRate: squad.fireRate *= 1f + Value / 100f; break;
                case PowerUpType.MoveSpeed: squad.moveSpeed *= 1f + Value / 100f; break;
                case PowerUpType.ProjectileSpeed: squad.projectileSpeed *= 1f + Value / 100f; break;
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
