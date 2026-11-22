using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Poker.EditorTools
{
    /// <summary>
    /// Generates the UI images the game needs (rounded box, outline, gloss, circle, ring,
    /// glow, background, vignette, felt table) as PNGs, so the project has no external art
    /// dependency. White shapes are tinted by Image.color in the scenes.
    /// </summary>
    public static class UISprites
    {
        private const string Folder = "Assets/Art/UI/Generated";

        /// <summary>Corner radius (px) baked into the rounded sprite.</summary>
        public const float RoundedRadius = 28f;

        public static Sprite Rounded { get; private set; }
        public static Sprite Circle { get; private set; }
        public static Sprite Ring { get; private set; }
        public static Sprite Glow { get; private set; }
        public static Sprite Table { get; private set; }
        /// <summary>Rounded outline (same corners as Rounded): card borders, input focus ring.</summary>
        public static Sprite Border { get; private set; }
        /// <summary>Rounded box fading from white at the top to clear: light on buttons and cards.</summary>
        public static Sprite Gloss { get; private set; }
        /// <summary>Soft dark edges over the whole screen.</summary>
        public static Sprite Vignette { get; private set; }
        /// <summary>Deep blue vertical gradient for scene backgrounds.</summary>
        public static Sprite Backdrop { get; private set; }

        public static void Generate()
        {
            Directory.CreateDirectory(Folder);

            Rounded = Create("rounded", 96, 96, (x, y) => White(RoundedRectAlpha(x, y, 96, 96, RoundedRadius)), border: 32);
            Circle = Create("circle", 128, 128, (x, y) => White(CircleAlpha(x, y, 128, 63f)));
            Ring = Create("ring", 128, 128, (x, y) => White(RingAlpha(x, y, 128, 63f, 8f)));
            Glow = Create("glow", 128, 128, (x, y) => White(GlowAlpha(x, y, 128)));
            Table = Create("table", 1200, 600, TablePixel);
            Border = Create("border", 96, 96, (x, y) => White(BorderAlpha(x, y, 96, RoundedRadius, 2.5f)), border: 32);
            Gloss = Create("gloss", 96, 96, (x, y) => White(RoundedRectAlpha(x, y, 96, 96, RoundedRadius) * GlossFade(y, 96)), border: 32);
            Vignette = Create("vignette", 256, 256, (x, y) => new Color(0f, 0f, 0f, VignetteAlpha(x, y, 256)));
            Backdrop = Create("backdrop", 8, 256, BackdropPixel);
        }

        // ---------- New shapes ----------

        private static float BorderAlpha(int x, int y, int size, float r, float thickness)
        {
            float outer = RoundedRectAlpha(x, y, size, size, r);
            float inner = InsetRoundedRectAlpha(x, y, size, r, thickness);
            return Mathf.Clamp01(outer - inner);
        }

        private static float InsetRoundedRectAlpha(int x, int y, int size, float r, float inset)
        {
            float px = x + 0.5f - size / 2f, py = y + 0.5f - size / 2f;
            float half = size / 2f - inset, ri = Mathf.Max(1f, r - inset);
            float dx = Mathf.Max(Mathf.Abs(px) - (half - ri), 0f);
            float dy = Mathf.Max(Mathf.Abs(py) - (half - ri), 0f);
            return Mathf.Clamp01(0.5f - (Mathf.Sqrt(dx * dx + dy * dy) - ri));
        }

        /// <summary>1 at the top row, fading to 0 just past the middle.</summary>
        private static float GlossFade(int y, int h)
        {
            float t = (y + 0.5f) / h; // 0 bottom, 1 top
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, t));
        }

        private static float VignetteAlpha(int x, int y, int size)
        {
            float nx = (x + 0.5f) / size * 2f - 1f, ny = (y + 0.5f) / size * 2f - 1f;
            float d = Mathf.Sqrt(nx * nx * 0.8f + ny * ny);
            return Mathf.SmoothStep(0f, 0.55f, Mathf.InverseLerp(0.55f, 1.35f, d));
        }

        private static Color BackdropPixel(int x, int y)
        {
            Color top = new Color32(24, 36, 62, 255), bottom = new Color32(8, 13, 24, 255);
            return Color.Lerp(bottom, top, Mathf.SmoothStep(0f, 1f, (y + 0.5f) / 256f));
        }

        /// <summary>Small stable pseudo-random value per pixel (felt texture), no Random state.</summary>
        private static float Hash(int x, int y)
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 1023) / 1023f;
        }

        // ---------- Shapes (alpha with 1px anti-aliasing) ----------

        private static Color White(float alpha) => new Color(1f, 1f, 1f, alpha);

        private static float RoundedRectAlpha(int x, int y, int w, int h, float r)
        {
            float px = x + 0.5f - w / 2f, py = y + 0.5f - h / 2f;
            float dx = Mathf.Max(Mathf.Abs(px) - (w / 2f - r), 0f);
            float dy = Mathf.Max(Mathf.Abs(py) - (h / 2f - r), 0f);
            float distOutside = Mathf.Sqrt(dx * dx + dy * dy) - r;
            return Mathf.Clamp01(0.5f - distOutside);
        }

        private static float CircleAlpha(int x, int y, int size, float radius)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f));
            return Mathf.Clamp01(radius + 0.5f - d);
        }

        private static float RingAlpha(int x, int y, int size, float radius, float thickness)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f));
            float distFromBand = Mathf.Abs(d - (radius - thickness / 2f)) - thickness / 2f;
            return Mathf.Clamp01(0.5f - distFromBand);
        }

        private static float GlowAlpha(int x, int y, int size)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f));
            float t = Mathf.Clamp01(1f - d / (size / 2f));
            return t * t;
        }

        /// <summary>
        /// Oval poker table: soft shadow underneath, padded leather rim with a highlight,
        /// thin gold inlay, then felt with a fine texture, a spotlight and a shadow under the rim.
        /// </summary>
        private static Color TablePixel(int x, int y)
        {
            const int w = 1200, h = 600;
            const float margin = 22f; // room for the drop shadow
            float a = w / 2f - margin, b = h / 2f - margin;
            float nx = (x + 0.5f - w / 2f) / a, ny = (y + 0.5f - h / 2f + 6f) / b;
            float e = Mathf.Sqrt(nx * nx + ny * ny);   // 1 at the edge of the oval
            float inside = (1f - e) * b;               // approx. px from the edge, positive inside

            if (inside < -0.5f)
            {
                // Drop shadow: the oval shifted down, fading out over the margin.
                float sy = (y + 0.5f - h / 2f + 14f) / b;
                float se = Mathf.Sqrt(nx * nx + sy * sy);
                float shadow = Mathf.Clamp01(1f - (se - 1f) * b / margin);
                return new Color(0f, 0f, 0f, 0.45f * shadow * shadow);
            }
            float edgeAlpha = Mathf.Clamp01(inside + 0.5f);

            Color rimDark = new Color32(46, 26, 16, 255), rimLight = new Color32(112, 70, 40, 255);
            Color gold = new Color32(222, 180, 92, 255);
            Color feltCenter = new Color32(42, 156, 96, 255), feltEdge = new Color32(14, 84, 50, 255);

            Color c;
            const float rim = 38f;
            if (inside < rim)
            {
                // Rounded rim: lighter in its middle, a bright line where the light hits the top.
                float t = 1f - Mathf.Abs(inside / rim - 0.45f) * 2f;
                c = Color.Lerp(rimDark, rimLight, Mathf.Clamp01(t));
                float topLight = Mathf.Clamp01(-ny) * Mathf.Clamp01(1f - Mathf.Abs(inside - rim * 0.3f) / 4f);
                c = Color.Lerp(c, new Color32(170, 120, 78, 255), topLight * 0.6f);
            }
            else if (inside < rim + 3.5f) c = gold;
            else
            {
                c = Color.Lerp(feltCenter, feltEdge, Mathf.SmoothStep(0f, 1f, e));
                float rimShadow = Mathf.Clamp01(1f - (inside - rim - 3.5f) / 26f);  // shade under the rim
                c = Color.Lerp(c, Color.black, rimShadow * rimShadow * 0.45f);
                float grain = (Hash(x, y) - 0.5f) * 0.035f;                         // felt texture
                c = new Color(c.r + grain, c.g + grain, c.b + grain);
            }

            c.a = edgeAlpha;
            return c;
        }

        // ---------- PNG + importer ----------

        private static Sprite Create(string name, int width, int height, Func<int, int, Color> pixel, int border = 0)
        {
            string path = $"{Folder}/{name}.png";
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = pixel(x, y);
            tex.SetPixels(pixels);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spriteBorder = new Vector4(border, border, border, border);
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
