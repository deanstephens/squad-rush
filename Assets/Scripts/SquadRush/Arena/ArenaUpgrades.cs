using System;
using System.Collections.Generic;
using UnityEngine;

namespace SquadRush.Arena
{
    /// <summary>One level-up option in the arena.</summary>
    public class ArenaUpgrade
    {
        public string Id;
        public string Title;
        public string Description;
        public int MaxStacks;
        public bool IsGunPerk;
        public Action<ArenaManager> Apply;

        public ArenaUpgrade(string id, string title, string description, Action<ArenaManager> apply, int maxStacks = 1, bool isGunPerk = false)
        {
            Id = id;
            Title = title;
            Description = description;
            Apply = apply;
            MaxStacks = maxStacks;
            IsGunPerk = isGunPerk;
        }
    }

    public static class ArenaUpgrades
    {
        static readonly List<ArenaUpgrade> Generic = new List<ArenaUpgrade>
        {
            new ArenaUpgrade("gen_tough", "Toughness", "+25 max HP and heal 25", m => { m.player.maxHp += 25f; m.player.Heal(25f); }, 4),
            new ArenaUpgrade("gen_medkit", "Medkit", "Heal 50% of max HP", m => m.player.Heal(m.player.maxHp * 0.5f), 99),
            new ArenaUpgrade("gen_sprint", "Sprint", "+12% move speed", m => m.player.moveSpeed *= 1.12f, 3),
            new ArenaUpgrade("gen_magnet", "Magnet", "+40% pickup radius", m => m.player.pickupRadius *= 1.4f, 3),
            new ArenaUpgrade("gen_regen", "Regeneration", "+1 HP per second", m => m.player.regenPerSecond += 1f, 3),
            new ArenaUpgrade("gen_armor", "Armor", "Take 15% less damage", m => m.player.damageTakenMult *= 0.85f, 3),
        };

        /// <summary>Three options: up to two from the equipped gun's own perk list, the rest generic.</summary>
        public static ArenaUpgrade[] RollThree(ArenaManager am)
        {
            var gunPool = new List<ArenaUpgrade>();
            var gun = am.player.Gun;
            if (gun != null)
            {
                foreach (var perk in gun.Def.Perks)
                {
                    var p = perk;
                    if (am.TimesTaken(p.Id) >= p.MaxStacks) continue;
                    gunPool.Add(new ArenaUpgrade(p.Id, p.Title, p.Description, m => p.Apply(m.player.Gun.Stats), p.MaxStacks, true));
                }
            }
            var genericPool = new List<ArenaUpgrade>();
            foreach (var g in Generic)
                if (am.TimesTaken(g.Id) < g.MaxStacks) genericPool.Add(g);

            var result = new List<ArenaUpgrade>();
            int gunCount = Mathf.Min(2, gunPool.Count);
            for (int i = 0; i < gunCount; i++) result.Add(TakeRandom(gunPool));
            while (result.Count < 3 && (genericPool.Count > 0 || gunPool.Count > 0))
            {
                bool useGun = genericPool.Count == 0 || (gunPool.Count > 0 && UnityEngine.Random.value < 0.35f);
                result.Add(TakeRandom(useGun ? gunPool : genericPool));
            }
            return result.ToArray();
        }

        static ArenaUpgrade TakeRandom(List<ArenaUpgrade> pool)
        {
            int idx = UnityEngine.Random.Range(0, pool.Count);
            var u = pool[idx];
            pool.RemoveAt(idx);
            return u;
        }
    }
}
