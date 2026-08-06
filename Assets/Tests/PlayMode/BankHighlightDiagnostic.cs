using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HapisHavoc.Tests
{
    /// <summary>
    /// STEP 3. V1b reported banksHighlighted=2 while only the BOTTOM bank ever changed colour.
    /// This determines whether that is (a) a real bug or (b) the top bank simply not being a
    /// valid dock in the tested scenario - and therefore what V1b actually proves.
    ///
    /// Diagnostic, not a regression assertion. It reports; it does not fix.
    /// </summary>
    [TestFixture]
    public class BankHighlightDiagnostic
    {
        const float Settle = 1.6f;

        [UnityTest]
        public IEnumerator S3_WhichBankHighlightsAndDoHandlersLeak()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneFixture.Load(FixtureMode.Playing, "Levels/01_06_TestLevel");

            var boat = SceneFixture.Boat;
            var rbm = Object.FindFirstObjectByType<RiverBankManager>();
            Assert.IsNotNull(boat, "no boat");
            Assert.IsNotNull(rbm, "no RiverBankManager");

            var topGo = rbm.GetBankGameObject(RiverBankManager.BankSide.Top);
            var botGo = rbm.GetBankGameObject(RiverBankManager.BankSide.Bottom);

            // Count handlers per bank INCLUDING children - the add site uses the child mesh
            // object while the remove site looks at the parent.
            int HandlersUnder(GameObject go) =>
                go == null ? -1 : go.GetComponentsInChildren<BankClickHandler>(true).Length;
            int HandlersOnParentOnly(GameObject go) =>
                go == null ? -1 : go.GetComponents<BankClickHandler>().Length;

            var rows = new List<string>();
            rows.Add($"  bankGO(top)={topGo?.name}  bankGO(bottom)={botGo?.name}");

            // Three select/deselect cycles. If cleanup worked, the count returns to 0 each time.
            for (int cycle = 1; cycle <= 3; cycle++)
            {
                boat.DeselectBoat();
                yield return new WaitForSecondsRealtime(Settle);
                int topAfterClear = HandlersUnder(topGo), botAfterClear = HandlersUnder(botGo);

                boat.SelectBoat();
                yield return new WaitForSecondsRealtime(Settle);
                int topLit = HandlersUnder(topGo), botLit = HandlersUnder(botGo);
                int topParent = HandlersOnParentOnly(topGo), botParent = HandlersOnParentOnly(botGo);

                rows.Add($"  cycle {cycle}: afterClear(top={topAfterClear}, bot={botAfterClear})  " +
                         $"whileSelected(top={topLit}, bot={botLit})  " +
                         $"onParentOnly(top={topParent}, bot={botParent})");
            }

            // Which bank's renderer actually carries a changed colour right now?
            string ColourOf(GameObject go)
            {
                if (go == null) return "<null>";
                var r = go.GetComponentInChildren<MeshRenderer>();
                return r == null ? "<no renderer>" : $"{(Color32)r.material.color} on '{r.gameObject.name}'";
            }
            rows.Add($"  live colour top   : {ColourOf(topGo)}");
            rows.Add($"  live colour bottom: {ColourOf(botGo)}");

            Debug.Log("[S3 bank diagnostic]\n" + string.Join("\n", rows));

            // Report, do not fix. The assertion below only guards the diagnostic itself.
            Assert.IsNotNull(topGo, "top bank missing entirely");
            Assert.IsNotNull(botGo, "bottom bank missing entirely");
        }

        /// <summary>
        /// Does the boat ever consider the TOP bank a valid dock? HighlightBankForDocking is
        /// private, so this probes the observable trigger: put the boat on the top row with a
        /// path exiting upward, which is the condition FindMoveAtEndOfChain requires.
        /// </summary>
        [UnityTest]
        public IEnumerator S3b_IsTheTopBankEverAValidDock()
        {
            LogAssert.ignoreFailingMessages = true;
            var report = new List<string>();

            foreach (var lvl in SceneFixture.AllLevels)
            {
                yield return SceneFixture.Load(FixtureMode.Playing, lvl);
                var boat = SceneFixture.Boat;
                var grid = SceneFixture.Grid;
                var rbm = Object.FindFirstObjectByType<RiverBankManager>();
                if (boat == null || grid == null || rbm == null) { report.Add($"  {lvl}: fixture incomplete"); continue; }

                var topGo = rbm.GetBankGameObject(RiverBankManager.BankSide.Top);
                var botGo = rbm.GetBankGameObject(RiverBankManager.BankSide.Bottom);

                boat.DeselectBoat();
                yield return new WaitForSecondsRealtime(0.8f);

                // Walk the boat onto every top-row tile in turn and see whether selecting there
                // makes the top bank a dock.
                bool topEverLit = false;
                int topRow = grid.rows - 1;
                for (int x = 0; x < grid.cols; x++)
                {
                    var t = grid.GetTileAt(x, topRow);
                    if (t == null || t.IsHardBlocker) continue;
                    for (int snap = 0; snap < 6; snap++)
                    {
                        boat.PlaceOnTile(t, snap);
                        boat.SelectBoat();
                        yield return new WaitForSecondsRealtime(0.5f);
                        if (topGo.GetComponentsInChildren<BankClickHandler>(true).Length > 0) topEverLit = true;
                        boat.DeselectBoat();
                        yield return new WaitForSecondsRealtime(0.3f);
                        if (topEverLit) break;
                    }
                    if (topEverLit) break;
                }
                report.Add($"  {lvl.Replace("Levels/", ""),-22} topBankEverBecameADock={topEverLit}");
            }

            Debug.Log("[S3b top-bank dock reachability]\n" + string.Join("\n", report));
        }
    }
}
