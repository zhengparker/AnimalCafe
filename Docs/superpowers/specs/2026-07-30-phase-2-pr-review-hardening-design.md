# Phase 2 PR Review Hardening Design

> 状态：Approved
>
> 类型：P0 regression hardening + P2 safety hardening
>
> 目标 branch：`codex/phase-2`

## 1. 这次修复是干什么的

这次处理两个彼此独立的问题。

### 问题 A：点击 UI 时不应该同时点击到游戏世界

可以把 UI button 想成盖在游戏世界上方的一张纸。

玩家点击 `Pause` 时，点击应该停在 button 上，不能穿过这张纸，又选中 button 后面的家具，或者清除当前 selection。

### 问题 B：错误的超大家具不能让游戏耗尽内存

正常家具可能占 `1 × 1`、`2 × 3` 或更大的少量 Grid cells。

如果错误 data 把家具写成：

```text
int.MaxValue × 1
```

旧代码会尝试逐格建立一个巨大 List，可能在返回 placement failure 之前先耗尽内存。

本修复要求 Furniture Definition 在进入 placement system 前拒绝超过 `1024 cells` 的 footprint。

## 2. 已批准决定

### 2.1 Branch 与 merge

- 两项修复都只加入 `codex/phase-2`。
- 不直接修改 `main`。
- 用户批准 P2 merge 后，两项修复一起进入 `main`。
- 不自动 commit、push、merge、resolve GitHub thread 或删除 branch。

### 2.2 UI pointer boundary

`SceneInteractionController.Update()` 收到 `TapReleased` 时：

1. 先确认当前 pointer 是否位于 UGUI 上；
2. 如果位于 UI 上，不调用 `TrySelectAt()`；
3. 不改变当前 world selection；
4. 不进行 Scene physics raycast；
5. 如果没有 `EventSystem` 或 pointer 不在 UI 上，保持原有 world selection 行为。

使用 Unity `EventSystem.current.IsPointerOverGameObject()` 作为当前 Windows mouse boundary。

公开的 `TrySelectAt(Vector2)` contract 保持不变。明确调用它的 code 和 tests 仍然执行 world selection；UI blocking 只属于 runtime input routing。

### 2.3 Furniture footprint safety limit

- 单件 `FurnitureDefinition` 的 footprint area 最大为 `1024 cells`。
- Area 使用 `long` 计算：

```text
(long)Width × Height
```

- `32 × 32 = 1024`：允许。
- `1 × 1024 = 1024`：允许。
- `1 × 1025`：拒绝。
- `int.MaxValue × 1`：立即拒绝。
- 超过上限属于错误 Definition data，构造 `FurnitureDefinition` 时抛出 `ArgumentOutOfRangeException`。
- 上限只属于 Furniture Definition，不限制通用 `GridSize` 或 unlocked Layout regions。

建议在 `FurnitureDefinition` 暴露一个有名字的 constant：

```csharp
public const int MaxFootprintCellCount = 1024;
```

未来 Editor validation 可以复用同一个 contract，不应在多个地方复制 magic number。

## 3. Root Cause

### 3.1 UI click-through

`SceneInteractionController.Update()` 当前只检查 `TapReleased`，然后直接调用 `TrySelectAt()`。

它没有询问 `EventSystem` 当前 pointer 是否在 UGUI 上，因此同一次 mouse release 同时被 UI 和 world-selection pipeline 消费。

### 3.2 Unbounded footprint allocation

`GridSize` 只要求 Width、Height 至少为 `1`，没有最大值。

`FurnitureDefinition` 也只检查正数。

`CafeLayout.TryGetFootprintCells()` 在检查 unlocked region 之前，用 nested loops 把整个 footprint 加入 `List<GridPosition>`。错误的巨大 Definition 因此可以造成极长 loop 或 memory exhaustion。

## 4. 修改范围

### Production

```text
Assets/Scripts/Interaction/SceneInteractionController.cs
Assets/Scripts/Layout/FurnitureDefinition.cs
```

### Tests

```text
Assets/Tests/PlayMode/Phase0PlayModeTests.cs
Assets/Tests/EditMode/FurnitureDefinitionCatalogTests.cs
```

如实际 Unity UI integration test 需要 test-only fixture，只能放在现有 `Phase0PlayModeTests.cs`，不新增 runtime abstraction。

### Documentation

```text
Docs/AnimalCafe_Development_Roadmap.md
Docs/Phase0_Beginner_Guide.md
Docs/Phase2_Beginner_Guide.md
```

文档只记录实际实现与 fresh evidence；测试完成前不填写预测 count。

## 5. Test Cases

### 5.1 UI normal cases

- Pointer 不在 UI 上时，tap 仍能选择 world object。
- 点击 Scene 空白处仍能清除 selection。
- Scene 没有 `EventSystem` 时，保持原有 world tap behavior。

### 5.2 UI bug cases

- Pointer 位于 UGUI button 上时，不选择 button 后方的 world object。
- 已有 selection 时点击 UGUI button，不清除原 selection。
- UI-blocked tap 不发布 world `SelectionChanged` event。

测试应覆盖真实 `EventSystem` / UGUI raycast boundary，而不只断言一个永远返回固定值的 mock。

### 5.3 Footprint normal cases

- `32 × 32` Furniture Definition 成功。
- `1 × 1024` Furniture Definition 成功。
- 原有小 footprint definitions 与 placement tests 继续通过。

### 5.4 Footprint bug and boundary cases

- `1 × 1025` 在 Definition construction 时抛出 `ArgumentOutOfRangeException`。
- `1025 × 1` 同样拒绝，证明 rotation orientation 不影响上限。
- `int.MaxValue × 1` 立即拒绝，不进入 `CafeLayout`。
- `int.MaxValue × int.MaxValue` 使用 `long` area calculation 安全拒绝，不发生 `int` overflow。
- Rejection 不会把 definition 加入 catalog，也不会改变 Layout instances 或 Occupancy。

## 6. Implementation Direction

### 6.1 UI

在 runtime tap routing 中，在 `TrySelectAt()` 前检查：

```csharp
EventSystem.current != null
    && EventSystem.current.IsPointerOverGameObject()
```

如果为 `true`，本帧不执行 world selection。

不改变 `MouseCameraInput` 的 tap detection，也不把 UI dependency 放进 pure Layout code。

### 6.2 Footprint

在 `FurnitureDefinition` constructor 中，在保存 `Footprint` 前：

1. 用 `long` 计算 area；
2. 与 `MaxFootprintCellCount` 比较；
3. 超出时抛出包含实际 area 与最大值的 `ArgumentOutOfRangeException`。

`CafeLayout.TryGetFootprintCells()` 保持 placement responsibility，不再负责防御不可能通过 Definition construction 的巨大 footprint。

## 7. Risks 与 likely bugs

- UI test 只检查 helper，没有连接真实 `EventSystem`，可能出现 wiring 未被验证的假通过。
- UI pointer check 放在 `TrySelectAt()` 内会意外阻止明确的 programmatic selection；因此只放在 `Update()` input routing。
- 使用 `int` 计算 area 会 overflow；必须先转换为 `long`。
- 把上限放进 `GridSize` 会错误限制 Layout region；必须只限制 Furniture Definition。
- 测试或文档提前写死新的 full count，之后实际数量不一致。

## 8. Out of Scope

- Touch pointer ID 与完整 iOS input adaptation；
- 新的 `IUiPointerBlocker` abstraction；
- Input Action Map 重构；
- Decoration UI、mouse furniture placement 或 preview；
- 修改 Layout region 最大面积；
- 自动回复或 resolve GitHub PR comment；
- `main` branch 的直接修改；
- commit、push、merge 或 branch deletion。

## 9. Acceptance Gate

- 每个修复先取得针对旧代码的正确 RED。
- UI focused PlayMode tests 全部通过。
- Footprint focused EditMode tests 全部通过。
- Full EditMode 与 PlayMode regression 全部通过，所有 non-pass counts 为 `0`。
- Scene、Prefab、ProjectSettings 和 generated `.slnx` 没有意外 diff。
- `git diff --check` 通过。
- 用户完成必要 manual acceptance 后，才进入 P2 merge approval。
