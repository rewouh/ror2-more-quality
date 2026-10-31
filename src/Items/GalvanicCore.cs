using System.Linq;
using ItemQualities;
using RoR2;
using RoR2.Orbs;
using SS2;
using UnityEngine;
using UnityEngine.Networking;

namespace MoreQuality.Items
{
    // Higher stun chance; razed enemies take extra damage and get random debuffs.
    // Every second, each razed enemy arcs lightning to the nearest other enemy.
    internal static class GalvanicCore
    {
        private static readonly T4 StunChance = new T4(0.15f, 0.2f, 0.35f, 0.6f);
        private static readonly T4 DamageTaken = new T4(0.1f, 0.2f, 0.5f, 1f);
        private static readonly T4 Debuffs = new T4(1f, 2f, 3f, 5f);
        private static readonly T4 DebuffDuration = new T4(3f, 4f, 6f, 10f);
        private static readonly T4 ArcDamage = new T4(0.5f, 1f, 2.5f, 5f);   // of the owner's damage
        private const float ArcRange = 15f;

        internal static void Init()
        {
            Hooks.Patch(typeof(SS2.Items.GalvanicCore), "SetStateDamageGalvanic", typeof(GalvanicCore), prefix: nameof(Before), postfix: nameof(After));
            Hooks.Patch(typeof(CharacterBody), "OnBuffFirstStackGained", typeof(GalvanicCore), postfix: nameof(OnBuffGained));
            DamageHooks.Add(ExtraDamage);
            Ticker.Every(1f, Arc);
        }

        private static void Before(DamageReport damageReport, out float __state)
        {
            __state = SS2.Items.GalvanicCore.stunChance;
            var counts = Q.Counts(damageReport.attackerMaster, SS2Content.Items.GalvanicCore);
            if (Q.Any(counts)) SS2.Items.GalvanicCore.stunChance = Mathf.Max(__state, Q.AtHighest(counts, StunChance));
        }

        private static void After(float __state)
        {
            SS2.Items.GalvanicCore.stunChance = __state;
        }

        private static void ExtraDamage(HealthComponent victim, DamageInfo damageInfo, CharacterBody attacker)
        {
            if (!victim.body || victim.body.GetBuffCount(SS2Content.Buffs.bdGalvanized) <= 0) return;
            var counts = Q.Counts(attacker, SS2Content.Items.GalvanicCore);
            if (Q.Any(counts)) damageInfo.damage *= 1f + Q.Sum(counts, DamageTaken);
        }

        // Razing doesn't record who caused it, so the best quality core on the player team decides.
        private static CharacterBody BestOwner(out QualityTier best)
        {
            CharacterBody owner = null;
            best = QualityTier.None;
            foreach (var player in PlayerCharacterMasterController.instances)
            {
                var body = player.master ? player.master.GetBody() : null;
                var tier = Q.Highest(Q.Counts(body, SS2Content.Items.GalvanicCore));
                if (tier > best) { best = tier; owner = body; }
            }
            return owner;
        }

        private static void Arc()
        {
            QualityTier best;
            var owner = BestOwner(out best);
            if (best == QualityTier.None) return;
            foreach (var razed in CharacterBody.readOnlyInstancesList.Where(b => b && b.HasBuff(SS2Content.Buffs.bdGalvanized) && b.healthComponent && b.healthComponent.alive).ToList())
            {
                if (razed.teamComponent.teamIndex == owner.teamComponent.teamIndex) continue;
                var next = Enemies.Near(razed.corePosition, ArcRange, owner.teamComponent.teamIndex, razed.healthComponent).FirstOrDefault();
                if (!next || !next.body.mainHurtBox) continue;
                OrbManager.instance.AddOrb(new LightningOrb
                {
                    origin = razed.corePosition,
                    target = next.body.mainHurtBox,
                    attacker = owner.gameObject,
                    teamIndex = owner.teamComponent.teamIndex,
                    damageValue = owner.damage * Q.Stack(Q.Counts(owner, SS2Content.Items.GalvanicCore), ArcDamage),
                    isCrit = owner.RollCrit(),
                    procCoefficient = 0.3f,
                    damageColorIndex = DamageColorIndex.Item,
                    lightningType = LightningOrb.LightningType.Ukulele,
                    bouncesRemaining = 0,
                    range = ArcRange
                });
            }
        }

        private static void OnBuffGained(CharacterBody __instance, BuffDef buffDef)
        {
            if (!NetworkServer.active || buffDef != SS2Content.Buffs.bdGalvanized) return;
            QualityTier best;
            var owner = BestOwner(out best);
            if (best == QualityTier.None || __instance.teamComponent.teamIndex == owner.teamComponent.teamIndex) return;
            var duration = Q.Stack(Q.Counts(owner, SS2Content.Items.GalvanicCore), DebuffDuration);
            foreach (var debuff in Enumerable.Range(0, 6).OrderBy(_ => Random.value).Take((int)Debuffs[best]))
                Apply(__instance, owner, debuff, duration);
        }

        private static void Apply(CharacterBody victim, CharacterBody owner, int debuff, float duration)
        {
            switch (debuff)
            {
                case 0: victim.AddTimedBuff(RoR2Content.Buffs.Slow50, duration); break;
                case 1: victim.AddTimedBuff(RoR2Content.Buffs.Weak, duration); break;
                case 2: victim.AddTimedBuff(RoR2Content.Buffs.Cripple, duration); break;
                case 3: victim.AddTimedBuff(RoR2Content.Buffs.BeetleJuice, duration); break;
                case 4: Dot(victim, owner, DotController.DotIndex.Burn, duration); break;
                default: Dot(victim, owner, DotController.DotIndex.Bleed, duration); break;
            }
        }

        private static void Dot(CharacterBody victim, CharacterBody owner, DotController.DotIndex dot, float duration)
        {
            var info = new InflictDotInfo
            {
                attackerObject = owner ? owner.gameObject : null,
                victimObject = victim.gameObject,
                dotIndex = dot,
                duration = duration,
                damageMultiplier = 1f
            };
            DotController.InflictDot(ref info);
        }
    }
}
