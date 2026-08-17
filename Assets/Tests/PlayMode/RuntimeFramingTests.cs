using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.TestTools;

namespace HapisHavoc.Tests
{
    /// <summary>
    /// C7 and X17. Everything else about framing has been asserted against a pose the TEST
    /// computed; nothing has ever checked that the RUNNING GAME uses it. BoardFraming was
    /// harness-only, so the live camera carried a static orthographic size and a 3x3 and a 6x6
    /// framed identically.
    /// </summary>
    [TestFixture]
    public class RuntimeFramingTests
    {
        /// <summary>The pose the driver should have applied for this scene, per the layout.</summary>
        static bool ExpectedPose(FixtureMode mode, out BoardFraming.Pose pose)
        {
            var layout = BoardFramingDriver.LayoutFor(mode == FixtureMode.Editor
                ? OperatingMode.Editor : OperatingMode.Playing);
            var orientation = BoardFramingDriver.OrientationFor(
                mode == FixtureMode.Editor ? OperatingMode.Editor : OperatingMode.Playing,
                Screen.width, Screen.height);

            // Pitch from the camera that renders, exactly as the driver does.
            var vcam = CameraManager.Instance != null
                ? CameraManager.Instance.CameraFor(mode == FixtureMode.Editor
                    ? OperatingMode.Editor : OperatingMode.Playing)
                : null;
            float pitch = vcam != null ? BoardFraming.PitchOf(vcam.transform)
                                       : BoardFraming.DefaultPitch;

            return BoardFraming.TryFit(layout, orientation, Screen.width, Screen.height,
                                       BoardFraming.Projection.OrthographicTilted, pitch,
                                       out pose, out _);
        }

        [UnityTest]
        public IEnumerator C7_FramingIsAppliedAtRuntime()
        {
            LogAssert.ignoreFailingMessages = true;

            var rows = new List<string>();
            var failures = new List<string>();

            // SHIPPED LEVELS AS WELL AS STUDY FIXTURES. C7 covered only study_* until W1's
            // warts-and-all capture showed 01_01 and 01_06 cropped at the left edge in the LIVE
            // game while their goldens framed correctly. Study fixtures are authored with an empty
            // hand and simple lock states, so they never exercised what the shipped levels do -
            // a test suite that only checks its own fixtures checks the fixtures.
            var levels = new List<string> { "Levels/study_3x3", "Levels/study_3x6", "Levels/study_6x6" };
            levels.AddRange(SceneFixture.AllLevels);

            foreach (var lvl in levels)
            {
                yield return SceneFixture.Load(FixtureMode.Playing, lvl);
                var boat = SceneFixture.Boat;
                if (boat != null) boat.DeselectBoat();
                // Past the Brain's DefaultBlend (EaseInOut, Time: 2) - sampling mid-blend reads
                // a lerp between vCams, not the framed pose. Wall clock, never frame counts.
                yield return new WaitForSecondsRealtime(3.5f);

                var cam = Camera.main;
                Assert.IsNotNull(cam, "no main camera");
                Assert.IsTrue(ExpectedPose(FixtureMode.Playing, out var want),
                              $"{lvl}: BoardFraming could not compute a pose");

                // The bounds C7 sees AT SAMPLE TIME. If these differ from the bounds the driver
                // framed against, framing is not stable across the interval - which would be a
                // bug in its own right, not a test artifact.
                BoardFraming.TryCollectBoardBounds(out var nowBounds, out int nowCount);
                var atSample = new System.Collections.Generic.List<string>(BoardFraming.LastContributors);
                var atFrame = BoardFramingDriver.DriverContributors;
                var joined = atSample.Except(atFrame).ToList();
                var left = atFrame.Except(atSample).ToList();

                Debug.Log($"[C7BOUNDS] {lvl.Replace("Levels/", "")}: driver framed {atFrame.Count} " +
                          $"contributors, sample sees {atSample.Count}\n" +
                          $"  JOINED AFTER FRAMING ({joined.Count}):\n    " +
                          string.Join("\n    ", joined.Take(14)) +
                          (left.Count > 0 ? $"\n  GONE SINCE FRAMING ({left.Count}):\n    " +
                          string.Join("\n    ", left.Take(8)) : ""));

                float dPos = Vector3.Distance(cam.transform.position, want.position);
                float dSize = Mathf.Abs(cam.orthographicSize - want.orthographicSize);
                bool orthoOk = cam.orthographic == want.orthographic;

                rows.Add($"  {lvl.Replace("Levels/", ""),-12} " +
                         $"cam ortho={cam.orthographic} size={cam.orthographicSize:F2} pos={cam.transform.position:F2}\n" +
                         $"                want ortho={want.orthographic} size={want.orthographicSize:F2} pos={want.position:F2}\n" +
                         $"                delta pos={dPos:F3} size={dSize:F3}\n" +
                         $"                bounds x[{nowBounds.min.x:F2},{nowBounds.max.x:F2}] " +
                         $"z[{nowBounds.min.z:F2},{nowBounds.max.z:F2}] from {nowCount} renderer(s); " +
                         $"reservation applied={BoardFraming.LastReservationApplied}" +
                         (BoardFraming.LastReservationApplied
                             ? $" x[{BoardFraming.LastReservation.min.x:F2},{BoardFraming.LastReservation.max.x:F2}]"
                             : ""));

                if (!orthoOk) failures.Add($"{lvl}: projection mismatch");
                if (dPos > 0.05f) failures.Add($"{lvl}: camera position off by {dPos:F3}");
                if (dSize > 0.05f) failures.Add($"{lvl}: orthographicSize off by {dSize:F3}");

                yield return new WaitForSecondsRealtime(0.2f);
            }

            Debug.Log("[C7] runtime camera vs BoardFraming\n" + string.Join("\n", rows));

            Assert.IsEmpty(failures,
                "C7: the running game's camera is not the pose BoardFraming computes. Framing is " +
                "not applied at runtime, so every level is framed the same regardless of shape:\n  "
                + string.Join("\n  ", failures));
        }

        /// <summary>
        /// X17 -> C7 must fail.
        ///
        /// REWRITTEN, NOT LOOSENED. The original displaced Camera.main and halved its
        /// orthographicSize - which stopped being a breakage the moment framing moved onto the
        /// Cinemachine path, because the Brain restores BOTH from the vCam every LateUpdate. The
        /// control reported delta 0.000 and failed while the code was fine.
        ///
        /// A meta-test encodes an assumption about OWNERSHIP, so it breaks by design when
        /// ownership moves. That is the control working, and it must never be resolved with a
        /// tolerance change. It now breaks what the Brain will NOT put back: the proxy the
        /// follow component tracks, and the vCam's own lens.
        /// </summary>
        [UnityTest]
        public IEnumerator X17_C7_FailsWhenTheDriverNeverApplies()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneFixture.Load(FixtureMode.Playing, "Levels/study_3x6");
            yield return new WaitForSecondsRealtime(3.5f);

            var driver = Object.FindFirstObjectByType<BoardFramingDriver>();
            Assert.IsNotNull(driver, "X17: no BoardFramingDriver in the scene to disable");

            var vcam = CameraManager.Instance != null
                ? CameraManager.Instance.CameraFor(OperatingMode.Playing) : null;
            Assert.IsNotNull(vcam, "X17: no player vCam");

            var follow = vcam.GetComponent<CinemachineFollow>();
            Transform proxy = vcam.Follow != null ? vcam.Follow : vcam.transform;
            Assert.IsNotNull(proxy, "X17: no tracking target to displace");

            // THE BREAKAGE: stop the driver, then move the thing the follow component TRACKS and
            // change the vCam's own lens. The Brain propagates both rather than reverting them,
            // which is exactly why they are the right things to break.
            driver.enabled = false;
            proxy.position += new Vector3(0f, 0f, -25f);
            var lens = vcam.Lens;
            lens.OrthographicSize = 1.0f;
            vcam.Lens = lens;
            yield return new WaitForSecondsRealtime(1.0f);

            Assert.IsTrue(ExpectedPose(FixtureMode.Playing, out var want), "X17: no expected pose");
            var cam = Camera.main;
            float dPos = Vector3.Distance(cam.transform.position, want.position);
            float dSize = Mathf.Abs(cam.orthographicSize - want.orthographicSize);

            Debug.Log($"[X17] driver disabled, proxy displaced 25 and vCam lens forced to 1.0: " +
                      $"camera pos={cam.transform.position:F2} size={cam.orthographicSize:F2}  " +
                      $"want pos={want.position:F2} size={want.orthographicSize:F2}  " +
                      $"delta pos={dPos:F3} size={dSize:F3} (C7 allows 0.05)");

            Assert.IsTrue(dPos > 0.05f || dSize > 0.05f,
                "X17 META-FAILURE: the tracking target was displaced 25 units and the vCam lens " +
                "forced to 1.0 with the driver disabled, yet C7's comparison still reports a " +
                "match. C7 cannot detect an unapplied pose. Fix C7, not this control.");
        }

    }
}
