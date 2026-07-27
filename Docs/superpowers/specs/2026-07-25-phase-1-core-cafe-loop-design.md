# AnimalCafe Phase 1 — 核心咖啡厅循环 MVP 设计

> **状态：Superseded（2026-07-26）**
>
> 本文档描述旧版大型 Phase 1，保留作为历史参考，不再作为当前 implementation 依据。
> 当前 roadmap 已将 Phase 1 之后重新拆分；新的执行入口是
> `Docs/AnimalCafe_Development_Roadmap.md` 中的 **Phase 1 — Layout Data Model**。

**旧版状态：** 曾获批准，但现已由新版 dependency-driven roadmap 取代
**日期：** 2026-07-25
**基线：** Phase 0 已完成；Unity `6000.5.5f1` 下 16 / 16 Play Mode tests 通过

## 1. 目标

Phase 1 必须证明咖啡厅可以在没有玩家持续操作的情况下，自动完成一个小型外带服务循环：

```text
生成 Customer
→ 加入柜台 Queue
→ 固定岗位 Cashier 完成点单和收银
→ 创建 FIFO order
→ Customer 前往 Pick-up
→ 固定岗位 Barista 领取最早的等待 order
→ Barista 制作一杯基础 coffee
→ Barista 将 coffee 送到 Pick-up
→ 对应 Customer 取餐
→ Customer 离开
```

完成后的 prototype 必须支持多名 customers 连续完成服务，不能出现 order 重复、容量超限、角色永久卡住或未处理的 Console error。

## 2. 范围

### 2.1 包含内容

- 一个小型 L-shaped cafe layout。
- 使用 Unity AI Navigation / NavMesh 移动。
- 两名固定岗位员工：
  - 一名 Cat Cashier；
  - 一名 Fox Barista。
- 一种 Bunny customer 外观，runtime 中可生成多个 instances。
- 一台 coffee machine。
- 一种不依赖 inventory 的基础 coffee。
- 点单和收银时间为 1 秒。
- Coffee 制作时间为 2 秒。
- FIFO order 处理。
- 明确的 customer、employee 和 order states。
- 柜台 Queue 容量为 3。
- Pick-up 容量为 2。
- 同时最多存在 5 名 active customers。
- 每隔 3–5 秒尝试生成一名 customer。
- 根据容量暂停和恢复生成。
- 在 Cashier 开始服务前预留 Pick-up 位置。
- Movement failure detection 和安全 recovery。
- 精简的 placeholder status UI。
- Phase 1 Play Mode tests 和 beginner guide。

所有时间与容量参数都必须通过 Inspector 配置，不能隐藏为难以调整的硬编码常量。

### 2.2 不包含内容

- Inventory、ingredients、材料预留或 restocking。
- Income、costs、pricing 或完整 economy。
- 员工换岗、排班、skills 或动态工作分配。
- 多名制作员工或多台制作设备。
- Character traits、relationships、moods 或 stories。
- Seats 或 dine-in service。
- Customer patience 或 satisfaction。
- Save 或 offline progression。
- Order cancellation、refund 或 replacement。
- 正式 UI art 或正式角色 animation。
- 随机 customer species 或外观生成。

## 3. Architecture

Phase 1 使用小型 component state machines，并由中央 order service 和 capacity service 管理共享规则。

### 3.1 计划创建的 runtime files

```text
Assets/Scripts/
├─ Characters/
│  ├─ CustomerController.cs
│  ├─ CustomerState.cs
│  ├─ EmployeeMover.cs
│  ├─ CashierController.cs
│  └─ BaristaController.cs
├─ Orders/
│  ├─ CafeOrder.cs
│  ├─ OrderState.cs
│  └─ OrderService.cs
├─ Cafe/
│  ├─ CafeCapacityService.cs
│  ├─ CustomerSpawner.cs
│  ├─ CafeStations.cs
│  └─ Phase1CafeController.cs
└─ UI/
   └─ CafeStatusPanel.cs
```

### 3.2 File 职责

- `CustomerController`：管理单个 customer 从进入到离开的 state transitions。
- `CustomerState`：定义 customer 可使用的主要 states。
- `EmployeeMover`：提供共用的 NavMesh movement、timeout、retry 和 recovery 行为。
- `CashierController`：服务 Queue 最前方的 customer，并在 1 秒后创建 order。
- `BaristaController`：领取一个 FIFO order，移动、制作 2 秒并完成送餐。
- `CafeOrder`：保存唯一 ID、对应 customer 和当前 order state。
- `OrderState`：定义 order 可使用的 states。
- `OrderService`：唯一允许创建、领取、失败、取走或完成 order 的 service。
- `CafeCapacityService`：统一管理柜台、Pick-up 和 customer 总容量。
- `CustomerSpawner`：按照配置的间隔尝试生成 customer，并在容量不足时暂停。
- `CafeStations`：保存入口、Queue slots、收银点、coffee machine、Pick-up slots、员工 idle point、recovery points 和出口。
- `Phase1CafeController`：连接各项 dependencies 并启动循环，不包含各 component 的具体内部行为。
- `CafeStatusPanel`：显示只读 runtime 状态，方便观察与测试。

现有 `AnimalCafe.Runtime` assembly 继续作为 runtime boundary。Phase 1 不额外创建第二个 gameplay assembly。

## 4. States 与服务规则

### 4.1 Customer states

```text
Entering
→ Queueing
→ Ordering
→ MovingToPickup
→ WaitingForOrder
→ Collecting
→ Leaving
→ Completed
```

`Recovering` 只在 movement failure 后使用。

- Customers 按柜台 Queue 顺序接受服务。
- 只有 Queue 最前方的 customer 可以进入 `Ordering`。
- Cashier 开始服务前必须先预留一个 Pick-up slot。
- 没有 Pick-up 空位时，Cashier 等待，不创建无处等待的 order。
- Customer 离开柜台时释放 counter slot。
- Customer 取餐并开始离开时释放 Pick-up slot。
- Customer 完成离场或完成安全 cleanup 后释放总容量。

### 4.2 Cashier states

```text
Idle
→ Serving
→ CompletingPayment
→ Idle
```

- Cashier 是固定位置的 employee。
- 每次服务耗时 1 个 scaled second。
- 只有服务完成后才创建 order。
- 如果正在服务的 customer 失效，Cashier 取消本次服务、释放预留位置并回到 `Idle`。

### 4.3 Order states

```text
Created
→ Waiting
→ Claimed
→ Preparing
→ ReadyForDelivery
→ AtPickup
→ Collected
→ Completed
```

`Failed` 是 terminal state，只在 recovery 无法完成 order 时使用。

- Order ID 在一次 runtime session 中必须唯一并持续递增。
- 新建 order 进入 FIFO waiting queue。
- 只有 `Waiting` order 可以被领取。
- 领取成功后立即从可领取 FIFO queue 中移除。
- 已领取、已完成或失败的 order 不能再次领取。
- 已取走、已完成或失败的 order 不能再次完成。

### 4.4 Barista states

```text
Idle
→ MovingToMachine
→ Preparing
→ MovingToPickup
→ Delivering
→ ReturningToIdle
→ Idle
```

- Barista 每次只处理一个 order。
- Coffee 制作耗时 2 个 scaled seconds。
- Phase 1 只有一名 Barista 和一个 active preparation task，因此 coffee machine 每次只制作一杯 coffee。
- 只有 order 中记录的对应 customer 可以领取该 order。
- 送餐完成后，Barista 先返回 idle point，再领取下一个 order。

### 4.5 Game Time

- Movement、Cashier 服务、Barista 制作和正常 spawn interval 使用 scaled game time。
- Pause 停止 cafe loop，同时保留 Phase 0 的 Camera、selection 和 UI 操作。
- 在 `2x` 下，1 秒收银和 2 秒制作所需的现实时间约为 `1x` 的一半。
- Pause 时 movement failure timeout 不增加。

## 5. 容量与生成规则

默认 Inspector 参数：

| 参数 | 默认值 |
|---|---:|
| Counter Queue 容量 | 3 |
| Pick-up 容量 | 2 |
| Active customer 总容量 | 5 |
| Spawn interval | 3–5 scaled seconds |
| Cashier 服务时间 | 1 scaled second |
| Barista 制作时间 | 2 scaled seconds |

以下任一情况发生时暂停生成：

- Customer 总容量已满；
- Counter Queue 无法接收下一名 customer；
- 2 个 Pick-up slots 全部已被占用或预留。

容量恢复后，spawner 自动继续运行。失败的 spawn attempt 不能留下不完整的 customer，也不能消耗容量。

## 6. Scene 与视觉设计

### 6.1 L-shaped layout

已选择的 layout 包含：

- 入口和柜台 Queue 位于下方；
- Counter 和固定 Cashier 位于转角；
- Coffee machine 和 Barista 工作区位于柜台上方；
- Pick-up 位于右侧；
- 出口位于 Pick-up 下方或后方。

Customer 的路线会从 Queue 明显转向 Pick-up。通道必须足够宽，避免 Phase 1 agents 在转角处互相堵住。

### 6.2 Models

- Kenney Cat model：Cashier。
- Kenney Fox model：Barista。
- Kenney Bunny model：customer prefab。
- 现有 Kenney Furniture Kit assets：counter、coffee machine stand-in 和简单装饰。
- 小型 cup-like placeholder：完成后的 coffee。

使用简单 label 或克制的颜色区分岗位和当前状态。随机 species 和正式角色 animation 不属于 Phase 1。

### 6.3 NavMesh

- Cafe floor 是 walkable area。
- Counter、machine、墙体和阻挡路线的家具是 obstacles。
- Customer 与 Barista prefabs 使用 `NavMeshAgent`。
- Cashier 固定在 service point。
- 各 stations 提供位于家具外部的 interaction points。
- Queue 和 Pick-up 使用明确的 slot transforms。
- Setup 或 initialization 时必须用 NavMesh 检查入口、出口、idle point 和 recovery points。

## 7. Placeholder UI

保留 Phase 0 的 Game Speed controls。右上角新增只读状态面板：

```text
CAFE STATUS
Customers: 4 / 5
Counter Queue: 2 / 3
Pick-up: 1 / 2
Spawner: Running

Cashier: Serving · 0.4s
Barista: Preparing #003 · 1.2s

#003 Preparing
#004 Waiting
Completed: 2
```

Customer label 显示 `Queueing` 或 `Waiting #003` 等简短状态。Employee label 显示 `Cashier` 或 `Barista`。Phase 1 不添加 management buttons。

## 8. Failure handling

- 缺少必要 station、prefab、NavMesh 或 reference 时，输出可操作的 Console error，并 disable 受影响的 system。
- Spawn position 无法定位到 NavMesh 时，取消本次生成并稍后重试。
- 正常移动前先检查目标位置和 path 是否有效。
- 第一次 movement timeout 后重新计算一次 path。
- 第二次失败时，尝试在对应 recovery point 的配置半径内寻找最近的合法 NavMesh position，并将角色放到该位置。
- 如果没有可用 recovery position，系统执行可重复调用的 cleanup：
  - 将相关 order 标记为 `Failed`；
  - 释放所有占用或预留的容量；
  - 移除受影响的 customer；
  - 清除 Barista 的无效任务并让其回到 `Idle`。
- Cashier 服务中的 customer 失效时，取消服务并释放预留的 Pick-up slot。
- Barista 的 current order 失效时，清除任务并回到 `Idle`。
- Cleanup 必须是 idempotent，重复调用时不能重复释放 slot 或重复完成 order。
- Tests 中的 expected warnings 必须使用明确的 log expectation。

正常 gameplay 不应触发 recovery。

## 9. Verification

### 9.1 Automated Play Mode tests

现有 16 个 Phase 0 tests 必须继续通过。Phase 1 新增 tests，验证：

1. Order ID 唯一并持续递增。
2. Orders 按 FIFO 被领取。
3. 同一个 order 不能被领取两次。
4. 同一个 order 不能完成两次。
5. Cashier 在 1 秒后完成服务。
6. Barista 在 2 秒后完成制作。
7. Pause 停止收银、制作和移动。
8. `2x` 比 `1x` 更快完成相同 timed behavior。
9. Counter Queue 不超过容量。
10. Pick-up reservations 不超过容量。
11. Customer 总数不超过容量。
12. 容量满时 spawner 暂停。
13. 释放容量后 spawner 恢复。
14. Pick-up 满时 Cashier 等待。
15. Customer 只能领取自己的 order。
16. Movement failure 会正确清理 order 和容量。
17. 多名 customers 可以连续完成完整循环。
18. `MainCafe` 中存在全部必要 Phase 1 references。
19. 正常 integrated loop 中没有未处理 Console error。

Phase 0 与 Phase 1 合计预计至少有 35 个 passing tests。如果某项要求拆分成多个 focused tests 更清楚，最终数量可以更多。

### 9.2 人工 Play Mode 验收

- Bunny customers 按预期的 L-shaped 路线移动。
- Counter Queue 顺序清楚，数量不超过 3。
- Cat Cashier 每次服务约 1 个 scaled second。
- Fox Barista 按 FIFO 领取 orders，每杯 coffee 制作约 2 个 scaled seconds。
- Customers 领取对应 order 后离开。
- 至少连续完成 8 个 orders，不出现重复、丢失或永久卡住。
- 达到容量后暂停生成，释放容量后恢复。
- Pause 停止 cafe loop，但 Camera 和 UI 仍可使用。
- `1x` 和 `2x` 产生正确的速度差异。
- Status UI 与 Scene 中可见状态一致。
- Console 没有未处理 error。

## 10. 计划中的 File changes

### 10.1 Create

- 第 3.1 节列出的 runtime files。
- `Assets/Tests/PlayMode/Phase1PlayModeTests.cs`。
- `Assets/Editor/Phase1SceneSetup.cs`。
- `Docs/Phase1_Beginner_Guide.md`。
- `Docs/superpowers/plans/2026-07-25-phase-1-core-cafe-loop.md`。

### 10.2 Modify

- `Assets/Scenes/MainCafe.unity`。
- `Assets/Editor/AnimalCafe.Editor.asmdef`：添加 `Unity.AI.Navigation` reference，供 NavMesh scene setup tool 使用。
- `Docs/AnimalCafe_Development_Roadmap.md`：
  - 将 Phase 1 的一名 employee 改为两名固定岗位 employees；
  - 继续排除动态多员工工作分配；
  - 只有通过全部 completion gates 后才记录完成证据。

Unity 会为每个新增 asset 和 folder 创建对应 `.meta` file，这些 files 必须与对应 assets 一起保留。

## 11. Implementation stages

1. 建立 Order foundation 和 focused tests。
2. 建立 Capacity service、spawner 和 focused tests。
3. 建立 Stations、可复用 NavMesh movement 和 recovery tests。
4. 完成 Customer state flow。
5. 完成固定岗位 Cashier 和 Barista flows。
6. 完成自动 cafe loop integration tests。
7. 在 `MainCafe` 建立 L-shaped Scene、Kenney models 和 NavMesh。
8. 加入只读 status UI。
9. 运行完整 automated suite，完成人工验收、beginner guide 和 roadmap completion evidence。

每个 stage 通过测试后才能进入下一步。只有 automated 和 manual gates 全部通过后，才能把 Phase 1 标记为 completed。

## 12. Completion gate

Phase 1 只有在以下条件全部满足后才算完成：

- Phase 0 与 Phase 1 的全部 automated tests 通过；
- 人工 Play Mode 验收中至少 8 名 customers 完成完整循环；
- FIFO、capacity、order ownership 和 duplicate prevention 正确；
- Customer、employee 或 order 不会永久卡住；
- Pause、`1x` 和 `2x` 可以正确控制完整 cafe loop；
- Status panel 显示准确的 runtime 状态；
- `MainCafe` 包含全部必要 references，并且 Console 没有未处理 error；
- Beginner guide 和 roadmap completion evidence 已更新。
