using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HapisHavoc.Tests
{
    /// <summary>
    /// C2 and C5. Both are written BEFORE the bounds fix and are expected to be RED.
    ///
    /// C2 is the assertion whose absence let the missing-arrows bug exist: framing never
    /// collected the push affordances at all, and they appeared in captures only by coincidence
    /// of position. C5 is the permanent guard against the subtler problem - the capture rig
    /// hides hand palettes in Quiesce(), so a non-board object pulled into the bounds sweep is
    /// invisible to every golden while wrecking framing in the running game.
    /// </summary>
    [TestFixture]
    public class FramingTests
    {
        /// <summary>
        /// Everything the player must be able to TOUCH, found independently of BoardFraming so
        /// the assertion cannot be fooled by the same omission it is testing for.
        ///
        /// Arrows and locks are parented to GridManager.gridParent (not to RiverControls, which
        /// is where framing used to look). Drop zones live on a separate UI canvas.
        /// </summary>
        static List<(string name, Bounds b)> InteractiveElements()
        {
            var found = new List<(string, Bounds)>();

            static bool TryBounds(GameObject go, out Bounds b)
            {
                b = default;
                bool any = false;
                foreach (var r in go.GetComponentsInChildren<Renderer>(false))
                {
                    if (r == null || !r.enabled || r.bounds.size.sqrMagnitude <= 0f) continue;
                    if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                }
                return any;
            }

            var grid = Object.FindFirstObjectByType<GridManager>();
            if (grid != null && grid.gridParent != null)
            {
                foreach (var t in grid.gridParent.GetComponentsInChildren<Transform>(false))
                {
                    if (!t.name.StartsWith("Arrow_") && !t.name.StartsWith("Lock_")) continue;
                    if (TryBounds(t.gameObject, out var b)) found.Add((t.name, b));
                }
            }

            // Drop zones are world-space UI; their RectTransform corners are the touch target.
            foreach (var rt in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Exclude,
                                                                      FindObjectsSortMode.None))
            {
                if (!rt.name.StartsWith("DropZone_")) continue;
                var corners = new Vector3[4];
                rt.GetWorldCorners(corners);
                var b = new Bounds(corners[0], Vector3.zero);
                for (int i = 1; i < 4; i++) b.Encapsulate(corners[i]);
                if (b.size.sqrMagnitude > 0f) found.Add((rt.name, b));
            }

            foreach (var boat in Object.FindObjectsByType<BoatController>(FindObjectsInactive.Exclude,
                                                                         FindObjectsSortMode.None))
                if (TryBounds(boat.gameObject, out var b)) found.Add(("boat", b));

            foreach (var g in Object.FindObjectsByType<GoalMarker>(FindObjectsInactive.Exclude,
                                                                  FindObjectsSortMode.None))
                if (TryBounds(g.gameObject, out var b)) found.Add(("goalMarker", b));

            return found;
        }

        /// <summary>boardRect shrunk by padding, exactly as BoardFraming.Fit shrinks it.</summary>
        static Rect PaddedBoardRect(BoardLayout layout, BoardLayout.Orientation o)
        {
            var cfg = layout.For(o);
            float pad = cfg.padding * Mathf.Min(cfg.boardRect.width, cfg.boardRect.height);
            return Rect.MinMaxRect(cfg.boardRect.xMin + pad, cfg.boardRect.yMin + pad,
                                   cfg.boardRect.xMax - pad, cfg.boardRect.yMax - pad);
        }

        /// <summary>Materialises the drop zones, which otherwise only exist during a drag.</summary>
        static void EnsureDropZones(GridManager grid)
        {
            var rc = Object.FindFirstObjectByType<RiverControls>();
            if (rc == null || grid == null) return;
            for (int row = 0; row < grid.rows; row++) rc.CreateDropZonesForRow(row);
        }

        // ---------------------------------------------------------------- C2

        [UnityTest]
        public IEnumerator C2_InteractiveElementsWithinBoardRect()
        {
            LogAssert.ignoreFailingMessages = true;

            var cases = new (FixtureMode mode, string level, string label)[]
            {
                (FixtureMode.Editor,  "Levels/01_06_TestLevel", "Editor"),
                (FixtureMode.Playing, "Levels/01_06_TestLevel", "Playing"),
                (FixtureMode.Endless, null,                     "Endless"),
            };

            var lines = new List<string>();
            var failures = new List<string>();

            foreach (var (mode, level, label) in cases)
            {
                yield return SceneFixture.Load(mode, level);
                var grid = SceneFixture.Grid;
                var boat = SceneFixture.Boat;
                if (boat != null) boat.DeselectBoat();
                if (mode != FixtureMode.Editor) EnsureDropZones(grid);
                yield return new WaitForSecondsRealtime(1.0f);

                using (var ctx = new DeterministicContext())
                {
                    ctx.Quiesce();
                    ctx.FrameBoard();               // re-frame after Quiesce hid the hands
                    var rect = PaddedBoardRect(ctx.Layout, ctx.Orientation);
                    var items = InteractiveElements();

                    lines.Add($"  {label}: {items.Count} interactive element(s), " +
                              $"boardRect(padded) = {rect}");

                    if (items.Count == 0)
                        failures.Add($"{label}: found NO interactive elements to check - the " +
                                     "search is wrong, so this would pass vacuously.");

                    foreach (var (name, b) in items)
                    {
                        var v = BoardFraming.ViewportRectOf(b, ctx.Cam);
                        bool inside = rect.xMin <= v.xMin + 1e-4f && v.xMax <= rect.xMax + 1e-4f
                                   && rect.yMin <= v.yMin + 1e-4f && v.yMax <= rect.yMax + 1e-4f;
                        if (!inside)
                        {
                            lines.Add($"      OUTSIDE {name,-22} viewport x[{v.xMin:F3},{v.xMax:F3}] " +
                                      $"y[{v.yMin:F3},{v.yMax:F3}]");
                            failures.Add($"{label}/{name} outside boardRect: " +
                                         $"x[{v.xMin:F3},{v.xMax:F3}] y[{v.yMin:F3},{v.yMax:F3}]");
                        }
                    }
                }
                yield return new WaitForSecondsRealtime(0.2f);
            }

            Debug.Log("[C2] interactive elements vs boardRect\n" + string.Join("\n", lines));

            Assert.IsEmpty(failures,
                "C2: elements the player must touch are outside the framed board rect. A level " +
                "with an off-screen push arrow is literally unplayable while every tile looks " +
                "correctly framed.\n  " + string.Join("\n  ", failures));
        }

        // ---------------------------------------------------------------- C5

        [UnityTest]
        public IEnumerator C5_FramingIsIndependentOfHandVisibility()
        {
            LogAssert.ignoreFailingMessages = true;

            var lines = new List<string>();
            var failures = new List<string>();

            foreach (var (mode, label) in new[] { (FixtureMode.Editor, "Editor"),
                                                  (FixtureMode.Playing, "Playing") })
            {
                yield return SceneFixture.Load(mode, "Levels/01_06_TestLevel");
                var lem = Object.FindFirstObjectByType<LevelEditorManager>();
                Assert.IsNotNull(lem, "C5: no LevelEditorManager");
                yield return new WaitForSecondsRealtime(1.0f);

                var layout = ScriptableObject.CreateInstance<BoardLayout>();
                var orientation = BoardLayout.OrientationFor(DeterministicContext.Width,
                                                             DeterministicContext.Height);

                bool Fit(out BoardFraming.Pose pose, out Bounds b) =>
                    BoardFraming.TryFit(layout, orientation, DeterministicContext.Width,
                                        DeterministicContext.Height,
                                        BoardFraming.Projection.PerspectiveTilted, out pose, out b);

                // Hands VISIBLE - which is the state the running game is in.
                var hands = new List<GameObject>();
                foreach (var t in new[] { lem.playerHandContainer, lem.editorHandContainer })
                    if (t != null && t.gameObject.activeSelf) hands.Add(t.gameObject);

                Assert.IsTrue(Fit(out var withHands, out var boundsWith),
                              $"C5/{label}: could not frame with hands visible");

                foreach (var go in hands) go.SetActive(false);
                yield return new WaitForSecondsRealtime(0.3f);

                Assert.IsTrue(Fit(out var withoutHands, out var boundsWithout),
                              $"C5/{label}: could not frame with hands hidden");

                foreach (var go in hands) go.SetActive(true);
                Object.DestroyImmediate(layout);

                float dPos = Vector3.Distance(withHands.position, withoutHands.position);
                float dFov = Mathf.Abs(withHands.fieldOfView - withoutHands.fieldOfView);
                float dSize = Mathf.Abs(withHands.orthographicSize - withoutHands.orthographicSize);

                lines.Add($"  {label}: {hands.Count} hand container(s)\n" +
                          $"      bounds with hands    {boundsWith.min:F2}..{boundsWith.max:F2}\n" +
                          $"      bounds without hands {boundsWithout.min:F2}..{boundsWithout.max:F2}\n" +
                          $"      pose delta: position {dPos:F4}, fov {dFov:F4}, orthoSize {dSize:F4}");

                const float Eps = 0.01f;
                if (dPos > Eps || dFov > Eps || dSize > Eps)
                    failures.Add($"{label}: hiding the hand moved the camera by {dPos:F4} units " +
                                 $"(fov {dFov:F4}, orthoSize {dSize:F4})");

                yield return new WaitForSecondsRealtime(0.2f);
            }

            Debug.Log("[C5] framing vs hand visibility\n" + string.Join("\n", lines));

            Assert.IsEmpty(failures,
                "C5: the framing pose depends on whether the hand palette is visible, so hand " +
                "tiles are being collected as board geometry. The capture rig hides hands in " +
                "Quiesce(), so every golden looks fine while the running game frames differently.\n  "
                + string.Join("\n  ", failures));
        }
    }
}
