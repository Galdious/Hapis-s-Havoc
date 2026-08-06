using System;
using System.IO;
using UnityEngine;

namespace HapisHavoc.Tests
{
    /// <summary>Image analysis helpers. No Unity scene dependencies - pure pixel maths.</summary>
    public static class PixelUtil
    {
        public struct Image
        {
            public Color32[] Pixels;
            public int Width;
            public int Height;
            public int Count => Pixels.Length;
        }

        public static Image Load(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"[PixelUtil] No image at {path}");
            var bytes = File.ReadAllBytes(path);

            // The repo uses Git LFS for images (see /.gitattributes). A clone that has not run
            // `git lfs install` gets ~130-byte POINTER TEXT FILES on disk instead of PNGs. Left
            // unchecked, two pointer files would decode to nothing and compare equal, so every
            // golden assertion would pass vacuously against files containing no image at all.
            // Fail loudly instead - a silently green golden suite is the worst outcome here.
            if (LooksLikeLfsPointer(bytes))
                throw new InvalidOperationException(
                    $"[PixelUtil] {path} is a Git LFS POINTER, not an image. This clone has not " +
                    "fetched LFS content. Run `git lfs install` then `git lfs pull`. Comparing " +
                    "goldens now would pass vacuously.");

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            if (!tex.LoadImage(bytes, false))
                throw new InvalidOperationException($"[PixelUtil] Could not decode {path}");
            var img = new Image { Pixels = tex.GetPixels32(), Width = tex.width, Height = tex.height };
            UnityEngine.Object.DestroyImmediate(tex);
            return img;
        }

        /// <summary>
        /// An LFS pointer is a small ASCII file whose first line is the spec URL. Checking the
        /// magic prefix is enough and costs nothing.
        /// </summary>
        static bool LooksLikeLfsPointer(byte[] bytes)
        {
            const string magic = "version https://git-lfs.github.com/spec/v1";
            if (bytes.Length < magic.Length || bytes.Length > 1024) return false;
            for (int i = 0; i < magic.Length; i++)
                if (bytes[i] != (byte)magic[i]) return false;
            return true;
        }

        /// <summary>Byte-exact equality. Used by the determinism gate - no tolerance at all.</summary>
        public static bool BytesIdentical(string pathA, string pathB)
        {
            var a = File.ReadAllBytes(pathA);
            var b = File.ReadAllBytes(pathB);
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        public static int ChannelDelta(Color32 a, Color32 b)
        {
            int dr = Mathf.Abs(a.r - b.r);
            int dg = Mathf.Abs(a.g - b.g);
            int db = Mathf.Abs(a.b - b.b);
            return Mathf.Max(dr, Mathf.Max(dg, db));
        }

        /// <summary>Fraction of pixels whose max channel delta exceeds tolerance (0..1).</summary>
        public static float FractionDiffering(Image a, Image b, int tolerance = 4)
        {
            if (a.Width != b.Width || a.Height != b.Height)
                throw new ArgumentException($"[PixelUtil] Size mismatch: {a.Width}x{a.Height} vs {b.Width}x{b.Height}");
            int differing = 0;
            for (int i = 0; i < a.Count; i++)
                if (ChannelDelta(a.Pixels[i], b.Pixels[i]) > tolerance) differing++;
            return (float)differing / a.Count;
        }

        public static void MeanMaxDelta(Image a, Image b, out float mean, out int max)
        {
            if (a.Width != b.Width || a.Height != b.Height)
                throw new ArgumentException("[PixelUtil] Size mismatch");
            long sum = 0; max = 0;
            for (int i = 0; i < a.Count; i++)
            {
                int d = ChannelDelta(a.Pixels[i], b.Pixels[i]);
                sum += d; if (d > max) max = d;
            }
            mean = (float)sum / a.Count;
        }

        /// <summary>Fraction of pixels within tolerance of a target colour (0..1).</summary>
        public static float FractionMatching(Image img, Color32 target, int tolerance = 24)
        {
            int hit = 0;
            for (int i = 0; i < img.Count; i++)
                if (ChannelDelta(img.Pixels[i], target) <= tolerance) hit++;
            return (float)hit / img.Count;
        }

        /// <summary>Relative luminance per WCAG 2.x. Input is sRGB 0..255.</summary>
        public static double RelativeLuminance(Color32 c)
        {
            double L(double v)
            {
                v /= 255.0;
                return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
            }
            return 0.2126 * L(c.r) + 0.7152 * L(c.g) + 0.0722 * L(c.b);
        }

        /// <summary>WCAG contrast ratio, 1.0 (identical) .. 21.0 (black on white).</summary>
        public static double ContrastRatio(Color32 a, Color32 b)
        {
            double la = RelativeLuminance(a), lb = RelativeLuminance(b);
            double hi = Math.Max(la, lb), lo = Math.Min(la, lb);
            return (hi + 0.05) / (lo + 0.05);
        }

        /// <summary>Average colour over a rect, for sampling "the path colour" or "the tile colour".</summary>
        public static Color32 AverageInRect(Image img, RectInt r)
        {
            long sr = 0, sg = 0, sb = 0; int n = 0;
            int x1 = Mathf.Clamp(r.xMax, 0, img.Width), y1 = Mathf.Clamp(r.yMax, 0, img.Height);
            for (int y = Mathf.Clamp(r.yMin, 0, img.Height); y < y1; y++)
                for (int x = Mathf.Clamp(r.xMin, 0, img.Width); x < x1; x++)
                {
                    var p = img.Pixels[y * img.Width + x];
                    sr += p.r; sg += p.g; sb += p.b; n++;
                }
            if (n == 0) return new Color32(0, 0, 0, 255);
            return new Color32((byte)(sr / n), (byte)(sg / n), (byte)(sb / n), 255);
        }

        /// <summary>Pixels along a vertical scanline that are within tolerance of a colour.</summary>
        public static bool[] VerticalScan(Image img, int x, Color32 target, int tolerance = 40)
        {
            var hits = new bool[img.Height];
            if (x < 0 || x >= img.Width) return hits;
            for (int y = 0; y < img.Height; y++)
                hits[y] = ChannelDelta(img.Pixels[y * img.Width + x], target) <= tolerance;
            return hits;
        }
    }
}
