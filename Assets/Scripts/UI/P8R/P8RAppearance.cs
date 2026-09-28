using System;
using System.Collections.Generic;
using System.Linq;
using AnimalCafe.Content;
using AnimalCafe.Layout;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AnimalCafe.UI.P8R
{
    /// <summary>Instance-scoped presentation; no placement or global Theme rules.
    /// 只提供当前 UI 实例的图片和文案，不参与摆放规则或全局 Theme。</summary>
    public sealed class P8RAppearance : ScriptableObject
    {
        [Serializable] public sealed class SpriteEntry { public string key; public Sprite value; }
        [Serializable] private sealed class TextEntry { public string key; public string value; }
        [Serializable] private sealed class TextTable { public TextEntry[] entries; }
        [SerializeField] private SpriteEntry[] sprites = Array.Empty<SpriteEntry>();
        [SerializeField] private TextAsset english;
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private bool refinedB;
        private Dictionary<string, string> textCache;
        public TMP_FontAsset Font => font;
        public bool IsRefinedB => refinedB;
        public Sprite Thumbnail(string itemId, Sprite fallback)
        {
            var key = "TH_" + (itemId ?? string.Empty).Replace('-', '_').Replace('.', '_');
            foreach (var entry in sprites) if (entry.key == key) return entry.value;
            return fallback;
        }

        public string ItemName(string id, string fallback)
        {
            var key = "item." + id;
            var value = Text(key);
            return value == key ? fallback : value;
        }

        // Selected range/speed is a state, even if tapping it again is disabled.
        // 已选中的档位/范围保持 selected 外观，不能被 disabled 皮肤覆盖。
        public void Tab(Button button, string action, bool selected, bool locked = false, bool iconOnly = false, string iconAction = null)
        {
            if (button == null) return;
            Button(button, action, iconOnly: iconOnly, iconAction: iconAction);
            var key = locked ? "tab_unavailable" : selected ? "tab_selected" : "tab_idle";
            Paint(button.image, key);
            button.spriteState = new SpriteState { highlightedSprite = Sprite(key), selectedSprite = Sprite(key),
                pressedSprite = Sprite("tab_selected"), disabledSprite = Sprite(key) };
            foreach (var text in button.GetComponentsInChildren<TMP_Text>(true)) Typography(text, selected && !locked);
            // Measure after the final weight; Bold must not consume the icon/text gap.
            // 最终字重确定后重排，避免选中状态挤掉图标与文字的间距。
            if (!iconOnly) P8RButtonLayout.TextButton(button);
        }
        public static Color Cocoa => new Color(90f / 255f, 70f / 255f, 58f / 255f, 1f);

        /// <summary>State-driven emphasis, without changing font family or layout / 只更新字重，不改字号与排版。</summary>
        public void Typography(TMP_Text text, bool emphasized)
        {
            if (!refinedB || text == null) return;
            text.fontStyle = emphasized ? FontStyles.Bold : FontStyles.Normal;
            text.characterSpacing = 0;
            text.color = Cocoa;
        }

        /// <summary>Display confirmed setup; keep domain validation as the source of truth.
        /// 只展示已确认的设施状态，整体连通性仍由现有 report 判定。</summary>
        public string ChecklistSummary(LayoutReadinessReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            if (report.CanOpenForBusiness) return Text("checklist.complete");
            var count = (report.CashRegisters.ValidCount > 0 ? 1 : 0)
                + (report.CoffeeMachines.ValidCount > 0 ? 1 : 0)
                + (report.PickUpPoints.ValidCount > 0 ? 1 : 0);
            return Text("checklist.title") + " · " + count + "/3";
        }

        public string ChecklistDetails(LayoutReadinessReport report) => string.Join("\n", ChecklistRows(report));

        public string[] ChecklistRows(LayoutReadinessReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            var rows = new List<string>();
            AddChecklistRow(rows, report, LayoutStationType.CashRegister, report.CashRegisters);
            AddChecklistRow(rows, report, LayoutStationType.CoffeeMachine, report.CoffeeMachines);
            AddChecklistRow(rows, report, LayoutStationType.PickUpPoint, report.PickUpPoints);
            if (!report.CanOpenForBusiness && report.CashRegisters.ValidCount > 0
                && report.CoffeeMachines.ValidCount > 0 && report.PickUpPoints.ValidCount > 0)
                rows[2] += "\n" + Text("readiness.action.NoCompleteReachableServiceCombination");
            return rows.ToArray();
        }
        private void AddChecklistRow(List<string> rows, LayoutReadinessReport report,
            LayoutStationType type, LayoutReadinessSummary summary)
        {
            var ready = summary.ValidCount > 0;
            var missing = summary.TotalCount == 0;
            var state = Text(ready ? "checklist.state.ready" : missing ? "checklist.state.to_place" : "checklist.state.adjust");
            var row = FormatReadinessText(Text("readiness." + type), true, true) + " · " + state;
            if (ready && summary.InvalidCount > 0)
                row += "\n" + Text(summary.InvalidCount == 1 ? "checklist.extra" : "checklist.extra_many")
                    .Replace("{count}", summary.InvalidCount.ToString());
            // Missing rows stay compact; invalid rows retain their corrective hint.
            // 缺失行保持简短；无效设施继续显示修正提示。
            else if (!ready && !missing)
            {
                var failure = report.Failures.FirstOrDefault(item => item.FunctionType == type);
                var key = failure != null ? "readiness.action." + failure.Code : "checklist.adjust";
                if (failure?.Code == LayoutReadinessFailureCode.AnchorBlocked && failure.Role.HasValue)
                    key += "." + failure.Role.Value;
                row += "\n" + FormatReadinessText(Text(key), true, false);
            }
            rows.Add(row);
        }

        public string ReadinessSummary(LayoutReadinessReport report)
        {
            var failures = DistinctReadinessFailures(report);
            var severity = report.CanOpenForBusiness ? LayoutReadinessSeverity.Warning : LayoutReadinessSeverity.Blocking;
            var count = failures.Count(failure => failure.Severity == severity);
            if (report.CanOpenForBusiness && count == 0) return Text("readiness.summary.ready");
            var key = report.CanOpenForBusiness ? "readiness.summary.warning" : "readiness.summary.blocked";
            return Text(key + (count == 1 ? "_one" : "_many")).Replace("{count}", count.ToString());
        }

        public string ReadinessDetails(LayoutReadinessReport report, bool richText = false)
        {
            var entries = new List<string>();
            var hasSuggestionsHeading = false;
            foreach (var failure in DistinctReadinessFailures(report))
            {
                var sectionHeading = string.Empty;
                if (failure.Severity == LayoutReadinessSeverity.Warning && !hasSuggestionsHeading)
                {
                    sectionHeading = FormatReadinessText(Text("readiness.suggestions"), richText, true) + "\n";
                    hasSuggestionsHeading = true;
                }

                var subject = ReadinessSubject(report, failure);
                var actionKey = "readiness.action." + failure.Code;
                if (failure.Code == LayoutReadinessFailureCode.AnchorBlocked && failure.Role.HasValue)
                    actionKey += "." + failure.Role.Value;
                entries.Add(sectionHeading + FormatReadinessText(subject, richText, true) + "\n"
                    + FormatReadinessText(Text(actionKey), richText, false));
            }
            return string.Join("\n\n", entries);
        }

        public string ReadinessDiagnosticMessage(LayoutReadinessReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            var severity = report.CanOpenForBusiness ? LayoutReadinessSeverity.Warning : LayoutReadinessSeverity.Blocking;
            var primary = report.Failures.FirstOrDefault(f => f.Severity == severity) ?? report.Failures.FirstOrDefault();
            var summary = Text(!report.CanOpenForBusiness ? "readiness.blocked" : primary == null ? "readiness.ready" : "readiness.warning")
                + (primary == null ? string.Empty : "\n" + Text("readiness." + primary.Code));
            if (report.Failures.Count == 0) return summary;
            // Diagnostics retain every original record and raw cause, without UI deduplication.
            // 诊断保留全部原始记录、坐标和原因；精简仅用于玩家看到的文案。
            return summary + "\n" + string.Join("\n", report.Failures.Select(f =>
                Text(f.Severity == LayoutReadinessSeverity.Blocking ? "readiness.blocking" : "readiness.warning_label") + ": "
                + Text(f.FunctionType.HasValue ? "readiness." + f.FunctionType.Value : "readiness.layout")
                + (f.Role.HasValue ? " / " + Text("readiness." + f.Role.Value) : string.Empty)
                + (f.Position.HasValue ? " (" + f.Position.Value.X + ", " + f.Position.Value.Y + ")" : string.Empty)
                + ": " + Text("readiness." + f.Code)
                + (string.IsNullOrEmpty(f.Message) ? string.Empty : "\n" + f.Message)));
        }

        private static List<LayoutReadinessFailure> DistinctReadinessFailures(LayoutReadinessReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            // Match the complete issue identity; equal wording can describe different people or places.
            // 只去掉完全重复的记录，不因为文案相同而合并不同物件、角色或位置的问题。
            return report.Failures.GroupBy(f => new
                {
                    f.Severity, f.Code, f.FunctionType, f.InstanceId, f.SupportFurnitureInstanceId,
                    f.SurfaceSlotId, f.Role, f.Position, f.Message
                }).Select(group => group.First())
                .OrderByDescending(f => f.Severity).ThenBy(f => f.FunctionType)
                .ThenBy(f => f.InstanceId, StringComparer.Ordinal).ThenBy(f => f.Role)
                .ThenBy(f => f.Position?.X).ThenBy(f => f.Position?.Y).ThenBy(f => f.Code)
                .ThenBy(f => f.SupportFurnitureInstanceId, StringComparer.Ordinal)
                .ThenBy(f => f.SurfaceSlotId, StringComparer.Ordinal)
                .ThenBy(f => f.Message, StringComparer.Ordinal).ToList();
        }

        private string ReadinessSubject(LayoutReadinessReport report, LayoutReadinessFailure failure)
        {
            var subject = Text(failure.FunctionType.HasValue ? "readiness." + failure.FunctionType.Value
                : string.IsNullOrEmpty(failure.InstanceId) ? "readiness.layout" : "readiness.equipment");
            if (!string.IsNullOrEmpty(failure.InstanceId))
            {
                // Include healthy instances so removing one warning does not renumber the others.
                // 编号包括同类正常物件，修好一条问题后不会给剩余物件重新编号。
                var instanceIds = report.Stations.Where(station => station.FunctionType == failure.FunctionType)
                    .Select(station => station.InstanceId)
                    .Concat(report.Failures.Where(item => item.FunctionType == failure.FunctionType)
                        .Select(item => item.InstanceId))
                    .Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal)
                    .OrderBy(id => id, StringComparer.Ordinal).ToArray();
                if (instanceIds.Length > 1)
                    subject = Text("readiness.numbered_subject").Replace("{item}", subject)
                        .Replace("{number}", (Array.IndexOf(instanceIds, failure.InstanceId) + 1).ToString());
            }
            return failure.Role.HasValue
                ? Text("readiness.subject_role").Replace("{item}", subject)
                    .Replace("{role}", Text("readiness.side." + failure.Role.Value))
                : subject;
        }

        private static string FormatReadinessText(string value, bool richText, bool emphasized)
        {
            if (!richText) return value;
            var escaped = value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
            return emphasized ? "<b>" + escaped + "</b>" : escaped;
        }

        public Sprite Sprite(string key)
        {
            foreach (var entry in sprites) if (entry.key == key) return entry.value;
            throw new InvalidOperationException("Missing P8R sprite: " + key);
        }

        public string Text(string key)
        {
            if (textCache == null)
            {
                textCache = new Dictionary<string, string>(StringComparer.Ordinal);
                if (english != null)
                    foreach (var entry in JsonUtility.FromJson<TextTable>(english.text).entries)
                        textCache.Add(entry.key, entry.value);
            }
            return textCache.TryGetValue(key, out var value) ? value : key;
        }

        public string ItemName(FurnitureDefinitionAsset definition, string fallback)
        {
            if (definition == null) return fallback;
            if (definition.FunctionType == FurnitureFunctionType.CashRegister) return Text("item.cash_register");
            if (definition.FunctionType == FurnitureFunctionType.CoffeeMachine) return Text("item.coffee_machine");
            return definition.DefinitionId.StartsWith("furniture.counter", StringComparison.Ordinal)
                ? Text("item.counter").Replace("{width}", definition.FootprintWidth.ToString())
                    .Replace("{depth}", definition.FootprintDepth.ToString()) : fallback;
        }

        public void Paint(Image image, string spriteKey, bool sliced = true)
        {
            if (image == null) return;
            image.sprite = Sprite(spriteKey);
            image.color = Color.white;
            image.material = null;
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            // 64 source-pixel borders become 16 reference units; PPU stays 100.
            image.pixelsPerUnitMultiplier = 4f;
            image.preserveAspect = !sliced;
        }

        public void Button(Button button, string action, string role = "secondary", bool iconOnly = false, string iconAction = null)
        {
            if (button == null) return;
            Paint(button.image, "button_" + role + "_normal");
            button.transition = Selectable.Transition.SpriteSwap;
            button.colors = new ColorBlock { normalColor = Color.white, highlightedColor = Color.white,
                pressedColor = Color.white, selectedColor = Color.white, disabledColor = Color.white,
                colorMultiplier = 1, fadeDuration = 0 };
            button.spriteState = new SpriteState { highlightedSprite = Sprite("button_" + role + "_normal"),
                selectedSprite = Sprite("button_" + role + "_pressed"),
                pressedSprite = Sprite("button_" + role + "_pressed"), disabledSprite = Sprite("button_disabled") };
            // Stop a previous legacy ColorTint tween, not just its current frame's color.
            // 停止旧 ColorTint tween，防止下一帧继续污染 PNG 原色。
            button.image.CrossFadeColor(Color.white, 0f, true, true);
            var icon = button.transform.Find("Icon")?.GetComponent<Image>();
            if (icon != null)
            {
                Paint(icon, (iconAction ?? action) + (button.interactable ? "_cocoa" : "_muted"), false);
                icon.gameObject.SetActive(true);
                icon.raycastTarget = false;
            }
            foreach (var text in button.GetComponentsInChildren<TMP_Text>(true))
            {
                text.text = Text("action." + action);
                text.font = font;
                text.color = Cocoa;
                Typography(text, role == "primary");
                if (text.name == "Label") text.gameObject.SetActive(!iconOnly);
            }
            if (iconOnly) P8RButtonLayout.IconButton(button);
            else P8RButtonLayout.TextButton(button);
        }
    }
}
