# Phase 12 — Customer Spawn & Counter Queue Design

> 状态：Approved design — 到期机会保留、安全跟随与Owner追加批准的20fps原生路径提交恢复均已实现（2026-10-09）；受影响直接Play112 PASS/0 FAIL/0 SKIP，P11/P12 Edit126 PASS/0 FAIL/0 SKIP。完整30游戏分钟循环通过；此前109 PASS/1 FAIL为修复前快照。M1～M8的8/8 Owner PASS保留为历史记录，本轮仅补连续来客/前移视觉观察；全项目完整回归与Player/真机未以本轮结果代替，详见Report。
> 日期：2026-10-04（America/Toronto）。代码基线：main `e53bd03`。
> Sources of Truth：[Game Design](../../AnimalCafe_Project_Design.md)、[Roadmap](../../AnimalCafe_Development_Roadmap.md)、[Phase Process](../../AnimalCafe_Phase_Development_Process.md)。

## 1. 目标与范围

让顾客按随机间隔入店，遵守容量与真实空间限制，按到店顺序排队、前移，并沿有效路径安全离店。P12 使用 P10 Capacity 和 P11 Navigation；本阶段不接入收银、制作、付款、真实 Order ownership、Pick-up 流程、耐心值、满意度、离线模拟或 Save 中的顾客持久化。

玩家可见结果：顾客能走进队伍；点击验证按钮让队首离开，后方依次前移；满额/堵路有明确状态；暂停、2x 和装修恢复不破坏顺序或占位。

## 2. Owner 已确认的行为

| 项目 | 已确认规则 |
|---|---|
| 到店 | 随机 1～5 个游戏秒一位；暂停冻结，2x 跟随游戏时间加速 |
| 满额/装修 | 不积压、不补发；恢复后重新抽取间隔 |
| 排队 | 优先直线，遇障碍按固定规则转弯绕开；空间不足停止接纳新顾客 |
| 验证离场 | 验证场景提供“让队首离开”按钮，检查离店和队列前移 |
| 入店前失败 | 取消本次生成，释放全部预留 |
| 店内失败 | 停在当前位置，提示原因，保留实际占位和相应容量；修复布局后从当前位置重新寻路 |
| 安全 | 不瞬移、不穿墙、不持续重叠；Failed/Recovered 不冒充原目标 Arrived |

既有规则继续有效：队首起点为 Cash Register Customer Side；初始延伸方向远离 Cash Register。Total/Counter/Pick-up 额度和三项 admission reservation 直接沿用 P10；不按队伍空间修改 Floor 计数。

## 3. 已批准的具体设计

以下具体方案随开发文档获批；逐项行为确认见 §2。

### 3.1 到店计时

采用连续均匀分布 `[1,5]` 秒，random source 可注入供测试。2026-10-09 Owner批准：营业条件、容量和物理队伍名额满足时计时；入口、队尾和路线暂时不通不重新抽取间隔。一次 Tick 最多尝试生成一位，丢弃超出的时间，不循环 catch-up。

手动暂停保留剩余时间，到期的顾客也不能在零游戏时间时生成。到期未能入场仅保留一位待入场顾客，显示0秒；后续有游戏时间推进的Step继续验证门禁，安全时接纳成功，再通知clock抽取下一段完整1～5秒。进入装修、组件不可用、真实movement Blocked、容量或物理队伍名额用尽时取消当前间隔与待入场机会；恢复后重新计时，不积压批量补发。无效配置/布局则持续阻塞，直到配置修复或 layout revision 更新。此规则替代此前失败后重抽完整间隔的处理。

GameTimeService 已设置 Unity timeScale，runtime Update 直接传 `Time.deltaTime` 给 clock；不得再乘 CurrentSpeed。CurrentSpeed 只用于显示/暂停识别，测试可直接注入 scaled delta。不修改 IGameTimeService。所有顾客推进和计时在同一主线程执行。

### 3.2 确定性队伍几何

P12 使用一个 Cash Register。无有效 register 时停止接纳；多个有效 register 时按稳定 InstanceId 选择首个，并在验证 HUD 显示选中对象，避免依赖 GameObject 枚举顺序。

以 Customer Side anchor 为 slot 0，使用 gridRoot 的水平坐标系形成正交 slot 链。Owner于2026-10-07批准紧凑间距试用：默认与最小间距为 `1.0 m`，同一段直队可以均匀拉开；代理半径0.45m、CollisionSkin 0.01m、Epsilon 0.001m、到达误差0.08m不变。名义间距比身体路线保护0.911m多0.089m，不能覆盖两位各自8cm停止误差的任意组合；因此保留实际身体、TailClear和每段路线复验，并以真实prefab到达/离场证据验收，不仅检查目标坐标。默认队首local(2.5,2.5)，第二(2.5,1.5)，第三(2.5,0.5)，第三优先站在第二后方。不缩小角色或改变P11导航参数。

每一步候选方向仍为：保持当前方向 → 相对左转 90° → 相对右转 90°；不立即回头、不重复slot。候选必须在本world可走区域，避开实体、入口clearance和Pick-up Customer anchor，并验证代理间距及连续段可达性。Owner于2026-10-06确认：空Employee anchor允许排队；实际角色仍参与路线避让与World碰撞保护。Pick-up顾客位置按最大顾客代理直径＋CollisionSkin＋Epsilon＋ArrivalDistance保留安全空间（当前0.991m），不以1.1m队伍间距作为禁站半径。

Owner批准场边修正：优先沿当前方向，越出可走边界时先尝试对齐安全边缘，再左/右转；分支不足当前Counter容量时，有限回退已选候选并尝试其他方向。所有候选、边缘探测、重排复验共用256次检查预算（head单独验证）；找到完整容量即停止，否则保留预算内验证过的最长分支，同长度优先已安全对齐的分支，再保留原方向顺序。不承诺全局最长或所有布局八站位。回退只生成新slot链；真实角色仍沿原Navigation移动，不瞬移。

边缘对齐最多8次探测，从当前尾站位向前寻找不足1.0m的剩余安全空间；使用owned完整路径实际endpoint，再向内留2cm。该直段起点固定，其余站位均匀分布，间距最多增加25%（默认上限1.25m）；短段需要大幅拉开则保留原段。容量已填满立即停止，不继续为不存在的下一次转弯拉长末段。每个重排目标重新验证全体至少1.0m、原边界/家具/入口/Pick-up规则及当前prefix占位下的入口可达性。采样高度误差不改变水平队形；任何验证失败则恢复整段原站位。队首始终固定，不靠挤人新增名额。非队首目标距生成点至少最大代理直径＋CollisionSkin＋Epsilon＋最大ArrivalDistance（当前0.991m）；EntryClear针对实际位置按该已有角色半径＋最大新顾客半径/skin/epsilon判断（当前0.911m）。两处不再以队伍1.1m间距作为出生禁站半径；原入口clearance等待slot排除、exit 1.2m保护、Pick-up与所有碰撞检查保持。

候选还须在当前分支所有前方已接受slots都有人时，能通过既有有限CustomerRoutePlanner从入口到达；只读prefix随着回退撤回被放弃的站位，不能累计不同分支的虚拟角色。角色代理直径＋skin＋epsilon作为绕行间距，不生成被家具与前方队伍围死的站位。若预算内仍只找到7个安全slots而已有8位，超额尾客保留原位置/assignment/容量，停止接纳；前客安全离场后继续FIFO分配。不能把保留旧站位的Queued当作抵达新站位。

静态 slot 链不因顾客暂时经过而左右改道；动态角色碰撞由 P11 阻止，admission 还需验证入口与尾 slot 当前可用。逻辑名额和可用 physical slot 必须同时满足才入店。弯角与通道由真实 P11路径/代理验证，不能只看格子为空。

M-003修复（2026-10-05）：静态NavMesh路径可能穿过已排队角色。P12用有限临时waypoints绕过角色当前位置及正在前移的目标slot；2026-10-06并发前移采用§3.3的活动前客预测与FIFO目标保留规则。每条候选边检查完整owned NavMesh path的corners。最多130候选节点、24个route waypoint、32次segment提交及3次动态重算；没有安全路线则暂停入店或保留店内Blocked，不改slot链或P11碰撞政策。NavWorld仍是唯一移动执行者；中间Arrived不提交Queued或释放Total。disable/revision恢复从实际位置重算。

入口邻近柜台修正（2026-10-06）：对已知角色中心验证路线时，间距使用自身AgentRadius + 最大其他代理半径 + CollisionSkin + Epsilon；ArrivalDistance属于停止容差，不额外当作身体半径。队尾/admission仍保留到站误差保护，每段提交前从实际位置复验，实际移动持续受P11碰撞约束。

队首到站后朝收银台；其余顾客朝前一位的slot。入队和逐位前移均重新计算目标朝向，由P11平滑转身完成，不能直接写Transform。队尾动态占用检查按代理半径、CollisionSkin、Epsilon及ArrivalDistance计算安全距离；不以slot间距充当占用半径。

### 3.2.1 P12专用匀速模式（2026-10-06 Owner批准）

新P12顾客在NavigationActor注册前选择非serialized的SteadyPathMotion；默认P11角色保留native desiredVelocity。模式沿owned complete NavMesh path的水平corners以MaxSpeed提出移动意图，每substep最多抵达当前corner。NavigationWorld仍唯一写真实Transform，并保留静态/动态连续sweep、完整CollisionSkin、owned NavMesh、时间预算、暂停、turn/Facing和deadline约束。畅通路线保持匀速；真正碰撞、转身等待和每段最后不足一个substep的距离可以停止或缩短，不补走被guard裁剪的距离。

cursor只按真实pose推进。内部corner进入原ArrivalDistance范围后，可在owned NavMesh Raycast与原完整sweep全段批准时简化至下一corner；不能跳过最终destination。2026-10-07 Owner批准桌角修正：对贴近实体的内部corner提前检查一个沿最近实体外法线、距raw corner为ArrivalDistance减Epsilon的候选（默认0.079m，不超过原0.08m范围），从真实pose验证整个连接。仅采用安全候选后才标记该corner已调整；初次连接失败保留原corner，后续pose/实际身体变化时仍可检查同一有界候选，不增加候选搜索或放宽安全间距。这样避免未进入原到站范围即被拐角路径腿挡住，以及失败尝试永久锁死修正。最终destination与Arrived容差不变；下一腿在真实移动时仍受全部guards，不因前段批准而提前承诺可走。Stop、新request、disable/revision清理缓存与尝试状态。

P12临时route waypoint的中间Arrived还须验证真实水平pose距owned sampled endpoint不超过原ArrivalDistance；下一segment从实际pose重新查询并验证完整路径，必要时使用既有有限重算，不瞬移至waypoint。2026-10-06 Owner批准修正：完整skin可能使native拐点无法精确抵达，原Epsilon检查会把Service已合法完成的临时到达误判为Blocked。中间到达仍不提交Queued或释放Total；最终Queued/Exit仍遵守原Service Arrived与Facing合同，容量释放边界不变。没有scene/prefab/Save migration、NavMesh重烘焙参数或dependency变化。

### 3.3 FIFO 与前移

单一 CounterQueueService 分配 slot，以成功 admission commit 顺序作为 arrival order；顾客自己不挑 slot。新顾客从入口走向当前队尾，禁止插队。

队首离开请求只在队首已经 Queued 且没有前移事务时接受。原队首实际离开自己 slot 的保护区域后才能释放 Counter token；退出路线仍可能穿过队伍，因此前移还需动态路径安全。Owner于2026-10-06批准：队伍前移与装修重排改为每位独立任务；目标仍按FIFO顺序分配，但后客不必等待前客Arrived，可在安全约束下同时移动。每位拥有独立Moving/assignment generation与完成回调，只有自己真实到达才提交Queued，不交换slot或冒充前客到达。

后客路线规划只对同队更前、实际启用且持有活动前移请求的顾客预测腾位：保留前客目标slot，不把其移动中的当前位置当作永久障碍；静止顾客、员工/外部角色、Entering/Exiting、disabled角色继续原检查。实际身体仍在P11 World完整连续碰撞检查内，狭窄拐角可限制位移/暂时等待，半径、skin、匀速模式及有限timeout/retry均不改变。不能穿角色/家具/墙或直接写Transform；此调整不扩张CustomerRoutePlanner搜索范围。

提交前移assignment前，用实际剩余native路径检查活动前客/离场者：交叉、对向或拐弯冲突时先等待，不修改assignment或generation。后客不能提前停在前客还需经过的通道上；同向逐格跟进可使用前客刚离开的站位，但仍先检查交叉冲突。规划目标保留采用FIFO优先级：前客及离场者不被后客尚未抵达的目标挡住，后客仍保留前客目标；实际身体始终参与避让和完整碰撞检查。这是任务启动门禁，不增加移动deadline或重试次数。

Owner于2026-10-07批准并行入店：已有Entering/Advancing/Exiting不再单独禁止下一位入店。CounterQueue只在FIFO目标已稳定、存在唯一队尾slot且离场者已实际离开slot后允许预留；尚待重新分配的旧slots禁止接纳，不能用实际尚未抵达伪造Arrived。新入店前继续检查容量、入口身体安全、队尾实际空位、静态完整路线及所有活动顾客的剩余路线/目标。交叉、对向或新队尾会堵住已有任务时，在reserve/create前等待，已有移动优先；同向且保持安全空间可并行。实际P11连续碰撞、有限路线与timeout不变；到店clock的当前等待规则见§3.1，不补发积压。

本轮不扩张离场按钮门禁：已有入店或前移需先完成，前一位Exiting抵达出口后才接受下一次离场。队首实际离位后，下一位仍可在原队首Exiting期间前移，已有后客可并发跟进。静止Blocked尾客不会单独禁止Queued队首离场，退出路线仍需绕开它；普通非Decor缩队继续保留tokens并允许FIFO排空。新入店目标不能反过来限制更早顾客的未完成任务，角色实际身体始终参与检查。

movement 失败时暂停受影响队伍的继续推进和新 admission，保留 assignment/占位；前移失败会退休已在移动的更后顾客请求，停止其移动且保留原位置/容量。修复后按FIFO从各自实际位置恢复。movement callback 必须同时匹配 visit ID、assignment generation 和 layout revision；准备状态后再调用 MoveTo，支持同步失败 callback，旧结果不修改新 assignment。

2026-10-08 Owner批准同弯连续入店修正：原空间线段交叉检查会把同一弯道的不同路径腿误认作冲突。仅对已有活动Entering顾客识别顺序共享的路线：前客起点须在新路线前方至少原clearance，各共同腿须同向并匹配相同/相邻段（容差仅原ArrivalDistance＋Epsilon），共同弯道末端必须对齐，之后只允许同向最后一段分流；真实内部横穿仍拒绝。新尾目标堵住前客剩余路线的检查、Exiting/Advancing冲突规则及P11全部实际身体guards不变。未匹配的复杂路线仍先等待，不引入交通调度或时序预测。clock仍为原1～5秒；失败尝试不预留容量、按后续interval重试。

2026-10-08 Owner批准队尾临时占位修订：仅本Flow拥有、实际启用、持有活动请求的Entering前客，且自己的最终slot在新队尾占用范围外，允许预测腾位并继续入店计时；静止/Queued/Blocked、外部角色及其他phase仍按实际位置拒绝。TrySpawn仍必须通过完整入店路线检查。若前客当前在新队尾clearance内，只可排除向外腾空的路线前缀：各段到队尾的距离须不再减小，退出后剩余路径不得重新穿回新队尾。前客的实际身体仍参加全部P11 guards，真实交叉/对向检查及FIFO目标保护保持。未满足条件仍等待，不改变1～5秒clock、失败后的后续interval或引入积压到店。

2026-10-09 Owner批准连续进客修订：上述两项2026-10-08记录保留历史；当前识别共同走廊上的顺序跟随，并允许其末端最多两段、向前且不回头的有限分流，容纳绕开前方slot产生的额外拐点。前客起点仍须在后客路线前方至少原clearance，共同段仍按原ArrivalDistance＋Epsilon匹配，真实内部横穿/对向继续拒绝。如果前客剩余路线经过新尾目标，还须验证最终目标不占该位置，并用剩余路线长度、两位MaxSpeed及一个身体clearance的时间余量估算前客先腾位；不再单凭未来位置相交拒绝安全跟随。实际World连续碰撞保护仍是最终限制，复杂未匹配或余量不足的路线继续等待；不改变代理半径、skin、速度、到站容差、FIFO或容量，原离场/前移冲突规则不变。

### 3.4 入口、出口与离场

复用既有稳定 Entrance ID、入口内侧 2×2 clearance 和 gridRoot。P12 用入口区域内的 authored spawn/exit marker；出口到达区必须在该 world 可走地面内，不要求角色走到 NavMesh 外。生成点与离场点区分，避免在同一位置相向重叠；首次生成位置是创建初始 pose，不是运行中 relocation。

入口2×2是家具/物品放置禁区，四格对人物保持可通行，可参与入店、前移和离场路线；不将该reservation生成NavMesh obstacle。排队等待slot继续避开入口clearance，人物经过该区域不受此slot规则限制。

Markers 通过 Unity Editor API 建立并验证，不修改既有 Entrance Save 数据。Exit marker 到达区与队伍 slot 不重叠；到达后隐藏/销毁代表已跨过抽象店铺出口。被堵住的离场者留在店内，占用 Total，并提示“离店路径受阻”；不强制删除来掩盖寻路失败。

P12 的“让队首离开”是验证用提前离场，不模拟完成购买。队首离场时取消未使用的 Pick-up reservation；Counter 在实际离位后释放；Total 只在出口 Arrived 后释放。入店前失败释放三项 reservation。店内移动失败不释放仍应占用的 token。

## 4. 生命周期与事务

`Preparing → Entering → Queued → Advancing / Exiting → Removed`；任一已入店活动阶段可进入 `Blocked`，保存原阶段，修复后恢复。Preparing 是尚未发布的创建事务，不作为玩家可见的营业顾客。

Admission 顺序：校验 gate/slot/入口 → 生成唯一 visit ID → TryReserveAdmission → 创建角色并初始化 ID/初始 pose → active 注册 → 原子发布 visit/queue assignment → Occupy Total → 提交 MoveTo。任一步在发布前失败，unregister/destroy 临时对象并释放三项预留；此过程不跨 movement Tick，不能先移动后称为入店前回滚。

发布后已在入口内，视为入店；Counter 在抵达 assigned slot 时 Occupy，Pick-up 保持 Reserved。发布后的 path rejection/失败进入 Blocked，保留 Total、Counter、Pick-up，不用 rollback 销毁可见顾客。发布/Occupy 本身失败属于创建事务失败，恢复到发布前状态后清理。

重复按钮、callback、remove 和 cleanup 必须幂等。正常离店由 Exit Arrived 清理；Owner批准的已确认装修完成reset及测试/scene unload 的显式 remove 作为 session teardown 清理所有所属 token，不声称顾客沿路离店成功。禁止只 SetActive(false) 后释放容量：P11 disable 保留 proxy；真正 remove 必须取消请求、unregister 并销毁实例。

## 5. 组件、接口与影响范围

优先组合现有服务，不引入通用 AI framework、event bus、新 dependency 或 pooling。

| File | 职责 |
|---|---|
| `Assets/Scripts/Customers/CustomerContracts.cs` | visit state、failure、只读 snapshot 与 queue assignment |
| `Assets/Scripts/Customers/CustomerSpawnClock.cs` | 注入 random source，统一计时和暂停/阻塞恢复 |
| `Assets/Scripts/Customers/CounterQueueService.cs` | FIFO、slot assignment、前移事务和 generation |
| `Assets/Scripts/Customers/CounterQueuePlanner.cs` | 确定性 slot 链；可注入合法性查询供 domain test |
| `Assets/Scripts/Customers/CustomerRoutePlanner.cs` | 有限临时waypoint查询，绕开排队角色；只产出路径，不写Transform |
| `Assets/Scripts/Customers/CustomerAdmission.cs` | 小型P10 lease wrapper，统一三项reservation rollback与实际离位/离店释放 |
| `Assets/Scripts/Decoration/DecorationModeController.cs` | 已确认修改的正常装修完成标记；非正常shutdown不发布 |
| `Assets/Scripts/Customers/CustomerFlowController.cs` | Unity prefab 生命周期、Capacity/Navigation/queue 协调及 HUD snapshot |
| `Assets/Scripts/Customers/CustomerQueueValidationController.cs` | 验证按钮和中文状态显示 |
| `Assets/Scripts/Navigation/NavigationActor.cs` | 新 runtime ID 初始化，保留 Editor Configure |
| `Assets/Scripts/Navigation/NavigationLayoutAdapter.cs` | 本world的只读point/path查询与首次有效营业布局的readiness启用 |
| `Assets/Editor/Phase12/Phase12CustomerQueueSceneSetup.cs` | 创建独立验证场景、markers 和显式 MainCafe 接线 |
| `Assets/Scenes/Validation/Phase12CustomerQueue.unity` | 可重复运行的 P12 人工验收入口 |
| `Assets/Scenes/MainCafe.unity` | 最终接入营业 flow、markers 与 readiness；必须通过 Unity API |

新 runtime 接口建议：`bool NavigationActor.TryInitializeRuntimeId(string id)`，空/空白 ID 拒绝；注册期间不可变；初始化仅一次，即便后来 unregister 也不复用 visit 身份。已有 prefab serialized ID 保留，由新实例初始化覆盖。多个同种实例必须可并存。

Domain 接口：`CustomerSpawnClock.Tick(float scaledDeltaTime, bool canAdmit, bool decorating)` 返回是否应尝试一次生成；`CounterQueuePlanner.Build(Vector3 head, Vector3 outward, int maxSlots, Func<Vector3, Vector3?, bool> isLegal)` 返回只读 slot positions；`CounterQueueService.TryJoin(string visitId)`、`TryBeginFrontExit(out string visitId)`、`Remove(string visitId)` 和只读 `Snapshot`。具体 snapshot 类型在 Contracts 中一次定义，不各自复制。

Queue/clock 不直接写 Transform、调用 Unity random 全局状态或释放 Capacity。FlowController 统一处理可重入回调；每个 session 使用单独 CapacityService，teardown 后释放引用。P10 已释放 token 历史增长在 P12 运行观察中记录，若出现实测长时成本再提出针对性改善，不预先改 P10。

P11 扩展为 runtime ID、owned-filter只读point/path查询和首次有效布局readiness启用，以及§3.2.1注册前选择的P12专用移动模式；默认角色移动、碰撞参数、retry、动画和材质保持原行为。Scene builder 使用自己的明确 root，幂等复用单套 World/Adapter/Bridge，不调用会复制 owner 的旧 P11 builder。不改旧 P11 validation scene。

## 6. 装修、暂停与恢复

进入装修：暂停时钟、停止发布新 admission，保留顾客身份/队伍顺序/容量；P11 layout invalidation 终止旧路径，P12 不把 LayoutChanged 当离店。保持当前 pose 与占位。

Owner于2026-10-07批准顾客装修reset：正常完成Decor且期间至少有一次成功Confirm/Store/表面修改后，旧顾客批次结束。清理仅遍历CustomerFlowController自己的visits：退休generation/旧请求、移除queue entry、unregister/destroy对应actor、Dispose其三项capacity token；保留CapacityService对象、递增visit ID及所有非顾客actor。员工即使挂在flow.transform下面也不能删除，不按World全部RegisteredActors清理。员工任务暂停/恢复属于P13独立合同，本轮不实现。

装修打开期间仍保留顾客；Confirm本身不立即删除。未确认Preview导致Done请求被拦截、取消Preview、无确认修改直接Done均不reset。正常Done为退出该编辑会话；即使business readiness未通过也清理旧批次，原P11 gate阻止恢复时间和新admission，不能立即生成。Decor组件shutdown/disable清理不是正常Done，不发布reset信号。

DecorationModeController提供只读CompletedLayoutChangeVersion：仅正常结束有确认修改的会话递增；启动authoring seed/导航rebuild、不含提交的开关不递增。Flow在Step消费变化一次，清理队伍/错误状态与clock后重新BuildQueue；布局有效才抽新1～5游戏秒间隔，不即时补满。重复Done/Step不能删掉新顾客；不重用visit ID，无订阅/event bus。无确认修改或非Decor的layout revision/disable/修复恢复仍从原位重算；空间不足时沿用原保留容量规则。

启动、disable/enable、重复 Play（含关闭 Domain Reload）不得重复订阅或创建 owner。禁用 flow 时停止时钟并撤销当前 movement，顾客保留占位与 token；重新启用先 revalidate。session 销毁清理对象、请求、订阅和 token exactly once。

MainCafe 初始未营业布局保持 passive，首次出现有效服务布局时才启用business readiness；验证scene从启动启用gate并在runtime准备测试布局。验证HUD在装修时整体隐藏，避免覆盖已有工具栏。

## 7. 方案选择、风险与验收

推荐：纯 domain clock/queue + 一个 Unity flow coordinator，复用 P10/P11。全部写进一个 MonoBehaviour 虽少文件，但回调和 rollback 难测试；为每个顾客建立完整通用 state-machine framework 则超出本阶段需要。

重点风险：1 秒下限快于单人移动周期导致入口拥堵；并发弯角挤压/等待；callback 重入；装修改变 slot；容量提前释放。解决方式为受控admission、保守间距、FIFO目标分配、完整连续碰撞、失败时退休后客请求、generation检查和事件对应的token释放。

自动与人工验收见 [test cases](2026-10-04-phase-12-customer-queue-test-cases.md)；步骤见 [implementation plan](../plans/2026-10-04-phase-12-customer-queue.md)。现有 P11 3 项 Minor 保留，尤其 Westie 密集队伍可读性需 Owner 判断。Android/iOS 真机、Player build 和正式 Pick-up 全链路不计为本阶段 Editor PASS。

当前：docs self-review与独立文档检查已完成，Markdown链接/空白检查PASS；已批准行为及Decor reset已实施，直接相关自动回归通过。Owner于2026-10-07明确确认更新后的M1～M8全部完成，manual为8/8 Owner PASS；当前代码的Phase最终完整回归仍待收尾，commit/push NOT_RUN。最新证据见 [Validation Report](../../Phase12_Validation_Report.md)。本文不修改P9–P11完成状态。
