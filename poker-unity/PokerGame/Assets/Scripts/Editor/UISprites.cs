using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Poker.EditorTools
{
    /// <summary>
    /// Generates the few UI images the game needs (rounded box, circle, ring, glow,
    /// felt table) as PNGs, so the project has no external art dependency.
    /// White shapes are tinted by Image.color in the scenes.
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

        public static void Generate()
        {
            Directory.CreateDirectory(Folder);

            Rounded = Create("rounded", 96, 96, (x, y) => White(RoundedRectAlpha(x, y, 96, 96, RoundedRadius)), border: 32);
            Circle = Create("circle", 128, 128, (x, y) => White(CircleAlpha(x, y, 128, 63f)));
            Ring = Create("ring", 128, 128, (x, y) => White(RingAlpha(x, y, 128, 63f, 8f)));
            Glow = Create("glow", 128, 128, (x, y) => White(GlowAlpha(x, y, 128)));
            Table = Create("table", 1200, 600, TablePixel);
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

        /// <summary>Oval poker table: wooden rim, thin gold line, felt with a soft spotlight.</summary>
        private static Color TablePixel(int x, int y)
        {
            const int w = 1200, h = 600;
            float a = w / 2f - 2f, b = h / 2f - 2f;
            float nx = (x + 0.5f - w / 2f) / a, ny = (y + 0.5f - h / 2f) / b;
            float e = Mathf.Sqrt(nx * nx + ny * ny);   // 1 at the edge of the oval
            float inside = (1f - e) * b;               // approx. px from the edge, positive inside

            if (inside < -0.5f) return Color.clear;
            float edgeAlpha = Mathf.Clamp01(inside + 0.5f);

            Color rimDark = new Color32(70, 42, 24, 255), rimLight = new Color32(128, 82, 48, 255);
            Color gold = new Color32(214, 170, 82, 255);
            Color feltCenter = new Color32(40, 150, 92, 255), feltEdge = new Color32(18, 92, 56, 255);

            Color c;
            const float rim = 34f;
            if (inside < rim)
            {
                // Rim gets lighter toward its middle for a rounded look.
                float t = 1f - Mathf.Abs(inside / rim - 0.5f) * 2f;
                c = Color.Lerp(rimDark, rimLight, t);
            }
            else if (inside < rim + 3f) c = gold;
            else c = Color.Lerp(feltCenter, feltEdge, Mathf.SmoothStep(0f, 1f, e));

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
