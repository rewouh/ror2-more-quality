using System;
using System.Collections.Generic;
using System.Linq;
using R2API;
using RoR2;
using UnityEngine;

namespace MoreQuality
{
    // MoreQuality's own visuals: particle systems and small models built in code. Only the base materials
    // (a glow for particles, a lit surface for models) are borrowed from the game, then recoloured.
    //
    // Three kinds:
    //   - Effects: one-shot bursts spawned through EffectManager, so every player sees them.
    //     EffectData.scale is the radius in metres; EffectData.genericFloat can carry a duration.
    //   - Marks: looping visuals attached, on every game, to a body while it has a buff (see BuffVisuals).
    //   - Models: simple meshes for our pickups (molten coin, chocolate).
    internal static class Visuals
    {
        private static Material glow;
        private static Material solid;
        private static Material smoke;
        private static Material spark;
        private static Material blood;

        // Which borrowed texture a particle system uses; colours, motion and shapes are always ours.
        public enum Kind { Glow, Smoke, Spark, Blood }

        public struct Spec
        {
            public Color Color;
            public float Size;          // particle size, metres at scale 1
            public float Lifetime;
            public float Speed;
            public int Burst;           // particles emitted at once
            public float Rate;          // particles per second (loops when > 0)
            public ParticleSystemShapeType Shape;
            public float Radius;        // shape radius at scale 1
            public bool Shell;          // emit from the shape's edge only
            public float Gravity;       // negative rises
            public bool Shrink;
            public bool Local;          // particles follow the parent
            public Vector3 Rotation;    // shape rotation (degrees)
            public Kind Material;
            public Color? ColorEnd;     // fades from Color to this over the particle's life
            public bool Grow;           // grows to 2.5x its size
            public bool Stretch;        // drawn stretched along its velocity (streaks)
            public bool Flat;           // lies flat on the ground (splats, stains)
            public bool Spin;           // random rotation, slowly turning
            public float Noise;         // turbulence strength
            public float Drag;          // slows down over its life (0 to 1)
        }

        // ---------------------------------------------------------------- materials

        private static void LoadMaterials()
        {
            if (glow && solid) return;
            foreach (var path in new[] { "Prefabs/Effects/OmniEffect/OmniExplosionVFX", "Prefabs/Effects/ImpactEffects/CoinImpact", "Prefabs/Effects/OmniEffect/OmniImpactVFX" })
            {
                if (glow) break;
                var prefab = LegacyResourcesAPI.Load<GameObject>(path);
                if (!prefab) continue;
                var materials = prefab.GetComponentsInChildren<ParticleSystemRenderer>(true).Select(r => r.sharedMaterial).Where(m => m).ToList();
                glow = materials.FirstOrDefault(m => m.shader && m.shader.name.Contains("Additive"))
                    ?? materials.FirstOrDefault(m => m.shader && m.shader.name.Contains("Cloud"))
                    ?? materials.FirstOrDefault();
            }
            smoke = Pick(new[] { "Prefabs/Effects/MagmaWormDeathDust", "Prefabs/Effects/Tracers/TracerSmokeChase", "Prefabs/PoisonEffect" }, "Smoke", "Dust", "Cloud", "Puff");
            spark = Pick(new[] { "Prefabs/Effects/ImpactEffects/CritsparkHeavy", "Prefabs/Effects/ImpactEffects/Critspark", "Prefabs/Effects/ImpactEffects/Hitspark1" }, "Spark", "Streak", "Tracer", "Flare");
            blood = Pick(new[] { "Prefabs/Effects/ImpactEffects/BleedOnHitAndExplode_Impact", "Prefabs/BleedEffect", "Prefabs/SuperBleedEffect" }, "Blood", "Bleed", "Gore", "Splat");
            var pack = LegacyResourcesAPI.Load<GameObject>("Prefabs/NetworkedObjects/BonusMoneyPack");
            var surface = pack ? pack.GetComponentsInChildren<MeshRenderer>(true).Select(r => r.sharedMaterial).FirstOrDefault(m => m) : null;
            solid = surface;
            MoreQualityPlugin.Log.LogInfo("Visuals: glow " + Name(glow) + ", smoke " + Name(smoke) + ", spark " + Name(spark) + ", blood " + Name(blood) + ", solid " + Name(solid));
        }

        private static string Name(Material material)
        {
            return material ? material.name + " (" + (material.shader ? material.shader.name : "?") + ")" : "none";
        }

        // The first particle material in these prefabs whose name has one of the keywords, else their first one.
        private static Material Pick(string[] paths, params string[] keywords)
        {
            var all = new List<Material>();
            foreach (var path in paths)
            {
                var prefab = LegacyResourcesAPI.Load<GameObject>(path);
                if (prefab) all.AddRange(prefab.GetComponentsInChildren<ParticleSystemRenderer>(true).Select(r => r.sharedMaterial).Where(m => m));
            }
            MoreQualityPlugin.Log.LogDebug("Visuals candidates: " + string.Join(", ", all.Select(Name).Distinct()));
            return all.FirstOrDefault(m => keywords.Any(k => m.name.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0)) ?? all.FirstOrDefault();
        }

        public static Material Particle(Kind kind, Color color)
        {
            LoadMaterials();
            var source = kind == Kind.Smoke ? smoke : kind == Kind.Spark ? spark : kind == Kind.Blood ? blood : glow;
            if (!source) source = glow;
            if (!source) return null;
            var material = new Material(source);
            if (material.HasProperty("_TintColor")) material.SetColor("_TintColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            return material;
        }

        public static Material Glow(Color color)
        {
            return Particle(Kind.Glow, color);
        }

        public static Material Solid(Color color, Color emission, float emissionPower)
        {
            LoadMaterials();
            if (!solid) return null;
            var material = new Material(solid);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_EmColor")) material.SetColor("_EmColor", emission);
            if (material.HasProperty("_EmPower")) material.SetFloat("_EmPower", emissionPower);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", null);
            return material;
        }

        // ---------------------------------------------------------------- building blocks

        public static ParticleSystem Particles(Transform parent, string name, Spec spec)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localEulerAngles = spec.Rotation;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            var loops = spec.Rate > 0f;
            main.duration = loops ? 1f : Mathf.Max(0.1f, spec.Lifetime);
            main.loop = loops;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(spec.Lifetime * 0.7f, spec.Lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(spec.Speed * 0.6f, spec.Speed);
            main.startSize = new ParticleSystem.MinMaxCurve(spec.Size * 0.6f, spec.Size);
            main.startColor = spec.Color;
            if (spec.Spin)
            {
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                var rotation = ps.rotationOverLifetime;
                rotation.enabled = true;
                rotation.z = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);
            }
            main.gravityModifier = spec.Gravity;
            main.simulationSpace = spec.Local ? ParticleSystemSimulationSpace.Local : ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.maxParticles = Mathf.Max(50, spec.Burst * 2 + (int)(spec.Rate * spec.Lifetime * 2f));

            var emission = ps.emission;
            emission.rateOverTime = spec.Rate;
            emission.SetBursts(spec.Burst > 0 ? new[] { new ParticleSystem.Burst(0f, (short)spec.Burst) } : new ParticleSystem.Burst[0]);

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = spec.Shape;
            shape.radius = Mathf.Max(0.01f, spec.Radius);
            shape.radiusThickness = spec.Shell ? 0f : 1f;
            if (spec.Shape == ParticleSystemShapeType.Cone) shape.angle = 25f;

            var colors = ps.colorOverLifetime;
            colors.enabled = true;
            var gradient = new Gradient();
            var end = spec.ColorEnd.HasValue && spec.Color.r + spec.Color.g + spec.Color.b > 0f
                ? new Color(spec.ColorEnd.Value.r / Mathf.Max(0.01f, spec.Color.r), spec.ColorEnd.Value.g / Mathf.Max(0.01f, spec.Color.g), spec.ColorEnd.Value.b / Mathf.Max(0.01f, spec.Color.b))
                : Color.white;
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(end, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
            colors.color = gradient;

            if (spec.Shrink || spec.Grow)
            {
                var size = ps.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, spec.Grow
                    ? new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.3f, 1.4f), new Keyframe(1f, 2.5f))
                    : AnimationCurve.Linear(0f, 1f, 1f, 0f));
            }
            if (spec.Noise > 0f)
            {
                var noise = ps.noise;
                noise.enabled = true;
                noise.strength = spec.Noise;
                noise.frequency = 0.6f;
                noise.scrollSpeed = 0.4f;
                noise.damping = true;
            }
            if (spec.Drag > 0f)
            {
                var limit = ps.limitVelocityOverLifetime;
                limit.enabled = true;
                limit.limit = 0f;
                limit.dampen = spec.Drag;
                limit.drag = 0f;
            }

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = spec.Stretch ? ParticleSystemRenderMode.Stretch : spec.Flat ? ParticleSystemRenderMode.HorizontalBillboard : ParticleSystemRenderMode.Billboard;
            if (spec.Stretch)
            {
                renderer.velocityScale = 0.12f;
                renderer.lengthScale = 2f;
            }
            renderer.sharedMaterial = Particle(spec.Material, spec.Color);
            return ps;
        }

        public static GameObject Model(Transform parent, PrimitiveType type, Vector3 localPosition, Vector3 localScale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            var renderer = go.GetComponent<MeshRenderer>();
            if (material) renderer.sharedMaterial = material;
            return go;
        }

        // A short point light flash (or a steady glow for zones), scaled like the effect.
        public static void Flash(Transform parent, Color color, float range, float intensity, float duration)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(parent, false);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            go.AddComponent<LightFade>().duration = duration;
        }

        // Fades a light out over `duration` seconds (0: stays on until the zone stops).
        internal sealed class LightFade : MonoBehaviour
        {
            public float duration;
            private Light light;
            private float start, intensity, fadeFrom = -1f;

            private void Start()
            {
                light = GetComponent<Light>();
                start = Time.time;
                intensity = light ? light.intensity : 0f;
            }

            public void FadeOut() { fadeFrom = Time.time; }

            private void Update()
            {
                if (!light) return;
                if (duration > 0f) light.intensity = intensity * Mathf.Clamp01(1f - (Time.time - start) / duration);
                else if (fadeFrom >= 0f) light.intensity = intensity * Mathf.Clamp01(1f - (Time.time - fadeFrom) / 1.5f);
            }
        }

        // A template object that isn't in the scene, ready to be cloned.
        public static GameObject Template(string name, Action<Transform> build)
        {
            var root = new GameObject(name);
            build(root.transform);
            var prefab = PrefabAPI.InstantiateClone(root, name, false);
            UnityEngine.Object.Destroy(root);
            return prefab;
        }

        // ---------------------------------------------------------------- networked one-shot effects

        private static readonly List<GameObject> effects = new List<GameObject>();

        public static GameObject Effect(string name, float lifetime, string sound, Action<Transform> build, bool follow = false)
        {
            var root = new GameObject(name);
            build(root.transform);
            var component = root.AddComponent<EffectComponent>();
            component.applyScale = true;
            root.AddComponent<EffectScaler>();
            component.soundName = sound ?? "";
            component.parentToReferencedTransform = follow;
            component.positionAtReferencedTransform = follow;
            var vfx = root.AddComponent<VFXAttributes>();
            // Every live effect with VFXAttributes counts against the game's shared VFX budget, and past a
            // threshold the game skips other effects (anyone's explosions, impacts...). Ours are always shown
            // but cost the minimum, so they never crowd out the rest.
            vfx.vfxPriority = VFXAttributes.VFXPriority.Always;
            vfx.vfxIntensity = VFXAttributes.VFXIntensity.Low;
            vfx.DoNotPool = true;   // our components set themselves up in Start(), which a reused instance skips
            root.AddComponent<DestroyOnTimer>().duration = lifetime;
            var prefab = PrefabAPI.InstantiateClone(root, name, false);
            UnityEngine.Object.Destroy(root);
            effects.Add(prefab);
            return prefab;
        }

        public static void Register(RoR2.ContentManagement.ContentPack pack)
        {
            pack.effectDefs.Add(effects.Select(e => new EffectDef(e)).ToArray());
        }

        public static void Spawn(GameObject effect, Vector3 position, float radius, float duration = 0f)
        {
            if (!effect) return;
            EffectManager.SpawnEffect(effect, new EffectData { origin = position, scale = radius, genericFloat = duration }, true);
        }

        // For effects built with follow: true, which stick to the body.
        public static void SpawnOn(GameObject effect, CharacterBody body, float radius)
        {
            if (!effect || !body) return;
            var data = new EffectData { origin = body.corePosition, scale = radius };
            data.SetNetworkedObjectReference(body.gameObject);
            EffectManager.SpawnEffect(effect, data, true);
        }

        // An expanding ring with a burst of sparks and a flash: the shape of most of our explosions.
        public static GameObject Shockwave(string name, Color color, Color sparks, string sound)
        {
            return Effect(name, 2f, sound, root =>
            {
                Particles(root, "Flash", new Spec { Color = color, Size = 2.5f, Lifetime = 0.25f, Burst = 1, Shape = ParticleSystemShapeType.Sphere, Radius = 0.01f, Local = true });
                Particles(root, "Ring", new Spec { Color = color, Size = 0.5f, Lifetime = 0.45f, Speed = 2.2f, Burst = 60, Shape = ParticleSystemShapeType.Circle, Radius = 0.1f, Shell = true, Shrink = true, Rotation = new Vector3(90f, 0f, 0f) });
                Particles(root, "Sparks", new Spec { Color = sparks, Size = 0.18f, Lifetime = 0.8f, Speed = 1.6f, Burst = 40, Shape = ParticleSystemShapeType.Sphere, Radius = 0.2f, Gravity = 0.4f, Shrink = true });
            });
        }

        // A short-lived (genericFloat seconds) puddle of rising particles: zones and fields.
        public static GameObject Zone(string name, Color color, Color motes, string sound)
        {
            return Effect(name, 30f, sound, root =>
            {
                Particles(root, "Edge", new Spec { Color = color, Size = 0.25f, Lifetime = 0.6f, Rate = 50f, Shape = ParticleSystemShapeType.Circle, Radius = 1f, Shell = true, Rotation = new Vector3(90f, 0f, 0f) });
                Particles(root, "Fill", new Spec { Color = color, Size = 0.5f, Lifetime = 1.2f, Rate = 25f, Speed = 0.1f, Shape = ParticleSystemShapeType.Circle, Radius = 1f, Gravity = -0.05f, Rotation = new Vector3(90f, 0f, 0f) });
                Particles(root, "Motes", new Spec { Color = motes, Size = 0.12f, Lifetime = 1.5f, Rate = 15f, Speed = 0.3f, Shape = ParticleSystemShapeType.Circle, Radius = 1f, Gravity = -0.1f, Shrink = true, Rotation = new Vector3(90f, 0f, 0f) });
                root.gameObject.AddComponent<TimedZone>();
            });
        }

        // Stops a zone effect's emission after EffectData.genericFloat seconds, then lets its particles fade.
        internal sealed class TimedZone : MonoBehaviour
        {
            private float stopAt = -1f;

            private void Start()
            {
                var component = GetComponent<EffectComponent>();
                var duration = component && component.effectData != null && component.effectData.genericFloat > 0f ? component.effectData.genericFloat : 3f;
                stopAt = Time.time + duration;
                var timer = GetComponent<DestroyOnTimer>();
                if (timer) timer.duration = duration + 2f;
            }

            private void Update()
            {
                if (stopAt < 0f || Time.time < stopAt) return;
                stopAt = -1f;
                foreach (var ps in GetComponentsInChildren<ParticleSystem>()) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                foreach (var fade in GetComponentsInChildren<LightFade>()) fade.FadeOut();
            }
        }

        // The root's scale (EffectData.scale) only stretches the emission shapes. This makes the particles move
        // proportionally faster so rings still reach the edge, and grow a little, but not to giant blobs.
        internal sealed class EffectScaler : MonoBehaviour
        {
            private void Start()
            {
                var component = GetComponent<EffectComponent>();
                var scale = component && component.effectData != null && component.effectData.scale > 0f ? component.effectData.scale : 1f;
                foreach (var ps in GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = ps.main;
                    main.startSpeedMultiplier *= scale;
                    main.startSizeMultiplier *= Mathf.Clamp(Mathf.Sqrt(scale), 0.5f, 3f);
                    var noise = ps.noise;
                    if (noise.enabled) noise.strengthMultiplier *= Mathf.Sqrt(scale);
                }
                foreach (var light in GetComponentsInChildren<Light>(true)) light.range *= scale;
            }
        }

        internal sealed class Spinner : MonoBehaviour
        {
            public float degreesPerSecond = 90f;
            public float bob = 0.15f;
            private Vector3 start;

            private void Start() { start = transform.localPosition; }

            private void Update()
            {
                transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f, Space.World);
                transform.localPosition = start + Vector3.up * Mathf.Sin(Time.time * 2f) * bob;
            }
        }
    }

    // Looping visuals shown on every game while a body has a buff (buffs are synced, so no networking).
    internal static class BuffVisuals
    {
        private sealed class Entry
        {
            public BuffDef Buff;
            public GameObject Template;
            public Func<CharacterBody, float> Scale;   // radius for the template (built at 1m)
            public bool AtFeet;
        }

        private static readonly List<Entry> entries = new List<Entry>();
        private static readonly Dictionary<KeyValuePair<CharacterBody, BuffDef>, GameObject> shown = new Dictionary<KeyValuePair<CharacterBody, BuffDef>, GameObject>();

        // While `buff` is on a body, a copy of `template` follows it.
        public static void Show(BuffDef buff, GameObject template, Func<CharacterBody, float> scale, bool atFeet)
        {
            if (entries.Count == 0) RoR2Application.onFixedUpdate += Update;
            entries.Add(new Entry { Buff = buff, Template = template, Scale = scale, AtFeet = atFeet });
        }

        private static void Update()
        {
            if (!Run.instance || entries.Count == 0) return;
            try
            {
                foreach (var body in CharacterBody.readOnlyInstancesList)
                {
                    if (!body) continue;
                    foreach (var entry in entries)
                    {
                        if (!entry.Template || !entry.Buff) continue;
                        var key = new KeyValuePair<CharacterBody, BuffDef>(body, entry.Buff);
                        GameObject visual;
                        shown.TryGetValue(key, out visual);
                        var has = body.HasBuff(entry.Buff) && body.healthComponent && body.healthComponent.alive;
                        if (has && !visual)
                        {
                            visual = UnityEngine.Object.Instantiate(entry.Template, body.gameObject.transform);
                            shown[key] = visual;
                        }
                        else if (!has && visual)
                        {
                            UnityEngine.Object.Destroy(visual);
                            shown.Remove(key);
                            continue;
                        }
                        if (!visual) continue;
                        visual.transform.position = entry.AtFeet ? body.footPosition + Vector3.up * 0.1f : body.corePosition;
                        visual.transform.rotation = Quaternion.identity;
                        var scale = entry.Scale != null ? entry.Scale(body) : body.radius;
                        visual.transform.localScale = Vector3.one * scale / Mathf.Max(0.01f, body.gameObject.transform.lossyScale.x);
                    }
                }
                foreach (var key in shown.Keys.Where(k => !k.Key).ToList())
                {
                    if (shown[key]) UnityEngine.Object.Destroy(shown[key]);
                    shown.Remove(key);
                }
            }
            catch (Exception e)
            {
                MoreQualityPlugin.Log.LogError(e);
            }
        }
    }
}
