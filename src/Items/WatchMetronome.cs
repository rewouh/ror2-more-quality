using R2API;
using SS2;

namespace MoreQuality.Items
{
    // Extra damage while the metronome has charge.
    internal static class WatchMetronome
    {
        private static readonly T4 Damage = new T4(0.1f, 0.2f, 0.35f, 0.5f);

        internal static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += (body, args) =>
            {
                if (body.GetBuffCount(SS2Content.Buffs.BuffWatchMetronome) <= 0) return;
                args.damageMultAdd += Q.Sum(Q.Counts(body, SS2Content.Items.WatchMetronome), Damage);
            };
        }
    }
}
