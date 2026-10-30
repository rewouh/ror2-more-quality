using System.Collections.Generic;
using System.Linq;
using RoR2;
using UnityEngine;

namespace MoreQuality
{
    internal static class Enemies
    {
        // Living enemies of `team` near a point, closest first, one entry per character.
        public static IEnumerable<HealthComponent> Near(Vector3 position, float radius, TeamIndex team, HealthComponent exclude = null)
        {
            var search = new BullseyeSearch
            {
                searchOrigin = position,
                searchDirection = Vector3.zero,
                maxDistanceFilter = radius,
                teamMaskFilter = TeamMask.GetEnemyTeams(team),
                filterByLoS = false,
                sortMode = BullseyeSearch.SortMode.Distance
            };
            search.RefreshCandidates();
            return search.GetResults()
                .Where(h => h && h.healthComponent && h.healthComponent.alive && h.healthComponent != exclude && h.healthComponent.body)
                .Select(h => h.healthComponent)
                .Distinct()
                .ToList();
        }

        // Every living enemy of `team` on the whole stage.
        public static List<HealthComponent> All(TeamIndex team)
        {
            var result = new List<HealthComponent>();
            for (var t = TeamIndex.Neutral; t < TeamIndex.Count; t++)
            {
                if (t == team || !TeamMask.GetEnemyTeams(team).HasTeam(t)) continue;
                foreach (var member in TeamComponent.GetTeamMembers(t))
                {
                    if (member && member.body && member.body.healthComponent && member.body.healthComponent.alive) result.Add(member.body.healthComponent);
                }
            }
            return result;
        }
    }
}
