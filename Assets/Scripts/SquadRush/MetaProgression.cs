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

        /// <summary>Treadmill level the next run starts on. Advances when a boss dies.</summary>
        public static int CurrentLevel
        {
            get => Mathf.Max(1, PlayerPrefs.GetInt("sr_level", 1));
            set
            {
                PlayerPrefs.SetInt("sr_level", Mathf.Max(1, value));
                if (value > BestLevel) PlayerPrefs.SetInt("sr_best_level", value);
                PlayerPrefs.Save();
            }
        }

        public static int BestLevel => Mathf.Max(1, PlayerPrefs.GetInt("sr_best_level", 1));

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

        /// <summary>The single gun carried into the arena. Falls back to the pistol if the saved one is locked.</summary>
        public static string SelectedGun
        {
            get
            {
                string id = PlayerPrefs.GetString(LoadoutKey, GunLibrary.DefaultGunId);
                return GunLibrary.Get(id) != null && IsGunUnlocked(id) ? id : GunLibrary.DefaultGunId;
            }
            set
            {
                PlayerPrefs.SetString(LoadoutKey, value);
                PlayerPrefs.Save();
            }
        }

        public static int GunLevel(string id) => PlayerPrefs.GetInt("sr_gunlvl_" + id, 0);

        public static bool TryUpgradeGun(GunDef def)
        {
            int lvl = GunLevel(def.Id);
            if (lvl >= GunDef.MaxLevel) return false;
            if (!TrySpendScrap(def.UpgradeCost(lvl))) return false;
            PlayerPrefs.SetInt("sr_gunlvl_" + def.Id, lvl + 1);
            PlayerPrefs.Save();
            return true;
        }

        public static bool HasMod(string modId) => PlayerPrefs.GetInt("sr_mod_" + modId, 0) == 1;

        public static bool TryBuyMod(GunMod mod)
        {
            if (HasMod(mod.Id)) return false;
            if (!TrySpendScrap(mod.Cost)) return false;
            PlayerPrefs.SetInt("sr_mod_" + mod.Id, 1);
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
