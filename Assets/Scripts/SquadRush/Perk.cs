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
            new Perk("Reinforcements", "+15 units", gm => gm.squad.AddUnits(15)),
            new Perk("Clone Army", "Double your units", gm => gm.squad.MultiplyUnits(2f)),
            new Perk("Heavy Rounds", "+40% damage", gm => gm.squad.damage *= 1.4f),
            new Perk("Rapid Fire", "+30% fire rate", gm => gm.squad.fireRate *= 1.3f),
            new Perk("Piercing Shots", "Bullets pass through one extra target", gm => gm.squad.pierce += 1),
            new Perk("Big Bullets", "Larger bullets, +20% damage", gm => { gm.squad.projectileScale *= 1.5f; gm.squad.damage *= 1.2f; }),
            new Perk("Barrier", "Absorb the next 2 hits", gm => gm.squad.shields += 2),
            new Perk("Brakes", "Treadmill runs 15% slower", gm => gm.treadmillSpeedMultiplier *= 0.85f),
            new Perk("Velocity", "+30% bullet speed", gm => gm.squad.projectileSpeed *= 1.3f),
            new Perk("Nimble", "+30% move speed", gm => gm.squad.moveSpeed *= 1.3f),
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
