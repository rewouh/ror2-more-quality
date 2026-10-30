using ItemQualities;
using RoR2;
using SS2;

namespace MoreQuality.Items
{
    // Every poison tick has a small chance to kill the enemy outright. Never works on bosses.
    internal static class Insecticide
    {
        private static readonly T4 KillChance = new T4(0.25f, 0.5f, 1f, 1.5f);
        private static readonly T4 KillChancePerStack = new T4(0.25f, 0.25f, 0.5f, 0.5f);

        internal static void Init()
        {
            GlobalEventManager.onServerDamageDealt += OnTick;
        }

        private static void OnTick(DamageReport report)
        {
            if (report.damageInfo.dotIndex != SS2.Items.Insecticide.DotIndex) return;
            var victim = report.victimBody;
            if (!victim || victim.isBoss || !report.victim.alive) return;
            var counts = Q.Counts(report.attackerMaster, SS2Content.Items.Insecticide);
            if (!Q.Any(counts) || !Util.CheckRoll(Q.Stacked(counts, KillChance, KillChancePerStack), report.attackerMaster)) return;
            report.victim.Suicide(report.attacker, report.attacker, DamageType.Generic);
        }
    }
}
