using HarmonyLib;
using R2API;
using RoR2;
using RoR2.Projectile;
using SS2;
using UnityEngine;

namespace MoreQuality.Items
{
    // Kills add more teleporter charge, and allies inside the charging zone attack faster.
    // Accelerated particles: enemies inside the zone are slowed, and allies' projectiles fly faster.
    internal static class FieldAccelerator
    {
        private static readonly T4 ChargePerKill = new T4(0.015f, 0.02f, 0.035f, 0.06f);
        private static readonly T4 AttackSpeed = new T4(5f, 10f, 25f, 50f);   // percent
        private static readonly T4 EnemySlow = new T4(10f, 20f, 40f, 70f);    // percent
        private static readonly T4 ProjectileSpeed = new T4(0.1f, 0.2f, 0.5f, 1f);
        private static readonly AccessTools.FieldRef<TeleporterInteraction, bool> monstersCleared = AccessTools.FieldRefAccess<TeleporterInteraction, bool>("monstersCleared");

        internal static void Init()
        {
            Hooks.Patch(typeof(SS2.Items.FieldAccelerator.Behavior), "OnKilledOtherServer", typeof(FieldAccelerator), postfix: nameof(OnKill));
            Ticker.Every(0.5f, UpdateAura);
            RecalculateStatsAPI.GetStatCoefficients += (body, args) =>
                args.attackSpeedMultAdd += body.GetBuffCount(CustomBuffs.FieldAura) / 100f;
            Hooks.Patch(typeof(ProjectileManager), nameof(ProjectileManager.FireProjectile), typeof(FieldAccelerator), prefix: nameof(FasterProjectile), args: new[] { typeof(FireProjectileInfo) });
        }

        // Runs on whichever game fires the projectile, so the bonus is read from the (synced) inventories.
        private static void FasterProjectile(ref FireProjectileInfo fireProjectileInfo)
        {
            var owner = fireProjectileInfo.owner ? fireProjectileInfo.owner.GetComponent<CharacterBody>() : null;
            if (!owner || !owner.HasBuff(CustomBuffs.FieldAura) || !fireProjectileInfo.projectilePrefab) return;
            var simple = fireProjectileInfo.projectilePrefab.GetComponent<ProjectileSimple>();
            if (!simple) return;
            var bonus = 0f;
            foreach (var player in PlayerCharacterMasterController.instances)
                if (player.master) bonus += Q.Sum(Q.Counts(player.master, SS2Content.Items.FieldAccelerator), ProjectileSpeed);
            var speed = fireProjectileInfo.useSpeedOverride ? fireProjectileInfo.speedOverride : simple.desiredForwardSpeed;
            if (bonus <= 0f || speed <= 0f) return;
            fireProjectileInfo.speedOverride = speed * (1f + bonus);
        }

        // Same conditions as Starstorm2's own charge bonus, topped up to the quality amount.
        private static void OnKill(SS2.Items.FieldAccelerator.Behavior __instance, DamageReport damageReport)
        {
            var tele = TeleporterInteraction.instance;
            var body = __instance.body;
            if (!tele || !tele.isCharging || damageReport.attackerBody != body) return;
            if (tele.activationState != TeleporterInteraction.ActivationState.Charging || !monstersCleared(tele)) return;
            if (!tele.holdoutZoneController.IsBodyInChargingRadius(damageReport.victimBody)) return;
            var counts = Q.Counts(body, SS2Content.Items.FieldAccelerator);
            if (!Q.Any(counts)) return;
            var extra = Q.AtHighest(counts, ChargePerKill) - SS2.Items.FieldAccelerator.chargePerKill;
            if (extra > 0f) tele.holdoutZoneController.Network_charge = Mathf.Min(1f, tele.holdoutZoneController.charge + extra);
        }

        private static void UpdateAura()
        {
            var tele = TeleporterInteraction.instance;
            var percent = 0f;
            var slow = 0f;
            if (tele && tele.isCharging)
            {
                foreach (var player in PlayerCharacterMasterController.instances)
                {
                    if (!player.master) continue;
                    var counts = Q.Counts(player.master, SS2Content.Items.FieldAccelerator);
                    percent += Q.Sum(counts, AttackSpeed);
                    slow = Mathf.Max(slow, Q.Stack(counts, EnemySlow));
                }
            }
            if (slow > 0f)
            {
                foreach (var enemy in Enemies.All(TeamIndex.Player))
                    if (tele.holdoutZoneController.IsBodyInChargingRadius(enemy.body)) CustomBuffs.Mark(enemy.body, CustomBuffs.Slowed, (int)slow, 0.75f);
            }
            foreach (var member in TeamComponent.GetTeamMembers(TeamIndex.Player))
            {
                var body = member ? member.body : null;
                if (!body) continue;
                var inside = percent > 0f && tele.holdoutZoneController.IsBodyInChargingRadius(body);
                var stacks = inside ? Mathf.RoundToInt(percent) : 0;
                if (body.GetBuffCount(CustomBuffs.FieldAura) != stacks) body.SetBuffCount(CustomBuffs.FieldAura.buffIndex, stacks);
            }
        }
    }
}
