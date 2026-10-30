using System.Collections.Generic;
using RoR2;
using UnityEngine;

namespace MoreQuality.Hallowed
{
    // Quality makes item costs (printers, scrappers...) ignore quality items so they are never spent by
    // accident. The Hallowed Shrine pays with hexes through that same item cost, so quality hexes
    // counted as nothing and the Hand of Life stayed shut. The hex cost gets its own check and payment,
    // which take quality hexes too; every other item cost keeps Quality's rule.
    internal static class HexCost
    {
        internal static void Init()
        {
            RoR2Application.onLoad += Replace;
        }

        private static void Replace()
        {
            var hex = CostTypeCatalog.GetCostTypeDef(CostTypeIndex.Hex);
            if (hex == null) return;
            hex.isAffordable = IsAffordable;
            hex.payCost = PayCost;
        }

        private static bool IsAffordable(CostTypeDef costTypeDef, CostTypeDef.IsAffordableContext context)
        {
            var body = context.activator ? context.activator.GetComponent<CharacterBody>() : null;
            var inventory = body ? body.inventory : null;
            return inventory && !inventory.inventoryDisabled && inventory.HasAtLeastXTotalRemovablePermanentItemsOfTier(ItemTier.Hex, context.cost);
        }

        // Same as the game's item payment: random hexes, weighted by how many of each you hold.
        private static void PayCost(CostTypeDef.PayCostContext context, CostTypeDef.PayCostResults result)
        {
            var inventory = context.activatorBody ? context.activatorBody.inventory : null;
            if (!inventory) return;
            var pool = new List<KeyValuePair<ItemIndex, int>>();
            foreach (var index in ItemCatalog.allItems)
            {
                if (index == context.avoidedItemIndex || inventory.IsItemDisabled(index)) continue;
                var count = inventory.GetItemCountPermanent(index);
                var def = ItemCatalog.GetItemDef(index);
                if (count > 0 && def.tier == ItemTier.Hex && def.canRemove && !def.ContainsTag(ItemTag.ObjectiveRelated)) pool.Add(new KeyValuePair<ItemIndex, int>(index, count));
            }

            var taken = new Dictionary<ItemIndex, int>();
            for (var i = 0; i < context.cost && pool.Count > 0; i++)
            {
                var total = 0;
                foreach (var entry in pool) total += entry.Value;
                var roll = context.rng != null ? context.rng.RangeInt(0, total) : Random.Range(0, total);
                for (var j = 0; j < pool.Count; j++)
                {
                    roll -= pool[j].Value;
                    if (roll >= 0) continue;
                    int already;
                    taken.TryGetValue(pool[j].Key, out already);
                    taken[pool[j].Key] = already + 1;
                    if (pool[j].Value <= 1) pool.RemoveAt(j);
                    else pool[j] = new KeyValuePair<ItemIndex, int>(pool[j].Key, pool[j].Value - 1);
                    break;
                }
            }

            foreach (var entry in taken)
            {
                var transformation = new Inventory.ItemTransformation
                {
                    originalItemIndex = entry.Key,
                    newItemIndex = ItemIndex.None,
                    maxToTransform = entry.Value,
                    forbidTempItems = true,
                    forbidRentItems = true,
                    transformationType = ItemTransformationTypeIndex.None
                };
                Inventory.ItemTransformation.TryTransformResult transformed;
                if (transformation.TryTransform(inventory, out transformed)) result.AddTakenItemsFromTransformation(in transformed);
            }
        }
    }
}
