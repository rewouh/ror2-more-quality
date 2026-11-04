using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using ItemQualities;
using R2API;
using RoR2;
using RoR2.CharacterAI;
using RoR2.Items;
using UnityEngine;
using UnityEngine.Networking;

namespace MoreQuality.Hallowed
{
    // The quality hexes that act on the world around you: thieves, ghosts, mimics, bones and crowns.

    internal static class HexItems
    {
        // Items a hex may take from you: real items, not hexes or hidden helpers.
        public static bool Takeable(ItemIndex index)
        {
            var def = ItemCatalog.GetItemDef(index);
            if (!def || def.hidden || !def.canRemove) return false;
            return def.tier != ItemTier.NoTier && def.tier != ItemTier.Hex && def.tier != ItemTier.Cursed;
        }

        public static void Drop(ItemIndex item, Vector3 position, Vector3 velocity)
        {
            var pickup = PickupCatalog.FindPickupIndex(item);
            if (!pickup.isValid) return;
            PickupDropletController.CreatePickupDroplet(new GenericPickupController.CreatePickupInfo { pickup = new UniquePickup(pickup), position = position }, position, velocity);
        }

        public static void Remove(CharacterMaster master, CharacterBody body)
        {
            if (body) NetworkServer.Destroy(body.gameObject);
            if (master) NetworkServer.Destroy(master.gameObject);
        }
    }

    // Hex of Greed, Pickpockets: a small, non-elite monster close to you can snatch one of your items, then
    // runs away with it. Kill it to get the item back; if it stays far away long enough, it escapes.
    internal static class HexOfGreed
    {
        private static readonly T4 StealChance = new T4(5f, 10f, 25f, 45f);   // per second
        private static readonly T4 FleeSpeed = new T4(0.4f, 0.7f, 1.2f, 2f);
        private static readonly T4 EscapeTime = new T4(8f, 7f, 6f, 5f);
        private static readonly T4 PauseMin = new T4(15f, 12f, 8f, 5f);    // seconds between two thefts from you
        private static readonly T4 PauseMax = new T4(30f, 25f, 20f, 15f);
        private const float Reach = 4f;
        private const float EscapeDistance = 40f;
        private const float Tick = 0.5f;

        private sealed class Thief { public CharacterMaster Victim; public ItemIndex Item; public float Speed, EscapeAfter, Far; }
        private static readonly Dictionary<CharacterBody, Thief> thieves = new Dictionary<CharacterBody, Thief>();
        private static readonly Dictionary<CharacterMaster, float> nextTheft = new Dictionary<CharacterMaster, float>();

        internal static void Init()
        {
            Ticker.Every(Tick, Steal);
            RoR2Application.onFixedUpdate += Flee;
            GlobalEventManager.onCharacterDeathGlobal += Caught;
            RecalculateStatsAPI.GetStatCoefficients += (body, args) =>
            {
                Thief thief;
                if (thieves.TryGetValue(body, out thief)) args.moveSpeedMultAdd += thief.Speed;
            };
            Run.onRunStartGlobal += _ => { thieves.Clear(); nextTheft.Clear(); };
        }

        private static void Steal()
        {
            foreach (var player in PlayerCharacterMasterController.instances)
            {
                var victim = player && player.master ? player.master.GetBody() : null;
                var counts = Q.Counts(victim, DLC4Content.Items.ShareItemsWithEnemies);
                if (!Q.Any(counts) || !victim.healthComponent.alive) continue;
                float next;
                if (nextTheft.TryGetValue(victim.master, out next) && Time.fixedTime < next) continue;
                var candidates = Enemies.Near(victim.corePosition, Reach + victim.radius, victim.teamComponent.teamIndex)
                    .Select(h => h.body)
                    .Where(b => !b.isElite && !b.isBoss && !b.isChampion && b.hullClassification == HullClassification.Human && b.master
                        && !thieves.ContainsKey(b) && !b.HasBuff(CustomBuffs.Phantom))
                    .ToList();
                if (candidates.Count == 0 || !Util.CheckRoll(Q.Stack(counts, StealChance) * Tick, victim.master)) continue;
                var items = victim.inventory.itemAcquisitionOrder.Where(i => HexItems.Takeable(i) && victim.inventory.GetItemCountPermanent(i) > 0).ToList();
                if (items.Count == 0) continue;

                var thiefBody = candidates[Random.Range(0, candidates.Count)];
                var item = items[Random.Range(0, items.Count)];
                victim.inventory.RemoveItemPermanent(item, 1);
                nextTheft[victim.master] = Time.fixedTime + Random.Range(Q.AtHighest(counts, PauseMin), Q.AtHighest(counts, PauseMax));
                thieves[thiefBody] = new Thief { Victim = victim.master, Item = item, Speed = Q.AtHighest(counts, FleeSpeed), EscapeAfter = Q.AtHighest(counts, EscapeTime) };
                foreach (var ai in thiefBody.master.GetComponents<BaseAI>()) ai.enabled = false;
                thiefBody.MarkAllStatsDirty();
                Visuals.SpawnStreak(HexFx.Snatch, victim.corePosition, thiefBody.corePosition);
                Decor.Decorate(thiefBody.gameObject, 1f, new[] { item });
            }
        }

        // With its AI off, the thief only does what its input says: run away from its victim.
        private static void Flee()
        {
            if (!NetworkServer.active || thieves.Count == 0) return;
            foreach (var entry in thieves.ToList())
            {
                var body = entry.Key;
                var thief = entry.Value;
                if (!body) { thieves.Remove(body); continue; }
                var victim = thief.Victim ? thief.Victim.GetBody() : null;
                if (!body.inputBank) continue;
                var away = victim ? body.corePosition - victim.corePosition : body.inputBank.moveVector;
                away.y = 0f;
                body.inputBank.moveVector = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward;
                body.inputBank.aimDirection = body.inputBank.moveVector;
                body.inputBank.sprint.PushState(true);

                var far = !victim || (body.corePosition - victim.corePosition).sqrMagnitude > EscapeDistance * EscapeDistance;
                thief.Far = far ? thief.Far + Time.fixedDeltaTime : 0f;
                if (thief.Far < thief.EscapeAfter) continue;
                thieves.Remove(body);
                Visuals.Spawn(HexFx.PhantomPoof, body.corePosition, body.radius);
                HexItems.Remove(body.master, body);   // gone, and the item with it
            }
        }

        private static void Caught(DamageReport report)
        {
            Thief thief;
            var body = report.victimBody;
            if (!NetworkServer.active || !body || !thieves.TryGetValue(body, out thief)) return;
            thieves.Remove(body);
            HexItems.Drop(thief.Item, body.corePosition + Vector3.up, Vector3.up * 15f);
        }
    }

    // Hex of Haunting, Ghost carriers: items that land near you get grabbed by a wisp that flies them away
    // from you. It's a real Lesser Wisp with its AI off: it can't attack, walls stop it, and you can shoot
    // it (or touch it) to make it drop the item. If you can't catch it in time, it lets go of the item
    // where it is, so nothing is ever lost. Quality makes it grab sooner and fly faster.
    internal static class HexOfHaunting
    {
        private static readonly T4 GrabDelay = new T4(0.6f, 0.4f, 0.2f, 0f);
        private static readonly T4 Speed = new T4(0.75f, 1.5f, 3f, 6f);   // movement speed bonus
        private static readonly T4 Immunity = new T4(1f, 1.5f, 2f, 3f);   // seconds of immunity when it appears
        private const float GrabRange = 40f;
        private const float CarryTime = 20f;
        private const float CatchRange = 3f;
        private const float Hover = 3f;

        private sealed class Ghost { public PickupIndex Pickup; public float Speed, Immunity, DropAt; }

        private static readonly Dictionary<GenericPickupController, float> seen = new Dictionary<GenericPickupController, float>();
        private static readonly HashSet<GenericPickupController> spared = new HashSet<GenericPickupController>();
        private static readonly Dictionary<CharacterBody, Ghost> ghosts = new Dictionary<CharacterBody, Ghost>();
        private static readonly List<KeyValuePair<Vector3, float>> releases = new List<KeyValuePair<Vector3, float>>();
        private static GameObject wispMaster;

        internal static void Init()
        {
            Ticker.Every(0.1f, Grab);
            RoR2Application.onFixedUpdate += Carry;
            GlobalEventManager.onCharacterDeathGlobal += Shot;
            DamageHooks.AddIncoming(Harmless);
            RecalculateStatsAPI.GetStatCoefficients += (body, args) =>
            {
                Ghost ghost;
                if (ghosts.TryGetValue(body, out ghost)) args.moveSpeedMultAdd += ghost.Speed;
            };
            Run.onRunStartGlobal += _ => { seen.Clear(); spared.Clear(); ghosts.Clear(); releases.Clear(); };
        }

        private static ItemQualityCounts Best(Vector3 position, out bool near)
        {
            near = false;
            var best = ItemQualityCounts.zero;
            foreach (var player in PlayerCharacterMasterController.instances)
            {
                var body = player && player.master ? player.master.GetBody() : null;
                var counts = Q.Counts(body, DLC4Content.Items.HexHaunting);
                if (!Q.Any(counts) || (body.corePosition - position).sqrMagnitude > GrabRange * GrabRange) continue;
                near = true;
                if (Q.Highest(counts) > Q.Highest(best)) best = counts;
            }
            return best;
        }

        private static void Grab()
        {
            var now = Time.fixedTime;
            releases.RemoveAll(r => now - r.Value > 10f);
            foreach (var key in seen.Keys.Where(k => !k).ToList()) seen.Remove(key);
            spared.RemoveWhere(p => !p);
            if (!wispMaster) wispMaster = MasterCatalog.FindMasterPrefab("WispMaster");
            foreach (var pickup in InstanceTracker.GetInstancesList<GenericPickupController>().ToList())
            {
                if (!pickup || spared.Contains(pickup)) continue;
                var position = pickup.gameObject.transform.position;
                float first;
                if (!seen.TryGetValue(pickup, out first))
                {
                    // An item a ghost just let go of is left alone for good.
                    if (releases.Any(r => (r.Key - position).sqrMagnitude < 64f)) { spared.Add(pickup); continue; }
                    seen[pickup] = first = now;
                }
                bool near;
                var counts = Best(position, out near);
                if (!near) { seen[pickup] = now; continue; }
                if (now - first < Q.AtHighest(counts, GrabDelay) || !wispMaster) continue;

#pragma warning disable 618
                var index = pickup.pickupIndex;
#pragma warning restore 618
                var master = new MasterSummon
                {
                    masterPrefab = wispMaster,
                    position = position + Vector3.up * Hover,
                    rotation = Quaternion.identity,
                    teamIndexOverride = TeamIndex.Monster,
                    ignoreTeamMemberLimit = true,
                    useAmbientLevel = true
                }.Perform();
                if (!master) continue;
                seen.Remove(pickup);
                NetworkServer.Destroy(pickup.gameObject);
                foreach (var ai in master.GetComponents<BaseAI>()) ai.enabled = false;
                var def = PickupCatalog.GetPickupDef(index);
                var icon = def != null && def.itemIndex != ItemIndex.None ? new[] { def.itemIndex } : new ItemIndex[0];
                Take(master, new Ghost { Pickup = index, Speed = Q.Stack(counts, Speed), Immunity = Q.AtHighest(counts, Immunity), DropAt = now + CarryTime }, icon, 30);
            }
        }

        // The wisp's body appears a moment after its master.
        private static void Take(CharacterMaster master, Ghost ghost, ItemIndex[] icon, int triesLeft)
        {
            if (!master) return;
            var body = master.GetBody();
            if (!body)
            {
                if (triesLeft > 0) RoR2Application.onNextUpdate += () => Take(master, ghost, icon, triesLeft - 1);
                else Drop(ghost, master.gameObject.transform.position);
                return;
            }
            ghosts[body] = ghost;
            body.AddTimedBuff(RoR2Content.Buffs.Immune, ghost.Immunity);
            body.MarkAllStatsDirty();
            var rewards = body.GetComponent<DeathRewards>();
            if (rewards) { rewards.goldReward = 0; rewards.expReward = 0; }
            Decor.Decorate(body.gameObject, 1f, icon);
        }

        // The wisp never hurts anyone.
        private static void Harmless(HealthComponent victim, DamageInfo damageInfo, CharacterBody attacker)
        {
            if (!attacker || !ghosts.ContainsKey(attacker)) return;
            damageInfo.damage = 0f;
            damageInfo.procCoefficient = 0f;
            damageInfo.damageType = DamageType.Silent | DamageType.NonLethal;
        }

        // With its AI off, it only does what its input says: fly away from the nearest player, hovering.
        private static void Carry()
        {
            if (!NetworkServer.active || ghosts.Count == 0) return;
            var now = Time.fixedTime;
            foreach (var entry in ghosts.ToList())
            {
                var body = entry.Key;
                var ghost = entry.Value;
                if (!body) { ghosts.Remove(body); continue; }
                var position = body.corePosition;
                CharacterBody nearest = null;
                var best = float.MaxValue;
                foreach (var player in PlayerCharacterMasterController.instances)
                {
                    var target = player && player.master ? player.master.GetBody() : null;
                    if (!target) continue;
                    var distance = Mathf.Min((target.corePosition - position).sqrMagnitude, (target.footPosition - position).sqrMagnitude);
                    if (distance < best) { best = distance; nearest = target; }
                }
                if (best < CatchRange * CatchRange || now >= ghost.DropAt)
                {
                    Release(body, ghost);
                    continue;
                }
                if (!body.inputBank) continue;
                var away = nearest ? position - nearest.corePosition : body.inputBank.moveVector;
                away.y = 0f;
                away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward;
                RaycastHit hit;
                if (Physics.Raycast(position, Vector3.down, out hit, 40f, LayerIndex.world.mask)) away.y = Mathf.Clamp((hit.point.y + Hover - position.y) * 0.5f, -1f, 1f);
                body.inputBank.moveVector = away;
                body.inputBank.aimDirection = new Vector3(away.x, 0f, away.z);
            }
        }

        private static void Release(CharacterBody body, Ghost ghost)
        {
            ghosts.Remove(body);
            Drop(ghost, body.corePosition);
            Visuals.Spawn(HexFx.GhostCatch, body.corePosition, 1f);
            HexItems.Remove(body.master, body);
        }

        private static void Shot(DamageReport report)
        {
            Ghost ghost;
            var body = report.victimBody;
            if (!NetworkServer.active || !body || !ghosts.TryGetValue(body, out ghost)) return;
            ghosts.Remove(body);
            Drop(ghost, body.corePosition);
        }

        private static void Drop(Ghost ghost, Vector3 position)
        {
            releases.Add(new KeyValuePair<Vector3, float>(position, Time.fixedTime));
            PickupDropletController.CreatePickupDroplet(new GenericPickupController.CreatePickupInfo { pickup = new UniquePickup(ghost.Pickup), position = position }, position, Vector3.up * 10f);
        }
    }

    // Hex of Loss, Slippery fingers: when one of your items triggers, a copy of it can slip out and bounce
    // away on the ground. An item is recognised by its proc (one entry per proc type, not per item) or by
    // the named source of a quality effect (Sources.cs); items that never visibly trigger are safe.
    internal static class HexOfLoss
    {
        private static readonly T4 Chance = new T4(1f, 2f, 5f, 10f);
        private static readonly T4 Bounce = new T4(10f, 15f, 25f, 40f);
        private const float Cooldown = 3f;

        private static readonly Dictionary<ProcType, string[]> procItems = new Dictionary<ProcType, string[]>
        {
            { ProcType.Behemoth, new[] { "Behemoth" } },
            { ProcType.Missile, new[] { "Missile", "MissileVoid" } },
            { ProcType.ChainLightning, new[] { "ChainLightning", "ChainLightningVoid" } },
            { ProcType.BounceNearby, new[] { "BounceNearby" } },
            { ProcType.Rings, new[] { "FireRing", "IceRing", "ElementalRingVoid" } },
            { ProcType.Thorns, new[] { "Thorns" } },
            { ProcType.Meatball, new[] { "FireballsOnHit" } },
            { ProcType.LightningStrikeOnHit, new[] { "LightningStrikeOnHit" } },
            { ProcType.BleedOnHit, new[] { "BleedOnHit" } },
            { ProcType.FractureOnHit, new[] { "BleedOnHitVoid" } },
            { ProcType.MeteorAttackOnHighDamage, new[] { "MeteorAttackOnHighDamage" } },
            { ProcType.DreamDemon, new[] { "DreamDemon" } },
            { ProcType.WispOnKill, new[] { "WispOnKill" } },
        };

        private static readonly Dictionary<KeyValuePair<CharacterMaster, ItemIndex>, float> lastSlip = new Dictionary<KeyValuePair<CharacterMaster, ItemIndex>, float>();

        internal static void Init()
        {
            GlobalEventManager.onServerDamageDealt += OnHit;
            Run.onRunStartGlobal += _ => lastSlip.Clear();
        }

        private static void OnHit(DamageReport report)
        {
            var body = report.attackerBody;
            var master = report.attackerMaster;
            if (!body || !master || !master.inventory) return;
            var counts = Q.Counts(body, DLC4Content.Items.RoamingItemDisable);
            if (!Q.Any(counts)) return;

            var candidates = new List<ItemIndex>();
            var fromEffect = Sources.ItemOf(report.damageInfo.inflictor);
            if (fromEffect != ItemIndex.None) candidates.Add(fromEffect);
            foreach (var entry in procItems)
            {
                if (!report.damageInfo.procChainMask.HasProc(entry.Key)) continue;
                foreach (var name in entry.Value)
                {
                    var index = ItemCatalog.FindItemIndex(name);
                    if (index != ItemIndex.None) candidates.Add(index);
                }
            }
            // The item itself or any of its quality versions, whichever copies you actually hold.
            var held = candidates.SelectMany(Variants).Where(i => master.inventory.GetItemCountPermanent(i) > 0 && HexItems.Takeable(i)).Distinct().ToList();
            if (held.Count == 0) return;
            var item = held[Random.Range(0, held.Count)];
            var key = new KeyValuePair<CharacterMaster, ItemIndex>(master, item);
            float last;
            if (lastSlip.TryGetValue(key, out last) && Time.fixedTime - last < Cooldown) return;
            if (!Util.CheckRoll(Q.Stack(counts, Chance), master)) return;
            lastSlip[key] = Time.fixedTime;

            master.inventory.RemoveItemPermanent(item, 1);
            var bounce = Q.Stack(counts, Bounce);
            var sideways = Random.insideUnitCircle.normalized;
            HexItems.Drop(item, body.corePosition + Vector3.up, new Vector3(sideways.x, 0.8f, sideways.y) * bounce);
            Visuals.Spawn(HexFx.Slip, body.corePosition, 1f);
        }

        private static IEnumerable<ItemIndex> Variants(ItemIndex item)
        {
            yield return item;
            var group = QualityCatalog.GetItemQualityGroup(QualityCatalog.FindItemQualityGroupIndex(item));
            if (group == null) yield break;
            for (var tier = QualityTier.Uncommon; tier < QualityTier.Count; tier++)
            {
                var variant = group.GetItemIndex(tier);
                if (variant != ItemIndex.None) yield return variant;
            }
        }
    }

    // Hex of Mimicry, Splitting: a mimic from a quality hex that dies holding stolen items bursts into smaller
    // mimics that share the loot; only when the last one dies do the items come back.
    internal static class HexOfMimicry
    {
        private static readonly T4 Children = new T4(2f, 2f, 3f, 3f);
        private static readonly T4 Generations = new T4(1f, 1f, 1f, 2f);
        private const float ChildScale = 0.7f;

        private sealed class Mimic { public QualityTier Tier; public int Generation; public List<KeyValuePair<Inventory, ItemIndex>> Held = new List<KeyValuePair<Inventory, ItemIndex>>(); }
        private static readonly Dictionary<CharacterMaster, Mimic> mimics = new Dictionary<CharacterMaster, Mimic>();

        // Must start at None: QualityTier's default value (0) is Uncommon.
        private static QualityTier spawning = QualityTier.None;
        private static Mimic pendingChild;

        internal static void Init()
        {
            Hooks.Patch(typeof(MimicryBehaviour), "TrySpawnMimic", typeof(HexOfMimicry), prefix: nameof(BeforeSpawn), postfix: nameof(AfterSpawn));
            Hooks.Patch(typeof(MasterSummon), nameof(MasterSummon.Perform), typeof(HexOfMimicry), postfix: nameof(Summoned));
            Hooks.Patch(typeof(EntityStates.Mimic.Death), nameof(EntityStates.Mimic.Death.OnEnter), typeof(HexOfMimicry), prefix: nameof(BeforeDeath), postfix: nameof(AfterDeath));
            Run.onRunStartGlobal += _ => mimics.Clear();
        }

        private static void BeforeSpawn(MimicryBehaviour __instance) { spawning = Q.Highest(Q.Counts(__instance.body, DLC4Content.Items.Mimicry)); }
        private static void AfterSpawn() { spawning = QualityTier.None; }

        private static void Summoned(CharacterMaster __result)
        {
            if (!__result) return;
            if (pendingChild != null) mimics[__result] = pendingChild;
            else if (spawning != QualityTier.None) mimics[__result] = new Mimic { Tier = spawning };
        }

        private static CharacterBody BodyOf(EntityStates.Mimic.Death state)
        {
            return state.outer ? state.outer.commonComponents.characterBody : null;
        }

        // What every player holds before the game hands the stolen items back.
        private static void BeforeDeath(EntityStates.Mimic.Death __instance, out Dictionary<Inventory, Dictionary<ItemIndex, int>> __state)
        {
            __state = null;
            var body = BodyOf(__instance);
            if (!NetworkServer.active || !body || !body.master || !mimics.ContainsKey(body.master)) return;
            __state = new Dictionary<Inventory, Dictionary<ItemIndex, int>>();
            foreach (var player in PlayerCharacterMasterController.instances)
            {
                var inventory = player && player.master ? player.master.inventory : null;
                if (inventory) __state[inventory] = inventory.itemAcquisitionOrder.ToDictionary(i => i, i => inventory.GetItemCountPermanent(i));
            }
        }

        private static void AfterDeath(EntityStates.Mimic.Death __instance, Dictionary<Inventory, Dictionary<ItemIndex, int>> __state)
        {
            if (__state == null) return;
            var body = BodyOf(__instance);
            Mimic mimic;
            if (!body || !body.master || !mimics.TryGetValue(body.master, out mimic)) return;
            mimics.Remove(body.master);

            // Everything this mimic carries: what the game just handed back, plus what it held already.
            var loot = new List<KeyValuePair<Inventory, ItemIndex>>(mimic.Held);
            foreach (var entry in __state)
            {
                if (!entry.Key) continue;
                foreach (var item in entry.Key.itemAcquisitionOrder.ToList())
                {
                    int before;
                    entry.Value.TryGetValue(item, out before);
                    var gained = entry.Key.GetItemCountPermanent(item) - before;
                    for (var i = 0; i < gained; i++) loot.Add(new KeyValuePair<Inventory, ItemIndex>(entry.Key, item));
                }
            }
            // Items it held from an earlier split go back now, unless they're split again below.
            foreach (var held in mimic.Held) if (held.Key) held.Key.GiveItemPermanent(held.Value, 1);
            if (loot.Count == 0 || mimic.Generation >= (int)Generations[mimic.Tier]) return;

            foreach (var taken in loot) if (taken.Key) taken.Key.RemoveItemPermanent(taken.Value, 1);
            var prefab = MasterCatalog.GetMasterPrefab(body.master.masterIndex);
            var count = (int)Children[mimic.Tier];
            for (var c = 0; c < count; c++)
            {
                var child = new Mimic { Tier = mimic.Tier, Generation = mimic.Generation + 1 };
                for (var i = c; i < loot.Count; i += count) child.Held.Add(loot[i]);
                var offset = Quaternion.Euler(0f, 360f * c / count, 0f) * Vector3.forward * 2f;
                pendingChild = child;
                CharacterMaster master;
                try
                {
                    master = new MasterSummon
                    {
                        masterPrefab = prefab,
                        position = body.footPosition + offset + Vector3.up,
                        rotation = Quaternion.identity,
                        teamIndexOverride = body.teamComponent.teamIndex,
                        ignoreTeamMemberLimit = true,
                        useAmbientLevel = true
                    }.Perform();
                }
                finally { pendingChild = null; }
                if (!master)
                {
                    foreach (var held in child.Held) if (held.Key) held.Key.GiveItemPermanent(held.Value, 1);
                    continue;
                }
                Show(master, Mathf.Pow(ChildScale, child.Generation), child.Held.Select(h => h.Value).ToArray(), 30);
            }
        }

        private static void Show(CharacterMaster master, float scale, ItemIndex[] items, int triesLeft)
        {
            if (!master || triesLeft <= 0) return;
            var body = master.GetBody();
            if (!body) { RoR2Application.onNextUpdate += () => Show(master, scale, items, triesLeft - 1); return; }
            Decor.Decorate(body.gameObject, scale, items);
        }
    }

    // Hex of Ossification, Calcify: enemies you kill leave a cluster of bone spikes that blocks the way and
    // shoots bone shards at the nearest player, then crumbles.
    internal static class HexOfOssification
    {
        private static readonly T4 Size = new T4(2f, 2.5f, 3f, 4f);
        private static readonly T4 ShotInterval = new T4(2f, 1.5f, 1f, 0.6f);
        private static readonly T4 ShardDamage = new T4(1f, 1.5f, 2.5f, 4f);   // of the dead enemy's damage
        private static readonly T4 Lifetime = new T4(4f, 6f, 8f, 12f);
        private const float ShardRange = 25f;
        private const float ShardTravel = 0.25f;

        private sealed class Spikes { public GameObject Object; public Vector3 Tip; public float Damage, Interval, NextShot, Until; }
        private struct Shard { public HealthComponent Target; public float Damage, Due; }
        private static readonly List<Spikes> spikes = new List<Spikes>();
        private static readonly List<Shard> shards = new List<Shard>();

        internal static void Init()
        {
            Hooks.Patch(typeof(GlobalEventManager), nameof(GlobalEventManager.ProcBarrierEnemiesOnKill), typeof(HexOfOssification), postfix: nameof(Killed));
            Ticker.Every(0.1f, Shoot);
            Run.onRunStartGlobal += _ => { spikes.Clear(); shards.Clear(); };
        }

        private static void Killed(DamageReport damageReport, CharacterBody victimBody)
        {
            var counts = Q.Counts(damageReport.attackerBody, DLC4Content.Items.BarrierEnemiesOnKill);
            if (!Q.Any(counts) || !victimBody || !HexFx.BoneSpikes) return;
            var position = victimBody.footPosition;
            RaycastHit hit;
            if (Physics.Raycast(victimBody.corePosition, Vector3.down, out hit, 30f, LayerIndex.world.mask)) position = hit.point;
            var cluster = Object.Instantiate(HexFx.BoneSpikes, position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            NetworkServer.Spawn(cluster);
            var size = Q.Stack(counts, Size);
            Decor.Decorate(cluster, size / 2f, null);
            var now = Time.fixedTime;
            spikes.Add(new Spikes
            {
                Object = cluster,
                Tip = position + Vector3.up * size,
                Damage = victimBody.damage * Q.Stack(counts, ShardDamage),
                Interval = Q.AtHighest(counts, ShotInterval),
                NextShot = now + 0.5f,
                Until = now + Q.Stack(counts, Lifetime)
            });
        }

        private static void Shoot()
        {
            var now = Time.fixedTime;
            foreach (var shard in shards.Where(s => s.Due <= now).ToList())
            {
                shards.Remove(shard);
                if (!shard.Target || !shard.Target.alive) continue;
                shard.Target.TakeDamage(new DamageInfo
                {
                    damage = shard.Damage,
                    position = shard.Target.body ? shard.Target.body.corePosition : shard.Target.gameObject.transform.position,
                    procCoefficient = 0f,
                    damageColorIndex = DamageColorIndex.Default
                });
            }
            foreach (var cluster in spikes.ToList())
            {
                if (!cluster.Object || now >= cluster.Until)
                {
                    spikes.Remove(cluster);
                    if (cluster.Object)
                    {
                        Visuals.Spawn(HexFx.PhantomPoof, cluster.Tip, 2f);
                        NetworkServer.Destroy(cluster.Object);
                    }
                    continue;
                }
                if (now < cluster.NextShot) continue;
                cluster.NextShot = now + cluster.Interval;
                var target = TeamComponent.GetTeamMembers(TeamIndex.Player)
                    .Select(m => m ? m.body : null)
                    .Where(b => b && b.isPlayerControlled && b.healthComponent && b.healthComponent.alive && (b.corePosition - cluster.Tip).sqrMagnitude <= ShardRange * ShardRange)
                    .OrderBy(b => (b.corePosition - cluster.Tip).sqrMagnitude)
                    .FirstOrDefault();
                if (!target) continue;
                Visuals.SpawnStreak(HexFx.BoneShard, cluster.Tip, target.corePosition);
                shards.Add(new Shard { Target = target.healthComponent, Damage = cluster.Damage, Due = now + ShardTravel });
            }
        }
    }

    // Hex of Strife, Pass the crown: Teleporter bosses wear a crown (more health and damage, bigger). When the
    // wearer dies, the crown jumps to the nearest enemy, a limited number of times.
    internal static class HexOfStrife
    {
        private static readonly T4 Crowns = new T4(1f, 1f, 1f, 2f);
        private static readonly T4 Jumps = new T4(1f, 2f, 3f, 5f);
        private static readonly T4 Health = new T4(50f, 100f, 200f, 400f);   // percent; damage is half of it
        private const float CrownScale = 1.3f;
        private const float JumpRange = 60f;

        private static readonly Dictionary<CharacterBody, int> worn = new Dictionary<CharacterBody, int>();   // wearer -> jumps left
        private static readonly ConditionalWeakTable<TeleporterInteraction, StrongBox<int>> placed = new ConditionalWeakTable<TeleporterInteraction, StrongBox<int>>();

        internal static void Init()
        {
            Ticker.Every(0.5f, Place);
            GlobalEventManager.onCharacterDeathGlobal += Pass;
            Run.onRunStartGlobal += _ => worn.Clear();
        }

        private static void Place()
        {
            var teleporter = TeleporterInteraction.instance;
            var counts = Dlc.TeamBest(DLC4Content.Items.HexMountain);
            if (!teleporter || !teleporter.bossGroup || !teleporter.bossGroup.combatSquad || !Q.Any(counts)) return;
            var count = placed.GetValue(teleporter, _ => new StrongBox<int>(0));
            if (count.Value >= (int)Crowns[Q.Highest(counts)]) return;
            var boss = teleporter.bossGroup.combatSquad.readOnlyMembersList
                .Select(m => m ? m.GetBody() : null)
                .FirstOrDefault(b => b && b.healthComponent && b.healthComponent.alive && !worn.ContainsKey(b));
            if (!boss) return;
            count.Value++;
            Crown(boss, Mathf.RoundToInt(Q.Stack(counts, Jumps)), counts);
        }

        private static void Crown(CharacterBody body, int jumpsLeft, ItemQualityCounts counts)
        {
            worn[body] = jumpsLeft;
            body.SetBuffCount(CustomBuffs.Crowned.buffIndex, Mathf.RoundToInt(Q.Stack(counts, Health)));
            Decor.Decorate(body.gameObject, CrownScale, null);
            // The bigger health pool starts full.
            RoR2Application.onNextUpdate += () => { if (body && body.healthComponent) body.healthComponent.HealFraction(1f, default(ProcChainMask)); };
        }

        private static void Pass(DamageReport report)
        {
            int jumpsLeft;
            var body = report.victimBody;
            if (!NetworkServer.active || !body || !worn.TryGetValue(body, out jumpsLeft)) return;
            worn.Remove(body);
            var counts = Dlc.TeamBest(DLC4Content.Items.HexMountain);
            if (jumpsLeft <= 0 || !Q.Any(counts)) return;
            var next = Enemies.Near(body.corePosition, JumpRange, TeamIndex.Player, report.victim)
                .Select(h => h.body)
                .FirstOrDefault(b => !worn.ContainsKey(b) && !b.HasBuff(CustomBuffs.Phantom));
            if (!next) return;
            Visuals.SpawnStreak(HexFx.CrownJump, body.corePosition + Vector3.up * body.radius, next.corePosition);
            Crown(next, jumpsLeft - 1, counts);
        }
    }
}
