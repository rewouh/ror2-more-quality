using ItemQualities;
using RoR2;
using SS2;

namespace MoreQuality.Items
{
    // Hits the amulet reduces from behind also grant barrier (a share of the whole hit) and stun the attacker.
    internal static class GuardingAmulet
    {
        private static readonly T4 Barrier = new T4(0.1f, 0.2f, 0.5f, 1f);
        private static readonly T4 Stun = new T4(0.5f, 1f, 2f, 4f);

        internal static void Init()
        {
            Hooks.Patch(typeof(SS2.Items.GuardingAmulet.Behavior), "OnIncomingDamageServer", typeof(GuardingAmulet), prefix: nameof(Before), postfix: nameof(After));
        }

        private static void Before(DamageInfo damageInfo, out float __state)
        {
            __state = damageInfo.damage;
        }

        private static void After(SS2.Items.GuardingAmulet.Behavior __instance, DamageInfo damageInfo, float __state)
        {
            var blocked = __state - damageInfo.damage;
            if (blocked <= 0f) return;
            var body = __instance.body;
            var counts = Q.Counts(body, SS2Content.Items.GuardingAmulet);
            if (!Q.Any(counts)) return;
            body.healthComponent.AddBarrier(__state * Q.Sum(counts, Barrier));
            if (damageInfo.attacker) SetStateOnHurt.SetStunOnObject(damageInfo.attacker, Q.Stack(counts, Stun));
        }
    }
}
