using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using R2API;
using RoR2;
using RoR2.Orbs;
using SS2;
using UnityEngine;

namespace MoreQuality.Items
{
    // Higher needle chance, more needles per proc, and needles can make the target bleed.
    // Starstorm2's roll is replaced (for quality holders only) by the same code with the quality numbers.
    // Acupuncture: a needled enemy becomes a Voodoo doll for good. Damage dealt to a voodoo doll is
    // partly copied to every other voodoo doll.
    internal static class Needles
    {
        private static readonly T4 Chance = new T4(25f, 30f, 45f, 70f);
        private static readonly T4 Count = new T4(4f, 5f, 8f, 13f);
        private static readonly T4 BleedChance = new T4(15f, 30f, 75f, 100f);
        private static readonly T4 Shared = new T4(5f, 10f, 25f, 50f);   // percent

        private static ModdedProcType needleProc;
        private static GameObject procEffect;
        private static readonly HashSet<CharacterBody> dolls = new HashSet<CharacterBody>();
        private static bool sharing;

        internal static void Init()
        {
            Hooks.Patch(typeof(SS2.Items.Needles), "OnServerDamageDealt", typeof(Needles), prefix: nameof(OnServerDamageDealt));
            GlobalEventManager.onServerDamageDealt += ShareDamage;
            Run.onRunStartGlobal += _ => dolls.Clear();
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
                totalStrikes = (int)Q.Stack(counts, Count),
                teamIndex = damageReport.attackerTeamIndex,
                attacker = info.attacker,
                procChainMask = info.procChainMask,
                procCoefficient = SS2.Items.Needles.needleProcCoefficient,
                damageColorIndex = DamageColorIndex.Item,
                strikeInterval = SS2.Items.Needles.needleStrikeInterval,
                hitNormal = info.position - attacker.corePosition,
                damageType = Util.CheckRoll(Q.Stack(counts, BleedChance), damageReport.attackerMaster) ? (DamageTypeCombo)DamageType.BleedOnHit : (DamageTypeCombo)DamageType.Generic
            };
            ProcTypeAPI.AddModdedProc(ref orb.procChainMask, needleProc);
            orb.target = info.inflictedHurtbox ? info.inflictedHurtbox : damageReport.victimBody ? damageReport.victimBody.mainHurtBox : null;
            if (orb.target) OrbManager.instance.AddOrb(orb);
            if (procEffect) EffectManager.SimpleEffect(procEffect, info.position, Quaternion.identity, true);

            var victim = damageReport.victimBody;
            if (victim && damageReport.victim.alive)
            {
                var share = (int)Q.Sum(counts, Shared);
                if (victim.GetBuffCount(CustomBuffs.Voodoo) < share) victim.SetBuffCount(CustomBuffs.Voodoo.buffIndex, share);
                dolls.Add(victim);
            }
            return false;
        }

        private static void ShareDamage(DamageReport report)
        {
            var victim = report.victimBody;
            if (sharing || !victim || report.damageDealt <= 0f || !dolls.Contains(victim)) return;
            var share = victim.GetBuffCount(CustomBuffs.Voodoo) / 100f;
            dolls.RemoveWhere(d => !d || !d.healthComponent || !d.healthComponent.alive);
            if (share <= 0f || dolls.Count < 2) return;
            sharing = true;
            try
            {
                foreach (var doll in dolls.ToList())
                {
                    if (doll == victim || doll.teamComponent.teamIndex != victim.teamComponent.teamIndex) continue;
                    doll.healthComponent.TakeDamage(new DamageInfo
                    {
                        attacker = report.attacker,
                        inflictor = Sources.Of(SS2Content.Items.Needles),
                        damage = report.damageDealt * share,
                        position = doll.corePosition,
                        procCoefficient = 0f,
                        damageColorIndex = DamageColorIndex.Item,
                        damageType = DamageType.Silent
                    });
                }
            }
            finally { sharing = false; }
        }
    }
}
