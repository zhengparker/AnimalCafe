# Phase 12 Validation Report

日期：2026-10-09（America/Toronto）。状态：**P12_20FPS_NAVIGATION_DIRECT_REGRESSION_PASS**。Owner已批准先解决20fps导航问题；生产代码加入校验真实起点后的有界原地Agent重绑定。受影响直接Play **112 PASS/0 FAIL/0 SKIP**，P11/P12 Edit **126 PASS/0 FAIL/0 SKIP**；两项原失败用例、真实场景20fps及完整30游戏分钟循环均通过，runtime identity另行2 PASS。此前直接Play109 PASS/1 FAIL与无并发20fps排空FAIL保留为修改前证据；M1～M8的8/8 Owner PASS仍为历史快照，本轮只补连续来客/前移的视觉观察。

工作区 `.worktrees/phase-12-customer-queue`；branch `codex/phase-12-customer-queue`；base `e53bd03`。main 的既有修改保留。无 dependency/Save migration。Owner于2026-10-09重新授权提交、push branch并创建合并到main的PR；未授权merge或清理branch/worktree。以下“未commit/push/PR”描述各测试快照，实际发布结果以GitHub PR记录为准。

## 本轮：20fps原生路径提交恢复（2026-10-09，Owner已批准）

- 测试期间Owner指示“先不急create pr”“先解决fps的问题”，当时暂停发布；修复后已重新授权branch/PR发布。本轮只改NavMeshMovementDriver、两份现有P12 PlayMode测试与开发文档，无新工具/scene/prefab/dependency。
- `fps-rebind-red.xml` **0 PASS/2可信FAIL/0 SKIP**：原30游戏分钟持续进出与六位逐个入队后排空，均在前移时进入Blocked。关闭AutoSpawn也可复现，独立于到店计时或并发入场规则。
- 完整NavMesh查询成功而Agent.SetPath返回false，是已确认的故障位置；原生内部绑定失配为诊断推断，不声明已确认Unity引擎根因。仅在完整路径提交被拒后重验活动状态、layout、真实起点与原World碰撞保护，原地disable/enable Agent，经原TryBind再查询完整路径并重试一次。无Warp/Transform写入，不变更半径、skin、容差、速度或到店规则；仍失败则沿原失败处理停止。
- `fps-rebind-focused.xml` **2 PASS/0 FAIL/0 SKIP**，30.96秒：六位20fps排空检查连续0.909m身体分离、每帧位移上限、owned NavMesh、真实离场与FIFO；30游戏分钟循环实际完成，记录720条released reservation历史，最终容量归零。仅证明有限测试，不保证所有布局或设备。
- 新增真实场景20fps cash-only五人离场补客case，复用现有家具skin、连续角色分离及移动上限检查；原时钟15/30/60/120fps覆盖中加入20fps。
- `fps-rebind-direct-play.xml` **112 PASS/0 FAIL/0 SKIP**，344.31秒：P11 Movement31、P12 Flow36、Scene45全部通过。原soak完整执行，记录714条released reservation历史、最终容量归零；默认1秒/2秒连续来客、15/20/30/120fps真实场景、墙/窄路、桌角、并发、暂停、disable、Decor reset与容量/FIFO回归通过。runtime identity所在namespace未被该轮filter覆盖，另行执行，不将其计入112项。
- `fps-rebind-direct-edit.xml` **126 PASS/0 FAIL/0 SKIP**，6.59秒：P11 77、P12 49，覆盖碰撞、layout、service、resume、角色资产、clock和queue domain。Player/真机/全项目完整suite本轮NOT_RUN；无commit/push/PR/merge。测试后只补人工默认六到八位连续进客，以及队首离开后的前移观察，不重做M1～M8。
- `fps-rebind-identity-play.xml` **2 PASS/0 FAIL/0 SKIP**，0.09秒：Shiba/Westie独立且不可重复初始化的visit身份通过。测试Editor已退出，当前新增源码/文档范围diff-check通过；既有MainCafe空m_Name行尾空格不在本轮修改范围，不声明整个branch无空格问题。EditMode SceneBuilder自动重生成验证场景的fileID已恢复到测试前staging版本，保留原P12场景内容；没有本轮额外tracked资源改动。旧staging与本地outputs仍保留，未commit/push/创建PR。

## 修改前快照：保留一个到期顾客并允许安全同向跟随（2026-10-09，Owner已批准）

- 修改仅CustomerFlowController、CustomerSpawnClock和三份现有测试，现有文档同步。无新工具、scene/prefab/dependency/Save修改。入口/尾位/路线临时受阻不重抽间隔；只保留一个到期机会，成功接纳后才抽下一段1～5秒。零游戏时间不生成；满员、物理slots用尽、装修、真实movement Blocked或组件停用取消等待，恢复后重计，不积压补发。
- 安全同向Entering前客的未来尾位通行按先后顺序验证：共享走廊须匹配，末端只允许两段向前且不回头的分流；最终slot不能占新尾位，预估腾位与后客到达之间还留一个clearance的时间余量。真实内部横穿、对向、未匹配路线仍拒绝。实际身体、家具skin、0.45m半径、1.2m/s速度、0.08m到站容差、容量/FIFO及原Decor reset保持。
- `pending-arrival-clock-red.xml` **10 PASS/4可信FAIL/0 SKIP**；旧clock到期后立即变回1或2秒，四项新规则断言失败。`pending-arrival-play-red.xml` **0 PASS/4可信FAIL/0 SKIP**：入口清空后没有立即生成；直线安全跟随遭拒；默认1秒/2秒连续进客在人数不变时错误重启计时。
- 首次实现后focused **6 PASS/2 FAIL**；clock与直线跟随已通过，第八位仍被末段单一分流限制。真实日志显示新队尾路线为避开第七位目标增加了一个拐点；补有限末段匹配和腾位时间余量后，`pending-arrival-focused-play-green.xml` **8 PASS/0 FAIL/0 SKIP**，10.25秒。
- 最终测试移除本轮临时路径诊断，保留行为断言。连续1秒/2秒自动入场各自验证第七/第八位出生时前客仍Entering、距其目标超过2m；计时不因失败刷新，至少12帧实际并行，八位真实Queued、FIFO排空、三项Used=0，并保持每帧家具skin、位移上限与连续0.909m身体分离。
- `pending-arrival-direct-edit.xml` **49 PASS/0 FAIL/0 SKIP**，4.14秒：P12全部domain/Editor回归，包括新clock边界、暂停、满员/装修取消机会、成功后新间隔、长帧不补发及原队伍/容量生命周期。
- `pending-arrival-direct-play.xml` **109 PASS/1 FAIL/0 SKIP**，304.66秒：Scene44全部通过、Flow34通过/1失败、P11 Movement31全部通过。唯一失败`ThirtyGameMinuteControlledLifecycleSoakKeepsCapacityBounded`为前移IncompletePath；soak在早期即失败，不能记作30分钟完成，也不能把整个110项记为PASS。
- `pending-arrival-soak-isolation.xml` **0 PASS/2可信FAIL/0 SKIP**：原soak重复失败，新增`QueueDrainsAtTwentyFpsWithoutAnyConcurrentAdmission`也失败。后者关闭AutoSpawn，六位逐个入队后才离场，全程无并发入场与待到期机会，隔离了当前计时与入场判定。诊断确认完整NavMesh路径查询成功但原生Agent.SetPath返回false；仅改用Agent.CalculatePath仍被拒绝。测试内原地重新绑定后同路径SetPath返回true、Transform位移为0（`pending-arrival-native-rebind-probe.xml`）；该probe仍保留失败断言，不冒充修复GREEN。
- 临时诊断/Agent重绑定实验代码已移除，仅保留无并发20fps行为回归；runtime自focused8 GREEN后未再改动。下一步待Owner确认追加P11 NavMeshMovementDriver的有界、原地重绑定修复，之后重跑受影响回归再交人工观察。当前不要求Owner先测，也不重做M1～M8。全项目完整suite/Player/真机本轮NOT_RUN，无commit/push/merge。

## 此前：不操作默认布局时六位之后计时刷新（2026-10-09，诊断）

- 保存的真实Unity场景持续AutoSpawn，不挪家具、不手动接纳；测试仅将随机源固定为范围内的1秒和2秒，以便重复核对。沿用实际身体连续分离、家具skin、位移上限检查并等到八位Queued。
- `unattended-arrival-gates.xml`：两项诊断 **2 PASS/0 FAIL/0 SKIP**，总测试用时约6.37秒。这里的PASS只说明诊断场景运行到八位且运动guards通过；日志明确证明问题仍存在，不代表修复PASS。1秒case在6人不增加时重复刷新5次、7人时4次；2秒case分别刷新2次、2次。
- 明确失败点：`CanStartAdmission`将新队尾目标与前客完整剩余路径作空间比较。日志中的第六位尚在绕台，`targetClear=False, vacates=False`；同一时刻部分路线已经符合`follows=True`，但目标检查先返回false，仍无法入场。当前并非全局Arrived锁，而是未来站位不能阻挡前客的保守门禁产生类似效果。
- clock在尝试生成之前已抽取下一段1～5秒，TrySpawn失败不会保留到期机会；因此“人数不变而倒计时刷新”可连续发生。入口被占等基础门禁另会取消计时，恢复后重新抽取间隔，不能与路线拒绝混为一谈。
- 上轮第七/第八位fixture只覆盖前客已靠近并向外腾位的选定时刻，没有覆盖从Play开始持续自动进客的完整尝试序列。当前仅现有SceneTests加诊断记录，本轮未改runtime。下一步需确认安全同向跟随的未来站位判定与保留一个到期机会的计时方案，再补行为回归；不降低碰撞保护，不积压批量补发。

## 本轮：队尾临时占位允许安全入店（2026-10-08，Owner已批准）

- Owner确认截图里的第七位仍在走，稍后第八位可入场；HUD“队尾站位暂被占用”、0秒来自TailClear门禁关闭时clock取消本次计时，并非容量满。真实默认场景在六位Queued、第七位经过未来第八个目标附近时稳定复现旧接纳拒绝；不是同弯路线误判的重复RED。
- runtime仅 `CustomerFlowController.cs`：队尾实际检查只对本Flow拥有、启用且有活动请求的Entering前客预测腾位，其最终slot必须在新尾位占用范围外。入店前仍检查完整剩余owned路线；只排除当前在新尾位clearance内、各段向外腾空的临时前缀，退出后剩余路线必须远离新尾位。真实交叉/对向、新尾位后续会堵前客、静止/Queued/外部人物实际占位仍拒绝。所有身体始终参加P11连续guards；无半径/skin/速度/到站容差、容量/FIFO、Decor reset、1～5秒clock或离场规则变化，无新工具/资源/dependency/Save修改。
- 两份已有PlayMode测试补真实默认第七/第八位，以及直线活动前客经过未来尾位的case。到店期满或TrySpawn须在前客仍Entering时成功；至少12帧双方实际位移，全部真实Queued、FIFO排空、Used归零，并保留原家具skin、速度上限和连续0.909m身体分离断言。静止外部占位、横穿、对向、新目标堵前客、暂停/disable等已有保护继续运行。
- `temporary-tail-red.xml` **0 PASS/2可信FAIL/0 SKIP**，3.27秒：直线接纳返回false；默认期满应8位却仍7位，HUD为队尾暂被占用、第七位仍Entering。最小修复后 `temporary-tail-focused-green.xml` **11 PASS/0 FAIL/0 SKIP**，10.42秒，包括两项新case、此前同弯native和八项相关保护。
- `temporary-tail-direct-play.xml` **105 PASS/0 FAIL/0 SKIP**，312.09秒：P12 Flow32、保存Scene42、P11 Movement31。覆盖默认八人、多个柜台/场边布局、满队/Decor reset/真实新客批次、不同帧率桌角持续移动、同向并发及完整碰撞/容量回归。focused11包含在105内，不重复相加；runtime此后无修改。完整suite/Edit/soak/Player/手机本轮NOT_RUN。
- 105项之后只强化同一个直线fixture：前客还在接近未来尾位时必须拒绝，角色/容量不增加，之后安全腾位才接受。`temporary-tail-approach-guard.xml` **1 PASS/0 FAIL/0 SKIP**；没有生产代码变化，不算第106个不同case，也未再次跑整105项。测试Editor已退出，局部源码/文档行尾检查通过。
- 本轮只需Owner重新Play默认布局，观察七到八人是否可以在安全条件下同时行走；随机间隔刚好落在前客到站之后不算失败。不重做M1～M8，不把此次自动PASS记为Owner PASS；不进入P13/P14，无commit/push/merge。

## 此前：同弯连续入店（2026-10-08，Owner已批准）

- 默认场景第四位还在收银桌左侧Entering，下一次到店期满却不能创建第五位。真实路线日志确认两人使用相同绕台弯道；旧RoutesConflict把不同方向的路径腿逐对比较，不区分按顺序跟随，误判为通道冲突。clock原本就会在尝试前抽下一interval，因此HUD再次显示倒计时不代表前客已到站。
- runtime仅 `CustomerFlowController.cs`：仅活动Entering前客可识别顺序共享弯道，前客须已在新路线前方至少原clearance；共同腿同向、顺序匹配，弯道末端须在原ArrivalDistance＋Epsilon容差内对齐，之后只允许同向最后一段分流。真实内部横穿、对向、入口/队尾实际占用、新队尾堵前客仍等待；Exiting/Advancing门禁、全部实际身体guards、速度/半径/skin、容量/FIFO、Decor reset及1～5秒clock保持。没有新工具/scene/prefab/dependency/Save变化。
- 现有SceneTests补 `NativeDefaultFifthArrivalFollowsFourthAroundCounterBeforeItQueues`：前三位Queued，第四位实际绕角时注入一秒clock到期；第五位必须在第四位Queued前成功入场，至少12帧两位Entering实际同时位移，全部真实到站、FIFO排空及三项Used归零。原速度上限、家具skin和连续0.909m身体分离断言保留。现有FlowTests补真实横穿入店拒绝、无预留、前客完成后恢复排空。
- `follow-entry-native-red.xml` **0 PASS/1可信FAIL/0 SKIP**，3.12秒：期满人数应为5，实际4，第四位仍Entering。`follow-entry-probe.xml`再次复现相同失败并记录真实路线；临时日志已从源码移除。`follow-entry-focused-green.xml` **8 PASS/0 FAIL/0 SKIP**，5.85秒：新native跟随、横穿保护及六项原并发保护。
- `follow-entry-direct-play.xml` **103 PASS/0 FAIL/0 SKIP**，310.19秒：P12 Flow31、保存Scene41、P11 Movement31，涵盖不同布局/满队/Decor reset/帧率/移动与碰撞回归。代码复核后仅追加“共同弯道末端必须对齐”的保守条件；最终源码 `follow-entry-final-focused.xml` **13 PASS/0 FAIL/0 SKIP**，77.76秒：上面8项＋五种帧时间桌角连续移动检查。13项包含重复case，不与103相加；末端条件收紧后没有再次跑全103项。
- 最新源码上述13项后无生产修改，测试Editor已退出。Owner本日确认“拐弯的问题修改好了”，上轮桌角视觉记Owner PASS；新同弯接纳仅需默认布局第四/第五位观察，不重做M1～M8。全项目suite/Edit/soak/Player/手机本轮NOT_RUN，无commit/push/merge，不进入P13/P14。

## 此前：桌角停顿修复（2026-10-07，Owner已批准；2026-10-08 Owner视觉PASS）

- 用户复现“Play后第四位在桌角卡住1～2秒”。真实Editor帧时间确认第四、第五位实际低速约3游戏秒后才重试；另一侧拐角在1000fps fixture最终Blocked。原因：原NavMesh拐点/路径腿与完整skin不一致；拐点修正仅在0.08m范围内尝试一次，首次失败也标记已处理。原始诊断见 `outputs/phase12/corner-pause-diagnosis-20261007.md`。这是本次具体停顿的定位，不把所有历史截图停顿都算作已证明同因。
- runtime仅 `NavMeshMovementDriver.cs`：提前从真实pose检查最近实体外侧的一个有界候选，默认距raw corner为0.079m（ArrivalDistance减Epsilon）；原owned NavMesh和完整静态/动态连接检查通过才采用并标记调整成功，失败仍可随真实pose/身体变化重试。实际位置仍逐步通过World guards，最终destination、0.08m Arrived、0.45m半径、0.01m skin、速度、timeout、容量、FIFO、Decor reset均未变。默认P11 native模式不使用此corner处理；无scene/prefab/Save/dependency/工具变化。
- 已有SceneTests新增五项 `NativeDefaultCornerArrivalsContinue...`，覆盖15/60/240/1000fps及实际Editor帧时间。一秒注入到店间隔填五位、FIFO排空、容量Used归零；保留每帧速度上限/家具skin/连续身体分离。在无近邻身体挡路、远离段目标时，不跳过贴家具区域；速度低于MaxSpeed的25%不得连续超过0.2游戏秒，检测停住/严重慢行。原空旷90%匀速断言保持，不宣称每帧严格等位移。
- TDD `corner-fix-red-play.xml` **0 PASS/2可信FAIL/0 SKIP**，23.60秒：实际帧时间在左桌角、1000fps在另一侧均触发持续低速断言。单次最小修复后 `corner-fix-focused-green-play.xml` **5 PASS/0 FAIL/0 SKIP**，81.44秒。
- `corner-fix-direct-play.xml` **101 PASS/0 FAIL/0 SKIP**，321.89秒：P12 Flow30、保存场景40（含五项新case）、P11 Movement31。涵盖其它柜台/墙边布局、满队/角落、连续并发、暂停/disable/rebind/取消、默认与匀速混合、墙/静止身体安全拒绝、短最终目标/Facing，以及全部原连续碰撞和容量断言；focused五项已包含在101中，不重复相加。生产代码从focused GREEN后没有追加修改。
- 本轮没有新增EditMode逻辑，不重复历史P12/P10 Edit155；全项目suite、30分钟soak、Player/手机、Owner新增视觉观察仍NOT_RUN/Pending。原M1～M8的Owner PASS保留为此前快照，不要求重测整套；无commit/push/merge，不进入P13。

## 此前：允许安全并行入店（2026-10-07）

- Owner批准方案并确认“好的，你开始开发把”。runtime仅修改`CustomerFlowController.cs`和`CounterQueueService.cs`：移除全局Entering/Advancing/Exiting接纳锁；稳定FIFO目标允许预留独立尾位，尚未分配的旧slot和未离位的exiting head仍锁住；入口、尾部身体安全和容量检查保持。
- reserve/create前复用有限路线规划、剩余owned路线交叉/对向检查和目标通道检查，已有任务优先。连续入店中的活动前客可预测腾位，仍保留其目标；新客目标不能锁住更早入店/前移/离场者。P11完整连续碰撞仍检查所有实际角色，身体不从World移除。没有P11生产代码、scene/prefab、Save、dependency或诊断工具变化。
- `concurrent-admission-red-play.xml` **1 PASS/3 FAIL/0 SKIP**，1.33秒：离场＋入店和前移＋入店被旧全局门禁拒绝，是两项可信产品RED；两位入店原fixture的新尾目标挡住前客斜向路线，后来确认属于fixture错误，不当作产品RED。原进出相向保护PASS。`concurrent-admission-red-edit.xml` **1 PASS/1可信FAIL/0 SKIP**，0.19秒：已稳定尾位仍因前客Moving而不能预留；未离位slot保护PASS。
- `concurrent-admission-green-play.xml` **3 PASS/1 FAIL/0 SKIP**，2.09秒；`concurrent-admission-final-green-play.xml` **3 PASS/1 FAIL/0 SKIP**，1.94秒。两次均只剩同一原入店fixture无重叠，不能称全部GREEN。已有test临时日志`concurrent-admission-entry-probe.xml/log`确认新目标(0,0,1)距旧斜向剩余路线约0.71m，targetClear=false，conflict=false；正确保护拒绝停车，未放宽它。
- 保留原斜向fixture为“新尾目标会挡前客→拒绝且不占容量→前客到站后恢复”回归；另用(-6,0,0)安全入口方向验证真正可并发的两位入店。移除临时probe源码，无新增诊断工具。`concurrent-admission-corrected-green-play.xml` **6 PASS/0 FAIL/0 SKIP**，2.88秒：三类至少12帧双方实际位移、原0.909m连续身体分离/速度上限、两类等待恢复、暂停/disable身份/pose/token保留、实际Queued及FIFO排空/容量归零。
- 既有native auto fixture显式停止在其本轮目标人数；离场者仍计Total，但不占新入队quota。每位deadline、真实站位、家具skin、连续碰撞与FIFO保持，不借用已取消的生产全局移动锁来凑固定人数。
- `concurrent-admission-direct-play.xml` **65 PASS/0 FAIL/0 SKIP**，237.73秒：P12 Flow30、真实保存Scene35，包括默认第三位/八人、角落、旧布局绕行、帧率/连续身体/家具skin、FIFO、Decor reset/非顾客保留与六项新并发cases。首个命令的P11 namespace写为Phase11Integration而实际为Phase11，因此该报告只含P12，不冒充P11通过。
- 随后正确filter的`concurrent-admission-p11-play.xml` **31 PASS/0 FAIL/0 SKIP**，1.92秒：原P11真实移动与碰撞回归。两份直接Play合计96个不同cases均PASS，不包含重复focused6，也不是一次96项suite。生产代码从focused6 GREEN起没有追加修改。
- `concurrent-admission-direct-edit.xml` **155 PASS/0 FAIL/0 SKIP**，5.19秒：P12 47、P10 108。稳定尾位前客Moving时可预留、旧generation拒绝、未离位slot保护，以及原clock/queue/容量/rollback/resize/session生命周期与scene-owner回归通过。
- clock源码和1～5游戏秒映射保持，路线冲突在接纳尝试时拒绝、不占容量，再按后续随机interval重试，不积压补发。离场按钮仍一次一位，不新增多位离場任务。Decor reset与非顾客生命周期保持。
- 本轮直接相关自动验收PASS，测试Editor已退出。scoped源码/文档行尾空格检查与tracked文档diff-check通过；既有MainCafe.unity空m_Name行尾空格未处理，不宣称全局whitespace PASS。全项目完整suite/长时soak/Player/真机本轮NOT_RUN，无commit/push/merge。此前M1～M8的8/8 Owner PASS保留为修改前快照，本次只追加M1/M3/M4的并发观察Pending；不重复整套、不进入P13。

## 此前：Owner人工验收完成（2026-10-07，Decor reset版）

Owner在更新M1～M8矩阵后明确回复“好的，所有都完成了可以把所有的都mark 完成”。据此记录M-001～M-008全部**Owner PASS，8/8，Completed**；具体步骤与逐项结果见[Beginner Guide](Phase12_Beginner_Guide.md)。没有声称这些是Codex Computer Use结果。M7-B条件重试分支没有单独触发证据，不额外认定该分支已运行；员工任务/P13与真机仍不属于本轮已验收范围。

本次只更新验收文档，无runtime/scene/test代码修改，无Unity rerun。当前Owner视觉/交互验收不再Pending；后文历次“Owner待复测/NOT_RUN”为当时历史状态，以此节为最新。此前1～2秒停顿的根因仍UNKNOWN，不把Owner整体接受改写成已定位修复。Phase最终完整回归、必要修订复核与版本控制分开记录。

## 本轮：已确认装修完成后仅reset顾客

- runtime仅修改`DecorationModeController.cs`和`CustomerFlowController.cs`。Decor记录成功Confirm/Store/表面修改，只在正常Done完成后递增只读version；顾客控制器消费一次，并复用RemoveVisit取消自有请求、注销/销毁自有顾客、释放三项容量，重建队伍，重新抽完整1～5游戏秒interval。保留原Capacity对象与递增visit ID；没有清空NavigationWorld actors。无修改/取消未确认Preview/被Preview拦住的Done/组件shutdown不发布完成标记；非Decor revision、Flow disable/enable与repair仍保留旧客、按原规则恢复。
- 已确认Done即使布局不可营业也清旧客；现有readiness和时间门禁阻止新客生成，未绕过营业检查。未改P11移动、碰撞、半径0.45m、skin0.01m、epsilon0.001m、到站容差0.08m、FIFO、路线/重试上限或1.0m队距。没有新scene/prefab、dependency、Save或诊断工具。
- `decor-reset-native-red.xml` **0 PASS/2可信FAIL/0 SKIP**，1.84秒：旧行为在正常Done后仍有三位顾客，失效营业布局后仍有一位；失败发生于旧批次未清理断言。`decor-reset-native-green.xml` **2 PASS/0 FAIL/0 SKIP**，2.27秒：两轮真实Confirm/Done、无修改/取消/未确认Preview、重复Done、完整注入两秒interval、新visit ID、容量与最终离场通过。
- 独立非顾客NavigationActor刻意挂在Flow下面；reset后同一instance/ID、注册和实际pose保留。它是员工生命周期隔离替身，**不代表P13 Employee任务/工作状态实现或验收**。不可营业case通过domain移除Coffee，再发布确认布局并正常Done；不是Computer Use店内Store手势证据。
- `decor-reset-final-play.xml` **62 PASS/2 FAIL/0 SKIP**，222.20秒：P12 Flow24、Scene34与六项原有Phase7 Decor操作。两项满八人新批次fixture把八次倒计时和逐次入店合在原60游戏秒等待里，截断时第七位仍正常前进，尚未Blocked；不能称为产品卡住或本轮全绿。
- fixture改为逐位等待自动倒计时/真实到达，各位仍受原60游戏秒/90真实秒deadline；身体连续0.909m分离、家具skin、速度上限、实际站位、FIFO排空断言未放宽。新增一条原已知五人墙边屏障布局验收，不增加测试工具。生产代码自首次GREEN后没有追加修改。
- `decor-reset-final-focused-play.xml` **6 PASS/0 FAIL/0 SKIP**，27.76秒：修正上述两项满八人等待；五位旧客Cash真实Confirm/Done移至Grid(0,5)后清空、五位新客实际入队/角落到达/FIFO排空；三项Phase6确认保留、连续Store、Exit/Disable/Destroy幂等回归。与首轮62 PASS按fullname去重后，**68个不同直接相关Play cases均有PASS（P12 59、原Decor 9）**；不是一次68/68单报告全绿suite。
- `decor-reset-final-edit.xml` **45 PASS/0 FAIL/0 SKIP**，4.32秒：P12 domain、容量/rollback、queue generations、随机clock、session生命周期与scene-owner基础回归。
- 旧五人跨墙边屏障的**Decor重排要求由本次reset替代（SUPERSEDED_FOR_DECOR）**，新合同该布局PASS；旧顾客保留时的绕行能力并未修复/证明。1～2秒停顿仍UNKNOWN。原图精确坐标、间距手感和玩家交互仍待Owner；全项目Edit/Play、P11独立suite、Player/真机、长时间soak、P13任务本轮NOT_RUN。测试Editor已退出，无commit/push/merge，Phase不关闭。
- Owner仅需等三～五客→Decor移动收银柜台→Confirm→Done：旧客清空，1～5游戏秒后新客入队且可离场。另测不修改Done、取消Preview后Done保留顾客；不用重新做全部M1～8。

## 上一轮：1.0m紧凑间距与身体入口保护

- Owner确认“好的，改吧”。runtime仅`CounterQueuePlanner.cs`（默认/下限1.0m）与`CustomerFlowController.cs`（实际入口保护0.911m，等待目标0.991m）；原角色半径0.45m、skin0.01m、epsilon0.001m、到站容差0.08m、入口2x2通行/等待slot规则、exit/Pick-up、真实连续碰撞、路线/重试上限、FIFO与并发均保持。无scene/prefab/Save/dependency迁移，无新诊断工具，无commit/push/merge。
- 默认local目标为队首(2.5,2.5)、第二位(2.5,1.5)、第三位(2.5,0.5)。1.0m间距不能覆盖双方任意8cm停止误差，实际入口/TailClear和每段路线/World碰撞持续判断；目标能放下不代替实际入队成功。
- `spacing-100-native-red.xml` **0 PASS/1可信FAIL/0 SKIP**，1.85秒：真实保存场景第二位距新目标0.1m，旧1.1m队形仍提前转弯。未改家具或marker。
- `spacing-100-native-green.xml` **1 PASS/0 FAIL/0 SKIP**，6.08秒：实际第二/第三位距手算目标≤0.081m，默认八位全部入队，按FIFO逐位离场与前移，三项容量归零。沿用原连续身体分离0.909m、家具skin及速度上限检查，未放宽安全断言。
- `spacing-100-direct-play.xml` **55 PASS/1 FAIL/0 SKIP**，227.50秒：P12 Flow24与Scene32。唯一FAIL `RealCornerQueueSeparatesDuringEntryForwardAndExit` 仍要求左转x严格小于-1.0，而真实新间距恰为-1.0；改为≤-0.999（1mm容差）后，仍验证完整入队、前移、离场和所有连续碰撞。三份已有tests更新：新default domain与真实八人case；旧1.1m边缘domain fixtures显式传入custom spacing保留；Flow/native间距界限改为新1.0～1.25m；原临时绕行/disable恢复改用第四位，因为第三位现在直达尾位。
- `spacing-100-final-focused-play.xml` **5 PASS/0 FAIL/0 SKIP**，59.64秒：修正旧断言后的真实弯队、默认第三位及八人排空、第四位中途绕行/disable/rebuild恢复、空店边缘五人、三人边缘重排后补五人全部通过。生产代码从首次GREEN至两轮回归没有追加修改。与首轮55个PASS按fullname去重得到**56个不同P12 Play cases全部有PASS**；这不是一次56/56的单报告全绿suite，不混计重复case。
- `spacing-100-final-edit.xml` **45 PASS/0 FAIL/0 SKIP**，4.45秒：P12所有domain（CounterQueue24），覆盖新默认三位直排、非法小于1.0m拒绝、自定义1.1m旧边缘/回退fixtures、queue generations、admission rollback与随机clock。
- **UNKNOWN / OPEN：**截图1～2秒停顿未定位，本次不声明修复；既有五人沿墙屏障跨侧重排未列入本次测试、没有复核当前1.0m是否改变该结果，仍OPEN，不能销项。原图精确摆法及主观间距手感仍待Owner；全项目Edit/Play、P11单独suite、Player/真机与长时间soak本轮NOT_RUN。
- Owner只需Stop→Play默认布局，确认第三位站在第一位Westie背后、后续队伍沿边转弯及离场跟进。无需重做M1～8；结果不代替玩家视觉验收，Phase不关闭。

## 上一轮：安全边缘对齐与直段均匀分布

- Owner批准“好的，试试看”：1.1m为最小间距，段起点/队首固定，尾部使用不足一步的剩余安全空间，同一段直队均匀分布。最多8次边缘探测，使用owned路径实际endpoint并向内留2cm，全部目标重新检查且失败整段恢复；与候选/复验共用256检查预算。微调最多25%（默认1.375m），名额填满后停止，不为不存在的下一次转弯延伸末段。P11半径/skin/运动、路线搜索/timeout、FIFO/并发不变，无新工具或资源迁移。
- `edge-align-domain-red.xml` **15 PASS/4可信FAIL/1 fixture FAIL**：四朝向旧尾只到3.3m、没有利用4m安全边缘；另一失败为Vector3精确float比较，已改距离容差，不计产品RED。`edge-align-scene-red.xml` **1可信FAIL**：真实Cash Grid(0,5)队伍转弯停在local(.5,1.2)，未用corner cell(0,0)。
- `edge-align-domain-green.xml` **19 PASS/1 FAIL**：首次实现同长度后来的右分支覆盖优先左分支，补保留首个已对齐分支；`edge-align-domain-final-green.xml` **21 PASS/0 FAIL/0 SKIP**。新增四朝向均匀最小间距、不可达回退、不足一个间距不加人/动头、采样高度保持水平，共7个cases。
- `edge-align-focused-play.xml` **2 PASS/5 FAIL**：两个真实入店case发现对齐后第二位堵入口，补非队首目标距spawn至少1.1m＋最大ArrivalDistance＋Epsilon；另三个Flow fixture仍按旧固定1.1m坐标，改为新slots实际到达/最小间距，保留全部并发帧、连续分离、FIFO、暂停/取消断言。`edge-align-entry-green-play.xml` **6 PASS/1 FAIL**，20.41秒：上述回归通过，仍有营业重排到新队首的实际身体冲突，未宣称全部GREEN。
- `edge-align-corner-green-play.xml` **2 PASS/0 FAIL/0 SKIP**，91.17秒，属于初版历史：空店corner五人入店/离场通过；临时定位case证明已有五人移动后新队首距旧尾**0.90001m < 0.911m**，正确安全Blocked、原位/容量保留。但它没有证明五人重排完成，不能把这个结果作为重排可交付。
- 首轮 `edge-align-final-play.xml` **83 PASS/3 FAIL/0 SKIP**，331.97秒：两项朝向fixture仍期待固定1.1m，补实际新slot到达、原左/右转方向、朝向与间距断言；NativeFiveCashMovedBehindCoffeeFindsDetourAndDrains为真实失败，第四位用完有限绕行重算。短段被过度拉开改变队形；`edge-align-compact-red.xml` **1可信FAIL**，0.14秒，1.1m被拉至1.978m。限制最多25%后 `edge-align-compact-green-play.xml` **5 PASS/1 FAIL**，32.12秒：原咖啡柜台布局与两项朝向/并发/满八人均恢复通过，五人同侧corner重排仍Blocked。
- `edge-align-capacity-red.xml` **1 PASS/1可信FAIL**，0.19秒：满四名额末段仍从3.3m拉到3.976m。改为满容量立即停止后 `edge-align-capacity-green-play.xml` **5 PASS/1 FAIL**，33.39秒：同侧corner队首已抵达，但第二位仍找不到绕过旧队伍的安全路线；并非全队恢复PASS。
- **OPEN / NOT_FIXED：**默认五人已Queued，Cash真实Confirm/Done移至Grid(0,5)，最终边缘实现下第一位抵达，第二位报告“前移：没有安全通道”。旧后三位沿墙占位形成屏障；既有有限路线/FIFO没有找到安全绕行。尚未证明全局几何不可达，也未验证旧基线在该确切五人摆法是否失败；不能断言它必然只是物理空间不足。本轮不改重排调度。这个探索case未计入最终选择suite，不能用其余测试PASS销项。
- `edge-align-corner-final-green-play.xml` **3 PASS/0 FAIL/0 SKIP**，56.03秒：空店corner五人入店/离场；三人已Queued后同位置重排、真实抵达新assignment，再补至五人并FIFO排空；原咖啡后方五人重排回归通过。最终corner fixture另强化真实角色确实占据corner cell和五位各自距离新目标≤0.081m，不以slots或Queued标签代替实际位置。
- `edge-align-final-edit.xml` **152 PASS/0 FAIL/0 SKIP**，4.92秒：P12 44、P10 108。CounterQueue 23项含新增9项，覆盖四朝向、最小/最大微调间距、不可达完整回退、采样高度、满容量停止、有限预算与generation/容量回归。
- `edge-align-final-verified-play.xml` **86 PASS/0 FAIL/0 SKIP**，316.09秒：P12 Flow24、Scene31、P11 Movement31。两个corner cases强化真实角色角落占位/新目标≤0.081m并排空；原咖啡柜台后方五人重排、其余既有布局、满八人、独立前移/暂停/取消/disable、FIFO及原连续0.909m分离/家具skin/速度上限全部通过。明确不包含上述OPEN五人跨屏障探索case，不代表它已修好。
- 测试Editor已退出；未新增无关tracked资源修改。上述本轮files未检出行尾空格，Roadmap scoped diff-check通过；全局既有MainCafe.unity:4130空m_Name行尾空格未处理，不宣称全局whitespace PASS。原图准确坐标/旋转未恢复；Owner只需重测红格利用与间距手感。全项目Edit/Play、Player build、长时间soak和复杂视觉本轮NOT_RUN，无commit/push/merge。

## 上一轮：场边死路有限回退

- Owner报告队伍到场边经常没有站位，批准保留1.1m并优先沿边转弯、死路有限回退试用。原CounterQueuePlanner只接受第一个合法方向，之后遇死路就停止，不能尝试已存在的另一分支。原BuildQueue的plannedSlots累加列表也不适合回退，会把弃用分支当成虚拟角色。
- 本轮源代码仅CounterQueuePlanner和CustomerFlowController：直/左/右优先，找到满容量停止；否则最多256候选，保留预算内最长已验证分支，同长度保留先找到者。prefix只包含当前分支，回退撤回虚拟占位；所有候选继续原边界/实体、1.1m、入口禁站、Pick-up安全空间、前方已站人时入口可达性检查。不改P11、CustomerRoutePlanner、实际移动/FIFO/并发/timeout规则；没有scene/prefab/Save/dependency迁移或新诊断工具。
- `boundary-queue-domain-red.xml` **9 PASS/4可信FAIL/0 SKIP**，0.16秒：0/90/180/270°都只找到三位，但已给定另一合法六站位分支。`boundary-queue-scene-red.xml` **1可信FAIL**，4.07秒：满八人真实Confirm/Done把收银台移至Grid(4,6)、旋回原方向后，旧规则只生成七位（不是已证明该布局物理极限）。
- `boundary-queue-domain-green.xml` **14 PASS/0 FAIL/0 SKIP**，0.17秒：四朝向正确找到六位、相邻/全体1.1m保护；有限区域请求100位仍最多256候选加head；同输入确定性；回退后的虚拟占位只含共同prefix，原CounterQueue事务/generation通过。
- `boundary-queue-focused-play.xml` **6 PASS/0 FAIL/0 SKIP**，51.33秒：该Grid(4,6)布局找到八个可达的新站位，全部真prefab实际抵达并FIFO排空/容量归零；原满八人case亦通过，直队/重排/弯队实际并发及真实取消后客停止保持。原连续0.909m分离、家具skin、速度上限及新assignment实际到达断言保留。
- 单独 `boundary-queue-rebuild-timing.xml` **1 PASS/0 FAIL/0 SKIP**，24.03秒：本机该布局的Done+flow.Step(0)重建约**0.043秒**。该测量仅在已有scene test内记录，包含Done恢复与队伍重建；不代表任意大布局或移动设备耗时保证。整个case时长含八人真实移动和离场。
- `boundary-queue-final-play.xml` **84 PASS/0 FAIL/0 SKIP**，269.00秒：P12 Flow24、Scene29、P11 Movement31。包括八位场边重排、三个近似截图摆法在Queued/Entering时移动、并发实际位移、取消/暂停/disable恢复、缩队保留、Cash-only 15/30/120fps/真实Editor时序及原P11碰撞/有限移动保护。该轮Done+重建记录0.045秒，与单独0.043秒一致；不是所有布局性能保证。
- `boundary-queue-final-edit.xml` **143 PASS/0 FAIL/0 SKIP**，4.67秒：P12 35、P10 108。六项新增domain cases、queue generation/事务及容量rollback回归全部通过。测试Editor已退出，git status未新增无关tracked资源修改；全局diff-check仍报告既有MainCafe.unity:4130空m_Name行尾空格，本轮没有改该scene，也不宣称全局whitespace PASS。
- 原图一准确Grid坐标/旋转没有恢复，自动结果不代替视觉验收；正常第一分支已够容量时不改队形，也不为“最贴边”缩小间距/半径。真正空间不足仍按旧缩队合同保留原位/容量，未承诺所有布局八位。全项目Edit/Play、Player build、长时间soak与复杂视觉本轮NOT_RUN。

## 上一轮：已有队伍独立前移与装修重排

- Owner报告“只有第一个人物停止移动，第二个才开始动”，批准限定范围：已有队伍前移、移动柜台后重新排队。旧CounterQueueService单一advancing锁和spec §3.3的逐位Arrived规则是直接原因。新到店接纳、验证离场按钮门禁保持原有语义，不扩张到新顾客并发入店。
- 仅CounterQueueService和CustomerFlowController：每entry有独立Moving/assignment generation；仍按FIFO顺序分配目标，完成回调不必同序。前移后客规划对同队更前、enabled且有活动Advancing请求的顾客预测腾位，保留其目标slot；所有实际身体仍参与完整P11连续碰撞，静止/外部/Employee/Entering/Exiting/disabled角色仍作为路径障碍。安全距离不足可限制位移/暂时等候，原有限timeout/retry保留，不许穿角色或重置deadline无限等候。
- 前移失败会退休已活动的更后顾客请求，保留实际位置、assignment和容量；修复后每位从实际pose重排。暂停/disable/revision通过各自generation取消旧回调。未改P11 guards、CustomerRoutePlanner搜索范围、间距1.1m、空Employee/Pick-up/入口规则，无新工具、scene/prefab/Save/dependency migration。
- `concurrent-advance-red.xml` **3 FAIL**：直队和native弯队实际并发帧数均0，为可信RED；首版重排fixture也0，但后来发现validationHead变化未触发新队伍计算，不能独立作为可信重排RED。`concurrent-queue-domain-red.xml` **6 PASS/2可信FAIL**：前客未Complete不能分配后客，恢复无法产生两个并发assignment。第一次domain启动因漏LINQ import编译失败、没有XML，不算产品FAIL；补import后得到上述真实RED。
- 最小实现后 `concurrent-advance-first-green.xml` **2 PASS/1 FAIL**，3.93秒：直队/native弯队并发已通过；重排fixture未触发新queue，失败不能算重排生产故障。补显式disable/enable并断言真实Advancing开始；`concurrent-advance-leader-red.xml` **3 PASS/1可信FAIL**，4.73秒：实际Service.Cancel前客后后客未停止，独立任务使这个原来不可见的保护缺口暴露。退休后客请求修正后GREEN。
- `concurrent-advance-focused-green.xml` **4 PASS/0 FAIL/0 SKIP**，4.47秒：直队与真实保存场景弯队至少12个60fps测试帧有两位Advancing实际位移>0.001m；3m重排真实并发并在多人移动中Pause/disable保留实际pose，恢复抵达并排空；真实Cancel前客停止活动后客、Total保留、重试抵达。逐帧完整角色分离、位移上限/无瞬移、直队不超车、native家具skin/FIFO排空断言保留。状态标签不代替实际移动。
- `concurrent-advance-focused-edit.xml` **137 PASS/0 FAIL/0 SKIP**，4.81秒：P12 29、P10 108。并发assignment独立完成和恢复后各旧generation拒绝通过；容量/rollback/scene-owner/双Play生命周期回归通过。
- 首轮直接回归 `concurrent-advance-regression-play.xml` **73 PASS/10 FAIL/0 SKIP**，92.80秒：后客未来目标挡住前客、交叉路径早启动造成真实超时/通道被占用，15fps连续分离最小0.908709m未通过原0.909m断言；此时不能交付。追加FIFO目标保留优先级与实际剩余路径冲突门禁后，`concurrent-advance-priority-green.xml` **28 PASS/3 FAIL/0 SKIP**，224.36秒：两项Pick-up后方布局中后客先抵达后挡住前客，另有原15fps分离失败。文件名带green不代表全部PASS。
- 继续限制提前占住前方通道，并将同向旧站位跟进豁免放在交叉检查之后；`concurrent-advance-corridor-green.xml` **7 PASS/0 FAIL/0 SKIP**，15.58秒：上述三项原失败与四项并发focused均通过，15fps原连续分离断言保留。等待发生在assignment提交之前，不重置移动deadline或增加无限重算；P11实现不变。
- 最终 `concurrent-advance-final-play.xml` **83 PASS/0 FAIL/0 SKIP**，236.94秒：P12 Flow24、Scene28、P11 Movement31；四项并发测试、六项近似截图布局、Cash-only 15/30/120fps/真实Editor时序、满八人缩为七站位、FIFO排空、实际容量释放、Pause/disable/修复及原移动保护全部通过。保留完整连续角色分离/家具skin/位移上限，排除旧30游戏分钟soak。
- 最终 `concurrent-advance-final-edit.xml` **137 PASS/0 FAIL/0 SKIP**，4.98秒：P12 29、P10 108。在最新启动门禁实现下重跑，新增断言安全门禁拒绝时assignment与generation不变，腾空后按FIFO继续；并发完成、旧回调拒绝、容量事务及生命周期通过。运行结果来自真实Unity XML，测试Editor已退出；git status无新增无关tracked资源修改。全局diff-check仍有既有MainCafe.unity:4130空m_Name行尾空格，不在本轮修正范围，未宣称全局whitespace PASS。
- 本轮源代码只修改CounterQueueService、CustomerFlowController和已有三份测试；同步本Phase现有文档。完整Edit/Play、Player build和复杂视觉本轮NOT_RUN；Owner前移/重排动作感受与原图准确布局验收Pending。

## 上一轮图2～4：新队伍站位被规则排除

- Owner随后明确批准空Employee工作位可排队、实际有人时避让。生产改动仅CustomerFlowController与CounterQueueService：不再排除空Employee anchor，只保留Pick-up Customer位置的0.991m安全空间（最大代理直径＋skin＋epsilon＋ArrivalDistance）；实际角色继续原路线避让和World完整碰撞保护。队伍1.1m间距与有限三方向贪心规则不变；未扩大CustomerRoutePlanner搜索范围。
- HUD现在区分带具体原因的移动失败、新站位不足（可用/当前队伍人数）和FIFO恢复等待。无站位时保留原ID/实际位置/容量，不驱逐、不瞬移；站位不足不冒充路径失败。
- `queue-space-status-red.xml` **1可信FAIL**：0/1站位旧提示为“队伍空间不足或正在前移”；修正后包含在 `global-detour-slots-green.xml` **8 PASS/0 FAIL/0 SKIP**，30.41秒。六项真实保存场景case覆盖Cash移动至(4,5)/(6,5)/(7,6)，五人已Queued和第五人Entering两种时机，均产生足够新slots、各assignment使用新revision站位、真实抵达并FIFO排空。另两项检查Pick-up Customer留空/真实占位角色拒绝admission且不reserve容量，以及0/1站位身份/位置/容量保留。
- 首轮直接回归：`empty-employee-focused-play.xml` **78 PASS/1 FAIL/0 SKIP**，231.95秒。满8人移Cash至(4,6)时，放开Employee后贪心链生成了被家具和前方七人围死的第八个目标，真实前移报告“没有安全通道”；其余六项近似截图布局通过。
- 追加修正仍只在CustomerFlowController：候选必须能用既有CustomerRoutePlanner从入口绕过此前接受的虚拟slots，路线阈值保留代理直径/skin/epsilon，不扩大搜索/重试上限。该(4,6)摆法只有七个可达slots，按既有缩队合同保留第八位旧位置与assignment，HUD提示7/8；允许队首安全离场后继续FIFO恢复，不承诺所有布局都容纳八人。
- `empty-employee-accessible-slots-green.xml` **8 PASS/1 FAIL**，32.85秒：六项近似截图布局、真实占位拒绝、0/1提示通过；满队case已正确生成七站位，失败因新fixture误期待保留尾客为Blocked，实际既有已Arrived尾客保持旧Queued。测试已改为明确检查旧位置不变、七位各自到新slot、7/8提示与Total8保留，然后逐位FIFO排空；不把旧尾客Queued冒充新slot到达。
- 最终 `empty-employee-final-focused-play.xml` **79 PASS/0 FAIL/0 SKIP**，231.39秒（P12 Flow21、Scene27、P11 Movement31）。满队缩为七站位的case通过：旧尾客实际位置与Total保留，七位抵达新slot，安全离场后FIFO继续、最终顾客与容量归零。六项近似图2～4布局、真实占位拒绝、零站位提示以及暂停/disable/装修/匀速/碰撞直接回归全部通过。测试结束Editor已退出，未发现新增无关tracked资源修改。
- 回归只选择P12 scene/flow和P11 movement，排除旧30游戏分钟soak；未重跑完整Edit/Play、Player build或复杂视觉验收。
- 当前自动结果证明上述近似布局，未取得Owner原图精确Grid坐标/旋转。原图2～4复测Pending，图1间距与边缘队形未在本次修改。

以下保留定位过程：

- Owner报告图2/3柜台移动后旧队伍不前移，图4虽有红/蓝绕路仍受限，补充提示“没有路径”。未取得原图准确Grid坐标；未声称原图精确复现。
- 只改现有Phase12CustomerQueueSceneTests，增加三个估算摆法：Cash移至Grid(4,5)、(6,5)、(7,6)，Coffee/Pick-up保持默认，并分别在五人已Queued/第五位Entering时Confirm/Done移动。初次`global-detour-scene-red.xml` **3 PASS**是测试覆盖不足：旧Queued状态及旧assignment位置被错误当作重排完成；本轮已加强为必须有足够的新slots，并断言assignment实际等于该revision的slot。
- `global-detour-entry-red.xml` **3 FAIL**初次包含fixture对装修Held尾客过早报错；修正Done后Step(0)并允许FIFO恢复中的Held后，`global-detour-entry-diagnosis.xml`仍 **3 FAIL**，前两个不动，靠墙摆法仅首位抵达新位置，符合用户现象类型。没有生产代码变更。
- `global-detour-slot-red.xml` **3可信FAIL**：对应实际新slot数量为0、0、1，期待五人重排。当前BuildQueue排除所有Employee anchors及Pick-up两种role周围1.1m；(4,5)/(6,5)的新Cash Customer anchor分别与Coffee/Pick-up Employee anchor重合；(7,6)新head可用，但后续固定方向候选被预留anchor/边界截断。故阻断发生在排队目标生成阶段，不能据此宣称“红蓝通道本身不可走”或先扩大全局路线搜索。
- 既有spec第3.2节原规定候选“避开实体、入口clearance、Employee Side、Pick-up anchor”。定位时尚未实施规则改变；Owner现已批准空员工位规则，spec与本轮实现同步更新，真实家具/角色碰撞保护、Pick-up顾客目标安全空间和图1的1.1m间距继续保留。
- 定位时另有错误反馈问题：恢复中的Held尾客可使HUD显示无具体原因的“顾客路径受阻（）”，旧提示未准确区分新站位不足与移动失败。本轮已按上述修正分别验证足够/不足站位，保留容量/FIFO并从实际位置恢复。原图Owner复测仍Pending。

## 最新 M6/窄路失败与临时拐点修正

- Computer Use只读当前Unity：1x、已退出Decor、部分顾客已走到新队伍，第5位Blocked，HUD明确“前移：尚未抵达绕行拐点”；不是仅凭最初静态截图推断未刷新。Console无warning/error。原图精确Grid坐标未恢复。
- P12对临时waypoint要求水平误差≤Epsilon（0.001m），但P11 Service允许ArrivalDistance（0.08m）。家具边缘完整skin会安全阻止精确抵达；合法Arrived被P12误判Blocked，从而停止后方队伍。
- `m6-edge-native-red.xml` **1 FAIL**，真实场景/角色/Service复现“入店：尚未抵达绕行拐点”，native endpoint约(-2.450,-1.025)，实际安全pose约(-2.462,-1.026)。测试指定两段route隔离Controller回调，不模拟Arrived、不写Transform；不是自然planner或Owner精确布局复现。
- 最小生产修改仅CustomerFlowController：临时到达额外距离检查采用原ArrivalDistance；下一segment仍从实际位置重新查询并检查完整动态路径，必要时既有有限重算。最终Queued/Exit、容量释放、P11完整碰撞skin与唯一Transform写入权不变。`m6-edge-green.xml` **1 PASS**，继续抵达真实最终站位，逐帧位移/角色分离/家具skin断言保留。
- `m6-wall-detour-red.xml`文件名带red但实际 **1 PASS**：满8人朝后墙排队后，通过真实Decor/Preview/Confirm/Done移柜台至Grid(4,6)并转回0°，逐位到达新slot、FIFO排空、容量归零。无强求满队入口被占用时补客。
- 早期探索结果不计产品RED：`m6-full-queue-red.xml`在后续补客时入口被占用，fixture错误期待恢复8人；`m6-wall-queue-red.xml`/`m6-wall-narrow-red.xml`移动柜台被角色占位保护拒绝；`m6-edge-red.xml`只因初次fixture未找到边缘corner失败。合法位置/边缘选择调整后得到上述证据，无生产诊断helper。
- `m6-waypoint-focused-play.xml` **72 PASS/0 FAIL/0 SKIP**，197.46秒：P12 Flow20（排除旧30分钟soak）、scene21（包含两项新case）、P11 movement31。默认/steady、窄通道安全失败、墙/静止角色拒绝、暂停/取消/disable/rebind、真实布局变化/缩队恢复均通过。未重跑完整Play/Edit/Player build，也不声称所有窄路已证明可达。
- 同步本Phase spec/tests/plan/report及Beginner Guide的Confirm→Done→1x步骤；无scene/prefab/Save/dependency migration。Owner原布局M6与窄路复测Pending；M2/M7未验证视觉/堵路部分继续保留。

## Codex Computer Use：实际 Unity 操作（2026-10-06）

使用现有Unity 6000.5.5f1 Editor和Computer Use的原生窗口截图、鼠标点击与拖动。下表不以Button.onClick、直接调用装修handler或修改Transform代替人工操作。所有试玩变更仅在Play Mode；未保存场景。Owner主观间距、手感和原截图精确布局验收仍独立保留。

| ID | Codex本轮状态 | 实际操作与观察边界 |
|---|---|---|
| M-001 | PASS | 不点离场观察84.6秒；自然到店达到8人，并保持Counter 8/8、Total 8/16、PickUp 8/16，显示容量满暂停生成。精确随机分布由自动测试覆盖。 |
| M-002 | PARTIAL / 布局拖动BLOCKED | 默认紧凑弯队及旋转收银柜台后的另一朝向队伍均观察到后客朝前方；Shiba/Westie可分辨。未完成通过拖家具分别构造直线、左弯、右弯布局；1.1m精确测量与主观视觉接受不由截图代替。 |
| M-003 | PASS（默认与旋转布局） | 默认场景完成11次真实队首离场，包括自动补客，再关闭本次Play的Auto Spawn将8人逐位排空；每位及最后尾客均完成前移，最终Total/Counter/PickUp全为0。暂停时双击离场仅一个ID成为Exiting，等待后恢复2x才移动。另一次旋转与设施恢复后的满队成功离场补客。截图估算的分散柜台布局未通过本轮鼠标摆出，不声称原截图人工复现PASS。 |
| M-004 | PASS | 满8人停止到店；真实离场期间Total保留Exiting，离位/离店后释放名额并自动补至8，没有一次补发积压顾客。 |
| M-005 | PASS（机械操作） | 实际点击Pause、1x、2x；暂停中的Exiting保持位置。另一轮在3人时捕捉到店倒计时2.7游戏秒，暂停后等待超过8秒仍为2.7、人物不动；恢复后继续。2x精确比例与空路匀速数值以自动证据为准。 |
| M-006 | Owner FAIL → 修正后原布局重测Pending | Codex此前只完成旋转，鼠标拖动未成功；Owner随后确认整个收银柜台移动、Confirm、Done、1x后卡住。当前Unity读取到部分前客已前移、第5位临时拐点Blocked。自动RED/GREEN与满8人移动见上节；不把自动通过算Owner原图人工PASS。 |
| M-007 | PARTIAL / 物理堵路BLOCKED | 额外完成实际撤下PickUp、退出装修、失败离场/重试、重新放置PickUp及恢复流程：中文不可用提示可见，原8个ID与容量保留；恢复后队首真实离场、后方前移补客。未完成用家具分别堵入口/队伍/出口的原定步骤。 |
| M-008 | PASS（目录与预览基础回归） | Stop/Play两次，ID重新从1开始，无重复owner/顾客；实际打开MainCafe并Play，保持初始未营业布局，进入Decor、打开目录、生成Counter1x1预览、Rotate、Confirm放置、Done退出；Console为0 warning/0 error。家具拖动的失败同时在MainCafe复现，另列为开放阻断。 |

**Computer Use限制与未验证步骤：** `sky.drag` 在P12柜台、浮起的家具预览、最大化Game View，以及MainCafe新建的空Counter1x1预览中均未造成可见位置变化。点击、旋转、确认、目录和HUD按钮可实际生效。当前证据无法区分Computer Use手势时序与游戏鼠标输入处理，不能断言游戏拖动已坏，也不能宣称工具故障已证实。M-002/006/007依赖步骤的Codex记录保持BLOCKED；按Owner最新要求，这些拖动布局与视觉接受留给玩家验证，不伪装为PASS。未删除顾客、未通过内部handler/Inspector位置替代鼠标操作。

本轮实际UI检查结束后安全停止Play并关闭Editor。EditMode画面显示的“Discard this preview?”来自已保存prefab的静态内容，Play按钮已灰色、场景无未保存星号；自动审批曾阻止首次关闭，核对这些证据后允许安全关闭。没有丢弃或保存玩家工作。

完整EditMode首次sandbox启动在licensing IPC/BIOS访问失败处停止，未生成XML、未执行测试；保留`cu-full-edit.log`，不计产品FAIL。宿主权限的追加运行随后按Owner收敛范围要求中止，保留`cu-full-edit-verified.log`，没有XML，不能报告完整Edit PASS。已精确恢复本次改写的4个场景（含MainCafe/P12原开发版本），2个新建DirtyCaller测试资源移到`cu-interrupted-side-effects/`保留；2239个Assets/ProjectSettings文本资源比对0改变/缺失、0新增，见`cu-interrupted-cleanup.json`。鼠标观察代码只准备过未启用草稿，已移除；没有新增Editor诊断文件或修改生产输入行为。Owner acceptance及Phase closeout仍NOT_RUN。

## 实现与 review

连续均匀 1～5 游戏秒到店、确定性直/左/右 slot 链、FIFO逐位前移、P10 admission lease、真实 P11移动与 runtime ID、装修/revision recovery、验证场景及 MainCafe 接线已实现。MainCafe 初始未营业布局保持 passive，首次有效服务布局后启用营业 gate。

Engineering review 原三项 Important（旧目标恢复、同步 rebuild 丢失旧请求终止、动态队尾未检查）已写可信 RED 并修复，原 reviewer 已局部复核关闭三项。QA 的五项Important补测与HUD遮挡finding均已复核关闭。仍明确保留无法以本轮fixture强制触发的 ID/Instantiate 异常注入边界。

## Owner 试玩修订：间距与朝向

- M-001：Owner明确“1没问题”，记录旧版本PASS。M-002：Owner后续明确后方朝向没问题，朝向PASS；间距1.1m继续试用。M-003 Owner FAIL；M-004～008 NOT_RUN。
- 获批试用：slot间距1.2m→1.1m；队首朝柜台，其余朝前一位slot；入队、前移、重排统一使用P11目标朝向平滑转身。
- 队尾检查改用已有代理半径/CollisionSkin/Epsilon/ArrivalDistance，修复紧凑站位被写死1.1m阈值误拒绝；代理半径与P11碰撞规则未改。
- 可信RED：polish-spacing-red.xml为3 FAIL / 4 PASS；polish-facing-red-v3.xml两项真实左右转弯均90度偏差FAIL。首次最小实现后两项因队尾误拒绝FAIL。
- GREEN：polish-facing-green-v3.xml为2 PASS，覆盖实际入队、前移、头部朝向及逐帧碰撞；polish-edit-green.xml为25 PASS / 0 FAIL / 0 SKIP，包含P12全EditMode和双Play生命周期。
- 新快照完整PlayMode：polish-full-play.xml，1150 PASS / 0 FAIL / 5 opt-in SKIP，444.5秒，含密集队伍/30游戏分钟soak及原有MainCafe/P11。全项目EditMode最终回归将在视觉调整确定后集中执行；旧版2465PASS不自动替代新快照。

## M-003：三人停滞、离场与前移修复

- Owner反馈：通常只有三人；队首有时卡住，离开后后方不前移，HUD提示修复布局。截图代表布局，具体第二张修改后的坐标未获取；自动测试使用保存的默认密集布局及另一个保持readiness有效的分散布局，不声称穷举Owner所有摆法。
- 可信复现：native-m3-red-v2.xml为默认三人FAIL、分散三人PASS；red-v3为默认三人FAIL、密集两人/分散四人PASS。第三位路径穿过已排队角色，P11碰撞保护使其MovementTimeout，进入Blocked并暂停后续流程。Counter上限8、Total16，没有写死三人限制。
- 修复：新增CustomerRoutePlanner有限临时waypoints，完整owned NavMesh path检查角色间距；进入前证明可绕行，过程中每段从实际位置验证并有限重算。移动、碰撞、转身仍由P11执行，中间Arrived不提交Queued或释放Total。同步恢复后重读blocked状态，避免HUD读取过期状态。
- 通道门禁：入店/前移/离场时不接纳下一位；入店/前移先完成，下一次离场等待前一位真正离店。队首实际离位后仍可同时Exiting与后方Advancing。阻塞后保留token与实际位置，中文原因区分入店/前移/离场。
- Review发现一个Important：缩队时静止Blocked尾客被活动移动门禁误拒绝，阻止安全队首排空。m3-shrink-red-v2.xml为可信1 FAIL；修复门禁只拦实际Entering/Advancing/Exiting。原Engineering reviewer已结合源码及GREEN XML复核关闭，无新增阻断项。
- 过程失败保留：m3-p12-play.xml为22 PASS/4 FAIL（同步状态HUD+soak出口争用）；v2为25 PASS/1 FAIL（入店/离场相向争用）；v3为26 PASS。初次shrink RED未真正改变floor revision，属于fixture失败；v2修改floor后才作为产品RED。
- 上一轮focused GREEN：m3-p12-play-final.xml **29 PASS/0 FAIL/0 SKIP**，含默认四人/分散四人逐位离场、连续角色分离/家具间距/无瞬移，中间waypoint token保持、disable/rebuild恢复、Entering缩队排空、Exiting与Advancing同时存在及30游戏分钟soak（408条released ledger记录，顾客与Used归零）。m3-p12-edit.xml **136 PASS/0 FAIL/0 SKIP**，含P12的28项与P10的108项；路线曲线corners和无路有限查询检查PASS。
- 上一轮完整回归：m3-full-edit.xml **2468 PASS/0 FAIL/0 SKIP**，1998.0秒；m3-full-play.xml **1156 PASS/0 FAIL/5 opt-in SKIP**，452.9秒；其中P12 Integration 29项全部PASS。两套使用上一轮M3修复快照；已恢复EditMode生成的30个无关资源文件及PlayMode随后再次改写的1个旧材质。本次门口小修正的结果见下一节，不继承旧快照完整PASS。

## 门口邻近柜台：路径安全距离误判修正

- Owner明确入口2×2只禁止家具/物品放置，人物可以经过，内部格子可参与寻路。核对Project Design和NavigationLayoutAdapter：入口reservation未烘焙为NavMesh障碍；它只排除排队等待slot，不排除移动路径。生成点/出口点无需因本次复现改变。
- 相近真实布局：先在Grid `(2,0)` 放置空柜台，再逐位入队。入口四格实际P11 MoveTo全部Arrived，但第二位入店被路径检查拒绝。`entrance-red.xml` **1 PASS/1 FAIL**。这不是Owner截图精确坐标/旋转复现。
- 独立native path证据：绕行末段与前方顾客中心间距约0.966m；真实代理安全距离为0.911m，旧路径阈值却为0.991m（多加0.08m ArrivalDistance）。`entrance-diagnosis.xml`保留native corners与两阈值对照；ArrivalDistance是停止容差，不是身体半径。
- 小步修正仅改CustomerFlowController.RouteClearance，移除额外ArrivalDistance。队尾/admission到站误差保护保留；下一segment仍从实际位置重新验证，P11持续碰撞约束、代理半径和Transform写入权不变。
- `entrance-green.xml` **2 PASS/0 FAIL**：入口四格真实走过；门口邻近柜台时三位真实顾客入队、逐位离场前移、最终容量归零。逐帧角色分离、家具间距、位移上限及无瞬移断言PASS。
- `entrance-focused-play.xml` **66 PASS/0 FAIL/0 SKIP**，30.3秒（P12 Integration 31项及P11 movement/layout 35项）；`entrance-focused-edit.xml` **136 PASS/0 FAIL/0 SKIP**，5.5秒（P12 28项、P10 108项）。
- `entrance-full-play.xml` **1158 PASS/0 FAIL/5 opt-in SKIP**，1163 total，454.1秒；其中P12 Integration 31项全部PASS。5项SKIP仍是下节列出的原有截图/几何导出opt-in case。已恢复本轮测试生成的1个旧验证材质；随后只更新文档，不重复Unity运行。
- 原Engineering reviewer局部复核无阻断项；证据支持当前clearance修正，不证明所有家具摆法，也不替代原截图Owner复测。M-003继续Pending，M-004～008 NOT_RUN。

## 再次 M3 FAIL：真实帧时序、拐点与减速诊断

- Owner截图：ID2/3 Queued、ID4 Advancing、ID5 Queued；后方必须等前一位Arrived，单帧状态不能证明最后一位本身死锁。Owner要求先由Codex执行真实Unity测试，并希望畅通路线匀速。
- 本轮先只修改P12真实scene tests，生产代码未变。`advance-five-red.xml`名字带red但实际 **2 PASS**，Dense/Spread五人都能排空。旧Spread同时移动现金与咖啡柜台，不能代表截图。
- `advance-cash-only-red.xml` **2 FAIL**属于fixture：allQueued会在连续前移之间短暂成立，过早TrySpawn误报。修正为连续两帧满足等待条件，并按游戏时间/真实时间预算等待，`advance-cash-only-diagnosis.xml` **2 PASS**，30/120fps五人各反复五轮离场补客再排空。
- `advance-auto-red.xml` **2 FAIL**属于新fixture reflection参数错误，已修复，不作为产品RED。`advance-auto-diagnosis.xml` **2 FAIL**：实际Editor frame timing自动到店第五位墙边入店Blocked；营业中把现金移到(1,5)后旧尾客占新head附近，可合法报告没有安全通道，未借此放松安全规则。
- `advance-steering-diagnosis.xml` **1 PASS/2 FAIL**：营业中真实Decoration/Preview/Confirm/Done把现金改移(2,6)避开旧尾客，五轮自动补客及排空PASS；实际Editor timing墙边入店重复FAIL；截图估算布局现金(2,6)、咖啡(5,4)、Pick-up柜台(6,6)离场FAIL。坐标仅按入口蓝区估算，未声称Owner精确原图复现。
- 墙边trace：位置约(-3.525,0,-1.963)持续不变，native desiredVelocity约(-0.029,0,-1.194)，目标(-3.525,0,-2.8)仍有0.837m；native意图带微小向墙外分量，安全夹限令actual speed=0，最终Blocked。它是新的可信真实帧时间边界案例，不等同于截图的前移请求。
- `advance-screenshot-diagnosis.xml` **1 FAIL**：离场中间waypoint按0.08m停止容差提前完成，实际pose重新验证后反复重算，segments=5/replans=4，最终“离场：通道被占用”。原有有限边界仍有效，未扩大重试上限。
- `steady-speed-red.xml` **1 FAIL**：单个P12顾客，路线与角色/家具距离充足，离目标约半米时实际速度持续低于MaxSpeed的90%，超过0.2游戏秒；证明可见减速，不只依赖主观观感。
- 方案随后获得Owner明确批准“同意，你改吧”；实现与最新证据见下一节。四个runtime files及相关tests/docs，无Save/scene/模型迁移。默认角色保持native模式，原安全参数与有限retry不变。

## 获批P12匀速模式与拐点修复

- 修改NavigationActor、NavMeshMovementDriver、NavigationWorld、CustomerFlowController。仅新P12顾客注册前选择SteadyPathMotion，沿owned complete path的水平corners请求MaxSpeed；每substep限当前corner，真实Transform仍由World唯一写入。全部guards、暂停/时间预算与原Service Arrived/容量合同保留。
- `steady-first-green.xml` **2 PASS/2 FAIL**：截图估算五人反复前移和空路匀速已PASS；自动生成中的两个家具拐点仍FAIL。`steady-corner-diagnosis.xml` **1 FAIL**：真实停点约(-3.457,2.051)，raw corner(-3.450,2.050)，后者距家具圆角只有0.45277m，完整skin要求0.46m，不能强求到达。
- 内部corner只在原ArrivalDistance范围内考虑简化；实际pose→下一corner必须通过owned Raycast与原完整连续sweep。`steady-corner-green.xml` **3 PASS/1 FAIL**：装修恢复PASS；第五位下一直线仍擦skin，guard拒绝正确，没有借此放松guard。
- 对这个边界，每内部corner仅一次有界外侧候选（2×CollisionSkin），保存raw corner和尝试标记；从实际pose验证完整连接才采用，下一腿仍逐步验证。最终目标不调整；失败不反复寻找、不重置deadline/retry，不瞬移。
- `steady-skin-green.xml` **10 PASS/0 FAIL/0 SKIP**，154.32秒。包含真实Editor帧时间自动到店5人、五轮HUD离场/自动补客及排空、营业中真实Decor移动/恢复、截图估算布局反复前移、空路无持续减速；mixed/default宽路到达/窄路安全失败，墙/disabled body不能跳过，Cancel/Pause/disable/rebind、短终点不越过和长帧无补走。
- review追加 `steady-final-facing-red.xml` **1可信FAIL**：accepted native furniture corner作为最终目标，完整skin安全停止后新模式按Epsilon启动Facing过严，导致MovementTimeout。最小修正仅恢复Steady final Facing原ArrivalDistance，DesiredVelocity/skin精度不放松；真实GREEN及完整回归见下列。
- `steady-final-facing-green.xml` **3 PASS/0 FAIL/0 SKIP**，3.30秒：上述靠家具终点正确Arrived/Facing、short final无越过、空路匀速都通过。focused结果独立记录，完整结果以本轮全套XML为准。
- Engineering已只读复核本轮4-file实现，无Critical/Important阻断；QA核对真实RED/GREEN后关闭final Facing Important。任意家具布局可达性不在这次有限验证结论内。
- 当前完整 `steady-full-play.xml` **1174 PASS/0 FAIL/5 SKIP**，1179 total，623.12秒；P12 Integration **40/40 PASS**（Flow21、Scene19）。覆盖五人反复补客/逐位排空、真实Editor帧时间、cashOnly15/30/120fps、截图估算布局、营业Decor恢复、30游戏分钟循环及全部旧P11/MainCafe。循环记录435条released ledger，结束顾客及Used归零；不声明无限运行或真机性能。
- 当前 `steady-focused-edit.xml` **213 PASS/0 FAIL/0 SKIP**，6.24秒：P10容量108、P11 navigation/resource/lifecycle77、P12 domain/scene lifecycle28。全项目完整Edit待视觉调整确定后按Phase最终流程集中执行；旧2468 PASS保留历史，不转成当前完整Edit验收。
- QA已核对完整Play XML，自动验收通过；本轮16项新tests均在完整Play内。测试结束后恢复1个Unity自动改写的旧验证材质 `M_WallProjection_Invalid.mat`，未恢复/覆盖MainCafe或P12改动。此后仅更新文档，无生产变更。
- HUD按钮测试调用真实Button.onClick，装修调用真实session/Confirm handler，证明业务接线；不等于实际pointer命中或Owner视觉接受。截图坐标是估算；匀速断言只覆盖安全余量足够、持续低速不超过0.2游戏秒的条件，真正障碍仍可停止。M-003保持Owner重测Pending，M-004～008 NOT_RUN。

## 早期实现证据

相对路径均在 worktree 的 `outputs/phase12/`。

| 证据 | 结果 | 范围 |
|---|---|---|
| task1-red.xml → task1-green.xml | 2 expected FAIL → 30 PASS | runtime ID/真实移动与动画 |
| task23-red.xml → task4-lease-green.xml | domain可信RED → 138 PASS | clock/queue/admission、P10与asset regression |
| task4-flow-red.xml → task4-flow-green.xml | 3 expected FAIL → 4 PASS | 真实 prefab入店/离店/失败恢复 |
| review-red.xml | 2 expected FAIL | changed-head + 同步rebuild、动态tail |
| phase12-main-green.xml | 98 PASS / 3 FAIL | 101项P12及MainCafe组合运行；三项失败为旧输入测试 |
| main-ui-isolated.xml | 3 PASS / 0 FAIL | 上述三项逐独立fixture补跑，暂不能声称组合稳定 |
| full-edit.xml | 2463 PASS / 0 FAIL / 0 SKIP | 完整EditMode，1924.6秒；随后新增QA测试另行补跑 |
| qa-red.xml | 17 PASS / 4 FAIL | HUD遮挡、两项中文原因可信RED；另一项为seed等待fixture问题 |
| qa-edit-green.xml | 25 PASS / 0 FAIL | 全部P12 EditMode含双Play Domain Reload off |
| qa-play-green.xml | 20 PASS / 1 FAIL | 剩余HUD恢复raycast读取早于真实draw frame |
| hud-ray-green.xml | 1 PASS / 0 FAIL | 等待真实draw frame后，HUD恢复按钮命中 |
| final-full-play.xml | 1123 PASS / 25 FAIL / 5 SKIP | 首轮完整Play失败集中旧MainCafe输入；P12测试全部通过，已定位P12 scene test未隔离共享InputAction状态，不能标最终GREEN |
| input-order-red.xml → input-order-green.xml | 4 PASS / 2 FAIL → 6 PASS | P12scene→legacyMouse/Touch最小顺序；修复新fixture隔离，旧生产input代码未改 |
| final-full-play-v2.xml | 1147 PASS / 1 FAIL / 5 SKIP | 旧输入失败全部消失；P11变帧率绕路timeout，隔离重跑PASS |
| p11-variable-isolated.xml | 1 PASS / 0 FAIL | 同一P11变帧率测试独立重跑 |
| final-full-play-v3.xml | 1148 PASS / 0 FAIL / 5 SKIP | 修复后的完整PlayMode，440.7秒；含全部P12和原有MainCafe/P11 regression |
| final-full-edit.xml | 2465 PASS / 0 FAIL / 0 SKIP | 全部修复后的最终完整EditMode，1956.0秒 |

30游戏分钟 controlled soak 已PASS：固定 `.05` 秒测试帧，36000 frames，受控队首离场；记录723条released reservation历史，结束时顾客/容量占用归零。仅证明本轮有限运行，不证明无限长期成本或真机性能。15/30/60/120fps的1x/2x与Pause clock检查PASS。

## 完整 PlayMode 的 SKIP 与波动记录

5项 SKIP 均为 opt-in evidence，未打开对应环境变量：CashIndicator_NativeOverlayCapture、SpacingAudit_ExportGeometry、ReadinessChecklist_CaptureNativeExamples、NativeUiExamples_CaptureActualGameView、Phase11 CaptureRepresentativeCanvasAndCamera。它们不算PASS，也不替代Owner视觉验收。

P11 DetourAtVariableFrameRateMustArrive 在首轮完整Play PASS、第二轮timeout FAIL、独立补跑PASS、最终第三轮完整Play PASS。记录为运行波动，尚无确定根因；本轮没有修改P11 movement/collision policy或放宽该断言。最终0FAIL并不证明这种波动从此消失。

## 收尾状态（2026-10-07更新）

- 旧快照最终完整EditMode与PlayMode均PASS，XML已保存；本次试玩修订边界见上节。
- Engineering/QA原Important、M3缩队门禁Important及本轮final Facing Important均已关闭；本轮Engineering无阻断，QA核对当前完整Play自动验收通过。当前回归与历史快照区分见匀速修复节；Phase最终完整Edit待视觉调整确定后集中执行。
- Owner已完成更新后的M1～M8，8/8 Owner PASS；无需继续等待人工验收。Codex历史Computer Use未执行步骤不会因此变成代理PASS，最新用户验收见本文顶部与[Beginner Guide](Phase12_Beginner_Guide.md)。
- Domain Reload off双Play已自动PASS；Android/iOS真机、Player build：NOT_RUN。
- E-012强制ID初始化失败、Instantiate异常注入：NOT_RUN；Register rollback不替代它们。
- Owner acceptance：Completed，8/8 PASS。当前代码的Phase最终完整regression/必要修订复核待收尾；commit/push/merge：NOT_RUN。

旧快照曾恢复31项测试生成的无关material/URP/UI/旧验证scene变化。本轮清理数量见M3修复节；保留MainCafe/P12验证scene及代码、测试、文档。
