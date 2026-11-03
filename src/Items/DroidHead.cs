using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ItemQualities;
using RoR2;
using SS2;
using UnityEngine;

namespace MoreQuality.Items
{
    // Drones last longer, a second drone can spawn, and each new drone extends the ones you already have.
    // Self-destruct: when a drone expires or is destroyed, it explodes.
    internal static class DroidHead
    {
        private static readonly T4 ExtraLife = new T4(10f, 20f, 50f, 100f);
        private static readonly T4 ExtraDroneChance = new T4(15f, 30f, 75f, 150f);
        private static readonly T4 ExtendOthers = new T4(5f, 10f, 25f, 50f);
        private static readonly T4 ExplosionDamage = new T4(3f, 6f, 15f, 30f);   // of the owner's damage
        private static readonly T4 ExplosionRadius = new T4(10f, 12f, 16f, 22f);

        private static CharacterMaster lastSummoned;
        private static CharacterMaster extraDrone;
        private static bool summoningExtra;
        private static readonly ConditionalWeakTable<CharacterBody, List<CharacterMaster>> drones = new ConditionalWeakTable<CharacterBody, List<CharacterMaster>>();
        private static readonly ConditionalWeakTable<CharacterMaster, CharacterBody> owners = new ConditionalWeakTable<CharacterMaster, CharacterBody>();

        internal static void Init()
        {
            Hooks.Patch(typeof(SS2.Items.DroidHead.Behavior), "OnKilledOtherServer", typeof(DroidHead), prefix: nameof(Before), postfix: nameof(After));
            Hooks.Patch(typeof(MasterSummon), nameof(MasterSummon.Perform), typeof(DroidHead), postfix: nameof(OnSummon));
            GlobalEventManager.onCharacterDeathGlobal += SelfDestruct;
        }

        private static void SelfDestruct(DamageReport report)
        {
            CharacterBody owner;
            var drone = report.victimBody;
            if (!report.victimMaster || !drone || !owners.TryGetValue(report.victimMaster, out owner) || !owner) return;
            owners.Remove(report.victimMaster);
            var counts = Q.Counts(owner, SS2Content.Items.DroidHead);
            if (!Q.Any(counts)) return;
            var radius = Q.Stack(counts, ExplosionRadius);
            new BlastAttack
            {
                attacker = owner.gameObject,
                inflictor = Sources.Of(SS2Content.Items.DroidHead),
                teamIndex = owner.teamComponent.teamIndex,
                position = drone.corePosition,
                radius = radius,
                baseDamage = owner.damage * Q.Stack(counts, ExplosionDamage),
                baseForce = 1500f,
                crit = owner.RollCrit(),
                procCoefficient = 1f,
                damageColorIndex = DamageColorIndex.Item,
                falloffModel = BlastAttack.FalloffModel.None,
                attackerFiltering = AttackerFiltering.NeverHitSelf
            }.Fire();
            Visuals.Spawn(Fx.SelfDestruct, drone.corePosition, radius);
        }

        private static void Before() { lastSummoned = null; }

        private static void OnSummon(CharacterMaster __result) { lastSummoned = __result; }

        private static void After(SS2.Items.DroidHead.Behavior __instance, DamageReport damageReport)
        {
            var drone = lastSummoned;
            lastSummoned = null;
            if (summoningExtra) extraDrone = drone;
            var owner = __instance.body;
            if (!drone || !drone.GetComponent<MasterSuicideOnTimer>()) return;   // Starstorm2 didn't spawn one
            var counts = Q.Counts(owner, SS2Content.Items.DroidHead);
            if (!Q.Any(counts)) return;

            drone.GetComponent<MasterSuicideOnTimer>().lifeTimer += Q.Stack(counts, ExtraLife);

            var mine = drones.GetOrCreateValue(owner);
            mine.RemoveAll(m => !m);
            var extend = Q.Stack(counts, ExtendOthers);
            foreach (var other in mine)
            {
                var timer = other.GetComponent<MasterSuicideOnTimer>();
                if (timer) timer.lifeTimer += extend;
            }
            mine.Add(drone);
            owners.Remove(drone);
            owners.Add(drone, owner);

            if (summoningExtra || !Util.CheckRoll(Mathf.Min(100f, Q.Sum(counts, ExtraDroneChance)), owner.master)) return;
            extraDrone = null;
            summoningExtra = true;
            try { __instance.OnKilledOtherServer(damageReport); }
            finally { summoningExtra = false; }
            var extra = extraDrone ? extraDrone.GetBody() : null;
            if (extra && extra != drone.GetBody()) TeleportHelper.TeleportBody(extra, extra.footPosition + Random.onUnitSphere * 3f + Vector3.up * 2f, false);
        }
    }
}
