using HarmonyLib;
using ItemQualities;
using R2API;
using RoR2;
using RoR2.Orbs;
using SS2;
using UnityEngine;

namespace MoreQuality.Items
{
    // Higher needle chance and more needles per proc; Legendary needles make the target bleed.
    // Starstorm2's roll is replaced (for quality holders only) by the same code with the quality numbers.
    internal static class Needles
    {
        private static readonly T4 Chance = new T4(30f, 40f, 55f, 75f);
        private static readonly T4 Count = new T4(4f, 5f, 6f, 8f);

        private static ModdedProcType needleProc;
        private static GameObject procEffect;

        internal static void Init()
        {
            Hooks.Patch(typeof(SS2.Items.Needles), "OnServerDamageDealt", typeof(Needles), prefix: nameof(OnServerDamageDealt));
        }

        private static bool OnServerDamageDealt(DamageReport damageReport)
        {
            var info = damageReport.damageInfo;
            var attacker = damageReport.attackerBody;
            var counts = Q.Counts(attacker, SS2Content.Items.Needles);
            if (!Q.Any(counts)) return true;

            if (procEffect == null)
            {
                needleProc = (ModdedProcType)AccessTools.Field(typeof(SS2.Items.Needles), "needleProcType").GetValue(null);
                procEffect = (GameObject)AccessTools.Field(typeof(SS2.Items.Needles), "_procEffect").GetValue(null);
            }
            if (!info.crit || ProcTypeAPI.HasModdedProc(info.procChainMask, needleProc)) return false;
            if (!Util.CheckRoll(Q.AtHighest(counts, Chance) * info.procCoefficient, damageReport.attackerMaster)) return false;

            var stack = attacker.inventory.GetItemCountEffective(SS2Content.Items.Needles);
            var orb = new SS2.Items.Needles.NeedleOrb
            {
                origin = info.position,
                damageValue = info.damage * SS2.Items.Needles.needleDamageCoefficient * stack,
                isCrit = true,
                totalStrikes = (int)Q.AtHighest(counts, Count),
                teamIndex = damageReport.attackerTeamIndex,
                attacker = info.attacker,
                procChainMask = info.procChainMask,
                procCoefficient = SS2.Items.Needles.needleProcCoefficient,
                damageColorIndex = DamageColorIndex.Item,
                strikeInterval = SS2.Items.Needles.needleStrikeInterval,
                hitNormal = info.position - attacker.corePosition,
                damageType = Q.Highest(counts) == QualityTier.Legendary ? (DamageTypeCombo)DamageType.BleedOnHit : (DamageTypeCombo)DamageType.Generic
            };
            ProcTypeAPI.AddModdedProc(ref orb.procChainMask, needleProc);
            orb.target = info.inflictedHurtbox ? info.inflictedHurtbox : damageReport.victimBody ? damageReport.victimBody.mainHurtBox : null;
            if (orb.target) OrbManager.instance.AddOrb(orb);
            if (procEffect) EffectManager.SimpleEffect(procEffect, info.position, Quaternion.identity, true);
            return false;
        }
    }
}
