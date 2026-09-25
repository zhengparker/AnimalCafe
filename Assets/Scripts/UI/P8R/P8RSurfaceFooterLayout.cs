using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace AnimalCafe.UI.P8R
{
    /// <summary>One measured reservation shared by the sheet, range row and action row.
    /// 三个 View 使用同一份实际行高；窄屏换行，不缩小文字或触控目标。</summary>
    public sealed class P8RSurfaceFooterLayout
    {
        public float Height { get; private set; }
        public float Width { get; private set; }
        public float RowHeight { get; private set; }
        public Vector2[] Centers { get; private set; }
        public float[] Widths { get; private set; }
        public bool CompactUtilityIcons { get; private set; }

        public static P8RSurfaceFooterLayout Measure(Component owner, float availableWidth,
            P8RAppearance appearance, TMP_Text measurementLabel, bool floor, bool includeActions = true)
        {
            var metrics = P8RMobileMetrics.For(owner);
            // Floor uses 12-unit copy and adjacent touch roots; visible faces supply the small gap.
            // 地板缩小文字和留白，点击范围仍至少48；极小边界保护避免浮点误差造成重叠。
            var gap = metrics.Units(floor ? 1f / 64f : 4f);
            var actions = floor ? (includeActions
                ? new[] { "whole_room", "single_grid", "undo", "rotate", "apply_all", "cancel", "confirm" }
                : new[] { "whole_room", "single_grid" })
                : includeActions ? new[] { "cancel", "confirm" } : new string[0];
            var result = new P8RSurfaceFooterLayout { Width = availableWidth, RowHeight = metrics.Units(48),
                Centers = new Vector2[actions.Length], Widths = new float[actions.Length] };
            var oldFont = measurementLabel != null ? measurementLabel.font : null;
            var oldSize = measurementLabel != null ? measurementLabel.fontSize : 0;
            var oldStyle = measurementLabel != null ? measurementLabel.fontStyle : FontStyles.Normal;
            if (measurementLabel != null)
            {
                measurementLabel.font = appearance.Font;
                measurementLabel.fontSize = metrics.Units(12);
                measurementLabel.fontStyle = FontStyles.Bold;
            }
            for (var i = 0; i < actions.Length; i++)
            {
                // Both surface modes display Apply, never the transient generic Confirm copy.
                // 两种表面模式都按最终 Apply 文案测量，避免预留看不见的 Confirm 空白。
                var copy = appearance.Text("action." + (actions[i] == "confirm" ? "apply" : actions[i]));
                var textWidth = measurementLabel != null ? measurementLabel.GetPreferredValues(copy).x : metrics.Units(copy.Length * 9);
                // Floor range: 20 icon + 4 gap + 6 padding per side. Every surface action uses 6 per side.
                // Floor范围保留图文宽度；Floor/Wall操作按钮统一使用每侧6单位留白。
                var padding = floor && i < 2 ? 36 : 12;
                result.Widths[i] = Mathf.Max(metrics.Units(48), textWidth + metrics.Units(padding));
            }
            if (floor)
                result.Widths[0] = result.Widths[1] = Mathf.Max(result.Widths[0], result.Widths[1]);
            if (measurementLabel != null)
            {
                measurementLabel.font = oldFont; measurementLabel.fontSize = oldSize; measurementLabel.fontStyle = oldStyle;
            }
            var rows = new List<List<int>>();
            if (floor && includeActions
                && result.Widths.Sum() + gap * (actions.Length - 1) > availableWidth)
            {
                var compact = (float[])result.Widths.Clone();
                for (var i = 2; i <= 4; i++) compact[i] = metrics.Units(48);
                var oneStripFits = compact.Sum() + gap * (actions.Length - 1) <= availableWidth;
                var twoRowsFit = compact[0] + compact[1] + gap <= availableWidth
                    && compact.Skip(2).Sum() + gap * (actions.Length - 3) <= availableWidth;
                if (oneStripFits || twoRowsFit)
                {
                    // Height-constrained portrait phones need the same existing icon-only
                    // utility treatment as landscape; named ranges and final actions keep text.
                    // 矮竖屏沿用既有utility icon方案；范围与最终操作仍保留文字。
                    result.Widths = compact;
                    result.CompactUtilityIcons = true;
                }
            }
            if (floor && includeActions && result.Widths.Sum() + gap * (actions.Length - 1) > availableWidth)
            {
                if (result.Widths[0] + result.Widths[1] + gap <= availableWidth) rows.Add(new List<int> { 0, 1 });
                else { rows.Add(new List<int> { 0 }); rows.Add(new List<int> { 1 }); }
            }
            var start = rows.Count == 0 ? 0 : 2;
            var row = new List<int>(); var used = 0f;
            for (var i = start; i < actions.Length; i++)
            {
                if (row.Count > 0 && used + gap + result.Widths[i] > availableWidth)
                { rows.Add(row); row = new List<int>(); used = 0; }
                used += (row.Count > 0 ? gap : 0) + result.Widths[i]; row.Add(i);
            }
            if (row.Count > 0) rows.Add(row);
            result.Height = rows.Count * result.RowHeight + Mathf.Max(0, rows.Count - 1) * gap;
            for (var r = 0; r < rows.Count; r++)
            {
                var width = rows[r].Sum(index => result.Widths[index]) + gap * (rows[r].Count - 1);
                var x = -width * .5f;
                foreach (var index in rows[r])
                {
                    result.Centers[index] = new Vector2(x + result.Widths[index] * .5f,
                        result.Height - result.RowHeight * .5f - r * (result.RowHeight + gap));
                    x += result.Widths[index] + gap;
                }
            }
            return result;
        }
    }
}
