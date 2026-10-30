using System.Collections.Generic;
using ItemQualities;
using RoR2;

namespace MoreQuality
{
    // Our quality descriptions start with "[[BASE]]", replaced here, at display time, by the Starstorm2
    // item's own text. Doing it at display time (rather than through Quality's one-off "{0}" copy at
    // startup) guarantees Starstorm2 has already filled its numbers in from its config.
    internal static class BaseText
    {
        private const string Marker = "[[BASE]]";
        private static readonly Dictionary<string, string> baseTokens = new Dictionary<string, string>();

        public static void Register()
        {
            RoR2Application.onLoad += MapTokens;
            Hooks.Patch(typeof(Language), nameof(Language.GetLocalizedStringByToken), typeof(BaseText), postfix: nameof(Substitute));
        }

        private static void MapTokens()
        {
            for (var i = 0; i < QualityCatalog.ItemQualityGroupCount; i++)
            {
                var group = QualityCatalog.GetItemQualityGroup((ItemQualityGroupIndex)i);
                var baseItem = ItemCatalog.GetItemDef(group.BaseItemIndex);
                if (!baseItem) continue;
                for (var tier = QualityTier.Uncommon; tier < QualityTier.Count; tier++)
                {
                    var item = ItemCatalog.GetItemDef(group.GetItemIndex(tier));
                    if (!item) continue;
                    Map(item.descriptionToken, baseItem.descriptionToken);
                    Map(item.pickupToken, baseItem.pickupToken);
                }
            }
            for (var i = 0; i < QualityCatalog.EquipmentQualityGroupCount; i++)
            {
                var group = QualityCatalog.GetEquipmentQualityGroup((EquipmentQualityGroupIndex)i);
                var baseEquipment = EquipmentCatalog.GetEquipmentDef(group.BaseEquipmentIndex);
                if (!baseEquipment) continue;
                for (var tier = QualityTier.Uncommon; tier < QualityTier.Count; tier++)
                {
                    var equipment = EquipmentCatalog.GetEquipmentDef(group.GetEquipmentIndex(tier));
                    if (!equipment) continue;
                    Map(equipment.descriptionToken, baseEquipment.descriptionToken);
                    Map(equipment.pickupToken, baseEquipment.pickupToken);
                }
            }
        }

        private static void Map(string token, string baseToken)
        {
            if (!string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(baseToken) && token != baseToken) baseTokens[token] = baseToken;
        }

        private static void Substitute(Language __instance, string token, ref string __result)
        {
            string baseToken;
            if (__result == null || !__result.StartsWith(Marker) || !baseTokens.TryGetValue(token, out baseToken)) return;
            __result = __instance.GetLocalizedStringByToken(baseToken) + __result.Substring(Marker.Length);
        }
    }
}
