using System;
using System.Collections.Generic;
using HarmonyLib;
using RoR2;
using UnityEngine;

namespace MoreQuality
{
    // Gives each quality effect's damage a recognisable source. Damage normally names its "inflictor" (the
    // object that dealt it); ours used the player, or nothing, so damage meters couldn't tell what it was.
    // Each item now gets an invisible, persistent object named "MoreQuality_<item>" to use as inflictor.
    //
    // DpsMeter (optional) resolves inflictors through a name -> item table it loads from its own resources;
    // when it's installed, our names (and our cloned projectile prefabs) are added to that table, so its
    // breakdown shows the quality item instead of "Unattributed".
    internal static class Sources
    {
        private const string DpsMeterMap = "DpsMeter.Tracking.InflictorMap";

        private static readonly Dictionary<string, GameObject> inflictors = new Dictionary<string, GameObject>();
        private static readonly Dictionary<string, string> owners = new Dictionary<string, string>();   // inflictor or prefab name -> item/equipment name
        private static bool loaded;

        public static void Register()
        {
            RoR2Application.onLoad += () => { loaded = true; foreach (var entry in owners) TellDpsMeter(entry.Key, entry.Value); };
        }

        public static GameObject Of(ItemDef item)
        {
            return item ? Get("MoreQuality_" + item.name, item.name) : null;
        }

        public static GameObject Of(EquipmentDef equipment)
        {
            return equipment ? Get("MoreQuality_" + equipment.name, equipment.name) : null;
        }

        // For objects that already have their own name: a cloned projectile prefab, a renamed trail...
        public static void Name(string objectName, ItemDef item)
        {
            if (item) Remember(objectName, item.name);
        }

        public static string NameFor(ItemDef item)
        {
            if (!item) return "MoreQuality";
            Remember("MoreQuality_" + item.name, item.name);
            return "MoreQuality_" + item.name;
        }

        private static GameObject Get(string name, string owner)
        {
            GameObject inflictor;
            if (inflictors.TryGetValue(name, out inflictor) && inflictor) return inflictor;
            inflictor = new GameObject(name);
            UnityEngine.Object.DontDestroyOnLoad(inflictor);
            inflictors[name] = inflictor;
            Remember(name, owner);
            return inflictor;
        }

        private static void Remember(string name, string owner)
        {
            if (owners.ContainsKey(name)) return;
            owners[name] = owner;
            if (loaded) TellDpsMeter(name, owner);
        }

        private static void TellDpsMeter(string name, string owner)
        {
            try
            {
                var map = AccessTools.TypeByName(DpsMeterMap);
                if (map == null) return;
                var table = AccessTools.Field(map, "_prefabToOwner");
                if (table == null) return;
                if (table.GetValue(null) == null) AccessTools.Method(map, "Load")?.Invoke(null, null);
                var entries = table.GetValue(null) as Dictionary<string, string>;
                if (entries != null) entries[name] = owner;
            }
            catch (Exception e)
            {
                MoreQualityPlugin.Log.LogWarning("DpsMeter registration failed for " + name + ": " + e.Message);
            }
        }
    }
}
