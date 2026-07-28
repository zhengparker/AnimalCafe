# AnimalCafe Phase 2 — Grid Rules 设计

> 状态：用户已批准
>
> Roadmap 正式名称：Phase 2 — Grid Occupancy & Placement Rules
>
> Branch：`codex/phase-2`
>
> Worktree：`E:\Unity\Project\AnimalCafe\.worktrees\phase-2`
>
> 验证环境：Unity `6000.5.5f1` / Windows

## 1. 先用一个简单例子说明这个 Phase

可以把咖啡厅地面想成一张方格纸：

```text
⬜ ⬜ ⬜ ⬜ ⬜
⬜ 🟫 🟫 ⬜ ⬜
⬜ ⬜ ⬜ ⬜ ⬜
⬜ ⬜ ⬜ ⬜ ⬜
```

这里的每一个 `⬜` 是一个 Grid cell，两个 `🟫` 是一个占地 `2 × 1` 的柜台。

Phase 1 已经让程序知道：

- 这是一件什么家具；
- 家具有多大；
- 家具现在放在哪里；
- 家具转向哪个方向。

但 Phase 1 还不会判断：

- 柜台有没有超出店铺边界；
- 柜台是不是放到了还没解锁的区域；
- 新桌子会不会和柜台重叠；
- 柜台移动失败后，是否应该留在原来的位置。

Phase 2 就像给咖啡厅加入一名“摆放管理员”。每次玩家以后想放置、移动或旋转家具时，这名管理员会先检查所有格子：

```text
全部合法
→ 才真正修改家具位置

任何一格不合法
→ 拒绝操作，家具保持原样
```

例如，柜台向右移动后超出边界：

```text
移动前：⬜ 🟫 🟫 ⬜ ⬜
想移动：⬜ ⬜ ⬜ ⬜ 🟫 🟫  ← 最后一格超出店铺
结果：移动失败，柜台仍留在移动前的位置
```

这个 Phase 只建立和测试这些看不见的规则。它不会加入可见的家具、摆放画面、鼠标操作或 UI，所以进入 Play Mode 后画面没有明显变化是正常的。

## 2. Technical Goal

Phase 2 在 Phase 1 的纯 C# Layout Data Model 上建立 Grid Occupancy 和 Placement Rules。

系统需要在不加载 Unity Scene、不引用 `GameObject`、`Transform`、`MonoBehaviour` 或 `ScriptableObject` 的情况下，可靠回答：

- 一件家具在指定位置和 rotation 下会占用哪些 Grid cells；
- 所有 cells 是否位于 unlocked regions；
- 是否与其他家具 overlap；
- furniture 能否被 place、move、rotate 或 remove；
- operation 失败后，正式 Layout 和 Occupancy 是否完全保持原状。

Phase 2 完成后，后续 Decoration Mode 可以调用同一套已测试规则，而不需要在 UI 或 Scene code 中重新判断 placement legality。

## 3. 已批准的 Architecture

采用 `CafeLayout` 统一管理 Layout instances 与 Occupancy 的方案。

`CafeLayout` 是 layout mutation 的唯一入口：

```text
Caller
  ↓
CafeLayout transaction
  ├─ FurnitureInstances
  └─ Occupancy: GridPosition → Furniture Instance ID
```

不建立独立、长期持有第二份状态的 `PlacementService`。这可以避免 `CafeLayout` 已更新、但外部 Occupancy service 仍保留旧 cells 的 synchronization bug。

也不采用“每次 query 都扫描全部 furniture 并重建 occupancy”的方案。Phase 2 会建立真实的 occupy / release lifecycle，并直接测试它的一致性。

Occupancy 仍然是可以从 Furniture Instances、Definitions、positions 和 rotations 重建的 derived state。未来 Save system 不应把 Occupancy 当作 authoritative persisted data。

## 4. Phase 边界

### 4.1 Included

- Grid bounds，由 unlocked `LayoutRegion` cells 共同定义；
- unlocked-region containment；
- footprint rotation；
- cell calculation；
- occupy / release；
- overlap detection；
- place transaction；
- move transaction；
- rotate transaction；
- remove validation；
- occupancy consistency checks；
- 纯 EditMode automated tests；
- Phase 0 和 Phase 1 regression verification；
- `Docs/Phase2_Beginner_Guide.md`。

### 4.2 Not Included

- Mouse placement；
- visible Grid；
- placement preview；
- Decoration Mode；
- Scene rendering 或 furniture Prefabs；
- UI；
- pathfinding 或 NavMesh；
- functional anchors；
- wall placement 或 furniture-surface placement behavior；
- Save file；
- region purchase / Store Expansion；
- actual Exterior gameplay。

`FurnitureDefinition.AllowedPlacementSurfaces` 继续作为 Phase 1 data contract 保留，但 Phase 2 只执行 floor-grid occupancy。Wall 和 Furniture Surface 需要后续各自的 attachment / surface rules，不能伪装成普通 ground cells。

## 5. Public Behavior

### 5.1 Expected Gameplay Rejection

不能摆放、移动或旋转属于正常 gameplay result，不使用 exception 表达。

operation 返回一个明确的 `PlacementResult`：

```text
Succeeded
FailureReason
```

失败原因至少区分：

- `None`
- `OutOfUnlockedRegion`
- `Overlap`
- `InstanceNotFound`
- `InstanceAlreadyPlaced`

如果未来 UI 只需要显示统一的 invalid feedback，也仍可根据 `Succeeded` 工作；明确 reason 主要服务 tests、debugging 和后续可读 feedback。

### 5.2 Programming Errors

下列 caller/programming errors 继续使用现有 exception 风格：

- `null` argument；
- invalid stable ID format；
- unknown Furniture Definition；
- invalid enum value；
- default / invalid value type 绕过 constructor validation。

Expected rejection 不得抛 exception；programming error 不得被悄悄转换成普通 `false`。

同一个已经存在于 Layout 的 instance 再次执行 Place，是可预期的 gameplay rejection，返回 `InstanceAlreadyPlaced`。成功 mutation 后的内部数据仍必须保证 stable ID 唯一；任何绕过 public transaction 而形成的 duplicate stable ID 都属于 corrupted state，不是合法 gameplay state。

### 5.3 Place

Place transaction：

```text
验证 instance identity 和 definition
→ 计算 rotated footprint cells
→ 验证每个 cell 都 unlocked
→ 验证没有其他 instance 占用
→ 全部成功后同时加入 FurnitureInstances 和 Occupancy
```

任一步失败时：

- instance 不加入 Layout；
- 不留下任何 occupied cell；
- 已有 instances 和 occupancy 不改变。

Phase 1 的直接 `AddFurnitureInstance` 不再允许绕过 placement rules。Implementation plan 必须选择一个清晰 migration：

- 让它成为经过完整 validation 的 place entry point；或
- 以新的 place API 取代它，并同步更新 Phase 1 tests。

不能同时保留一个安全入口和一个可公开绕过 occupancy 的入口。

### 5.4 Move

Move 保持相同 `InstanceId` 和 `DefinitionId`，只改变 `Position`。

```text
读取旧 instance 和旧 cells
→ 使用新 position + 原 rotation 计算完整候选状态
→ 验证候选状态，检查 overlap 时忽略该 instance 自己
→ 成功才一起替换 instance data 与 occupied cells
→ 失败则旧 instance 与旧 cells 完全不变
```

Move 到当前位置是合法的 idempotent operation，不应被自己的 occupancy 拒绝。

### 5.5 Rotate

Rotate 保持 `InstanceId`、`DefinitionId` 和 `Position`，只改变 `Rotation`。

- `0° / 180°` 使用 definition 的原 width / height；
- `90° / 270°` 交换 width / height；
- overlap check 忽略该 instance 当前占用的 cells；
- 如果 rotated footprint 越界或与其他 instance overlap，rotation 失败并保留旧 rotation 和旧 cells；
- rotate 到当前 rotation 是合法的 idempotent operation。

### 5.6 Remove

第一次 remove 成功时：

- 从 `FurnitureInstances` 删除该 instance；
- 释放且只释放该 instance 的全部 cells；
- 其他 instance 和 occupancy 保持不变。

对同一个有效 ID repeated remove：

- 第一次成功；
- 后续返回 `InstanceNotFound`；
- 不抛出 gameplay exception；
- 不释放其他家具的 cells。

### 5.7 Occupancy Queries

Phase 2 需要提供只读 query，让 tests 和后续 systems 能够判断：

- 指定 cell 是否 occupied；
- occupied cell 对应哪个 Furniture Instance ID；
- 当前 occupied cell 总数。

Caller 不能取得并修改内部 dictionary。返回 collection 时必须是 read-only view 或 defensive copy。

## 6. Grid 与 Region 规则

### 6.1 Cell Calculation

家具的 `Position` 是 rotated footprint 的 anchor/origin，footprint 向 Grid 的正 X、正 Y 展开。

例如 `Position = (2, 3)`、rotated size 为 `2 × 2`：

```text
(2,3) (3,3)
(2,4) (3,4)
```

使用半开区间可以避免 boundary off-by-one：

```text
X: position.X <= x < position.X + width
Y: position.Y <= y < position.Y + height
```

### 6.2 Unlocked Cells

一个 candidate placement 合法，当且仅当它的每一个 footprint cell 都包含在至少一个 unlocked `LayoutRegion` 中。

- negative coordinates 本身合法，只要对应 cells 已 unlocked；
- footprint 可以跨越两个相邻或重叠的 unlocked regions；
- 如果 regions 之间存在一个 locked gap，footprint 不能跨过该 gap；
- 只有部分 footprint 在 unlocked region 内时必须拒绝；
- region 的右边界和上边界不属于 region。

Phase 2 不要求所有 unlocked regions 彼此连接。Region connectivity 属于后续 expansion/path validation。

### 6.3 Overlapping Regions

Unlocked regions 彼此 overlap 不会让同一个 Grid cell 被重复计数。Occupancy 以 `GridPosition` 为唯一 key。

Phase 2 不改变 Phase 1 已有的 Region ID validation，也不引入 region removal。

## 7. State Invariants

每次成功 mutation 后必须满足：

1. 每个 occupied cell 指向一个真实存在的 Furniture Instance。
2. 每个 Furniture Instance 的完整 rotated footprint 都被 Occupancy 记录。
3. 一个 Grid cell 最多属于一个 Furniture Instance。
4. Furniture Instance 的每个 occupied cell 都在 unlocked region。
5. 同一 instance 不会重复占用同一个 cell。
6. `FurnitureInstances` 中不存在 duplicate stable ID。
7. rejected transaction 前后的 instances 与 occupancy 完全相同。

Implementation 不需要在 production 中每次都做昂贵的全量 assertion，但 automated tests 必须证明所有 public mutation paths 保持这些 invariants。

## 8. File Boundary

预计 production files：

- Modify `Assets/Scripts/Layout/CafeLayout.cs`
  - 唯一 mutation 入口；
  - 管理 Occupancy；
  - 执行 place / move / rotate / remove transactions；
  - 暴露只读 occupancy queries。

- Modify `Assets/Scripts/Layout/FurnitureInstance.cs`
  - 提供保持 stable identity 的受控 position / rotation replacement；
  - 不能改成拥有 public setters 的 mutable object。

- Modify `Assets/Scripts/Layout/FurnitureDefinitionCatalog.cs`
  - 提供 transaction 所需的明确 definition lookup；
  - 保留 ordinal ID semantics。

- Modify `Assets/Scripts/Layout/GridPosition.cs`
  - 仅在需要时加入简单且可测试的 coordinate helper；
  - 不加入 Unity world-space conversion。

- Create `Assets/Scripts/Layout/PlacementResult.cs`
  - 保存 `Succeeded` 和 failure reason；
  - 不依赖 Unity types。

预计 tests：

- Create `Assets/Tests/EditMode/GridPlacementTests.cs`
  - footprint、bounds、regions、overlap、transactions 和 invariants。

- Modify `Assets/Tests/EditMode/CafeLayoutTests.cs`
  - 删除 Phase 1 明确允许 overlap / outside-region 的临时边界；
  - 替换成 Phase 2 正式 behavior；
  - 保留 Phase 1 identity、catalog、read-only 和 Scene-independence regression。

预计 documentation：

- Create `Docs/Phase2_Beginner_Guide.md`
  - 用中文解释 Occupancy、footprint、transaction、rollback、automatic/manual tests。

- Modify `Docs/AnimalCafe_Development_Roadmap.md`
  - 只在 automated verification、manual acceptance、merge 和 merged-main regression 完成后，把 Phase 2 标记为 `Completed`；
  - implementation 期间不能提前标记完成。

不修改 `Assets/Scenes/MainCafe.unity`。

## 9. Automated Test Design

### 9.1 Footprint 与 Rotation

- `1 × 1` 产生一个 cell。
- rectangular footprint 产生正确数量且没有 duplicate cells。
- non-square `2 × 3`：
  - `0°` → `2 × 3`
  - `90°` → `3 × 2`
  - `180°` → `2 × 3`
  - `270°` → `3 × 2`
- 四次 successive `90°` rotation 回到原 footprint。
- invalid rotation 不能进入 transaction。

### 9.2 Bounds 与 Unlocked Regions

- placement 刚好贴住 left、bottom、right、top boundary 时成功。
- right 或 top 多出一个 cell 时失败。
- footprint 只有一部分 unlocked 时失败。
- negative-coordinate region 内 placement 成功。
- 两个相邻 unlocked regions 可以共同覆盖一个 footprint。
- 两个 regions 中间有一个 cell gap 时失败。
- overlapping regions 不重复计算 occupied cells。
- 没有 unlocked region 时 place 失败。

### 9.3 Place 与 Overlap

- 第一个合法 instance place 成功。
- 两件家具紧邻成功。
- overlap 一个 cell 也失败。
- 完全相同 position 的第二个 instance 失败。
- failed place 不增加 instance count 或 occupied cell count。
- 同一个 instance ID 不能重复 place。
- unknown definition 继续使用 programming-error exception。

### 9.4 Move Transaction

- move 到空闲合法位置成功。
- 成功后旧 cells 全部释放、新 cells 全部 occupied。
- move 到当前位置成功且 state 不重复。
- move 时可以复用自己的部分旧 cells。
- move 越界失败并保留旧 position、rotation 和 cells。
- move 到 locked gap 失败并保留旧状态。
- move overlap 失败并保留旧状态。
- failed move 后另一次合法 move 仍能成功。
- unknown valid instance ID 返回 `InstanceNotFound`。

### 9.5 Rotate Transaction

- non-square furniture 的 `90°` rotation 正确改变 occupied cells。
- rotation 可以复用自己的旧 cells。
- rotation 越界失败并保留旧 rotation 和 cells。
- rotation overlap 失败并保留旧 rotation 和 cells。
- rotate 到当前 rotation 不产生 duplicate occupancy。

### 9.6 Remove 与 Release

- remove 成功后 instance 和它的 cells 消失。
- remove 不改变其他 instance。
- repeated remove 返回 `InstanceNotFound`。
- repeated remove 不释放后来占用邻近 cells 的其他家具。
- remove 后原 cells 可以被新 instance 合法复用。

### 9.7 Consistency 与 Regression

- 每个 occupied cell 的 owner 都存在于 Layout。
- 每个 Layout instance 的完整 footprint 都出现在 Occupancy。
- occupied cell count 等于所有不重叠 footprints 的 cell 总数。
- rejected operation 前后 instances 和 occupancy snapshot 完全相同。
- exposed occupancy 不能被 caller 修改。
- Layout domain tests 不加载 `MainCafe` Scene。
- Layout domain fields 不含 Unity object 或 Scene reference。
- Phase 1 stable ID、validation、catalog 和 collection tests 继续通过。
- Phase 0 Camera、Input、Selection 和 Time tests 继续通过。

## 10. Manual Test Plan

Phase 2 是 pure rules phase，没有新的 visible Scene behavior。Manual acceptance 重点是 Test Runner、Scene regression 和 Console。

### 10.1 打开隔离项目

在 Unity Hub 使用 Unity `6000.5.5f1` 打开：

```text
E:\Unity\Project\AnimalCafe\.worktrees\phase-2
```

### 10.2 EditMode

1. 打开 `Window → General → Test Runner`。
2. 选择 EditMode。
3. 运行全部 tests。
4. 确认全部绿色。
5. 确认 Failed、Skipped、Inconclusive 都为 `0`。
6. 展开 Phase 2 tests，确认能看到：
   - footprint / rotation；
   - bounds / unlocked regions；
   - overlap；
   - place；
   - failed move rollback；
   - rotate rollback；
   - repeated remove；
   - consistency。

### 10.3 Scene 与 PlayMode Regression

1. 打开 `Assets/Scenes/MainCafe.unity`。
2. 确认没有 Phase 2 Grid、furniture、preview 或 UI；Phase 2 不应创建这些 objects。
3. 清空 Console。
4. 进入 Play Mode。
5. 测试 mouse drag pan。
6. 测试 mouse-wheel zoom。
7. 测试 `Pause`、`1x`、`2x`。
8. 退出 Play Mode。
9. 确认 Console 没有红色 error。
10. 运行全部 PlayMode tests，确认全部绿色且没有 skipped / inconclusive。

### 10.4 用户 Acceptance

用户需要确认：

- automated tests 全绿；
- Scene 没有意外新增或丢失 objects；
- Phase 0 controls 正常；
- Console clean；
- Phase 2 beginner guide 可理解。

在用户明确批准前，不 merge 到 `main`，不删除 Phase 2 branch/worktree。

## 11. Verification 与 Completion Gate

Phase 2 implementation 完成后必须取得 fresh evidence：

- full EditMode tests：全部 passed；
- full PlayMode tests：全部 passed；
- failed `0`；
- skipped `0`；
- inconclusive `0`；
- layout source boundary scan 通过；
- Scene 未被 Phase 2 修改；
- orphan `.meta` scan 通过；
- `git diff --check` 通过；
- 用户完成 manual test 并明确批准；
- merge 到本地 `main` 后重新运行 full EditMode 和 PlayMode regression。

只有以上全部通过，才能：

1. 在 Roadmap 标记 Phase 2 `Completed`；
2. 删除 Phase 2 worktree；
3. 删除 Phase 2 branch。

本项目由用户通过 GitHub Desktop 管理 commit 和 push。Codex 不自动 commit、push、merge 或删除 branch。

## 12. 当前 Baseline 说明

Phase 1 记录的已接受基线是：

```text
EditMode: 116 / 116 passed
PlayMode: 18 / 18 passed
Failed: 0
Skipped: 0
Inconclusive: 0
```

2026-07-27 在新 Phase 2 worktree 尝试 fresh EditMode baseline 时，Unity batch mode 在 tests 启动前遇到 Licensing Client reconnect timeout，没有生成 result XML。这不是 test pass 或 test failure evidence。

在开始 implementation 前必须重新取得 fresh baseline；如果 sandbox 内仍无法连接 Licensing Client，则在用户允许且 Unity 已关闭的前提下，使用可正常访问 Licensing Client 的执行环境重试。
