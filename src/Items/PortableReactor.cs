using ItemQualities;
using R2API;
using RoR2;
using SS2;

namespace MoreQuality.Items
{
    // Lasts longer and you hit harder while invulnerable; Legendary chests opened during it drop 2 items.
    internal static class PortableReactor
    {
        private static readonly T4 ExtraDuration = new T4(20f, 40f, 60f, 100f);
        private static readonly T4 Damage = new T4(0.2f, 0.4f, 0.7f, 1f);

        internal static void Init()
        {
            Hooks.Patch(typeof(SS2.Items.PortableReactor), "ImFuckingInvincible", typeof(PortableReactor), postfix: nameof(OnBodyStart));
            Hooks.Patch(typeof(PurchaseInteraction), nameof(PurchaseInteraction.OnInteractionBegin), typeof(PortableReactor), prefix: nameof(OnPurchase));
            RecalculateStatsAPI.GetStatCoefficients += (body, args) =>
            {
                if (body.HasBuff(SS2Content.Buffs.BuffReactor)) args.damageMultAdd += Q.Sum(Q.Counts(body, SS2Content.Items.PortableReactor), Damage);
            };
        }

        private static void OnBodyStart(CharacterBody obj)
        {
            var counts = Q.Counts(obj, SS2Content.Items.PortableReactor);
            if (!Q.Any(counts)) return;
            // Same duration Starstorm2 just gave, plus the quality bonus; re-adding a non-stacking timed
            // buff with a longer duration extends it.
            var stack = obj.inventory.GetItemCountEffective(SS2Content.Items.PortableReactor);
            var baseDuration = (obj.isPlayerControlled ? SS2.Items.PortableReactor.invulnTime : SS2.Items.PortableReactor.invulnTime / 4f)
                + (stack - 1) * SS2.Items.PortableReactor.stackingInvuln;
            obj.AddTimedBuff(SS2Content.Buffs.BuffReactor, baseDuration + Q.AtHighest(counts, ExtraDuration));
        }

        private static void OnPurchase(PurchaseInteraction __instance, Interactor activator)
        {
            var body = activator ? activator.GetComponent<CharacterBody>() : null;
            if (!body || !body.HasBuff(SS2Content.Buffs.BuffReactor) || !__instance.CanBeAffordedByInteractor(activator)) return;
            if (Q.Highest(Q.Counts(body, SS2Content.Items.PortableReactor)) != QualityTier.Legendary) return;
            var chest = __instance.GetComponent<ChestBehavior>();
            if (chest) chest.dropCount = System.Math.Max(chest.dropCount, 1) + 1;
        }
    }
}
