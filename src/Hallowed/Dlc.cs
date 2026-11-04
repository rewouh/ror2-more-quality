using ItemQualities;
using RoR2;

namespace MoreQuality
{
    // Helpers shared by the Hallowed Concepts modules.
    internal static class Dlc
    {
        // The best quality of an item held by any player: for team-wide effects (Corpse Lily, Hex of Rain...).
        public static QualityTier TeamHighest(ItemDef item)
        {
            var best = QualityTier.None;
            foreach (var player in PlayerCharacterMasterController.instances)
            {
                if (!player || !player.master) continue;
                var tier = Q.Highest(Q.Counts(player.master, item));
                if (tier > best) best = tier;
            }
            return best;
        }

        // The counts of the player holding the best quality of an item (team-wide effects that stack).
        public static ItemQualityCounts TeamBest(ItemDef item)
        {
            var best = ItemQualityCounts.zero;
            foreach (var player in PlayerCharacterMasterController.instances)
            {
                if (!player || !player.master) continue;
                var counts = Q.Counts(player.master, item);
                if (Q.Highest(counts) > Q.Highest(best)) best = counts;
            }
            return best;
        }

        // The player holding the best quality of an item (for crediting damage), or null.
        public static CharacterBody TeamHolder(ItemDef item, QualityTier tier)
        {
            foreach (var player in PlayerCharacterMasterController.instances)
            {
                if (player && player.master && Q.Highest(Q.Counts(player.master, item)) == tier) return player.master.GetBody();
            }
            return null;
        }

        public static ItemDef Variant(ItemDef baseItem, QualityTier tier)
        {
            var group = QualityContent.Group(baseItem);
            return group != null ? ItemCatalog.GetItemDef(group.GetItemIndex(tier)) : null;
        }

        public static int Permanent(Inventory inventory, ItemDef baseItem, QualityTier tier)
        {
            var variant = Variant(baseItem, tier);
            return inventory && variant ? inventory.GetItemCountPermanent(variant) : 0;
        }

        // Turns `count` permanent copies of one quality item into the same quality of another
        // (Quality only does this itself for the items it knows about).
        public static int Convert(Inventory inventory, ItemDef from, ItemDef to, QualityTier tier, int count)
        {
            var source = Variant(from, tier);
            var target = Variant(to, tier);
            if (!inventory || !source || !target) return 0;
            count = System.Math.Min(count, inventory.GetItemCountPermanent(source));
            if (count <= 0) return 0;
            inventory.RemoveItemPermanent(source, count);
            inventory.GiveItemPermanent(target, count);
            return count;
        }

        // The lowest quality tier the inventory holds permanently, or None.
        public static QualityTier LowestPermanent(Inventory inventory, ItemDef baseItem)
        {
            for (var tier = QualityTier.Uncommon; tier < QualityTier.Count; tier++)
            {
                if (Permanent(inventory, baseItem, tier) > 0) return tier;
            }
            return QualityTier.None;
        }

        public static void Execute(HealthComponent victim, CharacterBody killer)
        {
            if (!victim || !victim.alive || (victim.body && victim.body.isBoss)) return;
            victim.Suicide(killer ? killer.gameObject : null, killer ? killer.gameObject : null, default(DamageTypeCombo));
        }
    }
}
