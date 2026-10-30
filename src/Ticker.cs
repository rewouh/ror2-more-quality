using System;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace MoreQuality
{
    // Runs a callback on the server at a fixed interval during a run.
    internal static class Ticker
    {
        public static void Every(float seconds, Action action)
        {
            var timer = 0f;
            RoR2Application.onFixedUpdate += () =>
            {
                if (!NetworkServer.active || !Run.instance) return;
                timer -= Time.fixedDeltaTime;
                if (timer > 0f) return;
                timer = seconds;
                try { action(); }
                catch (Exception e) { MoreQualityPlugin.Log.LogError(e); }
            };
        }
    }
}
