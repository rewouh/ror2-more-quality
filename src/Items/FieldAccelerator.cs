using HarmonyLib;
using R2API;
using RoR2;
using SS2;
using UnityEngine;

namespace MoreQuality.Items
{
    // Kills add more teleporter charge, and allies inside the charging zone attack faster.
    internal static class FieldAccelerator
    {
        private static readonly T4 ChargePerKill = new T4(0.015f, 0.02f, 0.03f, 0.04f);
        private static readonly T4 AttackSpeed = new T4(5f, 10f, 15f, 25f);   // percent
        private static readonly AccessTools.FieldRef<TeleporterInteraction, bool> monstersCleared = AccessTools.FieldRefAccess<TeleporterInteraction, bool>("monstersCleared");

        internal static void Init()
        {
            Hooks.Patch(typeof(SS2.Items.FieldAccelerator.Behavior), "OnKilledOtherServer", typeof(FieldAccelerator), postfix: nameof(OnKill));
            Ticker.Every(0.5f, UpdateAura);
            RecalculateStatsAPI.GetStatCoefficients += (body, args) =>
                args.attackSpeedMultAdd += body.GetBuffCount(CustomBuffs.FieldAura) / 100f;
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
            if (tele && tele.isCharging)
            {
                foreach (var player in PlayerCharacterMasterController.instances)
                    if (player.master) percent += Q.Sum(Q.Counts(player.master, SS2Content.Items.FieldAccelerator), AttackSpeed);
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
