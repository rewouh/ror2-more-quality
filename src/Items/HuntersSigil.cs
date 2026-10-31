using HarmonyLib;
using R2API;
using RoR2;
using SS2;
using UnityEngine;

namespace MoreQuality.Items
{
    // A bigger zone that also grants crit chance.
    // Hunting grounds: enemies inside the zone are Hunted, taking more damage from everyone, and stay
    // Hunted for a few seconds after leaving it.
    internal static class HuntersSigil
    {
        private static readonly T4 Radius = new T4(10f, 12f, 16f, 22f);
        private static readonly T4 Crit = new T4(10f, 20f, 50f, 100f);
        private static readonly T4 HuntedDamage = new T4(20f, 40f, 100f, 200f);   // percent
        private static readonly T4 HuntedLinger = new T4(2f, 3f, 5f, 8f);

        private static readonly AccessTools.FieldRef<SS2.Items.HuntersSigil.Behavior, GameObject> sigil = AccessTools.FieldRefAccess<SS2.Items.HuntersSigil.Behavior, GameObject>("sigilInstance");

        internal static void Init()
        {
            Hooks.Patch(typeof(SS2.Items.HuntersSigil.Behavior), "FixedUpdate", typeof(HuntersSigil), prefix: nameof(Before), postfix: nameof(After));
            RecalculateStatsAPI.GetStatCoefficients += Stats;
            Ticker.Every(0.25f, Hunt);
        }

        // The zone radius is a static Starstorm2 reads when it spawns the ward.
        private static void Before(SS2.Items.HuntersSigil.Behavior __instance, out float __state)
        {
            __state = SS2.Items.HuntersSigil.radius;
            var counts = Q.Counts(__instance.body, SS2Content.Items.HuntersSigil);
            if (Q.Any(counts)) SS2.Items.HuntersSigil.radius = Mathf.Max(__state, Q.AtHighest(counts, Radius));
        }

        private static void After(float __state)
        {
            SS2.Items.HuntersSigil.radius = __state;
        }

        // The zone buff doesn't say whose sigil it came from, so use the best quality sigil on the team.
        private static void Stats(CharacterBody body, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (body.GetBuffCount(SS2Content.Buffs.BuffSigilStack) <= 0) return;
            var best = 0f;
            foreach (var member in TeamComponent.GetTeamMembers(body.teamComponent.teamIndex))
            {
                if (member && member.body) best = Mathf.Max(best, Q.Sum(Q.Counts(member.body, SS2Content.Items.HuntersSigil), Crit));
            }
            args.critAdd += best;
        }

        private static void Hunt()
        {
            foreach (var body in CharacterBody.readOnlyInstancesList)
            {
                if (!body) continue;
                var counts = Q.Counts(body, SS2Content.Items.HuntersSigil);
                if (!Q.Any(counts)) continue;
                var behavior = body.GetComponent<SS2.Items.HuntersSigil.Behavior>();
                var zone = behavior ? sigil(behavior) : null;
                var ward = zone ? zone.GetComponent<BuffWard>() : null;
                if (!ward) continue;
                var stacks = (int)Q.Sum(counts, HuntedDamage);
                var linger = Q.Stack(counts, HuntedLinger);
                foreach (var enemy in Enemies.Near(zone.transform.position, ward.radius, body.teamComponent.teamIndex))
                {
                    CustomBuffs.Mark(enemy.body, CustomBuffs.Hunted, stacks, linger);
                }
            }
        }
    }
}
