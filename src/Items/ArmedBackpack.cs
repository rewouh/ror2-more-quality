using ItemQualities;
using R2API;
using RoR2;
using RoR2.Projectile;
using SS2;
using UnityEngine;

namespace MoreQuality.Items
{
    // Quality backpacks fire a bigger missile that explodes on impact. Legendary is absurdly huge and slow.
    internal static class ArmedBackpack
    {
        private static readonly T4 Size = new T4(6f, 10.5f, 18f, 36f);
        private static readonly T4 Radius = new T4(4f, 7f, 11f, 20f);
        private static readonly T4 BonusDamage = new T4(2f, 5f, 10f, 20f);   // on top of the base 400% per stack
        private static readonly GameObject[] missiles = new GameObject[4];

        internal static void Init()
        {
            ContentProvider.AddBuilder(BuildMissiles);
            Hooks.Patch(typeof(SS2.Items.ArmedBackpack.Behavior), "OnTakeDamageServer", typeof(ArmedBackpack), prefix: nameof(OnTakeDamageServer));
        }

        private static void BuildMissiles(RoR2.ContentManagement.ContentPack pack)
        {
            var source = SS2.Items.ArmedBackpack.projectilePrefab ?? SS2Assets.LoadAsset<GameObject>("BackpackMissile", SS2Bundle.Items);
            if (!source) { MoreQualityPlugin.Log.LogWarning("Armed Backpack missile prefab not found"); return; }
            var explosion = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/OmniEffect/OmniExplosionVFXQuick");

            for (var tier = QualityTier.Uncommon; tier <= QualityTier.Legendary; tier++)
            {
                var prefab = PrefabAPI.InstantiateClone(source, "QualitySS2BackpackMissile" + tier, true);
                var controller = prefab.GetComponent<ProjectileController>();
                if (controller && controller.ghostPrefab)
                {
                    var ghost = PrefabAPI.InstantiateClone(controller.ghostPrefab, "QualitySS2BackpackMissileGhost" + tier, false);
                    ghost.transform.localScale *= Size[tier];
                    controller.ghostPrefab = ghost;
                }

                var single = prefab.GetComponent<ProjectileSingleTargetImpact>();
                var impact = prefab.GetComponent<ProjectileImpactExplosion>();
                if (!impact)
                {
                    impact = prefab.AddComponent<ProjectileImpactExplosion>();
                    impact.destroyOnWorld = true;
                    impact.destroyOnEnemy = true;
                    impact.lifetime = 20f;
                    impact.explosionEffect = explosion;
                    if (single)
                    {
                        impact.destroyOnWorld = single.destroyOnWorld;
                        impact.explosionSoundString = single.hitSoundString;
                    }
                }
                if (single) Object.DestroyImmediate(single);
                impact.blastRadius = Radius[tier];
                impact.blastDamageCoefficient = 1f;
                impact.blastProcCoefficient = 1f;
                impact.falloffModel = BlastAttack.FalloffModel.None;

                if (tier == QualityTier.Legendary)
                {
                    var missile = prefab.GetComponent<MissileController>();
                    if (missile) missile.maxVelocity *= 0.6f;
                }
                pack.projectilePrefabs.Add(new[] { prefab });
                missiles[(int)tier] = prefab;
            }
        }

        // Same roll as Starstorm2's, but with the quality missile and its bonus damage.
        private static bool OnTakeDamageServer(SS2.Items.ArmedBackpack.Behavior __instance, DamageReport damageReport)
        {
            var body = __instance.body;
            var counts = Q.Counts(body, SS2Content.Items.ArmedBackpack);
            var tier = Q.Highest(counts);
            if (tier == QualityTier.None || !missiles[(int)tier]) return true;
            if (__instance.stack <= 0 || damageReport.damageDealt <= 0f) return false;

            var victim = damageReport.victimBody;
            var baseHealth = victim.baseMaxHealth + victim.levelMaxHealth * (victim.level - 1f);
            var chance = damageReport.damageDealt / baseHealth * 100f * SS2.Items.ArmedBackpack.procMult;
            if (!Util.CheckRoll(Mathf.Max(chance, SS2.Items.ArmedBackpack.procMinimum), body.master)) return false;

            var coefficient = SS2.Items.ArmedBackpack.backpackDamageCoeff * __instance.stack + Q.Sum(counts, BonusDamage);
            var target = damageReport.attacker;
            if (!damageReport.attackerBody || damageReport.attackerTeamIndex == body.teamComponent.teamIndex) target = null;
            var origin = body.corePosition + Vector3.up * (body.radius + 3f + 0.3f * Size[tier]);   // clear of you, without starting too high
            MissileUtils.FireMissile(origin, body, default(ProcChainMask), target, body.damage * coefficient,
                Util.CheckRoll(body.crit, body.master), missiles[(int)tier], DamageColorIndex.Item, addMissileProc: false);
            return false;
        }
    }
}
