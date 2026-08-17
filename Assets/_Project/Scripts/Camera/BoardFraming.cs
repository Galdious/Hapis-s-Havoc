using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fit-to-rect camera framing. Pure maths, no MonoBehaviour, no scene lookups of its own - so it
/// is callable identically from the game, the Editor and the test harness, and the harness can
/// pin its INPUTS rather than pinning a camera pose.
///
/// WHAT COUNTS AS "THE BOARD"
/// ==========================
/// Everything the player must SEE or TOUCH, which is emphatically NOT just the tiles. Push
/// arrows sit `RiverControls.arrowDistance` (4 units, against a 2-unit tile) OUTSIDE the grid,
/// so a camera framed to tile bounds crops the controls off-screen and the level becomes
/// literally unplayable while every tile looks perfectly framed. Banks, the boat and the goal
/// marker are the same story in milder form. C2 is the assertion that stops that shipping.
/// </summary>
public static class BoardFraming
{
    public enum Projection { PerspectiveTilted, OrthographicTilted, OrthographicShallow }

    public struct Pose
    {
        public Vector3 position;
        public Quaternion rotation;
        public bool orthographic;
        public float fieldOfView;      // perspective only
        public float orthographicSize; // orthographic only

        public void ApplyTo(Camera cam)
        {
            cam.transform.SetPositionAndRotation(position, rotation);
            cam.orthographic = orthographic;
            if (orthographic) cam.orthographicSize = orthographicSize;
            else cam.fieldOfView = fieldOfView;
        }
    }

    /// <summary>
    /// Fallback only. The shipping vCams author their own angle and BoardFramingDriver reads it
    /// with <see cref="PitchOf"/>; this is what the harness uses when no camera is supplied.
    /// 70 to match the authored VCam_Player, chosen from the Z2 comparison.
    /// </summary>
    public const float DefaultPitch = 70f;
    public const float ShallowPitch = 38f;
    public const float DefaultFov = 40f;

    /// <summary>Default pitch for a projection, used only when no camera pitch is supplied.</summary>
    public static float PitchFor(Projection p) =>
        p == Projection.OrthographicShallow ? ShallowPitch : DefaultPitch;

    /// <summary>
    /// The downward pitch of a transform, in degrees, from its FORWARD BASIS VECTOR rather than
    /// eulerAngles - representation-independent, per CLAUDE.md gotcha 3. For a rotation about X
    /// by p, forward is (0, -sin p, cos p), so p = asin(-forward.y).
    /// </summary>
    public static float PitchOf(Transform t) =>
        t == null ? DefaultPitch
                  : Mathf.Asin(Mathf.Clamp(-t.forward.y, -1f, 1f)) * Mathf.Rad2Deg;

    public static bool IsOrthographic(Projection p) => p != Projection.PerspectiveTilted;

    // ---------------------------------------------------------------- bounds

    /// <summary>
    /// The bounds everything must fit inside: the board, and everything the player must SEE or
    /// TOUCH. Built from RENDERERS rather than serialised numbers, so it stays correct when a
    /// prefab changes size or a theme swaps in a differently-shaped bank.
    ///
    /// TWO THINGS THIS GETS RIGHT THAT THE FIRST VERSION DID NOT, both measured, not guessed:
    ///
    /// 1. NON-BOARD TILES ARE EXCLUDED. Hand and palette tiles carry TileInstance, so a global
    ///    sweep collects them as board geometry. In Editor that dragged the X span to 18.30
    ///    against a ~7-unit grid and made WIDTH bind at 2.54x. It was invisible in every golden
    ///    because the capture rig hides hand palettes in Quiesce(). Excluded by ANCESTRY, not by
    ///    name - names drift, hierarchy does not. C5 is the permanent guard.
    ///
    /// 2. PUSH AFFORDANCES ARE INCLUDED. Arrows and row locks are parented to
    ///    GridManager.gridParent, and drop zones live on a separate UI canvas - NOT under
    ///    RiverControls, which is where this used to look. So they were never in the bounds and
    ///    were framed by coincidence: measured, Lock_Row0 sat at viewport x=1.000..1.025, fully
    ///    off-screen and untappable. C2 is the guard.
    /// </summary>
    /// <summary>Set for one call to dump every contributor and the reservation step.</summary>
    public static bool DebugBounds;

    /// <summary>Names of every renderer that contributed to the LAST collection. Diagnostic:
    /// lets two collections at different moments be diffed rather than guessed about.</summary>
    public static readonly List<string> LastContributors = new List<string>();

    public static bool TryCollectBoardBounds(out Bounds bounds, out int contributors)
    {
        // Locals, because C# forbids touching an `out` parameter from a local function.
        var acc = new Bounds();
        int count = 0;
        bool any = false;
        LastContributors.Clear();

        void Add(Renderer r)
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) return;
            // A degenerate renderer reports an empty box at the origin, which would drag the
            // fit to (0,0,0).
            if (r.bounds.size.sqrMagnitude <= 0f) return;
            if (!any) { acc = r.bounds; any = true; }
            else acc.Encapsulate(r.bounds);
            count++;
            LastContributors.Add($"{Path(r.transform)}");
            if (DebugBounds && count <= 12)
                Debug.Log($"[BOUNDSDBG] +{count} '{r.name}' rb={r.bounds.size:F2} -> acc={acc.size:F2}");
        }

        void AddUnder(Component c)
        {
            if (c == null) return;
            foreach (var r in c.GetComponentsInChildren<Renderer>(false)) Add(r);
        }

        // Tiles - BoardTile, so inventory is excluded STRUCTURALLY rather than by a filter
        // this sweep had to remember. Hand and palette tiles carry TileInstance but never
        // BoardTile. See BoardTile for why the default is inverted.
        foreach (var t in Object.FindObjectsByType<BoardTile>(FindObjectsInactive.Exclude,
                                                             FindObjectsSortMode.None))
            AddUnder(t);

        // Banks - the boat embarks from them, so they are part of the playfield.
        foreach (var b in Object.FindObjectsByType<RiverBankManager>(FindObjectsInactive.Exclude,
                                                                    FindObjectsSortMode.None))
            AddUnder(b);

        // Boat and goal.
        foreach (var boat in Object.FindObjectsByType<BoatController>(FindObjectsInactive.Exclude,
                                                                     FindObjectsSortMode.None))
            AddUnder(boat);
        foreach (var g in Object.FindObjectsByType<GoalMarker>(FindObjectsInactive.Exclude,
                                                              FindObjectsSortMode.None))
            AddUnder(g);

        var grid = Object.FindFirstObjectByType<GridManager>();

        // Push arrows and row locks, which live under gridParent.
        if (grid != null && grid.gridParent != null)
            foreach (var t in grid.gridParent.GetComponentsInChildren<Transform>(false))
                if (t.name.StartsWith("Arrow_") || t.name.StartsWith("Lock_"))
                    foreach (var r in t.GetComponents<Renderer>()) Add(r);

        // AFFORDANCE RESERVATION - static, but honest per level and per side.
        //
        // Drop zones are transient (created during a drag), so including them only when they
        // happen to exist would lurch the camera mid-interaction. Their extent is therefore
        // RESERVED. But the reservation must not be blanket:
        //   - push arrows and row locks are EDITOR-ONLY (RiverControls gates CreateArrowsForRow
        //     on currentMode == Editor), so Playing/Endless must not reserve arrow width at all;
        //   - drop zones exist only on UNLOCKED sides, so a fully locked level - 01_01, 01_02
        //     and 01_03 all have lockedRows [3,3,3] - reserves nothing;
        //   - locks sit furthest out and only on the right, so the two sides differ.
        // lockedRows is fixed at load, so this is still static and still cannot thrash.
        if (DebugBounds)
            Debug.Log($"[BOUNDSDBG] BEFORE reservation: any={any} count={count} " +
                      $"min={acc.min:F2} max={acc.max:F2} size={acc.size:F2}");

        if (any && grid != null)
        {
            AffordanceReserveX(grid, out float leftReach, out float rightReach);
            float centreX = grid.cols > 0 && grid.rows > 0
                ? (grid.GetWorldPosition(0, 0).x + grid.GetWorldPosition(grid.cols - 1, 0).x) * 0.5f
                : acc.center.x;

            float minX = Mathf.Min(acc.min.x, centreX - leftReach);
            float maxX = Mathf.Max(acc.max.x, centreX + rightReach);

            // Z TOO. Drop zones are not only offset sideways: RiverControls places them at
            // rowCenter.z - 0.25 with height tileHeight + gapZ*0.5, so the outermost rows' zones
            // reach BEYOND the tile block in Z. Reserving X alone left that unframed - the
            // reframe check at padding 0.20 caught DropZone_Row7 overflowing by ~0.001 of
            // viewport height in Endless. Same static per-level basis as X, so still no thrash.
            AffordanceReserveZ(grid, out float reservedMinZ, out float reservedMaxZ, out bool anyZ);
            float minZ = anyZ ? Mathf.Min(acc.min.z, reservedMinZ) : acc.min.z;
            float maxZ = anyZ ? Mathf.Max(acc.max.z, reservedMaxZ) : acc.max.z;

            acc.SetMinMax(new Vector3(minX, acc.min.y, minZ),
                          new Vector3(maxX, acc.max.y, maxZ));
        }

        if (DebugBounds)
            Debug.Log($"[BOUNDSDBG] AFTER reservation: min={acc.min:F2} max={acc.max:F2} size={acc.size:F2}");

        bounds = acc;
        contributors = count;
        return any;
    }

    /// <summary>Root-relative path, so two renderers with the same leaf name are distinguishable.</summary>
    static string Path(Transform t)
    {
        var parts = new List<string>();
        for (var c = t; c != null; c = c.parent) parts.Add(c.name);
        parts.Reverse();
        return string.Join("/", parts);
    }

    /// <summary>
    /// How far the side affordances reach from the grid centre, per side, reproducing
    /// RiverControls' own placement maths so it holds whether or not the objects exist yet.
    /// Set HAPI_NO_AFFORDANCE_RESERVE=1 to measure the ceiling with affordances contributing
    /// nothing - that is ITEM 3(a), a measurement hook, not a shipping option.
    /// </summary>
    static void AffordanceReserveX(GridManager grid, out float leftReach, out float rightReach)
    {
        leftReach = 0f;
        rightReach = 0f;

        if (System.Environment.GetEnvironmentVariable("HAPI_NO_AFFORDANCE_RESERVE") == "1") return;

        var rc = Object.FindFirstObjectByType<RiverControls>();
        if (rc == null) return;

        bool editorMode = GameManager.Instance == null
                       || GameManager.Instance.currentMode == OperatingMode.Editor;

        float gridWidth = grid.cols * grid.tileWidth;
        float gridHalfWidth = (gridWidth + (grid.cols - 1) * grid.gapX) * 0.5f;

        for (int row = 0; row < grid.rows; row++)
        {
            var state = rc.GetRowLockState(row);
            bool leftOpen = state != RowLockState.LeftLocked && state != RowLockState.BothLocked;
            bool rightOpen = state != RowLockState.RightLocked && state != RowLockState.BothLocked;

            if (editorMode)
            {
                // Arrows exist on both sides in the Editor regardless of lock state, because the
                // lock toggle is how you change that state - it must stay reachable.
                float dyn = Mathf.Max(rc.arrowDistance, gridWidth * 0.5f + 1f);
                leftReach = Mathf.Max(leftReach, dyn + rc.arrowSpacing * 0.5f + rc.arrowScale * 2f);
                // The lock sits beyond the red arrow, on the right only.
                rightReach = Mathf.Max(rightReach, dyn + rc.arrowSpacing * 1.5f + rc.arrowScale * 2f);
            }
            else
            {
                // Drop zone outer edge: centre - gridHalfWidth - zoneWidth/2 + 2, minus another
                // zoneWidth/2 for its own half-extent.
                float zoneWidth = grid.tileWidth * 1.5f;
                float reach = gridHalfWidth + zoneWidth - 2f;
                if (leftOpen) leftReach = Mathf.Max(leftReach, reach);
                if (rightOpen) rightReach = Mathf.Max(rightReach, reach);
            }
        }
    }

    /// <summary>
    /// Z extent the drop zones need. Editor reserves NOTHING here: it has arrows rather than
    /// zones, and arrows carry no Z offset. Zones exist only on sides that are unlocked, so a
    /// fully locked level reserves nothing either.
    /// </summary>
    static void AffordanceReserveZ(GridManager grid, out float minZ, out float maxZ, out bool any)
    {
        minZ = 0f; maxZ = 0f; any = false;

        if (System.Environment.GetEnvironmentVariable("HAPI_NO_AFFORDANCE_RESERVE") == "1") return;

        var rc = Object.FindFirstObjectByType<RiverControls>();
        if (rc == null || grid == null || grid.rows <= 0) return;

        bool editorMode = GameManager.Instance == null
                       || GameManager.Instance.currentMode == OperatingMode.Editor;
        if (editorMode) return;

        float halfHeight = (grid.tileHeight + grid.gapZ * 0.5f) * 0.5f;
        const float zoneZOffset = 0.25f;      // RiverControls: rowCenter.z - 0.25

        for (int row = 0; row < grid.rows; row++)
        {
            if (rc.GetRowLockState(row) == RowLockState.BothLocked) continue;

            float rowZ = grid.GetWorldPosition(0, row).z - zoneZOffset;
            float lo = rowZ - halfHeight, hi = rowZ + halfHeight;
            if (!any) { minZ = lo; maxZ = hi; any = true; }
            else { minZ = Mathf.Min(minZ, lo); maxZ = Mathf.Max(maxZ, hi); }
        }
    }

    // ---------------------------------------------------------------- fit

    /// <summary>
    /// Fits <paramref name="bounds"/> into the layout's boardRect for the given render target.
    ///
    /// Works the same for both projections by transforming the bounds corners into the camera's
    /// own basis and fitting the resulting axis-aligned extents. Fitting the world-space box
    /// directly would over-estimate, because a tilted camera sees a rotated box.
    /// </summary>
    public static Pose Fit(Bounds bounds, BoardLayout layout, BoardLayout.Orientation orientation,
                           int targetWidth, int targetHeight, Projection projection)
        => Fit(bounds, layout, orientation, targetWidth, targetHeight, projection,
               PitchFor(projection));

    /// <summary>
    /// PITCH IS AN INPUT, not a constant. The fit derives orthographic size from it - extU scales
    /// with cos(pitch) - so fitting at 55 and rendering at the authored 70 is wrong, and not
    /// subtly. Framing fits THE CAMERA THAT EXISTS; it does not impose an angle. Use
    /// <see cref="PitchOf"/> to read it from the camera that will render.
    /// </summary>
    public static Pose Fit(Bounds bounds, BoardLayout layout, BoardLayout.Orientation orientation,
                           int targetWidth, int targetHeight, Projection projection,
                           float pitchDegrees)
    {
        var cfg = layout.For(orientation);
        float aspect = (float)targetWidth / targetHeight;
        float pitch = pitchDegrees;
        var rotation = Quaternion.Euler(pitch, 0f, 0f);

        // Camera basis.
        Vector3 right = rotation * Vector3.right;
        Vector3 up = rotation * Vector3.up;
        Vector3 forward = rotation * Vector3.forward;

        // Extents of the bounds in that basis.
        Vector3 c = bounds.center, e = bounds.extents;
        float extR = 0f, extU = 0f, extF = 0f;
        for (int i = 0; i < 8; i++)
        {
            var corner = new Vector3(
                (i & 1) == 0 ? -e.x : e.x,
                (i & 2) == 0 ? -e.y : e.y,
                (i & 4) == 0 ? -e.z : e.z);
            extR = Mathf.Max(extR, Mathf.Abs(Vector3.Dot(corner, right)));
            extU = Mathf.Max(extU, Mathf.Abs(Vector3.Dot(corner, up)));
            extF = Mathf.Max(extF, Mathf.Abs(Vector3.Dot(corner, forward)));
        }

        // The board is fitted into boardRect, not the whole viewport, and padding shrinks that
        // rect further. Padding is a fraction of the SMALLER side so it reads as an even margin
        // rather than a much bigger gap on the long axis.
        float pad = cfg.padding * Mathf.Min(cfg.boardRect.width, cfg.boardRect.height);
        float rectW = Mathf.Max(0.01f, cfg.boardRect.width - 2f * pad);
        float rectH = Mathf.Max(0.01f, cfg.boardRect.height - 2f * pad);

        var pose = new Pose { rotation = rotation, orthographic = IsOrthographic(projection) };

        float viewHalfW, viewHalfH, distance;

        if (pose.orthographic)
        {
            // Half-height of the FULL viewport such that the board occupies rectW x rectH of it.
            float halfHForU = extU / rectH;
            float halfHForR = extR / (rectW * aspect);
            pose.orthographicSize = Mathf.Max(halfHForU, halfHForR);

            viewHalfH = pose.orthographicSize;
            viewHalfW = pose.orthographicSize * aspect;
            // Ortho has no perspective, so distance only has to clear the geometry.
            distance = extF + 10f;
            pose.fieldOfView = DefaultFov;
        }
        else
        {
            pose.fieldOfView = DefaultFov;
            float tanV = Mathf.Tan(DefaultFov * Mathf.Deg2Rad * 0.5f);
            float tanH = tanV * aspect;

            // Distance at which the board's extents occupy rectW x rectH of the frustum, plus
            // extF so the NEAR face of the box - not its centre - is what clears.
            float distU = extU / (rectH * tanV);
            float distR = extR / (rectW * tanH);
            distance = Mathf.Max(distU, distR) + extF;

            viewHalfH = distance * tanV;
            viewHalfW = viewHalfH * aspect;
        }

        // Shift so the board lands at boardRect's CENTRE rather than the viewport's. Moving the
        // camera perpendicular to its view direction translates the image by the same amount at
        // the focal plane, so this is exact for ortho and correct at the board's depth for
        // perspective.
        float cx = cfg.boardRect.center.x;
        float cy = cfg.boardRect.center.y;
        Vector3 shift = right * ((0.5f - cx) * 2f * viewHalfW)
                      + up * ((0.5f - cy) * 2f * viewHalfH);

        pose.position = c - forward * distance - shift;
        return pose;
    }

    /// <summary>Convenience: collect the bounds and fit them in one call.</summary>
    public static bool TryFit(BoardLayout layout, BoardLayout.Orientation orientation,
                              int targetWidth, int targetHeight, Projection projection,
                              out Pose pose, out Bounds bounds)
        => TryFit(layout, orientation, targetWidth, targetHeight, projection,
                  PitchFor(projection), out pose, out bounds);

    public static bool TryFit(BoardLayout layout, BoardLayout.Orientation orientation,
                              int targetWidth, int targetHeight, Projection projection,
                              float pitchDegrees, out Pose pose, out Bounds bounds)
    {
        pose = default;
        if (!TryCollectBoardBounds(out bounds, out _)) return false;
        pose = Fit(bounds, layout, orientation, targetWidth, targetHeight, projection, pitchDegrees);
        return true;
    }

    // ---------------------------------------------------------------- measurement helpers

    /// <summary>
    /// Screen rect of a world-space Bounds under a camera, in normalised viewport coords.
    /// Used by C1/C2/C3 and by the fill measurement, so tests and framing agree by construction.
    /// </summary>
    public static Rect ViewportRectOf(Bounds b, Camera cam)
    {
        Vector3 c = b.center, e = b.extents;
        float minX = 1f, minY = 1f, maxX = 0f, maxY = 0f;
        for (int i = 0; i < 8; i++)
        {
            var w = c + new Vector3((i & 1) == 0 ? -e.x : e.x,
                                    (i & 2) == 0 ? -e.y : e.y,
                                    (i & 4) == 0 ? -e.z : e.z);
            var v = cam.WorldToViewportPoint(w);
            minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x);
            minY = Mathf.Min(minY, v.y); maxY = Mathf.Max(maxY, v.y);
        }
        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    /// <summary>Fraction of the frame the board's projected rect covers. Reported, never asserted
    /// against a fixed threshold - a 3x3 board in portrait simply cannot fill much of a tall
    /// frame, and a fixed number would fail levels that are correctly framed.</summary>
    public static float FillFraction(Bounds b, Camera cam)
    {
        var r = ViewportRectOf(b, cam);
        return Mathf.Clamp01(r.width) * Mathf.Clamp01(r.height);
    }
}
