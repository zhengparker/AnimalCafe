# Phase 10 — Capacity & Reservation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` for inline execution, or `superpowers:subagent-driven-development` if the Owner selects that method. Apply `superpowers:test-driven-development` to each code Task. Steps use checkbox (`- [ ]`) syntax for tracking. Project process overrides repeated per-Task approval/commit ceremonies.

**状态：** Owner于2026-09-29批准design / test cases / 本plan，并授权在现有P9同一branch开发、使用sub-agents及Codex GUI代跑。Task 1–6开发与必要验证完成；M-001…010通过，M-009拖动由Owner补验，Owner随后接受委托验收并授权final overall review后commit/push。最终Engineering和QA整体review均PASS，R-001…004已关闭。P10分支交付不等于main merge，Roadmap保持In Progress至正式closeout。

**Goal:** 建立随Floor面积改变的三类容量及原子reservation，保证owner正确、失败无副作用、缩容不丢顾客。

**Architecture:** 纯C# CapacityService管理token；CapacityRules计算三种上限；只读FloorCapacitySource读取确认布局。独立Editor窗口演示命令，不接真实NPC、Order事件或Scene。

**Tech Stack:** Unity `6000.5.5f1`、C#、现有Runtime/Editor/EditMode/PlayMode assemblies、NUnit与Unity Test Framework；无新增package/Python/virtual environment。

**Spec:** [2026-09-29-phase-10-capacity-reservation-design.md](../specs/2026-09-29-phase-10-capacity-reservation-design.md)

**Test Cases:** [2026-09-29-phase-10-capacity-reservation-test-cases.md](../specs/2026-09-29-phase-10-capacity-reservation-test-cases.md)

## Global Constraints

- 默认Total=floor(FloorCellCount/4)，PickUp=Total，Counter=ceil(Total*50/100)；不扣家具/入口净空/临时阻挡格数。
- Owner批准入店前原子预留三项各1；Counter离位、PickUp取走且离位、实际离店分别释放对应名额。
- 缩容保留既有reservation，允许Used>Limit；Available=max(0,Limit-Used)，任一超额拒绝新Reserve，已有Reserved仍可Occupy。
- P9正确取走即Completed，不自动释放PickUp或Total；不修改OrderService合同。
- Floor采用正式Interior region的有效格union；现有region发布视为地板已铺好；P28若解锁/铺设分离必须更新来源。RoomSurfaceLayout外观固定64格，不作为动态面积来源。
- owner是visit ID；token为service-scoped、不可公开伪造；重复Release仅同service+同owner+同token幂等。
- 单线程同步、独立只读snapshot；不加锁、async、singleton、event bus、DI框架或通用资源框架。
- P10无spawn/random scheduler、NavMesh、真实站位、员工、材料、Save、Scene wiring或正式UI。到店节奏属于P12，释放空位不自动填满。
- 复用P9基线，不以main是否merge作为P10文档准备前提；不自动closeout P9。
- 不改现有Scene/Prefab/asmdef/Packages/P9源码/旧Layout行为；新Assets附Unity生成的.meta。
- 保留用户无关文件；未经单独授权不commit/push/merge、删除branch/worktree。本次不创建worktree。
- 每Task focused+direct regression；Phase末集中完整回归、Engineering/QA review、必要manual。仅文档变更不运行Unity。

## Review Focus

1. 缩容产生的合法超额被误判为损坏、已有预约被拒：Task4，E-037…040、M-006。
2. 固定64个外观tile或矩形包围框被当作面积、重叠/Exterior重复计算：Task1，E-005…010。
3. 旧token/同Id跨service/错误owner借幂等释放新名额：Task3，E-028…032；Task5 D-003。
4. batch后半失败、ID耗尽后计数或nextId发生部分修改：Task2，E-016…023。
5. P9 Completed被当成离店，以及Reset属性正确但焦点字段仍显示旧值：Task4 E-045/046，Task5 D-003，Task6 M-001/008。

## 文件结构与用途

```text
Assets/Scripts/Capacity/
  CapacityKind.cs                 三种容量enum
  ReservationState.cs             Reserved/Occupied/Released
  CapacityRules.cs                 固定session参数与面积计算
  CapacityLimits.cs                immutable三项上限和Floor格数
  FloorCapacitySource.cs           只读已确认Interior布局adapter
  CapacityToken.cs                 service身份绑定的不可变token
  ReservationSnapshot.cs          token/owner/kind/state查询
  CapacitySnapshot.cs              limit/计数/空位/超额查询
  CapacityResult.cs                成功或错误及涉及的snapshots
  CapacityFailureReason.cs         确定性错误enum
  CapacityService.cs               唯一写入入口
Assets/Editor/Phase10/
  CapacityDebugSession.cs          临时service生命周期
  CapacityDebugWindow.cs           验收窗口
Assets/Tests/EditMode/Phase10/
  CapacityRulesTests.cs            E-001…004
  FloorCapacitySourceTests.cs      E-005…010
  CapacityReservationTests.cs      E-011…023
  CapacityLifecycleTests.cs        E-024…035
  CapacityResizeTests.cs           E-036…043、048
  CapacityInvariantTests.cs        E-044、047
  CapacityOrderBoundaryTests.cs    E-045…046
  CapacityTestSupport.cs           合法fixtures与U/I断言
  CapacityDebugWindowTests.cs      D-001…006
Assets/Tests/PlayMode/Phase10/
  CapacityDomainPlayModeTests.cs   P-001…003
Docs/Phase10_Beginner_Guide.md      Task5创建操作步骤，Task6写真实验收
```

以上为实施阶段新增文件范围；实际进度见各Task记录。任务完成时更新本plan/task摘要与原case结果，避免重复报告。Gameplay source修改限定在Capacity新目录。

## Workspace 与批准入口

- 已检查基线为 `E:/Unity/Project/AnimalCafe/.worktrees/phase-9-order-domain`、`codex/phase-9-order-domain`、`94cc48d7d8c6b43261b96e73bf2d2b6541fafbf4`。Owner明确继续此P9基线，不等待main合并。
- 本次将三份P10文档放入该checkout；不创建另一个worktree，不切branch，不操作P9 outputs。主checkout的.gitignore、slnx、Game Design、旧P9草稿及其他未跟踪文件保留。
- [x] Owner于2026-09-29审核批准整套文档，并明确“可以开发，确保开发在p9同一个branch上面”，随后允许使用sub-agent。
- [x] 开始实施前复核HEAD仍为94cc48d、旧tracked文件无改动；只读进程核对无Unity Editor占用，CLI 1.0.0-beta.8、项目Unity6000.5.5f1。
- [x] 沿用既有P9 linked worktree与codex/phase-9-order-domain，不创建或切换branch/worktree。

Owner已允许sub-agents；采用逐Task顺序实现、task-scoped review、Phase末Engineering/QA独立收尾。Unity运行权串行分配，不允许多个agent同时启动tests。复用此plan与同一测试清单，不另建第二套approval gate。

## 执行命令约定

命令沿用P9 plan的CLI格式；实际执行前加载unity-cli skill并只读核对当前`unity test --help`，确认checkout与Editor占用。同一时间仅一个Unity test/build process。输出目录为`outputs/phase10/`，实际运行前创建。

```powershell
unity test . --mode EditMode --filter AnimalCafe.Tests.EditMode.Phase10 --output outputs/phase10/focused-edit.xml --timeout 600
unity test . --mode PlayMode --filter AnimalCafe.Tests.PlayMode.Phase10 --output outputs/phase10/focused-play.xml --timeout 600
unity test . --mode EditMode --filter AnimalCafe.Tests.EditMode.Phase9 --output outputs/phase10/p9-edit-regression.xml --timeout 600
unity test . --mode PlayMode --filter AnimalCafe.Tests.PlayMode.Phase9 --output outputs/phase10/p9-play-regression.xml --timeout 600
```

各Task用对应test class filter（如`AnimalCafe.Tests.EditMode.Phase10.CapacityRulesTests`）缩小RED范围；GREEN跑已实现P10 cases。保留首次可信RED与GREEN的不同XML，不覆盖RED证据。已有功能新加验证直接GREEN时如实说明，不人为引入产品bug；编译失败不冒称行为RED。完整回归仅Task6。

## Task 1 — 容量公式与只读 Floor 来源

**Create:** CapacityKind.cs、CapacityRules.cs、CapacityLimits.cs、FloorCapacitySource.cs；CapacityRulesTests.cs、FloorCapacitySourceTests.cs、必要的CapacityTestSupport.cs。

**Modify docs（获批后）：** `Docs/AnimalCafe_Project_Design.md`第3.4同步已批准规则；`Docs/AnimalCafe_Development_Roadmap.md` P10明确缩容例外、P12注明random interval/空间接入、P28注明confirmed Floor来源，Current Next Step如需同步只记P10已获批及P9未closeout，不把P9标Completed。

**Consumes:** CafeLayout.UnlockedRegions、IsInsideUnlockedRegion、LayoutRegion.Origin/Size/ZoneType；现有layout测试fixture方式。

**Produces:** `CapacityRules(int cellsPerCustomer=4,int counterPercent=50)`、`CapacityLimits CalculateLimits(int floorCellCount)`、`static int FloorCapacitySource.CountInteriorCells(CafeLayout layout)`；properties按design第4节。

- [x] 写E-001…010的参数化tests，先明确边界预期；如下关键断言不可用同一计算公式代替常量：

```csharp
var limits = new CapacityRules().CalculateLimits(20);
Assert.That(limits.TotalCustomers, Is.EqualTo(5));
Assert.That(limits.CounterQueue, Is.EqualTo(3));
Assert.That(limits.PickUp, Is.EqualTo(5));
```

- [x] 最小可编译接口后运行这两个test classes，记录可信行为RED，确认失败来自未实现公式/区域统计。
- [x] 用整数/long计算、内部immutable构造；adapter逐格过滤Interior/bounds、HashSet去重，在枚举前检查region坐标范围，不修改旧Layout。
- [x] focused GREEN并读取XML；只跑受影响的直接layout regression（未改旧源码可用新adapter fixtures覆盖依赖）；静态检查Living Docs无P9状态倒退。
- [x] 在本Task记录实际files、RED/GREEN、限制。无新增Gameplay批准，不反复询问原规则。

**Task 1验证（2026-09-29）：** RED为27 total / 3 PASS / 24预期FAIL，GREEN为27 PASS、0 FAIL/SKIP；直接CafeLayout regression为31 PASS。证据为outputs/phase10/task1-red.xml、task1-green.xml、task1-layout-regression.xml。全部E-001…010已覆盖；独立task review的Spec/Quality通过，无Critical/Important/Minor。

## Task 2 — 原子预留、token 与查询

**Create:** ReservationState.cs、CapacityToken.cs、ReservationSnapshot.cs、CapacitySnapshot.cs、CapacityResult.cs、CapacityFailureReason.cs、CapacityService.cs、CapacityReservationTests.cs；扩展CapacityTestSupport.cs。

**Consumes:** Task1规则/上限；design第4/5节完整属性、错误优先级和ID合同。

**Produces:** `CapacityService(int floorCellCount, CapacityRules rules=null, long firstTokenId=1)`、`Limits`、`CanAdmit`、`TryReserveAdmission(string ownerId)`、`TryReserve(string ownerId,IReadOnlyList<CapacityKind> kinds)`、`GetCapacities()`、`GetReservations()`；返回类型严格按design。

- [x] 写E-011…023；容量失败与ID耗尽后probe证明未部分写入。Task2中E-017的Occupied变体、E-018…022依赖Release的恢复段留Task3补齐，其余预留拒绝路径本Task执行，不把未实现段计PASS。

```csharp
var service = new CapacityService(16);
Assert.That(service.TryReserveAdmission("V1").Reservations.Count, Is.EqualTo(3));
Assert.That(service.TryReserveAdmission("V2").Succeeded, Is.True);
Assert.That(service.TryReserveAdmission("V3").FailureReason,
    Is.EqualTo(CapacityFailureReason.InsufficientCapacity));
// U在失败前后比较完整snapshots，另probe nextId；不只断言错误码。
```

- [x] 按小组记录RED（格式校验、batch容量不足、ID耗尽）；先验证全部输入与额度，再发布完整记录。
- [x] 使用简单私有字典/记录，绑定签发service和token身份，保留正long顺序；不使用裸Id授权，不暴露live可变集合。
- [x] focused GREEN；执行Task1 tests，核对失败U和排序、result合同。临时编译接口不能被记为完整功能。
- [x] 记录Task摘要；每个有依赖的未运行变体明确标NOT_RUN并在Task3补齐。

**Task 2验证（2026-09-29）：** 初始RED 51 total / 28 PASS / 23预期FAIL；最终task2-green-final.xml为51 PASS、0 FAIL/SKIP，含Task1回归。中间一次测试fixture导致50/51，其XML/log被同名重跑覆盖，未作为可复核证据。独立review Spec/Quality PASS；两项Minor测试加强建议（组合错误优先级、旧查询集合独立性）记录于ledger，后续生命周期测试一并补齐。E-017 Occupied/Released、E-018…020及E-022的Release恢复段尚NOT_RUN，按计划由Task3完成。

## Task 3 — 占用、释放与旧 token 保护

**Modify:** CapacityService.cs、CapacityTestSupport.cs、CapacityReservationTests.cs。
**Create:** CapacityLifecycleTests.cs。

**Consumes:** Task2真实签发tokens与查询；不反射构造service状态。
**Produces:** `CapacityResult Occupy(CapacityToken token,string ownerId)`、`CapacityResult Release(CapacityToken token,string ownerId)`。

- [x] 写E-024…035，并补齐Task2暂缓的Occupied/Release恢复段；矩阵覆盖3state×2operation×2owner关系。

```csharp
var token = service.TryReserve("V1", new[] { CapacityKind.PickUp }).Reservations[0].Token;
Assert.That(service.Release(token, "V1").Succeeded, Is.True); // 允许未抵达即归还
Assert.That(service.Release(token, "V1").Succeeded, Is.True); // 幂等
Assert.That(service.Release(token, "V2").FailureReason,
    Is.EqualTo(CapacityFailureReason.WrongOwner));
```

- [x] Occupy、Reserved Release、Occupied Release和幂等分别取得独立RED；身份保护随Occupy实现、后补测试首次GREEN，未取得原计划要求的独立RED，此流程偏差如实记录。
- [x] 按design顺序校验身份再校验状态；生成新snapshot替换记录，旧snapshot/token不变；Released历史不删除。
- [x] E-011…035及Task1全部GREEN；旧token重试不影响新token的行为probe通过。
- [x] 记录实际RED/GREEN与coverage，不在本Task执行全项目regression。

**Task 3验证（2026-09-29）：** 四组实际RED分别为Occupy 3 FAIL、Reserved Release 3 FAIL、Occupied Release 1 FAIL、重复Release 1 FAIL，随后各自GREEN；最终task3-green-all-a.xml 85 PASS，P9直接回归132 PASS。身份保护在Occupy阶段已实现，后补测试首次GREEN，没有独立RED；此TDD顺序偏差如实保留，不能补造。Task2延期的E-017…020/022及两项Minor coverage已补齐。独立review功能Spec/Quality PASS，0 Critical/Important；E-027对另一顾客当前record的断言可加强，登记Minor供后续核对。

## Task 4 — 缩容、恢复、不变量与 P9 边界

**Modify:** CapacityService.cs、CapacityTestSupport.cs。
**Create:** CapacityResizeTests.cs、CapacityInvariantTests.cs、CapacityOrderBoundaryTests.cs。

**Consumes:** Task1 CountInteriorCells/CalculateLimits、Task3成熟token操作、P9现有OrderService公开API。
**Produces:** `CapacityResult UpdateFloorCellCount(int floorCellCount)`；Reserve全局超额gate、动态CanAdmit。P9只作测试消费者，不改生产接线。

- [x] 写E-036…048；缩容与实际新layout来源更新分别建fixture；seed=17的200次序列输出诊断上下文。

```csharp
var service = new CapacityService(16);
var held = service.TryReserveAdmission("V1").Reservations;
service.TryReserveAdmission("V2");
Assert.That(service.UpdateFloorCellCount(0).Succeeded, Is.True);
Assert.That(service.Occupy(held[0].Token, "V1").Succeeded, Is.True);
Assert.That(service.TryReserveAdmission("V3").FailureReason,
    Is.EqualTo(CapacityFailureReason.CapacityOverLimit));
```

- [x] Update取得7项行为RED；既有Occupy/全局gate以及新增P9独立性测试直接GREEN，如实记录，不补造RED。
- [x] 原子替换全部Limits，保留token/状态/nextId；Available/OverCapacity按公式钳制；新Reserve全局gate在capacity检查前。
- [x] 用真实P9 API走到Completed，断言P10计数保持，随后显式离位/离店Release；失败订单不等于实际离店。
- [x] 所有P10 domain GREEN；运行P9 focused EditMode直接regression；核对未改P9源码，无静默API变更。
- [x] 记录公式与token不变量、source映射及功能边界。当前动态刷新仍为显式调用，不写成MainCafe自动监听。

**Task 4验证（2026-09-29）：** task4-green-all-c.xml 102 PASS，task4-p9-direct-a.xml 132 PASS，均0 FAIL/SKIP。首组行为RED为7 FAIL/9；缺方法编译检查另保留log、不算RED。一次E-048 fixture错误产生99/100，原XML保留，修正后通过。独立review功能Spec/Quality PASS，0 Critical/Important；两项Minor（失败前后token实例断言、等待时间证据）留Task5测试补齐与最终核对。Task3的E-027另一顾客身份断言已补齐。

## Task 5 — Debug 工具与 PlayMode 边界

**Create:** CapacityDebugSession.cs、CapacityDebugWindow.cs、CapacityDebugWindowTests.cs、CapacityDomainPlayModeTests.cs、Docs/Phase10_Beginner_Guide.md（操作章节）。

**Consumes:** 完整CapacityService，现有Editor assembly/test访问方式；参考P9生命周期但不修改P9窗口。

**Produces:** `AnimalCafe.EditorTools.Phase10.CapacityDebugSession`（internal，`Service` get-only外部视图、`Reset()`替换service）；`CapacityDebugWindow : EditorWindow`（public，internal Session/ResetSession、RunApplyFloor/RunReserveAdmission/RunReserveSelected/RunOccupy/RunRelease均返回CapacityResult）；菜单路径与字段默认值按design第7节。

- [x] 写D-001…006、P-001…003；Editor tests在EditMode assembly，PlayMode不依赖Editor types。
- [x] session、按钮委托、Reset先写tests，但首次仅编译失败，未取得原计划行为RED；实现后GREEN。帧稳定性对成熟domain新增验证，不补造RED，此流程偏差如实记录。
- [x] 实现窗口：默认F64/V1/Id1/全选，已应用Floor与待输入分开；Token查找仅当前session；显示全部计数、超额、历史与最后结果；真实生命周期解绑/重置、清本窗口输入焦点。
- [x] 验证负输入不抛异常、开关窗口不改Scene/Selection/timeScale；不加入真实NPC、订单模拟控制器或自动补满功能。
- [x] focused P10 EditMode+PlayMode GREEN，并P9 focused PlayMode回归；Guide引用原M用例，不宣称人工已通过。
- [x] 记录自动结果；真实Reload Domain/焦点显示必须留给Task6 M-008，不能由直接调用回调代替。

**Task 5验证（2026-09-29）：** task5-edit-green-b.xml 108 PASS，task5-fix1-play.xml 3 PASS，task5-p9-play-direct-a.xml 3 PASS，均0 FAIL/SKIP。窗口首次只得到缺类型编译失败，没有行为RED；Play首轮2/3为测试预期错误，原XML保留。独立review要求补P-002 token身份/probe Released断言，修正后3 PASS并由原reviewer复核关闭，最终自动范围Spec/Quality PASS。Task4两项Minor补齐。M-001…010仍NOT_RUN，真实焦点/Reload Domain不由回调测试替代。

## Task 6 — 集中回归、独立 review 与必要 manual

**Modify docs:** 本spec/test/plan、Phase10_Beginner_Guide.md中的实际结果；阶段批准后才更新Roadmap实际状态，不替P9自动closeout。

- [x] 串行完整回归，保存新XML并核对PASS/FAIL/SKIP：

```powershell
unity test . --mode EditMode --output outputs/phase10/full-edit.xml --timeout 3600
unity test . --mode PlayMode --output outputs/phase10/full-play.xml --timeout 1200
```

- [x] Engineering检查R-002：source gate PASS，无开放Critical/Important/Minor source finding；各Task所需定点复核已关闭。非正式美术阶段不额外开启Art生产。
- [x] QA检查R-003自动部分：coverage/evidence PASS；独立核对raw XML、23个源码hash、branch/HEAD、资源恢复及文档链接，0 Critical / Important / open Minor。
- [x] R-003完整gate：原始自动证据、真实GUI截图、Owner补验及最新验收状态经独立QA复核；final overall QA PASS，无开放Critical/Important/Minor。
- [x] Owner必要manual与文档认可：Codex代跑和M-009 Owner补验均有执行者记录；Owner收到P10范围及结果后要求final overall review并推送，接受M-010委托技术/语义验收，R-004 PASS。未冒称Owner亲自操作全部用例。
- [x] 已有代码修复完成focused和相应回归，最终完整regression已执行；之后只改docs，不重跑Unity。若manual发现代码缺陷，再按影响范围补验。
- [x] Guide总结files/理由/操作/已知限制，XML与原生GUI截图留outputs；将自动case及manual真实状态写回原test-cases，不倒填初始RED或制造替代证据。
- [x] 自动/必要manual/Engineering/QA及Owner验收条件均满足，已获同P9分支commit/push授权；main merge、正式Roadmap Completed与下一Phase不在本次授权内。

**Task 6自动结果（2026-09-29）：** 原始full-edit.xml为2363 total / 2008 PASS / 355 SKIP / 0 FAIL；三个fresh-process补跑分别160、194、1 PASS，与355个原Skip fullname逐项匹配，无重复、遗漏或多余用例。有效Edit覆盖2363 PASS / 0 FAIL / 0未解决SKIP。full-play.xml为1046 total / 1042 PASS / 4 opt-in SKIP / 0 FAIL；四个Skip的全名与原始原因见原test-cases最新快照。不能把补跑结果改写进原XML，也不能把4个opt-in Skip算PASS。

收尾核对：23个P10 C#文件在完整回归前后SHA-256一致，Unity生成的.meta齐全；既有Assets/Packages/ProjectSettings无tracked差异。测试产生的已知资源改写已逐项备份并恢复；没有恢复或覆盖P10新文件、用户原checkout的工作。仍在codex/phase-9-order-domain，HEAD保持94cc48d；没有commit/push/merge。过程偏差保留在各Task记录，尤其Task2一份中间失败输出被覆盖、Task3身份测试直接GREEN、Task5窗口仅初始编译失败且无行为RED。

最终独立QA已保存至本地scratch的 `qa-full-regression-review.md`：自动范围PASS，无开放Critical/Important/Minor；其指出的一句过时Guide描述已修正并由原reviewer读回复核关闭。关机后重新核对，所有XML与报告完整、23个源码hash一致、branch/HEAD未变，因此仅同步文档状态，不重复已完成的Unity回归。R-003完整gate、R-004和P10完成状态继续等待必要manual与Owner确认。

**Task 6 GUI记录（2026-09-29）：** Owner另行授权Codex代跑后，通过真实Unity GUI完成M-001…008和M-010技术检查，并由独立sub-agent审查原始截图。M-009开窗前后缩放、时间、Decor操作通过；Camera拖动未观察到位移，保留BLOCKED并请求Owner真实鼠标确认，不能认定产品FAIL。M-010最初误在Edit Mode操作，已在Play Mode完整补跑，以`m010-play-*`为正式证据。浮动窗口两次越界由Owner移回；移动窗口本身不是验收通过。证据位于`outputs/phase10/manual-2026-09-29/`，原test-cases为唯一结果表。

GUI收尾已恢复Reload Domain and Scene及1x、退出Play、关闭Debug，Console 0/0/0、Scene无dirty；MainCafe/EditorSettings/ProjectSettings前后hash一致，23个P10源码hash仍等于自动回归基线，旧Assets/Packages/ProjectSettings无tracked差异。没有保存Scene或修改产品代码，不需重跑自动回归。

**后续Owner补验（2026-09-29 19:39 UTC）：** 针对当前P10开窗前后真实鼠标拖动，Owner明确回复“两种情况下都能移动”，M-009更新PASS。该部分是Owner报告的实际操作证据，不冒称Codex截图；此前BLOCKED仅保留为过程记录。补验后Scene/设置hash仍与操作前一致。Owner对M-010含义及本轮结果的最终认可尚未取得，P10保持In Progress。

## 本轮文档静态检查

检查规则→接口→Task→case映射、链接、P9基线和scope，不能把静态review记成功能PASS。后续文档发现问题直接在这三份文件修订；无重复方案或第二套test matrix。

2026-09-29：独立Engineering静态review无Critical/Important；QA指出E-045缺PickUp Occupy、E-048超额fixture缺缩容步骤、M-010缺Released前置，均已修正并由原reviewer定点复核关闭，剩余Critical/Important为0。主agent核对了48组E、6组D、3组P、10项M、4项R的编号连续性、文档链接及文件范围。本轮仅新增三份P10文档，tracked旧文件无变更；没有Unity测试或代码实施结果。

上述为文档准备时的静态记录。Owner随后已批准接口/文件范围、全部E/D/P/M/R用例及六个Task；实施状态由上文checkbox与下方Task记录更新，静态结论不替代真实测试。



## Final Engineering 范围裁决（2026-09-29）

Controller接受review列出的五项边界，均不改变已批准P10功能：

1. Ruling: 真实站位、可达性、队列和随机到店沿用P11–14/P12分工；P10只验证逻辑名额。若后续未接空间检查，逻辑余量不代表真实队伍放得下。
2. Ruling: 当前已确认Interior region视为已铺Floor；P28解锁/铺设分离时必须更新adapter。若忽略这项交接，会把未铺格算入容量。
3. Ruling: 当前小型布局采用逐格HashSet统计，不在P10新增巨大地图资源预算或通用union算法。尚未验证巨大区域/大量重叠区域性能；扩大输入规模前必须评估裁剪和范围限制，否则可能耗时或耗尽内存。
4. Ruling: Released历史按已批准session语义保留，不提前加入会改变幂等行为的清理器。长期不Reset、百万次visit的吞吐未验证；正式长时间接入前需测量规模和生命周期。
5. Ruling: 原子性覆盖主线程同步、稳定普通输入集合下的正常命令成功/失败；不增加并发锁、恶意容器reentry或OOM恢复。若后续改变调用模型，必须重新设计相应边界。

以上是当前支持范围与未来接入风险，不把完整回归、必要GUI或Owner acceptance移出Phase验收。

### Owner授权后的final overall review与分支交付（2026-09-29）

Owner在收到验收范围及结果后要求：“如果所有的manual test都通过过了，做一次final overall review就可以push到branch上面，和p9同一个branch就好”。据此关闭委托manual的M-010/R-004认可，并进行本次独立整体review；上方“待Owner确认”“未commit”等描述是此前操作快照。

- Engineering：独立重读全部23个P10 C#及相关合同、测试；PASS，0 Critical / Important / open Minor，无代码修复。
- QA：独立解析原始XML及355个补跑名称对应，核对源码hash、manual原始截图与Owner补验来源；PASS，0 Critical / Important / open Minor。M-001…010全部通过，实际执行者区别保留。
- 自动回归之后23个源码内容未变，旧Assets/Packages/ProjectSettings无tracked变化；本轮只同步文档，不重跑Unity。review报告留本地scratch，公开结论以原test cases §9为准。
- 授权提交范围：23个C#、27个Unity .meta和6份Docs，共56个文件。只在 `codex/phase-9-order-domain` 上commit/push；outputs、scratch与既有无关工作不纳入，不merge、不开始P11、不删除worktree。
- P10开发与验收已满足分支交付条件；正式Roadmap Completed留待之后的main merge/closeout。实际commit与远端一致性在推送后核验，不能以当前文档代替Git操作成功证据。
