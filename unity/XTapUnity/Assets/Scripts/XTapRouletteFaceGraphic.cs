using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class XTapRouletteFaceGraphic : MaskableGraphic
{
    const int SegmentCount = 11;
    const float InnerRadiusRatio = .22f;
    const float OuterRadiusRatio = .88f;

    static readonly Color Gold = new Color(.72f, .50f, .22f, 1f);

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect rect = rectTransform.rect;
        Vector2 center = rect.center;
        float radius = Mathf.Min(rect.width, rect.height) * .5f;
        float inner = radius * InnerRadiusRatio;
        float outer = radius * OuterRadiusRatio;
        float step = 360f / SegmentCount;

        // One complete disc, underneath the transparent ornamental rim and hub.
        for (int i = 0; i < 96; i++)
            AddTriangle(vh, center, center + Dir(i * Mathf.PI * 2f / 96f) * inner,
                center + Dir((i + 1) * Mathf.PI * 2f / 96f) * inner,
                new Color(.012f, .025f, .050f, 1f));

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

                // Soft radial illumination rather than a flat orange/blue overlay.
                for (int band = 0; band < 8; band++)
                {
                    float r0 = band / 8f;
                    float r1 = (band + 1) / 8f;
                    float radius0 = Mathf.Lerp(inner, outer, r0);
                    float radius1 = Mathf.Lerp(inner, outer, r1);
                    Color c0 = Color.Lerp(innerColor, outerColor, Mathf.Sin(r0 * Mathf.PI * .82f));
                    Color c1 = Color.Lerp(innerColor, outerColor, Mathf.Sin(r1 * Mathf.PI * .82f));
                    AddGradientQuad(vh, center + Dir(a0) * radius0, center + Dir(a1) * radius0,
                        center + Dir(a1) * radius1, center + Dir(a0) * radius1, c0, c1);
                }
            }
        }

        // Exact 11-way gold dividers.
        for (int segment = 0; segment < SegmentCount; segment++)
        {
            float a = (90f + step * .5f - segment * step) * Mathf.Deg2Rad;
            Vector2 dir = Dir(a);
            Vector2 normal = new Vector2(-dir.y, dir.x) * 1.35f;
            AddSolidQuad(
                vh,
                center + dir * inner - normal,
                center + dir * inner + normal,
                center + dir * outer + normal,
                center + dir * outer - normal,
                Gold
            );
        }

        AddRing(vh, center, inner, 2.5f, Gold);
        AddRing(vh, center, outer, 2.5f, Gold);
    }

    static Vector2 Dir(float radians)
    {
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }

    static void GetSegmentColors(int index, out Color innerColor, out Color outerColor)
    {
        if (index == 3)
        {
            innerColor = new Color(.065f, .022f, .10f, 1f);
            outerColor = new Color(.30f, .13f, .43f, 1f);
            return;
        }

        if (index == 9)
        {
            innerColor = new Color(.075f, .014f, .025f, 1f);
            outerColor = new Color(.36f, .075f, .10f, 1f);
            return;
        }

        if (index >= 4 && index <= 8)
        {
            innerColor = new Color(.075f, .036f, .012f, 1f);
            outerColor = new Color(.40f, .22f, .065f, 1f);
            return;
        }

        innerColor = new Color(.012f, .035f, .085f, 1f);
        outerColor = new Color(.045f, .23f, .40f, 1f);
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
        Color gold = new Color(.94f, .76f, .41f, 1f);
        Color blue = new Color(.08f, .30f, .54f, 1f);
        Color darkBlue = new Color(.01f, .10f, .28f, 1f);

        Vector2 gemCenter = center + Vector2.up * 16f;
        AddDiamond(vh, gemCenter, 19f, 27f, gold);
        AddDiamond(vh, gemCenter, 13f, 19f, blue);
        AddDiamond(vh, gemCenter + Vector2.up * 4f, 6f, 10f, new Color(.76f, .92f, 1f, 1f));

        // One stationary selector. The ornament and all values rotate beneath it.
        AddTriangle(
            vh,
            center + Vector2.down * 45f,
            center + new Vector2(-13f, -3f),
            center + new Vector2(13f, -3f),
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

[RequireComponent(typeof(CanvasRenderer))]
public sealed class XTapGachaShadeGraphic : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = rectTransform.rect;
        float[] heights = {0f, .25f, .60f, .82f, 1f};
        float[] alphas = {.82f, .55f, .06f, 0f, .32f};
        for (int band = 0; band < heights.Length - 1; band++)
        {
            int start = vh.currentVertCount;
            float bottom = Mathf.Lerp(rect.yMin, rect.yMax, heights[band]);
            float top = Mathf.Lerp(rect.yMin, rect.yMax, heights[band + 1]);
            AddVertex(vh, rect.xMin, bottom, alphas[band]);
            AddVertex(vh, rect.xMax, bottom, alphas[band]);
            AddVertex(vh, rect.xMax, top, alphas[band + 1]);
            AddVertex(vh, rect.xMin, top, alphas[band + 1]);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }

    static void AddVertex(VertexHelper vh, float x, float y, float alpha)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = new Vector2(x, y);
        vertex.color = new Color(0f, 0f, 0f, alpha);
        vh.AddVert(vertex);
    }
}
