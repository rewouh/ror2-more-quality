using System;
using System.Collections.Generic;
using System.Linq;
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
            Hooks.OnLoad("DpsMeter", () => { loaded = true; foreach (var entry in owners.ToList()) TellDpsMeter(entry.Key, entry.Value); });
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

        // The item whose quality effect dealt this damage, if it went through one of our named inflictors.
        public static ItemIndex ItemOf(GameObject inflictor)
        {
            string owner;
            if (!inflictor || !owners.TryGetValue(inflictor.name.Replace("(Clone)", ""), out owner)) return ItemIndex.None;
            return ItemCatalog.FindItemIndex(owner);
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

        // DpsMeter's table, looked up once. Never use AccessTools.TypeByName here: when the type is missing it
        // scans every type of every loaded assembly, and doing that once per source froze loading at 99% in
        // big modpacks (issue #6). Assembly.GetType only asks each assembly for that one name.
        private static bool searched;
        private static Type map;
        private static System.Reflection.FieldInfo table;

        private static Dictionary<string, string> Table()
        {
            if (!searched)
            {
                searched = true;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try { map = assembly.GetType(DpsMeterMap, false); } catch { map = null; }
                    if (map != null) break;
                }
                table = map != null ? AccessTools.Field(map, "_prefabToOwner") : null;
                if (table == null) map = null;
            }
            if (map == null) return null;
            if (table.GetValue(null) == null) AccessTools.Method(map, "Load")?.Invoke(null, null);
            return table.GetValue(null) as Dictionary<string, string>;
        }

        private static void TellDpsMeter(string name, string owner)
        {
            try
            {
                var entries = Table();
                if (entries != null) entries[name] = owner;
            }
            catch (Exception e)
            {
                MoreQualityPlugin.Log.LogWarning("DpsMeter registration failed for " + name + ": " + e.Message);
            }
        }
    }
}
