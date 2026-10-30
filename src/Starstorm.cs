using System;
using System.Collections;
using System.Diagnostics;
using HarmonyLib;
using RoR2.ContentManagement;
using SS2;

namespace MoreQuality
{
    // Starstorm2 has to finish loading before its items can be handed to Quality.
    //
    // RoR2 steps every mod's content loader in parallel, but whenever a loader yields a nested
    // coroutine, RoR2 runs that nested coroutine on its own until it finishes. Quality's loader does
    // exactly that, so anything inside it that waits for Starstorm2 freezes the whole loading screen.
    // Instead, Quality's loader is held back (yielding nothing but null, which keeps everyone else
    // running) until Starstorm2 is done, and only then allowed to start.
    internal static class Starstorm
    {
        private static Func<bool> finished;

        public static bool Ready
        {
            get
            {
                if (finished == null)
                {
                    var getter = AccessTools.PropertyGetter(typeof(SS2Content), "loadStaticContentFinished");
                    finished = getter != null
                        ? (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), getter)
                        : () => SS2Content.Items.Fork != null || SS2Content.Items.Malice != null;
                }
                return finished();
            }
        }

        // Waits cooperatively: only ever yields null.
        public static IEnumerator WaitUntilReady(string who)
        {
            var timer = Stopwatch.StartNew();
            while (!Ready)
            {
                if (timer.Elapsed.TotalSeconds > 180)
                {
                    MoreQualityPlugin.Log.LogError(who + ": Starstorm2 never finished loading, continuing without it.");
                    yield break;
                }
                yield return null;
            }
        }

        public static void HoldBackQuality()
        {
            Hooks.Patch(typeof(ItemQualities.ItemQualitiesContent), nameof(ItemQualities.ItemQualitiesContent.LoadStaticContentAsync), typeof(Starstorm), postfix: nameof(DeferQuality));
        }

        private static void DeferQuality(ref IEnumerator __result)
        {
            __result = AfterStarstorm(__result);
        }

        private static IEnumerator AfterStarstorm(IEnumerator qualityLoad)
        {
            var wait = WaitUntilReady("Quality");
            while (wait.MoveNext()) yield return null;
            // The Hallowed Concepts items come from the game's own DLC content pack.
            var timer = Stopwatch.StartNew();
            while (RoR2.DLC4Content.Items.HexCurse == null && timer.Elapsed.TotalSeconds < 180) yield return null;
            if (RoR2.DLC4Content.Items.HexCurse == null) MoreQualityPlugin.Log.LogError("Hallowed Concepts content never loaded; its items won't get quality.");
            MoreQualityPlugin.Log.LogInfo("Starstorm2 finished loading; starting Quality's content load.");
            while (qualityLoad.MoveNext()) yield return qualityLoad.Current;
        }
    }
}
