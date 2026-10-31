using System;
using ItemQualities;
using R2API;
using RoR2;
using UnityEngine;
using Spec = MoreQuality.Visuals.Spec;
using Shape = UnityEngine.ParticleSystemShapeType;

namespace MoreQuality
{
    // Every MoreQuality visual, built once the game's assets are loaded (see ContentProvider).
    internal static class Fx
    {
        // One-shot, networked (Visuals.Spawn).
        public static GameObject MetronomeTick;
        public static GameObject Ignite;
        public static GameObject ChocolateEaten;
        public static GameObject BloodWave;
        public static GameObject PoisonZone;
        public static GameObject Meltdown;
        public static GameObject SelfDestruct;
        public static GameObject BannerSlam;
        public static GameObject Stomp;
        public static GameObject SpiritBurst;
        public static GameObject CoinBurst;

        // Networked pickups.
        public static GameObject MoltenCoin;
        public static GameObject Chocolate;

        private static readonly Color Gold = new Color(1f, 0.8f, 0.25f);
        private static readonly Color Fire = new Color(1f, 0.45f, 0.08f);
        private static readonly Color Ember = new Color(1f, 0.75f, 0.3f);
        private static readonly Color Blood = new Color(0.75f, 0.02f, 0.08f);
        private static readonly Color Cocoa = new Color(0.35f, 0.18f, 0.08f);
        private static readonly Color Mint = new Color(0.3f, 0.85f, 0.45f);

        internal static void Register()
        {
            ContentProvider.AddBuilder(Build);
        }

        private static void Build(RoR2.ContentManagement.ContentPack pack)
        {
            MetronomeTick = Visuals.Effect("MoreQualityMetronomeTick", 1f, "Play_UI_menuClick", root =>
            {
                Visuals.Particles(root, "Pulse", new Spec { Color = Gold, Size = 0.2f, Lifetime = 0.35f, Speed = 3f, Burst = 36, Shape = Shape.Circle, Radius = 0.3f, Shell = true, Shrink = true, Rotation = new Vector3(90f, 0f, 0f) });
                Visuals.Particles(root, "Flash", new Spec { Color = Gold, Size = 1.2f, Lifetime = 0.15f, Burst = 1, Shape = Shape.Sphere, Radius = 0.01f, Local = true });
            }, follow: true);
            Ignite = Visuals.Effect("MoreQualityIgnite", 1.5f, null, root =>
            {
                Visuals.Particles(root, "Flames", new Spec { Color = Fire, Size = 0.5f, Lifetime = 0.5f, Speed = 1.5f, Burst = 10, Shape = Shape.Sphere, Radius = 0.4f, Gravity = -0.3f, Shrink = true });
                Visuals.Particles(root, "Embers", new Spec { Color = Ember, Size = 0.08f, Lifetime = 0.9f, Speed = 2.5f, Burst = 12, Shape = Shape.Sphere, Radius = 0.3f, Gravity = -0.2f });
            });
            ChocolateEaten = Visuals.Effect("MoreQualityChocolateEaten", 2f, "Play_item_proc_bandolierPickup", root =>
            {
                Visuals.Particles(root, "Crumbs", new Spec { Color = Cocoa, Size = 0.15f, Lifetime = 0.8f, Speed = 4f, Burst = 25, Shape = Shape.Sphere, Radius = 0.2f, Gravity = 1f, Shrink = true });
                Visuals.Particles(root, "Ring", new Spec { Color = Mint, Size = 0.35f, Lifetime = 0.6f, Speed = 1.5f, Burst = 50, Shape = Shape.Circle, Radius = 0.1f, Shell = true, Shrink = true, Rotation = new Vector3(90f, 0f, 0f) });
            });
            // Blood offering: a crimson flash, a wave of blood skimming the ground, streaks thrown out and
            // falling back, splats left on the floor and a slow red mist.
            BloodWave = Visuals.Effect("MoreQualityBloodWave", 4f, "Play_bleedOnCritAndExplode_explode", root =>
            {
                var flat = new Vector3(90f, 0f, 0f);
                Visuals.Flash(root, new Color(1f, 0.1f, 0.1f), 1.5f, 6f, 0.5f);
                Visuals.Particles(root, "Core", new Spec { Material = Visuals.Kind.Glow, Color = new Color(1f, 0.2f, 0.15f), ColorEnd = new Color(0.4f, 0f, 0f), Size = 2f, Lifetime = 0.3f, Burst = 2, Shape = Shape.Sphere, Radius = 0.01f, Local = true, Grow = true });
                Visuals.Particles(root, "Wave", new Spec { Material = Visuals.Kind.Blood, Color = new Color(0.85f, 0.05f, 0.08f), ColorEnd = new Color(0.25f, 0f, 0.02f), Size = 0.55f, Lifetime = 0.55f, Speed = 2f, Burst = 140, Shape = Shape.Circle, Radius = 0.08f, Shell = true, Flat = true, Spin = true, Drag = 0.15f, Rotation = flat });
                Visuals.Particles(root, "WaveCrest", new Spec { Material = Visuals.Kind.Smoke, Color = new Color(0.6f, 0.02f, 0.05f), ColorEnd = new Color(0.15f, 0f, 0f), Size = 0.45f, Lifetime = 0.6f, Speed = 1.9f, Burst = 70, Shape = Shape.Circle, Radius = 0.08f, Shell = true, Grow = true, Spin = true, Drag = 0.2f, Rotation = flat });
                Visuals.Particles(root, "Streaks", new Spec { Material = Visuals.Kind.Spark, Color = new Color(0.95f, 0.1f, 0.12f), ColorEnd = new Color(0.35f, 0f, 0.02f), Size = 0.1f, Lifetime = 0.9f, Speed = 2.6f, Burst = 60, Shape = Shape.Hemisphere, Radius = 0.2f, Gravity = 1.6f, Stretch = true, Rotation = new Vector3(-90f, 0f, 0f) });
                Visuals.Particles(root, "Droplets", new Spec { Material = Visuals.Kind.Blood, Color = new Color(0.7f, 0.02f, 0.05f), Size = 0.14f, Lifetime = 1.2f, Speed = 1.5f, Burst = 45, Shape = Shape.Hemisphere, Radius = 0.3f, Gravity = 1.2f, Shrink = true, Spin = true, Rotation = new Vector3(-90f, 0f, 0f) });
                Visuals.Particles(root, "Splats", new Spec { Material = Visuals.Kind.Blood, Color = new Color(0.45f, 0f, 0.03f), ColorEnd = new Color(0.2f, 0f, 0.01f), Size = 0.5f, Lifetime = 3.5f, Burst = 28, Shape = Shape.Circle, Radius = 0.9f, Flat = true, Spin = true, Grow = true, Rotation = flat });
                Visuals.Particles(root, "Mist", new Spec { Material = Visuals.Kind.Smoke, Color = new Color(0.45f, 0f, 0.05f), ColorEnd = new Color(0.1f, 0f, 0f), Size = 0.9f, Lifetime = 2f, Speed = 0.5f, Burst = 22, Shape = Shape.Circle, Radius = 0.6f, Gravity = -0.03f, Grow = true, Spin = true, Noise = 0.3f, Drag = 0.3f, Rotation = flat });
            });

            // Pesticide cloud: low, billowing poison fog that churns in place, a stain on the ground, bubbles
            // rising and popping, wisps curling off the edge, and a sickly green glow while it lasts.
            PoisonZone = Visuals.Effect("MoreQualityPoisonZone", 30f, null, root =>
            {
                var flat = new Vector3(90f, 0f, 0f);
                var toxic = new Color(0.5f, 0.9f, 0.15f);
                Visuals.Flash(root, toxic, 1.3f, 2.5f, 0f);
                Visuals.Particles(root, "Burst", new Spec { Material = Visuals.Kind.Smoke, Color = new Color(0.6f, 1f, 0.25f), ColorEnd = new Color(0.2f, 0.4f, 0.05f), Size = 0.7f, Lifetime = 0.8f, Speed = 1.5f, Burst = 30, Shape = Shape.Circle, Radius = 0.1f, Grow = true, Spin = true, Drag = 0.5f, Rotation = flat });
                Visuals.Particles(root, "Fog", new Spec { Material = Visuals.Kind.Smoke, Color = new Color(0.4f, 0.75f, 0.1f), ColorEnd = new Color(0.15f, 0.35f, 0.05f), Size = 0.9f, Lifetime = 2.6f, Rate = 22f, Speed = 0.08f, Shape = Shape.Circle, Radius = 0.85f, Gravity = -0.015f, Grow = true, Spin = true, Noise = 0.25f, Rotation = flat });
                Visuals.Particles(root, "Stain", new Spec { Material = Visuals.Kind.Smoke, Color = new Color(0.25f, 0.5f, 0.05f), Size = 1.4f, Lifetime = 3f, Rate = 6f, Shape = Shape.Circle, Radius = 0.7f, Flat = true, Spin = true, Rotation = flat });
                Visuals.Particles(root, "Bubbles", new Spec { Material = Visuals.Kind.Glow, Color = new Color(0.8f, 1f, 0.35f), ColorEnd = new Color(0.4f, 0.8f, 0.1f), Size = 0.12f, Lifetime = 1.3f, Rate = 26f, Speed = 0.1f, Shape = Shape.Circle, Radius = 0.9f, Gravity = -0.12f, Shrink = true, Noise = 0.15f, Rotation = flat });
                Visuals.Particles(root, "Wisps", new Spec { Material = Visuals.Kind.Smoke, Color = new Color(0.55f, 0.9f, 0.2f), ColorEnd = new Color(0.1f, 0.25f, 0f), Size = 0.35f, Lifetime = 1.6f, Rate = 20f, Speed = 0.25f, Shape = Shape.Circle, Radius = 1f, Shell = true, Gravity = -0.06f, Grow = true, Spin = true, Noise = 0.4f, Rotation = flat });
                root.gameObject.AddComponent<Visuals.TimedZone>();
            });
            Meltdown = Visuals.Shockwave("MoreQualityMeltdown", new Color(1f, 0.85f, 0.2f), new Color(0.6f, 1f, 0.3f), "Play_alloyHunter_spawn_04_explosion");
            SelfDestruct = Visuals.Shockwave("MoreQualitySelfDestruct", new Color(0.35f, 0.8f, 1f), Ember, "Play_defectiveUnit_attack_melee_explosion");
            BannerSlam = Visuals.Shockwave("MoreQualityBannerSlam", Gold, new Color(0.8f, 0.65f, 0.45f), null);
            Stomp = Visuals.Shockwave("MoreQualityStomp", new Color(0.5f, 1f, 0.3f), new Color(0.7f, 0.9f, 0.5f), null);
            SpiritBurst = Visuals.Shockwave("MoreQualitySpiritBurst", new Color(0.4f, 1f, 0.85f), new Color(0.75f, 0.45f, 1f), null);
            CoinBurst = Visuals.Effect("MoreQualityCoinBurst", 1.5f, "Play_UI_coin", root =>
                Visuals.Particles(root, "Sparkles", new Spec { Color = Gold, Size = 0.15f, Lifetime = 0.7f, Speed = 3f, Burst = 15, Shape = Shape.Sphere, Radius = 0.2f, Gravity = 0.5f, Shrink = true }));

            MoltenCoin = Pickup(pack, "Prefabs/NetworkedObjects/BonusMoneyPack", "MoreQualityMoltenCoin", visual =>
            {
                var metal = Visuals.Solid(new Color(1f, 0.55f, 0.15f), Fire, 2.5f);
                var coin = Visuals.Model(visual, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.7f, 0.06f, 0.7f), metal);
                coin.transform.localEulerAngles = new Vector3(90f, 0f, 0f);
                Visuals.Model(visual, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.45f, 0.08f, 0.45f), Visuals.Solid(new Color(1f, 0.85f, 0.3f), Ember, 3f))
                    .transform.localEulerAngles = new Vector3(90f, 0f, 0f);
                Visuals.Particles(visual, "Flames", new Spec { Color = Fire, Size = 0.35f, Lifetime = 0.5f, Rate = 25f, Speed = 0.6f, Shape = Shape.Sphere, Radius = 0.3f, Gravity = -0.4f, Shrink = true });
                Visuals.Particles(visual, "Embers", new Spec { Color = Ember, Size = 0.06f, Lifetime = 1f, Rate = 12f, Speed = 0.8f, Shape = Shape.Sphere, Radius = 0.3f, Gravity = -0.2f });
            });
            Chocolate = Pickup(pack, "Prefabs/NetworkedObjects/HealPack", "MoreQualityChocolate", visual =>
            {
                Visuals.Model(visual, PrimitiveType.Cube, new Vector3(0.12f, 0f, 0f), new Vector3(0.36f, 0.12f, 0.5f), Visuals.Solid(Cocoa, Color.black, 0f));
                Visuals.Model(visual, PrimitiveType.Cube, new Vector3(-0.18f, 0f, 0f), new Vector3(0.3f, 0.15f, 0.53f), Visuals.Solid(Mint, Mint, 0.6f));
                Visuals.Particles(visual, "Sparkle", new Spec { Color = Mint, Size = 0.1f, Lifetime = 0.8f, Rate = 8f, Speed = 0.3f, Shape = Shape.Sphere, Radius = 0.4f, Shrink = true });
            });

            Marks();
        }

        // A copy of a vanilla pickup with its looks replaced by ours; the pickup logic stays the game's.
        private static GameObject Pickup(RoR2.ContentManagement.ContentPack pack, string source, string name, Action<Transform> build)
        {
            var original = LegacyResourcesAPI.Load<GameObject>(source);
            if (!original) return null;
            var prefab = PrefabAPI.InstantiateClone(original, name, true);
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            foreach (var ps in prefab.GetComponentsInChildren<ParticleSystem>(true))
            {
                var emission = ps.emission;
                emission.enabled = false;
            }
            foreach (var light in prefab.GetComponentsInChildren<Light>(true)) light.enabled = false;
            var visual = new GameObject("MoreQualityVisual").transform;
            visual.SetParent(prefab.transform, false);
            build(visual);
            visual.gameObject.AddComponent<Visuals.Spinner>();
            pack.networkedObjectPrefabs.Add(new[] { prefab });
            return prefab;
        }

        // Looping visuals that follow a body while it has one of our buffs.
        private static void Marks()
        {
            var flat = new Vector3(90f, 0f, 0f);
            BuffVisuals.Show(CustomBuffs.Molten, Visuals.Template("MoreQualityMoltenAura", root =>
            {
                Visuals.Particles(root, "Ring", new Spec { Color = Fire, Size = 0.18f, Lifetime = 0.6f, Rate = 80f, Speed = 0.2f, Shape = Shape.Circle, Radius = 1f, Shell = true, Gravity = -0.15f, Shrink = true, Rotation = flat });
                Visuals.Particles(root, "Embers", new Spec { Color = Ember, Size = 0.05f, Lifetime = 1.2f, Rate = 30f, Speed = 0.15f, Shape = Shape.Circle, Radius = 1f, Gravity = -0.08f, Rotation = flat });
            }), body => Items.MoltenCoin.AuraRadiusFor(body), true);

            BuffVisuals.Show(CustomBuffs.Pinned, Visuals.Template("MoreQualityPinnedMark", root =>
                Visuals.Particles(root, "Pins", new Spec { Color = new Color(0.85f, 0.85f, 0.95f), Size = 0.12f, Lifetime = 0.4f, Rate = 30f, Speed = 0.1f, Shape = Shape.Circle, Radius = 1f, Shell = true, Local = true, Rotation = flat })),
                body => body.radius * 1.2f, true);

            BuffVisuals.Show(CustomBuffs.Hunted, Visuals.Template("MoreQualityHuntedMark", root =>
            {
                Visuals.Particles(root, "Ring", new Spec { Color = new Color(1f, 0.25f, 0.1f), Size = 0.15f, Lifetime = 0.5f, Rate = 35f, Shape = Shape.Circle, Radius = 1f, Shell = true, Local = true, Rotation = flat });
                Visuals.Particles(root, "Rise", new Spec { Color = new Color(1f, 0.4f, 0.2f), Size = 0.08f, Lifetime = 0.8f, Rate = 10f, Speed = 1f, Shape = Shape.Circle, Radius = 0.8f, Gravity = -0.1f, Rotation = flat });
            }), body => body.radius * 1.4f, true);

            BuffVisuals.Show(CustomBuffs.Voodoo, Visuals.Template("MoreQualityVoodooMark", root =>
                Visuals.Particles(root, "Swirl", new Spec { Color = new Color(0.65f, 0.2f, 0.75f), Size = 0.12f, Lifetime = 0.9f, Rate = 14f, Speed = 0.3f, Shape = Shape.Sphere, Radius = 1f, Shell = true, Local = true, Shrink = true })),
                body => body.radius, false);

            BuffVisuals.Show(CustomBuffs.Slowed, Visuals.Template("MoreQualitySlowedMark", root =>
                Visuals.Particles(root, "Dust", new Spec { Color = new Color(0.45f, 0.65f, 1f), Size = 0.15f, Lifetime = 0.6f, Rate = 12f, Speed = 0.1f, Shape = Shape.Circle, Radius = 1f, Gravity = 0.1f, Rotation = flat })),
                body => body.radius, true);

            BuffVisuals.Show(CustomBuffs.Overcharged, Visuals.Template("MoreQualityOverchargedMark", root =>
                Visuals.Particles(root, "Sparks", new Spec { Color = new Color(0.4f, 0.85f, 1f), Size = 0.07f, Lifetime = 0.3f, Rate = 40f, Speed = 2f, Shape = Shape.Sphere, Radius = 1f, Shell = true, Shrink = true })),
                body => body.radius, false);

            BuffVisuals.Show(CustomBuffs.GoldRush, Visuals.Template("MoreQualityGoldRushMark", root =>
                Visuals.Particles(root, "Glitter", new Spec { Color = Gold, Size = 0.08f, Lifetime = 0.8f, Rate = 20f, Speed = 0.4f, Shape = Shape.Sphere, Radius = 1f, Gravity = 0.2f, Shrink = true })),
                body => body.radius * 1.3f, false);

            BuffVisuals.Show(CustomBuffs.BloodOffering, Visuals.Template("MoreQualityBloodOfferingMark", root =>
            {
                Visuals.Particles(root, "Drips", new Spec { Color = Blood, Size = 0.09f, Lifetime = 0.9f, Rate = 14f, Speed = 0.1f, Shape = Shape.Sphere, Radius = 1f, Gravity = 0.5f, Shrink = true });
                Visuals.Particles(root, "Orbit", new Spec { Color = new Color(0.55f, 0f, 0.05f), Size = 0.2f, Lifetime = 0.6f, Rate = 10f, Shape = Shape.Circle, Radius = 1.1f, Shell = true, Local = true, Rotation = flat });
            }), body => body.radius * (1f + body.GetBuffCount(CustomBuffs.BloodOffering) / 150f), false);

            BuffVisuals.Show(CustomBuffs.Wounded, Visuals.Template("MoreQualityWoundedMark", root =>
                Visuals.Particles(root, "Bleed", new Spec { Color = new Color(0.6f, 0.05f, 0.05f), Size = 0.1f, Lifetime = 0.7f, Rate = 10f, Speed = 0.2f, Shape = Shape.Sphere, Radius = 0.8f, Gravity = 0.6f, Shrink = true })),
                body => body.radius, false);
        }
    }
}
