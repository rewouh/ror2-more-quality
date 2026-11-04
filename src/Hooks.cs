using System;
using HarmonyLib;
using RoR2;

namespace MoreQuality
{
    // Thin wrapper over Harmony: patch a method by name and fail loudly if it was renamed.
    internal static class Hooks
    {
        public static void Transpile(Type target, string method, Type owner, string transpiler)
        {
            var original = AccessTools.Method(target, method);
            if (original == null) throw new MissingMethodException(target.FullName, method);
            MoreQualityPlugin.Harmony.Patch(original, transpiler: new HarmonyMethod(AccessTools.Method(owner, transpiler)));
        }

        // RoR2Application.onLoad runs while the loading screen sits at 99%: an exception there stops the
        // game from ever finishing loading. Everything we run at that moment goes through here.
        public static void OnLoad(string what, Action action)
        {
            RoR2Application.onLoad += () =>
            {
                try { action(); }
                catch (Exception e) { MoreQualityPlugin.Log.LogError("Load step failed (" + what + "): " + e); }
            };
        }

        public static void Patch(Type target, string method, Type owner, string prefix = null, string postfix = null, Type[] args = null)
        {
            var original = args == null ? AccessTools.Method(target, method) : AccessTools.Method(target, method, args);
            if (original == null) throw new MissingMethodException(target.FullName, method);
            MoreQualityPlugin.Harmony.Patch(original,
                prefix: prefix != null ? new HarmonyMethod(AccessTools.Method(owner, prefix)) : null,
                postfix: postfix != null ? new HarmonyMethod(AccessTools.Method(owner, postfix)) : null);
        }
    }
}
