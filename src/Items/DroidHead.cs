using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ItemQualities;
using RoR2;
using SS2;
using UnityEngine;

namespace MoreQuality.Items
{
    // Drones last longer, a second drone can spawn, and each new drone extends the ones you already have.
    internal static class DroidHead
    {
        private static readonly T4 ExtraLife = new T4(10f, 20f, 35f, 60f);
        private static readonly T4 ExtraDroneChance = new T4(15f, 30f, 50f, 75f);
        private static readonly T4 ExtendOthers = new T4(5f, 10f, 15f, 25f);

        private static CharacterMaster lastSummoned;
        private static CharacterMaster extraDrone;
        private static bool summoningExtra;
        private static readonly ConditionalWeakTable<CharacterBody, List<CharacterMaster>> drones = new ConditionalWeakTable<CharacterBody, List<CharacterMaster>>();

        internal static void Init()
        {
            Hooks.Patch(typeof(SS2.Items.DroidHead.Behavior), "OnKilledOtherServer", typeof(DroidHead), prefix: nameof(Before), postfix: nameof(After));
            Hooks.Patch(typeof(MasterSummon), nameof(MasterSummon.Perform), typeof(DroidHead), postfix: nameof(OnSummon));
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

            drone.GetComponent<MasterSuicideOnTimer>().lifeTimer += Q.AtHighest(counts, ExtraLife);

            var mine = drones.GetOrCreateValue(owner);
            mine.RemoveAll(m => !m);
            var extend = Q.AtHighest(counts, ExtendOthers);
            foreach (var other in mine)
            {
                var timer = other.GetComponent<MasterSuicideOnTimer>();
                if (timer) timer.lifeTimer += extend;
            }
            mine.Add(drone);

            if (summoningExtra || !Util.CheckRoll(Mathf.Min(100f, Q.Sum(counts, ExtraDroneChance)), owner.master)) return;
            extraDrone = null;
            summoningExtra = true;
            try { __instance.OnKilledOtherServer(damageReport); }
            finally { summoningExtra = false; }
            var extra = extraDrone ? extraDrone.GetBody() : null;
            if (extra && extra != drone.GetBody()) TeleportHelper.TeleportBody(extra, extra.transform.position + Random.onUnitSphere * 3f + Vector3.up * 2f, false);
        }
    }
}
