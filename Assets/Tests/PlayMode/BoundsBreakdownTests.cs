using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HapisHavoc.Tests
{
    /// <summary>
    /// ITEM 1. Breaks the framing bounds down by contributor so the fix targets the thing that
    /// actually binds, rather than the thing that looks suspicious.
    ///
    /// Reports, not asserts - except for a guard that the measurement is not empty. The only
    /// number that decides anything is which term binds the FIT, so that is computed explicitly
    /// rather than left to be inferred from the AABB.
    /// </summary>
    [TestFixture]
    public class BoundsBreakdownTests
    {
        class Cat
        {
            public string name;
            public Bounds b;
            public bool any;
            public int n;
            public void Add(Renderer r)
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) return;
                if (r.bounds.size.sqrMagnitude <= 0f) return;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                n++;
            }
        }

        static List<Cat> Categorise()
        {
            var tiles = new Cat { name = "grid tiles" };
            var bankTop = new Cat { name = "top bank" };
            var bankBottom = new Cat { name = "bottom bank" };
            var arrows = new Cat { name = "push arrows" };
            var zones = new Cat { name = "drop zones" };
            var locks = new Cat { name = "row locks" };
            var goal = new Cat { name = "goal marker" };
            var boat = new Cat { name = "boat" };

            foreach (var t in Object.FindObjectsByType<BoardTile>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                foreach (var r in t.GetComponentsInChildren<Renderer>(false)) tiles.Add(r);

            // Banks split by which side of the tile block they sit on.
            float midZ = tiles.any ? tiles.b.center.z : 0f;
            foreach (var bm in Object.FindObjectsByType<RiverBankManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                foreach (var r in bm.GetComponentsInChildren<Renderer>(false))
                {
                    if (r == null || r.bounds.size.sqrMagnitude <= 0f) continue;
                    (r.bounds.center.z >= midZ ? bankTop : bankBottom).Add(r);
                }

            foreach (var rc in Object.FindObjectsByType<RiverControls>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                foreach (var r in rc.GetComponentsInChildren<Renderer>(false))
                {
                    if (r == null) continue;
                    string n = r.transform.root == r.transform ? r.name : NameOfOwner(r.transform);
                    if (n.StartsWith("Arrow")) arrows.Add(r);
                    else if (n.StartsWith("DropZone")) zones.Add(r);
                    else if (n.StartsWith("Lock")) locks.Add(r);
                    else arrows.Add(r);          // unnamed children of the arrow prefabs
                }

            foreach (var g in Object.FindObjectsByType<GoalMarker>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                foreach (var r in g.GetComponentsInChildren<Renderer>(false)) goal.Add(r);
            foreach (var bc in Object.FindObjectsByType<BoatController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                foreach (var r in bc.GetComponentsInChildren<Renderer>(false)) boat.Add(r);

            return new List<Cat> { tiles, bankTop, bankBottom, arrows, zones, locks, goal, boat };
        }

        /// <summary>Walks up to the first ancestor whose name identifies the affordance.</summary>
        static string NameOfOwner(Transform t)
        {
            for (var c = t; c != null; c = c.parent)
                if (c.name.StartsWith("Arrow") || c.name.StartsWith("DropZone") || c.name.StartsWith("Lock"))
                    return c.name;
            return t.name;
        }

        static string Row(Cat c)
        {
            if (!c.any) return $"    {c.name,-14} (absent)";
            var mn = c.b.min; var mx = c.b.max; var sz = c.b.size;
            return $"    {c.name,-14} n={c.n,-4} " +
                   $"X[{mn.x,7:F2},{mx.x,7:F2}]={sz.x,6:F2}  " +
                   $"Y[{mn.y,7:F2},{mx.y,7:F2}]={sz.y,6:F2}  " +
                   $"Z[{mn.z,7:F2},{mx.z,7:F2}]={sz.z,6:F2}";
        }

        static string BinderOn(List<Cat> cats, System.Func<Bounds, float> pick, bool max)
        {
            Cat best = null;
            foreach (var c in cats)
            {
                if (!c.any) continue;
                if (best == null) { best = c; continue; }
                float a = pick(c.b), b = pick(best.b);
                if (max ? a > b : a < b) best = c;
            }
            return best != null ? best.name : "<none>";
        }

        [UnityTest]
        public IEnumerator B1_BoundsBreakdown()
        {
            LogAssert.ignoreFailingMessages = true;

            var cases = new (FixtureMode mode, string level, string label)[]
            {
                (FixtureMode.Playing, "Levels/01_06_TestLevel", "Playing 3x3"),
                (FixtureMode.Editor,  "Levels/01_06_TestLevel", "Editor 3x3"),
                (FixtureMode.Endless, null,                     "Endless (streamed)"),
            };

            var report = new List<string>();

            foreach (var (mode, level, label) in cases)
            {
                yield return SceneFixture.Load(mode, level);
                var grid = SceneFixture.Grid;
                var boat = SceneFixture.Boat;
                if (boat != null) boat.DeselectBoat();
                yield return new WaitForSecondsRealtime(1.2f);

                var cats = Categorise();
                BoardFraming.TryCollectBoardBounds(out var union, out _);

                report.Add($"\n  === {label}  (grid {grid.cols}x{grid.rows}, " +
                           $"tile {grid.tileWidth}x{grid.tileHeight}) ===");
                foreach (var c in cats) report.Add(Row(c));
                report.Add($"    {"UNION",-14}       " +
                           $"X[{union.min.x,7:F2},{union.max.x,7:F2}]={union.size.x,6:F2}  " +
                           $"Y[{union.min.y,7:F2},{union.max.y,7:F2}]={union.size.y,6:F2}  " +
                           $"Z[{union.min.z,7:F2},{union.max.z,7:F2}]={union.size.z,6:F2}");

                report.Add($"    binds -X: {BinderOn(cats, b => b.min.x, false)}   " +
                           $"+X: {BinderOn(cats, b => b.max.x, true)}   " +
                           $"-Y: {BinderOn(cats, b => b.min.y, false)}   " +
                           $"+Y: {BinderOn(cats, b => b.max.y, true)}   " +
                           $"-Z: {BinderOn(cats, b => b.min.z, false)}   " +
                           $"+Z: {BinderOn(cats, b => b.max.z, true)}");

                // The decisive part: which term binds the FIT, not just the AABB.
                report.Add("    " + FitTermReport(union));

                // And what it costs: tiles' share of the framed extent.
                var tiles = cats[0];
                if (tiles.any)
                    report.Add($"    tiles occupy {tiles.b.size.x / union.size.x:P1} of framed X, " +
                               $"{tiles.b.size.z / union.size.z:P1} of framed Z, " +
                               $"{tiles.b.size.y / union.size.y:P1} of framed Y");
            }

            Debug.Log("[B1] framing bounds breakdown" + string.Join("\n", report));

            Assert.IsNotEmpty(report, "B1: measured nothing");
        }

        /// <summary>
        /// Reproduces BoardFraming.Fit's decision so the report says which term actually binds.
        /// A wide board in a tall frame is bound by width; the AABB alone does not reveal that.
        /// </summary>
        public static string FitTermReport(Bounds bounds)
        {
            var layout = ScriptableObject.CreateInstance<BoardLayout>();
            var cfg = layout.For(BoardLayout.Orientation.Portrait);
            float aspect = (float)DeterministicContext.Width / DeterministicContext.Height;

            var rotation = Quaternion.Euler(BoardFraming.DefaultPitch, 0f, 0f);
            Vector3 right = rotation * Vector3.right, up = rotation * Vector3.up, fwd = rotation * Vector3.forward;
            Vector3 e = bounds.extents;
            float extR = 0f, extU = 0f, extF = 0f;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                extR = Mathf.Max(extR, Mathf.Abs(Vector3.Dot(corner, right)));
                extU = Mathf.Max(extU, Mathf.Abs(Vector3.Dot(corner, up)));
                extF = Mathf.Max(extF, Mathf.Abs(Vector3.Dot(corner, fwd)));
            }
            float pad = cfg.padding * Mathf.Min(cfg.boardRect.width, cfg.boardRect.height);
            float rectW = cfg.boardRect.width - 2f * pad, rectH = cfg.boardRect.height - 2f * pad;
            float needU = extU / rectH;
            float needR = extR / (rectW * aspect);
            Object.DestroyImmediate(layout);

            return $"FIT: extR={extR:F2} extU={extU:F2} extF={extF:F2} -> " +
                   $"orthoSize needed by WIDTH={needR:F2}, by HEIGHT={needU:F2}  ==>  " +
                   $"**{(needR >= needU ? "WIDTH" : "HEIGHT")} BINDS** (ratio {Mathf.Max(needR, needU) / Mathf.Min(needR, needU):F2}x)";
        }
    }
}
