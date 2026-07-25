# AnimalCafe Phase 1 — 核心咖啡厅循环 MVP Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: 使用 `superpowers:subagent-driven-development`（推荐）或 `superpowers:executing-plans`，按照 task 顺序逐项实施。所有 steps 使用 checkbox 追踪。

**Goal:** 在 `MainCafe` 中建立“Customer 排队 → Cashier 收银 → Barista 按 FIFO 制作 → Pick-up 取餐 → Customer 离开”的自动外带服务循环。

**Architecture:** Customer、Cashier 和 Barista 使用各自的小型 state machine；`OrderService` 和 `CafeCapacityService` 分别作为 order 与容量规则的唯一 owner。角色移动通过 Unity NavMesh 完成，`Phase1CafeController` 只负责连接 dependencies，Scene 由可重复运行的 Editor setup tool 组装。

**Tech Stack:** Unity `6000.5.5f1`、C#、Unity AI Navigation `2.0.14`、Universal Render Pipeline `17.5.0`、Unity Test Framework `1.7.0`、NUnit、uGUI。

## Global Constraints

- Phase 0 的 16 个 Play Mode tests 必须持续通过。
- 使用一名固定 Cat Cashier 和一名固定 Fox Barista，不实现动态工作分配。
- Customer 使用单一 Bunny prefab，不实现随机 species。
- Counter Queue 容量默认 3，Pick-up 容量默认 2，active customers 默认最多 5。
- Cashier 服务默认 1 scaled second，Barista 制作默认 2 scaled seconds。
- Spawn interval 默认在 3–5 scaled seconds 之间。
- 所有容量、时间与 movement timeout 必须通过 Inspector 配置。
- Pause 停止 movement 与 timed gameplay；`2x` 以双倍 Game Time 推进。
- 不加入 inventory、economy、save、traits、seats、refund 或正式 UI art。
- 新增重要逻辑使用简短中英双语注释。
- 每个 task 完成后先测试和汇报；除非用户明确要求，不执行 `git commit`。

---

## File Structure

### Create

```text
Assets/Scripts/Orders/OrderState.cs
Assets/Scripts/Orders/CafeOrder.cs
Assets/Scripts/Orders/OrderService.cs
Assets/Scripts/Cafe/CafeCapacityService.cs
Assets/Scripts/Cafe/CafeStations.cs
Assets/Scripts/Cafe/CustomerSpawner.cs
Assets/Scripts/Cafe/Phase1CafeController.cs
Assets/Scripts/Characters/CustomerState.cs
Assets/Scripts/Characters/CustomerController.cs
Assets/Scripts/Characters/EmployeeMover.cs
Assets/Scripts/Characters/CashierController.cs
Assets/Scripts/Characters/BaristaController.cs
Assets/Scripts/UI/CafeStatusPanel.cs
Assets/Tests/PlayMode/Phase1PlayModeTests.cs
Assets/Editor/Phase1SceneSetup.cs
Docs/Phase1_Beginner_Guide.md
```

Unity 生成的 folders 与 `.meta` files 必须与对应 assets 一起保留。

### Modify

```text
Assets/Editor/AnimalCafe.Editor.asmdef
Assets/Scenes/MainCafe.unity
Docs/AnimalCafe_Development_Roadmap.md
```

### File responsibilities

- `OrderState.cs`：定义 order states。
- `CafeOrder.cs`：保存不可变 ID、customer ID 与当前 state。
- `OrderService.cs`：创建、FIFO 领取、推进、失败和完成 orders。
- `CafeCapacityService.cs`：原子化 reserve / release counter、Pick-up 和 total capacity。
- `CafeStations.cs`：保存全部 station 与 slot transforms。
- `CustomerSpawner.cs`：按 interval 和容量生成 customers。
- `Phase1CafeController.cs`：验证并连接 services、employees、spawner 和 UI。
- `CustomerState.cs`：定义 customer states。
- `CustomerController.cs`：推进单个 customer 的完整流程。
- `EmployeeMover.cs`：执行 NavMesh movement、timeout、retry 和 recovery。
- `CashierController.cs`：处理 Queue 首位 customer 和 1 秒收银。
- `BaristaController.cs`：领取 FIFO order、制作 2 秒并送到 Pick-up。
- `CafeStatusPanel.cs`：渲染只读 status text。
- `Phase1PlayModeTests.cs`：集中 Phase 1 automated tests。
- `Phase1SceneSetup.cs`：可重复搭建 L-shaped cafe、NavMesh、models 和 UI。
- `Phase1_Beginner_Guide.md`：面向 beginner 的运行与验收指南。

---

### Task 1: Order domain 与 FIFO service

**Files:**

- Create: `Assets/Scripts/Orders/OrderState.cs`
- Create: `Assets/Scripts/Orders/CafeOrder.cs`
- Create: `Assets/Scripts/Orders/OrderService.cs`
- Create: `Assets/Tests/PlayMode/Phase1PlayModeTests.cs`

**Interfaces:**

- Produces: `OrderState`
- Produces: `CafeOrder(int id, int customerId)`
- Produces: `int CafeOrder.Id`
- Produces: `int CafeOrder.CustomerId`
- Produces: `OrderState CafeOrder.State`
- Produces: `CafeOrder OrderService.CreateOrder(int customerId)`
- Produces: `bool OrderService.TryClaimNext(out CafeOrder order)`
- Produces: `bool OrderService.TryTransition(CafeOrder order, OrderState expected, OrderState next)`
- Produces: `bool OrderService.TryFail(CafeOrder order)`
- Produces: `IReadOnlyList<CafeOrder> OrderService.Orders`
- Produces: `int OrderService.CompletedCount`

- [ ] **Step 1: 写出 order states 与 failing tests**

`OrderState.cs` 计划定义：

```csharp
namespace AnimalCafe.Orders
{
    public enum OrderState
    {
        Created,
        Waiting,
        Claimed,
        Preparing,
        ReadyForDelivery,
        AtPickup,
        Collected,
        Completed,
        Failed
    }
}
```

在 `Phase1PlayModeTests.cs` 添加：

```csharp
[Test]
public void Orders_CreateUniqueIncreasingIds()
{
    var service = new OrderService();

    var first = service.CreateOrder(101);
    var second = service.CreateOrder(102);

    Assert.That(second.Id, Is.EqualTo(first.Id + 1));
    Assert.That(first.State, Is.EqualTo(OrderState.Waiting));
}

[Test]
public void Orders_ClaimInFifoOrderAndCannotClaimTwice()
{
    var service = new OrderService();
    var first = service.CreateOrder(101);
    service.CreateOrder(102);

    Assert.That(service.TryClaimNext(out var claimed), Is.True);
    Assert.That(claimed, Is.SameAs(first));
    Assert.That(first.State, Is.EqualTo(OrderState.Claimed));
    Assert.That(
        service.TryTransition(first, OrderState.Waiting, OrderState.Claimed),
        Is.False);
}

[Test]
public void Orders_CannotCompleteTwice()
{
    var service = new OrderService();
    var order = service.CreateOrder(101);
    service.TryClaimNext(out _);
    Assert.That(service.TryTransition(order, OrderState.Claimed, OrderState.Preparing), Is.True);
    Assert.That(service.TryTransition(order, OrderState.Preparing, OrderState.ReadyForDelivery), Is.True);
    Assert.That(service.TryTransition(order, OrderState.ReadyForDelivery, OrderState.AtPickup), Is.True);
    Assert.That(service.TryTransition(order, OrderState.AtPickup, OrderState.Collected), Is.True);
    Assert.That(service.TryTransition(order, OrderState.Collected, OrderState.Completed), Is.True);
    Assert.That(service.TryTransition(order, OrderState.Collected, OrderState.Completed), Is.False);
    Assert.That(service.CompletedCount, Is.EqualTo(1));
}
```

- [ ] **Step 2: 运行 focused tests，确认因为 types 尚不存在而失败**

Run：

```powershell
& 'E:\Unity\Hub\Editor\6000.5.5f1\Editor\Unity.exe' `
  -batchmode -nographics `
  -projectPath 'E:\Unity\Project\AnimalCafe' `
  -runTests -testPlatform PlayMode `
  -testFilter 'AnimalCafe.Tests.PlayMode.Phase1PlayModeTests' `
  -testResults 'E:\Unity\Project\AnimalCafe\Temp\Phase1Task1Results.xml' `
  -logFile 'E:\Unity\Project\AnimalCafe\Temp\Phase1Task1Unity.log'
```

Expected：compilation failure，指出 `OrderService`、`CafeOrder` 或 `OrderState` 尚不存在。

- [ ] **Step 3: 实现最小 order domain**

`CafeOrder` 使用只读 `Id`、`CustomerId`，并提供仅供 `OrderService` 调用的 internal state setter：

```csharp
public sealed class CafeOrder
{
    public CafeOrder(int id, int customerId)
    {
        Id = id;
        CustomerId = customerId;
        State = OrderState.Created;
    }

    public int Id { get; }
    public int CustomerId { get; }
    public OrderState State { get; internal set; }
}
```

`OrderService` 使用 `List<CafeOrder>` 保存历史，使用 `Queue<CafeOrder>` 保存等待队列。`CreateOrder` 将新 order 从 `Created` 立即推进到 `Waiting`；`TryTransition` 必须先验证 service ownership 与 expected state；进入 `Completed` 时只增加一次 `CompletedCount`。

- [ ] **Step 4: 重新运行 focused tests**

Expected：Task 1 的 3 个 tests 全部 PASS，Console 没有 unexpected error。

- [ ] **Step 5: Review checkpoint**

运行 `git diff --check`，汇报新增 files、test count 和结果。不 commit。

---

### Task 2: Capacity service 与 reservation safety

**Files:**

- Create: `Assets/Scripts/Cafe/CafeCapacityService.cs`
- Modify: `Assets/Tests/PlayMode/Phase1PlayModeTests.cs`

**Interfaces:**

- Produces: `CafeCapacityService(int counterCapacity, int pickupCapacity, int totalCapacity)`
- Produces: `bool TryReserveCustomer()`
- Produces: `bool TryReserveCounterSlot(out int slotIndex)`
- Produces: `bool TryReservePickupSlot(out int slotIndex)`
- Produces: `void ReleaseCustomer()`
- Produces: `void ReleaseCounterSlot(int slotIndex)`
- Produces: `void ReleasePickupSlot(int slotIndex)`
- Produces: `int ActiveCustomers`
- Produces: `int CounterUsed`
- Produces: `int PickupUsed`
- Produces: `bool CanSpawn`

- [ ] **Step 1: 添加 capacity failing tests**

```csharp
[Test]
public void Capacity_EnforcesCounterPickupAndTotalLimits()
{
    var capacity = new CafeCapacityService(3, 2, 5);

    Assert.That(capacity.TryReserveCustomer(), Is.True);
    Assert.That(capacity.TryReserveCounterSlot(out var counter0), Is.True);
    Assert.That(capacity.TryReserveCounterSlot(out var counter1), Is.True);
    Assert.That(capacity.TryReserveCounterSlot(out var counter2), Is.True);
    Assert.That(capacity.TryReserveCounterSlot(out _), Is.False);

    Assert.That(capacity.TryReservePickupSlot(out var pickup0), Is.True);
    Assert.That(capacity.TryReservePickupSlot(out var pickup1), Is.True);
    Assert.That(capacity.TryReservePickupSlot(out _), Is.False);

    capacity.ReleaseCounterSlot(counter0);
    capacity.ReleaseCounterSlot(counter0);
    Assert.That(capacity.CounterUsed, Is.EqualTo(2));
    Assert.That(counter1, Is.Not.EqualTo(counter2));
    Assert.That(pickup0, Is.Not.EqualTo(pickup1));
}

[Test]
public void Capacity_ReleaseMakesSpaceAvailableAgain()
{
    var capacity = new CafeCapacityService(1, 1, 1);
    Assert.That(capacity.TryReserveCustomer(), Is.True);
    Assert.That(capacity.TryReserveCounterSlot(out var counter), Is.True);
    Assert.That(capacity.TryReservePickupSlot(out var pickup), Is.True);
    Assert.That(capacity.CanSpawn, Is.False);

    capacity.ReleaseCounterSlot(counter);
    capacity.ReleasePickupSlot(pickup);
    capacity.ReleaseCustomer();

    Assert.That(capacity.CanSpawn, Is.True);
}
```

- [ ] **Step 2: 运行 tests，确认失败**

Expected：`CafeCapacityService` 尚不存在。

- [ ] **Step 3: 实现固定容量与 idempotent release**

使用两个 `bool[]` 管理 slot reservations。Invalid index 或重复 release 不抛 exception、不改变计数，并输出一次包含 service 名称与 index 的 warning；对应 warning test 使用 `LogAssert.Expect`。

- [ ] **Step 4: 运行 Task 1–2 tests**

Expected：全部 PASS；重复 release 不会造成负数。

- [ ] **Step 5: Review checkpoint**

运行 `git diff --check`，汇报 capacity API 和 test result。不 commit。

---

### Task 3: Stations 与 NavMesh movement recovery

**Files:**

- Create: `Assets/Scripts/Cafe/CafeStations.cs`
- Create: `Assets/Scripts/Characters/EmployeeMover.cs`
- Modify: `Assets/Tests/PlayMode/Phase1PlayModeTests.cs`

**Interfaces:**

- Produces: `Transform CafeStations.Entry`
- Produces: `IReadOnlyList<Transform> CafeStations.CounterSlots`
- Produces: `Transform CafeStations.CashierServicePoint`
- Produces: `Transform CafeStations.MachinePoint`
- Produces: `IReadOnlyList<Transform> CafeStations.PickupSlots`
- Produces: `Transform CafeStations.BaristaIdlePoint`
- Produces: `Transform CafeStations.CustomerRecoveryPoint`
- Produces: `Transform CafeStations.BaristaRecoveryPoint`
- Produces: `Transform CafeStations.Exit`
- Produces: `bool CafeStations.ValidateConfiguration(out string error)`
- Produces: `IEnumerator EmployeeMover.MoveTo(Vector3 target, Action<bool> completed)`
- Produces: `void EmployeeMover.Configure(NavMeshAgent agent, float timeoutSeconds, Transform recoveryPoint, float recoveryRadius)`

- [ ] **Step 1: 添加 station validation 与 movement failure tests**

```csharp
[Test]
public void Stations_RejectMissingRequiredReferences()
{
    var stationsObject = new GameObject("Stations");
    var stations = stationsObject.AddComponent<CafeStations>();

    Assert.That(stations.ValidateConfiguration(out var error), Is.False);
    StringAssert.Contains("Entry", error);

    Object.DestroyImmediate(stationsObject);
}

[UnityTest]
public IEnumerator EmployeeMover_InvalidTargetReportsFailureOnce()
{
    var actor = new GameObject("Mover");
    var agent = actor.AddComponent<UnityEngine.AI.NavMeshAgent>();
    var recovery = new GameObject("Recovery").transform;
    var mover = actor.AddComponent<EmployeeMover>();
    mover.Configure(agent, 0.05f, recovery, 0.5f);
    var callbackCount = 0;
    var success = true;

    yield return mover.MoveTo(
        new Vector3(float.PositiveInfinity, 0f, 0f),
        result =>
        {
            callbackCount++;
            success = result;
        });

    Assert.That(success, Is.False);
    Assert.That(callbackCount, Is.EqualTo(1));
    Object.DestroyImmediate(recovery.gameObject);
    Object.DestroyImmediate(actor);
}
```

- [ ] **Step 2: 运行 focused tests，确认失败**

Expected：`CafeStations` 与 `EmployeeMover` 尚不存在。

- [ ] **Step 3: 实现 configuration validation**

`ValidateConfiguration` 按固定顺序检查 Entry、3 个 Counter slots、Cashier point、Machine、2 个 Pick-up slots、Barista idle、两个 recovery points 与 Exit，并返回第一条清楚的 error。

- [ ] **Step 4: 实现 movement coroutine**

`MoveTo` 必须：

1. 拒绝 `NaN` / `Infinity` target；
2. 用 `NavMesh.SamplePosition` 检查 target；
3. 设置 destination；
4. 用 scaled `Time.deltaTime` 累计 timeout；
5. timeout 后 reset path 并 retry 一次；
6. 第二次失败后在 recovery radius 内 sample recovery point；
7. 可恢复时调用 `agent.Warp`；
8. 最终只调用一次 `completed(bool)`。

- [ ] **Step 5: 运行 Task 1–3 tests**

Expected：全部 PASS；Pause 下 `Time.deltaTime == 0`，timeout 不推进。

- [ ] **Step 6: Review checkpoint**

检查 coroutine 在 destroyed agent、disabled agent 和 invalid NavMesh target 时都能安全返回。不 commit。

---

### Task 4: Customer queue flow 与 fixed Cashier

**Files:**

- Create: `Assets/Scripts/Characters/CustomerState.cs`
- Create: `Assets/Scripts/Characters/CustomerController.cs`
- Create: `Assets/Scripts/Characters/CashierController.cs`
- Modify: `Assets/Tests/PlayMode/Phase1PlayModeTests.cs`

**Interfaces:**

- Produces: `CustomerState`
- Produces: `int CustomerController.CustomerId`
- Produces: `CustomerState CustomerController.State`
- Produces: `CafeOrder CustomerController.Order`
- Produces: `int CustomerController.CounterSlotIndex`
- Produces: `int CustomerController.PickupSlotIndex`
- Produces: `void CustomerController.Configure(int customerId, CafeCapacityService capacity, CafeStations stations, NavMeshAgent agent)`
- Produces: `void CustomerController.AssignOrder(CafeOrder order, int pickupSlotIndex)`
- Produces: `bool CustomerController.TryCollect(CafeOrder order)`
- Produces: `void CustomerController.BeginLeaving()`
- Produces: `void CustomerController.FailAndCleanup()`
- Produces: `void CashierController.Configure(OrderService orders, CafeCapacityService capacity, float serviceDuration)`
- Produces: `void CashierController.Enqueue(CustomerController customer)`
- Produces: `CustomerController CashierController.CurrentCustomer`

- [ ] **Step 1: 定义 CustomerState**

```csharp
public enum CustomerState
{
    Entering,
    Queueing,
    Ordering,
    MovingToPickup,
    WaitingForOrder,
    Collecting,
    Leaving,
    Completed,
    Recovering
}
```

- [ ] **Step 2: 添加 Cashier timing 与 Pick-up gate tests**

```csharp
[UnityTest]
public IEnumerator Cashier_CreatesOrderAfterOneScaledSecond()
{
    var fixture = CreateCashierFixture(serviceDuration: 1f);
    fixture.Cashier.Enqueue(fixture.Customer);

    yield return new WaitForSeconds(0.9f);
    Assert.That(fixture.Orders.Orders.Count, Is.Zero);
    yield return new WaitForSeconds(0.2f);

    Assert.That(fixture.Orders.Orders.Count, Is.EqualTo(1));
    Assert.That(fixture.Customer.Order.CustomerId, Is.EqualTo(fixture.Customer.CustomerId));
    fixture.Dispose();
}

[UnityTest]
public IEnumerator Cashier_WaitsWhenPickupIsFull()
{
    var fixture = CreateCashierFixture(serviceDuration: 0.05f, pickupCapacity: 1);
    Assert.That(fixture.Capacity.TryReservePickupSlot(out _), Is.True);
    fixture.Cashier.Enqueue(fixture.Customer);

    yield return new WaitForSeconds(0.1f);

    Assert.That(fixture.Orders.Orders.Count, Is.Zero);
    Assert.That(fixture.Customer.State, Is.EqualTo(CustomerState.Queueing));
    fixture.Dispose();
}
```

Fixture helper 必须创建真实 `GameObject` components，并在 `Dispose` 中恢复 `Time.timeScale`、销毁 objects。

- [ ] **Step 3: 运行 tests，确认失败**

Expected：customer 与 Cashier types 尚不存在。

- [ ] **Step 4: 实现最小 customer ownership 与 cleanup**

Customer 必须保存三个 independent ownership flags：total customer、counter slot、Pick-up slot。`FailAndCleanup()` 通过 flags 确保重复调用不会重复释放。

`TryCollect` 只有在以下条件全部满足时返回 true：

- 传入的 order 与 `CustomerController.Order` 是同一个 object；
- `order.CustomerId == CustomerId`；
- order 当前为 `AtPickup`；
- `OrderService.TryTransition(order, AtPickup, Collected)` 成功。

成功后 customer 进入 `Collecting`，释放 Pick-up slot，再进入 `Leaving`。其他情况返回 false，不改变 order 或容量。

- [ ] **Step 5: 实现 Cashier coroutine**

Cashier 只处理 Queue 首位。开始计时前调用 `TryReservePickupSlot`；成功后 customer 进入 `Ordering`。使用 `WaitForSeconds(serviceDuration)`；完成后创建 order、调用 `AssignOrder`，并释放 customer 的 counter slot。

- [ ] **Step 6: 添加 Pause test**

```csharp
[UnityTest]
public IEnumerator Cashier_PauseStopsServiceTimer()
{
    var fixture = CreateCashierFixture(serviceDuration: 0.1f);
    fixture.Cashier.Enqueue(fixture.Customer);
    Time.timeScale = 0f;
    yield return new WaitForSecondsRealtime(0.15f);
    Assert.That(fixture.Orders.Orders.Count, Is.Zero);

    Time.timeScale = 1f;
    yield return new WaitForSeconds(0.12f);
    Assert.That(fixture.Orders.Orders.Count, Is.EqualTo(1));
    fixture.Dispose();
}
```

- [ ] **Step 7: 运行 Task 1–4 tests**

Expected：全部 PASS。

- [ ] **Step 8: Review checkpoint**

报告 customer states、slot ownership 与 Cashier timing。不 commit。

---

### Task 5: Fixed Barista、制作与 order delivery

**Files:**

- Create: `Assets/Scripts/Characters/BaristaController.cs`
- Modify: `Assets/Tests/PlayMode/Phase1PlayModeTests.cs`

**Interfaces:**

- Produces: `void BaristaController.Configure(OrderService orders, EmployeeMover mover, CafeStations stations, float preparationDuration)`
- Produces: `CafeOrder BaristaController.CurrentOrder`
- Produces: `string BaristaController.StatusText`
- Consumes: Task 1 `OrderService`
- Consumes: Task 3 `EmployeeMover` 与 `CafeStations`
- Consumes: Task 4 `CustomerController`

- [ ] **Step 1: 添加 FIFO、2 秒制作和 ownership tests**

```csharp
[UnityTest]
public IEnumerator Barista_ClaimsOldestOrderAndPreparesForConfiguredDuration()
{
    var fixture = CreateBaristaFixture(preparationDuration: 0.2f);
    var first = fixture.Orders.CreateOrder(101);
    var second = fixture.Orders.CreateOrder(102);

    yield return null;
    Assert.That(fixture.Barista.CurrentOrder, Is.SameAs(first));
    yield return new WaitForSeconds(0.1f);
    Assert.That(first.State, Is.EqualTo(OrderState.Preparing));
    yield return new WaitForSeconds(0.15f);
    Assert.That((int)first.State, Is.GreaterThanOrEqualTo((int)OrderState.ReadyForDelivery));
    Assert.That(second.State, Is.EqualTo(OrderState.Waiting));
    fixture.Dispose();
}

[Test]
public void Customer_CannotCollectAnotherCustomersOrder()
{
    var fixture = CreateCustomerOwnershipFixture();
    var ownOrder = fixture.Orders.CreateOrder(fixture.First.CustomerId);
    var otherOrder = fixture.Orders.CreateOrder(fixture.Second.CustomerId);

    Assert.That(fixture.First.TryCollect(otherOrder), Is.False);
    Assert.That(fixture.First.TryCollect(ownOrder), Is.True);
    fixture.Dispose();
}
```

- [ ] **Step 2: 运行 tests，确认失败**

Expected：`BaristaController` 或 `TryCollect` 尚不存在。

- [ ] **Step 3: 实现 Barista state loop**

Barista 在 `Idle` 时调用 `TryClaimNext`。成功后：

1. Move to machine；
2. `Claimed → Preparing`；
3. `yield return new WaitForSeconds(preparationDuration)`；
4. `Preparing → ReadyForDelivery`；
5. Move to order customer 的 Pick-up slot；
6. `ReadyForDelivery → AtPickup`；
7. 通知对应 customer；
8. Move to idle；
9. 清空 `CurrentOrder`。

Movement failure 调用 `TryFail`，通知 customer `FailAndCleanup()`，然后清除任务。

- [ ] **Step 4: 添加制作 Pause / 2x test**

分别在 `Time.timeScale = 0f` 与 `2f` 下运行短 duration，使用 `WaitForSecondsRealtime` 比较 state。测试结束必须恢复 `Time.timeScale = 1f`。

- [ ] **Step 5: 运行 Task 1–5 tests**

Expected：FIFO、timing、Pause、2x 和 ownership tests 全部 PASS。

- [ ] **Step 6: Review checkpoint**

确认 Barista 一次只持有一个 order，movement failure 不会继续 delivery。不 commit。

---

### Task 6: Customer spawner 与完整 loop coordinator

**Files:**

- Create: `Assets/Scripts/Cafe/CustomerSpawner.cs`
- Create: `Assets/Scripts/Cafe/Phase1CafeController.cs`
- Modify: `Assets/Tests/PlayMode/Phase1PlayModeTests.cs`

**Interfaces:**

- Produces: `void CustomerSpawner.Configure(CustomerController prefab, CafeCapacityService capacity, CafeStations stations, float minInterval, float maxInterval)`
- Produces: `bool CustomerSpawner.IsPausedByCapacity`
- Produces: `int CustomerSpawner.SpawnedCount`
- Produces: `void Phase1CafeController.Configure(OrderService orders, CafeCapacityService capacity, CafeStations stations, CustomerSpawner spawner, CashierController cashier, BaristaController barista)`
- Produces: `bool Phase1CafeController.ValidateConfiguration(out string error)`

- [ ] **Step 1: 添加 spawner capacity tests**

```csharp
[UnityTest]
public IEnumerator Spawner_PausesAtCapacityAndResumesAfterRelease()
{
    var fixture = CreateSpawnerFixture(
        counterCapacity: 1,
        pickupCapacity: 1,
        totalCapacity: 1,
        interval: 0.05f);

    yield return new WaitForSeconds(0.07f);
    Assert.That(fixture.Spawner.SpawnedCount, Is.EqualTo(1));
    Assert.That(fixture.Spawner.IsPausedByCapacity, Is.True);

    fixture.FirstCustomer.FailAndCleanup();
    yield return new WaitForSeconds(0.07f);

    Assert.That(fixture.Spawner.SpawnedCount, Is.EqualTo(2));
    fixture.Dispose();
}
```

- [ ] **Step 2: 添加 controller validation test**

创建缺失 Barista 的 controller，断言 `ValidateConfiguration` 返回 false，error 包含 `Barista`。

- [ ] **Step 3: 运行 tests，确认失败**

Expected：spawner 与 controller types 尚不存在。

- [ ] **Step 4: 实现 deterministic test hook**

Production interval 使用 `UnityEngine.Random.Range(minInterval, maxInterval)`。为 tests 提供 `Configure` 传入相同 min/max，从而得到固定 interval。每次 spawn 前按顺序检查 total、counter、Pick-up capacity。

- [ ] **Step 5: 实现 coordinator validation**

`Phase1CafeController.Awake` 验证 references；失败时输出 `[Phase1CafeController] Missing required reference: <name>.` 并 disable 自身。成功时连接 spawner、Cashier 和 Barista。

- [ ] **Step 6: 添加 integrated loop test**

使用缩短后的 `0.02f` 收银、`0.03f` 制作和 `0.04f` spawn interval，运行直到 3 个 orders completed 或 3 秒 real-time safety limit。断言：

```csharp
Assert.That(fixture.Orders.CompletedCount, Is.GreaterThanOrEqualTo(3));
Assert.That(fixture.Capacity.ActiveCustomers, Is.LessThanOrEqualTo(5));
Assert.That(fixture.Capacity.CounterUsed, Is.LessThanOrEqualTo(3));
Assert.That(fixture.Capacity.PickupUsed, Is.LessThanOrEqualTo(2));
Assert.That(
    fixture.Orders.Orders.Select(order => order.Id).Distinct().Count(),
    Is.EqualTo(fixture.Orders.Orders.Count));
```

- [ ] **Step 7: 运行 Task 1–6 tests**

Expected：domain 与 integrated tests 全部 PASS。

- [ ] **Step 8: Review checkpoint**

检查 spawner 不会 partial spawn，controller 不包含角色内部 state logic。不 commit。

---

### Task 7: L-shaped MainCafe Scene、Kenney models 与 NavMesh

**Files:**

- Create: `Assets/Editor/Phase1SceneSetup.cs`
- Modify: `Assets/Editor/AnimalCafe.Editor.asmdef`
- Modify through Unity Editor API: `Assets/Scenes/MainCafe.unity`
- Modify: `Assets/Tests/PlayMode/Phase1PlayModeTests.cs`

**Interfaces:**

- Produces menu: `AnimalCafe/Phase 1/Configure Scene`
- Produces roots:
  - `Phase1_Runtime`
  - `Phase1_Cafe`
  - `Phase1_Characters`
  - `Phase1_UI`
- Produces required objects:
  - `CafeStations`
  - `Cashier_Cat`
  - `Barista_Fox`
  - `Customer_Bunny_PrefabSource`
  - `CoffeeMachine`
  - `CounterQueue_0..2`
  - `PickupSlot_0..1`
  - `CafeStatusPanel`

- [ ] **Step 1: 更新 Editor assembly reference**

在 `Assets/Editor/AnimalCafe.Editor.asmdef` 的 `references` 中加入：

```json
"Unity.AI.Navigation"
```

- [ ] **Step 2: 写 Scene structure failing test**

```csharp
[UnityTest]
public IEnumerator MainCafe_LoadsWithRequiredPhase1Objects()
{
    yield return SceneManager.LoadSceneAsync("MainCafe");
    yield return null;

    Assert.That(GameObject.Find("Phase1_Runtime"), Is.Not.Null);
    Assert.That(GameObject.Find("Phase1_Cafe"), Is.Not.Null);
    Assert.That(GameObject.Find("Cashier_Cat"), Is.Not.Null);
    Assert.That(GameObject.Find("Barista_Fox"), Is.Not.Null);
    Assert.That(GameObject.Find("CafeStatusPanel"), Is.Not.Null);
    Assert.That(Object.FindFirstObjectByType<CafeStations>(), Is.Not.Null);
    Assert.That(Object.FindFirstObjectByType<Phase1CafeController>(), Is.Not.Null);
}
```

- [ ] **Step 3: 运行 Scene test，确认失败**

Expected：找不到 `Phase1_Runtime`。

- [ ] **Step 4: 编写 idempotent Phase1SceneSetup**

沿用 `Phase0SceneSetup` 的 helpers 和风格：

- 打开 `Assets/Scenes/MainCafe.unity`；
- `FindOrCreateRoot`，不重复创建 root；
- 删除并重建 Phase 1 自有 children，不改动 Phase 0 roots；
- 建立 L-shaped floor、counter、machine 与标记点；
- 从 Kenney paths 加载 Cat、Fox 和 Bunny FBX；
- 找不到指定 FBX 时抛出包含完整 asset path 的 `InvalidOperationException`；
- 配置 `NavMeshSurface`；
- 通过 `NavMeshSurface.BuildNavMesh()` 生成 Scene NavMesh；
- 保存 Scene 和 assets。

使用已存在的 models：

```text
Assets/Models/Kenney/CubePets/FBX format/animal-cat.fbx
Assets/Models/Kenney/CubePets/FBX format/animal-fox.fbx
Assets/Models/Kenney/CubePets/FBX format/animal-bunny.fbx
```

- [ ] **Step 5: 运行 setup tool**

通过 Unity batch mode 调用一个 public static setup entry 或在 Editor 中选择：

```text
AnimalCafe → Phase 1 → Configure Scene
```

Expected Console：`[Phase1SceneSetup] MainCafe configured successfully.`

- [ ] **Step 6: 运行 Phase 0 + Phase 1 Scene tests**

Expected：两个 phases 的 required objects 都存在，Scene load 无 error。

- [ ] **Step 7: 人工检查 L-shaped layout**

打开 `MainCafe`，确认 Queue 在下方、machine 在上方、Pick-up 在右侧、NavMesh walkable area 不穿过 counter。

- [ ] **Step 8: Review checkpoint**

汇报 Scene objects、使用的 Kenney assets 和 NavMesh bake 结果。不 commit。

---

### Task 8: Status UI 与 runtime labels

**Files:**

- Create: `Assets/Scripts/UI/CafeStatusPanel.cs`
- Modify: `Assets/Editor/Phase1SceneSetup.cs`
- Modify through setup tool: `Assets/Scenes/MainCafe.unity`
- Modify: `Assets/Tests/PlayMode/Phase1PlayModeTests.cs`

**Interfaces:**

- Produces: `void CafeStatusPanel.Configure(OrderService orders, CafeCapacityService capacity, CustomerSpawner spawner, CashierController cashier, BaristaController barista, Text statusText)`
- Produces: `string CafeStatusPanel.BuildStatusText()`

- [ ] **Step 1: 写 status text failing test**

```csharp
[Test]
public void StatusPanel_ReportsCapacityEmployeesAndOrders()
{
    var fixture = CreateStatusFixture();
    var text = fixture.Panel.BuildStatusText();

    StringAssert.Contains("Customers: 0 / 5", text);
    StringAssert.Contains("Counter Queue: 0 / 3", text);
    StringAssert.Contains("Pick-up: 0 / 2", text);
    StringAssert.Contains("Spawner: Running", text);
    StringAssert.Contains("Cashier: Idle", text);
    StringAssert.Contains("Barista: Idle", text);
    StringAssert.Contains("Completed: 0", text);
    fixture.Dispose();
}
```

- [ ] **Step 2: 运行 test，确认失败**

Expected：`CafeStatusPanel` 尚不存在。

- [ ] **Step 3: 实现纯文本 builder**

`BuildStatusText()` 不查询 Scene objects；只读取注入的 services/controllers。`Update()` 每 `0.1f` unscaled seconds 更新一次 `Text`，因此 Pause 时仍可刷新 UI。

- [ ] **Step 4: 在 setup tool 中创建右上角 panel**

使用 `CanvasScaler.ScaleWithScreenSize`，reference resolution `1024 x 768`。Panel anchor 为右上角，size `360 x 260`，使用 `LegacyRuntime.ttf`，深色半透明背景和白色文字。

- [ ] **Step 5: 添加 customer 与 employee labels**

使用 world-space Canvas 或简单 child `TextMesh`；只显示 role 和简短 state，不加入点击行为。

- [ ] **Step 6: 运行 UI test 与 Scene test**

Expected：status text 正确；`CafeStatusPanel` reference 完整；Phase 0 time controls 仍存在。

- [ ] **Step 7: Review checkpoint**

人工检查 1024×768 与当前 Game view 下 UI 不遮挡 Phase 0 controls。不 commit。

---

### Task 9: Full verification、Beginner Guide 与 Roadmap

**Files:**

- Create: `Docs/Phase1_Beginner_Guide.md`
- Modify: `Docs/AnimalCafe_Development_Roadmap.md`
- Verify: all files created or modified in Tasks 1–8

**Interfaces:**

- Consumes: 完整 Phase 1 runtime、Scene、tests 和 UI。
- Produces: 可重复的 beginner 验收说明与准确 completion evidence。

- [ ] **Step 1: 运行全部 Play Mode tests**

Run：

```powershell
& 'E:\Unity\Hub\Editor\6000.5.5f1\Editor\Unity.exe' `
  -batchmode -nographics `
  -projectPath 'E:\Unity\Project\AnimalCafe' `
  -runTests -testPlatform PlayMode `
  -testResults 'E:\Unity\Project\AnimalCafe\Temp\Phase1FullResults.xml' `
  -logFile 'E:\Unity\Project\AnimalCafe\Temp\Phase1FullUnity.log'
```

Expected：

- Phase 0 的 16 tests 全部 PASS；
- Phase 0 + Phase 1 合计至少 35 tests PASS；
- failed、skipped、inconclusive 均为 0；
- Unity exit code 为 0。

- [ ] **Step 2: 检查 Console 与 test log**

搜索 `Error CS`、`NullReferenceException`、`MissingReferenceException`、`Unhandled`。Expected：无未处理 error。Expected warning 必须只出现在对应 `LogAssert.Expect` tests。

- [ ] **Step 3: 完成人工 Play Mode checklist**

在 `MainCafe` 验证：

1. Bunny customers 使用 L-shaped route；
2. Queue 最多 3 位；
3. Cashier 收银约 1 scaled second；
4. Barista 按 FIFO 制作，每杯约 2 scaled seconds；
5. Pick-up 最多 2 位；
6. 至少连续完成 8 个 orders；
7. 无重复、丢单或永久卡住；
8. 容量满时暂停 spawn，释放后恢复；
9. Pause 停止 cafe loop；
10. Pause 时 Camera 和 UI 仍可操作；
11. `2x` 明显快于 `1x`；
12. Status UI 与 Scene 状态一致；
13. Console 无未处理 error。

- [ ] **Step 4: 编写 beginner guide**

`Docs/Phase1_Beginner_Guide.md` 必须包含：

- Phase 1 做了什么和没有做什么；
- 如何打开 `MainCafe`；
- 如何识别 Cat Cashier、Fox Barista 和 Bunny customers；
- order 与 character states 的中文解释；
- 如何调整 1 秒、2 秒、3 / 2 / 5 和 spawn interval；
- 如何查看 NavMesh；
- 如何运行全部 Play Mode tests；
- 如何阅读 status UI；
- 遇到红色 Console error 时需要复制哪些信息；
- 不要在 Play Mode 中把临时 Inspector 修改当成正式保存。

- [ ] **Step 5: 更新 Roadmap 范围**

将 Phase 1 的“一名 employee”改为：

```text
- 两名固定岗位 employee：一名 Cashier 和一名 Barista。
- Phase 1 不包含换岗、排班或动态多员工工作分配。
```

- [ ] **Step 6: 只有验收通过后更新完成证据**

在 Phase 1 标题下记录：

- 状态 `Completed`；
- 完成日期；
- Unity version `6000.5.5f1`；
- automated test 总数；
- 8-order manual loop 结果；
- Console error scan 结果。

任何 gate 未通过时，Phase 1 保持未完成状态，并在交付说明中列出未通过项。

- [ ] **Step 7: 最终 workspace 检查**

Run：

```powershell
git diff --check
git status --short
git diff --stat
```

确认：

- 没有修改无关 files；
- `.meta` 与对应 assets 同时存在；
- `.superpowers/` Visual Companion 临时目录不进入正式改动；
- 没有 secrets 或 machine-specific paths 写入 tracked files。

- [ ] **Step 8: Final review checkpoint**

向用户汇报：

- 创建和修改了哪些 files；
- automated tests 的精确结果；
- 哪些人工 checks 已完成；
- 用户如何在 Unity 中重放 demo；
- 当前 changes 未 commit，等待用户明确指示。
