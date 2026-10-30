using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using EntityStates.HallowedCauldron;
using HarmonyLib;
using ItemQualities;
using RoR2;
using UnityEngine;

namespace MoreQuality.Hallowed
{
    // The payoff for carrying quality hexes: every quality hex drained by a Hallowed Shrine's Hand of
    // Life guarantees one choice of its quality in the Hallowed Potential. The best tiers go first,
    // spread over the groups (you take one item per group) starting with the best group.
    internal static class Cleansing
    {
        private static readonly ConditionalWeakTable<DrainingItems, List<QualityTier>> drained = new ConditionalWeakTable<DrainingItems, List<QualityTier>>();
        private static readonly AccessTools.FieldRef<DrainingItems, Inventory> inventoryOf = AccessTools.FieldRefAccess<DrainingItems, Inventory>("targetInventory");
        private static List<QualityTier> creating;

        internal static void Init()
        {
            Hooks.Patch(typeof(DrainingItems), "TakeItemServer", typeof(Cleansing), prefix: nameof(BeforeTake), postfix: nameof(AfterTake));
            Hooks.Patch(typeof(DrainingItems), "CreatePotentialServer", typeof(Cleansing), prefix: nameof(BeforeCreate), postfix: nameof(AfterCreate));
            Hooks.Patch(typeof(PickupDropletController), nameof(PickupDropletController.CreatePickupDroplet), typeof(Cleansing), prefix: nameof(OnDroplet),
                args: new[] { typeof(GenericPickupController.CreatePickupInfo), typeof(Vector3), typeof(Vector3) });
        }

        private static IEnumerable<KeyValuePair<ItemIndex, QualityTier>> QualityHexes(Inventory inventory)
        {
            if (!inventory) yield break;
            foreach (var index in inventory.itemAcquisitionOrder)
            {
                var def = ItemCatalog.GetItemDef(index);
                var tier = QualityCatalog.GetQualityTier(index);
                if (def && def.tier == ItemTier.Hex && tier != QualityTier.None) yield return new KeyValuePair<ItemIndex, QualityTier>(index, tier);
            }
        }

        private static void BeforeTake(DrainingItems __instance, out Dictionary<ItemIndex, int> __state)
        {
            var inventory = inventoryOf(__instance);
            __state = QualityHexes(inventory).ToDictionary(e => e.Key, e => inventory.GetItemCountPermanent(e.Key));
        }

        private static void AfterTake(DrainingItems __instance, Dictionary<ItemIndex, int> __state)
        {
            var inventory = inventoryOf(__instance);
            if (!inventory || __state.Count == 0) return;
            var list = drained.GetValue(__instance, _ => new List<QualityTier>());
            foreach (var entry in __state)
            {
                var taken = entry.Value - inventory.GetItemCountPermanent(entry.Key);
                for (var i = 0; i < taken; i++) list.Add(QualityCatalog.GetQualityTier(entry.Key));
            }
        }

        private static void BeforeCreate(DrainingItems __instance)
        {
            List<QualityTier> list;
            creating = drained.TryGetValue(__instance, out list) && list.Count > 0 ? list : null;
        }

        private static void AfterCreate(DrainingItems __instance)
        {
            creating = null;
            drained.Remove(__instance);
        }

        private static void OnDroplet(GenericPickupController.CreatePickupInfo pickupInfo)
        {
            var options = pickupInfo.pickerOptions;
            if (creating == null || options == null || options.Length == 0) return;
            var tiers = creating.OrderByDescending(t => t).ToList();
            creating = null;

            var groups = (options.Length + 2) / 3;
            var upgraded = new bool[options.Length];
            while (tiers.Count > 0)
            {
                var placedThisRound = false;
                for (var group = groups - 1; group >= 0 && tiers.Count > 0; group--)
                {
                    var tier = tiers[0];
                    var candidates = Enumerable.Range(group * 3, 3)
                        .Where(i => i < options.Length && options[i].available && !upgraded[i] && CanUpgrade(options[i].pickup.pickupIndex, tier))
                        .ToList();
                    if (candidates.Count == 0) continue;
                    var pick = candidates[Random.Range(0, candidates.Count)];
                    var pickup = QualityCatalog.GetPickupIndexOfQuality(options[pick].pickup.pickupIndex, tier);
                    options[pick].pickup = options[pick].pickup.WithPickupIndex(pickup);
                    upgraded[pick] = true;
                    tiers.RemoveAt(0);
                    placedThisRound = true;
                }
                if (!placedThisRound) break;
            }
        }

        private static bool CanUpgrade(PickupIndex pickup, QualityTier tier)
        {
            if (QualityCatalog.GetQualityTier(pickup) >= tier) return false;
            var upgraded = QualityCatalog.GetPickupIndexOfQuality(pickup, tier);
            return upgraded.isValid && upgraded != pickup;
        }
    }
}
