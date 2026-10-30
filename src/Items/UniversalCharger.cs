using ItemQualities;
using RoR2;
using SS2;

namespace MoreQuality.Items
{
    // Shorter recharge between refreshes; Legendary refreshes every non-primary skill at once.
    internal static class UniversalCharger
    {
        private static readonly T4 TimerReduction = new T4(15f, 30f, 45f, 60f);

        internal static void Init()
        {
            Hooks.Patch(typeof(SS2.Items.UniversalCharger.Behavior), "TryRefresh", typeof(UniversalCharger), prefix: nameof(Before), postfix: nameof(After));
        }

        private static void Before(float ___cooldownTimer, out float __state)
        {
            __state = ___cooldownTimer;
        }

        private static void After(SS2.Items.UniversalCharger.Behavior __instance, GenericSkill genericSkill, ref float ___cooldownTimer, float __state)
        {
            if (___cooldownTimer <= __state) return;   // didn't refresh this time
            var body = __instance.body;
            var counts = Q.Counts(body, SS2Content.Items.UniversalCharger);
            if (!Q.Any(counts)) return;

            var reduction = Util.ConvertAmplificationPercentageIntoReductionPercentage(Q.Sum(counts, TimerReduction)) / 100f;
            ___cooldownTimer *= 1f - reduction;

            if (Q.Highest(counts) != QualityTier.Legendary || !body.skillLocator) return;
            var skills = body.skillLocator;
            foreach (var skill in new[] { skills.secondary, skills.utility, skills.special })
            {
                if (skill && skill != genericSkill && skill.baseRechargeInterval > 0f && skill.skillDef && skill.skillDef.stockToConsume > 0)
                    skill.ApplyAmmoPack();
            }
        }
    }
}
