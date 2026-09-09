# Phase 8 — Functional Furniture & Layout Readiness Design

> 状态：Approved Phase 8 Baseline
>
> 日期：2026-09-02
>
> Project：AnimalCafe
>
> Lifecycle：Approved Design Baseline；Task 10 / Phase 8 仍为 `In Progress`
>
> Source of Truth：`Docs/AnimalCafe_Project_Design.md`、`Docs/AnimalCafe_Development_Roadmap.md`

## 1. 给初学者的简单说明

Phase 1–7 让玩家可以装修咖啡厅。Phase 8 让游戏理解这些家具的用途，并判断当前布局是否已经具备未来营业所需的摆放条件。

例如，玩家把 Cash Register 放到 Counter 顶面后，不需要手动设置员工或顾客站位。系统会根据设备方向自动计算：

- 员工站在哪里操作；
- 顾客站在哪里点单；
- 这些位置有没有被墙、家具或 locked cell 挡住；
- 当前是否至少存在一组可用的 Cash Register、Coffee Machine 和 Pick-up Point。

Phase 8 的边界很明确：只提供 functional placement、automatic anchors 和 layout-readiness contracts。它不加入 cafe day loop、NPC movement、Order、Queue、NavMesh / pathfinding agents、economy 或 Save；画面显示“可以营业”只表示布局条件已满足，不表示咖啡厅已经会正式营业。

## 2. Goal

让经营系统通过 furniture capabilities、Surface Slot bindings 和自动 Interaction Anchors 使用玩家布局，而不是依赖固定 Scene object names，并通过 `LayoutReadinessReport` 判断咖啡厅是否具备最低营业条件。

## 3. Player-visible Result

Phase 8 完成后，玩家可以在现有 Decoration Mode 中：

- 像操作普通 furniture 一样 Preview、放置、移动、旋转、Confirm、Cancel 或 Store Cash Register 和 Coffee Machine；
- 但 Cash Register 和 Coffee Machine 只能放在兼容家具的空闲 `Surface Slot`，不能放在 Floor；
- 点击 Furniture Tab 内的 Pick-up Point 按钮，创建一个可移动、可 Confirm、Cancel 或 Store 的 Pick-up Point indicator；
- 创建多个 Cash Registers、Coffee Machines 和 Pick-up Points；
- 看见具体的 readiness feedback，例如缺少必要功能、anchor 被挡住或某个设备暂不可用。

## 4. Confirmed Design Decisions

### 4.1 Architecture

采用独立 `LayoutReadinessEvaluator` 方案：

```text
CafeLayout + FunctionalSurfaceLayout
        ↓
InteractionAnchorResolver
        ↓
GridReachabilityEvaluator
        ↓
LayoutReadinessEvaluator
        ↓
LayoutReadinessReport
```

- `CafeLayout` 继续负责 Floor furniture、rotation、occupancy 和 placement。
- `FunctionalSurfaceLayout` 负责 Surface Slot occupancy、mounted equipment 和 Pick-up Points。
- `InteractionAnchorResolver` 只计算 anchors，不负责角色移动。
- `GridReachabilityEvaluator` 只验证 Grid 连通性，不实现 Unity Navigation 或 NPC movement。
- `LayoutReadinessEvaluator` 汇总所有 station results 和整体营业条件。

### 4.2 Multiple Functional Points

系统不得假设每种功能只有一个实例。

- 每个 Cash Register、Coffee Machine 和 Pick-up Point 都有独立稳定 ID。
- 每个功能点按自己的 ID、binding、anchors 和 failure reasons 独立验证。
- `LayoutReadinessReport` 分别记录每种功能的 `TotalCount`、`ValidCount` 和 `InvalidCount`。
- 每种必要功能至少有一个有效实例，并且至少存在一组处于同一可达区域的 Cash Register、Coffee Machine 和 Pick-up Point，整体才允许营业。
- 其他无效功能点只标记为 unavailable 并产生 warning，不拖垮已经存在的有效服务组合。
- Phase 8 不永久绑定 Cash Register → Coffee Machine → Pick-up Point，也不决定角色如何选择或分流；这些属于 Phase 10–14。

## 5. Surface Slot Domain

### 5.1 Stable Address

一个 Surface Slot 使用以下组合定位：

```text
SupportFurnitureInstanceId + SurfaceSlotId
```

`SurfaceSlotAddress` 必须保持 value equality，且不得依赖 Scene object name。

### 5.2 Mounted Equipment

`SurfaceMountedInstance` 保存：

- stable mounted instance ID；
- equipment `DefinitionId`；
- `SurfaceSlotAddress`；
- equipment-local `FurnitureRotation`。

Cash Register 和 Coffee Machine 必须来自允许 `PlacementSurfaceType.FurnitureSurface` 的 definition。一个 Surface Slot 同一时间只能被一个 mounted equipment 或 Pick-up Point 占用。

2026-09-08 Studio Owner manual clarification：未确认 CR / CM ghost 与普通 furniture 使用相同悬浮高度，位于 Surface Slot 或地面上方，footprint 留在对应平面。地面位置仅用于 Preview 显示，必须红色 invalid 且不可 Confirm；松手后可重新抓起拖回桌面。Confirm 后设备位于正式 Slot，无悬浮；此调整不改变 Surface Slot domain 或 anchors。

### 5.3 Pick-up Point

`PickUpPointInstance` 保存：

- stable Pick-up Point ID；
- `SurfaceSlotAddress`。

Pick-up Point：

- 不是 `FurnitureDefinition`，也不进入独立 Catalogue row；
- 使用一个兼容的 `1 × 1 Surface Slot` footprint；
- 不提供 Rotate；
- Employee delivery anchor 和 Customer pick-up anchor 由系统自动选择；
- 可以存在多个；
- 不得与 Cash Register、Coffee Machine 或另一个 Pick-up Point 重叠。

2026-09-08 Studio Owner approved manual fix：indicator 使用细长、硬边立体倒四方棱锥，宽深约为原来一半，高度基本不变；独立灰白 opaque Lit 材质不随有效性变色，只有 `1 × 1` footprint 使用 valid / invalid 色。Preview 可在地面悬浮显示并松手重新抓起，地面始终 invalid、不可 Confirm；拖回空闲桌面可确认，Cancel 恢复来源。地面位置仅为 presentation，不新增 Floor placement domain。玩家 readiness 文本使用中文角色、站位坐标和原因，原始 instance / support / Slot IDs 只保留在 diagnostics，不拼入可见文本。

### 5.4 Support Furniture Changes

- 移动或旋转支撑家具时，mounted equipment 和 Pick-up Point 保持相同 `SurfaceSlotAddress` 并随支撑家具更新 world position。
- anchors 和 readiness 必须根据当前 layout 重新计算，不得继续使用旧结果。
- 支撑家具上存在 mounted equipment 或 Pick-up Point 时，禁止将该支撑家具 Store；反馈必须列出需要先移除的内容。
- 不采用自动删除、自动换 Slot 或产生 orphaned binding 的隐式行为。

## 6. Interaction Anchors

### 6.1 Cash Register

- 玩家不手动指定 anchors。
- 根据 Cash Register authored sides、equipment rotation、支撑家具 transform 和 Surface Slot 自动计算。
- 必须生成一个 Employee anchor 和一个相反方向的 Customer anchor。
- Queue 起始方向可以作为稳定结果输出，但 Phase 8 不生成 Queue。

### 6.2 Coffee Machine

- 玩家不手动指定 anchor。
- 根据 authored `+Z` Forward、equipment rotation、支撑家具 transform 和 Surface Slot 自动计算一个 Employee anchor。
- 不生成 Customer anchor。

### 6.3 Pick-up Point

- 玩家不旋转、不指定 anchors。
- resolver 先收集 North、East、South、West 四个相邻 Grid cells，再使用固定优先顺序自动选择 Employee delivery anchor 和 Customer pick-up anchor。
- 优先选择两个有效且互相相反的 cells；若没有 opposite pair，则选择两个不同的有效 cells；只有一个有效 cell 时两者共用该 cell。相同条件下按 North → East → South → West 决定结果。
- 两者可以共用同一 Grid cell；若没有有效相邻位置，该 Pick-up Point invalid，不能 Confirm。
- Phase 8 不决定未来 Pick-up Queue 的完整转弯和分流规则。

## 7. Grid Reachability

Phase 8 使用 deterministic Grid search，而不是 Unity NavMesh 或正式 NPC Navigation。

### 7.1 Walkability Inputs

- 已解锁的 Interior Grid cells；
- Floor furniture occupancy；
- Wall / boundary constraints；
- locked cells；
- Entrance / Exit 可达起点；
- 当前自动计算的 Interaction Anchor cells。

### 7.2 Individual Validity

每个功能点至少检查：

- definition / function type 正确；
- 支撑家具存在；
- Surface Slot 存在且兼容；
- Slot 未被重复占用；
- anchors 位于有效范围；
- anchors 未被 furniture、wall 或 locked cell 阻挡；
- anchors 位于需要的可达区域。

### 7.3 Overall Readiness

顾客侧至少需要存在可连通的：

```text
Entrance → valid Cash Register customer anchor → valid Pick-up customer anchor → Entrance / Exit
```

员工侧至少需要存在同一可达区域内的：

```text
valid Cash Register employee anchor
↔ valid Coffee Machine employee anchor
↔ valid Pick-up employee anchor
```

这只证明存在完整可用组合，不决定实际 runtime assignment。

## 8. Readiness Report

`LayoutReadinessReport` 必须：

- 提供 `CanOpenForBusiness`；
- 汇总全部 station results，而不是在第一个错误处停止；
- 区分阻止营业的 `Blocking` failure 和不影响有效组合的 `Warning`；
- 提供稳定 `FailureCode`；
- 提供 player-readable message；
- 在适用时包含 function type、functional instance ID、support furniture ID、Surface Slot ID 和 Grid position；
- 对 missing capability、blocked anchor、unreachable anchor、duplicate occupancy、missing support、missing slot 和 invalid service combination 给出具体原因；
- 不依赖 Scene object name 作为身份或业务规则。

Phase 8 只提供营业判断依据。正式营业系统尚不存在，因此不创建假的营业流程或 Order system。

## 9. Decoration Mode Interaction

### 9.1 Cash Register / Coffee Machine

操作流程与现有 furniture 保持一致：

```text
Catalogue selection
→ Scene Preview
→ Move / Rotate
→ Confirm / Cancel
→ existing item can Move / Rotate / Store
```

唯一核心区别是 placement surface：普通 furniture 使用 Floor；Cash Register 和 Coffee Machine 只使用兼容 Surface Slot。

### 9.2 Pick-up Point

```text
Pick-up Point button
→ create indicator Preview
→ move between compatible Surface Slots
→ Confirm / Cancel
→ existing indicator can Move / Store
```

- 每次点击 Pick-up Point 按钮创建一个新的 Preview；完成后再次点击可以创建第二个实例。
- 新建时 Cancel 取消创建；编辑已有点时 Cancel 返回原位。
- Store 删除已确认的 Pick-up Point。
- invalid footprint / binding 时禁用 Confirm。

### 9.3 Atomic Preview

- 只有 Confirm 修改正式 `FunctionalSurfaceLayout`。
- Cancel 必须恢复完整原状态。
- transaction 失败不得留下部分 occupancy、重复 binding、丢失设备或错误 readiness cache。

## 10. UI Design

Furniture Tab 保持现有 Phase 7 visual language、card layout、spacing、scroll 和 interaction，增加：

```text
Furniture
[existing furniture cards]

Cash Register
[cash register cards]

Coffee Machine
[coffee machine cards]

[Pick-up Point button]
```

- Cash Register 和 Coffee Machine 使用独立 Catalogue rows，为未来多个型号保留扩展位置。
- Pick-up Point 使用按钮，不使用独立 row。
- 不创建新的 UI architecture，也不启动全面 UI visual redesign。
- Readiness feedback 使用现有 feedback patterns，显示整体状态和具体问题；点击问题突出相关设备、Slot 或 Grid cell 的能力只在实现成本不扩大系统边界时纳入。

## 11. Pick-up Point Indicator

- 使用倒四方棱锥箭头，在目标 Surface Slot 上方浮动。
- 显示对应 `1 × 1` footprint 和 valid / invalid feedback。
- 只在 Decoration Mode 中可见。
- 退出 Decoration Mode 后全部隐藏；数据继续存在。
- 再次进入 Decoration Mode 后，根据当前 confirmed layout 重建 indicators。
- 最终视觉接受由 Studio Owner 在 Play Mode 手工判断。

Phase 8 validation Scene 另外提供 developer-facing anchor gizmos / debug feedback，用来核对自动计算的 Employee / Customer anchors、角色侧和 Grid cells。Debug 表现不进入正常经营画面，也不要求玩家手动编辑 anchors。

## 12. Error Handling 与 Recovery

- invalid Preview 不能 Confirm，但可以继续移动、Cancel 或选择其他 Slot。
- missing support / missing slot / invalid definition / duplicate occupancy 返回明确 failure，不 crash。
- 读取到 orphaned 或损坏 binding 时，report 标记 blocking failure；runtime 不猜测替代 Slot。
- 移动或旋转支撑家具后立即使旧 readiness 结果失效。
- 无效但结构完整的装修可以保留；当前 `CanOpenForBusiness` 为 false。
- 非必要的额外设备只有普通 anchor / reachability 失败时，才可在另一组完整有效组合存在时降为 warning；损坏 binding / definition / direction 数据始终 blocking，不能用额外有效设备掩盖。
- 缺少方向配置的已确认设备仍保留在 Stations、InvalidCount 与具体 failure 中，不从 report 消失。
- readiness 按每项 failure 关联设备、中文角色、站位坐标与原因；自身 IDs 只留在 diagnostics，不拼入玩家文本。长文本在顶部 SafeArea 面板内换行、滚动。
- 一次只允许一个 Preview；切换 Tab 不隐式 Cancel。CR / CM / Pick-up 的 Store 使用确认框，Cancel 保持 Preview，确认后才修改正式 layout。
- Pick-up 无可用相邻站位时，action bar 必须给出专用原因，同时保留上一条正式 readiness。

Editor 配置工具的安全边界：

- Scene Configure 与 Build Assets 分开。依赖缺失、目标 Scene dirty 或项目 asset dirty 时，先明确拒绝，不隐式保存或重建其他资源。
- 对已存在 Scene，先验证候选配置再保存；失败恢复原 Scene、加载状态、顺序与 active Scene。唯一 loaded Scene 使用临时空 Scene 完成回滚。
- 首次复制 validation Scene 之前也执行相同 preflight。反馈容器的 parent、sibling 和 SafeArea 修复必须落盘；重复正确配置保持 byte-stable。

## 13. Expected File Boundaries

### 13.1 New Runtime Domain Files

预计在 `Assets/Scripts/Layout/` 新增：

- `SurfaceSlotAddress.cs`
- `SurfaceMountedInstance.cs`
- `PickUpPointInstance.cs`
- `FunctionalSurfaceLayout.cs`
- `InteractionAnchor.cs`
- `InteractionAnchorResolver.cs`
- `GridReachabilityEvaluator.cs`
- `StationReadiness.cs`
- `LayoutReadinessFailure.cs`
- `LayoutReadinessReport.cs`
- `LayoutReadinessEvaluator.cs`

### 13.2 New Decoration Runtime Files

预计在 `Assets/Scripts/Decoration/` 新增：

- `FunctionalSurfacePlacementPreview.cs`
- `FunctionalSurfaceDecorationSession.cs`
- `SurfaceMountedSceneRegistry.cs`
- `SurfaceMountedPreviewView.cs`
- `PickUpPointIndicatorView.cs`

### 13.3 Existing Runtime Files Likely Modified

- `Assets/Scripts/Decoration/CafeLayoutRuntime.cs`
- `Assets/Scripts/Decoration/DecorationModeController.cs`
- `Assets/Scripts/Decoration/DecorationCatalogueAsset.cs`
- `Assets/Scripts/UI/Decoration/DecorationCatalogueModels.cs`
- `Assets/Scripts/UI/Decoration/DecorationCatalogueView.cs`
- `Assets/Scripts/UI/Decoration/DecorationCatalogueTileView.cs`
- `Assets/Scripts/UI/Decoration/DecorationActionBarView.cs`
- input hit-kind / routing files only where required to preserve current mouse and future touch ownership contracts。

### 13.4 Editor, Assets, Scene and Tests

- 新增 `Assets/Editor/Phase8/` 下的 asset builder、Scene setup 和 validator。
- 新增 Phase 8 UI / indicator assets and prefabs；沿用现有 presentation。
- 新增 `Assets/Tests/EditMode/Phase8/` 和 Phase 8 PlayMode / EditorSceneLoading tests。
- 更新 `Assets/Scenes/Validation/` 中的 Phase 8 validation Scene，并通过 idempotent setup 接入 `MainCafe`。

最终 exact file list、interfaces 和 task-by-task edits 由 approved implementation plan 锁定。

## 14. Explicitly Not Included

简单说，本 Phase 只回答“功能家具放得对不对、自动站位在哪里、这套布局以后能不能支持营业”，不负责让营业流程真的跑起来。

- NPC、Customer 或 Employee movement。
- Order domain、capacity、task assignment 或 service timing。
- 正式 Queue、queue turning 或多队伍分流。
- Unity NavMesh、正式 path following 或 avoidance。
- Coffee production、payment、inventory、pricing 或家具商店。
- 自动绑定某台 Register、Coffee Machine 和 Pick-up Point。
- 新的 Decoration system、全面 UI visual redesign 或 Phase 8R Polish。
- Save / Load 和 migration；这些属于后续 Phase。
- iOS device acceptance；当前 Windows mouse 优先，并保留 future touch compatibility。

## 15. Verification Boundary

- Task 开发使用 focused RED → minimal implementation → focused GREEN → direct regression。
- Phase 收尾集中运行完整 EditMode、PlayMode、real Scene / input integration regression。
- automated tests 与 Studio Owner Manual Tests 分开记录。
- automated PASS 不代替 UI、操作手感和 indicator 可读性的 Studio Owner acceptance。
- 2026-09-08 Owner 明确授权例外：M18 开发用 anchor debug 检查改由 Codex 在 real Scene 执行并记录技术证据；验收标准不变，不计为 Owner 人工 PASS，也不替代 M1–M17 的玩家体验验收。
- 具体 cases 由 `Docs/superpowers/specs/2026-09-02-phase-8-functional-furniture-layout-readiness-test-cases.md` 定义。

## 16. Lifecycle 与剩余 Gate

本 design 与配套 test cases 已获批，现作为 Phase 8 baseline；这里的 `Approved` 只表示设计合同已确认，不表示 Task 10 或 Phase 8 已完成。

当前门槛分开记录：

- Automated：本轮 review 修复的最新结果与 XML authority 见 `Docs/Phase8_Beginner_Guide.md` 第 6 节；旧 Task 10 final report 的数字是修复前历史快照，不能代替本轮验证。
- Engineering：最终独立 department decision 为 `PASS`。
- QA：最终独立 department decision 为 `PASS`；P8-M-018 Beginner Guide 可观察性 Minor 已修正。
- Production：final focused re-review 为 `PASS`（`0 Critical / 0 Important / 0 Minor`）。
- Studio Owner Manual / 授权代测（2026-09-08）：M1–M17 为 Owner 人工 `PASS`；M18 经明确授权由 Codex 专项技术代测 `PASS`，最终 PlayMode 15/15（含 4 个新增 M18 tests）、独立 QA Spec / Quality 均 PASS，不计为用户亲自执行。M6 incompatible Slot 子项仍未覆盖，最新记录见 Beginner Guide 第 6 节。

上方最终 department decisions 为 2026-09-04 baseline，不替代当前验收。M6 / 玩家提示 bounded 修复：EditMode 75/75、Core PlayMode 374/374、直接 Scene/Input 15/15 PASS；独立 Engineering / QA 推荐交回人工复测，随后用户已确认通过。确切 XML 与视觉参考见 Beginner Guide 第 6 节。M18 授权技术代测与独立复查已 PASS；旧 Touch 顺序验证问题与 M6 未覆盖子项仍开放，下一步确认这些问题的处理并完成 closeout 验证，不进入 Phase 8R。只有验收与开放验证问题均处理后，才能另行决定 Phase 8 是否为 `Completed`。历史已接受 Minor 的 register 不因此自动增加，Phase 8R scope 也不自动扩大。
