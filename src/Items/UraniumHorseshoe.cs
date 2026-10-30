using ItemQualities;
using R2API;
using RoR2;
using SS2;
using UnityEngine;

namespace MoreQuality.Items
{
    // More speed and jump; Epic+ ignores fall damage; Legendary stomps where it lands.
    internal static class UraniumHorseshoe
    {
        private static readonly T4 Bonus = new T4(0.2f, 0.35f, 0.55f, 0.8f);
        private const float StompRadius = 10f;

        internal static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += (body, args) =>
            {
                var bonus = Q.Sum(Q.Counts(body, SS2Content.Items.UraniumHorseshoe), Bonus);
                args.moveSpeedMultAdd += bonus;
                args.jumpPowerMultAdd += bonus;
            };
            Hooks.Patch(typeof(GlobalEventManager), nameof(GlobalEventManager.OnCharacterHitGroundServer), typeof(UraniumHorseshoe), prefix: nameof(Before), postfix: nameof(After));
        }

        private static void Before(CharacterBody characterBody, CharacterMotor.HitGroundInfo hitGroundInfo, out bool __state)
        {
            __state = false;
            var tier = Q.Highest(Q.Counts(characterBody, SS2Content.Items.UraniumHorseshoe));
            if (tier < QualityTier.Epic) return;

            if ((characterBody.bodyFlags & CharacterBody.BodyFlags.IgnoreFallDamage) == 0)
            {
                characterBody.bodyFlags |= CharacterBody.BodyFlags.IgnoreFallDamage;
                __state = true;
            }
            if (tier == QualityTier.Legendary) Stomp(characterBody, -hitGroundInfo.velocity.y);
        }

        private static void After(CharacterBody characterBody, bool __state)
        {
            if (__state) characterBody.bodyFlags &= ~CharacterBody.BodyFlags.IgnoreFallDamage;
        }

        // The harder you land, the harder it hits: 150% base damage at a short drop, up to 600%.
        private static void Stomp(CharacterBody body, float fallSpeed)
        {
            if (fallSpeed < 20f) return;
            var coefficient = Mathf.Lerp(1.5f, 6f, (fallSpeed - 20f) / 40f);
            new BlastAttack
            {
                attacker = body.gameObject,
                inflictor = body.gameObject,
                teamIndex = body.teamComponent.teamIndex,
                position = body.footPosition,
                radius = StompRadius,
                baseDamage = body.damage * coefficient,
                crit = body.RollCrit(),
                procCoefficient = 1f,
                damageColorIndex = DamageColorIndex.Item,
                damageType = DamageType.Stun1s,
                falloffModel = BlastAttack.FalloffModel.None,
                attackerFiltering = AttackerFiltering.NeverHitSelf
            }.Fire();
            EffectManager.SpawnEffect(LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/ImpactEffects/BeetleGuardGroundSlam"),
                new EffectData { origin = body.footPosition, scale = StompRadius }, true);
        }
    }
}
