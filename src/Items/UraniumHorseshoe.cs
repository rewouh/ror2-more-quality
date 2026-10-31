using R2API;
using RoR2;
using SS2;
using UnityEngine;

namespace MoreQuality.Items
{
    // More speed and jump, less fall damage, and landings stomp the ground.
    internal static class UraniumHorseshoe
    {
        private static readonly T4 Bonus = new T4(0.15f, 0.3f, 0.75f, 1.5f);
        private static readonly T4 FallReduction = new T4(0.25f, 0.5f, 0.8f, 1f);
        private static readonly T4 MaxStomp = new T4(1.5f, 3f, 7.5f, 15f);   // of your damage, at a long fall
        private const float StompRadius = 10f;

        internal static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += (body, args) =>
            {
                var bonus = Q.Sum(Q.Counts(body, SS2Content.Items.UraniumHorseshoe), Bonus);
                args.moveSpeedMultAdd += bonus;
                args.jumpPowerMultAdd += bonus;
            };
            DamageHooks.AddIncoming(SofterFalls);
            Hooks.Patch(typeof(GlobalEventManager), nameof(GlobalEventManager.OnCharacterHitGroundServer), typeof(UraniumHorseshoe), prefix: nameof(Landed));
        }

        private static void SofterFalls(HealthComponent victim, DamageInfo damageInfo, CharacterBody attacker)
        {
            if ((damageInfo.damageType.damageType & DamageType.FallDamage) == 0) return;
            var counts = Q.Counts(victim.body, SS2Content.Items.UraniumHorseshoe);
            if (!Q.Any(counts)) return;
            damageInfo.damage *= 1f - Mathf.Min(1f, Q.Stack(counts, FallReduction));
        }

        private static void Landed(CharacterBody characterBody, CharacterMotor.HitGroundInfo hitGroundInfo)
        {
            var counts = Q.Counts(characterBody, SS2Content.Items.UraniumHorseshoe);
            if (Q.Any(counts)) Stomp(characterBody, -hitGroundInfo.velocity.y, Q.Stack(counts, MaxStomp));
        }

        // The harder you land, the harder it hits: a quarter of the maximum at a short drop, all of it at a long one.
        private static void Stomp(CharacterBody body, float fallSpeed, float maxCoefficient)
        {
            if (fallSpeed < 20f) return;
            var coefficient = Mathf.Lerp(maxCoefficient / 4f, maxCoefficient, (fallSpeed - 20f) / 40f);
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
            Visuals.Spawn(Fx.Stomp, body.footPosition + Vector3.up * 0.3f, StompRadius);
        }
    }
}
