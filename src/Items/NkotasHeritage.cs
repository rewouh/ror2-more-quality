using ItemQualities;
using RoR2;
using SS2;

namespace MoreQuality.Items
{
    // Every item the Heritage gives is a quality item, with the Heritage's own quality.
    internal static class NkotasHeritage
    {
        private static QualityTier dropping = QualityTier.None;

        internal static void Init()
        {
            Hooks.Patch(typeof(SS2.Items.NkotasHeritage.NkotasManager), "ActivateSingle", typeof(NkotasHeritage), prefix: nameof(Before), postfix: nameof(After));
            Hooks.Patch(typeof(PickupDropletController), nameof(PickupDropletController.CreatePickupDroplet), typeof(NkotasHeritage), prefix: nameof(OnDroplet),
                args: new[] { typeof(PickupIndex), typeof(UnityEngine.Vector3), typeof(UnityEngine.Vector3) });
        }

        private static void Before(CharacterBody body)
        {
            dropping = Q.Highest(Q.Counts(body, SS2Content.Items.NkotasHeritage));
        }

        private static void After()
        {
            dropping = QualityTier.None;
        }

        // Players get world drops. (AI allies holding the Heritage get theirs as normal, unconverted items.)
        private static void OnDroplet(ref PickupIndex pickupIndex)
        {
            if (dropping == QualityTier.None) return;
            var upgraded = QualityCatalog.GetPickupIndexOfQuality(pickupIndex, dropping);
            if (upgraded.isValid) pickupIndex = upgraded;
        }
    }
}
