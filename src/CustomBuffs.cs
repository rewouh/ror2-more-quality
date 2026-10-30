using System;
using System.Collections.Generic;
using R2API;
using RoR2;
using UnityEngine;

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

            RoR2Application.onLoad += AssignIcons;
            RecalculateStatsAPI.GetStatCoefficients += Stats;
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

        // X-4's random effects. Blood Tester's rush is handled by its own module (it scales with tier).
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
        }
    }
}
