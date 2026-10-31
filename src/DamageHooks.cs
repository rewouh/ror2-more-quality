using System;
using System.Collections.Generic;
using RoR2;

namespace MoreQuality
{
    // One prefix on HealthComponent.TakeDamage that item modules can plug damage changes into,
    // instead of each patching the hottest method in the game separately.
    internal static class DamageHooks
    {
        public delegate void Modifier(HealthComponent victim, DamageInfo damageInfo, CharacterBody attacker);

        private static readonly List<Modifier> modifiers = new List<Modifier>();
        private static readonly List<Modifier> incoming = new List<Modifier>();
        private static bool patched;

        // Only runs for damage dealt by a character (attacker is never null).
        public static void Add(Modifier modifier)
        {
            modifiers.Add(modifier);
            Patch();
        }

        // Runs for every hit, including fall damage and damage without an attacker (attacker may be null).
        public static void AddIncoming(Modifier modifier)
        {
            incoming.Add(modifier);
            Patch();
        }

        private static void Patch()
        {
            if (patched) return;
            patched = true;
            Hooks.Patch(typeof(HealthComponent), nameof(HealthComponent.TakeDamage), typeof(DamageHooks), prefix: nameof(TakeDamage), args: new[] { typeof(DamageInfo) });
        }

        private static void TakeDamage(HealthComponent __instance, DamageInfo damageInfo)
        {
            if (damageInfo == null || damageInfo.rejected) return;
            var attacker = damageInfo.attacker ? damageInfo.attacker.GetComponent<CharacterBody>() : null;
            foreach (var modifier in incoming)
            {
                try { modifier(__instance, damageInfo, attacker); }
                catch (Exception e) { MoreQualityPlugin.Log.LogError(e); }
            }
            if (!attacker) return;
            foreach (var modifier in modifiers)
            {
                try { modifier(__instance, damageInfo, attacker); }
                catch (Exception e) { MoreQualityPlugin.Log.LogError(e); }
            }
        }
    }
}
