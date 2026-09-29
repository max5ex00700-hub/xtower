using UnityEngine;
using UnityEngine.UI;

// Small vector ornaments and icons; no font glyph dependency on Android.
public sealed class XTapJailGraphic : MaskableGraphic
{
    public enum Symbol { Frame, Gate, Heart, Bag, Rune, Lock }
    public Symbol symbol;

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect rect = GetPixelAdjustedRect();
        if (rect.width <= 0f || rect.height <= 0f) return;
        if (symbol == Symbol.Frame)
        {
            float inset = 2f, cut = Mathf.Min(15f, rect.height * .1f);
            Vector2[] outline = {
                new Vector2(rect.xMin + inset + cut, rect.yMin + inset),
                new Vector2(rect.xMax - inset - cut, rect.yMin + inset),
                new Vector2(rect.xMax - inset, rect.yMin + inset + cut),
                new Vector2(rect.xMax - inset, rect.yMax - inset - cut),
                new Vector2(rect.xMax - inset - cut, rect.yMax - inset),
                new Vector2(rect.xMin + inset + cut, rect.yMax - inset),
                new Vector2(rect.xMin + inset, rect.yMax - inset - cut),
                new Vector2(rect.xMin + inset, rect.yMin + inset + cut)
            };
            for (int i = 0; i < outline.Length; i++)
                Line(mesh, outline[i], outline[(i + 1) % outline.Length], 1.6f);
            Line(mesh, new Vector2(rect.xMin + 24f, rect.yMax - 7f),
                new Vector2(Mathf.Min(rect.xMax - 24f, rect.xMin + 86f), rect.yMax - 7f), 2f);
            Line(mesh, new Vector2(Mathf.Max(rect.xMin + 24f, rect.xMax - 86f), rect.yMin + 7f),
                new Vector2(rect.xMax - 24f, rect.yMin + 7f), 2f);
            return;
        }

        float unit = Mathf.Min(rect.width, rect.height);
        Vector2 origin = rect.center - Vector2.one * unit * .5f;
        float stroke = Mathf.Max(1.8f, unit * .045f);
        if (symbol == Symbol.Gate)
        {
            Path(mesh, origin, unit, stroke, true, new[] {
                new Vector2(.5f,.97f), new Vector2(.83f,.74f), new Vector2(.83f,.18f),
                new Vector2(.5f,.04f), new Vector2(.17f,.18f), new Vector2(.17f,.74f) });
            Path(mesh, origin, unit, stroke * .7f, false, new[] {
                new Vector2(.29f,.26f), new Vector2(.29f,.66f), new Vector2(.5f,.82f),
                new Vector2(.71f,.66f), new Vector2(.71f,.26f) });
            Segment(mesh, origin, unit, stroke, .40f,.29f,.40f,.63f);
            Segment(mesh, origin, unit, stroke, .60f,.29f,.60f,.63f);
            Segment(mesh, origin, unit, stroke, .26f,.43f,.74f,.43f);
        }
        else if (symbol == Symbol.Heart)
        {
            Vector2 previous = Vector2.zero;
            for (int i = 0; i <= 40; i++)
            {
                float a = i / 40f * Mathf.PI * 2f;
                float x = 16f * Mathf.Pow(Mathf.Sin(a), 3f);
                float y = 13f * Mathf.Cos(a) - 5f * Mathf.Cos(2f*a) - 2f * Mathf.Cos(3f*a) - Mathf.Cos(4f*a);
                Vector2 point = origin + new Vector2(.5f + x / 38f, .55f + y / 38f) * unit;
                if (i > 0) Line(mesh, previous, point, stroke);
                previous = point;
            }
        }
        else if (symbol == Symbol.Bag)
        {
            Path(mesh, origin, unit, stroke, true, new[] {
                new Vector2(.18f,.14f), new Vector2(.82f,.14f), new Vector2(.76f,.71f), new Vector2(.24f,.71f) });
            Path(mesh, origin, unit, stroke, false, new[] {
                new Vector2(.35f,.65f), new Vector2(.35f,.85f), new Vector2(.65f,.85f), new Vector2(.65f,.65f) });
            Segment(mesh, origin, unit, stroke, .3f,.46f,.7f,.46f);
            Segment(mesh, origin, unit, stroke, .5f,.35f,.5f,.56f);
        }
        else if (symbol == Symbol.Rune)
        {
            Path(mesh, origin, unit, stroke, true, new[] {
                new Vector2(.5f,.95f), new Vector2(.85f,.5f), new Vector2(.5f,.05f), new Vector2(.15f,.5f) });
            Segment(mesh, origin, unit, stroke, .33f,.33f,.67f,.67f);
            Segment(mesh, origin, unit, stroke, .33f,.67f,.67f,.33f);
        }
        else
        {
            Path(mesh, origin, unit, stroke, true, new[] {
                new Vector2(.19f,.12f), new Vector2(.81f,.12f), new Vector2(.81f,.57f), new Vector2(.19f,.57f) });
            Path(mesh, origin, unit, stroke, false, new[] {
                new Vector2(.31f,.57f), new Vector2(.31f,.77f), new Vector2(.4f,.87f),
                new Vector2(.6f,.87f), new Vector2(.69f,.77f), new Vector2(.69f,.57f) });
            Segment(mesh, origin, unit, stroke, .5f,.26f,.5f,.43f);
        }
    }

    void Path(VertexHelper mesh, Vector2 origin, float unit, float stroke, bool closed, Vector2[] points)
    {
        for (int i = 1; i < points.Length; i++)
            Line(mesh, origin + points[i - 1] * unit, origin + points[i] * unit, stroke);
        if (closed) Line(mesh, origin + points[points.Length - 1] * unit, origin + points[0] * unit, stroke);
    }
    void Segment(VertexHelper mesh, Vector2 origin, float unit, float stroke, float x1, float y1, float x2, float y2)
    {
        Line(mesh, origin + new Vector2(x1, y1) * unit, origin + new Vector2(x2, y2) * unit, stroke);
    }
    void Line(VertexHelper mesh, Vector2 a, Vector2 b, float width)
    {
        Vector2 d = b - a;
        if (d.sqrMagnitude < .001f) return;
        Vector2 normal = new Vector2(-d.y, d.x).normalized * width * .5f;
        int start = mesh.currentVertCount;
        mesh.AddVert(a - normal, color, Vector2.zero);
        mesh.AddVert(a + normal, color, Vector2.zero);
        mesh.AddVert(b + normal, color, Vector2.zero);
        mesh.AddVert(b - normal, color, Vector2.zero);
        mesh.AddTriangle(start, start + 1, start + 2);
        mesh.AddTriangle(start, start + 2, start + 3);
    }
}
