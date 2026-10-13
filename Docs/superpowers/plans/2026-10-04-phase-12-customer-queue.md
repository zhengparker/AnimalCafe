# Phase 12 Customer Spawn & Counter Queue Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans for native execution, or superpowers:subagent-driven-development only if Owner selects that method. Steps use checkbox syntax.
> 状态：Approved — Task实现及M1～M8人工验收已完成，8/8 Owner PASS（2026-10-07）；当前代码的Phase最终完整回归待收尾。原授权日期2026-10-04（America/Toronto）。

**Goal:** 顾客按1～5游戏秒随机入店，安全FIFO排队、前移和离店。

**Architecture:** 使用独立clock/queue domain与一个Unity flow coordinator。复用P10 Capacity/P11 Navigation，World保持唯一Transform writer；验证按钮提供受控队首离场。

**Tech Stack:** Unity `6000.5.5f1`、C#、现有AI Navigation `2.0.14`、Unity Test Framework/NUnit；不安装或升级dependency。

**Spec:** [P12 design](../specs/2026-10-04-phase-12-customer-queue-design.md)；[test cases](../specs/2026-10-04-phase-12-customer-queue-test-cases.md)。

## Global Constraints

- 到店随机1～5个游戏秒；暂停冻结，2x跟随游戏时间加速。
- 满额/装修不积压、不补发；恢复后重新抽取间隔。
- 不瞬移、不穿墙、不持续重叠；Failed/Recovered不冒充原目标Arrived。
- P10三项预留不变；真实离位/离店才释放对应容量。
- 不接入P13/P14收银、制作或Order，不改Save schema，不引入pooling。
- 正式目标Android/iOS；本Phase证据为Windows Editor，不冒充真机验证。
- 新Scene/markers/serialized接线经Unity API/CLI，不手写YAML。
- 保留用户现有修改；不自动commit/push/merge/删除worktree。
- Project Phase Process优先：Task focused，Phase集中review/full regression/manual。

## Review Focus

1. 同步callback和返回后写状态：E-013/E-014由Task4覆盖。
2. 1秒生成造成入口暂占：E-019/P-003由Task2/4覆盖。
3. 弯角与采样误差造成队伍挤压：E-007/P-003由Task3/6覆盖。
4. 装修减少可用slots但仍有老顾客：E-018/P-007由Task5覆盖。
5. Domain Reload off留下旧session：P-009由Task5/6覆盖。

## 文件结构与隔离

按spec §5创建7个Customers源码（含小型CustomerAdmission lease wrapper）、一个Phase12 Editor builder、一个validation scene；修改NavigationActor和最后的MainCafe接线。测试分别放EditMode/Phase12、PlayMode/Phase12和既有EditorSceneLoading目录；沿用现有asmdef引用，若不包含新依赖先说明必要范围再修改，不默认新建assembly。

P11代码扩展提供runtime ID、owned-filter只读查询及首次有效营业布局的readiness启用；不改变移动/碰撞算法。无需Save migration，旧serialized ID和Editor Configure继续有效；需要旧prefab、重新启用和注册生命周期regression。Capacity规则、Navigation移动策略、角色模型/材质和旧validation scene不改。

实施开始先复核HEAD/status及文档批准；在项目授权边界内创建独立P12工作区，从main `e53bd03`或经核对的更新main开始。带入这三份文档，保留main本地P11收尾修改。文档准备不创建worktree。

## 验证命令

以下沿用既有Unity CLI形式，执行前读取unity-cli skill并用`unity test --help`核对本机参数；命令均在获批工作区根目录。不可把CLI参数错误当产品RED。

```powershell
unity test . --mode EditMode --filter AnimalCafe.Tests.EditMode.Phase12 --output outputs/phase12/focused-edit.xml --timeout 600
unity test . --mode PlayMode --filter AnimalCafe.Tests.PlayMode.Phase12 --output outputs/phase12/focused-play.xml --timeout 600
unity test . --mode PlayMode --filter AnimalCafe.Tests.PlayMode.Phase12Integration --output outputs/phase12/integration.xml --timeout 600
```

同名focused输出会覆盖，重要RED/GREEN用不同后缀。验证前保护用户Scene与必要资源；若Unity序列化无关资源，只恢复确认由测试生成的变化。一次只运行一个Unity process。

## Task 1 — Runtime visit identity

**Files:** 修改`Assets/Scripts/Navigation/NavigationActor.cs`；扩展`Assets/Tests/PlayMode/EditorSceneLoading/Phase11/NavigationMovementTests.cs`。

**Interfaces:** Produces `bool NavigationActor.TryInitializeRuntimeId(string id)`；只允许实例初始化一次，注册后不变；不改变World.Register签名。

- [x] 写P-001 RED：两只同种角色分别初始化唯一ID可注册；空ID、二次初始化及已注册actor改ID均拒绝，原ID不变。
- [x] 运行该用例，确认行为断言或缺失接口的可信RED。
- [x] 最小实现runtime初始化；保留Editor Configure和serialized字段兼容。
- [x] focused GREEN，再运行NavigationMovement/Animation/Asset直接回归。

## Task 2 — Spawn clock

**Files:** 创建`Assets/Scripts/Customers/CustomerSpawnClock.cs`、`CustomerContracts.cs`；测试`Assets/Tests/EditMode/Phase12/CustomerSpawnClockTests.cs`。

**Interfaces:** `CustomerSpawnClock(Func<float> nextUnitRandom)`；`bool Tick(float scaledDeltaTime, bool canAdmit, bool decorating)`；`float RemainingSeconds`。2026-10-09修订增加`void CompleteAdmission()`：Tick仅保留一位到期机会，成功后通知新一轮；0 delta保持且不生成，满员/装修取消，临时入口/尾位/路线占用不传作clock取消条件。

- [x] 写E-001～005/E-019 RED：1/5/3边界、30秒大Tick只一次、暂停保留、阻塞恢复重抽、非法输入状态不变。
- [x] focused RED后实现均匀映射、可接纳状态转换和每Tick至多一次attempt；runtime使用已经缩放的Time.deltaTime，不能再乘CurrentSpeed；无业务Capacity访问。
- [x] focused GREEN；P-006的真实1x/2x缩放在Task6执行，domain只接收已缩放delta。

## Task 3 — Queue geometry and FIFO authority

**Files:** 创建`CounterQueuePlanner.cs`、`CounterQueueService.cs`；扩展`CustomerContracts.cs`；测试`Assets/Tests/EditMode/Phase12/CounterQueueTests.cs`。

**Interfaces:** `IReadOnlyList<Vector3> Build(Vector3 head, Vector3 outward, int maxSlots, Func<Vector3, Vector3?, bool> isLegal)`；`bool TryJoin(string visitId)`、`bool TryBeginFrontExit(out string visitId)`、`bool Remove(string visitId)`。constructor接收有效slot chain；Contracts中定义`CustomerVisitSnapshot`/`QueueAssignment`，含visit ID、slot index、generation、state。

- [x] 写E-005～011 RED：1.1m、直/左/右优先级、禁止回头/重复、FIFO、重复按钮；前移原串行要求于2026-10-06被Owner批准的独立移动规则替代，后客可安全同时跟进。
- [x] 写稳定register选择和gridRoot旋转后的规则测试；UI/Geometry查询由后续flow注入。
- [x] 实现有限slot链、只读snapshot和单一assignment authority；slot-chain revision update保持visit顺序，不能静默丢老顾客。
- [x] focused GREEN；真实Collider/NavMesh、动态占位与弯角P-003留Task6，不能仅domain PASS就声称空间通过。

## Task 4 — Admission and movement lifecycle

**Files:** 创建`CustomerFlowController.cs`；测试`CustomerAdmissionTests.cs`、`Assets/Tests/PlayMode/Phase12/CustomerQueueFlowTests.cs`。

**Interfaces:** consumes Task1 ID、Task2 clock、Task3 assignments、`CapacityService.TryReserveAdmission/Occupy/Release`、`NavigationWorld.Register/Unregister`、`NavigationService.MoveTo/Cancel`。Produces `bool TrySpawn()`、`bool TryLetFrontLeave()`、`void RemoveVisit(string visitId)`和只读visit snapshots。TrySpawn只用于controller/test，不暴露普通玩家刷顾客按钮。

- [ ] 写E-012～017/E-019 RED，注入各创建失败点、同步MoveTo失败、重复callback/cleanup与错误owner。
- [x] 实现spec §4 admission发布边界；发布前全部rollback，发布后失败Blocked且保留token。Capacity失败不留half-created对象。
- [x] 先提交generation/assignment再MoveTo；拒绝旧callback。只Arrived推进状态；Recovered单独处理为原目标未到达。
- [x] 实现前移assignment/实际到达握手、实际离位释放Counter、验证提前离场取消Pick-up、Exit Arrived释放Total；2026-10-06每位独立Moving与callback，FIFO目标顺序保留。
- [x] focused GREEN；跑P10 reservation/lifecycle与P11 callback/collision直接regression，并补P-002/P-004/P-005真实路径。

## Task 5 — Decoration recovery and session lifecycle

**Files:** 扩展`CustomerFlowController.cs`及Task4测试；若必须新增Navigation只读查询，在实现前列出具体签名/文件/直接回归范围，不重写P11移动核心。

**Interfaces:** consumes已有`NavigationDecorationBridge`/`NavigationLayoutAdapter` readiness/revision与`GameTimeService.CurrentSpeed`。Produces内部revalidate/resume、session cleanup；不扩大公共IGameTimeService。

- [x] 写E-014/E-018、P-007～009 RED：装修改变register、3位顾客只剩2个slots但安全出口有效、disable/enable、unload、Domain Reload off两次Play；断言保持原位/FIFO、禁止admission，仍可队首逐位离场并恢复，queue不足不能锁住全局游戏时间。
- [x] 实现停clock/路径、保留身份与tokens，readiness通过后重算slots并从实际位置恢复；区分admission与已有顾客movement gate，不足时停止admission而不驱逐，保留无新slot顾客的原位与FIFO，并允许安全队首离场。
- [x] teardown取消请求、unregister、销毁实例、释放token/订阅exactly once；disable保持占位，enable先验证。
- [x] focused GREEN，再跑NavigationLayoutIntegration/装修gate/time直接regression。

## Task 6 — Saved scene, MainCafe and phase acceptance

**Files:** 创建`CustomerQueueValidationController.cs`、`Assets/Editor/Phase12/Phase12CustomerQueueSceneSetup.cs`、`Assets/Scenes/Validation/Phase12CustomerQueue.unity`及`Assets/Tests/PlayMode/EditorSceneLoading/Phase12CustomerQueueSceneTests.cs`；修改`Assets/Scenes/MainCafe.unity`。

**Interfaces:** validation HUD消费flow snapshots；“让队首离开”按钮调用TryLetFrontLeave。Builder `CreateValidationScene()`幂等，单套World/Adapter/Bridge/Flow；不执行旧P11 builder。

- [x] 写P-010/P-011 RED：builder重复运行、入口/出口markers有效、只有单套owner、MainCafe gate启用、原有Decor继续工作。
- [x] 用Unity API创建scene/markers，接入真实Shiba/Westie；显示选中register、剩余游戏秒、状态、三项容量与中文原因。
- [x] 跑P-003/P-006/P-012：密集入口/弯角每帧安全、帧率/游戏速度、30游戏分钟受控循环并记录性能/历史token增长。
- [x] scene focused GREEN与MainCafe相关regression；准备M-001～008的Beginner Guide，不填未执行manual PASS。
- [x] 集中完整EditMode/PlayMode、Engineering/QA review；Important已修复并复核，最终2465 Edit PASS / 1148 Play PASS / 5 opt-in SKIP。队伍视觉可读性由下项Owner manual确认。
- [x] Owner完成manual：2026-10-07明确确认当前M1～M8全部完成，8/8 Owner PASS；更新`Docs/Phase12_Beginner_Guide.md`、`Docs/Phase12_Validation_Report.md`和Roadmap当前状态。当前代码的Phase最终完整regression仍需收尾；文档/code交付不自动代表merge。

Phase最终命令：

```powershell
unity test . --mode EditMode --output outputs/phase12/final-edit.xml --timeout 5400
unity test . --mode PlayMode --output outputs/phase12/final-play.xml --timeout 5400
```

## 执行与审批

推荐native：主agent串行实施六个Task，接口连续且Unity执行必须串行；Phase末独立Engineering/QA review。Owner也可选择subagent-driven，需明确选择后再调度。

Owner已批准design/test cases/plan并授权实施。已完成项见checkbox；Task4异常注入仍有PARTIAL/NOT_RUN边界，最终全套自动回归完成，Owner manual待完成。Commit/push/merge由Owner另行授权或在GitHub Desktop执行。

Owner试玩修订：M-001旧版本PASS；M-002间距/朝向反馈已批准小范围试用1.1m及逐位朝向。修订后的证据与最终回归边界见Validation Report，旧版全套PASS不自动代表本次快照。

M-003修复（2026-10-05）：Owner已接受后方朝向，报告三人停滞/队首离场和后方前移失败。修复范围为CustomerFlowController、只读NavigationLayoutAdapter.GetCompletePath、新CustomerRoutePlanner及P12相关测试/文档；不改P11移动政策或scene，不增加dependency。已先做默认真实scene可信RED，有限waypoints与逐位通道门禁后GREEN；原reviewer缩队Important可信RED→修复→局部复核关闭。当前focused为29 Play PASS和136相关Edit PASS；当前完整回归与Owner重测记录统一见Validation Report。

门口反馈修正（2026-10-06）：入口2×2按既有Project Design保持人物可通行。真实四格MoveTo先PASS，入口旁Grid `(2,0)` 柜台第二位入店可信RED；native path两阈值对照定位ArrivalDistance误加身体间距。小范围仅改CustomerFlowController.RouteClearance的一项表达式，P12 scene tests追加四格通行和三顾客排空case，并同步本Phase文档。未改scene/NavMesh烘焙、P11碰撞或生成点/出口点；原Engineering reviewer局部复核无阻断项。相关回归及当前完整Play统一记录Validation Report，最终完整Edit在视觉调整确定后集中执行，旧快照PASS不转移。

## M3拐点与匀速修复（2026-10-06，Owner已批准）

Owner明确“同意，你改吧”，批准4个runtime文件：NavigationActor提供非serialized、注册前选择的P12模式；NavMeshMovementDriver跟随owned complete path corners；NavigationWorld限制每substep不越过corner，继续全部guards；CustomerFlowController只对新P12顾客启用，并严格验证中间waypoint实际抵达。默认P11角色保留原模式。相关测试与本Phase现有文档同步，不改scene、NavMesh烘焙、半径/skin、Save或dependency。

- [x] 真实Unity RED：自动生成墙边停滞、截图估算布局离场重算耗尽、畅通路线减速。
- [x] 第一轮最小实现；截图估算反复前移与空路匀速2 PASS，自动生成2 FAIL，不能交付。
- [x] 修复native corner与完整skin不一致的局部停滞；任何corner简化必须从真实pose验证owned NavMesh与完整连续sweep，不能放松安全间距。单corner一次有界外侧候选，失败不循环。
- [x] 原真实场景全部GREEN；补mixed默认/steady角色、动态/静态阻挡、取消/disable/rebind、短目标、长帧等6项，共10 PASS。15/30/120fps五人补客case在本轮完整Play中执行。
- [x] focused Edit 213 PASS、完整Play 1174 PASS/0 FAIL/5 opt-in SKIP及独立Engineering/QA复核；P12 Integration40/40 PASS。精确结果统一写入Validation Report，完整Edit留待视觉调整确定后的Phase最终流程。
- [x] Owner原布局视觉验收：2026-10-07明确接受更新后的M1～M8全部完成，M3及M4～8现为Owner PASS；旧Decor重排要求按批准的新reset合同替代。未授权commit/push/merge。

独立review：Engineering本轮无Critical/Important阻断；QA靠家具final Facing Important已可信RED→最小修复→3项GREEN并复核关闭，并核对当前完整Play XML通过自动验收。任意家具布局可达性与Owner原图视觉不由自动结果推导。

## M6与窄路临时拐点修正（2026-10-06，Owner批准先复现再修复）

Owner确认移动整个收银柜台、Confirm、Done并恢复播放后仍卡住。Computer Use读取实际HUD：第5位Blocked，原因“前移：尚未抵达绕行拐点”；部分前客已抵达新队伍。修正仅在CustomerFlowController将临时waypoint额外Epsilon判定改为原ArrivalDistance；下一段仍从实际pose验证，保留全部P11 guards、有限segment/replan和最终到达/容量合同。现有scene test补满8人营业移动与真实家具边缘waypoint，不增加工具或dependency。RED/GREEN与相关回归见Validation Report；原图Owner重测Pending，旧完整测试不转移为本次完整验收。

## 图2～4空员工位规则修正（2026-10-06，Owner已批准）

Owner明确“允许空员工位排队，实际有人时避让”。真实近似布局先复现新slots为0/0/1，根因是Employee anchors与Pick-up两种role周围1.1m的过度排除，未证明路线搜索不足。仅修改CustomerFlowController的候选筛选和HUD：允许空Employee anchor，保留Pick-up Customer按代理直径/skin/epsilon/ArrivalDistance的安全空间；实际角色继续原路线避让与完整碰撞保护。CounterQueueService新增只读AvailableSlotCount，区分站位不足、FIFO恢复等待和真实移动失败，不删除超额顾客、不释放其容量。

现有两份P12 PlayMode测试补六项营业移动、真实占位拒绝与站位不足提示；新revision的slots和实际到达必须一致，避免旧Queued状态造成假通过。8项定向GREEN，直接P12/P11回归结果见Validation Report。同步本Phase spec/tests/plan/report/guide；无新工具、scene/Save/dependency迁移，无路线搜索扩张。1.1m排队间距保持，图1另行处理；Owner原图2～4重测Pending。

历史（场边回退前）：直接回归发现满队摆法的第八站位被家具与队伍围死，追加在CustomerFlowController生成候选时复用既有有限路线查询，要求入口能绕过前面虚拟slots到达；不改P11或CustomerRoutePlanner、不放宽半径/skin。当时贪心分支截断为七站位，满八人按既有E-018保留旧尾客原位置/assignment/容量，前客离场后继续排空，并验证7/8提示。最新有限回退已找到该摆法的另一条八站位链，见后续场边修正；旧结果保留历史，真正空间不足合同不变。

## 独立前移与装修重排（2026-10-06，Owner已批准）

Owner确认按限定范围修改：已有队伍前移、移动柜台后重排允许多人同时移动，安全距离不足可等待，保持FIFO/不超车/不重叠。只修改CounterQueueService（每entry独立Moving/完成generation）和CustomerFlowController（前移时对同队活动前客预测腾位，仍保留其目标slot；实际身体继续完整P11 guards；前客失败退休活动后客请求并保留容量）。新到店接纳与离场按钮门禁、匀速设置、1.1m间距、入口/空Employee/Pick-up规则、有限route/segment/replan/timeout不变；不改P11或CustomerRoutePlanner，无scene/prefab/Save/dependency migration。

现有CounterQueueTests补并发assignment与旧generation测试；两份已有PlayMode测试追加直队实际并发、3m重排Pause/disable恢复、真实弯队并发和实际Cancel后客停止。先可信RED，再最小实现；4项focused GREEN、137项相关Edit GREEN，最终相关Play结果见Report。同步现有Phase文档，无新诊断工具或测试子系统；Owner视觉重测与Phase最终全量验收仍独立保留。

直接回归发现后客未来目标与交叉路径会阻挡前客，修正仍在上述两份runtime内：FIFO目标保留优先级，提交前检查实际剩余路径冲突，以及后客到站是否堵住前客通道；等待时不提前修改assignment/generation。先检查交叉冲突，再允许同向原站位跟进。保留原移动timeout、有限重算和全部碰撞断言，未改P11。失败记录与最终fresh结果统一写入Report。

最终fresh直接回归：concurrent-advance-final-play.xml 83 PASS/0 FAIL/0 SKIP（Flow24、Scene28、P11 Movement31），concurrent-advance-final-edit.xml 137 PASS/0 FAIL/0 SKIP（P12 29、P10 108）；安全门禁拒绝时assignment/generation不变断言包含在后者。Owner仅需复测多人前移与装修重排手感；Phase完整验收、原图准确布局仍Pending，不自动commit/push/merge。

## 场边队形有限回退（2026-10-06，Owner批准“可以，先试试看”）

只改CounterQueuePlanner与CustomerFlowController：保留直/左/右和1.1m，分支无法满足容量时尝试其他方向，256候选预算内保留最长已验证链；正常第一分支足够则不改变队形。入口可达性改为只读当前prefix，随回退撤回虚拟占位。P11、CustomerRoutePlanner及运行中移动/并发/FIFO/失败保护不改，无scene/prefab/Save/dependency迁移。

现有CounterQueueTests补四朝向死路、有限预算/确定性及prefix撤回；现有scene test补满八人边缘真实重排。可信RED为四朝向各只三位但有六位替代分支，及原Grid(4,6)只七位。初次GREEN 14项domain、6项真实移动；该Grid摆法现找到八个可达站位并实际到达/排空，旧七站位记录保留历史。仅在现有测试测量Done+重建耗时，未新增诊断工具。最终直接回归及耗时见Validation Report，Owner只需复测场边队形与原图一视觉。

最终fresh直接回归：boundary-queue-final-play.xml 84 PASS/0 FAIL/0 SKIP（Flow24、Scene29、P11 Movement31）；boundary-queue-final-edit.xml 143 PASS/0 FAIL/0 SKIP（P12 35、P10 108）。该布局两次Done+重建约0.043/0.045秒，仅本机fixture测量。真正不足仍保留原位/FIFO/容量，Owner原图一复测Pending；无commit/push/merge。

## 边缘对齐与直段均匀分布（2026-10-07，Owner批准“好的，试试看”）

只修改CounterQueuePlanner和CustomerFlowController及已有测试/文档：1.1m变为最小间距，段起点与队首固定。直段不足下一步时最多8次寻找剩余安全边缘，使用owned路径实际endpoint并内留2cm；整段均匀分布且逐点重新验证，失败完整恢复。间距微调最多增加25%，短段需大幅拉开则保留原形。所有探测/候选/复验共享原256预算；P11半径、skin、路线搜索上限、并发/FIFO保护不变。非队首目标保留入口1.1m加到站误差空间，防止新队形堵后续生成。

四朝向边缘旧尾3.3m未用到4m安全边缘构成可信RED，原真实Cash Grid(0,5)的corner cell亦未利用。focused首次Play暴露默认第二位堵入口，补入口安全空间；三项原并发与两项朝向fixture改为实际新目标、间距、预期方向及原全部移动保护断言。首轮直接Play83 PASS/3 FAIL暴露咖啡柜台后方重排真实失败，短段1.1m拉至1.978m的可信RED后，限制25%微调并恢复该布局。另以可信RED确认满容量末段不应再拉长。已有五人跨墙边旧队伍的corner重排最终仍Blocked，列OPEN/NOT_FIXED，不以安全停下当作重排交付，也不计入最终选择suite。两个corner验收cases分别为空店五人入店/离场、三人重排后补到五人/离场，强化真实角落占位及各新目标到达。不放宽碰撞或变更FIFO；全局几何可达性与该五人旧基线尚未证明。

最终fresh直接Unity：edge-align-final-verified-play.xml 86 PASS/0 FAIL/0 SKIP（Flow24、Scene31、P11 Movement31），316.09秒；edge-align-final-edit.xml 152 PASS/0 FAIL/0 SKIP（P12 44、P10 108），4.92秒。明确不包含OPEN五人跨旧队伍探索case。Owner只需复测红色角落利用与间距手感；原图精确坐标仍未恢复，Phase未关闭。没有新增诊断工具、scene/prefab/Save/dependency迁移，不commit/push/merge。

## Owner批准修订：1.0m紧凑间距与身体入口保护（2026-10-07）

Owner确认“好的，改吧”。仅修改CounterQueuePlanner默认/下限1.0m和CustomerFlowController入口门禁：实际出生保护按双方radius＋skin＋epsilon（0.911m），规划目标另加arrival（0.991m）。原0.45m半径、0.08m到站容差、碰撞/路线/入口等待slot排除/exit/FIFO/并发/有限timeout不变。已有tests补默认第三位位置及八人完整生命周期，并适配旧间距/绕行fixture；同步living spec/test cases/report/guide/roadmap。无scene/prefab/Save/dependency迁移，无commit/push/merge。测试结果以Validation Report最新段落为准；旧86/152为此前快照，不自动继承。1～2秒停顿与五人跨墙边屏障重排另行记录，不能因本次间距验证通过而宣称修复。
## Owner批准修订：装修结束仅reset顾客（2026-10-07）

Owner确认“好的，就按照你的方案来吧”，要求员工独立、不随Decor消失。本次bounded修改CustomerFlowController：消费已确认修改的正常完成标记、复用RemoveVisit清理自己拥有的visits/请求/token、重建队伍并抽新interval，保留非顾客actors与P11移动合同。DecorationModeController只新增只读完成version和小型会话提交标记，覆盖家具、mounted/Pick-up、wall与surface Confirm/Store，shutdown不发布。已有P12 native tests先RED后实现；旧真实Decor重排fixtures改为reset/新客合同，普通revision/disable/repair回归仍保留。同步Game Design/spec/tests/guide/report/roadmap；无资源/Save/dependency migration，无新工具，无commit/push/merge。员工任务/订单处理不在P12范围。最终证据以Validation Report最新段落为准，旧56/45、86/152为历史快照。

执行结果：runtime上述两文件完成，三条新增native cases和既有helper验证新合同。真实RED2失败、GREEN2通过；直接Play首轮62通过/2等待fixture超时，逐位保留原deadline后focused6通过，两份去重68项全部有PASS；P12 Edit45通过。原五人墙边屏障布局清旧客/新五人入队与离场通过，属Decor合同替代，不称导航修复。独立actor替身保留通过，P13任务不在本轮；Owner视觉Pending，Phase不关闭。

最新Owner验收（2026-10-07）：调整后的M1～M8全部Owner PASS，8/8 Completed，取代上文历史视觉Pending。人工验收相关checkbox已完成；未执行的异常注入或当前代码Phase最终完整回归不因此补记PASS。无代码修改、Unity rerun或commit/push/merge。

## Owner批准修订：并行入店（2026-10-07）

Owner确认“好的，你开始开发把”。bounded范围仅CustomerFlowController与CounterQueueService：去掉Entering/Advancing/Exiting全局接纳锁，稳定FIFO尾位可预留，入店前复用剩余owned路线冲突检查，已有顾客优先；新入店目标不能锁住更早任务。保留一次一位离场按钮、1～5秒clock、入口/尾部实际身体保护、全部P11参数/连续碰撞、Decor reset、容量释放及有限路径/重试。两份已有domain/Flow测试补RED→GREEN，真实保存场景及P12直接回归合并执行，不新增诊断工具、scene资源或dependency。既有8/8 Owner PASS保留为此前快照，新行为只需补M1/M3/M4并发观察，结果见Report。

执行完成：runtime上述两文件；新增六项真实角色Flow和两项domain回归，既有native helper显式限定目标入店人数。可信RED覆盖旧全局离场/前移接纳拒绝和Moving尾位锁；原两位入店fixture实为目标挡住斜向前客路线，修正fixture并单独保留等待恢复保护，未放宽碰撞。focused6 GREEN；P12 Play65、P11 Play31、P12/P10 Edit155全部0 FAIL/0 SKIP。测试Editor已退出，无新工具/资源迁移/commit/push/merge；只待新并发视觉观察，不重做M1～M8，不进入P13。

## Owner批准修订：桌角持续停顿（2026-10-07）

Owner确认“好的，你开始修复把”，bounded范围为NavMeshMovementDriver与现有SceneTests。实际帧时间/1000fps两项可信RED定位原角点/连接与完整skin不一致、原修正范围触发过晚和失败标记锁死。单次最小修改：提前校验距raw corner不超过原ArrivalDistance的外侧候选；仅安全连接成功才标记调整，失败保留后续重试。原最终目标/到站容差/身体安全与速度参数不变，无新工具或资源迁移。

- [x] 五种帧时间默认五位自动入队、停顿检查与FIFO排空：focused5 PASS。
- [x] 原P12 Flow30/Scene35、新Scene5与P11 Movement31直接Play：101 PASS/0 FAIL/0 SKIP。
- [x] spec/test cases/report/guide/roadmap同步；仅局部修复，不重新开启全部M1～M8或后续Phase。
- [x] Owner于2026-10-08确认“拐弯的问题修改好了”；此前并发观察Pending继续保留。

本轮EditMode/全项目suite/soak/Player/手机NOT_RUN；此前155项Edit证据仍为历史快照。无commit/push/merge。

## Owner批准修订：同弯连续入店（2026-10-08）

Owner确认“开始修吧”。bounded范围为CustomerFlowController与两份已有PlayMode测试：默认第四位仍绕桌角、第五位到店期满的真实RED先复现；仅活动Entering前客可用顺序共享弯道识别绕过空间线段误判。保持真实内部横穿/对向拒绝、新尾目标保护、实际World guards与原1～5秒clock；不改上轮桌角移动、员工、容量或离场按钮规则，不增加诊断工具。

- [x] 默认保存场景可信RED：期满仍4位，第四位Entering。
- [x] 最小门禁修正；真实同时入店/排空、横穿拒绝和原并发保护focused8 PASS。
- [x] 相关直接Play103 PASS；末端对齐条件收紧后定向13 PASS，源码时点与最终结果同步到Validation Report。
- [ ] Owner仅观察新同弯入店，无需重做原M1～M8。

## Owner批准修订：队尾临时占位（2026-10-08）

Owner确认第七位仍在走、稍后第八位可以进来，并批准“好的，就这样调整”。bounded范围为CustomerFlowController及两份已有PlayMode测试：只对自有活动Entering前客预测腾位，仍检查其最终slot不占新尾位；完整剩余路线只允许向外腾空的临时前缀，不能再穿回新队尾。保留静止/外部人物占用拒绝、真实交叉/对向、实际碰撞和1～5秒clock，不改员工或上轮桌角执行。

- [x] 真实默认六Queued＋第七移动的新接纳RED，以及直线临时尾位RED：2可信FAIL。
- [x] 最小门禁修正及原保护focused11 PASS。
- [x] 最终相关直接Play105 PASS/0 FAIL/0 SKIP（Flow32/Scene42/P11 Movement31）；runtime此后无修改，结果与Report/Guide/Roadmap同步。
- [x] 同一直线fixture补强“仍在接近未来尾位→拒绝且无新角色/容量”保护，末次单独1 PASS；无生产修改，不重复累计case数量。
- [ ] Owner仅观察默认七到八人的并行入场。

## Owner批准修订：到期顾客保留与安全顺序跟随（2026-10-09）

Owner确认“可以，开始吧”。bounded范围为CustomerFlowController、CustomerSpawnClock与三份现有测试及开发文档；不新增工具/资源/dependency，不更改身体保护、员工或后续phase。

- [x] 默认Play不操作的1秒/2秒诊断确认未来尾位门禁与失败重抽计时两个原因；诊断PASS只证明复现，不代表问题修复。
- [x] Clock四项可信RED；真实Play四项可信RED，包括入口清空后仍重等、接近未来尾位的安全跟随遭拒、两种连续进客计时刷新。
- [x] 到期仅保留一位，成功接纳后启动下一轮；暂停不生成，满员/装修/组件停用不积压。安全共享走廊允许末端两段向前分流，仍验证前客先腾位的时间余量、最终目标、真实横穿和完整身体guards。
- [x] focused8 PASS；P12 Edit49 PASS。原“接近未来尾位一律拒绝”已由安全跟随真实回归替代，其余真实占用/交叉/对向保护保持。
- [ ] 最终相关直接Play回归与文档结果同步。
- [ ] Owner仅观察默认六到八位连续进客及倒计时；此前M1～M8的Owner PASS保留，不重做整套，不进入P13/P14。

修复前直接回归结果：110项中109 PASS/1 FAIL/0 SKIP（Flow34/35、Scene44/44、P11 Movement31/31）。唯一soak失败在20fps前移时完整路径被原生Agent.SetPath拒绝；关闭AutoSpawn、六位逐个入队后再排空同样失败，隔离当前计时/并发规则。测试内原地重绑定后同路径可提交、Transform位移0；临时probe代码已删除，仅保留无并发20fps行为RED。当时追加P11最小修复尚待批准，后续授权与GREEN见下节。

## Owner批准追加：20fps原生路径提交恢复（2026-10-09）

Owner指示“先解决fps的问题”，并暂停create PR。本轮范围：NavMeshMovementDriver、两份现有P12 PlayMode测试和开发文档；不增加诊断工具、资源、dependency或Save迁移，不进入P13/P14。

- [x] 两项原行为case在当前修复前真实复现：fps-rebind-red.xml，0 PASS/2可信FAIL/0 SKIP。
- [x] 仅完整路径SetPath被拒后重验真实起点与碰撞，原地重新绑定Agent、重新查询并重试一次；没有Warp/Transform写入，保留全部原碰撞与速度参数。
- [x] 无并发20fps排空加入FIFO身份、启动请求原位、连续身体间距和逐帧速度上限；真实场景加入20fps cash-only case；1x/2x clock矩阵加入20fps。
- [x] 原失败focused 2 PASS/0 FAIL，完整30游戏分钟循环通过；受影响直接Play112 PASS/0 FAIL/0 SKIP（P11 Movement31、Flow36、Scene45），P11/P12 Edit126 PASS/0 FAIL/0 SKIP（77＋49）；runtime identity另行2 PASS。
- [ ] Owner仅补默认连续六到八位来客与队首离开后的前移观察。既有M1～M8与桌角人工PASS保留为旧快照，不重做整套。
- [ ] 全项目完整suite/Player/真机与最终Phase closeout按后续范围决定；本轮NOT_RUN，发布暂停，无commit/push/PR/merge。
