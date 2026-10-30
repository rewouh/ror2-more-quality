using System.Runtime.CompilerServices;
using EntityStates;
using ItemQualities;
using RoR2;
using SS2;
using UnityEngine;

namespace MoreQuality.Items
{
    // Faster recharge and a bigger blast; Legendary gets a second rocket jump before the recharge.
    internal static class JetBoots
    {
        private static readonly T4 Cooldown = new T4(8f, 6.5f, 5f, 3f);
        private static readonly T4 Damage = new T4(5f, 6.5f, 8.5f, 12f);

        private sealed class Charges { public bool Extra = true; public bool WaitingForRecharge; }
        private static readonly ConditionalWeakTable<SS2.Items.JetBoots.Behavior, Charges> charges = new ConditionalWeakTable<SS2.Items.JetBoots.Behavior, Charges>();

        internal static void Init()
        {
            Hooks.Patch(typeof(SS2.Items.JetBoots.Behavior), "OnJump", typeof(JetBoots), prefix: nameof(BeforeJump), postfix: nameof(AfterJump));
            Hooks.Patch(typeof(SS2.Items.JetBoots.Behavior), "FixedUpdate", typeof(JetBoots), postfix: nameof(AfterFixedUpdate));
            Hooks.Patch(typeof(SS2.Items.JetBoots), "ProcessJump", typeof(JetBoots), prefix: nameof(BeforeBlast), postfix: nameof(AfterBlast));
        }

        private static void BeforeJump(SS2.Items.JetBoots.Behavior __instance, out int __state)
        {
            __state = SS2.Items.JetBoots.jumpCooldown;
            var counts = Q.Counts(__instance.body, SS2Content.Items.JetBoots);
            if (Q.Any(counts)) SS2.Items.JetBoots.jumpCooldown = Mathf.CeilToInt(Q.AtHighest(counts, Cooldown));
        }

        private static void AfterJump(SS2.Items.JetBoots.Behavior __instance, ref float ___cooldownTimer, int __state)
        {
            SS2.Items.JetBoots.jumpCooldown = __state;
            var body = __instance.body;
            var counts = Q.Counts(body, SS2Content.Items.JetBoots);
            if (!Q.Any(counts) || !body.hasEffectiveAuthority) return;
            ___cooldownTimer = Q.AtHighest(counts, Cooldown);

            if (Q.Highest(counts) != QualityTier.Legendary) return;
            var state = charges.GetOrCreateValue(__instance);
            if (state.Extra)
            {
                state.Extra = false;
                ___cooldownTimer = 0f;   // the second jump is ready right away
            }
            else
            {
                state.WaitingForRecharge = true;
            }
        }

        private static void AfterFixedUpdate(SS2.Items.JetBoots.Behavior __instance, float ___cooldownTimer)
        {
            Charges state;
            if (!charges.TryGetValue(__instance, out state) || !state.WaitingForRecharge || ___cooldownTimer > 0f) return;
            state.WaitingForRecharge = false;
            state.Extra = true;
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
