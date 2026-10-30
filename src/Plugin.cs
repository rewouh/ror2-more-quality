using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using RoR2;

namespace MoreQuality
{
    [BepInPlugin(GUID, Name, Version)]
    [BepInDependency("com.Gorakh.ItemQualities")]
    [BepInDependency("com.TeamMoonstorm.Starstorm2")]
    [BepInDependency(R2API.RecalculateStatsAPI.PluginGUID)]
    [BepInDependency(R2API.PrefabAPI.PluginGUID)]
    [BepInDependency(R2API.ContentManagement.R2APIContentManager.PluginGUID)]
    public sealed class MoreQualityPlugin : BaseUnityPlugin
    {
        public const string GUID = "com.rewouh.MoreQuality";
        public const string Name = "MoreQuality";
        public const string Version = "0.2.0";

        internal static ManualLogSource Log;
        internal static Harmony Harmony;

        private void Awake()
        {
            Log = Logger;
            Harmony = new Harmony(GUID);

            var langFolder = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Info.Location), "lang");
            if (Directory.Exists(langFolder)) Language.collectLanguageRootFolders += folders => folders.Add(langFolder);
            else Log.LogError("Language folder missing: " + langFolder);

            Starstorm.HoldBackQuality();
            QualityContent.Register();
            ContentProvider.Register();
            CustomBuffs.Register();
            BaseText.Register();

            // Every quality effect lives in its own static class with an Init(): Starstorm2 items in
            // MoreQuality.Items, Hallowed Concepts ones in MoreQuality.Hallowed. One failing to hook
            // (for example after a game or Starstorm2 update) must not take the others down with it.
            var modules = typeof(MoreQualityPlugin).Assembly.GetTypes()
                .Where(t => (t.Namespace == "MoreQuality.Items" || t.Namespace == "MoreQuality.Hallowed") && t.IsAbstract && t.IsSealed)
                .Select(t => t.GetMethod("Init", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
                .Where(m => m != null);
            foreach (var init in modules)
            {
                try { init.Invoke(null, null); }
                catch (Exception e) { Log.LogError("Failed to set up " + init.DeclaringType.Name + ": " + (e.InnerException ?? e)); }
            }
        }
    }
}
