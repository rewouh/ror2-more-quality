using System.Collections.Generic;
using System.Linq;
using RoR2;
using SS2;
using UnityEngine;
using UnityEngine.Networking;

namespace MoreQuality.Items
{
    // Man-o'-War: kills can release a friendly jellyfish that drifts to the nearest enemy and explodes.
    // It's a real Jellyfish (summoned on your team, with the game's own model and AI), so it looks right on
    // every game. It explodes in the Jellyfish's electric nova when it reaches an enemy, runs out of time,
    // or is killed. A jellyfish kill is credited to you, so it can release more jellyfish.
    internal static class LightningOnKill
    {
        private static readonly T4 Chance = new T4(10f, 20f, 40f, 70f);
        private static readonly T4 ChancePerStack = new T4(10f, 15f, 20f, 25f);
        private static readonly T4 Damage = new T4(3f, 6f, 15f, 30f);
        private static readonly T4 Radius = new T4(6f, 7f, 9f, 12f);
        private const float Range = 40f;
        private const float Lifetime = 12f;
        private const float SpeedMultiplier = 2.5f;

        private sealed class Jelly
        {
            public CharacterBody Owner;
            public TeamIndex Team;
            public float Damage, Radius, Until;
            public bool Crit, Exploded;
        }

        private static readonly Dictionary<CharacterMaster, Jelly> jellies = new Dictionary<CharacterMaster, Jelly>();
        private static GameObject jellyMaster;
        private static GameObject explosionEffect;
        private static string explosionSound;

        internal static void Init()
        {
            GlobalEventManager.onCharacterDeathGlobal += OnDeath;
            RoR2Application.onFixedUpdate += Drive;
            Run.onRunStartGlobal += _ => jellies.Clear();
        }

        private static void OnDeath(DamageReport report)
        {
            if (!NetworkServer.active || !report.victimBody) return;

            // One of our jellyfish died before reaching anything: it still goes off.
            Jelly jelly;
            if (report.victimMaster && jellies.TryGetValue(report.victimMaster, out jelly))
            {
                Explode(jelly, report.victimBody.corePosition);
                jellies.Remove(report.victimMaster);
                return;
            }

            var attacker = report.attackerBody;
            if (!attacker) return;
            var counts = Q.Counts(attacker, SS2Content.Items.LightningOnKill);
            if (!Q.Any(counts)) return;
            var chance = Mathf.Min(90f, Q.Stacked(counts, Chance, ChancePerStack));
            if (!Util.CheckRoll(chance, attacker.master)) return;
            if (!Enemies.Near(report.victimBody.corePosition, Range, attacker.teamComponent.teamIndex, report.victim).Any()) return;
            Summon(attacker, report.victimBody.corePosition + Vector3.up * 2f, counts);
        }

        private static void Summon(CharacterBody owner, Vector3 position, ItemQualities.ItemQualityCounts counts)
        {
            if (!jellyMaster) jellyMaster = MasterCatalog.FindMasterPrefab("JellyfishMaster");
            if (!jellyMaster) return;
            var master = new MasterSummon
            {
                masterPrefab = jellyMaster,
                position = position,
                rotation = Quaternion.identity,
                summonerBodyObject = owner.gameObject,
                teamIndexOverride = owner.teamComponent.teamIndex,
                ignoreTeamMemberLimit = true,
                useAmbientLevel = true
            }.Perform();
            if (!master) return;
            jellies[master] = new Jelly
            {
                Owner = owner,
                Team = owner.teamComponent.teamIndex,
                Damage = owner.damage * Q.Stack(counts, Damage),
                Radius = Q.Stack(counts, Radius),
                Until = Time.fixedTime + Lifetime,
                Crit = owner.RollCrit()
            };
        }

        // The jellyfish's own AI flies it at enemies; this makes it faster and sets it off on arrival.
        private static void Drive()
        {
            if (!NetworkServer.active || jellies.Count == 0) return;
            foreach (var entry in jellies.ToList())
            {
                var master = entry.Key;
                var jelly = entry.Value;
                var body = master ? master.GetBody() : null;
                if (!master || (!body && Time.fixedTime > jelly.Until)) { jellies.Remove(master); continue; }
                if (!body) continue;   // still spawning

                if (body.baseMoveSpeed < 20f)
                {
                    body.baseMoveSpeed *= SpeedMultiplier;
                    body.baseAcceleration *= SpeedMultiplier;
                    body.MarkAllStatsDirty();
                }
                var target = Enemies.Near(body.corePosition, body.radius + 3f, jelly.Team).FirstOrDefault();
                if (!target && Time.fixedTime < jelly.Until) continue;

                jellies.Remove(master);
                Explode(jelly, body.corePosition);
                if (body.healthComponent) body.healthComponent.Suicide();
            }
        }

        private static void Explode(Jelly jelly, Vector3 position)
        {
            if (jelly.Exploded) return;
            jelly.Exploded = true;
            new BlastAttack
            {
                attacker = jelly.Owner ? jelly.Owner.gameObject : null,
                inflictor = Sources.Of(SS2Content.Items.LightningOnKill),
                teamIndex = jelly.Team,
                position = position,
                radius = jelly.Radius,
                baseDamage = jelly.Damage,
                crit = jelly.Crit,
                procCoefficient = 1f,
                damageColorIndex = DamageColorIndex.Item,
                damageType = DamageType.Shock5s,
                falloffModel = BlastAttack.FalloffModel.None,
                attackerFiltering = AttackerFiltering.NeverHitSelf
            }.Fire();
            if (!explosionEffect) LoadNova();
            if (explosionEffect) EffectManager.SpawnEffect(explosionEffect, new EffectData { origin = position, scale = jelly.Radius }, true);
            if (!string.IsNullOrEmpty(explosionSound) && jelly.Owner) Util.PlaySound(explosionSound, jelly.Owner.gameObject);
        }

        // The real Jellyfish's electric nova. Its prefab and sound live in the JellyNova state's
        // config, which the game only applies when that state is created, so create one first.
        private static void LoadNova()
        {
            try
            {
                new EntityStates.JellyfishMonster.JellyNova();
                explosionEffect = EntityStates.JellyfishMonster.JellyNova.novaEffectPrefab;
                explosionSound = EntityStates.JellyfishMonster.JellyNova.novaSoundString;
            }
            catch (System.Exception e)
            {
                MoreQualityPlugin.Log.LogWarning("Jellyfish nova not found, using a lightning strike: " + e.Message);
            }
            if (!explosionEffect) explosionEffect = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/ImpactEffects/LightningStrikeImpact");
        }
    }
}
