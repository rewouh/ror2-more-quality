using ItemQualities;
using RoR2;
using SS2;
using UnityEngine;
using UnityEngine.Networking;

namespace MoreQuality.Items
{
    // Intoxicated enemies explode on death; bigger blast the more stacks they had, and everything it
    // touches gets intoxicated.
    internal static class StrangeCan
    {
        private const float BaseRadius = 2f;
        private static readonly T4 RadiusPerStack = new T4(0.5f, 1f, 1.5f, 2.5f);
        private static readonly T4 Applied = new T4(1f, 2f, 3f, 5f);
        private static readonly T4 AppliedPerStack = new T4(1f, 1f, 1f, 1f);

        private static GameObject explosion;

        internal static void Init()
        {
            GlobalEventManager.onCharacterDeathGlobal += OnDeath;
        }

        private static void OnDeath(DamageReport report)
        {
            var victim = report.victimBody;
            var attacker = report.attackerBody;
            if (!NetworkServer.active || !victim || !attacker) return;
            var stacks = victim.GetBuffCount(SS2Content.Buffs.BuffIntoxicated);
            if (stacks <= 0) return;
            var counts = Q.Counts(attacker, SS2Content.Items.StrangeCan);
            if (!Q.Any(counts)) return;

            var radius = BaseRadius + stacks * Q.AtHighest(counts, RadiusPerStack);
            var applied = Mathf.RoundToInt(Q.Stacked(counts, Applied, AppliedPerStack));
            foreach (var enemy in Enemies.Near(victim.corePosition, radius, attacker.teamComponent.teamIndex, report.victim))
            {
                for (var i = 0; i < applied; i++)
                {
                    var info = new InflictDotInfo
                    {
                        attackerObject = attacker.gameObject,
                        victimObject = enemy.gameObject,
                        dotIndex = SS2.Items.StrangeCan.DotIndex,
                        duration = SS2.Items.StrangeCan.buffDuration,
                        maxStacksFromAttacker = (uint)SS2.Items.StrangeCan.maxStacks,
                        damageMultiplier = SS2.Items.StrangeCan.damageCoefficient
                    };
                    DotController.InflictDot(ref info);
                }
            }

            if (!explosion) explosion = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/ImpactEffects/CrocoDiseaseImpactEffect")
                ?? LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/OmniEffect/OmniExplosionVFXQuick");
            if (explosion) EffectManager.SpawnEffect(explosion, new EffectData { origin = victim.corePosition, scale = radius }, true);
        }
    }
}
