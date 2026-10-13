# Phase 12 — Customer Queue Test Cases

> Approved — Owner 已批准并授权实施。2026-10-04（America/Toronto）。
> 对应 [design](2026-10-04-phase-12-customer-queue-design.md) 与 [plan](../plans/2026-10-04-phase-12-customer-queue.md)。以下为验收要求；自动结果见 [Validation Report](../../Phase12_Validation_Report.md)。最新人工状态：Decor reset版M-001～008于2026-10-07全部Owner PASS；桌角修复于2026-10-08 Owner PASS。新增并行/同弯入店观察Pending，后文旧结果为当时快照。

## 1. 测试方法与 files

Domain EditMode 使用可注入 random、slot validity 和 movement 结果，不以 fake test 替代真实 NavMesh/Collider。PlayMode 使用 Shiba/Westie 正式 prefab、真实 P11 world 和装修桥。一个 Unity test process 串行运行；Task focused + 直接 regression，Phase 末完整 suite。

- `Assets/Tests/EditMode/Phase12/CustomerSpawnClockTests.cs`
- `Assets/Tests/EditMode/Phase12/CounterQueueTests.cs`
- `Assets/Tests/EditMode/Phase12/CustomerAdmissionTests.cs`
- `Assets/Tests/PlayMode/EditorSceneLoading/Phase12/CustomerQueueFlowTests.cs`
- `Assets/Tests/PlayMode/EditorSceneLoading/Phase12/Phase12CustomerQueueSceneTests.cs`
- P11 runtime ID regression 扩展既有 `Assets/Tests/PlayMode/EditorSceneLoading/Phase11/NavigationMovementTests.cs`，身份拒绝/一次性规则另用 NavigationRuntimeIdentityTests；真实注册扩展既有 movement class。

## 2. 自动测试矩阵

| ID | 范围/输入 | 必须断言 | Task |
|---|---|---|---|
| E-001 | random 取边界 0/1 和中间值 | interval 为 1/5/3 游戏秒，均匀映射；非法 random 明确拒绝 | 2 |
| E-002 | 0 delta/暂停/1x/2x | 暂停不减剩余；同游戏时间相同 spawn 数；无双重缩放 | 2 |
| E-003 | Tick 30 秒 | 最多保留一个到期机会，不 catch-up；成功接纳后才启动下一完整interval | 2 |
| E-004 | 满额/空间用尽/装修、临时入口占用后恢复 | 满额/装修取消后重抽，无backlog；临时入口受阻保留计时/到期机会；手动暂停保留且不生成 | 2 |
| E-005 | NaN/Infinity/负 delta、非法间距 | 明确拒绝且原状态不变 | 2/3 |
| E-006 | 同向无遮挡 slots、边缘剩余不足一步 | 0是Customer Side；最小间距1.0m，边缘直段均匀分布；四朝向一致，不移动段起点；不可达重排完整回退，采样高度不改变水平队形 | 3 |
| E-007 | 前方障碍，左/右均可用；先选分支死路 | 优先直/左/右，同长度先左；死路有限回退换方向，四种旋转一致 | 3 |
| E-008 | 三向阻塞/入口 clearance/Pick-up顾客位置；空Employee anchor | 预算内保留最长已验证链，不立即回头、不重复、不降低间距；空员工位可排队，实际角色不重叠 | 3 |
| E-009 | 多 register/随机枚举顺序 | 稳定 InstanceId 首选；无有效 register 阻塞 | 3 |
| E-010 | A/B/C 入队，A离场，重复请求 | FIFO不交换，按钮不能跳过未到队首或重入退出 | 3 |
| E-011 | 前移 B 尚未 Arrived，C安全跟进 | 每位独立任务、FIFO目标；允许真实移动重叠，不侵占前客目标或超车；前客失败时停止活动后客 | 3/4 |
| E-012 | 三项 reserve 失败/ID失败/instantiate失败/register失败 | 0 新 visit、0 残留 actor，三项 ledger 回到初始 | 4 |
| E-013 | 成功发布后首次 MoveTo 同步失败 | 一位 Blocked；token/assignment仍在，request不被返回值错误复活 | 4 |
| E-014 | 旧 callback 晚到/旧 generation/LayoutChanged | 不释放新 token、不修改新 assignment、不报 Arrived | 4/5 |
| E-015 | 已入店各状态失败 | Total不释放；Counter/Pick-up按原状态保留，无负数 | 4 |
| E-016 | 实际离开柜台但尚未到出口 | Counter已释放，未使用Pick-up已取消，Total仍占用 | 4 |
| E-017 | 出口 Arrived/重复 cleanup/session teardown | 正确释放exactly once，旧 owner无法释放他人token | 4/5 |
| E-018 | Floor缩小/恢复；3位顾客只剩2个slots但安全出口有效 | 原位与FIFO保留，停止admission，允许队首逐位安全离场并恢复正常分配；无全局queue时间死锁、强制补齐或驱逐 | 5 |
| E-019 | 到期后 attempt暂时失败 | 人数与容量不增加，计时保持0而不刷新；安全门禁恢复后接纳一位，成功后再抽下一轮，原因可见 | 2/4 |
| P-001 | runtime克隆两只Shiba及两只Westie | 唯一ID全部注册；空ID/重复初始化/注册后改ID失败 | 1 |
| P-002 | 真prefab入店并排队 | 真实Arrived才Queued，正确Walk/Hold/转向，无瞬移 | 4/6 |
| P-003 | 1秒到店密集入口、FIFO前移、弯角 | 每帧代理分离，墙/家具连续轨迹不穿透，容量从不超发 | 4/6 |
| P-004 | 点击队首离开 | 实际离位/出口到达分别释放；后方按顺序抵达 | 4/6 |
| P-005 | 堵入口/尾slot/出口 | admission暂停；离场失败保留Total和位置；修复后真实到达 | 4/5 |
| P-006 | 0x/1x/2x与15/20/30/60/120fps | 2x下2个真实秒推进约4个游戏秒，容差为2个测试帧；不要求不同帧率随机轨迹逐帧相同 | 2/6 |
| P-007 | 顾客存在时装修移动/旋转register | 正常Done后旧顾客/queue/request/token清理；新ID与完整随机间隔重新入店，非顾客actor保留；未修改/取消Preview/普通Pause/disable不reset | 5 |
| P-008 | flow或world disable/enable | proxy/token保留，旧callback不可复活；启用后验证 | 5 |
| P-009 | destroy/unload/重复Play含Domain Reload off | 无旧actor/request/subscription泄漏、无重复spawn/owner | 5/6 |
| P-010 | 两次运行P12scene builder | 单套World/Adapter/Bridge/Flow；不会改写P11scene | 6 |
| P-011 | MainCafe营业接线与退出装修gate | invalid business不继续计时；已有Decor操作正常 | 6 |
| P-012 | 30游戏分钟生成/受控离场循环 | active objects、容量和queue归零可重复；记录released token历史增长与耗时，不宣称无限运行通过 | 6 |

几何断言使用 P11 的正式 proxy/skin/epsilon，不把 mesh轻微短暂边缘接触自动判为穿墙，也不能只看最后一帧。Fail/Cancel/LayoutChanged/Recovered 必须各自检查；“请求结束”不能代替“到达”。

2026-10-07边缘对齐：已有CounterQueueTests增加9个domain cases（四朝向、不可达回退、不足最小间距、采样高度、短段超过25%调整必须回退、满容量不再延伸末段）；三项并发与两项朝向Flow测试改为实际新slot目标到达/最小间距断言，保留预期左/右转方向、朝向、并发帧数、FIFO、连续分离及暂停/取消。NativeEdgeAlignedRowReachesCornerCellAndDrains先在空店Confirm/Done将Cash移到Grid(0,5)，验证角落cell(0,0)有安全目标，五位真实入店并逐位离场；NativeEdgeAlignedThreeCustomerReflowThenFiveArrivalsDrain在已有三人时同样移动，必须到达新assignment，再补两位并排空。已有五人形成墙边屏障后移动到另一侧的探索case仍Blocked，不列为五人营业重排PASS；改变其运动调度不在本次边缘对齐范围。真实身体占位保护沿用原World/Flow，不放宽碰撞或更改FIFO。

## 3. 直接 regression

- Task 1：NavigationMovement/Animation/Asset 与真实 prefab ID/重新启用用例。
- Task 2–3：clock/queue focused；不启动不相关旧Phase全量。
- Task 4：P10 Capacity reservation/lifecycle、P11 movement callbacks/collision与P12 admission。
- Task 5：NavigationLayoutIntegration、装修ready gate、time control、P12 recovery/teardown。
- Task 6：P12 saved scene、MainCafe真实Decor按钮、scene-owner validator；Phase末完整EditMode/PlayMode。

## 4. Owner 手动验收

入口：`Assets/Scenes/Validation/Phase12CustomerQueue.unity`；Stop→Play前检查Console无编译错误，使用1x。HUD显示选择的Register、spawn剩余游戏秒、顾客状态、三项容量、阻塞原因。以下为2026-10-07 Decor reset后的当前矩阵，详细操作见[Beginner Guide](../../Phase12_Beginner_Guide.md)。

| ID | 操作 | PASS条件 | 完成状态 |
|---|---|---|---|
| M-001 | 默认布局1x观察入店约一分钟 | 可接纳时1～5游戏秒随机interval；容量/入口/实际路线冲突受限可等待，不重叠强塞，不要求固定真实秒出客 | 此前 Owner PASS · 2026-10-07；并发补测Pending |
| M-002 | 默认第三位；装修场边布局后等新客直排/转弯 | 第三位在Westie后方，队首朝柜台、后客朝前客；安全转弯、顺序稳定、间距自然，无穿透；不要求每格都可站或任意布局八人 | Owner PASS · 2026-10-07 |
| M-003 | 四位Queued后离场，移动中重复点击，再等稳定继续，累计三位 | 正确离位/离店；安全时后客实际同时前移，最后尾客到站、FIFO保持；重复点击无双重退出，容量按实际释放 | 此前 Owner PASS · 2026-10-07；并发补测Pending |
| M-004 | 等容量/空间限制停止生成，再让一位离开 | 中文原因明确，允许接纳后重新interval，逐次补客；不要求固定八人 | 此前 Owner PASS · 2026-10-07；并发补测Pending |
| M-005 | 移动及倒计时中Pause→1x→2x | 暂停实际pose/时钟，恢复原位连续移动、不清客，2x跟随游戏时间 | Owner PASS · 2026-10-07 |
| M-006 | 三～五客后Confirm修改→Done；另测无修改、取消未确认Preview、Preview阻止Done、重复Done | Confirm仍打开时保留；正常完成确认修改才清旧visits/request/queue/token，完整1～5游戏秒新入店并可离场；未完成/无确认修改不reset，重复完成不删新客。独立actor保留由自动测试验证，P13任务人工验收暂不适用 | Owner PASS · 2026-10-07 |
| M-007 | Confirm堵路→Done，再Confirm打开→Done恢复；普通营业Blocked出现时另试未改布局的重试 | 已确认Done清旧批次，无效布局/安全路线门禁保持，修好后新批次恢复；普通Blocked保留Total、原位重试，不保证无布局变化必成功。Blocked未出现时补充记NOT_RUN，非整项N/A | Owner PASS · 2026-10-07 |
| M-008 | P12两次Stop/Play；MainCafe目录、Preview/Cancel/Done | 原有装修正常，无重复顾客/owner或Console exception；MainCafe初始不营业，有效服务布局后才生成 | Owner PASS · 2026-10-07 |

M1～M8没有整项废除。被替代的旧要求是：M6正常已确认Done后旧顾客原位重排；Decor缩队保留多出的旧尾客；M7再次确认装修修复后旧Blocked身份/容量延续。当前统一为reset后新批次入店。普通Pause/Flow disable/非Decor revision/repair的保留恢复合同继续适用。员工工作/任务属于P13，本轮不要求Owner造员工做人工验收；非顾客隔离自动证据不替代P13验收。

Owner于2026-10-07确认“所有都完成了”，本节M-001～M-008全部记录为**Owner PASS，8/8，Completed**，依据是用户明确验收，不是自动测试推断。M7-B仅在Blocked发生时执行，用户未单独提供该分支触发证据，不将它改写成已运行；P13员工任务不在范围。P11横幅残影和Westie低对比继续观察，不重复已完成P11整套人工验收。

## 5. 结果与证据边界

当前M1～M8人工验收8/8 Owner PASS；旧M3 FAIL待重测及M4～8 NOT_RUN记录已由2026-10-07整体接受更新。旧快照实现、QA补测与完整回归结果保留历史；最新代码的Phase最终完整回归仍待收尾，统一见Validation Report。Android/iOS真机与Player build本轮未执行，须单独记录。文档静态检查不等于运行验收。

## 2026-10-07追加：并行入店（Owner已批准，直接自动回归PASS）

- AdmissionCanReserveStableTailWhileEarlierCustomerAdvances：前客尚未Arrived，但FIFO目标已经重新分配稳定，后客可预留独立尾位；未分配的旧slot仍拒绝，旧generation不可提交。
- ExitingHeadKeepsItsSlotUntilRemovedBeforeAdmission：离场者未实际离位前禁止复用其slot；移除queue entry后才可接纳。
- ConcurrentAdmissionDuringEarlierEntryMovesBothAndDrains：真实Shiba/Westie，1秒注入clock自动接纳第二位，两个Entering至少12帧都有实际位移，最后真实到站/FIFO排空/容量归零。
- ConcurrentAdmissionDuringExitMovesBothAndDrains：离场者尚未到出口，1秒clock接纳新客；Entering与Exiting至少12帧共同实际移动，离场按钮仍不能重复退出。
- ConcurrentAdmissionDuringAdvanceUsesVacatedTailAndDrains：3m验证slot隔开后，前客真实Advancing时接纳新尾客；两者实际移动至少12帧，正确到站和FIFO排空。该fixture不是修改生产1.0m队距。
- ConcurrentAdmissionWaitsForOpposingExitRouteWithoutReservingCapacity：进出共用路线，入口当前已空但已有离场路线仍需经过；新客被拒绝且所有容量不变；离场完成后无需布局修改即可入店。
- ConcurrentAdmissionWaitsWhenTailWouldBlockEarlierEntryThenRecovers：默认斜向入口时，新队尾距前客剩余路线约0.71m；必须拒绝接纳且不占容量。前客到站后恢复，不通过放宽半径使并发测试通过。
- ConcurrentAdmissionPauseDisableKeepsActorsAndResumes：两位Entering已有12帧同步位移时Pause，再Flow disable/enable；身份、实际pose与三项容量保留，恢复真实到站并排空。
- 三类并发逐帧检查连续身体间距≥0.909m、速度上限/无瞬移、无Blocked、最终实际到站和容量归零；既有真实保存场景回归继续检查家具skin、角落、Decor reset与空员工位。

既有M1～M8的8/8 Owner PASS是并行入店修改前的验收快照，不能自动代表新行为已人工接受。只追加M1/M3/M4的并发观察：已有角色移动时安全新客仍能入店，冲突时等待后恢复，队形/容量正确；其余Owner结果保留，不要求整套重测。

当前fresh证据：focused6 PASS；直接P12 Play65、P11 Play31、P12/P10 Edit155均0 FAIL/0 SKIP，具体XML及fixture失败区分见Report最新段落。新行为人工Pending，Phase最终完整suite、Player/真机与长时soak本轮NOT_RUN。


## 自动 case 对应关系

以下区分直接覆盖和组合/部分覆盖，suite总数不是31条设计case的逐项全覆盖声明。

| Cases | 实际证据/边界 |
|---|---|
| E-001～005、E-019 | CustomerSpawnClockTests：边界、暂停/阻塞、新interval、long frame、非法输入和失败attempt完整间隔；创建失败中文原因由Register rejection测试检查 |
| E-006～008 | CounterQueueTests直线/相对左右/截断/不回环；真实Corner flow；真实layout保留Pick-up Customer安全空间，允许空Employee anchor，真实占位角色仍拒绝admission。EntranceClearanceAllFourCellsRemainWalkableRoutes逐格真实MoveTo/Arrived，证明入口放置禁区仍可寻路；等待slot排除与路线通行分开检查，未穷举所有入口形状 |
| E-009 | StableRegisterSelectionAndPickupAnchorExclusionUseRealLayout：低ID后插入仍优先；默认未营业MainCafe属于无有效register gate回归 |
| E-010～011 | ForwardAssignmentsCanOverlapAndKeepFifo：前客未Complete时分配后客，独立回调可先后完成而顺序不交换；RestartConcurrentAssignmentsRejectsEachOldGeneration：并发任务恢复后各旧generation拒绝；真实多人并发前移与缩slot排空 |
| E-012 | lease完整reserve/Dispose及真实Register拒绝rollback PASS；强制ID初始化失败/Instantiate异常注入 NOT_RUN |
| E-013～015 | 同步Exit失败保留Total、Send callback重入guard；真实revision/rebuild/disable恢复；首次Entering同步MoveTo拒绝和人为晚到callback没有独立强制注入，属于部分覆盖 |
| E-016～017 | 实际离位但尚未Exit Arrived三ledger断言、真实出口清理、重复remove、两session teardown |
| E-018 | ThreeCustomersShrinkToTwoSlotsDrainAndRecover；ShrinkDuringThirdEntryKeepsBlockedTailButAllowsSafeFrontExit：第三位Entering中真实floor revision→Counter两slot→尾客Blocked保留Total→安全队首离场→FIFO恢复排空 |
| P-001～005 | 四实例两species同时注册、realprefab队伍、真实Collider弯角逐帧entry/forward/exit、动态tail拒绝、出口失败修复；新增默认密集/分散真实scene四人逐位离场，逐帧位移上限、连续角色分离、家具间距；SharedLane验证Entering门禁及Exiting/Advancing并存。不是所有店铺形状的穷举证明；动态route重算耗尽没有独立强制注入 |
| P-006～009 | 15/30/60/120fps速度、layout revision改变队首、flow/world disable、P12 Domain Reload off双session；NativeDetourIntermediateArrival验证中间Arrived仍Entering、Total/Counter未提前变化，disable原位并rebuild后真实恢复。人为迟到callback仍为部分覆盖；装修真实HUD入口可用性另有scene test，视觉接受仍属manual |
| P-010～012 | builder重复且P11scene字节不变；MainCafe完整回归；30游戏分钟controlled soak |

最终 counts 和 XML 见 [Validation Report](../../Phase12_Validation_Report.md)。M-001旧版本Owner PASS；M-002后方朝向PASS；M-003修复后待Owner重测，M-004～008仍由Owner逐项验收；PARTIAL/NOT_RUN不会折算为PASS。

2026-10-06 Computer Use记录：Codex已实际操作Unity完成M-001/003/004/005/008的下述机械检查；M-002/006/007拖家具构造布局的步骤仍未验证，另已观察旋转队伍恢复与撤下/恢复PickUp保留原8位顾客。精确范围见Report第一节；Button.onClick/内部装修handler自动覆盖不替代实际鼠标拖动。Owner先批准Editor-only鼠标观察，随后明确不必过度测试、复杂视觉可自行验证、不为测试开发很多代码，因此取消诊断扩展（没有新增Editor文件），中止额外全量Edit并恢复资源副作用。核心机械结果可交玩家验收，余下布局/视觉步骤保留未验证；不代签Owner结果，不把中止运行当PASS。

2026-10-06匀速模式追加（Owner已批准，结果以Report为准）：

- P-003/P-004：密集与分散五人入队、反复离场/补客、逐位FIFO排空；cash-only移动保留其他柜台位置；截图估算Cash `(2,6)` / Coffee `(5,4)` / Pick-up `(6,6)`，不声明精确复现Owner坐标。
- P-004/P-007：真实HUD离场按钮、真实AutoSpawn时钟；营业中通过Decoration Enter/Preview/Confirm/Done移动柜台后恢复与补客。到站条件连续两帧，避免回调间一帧Queued间隙掩盖尾客仍待前移。
- P-003/P-006：cash-only使用真实Editor帧时序（captureDeltaTime=0）和15/30/120fps；空路匀速断言持续减速不超过0.2游戏秒。逐帧位移上限、整步连续角色分离含skin、家具skin检查。
- P11直接回归：默认/steady混用宽路到达与窄路安全失败；corner连接不能跨墙/穿静止disabled角色；近于单帧行程的最终目标不越过且Facing正确；暂停、Cancel、disable/rebind、新目标不会复活旧corners；大帧有界且无catch-up。

2026-10-06门口修正追加：NativeCounterBesideEntranceStillAllowsQueueAndFrontDeparture在入店前摆放Grid `(2,0)` 空柜台，三位真prefab逐位Queued、离场、前移并排空；沿途检查连续分离、家具间距、位移上限及容量释放。RED为第二位admission误拒绝，移除路线计算中额外ArrivalDistance后GREEN。fixture属于截图相近布局，原截图具体坐标/旋转及操作时机仍需Owner复测，不扩张为所有摆法PASS。

2026-10-06 M6/窄路追加：NativeFullQueueCashMovedDuringBusinessRecoversAvailableSlotsAndDrains先将收银柜台旋转180°，满8人后通过真实Confirm/Done移至Grid `(4,6)` 并旋转回原方向；逐位验证新站位与最终排空，不强求入口被占用时补客。NativeFurnitureEdgeWaypointContinuesFromActualPoseToQueueSlot在真实保存场景中选取native家具拐点，指定两段临时route，使用真实角色、Service与World验证中间到达后从实际pose继续；不模拟Arrived、不写Transform，逐帧检查完整skin和位移上限。两者都不代表Owner原截图精确坐标；Owner M6 FAIL修复后待重测，当前结果见Report。

2026-10-06 图2～4/空员工位规则追加（Owner已批准）：Cash移至Grid `(4,5)`、`(6,5)`、`(7,6)`，分别在五人已Queued与第五位Entering时通过真实Decoration/Preview/Confirm/Done提交。六项Native场景测试必须先证明新revision有至少五个slots，再逐位检查assignment等于新slot、实际抵达、FIFO排空和容量归零，沿途保留连续分离/家具skin/位移上限断言。旧Queued状态不得作为重排成功。StableRegisterSelectionAndPickupAnchorExclusionUseRealLayout检查Pick-up Customer留空，并注册真实角色占住目标验证入队拒绝且未reserve容量；NoNewQueueSlotsReportsSpaceAndRetainsActualCustomer检查0/1站位提示、身份/实际位置/容量保留。近似坐标不等于Owner原图精确布局；结果统一见Report。

历史（场边回退前）：空工作位规则下满队Cash `(4,6)` 的第八个候选被家具和前方队伍围死。候选生成增加此前slots占位下的入口可达性检查后，当时NativeFullQueueCashMovedDuringBusinessRecoversAvailableSlotsAndDrains验证七个安全slots：七位真实抵达新assignment，第八位保留原位置/旧Queued assignment且Total仍8，HUD明确7/8并拒绝接纳；真实队首离场后尾客继续FIFO前移，最终八位全部真实离店、容量归零。此为旧贪心分支的E-018/P-007证据，本轮回退已找到另一条八站位链，见下方最新case。真正空间不足仍不得删除尾客规避失败。

2026-10-06 并发前移/装修重排追加（Owner批准“按这个范围修改”）：

- StraightQueueFollowersMoveTogetherBeforeLeaderArrives：四位真prefab排好后离场，至少12个60fps测试帧中两位Advancing实际位移均大于0.001m；逐帧完整分离、位移上限和直队前后顺序，最终实际新站位正确。
- LayoutReflowMovesIndependentlyAndSurvivesPauseDisable：validationHead移动3m，显式disable/enable触发真实重排，至少12帧实际并发；同时移动时Pause五帧与flow disable保留全部实际位置/容量，恢复后各自抵达，FIFO排空。validation marker不属于layout实体，不能仅靠不变geometry的Rebuild冒充新revision。
- NativeDenseFiveFollowersMoveTogetherThroughCornersAndDrain：真实保存场景五位弯队至少12帧实际并发，原逐帧连续角色/家具skin/位移上限及FIFO排空断言保留。
- FailedConcurrentLeaderHoldsFollowersUntilRepair：用真实Service.Cancel制造前客失败，已活动后客请求退休、位置与Total保留；修复后各自从实际位置抵达。没有模拟callback或写角色Transform。
- 只更改已有队伍前移与重排，不改变新到店接纳/离场按钮门禁。精确结果及fixture修正统一见Validation Report；自动结果不替代Owner动作手感或原图准确布局验收。

并发直接回归继续检查原Cash-only 15/30/120fps、真实Editor时序、后方Pick-up绕行及满八人缩队：后客未来目标不得挡住前客，交叉/对向路径或提前占住前方通道须延后提交，普通跟队仍须满足实际并发帧数。CounterQueue domain还检查安全门禁拒绝时assignment/generation完全保留，腾空后仍按FIFO提交。原0.909m连续分离、家具skin、速度上限及最终新slot到达断言不放宽；失败过程与最终XML见Report。

2026-10-06 场边队形有限回退（Owner批准先试）：

- BoundaryDeadEndRetriesTheOtherTurnWithoutCrowding：0/90/180/270°四朝向，第一左分支三站位，右侧实际存在六站位；期待找到六位、相邻1.1m、全体不重复/不挤压。旧规则四项可信RED。
- BoundarySearchIsFiniteAndDeterministicWhenCapacityCannotFit：有限区域请求100位，无论能否填满最多256候选加head，同输入相同队形，保留可验证部分。
- BacktrackingReachabilityUsesOnlyTheCurrentQueuePrefix：放弃左分支后右分支的虚拟占位只有共同前缀，不能残留旧候选；candidate本身未加入已占位列表。
- NativeBoundaryReflowFindsEightAccessibleSlotsAndDrains：真实场景先旋转收银台向后墙，满八人后真实Confirm/Done移至Grid(4,6)、旋回原方向。旧规则仅七站位RED；有限回退后必须八个新站位，每位实际到达各自新assignment，FIFO真实离场、容量归零。沿途保留原连续0.909m分离、家具skin和速度上限断言，不以八个逻辑名额代替八位到达。
- 原满八人case继续适用于空间不足合同，但上述布局本轮已有另一条八站位链；此前七位与7/8提示是旧贪心规则历史，不能继续称为该布局必然只有七位。真正空间不足由ThreeCustomersShrinkToTwoSlotsDrainAndRecover等原用例继续覆盖。原图一准确位置/边缘视觉仍需Owner复测；1.1m间距和P11半径不变。

## 2026-10-07：1.0m紧凑间距与入口身体保护

Owner批准1.0m及0.911/0.991m入口保护试用。DefaultSpacingFitsThirdCustomerBehindSecondAtSafeFloorEdge验证三位在距边界2m的直段中不提前转弯；NativeDefaultThirdQueuesBehindWestieThenEightCustomersDrain在原保存验证场景不移动家具，验证第二/第三实际位置、补到八位、FIFO离场/独立前移、完整连续身体分离与家具skin、容量归零。原1.1m domain fixtures明确传入自定义spacing，继续覆盖有限回退和边缘调整；默认Flow间距断言更新为1.0～1.25m。临时绕行/disable回归改用第四位，第三位现在是直达尾位，不能再把它当作绕行fixture。所有原碰撞断言不放宽。

1～2秒停顿仍UNKNOWN；该新case不宣称验证每一帧无停顿。五人旧队伍跨墙边屏障重排仍OPEN。本轮不改P11参数、入口2x2通行或等待slot政策，不移动spawn/exit，不增加工具或长时间soak。
## 2026-10-07：已确认装修完成后仅reset顾客

Owner批准新规则，替代真实Decor完成后的旧顾客重排验收；非Decor的layout revision、disable和路径repair保留旧顾客。新增NativeDecorResetCustomersOnlyAfterConfirmedDoneAndRestartsClock：真实保存场景三客与非顾客actor；无修改Done、未完成Preview拦截Done、取消Preview均保留；Confirm仍打开时保留；两轮正常Done只清顾客和三项token，保留原Capacity对象/worker实际pose与注册；新两秒注入interval完整计时、新visit ID、重复Done/Step幂等，最后新顾客真实离场与容量归零。worker只是独立NavigationActor替身，不代表P13员工任务已实现。

NativeDecorResetWithInvalidBusinessKeepsAdmissionStopped：移除确认Coffee功能后完成Decor，旧顾客批次清理但readiness仍无效，clock为0且TrySpawn拒绝。既有真实营业移动/Entering/满八人/边缘helper改为验证旧ID/请求/token归零，再接纳新队伍并保持原actual目标、FIFO排空、连续身体/家具skin检查。不为新规则保留“旧尾客跨屏障必须重排”的期待，也不把它标记为导航修复。

NativeFiveCustomerWallBarrierDecorResetReopensAndDrains：默认五位旧客Queued，真实Confirm/Done将Cash移至Grid(0,5)；旧批次与三项容量清空，五位新客逐次自动入店、抵达真实角落站位并FIFO排空。原墙边屏障的Decor旧客重排验收由新规则替代；非Decor保留旧客的导航能力没有因此改变。

满八人reset后的自动补客按每位原60游戏秒/90真实秒deadline分别等待，不把八次随机interval与路线总和当作单客超时；原连续碰撞、速度上限、真实站位与离场断言保持。最终证据见Report最新段落。

## 2026-10-07：桌角停顿回归（Owner已批准）

在现有保存场景测试中新增五项 `NativeDefaultCornerArrivalsContinue...`：15/60/240/1000fps 与实际 Editor 帧时间。默认布局、一秒注入到店间隔接纳五位，随后 FIFO 排空并确认全部容量 Used=0。保留每帧速度上限、完整家具 skin 和连续身体分离检查；不跳过桌角附近。

没有近邻身体挡路（中心距离>1.03m）、距离当前段目标>.25m时，实际速度低于 MaxSpeed 的25%不得持续超过 .2 游戏秒，检查入店、前移、离场；最终到站/转身和真实近邻避让不套用此停顿断言。既有空旷路线90%速度检查仍保持。这是针对持续停住/严重慢行的回归，不宣称每个渲染帧位移都相同。

实际帧时间与1000fps在修复前分别于桌子两侧触发可信RED；原无进展三秒重试不能掩盖失败。修复后五项focused通过；已有布局、并发、默认/匀速混合、墙/静止身体阻挡、暂停/重绑/取消、短最终目标与Facing的直接回归结果见Report。原M1～M8 Owner PASS保留为此前快照。Owner于2026-10-08确认“拐弯的问题修改好了”，该桌角视觉项记Owner PASS；后续新增同弯入店需独立观察。

## 2026-10-08：同弯连续入店（Owner已批准）

- `NativeDefaultFifthArrivalFollowsFourthAroundCounterBeforeItQueues`：真实保存默认场景先三位Queued，第四位还在左桌角Entering时注入一秒clock并Step至到店期满；第五位必须立即成功入店，第四位尚未Queued。至少12帧两位Entering实际同时位移；沿用速度上限、家具skin、连续0.909m身体分离检查，全部真实Queued、FIFO排空、三项Used=0。旧行为在人数应为5却仍为4处可信RED。
- `ConcurrentAdmissionWaitsForCrossingEntryRouteWithoutReservingCapacity`：第一位沿横向仍在入店，第二位新路线纵向穿过其剩余路径；拒绝创建/容量预留，前客到站后恢复并最终排空。原对向离场、队尾堵前客、入店/离场/前移并发及暂停/disable回归保留。
- 本轮仅修正路线门禁，不改clock/身体参数，不新增诊断工具或长时测试。先重点8项与相关直接103项通过；末端对齐条件收紧后，最终源码8项＋五种帧时间桌角检查合计13 PASS/0 FAIL/0 SKIP，未重复全103项，源码时点见Report。Owner无需重做M1～M8，仅观察到店期满时同弯新客可在前客行走中入场。

## 2026-10-08：队尾临时占位允许安全入店（Owner已批准）

- `NativeDefaultEighthArrivesWhileSeventhTemporarilyPassesTail`：默认保存场景先六位Queued，第七位实际经过第八个目标的0.98m范围内、开始腾位时注入一秒到店；第七位仍Entering时第八位必须成功入场。至少12帧双方实际位移，全部真实入队/FIFO排空及Used=0；每帧速度上限、家具skin和连续0.909m身体分离沿用原helper。
- `ConcurrentAdmissionAtTemporarilyOccupiedTailFollowsMovingEntryAndDrains`：真实prefab沿直线入店，前客越过未来尾位、仍在0.98m内行走时允许后客安全跟随，至少12帧并行、最终入队排空。2026-10-08曾加入“接近未来尾位时一律拒绝”，当时单独1 PASS；2026-10-09批准安全顺序跟随后移除该过度保守断言，以新增真实并行回归替代。静止/外部人物和真实交叉等保护未删除。
- 原 `DynamicallyOccupiedTailRejectsAdmissionBeforeCapacityReservation` 继续检查静止外部人物占位拒绝，原入店目标堵住前客/真实横穿/对向/暂停与disable cases保持。本轮RED为上述两个新case真实接纳失败，focused11 PASS；最终runtime直接105 PASS/0 FAIL/0 SKIP，证据见Report。Owner只需观察七到八人的入场，不重做M1～M8。

## 2026-10-09：连续进客到期机会与安全跟随（Owner已批准）

- Clock：失败后剩余0秒，不重新抽随机数，只保留一位；成功通知后开始新间隔；零游戏时间不生成，长帧不积压；满员/装修清理后恢复完整间隔。替换此前“失败后再等完整interval”的用例，不保留过期预期。
- `DueArrivalWaitsForEntranceThenSpawnsWithoutAnotherCountdown`：外部角色占住入口，到期无顾客/无容量预留；暂停并清理入口时仍不生成，恢复后用0.001秒Step即可接纳一位并显示下一段1秒，真实到站排空。
- `SafeFollowingEntryMayApproachFutureTailBeforePredecessorPassesIt`：前客尚未穿过未来队尾，新客在同向且足够靠后的入口即可安全接纳；真实双角色并行、到站、FIFO排空与碰撞检查通过。
- `NativeDefaultUnattendedOneSecondArrivalsDoNotResetFailedCountdown` / `NativeDefaultUnattendedTwoSecondArrivalsDoNotResetFailedCountdown`：保存默认场景不改家具、不手动接纳，固定范围内1秒/2秒用于复现。人数不增加时计时不能重新升高；第七/第八位出现时前客须仍Entering且距目标超过2m，至少12帧双角色实际运动，最终八位Queued、FIFO排空、三项Used=0。沿用每帧速度上限、家具skin与连续0.909m身体分离。
- 原横穿、对向、目标堵住前客、静止外部角色、暂停/disable和Decor仅reset顾客回归继续执行。RED/GREEN与当前直接回归证据见Validation Report；人工仅补默认Play到八位的计时与跟随观察。

修改前发现：`QueueDrainsAtTwentyFpsWithoutAnyConcurrentAdmission`在20fps、AutoSpawn关闭、六位逐个Queued后才离场时复现原生路径提交失败，独立于当前计时与并发入场。原30游戏分钟soak也失败，作为RED证据保留。

## 2026-10-09：20fps原生路径恢复（Owner已批准）

- 原无并发20fps排空case加强为逐帧位移不超过MaxSpeed×delta＋0.002m、连续身体分离≥0.909m、当前位置仍在owned NavMesh、FIFO身份不变；提交离场当刻所有Transform必须保持原位，排空后三项Used为0。
- 原30游戏分钟soak必须完整执行36000帧（每帧0.05游戏秒），期间没有Blocked、容量不超额，最后真实排空；不以原地恢复的诊断probe代替行为PASS。
- `NativeCashMovedOnlyFiveCustomersDrainAtTwentyFps`复用真实场景cash-only五人离场/补客流程及原有家具skin、角色分离、速度上限检查；与15/30/120fps、真实Editor时序cases一并回归。
- 计时1x/2x/暂停矩阵加入20fps；原窄路、墙体、无效起点/目标、静止身体、对向/横穿、取消/disable与装修reset保护仍必须通过。实际执行结果见Validation Report。
