using System.Collections;
using System.Collections.Generic;
using ItemQualities;
using ItemQualities.ContentManagement;
using RoR2;
using SS2;
using UnityEngine;

namespace MoreQuality
{
    // Hands Starstorm2's items to Quality through its public content API. Quality then generates
    // the four variants itself (item defs, recoloured icons, "Uncommon X" names), exactly like its
    // own vanilla groups, and counts every variant as a copy of the base item.
    internal static class QualityContent
    {
        private static readonly Dictionary<ItemDef, ItemQualityGroup> itemGroups = new Dictionary<ItemDef, ItemQualityGroup>();
        private static readonly Dictionary<EquipmentDef, EquipmentQualityGroup> equipmentGroups = new Dictionary<EquipmentDef, EquipmentQualityGroup>();

        public static void Register()
        {
            QualityContentManager.LoadContentAsync += LoadContentAsync;
        }

        public static ItemQualityGroup Group(ItemDef item)
        {
            ItemQualityGroup group;
            return item != null && itemGroups.TryGetValue(item, out group) ? group : null;
        }

        public static EquipmentQualityGroup Group(EquipmentDef equipment)
        {
            EquipmentQualityGroup group;
            return equipment != null && equipmentGroups.TryGetValue(equipment, out group) ? group : null;
        }

        // Runs inside Quality's loader, which Starstorm.HoldBackQuality only lets start once Starstorm2
        // is fully loaded. It must never wait here: see Starstorm.cs.
        private static IEnumerator LoadContentAsync(QualityContentLoadArgs args)
        {
            if (!Starstorm.Ready)
            {
                MoreQualityPlugin.Log.LogError("Starstorm2 wasn't loaded when Quality asked for content; no quality variants will be added.");
                yield break;
            }

            foreach (var item in Items)
            {
                if (item == null) continue;   // disabled in Starstorm2's config, or beta-only
                itemGroups[item] = args.CreateItemQualityGroup(item);
            }
            foreach (var equipment in Equipments)
            {
                if (equipment == null) continue;
                equipmentGroups[equipment] = args.CreateEquipmentQualityGroup(equipment);
            }
            MoreQualityPlugin.Log.LogInfo("Registered " + itemGroups.Count + " Starstorm2 items and " + equipmentGroups.Count + " equipments with Quality.");
            args.ProgressReceiver.Report(1f);
        }

        // The 0.7 beta items (Ice Tool, Neurotoxin Gland, Sticky Overloader, Toy Soldiers) are left out:
        // they only exist when Starstorm2's beta is enabled.
        private static IEnumerable<ItemDef> Items
        {
            get
            {
                yield return SS2Content.Items.ArmedBackpack;
                yield return SS2Content.Items.BloodTester;
                yield return SS2Content.Items.CoffeeBag;
                yield return SS2Content.Items.DetritiveTrematode;
                yield return SS2Content.Items.Diary;
                yield return SS2Content.Items.Fork;
                yield return SS2Content.Items.GuardingAmulet;
                yield return SS2Content.Items.Malice;
                yield return SS2Content.Items.MoltenCoin;
                yield return SS2Content.Items.Needles;
                yield return SS2Content.Items.UniversalCharger;
                yield return SS2Content.Items.UraniumHorseshoe;
                yield return SS2Content.Items.X4;
                yield return SS2Content.Items.CrypticSource;
                yield return SS2Content.Items.FieldAccelerator;
                yield return SS2Content.Items.HuntersSigil;
                yield return SS2Content.Items.JetBoots;
                yield return SS2Content.Items.LightningOnKill;
                yield return SS2Content.Items.LowQualitySpeakers;
                yield return SS2Content.Items.StrangeCan;
                yield return SS2Content.Items.WatchMetronome;
                yield return SS2Content.Items.DroidHead;
                yield return SS2Content.Items.ErraticGadget;
                yield return SS2Content.Items.GalvanicCore;
                yield return SS2Content.Items.GreenChocolate;
                yield return SS2Content.Items.Insecticide;
                yield return SS2Content.Items.NkotasHeritage;
                yield return SS2Content.Items.PortableReactor;
                yield return SS2Content.Items.SwiftSkateboard;
                yield return SS2Content.Items.ShackledLamp;
            }
        }

        private static IEnumerable<EquipmentDef> Equipments
        {
            get
            {
                yield return SS2Content.Equipments.BackThruster;
                yield return SS2Content.Equipments.CloakingHeadband;
                yield return SS2Content.Equipments.GreaterWarbanner;
                yield return SS2Content.Equipments.Magnet;
                yield return SS2Content.Equipments.MIDAS;
                yield return SS2Content.Equipments.WhiteFlag;
            }
        }
    }
}
