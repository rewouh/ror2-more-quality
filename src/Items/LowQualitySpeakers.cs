using ItemQualities;
using RoR2;
using SS2;
using UnityEngine;

namespace MoreQuality.Items
{
    // The speakers' burst launches enemies into the air; they take fall damage when they land.
    // Monsters normally ignore fall damage, so the landing hit is applied here with the game's
    // formula (overspeed / 60 x max health), credited to you, multiplied by quality. Bosses take at
    // most 10% of their max health per landing. Flying enemies can't fall, so they take the hit at once.
    internal static class LowQualitySpeakers
    {
        private static readonly T4 Height = new T4(10f, 20f, 40f, 70f);
        private static readonly T4 FallMultiplier = new T4(1f, 1.5f, 2.5f, 4f);
        private const float BossCap = 0.1f;

        private sealed class Launched : MonoBehaviour
        {
            public CharacterBody owner;
            public QualityTier tier;
            public float launchTime;
        }

        private struct State { public float KnockUp; public bool WasPending; }

        internal static void Init()
        {
            Hooks.Patch(typeof(SS2.Items.LowQualitySpeakers.Behavior), "OnIncomingDamageServer", typeof(LowQualitySpeakers), prefix: nameof(Before), postfix: nameof(After));
            Hooks.Patch(typeof(GlobalEventManager), nameof(GlobalEventManager.OnCharacterHitGroundServer), typeof(LowQualitySpeakers), prefix: nameof(OnLanding));
        }

        private static float LaunchSpeed(float height)
        {
            return Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y) * height);
        }

        private static void Before(SS2.Items.LowQualitySpeakers.Behavior __instance, bool ___destroyProjectilesNextFrame, out State __state)
        {
            __state = new State { KnockUp = SS2.Items.LowQualitySpeakers.knockUpForce, WasPending = ___destroyProjectilesNextFrame };
            var counts = Q.Counts(__instance.body, SS2Content.Items.LowQualitySpeakers);
            if (Q.Any(counts)) SS2.Items.LowQualitySpeakers.knockUpForce = LaunchSpeed(Q.AtHighest(counts, Height));
        }

        private static void After(SS2.Items.LowQualitySpeakers.Behavior __instance, bool ___destroyProjectilesNextFrame, State __state)
        {
            SS2.Items.LowQualitySpeakers.knockUpForce = __state.KnockUp;
            if (__state.WasPending || !___destroyProjectilesNextFrame) return;   // the burst didn't fire this time

            var body = __instance.body;
            var counts = Q.Counts(body, SS2Content.Items.LowQualitySpeakers);
            if (!Q.Any(counts)) return;
            var tier = Q.Highest(counts);
            var radius = SS2.Items.LowQualitySpeakers.baseRadius + counts.TotalCount * (SS2.Items.LowQualitySpeakers.radiusPerStack - 1f);
            foreach (var enemy in Enemies.Near(body.corePosition, radius, body.teamComponent.teamIndex))
            {
                var victim = enemy.body;
                if (victim.characterMotor && !victim.characterMotor.isFlying)
                {
                    var mark = victim.GetComponent<Launched>() ?? victim.gameObject.AddComponent<Launched>();
                    mark.owner = body;
                    mark.tier = tier;
                    mark.launchTime = Time.fixedTime;
                }
                else
                {
                    Land(victim, body, tier, LaunchSpeed(Height[tier]));
                }
            }
        }

        private static void OnLanding(CharacterBody characterBody, CharacterMotor.HitGroundInfo hitGroundInfo)
        {
            var mark = characterBody ? characterBody.GetComponent<Launched>() : null;
            if (!mark || Time.fixedTime - mark.launchTime < 0.2f) return;   // still leaving the ground
            Object.Destroy(mark);
            if (mark.owner) Land(characterBody, mark.owner, mark.tier, Mathf.Abs(hitGroundInfo.velocity.y));
        }

        private static void Land(CharacterBody victim, CharacterBody owner, QualityTier tier, float speed)
        {
            var overspeed = Mathf.Max(0f, speed - 20f);
            if (overspeed <= 0f || !victim.healthComponent) return;
            var fraction = overspeed / 60f * FallMultiplier[tier];
            if (victim.isBoss) fraction = Mathf.Min(fraction, BossCap);
            victim.healthComponent.TakeDamage(new DamageInfo
            {
                attacker = owner.gameObject,
                inflictor = owner.gameObject,
                damage = fraction * victim.maxHealth,
                position = victim.footPosition,
                procCoefficient = 0f,
                crit = false,
                damageColorIndex = DamageColorIndex.Item,
                damageType = DamageType.Generic
            });
        }
    }
}
