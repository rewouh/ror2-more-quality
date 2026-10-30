using R2API;
using SS2;
using UnityEngine;

namespace MoreQuality.Items
{
    // Triggers on smaller hits, and the buff hits harder.
    internal static class GreenChocolate
    {
        private static readonly T4 Threshold = new T4(0.17f, 0.15f, 0.12f, 0.08f);
        private static readonly T4 BuffDamage = new T4(0.6f, 0.75f, 1f, 1.5f);
        private static readonly T4 BuffCrit = new T4(25f, 30f, 40f, 50f);

        internal static void Init()
        {
            Hooks.Patch(typeof(SS2.Items.GreenChocolate.Behavior), "OnIncomingDamageServer", typeof(GreenChocolate), prefix: nameof(Before), postfix: nameof(After));
            // Starstorm2 already adds the base buff values; this adds the difference.
            RecalculateStatsAPI.GetStatCoefficients += (body, args) =>
            {
                if (!body.HasBuff(SS2Content.Buffs.BuffChocolate)) return;
                var counts = Q.Counts(body, SS2Content.Items.GreenChocolate);
                if (!Q.Any(counts)) return;
                args.damageMultAdd += Mathf.Max(0f, Q.AtHighest(counts, BuffDamage) - SS2.Items.GreenChocolate.buffDamage);
                args.critAdd += Mathf.Max(0f, Q.AtHighest(counts, BuffCrit) - SS2.Items.GreenChocolate.buffCrit);
            };
        }

        private static void Before(SS2.Items.GreenChocolate.Behavior __instance, out float __state)
        {
            __state = SS2.Items.GreenChocolate.damageThreshold;
            var counts = Q.Counts(__instance.body, SS2Content.Items.GreenChocolate);
            if (Q.Any(counts)) SS2.Items.GreenChocolate.damageThreshold = Mathf.Min(__state, Q.AtHighest(counts, Threshold));
        }

        private static void After(float __state)
        {
            SS2.Items.GreenChocolate.damageThreshold = __state;
        }
    }
}
