using System.Linq;
using ItemQualities;
using RoR2;
using SS2;

namespace MoreQuality.Items
{
    // Each enemy the burst can hit refunds utility cooldown (max 3 enemies), and the burst is bigger.
    internal static class CrypticSource
    {
        private static readonly T4 Refund = new T4(0.5f, 1f, 2.5f, 5f);
        private static readonly T4 RadiusMult = new T4(1.1f, 1.2f, 1.5f, 2f);
        private const int MaxEnemies = 3;

        private struct Saved { public float BaseRadius, StackRadius; }

        internal static void Init()
        {
            Hooks.Patch(typeof(SS2.Items.CrypticSource.Behavior), "Fire", typeof(CrypticSource), prefix: nameof(Before), postfix: nameof(After));
        }

        private static void Before(SS2.Items.CrypticSource.Behavior __instance, out Saved __state)
        {
            __state = new Saved { BaseRadius = SS2.Items.CrypticSource.baseRadius, StackRadius = SS2.Items.CrypticSource.stackRadius };
            var body = __instance.body;
            var counts = Q.Counts(body, SS2Content.Items.CrypticSource);
            if (!Q.Any(counts)) return;

            SS2.Items.CrypticSource.baseRadius *= Q.AtHighest(counts, RadiusMult);
            SS2.Items.CrypticSource.stackRadius *= Q.AtHighest(counts, RadiusMult);

            // Cooldowns belong to whoever controls the body, so the refund is applied there.
            if (!body.hasEffectiveAuthority || !body.skillLocator || !body.skillLocator.utility) return;
            var radius = SS2.Items.CrypticSource.baseRadius + SS2.Items.CrypticSource.stackRadius * (__instance.stack - 1);
            var hit = Enemies.Near(body.corePosition, radius, body.teamComponent.teamIndex).Take(MaxEnemies).Count();
            if (hit <= 0) return;
            var utility = body.skillLocator.utility;
            var refund = hit * Q.Sum(counts, Refund);
            RoR2Application.onNextUpdate += () => { if (utility) utility.RunRecharge(refund); };
        }

        private static void After(Saved __state)
        {
            SS2.Items.CrypticSource.baseRadius = __state.BaseRadius;
            SS2.Items.CrypticSource.stackRadius = __state.StackRadius;
        }
    }
}
