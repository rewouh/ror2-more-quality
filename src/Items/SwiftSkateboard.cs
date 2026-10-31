using System.Runtime.CompilerServices;
using R2API;
using RoR2;
using SS2;
using UnityEngine;

namespace MoreQuality.Items
{
    // More kickflip stacks, and each stack also gives attack speed.
    // Grind: at max stacks you leave a burning trail behind you.
    internal static class SwiftSkateboard
    {
        private static readonly T4 MaxStacks = new T4(5f, 6f, 9f, 14f);
        private static readonly T4 AttackSpeedPerStack = new T4(0.03f, 0.06f, 0.15f, 0.3f);
        private static readonly T4 TrailDamage = new T4(1f, 2f, 5f, 10f);   // of your damage, per second
        private static readonly T4 TrailWidth = new T4(1f, 1.25f, 1.6f, 2.2f);

        private static readonly ConditionalWeakTable<CharacterBody, DamageTrail> trails = new ConditionalWeakTable<CharacterBody, DamageTrail>();

        internal static void Init()
        {
            Hooks.Patch(typeof(SS2.Items.SwiftSkateboard.Behavior), "Kickflip", typeof(SwiftSkateboard), prefix: nameof(Before), postfix: nameof(After));
            RecalculateStatsAPI.GetStatCoefficients += (body, args) =>
            {
                var kickflips = body.GetBuffCount(SS2Content.Buffs.BuffKickflip);
                if (kickflips > 0) args.attackSpeedMultAdd += kickflips * Q.Sum(Q.Counts(body, SS2Content.Items.SwiftSkateboard), AttackSpeedPerStack);
            };
            // Like a Blazing elite's trail, it runs on every game so everyone sees it; only the host deals its damage.
            RoR2Application.onFixedUpdate += UpdateTrails;
        }

        private static void Before(SS2.Items.SwiftSkateboard.Behavior __instance, out int __state)
        {
            __state = SS2.Items.SwiftSkateboard.maxStacks;
            var counts = Q.Counts(__instance.body, SS2Content.Items.SwiftSkateboard);
            if (Q.Any(counts)) SS2.Items.SwiftSkateboard.maxStacks = Mathf.Max(__state, (int)Q.AtHighest(counts, MaxStacks));
        }

        private static void After(int __state)
        {
            SS2.Items.SwiftSkateboard.maxStacks = __state;
        }

        private static void UpdateTrails()
        {
            if (!Run.instance) return;
            foreach (var body in CharacterBody.readOnlyInstancesList)
            {
                if (!body) continue;
                DamageTrail trail;
                trails.TryGetValue(body, out trail);
                var counts = Q.Counts(body, SS2Content.Items.SwiftSkateboard);
                var grinding = Q.Any(counts) && body.GetBuffCount(SS2Content.Buffs.BuffKickflip) >= Mathf.Max(SS2.Items.SwiftSkateboard.maxStacks, (int)Q.AtHighest(counts, MaxStacks));
                if (grinding && !trail)
                {
                    trails.Remove(body);
                    trail = Object.Instantiate(LegacyResourcesAPI.Load<GameObject>("Prefabs/FireTrail"), body.gameObject.transform).GetComponent<DamageTrail>();
                    trail.gameObject.transform.position = body.footPosition;
                    trail.owner = body.gameObject;
                    trail.radius *= body.radius * Q.AtHighest(counts, TrailWidth);
                    trails.Add(body, trail);
                }
                else if (!grinding && trail)
                {
                    Object.Destroy(trail.gameObject);
                    trails.Remove(body);
                    continue;
                }
                if (trail) trail.damagePerSecond = body.damage * Q.Stack(counts, TrailDamage);
            }
        }
    }
}
