# AnimalCafe Phase 1 Beginner Guide

> 这是一份面向 Unity 和 coding 初学者的 educational note。
> 它解释 Phase 1 做了什么、为什么这样做，以及你应该怎样验证结果。

## 1. 这个 Phase 的目标

Phase 1 的名称是：

```text
Layout Data Model
```

它的目标是先建立“咖啡店布局数据应该怎样表达”的可靠规则，而不是立刻在 Scene 中摆放家具。

完成后，程序可以用纯 C# data 表达：

- Grid 的基本设置；
- 区域的位置和尺寸；
- 家具定义；
- 单个家具实例；
- 稳定 ID；
- 家具目录；
- 整个 Cafe layout。

这些 data contracts 会被后续 Grid Rules、placement、save 和 expansion phases 使用。

## 2. 开发前是什么状态

Phase 0 已经提供 Camera、input、selection、time controls 和 automated test foundation。

但是项目还没有统一的方法回答：

- 一个 grid cell 多大？
- 一个区域从哪里开始、占多少格？
- 一件家具占多少格？
- 家具允许怎样旋转？
- definition ID 和 instance ID 有什么区别？
- 怎样保存多个 Regions 和 Furniture Instances？
- duplicate ID 或 unknown definition 应该怎样拒绝？

旧 Phase 1 Core Cafe Loop 曾经把柜台、员工、顾客、NavMesh 和临时地板直接放进 Scene。该方案已经 `Superseded`，不是当前 Phase 1 的实现基础。

## 3. Phase 1 做了什么改动

### 3.1 新增 Layout Data Model

新增了 Grid、Furniture、Region 和 CafeLayout 的纯 C# types。

这些 types 不继承 `MonoBehaviour` 或 `ScriptableObject`，也不保存 `GameObject`、`Transform` 或 Scene reference。

这样做的好处是：

- 容易 automated test；
- 不依赖某一个 Scene；
- 以后容易 Save/Load；
- gameplay 和 visual representation 可以分开开发。

### 3.2 清理 Phase 0 Demo

正式 `MainCafe` 不再包含：

- `Phase0_Demo`
- `Selectable_Blue`
- `Selectable_Green`
- `Time_Test_Mover`
- 三个 demo materials
- 灰色或黄色的旧临时 floor
- 旧 Customer、Cashier、Barista、Counter、NavMesh 或 status UI

正式 Camera、runtime、selection contract 和 time controls 都保留。

### 3.3 加强 Regression Tests

Phase 1 使用 strict TDD：

1. 先写会失败的 test。
2. 确认失败原因正是缺少需要的 behavior。
3. 再写最小 implementation。
4. 重新运行 focused 和 full tests。
5. 独立 review 发现缺口时，再用同样的 RED → GREEN 流程修复。

## 4. 重要概念解释

### GridSettings

保存整个 layout grid 的共同设置，例如 cell size。

### GridPosition

表示一个 object 在 grid 上的整数坐标，而不是 Unity world-space `Vector3`。

### GridSize

表示一个区域或家具 footprint 占多少格。Width 和 Height 必须至少为 `1`。

### FurnitureDefinition

描述“一类家具是什么”，例如：

- definition ID；
- footprint；
- placement surface；
- allowed rotations。

它不是某一张具体桌子，而是桌子种类的定义。

### FurnitureInstance

表示 layout 中真正存在的一件家具。它保存：

- stable instance ID；
- definition ID；
- GridPosition；
- rotation。

### StableId

长期稳定的 identity。即使家具移动或旋转，instance ID 仍不应该改变。

### FurnitureDefinitionCatalog

保存所有已知 Furniture Definitions，并负责：

- lookup；
- unknown definition rejection；
- duplicate definition rejection；
- ordinal ID comparison。

### LayoutRegion

表示一个可用区域的 origin、size 和 zone type。

### CafeLayout

Phase 1 的 aggregate root。它统一保存：

- GridSettings；
- FurnitureDefinitionCatalog；
- LayoutRegions；
- FurnitureInstances。

Phase 1 只负责 data integrity，不负责 occupancy、pathfinding 或真实 placement execution。

## 5. Phase 1 Files

### Production Data Model

```text
Assets/Scripts/Layout/GridSettings.cs
Assets/Scripts/Layout/GridPosition.cs
Assets/Scripts/Layout/GridSize.cs
Assets/Scripts/Layout/FurnitureRotation.cs
Assets/Scripts/Layout/PlacementSurfaceType.cs
Assets/Scripts/Layout/LayoutZoneType.cs
Assets/Scripts/Layout/StableId.cs
Assets/Scripts/Layout/FurnitureDefinition.cs
Assets/Scripts/Layout/FurnitureInstance.cs
Assets/Scripts/Layout/FurnitureDefinitionCatalog.cs
Assets/Scripts/Layout/LayoutRegion.cs
Assets/Scripts/Layout/CafeLayout.cs
```

### EditMode Tests

```text
Assets/Tests/EditMode/GridValueTests.cs
Assets/Tests/EditMode/FurnitureDefinitionTests.cs
Assets/Tests/EditMode/FurnitureInstanceTests.cs
Assets/Tests/EditMode/FurnitureDefinitionCatalogTests.cs
Assets/Tests/EditMode/CafeLayoutTests.cs
Assets/Tests/EditMode/Phase0SceneCleanupTests.cs
```

### Scene and Phase 0 Regression

```text
Assets/Editor/Phase0SceneSetup.cs
Assets/Scenes/MainCafe.unity
Assets/Tests/PlayMode/Phase0PlayModeTests.cs
```

### Deleted Demo-only Files

```text
Assets/Materials/Phase0Blue.mat
Assets/Materials/Phase0Green.mat
Assets/Materials/Phase0Orange.mat
Assets/Scripts/Testing/TimeTestMover.cs
```

对应 `.meta` files 也一起删除，避免 orphan metadata。

## 6. Tests 和 Bug Cases

Phase 1 最终 automated evidence：

```text
EditMode: 116 / 116 passed
PlayMode: 18 / 18 passed
Failed: 0
Skipped: 0
Inconclusive: 0
```

重要 regression cases 包括：

- negative、zero、NaN 或 infinity grid settings 被拒绝；
- zero-size `GridSize` 被拒绝；
- `default(GridSize)` 不能绕过 Region 或 Furniture footprint validation；
- invalid enum value 被拒绝；
- duplicate definition、region 或 instance ID 被拒绝；
- unknown furniture definition 被拒绝；
- validation 失败不会留下半完成 mutation；
- exposed collections 不能从外部修改；
- ID comparison 使用 ordinal semantics，不受语言文化设置影响；
- Layout domain 不包含 Unity object 或 Scene reference；
- cleanup 能删除 active、inactive 和 duplicate legacy demo roots；
- 正式 Scene 不含旧 Phase 1 objects 或 baked NavMesh；
- Phase 0 Camera、selection 和 time controls 继续通过 regression tests。

## 7. Unity Manual Test

### 7.1 打开正确项目

在 Unity Hub 使用 `Add project from disk` 添加：

```text
E:\Unity\Project\AnimalCafe\.worktrees\phase1-layout-data-model
```

使用 Unity `6000.5.5f1`，打开：

```text
Assets/Scenes/MainCafe.unity
```

### 7.2 EditMode Tests

1. 打开 `Window → General → Test Runner`。
2. 选择 EditMode。
3. 运行全部 tests。
4. 确认全部绿色，没有 Failed、Skipped 或 Inconclusive。
5. 确认能看到 Grid、Definition、Instance、Catalog 和 Layout test groups。

### 7.3 Scene 清洁检查

Hierarchy 应该有：

- `Main Camera`
- `Phase0_Runtime`
- `Phase0_TimeControls`
- `EventSystem`

Hierarchy 不应该有：

- Phase 0 demo cubes 或 mover；
- 灰色/黄色旧 floor；
- Customer、Cashier 或 Barista；
- Counter、Coffee Machine 或 Pick-up；
- NavMesh 或旧 Phase 1 status UI。

### 7.4 Play Mode

1. 清空 Console。
2. 进入 Play Mode。
3. 确认没有旧咖啡店 objects。
4. 点击 `Pause`、`1x`、`2x`。
5. 使用 mouse wheel 测试 zoom。
6. 使用 mouse drag 测试 pan。
7. 退出 Play Mode。
8. 确认 Console 没有红色 error。
9. 在 Test Runner 运行全部 PlayMode tests，确认全部绿色。

正式 Scene 不再保留 selection cube 或 time mover。selection 与精确 time-scale behavior 由 automated test-local fixtures 验证。

## 8. Phase 1 没有做什么

Phase 1 没有实现：

- visible placement grid；
- furniture placement、move、rotate 或 remove UI；
- occupancy rules；
- surface containment；
- NavMesh navigation；
- Customer、Cashier 或 Barista；
- order、preparation 或 pickup flow；
- Save/Load；
- economy 或 progression；
- 正式咖啡店 visual layout。

因此进入 Play Mode 后没有家具画面是正确结果，不是功能丢失。

## 9. Beginner Glossary

| Term | 简单解释 |
| --- | --- |
| Data Model | 描述数据结构和规则的 code |
| Value Type | 主要表达一个值的 type，例如 position 或 size |
| Domain | 当前业务规则所属的范围，这里是 Cafe Layout |
| Contract | 其他 code 可以依赖的明确规则 |
| Validation | 检查输入是否允许 |
| Exception | 非法操作发生时给 caller 的明确错误 |
| ID | 用来识别 definition 或 instance 的文字 identity |
| Ordinal Comparison | 按字符本身比较 ID，不受语言文化影响 |
| Immutable | 建立后不能从外部随意改变 |
| Read-only Collection | caller 可以查看、不能直接修改的 collection |
| Aggregate Root | 统一管理一组相关 domain data 的入口 |
| Footprint | 家具或区域在 grid 上占用的宽度和高度 |
| TDD | Test-Driven Development，先写失败 test 再实现 |
| RED | test 按预期失败 |
| GREEN | implementation 后 test 通过 |

## 10. 完成状态和下一步

Phase 1 当前状态是 `Completed`。

最终 gate 已全部完成：implementation、strict TDD、automated verification、independent review、Studio Owner manual acceptance、merge，以及 merged-main regression。最终 fresh evidence 为 EditMode `116 / 116`、PlayMode `18 / 18` passed；Failed、Skipped、Inconclusive 均为 `0`。

本 Guide 中列出的正常、异常、边界、rollback、Scene 清洁、Camera、Input、Selection 和 Game Time test cases 均已完成并通过。它们现在是历史验收记录；以后修改 Phase 1 contract 时，应重新运行受影响的 focused tests 和完整 regression。

下一阶段是 Phase 2：Grid Rules。它会在本 Phase 的 data contracts 上继续建立 grid behavior。
