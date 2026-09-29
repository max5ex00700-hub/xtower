using UnityEngine;
using UnityEngine.UI;

// Vertex colours keep the UI crisp at every phone size without new textures.
[RequireComponent(typeof(Graphic))]
public sealed class XTapJailGradient : BaseMeshEffect
{
    public Color top = new Color(1.22f, 1.18f, 1.12f, 1f);
    public Color bottom = new Color(.72f, .76f, .84f, 1f);

    public override void ModifyMesh(VertexHelper mesh)
    {
        if (!IsActive() || mesh.currentVertCount == 0) return;
        Rect rect = graphic.rectTransform.rect;
        UIVertex vertex = new UIVertex();
        for (int i = 0; i < mesh.currentVertCount; i++)
        {
            mesh.PopulateUIVertex(ref vertex, i);
            float t = Mathf.InverseLerp(rect.yMin, rect.yMax, vertex.position.y);
            vertex.color = (Color)vertex.color * Color.Lerp(bottom, top, t);
            mesh.SetUIVertex(vertex, i);
        }
    }
}
