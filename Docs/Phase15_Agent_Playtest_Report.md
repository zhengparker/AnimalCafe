# Phase 15 Agent 代测记录（2026-10-27）

用户授权代测后，Agent 使用真实 Unity Editor UI 操作 MainCafe 与独立 P15 Scene。以下是 **Agent 实测**，不冒充 Owner 本人试玩或手机验收。初轮未修改产品／测试源码、Scene、prefab 或 meta；随后经用户批准执行下文三文件HUD修复。未执行 CI／Git 交付。

环境：worktree `E:/Unity/Project/AnimalCafe/.worktrees/phase-15-business-day`，branch `codex/phase-15-business-day`，HEAD `f55d4eb685f32e4dcc30fb64b753b45b695a6a3b` 加当前未提交实现，Unity `6000.5.5f1`，Windows Editor Game View。Validation 使用真实60／15秒，Main使用28800／7200秒；没有注入时钟或把验证布局注入 Main。

## 当前结论：原批准 M01–M07 已有 Agent PASS；Guide 扩展检查仍有缺口

2026-10-27 HUD修复后，按用户要求补测M01–M04。当前按[原批准test cases第4节](superpowers/specs/2026-10-26-phase-15-business-day-test-cases.md#4-scene--owner-manual)记录：M01–M04 Validation PASS，结合此前M05出口分支、M06、M07正常UI PASS，原七项均有Agent通过记录。后续Guide追加的Main正常装修开店和直接付款集合对照仍未完成，不能将它们写成PASS。下面保留初轮和HUD修复时点的3／7历史；本轮证据见末节。Debugger异常仍UNKNOWN，Owner观感和device仍未验收。

> 最新展示分离修正已实现并完成限定回归／独立review／真实UI检查，详见末节与Validation Report第17节。Main采用P5玩家卡，Validation保留黑色诊断。已有七项PASS仅对应各自采集源，未声称新源完整重跑七项。

## 初轮逐项结论（修复前历史）

`PARTIAL` 表示已观察部分 PASS，尚有步骤／身份取证未完成；不计作整项 PASS。Owner 本人 M01–M07 仍未执行。

| 项目 | Agent 结果 | 实际覆盖与缺口 |
| --- | --- | --- |
| M01 | PARTIAL | Validation 自动准备并接待，无 Start；Main Inspector 确认28800／7200、Validation及seed均关闭，原始单柜台布局零客人且readiness拒绝。正常Decor可创建／确认默认绿色柜台；拖动未生效，完整CR／CM／Pick-up布局→Done→自动来客未完成。 |
| M02 | PARTIAL | Day1截止时保留9位客人，Closing停新客，恢复动作后逐单完成，最后一位已取餐客人仍在店时不会Rest；真实离店且双方员工Idle、Total0才Rest15。原始顾客／付款／订单的完整前后ID对照未保存，不补写身份断言。 |
| M03 | PARTIAL | 连续看到Day1→2→3，以及后续Day4→5自动转换，无Next Day；普通跨日Epoch1不变，Completed15→33累积。员工外观／岗位保持；ActorId和付款集合逐项对照未捕获。Day3实际装修换Epoch另列M06，不能当普通跨日历史保留证据。 |
| M04 | PARTIAL | 游戏HUD Pause贯穿Open截止→Closing，动作／队列保持，不能跳过排空；Rest在Pause期间仍结束，Day2自动Open继续Pause；2x下营业倒计时按现实秒、场景工作继续。横屏Closing的Pause／2x及Rest2x、后续Open保留Pause可见。没有完整逐格记录Open／Closing／Rest各1x／Pause／2x均约5秒，整项不冒称全覆盖。 |
| M05 | PASS（出口分支） | 创建本次拥有的出口blocker后，实际离店移动超时，Day1持续Closing，Total1、Completed15、双方员工Idle；Remove Test Blockers→Resume Blocked Flow后原已取餐客人真实离店，Completed仍15，Total0后才开始Rest00:15。返程blocker首次安全拒绝，不算制造成功；送杯／返程两分支未另测。 |
| M06 | PASS | Preview→Cancel→Done保持Epoch1、LayoutInterrupted0、原队列与历史；Day3 Closing期间确认绿色合法柜台并Done，换Epoch2、只记一次中断；实际余客离店后Last Day3为LayoutInterrupted(1)，不伪记CompletedDrain。Day4 Open仍显示上日中断；Replay Last Business Callback明确source=old-epoch，未产生新订单／杯子或重复中断。 |
| M07 | FAIL | Validation在固定540×960、960×540下Open／长Closing／Rest／上日中断文字均在面板内，Pause／2x／Decor可点；但正式Main未就绪时，Setup incomplete提示遮住周期HUD，横竖屏均复现。Console异常另列下节，不能声称整轮零error。 |

## 确认的问题与未定原因

1. **P15-MANUAL-UI-01：Main未就绪提示与周期HUD重叠，FAIL。** 540×960遮住`Business day / Open8h / Rest2h`首行；960×540还遮住Day／Open信息。无需移动家具或调试器即可复现：原始Main→Play→观察未就绪提示。Validation布局已就绪时没有此提示，不能用其画面替代Main验收。需最小HUD避让修复，再复测两Scene、两尺寸、ready／unready／Decor／长Closing及原按钮。
2. **Editor调试器期间异常：原因UNKNOWN，保留调查。** 在分辨率下拉操作中误打开Frame Debugger，Editor被暂停；17:48:43 Console出现一条Unity `dt > 0` assertion和两条`ArgumentOutOfRangeException: scaledDeltaTime`。已读的一条调用栈为`CafeLoopController.Step:167 → Update:163`；源码确实拒绝负值／NaN／Infinity。关闭Debugger并恢复Editor后玩法继续。这只建立时间关联，未采到实际delta值及另一条完整栈，不能断言Unity单独负责、P15回归、已修复或可忽略。该区间不计入游戏内Pause的计时证据。Main后续独立Play期间Console显示0／0／0。
3. **Main鼠标拖动／目录滚动未完成：原因UNKNOWN。** 原生UI按钮、绿色默认摆放和Cancel可用；多次从预览／基底拖动未改变落点，目录滚轮尝试改变了视角而未滚出设备。没有修改输入代码来迁就工具，也没有借seed／直接API布置来冒充正常手测。默认柜台确认可用，不等于完整开店路径通过。

只读独立复核已定位UI原因：`BusinessDayStatusView.cs:49–61`与既有`ValidationMessageView.cs:573–612`都紧跟topControls下沿。建议待用户确认后，仅让P15 View避让同一UI根下当前可见的readiness实际Rect；不改P8R提示、Scene配置、时间按钮或营业规则。预计修改`BusinessDayStatusView.cs`及P15的`BusinessDayMainSceneTests.cs`、`BusinessDayValidationSceneTests.cs`，增加双尺寸实际矩形不重叠与可见性切换回归，再真实UI复测。这是修复前建议；该三文件范围现已批准、实施并复测，结果见末节。Debugger异常继续独立只读调查；0 delta在当前合同中合法，不能为规避症状删除非法delta校验或吞异常。

## 初轮原始证据与当时结论（修复前历史）

所有未编辑截图与操作事件在 [agent-manual](../outputs/phase15/agent-manual/)；最终清单为 [events-final.json](../outputs/phase15/agent-manual/events-final.json)。关键截图：

- [Open Pause](../outputs/phase15/agent-manual/m04-open-paused-1793137076475.png) → [Closing仍保持队列](../outputs/phase15/agent-manual/m04-closing-paused-1793137157921.png)。
- [真实出口堵塞](../outputs/phase15/agent-manual/m05-actual-exit-blocked-1793137549632.png) → [恢复后排空才Rest15](../outputs/phase15/agent-manual/m02-m05-recovered-rest15-1793137601560.png) → [次日仍Pause](../outputs/phase15/agent-manual/m03-m04-next-day-keeps-pause-1793137619194.png)。
- [Preview取消不中断](../outputs/phase15/agent-manual/m06-preview-cancel-no-interruption-1793137662761.png)；[Closing实际装修中断一次](../outputs/phase15/agent-manual/m06-closing-layout-interrupted-once-1793137800203.png)；[下一Open及旧callback](../outputs/phase15/agent-manual/m06-next-open-retired-callback-safe-1793137826821.png)。
- [Validation横屏长Closing](../outputs/phase15/agent-manual/m07-landscape960-long-closing-1793137888686.png)；[Main正式配置及横屏遮挡](../outputs/phase15/agent-manual/m01-main-formal-config-1793138005679.png)；[Main竖屏遮挡](../outputs/phase15/agent-manual/m07-fail-main-readiness-overlap-portrait-1793138019941.png)。
- [调试器期间异常栈](../outputs/phase15/agent-manual/editor-debugger-exception-stack-1793137764300.png)；[Main结束后Console及非Play状态](../outputs/phase15/agent-manual/main-session-console-zero-and-stopped-1793138275629.png)。

退出Play后，[Assets逐hash核对](../outputs/phase15/agent-manual-assets-parity.json)为测试前3172／后3172、差异0。没有保存Play中的装修变化；Editor停在Main的非Play状态，无Scene dirty标记。没有找到本次启动请求的独立`agent-manual-editor.log`，不引用旧Editor.log作为本轮原生日志；以UI截图及事件为证据。

此前完整Edit2832 PASS和独立Play1445 PASS／5 opt-in SKIP保持各自原receipt；此次manual FAIL不会被自动测试抵消。**当前手测未收口，不具备正式Phase closure／merge readiness。** 需修复HUD遮挡、调查或明确处置Debugger异常，并补完表内缺口。Owner视觉喜好、实际8小时＋排空＋2小时、Player／真机／挂起恢复、CI及Git交付仍未验证。

## HUD 修复及正常 UI 复测（2026-10-27）

本节时点为 **3／7 PASS**：M05出口分支、M06、M07正常Editor UI；当时M01–M04按扩展Guide仍为PARTIAL。初轮M07 FAIL已由以下修复与复测解决，不覆盖Debugger UNKNOWN。最新原批准范围与扩展检查的分列见末节。

只修改BusinessDayStatusView.cs和两份P15 Scene测试：周期HUD避让同一UI根下可见readiness实际边界，保留8单位间距；隐藏后释放，Decor沿用隐藏共享HUD，Done恢复。不改P8R、Scene、prefab、meta、时间规则或游戏流程。实现细节和hash见[修复报告](../.superpowers/sdd/2026-10-26-phase-15-business-day/manual-hud-repair-report.md)，[独立复核](../.superpowers/sdd/2026-10-26-phase-15-business-day/manual-hud-task-review.md)Spec／quality PASS，Critical0／Important0／Minor0。

当前源定向PlayMode：[原始XML](../outputs/phase15/manual-hud-green3.xml)123项，**122 PASS／0 FAIL／1原有opt-in SKIP**；P15为70 PASS。CLI0、native log0，OS handle exit UNKNOWN。原RED两项重叠FAIL及中间33／1 FAIL运行均保留。此前完整Edit2832 PASS、完整Play1445 PASS＋5 SKIP是HUD修复前source的receipt，本次没有重新完整suite，不能冒充当前源全量通过。

真实Windows Editor未注入时钟或伪造snapshot，操作两Scene及540×960／960×540：

- Main原始未就绪提示与正式8h／2h、Day／倒计时均分开可见；两尺寸Decor→Done后HUD恢复，readiness仍拒绝来客。
- Validation真实Open、长Closing原因、Rest均在面板内；Pause／2x／Decor实际可点击。Day2 Closing确认默认绿色柜台后LayoutInterrupted1、Epoch2；恢复2x后真实排空才Rest00:15，Day3自动Open仍显示上日LayoutInterrupted1，两尺寸历史均完整。横屏Rest使用Day1 CompletedDrain，竖屏Rest使用Day2 LayoutInterrupted，未声称捕获横屏中断Rest。
- 本次正常路径Console warning／error均0；定向日志未找到NullReferenceException、ArgumentOutOfRangeException、dt assertion或C#编译error。退出日志另有mono abort_threads、debugger-agent端口监听及hubIPC shutdown诊断，未定位，不能由此声称原生资源完全关闭；Sky随后确认本次Editor窗口消失。原Debugger期间异常与拖动／目录滚动UNKNOWN继续保留，没有修复或重试其代码。

未编辑截图与逐条记录见[新增证据events-final.json](../outputs/phase15/manual-hud-ui/events-final.json)。Stop后未保存运行中装修，Scene无dirty标记；[修复源之后至UI结束Assets核对](../outputs/phase15/manual-hud-ui-assets-parity.json)3172／3172、增删改0，三份修复源码hash与GREEN3一致。

接下来补M01–M04缺口与Debugger问题的明确处置，Owner观感、真实8h＋排空＋2h、Player／device、CI／VCS仍未验证。当前不宣布正式Phase closure或merge readiness，不进入P16。

## M01–M04 Validation 补测（2026-10-27）

### 范围澄清与计数

用户问“这只是validation scene对吧”后，重新核对原批准test cases第4节：M01是独立Validation自动开店，M02是截止后服务原客并实际离店，M03是连续两次自动跨日，M04是三阶段Pause／2x与独立周期。Main正常装修开店、完整付款集合逐项输出是后续Guide扩展检查，并非原四项的逐项批准门槛。先前将这些扩展缺口直接写成M01–M04 PARTIAL，范围混用；这里明确区分，没有把扩展项补写成已通过。

**原批准M01–M04：4 PASS。** 加上此前M05出口分支、M06、M07正常UI通过，原七项均有Agent PASS；这是多轮保留的证据，未声称本轮重测M05–M07、Owner个人接受、device、Phase closure或零历史异常。按旧Guide扩展口径，Main完整布局、原始PaidVisitIds集合对照仍有缺口。

### 本轮实测

| 原批准项 | 结果 | 本轮实际证据 |
| --- | --- | --- |
| M01 | PASS | 原生Editor打开独立Phase15BusinessDay后Play；HUD明确Validation Open60s／Rest15s，fixture自动准备，1x自动服务，无Start。 |
| M02 | PASS | Day1 Open00:06已有9客、Completed6、订单7 Preparing及8 Waiting；游戏Pause经过截止进入Closing，9客与原订单未清理。恢复1x／2x后原客依次完成，最终15单Completed、Total0、双方员工Idle后Rest。完整订单key／owner在三次PrintSnapshot和对照JSON中保留；不把新付费订单生成误当新顾客入店。 |
| M03 | PASS | Day1→Rest→Day2，Day2→Rest→Day3自动转换，无NextDay／日报操作；后续继续至Day6。Day2／Day3相同15条Completed key／owner、相同Cashier／Barista ActorId，Epoch1。HoldArrivals仅控制后续来客，不重建业务；第二周期为空，正常排空可直接进入Rest。 |
| M04 | PASS | 采用游戏HUD，Open／Closing／Rest各1x／Pause／2x的实际画面与五秒观察；Open及Rest倒计时继续按现实秒，Closing有业务时Pause不能跳过排空，恢复后动作继续，次日不自动取消Pause。详见下表。 |

| 同步观察 | 实际五秒前后结果 |
| --- | --- |
| Open 1x | Day3 00:37→00:32；使用confirmed-before／after，不采信旧文件名错误的pair。 |
| Open Pause | Day1 00:34→00:29；Total6、Completed2、原队列与Barista剩余1.1s保持。 |
| Open 2x | Day1 00:21→00:16；Completed2→4、制作／移动继续。 |
| Closing Pause | Total9、Completed6、订单7／8与工作状态保持，未Rest。 |
| Closing 1x | Completed6→7，继续服务原顾客，无新入店。 |
| Closing 2x | Completed9→11、Total7→5，继续排空。 |
| Rest 1x | Day4 00:12→00:07，约5.24s。 |
| Rest Pause | Day5 00:13→00:07，约5.25s，秒显示取整；没有解除Pause。 |
| Rest 2x | Day6 00:13→00:08，约5.25s，没有把Rest加速一倍。 |

本轮截图与事件见[events-final.json](../outputs/phase15/m01-m04-manual/events-final.json)；日志[独立Editor log](../outputs/phase15/m01-m04-editor.log)，[identity-comparison.json](../outputs/phase15/m01-m04-manual/identity-comparison.json)实际解析3次Snapshot：订单8／15／15，首批key／owner保留，后两次Completed集合及员工行完全相同。PaidVisitIds没有被现有PrintSnapshot打印，不能由订单或源码推断冒充直接付款集合证据。

### 采集限制与仍开放的补充检查

部分早期文件名／说明与实际画面不符，已在events中加correction并保留原note：open-1x-fresh实际是Pause；rest-pause初对实际已是Day3Open；rest-1x初对跨了天且首图保存滞后；day3-open-snapshot的返回画面是Day3Rest。它们不计入精确矩阵；仅用confirmed Open1x及三组同一次调用内的timed Rest前后图。菜单可能阻塞Editor repaint，因此不拿菜单打开区间计算五秒速度。

补充检查继续开放：Main按正常UI布置完整有效布局→Done→自动来客，直接读取完整PaidVisitIds前后集合，Owner观感、8h＋排空＋2h、Player／device／挂起、CI／VCS。此前拖动／目录滚动和Debugger时间参数异常仍UNKNOWN；本轮没有新调查或修复。Main打开对话框在加载前已取消，未用seed／API布局或修改正式8h／2h。

本轮Console保留5条正常信息，warning0／error0；日志定向异常检查0。Play已停止且未保存Scene，随后Sky确认Editor窗口关闭。退出日志诊断和OS native handle结果另保留边界，不推断资源全部关闭。[Assets核对](../outputs/phase15/m01-m04-assets-parity.json)3172／3172、增删改0；本轮未改产品／测试源码、Scene、prefab或meta，未新增自动suite或Git／CI操作。

独立QA已读取原批准标准、实际截图和日志，结论为**原批准M01–M04 4 PASS，限Windows Editor Validation Scene**；Main补充路径与PaidVisitIds／Paid FIFO原始集合仍未完成／NOT_PRINTED。复核同时排除误标pair，未操作Unity或重跑测试，见[review.md](../outputs/phase15/m01-m04-manual/review.md)。


## Main 玩家 HUD 分离后复测（2026-10-27）

本次Owner批准Main使用P5玩家卡，Validation保留黑色诊断。自动Edit59 PASS、直接Play122 PASS＋1原有SKIP、0FAIL；独立review Spec／quality通过，无代码finding。当前UI检查与Source边界以[Validation Report第17节](Phase15_Validation_Report.md#17-main-玩家界面与-validation-诊断分离2026-10-27)为准，原七项记录仍保留各自源时点。Main横竖屏及两尺寸Decor→Done不再显示黑色诊断；Validation仍显示完整诊断，游戏Pause／2x可操作。旧Main截图为历史，不代表修正后正式玩家界面。

本次15张原始截图，Main两尺寸Decor→Done、Validation诊断／Pause／2x／长Closing／下一Open／横屏Rest已观察。Console1info／0warning／0error；UI后3172Assets逐hash零差异，源码与GREEN一致，Editor窗口已关闭。退出原生诊断及OS handles UNKNOWN继续保留，见Validation Report第17节。


## 用户长柜台问题：自动复现，未修复

近似照片方向的6／7格柜台在1x／2x四例均出现离店Blocked、取餐队首不释放、杯子积留和员工Idle；同方向反向对照四例完成20位真实离店。额外只读诊断显示交通消散后当前出口路线已安全，但顾客仍Blocked。该新经营缺陷不被此前manual／HUD PASS覆盖，详见[Validation Report第18节](Phase15_Validation_Report.md#18-用户长柜台停滞复现2026-10-27)与原始调查记录。本轮为batch真实经营复现，不冒充native UI或Owner手测。

Owner补充全游戏无响应后，普通Editor保留相似停滞现场；真实鼠标Pause／恢复1x、缩放、Decor／Done均可用。原全游戏无响应仍未复现／UNKNOWN，未以经营停滞解释完整症状。Native诊断harness timeout与后续batch帧记录分列，见Validation Report第18节及调查记录末节；不宣称新native完整PASS或产品已修复。

## 2026-10-28 当前诊断源 Main 正常 Editor 补充（PARTIAL）

获批trace-on诊断8P0F后，root另做原Game输入问题的有界Main原生UI复现，无code/Scene保存。Decor、catalogue preview/cancel、Done、camera wheel有响应；starter未营业，注入drag未移动preview，不能确认是工具还是app原因。未完成截图长柜台，营业中Pause/2x／经营停滞复现NOT_RUN。原Game无响应仍NOT_REPRODUCED/UNKNOWN，不覆盖旧M01–M07证据范围。已恢复P5 portrait、退出Play和Editor，3174 Assets无变化。完整记录：.superpowers/sdd/2026-10-26-phase-15-business-day/main-manual-stall-check.md；原始图outputs/phase15/stall-observation/main-manual/。
