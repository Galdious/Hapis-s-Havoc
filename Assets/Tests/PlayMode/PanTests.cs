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
        /// E3 — THE LIVE BUG. Endless pans on the streaming axis and never returns, which is
        /// correct; but when a turn ends the camera must re-centre on the boat, and it does not.
        /// Pawel's report: the camera follows during movement and then loses the boat below the
        /// bottom edge.
        ///
        /// Asserted as the INVARIANT rather than the mechanism: whatever the camera does between
        /// turns, the boat must be inside the framed board rect once a turn has ended. That way the
        /// test does not encode a particular fix, and it cannot be satisfied by a re-centre that
        /// re-centres on the wrong thing.
        /// </summary>
        [UnityTest]
        public IEnumerator E3_EndlessRecentresOnTheBoatWhenATurnEnds()
        {
            LogAssert.ignoreFailingMessages = true;

            yield return SceneFixture.Load(FixtureMode.Endless);
            yield return new WaitForSecondsRealtime(FramingSettle);

            var cam = Camera.main;
            var boat = SceneFixture.Boat;
            Assert.IsNotNull(cam, "E3: no main camera.");
            Assert.IsNotNull(boat, "E3: no boat in Endless mode.");

            var layout = BoardFramingDriver.LayoutFor(OperatingMode.Endless);
            Assert.IsNotNull(layout, "E3: no BoardLayout for Endless.");
            var cfg = layout.For(BoardFramingDriver.OrientationFor(
                OperatingMode.Endless, Screen.width, Screen.height));
            float pad = cfg.padding * Mathf.Min(cfg.boardRect.width, cfg.boardRect.height);
            var rect = Rect.MinMaxRect(cfg.boardRect.xMin + pad, cfg.boardRect.yMin + pad,
                                       cfg.boardRect.xMax - pad, cfg.boardRect.yMax - pad);

            // COUNT TURN ENDINGS. The assertion is about what happens when a turn ENDS, so a run in
            // which no turn ended proves nothing about it - and would pass while measuring an idle
            // board. Read from the manager rather than inferred from elapsed time, because elapsed
            // wall time says nothing about how far the game loop has got (divergence #7).
            var mgr = Object.FindFirstObjectByType<EndlessModeManager>();
            Assert.IsNotNull(mgr, "E3: no EndlessModeManager, so there are no turns to observe.");
            var turnField = typeof(EndlessModeManager).GetField("isPlayerTurn",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(turnField,
                "E3: isPlayerTurn not found - the field this test watches has been renamed.");

            bool WasPlayerTurn() => (bool)turnField.GetValue(mgr);

            var samples = new List<string>();
            var offences = new List<string>();
            float deadline = Time.realtimeSinceStartup + 25f;
            int n = 0;
            int turnEndings = 0;
            bool prevTurn = WasPlayerTurn();
            Vector3 boatStart = boat.transform.position;

            while (Time.realtimeSinceStartup < deadline)
            {
                yield return new WaitForSecondsRealtime(1.0f);
                n++;

                bool nowTurn = WasPlayerTurn();
                if (prevTurn && !nowTurn) turnEndings++;
                prevTurn = nowTurn;

                var vp = cam.WorldToViewportPoint(boat.transform.position);
                bool inside = vp.z > 0f
                           && vp.x >= rect.xMin && vp.x <= rect.xMax
                           && vp.y >= rect.yMin && vp.y <= rect.yMax;

                samples.Add($"    t+{n}s boat viewport=({vp.x:F3},{vp.y:F3}) inside={inside} " +
                            $"playerTurn={nowTurn}");
                if (!inside)
                    offences.Add($"t+{n}s boat at viewport ({vp.x:F3},{vp.y:F3}), " +
                                 $"outside board rect x[{rect.xMin:F2},{rect.xMax:F2}] " +
                                 $"y[{rect.yMin:F2},{rect.yMax:F2}]");
            }

            float boatTravel = Vector3.Distance(boat.transform.position, boatStart);

            Debug.Log($"[E3] Endless: boat containment over {n} sample(s), board rect " +
                      $"x[{rect.xMin:F2},{rect.xMax:F2}] y[{rect.yMin:F2},{rect.yMax:F2}]\n" +
                      $"  turn endings observed={turnEndings}  boat travelled={boatTravel:F2} " +
                      $"world units\n" + string.Join("\n", samples));

            Assert.IsNotEmpty(samples, "E3: took no samples, so this proves nothing.");

            // THE VACUITY GUARD, and it is currently what makes E3 red.
            //
            // Measured: 25s of wall clock is roughly 1.7s of GAME time (divergence #7), and the
            // Endless loop parks on WaitUntil(currentAP <= 0 || !isPlayerTurn || currentStamina <= 0).
            // With no player input AP never drains, so the loop sits in the player's turn forever:
            // two cycle log lines, zero turn endings, and the boat stationary to within its idle bob
            // (viewport y 0.144..0.148 across 25 samples). The containment check passed while
            // measuring an idle board.
            //
            // E3 therefore fails here rather than reporting a green it has not earned. Closing it
            // needs the test to SPEND the player's AP - move the boat through BoatController until
            // AP reaches zero - so that a turn genuinely ends and the camera has something to
            // re-centre from. That is the next piece of work, not a tolerance to widen.
            Assert.Greater(turnEndings, 0,
                $"E3 CANNOT YET SEE ITS SUBJECT. No turn ended in {n} samples, so the containment " +
                $"check above measured an idle board and its pass means nothing. The Endless loop " +
                $"waits for the player to spend AP and no input was given, so it never left the " +
                $"player's turn (boat travelled {boatTravel:F2} units - idle bob only). To make " +
                $"this assertion real, drive the boat until AP is exhausted and a turn ends. Do " +
                $"NOT delete this guard to get a green.");

            Assert.IsEmpty(offences,
                "E3: the Endless camera lost the boat. Pawel's report is that the camera follows " +
                "during movement and then loses the boat below the bottom edge; when a turn ENDS " +
                "the camera must re-centre on it. A player who cannot see their own boat cannot " +
                "play.\n  " + string.Join("\n  ", offences));
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
