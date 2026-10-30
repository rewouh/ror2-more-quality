using System.Linq;
using HarmonyLib;
using ItemQualities;
using RoR2;
using SS2;
using UnityEngine;

namespace MoreQuality.Items
{
    // An infested enemy that dies passes the infestation on, whatever the health of its neighbours.
    internal static class DetritiveTrematode
    {
        private static readonly T4 Targets = new T4(1f, 2f, 3f, 5f);
        private static readonly T4 Range = new T4(15f, 15f, 20f, 25f);

        internal static void Init()
        {
            GlobalEventManager.onCharacterDeathGlobal += OnDeath;
        }

        private static void OnDeath(DamageReport report)
        {
            var victim = report.victimBody;
            var attacker = report.attackerBody;
            if (!victim || !attacker || !victim.HasBuff(SS2Content.Buffs.BuffTrematodes)) return;
            var counts = Q.Counts(attacker, SS2Content.Items.DetritiveTrematode);
            if (!Q.Any(counts)) return;

            var dot = (DotController.DotIndex)AccessTools.Field(typeof(SS2.Items.DetritiveTrematode), "_trematodeDotIndex").GetValue(null);
            var stack = attacker.inventory.GetItemCountEffective(SS2Content.Items.DetritiveTrematode);
            var targets = Enemies.Near(victim.corePosition, Q.AtHighest(counts, Range), attacker.teamComponent.teamIndex, victim.healthComponent)
                .Where(h => !h.body.HasBuff(SS2Content.Buffs.BuffTrematodes))
                .Take((int)Q.Sum(counts, Targets));
            foreach (var target in targets)
            {
                var info = new InflictDotInfo
                {
                    attackerObject = attacker.gameObject,
                    victimObject = target.gameObject,
                    dotIndex = dot,
                    duration = float.PositiveInfinity,
                    damageMultiplier = SS2.Items.DetritiveTrematode.trematodeDamage * stack
                };
                DotController.InflictDot(ref info);
            }
        }
    }
}
