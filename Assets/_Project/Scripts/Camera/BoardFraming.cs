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

    /// <summary>
    /// Measurement hook: HAPI_NO_AFFORDANCE_RESERVE=1 collects bounds with affordances
    /// contributing nothing, to measure the ceiling the reservation costs. A diagnostic, not a
    /// shipping option - nothing in the game reads this.
    /// </summary>
    static bool NoAffordanceReserve =>
        System.Environment.GetEnvironmentVariable("HAPI_NO_AFFORDANCE_RESERVE") == "1";

    /// <summary>
    /// What the LAST bounds collection actually reserved, and whether it reserved at all.
    /// Diagnostic. Exists because "the reservation must have been applied" is precisely the kind
    /// of assumption that has been wrong here repeatedly - a failing frame check now says whether
    /// the reservation was missing or merely too small, instead of leaving it to be inferred.
    /// </summary>
    public static Bounds LastReservation;
    public static bool LastReservationApplied;

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
        // RESERVED - and the reservation is ASKED FOR, not recomputed here. RiverControls owns
        // both, so the mode and lock-state rules that make it non-blanket live with the code that
        // places the affordances. See RiverControls' affordance geometry region.
        if (DebugBounds)
            Debug.Log($"[BOUNDSDBG] BEFORE reservation: any={any} count={count} " +
                      $"min={acc.min:F2} max={acc.max:F2} size={acc.size:F2}");

        if (any && grid != null && !NoAffordanceReserve)
        {
            // ASK, DO NOT RE-DERIVE. RiverControls owns where affordances go, so it owns how much
            // room they need; TryGetReservedBounds is computed by the SAME code that places them.
            //
            // What used to be here was a reimplementation of those formulas, and it drifted five
            // times. The last one was found by reading the two side by side rather than by any
            // test: the arrow reach used `cols * tileWidth` where RiverControls uses
            // `(cols-1) * (tileWidth + gapX) + tileWidth`, so every inter-tile gap was missing
            // from the reservation and the error grew with board width.
            //
            // Y is deliberately left alone. Arrows sit arrowHeight above the tiles, and folding
            // that into the fit would shrink the board to make room for empty air above it.
            var rc = Object.FindFirstObjectByType<RiverControls>(FindObjectsInactive.Include);
            Bounds reserved = default;
            LastReservationApplied = rc != null && rc.TryGetReservedBounds(out reserved);
            LastReservation = reserved;
            if (LastReservationApplied)
            {
                acc.SetMinMax(
                    new Vector3(Mathf.Min(acc.min.x, reserved.min.x), acc.min.y,
                                Mathf.Min(acc.min.z, reserved.min.z)),
                    new Vector3(Mathf.Max(acc.max.x, reserved.max.x), acc.max.y,
                                Mathf.Max(acc.max.z, reserved.max.z)));
            }
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
        //
        // SIGN: to make the board appear BELOW viewport centre (cy < 0.5) the camera must move
        // UP, so the shift is ADDED. It was subtracted, which put the board the same distance on
        // the WRONG SIDE - measured at viewport y-centre 0.55 in all three modes against a
        // boardRect centred on 0.45, a consistent 2*(0.5-cy) error.
        //
        // It hid for so long because boardRect is horizontally centred (cx = 0.5), so the X term
        // is identically zero and only Y could ever show it; and because a short board still fits
        // inside the rect when displaced by 0.1 of the viewport. Endless is the first board tall
        // enough to push an element out, which is how C2 finally caught it.
        float cx = cfg.boardRect.center.x;
        float cy = cfg.boardRect.center.y;
        Vector3 shift = right * ((0.5f - cx) * 2f * viewHalfW)
                      + up * ((0.5f - cy) * 2f * viewHalfH);

        pose.position = c - forward * distance + shift;
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
