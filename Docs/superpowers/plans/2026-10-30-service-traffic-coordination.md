# Service Traffic Coordination Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 统一P14/P15顾客与员工的空间承诺和等待，使普通拥堵自动推进，保留真实碰撞、付款、FIFO、容量、离店和营业周期。

**Architecture:** 保留当前owned NavMesh与NavigationWorld单一运动控制。普通C# ServiceTrafficCoordinator负责角色无关的候位／路线许可、稳定优先、事务和等待依赖；现有Queue／Order／Capacity仍持有canonical业务事实。先建立Main可信反例，逐入口迁移，不并存两套交通批准者。

**Tech Stack:** Unity6000.5.5f1、C#、NUnit/EditMode、UnityTest/Native PlayMode、PowerShell Unity CLI、Computer Use。

**Spec:** `Docs/superpowers/specs/2026-10-30-service-traffic-coordination-design.md`（2026-10-30 Owner回复“好的，开发吧，结束了和我说，然后你也可以直接开始测试”，批准设计、实现及测试；按此直接持续执行，不为逐Task重复索取批准）。

## Global Constraints

- 工作位置`E:/Unity/Project/AnimalCafe/.worktrees/phase-15-business-day`，branch`codex/phase-15-business-day`；保留此前P15未提交内容，不commit/push/merge或另起Phase。
- 现实营业28800秒、排空后休息7200秒；Pause／2x只缩放Scene工作，Pause不耗交通预算或使通行权按墙钟到期。
- 不改模型、半径、skin、epsilon、NavMesh Filter／Bake及运动底层行为；不瞬移、穿人、假Arrived或重生恢复。
- 既有Queue、付款、Order、容量ledger和checkpoint为canonical；空间许可不是第二套业务账。真实Exit才释放Total。
- 新的完整设计已授权。常规可逆实现选择在本plan/ledger记录Ruling后继续；新底层行为、Save/Scene迁移、不可逆操作或外部动作另报。
- 一个Native Unity过程，Editor完全退出后才改Assets；原失败证据保留，可信Native RED后才修相应生产行为。
- 编译、fixture前提和observer错误与产品FAIL分开。新源结果不得借用旧387 Edit／299 Play＋6SKIP。
- 不安装package，不手改Scene/Prefab/asset YAML。新C#及meta可正常创建，真实Scene布局通过正式Decoration API与GUI。

## Review Focus

1. 后客未付款reservation覆盖先有员工送杯／返程——Task1原故障trace＋Task3双向停车承诺反例。
2. 分腿／replan更换Native request而重排优先，或等待依赖循环——Task2稳定Intent与环检测，Task4 Native双向入口。
3. 提交事件重入造成Queue存在而traffic缺失／重复付款——Task2事务回滚、Task3集成重入。
4. Cancel／disable后身体仍存在，旧路线却继续占权或错误放行——Task2退休、Task4原身体正反例。
5. Closing中交通pending／idle缓存造成假排空或永不Rest——Task5 drain正反例及Task7真实周期。

## Files and interfaces

拟新增`Assets/Scripts/Navigation/ServiceTrafficContracts.cs`与`ServiceTrafficCoordinator.cs`，职责依Spec；新增Edit `Assets/Tests/EditMode/Phase14/ServiceTrafficCoordinatorTests.cs`、Native partial `Assets/Tests/PlayMode/EditorSceneLoading/Phase14/CafeServiceTrafficTests.cs`（真实class可复用`CafePickupSideTests`）。修改CFC、PickupQueueService、EFC、EmployeeTaskRunner、CafeLoopController及必要正式状态映射。物理driver/world/service保持原语义。

核心接口作为相邻Task契约；实现若需同义的小幅调整，先在ledger记录并同步所有调用／测试，不引入另一政策owner：

- `TrafficIntentKey`：ActorId、IntentId、Generation、LayoutRevision、业务Context；稳定取得序号由coordinator分配，不用Native request排序。
- `TrafficDecisionKind`：Granted、WaitingForTraffic、DeferredByBudget、Unreachable、PlanningFailed、InvalidStart、Cancelled、StaleContext。
- `TrafficDecision`：Kind、Reason、阻挡owner/资源及意图身份；区分未来停车保护与当前移动互斥。
- `ServiceTrafficCoordinator.EvaluateParking(...)`：新停车／服务侧在发布前统一核对全角色既有承诺及必要服务能力。
- `ServiceTrafficCoordinator.EvaluateMovement(...)`：已选余程、当前实际pose、目标和canonical上下文的唯一交通许可入口；不替代真实碰撞。
- `ServiceTrafficCoordinator.TryCommit(...)`：准备后同步提交canonical变更和traffic引用，失败回滚一次，外部事件仅在两者成功后发布。
- `ServiceTrafficCoordinator.NotifyChanged(...)`、`Retire(...)`、`ResetLayout(...)`：精准唤醒、幂等退休与换代；同一意图分腿保留序号／预算。
- `HasPendingWork`只统计有效活动grant／决策／回调／等待；不能把没有任务的idle缓存算进drain。

几何与当前业务数据通过小型不可变snapshot／只读delegate提供，不把private Visit或Order状态机搬到导航层。内部geometry helpers可保留，旧“谁优先／谁可预留”的政策必须迁出。

实施前预算校准：原4000仅为CFC目标保护的局部每帧上限，并非已有全局上限；原Native观察中一次真实Exit规划已有5612次查询。新协调器默认每帧128次实际Native查询、硬上限4000；同一稳定phase intent跨job／leg累计最多16384次，换Native request不重置。穷尽返回PlanningFailed，不冒充无路证明。相同布局／速度／观察设置下，P95／P99同时增加超过25%且超过2ms即调查；新增无法解释的产品帧>500ms视为失败。参数在新实现之前固定，不按失败结果抬高。

## Task 1: Main原故障、运行基线与保护测试

**Files:** 新`CafeServiceTrafficTests.cs`及meta；`outputs/phase15/service-traffic-coordination-20261030/`诊断、测试runner与收据。不得改产品。

**Inputs:** CFC C8F521、EFC71744D及既有Main证据。GUI已补拍Counter1x3 root local(4.5,0,5.5), rotationY90，CM root local(4.5,.42,5.5), rotationY180；CR留在原starter。准确slot/mounted朝向由正式surface模型与读回实际pose验证，不能猜完就把fixture叫原摆法。

- [ ] 正常关闭原Editor，保存布局／原快照与源码基线；验证无Editor/test进程后才写测试。
- [ ] 用正式Main及Decoration API建同布局；原始Main未配齐时无NPC，不开启seedValidationLayout。真实顾客、付款、员工Prepared与移动来自原流程。
- [ ] 复用默认关闭Pickup/Corridor/Movement/Path observers，在原决策点记录第二reservation、员工实际route suffix与下一腿拒绝。只运行一个120秒有界诊断，完整原始记录；若未建立前提，修fixture而非生产。
- [ ] 加入`MainDefaultCounterPlacementCompletesServiceWithoutTrafficDeadlock`（名称可按FQN规范调整），必须以真实首单／后续Exit与容量释放判断，原错误必须可见地失败；保存Native RED。
- [ ] 捕获同机基线Native查询／帧指标，将Task2新增共享每帧查询硬上限限定为不超过旧目标保护局部上限4000；后续结果与有效快照比较。

执行调整（2026-10-30）：普通随机窗口已跑，原布局读回通过，观察到6次真实Exit，没有重现截图首单Blocked；该轮日志overflow，不作产品RED或完整排空证明。受控slot1反例的“覆盖实际返程”前提也未成立。保留原GUI FAIL和内部predicate UNKNOWN；不继续靠随机时序碰运气。Task2先通过独立行为RED建立未接入的基础类，Task3在修改旧controller政策前补有效Native集成反例。原始因果缺口和最终Main回归仍需单列，不能因实现基础类而勾选完成。

## Task 2: 角色无关coordinator和Edit契约

**Files:** 两个新runtime文件＋meta，新`ServiceTrafficCoordinatorTests.cs`＋meta。

**Produces:** 上述接口与稳定身份、停车／已选旅程／当前瓶颈三类事实，不复制Queue/容量ledger。

- [ ] 先写可执行的契约RED：角色交换、后停车拒绝先路线、同帧稳定排序、分腿年龄保持、两/三方环拒绝、正确owner退休、disabled身体不删除。
- [ ] 先跑RED（缺类型编译错误不算完成行为RED）；最小可编译骨架后验证错误决策，再实现合同。
- [ ] 同步事务／失败回滚／事件重入tests；真实canon owner版本变化拒绝陈旧提交，未知预算不放行。
- [ ] 事件驱动唤醒与有界决策工作；单意图预算不因换request重置。无变化只等待，不重复完整Native搜索；Pause不消耗预算。
- [x] 跑新Edit组与E11碰撞／E12admission／E14Queue直接回归，独立检查spec＋quality。Task2当前39＋75＝114 PASS，fixround1复核C0/I0；退役大对象引用M1转Task4/6。

## Task 3: 候位能力与停车共同提交

**Files:** CFC、PickupQueueService、CafeLoopController，Task1/2 tests。

**Consumes:** EvaluateParking/TryCommit；**Produces:** 所有canonical停车／服务侧变化均有同一许可。

- [ ] Native RED证明新增目标不能覆盖先有员工往返或先顾客完整目标通路；反向角色同样执行。
- [ ] 新站位、已有reservation重验证、付款前准备／付款后转队、多侧／队伍前移调用统一入口，保留原canonical reservation和paid FIFO。
- [ ] 从现有自然单列候选选择安全链；逐个候位保留必要工作／出口能力，允许较短链与合法备用路，不永久预留未选未来出口。
- [ ] 队列与空间许可同步提交，完整失败／UNKNOWN不新增Actor、支付、Order、token。既有有效已付款目标不因新政策清除。
- [ ] 运行Task1原Main GREEN、新事务／reservation Native正反例及旧Queue／capacity／paid-order直接回归；检查没有两套活跃停车批准政策。

执行依赖说明（2026-10-31）：Task3先验收停车共同提交及真实后续取餐。当前实际发腿仍未完成Task4迁移：`CompleteLeg`尚无consumer，协调器仍把初始完整Exit路线当作当前移动段，真实phase handoff也未接线。已取得原始拒绝记录的ThreeNatural／OneX失败保留为Task4必须解决的集成责任，不能靠停车层放宽finite／frozen检查、自动重启terminal或修改旧断言通过。Task1原Main及完整连续经营GREEN仍是全重构的完成条件；Task3局部通过不等于它们通过。

## Task 4: 所有移动入口与有限等待迁移

**Files:** CFC、EFC、EmployeeTaskRunner、CafeLoopController，新Native tests。

**Consumes:** EvaluateMovement、NotifyChanged、Retire；**Produces:** 顾客与员工统一通行、稳定先后、有限Waiting及真实Blocked。

- [ ] 为admission、取餐转队／前移／领取、送杯／返程、实际Exit初次／pending／replan、让位恢复建立跨入口RED；Native actor/真实几何不伪造。
- [ ] 逐入口只执行一次traffic政策，迁移并删除被替代的CFC角色专属优先分支；geometry查询和物理最终门禁保留。
- [ ] 已选送杯余程／返程保护对长期停车生效，当前冲突段才互斥；原多侧合法并行保持。普通Waiting在真实腾空事件后自动继续同一任务。
- [ ] 保留Prepared／杯／付款／FIFO／容量；拒绝循环依赖，冻结owner集合、年龄和预算，真正Blocked不自动retry。
- [ ] 接入cancel／disable／destroy／Scene／layout／分腿退休和回调身份；保留仍存在身体，恢复基于实际pose。
- [ ] 跑R01–R13与原八组20实际Exit／两档20订单，Independent review后再扩大运行。

## Task 5: Drain、正式提示与装修兼容

**Files:** CafeLoopController、CafeLoopStatusView／必要UI映射、E15/P15 tests。

- [ ] 先写新交通pending阻止Rest、仅idle缓存不阻止Rest的RED；Pause保存权利、2x不双重缩放。
- [ ] 接入HasPendingWork及typed Waiting／Blocked提示，隐藏内部ID；Main无黑HUD。保持28800/7200及实际排空规则。
- [ ] 取消Preview不退休旧承诺；真实Confirm一次LayoutInterrupted，已取餐Exit的Actor／杯／Total保留；回岗和旧回调规则不变。
- [ ] 跑P13独立模式、P14装修和P15全部自动回归，以及横竖屏HUD直接回归。

## Task 6: 扩展布局、成本与完整自动回归

**Files:** 新Native tests／必要小型test support、输出报告；真实业务RED所必需的窄Core／CFC修复及对应Core tests；不为测试改业务断言。

- [ ] 增加12固定可重放seed，预先独立标注可运行／应明确拒绝；覆盖柜台尺寸、旋转、墙角、窄道和来客时机，最小失败独立保存。
- [ ] 同源无重型trace运行原8布局各20实际Exit、中央柜台两档各60及必要现实时间／跨日场景，记录真实Order/Exit/token/drain。
- [ ] 性能与Task1同机基线比较。工程门槛：无变化等待零新增完整Native搜索；协调查询不超过旧4000共享上限；新增不可解释>500ms卡顿或P95/P99显著退化视为待修，不能只用test总时长判定。
- [ ] 新源码完整EditMode＋PlayMode集中一次，读取真实XML（重复/缺失/case状态），保留原opt-in SKIP分类；单一Unity串行，恢复已知测试资源和历史输出。
- [ ] 失败先定位并focused修复，最终完整回归基于最终源；无代码／新失败不重复全suite。

执行调整（2026-11-02）：Main已完成3次真实Exit，但后2客无法排空。原失败回调已证未获权pickup-wait的`new-frozen-owner`；同时真实Queue前移使Reserve preview暂不可用，旧queued请求仍按旧目标进入Ready。保留closed-set／D1原合同，不扩大Frozen或重启Terminal。拟仅在`ServiceTrafficCoordinator.cs`、`CustomerFlowController.cs`及`ServiceTrafficCoordinatorTests.cs`加入受限producer hold：精确当前Waiting、原正Wait预算、未获权Parking、原身份／claims有效时暂停Ready；Notify不能绕过，Tick照常扣原预算；仅成功验证新的真实preview并走原typed refresh后恢复，即使shape不变。Key／FirstFrame／顺序／累计查询／Frozen／原时限均不重置，已获权、Terminal、过期、未知来源不得进入。先以真实Queue的Reserve／Advance／MarkArrived模型和no-op stub取得FEATURE RED，再实现；保护暂停期间Notify、同shape恢复、预算到期、身份变化、真实权限和原D1反例。真实Main已有Native RED；模型不代替修后Main全部来客Exit／drain，后续仍按上述同源布局及最终完整回归。

## Task 7: 实际操作、独立review与交付

**Files:** 实测evidence、Validation Report、Guide、Roadmap、GDD及本plan状态。

- [ ] 用Computer Use执行TM01–TM07；完整普通Main目录操作不以API／seed替代，原摆法、普通拥堵、真实故障恢复分别记录。
- [ ] Main15现实分钟、Validation10分钟健康窗口，含1x/2x；实际Pause／菜单区间不计活跃窗口，健康窗口不注入／恢复。验证至少20实际Exit及跨日真实排空。
- [ ] 最终Engineering、QA独立检查Spec／代码／Native／manual证据；无Critical/Important才声称本地技术完成。Owner观感／device、8h、CI／Git／merge边界分列，不借本地成功正式关闭P15。
- [ ] 正常Stop／关闭Editor，核对未意外保存Scene及已知资源；更新文档中的当前状态、真实counts、限制和如何打开本分支。

## Execution ledger / review

- Owner已明确要求直接开发并测试，本plan可在当前授权下执行，不再询问逐Task许可或执行方式。
- 使用subagent-driven-development：实现串行，root统一运行Unity／收据，独立review按有风险的Task边界进行；不让多个实现者同时写共享契约，不让子agent再spawn。
- 没有commit记录可作恢复点，ledger必须保留当前文件hash／Task结果与原失败证据；不删除已有工作区／证据来满足通用skill清理步骤。


### Task6 routine ruling: legacy employee-wait test contract migration (2026-11-02)

The approved integrated traffic migration replaces the former role-specific wait authority. `EmployeeExitWaitAttempt` and `CanWaitForOwnedEmployeeExitTraffic` are legacy implementation details; the integrated Send path bypasses their writer and the old oracle requires a non-null Route that the new preparation intentionally does not provide. Migrate the shared Native helper for its five existing public cases to actual Core source/claim/AcquiredSequence, real Bound Native provenance, finite Waiting blockers or a proven safe Granted path, original-key age/budget preservation, and actual cancellation retirement. Preserve paid order, cups, actor/token identity, no fake Exit, two real exits/drain and strict multi-substep delivery arrival checks. Do not restore a second product traffic authority or disable Core for these tests.

This is an authorized reversible implementation/test migration under this plan, not a relaxation of business assertions. Source review and the old-to-new contract mapping are recorded in `.superpowers/sdd/2026-10-30-service-traffic-coordination/task6-employee-exit-core-contract-migration-r2-draft/ruling-and-coverage.md` and the independent compatibility ruling. The integrated Native negative case combining an actual Exit waiter with a genuinely obstructed current Returning leg and its unchanged wait budget remains OPEN; separate Core and return-obstacle tests do not close that coverage gap. No Phase closure follows from the migration alone.
