using System;
using Poker.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Poker.EditorTools
{
    /// <summary>
    /// Small helpers to create uGUI/TextMeshPro objects from code with consistent styling.
    /// Positions use a centre anchor unless stated otherwise (Unity UI units, 1920x1080 reference).
    /// </summary>
    public static class UIFactory
    {
        public static TMP_FontAsset Font;

        // ---------- Rect helpers ----------

        public static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = LayerMask.NameToLayer("UI") };
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Centre-anchored position and size.</summary>
        public static RectTransform At(this RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>Anchor to a point of the parent (e.g. (1,0) = bottom right) with a matching pivot.</summary>
        public static RectTransform Pin(this RectTransform rt, Vector2 anchor, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>Stretch to fill the parent with margins.</summary>
        public static RectTransform Fill(this RectTransform rt, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        // ---------- Graphics ----------

        /// <summary>Image; rounded sprites are 9-sliced and 'radius' sets the visible corner size.</summary>
        public static Image Img(Transform parent, string name, Sprite sprite, Color color, float radius = 18f, bool raycast = false)
        {
            var img = Node(name, parent).gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            if (sprite != null && sprite.border != Vector4.zero)
            {
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = UISprites.RoundedRadius / Mathf.Max(1f, radius);
            }
            return img;
        }

        public static Image Panel(Transform parent, string name, Color color, float radius = 22f) =>
            Img(parent, name, UISprites.Rounded, color, radius);

        public static TextMeshProUGUI MakeText(Transform parent, string name, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Center, bool bold = false)
        {
            var tmp = Node(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = Font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            tmp.enableWordWrapping = true;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.raycastTarget = false;
            return tmp;
        }

        // ---------- Controls ----------

        public static Button MakeButton(Transform parent, string name, string label, Color background, Color textColor,
            float fontSize = 28f, float radius = 18f)
        {
            var img = Img(parent, name, UISprites.Rounded, background, radius, raycast: true);
            var button = img.gameObject.AddComponent<Button>();
            img.gameObject.AddComponent<ButtonClickSound>(); // every button clicks
            button.targetGraphic = img;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.45f, 0.45f, 0.5f, 0.6f);
            button.colors = colors;

            var text = MakeText(img.transform, "Label", label, fontSize, textColor, TextAlignmentOptions.Center, bold: true);
            text.rectTransform.Fill(8, 8, 4, 4);
            text.enableAutoSizing = true;
            text.fontSizeMin = 14;
            text.fontSizeMax = fontSize;
            return button;
        }

        public static TMP_Text LabelOf(Button button) => button.GetComponentInChildren<TMP_Text>();

        public static TMP_InputField MakeInput(Transform parent, string name, string placeholder, float fontSize = 28f)
        {
            var bg = Img(parent, name, UISprites.Rounded, Theme.Input, 16f, raycast: true);
            var area = Node("Text Area", bg.transform).Fill(20, 20, 6, 6);
            area.gameObject.AddComponent<RectMask2D>();

            var hint = MakeText(area, "Placeholder", placeholder, fontSize, Theme.Muted.WithAlpha(0.7f), TextAlignmentOptions.Left);
            hint.fontStyle = FontStyles.Italic;
            hint.enableWordWrapping = false;
            hint.rectTransform.Fill();

            var text = MakeText(area, "Text", "", fontSize, Theme.Text, TextAlignmentOptions.Left);
            text.enableWordWrapping = false;
            text.rectTransform.Fill();

            var input = bg.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = bg;
            input.textViewport = area;
            input.textComponent = text;
            input.placeholder = hint;
            input.fontAsset = Font;
            input.pointSize = fontSize;
            input.customCaretColor = true;
            input.caretColor = Theme.Gold;
            input.caretWidth = 3;
            input.selectionColor = Theme.Gold.WithAlpha(0.35f);
            return input;
        }

        public static Toggle MakeToggle(Transform parent, string name, string label, bool isOn)
        {
            var root = Node(name, parent);
            var box = Img(root, "Box", UISprites.Rounded, Theme.Input, 10f, raycast: true);
            box.rectTransform.Pin(new Vector2(0f, 0.5f), 0, 0, 40, 40);
            var check = Img(box.transform, "Check", UISprites.Rounded, Theme.Gold, 6f);
            check.rectTransform.At(0, 0, 24, 24);
            var text = MakeText(root, "Label", label, 24, Theme.Text, TextAlignmentOptions.Left);
            text.rectTransform.Fill(56, 0, 0, 0);
            text.raycastTarget = true; // clicking the label toggles too

            var toggle = root.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = box;
            toggle.graphic = check;
            toggle.isOn = isOn;
            return toggle;
        }

        public static Slider MakeSlider(Transform parent, string name)
        {
            var root = Node(name, parent);
            var track = Img(root, "Background", UISprites.Rounded, Theme.Input, 6f);
            track.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            track.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            track.rectTransform.sizeDelta = new Vector2(0f, 12f);

            var fillArea = Node("Fill Area", root);
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.sizeDelta = new Vector2(-24f, 12f);
            var fill = Img(fillArea, "Fill", UISprites.Rounded, Theme.Gold, 6f);
            fill.rectTransform.sizeDelta = new Vector2(12f, 0f);

            var handleArea = Node("Handle Slide Area", root).Fill(14, 14, 0, 0);
            var handle = Img(handleArea, "Handle", UISprites.Circle, Theme.Text, raycast: true);
            handle.rectTransform.sizeDelta = new Vector2(34f, 0f);

            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.wholeNumbers = true;
            return slider;
        }

        /// <summary>Vertical scrolling list. Returns the content transform to add rows to.</summary>
        public static RectTransform ScrollList(Transform parent, string name, out ScrollRect scroll)
        {
            var root = Img(parent, name, null, Color.clear, raycast: true); // invisible, catches drag
            var viewport = Node("Viewport", root.transform).Fill();
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = Node("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            return content;
        }

        public static CanvasGroup Group(Component c, float alpha = 1f)
        {
            var group = c.gameObject.AddComponent<CanvasGroup>();
            group.alpha = alpha;
            return group;
        }

        // ---------- Wiring [SerializeField] references ----------

        public static void Wire(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field)
                ?? throw new ArgumentException($"{target.GetType().Name} has no serialized field '{field}'");
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Set a serialized enum field (by its C# value).</summary>
        public static void WireEnum(Object target, string field, Enum value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field)
                ?? throw new ArgumentException($"{target.GetType().Name} has no serialized field '{field}'");
            prop.enumValueIndex = Array.IndexOf(Enum.GetValues(value.GetType()), value);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void WireArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field)
                ?? throw new ArgumentException($"{target.GetType().Name} has no serialized field '{field}'");
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
