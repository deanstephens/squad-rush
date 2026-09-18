using UnityEngine;

namespace SquadRush
{
    public enum UpgradeType
    {
        StartUnits,
        Damage,
        FireRate,
    }

    /// <summary>Persistent progression between runs: coins, best distance and permanent upgrades.</summary>
    public static class MetaProgression
    {
        const string CoinsKey = "sr_coins";
        const string BestKey = "sr_best";
        public const int MaxLevel = 10;

        public static int Coins
        {
            get => PlayerPrefs.GetInt(CoinsKey, 0);
            private set => PlayerPrefs.SetInt(CoinsKey, value);
        }

        public static float BestDistance
        {
            get => PlayerPrefs.GetFloat(BestKey, 0f);
            private set => PlayerPrefs.SetFloat(BestKey, value);
        }

        public static int Level(UpgradeType t) => PlayerPrefs.GetInt("sr_lvl_" + t, 0);

        public static int Cost(UpgradeType t) => Mathf.RoundToInt(40f * Mathf.Pow(Level(t) + 1, 1.6f));

        public static int StartUnits => 5 + 2 * Level(UpgradeType.StartUnits);
        public static float Damage => 2f * (1f + 0.15f * Level(UpgradeType.Damage));
        public static float FireRate => 2f * (1f + 0.10f * Level(UpgradeType.FireRate));

        public static string Title(UpgradeType t) => t switch
        {
            UpgradeType.StartUnits => "Start Units",
            UpgradeType.Damage => "Damage",
            UpgradeType.FireRate => "Fire Rate",
            _ => t.ToString(),
        };

        public static string CurrentValue(UpgradeType t) => t switch
        {
            UpgradeType.StartUnits => StartUnits.ToString(),
            UpgradeType.Damage => Damage.ToString("0.0"),
            UpgradeType.FireRate => FireRate.ToString("0.0") + "/s",
            _ => "",
        };

        public static bool TryBuy(UpgradeType t)
        {
            if (Level(t) >= MaxLevel) return false;
            int cost = Cost(t);
            if (Coins < cost) return false;
            Coins -= cost;
            PlayerPrefs.SetInt("sr_lvl_" + t, Level(t) + 1);
            PlayerPrefs.Save();
            return true;
        }

        public static void AddCoins(int n)
        {
            Coins += Mathf.Max(0, n);
            PlayerPrefs.Save();
        }

        public static void RecordDistance(float d)
        {
            if (d > BestDistance)
            {
                BestDistance = d;
                PlayerPrefs.Save();
            }
        }
    }
}
