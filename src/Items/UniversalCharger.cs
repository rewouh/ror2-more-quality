using System.Runtime.CompilerServices;
using R2API;
using RoR2;
using SS2;

namespace MoreQuality.Items
{
    // Shorter recharge between refreshes, and a chance to refresh every non-primary skill at once.
    // Overcharge: the next use of a refreshed skill makes you Overcharged, dealing more damage for a moment.
    internal static class UniversalCharger
    {
        private static readonly T4 TimerReduction = new T4(10f, 20f, 50f, 100f);
        private static readonly T4 RefreshAllChance = new T4(10f, 20f, 50f, 100f);
        private static readonly T4 OverchargeDamage = new T4(0.2f, 0.4f, 1f, 2f);
        private const float OverchargeDuration = 2.5f;

        // Skills refreshed by a quality charger whose next use is overcharged (on the owner's game).
        private static readonly ConditionalWeakTable<GenericSkill, object> overcharged = new ConditionalWeakTable<GenericSkill, object>();

        internal static void Init()
        {
            Hooks.Patch(typeof(SS2.Items.UniversalCharger.Behavior), "TryRefresh", typeof(UniversalCharger), prefix: nameof(Before), postfix: nameof(After));
            RecalculateStatsAPI.GetStatCoefficients += (body, args) =>
            {
                if (body.HasBuff(CustomBuffs.Overcharged)) args.damageMultAdd += Q.Sum(Q.Counts(body, SS2Content.Items.UniversalCharger), OverchargeDamage);
            };
        }

        // Starstorm2 calls this every time a skill is used: spend a pending overcharge first.
        private static void Before(SS2.Items.UniversalCharger.Behavior __instance, GenericSkill genericSkill, float ___cooldownTimer, out float __state)
        {
            __state = ___cooldownTimer;
            object pending;
            if (genericSkill && overcharged.TryGetValue(genericSkill, out pending))
            {
                overcharged.Remove(genericSkill);
                __instance.body.AddTimedBuffAuthority(CustomBuffs.Overcharged.buffIndex, OverchargeDuration);
            }
        }

        private static void After(SS2.Items.UniversalCharger.Behavior __instance, GenericSkill genericSkill, ref float ___cooldownTimer, float __state)
        {
            if (___cooldownTimer <= __state) return;   // didn't refresh this time
            var body = __instance.body;
            var counts = Q.Counts(body, SS2Content.Items.UniversalCharger);
            if (!Q.Any(counts)) return;

            var reduction = Util.ConvertAmplificationPercentageIntoReductionPercentage(Q.Sum(counts, TimerReduction)) / 100f;
            ___cooldownTimer *= 1f - reduction;
            if (genericSkill)
            {
                overcharged.Remove(genericSkill);
                overcharged.Add(genericSkill, null);
            }

            if (!body.skillLocator || !Util.CheckRoll(Q.Stack(counts, RefreshAllChance), body.master)) return;
            var skills = body.skillLocator;
            foreach (var skill in new[] { skills.secondary, skills.utility, skills.special })
            {
                if (skill && skill != genericSkill && skill.baseRechargeInterval > 0f && skill.skillDef && skill.skillDef.stockToConsume > 0)
                    skill.ApplyAmmoPack();
            }
        }
    }
}
