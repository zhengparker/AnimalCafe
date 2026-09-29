# Phase 10 — Capacity & Reservation Design

> 状态：Approved P10 Baseline。Owner 于2026-09-29批准本文、tests和implementation plan，并授权直接在现有P9分支开发，可使用sub-agents。实施状态与证据见plan/test cases。
> 日期：2026-09-29（America/Toronto）。
> 基线：`codex/phase-9-order-domain`，`94cc48d7d8c6b43261b96e73bf2d2b6541fafbf4`。

## 1. 目标、批准与交接

P10 建立三本可靠的名额账：店内顾客总数、柜台队伍和取餐等候队伍。顾客入店前先取得名额，实际抵达时转为占用，离开对应位置后归还。失败操作不能留下部分预留，重复释放不能减成负数。

Owner 已批准下列游戏规则，取代早期“固定 8 / 3 / 4”和“离开 Counter 才申请 Pick-up”的讨论方案：

| 规则 | 已确认内容 |
|---|---|
| Floor 来源 | 已铺设的室内 Floor 格数；不扣家具占用、入口净空或暂时不可走的格数 |
| Total | `floor(FloorCellCount / 4)` |
| Pick-up | 与 Total 相同；是顾客等候额度，不是可摆放杯子的数量 |
| Counter | `ceil(Total * 50 / 100)`；包括正在点单、排队与已预留的名额 |
| 入店预留 | 入店前同时取得 Total、Counter、Pick-up 各一个名额；全部成功或全部不变 |
| Counter 释放 | 点单结束且顾客实际离开柜台位置后 |
| Pick-up 释放 | 正确取走商品且顾客实际离开取餐位置后 |
| Total 释放 | 顾客实际离店后 |
| 缩容 | 保留现有顾客与全部 reservation，允许暂时超出新上限；暂停新申请，正常离开后恢复 |
| 到店节奏 | 新客按 random interval 到来；空位仅决定是否允许进入，不一次补满。实际 scheduler 属于 P12 |

例如 20 / 32 / 48 / 64 格分别得到 `(Total, Counter, PickUp) = (5,3,5) / (8,4,8) / (12,6,12) / (16,8,16)`。0–3 格均为 0/0/0，不能入店。4 格为 1/1/1。

P9 的 `ReadyForPickup → Completed` 仍由正确顾客实际取走触发，不等待离开位置或离店。P10 不修改这个合同。同一顾客可有多个 P9 Order；容量 owner 表示一次 visit，不是每张订单。

Owner 明确要求直接基于 P9 分支继续 P10，不等待 main merge。准备时 P9 HEAD 为上述提交、tracked diff 为空；本地 main/origin/main 仍为 `c80f2088fbff65e5a17b96206c1f32f6560719ce`（不代表本轮刷新远端）。P9 功能/必要验收已完成，但 merge、正式 closeout、Roadmap Completed 尚未完成。P10 文档不改变这些历史状态，也不继承 P9 的 commit/push 授权。

## 2. 范围、可见结果与依赖

**Included：** 纯 C# 容量计算、动态面积更新、原子批量预留、token/owner 校验、占用与幂等释放、不可变查询；一个只读 Floor 来源 adapter；临时 Editor debug window；automatic/manual cases。

**Not Included：** 真实 NPC、站位分配、队伍方向/FIFO移动、NavMesh、生成计时器、员工任务、杯子摆放槽位、材料库存、订单与容量跨 service transaction、自动超时/退款/取消、Save、正式 UI、Scene wiring。

- P11：导航与移动恢复；可以使用明确标注的测试替身，正式员工 model 不是 P10 前置条件。
- P12：顾客生成、random interval、实际队列与可达站位、容量调用时机。计时恢复策略/具体 interval 分布留在其设计阶段，不能因解除阻塞补发一批顾客。
- P13/P14：员工任务与完整外带循环，负责确认实际动作成功，再调用 Order / Capacity API。
- P20：材料库存 reservation，与 P10 顾客名额分开。
- P28：Store Expansion，购买/解锁区域及实际 Floor 扩大；P10 不提前实现扩建。

交付后 Owner 可在 Unity 独立窗口模拟名额变化。MainCafe 不会因此真实营业。数字容量通过，不代表等候队伍已经放得下或可达；P12 必须验证空间，不能叠加一个不计入 Pick-up 的隐藏等待队伍。

## 3. Floor 面积的现有代码边界

基线已核对：`CafeLayoutRuntime.Initialize()` 创建 8×8 的 `region.main`、Interior 类型和同范围 LayoutBounds。`RoomSurfaceLayout` 强制 8×8 完整 Floor 外观资料，`FloorSurfaceGridView` 也会画 Preview；它们都不是动态面积来源。

本阶段以 `CafeLayout` 中已发布的 Interior 地板区域作为现有“已铺设 Floor”的数据映射：读取 `UnlockedRegions`，只累计 Interior 覆盖的不同 GridPosition，并用 `IsInsideUnlockedRegion(position)` 裁剪有效 bounds。不计 Exterior，不重复累计重叠区域，不按包围矩形填补洞或空隙，不从 Renderer、Camera、外观条目或家具数量推算。

当前模型没有“区域已解锁但尚未铺 Floor”的独立状态。这里的映射仅适用于当前已铺地板的室内区域；P28 若引入解锁/铺设分离，须更新来源 adapter，不能直接把未铺格计入。Exterior 功能留到其 Phase 决定，不增加室内容量。

新增 `FloorCapacitySource.CountInteriorCells(CafeLayout layout)`，纯只读、返回 int。null 抛 ArgumentNullException；region 坐标范围或最终格数无法以 int 表达时抛 OverflowException，不能静默回绕。坐标终点计算用 long。当前小型布局用 HashSet 去重即可，不构建通用面积计算框架。

CapacityService 只接收已确认的格数，不订阅 Scene、不保存 Layout 引用。调用流程是 `CountInteriorCells(confirmedLayout) → UpdateFloorCellCount(count)`。P10 用 domain fixtures 验证区域变化后的显式刷新；MainCafe 自动接线由 P12/P14 实施。装修 Preview/Cancel、贴图更换和家具移动都不发布新的面积。当前基线来源应得到 64 格，即 16/8/16；本轮未运行 Scene 验证。

## 4. 数据、token 与不变量

Runtime namespace：`AnimalCafe.Capacity`，沿用 `AnimalCafe.Runtime` assembly。

| 类型 | 合同 |
|---|---|
| CapacityKind | `TotalCustomers=0, CounterQueue=1, PickUp=2`；顺序也用于结果排序与错误选择 |
| ReservationState | `Reserved, Occupied, Released` |
| CapacityRules | immutable；`CellsPerCustomer` 默认 4，必须 >0；`CounterPercent` 默认 50，范围 1–100；session 内固定 |
| CapacityLimits | immutable；`FloorCellCount, TotalCustomers, CounterQueue, PickUp`，全部 int |
| CapacityToken | sealed immutable；公开 `long Id`，无 public constructor；绑定签发 service 与精确 token 实例，不靠裸 ID 授权 |
| ReservationSnapshot | get-only `Token, OwnerId, Kind, State`；旧 snapshot 不随状态更新 |
| CapacitySnapshot | get-only `Kind, Limit, Reserved, Occupied, Used, Available, OverCapacity` |
| CapacityResult | `Succeeded, FailureReason, Reservations`；成功返回涉及的 reservation snapshots，更新面积返回空集合；失败返回空集合 |

ownerId 是一次 visit 的稳定 ID，由调用层生成；null/空白/首尾有空白拒绝，ordinal 区分大小写。不允许同一 owner 同时持有同一种容量的多个 active token，但可以分多次申请不同种类。Release 不删除历史，session 内保留 Released，确保重试能返回幂等成功；没有长期历史存储或清理器。

每项满足 `Used = Reserved + Occupied >= 0`，`Available = max(0, Limit - Used)`，`OverCapacity = max(0, Used - Limit)`。所有计数来自私有、受 service 管理的记录；不可把只读集合底层可变对象暴露给调用者。

**缩容是明确例外：Used 可以大于新 Limit。** 旧 Roadmap 的“不能超过 max”在 P10 具体化为“新 Reserve 不得造成或扩大超额”；不能为满足旧断言删除现有 token。只要任一项 OverCapacity >0，所有新 Reserve 拒绝。没有超额时，再检查本次申请各项 Available；正式入店始终申请三项。

Reserved→Occupied 只换分类，Used 不变；即使缩容后超额也允许已有合法 token Occupy。Release 从 Reserved 或 Occupied 各减对应计数一次；同 owner 对 Released 再 Release 为成功，无变化。错误 owner 即使碰到 Released 也不能成功。Occupy 重复调用或对 Released 调用均拒绝。

token Id 在 service 内递增且不复用，默认起点 1；批量预留按 Total / Counter / PickUp 分配。其他 service 即使同 Id 也无效；Reset 创建新 service，旧 token 无权访问它。批量分配若剩余正 long ID 数量不足，整体失败、不消耗任何 ID；允许用到 long.MaxValue 一次，随后耗尽不回绕。

## 5. 技术接口与原子边界

```csharp
public sealed class CapacityRules
{
    public CapacityRules(int cellsPerCustomer = 4, int counterPercent = 50);
    public int CellsPerCustomer { get; }
    public int CounterPercent { get; }
    public CapacityLimits CalculateLimits(int floorCellCount);
}

public static class FloorCapacitySource
{
    public static int CountInteriorCells(AnimalCafe.Layout.CafeLayout layout);
}

public sealed class CapacityService
{
    public CapacityService(int floorCellCount, CapacityRules rules = null,
        long firstTokenId = 1); // rules=null 使用默认值
    public CapacityLimits Limits { get; }
    public bool CanAdmit { get; } // 仅容量观察；不是 spawn 指令或已获得名额
    public CapacityResult UpdateFloorCellCount(int floorCellCount);
    public CapacityResult TryReserveAdmission(string ownerId);
    public CapacityResult TryReserve(string ownerId, IReadOnlyList<CapacityKind> kinds);
    public CapacityResult Occupy(CapacityToken token, string ownerId);
    public CapacityResult Release(CapacityToken token, string ownerId);
    public IReadOnlyList<CapacitySnapshot> GetCapacities();
    public IReadOnlyList<ReservationSnapshot> GetReservations();
}
```

`CalculateLimits` 用整数计算：Total=F/CellsPerCustomer；Counter=((long)Total*CounterPercent+99)/100；PickUp=Total。负 F 抛 ArgumentOutOfRangeException。构造负面积、非正 firstTokenId 或无效 rules 构造参数也抛此异常；已有 service 的 UpdateFloorCellCount 负数通过 result 返回失败，保留全部状态。

`TryReserveAdmission` 只委托同一批量实现，固定申请三项各 1。`TryReserve` 支持 1–3 个不同种类，各 1；不提供任意数量或资源框架。先复制输入并验证完整 batch，再检查，最后一次发布所有新记录。输入顺序不改变 ID/结果顺序；空集合、重复种类、未定义 enum 全部拒绝。失败后计数、历史、下一 ID 都不变。

`CanAdmit` 为三项各 Available>=1 且无任何超额；它不检查 owner/ID耗尽/设备/readiness/空间，也不承诺下一次 TryReserve 成功，调用者不能用先查后改代替原子申请。已满时不再申请，解除阻塞后等待 P12 的合法到店事件。

错误 enum：`None, InvalidOwnerId, InvalidKinds, OwnerAlreadyReserved, CapacityOverLimit, InsufficientCapacity, TokenIdExhausted, InvalidToken, WrongOwner, InvalidTransition, InvalidFloorCellCount`。

确定性校验顺序：

1. Reserve：owner格式 → kinds完整格式 → owner已持有所请求种类 → 任一超额 → 所请求种类容量不足 → ID剩余不足 → 提交；同级按 kind顺序。
2. Occupy/Release：owner格式 → token为空/非本service签发 → owner不符 → 状态 → 提交。Released 的正确 owner Release 是成功；错误身份优先于终态处理。
3. UpdateFloorCellCount：输入格式 → 一次替换三项 Limits；不改 token、状态和 next ID；相同值重复更新成功且无变化。
4. 查询按 kind 或 token Id 递增返回独立只读 snapshots；包含 Released 历史。查询不得改变任何状态。

所有 API 主线程同步；两个先后请求争最后名额，只允许第一个成功。不加入锁、async、event bus、singleton 或 dependency injection 框架。

原子范围仅限同一 CapacityService 的一次 batch 或面积更新。它不含 P9 Create/Complete、支付或实际移动；未来整合失败由上层明确释放尚未使用的名额，不在 P10 自动监听订单失败或强行让仍在店内的人消失。错过释放事件是后续消费者的风险，P10 提供可核对的 owner/token 历史。

## 6. 生命周期例子与实际位置边界

| 上层实际事件 | 对应容量命令 | Order 边界 |
|---|---|---|
| 到店事件通过其他入店条件 | TryReserveAdmission(visitId) | 未提前创建真实订单 |
| 实际进入店内 | Occupy(Total) | 无 |
| 实际抵达 Counter 队伍 | Occupy(Counter) | 点单/付款以后续流程为准 |
| 点单结束且离开 Counter 位置 | Release(Counter) | 订单可继续处理 |
| 抵达预留的取餐等候位置 | Occupy(PickUp) | 送达才 ReadyForPickup |
| 正确顾客取走商品 | 无自动容量变更 | Complete；Total/PickUp 仍可占用 |
| 离开取餐位置 | Release(PickUp) | 保持 Completed |
| 实际离店 | Release(Total) | 保持 Completed |

每个 token 可独立管理；低层 API 不推断顾客位置、不强制三种 token 的跨种类顺序。整合层负责按表调用；诊断工具允许单种预留以构造 Pick-up 单独满额等情况。未发生入店而明确放弃的预约可以由 owner Release；自动超时、普通顾客取消行为并未获批。

## 7. 手工验收工具

新增 `Window > AnimalCafe > Phase 10 Capacity Debug`，显示“P10 Debug / 临时容量模拟；不是真实营业、站位或 spawn”。

- 字段：Floor cells 默认64；Owner ID默认`V1`；Kind复选项默认三种全选；Token ID可输入，默认1。
- 按钮：Apply Floor、Reserve Admission、Reserve Selected、Occupy、Release、Reset Session。
- Token ID 仅在该窗口 session 的 GetReservations 中查找签发 token；没有匹配时用 null 调用并显示 InvalidToken，不创建 token、不跨session反查。
- 表格显示三种 Limit/Reserved/Occupied/Used/Available/OverCapacity、CanAdmit、全部token的Id/owner/kind/state和最后result。状态始终重新查询，不缓存旧表格。Floor输入与已应用Floor值分开显示，编辑输入未Apply不会修改容量。
- 单种批量操作和非法字段可真实执行；错误以result显示，Console不应新增Error/Exception。Floor负值不得进入exception路径。
- Reset、新开窗口、关闭重开、进入/退出Play均清空session、结果、输入、勾选与滚动；清除本窗口输入焦点，防止P9曾遇到的旧输入残留。
- 绑定真实playModeStateChanged，OnDisable解绑；Domain Reload 开/关都必须有效。只操作自己窗口，不保存Scene/Prefab，不改变Selection、PlayerPrefs、Time.timeScale或Editor设置。
- Debug在Pause/1x/2x下可执行；无自动spawn或释放。此行为不定义正式游戏暂停权限。

## 8. 文件边界与开发安排

以下路径相对本文所在 P9 checkout，实施复用此基线，不另取旧main：

| 文件 | 用途 |
|---|---|
| Assets/Scripts/Capacity/CapacityKind.cs、ReservationState.cs | 两个enum |
| Assets/Scripts/Capacity/CapacityRules.cs、CapacityLimits.cs | 面积公式与immutable上限 |
| Assets/Scripts/Capacity/CapacityToken.cs、ReservationSnapshot.cs、CapacitySnapshot.cs | 身份凭据与查询数据 |
| Assets/Scripts/Capacity/CapacityResult.cs、CapacityFailureReason.cs | 操作结果和错误码 |
| Assets/Scripts/Capacity/CapacityService.cs | 唯一容量写入入口 |
| Assets/Scripts/Capacity/FloorCapacitySource.cs | 从既有Layout读取室内有效格数 |
| Assets/Editor/Phase10/CapacityDebugSession.cs、CapacityDebugWindow.cs | 临时验收工具 |
| Assets/Tests/EditMode/Phase10/ | 公式、来源、原子预留、状态、缩容、P9边界、窗口测试 |
| Assets/Tests/PlayMode/Phase10/CapacityDomainPlayModeTests.cs | 帧与时间边界 |
| Docs/Phase10_Beginner_Guide.md | 实现后补操作与实际evidence，引用原case ID |

文档准备阶段新增本design、配套test-cases和plan。Owner批准后，实施第一步已将规则同步至Project Design的3.4、Roadmap的P10及P12/P28交接注记；明确缩容例外，不重写P9历史，也不把P9标Completed。涉及的Living Docs变更仅限以上规则与真实状态。

不改P9 OrderService、现有Layout行为、Scene、Prefab、asmdef、Packages或Save。没有存档迁移，无Python/virtual environment需求。Assets新文件/文件夹在实现阶段附Unity生成的.meta。

## 9. 验收与已知限制

采用一份test cases与一份plan：每Task可信RED→最小实现→focused GREEN→直接regression；Phase末完整EditMode/PlayMode、Engineering和QA review、真实GUI/Owner验收。没有初始RED的项目如实披露，不能倒填。PASS/FAIL/SKIP/BLOCKED/NOT_RUN严格区分。

Owner与朋友在P11后的集中review不替代P10必要验收。P9的GUI代跑授权不自动扩展到P10；本P10 manual默认Owner执行，后续可明确授权Codex操作，逐项记录实际执行者。

游戏规则与接口/文件/测试全套文档已于2026-09-29获Owner批准。代码及验证按plan小步执行，实际结果在plan/test cases更新；静态文档检查不等于功能PASS。实施批准不包含commit/push/merge或新建checkout。
