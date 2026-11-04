using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreQuality.Hallowed
{
    // Hex of Famine's hunger bar: a thin bar right under the health bar, shown while the HUD's target holds
    // a quality Hex of Famine. It reads the hidden Hunger buff (1 = empty, 101 = full), so it works on every game.
    internal static class HungerBar
    {
        internal static void Init()
        {
            Hooks.Patch(typeof(HUD), "Awake", typeof(HungerBar), postfix: nameof(Build));
        }

        private static void Build(HUD __instance)
        {
            if (!__instance.healthBar) return;
            var parent = (RectTransform)__instance.healthBar.gameObject.transform;

            var root = new GameObject("MoreQualityHungerBar", typeof(RectTransform));
            var rect = (RectTransform)root.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -4f);
            rect.sizeDelta = new Vector2(0f, 14f);
            root.AddComponent<Image>().color = new Color(0.08f, 0.05f, 0.03f, 0.85f);

            var fill = new GameObject("Fill", typeof(RectTransform));
            var fillRect = (RectTransform)fill.transform;
            fillRect.SetParent(rect, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);
            var fillImage = fill.AddComponent<Image>();

            var label = new GameObject("Label", typeof(RectTransform));
            var labelRect = (RectTransform)label.transform;
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(6f, 0f);
            labelRect.offsetMax = Vector2.zero;
            var text = label.AddComponent<TextMeshProUGUI>();
            var style = __instance.healthBar.GetComponentInChildren<TextMeshProUGUI>(true);
            if (style) text.font = style.font;
            text.fontSize = 11f;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.text = "HUNGER";
            text.color = new Color(1f, 0.92f, 0.8f);

            var bar = root.AddComponent<Bar>();
            bar.hud = __instance;
            bar.fill = fillRect;
            bar.fillImage = fillImage;
            bar.label = text;
            root.SetActive(true);
        }

        internal sealed class Bar : MonoBehaviour
        {
            public HUD hud;
            public RectTransform fill;
            public Image fillImage;
            public TextMeshProUGUI label;
            private readonly Color full = new Color(0.85f, 0.55f, 0.2f);
            private readonly Color empty = new Color(0.8f, 0.1f, 0.1f);

            private void Update()
            {
                var body = hud && hud.targetBodyObject ? hud.targetBodyObject.GetComponent<CharacterBody>() : null;
                var stacks = body ? body.GetBuffCount(CustomBuffs.Hunger) : 0;
                var visible = stacks > 0;
                foreach (Transform child in transform) child.gameObject.SetActive(visible);
                var background = GetComponent<Image>();
                if (background) background.enabled = visible;
                if (!visible) return;

                var fraction = Mathf.Clamp01((stacks - 1) / 100f);
                fill.anchorMax = new Vector2(Mathf.Max(0.001f, fraction), 1f);
                var color = Color.Lerp(empty, full, fraction * 2f);
                if (fraction < 0.25f) color *= 0.75f + 0.25f * Mathf.Sin(Time.time * 8f);   // pulses when you're starving
                color.a = 1f;
                fillImage.color = color;
                label.text = fraction <= 0f ? "STARVING" : "HUNGER";
            }
        }
    }
}
