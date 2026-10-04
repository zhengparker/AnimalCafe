# Phase 9 — Order Domain Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` for inline execution, or `superpowers:subagent-driven-development` if the Owner selects that method. Apply `superpowers:test-driven-development` to each code Task. Steps use checkbox (`- [ ]`) syntax for tracking. Project process overrides repeated per-Task approval/commit ceremonies.

**状态：GUI、最终 review 与修复后的完整回归已完成；2026-09-29 focused 132+3 PASS、最终 EditMode 合并 2255 个唯一 PASS、全量 PlayMode 1039 PASS / 0 FAIL / 4 个既有可选 SKIP。本记录为推送前验证，交付分支为 `codex/phase-9-order-domain`；Owner 已授权最后 review 及提交、推送，远端提交由交付时核验。main merge、阶段 closeout 与 Roadmap 更新未执行。**

**Goal:** 建立可独立验证的订单核心，确保 FIFO 领取、正确顾客取餐、状态/权限和重复操作保护。

**Architecture:** 纯 C# `OrderService` 私有持有订单与 waiting IDs，只返回不可变 snapshot。Editor debug window 使用自己独立的 service，让 Owner 在 Unity 逐步验收；运行时不连接 Scene、NPC 或现有 GameEventBus。

**Tech Stack:** Unity `6000.5.5f1`、C#、现有 Runtime/Editor/EditMode/PlayMode assemblies、NUnit/Unity Test Framework；不增加 dependency。

**Spec:** `Docs/superpowers/specs/2026-09-28-phase-9-order-domain-design.md`

**Test Cases:** `Docs/superpowers/specs/2026-09-28-phase-9-order-domain-test-cases.md`

## Global Constraints

- 正确顾客实际取走后才 Completed；制作且送达后是 ReadyForPickup。
- Preparing 包含制作及送达中的阶段；临时设备/路径受阻不自动 Fail。
- Order ID 为正 long，在单个 service 生命周期内唯一递增；合法 Max 只分配一次。
- Failed/Completed 为 terminal；失败操作同时保持全部订单、queue、busy 和 next ID 不变。
- 同员工最多一张 Claimed/Preparing；Ready 保留 claimant 但释放工作额度，旧单终止不能释放新单。
- 只在指定新文件实现；不改 Scene、Prefab、现有 gameplay 或 asmdef；Assets 附 Unity 生成的 `.meta`。
- P9 无 Save、付款/退款、库存、NPC/NavMesh、capacity、正式 UI 或业务超时；不自动重领/转交。
- 单线程同步 API；不使用 async、锁、singleton、DI framework 或通用 repository。
- 当前在 Windows Editor 验证；Android/iOS 为后续目标，不报告本阶段真机 PASS。
- 用户无关改动保留；未经另行授权不 commit、push、merge、删 branch/worktree。
- 每个 Task focused tests + direct regression；完整 regression、部门 review 和 Owner manual 在 Phase 收尾集中执行。

## Review Focus

以下高风险输入已对应具体测试，review 时逐项核查证据：

1. Create 因参数错误/上限失败后，不消耗或复用 ID：Task 1 E-005/008；Task 3 E-037/040。
2. E1 在旧单 Ready 后领取新单，旧单 Complete/Fail 不能误释放新单：Task 3 E-020、Manual M-007。
3. 错误 actor、错误状态同时出现时错误码一致且无副作用：Task 3 E-024/025/032。
4. 外部保留旧 snapshot/集合，不能观察隐式更新或改变 service：Task 1 E-011/012、Task 3 E-036。
5. Domain Reload 关闭时窗口也清理临时数据；窗口不弄脏用户工作：Task 4 D-003/004/005、Task 5 M-009/010。

## 文件结构

```text
Assets/Scripts/Orders/
  OrderState.cs                    六状态 enum
  OrderSnapshot.cs                 get-only 查询数据
  OrderOperationFailureReason.cs   错误码 enum
  OrderResult.cs                   成功/失败结果
  OrderService.cs                  创建、查询、领取、转换及 FIFO
Assets/Editor/Phase9/
  OrderDebugSession.cs             独立临时 service 与 Reset
  OrderDomainDebugWindow.cs        手工验收窗口
Assets/Tests/EditMode/Phase9/
  OrderCreationQueryTests.cs       E-001…012
  OrderClaimTests.cs               E-013…021
  OrderTransitionTests.cs          E-022…036
  OrderInvariantTests.cs           E-037…040
  OrderTestSupport.cs              合法 fixture 与不可变状态捕获
  OrderDebugWindowTests.cs         D-001…006
Assets/Tests/PlayMode/Phase9/
  OrderDomainPlayModeTests.cs       P-001…003
Docs/Phase9_Beginner_Guide.md       操作说明与最终 evidence（实施时创建）
```

现有文档在实施期间仅更新本 spec、test cases、plan 的状态；Phase 收尾再更新 Roadmap 与 Beginner Guide。设计准备阶段创建了三份文档；获批后已将它们和 Game Design 的 Completed 规则带入 P9 worktree。

## Workspace / Approval Gate

- [x] Owner 于 2026-09-28 审核并批准 design、全部 automated/manual test cases 和本 plan，已创建独立开发环境；随后明确要求使用 sub-agent 开发。
- [x] 已创建开发 branch `codex/phase-9-order-domain`，worktree 为 `E:/Unity/Project/AnimalCafe/.worktrees/phase-9-order-domain`，基线为 `c80f208`。
- [x] 创建时实际 GitHub main、本地 main 与 origin/main 均为 `c80f2088fbff65e5a17b96206c1f32f6560719ce`；主目录用户资料保留。
- [x] 已复制三份方案和含 Completed 规则的 Game Design，复制后逐份校验一致，再仅在本 worktree 记录批准状态；主目录原副本保留。
- [x] 启动前核实 Unity 6000.5.5f1 安装与无其他 Editor；测试命令均在 P9 checkout 执行。Task 1 已取得真实 RED/GREEN，历史 P8R 结果不替代新证据。

最初的设计审批不自动包含 commit/push/merge；Owner 已于 2026-09-29 另行明确授权最后 review 及提交、推送当前 P9 分支，未执行 main merge 或阶段 closeout。创建 worktree 时 Assets、Packages、ProjectSettings 与 main 基线一致；随后按 Owner 指令开始 sub-agent 实施。每轮 Unity 运行前确认 checkout 与进程 ownership，证据见各 Task。

## 测试命令与证据约定

以下命令在**已批准的 P9 checkout 根目录**运行。设计准备时已用本机 `unity test --help` 只读核对 CLI flags，当时尚未运行 Unity tests；实际执行结果见各 Task 与配套 test cases。使用 `unity-cli` skill 确认运行方式，若 Editor 已占用项目则选已支持的 live runner 或在安全关闭后串行执行，不能同时启动第二个项目进程。

```powershell
New-Item -ItemType Directory -Force outputs/phase9 | Out-Null
unity test . --mode EditMode --filter AnimalCafe.Tests.EditMode.Phase9 --output outputs/phase9/focused-edit.xml --timeout 600
unity test . --mode PlayMode --filter AnimalCafe.Tests.PlayMode.Phase9 --output outputs/phase9/focused-play.xml --timeout 600
```

单个 Task 可把 filter 缩小为对应 fixture fullname；一个 run 完成后才开始下一个。RED 与 GREEN 分别使用 task-specific XML 路径，避免覆盖失败证据。没有安装 Unity/依赖时先报告实际缺项，不自动安装。

成功标准：读取 XML test fullname 与 total/passed/failed/skipped，检查实际选中了预期测试且 0 FAIL；空报告或错误 filter 不算通过。RED 必须与缺失行为有关，环境/编译错误不冒充业务 RED；新类型先用最小可编译 stub 建立行为失败，再实现。允许为测试引入编译骨架，不提前写完整逻辑。

## Task 1 — 订单资料、创建、查询与 ID 上限

**Create:** 五个 runtime 文件；`OrderCreationQueryTests.cs`、`OrderTestSupport.cs`。

**Consumes:** 无其他 gameplay service。

**Produces:**

```csharp
OrderService(long firstOrderId = 1)
OrderResult Create(string customerId, string productId)
bool TryGetOrder(long orderId, out OrderSnapshot order)
IReadOnlyList<OrderSnapshot> GetOrders()
IReadOnlyList<long> GetWaitingOrderIds()
```

数据属性、enum 值和 result 合同逐字采用 design 第 3/5 节；不得增加公开 setter。

- [x] 为 E-001…012 写 NUnit tests，fixture namespace `AnimalCafe.Tests.EditMode.Phase9`。
- [x] 使用最小 stub 编译，运行 `OrderCreationQueryTests`：31 total，30 个预期行为失败、1 个空 service 基线通过。
- [x] 实现私有 Dictionary + waiting List，先验证参数再分配 ID，明确保护 ID 耗尽。
- [x] 实现独立只读 snapshot/集合，TryGetOrder 的 false/null 语义和 constructor 参数检查。
- [x] GREEN：31/31 PASS、0 FAIL/SKIP；独立 review 无 findings。证据 `outputs/phase9/task1-red.xml`、`task1-green.xml`。

代表性验收测试（除当前接口外不要求额外 facade）：

```csharp
[Test]
public void Create_InvalidCustomerDoesNotConsumeFirstId()
{
    var service = new OrderService();
    var rejected = service.Create("", "coffee");
    Assert.That(rejected.FailureReason,
        Is.EqualTo(OrderOperationFailureReason.InvalidCustomerId));
    Assert.That(service.GetOrders(), Is.Empty);
    Assert.That(service.Create("C1", "coffee").Order.OrderId, Is.EqualTo(1L));
}
```

## Task 2 — FIFO 与领取所有权

**Modify:** `OrderService.cs`。

**Create:** `OrderClaimTests.cs`。

**Consumes:** Task 1 的 Create、snapshots、query。

**Produces:** `OrderResult ClaimNext(string employeeId)`。

- [x] 写 E-013…017：空队列、严格 FIFO、非法 employee、Claimed busy、顺序抢单。
- [x] RED：11 total，10 个预期失败、1 个空队列基线通过。
- [x] 先检查 employee 格式，再检查当前 Claimed/Preparing 订单，最后检查空队列；一次成功只更改队首一单。
- [x] 输出 Claimed snapshot 并从 waiting List 移除该 ID；不暴露按 ID 抢指定订单的 API。
- [x] Task 1+2 GREEN：42/42 PASS，独立 review 无 findings；E-018…021 留 Task 3。证据 `outputs/phase9/task2-red.xml`、`task2-green.xml`。

```csharp
[Test]
public void ClaimNext_BusyEmployeeLeavesSecondOrderWaiting()
{
    var service = new OrderService();
    service.Create("C1", "coffee");
    service.Create("C2", "coffee");
    Assert.That(service.ClaimNext("E1").Order.OrderId, Is.EqualTo(1L));
    Assert.That(service.ClaimNext("E1").FailureReason,
        Is.EqualTo(OrderOperationFailureReason.EmployeeBusy));
    Assert.That(service.GetWaitingOrderIds(), Is.EqualTo(new long[] { 2 }));
}
```

## Task 3 — 状态转换、正确顾客与终态保护

**Modify:** `OrderService.cs`、`OrderClaimTests.cs`、`OrderTestSupport.cs`。

**Create:** `OrderTransitionTests.cs`、`OrderInvariantTests.cs`。

**Produces:**

```csharp
OrderResult StartPreparation(long orderId, string employeeId)
OrderResult MarkReadyForPickup(long orderId, string employeeId)
OrderResult Complete(long orderId, string customerId)
OrderResult Fail(long orderId, string reason)
```

- [x] 写 E-018…040；E-023 必须展开六状态 × 四操作的 24 个组合，使用真实 API 建状态。
- [ ] 对各转换的小组运行 RED。实际：StartPreparation 1 个预期 FAIL、Fail 3 个预期 FAIL；MarkReadyForPickup/Complete 没有独立初始 RED，保留此流程缺口，不能补写为已执行。
- [x] 按 design 校验顺序实现四个显式操作；Fail 清理 queue，所有 terminal 拒绝覆盖。
- [x] busy 从现有 Claimed/Preparing 订单判断；终止旧 Ready 单不能解除新单占用。
- [x] 实现 seed=17、200 次操作的不变量检查，独立记录创建顺序；日志包含 seed、step 和操作，不另建完整 reference model。
- [x] 运行已实现全部 P9 EditMode domain tests 至 GREEN；检查 U/I 与快照隔离，不提前接 GameTimeService/Scene。

```csharp
[Test]
public void Complete_WrongCustomerLeavesOrderReady()
{
    var service = new OrderService();
    service.Create("C1", "coffee");
    service.ClaimNext("E1");
    service.StartPreparation(1, "E1");
    service.MarkReadyForPickup(1, "E1");
    Assert.That(service.Complete(1, "C2").FailureReason,
        Is.EqualTo(OrderOperationFailureReason.WrongCustomer));
    Assert.That(service.TryGetOrder(1, out var order), Is.True);
    Assert.That(order.State, Is.EqualTo(OrderState.ReadyForPickup));
    Assert.That(service.Complete(1, "C1").Order.State,
        Is.EqualTo(OrderState.Completed));
}
```

**Task 3 结果：** `task3-fix1-green.xml` 共 126 PASS、0 FAIL/SKIP；独立 review 要求补强失败操作的 busy/next-ID probe，修复后 SPEC/QUALITY PASS。四个 API 与 E-018…040 已覆盖；RED 限制如上。

## Task 4 — 可人工验收的 Editor 工具与帧边界

**Create:** 两个 Editor 文件、`OrderDebugWindowTests.cs`、`OrderDomainPlayModeTests.cs`；创建 `Docs/Phase9_Beginner_Guide.md` 的操作章节。

**Consumes:** 完整 OrderService；现有 Editor InternalsVisibleTo、NUnit、Unity Test Framework。

**Produces:**

```csharp
// Namespace: AnimalCafe.EditorTools.Phase9
internal sealed class OrderDebugSession
{
    internal OrderService Service { get; }
    internal void Reset();
}
public sealed class OrderDomainDebugWindow : UnityEditor.EditorWindow
{
    // MenuItem: Window/AnimalCafe/Phase 9 Order Debug
    internal OrderDebugSession Session { get; }
    internal void ResetSession();
}
```

- [x] 为 D-001…006 写 Editor tests、P-001…003 写 PlayMode tests；时间状态在 try/finally 或 TearDown 恢复。
- [x] 运行 debug session/Reset 的 RED；PlayMode test 必须验证跨帧与时钟独立性，若已有 domain 正确而直接 GREEN，如实记为新增验证，不人为制造产品缺陷。
- [x] 实现窗口的字段、按钮、只读表格和 last result，严格按 design 第 6 节；domain 命令直接委托 Service，不复制规则。
- [x] ResetSession 同时清空 session/输入/结果；绑定真实 playModeStateChanged，并在 OnDisable 解除。查询/打开/重置不使用 AssetDatabase.SaveAssets、Scene setup、PlayerPrefs 或 Time.timeScale 写入。
- [x] 执行 focused EditMode 和 P9 PlayMode；检查 window 开关不会污染用户 Scene/Selection。
- [x] Codex 于 2026-09-28 实际操作正常/错误路径，核对菜单、按钮与 Guide；按 Owner 授权记录执行者 Codex，不冒称 Owner 亲测。
- [x] Guide 引用 M-001…010 原 case ID，给新手窗口入口、默认输入、Play Mode 操作及必要代表截图；不重复发明验收表。

真实 Domain Reload on/off 的 Play 切换已由 M-009 实测通过；不以直接调用回调替代真实 Editor 生命周期证据。

**Task 4 结果：** 历史自动结果为 `task4-edit-green2.xml` 132 PASS、`task4-play-green2.xml` 3 PASS，均 0 FAIL/SKIP；独立 QUALITY PASS。此前 native UI 控制受限的 BLOCKED 已解决，正常/错误路径、窗口生命周期与代表截图已补齐。GUI 发现的 Reset 焦点显示缺陷已修复并实际复测。M-001…009 和 M-010 其他子项由 Codex 于 2026-09-28 完成，M-010 Camera 拖动由 Owner 于 2026-09-29 在本 chat 回复“能移动的”确认，M-001…010 全部 PASS。最新 focused 与 full regression 状态见 Task 5。
## Task 5 — 集中回归、Review 与 Owner 验收

**Modify:** 本 spec/test/plan 的实际状态、`Docs/Phase9_Beginner_Guide.md`；获得 Phase closeout 批准后更新 Roadmap。

- [x] 2026-09-28 已串行执行历史完整 EditMode 与 PlayMode；以下命令与结果属于 Reset 焦点修复前版本：

```powershell
unity test . --mode EditMode --output outputs/phase9/full-edit-final.xml --timeout 3600
unity test . --mode PlayMode --output outputs/phase9/full-play.xml --timeout 1200
```

- [x] 读取新 XML，核对 test fullname 覆盖与 PASS/FAIL/SKIP；区分 fixture/环境失败和产品失败。
- [x] Engineering 检查 R-002，QA 检查 R-003；集中一次正式 review。诊断窗口不涉及正式视觉资产，Owner 仍需判断可读性。
- [x] 交付 Guide，M-001…010 均 PASS，逐项记录执行者、日期、checkout/commit 和证据。Codex 执行获批的功能性 GUI 用例，Owner 补核 M-010 Camera 拖动。
- [x] 修复 Critical/Important，补 focused regression，并完成修复后的最终 full regression。Reset 焦点修复、2026-09-29 focused 132+3 与最终 EditMode 2255 唯一测试已通过；全量 PlayMode 1039 PASS、0 FAIL、4 个既有可选 SKIP，必需功能无未覆盖。docs-only 修订按项目流程免 Unity 重跑。
- [x] 在 Guide 汇总准确自动化 counts、manual ledger 链接、review 结论、已知限制；只有满足 gate 且 Owner 批准才把 P9 标为 Completed。
- [x] 明确提交/合并权限和最终文件范围；Owner 于 2026-09-29 已授权最后 review 及提交、推送当前 P9 分支。main merge、阶段 closeout 与 P10 启动未执行。

**Task 5 历史自动结果（2026-09-28，Reset 焦点修复前）：** `full-edit-final.xml` 1900 PASS / 0 FAIL / 355 SKIP；三组隔离补跑 160+194+1 PASS，exact fullname 合并完整覆盖 2255 个唯一测试。`full-play.xml` 1039 PASS / 0 FAIL / 4 个既有 P8R opt-in SKIP。Engineering 无新增代码 findings，QA 独立核对全部名称与原 SKIP 集精确对应。当时测试后 P9 hash 一致、tracked Assets 无差异、所有 Unity runner 已退出；这些历史结果不表示修复后的 full regression 已完成。

**当前验证（2026-09-29，包含 Reset 焦点修复）：** 最终 Engineering/QA review 已完成，未发现新的代码或覆盖问题；旧文档状态已同步。`pre-push-2026-09-29-edit.xml` 为 132/132 PASS，`pre-push-2026-09-29-play.xml` 为 3/3 PASS，均 0 FAIL/SKIP。最终 `pre-push-2026-09-29-full-edit.xml` 为 1900 PASS / 355 SKIP / 0 FAIL；本次新进程 `pre-push-2026-09-29-isolated-maincafe.xml` 160 PASS、`pre-push-2026-09-29-isolated-validator.xml` 194 PASS、`pre-push-2026-09-29-isolated-scene.xml` 1 PASS，均 0 FAIL/SKIP。按 Ordinal exact fullname 合并后 2255 个唯一测试全部有 PASS，隔离补跑恰好覆盖本次 355 SKIP，无缺漏/额外；汇总为 `outputs/phase9/pre-push-2026-09-29-edit-verification.json`。最终 `pre-push-2026-09-29-full-play.xml` 为 1043 total / 1039 PASS / 0 FAIL / 4 SKIP，四个 SKIP 的 fullname 与 reason 均与历史相同，属于既有 P8R 可选截图/spacing 检查。

**最终环境核验：** `outputs/phase9/pre-push-2026-09-29-verification.json` 记录本次 7 份报告均无 FAIL，32 项 baseline 文件 hash 未变，tracked Assets/Packages/ProjectSettings 的 staged/unstaged diff 均为空，UnityProcesses=0。测试生成资源差异已备份并恢复；原始 XML 中的 SKIP 与历史 RED 不足继续保留。

**交付边界：** 本次 GUI、review、focused 与完整 regression gate 已通过；独立的阶段 closeout 尚未执行，Roadmap 不标 Completed，main 未合并。提交、推送按已记录授权执行，远端结果由交付时核验。

交付代码位于以 `c80f208` 为基线的 `codex/phase-9-order-domain` 开发分支。本记录为推送前验证；Owner 授权已记录，远端提交由交付时核验。运行证据位于 `outputs/` 和 `.superpowers/`，保留用于追溯，不纳入生产代码提交。主 checkout 的既有改动已保留。
## 审核与执行方式

Owner 已选择 subagent-driven：依次派遣 Task implementer，完成后进行 task-scoped review，Phase 收尾由独立 Engineering/QA reviewer 检查。所有实现者使用同一份 test cases 与 Phase gates，不重复建计划，也不并行修改同一 domain。

Owner 已审核完整方案与 tests；各 Task checkbox 记录实际进度。文档静态检查不等于功能 test PASS。后续开发以本 P9 worktree 中的方案为准，主目录保留的是设计准备副本。

### 设计准备复核记录（2026-09-28，历史记录）

以下为实施前记录；Owner 已批准。最新开发和验证状态见上文。

- Engineering 与 QA 独立只读复核三份草案；ID 耗尽断言、FIFO fixtures、Waiting 恢复步骤、错误身份路径及窗口生命周期步骤已修正，并由原 reviewer 定点复核。
- 当前无剩余设计 finding，建议提交 Owner 审核；这是文档结论，不是 Phase closeout。
- 测试清单为 40 项 domain、6 项 Editor 工具、3 项 PlayMode、3 项 regression/review 和 10 项 manual；参数化用例执行后另按 XML 统计。这是设计准备时的状态；当前执行结果见各 Task 和 test cases 第 9 节。
