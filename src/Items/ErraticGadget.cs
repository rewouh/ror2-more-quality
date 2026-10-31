using System.Collections.Generic;
using System.Linq;
using ItemQualities;
using R2API;
using RoR2;
using RoR2.Orbs;
using SS2;
using UnityEngine;
using GadgetOrb = SS2.Items.ErraticGadget.GadgetLightningOrb;

namespace MoreQuality.Items
{
    // More chain lightning, and every gadget strike can also hit a random enemy anywhere on the stage.
    // Rarely, a strike hits an unopened chest somewhere on the stage instead and opens it for free.
    internal static class ErraticGadget
    {
        private static readonly T4 ChainChance = new T4(0.15f, 0.2f, 0.35f, 0.6f);
        private static readonly T4 RemoteChance = new T4(5f, 10f, 25f, 50f);
        private static readonly T4 RemoteChancePerStack = new T4(5f, 5f, 5f, 5f);
        private static readonly T4 ChestChance = new T4(0.25f, 0.5f, 1.25f, 2.5f);

        private static ModdedProcType remoteStrike;

        internal static void Init()
        {
            remoteStrike = ProcTypeAPI.ReserveProcType();
            Hooks.Patch(typeof(SS2.Items.ErraticGadget.Behavior), "OnDamageDealtServer", typeof(ErraticGadget), prefix: nameof(Before), postfix: nameof(After));
            Hooks.Patch(typeof(GadgetOrb), "OnArrival", typeof(ErraticGadget), postfix: nameof(OnStrike));
        }

        private static void Before(SS2.Items.ErraticGadget.Behavior __instance, out float __state)
        {
            __state = SS2.Items.ErraticGadget.procChance;
            var counts = Q.Counts(__instance.body, SS2Content.Items.ErraticGadget);
            if (Q.Any(counts)) SS2.Items.ErraticGadget.procChance = Mathf.Max(__state, Q.AtHighest(counts, ChainChance));
        }

        private static void After(float __state)
        {
            SS2.Items.ErraticGadget.procChance = __state;
        }

        private static void OnStrike(GadgetOrb __instance)
        {
            if (!__instance.attacker || ProcTypeAPI.HasModdedProc(__instance.procChainMask, remoteStrike)) return;
            var body = __instance.attacker.GetComponent<CharacterBody>();
            var counts = Q.Counts(body, SS2Content.Items.ErraticGadget);
            if (Q.Any(counts) && Util.CheckRoll(Q.Stack(counts, ChestChance), body.master)) StrikeChest();
            if (!Q.Any(counts) || !Util.CheckRoll(Q.Stacked(counts, RemoteChance, RemoteChancePerStack), body.master)) return;

            var current = __instance.target ? __instance.target.healthComponent : null;
            var candidates = Enemies.All(__instance.teamIndex).Where(h => h != current && h.body.mainHurtBox).ToList();
            if (candidates.Count == 0) return;
            var victim = candidates[Random.Range(0, candidates.Count)];

            var orb = new GadgetOrb
            {
                origin = __instance.target ? __instance.target.transform.position : __instance.origin,
                target = victim.body.mainHurtBox,
                attacker = __instance.attacker,
                inflictor = __instance.inflictor,
                teamIndex = __instance.teamIndex,
                damageValue = __instance.damageValue,
                bouncesRemaining = 0,
                isCrit = __instance.isCrit,
                bouncedObjects = new List<HealthComponent>(),
                procChainMask = __instance.procChainMask,
                procCoefficient = __instance.procCoefficient,
                damageColorIndex = __instance.damageColorIndex,
                damageType = __instance.damageType,
                range = __instance.range
            };
            ProcTypeAPI.AddModdedProc(ref orb.procChainMask, remoteStrike);
            OrbManager.instance.AddOrb(orb);
        }

        // A closed chest that costs gold, anywhere on the stage.
        private static void StrikeChest()
        {
            var chests = InstanceTracker.GetInstancesList<ChestBehavior>()
                .Select(c => new { Chest = c, Purchase = c.GetComponent<PurchaseInteraction>() })
                .Where(c => c.Purchase && c.Purchase.available && c.Purchase.costType == CostTypeIndex.Money)
                .ToList();
            if (chests.Count == 0) return;
            var pick = chests[Random.Range(0, chests.Count)];
            pick.Purchase.SetAvailable(false);
            pick.Chest.Open();
            EffectManager.SpawnEffect(LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/ImpactEffects/LightningStrikeImpact"), new EffectData { origin = pick.Chest.transform.position, scale = 3f }, true);
        }
    }
}
