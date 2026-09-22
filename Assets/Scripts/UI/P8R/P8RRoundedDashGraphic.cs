using UnityEngine;
using UnityEngine.UI;

namespace AnimalCafe.UI.P8R
{
    /// <summary>
    /// Draws one continuously-spaced dashed rounded rectangle with a small AA fringe.
    /// 以同一条闭合路径绘制圆角虚线框；只在 Graphic 被标脏时重建 mesh。
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class P8RRoundedDashGraphic : MaskableGraphic
    {
        [SerializeField, Min(.5f)] private float strokeWidth = 4f;
        [SerializeField, Min(0f)] private float cornerRadius = 12f;
        [SerializeField, Min(.5f)] private float dashLength = 16f;
        [SerializeField, Min(.5f)] private float gapLength = 11f;
        [SerializeField, Min(0f)] private float inset = 4f;
        [SerializeField, Min(0f)] private float antiAliasWidth = 1f;

        private const float CurveStep = 2f;

        public void Configure(Color lineColor, float width = 4f, float radius = 12f,
            float dash = 16f, float gap = 11f, float frameInset = 4f, float aaWidth = 1f)
        {
            color = lineColor;
            strokeWidth = Mathf.Max(.5f, width);
            cornerRadius = Mathf.Max(0f, radius);
            dashLength = Mathf.Max(.5f, dash);
            gapLength = Mathf.Max(.5f, gap);
            inset = Mathf.Max(0f, frameInset);
            antiAliasWidth = Mathf.Max(0f, aaWidth);
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            var rect = GetPixelAdjustedRect();
            var halfStroke = strokeWidth * .5f;
            var fringe = antiAliasWidth;
            var halfWidth = rect.width * .5f - inset - halfStroke - fringe;
            var halfHeight = rect.height * .5f - inset - halfStroke - fringe;
            if (halfWidth <= 0f || halfHeight <= 0f) return;

            var radius = Mathf.Min(cornerRadius, halfWidth, halfHeight);
            var perimeter = 4f * (halfWidth + halfHeight - 2f * radius) + 2f * Mathf.PI * radius;
            var nominalCycle = dashLength + gapLength;
            var dashCount = Mathf.Max(4, Mathf.RoundToInt(perimeter / nominalCycle));
            var cycle = perimeter / dashCount;
            var visibleDash = Mathf.Min(dashLength, cycle - .5f);
            var actualGap = cycle - visibleDash;
            var capFringe = Mathf.Min(fringe, actualGap * .25f);

            // Put the closed-loop seam in the middle of a normal gap. Every dash then uses
            // the same cycle around straights and corners, with no special four-side joins.
            for (var index = 0; index < dashCount; index++)
            {
                var start = actualGap * .5f + index * cycle;
                AddDash(vertices, start, start + visibleDash, capFringe,
                    perimeter, halfWidth, halfHeight, radius, halfStroke, fringe, rect.center);
            }
        }

        private void AddDash(VertexHelper vertices, float start, float end, float capFringe,
            float perimeter, float halfWidth, float halfHeight, float radius, float halfStroke, float fringe,
            Vector2 center)
        {
            var length = end - start;
            var steps = Mathf.Max(1, Mathf.CeilToInt(length / CurveStep));
            var previous = AddRing(vertices, Sample(start - capFringe, perimeter, halfWidth, halfHeight, radius),
                halfStroke, fringe, 0, center);
            for (var step = 0; step <= steps; step++)
            {
                var distance = Mathf.Lerp(start, end, step / (float)steps);
                var current = AddRing(vertices, Sample(distance, perimeter, halfWidth, halfHeight, radius),
                    halfStroke, fringe, 255, center);
                ConnectRings(vertices, previous, current);
                previous = current;
            }
            var transparentEnd = AddRing(vertices, Sample(end + capFringe, perimeter, halfWidth, halfHeight, radius),
                halfStroke, fringe, 0, center);
            ConnectRings(vertices, previous, transparentEnd);
        }

        private int AddRing(VertexHelper vertices, PathSample sample, float halfStroke, float fringe, byte coreAlpha,
            Vector2 center)
        {
            var first = vertices.currentVertCount;
            var baseColor = (Color32)color;
            var faded = baseColor;
            faded.a = coreAlpha == 0 ? (byte)0 : (byte)Mathf.RoundToInt(baseColor.a * .5f);
            var transparent = baseColor;
            transparent.a = 0;
            var core = baseColor;
            core.a = coreAlpha == 0 ? (byte)0 : baseColor.a;
            var distances = new[]
            {
                halfStroke + fringe,
                halfStroke + fringe * .5f,
                halfStroke,
                -halfStroke,
                -halfStroke - fringe * .5f,
                -halfStroke - fringe
            };
            var colors = new[] { transparent, faded, core, core, faded, transparent };
            for (var i = 0; i < distances.Length; i++)
            {
                var vertex = UIVertex.simpleVert;
                vertex.position = center + sample.Position + sample.Normal * distances[i];
                vertex.color = colors[i];
                vertices.AddVert(vertex);
            }
            return first;
        }

        private static void ConnectRings(VertexHelper vertices, int previous, int current)
        {
            for (var band = 0; band < 5; band++)
            {
                vertices.AddTriangle(previous + band, current + band, current + band + 1);
                vertices.AddTriangle(previous + band, current + band + 1, previous + band + 1);
            }
        }

        private static PathSample Sample(float distance, float perimeter,
            float halfWidth, float halfHeight, float radius)
        {
            var d = Mathf.Repeat(distance, perimeter);
            var horizontalHalf = halfWidth - radius;
            var vertical = 2f * (halfHeight - radius);
            var horizontal = 2f * horizontalHalf;
            var arc = Mathf.PI * radius * .5f;

            if (d <= horizontalHalf)
                return new PathSample(new Vector2(d, halfHeight), Vector2.up);
            d -= horizontalHalf;
            if (d <= arc) return Arc(new Vector2(halfWidth - radius, halfHeight - radius), radius, Mathf.PI * .5f - d / radius);
            d -= arc;
            if (d <= vertical)
                return new PathSample(new Vector2(halfWidth, halfHeight - radius - d), Vector2.right);
            d -= vertical;
            if (d <= arc) return Arc(new Vector2(halfWidth - radius, -halfHeight + radius), radius, -d / radius);
            d -= arc;
            if (d <= horizontal)
                return new PathSample(new Vector2(halfWidth - radius - d, -halfHeight), Vector2.down);
            d -= horizontal;
            if (d <= arc) return Arc(new Vector2(-halfWidth + radius, -halfHeight + radius), radius, -Mathf.PI * .5f - d / radius);
            d -= arc;
            if (d <= vertical)
                return new PathSample(new Vector2(-halfWidth, -halfHeight + radius + d), Vector2.left);
            d -= vertical;
            if (d <= arc) return Arc(new Vector2(-halfWidth + radius, halfHeight - radius), radius, Mathf.PI - d / radius);
            d -= arc;
            return new PathSample(new Vector2(-halfWidth + radius + d, halfHeight), Vector2.up);
        }

        private static PathSample Arc(Vector2 center, float radius, float angle)
        {
            var normal = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            return new PathSample(center + normal * radius, normal);
        }

        private readonly struct PathSample
        {
            public PathSample(Vector2 position, Vector2 normal)
            {
                Position = position;
                Normal = normal;
            }

            public Vector2 Position { get; }
            public Vector2 Normal { get; }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            strokeWidth = Mathf.Max(.5f, strokeWidth);
            cornerRadius = Mathf.Max(0f, cornerRadius);
            dashLength = Mathf.Max(.5f, dashLength);
            gapLength = Mathf.Max(.5f, gapLength);
            inset = Mathf.Max(0f, inset);
            antiAliasWidth = Mathf.Max(0f, antiAliasWidth);
            SetVerticesDirty();
        }
#endif
    }
}
