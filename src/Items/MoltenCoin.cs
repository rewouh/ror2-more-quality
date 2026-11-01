using System.Collections.Generic;
using System.Linq;
using RoR2;
using SS2;
using UnityEngine;
using UnityEngine.Networking;

namespace MoreQuality.Items
{
    // Higher ignite chance. Pay to burn: burning enemies drop a molten coin when they die. Picking it up
    // pays gold and makes you Molten for a few seconds, setting enemies around you on fire.
    internal static class MoltenCoin
    {
        private static readonly T4 Chance = new T4(8f, 10f, 16f, 26f);
        private static readonly T4 Gold = new T4(2f, 4f, 10f, 20f);
        private static readonly T4 MoltenDuration = new T4(3f, 4f, 6f, 10f);
        private static readonly T4 AuraRadius = new T4(5f, 7f, 11f, 16f);
        private static readonly T4 AuraBurn = new T4(0.5f, 1f, 2.5f, 5f);   // burn strength per ignite

        // Marks a coin with what it gives (server side only).
        private sealed class Coin : MonoBehaviour { public float radius, duration, burn; }

        // The Molten buff's stack count is the aura's radius in metres, so every game can draw it.
        private static readonly Dictionary<CharacterBody, float> burning = new Dictionary<CharacterBody, float>();
        // When each enemy last showed the ignite flames: at most once every IgniteFxInterval, to keep effects light.
        private static readonly Dictionary<CharacterBody, float> lastFlames = new Dictionary<CharacterBody, float>();
        private const float IgniteFxInterval = 2f;

        internal static void Init()
        {
            Hooks.Patch(typeof(SS2.Items.MoltenCoin.Behavior), "OnDamageDealtServer", typeof(MoltenCoin), prefix: nameof(Before), postfix: nameof(After));
            Hooks.Patch(typeof(MoneyPickup), "OnTriggerStay", typeof(MoltenCoin), prefix: nameof(BeforePickup), postfix: nameof(AfterPickup));
            GlobalEventManager.onCharacterDeathGlobal += OnDeath;
            Ticker.Every(0.5f, Aura);
            Run.onRunStartGlobal += _ => { burning.Clear(); lastFlames.Clear(); };
        }

        internal static float AuraRadiusFor(CharacterBody body)
        {
            return Mathf.Max(1f, body.GetBuffCount(CustomBuffs.Molten));
        }

        // Starstorm2 reads its chance from a static field, so swap it for the duration of the roll.
        private static void Before(SS2.Items.MoltenCoin.Behavior __instance, out float __state)
        {
            __state = SS2.Items.MoltenCoin.procChance;
            var counts = Q.Counts(__instance.body, SS2Content.Items.MoltenCoin);
            if (Q.Any(counts)) SS2.Items.MoltenCoin.procChance = Mathf.Max(__state, Q.AtHighest(counts, Chance));
        }

        private static void After(float __state)
        {
            SS2.Items.MoltenCoin.procChance = __state;
        }

        private static void OnDeath(DamageReport report)
        {
            var victim = report.victimBody;
            if (!NetworkServer.active || !victim || !report.attackerMaster) return;
            if (!victim.HasBuff(RoR2Content.Buffs.OnFire) && !victim.HasBuff(DLC1Content.Buffs.StrongerBurn)) return;
            var counts = Q.Counts(report.attackerMaster, SS2Content.Items.MoltenCoin);
            if (!Q.Any(counts)) return;

            var prefab = Fx.MoltenCoin ? Fx.MoltenCoin : LegacyResourcesAPI.Load<GameObject>("Prefabs/NetworkedObjects/BonusMoneyPack");
            var pack = Object.Instantiate(prefab, victim.corePosition, Quaternion.identity);
            var filter = pack.GetComponentInChildren<TeamFilter>();
            if (filter) filter.teamIndex = report.attackerTeamIndex;
            var pickup = pack.GetComponentInChildren<MoneyPickup>();
            if (pickup)
            {
                pickup.baseGoldReward = (int)Q.Sum(counts, Gold);
                pickup.shouldScale = true;
                var coin = pickup.gameObject.AddComponent<Coin>();
                coin.radius = Q.Stack(counts, AuraRadius);
                coin.duration = Q.Stack(counts, MoltenDuration);
                coin.burn = Q.Stack(counts, AuraBurn);
            }
            NetworkServer.Spawn(pack);
        }

        private static void BeforePickup(MoneyPickup __instance, bool ___alive, out bool __state)
        {
            __state = ___alive;
        }

        private static void AfterPickup(MoneyPickup __instance, Collider other, bool ___alive, bool __state)
        {
            if (!__state || ___alive || !other) return;   // not picked up just now
            var coin = __instance.GetComponent<Coin>();
            var body = other.GetComponent<CharacterBody>();
            if (!coin || !body) return;
            burning[body] = coin.burn;
            CustomBuffs.Mark(body, CustomBuffs.Molten, Mathf.RoundToInt(coin.radius), coin.duration);
            Visuals.Spawn(Fx.CoinBurst, body.corePosition, 1f);
        }

        private static void Aura()
        {
            foreach (var entry in burning.ToList())
            {
                var body = entry.Key;
                if (!body || !body.HasBuff(CustomBuffs.Molten)) { burning.Remove(body); continue; }
                foreach (var gone in lastFlames.Keys.Where(b => !b).ToList()) lastFlames.Remove(gone);
                foreach (var enemy in Enemies.Near(body.corePosition, AuraRadiusFor(body), body.teamComponent.teamIndex))
                {
                    var info = new InflictDotInfo
                    {
                        attackerObject = body.gameObject,
                        victimObject = enemy.gameObject,
                        dotIndex = DotController.DotIndex.Burn,
                        duration = 2f,
                        damageMultiplier = entry.Value
                    };
                    DotController.InflictDot(ref info);
                    float last;
                    if (lastFlames.TryGetValue(enemy.body, out last) && Time.fixedTime - last < IgniteFxInterval) continue;
                    lastFlames[enemy.body] = Time.fixedTime;
                    Visuals.Spawn(Fx.Ignite, enemy.body.corePosition, enemy.body.radius);
                }
            }
        }
    }
}
