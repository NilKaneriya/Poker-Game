using System.IO;
using Poker.Reactions;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Poker.EditorTools.UIFactory;
using static Poker.Reactions.ReactionConstants;

namespace Poker.EditorTools.Reactions
{
    /// <summary>
    /// Builds the reactions UI into a scene: the overlay that plays emotes, the picker
    /// pop-up and the buttons that open it. Part of the reactions module; scene builders
    /// only call these methods and wire the result to their own screen.
    /// </summary>
    public static class ReactionUIBuilder
    {
        private const string ArtFolder = "Assets/Art/Reactions";
        private const string SetPath = ArtFolder + "/EmoteSet.asset";

        // ---------- Art ----------

        /// <summary>Imports emote_{id}.png as sprites and (re)builds the EmoteSet asset in emote-id order.</summary>
        public static EmoteSet LoadEmoteSet()
        {
            var entries = new EmoteSet.Entry[Emotes.Ids.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                string id = Emotes.Ids[i];
                string path = $"{ArtFolder}/emote_{id}.png";
                if (!File.Exists(path)) Debug.LogWarning($"[Reactions] Missing art for emote '{id}': {path}");
                else ImportAsSprite(path);
                entries[i] = new EmoteSet.Entry { id = id, sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path) };
            }

            var set = AssetDatabase.LoadAssetAtPath<EmoteSet>(SetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<EmoteSet>();
                AssetDatabase.CreateAsset(set, SetPath);
            }
            set.SetEntries(entries);
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            return set;
        }

        private static void ImportAsSprite(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.textureType == TextureImporterType.Sprite && !importer.mipmapEnabled) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 256;
            importer.SaveAndReimport();
        }

        // ---------- Overlay ----------

        /// <summary>Full-screen layer that plays reactions. Build it above whatever the anchors are.</summary>
        public static ReactionLayer BuildLayer(Transform canvas, EmoteSet set)
        {
            var root = Node("Reactions", canvas).Fill();
            var rootGroup = Group(root);
            rootGroup.blocksRaycasts = rootGroup.interactable = false; // never in the way of clicks

            var item = Node("ItemTemplate", root).At(0, 0, 120, 120);
            Group(item);
            var icon = Img(item, "Icon", set.Count > 0 ? set[0].sprite : null, Color.white);
            icon.type = Image.Type.Simple;
            icon.preserveAspect = true;
            icon.rectTransform.Fill();
            var caption = MakeText(item, "Caption", "Name", 22, Color.white, bold: true);
            caption.rectTransform.At(0, -80, 280, 32);
            caption.outlineWidth = 0.2f;
            caption.outlineColor = new Color32(0, 0, 0, 200);

            var fallback = Node("FallbackAnchor", root).At(0, 300, 10, 10);

            var layer = root.gameObject.AddComponent<ReactionLayer>();
            Wire(layer, "emotes", set);
            Wire(layer, "itemTemplate", item);
            Wire(layer, "fallbackAnchor", fallback);
            return layer;
        }

        // ---------- Buttons ----------

        /// <summary>"Emotes" button with a small emote icon, styled like the other corner buttons.</summary>
        public static Button BuildOpenButton(Transform parent, EmoteSet set, Vector2 anchor, float x, float y)
        {
            var button = MakeButton(parent, "EmoteButton", Texts.Button, Theme.Dark.WithAlpha(0.85f), Theme.Gold, 24);
            button.GetComponent<RectTransform>().Pin(anchor, x, y, 180, 60);
            LabelOf(button).rectTransform.Fill(56, 10, 4, 4);
            AddIcon(button.transform, set, 0, new Vector2(0f, 0.5f), 12, 0, 40);
            return button;
        }

        /// <summary>Square icon-only button that fills a slot (e.g. the chat window's tool slot).</summary>
        public static Button BuildIconButton(RectTransform slot, EmoteSet set)
        {
            var button = MakeButton(slot, "EmoteButton", "", Theme.PanelLight, Theme.Text, 20, 16f);
            button.GetComponent<RectTransform>().Fill();
            Object.DestroyImmediate(LabelOf(button).gameObject);
            AddIcon(button.transform, set, 0, new Vector2(0.5f, 0.5f), 0, 0, 42);
            return button;
        }

        private static void AddIcon(Transform parent, EmoteSet set, int index, Vector2 anchor, float x, float y, float size)
        {
            var icon = Img(parent, "Icon", set.Count > index ? set[index].sprite : null, Color.white);
            icon.type = Image.Type.Simple;
            icon.preserveAspect = true;
            icon.rectTransform.Pin(anchor, x, y, size, size);
        }

        // ---------- Picker ----------

        public static EmotePicker BuildPicker(Transform canvas, EmoteSet set)
        {
            var root = Node("EmotePicker", canvas).Fill();
            var group = Group(root, 0f);
            var dim = Img(root, "Dim", null, Color.black.WithAlpha(0.45f), raycast: true);
            dim.rectTransform.Fill();
            var backdrop = dim.gameObject.AddComponent<Button>();
            backdrop.transition = Selectable.Transition.None;

            var card = Card(root, "Card", Theme.Panel, 30f);
            card.raycastTarget = true; // clicks on the card must not reach the backdrop
            card.rectTransform.At(0, -40, 760, 560);
            Panel(card.transform, "HeaderBar", Theme.Gold, 7f).rectTransform.Pin(new Vector2(0.5f, 1f), 0, -16, 220, 12);
            var title = MakeText(card.transform, "Title", Texts.Title, 34, Theme.Gold, TextAlignmentOptions.Left, bold: true);
            title.rectTransform.Pin(new Vector2(0f, 1f), 36, -34, 560, 48);
            var close = MakeButton(card.transform, "CloseButton", "X", Theme.PanelLight, Theme.Text, 24, 20f);
            close.GetComponent<RectTransform>().Pin(new Vector2(1f, 1f), -26, -28, 56, 56);

            // Who it is for: "Everyone" + one chip per player (two rows of five fit a full table)
            var targets = Node("Targets", card.transform).Pin(new Vector2(0.5f, 1f), 0, -104, 690, 100);
            var targetGrid = targets.gameObject.AddComponent<GridLayoutGroup>();
            targetGrid.cellSize = new Vector2(130, 42);
            targetGrid.spacing = new Vector2(10, 12);
            targetGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            targetGrid.constraintCount = 5;
            targetGrid.childAlignment = TextAnchor.UpperCenter;
            var chip = MakeButton(targets, "TargetTemplate", Texts.Everyone, Color.white.WithAlpha(0.08f), Color.white, 20, 21f);

            // Emotes: two rows of six
            var grid = Node("Emotes", card.transform).Pin(new Vector2(0.5f, 0f), 0, 34, 690, 228);
            var emoteGrid = grid.gameObject.AddComponent<GridLayoutGroup>();
            emoteGrid.cellSize = new Vector2(104, 104);
            emoteGrid.spacing = new Vector2(10, 18);
            emoteGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            emoteGrid.constraintCount = 6;
            emoteGrid.childAlignment = TextAnchor.MiddleCenter;
            var emote = MakeButton(grid, "EmoteTemplate", "", Theme.PanelLight.WithAlpha(0.6f), Theme.Text, 20, 22f);
            Object.DestroyImmediate(LabelOf(emote).gameObject);
            AddIcon(emote.transform, set, 0, new Vector2(0.5f, 0.5f), 0, 0, 80);

            var picker = root.gameObject.AddComponent<EmotePicker>();
            Wire(picker, "emotes", set);
            Wire(picker, "group", group);
            Wire(picker, "card", card.rectTransform);
            Wire(picker, "closeButton", close);
            Wire(picker, "backdropButton", backdrop);
            Wire(picker, "titleText", title);
            Wire(picker, "targetRow", targets);
            Wire(picker, "targetTemplate", chip);
            WireColor(picker, "chipSelectedColor", Theme.Gold);
            WireColor(picker, "chipSelectedTextColor", Theme.Dark);
            Wire(picker, "emoteGrid", grid);
            Wire(picker, "emoteTemplate", emote);
            return picker;
        }
    }
}
