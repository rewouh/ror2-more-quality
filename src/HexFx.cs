using System;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;
using Spec = MoreQuality.Visuals.Spec;
using Shape = UnityEngine.ParticleSystemShapeType;

namespace MoreQuality
{
    // Visuals of the reworked quality Hexes (see Hallowed/Hexes.cs and HexesWorld.cs).
    internal static class HexFx
    {
        // One-shot, networked.
        public static GameObject Botched, BoneShard, PainLink, Snatch, ReaperMiss, ReaperHit, ReaperSwoop, CrownJump, PhantomPoof, Slip, GhostCatch;

        // Networked objects.
        public static GameObject GhostCarrier, BoneSpikes, StormCloud, WindZone;


        private static readonly Color Bone = new Color(0.93f, 0.9f, 0.8f);
        private static readonly Color Cursed = new Color(0.45f, 0.15f, 0.55f);
        private static readonly Color Gold = new Color(1f, 0.8f, 0.25f);
        private static readonly Color Rage = new Color(1f, 0.15f, 0.1f);
        private static readonly Vector3 Flat = new Vector3(90f, 0f, 0f);

        internal static void Register()
        {
            ContentProvider.AddBuilder(Build);
        }

        private static void Build(RoR2.ContentManagement.ContentPack pack)
        {
            Botched = Visuals.Effect("MoreQualityBotched", 2.5f, "Play_UI_menuClick", root =>
            {
                Visuals.Particles(root, "Smoke", new Spec { Material = Visuals.Kind.Smoke, Color = new Color(0.2f, 0.05f, 0.25f), ColorEnd = new Color(0.05f, 0f, 0.05f), Size = 0.8f, Lifetime = 1.4f, Speed = 1.2f, Burst = 18, Shape = Shape.Sphere, Radius = 0.4f, Gravity = -0.1f, Grow = true, Spin = true, Drag = 0.4f });
                Visuals.Particles(root, "Sparks", new Spec { Material = Visuals.Kind.Spark, Color = Cursed, Size = 0.08f, Lifetime = 0.5f, Speed = 4f, Burst = 20, Shape = Shape.Sphere, Radius = 0.2f, Gravity = 0.8f, Stretch = true });
            });
            BoneShard = Visuals.Effect("MoreQualityBoneShard", 2.5f, null, root =>
            {
                // A spinning shard of bone with a trail of bone dust; it shatters into chips where it lands.
                var bone = Visuals.Solid(Bone, new Color(0.25f, 0.22f, 0.15f), 0.4f);
                var shard = new GameObject("Shard").transform;
                shard.SetParent(root, false);
                Visuals.Model(shard, PrimitiveType.Cube, Vector3.zero, new Vector3(0.14f, 0.14f, 0.75f), bone);
                Visuals.Model(shard, PrimitiveType.Cube, new Vector3(0f, 0f, 0.42f), new Vector3(0.09f, 0.09f, 0.2f), bone).transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                Visuals.Model(shard, PrimitiveType.Sphere, new Vector3(0f, 0f, -0.38f), Vector3.one * 0.2f, bone);
                Visuals.Particles(root, "Dust", new Spec { Material = Visuals.Kind.Smoke, Color = new Color(0.85f, 0.8f, 0.7f), ColorEnd = new Color(0.4f, 0.38f, 0.32f), Size = 0.3f, Lifetime = 0.6f, Rate = 50f, Shape = Shape.Sphere, Radius = 0.1f, Grow = true, Spin = true });
                var impact = Visuals.Particles(root, "Impact", new Spec { Color = Bone, Size = 0.12f, Lifetime = 0.7f, Speed = 5f, Burst = 22, Shape = Shape.Sphere, Radius = 0.15f, Gravity = 1.5f, Shrink = true, Spin = true });
                var main = impact.main;
                main.playOnAwake = false;
                root.gameObject.AddComponent<Visuals.StreakMover>().duration = 0.3f;
            });
            PainLink = Visuals.Streak("MoreQualityPainLink", new Color(0.9f, 0.05f, 0.1f), 0.15f, null);
            Snatch = Visuals.Streak("MoreQualitySnatch", Gold, 0.3f, "Play_UI_coin");
            CrownJump = Visuals.Streak("MoreQualityCrownJump", Gold, 0.6f, null);
            ReaperMiss = Visuals.Effect("MoreQualityReaperMiss", 1.5f, null, root =>
                Visuals.Particles(root, "Slash", new Spec { Material = Visuals.Kind.Spark, Color = new Color(0.25f, 0.1f, 0.3f), ColorEnd = new Color(0f, 0f, 0f), Size = 0.3f, Lifetime = 0.35f, Speed = 0.5f, Burst = 50, Shape = Shape.Circle, Radius = 1.3f, Shell = true, Shrink = true, Rotation = new Vector3(0f, 90f, 0f) }));
            ReaperHit = Visuals.Effect("MoreQualityReaperHit", 2f, null, root =>
            {
                Visuals.Flash(root, Rage, 8f, 8f, 0.6f);
                Visuals.Particles(root, "Flash", new Spec { Color = Rage, Size = 4f, Lifetime = 0.3f, Burst = 2, Shape = Shape.Sphere, Radius = 0.01f, Local = true, Grow = true });
                Visuals.Particles(root, "Slash", new Spec { Material = Visuals.Kind.Spark, Color = Rage, Size = 0.3f, Lifetime = 0.5f, Speed = 1f, Burst = 60, Shape = Shape.Circle, Radius = 1.4f, Shell = true, Rotation = new Vector3(0f, 90f, 0f) });
            });
            ReaperSwoop = Visuals.Effect("MoreQualityReaperSwoop", 1.6f, null, Reaper, follow: true);
            PhantomPoof = Visuals.Effect("MoreQualityPhantomPoof", 2f, null, root =>
                Visuals.Particles(root, "Smoke", new Spec { Material = Visuals.Kind.Smoke, Color = new Color(0.6f, 0.6f, 0.7f), ColorEnd = new Color(0.2f, 0.2f, 0.25f), Size = 1f, Lifetime = 1.2f, Speed = 1.5f, Burst = 20, Shape = Shape.Sphere, Radius = 0.5f, Grow = true, Spin = true, Drag = 0.5f }));
            Slip = Visuals.Effect("MoreQualitySlip", 1.5f, "Play_UI_item_pickup", root =>
                Visuals.Particles(root, "Sparkle", new Spec { Color = new Color(0.7f, 0.9f, 1f), Size = 0.15f, Lifetime = 0.6f, Speed = 3f, Burst = 20, Shape = Shape.Sphere, Radius = 0.2f, Gravity = 0.5f, Shrink = true }));
            GhostCatch = Visuals.Effect("MoreQualityGhostCatch", 1.5f, null, root =>
                Visuals.Particles(root, "Wisps", new Spec { Color = new Color(0.85f, 0.95f, 1f), Size = 0.3f, Lifetime = 0.8f, Speed = 2.5f, Burst = 25, Shape = Shape.Sphere, Radius = 0.3f, Gravity = -0.3f, Shrink = true, Noise = 0.5f }));

            GhostCarrier = Networked(pack, "MoreQualityGhostCarrier", true, root =>
            {
                // A glowing orb with two black eyes, trailing wisps.
                var ghostly = Visuals.Solid(new Color(0.85f, 0.95f, 1f), new Color(0.6f, 0.85f, 1f), 2f);
                Visuals.Model(root, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.6f, ghostly);
                var eyes = Visuals.Solid(Color.black, Color.black, 0f);
                Visuals.Model(root, PrimitiveType.Sphere, new Vector3(0.12f, 0.08f, 0.26f), new Vector3(0.1f, 0.14f, 0.06f), eyes);
                Visuals.Model(root, PrimitiveType.Sphere, new Vector3(-0.12f, 0.08f, 0.26f), new Vector3(0.1f, 0.14f, 0.06f), eyes);
                Visuals.Flash(root, new Color(0.7f, 0.9f, 1f), 4f, 1.5f, 0f);
                Visuals.Particles(root, "Wisp", new Spec { Color = new Color(0.8f, 0.95f, 1f), Size = 0.3f, Lifetime = 0.8f, Rate = 30f, Speed = 0.2f, Shape = Shape.Sphere, Radius = 0.25f, Gravity = -0.2f, Shrink = true });
            });
            BoneSpikes = Networked(pack, "MoreQualityBoneSpikes", false, root =>
            {
                var bone = Visuals.Solid(Bone, new Color(0.3f, 0.28f, 0.2f), 0.3f);
                var random = new System.Random(7);
                for (var i = 0; i < 9; i++)
                {
                    var angle = i * 40f + random.Next(0, 20);
                    var distance = i == 0 ? 0f : 0.35f + (float)random.NextDouble() * 0.5f;
                    var position = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * distance;
                    var height = 0.8f + (float)random.NextDouble() * 1.2f;
                    var spike = Visuals.Model(root, PrimitiveType.Cube, position + Vector3.up * height * 0.45f, new Vector3(0.18f, height, 0.18f), bone);
                    spike.transform.localRotation = Quaternion.Euler(random.Next(-25, 25), angle, random.Next(-25, 25));
                }
                var collider = root.gameObject.AddComponent<CapsuleCollider>();
                collider.radius = 0.9f;
                collider.height = 2.5f;
                collider.center = Vector3.up * 1.2f;
                root.gameObject.layer = LayerIndex.world.intVal;
                Visuals.Particles(root, "Dust", new Spec { Material = Visuals.Kind.Smoke, Color = new Color(0.8f, 0.75f, 0.65f), Size = 0.8f, Lifetime = 1.2f, Speed = 1.5f, Burst = 20, Shape = Shape.Circle, Radius = 0.5f, Grow = true, Drag = 0.5f, Rotation = Flat });
            });

            WindZone = Networked(pack, "MoreQualityWindZone", false, root =>
            {
                // Built for a 10m radius; Decor scales it to the actual one. Streaks and dust blow along its
                // forward axis (the wind's direction), and a ring of swirling dust marks its edge.
                Blowing(Visuals.Particles(root, "Streaks", new Spec { Material = Visuals.Kind.Spark, Color = new Color(0.85f, 0.9f, 1f), Size = 0.12f, Lifetime = 0.7f, Rate = 90f, Speed = 0.01f, Shape = Shape.Box, Radius = 1f, Stretch = true, Local = true }),
                    new Vector3(17f, 5f, 17f), 20f, 30f).gameObject.transform.localPosition = Vector3.up * 2.5f;
                Blowing(Visuals.Particles(root, "Dust", new Spec { Material = Visuals.Kind.Smoke, Color = new Color(0.7f, 0.65f, 0.55f), Size = 0.8f, Lifetime = 1.2f, Rate = 25f, Speed = 0.01f, Shape = Shape.Box, Radius = 1f, Spin = true, Local = true }),
                    new Vector3(17f, 1f, 17f), 8f, 14f).gameObject.transform.localPosition = Vector3.up * 0.5f;
                Visuals.Particles(root, "Edge", new Spec { Material = Visuals.Kind.Smoke, Color = new Color(0.75f, 0.8f, 0.85f), Size = 0.9f, Lifetime = 1.5f, Rate = 40f, Speed = 0.4f, Shape = Shape.Circle, Radius = 10f, Shell = true, Gravity = -0.03f, Grow = true, Spin = true, Local = true, Rotation = Flat });
                root.parent.gameObject.AddComponent<Visuals.WindZoneMarker>();
            });
            StormCloud = Networked(pack, "MoreQualityStormCloud", true, root =>
            {
                // Built for a 10m radius; Decor scales it to the actual one. A body of overlapping, flattened
                // puffs (darker on top, lit red underneath), softened by a thin veil of drifting smoke.
                var top = Visuals.Solid(new Color(0.16f, 0.05f, 0.07f), new Color(0.12f, 0.02f, 0.03f), 0.4f);
                var belly = Visuals.Solid(new Color(0.32f, 0.07f, 0.09f), new Color(0.6f, 0.08f, 0.1f), 0.9f);
                var random = new System.Random(11);
                var puffs = new GameObject("Puffs").transform;
                puffs.SetParent(root, false);
                for (var i = 0; i < 22; i++)
                {
                    var angle = (float)random.NextDouble() * 360f;
                    var distance = Mathf.Sqrt((float)random.NextDouble()) * 7.5f;
                    var position = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * distance;
                    var size = 3.5f + (float)random.NextDouble() * 4f - distance * 0.25f;
                    position.y = (float)random.NextDouble() * 1.4f + (7.5f - distance) * 0.18f;
                    Visuals.Model(puffs, PrimitiveType.Sphere, position, new Vector3(size, size * 0.55f, size), top);
                }
                for (var i = 0; i < 12; i++)
                {
                    var angle = (float)random.NextDouble() * 360f;
                    var distance = Mathf.Sqrt((float)random.NextDouble()) * 6.5f;
                    var position = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * distance + Vector3.down * 0.9f;
                    var size = 3f + (float)random.NextDouble() * 2.5f;
                    Visuals.Model(puffs, PrimitiveType.Sphere, position, new Vector3(size, size * 0.4f, size), belly);
                }
                puffs.gameObject.AddComponent<Visuals.Spinner>().degreesPerSecond = 2f;
                var veil = Visuals.Particles(root, "Veil", new Spec { Material = Visuals.Kind.Smoke, Color = new Color(0.25f, 0.05f, 0.08f), ColorEnd = new Color(0.1f, 0.02f, 0.03f), Size = 4f, Lifetime = 6f, Rate = 10f, Speed = 0.3f, Shape = Shape.Box, Radius = 1f, Grow = true, Spin = true, Noise = 0.2f, Local = true });
                var veilShape = veil.shape;
                veilShape.scale = new Vector3(18f, 3f, 18f);
                var rain = Visuals.Particles(root, "Rain", new Spec { Material = Visuals.Kind.Spark, Color = new Color(0.8f, 0.08f, 0.1f), Size = 0.06f, Lifetime = 0.6f, Rate = 260f, Speed = 0.01f, Shape = Shape.Box, Radius = 1f, Stretch = true });
                var rainShape = rain.shape;
                rainShape.scale = new Vector3(18f, 0.5f, 18f);
                rain.gameObject.transform.localPosition = Vector3.down * 2f;
                var fall = rain.velocityOverLifetime;
                fall.enabled = true;
                fall.space = ParticleSystemSimulationSpace.World;
                fall.x = new ParticleSystem.MinMaxCurve(-1f, 1f);
                fall.y = new ParticleSystem.MinMaxCurve(-26f, -20f);
                fall.z = new ParticleSystem.MinMaxCurve(-1f, 1f);
                Visuals.Flash(root, new Color(1f, 0.2f, 0.25f), 25f, 0.8f, 0f);
                root.gameObject.AddComponent<Visuals.Flicker>();
            });

            Marks();
        }

        // Particles carried along the template's forward axis (the wind direction). Unity ignores velocity over
        // lifetime unless all three axes use the same kind of curve, hence the small random x and y.
        private static ParticleSystem Blowing(ParticleSystem particles, Vector3 area, float minSpeed, float maxSpeed)
        {
            var shape = particles.shape;
            shape.scale = area;
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-1f, 1f);
            velocity.y = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
            velocity.z = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
            return particles;
        }

        // A networked object of our own: built from scratch, spawned by the server, seen by everyone.
        // Ones that move are moved through Decor.Move (our own position sync).
        private static GameObject Networked(RoR2.ContentManagement.ContentPack pack, string name, bool moves, Action<Transform> build)
        {
            var root = new GameObject(name);
            root.AddComponent<NetworkIdentity>();
            // The looks live on a child, so their bobbing never fights the synced position of the root.
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            build(visual);
            if (moves)
            {
                root.AddComponent<Decor.Follower>();
                visual.gameObject.AddComponent<Visuals.Spinner>().bob = 0.25f;
            }
            var prefab = R2API.PrefabAPI.InstantiateClone(root, name, true);
            UnityEngine.Object.Destroy(root);
            pack.networkedObjectPrefabs.Add(new[] { prefab });
            return prefab;
        }

        private static void Marks()
        {
            BuffVisuals.Show(CustomBuffs.Enraged, Visuals.Template("MoreQualityEnragedMark", root =>
            {
                Visuals.Particles(root, "Rage", new Spec { Color = Rage, Size = 0.35f, Lifetime = 0.5f, Rate = 25f, Speed = 0.5f, Shape = Shape.Sphere, Radius = 1f, Shell = true, Gravity = -0.3f, Shrink = true });
                Visuals.Particles(root, "Steam", new Spec { Material = Visuals.Kind.Smoke, Color = new Color(0.5f, 0.05f, 0.05f), Size = 0.6f, Lifetime = 1f, Rate = 6f, Speed = 0.8f, Shape = Shape.Sphere, Radius = 0.6f, Gravity = -0.2f, Grow = true });
            }), body => body.radius, false);

            BuffVisuals.Show(CustomBuffs.Crowned, Visuals.Template("MoreQualityCrown", root =>
            {
                var gold = Visuals.Solid(Gold, new Color(1f, 0.6f, 0.1f), 2f);
                var gem = Visuals.Solid(new Color(0.9f, 0.1f, 0.2f), new Color(1f, 0.1f, 0.2f), 3f);
                for (var i = 0; i < 8; i++)
                {
                    var direction = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;
                    var band = Visuals.Model(root, PrimitiveType.Cube, direction * 0.4f, new Vector3(0.33f, 0.15f, 0.06f), gold);
                    band.transform.localRotation = Quaternion.LookRotation(direction);
                    if (i % 2 == 0)
                    {
                        var point = Visuals.Model(root, PrimitiveType.Cube, direction * 0.4f + Vector3.up * 0.2f, new Vector3(0.08f, 0.3f, 0.08f), gold);
                        point.transform.localRotation = Quaternion.LookRotation(direction) * Quaternion.Euler(0f, 0f, 45f);
                        Visuals.Model(root, PrimitiveType.Sphere, direction * 0.4f + Vector3.up * 0.38f, Vector3.one * 0.09f, gem);
                    }
                }
                Visuals.Particles(root, "Glitter", new Spec { Color = Gold, Size = 0.1f, Lifetime = 0.8f, Rate = 15f, Speed = 0.3f, Shape = Shape.Circle, Radius = 0.45f, Gravity = 0.2f, Shrink = true, Rotation = Flat });
                Visuals.Flash(root, Gold, 4f, 1.5f, 0f);
                root.gameObject.AddComponent<Visuals.Spinner>().bob = 0.08f;
            }), body => 1.5f + body.radius * 0.5f, false, true);

        }

        // The reaper, for one swing: it hangs from a point high above you and swoops through like a pendulum,
        // from a random side, scythe first, then fades. A figure of dark smoke with burning red eyes.
        private static void Reaper(Transform root)
        {
            var pivot = new GameObject("Pivot").transform;
            pivot.SetParent(root, false);
            pivot.localPosition = Vector3.up * 6f;
            var reaper = new GameObject("Reaper").transform;
            reaper.SetParent(pivot, false);
            reaper.localPosition = Vector3.down * 5.2f;

            var shadow = new Color(0.06f, 0.02f, 0.08f);
            // The body holds its shape (local smoke); a second, world-space smoke trails behind the swoop.
            var robe = Visuals.Particles(reaper, "Robe", new Spec { Material = Visuals.Kind.Smoke, Color = shadow, ColorEnd = new Color(0.02f, 0f, 0.03f), Size = 0.7f, Lifetime = 0.5f, Rate = 160f, Speed = 0.05f, Shape = Shape.Box, Radius = 1f, Spin = true, Local = true });
            var robeShape = robe.shape;
            robeShape.scale = new Vector3(0.7f, 1.6f, 0.45f);
            var hem = Visuals.Particles(reaper, "Hem", new Spec { Material = Visuals.Kind.Smoke, Color = shadow, Size = 0.9f, Lifetime = 0.5f, Rate = 80f, Speed = 0.1f, Shape = Shape.Box, Radius = 1f, Spin = true, Local = true });
            var hemShape = hem.shape;
            hemShape.scale = new Vector3(1.1f, 0.4f, 0.7f);
            hem.gameObject.transform.localPosition = Vector3.down * 0.8f;
            Visuals.Particles(reaper, "Hood", new Spec { Material = Visuals.Kind.Smoke, Color = new Color(0.03f, 0.01f, 0.04f), Size = 0.65f, Lifetime = 0.4f, Rate = 70f, Shape = Shape.Sphere, Radius = 0.25f, Spin = true, Local = true }).gameObject.transform.localPosition = Vector3.up * 1.05f;
            Visuals.Particles(reaper, "Trail", new Spec { Material = Visuals.Kind.Smoke, Color = new Color(0.1f, 0.03f, 0.12f), ColorEnd = new Color(0f, 0f, 0f), Size = 0.8f, Lifetime = 0.9f, Rate = 90f, Shape = Shape.Box, Radius = 0.6f, Grow = true, Spin = true, Noise = 0.3f });
            foreach (var x in new[] { 0.11f, -0.11f })
            {
                Visuals.Particles(reaper, "Eye", new Spec { Color = new Color(1f, 0.1f, 0.05f), Size = 0.18f, Lifetime = 0.15f, Rate = 30f, Shape = Shape.Sphere, Radius = 0.01f, Local = true }).gameObject.transform.localPosition = new Vector3(x, 1.05f, 0.3f);
            }
            Visuals.Flash(reaper, new Color(0.8f, 0.05f, 0.1f), 4f, 1.5f, 0f);

            // The scythe, held out in front, blade first, its edge leaving a red streak through the swing.
            var hand = new GameObject("Hand").transform;
            hand.SetParent(reaper, false);
            hand.localPosition = new Vector3(0.45f, 0.35f, 0.25f);
            hand.localRotation = Quaternion.Euler(40f, 0f, 0f);
            var wood = Visuals.Solid(new Color(0.15f, 0.1f, 0.08f), Color.black, 0f);
            var steel = Visuals.Solid(new Color(0.7f, 0.72f, 0.78f), new Color(0.4f, 0.1f, 0.15f), 0.8f);
            Visuals.Model(hand, PrimitiveType.Cylinder, Vector3.up * 0.55f, new Vector3(0.05f, 0.8f, 0.05f), wood);
            Visuals.Model(hand, PrimitiveType.Cube, new Vector3(0f, 1.32f, 0.25f), new Vector3(0.04f, 0.1f, 0.55f), steel);
            Visuals.Model(hand, PrimitiveType.Cube, new Vector3(0f, 1.22f, 0.64f), new Vector3(0.035f, 0.09f, 0.35f), steel).transform.localRotation = Quaternion.Euler(30f, 0f, 0f);
            Visuals.Model(hand, PrimitiveType.Cube, new Vector3(0f, 1.03f, 0.86f), new Vector3(0.03f, 0.07f, 0.25f), steel).transform.localRotation = Quaternion.Euler(60f, 0f, 0f);
            Visuals.Particles(hand, "Edge", new Spec { Material = Visuals.Kind.Spark, Color = new Color(0.9f, 0.1f, 0.15f), Size = 0.15f, Lifetime = 0.35f, Rate = 120f, Shape = Shape.Sphere, Radius = 0.05f, Shrink = true }).gameObject.transform.localPosition = new Vector3(0f, 1.1f, 0.8f);

            root.gameObject.AddComponent<Visuals.Pendulum>();
        }
    }
}
