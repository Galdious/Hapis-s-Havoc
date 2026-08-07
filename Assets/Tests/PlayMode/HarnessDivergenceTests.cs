using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace HapisHavoc.Tests
{
    /// <summary>
    /// X18. The harness must not quietly grow a new suppression.
    ///
    /// Three bugs have hidden behind suppressions that each looked reasonable on the day: the
    /// hand palette concealed the framing bounds bug, BoardFraming being harness-only meant
    /// framing never applied in play, and CinemachineBrain meant the captured camera was never
    /// the real one. Each was found by accident, sessions later.
    ///
    /// This cannot compare harness pixels against live pixels - the live game is
    /// non-deterministic, which is why DeterministicContext exists. So the guard is structural:
    /// the declared list must match a reviewed allowlist, and adding to one without the other
    /// fails loudly.
    /// </summary>
    [TestFixture]
    public class HarnessDivergenceTests
    {
        /// <summary>
        /// Reviewed and documented in docs/audit/HARNESS_DIVERGENCE.md. Adding an entry here
        /// means: you have written down what it changes, whether it is genuinely absent in play,
        /// and what a bug hiding behind it would look like.
        /// </summary>
        static readonly string[] Allowlist =
        {
            "CinemachineBrain", "UniversalCameraController", "EndlessModeManager",
            "BoardFramingDriver", "FPSCounter", "QualityLevel:Mobile", "Time.captureDeltaTime",
            "Random.InitState", "Camera.targetTexture", "HandPalettes", "BoatCoroutines",
            "PathVisualizerCoroutines", "GridManagerCoroutines", "TileLocalScale",
            "ShaderGlobalTime",
        };

        [Test]
        public void X18_HarnessSuppressionsMatchTheReviewedAllowlist()
        {
            var declared = new HashSet<string>(DeterministicContext.Suppressions);
            var allowed = new HashSet<string>(Allowlist);

            var undocumented = declared.Except(allowed).ToList();
            var stale = allowed.Except(declared).ToList();

            Debug.Log($"[X18] {declared.Count} declared suppression(s), {allowed.Count} allowed");

            Assert.IsEmpty(undocumented,
                "X18: the harness suppresses something that is NOT in the reviewed allowlist:\n  " +
                string.Join("\n  ", undocumented) +
                "\n  Add it to docs/audit/HARNESS_DIVERGENCE.md with what it changes, whether it " +
                "is genuinely absent in play, and what a bug hiding behind it would look like - " +
                "then add it here. Three real bugs have already hidden behind suppressions that " +
                "looked reasonable at the time.");

            Assert.IsEmpty(stale,
                "X18: the allowlist names suppressions the harness no longer performs:\n  " +
                string.Join("\n  ", stale) + "\n  Remove them, so the list keeps meaning something.");
        }
    }
}
