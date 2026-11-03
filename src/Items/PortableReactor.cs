using System.Collections.Generic;
using R2API;
using RoR2;
using SS2;
using UnityEngine;
using UnityEngine.Networking;

namespace MoreQuality.Items
{
    // Lasts longer and you hit harder while invulnerable.
    // Meltdown: when the invulnerability ends, the reactor blows up around you, harder the more damage it absorbed.
    internal static class PortableReactor
    {
        private static readonly T4 ExtraDuration = new T4(20f, 40f, 100f, 200f);
        private static readonly T4 Damage = new T4(0.2f, 0.4f, 1f, 2f);
        private static readonly T4 BlastBase = new T4(10f, 20f, 50f, 100f);        // of your damage
        private static readonly T4 BlastAbsorbed = new T4(1f, 2f, 5f, 10f);        // of the damage absorbed
        private static readonly T4 BlastRadius = new T4(20f, 25f, 32f, 45f);

        private static readonly Dictionary<CharacterBody, float> absorbed = new Dictionary<CharacterBody, float>();

        internal static void Init()
        {
            Hooks.Patch(typeof(SS2.Items.PortableReactor), "ImFuckingInvincible", typeof(PortableReactor), postfix: nameof(OnBodyStart));
            RecalculateStatsAPI.GetStatCoefficients += (body, args) =>
            {
                if (body.HasBuff(SS2Content.Buffs.BuffReactor)) args.damageMultAdd += Q.Sum(Q.Counts(body, SS2Content.Items.PortableReactor), Damage);
            };
            DamageHooks.AddIncoming(Absorb);
            CharacterBody.onBuffFinalStackLost += OnBuffLost;
            Run.onRunStartGlobal += _ => absorbed.Clear();
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
            obj.AddTimedBuff(SS2Content.Buffs.BuffReactor, baseDuration + Q.Stack(counts, ExtraDuration));
            absorbed[obj] = 0f;
        }

        private static void Absorb(HealthComponent victim, DamageInfo damageInfo, CharacterBody attacker)
        {
            var body = victim.body;
            if (!body || !body.HasBuff(SS2Content.Buffs.BuffReactor) || !absorbed.ContainsKey(body)) return;
            absorbed[body] += damageInfo.damage;
        }

        private static void OnBuffLost(CharacterBody body, BuffDef buff)
        {
            float total;
            if (!NetworkServer.active || buff != SS2Content.Buffs.BuffReactor || !absorbed.TryGetValue(body, out total)) return;
            absorbed.Remove(body);
            var counts = Q.Counts(body, SS2Content.Items.PortableReactor);
            if (!Q.Any(counts) || !body.healthComponent || !body.healthComponent.alive) return;
            var radius = Q.Stack(counts, BlastRadius);
            new BlastAttack
            {
                attacker = body.gameObject,
                inflictor = Sources.Of(SS2Content.Items.PortableReactor),
                teamIndex = body.teamComponent.teamIndex,
                position = body.corePosition,
                radius = radius,
                baseDamage = body.damage * Q.Stack(counts, BlastBase) + total * Q.Stack(counts, BlastAbsorbed),
                baseForce = 3000f,
                crit = body.RollCrit(),
                procCoefficient = 1f,
                damageColorIndex = DamageColorIndex.Item,
                falloffModel = BlastAttack.FalloffModel.SweetSpot,
                attackerFiltering = AttackerFiltering.NeverHitSelf
            }.Fire();
            Visuals.Spawn(Fx.Meltdown, body.corePosition, radius);
        }
    }
}
