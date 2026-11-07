# Phase 15 — Business Day Lifecycle Validation Report

<!-- P15_STOPPED_ARRIVAL_CLOSEOUT_20261107_BEGIN -->
> **P15 开发验收已通过，进入 PR 准备。** Owner 授权继续修复并跑新一轮精简回归，沿用上一轮 selectors，并由原 P15 namespace 纳入 1 条新增 regression。最终 EditMode **1388/1388 PASS**，其中 P15 68 项。PlayMode **90/90 PASS**，其中 P15 77 项。全部预期用例各执行一次，FAIL／SKIP／缺失／重复均为 0。对应 XML SHA256 为 `E8E25E3B5B9294378983AE34C2298909D763383946C324992E481543FED54E4B` 和 `189B7FC7E2B3BD9B9F6750D5D751487E7594B426AB8138B1AAB1946F167661E4`。
>
> **已修复停止自动来客后的残留预留。** 原 Main 失败时第 9 位尚未出生，AutoSpawn 已关闭，但其 Counter admission 继续准备并挡住第 8 单的送杯路线。新增 Native 测试先在旧产品源码得到准确 RED，关闭自动接客后仍保留原 anchor，XML `5FA5586C`。产品增量仅为 CustomerFlowController 的自动申请来源识别和生命周期处理，显式停止时复用既有未出生退休检查，准备回调之后重验当前来源，在安全 finally／refresh 边界重试清理。手动申请保留原对象、年龄与预算，继续经过真实准备和出生。
>
> **定向验证与最终回归均通过。** 新测试覆盖普通停止、准备回调中停止后无孤立 proposal、随后手动接客真实成功。与原普通 Main 一起 2/2 PASS，Main 7/7 实际离店排空，XML `688C87DA`。最终精简批次中 Main 3/3 实际离店、容量清空、两员工 Idle，原两组固定 1 秒／3 秒来客、2x 连续 20 单也通过。首次修后验证因新测试引用 internal API 产生 CS1061，记录为 NO_XML，改用既有只读 IDictionary 检查后才得到上述通过收据。
>
> 当前冻结源码为 Core `FBF884E94E77C7CD882229630506C5A1154DC291FBAB117699241FF9EBCA720A`、CustomerFlowController `E1F89C0C6C87C6291BE9B25F371074C41A58F3D5A1E3B77BEDFEEB8B96A61933`、Admission tests `29A013C24DF22360CF8395D26E852F6EE0ECC19014CCB6C3E0FB0D95D6DB252D`。独立限定 review 无剩余 Critical／Important。定向、Edit、Play 的 3192 Assets、469 source、3 inputs、28 Scenes 哈希逐项一致，收尾均无未知变化，Editor／worker 为 0。
>
> 开发收尾采用 Owner 批准的精简门槛。旧 distinct actual-exit 交接继续保留 NOT_REPRODUCED／NOT_PROVEN。全量 suite、真实八小时、移动真机、Owner 观感、CI、merge 与 merged-main 仍各有独立状态，正式阶段 Completed 等待后续 gates。GitHub Desktop 仍由 Owner commit／push，PR 当前待发布分支，CI 保持手动 workflow_dispatch。证据见[本轮结果](../outputs/phase15/p15-stopped-arrival-repair-20261107/results.json)。以下均为历史记录。
<!-- P15_STOPPED_ARRIVAL_CLOSEOUT_20261107_END -->

<!-- P15_COUNTER_REPAIR_REGRESSION_20261107_BEGIN -->
> **2026-11-07 UTC：原唯一失败已修复；精简回归出现另一项 MainCafe 失败，P15 仍 IN_PROGRESS。** 同一固定 1 秒来客、2x 的原用例先复现 FAIL，修复后专项 PASS；最终无临时观察代码的精简回归中也 PASS：20 位实际离店、原容量清空、两员工 Idle，原断言保留。
>
> 窄修复在 Core 与顾客流程：保持原有限等待，识别同一前客已获权路线的合法交接和中间腿；已认证取餐保护挡住当前送杯端点时，逐份核实当前身体和同一预留，只准尝试原候选备用侧，备用侧仍完整检查真实占用。未改碰撞、付款／容量、来客时间、Scene 或营业周期。
>
> **最终同源精简结果：EditMode 1388/1388 PASS（P15 68；XML 5B5E7E2C）；PlayMode 88 PASS／1 FAIL（89 项每项一次；P15 76/76 PASS；XML E5FAC203）。** 新失败是普通 MainCafe：第 8 位等待取餐，Barista 送杯因第 9 位尚未实际进店的 Prepared 收银站位占据规划路线被拒；原拒绝为 `owner-not-independent`／`current-parking-on-planned-route`。112.946 游戏秒，8 位入店／7 位实际离店，未排空。是否由本轮修复引入、与来客间隔是否有因果关系，均未证实。
>
> 按 Owner 要求停在新失败；不重跑、不进入 PR。旧 distinct actual-exit 仍 NOT_REPRODUCED／NOT_PROVEN。两批 3192 Assets、469 source、3 inputs、28 Scenes 哈希一致，结束后均无未知变化、无 Editor／worker；未进行 Git 写入。证据：[本轮结果](../outputs/phase15/p15-final-reduced-closeout-20261107/results-after-counter-repair.json)、[Main 原失败记录](../outputs/phase15/p15-final-reduced-closeout-20261107/main-blocked-after-counter-repair-output.txt)。以下为历史记录。
<!-- P15_COUNTER_REPAIR_REGRESSION_20261107_END -->

<!-- P15_REDUCED_CLOSEOUT_20261107_BEGIN -->
> **2026-11-07 UTC 最新精简回归：FAIL，P15 保持 IN_PROGRESS，未进入 PR。** Owner 授权“一次精简回归，通过则关闭 P15 开发并进入 PR”；本轮沿用既有 selectors，未修改产品或测试源码。EditMode **1388/1388 PASS**（P15 68；XML `ED754D36`，17.781s）；PlayMode **88 PASS／1 FAIL**（89 项精确覆盖、每项一次，无缺失／重复／额外；XML `E181AE0F`，298.402s），其中 P15 自身 **76/76 PASS**。
>
> 唯一失败：`CompletesTwentyRealOrdersAndDrainsAllCapacityAtTwoXWithOneSecondSpawnClock`。真实 2x、固定 1 秒来客的连续 20 单经营中，guest20 点单队列前移进入 Blocked，原因 `Counter traffic PlanningFailed: no-independent-finite-release-or-cycle`。冷记录的 blocker 为 guest19 当前 counter-advance 的 `selected-journey-parking-protection`；失败请求仅 2 queries、剩余等待约 29s。第一内部拒绝 predicate 尚 UNKNOWN，不把泛化 reason 冒充根因，也不推断与旧 actual-exit 是同一问题。
>
> 同轮正常随机 Main **9/9 实际离店、容量清空、两员工 Idle**；这不覆盖上述压力场景 FAIL。独立静态 review C0/I0 仍有效于其范围，但附带的当前回归通过门槛未满足。旧 actual-exit 仍 NOT_REPRODUCED／NOT_PROVEN；来客时序改变可能影响触发，因果未证实。
>
> 已停止本次回归，不自动修复／重跑；产品源码未改、未 commit／push／创建 PR。3192 Assets：Edit 变化2／恢复2、Play变化0、unknown0，Editor／worker0。记录：[完整本轮结果](../outputs/phase15/p15-final-reduced-closeout-20261107/results.json)、[唯一失败原输出](../outputs/phase15/p15-final-reduced-closeout-20261107/failures.json)。以下均为各自历史时点。
<!-- P15_REDUCED_CLOSEOUT_20261107_END -->

<!-- P15_MAIN_CURRENT_20261106_BEGIN -->
> **当前同一 Main paid 卡住专项（2026-11-06）：准确 Native RED→修复后同输入 GREEN；相关模型 980/980 PASS，正常 Main 单轮 8/8 实际离店排空 PASS。P15 仍 IN_PROGRESS。** 当前 Core `82DF7F7C`、Adapter `04F6F9CA`、CLC `93A1ED92`、Core tests `4936C988`、Main 正常通过时 fixture `6F899ACE`；后续 test-only exit gate 为 `F16FD95F`。
>
> **已复现并修复的第一原因：** 固定旧日志的合法四人来客输入，旧 Core 在 17.094s、4 born／1 actual exit 时失败（`6615B96D`）。第一拒绝 `new-frozen-owner`：guest1 真实离店后，guest2 真实 Bound／Committed 补位腿成为 guest3 未获权 paid 准备的新 body＋own-current-route 阻挡；旧 Frozen 仅有 guest1 actual-exit。查询 568，原剩余期限仍正，不是查询或等待耗尽；原 wildcard cache dropped=0。
>
> **窄修复：** 仅原未获权 paid 固定目标、真实 counter head arrived＋原 PQS 双 claim 均当前有效，且原 Frozen 的 actual-exit 已退休、对应全部身体／停车／路线真正消失时，对真实当前已发独立 Native 腿的 body＋同源 route alias 保留 Queued Deferred。沿用原有限债务、Frozen／当前旧边、原年龄／query／target／候选，不建立新 WAIT 边或许可；当前停车／canonical 保护、假 hint、失去 independence、身份变化仍 fail closed。真实移位后由原 driver 完整重证，Terminal 不复活。独立静态 review 无剩余 C/I。
>
> **验证：** 完全相同四人输入，修复后 4/4 实际离店，全部原容量释放、订单 Completed、两员工 Idle，65.713 scene／66.422 wall 秒（`CB1E6633`）。Core／PickupQueue／NavigationLayout **980/980 PASS**（`3AD86525`，2.1005s），新增 2 个正常 API 模型覆盖原 11 秒剩余债务到期、Pause、Frozen／年龄／query 不重置、同已完成 jobs 不新增 query、真实新腿完成后原固定目标 full reproof。产品预算／碰撞／Scene 均未改。
>
> **保留边界：** 先前自然三轮 7＋9＋8＝24 位全部实际离店与排空（`0AD71981`）发生于本次修复前；不能充当当前源码通过。旧 distinct actual-exit 失败 `B49CD294` 的第一拒绝仍未知，其旧来客时序这次 3/3 排空（`5815B2FB`）仅属 NOT_REPRODUCED，不宣称该旧边界已修复。当前 Core 正常 Main 冒烟 `AD4EEE02` 已 PASS：8/8 实际离店、原容量归零、两员工 Idle，129.618 scene／131.499 wall 秒。后续仅测试 fixture 增加 `exit-gate`，利用真实 Collected1、PQS head2 Arrived、第三位实际 Bound 腿的公开 collection 门控来观察旧 distinct Exit 窗口；产品仍为同一 `82DF7F7C`，该单次诊断 `0DB2D197` 已结束：3/3 实际离店与排空、无 Blocked；但要求的 Exit2 仍等待且 Pickup3 刚到达重叠未捕获，NUnit 因 coverage assertion 失败，分类 **NOT_PROVEN**，不能算该旧离店验收 PASS，也不是新增已确认产品故障。
>
> [当前状态与证据](../outputs/phase15/p15-closeout-20261104/main-delivery-20261105/state.json)。全量回归、手动／设备／真实八小时、CI、merge 与 P15 正式关闭未由这次专项完成。
> **2026-11-07 UTC 单次旧 Exit 再复现：NOT_REPRODUCED／NOT_PROVEN。** 按 Owner“未复现就告知”要求，仅增加测试输入 `exit-after-head`：第二位实际到柜台后开放第三位零间隔来客，普通自动取餐、1x、单次 90s 双时限。Native `69BEF797` 三人全部真实离店、原容量清空、员工 Idle，61.922 scene／63.082 wall 秒，无 Blocked；但指定 Exit2 等待跨 Pickup3 到达状态未出现，因此 NUnit coverage assertion 失败，不记该旧专项 PASS。产品 Core `82DF7F7C` 未改，fixture 为 `D36B60D7`；已停止重跑，旧故障仍未确认解决。
>
<!-- P15_MAIN_CURRENT_20261106_END -->


> **未付款预留查询预算专项（2026-11-05 Toronto）：已复现并修正一条重复重证路径；Main 出现不同失败，已停止，P15 仍 IN_PROGRESS。** 原失败同一 g51 在约 0.5182 秒内经历 11 次空间拒绝并耗尽 16k。源码的 Held 空间比较包含全部身体位置，而旧 Native 合并只跟踪拒绝角色中的 actor；未捕获的真实 Bound 身体连续移动可重复唤醒同一未付款请求。确定性模型先得到相同 `intent-query-budget-exhausted/Terminal` RED（32 帧／64 次测试预算；31A8A9C1），收窄空间保护后 RED 仍准确复现（9431880F）。这证明重复重证机制，不声称捕获了旧 g51 的每条失败 job。
>
> 产品修正仅在 `ServiceTrafficCoordinator.cs`：当前 typed unpaid 原 Held／真实拒绝中，识别同 owner／ActorLife 的实际位移及原有效 Bound Native leg；新增模式只合并这些已认证 actor 的位置变化，其他 target／roles／静态身体／parking／rights 变化仍立即完整重证。真实 checkpoint 或其他空间变化同时失效旧 cause，防止重新接管 fresh partial proof。原预算、年龄、付款／容量、碰撞及 Terminal 不变。独立 v2 review 无 Critical／Important；临时固定来客测试开关已移除。当前 Core／PQS **911／911 PASS**（FFCE81D5，含 14 项新增复现／保护用例）。
>
> 随后仅跑一次原 Main、关闭新增诊断，结果 **1 FAIL**（2F355181，123.295 秒）。约 sceneSeconds=27.376，Barista 准备送杯进入 Blocked：`Delivery candidate stopped: PlanningFailed: no-independent-finite-release-or-cycle`。窗口末接待 6 位、真实离店 1 位，未排空。此报错不同于本次查询耗尽；是否与当前改动有关 **UNKNOWN**，不能称预算问题已通过 Main 验收。已按“新失败停下来汇报”停止，无额外重跑／修复或扩大回归；3192 Assets 零差异／未知 0，Editor／worker 0，无 Git 写入／Scene 保存／P16。
>
> 证据：[专项结果](../outputs/phase15/p15-closeout-20261104/unpaid-budget-20261105/results.json)、[新 Main 失败](../outputs/phase15/p15-closeout-20261104/unpaid-budget-20261105/new-main-failure.json)、[911 模型 XML](../outputs/phase15/service-traffic-coordination-20261030/p15-unpaid-budget-current-core-pqs-green-1105-first/edit/tests.xml)、[原 Main XML](../outputs/phase15/service-traffic-coordination-20261030/p15-unpaid-budget-original-main-no-diagnostics-green-1105-first/play/tests.xml)、[独立 review](../outputs/phase15/p15-closeout-20261104/failure-counter-audit/unpaid-uncaptured-native-motion-v2-review-20261105.md)。以下为历史时点。


> **同源精简回归复跑（2026-11-05 Toronto）：Main 未排空及查询预算耗尽再次出现，整体仍未通过。** Owner 明确要求再跑一次相同精简范围，本轮未修改源码。Edit **1327/1327 PASS**（419CEF84，19.372 秒，含新增 3 项到达偏差模型）；Play 计划 89 个 fullname 各运行一次，**88 PASS／1 FAIL**（F18701F3，272.692 秒），其中 P15 namespace **76/76 PASS**。测试本体合计约 4 分 52 秒，启动／恢复另计。
>
> 唯一失败仍为 `MainDefaultCounterPlacementCompletesServiceWithoutTrafficDeadlock` 的全部实际离店断言：8 位接待、7 位真实离店，最后第 8 位仍 Queued，窗口末 Cashier Idle、Barista Returning；无 Blocked，未出现原到达许可错误。原记录确认第 8 位 `pickup-wait/g51` 为 `intent-query-budget-exhausted/Terminal`，与前次同类预算耗尽表现再次出现，具体耗尽首因 UNKNOWN，不靠复跑掩盖或标记已修复。已结束本次一次性复跑，无自动追加测试、重试或修复，P15 仍 IN_PROGRESS。
>
> 全 534 个 C# 文件与本轮开始一致；3192 Assets，Edit 变化 2／恢复 2、Play 零差异、未知 0，Editor／worker 0。原失败与全部排除范围保留，无 Git 写入／Scene 保存／P16。证据：[复跑结果](../outputs/phase15/p15-closeout-20261104/reduced-regression-20261105/rerun-after-arrival-01/results.json)、[Main 原失败](../outputs/phase15/p15-closeout-20261104/reduced-regression-20261105/rerun-after-arrival-01/failures.json)、[队列预算原记录](../outputs/phase15/p15-closeout-20261104/reduced-regression-20261105/rerun-after-arrival-01/last-queue-observations.txt)、[Edit XML](../outputs/phase15/service-traffic-coordination-20261030/p15-reduced-rerun-after-arrival-edit-1105-first/edit/tests.xml)、[Play XML](../outputs/phase15/service-traffic-coordination-20261030/p15-reduced-rerun-after-arrival-play-1105-first/play/tests.xml)。以下为各自历史时点。


> **Main 到达偏差专项（2026-11-05 Toronto）：模型已通过，原 Main 单项仍 FAIL，尚未验收完成。** Owner 授权仅复现及修复咖啡师到达后丢失 committed leg 的问题。原 request24 的虚拟中间目标与 NavMesh 实际投影端点相差约 6.18cm，Native 报 Arrived 时实际 pose 距原目标 9.219cm，超过原 8cm 到位检查。最小产品修改仅在 `ServiceTrafficCoordinator.RouteJob.Finish` 封存新 proof 时，将非 final 虚拟绕行点对齐该段已验证 Native endpoint 的 ground-y；最终声明 goal、raw path、LegEnds 不变，新增完整拼接及首尾 connector clearance 校验。未改变通用 Arrival API、碰撞尺寸、阈值或预算。独立 review 未发现 Critical／Important。
>
> 三项模型正确 RED（64E61C22）保留；新模型下一腿的原直线前提穿障碍被原 guard 正确拒绝后，仅修正新模型为同长径向外投影，明确核对下一段 clearance、投影幂等、同 stamp 实际到位及下一 strict leg 重证与原后缀／预算。最终 Core／PQS **897/897 PASS**（FF526039）。原 Main 自然经营实景 **1 FAIL**（69239101，123.274 秒）：本次无 Blocked／未出现原到达错误，8 位接待、6 位真实完成离店，但余下 2 位 Queued、员工空闲，未完整排空。记录显示第 7 位 `pickup-wait/g45` 为 `intent-query-budget-exhausted/Terminal`；其查询耗尽首因 UNKNOWN，尚未隔离复现或修复。不能把本次 Main 记为 PASS，也不能仅凭原错误未再出现宣称 Main 已完全修好。
>
> 已按新失败停止约定结束，无追加实景、重跑或预算放大；P15 仍 IN_PROGRESS。本次产品 Core 与对应模型各 1 文件修改，3192 Assets 零差异／未知 0，Editor／worker 0，无 Git 写入、Scene 保存或 P16。证据：[专项结果](../outputs/phase15/p15-closeout-20261104/main-arrival-20261105/results.json)、[897 模型 XML](../outputs/phase15/service-traffic-coordination-20261030/p15-main-native-waypoint-core-safe-fixture-1105-first/edit/tests.xml)、[原 Main XML](../outputs/phase15/service-traffic-coordination-20261030/p15-main-native-waypoint-original-main-1105-first/play/tests.xml)、[新队列拒绝原记录](../outputs/phase15/p15-closeout-20261104/main-arrival-20261105/queue-terminal-observation.txt)。原 trace `task-1/20261106-001243-957` 保留。以下为各自历史时点。


> **启动时序修复及精简复测（2026-11-05 Toronto）：启动测试已修复；精简回归出现新的 Main 经营失败后停止。** 本次仅修改 `CafeLoopPlaySessionTests.cs`：在 EnterPlayMode 前暂停测试场景的 AutoSpawn，退出后恢复原值；所有原员工／全局角色数量、真实服务／取餐／离店、容量释放和跨 session 断言逐行保留。原失败 RED 为 ACA013AC。专项 **2/2 PASS**（真实 Domain Reload 开／关，各 2 次 Play，共 4 次真实服务／离店），随后同范围精简 Edit **1324/1324 PASS**，18.093 秒。
>
> 精简 Play 的计划 89 个 fullname 各实际运行一次：**88 PASS／1 FAIL**，280.187 秒；其中 P15 namespace **76/76 PASS**。唯一新失败 `MainDefaultCounterPlacementCompletesServiceWithoutTrafficDeadlock`：原 Main 正常经营约 sceneSeconds=33.759 时，Barista 进入 Blocked，原因为 `Actual Native arrival lost its committed traffic leg`，现场 2 位已付款顾客仍 PickupWaiting。具体许可失效原因 UNKNOWN，尚未隔离复现或修复；未把本次 runtime 失败归为启动测试问题。已按新失败停止约定结束，没有重跑或继续修改产品。
>
> 全 534 个 C# 文件与修复后冻结源一致，产品源码本轮未改；3192 Assets，Edit 中变化 2／恢复 2、Play 零差异、未知 0，Editor／worker 0。P15 仍 IN_PROGRESS；本次精简范围不覆盖中央柜台 60 人长项、Mixed60、GUI、全历史 96 case 结案、device／CI／merge。证据：[修复与精简结果](../outputs/phase15/p15-closeout-20261104/reduced-regression-20261105/repaired-results.json)、[新失败原记录](../outputs/phase15/p15-closeout-20261104/reduced-regression-20261105/repaired-play-failures.json)、[Edit XML](../outputs/phase15/service-traffic-coordination-20261030/p15-reduced-current-edit-startup-fixed-1105-first/edit/tests.xml)、[Play XML](../outputs/phase15/service-traffic-coordination-20261030/p15-reduced-current-play-startup-fixed-1105-first/play/tests.xml)。原 Main trace 保留于 `task-1/20261105-231412-832`。以下为各自历史时点。


> **最新精简回归（2026-11-05 Toronto）：出现新失败后已停止，不能标记通过。** Owner 要求缩短全量耗时，本轮复用现有 Phase14／Phase15 模型，加 Navigation、CounterQueue、SpawnClock、EmployeeTaskRunner；Native Edit **1324 case：1323 PASS／1 FAIL**，测试本体 14.805 秒。唯一失败 `CafeLoopPlaySessionTests.RealP14RepeatedPlayWithDomainReload`：原测试要求启动时全局仅 2 个 NavigationActor，本轮首次 HoldArrivals 前已有 2 名员工及 1 名自然生成的 Entering 顾客，实际 3 个；staff 仍为 2、orders／paid 为 0。已读取原 startup 记录，首因仍 UNKNOWN，疑似测试启动时序前提与自动来客冲突，尚未认定产品故障，也未修正或重跑。
>
> 按失败停止约定，计划的 **89 项 PlayMode 均 NOT_RUN**，没有把之前通过的 7 项冒充本轮结果。精简 Play 原计划包含全部 P15 机制／UI、7 项经营修复、3 项旧通路保护、Main120 秒及长柜台 20 位实际离店 1x／2x；中央柜台 60 人长项、Mixed60、GUI 与全历史 96 项整体结案不在本轮。全 534 个 C# 文件与运行前一致；3192 Assets，测试中改变 2／恢复 2／未知 0，Editor／worker 0。无产品或测试源码修改、Git 写入、手工 Scene 保存或 P16，P15 仍 IN_PROGRESS。证据：[精简计划与结果](../outputs/phase15/p15-closeout-20261104/reduced-regression-20261105/results.json)、[唯一失败原记录](../outputs/phase15/p15-closeout-20261104/reduced-regression-20261105/failures.json)、[Edit XML](../outputs/phase15/service-traffic-coordination-20261030/p15-reduced-current-edit-1105-first/edit/tests.xml)。以下为各自历史时点，保留原通过与失败边界。


> **最新专项修复结果（2026-11-05 Toronto）：本次三处经营衔接修复已验证，P15 仍 IN_PROGRESS。** 未付款预留在真实 PQS preview 暂缺时，旧 Frozen-only hold 漏掉 queued Deferred，导致陈旧 snapshot 被判 Terminal；现暂停同一原准备，preview 恢复后以最新完整证明继续，累计预算与身份不变。新 Reserve 现在继续保护已取餐但仍持有真实 PQS 站位的前客离店能力，直到实际离位／释放。让路中真实 Disable／Cancel 后，仅显式 Resume 可单次接续；在原 strict generation 分配点同步同一原 plan，保留严格身份检查。
>
> 验证：相关 Core／PQS 模型 **894/894 PASS**，包含 preview 失配恢复、跨帧证明及已取餐仍占位的正反例；不是完整 EditMode。随后最终产品源正常、无额外诊断的 Unity Native **7/7 PASS**：1 秒与 3 秒两种节奏各完成 20 位真实离店、原 disconnected 拥挤布局完整排空、普通让路、Disable 后显式接续、Changed Done 退休，以及 Stationary 显式恢复。新接续必须实际 Bound，plan／Visit／Native stamp 的 generation 严格一致，最终取餐、离店与容量排空断言均保留。Native 47.833 秒，3192 Assets 零差异／未知 0；Core `02B28D49`、CFC `47BCBFA7`、CLC `6DCE9B9D`。证据：[专项汇总](../outputs/phase15/p15-closeout-20261104/focused-repair-results-20261105.json)、[模型 XML](../outputs/phase15/service-traffic-coordination-20261030/p15-preview-collected-and-owner-models-1105-first/edit/tests.xml)、[最终 7 项 Native XML](../outputs/phase15/service-traffic-coordination-20261030/p15-known-business-and-owner-seven-native-1105-first/play/tests.xml)。模型证据早于最后 CFC strict-generation 小修，不冒充最终源完整 EditMode。
>
> **未完成项保持开放：** 历史完整 Play 的 96 个失败 case 尚未整体重新验收；当前源完整 Edit／Play、Mixed60、Main／Validation GUI 与 TM01–07 均未重新执行。两项 Working-overlap 测试的真实同时工作前提仍 NOT_PROVEN，不据此宣称产品通过或继续盲试坐标／时长。Owner 观感、8h／device、CI／commit／push／merge／merged-main 仍分开记录；无 Git 写入、Scene 保存或 P16。以下“本轮／最新”等文字均为各自历史时点，不能覆盖本段的当前边界。


> **本轮完整回归未通过，已按 Owner 要求停止（2026-11-04 Toronto）。** 完整 EditMode **3685 PASS／0 FAIL／0 SKIP**；完整 PlayMode **1538 PASS／96 FAIL／15 SKIP**（1649 case，0 Inconclusive）。两项旧测试 `SingleSideQueuePreservesActualDeliveryAndReturnCorridor`、`CounterTailCannotCommitAcrossEarlierPendingExitSuffix`，以及 Returning 原请求失败退休负例，均在本轮最终产品／测试源完整 Play 中 PASS。Main 原摆法短项与中央 2x 长项 PASS，但**中央 1x 长项因 Counter 前移 `PlanningFailed: no-independent-finite-release-or-cycle` FAIL**；还有连续20单、取餐／离店、装修及旧断言相关失败。Phase15 namespace 的76项 Play 均 PASS，仅代表该 namespace，不能替代跨阶段整体验收。**96 是失败 case 数，尚未分类根因，不能称96个独立产品 bug，也不能直接全归为旧测试。** 按 Owner 最新“有新失败就停下”要求，未自动修复／重跑／启动 Mixed60 或 GUI；R19最终成本结论未继续评估。已收齐原 XML、CLI8／Native2，以及资源恢复凭据：3192 Assets、469 Scripts／Tests、3 inputs、28 Scene文件均零差异，未知／缺失0；恢复资源1、历史输出14，Editor／worker／进程0，OS handles UNKNOWN。**P15仍 IN_PROGRESS，整体验收未通过。** 证据：[停止汇总](../outputs/phase15/p15-closeout-20261104/stopped-results.json)、[96项原始失败记录](../outputs/phase15/p15-closeout-20261104/full-play-failures.json)、[Play XML](../outputs/phase15/service-traffic-coordination-20261030/p15-closeout-final-source-1104-play-first/play/tests.xml)。以下进展及“最新”字样均为各自历史时点。无 Git提交／Scene保存／P16。

> **本轮收尾进展（2026-11-04 Toronto）：两项旧测试的 focused 修复已通过，最终集中验收进行中。** 单侧取餐使用原五个候选，优先不挡员工往返的既有候位，仅作用于真实单侧；Counter pending 前提改为核对同一已获权 `TrafficMove.Source`，保留原安全与业务断言。两旧项＋照片摆法＋未 Ready 转队的中间源 Native **4/4 PASS**；收窄后的最终单侧排序已再次通过 SingleSide。返程负例在当前产品源 **1/1 PASS**：真实原 request15 Returning 出现 no-progress→Failed／RecoveryFailed，原有限 WAIT 权利退休，原订单、杯、身体与容量断言保留；其中 A 走 Terminal 分支，不能记成实际离店或健康排空。原 RED／Inconclusive 历史保留。完整 Edit 正在执行；完整 Play 将补 Counter 在最终源的覆盖，Mixed60、R19 与当前 GUI 仍待执行，不提前写 PASS。默认关闭的 Editor 被动记录器待本轮 Editor 正常退出后应用已审修正，再由完整 Play 编译与实际 GUI 验证。证据：[focused 收据](../outputs/phase15/p15-closeout-20261104/focused-results.json)、[执行账本](../outputs/phase15/p15-closeout-20261104/progress.md)。P15 仍 IN_PROGRESS，无 Git／Scene 保存或 P16。以下各“最新／当前”描述均保留其原采集时点，不能覆盖本段。

> **最新修复与限定验证（2026-11-04，Owner已批准并完成）：中间第2块 Pick-up 布局的卡住问题通过复现回归。** 新增窄 admission readiness：尚未出生/未获权的新客，真实候选首段会挡住合法已取餐客人的固定出口时，沿原身份及有限累计预算暂缓；出口真实腾空后正常重证并继续。普通候位、员工工作、碰撞/付款/容量/P15营业时间保持原合同。修正 Prepared/共同提交检查、回调重入与临时 Nav 缺失的生命周期边界。最终冻结源码 **Core654/654 PASS**（原636测试字节后缀完整保留＋新增18项）、**实景短回归18/18 PASS**（截图布局1＋未Ready先转队1＋P15接待门禁16）。截图布局使用合法固定1游戏秒来客输入，120游戏秒时已有12位取餐；随后真实排空到 **15/15订单完成、顾客0、员工Idle、容量归零**，观察到5位新客被暂缓后全部自然恢复。未手设Arrived/付款/完成，未用恢复工具掩盖停滞。独立SOURCE review C0/I0/M0。证据：[Core654](../outputs/phase15/service-traffic-coordination-20261030/admission-exit-readiness-final-verified-first/edit/test-summary.json)、[实景18](../outputs/phase15/service-traffic-coordination-20261030/admission-exit-readiness-final-verified-first/play/test-summary.json)、[实际排空记录](../outputs/phase15/photo-pickup-wait-20261103/readiness-final-native-output.txt)、[源码与结果](../outputs/phase15/photo-pickup-wait-20261103/readiness-final-results.json)。两批输入/源码/Scene/资产恢复零差异，Editor/worker0；OS handles UNKNOWN。**此前两项旧case失败仍保留：SingleSideQueuePreservesActualDeliveryAndReturnCorridor 的路线失败，以及 CounterTailCannotCommitAcrossEarlierPendingExitSuffix 的测试前提未捕获；精确修前对照也同样失败，本轮未扩展修复或冒充PASS。P15当前仍IN_PROGRESS，完整suite、长GUI/Owner/device/CI/merge等原验收缺口不变。** 无Scene保存、Git提交或进入P16。

> **上轮截图复现历史（2026-11-03；当时尚未修完）：Owner确认 Pick-up 在中间第2块。** 已修正未分配收银尾位误挡候位，以及原阶段交接已捕获 waiter 的资源资格迁移。当前 Core **636/636 PASS**；真实截图布局已有3位取餐、前2位实际离店，但第3位仍因后来获权的入口路线占用出口而 Blocked，实景为 **FAIL**。直接短回归 **17/19 PASS、2 FAIL**；两项在精确修前对照中同样失败，保留原结果。拟新增“仅暂缓会挡已取餐客人出口的未出生来客”的窄 producer gate，**待 Owner 批准，未实施**。详见[提案与范围](../outputs/phase15/photo-pickup-wait-20261103/admission-exit-hold-proposal.md)、[Core636](../outputs/phase15/service-traffic-coordination-20261030/photo-captured-bound-handoff-full-core-green-first/edit/test-summary.json)、[原决定时序实景FAIL](../outputs/phase15/service-traffic-coordination-20261030/photo-middle-first-freeze-timeline-first/play/test-summary.json)、[短回归19项](../outputs/phase15/service-traffic-coordination-20261030/photo-middle-two-fixes-direct-regression-first/play/test-summary.json)、[修前两项对照](../outputs/phase15/service-traffic-coordination-20261030/photo-middle-two-regressions-pre-task-control-first/play/test-summary.json)。修前对照结束后两份当前源码已恢复并核对；无 Scene 保存、Git写入或 P15正式closure。

> **最新参数修改（2026-11-03，Owner已批准并完成）：自动来客间隔改为0–10游戏秒。** 默认均匀随机，平均5游戏秒；Pause不放行零秒来客，成功接待后才抽下一轮，每次更新最多接纳一位，原容量／路径／营业门禁继续生效。产品改动仅 `CustomerSpawnClock.Draw` 的抽样公式；5个直接相关测试文件同步换算固定1／2／3秒输入，保留原测试负载及断言。新边界先在旧公式上得到预期RED，再验证 **Edit15/15 PASS、直接Play18/18 PASS、原短实景1/1 PASS**（含零秒真实接待、容量、帧率／2x及P15截止门禁）。初批实景filter的namespace未匹配，明确NOT_RUN；随后仅补正确FQN单项并通过，未重复其余18项。证据：[计时15项](../outputs/phase15/service-traffic-coordination-20261030/spawn-zero-ten-short-green-first/edit/test-summary.json)、[短回归18项](../outputs/phase15/service-traffic-coordination-20261030/spawn-zero-ten-short-green-first/play/test-summary.json)、[原实景1项](../outputs/phase15/service-traffic-coordination-20261030/spawn-zero-ten-native-scene-short-first/play/test-summary.json)。全部已结束批次输入／资产恢复零差异、Editor/worker0；OS handles UNKNOWN。未重跑其他固定间隔长fixture或完整suite。P15原专项NOT_PROVEN与最终验收缺口继续保留，不宣告正式Completed。

> **最新定向验证（2026-11-03）：未 Ready 先去 Pick-up 候位的现有规则已验证，无产品代码变更。** 仅新增两项测试：业务层保留前客已取杯但未实际 Exit 的事实，后客 Preparing 时仍请求转队；真实双顾客场景中，前客 Ready、未取杯且真实身体留在店内，后客在 Preparing 时已有真实 Native 移动，随后真实到达 Pick-up 候位，最后两单正常完成并排空。相关业务 class **31/31 PASS**，新增实景 **1/1 PASS**；现有断言和原测试行未删除或修改，未调整 Scene、游戏时间、容量、付款或碰撞规则。证据：[业务31项](../outputs/phase15/service-traffic-coordination-20261030/pickup-before-ready-business-short-first/edit/test-summary.json)、[双顾客实景](../outputs/phase15/service-traffic-coordination-20261030/pickup-before-ready-business-short-first/play/tests.xml)。本次证明现有 Validation 布局的上述行为；没有重现或确定 Owner 实际摆法的等待原因。此前“当前 Returning 受阻＋顾客同时等待”专项仍 NOT_PROVEN，当前源完整 PlayMode、长时间 GUI 与正式 P15 验收仍未完成，不宣告全部通过。本轮不追加长回归。

> **上一验证快照（2026-11-02；Source93；按 Owner 要求缩短验证）：P15 仍 IN_PROGRESS，本轮不再追加测试。** 当前修复完成后，完整 ServiceTrafficCoordinator class **634/634 PASS**（不是完整 EditMode）；修复涵盖已完成顾客移动后的单次有界唤醒，以及完整路径含一个静止点的合法保护要求。随后仅执行一次原 Native 保护性补测：**1 Inconclusive / NOT_PROVEN**，原因是未观察到 B 的同一任务当前 Returning selected-home Bound/live 状态；实际日志中 A、B 均出现 DeliveryArrived 与 Returned。该前提缺口未关闭，不能计为 PASS，也不等于已证明新的产品故障。本轮到此结束，不自动扩展 fixture、重跑或增加长时验收。两批输入与资产恢复零差异，Editor/worker 0；OS handles UNKNOWN。证据：[当前 Core634](../outputs/phase15/service-traffic-coordination-20261030/task6-complete-single-corner-runtime-full-core-green-first/edit/test-summary.json)、[单项 Native 未证实](../outputs/phase15/service-traffic-coordination-20261030/task6-single-corner-checkpoint-native-fast-final-first/play/tests.xml)。此前 Source86 完整 EditMode **3658/3658 PASS** 已结束，但早于本轮 Core 修改，不冒充当前源全量结果；此前 Main120秒、Mixed60真实离店及原五项 PASS 也保留各自 source 范围。当前源完整 PlayMode、TM01–07 / Main15分钟＋Validation10分钟 GUI，以及 Owner/8h/device/CI/merge 验收仍未完成；不宣告 P15 Completed，不进入 P16。

> **当前验证进展（2026-11-02；15:12Z）：P15仍IN_PROGRESS，Main经营阻断已在当前源验证通过。** Source61 CoreEE93／CFCF1EF／CoreTests5BF／MainTests943416；完整coordinator class **618／618 PASS**，含producer hold先3FEATURE RED后GREEN及原608保护。真实Main＋rapid1707／1708＋Working／有限等待 **5／5 Native PASS**；Main原120秒窗口内5自然来客全部真实Exit、原token全部释放、drain=True、无Blocked，顾客／reservations／paidFIFO／leases均为空且员工Idle。送杯primary＋selected-home绕路及Queue前移期间旧未获权准备的producer hold修复有效；Frozen／Key／累计查询／旧等待预算／Tick不变。证据：[Core618](../outputs/phase15/service-traffic-coordination-20261030/task6-producer-preview-hold-full-core-green-first/edit/test-summary.json)、[真实Main＋四项](../outputs/phase15/service-traffic-coordination-20261030/task6-producer-preview-hold-main-and-original-four-first/play/test-summary.json)、[Main真实最终状态](../outputs/phase15/service-traffic-coordination-20261030/task-1/20261102-150245-690/critical-state-trace.txt)。该Main使用Full trace＋诊断，不代替最终性能门槛。原12seed＋照片4共16布局回归正在同冻结源运行；Long4／mixed60Exit／最终Main轻记录性能／完整Edit与Play／GUI及独立whole Engineering／QA仍待完成。各已结束批次3190Assets／469source／3inputs／28scenes零差异、history0、Editor／worker0；OS handles UNKNOWN。无Git／CI／merge／正式P15 closure或P16。

> **历史阶段记录（2026-11-02；00:26Z 收据）：P15仍IN_PROGRESS。** Core **532／532 PASS**，生产CFC接线已编译；原照片6／7格×1x／2x四组仍 **4 FAIL**。真实scene-history记录的离店数分别为6格两档4次、7格两档2次，不能用Completed5／3冒充离店。新单项首次拒绝记录为第3客初始actual-exit仍未acquire，却遇到后客paid route的new-frozen-owner；旧Frozen仅第6客counter admission。原真实handoff绑定、paid phase出生帧及临时planning Jobs状态仍在只读核对，不由终态或客人序号推根因。当前Core786171BD／CFCFD055443／TestsBBFC5F65。证据：[Core532](../outputs/phase15/service-traffic-coordination-20261030/task6-canonical-primary-window-all-core-after-applied-first/edit/test-summary.json)、[原Photo4](../outputs/phase15/service-traffic-coordination-20261030/task6-collected-exit-window-photo-four-first/play/test-summary.json)、[首次拒绝观察](../outputs/phase15/service-traffic-coordination-20261030/task6-collected-exit-window-later-stall-first-witness/play/test-summary.json)。先前CFC long／int编译错误及应用脚本退出批次均明确NOT_RUN并保留，类型修正已由当前编译与模型通过验证。原矩阵经营、R17、性能完整窗口、最终suite、Main／Validation及最终review仍开放；无正式closure／CI／Git／P16。

> **历史阶段记录（2026-11-01；23:52Z 收据）：P15仍IN_PROGRESS。** 完整 Core **528／528 PASS**，包含真实 collection／delivery lease 的无关版本变化、Nav暂缺、原到站容差、替换／ABA及精确几何保护；三个局部runtime source review均C0／I0。原照片6格2x经营仍FAIL：本次没有previous-leg-not-completed，首客离店改被no-independent-finite-release-or-cycle拒绝，Completed=1／3位live，正在用现有观察点读取首次拒绝的准确依赖，不由终态推断原因。Core612C5446／CFCE5BE008A／TestsB9FF043E；1x canonical successor 的CFC实际consumer正在实现，不能以Core通过宣称经营修好。证据：[Core528](../outputs/phase15/service-traffic-coordination-20261030/task6-canonical-and-strict-runtime-r2-all-core-first/edit/test-summary.json)、[原Fast经营](../outputs/phase15/service-traffic-coordination-20261030/task6-strict-auth-service-photo-six-fast-first/play/test-summary.json)。原模型反例527中4项均在真实前提后失败，后经最小修正转绿；保留原raw及历史UNKNOWN。原Photo矩阵、R17十二项、性能完整窗口、最终完整suite、Main／Validation及最终Engineering／QA仍开放；无正式closure／CI／Git／P16。

> **历史阶段记录（2026-11-01；23:25Z 收据）：P15 仍 IN_PROGRESS。** Core 最新 512 项为 510 PASS／2 FAIL；两项失败均为尚未实现的 canonical successor preparation 反例，不是完整通过。2x 未获权 parking 原预算重证模型已转绿，另同真实 Counter body／claim 及 pose／ActorLife 漂移 3／3 PASS。但原照片6格2x仍 FAIL：第一次 strict leg 尚未发出，sameProof／index／generation／start 均相同，snapshot 20→21 后被 previous-leg-not-completed 拒绝；不可凭停住坐标认定已经走过离店腿。1x 新路线抢先问题的 Core 草案正独立审查，CFC consumer 尚未应用。证据：[Core512](../outputs/phase15/service-traffic-coordination-20261030/task6-unacquired-parking-temporary-owner-all-core-first/edit/test-summary.json)、[真实 body 模型3](../outputs/phase15/service-traffic-coordination-20261030/task6-unacquired-parking-existing-body-models-first/edit/test-summary.json)、[原 Fast](../outputs/phase15/service-traffic-coordination-20261030/task6-unacquired-parking-photo-six-fast-corrected-selector-first/play/test-summary.json)、[首次拒绝诊断](../outputs/phase15/service-traffic-coordination-20261030/task6-unacquired-parking-fast-actual-prepare-rejection-first/play/test-summary.json)。错误 partial class 选择器的零用例批次明确 NOT_RUN。R17 12 seed、性能完整窗口、最终 suite、Main／Validation 实测及最终 Engineering／QA 仍开放；不正式 closure／CI／Git／P16。

> **历史阶段记录（2026-11-01；21:33Z 收据）：P15仍IN_PROGRESS。** Core有限等候修正经独立审查后，完整478／478通过；原照片6格2x重测已消除previous-leg-not-completed，但仍因wait-budget-exhausted停在首位离店，正在查真实腾空／阶段交接的唤醒链。照片1x原始拒绝已取证为new-frozen-owner，不能称cycle或owner-not-independent；metadata polling意外重排队仍在TESTFIRST验证。12seed已加入但尚未运行；性能2项已取得真实计数缺失RED，尚未接线。证据：[Core478](../outputs/phase15/service-traffic-coordination-20261030/task6-fast-unissued-leg-all-core-green-first/edit/test-summary.json)、[照片及性能3项](../outputs/phase15/service-traffic-coordination-20261030/task6-fast-photo-twenty-and-r19-two-first/play/test-summary.json)。此前恢复5／5、中央两档各60和四长柜台各20真实离店的PASS保留各自source范围。当前Core44F2／CFC95DA；最新资源／源码／Scene零差异，Editor／worker0，OS handles UNKNOWN。最终完整suite、性能、Main／Validation实测及最终review仍OPEN；P15不正式收口，不进入P16／CI／Git／merge。
> **历史阶段记录（2026-11-01；20:45Z 收据）：P15仍IN_PROGRESS。** 共同层模型884／884（Core464）此前已通过；接入CFC5080后，原1x及固定1秒／3秒来客2x三组均各20真实Exit并排空，M3两项也PASS。该Native10为5 PASS／5 FAIL，五个失败先被同一测试observer NRE阻断；只读分类修正后，正常付款取餐到位已PASS，其余四项明确读到了本测试故意堵路后已退休的PlanningFailed记录，正在改为捕获原Fail时的真实证据，恢复断言尚未完成，不能把它们记PASS。证据：[经营与保护10](../outputs/phase15/service-traffic-coordination-20261030/task6-paid-initial-refresh-real-business-and-guards-first/play/test-summary.json)、[observer分类后5](../outputs/phase15/service-traffic-coordination-20261030/task6-paid-observer-classified-five-guards-first/play/test-summary.json)。当前产品Core2FEB／CFC5080不变，Source27为0FC6FCEE（仅test observer改变）；原八布局Native正在运行，尚无结果。扩展布局／性能、完整suite、Main／Validation真实窗口和最终review仍开放；不进入P16／CI／Git／正式closure。

> **历史阶段记录（2026-11-01；20:21Z 收据）：P15仍IN_PROGRESS。** 共同层15类模型884／884 PASS，其中完整Core464／464，原820项缺失0／重复0；CFC取餐接线尚未应用，不能据此称2x修复。证据：[共同层884](../outputs/phase15/service-traffic-coordination-20261030/task6-owned-claim-common-model-and-related-green-first/edit/test-summary.json)。离店恢复5项Native此前通过。收银前移目标变化现沿同一anchor／key及原预算重证，原1x连续20单已真实全部离店并排空；2x原失败为5入店／1离店：实际Accepted任务的ActiveIntent匹配已Terminal的StaleContext，而consumer仍持续暂缓，订单ReadyForPickup及原预留均保留。随后同产品的首次失效observer重跑为20真实Exit PASS／NOT_OBSERVED（未发生该失效），不能据此宣称已修复。固定1秒／3秒真实clock两项均FAIL，且首次失效均在原任务age1／q0时捕获：后排顾客真实到达使Counter全局Version12→13，前排自己的body／Entry／目标未变。现有queued刷新仍按严格OwnerVersion拒绝，单补接线不够；此前真实Queue9项为2功能RED／7保护PASS；共同修复后，这9项及新增2项strict兼容测试均已纳入884全部通过。原任务pre-pump接线草案待独立审查及Native验证，尚未声称2x修复。证据：[固定节奏两项](../outputs/phase15/service-traffic-coordination-20261030/task6-two-x-real-spawn-clock-pair-first/play/test-summary.json)、[真实Queue模型9](../outputs/phase15/service-traffic-coordination-20261030/task6-paid-own-claim-checkpoint-model-red-first/edit/test-summary.json)。证据：[2x当前任务字段](../outputs/phase15/service-traffic-coordination-20261030/task6-two-x-original-paid-transfer-current-flags-first/play/test-summary.json)。M3已从历史NOT_PROVEN取得真正反例并修复：真实额外Counter.Join曾对未变的服务位多发1次Notify；现两项Native均PASS，跨Queue无关变化不唤醒原等待，实际lease释放仍正常Notify＋wake。同Queue全局Version变化仍保守通知，未声称已优化未知lifetime／ABA事件。证据：[Core451](../outputs/phase15/service-traffic-coordination-20261030/task6-prepared-leg-and-anchor-core-contract-green-first/edit/test-summary.json)、[原20单两档](../outputs/phase15/service-traffic-coordination-20261030/task6-counter-forward-goal-original-twenty-both-first/play/test-summary.json)、[M3真实RED](../outputs/phase15/service-traffic-coordination-20261030/task6-prepared-anchor-current-body-m3-native-first/play/test-summary.json)、[M3修后两项PASS](../outputs/phase15/service-traffic-coordination-20261030/task6-m3-exact-parking-notification-native-green-first/play/test-summary.json)。当前CFC仍A0BBC383、共同Core2FEB3CBC；Source27为68E6AA68，各验证限其冻结source。最新资源／源码／Scene零差异、Editor／worker0，OS handles UNKNOWN。最终完整suite、扩展布局／性能、Main／Validation真实窗口及最终review仍开放；不进入P16／CI／Git／正式closure。

> **历史阶段记录（2026-11-01；17:34Z 收据）：P15仍IN_PROGRESS。** collected Exit显式恢复先做既有安全重绑，Shiba／Westie原同杯、Pause／Blocked／disable／ChangedDone／真实Exit及Shared、P11停用和不改站位保护，共5／5 Native PASS。queued未发出且尚待查询的strict leg跨帧更新修复后，完整Core429／429 PASS，局部独立review C0／I0。原连续20单仍2 FAIL：1x为7入店／6实际Exit后，剩余顾客停在counter slot3、无产品Failure；2x新顾客入店遇到另一处previous-leg-not-completed，原离店顾客已真正移动。两条继续取证，不宣称经营已修好。M3仍1 SKIP／NOT_PROVEN，但原拒绝点已证实：同一已Granted unborn准入在公开TrySpawn内，因新body snapshot重新变为Deferred；未证明无关通知反例，尚未实现过滤。证据：[恢复5](../outputs/phase15/service-traffic-coordination-20261030/task6-collected-exit-safe-rebind-native-guards-first/play/test-summary.json)、[Core429](../outputs/phase15/service-traffic-coordination-20261030/task6-queued-strict-core-contract-green-first/edit/test-summary.json)、[20单及M3原拒绝](../outputs/phase15/service-traffic-coordination-20261030/task6-fresh-queued-leg-original-twenty-and-m3-refusal-first/play/test-summary.json)。各批资源／源码／Scene零差异、Editor／worker0，OS handles UNKNOWN。最终完整suite、扩展回归、Main／Validation真实窗口及最终独立review仍开放；不进入P16／CI／Git／正式closure。

> **历史阶段记录（2026-11-01；17:14Z 收据）：P15仍IN_PROGRESS。** 未发出返程遇到无关counter claim退休的窄修复已完成：先2项真实模型RED／16项保护PASS，修后18／18及同Core完整418／418 PASS。2x原始诊断已捕获另一条原因：仍Queued、尚未提交的离店腿在下一帧snapshot变化后被判StaleContext，继而使依赖等待者失败；该模型与修复仍进行中。Carry恢复的提交条件正在cap1取证。M3同unborn提前预热已真实成立，但未取得旧lease有效时的真实额外Counter.Join，最新仍1 SKIP／NOT_PROVEN，未实现通知过滤。原20单及完整项目当前不能宣称PASS。证据：[18项修复验证](../outputs/phase15/service-traffic-coordination-20261030/task6-strict-external-removal-model-green-first/edit/test-summary.json)、[Core418](../outputs/phase15/service-traffic-coordination-20261030/task6-strict-removal-core-contract-covering-first/edit/test-summary.json)、[2x原始失效](../outputs/phase15/service-traffic-coordination-20261030/task6-two-x-original-owner-invalidation-r3-exact-selector/play/test-summary.json)、[M3真实预热](../outputs/phase15/service-traffic-coordination-20261030/task6-m3-same-source-early-preheat-native-first/play/test-summary.json)。各次资源／源码／Scene零差异、Editor／worker0，OS handles UNKNOWN。最终suite、扩展回归、Main／Validation真实窗口及review未完成，不进入P16／CI／Git／正式closure。

> **历史阶段记录（2026-11-01；16:45Z 收据）：P15 仍 IN_PROGRESS。** 原持续20单两档重测均 FAIL，不能用旧820模型或定向Native通过代替。1x已记录到：全部6个身体事实与员工自己的服务claim不变，另一个顾客的counter claim退休，使canonical snapshot版本变化，未发出的同一返程被拒为 `previous-leg-not-completed`；严格模型新增正反例正在验证。2x等待者的原依赖owner失效原因仍待原始诊断。Carry的真实 `ActorUnavailable` 回调现已进入原定Blocked，但显式恢复后仍因 `commit-version-changed` 失败；恢复问题保持FAIL。新增员工停用反测的原Terminal假设已证实不成立：全局availability hold会冻结等待，改正后的hold与独立NavMeshAgent故障两项Native均PASS。无关counter变化的精确唤醒反例仍SKIP／NOT_PROVEN，实际lease释放唤醒正例PASS；未实现过滤。最新三项为2 FAIL／1 SKIP，资源／源码／Scene零差异，Editor／worker0，OS handles UNKNOWN。证据：[两档20单](../outputs/phase15/service-traffic-coordination-20261030/task6-admission-endpoint-original-twenty-both-first/play/test-summary.json)、[真实snapshot与恢复诊断](../outputs/phase15/service-traffic-coordination-20261030/task6-one-x-snapshot-shiba-recovery-and-m3-premises-first/play/test-summary.json)。完整suite、扩展布局／性能、当前源Main／Validation真实窗口及独立review仍未完成；历史whole-game输入停住按Owner要求DEFERRED，不进入P16／CI／Git／正式closure。

> **历史阶段记录（2026-11-01；15:29Z 收据）：P15 仍 IN_PROGRESS。** 相关模型820／820 PASS；正式HUD的29项模型／场景构建及5项真实组件回归通过，营业准入／跨日集成／Validation三类此前68／68通过。新提交前检查已证明并修复“旧handoff过期仍写服务位”，有效交接与过期拒绝及既有12项Native通过。有限取餐等待已完成正常链路实证：原key／起始帧／seq不变，真实parking WAIT（q0、30秒），原lease退休后真实Preview只提交一次（q2、剩余28.31669秒），原双侧堵一侧的完整场景与三客3真实Exit均通过。本轮Native16为**15 PASS／1 FAIL**；唯一失败为新增员工停用反测，尚未证明其假定的child Terminal，正在仅用只读记录核对全局营业暂停与源状态，不把它冒称通过。此前持续20单为1x 3入店／3Exit、2x 5／5后停接客，实际已接纳顾客全离店；现已证明首次忙端点仍提交的问题，新guard的同anchor／key／frame忙→自然清空→首次出生定向PASS，原20单两档仍待新源重测。另3项Carry失败正在补严格Native时序取证，未弱化Blocked／杯／Total／Done／Exit断言。证据：[模型820](../outputs/phase15/service-traffic-coordination-20261030/task5-exact-handoff-core-and-ui-model-covering-first/edit/test-summary.json)、[新经营链路16](../outputs/phase15/service-traffic-coordination-20261030/task5-collection-release-and-admission-runtime-native-first/play/test-summary.json)、[原持续经营与Carry失败](../outputs/phase15/service-traffic-coordination-20261030/task6-carry-integration-positive-prep-native-first/play/test-summary.json)、[首次提交真实RED](../outputs/phase15/service-traffic-coordination-20261030/task5-admission-first-endpoint-cause-red-first/play/test-summary.json)。15:29:21Z release为process／worker0，3188 Assets及source／input／Scene零差异、OS handles UNKNOWN。精确唤醒、其余生命周期／Exit／兼容、扩展布局／性能、最终suite、Main／Validation真实窗口及独立review仍开放；不进入P16／CI／Git／正式closure。
> **历史阶段记录（2026-11-01；14:40Z 收据）：P15 仍 IN_PROGRESS。** prepared 路线循环修复已有相关14类模型783／783 PASS；原来失败的连续三客场景现已 **3入店／3真实离店 PASS**。P15营业准入、集成、Validation三类 Native **68／68 PASS**，覆盖实际跨日／排空、Pause／2x及装修中断。正式 HUD 的 typed 规划／等待／受阻提示尚未接入：新增模型／渲染12项为10预期功能RED＋2保护PASS，4项组件测试均在真实前提成立后缺少对应文案，正在实现只读显示。有限 lease 等待的新测试已补齐真实 Prepared 返程取证，得到**真正功能RED1项**：真实员工当前完整返程proof、原30秒期限及顾客原handoff均成立，但原始 prerequisite 记录缺失；consumer 正在实现，不延预算或伪造 Preview。历史 SKIP 保留。证据：[三客实际Exit](../outputs/phase15/service-traffic-coordination-20261030/task5-prepared-proof-reuse-three-natural-first/play/test-summary.json)、[营业周期68](../outputs/phase15/service-traffic-coordination-20261030/task6-p15-positive-admission-native-covering-first/play/test-summary.json)、[HUD组件RED](../outputs/phase15/service-traffic-coordination-20261030/task5-main-typed-ui-component-red-first/play/test-summary.json)、[有限等待原始RED](../outputs/phase15/service-traffic-coordination-20261030/task5-collection-release-prepared-capture-r2-first/play/test-summary.json)。各次均正常退出、资源／源码／Scene零差异，OS handles UNKNOWN；完整suite、扩展布局／性能、Main／Validation真实窗口、独立review及CI／merge仍开放，不进入P16。
> **历史阶段记录（2026-11-01；14:25Z 收据）：P15 仍 IN_PROGRESS。** 已真实复现两条互不冲突的准备路线反复撤销对方许可、四轮零提交的问题。最小修复仅在逐条验证完整路线仍安全时复用准备结果，新增真实能力路线冲突反测保持拒绝；相关14类模型 **783／783 PASS**。原三客经营正在 Native 回测，不能用模型通过宣称经营故障已解决。停用恢复及闭店／排空此前定向 Native9＋另4项已全部通过，各自限于冻结源码。有限送杯 lease 的业务等待 consumer 尚未实现：新原始捕获测试为 **SKIP／NOT_PROVEN**，因第一选择时真实返程尚未绑定 Native，未伪装成通过。精确唤醒、生命周期／Exit、兼容／布局／性能、最终完整 suite、Main／Validation 实测及 CI／merge 仍开放，不进入 P16。证据：[模型783](../outputs/phase15/service-traffic-coordination-20261030/task5-prepared-parking-proof-reuse-runtime-covering-first/edit/test-summary.json)、[原始循环RED](../outputs/phase15/service-traffic-coordination-20261030/task5-prepared-parking-reproof-model-red-first/edit/test-summary.json)、[能力路线反测](../outputs/phase15/service-traffic-coordination-20261030/task5-late-capability-controls-original-core-first/edit/test-summary.json)、[等待捕获NOT_PROVEN](../outputs/phase15/service-traffic-coordination-20261030/task5-collection-release-consumer-original-capture-first/play/test-summary.json)。14:25:56Z release 为 process／worker0，3188 Assets及source／input／Scene零差异，OS handles UNKNOWN。

> **历史阶段记录（2026-11-01；13:45Z 收据）：P15 仍 IN_PROGRESS。** 最近完整相关14组模型769／769 PASS；取餐快照更新、真实 owner 替换拒绝、真实已消费 mutation 拒绝、单侧完整经营及原双侧 BlockedPreferred 场景已在 Native 通过。初次到岗的残留许可已按真实完成里程碑退休；更新后的证据在退休前保存，未恢复产品重记录。最新定向 Native9 为 **8 PASS／1 FAIL**：Queued／Prepared 阻止假排空、Pause＋idle／retired 后允许排空、闭店退休／真实 Rest／自动第二天／一次出生、三项 initial／Cashier 生命周期全部通过。唯一失败的停用恢复已观察到 Hold 清除原 Pending handoff；修复已应用，正验证中。连续三客仍 FAIL（3入店／1离店）：原拒绝分支已证实旧 handoff 预算耗尽后，新 service lease 已取得但严格腿 joint commit 被拒绝，正在查明前置准备为何超时；不能绕过失效 handle 或重置预算。另一项 TwoStatic 的有限 lease 等待尚未接入。其 Queue 只读 prerequisite 及旧候选保护137／137 PASS，业务 consumer 仍待实现。证据：[相关769](../outputs/phase15/service-traffic-coordination-20261030/task5-pickup-m2b-and-initial-retirement-covering-first/edit/test-summary.json)、[Native27历史19P8F](../outputs/phase15/service-traffic-coordination-20261030/task5-pickup-m2b-initial-native-covering-first/play/test-summary.json)、[Native9最新8P1F](../outputs/phase15/service-traffic-coordination-20261030/task5-drain-initial-migration-availability-native-first/play/test-summary.json)、[连续三客原拒绝](../outputs/phase15/service-traffic-coordination-20261030/task5-three-natural-original-handoff-false-first/play/test-summary.json)、[Queue137](../outputs/phase15/service-traffic-coordination-20261030/task5-collection-release-prerequisite-runtime-model-first/edit/test-summary.json)。各结果只覆盖其冻结源码，137不是全项目回归；当前新源777尚在运行。13:45:31Z release 为 process／worker0、3188 Assets及source／input／Scene零差异，OS handles UNKNOWN。精确唤醒、未选 child／history 生命周期、其余兼容与 Exit、布局／性能、最终完整 suite、Main／Validation 实测和 CI／merge 仍开放，不进入 P16。

> **历史阶段记录（2026-11-01；11:40Z 收据）：P15 仍 IN_PROGRESS。** work index 已接入，Source17 `6FDFEA88...` 的14组相关 EditMode **757／757 PASS**，包含原始顺序、退休／回调／未完成腿及新增送杯候选保护；真实1000条冷历史后的枚举量由1001降到1。这是模型与枚举量证据，尚非 CPU／GC／长时性能结论。两项静态布局 Native 仍 **2 FAIL**，但只读记录已完整取得（errors0）：一项取餐选中了被占用目标；另一项取餐旧请求因 snapshot11→12 变成 Terminal StaleContext，员工返程遇到真实 blocker 属独立事实。前者正补取餐侧的提交前选择，后者进入原计划 metadata refresh；不重开 Terminal、不换 key 清预算。原始结果：[Edit757](../outputs/phase15/service-traffic-coordination-20261030/task5-d3-c-index-runtime-covering-first/edit/test-summary.json)、[两项静态布局及 END 记录](../outputs/phase15/service-traffic-coordination-20261030/task5-c-index-two-static-stored-end-first/play/test-summary.json)。11:40:56Z release 为 process／worker0、3188 Assets及source／input／Scene零差异，OS handles UNKNOWN。此后新增测试存在一次 NUnit 兼容编译失败（0 cases／NO_XML），单独保留，不计测试结果。员工重建及单侧完整经营的旧源 PASS 保留其范围。实际 drain／Closing admission、metadata／精确唤醒、其余 Exit、兼容测试、布局／性能、最终 suite 与 Main／Validation 手测仍开放；不进入 CI／merge 或正式 closure。

> **历史阶段记录（2026-11-01；11:19Z 收据）：P15 仍 IN_PROGRESS。** 已接受的员工重建修复在相关 EditMode **739／739 PASS**；完整单侧柜台送杯、返程、取餐、真实 Exit 已通过 Native。新增收银员重建用例在修正测试等待条件后整项 PASS，保留实际 Finished 历史、旧请求退休及新请求真实到达断言。两项原有静态站位组合仍 FAIL：第一项已观察到送杯首选目标被身体占用、尚无路径查询；第二项的 Collection 尚未观察到原拒绝原因，不能借返程阻挡推断。当前 Source16 `E78F03DA...` 仅增加默认关闭的实际枚举计数及6项模型，结果 **5 PASS／1 FAIL**：500条 Retired＋500条 Discarded 使实际处理枚举量从应有1变成1001，work index 正在实现前准备。这里只是相关模型及定向 Native 证据，不代表最终完整 suite、drain、性能或 Main 验收通过。原始结果：[Edit739](../outputs/phase15/service-traffic-coordination-20261030/task5-cashier-finished-efc-recreation-m2-covering-first/edit/test-summary.json)、[定向 Native12](../outputs/phase15/service-traffic-coordination-20261030/task5-cashier-finished-efc-recreation-m2-native-first/play/test-summary.json)、[收银修复及两项原失败](../outputs/phase15/service-traffic-coordination-20261030/task5-cashier-wait-and-static-witness-first/play/test-summary.json)、[计数6项](../outputs/phase15/service-traffic-coordination-20261030/task5-d3-phase-c-counter-model-red-first/edit/test-summary.json)。最新 release 为 process／worker0、3188 Assets及源码／输入／Scene零差异，OS handles UNKNOWN。下面各轮记录仅对应其当时源码；metadata／精确唤醒、Exit、实际 drain／正式 UI、扩展布局、性能、最终 suite、Main／Validation 手测与 CI／merge 仍未收口。

> **历史阶段记录（2026-11-01；09:40Z 收据）：P15 仍 IN_PROGRESS。** Source16 `D19B78E17FFAE0DF57356A05B2F30B68256F211A2C4D646FD52013181A245BB8` 的相关 EditMode **727／727 PASS**，覆盖真实退休后的 payload 释放、callback 核验窗口及单点未完成腿的 pending 状态；这里只是 model／相关回归，不代表 Native 经营或最终 drain 接线通过。此前路径版本 Source16 `51911F...` 的真实 Native13 为 **7 PASS／6 FAIL**：原始有限让位选项声明及 BeginYield 来源已通过，四项单侧下游流程仍未完成；另两项布局失败分别保留。随后一次只读诊断明确观察到 child 的第0个落点被身体占用，count24／attempted{0}／queries0 即 Terminal，剩余候选未尝试；这不是 Native 路径查询失败。扩大 Core 失败范围的提案触发两项旧保护回归，已完整撤回并保留失败证据；当前改为准备提交前候选可用性方案，尚未实现。原始结果：[Edit727](../outputs/phase15/service-traffic-coordination-20261030/task5-d3-phase-b-runtime-covering-first/edit/test-summary.json)、[Native13](../outputs/phase15/service-traffic-coordination-20261030/task5-original-planned-yield-runtime-native-first/play/test-summary.json)、[单项诊断](../outputs/phase15/service-traffic-coordination-20261030/task5-original-planned-yield-stopped-witness-first/play/test-summary.json)。09:40Z release 为 process／worker0，3188 Assets及source／input／Scene零差异，OS handles UNKNOWN。历史 capture、SKIP、FAIL 与旧 suite 保留各自源码范围；剩余 metadata／精确唤醒、Exit、work index、drain／UI 接线、扩展布局／性能、最终完整 suite、Main／Validation 手测及 CI／merge 均未收口。

> **当前（2026-11-01 Toronto）：P15 仍 IN_PROGRESS。** 普通取餐的原始许可记录已在 Source15 `2C81A7B116F3F9C17D7DADF49AB0652B8BE2D2E7D4B82BB255CF44C90834C7C4` 验证：相关 Edit710 PASS；实际场景30项为28 PASS／2 SKIP／0 FAIL，新增 Collection 的真实来源、绑定与 Current 均为 true。两项 SKIP 为真实 foreign EFC／自然外部障碍变化前提缺失，不算通过。该增量只接入 capture／identity，尚无 family refresh、sibling cleanup 或 semantic wake。单侧三例此前仍在送杯 bounded proof 阶段失败，未到达三个目标 pickup phase；随后 Source15 `E6E45DD...` 的一次 opt-in 原始诊断确认 N=1／index0／attempted0 的决定为 PlanningFailed＋Terminal，而让位入口要求 AlternateAllowed，故未进入让位。原始 Native 返回未保存，合法让位仍须实际证明；这次诊断的产品流程结果仍 FAIL。收据：[capture verification](../.superpowers/sdd/2026-10-30-service-traffic-coordination/task5-pickup-family-capture-runtime-verification.md)、[一次原始诊断](../outputs/phase15/service-traffic-coordination-20261030/task5-single-side-yield-failure-witness-first/play/test-summary.json)。剩余取餐／让位接线、离店恢复、drain、历史任务清理、布局／性能、完整 suite 及 GUI／CI／merge 均未收口。

> **最新增量（2026-10-31 Toronto）：Queue 服务站位身份验证 PASS，P15 仍 IN_PROGRESS。** Source13 manifest `DE1C9B9D451A032076D64E6C42F584E71CC63184459AE19ADD7116D63F876137`：相关 Edit710／710 PASS（含新增8项），真实取餐／前移／离店 controls 4／4 PASS。实际释放与同位置重新占用具有不同身份，幂等 acquire、无关版本变化、副本及 rollback 的原有规则保留。原始结果：[Edit710](../outputs/phase15/service-traffic-coordination-20261030/task5-delivery-lease-identity-runtime-covering-first/edit/test-summary.json)、[Native4](../outputs/phase15/service-traffic-coordination-20261030/task5-delivery-lease-identity-runtime-native-first/play/test-summary.json)。03:06:24Z release 为 Editor／worker0、3188 Assets及源码／输入／Scene零差异，OS handles UNKNOWN。该结果尚不证明 CFC 精确唤醒、四类 pickup preparation、layout／Exit／drain、全部布局或最终手测完成；四项旧 SingleSide controls 因同步 admission 准备过时尚未运行，不算通过或豁免。

> **当前开发（2026-10-31 Toronto；收据 UTC 2026-11-01）：统一服务交通重构 IN_PROGRESS，P15 未完成。** 按已批准的[实施计划](superpowers/plans/2026-10-30-service-traffic-coordination.md)继续。员工初次回岗、补收银员后的显式恢复及 counter advance 的 source12 定向结果：Edit702 PASS；Native26 为24 PASS／2 SKIP／0 FAIL，三名自然顾客均实际离店。两项 SKIP 缺少真实 foreign EFC／外部障碍变化前提，不算通过。原 Main 失败与旧结果仍保留下方各自源码范围；重构尚未完成 Main 普通目录操作、所有布局、长时窗口、性能或完整 suite。接下来为服务站位身份与精确唤醒、其余 pickup phases、实际 Exit 恢复、layout／drain 接线及最终回归；无 CI／Git／merge／正式 closure。原始结果：[Edit702](../outputs/phase15/service-traffic-coordination-20261030/task5-traffic-resume-order-covering-first/edit/test-summary.json)、[Native26](../outputs/phase15/service-traffic-coordination-20261030/task5-traffic-resume-order-native-first/play/test-summary.json)。Source12 manifest `2E49CD3A278C3E065A55DE02E1740EE94085A7579E83E0DC9B7C592F75D221B8`；后续 lease M1 为新增测试及惰性接口，尚不属于这轮 GREEN。

> **最新Main普通UI补测（2026-10-30本地）：FAIL；P15仍IN_PROGRESS。** 目录正常确认CR／CM／Pick-up后三项Ready，Done自动出现员工与顾客，但首单Barista在Prepared阶段`Blocked / IncompletePath`，Completed=0；两份原生快照保留同一未完成订单。Pause／2x／镜头仍响应，正式28800／7200与Validation关闭已观察。未改源码或Scene、未调用恢复；3176Assets／12source／14Scenes当前差异0。现场保留Play＋游戏Pause，未做退出release。精确拒绝层UNKNOWN；历史整个Game无响应按Owner指示搁置。此前相关suite及Validation窗口PASS保留其范围，不能覆盖此次Main失败，不进入CI／merge收口。见[Main补测报告](../outputs/phase15/reservation-rule-20261029-01/manual-main-complete/report.md)。

> **此前相关suite与Validation窗口（2026-10-30）：PASS；不覆盖上方Main失败。** CFC C8F521、EFC71744D、tests7921FA同源：相关Edit387PASS／0FAIL；Play299PASS／0FAIL／6项明确opt-in SKIP，旧302用例缺失0，重点30项全PASS。八种长柜台各20次真实离店／容量归零，两组中央柜台controlled-clock各60次；独立review C0/I0。当前源码普通Editor真实装修后连续618.975秒运行至Day7／Completed66，观察到真实Rest全容量0、自动重开、Pause／1x／2x／Decor／镜头与横竖屏响应，Console0warning／0error；手测后3176Assets／源码／Scenes差异0。修复仍有有限寻路开销，历史wholeGame首因UNKNOWN、旧失败／旧全项目suite及Owner／CI／Git／设备门槛分别保留，不宣称永久消除故障或Phase closure。见[本轮修复记录](../outputs/phase15/reservation-rule-20261029-01/report.md)与[当前源码GUI报告](../outputs/phase15/reservation-rule-20261029-01/manual-detour-final/report.md)。

> **历史完整受控复测（后续修复前）：0P/1F/519.9587383s。** `pending-exit-arrival-full` 在真实中央柜台装修及速度切换后，于Day5 Closing再次出现主要顾客互锁：50实际离店590→51实际离店593→52取餐594，52移动超时、员工Idle、Ready仍在。全部交付到站采样为true，未重现此前独立员工预检错误。已保留first-blocked／40秒history／rings，恢复后3176 Assets及source/input/Scenes零差异、Editor退出。晚期semantics已达80000容量，不能据缺失日志认定原独立检查的真假。正在用三名真实顾客和原waypoint候选入口证明“较早真实离店暂时被前客身体遮挡”这一边界；已有准备失败／编译失败均单独保留，不算产品RED，尚无这一分支的产品修复或Phase closure。

> **历史现实时间结果（员工到站修复前）：FAIL。** `pending-exit-realtime` 为0P/1F/72.0148469s；在stage0、Day1 Closing、尚未装修时，员工真实最终Arrived后被EmployeeFlow交付复核拒绝，提示“业务交付入口或实际到站不可用”。不能归因为杯位满，也不能据此认定与pending-exit同因。失败后3176 Assets／source／inputs／Scenes零差异、Editor退出。新增短multi-substep测试1P，未重现；一次采样字段读取错误单独保留为harness失败。当前仅在对应测试内补充只读到站采样，产品源码仍为下述hash。

> **历史阶段记录（当时，2026-10-30）：修复IN_PROGRESS。** 已证明后来实际离店抢占较早 clear/unissued pickup route 的缺口：Native RED 2P/1F → 修后3P/0F，补等待中停用／静态堵塞及既有相关回归13P/0F。Review指出 canonical 未转移预留目标遗漏，已先证明真实 reservation 反例0P/1F，再局部修正并6P/0F；独立review C0/I0。当前CFC SHA256 `280C229732325DA60FE569C91BE8E0184D5355250051EC2E16B46AB68E60320F`。同源现实时间装修／Closing／Pause／1x／2x复测进行中；此前普通Editor第8项FAIL仍保留，不能据此宣称原自然停滞已解决。新源完整相关回归及后续Computer Use验收尚未完成。

> **历史修复与manual失败（后续修复前，2026-10-30）：FAIL。** 当前冻结源码相关Edit387PASS/0FAIL，相关Play290项=288PASS/0FAIL/2原有opt-in SKIP，原八组合及中央柜台两档60真实离店均通过。但普通Editor装修后6分余出现自然停滞：Day5Closing／Completed35／Total5，Ready队首未到位、员工皆Idle；Pause／1x仍响应。第6项装修记录PASS，第7项未注入blocker而暂缓，第8项FAIL。已撤回过早的健康／PASS判断，保留现场及原始自动结果。详见[最新手测记录](../outputs/phase15/reservation-rule-20261029-01/manual-ui/report.md)与[本轮修复记录](../outputs/phase15/reservation-rule-20261029-01/report.md)。根因尚待native分支证据；历史整个Game输入失效首因仍UNKNOWN。无Git／CI／正式Phase closure。

> **最新状态（2026-10-28）：Owner追加批准原两份测试准备修正，已应用并定向2 PASS／0 FAIL。** Decoration在真实取餐到位后注入出口故障；Matrix在原600帧内等待同一request实际移动，原业务断言保留。未应用Matrix搬柜台、Base PickupSide及Status提案。经营修复此前原8组合各20真实离店＋controlled2共10 PASS，普通Editor实际输入＋20离店PASS，Fix1单例PASS。现在剩3项旧关联失败未关闭；完整关联最近快照仍220P9F2S，当前源完整关联／后续Edit NOT_RUN。历史整个Game输入失效本次未复现，根因UNKNOWN，P15不收口。最新增量见18.28，18.27保留此前交付决定。

> 本报告第2、6–11节保留各次失败、批准、恢复与诊断的历史时间点；其中 IN_PROGRESS／BLOCKED 仅对应当时状态。HUD修复前全量结果以第12节为准；后续Agent手测见第16节、最新玩家HUD修正见第17节，历史证据不删除、不合并为同一次成功运行。

> **展示分离修正后当前（2026-10-27）：** Main改用P5玩家状态卡，Validation保留黑色详细诊断。Edit59 PASS；Play122 PASS／0 FAIL／1原有SKIP；独立review通过，真实两Scene横竖屏及Decor／Done复测通过，15张原始截图、UI后3172 Assets零差异。详见第17节。未重跑完整suite，不宣告Phase closure。

> **以下为第16节手测时点：** 原批准test cases的M01–M04 Validation补测PASS；结合此前M05出口、M06、M07正常UI，原七项均有Agent通过记录。Guide扩展的Main完整装修及直接付款集合对照仍有缺口，Debugger仍UNKNOWN。当前源定向自动122 PASS／0 FAIL／1原有SKIP；旧全量属HUD修复前source。本轮没有产品／测试／资产修改或新自动suite；正式closure／merge readiness仍No。见第16节。

> **以下为17:17、HUD修复前的完整自动快照，仅保留其source范围：** 2026-10-27；worktree `E:/Unity/Project/AnimalCafe/.worktrees/phase-15-business-day`；Unity `6000.5.5f1`。**Local automated gate PASS；正式 Phase closure 待完成。** 本地自动回归已通过：最新完整 EditMode 2832 PASS／0 FAIL／0 SKIP；独立完整 PlayMode 1445 PASS／0 FAIL／5 项 opt-in SKIP。两次 CLI／native log 均0；后续仅 Editor／test 变化未重跑完整 Play，454 runtime 与 Main／P15 Scene 保持一致。17:17恢复34项 test-owned 资产后，3172 Assets／1126 source 零差异、extra／temp0、Editor0。OS process-handle exit UNKNOWN，未以日志退出码冒充 OS handle 证据。Engineering／QA／Production 技术复核已完成（Critical 0／Important 0）；Owner M01–M07、真实8小时／device、CI／VCS／merge／merged-main 均 NOT_RUN。Phase 未正式 Completed，不进入P16。完整历史、source binding 与原始 XML 见下文。

> **追加定向诊断（不改源码）：未取得所需证据。** `photo-six-original-decisions` 为0P／1F／180.9960593秒：测试框架180秒超时，随后清场触发未解除的离店观察器断言，未保存完整原规划trace。此为诊断harness失败，不能当作新产品反例、确定旧失败根因或将旧FAIL改为PASS；不自动重复。分类见[诊断记录](../outputs/phase15/reservation-rule-20261029-01/photo-six-original-decisions/diagnostic-classification.json)。本次手测完整操作与限制见[manual 6–8报告](../outputs/phase15/reservation-rule-20261029-01/manual-final-ui/report.md)。
## 1. 实现范围与实际接入

> 2026-10-27 展示分离修正已实现并完成限定验证，见第17节。第16节七项Agent记录属于此前展示源；本次只报告实际限定回归与UI检查，不冒充新源完整manual重跑。正式营业／排空／时间规则不变，无Phase closure或CI／VCS动作。

Tasks1–5 实现了独立现实周期 Service、实时 admission policy、唯一 Cafe Loop driver、真实 drain 聚合、按 layout version 去重的 LayoutInterrupted，以及只读 HUD／独立验证 Scene。Closing 停新客但继续已有服务；真正 Blocked 保持原因并走既有显式有界恢复。Completed／付款历史不按日清除，没有新增 Economy、Save 或 Offline 旁路。

Task6通过既有 `AnimalCafe.EditorTools.Phase15.Phase15BusinessDaySceneSetup.InstallInMainCafe()` 和Unity Editor API安装MainCafe，CLI／native exit均0；日志 `outputs/phase15/task-6-install.log` 含 `AC_P15_SCENE_READY Assets/Scenes/MainCafe.unity`。正式安装没有手改YAML、调用P14 Main builder或打开validation seed。集中suite中的既有legacy builder tests仍按批准运行，其附带资产写入另行归档并恢复，不能当作正式布局准备。

MainCafe保留原始未就绪布局：runtime已有starter counter `(2,3)`，没有CR／CM或Pick-up；“空店”不表示所有家具数组均为0。正式周期为Open28800秒／Rest7200秒，validation=false、required policy=true。独立P15 Scene仍Open60秒／Rest15秒且明确标Validation。新增Main真实加载测试核对正式配置、原布局readiness fail closed及零顾客／容量／订单分配；有效装修Done后自动接待由既有P14 `InitialMainCafeAutomaticSetupPauseValidDoneStartsWithoutPlay` 在本轮full Play中回归。

Open是计划阶段，不能代表实际接待已就绪；实际服务HUD仍报告readiness／Pause／路径／容量原因。Closing实际排空后才开始完整Rest。Pause／2x／装修只改变Scene动作；周期使用 `Time.realtimeSinceStartupAsDouble`，纯逻辑和integration可注入同一受控clock。重新Play会重建Day1，当前没有退出后的Save／Offline恢复承诺。

## 2. Automated receipts

所有路径相对worktree。XML中的实际case数与CLI exit分别读取；PowerShell wrapper退出不代替CLI退出。此前每Task RED／GREEN与失败历史见 `.superpowers/sdd/2026-10-26-phase-15-business-day/task-1-report.md` 至 `task-5-report.md`。

| Run | 实际Total／PASS／FAIL／SKIP | Process | 状态与证据 |
| --- | --- | --- | --- |
| Task1–4 focused | 各Task report中的实际XML | 各Task receipt | 已完成focused与直接回归，保留Task reports的源范围与失败历史 |
| Task5最终受影响Edit | 4／4／0／0 | CLI0 | `task-5-layout-final-edit.xml` |
| Task5最终normal Editor | 4／4／0／0 | native0 | `task-5-native-final.xml`；真实横竖Open截图与长Closing geometry，非Owner验收 |
| Task6初次full EditMode | 无最终XML，统计UNKNOWN | CLI6／native3 | `full-edit-aborted-assembly-reload.*`；执行步骤错误触发运行中assembly reload，未完成，不能计为产品RED／PASS |
| Task6首轮完整EditMode | 2803／2796／7／0；inconclusive0 | CLI8／native2 | `outputs/phase15/full-edit-first-complete-failed.xml`及对应log／CLI；7项失败均涉及Main冷加载dirty／序列化geometry稳定性，P15 Edit46／46 PASS不能替代这7项真实集成FAIL。 |
| Task6首轮full PlayMode | 1449／1443／1／5；inconclusive0 | CLI8／native2 | `outputs/phase15/full-play-initial-failed.xml`及对应log／CLI；唯一失败为旧P13 M012 fixture未等待真实rebind。P15实际68／68 PASS，Main正式空布局及P14正常装修接待case均PASS。 |
| Task6 fix focused EditMode | 17／17／0／0；inconclusive0 | CLI0／native0 | `task-6-fix-focused-edit.xml`；原7失败相关用例／参数对照与P15 builder均通过，保留原断言 |
| Task6 fix focused PlayMode | 6／6／0／0；inconclusive0 | CLI0／native0 | `task-6-fix-focused-play.xml`；P13 M012、P14只读状态、Main正式门禁、UI绑定、长Closing、横竖geometry均通过 |
| Task6最终full EditMode | **2803／2803／0／0**；inconclusive0 | **CLI0／native0** | `full-edit.xml`及对应log／CLI；冻结修后源完整通过，原7个失败用例均通过 |
| Task6修正wave后full PlayMode | **1449／1443／1／5**；inconclusive0 | **CLI8／native2** | `full-play-after-layout-fix-failed.xml`及log／CLI；P15 68／68、P13 M012均PASS，但旧P12真实时间corner连续移动用例出现新FAIL，集中验收仍未通过 |
| Task6完整诊断上下文Play（修复前历史） | **1449／1443／1／5**；inconclusive0 | **CLI8／native2** | `full-play-p12-diagnostic-context.xml`及log／CLI；P12原case PASS未复现，但P14 `ContinuedArrivalsFillThenResumeCollectionAndDrain`真实IncompletePath未排空，完整gate BLOCKED |

| Run | 实际Total／PASS／FAIL／SKIP | Process | 状态与证据 |
| --- | --- | --- | --- |
| P14批准修复 focused／direct | Edit70／70、realScene1／1、direct10／10 PASS | CLI0／native0 | 原高客流case158.733041秒PASS；同一final fixture counterfactual1行为FAIL→exact bytes恢复→1PASS；独立scoped review Approved C0／I0 |
| P14修复后完整Edit（最新） | **2809／2789／20／0**；inconclusive0 | **CLI8／native2** | `p14-repair-full-edit.xml`；1983.1210559秒；20项IO1224，非缺XML／assembly reload |
| P14修复后完整Play | NOT_RUN | NOT_RUN | 资产恢复遇持续文件映射写入限制，root hold；未启动／未重试 |

初次full Edit运行期间，Task6 implementer两次修正新Main test的fixture断言（既有starter counter／初始capacity可能null，源mtime为2026-10-27 01:03:57 -04:00），触发重新编译。日志15647行请求script compilation，19664行报告 `Unexpected assembly reload happened while running tests`，19750行出现 `EditModeRunTask.cs:52` 的NullReferenceException，最终RunError3；没有完整XML。该错误由执行步骤造成，不归咎于licensing、不冒充产品RED或完整suite失败用例。root批准冻结Assets源码后的恢复性fresh full Edit；首轮健康full Play已启动，批准按Play→Edit顺序完成。保留原始log／CLI/native receipt，不盲重试或改package／Library。

单独Main测试属于既有实现与正式Scene配置的integration验证，不新增runtime行为或宣称一次新的行为TDD。活动runner期间不再修改Assets（含test／Editor／metadata）；只有docs与输出证据可写。

首轮full Play的唯一失败：`AnimalCafe.Tests.PlayMode.Phase13.EmployeeDelegatedSceneChecks.M012_SecondCompleteStationSetAndStableRebinding`，line264预期选中CR为新随机ID `086cbe785cdb4efdb0e5211783f213c2`，员工仍绑定旧CR `12000000000000000000000000000004`。首次确认第二套工作站后使用 `Exit(); yield return Ready()`，Ready只看Idle，可能立即读到旧snapshot；同case日志下一时刻两员工已Blocked／Layout changed。该文件既有 `ResumeAfterDecor()` 明确等待Update、使用原显式Retry、恢复1x并等待实际返岗，后续移除工作站分支已经使用它。已在Editor退出后按root批准一行复用该helper，不改断言、GUID或P13产品行为，focused及随后完整Play中的M012均PASS。初次FAIL保留，不能把不同run伪合成一次全套PASS。

首轮5个SKIP全部为明确opt-in证据任务：P8R `CashIndicator_NativeOverlayCapture_WhenExplicitlyEnabled_WritesUniqueReviewGallery`、`SpacingAudit_WhenRequested_ExportGeometry`、`ReadinessChecklist_CaptureNativeExamplesWhenRequested`、`NativeUiExamples_WhenRequested_CaptureActualGameView`，以及P11 `CaptureRepresentativeCanvasAndCamera`。未开启对应native capture／geometry导出opt-in，不作为产品PASS或Owner手测。

首轮完整Edit的7个FAIL：`LegacyMainCafeFixtureTests.LegacyFixture_RestoresOnlyMainEntryAndKeepsUnrelatedDirtySceneAndSelection(False)`；P8R `AuthoredCompleteUi_ColdAndRepeatedFrameLoadKeepsSerializedBytesStable`；`Refresh_WithUnsavedTargetPrefab_PreservesStageAndAllTargetFiles`四参数case（False／True×ActionBar／ExitModal）；`RefinedB_StyleOnlyMigrationPreservesLayoutAndRollsBackEveryTarget(True)`。最后一项明确P15 Readonly Status的RectTransform `sizeDelta.y` 从116变为0。根因是P15 builder保存了已启用的ContentSizeFitter／VerticalLayoutGroup，Editor冷加载会重新计算geometry并弄脏Scene。已按root批准修正为serialized禁用、只在Play由View启用；Subscribe／Canvas geometry同样加Play guard，Editor Bind仅写静态preview。View disable只解绑事件，保留runtime layout独立工作。旧dirty／rollback断言未改，focused17／17通过；P13首次路径已一行复用既有ResumeAfterDecor，focused M012通过。最终完整suite仍待scoped review后运行，当前不宣称集中验收已通过。

上述fix wave仅改P15 StatusView、P15 builder、P13既有test一行，并用Editor重生两Scene；Main原starter counter与正式28800／7200、P15独立60／15均保持。当前intended Main SHA256 `5B61625ABC6455BAFB5DC5C1627060BA80DBD9CDF544805C7010E3781C91B7A2`；P15 Scene `6DAF54A63961BFFFF3FB68B9852D7CE9B7C372E52616E672C22F025303A6126B`。初次安装／旧Task5 Scene快照保留，恢复helper使用当前新intended bytes；fix详情、精确命令与hash manifest见Task6 report。原Engineering reviewer的scoped复核确认两项修正ADDRESSED、无新breakage，root已批准冻结修后源最终full Edit→full Play；最终QA evidence与Owner gate仍未完成。

修正wave后的完整Play唯一新失败是 `AnimalCafe.Tests.PlayMode.Phase12Integration.Phase12CustomerQueueSceneTests.NativeDefaultCornerArrivalsContinueWithRealEditorTiming`，原WaitNative line801：clear-route慢速累计0.3333333秒，要求≤0.2秒。该case使用自然Time.deltaTime；初轮同case曾PASS，当前没有足够失败帧证据区分单长帧／fixture观测与持续移动问题，不能直接标flaky、放宽阈值或冒充PASS。首次记录时尚未为此修改P12或重跑。完整FAIL已归档，唯一test-owned材质写入已恢复，当时1116冻结项差异0；P15与M012通过不能覆盖这项全局FAIL。

随后root批准一次原case的test-only只读诊断：只在P12 test加入失败前bounded48帧ring与状态摘要，未改阈值／clear／断言／真实时钟／产品。`task-6-p12-single-diagnostic.xml`实际1／1 PASS、CLI0／native0；隔离Scene仅InitTestScene与P12，active P15 owner/view均0，maxDelta0.333333343、maxSlow0.002052799。没有复现，不能用isolated PASS覆盖完整FAIL或确定首因；当前仍不宣称集中验收通过。新增诊断源是1116项中唯一差异，详细SHA与证据在Task6 report。

原Engineering／QA保持首因UNKNOWN，并共同建议严格一次冻结诊断源的完整上下文以取失败帧。已按root批准完成，不缩scope、不改defaultseed／顺序／真实timing／阈值；无后续repeat或fix wave。完整Edit2803P仍对应相同production和Edit-selected源，附加诊断仅在Play测试。最新结果仍FAIL，不能将旧失败称为已修复／flaky。

最新完整context中P12原case实际PASS（26.943540秒），loaded Scene为Phase11Cleanup＋P12、activeDay／activeView均0、captureDelta0、maxDt0.333333343、maxSlow0.002069898；旧失败首因仍UNKNOWN／未复现。新唯一失败为P14 `CafeStatusAndToolsTests.ContinuedArrivalsFillThenResumeCollectionAndDrain` line174。精确路径是：fill日志`blocked=False`；line169只在ResumeCollection之前已经Blocked时才调用恢复，因此本次未在该分支调用ResumeBlockedFlow。正常ResumeCollection后，Barista在Prepared阶段新出现IncompletePath，line172遇新Blocked退出；仍有13位顾客未排空。不能声称“恢复过一次仍失败”或“耗尽120秒”，也不能擦成hitch、取消订单或移动角色伪造排空。完整failure／traffic／订单／容量输出见`task-6-p12-context-failure.json`。Engineering／QA最终均判BLOCKED／Important integration，首因UNKNOWN；不能因发生于既有P12／P14场景就断言与P15改动无关，需Owner确认独立调查scope。

## 3. E01–E28 到实际NUnit覆盖的映射

类缩写：`Service`=`AnimalCafe.Tests.EditMode.Phase15.BusinessDayServiceTests`，`Drain`=`...EditMode.Phase15.BusinessDayDrainTests`；`Admission`／`Integration`／`Ui`／`ValidationScene`／`MainScene`分别为 `AnimalCafe.Tests.PlayMode.Phase15.BusinessDayAdmissionTests`／`BusinessDayIntegrationTests`／`BusinessDayUiSceneTests`／`BusinessDayValidationSceneTests`／`BusinessDayMainSceneTests`。下表列实际方法，参数case以XML为准；多对多映射不把28项误算成28个tests。

| 设计ID | 实际覆盖／证据 |
| --- | --- |
| E01 | Service.`DefaultsUseEightHoursAndTwoHours`；ValidationScene.`ValidationSceneStartsOneReadonlyCycleAndRendersPortraitLandscape`；MainScene.`MainUsesFormalCycleAndOriginalUnreadyLayoutRejectsWithoutAllocation`；P14 `InitialMainCafeAutomaticSetupPauseValidDoneStartsWithoutPlay` |
| E02 | Service.`CutoffIsExclusiveForAdmission`／`ClosingWaitsForDrainAndStartsRestAtActualObservation`；Admission.`ExpiredDeadlineRejectsDirectSpawnBeforeDriver` |
| E03 | Admission.`ExpiredDeadlineRejectsDirectSpawnBeforeDriver`／`ClosingKeepsExistingRealCustomerServiceRunning`；Integration.`RestExpiryCannotBypassPauseOrUnavailableLayout`；入口真实TrySpawn无allocation断言 |
| E04 | Admission.`ClosingKeepsExistingRealCustomerServiceRunning`（在付款前截止，原客完成唯一付款与订单）；Drain.`AcceptedCashierWorkPreventsDrainBeforeAnyOrderExists` |
| E05 | Admission.`ClosingKeepsExistingRealCustomerServiceRunning`（完整Waiting→制作→交付→领取）；Integration.`LogicalDeliveryStillPreventsDrainWhenVisibleCupIsMissing`；P14 Service／runtime完整流程直接回归。不是逐个状态的独立Closing fixture矩阵。 |
| E06 | Integration.`CompletedCustomerStillExitingPreventsRest`；Drain.`CompletedHistoryWaitsForActualExitAndReturnedCallbackConsumption` |
| E07 | Integration.`ReturningBaristaPreventsRest`；Drain.`CompletedHistoryWaitsForActualExitAndReturnedCallbackConsumption` |
| E08 | Integration.`LogicalDeliveryStillPreventsDrainWhenVisibleCupIsMissing`／`VisibleCupIsAnAdditionalCheckWhenLogicalDeliveryIsEmpty` |
| E09 | Integration.`HistoricalCompletedOrdersAllowRest`／`TwoAutomaticCyclesKeepEmployeesAndHistoricalPaidFacts`；Drain.`CompletedHistoryWaitsForActualExitAndReturnedCallbackConsumption` |
| E10 | Integration.`ReservationOrCallbackPreventsRest`的三CapacityKind×reserved／occupied参数；`LeftoverAssignmentCannotBeHiddenByEmptyVisits` |
| E11 | Integration.`DisabledLiveCustomerStillOwnsBodyAndCapacity`／`MissingBindingOrBlockedEmployeeCannotCountAsAtStation`／`IdleLabelCannotReplaceRealStationFacts` |
| E12 | Integration.`MissingLedgerIsNotEmpty`／`UnstableLifecycleCannotDrain`／`MissingBindingOrBlockedEmployeeCannotCountAsAtStation`；MainScene真实未就绪入口 |
| E13 | Drain.`RecoveryAndCollectedFactsCannotDisappearFromDrain`／`QueuedCallbackPreventsDrainWithoutActiveWork`；Integration.`PendingCallbackWithoutVisibleBusinessPreventsRest` |
| E14 | Service.`RestReopensWithoutButtonAndIncrementsDayOnce`／`LayoutVersionIsRecordedOnceAndLastCloseSurvivesNextOpen`；Admission.`ReinitializeAndReenablePreserveDeadlineAndDay`／`SnapshotListenerCannotReenterAndDriveTwice`；Integration.`RepeatedVersionIsIdempotentAndDoesNotPublishRecursiveSnapshot` |
| E15 | Integration.`TwoAutomaticCyclesKeepEmployeesAndHistoricalPaidFacts`；Service.`RestReopensWithoutButtonAndIncrementsDayOnce` |
| E16 | Admission.`UniqueDriverObservesDeadlineWhileScenePausedAndUsesRealEmptyDrain`；Integration.`RestExpiryCannotBypassPauseOrUnavailableLayout`；Service.`CutoffIsExclusiveForAdmission`；Ui.`StatusRefreshIsReadOnlyAndShowsInterruption` |
| E17 | Integration.`FastPauseAndDecorationDoNotStopRealDeadlineOrAdvancePausedWork`／`PausedEmptyCafeCanRestWithoutUnpausing`／`RestExpiryCannotBypassPauseOrUnavailableLayout`；纯Service不接scaled delta |
| E18 | Integration.`PreviewOrCancelDoesNotRecordInterruption`／`FastPauseAndDecorationDoNotStopRealDeadlineOrAdvancePausedWork`；P14 Decoration direct regressions |
| E19 | Integration.`ConfirmedLayoutChangeRecordsInterruptionBeforeClear`（Open／Closing）／`CleanupFailurePreventsRestAndKeepsInterruption`／`NextDayRetainsLastInterruptedClose`／`RestDecorationDoesNotRewriteClosedResultOrPrecountNextDay` |
| E20 | Integration.`ExplicitRecoveryCompletesOriginalVisitThenStartsFullRestAtActualDrain`证明createOrder拒绝后的同visit显式恢复、真实drain→完整Rest；Service.`ClosingKeepsBlockedReasonUntilTrueDrain`证明周期保留原因。P14 `RealVisualRefusalAndCompleteRejectionPreserveBusinessThenRepairWhileReturnBlocked`补实际return route受阻／repair证据。P15 Closing＋已付款订单真实堵路的联合手测仍归M05 `NOT_RUN`，不把前者写成已付款堵路覆盖。 |
| E21 | Integration.`RestExpiryCannotBypassPauseOrUnavailableLayout`（Pause／layout两个参数）；MainScene未就绪入口 |
| E22 | Service.`SettingsRejectNonPositiveOrNonFinite`／`InvalidObservationIsRejectedByEveryEntryWithoutMutation`／`RollbackIsRejectedByMutatorsAndQueriesWithoutMutation`／`DefaultDrainCannotProveSuccessOrAdvanceObservation` |
| E23 | Service.`ClosingWaitsForDrainAndStartsRestAtActualObservation`／`RestReopensWithoutButtonAndIncrementsDayOnce`；Admission.`ControllerHasNoIndependentDriver`；大时差止于Closing，不给NPC补算遗漏订单 |
| E24 | Admission.`LegacySceneWithoutRequirementStillAdmits`／`LegacyLoopDisableResumeStillAdmits`；完整suite中的P12／P13／P14原Scene流程 |
| E25 | Admission.`MissingProviderAtLoadFailsClosedWhenRequired`／`MissingPolicyFailsClosedBeforeIntegratedModeEstablished`／`DisabledOrDestroyedProviderCannotAdmit`／`StaleOwnerUnregisterCannotRemoveCurrentPolicy`／`ThrowingPolicyRejectsBeforeAllocatingAndExplainsFailure`／`DisabledOwnerReplacementAndStaleUnregisterPreserveCurrentPolicy` |
| E26 | Service.`QueryDoesNotAdvanceTimeOrMutateFrozenSnapshot`；Ui.`StatusRefreshIsReadOnlyAndShowsInterruption`；UI启停unsubscribe tests；`Phase15SceneBuilderTests.StatusFormatShowsRealPhaseReasonAndInterruptedHistory` |
| E27 | **静态scope／diff核对**：仅周期、接待、drain、HUD与Editor/test/doc变化；没有Coins／Revenue／Save／Offline实现。不是虚构的NUnit case，也不声称运行过Offline。 |
| E28 | Service.`DayOverflowPreservesRestAndLastObservation`／`SequenceOverflowPreservesOpenAndLastObservation`／`SequenceOverflowPreservesClosingWhenDrainIsObserved`／`DeadlineOverflowOrUnrepresentableDurationIsRejected` |

## 4. Agent、Owner、runtime与device分开记录

| 证据层 | 结果／限制 |
| --- | --- |
| Agent自动测试 | 上方fresh XML receipts；短配置与fake clock证明边界，不是连续真实8小时 |
| Agent normal Editor观察 | Task5真实Open 540×960／960×540原始截图；长Closing以geometry测试检查，active全UI横屏长原因仍是Minor/polish观察点 |
| Owner M01–M07 | 全部 `NOT_RUN`；[Guide](Phase15_Beginner_Guide.md#4-owner-手测记录全部待执行)列逐项操作与结果字段 |
| 最终Engineering／QA／Production gate | 各次获批 source 修正已有独立 scoped review；最新 local automated gate PASS。最终部门 actual-receipt addenda 由 root 统一收口，Owner／CI／版本管理／正式 Phase 验收未完成；窄 source review 不替代这些 gate。 |
| 实际8小时Open＋Closing＋2小时Rest | `NOT_RUN` |
| Windows Player、Android／iOS device、safe area／suspend-resume | `NOT_RUN` |
| 退出后Offline／持久化恢复 | 尚未实现，执行 `NOT_RUN`；P17／P18A范围 |
| CI／commit／push／PR／merge／merged-main regression | `NOT_RUN`，本Task无版本管理／workflow_dispatch权限 |

覆盖边界：E05走完整Closing服务流程而非逐OrderState独立矩阵；E20使用上表组合证据，联合实际堵路验收留给M05。候选Minor另有provider disabled后HUD最后snapshot可能滞留，最终分级以root部门review为准；实际admission仍fail closed，不据HUD旧Open推断当前可接待。不在活动suite中为polish修改源码。

## 5. 资产与进程边界

安装前Main／P14 Scene／P14顾客prefab／P15 Scene备份与hash：`outputs/phase15/task-6-before/`、`task-6-before-hashes.json`；正式安装后的Main intended bytes：`task-6-installed/MainCafe.unity`。集中EditMode中的旧builder可重写旧Scene；只归档并恢复本次已知test-owned incidental写入，保留runtime source、已有文档与untracked资料以及原metadata；最终核对见Task6 report。不会使用blanket reset／restore、手改YAML或P14 validation seed掩盖Main布局。

使用唯一显式Editor路径 `E:/Unity/Hub/Editor/6000.5.5f1/Editor/Unity.exe`，CLI `C:/Users/zheng/AppData/Local/Unity/bin/unity.exe`，无Editor／package安装、登录、cache清理或并发Editor。既有sandbox licensing WMI限制沿已批准scoped escalation执行；日志中的licensing诊断与实际case结果分列，不能将XML PASS写成整个基础设施无warning。

此前Task6收口：已归档恢复当时完整Edit的35项known owned资产，以及各完整Play写入的旧材质；`task-6-final-source-check.json`确认新诊断freeze **1116项0差异**，相对最初freeze唯一已声明变化为P12 test诊断B6。P14两Scene／两顾客prefab与开始前hash均相等，Main／P15 Scene为本报告新intended hash，metadata／URP baseline／用户资料保留。CIM确认Editor0，未停止任何进程。source／docs `git diff --check`退出0；全局退出2仅为Editor生成MainCafe的空`m_Name`字段尾随空格，保留原始Editor YAML，没有手改来消除警告。无commit／push／merge／CI／worktree删除。

## 6. P14 独立调查提案、后续批准与修复记录

以下为批准前的原调查提案，保留历史；Owner随后已明确批准该P14专项，实际完成范围见本节末。原证据起点为最新P14 case的XML／traffic／订单／容量／prepared handle与IncompletePath，保留P12旧失败UNKNOWN作为独立未复现记录。

| 范围 | 先做什么／何时才考虑改动 |
| --- | --- |
| `Assets/Tests/PlayMode/EditorSceneLoading/Phase14/CafeStatusAndToolsTests.cs` | 只读核对fill→ResumeCollection→新Blocked的真实时序与line169／172；如获批，再为原case取受控证据，保持真实客流、原断言和至多有界恢复合同。不能假称本次已执行过恢复。 |
| `Assets/Scripts/CafeLoop/CafeLoopController.cs`、`CafeLoopService.cs` | 先追踪O8 Prepared／交付请求与最新snapshot、队列lease／回调身份；仅在因果证据指向这里并再次明确改动文件后修正。 |
| `Assets/Scripts/Employees/EmployeeFlowController.cs`、`Assets/Scripts/Customers/CustomerFlowController.cs` | 先读交付与顾客领取／离店同时发生时的身体、reservation、站位与request更新；不能默认归咎fixture或P15。 |
| `Assets/Scripts/Navigation/NavigationWorld.cs`、`NavMeshMovementDriver.cs`、`NavigationLayoutAdapter.cs` | 只读确认IncompletePath来自哪次真实查询／revision／几何与动态身体条件。当前不改Nav；证据若要求改已完成Nav行为，另列文件、风险和Owner批准。 |

风险是破坏旧P12／P14队列／动态身体间距、工作站binding、显式有界恢复、付款／交付幂等与P15 Closing真实drain。禁止缩小身体／容差、瞬移、放宽旧断言、固定假GUID、无限retry、强行清单或把付款订单伪造Completed。若后续获批实现，应沿TDD先复现原失败，再做最小修正；先focused原高客流case与直接顾客／员工／delivery／return／exit recovery回归，并保留原P12 real-timing及不同fps边界；最后按Phase流程完整Edit／Play、必要视觉Owner回归和独立review。上述为批准前提案，不能继续当作当前未授权状态。Owner后续已批准具名P14 repair；Nav/tolerance修改、版本管理动作和P16仍未授权。

P14批准修复实际只改 `CustomerFlowController`、`EmployeeTaskRunner`、`EmployeeFlowController`，新增checkpoint覆盖与现有Scene fixture partial/meta；Nav、容差、原高客流case、Scene与P15配置未改。CFC沿原guard与finite traffic判定允许在未发出下一段前等待，Runner保留同一handle／Prepared／杯／lease，Flow绑定query；真实MoveTo拒绝与已发出failure保持原合同。原失败精确body／target仍UNKNOWN，受控RED/GREEN不能倒推旧几何。独立scoped review Spec PASS／Quality Approved，Critical0／Important0；Minor真实Scene可在fixture cleanup前补直接pending-resume观察，当前不为Minor加测试。

本轮verification source selector1120（相对旧1116：新partial/meta两项，另外两项是扩大selector纳入已有ImportManifest.json与GlyphMipReadback.shader）；allAssets3166与repairGREEN逐项0差异才启动一次完整Edit，无filter／retries0。完整20FAIL均IO1224：P6Migration4／P7Migration14／P7Validator1／P8Migration1，完整case/CDATAs见`p14-repair-full-edit-failures.json`。CIM确认Editor0后，已知35旧资产恢复在M_WallSurface_Layered材质WriteAllBytes仍报user-mapped section open；当前22项恢复／23项归档／14差异待恢复，1120source仅Main尚待恢复。另1个thumbnail为本次P7Validator写入，已报告root，尚未覆盖。root正在只读核实映射持有者；不修改产品／fixture、不换写法、不重试、不启动Play。新full gate BLOCKED，不把持续IO错误自行归咎P14、licensing或flaky。详细原始记录见`.superpowers/sdd/2026-10-26-phase-15-business-day/p14-approved-final-verification-report.md`与outputs/phase15同名receipts。
后续root曾解除hold：剩余13项按原字节逐hash恢复成功，all3166/source1120曾再次0差异，随后只跑原20失败fullnames的focused。该次实际只选中4非参数化case，4 PASS／0 FAIL，CLI0／native0；16参数case缺选，不称20复测完成。原因已从本地Framework确认CLI filter走regex，直接参数括号未转义。root批准改为9个原方法级filter（预计121参数case，包含20原失败＋101对照）一次复核；但恢复这次4case产生的24known旧资产时，host请求上下文再次在M_WallProjection_Valid材质WriteAllBytes报user-mapped section open。已20项恢复，尚余3旧材质＋P7 Scene共4项，方法组未启动。无代码修改／重试，root已获报告；新完整Edit20FAIL不因focused4PASS改写。root的default同字节probe也曾成功，因此不成立sandbox/context首因结论；实际映射持有者与完整FAIL首因继续UNKNOWN。详见新verification report的逐次receipt。
最新恢复状态：root先恢复Valid材质，再批准其余3项只做atomic原字节恢复；已逐HEADraw=GREEN、唯一temp／旧字节archive、File.Replace及双hash读回。`p14-repair-atomic-final-parity.json`确认全部Assets3166／source1120零差异、无extra、Editor0。前文14／4待恢复为历史中断快照，现在已恢复；不代表IO映射持有者或完整20FAIL首因已解决。没有改fixture／产品代码，也未启动9方法121case或完整Play；新验证顺序由root裁定。

## 7. Legacy fixture IO 修正与新完整验证

已获准只修改 `Assets/Tests/EditMode/LegacyMainCafeFixture.cs` 与 `LegacyMainCafeFixtureTests.cs`：用独有同目录temp写完整字节，再File.Replace；目标缺失时保留原recreate语义，临时文件cleanup只处理自身创建项。没有产品／Nav／assertion／容差变更。两个真实MemoryMappedFile只读view测试分别获得可见Dispose与direct Install的Win32 IO1224 RED；GREEN4／4核对旧view仍读原字节、目标／meta精确恢复与caller state全部原断言。独立scoped review Approved，Critical0／Important0；review时121仍活动，之后实际XML121／121及精确name-set已由root核验，无missing／extra／duplicate，原20失败均exact once PASS。4-case selector不足历史不冒称20成功。

新freeze为Assets3166／source1120；相对上轮full只有上述2test source hash变化，P14三项production repair及Main5B／P156DA等其余资产未变。所有24known incidental资产已atomic恢复，临时fixture leftovers0、Editor0后，按root批准启动唯一一次新完整Edit，随后退出／atomic恢复旧known写入／全hash核对后才完整Play。新prefix `p14-repair-final-full-edit`／`p14-repair-final-full-play`，nofilter／retries0、明确6000.5.5f1、timeout3600／7200。目前IN_PROGRESS；实际XML计数未知，不采用预估2811／1450作为结果。历史完整20IOFAIL和P12／P14失败保留；受控mapped mechanism已证明，不等于识别外部映射持有者或旧失败全部根因。实际证据见新verification report及outputs receipts。
### 新完整Edit实际结果（非预估）

`p14-repair-final-full-edit.xml`实际2811 total／2808 PASS／3 FAIL／0 SKIP／inconclusive0，2026.4710955秒，CLI8／native2；P15 Edit46／46 PASS。三项原始case/CDATAs完整保存`p14-repair-final-full-edit-failures.json`：P7 NonCanonicalWindowSeedVariant("wrong-surface")在FormalAssetIntake.CopyRepositorySources:93的File.Copy写WoodShelf.fbx时mapped拒绝；P7 HostileWall("P4_Wall_BackRight","custom-mesh")在NormalizeSerializedWhitespace:419的File.WriteAllText写Main时IO1224；P7Validator drift("footprint")同File.Copy写TallGlass1x1.fbx时mapped拒绝。不是Legacy helper的三个已修正写点，不能把helper4＋121 GREEN外推到所有Editor写入，也不能据此认定Nav失败。外部映射持有者与原full首因仍UNKNOWN；未改P7管线／assertion，无重复Edit／附加focused。

退出后Editor0，actual34资产差异全部属于既有known35，unknown／extra／FBX／meta额外差异0。每项先核HEADraw或Main intended快照=新freeze，再唯一sibling temp→File.Replace→target/archive双hash读回；34个旧测试版本全部归档。`p14-repair-final-full-edit-restoration.json`与`p14-repair-final-full-edit-restored-parity.json`确认all3166／source1120零差异、ownedTemp0、Editor0。正式Main5B／P156DA未变。已批准pair的第一次完整Play尚未启动，等待root基于工作区一致性指令；新full gate不记PASS。
## 8. 本轮完整Play完成与P7交接

| Run | 实际Total／PASS／FAIL／SKIP／inconclusive | CLI／native | 说明 |
| --- | --- | --- | --- |
| `p14-repair-final-full-edit.xml` | 2811／2808／3／0／0 | 8／2 | 2026.4710955秒，三项P7 File.Copy／WriteAllText mapped I/O；不能拿历史2803 PASS替代 |
| `p14-repair-final-full-play.xml` | 1450／1445／0／5／0 | 0／0 | 1537.91996秒，完整noFilter／retries0；P15实际68／68 PASS |

P14原 `ContinuedArrivalsFillThenResumeCollectionAndDrain` PASS163.849062秒；P12 `NativeDefaultCornerArrivalsContinueWithRealEditorTiming` PASS26.561590秒。P15 Edit46＋Play68＝本轮114 subset PASS。P12旧完整FAIL的原首因仍UNKNOWN／未复现，不称fixed或flaky；原P14失败精确瞬态几何未倒推。五SKIP均既有明确opt-in capture／audit：P8R CashIndicator gallery、SpacingAudit geometry、ReadinessChecklist native、NativeUiExamples及P11 CaptureRepresentativeCanvasAndCamera；不作Owner视觉验收或产品PASS。

fullPlay后只有known Invalid Wall材质一项变化，Editor0后通过原字节／冻结hash验证、独有siblingtemp、File.Replace和旧字节archive恢复。`p14-repair-final-full-play-restored-parity.json`于2026-10-27T13:02:52-04:00确认Assets3166／source1120零差异、extra0、ownedTemp0、Editor0，Main5B／P156DA及metadata保持。该时间点后，root已放行P7独立worker；本报告不把随后获批的P7 Editor新source当作本轮Play已执行的源。

Owner已明确批准P7两个Editor写点的安全发布修正与tests，要求内容／GUID／玩法／规则／原assertion保持；实施IN_PROGRESS，当前worker只收口docs／evidence，没有自行修改P7。后续P7 focused真实GREEN＋独立scoped review通过后，root计划另行授权必要的新完整Edit验证Editor-only delta；目前未执行，也不预授权或自动重复完整Play。若Scene／meta／runtime变化或异常，由root重新判断Play范围。完整gate仍BLOCKED；Owner7项、真实8小时／device／CI／VCS均NOT_RUN，不标PhaseCompleted。
## 9. P7修正后的唯一完整Edit：新四项FAIL与恢复交接

`p7-approved-final-full-edit.xml`实际2819 total／2815 PASS／4 FAIL／0 SKIP／inconclusive0，2029.8092198秒，CLI8／native2。P15 Edit46／46 PASS，P7新8tests全PASS；原三项P7失败fullnames各exactly1PASS（`p7-approved-final-full-original-three.json`）。8＋42 focused和P7 scoped review Approved C0I0M0继续有效，但不能替代本轮full4FAIL。

| 新失败case | 实际message／visible stack |
| --- | --- |
| `AssetBuilderSaveIsolationTests.Build_SavesGeneratedMaterialButPreservesUnrelatedUnsavedEdit("phase7")` | texture `T_SM_Window_TallGlass_1x1_01_BaseColor.png` IO1224；test line80 `File.WriteAllBytes` |
| 同method `("formal")` | `Loaded assets are dirty; save them before P8R authoring.`；RequireCleanLoadedAssets548→test22。具体与其他失败的因果尚未建立 |
| `P8RCompleteUiTests.FailedRefresh_RollsBackEverySavedPrefabAndSceneByte` | Catalogue.prefab IO1224；test607 `File.WriteAllBytes` |
| `Phase6DecorationAssetBuilderTests.BuildAll_UiPostPublishValidationFaultRollsBackEveryByteMetaAndIdentity` | test326 expected InvalidOperationException/message含post-publish，actual IOException；完整XML未给该IOException的propagated path，不猜mapping／cascade／rollback首因 |

完整原始CDATA／stack见`p7-approved-final-full-edit-failures.json`。无重试、额外selector、fullPlay或源码fix。root随后单独委派P6 test-only delegate日志＋barethrow、保留原assertion，单原case一次取异常路径，IN_PROGRESS；这是后续诊断源，不冒充本轮full已执行，也不改写本轮4FAIL。

退出后actual38资产差异／extra0：原known35内34项，另4个P8R ActionBar／Catalogue／ExitModal／PutAwayModal prefab。root只读核对失败fixture580–607的original snapshots／finally，确认4项test-owned并明确批准精确原字节恢复。旧operator hash79866AA...完全保留；ignored副本`RestoreP7FinalKnownAssetsAtomically.ps1`只将白名单扩为known35＋这4个明确relative paths，hash6189BD154AC067C3DCDCB9D66FF802FB88CDEE40DDE83E1010DE379282432BC6。38actualPaths全部在Editor0、noextras、underroot、原HEADraw或Main5B bytes＝本轮新freeze的前提下，用unique siblingtemp→File.Replace＋旧字节archive、target/archive双hash恢复；未改source/meta。

`p7-approved-final-full-edit-restored-parity.json`于2026-10-27T14:06:01-04:00确认Assets3168／source1122零差异、extra0、ownedTemp0、Editor0。14:06:28的`p7-approved-final-runtime-main-check.json`确认454runtime对已完成Play零差异，Main5B／P156DA不变。root独立接受并接回Assetsowner，之后由P6诊断worker执行；本worker仅docs／evidence，不再查询／改其活动Assets或启动Editor。旧Play1450=1445P0F5S保持独立gameplay receipt；原三P7源码写点修复通过，不把新增4FAIL认定为它们的回归，也不推断完全无关。当前full gate BLOCKED，P7 RED CLI numeric／外部mapping holder／旧P12原因均UNKNOWN，Owner7／8h／device／CI／VCS NOT_RUN，无PhaseCompleted。

后续小更新：P6单原case诊断实际1 PASS／0 FAIL（CLI0、native log明确0；观察迟导致OS process-handle exit UNKNOWN，未重跑）。记录的是预期 `post-publish InvalidOperationException`；旧full IOException未复现，其路径／原首因仍UNKNOWN，不称fixed。诊断源窄review SpecPASS／Approved／0block，postrun3168／1122零差异、Editor0。Owner已批准两现有fixture＋TestAssetBytes helper／tests的test-cleanup，独立worker IN_PROGRESS，production与原assertion不动；latest full Edit2815P4F仍BLOCKED，已有Play receipt未重跑。Owner7／CI／device／真实8小时／VCS仍NOT_RUN。

## 10. Test-cleanup后的唯一完整Edit：2827 PASS／1 FAIL

`remaining-cleanup-final-full-edit.xml`实际2828 total／2827 PASS／1 FAIL／0 SKIP／inconclusive0，2059.1222039秒，CLI8／native log2，OS handle UNKNOWN。P15 Edit46、cleanup新9均PASS；旧四项中phase7／formal／P8R FailedRefresh各exactly1PASS，P6 post-publish typed assertion仍FAIL。原始failure与完整诊断输出分别为`remaining-cleanup-final-full-edit-failures.json`、`remaining-cleanup-final-p6-output.txt`。

本次传播异常路径已明确：`Phase6DecorationAssetBuilder.UiPublishBackup.Restore:2422`的`File.Copy`覆盖`Assets/UI/Phase6/Fonts/NotoSansSC-Phase6 SDF.asset`时user-mapped section open，经PublishUiCandidateSet1416→BuildDecorationUiAssets728→BuildAll133→原delegate331传播，原typed assertion expected post-publish InvalidOperationException／actual IOException。catch里的rollback failure遮蔽了原try异常，因此本次原try cause仍UNKNOWN；旧首次generic IOException是否同一cause也UNKNOWN。不能继续说本次propagated path未知，也不能据此虚称旧问题已fixed。

退出后Editor0，actual34旧资产变化全在known39内，unknown／extra0；Font本身没有最终delta。沿原scoped operator6189BD...、本轮NEW3172／1126 freeze，逐rawHEAD或Main5B等于freeze后unique siblingtemp／File.Replace／oldarchive双hash恢复。`remaining-cleanup-final-full-edit-restored-parity.json`在15:07:57确认3172／1126零差异、extra／temp0、Editor0；15:08的runtime-meta-main-check确认454runtime＋1682既有meta对已完成Play零差异、Main5B／P156DA及P6diagFA56保持。root接回Assetsowner，之后本worker仅docs，未追加Unity/sourcefix/retry。

root已向Owner提出只改P6单production文件＋tests的安全Restore scope，审批PENDING；P6生产尚未修改。当前完整gate FAIL／BLOCKED，独立Play1445P0F5S不覆盖Editor失败；Owner7／device8h／CI／VCS仍NOT_RUN，无PhaseCompleted。Test-cleanup本身source review Approved C0I0／Minor1仅未来guard、9GREEN＋24direct证据保留；P7 RED CLI UNKNOWN、P12旧原因UNKNOWN与所有旧FAIL不删除。

## 11. P6修复后的唯一完整Edit：原P6通过、thumbnail恢复仍FAIL

`p6-rollback-final-full-edit.xml`实际2832 total／2831 PASS／1 FAIL／0 SKIP／inconclusive0，2033.6270151秒，CLI8／native log2，OS handle UNKNOWN。原sole P6 post-publish typed fault PASS7.639331秒；P15 Edit46、P7新8、TestAssetBytes9、P6新4全部PASS。历史masked original try cause仍UNKNOWN，不能从当前P6 PASS倒推旧try异常。唯一剩余失败为 `Phase7ValidatorTests.ValidateAll_RejectsEachFormalAssetDriftWithoutBuilderRepair("thumbnail-backdrop")`，test268 File.WriteAllBytes恢复`Assets/UI/Phase7/Thumbnails/TH_wall_decor_monitor_01.png`报IO1224；完整CDATA在`p6-rollback-final-full-edit-failures.json`。

Editor0后actual35资产差异=34known39＋该精确thumbnail，extra0。先STOP报告，root确认本轮finally target及历史142B... before bytes，明确授权ignored新副本白名单known39＋该PNG=40；旧6189／7986 operator未改。新`RestoreP6FinalKnownAssetsAtomically.ps1` hashF044441CDB071DE5E454AA95C309BB47E293D8E1F9A48382DC31686FEAA11185，仅增这一PNG，显式35actualPaths；逐rawHEAD或Main5B＝本轮NEWfreeze、unique siblingtemp／File.Replace／oldarchive双hash恢复。`p6-rollback-final-full-edit-restored-parity.json`16:18:39确认3172／1126零差异、extra／temp0、Editor0；16:18:41后验454runtime＋1682既存meta对已完成Play零差异，Main5B／P156DA、P6prod07C2／test914B保持。root接管后，本worker仅docs，不再运行或触碰活动Assets。

root将按既有P7安全写入／已批准test-cleanup范围，必要小步把对应测试恢复一行改接TestAssetBytes.Restore；不扩production、不改原assert，尚无本worker新验证或完整mode。当前gate FAIL／BLOCKED；独立Play1445P0F5S不冒充新Play。Owner7／8h／device／CI／VCS仍NOT_RUN，无PhaseCompleted。所有旧FAIL／原try UNKNOWN／P12 UNKNOWN与RED CLI UNKNOWN保留。

## 12. 当时完整自动验证通过与验收边界（2026-10-27 17:17）

P7 thumbnail测试的必要单行恢复补充已完成并通过独立scoped review：Spec PASS／Quality Approved、C0／I0／新增Minor0；原mutation、assertion、import与cleanup顺序不变。focused19／19 PASS且原thumbnail各exactly1PASS。其后唯一获批、冻结源的完整Edit实际结果：

| Run／覆盖 | 实际Total／PASS／FAIL／SKIP／inconclusive | 退出／证据 |
| --- | --- | --- |
| 最新完整Edit | **2832／2832／0／0／0**，2144.8047596秒 | `p7-thumbnail-final-full-edit.xml`；CLI0／native log0；OS handle UNKNOWN |
| 原P6 typed post-publish case | 1／1／0／0／0，8.600886秒 | 原assertion通过，未被FontCopy IOException遮蔽；不倒推历史原try cause |
| 原thumbnail-backdrop case | 1／1／0／0／0，28.147820秒 | 原fullname在本XML恰好一次 |
| P15／P7新测试／TestAssetBytes／P6新测试 | 46／8／9／4，各自全PASS | 全部包含在2832内，不能重复相加 |
| 独立已完成完整Play | **1450／1445／0／5／0**，1537.91996秒 | `p14-repair-final-full-play.xml`；CLI0／native log0，后续未重跑Play |

完整Edit命令为显式 `unity.exe test . --mode EditMode --editor-path E:/Unity/Hub/Editor/6000.5.5f1/Editor/Unity.exe --output outputs/phase15/p7-thumbnail-final-full-edit.xml --retries 0 --timeout 3600 --format json -- -logFile outputs/phase15/p7-thumbnail-final-full-edit.log`，无filter。实际CLI、native-log、summary和name-audit分别保留；XML2832个test ID均唯一。P10三组数组参数渲染同一fullname但ID不同，不能把fullname字符串去重当作case总数。

新完整Edit使用fresh3172 Assets／1126 source freeze。17:16:56确认Editor0、34项差异均属known40且无unknown／extra；按原HEADraw或Main intended快照逐hash验证、唯一temp／archive、File.Replace、target与archive双hash，精确恢复34项。`p7-thumbnail-final-full-edit-restored-parity.json`于17:17:12确认3172／1126零差异、extras0／ownedTemps0／Editor0。17:17:22的protected receipt确认runtime454、Play既有metadata1682与fresh metadata1685均未变，Main5B／P156DA以及P6／P7 reviewed source hashes一致。未改meta、未手改YAML、无blanket restore。

Play中的P1568、原P14高客流与P12目标均PASS。此后差异只在已批准的Editor／test范围：7个既有CS及6个新增helper／test／meta路径，确切路径和hash见fresh preflight及详细report；454 runtime与两Scene保持一致，所以既有完整Play作为独立gameplay receipt保留，不冒充本轮新Play。五SKIP均是原有opt-in capture／audit，不替代Owner视觉或device验收。

当前local automated gate为PASS。Engineering／QA／Production actual-receipt复核已完成（Critical0／Important0），保留既有Minor；Owner M01–M07、真实8小时／device、CI／VCS／merge／merged-main均NOT_RUN，正式Phase closure仍No、不进入P16。旧masked original try／首轮genericIOException关联、P12旧FAIL首因与外部mapping holder仍UNKNOWN；全部历史FAIL／abort／RED CLI UNKNOWN保留。完整详细交接在`.superpowers/sdd/2026-10-26-phase-15-business-day/p7-thumbnail-final-verification-report.md`，raw receipts均在`outputs/phase15/`。

## 13. 最终技术交接与 Owner 下一步

Engineering 与 QA 已独立核对实际完整 XML、原失败用例、source／资产恢复、当前 Guide 和 Roadmap，结论均为本地自动 gate **PASS，Critical 0／Important 0／新增 Minor 0**。Production 采纳两部门技术结论：P15 实现可交给 Owner 手测；正式 Phase closure 与 merge readiness 仍为 **No**。没有新增代码修正或补跑测试。

本地审查和执行记录保留于工作区忽略目录，未提交：

- [Engineering receipt addendum](../.superpowers/sdd/2026-10-26-phase-15-business-day/final-engineering-receipt-addendum.md)
- [QA receipt addendum](../.superpowers/sdd/2026-10-26-phase-15-business-day/final-qa-receipt-addendum.md)
- [完整执行取舍与理由](../.superpowers/sdd/2026-10-26-phase-15-business-day/rulings-made.md)

四项既有非阻挡观察继续保留：provider disabled 后顶部 HUD 可能滞留旧 snapshot；M07 的长 Closing 横屏完整 UI；P14 native traffic 测试对 pending resume 的直接观察限制；test Temp cleanup 的显式 guard hardening。没有把它们升级为未经证实的当前缺陷，也不自动新开 polish wave。

Owner 下一步按 [Guide 第4节](Phase15_Beginner_Guide.md#4-owner-手测记录全部待执行)完成 M01–M07，并记录 Console／截图或 log／PASS 或 FAIL。branch `codex/phase-15-business-day`、worktree 与全部未提交更改继续保留；commit／push／CI／merge 及 merged-main 验证由 Owner 按项目流程另行处理。Main checkout 原有受保护更改未动。未进入 P16。

## 14. 用户授权 Agent 初轮代测（HUD 修复前历史）

第13节为代测前交接。2026-10-27用户委托代测后，Agent通过真实Windows Editor UI完成可执行部分；完整逐项表、未编辑截图、事件与缺口见[Agent Playtest Report](Phase15_Agent_Playtest_Report.md)。M05实际出口堵塞→显式恢复→真实离店→完整Rest15，以及M06取消预览／Closing实际装修中断一次／旧callback隔离／下一Open保留结束原因通过。自动跨日、游戏内Pause／2x、Main正式配置与原始readiness亦有实测证据；M01完整Main摆设路径、M02／M03完整身份集合、M04完整逐格时间矩阵尚未覆盖，不声称七项全PASS。

新确认问题`P15-MANUAL-UI-01`：原始Main未就绪时，readiness banner与周期HUD在540×960及960×540均重叠；M07 FAIL。Validation就绪场景的长Closing／Rest布局通过不覆盖这一失败。另在误打开Frame Debugger期间有Unity dt assertion及两条scaledDeltaTime异常；已保存一条CafeLoopController.Step→Update栈，实际delta及另一栈未采到，根因UNKNOWN，不能写整轮零error。后续Main独立Play Console显示0／0／0。

本轮没有改源／Scene／prefab／meta；退出Play后3172 Assets逐hash差异0，未保存运行中装修，Editor停在Main非Play且无dirty标记。未启动新完整自动回归。此前Edit2832 PASS／Play1445 PASS＋5 SKIP保持独立历史；manual FAIL不被其抵消。当前先确定最小HUD修复与异常调查范围、补完实测缺口，再做必要Owner观感确认及获授权的CI／VCS；不进入P16、不宣告正式Completed。

## 15. HUD 修复后的当时状态（本轮补测前）

用户批准最小三文件修复后，BusinessDayStatusView按当前可见readiness实际Rect避让；两P15 Scene测试新增双尺寸实际几何、不裁切及Decor／Done可见性检查，未改Scene／prefab／meta或营业规则。Main实际RED2 FAIL；中间GREEN33 FAIL及GREEN2的1 FAIL均保留。最终[manual-hud-green3.xml](../outputs/phase15/manual-hud-green3.xml)123项：122 PASS／0 FAIL／1原有opt-in SKIP（P15 70 PASS）；CLI与native log0，OS handle UNKNOWN。独立[task review](../.superpowers/sdd/2026-10-26-phase-15-business-day/manual-hud-task-review.md)Spec与quality PASS，C0／I0／M0。当前源码hash见[修复报告](../.superpowers/sdd/2026-10-26-phase-15-business-day/manual-hud-repair-report.md)。

Root真实Editor按两Scene、540×960／960×540复测：Main未就绪不遮挡，Decor→Done恢复；Validation实际Open、长Closing、Rest、上日LayoutInterrupted均可读，原按钮可用。正常路径Console warning／error0；原Debugger故障仍UNKNOWN，退出日志的mono／debugger-agent／hubIPC诊断不推断为已解决或资源全部关闭。Sky确认Editor窗口关闭。原始截图、events及逐项限制见[Agent报告末节](Phase15_Agent_Playtest_Report.md#hud-修复及正常-ui-复测2026-10-27)。本轮UI后3172 Assets hash增删改0。

**本节当时Agent手测3／7 PASS：M05出口分支、M06、M07正常UI；M01–M04 PARTIAL。** 第12节完整Edit2832／Play1445＋5 SKIP属于HUD修复前source；本次只重跑上述受影响selectors，不能把旧全量套到新UI源。尚需补manual缺口、明确处置Debugger UNKNOWN，Owner观感／8h／device／CI／VCS／merged-main均未完成；正式Phase closure与merge readiness为No，不进入P16。

## 16. M01–M04 Validation 补测与范围澄清

原批准[Test Cases第4节](superpowers/specs/2026-10-26-phase-15-business-day-test-cases.md)四项均在Validation Scene，本轮真实Editor60／15秒操作已补完：自动开店、Closing原客真实服务／离店与员工返回、两次自动跨日、Open／Closing／Rest的Pause／1x／2x五秒同步画面。原M01–M04为PASS，结合保留的M05出口分支／M06／M07正常UI通过，原批准七项均有Agent通过记录；不是一次全套重跑或Owner本人接受。

此前Guide额外要求Main完整正常装修开店及完整付款集合逐项对照，先前混入原四项PARTIAL门槛，现按[Guide4.1](Phase15_Beginner_Guide.md#41-原批准范围与补充检查)单列，尚未完成的部分不补PASS。三次真实Snapshot订单8／15／15，后两次15条Completed key／owner与两员工ActorId完全相同；PaidVisitIds未输出，直接集合证据仍NOT_CAPTURED，不用源码推断代替。截图误标／滞后保存及未计入的pair逐条更正在events并保留原note；三组timed Rest和confirmed Open1x是同步新证据。

实际逐项、秒值和边界见[Agent报告末节](Phase15_Agent_Playtest_Report.md)，raw[events-final](../outputs/phase15/m01-m04-manual/events-final.json)、[独立Editor log](../outputs/phase15/m01-m04-editor.log)、[身份对照](../outputs/phase15/m01-m04-manual/identity-comparison.json)。本轮Console5条正常信息、warning0／error0，日志定向异常检查0；Stop后未保存Scene，Sky确认Editor窗口消失。[Assets parity](../outputs/phase15/m01-m04-assets-parity.json)3172／3172、增删改0，产品／测试源码未变，没有新全量自动suite，旧source边界保持。

Debugger异常／拖动和目录滚动UNKNOWN、Main额外开店路径、直接完整付款集合、Owner观感、真实8h＋排空＋2h、Player／device／挂起、CI／VCS／merged-main仍独立保留。正式Phase closure与merge readiness为No，不进入P16。

独立QA对本轮证据复核支持原批准M01–M04为4 PASS（Windows Editor Validation），明确保留扩展Main／直接Paid集合和Debugger UNKNOWN边界；[review.md](../outputs/phase15/m01-m04-manual/review.md)。未进行新的Phase整体review或自动suite。


## 17. Main 玩家界面与 Validation 诊断分离（2026-10-27）

Owner 指出黑色诊断 HUD 不应进入 Main，随后批准本次展示修正。Main 使用既有 P5 Surface／Text、圆角 sprite、Body 字体和 spacing；显示 Day、阶段、倒计时与必要玩家提示。P14 内部状态只在 Validation 显示。Closing 原因转为玩家语言，装修中断历史保留；原始诊断 snapshot 和 PrintSnapshot formatter 不变。正式28800／7200、原始布局／readiness、唯一 driver、Pause／2x 和排空规则没有改动。

只修改三份 product source、两份 Scene 和三份测试；Scene 通过 Editor API 生成。完整差异与逐文件hash见[实现报告](../.superpowers/sdd/2026-10-26-phase-15-business-day/player-hud-report.md)。原始证据目录为 `outputs/phase15/player-hud/`。

| Run | Total／PASS／FAIL／SKIP | 证据 |
| --- | --- | --- |
| sandbox RED 启动 | NOT_RUN，无XML | red-edit-1，license BIOS访问失败，不能算产品RED |
| 实际 RED | 17／3／14／0 | red-edit-2.xml，样式、内部文本与Closing映射实际assertion FAIL |
| 首轮 GREEN | 59／56／3／0 | green-edit-1.xml，既有静态TMP字体source reference为空，改用同一asset保存GUID读取原Font |
| 最终 EditMode | **59／59／0／0** | green-edit-2.xml，全P15 Edit |
| 最终 PlayMode | **123／122／0／1** | green-play-1.xml，全P15与批准的P8R／P8／P14直接回归；原有opt-in screenshot SKIP保留 |

Root 与独立 reviewer 均直接解析最终XML。CLI返回0来自保留的原tool completion，native log报告test exit0，补充见 `process-exit-receipts.json`；空的 `*-cli.txt` 不能单独作为依据。OS native exit／handles保持UNKNOWN。未重跑完整suite，旧2832／1445＋5SKIP仍只绑定旧source。

独立[task review](../.superpowers/sdd/2026-10-26-phase-15-business-day/player-hud-task-review.md) Spec PASS／quality Approved，Critical0／Important0／Minor0代码问题。实际屏幕与Console验证由root另做，不能由batch geometry代替。worker释放时全部Assets3172／3172，只有8个批准文件变化，新增／缺失／越界0；UI结束后root逐hash核对3172／3172、增删改0，八个源码／Scene hash与GREEN源一致；见 `ui-assets-parity.json`。

Root 正常 Windows Editor 检查：Main在540×960、960×540显示浅色玩家卡，readiness独立可见；两尺寸Decor→Done后玩家卡恢复，黑色诊断未重现。Main原始未就绪时速度按钮继续受门禁，未把一次点击无变化写成2x通过。Validation横屏实际Open和下一Day、竖屏实际长Closing及Decor→Done保留两块黑色详细HUD，60／15标记和Epoch／订单字段可见。Validation游戏内Pause／2x均实际切换；另保存横屏Day2真实Rest00:06、LastDay2 CompletedDrain、Total0和双方员工Idle。未取得同轮清晰横屏Closing截图，不以旧图冒充。没有注入时钟、snapshot或Main验证布局。原始截图与逐项记录见[UI证据](../outputs/phase15/player-hud/ui/events-final.json)。

本次只接受该展示修正；Owner视觉喜好、Main完整正常装修开店／直接PaidVisitIds、Debugger异常、真实8h／device／CI／VCS／merged-main边界不变。原七项manual历史不冒充新source完整重跑，P15未正式Completed。

本轮共15张未编辑原生截图，全部文件存在；Console末次1info／0warning／0error，日志定向runtime exception检查0。Play已停止，未保存运行中Scene；Sky确认本次Editor窗口关闭。退出日志仍有mono abort_threads／debugger-agent监听诊断，未在本次调查，OS native handles保持UNKNOWN；不将正常UI零error解释为原生资源收尾通过。


## 18. 用户长柜台停滞复现（2026-10-27）

用户截图原Play已关闭，不能恢复原业务状态；按截图结构搭建近似6／7格连续柜台，CR和Pick-up在两端，CM单独。新增一个test及其meta，未改任何product、原test或Scene。

实际 `repro-3.xml` 8项=4对照PASS／4照片方向FAIL，Normal／Fast均复现。照片方向采用CR(length,4)、Pick-up(1,4)、CM(1,7)180°；对照方向每例完成20位真实离店。失败布局中一位已领取顾客被判“离场：没有安全通道”，仍占取餐队首；杯子达到ReadyForPickup、两位员工Idle，45游戏秒无新Completed。

随后同6格Fast仅加只读路线查询，`repro-4.xml` 1 FAIL：FIRST_BLOCK时入口和取餐顾客正在移动，出口被实际身体暂时占用（0.561＜0.911安全间距），当时安全出口计划NULL；交通结束后同一位置当前出口计划已完整、安全，但顾客仍保持Blocked，队列继续停滞。这定位了临时交通进入显式恢复故障状态并持续阻塞队首的链路，未实现修复；不把任意永久堵路改成自动重试，也不忽略身体或强制清场。

原始XML／心跳／只读查询和试验历史见[调查记录](../outputs/phase15/long-counter-stall/investigation.md)。repro-1编译NOT_RUN、repro-2的frame-end观察错误均保留，未当产品RED。当前3172既有Assets逐hash零变化，只新增批准test和meta（总3174），无Git／CI／Phase closure。当前经营问题为OPEN；HUD显示原因的缺口另列，尚未修改。

Owner随后补充原现场“整个游戏都没有反应”。上面的batch经营停滞证据不足以解释按钮／镜头无响应，原现场完整原因仍UNKNOWN；正在普通Editor保留近似现场检查真实输入与帧间隔，不将已复现的Blocked直接当作完整根因。

普通Editor限定复测结果：近似布局经营停滞仍成立，但实际鼠标Pause→1x、镜头滚轮、Decor→Done全部可响应；68条心跳跨134.39现实秒，帧号持续增长。原“全游戏无响应”仍NOT_REPRODUCED／原因UNKNOWN，不能把经营Blocked当其完整根因。Native观察运行最终因诊断harness180秒默认timeout失败，清理断言另列，不称整轮PASS；后续harness修正只通过编译／opt-in guard，未重跑native。补充batch1709帧最大间隔45.094ms、>500ms0，既有经营停滞1 FAIL、新native opt-in入口1 SKIP；不替代Player profile。详见调查记录末节、native-ui/events.json、native-input-heartbeat-summary.json。UI与帧证据限定于该近似现场。最终3172既有Assets零变化，仍只有批准的新test及meta。


### 18.1 已批准修复，执行中

Owner已明确确认只改CustomerFlowController.cs与CafeLongCounterStallTests.cs。当前按既有P15计划执行：补临时实际交通／静态阻挡／取消请求测试，保留RED，再做最小等待判定修复与直接回归。运行规则、碰撞、身份／付款／真实出口合同不变；未预填GREEN，不将全游戏无响应写成已解决。Implementation brief为 `.superpowers/sdd/2026-10-26-phase-15-business-day/exit-traffic-repair-brief.md`。

修复中间证据：新实际RED为照片方向4 FAIL及实际Entry等待guard3 FAIL；首轮guard fixture失败另列。green-2中取消／disable静态出口阻挡2 PASS，SixFast已20次真实离店但新增Collected历史断言使用了退休后的live集合而FAIL；原终点Paid FIFO断言当时已通过，最终必须保留。首轮完整P14回归尚在运行，samples另见reverse7Fast与photo7Fast未完成20人，当前修复尚非GREEN；两种失败分别继续调查活动交通与静态排队／安全取餐分配。所有失败保留，不用单例通过替代八布局验收。


首轮完整P14正式XML regress-play14-1：121 total＝116 PASS／4 FAIL／1 opt-in SKIP，627.7414247秒，原CLI1／native test-log2。失败为CafeLoopIntegrationTests.CompletesTwentyRealOrdersAndDrainsAllCapacityAtTwoX，以及CafePickupSideTests.UserLongCounterSevenFast、UserPhotoCounterSevenFast、UserPhotoCounterSevenNormal。natural八例为5 PASS／3 FAIL，新guard三例PASS。该轮为失败candidate证据；后续P15／P12／Edit均保持NOT_RUN，不能用子集通过冲抵本轮FAIL。

新静态证据（diagnose-1，3项1 PASS／2 FAIL）：photo7Fast在7次真实离店后，8位顾客均Queued且无活动请求；队首已有取餐预留，移除唯一静止折返队尾的只读反事实使前台→取餐完整路线恢复。拟新增「入店前保护已经真实预留的取餐通道」gate，仍两文件、runtime-only、无Scene／Save迁移；这扩展临时交通等待范围，已向Owner请求明确批准，当前PENDING，未实施。原TwoX及动态交通诊断继续。
### 18.2 冻结候选与最终现状测试（尚未验收）

[final14.xml](../outputs/phase15/exit-traffic-repair/final14.xml)实际14项＝9 PASS／4 FAIL／1 SKIP，173.0251079秒；原CLI1／native test-log2。原TwoX集成测试与四个guard通过，八个自然布局只4 PASS／4 FAIL。唯一SKIP为新opt-in UserPhotoCounterSixNativeInputProbe，本候选未做正常Editor UI验证。

| 最终FAIL | 现场边界 |
| --- | --- |
| UserLongCounterSevenNormal | 已发出的MovingToPickup在NavMesh边缘真实MovementTimeout，并有送杯通道阻挡 |
| UserPhotoCounterSevenFast／Normal | 八个静止Queued，无活动请求，未来前台→取餐通道被折返队尾封住 |
| UserPhotoCounterSixFast | MovingToPickupService尚未发出的下一腿与另一Queued顾客的正式Pickup reservation冲突；该顾客没有活动请求，不能当有限moving traffic移除 |

不能把局部guard通过解释成全部交通／reservation归属问题解决。独立审查当前聚焦post-replan冲突复验、等待证书未来target保留，以及future服务路与实际移动的归属边界。多个局部修正仍暴露跨状态失败，当前停止新产品patch并先完成诊断审查；入店通道保护只是待批准提案，尚不能断言足以解决四个FAIL。

冻结product SHA256为1A069B9101A6E8C1FC8BA862030608056FDF632FF8ACAE3E66022582F8E8BD34；test为26B4ECB08698D1F84BA9E8606816905D59A2F9150CE929DBAA3F373F79785655。3174 Assets仅原批准两份CS变化、缺失／新增0，Scene／meta／其余3172项未变。Root23:28:33独立逐hash对worker最终manifest读回3174项增删改0，见[root readback](../outputs/phase15/exit-traffic-repair/root-frozen-readback.json)。23:27:44指定Editor进程0并释放ownership；独立OS native exit／handles仍UNKNOWN。

全部早期失败、编译NOT_RUN、callback与readonly诊断source及退出证据见[实现报告](../.superpowers/sdd/2026-10-26-phase-15-business-day/exit-traffic-repair-report.md)。没有重跑新完整P14／P15／P12／Edit，没有修复后native UI、CI或VCS动作；当前无Phase closure、merge readiness或P16推进。
Owner后续澄清：原现场Editor顶部Stop、菜单及其他面板仍能操作，只有Game无响应。因此原症状记录为Game运行／输入失效，不再描述为整个Editor不可交互。近似现场的既有native Pause／镜头／Decor可响应，仍不能解释原Game输入失效；根因及是否涉及输入锁、时间门禁或其他运行状态继续UNKNOWN。此答复不是对新增入店通道规则的批准。
独立诊断审查正式结论：NOT GREEN，C0／I4／M1，见[review](../.superpowers/sdd/2026-10-26-phase-15-business-day/exit-traffic-diagnostic-review.md)。I1最终replan后的precedence复验、I2等待证书其他future target保留进入原批准范围fix round1；I3统一reserved／owned服务通道与新assignment发布边界仍需批准；I4边缘运动首个拒绝层UNKNOWN；M1反事实路线24 waypoint gate记录Minor deferred。当前fix1只处理I1／I2，不再叠加未证明的新策略。

先前仅admission通道的提案不能覆盖PhotoSixFast，因此已由[三文件后续提案](../.superpowers/sdd/2026-10-26-phase-15-business-day/stall-next-scope-proposal.md)替代并请求Owner明确批准：原controller／test增加通道归属规则，新增NavigationWorld.cs仅Editor/test opt-in记录已有guard阶段结果，不改导航行为。该扩展当前PENDING，前一份25–40分钟估计不能套用；底层行为修复需诊断后再说明。所有旧XML与hash保留，整体仍未验收。
Owner现已明确批准三文件「通道规则与导航诊断」提案，替代未批准的单独admission方案；以上PENDING是批准前历史。先完成原I1／I2，再执行controller／test服务通道规则及NavigationWorld.cs仅Editor/test opt-in诊断；不包含导航底层行为修改，不改Scene／Save／安全与营业规则。当前尚未实现新增scope，不把批准当作测试通过。
### 18.3 I1／I2 限定修正完成，已交接批准的新范围（2026-10-28）

fix1实际covering7／7 PASS、0 FAIL／0 SKIP、24.9944441秒，原CLI0／native log0。独立[fix1 review](../.superpowers/sdd/2026-10-26-phase-15-business-day/exit-traffic-fix1-review.md)确认I1／I2 ADDRESSED、新增C0／I0／M0。真正post-replan行为只独立实测Exit；Pickup为真实Entry下预选路线WAIT／恢复，独立Pickup post-replan仍NOT_VERIFIED，按已记录的shared gate组合覆盖接受，不能伪报两分支都实际重算。整个停滞修复仍NOT GREEN，I3／I4 open、M1 deferred。

当前reviewed product F3893E214DB037B05ED113017988151F7DF46A02EAC1B4FFE2DA7FBDF971B76A／test C8D404F7759A3FF1B983F02043F658A0EE73FDA4D4488AFF353B17E729FD6C03。3174 Assets仅两源变化、缺失／新增0，Scene／meta／其余3172项不变；Editor00:01:04释放，root00:02:08全3174逐hash读回增删改0，见fix1 receipts。旧final14四FAIL保持其原source，不与7PASS合并。未跑新八布局／完整P14／下游／native UI。

Owner已批准三文件通道规则与导航诊断，新实现agent现接管原controller／test及NavigationWorld.cs；先抓真实边缘拒绝阶段与PhotoSix预留冲突的实际先后，再执行已批准服务通道规则。NavigationWorld只允许Editor/test opt-in诊断，不包括导航底层行为修改。新的baseline与结果将独立绑定，不覆盖本节reviewed source。正式closure、CI／VCS与P16边界不变。
### 18.4 三文件诊断与第四文件边缘修复批准（2026-10-28）

新任务fresh baseline3174项，原I1／I2源码保留。diagnostic1启动因licensing失败且无XML，CLI1／native-536870911，CIM指定Editor0；diagnostic2内部hook的测试访问编译失败NOT_RUN，改test reflection绑定，不扩大API。随后diagnostic3实际2项1 PASS／1 FAIL、28.9502267秒，root已读XML；PhotoSixFast20真实出口但旧冲突未复现，不能关闭I3。

Reverse7Normal真实request38连续362个采样（overflow0）显示velocity／corner／dynamic／static均批准前进.02米，ClampToNavMesh首次归零，原retry后仍timeout。补充boundary4-red实际2 FAIL：真实admission产品RED（旧入口实际创建第二actor而应在reserve/create前拒绝）以及原边缘timeout。sameFilter纯query确认两ray在z≈0给出zero normal，水平offset≈1.15e-7小于epsilon .001，旧距离裁剪归零；raw hit.distance≈.0125含垂直映射，不能与水平门槛混用。

boundary5 agent报告admission／transfer2 PASS、边缘1 FAIL；原始未裁剪end的short CalculatePath为PathComplete且恰两个准确corner，end sample水平误差0；ClosestEdge(+1,0,0)距离0，向内.001／.01的仅query平行ray均畅通。这支持一个严格限定的边界退化证明，不允许直接忽略Raycast。官方[CalculatePath合同](https://docs.unity.com/en-us/engine/6000.5/script-reference/unityengine/ai/navmesh/calculatepath)的bool true也包括partial，必须单独验证status／corners。

Owner已根据[精确提案](../.superpowers/sdd/2026-10-26-phase-15-business-day/navmesh-boundary-repair-proposal.md)明确批准第四份NavMeshMovementDriver.cs，仅ClampToNavMesh退化分支与必要private helper；已有长柜台test增加反例。双ray近水平起点zero normal、有效Steady owned path／当前corner、closest edge切向不向外、原end准确、同Filter短完整两corner及向内仅query ray clear全部成立才保留原input，最终真实身体／家具sweep不变。其它导航层／安全门槛／预算／资产不扩大。当前批准不等于实现或验收通过；I3通道规则独立继续。
后续机制进展（不同source分别保留，非最终验收）：root读取publication-green1实际3／3 PASS（3.9087906秒），覆盖admission保留due、真实service owned route发布前拒绝、transfer实际body重验；normal1原Reverse7Normal实际1／1 PASS（26.6106063秒，原20次真实出口断言保留）。此前2x该布局亦有20出口PASS。BoundaryTangentRetainsNativeEdgeHoleAndBodyGuards实际PASS，原publication几何fixture失败另列NOT_RUN。取消／再预留与Decor退休目前agent报告通过，最终source与完整矩阵／直接回归／review尚待完成；不合并这些子集为完整P14或整体成功。
### 18.5 最新matrix1失败与四源冻结诊断交接（2026-10-28）

root已读取[matrix1.xml](../outputs/phase15/corridor-navigation/matrix1.xml)：25项＝18 PASS／7 FAIL／0 SKIP，166.0267103秒，worker原CLI1／native test-log2。机制4、Boundary guard、Controlled4、Fix1两项、yield五项和旧TwoX20出口通过；原八自然布局只有UserLongCounterSevenFast通过。之前normal1单例20出口PASS不稳定，不能覆盖本轮FAIL。没有启动新完整P14／P15／P12／Edit或正常Editor UI，也不重复matrix碰运气。

| matrix1自然FAIL | 当时completed／状态 |
| --- | --- |
| UserLongCounterSevenNormal |2；Blocked1／PickupWaiting2／Queued5，先有staff deliveryBlocked |
| UserLongCounterSixFast |2；Blocked1／PickupWaiting2／Queued3，FIRST_BLOCK Exiting |
| UserLongCounterSixNormal |5；Blocked1／PickupWaiting1／Queued8，FIRST_BLOCK Exiting |
| UserPhotoCounterSevenFast |2；Blocked1／PickupWaiting2／Queued5，FIRST_BLOCK Exiting |
| UserPhotoCounterSevenNormal |4；全8Queued，无FIRST_BLOCK，实际reservations0／canReserve=true；只读移除唯一尾客00012@(2.5,-.5)使front→head路线从NULL变complete |
| UserPhotoCounterSixFast |5；Blocked3／Queued3，首次MovingToPickup，segments2／replans1；后来正式target(-3.6,-1.6)挡next，后另有Exit timeout |
| UserPhotoCounterSixNormal |4；只剩Blocked2，首次同类future target冲突 |

现有日志没有每次决策的certificate有效性／owner／phase／generation／layout／真实reservation-count对照和actual chosen side→exit的acquire前后证明，不能直接断言stale prefix、统计错误或具体发布漏洞。已安排一次四文件限定独立诊断review，随后如需观察只统一扩三类最小代表，不加未证明的新WAIT／预测路线或入店政策。

矩阵绑定controller581FEA1F24EFA129E22D9F0DE04FE75652C063AF3896A84E1FEDA699C7F47C39；test685148BC46D24AEBB8AF980EE1BAED4F4825AD1FFE505D0F79AE873A7FC16B34；WorldA34A7D8A0A5BC55C347F25A8503D2BC05DA11D16638406FC04A2F9DD18872B0D；Driver74178032CA1A15D9690511FDED35739731621F8A2BBAB5A17304C5FFA1853EB6。3174资产仅批准四源变化，Scene／meta／其他3170项不变、缺失／新增0；指定Editor05:14:09Z count0并释放writer。root01:16:17全3174逐hash对冻结manifest读回增删改0，见[matrix root readback](../outputs/phase15/corridor-navigation/matrix1-root-readback.json)。OS handles仍UNKNOWN。

详细selector文件、实际命令、机制／fixture NOT_RUN／生命周期历史及各first／stalled时序见[当前实现报告](../.superpowers/sdd/2026-10-26-phase-15-business-day/corridor-navigation-report.md)。No GREEN、无正式closure／CI／VCS／P16。
四源独立[诊断审查](../.superpowers/sdd/2026-10-26-phase-15-business-day/corridor-navigation-matrix1-review.md)完成：NOT GREEN，C0／I2／M1。源代码确认两处证明缺口：完整旧path的已消费前缀会参与剩余保护有效性判断；实际获选collection side缺少当下exit证明及服务到出口的handoff。它们与七个自然失败的直接因果仍TRACE NEEDED。Nav严格fallback未发现具体新安全缺陷；不能用这些结论关闭整体问题。

现已在原四文件授权内统一补一次只读诊断，代表仅Long6Normal／Photo6Normal／Photo7Normal，记录真实reservations含unpaid对照、每个certificate身份／phase／generation／layout及skip reason、完整旧path与current segment／未发腿、所有新target发布、实际服务side acquire前后纯exitquery和实际exit dispatch。不改行为分支／NavDriver、不加未批准的blanket front admission gate或预测预留。原route24cap Minor仍独立deferred；本次观测覆盖Minor仅作为定位Important的必要证据补齐。

### 18.6 Round1 已拥有路线修正（2026-10-28，待 scoped re-review）

当前冻结源：CustomerFlowController `DD04F07A17D330D6E81E1C98F5D55A5EA53D115B5BDD80299331D8115E86013E`；原长柜台test `952732ED413C00FA92482EB83027CAD2985B8D566141958684E8C713A7CA01F7`。NavigationWorld／NavMeshMovementDriver保持matrix1字节不变。本轮只修正实际已选路线的时间范围、真实active request优先权和已捕获的严格finite-WAIT证明；不扩展未预留队首政策。

Root直接解析原XML：

| 当前源执行 | PASS | FAIL | SKIP | 耗时 | 结论 |
| --- | ---: | ---: | ---: | ---: | --- |
| `outputs/phase15/corridor-navigation/round1-fix-mechanism.xml` | 22 | 0 | 0 | 45.8032725s | 包含原2x20单、boundary反测、原controlled／Fix1／yield以及五项新增机制 |
| `outputs/phase15/corridor-navigation/round1-fix-replay.xml` | 2 | 1 | 0 | 158.7844842s | Reverse6／Photo6原20单真实离店PASS；Photo7 FAIL |

Photo7保留trace证明：实际队首10因前客真实owned corridor而推迟，之后仍无accepted空间预留；接待17时真实取餐reservations2=tracked2且CanReserve=false。首次后续provisional预留因route=NULL回滚，没有取得保护权。最终9单Completed、其余八位Queued，无顾客Blocked／delivery failure。不能把provisional generation计作成功预留，也未证明17是唯一必要阻塞者。这暴露已拥有路线规则之外的未预留队首接待问题，新行为尚未批准或实施。

三个replay trace均overflow：只能使用保留的早期决策事件及原测试最终断言，不声明完整因果时间线。I2 acquisition-time future-exit证明已由原reviewer纠正为待证实设计问题，不能据此强加未来出口预留；matrix1旧Exiting原因仍UNKNOWN。原Game输入无响应也仍未复现。

Root重算3174 Assets与冻结manifest：mismatch0／extra0／missing0，四源均与执行快照hash一致。Worker释放记录Editor0；root Get-Process Unity为空，新CIM查询被拒因此该查询状态UNKNOWN，OS handles仍UNKNOWN。原CLI/native分别mechanism0/0、replay1/2。源码冻结，独立delta-only re-review进行中；八场景、下游、CI、VCS和Phase closure NOT_RUN。完整失败历史、命令和证据索引见 `.superpowers/sdd/2026-10-26-phase-15-business-day/corridor-navigation-report.md`；root readback见 `outputs/phase15/corridor-navigation/round1-fix-root-readback.json`。

### 18.7 实际队首预留失败后的接待限流：首轮（2026-10-28，NOT GREEN）

Owner回复“继续就可以”后按原两个文件实施最小观察式门禁，不改导航／clock／调度／Scene／容量或原断言。原真实RED为0 PASS／1 FAIL，1.6838192s，失败点是已观察实际队首安全预留拒绝后TrySpawn仍True。修正源CFC `F6D5CFAAB2962BE7EBD0A6D554ED7B7ACB52895F18790AEC79D4CDBD1A7DABAF`、test `21229A5C90E237A8734BEACE29CBE61AF0CD15DA193D20190DE19AA5560B5129`：原22＋新6机制 **28 PASS／0 FAIL／0 SKIP，47.3525088s**；原Photo7单次source-change复测 **0 PASS／1 FAIL，54.0008878s**，完成13单后14／15在MovingToPickup相关阶段Blocked，16Queued、2delivery、staffIdle。八场景及下游NOT_RUN。

有限首决策trace无overflow，但主动过滤其他事件，不是完整movement时间线。front4真实拒绝→拒绝admission→真实accepted→下一位接纳，证明门禁实际生效。另有spec缺口I1：front15在同owner/layout下仅assignment前移为Advancing，尚无accepted pickup reservation，旧stamp却被generation／Arrived／Queued资格变化清掉，16获接纳；下一帧15才成功预留。Root确认“head更换”指Visit／owner identity更换，不指同Visit正常前移；这是原批准门禁生命周期修正，不新增交通策略。是否16充分导致14／15最终Blocked、最早static／dynamic／corner／clamp拒绝层仍UNKNOWN，不把该事件当唯一根因。

Root直接解析三XML并核对四hash、3174aftermanifest（mismatch0／extra0／missing0）、Unity process0；worker GetProcess／CIM均0，OS handles UNKNOWN。原机制／自然test body86269字符与八wrapper一致，旧高负荷整个文件hash不变。证据在 `outputs/phase15/unreserved-front-admission/`；命令与完整报告在 `.superpowers/sdd/2026-10-26-phase-15-business-day/unreserved-front-admission-report.md`。独立review定稿后进入同任务fix round1；不提前宣告修好或开始下游。

### 18.8 接待门禁fix1通过，但八组合仍有残余停滞（2026-10-28）

当前冻结CFC `3108563E34F37DEE1D7ACFE00EC2A4A7D4BDA7F7145CC9F975D5A71009B1D6FC`；test `193448E54C20D0853BB9032FEE6374CAFAC1A00761629F4B6E604B29CEE8F9D9`；World／Driver仍保持原A34A…72B0D／7417…53EB6。Fix1真实samehead advance RED0 PASS／1 FAIL，1.853593s；修正后29 PASS／0 FAIL／0 SKIP，46.7621103s。原Photo7单次source-change复测1 PASS，32.8165271s，保留20distinct paid／实际近出口／Completed／容量和delivery排空／员工Idle全部原断言。独立限定re-review确认I1 ADDRESSED，新增C0／I0／M0，不能据此宣告整体验收。

随后原八组合一次执行7 PASS／1 FAIL／0 SKIP，195.9141496s（`fix1-matrix.xml`）。唯一失败Reverse7Fast；其余七组合含Photo7 Normal／Fast全部通过。原保留sample：customer8 MovingToPickupService/request106，frame3679在(-3.525,0,2.499998)首次Blocked；员工此时仍CarryingToPickUp，frame3830才首见IncompletePath。首个移动／重规划拒绝层UNKNOWN，边缘点差不能解释为阈值缺陷。

Root只批准随后一次同源失败单例诊断：0 PASS／1 FAIL，18.993308s。该次转为customer7已领取后Exiting/request0／actualSegments0／无可执行route、collection lease尚保留的规划／等待资格拒绝；原customer8服务移动＋员工IncompletePath签名NOT_REPRODUCED。Filtered trace无overflow但非全时序，员工被原firstloss筛选排除；不能合并两次FAIL为同一根因，或认为必须预留尚未选择的未来出口。

Senior限定诊断判断指出EmployeeFlow.Environment.MoveTo可在调用NavigationService之前，因delivery／return precheck直接返回IncompletePath；只记录Driver或最终Blocked仍会缺证。下一步提案只增加CFC实际路线／等待分支、EmployeeFlow precheck/submission/callback、Driver BeginPath/Observe原计算结果及现有test统一观察；保持全部游戏行为与原断言，再一次原八组合诊断。涉及新增EmployeeFlow文件及Driver原Clamp-only以外观察位置，待Owner明确批准，尚未实施。提案和ruling见 `.superpowers/sdd/2026-10-26-phase-15-business-day/remaining-stall-observation-proposal.md`、`corridor-navigation-next-diagnostic-ruling.md`。

Root最终readback3174 Assets：mismatch0／extra0／missing0，四源吻合；Unityprocess0，worker指定Editor与importworker GetProcess／CIM0，OS handles UNKNOWN。证据见 `outputs/phase15/unreserved-front-admission/fix1-root-final-readback.json`。原高负荷、Play14／15、具名P11／P12、限定Edit及正常Editor输入复查全部NOT_RUN（八组合gate失败）。两个SceneBuilder生成类为无关范围明确NOT_RUN，不通过namespace广跑触发Scene写入。原Game输入无响应仍未复现；源码未提交，CI／merge／P15 closure／P16均未进行。

### 18.9 Owner批准的完整诊断链：观察实现与preflight（2026-10-28）

Owner明确回复call_daf9ad5ff18e4f4986463e0f5c5ff23c，批准诊断扩展。仅四既有文件增加默认关闭Editor/test观察：CFC原route／WAIT／segment判断，EmployeeFlow原precheck／submission／callback，Driver原BeginPath／Observe结果，原长柜台test统一收集。World原hook复用且字节不变。没有游戏行为修复、Scene/meta/Save/UI/参数或调度改动。

初版真实观察RED：0 PASS／1 FAIL，1.5930336s，原真实员工Arrived及同步InvalidTarget先通过，缺少观察记录才失败；新增3项观察GREEN：3 PASS／0 FAIL，3.4035587s。原29＋新3同源 **32 PASS／0 FAIL／0 SKIP，49.763514s**，CLI/native0/0。原八入口、原27Assert和29原逻辑保持；仅自然harness添加opt-in接线。五源hash及源码快照、命令、XML见 `outputs/phase15/stall-observation/`，interim报告见 `.superpowers/sdd/2026-10-26-phase-15-business-day/stall-observation-report.md`。

Root重算3174frozenmanifest（path-hash字典）：mismatch0／extra0／missing0，五源与执行快照吻合。早期root按array解析manifest失败，未得到资产漂移结论；错误receipt保留，已按实际结构正确readback。Worker精确Editor/worker nativeCIM／GetProcess0，排除Hub／CLI同名进程，OS handles UNKNOWN。

Root为确保获批唯一诊断机会有效，先安排同一task review的code/coverage部分，再GO八组合。初判发现caller UNASSIGNED会跨顾客合并，以及ring identity缺少path generation/layout/employee task generation的记录缺口。源码冻结，等待限定review定稿后在原批准范围补正；**原八组合新诊断仍NOT_RUN／HOLD**，不是通过32项即宣告经营修复。原matrix7 PASS／1 FAIL和Game输入UNKNOWN保留。

诊断preflight补充：首轮独立review为C0／I2／M1，已在同批准范围完成fix round1。两新增RED0 PASS／2 FAIL（2.0578728s），focused2 PASS（2.0700573s），最终原29＋原3＋新2共 **34 PASS／0 FAIL／0 SKIP，45.4568368s**，最终CLI/native0/0。真实两caller original planner调用证明caller/purpose保留；generation/layout/taskgen收集器测试明确synthetic metadata，不冒充真实Native recovery证明。

最终CFC `B616CF9020007ECB36F37EAF42F6BB667DA5B5C6B664503C59B1CB585090D964`；Employee `B2AFC2096BD9074AD5671AEB3FB416CDF201A87D5A00071094B3DF2E6A50FE49`；Driver `E3236CE04F26622DCABCBD545B0EAAFFBAD34939B69BA21803286953E7DB11E9`；test `AF21939DBDE174E9209E1262F4DDFFF7060534C1CA3DFEF73D409EBFF8960C86`；World仍A34A…72B0D。Root五hash＋3174最终manifest实际0mismatch／extra／missing。独立限定复核I1／I2 ADDRESSED，M1文案也修正，新C0／I0／M0；review SHA `D5C2AF2D54C6ABBC101F227CAA2A9EDA3CECB544C0195A7CA3A5665895685DED`。Root已GO一次原八组合统一诊断，运行中；当前未得到八组合结果，不代表经营已修复。无其它回归或新行为修复。

诊断最终结果：唯一原八组合统一trace-on batch **8 PASS／0 FAIL／0 SKIP，276.9861098s，CLI/native0/0**，全部保持20单真实离店原断言。独立runtime evidence review新增C0／I0／M0，SHA `478870890000397377576CDC3E49BA2C68C3FAA416FEEE3E08B63CD247D9DE66`。旧失败签名本次NOT_REPRODUCED，根因UNKNOWN，未宣称业务修复。记录窗口1711次Driver Begin均accepted且generation1、无rebind／Observe failure；320员工precheck全clear、320callback全Arrived。CFC53次实际whole-exit NULL、9次next及1次full guard拒绝，共63次非terminal拒绝均有同actor后续accepted→该request Arrived→最终removed的关联；不能拿这些成功等待证明另一历史失败的原因。零capacity drop不等于完整逐帧时间线。

本次case顺序／布局／速度receipts匹配旧run；seed150027及captureDeltaTime=1/60仅source/config parity，未另作新runtime读回。总时长增加81.0719602s、旧7个Over500ms／新0，但工作量不等（旧一例7单失败，本次20单完成），whole-frame也包含Editor/render/IO，不能认定日志开销或帧时间导致／消除了旧故障。无额外控制实验或第二batch。

Root随后正常MainCafe Play手工检查PARTIAL：新场景starter仅一counter，Setup incomplete；Decor／catalogue／cancel／Done和wheel zoom响应可观察，自动化拖拽未移动preview，未完成营业长柜台，Pause／2x营业中响应及经营停滞复现NOT_RUN。不得把工具手势失败当作已重现原Game无响应，也不得把非营业状态的UI响应当完整Main验收。恢复P5 portrait、停止Play、正常关闭Editor，无Save命令；手测截图和报告见 `outputs/phase15/stall-observation/main-manual/` 及 `.superpowers/sdd/2026-10-26-phase-15-business-day/main-manual-stall-check.md`。

Root手测后重算3174文件全部匹配冻结source，MainCafe Scene hash不变；14:13:49Z精确本worktree Editor/worker nativeCIM0，OS handles仍UNKNOWN。一次post-UI readback误把array manifest按dictionary读取，错误receipt仅解析错误不是漂移，正确readback保留。没有新Source修改、额外test或下游回归。追加一次同源关闭诊断的八组合对照仅提案，预计5–10分钟、无代码修改，因超过此前一次batch限制待Owner单独批准，尚未运行；见trace-off-contrast-proposal.md。P15／修复整体验收依旧未完成。

### 18.10 Owner追加批准的唯一关闭诊断对照（2026-10-28）

Owner明确回复“批准这一次关闭诊断对照”（call_2ee11640c33341ce87d89c0878fd746d）。仅追加一次原八组合，不修改代码、不重跑或启动下游。命令只含原logFile，无stallObservationTrace／corridorTrace／unreservedFrontTrace／longCounterNativeHold；selector文件与上一轮byte hash一致，五源hash与18.9最终冻结相同。

Root读取[原始XML](../outputs/phase15/trace-off-contrast/original8.xml)：**8项＝6 PASS／2 FAIL／0 SKIP，139.8352659秒**。实际执行顺序及结果：

| 原case | 结果 |
| --- | --- |
| UserLongCounterSevenFast | FAIL |
| UserLongCounterSevenNormal | FAIL |
| UserLongCounterSixFast | PASS |
| UserLongCounterSixNormal | PASS |
| UserPhotoCounterSevenFast | PASS |
| UserPhotoCounterSevenNormal | PASS |
| UserPhotoCounterSixFast | PASS |
| UserPhotoCounterSixNormal | PASS |

两次FAIL均只完成1单，顾客2停在(-3.525,0,2.499998)，状态Blocked、resume MovingToPickupService；最终request0，消息“入店：通道被占用”。Barista停在(-2.5,0,3.546059)，Blocked／IncompletePath。该消息不能直接等同于入店逻辑失败；本轮没有诊断链，首个移动／重规划／precheck拒绝层仍UNKNOWN。与旧Reverse7Fast的顾客8失败位置相似，但顾客身份及时间不同；与旧requestless Exiting单例也不同，不能合并根因。

Native log明确exit2。保存的PowerShell脚本没有单独持久化LASTEXITCODE，因此原CLI数值exit为UNKNOWN；outer shell0不表示测试成功。XML SHA256 `C510A854CEF83AC82830DAA560465CAB89E330EEF6B73AF823369CC9FA688702`。同源trace-on8 PASS与本次trace-off6 PASS／2 FAIL仅是两次实际结果，不证明日志记录造成、掩盖或修复故障；两轮完成工作量不同，也不据耗时推断因果。

Root独立重算3174 Assets全部匹配fix1-frozen字典，missing／extra／mismatch0，五源hash一致。Worker 14:40:53.7642742Z精确Editor／importworker GetProcess与CIM均0；Root 14:44:32Z再查精确Editor GetProcess0，fresh CIM查询被拒，记UNKNOWN，OS handles也为UNKNOWN。两份receipt分别保留，不以旧成功代替新查询。证据见 `outputs/phase15/trace-off-contrast/`，详细报告见 `.superpowers/sdd/2026-10-26-phase-15-business-day/trace-off-contrast-report.md`。本次授权执行完成；经营修复仍FAIL，Main输入问题仍UNKNOWN，P15／下游／正式验收不推进。

### 18.11 继续调查：仅失败时保存原始决定（2026-10-28）

Owner要求“继续修复，看看怎么回事”后，沿原问题继续调查。成功trace-on中customer2在同一转角的原next-check为clear，Barista在(1.5,0,2.5)；失败trace-off首次Blocked快照的Barista在(-2.5,0,3.546059)。两轮实际几何不同，历史firstguard依旧UNKNOWN。

只改CFC及原CafeLongCounterStallTests，新增默认关闭、UNITY_EDITOR-only failure hook；原next/full检查、WAIT及首次replan均拒绝后才格式化，保存原计算值，不补路径查询。测试端只在该callback读取已存员工route/index/selected target/task generation/MoveLeg/service request，短路未计算项标NOT_EVALUATED。原反射入口、自然27个assertions、seed150027、速度和布局保持。独立review初判I1员工路线metadata缺失、M1测试stepping未恢复，原实现者test-only修订后均ADDRESSED，新C0/I0/M0；没有游戏行为修复。

Root读取实际XML：初始RED为missing-hook assertion **0 PASS／2 FAIL，1.8430035s**；focused新2＋直接3为 **5 PASS／0 FAIL，7.6502962s**。Review修订RED为实际sink缺少字段 **0 PASS／1 FAIL，1.7361477s**，覆盖GREEN **2 PASS／0 FAIL，2.1442347s**。RED均CLI8/native2，GREEN均0/0。Staged tests通过真实adapter/planner的不可达waypoint验证启用／关闭hook不改变Block、预算、route、pose、generation和capacity；员工字段断言取真实idle Runner，不冒充active/native证据。

唯一两场自然复现，[natural2.xml](../outputs/phase15/segment-failure-witness/natural2.xml) **2 PASS／0 FAIL／0 SKIP，58.4000994s，CLI/native0/0**：SevenFast21.696267s、SevenNormal36.554547s，各20单实际离店、容量排空；两个cold sink均records0/dropped0。没有捕获实际拒绝，旧故障NOT_REPRODUCED／根因UNKNOWN，不宣称fixed。XML SHA256 `1E6532881960FD75A9F10E36FB56BD11486D65E9BE145846D3ABF15602FDB1B9`。

当前冻结CFC `92CE62D113105ED8BE2054E54459BEC211A9EAA6A181DBA92542DC5688F09617`，test `505ADC1AACAA17D0DDC303E27792CCABE686E9729990222528CA067295EA421B`；Employee／Driver／World保持18.9源。3174 Assets相对review freeze零变化、package未变、精确Editor0，OS handles UNKNOWN。原始记录 `outputs/phase15/segment-failure-witness/`，详情 `.superpowers/sdd/2026-10-26-phase-15-business-day/segment-failure-witness-report.md`。下一步先准备受控路线交接复现，不反复执行同一自然batch，不据body快照修改WAIT或导航容差。

### 18.12 受控真实取得顺序：捕获后续C3／C4互锁（2026-10-28）

新增两例test-only控制原delivery gate及collection先后，原AutoSpawn最多4人，所有订单／请求／移动／付款与离店走原流程。未warp、写假route/request或单独Tick。初版先等c1exit的设计在执行前撤回，改为并发正常流程；实际c1body/lease仅记录，不强留旧lease，不假定它导致换侧。原自然八入口及body、27assertions保持。静态review补正目标左通道资格后GO一次两例。

第一次运行 **0 PASS／2 FAIL，5.7579277s，CLI8/native2**，在新测试读取合法未初始化pickupQueue时NRE，尚未进入目标场景。外部Update异常中断iterator后，teardown又销毁未离场body产生额外距离断言。此为测试自身失败，不作产品RED；当时finally清理UNKNOWN，原始XML/first-NRE保存。

最小修正只在新helper处理UNINITIALIZED／UNKNOWN，并在CafePickupSideTests.Cleanup加入一行调用幂等cleanup Action，保证scene unload前detach；实际exit/order/token断言未减弱，无LogAssert.Expect或日志抑制。独立fix2静态C0/I0后，一次固定新源两例重跑，[原始XML](../outputs/phase15/controlled-handoff/fix2-controlled2.xml) **1 PASS／1 FAIL／0 SKIP，11.6570314s，CLI8/native2**，SHA256 `F2AF6A380535C20B023621FBEA632AB923E066F4E3637ABD0462D5A4F71B22EF`。

- customer-first：6.707781s PASS，4人真实离场，FIFO／原token／单次delivery／容量清零断言通过；第三handle的原gate1462机会、1461拒绝，最终request39。没有同时取得目标交叉路线，`PRECONDITION_NOT_REPRODUCED`，不称修复。
- staff-first：4.782977s FAIL。初始handle3 request24先于customer2 request26，但c2使用短front路线，计划的c2/c3左侧拓扑没有形成。**后续原流程的customer3与Barista handle4出现真实产品Blocked**，已真实退出2人；drain18.23323gameSeconds、无完成17.03325秒，没有达到任何fixture时限。

唯一原始cold witness frame6314：customer3上一腿request34实际Arrived/None，generation15；from(-3.525,0,2.499998)→next(-3.239676,0,3.239676)的原nextPath为完整两点，nextClear=False。原WAIT=`EmployeeNoPath`，active／agentExists／enabled／onNavMesh／complete=True，stopped／pending=False，hasPath=False；destination因短路NOT_EVALUATED。首次replan=NULL、segments4/replans1。实际Barista在(-2.5,0,3.546059)，Carrying handle4，deliveryRoute index1的已选剩余路线向(-3.239676,3.239676)→(-3.546059,2.5)→(3.5,.5)，与顾客相向；MoveLeg/service request=NULL。顾客lease(3.5,2.5)、员工lease(3.5,.5)，没有释放／替换身份。

上述证明这次是**完整路径被实际身体挡住、WAIT拒绝后replan失败**，不是next native path=NULL。Barista处于pendingDeliveryTraffic是强推断，字段未直接记录；实际C3/C4 acquire先后仍UNKNOWN。它与历史同一卡点／失败族一致，但不能把本次证据冒充历史完整因果。

两例cleanup均normalfinally，snapshot/task/exitReceipts/gate/collection/coldHook各项managed readback通过；basefallback本次未动态触发，只具静态覆盖，旧清理UNKNOWN保留。精确/allEditor0、3174 Assets相对freeze零变化、package不变，OS handles UNKNOWN。当前LongCounter test `84F3D6777D87927FF3E96E728D677D2CD0B853B3C6BFE5201E1DBF947FDE0483`，Base `6506EF857461D0DE09EA0AC143D1255CFB1EAD29A1AE97CB0C5C005534C28624`，CFC仍92CE…9617，其余运行源未改。

独立Engineering判断最小完整修复应补齐双方真实owned route保护：客户先选时不能漏掉严格有限证明的request0未发腿；员工先选时provider须提供实际outward suffix，而非仅endpoint→return，同时保持有限WAIT分类。四文件提案 `owned-service-route-repair-proposal.md` 已发Owner确认，**尚未实施**。原始证据与详细report在 `outputs/phase15/controlled-handoff/`及同plan workspace的`controlled-handoff-report.md`。P15／原Main输入问题／正式验收不收口。

### 18.13 已批准四文件双向修复：局部GREEN，原八仍7 PASS／1 FAIL

Owner明确批准call_72f19533c2564aa7821910031d28b6bc。只改CFC／EmployeeFlow／Runner／原LongCounter test，base6506及导航底层／Scene／Save／时间／容量不改。新Runner只读实际outward suffix，Flow把当前pose、outward及已选return交给原provider，并保留真实Delivered同步回调中的return；CFC补齐有资格pending0和contributor的保护，新WAIT分支先验证候选的全量物理障碍。原active请求检查保留。

真实RED分别抓到provider未来endpoint错误、pending owner候选错误放行、Delivered回调丢return，以及独立parked body仍被错误判finiteWAIT。初次Assert.Multiple编译NOT_RUN及contributor直达query未形成的fixture失败分列保留；后者用完整两段实际native candidate验证，没有假request／proof。全部原assert不减弱。

旧静止墙回归曾失败，没有豁免：一次before三runtime对照原case1 PASS／5.1298421s并原样恢复current bytes；一次限定probe的两条原结果显示新增helper重复保护active request10，保守缓存仍带已走Counter prefix，改变决策时机。新增检查因此收窄为原缺口pending0，active原guard不改。临时probe源码／输出归档后撤回，原dense-wall Blocked＋显式恢复测试重新通过。

最终四源：CFC `EE6F9FC5D994B6407B20B919F554E2683406D74107DBE38DBBE4EA2F182E5424`；Flow `9A972CA6671DC50056995B4A7F2A5F34A012FBABD73299224D49FD6D8AAB569E`；Runner `B8B438F7939896150CD855E4A9E60BF6AC149EC1A4DD56B649B9145F8B2FE0A9`；Long test `20633EFC3EBA8BE0D93DF5A44A1FEE99AF3C2368C691EDA201C33E114F423B4E`。同源实际结果：

- final-focused-direct.xml：**23 PASS／0 FAIL，38.6216381s**。
- final-direct-edit.xml：**8 PASS／0 FAIL，0.4298797s**，是Runner直接生命周期检查，非八布局矩阵。
- final-controlled2.xml：**2 PASS／0 FAIL，15.2902778s**，每例4真实退出及原FIFO／token／delivery／capacity／cleanup通过。目标crossing均PRECONDITION_NOT_REPRODUCED，不宣称复现crossing已解决。事件记录的当前C3先于handle4选择不重写旧UNKNOWN。

独立task code spec／quality PASS、C0/I0/M0；同席仅新增targeted runtime证据review PASS，无新C/I。Root实际XML及四hash／3174边界核验一致后GO一次原八，代码review结论不替代后续矩阵结果。

原八 [original8.xml](../outputs/phase15/owned-service-route-repair/original8.xml)：**7 PASS／1 FAIL／0 SKIP，172.5953467s，CLI8/native2**。其余七例保留各20真实出口等原全部断言。唯一UserLongCounterSevenNormal，Prepared送杯路线取得Blocked，45gameSeconds无完成；最终Completed2/seen12/actualExit2。所有case的segment cold记录0，未触发顾客下一腿失败hook，不能复用旧转角firstguard解释本次。

首次观察frame8829：Completed1／seen8／actualExit1；C2 MovingToPickupService，Request0、PendingPickup=True，body(3.5,0,-1.7)、service lease(3.5,0,2.5)，所选长route经过左通道。C4有真实MovingToPickup request29，C7 Advancing32、C8 Entering28；Barista在work(1.5,0,2.5)已Prepared/Blocked。原candidate、physical-clear及C2 strict proof是否有效／谁是contributor未记录，均UNKNOWN；C2后来实际request43并完成不是原判定的proof receipt。不得一概省略C2身体／正式目标／lease。

原八后source／3174／base／package一致，Editor／worker已退出，OS handle closure不宣称。代码与旧FAIL不覆盖。继续同四文件限定delivery-acquisition事件witness：保留原计算结果及静态拒绝前身份；原短路未求值仍NOT_EVALUATED。若在原拒绝后单次纯只读资格校验，明确POST_DECISION_QUERY，不冒充原结果。仅一次SevenNormal诊断、非新验收重试；未授权新策略／底层或Scene改动。

完整report、review及所有原始XML／hash／命令／前后source在 `.superpowers/sdd/2026-10-26-phase-15-business-day/owned-service-route-repair-*.md` 和 `outputs/phase15/owned-service-route-repair/`。当前整体验收仍FAIL，Main输入UNKNOWN，未运行下游／全Phase／CI或merge。

### 18.14 唯一取得诊断未复现目标，转为固定反例验证

`acquire-diagnostic/seven-normal-diagnostic.xml`实际 **0 PASS／1 FAIL，31.9142696s，CLI8/native2**。目标delivery-acquisition static refusal **NOT_REPRODUCED**，captured=False／postQueries=0；不能据此确定原八失败中身体、目标、lease或strict proof的作用。诊断为默认关闭的原结果记录，原短路标NOT_EVALUATED；没有改变游戏分支。

本场另一故障：Completed9／actualExit8／seen19，C9已取餐但Exiting Blocked，原因“离场：没有安全通道”。首个观察frame9671的Barista为Returning h9，末尾h16是后续状态。事后native候选存在但全身体clear=False／currentExitPlan=NULL；这些不是原exit拒绝层回执，也不证明物理上绝无路线。有限候选查询及去除同坐标障碍不能据此推出扩预算、忽略身体或保留未来出口的新政策。

冻结CFC `6100CD6449A36F7ADA508FDE0F7CD78FA2E06115BBA20AEE57E518E29C7EABD3`、Long `7293ADED6BC222E59F341BBE9297CD48489CE0599352BD9B07CAA112F7BC9AAC`，Flow／Runner保持18.13值。默认关闭诊断cold checks 2 PASS／1.7911727s；只证明hook与原短路／token parity，非经营验收。运行期间3174 Assets零差异，base6506与package未变，Editor／worker0；不宣称OS handles已证实关闭。完整证据见 `outputs/phase15/owned-service-route-repair/acquire-diagnostic/report.md`。

接下来只在已批准四文件范围写两个focused反例，先验证真实前提并取得RED，再决定最小修复：①真实service lease与strict pending proof下，区分有界依赖和独立物理阻塞，WAIT必须随proof／owner变化重新判定；②真实NextAdmissionPosition及原spawn→tail路径，验证tail安全而完整incoming穿过有效pending-owned路线时不得发布新Actor／ID／token。所有实际身体、独立目标及lease继续保留；不追加自然重跑。C9出口规则仍未定位，未来出口预留／planner预算修改不属于此批准。

### 18.15 固定A／B反例与最小修复：9 PASS，等待review

A首次共享fixture实际满足核心前提，但测试误断言取餐后不能有新Exiting ownership；B首次未满足tail安全前提。原0 PASS／2 FAIL保存为test defect及fixture failure，不作产品RED。A只修旧service proof退休断言；B仅用一个有几何依据的固定替代：真实C2已付款、离开Counter并MovingToPickup，无slots修改或几何搜索。

实际产品RED：A `focused-guards/a-corrected-red.xml` **0 PASS／1 FAIL／3.2806541s，CLI8/native2**。真实Ready／paid／Arrived顾客取得原side lease(2.5,0,1.5)，strict proof依赖真实request7；候选仅被该owner未占用formal target／lease阻塞，全部实际身体及外来来源clear，实际acquire仍拒绝，但HasDeliveryTraffic=False。恢复canonical后实际Completed、lease释放、旧proof／binding退休及原tokens均已通过，再在预期WAIT断言失败。它证明此guard缺口，不冒充历史原八的首次拒绝。

B `focused-guards/b-fixed-red.xml` **0 PASS／1 FAIL／2.4294357s，CLI8/native2**。真实MovingToPickup request8；actual tail(-2.5,0,0.5)与owner／contributor均安全，原spawn(-0.5,0,-3.35)→tail完整incoming穿过pending owner路线，原active guard允许。实际CanStart／TrySpawn=True，visits2→3、nextId2→3，在预期拒绝断言失败。

最小修复仅CFC与Long test。新WAIT分类每次要求当前pending MovingToPickupService、真实匹配lease／assignment、尚未到位、有效ownership及strict proof；只按owner身份排除那个未占用formal target／lease，保留全部实际身体与外来同坐标来源。实际RouteObstacles、acquisition及碰撞不变。入店在原incoming构造后复用pending ownership冲突检查；旧active following loop不改。Flow／Runner不变。

`focused-guards/green.xml`实际 **9 PASS／0 FAIL／0 SKIP，19.4244241s，CLI0/native0**。包括A proof／phase失效、独立静止身体、lease同坐标外来身体、真实取餐及预留释放；B拒绝且不发布Actor／ID／token／assignment；旧pending／identity／静止墙Blocked后显式恢复及有限Counter工作回归。当前四源CFC `5BC450556F1EC91F720454A6446B7352BEFA7FEE1E10013A494A38DD3BD97A50`、Long `61B9DC8724A2C64BA0D2AE13D4DC9E896C92036C763F3D3BD6377C47F3698B5C`，Flow／Runner保持18.13值。3174 Assets运行中零差异，base6506／package不变，Editor／worker0；无OS handles闭合声明。

新delta独立spec／quality review PASS，新增C0／I0／M0；只检查此增量及对应证据，不重审未变的旧实现。Root据此批准新冻结candidate一次controlled2＋原八合并10例，不自动追加重试；flags仅原segment／acquisition事件记录，不启用逐帧导航查询。原有7份固定输出在执行前复制并校验SHA256，本次结果另存后恢复旧bytes，旧FAIL／NOT_REPRODUCED证据不覆盖。实际运行结果尚待完成；历史7 PASS／1 FAIL、C9及Main输入边界保留。报告与只供阅读的换行归一diff见 `outputs/phase15/owned-service-route-repair/focused-guards/`；运行源码没有为美化diff重写。

### 18.16 新冻结source合并10例：9 PASS／1 FAIL，C3离场Blocked

`post-guard-regression/ten-cases.xml`实际 **9 PASS／1 FAIL／0 SKIP，185.6263981s，CLI8/native2**。其中原八为7 PASS／1 FAIL，其余七例各保留20个真实出口；controlled2均完成每场4个真实出口及原FIFO／token／delivery／capacity／cleanup。两场目标crossing仍PRECONDITION_NOT_REPRODUCED，不证明复现目标互锁后恢复。

唯一UserLongCounterSevenNormal失败，Completed3／actualExit2／seen13；C3已收杯但Exiting Blocked，“离场：没有安全通道”，deliveryFailure为空。首次观察frame20162，C3(3.5,0,0.499998)，request0／pending标记false；Barista Returning h3/O3，最终h10是后续状态。其他pickup顾客形成等候队列，C12／C13均Queued且request0。所有10例segment cold记录0；acquisition目标captured=False／postQueries0，不能据此推导旧Prepared拒绝已修好。

首次失败后的native查询显示currentExitPlan=NULL、nativeClear=False、56个保留身体的单via检查NONE；这些是事后查询，不是原first-decision回执，也不证明全局无路。不能将readonlyRemove的pickup规划结果误作exit规划证据。原始first-block／failure／natural／cold／acquisition各自保存在 `post-guard-regression/cases/UserLongCounterSevenNormal/`。

冻结四源仍5BC4／9A97／B8B4／61B9，3174 Assets运行中零差异，base6506／package未变，Editor／worker0。7份历史fixed sink已按原SHA恢复，新结果复制并单独hash绑定；旧FAIL／NOT_REPRODUCED不覆盖。无额外自然case重试、下游suite、CI或Git操作。

下一步仅准备Long test默认关闭的consumer，订阅CFC已有的原exit dispatch、native／boundary候选、WAIT及route.blocked事件，在首次真实collected exit Blocked时冻结原始输入／结果和障碍来源。无新CFC／World／Driver查询或游戏规则变更；静态review后才决定一次限定诊断。未来出口预留、planner预算及Main输入修复尚未获本步授权。

### 18.17 Exit事件诊断：1 PASS，但目标NOT_REPRODUCED

只改Long test consumer，独立静态review C0／I0／M0；原production5BC4／9A97／B8B4不变，Long冻结 `51FA68F048188AE94207FBC21CF6C74447D68FC3802B98C86DB319776C1D1C6F`。消费已有CFC事件，原direct记录是planner返回值，其内部native／ring拒绝NOT_OBSERVED；boundary-leg才是实际原native结果。保留derived clearance、未观察WAIT及短路标签，无新增导航查询。

唯一SevenNormal `exit-decision-witness/runtime-one/seven-normal.xml`实际 **1 PASS／0 FAIL／34.8305942s，CLI0/native0**，20位顾客真实离店、订单完成、容量清零。目标collected exit Blocked **NOT_REPRODUCED**；captured=False、overflow0、errors0、原hookRestored=True，segment cold0。没有捕获失败planner／WAIT链，历史10例9 PASS／1 FAIL仍有效，不能据单场通过声称经营／Main输入修好。

3174 Assets运行中零差异、四hash／base6506／package一致，Editor／worker0，未重复自然case。转而准备一个固定native-query fixture：用18.16 C3事后快照的12个六位小数障碍重建Long7，只对“去掉具名C12 Counter队首身体”做一次11点对照。标记POST_FAILURE_QUANTIZED_SNAPSHOT_REPLAY，区分重建fixture的filter与历史agent-type ID，不伪造业务状态或历史原决定；不改变实际碰撞。其结果尚未执行。

### 18.18 固定快照12点／11点对照：Counter队首身体封住返回路线

`quantized-exit-query/runtime-one/query.xml`实际 **1 PASS／0 FAIL／2.1167222s，CLI0**，root已独立读取XML及CLI回执。此PASS仅表示一次本地几何诊断执行完成，不是产品RED／GREEN或自然经营验收。

权威C3快照源 `post-guard-regression/cases/UserLongCounterSevenNormal/natural.txt` 284–287，SHA256 `0304F36A12BD2F283B0EFA5CE7B4B7A6E999C6F4ADC0EDA8C90058D68AFF3688`。重建原Long7，用当前相同方法／settings做POST_FAILURE_QUANTIZED_SNAPSHOT_REPLAY，不声称新bake的agent-type ID等同历史ID。无假订单、arrival、request或proof，没有移动／删除真实Actor。

- Full12：原direct planner返回NULL；四个boundary候选分别在c0l0、c1l1、c2l0、c3l0拒绝，其中c1l0实际clear。内部direct native／ring拒绝仍NOT_OBSERVED。
- 仅去index8具名C12 Counter队首身体(-2.5,0,0.5)，其他11点全部保留：原direct planner仍NULL；原boundary c0三段clear，返回(-3.525,0,0.4999982)→(-3.525,0,-3.35)→(0.5,0,-3.35)。无继续候选或额外删人搜索。
- 后置验证只检查该实际返回路线：对原11点full-clear=True；对完整12点=False；对具名C12身体=False。C12的Queued assignment是同一身体的context，不是原数组中另一foreign target。该结果支持“此量化快照中的返回路线被C12身体封住”，不证明全局无路线、历史原调用的完整拒绝层或已根治。

独立source核对：建队caller是实际Counter队首，`RouteObstacles(visit.Id)`排除它自身，再加入未来Pick-up tail；`counterWaitSlots[0]`会被后续Counter顾客重新占据。现有点间距检查不保护经过head的通路。最终collection还只验证到选中side的路线及员工通路，没有校验该选中side→exit；早期Any(side)不等同最终选侧证明。后续若修改这些eligibility规则，须明确接受可用候位减少及查询成本风险，不能新增永久WAIT或全局忽略身体。

该次tested Long `90141911315BB7C36B7B22AD0F9C9939E120DAB464FCB4F2CF712FD7DF831283`；运行后3174 Assets零差异，originalHookRestored=True／errors0，production5BC4／9A97／B8B4、base6506及package不变。Editor退出后，只加两行本地flag前置Ignore，避免normal／CI依赖本地outputs；最终Long `D825F175105A8AFF97935BF05AF51FD763F5882AFA468DF4A34C4D1D8A5C7A0E`。这两行仅静态核对，runtime NOT_RUN，不冒充已实测SKIP。

最后release及root当前进程读取均Editor0；不宣称OS handles闭合。全部原始输入、原事件、后置验证、hash与source/parity在 `outputs/phase15/owned-service-route-repair/quantized-exit-query/`。无新的产品规则、自然重跑、下游suite、CI／Git或Phase closure。

### 18.19 Owner批准两文件eligibility修复：实施中

Owner对上一轮明确列出的两个文件／两条规则回复“修复吧”。[批准设计](superpowers/specs/2026-10-28-pickup-exit-eligibility-design.md)及canonical plan Task7记录完整范围：CFC仅geometry eligibility计入真实Counter head的连续身体／到站包络，collection acquire前核验实际candidate side→exit；原Long test补真实前提的RED／GREEN、正常与独立阻塞反例。有效候位可能减少，不改物理碰撞、容量上限、营业时间或导航预算，不缓存未来出口，不增加无证明WAIT。

Fresh implementer独占两个Assets及sole Editor，root维护Docs；before CFC5BC4／LongD825，Flow9A97／RunnerB8B4／base6506及其他Assets只读。先focused GREEN和独立task review，再GO源变更后一次原八；key matrix通过后做必要P14／P15关联集成检查。当前没有新的PASS或修复完成声明，历史经营FAIL与Main输入UNKNOWN保留。

Task7实际TDD：`pickup-exit-eligibility/red-01/tests.xml`为 **0 PASS／2 FAIL，5.3522985s，CLI8/native2**，分别是旧实际候位链缺少连续head范围和旧服务侧在exit不可用时仍发布lease。不是fixture RED。首次修后两个新行为及真实离店走通，但收尾测试错误要求历史reservation列表为空；只改为原tokens保留、Released且Used0，旧失败保留。后续11项两次10 PASS／1 FAIL均是旧pending-admission fixture的准备前提失败，未到产品断言，没有因此放松产品安全。

唯一该fixture诊断表明：新gate允许取得service lease的较晚时刻，真实contributor身体已挡住原人工detour。只改测试时序：先真实acquire C1 lease，public方法返回后立即Hold原初始移动、保留lease与原未到位状态；C2正常付款／MovingToPickup后再stage原固定未发路线。原作者只读确认此时序；全量原full-clear／strict proof／同一request／safe-tail／incoming冲突／old-active允许／零发布断言保留，无假订单或Ready状态。

`focused-03/tests.xml`实际 **11 PASS／0 FAIL／25.5332303s，CLI0/native0**，root已读取XML及CLI；自然method前后同边界bytes完全相同（本边界15778 bytes／SHA E0FCA127…C7C2C）、27assert／seed150027保持。历史15961是另一截取边界的元数据，不是改代码的目标。

候选CFC `B00B3E360449C573A3B60A80E34006974C0F0B925D40D79FF0098969AEC0CA43`，Long `67F98D7007BF5FEE2B5ACA1234AA64FECC011E3B07DBE6FD3340556C2819DB7F`。包络由caller(r+skin+eps)+max_prefab(r+TargetSampleDistance+eps+ArrivalDistance)计算，当前默认1.142；原P11采样与到站contract支持此保守范围，物理参数未改。当前Long7实际候位9→8，不将8硬编码为规则。新query-filter会影响构建分支及拒绝重试成本；已记录数千次native查询的限定fixture计数，不宣称性能不变或全局可达保证。

3174 Assets仅两目标变动，Flow9A97／RunnerB8B4／base6506、package/version保持，Editor0；OS handles不宣称闭合。实现报告与可读diff位于同SDD plan workspace及`outputs/phase15/pickup-exit-eligibility/`。当前独立task review尚在进行，没有执行新原八／full suite／CI／Git；历史FAIL仍保留。

独立Task7 spec／quality review **PASS，C0／I0／M0**，报告 `pickup-exit-eligibility-review.md`。源码／diff／11项XML与契约核验一致；指出已有retry没有累计query上限，focused不能证明自然拥堵的性能或有限进展。Root据此GO新candidate一次原八＋原controlled2，flags仅cold segment witness。

`pickup-exit-eligibility/matrix-01/ten-cases.xml`实际 **10 PASS／0 FAIL／0 SKIP，369.2084587s，CLI0/native0**。原八均达到各20真实出口和原订单／token／FIFO／capacity断言；controlled2各4真实出口，normalfinally／完成断言通过，但计划的crossing仍PRECONDITION_NOT_REPRODUCED。10例cold records0／dropped0，未见first-block。此结果是新candidate的实际经营通过，旧FAIL没有删除，历史精确同因／Main输入不能由本次推定。

现成whole-frame数据共70985帧，15帧>500ms、最长818.736ms；包含Editor／render／IO等共享耗时，不是排他CPU测量，未作跨源性能因果或移动设备FPS保证。3174 Assets对focus freeze零变化，四source／package一致、Editor／worker0；6份controlled历史fixed sink已保存本次结果后恢复原bytes／SHA，native handles未声称闭合。

当前继续默认flags的完整P14／P15 Play namespace关联集成验证，属于本次候位／取餐／Pause／Decor／Closing真实排空的必要gate；仅其通过后执行PickupQueue／PickupSingleFilePlanner／PickupSides／CafeLoopContract／BusinessDayDrain／BusinessDayService六类Edit。不会运行无关asset／Scene builders或全项目suite，不补报旧P12／Nav正式gate为新通过。14个会复用的旧固定产物要求运行前按hash保护，默认local snapshot及Native input opt-in必须分别记录SKIP理由。尚无该批新结果，无CI／Git或Phase closure。

### 18.20 默认P14／P15关联Play：220 PASS／9 FAIL／2 SKIP

`pickup-exit-eligibility/integration-play-01/tests.xml`实际 **231 total／220 PASS／9 FAIL／2 SKIP，967.4159079s，CLI8/native2**，root已独立读取XML。P15为70／70 PASS；P14为150 PASS／9 FAIL／2 SKIP。本批原八6 PASS／2 FAIL，原独立matrix10 PASS不是本批的结果，也不是修复已充分的证明。

失败：BlockedCollectedExitKeepsOverCapacityTokensThenRepairAndNewEpochRejectOldSuccess；ControlledLongSevenReadyCustomerAcquiresBeforeStaff；ControlledPendingCollectedExitNextLegKeepsRouteAndBudgetDuringIndependentPickup；Fix1ReplannedExitRetainsCandidateUntilAcceptedTrafficClears；MatrixExposedButDisconnectedSidesBlockWithoutChargeThenFormalRepairServes；SingleSideHeldReadyStartsSafeCollectionBeforeActualMachineReturn；UserLongCounterSevenNormal；UserLongCounterSixNormal；CashierNeverStartsWithSlotOnEarlierPickupRemainingPath。原始完整message／stack在`failures-and-skips.md`，不将这些一概解释为fixture失败。

Long7Normal：C5完成收杯后在upper side(3.5,2.5)离场Blocked／没有安全通道，最终actualExit4；Long6Normal：C4在upper side(2.5,2.5)的已选离场腿Blocked／通道被占用，最终actualExit3。这与旧C3 lower-side静态快照不是同一完整原决定链。新侧位检查在取得lease时算出的path未保存；该path后来是否仍安全、输入何时改变、是否因候选集变化而重新搜索失败，均需原事件证据，不能直接推定需要cache或预算扩展。

controlled customer-first未进入armed阶段，setup `concurrent-original-arrivals-second-ready`45s内未满足；末次snapshot有真实C1 Exiting、Barista carrying C2接近服务侧且blocked=False。setup预算与路程可能相关，尚不能仅据此宣告产品正常或直接加时；staff-first本批4真实出口／normalfinally通过，目标crossing仍未复现。

两个实际SKIP均有明确opt-in理由：本地quantized snapshot在任何case IO前Ignore、NativeInput probe未启用；其余不记为SKIP。14个fixed历史文件全部已另存本次结果并恢复原SHA，28份新动态证据归档；3174 Assets／四source／package/version无漂移，Editor／worker0，native handles不宣称闭合。EditMode按gate NOT_RUN；无原样重跑、产品修改、Git／CI或Phase closure。

Root已要求原实现者与同席reviewer仅读这9项的准确source／evidence进行分组，先识别真实业务／新eligibility错误拒绝／旧准备前提，不再盲跑自然场景。当前不授权未来exit ownership、扩大planner预算或编辑第三个Assets文件；范围内常规修正会据证据继续，必要设计／scope变动才另行解释。

### 18.21 诊断批次：1 PASS／2 FAIL，取证接线缺陷待修

`pickup-exit-eligibility/diagnostic-runtime-01/tests.xml`实际 **1 PASS／2 FAIL，69.2953598s，CLI8/native2**，XML SHA256 `4F9D86E5597F50FA69E0F8EBA235A09DA20A6BD66B880212A71C233192BB6673`。此批不是产品RED／GREEN：新geometry case先在“所有side的原direct均穿过head范围”前提失败，未到预期机制断言；不能据此修改NULL语义。

Long6Normal达到20真实出口；Long7Normal真实C1 Exiting在(1.5,3.546059)后续离场腿Blocked。cold原决定为前一request Arrived、下一腿clear、原完整路线不clear、replan NULL；事后完整路线与真实C5 Entering身体冲突。这不是上批C5初始exit NULL的同一完整故障链，尚不能认定admission的具体放行原因。

本批exit-decision consumer在natural iterator执行MatrixStart之前绑定旧customers；MatrixStart换掉场景及controller，导致新实例没有hook。两份captured=False记录均为 **UNKNOWN_NOT_ATTACHED_TO_ACTIVE_SCENE**，不能解释为目标未发生，原eligibility path→lease→exit输入变化仍UNKNOWN。此前静态review遗漏该生命周期问题；旧cold recorder在MatrixStart之后安装，其原决定记录仍保留有效。

3174 Assets运行前后零差异、四source及package一致，Editor／worker0。当前仅继续已批准CFC／Long两个文件：修正consumer绑定时机并加入active实例／回调计数；使用实际production query delegate及原有bounded ring节点构造单一side机制fixture；记录同次原admission判断的原输入和短路结果。均不增加导航查询、策略或预算，先source-only静态核对再决定一次限定验证。

另有两份额外测试文件准备流程补丁已静态review、尚未应用：CafeDecorationFlowTests.cs将出口故障注入放到真实取餐到位后；CafeFurnitureLayoutMatrixTests.cs在原600帧上限内等待同request实际位移后保留原断言。两文件超出当前批准范围，已单独请求Owner确认；答复前保持只读。整体integration仍FAIL，EditMode NOT_RUN，Main完整输入无响应仍UNKNOWN。

delta03源码冻结CFC `C5F7F6042674198F03F609336BD194A6F03B8F5B89666CC879FE100FA8B939DD`／Long `CCAF0E347298495FA00889EC6E2CDBAF830C38630D6D9AD0B67E0F0A2D890D4C`，同席增量spec／quality review PASS、C0／I0／M0。原Query及NULL语义尚未改变，natural body仍同bytes；source review不能代替编译／实际挂钩证据。

Root先GO一次最小A机制用例，未重复自然两例。`mechanism-red-02`实际 **compile FAIL，CLI6／native1，A NOT_RUN，无tests.xml**：新增Long640／702的GetInstanceID触发CS0619，当前Unity要求GetEntityId。任何fixture前提与最后Build断言均未执行，不是产品RED。root已读取原错误和数值回执；3174 Assets／source／package无变化，Editor／worker0。当前只修这两处test诊断ID API及必要类型，不改CFC策略，修后再核对小增量；旧失败资料保留。

delta04只改两处GetEntityId().ToString及诊断ID字段／参数string，root核对四处实际diff和本机API摘录。CFC仍C5F7，Long `934A553A22587380ECF0CF25582B06FA08BAFF5D938F2F9AD36811CF9FD6A0FF`。`mechanism-red-03`实际 **0 PASS／1 FAIL，1.6032946s，CLI8／native2**；此执行证明编译已过，但仍是 **FIXTURE_FAIL**：指定south raw direct已通过包络检查，后续detour前提及真实factory＋Build断言未到。XML SHA `9E81788E9152535E5F2D608079BBD282258893EC9BF7DC127C7046E728F20C3F`。运行中3174／source／package零差异、Editor／worker0。

Root停止A人工点位调整及NULL→empty策略，不把几何假设当成已成立的产品缺陷。这个新加A没有成为有效产品回归，后续正常suite前必须明确移除或转为适当diagnostic，旧source／失败资料保留。下一步限定为原Long7Normal＋Long6Normal一次，使用新active实例／原admission记录器与原cold hook；不添加查询或规则，先取得真实判断链。本批尚无结果，整体integration FAIL、Main输入UNKNOWN保留。

### 18.22 有效原链捕获：Long7 FAIL／Long6 PASS

`diagnostic-runtime-02/tests.xml`实际 **1 PASS／1 FAIL，64.0299718s，CLI8／native2**，root已独立读取原XML／数值回执。XML SHA `DB546F71914B4012B849D914C3D9C98C959621FD8CF18DD015718204F8B064A9`。Long7在初始离场规划Blocked，最终Completed1／actualExit0；Long6达到20真实出口及原完成／capacity断言。3174 Assets／四source／package无变化，Editor／worker0，8份新动态产物已逐bytes另存且历史460文件SHA未变。

Long7这次挂钩有效：callbacks＝activeCallbacks11698、current reference一致、mismatch=False、overflow0／errors0／rejectedDropped0、原hook恢复。完整捕获同一C1的frame810实际selected upper side(3.5,2.5) eligibility True→取得lease→frame1669相同side／actual start／Actor／PickupOwner／revision／exit的真实dispatch→原planner NULL及四个boundary候选拒绝→Blocked。不是segment WAIT失效，未发出首个exit request。

原eligibility expanded path绕柜台北边及左边，再经过(-2.5,.55)。到实际exit时，保存的原路线已被C5身体距0.571410154及C6身体／同位formal Counter head(-2.5,.5)距0.05阻挡，均小于原clearance0.911。此为原输入保存后的纯XZ几何比较，不是补查native或证明全局无替代路线。C1当时尚在MovingToPickupService，consumer只记录已collected Exiting期间的admission；因此这次没有C5／C6原admission链，不能判定admission具体放行错误。

Long6实际业务PASS，但诊断overflow5443，必须保留UNKNOWN_INCOMPLETE，不用它证明完整chronology。两例cold segment records0不否定Long7真实初始规划失败。

source核对发现具体一致性缺口：PickupPlanCanExit本地采用Counter head连续包络，最终PickupCollectionCanExit仍用raw-native exit query，可能接受依赖未来Counter head空着的选侧路线。下一步仅在原两文件范围准备真实前提的RED与复用同一包络的最小方案，并请同席reviewer核对因果／范围；尚未修改策略。没有新增future exit ownership、cache、全局障碍、planner预算或宽松WAIT，完整integration仍FAIL、Edit仍NOT_RUN、Main输入UNKNOWN。

同席独立因果核对支持该缺口及两文件内的最小收紧。不能倒称frame810当时的当前身体检查算错；修正的目的是让已批准的未来Counter-head不变量贯穿最终选侧。它不证明C5移动身体、所有自然卡死或admission问题均解决。

`selected-side-test-delta-01`只替换我们新增且前提不成立的A，原A source／失败资料保留；原natural bytes／27 assertions／seed保持。新case为 `PickupCollectionPreservesFutureCounterEnvelopeAndSafeSideCompletes`。`selected-side-red-01/tests.xml`实际 **0 PASS／1 FAIL，3.011062s，CLI8／native2**，XML SHA `ECAFEAFFE5852BC2741BBE8FE13D3751249F1D7A3CA7D816186731DDF94D02C1`。root已独立读取：真实Long7单顾客paid／Ready／PickupArrived且员工Idle，原bounded route完整、当前body／delivery／return guards全True，原expanded path却穿head包络；实际helper记录accepted=True，最后安全断言Expected True／Actual False。这是 **VALID_PRODUCT_RED**，不是fixture失败；后续正常收杯／真实出口正例尚未执行。

该RED源CFC C5F7／Long `5C33DB10A8A89E8AAE1593BF9A407DB3DEF36E083B55DF6A904EF77E70E35362`，3174 Assets／source／package parity、Editor／worker0。下一步GO只改CFC共享原head公式／factory到最终side exit query，并纯检查最终expanded path；测试源保持不变，先freeze增量再focused GREEN及直接正反回归。不会改变NULL语义或真实离场规划，不自动扩展其他文件。

### 18.23 最终选侧包络修正：旧11项PASS，新真实exit回执待验证

最小产品delta冻结CFC `2776D6318A31E1BC4BB2AA7FE36767D5D08BD140A0CDA458BBBB860D46C49CE2`、Long仍5C33。两处eligibility共用原有效head／同公式；final只在本地occupied加入head并复用原filtered query，最终ExpandRoute后再纯IsClear。原NULL语义／130-node和boundary／waypoint预算、实际exit raw规划／ownership／物理参数保持；root已读取实际diff及hash。

`selected-side-focused-01/tests.xml`实际 **11 PASS／1 FAIL，27.282741s，CLI8／native2**，XML SHA `093CF94AF8F4590E227F1446A1475A7AA081AA5E9076EF56DF017151B106EDD5`。原focused11项全部通过，包含真实physical出口阻塞及清除后恢复；新用例已证明原unsafe upper helper现在拒绝，readonly未发布、随后public安全path／真实lease／native request均通过，订单Completed且customer snapshot清空。它随后在新OnDestroy回执count Expected1／Actual0失败，pose／tokens等后续断言未到，不能据此报完整GREEN或真实exit回执PASS。

root核对：CFC RemoveVisit先visits.Remove，随后调用Destroy；测试observer只在OnDestroy产生回执，而新测试在snapshot空后立即断言并可能于finally清回调。这支持新test收尾时序错误。只批准该新测试的原bounded Until再等待exits==1，保留原6000上限及count／pose／token所有断言；不增加sleep或伪造回执，不改CFC。后续只重跑该新case，旧11的方法逐bytes保持，不原样重跑整12。当前真实出口收尾尚待验证，完整business matrix／整体integration／Main输入不能据此报通过。

`selected-side-test-delta-02`实际只在该Until条件增加exits==1，其余Long bytes保持；CFC仍2776，Long `D06140FC4D9E23358BA4EACAD2108F99E82F720524D16C1A6519E7DE02405CF8`。唯一覆盖新case的 `selected-side-focused-fix-01/tests.xml` 实际 **1 PASS／0 FAIL，3.9008478s，CLI0／native0**，XML SHA `40C47721BEAE52D44A17B69AC4E5172318CD84BD76451117F6BC09F8B557395D`。root已读取：unsafe upper被拒绝，public采用safe lower(3.5,.5)，实际exit回调1／pose(.5,0,-3.35)，原tokens Released、leases空、capacity0全部通过。3174／source／package parity，Editor／worker0。原11同产品源且方法未改的PASS保留，旧12批仍11P1F；不伪造一批12P。当前同席review该最小产品／测试增量及覆盖证据，尚未执行当前源的新business matrix或完整integration，不据此关闭Phase或Main输入问题。

### 18.24 当前修正源经营矩阵：10 PASS

同席增量spec／quality review PASS，C0／I0／M0后，`selected-side-matrix-01/tests.xml`实际 **10 PASS／0 FAIL／0 SKIP，381.083454s，CLI0／native0**。XML SHA `008B6326DED05317F54BDDF7BB4563F884E19F7C4A9893C1BFD889D22ABCBE05`，root已独立解析10个case。原Long6／Long7／Photo6／Photo7各Normal／Fast的8组均20真实出口、原订单／token／FIFO／capacity断言通过；两controlled各4真实出口及原完成／managed清理断言通过，但其目标crossing仍 **PRECONDITION_NOT_REPRODUCED**，不升级为原交叉因果验证。

该批cold records0／dropped0、firstblock0；原whole-frame数据64034帧、14帧>500ms、max759.505ms，包含Editor／render／IO，不能据此保证性能不变或移动设备FPS。CFC2776／LongD061及其他保护source／package／3174 Assets运行中无漂移，Editor／worker0。6个fixed历史sink已另存本次结果再恢复原SHA，26份动态及总32份raw归档。旧自然FAIL和完整关联220P9F2S仍保留其source范围，没有重写为本批结果。

下一步用现有opt-in UserPhotoCounterSixNativeInputProbe作一次普通可见Editor输入检查；root通过computer-use实际鼠标操作Pause／Resume、缩放及Decor／Done（不确认实际装修），sole implementer只启动／监控同一进程。无source修改／Pipeline安装／第二Editor，当前尚无该人工输入结果。Main历史完整输入无响应仍UNKNOWN，当前修正源的完整关联及Edit未重跑，额外两份测试准备补丁仍待Owner答复，P15不收口。

### 18.25 普通Editor实际输入：20真实出口及鼠标操作通过

`selected-side-native-input-01/tests.xml`实际 **1 PASS／0 FAIL，261.6761821s，native exit0**；XML SHA `E0EE132F99503293D9F3AEDC0010C17A79BDB5C6209067274DE0846C791CE5F3`，root已解析。运行现有NativeInputProbe、正常可见MainCafe；root用Sky实际鼠标验证Pause／Resume、镜头缩放及Decor／Done（无实际装修确认），均有可见响应、恢复后继续营业；原20真实出口／订单／token／capacity断言最终通过。临时FreeAspect已恢复原P5，Editor正常自行退出，未保存Scene或改source，3174／四hash／package parity、Editor／worker0。

这是有人工UI干预的独立probe，不混入无干预matrix；cold0／firstblock0，本次完整输入失效 **NOT_REPRODUCED**，历史输入故障根因仍UNKNOWN，不能声明已修好该历史故障。原始窗口截图及动作UTC在 `selected-side-native-input-01/manual-input-report.md`；03-operating-main.png是当前代码下完整布局的原生截图。原whole-frame统计包含Editor／render／截图／UI输入，不作为移动设备性能证明。

### 18.26 剩余旧用例：4 FAIL后，Fix1准备时序修正单例PASS

当前产品下 `selected-side-remaining-four-01/tests.xml`实际 **0 PASS／4 FAIL，40.5725427s，CLI8／native2**，XML SHA `5C6D8785CF0F8C29334EEF25A62AE1EAACE31ADF33B8228C2FE77663482EA9DC`。Fix1在三阶段candidate前提为NULL时失败；FormalRepair实际通过断开gate／门洞移动／4 exposed sides但未恢复合法waitslot；HeldReady实际public acquire False，原完整exit guard细节未观察；Cashier达到4次安全start但缺目标等待witness。两fixed sink已另存当前结果再恢复旧SHA，3174／source／package parity、Editor／worker0。不能一概称作测试错误或产品GREEN。

独立source分析确认两处测试准备缺陷：Cashier listener匹配生产代码已不再发出的旧字符串；Shared HeldReady自身controlled body(.35,-3.5)距真实exit(.5,-3.35)仅0.212132，小于0.911安全间距，因此旧“安全collection”前提未包含完整出口。它们仍不证明实际首个短路门或所有冲突窗口。另准备固定拓宽Counter通道的新FormalRepair recipe，native可行性尚未验证。

Fix1只在已批准Long范围调整真实准备顺序：C1真实paid／Ready／PickupArrived→public acquire一次并自然ServiceArrived（仍HoldCollection）→原C2正常付费／MovingPickup→原ResumeCollection及Exiting。保留原64点、三阶段strict proof、全部原断言、非exiting分支及natural bytes。CFC仍2776，Long `6886429C90BF5221D5366DE666B79C90DDC64605DE3CCAB08C200456617A7BB8`。唯一覆盖的 `fix1-fixture-runtime-01/tests.xml`实际 **1 PASS／0 FAIL，3.3000681s，CLI0／native0**，XML SHA `505198ADB143C06526831800FF30E19D496D431B1D35D2BD40A9276713E247D9`；root已独立读取同request9、三阶段形成、真实bounded replan1、replacement保留、后续2真实出口／2Completed及原tokens断言通过。可选observePending子分支未执行，不扩大覆盖。运行中3174／source／package parity、Editor／worker0，当前增量review，旧0P4F不改写为同批1P3F。

四份额外测试文件草稿目前全部未应用：Decoration故障注入时机、Matrix同request真实位移及固定拓宽repair通道、Base提前清除自身出口阻挡、Status更新listener。静态增量review C0／I0／M0；新recipe／其余完整前提仍需native验证，不能预报PASS。已将原两份未应用提案合并为四文件授权问题交Owner确认；答案前这些Assets保持只读。当前原完整关联9个失败中仍有5项未关闭，完整关联／Edit未重跑，P15不收口。

Fix1最后Long-only delta同席spec／quality review PASS，C0／I0／M0，见SDD review第224行。当前CFC2776／Long688642冻结，原natural／非exiting分支及其余测试方法未改。经营修正已经可在此worktree实际试用；全部关联gate仍须在额外测试准备范围获批后验证，不以本轮局部通过替代正式Phase验收。

### 18.27 Owner选择交付经营修复，延期额外测试修正

> 此处保留当时决定；Owner后续只追加批准原两份准备补丁，最新结果见18.28。

Owner明确选择“先交付经营修复，暂不改额外测试”。四文件合并提案及此前两份提案均不应用，不继续其dependent回归；剩余5项失败及完整关联／Edit缺口保留，不能改称整体GREEN或P15正式完成。

交付工作区 `E:/Unity/Project/AnimalCafe/.worktrees/phase-15-business-day`，branch `codex/phase-15-business-day`。本轮产品修正在CustomerFlowController.cs：最终选侧也继承队首连续包络，避免依赖暂时空着的收银队首作为出口；CafeLongCounterStallTests.cs补真实安全侧离店验证及合法fixture时序。当前CFC2776／Long688642与验证记录一致；交付核对确认四个额外测试文件仍保持原SHA、Editor进程0。未commit／push／merge。

可由Unity Hub打开此工作区，在MainCafe用原来的合法装修布局复测。关键经营矩阵及普通Editor实际鼠标记录见18.24–18.25；真实截屏为 `outputs/phase15/pickup-exit-eligibility/selected-side-native-input-01/screenshots/03-operating-main.png`。额外测试以后需新授权再处理，本次不继续等待或重复申请。

### 18.28 Owner追加批准原两份准备修正，定向2 PASS

Owner在此前先交付经营修复的决定后，明确回复原两文件问题“批准这2个测试文件的准备流程修正”。仅应用原Decoration补丁5CED6101…与原Matrix补丁7B3F3FD9…，各增加10行、没有删除或改写原语句／业务断言。Decoration真实paid／Ready／PickupWaiting并取得service到位后再注入出口故障；Matrix保留600帧上限，验证同一非零request／generation／native byId并等待实际位移>0.1。没有应用combined Matrix搬柜台、Base PickupSide或Status补丁，没有产品代码修改。

一次无诊断flags的对应PlayMode定向批次 `outputs/phase15/pickup-exit-eligibility/approved-two-prep-01/`：**2 PASS／0 FAIL／0 SKIP，6.2672538秒，CLI0／native0**。原XML SHA256 `3C29393DA6DFAB51B4E6F662C9F4CC51A6049BB575C09C566F4297D2887D5560`，完整selector、原日志、source delta与release见该目录[报告](../outputs/phase15/pickup-exit-eligibility/approved-two-prep-01/report.md)。UTC运行时间2026-10-29 01:46，对应本地2026-10-28。

- `BlockedCollectedExitKeepsOverCapacityTokensThenRepairAndNewEpochRejectOldSuccess`：PASS，3.509096秒；原占用／超容量／装修恢复／epoch与旧success拒绝断言保留且通过。原输出含cup appearance missing警告，本次不作为杯子外观验收。
- `ControlledPendingCollectedExitNextLegKeepsRouteAndBudgetDuringIndependentPickup`：PASS，2.621178秒；真实accepted request8移动0.1000003后满足原严格certificate，pending保留segment1及replan0，原两次真实出口／Completed／token／容量清零断言通过。

运行前后3174 Assets、8项绑定source与3项package／version输入一致，两个历史JSONL原SHA恢复；正常结束后Editor／worker0。当前Decoration SHA为6CF7CBBC…、Matrix CD22D67E…，CFC2776／Long688642及Base／Status原SHA保持。原79／101条Assert-containing lines顺序保留，逆补丁可逐bytes恢复整份原文件。

这两项定向PASS将待关闭旧失败从5项减少到3项：FormalRepair、HeldReady、Cashier提示／恢复用例仍未关闭。完整关联最近一次仍为原source的220P9F2S；没有合成新的全量PASS数量，当前源完整关联及后续Edit为NOT_RUN。历史整个Game输入失效根因仍UNKNOWN；未做Git／CI／merge，不宣告Task7或P15正式完成。

独立增量review完成：Spec／quality PASS，C0／I0／M0，见 `.superpowers/sdd/2026-10-26-phase-15-business-day/extra-fixture-draft-review.md` 的 narrow2 应用后审查。Root读取原XML、数值退出回执、当前source SHA并确认范围；本次两文件授权已完成。余下三项及完整gate保持未关闭，不继续追加运行。

### 18.29 继续完成P15：剩余三项修正与最终回归范围

Owner要求“继续直到P15完成”，授权继续此前延期的三份准备修正及P15既有收尾。产品代码未再变化：CFC2776／Long688642／Decoration6CF7保持冻结。仅Matrix通过正式Decor将同一收银台支撑向左移一格，Base先移除controlled blockers并补全真实未来出口安全资格，Status改为当前reason并在首次完整冲突证据后锁定该目标观察；原业务断言、identity、容差和3000帧边界保留。

首批 `remaining-three-prep-01` 实际1P2F7.8373985秒、CLI8／native2：FormalRepair PASS，另外两项FAIL。HeldReady单次原决策诊断 `held-ready-diagnostic-01` 为0P1F，记录route3／deliveryTrue／returnFalse，证明旧准备只检查接近路线，没有满足出口路线与员工返岗的完整安全前提。该诊断及临时hook源码保留为历史资料；最终default测试已移除hook。

最终 `remaining-two-focused-01` 实际 **2P0F0S4.2798239秒、CLI0／native0**。HeldReady成功在frame499、员工仍Returning且距CM1.10502481m时取得实际request14；原early-issued／actual-return／Completed／capacity／token断言通过。Status保留真实C2/C1request7冲突、未付款／未发布预留、全程unsafeStartFalse及同一waiting-head恢复断言。不得将分批结果合成同一次3P全量运行。

三项最终增量独立审查 **Spec PASS／quality PASS，C0／I0／M0**，见 `.superpowers/sdd/2026-10-26-phase-15-business-day/remaining-fixture-applied-review.md`。当前Base C6DE9EC4…／Status2666B578…／Matrix2B01B8B7…；运行前后3174Assets／8source／3inputs一致，历史输出恢复，Editor／worker0。原审查seat本次capacity error未产出结论，替换reviewer只完成同一窄审查，没有重复whole-phase review。

**最终回归范围：** 按plan208／218／277及原导航修复brief，相关Edit使用Phase14、Phase15两个namespace和NavigationServiceTests／NavigationCollisionTests／NavigationResumeBlockTests／NavigationResumeLifecycleTests四class；完整PlayMode覆盖共享导航、CFC、员工与UI对P11–P15等旧功能的影响。无关P6–P8 asset-builder Edit源码未变，复用其既有完整Edit2832 PASS的原源边界，不声称当前六filter是全项目Edit。最终批次位于 `outputs/phase15/current-source-final-regression-01/`，实际XML与gate待完成后更新。

原批准M01–M07 Agent代测保留，不为测试准备delta重复整套；原8经营组合各20真实出口、controlled2及普通Editor实际鼠标／20出口的源边界仍有效。8h／device单列NOT_RUN，不新增为P15本地技术阻挡；Owner亲自观感与最终Go、CI／Git／merge／merged-main仍是独立交付门槛。历史Game输入失效根因UNKNOWN／本次未复现，不冒称已修复该历史输入故障。

### 18.30 最终当前源自动回归通过（2026-10-28本地）

当前P15开发及本地技术验收已完成，Engineering／QA独立最终复核均PASS，无Critical／Important／新增Minor。现源相关Edit六filter实际 **278 PASS／0 FAIL／0 SKIP，12.6343693秒**；完整未过滤PlayMode实际 **1505总数：1498 PASS／0 FAIL／7 SKIP，1968.9434184秒**。两mode CLI0，独立native日志marker均0；均只启动一次，没有因agent capacity error重跑native。原220P9F2S那次失败的九个精确fullname在本次完整Play中各出现一次且全部PASS；旧失败资料不改写。

P15现源Play **70／70 PASS**，P15相关Edit **59／59 PASS**。完整Play同时覆盖P13 44P、P12Integration81P及P12 2P、P11 37P及P11Integration38P1opt-inSKIP、P14 160P2opt-inSKIP。7项SKIP均保留显式开启条件：原五项截图／几何审计，以及本地quantized snapshot诊断和正常Editor输入probe；不是七项业务失败。后者已有前次正常Editor实际鼠标／20真实出口PASS，不把本次SKIP算作新的手测通过。

收尾恢复Edit两份已知Phase14测试Scene、Play一份测试材质及13份历史输出，实际生成bytes均已归档。最终3174Assets、8绑定source、3package/version输入、14Scene与运行前一致，unknown／missing／extra0，Editor／worker0；OS native handle闭合不作承诺。详见 `outputs/phase15/current-source-final-regression-01/report.md`、两mode原XML／log／release。完整旧Edit2832P仅复用未改变的asset-builder范围，本次278相关Edit不是全项目Edit。

本次最后三份修改均为测试准备：同一正式Decor修复通道、真实完整安全的提前取餐窗口、首次严格冲突观察。产品规则未再放宽，原业务断言保留；独立source review Spec／quality PASS、C0／I0／M0。`.gitignore`只增加 `/outputs/phase15/`，保留本地证据而不纳入提交，源码／tests／Docs仍正常可提交。

**正式Phase状态分离：** Engineering／QA均判定本地技术验收PASS；不等于Owner个人观感接受、CI／commit／push／merge／merged-main已完成。原批准M01–M07 Agent记录保留源边界；历史整个Game输入失效仍UNKNOWN／本次未复现。8h／Player／mobile设备与挂起恢复仍单列NOT_RUN，不新增为P15本地阻挡。Owner在GitHub Desktop自行交付；未自动触发账号登录CI或进入P16。后续具体步骤见Beginner Guide第6节。

最终收口：Engineering与QA已独立读取原XML、当前source绑定及恢复bytes，两席均Spec／quality／local technical PASS，C0／I0／新增Minor0。见SDD `remaining-fixture-applied-review.md` 最终receipt节及 `final-qa-receipt-addendum.md` 95–123行。**P15开发及本地技术验收完成**；branch `codex/phase-15-business-day`及worktree保留，文件未commit／push。正式Owner观感／Go、CI／merge／merged-main仍须实际执行，Roadmap不虚标正式Completed，不进入P16。历史Game输入首因UNKNOWN保持原状。本次无需再重复运行已通过且源码未变的suite。

## 19. 2026-10-29 Owner委托Computer Use验收：发现经营阻塞，未修复

Owner要求用computer-use代测前述8项，“有问题先不用修”。本轮实际Main／Validation点击、滚轮、游戏Pause／1x／2x、上下文验证工具及真实Decor取消／确认均由@oai/sky完成，没有eval／改代码／新自动suite。前5项功能符合预期；第6项仅取消／中断记录通过，装修后经营有问题，不能整体算PASS；受控堵路实际恢复并排空，但Blocked原因显示Recovered:None；自然持续经营项FAIL：Day4Closing真实确认新增普通Counter1x1后，Day5→Day6自然运行，Day6Closing出现“顾客路径受阻（离场：移动超时）”，随后BaristaBlocked／RecoveryFailed。游戏Pause／镜头／Editor面板仍响应，区别于历史整个Game无响应，不能归为同一首因。

实际持续观察段4m10s，约3m22s首次截图包含离场timeout，未达到健康连续运行5–10分钟；最终epoch2／Completed30／Total4／Counter0／Pickup3，原订单31Preparing／32及33Waiting。此前Day3人工delivery blocker已移除并恢复、Completed14／Total0／Idle／Rest，后续无重新注入。Root cause UNKNOWN；新柜台布局及紧邻首次timeout的滚轮操作仅作事件顺序，不作因果判断。不是原截图长柜台逐格重建。

Main浅色正式HUD无黑色诊断，540x960可读；Main960x540日期／阶段状态卡字号显著偏小，记录观感问题。Console最终7info／0warning／0error，不能以无红色error覆盖业务阻塞。3174Assets前后零差异，无代码／Scene／prefab／meta修复或保存。仅写证据／结果文档。Editor有意保留Play＋游戏Pause的实际失败现场，未恢复此自然故障，未Stop。

完整8项表、复现操作顺序和54份原始截图见[本轮手测报告](../outputs/phase15/owner-delegated-ui-20261029-01/report.md)，关键故障图为该目录44／45／46／52／53。**此前18.30自动回归PASS保留原源与模式；新增manual验收FAIL，当前不能据它正式关闭P15或宣称经营问题全部消除。** Owner明确要求本轮不修，未启动后续修复／Git／CI／P16。

## 20. 2026-10-29 manual问题修复：局部确认，整体未通过

Owner后续“查明然后修复吧”替代第19节只测不修的限制。已读取真实现场再退出Play，之后native复现确认：员工后发路线遗漏真实已取餐离场顾客；既有reservation的路径缓存失效后，其真实站位也被错误从障碍集合漏掉。当前产品改动限CustomerFlowController、EmployeeTaskRunner、PickupQueueService；不改Scene、NavMesh底层、碰撞、付款、容量或营业时间。

Runner/checkpoint Edit71PASS属Runner未再改变的源；员工出口两项与canonical reservation等当前五项合计5PASS/0FAIL。完整相关280项最近为277PASS/1FAIL/2SKIP，失败Photo7Normal；后续当前canonical补丁focused3PASS/1FAIL。分别运行出现PASS不能覆盖该真实失败。same-direction新probe因资格窗口matches=0而INCONCLUSIVE，已归档退休，没有据此改body-WAIT产品逻辑。原业务断言均保留。

当前3176Assets，较final-related基线4项预期source/test变化，unexpected/missing0，Scene/资源无差异；Editor/workers正常退出后0，OS handles UNKNOWN。第6–8项新Computer Use尚未重测，P15 IN_PROGRESS，不能正式关闭。下一步需要讨论canonical目标的有界路径保持门禁，Owner确认待回；详细范围、风险、总查询预算、native先证反例要求见本轮reservation-rule-proposal.md，未实施。

本轮记录：outputs/phase15/manual-exit-repair-20261029-01/report.md。无Git、CI、merge、安装或P16动作。

## 21. 新站位路径保持与实际离店选路（2026-10-29／30，进行中）

Owner明确批准“同意，先证明反例再实施”。新增停车目标只可在不封住较早真实预留目标通路时提交；安全证明预算不足记UNKNOWN，先暂缓分配。真实到位／取消退休移动承诺，身体、reservation、lease与容量继续按原生命周期处理。该增量仅修改CFC与对应测试，不改Scene、碰撞尺寸、NavMesh底层、付款、容量上限或营业时间。

真实几何证据1PASS、canonical reservation反例RED→GREEN、生命周期4PASS、原八组合等13PASS分别保留于 `outputs/phase15/reservation-rule-20261029-01/`。新gate共享每帧4000次Native查询预算，单候选仅缓存拒绝；无证明的旧目标不获永久优先。服务侧资格仍用原检查，不预占未选择的未来出口。

随后相关Edit387PASS；相关Play实际286=283PASS/1FAIL/2SKIP，1348.6978992s，native2/CLI8，Twenty2x失败。一次有效pair诊断1PASS/1FAIL证实：员工req16已有真实送杯＋返程选择，后发顾客req17的实际离店路线与之相交，最终真实身体互锁。资格阶段的假设路线与实际Send选路并不保证相同。formatter失败及错误fixture单列保留，不作产品RED。

实际离店新candidate与真实replan新增同等员工检查；只有当前有效任务及native运动、完整剩余往返对所有真实身体／目标／lease均独立清空时才可暂等，绑定attempt／actor／generation／revision／Queue。取消或失效不沿用证明；清掉未发candidate以免倒置阻挡员工下一腿。初始正例及原1x／2x20真实单3PASS，真实员工Cancel保留body与future静态body反例等2PASS。Review发现两个延后首腿入口后，两个controlled seam先RED0PASS/2FAIL，再于Core仅对Exiting／Segments0／Request0终验；GREEN4PASS/0FAIL/9.9440527s，保留原预算、request、lease、token及真实离店断言。中途新replan独立检查，旧suffix不新增让权。

当前冻结CFC SHA256 `3E468F1584D2C1C2BE5C137D43A94BC71BD0A47FE3D739D71C4C948ED1DA686B`。独立review Critical0／Important0／newMinor0。`related-final/edit`实际387PASS/0FAIL/0SKIP，12.0286263s，native/CLI0；恢复3项已知资源后3176Assets／12source／3inputs／14Scenes零差异，未知／missing0，EditorWorkers0。当前同源相关Play执行中，Computer Use第6–8项尚待复测。历史FAIL不由focused PASS覆盖；整个Game输入首因UNKNOWN，真实8h／device／Owner／CI／Git门槛仍分列，P15不正式关闭。
