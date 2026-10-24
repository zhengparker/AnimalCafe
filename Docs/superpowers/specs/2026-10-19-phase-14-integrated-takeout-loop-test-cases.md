# Phase 14 — Integrated Takeout Cafe Loop Test Cases

> 设计日期：2026-10-19，自动证据更新：2026-10-23 UTC。Approved Test Design 已按review修订并获Owner批准；自动实现、实际method/layer/evidence见第8节，设计批准本身不等于执行或通过。
> [Design](2026-10-19-phase-14-integrated-takeout-loop-design.md) 是规则来源。第2–5节定义验收场景，第8节记录实现映射与作者运行证据；Owner manual 验收另记。
> 最新状态：上一轮P-005／009送杯拥堵补修256／164已通过，Owner旧三项visual PASS保持。当前新批准自然单列、P14收银1秒及每代表布局真实2／4／6人验证，执行结果另记`single-file/`；旧源PASS不冒充新源结果，P13独立默认2秒与Barista3秒保持。
> 原Task9快照的作者自动验证见第8节：完整Play1319P/0F/5S（当时P14全部51P/0S）、CLI0，08:53:38Z–09:11:56Z；5项均旧opt-in capture/audit。该runtime受影响Edit为P11全部/P14全部/P12直接254P/0F/0S；完整Edit2723更早于disabled Customer guard及Nav修正。前四完整Play FAIL历史保留。此为runtime08:35:02Z/tests08:48:59Z的历史源码证据；之后补修均另列，不冒称当前同源完整回归。Owner手测状态见Guide，Player/device/CI仍NOT_RUN，技术交接不等于Owner视觉验收。

共 **40 项 EditMode、24 项 PlayMode、22 项 manual** 需求场景。场景 ID 不等同最终 NUnit case 数，参数化及 fault injection 可产生多个 tests。实现阶段补 coverage 映射和实际执行结果，不把设计检查算作 runtime PASS。

2026-10-20 Owner在M-007反馈后批准补充：成功点单后立即请求前往Pick-up（不等Ready），领取后持杯离店并复用既有Carry动画。下列相关场景已更新；旧自动/Agent PASS只代表旧快照，新持杯、跨Done保留及warning修正须另列执行证据。M-007 Owner为“待复测（已反馈）”，其余Owner项尚未验收。

Owner随后批准Pick-up多侧交互：每个外露且可达侧均可用于交付/领取，同侧互斥、不同侧可并行。下列E-015/P-005及M-005/010补充动态备用侧和单侧等待。2026-10-21最终同源直接Edit289P/0F/0S、Play130个不同case全部PASS（主批129＋自然Continued1），窄review CLOSED；普通Editor已确认同原delivery lease仍占时客人从第三侧实际领取、Pause与正常离店，Agent／Owner状态见Guide。旧持杯Edit166/Play71或分离fixture时序1P不冒充新增多侧证据；历史失败、清理及结果边界见Validation Report。

Shared首客CR旁停顿补修（2026-10-22 UTC）：新增P-004实际转角case先RED0P/1F，原request需retry1且Barista已Carrying；private有限候选修复后，最终直接Edit334P/0F/0S、Play168P/0F/0S＋原自然Continued1P/0F/0S，合计169唯一Play全部PASS，独立窄review CLOSED。最终Shared自动case付款后.833252秒真实释放CR／原request无retry／Barista仍Working；普通Editor1080×1920自然1×实测.832131704秒、原客后续Completed正常离场。随后Owner明确委托四组功能、仅本人看visual；Agent另跑五个ordinary Editor组八项功能记录PASS，未重跑上述NUnit。历史Owner FAIL保留且功能已核对，视觉待Owner。完整项目suite／Player／设备／CI未在本次代测执行。当前源码hash、证据分层、工具中止及清理见Validation Report；与上述多侧289/130分列。

最新长柜Facing补修：P-004新增两项真实Nav站稳／领取后前移／90°service geometry case先RED0P2F，再CF focused11P0F；最终直接Edit252／Play132＋原自然Continued1，共133唯一Play全部PASS，独立窄review CLOSED（不含自然项／native）。普通Editor近似长柜两位真实Paid站稳后后客面前位dot=1，正常首客取杯离位后原第二真实前移为head、转向服务dot=.996758。七个slot坐标修前后相同，验证Facing而非强制直线；90°受控case不冒称整Decor Grid旋转。证据、源码hash、原失败与前后图见Validation Report／Guide；Owner仅看队形visual，本轮不是完整项目suite。

## 1. 测试分层和公共断言

> 最新执行（2026-10-23）：P14收银1秒、制作3秒、来客1～5秒，旧P13仍2秒。最终`single-file/final3b-edit.xml`267P0F0S、`final3b-play.xml`171P0F0S；19个原matrix/rapid变体及2个另列的controlled恢复case通过。下方旧2秒XML／milestone属于历史源，不沿用上一轮256／164为新源PASS。

本轮追加验收：原合法家具结构变体各目标2／4／6名真实Paid、PickupWaiting且Arrived、request0的客人；world-space实际pose遵循安全直线prefix优先，几何／完整路径／往返通道拒绝后才转弯，不以Facing／屏幕左右位置代替队形。每variant记录`requested/actual/physicalSlots/status`及转弯原因；有真实容量限制时另验无预留不收费，不将人数不足冒充目标人数PASS。已付费原tokens、FIFO和间距保持；Pause、让位及领取途中与稳定等候分开。恢复领取须真实前移并全链排空。L14／16保留无解拒绝和修复分支，其余具体几何沿L01～16，不新建第二矩阵。

计时追加：当前P14 runner的.99+.01边界、同一Cashier在Pause／2×／disable恢复保留剩余值及exactly-once，旧P13默认2秒和Barista3秒直接回归均通过；普通/加速各真实20单及原自然来客长项已含最终171中。原失败与精确覆盖见Validation Report。

<a id="single-file-cohort-results"></a>

### 本轮实际多人结果（最终final3b）

沿用下文L-01～L-16的正式Decor配方，`variant`为case旋转参数。这里的`physicalSlots`是本次规划在真实几何／完整路线／往返通道与原预算下验证并选中的安全链长度，不是全房间可放人的数学最大值。每例依次请求2／4／6人，下面填写实际站稳Paid人数；低于请求数均记`CAPACITY_LIMITED`，不冒称该人数达标。

| 原布局／variant | physicalSlots | requested 2／4／6 → actual | 最终case |
| --- | ---: | --- | --- |
| L-01／0 | 16 | 2／4／6 | `MatrixOpenFourSidesCompleteRealRoundTrip` PASS |
| L-02／0 | 1 | 1／1／1，容量限制 | `MatrixThreeSidesHaveRealDeliveryAndReturn(0)` PASS |
| L-02／1 | 16 | 2／4／6 | 同名(1) PASS |
| L-02／2 | 1 | 1／1／1，容量限制 | 同名(2) PASS |
| L-02／3 | 16 | 2／4／6 | 同名(3) PASS |
| L-03／0 | 4 | 2／4／4，6人受限 | `MatrixMiddleSlotRotationsHaveRealDeliveryAndReturn(0)` PASS |
| L-03／1 | 16 | 2／4／6 | 同名(1) PASS |
| L-03／2 | 4 | 2／4／4，6人受限 | 同名(2) PASS |
| L-03／3 | 16 | 2／4／6 | 同名(3) PASS |
| L-04／0 | 1 | 1／1／1，容量限制 | `MatrixAdjacentCornerSidesCompleteRealRoundTrip` PASS |
| L-05／0 | 1 | 1／1／1，容量限制 | `MatrixSingleSideOrientationsHaveRealDeliveryAndReturn(0)` PASS |
| L-05／1 | 3 | 2／3／3，4／6人受限 | 同名(1) PASS |
| L-05／2 | 4 | 2／4／4，6人受限 | 同名(2) PASS |
| L-05／3 | 1 | 1／1／1，容量限制 | 同名(3) PASS |
| L-07／0 | 1 | 1／1／1，容量限制 | `MatrixCounterCornerRequiresActualDetourAndReturn` PASS |
| L-08＋L-09／0 | 9 | 2／4／6 | `MatrixEntranceRejectsFurnitureAndFloorEdgeKeepsOwnedWaitsInside` PASS |
| L-16修复后／1 | 1 | 1／1／1，容量限制；修复前另验无解不收费 | `MatrixExposedButDisconnectedSidesBlockWithoutChargeThenFormalRepairServes` PASS |
| L-03 rapid／0 | 4 | 2／4／4，6人受限 | `MatrixRapidArrivalsRetainFiniteTrafficAndDrain(0)` PASS |
| L-05 rapid／1 | 3 | 2／3／3，4／6人受限 | 同名(1) PASS |

共57条人数收据：请求2／4／6分别有12／10／6例达到人数，7／9／13例明确容量不足。13个短队case有额外真实客人未付款的门禁证据；19例均真实领取／出口并释放原tokens，12个有后客的case验证实际前移，7个单slot case没有后客可前移。稳定pose与slot最大偏差0.075001以内，多人最小实测间距约1.099965；44条转弯匹配最终选中prefix的真实gate拒绝。间距与方向使用world空间，转角、领取／让位中的临时错位及相机投影可能看似并排，不能以屏幕X相等判FAIL。

原19以外的两个controlled case为`ControlledPendingCollectedExitRecoveryUsesIntegratedBoundaryPlan`、`ControlledPendingCollectedExitNextLegKeepsRouteAndBudgetDuringIndependentPickup`。它们使用真实Paid／canonical领取／Nav来稳定隔离恢复分支，分别验证同一P14出口规划和仅相关独立交通的等待／预算保持；不计为新增自然家具配方。全部case在`CafePickupSideTests.cs`，原始XML、实际57行TSV及root审计见`outputs/phase14/single-file/`。

自然经营另按原随机1～5秒与真实速度跑：Normal20单、Fast20单均完成并实际离店，原三milestone各一次，容量／杯／句柄／请求归零。最终稳定Paid PickupWaiting峰值为Normal2／Fast1；CR相关峰8包含Entering／Queued／Advancing。多人Hold验证证明安全容量与前移，不承诺正常经营始终积累6人，不把不同随机批次当吞吐对照实验。普通Editor多人截图与仅Owner新visual步骤见Beginner Guide；旧三项visual PASS保留。

- EditMode：使用受控 ports/clock/callback 验证业务状态、ownership、幂等、checkpoint 和清场；不替代真实人物移动。
- PlayMode：验证真实 NavigationWorld、MainCafe wiring、身体碰撞、UI snapshots 和生命周期；至少一项使用真实到店/收银/制作/取餐完成 20 单，不直接把 Order 置为 Completed。
- Manual：Owner 在 Unity Play Mode 观察动作、队伍、杯子、UI 和 Console。工具代跑须单独标执行者，不能替代 Owner 视觉验收。
- 全部场景共用断言：无重复付款/Order/delivery/collection；无错误 owner；无瞬移/穿人/穿家具；无提前释放仍占用的 token；无无界 retry。
- 回归基线为 `8c1582f7388191ff155d63b824b69c10d47cff3c`；P13 历史 counts 不当作 P14 执行证据。

## 2. EditMode 需求场景

以下为需求定义，实际执行映射见第8节。

| ID | 输入 / 场景 | 必须断言的结果 |
| --- | --- | --- |
| E-001 | 有效布局初始化、反复 Tick | 一套协调层、两固定员工、唯一 Visit/session；不因 UI 读取触发业务 |
| E-002 | 未抵达 Counter 队首、后客先到或重复 Arrived | 只有已到达队首可收银；无跳队或收费 |
| E-003 | P14 Cashier 1 游戏秒，分段/边界/大 delta；旧P13默认2秒 | 到期前无付款；到期只付款并 Create 一次，无余帧补发第二单 |
| E-004 | Create 失败后恢复、重复服务结果 | 不留下 Paid 无 Order 的半提交；同 Visit 最多一单，无重复收费 |
| E-005 | 缺设备/缺完整路线/无安全 Pick-up slot；设备只是忙 | 前三者不收费并说明原因；忙但可服务仍能排入 Waiting |
| E-006 | 一 Visit 重复付款事件；两 Visit 正常点单 | 每 Visit 一单一杯；通用 P9 API 没被改为每 customerId 只能一单 |
| E-007 | 多张 Order 等待、Barista 尚未回 CM、Claim成功但task start拒绝 | 按 ClaimNext FIFO；回到工作位前不领下一单；派发拒绝保留同一Claimed Order待恢复，不重新Claim/丢队首 |
| E-008 | 制作 3 游戏秒；制作完成但途中受阻 | 到期只形成一次已制作事实；送达前保持 Preparing，不能提前 Ready |
| E-009 | 实际交付、重复/失败/重入放杯回执 | Ready 与 delivery 一致提交；失败不留幽灵杯，重复回执无副作用 |
| E-010 | 错顾客、非队首、未 Ready、未实际到位尝试领取 | 全部拒绝；Order、delivery 和容量不变 |
| E-011 | 正确队首取餐、重复 Complete/Arrived | 一次移除正确 delivery 并 Completed，重复无副作用；不提前释放 Total |
| E-012 | 点单顺序与到站/callback顺序不同；队首离位后余客前移 | Pick-up FIFO按成功点单顺序；余客以新generation前移且实际到达才提交；新尾客不能抢前位，不按Scene列表或callback抢先排序 |
| E-013 | 第1/2/3/4/10杯交付与取走中间可见杯 | 可见1/2/3/3/3；实际delivery不丢；补显示最早未显示Order；无隐藏对象积压 |
| E-014 | Cup prefab创建失败、补显示失败、错误Slot | 真实delivery不重复/不丢；可诊断视觉错误，不能错误绑定或重做Coffee |
| E-015 | Pick-up 1–4外露边、桌拼接/墙/旋转/多Slot、按侧交付/领取、站位不足 | 候选仅合法外露方向，旧role-pair兼容；不同侧可同时持有、同侧拒绝，重复申请稳定目标，错误owner/旧侧不能释放或提交；实际离位才释放；FIFO及物理wait容量保持，无合法位时明确拒绝、不建隐藏队伍 |
| E-016 | admission batch部分失败、付款后尚未离Counter | 入店前失败完整rollback；成功付款不提前Release Counter |
| E-017 | 实际Counter离位→Pick-up到达→取餐→离位→出口 | 各token对应实际事件；取餐Completed后Pick-up/Total仍按离位/出口释放 |
| E-018 | 重复离位/出口、wrong owner/token、旧epoch回执 | 只释放自己的token一次；旧操作不影响新顾客或保留离场者 |
| E-019 | Total/Counter/Pick-up分别满额与恢复 | 任一满额停止接纳；恢复按随机间隔，不批量补发，不抹掉旧reservation |
| E-020 | Floor缩容，含已取餐离场者和新epoch顾客 | 保留旧占用可暂超限，拒绝新接纳直到恢复；不Reset Capacity消超额 |
| E-021 | Pause/零delta及暂停期间收到成功callback | 无新收费/制作/交付/取餐/生成；合法事件恢复后仅消费一次 |
| E-022 | 1x/2x及非法delta | 只使用已缩放游戏时间；P14收银1／制作3游戏秒准确，无双重缩放；负/NaN/Infinity被防御 |
| E-023 | 短暂动态阻挡与Navigation有界恢复 | 不立即Fail Order，不丢checkpoint；重试有界，不无限重复派单 |
| E-024 | 收银/制作中失败，含task cleanup清空timer | 原进度保留，恢复剩余时长；不重收款、不从头制作、不复活旧handle |
| E-025 | 已制作送达前失败，手杯被P13 cleanup移除 | 恢复只重建必要视觉并续送；不再次计3秒、不重复delivery |
| E-026 | 已Ready后Barista返回失败/取消 | 原delivery/Order不回退；顾客仍能领取，恢复只负责回位 |
| E-027 | 顾客等待/取餐/离场失败与重复手动恢复 | 队首和容量保留，实际位置重寻路；无自动超时、越队或无限retry |
| E-028 | Decor只查看、Preview后Cancel、Done | 原Order/visit/checkpoint/杯子保持，无epoch切换或清场 |
| E-029 | 有正式修改的Done，混合未取餐与已取餐顾客 | 清空全部Order、未取餐顾客、桌杯与员工手杯；保留已取餐者Actor/ID/身体/剩余token及顾客手杯 |
| E-030 | 离场顾客Blocked/disable-held，连续两轮装修与出口重复callback | 用已取餐事实识别保留者，不只看Exiting；独立于空Order store离场，仅释放自身一次 |
| E-031 | 清场中同步callback/旧generation/复用数字OrderID | 先退休epoch/handle；旧回调不重建顾客、Order、杯子或污染新任务 |
| E-032 | Done布局有效/无效/主动Pause、新session读UI | 有效自动接待，无效阻止，主动Pause保持；新session干净，UI查询无写副作用 |
| E-033 | Done PrepareExit前同步旧callback、Pause恢复监听重入，清场失败 | P14先持block并退休旧epoch；version发布前正常业务/新移动不能推进；失败不释放block，重复通知只清一次 |
| E-034 | Complete拒绝、同帧领取与清场/disable重入 | 提交不调用外部port；拒绝时Ready/delivery/已取餐/容量全保持；已提交领取后清场识别为保留者 |
| E-035 | Ready提交成功后首杯视觉失败，Complete后视觉移除失败 | 真实delivery/Completed不回退，不重复制作/取餐；重绘投影清理遗留对象，UI可诊断 |
| E-036 | P14接线时Customer/Employee Update和旧验证按钮同时触发 | 唯一驱动每帧推进一次；旧手动写入口被拒绝；未接P14旧场景按原模式工作 |
| E-037 | 两服务请求争用最后一个真实Pick-up slot、收费提交失败 | 同一时刻仅一个Visit持物理分配；失败不会占别人的slot，也不重复收费 |
| E-038 | task先disable→cleanup→恢复，含收银完成尚未Create | cleanup前checkpoint已保存；已到期收银只重试Create，不重新计时；未到期工作只恢复剩余时间 |
| E-039 | Hold Collection且共享anchor连续5次delivery | 队首留在计入容量的合法wait位，不能抢共享service lease堵住员工；关闭hold后按FIFO领取 |
| E-040 | 普通关闭Decor/无修改、异常shutdown、完整session Dispose | 只有正常含修改Done执行选择性清场；session结束清全部owned资源，旧回调不影响下次Play |

## 3. PlayMode / Integration 需求场景

以下为需求定义，实际执行映射见第8节。

| ID | 真实场景 | 必须观察 / 自动断言 |
| --- | --- | --- |
| P-001 | MainCafe空店，经Decor布置CR/CM/Pick-up并Done | 自动接线且幂等；仅两员工/一套UI，无需开始接待或P13受控任务按钮 |
| P-002 | 有效布局连续20个真实自动Orders | 20个不同Visit依次完成收银/制作/实际送达/正确取餐；逐单ownership正确；等待本批顾客离店后检查容量无leak |
| P-003 | Counter队伍和收银并行，新客与前移并行 | FIFO、1–5游戏秒到店、实际身体/入口门禁保持；不因单人移动锁死全店 |
| P-004 | 多顾客Pick-up单列与前移；默认Shared首客真实转角；合法折行朝向 | 成功收银/建单后同次请求去已预留Pick-up等候位，不等Ready；Shared空通道首客同一原request无retry且在Barista仍Working时真实释放Counter、最终实际到wait，不只验证>.1m起步；后客先到队尾再前移，Counter实际离位才腾出名额。折行wait链的真实站稳后客朝前slot、队首朝取餐方向；实际前移后新队首和后客重算朝向，90°Pickup geometry同样成立 |
| P-005 | Barista仍停交付侧且商品Ready；首选侧受阻还有第三侧；旧首选侧孤岛而备用侧完整可达；静态单侧或两侧中一侧被身体占用 | 队首从实际可达空侧到位领取，Barista不必先走开/回CM；备用侧贯通readiness、wait预留与真实交付/领取，不被旧首选侧错误否决；员工按所选侧校验到达，选路不冲突于顾客实际remaining route；只剩一侧时Preparing顾客不堵唯一交付侧，领取等实际让位；无重叠/瞬移/越队、无死锁，旧目标回执不能提交新目标 |
| P-006 | 暂停自动领取后真实完成5次送达，再恢复 | delivery=5且可见最多3；取走补显示，顺序/ownership正确，无隐藏Cup GameObject |
| P-007 | 员工及Shiba/Westie顾客的真实动画 | 员工2/3游戏秒、Carry+Walk、到点放杯后空手回CM；顾客领取前无杯，成功领取后恰一个杯挂手部，Carry上身+实际位移Walk腿部，停下Hold；不新增领取/饮用耗时或延迟Completed |
| P-008 | 各阶段Pause、1x、2x | 时间/位置/动画同步，无零游戏时间业务动作，恢复无重复；UI状态一致 |
| P-009 | 实际动态阻挡、路径拒绝与修复恢复 | 有界等待→明确Blocked；实际位置继续，不重复制作/收费或瞬移 |
| P-010 | Ready后员工返回受阻，顾客先取餐 | Completed保持，桌杯正确移除；回位恢复不重新送杯 |
| P-011 | 逻辑各容量满、物理Pick-up站位不足、恢复 | 停止新接纳，说明具体原因；无越界站位或批量补客 |
| P-012 | Decor仅查看/Cancel Preview后Done | 原顾客/Order/checkpoint/杯子保留，原流程续行 |
| P-013 | 未取餐顾客+员工手杯+桌杯+已取餐持杯离场者时改布局Done | 清除前三者与全部Order，保留后者原Actor/手杯/Total及原两员工；旧callback不复活业务，实际出口才清理该顾客杯 |
| P-014 | 保留离场者的新路线失效，再修复 | 身体不删除；Total保持；新布局实际位置寻路后出口释放一次 |
| P-015 | 有保留离场者时连续装修、Floor缩容、接新客 | 同Capacity service保留用量，新旧epoch不串；不会重生/重复释放 |
| P-016 | 设备删除/添回、空店无效布局、主动Pause | 无效不能开店但可再进Decor修复；有效Done自动接待，主动Pause不被覆盖 |
| P-017 | 多设备稳定绑定、多Slot/旋转家具与Grid root | CR两侧正确，CM/Pick-up稳定可达；杯子归属正确Slot且三角形，坐标变换正确 |
| P-018 | 客人/员工/协调层disable-reenable，Domain Reload开关重复Play | 保留身体和业务checkpoint边界，无重复员工/UI/旧subscription/跨sessioncallback |
| P-019 | P9/P10/P11/P12/P13直接回归与独立受控验证模式 | 原domain合同/受控任务/导航/装修输入保持，P14自动调度不抢独立测试的所有权 |
| P-020 | UI只读投影、横/竖Game view、Console | Order/队伍/杯子/容量与runtime吻合，无UI触发写入；无新增未处理Error/MissingReference |
| P-021 | 实际Done时PrepareExit同步回调和暂停恢复事件 | 先清场与退休再恢复；version发布前无旧杯/旧Order/新移动，门禁不泄漏且主动Pause保持 |
| P-022 | 真实显示port故障与领取提交拒绝 | 业务拒绝不丢杯/Order；已提交后的visual失败不重复业务；修复重绘与snapshot一致 |
| P-023 | builder安装两次、旧HUD按钮、Update驱动计数 | 一套P14、无双倍计时/重复任务；旧P13受控Scene独立验收仍可使用 |
| P-024 | shared-anchor Hold Collection、各阶段disable恢复 | hold下仍能安全送5杯且无重叠；disable前后checkpoint连续，恢复不重做完成阶段 |

2026-10-22 Owner批准P-005／P-009拥堵扩展（以下为验收要求，尚非执行PASS）：

- 队伍站满后仍有至少一条完整CM→合法交付侧→CM路线；检验真实送达／返回，不只侧点或规划返回非null。单侧、多侧及柜台转弯布局均保留原身体安全。
- 默认路线被站稳本队列客人挡住时先用合法备用侧／有限绕行；必须让位时真实移动到安全目标，FIFO／Order／Visit／原三capacity token保持，旧Arrived失效。让位期间新reserve不能抢临时腾出的原slot，普通前移不得越队。
- 分别覆盖移动中的交通（普通等待）、可安全让位的静态等候者（实际让位后送杯）、没有安全位置／静态封路或预算耗尽（明确可恢复Blocked，不无限WaitingToDeliver）。修复后同一Prepared继续，不重做3秒或生成第二Order。
- P-008／018／021联合覆盖让位途中Pause、disable-reenable、取消／changed Done及迟到旧回执；无零游戏时间移动／交付、无新请求／通道ownership泄漏，保留离场者身体继续参与安全检查。
- 收银2秒、制作3秒、真实最终到侧才Ready、实际回CM才Idle；未授权跨P12/P13行为改变。原Guide三项Owner PASS保持原源码／视觉范围，新让位观感另验。

<a id="furniture-layout-matrix"></a>

### 送杯拥堵补修后的家具布局矩阵

Owner要求修复后设计多种家具摆法并实际测试。按外露侧、连接形状、站点相对位置、空间边界与交通状态划分覆盖；使用正式Decor或同一正式layout API构建，每项记录家具定义／grid坐标／rotation／Surface Slot／预期／实际结果。方向代表由真实家具摆法生成，不把单纯改Facing冒充布局旋转。下表保留原覆盖要求，不等于NUnit case数；最终实际PASS及分层边界见后面的映射表。

| 布局ID | 家具摆法／运行条件 | 验收重点 |
| --- | --- | --- |
| L-01 | 开阔区域，CR／CM／Pick-up分开，Pick-up四侧外露 | 正常真实收银→制作→送达→返CM→领取→离场，所有ownership与容量正确 |
| L-02 | Pick-up单边接柜台，三侧外露；四种连接方向 | 不选连桌内侧，备用侧及通道完整，站位不封送杯路线 |
| L-03 | 连续长柜台中间Slot，两个相对外露侧；0/90/180/270° | 桌杯绑正确Slot，队伍沿安全链转弯，后客Facing正确，送杯／返回不被等候链堵住 |
| L-04 | L形柜台／转角，只剩两个相邻可用侧 | 合法角侧可服务，交付和领取按侧互斥，不能切桌角或穿人 |
| L-05 | 三面家具／墙围住Pick-up，仅一侧；覆盖四个朝向代表 | 独立等候位、Preparing不占唯一交付口，员工实际离位才允许同侧领取 |
| L-06 | 狭长通道，CM与唯一交付侧共线，旧waithead落通道中 | 有其他安全等候空间时选偏移头位／安全短队，原单真实送达及返程；不是拒绝全部顾客来假装无死锁 |
| L-07 | CM与Pick-up隔柜角，去程和返程须绕行 | 两腿都有完整安全路线且实际走完；最终到侧才Ready、返CM才Idle |
| L-08 | Pick-up／柜台靠近入口与出口保留区 | 非法摆放正常拒绝；合法临界位置仍保留入口／出口／身体间距，不能将等候位塞进保留区 |
| L-09 | Pick-up靠Floor边缘／墙，后方队列空间不足 | 允许安全折行或较短物理队伍，容量不伪造、不向Floor外排队、不先收费后无位置 |
| L-10 | 两CR、两CM、长柜多Slot及旋转后新单 | 真实稳定绑定当前有效服务组合，正确Slot，不用固定GUID绕过另一合法组合 |
| L-11 | 有送杯通道的紧凑布局，Hold Collection使队列／桌杯积累 | 累积送达仍不堵员工完整往返；恢复后Paid FIFO、补显示及全部原token真实释放 |
| L-12 | 顾客从送杯路线前穿行／CR队伍正常服务前移 | 暂时交通等待后恢复，不因一帧冲突即Blocked，也不忽略正在行走的身体 |
| L-13 | 已站稳本队列客人封路，旁边有安全让位空间 | 有限实际让位、原FIFO／Order／reservation不变，真实清空后送杯，不过早复位封返程 |
| L-14 | 封路客人无安全让位点，或有限候选耗尽 | 明确可恢复Blocked及具体原因，不无限WaitingToDeliver／不隔空Ready；排障后原Prepared继续 |
| L-15 | L-13代表布局在让位途中Pause／disable、取消／changed Done | 冻结与恢复、迟到旧回执无效、owned请求／通道清理；Done沿原清场规则 |
| L-16 | 家具造成静态不连通，所有外露侧均无完整服务往返 | 不可服务明确可见，不收费／不冒认路径；正常装修修复后能重新服务 |

L-02／03／05优先参数化覆盖方向；L-11～15复用相关几何，避免重复无关Scene。执行记录必须区分PASS／FAIL／BLOCKED／NOT_RUN及预期拒绝：合法布局应成功，设计上无解的布局只有正确提示／不非法提交／修复恢复全部成立才PASS。最后补实际test方法与结果链接，未经执行的行保持NOT_RUN。

历史中途记录（2026-10-23 03:32Z，当时矩阵进行中）：L-02三侧四向`matrix-three-sides3.xml` **4P/0F**；L-03长柜四旋转＋L-05单侧四向`matrix-rotations.xml` **8P/0F**，两批互不重名、12个变体全通过真实单链。均按正式Decor Confirm，实际送杯／返CM、正确support/slot与桌杯父节点、领取／真实出口／原token释放，收费／制作／交付各一次；不把BUILT记录当PASS。当时其余L号尚待，现以最终同源映射为准，不沿用旧源码结果。

可复现配方共同底座：8×8 Grid、CellSize=1，Counter Module支撑CR在(1,5)、CR 0°；CM支撑在(4,6)、CM 90°。L-02的PP支撑(4,4)/slot.0，再分别接空柜(4,5)、(5,4)、(4,3)、(3,4)，四次独立运行。L-03改用`counter.preset.1x3`，0°／180°实际anchor(3,3)，90°／270°旋转保持中心后actual anchor(2,4)，PP都slot.1；不是把四旋转坐标强写相同。L-05的PP仍(4,4)/slot.0，以北／东／南／西四邻格围住其中三边，各保留一侧开放。每次新GUID／实际绑定及完整家具清单见`outputs/phase14/delivery-corridor/layout-recipes.jsonl`中同method的PASS记录；BUILT及失败历史保留。

#### 家具方案与实际验证映射（最终同源直接验证PASS）

下表连接家具配方和实际test层。返程临时交通补修后，最终`delivery-corridor/final2-edit.xml`256P／`final2-play.xml`163P＋`final2-natural.xml`1P，全部0F/0S、Play共164唯一case；17个`Matrix…`结构变体均Passed，其它同批动态case按下表分层补证。六源及四永久test hash由root核对一致，原QA本范围C/I CLOSED。原失败保留在[Validation Report](../../Phase14_Validation_Report.md)，不累计focused重复数。`Matrix…`方法位于`CafeFurnitureLayoutMatrixTests.cs`（partial `CafePickupSideTests`）；其它方法沿用同Phase14目录现有文件。

配方B为上述8×8 Main底座（CR支撑(1,5)/设备0°，CM支撑(4,6)/设备90°）。配方S为现有Shared：CR支撑(1,2)/设备90°，CM支撑(6,3)/设备0°，PP支撑(7,6)/slot.0，空柜(7,5)/(7,7)。记录中的rotation以实际确认结果为准。动态围挡用本test注册的真实身体，不能当作只摆这些家具就必然触发让位。

| ID | 可复现摆法／条件 | 实际验证方法与证据层 |
| --- | --- | --- |
| L-01 | B＋PP单柜(4,4)/slot.0，四侧开放 | `MatrixOpenFourSidesCompleteRealRoundTrip`，真实完整单链 |
| L-02 | B＋PP(4,4)，分别接北(4,5)／东(5,4)／南(4,3)／西(3,4)空柜 | `MatrixThreeSidesHaveRealDeliveryAndReturn(0..3)`，四套实际新GUID与完整单链 |
| L-03 | B＋1×3长柜，0°/180°anchor(3,3)，90°/270°anchor(2,4)，PP slot.1 | `MatrixMiddleSlotRotationsHaveRealDeliveryAndReturn(0..3)`验证真家具与中slot；多人折行／前移Facing另由CF `PickupBendFacesPreviousCustomerAndUpdatesAfterActualAdvance`／`RotatedPickupBend…`受控service geometry分层验证，不称四种Decor都灌满多人 |
| L-04 | B＋PP(4,4)，接空柜(4,5)/(5,4)，只余相邻South/West | `MatrixAdjacentCornerSidesCompleteRealRoundTrip`；同侧互斥／备用侧另由Side `BlockedPreferredSideUsesAlternativeWaitHeadAndCollectsBesideOccupiedDeliverySide`及Edit侧lease断言补证 |
| L-05 | B＋PP(4,4)，四邻柜围三边，各开放一边 | `MatrixSingleSideOrientationsHaveRealDeliveryAndReturn(0..3)`；离位前不能同侧领取由`SingleSideWaitsUntilTheEmployeeActuallyVacatesThenCollects`补证，不称每朝向都独立验证并发窗口 |
| L-06 | S另加(1,6)、(4,5)/(5,5)/(6,5)、(4,7)/(5,7)/(6,7)；CM移到(1,6)支撑并转90° | `SingleSideQueuePreservesActualDeliveryAndReturnCorridor`，实际wait偏移、真实送达／CM／领取出口，不靠拒绝所有客人避死锁 |
| L-07 | B＋PP(4,4)，空柜(4,5)/(5,5)形成转角 | `MatrixCounterCornerRequiresActualDetourAndReturn`断真实cornered native及最终实际CM；Edit `SelectedReturnRouteConsumesEveryLegAndOnlyFinalMachineArrivalFinishes`／Returning恢复验证受控port逐leg消费 |
| L-08 | 正式将CR柜preview移入口保留格，拒绝后Cancel；合法边缘配方继续 | `MatrixEntranceRejectsFurnitureAndFloorEdgeKeepsOwnedWaitsInside`，正式入口拒绝／原位置保持／恢复及实际单链；临时让位候选复用InEntrance guard |
| L-09 | B＋PP靠东Floor边(7,3)/slot.0 | 同一edge case核全部planned wait位Nav合法且非入口，真实单链；Edit `ConstructorCopiesSlotsAndHasNoHiddenServiceCapacity`／`NoPickupReservationMeansWaitWithoutChargeOrWork`与CF `NoPickupSlotKeepsCustomerAtCounter`补物理位上限及无位不工作／不建单。未称边缘已灌满真实人群 |
| L-10 | Guide M-017现有两CR／两CM／长柜slot.1及旋转Grid配方 | `RotatedGridMultiStationAndNonFirstSlotUseActualOrdinalBindingAndCupModule`及`RecordedGuidSecondRegisterAndMachine…`／`FirstRegisterSecondMachine…`／`SecondRegisterFirstMachine…`四组合真实单链；固定ID用于覆盖不同ordinal组合，不只强制选择一台绕过其它组合 |
| L-11 | 现有分离与Shared两验证布局，Hold Collection积累 | `HeldCollectionAllowsFiveRealDeliveriesAndFifoDrain`／`SharedServiceAllowsFiveRealDeliveriesAndFifoDrain`＋单列原`ContinuedArrivalsFillThenResumeCollectionAndDrain`，真实五送达、三显示、FIFO补杯、连续接纳与原tokens排空 |
| L-12 | S的CM柜移(6,0)/设备90°，第二真实CR客Working；5个注册身体在world X=-.5/.5/1.5/2.5/3.5、Z=-1.5形成受控通道，客人实际离CR后移自有围挡 | `SingleSideRelatedCounterWorkingIsTemporaryDeliveryTraffic`，真实2秒Working／已验证完成事件交接／付款后实际腾位，两原单全链；CF `FuturePickupCrossingWaitsWhileRealRequestIsOutsideDeliveryCorridor`／`CancelledFuturePickupCrossingDoesNotKeepDeliveryWaitingForever`另测完整future route／Cancel |
| L-13 | 正式S＋动态注册身体，使已Paid等候者需实际让位；另有18m受控Nav双候客窄廊 | `SingleSidePaidOwnerActuallyYieldsThroughDeliveryReturnThenCollectsWithOriginalTokens`／`SingleSideHeldReadyStartsSafeCollectionBeforeActualMachineReturn`核实际Order链；CF `TwoStationaryWaitersYieldWithUnrelatedCounterQueuePresent`／`TwoHeldWaitersRestoreAfterActualMovingTrafficVacatesOriginalSlots`核两位及临时拒绝后复位。CF层本身不建立Order |
| L-14 | S无安全侧的静态阻挡；以及L-12横墙持续保留的独立分支 | `SingleSideStationaryObstructionReportsPreparedBlockedAndExplicitlyRecovers`＋CF `ExistingPickupWaiterWithoutSafeYieldKeepsOriginalSlotAndCapacity`；`SingleSideDenseStationaryCorridorBlocksPreparedThenExplicitlyRecovers`核有界Blocked／清墙显式Resume／原两单。首次最终批返程FAIL保留；return-traffic补修后该原case在final2真实两次送达／返回／原token排空PASS |
| L-15 | L-13真实phase1已移动>.1m，请求仍活动时Pause；分别disable/Resume、正式改CM后Done | `SingleSideActualYieldPauseDisableResumeKeepsPaidBodyAndTokens`／`SingleSideChangedDoneDuringActualYieldRetiresOriginalPaidVisit`；旧callback拒绝由原generation／epoch相关Edit与Play守卫补证，不称这两项独立注入了伪造callback |
| L-16 | B改PP(5,5)，以X/Y=3..7外围柜围出四侧外露孤岛；正式将柜(3,5)移到(0,7)开口 | `MatrixExposedButDisconnectedSidesBlockWithoutChargeThenFormalRepairServes`：formal resolver实际4侧，初始不建单／不创建Capacity ledger／有拒绝原因，移柜后真实完整单链。未初始化ledger不写成已初始化0用量 |

## 4. Manual 测试准备

**场景、菜单与两套五杯布局已实现并有真实自动验证；Owner最新委托的四组功能已由Agent完成，本人仅看三项visual。** 当前说明与可直接打开的截图见[Beginner Guide](../../Phase14_Beginner_Guide.md)。Agent与Owner视觉结果分列，旧Owner状态不自动改PASS。Task9工具Console反馈可见性（I-UX-01）已有真实LogAssert证据；原人工步骤保留为复现说明，不要求Owner重搭已委托的功能现场。

已提供的入口如下；Owner执行前仍须按Beginner Guide核对当前配置：

1. Unity `6000.5.5f1` 打开P14 worktree，场景使用现有 `Assets/Scenes/MainCafe.unity`。实现者先确认Scene路径及P14接线，不能让Owner猜哪份Scene。
2. Hierarchy实际根对象 **`P14 Cafe Loop`**，含验证组件 **`CafeLoopValidationController`**；正式只读UI与验证菜单分开。
3. 现有组件菜单：`P14/Resume Blocked Flow`、`P14/Hold Arrivals`、`P14/Resume Arrivals`、`P14/Hold Collection`、`P14/Resume Collection`、`P14/Print Snapshot`、`P14/Block Current Delivery Route`、`P14/Block Current Return Route`、`P14/Block Current Exit Route`、`P14/Remove Test Blockers`、`P14/Replay Last Business Callback`。Hold Arrivals只停止测试中的新客生成，让当前顾客继续完成服务和离店，不清订单、不触发装修；它不是正式开始/停止接待UI。阻挡只影响运行时验证，不能绕过碰撞、改已确认Layout或保存到资源。
4. `Hold Collection`只阻止最终业务领取；排队、收银、真实制作和送杯仍运行。共享anchor时队首留在真实合法wait位，不持有service lease挡住后续delivery；普通三杯验收优先使用分离员工/顾客anchor的宽敞fixture，另由P-024核对共享情形。用于确定性构造三杯以上，不作为产品功能。若验证布局不足以安全容纳5位取餐客，应使用实现者交付的已验证宽敞fixture，不移动角色造假。
5. Snapshot须能核对Visit ID、Order key/owner/state、employee ID/handle/checkpoint、delivery keys、可见杯数、三类capacity及清场epoch。它是诊断，不是状态修改入口。
6. 两套已验证布局为`Assets/Scenes/Validation/Phase14CafeLoop.unity`（分离）及`Phase14SharedPickup.unity`（共享），均有一个CR、一个CM、一个Pick-up、真实往返路径和5个合法取餐站位；Task8真实5次交付/FIFO排空已通过。坐标/Prefab见Beginner Guide，不照抄旧P13布局。
7. 每项默认重新进入Play作为独立起点；测试中可用Pause定位阶段。不要修改readonly Order字段，不调用旧P12的Let Front Customer Leave冒充真实取餐。
8. 开始前保存Console既有warning/error基线；测试后记录新增日志。退出Play丢弃运行时修改；不保存测试阻挡或临时Editor设置。

Manual准备入口不完整时对应项为 `BLOCKED`，说明缺失条件；不能把工具缺失或未执行写成PASS。

## 5. Manual 逐项步骤

以下保留完整验收场景／复现步骤；2026-10-22 Owner已把M-007＋010、M-017、M-019、M-021四组功能明确委托Agent，八项本轮普通Editor功能记录PASS，具体层次／工具中止历史见Validation Report。原Shared修复及334／169同源自动证据复用，非本轮重跑。Owner只需Guide上方三项visual，不再搭建四组功能现场；本人视觉验收尚未记录。每项仍记录布局/速度、Visit/Order或截图和实际执行者，Agent不代填Owner visual PASS。

### M-001 — Done后自动接待

- 前置：空MainCafe进入Play，保留setup自动Pause，未主动选择Pause；无需先点击1x。
- 操作：进入Decor，按已验证布局添齐CR/CM/Pick-up及支撑家具，确认后Done；不点任务或开始按钮。
- 预期：布局有效后两固定员工就位，顾客按随机间隔进入并自动开始服务；不会瞬间补满。
- 复位：退出Play再进入。

### M-002 — 完整自动外带循环

- 前置：默认有效布局，1x，自动领取正常。
- 操作：完整跟踪同一Visit，从进店到Counter、付款、Pick-up等候、制作、送杯、取走和出口；用Snapshot核对其Order。
- 预期：对应关系始终一致，取走时Completed，实际离店前仍有Total占用；不需要手动派任务。
- 复位：等待离店，或重新Play。

### M-003 — 连续20单

- 前置：有效宽敞布局、自动领取正常、无验证阻挡，可使用2x。
- 操作：让系统连续完成至少20张不同Order，记录起止keys和Completed数量；使用Hold Arrivals，等待当前顾客实际离店，再Print Snapshot；不能直接完成订单。
- 预期：20单均经过真实服务，没有错取、重复、永久堵塞；记录测得数量，不把只出现20个顾客算20单。排空后三类capacity用量均为0，无残留delivery/手杯/桌杯。
- 复位：Resume Arrivals或退出Play。

### M-004 — Counter FIFO和收银

- 前置：至少3名Counter顾客，1x。
- 操作：Snapshot记录先后顺序，观察前两次收银和后客前移。
- 预期：只服务实际队首，working约2游戏秒；每客一单；后客不会提前收费，CR顾客侧/员工侧正确且不穿人。
- 复位：重新Play。

### M-005 — Pick-up FIFO与正确取餐

- 前置：用Hold Collection积累至少3名付款顾客及已送达商品。
- 操作：记录队列/Order对应，Resume Collection并逐位观察前移取餐。另选至少两个外露侧的Pick-up，在Ready且Barista仍占一侧时观察队首从另一侧实际取餐；一侧被其他身体占用时检查是否可从剩余空侧到位。
- 预期：队首只取自己的Coffee；后客不越过；不同侧允许员工/队首并行，同侧不重叠；正确桌杯消失，顾客拿一份手杯，Order只Completed一次；不以Barista回CM为领取条件。
- 复位：Resume Collection，重新Play。

### M-006 — 三杯显示与补显示

- 前置：宽敞fixture，Hold Collection。
- 操作：等待5次真实送达，Print Snapshot；检查delivery=5与可见杯数；Resume Collection，逐个取走并再次核对。
- 预期：前三杯三角形；第4/5杯仍成功且最多3个可见杯；取走后补显示正确未领Order；最终清空，无漂浮/隐藏杯堆积。
- 复位：Resume Collection，重新Play。

### M-007 — 点单后排取餐队、员工送杯与顾客持杯离店

- 前置：有效布局，1x，能看到CM与Pick-up。
- 长柜队形追加：后客站稳后应面向前一等候位，首客取杯实际离位后，原第二位前移成为队首并面向Pick-up；安全站位可转弯。Agent已用近似截图布局普通Editor验证，Owner只看Guide的两张修后近景，不要求重新搭功能现场。
- 操作：先跟踪同一顾客：Cashier完成2游戏秒、Order创建后，观察其是否在Coffee尚Waiting/Preparing时前往Pick-up排队。另用Shared默认布局1080×1920重测首客，观察从CR走过柜角到等候位的完整过程，不能只看到起步就结束；应在Barista仍Working时实际腾出CR，不再原地停约3秒等内部重算。有前客时先去队尾再前移是正常FIFO。再跟踪Barista从3秒working到持杯、放下、空手回CM；最后观察Shiba和Westie各一位领取后的手杯与离店动作，可Pause看手部后恢复。
- 预期：成功点单后即请求取餐排队，不等Ready；短暂空间安全等待不跳过、不瞬移。员工到达才放杯、回位前仍忙。顾客领取前空手、领取成功即Completed并持一个杯离场；Carry上身与Walk腿部一致，停下不原地走，Pause时位置与动画冻结，无额外拿杯/喝饮料等待；两种动物杯子不漂浮、不穿掌，实际出口后无残留杯。
- 复位：等员工回位或重新Play。

### M-008 — Pause与1x/2x

- 前置：依次准备收银、制作、端杯、可取餐四阶段。
- 操作：每阶段Pause，Print Snapshot并等待至少2现实秒，再恢复1x；另一次2x完整服务。
- 预期：Pause时位置/动画/计时/业务里程碑不推进；恢复只执行一次；2x工作现实等待约减半，无重复乘速。
- 复位：恢复1x，重新Play。

### M-009 — 送达前受阻及恢复

- 前置：Barista已制作完成、正在持杯送达；记录Order key和checkpoint。
- 操作：Block Current Delivery Route，等待有限恢复结束并出现Blocked；确认未Ready；Remove Test Blockers，再Resume Blocked Flow两次。
- 预期：显示原因，原Order保留；从实际位置续送，不重新做3秒Coffee、不收费/新建Order/重复杯；重试无重复副作用。
- 复位：Remove Test Blockers，重新Play。

### M-010 — 已送达后返回受阻

- 前置：至少两个合法外露侧，Barista即将放杯，准备通过验证工具阻挡返回路线；另保留仅一个合法侧的独立fixture作对照。
- 操作：Block Current Return Route；确认Ready且Barista仍占交付侧时让顾客从其他空侧实际到位领取；移除阻挡并恢复。再独立Play使用单侧fixture，观察实际让位前后。
- 预期：多侧时不等待Barista走开/回CM；返回失败不撤回Ready/Completed，员工恢复不重做/重复交付。单侧时安全等待而非穿过员工，实际腾位后可领取；不能强制单侧也必须早于CM到达领取。
- 工具边界：现有`Block Current Return Route`阻挡CM回岗目标，不能保证员工一直占交付侧。若已实际腾位，只记“不等回CM”分支；原侧lease仍持有／第三侧领取需专门近侧真实阻挡证据，不能凭菜单名登记PASS。
- 复位：Remove Test Blockers，重新Play。

### M-011 — 容量及真实取餐站位不足

- 前置：Hold Collection使有效取餐位逐渐占满。
- 操作：观察满额/无站位时状态、入口和Counter；Print Snapshot后Resume Collection观察恢复。
- 预期：不把人叠进同一slot，不收费后丢在无站位处；说明容量或空间原因；空出后按随机间隔恢复，不批量补客。
- 复位：Resume Collection，重新Play。三种逻辑额度分别满额的精确断言由E-019/P-011完成。

### M-012 — 查看装修或取消Preview

- 前置：有未完成Order和手持/桌面杯，记录Visit/Order/进度。
- 操作：进Decor直接Done；另一次移动家具Preview后Cancel，再Done。
- 预期：顾客、订单、杯子和业务进度保留，无重新收银/制作；原流程继续。
- 复位：重新Play。

### M-013 — 装修清场及保留已取餐者

- 前置：同时有未取餐顾客与已取餐但未到出口顾客；Pause固定现场并记录IDs。
- 操作：进入Decor，在不撞身体处确认一次真实家具修改，Done；因主动Pause仍生效，先核对Snapshot，再手动1x。
- 预期：未取餐顾客、全部旧Order、桌杯及员工手杯清空；已取餐者原Actor/ID/身体、Total和手中杯保留；原两员工保留；该顾客持杯正常离店，不被Order清空影响。
- 复位：重新Play。

### M-014 — 被保留顾客的出口受阻

- 前置：已取餐顾客正在离场，Block Current Exit Route使其Blocked；记录已取餐事实和Total。
- 操作：再做一次安全的正式装修修改并Done；确认该顾客仍存在；Remove Test Blockers并Resume Blocked Flow。
- 预期：即使当前状态是Blocked也不清除，手杯保持且停下不原地走；从原位置按新revision持杯离场，Total不提前释放且出口只释放一次，杯随实际退出清理。
- 复位：Remove Test Blockers，重新Play。

### M-015 — 连续装修和旧callback

- 前置：有已取餐离场者和未完成Order，Pause后记录旧epoch/keys。
- 操作：连续两轮安全修改Decor并Done；恢复1x，等新客生成新Order，再Replay Last Business Callback。
- 预期：保留离场者不复制/丢失；旧Order/杯子不复活，新epoch订单不受旧callback影响，无重复释放。
- 复位：重新Play。Replay工具必须明确选择已退休epoch事件，不得回放任意不明事件。

### M-016 — 无效布局、修复与主动Pause

- 前置：有效布局。
- 操作：Decor收起必要CM或Pick-up，Done；再进Decor补回并Done。另一次在主动Pause下进行安全正式修改并Done。
- 预期：缺设备时不营业且说明原因，仍可装修修复；有效且非主动Pause时自动接待；主动Pause时不擅自恢复，手动1x才推进。
- 复位：重新Play。

### M-017 — 多设备、Slot和家具保护

- 前置：fixture添加第二组设备和多Slot支撑，所有路径已验证。
- 操作：观察Cashier／Barista实际岗位、顾客同CR另一侧及实际交付支撑／Slot；Print Snapshot核对Actor／Visit／Order和delivery的supportId／Slot。正常经营尝试移动必要家具，只能经Decor；选另一合法Slot／旋转支撑并确认Done。
- 预期：实际岗位绑定稳定，Cashier与顾客同CR两侧；桌杯只在对应支撑／Slot；正式修改按清场规则，不保留旧Order杯；不能绕过Decor／身体保护。精确设备InstanceId的ordinal选择由P-017自动G6（R.RotatedGridMultiStationAndNonFirstSlotUseActualOrdinalBindingAndCupModule）证明，本项人工核对上述实际可观测事实。
- 复位：重新Play。

### M-018 — 停用与重新启用

- 前置：分别准备制作中、送达前、已取餐离场者，记录ID/位置/checkpoint。
- 操作：一次只停用对应Actor或协调组件，再启用并使用恢复入口；对象操作由实现者在Beginner Guide标出。
- 预期：停用不当离店、不提前释放；身体保护保留；已取餐顾客重新启用后仍恰一个手杯，从实际位置恢复，无重复员工/收银/制作/订单和旧handle复活。
- 复位：恢复所有勾选，重新Play。

### M-019 — 只读UI、横竖显示与Console

- 前置：运行中有排队、Ready、Blocked等可核对状态。
- 操作：将UI与Print Snapshot逐项核对；Game view切横屏/竖屏，操作时间和Decor；保存Console新增日志。
- 预期：队伍/owner/state/桌杯数/容量一致（顾客已领取手杯不计入delivery或桌杯），UI刷新无写业务；关键状态可读、按钮可用；无新增未处理Error/MissingReference/异常刷屏。截图所示CafeLoopPlaySessionTests的obsolete Find API warning不再出现；其他既有warning独立记录，不靠Clear或pragma屏蔽。
- 复位：恢复Game view尺寸，退出Play。既有warning列明，不靠Clear冒充干净。

### M-020 — 重复Play与Domain Reload

- 前置：记录原 `Project Settings > Editor > Enter Play Mode Settings`。
- 操作：Domain Reload启用和关闭分别进入Play→完成一单→退出→再进入，各做两轮；核对初始状态和Console。
- 预期：每轮一个干净session，两员工、一套UI、无旧Order/杯/回调；无重复订阅导致双重业务。
- 复位：恢复原Editor设置，不提交临时设置。

### M-021 — Done清场门禁与暂停恢复

- 前置：有未完成Order、已取餐离场者；1x下进入Decor，记录旧epoch/Visit IDs。
- 操作：Done前Print Snapshot并观察角色/杯子，确认一次安全家具修改后Done，再Print Snapshot核对epoch、清场与保留者；另一次主动Pause后重复，检查Pause仍保留。
- 预期：没有先继续旧任务再消失的闪动/重复交付；保留离场者位置连续、手中杯保持原对象；有效布局无门禁遗留，主动Pause不被解除。Print Snapshot是当前事实而非事件trace；同步精确顺序由`CafeDecorationFlowTests.PreparingGateRetiresOldCallbacksBeforeNavigationAndRelease`（P-021）自动证明，人工前后观感不替代它。
- 复位：重新Play。

### M-022 — 旧入口隔离与安装幂等

- 前置：实现者已在非Play、无dirty Scene条件下运行P14 installer两次，提供安装检查结果；Owner进入MainCafe Play。
- 操作：检查P14根/员工/UI各一套，观察一轮收银和制作；检查旧P13手动派单/旧P12直接离场入口在P14模式被隐藏或明确拒绝；另打开独立P13验证Scene确认受控入口仍可用。
- 预期：P14只有自动业务入口，不受旧按钮干扰、不双倍计时；P13独立Scene未被installer改写。
- 复位：退出Play，回到MainCafe；不保存运行时修改。

## 6. 规则覆盖与回归范围

| Design规则 | EditMode | PlayMode | Manual |
| --- | --- | --- | --- |
| D1付款与Create一次 | E-002–006 | P-002–003 | M-002–004 |
| D2单列Pick-up FIFO/实际站位 | E-010–012、015 | P-004–005 | M-005、011 |
| D3Order杯与三杯补显 | E-009、013–014 | P-006–007 | M-006–007 |
| D4选择性清场/空Order仍离场 | E-028–031 | P-012–015 | M-012–015 |
| D5Blocked保进度恢复 | E-023–027 | P-009–010、014 | M-009–010、014、018 |
| D6Done自动接待/主动Pause | E-021–022、032 | P-001、008、016 | M-001、008、016 |
| Capacity/ownership/过期事件/UI | E-016–020、031–032 | P-011、015、017–020 | M-011、015、017–020 |
| Review R1–R7新增边界 | E-033–040 | P-021–024、018 | M-021–022、006、018、020 |

Task阶段只跑新增focused和直接受影响回归。Phase末集中完整EditMode/PlayMode及真实Scene；检查P9 Order state/FIFO、P10 owner/token、P11 Navigation no-teleport/body guards、P12入店/队形/旧清场验证模式、P13 task/carry/三杯旧模式，以及Decoration输入/身体保护。

P12/P13历史用例若与获批的P14整合模式有不同结果，应显式区分独立受控模式与P14接线，不能直接删旧测试或放宽通用断言使其变绿。

## 7. 执行记录与验收条件

原Task9快照：E-001–040、P-001–024已有具体自动method/layer/evidence映射，见第8节。当时完整Play1319P/0F/5S、CLI0，P14全部51P/0S；受影响Edit254P/0F/0S、CLI0覆盖P11全部82/P14全部124/P12直接48。完整Edit2723P/0F/0S更早于disabled guard及Nav修正。Nav正式Edit17P2F/Play0P1F→private连续3D证明修复后，RecordedNative及15/30/144/variable detour在该完整序列通过。旧carry实际cap1 RED保留，test-only预算等效计量相关67P及该完整序列通过。

M-006／011后续窄修复：真实waypoint行为RED0P/2F→CustomerFlow私有未发segment临时让路门禁；最终受影响Edit157P/0F/0S、Play90P/0F/0S（当时P14全部54＋P12队列36）、CLI均0，独立窄QA CLOSED。新增两个waypoint与持续到店恢复方法已纳入P-005／006／011映射，详细边界见Validation Report；旧完整suite不冒称该修后同源结果。当时Owner均NOT_RUN；之后M-007已有反馈待复测，当前Owner/Agent状态以Guide为准，文档映射不冒充运行结果或Owner验收。

M-017／019后续验证修正：原Guide第二CR队首占入口的输入RED0P/1F保留，只把配方第二CR移至(3,3)，四种CR／CM ordinal组合4P/0F；runtime未变。代测工具在EditMode经公开Search API完成真实默认索引首次初始化后才Play，保留首次仅等自然delay45秒BLOCKED历史。`G17-20261020-164313`普通新GUID实际完成中间Slot杯与旋转后新杯、冷启动索引初始化及Application Console0异常；M-017／019 Agent客观PASS，窄QA CLOSED。M-019横／竖UI沿用未改UI的C组实际证据；Owner观感仍NOT_RUN，源与收尾边界详见Validation Report。

执行时每条记录：`ID | PASS/FAIL/BLOCKED/NOT_RUN | executor | source commit/工作区修改 | Scene/配置 | 实际结果 | evidence路径 | 缺陷/复位结果`。保留失败历史，修复后附新结果，不覆盖旧失败。自动XML、真实场景和Owner手测分别报告。

Phase验收需要：20单真实自动闭环、ownership与FIFO正确、容量无leak、Pause/速度正确、失败恢复不重复业务、装修清场例外正确、UI一致、Console无新增未处理error、必要回归和Owner manual验收完成。未执行的Player/device不得写PASS，未解决Critical/Important阻挡收尾。

Owner已批准配套 [Implementation Plan](../plans/2026-10-19-phase-14-integrated-takeout-loop.md) 并按Task实施；总验收仍需独立review及Owner manual。未授权CI、commit/push/merge或下一Phase。

## 8. 实际自动测试映射（2026-10-20）

作者实现验证，不冒充fresh reviewer独立QA。40E/24P是需求场景，不等于NUnit数量；domain/port/真实Scene证据必须分层。`outputs/phase14/full-edit.xml`为2723P/0F/0S（P14 124P），CLI0，05:45:54Z–06:18:36Z，属于05:41 UTC、disabled guard及Nav修正前快照。最终runtime受影响Edit见`task9-nav-edit-green.xml`254P/0F/0S，08:35:46Z–08:35:57Z；最终`full-play.xml`为1319P/0F/5S、CLI0，08:53:38Z–09:11:56Z、1097.7854724s（P14 51P/0S）。四轮FAIL历史与修后证据保留；各快照和重叠counts不累加。runtime冻结08:35:02Z/tests08:48:59Z，之后仅docs/已证明自动资产精确复位。

Class缩写：Edit路径`Assets/Tests/EditMode/Phase14/`：C=CafeLoopContractTests，Q=PickupQueueTests，A=CafeAdmissionTests，CP=CafeEmployeeCheckpointTests，S=CafeLoopServiceTests，V=CafeCupProjectionTests，B=Phase14SceneBuilderTests，DR=CafeLoopDriverTests，PS=CafeLoopPlaySessionTests。Play路径`Assets/Tests/PlayMode/EditorSceneLoading/Phase14/`：CF=CafeCustomerFlowTests，CC=CafeCustomerCarryTests，ER=CafeEmployeeRecoveryTests，LI=CafeLoopIntegrationTests，R=CafeLoopRegressionTests，DF=CafeDecorationFlowTests，T=CafeStatusAndToolsTests。另DL=CafeDecorationLifecycleTests（Edit）。

2026-10-20 M-007新增映射：`customer-carry-behavior-final-green.xml` CC共5P/0F，分别为`SuccessfulCreateMovesActualCustomerBeforeBaristaFinishesWorking`（P-004）、`ShibaCollectedCupSurvivesPauseBlockedDisableAndChangedDoneUntilActualExit`及Westie对应方法（P-007/008/013/014/018/021持杯部分），`MissingCollectedAppearanceReportsOnceWithoutBlockingActualExit`及`MissingCollectedCupPrefabReportsOnceWithoutBlockingActualExit`（P-022视觉缺配置诊断/业务不回滚部分）。`customer-carry-prefab-green.xml` B共9P/0F，新增`DedicatedCustomerPrefabsKeepAcceptedRigBodyAndNoEmployeePresenter`与`InstallerWiresBothCustomerCarryAppearancesFromAcceptedAssets`（P-001/007/023）。这批证据不将所有旧映射重标为当前全量PASS；直接cohort/普通Editor补证及历史RED均见Validation Report。

多侧新增映射：Edit `PickupSidesTests.AllExposedReachableDirectionsAreReturnedInStableCardinalOrder`（四组）、`JoinedTablesWallsRotatedLongSupportAndEachSlotExcludeInnerCells`（四旋转）及`DifferentSidesCanBeHeldTogetherButSameSideAndSwitchingAreRejected`覆盖E-015；首轮52P包含该9项及旧Pickup26/P8 Resolver17。Play `CafePickupSideTests`四方法覆盖P-005：`BlockedPreferredSideUsesAlternativeWaitHeadAndCollectsBesideOccupiedDeliverySide`、`IsolatedDefaultNorthUsesConnectedEastSouthForActualFullOrder`、`SingleSideWaitsUntilTheEmployeeActuallyVacatesThenCollects`、`TwoStaticSidesWithOneOccupiedKeepWaiterOffTheOnlyDeliverySide`；最终边界XML为`pickup-sides-boundaries-green.xml`4P/0F/0S。直接cohort与普通Editor另列，不把自动测试直接登记为Owner manual PASS。

多侧回归补充映射：`CafeEmployeeCheckpointTests.DeliveryRouteOwnsCopiedSegmentsAndOnlyFinalActualRequestCanCommit`、`SuspendedDeliveryRouteResumesCurrentSegmentAndRetirementRejectsOldArrivals`、`DeliveryRouteRejectsOversizedOrWrongFinalAndKeepsSelectedSideStable`覆盖E-015及P-005/009/010/013/014/018相关送达身份边界；checkpoint20P包含原17及新3。原M017 ordinal方法实际验证多段送达、领取、原return与容量排空；原no-change Cancel方法保留订单／checkpoint继续验证；PendingExit仅以正式port提早实际接近建立真实等待窗口，保留原全部Pause及exactly-once断言。最终直接`pickup-sides-final4-edit.xml`289P/0F/0S、`pickup-sides-final4-play.xml`129P/0F/0S，加同源`pickup-sides-collection-corridor-green.xml`的原自然Continued唯一case1P，合计130不同Play case、0F/0S；其他6项不重复加计。普通Editor以Validation Report实际结果为准，不以NUnit替代manual或Owner验收。

S为真实P9+fake Scene port；CF是真实顾客模块；ER是真实员工但测试commit delegate；LI/R/DF/T为完整owner/service真实Scene。PS是在Edit测试中真正Enter/Exit Play。它们不可互相冒充。

证据索引（均`outputs/phase14/`）：Tasks1–7最终focused见对应report；Task8 Edit43/Play41/native1均PASS。Task9 `task9-edit-coverage.xml`52P；`task9-reserved-path-green.xml`46P；`task9-play-coverage-2.xml`6P/1 fixture FAIL中真实20单1x/2x、G3/G4已PASS；`task9-grid-lifecycle.xml`2P；`task9-final-gaps-red.xml`4P/1 replay FAIL中G4最后两项/Counter负例PASS；`task9-replay-green.xml`2P；`task9-play-sessions-final.xml`2P含每设置两轮完整服务/实际出口。历史FAIL均保留，G6初次input释放过早和G5 true-reload局部闭包失败均为fixture缺陷，未据此改产品。

反馈/序列化补修证据：`task9-feedback-red.xml` 0P/3F，原工具缺少Console输出；`task9-feedback-play-green.xml` 10P/0F/0S含三个LogAssert与原Tools7；`task9-feedback-edit-green.xml` 126P/0F/0S含P14全部124+原P8R冷加载/完整builder两项。首轮`full-edit-pre-feedback.xml`2722P/1F/0S保留：唯一P8R cold-load发现Main五处空YAML尾空格被trim，保留Unity原生序列化后原test及最终完整Edit均已GREEN。只保留这五处Unity原生empty-YAML例外，未改旧test/公共UI或忽略规则。

disabled Customer补修证据：`task9-disabled-customer-red.xml` 1P/2F（fresh Main错误开启Nav gate及原Touch restore1失败；existing Visit保留/恢复firstPASS）；唯一产品改动是Customer.Step在delta验证后对integrated disabled立即return，legacy直接Step不变。`task9-disabled-customer-play-green.xml`76P/0F/0S＝P14全部51＋原Touch整类23＋P11 shutdown2；`task9-disabled-customer-edit-green.xml`172P/0F/0S＝P14全部124＋P12客户直接48，均CLI0。原Touch expected1及live Navigation shutdown必须Pause0的断言全部保留。原Engineering仅此guard/新增测试窄复核由root安排；完整Play修后重跑结果待补。

第二完整Play `full-play-second.xml` 1317P/1F/5S、CLI1（P14全部51P，旧Touch通过）。唯一旧P13 `EmployeeAnimationTests.PauseAndFastScaleWorkingExactlyOnce`两个0.25s wall-clock窗口ratio3.54796863越原1.7–2.3，Pause drift0；原样单项1P及仅采样诊断整类5P均CLI0，后者动画每scaled秒推进率比1.00033712。单次Editor触发细因仍UNKNOWN。仅此test改固定captureDeltaTime/同30完整帧并恢复设置，保留原阈值/暂停精度、真实PlayerLoop，另核scaled delta比2与动画rate比1；无产品改动、不手动Animator.Update。最终完整Play结果待补，历史失败不覆盖。

修后`task9-animation-clock-green.xml`56P/0F/0S（P13动画5+P14全部51），CLI0；Pause drift0，30帧normal normalized advance .4166668 / scaled delta .5000002，fast .8333337 / 1.00000036。runtime freeze06:46:35 UTC，此test写入07:15:49 UTC、流程freeze07:18 UTC（第三轮用源）；原QA窄复核由root保存为CLOSED，该冻结快照的第三轮实际结果见下。

第三完整Play `full-play-third.xml`＝1316P/2F/5S、CLI1，07:18:24Z–07:36:37Z（P14 50P/1F，两个真实20单均PASS；前述Animator clock也PASS）。唯一旧P13 `EmployeeMainCafeTests.RealGameTimeScalesWorkCarryAndFreezesCarrier`在原capture0/实际路径中ratio.623853505越原.5±.12，具体单次触发UNKNOWN；P14 `T.CashierNeverStartsWithSlotOnEarlierPickupRemainingPath`的安全断言PASS，但自然随机到店未出现其额外要求的等待窗口。两个原样复测2P，诊断1P，不覆盖历史FAIL。最小test-only修正：旧P13两速度都同Pause12帧/真实PathComplete/同phase开始全程采样，保留原ratio、capture0/冻结、Delivered/实际杯/回岗；P14用正式CustomerSpawnClock固定合法1秒输入，真实构成remainingRoute冲突，在同Snapshot同步窗口验证activeRequest/实际destination+Route/候选slot按production clearance冲突，未Paid/Order/残留reservation且非Blocked，之后同head自然恢复正式开工，原逐start安全不放松。修后精确2P，最终P14全51+P13 MainCafe11+动画5相关cohort实际67P/0F/0S/CLI0；原QA两fixture窄复核由root保存CLOSED。root因完整sequence concern明确要求第四full，不以focused PASS代替完整序列证据；runtime仍06:46:35 UTC不变，07:44:54 UTC仅test再次冻结。没有以不同快照拼成单次full PASS。

### 8.1 40E实际映射

| ID | 实际method / 层级 | 证据范围 |
|---|---|---|
| E-001 | B.InstallerIsIdempotentAndPreservesExistingLayoutAndRoster；DR.ConfigureTwicePreservesBusinessSessionAndSerializedOwnership；T.StatusIsReadonlyAndReportsActualPauseAndDiagnosticFields | 安装/唯一session/只读UI；PS/LI验证一owner两员工一UI |
| E-002 | S.NotArrivedAndWrongPhaseCannotChargeThenDuplicateSuccessCreatesOnlyOneOrder；CF.PaidFifoMovesRealFollowersAndRejectsNonHeadCollection | 未到位/错phase不收费；真实已到Counter后客不能抢reserve，旧CounterQueue直接回归generation |
| E-003 | CP.IntegratedDurationBoundaryAndOversizedDeltaEmitOnlyOneMilestone(Cashier,2)；S.CashierCompletionCreatesOnePaidOrder | 真TryStartIntegrated 1.99+.01/大delta一次Finished/Create；非旧P13 TryStart分支替代 |
| E-004 | S.CreateRejectedLeavesNoPartialPaidRecordAndRestoresFinishedWithoutTwoMoreSeconds；R.RealCreateRejectionKeepsFinishedAcrossOwnerDisableUntilExplicitRetryWithoutTwoMoreSeconds | 真P9拒绝无Paid半状态；完整owner Finished/disable/显式恢复无需再2秒 |
| E-005 | S.NoPickupReservationMeansWaitWithoutChargeOrWork；S.FinishedCashierWaitsForTemporarilyUnavailableReservedPathWithoutRestartOrCharge；CF.NoPickupSlotKeepsCustomerAtCounter；DF.InvalidChangedDoneStaysBlockedUntilAnotherValidChangedDone | 缺slot/设备不收费；pre-Create暂时物理门禁普通等候；忙后单Waiting见S.ReturningBaristaCannotClaimNextEvenIfIdlePortIsWrong |
| E-006 | S.NotArrivedAndWrongPhaseCannotChargeThenDuplicateSuccessCreatesOnlyOneOrder；S.WrongOwnerOrNonHeadCannotCollectEvenWhenPortReturnsTrue；LI.CompletesTwentyRealOrdersAndDrainsAllCapacityAtOneX/AtTwoX | 每Visit一次；真实20 distinct Orders/owner各一；P9通用API由直接/full回归验证 |
| E-007 | S.RejectedTaskStartRetainsClaimWithoutClaimingAnotherOrder；S.ReturningBaristaCannotClaimNextEvenIfIdlePortIsWrong；T.HeldCollectionAllowsFiveRealDeliveriesAndFifoDrain | Claim拒派保留原key；回岗前不领新单；真实多单FIFO |
| E-008 | CP.IntegratedDurationBoundaryAndOversizedDeltaEmitOnlyOneMilestone(Barista,3)；CP.PreparedFailureDoesNotPrepareAgain；T.RealOwnedBlockersPauseDeliveryReturnAndExitWithoutLosingBusiness | P14精确3秒Prepared；真实delivery受阻仍Preparing |
| E-009 | S.DeliveryRequiresRegisteredExactArrivalAndValidSlotBeforeReady；S.DeliveryQueryReentryCannotCommitAgainstChangedGateOrEpoch；LI两项Twenty | 真P9事务/身份门禁+每单真实最终delivery和owner |
| E-010 | S.PhysicallyAllowedButUnreadyCollectionCannotMutateAnyBusinessOrReservation；S.WrongOwnerOrNonHeadCannotCollectEvenWhenPortReturnsTrue；CF.RealCounterPickupServiceExitUsesOriginalTokensAndActualMilestones | 未Ready/错owner/非队首拒领；真实wait不冒充service到达 |
| E-011 | S.WrongOwnerOrNonHeadCannotCollectEvenWhenPortReturnsTrue；A.CollectionDoesNotReleaseTotalUntilExit；CF.RealCounterPickupServiceExitUsesOriginalTokensAndActualMilestones | 正确key一次Completed，重复无效；Total实际出口释放 |
| E-012 | Q.SuccessfulTransferOrderWinsOverEarlyReservationOrder；Q.DepartedHeadAdvancesFollowersBeforeANewTailCanReserve；CF.PaidFifoMovesRealFollowersAndRejectsNonHeadCollection；T.SharedServiceAllowsFiveRealDeliveriesAndFifoDrain | paid顺序/新generation/实际前移；新尾不抢前位；真实5单FIFO |
| E-013 | V.TenLogicalDeliveriesNeverCreateHiddenOverflowAndRefillOldestKey；V.ProjectionShowsThreeAndRefillsCorrectKey；T.HeldCollectionAllowsFiveRealDeliveriesAndFifoDrain | 1–10逻辑输入可见≤3/无隐藏对象；真实5杯补显示 |
| E-014 | V.MissingSlotRemovesStaleVisualAndReportsFailure；CP.HeldVisualFailureKeepsPreparedBeforePortCleanup；R.RealVisualRefusalAndCompleteRejectionPreserveBusinessThenRepairWhileReturnBlocked | 错Slot/手杯失败；完整owner真display prefab缺失/port拒绝/修复重绘 |
| E-015 | Q.SharedServicePositionLetsDeliveryFinishBeforeCollection/SeparateServicePositionsAllowDeliveryDuringCollection；T.SharedServiceAllowsFiveRealDeliveriesAndFifoDrain；CF.NoPickupSlotKeepsCustomerAtCounter | lease互斥/真实共享5杯、无合法站位明确拒绝 |
| E-016 | A.PaymentDoesNotReleaseCounterUntilDeparture；P12 CustomerAdmissionTests.FailedPreparationRollsBackAllThreeReservationsExactlyOnce/FullAdmissionDoesNotCreatePartialLease；CF.RealCounterPickupServiceExitUsesOriginalTokensAndActualMilestones | batch rollback、原token、付款不提前释放Counter |
| E-017 | CF.RealCounterPickupServiceExitUsesOriginalTokensAndActualMilestones；LI.RealCashierCoffeeCollectionAndActualExitDrainCapacity | 各原token随实际离位/到位/出口；完整单排空 |
| E-018 | A.CancelledPickupCannotBeReoccupiedAndDoesNotAffectAnotherOwner；Q.RemoveOnlyReleasesItsOwnReservationAndRejectsOldArrival；DF.BlockedCollectedExitKeepsOverCapacityTokensThenRepairAndNewEpochRejectOldSuccess | owned release/旧generation/epoch；wrong token底层见P10全suite |
| E-019 | R.TotalFullResetsClockThenDrawsOneCompleteRandomInterval/CounterFullResetsClockThenDrawsOneCompleteRandomInterval/PickupFullResetsClockThenDrawsOneCompleteRandomInterval | 原Capacity正式逻辑输入分别满额；恢复重新Draw完整1–5秒/每tick最多一位，无backlog；锁token不是隐藏身体 |
| E-020 | DF.BlockedCollectedExitKeepsOverCapacityTokensThenRepairAndNewEpochRejectOldSuccess | 原Capacity输入缩小、旧Total保留/超限拒接→真实出口；非Floor缩小UI |
| E-021 | S.ZeroTickAndPauseCannotConsumeSuccessfulCompletion；CP.PausedArrivalCommitsOnceOnlyAfterPositiveGameDelta；LI.CustomerZeroDeltaCannotSubmitReplacementMovement；LI两项Twenty | 零游戏时间无提交/新move；真实Working/Carry暂停位置/动画/remaining冻结 |
| E-022 | DR.InvalidDeltaCannotDriveEitherSubsystem；CP.IntegratedDurationBoundaryAndOversizedDeltaEmitOnlyOneMilestone；LI.SingleDriverPauseAndDoubleSpeedKeepOneWorkingTimer | 非法delta防御，P14精确2/3秒与1x/2x只消费scaled delta一次 |
| E-023 | T.RealOwnedBlockersPauseDeliveryReturnAndExitWithoutLosingBusiness；CP.PreparedFailureDoesNotPrepareAgain | 真registered blocker有界Blocked、明确恢复实际pose继续，不无限Nav retry |
| E-024 | CP.WorkingResumesRemainingSeconds/DisableCapturesBeforeCleanup；LI.OwnerDisableRetainsModeAndResumesSameBusinessSession | 分段/cleanup前保存；完整owner恢复前后真实Working remaining相同 |
| E-025 | ER.PreparedDisableRebuildsHandCupAndContinuesFromActualPose；R.WholeOwnerPreparedAndReturningDisableResumeSameBodyWithoutRepreparingOrRedelivery | 真实员工及完整owner Prepared续送，同body、不重做Prepared |
| E-026 | S.ReplayedPreparedCannotRegressReturningCheckpointBeforeExplicitRestore；R.RealVisualRefusalAndCompleteRejectionPreserveBusinessThenRepairWhileReturnBlocked | Ready不回退；返回Blocked时顾客Completed/出口，修复仅回岗 |
| E-027 | CF.HeldMovementKeepsActualBodyAndCapacityThenResumes；T.RealOwnedBlockersPauseDeliveryReturnAndExitWithoutLosingBusiness；DF.BlockedCollectedExitKeepsOverCapacityTokensThenRepairAndNewEpochRejectOldSuccess | 真实等待/移动/出口受阻修复保留队首、body、容量，无传送 |
| E-028 | DF.NoChangeAndCancelledPreviewPreserveOrderAndCheckpoint；R.CancelledPreviewWithTwoTableCupsAndCarriedCupPreservesActualObjectsAndEpoch | 真owner两桌杯+手杯/原对象/handle/epoch保留，继续服务 |
| E-029 | DF.CollectedDisabledVisitAndOriginalCapacitySurviveRepeatedDoneAndExitOnce；DF.DoneDuringActualCarryRetiresDeliveryLeaseBeforeNextEpochDelivery | 混合清场保留已领body/ID/token/两员工；旧hand/lease退休 |
| E-030 | DF.CollectedDisabledVisitAndOriginalCapacitySurviveRepeatedDoneAndExitOnce；DF.BlockedCollectedExitKeepsOverCapacityTokensThenRepairAndNewEpochRejectOldSuccess | disabled/Blocked已领者连续Done；空Order store实际出口一次释放 |
| E-031 | DF.PreparingGateRetiresOldCallbacksBeforeNavigationAndRelease；DF.BlockedCollectedExitKeepsOverCapacityTokensThenRepairAndNewEpochRejectOldSuccess | 真Preparing窗口旧request callback+成功event重放、数字ID1复用不跨epoch |
| E-032 | DF.InitialMainCafeAutomaticSetupPauseValidDoneStartsWithoutPlay/InitialMainCafeExplicitPauseAtAlreadyPausedSetupSurvivesDone/InvalidChangedDoneStaysBlockedUntilAnotherValidChangedDone；PS | 真实Main唯一已装owner，自动setupPause恢复/显式Pause保留/无效阻止；新session干净 |
| E-033 | DF.PreparingGateRetiresOldCallbacksBeforeNavigationAndRelease/CleanupFailureKeepsResumeBlockAndDiagnostic | PrepareExit前gate/幂等/同步重入无move；清场失败不放未知状态 |
| E-034 | S.CommitPublishesBeforeReentrantProjectionAndPreservesCanonicalCollectedIds；CF.CanonicalCollectedBeforeExitRequestSurvivesReentrantClear；R.RealVisualRefusalAndCompleteRejectionPreserveBusinessThenRepairWhileReturnBlocked | 提交先canonical已领；真P9拒绝保持真实reservation/body/杯，再显式恢复 |
| E-035 | S.ProjectionExceptionCannotRollbackReadyOrCompleted；R.RealVisualRefusalAndCompleteRejectionPreserveBusinessThenRepairWhileReturnBlocked | 真实首杯prefab失败/Complete后display拒绝留杯，修复只重绘不重做 |
| E-036 | DR.ConfigureTwicePreservesBusinessSessionAndSerializedOwnership；LI.SingleDriverPauseAndDoubleSpeedKeepOneWorkingTimer；R三Full方法Customer clock断言；T.InspectorReplayRejectsCurrentOrUnknownEvent | Customer/Employee扣一次；旧入口/current/unknown拒绝；正式builder隔离旧HUD |
| E-037 | Q.LastWaitSlotHasOneOwner/FailedTransferKeepsReservation；S.CreateRejectedLeavesNoPartialPaidRecordAndRestoresFinishedWithoutTwoMoreSeconds；CF.NoPickupSlotKeepsCustomerAtCounter | 最后物理slot唯一、失败不占别人；Create拒绝不半收费 |
| E-038 | CP.DisableCapturesBeforeCleanup/CashierFinishedRetriesSubmissionWithoutWorkingAgain；R.RealCreateRejectionKeepsFinishedAcrossOwnerDisableUntilExplicitRetryWithoutTwoMoreSeconds；S.FinishedProgressAloneCannotCreateAndPendingSubmissionWaitsForEnabledQueuedVisit；R.DisabledCustomerCannotInitializeNavigationThroughIntegratedOwner/DisabledCustomerKeepsActualVisitBodyAndCapacityUntilExplicitResume | 完整owner Finished未Create→disable→显式仅提交；仅Progress Finished不能收费/Blocked顾客不自动收费；另证disabled Customer不会由owner继续初始化Nav/clock，原Visit身体/状态/同Capacity/token保留后恢复 |
| E-039 | T.SharedServiceAllowsFiveRealDeliveriesAndFifoDrain；Q.HoldCollectionDoesNotHoldDeliveryLease | 真shared合法5wait/5delivery/visible3、hold头客不抢lease、恢复FIFO |
| E-040 | DL.NoChangeAndShutdownDoNotTriggerEditedDone；PS.RealP14RepeatedPlayWithDomainReload/WithoutDomainReload | changed Done≠shutdown；每设置两轮完整服务/实际出口，Dispose后新session拒旧callback |

### 8.2 24P实际映射

| ID | 实际真实method | 实际边界 |
|---|---|---|
| P-001 | DF.InitialMainCafeAutomaticSetupPauseValidDoneStartsWithoutPlay；B.InstallerIsIdempotentAndPreservesExistingLayoutAndRoster | 真Main既有唯一owner（Task9适配旧双owner fixture），空店Done无需Play |
| P-002 | LI.CompletesTwentyRealOrdersAndDrainsAllCapacityAtOneX/AtTwoX | 各20distinct自动真实服务/正确领取/实际body出口；`task9-real20-Normal.tsv`/`Fast.tsv`各20 receipts |
| P-003 | LI两项Twenty；CF.PaidFifoMovesRealFollowersAndRejectsNonHeadCollection | 随机到店并行服务/前移、连续body separation/walkability/位移上限 |
| P-004 | CF.PaidFifoMovesRealFollowersAndRejectsNonHeadCollection／PickupBendFacesPreviousCustomerAndUpdatesAfterActualAdvance／RotatedPickupBendFacesPreviousCustomerAndUpdatesAfterActualAdvance；T.HeldCollectionAllowsFiveRealDeliveriesAndFifoDrain；CC.SharedFirstPaidCustomerReleasesCounterWhileBaristaWorksWithoutNavigationRetry | 真前移新generation、真实paid FIFO/Counter实际离位token；新CF controlled port的三真实Actor/native到位／朝前slot、授权领取离位后两客实际前移与新head方向，0°／90°Pickup geometry。正式RED0P2F→CF11P见Validation Report；不是完整CafeLoop订单测试。Shared首客同引用／释放时retry0／Barista仍Working／最终wait Arrived的旧源证据另列，不以>.1m起步替代 |
| P-005 | T.SharedServiceAllowsFiveRealDeliveriesAndFifoDrain；LI两项Twenty；CF.PickupWaitsAtActualWaypointForMovingBodyWithoutSpendingReplans/PickupDoesNotWaitForeverAfterBlockingBodyRequestIsCancelled | 真共享/分离anchor、wait/lease互斥；未发下一段时真实活动身体暂占只普通等候，Pause不扣预算，实际腾位后原客到站；取消且仍占位则退出等待并走原有限重算，不注入Arrived |
| P-006 | T.HeldCollectionAllowsFiveRealDeliveriesAndFifoDrain/SharedServiceAllowsFiveRealDeliveriesAndFifoDrain/ContinuedArrivalsFillThenResumeCollectionAndDrain | 两正式fixture真5杯→3实际Cup含inactive总数→补显示/FIFO实际排空；新增持续到店至真实接待门禁后的完整恢复，原Total实际离店回执与默认到店间隔 |
| P-007 | LI两项Twenty；CP.IntegratedDurationBoundaryAndOversizedDeltaEmitOnlyOneMilestone | 真Working/Walk+Carry layer/空手Returning/暂停normalizedTime冻结；P14精确2/3秒 |
| P-008 | LI.SingleDriverPauseAndDoubleSpeedKeepOneWorkingTimer；LI两项Twenty | 1x/2x真实闭环，Working/Carry暂停位置/动画/计时；无double scaling |
| P-009 | T.RealOwnedBlockersPauseDeliveryReturnAndExitWithoutLosingBusiness；R.WholeOwnerPreparedAndReturningDisableResumeSameBodyWithoutRepreparingOrRedelivery | 真blocker/有界Blocked、实际pose恢复，不重做已完成阶段 |
| P-010 | R.RealVisualRefusalAndCompleteRejectionPreserveBusinessThenRepairWhileReturnBlocked | 真owner Ready后Barista回程Blocked，顾客先Completed/出口，修复仅回岗 |
| P-011 | R.TotalFull.../CounterFull.../PickupFull...；CF.NoPickupSlotKeepsCustomerAtCounter；T.ContinuedArrivalsFillThenResumeCollectionAndDrain | 三kind逻辑满额/恢复clock和真无合法wait位；另有持续实体现客至任一有效空间门禁、未付款队首普通等候及恢复；不强制Total满额，不把锁token输入称实体现客 |
| P-012 | R.CancelledPreviewWithTwoTableCupsAndCarriedCupPreservesActualObjectsAndEpoch；DF.NoChangeAndCancelledPreviewPreserveOrderAndCheckpoint | 真P14手杯/桌杯原对象及Order/epoch/handle保留 |
| P-013 | DF.CollectedDisabledVisitAndOriginalCapacitySurviveRepeatedDoneAndExitOnce；DF.DoneDuringActualCarryRetiresDeliveryLeaseBeforeNextEpochDelivery | 混合清场、已领保留、carry旧lease退休、原员工body |
| P-014 | DF.BlockedCollectedExitKeepsOverCapacityTokensThenRepairAndNewEpochRejectOldSuccess | 出口无owned路线→Blocked保留→修复实际出口一次释放 |
| P-015 | DF.CollectedDisabledVisitAndOriginalCapacitySurviveRepeatedDoneAndExitOnce；DF.BlockedCollectedExitKeepsOverCapacityTokensThenRepairAndNewEpochRejectOldSuccess | 两次Done、原Capacity输入缩小、新epoch旧callback拒绝；不声称Floor缩小UI |
| P-016 | DF两项InitialMainCafe；InvalidChangedDoneStaysBlockedUntilAnotherValidChangedDone | 设备删/添回、无效仍能编辑、有效Done自动恢复/显式Pause保留 |
| P-017 | R.RotatedGridMultiStationAndNonFirstSlotUseActualOrdinalBindingAndCupModule/RecordedGuidSecondRegisterAndMachineHaveSafeQueueAndMiddleSlotDelivery/FirstRegisterSecondMachineHaveSafeQueueAndMiddleSlotDelivery/SecondRegisterFirstMachineHaveSafeQueueAndMiddleSlotDelivery | 出生前刚性yaw37、四个CR/CM ordinal组合、旋转1x3家具slot.1真交付／实际离店归零；旧Guide第二CR(3,2)占入口的0P/1F配方RED保留，修为(3,3)后4P/0F，不改运行时入口保护 |
| P-018 | PS.RealP14RepeatedPlayWithDomainReload/WithoutDomainReload；R.WholeOwnerPreparedAndReturningDisableResumeSameBodyWithoutRepreparingOrRedelivery/DisabledCustomerCannotInitializeNavigationThroughIntegratedOwner/DisabledCustomerKeepsActualVisitBodyAndCapacityUntilExplicitResume；ER各阶段 | 真reload开/关每设置两轮完整单/实际出口；两员工一UI，新session拒旧success；disabled Customer真实owner不初始化Nav、原Visit身体/容量保留恢复；模块ER非整合替代 |
| P-019 | 最终full-edit/full-play及Tasks1–8直接回归；Phase11.NavigationCollisionTests.RecordedLowCounterSentinelPreservesFullSkinTangentAcrossFaceAndCorner/RecordedLowCounterHeadOnIntentionStillStops/RecordedLowPoseIsRejectedByHighCounterFullHorizontalSkin/LowBoxProofChecksWholeCrossRegionSweepAndDegenerateAxes/BoxProofStillRejectsRotationScaleAndOtherColliderShapes；Phase11NavigationSceneTests.RecordedLegalLowCounterPoseArrivesWithNativeMovement | P9/P10/P11/P12/P13原合同/独立Scene；必要private3D证书不减radius/skin，正式Edit17P2F/Play0P1F→合并Edit254P及最终完整Play1319P0F5S；RecordedNative/allFPS detour真实Arrived |
| P-020 | T.StatusIsReadonlyAndReportsActualPauseAndDiagnosticFields；B.ViewSnapshotCopiesMutableInputLists；R.ToolBlockRegistrationAndRejectionPublishVisibleConsoleResults/PrintSnapshotDoesNotOverwriteLatestToolFeedback/ReplayMenuPrefersObservedOldEpochAfterNewEpochOrderExists；Task8 native1P | Console真实注册/拒绝/旧epoch完整token可见与Snapshot不覆盖反馈（反馈RED/GREEN证据见索引）；真横竖GameView `task8-screens-height-fit/status-1080x1920.png`/`status-1600x720.png`尺寸/raycast，root已view；非Owner/device |
| P-021 | DF.PreparingGateRetiresOldCallbacksBeforeNavigationAndRelease/CleanupFailureKeepsResumeBlockAndDiagnostic | 原PrepareExit前退休、同步callback/未发布version门禁；失败保留block |
| P-022 | R.RealVisualRefusalAndCompleteRejectionPreserveBusinessThenRepairWhileReturnBlocked | 真display prefab/port拒绝+真P9Complete拒绝→实际token不动，视觉修复不重做业务 |
| P-023 | B.InstallerIsIdempotentAndPreservesExistingLayoutAndRoster/ValidationInstallPreservesIndependentP12AndP13Assets；DR/LI单驱动；R三Full clock | 两次安装/旧HUDAPI无干扰、Customer/Employee delta一次、旧P13独立Scene保留 |
| P-024 | T.SharedServiceAllowsFiveRealDeliveriesAndFifoDrain；R.WholeOwnerPreparedAndReturningDisableResumeSameBodyWithoutRepreparingOrRedelivery/RealCreateRejectionKeepsFinishedAcrossOwnerDisableUntilExplicitRetryWithoutTwoMoreSeconds/DisabledCustomerCannotInitializeNavigationThroughIntegratedOwner/DisabledCustomerKeepsActualVisitBodyAndCapacityUntilExplicitResume；LI.OwnerDisable... | 真shared5杯与Working/Finished/Prepared/Returning完整owner恢复；完成阶段不重做；disabled Customer真实owner不再驱动Nav/clock，原Visit身体/状态/同Capacity/token保留恢复，1P2F→76P/172P证据见索引 |

实际出口不是“Visit消失即成功”：Twenty/PS用仅测试的Actor root OnDestroy observer，normal测试不手动Destroy/Done/disable客人，teardown先关闭observer。每receipt同时验证同Visit已Completed、同一原Total token Released、真实Transform在实际出口容差内；没有注入Arrived。Twenty另断Orders恰20/全部Completed/每owner一张/Paid↔seen一致，员工Idle/handle0、手杯/桌杯/delivery/三capacity/活动Nav request排空。Shiba/Westie证据指既有动物prefab/Animator输入，非新增正式SpeciesId。

【历史第四轮快照】`full-play-fourth.xml`为1317P/1F/5S，07:49:04Z–08:07:19Z、1095.1407335s、CLI1，P14全部51P/0S；唯一旧P11 variable detour MovementTimeout/retry1停在(.4999816,0,-1.959028)。原样focused1P不能消除该历史FAIL；有限诊断后，fresh合法记录起点正式0P1F复现低.42counter sentinel被旧二维private证书误拒的机制。保留原半径/skin/预算/到站规则，仅修连续3D证书；真实Edit17P2F/Play0P1F→最终受影响Edit254P，RecordedNative及allFPS detour在第五完整Play通过。历史原完整时刻raw intent未捕获，不把fresh初始fixture冒称原序列逐帧重放。旧carry cap1 oracle FAIL及修后67P也保留，当前状态以下方最终记录为准。

最终完整Play：`outputs/phase14/full-play.xml/log`为1319P/0F/5S、CLI0；5个skip为旧P8R四项opt-in capture/audit及P11 capture，P14无skip。六类P14实际7+10+6+8+13+7=51P。Normal/Fast fresh TSV分别09:09:54.6499601Z/09:10:05.5800418Z写出，各20行/20独立Visit/Order/原Total token，CashierFinished、Prepared、FinalDeliveryArrival每行均1；实际出口由同body pose+Completed+原token Released交叉证明，不由消失推断。两个case同时断最终恰20张全部Completed、每客一单/Paid一一对应、三capacity/杯/delivery/handles/requests排空。Editor完整路径进程0；已知P7自动材质三行diff备份后精确复位，旧Art/UI/Settings/ProjectSettings/Validation tracked diff0，P12/P13 Scene+meta literal4/4；非Main diff-check0，Main仅5原生空m_Name尾空格。

M-001–022继续 **Owner NOT_RUN**。Player/Android/iOS/device/CI继续 **NOT_RUN**。独立Engineering/QA/UX及必要fix scoped复核已由root保存CLOSED结论；本表仅记录作者证据，不冒充独立验收或Owner手测。
