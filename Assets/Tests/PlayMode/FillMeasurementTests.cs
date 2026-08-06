using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HapisHavoc.Tests
{
    /// <summary>
    /// ITEM 3.0 / 3(a). Reports board fill per level. A measurement, not an assertion - fill is
    /// bounded by geometry (a 3x3 board in a 9:16 frame simply cannot fill much), so a fixed
    /// threshold would fail correctly-framed levels.
    ///
    /// Set HAPI_NO_AFFORDANCE_RESERVE=1 to read the CEILING: what fill would be if push
    /// affordances contributed nothing to world bounds at all.
    /// </summary>
    [TestFixture]
    public class FillMeasurementTests
    {
        [UnityTest]
        public IEnumerator F1_FillPerLevel()
        {
            LogAssert.ignoreFailingMessages = true;
            bool noReserve = System.Environment.GetEnvironmentVariable("HAPI_NO_AFFORDANCE_RESERVE") == "1";

            var lines = new List<string>();
            foreach (var lvl in SceneFixture.AllLevels)
            {
                yield return SceneFixture.Load(FixtureMode.Playing, lvl);
                var boat = SceneFixture.Boat;
                if (boat != null) boat.DeselectBoat();
                yield return new WaitForSecondsRealtime(1.0f);

                using (var ctx = new DeterministicContext(
                    projection: BoardFraming.Projection.OrthographicTilted))
                {
                    ctx.Quiesce();
                    ctx.FrameBoard();
                    float fill = BoardFraming.FillFraction(ctx.FramedBounds, ctx.Cam);
                    var b = ctx.FramedBounds;
                    lines.Add($"  {lvl.Replace("Levels/", ""),-22} fill={fill:P2}  " +
                              $"boundsX=[{b.min.x,6:F2},{b.max.x,6:F2}]={b.size.x,5:F2}");
                }
                yield return new WaitForSecondsRealtime(0.2f);
            }

            Debug.Log($"[F1] board fill per level, ortho tilted, portrait " +
                      $"(affordance reserve {(noReserve ? "DISABLED - ceiling" : "enabled")}):\n" +
                      string.Join("\n", lines));
            Assert.IsNotEmpty(lines);
        }
    }
}
