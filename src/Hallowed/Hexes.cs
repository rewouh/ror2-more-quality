using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using ItemQualities;
using R2API;
using RoR2;
using RoR2.Items;
using UnityEngine;
using UnityEngine.Networking;

namespace MoreQuality.Hallowed
{
    // A quality hex is a WORSE hex. Its payoff is the cleansing bonus (see Cleansing.cs).

    // Hex of Acceleration: acceleration swings every 3 seconds between the normal penalty and a harsher one.
    internal static class HexOfAcceleration
    {
        private static readonly T4 HarshPenalty = new T4(0.55f, 0.65f, 0.75f, 0.9f);
        private const float Period = 3f;
        private const float BasePenalty = 0.4f;
        private static bool harsh;
        private static readonly System.Action<CharacterBody, float> setAcceleration =
            AccessTools.MethodDelegate<System.Action<CharacterBody, float>>(AccessTools.PropertySetter(typeof(CharacterBody), nameof(CharacterBody.acceleration)));

        internal static void Init()
        {
            Hooks.Patch(typeof(CharacterBody), nameof(CharacterBody.RecalculateStats), typeof(HexOfAcceleration), postfix: nameof(After));
            // Movement runs on each player's own game, so every game flips the phase from the shared run clock.
            RoR2Application.onFixedUpdate += () =>
            {
                if (!Run.instance) return;
                var now = Mathf.FloorToInt(Run.instance.GetRunStopwatch() / Period) % 2 == 1;
                if (now == harsh) return;
                harsh = now;
                foreach (var body in CharacterBody.readOnlyInstancesList)
                {
                    if (body && Q.Any(Q.Counts(body, DLC4Content.Items.SlipperyGround))) body.MarkAllStatsDirty();
                }
            };
        }

        private static void After(CharacterBody __instance)
        {
            if (!harsh) return;
            var tier = Q.Highest(Q.Counts(__instance, DLC4Content.Items.SlipperyGround));
            if (tier != QualityTier.None) setAcceleration(__instance, __instance.acceleration * (1f - HarshPenalty[tier]) / (1f - BasePenalty));
        }
    }

    // Hex of Blindness: vision drops much further.
    internal static class HexOfBlindness
    {
        private static readonly T4 Vision = new T4(65f, 40f, 20f, 1f);
        private static readonly System.Action<CharacterBody, float> setVision =
            AccessTools.MethodDelegate<System.Action<CharacterBody, float>>(AccessTools.PropertySetter(typeof(CharacterBody), nameof(CharacterBody.visionDistance)));

        internal static void Init()
        {
            Hooks.Patch(typeof(CharacterBody), nameof(CharacterBody.RecalculateStats), typeof(HexOfBlindness), postfix: nameof(After));
        }

        private static void After(CharacterBody __instance)
        {
            var tier = Q.Highest(Q.Counts(__instance, DLC4Content.Items.Blindness));
            if (tier != QualityTier.None) setVision(__instance, Mathf.Min(__instance.visionDistance, Vision[tier]));
        }
    }

    // Hex of Famine: healing is cut harder (Legendary: almost entirely).
    internal static class HexOfFamine
    {
        private static readonly T4 Reduction = new T4(0.45f, 0.5f, 0.6f, 0.99f);
        private const float BaseReduction = 0.4f;

        internal static void Init()
        {
            Hooks.Patch(typeof(HealthComponent), nameof(HealthComponent.Heal), typeof(HexOfFamine), prefix: nameof(Before));
        }

        private static void Before(HealthComponent __instance, ref float amount)
        {
            var tier = Q.Highest(Q.Counts(__instance.body, DLC4Content.Items.LowerHealing));
            if (tier != QualityTier.None) amount *= (1f - Reduction[tier]) / (1f - BaseReduction);
        }
    }

    // Hex of Greed: shares more often; Legendary shares with 5 enemies.
    internal static class HexOfGreed
    {
        private static readonly T4 Interval = new T4(13f, 11f, 9f, 6f);

        internal static void Init()
        {
            Hooks.Transpile(typeof(ShareItemsWithEnemiesBehaviour), "FixedUpdate", typeof(HexOfGreed), nameof(IntervalPatch));
            Hooks.Transpile(typeof(ShareItemsWithEnemiesBehaviour), "ShareItemsWithEnemies", typeof(HexOfGreed), nameof(TargetsPatch));
        }

        private static float GetInterval(ShareItemsWithEnemiesBehaviour behaviour)
        {
            var tier = Q.Highest(Q.Counts(behaviour.body, DLC4Content.Items.ShareItemsWithEnemies));
            return tier == QualityTier.None ? 15f : Interval[tier];
        }

        private static int GetTargets(ShareItemsWithEnemiesBehaviour behaviour)
        {
            return Q.Highest(Q.Counts(behaviour.body, DLC4Content.Items.ShareItemsWithEnemies)) == QualityTier.Legendary ? 5 : 3;
        }

        private static IEnumerable<CodeInstruction> IntervalPatch(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldc_R4 && (float)instruction.operand == 15f)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(HexOfGreed), nameof(GetInterval)));
                    continue;
                }
                yield return instruction;
            }
        }

        private static IEnumerable<CodeInstruction> TargetsPatch(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldc_I4_3)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(HexOfGreed), nameof(GetTargets)));
                    continue;
                }
                yield return instruction;
            }
        }
    }

    // Monsters summoned by a quality Hex of Haunting or Mimicry come out stronger.
    internal static class HexMonsters
    {
        private static readonly BuffDef[] Boons =
        {
            RoR2Content.Buffs.ArmorBoost, RoR2Content.Buffs.WarCryBuff, RoR2Content.Buffs.CloakSpeed, RoR2Content.Buffs.FullCrit, RoR2Content.Buffs.PowerBuff
        };

        public static void Empower(CharacterMaster master, int bonusPercent, float boonChance, bool immune)
        {
            if (!master) return;
            RoR2Application.onNextUpdate += () =>
            {
                var body = master ? master.GetBody() : null;
                if (!body) return;
                body.SetBuffCount(CustomBuffs.HexEmpowered.buffIndex, bonusPercent);
                if (Util.CheckRoll(boonChance)) body.AddBuff(Boons[Random.Range(0, Boons.Length)]);
                if (immune) body.AddTimedBuff(RoR2Content.Buffs.Immune, 5f);
            };
        }
    }

    // Hex of Haunting: Poltergeists hit harder and may get a random boon (always at Legendary, plus
    // 5 seconds of immunity when they appear).
    internal static class HexOfHaunting
    {
        private static readonly T4 Damage = new T4(25f, 50f, 100f, 200f);
        private static readonly T4 BoonChance = new T4(25f, 50f, 75f, 100f);
        private static readonly AccessTools.FieldRef<HauntedInteractable, CharacterMaster> spawned = AccessTools.FieldRefAccess<HauntedInteractable, CharacterMaster>("poltergeistMasterInstance");
        private static readonly ConditionalWeakTable<CharacterMaster, object> done = new ConditionalWeakTable<CharacterMaster, object>();

        internal static void Init()
        {
            Hooks.Patch(typeof(HauntedInteractable), "OnTriggerStay", typeof(HexOfHaunting), postfix: nameof(After));
        }

        private static void After(HauntedInteractable __instance)
        {
            var master = spawned(__instance);
            object seen;
            if (!NetworkServer.active || !master || done.TryGetValue(master, out seen)) return;
            done.Add(master, null);
            var tier = Q.Highest(Q.Counts(__instance.ownerBody, DLC4Content.Items.HexHaunting));
            if (tier != QualityTier.None) HexMonsters.Empower(master, (int)Damage[tier], BoonChance[tier], tier == QualityTier.Legendary);
        }
    }

    // Hex of Mimicry: Mimics are faster and hit harder; Legendary ones are immune for 5 seconds.
    internal static class HexOfMimicry
    {
        private static readonly T4 Bonus = new T4(20f, 40f, 70f, 100f);
        // Must start at None: QualityTier's default value (0) is Uncommon, which empowered every summon.
        private static QualityTier spawning = QualityTier.None;

        internal static void Init()
        {
            Hooks.Patch(typeof(MimicryBehaviour), "TrySpawnMimic", typeof(HexOfMimicry), prefix: nameof(Before), postfix: nameof(After));
            Hooks.Patch(typeof(MasterSummon), nameof(MasterSummon.Perform), typeof(HexOfMimicry), postfix: nameof(Summoned));
        }

        private static void Before(MimicryBehaviour __instance) { spawning = Q.Highest(Q.Counts(__instance.body, DLC4Content.Items.Mimicry)); }
        private static void After() { spawning = QualityTier.None; }

        private static void Summoned(CharacterMaster __result)
        {
            if (spawning != QualityTier.None) HexMonsters.Empower(__result, (int)Bonus[spawning], 0f, spawning == QualityTier.Legendary);
        }
    }

    // Hex of Leaping: each jump costs more height. Legendary: no jumping at all.
    internal static class HexOfLeaping
    {
        private static readonly T4 ExtraPenalty = new T4(5f, 10f, 20f, 35f);   // on top of the base 25%

        internal static void Init()
        {
            Hooks.Patch(typeof(WeakAssHamstringBehaviour), "HandleJump", typeof(HexOfLeaping), postfix: nameof(AfterJump));
            RecalculateStatsAPI.GetStatCoefficients += (body, args) =>
            {
                if (Q.Highest(Q.Counts(body, DLC4Content.Items.WeakAssHamstring)) == QualityTier.Legendary) args.jumpPowerMultAdd -= 1f;
            };
        }

        private static void AfterJump(WeakAssHamstringBehaviour __instance)
        {
            var body = __instance.body;
            var tier = Q.Highest(Q.Counts(body, DLC4Content.Items.WeakAssHamstring));
            if (tier == QualityTier.None || !NetworkServer.active) return;
            var buff = DLC4Content.Buffs.WeakAssHamstringJumpReduction;
            body.SetBuffCount(buff.buffIndex, Mathf.Min(100, body.GetBuffCount(buff) + (int)ExtraPenalty[tier]));
        }
    }

    // Hex of Loss: a larger share of your items is disabled.
    internal static class HexOfLoss
    {
        private static readonly T4 Share = new T4(0.15f, 0.25f, 0.35f, 0.5f);

        internal static void Init()
        {
            Hooks.Transpile(typeof(RoamingItemDisableBehavior), "RoamItemDisable", typeof(HexOfLoss), nameof(Patch));
        }

        // The game disables 10% of your items per stack: count each quality hex as share / 10%.
        private static float EffectiveStacks(BaseItemBodyBehavior behaviour)
        {
            var counts = Q.Counts(behaviour.body, DLC4Content.Items.RoamingItemDisable);
            return behaviour.stack - counts.TotalQualityCount + Q.Sum(counts, Share) / 0.1f;
        }

        private static IEnumerable<CodeInstruction> Patch(IEnumerable<CodeInstruction> instructions)
        {
            var stack = AccessTools.Field(typeof(BaseItemBodyBehavior), nameof(BaseItemBodyBehavior.stack));
            var list = instructions.ToList();
            for (var i = 0; i < list.Count - 1; i++)
            {
                if (list[i].LoadsField(stack) && list[i + 1].opcode == OpCodes.Conv_R4)
                {
                    list[i] = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(HexOfLoss), nameof(EffectiveStacks))).MoveLabelsFrom(list[i]);
                    list[i + 1] = new CodeInstruction(OpCodes.Nop).MoveLabelsFrom(list[i + 1]);
                }
            }
            return list;
        }
    }

    // Hex of Misfortune: debuffs are more likely; Epic gives 2 at a time, Legendary 4.
    internal static class HexOfMisfortune
    {
        private static readonly T4 Chance = new T4(60f, 70f, 80f, 100f);
        private static readonly MethodInfo applyDebuff = AccessTools.Method(typeof(RandomDebuffOnInteractBehaviour), "ApplyDebuff");
        private static readonly MethodInfo reroll = AccessTools.Method(typeof(RandomDebuffOnInteractBehaviour), "RerollHalfOfDebuffs");
        private static int applied;

        internal static void Init()
        {
            Hooks.Patch(typeof(RandomDebuffOnInteractBehaviour), "RollChanceForDebuff", typeof(HexOfMisfortune), prefix: nameof(Before), postfix: nameof(After));
            Hooks.Patch(typeof(RandomDebuffOnInteractBehaviour), "ApplyDebuff", typeof(HexOfMisfortune), postfix: nameof(Counted));
        }

        private static void Before() { applied = 0; }
        private static void Counted() { applied++; }

        private static void After(RandomDebuffOnInteractBehaviour __instance, Interactor interactor, GameObject interactableObject)
        {
            var body = __instance.body;
            if (!body || !interactor || interactor.GetComponent<CharacterBody>() != body || interactableObject.GetComponent<ShrineChanceBehavior>()) return;
            var tier = Q.Highest(Q.Counts(body, DLC4Content.Items.RandomDebuffOnInteract));
            if (tier == QualityTier.None) return;
            // The game rolled 50% per stack; top the misses up to the quality chance.
            var hits = applied;
            var topUp = (Chance[tier] - 50f) / 50f * 100f;
            for (var i = applied; i < __instance.stack; i++)
            {
                if (Util.CheckRoll(topUp, body.master)) { applyDebuff.Invoke(__instance, null); hits++; }
            }
            var extra = tier == QualityTier.Legendary ? 3 : tier == QualityTier.Epic ? 1 : 0;
            for (var i = 0; i < hits * extra; i++)
            {
                reroll.Invoke(__instance, null);
                applyDebuff.Invoke(__instance, null);
            }
        }
    }

    // Hex of Ossification: more barrier; Epic also fully heals them; Legendary passes on an elite's powers.
    internal static class HexOfOssification
    {
        private static readonly T4 Barrier = new T4(1.25f, 1.5f, 2f, 3f);
        private const float EliteDuration = 20f;

        internal static void Init()
        {
            Hooks.Patch(typeof(GlobalEventManager), nameof(GlobalEventManager.ProcBarrierEnemiesOnKill), typeof(HexOfOssification), postfix: nameof(After));
        }

        private static void After(DamageReport damageReport, int itemStacks, CharacterBody victimBody, TeamIndex attackerTeamIndex)
        {
            var tier = Q.Highest(Q.Counts(damageReport.attackerBody, DLC4Content.Items.BarrierEnemiesOnKill));
            if (tier == QualityTier.None || !victimBody) return;
            var radius = 15f + 15f * itemStacks + victimBody.radius;
            var eliteBuffs = tier == QualityTier.Legendary && victimBody.isElite
                ? BuffCatalog.eliteBuffIndices.Where(victimBody.HasBuff).ToList()
                : new List<BuffIndex>();
            foreach (var enemy in Enemies.Near(victimBody.corePosition, radius, attackerTeamIndex, victimBody.healthComponent))
            {
                enemy.AddBarrier(victimBody.maxHealth * (Barrier[tier] - 1f));
                if (tier >= QualityTier.Epic) enemy.HealFraction(1f, default(ProcChainMask));
                foreach (var buff in eliteBuffs) enemy.body.AddTimedBuff(buff, EliteDuration);
            }
        }
    }

    // Hex of Pain: max health falls further (Legendary: by 80%).
    internal static class HexOfPain
    {
        // Extra curse per quality copy, on top of the game's 0.3: 1 / (1 - reduction) - 1.3.
        private static readonly T4 ExtraCurse = new T4(0.2385f, 0.3667f, 0.7f, 3.7f);

        internal static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += (body, args) =>
            {
                var counts = Q.Counts(body, DLC4Content.Items.HexCurse);
                if (Q.Any(counts)) args.baseCurseAdd += Q.Sum(counts, ExtraCurse);
            };
        }
    }

    // Hex of Patience: outside the charging Teleporter you take extra, growing damage. Epic: a random
    // debuff every 3 seconds. Legendary: a 10% chance every 3 seconds to be executed.
    internal static class HexOfPatience
    {
        private static readonly T4 DamagePerSecond = new T4(0.005f, 0.0075f, 0.01f, 0.015f);   // of max health
        private const float Growth = 0.125f;
        private const float ExecuteChance = 10f;
        private static readonly Dictionary<CharacterBody, float> outside = new Dictionary<CharacterBody, float>();
        private static int tick;

        private static readonly BuffDef[] Debuffs =
        {
            RoR2Content.Buffs.Slow50, RoR2Content.Buffs.Cripple, RoR2Content.Buffs.HealingDisabled, RoR2Content.Buffs.BeetleJuice
        };

        internal static void Init()
        {
            Ticker.Every(1f, Tick);
        }

        private static void Tick()
        {
            var teleporter = TeleporterInteraction.instance;
            var tier = Dlc.TeamHighest(DLC4Content.Items.OutOfTPRadius);
            if (!teleporter || !teleporter.isCharging || tier == QualityTier.None || !teleporter.holdoutZoneController)
            {
                outside.Clear();
                return;
            }
            tick++;
            foreach (var member in TeamComponent.GetTeamMembers(TeamIndex.Player))
            {
                var body = member ? member.body : null;
                if (!body || !body.healthComponent || !body.healthComponent.alive || !body.isPlayerControlled) continue;
                if (teleporter.holdoutZoneController.IsBodyInChargingRadius(body)) { outside.Remove(body); continue; }
                float seconds;
                outside.TryGetValue(body, out seconds);
                outside[body] = seconds + 1f;
                var health = body.healthComponent;
                health.TakeDamage(new DamageInfo
                {
                    damage = health.fullCombinedHealth * DamagePerSecond[tier] * Mathf.Pow(1f + Growth, Mathf.Floor(seconds / 10f)),
                    position = body.corePosition,
                    procCoefficient = 0f,
                    damageColorIndex = DamageColorIndex.Void,
                    damageType = DamageType.BypassArmor | DamageType.BypassBlock
                });
                if (tick % 3 != 0) continue;
                if (tier >= QualityTier.Epic) body.AddTimedBuff(Debuffs[Random.Range(0, Debuffs.Length)], 3f);
                if (tier == QualityTier.Legendary && Util.CheckRoll(ExecuteChance)) health.Suicide();
            }
        }
    }

    // Hex of Rain: difficulty rises faster (Legendary: +100%).
    internal static class HexOfRain
    {
        // Each quality copy counts as this many 25% stacks: 30% = 1.2, 35% = 1.4, 45% = 1.8, 100% = 4.
        private static readonly T4 Stacks = new T4(1.2f, 1.4f, 1.8f, 4f);

        internal static void Init()
        {
            Hooks.Transpile(typeof(Run), "RecalculateDifficultyCoefficentInternal", typeof(HexOfRain), nameof(Patch));
        }

        private static float Adjust(float count)
        {
            foreach (var player in PlayerCharacterMasterController.instances)
            {
                var counts = Q.Counts(player ? player.master : null, DLC4Content.Items.IncreasedDifficulty);
                count += Q.Sum(counts, Stacks) - counts.TotalQualityCount;
            }
            return count;
        }

        private static IEnumerable<CodeInstruction> Patch(IEnumerable<CodeInstruction> instructions)
        {
            var total = AccessTools.Method(typeof(Util), nameof(Util.GetTotalItemCountFromTeam), new[] { typeof(Util.GetTotalItemCountFromTeamArgs) });
            var list = instructions.ToList();
            for (var i = 0; i < list.Count - 1; i++)
            {
                if (list[i].Calls(total) && list[i + 1].opcode == OpCodes.Conv_R4)
                {
                    list.Insert(i + 2, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(HexOfRain), nameof(Adjust))));
                    break;
                }
            }
            return list;
        }
    }

    // Hex of Strife: a chance of one more Mountain effect; Legendary always adds two.
    internal static class HexOfStrife
    {
        private static readonly T4 ExtraChance = new T4(10f, 25f, 50f, 100f);
        private static readonly ConditionalWeakTable<TeleporterInteraction, StrongBox<int>> extra = new ConditionalWeakTable<TeleporterInteraction, StrongBox<int>>();

        internal static void Init()
        {
            Hooks.Patch(typeof(TeleporterInteraction), "RecalculateHexMountainCount", typeof(HexOfStrife), postfix: nameof(After));
        }

        private static void After(TeleporterInteraction __instance)
        {
            if (!NetworkServer.active) return;
            var tier = Dlc.TeamHighest(DLC4Content.Items.HexMountain);
            if (tier == QualityTier.None) return;
            var box = extra.GetValue(__instance, _ => new StrongBox<int>(-1));
            if (box.Value < 0)
            {
                box.Value = tier == QualityTier.Legendary ? 2 : Util.CheckRoll(ExtraChance[tier]) ? 1 : 0;   // rolled once per Teleporter
            }
            __instance.hexBonusStacks += box.Value;
        }
    }
}
