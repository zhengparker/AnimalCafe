using UnityEngine;
using UnityEngine.UI;

namespace AnimalCafe.UI.P8R
{
    /// <summary>Transparent prohibition overlay with a one-pixel antialias fringe.
    /// 透明红圈斜线：沿用原生 UI mesh，缩放不依赖低分辨率贴图。</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class P8RInvalidRoleGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = GetPixelAdjustedRect();
            var size = Mathf.Min(rect.width, rect.height);
            if (size <= 0) return;
            var fringe = 1f / Mathf.Max(.01f, canvas != null ? canvas.scaleFactor : 1f);
            var halfStroke = size * .035f;
            var radius = size * .5f - fringe - halfStroke;
            if (radius <= halfStroke) return;
            const int segments = 96;
            for (var i = 0; i <= segments; i++)
            {
                var angle = 2f * Mathf.PI * i / segments;
                var normal = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                AddBand(mesh, rect.center + normal * radius, normal, halfStroke, fringe);
                if (i > 0) JoinBands(mesh, (i - 1) * 4, i * 4);
            }
            // Endpoints join the opaque ring, avoiding a doubled translucent cap.
            // 斜线端点连接实色圆环，避免半透明边缘叠加变深。
            var diagonal = new Vector2(1, -1).normalized;
            var cross = new Vector2(1, 1).normalized;
            var first = mesh.currentVertCount;
            AddBand(mesh, rect.center - diagonal * radius, cross, halfStroke, fringe);
            AddBand(mesh, rect.center + diagonal * radius, cross, halfStroke, fringe);
            JoinBands(mesh, first, first + 4);
        }

        private void AddBand(VertexHelper mesh, Vector2 center, Vector2 normal, float halfStroke, float fringe)
        {
            var opaque = (Color32)color;
            var clear = opaque;
            clear.a = 0;
            AddVertex(mesh, center - normal * (halfStroke + fringe), clear);
            AddVertex(mesh, center - normal * halfStroke, opaque);
            AddVertex(mesh, center + normal * halfStroke, opaque);
            AddVertex(mesh, center + normal * (halfStroke + fringe), clear);
        }

        private static void AddVertex(VertexHelper mesh, Vector2 point, Color32 tint)
        {
            var vertex = UIVertex.simpleVert;
            vertex.position = point;
            vertex.color = tint;
            mesh.AddVert(vertex);
        }

        private static void JoinBands(VertexHelper mesh, int first, int next)
        {
            for (var band = 0; band < 3; band++)
            {
                mesh.AddTriangle(first + band, next + band, next + band + 1);
                mesh.AddTriangle(first + band, next + band + 1, first + band + 1);
            }
        }
    }
}
