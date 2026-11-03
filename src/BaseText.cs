using System.Collections.Generic;
using ItemQualities;
using R2API;
using RoR2;

namespace MoreQuality
{
    // Our quality descriptions start with "[[BASE]]", replaced here, at display time, by the Starstorm2
    // item's own text. Doing it at display time (rather than through Quality's one-off "{0}" copy at
    // startup) guarantees Starstorm2 has already filled its numbers in from its config.
    //
    // Names too: Quality builds each quality name once, at startup, from the base item's name, and registers
    // it with R2API. When Starstorm2's strings aren't loaded yet at that moment, the raw token
    // ("SS2_ITEM_..._NAME") gets baked in. R2API answers those lookups itself, before the game (and our
    // postfix) is reached, so a broken name is replaced with an R2API overlay, which wins over Quality's.
    // This is redone whenever the strings may have changed: after loading, at each run start, on a language change.
    internal static class BaseText
    {
        private const string Marker = "[[BASE]]";
        private static readonly Dictionary<string, string> baseTokens = new Dictionary<string, string>();
        private struct NameSource { public string BaseToken, ModifierToken; }
        private static readonly Dictionary<string, NameSource> names = new Dictionary<string, NameSource>();

        public static void Register()
        {
            RoR2Application.onLoad += () => { MapTokens(); RepairNames(); };
            Run.onRunStartGlobal += _ => RepairNames();
            Language.onCurrentLanguageChanged += RepairNames;
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
                    MapName(item.nameToken, baseItem.nameToken, "QUALITY_" + tier.ToString().ToUpper() + (item.isConsumed ? "_CONSUMED_MODIFIER" : "_MODIFIER"));
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
                    MapName(equipment.nameToken, baseEquipment.nameToken, "QUALITY_" + tier.ToString().ToUpper() + "_MODIFIER");
                }
            }
        }

        private static void Map(string token, string baseToken)
        {
            if (!string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(baseToken) && token != baseToken) baseTokens[token] = baseToken;
        }

        private static void MapName(string token, string baseToken, string modifierToken)
        {
            if (!string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(baseToken) && token != baseToken)
                names[token] = new NameSource { BaseToken = baseToken, ModifierToken = modifierToken };
        }

        private static void RepairNames()
        {
            var language = Language.currentLanguage;
            if (language == null) return;
            var repaired = 0;
            foreach (var entry in names)
            {
                var name = language.GetLocalizedStringByToken(entry.Key);
                if (name == null || !name.Contains(entry.Value.BaseToken)) continue;   // fine already
                var baseName = language.GetLocalizedStringByToken(entry.Value.BaseToken);
                if (baseName == entry.Value.BaseToken) continue;   // base text not loaded yet: try again later
                LanguageAPI.AddOverlay(entry.Key, language.GetLocalizedFormattedStringByToken(entry.Value.ModifierToken, baseName), language.name);
                repaired++;
            }
            if (repaired > 0) MoreQualityPlugin.Log.LogInfo("Repaired " + repaired + " quality item names for " + language.name);
        }

        private static void Substitute(Language __instance, string token, ref string __result)
        {
            string baseToken;
            if (__result == null || !__result.StartsWith(Marker) || !baseTokens.TryGetValue(token, out baseToken)) return;
            __result = __instance.GetLocalizedStringByToken(baseToken) + __result.Substring(Marker.Length);
        }
    }
}
