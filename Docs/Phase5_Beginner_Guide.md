# AnimalCafe Phase 5 Beginner Guide — UI Architecture & Design System

> 状态：`Completed`。本文件同时保存原 manual-review Excel 和 intermediate reports 中的最终验收结论。

## 1. Phase 5 做了什么

Phase 5 建立全游戏共用的 UI foundation：`uGUI`、`TextMeshPro`、UI layers、Button、Panel、Modal、Bottom Sheet、Toast、Tooltip、Validation Message、Safe Area、Reduced Motion，以及 UI/Scene input boundary。

简单例子：玩家点击 Modal 外侧关闭窗口时，这次点击只能关闭 Modal，不能同时穿透到咖啡厅 Scene、选中家具或移动物体。

## 2. 主要规则

- 一个 `UI Root` 管理 HUD、Screen、Modal 和 Toast layers。
- Modal 阻止下层 UI 与 Scene input。
- 一次 pointer interaction 只能属于 UI 或 Scene。
- Reference layout 为 portrait `1080 × 1920`；landscape 必须可用且不能裁切关键 controls。
- Minimum touch target 为 `48 × 48` logical pixels。
- Strong Frost 同一时间只有一个 owner；额外请求回退到 Light Frost。
- Long label、CJK/Latin text、Safe Area 和 Reduced Motion 都保留明确 expansion points。

## 3. Test cases 与完成记录

Studio Owner 已完成并接受 `MT001–MT034`，结果为 `34 / 34 PASS`：

- `MT001–MT005`：Button roles/states、touch targets、Panel styles、Strong Frost fallback。
- `MT006–MT010`：navigation、Reduced Motion、Bottom Sheet、Modal stacking 和 dismissal。
- `MT011–MT015`：Back、UI/world pointer isolation、drag/outside-close、lifecycle。
- `MT016–MT020`：Toast、Tooltip、Validation feedback、Safe Area、localized text。
- `MT021–MT025`：portrait、small/tall portrait、landscape、Safe Area、long labels。
- `MT026–MT030`：CJK/Latin typography、motion、interrupted Modal lifecycle、MainCafe time controls。
- `MT031`：world selection/deselection 与 UI click isolation。
- `MT032`：CPU profiling 和 Strong Frost ownership；headless runner 无法提供真实 GPU/batches/overdraw，因此这些项目正确记录为 `N/A`。
- `MT033–MT034`：无 unexpected runtime error；reload 后无 permanent pause、stale blocker、duplicate EventSystem/UI Root。

Manual closeout 时 Phase 5 focused evidence 为 EditMode `119 / 119`、PlayMode `58 / 58` passed。PR #4 merge 后 fresh full regression 为 EditMode `690 / 690`、Editor PlayMode `121 / 121`、Windows standalone PlayMode `103 / 103` passed；Failed、Skipped、Inconclusive 均为 `0`。

## 4. 没有做什么

Phase 5 没有实现完整 feature pages、正式 localization、最终 mobile layout、正式 icons 或 tutorial。这些由后续 gameplay phases 和 Phase 47/50 负责。

## 5. Beginner glossary

| Term | 简单解释 |
|---|---|
| `UI Root` | 管理全部游戏 UI 的顶层对象。 |
| `Modal` | 打开时阻止玩家操作后方内容的窗口。 |
| `Input boundary` | 决定一次点击归 UI 还是游戏 Scene。 |
| `Safe Area` | 避开手机刘海、圆角和系统手势区域。 |
| `Reduced Motion` | 减少或立即完成动画，降低不适。 |
| `Regression` | 新修改意外破坏已经完成的功能。 |

## 6. 最终结论

Phase 5 的 approved design、implementation、test cases、manual acceptance、merge 和 merged-main regression 已全部完成。原 Excel 和 intermediate review Markdown 可以删除；本 Guide 与 Phase 5 Spec 是长期 documentation。
