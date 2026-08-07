using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HapisHavoc.Tests.EditorTools
{
    /// <summary>
    /// STEP 0a. Makes the scene camera ORTHOGRAPHIC, which won the projection comparison on both
    /// axes measured: foreshortening 1.000 vs 1.114, and board fill 47.75% vs 40.89%.
    ///
    /// Gated on HAPI_ADOPT_ORTHO=1 like the other one-shot authoring passes, so a normal suite
    /// run never edits the scene.
    /// </summary>
    [TestFixture]
    public class CameraProjectionAdoption
    {
        const string Scene = "Assets/_Project/Scenes/LevelEditor.unity";

        [Test]
        public void AdoptOrthographicCamera()
        {
            if (System.Environment.GetEnvironmentVariable("HAPI_ADOPT_ORTHO") != "1")
                Assert.Ignore("Set HAPI_ADOPT_ORTHO=1 to switch the scene camera to orthographic.");

            EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            var cam = Camera.main ?? Object.FindFirstObjectByType<Camera>();
            Assert.IsNotNull(cam, "no camera in the scene");

            bool wasOrtho = cam.orthographic;
            float wasSize = cam.orthographicSize;
            float wasFov = cam.fieldOfView;

            cam.orthographic = true;
            // Sized for the default 6x6 board plus banks. STATIC, because nothing in the GAME
            // applies BoardFraming - it is harness-only - so there is no fit-to-rect to compute
            // this at runtime. Flagged rather than silently accepted.
            cam.orthographicSize = 7.5f;
            cam.transform.rotation = Quaternion.Euler(BoardFraming.DefaultPitch, 0f, 0f);

            EditorSceneManager.MarkSceneDirty(cam.gameObject.scene);
            EditorSceneManager.SaveScene(cam.gameObject.scene);

            Debug.Log($"[ORTHO] camera was orthographic={wasOrtho} size={wasSize} fov={wasFov}\n" +
                      $"[ORTHO] camera now orthographic={cam.orthographic} size={cam.orthographicSize} " +
                      $"pitch={BoardFraming.DefaultPitch}");
        }
    }
}
