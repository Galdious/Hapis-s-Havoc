using System.IO;
using UnityEngine;

namespace HapisHavoc.Tests
{
    /// <summary>
    /// Saves labelled PNGs under TestOutput/Captures/&lt;suite&gt;/&lt;name&gt;.png.
    ///
    /// Rendering is driven by an explicit Camera.Render() into the context's RenderTexture,
    /// never by the frame loop, so a capture cannot pick up a half-finished frame.
    /// </summary>
    public static class CaptureRig
    {
        public static string Root =>
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, "TestOutput", "Captures");

        public static string PathFor(string suite, string name)
            => Path.Combine(Root, suite, name + ".png");

        /// <summary>Full framed board from the context's pinned camera.</summary>
        public static string Capture(DeterministicContext ctx, string suite, string name)
        {
            ctx.Quiesce();
            var path = RenderAndSave(ctx.Cam, ctx.Target, suite, name);
            WriteConditions(path, ctx);
            return path;
        }

        /// <summary>
        /// Writes a sidecar recording every condition that could change what the image looks
        /// like. A golden PNG on its own is not interpretable a year later - this is what makes
        /// "the baseline changed" answerable rather than a guess.
        /// </summary>
        public static void WriteConditions(string pngPath, DeterministicContext ctx)
        {
            if (string.IsNullOrEmpty(pngPath) || ctx == null) return;
            File.WriteAllText(Path.ChangeExtension(pngPath, ".conditions.txt"),
                              ctx.Conditions + "\n");
        }

        /// <summary>
        /// One tile, isolated on a fixed neutral backdrop at a fixed angle. The camera is moved
        /// to the tile rather than the tile to the camera, so the tile's own transform (which
        /// carries the rotation/flip under test) is never disturbed.
        /// </summary>
        public static string CaptureTile(DeterministicContext ctx, TileInstance tile, string suite, string name,
                                         float distance = 3.2f)
        {
            if (tile == null) throw new System.ArgumentNullException(nameof(tile));
            ctx.Quiesce();

            var cam = ctx.Cam;
            var prevPos = cam.transform.position;
            var prevRot = cam.transform.rotation;
            var prevClear = cam.clearFlags;
            var prevBg = cam.backgroundColor;

            // Straight down, fixed yaw: a top-down view is the only angle where a tile's
            // painted path is fully visible and unforeshortened.
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.10f, 0.12f, 1f);
            cam.transform.SetPositionAndRotation(
                tile.transform.position + Vector3.up * distance,
                Quaternion.Euler(90f, 0f, 0f));

            var path = RenderAndSave(cam, ctx.Target, suite, name);
            WriteConditions(path, ctx);

            cam.clearFlags = prevClear;
            cam.backgroundColor = prevBg;
            cam.transform.SetPositionAndRotation(prevPos, prevRot);
            return path;
        }

        /// <summary>Sub-area of the framed board, in RenderTexture pixel space.</summary>
        public static string CaptureRegion(DeterministicContext ctx, RectInt region, string suite, string name)
        {
            ctx.Quiesce();

            var prevActive = RenderTexture.active;
            ctx.Cam.Render();
            RenderTexture.active = ctx.Target;

            int w = Mathf.Clamp(region.width, 1, ctx.Target.width - region.x);
            int h = Mathf.Clamp(region.height, 1, ctx.Target.height - region.y);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
            tex.ReadPixels(new Rect(region.x, region.y, w, h), 0, 0);
            tex.Apply(false);
            RenderTexture.active = prevActive;

            var path = WritePng(tex, suite, name);
            Object.DestroyImmediate(tex);
            WriteConditions(path, ctx);
            return path;
        }

        static string RenderAndSave(Camera cam, RenderTexture rt, string suite, string name)
        {
            var prevActive = RenderTexture.active;

            // Explicit synchronous render. Under URP this issues a full render of this camera
            // into its targetTexture without waiting for the player loop.
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply(false);
            RenderTexture.active = prevActive;

            var path = WritePng(tex, suite, name);
            Object.DestroyImmediate(tex);
            return path;
        }

        static string WritePng(Texture2D tex, string suite, string name)
        {
            var dir = Path.Combine(Root, suite);
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, name + ".png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            return path;
        }

        /// <summary>True if the capture is not effectively a black frame - the classic
        /// -nographics failure mode, which otherwise makes every comparison trivially pass.</summary>
        public static bool LooksRendered(string pngPath, float minNonBlackFraction = 0.02f)
        {
            var img = PixelUtil.Load(pngPath);
            int nonBlack = 0;
            for (int i = 0; i < img.Count; i++)
            {
                var p = img.Pixels[i];
                if (p.r > 8 || p.g > 8 || p.b > 8) nonBlack++;
            }
            return (float)nonBlack / img.Count >= minNonBlackFraction;
        }
    }
}
