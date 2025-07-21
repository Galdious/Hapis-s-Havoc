using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(TileInstance))]
public class PathVisualizer : MonoBehaviour
{
    [Header("Line look")]
    public Material lineMat;
    public float    width = 0.07f;
    [Range(6,32)]
    public int      segsPerBezier = 18;      // smoothness for ¼-arcs and half-arc

    TileInstance tile;
    Transform    centre;
    readonly List<GameObject> pool = new();

    /* ─────────────────────────────────────── */
    void Awake()
    {
        tile   = GetComponent<TileInstance>();
        centre = transform.Find("Snap_Centre");
    }

    /* ─────────────────────────────────────── */
    public void DrawPaths()
    {
        foreach (var g in pool) if (g) Destroy(g);
        pool.Clear();
        foreach (var c in tile.connections)
            CreateLine( Build(c.from, c.to) );
    }

public void CleanUpPaths()
{
    foreach (var g in pool)
    {
        if (g != null)
        {
            Destroy(g);
        }
    }
    pool.Clear();
}

    /* ───────── routing table ─────────────── */
    List<Vector3> Build(int a, int b)
    {
        Vector3 A = P(a), B = P(b), C = centre.localPosition;

        /* straight families */
        if (Is(a,b, 0,2, 1,3, 5,4)) return new(){A,B};

        /* corners: single quadratic Bézier */
        if (Is(a,b, 0,5, 5,2, 1,4, 4,3))
            return Quad(A, ctrl(A,B), B);

        /* U-pairs 0-1 / 2-3  → single smooth half-arc */
        if (Is(a,b, 0,1))
            return HalfArc(A, B, topRow:true);
        if (Is(a,b, 2,3))
            return HalfArc(A, B, topRow:false);

        /* S-pairs 0-3 / 2-1 – keep previous behaviour */
        if (Is(a,b, 0,3, 2,1))
        {
            var first  = Quad(A, ctrl(A,C), C);
            var second = Quad(C, ctrl(B,C), B);
            first.RemoveAt(first.Count-1); first.AddRange(second); return first;
        }

        /* curve + straight 0-4 / 2-4 / 1-5 / 5-3 */
        if (Is(a,b, 0,4, 2,4, 1,5, 5,3))
        {
            var arc = Quad(A, ctrl(A,C), C); arc.Add(B); return arc;
        }

        /* fallback straight */
        return new(){A,B};
    }

    /* ───────── geometry helpers ──────────── */
    Vector3 P(int id) => (id==6 ? centre : tile.snapPoints[id]).localPosition;

    bool Is(int a,int b,int p,int q)           => (a==p&&b==q)||(a==q&&b==p);
    bool Is(int a,int b, params int[] list)
    { for(int i=0;i<list.Length;i+=2) if(Is(a,b,list[i],list[i+1])) return true; return false; }

    /* control point for quarter Bézier */
    Vector3 ctrl(Vector3 start, Vector3 end) => new(start.x, start.y, end.z);

    /* quadratic Bézier sampler (¼-arc) */
    List<Vector3> Quad(Vector3 p0, Vector3 p1, Vector3 p2)
    {
        var pts = new List<Vector3>(segsPerBezier+1);
        for (int i=0;i<=segsPerBezier;i++)
        {
            float t = i / (float)segsPerBezier, u = 1-t;
            pts.Add(u*u*p0 + 2*u*t*p1 + t*t*p2);
        }
        return pts;
    }

    /* ── NEW: smooth half-circle for U-shape ── */List<Vector3> HalfArc(Vector3 A, Vector3 B, bool topRow)
{
    Vector3 centreArc = new(0f, A.y, topRow ? 0.5f : -0.5f);
    Vector3 r         = A - centreArc;                // radius vector
    int     samples   = segsPerBezier * 2;            // 180° / sample density

    var pts = new List<Vector3>(samples + 1) { A };
    for (int i = 1; i < samples; i++)
    {
        float t   = i / (float)samples;
        /* ↓↓↓ flip the sign choice ↓↓↓ */
        float ang = (topRow ?  Mathf.PI     // CCW → bends downwards
                            : -Mathf.PI)    // CW  → bends upwards
                     * t;
        float c = Mathf.Cos(ang), s = Mathf.Sin(ang);
        pts.Add(centreArc + new Vector3(c*r.x - s*r.z, 0,
                                        s*r.x + c*r.z));
    }
    pts.Add(B);
    return pts;
}

    /* ───────── LineRenderer ──────────────── */
    void CreateLine(List<Vector3> pts)
    {
        var go = new GameObject("Path", typeof(LineRenderer));
        go.transform.SetParent(transform, false);

        var lr = go.GetComponent<LineRenderer>();
        lr.material      = lineMat;
        lr.positionCount = pts.Count;
        lr.SetPositions  (pts.ToArray());
        lr.startWidth = lr.endWidth = width;
        lr.numCapVertices = 4;
        lr.useWorldSpace  = false;

        pool.Add(go);
    }
}
