using EntityStates;
using RoR2;
using RoR2.Projectile;
using SS2;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace MoreQuality.Items
{
    // Faster recharge, a bigger blast, and a chance for the boots to be ready again right away.
    // Afterburner: every rocket jump leaves a pool of fire where you took off.
    internal static class JetBoots
    {
        private static readonly T4 Cooldown = new T4(8f, 6f, 4f, 2f);
        private static readonly T4 Damage = new T4(5f, 6f, 9f, 14f);
        private static readonly T4 InstantRecharge = new T4(10f, 20f, 50f, 90f);
        private static readonly T4 FireDamage = new T4(1f, 2f, 5f, 10f);   // of your damage

        private static GameObject firePool;

        internal static void Init()
        {
            Hooks.Patch(typeof(SS2.Items.JetBoots.Behavior), "OnJump", typeof(JetBoots), prefix: nameof(BeforeJump), postfix: nameof(AfterJump));
            Hooks.Patch(typeof(SS2.Items.JetBoots), "ProcessJump", typeof(JetBoots), prefix: nameof(BeforeBlast), postfix: nameof(AfterBlast));
            RoR2Application.onLoad += () => firePool = Addressables.LoadAssetAsync<GameObject>("RoR2/DLC1/Molotov/MolotovProjectileDotZone.prefab").WaitForCompletion();
        }

        private static void BeforeJump(SS2.Items.JetBoots.Behavior __instance, out int __state)
        {
            __state = SS2.Items.JetBoots.jumpCooldown;
            var counts = Q.Counts(__instance.body, SS2Content.Items.JetBoots);
            if (Q.Any(counts)) SS2.Items.JetBoots.jumpCooldown = Mathf.CeilToInt(Q.AtHighest(counts, Cooldown));
        }

        // Runs on the jumping player's own game.
        private static void AfterJump(SS2.Items.JetBoots.Behavior __instance, ref float ___cooldownTimer, int __state)
        {
            SS2.Items.JetBoots.jumpCooldown = __state;
            var body = __instance.body;
            var counts = Q.Counts(body, SS2Content.Items.JetBoots);
            if (!Q.Any(counts) || !body.hasEffectiveAuthority) return;
            ___cooldownTimer = Util.CheckRoll(Q.Stack(counts, InstantRecharge), body.master) ? 0f : Q.AtHighest(counts, Cooldown);

            if (!firePool) return;
            ProjectileManager.instance.FireProjectile(new FireProjectileInfo
            {
                projectilePrefab = firePool,
                position = body.footPosition,
                rotation = Quaternion.identity,
                owner = body.gameObject,
                damage = body.damage * Q.Stack(counts, FireDamage),
                crit = body.RollCrit(),
                damageColorIndex = DamageColorIndex.Item
            });
        }

        private static void BeforeBlast(GenericCharacterMain self, out float __state)
        {
            __state = SS2.Items.JetBoots.baseDamage;
            var counts = Q.Counts(self != null && self.outer ? self.outer.GetComponent<CharacterBody>() : null, SS2Content.Items.JetBoots);
            if (Q.Any(counts)) SS2.Items.JetBoots.baseDamage = Mathf.Max(__state, Q.AtHighest(counts, Damage));
        }

        private static void AfterBlast(float __state)
        {
            SS2.Items.JetBoots.baseDamage = __state;
        }
    }
}
