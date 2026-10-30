using System.Linq;
using HarmonyLib;
using ItemQualities;
using RoR2;
using RoR2.Projectile;
using SS2;
using UnityEngine;
using UnityEngine.Networking;

namespace MoreQuality.Items
{
    // Haunted Lamp: fires more often, and a kill can fire another projectile from the victim toward the
    // nearest enemy. Those can chain.
    internal static class ShackledLamp
    {
        private static readonly T4 UsesPerShot = new T4(4f, 4f, 3f, 2f);
        private static readonly T4 Chance = new T4(15f, 25f, 40f, 60f);
        private static readonly T4 ChancePerStack = new T4(10f, 10f, 10f, 10f);
        private const float Range = 40f;

        private static GameObject projectile;

        internal static void Init()
        {
            Hooks.Patch(typeof(SS2.Items.ShackledLamp.LampBehavior), "IncrementFire", typeof(ShackledLamp), prefix: nameof(BeforeIncrement));
            GlobalEventManager.onCharacterDeathGlobal += OnDeath;
        }

        // Starstorm2 fires at a hard-coded 5 uses; advancing the counter faster gives "every N uses".
        private static void BeforeIncrement(SS2.Items.ShackledLamp.LampBehavior __instance, ref float ___attackCounter)
        {
            var counts = Q.Counts(__instance.body, SS2Content.Items.ShackledLamp);
            if (!Q.Any(counts)) return;
            ___attackCounter += 5f / Q.AtHighest(counts, UsesPerShot) - 1f + 0.0001f;
        }

        private static void OnDeath(DamageReport report)
        {
            if (!NetworkServer.active) return;
            var inflictor = report.damageInfo.inflictor;
            var attacker = report.attackerBody;
            if (!inflictor || !attacker || !report.victimBody) return;
            if (projectile == null) projectile = (GameObject)AccessTools.Field(typeof(SS2.Items.ShackledLamp), "projectilePrefab").GetValue(null);
            if (!projectile || !inflictor.name.StartsWith(projectile.name)) return;

            var counts = Q.Counts(attacker, SS2Content.Items.ShackledLamp);
            if (!Q.Any(counts) || !Util.CheckRoll(Mathf.Min(90f, Q.Stacked(counts, Chance, ChancePerStack)), attacker.master)) return;

            var origin = report.victimBody.corePosition;
            var target = Enemies.Near(origin, Range, attacker.teamComponent.teamIndex, report.victim).FirstOrDefault();
            if (!target) return;
            var damage = attacker.damage * (2f + attacker.inventory.GetItemCountEffective(SS2Content.Items.ShackledLamp));
            var direction = (target.body.corePosition - origin).normalized;
            ProjectileManager.instance.FireProjectile(new FireProjectileInfo
            {
                projectilePrefab = projectile,
                position = origin + direction * (report.victimBody.radius + 1f),
                rotation = Util.QuaternionSafeLookRotation(direction),
                owner = attacker.gameObject,
                target = target.gameObject,
                damage = damage,
                force = 80f,
                crit = attacker.RollCrit(),
                damageColorIndex = DamageColorIndex.Item
            });
        }
    }
}
