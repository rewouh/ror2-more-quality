using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using ItemQualities;
using R2API;
using RoR2;
using RoR2.Items;
using RoR2.Orbs;
using RoR2.Projectile;
using UnityEngine;
using UnityEngine.Networking;

namespace MoreQuality.Hallowed
{
    // Corpse Lily: enemies that die inside the cloud feed the Teleporter and burst into more Blight.
    internal static class CorpseLily
    {
        private static readonly T4 ChargePerKill = new T4(0.005f, 0.01f, 0.025f, 0.05f);
        private static readonly T4 BurstRadius = new T4(3f, 5f, 8f, 12f);
        private static readonly List<BlightZoneController> zones = new List<BlightZoneController>();
        private static readonly AccessTools.FieldRef<BlightZoneController, float> radius = AccessTools.FieldRefAccess<BlightZoneController, float>("_radius");
        private static readonly AccessTools.FieldRef<BlightZoneController, TeamIndex> team = AccessTools.FieldRefAccess<BlightZoneController, TeamIndex>("_teamIndex");

        internal static void Init()
        {
            Hooks.Patch(typeof(BlightZoneController), "OnEnable", typeof(CorpseLily), postfix: nameof(Added));
            Hooks.Patch(typeof(BlightZoneController), "OnDestroy", typeof(CorpseLily), postfix: nameof(Removed));
            GlobalEventManager.onCharacterDeathGlobal += OnDeath;
        }

        private static void Added(BlightZoneController __instance) { zones.Add(__instance); }
        private static void Removed(BlightZoneController __instance) { zones.Remove(__instance); }

        private static void OnDeath(DamageReport report)
        {
            var victim = report.victimBody;
            if (!NetworkServer.active || !victim || zones.Count == 0) return;
            var tier = Dlc.TeamHighest(DLC4Content.Items.BlightZone);
            if (tier == QualityTier.None) return;
            foreach (var zone in zones)
            {
                if (!zone || team(zone) == victim.teamComponent.teamIndex) continue;
                if ((victim.corePosition - zone.transform.position).sqrMagnitude > radius(zone) * radius(zone)) continue;
                var holdout = zone.holdoutZoneController;
                if (holdout && holdout.charge < 0.99f) holdout.Network_charge = Mathf.Min(0.99f, holdout.charge + ChargePerKill[tier]);
                var holder = Dlc.TeamHolder(DLC4Content.Items.BlightZone, tier);
                foreach (var enemy in Enemies.Near(victim.corePosition, BurstRadius[tier], team(zone)))
                {
                    DotController.InflictDot(enemy.gameObject, holder ? holder.gameObject : zone.gameObject, enemy.body.mainHurtBox, DotController.DotIndex.Blight, 5f, 1f);
                }
                return;
            }
        }
    }

    // Dream Crucible: a chance to save the item without being used up itself, and a consumed one restores
    // itself after a while.
    internal static class DreamCrucible
    {
        private static readonly T4 FreeChance = new T4(10f, 20f, 50f, 90f);
        private static readonly T4 RestoreDelay = new T4(600f, 420f, 240f, 120f);
        private static CharacterBody freeCooldown;
        private struct Used { public CharacterMaster Master; public QualityTier Tier; public float Due; }
        private static readonly List<Used> used = new List<Used>();

        internal static void Init()
        {
            Hooks.Patch(typeof(LowHealthOrbSpawnerBehavior), "ProcessRestoration", typeof(DreamCrucible), prefix: nameof(Before), postfix: nameof(After));
            Hooks.Patch(typeof(CharacterMaster), "TryLowHealthOrbSpawnerRegen", typeof(DreamCrucible), postfix: nameof(StageRegen));
            Ticker.Every(1f, RestoreTick);
            Run.onRunStartGlobal += _ => used.Clear();
        }

        private static bool Before(LowHealthOrbSpawnerBehavior __instance, object[] __args)
        {
            var body = __instance.body;
            var counts = Q.Counts(body, DLC4Content.Items.LowHealthOrbSpawner);
            if (!Q.Any(counts) || !body.inventory) return true;
            var info = Traverse.Create(__args[0]);
            var free = Util.CheckRoll(Q.Stack(counts, FreeChance), body.master);
            if (info.Field("buffDef").GetValue<BuffDef>() != null)
            {
                if (free) freeCooldown = body;   // the game puts it on cooldown; After() lifts it
                return true;
            }

            var taken = info.Field("transformResult").GetValue<Inventory.ItemTransformation.TryTransformResult>();
            if (taken.totalTransformed <= 0) return true;
            var inventory = body.inventory;
            if (!free)
            {
                if (inventory.GetItemCountPermanent(DLC4Content.Items.LowHealthOrbSpawner) > 0) return true;   // the game consumes a normal one
                var tier = Dlc.LowestPermanent(inventory, DLC4Content.Items.LowHealthOrbSpawner);
                if (Dlc.Convert(inventory, DLC4Content.Items.LowHealthOrbSpawner, DLC4Content.Items.LowHealthOrbSpawnerConsumed, tier, 1) == 0) return true;
                used.Add(new Used { Master = body.master, Tier = tier, Due = Run.instance.GetRunStopwatch() + RestoreDelay[tier] });
            }
            new Inventory.ItemTransformation
            {
                originalItemIndex = taken.givenItem.itemIndex,
                newItemIndex = taken.takenItem.itemIndex,
                minToTransform = 1,
                maxToTransform = taken.totalTransformed,
                transformationType = (ItemTransformationTypeIndex)0
            }.TryTransform(inventory, out _);
            if (LowHealthOrbSpawnerBehavior.EffectPrefab)
            {
                var effect = new EffectData { origin = body.corePosition };
                effect.SetHurtBoxReference(body.mainHurtBox);
                EffectManager.SpawnEffect(LowHealthOrbSpawnerBehavior.EffectPrefab, effect, true);
            }
            return false;
        }

        private static void After(LowHealthOrbSpawnerBehavior __instance)
        {
            if (freeCooldown == null) return;
            if (freeCooldown == __instance.body) freeCooldown.ClearTimedBuffs(DLC4Content.Buffs.LowHealthOrbSpawnerCooldown);
            freeCooldown = null;
        }

        private static void StageRegen(CharacterMaster __instance)
        {
            for (var tier = QualityTier.Uncommon; tier < QualityTier.Count; tier++)
            {
                Dlc.Convert(__instance.inventory, DLC4Content.Items.LowHealthOrbSpawnerConsumed, DLC4Content.Items.LowHealthOrbSpawner, tier, int.MaxValue);
            }
            used.RemoveAll(e => e.Master == __instance);
        }

        private static void RestoreTick()
        {
            var now = Run.instance.GetRunStopwatch();
            for (var i = used.Count - 1; i >= 0; i--)
            {
                var entry = used[i];
                if (!entry.Master) { used.RemoveAt(i); continue; }
                if (now < entry.Due) continue;
                used.RemoveAt(i);
                Dlc.Convert(entry.Master.inventory, DLC4Content.Items.LowHealthOrbSpawnerConsumed, DLC4Content.Items.LowHealthOrbSpawner, entry.Tier, 1);
            }
        }
    }

    // Earthbound Vessel: every unused jump also grants stats. Grounded: while you stand on the ground, your
    // unused jumps shake it, slowing enemies around you.
    internal static class EarthboundVessel
    {
        private static readonly T4 PerJump = new T4(0.02f, 0.04f, 0.08f, 0.15f);   // movement speed, attack speed, damage
        private static readonly T4 CritPerJump = new T4(1f, 2f, 4f, 8f);
        private static readonly T4 RegenPerJump = new T4(0.25f, 0.5f, 1.25f, 2.5f);
        private static readonly T4 QuakeRadius = new T4(8f, 10f, 14f, 20f);
        private static readonly T4 SlowPerJump = new T4(4f, 8f, 15f, 25f);   // percent

        internal static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += Stats;
            Ticker.Every(0.25f, Quake);
        }

        private static float Scale(CharacterBody body, ItemQualityCounts counts)
        {
            return body.GetBuffCount(DLC4Content.Buffs.RechargeableExtraJump) * (1f + 0.5f * (counts.TotalQualityCount - 1));
        }

        private static void Stats(CharacterBody body, RecalculateStatsAPI.StatHookEventArgs args)
        {
            var counts = Q.Counts(body, DLC4Content.Items.RechargeableJump);
            var tier = Q.Highest(counts);
            if (tier == QualityTier.None) return;
            var scale = Scale(body, counts);
            if (scale <= 0f) return;
            args.moveSpeedMultAdd += PerJump[tier] * scale;
            args.attackSpeedMultAdd += PerJump[tier] * scale;
            args.damageMultAdd += PerJump[tier] * scale;
            args.critAdd += CritPerJump[tier] * scale;
            args.baseRegenAdd += RegenPerJump[tier] * scale;
        }

        private static void Quake()
        {
            foreach (var body in CharacterBody.readOnlyInstancesList)
            {
                if (!body || !body.characterMotor || !body.characterMotor.isGrounded) continue;
                var counts = Q.Counts(body, DLC4Content.Items.RechargeableJump);
                var tier = Q.Highest(counts);
                if (tier == QualityTier.None) continue;
                var slow = Mathf.RoundToInt(Mathf.Min(90f, SlowPerJump[tier] * Scale(body, counts)));
                if (slow <= 0) continue;
                foreach (var enemy in Enemies.Near(body.footPosition, QuakeRadius[tier], body.teamComponent.teamIndex))
                {
                    CustomBuffs.Mark(enemy.body, CustomBuffs.Slowed, slow, 0.5f);
                }
            }
        }
    }

    // Infernal Mark: brands sooner and longer; you get stronger for every enemy you have branded.
    // Enough enemies branded at once are all executed, and branded enemies take more damage from their allies.
    internal static class InfernalMark
    {
        private static readonly T4 HitsNeeded = new T4(8f, 7f, 5f, 3f);
        private static readonly T4 ExtraDuration = new T4(1f, 2f, 4f, 8f);
        private static readonly T4 BonusPerBrand = new T4(2f, 4f, 10f, 20f);
        private static readonly T4 ExecuteAt = new T4(8f, 7f, 5f, 4f);
        private static readonly T4 AllyDamage = new T4(0.25f, 0.5f, 1.25f, 2.5f);

        internal static void Init()
        {
            Hooks.Patch(typeof(CausticPheromonesBehavior), "OnServerDamageDealt", typeof(InfernalMark), prefix: nameof(Before), postfix: nameof(After));
            Ticker.Every(0.5f, UpdateFervor);
            DamageHooks.Add(AlliesHitHarder);
        }

        private static void Before(DamageReport damageReport, out bool __state)
        {
            __state = damageReport.victimBody && damageReport.victimBody.HasBuff(DLC4Content.Buffs.MalignantAttractionActive);
        }

        private static void After(CausticPheromonesBehavior __instance, DamageReport damageReport, bool __state)
        {
            var body = __instance.body;
            var victim = damageReport.victimBody;
            if (!victim || damageReport.attackerBody != body) return;
            var counts = Q.Counts(body, DLC4Content.Items.CausticPheromones);
            var tier = Q.Highest(counts);
            if (tier == QualityTier.None) return;
            var duration = 5f + 5f * (__instance.stack - 1) + Q.Stack(counts, ExtraDuration);

            var branded = victim.HasBuff(DLC4Content.Buffs.MalignantAttractionActive);
            if (!branded && victim.GetBuffCount(DLC4Content.Buffs.MalignantAttraction) >= HitsNeeded[tier])
            {
                victim.SetBuffCount(DLC4Content.Buffs.MalignantAttraction.buffIndex, 0);
                var marker = victim.GetComponent<MalignantAttractionMarker>() ?? victim.gameObject.AddComponent<MalignantAttractionMarker>();
                marker.attackerBody = body;
                marker.attackerTeamIndex = damageReport.attackerTeamIndex;
                marker.attackerMaster = damageReport.attackerMaster;
                branded = true;
            }
            if (!branded || __state) return;
            victim.AddTimedBuff(DLC4Content.Buffs.MalignantAttractionActive, duration);   // newly branded: the longer duration

            var mine = BrandedBy(body);
            if (mine.Count < ExecuteAt[tier]) return;
            foreach (var enemy in mine) Dlc.Execute(enemy.healthComponent, body);
        }

        private static List<CharacterBody> BrandedBy(CharacterBody owner)
        {
            var result = new List<CharacterBody>();
            foreach (var body in CharacterBody.readOnlyInstancesList)
            {
                if (!body || !body.HasBuff(DLC4Content.Buffs.MalignantAttractionActive) || !body.healthComponent || !body.healthComponent.alive) continue;
                var marker = body.GetComponent<MalignantAttractionMarker>();
                if (marker && marker.attackerBody == owner) result.Add(body);
            }
            return result;
        }

        private static void UpdateFervor()
        {
            var counts = new Dictionary<CharacterBody, int>();
            foreach (var body in CharacterBody.readOnlyInstancesList)
            {
                if (!body || !body.HasBuff(DLC4Content.Buffs.MalignantAttractionActive)) continue;
                var marker = body.GetComponent<MalignantAttractionMarker>();
                if (!marker || !marker.attackerBody) continue;
                int n;
                counts.TryGetValue(marker.attackerBody, out n);
                counts[marker.attackerBody] = n + 1;
            }
            foreach (var player in PlayerCharacterMasterController.instances)
            {
                var body = player && player.master ? player.master.GetBody() : null;
                if (!body) continue;
                int n;
                counts.TryGetValue(body, out n);
                var stacks = Mathf.RoundToInt(n * Q.Stack(Q.Counts(body, DLC4Content.Items.CausticPheromones), BonusPerBrand));
                if (body.GetBuffCount(CustomBuffs.InfernalFervor) != stacks) body.SetBuffCount(CustomBuffs.InfernalFervor.buffIndex, stacks);
            }
        }

        private static void AlliesHitHarder(HealthComponent victim, DamageInfo damageInfo, CharacterBody attacker)
        {
            var body = victim.body;
            if (!body || !body.HasBuff(DLC4Content.Buffs.MalignantAttractionActive) || attacker.teamComponent.teamIndex != body.teamComponent.teamIndex) return;
            var marker = body.GetComponent<MalignantAttractionMarker>();
            if (marker) damageInfo.damage *= 1f + Q.Stack(Q.Counts(marker.attackerBody, DLC4Content.Items.CausticPheromones), AllyDamage);
        }
    }

    // Nightmare Demon: bigger demons with a stronger explosion. When a haunted enemy dies before its
    // demon exploded, the demon can jump to a nearby enemy, keeping its charge.
    internal static class NightmareDemon
    {
        private static readonly T4 JumpChance = new T4(25f, 40f, 70f, 100f);
        private static readonly T4 DamageMult = new T4(1.25f, 1.5f, 2.5f, 4f);
        private static readonly T4 Size = new T4(1.25f, 1.5f, 2f, 3f);
        private const float JumpRange = 30f;
        private static GameObject demonPrefab;
        private static bool jumping;
        private static readonly AccessTools.FieldRef<DreamDemonProjectileController, float> charge = AccessTools.FieldRefAccess<DreamDemonProjectileController, float>("storedDamageCoefficient");
        private static readonly Dictionary<HealthComponent, float> pendingCharge = new Dictionary<HealthComponent, float>();
        private static readonly ConditionalWeakTable<DreamDemonProjectileController, object> scaled = new ConditionalWeakTable<DreamDemonProjectileController, object>();

        internal static void Init()
        {
            // Runs before the demon's own reaction to its host dying (it detaches, or dies if barely charged).
            Hooks.Patch(typeof(DreamDemonProjectileController), "OnCharacterDeath", typeof(NightmareDemon), prefix: nameof(HostDied));
            Hooks.OnLoad("Nightmare Demon", () => demonPrefab = DemonPrefab());
            Hooks.Patch(typeof(ProjectileManager), nameof(ProjectileManager.FireProjectile), typeof(NightmareDemon), prefix: nameof(StrongerDemon), args: new[] { typeof(FireProjectileInfo) });
            Hooks.Patch(typeof(DreamDemonProjectileController), "FixedUpdate", typeof(NightmareDemon), prefix: nameof(DemonUpdate));
        }

        private static GameObject DemonPrefab()
        {
            return (GameObject)AccessTools.Field(typeof(DreamDemonBehavior), "dreamDemonProjectilePrefab").GetValue(null);
        }

        private static QualityTier OwnerTier(GameObject owner)
        {
            return owner ? Q.Highest(Q.Counts(owner.GetComponent<CharacterBody>(), DLC4Content.Items.DreamDemon)) : QualityTier.None;
        }

        private static void StrongerDemon(ref FireProjectileInfo fireProjectileInfo)
        {
            if (jumping || !demonPrefab || fireProjectileInfo.projectilePrefab != demonPrefab) return;
            var tier = OwnerTier(fireProjectileInfo.owner);
            if (tier != QualityTier.None) fireProjectileInfo.damage *= Q.StackMultiplier(Q.Counts(fireProjectileInfo.owner.GetComponent<CharacterBody>(), DLC4Content.Items.DreamDemon), DamageMult);
        }

        // Runs on every game: grows quality demons once, and hands a jumped demon its charge back.
        private static void DemonUpdate(DreamDemonProjectileController __instance)
        {
            object done;
            if (!scaled.TryGetValue(__instance, out done))
            {
                scaled.Add(__instance, null);
                var projectile = __instance.GetComponent<ProjectileController>();
                var tier = OwnerTier(projectile ? projectile.owner : null);
                if (tier != QualityTier.None) __instance.transform.localScale *= Size[tier];
            }
            if (!NetworkServer.active || pendingCharge.Count == 0 || !__instance.targetHurtBox) return;
            float stored;
            var target = __instance.targetHurtBox.healthComponent;
            if (!target || !pendingCharge.TryGetValue(target, out stored)) return;
            pendingCharge.Remove(target);
            if (stored > charge(__instance)) __instance.NetworkstoredDamageCoefficient = stored;
        }

        // Returning false skips the game's handler: the demon has left for a new host.
        private static bool HostDied(DreamDemonProjectileController __instance, DamageReport damageReport)
        {
            var demon = __instance;
            var victim = damageReport.victimBody;
            if (!NetworkServer.active || !victim || demon.attachedToBody != victim) return true;
            if (!demonPrefab) demonPrefab = DemonPrefab();
            {
                var projectile = demon.GetComponent<ProjectileController>();
                var owner = projectile && projectile.owner ? projectile.owner.GetComponent<CharacterBody>() : null;
                var tier = Q.Highest(Q.Counts(owner, DLC4Content.Items.DreamDemon));
                if (tier == QualityTier.None || !Util.CheckRoll(Q.Stack(Q.Counts(owner, DLC4Content.Items.DreamDemon), JumpChance), owner.master)) return true;
                var next = Enemies.Near(victim.corePosition, JumpRange, owner.teamComponent.teamIndex, victim.healthComponent).FirstOrDefault();
                if (!next || !next.body.mainHurtBox || !demonPrefab) return true;

                pendingCharge[next] = charge(demon);
                var damage = demon.GetComponent<ProjectileDamage>();
                jumping = true;   // its damage already includes the quality bonus
                try
                {
                    ProjectileManager.instance.FireProjectile(new FireProjectileInfo
                    {
                        projectilePrefab = demonPrefab,
                        position = demon.transform.position,
                        rotation = Util.QuaternionSafeLookRotation(next.body.corePosition - demon.transform.position),
                        owner = owner.gameObject,
                        damage = damage ? damage.damage : owner.damage * 6f * DamageMult[tier],
                        crit = damage && damage.crit,
                        damageColorIndex = DamageColorIndex.Item,
                        target = next.body.mainHurtBox.gameObject,
                        speedOverride = 15f
                    });
                }
                finally { jumping = false; }
                NetworkServer.Destroy(demon.gameObject);
                return false;
            }
        }
    }

    // Ritual Helmet: skulls stun the enemies they hit, longer the faster you were going.
    internal static class RitualHelmet
    {
        private static readonly T4 BaseStun = new T4(0.4f, 0.8f, 2f, 4f);
        private const float MaxStun = 6f;

        internal static void Init()
        {
            GlobalEventManager.onServerDamageDealt += OnDamage;
        }

        private static void OnDamage(DamageReport report)
        {
            var attacker = report.attackerBody;
            var victim = report.victimBody;
            if (!attacker || !victim || (report.damageInfo.damageType.damageTypeExtended & DamageTypeExtended.SprintDamage) == 0) return;
            var tier = Q.Highest(Q.Counts(attacker, DLC4Content.Items.SprintDamage));
            if (tier == QualityTier.None) return;
            var setState = victim.GetComponent<SetStateOnHurt>();
            if (!setState || !setState.canBeStunned) return;
            var sprintSpeed = attacker.sprintingSpeedMultiplier * attacker.baseMoveSpeed;
            var speedRatio = sprintSpeed > 0f ? Mathf.Max(1f, attacker.moveSpeed / sprintSpeed) : 1f;
            setState.SetStun(Mathf.Min(MaxStun, Q.Stack(Q.Counts(attacker, DLC4Content.Items.SprintDamage), BaseStun) * speedRatio));
        }
    }

    // Sanguine Chalice: more bleed, more healing per bleed tick. Blood offering: bleed healing you get at full
    // health is stored as blood, and starting to sprint releases it as a wave that hurts and bleeds enemies.
    internal static class SanguineChalice
    {
        private static readonly T4 ExtraBleedChance = new T4(5f, 10f, 25f, 50f);
        private static readonly T4 ExtraHealPerTick = new T4(0.001f, 0.002f, 0.005f, 0.01f);   // on top of the base 0.5%
        private static readonly T4 MaxBlood = new T4(0.5f, 0.75f, 1.25f, 2f);      // of max health
        private static readonly T4 WaveDamage = new T4(2f, 4f, 10f, 20f);           // of the blood stored
        private static readonly T4 WaveRadius = new T4(12f, 15f, 20f, 28f);

        private static readonly Dictionary<CharacterBody, float> blood = new Dictionary<CharacterBody, float>();
        private static readonly HashSet<CharacterBody> sprinting = new HashSet<CharacterBody>();

        internal static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += (body, args) =>
            {
                args.bleedChanceAdd += Q.Stack(Q.Counts(body, DLC4Content.Items.SanguineChalice), ExtraBleedChance);
            };
            GlobalEventManager.onServerDamageDealt += OnDamage;
            Ticker.Every(0.1f, Release);
            Run.onRunStartGlobal += _ => { blood.Clear(); sprinting.Clear(); };
        }

        private static void Release()
        {
            foreach (var body in blood.Keys.ToList())
            {
                if (!body) { blood.Remove(body); sprinting.Remove(body); continue; }
                var wasSprinting = sprinting.Contains(body);
                if (body.isSprinting) sprinting.Add(body); else sprinting.Remove(body);
                var stored = blood[body];
                if (!body.isSprinting || wasSprinting || stored <= 0f) continue;
                var counts = Q.Counts(body, DLC4Content.Items.SanguineChalice);
                if (!Q.Any(counts)) continue;
                var radius = Q.Stack(counts, WaveRadius);
                blood[body] = 0f;
                body.SetBuffCount(CustomBuffs.BloodOffering.buffIndex, 0);
                new BlastAttack
                {
                    attacker = body.gameObject,
                    inflictor = Sources.Of(DLC4Content.Items.SanguineChalice),
                    teamIndex = body.teamComponent.teamIndex,
                    position = body.corePosition,
                    radius = radius,
                    baseDamage = stored * Q.Stack(counts, WaveDamage),
                    crit = body.RollCrit(),
                    procCoefficient = 1f,
                    damageColorIndex = DamageColorIndex.Bleed,
                    damageType = DamageType.BleedOnHit,
                    falloffModel = BlastAttack.FalloffModel.None,
                    attackerFiltering = AttackerFiltering.NeverHitSelf
                }.Fire();
                Visuals.Spawn(Fx.BloodWave, body.footPosition + Vector3.up * 0.3f, radius);
            }
        }

        private static void OnDamage(DamageReport report)
        {
            var attacker = report.attackerBody;
            if (!attacker || report.dotType != DotController.DotIndex.Bleed || !attacker.healthComponent) return;
            var counts = Q.Counts(attacker, DLC4Content.Items.SanguineChalice);
            var tier = Q.Highest(counts);
            if (tier == QualityTier.None) return;
            var health = attacker.healthComponent;
            if (health.health >= health.fullHealth)
            {
                // Already full: the whole tick's healing (the game's 0.5% per stack plus ours) is stored as blood.
                float stored;
                blood.TryGetValue(attacker, out stored);
                stored = Mathf.Min(health.fullHealth * Q.Stack(counts, MaxBlood), stored + health.fullHealth * (Q.Stack(counts, ExtraHealPerTick) + 0.005f * counts.TotalCount));
                blood[attacker] = stored;
                var percent = Mathf.RoundToInt(stored / health.fullHealth * 100f);
                if (attacker.GetBuffCount(CustomBuffs.BloodOffering) != percent) attacker.SetBuffCount(CustomBuffs.BloodOffering.buffIndex, percent);
                return;
            }
            health.Heal(health.fullHealth * Q.Stack(counts, ExtraHealPerTick), default(ProcChainMask));
        }
    }

    // Stained Scimitar: bigger, stronger blades that can execute.
    internal static class StainedScimitar
    {
        private static readonly T4 Size = new T4(1.5f, 2f, 3f, 5f);
        private static readonly T4 Damage = new T4(1.5f, 2f, 3.5f, 6f);
        private static readonly T4 ExecuteChance = new T4(1f, 2f, 5f, 10f);
        private static readonly GameObject[] blades = new GameObject[4];
        private static readonly GameObject[] thrown = new GameObject[4];
        private static readonly System.Reflection.FieldInfo bladePrefab = AccessTools.Field(typeof(GhostBladesBehavior), "ghostBladePrefab");
        private static readonly System.Reflection.FieldInfo thrownPrefab = AccessTools.Field(typeof(GhostBladesBehavior), "thrownBladePrefab");
        private static readonly System.Reflection.FieldInfo damageMult = AccessTools.Field(typeof(GhostBladesBehavior), "damageMult");

        private struct Saved { public GameObject Blade, Thrown; public float Damage; }

        internal static void Init()
        {
            ContentProvider.AddBuilder(Build);
            Hooks.Patch(typeof(GhostBladesBehavior), "SpawnGhostBlade", typeof(StainedScimitar), prefix: nameof(Swap), postfix: nameof(Restore));
            Hooks.Patch(typeof(GhostBladesBehavior), "OnSkillActivatedAuthority", typeof(StainedScimitar), prefix: nameof(Swap), postfix: nameof(Restore));
            GlobalEventManager.onServerDamageDealt += OnDamage;
        }

        private static void Build(RoR2.ContentManagement.ContentPack pack)
        {
            var blade = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>("7322865710b5ebc458ec418387ce27b1").WaitForCompletion();
            var throwBlade = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>("63ff64f08338e0345957935286168f2d").WaitForCompletion();
            for (var tier = QualityTier.Uncommon; tier < QualityTier.Count; tier++)
            {
                blades[(int)tier] = Scaled(blade, "MoreQualityGhostBlade" + tier, Size[tier]);
                thrown[(int)tier] = Scaled(throwBlade, "MoreQualityThrownBlade" + tier, Size[tier]);
                if (blades[(int)tier]) pack.projectilePrefabs.Add(new[] { blades[(int)tier] });
                if (thrown[(int)tier]) pack.projectilePrefabs.Add(new[] { thrown[(int)tier] });
            }
        }

        private static GameObject Scaled(GameObject source, string name, float size)
        {
            if (!source) return null;
            var clone = PrefabAPI.InstantiateClone(source, name, true);
            Sources.Name(clone.name, DLC4Content.Items.GhostBlades);
            clone.transform.localScale *= size;
            var controller = clone.GetComponent<ProjectileController>();
            if (controller && controller.ghostPrefab)
            {
                var ghost = PrefabAPI.InstantiateClone(controller.ghostPrefab, name + "Ghost", false);
                ghost.transform.localScale *= size;
                controller.ghostPrefab = ghost;
            }
            return clone;
        }

        private static void Swap(GhostBladesBehavior __instance, out Saved __state)
        {
            __state = new Saved { Blade = (GameObject)bladePrefab.GetValue(null), Thrown = (GameObject)thrownPrefab.GetValue(null), Damage = (float)damageMult.GetValue(null) };
            var tier = Q.Highest(Q.Counts(__instance.body, DLC4Content.Items.GhostBlades));
            if (tier == QualityTier.None) return;
            if (blades[(int)tier]) bladePrefab.SetValue(null, blades[(int)tier]);
            if (thrown[(int)tier]) thrownPrefab.SetValue(null, thrown[(int)tier]);
            damageMult.SetValue(null, __state.Damage * Q.StackMultiplier(Q.Counts(__instance.body, DLC4Content.Items.GhostBlades), Damage));
        }

        private static void Restore(Saved __state)
        {
            if (__state.Blade) bladePrefab.SetValue(null, __state.Blade);
            if (__state.Thrown) thrownPrefab.SetValue(null, __state.Thrown);
            if (__state.Damage > 0f) damageMult.SetValue(null, __state.Damage);
        }

        private static void OnDamage(DamageReport report)
        {
            var inflictor = report.damageInfo.inflictor;
            if (!inflictor || !report.victim || !inflictor.name.StartsWith("MoreQuality")) return;
            for (var tier = QualityTier.Uncommon; tier < QualityTier.Count; tier++)
            {
                if (!inflictor.name.Contains("Blade" + tier)) continue;
                if (Util.CheckRoll(ExecuteChance[tier], report.attackerMaster)) Dlc.Execute(report.victim, report.attackerBody);
                return;
            }
        }
    }

    // Totem of Avarice: recharges faster; gilded enemies drop extra gold when they die.
    internal static class TotemOfAvarice
    {
        private static readonly T4 Recharge = new T4(9f, 8f, 6f, 4f);
        private static readonly T4 ExtraGold = new T4(0.2f, 0.4f, 1f, 2f);   // of the enemy's gold reward

        internal static void Init()
        {
            CharacterBody.onBuffFirstStackGained += OnBuff;
            GlobalEventManager.onCharacterDeathGlobal += OnDeath;
        }

        private static void OnBuff(CharacterBody body, BuffDef buff)
        {
            if (!NetworkServer.active || buff != DLC4Content.Buffs.MidasTouchCooldown) return;
            var tier = Q.Highest(Q.Counts(body, DLC4Content.Items.GoldenGoose));
            if (tier == QualityTier.None) return;
            RoR2Application.onNextUpdate += () =>
            {
                if (!body) return;
                body.ClearTimedBuffs(DLC4Content.Buffs.MidasTouchCooldown);
                for (var i = 1; i <= Mathf.RoundToInt(Recharge[tier]); i++) body.AddTimedBuff(DLC4Content.Buffs.MidasTouchCooldown, i);
            };
        }

        private static void OnDeath(DamageReport report)
        {
            var victim = report.victimBody;
            var attacker = report.attackerBody;
            if (!NetworkServer.active || !victim || !attacker || !victim.HasBuff(DLC4Content.Buffs.MidasTouchFrozen) || !attacker.master) return;
            var tier = Dlc.TeamHighest(DLC4Content.Items.GoldenGoose);
            if (tier == QualityTier.None) return;
            var rewards = victim.GetComponent<DeathRewards>();
            if (!rewards) return;
            var gold = (uint)Mathf.Max(1f, rewards.goldReward * ExtraGold[tier]);
            attacker.master.GiveMoney(gold);
            EffectManager.SimpleEffect(LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/CoinImpact"), victim.corePosition, Quaternion.identity, true);
        }
    }

    // Vengeful Spirit: a bigger spirit that explodes when it dies, healing allies and hurting enemies.
    // The spirit comes back after a while, and the rage of the dead one stays until the end of the stage,
    // so it builds up.
    internal static class VengefulSpirit
    {
        private static readonly T4 Size = new T4(1.5f, 2f, 3f, 5f);
        private static readonly T4 RegenDelay = new T4(720f, 540f, 300f, 150f);

        private struct Regen { public CharacterMaster Master; public ItemDef Item; public float Due; }
        private static readonly List<Regen> regens = new List<Regen>();
        // Spirits given back this stage on top of the dormant ones, removed again at the next stage.
        private static readonly List<KeyValuePair<CharacterMaster, ItemDef>> extra = new List<KeyValuePair<CharacterMaster, ItemDef>>();
        private static readonly T4 Radius = new T4(12f, 15f, 20f, 30f);
        private static readonly T4 Damage = new T4(4f, 8f, 20f, 40f);
        private static readonly T4 Heal = new T4(0.1f, 0.15f, 0.3f, 0.6f);

        internal static void Init()
        {
            Hooks.Patch(typeof(VengefulSpiritAttachmentController), "OnSpiritSpawnedServer", typeof(VengefulSpirit), postfix: nameof(OnSpawned));
            Hooks.Patch(typeof(VengefulSpiritAttachmentController), "OnSpiritDefeatedServer", typeof(VengefulSpirit), prefix: nameof(BeforeDefeat), postfix: nameof(OnDefeated));
            Hooks.Patch(typeof(CharacterMaster), "TryVengefulSpiritRegen", typeof(VengefulSpirit), postfix: nameof(StageRegen));
            Ticker.Every(1f, RegenTick);
            Run.onRunStartGlobal += _ => { regens.Clear(); extra.Clear(); };
        }

        private static CharacterBody Owner(VengefulSpiritAttachmentController controller)
        {
            var attachment = controller.GetComponent<NetworkedBodyAttachment>();
            return attachment ? attachment.attachedBody : null;
        }

        private static void OnSpawned(VengefulSpiritAttachmentController __instance, SpawnCard.SpawnResult spawnResult)
        {
            var tier = Q.Highest(Q.Counts(Owner(__instance), DLC4Content.Items.VengefulSpirit));
            if (tier == QualityTier.None || !spawnResult.spawnedInstance) return;
            Grow(spawnResult.spawnedInstance.GetComponent<CharacterMaster>(), Q.StackMultiplier(Q.Counts(Owner(__instance), DLC4Content.Items.VengefulSpirit), Size), 30);
        }

        // The spirit's body can appear a few frames after its master, so keep trying for a moment.
        private static void Grow(CharacterMaster master, float size, int triesLeft)
        {
            if (!master || triesLeft <= 0) return;
            var body = master.GetBody();
            var locator = body ? body.modelLocator : null;
            var model = locator ? (locator.modelBaseTransform ? locator.modelBaseTransform : locator.modelTransform) : null;
            if (model) { model.localScale *= size; return; }
            RoR2Application.onNextUpdate += () => Grow(master, size, triesLeft - 1);
        }

        private static void BeforeDefeat(VengefulSpiritAttachmentController __instance, out int __state)
        {
            var owner = Owner(__instance);
            __state = owner && owner.inventory ? owner.inventory.GetItemCountPermanent(DLC4Content.Items.VengefulSpirit) : 0;
        }

        private static void OnDefeated(VengefulSpiritAttachmentController __instance, CharacterMaster spiritMaster, DamageReport damageReport, int __state)
        {
            var owner = Owner(__instance);
            if (!owner || !owner.inventory || (object)damageReport.damageInfo.inflictor == __instance.gameObject) return;
            var counts = Q.Counts(owner, DLC4Content.Items.VengefulSpirit);
            var tier = Q.Highest(counts);
            if (tier == QualityTier.None) return;
            var radius = Q.Stack(counts, Radius);
            // Extra quality spirits bring the dead one back sooner.
            var delay = RegenDelay[tier] / (1f + 0.5f * (counts.TotalQualityCount - 1));

            // The game only turns a normal spirit into its "dormant" form; do the same for a quality one.
            var died = DLC4Content.Items.VengefulSpirit;
            if (owner.inventory.GetItemCountPermanent(DLC4Content.Items.VengefulSpirit) == __state)
            {
                var used = Dlc.LowestPermanent(owner.inventory, DLC4Content.Items.VengefulSpirit);
                if (Dlc.Convert(owner.inventory, DLC4Content.Items.VengefulSpirit, DLC4Content.Items.VengefulSpiritRageMode, used, 1) > 0)
                {
                    died = Dlc.Variant(DLC4Content.Items.VengefulSpirit, used);
                    var rage = owner.GetComponent<VengefulSpiritRageModeBehavior>();
                    if (rage) rage.TryEnqueueDeathEvent(damageReport);
                }
            }
            if (delay > 0f && owner.master)
            {
                regens.Add(new Regen { Master = owner.master, Item = died, Due = Run.instance.GetRunStopwatch() + delay });
            }

            var spirit = spiritMaster ? spiritMaster.GetBody() : null;
            var position = spirit ? spirit.corePosition : owner.corePosition;
            new BlastAttack
            {
                attacker = owner.gameObject,
                inflictor = Sources.Of(DLC4Content.Items.VengefulSpirit),
                teamIndex = owner.teamComponent.teamIndex,
                position = position,
                radius = radius,
                baseDamage = owner.damage * Q.Stack(counts, Damage),
                crit = owner.RollCrit(),
                procCoefficient = 1f,
                damageColorIndex = DamageColorIndex.Item,
                falloffModel = BlastAttack.FalloffModel.None,
                attackerFiltering = AttackerFiltering.NeverHitSelf
            }.Fire();
            foreach (var member in TeamComponent.GetTeamMembers(owner.teamComponent.teamIndex))
            {
                var ally = member ? member.body : null;
                if (ally && ally.healthComponent && (ally.corePosition - position).sqrMagnitude <= radius * radius)
                {
                    ally.healthComponent.HealFraction(Q.Stack(counts, Heal), default(ProcChainMask));
                }
            }
            Visuals.Spawn(Fx.SpiritBurst, position, radius);
        }

        private static void StageRegen(CharacterMaster __instance)
        {
            for (var tier = QualityTier.Uncommon; tier < QualityTier.Count; tier++)
            {
                Dlc.Convert(__instance.inventory, DLC4Content.Items.VengefulSpiritRageMode, DLC4Content.Items.VengefulSpirit, tier, int.MaxValue);
            }
            // Every dormant spirit has just woken up again, so the extra ones given back go away.
            foreach (var given in extra.Where(e => e.Key == __instance).ToList())
            {
                if (__instance.inventory.GetItemCountPermanent(given.Value) > 0) __instance.inventory.RemoveItemPermanent(given.Value, 1);
                extra.Remove(given);
            }
            regens.RemoveAll(r => r.Master == __instance);
        }

        private static void RegenTick()
        {
            var now = Run.instance.GetRunStopwatch();
            for (var i = regens.Count - 1; i >= 0; i--)
            {
                var regen = regens[i];
                if (!regen.Master || !regen.Item) { regens.RemoveAt(i); continue; }
                if (now < regen.Due) continue;
                regens.RemoveAt(i);
                regen.Master.inventory.GiveItemPermanent(regen.Item, 1);   // the dormant one stays: its rage keeps going
                extra.Add(new KeyValuePair<CharacterMaster, ItemDef>(regen.Master, regen.Item));
            }
        }
    }

    // Vile Censer: enemies hit by the tendrils are linked for a second; damage to one is shared with the
    // others. When a linked enemy dies, each other one may be executed.
    internal static class VileCenser
    {
        private static readonly T4 Shared = new T4(0.1f, 0.2f, 0.4f, 0.75f);
        private const float LinkDuration = 1f;
        private static readonly T4 ExecuteChance = new T4(3f, 6f, 15f, 30f);

        private sealed class Link
        {
            public List<HealthComponent> Members = new List<HealthComponent>();
            public CharacterBody Owner;
            public QualityTier Tier;
            public float Until;
        }

        private static readonly Dictionary<HealthComponent, Link> links = new Dictionary<HealthComponent, Link>();
        private static readonly Dictionary<object, Link> byChain = new Dictionary<object, Link>();
        private static bool sharing;

        internal static void Init()
        {
            Hooks.Patch(typeof(VileCenserTendrilOrb), "OnArrival", typeof(VileCenser), postfix: nameof(OnArrival));
            GlobalEventManager.onServerDamageDealt += OnDamage;
            GlobalEventManager.onCharacterDeathGlobal += OnDeath;
            Ticker.Every(1f, Cleanup);
        }

        private static void OnArrival(VileCenserTendrilOrb __instance)
        {
            var target = __instance.target ? __instance.target.healthComponent : null;
            var owner = __instance.attacker ? __instance.attacker.GetComponent<CharacterBody>() : null;
            if (!target || !owner || __instance.ignoredHealthComponents == null) return;
            var tier = Q.Highest(Q.Counts(owner, DLC4Content.Items.VileCenser));
            if (tier == QualityTier.None) return;
            Link link;
            if (!byChain.TryGetValue(__instance.ignoredHealthComponents, out link))
            {
                link = new Link { Owner = owner, Tier = tier };
                byChain[__instance.ignoredHealthComponents] = link;
            }
            link.Until = Time.time + LinkDuration;
            if (!link.Members.Contains(target)) link.Members.Add(target);
            links[target] = link;
        }

        private static Link Active(HealthComponent victim)
        {
            Link link;
            return victim && links.TryGetValue(victim, out link) && link.Until >= Time.time ? link : null;
        }

        private static void OnDamage(DamageReport report)
        {
            if (sharing || report.damageDealt <= 0f) return;
            var link = Active(report.victim);
            if (link == null || !link.Owner) return;
            sharing = true;
            try
            {
                foreach (var other in link.Members.ToList())
                {
                    if (!other || other == report.victim || !other.alive) continue;
                    other.TakeDamage(new DamageInfo
                    {
                        attacker = link.Owner.gameObject,
                        inflictor = Sources.Of(DLC4Content.Items.VileCenser),
                        damage = report.damageDealt * Q.Stack(Q.Counts(link.Owner, DLC4Content.Items.VileCenser), Shared),
                        position = other.body ? other.body.corePosition : other.transform.position,
                        procCoefficient = 0f,
                        damageColorIndex = DamageColorIndex.Item,
                        damageType = DamageType.Silent
                    });
                }
            }
            finally { sharing = false; }
        }

        private static void OnDeath(DamageReport report)
        {
            var link = Active(report.victim);
            if (link == null) return;
            foreach (var other in link.Members.ToList())
            {
                if (other && other != report.victim && other.alive && Util.CheckRoll(Q.Stack(Q.Counts(link.Owner, DLC4Content.Items.VileCenser), ExecuteChance), link.Owner ? link.Owner.master : null)) Dlc.Execute(other, link.Owner);
            }
        }

        private static void Cleanup()
        {
            foreach (var key in links.Where(e => !e.Key || e.Value.Until < Time.time).Select(e => e.Key).ToList()) links.Remove(key);
            foreach (var key in byChain.Where(e => e.Value.Until < Time.time).Select(e => e.Key).ToList()) byChain.Remove(key);
        }
    }
}
