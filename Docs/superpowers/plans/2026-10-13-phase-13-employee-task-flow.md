# Phase 13 — Employee Task Flow Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. 执行方式由 Owner 审阅后选择；本项目采用 Task focused、Phase 集中 review/manual 的流程，不新增每 Task 的完整验收。

**状态：** Approved for implementation — Owner 于 2026-10-14 明确要求开始开发并使用 subagent；执行位置固定为本计划的 E 盘 worktree。

**Goal:** 让两个固定员工用已准备的 working/carry 素材执行受控服务与制杯任务，安全完成放杯、返回和装修恢复。

**Architecture:** `EmployeeTaskRunner` 管理单员工单 handle 与计时，Scene controller 把已批准的 binding、Navigation、Cup display 和动画连接起来。员工拥有独立身份/生命周期，桌面杯是最多三个的纯视觉；NavigationWorld 仍是唯一位移执行者。沿用当前 namespace/asmdef，不建立通用 task framework，不连接真实订单。

**Tech Stack:** Unity `6000.5.5f1`、C#、现有 AI Navigation `2.0.14`、Unity Test Framework `1.7.0`/NUnit、Blender 只读动画导出；不安装或升级 dependency。

**Spec:** [Approved Design](../specs/2026-10-13-phase-13-employee-task-flow-design.md)；[完整 Test Cases 与 Manual 步骤](../specs/2026-10-13-phase-13-employee-task-flow-test-cases.md)。两者与本计划一起阅读。

## Global Constraints

- 工作区：`E:/Unity/Project/AnimalCafe/.worktrees/phase-13-employee-task-flow`；branch：`codex/phase-13-employee-task-flow`；基线：`302f3e969da667f3f389820d303503865b1b0d63`。
- Cashier 在 P12 选中 CR 的 Employee Side 待命；服务 **2 个游戏秒**。Barista 在 CM `+Z` 正面待命；制作 **3 个游戏秒**。两者 working，Barista 去程持杯、到位即放、空手返回。
- 每 table module 最多3个三角形杯子；更多交付仍成功，不限制制作，不维护真实待取餐库存，不操作 P9 Order/P10 Capacity。
- 放杯是制作/交付成功点；Returning 仍忙，实际回 CM 才可开始下一杯。不增加自动 task queue。
- 所有计时消费已缩放的delta一次；Pause冻结动画/移动/计时；2x同时作用。不改 `IGameTimeService`。
- 一员工一个handle；同步callback先建立上下文；旧session/generation/request/layout callback不能提交成功；cleanup exactly-once。
- 无修改的Decor退出继续原任务；确认修改取消旧任务/手杯、保留员工身份岗位、步行回位；station缺失/不可达明确Blocked。员工不参加Customer reset。
- 不瞬移、不穿墙/整身穿人、不放宽角色尺寸、CollisionSkin/到站容差或已有导航失败规则；Recovered不是目标Arrived。
- 素材母版仅从主checkout只读访问；产物仅写P13 worktree；不改锁定MasterRig、已接受P11默认Walk或原Cup Prefab。
- Scene/Prefab/Controller/Mask由Unity Editor API/CLI生成；不手写YAML，不保存正在Play的Scene或用户dirty Scene。
- Task focused +直接regression；Phase末集中全suite、Engineering/QA及必要Art技术review和Owner manual。Editor PASS不代表Android/iOS真机。
- 本计划不授权commit/push/PR/merge、删除branch/worktree、cloud CI登录/dispatch、安装或发布。新classes/meta由正常Unity导入生成，保留所有无关改动。

## Review Focus

1. **零游戏时间收到到达callback：** 暂停期间不放杯；合法动作恢复后一次处理。Task2/6固定E-036。
2. **交付visual创建失败：** 不提前更新count/成功，不丢手杯后误报成功；饱和3杯仍须验证正式Slot有效。Task3/5固定E-034、P-017。
3. **Scene对象还在但正式绑定已删除：** 按Layout/InstanceId/Slot判断，不使用陈旧representation。Task1/6固定E-037。
4. **cleanup重入与新session：** 先退休handle、清外部subscription；老callback不能创建杯或抢新任务。Task2/7固定E-033/E-035、P-015。
5. **空MainCafe与多Slot/坐标转换：** 空店仍可装修；有效布局才生成员工；旋转root与多格桌不串Slot。Task3/7固定E-038、P-016。

## 文件结构、接口与数据约定

以下都是计划文件，不代表已经创建。先完成各Task可信RED再写对应实现。

| File | 单一责任 |
| --- | --- |
| `Assets/Scripts/Employees/EmployeeContracts.cs` | Role/State/Outcome/visual mode、immutable binding/snapshot，以及小型runtime port |
| `Assets/Scripts/Employees/EmployeeStationBindingResolver.cs` | 按station capability、P12 register与完整可达性选择稳定绑定 |
| `Assets/Scripts/Employees/EmployeeTaskRunner.cs` | 单员工任务状态、2/3秒计时、成功/取消/回位与callback所有权 |
| `Assets/Scripts/Employees/PickupCupDisplay.cs` | 按SurfaceSlotAddress显示0～3杯；支撑局部坐标、有效性与清理 |
| `Assets/Scripts/Employees/EmployeeVisualPresenter.cs` | 唯一员工Animator状态选择者与手持杯；复用working和upper-body carry |
| `Assets/Scripts/Employees/EmployeeFlowController.cs` | 两员工runtime生成/注册、runner runtime port、GameTime/Decor/Navigation接线 |
| `Assets/Scripts/Employees/EmployeeTaskValidationController.cs` | 仅受控验证：两个task按钮、状态与Inspector helpers |
| `Assets/Scripts/Navigation/NavigationWalkPresenter.cs`（修改） | 增加员工opt-in外部动画驱动与真实速度通知；默认顾客路径保持 |
| `ArtSource/Phase13/Tools/ExportEmployeeAnimations.py` | 只读既有Walk/Work/Carry `.blend`，导出员工FBX，排除自带CarryProp_Cup |
| `Assets/Editor/Phase13/Phase13EmployeeAssetBuilder.cs` | Unity导入FBX、生成员工Controller/Mask/Prefab、验证挂点 |
| `Assets/Editor/Phase13/Phase13EmployeeTaskSceneSetup.cs` | 幂等构建独立验证Scene与MainCafe P13接线 |
| `Assets/Art/Phase13/Employees/{Shiba,Westie}/` | `SM_{Species}_Employee_Walk.fbx`、`SM_{Species}_Work_Generic_Busy.fbx`、`SM_{Species}_Carry_Cup.fbx`、`AC_{Species}_Employee.controller`、`PF_{Species}_Employee.prefab` |
| `Assets/Art/Phase13/Employees/AM_Employee_CarryUpperBody.mask` | 通用上半身Mask；root/pelvis/腿不被carry覆盖 |
| `Assets/Scenes/Validation/Phase13EmployeeTasks.unity`（新增）、`Assets/Scenes/MainCafe.unity`（接线） | 独立验证后才改MainCafe；原P11/P12验证Scene不重建 |
| `Assets/Tests/EditMode/Phase13/` | `EmployeeBindingTests.cs`、`EmployeeTaskRunnerTests.cs`、`EmployeeLifecycleTests.cs`、`PickupCupDisplayTests.cs`、`EmployeeAssetTests.cs`、`Phase13SceneBuilderTests.cs`、`EmployeePlaySessionTests.cs`、`EmployeeTaskTestSupport.cs` |
| `Assets/Tests/PlayMode/EditorSceneLoading/Phase13/` | `EmployeeFlowTests.cs`、`EmployeeAnimationTests.cs`、`EmployeeDecorationTests.cs`、`EmployeeMainCafeTests.cs` |
| `Docs/Phase13_Beginner_Guide.md`、`Docs/Phase13_Validation_Report.md`（后续新增） | 真实菜单/Inspector操作、最终counts与Owner结果，不预填PASS |
| `Docs/AnimalCafe_Project_Design.md`、`Docs/AnimalCafe_Development_Roadmap.md`（必要更新） | 记录已批准的P13范围与真实进度、修正活动入口；保留历史阶段快照 |

Contracts的最小类型：`EmployeeRole { Cashier, Barista }`；`EmployeeState`沿用spec §3.2；`EmployeeTaskOutcome { None, Succeeded, Failed, Cancelled }`；`EmployeeVisualMode { Idle, Walking, Working, Carrying }`。

- `EmployeeStationBinding`只读属性：`EmployeeRole Role`、`string WorkStationId`、`string PickUpId`、`NavigationTarget WorkTarget`、`NavigationTarget? PickUpTarget`、`SurfaceSlotAddress? CupAddress`、`int LayoutRevision`。Cashier后两个nullable为空；Barista必须完整。
- `EmployeeTaskSnapshot`只读属性：`string ActorId`、`EmployeeRole Role`、`EmployeeState State`、`long HandleId`（0表示无active handle）、`EmployeeTaskOutcome Outcome`、`float RemainingWorkSeconds`、`bool Delivered`、`string Failure`。
- `IEmployeeTaskEnvironment`：`bool IsProgressAllowed { get; }`；`MoveStartResult MoveTo(NavigationTarget target, Action<MovementResult> completed)`；`bool CancelMove(long requestId)`；`bool TryShowHeldCup(out string reason)`；`bool TryPlaceCup(SurfaceSlotAddress address, out string reason)`；`void SetVisualMode(EmployeeVisualMode mode)`；`void CleanupTask(long handleId)`。port由EmployeeFlowController内的小型adapter实现，每个adapter只服务一位员工，不做service locator。
- cleanup不能删除桌面杯；TryPlaceCup返回true才提交交付。状态、request IDs和task handle保持单线程拥有。

## 验证命令与Task约定

执行前读取 `unity-cli` skill，并用本机 `unity test --help` 核对参数。以下沿用现有项目CLI形式，是计划命令，本轮未运行。工作目录始终为P13 worktree；不把CLI、license、fixture或编译环境失败当行为RED。

```powershell
unity test . --mode EditMode --filter AnimalCafe.Tests.EditMode.Phase13 --output outputs/phase13/focused-edit-green.xml --timeout 600
unity test . --mode PlayMode --filter AnimalCafe.Tests.PlayMode.Phase13 --output outputs/phase13/focused-play-green.xml --timeout 1200
```

每Task将filter缩到下面指定class，输出用`taskN-red.xml`、`taskN-green.xml`、`taskN-regression.xml`分开。Play测试namespace统一`AnimalCafe.Tests.PlayMode.Phase13`，Edit统一`AnimalCafe.Tests.EditMode.Phase13`。Task内不跑完整旧phase suite；直接regression class列表用分号filter（执行前核对CLI支持），否则分别运行，不并发Unity。

每Task：RED →最小实现→GREEN→直接regression→在本plan勾选已完成步骤并简记证据。无授权不执行skill模板中的commit。

## Task 1 — 岗位合同与稳定Station binding

**Files:** 创建`EmployeeContracts.cs`、`EmployeeStationBindingResolver.cs`、`EmployeeBindingTests.cs`。

**Interfaces:** resolver produces `bool TryResolve(EmployeeRole role, LayoutReadinessReport report, string selectedRegisterId, int layoutRevision, Func<InteractionAnchor, NavigationTarget> toTarget, Func<Vector3, Vector3, bool> canReach, out EmployeeStationBinding binding, out string reason)`。Cashier选中ID存在时必须使用同CR；为空时沿P12 ordinal InstanceId规则。Barista按CM ID→Pick-up ID顺序筛完整Employee Anchor路径。当前布局无可用组合返回false与可读reason，不制造默认成功binding。

- [x] 写`SelectedRegisterWinsRegardlessOfEnumerationOrder`、`BaristaChoosesFirstReachablePair`、`DeletedPickupRepresentationCannotKeepBindingAlive`（E-001/E-006/E-007/E-037）。构造两个CR、两个CM/Pick-up并倒序输入；断言selected CR不变、不可达首候选被跳过、缺正式station失败。
  ```csharp
  Assert.That(cashierBinding.WorkStationId, Is.EqualTo(selectedRegisterId));
  Assert.That(baristaBinding.PickUpId, Is.EqualTo(reachablePickupId));
  Assert.That(missingStationAccepted, Is.False);
  ```
- [x] 运行`EmployeeBindingTests`确认可信RED。
- [x] 在列出的两个runtime files最小实现immutable合同/验证与resolver；纯binding只查正式Layout和路径，不生成actor/Cup。
- [x] 同class GREEN；直接regression：`AnimalCafe.Tests.EditMode.Phase8.LayoutReadinessEvaluatorTests`、`AnimalCafe.Tests.EditMode.Phase8.InteractionAnchorResolverTests`。退出条件：顺序稳定、binding含真实IDs/revision、失败明确。

## Task 2 — 单handle任务流程、时间与callback

**Files:** 创建`EmployeeTaskRunner.cs`、`EmployeeTaskRunnerTests.cs`、`EmployeeTaskTestSupport.cs`；完成Contracts中的port/snapshot。

**Interfaces:** `EmployeeTaskRunner(string actorId, EmployeeRole role, IEmployeeTaskEnvironment environment, float? workDurationSeconds = null)`；`EmployeeTaskSnapshot Snapshot { get; }`；`bool TryAssignBinding(EmployeeStationBinding binding)`；`bool TryStart()`；`void Tick(float scaledDeltaTime)`；`void Cancel(string reason)`；`void InvalidateLayout(int revision)`；`void TryReturnToWork()`；`void Dispose()`。duration默认Cashier2/Barista3，配置须positive finite；Tick无效delta抛ArgumentOutOfRangeException且状态不变。Fake environment定义同步/延迟Move结果、held/placed cup计数、CleanupCount以及模拟progress gate，全部只存在test support。

- [x] 写`CashierCompletesAfterExactlyTwoGameSeconds`、`BaristaRequiresPickupArrivedThenActualReturn`、`BusyEmployeeRejectsSecondHandle`（E-002～E-005/E-009～E-012/E-020）；fake在工作位同步Arrived，Barista的交付/返回callback分别受控触发。
  ```csharp
  Assert.That(runner.TryStart(), Is.True);
  Assert.That(runner.TryStart(), Is.False);
  runner.Tick(1.99f);
  Assert.That(runner.Snapshot.Outcome, Is.EqualTo(EmployeeTaskOutcome.None));
  runner.Tick(.01f);
  Assert.That(runner.Snapshot.Outcome, Is.EqualTo(EmployeeTaskOutcome.Succeeded));
  ```
- [x] 写`SynchronousCompletionCannotOverwriteNextLeg`、`RecoveredIsNotDelivered`、`StaleSessionCannotPlaceCup`、`ReentrantCancelCleansOnce`、`PausedArrivalDefersDelivery`、`ReturnFailurePreservesDeliveredOutcome`（E-013～E-019/E-033/E-035/E-036）；分别断言place count=0/1、cleanup count=1、返回未到时State不是Idle。
- [x] 跑`EmployeeTaskRunnerTests`确认RED。
- [x] 最小实现runner。提交move前建立session/generation/leg token；同步callback可先得到request ID，MoveTo返回后仅在同leg仍live时保存ID。取消先退休handle再调用CancelMove/cleanup；合法暂停callback保存待处理动作，Tick有正delta且gate允许才推进，失效callback丢弃。
- [x] GREEN；直接regression：`AnimalCafe.Tests.EditMode.Phase11.NavigationServiceTests`、`AnimalCafe.Tests.EditMode.Phase10.CapacityOrderBoundaryTests`。退出条件：计时/重入可重现测试通过，无Order/Capacity副作用。

## Task 3 — 最多三杯的module展示

**Files:** 创建`PickupCupDisplay.cs`、`PickupCupDisplayTests.cs`。

**Interfaces:** `void Configure(GameObject cupPrefab, Func<SurfaceSlotAddress, Transform> resolveConfirmedModule)`；`bool TryDeliver(SurfaceSlotAddress address, out string reason)`；`int VisibleCount(SurfaceSlotAddress address)`；`void RefreshConfirmedSupports()`；`void Clear()`。resolve函数返回正式家具Slot的Transform，不取Preview sign；missing/invalid slot返回null。display用address→最多3个owned objects；新杯创建成功后才增加count。第4杯起验证正式module仍有效后返回true且不再创建对象。

- [x] 写`TenDeliveriesShowOnlyThreeCups`、`ThreeOffsetsAreTriangular`、`FailedInstantiationDoesNotCommitCount`、`DifferentModulesDoNotShareCups`、`MovedSupportKeepsLocalPlacement`（E-021～E-025/E-028～E-030/E-034/E-037，P-016的坐标风险先做Edit fixture）。
  ```csharp
  for (var i = 0; i < 10; i++) Assert.That(display.TryDeliver(address, out _), Is.True);
  Assert.That(display.VisibleCount(address), Is.EqualTo(3));
  Assert.That(visibleCupObjects, Has.Count.EqualTo(3));
  Assert.That(Vector3.Cross(offsets[1]-offsets[0], offsets[2]-offsets[0]).sqrMagnitude,
      Is.GreaterThan(0f));
  ```
- [x] 同class RED。
- [x] 实现固定三角形offset，初值相对module center为`(-.12,0,-.08)`、`(.12,0,-.08)`、`(0,0,.12)`米，高度用表面/杯底pivot；只调员工/展示配置，不改原Prefab。Cup Collider在这些runtime视觉实例关闭，不注册Navigation obstacle或furniture。
- [x] 同class GREEN；直接regression：`AnimalCafe.Tests.EditMode.Phase8.FunctionalSurfaceLayoutTests`、`AnimalCafe.Tests.EditMode.Phase8.SurfaceSlotDomainTests`。退出条件：创建失败原子回滚，移动/旋转局部跟随，3杯饱和不阻塞。

## Task 4 — 导出working/carry与唯一Animator所有权

**Files:** 创建`ExportEmployeeAnimations.py`、`Phase13EmployeeAssetBuilder.cs`、`EmployeeVisualPresenter.cs`、`EmployeeAssetTests.cs`、`EmployeeAnimationTests.cs`及文件表中的FBX/Mask/Controller/Prefab；修改`NavigationWalkPresenter.cs`。

**Interfaces:** `NavigationWalkPresenter.SetExternalAnimationDriver(bool enabled)`与`event Action<float> MotionChanged`；默认false路径行为保持。`EmployeeVisualPresenter.Configure(NavigationActor actor, GameObject cupPrefab)`、`SetMode(EmployeeVisualMode mode)`、`bool TryShowHeldCup(out string reason)`、`HideHeldCup()`。Asset builder：`AnimalCafe.EditorTools.Phase13.Phase13EmployeeAssetBuilder.Build()`。Export工具参数`--source-root`、`--output-root`。

- [x] 先写`EmployeeAssetsContainWorkAndCarrySocket`、`CarryMaskExcludesRootPelvisAndLegs`、`DefaultNavigationPresenterStillSelectsWalkHold`、`WorkingCannotBeOverwrittenByMotionZero`（P-002/P-003/P-007/P-011～P-014）。Asset不存在可作为asset合同RED；UnityImporter/Blender启动错误单列，不充当行为RED。
  ```csharp
  Assert.That(workClip.isLooping, Is.True);
  Assert.That(handSocket.name, Is.EqualTo("hand_socket.R"));
  Assert.That(actor.ModelAnimator.applyRootMotion, Is.False);
  Assert.That(workingState.IsName("Working"), Is.True);
  ```
- [x] 跑对应Edit/Play class确认RED；读取unity-cli skill后才运行Unity操作。
- [x] 只读导出主checkout的已有Walk_Default、Work_Generic_Busy、Carry_Cup；排除CarryProp_Cup，不save母版。保留socket bones；若旧P11导出不含socket，生成员工专用同Rig body/Walk资产，不改旧model/Rig/默认Walk动作。Material复用现有同species资源。
- [x] builder经Unity API导入Generic clips、创建`Hold / Walk / Working` base layer及`CarryUpperBody`层，carry weight只在Carrying为1。Hold取已接受walk pose；Carry只覆盖upper body；work速度1，walk按实际speed驱动，Animator用Normal scaled time，root motion=false。
- [x] 新员工Prefab保留P11根部physics/尺寸/settings，配置员工专用模型/Controller/Presenter。外部driver只让员工Presenter写Animator；停用及时取消订阅，Inactive Animator不调用Play；手杯归Presenterowned，不重复导出杯mesh。
- [x] Edit/Play GREEN；直接regression：`AnimalCafe.Tests.EditMode.Phase11.NavigationAssetTests`、`AnimalCafe.Tests.PlayMode.Phase11.NavigationAnimationTests`。退出条件：两species实际采样合格，当时视觉等待Owner（后续结果见Task8与Validation Report），原角色资源未覆盖。

## Task 5 — 两员工Scene runtime与真实送杯

**Files:** 创建`EmployeeFlowController.cs`、`EmployeeFlowTests.cs`；连接Tasks1～4。

**Interfaces:** `Configure(NavigationWorld world, NavigationLayoutAdapter adapter, CafeLayoutRuntime layout, DecorationModeController decoration, GameTimeService time, CustomerFlowController customers, Transform gridRoot, NavigationActor cashierPrefab, NavigationActor baristaPrefab, PickupCupDisplay display)`；`void Step(float scaledDeltaTime)`；`bool TryStartTask(EmployeeRole role)`；`bool TryCancelTask(EmployeeRole role)`；`void RetryBlockedEmployees()`；`IReadOnlyList<EmployeeTaskSnapshot> Snapshot { get; }`。手杯port委托Presenter，放杯委托display；owned actors只能由本controller注册/清理。

- [x] 写`CreatesTwoUniqueEmployeesAtSafeWorkAnchors`、`RealBaristaPlacesCupOnlyAfterPickupArrived`、`FiveRealDeliveriesKeepThreeCupsAndReturnIdle`、`BlockedCarrierStopsWithoutLosingCup`、`MissingCupConfigurationCannotReportDeliverySuccess`（E-001/E-004/E-005/E-034，P-001/P-004/P-005/P-006/P-008/P-013/P-017）。真实NavMesh/角色测到达、朝向与杯归属，不用fake driver替代；五次真实交付的成功回调数为5、桌面objects为3。
  ```csharp
  Assert.That(staffActorIds, Is.Unique);
  Assert.That(display.VisibleCount(binding.CupAddress.Value), Is.EqualTo(1));
  Assert.That(baristaSnapshot.Delivered, Is.True);
  Assert.That(baristaSnapshot.State, Is.EqualTo(EmployeeState.Idle)); // 实际返抵后
  ```
- [x] 跑`EmployeeFlowTests`确认RED。
- [x] 实现初次安全出生/唯一session与actor ID。使用已有`TryInitializeRuntimeId`在World.Register前设ID；无合法位置不生成，保留配置失败。使用adapter owned路径查询与gridRoot转换Anchor位置/朝向，World.Service.MoveTo/Cancel执行，Update只传Time.deltaTime。
- [x] 初始化两runtime ports/runner，失败/退出只清理本员工；任何callback除runner token外还检查controller session/disposed，防destroy后放杯。task snapshot分开显示交付结果与回位失败。
- [x] 同class GREEN；直接regression：`AnimalCafe.Tests.PlayMode.Phase11.NavigationMovementTests`、`AnimalCafe.Tests.EditMode.Phase12.CustomerAdmissionTests`。退出条件：真实完整制杯循环、受阻/失败正确，无Customer生命周期改动。

## Task 6 — 装修、失效、disable与恢复

**Files:** 扩展`EmployeeFlowController.cs`、`EmployeeTaskRunner.cs`；创建`EmployeeLifecycleTests.cs`、`EmployeeDecorationTests.cs`。

**Interfaces:** consumes `DecorationModeController.IsOpen / CompletedLayoutChangeVersion`、`NavigationLayoutAdapter.Revision / PublishedRevision / CurrentReadiness`、Task2 InvalidateLayout/TryReturnToWork、Task3 RefreshConfirmedSupports。不增加Decoration全局事件或改变reset入口。

- [x] 写`UnchangedDecorationResumesSameTask`、`ConfirmedEditRetiresOldTaskExactlyOnce`、`RepeatedConfirmKeepsEmployeeIdentity`、`DisabledBodyCannotBeBypassed`、`RemovedPickupRejectsStaleSceneObject`（E-008/E-017～E-019/E-026～E-032/E-036/E-037，P-008/P-010/P-013）。
  ```csharp
  Assert.That(employeeIdsAfterDecor, Is.EqualTo(employeeIdsBeforeDecor));
  Assert.That(cleanupCallsForOldHandle, Is.EqualTo(1));
  Assert.That(oldCallbackPlacedCups, Is.Zero);
  Assert.That(resumedRemainingSeconds, Is.EqualTo(pausedRemainingSeconds)); // 未Confirm
  ```
- [x] 两classes RED。
- [x] 实现Decor暂停与正式变化区分：`CompletedLayoutChangeVersion`识别完成的已确认修改，adapter revision保护请求/目标；单纯重建/ReadinessVersion变化不能误判为玩家修改。Confirm导致旧navigation callback失效时只退休旧任务一次；没有确认的退出保留任务时间并必要时从实际位置重新提交同目标。
- [x] Confirm后移除未交付手杯、重新绑定，新layout可运行后回岗位；已交付桌面杯由display按support有效性处理，不由cancel撤销。无station/路径则Blocked，修复入口才能尝试恢复，返回仍未到时不Idle。
- [x] disable/destroy取消owned task/request及subscription；仍存在身体沿P11保持保护。重新enable创建新session/验证实际位置，不复活旧任务；CustomerFlowController代码不扩大为员工清理者。
- [x] GREEN；直接regression：`AnimalCafe.Tests.EditMode.Phase12.CustomerSessionLifecycleTests`、`AnimalCafe.Tests.PlayMode.EditorSceneLoading.NavigationLayoutIntegrationTests`、`AnimalCafe.Tests.PlayMode.Phase12.NavigationRuntimeIdentityTests`。后一个历史class位于Phase11目录但namespace是Phase12，按实际class filter，不能空筛选后声称PASS。

## Task 7 — 验证Scene、MainCafe与manual入口

**Files:** 创建`EmployeeTaskValidationController.cs`、`Phase13EmployeeTaskSceneSetup.cs`、`Phase13SceneBuilderTests.cs`、`EmployeePlaySessionTests.cs`、`EmployeeMainCafeTests.cs`、验证Scene、`Docs/Phase13_Beginner_Guide.md`；经Editor API接线MainCafe；必要扩展现有P13 tests覆盖坐标。

**Interfaces:** scene builder `BuildValidationScene()`、`BuildMainCafe()`，namespace `AnimalCafe.EditorTools.Phase13`。菜单`Tools/AnimalCafe/Phase 13/Create Employee Task Validation Scene`与`Install Employee Tasks in MainCafe`。validation controller `Configure(EmployeeFlowController employees, CustomerFlowController customers, GameTimeService time)`；两task按钮“收银服务”“制作咖啡”。Inspector context helpers `P13/Cancel Barista Task`、`P13/Cancel Cashier Task`、`P13/Enable Customer Arrivals`、`P13/Disable Customer Arrivals`、`P13/Let Front Customer Leave`、`P13/Retry Blocked Employees`，只作调试验证，不增加正式业务管理按钮。队首离场helper只调用已有`CustomerFlowController.TryLetFrontLeave()`，不能把它当作订单服务成功。

- [x] 写`BuildIsIdempotentAndProtectsOtherScenes`、`EmptyCafeRemainsEditableUntilStationsAreReady`、`StaffAndCustomersShareSelectedRegister`、`RepeatedPlayStartsOneFreshSession`、`RotatedMultiSlotTableKeepsCupModule`、`HudControlsRemainClickableAcrossAspectRatios`（E-033/E-038，P-009/P-015/P-016/P-018）。真实同场测试等待至少4顾客，覆盖队首受控离场/前移与员工身体保护。
  ```csharp
  Assert.That(employeeControllerCount, Is.EqualTo(1));
  Assert.That(employeeActors, Has.Count.EqualTo(2)); // 有效布局
  Assert.That(cashierWorkStationId, Is.EqualTo(customers.SelectedRegisterId));
  Assert.That(emptyCafeDecorationCanOpen, Is.True);
  ```
- [x] 同classes RED。
- [x] `EmployeePlaySessionTests`在EditMode runner用Unity Test Framework的进入/退出Play指令，分别验证Domain Reload开/关的两次真实session（P-015）；finally恢复EditorSettings与原Scene。不能用同一Play里的两次SetActive代替真实重复进入Play证据；编译重载或runner受限单列BLOCKED，M-015仍由Owner照步骤验证，不把simulation写成该项PASS。
- [x] 首先构建独立验证Scene，默认种齐可用设备、禁止自动进客；随后才接线MainCafe。新增`P13 Employees` root，不重建旧P12 root/HUD或改已有validationScene；Cashier/Barista品种通过Scene引用配置，非Role写死。
- [x] 空MainCafe等待布局，无无效员工出生或提前启用不可退出的business gate；已具有效营业配置时复用原gate。P12 AutoSpawn在MainCafe保持已有配置，测试helper仅在Play切换。
- [x] 独立Scene/辅助HUD显示角色状态、remaining game seconds、绑定设备和0～3杯数，busy/Paused/Blocked按钮给原因。Cancel/进客helpers只在validation/debug组件；保留time/Decor操作与Camera输入，初始化不重复创建静态subscription。
- [x] 将test-cases §4的16项步骤迁入Beginner Guide并核对真实菜单、Hierarchy、Inspector helper、默认设备位置；为M-008堵路/M-012多设备提供可重复布置说明。不要求Owner猜Script字段或写代码。
- [x] GREEN；直接regression：`AnimalCafe.Tests.PlayMode.Phase12Integration.Phase12CustomerQueueSceneTests`、`AnimalCafe.Tests.PlayMode.Phase12Integration.CustomerQueueFlowTests`及现有MainCafe/time/装修接线tests（核对class名称后选相关项）。退出条件：16项manual均有实际可操作入口，MainCafe与P12共存，旧Scene/资源无意外dirty。

## Task 8 — 集中完整验收、review与交接

**Files:** 创建`Docs/Phase13_Validation_Report.md`；更新Beginner Guide、本plan任务状态、test-cases执行结果、Game Design/Roadmap活动状态。修正旧P12工作区/待收尾入口只用已存在合并证据，不重写历史测试结果。

**Interfaces:** consumes Tasks1～7实际产物/输出；produces完整测试counts、Engineering/QA/必要Art review、Owner M-001～M-016结果和明确的P14边界。技术review不替代Owner视觉接受。

- [x] 先检查38个E、18个P的coverage，确认对应代码/fixture存在；未实现/未执行项目不能勾为PASS。检查working、carry mask/socket、默认P11/P12行为与Scene资产写入边界。
- [x] 串行运行完整EditMode、PlayMode；42cm调整前最终2594P/0F/0S与1253P/0F/5S，均exit0/inconclusive0。逐项从XML报告PASS/FAIL/SKIP；保存logs、初轮失败与五项旧opt-in skip原因。
  ```powershell
  unity test . --editor-path E:/Unity/Hub/Editor/6000.5.5f1/Editor/Unity.exe --mode EditMode --output outputs/phase13/final-edit.xml --timeout 5400 -- -logFile E:/Unity/Project/AnimalCafe/.worktrees/phase-13-employee-task-flow/outputs/phase13/final-edit.log
  unity test . --editor-path E:/Unity/Hub/Editor/6000.5.5f1/Editor/Unity.exe --mode PlayMode --output outputs/phase13/final-play.xml --timeout 5400 -- -logFile E:/Unity/Project/AnimalCafe/.worktrees/phase-13-employee-task-flow/outputs/phase13/final-play.log
  ```
- [x] Phase末做一次独立Engineering/QA review；working/carry/三角形展示发生明显视觉变化，加入Art技术检查。Critical/Important交回原实现者修复，focused复验；Minor进polish backlog。review方式在实际执行时按Owner选择与工具权限安排，不在Task内重复全phase gate。
- [x] 准备三张真实 graphics Editor 代表截图与Guide；实际入口、恢复步骤和16项manual均已列出。截图 receipt 为 `outputs/phase13/final-capture-caption.xml`，1P。
- [x] Owner明确委托Codex执行M-005～008、M-010～015共10项；已完成新一轮场景API代测并记录PASS、失败脚本修正、实际画面和日志，不冒充物理鼠标或Owner观感接受。
- [x] Owner已确认M-001～004及M-006 PASS（M-006为复验），结果已记录；另提出working手低于台面的视觉适配问题，后续已批准降低台面30cm并完成实施和追加回归。
- [x] Owner明确确认M-009、M-016手测PASS（2026-10-14），16项manual去重后全部有通过记录。42cm台面方案已批准实施并完成追加回归；computer-use的BLOCKED尝试单独保留。
- [x] 修复后先focused，最后重新完整regression；两旧Play baseline改动不改变产品/Editor/Scene，已通过最后完整Play重跑，保留完整Edit PASS。不为docs-only变化重复Unity。Report/Roadmap已更新真实状态，未写Phase Complete。
- [x] 输出最终changed files、测试counts/证据、10项Codex场景代测PASS、Owner确认M-001～004/M-006 PASS及最新M-009/M-016 Owner手测PASS、known limits和后续决策。Android/iOS Player/device、cloud CI、commit/push/PR/merge保持NOT_RUN；技术交付见Validation Report和Beginner Guide。

## 验收覆盖映射

| 用例 | 实现/自动验证责任 | Owner验证 |
| --- | --- | --- |
| E-001/E-006/E-007/E-037；正确设备与身份 | Task1；实际注册/失效由Task5/6补齐 | M-001/M-012 |
| E-002～E-005/E-009～E-020/E-033/E-035/E-036；流程/时间/callback | Task2；真实Nav与Decor由Task5/6/7补齐 | M-002/M-003/M-005/M-011/M-013 |
| E-008/E-026～E-032；layout/生命周期 | Task6；T1选设备、T3展示跟随 | M-006～M-009/M-014 |
| E-021～E-025/E-028～E-030/E-034；杯与原子交付 | Task3；Task5真实交付失败 | M-003/M-004/M-007/M-016 |
| E-038；空店可装修/有效后启用 | Task7 | M-016 |
| P-001/P-004/P-005/P-008/P-013/P-017；真实movement/失败 | Task5/6；动画由Task4 | M-001/M-003/M-008/M-013/M-014 |
| P-002/P-003/P-006/P-007/P-011～P-014；动画/杯资源/默认回归 | Task3/4/5 | M-002～M-005/M-010 |
| P-009/P-010；MainCafe/P12/Decor同场 | Task6/7 | M-006～M-009/M-012 |
| P-015/P-016/P-018；重复Play/坐标/Scene/UI | Task7；局部坐标由Task3/4补齐 | M-015/M-016 |
| M-001～M-016全部 | Task7准备入口/Guide；Task8集中验收 | 10项委托代测PASS；Owner确认M-001～004/M-006 PASS，M-009/M-016亦已由Owner手测PASS，见Report |

## 计划自检与执行选择

计划编写时（2026-10-13）的历史自检：每个E/P/M ID都映射到Task；代码接口/nullable含义保持一致；时间、三杯饱和、成功点、回位Busy、装修与Phase边界一致。当时尚未启动Unity或生成runtime/test/asset files；后续执行结果见 Phase13 Validation Report。

建议 **本chat直接执行（Native）**：8个Task共享binding/runner/visual接口，逐步由同一实现者衔接更简单；按项目流程在Phase末集中独立review和manual。可选Subagent-driven，但会增加每Task独立上下文成本，并须保持现有Phase验收比例，不能生成第二套重复流程。

执行决定（2026-10-14）：Owner 已确认开始并选择 Subagent-driven。按 Task 顺序执行；每 Task focused/直接回归，Phase 末集中完整验收。随后Owner委托10项场景代测，已执行PASS；Owner另确认M-001～004及M-006 PASS，M-009/M-016现由Owner明确确认手测PASS。新增台面降低30cm至42cm已获批准、实施并完成追加回归；最终review见Validation Report。




最终追加验证：42cm资源focused60P、实际场景/截图3P；完整Edit2599P（微小位移修复前），修复后P11 Edit77P、movement34P、pause7P；最终完整Play1267P/0F/5原有opt-in SKIP。当前116文件与最终源码manifest一致。详见Validation Report的最终review；本次文档更新未重跑Unity。
