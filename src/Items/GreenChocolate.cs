using System.Linq;
using R2API;
using RoR2;
using SS2;
using UnityEngine;
using UnityEngine.Networking;

namespace MoreQuality.Items
{
    // Triggers on smaller hits, and the buff hits harder.
    // Comfort food: each trigger also drops a chocolate bar. Whoever picks it up is cleansed of debuffs, and
    // every ally near them gets the chocolate buff too.
    internal static class GreenChocolate
    {
        private static readonly T4 Threshold = new T4(0.18f, 0.15f, 0.1f, 0.05f);
        private static readonly T4 BuffDamage = new T4(0.6f, 0.75f, 1.25f, 2f);
        private static readonly T4 BuffCrit = new T4(25f, 30f, 45f, 70f);
        private static readonly T4 ShareRadius = new T4(10f, 15f, 25f, 40f);
        private static readonly T4 ShareDuration = new T4(3f, 5f, 8f, 15f);

        private struct State { public float Threshold; public bool Triggered; }

        // Marks a health pack as a chocolate (server side only).
        private sealed class Chocolate : MonoBehaviour { public float radius, duration; public bool eaten; }

        internal static void Init()
        {
            Hooks.Patch(typeof(SS2.Items.GreenChocolate.Behavior), "OnIncomingDamageServer", typeof(GreenChocolate), prefix: nameof(Before), postfix: nameof(After));
            Hooks.Patch(typeof(HealthPickup), "OnTriggerStay", typeof(GreenChocolate), prefix: nameof(Eat));
            // Starstorm2 already adds the base buff values; this adds the difference.
            RecalculateStatsAPI.GetStatCoefficients += (body, args) =>
            {
                if (!body.HasBuff(SS2Content.Buffs.BuffChocolate)) return;
                var counts = Q.Counts(body, SS2Content.Items.GreenChocolate);
                if (!Q.Any(counts)) return;
                args.damageMultAdd += Mathf.Max(0f, Q.Stack(counts, BuffDamage) - SS2.Items.GreenChocolate.buffDamage);
                args.critAdd += Mathf.Max(0f, Q.Stack(counts, BuffCrit) - SS2.Items.GreenChocolate.buffCrit);
            };
        }

        private static void Before(SS2.Items.GreenChocolate.Behavior __instance, DamageInfo damageInfo, out State __state)
        {
            __state = new State { Threshold = SS2.Items.GreenChocolate.damageThreshold };
            var body = __instance.body;
            var counts = Q.Counts(body, SS2Content.Items.GreenChocolate);
            if (!Q.Any(counts)) return;
            SS2.Items.GreenChocolate.damageThreshold = Mathf.Min(__state.Threshold, Q.AtHighest(counts, Threshold));
            __state.Triggered = damageInfo.damage >= body.healthComponent.fullCombinedHealth * SS2.Items.GreenChocolate.damageThreshold;
        }

        private static void After(SS2.Items.GreenChocolate.Behavior __instance, State __state)
        {
            SS2.Items.GreenChocolate.damageThreshold = __state.Threshold;
            if (!__state.Triggered) return;
            var body = __instance.body;
            var counts = Q.Counts(body, SS2Content.Items.GreenChocolate);
            var prefab = Fx.Chocolate ? Fx.Chocolate : LegacyResourcesAPI.Load<GameObject>("Prefabs/NetworkedObjects/HealPack");
            var pack = Object.Instantiate(prefab, DropPoint(body), Quaternion.identity);
            var filter = pack.GetComponentInChildren<TeamFilter>();
            if (filter) filter.teamIndex = body.teamComponent.teamIndex;
            var pickup = pack.GetComponentInChildren<HealthPickup>();
            if (pickup)
            {
                pickup.flatHealing = 0f;
                pickup.fractionalHealing = 0f;
                pickup.pickupEffect = null;
                var chocolate = pickup.gameObject.AddComponent<Chocolate>();
                chocolate.radius = Q.Stack(counts, ShareRadius);
                chocolate.duration = Q.Stack(counts, ShareDuration);
            }
            NetworkServer.Spawn(pack);
        }

        // A spot a few metres away in a random direction, on the ground, so you can see it and walk to it.
        private static Vector3 DropPoint(CharacterBody body)
        {
            for (var attempt = 0; attempt < 6; attempt++)
            {
                var direction = Random.insideUnitCircle.normalized * Random.Range(4f, 8f);
                var above = body.corePosition + new Vector3(direction.x, 3f, direction.y);
                RaycastHit hit;
                if (Physics.Raycast(above, Vector3.down, out hit, 15f, LayerIndex.world.mask, QueryTriggerInteraction.Ignore))
                    return hit.point + Vector3.up * 1f;
            }
            return body.corePosition + Vector3.up * 2f;
        }

        // Same pickup conditions as the game's; the game then destroys the pack as usual.
        private static void Eat(HealthPickup __instance, Collider other)
        {
            var chocolate = __instance.GetComponent<Chocolate>();
            if (!NetworkServer.active || !chocolate || chocolate.eaten || !other) return;
            var eater = other.GetComponent<CharacterBody>();
            if (!eater || TeamComponent.GetObjectTeam(other.gameObject) != __instance.teamFilter.teamIndex) return;
            chocolate.eaten = true;
            Util.CleanseBody(eater, true, false, false, true, true, false);
            var radius = chocolate.radius;
            foreach (var member in TeamComponent.GetTeamMembers(eater.teamComponent.teamIndex).ToList())
            {
                var ally = member ? member.body : null;
                if (ally && (ally.corePosition - eater.corePosition).sqrMagnitude <= radius * radius)
                    ally.AddTimedBuff(SS2Content.Buffs.BuffChocolate, chocolate.duration);
            }
            Visuals.Spawn(Fx.ChocolateEaten, eater.corePosition, radius);
        }
    }
}
