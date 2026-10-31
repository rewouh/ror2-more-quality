using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using ItemQualities;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;
using Spec = MoreQuality.Visuals.Spec;
using Shape = UnityEngine.ParticleSystemShapeType;

namespace MoreQuality.Hallowed
{
    // Quality Hand of Death: a Hallowed Shrine's Hand of Death has a chance (config) to be a quality one,
    // painted white. Every hex it drops gets its own random quality, so it hurts more, but each quality hex
    // guarantees a choice of its quality when cleansed (see Cleansing.cs).
    //
    // The hand itself is the game's. The server remembers which hands are quality, and spawns one of our
    // small networked markers on it, which repaints the hand and adds the particles on every game.
    internal static class QualityHand
    {
        // Each hex's quality: flatter than Quality's own odds, since the whole point of this hand is quality.
        private static readonly WeightedSelection<QualityTier> tiers = new WeightedSelection<QualityTier>();

        private static readonly HashSet<GrantHexesController> qualityHands = new HashSet<GrantHexesController>();
        private static readonly AccessTools.FieldRef<HallowingShrineSplitBase, List<HallowingShrineSplitNode>> nodes =
            AccessTools.FieldRefAccess<HallowingShrineSplitBase, List<HallowingShrineSplitNode>>("splitNodes");
        private static readonly AccessTools.FieldRef<EntityStates.HallowedCauldron.GrantHexes, GrantHexesController> stateController =
            AccessTools.FieldRefAccess<EntityStates.HallowedCauldron.GrantHexes, GrantHexesController>("controller");

        private static GrantHexesController dropping;
        private static GameObject markerPrefab;

        internal static readonly Color[] TierColors =
        {
            new Color(0.35f, 0.95f, 0.45f), new Color(0.3f, 0.6f, 1f), new Color(0.75f, 0.35f, 1f), new Color(1f, 0.6f, 0.15f)
        };

        internal static void Init()
        {
            tiers.AddChoice(QualityTier.Uncommon, 0.4f);
            tiers.AddChoice(QualityTier.Rare, 0.3f);
            tiers.AddChoice(QualityTier.Epic, 0.2f);
            tiers.AddChoice(QualityTier.Legendary, 0.1f);

            ContentProvider.AddBuilder(BuildMarker);
            Hooks.Patch(typeof(HallowingShrineSplitBase), nameof(HallowingShrineSplitBase.Start), typeof(QualityHand), postfix: nameof(ShrineStarted));
            Hooks.Patch(typeof(EntityStates.HallowedCauldron.GrantHexes), "SpawnHex", typeof(QualityHand), prefix: nameof(BeforeHex), postfix: nameof(AfterHex));
            Hooks.Patch(typeof(PickupDropTable), nameof(PickupDropTable.GeneratePickup), typeof(QualityHand), postfix: nameof(QualityHex));
            Run.onRunStartGlobal += _ => qualityHands.Clear();
        }

        private static void ShrineStarted(HallowingShrineSplitBase __instance)
        {
            if (!NetworkServer.active) return;
            qualityHands.RemoveWhere(h => !h);
            foreach (var node in nodes(__instance) ?? new List<HallowingShrineSplitNode>())
            {
                var hand = node ? node.GetComponent<GrantHexesController>() : null;
                if (!hand || !Util.CheckRoll(MoreQualityPlugin.QualityHandChance.Value)) continue;
                qualityHands.Add(hand);
                if (!markerPrefab) continue;
                var marker = Object.Instantiate(markerPrefab, hand.gameObject.transform.position, hand.gameObject.transform.rotation);
                NetworkServer.Spawn(marker);
            }
        }

        private static void BeforeHex(EntityStates.HallowedCauldron.GrantHexes __instance)
        {
            var controller = stateController(__instance);
            dropping = controller && qualityHands.Contains(controller) ? controller : null;
        }

        private static void AfterHex()
        {
            dropping = null;
        }

        // Only while a quality hand is dropping: every hex it rolls gets its own random quality.
        private static void QualityHex(ref UniquePickup __result)
        {
            if (!dropping || !__result.isValid) return;
            var baseIndex = QualityCatalog.GetPickupIndexOfQuality(__result.pickupIndex, QualityTier.None);
            var upgraded = QualityCatalog.GetPickupIndexOfQuality(baseIndex, tiers.Evaluate(Random.value));
            if (upgraded.isValid) __result = __result.WithPickupIndex(upgraded);
        }

        // ---------------------------------------------------------------- visuals

        private static void BuildMarker(RoR2.ContentManagement.ContentPack pack)
        {
            var root = new GameObject("MoreQualityQualityHandMarker");
            root.AddComponent<NetworkIdentity>();
            var visuals = new GameObject("Visuals").transform;
            visuals.SetParent(root.transform, false);
            visuals.gameObject.SetActive(false);   // shown once it found its hand
            var flat = new Vector3(90f, 0f, 0f);
            Visuals.Flash(visuals, new Color(1f, 0.97f, 0.9f), 9f, 3f, 0f);
            Visuals.Particles(visuals, "Halo", new Spec { Color = new Color(1f, 0.97f, 0.88f), Size = 0.25f, Lifetime = 1.5f, Rate = 30f, Speed = 0.15f, Shape = Shape.Sphere, Radius = 2.2f, Shell = true, Gravity = -0.05f, Shrink = true, Spin = true });
            for (var i = 0; i < TierColors.Length; i++)
            {
                Visuals.Particles(visuals, "Tier" + i, new Spec { Color = TierColors[i], Size = 0.18f, Lifetime = 2f, Rate = 8f, Speed = 0.3f, Shape = Shape.Sphere, Radius = 2f, Gravity = -0.08f, Shrink = true, Noise = 0.3f });
            }
            Visuals.Particles(visuals, "Base", new Spec { Material = Visuals.Kind.Smoke, Color = new Color(0.9f, 0.88f, 0.8f), ColorEnd = new Color(0.5f, 0.5f, 0.5f), Size = 1.2f, Lifetime = 2.5f, Rate = 6f, Speed = 0.1f, Shape = Shape.Circle, Radius = 1.5f, Grow = true, Spin = true, Rotation = flat });
            root.AddComponent<Marker>().visuals = visuals.gameObject;
            markerPrefab = R2API.PrefabAPI.InstantiateClone(root, "MoreQualityQualityHandMarker", true);
            Object.Destroy(root);
            pack.networkedObjectPrefabs.Add(new[] { markerPrefab });
        }

        // Runs on every game: finds the Hand of Death it was spawned on, paints it white and shows the particles
        // around the hand's model. Leaves when the hand is gone; dims once the hand has been used.
        internal sealed class Marker : MonoBehaviour
        {
            public GameObject visuals;
            private GrantHexesController hand;
            private float searchUntil;
            private bool painted;

            private static Material white;

            private void Start()
            {
                searchUntil = Time.time + 10f;
            }

            private void Update()
            {
                if (!hand)
                {
                    if (painted || Time.time > searchUntil) { Leave(); return; }
                    hand = Object.FindObjectsOfType<GrantHexesController>()
                        .OrderBy(h => (h.gameObject.transform.position - transform.position).sqrMagnitude)
                        .FirstOrDefault(h => (h.gameObject.transform.position - transform.position).sqrMagnitude < 4f);
                    if (hand) Paint();
                    return;
                }
                if (!hand.Available && visuals && visuals.activeSelf)
                {
                    foreach (var ps in visuals.GetComponentsInChildren<ParticleSystem>()) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }

            private void Paint()
            {
                painted = true;
                if (!white) white = Visuals.Solid(new Color(0.95f, 0.93f, 0.88f), new Color(1f, 0.95f, 0.8f), 0.7f);
                // Only the hand's solid surfaces: the floating sigil and other transparent parts (glows, sprites)
                // keep their own materials, or the white surface shows up as an opaque box behind them.
                var node = hand.GetComponent<HallowingShrineSplitNode>();
                var sigil = node ? node.sigilRoot : null;
                var renderers = hand.GetComponentsInChildren<Renderer>(true)
                    .Where(r => (r is MeshRenderer || r is SkinnedMeshRenderer)
                        && !(sigil && r.gameObject.transform.IsChildOf(sigil))
                        && r.sharedMaterials.All(m => m && m.renderQueue < 2450))
                    .ToList();
                if (white)
                {
                    foreach (var renderer in renderers)
                    {
                        renderer.sharedMaterials = Enumerable.Repeat(white, Mathf.Max(1, renderer.sharedMaterials.Length)).ToArray();
                    }
                }
                var bounds = renderers.Where(r => r.enabled).Select(r => r.bounds).ToList();
                var center = hand.gameObject.transform.position;
                if (bounds.Count > 0)
                {
                    var all = bounds[0];
                    foreach (var b in bounds) all.Encapsulate(b);
                    center = all.center;
                }
                transform.SetParent(hand.gameObject.transform, true);
                if (visuals)
                {
                    visuals.transform.position = center;
                    visuals.SetActive(true);
                }
            }

            private void Leave()
            {
                if (NetworkServer.active) NetworkServer.Destroy(gameObject);
                else if (!GetComponent<NetworkIdentity>()) Object.Destroy(gameObject);
                enabled = false;
            }
        }
    }
}
