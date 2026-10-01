# Phase 9 — Order Domain Design

> 状态：Approved Phase 9 Baseline — 实现、GUI 验收、最终 review 与修复后的完整回归已完成。2026-09-29 focused 132+3 PASS、最终 EditMode 2255 个唯一 PASS、全量 PlayMode 1039 PASS / 0 FAIL / 4 个既有可选 SKIP。本记录为推送前验证，交付分支为 `codex/phase-9-order-domain`；Owner 授权已记录，远端提交由交付时核验。main merge 与阶段 closeout 未执行。证据见配套 plan/test cases。
> 日期：2026-09-28
> 已确认的游戏规则：正确顾客实际取走商品后，订单才进入 `Completed`。

## 1. 这个 Phase 做什么

P9 建立咖啡厅的订单登记与处理规则。它像一张可靠的订单簿：知道谁点了什么、轮到哪一单、由谁负责、有没有交到正确客人手里。

例如：顾客 C1、C2 依次点单，员工 E1 先领 #1，E2 再领 #2。#1 做好并送到 Pick-up 后仍等待取餐；只有 C1 取走才完成。E2 抢领 #1、C2 取走 #1、重复完成 #1 都不能破坏订单。

P9 用纯 C# 和一个独立 Editor 测试窗口模拟这些操作。实际人物、制作动作和经营画面由后续 Phase 接入。

### 依据与批准边界

- 游戏规则：`Docs/AnimalCafe_Project_Design.md` 第 3.1–3.3 节。
- 阶段范围：`Docs/AnimalCafe_Development_Roadmap.md` Phase 9；P10–14 提供后续整合边界。
- 开发与验收：`Docs/AnimalCafe_Phase_Development_Process.md`。
- 本文接口、技术默认值和诊断窗口已作为 P9 开发基线获批；本文描述目标合同，实际实现/验收状态由配套 plan/test cases 记录。
- 配套：`2026-09-28-phase-9-order-domain-test-cases.md`；plan 位于 `../plans/2026-09-28-phase-9-order-domain.md`。

## 2. 范围、依赖与交付

### Included

- 正整数、递增且在一个 service 生命周期内唯一的 Order ID。
- 订单资料、六个状态、唯一 `OrderService` 写入入口。
- FIFO 等待队列；领取者权限；重复和非法操作保护。
- 明确的成功/失败结果；查询只返回不可修改的 snapshot。
- EditMode domain tests、Editor 工具 tests、少量 PlayMode 边界 tests。
- 供 Owner 手工操作的临时 Editor debug window；Beginner Guide 与执行结果记录。

### Not Included

- Customer/NPC、Employee task execution、NavMesh、真实制作/配送/取餐。
- Capacity、reservation、付款/退款、库存、Recipe/Menu 查询、经济、Save/Load。
- 自动超时、取消规则、重新排队、员工转交、玩家插队或指定员工。
- 正式订单 UI、MainCafe scene wiring、全局 singleton、event bus 接线和后台线程。
- 一单多商品：P9 每次 Create 记录一个 productId，作为后续基础 Coffee loop 的接口；不增加数量、定价或配方逻辑。

### 为什么现在做

P8 已提供家具与 layout-readiness；P9 先稳定订单规则，P10–13 分别解决容量、移动、顾客和员工，P14 再组合为外带循环。P9 不读取 Scene 或家具，测试可独立重现。

### 交付后的可见结果

Owner 能在 Unity 的专用测试窗口逐步观察订单和操作结果。MainCafe 的玩法不增加顾客或营业循环。本阶段手工结果证明 debug 操作与 domain 状态可理解，不能证明真实顾客服务、实体手机或盈利/经济表现。

## 3. 数据与所有权

Runtime namespace：`AnimalCafe.Orders`，沿用 `AnimalCafe.Runtime` assembly。

| 数据 | 规则 |
|---|---|
| OrderId | `long`，有效值 1 到 `long.MaxValue`；默认第一单为 1 |
| CustomerId / ProductId | 非空字符串，无首尾空白；区分大小写，按 ordinal 比较；不自动修正 ID |
| ClaimantId | 未领取为 null；领取后记录 employeeId，终态仍保留用于查询 |
| State | `Waiting / Claimed / Preparing / ReadyForPickup / Completed / Failed` |
| TerminationReason | 仅 Failed 有非空原因；其他状态为 null |

- `OrderSnapshot` 为 sealed、get-only 的不可变对象。旧 snapshot 不会随新操作变化。
- `GetOrders()` 按 ID 递增返回 snapshot 的独立只读集合；`GetWaitingOrderIds()` 返回严格按 FIFO 排列的独立只读集合。
- service 私有持有所有订单和 waiting IDs；使用简单 Dictionary 与 List 即可，不引入通用 repository、锁或事件框架。
- 所有写入为主线程同步调用；“抢单”测试验证两个先后到达的请求，不宣称支持多线程并发。
- Completed/Failed 在当前 session 保留用于查询；P9 不做长期历史清理或持久化。
- service 之间数据隔离，ID 可各自从 1 开始；ID 不是跨 session 全局身份。将来 Save 必须单独设计恢复与命名空间。

## 4. 状态与权限

| 操作 | 来源状态 | 成功后的状态 | 调用条件 |
|---|---|---|---|
| Create | 尚无订单 | Waiting | customerId、productId 合法，ID 未耗尽 |
| ClaimNext | 队首 Waiting | Claimed | employeeId 合法，且该员工没有 Claimed/Preparing 订单 |
| StartPreparation | Claimed | Preparing | employeeId 等于本单 ClaimantId |
| MarkReadyForPickup | Preparing | ReadyForPickup | 同一 ClaimantId；调用者确认商品已制作并送达 Pick-up |
| Complete | ReadyForPickup | Completed | customerId 等于本单 CustomerId；调用者确认顾客实际取走 |
| Fail | 任意非终态 | Failed | 上层明确终止并提供非空原因 |

除表中规则外，所有转换都拒绝。不存在任意 `SetState` 或公开通用 `Transition`，以免绕过权限。

### 4.1 制作、送达与取餐

- `Preparing` 覆盖开始制作到成功送达之前的阶段。商品已做好但尚未送达仍保持 Preparing。
- `ReadyForPickup` 表示已送达、等待对应顾客领取。
- P13/P14 的 task 系统负责实际动作及成功证据；P9 验证调用者身份与合法状态，不检查人物位置。
- `Complete` 只允许正确顾客身份；一旦成功，不需等顾客离店。

### 4.2 员工与 FIFO

- 员工在 Claimed/Preparing 时为 busy；再次 ClaimNext 返回 `EmployeeBusy`，队列不变。
- 进入 ReadyForPickup、Completed 或 Failed 后不占用该员工的领取额度；ClaimantId 仍保留为历史资料。
- Ready 的旧单不会因同一员工领取新单而改变 owner 或状态。
- 旧 Ready 订单随后 Complete/Fail，不得释放同一员工正在处理的新订单；busy 由当前 Claimed/Preparing 订单决定。
- FIFO 约束领取顺序，不约束完成顺序；订单 #2 可以先于 #1 被制作并交付。
- 同一 customerId 可以创建多张独立订单；不在 P9 强加每客一单限制。

### 4.3 失败、临时中断与重复操作

- 设备或路径暂时受阻时，上层暂停动作，不调用 Fail；P9 不计时、不自动前进、不自动释放 owner。解除阻碍后可继续合法操作。
- `Fail` 是受信任上层流程的显式终止入口，不需要 claimant；不是玩家取消按钮，也不实现退款规则。
- reason 为 null/空白时拒绝；有效 reason 保留原文。Waiting 被 Fail 后立即移出 waiting queue；其他非终态被 Fail 后释放 busy 状态。
- Completed/Failed 为 terminal，不能恢复、重新领取、再次完成或覆盖 termination reason。
- 重复 StartPreparation/MarkReadyForPickup 返回 InvalidTransition；重复 Complete/Fail 返回 OrderTerminal；失败操作没有副作用。
- `Create` 的每次合法调用都表示一张新订单；`ClaimNext` 的每次合法调用都表示一次新领取请求。P9 不提供网络 requestId 去重，也不承诺延迟请求的 exactly-once。上层必须只在相应业务事件发生时调用。

## 5. 接口与确定性错误

```csharp
public sealed class OrderService
{
    public OrderService(long firstOrderId = 1);
    public OrderResult Create(string customerId, string productId);
    public OrderResult ClaimNext(string employeeId);
    public OrderResult StartPreparation(long orderId, string employeeId);
    public OrderResult MarkReadyForPickup(long orderId, string employeeId);
    public OrderResult Complete(long orderId, string customerId);
    public OrderResult Fail(long orderId, string reason);
    public bool TryGetOrder(long orderId, out OrderSnapshot order);
    public IReadOnlyList<OrderSnapshot> GetOrders();
    public IReadOnlyList<long> GetWaitingOrderIds();
}
```

`OrderSnapshot` 属性：`long OrderId`、`string CustomerId`、`string ProductId`、`string ClaimantId`、`OrderState State`、`string TerminationReason`。

`OrderResult` 属性：`bool Succeeded`、`OrderOperationFailureReason FailureReason`、`OrderSnapshot Order`。成功为 `true / None / 当前 snapshot`；失败为 `false / 具体原因 / null`。读取失败后的当前订单应重新查询。

`OrderOperationFailureReason`：`None, InvalidCustomerId, InvalidProductId, InvalidEmployeeId, InvalidOrderId, OrderNotFound, NoWaitingOrders, EmployeeBusy, NotClaimOwner, WrongCustomer, InvalidTransition, OrderTerminal, InvalidFailureReason, IdExhausted`。

### 校验顺序

1. Create：customerId → productId → ID 耗尽。
2. ClaimNext：employeeId → EmployeeBusy → NoWaitingOrders。
3. 按 ID 操作：orderId 必须 > 0 → 其余参数格式 → OrderNotFound → OrderTerminal → 来源状态 → owner/customer 是否匹配 → 执行变更。
4. TryGetOrder：非法或不存在 ID 返回 false，out 为 null，不抛异常。

所有返回失败的操作必须同时保留订单资料、列表数量/顺序、waiting queue、busy 判定和下一个 ID。合法构造时 `firstOrderId` 必须 > 0，否则抛 `ArgumentOutOfRangeException`；构造参数用于确定性 ID 起点及上限测试，不代表 Save 恢复。

ID 上限：允许创建 ID=`long.MaxValue` 一次，随后 Create 永远返回 IdExhausted；不 wrap 到负数、不复用终态 ID。已存在订单仍可继续处理。

## 6. Manual 验收工具

在 `Window > AnimalCafe > Phase 9 Order Debug` 打开 EditorWindow。使用现有 Editor assembly，无需新增 Scene、Prefab、package 或 runtime UI。

- 明确显示“P9 Debug / 临时模拟数据；不是真实营业”。
- 字段：Customer ID、Product ID、Employee ID、Order ID、Failure reason。
- 按钮：Create、Claim Next、Start Preparation、Mark Ready For Pickup、Simulate Customer Collection、Fail、Reset Session。
- 操作可保留无效输入并执行，让 Owner 实际检查错误；Order ID 使用可编辑 long 字段，Claim 成功后不自动覆盖它，以便重试原订单。
- 显示所有订单、waiting FIFO、最后一次操作的成功/错误原因；订单表始终重新读取 service。只保留最后结果，避免日志窗口功能扩张。
- `OrderDebugSession` 为 internal、纯 C# Editor adapter，暴露本窗口的 Service，并以 `Reset()` 替换为新的 service。EditorWindow 不重复实现 domain 规则。
- 新开窗口、Reset、进入 Play 前、退出 Play 时均清空临时订单、输入与结果。默认输入 C1 / coffee / E1 / 1 / diagnostic.stop。关闭后不持久化；不依赖 Domain Reload 开关。
- OnEnable 注册 playModeStateChanged；OnDisable 解除订阅并释放 session；Play Mode 切换时按 `ExitingEditMode / ExitingPlayMode` Reset。
- 开关/重置窗口不得保存或弄脏 Scene/Prefab、创建 GameObject、写 PlayerPrefs/Save 或改变 Time.timeScale。
- Debug 按钮在 Pause/1x/2x 下均可执行：它们是诊断命令，不能据此决定正式游戏 Pause 管理权限。

## 7. 文件边界与影响

| 路径 | 用途 |
|---|---|
| `Assets/Scripts/Orders/OrderState.cs` | 六个状态 |
| `Assets/Scripts/Orders/OrderSnapshot.cs` | 不可变查询数据 |
| `Assets/Scripts/Orders/OrderOperationFailureReason.cs` | 可测试的错误码 |
| `Assets/Scripts/Orders/OrderResult.cs` | 统一操作结果 |
| `Assets/Scripts/Orders/OrderService.cs` | 唯一写入入口与 FIFO |
| `Assets/Editor/Phase9/OrderDebugSession.cs` | 临时测试数据的生命周期 |
| `Assets/Editor/Phase9/OrderDomainDebugWindow.cs` | Owner 手工操作窗口 |
| `Assets/Tests/EditMode/Phase9/` | domain 与 Editor 工具 tests |
| `Assets/Tests/PlayMode/Phase9/OrderDomainPlayModeTests.cs` | 帧与时间边界 tests |
| `Docs/Phase9_Beginner_Guide.md` | 实施时加入操作步骤、实际界面和验收记录 |

新 Assets 文件/文件夹附 Unity 生成的 `.meta`。不改现有 runtime service、Scene、Prefab 或 asmdef；不需要 virtual environment、Python dependency 或 package 安装。Editor 工具自动测试在现有 EditMode assembly 中直接访问 Editor 类型；真实 Play 切换与 Domain Reload 组合由 manual cases 覆盖，避免为测试窗口增加 runtime/Editor assembly 耦合。

本阶段没有既有 Order 数据要迁移；不会修改已完成装修功能。风险集中在状态权限、FIFO、ID 上限、snapshot 可变性及 debug 窗口生命周期。P9 参数与内部状态均须按配套 cases 测试。

## 8. 验收与后续

- 每个 Task：可信 RED → 最小实现 → focused GREEN → 直接相关 regression。
- Phase 收尾：完整 EditMode / PlayMode，Engineering 和 QA review，Owner manual cases；测试修复后补足相关与最终 regression。
- 本文、test cases、implementation plan 先审核；批准后才建立/确认隔离开发环境并实施。
- 手工窗口按实际 GUI 操作验收。Owner 已于 2026-09-28 授权 Codex 代跑功能性用例，M-010 Camera 拖动由 Owner 于 2026-09-29 人工补核；分别记录执行者和证据。自动 tests 不能代替真实 GUI 操作；未运行记 NOT_RUN，受环境阻挡记 BLOCKED。
- 只记录当前证据；P8R 历史 PASS 不能当作 P9 PASS。P9 完成不等于 P14 经营闭环或真机验收。
- 所有 deferred 内容保持后续阶段边界；发现需要 Prepared、retry/reassignment 或 Save 时，先评估接口影响并回到设计确认。
