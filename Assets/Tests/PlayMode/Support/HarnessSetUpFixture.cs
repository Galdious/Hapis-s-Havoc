using NUnit.Framework;
using UnityEngine;

// Namespace-level setup: NUnit runs this ONCE before any test in HapisHavoc.Tests.
namespace HapisHavoc.Tests
{
    /// <summary>
    /// Puts the harness screen into portrait BEFORE ANY TEST LOADS A SCENE.
    ///
    /// This is the right PLACE for the setup - every scene load in the run then sees the same,
    /// already-portrait screen instead of the resolution changing partway through the first load.
    /// SceneFixture keeps its own guarded call as a fallback for anything running outside this
    /// fixture.
    ///
    /// IT DID NOT FIX THE ONE THING IT WAS WRITTEN FOR, and that is recorded rather than quietly
    /// dropped. After the portrait switch, `01_01_BasicMoves` differs from its golden by 9 pixels
    /// (0.0004 %, max 21) - a single 5x4 antialiased cluster on the boat's red edge at viewport
    /// (0.50, 0.386). Every other level is byte-identical. The difference is STABLE: identical
    /// across separate processes and across repeated runs, so it is not noise.
    ///
    /// Three explanations were tested and all three were refuted by measurement:
    ///   1. "the first level gets an extra frame from the portrait switch" - made the yield
    ///      unconditional so every level got one; 01_01 was unchanged and V9 moved 30.37 %.
    ///   2. "every level should be treated identically" - same experiment, reverted.
    ///   3. "the resolution change lands during the first load" - this fixture; 01_01 unchanged.
    ///
    /// So the cause is still unknown. It is within V8's tolerance and V8 passes, and the golden has
    /// deliberately NOT been re-baked to absorb it. Left open and reported rather than approved.
    /// </summary>
    [SetUpFixture]
    public class HarnessSetUpFixture
    {
        [OneTimeSetUp]
        public void BeforeAnyTest()
        {
            bool ok = HarnessScreen.EnsurePortrait();
            Debug.Log($"[HarnessSetUp] portrait requested={ok}; {HarnessScreen.Describe()}. " +
                      "The change lands on the next frame, before the first scene load.");
        }
    }
}
