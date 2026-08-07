using NUnit.Framework;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HapisHavoc.Tests.EditorTools
{
    /// <summary>
    /// Sets the CinemachineBrain's DefaultBlend to Cut.
    ///
    /// Justified independently of the harness: the board has three near-static vCam poses and
    /// the project notes describe camera transitions as instant, yet the Brain carried a
    /// 2-second EaseInOut. In play that blend DOES complete, so every level load drifted the
    /// camera for two seconds before settling.
    ///
    /// Gated like the other one-shot authoring passes.
    /// </summary>
    [TestFixture]
    public class BrainBlendAdoption
    {
        const string Scene = "Assets/_Project/Scenes/LevelEditor.unity";

        [Test]
        public void SetDefaultBlendToCut()
        {
            if (System.Environment.GetEnvironmentVariable("HAPI_BLEND_CUT") != "1")
                Assert.Ignore("Set HAPI_BLEND_CUT=1 to set the Brain's DefaultBlend to Cut.");

            EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            var brain = Object.FindFirstObjectByType<CinemachineBrain>();
            Assert.IsNotNull(brain, "no CinemachineBrain in the scene");

            var was = $"{brain.DefaultBlend.Style} {brain.DefaultBlend.Time:F2}s";
            brain.DefaultBlend = new CinemachineBlendDefinition(
                CinemachineBlendDefinition.Styles.Cut, 0f);

            EditorSceneManager.MarkSceneDirty(brain.gameObject.scene);
            EditorSceneManager.SaveScene(brain.gameObject.scene);
            Debug.Log($"[BLEND] DefaultBlend was {was}, now {brain.DefaultBlend.Style} " +
                      $"{brain.DefaultBlend.Time:F2}s");
        }
    }
}
