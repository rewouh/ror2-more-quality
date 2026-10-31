using System.Collections.Generic;
using System.Runtime.CompilerServices;
using R2API;
using RoR2;
using SS2;

namespace MoreQuality.Items
{
    // Extra base damage per quality fork, on top of the normal 8% each.
    // Pitchfork: every few hits on the same enemy pin it in place, and pinned enemies take more damage from everyone.
    internal static class Fork
    {
        private static readonly T4 Damage = new T4(0.12f, 0.24f, 0.6f, 1.2f);
        private static readonly T4 HitsToPin = new T4(6f, 5f, 3f, 2f);
        private static readonly T4 PinDuration = new T4(0.5f, 0.8f, 1.5f, 3f);
        private static readonly T4 PinnedDamage = new T4(10f, 20f, 50f, 100f);   // percent

        // Hits each attacker has landed on a victim since its last pin.
        private static readonly ConditionalWeakTable<HealthComponent, Dictionary<CharacterBody, int>> hits = new ConditionalWeakTable<HealthComponent, Dictionary<CharacterBody, int>>();

        internal static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += (body, args) =>
                args.damageMultAdd += Q.Sum(Q.Counts(body, SS2Content.Items.Fork), Damage);
            GlobalEventManager.onServerDamageDealt += OnHit;
        }

        private static void OnHit(DamageReport report)
        {
            var attacker = report.attackerBody;
            var victim = report.victimBody;
            if (!attacker || !victim || !report.victim.alive || report.damageInfo.procCoefficient <= 0f) return;
            var counts = Q.Counts(attacker, SS2Content.Items.Fork);
            if (!Q.Any(counts)) return;
            var mine = hits.GetOrCreateValue(report.victim);
            int n;
            mine.TryGetValue(attacker, out n);
            if (++n < Q.AtHighest(counts, HitsToPin)) { mine[attacker] = n; return; }
            mine[attacker] = 0;
            CustomBuffs.Mark(victim, CustomBuffs.Pinned, (int)Q.Stack(counts, PinnedDamage), Q.Stack(counts, PinDuration));
        }
    }
}
