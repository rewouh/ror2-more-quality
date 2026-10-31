using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using ItemQualities;
using RoR2;
using SS2;
using UnityEngine;
using UnityEngine.Networking;

namespace MoreQuality.Items
{
    // Quality equipment doesn't stack: only the tier of the equipment being used counts.

    internal static class Stun
    {
        // Stuns everything a blast hit, for `seconds`.
        public static void Hits(BlastAttack.Result result, float seconds)
        {
            if (result.hitPoints == null) return;
            foreach (var hit in result.hitPoints)
            {
                var health = hit.hurtBox ? hit.hurtBox.healthComponent : null;
                var setState = health ? health.GetComponent<SetStateOnHurt>() : null;
                if (setState && setState.canBeStunned) setState.SetStun(seconds);
            }
        }
    }

    // Longer thrust; at full speed you ram through enemies, stunning them.
    internal static class BackThruster
    {
        private static readonly T4 Duration = new T4(10f, 12f, 16f, 24f);
        private static readonly T4 RamDamage = new T4(3f, 6f, 15f, 30f);
        private static readonly T4 RamStun = new T4(0.5f, 1f, 2f, 4f);
        private const float RamCooldown = 0.75f;
        private static readonly ConditionalWeakTable<CharacterBody, object> lastRam = new ConditionalWeakTable<CharacterBody, object>();

        internal static void Init()
        {
            Hooks.Patch(typeof(SS2.Equipments.BackThruster), "Execute", typeof(BackThruster), prefix: nameof(Before), postfix: nameof(After));
            Hooks.Patch(typeof(SS2.Equipments.BackThruster.BackThrusterBuffBehaviour), "FixedUpdate", typeof(BackThruster), postfix: nameof(Ram));
        }

        private static void Before(EquipmentSlot slot, out float __state)
        {
            __state = SS2.Equipments.BackThruster.thrustDuration;
            var tier = Q.EquipmentTier(slot);
            if (tier == QualityTier.None) return;
            SS2.Equipments.BackThruster.thrustDuration = Duration[tier];
            EquipmentMemory.Remember(slot.characterBody, SS2Content.Equipments.BackThruster, tier);
        }

        private static void After(float __state)
        {
            SS2.Equipments.BackThruster.thrustDuration = __state;
        }

        private static void Ram(SS2.Equipments.BackThruster.BackThrusterBuffBehaviour __instance, float ___thrust)
        {
            if (!NetworkServer.active || ___thrust < SS2.Equipments.BackThruster.speedCap * 0.6f) return;
            var body = __instance.GetComponent<CharacterBody>();
            var use = EquipmentMemory.Recent(body, SS2Content.Equipments.BackThruster, Duration.L + 1f);
            if (use == null) return;

            object last;
            if (lastRam.TryGetValue(body, out last) && Time.fixedTime - (float)last < RamCooldown) return;
            lastRam.Remove(body);
            lastRam.Add(body, Time.fixedTime);

            var result = new BlastAttack
            {
                attacker = body.gameObject,
                inflictor = body.gameObject,
                teamIndex = body.teamComponent.teamIndex,
                position = body.corePosition + body.characterMotor.velocity.normalized * body.radius,
                radius = body.radius + 3f,
                baseDamage = body.damage * RamDamage[use.tier],
                baseForce = 1500f,
                crit = body.RollCrit(),
                procCoefficient = 1f,
                damageColorIndex = DamageColorIndex.Item,
                falloffModel = BlastAttack.FalloffModel.None,
                attackerFiltering = AttackerFiltering.NeverHitSelf
            }.Fire();
            Stun.Hits(result, RamStun[use.tier]);
        }
    }

    // The first hit out of cloak is a guaranteed crit with bonus damage, and so is every other hit in the
    // short window after it.
    internal static class CloakingHeadband
    {
        private static readonly T4 Bonus = new T4(0.5f, 1f, 2.5f, 5f);
        private static readonly T4 Window = new T4(0.2f, 0.4f, 1f, 2f);

        internal static void Init()
        {
            Hooks.Patch(typeof(SS2.Equipments.CloakingHeadband), "Execute", typeof(CloakingHeadband), postfix: nameof(After));
            DamageHooks.Add(FirstStrike);
        }

        private static void After(EquipmentSlot slot)
        {
            var tier = Q.EquipmentTier(slot);
            if (tier != QualityTier.None) EquipmentMemory.Remember(slot.characterBody, SS2Content.Equipments.CloakingHeadband, tier);
        }

        private static void FirstStrike(HealthComponent victim, DamageInfo damageInfo, CharacterBody attacker)
        {
            if (victim.body && victim.body.teamComponent.teamIndex == attacker.teamComponent.teamIndex) return;
            var use = EquipmentMemory.Recent(attacker, SS2Content.Equipments.CloakingHeadband, SS2.Equipments.CloakingHeadband.cloakDuration + Window.L);
            if (use == null) return;
            if (use.spent)
            {
                if (Time.fixedTime - use.firstHit > Window[use.tier]) return;
            }
            else
            {
                use.spent = true;
                use.firstHit = Time.fixedTime;
            }
            damageInfo.crit = true;
            damageInfo.damage *= 1f + Bonus[use.tier];
        }
    }

    // Bigger banners, and more of them at once. Plant the flag: the banner slams into the ground, hurting
    // and stunning enemies around it.
    internal static class GreaterWarbanner
    {
        private static readonly T4 Radius = new T4(0.2f, 0.4f, 1f, 2f);
        private static readonly T4 MaxBanners = new T4(1f, 2f, 3f, 5f);
        private static readonly T4 SlamDamage = new T4(3f, 6f, 15f, 30f);   // of your damage
        private static readonly T4 SlamRadius = new T4(8f, 10f, 14f, 20f);
        private static readonly T4 SlamStun = new T4(1f, 1.5f, 2.5f, 4f);

        internal static void Init()
        {
            Hooks.Patch(typeof(SS2.Equipments.GreaterWarbanner), "Execute", typeof(GreaterWarbanner), prefix: nameof(Before), postfix: nameof(After));
        }

        private static void Before(EquipmentSlot slot, out int __state)
        {
            __state = SS2.Equipments.GreaterWarbanner.maxGreaterBanners;
            var tier = Q.EquipmentTier(slot);
            if (tier != QualityTier.None) SS2.Equipments.GreaterWarbanner.maxGreaterBanners = Mathf.Max(__state, (int)MaxBanners[tier]);
        }

        private static void After(EquipmentSlot slot, int __state)
        {
            SS2.Equipments.GreaterWarbanner.maxGreaterBanners = __state;
            var tier = Q.EquipmentTier(slot);
            var token = slot.characterBody ? slot.characterBody.GetComponent<SS2.Equipments.GreaterWarbanner.GreaterBannerToken>() : null;
            if (tier == QualityTier.None || !token || token.ownedBanners.Count == 0) return;
            var banner = token.ownedBanners[token.ownedBanners.Count - 1];
            var ward = banner ? banner.GetComponent<BuffWard>() : null;
            if (ward) ward.Networkradius = ward.radius * (1f + Radius[tier]);
            if (!banner || !NetworkServer.active) return;

            var body = slot.characterBody;
            var result = new BlastAttack
            {
                attacker = body.gameObject,
                inflictor = banner,
                teamIndex = body.teamComponent.teamIndex,
                position = banner.transform.position,
                radius = SlamRadius[tier],
                baseDamage = body.damage * SlamDamage[tier],
                baseForce = 2000f,
                crit = body.RollCrit(),
                procCoefficient = 1f,
                damageColorIndex = DamageColorIndex.Item,
                falloffModel = BlastAttack.FalloffModel.None,
                attackerFiltering = AttackerFiltering.NeverHitSelf
            }.Fire();
            Stun.Hits(result, SlamStun[tier]);
            Visuals.Spawn(Fx.BannerSlam, banner.transform.position + Vector3.up * 0.3f, SlamRadius[tier]);
        }
    }

    // Each item lying on the ground that the magnet pulls gets one chance, ever, to be upgraded to the
    // magnet's quality.
    // (Starstorm2's "treasure chance" setting is never used by its code, so it couldn't be scaled.)
    internal static class Magnet
    {
        private static readonly T4 UpgradeChance = new T4(5f, 10f, 25f, 50f);
        private static readonly ConditionalWeakTable<GenericPickupController, object> rolled = new ConditionalWeakTable<GenericPickupController, object>();

        // Hexes and Hallowed items only get quality from the DLC's own sources.
        private static bool DlcTierOnly(PickupIndex pickup)
        {
            var def = PickupCatalog.GetPickupDef(pickup);
            var item = def != null ? ItemCatalog.GetItemDef(def.itemIndex) : null;
            return item && (item.tier == ItemTier.Hex || item.tier == ItemTier.Cursed);
        }

        internal static void Init()
        {
            Hooks.Patch(typeof(SS2.Equipments.Magnet), "Execute", typeof(Magnet), postfix: nameof(After));
        }

        private static void After(EquipmentSlot slot)
        {
            var tier = Q.EquipmentTier(slot);
            if (tier == QualityTier.None || !NetworkServer.active) return;
            foreach (var pickup in InstanceTracker.GetInstancesList<GenericPickupController>().ToList())
            {
#pragma warning disable 618
                var current = pickup.pickupIndex;
                object seen;
                if (rolled.TryGetValue(pickup, out seen)) continue;   // one roll per item, ever
                rolled.Add(pickup, null);
                if (QualityCatalog.GetQualityTier(current) != QualityTier.None || DlcTierOnly(current) || !Util.CheckRoll(UpgradeChance[tier], slot.characterBody.master)) continue;
                var upgraded = QualityCatalog.GetPickupIndexOfQuality(current, tier);
                if (upgraded.isValid && upgraded != current) pickup.pickupIndex = upgraded;
#pragma warning restore 618
            }
        }
    }

    // Costs less health and pays more gold. Gold rush: for a few seconds after using it, every kill pays
    // gold based on the health you spent.
    internal static class MIDAS
    {
        private static readonly T4 HealthCost = new T4(0.45f, 0.4f, 0.3f, 0.15f);
        private static readonly T4 GoldMultiplier = new T4(1.25f, 1.5f, 2.25f, 3.5f);
        private static readonly T4 RushDuration = new T4(4f, 6f, 10f, 15f);
        private static readonly T4 RushGold = new T4(0.1f, 0.2f, 0.5f, 1f);   // of the health spent, per kill

        private struct State { public float Health, Gold, HealthBefore; public uint MoneyBefore; }
        private static readonly Dictionary<CharacterBody, uint> rushGold = new Dictionary<CharacterBody, uint>();

        internal static void Init()
        {
            Hooks.Patch(typeof(SS2.Equipments.MIDAS), "Execute", typeof(MIDAS), prefix: nameof(Before), postfix: nameof(After));
            GlobalEventManager.onCharacterDeathGlobal += OnKill;
            Run.onRunStartGlobal += _ => rushGold.Clear();
        }

        private static void OnKill(DamageReport report)
        {
            uint gold;
            var killer = report.attackerBody;
            if (!NetworkServer.active || !killer || !killer.master || !killer.HasBuff(CustomBuffs.GoldRush) || !rushGold.TryGetValue(killer, out gold) || gold == 0) return;
            killer.master.GiveMoney(gold);
            if (report.victimBody) EffectManager.SimpleImpactEffect(LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/ImpactEffects/CoinImpact"), report.victimBody.corePosition, Vector3.up, true);
        }

        private static void Before(EquipmentSlot slot, out State __state)
        {
            __state = new State
            {
                Health = SS2.Equipments.MIDAS.healthPercentage,
                Gold = SS2.Equipments.MIDAS.goldMultiplier,
                MoneyBefore = slot.characterBody && slot.characterBody.master ? slot.characterBody.master.money : 0u,
                HealthBefore = slot.characterBody && slot.characterBody.healthComponent ? slot.characterBody.healthComponent.combinedHealth : 0f
            };
            var tier = Q.EquipmentTier(slot);
            if (tier == QualityTier.None) return;
            SS2.Equipments.MIDAS.healthPercentage = HealthCost[tier];
            SS2.Equipments.MIDAS.goldMultiplier = GoldMultiplier[tier];   // used for AI allies' payouts
        }

        // For players Starstorm2 ignores its own gold multiplier, so the bonus is paid here.
        private static void After(EquipmentSlot slot, State __state)
        {
            SS2.Equipments.MIDAS.healthPercentage = __state.Health;
            SS2.Equipments.MIDAS.goldMultiplier = __state.Gold;
            var tier = Q.EquipmentTier(slot);
            var body = slot.characterBody;
            var master = body ? body.master : null;
            if (tier == QualityTier.None || !master) return;
            var spent = body.healthComponent ? __state.HealthBefore - body.healthComponent.combinedHealth : 0f;
            if (spent > 0f && NetworkServer.active)
            {
                rushGold[body] = (uint)Mathf.Max(1f, spent * RushGold[tier]);
                body.AddTimedBuff(CustomBuffs.GoldRush, RushDuration[tier]);
            }
            if (master.money <= __state.MoneyBefore) return;
            var earned = master.money - __state.MoneyBefore;
            master.GiveMoney((uint)(earned * (GoldMultiplier[tier] - 1f)));
        }
    }

    // A bigger, longer-lasting flag that only silences enemies and heals allies standing near it.
    internal static class WhiteFlag
    {
        private static readonly T4 Radius = new T4(0.2f, 0.4f, 1f, 2f);
        private static readonly T4 Regen = new T4(0.02f, 0.04f, 0.1f, 0.2f);
        private static readonly T4 Duration = new T4(16f, 18f, 22f, 30f);

        private struct State { public float Radius, Duration; }

        private sealed class HealingFlag : MonoBehaviour
        {
            public QualityTier tier;
            public TeamIndex team;
            public BuffWard ward;
            private float timer;

            private void FixedUpdate()
            {
                if (!NetworkServer.active || !ward) return;
                timer -= Time.fixedDeltaTime;
                if (timer > 0f) return;
                timer = 0.5f;
                var radiusSqr = ward.radius * ward.radius;
                foreach (var member in TeamComponent.GetTeamMembers(team))
                {
                    var body = member ? member.body : null;
                    if (!body || !body.healthComponent || (body.corePosition - transform.position).sqrMagnitude > radiusSqr) continue;
                    body.healthComponent.Heal(body.healthComponent.fullHealth * Regen[tier] * 0.5f, default(ProcChainMask));
                }
            }
        }

        internal static void Init()
        {
            Hooks.Patch(typeof(SS2.Equipments.WhiteFlag), "Execute", typeof(WhiteFlag), prefix: nameof(Before), postfix: nameof(After));
        }

        private static void Before(EquipmentSlot slot, out State __state)
        {
            __state = new State { Radius = SS2.Equipments.WhiteFlag.flagRadius, Duration = SS2.Equipments.WhiteFlag.flagDuration };
            var tier = Q.EquipmentTier(slot);
            if (tier == QualityTier.None) return;
            SS2.Equipments.WhiteFlag.flagRadius = __state.Radius * (1f + Radius[tier]);
            SS2.Equipments.WhiteFlag.flagDuration = Duration[tier];
        }

        private static void After(EquipmentSlot slot, State __state)
        {
            SS2.Equipments.WhiteFlag.flagRadius = __state.Radius;
            SS2.Equipments.WhiteFlag.flagDuration = __state.Duration;
            var tier = Q.EquipmentTier(slot);
            var body = slot.characterBody;
            if (tier == QualityTier.None || !body) return;

            // Execute spawns the flag at the user's position; find the one it just made.
            var ward = Object.FindObjectsOfType<BuffWard>()
                .Where(w => w.buffDef == SS2Content.Buffs.BuffSurrender && !w.GetComponent<HealingFlag>())
                .OrderBy(w => (w.transform.position - body.corePosition).sqrMagnitude)
                .FirstOrDefault();
            if (!ward) return;
            var flag = ward.gameObject.AddComponent<HealingFlag>();
            flag.tier = tier;
            flag.team = body.teamComponent.teamIndex;
            flag.ward = ward;
            ward.invertTeamFilter = true;
        }
    }
}
