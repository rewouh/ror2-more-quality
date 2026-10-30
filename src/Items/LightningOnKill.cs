using System.Linq;
using ItemQualities;
using R2API;
using RoR2;
using RoR2.Orbs;
using SS2;
using UnityEngine;
using UnityEngine.Networking;

namespace MoreQuality.Items
{
    // Man-o'-War: kills can release a friendly jellyfish that drifts to the nearest enemy and explodes.
    // It's an orb with a jellyfish model as its visual rather than an AI ally, so it can't get stuck.
    // A jellyfish kill is a kill like any other, so it can release more jellyfish.
    internal static class LightningOnKill
    {
        private static readonly T4 Chance = new T4(10f, 20f, 35f, 50f);
        private static readonly T4 ChancePerStack = new T4(10f, 15f, 20f, 25f);
        private static readonly T4 Damage = new T4(3f, 5f, 8f, 12f);
        private static readonly T4 Radius = new T4(6f, 7f, 8f, 10f);
        private const float Range = 40f;
        private const float Speed = 14f;

        private static GameObject jellyEffect;
        private static GameObject explosionEffect;

        internal static void Init()
        {
            ContentProvider.AddBuilder(BuildEffect);
            GlobalEventManager.onCharacterDeathGlobal += OnDeath;
        }

        private static void BuildEffect(RoR2.ContentManagement.ContentPack pack)
        {
            explosionEffect = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/ImpactEffects/LightningStrikeImpact")
                ?? LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/OmniEffect/OmniExplosionVFXQuick");

            GameObject source = null;
            foreach (var path in new[] { "Prefabs/Effects/OrbEffects/InfusionOrbEffect", "Prefabs/Effects/OrbEffects/HealthOrbEffect", "Prefabs/Effects/OrbEffects/ArrowOrbEffect" })
            {
                source = LegacyResourcesAPI.Load<GameObject>(path);
                if (source && source.GetComponent<OrbEffect>()) break;
            }
            if (!source) { MoreQualityPlugin.Log.LogWarning("No orb effect to build the jellyfish from"); return; }

            jellyEffect = PrefabAPI.InstantiateClone(source, "QualitySS2JellyfishOrbEffect", false);
            var orbEffect = jellyEffect.GetComponent<OrbEffect>();
            if (orbEffect) orbEffect.movementCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            jellyEffect.AddComponent<JellyfishVisual>();
            pack.effectDefs.Add(new[] { new EffectDef(jellyEffect) });
        }

        // Unity won't parent objects under a prefab at load time, so each orb effect instance builds its
        // jellyfish when it appears: hide the orb's own visuals, then add a copy of the Jellyfish model.
        private sealed class JellyfishVisual : MonoBehaviour
        {
            private static GameObject model;

            private void Start()
            {
                try
                {
                    if (!model)
                    {
                        var body = LegacyResourcesAPI.Load<GameObject>("Prefabs/CharacterBodies/JellyfishBody");
                        var locator = body ? body.GetComponent<ModelLocator>() : null;
                        model = locator && locator.modelTransform ? locator.modelTransform.gameObject : null;
                    }
                    if (!model) return;

                    foreach (var particles in GetComponentsInChildren<ParticleSystem>(true)) particles.gameObject.SetActive(false);
                    foreach (var renderer in GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;

                    var jelly = Instantiate(model, transform, false);
                    jelly.transform.localPosition = Vector3.zero;
                    jelly.transform.localRotation = Quaternion.identity;
                    // Strip everything that would make it a real, targetable character.
                    foreach (var hurtBox in jelly.GetComponentsInChildren<HurtBox>(true)) Destroy(hurtBox);
                    foreach (var collider in jelly.GetComponentsInChildren<Collider>(true)) Destroy(collider);
                    // CharacterModel starts invisible and only a living body makes it visible again, so
                    // without a body it would hide every renderer. It can't be removed (the skin
                    // controller needs it), so switch it off.
                    foreach (var characterModel in jelly.GetComponentsInChildren<CharacterModel>(true)) characterModel.enabled = false;
                    jelly.SetActive(true);
                    foreach (var renderer in jelly.GetComponentsInChildren<Renderer>(true)) renderer.enabled = true;
                }
                catch (System.Exception e)
                {
                    MoreQualityPlugin.Log.LogWarning("Jellyfish visual failed, showing a plain orb: " + e.Message);
                }
            }
        }

        private static void OnDeath(DamageReport report)
        {
            var attacker = report.attackerBody;
            if (!NetworkServer.active || !attacker || !report.victimBody) return;
            var counts = Q.Counts(attacker, SS2Content.Items.LightningOnKill);
            if (!Q.Any(counts)) return;
            var chance = Mathf.Min(90f, Q.Stacked(counts, Chance, ChancePerStack));
            if (!Util.CheckRoll(chance, attacker.master)) return;

            var origin = report.victimBody.corePosition;
            var target = Enemies.Near(origin, Range, attacker.teamComponent.teamIndex, report.victim).FirstOrDefault();
            if (!target || !target.body.mainHurtBox) return;
            var tier = Q.Highest(counts);
            OrbManager.instance.AddOrb(new JellyfishOrb
            {
                origin = origin,
                target = target.body.mainHurtBox,
                attacker = attacker.gameObject,
                team = attacker.teamComponent.teamIndex,
                damage = attacker.damage * Damage[tier],
                radius = Radius[tier],
                crit = attacker.RollCrit()
            });
        }

        private sealed class JellyfishOrb : Orb
        {
            public GameObject attacker;
            public TeamIndex team;
            public float damage;
            public float radius;
            public bool crit;

            public override void Begin()
            {
                duration = Mathf.Max(0.3f, distanceToTarget / Speed);
                if (!jellyEffect) return;
                var data = new EffectData { origin = origin, genericFloat = duration };
                data.SetHurtBoxReference(target);
                EffectManager.SpawnEffect(jellyEffect, data, true);
            }

            public override void OnArrival()
            {
                if (!target) return;
                var position = target.transform.position;
                new BlastAttack
                {
                    attacker = attacker,
                    inflictor = attacker,
                    teamIndex = team,
                    position = position,
                    radius = radius,
                    baseDamage = damage,
                    crit = crit,
                    procCoefficient = 1f,
                    damageColorIndex = DamageColorIndex.Item,
                    damageType = DamageType.Generic,
                    falloffModel = BlastAttack.FalloffModel.None,
                    attackerFiltering = AttackerFiltering.NeverHitSelf
                }.Fire();
                if (explosionEffect) EffectManager.SpawnEffect(explosionEffect, new EffectData { origin = position, scale = radius }, true);
            }
        }
    }
}
