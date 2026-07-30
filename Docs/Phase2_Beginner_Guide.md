# AnimalCafe Phase 2 Beginner Guide

> 这是一份面向 Unity 和 coding 初学者的 educational note。
> 它只解释 Phase 2 的 Grid Rules。

## 1. 先用一个简单例子说明 Phase 2

把咖啡厅地面想成一张方格纸，每件家具会盖住一个或多个格子。

如果一个 `2 × 1` 柜台已经占了两个格子，新桌子就不能盖住其中任何一格。
如果玩家把柜台移动到店铺外面，程序必须拒绝这次移动，并让柜台留在原来的位置。

Phase 2 建立的就是这名“摆放管理员”。它只负责判断规则，不负责显示家具或让鼠标拖动家具。

## 2. Phase 1 和 Phase 2 的区别

- Phase 1 stores data：它建立 `CafeLayout`、`FurnitureInstance`、`FurnitureDefinition` 等资料结构，回答“有哪些家具、它们的 ID 和位置是什么”。
- Phase 2 judges legal changes：它判断一次 `Place`、`Move`、`Rotate` 或 `Remove` 是否合法，并安全地更新资料。

可以把 Phase 1 想成记录家具资料的表格，Phase 2 则像检查规则的管理员。Phase 2 仍然是 pure rules，不会在 Scene 中生成看得见的家具。

## 3. Grid Cell、Footprint 和 Occupancy

`Grid Cell` 是地面上的一格。`Footprint` 是一件家具会盖住哪些格子。

例如一件 `2 × 3` 家具：

```text
0°：宽 2 格、高 3 格       90°：宽 3 格、高 2 格

■ ■                         ■ ■ ■
■ ■                         ■ ■ ■
■ ■
```

`180°` 的大小仍是 `2 × 3`，`270°` 的大小仍是 `3 × 2`。

Region 的右边和上边使用“到边界前一格为止”的规则。比如一排从 `X = 0` 开始、宽 3 格，真正包含的是 `0、1、2`，`X = 3` 已经在外面。这样可以把相邻 Region 接在一起而不重复同一格，不需要先记数学公式。

两个 unlocked regions 如果边缘紧贴，家具可以跨过它们，因为每一格都有开放区域覆盖：

```text
开放区域 A | 开放区域 B
□ □ □     | □ □ □
```

如果中间隔着一格 locked gap，家具只要碰到那一格就必须被拒绝：

```text
开放区域 A | 锁住的一格 | 开放区域 B
□ □ □     |     ×      | □ □ □
```

`Occupancy` 是“每一格目前由谁使用”的记录。它很像电影院座位表或停车场地图：看到一个格子，就能查到它是空的，还是被哪一个 `Furniture Instance ID` 占用。即使两个 unlocked regions 重叠，同一个 Grid Cell 也只会记录一次。

## 4. Place、Move、Rotate、Remove

- `Place`：先确认 definition 允许 `Floor`，再检查家具全部 footprint cells 都在 unlocked region 内，而且没有 overlap；成功后才加入家具和 Occupancy。
- `Move`：保持同一个 `InstanceId`、`DefinitionId` 和 rotation，只尝试替换 position。
- `Rotate`：保持同一个 `InstanceId`、`DefinitionId` 和 position，只尝试替换 rotation；`90° / 270°` 会交换宽高。
- `Remove`：成功时删除家具并释放它自己的全部 cells。

`FurnitureInstance` 采用 immutable replacement。意思是成功移动或旋转时，不直接修改旧 object，而是建立一个带有新 position 或 rotation 的 data object。它仍使用相同的 `InstanceId`，所以在游戏逻辑中还是同一件家具。

对同一个 ID repeated `Remove` 是安全的：第一次成功，之后返回 `InstanceNotFound`。后面的尝试不会抛出 gameplay exception，也不会误删或释放其他家具。

Phase 2 的 API 专门处理 floor-grid placement，因此会拒绝只允许 `Wall` 或只允许 `FurnitureSurface` 的 definition，并返回 `UnsupportedPlacementSurface`。如果 definition 同时包含 `Floor` 和其他 surface（例如 `Floor | Wall`），它仍然可以使用 floor-grid placement；关键是 allowed surfaces 中必须包含 `Floor`。

## 5. Transaction 和 Rollback

`Transaction` 的核心是：先检查所有条件，再一次改变资料。

```text
保留旧状态
→ 计算完整候选 footprint
→ 检查 bounds、unlocked cells 和 overlap
→ 全部通过才更新 FurnitureInstances 与 Occupancy
```

如果其中一步失败，就执行 `Rollback` 的效果：旧家具、旧位置和旧 Occupancy 都保持不变，不会留下“移动了一半”的资料。

程序也会区分两类问题：

- Expected result：玩家尝试把家具放在 locked cell、发生 overlap、使用不支持 floor grid 的 definition，或移除不存在的有效 ID。这是正常 gameplay rejection，使用 `PlacementResult` 返回失败原因。
- Programming exception：caller 传入 `null`、格式错误的 ID、未知 `FurnitureDefinition` 或无效 rotation。这代表代码使用方式错误，应尽早抛出 exception，帮助开发者发现 bug。

`PlacementResult` 的常见 failure reason 包括：`OutOfUnlockedRegion`、`Overlap`、`InstanceNotFound`、`InstanceAlreadyPlaced` 和 `UnsupportedPlacementSurface`。最后一项表示 definition 不包含 `Floor`，不是程序 crash。

## 6. 正常 Tests

正常 tests 证明合法操作能够成功，例如：

- `1 × 1`、矩形和 `2 × 3` footprint 产生正确 cells。
- `0° / 90° / 180° / 270°` 使用正确宽高。
- 家具刚好贴住 unlocked region 的边界仍可放置。
- 两件家具相邻但不 overlap 时都能放置。
- 合法 `Place`、`Move`、`Rotate`、`Remove` 会同步更新家具资料和 Occupancy。
- Move 到当前位置、Rotate 到当前角度等 idempotent operation 不会重复占格。

Fresh automated evidence（2026-07-27）：

```text
EditMode: 184 / 184 passed
PlayMode: 18 / 18 passed
Failed: 0
Skipped: 0
Inconclusive: 0
```

Latest-main integrated evidence（2026-07-30）：

```text
Phase 0 Scene cleanup: 1 / 1 passed
Phase 0 PlayMode: 25 / 25 passed
GridPlacementTests: 67 / 67 passed
Full EditMode: 184 / 184 passed
Full PlayMode: 31 / 31 passed
Failed: 0
Skipped: 0
Inconclusive: 0
```

这些是 cumulative regression tests：

```text
Phase 0 tests + Phase 1 tests + Phase 2 tests
```

Phase 2 加入的是 pure C# layout rules，所以新增 coverage 主要在 EditMode，EditMode 数量增加到 184。Phase 2 本身没有新增 Scene object 或运行时操作；最新 main 的 Phase 0 hardening 增加了 PlayMode coverage，因此 integrated PlayMode baseline 从原来的 18 增加到 31，用来进一步保护 Camera、Input、Selection 和 Time behavior。

## 7. Bug / Edge Tests

Bug / Edge tests 专门检查容易出错的边界：

- footprint 右边或上边多出一格时必须失败，防止 off-by-one。
- footprint 只有一部分在 unlocked region 内时必须失败。
- 相邻 unlocked regions 可以共同覆盖家具，但一格 locked gap 会拒绝它。
- overlap 一格也必须失败。
- failed `Place`、`Move` 或 `Rotate` 前后的 instances 与 Occupancy 完全相同。
- Move 或 Rotate 可以复用自己原来占用的 cells，不会被自己挡住。
- repeated `Remove` 返回 `InstanceNotFound`，而且不释放别的家具。
- Occupancy 中每个 owner 都存在，每件家具的完整 footprint 都能在 Occupancy 找到。
- caller 不能通过 read-only collection 绕过 rules 修改内部资料。
- 极端 coordinate 不会发生整数溢出后错误占格。
- Layout source 保持 pure C#，不加入 Unity Scene reference。

## 8. Phase 2 Files

Phase 2 source、tests 与本 guide 的准确路径是：

```text
Assets/Scripts/Layout/PlacementResult.cs
Assets/Scripts/Layout/CafeLayout.cs
Assets/Scripts/Layout/FurnitureInstance.cs
Assets/Tests/EditMode/GridPlacementTests.cs
Assets/Tests/EditMode/CafeLayoutTests.cs
Assets/Tests/EditMode/FurnitureDefinitionCatalogTests.cs
Docs/Phase2_Beginner_Guide.md
```

- `PlacementResult.cs`：表示操作成功或可预期的失败原因。
- `CafeLayout.cs`：唯一的 placement mutation 入口，管理 transaction 与 Occupancy。
- `FurnitureInstance.cs`：使用 stable identity 和 immutable replacement。
- 三个 EditMode test files：覆盖 Phase 2 rules、Phase 1 regression 与 source boundary。
- 本 guide：帮助初学者理解与手动验收。

## 9. Unity Manual Test

1. 用 Unity `6000.5.5f1` 打开 `E:\Unity\Project\AnimalCafe\.worktrees\phase-2`。
2. 打开 `Test Runner → EditMode → Run All`；确认全部绿色，Failed、Skipped、Inconclusive 都是 `0`。
3. 展开 `AnimalCafe.Tests → GridPlacementTests`，确认包含 bounds、rotation、overlap、rollback、remove、consistency。
4. 打开 `Assets/Scenes/MainCafe.unity`；确认没有出现可见的 Phase 2 Grid、furniture、preview 或 UI。
5. 清空 Console，然后进入 Play Mode。
6. 检查 mouse pan、wheel zoom、`Pause`、`1x`、`2x`。
7. 退出 Play Mode；确认 Console 没有红色 error。
8. 打开 `Test Runner → PlayMode → Run All`；确认全部绿色。
9. 阅读本 guide，并确认解释对初学者来说可以理解。

## 10. Phase 2 没有做什么

Phase 2 没有制作：

- 看得见的 Grid、furniture model 或 Scene renderer；
- mouse drag placement；
- placement preview 或合法/非法颜色提示；
- Decoration UI；
- pathfinding、customer 或 employee behavior；
- Save / Load。

因此打开 `MainCafe.unity` 时没有新的可见功能是预期结果，不是漏做。Phase 2 只先把未来可视化摆放功能依赖的规则打稳。

## 11. Beginner Glossary

| 名词 | 初学者解释 |
|---|---|
| `Grid Cell` | 咖啡厅方格地面上的一格。 |
| `Footprint` | 一件家具覆盖的全部格子。 |
| `Unlocked Region` | 当前允许放家具的区域。 |
| `Occupancy` | 记录每一格是否被占用、由谁占用的地图。 |
| `Overlap` | 两件家具占到至少一个相同格子。 |
| `Transaction` | 先完整检查，全部合法才一起改变资料。 |
| `Rollback` | 操作失败后保留原状态，不留下半成品。 |
| `Immutable` | 旧 data object 不直接改；用新 object 替换。 |
| `Stable ID` | object 更新后仍保持不变的身份编号。 |
| `Idempotent` | 同样操作重复执行，不会产生额外副作用。 |
| `Regression` | 新功能意外破坏旧功能。 |
| `EditMode` | 不进入游戏运行状态即可执行的 Unity tests。 |
| `PlayMode` | 在 Unity 运行状态中检查行为的 tests。 |

## 12. 完成状态和下一步

Phase 2 当前状态是 `In Review`，不是 `Completed`。用户已于 2026-07-30 完成 manual acceptance：EditMode `184 / 184`、PlayMode `31 / 31` 全部通过，`GridPlacementTests` categories、Scene regression、Phase 0 controls、Console 和本 guide 均已确认。

最新 `main` 已整合到 Phase 2，fresh integrated automated verification 与 manual acceptance 均已通过。下一步由用户完成当前 P2 merge commit 和 branch push，再批准 merge，并在 merged `main` 上重新运行 full EditMode 与 PlayMode regression。

在用户明确验收、merge 和 merged-main regression 完成前，Roadmap 不能把 Phase 2 标记为 `Completed`。
