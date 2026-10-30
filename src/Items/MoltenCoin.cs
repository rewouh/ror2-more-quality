using ItemQualities;
using RoR2;
using SS2;
using UnityEngine;

namespace MoreQuality.Items
{
    // Higher ignite chance, and killing a burning enemy pays out gold.
    internal static class MoltenCoin
    {
        private static readonly T4 Chance = new T4(8f, 10f, 13f, 18f);
        private static readonly T4 Gold = new T4(2f, 4f, 6f, 10f);

        internal static void Init()
        {
            Hooks.Patch(typeof(SS2.Items.MoltenCoin.Behavior), "OnDamageDealtServer", typeof(MoltenCoin), prefix: nameof(Before), postfix: nameof(After));
            GlobalEventManager.onCharacterDeathGlobal += OnDeath;
        }

        // Starstorm2 reads its chance from a static field, so swap it for the duration of the roll.
        private static void Before(SS2.Items.MoltenCoin.Behavior __instance, out float __state)
        {
            __state = SS2.Items.MoltenCoin.procChance;
            var counts = Q.Counts(__instance.body, SS2Content.Items.MoltenCoin);
            if (Q.Any(counts)) SS2.Items.MoltenCoin.procChance = Mathf.Max(__state, Q.AtHighest(counts, Chance));
        }

        private static void After(float __state)
        {
            SS2.Items.MoltenCoin.procChance = __state;
        }

        private static void OnDeath(DamageReport report)
        {
            var victim = report.victimBody;
            if (!victim || !report.attackerMaster) return;
            if (!victim.HasBuff(RoR2Content.Buffs.OnFire) && !victim.HasBuff(DLC1Content.Buffs.StrongerBurn)) return;
            var counts = Q.Counts(report.attackerMaster, SS2Content.Items.MoltenCoin);
            if (!Q.Any(counts)) return;
            var gold = Run.instance.GetDifficultyScaledCost((int)Q.Sum(counts, Gold), Stage.instance ? Stage.instance.entryDifficultyCoefficient : Run.instance.difficultyCoefficient);
            report.attackerMaster.GiveMoney((uint)gold);
            EffectManager.SimpleImpactEffect(LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/ImpactEffects/CoinImpact"), victim.corePosition, Vector3.up, true);
        }
    }
}
