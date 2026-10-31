using ItemQualities;
using ItemQualities.Utilities.Extensions;
using RoR2;

namespace MoreQuality
{
    // One value per quality tier, written in the design doc's order: Uncommon, Rare, Epic, Legendary.
    internal struct T4
    {
        public readonly float U, R, E, L;

        public T4(float u, float r, float e, float l)
        {
            U = u; R = r; E = e; L = l;
        }

        public float this[QualityTier tier]
        {
            get
            {
                switch (tier)
                {
                    case QualityTier.Uncommon: return U;
                    case QualityTier.Rare: return R;
                    case QualityTier.Epic: return E;
                    case QualityTier.Legendary: return L;
                    default: return 0f;
                }
            }
        }
    }

    internal static class Q
    {
        public static ItemQualityCounts Counts(Inventory inventory, ItemDef item)
        {
            var group = QualityContent.Group(item);
            if (group == null || !inventory) return ItemQualityCounts.zero;
            return group.GetItemCountsEffective(inventory);
        }

        public static ItemQualityCounts Counts(CharacterBody body, ItemDef item)
        {
            return body ? Counts(body.inventory, item) : ItemQualityCounts.zero;
        }

        public static ItemQualityCounts Counts(CharacterMaster master, ItemDef item)
        {
            return master ? Counts(master.inventory, item) : ItemQualityCounts.zero;
        }

        public static bool Any(ItemQualityCounts counts)
        {
            return counts.TotalQualityCount > 0;
        }

        public static QualityTier Highest(ItemQualityCounts counts)
        {
            return counts.TotalQualityCount > 0 ? counts.HighestQuality : QualityTier.None;
        }

        // "X (+X per stack)": every quality copy adds its own tier's value.
        public static float Sum(ItemQualityCounts counts, T4 values)
        {
            return counts.UncommonCount * values.U + counts.RareCount * values.R + counts.EpicCount * values.E + counts.LegendaryCount * values.L;
        }

        // A value that doesn't stack: only the best quality you hold counts.
        public static float AtHighest(ItemQualityCounts counts, T4 values)
        {
            return values[Highest(counts)];
        }

        // Stacking for quality values: the best copy gives its full value, and every other quality copy adds half
        // of its own tier's value, so a second quality item is never useless.
        public static float Stack(ItemQualityCounts counts, T4 values)
        {
            var best = Highest(counts);
            if (best == QualityTier.None) return 0f;
            return values[best] + 0.5f * (Sum(counts, values) - values[best]);
        }

        // Same, for multipliers (x1.5, x2...): the extra copies stack the part above x1.
        public static float StackMultiplier(ItemQualityCounts counts, T4 multipliers)
        {
            var best = Highest(counts);
            if (best == QualityTier.None) return 1f;
            var others = Sum(counts, multipliers) - multipliers[best] - (counts.TotalQualityCount - 1);
            return multipliers[best] + 0.5f * others;
        }

        // "X (+Y per stack)": the first copy of your best quality gives X, every other copy adds its own Y.
        public static float Stacked(ItemQualityCounts counts, T4 first, T4 perStack)
        {
            var best = Highest(counts);
            if (best == QualityTier.None) return 0f;
            return first[best] + Sum(counts, perStack) - perStack[best];
        }

        // Quality makes Inventory.currentEquipmentIndex report the BASE equipment, so the tier has to come
        // from its own lookups: the one being activated right now, or the one held.
        public static QualityTier EquipmentTier(EquipmentSlot slot)
        {
            return slot ? slot.GetCurrentEquipmentActionQualityTier() : QualityTier.None;
        }

        public static QualityTier EquipmentTier(CharacterBody body)
        {
            return body && body.inventory ? body.inventory.GetActiveEquipmentQualityTier() : QualityTier.None;
        }

        // True if the body holds a quality version of this (base) equipment.
        public static QualityTier EquipmentTier(CharacterBody body, EquipmentDef baseEquipment)
        {
            if (!body || !body.inventory || !baseEquipment || body.inventory.currentEquipmentIndex != baseEquipment.equipmentIndex) return QualityTier.None;
            return body.inventory.GetActiveEquipmentQualityTier();
        }
    }
}
