using ItemQualities;
using RoR2;
using SS2;
using SS2.Components;
using UnityEngine;
using UnityEngine.Networking;

namespace MoreQuality.Items
{
    // More beans, "Espresso" (a bean grabbed at max stacks cuts every cooldown), and nearby beans fly to you.
    internal static class CoffeeBag
    {
        private static readonly T4 DropChance = new T4(0.12f, 0.14f, 0.2f, 0.3f);
        private static readonly T4 Espresso = new T4(0.5f, 1f, 2.5f, 5f);
        private static readonly T4 PullRange = new T4(8f, 15f, 35f, 70f);
        private const float MaxPullRange = 40f;

        internal static void Init()
        {
            Hooks.Patch(typeof(CoffeeBeanPooler), "FixedUpdate", typeof(CoffeeBag), postfix: nameof(Pull));
            Hooks.Patch(typeof(CoffeeBeanPickup), "OnTriggerStay", typeof(CoffeeBag), prefix: nameof(BeforePickup), postfix: nameof(AfterPickup));
            GlobalEventManager.onServerDamageDealt += ExtraRoll;
        }

        // A second roll so the total drop chance matches the quality tier's.
        private static void ExtraRoll(DamageReport report)
        {
            var attacker = report.attackerBody;
            var counts = Q.Counts(attacker, SS2Content.Items.CoffeeBag);
            if (!Q.Any(counts)) return;
            var extra = (Q.AtHighest(counts, DropChance) - SS2.Items.CoffeeBag.procChance) / (1f - SS2.Items.CoffeeBag.procChance);
            if (!Util.CheckRoll(extra * report.damageInfo.procCoefficient * 100f, report.attackerMaster)) return;
            CoffeeBeanPooler.SpawnBean(report.damageInfo.position, attacker.inventory.GetItemCountEffective(SS2Content.Items.CoffeeBag), report.attackerTeamIndex);
        }

        // The bean's own pull only starts when you touch its small trigger. Any loose bean within
        // pull range of a teammate holding a quality bag flies to the closest one instead.
        private static void Pull(CoffeeBeanPooler __instance)
        {
            var gravity = __instance.gravity;
            var bean = __instance.beanis;
            if (!NetworkServer.active || !gravity || gravity.gravitateTarget || !bean || !bean.initialized) return;
            var position = __instance.transform.position;
            CharacterBody closest = null;
            var best = MaxPullRange * MaxPullRange;
            foreach (var body in CharacterBody.readOnlyInstancesList)
            {
                if (!body.teamComponent || body.teamComponent.teamIndex != bean.teamFilter.teamIndex) continue;
                var distance = (body.corePosition - position).sqrMagnitude;
                if (distance >= best) continue;
                var counts = Q.Counts(body, SS2Content.Items.CoffeeBag);
                var range = Q.Stack(counts, PullRange);
                if (!Q.Any(counts) || distance > range * range) continue;
                best = distance;
                closest = body;
            }
            if (closest) gravity.gravitateTarget = closest.gameObject.transform;
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
