using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HapisHavoc.Tests
{
    /// <summary>
    /// L8. BankClickHandler must not accumulate across select/deselect cycles.
    ///
    /// Written and landed RED against the pre-fix code, before the fix, so the failure is
    /// observed rather than assumed. Measured accumulation before the fix: 1 -> 2 -> 3 -> 4 -> 5
    /// -> 6 across five cycles, with the parent-only count 0 throughout - BoatController adds the
    /// handler to renderer.gameObject (the CHILD mesh object) while ClearHighlights removes it
    /// via bankGO.GetComponent (the PARENT), and GetComponent does not search children.
    /// </summary>
    [TestFixture]
    public class BankHandlerTests
    {
        const float Settle = 1.2f;

        [UnityTest]
        public IEnumerator L8_BankClickHandlerCountStaysConstant()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneFixture.Load(FixtureMode.Playing, "Levels/01_06_TestLevel");

            var boat = SceneFixture.Boat;
            var rbm = Object.FindFirstObjectByType<RiverBankManager>();
            Assert.IsNotNull(boat, "no boat");
            Assert.IsNotNull(rbm, "no RiverBankManager");

            var topGo = rbm.GetBankGameObject(RiverBankManager.BankSide.Top);
            var botGo = rbm.GetBankGameObject(RiverBankManager.BankSide.Bottom);
            Assert.IsNotNull(topGo, "no top bank");
            Assert.IsNotNull(botGo, "no bottom bank");

            // Count INCLUDING children - the handler may legitimately live on either the parent
            // or the child mesh object, and this test should not care which, only that it does
            // not grow.
            int Under(GameObject go) => go.GetComponentsInChildren<BankClickHandler>(true).Length;

            // Establish a clean starting point: SceneFixture ends with SelectBoat, so clear first.
            boat.DeselectBoat();
            yield return new WaitForSecondsRealtime(Settle);
            int topBase = Under(topGo), botBase = Under(botGo);

            var rows = new List<string> { $"  baseline after clear: top={topBase} bottom={botBase}" };
            var topCounts = new List<int>();
            var botCounts = new List<int>();

            for (int cycle = 1; cycle <= 5; cycle++)
            {
                boat.SelectBoat();
                yield return new WaitForSecondsRealtime(Settle);
                int topLit = Under(topGo), botLit = Under(botGo);

                boat.DeselectBoat();
                yield return new WaitForSecondsRealtime(Settle);
                int topClear = Under(topGo), botClear = Under(botGo);

                topCounts.Add(topClear);
                botCounts.Add(botClear);
                rows.Add($"  cycle {cycle}: whileSelected(top={topLit}, bot={botLit})  " +
                         $"afterClear(top={topClear}, bot={botClear})");
            }

            Debug.Log("[L8] BankClickHandler counts\n" + string.Join("\n", rows));

            // After every clear the count must be back to the baseline. Growth means the handler
            // added on selection was never destroyed.
            for (int i = 0; i < topCounts.Count; i++)
            {
                Assert.AreEqual(topBase, topCounts[i],
                    $"L8: top bank handler count after clear in cycle {i + 1} is {topCounts[i]}, " +
                    $"expected {topBase}. Handlers are accumulating - added and removed on " +
                    "different GameObjects.");
                Assert.AreEqual(botBase, botCounts[i],
                    $"L8: bottom bank handler count after clear in cycle {i + 1} is {botCounts[i]}, " +
                    $"expected {botBase}. Handlers are accumulating - BoatController adds to " +
                    "renderer.gameObject (child) while ClearHighlights removes via " +
                    "bankGO.GetComponent (parent), and GetComponent does not search children.");
            }
        }
    }
}
