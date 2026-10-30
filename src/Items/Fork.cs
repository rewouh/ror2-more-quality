using R2API;
using RoR2;
using SS2;

namespace MoreQuality.Items
{
    // Extra base damage per quality fork, on top of the normal 8% each.
    internal static class Fork
    {
        private static readonly T4 Damage = new T4(0.16f, 0.28f, 0.44f, 0.64f);

        internal static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += (body, args) =>
                args.damageMultAdd += Q.Sum(Q.Counts(body, SS2Content.Items.Fork), Damage);
        }
    }
}
