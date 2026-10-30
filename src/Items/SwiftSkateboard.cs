using R2API;
using SS2;
using UnityEngine;

namespace MoreQuality.Items
{
    // More kickflip stacks, and each stack also gives attack speed.
    internal static class SwiftSkateboard
    {
        private static readonly T4 MaxStacks = new T4(5f, 6f, 8f, 10f);
        private static readonly T4 AttackSpeedPerStack = new T4(0.04f, 0.06f, 0.09f, 0.12f);

        internal static void Init()
        {
            Hooks.Patch(typeof(SS2.Items.SwiftSkateboard.Behavior), "Kickflip", typeof(SwiftSkateboard), prefix: nameof(Before), postfix: nameof(After));
            RecalculateStatsAPI.GetStatCoefficients += (body, args) =>
            {
                var kickflips = body.GetBuffCount(SS2Content.Buffs.BuffKickflip);
                if (kickflips > 0) args.attackSpeedMultAdd += kickflips * Q.Sum(Q.Counts(body, SS2Content.Items.SwiftSkateboard), AttackSpeedPerStack);
            };
        }

        private static void Before(SS2.Items.SwiftSkateboard.Behavior __instance, out int __state)
        {
            __state = SS2.Items.SwiftSkateboard.maxStacks;
            var counts = Q.Counts(__instance.body, SS2Content.Items.SwiftSkateboard);
            if (Q.Any(counts)) SS2.Items.SwiftSkateboard.maxStacks = Mathf.Max(__state, (int)Q.AtHighest(counts, MaxStacks));
        }

        private static void After(int __state)
        {
            SS2.Items.SwiftSkateboard.maxStacks = __state;
        }
    }
}
