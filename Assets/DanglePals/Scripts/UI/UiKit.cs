using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DanglePals
{
    /// <summary>Small builders for uGUI so screens read as layout, not boilerplate.</summary>
    public static class UiKit
    {
        public static readonly Color Ink = Res.Hex("#3B2A1C"), Body = Res.Hex("#6A5442"), Paper = Res.Hex("#FFFBF2"),
            Leaf = Res.Hex("#2D4A3A"), Muted = Res.Hex("#6F8577"), Cream = Res.Hex("#F6EEDF");

        public static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        public static Image Img(Transform parent, string sprite, Color color, Vector2 size, bool sliced = false, bool raycast = false)
        {
            var rt = Node(sprite ?? "rect", parent);
            rt.sizeDelta = size;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite != null ? Res.Sprite(sprite) : null;
            img.color = color;
            img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            img.raycastTarget = raycast;
            return img;
        }

        public static Image PalImg(Transform parent, string id, string mood, float size)
        {
            var rt = Node(id, parent);
            rt.sizeDelta = new Vector2(size, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Res.Pal(id, mood);
            img.raycastTarget = false;
            img.preserveAspect = true;
            return img;
        }

        public static TextMeshProUGUI Text(Transform parent, string text, float size, Color color, bool bold = false, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var rt = Node("text", parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = Res.Font(bold);
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        static readonly System.Collections.Generic.Dictionary<string, Color> LabelColors = new System.Collections.Generic.Dictionary<string, Color>
        {
            { "blue", Color.white }, { "green", Color.white }, { "red", Color.white }, { "gold", Res.Hex("#5A3200") }, { "ghost", Res.Hex("#6A4A2C") },
        };

        public static Button Button(Transform parent, string style, string label, float width, float height, Action onClick, float fontSize = 19, string icon = null)
        {
            var img = Img(parent, "btn_" + style, Color.white, new Vector2(width, height), true, true);
            img.gameObject.name = "btn " + label;
            var b = img.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            if (onClick != null) b.onClick.AddListener(() => onClick());
            img.gameObject.AddComponent<PressScale>();
            var t = Text(img.transform, label, fontSize, LabelColors[style], true);
            Stretch(t.rectTransform);
            t.rectTransform.offsetMin = new Vector2(6, 6);
            t.rectTransform.offsetMax = new Vector2(-6, 0);
            if (icon != null)
            {
                t.text = "    " + label; // room for the icon image on the left
                var ic = Img(img.transform, icon, LabelColors[style], new Vector2(14, 14));
                ic.rectTransform.anchoredPosition = new Vector2(-t.GetPreferredValues(label).x / 2f - 6f, 2f);
            }
            var le = img.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width; le.preferredHeight = height;
            return b;
        }

        public static Button LinkButton(Transform parent, string label, Color color, Action onClick, float size = 15)
        {
            var t = Text(parent, "<u>" + label + "</u>", size, color, true);
            t.raycastTarget = true;
            t.rectTransform.sizeDelta = new Vector2(t.GetPreferredValues().x + 16, 34);
            var b = t.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            if (onClick != null) b.onClick.AddListener(() => onClick());
            var le = t.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 34;
            return b;
        }

        public static LayoutElement Layout(Component c, float w = -1, float h = -1)
        {
            if (!c.TryGetComponent<LayoutElement>(out var le)) le = c.gameObject.AddComponent<LayoutElement>();
            if (w >= 0) le.preferredWidth = w;
            if (h >= 0) le.preferredHeight = h;
            return le;
        }

        public static HorizontalLayoutGroup Row(Transform parent, float spacing = 10, float height = -1)
        {
            var rt = Node("row", parent);
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            if (height > 0) Layout(h, -1, height);
            return h;
        }
    }
}
