using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HapisHavoc.Tests
{
    /// <summary>
    /// E1, E2, E3 and X20 — pan behaviour, written against WHAT PAWEL REPORTS FROM PLAYING IT,
    /// not against what the code currently does:
    ///
    ///   PLAY    pan freely; on release, return to the framed pose
    ///   EDITOR  pan freely; NO return
    ///   ENDLESS pan on the streaming axis only; no return on release; BUT re-centre on the boat
    ///           when a turn ENDS
    ///
    /// The Endless one is the live bug: the camera follows during movement and then loses the boat
    /// below the bottom edge. E3 is expected RED until that is fixed, and it is written to fail for
    /// that reason rather than for a proxy of it.
    ///
    /// FUNCTIONAL, NOT VISUAL. These assert transform positions and viewport containment, never
    /// pixels, so they do not depend on divergence #7's time behaviour. Every wait is on a
    /// CONDITION with a wall-clock timeout — never a fixed number of frames, and never a fixed
    /// duration standing in for "the return has happened".
    ///
    /// NOTHING IS SUPPRESSED HERE. `DeterministicContext` disables `UniversalCameraController`
    /// (suppression 8), which is precisely the component under test, so these tests deliberately do
    /// not use it. That is also why they cannot be capture-based.
    /// </summary>
    [TestFixture]
    public class PanTests
    {
        /// <summary>Seconds of wall clock to let the driver frame before touching the camera.</summary>
        const float FramingSettle = 6f;

        /// <summary>
        /// The proxy UCC pans. Reached by reflection because it is a private serialised field and
        /// exposing it publicly would widen production API for a test's benefit. Confined to here.
        /// </summary>
        static Transform Proxy(UniversalCameraController ucc)
        {
            var f = typeof(UniversalCameraController).GetField("cameraProxy",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return f?.GetValue(ucc) as Transform;
        }

        static UniversalCameraController Ucc() =>
            Object.FindFirstObjectByType<UniversalCameraController>();

        /// <summary>
        /// Waits until <paramref name="condition"/> holds, or the wall clock runs out. Returns
        /// whether it held. Wall clock, because a condition that never comes true must fail loudly
        /// rather than hang, and because game time is not comparable here (divergence #7).
        /// </summary>
        static IEnumerator WaitUntil(System.Func<bool> condition, float timeoutSeconds,
                                     System.Action<bool> result)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (condition()) { result(true); yield break; }
                yield return null;
            }
            result(condition());
        }

        // ---------------------------------------------------------------- E1  PLAY returns

        [UnityTest]
        public IEnumerator E1_PlayModePanReturnsToTheFramedPose()
        {
            LogAssert.ignoreFailingMessages = true;

            yield return SceneFixture.Load(FixtureMode.Playing, "Levels/01_06_TestLevel");
            var boat = SceneFixture.Boat;
            if (boat != null) boat.DeselectBoat();
            yield return new WaitForSecondsRealtime(FramingSettle);

            var ucc = Ucc();
            Assert.IsNotNull(ucc, "E1: no UniversalCameraController in the scene.");
            Assert.IsTrue(ucc.enabled, "E1: UCC is disabled, so nothing about pan is being tested.");
            var proxy = Proxy(ucc);
            Assert.IsNotNull(proxy, "E1: UCC has no cameraProxy assigned - pan cannot work at all.");

            // The framed resting pose is wherever the driver left the proxy.
            Vector3 framed = proxy.position;

            // Simulate the RESULT of a pan: the proxy displaced from rest. Asserting the return
            // behaviour does not require synthesising input, and doing it this way keeps the test
            // clear of Unity's input stack.
            const float panDistance = 3f;
            proxy.position = framed + new Vector3(panDistance, 0f, -panDistance);
            Vector3 displaced = proxy.position;

            bool returned = false;
            yield return WaitUntil(() => Vector3.Distance(proxy.position, framed) < 0.15f,
                                   12f, r => returned = r);

            float finalDistance = Vector3.Distance(proxy.position, framed);
            Debug.Log($"[E1] Play: framed={framed:F2} displaced={displaced:F2} " +
                      $"final={proxy.position:F2} distanceFromFramed={finalDistance:F3} " +
                      $"returned={returned}");

            Assert.IsTrue(returned,
                $"E1: Play mode did not rubberband back to the framed pose. Pawel's report is " +
                $"'we can pan to see further but when we lift the finger we rubberband back to " +
                $"the boat'. Displaced {panDistance} units and it settled {finalDistance:F3} away " +
                $"from the framed pose after 12s of wall clock.");
        }

        // ---------------------------------------------------------------- E2  EDITOR does not

        [UnityTest]
        public IEnumerator E2_EditorModePanDoesNotReturn()
        {
            LogAssert.ignoreFailingMessages = true;

            yield return SceneFixture.Load(FixtureMode.Editor, "Levels/01_06_TestLevel");
            var boat = SceneFixture.Boat;
            if (boat != null) boat.DeselectBoat();
            yield return new WaitForSecondsRealtime(FramingSettle);

            var ucc = Ucc();
            Assert.IsNotNull(ucc, "E2: no UniversalCameraController in the scene.");
            var proxy = Proxy(ucc);
            Assert.IsNotNull(proxy, "E2: UCC has no cameraProxy assigned.");

            Vector3 framed = proxy.position;
            const float panDistance = 3f;
            proxy.position = framed + new Vector3(panDistance, 0f, -panDistance);
            Vector3 displaced = proxy.position;

            // Wait well past Play mode's returnDelay so a return WOULD have happened by now.
            // If the proxy comes back here, Editor has inherited Play's rubberband.
            bool cameBack = false;
            yield return WaitUntil(() => Vector3.Distance(proxy.position, framed) < 0.15f,
                                   8f, r => cameBack = r);

            float drift = Vector3.Distance(proxy.position, displaced);
            Debug.Log($"[E2] Editor: framed={framed:F2} displaced={displaced:F2} " +
                      $"final={proxy.position:F2} driftFromDisplaced={drift:F3} " +
                      $"cameBackToFramed={cameBack}");

            Assert.IsFalse(cameBack,
                "E2: Editor mode rubberbanded back to the framed pose. Pawel's report is 'we can " +
                "pan freely and we do not rubberband back to main screen' - the Editor is an " +
                "authoring surface, so a camera that springs back fights the person using it.");

            Assert.Less(drift, 0.5f,
                $"E2: the Editor camera drifted {drift:F3} units from where it was left without " +
                "any input. It should simply stay put.");
        }

        // ---------------------------------------------------------------- E3  ENDLESS keeps the boat

        /// <summary>
        /// E3 — ENDLESS PAN AND END-OF-TURN RE-CENTRE. Four claims, all from Pawel's report:
        ///
        ///   a) pans on the STREAMING AXIS only
        ///   b) does NOT return immediately on release
        ///   c) DOES return after the delay (~3s)
        ///   d) re-centres on the boat when a turn ENDS
        ///
        /// (c) is a CHANGE from his earlier description - he first said Endless should not return
        /// at all, and after more playtesting wants it to come back, with Play's feel as the
        /// reference. So this test is written against the new report, not the old behaviour.
        ///
        /// (d) is the live bug: the camera follows during movement and then loses the boat below
        /// the bottom edge. It is asserted as the INVARIANT - after a turn ends the boat must be
        /// inside the framed board rect - so it cannot be satisfied by a re-centre onto the wrong
        /// thing.
        ///
        /// THE TURN IS ENDED BY PLAYING, not by setting state. GameDriver spends the player's AP
        /// through BoatController.OnTileClicked, the same method a tap reaches. Before that existed
        /// this test sampled an idle board for 25 seconds and reported green.
        /// </summary>
        [UnityTest]
        public IEnumerator E3_EndlessPanReturnsAndRecentresOnTheBoat()
        {
            LogAssert.ignoreFailingMessages = true;

            // SEED THE RIVER. Endless generation uses UnityEngine.Random, so the river differs
            // every run and so does whether the boat can keep moving. Measured: identical code
            // drove 4 moves and ended the turn in isolation, then dead-ended after 1 move in a full
            // suite run, because a different river had been generated. Seeding makes the fixture
            // reproducible rather than making the assertion tolerant.
            //
            // This is NOT a suppression - nothing is disabled and the generator runs exactly as it
            // does in play. It is the same pinning DeterministicContext already does for captures,
            // applied to a test that needs a repeatable board. A step towards divergence #3.
            Random.InitState(4242);

            yield return SceneFixture.Load(FixtureMode.Endless);
            yield return new WaitForSecondsRealtime(FramingSettle);

            var cam = Camera.main;
            var boat = SceneFixture.Boat;
            var grid = SceneFixture.Grid;
            var ucc = Ucc();
            var mgr = Object.FindFirstObjectByType<EndlessModeManager>();

            Assert.IsNotNull(cam, "E3: no main camera.");
            Assert.IsNotNull(boat, "E3: no boat in Endless mode.");
            Assert.IsNotNull(ucc, "E3: no UniversalCameraController.");
            Assert.IsNotNull(mgr, "E3: no EndlessModeManager, so there are no turns to observe.");

            var failures = new List<string>();
            var log = new List<string>();

            // ---------------------------------------------------------------- (a) streaming axis

            var settings = EndlessSettings(ucc);
            Assert.IsNotNull(settings, "E3: endlessSettings not found on UCC.");

            log.Add($"  endless panMode={settings.panMode} returnToOrigin={settings.returnToOrigin} " +
                    $"returnDelay={settings.returnDelay:F2} friction={settings.friction:F2}");

            if (settings.panMode != UniversalCameraController.PanMode.ZAxisOnly)
                failures.Add($"(a) Endless pan mode is {settings.panMode}, expected ZAxisOnly - " +
                             "the river streams along Z, so panning sideways only loses the boat.");

            // ---------------------------------------------------------------- (b) and (c) return

            // Simulate the RESULT of a pan: an accumulated offset, as ApplyEndlessPan would leave.
            // There is no public input entry point, so this sets the offset the same way E1/E2 set
            // the proxy - the behaviour under test is the RETURN, not the input plumbing.
            const float panZ = 4f;
            SetEndlessOffset(ucc, new Vector3(0f, 0f, panZ));
            float camXBefore = cam.transform.position.x;

            yield return new WaitForSecondsRealtime(0.5f);

            float heldOffset = ucc.GetEndlessCameraOffset().z;
            float camXDuring = cam.transform.position.x;
            log.Add($"  after 0.5s: offset.z={heldOffset:F3} (panned {panZ}) camX {camXBefore:F2}->{camXDuring:F2}");

            if (Mathf.Abs(heldOffset) < panZ * 0.5f)
                failures.Add($"(b) the pan offset collapsed from {panZ} to {heldOffset:F3} within " +
                             "0.5s. Endless must not snap back the instant the finger lifts - " +
                             "Pawel pans to look ahead and needs a moment to read the river.");

            if (Mathf.Abs(camXDuring - camXBefore) > 0.01f)
                failures.Add($"(a) panning moved the camera on X by " +
                             $"{Mathf.Abs(camXDuring - camXBefore):F3}; Endless pans on Z only.");

            // Now wait past the return delay and require it to come home. Condition + wall-clock
            // timeout, never a fixed sleep: game time runs ~15x slower here (divergence #7), and
            // returnDelay is measured in GAME time.
            bool returned = false;
            yield return WaitUntil(() => Mathf.Abs(ucc.GetEndlessCameraOffset().z) < 0.25f,
                                   30f, r => returned = r);

            float finalOffset = ucc.GetEndlessCameraOffset().z;
            log.Add($"  after waiting for the return: offset.z={finalOffset:F3} returned={returned}");

            if (!returned)
                failures.Add($"(c) the Endless pan never returned: offset.z is still " +
                             $"{finalOffset:F3} after 30s of wall clock. Pawel now wants it to come " +
                             $"back after about {settings.returnDelay:F1}s, with Play's smoothing - " +
                             "he says Play's feel is the one that is right.");

            // ---------------------------------------------------------------- (d) end-of-turn

            var layout = BoardFramingDriver.LayoutFor(OperatingMode.Endless);
            var cfg = layout.For(BoardFramingDriver.OrientationFor(
                OperatingMode.Endless, Screen.width, Screen.height));
            float pad = cfg.padding * Mathf.Min(cfg.boardRect.width, cfg.boardRect.height);
            var rect = Rect.MinMaxRect(cfg.boardRect.xMin + pad, cfg.boardRect.yMin + pad,
                                       cfg.boardRect.xMax - pad, cfg.boardRect.yMax - pad);

            var turnField = typeof(EndlessModeManager).GetField("isPlayerTurn",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(turnField, "E3: isPlayerTurn not found - the field this test watches " +
                                        "has been renamed.");
            bool IsPlayerTurn() => (bool)turnField.GetValue(mgr);

            // PLAY THE TURN OUT. Real moves, real AP spend, real turn end.
            int movesMade = 0;
            bool sawTurnEnd = false;
            log.Add("  driving moves through OnTileClicked until the turn ends:");
            yield return GameDriver.PlayUntilTurnEnds(boat, grid, () => !IsPlayerTurn(), log,
                                                      m => movesMade = m);
            yield return WaitUntil(() => !IsPlayerTurn(), 20f, r => sawTurnEnd = r);

            log.Add($"  moves landed={movesMade} turnEnded={sawTurnEnd}");

            // LOG BEFORE ASSERTING. A guard that fires below must not take the evidence with it -
            // the first version asserted first and reported "1 move, no turn ended" with the
            // per-move log unprinted, which is divergence #8's lesson in miniature.
            void Dump() => Debug.Log("[E3] Endless pan and end-of-turn re-centre\n" +
                                     string.Join("\n", log));
            Dump();

            // VACUITY GUARD. Without a turn ending, (d) measures nothing.
            Assert.Greater(movesMade, 0,
                "E3 could not drive a single move, so nothing below was exercised. GameDriver goes " +
                "through BoatController.OnTileClicked with destinations from the boat's own " +
                "ValidMoves; if that lands no moves, find out why rather than removing this guard.");
            Assert.IsTrue(sawTurnEnd,
                $"E3 drove {movesMade} move(s) but no turn ended, so the end-of-turn re-centre was " +
                "never exercised. Do NOT delete this guard to get a green.");

            // Give the camera whatever settling the end of a turn triggers, then check the boat.
            yield return new WaitForSecondsRealtime(3f);

            var vp = cam.WorldToViewportPoint(boat.transform.position);
            bool inside = vp.z > 0f
                       && vp.x >= rect.xMin && vp.x <= rect.xMax
                       && vp.y >= rect.yMin && vp.y <= rect.yMax;

            log.Add($"  after turn end: boat viewport=({vp.x:F3},{vp.y:F3}) inside={inside} " +
                    $"rect x[{rect.xMin:F2},{rect.xMax:F2}] y[{rect.yMin:F2},{rect.yMax:F2}]");

            if (!inside)
                failures.Add($"(d) the turn ended with the boat at viewport ({vp.x:F3},{vp.y:F3}), " +
                             $"outside the board rect. Pawel's report: the camera follows during " +
                             "movement and then loses the boat below the bottom edge. A player who " +
                             "cannot see their own boat cannot play.");

            Dump();

            Assert.IsEmpty(failures, "E3:\n  " + string.Join("\n  ", failures));
        }

        /// <summary>UCC's Endless pan settings. Private serialised field; test-only reflection.</summary>
        static UniversalCameraController.PanSettings EndlessSettings(UniversalCameraController ucc)
        {
            var f = typeof(UniversalCameraController).GetField("endlessSettings",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return f?.GetValue(ucc) as UniversalCameraController.PanSettings;
        }

        /// <summary>
        /// Simulates the RESULT of a drag that has just ended: an accumulated offset, AND the input
        /// timestamp a real drag would have left behind.
        ///
        /// THE TIMESTAMP IS NOT OPTIONAL. The return is gated on
        /// `Time.time - lastPlayerInputTime > returnDelay`, so setting only the offset simulates a
        /// pan that finished long ago - and the camera correctly begins returning at once. The first
        /// version did exactly that and the offset collapsed from 4.0 to 0.876 within half a second,
        /// which reads as "the delay is broken" when the delay was working perfectly on a stale
        /// timestamp. Same class of mistake as passing a PointerEventData with no pressEventCamera.
        /// </summary>
        static void SetEndlessOffset(UniversalCameraController ucc, Vector3 offset)
        {
            var f = typeof(UniversalCameraController).GetField("endlessCameraOffset",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            f?.SetValue(ucc, offset);

            var t = typeof(UniversalCameraController).GetField("lastPlayerInputTime",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            t?.SetValue(ucc, Time.time);
        }

        // ---------------------------------------------------------------- X20  control for E1

        /// <summary>
        /// X20 — the control for E1.
        ///
        /// OWNERSHIP THIS ENCODES: `playerSettings.returnToOrigin` is what makes Play mode
        /// rubberband. Turning it off must make E1's check fail; if it does not, E1 is not
        /// measuring the return at all and would stay green after the feature was lost.
        ///
        /// E1 and E2 are also each other's control in a weaker sense - the same displacement with
        /// opposite expectations through the same code path - so a mechanism broken in either
        /// direction is caught by one of them. X20 is the explicit version for the direction that
        /// matters most: silently losing the rubberband.
        ///
        /// Per CLAUDE.md: if ownership moves, rewrite this breakage against the new owner. Never
        /// resolve it by loosening E1's tolerance.
        /// </summary>
        [UnityTest]
        public IEnumerator X20_E1_FailsWhenTheReturnIsTurnedOff()
        {
            LogAssert.ignoreFailingMessages = true;

            yield return SceneFixture.Load(FixtureMode.Playing, "Levels/01_06_TestLevel");
            var boat = SceneFixture.Boat;
            if (boat != null) boat.DeselectBoat();
            yield return new WaitForSecondsRealtime(FramingSettle);

            var ucc = Ucc();
            Assert.IsNotNull(ucc, "X20: no UniversalCameraController.");
            var proxy = Proxy(ucc);
            Assert.IsNotNull(proxy, "X20: no cameraProxy.");

            // THE BREAKAGE: switch off the setting that owns the rubberband.
            var settingsField = typeof(UniversalCameraController).GetField("playerSettings",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(settingsField,
                "X20: playerSettings field not found - the field this control breaks has been " +
                "renamed or moved. Rewrite the breakage against the new owner rather than " +
                "deleting this test.");

            var settings = settingsField.GetValue(ucc) as UniversalCameraController.PanSettings;
            Assert.IsNotNull(settings, "X20: playerSettings was null.");

            bool previous = settings.returnToOrigin;
            settings.returnToOrigin = false;

            // Force the controller to pick the mutated settings up.
            var currentField = typeof(UniversalCameraController).GetField("currentSettings",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            currentField?.SetValue(ucc, settings);

            Vector3 framed = proxy.position;
            proxy.position = framed + new Vector3(3f, 0f, -3f);

            bool returned = false;
            yield return WaitUntil(() => Vector3.Distance(proxy.position, framed) < 0.15f,
                                   8f, r => returned = r);

            float finalDistance = Vector3.Distance(proxy.position, framed);
            settings.returnToOrigin = previous;

            Debug.Log($"[X20] returnToOrigin forced false: proxy settled {finalDistance:F3} from " +
                      $"the framed pose, E1's threshold is 0.15. E1 would report returned={returned}");

            Assert.IsFalse(returned,
                "X20 META-FAILURE: the proxy returned to the framed pose with returnToOrigin " +
                "switched OFF. Something other than that setting is moving the proxy home, so E1 " +
                "is not measuring the rubberband and would stay green if the feature were lost. " +
                "Fix E1, not this control.");
        }
    }
}
