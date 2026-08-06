using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HapisHavoc.Tests.EditorTools
{
    /// <summary>
    /// Authors the theme and world assets. Not an assertion - a one-shot generator, gated on
    /// HAPI_AUTHOR_THEMES=1 exactly like golden regeneration is gated on HAPI_REGENERATE_GOLDENS,
    /// so a normal suite run never touches assets on disk.
    ///
    /// It runs as an EditMode test because the Editor must stay closed for the harness, and this
    /// is the only sanctioned way to reach AssetDatabase. Letting Unity resolve the references
    /// is far safer than hand-writing the .asset YAML and guessing fileIDs.
    ///
    /// EGYPT IS SNAPSHOTTED, NOT DESIGNED. Every value is READ FROM THE CURRENT PROJECT so that
    /// applying Egypt reproduces today's appearance exactly. Nothing here is an improvement, a
    /// retint or a fix - if the placeholder palette looks bad, it must keep looking bad, or the
    /// goldens move and the job has failed its success condition.
    /// </summary>
    [TestFixture]
    public class ThemeAuthoring
    {
        const string ThemeDir = "Assets/_Project/Themes";
        // Under a Resources folder because PlayMode tests have no AssetDatabase - Resources.Load
        // is the only way X2 can reach this asset at runtime. It does mean the control ships in
        // a build; acceptable for a ~2 KB ScriptableObject, and noted rather than hidden.
        const string BrokenDir = "Assets/Tests/BrokenControls/Resources";
        const string Scene = "Assets/_Project/Scenes/LevelEditor.unity";

        static Color ReadColour(Material m, Color fallback)
        {
            if (m == null) return fallback;
            // Tile_Base_Mat carries BOTH _BaseColor and _Color with different values
            // (ARCHITECTURE.md R16), so the order here matters: prefer the URP/shadergraph name.
            if (m.HasProperty("_BaseColor")) return m.GetColor("_BaseColor");
            if (m.HasProperty("_Base_Color")) return m.GetColor("_Base_Color");
            if (m.HasProperty("_Color")) return m.GetColor("_Color");
            return fallback;
        }

        static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

        static Material MaterialOf(GameObject prefab)
        {
            if (prefab == null) return null;
            var r = prefab.GetComponentInChildren<Renderer>(true);
            return r != null ? r.sharedMaterial : null;
        }

        [Test]
        public void AuthorThemesAndWorlds()
        {
            if (System.Environment.GetEnvironmentVariable("HAPI_AUTHOR_THEMES") != "1")
                Assert.Ignore("Set HAPI_AUTHOR_THEMES=1 to (re)author theme and world assets.");

            Directory.CreateDirectory(ThemeDir);
            Directory.CreateDirectory(BrokenDir);

            // --- read the current project state -------------------------------------------
            EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);

            // PathVisualizer is PER TILE, not a scene singleton - it lives on the tile prefab
            // and is fetched with tile.GetComponent<PathVisualizer>(). So the authoritative
            // path colours are the prefab's serialised values, which every spawned tile copies.
            var tilePrefab = Load<GameObject>("Assets/_Project/Prefabs/DominoTile.prefab");
            Assert.IsNotNull(tilePrefab, "DominoTile.prefab not found");
            var paths = tilePrefab.GetComponent<PathVisualizer>();
            Assert.IsNotNull(paths, "DominoTile.prefab has no PathVisualizer to snapshot from");
            var cam = Camera.main;

            var tileMat    = Load<Material>("Assets/_Project/Materials/Tile_Base_Mat.mat");
            var vortexMat  = Load<Material>("Assets/_Project/Materials/Vortex_Material.mat");
            var bankMat    = Load<Material>("Assets/_Project/Materials/Bank_Base_Mat.mat");
            var boatMat    = Load<Material>("Assets/_Project/Materials/Boat_Base_Material.mat");
            var pathMat    = paths.lineMat;

            var blockerPrefab = Load<GameObject>("Assets/_Project/Prefabs/BlockerMarkerPrefab.prefab");
            var vortexPrefab  = Load<GameObject>("Assets/_Project/Prefabs/VortexMarker.prefab");
            var boatPrefab    = Load<GameObject>("Assets/_Project/Prefabs/BoatPrefab.prefab");
            var goalPrefab    = Load<GameObject>("Assets/_Project/Prefabs/EndMarker.prefab");
            var blockerMat    = MaterialOf(blockerPrefab);

            // --- Egypt: a snapshot of what the board looks like today ---------------------
            var egypt = ScriptableObject.CreateInstance<ThemeDefinition>();
            egypt.id = "egypt";
            egypt.displayName = "Egypt";

            egypt.tileMat = tileMat;
            egypt.pathMat = pathMat;
            egypt.blockerMat = blockerMat;
            egypt.vortexMat = vortexMat;
            egypt.bankMat = bankMat;
            egypt.boatMat = boatMat;

            egypt.tileBase = ReadColour(tileMat, Color.white);
            egypt.tileEdge = ReadColour(tileMat, Color.white);
            egypt.pathShallow = paths.defaultPathColor;      // exact current value - inertness
            egypt.pathDeep = paths.defaultPathColor;
            egypt.pathHighlight = paths.highlightColor;      // exact current value - inertness
            egypt.blocker = ReadColour(blockerMat, Color.black);
            egypt.vortex = ReadColour(vortexMat, Color.cyan);
            egypt.goal = ReadColour(MaterialOf(goalPrefab), Color.red);
            egypt.bankTop = ReadColour(bankMat, Color.grey);
            egypt.bankBottom = ReadColour(bankMat, Color.grey);
            egypt.uiAccent = ReadColour(Load<Material>("Assets/_Project/Materials/Arrow_Material.mat"), Color.yellow);
            egypt.backgroundTop = cam != null ? cam.backgroundColor : Color.white;
            egypt.backgroundBottom = cam != null ? cam.backgroundColor : Color.white;

            egypt.blockerPrefab = blockerPrefab;
            egypt.vortexPrefab = vortexPrefab;
            egypt.boatPrefab = boatPrefab;
            egypt.goalMarkerPrefab = goalPrefab;
            // bankPrefab intentionally null: banks are authored in the scene, not spawned.
            // audio intentionally unpopulated.

            AssetDatabase.CreateAsset(egypt, $"{ThemeDir}/Theme_Egypt.asset");

            // --- World 1 ------------------------------------------------------------------
            var world = ScriptableObject.CreateInstance<WorldDefinition>();
            world.worldIndex = 1;
            world.displayName = "Egypt";
            world.theme = egypt;
            world.levelResourcePaths.AddRange(new[]
            {
                "Levels/01_01_BasicMoves", "Levels/01_02_BonusMove", "Levels/01_03_BasicSkip",
                "Levels/01_04_SimplePush", "Levels/01_05_SimpleFall", "Levels/01_06_TestLevel",
                "Levels/01_07_OneWayPush",
            });
            world.unlockedMechanics = WorldDefinition.MechanicFlags.None;
            AssetDatabase.CreateAsset(world, $"{ThemeDir}/World_01_Egypt.asset");

            // --- The broken control: deliberately unreadable -------------------------------
            // A REAL second theme, not a mock. It proves theme SWAPPING works, and it is what
            // X2 uses to prove V5 can fail. Its path colour sits almost on top of its tile
            // colour, which is precisely the mistake V5 exists to catch.
            var unreadable = ScriptableObject.CreateInstance<ThemeDefinition>();
            unreadable.id = "unreadable";
            unreadable.displayName = "Unreadable (broken control)";

            var muddy = new Color(0.52f, 0.50f, 0.48f, 1f);
            var barelyDifferent = new Color(0.55f, 0.53f, 0.51f, 1f);

            unreadable.tileBase = muddy;
            unreadable.tileEdge = muddy;
            unreadable.pathShallow = barelyDifferent;
            unreadable.pathDeep = barelyDifferent;
            unreadable.pathHighlight = barelyDifferent;
            unreadable.blocker = muddy;
            unreadable.vortex = barelyDifferent;
            unreadable.goal = muddy;
            unreadable.bankTop = muddy;
            unreadable.bankBottom = muddy;
            unreadable.uiAccent = barelyDifferent;
            unreadable.backgroundTop = muddy;
            unreadable.backgroundBottom = muddy;

            // The control's MATERIALS and its PALETTE do different jobs and are set independently:
            //   - the PALETTE is near-monochrome, which is what makes V5 reject it (X2).
            //   - the MATERIALS must render far outside Egypt's gamut, which is what gives X12
            //     separation and proves the swap is real (T2).
            // Reusing Locked_Mat for the materials failed the second job: it renders close
            // enough to Egypt's palette that a fully repainted board still scored inside V6's
            // tolerance. So the control gets a purpose-built material in a hue Egypt never uses
            // - Egypt already covers pale blue, green, red, navy, cyan, black and amber, so
            // magenta is the one obvious gap.
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var garish = new Material(shader) { name = "BrokenControl_Magenta" };
            if (garish.HasProperty("_BaseColor")) garish.SetColor("_BaseColor", Color.magenta);
            if (garish.HasProperty("_Color")) garish.SetColor("_Color", Color.magenta);
            AssetDatabase.CreateAsset(garish, $"{BrokenDir}/BrokenControl_Magenta.mat");

            unreadable.tileMat = garish;
            unreadable.pathMat = pathMat;
            unreadable.blockerMat = garish;
            unreadable.vortexMat = garish;
            unreadable.bankMat = garish;
            unreadable.boatMat = garish;

            AssetDatabase.CreateAsset(unreadable, $"{BrokenDir}/Theme_Unreadable.asset");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // --- put ThemeService in the scene ---------------------------------------------
            // Wired to Egypt as BOTH reference and starting theme, so the remap it builds is
            // the identity map, which is empty - applying it touches nothing. That is what
            // makes adding this to the shipping scene visually inert.
            var existing = Object.FindFirstObjectByType<ThemeService>();
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var host = new GameObject("ThemeService");
            var svc = host.AddComponent<ThemeService>();
            var so = new SerializedObject(svc);
            so.FindProperty("referenceTheme").objectReferenceValue = egypt;
            so.FindProperty("startingTheme").objectReferenceValue = egypt;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(host.scene);
            EditorSceneManager.SaveScene(host.scene);
            Debug.Log("[AUTHOR] ThemeService added to LevelEditor.unity, reference=starting=Egypt " +
                      "(identity remap, so visually inert).");

            Debug.Log($"[AUTHOR] Egypt snapshotted from the live project:\n" +
                      $"  tileMat={tileMat?.name} pathMat={pathMat?.name} bankMat={bankMat?.name} " +
                      $"boatMat={boatMat?.name} vortexMat={vortexMat?.name} blockerMat={blockerMat?.name}\n" +
                      $"  tileBase={egypt.tileBase} pathShallow={egypt.pathShallow} " +
                      $"pathHighlight={egypt.pathHighlight}\n" +
                      $"  background={egypt.backgroundBottom} (camera clearFlags=" +
                      $"{(cam != null ? cam.clearFlags.ToString() : "n/a")})\n" +
                      $"  contrast(pathShallow vs tileBase) = " +
                      $"{PixelUtil.ContrastRatio(egypt.pathShallow, egypt.tileBase):F3}:1\n" +
                      $"[AUTHOR] Unreadable control contrast = " +
                      $"{PixelUtil.ContrastRatio(unreadable.pathShallow, unreadable.tileBase):F3}:1");
        }
    }
}
