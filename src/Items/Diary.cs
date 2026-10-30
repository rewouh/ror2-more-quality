using System;
using System.Reflection;
using HarmonyLib;
using ItemQualities;
using RoR2;
using SS2;
using UnityEngine;

namespace MoreQuality.Items
{
    // Consuming a quality Diary also gives a random item (rarity rolled like a small chest) of the Diary's quality.
    //
    // Starstorm2 consumes the diary with RemoveItem(Diary): that only removes the BASE item, so a quality
    // diary would never be used up and would level you every stage. This handles quality diaries itself.
    internal static class Diary
    {
        private static MethodInfo addLevel;

        internal static void Init()
        {
            addLevel = AccessTools.Method(typeof(SS2.Items.Diary.Behavior), "AddLevel");
            Hooks.Patch(typeof(SS2.Items.Diary.Behavior), "AddLevel", typeof(Diary), prefix: nameof(AddLevel));
        }

        private static bool AddLevel(SS2.Items.Diary.Behavior __instance, CharacterBody body)
        {
            var inventory = body ? body.inventory : null;
            var group = QualityContent.Group(SS2Content.Items.Diary);
            if (!inventory || group == null) return true;
            // Base diaries first: let Starstorm2 consume one of those as usual.
            if (inventory.GetItemCountPermanent(SS2Content.Items.Diary.itemIndex) > 0) return true;

            var counts = group.GetItemCountsEffective(inventory);
            var tier = Q.Highest(counts);
            if (tier == QualityTier.None) return true;
            var qualityDiary = group.GetItemIndex(tier);

            var team = body.teamComponent.teamIndex;
            var experience = TeamManager.GetExperienceForLevel(TeamManager.instance.GetTeamLevel(team) + (uint)SS2.Items.Diary.extraLevels)
                - TeamManager.instance.GetTeamCurrentLevelExperience(team);
            ExperienceManager.instance.AwardExperience(body.transform.position, body, experience);

            inventory.RemoveItemPermanent(qualityDiary, 1);
            inventory.GiveItemPermanent(SS2Content.Items.DiaryConsumed.itemIndex, 1);
            CharacterMasterNotificationQueue.SendTransformNotification(body.master, qualityDiary, SS2Content.Items.DiaryConsumed.itemIndex, CharacterMasterNotificationQueue.TransformationType.Default);
            SS2.Items.Diary.Behavior.PlayDiarySFXLocal(body.gameObject);

            GiveRandomItem(body.master, tier);

            body.master.onBodyStart -= (Action<CharacterBody>)Delegate.CreateDelegate(typeof(Action<CharacterBody>), __instance, addLevel);
            return false;
        }

        private static void GiveRandomItem(CharacterMaster master, QualityTier tier)
        {
            var run = Run.instance;
            var roll = run.treasureRng.nextNormalizedFloat;
            var list = roll < 0.01f ? run.availableTier3DropList : roll < 0.208f ? run.availableTier2DropList : run.availableTier1DropList;
            if (list == null || list.Count == 0) return;
            var pickup = QualityCatalog.GetPickupIndexOfQuality(list[run.treasureRng.RangeInt(0, list.Count)], tier);
            var item = PickupCatalog.GetPickupDef(pickup);
            if (item == null || item.itemIndex == ItemIndex.None) return;
            master.inventory.GiveItemPermanent(item.itemIndex, 1);
            CharacterMasterNotificationQueue.PushPickupNotification(master, pickup);
        }
    }
}
