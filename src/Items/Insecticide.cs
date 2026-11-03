using System.Collections.Generic;
using RoR2;
using SS2;
using UnityEngine;
using UnityEngine.Networking;

namespace MoreQuality.Items
{
    // Every poison tick has a small chance to kill the enemy outright. Never works on bosses.
    // Pesticide cloud: a poisoned enemy that dies leaves a toxic cloud on the ground that keeps hurting
    // enemies standing in it.
    internal static class Insecticide
    {
        private static readonly T4 KillChance = new T4(0.25f, 0.5f, 1.25f, 2.5f);
        private static readonly T4 KillChancePerStack = new T4(0.25f, 0.25f, 0.5f, 0.5f);
        private static readonly T4 CloudRadius = new T4(4f, 5f, 7f, 10f);
        private static readonly T4 CloudDuration = new T4(3f, 4f, 6f, 8f);
        private static readonly T4 CloudDamage = new T4(1f, 2f, 5f, 10f);   // of the holder's damage, per second
        private const float TickInterval = 0.5f;

        private sealed class Cloud
        {
            public CharacterBody Owner;
            public TeamIndex Team;
            public Vector3 Position;
            public float Radius, DamagePerTick, Until;
        }

        private static readonly List<Cloud> clouds = new List<Cloud>();

        internal static void Init()
        {
            GlobalEventManager.onServerDamageDealt += OnTick;
            GlobalEventManager.onCharacterDeathGlobal += OnDeath;
            Ticker.Every(TickInterval, Hurt);
            Run.onRunStartGlobal += _ => clouds.Clear();
        }

        private static void OnTick(DamageReport report)
        {
            if (report.damageInfo.dotIndex != SS2.Items.Insecticide.DotIndex) return;
            var victim = report.victimBody;
            if (!victim || victim.isBoss || !report.victim.alive) return;
            var counts = Q.Counts(report.attackerMaster, SS2Content.Items.Insecticide);
            if (!Q.Any(counts) || !Util.CheckRoll(Q.Stacked(counts, KillChance, KillChancePerStack), report.attackerMaster)) return;
            report.victim.Suicide(report.attacker, report.attacker, DamageType.Generic);
        }

        private static void OnDeath(DamageReport report)
        {
            var victim = report.victimBody;
            if (!NetworkServer.active || !victim) return;
            var dots = DotController.FindDotController(victim.gameObject);
            if (!dots || !dots.HasDotActive(SS2.Items.Insecticide.DotIndex)) return;

            // The killer's cloud if they hold a quality Insecticide, else the best one on the players' team.
            var owner = report.attackerBody;
            var counts = Q.Counts(owner, SS2Content.Items.Insecticide);
            if (!Q.Any(counts))
            {
                owner = Dlc.TeamHolder(SS2Content.Items.Insecticide, Dlc.TeamHighest(SS2Content.Items.Insecticide));
                counts = Q.Counts(owner, SS2Content.Items.Insecticide);
            }
            if (!Q.Any(counts) || owner.teamComponent.teamIndex == victim.teamComponent.teamIndex) return;

            var cloud = new Cloud
            {
                Owner = owner,
                Team = owner.teamComponent.teamIndex,
                Position = victim.footPosition,
                Radius = Q.Stack(counts, CloudRadius),
                DamagePerTick = owner.damage * Q.Stack(counts, CloudDamage) * TickInterval,
                Until = Time.fixedTime + Q.Stack(counts, CloudDuration)
            };
            clouds.Add(cloud);
            Visuals.Spawn(Fx.PoisonZone, cloud.Position + Vector3.up * 0.2f, cloud.Radius, cloud.Until - Time.fixedTime);
        }

        private static void Hurt()
        {
            var now = Time.fixedTime;
            clouds.RemoveAll(c => c.Until < now || !c.Owner);
            foreach (var cloud in clouds)
            {
                new BlastAttack
                {
                    attacker = cloud.Owner.gameObject,
                    inflictor = Sources.Of(SS2Content.Items.Insecticide),
                    teamIndex = cloud.Team,
                    position = cloud.Position,
                    radius = cloud.Radius,
                    baseDamage = cloud.DamagePerTick,
                    crit = false,
                    procCoefficient = 0.2f,
                    damageColorIndex = DamageColorIndex.Poison,
                    falloffModel = BlastAttack.FalloffModel.None,
                    attackerFiltering = AttackerFiltering.NeverHitSelf
                }.Fire();
            }
        }
    }
}
