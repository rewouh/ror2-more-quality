using System;
using System.IO;
using System.Runtime.CompilerServices;
using RiskOfOptions;
using RiskOfOptions.OptionConfigs;
using RiskOfOptions.Options;
using UnityEngine;

namespace MoreQuality
{
    // Risk of Options is optional. Everything touching its types lives here, and is only called when it's
    // installed, so MoreQuality still loads without it.
    internal static class RiskOfOptionsCompat
    {
        public const string GUID = "com.rune580.riskofoptions";

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Register(string pluginLocation)
        {
            try
            {
                ModSettingsManager.SetModDescription("Quality variants with their own effects for Starstorm 2 items and equipment, and Hallowed Concepts' Hallowed items and Hexes.");
                var icon = LoadIcon(pluginLocation);
                if (icon) ModSettingsManager.SetModIcon(icon);

                ModSettingsManager.AddOption(new SliderOption(MoreQualityPlugin.QualityHandChance, new SliderConfig { min = 0f, max = 100f, formatString = "{0:0}%" }));
            }
            catch (Exception e)
            {
                MoreQualityPlugin.Log.LogWarning("Risk of Options setup failed: " + e.Message);
            }
        }

        // The package's icon.png sits next to the DLL once installed by a mod manager.
        private static Sprite LoadIcon(string pluginLocation)
        {
            var folder = Path.GetDirectoryName(pluginLocation);
            foreach (var path in new[] { Path.Combine(folder, "icon.png"), Path.Combine(Path.GetDirectoryName(folder) ?? folder, "icon.png") })
            {
                if (!File.Exists(path)) continue;
                var texture = new Texture2D(2, 2);
                if (!texture.LoadImage(File.ReadAllBytes(path))) continue;
                return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            }
            return null;
        }
    }
}
