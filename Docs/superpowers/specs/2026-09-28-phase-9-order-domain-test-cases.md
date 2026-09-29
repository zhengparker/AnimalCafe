# Phase 9 — Order Domain Test Cases

> 状态：GUI、最终 review 与修复后的完整回归完成 — M-001…010 PASS；M-001…009 及 M-010 其他子项由 Codex 于 2026-09-28 实测，M-010 Camera 拖动由 Owner 于 2026-09-29 确认。2026-09-29 focused 132+3 PASS、最终 EditMode 2255 个唯一 PASS、全量 PlayMode 1039 PASS / 0 FAIL / 4 个既有可选 SKIP。本记录为推送前验证，交付分支为 `codex/phase-9-order-domain`；Owner 授权已记录，远端提交由交付时核验。main merge、阶段 closeout 与 Roadmap 更新未执行。历史及当前证据分别见第 9 节。
> 日期：2026-09-28
> Design：`2026-09-28-phase-9-order-domain-design.md`
> Plan：`../plans/2026-09-28-phase-9-order-domain.md`

## 1. 测什么、谁来做

用一组模拟顾客和员工，验证“正确的人按正确顺序处理正确的订单”。例如，C2 不能领走 C1 的商品；重复点击完成也不能再结算一次状态。

| 层次 | 执行者 | 证明什么 |
|---|---|---|
| E：EditMode domain | Codex / NUnit | ID、状态、权限、FIFO、失败无副作用 |
| D：EditMode Editor tool | Codex / NUnit | debug session、窗口开关/Reset、Scene 不受污染 |
| P：PlayMode | Codex / Unity Test Runner | 纯 domain 跨帧稳定、不依赖时间倍速 |
| R：Regression / review | Codex；Engineering / QA reviewer | 完整项目回归与 scope/coverage 检查 |
| M：Manual / GUI | Owner 授权的 Codex 使用 computer use 在 Unity 操作；M-010 Camera 拖动由 Owner 补核 | 操作步骤可执行、结果看得懂、真实窗口生命周期与已有玩法 smoke |

这是 P9 当前合同的预先覆盖清单；实现中发现遗漏应先补具体 case，再修复。case 数量不同于 NUnit total：参数化测试会展开为多个结果，执行时按 XML 实际统计。

结果使用 `PASS / FAIL / SKIP / BLOCKED / NOT_RUN`。SKIP 不能算 PASS；执行受环境限制使用 BLOCKED 并记录原因。Owner 已于 2026-09-28 授权 Codex 代跑以下功能性 GUI 用例；只有实际操作与证据支持的项目可记 PASS。每项记录实际执行者，Codex 操作不表示 Owner 本人亲测；M-010 Camera 拖动另记 Owner 于 2026-09-29 的人工确认。

## 2. 公共前置条件与断言

- Unity `6000.5.5f1`；每个 domain test 创建全新 `OrderService()`，默认第一单 ID=1。
- 固定身份：C1/C2/C3；E1/E2/E3；商品 `coffee`；失败原因 `diagnostic.stop`。
- 创建 N 单：依次 Create(C1,coffee)、Create(C2,coffee)、Create(C3,coffee)，更多顾客用 C4…。
- `ToState(S)`：使用真实公开 API 合法推进订单到 S；不靠反射/直接改内部字段伪造状态。
- **U（失败无副作用）**：比较调用前后全部 snapshot 字段、GetOrders 顺序/数量、waiting IDs。再用独立 probe 验证原 employee 的 busy 结果；ID 尚未耗尽时，检查下一次合法 Create 的编号未变；已耗尽时，重复 Create 必须仍为 IdExhausted，既有订单/queue 不变且无回绕。probe 在比较之后进行，不能把 probe 写入混进被测操作。
- **I（不变量）**：所有 ID 正数且唯一；waiting IDs 恰好是所有 Waiting 订单，按创建顺序且无重复；非 Waiting 都不在队列；每个员工最多一张 Claimed/Preparing；Completed/Failed 不可再变。
- 成功 result：Succeeded=true、FailureReason=None、Order 为当前不可变 snapshot。失败 result：Succeeded=false、明确错误码、Order=null。
- 输入 ID 比较为 ordinal case-sensitive；null/空字符串/全空白/首尾有空白的身份和 product ID 均非法。Fail reason 仅要求非空白，保留有效原文。
- 测试必须在 TearDown 恢复自己修改的 Time.timeScale、窗口和设置；保留用户 Scene、Prefab、Selection 与已有窗口。fixture 无法安全隔离时 BLOCKED，不强行保存或关闭用户工作。

## 3. E — Automated EditMode Domain Cases

每行均为 `前置条件 → 操作 → 预期结果`，执行者 Codex；实际执行状态见第 9 节。

### 3.1 创建、ID 与查询（Task 1）

| ID | 前置条件与操作 | 预期结果 |
|---|---|---|
| P9-E-001 | 新建默认 service，查询全部订单/队列 | 都为空；查询不存在的 1 返回 false/null |
| P9-E-002 | Create(C1,coffee) | ID=1、Waiting、正确 customer/product、claimant/reason=null、queue=[1]；成功 result 合同成立 |
| P9-E-003 | 依次创建三单 | IDs=1,2,3；列表与 queue 都为 [1,2,3]，无重复 |
| P9-E-004 | 同一 C1 与 coffee 连续 Create 两次 | 创建两个不同 ID；明确这是两个业务请求，不自动合并 |
| P9-E-005 | 分别给 customer/product 输入 null、空串、空格、tab、首/尾空白；其他参数有效 | 相应 InvalidCustomerId/InvalidProductId，U；之后合法创建仍取得原 next ID |
| P9-E-006 | Create(C1,coffee)、Create(c1,COFFEE)，以及合法中文 ID | 原文保留且区分大小写；P9 不查询菜单，也不拒绝合法未知 product ID |
| P9-E-007 | firstOrderId=0、-1、long.MinValue | 构造抛 ArgumentOutOfRangeException；默认值与 firstOrderId=5 可正常创建对应编号 |
| P9-E-008 | firstOrderId=long.MaxValue-1，创建三次 | 前两次 ID 为 Max-1、Max；第三次 IdExhausted，U；没有负数/覆盖/回绕 |
| P9-E-009 | 查询 ID=0、-1、long.MinValue、999；已有订单为 1 | TryGetOrder 都为 false/null，已有 snapshot 与 queue 不变 |
| P9-E-010 | 两个独立 service 各创建一单；只操作其中一个 | 各自 ID=1，数据互不影响；不宣称跨 service ID 全局唯一 |
| P9-E-011 | 捕获 GetOrders/GetWaitingOrderIds 返回集合，再创建第二单 | 旧集合仍只含第一单；新查询含两单 |
| P9-E-012 | 尝试通过返回集合可用的可变接口更改/清空；检查 snapshot 公开属性 | 不存在外部修改权，或写入抛 NotSupportedException；service 数据不变；snapshot 没有公开 setter |

### 3.2 FIFO 与领取（E-013…017 为 Task 2，其余随 Task 3 集成）

| ID | 前置条件与操作 | 预期结果 |
|---|---|---|
| P9-E-013 | 空 service 上 ClaimNext(E1) | NoWaitingOrders，U |
| P9-E-014 | 三张 Waiting；E1、E2、E3 依次 ClaimNext | 各得 1、2、3；每步准确 dequeue，claimant 匹配；第 4 位员工得到 NoWaitingOrders |
| P9-E-015 | 两张 Waiting；employee 输入公共非法字符串集合 | InvalidEmployeeId，U；随后 E1 仍得到 1 |
| P9-E-016 | E1 已领取 1；在 Claimed 时再领；分别有/无其他 Waiting | 均 EmployeeBusy，U；即使空队列也优先返回 EmployeeBusy |
| P9-E-017 | 两单；E1 请求后 E2 紧接着请求 | 每个请求仅领取一个不同 ID，#1 claimant 不被 E2 改写；这是顺序请求测试，不是多线程证据 |
| P9-E-018 | E1 的 #1 Preparing 时再次 Claim；随后推进到 Ready 再 ClaimNext(E1) | Preparing 时 EmployeeBusy 且 U；Ready 后取得 #2；#1 仍 Ready 且 claimant=E1；#2 Claimed |
| P9-E-019 | E1 的 Claimed/Preparing #1 被 Fail，再 ClaimNext(E1) | 可取 #2；#1 Failed 且保留 claimant/原因 |
| P9-E-020 | E1 的 #1 Ready，#2 Claimed；分别 Complete 或 Fail #1；再 ClaimNext(E1) | #2 始终占用 E1；新领取仍 EmployeeBusy；不能由旧单终止误释放新单 |
| P9-E-021 | 三组独立 fixture 各创建 1,2,3；每组只 Fail 一个位置：队首/中间/队尾，再用不同员工领取剩余 | 分别得到 [2,3]、[1,3]、[1,2]；无遗漏/重复，Failed ID 永不出现；I |

### 3.3 状态、权限与恢复（Task 3）

| ID | 前置条件与操作 | 预期结果 |
|---|---|---|
| P9-E-022 | #1 经 Claim→Start→Ready→Complete(C1) | 精确经历六状态中的正常五状态；Ready 仍未完成；最后 Completed，不要求离店事件 |
| P9-E-023 | 下表六状态 × 四个按 ID 操作；参数均有效且 actor 正确 | 每个组合严格按转换矩阵；拒绝分支 U，成功分支 I |
| P9-E-024 | #1 Claimed 时 Start(1,E2)；Preparing 时 Ready(1,E2) | NotClaimOwner，U；E1 随后可成功执行 |
| P9-E-025 | C1 的 Ready #1，分别 Complete(1,C2)、Complete(1,c1)，再 Complete(1,C1) | 前两次 WrongCustomer 且 U；最后成功 Completed |
| P9-E-026 | Start 成功再重复；Ready 成功再重复；Complete/Fail 成功再重复 | 前两者 InvalidTransition；terminal 重复为 OrderTerminal；U；原 termination reason 不被覆盖 |
| P9-E-027 | 在四个非终态分别 Fail(1,diagnostic.stop) | Failed、reason 准确保留、原身份不变；Waiting 队列移除；活跃 claimant 释放；I |
| P9-E-028 | 在每个非终态给 Fail reason 输入 null、空串、空格、tab | InvalidFailureReason，U；有效原因随后可终止；有效原因的首尾空格保留 |
| P9-E-029 | Completed/Failed 上尝试 Start、Ready、Complete、Fail，参数有效 | 都 OrderTerminal、U；不新增队列；两个不同终态都保持原样 |
| P9-E-030 | 对四种按 ID 操作输入 0、-1、long.MinValue；再输入未使用正 ID=999 | 前者 InvalidOrderId，后者 OrderNotFound，U；TryGet 与 mutation 的返回方式有区别 |
| P9-E-031 | 对 Start/Ready 的 employee、Complete 的 customer 输入全部非法 ID 样本 | 格式错误优先于不存在/terminal/state；U；错误码为对应 InvalidEmployeeId/InvalidCustomerId |
| P9-E-032 | 多重错误：Create 两参数非法；非法 orderId+非法 actor；未知 ID+非法 actor；terminal+错误身份；错误状态+错误身份 | 顺序依次为 InvalidCustomerId、InvalidOrderId、参数格式错误、OrderTerminal、InvalidTransition；均 U |
| P9-E-033 | 三组独立 fixture 的 #1 分别停在 Waiting/Claimed/Preparing，仅查询；Waiting 组先由 E1 Claim，其他组沿用原 owner E1，再继续 | 查询不自动改变状态/owner/queue；三组都能继续到 Completed；不模拟路径系统，不触发 Fail |
| P9-E-034 | #1 E1 领取、#2 E2 领取；先完整处理 #2，再处理 #1 | 合法，两单 Completed；领取仍为 FIFO，完成无需 FIFO |
| P9-E-035 | 先做错 owner/非法状态/错误 customer，再用正确步骤完成；随后创建与领取新单 | 错误不毒化 service；原单完成，新单编号递增且可按 FIFO 处理 |
| P9-E-036 | 捕获 Waiting snapshot，推进到 Ready，再捕获并 Complete | 旧 snapshot 字段保持原值；新查询给最新状态；返回 result snapshot 也不可变 |

#### P9-E-023 转换矩阵（共 24 个参数组合）

所有行使用正确 customer/claimant 与非空 reason；Waiting 没有 claimant，但仍使用有效 E1 格式。终态检查先于 state/identity。

| 当前状态 | StartPreparation | MarkReadyForPickup | Complete | Fail |
|---|---|---|---|---|
| Waiting | InvalidTransition | InvalidTransition | InvalidTransition | Failed |
| Claimed | Preparing | InvalidTransition | InvalidTransition | Failed |
| Preparing | InvalidTransition | ReadyForPickup | InvalidTransition | Failed |
| ReadyForPickup | InvalidTransition | InvalidTransition | Completed | Failed |
| Completed | OrderTerminal | OrderTerminal | OrderTerminal | OrderTerminal |
| Failed | OrderTerminal | OrderTerminal | OrderTerminal | OrderTerminal |

### 3.4 综合不变量（Task 3）

| ID | 前置条件与操作 | 预期结果 |
|---|---|---|
| P9-E-037 | 正常创建、完成和失败后继续创建；另在 Max 上限实例终止最后一单后再创建 | 正常实例不复用旧 ID；耗尽实例仍 IdExhausted，不能靠完成/失败复用 Max |
| P9-E-038 | 固定 seed=17，200 次混合 Create/Claim/Start/Ready/Complete/Fail；含非法输入 | 每步 I，拒绝分支 U；测试记录成功创建顺序并核对 Waiting FIFO；失败时输出 seed、step、命令 |
| P9-E-039 | 创建 1000 单，轮换 E1/E2，混合完成/显式失败直至无 Waiting/active | 无漏单、双领、双完成、剩余 busy 或重复 queue；保留 1000 个终态记录；只作为有界压力正确性测试，不设性能 KPI |
| P9-E-040 | ID 已耗尽但存在 Ready/Waiting 单；继续合法 claim/完成/Fail | 旧单可继续处理，只有 Create 受 IdExhausted 影响；I |

测试不访问内部集合来替代业务操作；E-038 只维护创建顺序并核对不变量，不另建完整状态机。明确错误码与转换由 E-023 等例子测试负责。

## 4. D — Automated Editor Tool Cases（Task 4）

namespace：`AnimalCafe.Tests.EditMode.Phase9`。D-001…006 自动测试 PASS，证据见第 9 节。

| ID | 前置条件与操作 | 预期结果 |
|---|---|---|
| P9-D-001 | 两个独立 OrderDebugSession；只在一个创建订单 | Service 隔离，各自从 ID=1 开始；没有 static service |
| P9-D-002 | session 中已有 Waiting/Ready/Failed；Reset 后创建 | 空队列/空订单，下一 ID=1；持有旧 service 的引用不会影响新 service |
| P9-D-003 | 测试独占的新窗口创建订单，调用同一个 ResetSession 入口 | 新 service、默认输入、清空最后结果；窗口查询显示空数据，不沿用旧 snapshot |
| P9-D-004 | 测试独占窗口创建订单后 Close，再新开 | 新 session，旧记录消失；关闭不产生 exception；不关闭用户已有窗口 |
| P9-D-005 | 捕获 loaded scenes 的 dirty flags、root IDs、active Scene、Selection、Time.timeScale；开窗口、执行 domain 操作、Reset、Close | 捕获值完全不变；无 Scene/Prefab/PlayerPrefs 写入；运行时验证值并配合 R-003 source review 验证写入禁令 |
| P9-D-006 | 先 ClaimNext(E1) 得空队列错误；Create(C1,coffee)→ClaimNext(E1)→StartPreparation(1,E2)，随后读取 session | 依次 NoWaitingOrders、成功、成功、NotClaimOwner；#1 仍 Claimed/E1；窗口只显示 result，不抛 Console error |

窗口按钮输入/可读性和真实 Play Mode 切换由 M 用例验证；以上自动测试不声称模拟真实鼠标点击或 Domain Reload。项目已有 Editor InternalsVisibleTo 可供测试 internal adapter，无需改 assembly。

## 5. P — Automated PlayMode Cases（Task 4）

namespace：`AnimalCafe.Tests.PlayMode.Phase9`。直接创建纯 service，不加载或改写 MainCafe。P-001…003 自动测试 PASS，证据见第 9 节。

| ID | 前置条件与操作 | 预期结果 |
|---|---|---|
| P9-P-001 | 创建 service；每个合法步骤之间 yield 一帧，走完整订单流程 | 跨帧仍按正确状态/身份工作；无 MonoBehaviour 或 Scene 查找依赖 |
| P9-P-002 | 准备 Waiting/Claimed/Preparing/Ready 四单；分别 Time.timeScale=0,1,2，每档等待 3 帧并 yield WaitForSecondsRealtime(0.1f) | 订单/owner/FIFO 无自动变化或超时；在每档可用合法诊断命令继续；不据此批准正式 Pause 管理行为 |
| P9-P-003 | 创建 service A 后释放测试引用，再建 B；运行帧并操作 B | B 空且从 1 开始，不继承 A；test 自己恢复原 timeScale，无预期之外 Console Error/Exception |

## 6. R — Regression 与 Review（Task 5）

| ID | 执行与检查 | 通过条件 |
|---|---|---|
| P9-R-001 | 完整 EditMode 与完整 PlayMode（包含现有 P0–8R tests） | 0 FAIL；SKIP 逐项解释且必要功能无未覆盖；读取 XML，不能只看 process exit code |
| P9-R-002 | Engineering 检查 runtime/Editor 分离、public state 写入口、ID/错误/权限/queue 合同、资源改动 | 无未解决 Critical/Important；无场景迁移或新增 package；代码与 spec 一致 |
| P9-R-003 | QA 核对 E/D/P/M 映射、真实 RED/GREEN、no-write 代码路径、窗口事件注册/解除、manual 记录 | 每个计划项有 test fullname 或 manual 记录；无用 fixture/反射绕过所有权；自动测试与实际 GUI 证据分开 |

全量 regression 在 Phase 收尾集中运行。Task 1–3 只跑 P9 已实现 domain tests；Task 4 增加工具与 PlayMode tests。无关文档修改不启动 Unity。

## 7. M — Manual / GUI Cases

### 操作准备

窗口已在 P9 worktree 实现。Beginner Guide 已按源代码中的菜单/按钮核对同一组步骤；本轮由已授权的 Codex 执行真实 GUI 操作，M-010 Camera 拖动由 Owner 补核，各项状态以本节结果表为准。

1. 在批准的 P9 checkout 打开 Unity `6000.5.5f1`，等待编译完成。
2. 保留自己的未保存工作；用已保存的 `Assets/Scenes/MainCafe.unity` 做 smoke，若有 dirty Scene/Prefab 先自行处理，测试不自动保存。
3. 打开 `Window > AnimalCafe > Phase 9 Order Debug` 与 Console。
4. 除 M-009/010 外，在 Play Mode 开始后点 Reset Session 再测试。每个 case 从 Reset 开始。
5. 填字段后点击对应按钮。Order ID 手工填指定编号；按钮不会自动改成另一单。`Simulate Customer Collection` 调用 Complete。
6. 对每步观察 last result、订单表的 State/Customer/Claimant/Reason 和 waiting FIFO。预期错误应显示在窗口，Console 不应有新增 Error/Exception。

### P9-M-001 — 正常订单与正确取餐者

- 前置：Reset；Customer=C1、Product=coffee、Employee=E1、Order=1。
- 操作：Create → Claim Next → Start Preparation → Mark Ready For Pickup。
- 预期：依次 Waiting、Claimed、Preparing、ReadyForPickup；Ready 时没有 Completed。
- 操作：Customer 改为 C2，点 Simulate Customer Collection；再改回 C1 重试。
- 预期：第一次 WrongCustomer、仍 Ready；第二次 Completed、queue 为空；无需模拟离店。

### P9-M-002 — FIFO 与员工忙碌

- 前置：Reset；用 C1/C2/C3 各 Create 一单。
- 操作：E1 Claim 两次；E2 Claim 一次。
- 预期：E1 得 #1；重复请求 EmployeeBusy、queue 不变；E2 得 #2，queue=[3]。
- 操作：Order=1、Employee=E1，Start → Ready；E1 再 Claim。
- 预期：领取 #3；#1 仍 Ready 且 claimant=E1；#2 仍归 E2。

### P9-M-003 — 非法跳步与错误员工

- 前置：Reset；Create #1，E1 Claim。
- 操作：直接 Ready；随后 E2 Start；再 E1 Start；再 E2 Ready；最后 E1 Ready。
- 预期：依次 InvalidTransition、NotClaimOwner、成功 Preparing、NotClaimOwner、成功 Ready；每次失败都保留原状态/owner/queue。

### P9-M-004 — 失败订单离开队列与员工恢复

- 前置：Reset；创建 #1、#2、#3。
- 操作：Order=2，reason=diagnostic.stop，Fail；E1 Claim #1 后对 #1 Fail；E1 再 Claim。
- 预期：#2 Failed 后 queue=[1,3]；#1 Failed 释放 E1；最后领取 #3，两个 Failed 永不重新出现；原因可读。

### P9-M-005 — 重复操作与终态保护

- 前置：Reset；按 M-001 正确流程将 #1 完成，再创建 #2 并 Fail。
- 操作：分别对 #1、#2 重复 Complete/Fail/Start；再让新员工 Claim。
- 预期：有效格式的终态请求均 OrderTerminal；原因和原终态不变；空队列 NoWaitingOrders。

### P9-M-006 — 无效输入后可以继续

- 前置：Reset；Customer 清空再 Create；Customer=C1、Product 清空再 Create。
- 预期：相应 InvalidCustomerId/InvalidProductId；没有订单。
- 操作：恢复合法输入 Create，确认 ID=1；Order=0 点 Start；Order=999 点 Start；空 Employee 点 Claim；恢复 E1 再 Claim。
- 预期：依次 InvalidOrderId、OrderNotFound、InvalidEmployeeId；最后正常领取 #1。把 Order 改回 1，空 reason 的 Fail 同样拒绝且不改变 #1。

### P9-M-007 — 旧订单结束不影响员工新订单

- 前置：Reset；创建 #1/#2/#3；E1 将 #1 推进到 Ready，再 Claim #2。
- 操作：Order=1，Customer=C1 完成 #1；E1 再 Claim。
- 预期：仍 EmployeeBusy；#2 归 E1、queue=[3]。再次 Reset，重复前置但改为 Fail #1，预期相同。

### P9-M-008 — 暂时停滞与倍速边界

- 前置：Reset；#1 保持 Preparing；记录 owner 和 queue。
- 操作：使用现有 MainCafe 控件切换 Pause/1x/2x，每档等待至少 3 秒；不点击 domain 推进按钮。
- 预期：订单不自动 Failed/Completed/requeue，owner 不丢失。
- 操作：恢复 1x，手动 Ready，再用 C1 取餐。
- 预期：正常完成。记录这是模拟继续，不是设备/路径恢复测试；debug 命令可用不代表正式游戏暂停权限已决定。

### P9-M-009 — Reset、关闭与 Play Mode 生命周期

- 前置：记录当前 Enter Play Mode Settings；先使用 Reload Domain 开启的配置。
- 操作：Edit Mode 创建 #1 → Reset，立即检查空列表、默认输入与空结果，再 Create 确认首单 ID=1；关闭重开，重复检查及首单创建。
- 操作：有订单时进入 Play，立即检查空列表、默认输入与空结果；在 Play Create 确认首单 ID=1；有订单时退出 Play，再立即检查同样三项并 Create 确认首单 ID=1。
- 预期：每个边界都实际清空，没有沿用上一 session 数据；退出后 Scene dirty 状态与测试前一致。
- 操作：在 `Edit > Project Settings > Editor > Enter Play Mode Settings` 启用选项并关闭 Reload Domain，重复上述序列两轮；完成后恢复原设置。
- 预期：无旧订单残留、双重回调、Console exception；实际菜单名称以本版本界面为准，由实施后的 Guide 核对。
- 恢复：确认设置已还原；若产生 ProjectSettings diff，单独检查本 case 改动，不覆盖既有用户设置。

### P9-M-010 — MainCafe 回归 smoke 与工具影响

- 前置：已保存的 MainCafe，在 Play Mode，记录已有 Console 警告；测试窗口关闭。
- 操作：检查 Camera 拖动/缩放、Pause/1x/2x；进入 Decor，打开 Catalogue，选一个已有家具进入 Preview 后 Cancel，再退出 Decor。
- 预期：现有功能可操作，无新增 Error/Exception；Cancel 保留原布局。
- 操作：打开 P9 window，Create/Claim/Reset/Close，再重复同一组操作；退出 Play。
- 预期：行为与开窗前一致；没有新增 Scene 对象/持久布局或 Scene dirty 变化。发现旧问题单独记录，不能冒称本阶段回归 PASS。

### Manual / GUI 结果记录

2026-09-28 Owner 已明确授权 Codex 使用 computer use 代跑 M-001…010。以下功能性用例记录 Codex 的实际执行结果；M-010 Camera 拖动另由 Owner 于 2026-09-29 人工确认。Codex 执行的部分不表示 Owner 本人亲测，GUI 验收通过也不自动关闭 P9。此授权更新本文原先要求 Owner 亲手操作的执行分工，测试步骤与预期保持不变。

| ID | 执行者 / 日期 / commit | 结果 | 实际现象与必要证据 |
|---|---|---|---|
| P9-M-001 | Codex / 2026-09-28 / c80f208 + 未提交 P9 实现 | PASS | 面板停靠后从 Reset 重新完整执行：#1 Waiting/C1/coffee、FIFO=[1]；E1 Claim 后 Claimed/E1、FIFO 空；Start→Preparing；Ready→ReadyForPickup。C2 Collection 返回 WrongCustomer，#1 仍 ReadyForPickup/C1/E1；C1 重试后 Completed，历史 claimant=E1、FIFO 空。代表截图：outputs/phase9/gui-2026-09-28/M01-wrong-customer.png、M01-completed.png。M-001…006 后 Console 仍为 0/0/0。 |
| P9-M-002 | Codex / 2026-09-28 / c80f208 + 未提交 P9 实现 | PASS | Reset 后 C1/C2/C3 创建 #1/#2/#3，FIFO=[1,2,3]；E1 Claim #1，重复 Claim 返回 EmployeeBusy 且 FIFO=[2,3] 不变；E2 Claim #2 后 FIFO=[3]；E1 将 #1 Start→Ready，再 Claim 得 #3。最终 #1 ReadyForPickup/E1、#2 Claimed/E2、#3 Claimed/E1、FIFO 空。截图：outputs/phase9/gui-2026-09-28/M02-fifo-123.png、M02-employee-busy.png、M02-final.png。 |
| P9-M-003 | Codex / 2026-09-28 / c80f208 + 未提交 P9 实现 | PASS | Reset→Create→E1 Claim 后直接 Ready 返回 InvalidTransition，保持 Claimed/E1；E2 Start 返回 NotClaimOwner，仍 Claimed；E1 Start 成功 Preparing；E2 Ready 返回 NotClaimOwner，仍 Preparing/E1；E1 Ready 成功 ReadyForPickup。领取后 FIFO 全程为空，失败操作未改状态或 claimant。截图：outputs/phase9/gui-2026-09-28/M03-invalid-transition.png、M03-wrong-start.png、M03-wrong-ready.png、M03-final.png。 |
| P9-M-004 | Codex / 2026-09-28 / c80f208 + 未提交 P9 实现 | PASS | Reset 后创建 C1/C2/C3 三单；对 #2 Fail(diagnostic.stop) 后 #2 Failed、无 claimant、FIFO=[1,3]；E1 Claim #1 后 FIFO=[3]；对 #1 Fail 后 Failed/E1/diagnostic.stop、FIFO 仍 [3]；E1 再 Claim 得 #3。最终 FIFO 空，#1/#2 的 Failed 历史、原 claimant 与原因均保留。截图：outputs/phase9/gui-2026-09-28/M04-waiting-failed.png、M04-final.png。 |
| P9-M-005 | Codex / 2026-09-28 / c80f208 + 未提交 P9 实现 | PASS | #1 完整正常流程后 Completed/C1/E1/reason 空；#2 C2 创建后 Fail(diagnostic.stop)。将 reason 改为 retry.reason，先对 #2/C2/E1，再对 #1/C1/E1，分别点击 Collection、Fail、Start，六次均为 OrderTerminal；原终态、claimant 和 reason 均不变，#2 仍保留 diagnostic.stop。E2 Claim 返回 NoWaitingOrders，FIFO 空。调整两组检查先后顺序以减少字段切换，覆盖不变。截图：outputs/phase9/gui-2026-09-28/M05-{failed,completed}-{collect,fail,start}.png（六张）、M05-no-waiting.png。 |
| P9-M-006 | Codex / 2026-09-28 / c80f208 + 未提交 P9 实现 | PASS | 空 Customer Create→InvalidCustomerId；C1/空 Product Create→InvalidProductId，均无单；恢复 coffee 后首单仍为 #1。Order=0/E1 Start→InvalidOrderId；999 Start→OrderNotFound；空 Employee Claim→InvalidEmployeeId；恢复 E1 Claim #1 成功；Order=1/空 reason Fail→InvalidFailureReason，#1 保持 Claimed/C1/coffee/E1、FIFO 空。截图：outputs/phase9/gui-2026-09-28/M06-invalid-{customer,product,order,employee,reason}.png、M06-first-valid-id.png、M06-order-not-found.png。 |
| P9-M-007 | Codex / 2026-09-28 / c80f208 + 未提交 P9 实现 | PASS | 两个独立 Reset 分支均创建 C1/C2/C3 三单，E1 将 #1 推进到 Ready 后领取 #2。分支 A 对 #1/C1 Collection 后 #1 Completed；分支 B 对 #1 Fail(diagnostic.stop) 后 #1 Failed。两分支再用 E1 Claim 均 EmployeeBusy，#2 保持 Claimed/E1，FIFO=[3]。截图：outputs/phase9/gui-2026-09-28/M07-old-completed-new-busy.png、M07-old-failed-new-busy.png。 |
| P9-M-008 | Codex / 2026-09-28 / c80f208 + 未提交 P9 实现 | PASS | #1 Preparing/C1/coffee/E1、FIFO 空；用 MainCafe 现有控件切换 Pause、1x、2x，分别实际等待 8.185s、6.861s、10.595s，各档订单、owner 和 FIFO 均不变；恢复 1x 后手动 Ready→ReadyForPickup、C1 Collection→Completed。退出 Play 后窗口立即显示空订单/空 FIFO、默认字段与 Last result=(none)。截图：outputs/phase9/gui-2026-09-28/M08-pause.png、M08-1x.png、M08-2x.png、M08-completed.png。这是模拟诊断继续，不是设备/路径恢复证据。 |
| P9-M-009 | Codex / 2026-09-28 / c80f208 + 未提交 P9 实现及 Reset 焦点修复 | PASS | 原 Reset 显示缺陷已真实复现并保留 FAIL 历史；修复后有焦点的 Customer C3→C1、Employee E2→E1、Order 999→1 均立即恢复，Create 首单 #1/C1，GUI 复测 PASS。Reload Domain 开启时完整执行 Edit Create→Reset→Create、Close/Reopen→Create、Enter Play→Create、Exit Play→Create；关闭时连续完整执行两轮。每个边界空订单/空 FIFO、默认字段、Last result=(none)，随后仅首单 #1 Waiting/C1/coffee、FIFO=[1]。实际 UI 已从 Reload Scene only 恢复为原来的 Reload Domain and Scene；最终 Console 0/0/0、Scene 无 dirty 星号。代表截图与设置证据见下方执行说明。 |
| P9-M-010 | 其他子项：Codex / 2026-09-28；Camera 拖动：Owner / 2026-09-29；c80f208 + 未提交 P9 实现及 Reset 焦点修复 | PASS | Codex 在 P9 工具使用前后两轮验证滚轮缩放、Pause→1x→2x→1x、Decor→Counter 1x1 Preview→Cancel→Done 均成功，ghost 消失且原单柜台保留；中间打开 P9 Create #1/C1/coffee→E1 Claim→Reset 空→Close。当时工具发送的 Camera 拖动未观察到位移；Owner 随后回应本 chat 的工具使用前后拖动对照请求，于 2026-09-29 回复“能移动的”，确认前后移动正常。2026-09-28 的前后 Console 0/0/0、恢复 1x、退出 Play 和 Scene 无 dirty 星号记录保留；本次没有新增截图或 Console 检查。详情见下方。 |

失败时记录最后一步、实际/预期状态、窗口或 Console 证据。只需代表性的截图与错误证据，不要求每步截图。

前次 computer-use 尝试：Windows 接口只返回主窗口控制对象，Debug 浮窗文字输入会切回主窗口，自动 docking 未成功；M-001 只完成正常路径，记 BLOCKED，其他用例未执行。该次结束时已退出 Play，Console Error/Exception 为 0，未改游戏代码、Scene、Packages 或 Enter Play Mode Settings；保留当时截图 M01-ready.png 作为历史记录。

本轮继续：Owner 将 P9 与 Console 停靠到主窗口后，Codex 从 Reset 重新执行各项。开始时 Console 为 0/0/0，证据 outputs/phase9/gui-2026-09-28/00-console-baseline.png。本轮开始前 MainCafe、EditorSettings、Packages 的 staged/unstaged diff 均为空；EditorSettings 的 Enter Play Mode options enabled=1/options=0。后续 Owner 将 Project Settings 停靠后，Codex 补完 M-009 的 Reload Domain 关闭两轮。2026-09-28 已完成 M-001…009 并检查环境；M-010 的拖动补核由 Owner 于 2026-09-29 完成，见下方记录。

阶段 Console 检查：M-001…006 完成后，Console 的 Log/Warning/Error 仍为 0/0/0，证据 outputs/phase9/gui-2026-09-28/M01-M06-console.png；这不替代剩余用例完成后的最终检查。

M-009 的缺陷与复测记录：

- 初始 FAIL：C3 输入保留焦点时 Reset 后仍显示 C3，而直接 Create 实际创建 C1；证据 M09-reset-stale-customer.png、M09-reset-display-data-mismatch.png。同类现象曾出现在 E2 字段，切换到其他字段后才显示默认 E1。
- 修复与 GREEN：`OrderDomainDebugWindow` 在 Reset 后安排下一次 OnGUI 绘制前清理本窗口输入焦点。GUI 实测 Customer C3、Employee E2、Order 999 三种焦点输入均立即恢复默认值；证据 M09-fix-customer-reset.png、M09-fix-customer-create.png、M09-fix-employee-reset.png、M09-fix-order-reset.png。
- Reload Domain 开启：实际 UI 原设置为 `Reload Domain and Scene`，证据 M09-original-reload-settings.png；Reset、关闭重开、进出 Play 的空状态与首单 ID=1 均通过。证据 M09-on-{reopened,play,exit}-{empty,first}.png（六张），操作过程中 Scene 无 dirty 星号。
- Reload Domain 关闭：此前因 Project Settings 浮窗下拉选择失焦而 BLOCKED；Owner 停靠设置页后，Codex 在实际 UI 选择 `Reload Scene only`，证据 M09-off-settings.png。连续两轮均完成 Edit Reset、关闭重开、进入 Play、退出 Play；每个边界均为空订单、空 FIFO、Last result=(none)，输入为 C1/coffee/E1/1/diagnostic.stop；每次随后 Create 均仅得到 #1 Waiting/C1/coffee、FIFO=[1]。证据 M09-off{1,2}-{initial-first,reset-empty,reset-first,reopened-empty,reopened-first,play-empty,play-first,exit-empty,exit-first}.png，以及各轮 play/exit-context.png。第二轮 initial-first 使用第一轮 exit-first 的同一次 Create 状态，两轮连续执行。
- 设置证据与恢复：两轮后再次在实际 UI 确认 `Reload Scene only`，证据 M09-off-settings-after-two-rounds.png；随后恢复原来的 `Reload Domain and Scene`，证据 M09-restored-reload-settings.png。关闭配置测试期间磁盘 EditorSettings 只读值仍为 enabled=1/options=0，因此 OFF 的证据来自实际 UI 选择及前后截图，不宣称磁盘曾变成 options=1。最终文件 hash 与 baseline 相同，详见下方环境检查。
- Console：修复后编译出现既有 `Phase7SurfaceAssetBuilder.cs:456` 的 CS0618（TMP_Text.enableWordWrapping 已弃用）。该文件 working tree 与 HEAD blob 均为 `bd8d0c91dd5f1f144dc831df93503fae0ddf6edb`，并非本次新增代码；进入 Play 时 Console 自动清空到 0/0/0，执行者未手动 Clear。本条保留已观察到的 warning；后续 M-010 与最终退出 Play 后 Console 仍为 0/0/0。

上述 M-009 截图均位于 `outputs/phase9/gui-2026-09-28/`。Reset 修复、Reload Domain 开启序列、关闭时连续两轮及设置恢复均有实际 GUI 证据，整项 M-009 为 PASS；保留初始缺陷与此前执行受阻的历史。

M-010 的执行与补核记录：

- P9 工具关闭时先跑基线：滚轮输入 -3 成功缩放；Pause→1x→2x→1x 均响应；Decor 中选择 Counter 1x1，出现 Preview 后 Cancel，再 Done 退出，原布局保留。
- 中间打开 P9，Create #1/C1/coffee→E1 Claim→Reset→Close；随后重复同组 smoke，滚轮输入 +3 成功缩放，时间按钮与 Preview/Cancel/Done 均正常。
- Camera 拖动的工具执行历史（2026-09-28）：基线尝试 `(450,500)→(520,535)` 及反向；点击 Game 空地聚焦后尝试 `(380,530)→(590,530)`；使用 P9 后重新聚焦再尝试 `(590,530)→(380,530)`，均未观察到位移，当时整项记 BLOCKED。现象在使用 P9 前已有，原因未确认；工具输入时序仅为可能解释，不能据此归因于 P9 或判定 Camera 代码故障。
- Owner 人工补核（2026-09-29）：针对本 chat 明确提出的 P9 工具使用前后相机拖动对照请求，Owner 回复“能移动的”，确认前后 Camera 均能正常移动。以本 chat 回复为证据，M-010 改记 PASS；不声称 Codex 成功拖动，不新增截图或 Console 检查，也不将此前工具未观察到位移的原因写成已确认。
- 代表证据：M10-{before,after}-{baseline,zoom,pause,1x,2x,preview,cancel}.png；M10-debug-claimed.png、M10-debug-reset.png；M10-before-pan-focused.png、M10-before-pan-no-displacement.png、M10-after-pan-no-displacement.png。全部位于 `outputs/phase9/gui-2026-09-28/`。

前一轮收尾（2026-09-28 23:29，America/Toronto）：已恢复 1x 并退出 Play，MainCafe 标题和 Hierarchy root 均无 dirty 星号，Console Log/Warning/Error 为 0/0/0；证据 M10-final-play-console.png、M10-final-edit-console.png。当时只读检查 tracked `Assets/`、`Packages/`、`ProjectSettings/` 的 staged/unstaged diff 均为空。

当时退出 Play 后仅重新打开 Project Settings 浮窗供后续停靠，显示原值 `Reload Domain and Scene`。之后 Owner 完成停靠，Codex 执行上述 M-009 OFF 两轮并恢复设置。

本次 M-009 补测收尾（2026-09-28 23:47，America/Toronto）：已退出 Play，P9 Reset 后关闭，Project Settings 保留为停靠 tab；实际 UI 恢复 `Reload Domain and Scene`。最终 MainCafe 标题和 Hierarchy root 均无 dirty 星号，Console Log/Warning/Error 为 0/0/0，证据 M09-final-edit-console.png。只读检查 tracked `Assets/`、`Packages/`、`ProjectSettings/` 的 staged/unstaged diff 均为空；新增 P9 文件仍未提交。HEAD 仍为 `c80f2088fbff65e5a17b96206c1f32f6560719ce`，branch 仍为 `codex/phase-9-order-domain`，无 commit。

以下 SHA256 在 22:40 baseline、23:29 收尾与 23:47 补测收尾完全一致；最终磁盘 Enter Play Mode 设置为 enabled=1/options=0，与恢复后的实际 UI 相符。

| 文件 | SHA256（前后相同） |
|---|---|
| Assets/Scenes/MainCafe.unity | E79248E00173F673E90416B8EFBDD40E95CE92CE15F5BC16F40324EBC7EA81F3 |
| ProjectSettings/EditorSettings.asset | 20E7399984B858EF7485C4E58F26A42345169749CF0D3A63EFAAA82B33F8A883 |
| Packages/manifest.json | FAD171F7061872A01804E6BD5470DF11E305D785F27AB054DBFD36D0331DE063 |
| Packages/packages-lock.json | 54158277D1C10FDA186B89F4179950287E1666CB631655B8395436C0A488D66D |

## 8. 最终门槛与明确未覆盖内容

- 必需 E/D/P cases 全部有可复现 automated evidence；R reviews 无未解决 Critical/Important。
- M-001…010 由 Owner 或本次已授权的 Codex 实际完成并记录；BLOCKED/SKIP 必须单独披露，不能批量改为 PASS。
- 按 test fullname 汇总 XML、记录运行 checkout/commit/未提交修改；保留必要 log，不提交 outputs。
- 此处没有真实 NPC collection、真实员工制作/路径恢复、付款去重、capacity、Save、Android/iOS 设备或玩家正式 UI 的测试。这些属于后续阶段，不能由本阶段模拟结果替代。

## 9. 分项执行证据

### 2026-09-28 历史自动测试与 GUI 记录

| 范围 | 新证据 | 当前结果 |
|---|---|---|
| E-001…012 / Task 1 | `outputs/phase9/task1-red.xml`：31 total，1 PASS、30 预期 FAIL；`task1-green.xml`：31 PASS、0 FAIL/SKIP | PASS；独立 task review 无 findings |
| E-013…017 / Task 2 | `outputs/phase9/task2-red.xml`：11 total，1 PASS、10 预期 FAIL；`task2-green.xml`：42 PASS，包含 Task 1 回归，0 FAIL/SKIP | PASS；独立 task review 无 findings |
| E-018…040 / Task 3 | `task3-fix1-green.xml`：126 PASS、0 FAIL/SKIP，包含 Task 1/2；补强 busy/next-ID probe 后定点复审通过 | PASS；独立 SPEC/QUALITY PASS，初始 RED 限制见下 |
| D-001…006 / Task 4 | `task4-edit-green2.xml`：132 PASS，其中 126 domain + 6 Editor，0 FAIL/SKIP | 自动 PASS；GUI 已补充正常/错误路径与 Reset 修复证据，实际执行见 M 结果 |
| P-001…003 / Task 4 | `task4-play-green2.xml`：3 PASS，0 FAIL/SKIP | 自动 PASS |
| R-001 / Task 5 | `full-edit-final.xml`：1900 PASS/355 SKIP；三组隔离补跑 160+194+1 PASS，exact fullname 合并 2255/2255；`full-play.xml`：1039 PASS、0 FAIL、4 个既有 opt-in SKIP | PASS；原 SKIP 保留，必需自动行为全部有证据 |
| R-002 / Engineering | 最终全分支代码 review：0 新 Critical/Important/Minor；runtime/Editor 分离与资源边界符合设计 | 代码 PASS；GUI 验收证据另见 M 结果 |
| R-003 / QA | 49 个计划自动 case ID 对应 135 个 P9 fullname；核对完整 XML 与 source/生命周期/禁止写入项 | 自动与代码证据审核 PASS；M 的真实 GUI 证据单独记录 |
| M-001…010 | M-001…009 与 M-010 其他子项：Codex / 2026-09-28；M-010 Camera 拖动：Owner / 2026-09-29 本 chat 回复；详见第 7 节 | M-001…010 PASS；GUI 验收完成；2026-09-28 的 Console/Scene/设置/文件差异收尾检查记录保留 |

以上自动测试为 Reset 焦点修复之前的未提交 P9 版本证据；焦点修复已有实际 GUI GREEN，不能将之前的完整 regression 表述为修复后重新全量执行。M-001…010 的 GUI 验收已完成；环境收尾检查日期为 2026-09-28，Owner 的 Camera 补核日期为 2026-09-29。最新 focused 与最终 full regression 状态见下方，阶段 closeout 与 Roadmap 更新尚未执行。Task 1 的 XML 中完整 test names 保留，case ID 也编码在测试名称中。

### 2026-09-29 推送前验证（包含 Reset 焦点修复）

本节为 `codex/phase-9-order-domain` 的推送前验证。Owner 已明确授权最后 review 及提交、推送该分支；远端提交由交付时核验，main merge 与阶段 closeout 单独处理。最终 Engineering/QA review 已完成，无新增代码或覆盖问题；文档中的旧 GUI 状态已同步。

| 范围 | 证据 | 结果 |
|---|---|---|
| P9 focused EditMode | `outputs/phase9/pre-push-2026-09-29-edit.xml` | 132/132 PASS，0 FAIL/SKIP |
| P9 focused PlayMode | `outputs/phase9/pre-push-2026-09-29-play.xml` | 3/3 PASS，0 FAIL/SKIP |
| 最终全量 EditMode | `outputs/phase9/pre-push-2026-09-29-full-edit.xml`；本次三个 `pre-push-2026-09-29-isolated-*.xml`；`pre-push-2026-09-29-edit-verification.json` | 原运行 1900 PASS / 355 SKIP / 0 FAIL；隔离补跑 160+194+1 PASS，合并 2255 个唯一 PASS |
| 最终全量 PlayMode | `outputs/phase9/pre-push-2026-09-29-full-play.xml` / `.log` | 1043 total / 1039 PASS / 0 FAIL / 4 个既有可选 SKIP |

本次 EditMode 的 355 个 SKIP 与 2026-09-28 的 fullname 和 reason 完全一致，均为 dirty Scene 保护。新进程 `pre-push-2026-09-29-isolated-maincafe.xml` 160 PASS、`pre-push-2026-09-29-isolated-validator.xml` 194 PASS、`pre-push-2026-09-29-isolated-scene.xml` 1 PASS，均 0 FAIL/SKIP；按 Ordinal exact fullname 核对，隔离补跑集合恰好等于本次 355 SKIP，合并覆盖 2255 个唯一测试，Missing/Extra 均为空。原始 SKIP 记录保留。

本次 PlayMode 的四个 SKIP 与历史报告的 fullname 和 reason 逐项完全一致：P8RCashRegisterSideIndicatorTests 原生截图、P8RCompactChromeTests spacing audit、P8RReadinessSafeAreaTests 原生截图、P8RUiEnhancementSceneTests 原生截图。均为既有 opt-in 检查；P9 三项 PlayMode 全部 PASS，原始 SKIP 不改写。

本次最终 full regression 已按项目流程 §6.9 完成，必需功能均有通过证据，最终回归 gate 可勾选。汇总 `outputs/phase9/pre-push-2026-09-29-verification.json`：7 份报告均无 FAIL；EditMode 2255 唯一 PASS；32 项 baseline 文件 hash 未变；tracked Assets/Packages/ProjectSettings 的 staged/unstaged diff 均为空；UnityProcesses=0。测试生成资源差异已备份后恢复。历史全量结果保持原日期与版本边界，阶段 closeout 与 main merge 单独处理。

Task 3 的独立初始行为 RED 只记录了 StartPreparation（1 FAIL）和 Fail（3 FAIL）。MarkReadyForPickup 与 Complete 的后续断言被前序 stub 失败挡住，没有各自独立的初始 RED；最终 GREEN 覆盖四操作，但不能补写为完整的逐操作 TDD 历史。




### 2026-09-28 全量回归说明（历史）

- 首轮完整 EditMode 在 1200 秒中止，没有 XML，不能计为 PASS/FAIL；按历史运行时长把上限改为 3600 秒后完成。保留 `full-edit-timeout.log`。
- 原始 355 个 EditMode SKIP 来自前序测试留下的 dirty Scene guard。新进程 `edit-isolated-maincafe.xml` 160 PASS、`edit-isolated-validator.xml` 194 PASS、`edit-isolated-scene.xml` 1 PASS；与原清单按 exact fullname 合并 2255 唯一 PASS，无缺漏/额外。汇总 `outputs/phase9/final-verification.json`。
- PlayMode 4 SKIP：P8RCashRegisterSideIndicatorTests 原生截图、P8RCompactChromeTests spacing audit、P8RReadinessSafeAreaTests 原生截图、P8RUiEnhancementSceneTests 原生截图。XML 的 opt-in reason 与源码 guard 一致；P9 Play 无跳过。
- Task 4 原先缺少的真实 GUI 正常/错误路径已由本轮 M-001 操作与代表截图补充。M-009 发现的 Reset 焦点显示缺陷已修复并实际 GUI 复测通过；既有 D-003 直接调用属性与 Reset，没有覆盖真实 IMGUI 焦点显示。M-009 已补完关闭 Reload Domain 的两轮；2026-09-28 的 Console、设置、Scene 与文件差异检查已完成。M-010 Camera 拖动由 Owner 于 2026-09-29 人工确认，GUI 验收完成；阶段 closeout 尚未执行。
- 测试发生于本地 `codex/phase-9-order-domain` / 基线 `c80f208` 加未提交 P9 文件；测试后 hash 一致，tracked Assets 恢复到运行前状态。文档更新不触发无关 Unity 重跑。
