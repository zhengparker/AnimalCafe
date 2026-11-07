# Phase 15 — Business Day Lifecycle Implementation Plan

> 最新长柜台范围（2026-10-28）：Owner已批准原三文件服务通道规则／导航诊断，并在实际trace后另行批准第四文件NavMeshMovementDriver.cs的ClampToNavMesh严格退化分支修复；具体资格见工作区navmesh-boundary-repair-proposal.md。其余导航层／安全门槛不扩大。原I1／I2限定复核已完成，无需重复请求同项授权。

> 后续范围批准（2026-10-27）：Owner 对 P14 导航／排空专项修复回答“批准”，授权继续执行 Validation Report 第6节。先用同一任务／lease 下的真实交通证据和行为 RED 定位，再做最小修正；仍不放宽 Nav／身体／容量／断言，不执行版本管理动作。既有失败记录保留，P15 尚未正式验收完成。

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development`，按 Owner 最新选择用 sub-agent 在当前 chat 逐 Task 实现；Task 做窄范围技术复核，Phase 末集中正式验收，不做逐 Task 多部门验收。
> 状态：Approved for Implementation（2026-10-26）。Owner 在确认 D1／D2／D3、查看 plan 与测试范围后明确指示“好的，用sub agent开工”；无需重复请求 plan 内 Task 的批准。正式 Phase closeout／版本管理权限不扩大。

**Goal:** 咖啡厅按现实 8 小时营业、真实排空后休息 2 小时，自动进入下一天，保留既有服务与真实装修中断事实。

**Architecture:** 一个纯 C# Service 维护周期；一个 BusinessDayController 使用独立 realtime clock 连接原 CafeLoopController 的唯一 driver。新客许可与已有服务分离；每次实际来客都查询实时截止许可，服务后聚合真实 drain，再决定 Rest。UI 只读。

**Tech Stack:** Unity `6000.5.5f1`、C#、现有 UGUI／P5 UI、Unity Test Framework／NUnit；不新增 package、assembly、scheduler 或 Save engine。

**Spec:** [P15 Design](../specs/2026-10-26-phase-15-business-day-design.md)；[Test Cases](../specs/2026-10-26-phase-15-business-day-test-cases.md)；[Phase Process](../../AnimalCafe_Phase_Development_Process.md)。

## Global Constraints

- 正式默认 Open `28800` 秒，Rest `7200` 秒。八小时不含 Closing；Rest 从实际 drain 观察时刻开始。
- `Pause`／`2x`／装修暂停只影响既有 Scene 工作、移动和动画，周期仍使用独立现实经过时间。
- Closing 停止新客，包括直接 `TrySpawn()`；已有未付款／制作中／待取餐／离店者继续正常服务。
- 真正 Blocked 保留 Closing 与原因，使用既有显式有界恢复；不无限 retry、强行跨日、瞬移、伪造 Completed 或退款。
- 真实装修修改沿 P14 清场规则，破坏旧业务前按 layout version 记录一次 `LayoutInterrupted`；不是正常全部服务完成。
- 历史 Completed／付款事实不按日清空；本阶段不实现 Coins、Daily Report 正文、Save 或退出后 Offline 收益。
- E 盘项目内 worktree；保留主 checkout 的文档及其他 untracked 资料。不 commit／push／merge、删除 branch／worktree或自动触发 GitHub CI。
- 代码重要逻辑使用中文解释＋English identifiers；Unity `.meta` 由 Editor 正常生成并保留，不手改 Scene／prefab YAML。

## Review Focus

1. 截止已到但 driver 尚未运行时，直接 TrySpawn 仍不能使用缓存 true（Task 2）。
2. P15 provider 在加载前缺失、disabled／destroyed，以及旧 owner 解绑，不能使新接待失去门禁（Task 2）。
3. last customer Completed 但未离店、Barista Returning、残留 reservation／callback／delivery，均不能提前 Rest（Task 3）。
4. Pause 不能停止独立 Rest；空店在 Pause 时能确认真实 drain，但暂停中的未完成服务不能被跳过（Tasks 1／3／4）。
5. 装修清场异常、重复版本和旧回调不能伪装正常日结，也不能重复计中断；最新结束原因跨下一 Open 保留（Tasks 1／4）。

## 文件结构与责任

新建：

```text
Assets/Scripts/BusinessDay/
  BusinessDayContracts.cs          配置、阶段、drain result、只读 snapshot／最近结束记录
  BusinessDayService.cs            纯逻辑周期与中断去重
  BusinessDayController.cs         realtime clock、唯一 driver 接线、状态事件
  BusinessDayStatusView.cs         Day／阶段／剩余时间／原因 UI
Assets/Editor/Phase15/
  Phase15BusinessDaySceneSetup.cs   独立 Scene 与明确的 MainCafe 安装入口
Assets/Tests/EditMode/Phase15/
  BusinessDayServiceTests.cs       时间与状态转换
  BusinessDayDrainTests.cs         服务 pending／聚合事实
  Phase15SceneBuilderTests.cs      接线、默认时长与受保护资产
Assets/Tests/PlayMode/EditorSceneLoading/Phase15/
  BusinessDayTestSupport.cs        真实 Scene setup、可控时钟、cleanup
  BusinessDayAdmissionTests.cs     入口门禁／provider 生命周期
  BusinessDayIntegrationTests.cs   排空、Pause／2x、跨日、装修中断
Assets/Scenes/Validation/Phase15BusinessDay.unity
Docs/Phase15_Beginner_Guide.md
Docs/Phase15_Validation_Report.md
```

修改：`CafeLoopController.cs`（接待与 driver hook／drain）、`CafeLoopService.cs`（只读 pending）、`CustomerFlowController.cs`（required policy 与入口查询）、`EmployeeFlowController.cs`（真实返岗查询）。Task 6 才通过 Editor 安装到 `Assets/Scenes/MainCafe.unity`；不重做旧 P14 validation Scene、prefab、队伍算法或资产。

上述 Runtime／Editor／tests 均位于现有 asmdef 覆盖目录；各新增目录及资产配套 `.meta` 正常生成。没有 Python virtual environment 或 dependency 安装。

## 验证命令与执行前准备

只在执行批准后建立 `E:/Unity/Project/AnimalCafe/.worktrees/phase-15-business-day`，拟用 branch `codex/phase-15-business-day`，从当前 main 基线开始。先确认 worktree inventory／Git ignore；同名存在则核对并复用，不能覆盖或 reset。携带本轮两份 living docs、P15 spec／cases／本 plan 的明确版本；不复制 Library／Temp 或其他无关 untracked。

本机只读核对：Unity CLI `1.0.0-beta.12`，`unity test --help` 支持以下参数。以下命令均在新 worktree cwd 执行，实际执行时把 XML 名称标明 Task／RED 或 GREEN；同一时间只有一个 Unity process，默认 retries=0。

```powershell
unity test . --mode EditMode --filter AnimalCafe.Tests.EditMode.Phase15 --output outputs/phase15/focused-edit.xml --timeout 600
unity test . --mode PlayMode --filter AnimalCafe.Tests.PlayMode.Phase15 --output outputs/phase15/focused-play.xml --timeout 1200
```

每个 test run 必须读取 XML 的实际 selected cases、PASS／FAIL／SKIP 与 failure 原因并核对 process exit；没有 XML、选中零用例或 compile／fixture failure 不算产品 RED／GREEN。初次导入失败先诊断，不擅自安装 Editor、登录账号或删除 Library。

执行前先跑直接受影响旧 P14 Driver／Service 与 P13 员工返回的 baseline；Task 阶段只跑本 Task 和直接 regression，完整 suite 留到 Task 6。新 namespace 在尚无 tests 时不当作 baseline。

---

## Task 1：纯逻辑周期、时间边界和中断记录

**Files:** 新 `BusinessDayContracts.cs`、`BusinessDayService.cs`、`Assets/Tests/EditMode/Phase15/BusinessDayServiceTests.cs`。

**Interfaces:**
- `BusinessDayPhase { Open, Closing, Rest }`；`BusinessDayEndKind { CompletedDrain, LayoutInterrupted }`。
- `BusinessDaySettings(double openSeconds = 28800, double restSeconds = 7200)`，只接受 finite positive。
- `BusinessDayDrainResult(bool isDrained, string reason)`；false 必须有 reason。
- `BusinessDayService(BusinessDaySettings settings, double nowSeconds, long initialDay = 1)`。
- `void BeforeService(double nowSeconds)`；`void AfterService(double nowSeconds, BusinessDayDrainResult drain)`；`bool CanAdmit(double nowSeconds)`；`BusinessDaySnapshot GetSnapshot(double nowSeconds)`。
- `void RecordLayoutInterruption(long layoutVersion)`，按单调 version 去重；getter／CanAdmit 只读。
- Snapshot 暴露 `DayNumber`、`Phase`、`RemainingSeconds`、`TransitionSequence`、本日 `LayoutInterruptionCount`、最新只读 `LastClosedDay`（day／结束原因／中断次数）和 drain reason。内部状态只由 Service 写入。

- [x] **RED：** `DefaultsUseEightHoursAndTwoHours`；`CutoffIsExclusiveForAdmission`；`ClosingWaitsForDrainAndStartsRestAtActualObservation`；`RestReopensWithoutButtonAndIncrementsDayOnce`；`LayoutVersionIsRecordedOnceAndLastCloseSurvivesNextOpen`。
  ```csharp
  var day = new BusinessDayService(new BusinessDaySettings(8, 2), 0);
  Assert.That(day.CanAdmit(8), Is.False); // 即使还没推进driver也已截止
  day.BeforeService(8);
  day.AfterService(9, new BusinessDayDrainResult(false, "顾客尚未离店"));
  Assert.That(day.GetSnapshot(9).Phase, Is.EqualTo(BusinessDayPhase.Closing));
  day.AfterService(12, new BusinessDayDrainResult(true, ""));
  Assert.That(day.GetSnapshot(12).RemainingSeconds, Is.EqualTo(2));
  ```
- [x] 运行 Task1 filter，确认 assertions 因旧行为／缺少新行为而真实 FAIL；无法编译先完成最小声明，再验证行为 RED。
- [x] 最小实现：Open 到截止只进 Closing；Closing 仅在 AfterService 的真实 drain=true 时 Rest；Rest 到期只开下一轮一次，新 Open 起点为观察时刻，不循环模拟遗漏的营业。
- [x] 同时固定 invalid／rollback／overflow：拒绝 negative／NaN／Infinity、观测时间回退和 Day／sequence 溢出，原状态不变；query 早于最后观测返回拒绝或明确参数错误，不产生写副作用。
- [x] focused GREEN。覆盖 E01–02／09／14–17／22–23／26／28 的 domain 部分；Pause 不是 Service 输入，因此不应能缩放周期。

## Task 2：实时接待 policy 与唯一 driver

**Files:** 新 `BusinessDayController.cs`、PlayMode `BusinessDayTestSupport.cs`／`BusinessDayAdmissionTests.cs`；改 `CafeLoopController.cs`、`CustomerFlowController.cs`。

**Interfaces:**
- Controller：`Initialize(CafeLoopController loop, BusinessDaySettings settings, Func<double> readNow = null, bool validationMode = false)`；`bool CanAdmitNow()`；`void BeforeService()`；`void AfterService(BusinessDayDrainResult drain)`；`BusinessDaySnapshot Snapshot`；`event Action<BusinessDaySnapshot> SnapshotChanged`。
- 正式时钟默认 `Time.realtimeSinceStartupAsDouble`；测试注入同一 FakeClock。Editor builder 只写 serialized refs／settings，不能在 EditMode 提前启动营业计时。
- Cafe Loop：`SetAdmissionEnabled(bool enabled)`、`RegisterBusinessDay(BusinessDayController owner)`／`UnregisterBusinessDay(BusinessDayController owner)`；旧 `Step(float)` 与无 P15 默认保持兼容。
- Customer Flow：serialized `requireAdmissionPolicy=false`；`RegisterAdmissionPolicy(UnityEngine.Object owner, Func<bool> canAdmit)`／`UnregisterAdmissionPolicy(UnityEngine.Object owner)`；重复同 owner 幂等，不允许两个活动 owner 竞争。

- [x] **RED：** `ExpiredDeadlineRejectsDirectSpawnBeforeDriver`、`MissingProviderAtLoadFailsClosedWhenRequired`、`DisabledOrDestroyedProviderCannotAdmit`、`StaleOwnerUnregisterCannotRemoveCurrentPolicy`、`LegacySceneWithoutRequirementStillAdmits`。
- [x] 在真实 P14 runtime fixture 设置 required=true／注册时钟；fake clock 推过截止后、尚未 yield 或 Step 时直接调用 TrySpawn，断言无新 Actor／token／Order。不是只测 policy delegate 返回 false。
- [x] 最小接线：TrySpawn 所有入口在任何 reserve／生成前读同一实时 policy；provider 无效或抛错时拒绝并明确原因。required=false 的旧 Scene 沿旧规则，不能放宽原容量／路径检查。
- [x] 原 Cafe Loop Update 仍是唯一 driver：最先 BeforeService 处理截止，原业务推进完成后 AfterService。原 layoutPending 早退等分支仍观察周期，但 drain 返回具体未就绪；不能让额外 Update 重复 Step。
- [x] OnDisable／destroy 撤销各自 registration 并保持 required gate 关闭；重复 Initialize 不重建 active Service／重置 Day。Controller 没有第二个 NPC driver。
- [x] focused PlayMode GREEN；直接回归旧 `AnimalCafe.Tests.EditMode.Phase14.CafeLoopDriverTests`、P12／P14 真实自动与手动来客。覆盖 E03／14／24–26。

## Task 3：真实排空与员工返岗

**Files:** 改 `CafeLoopService.cs`、`CafeLoopController.cs`、`EmployeeFlowController.cs`；新 `Assets/Tests/EditMode/Phase15/BusinessDayDrainTests.cs`；扩展 `BusinessDayIntegrationTests.cs`。

**Interfaces:**
- `CafeLoopService`：`internal bool HasPendingBusinessWork { get; }`，检查 cashier／barista、events、pickupMoves／exitMoves／blockedCollections、collected 及任何非 Completed Order；不能用 Status 字符串。
- `EmployeeFlowController.AreEmployeesAtWorkStations(out string reason)`：两 Role 唯一真实 enabled Actor、有效 Binding、Idle／HandleId0；实际位置在 WorkTarget 的原 `ArrivalDistance` 内、速度不超过 `ArrivalSpeed`、朝向符合原 `FacingToleranceDegrees`。不依赖 CanProgress，空店 Pause 也能读取真实事实。
- `CafeLoopController.GetDrainSnapshot()` 返回 `BusinessDayDrainResult`，覆盖 spec 第7节全部条件；不以 Orders／PaidFIFO 历史非空否定排空。

- [x] **RED：** `CompletedCustomerStillExitingPreventsRest`、`ReturningBaristaPreventsRest`、`ReservationOrCallbackPreventsRest`、`MissingLedgerIsNotEmpty`、`HistoricalCompletedOrdersAllowRest`、`PausedEmptyCafeCanRestWithoutUnpausing`。
- [x] 用真实流程观察领取→离店→员工返回；分别证明 live visits、两队 assignments、三项 Used、logic delivery／visible cups、pending、员工与 layout 稳定。每个缺口有 focused 断言，不能只通过一个泛化 IsDrained bool。
- [x] 最小只读聚合；missing／disabled／Blocked 返回原因，不发导航、不清 store、不重新注册员工。Query 实际 Actor／Binding 时沿原到达条件，不调整 NavMesh／身体／步速。
- [x] focused GREEN；直接回归 P14 Service／实际取餐离店、P13 Runner 返回。覆盖 E04–13／20–21。

## Task 4：装修中断、跨日与旧行为兼容

**Files:** 改 `CafeLoopController.cs` 的现有 `OnChangedLayoutExitPreparing` hook 与 BusinessDayController；扩展 Service／Integration tests。

**Interfaces:** Controller `void RecordLayoutInterruption(long layoutVersion)` 调用同名 Service 方法；最新 LastClosedDay 在新 Open 后保留。沿用现有 layout version，不新建第二套装修 transaction。

- [x] **RED：** `ConfirmedLayoutChangeRecordsInterruptionBeforeClear`、`PreviewOrCancelDoesNotRecordInterruption`、`RepeatedVersionIsIdempotent`、`CleanupFailurePreventsRestAndKeepsInterruption`、`NextDayRetainsLastInterruptedClose`。
- [x] 在退休旧业务前同步记录一次中断；不改原 P14 清场、已领取离场者保留与员工恢复。清场失败仍阻止 drain；成功后也需等真实排空，不能立即切 Rest。
- [x] 两个完整短周期：无人点击、同一员工 Actor／ID、Day只加一次；Pause／2x／装修期间实际周期不停止，未完成动作仍暂停，Rest 不虚构接待收益。
- [x] focused GREEN；直接回归 P14 Decoration lifecycle／顾客 Carry／员工恢复。覆盖 E14–21／23–24，解释 MainCafe 空布局与玩家 Pause 行为。

## Task 5：只读 UI 与独立 P15 Scene

**Files:** 新 `BusinessDayStatusView.cs`、`Phase15BusinessDaySceneSetup.cs`、`Phase15SceneBuilderTests.cs`；扩展 Integration tests；生成独立 P15 Scene／meta。

**Interfaces:** StatusView `Bind(BusinessDayController owner)`、`string CurrentText`、`static string Format(BusinessDaySnapshot value, bool validationMode)`；Editor `BuildValidationScene()`／`InstallInMainCafe()` 两个明确入口。

- [x] **RED：** `SceneBuilderInstallsOneDayOwnerAndRequiredPolicy`、`ValidationDurationsAreMarkedAndMainRemainsUnchanged`、`StatusRefreshIsReadOnlyAndShowsInterruption`。
- [x] Builder 复制已验证 P14 validation Scene，再用 Unity Editor API 接线；验证配置为 Open60秒／Rest15秒，HUD 标明 Validation。MainCafe 正式默认28800／7200，不能继承短时设置。
- [x] UI 显示 Day／Open／Closing／Rest、小时分钟或短时倒计时、真实停止原因及 LayoutInterrupted。复用 P5 字体、SafeArea 和输入边界，不新增 Start／Close／NextDay／领奖按钮。
- [x] 重复安装不重复 owner／UI／注册；dirty Scene 不静默覆盖。确认旧 P14 Scene／prefab／MainCafe 在 validation builder 后未变。
- [x] focused GREEN；代表性横／竖 Editor 观察与 Console 检查；对应 M01–07 仅准备，不代填 Owner PASS。

## Task 6：MainCafe 正式接入与 Phase 集中验收

**Files:** 通过批准后的明确 Editor `InstallInMainCafe()` 更新 `Assets/Scenes/MainCafe.unity`；新增 Guide／Validation Report；更新 living docs、spec／cases／本 plan 进度。

- [x] 用MainCafe原布局启动；无效／空布局保持readiness gate；Main真实自动测试与既有正常装修Done后自动接待case通过，没有validation seed填正式布局。Owner M01仍NOT_RUN。
- [x] MainCafe正式28800／7200秒，独立fixture60／15并标Validation；旧无P15场景保留默认。本轮34项test-owned写入在17:17恢复，3172 Assets／1126 source零差异、extra／temp0、Editor0；Main／P15与既有metadata保持。
- [x] Task1–5 focused、直接回归、必要P15 Scene integration与集中完整自动验证完成；中途失败及获批修正保留完整历史，最终实际命令／XML见Validation Report（以下为原计划命令）：
  ```powershell
  unity test . --mode EditMode --output outputs/phase15/full-edit.xml --timeout 3600
  unity test . --mode PlayMode --output outputs/phase15/full-play.xml --timeout 7200
  ```
- 当前结果：本地自动回归已通过：最新完整 EditMode 2832 PASS／0 FAIL／0 SKIP；独立完整 PlayMode 1445 PASS／0 FAIL／5 项 opt-in SKIP。两次 CLI／native log 均0；后续仅 Editor／test 变化未重跑完整 Play，454 runtime 与 Main／P15 Scene 保持一致。Engineering／QA／Production 技术复核已完成（Critical 0／Important 0）；Owner M01–M07、真实8小时／device、CI／VCS／merge／merged-main 均 NOT_RUN。Phase 未正式 Completed，不进入P16。具体命令、XML、恢复与source范围见[Validation Report](../../Phase15_Validation_Report.md)。
- [x] Engineering／QA／Production 各一次 Phase review，必要 UI／Art 仅审实际变化；修复 Critical／Important，focused复核后按项目流程做最终完整回归。Minor进入polish backlog。
- [x] Guide／Report已创建：通俗例子、Game HUD Pause／1x／2x与Editor toolbar Pause区别、短时／正式配置、M01–07操作及结果字段；自动证据、Owner／device／正式验收边界分别记录。
- [ ] Owner 完成 M01–07 manual；真实八小时、退出后 Offline、Android／iOS device、CI 等没有实际执行就保留 `NOT_RUN`，不由短周期 tests 替代。
- [x] 未commit／push／PR／merge／workflow_dispatch／删除worktree；版本管理由Owner决定。Task6集中验收和Phase均未标Completed，不进入P16。

## 执行记录与批准

2026-10-27 展示修正批准：Owner 指出 MainCafe 不应出现开发黑色HUD，并批准 Main P5玩家状态／Validation详细诊断分离。该工作是既有Task5／6的一个有界follow-up，沿用本worktree、SDD ledger和此前sub-agent执行授权；短设计、精确文件范围、RED→GREEN与双Scene／双尺寸UI复测步骤在 `.superpowers/sdd/2026-10-26-phase-15-business-day/player-hud-brief.md`。不重开Phase、不修改营业／导航规则、不重复索要已批准范围确认、不进行CI／Git交付；旧全部测试与manual只保留原source边界。Game Design已补Main／Validation长期展示原则。

Self-review覆盖：Task1对应E01–02／09／14–17／22–23／26／28；Task2对应E03／14／24–26；Task3对应E04–13／20–21；Task4对应E14–21／23–24；Task5对应E26与UI／Scene；E27以scope／diff和实际接线检查证明没有新增伪Economy／Save，不写空泛测试；M01–07在Tasks5–6准备并由Owner手工执行。35项均有验证归属，接口名称沿上述Interfaces引用。

Tasks1–5实现、Main接入与后续获批修正均有focused／独立source review；最新完整Edit2832 PASS，独立完整Play1445 PASS／5 opt-in SKIP，本地自动gate通过。最终部门收口、Owner7项／device8h／CI／VCS／merged-main待完成，Phase未正式Completed。实际证据、source边界与历史见[Validation Report](../../Phase15_Validation_Report.md)。

**Owner 已批准：用 sub-agent 按此 plan 在当前 chat 逐 Task 实现，建立 E 盘 worktree并开始 Task1。** 设计 D1／D2／D3 已确认，不重复提问；后续不为 plan 内每个 Task 再建批准门槛。执行位置为 `E:/Unity/Project/AnimalCafe/.worktrees/phase-15-business-day`，branch `codex/phase-15-business-day`。


2026-10-27 approved player-HUD follow-up已实现并完成限定验证：Edit59P，Play122P／1原有S／0F，独立Spec／quality通过。Root真实两Scene／两尺寸、Main Decor→Done和Validation诊断／时间按钮／实际Closing及Rest记录15张截图，UI后3172Assets零差异。详见Validation Report第17节、player-hud-report／task-review及ui/events-final.json。无Phase closure／CI／Git动作；旧完整suite与manual source边界保留。


## Approved follow-up: long-counter exit traffic repair

Owner在明确两份代码文件、正常／静态堵路／请求取消与P14/P15回归范围后回复“确认”。按 exit-traffic-repair-brief.md 执行一个集成TDD task：先补真实RED，最小修改CustomerFlowController和CafeLongCounterStallTests，再GREEN／直接回归／独立复核／root native UI。临时实际交通使用既有PendingExitSegment条件等待；永久堵路仍Blocked。保留身份、付款、容量及实际离店，不修未复现全游戏冻结、不改UI或Scene。沿用既有sub-agent方式，无新Phase／CI／Git动作。
## 已批准长柜台后续：服务通道与导航诊断

本节是既有P15计划的已批准follow-up，不创建新Phase。原始调查、failed candidate、逐source XML及独立诊断review保留于Validation Report第18节与`.superpowers/sdd/2026-10-26-phase-15-business-day/`。原I1／I2修正与新增三文件scope分开绑定source，不把批准／局部测试当作整体GREEN。

- [x] 原范围review fix1：最终replan candidate再次检查独立accepted路线precedence；WAIT投影明确保留其他Entry／Advance／Pickup owner目标与全部lease。7项限定PASS，I1／I2独立复核ADDRESSED、新增C0／I0／M0，Pickup独立post-replan实测缺口明确保留；不代表整体修复GREEN，final14失败source保留。
- [ ] 新范围阶段一：只在NavigationWorld既有Step计算上加Editor/test opt-in有界诊断，原test读取driver已有字段，禁止额外调用会推进corner的getter／重复执行运动。抓取第一条边缘停滞真实request的原始corner／generation／revision／每阶段位移与原timeout；没有观察到拒绝层就写UNKNOWN，不改碰撞半径／skin／retry／arrival或导航底层行为。
- [ ] 同一阶段抓取PhotoSixFast第一条新pickup assignment与已有service corridor冲突的实际发布顺序，选择已证实的最小reservation hook；不凭最终FIRST_BLOCK反推事件先后。
- [ ] 新范围阶段二：controller内以Visit／PickupOwner／reservation assignment generation／layout revision绑定真实成功通道证据，覆盖Queued预留到transfer，以及已拥有service／pickup路线的未发腿。admission在reserve/create前拒绝封路tail；新／变更pickup assignment在发布前等待较早仍有效的service corridor。保留到店机会、原token／付款／lease；取消、换预留、layout／owner变化、真正离店按合同准确退休。实际派发仍全量复验，不强行恢复已经形成的静态死锁。
- [ ] 对应行为RED／GREEN：封路前拒绝且零新分配；非冲突放行；同一due机会后续自动成功；reservation／service先后；generation／owner／取消／Decor／离店退休；Pause冻结与Closing禁入／已有服务排空。
- [ ] 机制收敛后按批准scope跑八布局1x／2x、旧20单、全P14 Play、P15／P12相关Play与P14／P15相关Edit；已知FAIL时只跑定位机制所需selector，不循环重跑完整suite。
- [ ] 独立限定review、source／Assets manifest（仅批准三文件）、Editor release；必要自动GREEN后root正常Editor检查运行与输入。原Game输入无响应单独保留NOT_REPRODUCED／UNKNOWN，不能用经营修复PASS代替。

文件只限CustomerFlowController.cs、CafeLongCounterStallTests.cs、NavigationWorld.cs。无Scene／prefab／meta／Save迁移，不改变正式28800／7200周期、容量上限、收费／身份或安全规则。不安装package、不做Git／CI／merge、不进入P16。若诊断明确需要修改NavMeshMovementDriver、NavigationCollisionGuard等底层行为，先说明准确位置、风险与验证；本诊断批准不能解释为该行为改动已获批准。
### 实际边缘trace后的第四文件批准（2026-10-28）

Owner已明确批准NavMeshMovementDriver.cs仅ClampToNavMesh退化分支及必要private helper／原测试。前述仅诊断限制仅在此准确范围被新批准替代；CollisionGuard、Adapter、NavBuilder与半径／skin／epsilon／arrival／预算／Scene仍不改。资格是双ray近水平起点且zero normal、有效Steady owned path／沿当前corner、ClosestEdge切向不向外、原endpoint准确、同Filter短PathComplete且只有两个准确corner、epsilon向内仅query平行射线clear；证明全部成立才保留原input，实际最终全身体sweep照常。任何证明不成立继续旧裁剪，不实际向内推／瞬移。

- [ ] 记录第四源before/hash，保持原3174 all-Assets baseline；保持原I1／I2复核改动。
- [ ] 原真实timeout RED与资格／真实外边缘／洞／断连／Partial／多corner／end偏移／内移queryblocked／静动态障碍反测。
- [ ] 正常／2x实际request及原八布局每例20真实出口GREEN后，追加Phase11 movement／scene／layout integration、Phase12 identity及必要Service／Collision／ResumeBlock Edit检查；再完成原相关回归。无关asset builder／全项目suite不因该补修自动重跑。
- [ ] 同一实现owner维护共享test／soleEditor，source每轮冻结；独立限定review、最终四源资产边界、正常Editor运行／输入代测。旧fail、编译NOT_RUN、Game输入原症状UNKNOWN与正式Phase边界保留。

### 用户批准继续：实际队首预留失败后的接待限流（2026-10-28）

Owner在当前失败说明后回复“继续就可以”；采用最小观察式规则：实际到位、Queued、尚无成功空间预留的Counter队首，其真实TryReservePickup被安全空间／路线拒绝后，暂停后续TrySpawn，既有角色继续；成功预留／离队／head或layout换代后重新判断。门禁在ID／Actor／容量创建前，保留due机会，不改clock Tick、调度顺序、capacity／payment／FIFO／导航。仅改原controller和长柜台test；无迁移。拥挤时排队缓存和吞吐可能下降，是否足够修复Photo7必须实测。只在CanReserve=false拦客过晚，不能代表安全可达性。

- [ ] 新任务fresh implementer，按同目录unreserved-front-admission-brief.md先真实RED，再最小两文件GREEN／生命周期反测。
- [ ] 原22机制与新机制GREEN后，原Photo7Normal一次source-change复测；失败立即冻结，不添加第二策略。
- [ ] Photo7GREEN再原8场景20单1x／2x矩阵；GREEN后直接相关P14／15／导航回归，保留高负荷原全部断言。
- [ ] 独立限定review、3174Assets／四源冻结readback、soleEditor释放；自动GREEN后正常Editor输入观察。原Game输入无响应仍UNKNOWN。

### Owner批准的完整诊断链扩展（2026-10-28）

Owner明确批准call_daf9ad5ff18e4f4986463e0f5c5ff23c，答复为批准诊断扩展。按remaining-stall-observation-proposal.md和stall-observation-brief.md：仅CFC／EmployeeFlow／Driver BeginPath/Observe与原test加默认关闭Editor观察，World原hook复用；行为／原调用次数／断言保持不变。

- [x] Fresh subagent独占四source及soleEditor；保存五源／3174Assets baseline。
- [x] 观察defaultoff／enabled验证及原29机制同源GREEN；无产品行为修复。最终34 PASS。
- [x] 一次原八组合统一诊断，不重复single／matrix或跑下游；旧FAIL不被新PASS覆盖。本次8 PASS，旧签名NOT_REPRODUCED。
- [x] 独立限定review、源／资产／进程释放readback；未知保留UNKNOWN。Code／runtime evidence review无新C/I/M；Main补充检查PARTIAL，不替代业务验收。

诊断执行顺序补充（root ruling）：观察＋原29 GREEN后先冻结并做同一独立review的code/coverage部分，确认无C/I再运行获批的一次8组合。运行后该review仅补核验runtime evidence，不重复审源码。此为提高一次诊断有效性的内部顺序调整，原文件／行为／运行次数边界保持。

### Owner批准：双方已选服务路线保护修复（2026-10-28）

Spec／授权：同plan workspace `owned-service-route-repair-proposal.md`；Owner答复call_72f19533c2564aa7821910031d28b6bc“批准四文件双向路线修复”。实际controlled产品RED为1P1F／11.6570314s，C3与handle4互锁；历史等价性及精确acquire顺序UNKNOWN。

单个集成Task，四文件共同验证，不拆并行source writer：CFC、EmployeeFlow、EmployeeTaskRunner、CafeLongCounterStallTests。其余源码含base测试清理行、导航／碰撞、Scene／Save／资源／package只读。正式28800／7200、付款／容量／FIFO／token／collision参数不变，无future exit预留／traffic manager／无限WAIT／Blocked自动重试；无迁移、Git／CI／merge或Phase closure。

接口决定：Runner新增只读当前delivery suffix getter（例如`internal IReadOnlyList<NavigationTarget> RemainingPickupDeliveryRoute`），使用真实selected array/index且按当前任务生命周期有效；不推进状态。EmployeeFlow沿现有`GetPickupReturnTraffic(string actorId)` provider signature提供当前pose＋有效outward suffix＋既有return（Returning只保留实际剩余return），不暴露已退休腿。CFC复用`HasOwnedRoute`／`ProtectedOwnedRoute`及contributor当前suffix/binding/strict有限proof，双方取得／前进检查与`HasDeliveryTraffic`有限等待分类必须一致。私有命名可在同四文件内据现有类型合理调整并记录，不改变公共契约。

- [x] **Step 1 RED：** 新focused tests覆盖客户真实owned pending腿及有限contributor不被后来staff candidate封住；员工真实outward suffix阻止后来customer acquisition。原完整native paths、身份及实际原流程请求作为前提，不伪造request/假成功query。记录预计断言及实际RED，保留controlled旧FAIL。
- [x] **Step 2 最小实现：** Runner／Flow只读当前计划，CFC双向原规则保护与同源有限WAIT；不直接放宽hasPath、不引入新的调度层。记录实际C3/C4 publication/acquire次序及pending字段，仅事件边界／cold记录。
- [x] **Step 3 focused GREEN：** 正常不冲突候选允许；相向两种取得顺序在lease／route发布前拒绝后来者，先owner实际推进、后来者随后成功。加入Arrived只退休已走腿、cancel／generation／layout／complete旧计划失效、无finiteproof pending0不得无限WAIT反测；原直接WAIT／owned-route回归保持。冻结source后一次原受控2回归，不改旧assert。实际23 Play＋8 Edit＋2 controlled通过；controlled目标crossing未形成，不作为复现crossing修复证明。
- [x] **Step 4 task review：** 一个独立review同时判spec及quality，重点finite contributor未被新路线封住、有效任务/route退休、无请求/取消/旧layout不能伪造进度，以及死锁/饥饿。C/I同实现者修订并限定复核，不无关扩scope。实际code与targeted runtime证据review通过，无新增C/I/M。
- [ ] **Step 5 原八组合：** focused及controlled GREEN、review无C/I后，一次原8布局／1x2x，每例原20真实出口与FIFO／token／delivery／capacity断言；失败保存原决定链后停止，不重复不变源碰运气。
- [ ] **Step 6 readback／文档：** 实际XML／CLI-native exit、source／Assets／package边界与soleEditor释放；更新GDD／roadmap／validation实际状态。完整Phase regression、Main输入手测／device／CI／merge按原gate单独安排，不能以本Task通过收口P15。

### Task 7: Owner-approved Pick-up exit eligibility repair

Spec: `Docs/superpowers/specs/2026-10-28-pickup-exit-eligibility-design.md`. Owner已针对两文件与两条行为回复“修复吧”；不重新请求普通实现批准。此前已选路线保护的原八7P1F与新单场NOT_REPRODUCED保留，不混作成功。

**Goal:** 建队时计入收银队首合法站立范围，并在取得collection lease前验证所选side→exit。

**Architecture:** 复用现有 bounded planner 和已有 geometry/collection eligibility；仅新 private helper 或最小共用query抽取。不新增未来出口cache／生命周期。单个集成task，fresh implementer独占两个Assets及sole Editor；root维护Docs。

**Tech stack:** Unity6000.5.5f1, C#, NUnit/Unity Test Framework, installed Unity CLI; existing E: worktree.

**Global constraints:** spec的Scope完整适用。实际身体/foreign target/lease保持；未来Counter head只进入geometry eligibility，完整连续到位包络，不用有限采样冒充覆盖。公开及既有反射入口signature保持。原自然method逐字节不变，以相同提取边界before/after SHA相等验证；27 assertions/seed150027保持。历史15961为另一截取边界的元数据，不作为改写测试的目标。新local snapshot诊断默认flag前置Ignore，不让CI依赖outputs。No Git/CI/commit/install/Scene Save；raw历史输出不可覆盖。非产品diagnostic PASS不可代替业务PASS。所有实际CLI exit立即持久化，native/readback与OS handles区别。

**Files / interfaces:** modify CFC and original Long test only. Existing `PickupPlanCanExit`, `TryMoveToPickupService`, original planner/native-filter, `counterWaitSlots[0]`, prefabs' current settings. Flow9A97/RunnerB8B4/base6506/other3172 Assets are frozen. Exact private helper names may follow existing style; no unrelated refactor.

**Review focus:** future Counter customer's continuous body/arrival envelope and varying radii; selected-side check before lease/publication; no false finite wait or loss of real progress; actual/diagnostic native-query provenance and retry cost; unchanged identity/collision and old test assertions.

- [ ] Step 1 — Freeze current CFC5BC4/LongD825 and3174/package, Editor0; introduce meaningful real preconditioned REDs for both eligibility gaps. A fixed query replay is only fixture/geometry evidence, not a product RED.
- [ ] Step 2 — Minimal CFC changes, private reusable bounded-query helper if needed. Counter envelope only in eligibility; actual obstacles/movement unchanged. Full selected-side exit checked before collection publication. Preserve existing strict owned traffic.
- [ ] Step 3 — Focused positives/negatives: live Counter occupancy and arrival offsets/radii; legal nonconflicting queue/side; unsafe preferred side not acquired; valid alternative accepted with same owner/tokens; all rejected leaves no publication and no permanent WAIT; real completion after bounded explicit obstacle removal/normal release. Existing pending proof/identity/staticBlocked explicit recovery and relevant staff suffix cases.
- [ ] Step 4 — Freeze tested two-file delta and evidence package, independent spec/quality review. Same implementer handles scoped findings; no duplicate broad review or test reruns on unchanged source.
- [ ] Step 5 — After review/root GO, original8 (optionally combined existing controlled2) once on this candidate, unchanged20realexit/order/FIFO/token/capacity assertions. Preserve fresh immutable artifacts; fail means original evidence+diagnosis, not automatic identical retries.
- [ ] Step 6 — Once key matrix is green, complete applicable required P14/P15 integration and relevant P12/Navigation/direct Edit checks from the existing phase plan. No unrelated full asset-builder suites. Source-bound readback, Editor release, actual Docs updates; manual/Main input/device/CI/merge/P15 formal closure are separate.

## Owner delivery decision

Owner明确选择“先交付经营修复，暂不改额外测试”。当前两文件经营修正及关键经营验证交付；四个额外测试文件及此前两份准备补丁均不应用，其dependent回归延期。Task7整体gate仍未关闭，Step6不标完成，不进入P16。当前交付source CFC2776／Long688642；原8各20真实离店、controlled2正常完成、普通Editor鼠标＋20离店、Fix1单例及相应review见Validation Report18.24–18.27。四个额外测试源原SHA保持；无Git／CI／merge／cleanup动作。未来要处理这些延期项时再按Owner新授权继续，本次不重复确认。

Latest Owner amendment: after the delivery-only decision, Owner explicitly approved the ORIGINAL two-file preparation question. Only CafeDecorationFlowTests original fault-injection timing and CafeFurnitureLayoutMatrixTests original same-request/actual-movement600-frame patch may now be applied. The combined Matrix counter relocation, Base PickupSide and Status proposals remain deferred. Preserve product code/original assertions; validate the corresponding two focused cases, then report results. This does not authorize full-phase closure or the other three outstanding cases.

Narrow-two execution result: exact original patches applied; one authorized no-flags native batch 2P0F0S, 6.2672538s, CLI/native0, XML3C29393D… in approved-two-prep-01. Decoration real-arrival-before-fault and Matrix same-request actual-movement within600 preserve original assertions. Current source Decoration6CF7CBBC / MatrixCD22D67E; CFC2776 / Long688642 / Base6506 / StatusC3DB unchanged. 3174 Assets/runtime8source/3inputs parity, historical sinks restored, Editor/worker0. Three old cases remain unclosed; full association latest220P9F2S/current full+Edit NOT_RUN. No combined relocation/Base/Status application or further runtime authorized by this narrow amendment. Latest details Validation18.28; independent incremental review pending at this timestamp.

Root closeout of narrow-two authorization: independent applied-delta/spec/quality review PASS, C0/I0/M0 (extra-fixture-draft-review.md); exact XML/CLI/native and current hashes read back by Root. Original two-file preparation request fulfilled; no further tests dispatched. Validation18.28/Guide/Roadmap reflect 2PASS and three unresolved cases. Full-phase gate remains open.

OWNER latest “继续直到P15完成”: Root resumes approved P15 implementation/regression/closeout preparation. Prior deferrals superseded for remaining three already-reviewed fixture preparations (Matrix firsthunk CR aisle via formalDecor; Base controlled-blocker retirement; Status currentreason observer), originalasserts/product/Scene unchanged. No newbehavior/Git/CI/merge/P16 or automaticformalclosure authorization inferred. Same solewriter/Editorseat receives completion-continuation-brief.md; staged native3 then incrementalreview then required current-source regression. Root handles Docs/remaininggate inventory; do not repeat completedTasks1–6 or historic manuals. Independent review and actual evidence before any completion claim.

## P15 continuation local closeout checklist (Owner: 继续直到P15完成)

- [x] Remaining three fixture cases: FormalRepair actual first-batch PASS; HeldReady+Status actual2PASS and independent Spec/quality C0/I0/M0. Original FAIL and diagnostic preserved.
- [x] Related six-filter EditMode: actual278 PASS /0 FAIL /0 SKIP,12.6343693s,CLI/native0; current3174Assets/source/package/Scene parity restored (two known test Scenes), see current-source-final-regression-01/edit.
- [x] Complete current-source PlayMode1498 PASS/0 FAIL/7opt-in SKIP (1505total),1968.9434184s,CLI/native0; old9failed names eachoncePASS;3174Assets/source/package/Scene parity restored.
- [x] Independent final Engineering/QA receipt reviews PASS C0/I0/newMinor0; Report18.30,Guide,Roadmap,current outputreport updated. P15 local technical acceptance complete; Owner/CI/Git/formal closure remain separate.

No new Phase, product behavior, Git/CI/merge or P16 implied. Owner sensory/Go and repository closure gates remain explicit after technical evidence is reviewable. See Validation18.29.

Final current-source gate underway: relatedEdit actual278P0F0S12.6343693s CLI/native0 XML0BAA9D1D…; two known Phase14Scene outputs restored to frozenCURRENTbytes (first explicitrecoveryreceipt + secondcollector),3174Assets/8source/3inputs/14Scenes all0mismatch, noextras/temp. RootONEwholePlay launched02:22:47Z session1864, exactcommandunfiltered/noflags/retries0. No newsourcechanges. Finalresultpending; sourceUI/manualpriorreceipts retainboundaries.

Root final actual wholePlay: 1505total1498P0F7S1968.9434184s CLI/native0. Original9failed fullnames eachoncePASS;7skip=5previousoptins+localquantizedsnapshot+normalEditorinputprobe(no flags). CurrentnativeInput prior20exits/mouse remains separatePASS, no syntheticskipacceptance. CollectorafterCLIresident naturallygone (no kill/no native retry) restored1knownAsset and13historicaloutputs,3174Assets/8sources/3inputs/14Scenes0mismatch/unknown0/missing0,EditorWorkers0. SameQA andappliedEngineering reviewers finalreceipt delta inprogress; sourcefrozen. Rootnew.gitignore exactP15outputs staticvalidated; noGitcommit/push/CI/merge/P16. Finaldocsnext technical readiness vsformalownergates.

最终收口：Engineering与QA已独立读取原XML、当前source绑定及恢复bytes，两席均Spec／quality／local technical PASS，C0／I0／新增Minor0。见SDD `remaining-fixture-applied-review.md` 最终receipt节及 `final-qa-receipt-addendum.md` 95–123行。**P15开发及本地技术验收完成**；branch `codex/phase-15-business-day`及worktree保留，文件未commit／push。正式Owner观感／Go、CI／merge／merged-main仍须实际执行，Roadmap不虚标正式Completed，不进入P16。历史Game输入首因UNKNOWN保持原状。本次无需再重复运行已通过且源码未变的suite。

## 2026-10-29 manual第6–8项修复（Owner：查明然后修复吧）

- [x] 读取保留现场，再退出Play；真实native RED确认先发离场109与后发送餐110相交，补充精确状态／身体／request前提。
- [x] 最小产品变更：CustomerFlowController原guard覆盖active collected Exiting；EmployeeTaskRunner解释Recovered仍未完成原服务。沿用原有路线优先权、Blocked和显式恢复。
- [x] Runner／checkpoint Edit71PASS；定向离场保护与cancel退休2PASS。
- [x] 中心Counter(4,4)真实Confirm后，1x／2x各60实际离场、Pause、跨日排空PASS。保留初始180sTimeout及Normal40000帧不足的FAIL；仅测试预算／清理修正，产品源冻结。
- [ ] Phase13／14／15相关Play回归（预期278），排除已单验的两项60人长测。历史全项目Play1498P0F7S不冒充新源结果。
- [ ] 新computer-use：Closing真实装修取消／确认、持续经营至少5分钟、受控堵路恢复及新提示。
- [ ] 原始XML／资产与历史输出保护／独立review／living docs最终核对；不执行Git／CI／merge或P16。


## 2026-10-29 修复追补状态（覆盖上段未完成清单的当前状态）

- [x] 员工出口检查限定非Visit候选，保留原客户未来出口资格；原PendingExit回归在该源通过。
- [x] canonical reservation与route certificate分离，新增只读getter与两处统一障碍集合；实际RED→GREEN生命周期已确认。
- [x] 当前五项定向native验证5PASS/0FAIL，3176Assets无未知差异，Editor/workers0。
- [ ] 长柜台仍未通过：完整相关277PASS/1FAIL/2SKIP；当前focused3PASS/1FAIL。单独／观察PASS不升级为修好。
- [ ] 待Owner讨论并确认新的预留目标路径保持规则；先native反例与总查询成本验证，未实施额外body-WAIT补丁。
- [ ] 新Computer Use第6–8项重验NOT_RUN；之后再进行当前源相关回归与独立验收，不启动Git／CI／P16。

详见outputs/phase15/manual-exit-repair-20261029-01/report.md及reservation-rule-proposal.md。

2026-10-29 Owner已确认新站位路径保持规则；先native反例后实施已执行，首轮3PASS与生命周期4PASS。当前原13项及后续相关回归、Computer Use待实际结果。详见outputs/phase15/reservation-rule-20261029-01/report.md，不以旧PASS覆盖新FAIL，不跨Phase。
