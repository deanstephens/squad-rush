using System;
using System.Collections.Generic;
using UnityEngine;

namespace SquadRush
{
    /// <summary>A run-only upgrade offered after each boss (the roguelite "pick one of three").</summary>
    public class Perk
    {
        public string Title;
        public string Description;
        public Action<GameManager> Apply;

        public Perk(string title, string description, Action<GameManager> apply)
        {
            Title = title;
            Description = description;
            Apply = apply;
        }
    }

    public static class PerkLibrary
    {
        public static readonly List<Perk> All = new List<Perk>
        {
            new Perk("Reinforcements", "+6 units", gm => gm.squad.AddUnits(6)),
            new Perk("Recruit Drive", "+10 units", gm => gm.squad.AddUnits(10)),
            new Perk("Heavy Rounds", "+20% damage", gm => gm.squad.damageBonus += 0.2f),
            new Perk("Rapid Fire", "+15% fire rate", gm => gm.squad.fireRateBonus += 0.15f),
            new Perk("Piercing Shots", "Bullets pass through one extra enemy", gm => gm.squad.pierce += 1),
            new Perk("Big Bullets", "Larger bullets, +10% damage", gm => { gm.squad.projectileScale += 0.3f; gm.squad.damageBonus += 0.1f; }),
            new Perk("Barrier", "Absorb the next 2 hits", gm => gm.squad.shields += 2),
            new Perk("Brakes", "Treadmill runs 10% slower", gm => gm.treadmillSpeedMultiplier = Mathf.Max(0.6f, gm.treadmillSpeedMultiplier - 0.1f)),
            new Perk("Velocity", "+20% bullet speed", gm => gm.squad.projectileSpeedBonus += 0.2f),
            new Perk("Nimble", "+20% move speed", gm => gm.squad.moveSpeedBonus += 0.2f),
        };

        public static Perk[] RollThree()
        {
            var pool = new List<Perk>(All);
            var result = new Perk[Mathf.Min(3, pool.Count)];
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
