using System.Collections.Generic;
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

        // ---------------------------------------------------------------- scrap (arena currency)

        const string ScrapKey = "sr_scrap";
        const string LoadoutKey = "sr_loadout";
        const string BestArenaKey = "sr_best_arena";
        public const int LoadoutSlots = 3;

        /// <summary>Materials gathered in treadmill runs. Spent in the Armory on guns.</summary>
        public static int Scrap
        {
            get => PlayerPrefs.GetInt(ScrapKey, 0);
            private set => PlayerPrefs.SetInt(ScrapKey, value);
        }

        public static float BestArenaTime
        {
            get => PlayerPrefs.GetFloat(BestArenaKey, 0f);
            private set => PlayerPrefs.SetFloat(BestArenaKey, value);
        }

        public static void AddScrap(int n)
        {
            Scrap += Mathf.Max(0, n);
            PlayerPrefs.Save();
        }

        public static bool TrySpendScrap(int n)
        {
            if (Scrap < n) return false;
            Scrap -= n;
            PlayerPrefs.Save();
            return true;
        }

        public static void RecordArenaTime(float seconds)
        {
            if (seconds > BestArenaTime)
            {
                BestArenaTime = seconds;
                PlayerPrefs.Save();
            }
        }

        public static bool IsGunUnlocked(string id) => id == GunLibrary.DefaultGunId || PlayerPrefs.GetInt("sr_gun_" + id, 0) == 1;

        public static void UnlockGun(string id)
        {
            PlayerPrefs.SetInt("sr_gun_" + id, 1);
            PlayerPrefs.Save();
        }

        /// <summary>Gun ids equipped for the arena, in slot order. Always contains at least one unlocked gun.</summary>
        public static List<string> EquippedGuns
        {
            get
            {
                var result = new List<string>();
                foreach (var id in PlayerPrefs.GetString(LoadoutKey, GunLibrary.DefaultGunId).Split(','))
                    if (GunLibrary.Get(id) != null && IsGunUnlocked(id) && !result.Contains(id) && result.Count < LoadoutSlots)
                        result.Add(id);
                if (result.Count == 0) result.Add(GunLibrary.DefaultGunId);
                return result;
            }
            set
            {
                PlayerPrefs.SetString(LoadoutKey, string.Join(",", value));
                PlayerPrefs.Save();
            }
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
