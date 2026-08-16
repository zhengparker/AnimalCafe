# Phase 5 Soft Elevation Containers Design

## Goal

把已确认的 Figma `B — Soft Elevation` 第一阶段落地到 Unity，使 Button、Solid Panel、Bottom Sheet 和 Critical Modal 具有明确纵深，同时修复 MT-009/010 的可用性问题。

## Scope

### Included

- Button 保留现有颜色、48×48 touch target、Pressed 25% 加深与 97% scale，并增加不接收 raycast 的暖棕 soft shadow。
- Solid Panel 增加 12–20px 圆角视觉、cream highlight 与 soft shadow；不改变 Light/Strong Frost 行为。
- Bottom Sheet 只覆盖下半屏，具有 dim scrim、top radius、drag handle、标题、内容、Cancel 和 Confirm。
- Critical Modal 具有 55% dim scrim、强 shadow、标题、说明、Cancel 和 Destructive Confirm。
- 第二层 Modal 可关闭；关闭后第一层恢复可操作，不残留 blocker。

### Excluded

- 真实 blur shader、MT-006、MT-007。
- MainCafe 正式业务内容。
- 新增美术图片、icon 包或第三方 package。

## Interaction Contracts

- Shadow、highlight、装饰 divider 与 drag handle 的 `raycastTarget` 必须为 `false`。
- Bottom Sheet outside 和 Back 均关闭；Cancel 也关闭；Confirm 由 validation fixture 记录动作后关闭。
- Critical Modal outside 不关闭；下层 controls 不响应；Cancel 关闭；Destructive Confirm 关闭并记录动作。
- 第二层 Modal 关闭后第一层仍保持 top modal，并可继续 Cancel。

## Regression Boundaries

- MT-001–005 自动契约必须保持 GREEN；人工执行一次 spot-check。
- Button role colors、pressed darkening、pressed scale、touch target 不变。
- Frost Panel 的尺寸、切换与 fallback 不在本轮改变。
- Phase5 cumulative EditMode/PlayMode 必须全绿。

## Manual Acceptance

- MT-009：Sheet 明确从底部浮起，世界仍可见；outside、Cancel、Back 都可关闭并可重复打开。
- MT-010：Modal 内容明确；outside/下层无响应；Cancel 可恢复；第二层 Modal 不会锁死输入。
