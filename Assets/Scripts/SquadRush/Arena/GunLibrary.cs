using System.Collections.Generic;
using UnityEngine;

namespace SquadRush
{
    /// <summary>Static definition of a gun that can be bought in the Armory and carried into the arena.</summary>
    public class GunDef
    {
        public string Id;
        public string Name;
        public string Description;
        public int Cost;
        public float Damage;
        public float FireRate;
        public int BulletsPerShot = 1;
        public float Spread;          // total cone in degrees
        public float BulletSpeed = 18f;
        public float Range = 9f;
        public int Pierce;
        public float BulletScale = 1f;
        public float SplashRadius;    // 0 = no splash
        public Color Color = Color.yellow;

        public string StatsLine()
        {
            string s = "DMG " + Damage.ToString("0.#") + (BulletsPerShot > 1 ? " x" + BulletsPerShot : "") +
                       "   RATE " + FireRate.ToString("0.#") + "/s" +
                       "   RANGE " + Range.ToString("0");
            if (Pierce > 0) s += "   PIERCE " + Pierce;
            if (SplashRadius > 0f) s += "   SPLASH";
            return s;
        }
    }

    public static class GunLibrary
    {
        public const string DefaultGunId = "pistol";

        public static readonly List<GunDef> All = new List<GunDef>
        {
            new GunDef { Id = "pistol",  Name = "Pistol",   Description = "Reliable sidearm. Always available.",
                Cost = 0,   Damage = 5f,   FireRate = 3f,   Range = 9f,  BulletSpeed = 20f, Color = new Color(1f, 0.9f, 0.3f) },
            new GunDef { Id = "smg",     Name = "SMG",      Description = "Sprays a stream of light rounds.",
                Cost = 60,  Damage = 2.5f, FireRate = 10f,  Range = 8f,  Spread = 10f, BulletSpeed = 22f, BulletScale = 0.8f, Color = new Color(0.6f, 1f, 0.4f) },
            new GunDef { Id = "shotgun", Name = "Shotgun",  Description = "Six pellets in a wide cone. Devastating up close.",
                Cost = 90,  Damage = 4f,   FireRate = 1.2f, Range = 6f,  BulletsPerShot = 6, Spread = 35f, BulletSpeed = 18f, Color = new Color(1f, 0.6f, 0.25f) },
            new GunDef { Id = "rifle",   Name = "Rifle",    Description = "Long range, high damage, punches through enemies.",
                Cost = 120, Damage = 14f,  FireRate = 1.6f, Range = 15f, Pierce = 3, BulletSpeed = 32f, BulletScale = 1.1f, Color = new Color(0.4f, 0.9f, 1f) },
            new GunDef { Id = "minigun", Name = "Minigun",  Description = "Never stops firing. Never.",
                Cost = 200, Damage = 3f,   FireRate = 18f,  Range = 9f,  Spread = 14f, BulletSpeed = 24f, BulletScale = 0.8f, Color = new Color(1f, 0.45f, 0.45f) },
            new GunDef { Id = "rocket",  Name = "Rocket Launcher", Description = "Slow, but every hit explodes.",
                Cost = 260, Damage = 28f,  FireRate = 0.8f, Range = 12f, BulletSpeed = 12f, BulletScale = 1.8f, SplashRadius = 2.6f, Color = new Color(0.9f, 0.5f, 1f) },
        };

        public static GunDef Get(string id)
        {
            foreach (var g in All) if (g.Id == id) return g;
            return null;
        }
    }
}
