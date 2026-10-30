using R2API;
using RoR2;
using SS2;
using UnityEngine;

namespace MoreQuality.Items
{
    // A bigger zone that also grants crit chance.
    internal static class HuntersSigil
    {
        private static readonly T4 Radius = new T4(10f, 12f, 15f, 20f);
        private static readonly T4 Crit = new T4(10f, 15f, 25f, 40f);

        internal static void Init()
        {
            Hooks.Patch(typeof(SS2.Items.HuntersSigil.Behavior), "FixedUpdate", typeof(HuntersSigil), prefix: nameof(Before), postfix: nameof(After));
            RecalculateStatsAPI.GetStatCoefficients += Stats;
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
    }
}
