using System.Runtime.CompilerServices;
using ItemQualities;
using RoR2;
using UnityEngine;

namespace MoreQuality
{
    // Remembers which quality an equipment was used at, for effects that outlast the activation
    // (Back Thruster's ramming, Cloaking Headband's first strike).
    internal static class EquipmentMemory
    {
        public sealed class Use
        {
            public EquipmentDef equipment;
            public QualityTier tier;
            public float time;
            public bool spent;
            public float firstHit = -1f;
        }

        private static readonly ConditionalWeakTable<CharacterBody, Use> uses = new ConditionalWeakTable<CharacterBody, Use>();

        public static void Remember(CharacterBody body, EquipmentDef equipment, QualityTier tier)
        {
            if (!body) return;
            uses.Remove(body);
            uses.Add(body, new Use { equipment = equipment, tier = tier, time = Time.fixedTime });
        }

        // The last use of this equipment, if it happened within `window` seconds.
        public static Use Recent(CharacterBody body, EquipmentDef equipment, float window)
        {
            Use use;
            if (!body || !uses.TryGetValue(body, out use) || use.equipment != equipment || Time.fixedTime - use.time > window) return null;
            return use;
        }
    }
}
