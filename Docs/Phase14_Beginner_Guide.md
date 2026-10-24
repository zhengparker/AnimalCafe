# Phase 14 自动外带循环：Beginner Guide

> 当前结果（2026-10-23）：自然单列优先及P14收银1游戏秒已实现，制作3秒、来客1～5秒保持。最终同源直接Edit **267 PASS**、Play **171 PASS**，均0 FAIL／0 SKIP；普通Editor六人站稳／前移／实际出口及自然三单PASS，独立QA与清理证据见Report。只留下新增队形／动作观感供Owner确认；旧三项visual PASS保持，不要求重做整套manual。

上一轮历史结果（2026-10-23早间，2秒源）：排队挡送杯已补修。队列会留出完整往返通道，必要时安全换侧／绕行、让本系统等候客人实际让位；暂时穿行会等待，静态无解会明确Blocked，清障后可继续原订单。当时收银2游戏秒／制作3游戏秒，受影响范围Edit **256 PASS**、Play **164个不同case全部PASS**，原QA当时本范围C/I CLOSED；普通Editor代表现场PASS，临时工具与资产清理已核对。当前P14一秒与多人队形结果以文首新记录为准。旧三项visual（持杯／走路、长柜桌杯贴合、横竖UI）保持 **Owner PASS**；下方旧“待验”是历史记录，不要求重做旧三项。Phase未Completed。

本轮家具实际人数见[Test Cases多人结果表](superpowers/specs/2026-10-19-phase-14-integrated-takeout-loop-test-cases.md#single-file-cohort-results)：原19个结构／rapid变体分别请求2／4／6人，空间不足明确按1／3／4人的安全链限流；达到6人的有6例。全部真实领取／出口并释放原tokens，有后客时验证前移。另2项controlled恢复测试与自然家具分列。普通／加速各20单完成排空，原自然来客长项通过；不用Owner逐套搭家具，XML、历史失败和hash见[Validation Report](Phase14_Validation_Report.md)。

本轮改动的五个runtime文件：`CounterQueuePlanner`保留安全直行prefix，`CustomerFlowController`接P14队形／相关交通等待及一致出口恢复，`EmployeeTaskRunner`公开实际选中完整返程，`EmployeeFlowController`保持返程通道并显式配置P14一秒，`CafeLoopService`同步初始一秒checkpoint。八个现有P14测试文件及一个新planner test＋meta覆盖边界；Scene／prefab和旧P12/P13默认不修改。

收银缩短不保证自然经营一直积累多人：最终Normal20单稳定PaidWaiting峰2、Fast20单峰1，CR相关峰8包含入店／前移者。同一默认fixture的普通Editor自然3单另观察到9个安全slot、Paid未领取／在途峰2但稳定等候峰1，付款后约4.4～4.7秒才走到等候位；都正常领取离店。多人Hold用于验证队形容量；自然吞吐还包含行走、服务侧与安全等待，随机客流两批不能当固定性能对比。CR队首到付款也可能含工作启动前等候，不能把总差值当1秒工作计时错误。

历史长柜反馈已近似复建并修正：原先等候客人都朝柜台，现在队首朝Pick-up，后客朝前一位；取杯后前移成为队首时会更新朝向。当次只改Facing，安全站位仍可拐弯。正式RED0P2F→focused11P，当次直接Edit252／Play133全部PASS，普通Editor同蓝图两人站稳及真实取杯前移也通过；保留该源码范围，当前回归以文首为准。详细证据见Validation Report。

## 本轮只需确认的新visual

Agent已在普通Editor验证六位真实Paid站稳、首客领取后原第二位实际前移，以及六位真实出口／原token排空。截图是1600×1600、capture0的正常PlayerLoop现场，未调用NUnit、未写入角色pose／订单／队列slot；相机只为截图临时取景。Owner观感仍待本人确认。

1. 打开[六人站稳图](../outputs/phase14/single-file/native/long-counter-six-stable.png)，看队首与后客朝向、间距及折返队伍是否自然。这张场地靠边，安全直线只能先延伸一段，再转弯折返；两段投影会相邻，不能把它描述为六人全部直线。每人仍按同一FIFO链跟着前一位。
2. 对照[首客取杯后前移图](../outputs/phase14/single-file/native/long-counter-front-advanced.png)，原第二位已真实到原head位置并转向Pick-up，其余按顺序跟进。判断新的折返／前移观感即可；付款、order、移动身份、实际出口和容量由Agent验证。

若要看动态：在本worktree打开`MainCafe`，Play后Decor按下面配方放置并Done；在第一位领取前用验证组件`Hold Collection`，等到6人后`Hold Arrivals`，再`Resume Collection`观察实际前移。每件家具／设备先✓，Stop后不要保存运行时Scene。空间受限时可能少于6人，参照实际人数表，不通过移动角色凑数。

| 家具／设备 | Grid位置与朝向 |
| --- | --- |
| CR支撑／CR | `(1,5)`，0° |
| CM支撑／CM | `(4,6)`，CM90° |
| Counter 1×3／Pick-up | 起点`(3,3)`，支撑90°，Pick-up放中间`slot.1` |

旧三项visual（持杯／走路、长柜桌杯贴合、横竖UI）保持Owner PASS，不要求重测。临时helper已归档移除，317受保护文件及15最终源码／test均核hash一致，原Editor设置已恢复且Editor已停止，可以正常打开本worktree检查；本轮不是全项目suite、Player或手机测试。

## 此前让位表现参考

此前2秒源普通Editor功能已代测：原已付款客人真实让位／Pause冻结、Barista真实送杯并回CM、同客回原slot，随后正常领取离场与容量归零。以下保留同一位客人的两个历史现场，不作为本轮六人新源截图，不需要你重新搭16类布局。画面中额外静止角色是测试用阻挡身体，用来稳定触发让位，不代表新增了多条排队规则。

1. [实际让位途中暂停](../outputs/phase14/agent-manual/Corridor-20261023-042217/corridor-actual-yield-paused.png)：原站位X=1.4，客人已实际移到X≈.497，咖啡仍Preparing；Pause后应自然停住。
2. [送杯返程后回原位](../outputs/phase14/agent-manual/Corridor-20261023-042217/corridor-actual-return-restored.png)：同客回到X=1.4，咖啡Ready、Barista已回CM。只需确认新增让位／回位的观感；功能已PASS，Owner visual尚未代填。

如想在项目中看动态：打开本Guide的worktree→`Phase14SharedPickup`→Play，观察拥挤时的等候／让位。只有确实挡住完整通路且存在安全临时位置时才让位；正常留出通道的布局不会每单触发让位。无安全路的布局应明确Blocked，清障后使用原恢复操作继续原订单；不用为看到动画人为重叠角色或保存运行时Scene。

历史功能记录（2026-10-22）：委托的四组功能、八项普通Editor检查已PASS，当次未改正式代码／Scene／prefab；Shared首客约.83秒转队、Edit334／Play169保留当时源码范围。该段后续三项visual现已Owner PASS，以文首最新状态为准；旧工具中止与实际结果见[Validation Report](Phase14_Validation_Report.md)。

最新Owner复测问题已补修：`Phase14SharedPickup`首客原先在CR旁转角停约3秒，是绕角候选越出NavMesh后等待内部重算。现于原安全范围内有限尝试较短候选，每条都沿用完整路径／身体检查；默认竖屏已验证不再出现该停顿，原客随后正常取杯离场。此前仅检查起步的自动PASS不覆盖此缺陷，新增正式RED→GREEN及当前回归见Validation Report。M-007功能已按Owner最新要求交由Agent核对，本人仅看持杯visual；有前客时新客先去队尾再前移属于正常FIFO。

## 打开项目与选择Scene

1. 在Unity Hub打开 `E:/Unity/Project/AnimalCafe/.worktrees/phase-14-integrated-takeout-loop`，使用Editor `6000.5.5f1`。不要打开main目录来检查本轮未提交实现。
2. 等编译完成，记录Console原有warning；运行前保存自己正在编辑的Scene。
3. 打开下面一份Scene，再按Play。每项独立手测用Stop→Play复位，运行时不要Save Scene。

| Scene | 用途 |
| --- | --- |
| `Assets/Scenes/MainCafe.unity` | 正式接线入口。空店先Decor摆出有效CR、CM、Pick-up，再Done；有效且未主动Pause时自动开始，无Start按钮。 |
| `Assets/Scenes/Validation/Phase14CafeLoop.unity` | 分离取餐布局；Play期间自动准备设备，用于五杯与完整业务验证。当前自动五杯／FIFO／排空及多侧边界通过，普通Editor补证见下文；Owner手测待执行。 |
| `Assets/Scenes/Validation/Phase14SharedPickup.unity` | 单侧共享取餐布局；顾客与Barista轮流使用服务点，顾客在计入容量的wait位等候。当前自动五杯／FIFO／排空及单侧退让通过；Owner手测待执行。 |
| `Assets/Scenes/Validation/Phase13EmployeeTasks.unity` | 原P13受控派单验证模式，保留旧按钮；不能用它代替P14自动闭环。 |

Editor菜单 `Tools > AnimalCafe > Phase 14` 提供 `Install Cafe Loop in MainCafe`、`Create Cafe Loop Validation Scene`、`Create Shared Pickup Validation Scene`。仅在退出Play、所有已加载Scene均无未保存修改时执行；dirty Scene会明确拒绝。普通观察无需重复安装。

P14顾客使用`Assets/Art/Phase14/Customers/PF_Shiba_P14Customer.prefab`及`PF_Westie_P14Customer.prefab`。它们复用既有完整Carry骨架，保留原导航/身体配置，移除员工表现组件；顾客自己的`CustomerCarryPresenter`在领取后负责手杯和Carry。安装器生成并引用这两份资源，旧P11/P13场景继续使用原资源。

## 验证布局与五个实际等候位

坐标针对独立P14 Scene的默认Grid root；Grid cell从0开始数，X从左到右、Y从入口向店内。MainCafe空店可按分离布局摆放，每件先✓，最后Done。不要在Play中直接移动角色或Grid root来凑坐标。

| Fixture | CR支撑／CR旋转 | CM支撑 | Pick-up支撑 | 额外空支撑 |
| --- | --- | --- | --- | --- |
| 分离 | `(1,5)`／0° | `(4,6)` | `(6,6)` | 无 |
| 共享 | `(1,2)`／90° | `(6,3)` | `(7,6)` | `(7,7)`、`(7,5)`，使Pick-up实际只剩West anchor |

分离布局的员工CR站位为Grid `(1,6)`，顾客服务位为`(1,4)`；CM员工在`(4,7)`。Pick-up旧role-pair默认侧为员工`(6,7)`／顾客`(6,5)`，多侧模式会按真实占位和路径选择合法侧；顾客在独立wait位等Ready，不预占交互侧。共享布局CR员工在`(2,2)`、顾客在`(0,2)`，CM员工在`(6,4)`；Pick-up双方只能轮流使用`(6,6)`，头客在独立wait位等候。

以下仅保留旧`task8-play-final-green.xml`五次真实送达时Print Snapshot的历史world坐标（`X, Z`，高度Y=0）。本轮多侧模式增加独立wait头位，实际坐标应读取当前Snapshot，不按此表要求人物站回旧位。规划器根据真实身体、Nav和可用区域选择站位；默认Grid root的world位置为`(-4,0,-4)`、rotation为0°。

| 队序 | 分离fixture `X, Z` | 共享fixture `X, Z` |
| --- | --- | --- |
| 1 | `2.50, 1.50` | `1.40, 2.50` |
| 2 | `2.50, 0.25` | `0.30, 2.50` |
| 3 | `2.50, -1.00` | `-0.80, 2.50` |
| 4 | `2.50, -2.25` | `-1.90, 2.50` |
| 5 | `2.50, -3.51` | `-3.00, 2.50` |

既有五杯证据：`task8-play-final-green.xml`41P/0F/0S；两fixture完成真实五杯、可见三杯、paid FIFO领取、实际离场及容量排空。原Task9完整Play1319P/0F/5S及Edit254/更早完整Edit2723均为历史源码快照。**本次顾客持杯最终源码直接回归Edit166P/0F/0S、Play71P/0F/0S**，范围与历史失败见Validation Report，未重新跑全项目suite。Owner验收另记。
## 正常经营看什么

当前批准行为：顾客自动入店，实际到Counter队首后P14 Cashier工作1游戏秒，只建立一份付款事实和Coffee订单；旧P13独立模式仍2秒。成功点单后立即请求去已预留Pick-up位排队，不等Coffee Ready；实际路线暂被身体占用仍安全等候。Barista按FIFO制作3游戏秒，手持杯走到Pick-up，实际送达才Ready，再空手回CM；真正回位前仍忙。已到取餐位的正确队首领取自己的Coffee，订单变Completed，随后实际离店。

台面每个Surface Slot最多显示3杯，第四杯及之后仍有真实delivery记录；取走前杯后补显示尚未领取的正确Order。没有隐藏杯堆、金币、价格、收入、Inventory或营业日。本次已批准补上顾客领取后持杯离店：复用Carry动画，停下不原地走，Pause冻结；手杯不计入台面delivery，正式装修Done保留已领取顾客及其手杯，实际出口再一同清理。实现/验证进度见Validation Report，不将此规则描述当作PASS。

底部状态是只读显示：接待原因、epoch、可见杯/逻辑delivery、Completed、Total/Counter/Pickup用量、两条队列、员工状态及Order。短ID用于读图；Print Snapshot提供完整Visit／employee ID、Order key、delivery的支撑ID／Slot、checkpoint与handle。`Working`工作，`CarryingToPickUp`持杯送达，`Returning`空手回岗，`Blocked`需修复并明确恢复。

顶部时间按钮沿用Pause、1×、2×。Pause同时冻结位移、动画、工作与到店计时；2×使用统一游戏时间。主动Pause后Done仍保持暂停。布局无效时保留原因并可再进Decor修复，时间按钮不能绕过门禁。

本次新增规则：Pick-up每个外露且可达的侧都可交互，和桌子相连／被墙或家具挡住的侧排除；Barista占一侧，Ready队首可从其他空侧取餐，同侧才等安全让位。单列FIFO保持。该规则已批准，当前实现状态见Report，旧PASS不证明备用侧已经可用。

本轮取餐快速复测：

1. 打开分离布局`Phase14CafeLoop`并Play，先在下方验证组件菜单执行`Hold Collection`；出现第一位顾客后执行`Hold Arrivals`。
2. 等该单成为Ready、Barista开始Returning，Pause固定现场，执行`Block Current Return Route`，确认Console显示成功注册阻挡。
3. 执行`Resume Collection`并恢复1×。顾客应实际走到可用取餐侧、持杯离店，Order只Completed一次；Barista不必先回CM。
4. 执行`Remove Test Blockers`、`Resume Blocked Flow`，确认员工回岗；Stop结束，勿保存运行时场景。

该菜单挡的是CM回岗目标，员工可能已离开交付侧；这组手测只确认“不等回到CM”。“员工仍持交付侧、顾客走第三侧”由本轮专门的真实流程测试及Agent近侧阻挡补测另证，不能把两种现场混记为同一结果。只有一个合法侧时仍应等实际腾位，可用`Phase14SharedPickup`作对照。

## 验证组件菜单

Hierarchy选择根 **P14 Cafe Loop**，在Inspector找到 **CafeLoopValidationController**，点组件标题右上角菜单，展开 **P14**。所有修改操作只在Play中执行。工具成功／拒绝结果查看Unity **Console**的`[P14 tool]`消息；默认Inspector不显示Feedback property。Print Snapshot另打印完整当前状态，不覆盖最近工具反馈。

| 菜单 | 用途 |
| --- | --- |
| Hold Arrivals / Resume Arrivals | 暂停／恢复生成新客；已有客人与订单继续。 |
| Hold Collection / Resume Collection | 暂停／恢复最终领取；仍可实际收银、制作和送达，头客留wait位，不抢共享服务点。 |
| Print Snapshot | 向Console打印完整只读状态。 |
| Resume Blocked Flow | 修复后请求从当前实际位置续行；不能跳过坏布局、重复收费或重做已完成制作。 |
| Block Current Delivery Route | 仅当Barista正在持杯送达，尝试在其实际送达目标注册本工具拥有的真实障碍Actor。 |
| Block Current Return Route | 仅当Barista正在Returning，尝试阻挡实际回岗目标。 |
| Block Current Exit Route | 仅当有已取餐且未实际离店者，尝试阻挡出口。 |
| Remove Test Blockers | 只移除本组件创建的障碍，不删除原员工、客人或家具。 |
| Replay Last Business Callback | 仅重放本session实际观察过且已退休的事件；完整token、source与拒绝／忽略原因打印到Console，不可指定任意事件。 |

五杯准备先在第一位顾客开始领取前执行Hold Collection，再积累送达；共享fixture尤其如此。Hold不会撤销已发出的取餐移动或让已占服务点的身体瞬间退回wait位。若很晚才开启，已有顾客正前往或已占共享服务点，先Resume Collection让它完成领取，再Stop→Play按前置重新准备五杯。

堵路时先Pause固定正确阶段，再调用对应菜单并查看Console中的`[P14 tool]`结果。只有`Registered owned stationary blocker ...`才表示本次成功注册；正常身体注册拒绝时记录当时状态和拒绝，不把它算作已经堵路。不要改Actor Transform、缩小角色或反复放未知障碍。成功后恢复1×，等有限恢复结束观察Blocked。随后Remove Test Blockers，再Resume Blocked Flow；检查速度并恢复1×。每项结束Stop→Play清掉本次工具状态。

## 装修、停用与恢复

沿用Decor、Catalogue、✓确认、×取消Preview、Store及Done。只查看或Cancel Preview后Done会保留原订单和进度。**正常确认过真实布局修改再Done**会清除全部旧订单、未领取顾客、相关杯子及任务；已实际取走Coffee、尚在离店途中的顾客保留原Actor/ID/身体和Total，包含Blocked或暂时disabled者。两位员工保留身份，从实际位置走回有效新岗位。

M-018只操作Play中的运行时实例：展开 `P13 Employees` 找Actor Id含Cashier／Barista的root；顾客在 `Phase12 Customer Flow` 下按Print Snapshot的Visit/Actor ID辨认。只切换该root的Active或相应flow组件Enabled，不改Prefab、Transform、NavMeshAgent。重新启用后用P14 Resume Blocked Flow，核对原身份、位置和checkpoint；停用不算离店，身体与原容量不能提前释放。

## M-017：多设备与多Slot布局

修正后自动场景已通过的正常Grid坐标如下。Owner从空MainCafe进入Decor，在尚未出生员工时一次摆完，再Done；每个支撑先✓，再放对应设备。不要在Play直接改Grid root；yaw37°与平移是自动测试的刚性坐标防御，人工只操作家具。

| 支撑／设备 | Grid cell | 旋转与Slot |
| --- | --- | --- |
| 原CR支撑／CR | `(1,5)` | 支撑0°、CR0° |
| 主CM支撑／CM | `(4,6)` | 支撑0°、CM90° |
| 第二CR支撑／CR | `(3,3)` | 支撑0°、CR0°；顾客站位避开入口保留区 |
| 第二CM支撑／CM | `(0,6)` | 支撑0°、CM0° |
| Counter 1×3／Pick-up | 起点`(5,2)` | 支撑90°，占`(5,2)/(6,2)/(7,2)`；Pick-up放中间`slot.1` |

运行一单，观察客人和Cashier实际使用哪台CR、Barista实际在哪台CM工作；用Print Snapshot记录Visit／Order／员工Actor ID，以及送达后delivery的支撑ID／Slot。人工核对岗位是否稳定、Cashier与客人是否在同一CR两侧、杯子是否落在中间module。精确设备InstanceId的ordinal选择由P-017自动测试核对：修正后四个固定ID组合分别实际选择原／第二CR与主／第二CM，均完成中间Slot送杯、顾客离店及容量归零；人工新增GUID仍以实际岗位和路径诊断核对。站位／身体保护拒绝时记录原因，不把红Preview或未成立布局算PASS。

旧配方第二CR为`(3,2)`，其顾客站位`(3,1)`落在入口保留区；原固定ID测试仅选原CR而漏过该问题。Agent首次新GUID操作选中第二CR后正确被入口保护拒绝，40秒无顾客，该历史BLOCKED保留。现仅修正第二CR至`(3,3)`，设备选择与身体保护规则不变；同记录GUID的旧配方0P/1F→修正后四组合4P/0F证据见Validation Report。修后普通Editor `G17-20261020-164313`使用正常新GUID，实际选中第二CR／CM，完成中间slot.1送杯及正常旋转／Done后新单送达，Agent客观项PASS；Owner观感仍NOT_RUN。

桌杯应只在长柜台中间module，局部三角形跟随该module。后续正常Decor选择长柜台，移动／旋转到合法空位并✓、Done；本阶段真实修改会清旧订单／杯子，随后新一单仍应落在当前绑定Slot，不要求旧杯跨P14清场保留。每个分支Stop→Play复位；不要复制P13“旧杯随桌保留”的受控模式预期。
## Owner三项visual（2026-10-22已全部PASS）

Owner已明确把下面四组功能委托Agent代测，功能现已完成，实际结果见Validation Report最新小节。本人只看下面三项画面观感；Agent的流程／状态PASS不能替填视觉验收。Done保留／清场、身份与容量、Pause及按钮功能无需再搭场景复测。

1. **两种客人的持杯与走路是否自然。** 先看本轮[Shiba正面](../outputs/phase14/agent-manual/Carry-20261022-122304/customer-shiba-front-moving-carry.png)／[Westie正面](../outputs/phase14/agent-manual/Carry-20261022-122304/customer-westie-front-moving-carry.png)，看杯子是否漂浮、穿手、比例是否合适。动作连贯程度请在`Phase14CafeLoop`直接Play、保持1×，看两种客人各一次持杯离店即可；图只能代表单帧，不能替代动画观感。

   新长柜队形直接看[修后等候近景](../outputs/phase14/agent-manual/LongQueue-20261023-005629/long-counter-settled-pickup-closeup.png)及[原第二位前移成为队首](../outputs/phase14/agent-manual/LongQueue-20261023-005629/long-counter-original-follower-now-head-closeup.png)：后客朝前一位，成为队首再朝Pick-up。也可对照同尺寸[修前全景](../outputs/phase14/agent-manual/LongQueue-20261023-004134/long-counter-settled-pickup-formation.png)／[修后全景](../outputs/phase14/agent-manual/LongQueue-20261023-005629/long-counter-settled-pickup-formation.png)。站位可安全转弯，未强制一条直线；Agent已搭建并验证真实前移，无须你再搭功能现场。

2. **桌杯与长柜台的贴合是否合适。** 打开本轮[中Slot送杯](../outputs/phase14/agent-manual/G17-20261022-123420/m017-actual-middle-module-cup.png)和[旋转后送杯](../outputs/phase14/agent-manual/G17-20261022-123420/m017-after-normal-support-rotation.png)，可放大原图，看看杯子落在中间module时是否贴桌、悬浮或穿桌。五组设备与旋转功能已由Agent搭建测试，无须Owner再搭一遍。
3. **横竖屏UI是否清楚。** 看本轮[横屏营业](../outputs/phase14/agent-manual/ReviewUI-20261022-123922/m019-business-1600x720.png)／[竖屏营业](../outputs/phase14/agent-manual/ReviewUI-20261022-123922/m019-business-1080x1920.png)，以及[横屏Decor](../outputs/phase14/agent-manual/ReviewUI-20261022-123922/m019-decor-1600x720.png)／[竖屏Decor](../outputs/phase14/agent-manual/ReviewUI-20261022-123922/m019-decor-1080x1920.png)。只看文字大小、裁切、按钮与画面遮挡是否能接受；可见按钮事件、时间／Camera行为及文字与Snapshot一致性由Agent核对。

反馈可直接写“visual 1／2／3 PASS”，或指出对应截图和想调整的位置。手机真机与Phase最终发布验证另有范围，当前截图是Windows Editor。

## 四组完整步骤（Agent功能代测记录）

以下保留M-007＋M-010、M-017／019／021的完整复现步骤供查证；Owner已委托Agent执行功能部分，本人只按上方三项看visual。每组独立Stop→Play，保持1×，运行中不要保存Scene。菜单位置见上方“验证组件菜单”。

### 第一组：点单转队、取餐和持杯（M-007＋M-010）

1. 打开`Phase14SharedPickup`，Game view选择`1080×1920`，Play后跟踪第一位客人。
2. Cashier点单完成、底部出现第一张Order后，连续看这位客人从CR走过柜角，到Pick-up等候位。预期在Barista仍`Working`时实际离开CR，不再在柜角停约3秒；等候区安全让路仍是正常行为。
3. 等Barista送杯并让出唯一服务侧，确认同一客人领取自己的杯子、持杯走向出口。此Scene只有一个合法侧，不要求穿过Barista或一定早于Barista回到CM领取。
4. Stop，换`Phase14CafeLoop`再Play。Pick-up前已有客人时，新付款客人先去队尾，再依次前移是正常FIFO；只允许队首领取。
5. 观察多侧可用时，客人不必等Barista回CM才拿走Ready的杯子。需要固定这个阶段时，先Hold Collection并积累一杯Ready，再Hold Arrivals；在Barista `Returning`时Pause，调用Block Current Return Route且Console确认成功，然后Resume Collection、恢复1×。预期客人能领取；最后Remove Test Blockers、Resume Blocked Flow。若操作时Barista已Idle，Stop→Play重做阶段，不把工具拒绝记为产品失败。该工具挡CM回岗目标，不证明员工身体仍占原交付侧。
6. Shiba和Westie各看一位：领取前空手，领取后手里只有一个杯子，Walk腿部与Carry上身自然；Pause时人物和动画都停止，恢复后继续走，离店后没有遗留手杯。

记录：Scene、分辨率、第一位客人是否在Working时离CR、杯子/动作是否异常；异常时Pause并Print Snapshot，保留Console文本或截图。

### 第二组：多设备和长柜台中间Slot（M-017）

1. Stop，打开MainCafe后Play，从空店Decor按上方“M-017：多设备与多Slot布局”表摆完五组支撑／设备。每件先绿色Preview并✓；第二CR用`(3,3)`，长柜台Pick-up放中间`slot.1`。
2. Done，确认有效布局自动1×开始。观察一位客人使用的CR、Cashier是否在同一台CR另一侧，Barista使用的CM在该任务中是否稳定；杯子应实际落在长柜台中间module。
3. Print Snapshot记录岗位及delivery支撑ID／Slot。重新进Decor，把长柜台移动或旋转到安全绿色位置，✓后Done。
4. 预期旧未领取订单／桌杯清除，新一单仍送到当前长柜台中间Slot；人物不穿家具。红Preview或明确身体保护拒绝先按提示调整，不能把未成立布局计为已测试成功。

### 第三组：横竖屏UI与启动Console（M-019）

1. Stop，打开Phase14CafeLoop。先看Console启动信息，确认不再出现之前截图那批`CafeLoopPlaySessionTests`的CS0618 warning；若有其他warning，记录完整文本。
2. Game view选`1600×720`，Play，依次操作Pause→1×→2×→1×，再进Decor、Done及移动Camera。确认按钮能点、底部文字可读，没有严重裁切／遮挡。
3. Stop，换`1080×1920`重复同样操作。用Print Snapshot核对可见杯数、Completed与容量显示；Console不应出现新Error／Exception或反复刷屏。
4. Stop，恢复原Game view尺寸。Editor横竖检查不能代替Android／iOS设备测试。

### 第四组：Done清场例外和主动Pause（M-021）

1. Phase14CafeLoop重新Play，尽早点Hold Collection，等至少两位已点单客人及Ready杯，再Hold Arrivals。
2. Resume Collection，看到一位客人已拿杯、仍往出口走，而另一位尚未领取时，立即进Decor。需要辨认身份可Print Snapshot；保留者必须是真正已领取的那位。
3. 在远离人物、入口和行走通道的绿色空位新增一个小桌并✓，再Done。这必须是已确认的真实修改，不能只打开Decor就Done。
4. 预期：未领取客人、全部旧订单和旧桌杯清掉；已领取离店者保持原人物、位置连续和手杯，继续正常走出店；两位员工保留身份，从当前位置回到有效岗位。Hold Arrivals仍开着时，用Resume Arrivals才会再生成客人。
5. Stop→Play另做一遍，但先主动点Pause，再进Decor、做合法真实修改并Done。预期Done后仍Pause，手动点1×才继续。若第一分支没赶上“一个已领、一个未领”的时刻，重新准备，不用改人物Transform。

这四组由Agent记录功能结果、原生截图与实际执行层；Owner仅记录上方三项visual，不把Agent结果自动填为Owner视觉PASS。

## 22项手测记录（Agent／Owner）

原快照Agent代测及后续修正补测：18项功能流程及4项客观检查均PASS。本次新增持杯已有Edit166/Play71直接回归，M-007两species普通Editor客观补测与正面可见手杯画面见`Carry-20261021-013509`；新的Pause/Blocked/disable/Done手杯生命周期由CC真实自动测试补证，不冒充重跑全部manual。M-006／011证据为`B-20261020-154541`；M-017／019为`G17-20261020-164313`。M-019旧启动异常由代测工具过早进入Play触发，现于EditMode通过Unity公开Search API完成真实默认索引首次初始化后才进入Play；当轮Application Console无Error／Exception／Assert，UI横／竖屏沿用未改UI的C组证据。历史失败／超时均保留，详见Validation Report；不表示Unity Search一般缺陷已被项目修复。Owner列仅记录Owner本人；M-007已反馈，其余尚未验收。

2026-10-21追加多侧普通Editor组`Sides-20261021-045308`：Ready时员工仍在North交付点，实际领取时同原lease仍在、员工距该侧.0283m（arrival .08m），顾客从East领取；Pause保留真实手杯，正常恢复后实际出口／原Total与全部容量释放。截图及同次状态见Validation Report；本组背向／HUD遮挡，不用它评价手杯动画观感。新增技术回归为最终Edit289P、Play130唯一case全部PASS；以下未重跑的原Agent22项保留历史证据，不全重标为本轮同源manual。

具体前置、操作、预期与复位均在 [Test Cases §5](superpowers/specs/2026-10-19-phase-14-integrated-takeout-loop-test-cases.md#5-manual-逐项步骤)；按下表执行并记录实际结果、Scene、Console／截图和复位情况。本轮Agent通过独立Editor场景操作脚本执行正式入口，记录操作和真实状态，非运行NUnit方法；界面事件由Editor工具触发，不声称OS鼠标／touch手测。M-007／017／019／021的Agent结论限客观状态与截图，最终动画观感／可读性／操作体验由Owner确认。自动NUnit PASS不能直接填入Agent或Owner列。

| ID | 检查重点 | Agent代测 | Owner验收 |
| --- | --- | --- | --- |
| M-001 | 空店有效Done后自动接待，两员工各一位 | PASS | NOT_RUN |
| M-002 | 一名客人从入店到实际离店，容量排空 | PASS | NOT_RUN |
| M-003 | 连续20单，ownership／队列／离店与Console | PASS | NOT_RUN |
| M-004 | Counter FIFO与P14 1游戏秒收银 | 旧2秒PASS保留；新1秒普通Editor三单FIFO／真实付款离场PASS，精确计时边界由最终自动化单列证明 | NOT_RUN |
| M-005 | FIFO；Barista占一侧时队首可用另一空侧 | 历史FIFO PASS；新增多侧普通Editor客观PASS | NOT_RUN |
| M-006 | 五次真实送达，最多三杯、正确补显示 | PASS（窄修复后真实恢复、补杯并排空） | NOT_RUN |
| M-007 | 点单即去Pick-up、员工送杯、顾客持杯离店 | 本轮Carry双species功能PASS；Shared修后同源竖屏证据复用 | 历史反馈FAIL已技术修复；功能委托Agent，仅visual待验 |
| M-008 | 各阶段Pause、1×、2× | PASS | NOT_RUN |
| M-009 | 送达前堵路，Prepared恢复不重新制作 | PASS | NOT_RUN |
| M-010 | Ready且员工仍占交付侧，顾客走其他空侧；单侧安全等待 | 本轮Sides功能PASS；单侧沿当前同源自动／原Shared证据 | 功能已委托Agent；无新增Owner功能手测 |
| M-011 | 容量／真实站位不足，腾空后有间隔恢复 | PASS（安全站位门禁；恢复后2.04游戏秒接待） | NOT_RUN |
| M-012 | 无修改Done／Cancel Preview保留进度与杯子 | PASS | NOT_RUN |
| M-013 | 已确认修改清场，保留真正已领取的离店者 | PASS | NOT_RUN |
| M-014 | Blocked的已领取离店者跨Done保留并实际退出 | PASS | NOT_RUN |
| M-015 | 连续Done及退休callback拒绝 | PASS | NOT_RUN |
| M-016 | 无效布局修复、自动恢复与主动Pause | PASS | NOT_RUN |
| M-017 | 多设备、多Slot、旋转及家具身体保护 | 本轮G17新GUID空Main／中slot／合法旋转功能PASS | 仅桌杯／柜台visual待验 |
| M-018 | Actor／协调组件停用、原位恢复 | PASS | NOT_RUN |
| M-019 | 只读UI与Snapshot一致、横竖Game view、Console | 本轮两尺寸UI事件／Snapshot／Camera功能PASS | 仅横竖UI visual待验 |
| M-020 | Domain Reload开／关各重复Play，恢复Editor原设置 | PASS | NOT_RUN |
| M-021 | Done门禁顺序、保留者位置连续、主动Pause | 本轮正常Done＋fresh主动Pause功能PASS | 功能已委托Agent；无新增Owner功能手测 |
| M-022 | 安装幂等、旧写入口隔离、独立P13仍可用 | PASS | NOT_RUN |

横竖检查在Game view分别选 `1600×720` 与 `1080×1920`，操作时间、Decor与Camera，完成后恢复原Game view尺寸。Editor截图仅是代表状态；Android、iOS、Player与设备安全区尚未验证，不能用Editor结果代替。
