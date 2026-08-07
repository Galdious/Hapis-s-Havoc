using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
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

            return BoardFraming.TryFit(layout, orientation, Screen.width, Screen.height,
                                       BoardFraming.Projection.OrthographicTilted,
                                       out pose, out _);
        }

        [UnityTest]
        public IEnumerator C7_FramingIsAppliedAtRuntime()
        {
            LogAssert.ignoreFailingMessages = true;

            var rows = new List<string>();
            var failures = new List<string>();

            foreach (var lvl in new[] { "Levels/study_3x3", "Levels/study_3x6", "Levels/study_6x6" })
            {
                yield return SceneFixture.Load(FixtureMode.Playing, lvl);
                var boat = SceneFixture.Boat;
                if (boat != null) boat.DeselectBoat();
                yield return new WaitForSecondsRealtime(1.4f);

                var cam = Camera.main;
                Assert.IsNotNull(cam, "no main camera");
                Assert.IsTrue(ExpectedPose(FixtureMode.Playing, out var want),
                              $"{lvl}: BoardFraming could not compute a pose");

                float dPos = Vector3.Distance(cam.transform.position, want.position);
                float dSize = Mathf.Abs(cam.orthographicSize - want.orthographicSize);
                bool orthoOk = cam.orthographic == want.orthographic;

                rows.Add($"  {lvl.Replace("Levels/", ""),-12} " +
                         $"cam ortho={cam.orthographic} size={cam.orthographicSize:F2} pos={cam.transform.position:F2}\n" +
                         $"                want ortho={want.orthographic} size={want.orthographicSize:F2} pos={want.position:F2}\n" +
                         $"                delta pos={dPos:F3} size={dSize:F3}");

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
        /// X17 -> C7 must fail. Disables the driver and re-checks: a driver that never applies
        /// the pose must be caught, otherwise C7 would pass on a scene that simply happened to
        /// be posed correctly once.
        /// </summary>
        [UnityTest]
        public IEnumerator X17_C7_FailsWhenTheDriverNeverApplies()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneFixture.Load(FixtureMode.Playing, "Levels/study_3x6");
            yield return new WaitForSecondsRealtime(1.2f);

            var driver = Object.FindFirstObjectByType<BoardFramingDriver>();
            Assert.IsNotNull(driver, "X17: no BoardFramingDriver in the scene to disable");

            // THE BREAKAGE: shove the camera somewhere the driver would never leave it, and stop
            // the driver from correcting it.
            driver.enabled = false;
            var cam = Camera.main;
            cam.orthographicSize = 3.0f;
            cam.transform.position += new Vector3(0f, 0f, -25f);
            yield return new WaitForSecondsRealtime(0.6f);

            Assert.IsTrue(ExpectedPose(FixtureMode.Playing, out var want), "X17: no expected pose");
            float dPos = Vector3.Distance(cam.transform.position, want.position);
            float dSize = Mathf.Abs(cam.orthographicSize - want.orthographicSize);

            Debug.Log($"[X17] driver disabled and camera displaced: delta pos={dPos:F3} size={dSize:F3} " +
                      $"(C7 allows 0.05)");

            Assert.IsTrue(dPos > 0.05f || dSize > 0.05f,
                "X17 META-FAILURE: the camera was displaced 25 units and its ortho size halved " +
                "with the driver disabled, yet C7's comparison still reports a match. C7 cannot " +
                "detect an unapplied pose. Fix C7, not this control.");
        }
    }
}
