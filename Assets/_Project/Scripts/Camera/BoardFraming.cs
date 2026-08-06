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

    public const float DefaultPitch = 55f;
    public const float ShallowPitch = 38f;
    public const float DefaultFov = 40f;

    public static float PitchFor(Projection p) =>
        p == Projection.OrthographicShallow ? ShallowPitch : DefaultPitch;

    public static bool IsOrthographic(Projection p) => p != Projection.PerspectiveTilted;

    // ---------------------------------------------------------------- bounds

    /// <summary>
    /// The bounds everything must fit inside. Built from RENDERERS rather than from each
    /// manager's serialised numbers, so it stays correct when a prefab changes size or a theme
    /// swaps in a differently-shaped bank - and so nothing has to be kept in sync by hand.
    /// </summary>
    public static bool TryCollectBoardBounds(out Bounds bounds, out int contributors)
    {
        // Locals, because C# forbids touching an `out` parameter from a local function.
        var acc = new Bounds();
        int count = 0;
        bool any = false;

        void Add(Renderer r)
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) return;
            // LineRenderers for paths sit inside tile bounds already, and a degenerate one
            // reports an empty box at the origin that would drag the fit to (0,0,0).
            if (r.bounds.size.sqrMagnitude <= 0f) return;
            if (!any) { acc = r.bounds; any = true; }
            else acc.Encapsulate(r.bounds);
            count++;
        }

        void AddUnder(Component c)
        {
            if (c == null) return;
            foreach (var r in c.GetComponentsInChildren<Renderer>(false)) Add(r);
        }

        // Tiles.
        foreach (var t in Object.FindObjectsByType<TileInstance>(FindObjectsInactive.Exclude,
                                                                FindObjectsSortMode.None))
            AddUnder(t);

        // Banks - the boat embarks from them, so they are part of the playfield.
        foreach (var b in Object.FindObjectsByType<RiverBankManager>(FindObjectsInactive.Exclude,
                                                                    FindObjectsSortMode.None))
            AddUnder(b);

        // Push arrows (Editor) and drop zones (Playing/Endless). THE ONES THAT GET FORGOTTEN.
        foreach (var rc in Object.FindObjectsByType<RiverControls>(FindObjectsInactive.Exclude,
                                                                  FindObjectsSortMode.None))
            AddUnder(rc);

        // Boat and goal.
        foreach (var boat in Object.FindObjectsByType<BoatController>(FindObjectsInactive.Exclude,
                                                                     FindObjectsSortMode.None))
            AddUnder(boat);
        foreach (var g in Object.FindObjectsByType<GoalMarker>(FindObjectsInactive.Exclude,
                                                              FindObjectsSortMode.None))
            AddUnder(g);

        bounds = acc;
        contributors = count;
        return any;
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
    {
        var cfg = layout.For(orientation);
        float aspect = (float)targetWidth / targetHeight;
        float pitch = PitchFor(projection);
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
    {
        pose = default;
        if (!TryCollectBoardBounds(out bounds, out _)) return false;
        pose = Fit(bounds, layout, orientation, targetWidth, targetHeight, projection);
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
