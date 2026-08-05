using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HapisHavoc.Tests
{
    /// <summary>
    /// STEP 2 acceptance gate. If captures are not byte-stable, every downstream diff is noise
    /// and the whole harness is worthless. Nothing else in the suite is trustworthy until these
    /// pass.
    ///
    /// Two levels of proof:
    ///   - within-run   : capture twice in one process, assert byte-identical.
    ///   - across-run   : each run writes a capture plus a sidecar hash; tools/determinism-gate.sh
    ///                    invokes Unity twice and diffs the two PNGs externally.
    /// </summary>
    [TestFixture]
    public class DeterminismGateTests
    {
        const string Suite = "determinism";

        [UnityTest]
        public IEnumerator D0_CaptureIsNotBlack()
        {
            yield return SceneFixture.Load(FixtureMode.Playing, "Levels/01_06_TestLevel");
            using (var ctx = new DeterministicContext())
            {
                var path = CaptureRig.Capture(ctx, Suite, "not-black-check");
                Assert.IsTrue(File.Exists(path), $"No capture written to {path}");
                Assert.IsTrue(CaptureRig.LooksRendered(path),
                    "Capture is effectively black. This is the classic -nographics failure: " +
                    "the render loop is dead, so every image comparison would trivially pass. " +
                    "Run with -batchmode and WITHOUT -nographics.");
            }
        }

        [UnityTest]
        public IEnumerator D1_SameCaptureTwiceInOneRun_IsByteIdentical()
        {
            yield return SceneFixture.Load(FixtureMode.Playing, "Levels/01_06_TestLevel");
            using (var ctx = new DeterministicContext())
            {
                var a = CaptureRig.Capture(ctx, Suite, "within-run-a");
                // Deliberately let real time pass between the two captures. If anything is
                // driven by wall-clock or by the frame loop, this is where it shows up.
                yield return new WaitForSecondsRealtime(0.75f);
                var b = CaptureRig.Capture(ctx, Suite, "within-run-b");

                Assert.IsTrue(CaptureRig.LooksRendered(a), "First capture is black.");

                var ia = PixelUtil.Load(a);
                var ib = PixelUtil.Load(b);
                PixelUtil.MeanMaxDelta(ia, ib, out float mean, out int max);
                float frac = PixelUtil.FractionDiffering(ia, ib, 0);

                Assert.IsTrue(PixelUtil.BytesIdentical(a, b),
                    $"Captures drifted within a single run. differing={frac:P4} meanDelta={mean:F4} maxDelta={max}. " +
                    "Something is still time- or frame-dependent; fix that before trusting any other assertion.");
            }
        }

        [UnityTest]
        public IEnumerator D2_WriteCrossRunArtifact()
        {
            yield return SceneFixture.Load(FixtureMode.Playing, "Levels/01_06_TestLevel");
            using (var ctx = new DeterministicContext())
            {
                var path = CaptureRig.Capture(ctx, Suite, "cross-run");
                Assert.IsTrue(CaptureRig.LooksRendered(path), "Cross-run artifact is black.");

                // Sidecar makes the external diff readable even without an image tool.
                var bytes = File.ReadAllBytes(path);
                using (var md5 = System.Security.Cryptography.MD5.Create())
                {
                    var hash = System.BitConverter.ToString(md5.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
                    File.WriteAllText(Path.ChangeExtension(path, ".md5"),
                        $"{hash}  bytes={bytes.Length}  quality={QualitySettings.names[QualitySettings.GetQualityLevel()]}  " +
                        $"rt={DeterministicContext.Width}x{DeterministicContext.Height}\n");
                    Debug.Log($"[DeterminismGate] cross-run md5={hash} bytes={bytes.Length}");
                }
            }
        }

        [UnityTest]
        public IEnumerator D3_SeedIsHonoured_TwoContextsSameSeedAgree()
        {
            yield return SceneFixture.Load(FixtureMode.Playing, "Levels/01_06_TestLevel");

            string first, second;
            using (var ctx = new DeterministicContext(seed: 4242))
                first = CaptureRig.Capture(ctx, Suite, "seed-4242-a");

            yield return new WaitForSecondsRealtime(0.25f);

            using (var ctx = new DeterministicContext(seed: 4242))
                second = CaptureRig.Capture(ctx, Suite, "seed-4242-b");

            Assert.IsTrue(PixelUtil.BytesIdentical(first, second),
                "Two DeterministicContexts with the same seed produced different images. " +
                "Context setup or teardown is leaking state.");
        }
    }
}
