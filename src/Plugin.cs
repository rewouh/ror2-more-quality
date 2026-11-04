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
    [BepInDependency(R2API.LanguageAPI.PluginGUID)]
    [BepInDependency(R2API.Networking.NetworkingAPI.PluginGUID)]
    [BepInDependency(RiskOfOptionsCompat.GUID, BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class MoreQualityPlugin : BaseUnityPlugin
    {
        public const string GUID = "com.rewouh.MoreQuality";
        public const string Name = "MoreQuality";
        public const string Version = "0.4.1";

        internal static ManualLogSource Log;
        internal static Harmony Harmony;
        internal static BepInEx.Configuration.ConfigEntry<float> QualityHandChance;

        private void Awake()
        {
            Log = Logger;
            Harmony = new Harmony(GUID);
            QualityHandChance = Config.Bind("Hallowed Concepts", "Quality Hand of Death chance", 10f,
                "% chance for a Hallowed Shrine's Hand of Death to be a white, quality one: every hex it drops gets a random quality.");

            var langFolder = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Info.Location), "lang");
            if (Directory.Exists(langFolder)) Language.collectLanguageRootFolders += folders => folders.Add(langFolder);
            else Log.LogError("Language folder missing: " + langFolder);

            if (BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(RiskOfOptionsCompat.GUID)) RiskOfOptionsCompat.Register(Info.Location);

            Starstorm.HoldBackQuality();
            QualityContent.Register();
            ContentProvider.Register();
            CustomBuffs.Register();
            BaseText.Register();
            Fx.Register();
            Sources.Register();
            HexFx.Register();
            Decor.Register();

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
