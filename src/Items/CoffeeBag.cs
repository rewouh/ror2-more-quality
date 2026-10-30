using ItemQualities;
using RoR2;
using SS2;
using SS2.Components;
using UnityEngine;
using UnityEngine.Networking;

namespace MoreQuality.Items
{
    // More beans, "Espresso" (a bean grabbed at max stacks cuts every cooldown), and Legendary beans fly to you.
    internal static class CoffeeBag
    {
        private static readonly T4 DropChance = new T4(0.12f, 0.14f, 0.17f, 0.22f);
        private static readonly T4 Espresso = new T4(0.5f, 1f, 1.5f, 2.5f);

        private static CharacterBody beanOwner;     // who the bean being spawned right now belongs to
        private static CoffeeBeanPooler lastBean;

        internal static void Init()
        {
            Hooks.Patch(typeof(SS2.Items.CoffeeBag), "OnServerDamageDealt", typeof(CoffeeBag), prefix: nameof(BeforeBaseRoll), postfix: nameof(AfterBaseRoll));
            Hooks.Patch(typeof(CoffeeBeanPooler), "GetPooledBean", typeof(CoffeeBag), postfix: nameof(OnGetPooledBean));
            Hooks.Patch(typeof(CoffeeBeanPooler), "SpawnBean", typeof(CoffeeBag), postfix: nameof(OnSpawnBean));
            Hooks.Patch(typeof(CoffeeBeanPickup), "OnTriggerStay", typeof(CoffeeBag), prefix: nameof(BeforePickup), postfix: nameof(AfterPickup));
            GlobalEventManager.onServerDamageDealt += ExtraRoll;
        }

        private static void BeforeBaseRoll(DamageReport report) { beanOwner = report.attackerBody; }
        private static void AfterBaseRoll() { beanOwner = null; }

        // A second roll so the total drop chance matches the quality tier's.
        private static void ExtraRoll(DamageReport report)
        {
            var attacker = report.attackerBody;
            var counts = Q.Counts(attacker, SS2Content.Items.CoffeeBag);
            if (!Q.Any(counts)) return;
            var extra = (Q.AtHighest(counts, DropChance) - SS2.Items.CoffeeBag.procChance) / (1f - SS2.Items.CoffeeBag.procChance);
            if (!Util.CheckRoll(extra * report.damageInfo.procCoefficient * 100f, report.attackerMaster)) return;
            beanOwner = attacker;
            CoffeeBeanPooler.SpawnBean(report.damageInfo.position, attacker.inventory.GetItemCountEffective(SS2Content.Items.CoffeeBag), report.attackerTeamIndex);
            beanOwner = null;
        }

        private static void OnGetPooledBean(CoffeeBeanPooler __result) { lastBean = __result; }

        private static void OnSpawnBean()
        {
            if (!NetworkServer.active || !beanOwner || !lastBean || !lastBean.gravity) return;
            if (Q.Highest(Q.Counts(beanOwner, SS2Content.Items.CoffeeBag)) != QualityTier.Legendary) return;
            lastBean.gravity.gravitateTarget = beanOwner.transform;
        }

        private static void BeforePickup(Collider other, bool ___alive, bool ___initialized, out bool __state)
        {
            __state = false;
            if (!NetworkServer.active || !___alive || !___initialized || !other) return;
            var body = other.GetComponent<CharacterBody>();
            if (!body) return;
            __state = body.GetBuffCount(SS2Content.Buffs.BuffCoffeeBag) >= SS2.Items.CoffeeBag.maxStax;
        }

        private static void AfterPickup(Collider other, bool ___alive, bool __state)
        {
            if (!__state || ___alive) return;   // only beans actually picked up while already at 5+ beans
            var body = other.GetComponent<CharacterBody>();
            var counts = Q.Counts(body, SS2Content.Items.CoffeeBag);
            if (!Q.Any(counts) || !body.skillLocator) return;
            body.skillLocator.DeductCooldownFromAllSkillsServer(Q.Sum(counts, Espresso));
        }
    }
}
