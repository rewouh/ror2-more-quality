using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ItemQualities;
using RoR2;
using SS2;
using UnityEngine;
using MaliceOrb = SS2.Items.Malice.Behavior.MaliceOrb;

namespace MoreQuality.Items
{
    // When Malice's splash runs out of bounces, the enemy it last hit can pass it on again.
    // Each extra hop rolls separately, up to a cap.
    internal static class Malice
    {
        private static readonly T4 Chance = new T4(0.1f, 0.2f, 0.5f, 0.9f);
        private static readonly T4 MaxHops = new T4(2f, 4f, 10f, 20f);

        // Every orb of one Malice chain shares the same bouncedObjects list, so it identifies the chain.
        private sealed class Hops { public int Count; }
        private static readonly ConditionalWeakTable<List<HealthComponent>, Hops> hops = new ConditionalWeakTable<List<HealthComponent>, Hops>();

        internal static void Init()
        {
            Hooks.Patch(typeof(MaliceOrb), "OnArrival", typeof(Malice), prefix: nameof(OnArrival));
        }

        private static void OnArrival(MaliceOrb __instance)
        {
            if (__instance.bouncesRemaining > 0 || __instance.bouncedObjects == null || !__instance.attacker) return;
            var counts = Q.Counts(__instance.attacker.GetComponent<CharacterBody>(), SS2Content.Items.Malice);
            if (!Q.Any(counts)) return;

            var chain = hops.GetOrCreateValue(__instance.bouncedObjects);
            if (chain.Count >= (int)Q.Stack(counts, MaxHops)) return;
            var chance = Mathf.Min(0.9f, Q.Sum(counts, Chance));
            if (!Util.CheckRoll(chance * 100f)) return;
            chain.Count++;
            __instance.bouncesRemaining = 1;   // Starstorm2's OnArrival then fires the next hop itself
        }
    }
}
