using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HapisHavoc.Tests
{
    /// <summary>
    /// V5, V6, T1 and the theme-swap proof.
    ///
    /// THE V5 THRESHOLD, AND WHY IT IS 3.0 AND NOT 4.5
    /// ===============================================
    /// WCAG 2.1 has two numbers and they are for different things:
    ///   - SC 1.4.3 Contrast (Minimum) wants 4.5:1, and is about BODY TEXT at ~12-14px. Thin
    ///     glyph strokes need the margin. A river path is not that.
    ///   - SC 1.4.11 Non-text Contrast wants 3:1 for "graphical objects ... required to
    ///     understand the content". A path drawn on a tile is exactly a graphical object
    ///     required to understand the content - it is the thing the player reads to plan a move.
    /// WCAG itself relaxes large-scale text to 3:1 on the reasoning that size compensates for
    /// contrast, and these paths are thick, continuous, high-contrast-edged LineRenderers many
    /// times the width of a glyph stroke. So 3.0:1 is the standard's own number for what this
    /// actually is, applied honestly rather than borrowed from a stricter clause.
    ///
    /// WHY A LUMINANCE RATIO AND NOT A HUE DISTANCE. The ratio deliberately ignores hue, and
    /// that is the point rather than a limitation. Two colours far apart in hue but close in
    /// luminance look obvious to a trichromat and can vanish entirely for a player with
    /// deuteranopia or protanopia. Luminance contrast is the colour-vision-deficiency-safe
    /// measure, which is why the accessibility standards are written in it.
    /// </summary>
    [TestFixture]
    public class ThemeTests
    {
        public const double MinPathContrast = 3.0;   // WCAG 2.1 SC 1.4.11, argued above
        const float Settle = 1.6f;

        static ThemeService ServiceInScene() => Object.FindFirstObjectByType<ThemeService>();

        // ---------------------------------------------------------------- V5

        /// <summary>
        /// V5. Every SHIPPING theme must keep its path colour readable against its tile base.
        /// Themes under Assets/Tests/BrokenControls are excluded by construction - they are
        /// reached only through Resources.Load by name, never scanned.
        /// </summary>
        [UnityTest]
        public IEnumerator V5_ThemeContrastRatio()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneFixture.Load(FixtureMode.Playing, "Levels/01_06_TestLevel");

            var svc = ServiceInScene();
            Assert.IsNotNull(svc, "V5: no ThemeService in the scene");
            var theme = svc.ReferenceTheme;
            Assert.IsNotNull(theme, "V5: ThemeService has no reference theme assigned");

            double shallow = PixelUtil.ContrastRatio(theme.pathShallow, theme.tileBase);
            double deep = PixelUtil.ContrastRatio(theme.pathDeep, theme.tileBase);
            double highlight = PixelUtil.ContrastRatio(theme.pathHighlight, theme.tileBase);

            Debug.Log($"[V5] theme '{theme.id}' contrast against tileBase {theme.tileBase}:\n" +
                      $"     pathShallow   {theme.pathShallow} -> {shallow:F3}:1\n" +
                      $"     pathDeep      {theme.pathDeep} -> {deep:F3}:1\n" +
                      $"     pathHighlight {theme.pathHighlight} -> {highlight:F3}:1\n" +
                      $"     threshold {MinPathContrast:F1}:1 (WCAG 2.1 SC 1.4.11, non-text contrast)");

            Assert.GreaterOrEqual(shallow, MinPathContrast,
                $"V5: theme '{theme.id}' draws its paths at {shallow:F3}:1 against the tile base, " +
                $"below the {MinPathContrast:F1}:1 required for a graphical object that the player " +
                "must read to plan a move. Note the ratio is luminance-only and hue-blind on " +
                "purpose: these two colours differ strongly in hue, so they look fine to a " +
                "trichromat and can disappear for a player with red-green colour blindness.");
        }

        // ---------------------------------------------------------------- X2

        /// <summary>
        /// X2 -> V5 must fail. Uses a REAL second ThemeDefinition asset, not a mock, so it also
        /// proves theme assets other than Egypt load and behave.
        /// </summary>
        [UnityTest]
        public IEnumerator X2_V5_FailsOnNearIdenticalPathAndTileColours()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return null;

            var broken = Resources.Load<ThemeDefinition>("Theme_Unreadable");
            Assert.IsNotNull(broken,
                "X2: Theme_Unreadable not found. It must live under a Resources folder in " +
                "Assets/Tests/BrokenControls so PlayMode can load it.");

            double ratio = PixelUtil.ContrastRatio(broken.pathShallow, broken.tileBase);
            Debug.Log($"[X2] broken control '{broken.id}': path {broken.pathShallow} on tile " +
                      $"{broken.tileBase} -> {ratio:F3}:1 (V5 requires {MinPathContrast:F1}:1)");

            Assert.Less(ratio, MinPathContrast,
                $"X2 META-FAILURE: the deliberately unreadable theme measures {ratio:F3}:1, which " +
                $"PASSES V5's {MinPathContrast:F1}:1 threshold. V5 cannot detect an unreadable " +
                "palette, so it proves nothing. Fix V5 or the control, not the threshold.");
        }

        // ---------------------------------------------------------------- T1

        /// <summary>
        /// T1. The layering contract: clearing a highlight must return the renderer to the
        /// THEME's value, not to a snapshot taken when the highlight began.
        ///
        /// This is the seam the "highlight stuck on" bug family lived in. It works because the
        /// two layers use different mechanisms - theme in the shared material, highlight in a
        /// MaterialPropertyBlock - so clearing the block reveals the theme rather than erasing
        /// it. The strongest form of the assertion is the one that would have caught the old
        /// bug: re-theme the tile WHILE it is highlighted, then clear, and require the result to
        /// be the NEW theme rather than what was current when the highlight started.
        /// </summary>
        [UnityTest]
        public IEnumerator T1_ClearingAHighlightReturnsToTheThemeNotASnapshot()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneFixture.Load(FixtureMode.Playing, "Levels/01_06_TestLevel");

            var grid = SceneFixture.Grid;
            var svc = ServiceInScene();
            Assert.IsNotNull(svc, "T1: no ThemeService in the scene");

            var tile = grid.GetTileAt(1, 1);
            var rend = tile.GetComponentInChildren<MeshRenderer>();
            Assert.IsNotNull(rend, "T1: tile has no MeshRenderer");

            var themeMaterialBefore = rend.sharedMaterial;

            // Highlight it, then swap theme underneath the highlight.
            HighlightService.Apply(rend, Color.magenta);
            yield return new WaitForSecondsRealtime(0.2f);

            var broken = Resources.Load<ThemeDefinition>("Theme_Unreadable");
            Assert.IsNotNull(broken, "T1: broken control theme missing");
            svc.ApplyTheme(broken);
            yield return new WaitForSecondsRealtime(0.2f);

            var themeMaterialAfterSwap = rend.sharedMaterial;

            HighlightService.Clear(rend);
            yield return new WaitForSecondsRealtime(0.2f);

            var block = new MaterialPropertyBlock();
            rend.GetPropertyBlock(block);
            bool blockIsEmpty = block.isEmpty;

            Debug.Log($"[T1] material before theme swap: {themeMaterialBefore?.name}\n" +
                      $"     material after theme swap:  {themeMaterialAfterSwap?.name}\n" +
                      $"     property block empty after Clear: {blockIsEmpty}\n" +
                      $"     material after Clear:       {rend.sharedMaterial?.name}");

            Assert.AreNotSame(themeMaterialBefore, themeMaterialAfterSwap,
                "T1 setup: swapping to the broken theme did not change the tile's material, so " +
                "this proves nothing about which layer wins.");

            Assert.IsTrue(blockIsEmpty,
                "T1: HighlightService.Clear left a non-empty MaterialPropertyBlock, so a tint is " +
                "still overriding the theme. This is the 'highlight stuck on' failure.");

            Assert.AreSame(themeMaterialAfterSwap, rend.sharedMaterial,
                "T1: after clearing the highlight the renderer is not showing the theme that is " +
                "currently active. Clearing must reveal the CURRENT theme, not restore a snapshot " +
                "captured when the highlight was applied.");

            // Put the scene back so later tests in the same run are unaffected.
            svc.ApplyTheme(svc.ReferenceTheme);
        }

        // ---------------------------------------------------------------- V6

        /// <summary>
        /// V6. Every pixel drawn on a themed tile should be explainable by the theme's declared
        /// palette. If it is not, the palette is lying about what the theme looks like - and V5,
        /// which reasons entirely from declared colours, would be checking fiction.
        ///
        /// TOLERANCE, AND WHY IT IS NOT A FLAT RGB DISTANCE. The board is lit. A diffuse surface
        /// reports its albedo scaled by incident light, so a tile whose palette colour is
        /// (0.56, 0.79, 0.90) legitimately renders anywhere along that ray - darker in shadow,
        /// brighter under the key light - without the palette being wrong. Comparing raw RGB
        /// would therefore flag correct rendering as a violation. Each pixel is instead matched
        /// against every palette colour SCALED across a plausible lighting range, and the
        /// residual after the best scale is what must be small. That residual is a chromaticity
        /// error: "this pixel is not any palette hue, at any brightness".
        /// </summary>
        [UnityTest]
        public IEnumerator V6_PaletteConformance()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneFixture.Load(FixtureMode.Playing, "Levels/01_06_TestLevel");

            var grid = SceneFixture.Grid;
            var svc = ServiceInScene();
            var boat = SceneFixture.Boat;
            Assert.IsNotNull(svc, "V6: no ThemeService in the scene");
            var theme = svc.ReferenceTheme;

            if (boat != null) boat.DeselectBoat();
            yield return new WaitForSecondsRealtime(Settle);

            string shot;
            using (var ctx = new DeterministicContext())
                shot = CaptureRig.Capture(ctx, "theme", "v6-palette");
            var img = PixelUtil.Load(shot);

            var cam = Camera.main;
            Assert.IsNotNull(cam, "V6: no main camera");

            var palette = theme.AllPaletteColours().ToList();
            var outliers = new System.Collections.Generic.List<string>();
            var residuals = SampleTileResiduals(grid, cam, img, palette, outliers);

            Assert.Greater(residuals.Count, 100,
                $"V6: only {residuals.Count} sample points landed on an actual tile. Either the " +
                "tiles have no colliders for the verification raycast, or the projection is " +
                "wrong - either way this would pass vacuously.");

            residuals.Sort();
            float median = residuals[residuals.Count / 2];
            float p95 = residuals[Mathf.Min(residuals.Count - 1, (int)(residuals.Count * 0.95f))];
            float worst = residuals[residuals.Count - 1];

            Debug.Log($"[V6] theme '{theme.id}': {residuals.Count} tile pixels sampled against " +
                      $"{palette.Count} declared palette colours\n" +
                      $"     residual after best lighting scale: median={median:F1}/255 " +
                      $"p95={p95:F1}/255 worst={worst:F1}/255  (threshold p95 <= {MaxPaletteResidual}/255)\n" +
                      $"     {outliers.Count} pixel(s) over threshold:\n       " +
                      string.Join("\n       ", outliers.Take(14)));

            Assert.LessOrEqual(p95, MaxPaletteResidual,
                $"V6: 5% of sampled tile pixels are more than {p95:F1}/255 away from ANY declared " +
                $"palette colour at any plausible brightness. The theme's palette does not " +
                "describe what is actually drawn, so V5's reasoning from declared colours is " +
                "checking something the player never sees.");
        }

        /// <summary>
        /// X12 -> V6 must fail. Repaints the board with the Unreadable theme's materials while
        /// checking the pixels against EGYPT's declared palette. The palette now describes a
        /// board that is not on screen, which is exactly the condition V6 exists to catch.
        /// </summary>
        [UnityTest]
        public IEnumerator X12_V6_FailsWhenThePaletteDoesNotDescribeTheBoard()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneFixture.Load(FixtureMode.Playing, "Levels/01_06_TestLevel");

            var grid = SceneFixture.Grid;
            var svc = ServiceInScene();
            var boat = SceneFixture.Boat;
            var egyptPalette = svc.ReferenceTheme.AllPaletteColours().ToList();

            // THE BREAKAGE: paint the board with a completely different theme.
            svc.ApplyTheme(Resources.Load<ThemeDefinition>("Theme_Unreadable"));
            if (boat != null) boat.DeselectBoat();
            yield return new WaitForSecondsRealtime(Settle);

            string shot;
            using (var ctx = new DeterministicContext())
                shot = CaptureRig.Capture(ctx, "theme", "x12-mismatched-palette");
            var img = PixelUtil.Load(shot);
            var cam = Camera.main;

            var residuals = SampleTileResiduals(grid, cam, img, egyptPalette);

            // A test that THROWS reports nothing. residuals goes empty when the framing moves
            // the tiles out from under the sample projection, and Mathf.Min(Count-1, ...) is -1
            // at Count 0. Fail with the reason instead of an IndexOutOfRange.
            Assert.IsNotEmpty(residuals,
                "X12: no sample point landed on a tile, so the control measured nothing. The " +
                "framing changed under it - fix the sampling, do not relax the assertion.");

            residuals.Sort();
            float p95 = residuals[Mathf.Min(residuals.Count - 1, (int)(residuals.Count * 0.95f))];

            Debug.Log($"[X12] board painted Unreadable, checked against EGYPT's palette: " +
                      $"p95={p95:F1}/255 (V6 allows {MaxPaletteResidual}/255)");

            svc.ApplyTheme(svc.ReferenceTheme);

            Assert.Greater(p95, MaxPaletteResidual,
                $"X12 META-FAILURE: the board was repainted with an entirely different theme, yet " +
                $"95% of tile pixels still sit within {p95:F1}/255 of Egypt's palette. V6 cannot " +
                "tell a palette that describes the board from one that does not, so it proves " +
                "nothing. Fix V6, not this control.");
        }


        /// <summary>
        /// Samples the middle of every tile face and returns each pixel's residual against the
        /// supplied palette. Shared by V6 and X12 so the control exercises the SAME comparison.
        /// Projected through the camera, never hardcoded pixel coordinates - the V1b lesson.
        /// </summary>
        static System.Collections.Generic.List<float> SampleTileResiduals(
            GridManager grid, Camera cam, PixelUtil.Image img,
            System.Collections.Generic.List<Color> palette,
            System.Collections.Generic.List<string> outliers = null)
        {
            var residuals = new System.Collections.Generic.List<float>();

            for (int y = 0; y < grid.rows; y++)
            for (int x = 0; x < grid.cols; x++)
            {
                var tile = grid.GetTileAt(x, y);
                if (tile == null) continue;
                var rend = tile.GetComponentInChildren<MeshRenderer>();
                if (rend == null) continue;

                var b = rend.bounds;
                for (int sx = 0; sx <= 4; sx++)
                for (int sy = 0; sy <= 4; sy++)
                {
                    // Sample across the tile face. The inset is relative to THE TILE, not to the
                    // frame, so reframing does not move the sample points off their subject.
                    var world = new Vector3(
                        Mathf.Lerp(b.min.x, b.max.x, 0.35f + 0.075f * sx),
                        b.max.y,
                        Mathf.Lerp(b.min.z, b.max.z, 0.35f + 0.075f * sy));
                    var sp = cam.WorldToScreenPoint(world);
                    if (sp.z <= 0) continue;

                    // VERIFY THE SAMPLE IS ACTUALLY ON THIS TILE before judging its colour.
                    // Projecting a bounding box overshoots the silhouette under perspective, so
                    // edge tiles used to sample the backdrop and report it as a palette
                    // violation - measured at rgb(208,153,4), the background. A raycast asks the
                    // geometry rather than trusting the projection.
                    if (!Physics.Raycast(cam.ScreenPointToRay(sp), out var hit, 500f)) continue;
                    if (hit.transform != tile.transform && !hit.transform.IsChildOf(tile.transform))
                        continue;

                    int px = Mathf.RoundToInt(sp.x * img.Width / cam.pixelWidth);
                    int py = Mathf.RoundToInt(sp.y * img.Height / cam.pixelHeight);
                    if (px < 0 || py < 0 || px >= img.Width || py >= img.Height) continue;

                    var c = img.Pixels[py * img.Width + px];   // bottom-left origin, no flip
                    float r = BestPaletteResidual(c, palette);
                    residuals.Add(r);
                    if (outliers != null && r > MaxPaletteResidual)
                        outliers.Add($"({x},{y}) rgb({c.r},{c.g},{c.b}) residual={r:F1}");
                }
            }
            return residuals;
        }

        public const float MaxPaletteResidual = 40f;   // /255, after best-fit lighting scale

        /// <summary>
        /// Smallest residual between a rendered pixel and any palette colour, allowing that
        /// colour to be scaled by incident light. Returns 0..255.
        /// </summary>
        static float BestPaletteResidual(Color32 pixel, System.Collections.Generic.List<Color> palette)
        {
            float best = float.MaxValue;
            Vector3 p = new Vector3(pixel.r, pixel.g, pixel.b);

            foreach (var c in palette)
            {
                Vector3 q = new Vector3(c.r * 255f, c.g * 255f, c.b * 255f);
                if (q.sqrMagnitude < 1f) { best = Mathf.Min(best, p.magnitude); continue; }

                // Least-squares best scale of q onto p, clamped to a plausible lighting range.
                //
                // THIS RANGE IS THE WHOLE TEST. Opened too wide it becomes a chromaticity-only
                // comparison, and with 13 palette colours spanning a broad gamut almost any
                // pixel finds SOME entry it is parallel to - measured: at [0.25, 1.75] a board
                // repainted with an entirely different theme scored p95 7.8/255 against the
                // ORIGINAL palette, better than the correct board's 9.4. X12 caught that.
                // Narrow enough to still allow real shading, tight enough to discriminate.
                float s = Mathf.Clamp(Vector3.Dot(p, q) / q.sqrMagnitude, 0.85f, 1.15f);
                best = Mathf.Min(best, (p - q * s).magnitude / Mathf.Sqrt(3f));
            }
            return best;
        }

        // ---------------------------------------------------------------- theme swap is visible

        /// <summary>
        /// Proves swapping themes actually changes what is drawn. Without this, V5 and V6 could
        /// both pass against a ThemeService that silently does nothing.
        /// </summary>
        [UnityTest]
        public IEnumerator T2_SwappingThemeVisiblyChangesTheBoard()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneFixture.Load(FixtureMode.Playing, "Levels/01_06_TestLevel");

            var svc = ServiceInScene();
            var boat = SceneFixture.Boat;
            if (boat != null) boat.DeselectBoat();
            yield return new WaitForSecondsRealtime(Settle);

            string before, after;
            using (var ctx = new DeterministicContext())
                before = CaptureRig.Capture(ctx, "theme", "t2-egypt");

            var broken = Resources.Load<ThemeDefinition>("Theme_Unreadable");
            svc.ApplyTheme(broken);
            yield return new WaitForSecondsRealtime(0.6f);

            using (var ctx = new DeterministicContext())
                after = CaptureRig.Capture(ctx, "theme", "t2-unreadable");

            float diff = PixelUtil.FractionDiffering(PixelUtil.Load(before), PixelUtil.Load(after), 8);
            Debug.Log($"[T2] swapping Egypt -> Unreadable changed {diff:P3} of pixels");

            svc.ApplyTheme(svc.ReferenceTheme);

            Assert.Greater(diff, 0.02f,
                $"T2: swapping to a completely different theme changed only {diff:P3} of the " +
                "frame. ThemeService is not actually applying materials, which would make every " +
                "other theme assertion vacuous.");
        }
    }
}
