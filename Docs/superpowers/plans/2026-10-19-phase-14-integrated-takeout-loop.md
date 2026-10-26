# Phase 14 — Integrated Takeout Cafe Loop Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task; if Owner explicitly chooses specialist execution, use superpowers:subagent-driven-development. Steps use checkbox (`- [ ]`) syntax for tracking. 按项目约定采用 Task focused、Phase 集中 review/manual，不增加每 Task 的完整验收。

**状态：** 上一轮拥堵补修Edit256／Play164与窄review已通过；Owner于2026-10-23继续批准自然单列优先、P14收银1秒／制作3秒／来客1～5秒及多人布局测试，现按原Task9追加段实施。旧P13独立收银2秒保持；旧源证据不转记为新源PASS，旧三项visual Owner PASS保留。Phase尚未Completed，未进入下一Phase。

**Goal:** 在有效布局中自动完成至少20单真实外带服务，并正确处理取餐FIFO、受阻恢复和装修后的选择性清场。

**Architecture:** 新增一个轻量 Cafe Loop service 管订单关联及业务提交，一个 Scene controller 统一驱动现有 Customer/Employee。沿用 OrderService、CapacityService、NavigationWorld 与原岗位动画；新增 Pick-up 队伍和 Order-aware cup projection。旧P12/P13独立受控模式保留，P14模式只允许一个业务驱动入口。

**Tech Stack:** Unity `6000.5.5f1`、C#、AI Navigation `2.0.14`、Unity Test Framework `1.7.0`/NUnit、UGUI；版本已从本worktree读取，不安装/升级dependency。

**Spec:** [Reviewed Design](../specs/2026-10-19-phase-14-integrated-takeout-loop-design.md)；[完整Test Cases和Manual步骤](../specs/2026-10-19-phase-14-integrated-takeout-loop-test-cases.md)。执行者必须同时阅读两份文件。

## Global Constraints

- 工作区：`E:/Unity/Project/AnimalCafe/.worktrees/phase-14-integrated-takeout-loop`；branch：`codex/phase-14-integrated-takeout-loop`；基线：`8c1582f7388191ff155d63b824b69c10d47cff3c`。不写main或旧worktree。
- 两个固定岗位；每Visit一单一杯基础Coffee，无Inventory。Owner于2026-10-23批准P14 Cashier改为1游戏秒，Barista仍制作3游戏秒；旧P13独立默认Cashier2秒不改。实际回CM之前不能领下一单，来客随机1～5秒保持。
- 付款只有业务状态，无Coins/price/Revenue/资金transaction/refund；无营业日、timeout取消、优先插单、Save或新增拿杯/饮用耗时。Owner于2026-10-20批准顾客领取后复用既有Carry动画持杯离场。
- Counter/Pick-up均FIFO，只有正确且实际到位的队首顾客可取自己的Ready Order；Order Completed与实际离店分开。
- 桌面最多显示3杯，空位后补显示；真实delivery不限于3，不能创建隐藏Cup对象充当库存。
- 保持P10 owner/token与实际离位/出口释放合同；Blocked/disable不是离店；不能Reset Capacity抹掉保留离场者。
- 有修改的正常Done清除未取餐顾客、全部旧Order及杯子；已实际取餐的离场者（含Blocked/disable-held）和原两员工保留。无修改/Cancel不清场。
- Done在PrepareExit前持有P14门禁并清场；清场一致、version发布且路径验证完成前不恢复。主动Pause不被Done覆盖。
- Pause时不推进正常收费/制作/交付/领取/生成；允许明确的生命周期清场。所有计时只消费已缩放delta一次。
- NavigationWorld仍是唯一位移执行者；不瞬移、不穿墙/人/家具，不放宽角色尺寸、CollisionSkin、到站容差或P11恢复预算。
- Scene/Prefab由Unity Editor API/CLI生成，不手写YAML；不保存Play中的Scene，不覆盖用户dirty Scene。新C#对应meta由Unity正常导入。
- Task只运行focused和直接regression，Phase末集中完整suite、独立Engineering/QA、必要视觉review和Owner manual。无新增Art/Audio production。
- 不执行commit/push/PR/merge、删除branch/worktree、CI dispatch、安装或发布；不新增全局event bus、singleton、通用任务框架或额外asmdef。

## Review Focus

1. **Done先恢复后发布version：** 必须在PrepareExit前拿到block并退休旧epoch；同步callback也不能越过。Task7：E-033、P-021。
2. **业务提交后外部视觉失败：** Ready/Completed不可回滚或重复；Complete拒绝不得先丢delivery。Task5：E-034–035、P-022。
3. **cleanup后已制作事实丢失：** 保存checkpoint后才能退休runner；恢复已制作的Coffee只续送，不再工作3秒。Task4：E-024–026、038、P-024。
4. **共享anchor自锁/重复占位：** 队首在合法wait位，service lease互斥；Hold Collection也不能挡后续送杯。Task2/3：E-015、037、039、P-005/024。
5. **清空Order后旧顾客仍在离场：** 保存与Order store独立的已取餐事实与Total token；换epoch/缩容/重复Done都不能删除或提前释放。Task6/7：E-029–031、P-013–015。

## 1. 文件结构与责任

以下是批准的实施文件清单；实际完成状态见逐Task复选框和执行记录。每个Task先RED再最小实现；如实际实现需要扩大清单或改变已确认规则，先说明影响。

| 路径 | 动作 / 单一责任 |
| --- | --- |
| `Assets/Scripts/CafeLoop/CafeLoopContracts.cs` | 新建：业务keys、checkpoint、event、readonly snapshots和小型port |
| `Assets/Scripts/CafeLoop/CafeLoopService.cs` | 新建：Order关联、一次性付款/送达/领取、epoch和协调调度 |
| `Assets/Scripts/CafeLoop/CafeDeliveryStore.cs` | 新建：逻辑已交付未领取记录；仅service写入，display只读 |
| `Assets/Scripts/CafeLoop/CafeLoopController.cs` | 新建：唯一业务Step、ports适配、GameTime/Decor/Navigation接线 |
| `Assets/Scripts/CafeLoop/CafeLoopValidationController.cs` | 新建：明确标记的Inspector验证菜单，不构成第二业务写入口 |
| `Assets/Scripts/Customers/PickupQueueService.cs` | 新建：真实取餐站位分配、FIFO和共享服务点lease |
| `Assets/Scripts/CafeLoop/CafeLoopStatusView.cs` | 新建：只读状态显示；不处理付款/派单/取餐；实施时与同域readonly view snapshot放在CafeLoop目录 |
| `Assets/Editor/Phase14/Phase14CafeLoopSceneSetup.cs` | 新建：幂等安装MainCafe、生成宽敞独立fixture和只读UI |
| `Assets/Scripts/Customers/CustomerFlowController.cs`、`CustomerContracts.cs`、`CustomerAdmission.cs` | 修改：显式P14模式、实际Pick-up阶段与token事件；旧受控模式保持 |
| `Assets/Scripts/Customers/CounterQueuePlanner.cs`、`CounterQueueService.cs`、`CustomerRoutePlanner.cs` | 优先复用；仅在当前API不足时作兼容扩展，不重写原算法/预算 |
| `Assets/Scripts/Employees/EmployeeFlowController.cs`、`EmployeeTaskRunner.cs`、`EmployeeContracts.cs` | 修改：Order上下文、checkpoint、事件与恢复；原TryStart受控模式保留 |
| `Assets/Scripts/Employees/PickupCupDisplay.cs` | 修改：接受readonly Order投影、精确移除/补显示，兼容旧TryDeliver |
| `Assets/Scripts/Decoration/DecorationModeController.cs` | 修改：正常含修改Done的PrepareExit前可选通知，不重写装修行为 |
| `Assets/Scripts/Core/Time/GameTimeService.cs` | Task7兼容观测扩展：只读LastExplicitSpeed，区分主动Pause与自动布局暂停；不改变计时/切速规则 |
| `Assets/Tests/EditMode/Phase14/` | 新建下列Task点名的focused tests和共用test support |
| `Assets/Tests/PlayMode/EditorSceneLoading/Phase14/` | 新建下列Task点名的真实Scene/Navigation tests，复用现有asmdef |
| `Assets/Scenes/MainCafe.unity` | 最终由P14 installer安装；保留现有布局和资源，非代码阶段手动编辑目标 |
| `Assets/Scenes/Validation/Phase14CafeLoop.unity`、`Phase14SharedPickup.unity` | 新建：默认分离anchors与共享anchor两份独立fixture，均须真实五杯验证 |
| `Docs/Phase14_Beginner_Guide.md`、`Docs/Phase14_Validation_Report.md` | Phase收尾新建：真实入口、执行结果/缺陷与Owner记录，不预填PASS |
| `Docs/AnimalCafe_Project_Design.md`、`Docs/AnimalCafe_Development_Roadmap.md` | 实施时只同步已确认P14规则及真实状态，不将阶段提前标Completed |

不计划修改OrderService、CapacityService或NavigationWorld的公共规则；若发现必须改动，先说明触发条件、兼容影响和直接regression。P14 installer不调用旧P11/P12/P13 builder重建已接受场景；独立P13验证Scene不改。

## 2. 共享接口与执行约定

本节定义跨Task必须一致的最小接口，方法体由对应Task实现。类型放在`AnimalCafe.CafeLoop`，取餐队伍类型放`AnimalCafe.Customers`；保持现有runtime asmdef。

### 2.1 Keys、checkpoint与snapshot

- `readonly struct CafeOrderKey(string sessionId, long epoch, long orderId)`：readonly `SessionId/Epoch/OrderId`；ordinal值相等，三项均有效；epoch/orderId为正，不回绕。
- `readonly struct CafeTaskToken(string sessionId, long epoch, long sequence, int attempt)`：readonly对应属性；一次业务操作可恢复为新attempt，但旧attempt不能提交；sequence递增，attempt从1开始。
- `enum CafeWorkStage { Working, Prepared, Delivered, Returning, Finished }`。
- `readonly struct CafeWorkCheckpoint`：`EmployeeRole Role`、`string VisitId`、`CafeOrderKey? Order`（Cashier完成收费前为空）、`CafeWorkStage Stage`、`float RemainingWorkSeconds`。P14 Working初始为Cashier1/Barista3，P13独立默认Cashier2；Prepared及之后为0。
- `enum CafeTaskEventKind { Progress, ServiceFinished, Prepared, DeliveryArrived, Returned, Blocked }`；`readonly struct CafeTaskEvent`包含`CafeTaskToken Token`、`EmployeeRole Role`、`string ActorId`、`long HandleId/RequestId`、`int Generation/LayoutRevision`、`CafeTaskEventKind Kind`、`CafeWorkCheckpoint Checkpoint`、`string Failure`。所有身份在实际runner/port产生，不能从UI拼接成功事件。
- `readonly struct CafeVisitSnapshot`：`string VisitId`、`CustomerVisitState NavigationState`、`Vector3 Position`、`bool CounterArrived/PickupArrived/PickupDeparted/ExitArrived/IsEnabled`、`int LayoutRevision`。这些bool来自实际移动里程碑，不由目标分配推定到达。
- `readonly struct CafeDeliverySnapshot`：`CafeOrderKey Key`、`string VisitId`、`SurfaceSlotAddress Address`、`long DeliverySequence`；按实际delivery顺序排序。
- `CafeLoopSnapshot`：不可变聚合，提供`SessionId/Epoch`、当前Order snapshots及keys、`PaidVisitIds/CollectedExitVisitIds`、`Delivery`、`Visits`、两员工checkpoint、`Status/Failure`；不得暴露可写集合。容量直接读取同一Customer controller的Capacity snapshots。
- `public delegate bool CafeDeliveryCommit(CafeTaskEvent arrival, SurfaceSlotAddress address, out string reason)`：仅用于P14员工真实到达后的交付提交；普通显示失败与业务拒绝必须能分别报告原因。

### 2.2 单一业务port

`ICafeLoopEnvironment`只连接service与现有Scene，不提供网络/线程/通用事件总线：

```csharp
bool CanProgress { get; }
IReadOnlyList<CafeVisitSnapshot> Visits { get; }
bool IsEmployeeIdle(EmployeeRole role);
string EmployeeId(EmployeeRole role);
bool TryReservePickup(string visitId, out string reason);
void ReleasePickupAssignment(string visitId);
bool TryMoveToPickup(string visitId, out string reason);
bool TryMoveToExit(string visitId, bool hasCollected, out string reason);
bool TryStartWork(CafeTaskToken token, CafeWorkCheckpoint checkpoint, out string reason);
bool IsCurrentDeliveryArrival(CafeTaskEvent arrival, SurfaceSlotAddress address);
bool CanCollect(string visitId, CafeOrderKey key);
void ProjectDeliveries(IReadOnlyList<CafeDeliverySnapshot> deliveries);
```

service业务提交期间禁止调用port；仅在提交前读取稳定条件、提交后发起移动/视觉。`TryStartWork`可同步回调，service须先登记token；通知可排队，但不能在同步回调里无界递归开始下一任务。

`CafeLoopService(string sessionId, ICafeLoopEnvironment environment)`内部拥有OrderService和CafeDeliveryStore；公开`CafeLoopSnapshot Snapshot`、`void Tick(float scaledDeltaTime)`、`void ReportTask(CafeTaskEvent value)`、`bool TryCommitDelivery(CafeTaskEvent arrival, SurfaceSlotAddress address, out string reason)`、`bool TryCollect(string visitId, out string reason)`、`void ClearOrdersForDecoration(long completedVersion)`、`void ResumeBlocked()`、`void Dispose()`。

`ReportTask`的进度checkpoint可在生命周期暂停期间保存，但正常成功事件只能在允许进度时消费。`TryCommitDelivery`从真实最终到达的employee port同步调用，必须检查当前token/handle/request/generation/revision、当前位置/Slot/lease和CanProgress；外部只读检查返回后再次确认epoch与门禁，防止检查期间重入装修。`ClearOrdersForDecoration`清订单epoch并保留CollectedExitVisitIds直至实际出口；不负责销毁Scene Actor。

### 2.3 唯一Step顺序

`CafeLoopController.Step(float scaledDeltaTime)`依次：处理生命周期/Done门禁 → Customer.Step一次并读真实状态 → service.Tick调度/消费合法事件 → Employee.Step一次 → 刷新只读UI投影。NavigationWorld维持原更新。所有外部回调验证当前身份，零游戏时间不提交正常里程碑；循环内不以帧余量补做第二任务。

Customer/Employee新增`SetIntegratedMode(bool enabled)`：只改变自动Update和受控入口的所有权，不disable组件、不调用Configure重建session。mode由P14接线设置并可序列化恢复；未接P14默认为false。P14 owner停用时保留mode与hold，不能意外让旧Update接管；完整session结束才释放所有权。

### 2.4 测试命令

执行阶段先读`unity-cli` skill，使用本机`unity test --help`核实参数；以下保留最初沿用项目形式的计划命令；实际执行及结果见各Task记录与Validation Report。执行工作目录必须是P14 worktree，不并发启动多个Unity Editor。

```powershell
unity test . --mode EditMode --filter AnimalCafe.Tests.EditMode.Phase14 --output outputs/phase14/focused-edit-green.xml --timeout 600
unity test . --mode PlayMode --filter AnimalCafe.Tests.PlayMode.Phase14 --output outputs/phase14/focused-play-green.xml --timeout 1200
```

每Task把filter缩至其测试class，分别保存`taskN-red.xml`、`taskN-green.xml`及直接regression结果。可信RED必须是目标行为断言失败，不是编译错误、缺包、license或工具故障。可以先写能编译的最小API空壳以观察行为RED，不提前实现功能。

## Task 1 — 业务身份、checkpoint与独立模式合同

**Files:** 新建`Assets/Scripts/CafeLoop/CafeLoopContracts.cs`、`Assets/Tests/EditMode/Phase14/CafeLoopContractTests.cs`、`CafeLoopTestSupport.cs`；后两者路径均在同一Phase14测试目录。

**Interfaces:** 实现§2.1、§2.2的types/port。测试support提供port fake，记录reserve/move/start/project调用，允许同步事件、拒绝和重入，不伪造真实Navigation PASS。

- [x] 写`OrderKeySeparatesEpochAndSession`、`OldAttemptCannotMatchRestoredTask`、`SnapshotsCannotMutateOwnerState`、`CheckpointRejectsInvalidRemainingTime`；断言同数字OrderID跨epoch不相等，负/NaN/Infinity无效，checkpoint保存2/3秒和Prepared=0。
  ```csharp
  Assert.That(new CafeOrderKey("s", 1, 1), Is.Not.EqualTo(new CafeOrderKey("s", 2, 1)));
  Assert.That(cashierCheckpoint.RemainingWorkSeconds, Is.EqualTo(2f));
  Assert.That(preparedCheckpoint.RemainingWorkSeconds, Is.Zero);
  ```
- [x] 运行`CafeLoopContractTests`得到行为RED；最小实现验证、值相等和immutable集合。
- [x] 同class GREEN；直接regression为P9 `OrderCreationQueryTests`，确认不修改通用Order identity规则。记录E-001/006/031的合同覆盖，后续Task补业务部分。

Task1证据（2026-10-19）：`outputs/phase14/task1-red-host.xml`为2 PASS/14行为FAIL；`task1-green.xml`为47 PASS/0 FAIL/0 SKIP（P14合同16，P9直接回归31）。初次sandbox启动在Licensing/WMI处失败、未生成XML，单独保留`task1-red.log`，不算行为RED。真实Scene/manual/Player/device仍NOT_RUN。

## Task 2 — Pick-up FIFO、真实站位与服务点lease

**Files:** 新建`Assets/Scripts/Customers/PickupQueueService.cs`、`Assets/Tests/EditMode/Phase14/PickupQueueTests.cs`；复用`CounterQueuePlanner.cs`、`CounterQueueService.cs`、`CustomerRoutePlanner.cs`，若需兼容扩展只增加可选API。

**Interfaces:** `PickupQueueService`提供`bool CanReserve { get; }`、`bool TryReserve(string visitId, out QueueAssignment assignment)`、`bool TryBeginTransfer(string visitId, out QueueAssignment assignment)`、`bool MarkArrived(string visitId, int generation)`、`bool TryGetNextAdvance(out QueueAssignment assignment, Func<QueueAssignment,bool> canAdvance = null)`、`bool TryAcquireDelivery(string employeeId)`、`bool TryAcquireCollection(string visitId)`、`void ReleaseServicePosition(string actorId)`、`bool Remove(string visitId)`、`IReadOnlyList<QueueAssignment> Snapshot`。构造函数`PickupQueueService(IReadOnlyList<Vector3> waitSlots, Vector3 servicePosition, bool sharedServicePosition)`；waitSlots必须预先验证合法，服务点不能当额外未计容量的顾客槽。

物理reserve发生在收银前，FIFO入队发生在付款提交后的TryBeginTransfer；不能因早期预留次序改变点单顺序。head只在自己的Ready和service lease允许时进入领取点，先前的位置保留到实际离位。delivery lease直到员工身体实际让出交付点才释放，不在放杯瞬间提前让顾客穿过员工。

Task2读回修正：队首实际离位Remove后，余客必须按FIFO前移到前方wait slot，以新generation并等待实际到达；新客不能直接占前方空位。TryGetNextAdvance在canAdvance拒绝时不提交半份assignment；上层按实际身体/路线判断允许移动，不伪造Arrived。初轮21项测试遗漏了这个D2要求，已要求补RED/GREEN，初轮50 PASS不作为最终Task2完成证据。

- [x] 写`OnlyPaidHeadCanCollectOwnOrder`、`LastWaitSlotHasOneOwner`、`SharedServicePositionLetsDeliveryFinishBeforeCollection`、`HoldCollectionDoesNotHoldDeliveryLease`、`FailedTransferKeepsReservation`。模拟5个真实wait分配与错序回执，断言后客不能拿lease、重复reserve不占第二位、拒绝后不丢FIFO。
  ```csharp
  Assert.That(lastSlotSecondOwnerAccepted, Is.False);
  Assert.That(nonHeadCollectionAccepted, Is.False);
  Assert.That(deliveryCountWithCollectionHeld, Is.EqualTo(5));
  ```
- [x] focused RED→最小实现→GREEN；规划站位时复用原方向/间距/预算与可达性predicate，不复制一套不受碰撞检查的算法。
- [x] 直接regression：P12 `CounterQueueTests`（包含现有planner用例）与`CustomerRoutePlannerTests`。覆盖E-012/015/037/039的domain部分；真实身体在Task3/9验证。

Task2最终证据：首轮`task2-red.xml`为0P/21F、`task2-green.xml`50P/0F；发现前移遗漏后，`task2-advance-red.xml`为20P/5F，`task2-advance-green.xml`为54P/0F/0S（25新tests+26 CounterQueue+3 RoutePlanner）。前移finding已修复；CLI exit0。domain队列不代替Task3的实际NavWorld/body验证。

## Task 3 — Customer实际取餐移动与Capacity生命周期

**Files:** 修改`CustomerFlowController.cs`、`CustomerContracts.cs`、`CustomerAdmission.cs`；新建`Assets/Tests/EditMode/Phase14/CafeAdmissionTests.cs`、`Assets/Tests/PlayMode/EditorSceneLoading/Phase14/CafeCustomerFlowTests.cs`。

**Interfaces:** Customer新增`SetIntegratedMode(bool enabled)`、`IReadOnlyList<CafeVisitSnapshot> CafeSnapshot`、`bool TryReservePickup(string visitId, out string reason)`、`bool TryMoveToPickup(string visitId, out string reason)`、`bool TryMoveToExit(string visitId, bool hasCollected, out string reason)`、`void ReleasePickupAssignment(string visitId)`、`void ClearUncollectedVisits(IReadOnlyCollection<string> collectedVisitIds)`。实际Pick-up状态可加P14专属字段，不改变旧enum已有数值/含义。

`CustomerAdmission`新增`bool MarkPickupWaiting()`与`void LeavePickup()`，按原owner/token幂等操作；保留`CancelUnusedPickup`只用于旧提前离场/明确未使用取消。正常付款后去Pick-up不能调用P12 `TryLetFrontLeave`，因为该方法会取消Pick-up预留。

- [x] 写`PaymentDoesNotReleaseCounterUntilDeparture`、`CollectionDoesNotReleaseTotalUntilExit`、`PickupOccupiedOnActualArrival`、`IntegratedTransferNeverCancelsPickupReservation`、`NoPickupSlotKeepsCustomerAtCounter`。用三种token状态逐事件断言，wrong owner、重复/旧generation事件不影响他人。
  ```csharp
  Assert.That(totalUsedAfterCollection, Is.EqualTo(1));
  Assert.That(pickupUsedBeforeActualDeparture, Is.EqualTo(1));
  Assert.That(totalUsedAfterExitArrived, Is.Zero);
  ```
- [x] 在`CafeCustomerFlowTests`写真实Counter→Pick-up→Exit与共享anchor互斥的RED；只通过NavigationWorld/现有route planner移动，复用实际离位和连续碰撞验证。
- [x] 最小实现新阶段、站位保留、实际Arrived/离位事件、mode下Update/旧离场入口隔离；非P14模式保持原流程。
- [x] focused GREEN；直接regression：P12 `CustomerAdmissionTests`、`CounterQueueTests`、`AnimalCafe.Tests.PlayMode.Phase12Integration.CustomerQueueFlowTests`。覆盖E-002/005/015–020/027/037、P-003–005/011的相应部分。

**执行记录（2026-10-19）：** Edit行为RED `task3-edit-red.xml` 0P/5F；真实Play RED `task3-play-red.xml` 1P/4F；新增canonical清场/未来Counter站位RED `task3-canonical-red.xml` 0P/2F。最终Edit `task3-edit-final-green.xml` 76P/0F/0S（新admission5+P12 admission4/queue26+P14 contract16/queue25）；最终Play `task3-play-final-green.xml` 43P/0F/0S（新真实移动7+P12直接回归36），CLI均exit0。复用原admission/token、真实NavWorld与实际出口事实；已领取集合为清场权威。完整业务闭环及Owner manual尚未执行。

## Task 4 — Employee订单上下文与可恢复checkpoint

**Files:** 修改`EmployeeContracts.cs`、`EmployeeTaskRunner.cs`、`EmployeeFlowController.cs`；新建`Assets/Tests/EditMode/Phase14/CafeEmployeeCheckpointTests.cs`、`Assets/Tests/PlayMode/EditorSceneLoading/Phase14/CafeEmployeeRecoveryTests.cs`。

**Interfaces:** Employee controller新增`SetIntegratedMode(bool enabled)`、`void SetDeliveryCommit(CafeDeliveryCommit commit)`、`event Action<CafeTaskEvent> IntegratedTaskEvent`、`bool TryStartOrderTask(CafeTaskToken token, CafeWorkCheckpoint checkpoint, out string reason)`、`bool TryCaptureOrderCheckpoint(EmployeeRole role, out CafeWorkCheckpoint checkpoint)`、`void HoldIntegratedTasks()`。runner新增`bool TryStartIntegrated(CafeTaskToken token, CafeWorkCheckpoint checkpoint)`与`CafeWorkCheckpoint CaptureCheckpoint()`；旧构造/`TryStart()`及P13 environment合同兼容。

实施适配：controller另提供`SetDeliveryGate(Func<string, bool>)`连接真实共享位置lease，以及`IsCurrentDeliveryArrival(CafeTaskEvent, SurfaceSlotAddress)`纯查询供提交前复核。仅在真实最终Arrived的同步commit窗口承认当前事件；正常lease占用进入等待，不能当作需要手动恢复的失败。runner可使用兼容原environment的小型扩展接口，旧P13 fakes不增加成员。

P14 task的事件必须带真实handle/generation/request/revision。真实送达最终到站时通过controller绑定的`CafeDeliveryCommit`向service申请提交，成功后才隐藏手杯并返回；P13模式仍走原`TryPlaceCup`视觉路径。service/renderer失败原因保存在snapshot，不让bool失败丢原因。

先保存进度再处理failure、Actor/component disable或layout invalidation；恢复使用新attempt/handle，从实际位置继续。恢复Prepared只补手杯视觉并续送；Delivered/Returning只回CM。Cashier工作已完成但Create未成功时，只重试提交，不重新计时。旧attempt无法完成新任务。

- [x] 写`WorkingResumesRemainingSeconds`、`PreparedFailureDoesNotPrepareAgain`、`DeliveredReturnFailureCannotDeliverAgain`、`DisableCapturesBeforeCleanup`、`LateOldHandleDoesNotAdvanceRestoredTask`、`CashierFinishedRetriesSubmissionWithoutWorkingAgain`、`SynchronousMoveCompletionCannotOverwriteAdvancedState`。
  ```csharp
  Assert.That(resumedWorkSeconds, Is.EqualTo(savedRemaining));
  Assert.That(preparationCompletions, Is.EqualTo(1));
  Assert.That(deliveryCommits, Is.EqualTo(1));
  ```
- [x] RED→最小checkpoint/事件/恢复实现→GREEN；disabled时身体注册不移除，单一mode不能靠禁用controller实现；Task完成前不会自动Claim下一单。
- [x] 真实PlayMode验证手杯cleanup后重建、实际姿态续送、回位失败后仍可取餐；直接regression：P13 `EmployeeTaskRunnerTests`、`EmployeeLifecycleTests`、`EmployeePlaySessionTests`、`EmployeeDecorationTests`。
- [x] 覆盖E-007–008/021–027/038、P-007–010/018/024；保存每种failure checkpoint和实际业务次数，不只比最终state。

**执行记录（2026-10-19）：** Edit行为RED `task4-edit-red.xml` 1P/14F；真实Play RED `task4-play-red.xml` 0P/5F；mode序列化合同追加RED `task4-mode-contract-red.xml` 0P/1F。最终Edit `task4-edit-final-green.xml` 60P/0F/0S（P14 checkpoint15、P13 runner38/lifecycle5/真实PlaySession2）；最终Play `task4-play-final-green.xml` 17P/0F/0S（P14真实恢复6、P13装修11），CLI均exit0。两次早期JSON探针虽PASS但不算行为RED；报告保留其限制。已交付事实优先于重入disable保存的Prepared由Task5接线；Done丢弃旧context由Task7实现，未提前加入。

## Task 5 — 一次性付款、交付、领取与Order投影

**Files:** 新建`CafeLoopService.cs`、`CafeDeliveryStore.cs`、`Assets/Tests/EditMode/Phase14/CafeLoopServiceTests.cs`、`CafeDeliveryStoreTests.cs`；不修改P9公共规则。

实施适配：仅给`CafeLoopContracts.cs`的environment与`CafeLoopTestSupport.cs`增加readonly `EmployeeId(role)`查询，让ClaimNext使用Scene实际员工身份，不由service猜测ID。此查询无写入，沿用原两员工对象。

**Interfaces:** 实现§2.2 service API。`CafeDeliveryStore`公开readonly `Snapshot`及`bool Contains(CafeOrderKey key)`；`CommitDelivery(CafeDeliverySnapshot value)`、`CommitCollection(CafeOrderKey key)`、`Clear()`仅internal由service写入。业务提交预验证全部可预期失败，commit内部不调用port或发布event。

收银完成只提交一次Create+Paid；ClaimNext只在员工实际Idle时；领取成功但任务派发被拒时保留同一Claimed Order与初始checkpoint并Blocked，恢复只重派该单，不重新Claim/插到队尾/每帧重试。Order `Preparing`覆盖送达前；`TryCommitDelivery`一次提交Ready+delivery，再渲染；`TryCollect`一次提交Completed+移除delivery+加入CollectedExitVisitIds，再移除视觉并派离场。保存独立于Order store的已取餐离场集合，直到实际ExitArrived再移除；因此提交后port重入装修也能保留正确顾客。

- [x] 写`CashierCompletionCreatesOnePaidOrder`、`CreateRejectedLeavesNoPartialPaidRecord`、`ReturningBaristaCannotClaimNext`、`RejectedTaskStartRetainsClaimWithoutClaimingAnotherOrder`、`WrongOwnerOrNonHeadCannotCollect`、`CompleteRejectedKeepsReadyDeliveryAndCapacity`、`CommitPublishesBeforeReentrantProjection`、`ReplayCannotAffectNewEpoch`。
  ```csharp
  Assert.That(ordersForVisit, Has.Count.EqualTo(1));
  Assert.That(afterRejectedComplete, Is.EqualTo(beforeRejectedComplete));
  Assert.That(collectedExitIdsAtReentrantDecoration, Does.Contain(visitId));
  ```
- [x] 配套`CafeDeliveryStoreTests`写5次交付、取走中间可见记录、重复key/旧epoch拒绝、renderer异常不回滚Ready/Completed；先domain RED，再最小实现。
- [x] GREEN；直接regression：P9 `OrderTransitionTests`、`OrderClaimTests`、`OrderInvariantTests`；P10 `CapacityOrderBoundaryTests`。不能调用任意SetState或用Fail来模拟临时Blocked。
- [x] 覆盖E-003–014/021/023–027/031/034–035；service只保存业务checkpoint，不和runner重复计算工作时间。验证fake拒绝的是实际调用路径，不能只测试手写expected对象。

**执行记录（2026-10-19）：** 初始业务RED `task5-edit-red.xml` 0P/24F；最高stage/revision RED `task5-checkpoint-red.xml` 0P/2F；同步Pause派发RED `task5-dispatch-gate-red.xml` 0P/1F；恢复重入RED `task5-reentry-red.xml` 0P/1F。最终 `task5-reentry-final-green.xml` 157P/0F/0S、CLIexit0，包含Service25/Store3/Contract16/EmployeeCheckpoint15与P9 Transition68/Claim19/Invariant8、P10 Boundary3。此前153/156是修复前快照，不作最终接收。新attempt用新Work引用，checkpoint不降级，合法Blocked可保存新revision，Pause延后Preparing。业务提交先于投影；真实Scene容量联通及杯子展示留Task6。

## Task 6 — 单一Scene驱动与精确杯子显示

**Files:** 新建`CafeLoopController.cs`；修改`PickupCupDisplay.cs`和Customer/Employee的P14 mode适配；新建`Assets/Tests/EditMode/Phase14/CafeLoopDriverTests.cs`、`CafeCupProjectionTests.cs`、`Assets/Tests/PlayMode/EditorSceneLoading/Phase14/CafeLoopIntegrationTests.cs`。

**Interfaces:** controller提供`void Configure(CustomerFlowController customers, EmployeeFlowController employees, PickupCupDisplay cups, GameTimeService time, NavigationLayoutAdapter adapter, DecorationModeController decoration)`、`void Step(float scaledDeltaTime)`、`CafeLoopSnapshot Snapshot`、`void ResumeBlockedFlow()`。它实现ICafeLoopEnvironment并持唯一service，不创建第二套Capacity/Navigation/员工。

display新增`bool TryProject(IReadOnlyList<CafeDeliverySnapshot> deliveries, out string reason)`；按每个SurfaceSlot的delivery sequence取前三杯，以Order key保留/移除对象，不创建隐藏库存。P13的`TryDeliver(address,out reason)`保留，只在非P14模式使用；同一display不能混用两种写入口。

- [x] 写`DriverTicksEachSubsystemExactlyOnce`、`LegacyButtonsCannotWriteIntegratedSession`、`ProjectionShowsThreeAndRefillsCorrectKey`、`ProjectionFailureCannotUndoCompleted`、`DisabledOwnerDoesNotHandControlBackToLegacyUpdate`；断言实际调用次数、Order key对象映射和新session订阅数量。
  ```csharp
  Assert.That(customerTicksInOneFrame, Is.EqualTo(1));
  Assert.That(employeeTicksInOneFrame, Is.EqualTo(1));
  Assert.That(cupObjectsIncludingInactive, Has.Count.LessThanOrEqualTo(3));
  ```
- [x] RED→按§2.3唯一Step和port适配最小实现→GREEN；旧P13手动派单和P12提前离场在P14模式明确拒绝或隐藏，不能仅靠UI隐藏而API仍能干扰。
- [x] 用真实Scene测试联通Cashier→Create→Barista→Ready→collect→exit，含同步callback和Pause/2x；直接regression：P13 `PickupCupDisplayTests`、`EmployeeFlowTests`；P12 CustomerFlow直接相关class。
- [x] 覆盖E-001/013–014/021–022/032/036/039，P-002/006/008/018/022–024的驱动与显示部分；不将fixture传来的成功信号当真实20单闭环证据。

**执行记录（2026-10-19）：** 行为RED为Edit `task6-edit-red-host.xml` 0P/13F、Play `task6-play-red.xml` 1P/4F；编译失败的早期启动不计行为RED。最终Edit `task6-edit-final-green.xml` 32P/0F/0S（投影7、driver6、P13显示19）；真实Play `task6-play-final-green.xml` 21P/0F/0S（闭环6、P13 EmployeeFlow8、P14顾客7）；旧P12直接回归 `task6-p12-green.xml` 36P/0F/0S。Root已读XML和源码；单一Step、实际领取/出口、Hold Collection、2x、停用恢复与初始员工失败显式重试已验证。首轮Play的单客fixture误放入第二客已修正为首个真实Visit生成后停止新客。真实5杯并行、20单和Done门禁留后续Task；Owner manual尚未执行。
## Task 7 — Done同步门禁、选择性清场和生命周期

**Files:** 修改`DecorationModeController.cs`、`CafeLoopController.cs`、`CafeLoopService.cs`、`CustomerFlowController.cs`及Employee必要的checkpoint接线；给`Core/Time/GameTimeService.cs`增加兼容的只读手动速度记录；新建`Assets/Tests/EditMode/Phase14/CafeDecorationLifecycleTests.cs`、`Assets/Tests/PlayMode/EditorSceneLoading/Phase14/CafeDecorationFlowTests.cs`。

技术补充：`GameSpeed? LastExplicitSpeed { get; private set; }`只在现有成功explicit选择计数处更新（包括同speed），automatic选择不覆盖。原事件/version无法区分“同speed手动选择后紧接自动Pause”，不据此猜测玩家意图。布局有效Done先协调Decor原pending restore，再在无其他block且非主动Pause时自动恢复最后运行速度；用真实空店自动暂停→有效Done、主动Pause和2x场景及原Time/Navigation直接回归验证。

**Interfaces:** Decoration新增可选`event Action<long> ChangedLayoutExitPreparing`，只在`completedEditedSession`成立时，于`PrepareExit()`之前且仍持装修Pause的位置触发，参数为待发布的`CompletedLayoutChangeVersion + 1`。无订阅时行为保持；shutdown/无修改不触发。P14 handler先`time.AcquireResumeBlock(this, reason)`再清场；最终version发布及验证后由controller完成释放，不要求改变旧marker含义。

处理同一目标version幂等，抓取service的CollectedExitVisitIds后才能清Order store；Customer.ClearUncollectedVisits保留该集合中的Actor和原admission，不新建Capacity。保留者只退休旧移动、下一合法推进时从实际位置重寻出口。正常shutdown/完整Dispose清全session，与装修保留例外分开。

- [x] 写`PreparingHookRunsBeforeNavigationAndPauseRelease`、`OldCallbackCannotCommitWhileDoneIsPreparing`、`CleanupFailureKeepsResumeBlock`、`CollectedBlockedCustomerSurvivesOrderEpochReset`、`RepeatedDoneKeepsOneTotalLease`、`NoChangeAndShutdownDoNotTriggerEditedDone`。
  ```csharp
  Assert.That(eventNames, Is.EqualTo(new[] { "gate", "retire", "clear", "prepareExit", "publish", "release" }));
  Assert.That(survivingVisitId, Is.EqualTo(originalVisitId));
  Assert.That(totalUsedBeforeActualExit, Is.EqualTo(1));
  ```
- [x] 实际Done同步注入旧movement callback及恢复事件，先RED→最小hook/门禁/选择性清场→GREEN。清场异常保留block与原因，恢复入口不能直接清掉未知不一致状态。
- [x] PlayMode测试未取餐/已取餐混合、Blocked/disabled离场者、两次Done、缩容、新Order复用数字ID、出口无路后修复。主动Pause和1x自动恢复各测一次。
- [x] 直接regression：`UiPauseCoordinatorTests`、`NavigationResumeBlockTests`、`NavigationResumeLifecycleTests`、`NavigationLayoutIntegrationTests`、P12 `CustomerSessionLifecycleTests`、P13 `EmployeeDecorationTests`。覆盖E-020/028–033/038/040，P-012–016/018/021。

**执行记录（2026-10-19）：** 初始Edit行为RED `task7-edit-red.xml` 2P/3F；真实MainCafe初始自动Pause→有效Done失败已复现。首次Play含过早读取P12 startup block的fixture失败，修正后的行为RED另存`task7-play-fixture-red.xml`；旧lease追加RED `task7-lease-red.xml` 0P/1F。最终Edit `task7-edit-final-green.xml` 76P/0F/0S（含真实namespace的UiPauseCoordinator9），Play `task7-play-final-green.xml` 38P/0F/0S（新装修10、NavLayout11、P13装修11、P14闭环6），CLI均exit0。Main首次营业先在门禁内启用现有business readiness配置，再由原PrepareExit验证，未放开零delta移动；旧lease与任务context退休，原员工handle单调。Disabled/Blocked已取餐者、重复Done、原Capacity/token、限额下降输入和真实出口、新epoch数字ID复用、invalid→valid Done及主动Pause/2x已覆盖。限额下降用原Capacity API注入，不声称新增Floor缩小UI。Root确认Unity0后，仅还原测试自动重存的P7 Invalid材质，保留全部P14源；Owner manual仍NOT_RUN。
## Task 8 — MainCafe installer、只读UI与可执行Manual准备

**Files:** 新建`Phase14CafeLoopSceneSetup.cs`、`CafeLoopStatusView.cs`、`CafeLoopValidationController.cs`、`Assets/Tests/EditMode/Phase14/Phase14SceneBuilderTests.cs`、`Assets/Tests/PlayMode/EditorSceneLoading/Phase14/CafeStatusAndToolsTests.cs`；由Editor API创建/更新MainCafe与P14独立fixture。

必要兼容接线：`CustomerFlowController.cs`增加真实Counter顺序的readonly copy；`CafeLoopController.cs`增加供view/诊断读取的immutable聚合和仅重放已观察、已退休callback的受控验证入口，不暴露service写入口。`Assets/Editor/Phase12/Phase12CustomerQueueSceneSetup.cs`仅在验证copy移除识别出的P14 root并恢复旧mode；`Phase13LegacySceneCompatibilityTests.cs`补隔离断言。`EmployeeMainCafeTests.cs`及其`EmployeeCounterHeightTests.cs` partial只将MainCafe路径改为真实自动服务，保留运动/高度/ownership断言和P13独立Scene原模式。旧P13 builder若实际覆盖新HUD设置，先复现再作必要最小兼容分支，不改已接受独立Scene资产。

Task8实际兼容RED：`task8-install-check.xml` 4P/1F，旧`Assets/Editor/Phase13/Phase13EmployeeTaskSceneSetup.cs`重装MainCafe两次将P14文字best-fit从false改true。允许在该builder中为同Scene的P14只读状态文字保留设置，P13独立验证的8–10字号适配/captions保持原规则；不放宽Phase8 validator。

Task8真实并行暴露的P14适配缺口：前客正在取餐移动，其remaining route穿过后客待预留slot；当时static path有路，后客开工两秒后body route被暂时挡住而Finished/Blocked。CR旋转布局对照仍复现，不能以逐客开关到店替代自动五杯。补`CustomerFlowController`的P14初次reserve门禁：沿用既有ExpandRoute/IsClear和真实remaining route，在开工前拒绝仍位于前客路线上的候选，仅退本次未付款临时reservation并普通等待；不改Service重试规则、旧P12模式或P11预算。新增真实开工断言先RED，再最小实现，并回归P14 Customer/Integration。

Task8 FIFO前移修复范围：真实五杯drain发现付费前客仍在向slot1前移、slot0刚释放时，`PickupQueueService.TryGetNextAdvance`跳过Advancing前客后会把slot0交给后客。补该纯queue行为RED，限制后客候选slot始终在所有paid前客当前owned Slot之后；仍允许安全的并行前移，不更改旧Counter算法。回归完整PickupQueue class和真实P14 Customer/Integration，未证明的其他出口失败继续单独定位。

Task8共享fixture真实RED：队列planner折回正式共享service点，被`PickupQueueService`既有constructor合同正确拒绝。仅在P14共享候选站位predicate中按原clearance排除service身体范围，保留constructor检查；回归分离/共享自动五杯与实际离场，不修改角色尺寸、导航预算或旧Counter规则。

Task8出口普通等待的实测适配：`task8-exit-first-trace.xml`中前客仍有真实Exiting Request59，距离共同exit约0.072m未收到最终Arrived；后客下一段尚未发请求，static route有效、body route无解，被立即Blocked。仅P14、实际已领取且有active Exiting前客临近同一出口时，后客保留原身体/token/Exiting checkpoint与remaining route，正delta再查普通发起条件；等待不消耗Segments/Replans/Nav预算。真正已发请求的失败及其他障碍继续原有bounded Blocked/显式恢复规则。

Task8共享drain进一步RED：实际五杯均送达，但后客被分配的前移target仍在前客正在通过的remaining path上。`CanBeginPickupAdvance`补与原Counter相同的末点路径保护：在queue提交新Slot/generation之前，既查RoutesConflict也查候选target不占用活动前客尚需经过的路径；原有身体、并行已腾位跟进与Nav预算不变。证据`task8-exit-wait-green.xml`为分离完整drain PASS、共享drain FAIL，不能汇总成两个fixture通过。

Task8真实Main四客进一步RED：两个已发Exiting最终leg从相向方向进入同一出口，前客离goal约0.185m、后客相距约0.912m，P11身体保护正确阻挡后超时。在P14未发初始/后续segment之前，用现有ExpandRoute/RoutesConflict检查较早已领取Exiting活动Request的真实remaining route；仅cross/opposed冲突普通等待，安全同向仍可并行。Customer内私有单调领取/离场顺序在canonical首次HasCollected时赋一次、跨Done保留，不新增公开API；等待不消耗Nav/segment/replan预算，已发请求failure保持原规则。

Task8真实横屏截图发现复用P13 CanvasScaler的match=.5使184逻辑高面板占720px约411px，遮住过多经营区域。仅P14 installer将该复用Canvas设为height fit(match=1)，保留原360×640 reference与独立P13的.5；增加旧builder重装保持契约RED/GREEN，再用真实1600×720及1080×1920 Game view与Time/Decor raycast验证。原capture随后出口FAIL的NUnit不算GREEN，PNG只证明当时画面。

Task8只读状态通知RED：单独停用owner组件后，view仍启用但只收SnapshotChanged，原Update停止且OnDisable未通知，文字停留在旧Paused/营业状态。补OnDisable完成Hold后的最终readonly snapshot通知与显式恢复刷新，沿用事件驱动，不加UI轮询或业务写入口。

**Interfaces:** Editor菜单`Tools/AnimalCafe/Phase 14/Install Cafe Loop in MainCafe`调用`Phase14CafeLoopSceneSetup.BuildMainCafe()`；`Tools/AnimalCafe/Phase 14/Create Cafe Loop Validation Scene`调用`BuildValidationScene()`。根为`P14 Cafe Loop`，含controller和验证组件；复用原两员工对象管理器/Navigation/Time/UI EventSystem。

共享anchor的独立`Phase14SharedPickup.unity`复用同一installer/validation组件的fixture-only seed分支；不新增产品开关或另一套导航规则。两份Scene都由Editor API生成，真实5次delivery证明后才记录为可用fixture。

`CafeLoopStatusView.Bind(CafeLoopController owner)`只订阅/读取readonly snapshots。验证组件的`Configure(CafeLoopController owner)`后提供test cases §4规定的所有`P14/...`组件菜单；Hold Arrivals只影响测试生成开关，Hold Collection只影响最终领取/lease，不写OrderService。Replay必须选择确切旧epoch/attempt，运行时blocker为可逆owned对象，不改正式Layout/角色尺寸或碰撞预算。

- [x] 写`InstallerIsIdempotentAndPreservesLayout`、`LegacyValidationSceneIsUnchanged`、`StatusRefreshNeverMutatesBusiness`、`HeldCollectionAllowsFiveRealDeliveries`、`InspectorReplayRejectsCurrentOrUnknownEvent`。两次安装断言一个root/两员工管理槽/一套UI/无重复订阅；给dirty Scene与Play中安装明确拒绝。
- [x] RED→最小installer/UI/菜单实现→GREEN；Editor API保存时不覆盖用户未保存Scene。不重建旧builder或覆盖P13独立验证Scene。
- [x] 在Beginner Guide准备实际验证布局坐标、5个真实Pick-up等候位、分离/共享anchor布局、所有菜单位置和故障注入方式；确认每个manual入口已存在。UI不增加开始接待按钮或Coins。
- [x] PlayMode核对snapshot与UI、横竖Game view、Console；直接regression：P13 `Phase13SceneBuilderTests`、`Phase13LegacySceneCompatibilityTests`、`Phase5UiFoundationRound2RealInputTests`。覆盖E-032/036/039、P-001/017/020/023–024，解锁M-001–022的准备条件。

**执行记录（2026-10-20）：** 最终Edit `task8-edit-final-green.xml` 43P/0F/0S（P14Builder7、Pickup26、P13Builder3、Legacy7），Play `task8-play-final-green.xml` 41P/0F/0S（Tools7、完整Main partial11、P14顾客7／闭环6、原UI真实输入10），CLI均0。真实native `task8-native-final-green.xml` 1P/0F/0S、wrapper0；两张height-fit真实Game view及Time/Decor raycast通过，root已view_image。两fixture均真实自动五杯、仅三实际杯、正确key补显、paid FIFO Completed、实际离店与三Capacity清0；均观察到两条并行真实exit请求。旧反例分别保留，不以错误fixture patch／无XML中断／截图代替GREEN。原P12／P13 Scene+meta literal bytes全部恢复，Main仅清5处新空白尾空格；Editor0、diff-check通过，无Nav/Art/Settings副作用。Guide记录实际样本站位及完整菜单，Owner22仍NOT_RUN。Task7两个InitialMain测试将在Task9复用正式唯一owner后重验；没有冒称本批已包含DF10。Task9现已交接唯一Unity/source实现者，完整suite／独立review／Owner验收继续。
## Task 9 — 完整闭环、回归、manual与阶段交接

**Files:** 完成`CafeLoopIntegrationTests.cs`及新增`Assets/Tests/PlayMode/EditorSceneLoading/Phase14/CafeLoopRegressionTests.cs`；新建`Docs/Phase14_Beginner_Guide.md`、`Docs/Phase14_Validation_Report.md`；必要同步Project Design/Roadmap及本plan/cases执行映射。

必要test fixture兼容：`CafeDecorationFlowTests.cs`的两个Initial Main分支需复用Task8正式安装的唯一owner并断言Single，不能在Main额外Add第二driver；P12临时fixture分支保持原host，真实Done/主动Pause断言保留，回归DF10。Task8的41项filter未包含此class，不将历史Task7的Main结果当作安装后已重验。

Task9连续20单真实RED暴露付款提交前可用性门禁：已完成收银且仍独占原slot时，移动身体使预留路径暂不可走，尚未调用P9 Create却被永久Blocked。仅将已消费真实ServiceFinished的提交前可用性检查保留为普通等待；正delta最多检查一次，原slot/token/Finished/0秒不变，不派新任务。必须仍是Enabled、实际Counter已到位且Queued的原Visit；不能由任意Finished Progress伪造付款。真正P9 Create拒绝、派发拒绝与已发Nav失败仍Blocked并显式恢复，不变成每帧业务retry。

Task9 manual-readiness复核：`Replay Last Business Callback`若只取所有retired事件最后一个，Done后新订单会使新epoch Cashier事件先退休，菜单误选该事件而没有验证M-015旧epoch。仅调整已观察known-retired候选的选择：优先旧epoch，缺少时可明确注明同epoch已退休attempt/task来源；保持current/unknown拒绝。补真实Done→新订单→菜单replay无副作用回归，不增加公开写API或无限事件历史。

Task9首次完整Edit（`full-edit-pre-feedback.xml`）2722P/1F/0S：旧P8R冷加载字节稳定测试只发现Main新增5处空`m_Name:`被手工清尾空格，与Unity原生`m_Name: `不同。保留Editor原生序列化，保持原测试与业务代码；这些Unity生成空字段的空格单独记录，不再为通用diff-check修改Scene字节。合并I-UX-01 Console反馈窄修后，原失败定向回归及最终完整Edit/Play重新验证。完整FAIL历史保留，不以定向GREEN取代完整suite。

Task9完整Play首轮为1315P/1F/5S（P14全部49PASS）：旧真实Touch发现P14 owner会继续调用已disable的Customer.Step，后者初始化Nav业务门禁，导致原Touch停用装修组件时无法恢复1×。新增真实owner RED与原Touch共同失败（1P/2F）；仅在integrated Step的delta校验后检查isActiveAndEnabled，保留legacy和原P11 shutdown failclosed。修后全部P14 Play51／旧Touch23／P11 shutdown2共76P、全部P14 Edit124／P12客户48共172P，均0F/0S、CLI0；原Engineering窄复核无残留Critical/Important。产品于06:46 UTC再次冻结；后续完整Play的旧test fixture修复与原始FAIL分别保留在Validation Report。完整Edit2723为该guard前快照，按本Task风险范围以直接Edit补验，不冒称同源完整Edit。
必要Nav兼容修复（第四轮完整Play）：P14全部51 PASS，旧P11 detour超时；从记录的合法位置创建真实native Actor可重现静态投影清零。依本plan的必要兼容条款，修复限定`NavigationCollisionGuard.cs`私有Box sweep证明、`NavigationCollisionTests.cs`、`Phase11NavigationSceneTests.cs`三文件；先正式Edit RED17P/2F及真实native Play RED0P/1F，再用实际float32终点的连续XZ segment／rectangle距离加胶囊轴／BoxY间隙证明完整半径。角色尺寸、skin、到站容差、恢复预算及unsupported collider failclosed保持；原Engineering／QA窄复核本修复CLOSED；最终源Edit254P/0F/0S，真实Play新增recorded Native与原四档detour及全部P14 PASS。相关cohort整体224P/1F/1S，旧P13 carry被真实长帧cap污染wall比例；依原World预算合同只将test改budget等效时间，保留原.5±.12及全部真实断言，修后相关67P/0F/0S、原QA窄复核CLOSED。runtime08:35:02、最后test08:48:59 UTC冻结；因shared碰撞修复完成第五完整Play：1319P/0F/5S、CLI0、inconclusive0，08:53:38Z–09:11:56Z；P14全部51 PASS，两组20单actualExit各20并排空，原FAIL历史保留。完整Edit2723明确为guard／Nav前，最终源受影响Edit254补验，无同源全Edit冒称。

**Interfaces:** 不新增产品API。测试通过真实Scene/ports驱动已完成组件，不直接Create/Complete假冒闭环。测试辅助Hold Arrivals在达到目标后停止新生成并让剩余客人实际离店。

- [x] 写`CompletesTwentyRealOrdersAndDrainsAllCapacity`：至少20个不同Visit，各自记录实际收银完成、制作完成、最终送达、正确取餐和实际出口；末尾排空后三类capacity=0，delivery/手杯/桌杯=0，无active handles或stale callbacks。
  ```csharp
  Assert.That(realCompletedDistinctVisits, Is.GreaterThanOrEqualTo(20));
  Assert.That(wrongOwnerCollections + duplicateBusinessCommits, Is.Zero);
  Assert.That(totalUsed + counterUsed + pickupUsed, Is.Zero);
  ```
- [x] 先RED确认缺失整合断言能失败，再完成必要最小接线并GREEN；额外固定1x/2x、多个species、满额恢复、共享anchor、轮流disable与Domain Reload的P14回归。
- [x] 完成40项E/24项P到实际NUnit tests的coverage映射；遗漏必须补测，不能以一个泛化“全部通过”替代。然后运行一次完整EditMode和PlayMode、真实MainCafe与fixture integration。
- [x] 按本机help支持的CLI执行完整suite（下列是计划命令）；若工程编译或运行环境失败先修复，不标行为PASS：
  ```powershell
  unity test . --mode EditMode --output outputs/phase14/full-edit.xml --timeout 3600
  unity test . --mode PlayMode --output outputs/phase14/full-play.xml --timeout 7200
  ```
- [x] 集中一次独立Engineering/QA review，发生明显UI/杯子视觉变化再做必要Art/UX review；Critical/Important修复后只重跑受影响检查，完整重跑由实际风险决定。Engineering无finding；QA三项文档Minor已原reviewer关闭；UX的Console反馈Important已修并原reviewer关闭，定向Edit126P／Play10P。最终完整suite另行记录。
- [x] 将22项manual准备为可执行步骤，Guide／菜单与真实反馈均具备。M-021用Done前后Snapshot／画面辅助观察，精确同步时序由P-021自动测试证明；工具代跑与Owner视觉acceptance分开。
- [ ] 由Owner执行M-001–022并逐项填写真实结果；M-007已反馈待复测，其余尚未验收，自动验证不替代Owner acceptance。
- [x] Validation Report记录source/Scene配置、RED/GREEN及全suite counts/FAIL/SKIP、manual结果、缺陷和剩余限制。Player/Android/iOS/CI未执行写NOT_RUN，不用Editor代替真机。
- [x] Living documents仅同步已批准规则及真实状态；Owner验收前不标Completed。phase完成不授权commit/push/merge、自动CI、下个Phase或删除worktree。

## 3. Coverage任务映射

每行是需求场景的责任Task，不是已实现tests的证明。交付前补具体test method和结果路径。

| 场景范围 | 责任Task | 主要输出 |
| --- | --- | --- |
| E-001–006 | 1、3、5、6 | 身份、队首、付款Create和空间门禁 |
| E-007–014 | 4、5、6 | 员工FIFO、送达/领取、delivery及visual |
| E-015–020 | 2、3、7 | lease/实际移动、容量释放、缩容 |
| E-021–027 | 3、4、5、6 | 时间、checkpoint和恢复 |
| E-028–033 | 1、5、7 | 装修、epoch、保留离场和同步门禁 |
| E-034–035 | 5、6 | 领取拒绝/visual故障一致性 |
| E-036–037 | 2、3、6、8 | 单一驱动和唯一物理slot |
| E-038–040 | 4、7、8 | disable、Hold Collection和shutdown |
| P-001–006 | 2、3、6、8、9 | 自动MainCafe、20单、队伍/杯子 |
| P-007–011 | 3、4、6、9 | 动画、时间、恢复、满额 |
| P-012–016 | 7、9 | 装修与保留离场/主动Pause |
| P-017–020 | 6、8、9 | 多设备、session、旧模式、UI |
| P-021–024 | 4、5、6、7、8、9 | review新增的时序/事务/驱动边界 |
| M-001–022 | 8准备，9执行和记录 | Owner／授权Agent代测与复位，执行者分列 |

## 4. 本轮review结论与执行交接

- 独立review发现2项Important（Done时序、领取失败一致性）和1项Minor（Hold Collection共享anchor测试准备）；design/cases修订后，原三项已获独立文档复核关闭。未发现新的Owner玩法决定。
- 准备阶段：单一Step、独占物理slot、disable前checkpoint与shutdown边界作为本plan明确实现约束并补测试。当时review关闭仅在文档层面，功能与runtime尚未运行；实施后的实际结果以上方逐Task执行记录及Validation Report为准。
- 本plan准备阶段已做作者自检：每条D1–D6有Task，全部E/P/M有映射，公共types/method一致，全部新增文件列明。实施阶段逐项记录真实结果。
- 推荐由当前chat按`executing-plans`逐Task实施，因Customer/Employee/Decor共享状态较多，不适合多个实现者同时改；Phase末集中独立review。Owner也可另选subagent执行，但不能重叠写共享合同。
- Owner已批准当前chat逐Task实施并允许sub-agent；工作区与Unity入口已核对。继续未完成Task，Phase末进行集中review、完整regression与Owner manual acceptance；不将文档review或单项GREEN当作Phase完成。

- 2026-10-20 Owner追加授权Agent代测已执行结束：18项功能16 PASS、M-006／011 BLOCKED；四项客观检查M-007／021 PASS、M-017 BLOCKED、M-019仅UI部分PASS且Editor启动异常待查。逐项证据及helper前置修正见Validation Report／Guide；Owner列仍NOT_RUN，原Task9 Owner验收checkbox不代勾，新现场不以历史自动PASS关闭。本轮未修改产品规则、未重复完整suite、未进行Git publication。
- 同日Owner要求先修复M-006／011：实际前段Arrived后活动身体暂占下一段会同步Blocked，正式行为RED0P/2F。仅在CustomerFlow私有未发request阶段安全等候，保留candidate与原预算；实际腾位后继续，停止仍占位回原有限重算。新增两真实waypoint case及持续到店至门禁→恢复／离店／默认间隔case；修后受影响Edit157P/0F/0S、Play90P/0F/0S、CLI均0，独立窄QA CLOSED。普通Editor `B-20261020-154541`按原四Pause／首单前序完成实际五delivery／三杯、空间门禁、Resume后的补显示／FIFO／全排空及2.0377378游戏秒新到店间隔，M-006／011 Agent PASS。旧完整suite为修前历史快照，不重复全suite或代勾Owner验收，M-017／启动异常保持独立。
- Owner随后授权修正M-017／019：旧Guide第二CR(3,2)的顾客head落入口保留区，产品正确拒绝；保留记录GUID配方输入RED0P/1F，仅第二CR移(3,3)，新增CR／CM四ordinal组合覆盖4P/0F，未改runtime。M-019原工具过早Play缺少默认索引初始化；首次等自然delay45秒BLOCKED保留，最终工具在EditMode只调用一次Unity公开Search API完成真实首次初始化后才Play，无内部Setup反射／删cache／屏蔽异常。`G17-20261020-164313`普通新GUID实际完成中间Slot杯、正常旋转后新杯；cold indexFalse→真实indexTrue/db1后Play，Application Console Error／Exception／Assert0，两项Agent客观PASS，窄QA CLOSED。原Scene／Editor设置复位，Owner验收与Phase完整收尾不代勾，不重复无关测试或进行Git publication。

### Task9反馈补修 — warning与顾客持杯（2026-10-20，Owner已批准）

先保留既有业务合同，再补最小表现和有针对性的证据，不新开Phase或重复整套plan：

- [x] `CafeLoopPlaySessionTests.cs`替换obsolete Unity Find API，保留两DomainReload测试全部原断言；编译核对截图所示warning消失，两方法实跑通过。
- [x] 真实收银完成/成功Create后，验证同顾客在Waiting/Preparing时实际去已预留Pick-up排队；现有CreatePaidOrder已立即MovePickup，真实首次及最终均PASS，故不改Service。保留实际安全路径等待、FIFO及Counter实际离位释放。
- [x] 新增独立`CustomerCarryPresenter`，复用Shiba/Westie既有Carry/controller及Cup资源；`CustomerFlowController`仅接线canonical collection和spawn表现。实际核对发现P11模型缺P13 sockets，故新增两份P14顾客prefab复用accepted P13完整模型，移除EmployeeVisualPresenter且保留Nav/body原配置，不造新clip或retarget。P14 installer只读既有校准值，经Editor API更新Main及两P14验证Scene，不改旧P11/P13 controller/prefab或员工业务。最终builder9P/0F。
- [x] 先行为RED再最小实现/GREEN：领取前无杯，成功领取后恰一个无碰撞杯；两species真实Carry上身+actual-speed Walk/Hold，Pause冻结；堵路、disable-reenable及真实Done保留原Actor/杯/Total，Order清空不丢杯；同Actor实际出口清理，无重复Complete或额外领取耗时。两缺配置诊断RED0P/2F亦补齐，最终5项行为5P/0F；独立窄review CLOSED，无C/I。
- [x] 定向与直接受影响回归、普通Editor画面/状态检查，一次窄review；结果和源码边界补Validation Report及原task9-report。最终Edit166/Play71均0F/0S；普通Editor两species真实持杯/Pause/actual exit通过，root已看正面手杯补图，窄review CLOSED。未重跑完整suite，Owner观感不写PASS。

本次改动沿用原Task9，M-007历史客观PASS保留但不能代替新动作复测；M-013/014/018/021原持杯未覆盖部分需补证。Owner仍亲自确认动画观感；不commit/push/merge。

### Task9后续补修 — Pick-up多侧交互（2026-10-21技术验证完成，Owner待复测）

Owner澄清的目标：Pick-up每个外露且可达的方向都能交互；连着桌子、墙或其他家具的内侧不参与。Barista占一侧时，队首应能从其他合法空侧领取Ready商品；不等待员工回CM。仅剩同一侧可用时，仍按实际身体让位后交互。

调查：现有`InteractionAnchorResolver.ResolvePickUp`从四方向中固定选一对Employee/Customer anchor；`ResolvedStationAnchors`每个role只能一个，P14占位仍按这个单一服务目标处理。真实分离fixture已观察到Ready后2帧顾客领取、此时Barista仍Returning，因此不能把全部等待归因为Idle门禁；动态替代侧尚无支持。

Owner已明确回复“可以，就按这个改吧”。按以下小步实施，沿用现有worktree/Task9，不重复请求范围批准：

1. `InteractionAnchorResolver.cs`补全部合法方向候选接口，保留旧pair接口兼容P8/P11/P12/P13；测试桌子拼接、墙、旋转、多Slot及1–4外露边。
2. `PickupQueueService.cs`、`CustomerFlowController.cs`将交互占用细化到具体边，队首选择可达空侧，实际到位才领取，已选目标在该次交互中保持稳定；不改变单列paid FIFO/容量释放。实际拥挤RED证实原有限planner遗漏安全路线后，仅P14 wait移动新增私有`BuildPickupWaitRoute`，以最多四个正式侧检查同一全量障碍的完整native两段路径并保留原wait goal；其他路线和原24／32／3预算不变。
3. `CafeLoopController.cs`与必要的`EmployeeFlowController.cs`/runner接线使送达/领取各自持有对应侧，按实际离位释放，保留原task handle/checkpoint与真实身体检查。`EmployeeStationBindingResolver`仅增P14可选候选选择入口、旧调用不变；验证工具阻挡目标跟随实际selected target。静态孤岛实际RED后，`CafeLayoutRuntime.cs`、`LayoutReadinessEvaluator.cs`、`NavigationLayoutAdapter.cs`补显式P14 opt-in，让domain及owned Nav readiness使用完整候选侧，旧默认双anchor语义保持。不同侧可并行，同侧不能重叠。
4. 先真实RED再最小实现/GREEN：Barista仍停在一侧、另一侧空闲即可取餐；原Customer侧被挡时可用第三侧；旧首选侧孤岛而备用侧完整可服务时，P14 readiness也使用真实候选，旧默认门禁不改。仅一侧时安全等待，含两侧中一侧动态占用，不能让Preparing顾客堵住唯一交付侧；员工路线须避开顾客实际remaining route。回归FIFO/无重复/无穿人、Pause/Done及当前手杯生命周期。更新现有design/test cases/manual，不新建第二套plan。

直接回归发现三项问题后追加最小修正：P14 opt-in须在首个业务派发前使旧readiness publication失效，并沿原合法重建验证，不留到无改动Decor Done才发现policy变化；员工layout观察沿用同一候选模式。多设备布局的两条原生直达路线均被原Cashier身体阻挡，而同一全量障碍存在安全多段路径，故仅P14 delivery腿允许实际逐段消费已验证的有限绕路，固定最终side／lease／handle，末段真实到达才交付；逐段发前复核、暂停／停用恢复、迟到callback与清理沿原身份规则，旧P13默认及return腿不扩展。PendingExit测试仅提早真实第一客exit活动期间的暂停窗口，保留真正Pending、request0和全部冻结／代次／预算／两客exactly-once断言；旧P13动画test独立设置并恢复原clock，不改生产时间语义。

实施证据约束（已按下述结果完成）：多侧行为在实际RED/GREEN前不记PASS。原分离fixture独立领取时序1P/0F只说明既有不同侧并行基线，动态备用侧、按侧保留与单侧退让由本轮另证。实现者独占源码与Unity运行，root同步文档并沿同一窄review收口；不commit/push/merge。

原自然Continued实流另发现领取目标抢占后客活动绕行点：full5成立但恢复领取后后客Blocked。仅`CustomerFlowController`在Acquire collection前比较本次完整route与其他真实moving pickup remaining corridor，排除自身、保留全body／owned目标；冲突候选普通等待或换侧，不拿lease／不改状态。通过的同一route交给原逐段移动，保留原context初始化／预算及全部原自然实流断言，修后重跑直接范围与普通Editor单组。

完成证据：最终runtime CFC04:42:02Z，`pickup-sides-final4-edit.xml`289P/0F/0S、`pickup-sides-final4-play.xml`129P/0F/0S，加同源`pickup-sides-collection-corridor-green.xml`原自然Continued1P，130唯一case／重复0；该7项其余6不加总。边界含旧首选孤岛、动态第三侧、静态单侧、两侧中一侧动态占用及原FIFO／Pause／Done／恢复／持杯。独立窄review CLOSED，无残留C/I；retired-id guard独立oracle的Minor限制保留。普通Editor `Sides-20261021-045308`实际Completed时员工距原交付侧.02832985m（arrival .08m）且同lease仍在，客人从East真实领取；正常恢复后实际出口／三capacity0。Owner视觉验收、Player/device/CI及Phase收尾未因此完成，不commit/push/merge。

最终cleanup已完成，`pickup-sides-final-cleanup.json`核Editor0／临时helper两文件归档移除／原GameView与EditorOptions恢复／旧资产和Scene字节保护；main tracked clean，本worktree未提交。Root同步七份living docs与本Task9原report，工作交回Owner按Guide复测，不进入下一Phase。

### Task9 Owner复测补修 — Shared首客导航转角停顿（技术完成，Owner待复测）

Owner复测`Phase14SharedPickup`首客付款后仍在CR附近等到咖啡接近Ready；原普通Editor横竖对照确认付款即发移动，但距CR约.552m的同一native corner停约3秒。追加只读probe证实静态avoidance将朝原corner的位移裁近零，NoProgress达到原3秒阈值后同request内部唯一retry重算才恢复。不是订单Ready门禁或竖屏专属；另一Scene中有前客时先去队尾再前移由Owner确认，属于正常FIFO，不改。

沿Owner此前“如果有问题那就修复先”的授权及原Task9继续小步修复，root已先说明文件范围：只在`NavMeshMovementDriver.cs`已有private steady corner处理内改进安全候选选择，并在`CafeCustomerCarryTests.cs`补Shared首客实际持续离开Counter的regression。先复现正式RED，避免仅以付款后>.1m起步冒充无停顿；最小修复须沿原完整NavMesh与碰撞证书，保留半径／skin／arrival／3秒NoProgress／唯一内部retry／真实Counter腾位规则。无Scene／prefab／Save迁移，无游戏设计变更或下一Phase，不增加通用路由框架。

先给出录制场景RED与实际候选失败层，再实施最小private修正。新Shared case、旧P11相关实际转角／身体保护用例及P14/P12/P13直接cohort按最终源验证；独立QA只读核完整安全证书、有限预算和旧兼容，不将旧289/130冒充新修后结果。沿用原report／Guide记录失败与Owner复测，不新建第二套计划，不commit/push。

实际完成：正式Shared RED0P/1F（原客3.85游戏秒才释放CR／retry1／BaristaCarrying）→private四个offset候选且每次完整认证→focused34P/0F；追加SameAs断言后最终直接Edit334P/0F/0S、Play168P/0F/0S＋原自然Continued1P/0F/0S，共169唯一Play全部PASS。两种速度各20单真实出口／原token证据已同轮更新。独立窄QA复核patch与最终168：CLOSED，无C/I，两个Minor与边界见Validation Report。

普通Editor唯一竖屏补证`PortraitProbe-20261022-035755`：capture0、自然1×，首客Create后.832131704游戏秒实际腾出CR、距离.992961m、原service request5/gen1/retry0，Barista仍Working余2.159036秒；同帧截图已由root查看，随后同首客正常Completed／离场／原三reservation Released。最终精确清理另记Validation Report。Owner历史FAIL及待本人修后验收不自动改PASS；Guide已给四组具体复测步骤，不重跑全部manual或跨Phase。

### Task9 Owner追加委托 — 四组功能代测（功能完成；仅visual待验）

2026-10-22 Owner明确要求“四组帮我测，只留下visual”。沿用原用例，五个ordinary Editor组Carry／Sides／G17／ReviewUI／ReviewDone的八项功能记录PASS：双species真实单手杯／Pause／离店，多侧原delivery lease仍占时另侧领取，新GUID多设备中Slot及合法旋转后新单，两尺寸真实UI事件／Camera／Snapshot，以及正常Done保留原已领Actor／同杯／pose／Total、主动Pause跨Done。Shared.832秒与原334／169按同源证据复用，不重跑NUnit／manual22；本轮不改产品／永久tests／正式Scene或prefab。工具接待前置UNKNOWN、视口瞬态、错误legacy按钮三次中止均保留并与最终成功分列，详Validation Report。Owner只看Guide三项visual，不再要求本人搭四组功能现场；未改变Phase／release／版本管理授权。实际清理与根目录tracked状态以本轮receipt和root读回为准。

### Task9 Owner新布局反馈 — Pick-up队形并排（技术验证通过，Owner visual待验）

Owner截图为自摆连续长柜、CR在左端、Pick-up靠右。Owner明确已关闭Unity并要求Agent复制尝试，沿原“有问题先修”的授权继续原Task9。先保全用户最新Editor.log及现有文件，在普通Editor仅运行时按图构建近似蓝图，经正式Decor确认、真实付费和Hold Collection积累已到位顾客，记录完整站位链及实际forward。图片没有原Grid坐标，不冒称逐格复原；先排除Counter／已取餐离场者／移动中角色混淆，再区分布局规划折行和等候朝向遗漏。仅截图不能判定具体根因；本阶段产品不改。若证实违背原单列FIFO／队形表现，先补真实失败测试、说明CFC与相关test最小范围再修，不改旧Counter规划、身体／Nav安全、容量或多侧领取规则，不新建第二套plan／Phase。

复建阶段已完成：`LongQueue-20261023-004134`两位真实Paid／Arrived／request0在横向slot链仍统一朝+Z，后客朝前位+X的dot仅.05320，Facing遗漏已证实；首步直后方可走不等于整条直行队列能满足容量，本轮不判plannerbug。root已向Owner说明并授权原Task9最小范围：CFC private PickupWaitTarget读取该Visit的PickupOwner slot链，初次转队与每次前移两入口消费；真实Actor站稳、前移后新队首及90°布局先RED再实现。队首、实际服务侧Facing、slots／FIFO／容量／lease／Nav规则不改。正式结果完成后更新原report／Guide，旧Current八项不当新修复证据。

已执行`pickup-facing-red.xml`0P2F，两个controlled port真实Nav case在settled follower方向dot .078/.084失败；最小private修正后整个CF11P0F0S。测试实走领取侧、上层授权后实际离位，再验原第二／第三实际前移的新head／follower方向。90°是service geometry重规划，不冒称整Grid Decor或完整Paid业务。独立窄QA已核源码与RED/GREEN无C/I，最终直接cohort与同长柜native补证完成前不标最终CLOSED。范围仍原Task9，未改旧Counter、容量、slots、导航阈值或序列化数据。

最终直接Edit252P0F0S、Play132P0F0S＋原自然Continued1P0F0S，共133唯一Play全部PASS；两档真实20单TSV同源刷新。窄QA另核最终252／132与源码freeze：CLOSED，无C/I；自然长项及native由执行者与root分别读回。普通Editor`LongQueue-20261023-005629`capture0、OSexit0，同修前七个slot坐标不变，两实际Paid站稳后后客向前位dot=1；正常ResumeCollection、原首客实际离服务位后，原第二真实变head且服务朝向dot=.996758。root查看两张近景并核身份／目标／实际pose，三图入口已加Guide。不强称所有站位直线或Owner逐格蓝图相同；Owner仅看visual，未commit/push/merge。

<a id="task9-delivery-corridor"></a>

### Task9 Owner批准补修 — 排队封住送杯通道（技术验证完成）

Owner明确批准上述四层方案，收银时间先不改。现有EnsurePickupQueue只逐点避开服务侧，不证明整队占位后仍留出连续送杯／返回路线；本轮先用真实Nav／业务fixture建立可信RED，不能仅以源码推断替代运行复现。Engineering为唯一C#/Unity执行者，root维护现有Docs，原QA做本范围独立检查；不新建第二套plan、不改变旧P12/P13或Nav安全参数、不改Scene／prefab／Save。

- [x] 补可信RED：预留通道、真实静态waiter门禁、无处让位、多人共同封路、真实未来交叉及复位ownership遗弃均已复现；原多侧与wholeowner直接focused通过。fixture前提失败与可信RED分列Report。
- [x] 最小实现：规划保留至少一条完整送杯／返回通道；沿用有限备用侧／绕路；本系统站稳等候者有限实际让位，保持原FIFO／订单／capacity及owned request身份。返程实际消费所验证route；Ready安全早取不再绑CM，临时交通待真正腾空。
- [x] 无安全空间或预算耗尽时明确可恢复Blocked；原Prepared不重做、未实际到侧不Ready；相关Pause／disable／Done／迟到回执清理保持。
- [x] 跑focused GREEN及直接相关Edit／Play；复核实际付款、送达、领取、前移、真实出口与原token释放；按结果选择必要普通Editor代表补证。
- [x] 原QA独立核patch与实际XML，修复C/I；同步Report／Guide／Roadmap当前源码结果及必要Owner新visual。
- [x] 完成修复后执行Owner追加的家具布局矩阵L-01～L-16（原Test Cases）：外露侧／旋转／长柜／狭廊／边界／多设备与队满交通、可让位／不可让位／生命周期；正式摆法可复现，逐项绑定实际case与结果，不把计划或预期拒绝自动记PASS。

最终证据（2026-10-23）：`delivery-corridor/final2-edit.xml`256P0F0S，`final2-play.xml`163P0F0S＋原`final2-natural.xml`1P0F0S，共164唯一Play全部Passed；六runtime／四永久test与freeze一致，旧focused不重复累计。17个Matrix结构变体及同批动态case覆盖原16分类，配方／分层限制见Test Cases。保留first-final162P1F及可信return-traffic RED；最小pending补修仅等相关实际活动交通、原返程目标不变、Pause不发请求、Cancel退休清理，原QA最终窄review C/I CLOSED。两档各20真实原单三milestone各一次、实际出口／容量杯句柄请求全部排空。ordinary `Corridor-20261023-042217`真实Paid让位／暂停／实际送杯返CM／原slot恢复／领取出口PASS，图见Guide；OS ExitCode=null保持UNKNOWN，root另核完整Editor路径进程0。临时工具与旧资产清理receipt见Report，未将Owner新visual或Phase最终收尾记完成，不commit／push／进入下一Phase。

Ruling：用户已批准新排队／让路行为，原“保持slot不变”仅限上一轮Facing窄修复，不阻止本轮按安全验证产生新等候／让位目标；FIFO、身份及实际身体／容量规则仍不变。收银2秒／制作3秒的全局约束继续有效。没有自动清空客人、取消订单或让顾客穿人的授权。

<a id="task9-single-file"></a>

### Task9追加：自然单列、P14一秒收银及多人布局验证

Owner于2026-10-23已明确批准上一条中文方案并要求开发和测试。本追加覆盖旧P14两秒要求，旧执行结果不回写成新规则PASS。Engineering唯一C#/Unity owner，root写现有Docs和ledger，独立QA核增量与证据。继续当前worktree／原计划，无commit／push／Phase跨越。

**文件与接口：**
- `Assets/Scripts/Customers/CustomerFlowController.cs`：仅P14 Pickup规划选择自然单列；保持现有完整Nav／身体／服务通道callbacks。
- `Assets/Scripts/Customers/CounterQueuePlanner.cs`：如确有需要，增加默认关闭的显式规划选项，旧调用行为逐字保留；或新增小型internal `PickupQueuePlanner.cs`＋meta封装独立策略。工程检查后选择较小且能保持旧合同的一种，记录理由，不做两套重复逻辑。
- `Assets/Scripts/Employees/EmployeeFlowController.cs`：经现有`EmployeeTaskRunner(..., float? workDurationSeconds, ...)`入口选择P14 Cashier1秒。Runner共享默认Cashier2／Barista3不改，checkpoint恢复必须采用相同配置。
- `Assets/Scripts/CafeLoop/CafeLoopService.cs`：`ScheduleCashier`创建P14初始checkpoint也设1秒，保持与实际runner接受／恢复一致；只改P14入口，非共享默认。
- `Assets/Scripts/Employees/EmployeeTaskRunner.cs`：保持共享默认时长不变；为避免前移封住已选返程，只读发布Carrying／Delivered期间从选中交付侧开始的完整home，Returning从真实pose开始发布remaining home；内部provider明确origin语义、退休清理，不把当前outward pose直接连home当实际选路。
- 为验证真实转弯原因，允许Planner可选只读观察callback记录原gate拒绝及原始prefix，CFC保留有界最近记录／重配清理。默认null、不增加候选预算／不复制安全gate，不增加玩家UI／serialized字段；matrix按最终选中prefix匹配，不能只打印固定原因或以Facing代替。
- 测试沿用P14 `CafeEmployeeCheckpointTests.cs`、`CafeCustomerFlowTests.cs`、`CafeFurnitureLayoutMatrixTests.cs`／`CafePickupSideTests.cs`；纯规划新断言放P14 EditMode现有或一个专用小test文件，不修改旧P12/P13预期迎合新行为。

**Review Focus：** 相同候选在straight与横向大容量之间取舍；第一步即因真实服务通道拒绝的合法转角；四旋转及长柜slot.1；短队容量与已付费ownership；P14一秒在Pause／2×／disable恢复及P13两秒隔离。

- [x] **A RED→GREEN：自然单列。** 补一个当前规划会为更多slots舍弃安全直线prefix的独立几何RED；断言已验证安全直行不能被侧向支路替代。补直行确实被挡才转弯、容量不足安全短队、四向等价及旧默认Counter直接回归。真实Nav至少覆盖长柜多人稳定pose／间距／队列方向及取餐后前移，不能只检查Facing或注入slots／Actor pose。实现保留原有限预算、完整路径／身体／corridor guards；若原布局已有正确队形，不人为制造产品FAIL。
- [x] **B RED→GREEN：P14收银1秒。** 真集成runner/owner验证.99秒不Finished、再.01完成且付款一次；Pause不扣时、2×只缩放一次、Working中disable／恢复保留剩余值。旧独立Cashier仍2秒、Barista仍3秒，真实订单与checkpoint恢复相容；同一流程只显式调整P14入口，不全局替换数字2。
- [x] **C 多人矩阵与最终直接回归。** 扩展原17个结构变体的合法家具配方，目标稳定2／4／6实际Paid等待者，记录各实际人数／物理slot上限。完整body／slot/generation／FIFO／Facing／间距，发生转弯时记录原因与合法直行前提；不足人数记录CAPACITY_LIMITED或实际原因而非假PASS。共用fixture复用case／TestCase，不复制大量body。暂时yield不以永久队形断言误判。恢复Collection后真实前移／领取／出口／原token释放。无解L14／16继续预期Blocked／拒绝及恢复，生命周期／通道旧case直接回归。最终跑全部P14 Edit／Play＋所触及旧Counter／P13／Nav直接class，原自然长项单列，普通/加速各20单；不机械重跑完整项目suite。
- [x] **D review、普通Editor与交付。** 给独立QA同任务相对本轮baseline的完整patch、实际XML与人数结果，不用整个HEAD旧P14差异淹没新改动。修复本范围C/I，给长柜／转角至少一组普通Editor多人稳定及前移截图，Agent客观功能与Owner观感分列。保存原Scene／prefab／旧自动资产，只清理本轮helper，恢复Editor设置。同步现有Design/TestCases/Report/Guide/Roadmap与ledger，保持未提交。

执行输出使用`outputs/phase14/single-file/`。顺序A→B→C→D；工程可将A/B focused在同一Editor批中执行但须分别保留有效RED与GREEN，最终冻结后再直接回归。没有安全路、显式Blocked或已证实容量上限都要记录实际断言，不可仅凭status文本PASS。

执行中必要小修边界：SteadyPathMotion的当前accepted-owned request可在native hasPath=false时继续有界运动，交通判定依真实request/gen/issued segment/remaining route，Cancel／Blocked／失效不算；不授权任意pending0。后客前移同时保护员工已选完整返程，已领取Exiting仍持原服务lease时只按有效accepted exit的实际释放事实短暂等待。队伍创建须对front vacates head后的future tail验证至少一个真实可领取且能退出的侧，安全领取仍按原approach／FIFO／generation／lease／返程通道条件；不能普遍要求领取当帧完整出口通畅。已canonical领取后的出口派发／未发segment若仅被可独立完成的accepted交通暂挡，应保留原Completed／body／token并有限等待；静态无路则原本明确Blocked及Done保留。可用最多4条Grid/root推导边缘候选、每条≤3 waypoint，缓存原Nav owned endpoint，沿用原完整路径／身体／waypoint预算。若初始规划找不到安全路则在收费前缩短真实队列，不清人、丢单或释放仍占用token。这些是原完整链／安全回归目标所暴露的必要修复，不改旧P12默认导航或容差。

Ruling（review round2）：曾尝试的普遍领取前出口门禁及Ready-query桥接被实际旧生命周期回归否定，撤回该实现方案，以上canonical后精确交通等待为准。保留三个CollectedBlocked／PendingExit与HeldReady-beforeCM旧断言，不能调整fixture来掩盖语义变化；完整退出路线证书只用于安全规划／精确等待分类，不授权穿人或提前释放。

恢复策略一致性（round3）：空Route恢复、首次和有界重算统一P14边缘规划策略，旧P12保持。已选未发下一腿被相关accepted身体暂挡时可保留候选／预算等其独立通过；必须有完整native nextPath及该body单独造成的阻挡事实，不能因任意远处活动者也有安全反事实出口就静候。空Route时则需实际完整出口无路、仅去该相关body后完整出口成立。仍保留其future slot、原有限独立request及所有实际派发guard；静态无路明确Blocked。两处受控真实Nav入口单列验证，不混同19个自然家具case。

本追加最终交付（2026-10-23）：final3b Edit267／Play171均0F0S；19原matrix/rapid的57人数收据与2个controlled恢复分列，原自然长项已含171，Normal/Fast各20原单实际出口并排空。源五runtime＋八改tests＋新planner/meta共15，freeze及精确copy一致；原QA自动化与D证据复核见task9-single-file-review。ordinaryEditor六Paid站稳／原第二前移1.1m／六原body真实出口PASS，同次默认fixture自然三单PASS；Owner只补Guide中新折返／前移观感，旧三项visual PASS保持。root独立23:20:18Z核317protected含13Scene全一致，3项已证明自动delta先raw/diff归档再原字节恢复，临时helper两文件归档且absent，完整Editor路径进程0；GameView8/12／OptionsFalse/None按native实际readback。旧5/5计数不套入本轮。结果／容量边界／自然峰2/1及3单采样限制已同步七份living Docs，所有功能工作在原worktree未提交。
