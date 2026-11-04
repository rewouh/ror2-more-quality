using System.Collections.Generic;
using System.Linq;
using EntityStates;
using HarmonyLib;
using ItemQualities;
using RoR2;
using RoR2.Items;
using UnityEngine;
using UnityEngine.Networking;

namespace MoreQuality.Hallowed
{
    // A quality hex keeps its normal effect and adds a new twist on top. Its payoff is the cleansing
    // bonus (see Cleansing.cs). The other half of the hexes is in HexesWorld.cs.

    // Hex of Acceleration, Gale: every so often (a random delay, shorter with quality) a wind zone appears where
    // you stand and stays there for a while, pushing every player inside it one way. The zones are our own
    // networked objects (their size and direction travel with them); movement belongs to each player's own
    // game, so every game pushes its own player when they're inside one.
    internal static class HexOfAcceleration
    {
        private static readonly T4 Push = new T4(4f, 8f, 14f, 22f);          // m/s
        private static readonly T4 Radius = new T4(8f, 11f, 15f, 20f);
        private static readonly T4 Lifetime = new T4(8f, 10f, 12f, 15f);
        private static readonly T4 DelayMin = new T4(25f, 18f, 12f, 8f);
        private static readonly T4 DelayMax = new T4(45f, 32f, 22f, 14f);
        private const float BuiltRadius = 10f;   // the zone prefab's radius at scale 1

        private struct Zone { public GameObject Object; public float Until; }
        private static readonly List<Zone> zones = new List<Zone>();
        private static readonly Dictionary<CharacterBody, float> next = new Dictionary<CharacterBody, float>();

        internal static void Init()
        {
            Ticker.Every(0.5f, Spawn);
            RoR2Application.onFixedUpdate += Blow;
            Run.onRunStartGlobal += _ => { zones.Clear(); next.Clear(); };
        }

        private static void Spawn()
        {
            var now = Time.fixedTime;
            foreach (var zone in zones.Where(z => !z.Object || z.Until <= now).ToList())
            {
                zones.Remove(zone);
                if (zone.Object) NetworkServer.Destroy(zone.Object);
            }
            if (!HexFx.WindZone) return;
            foreach (var player in PlayerCharacterMasterController.instances)
            {
                var body = player && player.master ? player.master.GetBody() : null;
                var counts = Q.Counts(body, DLC4Content.Items.SlipperyGround);
                if (!Q.Any(counts)) { if (body) next.Remove(body); continue; }
                float due;
                if (!next.TryGetValue(body, out due)) { next[body] = now + Delay(counts); continue; }
                if (now < due) continue;
                next[body] = now + Delay(counts);

                var position = body.footPosition;
                RaycastHit hit;
                if (Physics.Raycast(body.corePosition, Vector3.down, out hit, 30f, LayerIndex.world.mask)) position = hit.point;
                var zone = Object.Instantiate(HexFx.WindZone, position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                NetworkServer.Spawn(zone);
                Decor.Decorate(zone, Q.Stack(counts, Radius) / BuiltRadius, null);
                zones.Add(new Zone { Object = zone, Until = now + Q.AtHighest(counts, Lifetime) });
            }
        }

        private static float Delay(ItemQualityCounts counts)
        {
            return Random.Range(Q.AtHighest(counts, DelayMin), Q.AtHighest(counts, DelayMax));
        }

        // On every game: push this game's own players while they stand in a zone.
        private static void Blow()
        {
            if (!Run.instance || Visuals.WindZoneMarker.All.Count == 0) return;
            var strength = Q.Stack(Dlc.TeamBest(DLC4Content.Items.SlipperyGround), Push);
            if (strength <= 0f) return;
            foreach (var user in LocalUserManager.readOnlyLocalUsersList)
            {
                var body = user.cachedBody;
                if (!body || !body.hasEffectiveAuthority || !body.characterMotor) continue;
                foreach (var zone in Visuals.WindZoneMarker.All)
                {
                    if (!zone) continue;
                    var radius = BuiltRadius * zone.transform.lossyScale.x;
                    var offset = body.footPosition - zone.transform.position;
                    if (offset.x * offset.x + offset.z * offset.z > radius * radius || Mathf.Abs(offset.y) > 12f) continue;
                    body.characterMotor.rootMotion += zone.transform.forward * strength * Time.fixedDeltaTime;
                }
            }
        }
    }

    // Hex of Blindness, Phantoms: harmless illusions of nearby enemies keep appearing around you. They deal
    // no damage and give nothing; hitting one pops it into smoke and gives whoever hit it a random malus for a
    // few seconds. They fade after a while. Only you can see yours:
    // every other player's game hides them (see Decor).
    internal static class HexOfBlindness
    {
        private static readonly T4 Interval = new T4(12f, 9f, 6f, 4f);
        private static readonly T4 Count = new T4(1f, 2f, 3f, 5f);
        private static readonly T4 CurseTime = new T4(2f, 3f, 5f, 8f);   // the malus for popping one
        private const float Lifetime = 20f;

        private static readonly BuffDef[] Curses =
        {
            RoR2Content.Buffs.Slow50, RoR2Content.Buffs.Cripple, RoR2Content.Buffs.Weak, RoR2Content.Buffs.HealingDisabled, RoR2Content.Buffs.BeetleJuice
        };
        private const float SearchRange = 40f;

        private static readonly Dictionary<CharacterBody, float> next = new Dictionary<CharacterBody, float>();
        private static readonly Dictionary<CharacterMaster, float> phantoms = new Dictionary<CharacterMaster, float>();
        private static readonly Dictionary<CharacterMaster, float> curseTime = new Dictionary<CharacterMaster, float>();

        internal static void Init()
        {
            Ticker.Every(1f, Tick);
            DamageHooks.AddIncoming(Illusions);
            Run.onRunStartGlobal += _ => { next.Clear(); phantoms.Clear(); curseTime.Clear(); };
        }

        private static void Tick()
        {
            var now = Time.fixedTime;
            foreach (var entry in phantoms.Where(e => !e.Key || e.Value < now).ToList())
            {
                phantoms.Remove(entry.Key);
                Vanish(entry.Key);
            }
            foreach (var player in PlayerCharacterMasterController.instances)
            {
                var body = player && player.master ? player.master.GetBody() : null;
                var counts = Q.Counts(body, DLC4Content.Items.Blindness);
                if (!Q.Any(counts)) continue;
                float due;
                if (!next.TryGetValue(body, out due)) due = now + Q.AtHighest(counts, Interval);
                if (now < due) { next[body] = due; continue; }
                next[body] = now + Q.AtHighest(counts, Interval);
                var models = Enemies.Near(body.corePosition, SearchRange, body.teamComponent.teamIndex)
                    .Where(h => h.body.master && !h.body.isBoss && !h.body.HasBuff(CustomBuffs.Phantom)).ToList();
                if (models.Count == 0) continue;
                for (var i = 0; i < Mathf.RoundToInt(Q.Stack(counts, Count)); i++) Spawn(models[Random.Range(0, models.Count)].body, body, Q.Stack(counts, CurseTime));
            }
        }

        private static void Spawn(CharacterBody model, CharacterBody near, float curse)
        {
            var prefab = MasterCatalog.GetMasterPrefab(model.master.masterIndex);
            if (!prefab) return;
            var ring = Random.insideUnitCircle.normalized * Random.Range(8f, 15f);
            var position = near.footPosition + new Vector3(ring.x, 3f, ring.y);
            RaycastHit hit;
            if (Physics.Raycast(position, Vector3.down, out hit, 20f, LayerIndex.world.mask)) position = hit.point + Vector3.up * 0.5f;
            var master = new MasterSummon
            {
                masterPrefab = prefab,
                position = position,
                rotation = Quaternion.LookRotation(near.corePosition - position),
                teamIndexOverride = model.teamComponent.teamIndex,
                ignoreTeamMemberLimit = true,
                useAmbientLevel = true
            }.Perform();
            if (!master) return;
            phantoms[master] = Time.fixedTime + Lifetime;
            curseTime[master] = curse;
            Mark(master, near, 30);
        }

        // The body appears a moment after the master: tag it once it's there.
        private static void Mark(CharacterMaster master, CharacterBody seer, int triesLeft)
        {
            if (!master || triesLeft <= 0) return;
            var body = master.GetBody();
            if (!body) { RoR2Application.onNextUpdate += () => Mark(master, seer, triesLeft - 1); return; }
            body.AddBuff(CustomBuffs.Phantom);
            Decor.Decorate(body.gameObject, 1f, null, seer ? seer.gameObject : null);
            var rewards = body.GetComponent<DeathRewards>();
            if (rewards) { rewards.goldReward = 0; rewards.expReward = 0; }
        }

        private static void Illusions(HealthComponent victim, DamageInfo damageInfo, CharacterBody attacker)
        {
            if (attacker && attacker.HasBuff(CustomBuffs.Phantom))
            {
                damageInfo.damage = 0f;
                damageInfo.procCoefficient = 0f;
                damageInfo.damageType = DamageType.Silent | DamageType.NonLethal;
                damageInfo.force = Vector3.zero;
                return;
            }
            var body = victim.body;
            if (!body || !body.HasBuff(CustomBuffs.Phantom) || !NetworkServer.active) return;
            damageInfo.damage = 0f;
            damageInfo.procCoefficient = 0f;
            var master = body.master;
            if (!master || !phantoms.Remove(master)) return;   // already popped
            float curse;
            if (attacker && curseTime.TryGetValue(master, out curse) && attacker.teamComponent.teamIndex != body.teamComponent.teamIndex)
                attacker.AddTimedBuff(Curses[Random.Range(0, Curses.Length)], curse);
            curseTime.Remove(master);
            RoR2Application.onNextUpdate += () => Vanish(master);
        }

        // No death: nothing to kill, no on-kill effects to trigger.
        private static void Vanish(CharacterMaster master)
        {
            if (!master) return;
            var body = master.GetBody();
            if (body)
            {
                Visuals.Spawn(HexFx.PhantomPoof, body.corePosition, body.radius);
                NetworkServer.Destroy(body.gameObject);
            }
            NetworkServer.Destroy(master.gameObject);
        }
    }

    // Hex of Famine, Hungry: a hunger bar (HungerBar.cs) empties over time. Healing fills it first; while
    // it's empty you starve. The hunger is a hidden buff's stack count (1-101), so every game can draw it.
    internal static class HexOfFamine
    {
        private static readonly T4 EmptyTime = new T4(90f, 60f, 40f, 25f);
        private static readonly T4 Starve = new T4(0.01f, 0.02f, 0.04f, 0.08f);   // of max health per second
        private const float HealToFill = 0.5f;   // healing this share of max health fills the whole bar
        private const float Tick = 0.5f;

        private static readonly Dictionary<CharacterBody, float> hunger = new Dictionary<CharacterBody, float>();

        internal static void Init()
        {
            Hooks.Patch(typeof(HealthComponent), nameof(HealthComponent.Heal), typeof(HexOfFamine), prefix: nameof(Eat));
            Ticker.Every(Tick, Digest);
            Run.onRunStartGlobal += _ => hunger.Clear();
        }

        private static void Eat(HealthComponent __instance, ref float amount)
        {
            float food;
            var body = __instance.body;
            if (!body || amount <= 0f || __instance.fullHealth <= 0f || !hunger.TryGetValue(body, out food) || food >= 100f) return;
            var points = amount / (__instance.fullHealth * HealToFill) * 100f;
            var eaten = Mathf.Min(points, 100f - food);
            hunger[body] = food + eaten;
            amount -= eaten / 100f * __instance.fullHealth * HealToFill;
            Show(body);
        }

        private static void Digest()
        {
            foreach (var body in hunger.Keys.Where(b => !b || !Q.Any(Q.Counts(b, DLC4Content.Items.LowerHealing))).ToList())
            {
                hunger.Remove(body);
                if (body) body.SetBuffCount(CustomBuffs.Hunger.buffIndex, 0);
            }
            foreach (var player in PlayerCharacterMasterController.instances)
            {
                var body = player && player.master ? player.master.GetBody() : null;
                var counts = Q.Counts(body, DLC4Content.Items.LowerHealing);
                if (!Q.Any(counts) || !body.healthComponent || !body.healthComponent.alive) continue;
                float food;
                if (!hunger.TryGetValue(body, out food)) food = 100f;
                food = Mathf.Max(0f, food - 100f / Q.AtHighest(counts, EmptyTime) * Tick);
                hunger[body] = food;
                Show(body);
                if (food > 0f) continue;
                body.healthComponent.TakeDamage(new DamageInfo
                {
                    damage = body.healthComponent.fullHealth * Q.Stack(counts, Starve) * Tick,
                    position = body.corePosition,
                    procCoefficient = 0f,
                    damageColorIndex = DamageColorIndex.Void,
                    damageType = DamageType.BypassArmor | DamageType.BypassBlock
                });
            }
        }

        private static void Show(CharacterBody body)
        {
            // Offset by one, so the bar stays visible when it's empty.
            var stacks = Mathf.Clamp(Mathf.CeilToInt(hunger[body]), 0, 100) + 1;
            if (body.GetBuffCount(CustomBuffs.Hunger) != stacks) body.SetBuffCount(CustomBuffs.Hunger.buffIndex, stacks);
        }
    }

    // Hex of Misfortune, Botched: an interaction can fail. The cost is paid, nothing happens.
    internal static class HexOfMisfortune
    {
        private static readonly T4 Chance = new T4(20f, 35f, 50f, 80f);
        private const float MaxChance = 90f;

        private struct Saved { public PurchaseEvent Purchase; public DetailedPurchaseEvent Detailed; }

        internal static void Init()
        {
            Hooks.Patch(typeof(PurchaseInteraction), nameof(PurchaseInteraction.OnInteractionBegin), typeof(HexOfMisfortune), prefix: nameof(Before), postfix: nameof(After));
        }

        // The game pays first, then runs the purchase's effects (open, roll, buy): those are swapped out.
        private static void Before(PurchaseInteraction __instance, Interactor activator, out Saved __state)
        {
            __state = default(Saved);
            if (!NetworkServer.active || !activator || __instance.costType == CostTypeIndex.None || __instance.costType == CostTypeIndex.Hex) return;
            if (!__instance.CanBeAffordedByInteractor(activator)) return;
            var body = activator.GetComponent<CharacterBody>();
            var counts = Q.Counts(body, DLC4Content.Items.RandomDebuffOnInteract);
            if (!Q.Any(counts) || !Util.CheckRoll(Mathf.Min(MaxChance, Q.Stack(counts, Chance)), body.master)) return;
            __state = new Saved { Purchase = __instance.onPurchase, Detailed = __instance.onDetailedPurchaseServer };
            __instance.onPurchase = new PurchaseEvent();
            __instance.onDetailedPurchaseServer = new DetailedPurchaseEvent();
            Visuals.Spawn(HexFx.Botched, __instance.gameObject.transform.position + Vector3.up, 1.5f);
        }

        private static void After(PurchaseInteraction __instance, Saved __state)
        {
            if (__state.Purchase == null) return;
            __instance.onPurchase = __state.Purchase;
            __instance.onDetailedPurchaseServer = __state.Detailed;
        }
    }

    // Hex of Pain, Shared pain: every hit you take is also dealt to your allies nearby (players, drones,
    // turrets, summons). With nobody around, an echo of the hit comes back to you instead.
    internal static class HexOfPain
    {
        private static readonly T4 Shared = new T4(0.1f, 0.25f, 0.6f, 1.2f);
        private static readonly T4 Range = new T4(15f, 20f, 30f, 40f);
        private static readonly T4 Echo = new T4(0.05f, 0.1f, 0.25f, 0.5f);
        private const float EchoDelay = 0.5f;

        private struct Pending { public HealthComponent Victim; public float Damage, Due; }
        private static readonly List<Pending> echoes = new List<Pending>();
        private static bool sharing;

        internal static void Init()
        {
            GlobalEventManager.onServerDamageDealt += OnHit;
            Ticker.Every(0.1f, Echoes);
            Run.onRunStartGlobal += _ => echoes.Clear();
        }

        private static void OnHit(DamageReport report)
        {
            var victim = report.victimBody;
            if (sharing || !victim || report.damageDealt <= 0f) return;
            var counts = Q.Counts(victim, DLC4Content.Items.HexCurse);
            if (!Q.Any(counts)) return;

            var range = Q.AtHighest(counts, Range);
            var allies = TeamComponent.GetTeamMembers(victim.teamComponent.teamIndex)
                .Select(m => m ? m.body : null)
                .Where(b => b && b != victim && b.healthComponent && b.healthComponent.alive && (b.corePosition - victim.corePosition).sqrMagnitude <= range * range)
                .ToList();
            if (allies.Count == 0)
            {
                echoes.Add(new Pending { Victim = report.victim, Damage = report.damageDealt * Q.Stack(counts, Echo), Due = Time.fixedTime + EchoDelay });
                return;
            }
            var damage = report.damageDealt * Q.Stack(counts, Shared);
            sharing = true;
            try
            {
                foreach (var ally in allies)
                {
                    Visuals.SpawnStreak(HexFx.PainLink, victim.corePosition, ally.corePosition);
                    ally.healthComponent.TakeDamage(new DamageInfo
                    {
                        attacker = report.attacker,
                        inflictor = Sources.Of(DLC4Content.Items.HexCurse),
                        damage = damage,
                        position = ally.corePosition,
                        procCoefficient = 0f,
                        damageColorIndex = DamageColorIndex.Bleed,
                        damageType = DamageType.BypassArmor
                    });
                }
            }
            finally { sharing = false; }
        }

        private static void Echoes()
        {
            var now = Time.fixedTime;
            sharing = true;
            try
            {
                foreach (var echo in echoes.Where(e => e.Due <= now).ToList())
                {
                    echoes.Remove(echo);
                    if (!echo.Victim || !echo.Victim.alive) continue;
                    echo.Victim.TakeDamage(new DamageInfo
                    {
                        damage = echo.Damage,
                        position = echo.Victim.body ? echo.Victim.body.corePosition : echo.Victim.gameObject.transform.position,
                        procCoefficient = 0f,
                        damageColorIndex = DamageColorIndex.Bleed,
                        damageType = DamageType.BypassArmor
                    });
                }
            }
            finally { sharing = false; }
        }
    }

    // Hex of Patience, Reaper: outside the charging Teleporter a reaper follows you and swings every second,
    // with a chance to execute you that doubles every few seconds you stay out.
    internal static class HexOfPatience
    {
        private static readonly T4 Chance = new T4(1f, 2f, 3f, 5f);   // per swing
        private static readonly T4 DoublingTime = new T4(20f, 15f, 8f, 5f);
        private static readonly Dictionary<CharacterBody, float> outside = new Dictionary<CharacterBody, float>();

        internal static void Init()
        {
            Ticker.Every(1f, Swing);
            Run.onRunStartGlobal += _ => outside.Clear();
        }

        private static void Swing()
        {
            var teleporter = TeleporterInteraction.instance;
            var counts = Dlc.TeamBest(DLC4Content.Items.OutOfTPRadius);
            if (!teleporter || !teleporter.isCharging || !Q.Any(counts) || !teleporter.holdoutZoneController)
            {
                outside.Clear();
                return;
            }
            foreach (var member in TeamComponent.GetTeamMembers(TeamIndex.Player))
            {
                var body = member ? member.body : null;
                if (!body || !body.healthComponent || !body.healthComponent.alive || !body.isPlayerControlled) continue;
                if (teleporter.holdoutZoneController.IsBodyInChargingRadius(body)) { outside.Remove(body); continue; }
                float seconds;
                outside.TryGetValue(body, out seconds);
                outside[body] = seconds + 1f;
                CustomBuffs.Mark(body, CustomBuffs.Reaped, 1, 1.5f);
                Visuals.SpawnOn(HexFx.ReaperSwoop, body, 1f);
                var chance = Q.Stack(counts, Chance) * Mathf.Pow(2f, seconds / Q.AtHighest(counts, DoublingTime));
                if (!Util.CheckRoll(chance)) continue;   // a miss: the scythe's swing is enough
                Visuals.Spawn(HexFx.ReaperHit, body.corePosition, 1f);
                body.healthComponent.Suicide();
            }
        }
    }

    // Hex of Rain, Storm clouds: red storm clouds drift around the stage, raining on whatever is under them.
    // Enemies under a cloud are enraged; players under one are drenched, losing luck. The clouds are our own
    // networked objects, moved by the server (Decor.Move), so every game sees them in the same place.
    internal static class HexOfRain
    {
        private static readonly T4 Clouds = new T4(1f, 2f, 3f, 4f);
        private static readonly T4 Radius = new T4(8f, 10f, 13f, 16f);
        private static readonly T4 Drift = new T4(3f, 4f, 5f, 6f);           // m/s
        private static readonly T4 Rage = new T4(15f, 30f, 60f, 100f);       // percent
        private static readonly T4 BadLuck = new T4(1f, 1f, 2f, 3f);
        private const float Height = 14f;
        private const float Wander = 40f;

        private sealed class Cloud { public GameObject Object; public Vector3 Target; public float Radius; }
        private static readonly List<Cloud> clouds = new List<Cloud>();

        internal static void Init()
        {
            Ticker.Every(0.5f, Rain);
            RoR2Application.onFixedUpdate += Move;
            Hooks.Patch(typeof(CharacterBody), nameof(CharacterBody.RecalculateStats), typeof(HexOfRain), postfix: nameof(Drenched));
            Run.onRunStartGlobal += _ => clouds.Clear();
        }

        // The game sets your luck in RecalculateStats; being drenched takes some away.
        private static void Drenched(CharacterBody __instance)
        {
            var stacks = __instance.GetBuffCount(CustomBuffs.Drenched);
            if (stacks > 0 && __instance.master) __instance.master.luck -= stacks;
        }

        private static CharacterBody RandomPlayer()
        {
            var players = PlayerCharacterMasterController.instances.Select(p => p && p.master ? p.master.GetBody() : null).Where(b => b).ToList();
            return players.Count > 0 ? players[Random.Range(0, players.Count)] : null;
        }

        private static Vector3 NewTarget()
        {
            var player = RandomPlayer();
            var offset = Random.insideUnitCircle * Wander;
            return player ? player.footPosition + new Vector3(offset.x, 0f, offset.y) : Vector3.zero;
        }

        private static void Rain()
        {
            clouds.RemoveAll(c => !c.Object);
            var counts = Dlc.TeamBest(DLC4Content.Items.IncreasedDifficulty);
            if (!Q.Any(counts) || !HexFx.StormCloud)
            {
                foreach (var cloud in clouds) NetworkServer.Destroy(cloud.Object);
                clouds.Clear();
                return;
            }
            var radius = Q.Stack(counts, Radius);
            while (clouds.Count < (int)Q.AtHighest(counts, Clouds) && RandomPlayer())
            {
                var start = NewTarget() + Vector3.up * Height;
                var cloud = Object.Instantiate(HexFx.StormCloud, start, Quaternion.identity);
                NetworkServer.Spawn(cloud);
                Decor.Decorate(cloud, radius / 10f, null);
                clouds.Add(new Cloud { Object = cloud, Target = NewTarget(), Radius = radius });
            }

            var rage = Mathf.RoundToInt(Q.Stack(counts, Rage));
            var luck = Mathf.RoundToInt(Q.AtHighest(counts, BadLuck));
            foreach (var member in CharacterBody.readOnlyInstancesList)
            {
                if (!member || !member.healthComponent || !member.healthComponent.alive) continue;
                if (!clouds.Any(c => Under(member, c))) continue;
                if (member.teamComponent.teamIndex == TeamIndex.Player) { if (member.isPlayerControlled) CustomBuffs.Mark(member, CustomBuffs.Drenched, luck, 0.75f); }
                else CustomBuffs.Mark(member, CustomBuffs.Enraged, rage, 0.75f);
            }
        }

        private static bool Under(CharacterBody body, Cloud cloud)
        {
            var offset = body.corePosition - cloud.Object.transform.position;
            return offset.y < 0f && offset.x * offset.x + offset.z * offset.z <= cloud.Radius * cloud.Radius;
        }

        private static void Move()
        {
            if (!NetworkServer.active || clouds.Count == 0) return;
            var speed = Q.AtHighest(Dlc.TeamBest(DLC4Content.Items.IncreasedDifficulty), Drift);
            foreach (var cloud in clouds)
            {
                if (!cloud.Object) continue;
                var position = cloud.Object.transform.position;
                var flat = cloud.Target - position;
                flat.y = 0f;
                if (flat.sqrMagnitude < 4f) { cloud.Target = NewTarget(); continue; }
                position += flat.normalized * speed * Time.fixedDeltaTime;
                RaycastHit hit;
                if (Physics.Raycast(position + Vector3.up * 30f, Vector3.down, out hit, 120f, LayerIndex.world.mask)) position.y = Mathf.Lerp(position.y, hit.point.y + Height, 0.02f);
                Decor.Move(cloud.Object, position);
            }
        }
    }

    // Hex of Leaping: each jump costs more height, and throws you off in a random direction.
    internal static class HexOfLeaping
    {
        private static readonly T4 ExtraPenalty = new T4(5f, 10f, 25f, 50f);   // on top of the base 25%
        private static readonly T4 Kick = new T4(5f, 10f, 25f, 50f);           // sideways speed added to each jump

        internal static void Init()
        {
            Hooks.Patch(typeof(WeakAssHamstringBehaviour), "HandleJump", typeof(HexOfLeaping), postfix: nameof(AfterJump));
            Hooks.Patch(typeof(GenericCharacterMain), nameof(GenericCharacterMain.ProcessJump), typeof(HexOfLeaping), prefix: nameof(BeforeProcessJump), postfix: nameof(AfterProcessJump), args: new[] { typeof(bool) });
        }

        // EntityState.characterMotor/characterBody are protected: go through the state machine instead.
        private static CharacterMotor Motor(GenericCharacterMain state) { return state.outer ? state.outer.commonComponents.characterMotor : null; }
        private static CharacterBody Body(GenericCharacterMain state) { return state.outer ? state.outer.commonComponents.characterBody : null; }

        // Movement belongs to the player's own game, so the kick is applied there.
        private static void BeforeProcessJump(GenericCharacterMain __instance, out int __state)
        {
            var motor = Motor(__instance);
            __state = motor ? motor.jumpCount : -1;
        }

        private static void AfterProcessJump(GenericCharacterMain __instance, int __state)
        {
            var motor = Motor(__instance);
            var body = Body(__instance);
            if (__state < 0 || !motor || !body || motor.jumpCount <= __state || !body.hasEffectiveAuthority) return;
            var tier = Q.Highest(Q.Counts(body, DLC4Content.Items.WeakAssHamstring));
            if (tier == QualityTier.None) return;
            var direction = Random.insideUnitCircle.normalized;
            motor.velocity += new Vector3(direction.x, 0f, direction.y) * Q.Stack(Q.Counts(body, DLC4Content.Items.WeakAssHamstring), Kick);
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
}
