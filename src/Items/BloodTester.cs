using ItemQualities;
using R2API;
using RoR2;
using SS2;

namespace MoreQuality.Items
{
    // Each Blood Tester trigger also grants "Rush": more stats the higher the quality.
    internal static class BloodTester
    {
        private static readonly T4 Duration = new T4(4f, 5f, 6f, 8f);
        private static readonly T4 MoveSpeed = new T4(0.2f, 0.3f, 0.4f, 0.5f);
        private static readonly T4 AttackSpeed = new T4(0f, 0.2f, 0.3f, 0.4f);
        private static readonly T4 Damage = new T4(0f, 0f, 0.2f, 0.3f);
        private static readonly T4 Armor = new T4(0f, 0f, 0f, 30f);

        internal static void Init()
        {
            Hooks.Patch(typeof(SS2.Items.BloodTester.Behavior), "OnGoldCollected", typeof(BloodTester), postfix: nameof(OnGoldCollected));
            RecalculateStatsAPI.GetStatCoefficients += Stats;
        }

        // Starstorm2 resets its stored gold to exactly 0 when it triggers.
        private static void OnGoldCollected(SS2.Items.BloodTester.Behavior __instance, float gold, float ___storedGold)
        {
            if (gold <= 0f || ___storedGold != 0f) return;
            var body = __instance.body;
            var counts = Q.Counts(body, SS2Content.Items.BloodTester);
            if (!Q.Any(counts)) return;
            var duration = Q.AtHighest(counts, Duration) + (counts.TotalQualityCount - 1);
            body.AddTimedBuff(CustomBuffs.BloodRush, duration);
        }

        private static void Stats(CharacterBody body, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (!body.HasBuff(CustomBuffs.BloodRush)) return;
            var counts = Q.Counts(body, SS2Content.Items.BloodTester);
            args.moveSpeedMultAdd += Q.AtHighest(counts, MoveSpeed);
            args.attackSpeedMultAdd += Q.AtHighest(counts, AttackSpeed);
            args.damageMultAdd += Q.AtHighest(counts, Damage);
            args.armorAdd += Q.AtHighest(counts, Armor);
        }
    }
}
