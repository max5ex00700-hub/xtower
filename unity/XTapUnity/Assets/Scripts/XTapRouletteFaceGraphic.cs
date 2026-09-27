using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class XTapRouletteFaceGraphic : MaskableGraphic
{
    const int SegmentCount = 11;
    const float InnerRadiusRatio = .34f;
    const float OuterRadiusRatio = .82f;

    static readonly Color Gold = new Color(1f, .67f, .16f, 1f);
    static readonly Color PointerGold = new Color(1f, .76f, .18f, 1f);

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect rect = rectTransform.rect;
        Vector2 center = rect.center;
        float radius = Mathf.Min(rect.width, rect.height) * .5f;
        float inner = radius * InnerRadiusRatio;
        float outer = radius * OuterRadiusRatio;
        float step = 360f / SegmentCount;

        for (int segment = 0; segment < SegmentCount; segment++)
        {
            Color innerColor;
            Color outerColor;
            GetSegmentColors(segment, out innerColor, out outerColor);

            float centerDeg = 90f - segment * step;
            float startDeg = centerDeg + step * .5f;
            float endDeg = centerDeg - step * .5f;

            const int slices = 10;
            for (int slice = 0; slice < slices; slice++)
            {
                float t0 = (float)slice / slices;
                float t1 = (float)(slice + 1) / slices;
                float a0 = Mathf.Lerp(startDeg, endDeg, t0) * Mathf.Deg2Rad;
                float a1 = Mathf.Lerp(startDeg, endDeg, t1) * Mathf.Deg2Rad;

                Vector2 i0 = center + Dir(a0) * inner;
                Vector2 i1 = center + Dir(a1) * inner;
                Vector2 o0 = center + Dir(a0) * outer;
                Vector2 o1 = center + Dir(a1) * outer;
                AddGradientQuad(vh, i0, i1, o1, o0, innerColor, outerColor);
            }
        }

        // Exact 11-way gold dividers.
        for (int segment = 0; segment < SegmentCount; segment++)
        {
            float a = (90f + step * .5f - segment * step) * Mathf.Deg2Rad;
            Vector2 dir = Dir(a);
            Vector2 normal = new Vector2(-dir.y, dir.x) * 2.8f;
            AddSolidQuad(
                vh,
                center + dir * inner - normal,
                center + dir * inner + normal,
                center + dir * outer + normal,
                center + dir * outer - normal,
                Gold
            );
        }

        AddRing(vh, center, inner, 4f, Gold);
        AddRing(vh, center, outer, 4f, Gold);

        // One outward-pointing yellow marker per sector, exactly like the approved reference.
        for (int segment = 0; segment < SegmentCount; segment++)
        {
            float a = (90f - segment * step) * Mathf.Deg2Rad;
            Vector2 dir = Dir(a);
            Vector2 normal = new Vector2(-dir.y, dir.x);

            Vector2 tip = center + dir * (radius * .775f);
            Vector2 baseCenter = center + dir * (radius * .710f);
            AddTriangle(
                vh,
                tip,
                baseCenter + normal * 11f,
                baseCenter - normal * 11f,
                PointerGold
            );
        }
    }

    static Vector2 Dir(float radians)
    {
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }

    static void GetSegmentColors(int index, out Color innerColor, out Color outerColor)
    {
        if (index == 3)
        {
            innerColor = new Color(.24f, .035f, .36f, 1f);
            outerColor = new Color(.57f, .12f, .78f, 1f);
            return;
        }

        if (index == 9)
        {
            innerColor = new Color(.42f, .01f, .02f, 1f);
            outerColor = new Color(.92f, .055f, .035f, 1f);
            return;
        }

        if (index >= 4 && index <= 8)
        {
            innerColor = new Color(.55f, .16f, .005f, 1f);
            outerColor = new Color(.99f, .46f, .02f, 1f);
            return;
        }

        innerColor = new Color(.005f, .12f, .34f, 1f);
        outerColor = new Color(.015f, .37f, .94f, 1f);
    }

    static void AddGradientQuad(
        VertexHelper vh,
        Vector2 a,
        Vector2 b,
        Vector2 c,
        Vector2 d,
        Color innerColor,
        Color outerColor)
    {
        int start = vh.currentVertCount;
        AddVertex(vh, a, innerColor);
        AddVertex(vh, b, innerColor);
        AddVertex(vh, c, outerColor);
        AddVertex(vh, d, outerColor);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }

    static void AddSolidQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
    {
        int start = vh.currentVertCount;
        AddVertex(vh, a, color);
        AddVertex(vh, b, color);
        AddVertex(vh, c, color);
        AddVertex(vh, d, color);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }

    static void AddTriangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color color)
    {
        int start = vh.currentVertCount;
        AddVertex(vh, a, color);
        AddVertex(vh, b, color);
        AddVertex(vh, c, color);
        vh.AddTriangle(start, start + 1, start + 2);
    }

    static void AddRing(VertexHelper vh, Vector2 center, float radius, float thickness, Color color)
    {
        const int steps = 96;
        float half = thickness * .5f;

        for (int i = 0; i < steps; i++)
        {
            float a0 = i * Mathf.PI * 2f / steps;
            float a1 = (i + 1) * Mathf.PI * 2f / steps;
            Vector2 d0 = Dir(a0);
            Vector2 d1 = Dir(a1);

            AddSolidQuad(
                vh,
                center + d0 * (radius - half),
                center + d1 * (radius - half),
                center + d1 * (radius + half),
                center + d0 * (radius + half),
                color
            );
        }
    }

    static void AddVertex(VertexHelper vh, Vector2 position, Color color)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = color;
        vertex.uv0 = Vector2.zero;
        vh.AddVert(vertex);
    }
}

[RequireComponent(typeof(CanvasRenderer))]
public sealed class XTapFixedPointerGraphic : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect rect = rectTransform.rect;
        Vector2 center = rect.center;
        Color gold = new Color(1f, .66f, .12f, 1f);
        Color blue = new Color(.02f, .42f, 1f, 1f);
        Color darkBlue = new Color(.01f, .10f, .28f, 1f);

        Vector2 gemCenter = center + Vector2.up * 19f;
        AddDiamond(vh, gemCenter, 32f, 42f, gold);
        AddDiamond(vh, gemCenter, 20f, 28f, blue);
        AddDiamond(vh, gemCenter + Vector2.up * 4f, 9f, 13f, new Color(.48f, .86f, 1f, 1f));

        // Downward selector spear. Its tip ends above the wheel with a visible air gap.
        AddTriangle(
            vh,
            center + Vector2.down * 51f,
            center + new Vector2(-13f, -5f),
            center + new Vector2(13f, -5f),
            gold
        );
        AddTriangle(
            vh,
            center + Vector2.down * 42f,
            center + new Vector2(-7f, -7f),
            center + new Vector2(7f, -7f),
            darkBlue
        );
    }

    static void AddDiamond(VertexHelper vh, Vector2 center, float halfWidth, float halfHeight, Color color)
    {
        int start = vh.currentVertCount;
        AddVertex(vh, center + Vector2.up * halfHeight, color);
        AddVertex(vh, center + Vector2.right * halfWidth, color);
        AddVertex(vh, center + Vector2.down * halfHeight, color);
        AddVertex(vh, center + Vector2.left * halfWidth, color);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }

    static void AddTriangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color color)
    {
        int start = vh.currentVertCount;
        AddVertex(vh, a, color);
        AddVertex(vh, b, color);
        AddVertex(vh, c, color);
        vh.AddTriangle(start, start + 1, start + 2);
    }

    static void AddVertex(VertexHelper vh, Vector2 position, Color color)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = color;
        vertex.uv0 = Vector2.zero;
        vh.AddVert(vertex);
    }
}
