using System;
using System.Collections.Generic;
using AnimalCafe.UI.Feedback;
using AnimalCafe.CafeLoop;
using UnityEngine;
using UnityEngine.UI;

namespace AnimalCafe.BusinessDay
{
    public sealed class BusinessDayStatusView : MonoBehaviour
    {
        [SerializeField] private BusinessDayController owner;
        [SerializeField] private Text status;
        [SerializeField] private RectTransform topControls;
        private readonly Vector3[] controlCorners = new Vector3[4];
        private ValidationMessageView readiness;
        public string CurrentText => status == null ? "" : status.text;
        private BusinessDayController subscribed;
        public void Bind(BusinessDayController value)
        { Unbind(); owner = value; EnableRuntimeLayout(); Subscribe(); Refresh(owner == null ? null : owner.Snapshot); }
        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            EnableRuntimeLayout();
            Subscribe(); Refresh(owner == null ? null : owner.Snapshot);
            Canvas.preWillRenderCanvases -= AlignBelowControls; Canvas.preWillRenderCanvases += AlignBelowControls;
        }
        private void OnDisable() { Unbind(); Canvas.preWillRenderCanvases -= AlignBelowControls; }
        private void OnDestroy() { Unbind(); Canvas.preWillRenderCanvases -= AlignBelowControls; }
        private void Subscribe()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || owner == null || subscribed == owner) return;
            subscribed = owner; subscribed.SnapshotChanged += Refresh;
        }
        private void Unbind()
        { if (subscribed != null) subscribed.SnapshotChanged -= Refresh; subscribed = null; }
        private void Refresh(BusinessDaySnapshot value)
        {
            // 只消费不可变snapshot；HUD不推进时钟、admission或营业周期。
            if (status != null) status.text = owner!=null && !owner.IsValidationMode
                ?Format(value,false,owner.ServicePresentation):Format(value,owner!=null && owner.IsValidationMode);
        }
        private void EnableRuntimeLayout()
        {
            // Editor保留稳定preview几何；动态高度只在Play启用，停用View不破坏独立layout。
            if (!Application.isPlaying || status == null || status.transform.parent == null) return;
            // 从既有controls的UI根缓存提示；View本体属于业务root，不做全Scene搜索。
            // Cache within the existing UI root, never search globally each frame.
            readiness = topControls == null ? null : topControls.root.GetComponentInChildren<ValidationMessageView>(true);
            var panel = status.transform.parent;
            var layout = panel.GetComponent<VerticalLayoutGroup>();
            var fit = panel.GetComponent<ContentSizeFitter>();
            if (layout != null) layout.enabled = true;
            if (fit != null) fit.enabled = true;
        }
        private void AlignBelowControls()
        {
            if (!Application.isPlaying || status == null || topControls == null || !topControls.gameObject.activeInHierarchy) return;
            var panel = status.transform.parent as RectTransform;
            if (panel == null || !(panel.parent is RectTransform safe)) return;
            // Canvas缩放策略不同：先转屏幕坐标，再转P15 SafeArea，保留8单位间距。
            // Read rendered geometry only; never advance business state.
            var bottom = BottomInSafeArea(topControls, safe);
            // OnDisable并不隐藏P8R的Graphic；仍可见时继续避让，Clear/失活后立即还原。
            if (readiness != null && readiness.IsVisible && readiness.gameObject.activeInHierarchy)
                bottom = Mathf.Min(bottom, BottomInSafeArea((RectTransform)readiness.transform, safe));
            var y = bottom - safe.rect.yMax - 8;
            if (!Mathf.Approximately(panel.anchoredPosition.y, y))
                panel.anchoredPosition = new Vector2(panel.anchoredPosition.x, y);
        }
        private float BottomInSafeArea(RectTransform source, RectTransform safe)
        {
            // Decor会失活整个P14/P15 HUD；业务root上的View仍收到Canvas回调。
            // Include inactive ancestors to retain geometry while the shared HUD is hidden.
            var sourceCanvas = source.GetComponentInParent<Canvas>(true).rootCanvas;
            var targetCanvas = safe.GetComponentInParent<Canvas>(true).rootCanvas;
            var sourceCamera = sourceCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : sourceCanvas.worldCamera;
            var targetCamera = targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : targetCanvas.worldCamera;
            source.GetWorldCorners(controlCorners);
            var bottom = float.PositiveInfinity;
            foreach (var corner in controlCorners)
            {
                var screen = RectTransformUtility.WorldToScreenPoint(sourceCamera, corner);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(safe, screen, targetCamera, out var point))
                    bottom = Mathf.Min(bottom, point.y);
            }
            return bottom;
        }
        public static string Format(BusinessDaySnapshot value,bool validationMode,CafeServicePresentation presentation)
        {
            var text=Format(value,validationMode);
            if(validationMode || value==null || value.Phase==BusinessDayPhase.Rest)return text;
            var activity=new List<string>();
            if(presentation.IsPaused)activity.Add("已暂停");
            switch(presentation.Kind)
            {
                case CafeServicePresentationKind.Blocked:activity.Add("服务受阻，请检查通路与营业条件");break;
                case CafeServicePresentationKind.Waiting:activity.Add("等待通路腾空");break;
                case CafeServicePresentationKind.Planning:activity.Add("通路规划待完成");break;
            }
            if(!presentation.IsAvailable)activity.Add("等待营业条件恢复");
            return activity.Count==0?text:text+"\n"+string.Join(" · ",activity);
        }

        public static string Format(BusinessDaySnapshot value, bool validationMode)
        {
            if (!validationMode) return FormatPlayer(value);
            var lines = new List<string> { "Validation | Open 60s / Rest 15s" };
            if (value == null) { lines.Add("Waiting for business day / 等待周期接线"); return string.Join("\n", lines); }
            var seconds = Math.Ceiling(value.RemainingSeconds);
            var clock = validationMode
                ? $"{Math.Floor(seconds / 60):00}:{seconds % 60:00}"
                : $"{Math.Floor(seconds / 3600):00}h {Math.Floor(seconds % 3600 / 60):00}m";
            lines.Add($"Day {value.DayNumber} | {value.Phase}" + (value.Phase == BusinessDayPhase.Closing ? "" : " " + clock));
            if (!string.IsNullOrEmpty(value.DrainReason)) lines.Add(value.DrainReason);
            lines.Add($"LayoutInterrupted: {value.LayoutInterruptionCount}");
            if (value.LastClosedDay != null)
                lines.Add($"Last Day {value.LastClosedDay.DayNumber}: {value.LastClosedDay.EndKind} ({value.LastClosedDay.LayoutInterruptionCount})");
            // 周期Open不代表服务ready；实际暂停/堵路原因仍由P14状态区显示。
            lines.Add("Cycle phase / 服务状态见下方");
            return string.Join("\n", lines);
        }
        private static string FormatPlayer(BusinessDaySnapshot value)
        {
            if (value == null) return "营业信息准备中 / Preparing";
            var seconds = Math.Ceiling(value.RemainingSeconds);
            var clock = $"{Math.Floor(seconds / 3600):00}h {Math.Floor(seconds % 3600 / 60):00}m";
            var phase = value.Phase == BusinessDayPhase.Open ? "营业时段 Open" :
                value.Phase == BusinessDayPhase.Closing ? "收尾中 Closing" : "休息中 Rest";
            var lines = new List<string> { $"Day {value.DayNumber} | {phase}" +
                (value.Phase == BusinessDayPhase.Closing ? "" : " · 剩余 " + clock) };
            if (value.Phase == BusinessDayPhase.Closing) lines.Add(PlayerClosingReason(value.DrainReason));
            // Scheduled Open is a clock phase, never a claim that stations are ready.
            // 营业时段不等于实际可接待；原有readiness提示继续展示真实条件。
            lines.Add("接待以营业条件提示为准");
            if (value.LastClosedDay != null && value.LastClosedDay.EndKind == BusinessDayEndKind.LayoutInterrupted)
                lines.Add("上轮营业因装修调整中断");
            return string.Join("\n", lines);
        }
        private static string PlayerClosingReason(string reason)
        {
            // Preserve raw reasons in the snapshot/Validation; translate only player presentation.
            // 只转换玩家文案，snapshot与Validation保留完整失败事实。
            reason = reason ?? "";
            if (reason.Contains("Cafe owner unavailable")) return "营业状态暂不可用，请等待恢复";
            if (reason.Contains("Layout lifecycle unavailable")) return "等待装修或通路恢复";
            if (reason.Contains("Service configuration unavailable")) return "服务设施暂不可用，请检查营业条件";
            if (reason.Contains("Capacity ledger unavailable")) return "接待容量暂不可用，请等待恢复";
            if (reason.Contains("Logical delivery pending")) return "等待饮品领取";
            if (reason.Contains("Visible cups pending")) return "等待取餐台清空";
            if (reason.Contains("Live customer visits")) return "等待顾客离店；若停滞请检查通路";
            if (reason.Contains("Queue assignments pending")) return "等待队伍疏散";
            if (reason.Contains("Capacity reservations pending")) return "等待接待名额释放";
            if (reason.Contains("Pending business work")) return "等待当前服务完成或恢复";
            if (reason.Contains("Employee stations unavailable") || reason.Contains("Employee or binding unavailable"))
                return "员工或工作站暂不可用，请检查营业条件";
            if (reason.Contains("员工尚未返岗") || reason.Contains("Work station arrival pending")) return "等待员工回到工作站";
            if (reason.IndexOf("Blocked", StringComparison.OrdinalIgnoreCase) >= 0 || reason.Contains("受阻")) return "通路受阻，请检查店内通路";
            if (reason == "等待服务排空") return "等待店内服务结束";
            return "服务尚未完成，请检查店内状态";
        }
    }
}
