using System.Linq;
using ItemQualities;
using RoR2;
using SS2;
using UnityEngine;

namespace MoreQuality.Items
{
    // Using your secondary can grant a random positive effect for 5s (no attack speed in the pool).
    // Legendary always grants two different ones.
    internal static class X4
    {
        private static readonly T4 Chance = new T4(25f, 40f, 60f, 100f);
        private static readonly T4 ChancePerStack = new T4(10f, 10f, 10f, 10f);
        private const float Duration = 5f;

        internal static void Init()
        {
            Hooks.Patch(typeof(SS2.Items.X4.Behavior), "X4HealOnSkillActivation", typeof(X4), postfix: nameof(OnSkill));
        }

        private static void OnSkill(GenericSkill skill)
        {
            var body = skill ? skill.characterBody : null;
            if (!body || !body.skillLocator || (skill != body.skillLocator.secondary && skill != body.skillLocator.secondaryBonusStockSkill)) return;
            var counts = Q.Counts(body, SS2Content.Items.X4);
            if (!Q.Any(counts)) return;

            var effects = Q.Highest(counts) == QualityTier.Legendary ? 2 : Util.CheckRoll(Q.Stacked(counts, Chance, ChancePerStack)) ? 1 : 0;
            foreach (var effect in Enumerable.Range(0, 6).OrderBy(_ => Random.value).Take(effects)) Grant(body, effect);
        }

        private static void Grant(CharacterBody body, int effect)
        {
            switch (effect)
            {
                case 0: body.AddTimedBuffAuthority(CustomBuffs.StimArmor.buffIndex, Duration); break;
                case 1: body.AddTimedBuffAuthority(CustomBuffs.StimSpeed.buffIndex, Duration); break;
                case 2: body.AddTimedBuffAuthority(CustomBuffs.StimDamage.buffIndex, Duration); break;
                case 3: body.AddTimedBuffAuthority(CustomBuffs.StimCrit.buffIndex, Duration); break;
                case 4: body.AddTimedBuffAuthority(CustomBuffs.StimRegen.buffIndex, Duration); break;
                default: body.healthComponent.AddBarrierAuthority(body.healthComponent.fullCombinedHealth * 0.1f); break;
            }
        }
    }
}
