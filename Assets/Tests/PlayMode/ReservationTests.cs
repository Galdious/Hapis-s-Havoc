using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HapisHavoc.Tests
{
    /// <summary>
    /// D5 and its control X19.
    ///
    /// THE ASSERTION THAT MAKES A SIXTH DRIFT VISIBLE. The camera has to leave room for
    /// affordances before they exist - drop zones are created during a drag, and framing that
    /// reacted to them would lurch the board mid-interaction - so the reservation is computed
    /// rather than measured. That is exactly the kind of "computed twice" arrangement that goes
    /// wrong quietly: BoardFraming used to re-derive RiverControls' placement formulas, and the
    /// two drifted apart five times.
    ///
    /// RiverControls.TryGetReservedBounds is now computed by the same code that does the
    /// placing, so the two agree by construction. D5 is the check that the construction holds:
    /// every affordance that ACTUALLY EXISTS in the scene must lie inside the reservation.
    ///
    /// D5 compares the reservation against REAL OBJECTS, never against the formula that produced
    /// it. A test that recomputed the expectation would pass no matter how wrong both were.
    /// </summary>
    [TestFixture]
    public class ReservationTests
    {
        /// <summary>
        /// Every affordance present in the scene, by the same reading the framing and C2 use:
        /// meshes for arrows and locks, RectTransform world corners for drop zones.
        /// </summary>
        internal static List<(string name, Bounds b)> LiveAffordances()
        {
            var found = new List<(string, Bounds)>();

            var grid = Object.FindFirstObjectByType<GridManager>();
            if (grid != null && grid.gridParent != null)
            {
                foreach (var t in grid.gridParent.GetComponentsInChildren<Transform>(false))
                {
                    if (!t.name.StartsWith("Arrow_") && !t.name.StartsWith("Lock_")) continue;
                    bool any = false; Bounds b = default;
                    foreach (var r in t.GetComponentsInChildren<Renderer>(false))
                    {
                        if (r == null || !r.enabled || r.bounds.size.sqrMagnitude <= 0f) continue;
                        if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                    }
                    if (any) found.Add((t.name, b));
                }
            }

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

            return found;
        }

        /// <summary>
        /// X and Z only. The reservation deliberately does not constrain Y: arrows float
        /// `arrowHeight` above the tiles, and reserving that would shrink the board to frame
        /// empty air. Y is left to the renderers' own bounds.
        /// </summary>
        static bool InsideXZ(Bounds outer, Bounds inner, float tol)
            => inner.min.x >= outer.min.x - tol && inner.max.x <= outer.max.x + tol
            && inner.min.z >= outer.min.z - tol && inner.max.z <= outer.max.z + tol;

        [UnityTest]
        public IEnumerator D5_ReservationContainsEveryAffordanceThatExists()
        {
            LogAssert.ignoreFailingMessages = true;

            var cases = new (FixtureMode mode, string level, string label)[]
            {
                (FixtureMode.Editor,  "Levels/01_06_TestLevel", "Editor 3x3"),
                (FixtureMode.Playing, "Levels/01_06_TestLevel", "Playing 3x3"),
                (FixtureMode.Playing, "Levels/01_04_SimplePush", "Playing 01_04"),
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
                if (mode != FixtureMode.Editor) FramingTests.EnsureDropZones(grid);
                yield return new WaitForSecondsRealtime(1.0f);

                var rc = Object.FindFirstObjectByType<RiverControls>();
                Assert.IsNotNull(rc, $"{label}: no RiverControls, so there is nothing to check.");

                bool haveReserve = rc.TryGetReservedBounds(out var reserved);
                var live = LiveAffordances();

                lines.Add($"  {label}: {live.Count} live affordance(s), reserved=" +
                          (haveReserve
                              ? $"x[{reserved.min.x:F2},{reserved.max.x:F2}] z[{reserved.min.z:F2},{reserved.max.z:F2}]"
                              : "<none>"));

                // A reservation of nothing is only legitimate when there is nothing to reserve.
                if (!haveReserve)
                {
                    if (live.Count > 0)
                        failures.Add($"{label}: reserved NOTHING while {live.Count} affordance(s) " +
                                     "exist in the scene.");
                    continue;
                }

                if (live.Count == 0)
                    lines.Add($"      (none present to verify against - reservation unchecked here)");

                // Tolerance is a hair over float noise on world coordinates of this magnitude.
                // It is NOT a fudge factor for a wrong reservation: a genuine drift is worth
                // whole units - the gap bug alone was (cols-1)*gapX.
                const float tol = 1e-3f;

                foreach (var (name, b) in live)
                {
                    if (InsideXZ(reserved, b, tol)) continue;
                    lines.Add($"      OUTSIDE {name,-24} x[{b.min.x:F3},{b.max.x:F3}] " +
                              $"z[{b.min.z:F3},{b.max.z:F3}]");
                    failures.Add($"{label}/{name} is outside the reservation: " +
                                 $"object x[{b.min.x:F3},{b.max.x:F3}] z[{b.min.z:F3},{b.max.z:F3}] " +
                                 $"vs reserved x[{reserved.min.x:F3},{reserved.max.x:F3}] " +
                                 $"z[{reserved.min.z:F3},{reserved.max.z:F3}]");
                }
            }

            Debug.Log("[D5] reservation vs live affordances\n" + string.Join("\n", lines));

            Assert.IsEmpty(failures,
                "D5: RiverControls reserved less room than its own affordances occupy. The " +
                "reservation and the placement have drifted apart - which is the exact failure " +
                "this design exists to prevent, so fix the shared code path rather than the " +
                "tolerance.\n  " + string.Join("\n  ", failures));
        }

        /// <summary>
        /// X19 - the control for D5.
        ///
        /// OWNERSHIP THIS ENCODES: RiverControls.TryGetReservedBounds is the ONLY thing that
        /// decides how much room affordances need. The breakage therefore has to be a genuine
        /// disagreement between a placed object and the reservation, not a mutated copy of the
        /// reservation - so it MOVES A REAL DROP ZONE, by a distance smaller than the drift that
        /// prompted this rewrite, and requires D5's comparison to notice.
        ///
        /// Per CLAUDE.md: if ownership moves, rewrite this breakage so it breaks what the new
        /// owner will not put back. Never loosen D5's tolerance to resolve it.
        /// </summary>
        [UnityTest]
        public IEnumerator X19_D5_FailsWhenAPlacedZoneEscapesTheReservation()
        {
            LogAssert.ignoreFailingMessages = true;

            yield return SceneFixture.Load(FixtureMode.Playing, "Levels/01_04_SimplePush");
            var grid = SceneFixture.Grid;
            var boat = SceneFixture.Boat;
            if (boat != null) boat.DeselectBoat();
            FramingTests.EnsureDropZones(grid);
            yield return new WaitForSecondsRealtime(1.0f);

            var rc = Object.FindFirstObjectByType<RiverControls>();
            Assert.IsNotNull(rc, "X19: no RiverControls.");
            Assert.IsTrue(rc.TryGetReservedBounds(out var reserved),
                "X19: nothing reserved, so there is no reservation to escape - the control " +
                "would pass vacuously.");

            var live = LiveAffordances();
            Assert.IsNotEmpty(live, "X19: no live affordances to displace; D5 would be vacuous here too.");

            // Displace one real drop zone just past the reserved edge. 0.35 world units - well
            // under the gap-sized error the old re-derivation carried, so this proves D5 catches
            // a SMALL drift and not merely a catastrophic one.
            RectTransform victim = null;
            foreach (var rt in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Exclude,
                                                                      FindObjectsSortMode.None))
                if (rt.name.StartsWith("DropZone_")) { victim = rt; break; }

            Assert.IsNotNull(victim, "X19: no drop zone found to displace.");

            float overhang = 0.35f;
            var corners = new Vector3[4];
            victim.GetWorldCorners(corners);
            var before = new Bounds(corners[0], Vector3.zero);
            for (int i = 1; i < 4; i++) before.Encapsulate(corners[i]);

            // Push it outward past whichever X edge it is nearer to.
            bool nearMax = Mathf.Abs(reserved.max.x - before.max.x) <= Mathf.Abs(before.min.x - reserved.min.x);
            float push = nearMax ? (reserved.max.x - before.max.x) + overhang
                                 : (reserved.min.x - before.min.x) - overhang;
            victim.position += new Vector3(push, 0f, 0f);

            yield return null;

            victim.GetWorldCorners(corners);
            var after = new Bounds(corners[0], Vector3.zero);
            for (int i = 1; i < 4; i++) after.Encapsulate(corners[i]);

            bool caught = !InsideXZ(reserved, after, 1e-3f);

            Debug.Log($"[X19] displaced {victim.name} by {push:F3} on X\n" +
                      $"  reserved   x[{reserved.min.x:F3},{reserved.max.x:F3}]\n" +
                      $"  zone before x[{before.min.x:F3},{before.max.x:F3}]\n" +
                      $"  zone after  x[{after.min.x:F3},{after.max.x:F3}]\n" +
                      $"  D5 comparison reports outside = {caught}");

            Assert.IsTrue(caught,
                "X19: a drop zone moved 0.35 units beyond the reserved edge and D5's containment " +
                "check still called it inside. D5 cannot detect a placement/reservation drift, " +
                "which is the only thing it exists to detect.");
        }
    }
}
