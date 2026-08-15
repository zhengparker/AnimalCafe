# Phase 5 Manual Review Readability and MT-001–005 Visual Fix

## Problem

`Phase5UiFoundation.unity` 已完成分区分页、真实 action label、横向状态文字和字体 Warning 修复，但 Studio Owner 在 MT-001～005 手动验收时发现三个视觉缺口：

- Button 仍是普通直角矩形，没有落实 Phase 5 设计合同中的柔和圆角；
- `Default` 与 `Pressed` 的差异太弱，而且两个静态样品都可以点击，无法说明状态含义；
- `Solid / Light Frost / Strong Frost` Panel 样品过小、缺少背景参照和当前状态说明，点击切换后肉眼看不出变化。

这些问题属于 Phase 5 UI foundation 与 validation scene 的视觉可验收性，不修改 gameplay 或 `MainCafe` 行为。

## Approved direction

采用 **方案 A：Figma 定义 + Unity 9-slice 圆角 Sprite**。

Figma 是视觉目标和 component state 的 Source of Truth；Unity 使用 Theme token、9-slice Sprite、Prefab、Material 与 C# 实现 runtime。不得把整张 Figma 页面作为图片导入 Unity。

## Button design

### Structure

- 三列标题固定为 `Default / Pressed Preview / Disabled`。
- 三行 role 固定为 `Primary / Secondary / Destructive`。
- `Default` 列是真实可操作 Button。
- 按住 `Default` 时显示真实 `Pressed`，松开后恢复 `Default`。
- `Pressed Preview` 是固定视觉样品，不响应普通点击。
- `Disabled` 不可点击，并保持可辨识的 disabled 外观。

### Visual tokens

- Corner radius：`12 px`。
- 最小 touch target：`48 × 48 px`，现有展示按钮保持更大的实际尺寸。
- Default：使用现有 semantic role color。
- Pressed：在 role color 基础上明显加深约 `25%`，并缩放至 `97%`；松开恢复 `100%`。
- Press animation：使用 unscaled time，目标持续时间保持现有合同 `0.08–0.12 s`。
- Disabled：使用现有 disabled token，不响应 input。
- 文字必须保持足够 contrast；Pressed 不得只依靠缩放表达。

## Panel design

### Presentation

- Panels 页面使用一个约 `560 × 360 px` 的主预览区域，而不是三个小型空白方块。
- 主预览区域后方放置简单的 warm wood / sage / cream 几何背景，作为透明度与 Frost 的参照。
- 同一时间只显示一种 Panel。
- Panel 内显示清晰标题：`Solid Panel / Light Frost Panel / Strong Frost Panel`。
- Panel 外显示当前状态：`Current: Solid / Light Frost / Strong Frost / Light Frost Fallback`。

### Variants

- Solid：不透明 cream surface，清晰边界。
- Light Frost：半透明浅色 tint、细边界与柔和 highlight；不要求实时 blur。
- Strong Frost：更强的背景分离效果；支持时使用现有 Strong material contract。
- Strong 不支持或已有 owner 时，明确显示 `Light Frost Fallback`，功能和控件位置不改变。
- 所有 Panel 使用与 Button 一致的圆润视觉语言；Panel radius 可大于 Button，但不得引入新的复杂 Shader 系统。

## Figma scope

只创建一个小型 Phase 5 component comparison board：

- A2 + P1 foundations 摘要；
- Button 3 roles × 3 states；
- Button live-press behavior annotation；
- Panel 3 variants + fallback；
- Unity token mapping 表。

不重做 Coffee Machine flow、不创建完整游戏 screen、不扩展 MT-006 以后内容。

## Expected Unity file scope

- `Assets/Editor/Phase5/Phase5UiAssetBuilder.cs`
  - 生成圆角 9-slice Sprite、更新 Theme/Prefab visual state，并保持 deterministic build。
- `Assets/Scripts/UI/Components/AnimalCafeButtonView.cs`
  - 加强真实 Pressed visual，同时保持 input 与 role contract。
- `Assets/Editor/Phase5/Phase5UiFoundationSceneSetup.cs`
  - 生成列标题、不可点击的 Pressed Preview 与大型 Panel comparison fixture。
- `Assets/Scripts/UI/Foundation/Phase5UiFoundationReviewController.cs`
  - 切换唯一可见 Panel，并更新可读状态文字。
- Phase 5 EditMode / PlayMode tests
  - 覆盖 Sprite/rounding binding、Pressed input lifecycle、Preview 非交互、Panel 唯一可见与真实 Button 切换。
- `Assets/Scenes/Validation/Phase5UiFoundation.unity`
  - 仅通过 Unity Editor builder 重建，不手工编辑 YAML。
- `outputs/phase5-manual-review/AnimalCafe_P5_Manual_Review.xlsx`
  - 根据 Studio Owner 结果更新 MT-001～005 状态和备注。

## MT-001–005 acceptance

- **MT-001:** Button 视觉一致、具有柔和圆角与清晰边界。
- **MT-002:** Default、Pressed Preview、Disabled 肉眼明显不同；真实按住 Default 时进入 Pressed，松开恢复。
- **MT-003:** 真实可操作 Button 保持至少 `48 × 48 px`，Mouse / Touch 点击正常。
- **MT-004:** Primary、Secondary、Destructive role 仍可直接辨识。
- **MT-005:** 点击 Panel controls 后，唯一主预览的标题、状态和材质外观同步改变；Frost fallback 可辨识。

## TDD and verification

1. RED：新增测试证明当前缺少圆角 Sprite、Pressed Preview 仍可交互、Pressed 差异不足、Panel 切换不可见。
2. GREEN：仅实现上述最小视觉与 fixture 改动。
3. Focused：MT-001～005 EditMode/PlayMode real-input tests。
4. Regression：Phase 5 cumulative EditMode、PlayMode、Build Settings 和 runtime assembly gates。
5. Manual：Studio Owner 在 Unity Editor 中重新执行 MT-001～005，自动测试不能替代视觉确认。

## Out of scope

- 不修改 MT-006～034 的视觉与功能合同。
- 不重做完整正式游戏 UI screen。
- 不引入通用 blur Shader framework 或新的 render pipeline。
- 不修改 Coffee Machine、Syrup、装修模式等 gameplay decisions。
- 不修改 `MainCafe` migration behavior。
- 不 commit、push、merge 或删除 worktree，除非 Studio Owner 另行授权。
