using System;
using System.Collections.Generic;
using UnityEngine;

namespace SquadRush
{
    /// <summary>
    /// Mutable stat block for one gun during a run. Starts from the gun's level and owned mods,
    /// then run perks stack on top. Every mechanic the perks can touch lives here.
    /// </summary>
    public class GunStats
    {
        public float damageMult = 1f;
        public float fireRateMult = 1f;
        public float spreadMult = 1f;
        public float rangeMult = 1f;
        public float bulletSpeedMult = 1f;
        public float bulletScaleMult = 1f;
        public float splashRadiusMult = 1f;
        public int extraBullets;
        public int extraPierce;

        public float critChance;
        public float critMult = 2f;
        public int ricochet;                 // extra targets a bullet jumps to after a hit
        public float focusStackPct;          // pistol: +X per consecutive hit on the same target
        public float focusMaxPct;
        public float lowHpBonus;             // extra damage vs enemies under 30% HP
        public float fullHpBonus;            // extra damage vs untouched enemies
        public float pointBlankBonus;        // extra damage vs enemies within 3.5 m
        public float piercedBonusPct;        // rifle: +X per enemy already pierced by the bullet
        public float burnPct;                // burn deals this fraction of hit damage per second
        public float burnDuration;
        public float slowPct;
        public float slowDuration;
        public float knockback;
        public float heatRampPerSec;         // fire rate ramps up while continuously firing
        public float heatMax;
        public int clusterCount;             // rocket: extra mini explosions per hit
    }

    /// <summary>A permanent, Scrap-purchased modification to one gun.</summary>
    public class GunMod
    {
        public string Id, Name, Description;
        public int Cost;
        public Action<GunStats> Apply;
        public GunMod(string id, string name, string desc, int cost, Action<GunStats> apply) { Id = id; Name = name; Description = desc; Cost = cost; Apply = apply; }
    }

    /// <summary>A run-only level-up perk specific to one gun.</summary>
    public class GunPerk
    {
        public string Id, Title, Description;
        public int MaxStacks = 1;
        public Action<GunStats> Apply;
        public GunPerk(string id, string title, string desc, Action<GunStats> apply, int maxStacks = 1) { Id = id; Title = title; Description = desc; Apply = apply; MaxStacks = maxStacks; }
    }

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
        public List<GunMod> Mods = new List<GunMod>();
        public List<GunPerk> Perks = new List<GunPerk>();

        public const int MaxLevel = 5;
        public const float DamagePerLevel = 0.10f;
        public const float FireRatePerLevel = 0.05f;

        public int UpgradeCost(int currentLevel) => 40 + Cost / 2 + currentLevel * (30 + Cost / 4);

        public string StatsLine(GunStats s)
        {
            float dmg = Damage * s.damageMult;
            float rate = FireRate * s.fireRateMult;
            int bullets = BulletsPerShot + s.extraBullets;
            int pierce = Pierce + s.extraPierce;
            string line = "DMG " + dmg.ToString("0.#") + (bullets > 1 ? " x" + bullets : "") +
                          "   RATE " + rate.ToString("0.#") + "/s" +
                          "   RANGE " + (Range * s.rangeMult).ToString("0");
            if (pierce > 0) line += "   PIERCE " + pierce;
            if (SplashRadius > 0f) line += "   SPLASH " + (SplashRadius * s.splashRadiusMult).ToString("0.#");
            if (Spread > 0f) line += "   SPREAD " + (Spread * s.spreadMult).ToString("0") + "°";
            if (s.critChance > 0f) line += "   CRIT " + Mathf.RoundToInt(s.critChance * 100f) + "%";
            return line;
        }
    }

    public static class GunLibrary
    {
        public const string DefaultGunId = "pistol";

        public static readonly List<GunDef> All = new List<GunDef>
        {
            new GunDef
            {
                Id = "pistol", Name = "Pistol", Description = "Reliable sidearm. Rewards staying on one target.",
                Cost = 0, Damage = 5f, FireRate = 3f, Range = 9f, BulletSpeed = 20f, Color = new Color(1f, 0.9f, 0.3f),
                Mods =
                {
                    new GunMod("pistol_barrel", "Match Barrel", "+20% range", 40, s => s.rangeMult *= 1.2f),
                    new GunMod("pistol_trigger", "Hair Trigger", "+20% fire rate", 60, s => s.fireRateMult *= 1.2f),
                    new GunMod("pistol_hollow", "Hollow Points", "+25% damage", 80, s => s.damageMult *= 1.25f),
                    new GunMod("pistol_sight", "Marksman Sight", "15% crit chance, 2x crit damage", 120, s => { s.critChance += 0.15f; s.critMult = Mathf.Max(s.critMult, 2f); }),
                },
                Perks =
                {
                    new GunPerk("pistol_focus", "Focus Fire", "Each consecutive hit on the same target adds +12% damage (up to +120%)", s => { s.focusStackPct += 0.12f; s.focusMaxPct += 1.2f; }),
                    new GunPerk("pistol_doubletap", "Double Tap", "+1 bullet per shot", s => s.extraBullets += 1, 2),
                    new GunPerk("pistol_ricochet", "Ricochet", "Bullets bounce to one more nearby enemy", s => s.ricochet += 1, 2),
                    new GunPerk("pistol_quickdraw", "Quick Draw", "+30% fire rate", s => s.fireRateMult *= 1.3f, 2),
                    new GunPerk("pistol_executioner", "Executioner", "+60% damage to enemies below 30% HP", s => s.lowHpBonus += 0.6f),
                    new GunPerk("pistol_deadeye", "Deadeye", "+15% crit chance", s => s.critChance += 0.15f, 2),
                },
            },
            new GunDef
            {
                Id = "smg", Name = "SMG", Description = "A stream of light rounds. Gets hotter the longer it fires.",
                Cost = 60, Damage = 2.5f, FireRate = 10f, Range = 8f, Spread = 10f, BulletSpeed = 22f, BulletScale = 0.8f, Color = new Color(0.6f, 1f, 0.4f),
                Mods =
                {
                    new GunMod("smg_comp", "Recoil Compensator", "-35% spread", 50, s => s.spreadMult *= 0.65f),
                    new GunMod("smg_barrel", "Rapid Barrel", "+20% fire rate", 80, s => s.fireRateMult *= 1.2f),
                    new GunMod("smg_laser", "Laser Sight", "+20% range, +15% bullet speed", 90, s => { s.rangeMult *= 1.2f; s.bulletSpeedMult *= 1.15f; }),
                    new GunMod("smg_incendiary", "Incendiary Rounds", "Hits burn for 40% damage per second for 3 s", 150, s => { s.burnPct += 0.4f; s.burnDuration = Mathf.Max(s.burnDuration, 3f); }),
                },
                Perks =
                {
                    new GunPerk("smg_overheat", "Overheat", "Fire rate ramps up to +60% while firing continuously", s => { s.heatRampPerSec += 0.15f; s.heatMax += 0.6f; }, 2),
                    new GunPerk("smg_spray", "Spray & Pray", "+3 bullets per shot, +50% spread", s => { s.extraBullets += 3; s.spreadMult *= 1.5f; }),
                    new GunPerk("smg_shredder", "Shredder", "+1 pierce", s => s.extraPierce += 1, 3),
                    new GunPerk("smg_tracer", "Tracer Rounds", "+30% bullet speed, +20% damage", s => { s.bulletSpeedMult *= 1.3f; s.damageMult *= 1.2f; }),
                    new GunPerk("smg_hose", "Bullet Hose", "+35% fire rate", s => s.fireRateMult *= 1.35f, 2),
                    new GunPerk("smg_hollow", "Hollow Points", "+30% damage", s => s.damageMult *= 1.3f, 2),
                },
            },
            new GunDef
            {
                Id = "shotgun", Name = "Shotgun", Description = "Six pellets in a wide cone. Devastating up close.",
                Cost = 90, Damage = 4f, FireRate = 1.2f, Range = 6f, BulletsPerShot = 6, Spread = 35f, BulletSpeed = 18f, Color = new Color(1f, 0.6f, 0.25f),
                Mods =
                {
                    new GunMod("shotgun_choke", "Choke", "-30% spread", 60, s => s.spreadMult *= 0.7f),
                    new GunMod("shotgun_barrel", "Extra Barrel", "+2 pellets per shot", 100, s => s.extraBullets += 2),
                    new GunMod("shotgun_slugs", "Heavy Shot", "+35% damage", 120, s => s.damageMult *= 1.35f),
                    new GunMod("shotgun_dragon", "Dragon's Breath", "Pellets burn for 30% damage per second for 3 s", 170, s => { s.burnPct += 0.3f; s.burnDuration = Mathf.Max(s.burnDuration, 3f); }),
                },
                Perks =
                {
                    new GunPerk("shotgun_tight", "Tight Choke", "-30% spread", s => s.spreadMult *= 0.7f, 2),
                    new GunPerk("shotgun_wall", "Buckshot Wall", "+3 pellets per shot", s => s.extraBullets += 3, 2),
                    new GunPerk("shotgun_pointblank", "Point Blank", "+70% damage to enemies within 3.5 m", s => s.pointBlankBonus += 0.7f),
                    new GunPerk("shotgun_knock", "Knockback", "Pellets shove enemies back", s => s.knockback += 4f, 2),
                    new GunPerk("shotgun_pump", "Pump Master", "+30% fire rate", s => s.fireRateMult *= 1.3f, 2),
                    new GunPerk("shotgun_reach", "Long Barrel", "+35% range", s => s.rangeMult *= 1.35f),
                },
            },
            new GunDef
            {
                Id = "rifle", Name = "Rifle", Description = "Long range, high damage, punches through enemies.",
                Cost = 120, Damage = 14f, FireRate = 1.6f, Range = 15f, Pierce = 3, BulletSpeed = 32f, BulletScale = 1.1f, Color = new Color(0.4f, 0.9f, 1f),
                Mods =
                {
                    new GunMod("rifle_barrel", "Long Barrel", "+25% range", 60, s => s.rangeMult *= 1.25f),
                    new GunMod("rifle_ap", "AP Rounds", "+2 pierce", 100, s => s.extraPierce += 2),
                    new GunMod("rifle_heavy", "Heavy Rounds", "+30% damage", 130, s => s.damageMult *= 1.3f),
                    new GunMod("rifle_scope", "Scope", "25% crit chance, 2.5x crit damage", 180, s => { s.critChance += 0.25f; s.critMult = Mathf.Max(s.critMult, 2.5f); }),
                },
                Perks =
                {
                    new GunPerk("rifle_lineup", "Line 'Em Up", "+20% damage for every enemy the bullet has already pierced", s => s.piercedBonusPct += 0.2f, 2),
                    new GunPerk("rifle_penetrator", "Penetrator", "+2 pierce", s => s.extraPierce += 2, 2),
                    new GunPerk("rifle_headhunter", "Headhunter", "+50% damage to untouched enemies", s => s.fullHpBonus += 0.5f),
                    new GunPerk("rifle_bolt", "Rapid Bolt", "+30% fire rate", s => s.fireRateMult *= 1.3f, 2),
                    new GunPerk("rifle_twin", "Twin Barrel", "+1 bullet per shot", s => s.extraBullets += 1, 2),
                    new GunPerk("rifle_crit", "Sharpshooter", "+20% crit chance", s => s.critChance += 0.2f, 2),
                },
            },
            new GunDef
            {
                Id = "minigun", Name = "Minigun", Description = "Never stops firing. Takes a moment to spin up.",
                Cost = 200, Damage = 3f, FireRate = 18f, Range = 9f, Spread = 14f, BulletSpeed = 24f, BulletScale = 0.8f, Color = new Color(1f, 0.45f, 0.45f),
                Mods =
                {
                    new GunMod("minigun_vents", "Cooling Vents", "+20% fire rate", 90, s => s.fireRateMult *= 1.2f),
                    new GunMod("minigun_gyro", "Gyro Mount", "-35% spread", 110, s => s.spreadMult *= 0.65f),
                    new GunMod("minigun_tungsten", "Tungsten Rounds", "+25% damage", 150, s => s.damageMult *= 1.25f),
                    new GunMod("minigun_motor", "Overclocked Motor", "Fire rate ramps up to +40% while firing", 200, s => { s.heatRampPerSec += 0.1f; s.heatMax += 0.4f; }),
                },
                Perks =
                {
                    new GunPerk("minigun_spinup", "Spin Up", "Fire rate ramps up to +80% while firing continuously", s => { s.heatRampPerSec += 0.2f; s.heatMax += 0.8f; }, 2),
                    new GunPerk("minigun_suppress", "Suppression", "Hits slow enemies by 35% for 1.5 s", s => { s.slowPct = Mathf.Max(s.slowPct, 0.35f); s.slowDuration = Mathf.Max(s.slowDuration, 1.5f); }),
                    new GunPerk("minigun_storm", "Lead Storm", "+2 bullets per shot", s => s.extraBullets += 2, 2),
                    new GunPerk("minigun_belt", "Bigger Belt", "+25% damage", s => s.damageMult *= 1.25f, 3),
                    new GunPerk("minigun_hurricane", "Hurricane", "+25% fire rate", s => s.fireRateMult *= 1.25f, 2),
                    new GunPerk("minigun_shred", "Shredder", "+1 pierce", s => s.extraPierce += 1, 2),
                },
            },
            new GunDef
            {
                Id = "rocket", Name = "Rocket Launcher", Description = "Slow, but every hit explodes.",
                Cost = 260, Damage = 28f, FireRate = 0.8f, Range = 12f, BulletSpeed = 12f, BulletScale = 1.8f, SplashRadius = 2.6f, Color = new Color(0.9f, 0.5f, 1f),
                Mods =
                {
                    new GunMod("rocket_warhead", "Bigger Warhead", "+30% splash radius", 100, s => s.splashRadiusMult *= 1.3f),
                    new GunMod("rocket_he", "HE Rounds", "+25% damage", 140, s => s.damageMult *= 1.25f),
                    new GunMod("rocket_fuse", "Fast Fuse", "+25% fire rate", 160, s => s.fireRateMult *= 1.25f),
                    new GunMod("rocket_twin", "Twin Tubes", "+1 rocket per shot", 240, s => s.extraBullets += 1),
                },
                Perks =
                {
                    new GunPerk("rocket_cluster", "Cluster Bombs", "Every explosion spawns 3 smaller blasts", s => s.clusterCount += 3, 2),
                    new GunPerk("rocket_napalm", "Napalm", "Explosions burn for 35% damage per second for 3 s", s => { s.burnPct += 0.35f; s.burnDuration = Mathf.Max(s.burnDuration, 3f); }),
                    new GunPerk("rocket_shockwave", "Shockwave", "Explosions hurl enemies back", s => s.knockback += 7f, 2),
                    new GunPerk("rocket_bigboom", "Big Boom", "+40% splash radius", s => s.splashRadiusMult *= 1.4f, 2),
                    new GunPerk("rocket_reloader", "Speed Loader", "+35% fire rate", s => s.fireRateMult *= 1.35f, 2),
                    new GunPerk("rocket_payload", "Heavy Payload", "+35% damage", s => s.damageMult *= 1.35f, 2),
                },
            },
        };

        public static GunDef Get(string id)
        {
            foreach (var g in All) if (g.Id == id) return g;
            return null;
        }

        /// <summary>Stats for a gun as owned right now: level bonuses plus purchased mods, no run perks.</summary>
        public static GunStats BuildMetaStats(GunDef def)
        {
            var s = new GunStats();
            int lvl = MetaProgression.GunLevel(def.Id);
            s.damageMult *= 1f + GunDef.DamagePerLevel * lvl;
            s.fireRateMult *= 1f + GunDef.FireRatePerLevel * lvl;
            foreach (var m in def.Mods)
                if (MetaProgression.HasMod(m.Id)) m.Apply(s);
            return s;
        }
    }
}
