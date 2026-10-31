using System.Runtime.CompilerServices;
using HarmonyLib;
using RoR2;
using SS2;
using UnityEngine;

namespace MoreQuality.Items
{
    // On the beat: while the metronome has charge, it ticks once a second. Hits landed in the short window
    // after each tick (shown by the On the Beat buff) are critical strikes with bonus damage, and the first
    // one each beat can give a charge back.
    internal static class WatchMetronome
    {
        private static readonly T4 Window = new T4(0.2f, 0.3f, 0.45f, 0.6f);
        private static readonly T4 BeatDamage = new T4(0.2f, 0.4f, 1f, 2f);
        private static readonly T4 RefundChance = new T4(10f, 20f, 50f, 100f);
        private const float Beat = 1f;

        private static readonly AccessTools.FieldRef<SS2.Items.WatchMetronome.Behavior, float> charge = AccessTools.FieldRefAccess<SS2.Items.WatchMetronome.Behavior, float>("charge");
        private static readonly ConditionalWeakTable<CharacterBody, object> refunded = new ConditionalWeakTable<CharacterBody, object>();

        internal static void Init()
        {
            Ticker.Every(Beat, Tick);
            DamageHooks.Add(OnBeat);
        }

        private static void Tick()
        {
            foreach (var body in CharacterBody.readOnlyInstancesList)
            {
                if (!body || body.GetBuffCount(SS2Content.Buffs.BuffWatchMetronome) <= 0) continue;
                var counts = Q.Counts(body, SS2Content.Items.WatchMetronome);
                if (!Q.Any(counts)) continue;
                refunded.Remove(body);
                body.AddTimedBuff(CustomBuffs.OnTheBeat, Q.AtHighest(counts, Window));
                Visuals.SpawnOn(Fx.MetronomeTick, body, body.radius * 2f);
            }
        }

        private static void OnBeat(HealthComponent victim, DamageInfo damageInfo, CharacterBody attacker)
        {
            if (!attacker.HasBuff(CustomBuffs.OnTheBeat) || damageInfo.procCoefficient <= 0f) return;
            if (victim.body && victim.body.teamComponent.teamIndex == attacker.teamComponent.teamIndex) return;
            var counts = Q.Counts(attacker, SS2Content.Items.WatchMetronome);
            if (!Q.Any(counts)) return;
            damageInfo.crit = true;
            damageInfo.damage *= 1f + Q.Sum(counts, BeatDamage);

            object done;
            if (refunded.TryGetValue(attacker, out done)) return;
            refunded.Add(attacker, null);
            var behavior = attacker.GetComponent<SS2.Items.WatchMetronome.Behavior>();
            if (behavior && Util.CheckRoll(Q.Stack(counts, RefundChance), attacker.master)) charge(behavior) = Mathf.Min(5f, charge(behavior) + 1f);
        }
    }
}
