using System;
using System.Collections.Generic;
using System.Linq;
using R2API;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace MoreQuality
{
    // Buffs the quality effects need. They borrow vanilla icons, picked once the catalog is loaded.
    internal static class CustomBuffs
    {
        public static BuffDef BloodRush;
        public static BuffDef StimArmor;
        public static BuffDef StimSpeed;
        public static BuffDef StimDamage;
        public static BuffDef StimCrit;
        public static BuffDef StimRegen;
        public static BuffDef FieldAura;   // stacks = % attack speed
        public static BuffDef InfernalFervor;   // Infernal Mark: stacks = % damage and attack speed
        public static BuffDef HexEmpowered;     // hex monsters: stacks = % damage and movement speed
        public static BuffDef Pinned;      // Fork: rooted; stacks = % extra damage taken
        public static BuffDef Hunted;      // Hunter's Sigil: stacks = % extra damage taken
        public static BuffDef Voodoo;      // Needles: stacks = % of damage taken shared with other voodoo enemies
        public static BuffDef Slowed;      // Field Accelerator, Earthbound Vessel: stacks = % movement speed lost
        public static BuffDef OnTheBeat;   // Watch Metronome: the beat window
        public static BuffDef Molten;      // Molten Coin: burning aura; stacks = its radius in metres
        public static BuffDef Overcharged; // Universal Charger: the refreshed skill's empowered use
        public static BuffDef GoldRush;    // M.I.D.A.S.: kills pay gold
        public static BuffDef BloodOffering; // Sanguine Chalice: stacks = stored blood, % of max health
        public static BuffDef Wounded;     // Hex of Pain: stacks = open wounds

        private static readonly List<KeyValuePair<BuffDef, Func<BuffDef>>> icons = new List<KeyValuePair<BuffDef, Func<BuffDef>>>();

        public static void Register()
        {
            BloodRush = Create("bdQualitySS2BloodRush", new Color(0.9f, 0.25f, 0.3f), false, false, () => SS2.SS2Content.Buffs.BuffBloodTesterRegen ?? RoR2Content.Buffs.CloakSpeed);
            StimArmor = Create("bdQualitySS2StimArmor", new Color(0.65f, 0.75f, 0.85f), false, false, () => RoR2Content.Buffs.ArmorBoost);
            StimSpeed = Create("bdQualitySS2StimSpeed", new Color(0.4f, 0.85f, 1f), false, false, () => RoR2Content.Buffs.WhipBoost);
            StimDamage = Create("bdQualitySS2StimDamage", new Color(1f, 0.5f, 0.2f), false, false, () => RoR2Content.Buffs.PowerBuff);
            StimCrit = Create("bdQualitySS2StimCrit", new Color(1f, 0.85f, 0.3f), false, false, () => RoR2Content.Buffs.FullCrit);
            StimRegen = Create("bdQualitySS2StimRegen", new Color(0.5f, 1f, 0.45f), false, false, () => RoR2Content.Buffs.MedkitHeal);
            FieldAura = Create("bdQualitySS2FieldAura", new Color(0.9f, 0.55f, 0.1f), true, false, () => RoR2Content.Buffs.AttackSpeedOnCrit);
            InfernalFervor = Create("bdMoreQualityInfernalFervor", new Color(0.85f, 0.2f, 0.45f), true, false, () => RoR2Content.Buffs.WarCryBuff);
            HexEmpowered = Create("bdMoreQualityHexEmpowered", new Color(0.55f, 0.25f, 0.8f), true, false, () => RoR2Content.Buffs.PowerBuff);
            Pinned = Create("bdMoreQualityPinned", new Color(0.75f, 0.75f, 0.8f), true, true, () => RoR2Content.Buffs.Entangle);
            Hunted = Create("bdMoreQualityHunted", new Color(0.95f, 0.35f, 0.2f), true, true, () => RoR2Content.Buffs.DeathMark);
            Voodoo = Create("bdMoreQualityVoodoo", new Color(0.6f, 0.2f, 0.55f), true, true, () => RoR2Content.Buffs.LunarSecondaryRoot ?? RoR2Content.Buffs.DeathMark);
            Slowed = Create("bdMoreQualitySlowed", new Color(0.45f, 0.6f, 0.95f), true, true, () => RoR2Content.Buffs.Slow50);
            OnTheBeat = Create("bdMoreQualityOnTheBeat", new Color(1f, 0.9f, 0.4f), false, false, () => RoR2Content.Buffs.FullCrit);
            Molten = Create("bdMoreQualityMolten", new Color(1f, 0.45f, 0.1f), true, false, () => RoR2Content.Buffs.OnFire);
            Overcharged = Create("bdMoreQualityOvercharged", new Color(0.4f, 0.8f, 1f), false, false, () => RoR2Content.Buffs.TeslaField ?? RoR2Content.Buffs.PowerBuff);
            GoldRush = Create("bdMoreQualityGoldRush", new Color(1f, 0.8f, 0.2f), false, false, () => RoR2Content.Buffs.CloakSpeed);
            BloodOffering = Create("bdMoreQualityBloodOffering", new Color(0.7f, 0.05f, 0.15f), true, false, () => RoR2Content.Buffs.Bleeding);
            Wounded = Create("bdMoreQualityWounded", new Color(0.6f, 0.1f, 0.1f), true, true, () => RoR2Content.Buffs.Bleeding);

            RoR2Application.onLoad += AssignIcons;
            RecalculateStatsAPI.GetStatCoefficients += Stats;
            DamageHooks.AddIncoming(Vulnerable);
            Ticker.Every(0.2f, ExpireMarks);
            Run.onRunStartGlobal += _ => marks.Clear();
        }

        private static BuffDef Create(string name, Color color, bool canStack, bool isDebuff, Func<BuffDef> iconSource, bool hidden = false)
        {
            var def = ScriptableObject.CreateInstance<BuffDef>();
            def.name = name;
            def.buffColor = color;
            def.canStack = canStack;
            def.isDebuff = isDebuff;
            def.isHidden = hidden;
            def.isCooldown = false;
            ContentAddition.AddBuffDef(def);
            icons.Add(new KeyValuePair<BuffDef, Func<BuffDef>>(def, iconSource));
            return def;
        }

        private static void AssignIcons()
        {
            foreach (var pair in icons)
            {
                try
                {
                    var source = pair.Value();
                    if (source) pair.Key.iconSprite = source.iconSprite;
                }
                catch (Exception e)
                {
                    MoreQualityPlugin.Log.LogWarning("No icon for " + pair.Key.name + ": " + e.Message);
                }
            }
        }

        // X-4's random effects and the stack-counted buffs. Blood Tester's rush is handled by its own module (it scales with tier).
        private static void Stats(CharacterBody body, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (body.HasBuff(StimArmor)) args.armorAdd += 30f;
            if (body.HasBuff(StimSpeed)) args.moveSpeedMultAdd += 0.3f;
            if (body.HasBuff(StimDamage)) args.damageMultAdd += 0.25f;
            if (body.HasBuff(StimCrit)) args.critAdd += 20f;
            if (body.HasBuff(StimRegen)) args.baseRegenAdd += 10f;
            var fervor = body.GetBuffCount(InfernalFervor) / 100f;
            args.damageMultAdd += fervor;
            args.attackSpeedMultAdd += fervor;
            var empowered = body.GetBuffCount(HexEmpowered) / 100f;
            args.damageMultAdd += empowered;
            args.moveSpeedMultAdd += empowered;
            if (body.HasBuff(Pinned)) args.moveSpeedRootCount += 1;
            args.moveSpeedReductionMultAdd += Mathf.Min(body.GetBuffCount(Slowed), 90) / 100f;
        }

        // Pinned and Hunted enemies take more damage from everyone.
        private static void Vulnerable(HealthComponent victim, DamageInfo damageInfo, CharacterBody attacker)
        {
            var body = victim.body;
            if (!body) return;
            var extra = body.GetBuffCount(Pinned) + body.GetBuffCount(Hunted);
            if (extra > 0) damageInfo.damage *= 1f + extra / 100f;
        }

        // ---- short debuffs refreshed while their source lasts (zones, auras), server only.
        // Mark() sets the buff to at least `stacks` for `duration` seconds; it drops back to 0 when it runs out.

        private struct MarkState { public int Stacks; public float Until; }
        private static readonly Dictionary<KeyValuePair<CharacterBody, BuffDef>, MarkState> marks = new Dictionary<KeyValuePair<CharacterBody, BuffDef>, MarkState>();

        public static void Mark(CharacterBody body, BuffDef buff, int stacks, float duration)
        {
            if (!NetworkServer.active || !body || !buff || stacks <= 0) return;
            var key = new KeyValuePair<CharacterBody, BuffDef>(body, buff);
            MarkState state;
            var now = Time.fixedTime;
            if (marks.TryGetValue(key, out state) && state.Until >= now) stacks = Mathf.Max(stacks, state.Stacks);
            marks[key] = new MarkState { Stacks = stacks, Until = Mathf.Max(now + duration, state.Until) };
            if (body.GetBuffCount(buff) != stacks) body.SetBuffCount(buff.buffIndex, stacks);
        }

        private static void ExpireMarks()
        {
            var now = Time.fixedTime;
            foreach (var entry in marks.Where(e => !e.Key.Key || e.Value.Until < now).ToList())
            {
                marks.Remove(entry.Key);
                if (entry.Key.Key) entry.Key.Key.SetBuffCount(entry.Key.Value.buffIndex, 0);
            }
        }
    }
}
