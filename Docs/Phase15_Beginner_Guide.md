# Phase 15 自动营业周期：Beginner Guide

> **当前 P15 开发验收已通过，等待 PR。** Main 自动营业、真实闭店排空、Pause／2x 与现实周期分离，以及停止自动来客时释放未出生申请，已纳入最新精简 regression。EditMode 1388/1388 PASS，PlayMode 90/90 PASS，P15 自身 77 项 PlayMode 全通过。客观通过记录与 Owner 观感／移动设备验收分别保存，具体范围见[Validation Report](Phase15_Validation_Report.md)。下方旧状态保留各自时点。

## 0. 合并后按顺序测试（8 项）

使用 Unity 6000.5.5f1 打开当前项目。每项先 Stop，再双击指定 Scene，按顶部 Play 开始。打开 Window > General > Console，清空旧记录后记录本项新增 error。只在 Play 内操作，结束后 Stop。

前两项使用 Assets/Scenes/MainCafe.unity。第 3–8 项使用 Assets/Scenes/Validation/Phase15BusinessDay.unity，其营业 60 秒、休息 15 秒。Main 的正式配置为营业 8 小时、排空后休息 2 小时。

需要验证工具时，在 Hierarchy 搜索 t:CafeLoopValidationController，选择带该组件的对象。在 Inspector 该组件右上角菜单中展开 P14，点击下文所列操作。

### 1. Main 的玩家界面和正常开店

1. 打开 MainCafe 并 Play。看浅色营业状态卡及 readiness 提示。
2. 点击游戏内 Decor。将收银台支撑柜和 Cash Register 放在 (1,5)，朝向 0°，咖啡机支撑柜和 Coffee Machine 放在 (4,6)，Pick-up 支撑柜放在 (6,6) 并指定 Pick-up Point。每件点击 ✓ 确认，完成后点 Done。已有可营业布局也可以直接沿用。
3. 确认 CR、CM、Pick-up 都 Ready，镜头缩放和 UI 按钮能响应，顾客自动入店。
4. 将 Game 窗口分别设为 540×960 和 960×540，观察状态卡、按钮和长提示。
5. 通过条件是三项 Ready 后自动营业，玩家界面清晰可读。Main 的浅色状态卡显示正式周期，黑色详细诊断属于 Validation。

### 2. Main 连续服务和 Pickup

1. 沿用第 1 项布局，以游戏内 1x 运行至至少 3 位顾客完成点单、走向 Pickup、取杯并实际离店。
2. 点游戏内 2x，再观察约 2 分钟。
3. 看后来的顾客能否继续点单和进入 Pickup 等候，Barista 是否制作、送杯、返回，Cashier 是否继续接单。
4. 通过条件是持续服务和真实离店。杯子尚未 Ready 时，顾客可以先前往安全的 Pickup 等候位，实际领取仍等杯子 Ready。看到“等待通路腾空”时，继续观察前客移开后流程是否恢复。

### 3. 自动 Closing、Rest 和下一天

1. 打开 Phase15BusinessDay 并 Play，等待验证布局自动准备好。
2. 观察 Day 1 Open。约 60 秒后进入 Closing，记录当时已有顾客。
3. 等已有顾客取杯并实际离店、员工回位。随后应进入 Rest，倒计时从完整 15 秒开始。
4. 等 Rest 结束，应自动进入 Day 2 Open 并重新接客。再观察一次跨日。
5. 通过条件是自动循环，Closing 期间停止新客，Rest 在真实排空后开始。

### 4. 游戏内 Pause 和 2x

1. 在 Validation 的 Open 阶段点击 Game 窗口里的 Pause，现实等待 5 秒。
2. 观察人物暂停，营业倒计时仍减少约 5 秒。点击 1x 恢复，再点击 2x 观察工作加快。
3. 在 Rest 阶段再点一次游戏内 Pause，观察现实休息倒计时。
4. 通过条件是场景工作服从游戏速度，营业／休息周期服从现实时间。此项使用游戏 HUD 的 Pause。

### 5. Closing 真的等待已有顾客

1. 在 Validation 中确认已经有人点单，使用 P14 > Hold Collection。
2. 等到 Closing。使用 Print Snapshot 查看已有订单和顾客，现实再等约 5 秒。
3. 确认仍为 Closing，并显示尚未完成的服务原因。
4. 使用 Resume Collection，等待原顾客取杯、实际离店和员工回位。
5. 通过条件是原顾客完成后才开始完整 15 秒 Rest。

### 6. 停止和恢复自动接客

1. 在 Validation 的 Open 阶段，等几位顾客点单、员工开始工作。
2. 使用 P14 > Hold Arrivals，随后使用 Print Snapshot，记下当时已在店里的顾客。
3. 观察这些顾客继续得到服务、取杯、离店，员工完成送杯和返程。停止新客期间，已有经营应能排空。
4. 若仍处于 Open，使用 Resume Arrivals，应重新自动接客。若已到 Closing／Rest，等下一次 Open 再观察。
5. 通过条件是停止新客后旧顾客能完成服务，恢复后正常接客。此项覆盖本轮修复的未出生申请残留预留风险。

### 7. 装修取消与真实装修中断

1. 在 Validation 有客人时点 Decor，只查看或建立 Preview 后 Cancel，再点 Done。观察原经营继续。
2. 再点 Decor，实际移动一个支撑柜到合法位置并点 ✓，最后 Done。保持 CR、CM、Pick-up 有效。
3. 观察上轮结束原因出现装修中断／LayoutInterrupted，随后在具备接待条件的 Open 阶段恢复营业。
4. 通过条件是取消 Preview 保留原业务，实际布局修改记录装修中断，并能继续下一轮经营。

### 8. 真实堵路和恢复

1. 在 Validation 等 Barista 正在送杯时，使用 P14 > Block Current Delivery Route。也可以选择员工返程时的 Block Current Return Route，或持杯顾客离店时的 Block Current Exit Route。
2. 查看 Console 的工具反馈。若提示没有当前 route，本次尚未制造堵路，等相应动作实际发生后再操作。
3. 出现真实 Blocked 后使用 Print Snapshot，记下订单和原因。到 Closing 时应继续显示尚未排空。
4. 依次使用 Remove Test Blockers、Resume Blocked Flow，观察原顾客完成取杯／实际离店，员工返回。
5. 通过条件是清除实际障碍并显式恢复后原业务能完成，随后开始 Rest。

每项记下 PASS／FAIL、Scene、操作步骤和新增 Console error。这 8 项的本人人工结果仍由实际观察填写。原旧离店交接专项继续保留 NOT_PROVEN，短周期测试记录覆盖本次观察的周期。

> **历史阶段记录（2026-10-31）：统一服务交通重构仍在开发，暂未进入最终手动验收。** 一轮定向测试为 Edit702 PASS、实际运行24 PASS／2 SKIP／0 FAIL；这只验证已接入的员工恢复与部分顾客流程。服务站位、闭店排空、其余布局及 Main 完整操作仍需继续开发和测试。下方 Main FAIL 与旧手测是历史源码记录，不能当作当前重构的完整结论。当前范围和原始结果见[Validation Report](Phase15_Validation_Report.md)；正式8小时营业／2小时休息规则不变。

> **最新Main补测（2026-10-30本地）：完整经营FAIL。** 正常目录装修后三项设备Ready，Done能自动进店，但首单送杯阶段Barista为`Blocked / IncompletePath`，Completed=0。Pause／2x／镜头仍响应，现场保留在Play＋游戏Pause，没有改代码或保存Scene。原因尚未查明；历史整局无响应按Owner要求搁置。旧相关suite与Validation通过记录保持其范围，P15未完成。见[Main补测报告及截图](../outputs/phase15/reservation-rule-20261029-01/manual-main-complete/report.md)。

> **此前相关suite与Validation窗口（2026-10-30）：PASS，不覆盖上方Main失败。** 当前同源相关Edit387PASS、Play299PASS／0FAIL／6项明确opt-in SKIP；八种长柜台各20次真实离店、容量归零，原失败照片六节1x本次通过。普通Editor在真实中央柜台装修后连续618.975秒运行至Day7／Completed66，真实Rest排空与自动重开、Pause／1x／2x／Decor／镜头／横竖屏响应正常，Console0warning／0error。此次没有重做真实堵路恢复manual7，旧源码记录与当前Native正反例分开；历史整个Game输入失效首因仍UNKNOWN。下面旧失败保留，具体结果与剩余Owner／CI／Git等门槛见[Validation Report](Phase15_Validation_Report.md)和[当前GUI报告](../outputs/phase15/reservation-rule-20261029-01/manual-detour-final/report.md)。

> **此前修复与手测（历史源码快照，2026-10-30）：仍未修完。** 当时相关Edit387项通过，Play288项通过／0失败／2项原有诊断跳过，原八组合全部通过；随后普通Editor在装修后发现Day5 Closing停住，已撤回短时健康判断。当时第6项通过、第7项尚未注入堵路、第8项失败。详见[历史手测记录](../outputs/phase15/reservation-rule-20261029-01/manual-ui/report.md)及[本轮记录](../outputs/phase15/reservation-rule-20261029-01/report.md)。历史整个Game输入失效首因仍UNKNOWN，不能由本次限定窗口通过推断已解决。

> 工作区：`E:/Unity/Project/AnimalCafe/.worktrees/phase-15-business-day`；Unity `6000.5.5f1`。原批准test cases的M01–M04均在Validation Scene，本轮已Agent补测PASS；结合原M05出口、M06、M07正常UI，原七项均有Agent通过记录。后续Guide扩展的Main正常装修路径和完整付款集合对照仍未完成。最新展示修正Edit59 PASS、定向Play122 PASS／0 FAIL／1原有SKIP；旧全量Edit2832／Play1445＋5 SKIP属HUD修复前source。Debugger异常仍UNKNOWN，Owner观感／device／CI／VCS未验收，不宣告Phase closure。见[Agent报告](Phase15_Agent_Playtest_Report.md)及[Validation Report](Phase15_Validation_Report.md)。

例如：营业时间到了，一位客人还没付款，另一位正在等咖啡。咖啡厅先停止接新客，继续服务这两位；客人真正走出门、员工回位、订单与容量排空之后才休息。休息结束自动进入下一天，不用点 Start、Next Day 或确认日报。

正式配置是现实营业 **8 小时（28800 秒）**，实际排空后休息 **2 小时（7200 秒）**。Closing 需要多久就等多久，八小时不包含它。Pause／2x 会暂停或加快 Scene 内制作、走路与动画，但营业和休息倒计时继续按现实时间走。进入装修同样不停止周期。

## 1. 打开与选择 Scene

1. 用 Unity Hub 打开上方 worktree，选择 Editor `6000.5.5f1`，等编译完成。一次只开一个本项目 Editor。
2. 先保存自己的 Scene 修改，再打开下表中的 Scene，按顶部 Play。观察 Console 原有 warning 与本次新增 error，分别记录。
3. 每项独立验证可 Stop→Play 重新开始；不要保存运行中的 Scene 修改。

| Scene | 用途与预期 |
| --- | --- |
| `Assets/Scenes/MainCafe.unity` | 正式 28800／7200 秒，无 Validation 标记。保留原始未就绪布局：只有 starter counter，没有完整 CR／CM／Pick-up。周期可显示 Day 1 Open，但 readiness 必须拒绝来客。 |
| `Assets/Scenes/Validation/Phase15BusinessDay.unity` | 独立短时 Scene，Open60秒／Rest15秒，HUD 明确显示 Validation；Play 时使用既有 P14 验证布局准备流程。便于手测整个循环。 |
| 原 P14 Scene | 保留既有无 P15 行为。不要用旧 P14 安装／重建菜单覆盖已接入 P15 的 MainCafe。 |

MainCafe 完成正常装修后才可接待：点 Decor，按 [P14 Guide 的分离布局](Phase14_Beginner_Guide.md#验证布局与五个实际等候位) 放置 CR支撑／CR `(1,5)` 0°、CM支撑／CM `(4,6)`、Pick-up支撑 `(6,6)`，每件先 ✓，最后 Done。保留员工工作位、客人排队位与进出通道。有效且未主动 Pause 时会自动接待，不需额外 Start；布局不完整时按 readiness 原因修正，不要打开 seedValidationLayout。

已有 Editor 菜单 `Tools > AnimalCafe > Phase 15` 提供 `Create Business Day Validation Scene` 和 `Install Business Day in MainCafe`。接线已生成，普通观察不必再运行。确需重装时先退出 Play、保存所有已加载 Scene；dirty Scene 会明确拒绝，不静默覆盖。

## 2. 怎样读状态

MainCafe 采用现有 P5 玩家 UI：浅色状态卡显示 Day、营业阶段、倒计时和必要提示，readiness 提示继续说明布局问题。黑色 P14／P15 诊断面板仅供 Validation Scene；它显示 Epoch、订单 ID、任务状态等开发信息。2026-10-27 展示分离修正已通过限定自动回归、独立review与真实Editor UI检查；详见[Validation Report第17节](Phase15_Validation_Report.md#17-main-玩家界面与-validation-诊断分离2026-10-27)。旧黑色Main截图仅作历史。

![Main玩家状态卡](../outputs/phase15/player-hud/ui/main-portrait.png)

| 显示 | 含义 |
| --- | --- |
| `Day / Open` | 计划营业阶段；实际接待还须通过 readiness、玩家 Pause、路径和容量检查。Main 看玩家 readiness 提示；Validation 可同时看 P14 service HUD 的诊断原因。 |
| `Closing` | 已停止所有新接待；继续服务已有客人。显示排空停止原因，无固定清场时长。 |
| `Rest` | 实际排空后的休息阶段；不接新客，倒计时结束自动开始下一天。 |
| 装修中断／`LayoutInterrupted` | Main 用玩家语言保留上轮装修中断事实；Validation 保留诊断枚举与次数。真实装修确认按 P14 合同清场，不能算所有客人正常服务完成，也不代表退款。 |
| 上轮营业／`Last closed day` | 上一次结束原因，进入下一 Open 仍保留；Main 使用玩家语言，Validation 保留详细诊断。 |

历史 Completed Orders 和付款事实不会每天清空。P15 没有 Coins、Revenue 或 Daily Report 正文；Stop／重新 Play 会重新建立 Day 1 foundation，没有 Save，也没有退出后的 Offline 收益。未来 P17／P18A 才接持久化与离线恢复。

## 3. 短周期操作与现有验证工具

在独立 P15 Scene Play 后，Hierarchy 选带有 `CafeLoopValidationController` 的业务对象。在 Inspector 组件右上角菜单使用既有 `P14/...` ContextMenu：`Hold Arrivals`／`Resume Arrivals` 暂停／恢复自动来客，`Hold Collection`／`Resume Collection` 保留／恢复领取，`Print Snapshot` 在 Console 查看实际订单、员工和队伍。

需要受控堵路时，只使用已有 `Block Current Delivery Route`、`Block Current Return Route` 或 `Block Current Exit Route`。时机不符会反馈没有当前 route；这不等于已制造 Blocked。观察真实故障后，依次 `Remove Test Blockers`、`Resume Blocked Flow`，确认原订单恢复。不要拖动 Actor、伪造 Completed 或直接清空订单。

## 4. Owner 手测记录（全部待执行）

每项填写：**日期／Editor与Scene／观察结果／Console新增error／截图或log路径／PASS或FAIL**。未做保持 `NOT_RUN`；Agent 自动测试不能替本人填写。

用户现已委托Agent执行可代测部分；[Agent实测报告](Phase15_Agent_Playtest_Report.md)独立记录真实Editor UI操作。它保留初轮M07 FAIL及HUD修复历史，并记录本轮M01–M04 Validation PASS；原批准七项均有Agent通过记录，扩展检查缺口单列；下表的`NOT_RUN`仅表示Owner本人尚未执行，不表示Agent没做，也不把既有自动测试改叫manual。当前先处理实测问题与缺口，再安排必要的Owner观感确认。

M04请使用 **Game窗口内游戏HUD** 的 Pause（`||`）、1x、2x（`>>`）按钮。Unity Editor顶部工具栏的Pause会暂停整个Editor，不能用来验证本条游戏内Pause与现实周期的关系。

| ID／当前结果 | 具体操作 | 应观察与记录 |
| --- | --- | --- |
| P15-M01 `NOT_RUN` | 打开独立 P15 Validation Scene→Play，等待布局准备。 | 短时Validation标记清楚，自动经营，无Start。Main额外路径见下方补充检查。 |
| P15-M02 `NOT_RUN` | 短时Scene等到60秒打烊，观察原先已有顾客；可用游戏Pause或Hold Collection保留业务后恢复。 | Closing停新客，已有顾客继续服务、领取、持杯真实离店，员工返回后才Rest。身份与停止原因可用Print Snapshot辅助。 |
| P15-M03 `NOT_RUN` | 用Hold Arrivals控制排空，观察Rest15秒结束；连续观察两个跨日边界。 | 阶段／倒计时／Day可读，无Next Day／Report操作；员工／订单身份为补充取证，完整付款集合见下方扩展检查。 |
| P15-M04 `NOT_RUN` | 分别在Open、Closing和Rest点Game窗口游戏HUD的Pause（`||`），现实等待约5秒；再用同一HUD恢复1x、切2x（`>>`）各观察约5秒。不用Editor顶部工具栏Pause。 | Scene动作暂停／加速，周期仍按现实秒走；Closing有未完成业务时Pause不能跳过排空；自动下一Open不解除主动Pause。记录两套时间。 |
| P15-M05 `NOT_RUN` | 在Barista正送杯／返程或顾客离店时调用对应Block工具；等待真实Blocked与Closing。Print Snapshot，清除测试blocker，再Resume Blocked Flow。 | Blocked原因可读；不会瞬移或假完成；保留原付款／订单，恢复并真实排空后才开始完整15秒Rest。 |
| P15-M06 `NOT_RUN` | 先Decor只查看／创建Preview后Cancel→Done；另一次在Closing确认合法实际移动或摆放，再Done；必要时Replay Last Business Callback。 | 前者不中断；后者记录一次LayoutInterrupted，保留旧清场安全规则、不假装CompletedDrain；旧callback不能污染新业务；下一Open仍能看到上日结束原因。 |
| P15-M07 `NOT_RUN` | Game窗口分别设540×960与960×540，观察Open、长原因Closing、Rest以及上一日中断信息；试点原Pause／2x／Decor并查看Console。 | 字体可读、关键文字未裁切、现有按钮可用、无关键遮挡与新增未处理error。重点复看长Closing横屏的整个活动UI区域；该项仍为Minor/polish观察点。Editor结果不能代替手机。 |


### 4.1 原批准范围与补充检查

M01–M04原批准范围以[Test Cases第4节](superpowers/specs/2026-10-26-phase-15-business-day-test-cases.md)为准，全部在独立Validation Scene。此前Guide把以下更严格的集成／取证扩展混入原四项PASS门槛，现予以分列，保留未完成事实，不把范围澄清当补充项已通过：

- MainCafe正式28800／7200、原始readiness与正常目录完整布置已观察，Done自动进店PASS；本次摆法的完整经营FAIL：首单Barista Prepared后IncompletePath、Completed=0，未完成取餐／离店。采用默认落点与旋转，并非上述Guide分离布局的坐标复验；未开启验证seed或API注入，详见最新Main补测报告。
- M02／M03完整订单key／owner及员工ActorId：本轮已取得三次原生Print Snapshot并核对；完整PaidVisitIds前后集合仍NOT_CAPTURED，现有工具未输出，不能以订单或源码推断冒充直接证据。
- M04每阶段各1x／Pause／2x约5秒：本轮同步画面矩阵已完成，原来误标的早期采集不计入；详情见Agent报告。

上表Owner本人仍NOT_RUN；用户委托的客观Agent验证已另记，不要求Owner重做全部客观步骤来冒充其本人观感验收。

## 5. 真实默认与验收边界

短时 Scene 和注入时钟测试只证明边界逻辑；**真实8小时营业＋Closing＋2小时Rest连续运行、Windows Player、Android／iOS真机、应用挂起恢复、CI均 `NOT_RUN`**。退出后 Offline 收益尚未实现。最终部门 review、Owner手测、CI／merge与merged-main验证须按项目流程记录，不能仅凭本地自动测试将P15写成正式Completed。

## 6. 本地完成后的Owner验收与交付

本节是后续操作说明，不代表这些操作已执行。最终本地结果以Validation Report最新收尾节为准；本地测试通过后，仍须由Owner确认观感并安排CI／仓库合入。

1. 在Unity Hub打开 `E:/Unity/Project/AnimalCafe/.worktrees/phase-15-business-day`（branch `codex/phase-15-business-day`），使用Unity6000.5.5f1。MainCafe是正式玩家UI；短时周期检查使用独立 `Assets/Scenes/Validation/Phase15BusinessDay.unity`。原M01–M07已有Agent代测，无须为了流程重复整套；请Owner实际判断当前界面的可读性和操作感。
2. Owner在GitHub Desktop检查P15源码、tests、配套 `.meta`、Scene和Docs，再自行commit／push。建议commit说明：`Phase 15: automatic business days and safe service routes`。`.gitignore`仅新增 `/outputs/phase15/`，本地raw XML、日志、截图和恢复备份仍在磁盘；它们不随该规则纳入提交。
3. 需要正式CI时，由Owner在仓库Actions选择 **Unity tests** → **Run workflow**，选P15 branch，`test_scope=full`；只有本次同意Unity账号登录及两套测试时，才勾选 `allow_unity_login`。这是既有手动workflow，Codex未触发，也不因本地PASS代替登录授权。
4. CI结束后核对真实EditMode／PlayMode XML及测试commit。Owner决定PR／merge；实际合入后再核对merged-main，才按项目规则更新正式Completed。未完成的Owner接受、CI、merge不填为PASS，不自动进入P16。

8小时实钟／Player／Android与iOS设备、挂起恢复继续单列NOT_RUN，不新增为本次Windows Editor本地技术验收门槛；退出后Save／Offline收益不在P15实现范围。
