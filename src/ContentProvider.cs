using System;
using System.Collections;
using System.Collections.Generic;
using RoR2;
using RoR2.ContentManagement;
using SS2;

namespace MoreQuality
{
    // Our own content pack, for prefabs built from Starstorm2's assets (bigger missiles, etc.).
    // Content packs load in parallel, so this waits for Starstorm2 before building anything.
    internal sealed class ContentProvider : IContentPackProvider
    {
        private static readonly List<Action<ContentPack>> builders = new List<Action<ContentPack>>();
        private readonly ContentPack contentPack = new ContentPack();

        public string identifier => MoreQualityPlugin.GUID;

        public static void Register()
        {
            ContentManager.collectContentPackProviders += add => add(new ContentProvider());
        }

        // Called from item modules' Init(); runs once Starstorm2's content is loaded.
        public static void AddBuilder(Action<ContentPack> builder)
        {
            builders.Add(builder);
        }

        public IEnumerator LoadStaticContentAsync(LoadStaticContentAsyncArgs args)
        {
            var wait = Starstorm.WaitUntilReady("MoreQuality content");
            while (wait.MoveNext()) yield return null;

            foreach (var build in builders)
            {
                try { build(contentPack); }
                catch (Exception e) { MoreQualityPlugin.Log.LogError("Content builder failed: " + e); }
            }
            args.ReportProgress(1f);
        }

        public IEnumerator GenerateContentPackAsync(GetContentPackAsyncArgs args)
        {
            ContentPack.Copy(contentPack, args.output);
            args.ReportProgress(1f);
            yield break;
        }

        public IEnumerator FinalizeAsync(FinalizeAsyncArgs args)
        {
            args.ReportProgress(1f);
            yield break;
        }
    }
}
