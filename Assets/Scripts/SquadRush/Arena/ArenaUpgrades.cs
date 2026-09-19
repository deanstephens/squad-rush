using System;
using System.Collections.Generic;
using UnityEngine;

namespace SquadRush.Arena
{
    /// <summary>One level-up option in the arena.</summary>
    public class ArenaUpgrade
    {
        public string Title;
        public string Description;
        public Action<ArenaManager> Apply;

        public ArenaUpgrade(string title, string description, Action<ArenaManager> apply)
        {
            Title = title;
            Description = description;
            Apply = apply;
        }
    }

    public static class ArenaUpgrades
    {
        static List<ArenaUpgrade> Generic(ArenaManager am) => new List<ArenaUpgrade>
        {
            new ArenaUpgrade("Firepower", "+20% damage on all guns", m => m.player.damageMult *= 1.2f),
            new ArenaUpgrade("Trigger Finger", "+15% fire rate on all guns", m => m.player.fireRateMult *= 1.15f),
            new ArenaUpgrade("Sprint", "+12% move speed", m => m.player.moveSpeed *= 1.12f),
            new ArenaUpgrade("Toughness", "+25 max HP and heal 25", m => { m.player.maxHp += 25f; m.player.Heal(25f); }),
            new ArenaUpgrade("Medkit", "Heal 50% of max HP", m => m.player.Heal(m.player.maxHp * 0.5f)),
            new ArenaUpgrade("Magnet", "+40% pickup radius", m => m.player.pickupRadius *= 1.4f),
            new ArenaUpgrade("Regeneration", "+1 HP per second", m => m.player.regenPerSecond += 1f),
            new ArenaUpgrade("Armor-Piercing", "All bullets pierce +1 enemy", m => m.player.extraPierce += 1),
        };

        public static ArenaUpgrade[] RollThree(ArenaManager am)
        {
            var pool = Generic(am);
            foreach (var gun in am.player.Guns)
            {
                string id = gun.Def.Id;
                string name = gun.Def.Name;
                pool.Add(new ArenaUpgrade(name + " +1", "+1 projectile per shot for " + name, m => { var g = m.player.FindGun(id); if (g != null) g.extraBullets += 1; }));
                pool.Add(new ArenaUpgrade(name + " Power", "+35% damage for " + name, m => { var g = m.player.FindGun(id); if (g != null) g.damageMult *= 1.35f; }));
                pool.Add(new ArenaUpgrade(name + " Speed", "+30% fire rate for " + name, m => { var g = m.player.FindGun(id); if (g != null) g.fireRateMult *= 1.3f; }));
            }

            var result = new ArenaUpgrade[Mathf.Min(3, pool.Count)];
            for (int i = 0; i < result.Length; i++)
            {
                int idx = UnityEngine.Random.Range(0, pool.Count);
                result[i] = pool[idx];
                pool.RemoveAt(idx);
            }
            return result;
        }
    }
}
