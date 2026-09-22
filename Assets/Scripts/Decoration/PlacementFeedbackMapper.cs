using System;
using System.Collections.Generic;
using System.Linq;
using AnimalCafe.Layout;

namespace AnimalCafe.Decoration
{
    public enum PlacementFeedbackKey
    {
        None,
        Occupied,
        OutsideUnlockedArea,
        Locked,
        Blocked,
        EntranceClearance,
        UnsupportedSurface,
        MissingInstance,
        WallOverlap,
        WallOutOfBounds,
        WallCrossCorner,
        WallSurfaceMissing,
        SelectWallTarget,
        SelectFloorGridTarget,
        NoValidInteractionAnchor
    }

    public static class PlacementFeedbackMapper
    {
        public const string ValidEditingPosition = "位置有效，可以确认";
        public const string FinishEditingFirst = "请先确认或取消当前编辑，再选择其他物件或模式";
        public const string ReturnToEditing = "返回编辑";
        public const string ChooseSurfaceStyle = "请选择样式或继续涂抹，再确认修改";
        public const string SurfaceChangesReady = "已预览修改，可以确认";

        public static string GetEditingMessage(string subject, string reason) =>
            $"正在编辑：{subject} · 尚未确认\n{reason}";

        public static string GetPlayerMessage(WallPlacementResult result) => result.FailureReason switch
        {
            WallPlacementFailureReason.None => ValidEditingPosition,
            WallPlacementFailureReason.Overlap => "这个墙面位置已经被占用",
            WallPlacementFailureReason.OutOfBounds => "这个位置超出墙面范围",
            WallPlacementFailureReason.CrossCorner => "墙饰不能跨越墙角",
            WallPlacementFailureReason.SurfaceMissing => "请将墙饰移到可用墙面",
            WallPlacementFailureReason.SurfaceMismatch => "这个墙饰不能放在该墙面",
            _ => "目前无法完成这个墙饰操作"
        };

        public static PlacementFeedbackKey Map(PlacementResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            switch (result.FailureReason)
            {
                case PlacementFailureReason.None:
                    return PlacementFeedbackKey.None;
                case PlacementFailureReason.Overlap:
                    return PlacementFeedbackKey.Occupied;
                case PlacementFailureReason.OutOfUnlockedRegion:
                case PlacementFailureReason.OutOfLayoutBounds:
                    return PlacementFeedbackKey.OutsideUnlockedArea;
                case PlacementFailureReason.LockedCell:
                    return PlacementFeedbackKey.Locked;
                case PlacementFailureReason.Blocked:
                    return PlacementFeedbackKey.Blocked;
                case PlacementFailureReason.ReservedEntranceClearance:
                    return PlacementFeedbackKey.EntranceClearance;
                case PlacementFailureReason.UnsupportedPlacementSurface:
                    return PlacementFeedbackKey.UnsupportedSurface;
                case PlacementFailureReason.InstanceNotFound:
                case PlacementFailureReason.InstanceAlreadyPlaced:
                    return PlacementFeedbackKey.MissingInstance;
                default:
                    return PlacementFeedbackKey.None;
            }
        }

        public static PlacementFeedbackKey Map(WallPlacementResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            switch (result.FailureReason)
            {
                case WallPlacementFailureReason.None:
                    return PlacementFeedbackKey.None;
                case WallPlacementFailureReason.Overlap:
                    return PlacementFeedbackKey.WallOverlap;
                case WallPlacementFailureReason.OutOfBounds:
                    return PlacementFeedbackKey.WallOutOfBounds;
                case WallPlacementFailureReason.CrossCorner:
                    return PlacementFeedbackKey.WallCrossCorner;
                case WallPlacementFailureReason.SurfaceMismatch:
                case WallPlacementFailureReason.SurfaceMissing:
                    return PlacementFeedbackKey.WallSurfaceMissing;
                default:
                    return PlacementFeedbackKey.None;
            }
        }

        public static PlacementFeedbackKey Map(FunctionalSurfacePlacementResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            return result.FailureReason switch
            {
                FunctionalSurfacePlacementFailureReason.None => PlacementFeedbackKey.None,
                FunctionalSurfacePlacementFailureReason.SlotOccupied => PlacementFeedbackKey.Occupied,
                FunctionalSurfacePlacementFailureReason.UnsupportedPlacementSurface => PlacementFeedbackKey.UnsupportedSurface,
                FunctionalSurfacePlacementFailureReason.MissingSupportFurniture => PlacementFeedbackKey.MissingInstance,
                FunctionalSurfacePlacementFailureReason.MissingSurfaceSlot => PlacementFeedbackKey.UnsupportedSurface,
                FunctionalSurfacePlacementFailureReason.InstanceAlreadyPlaced => PlacementFeedbackKey.MissingInstance,
                FunctionalSurfacePlacementFailureReason.InstanceNotFound => PlacementFeedbackKey.MissingInstance,
                FunctionalSurfacePlacementFailureReason.NoValidInteractionAnchor => PlacementFeedbackKey.NoValidInteractionAnchor,
                _ => PlacementFeedbackKey.Blocked
            };
        }

        public static string GetPlayerMessage(
            FunctionalSurfacePlacementResult result,
            IEnumerable<string> diagnosticIds = null)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            var message = result.FailureReason switch
            {
                FunctionalSurfacePlacementFailureReason.None => "可以确认摆放",
                FunctionalSurfacePlacementFailureReason.MissingDefinition => "找不到这个设备定义",
                FunctionalSurfacePlacementFailureReason.UnsupportedFunction => "这个设备功能暂不支持",
                FunctionalSurfacePlacementFailureReason.UnsupportedPlacementSurface => "这个物件不能放在这里",
                FunctionalSurfacePlacementFailureReason.MissingSupportFurniture => "找不到承托它的家具",
                FunctionalSurfacePlacementFailureReason.MissingSurfaceSlot => "这个家具没有兼容的摆放位",
                FunctionalSurfacePlacementFailureReason.SlotOccupied => "这个摆放位已经被占用",
                FunctionalSurfacePlacementFailureReason.InstanceAlreadyPlaced => "这个物件已经摆放过了",
                FunctionalSurfacePlacementFailureReason.InstanceNotFound => "找不到这个物件",
                FunctionalSurfacePlacementFailureReason.InvalidRotation => "这个旋转方向不可用",
                FunctionalSurfacePlacementFailureReason.InvalidInstance => "这个物件资料无效",
                FunctionalSurfacePlacementFailureReason.InvalidSurfaceSlotAddress => "请将物件移到柜台上的摆放位",
                FunctionalSurfacePlacementFailureReason.UnsupportedAction => "这个物件不支持此操作",
                FunctionalSurfacePlacementFailureReason.NoValidInteractionAnchor => "取餐点周围没有可用的互动位置",
                _ => "目前无法完成这个摆放操作"
            };
            return message;
        }

        public static string GetPlayerMessage(
            PlacementResult result,
            IEnumerable<string> diagnosticIds = null)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            var message = result.FailureReason switch
            {
                PlacementFailureReason.None => "可以确认摆放",
                PlacementFailureReason.Overlap => "这个位置已经被占用",
                PlacementFailureReason.OutOfUnlockedRegion => "这个位置还没有解锁",
                PlacementFailureReason.OutOfLayoutBounds => "这个位置超出可用区域",
                PlacementFailureReason.LockedCell => "这个格子还没有解锁",
                PlacementFailureReason.Blocked => "请先移除这个家具上的物件",
                PlacementFailureReason.ReservedEntranceClearance => "这里需要保留入口通道",
                PlacementFailureReason.UnsupportedPlacementSurface => "这个物件不能放在这里",
                PlacementFailureReason.InstanceNotFound => "找不到这个家具",
                PlacementFailureReason.InstanceAlreadyPlaced => "这个家具已经摆放过了",
                _ => "目前无法完成这个摆放操作"
            };
            return message;
        }

        public static string GetPlayerMessage(
            LayoutReadinessFailureCode code,
            params string[] diagnosticIds)
        {
            var message = code switch
            {
                LayoutReadinessFailureCode.MissingCashRegister => "还需要至少一个收银机",
                LayoutReadinessFailureCode.MissingCoffeeMachine => "还需要至少一台咖啡机",
                LayoutReadinessFailureCode.MissingPickUpPoint => "还需要至少一个取餐点",
                LayoutReadinessFailureCode.MissingSupportFurniture => "营业设备缺少承托家具",
                LayoutReadinessFailureCode.MissingSurfaceSlot => "营业设备缺少有效摆放位",
                LayoutReadinessFailureCode.DuplicateSurfaceOccupancy => "同一个摆放位有重复物件",
                LayoutReadinessFailureCode.AnchorOutOfBounds => "互动位置超出可用区域",
                LayoutReadinessFailureCode.AnchorBlocked => "互动位置被阻挡",
                LayoutReadinessFailureCode.AnchorUnreachable => "互动位置无法到达",
                LayoutReadinessFailureCode.NoCompleteReachableServiceCombination => "还没有完整且可到达的营业动线",
                LayoutReadinessFailureCode.MissingFunctionalDirection => "营业设备缺少有效方向配置",
                LayoutReadinessFailureCode.InvalidFunctionalDefinition => "营业设备定义资料无效",
                _ => "营业检查未通过"
            };
            return message;
        }

        public static string GetPlayerMessage(LayoutReadinessReport report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            var preferredSeverity = report.CanOpenForBusiness
                ? LayoutReadinessSeverity.Warning
                : LayoutReadinessSeverity.Blocking;
            var failure = report.Failures.FirstOrDefault(item =>
                item.Severity == preferredSeverity);
            var summary = (report.CanOpenForBusiness ? "已确认布局：已就绪\n" : "已确认布局：还需调整\n") + (report.CanOpenForBusiness
                ? (failure == null
                    ? "布局已准备好，可以营业"
                    : $"可以营业，但{GetPlayerMessage(failure.Code)}")
                : (failure == null
                    ? "暂时不能营业"
                    : $"暂时不能营业：{GetPlayerMessage(failure.Code)}"));
            if (report.Failures.Count == 0 || (report.Failures.Count == 1 && failure?.InstanceId == null))
                return summary;

            // Keep every cause beside its own station, role and cell; raw IDs stay in diagnostics.
            // 每项原因保留对应设备、角色及格子；原始 IDs 只保留在诊断数据中。
            return summary + "\n" + string.Join("\n", report.Failures.Select(FormatReadinessFailure));
        }

        public static string GetReadinessSummary(LayoutReadinessReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            var severity = report.CanOpenForBusiness
                ? LayoutReadinessSeverity.Warning : LayoutReadinessSeverity.Blocking;
            var primary = report.Failures.FirstOrDefault(failure => failure.Severity == severity)
                ?? report.Failures.FirstOrDefault();
            if (report.CanOpenForBusiness)
                return primary == null ? "已确认布局：可以营业"
                    : $"已确认布局：可以营业，但{GetPlayerMessage(primary.Code)}";
            return primary == null ? "已确认布局：暂时不能营业"
                : $"已确认布局：暂时不能营业：{GetPlayerMessage(primary.Code)}";
        }

        public static string GetReadinessDetails(LayoutReadinessReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            if (report.Failures.Count == 0) return string.Empty;
            var only = report.Failures[0];
            // A single room-level cause is already complete in the summary.
            // 单一整体原因已在摘要说清，不生成重复的详情按钮。
            if (report.Failures.Count == 1 && only.FunctionType == null
                && only.Role == null && !only.Position.HasValue && only.InstanceId == null)
                return string.Empty;
            return string.Join("\n", report.Failures.Select(failure =>
                (failure.Severity == LayoutReadinessSeverity.Blocking ? "阻挡：" : "提醒：")
                + FormatReadinessFailure(failure)));
        }

        private static string FormatReadinessFailure(LayoutReadinessFailure failure)
        {
            var type = failure.FunctionType switch
            {
                LayoutStationType.CashRegister => "收银机",
                LayoutStationType.CoffeeMachine => "咖啡机",
                LayoutStationType.PickUpPoint => "取餐点",
                _ => "布局"
            };
            var role = failure.Role switch
            {
                InteractionRole.Employee => " / 员工",
                InteractionRole.Customer => " / 客人",
                _ => string.Empty
            };
            var cell = failure.Position.HasValue
                ? $" ({failure.Position.Value.X}, {failure.Position.Value.Y})" : string.Empty;
            return $"{type}{role}{cell}：{GetPlayerMessage(failure.Code)}";
        }
    }
}
