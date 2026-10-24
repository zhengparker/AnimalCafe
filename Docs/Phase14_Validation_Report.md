# Phase 14 Validation Report

> 当前结果（2026-10-23）：自然单列优先及P14收银1秒已实现，最终冻结源`single-file/final3b-source-freeze.json`直接Edit **267P/0F/0S**、Play **171P/0F/0S**。19个原家具／rapid变体、2个明确标注的controlled恢复case及原自然长项均通过；普通Editor代表现场与清理结果见本节后续记录。旧256／164是2秒源码历史，Owner旧三项visual PASS保持，新队形／动作观感单列，不将技术通过记为Phase Completed。

## 自然单列＋一秒收银：当前执行记录

### 最终同源自动化结果

| 执行 | 实际结果／范围 | UTC／时长 |
| --- | --- | --- |
| `single-file/final3b-edit.xml` | **267P/0F/0S**；P14 153＋旧P12直接47＋旧P13直接67 | 22:58:03–22:58:14，11.1524725秒 |
| `single-file/final3b-play.xml` | **171P/0F/0S**；P14 108＋旧P12／P13／Nav直接63，171个不同fullname | 22:59:02–23:08:08，546.111026秒 |

XML和最终15文件hash由root独立读回；Play SHA256为`FAD98C4D19419720D83C661A14D2236A48234A649BC35A8B4480350612DB61EE`。完整增量见`final3b-task-incremental.patch`，五runtime＋八个改动test＋新planner test/meta；`CafeLoopController`已恢复本任务baseline，不保留候选中的Ready查询桥。原失败、focused和final2不重复累计为最终通过数。

原19家具／rapid变体共57条requested 2／4／6收据，实际人数与本次规划验证的安全站位上限见[Test Cases实际多人结果](superpowers/specs/2026-10-19-phase-14-integrated-takeout-loop-test-cases.md#single-file-cohort-results)。容量不足明确`CAPACITY_LIMITED`，额外真实未付款客验证无slot不收费；不是所有布局均可站6人。19例中44条转弯均匹配最终选中prefix的真实gate拒绝；6例命中领取后有限独立交通等待并实际恢复。另2项controlled真实Nav case只验证PendingExit空Route统一边缘规划、未发下一段的相关身体证书／保留route与预算，不混入19个自然家具变体。

Round3精确RED／GREEN：`c-exit-recovery-seams-red.xml`0P2F区分真实恢复Blocked缺陷和新“保留选定下一段／预算”策略；后者旧行为可安全改道，不能描述为旧穿人。`c-exit-nextleg-certified-red.xml`0P1F核真实MovingPickup已移动2.200001、旧earlier-exit gate不成立；`c-exit-unrelated-traffic-red.xml`0P1F仅证明helper相关性判断错误，不冒充实际caller停滞。修正后`c-exit-relevance-green.xml`2P0F、16.6537987秒，原相关body短暂等待、request0／Blocked／无关body不算可通行交通，route／segments／replans和future目标保持，原tokens实际排空；这些断言保留在最终171中。原3项collected／Blocked／Done及HeldReady-beforeCM、预留失败不收费的旧断言同源通过。

最终两档各20真实单均有20不同Visit／Order／Total token，CashierFinished／Prepared／FinalDeliveryArrival各一次、20次实际出口及所有容量／杯／句柄／请求归零；原自然`ContinuedArrivalsFillThenResumeCollectionAndDrain`已含171中，不再另加1。Normal稳定`Paid && PickupWaiting`峰值2，Fast峰值1；CR相关峰值均8但包含Entering／Queued／Advancing。Hold Collection的多人容量验证与自然节奏分开：一秒收银不保证自然经营每次都形成多人取餐队，不能由两批随机样本推导固定吞吐提升。

### 普通Editor多人现场（D）

`single-file/native/`使用正式Decor构造L-03 rotation1／中间slot.1：CR支撑(1,5)、CM(4,6)旋转90°、长柜起点(3,3)旋转90°。普通Editor没有`batchmode`／`runTests`，1600×1600、captureDeltaTime0、timeScale1、camera.targetTexture=null；没有NUnit、伪造Order／slot／Actor pose。

23:13:52.3787355Z frame10924六位真实Paid站稳，pose均等于原分配目标；23:13:59.5588185Z frame11817首客已canonical Completed，原第二Visit `…00000002`从slot1／generation4实际前移1.100000m到原head／generation14，其余五位站稳。root实际查看两张PNG并核同帧JSON。原队形前两位从(1.7,-1.6)沿Z到(1.7,-2.7)，受边缘限制后沿X2.8折返；投影中有相邻两段，不宣称六位无限直线，也不将自动test的gate trace冒称本native现场日志。

23:14:21.6113542Z frame14514六位原Visit均有实际near-exit `OnDestroy`收据／原Total Released，六张原Order Completed、所有容量空、两员工Idle；`operations.log`实际`OBJECTIVE PASS`。`Facts.paid=6/orders=6`为该session累计已付款／原Order数，不是仍在场人数；drained entries为空。功能由Agent完成，新增折返／前移动作观感留Owner，原三项visual PASS保持；图片与操作见Beginner Guide。

同一普通Editor随后加载未修改的`Phase14CafeLoop`，原随机1～5秒自然生成3位后只停止新生成，Collection从未Hold；三原Order均实际Completed，客人与员工排空。只读`natural-three-timeline.tsv`／`metrics.txt`实测physicalSlots9、同时Paid未领取峰2／Paid在途峰2／稳定等候峰1，CR含入店／前移峰2。前三位从付款到实际离CR分别约.834／.835／1.002秒，到Pick-up分别约4.393／4.657／4.653秒；自然单客稳定等候不等于只有1人付款或只有1slot可用。

第三位CRHeadArrived到CashierFinished观察差值3.831秒；工具没有记录WorkingStarted，差值包含工作启动前等候，不把它当成收银工作时长，更不能据此指定某条路线为根因。P14一秒实际工作及暂停／加速／恢复仍由精确owner/runner测试证明。上述3单为只读节奏样本，不能承诺消除全部CR等待或证明配对吞吐提升，亦不擅改来客／制作／步速。

临时helper首启动发生`AnimalCafe.Camera`命名空间遮蔽`Camera.main`的compile-only错误，D当时NOT_RUN，生产／test15源未变。失败helper及log另存`native-startup-error`；工程仅退出自己创建且未进入Play的精确PID实例，修临时helper为`UnityEngine.Camera`后普通Editor成功进入Play。失败不算产品RED，不能改写为“首次无编译错误”。

清理完成：`native/cleanup-readback.json`及root独立`root-final-cleanup-audit.json`（23:20:18.1164793Z）核317 protected原字节全匹配，含13个Scene；15个最终源码／test与15份冻结copy全匹配。普通Editor自动写的EditorUserSettings、default-6000.dwlt及P7 Invalid material三项已分别存本次raw／diff、精确恢复原baseline，archive／restored hash均一致；没有整目录回滚。临时helper .cs／meta按owned hash归档后移除并确认absent，永久planner test及meta保留。原生Finish实际核GameView index8／count12，Options False／None，恢复原activeScene path、无Scene Save；不套用旧组“SceneSetup5/5”计数。本次root以完整Editor路径的elevated只读CIM核进程0，不以sandbox空结果代替；未为退出码再启动Editor。

独立原QA已分别核最终267／171、15文件冻结与D两图／JSON／六原body出口／三单时间线，最终cleanup closure见原`task9-single-file-review.md`。Owner新观感仍NOT_RUN，旧三项visual PASS保留；本轮技术完成不代填全部22manual、全项目suite、Player／设备、CI或Phase最终收尾。

### 开发与失败历史（按当时源码保留）

`single-file/a-b-red.xml`真实 **0P/6F/0S**，2026-10-23 UTC21:27:48、.3029538秒，root独立读XML。四个旋转的`SafeStraightPrefixWinsOverLargerLateralCapacity`均期望保留3格安全直线、旧实际回溯成6格侧向大容量，构成有效队形规则RED；`InitialCashierDispatchUsesOneSecondBeforeAnyPaidOrder`期望P14初始checkpoint1秒、旧实际2秒，构成入口RED。剩下`FirstStraightRejectedByRealRouteGuardAllowsLegalTurn`失败仅为Vector3精确相等浮点差，非产品RED，独立改为小距离误差断言；该case验证合法转弯，不要求它先失败。

首个sandbox启动仅Licensing BIOS/WMI权限错误，无XML，另存`a-b-sandbox-licensing.log`；不计测试。唯一工程执行者核对本轮owned进程后结束该次启动，再以精确授权Unity命令运行上述RED。生产策略选择默认关闭的`CounterQueuePlanner(float spacing=1,bool preserveStraightPrefix=false)`，仅P14 Pickup开启，原Counter默认及安全预算保留。P14一秒同时接`CafeLoopService.ScheduleCashier`初始checkpoint与EFC runner配置，避免2秒checkpoint被1秒runner拒绝；真实owner边界、GREEN／多人矩阵与最终回归尚待实际结果。

第二个可信RED `b-owner-red.xml` **0P/1F/0S**，21:28:55–58Z、2.9629481秒，真实owner→EFC→Runner初次Working的Progress仍2而非1。四runtime最小改动后`a-b-green-edit.xml` **212P/0F/0S**，21:30:14–24Z、9.6176071秒、CLI0：P14全部148＋旧Counter26＋P13 Runner38。`b-owner-green.xml` **1P/0F/0S**、1.538805秒，原owner计时test通过，包括一秒／Pause／Fast及重建runner仍只付款一次。root均独立读XML；4源freeze与增量patch在同目录，原QA开始同范围只读review。以上为阶段GREEN，多人矩阵、最终直接与普通Editor结果仍待，未标整套完成。

首次多人矩阵`c-matrix-first.xml` **11P/6F/0S**，21:33:31–34:57Z、86.2595855秒、CLI1／Editor2，root读XML。失败为长柜rotation0/2、单侧rotation1送杯corridor Blocked，单侧rotation2及三侧rotation1后续IncompletePath，入口／边缘case的TrySpawn即刻False；末项不是已证明的排空timeout。已记录若干实际2／4／6人站稳pose及physicalSlots，但这些中途receipts不等于整例PASS。工程先加强“稳定队形”采样前提为员工实际Idle／原slot复位，保留原并发失败供诊断；不得用只串行成功关闭真实并发问题。无解／预算耗尽须有证书及明确恢复，真实短暂交通若误成永久Blocked则继续修产品。

独立QA阶段核4源与212／owner1P，runtime当时无C/I；C有Important证据缺口：转弯处仅输出固定reason，没有断言forward实际被哪个原gate拒绝。该占位日志不能支持自然单列结论。已进入同任务修复：最小可选只读规划trace记录原始gate拒绝、point／previous／direction／prefix，默认null／有界且不改变候选预算或安全逻辑；CFC只保留最近创建并随重配／Done清理。matrix必须按最终选中prefix匹配真实拒绝记录，不能使用放弃分支。具体实现、新源hash、覆盖GREEN及原QA窄重审尚待，不提前关闭Important。

多人流程诊断已扩大到五个必要runtime文件，包括原Runner。`c-owned-traffic-red.xml`中真实稳态导航仍有accepted Request／owned remaining route但native `hasPath=false`，不能按静止身体判永久堵死；只承认当前有效request，不承认任意pending0。`c-carrying-home-red.xml` **0P/1F**证明已选返程在Carrying时未公开，后客可能占入；补为从选定交付侧保护完整未来返程，Returning才从实际pose接剩余各段。`c-home-exit-lease-red.xml` **7P/3F**进一步证明有效离店请求仍持服务lease时须等实际释放；取消请求不再作为可腾位交通。完整原始时序见本任务engineer报告。

`c-home-exit-edit-green2.xml`阶段 **216P/0F/0S**、9.5130391秒，P14 Edit152＋旧Counter26＋P13 Runner38。靠边退出只加P14有限4条Grid/root边缘候选、每条最多3 waypoint，缓存原Nav实际owned endpoint并保留完整身体／预留／路线guard；实际三段证书见`c-traffic2-exit-cert.xml`，不是仅以数值间距推断可走。

`c-matrix-traffic-final.xml`实际 **20P/18F/0S**、182.9835691秒（22:03:51–22:06:54Z）：19项原顾客直接检查全PASS，19项matrix中1P/18F。独立QA核17项只少最后一个异步`OnDestroy`出口receipt：生产同帧先移除Snapshot／释放容量，测试没有等待帧末销毁，现修正为同时等待所有原body receipt，仍断言实际出口距离／原token释放；不把原FAIL改写。唯一产品失败为L-08第4位实际取杯后出口遇到正在前移的后客，已批准范围内追加canonical领取前重检完整出口，先等安全路线再提交Completed。失败cleanup造成的observer回调另按测试scope清理。新矩阵／最终同源回归／普通Editor／原QA重审仍待实际结果。

`c-matrix-final2.xml` **17P/2F/0S**，原17个receipt时序问题和L-08已过；单侧rotation1与其rapid变体同因失败。`c-service-first-block.xml` **0P/1F**，8.1924201秒的firstBlocked证书表明：真实Ready／delivery／正确FIFO／!Hold的服务队首，其出口仅被已accepted离场request31暂挡；只去该body后完整出口成立，该request剩余路线对真实身体／目标独立可走。新增Service纯canonical查询→Owner进度及Hold门禁→CFC实际lease／generation／到达身份，只有当前出口可走或单个可独立离场先行者满足上述证书才短暂等待；反事实不授权实际走路。`c-service-handoff-green.xml` **3P/0F/0S**，17.7610028秒，两原失败和有效Exit lease／取消边界全链通过。

最终候选绑定`final-source-freeze.json`及相对本轮baseline的`final-task-incremental.patch`：六runtime、七个改动test及一个新planner test＋meta共15文件；root15/15 hash匹配。`final-edit.xml` **268P/0F/0S**、10.0911274秒（22:21:06–16Z），P14全部154＋旧P12直接47＋P13直接67，重复0。新增core Ready／FIFO／Complete拒绝／epoch和通用Configure清trace生命周期通过。原QA完成source/tests review，无open C/I源码问题；实际Play／trace Important／ordinary Editor仍待最后证据闭合。

节奏口径单列：本批`Phase14CafeLoop`的Normal／Fast各20单均生成`maxPaidPickupWaiters=1 maxCRQueue=8`；前者仅统计Paid且稳定`PickupWaiting`，后者包含Entering／Queued／Advancing。它既不证明所有已付款者始终仅1，也不证明自然经营已有多人稳定取餐队伍。Hold Collection的2／4／6人仅证容量和几何，不能代替自然节奏改善。原TSV每档20不同Visit／Order／Total token及三milestone各一次、实际出口已由root读回；完整Play判定和普通Editor三单只读时间线另记完成结果。

**该候选不是最终GREEN。** `final-play.xml` **162P/7F/0S**、169总／425.0491913秒（22:21:52–28:57Z），root独立解析。19个matrix全PASS，但CR前移三例及三个collected／PendingExit生命周期、HeldReady-beforeCM失败；原自然长项实际亦被selector命中并失败，不另算NOT_RUN或追加PASS。原QA明确Important：新增普遍collection approach／CanCollect出口门禁过度收紧旧安全领取合同，静态堵路不能无限停Ready代替原collected→Blocked及Done容量保留。当前按review round2撤回这条额外前置，将精确有限交通等待移到canonical领取后的出口派发／未发段；三个旧生命周期与beforeCM断言保留。另仅P14 CR前移补识别前客accepted Pickup movement，避免把仍在离开CR的真实有限请求当无leader直接送入阻挡路线。先精准覆盖再新freeze／最终回归，前述source-only PASS已被此实际失败及Important supersede，不能用矩阵或268 Edit盖过7F。

Round2覆盖GREEN：`c-postcollection-exit-green.xml` **7P/0F**，36.197568秒，保留三个旧collected／PendingExit、HeldReady-beforeCM断言，以及L-08、L-05 rotation1／rapid1。`c-prepay-straight-witness.xml` **1P/0F**，2.2881128秒，正式Decor构成West唯一侧，前客真实MovingToPickup request7的remaining route与后客候选位(-.7,0,1.5)冲突；未收费／预留rollback／同头客稍后恢复等断言保持，不写Actor pose或伪造导航回执。

Round2冻结`final2-source-freeze.json`五runtime＋八改test＋新planner test/meta共15，root全hash匹配。`final2-edit.xml` **267P/0F/0S**，11.4091577秒；`final2-play.xml` **169P/0F/0S**，524.6355845秒（22:38:40–47:25Z），包含原自然长项，重复0，不另追加自然PASS。19个matrix全PASS，44条真实转弯`finalPrefixMatched=True`；六例实际命中`FINITE_COLLECTED_EXIT_DEPENDENCY`并验证request0／Blocked不构成交通、恢复真实authority后成立、原future slot不变。两档真实20单均完成、实际出口20及所有容量／杯／句柄／请求排空；Normal稳定PaidWaiting峰1，Fast峰2，CR含入店／前移的相关峰值均8。证据由root独立解析，见`root-final2-play-audit.json`。

该全批GREEN仍不掩盖round3源码Important：pending exit的空Route恢复用了旧BuildRoute，而首次／重算用含Grid边缘候选的PlanPickupExit；未发下一段full-body失败处也需消费同一精确有限交通证书。只统一这些P14入口，保留旧P12／真实完整guard。补两个明确标注的controlled真实Nav恢复case，原19自然家具case保留；最终source3回归／原QA闭合／ordinary Editor／清理另填，当前尚未交付完成。

> 上一轮历史结果（2026-10-23早间，2秒源）：排队挡送杯补修与L-01～L-16布局分类的分层验证已完成；当时同源直接Edit **256P/0F/0S**、Play **164个不同case全部PASS**。原QA当时本范围C/I CLOSED，普通Editor代表现场PASS，精确清理已独立核对。当时收银2游戏秒／制作3游戏秒；当前批准P14收银1秒以文首新结果为准。Owner旧三项visual PASS保持，新让位观感尚未Owner验收。此结果不代填全部22manual、全项目suite、Player／设备或Phase最终完整验证。

最终同源证据（不累计重复focused）如下。源码绑定`outputs/phase14/delivery-corridor/final2-source-freeze.json`，四个永久test绑定`root-test2-source-freeze.json`；root独立重新hash十文件一致。

| 执行 | 实际结果 | UTC／时长 |
| --- | --- | --- |
| `final2-edit.xml` | 256P/0F/0S，P14全部142＋P12直接47＋P13直接67 | 04:11:56–04:12:07，11.0918331秒 |
| `final2-play.xml` | 163P/0F/0S，P14 100＋原P12/P13/Nav直接63 | 04:13:00–04:16:45，224.11671秒 |
| `final2-natural.xml` | 原自然连续来客长项1P/0F/0S，保持原case内部预算 | 04:18:14–04:20:59，164.5563059秒 |

Play合计164唯一fullname／重复0，均Passed；不是全项目完整回归。两档真实20单保存在`final2-real20-Normal.tsv`／`Fast.tsv`：各20不同原Visit、Order、Total token，CashierFinished／Prepared／FinalDeliveryArrival各一次且实际出口非空；XML明确两档最终allCapacity/cups/handles/requests=0。此前`final-play.xml`162P1F与可信返程交通RED均保留；最小补修后，原L-14两单真实送达／返回CM／领取出口及容量归零已在最后一批通过。独立原QA已核patch、六源hash、256／163 XML及pending生命周期，本范围无残留Critical／Important；natural由执行者与root读回。

L-01～L-16的配方、方法及分层限制见原[Test Cases布局矩阵](superpowers/specs/2026-10-19-phase-14-integrated-takeout-loop-test-cases.md#furniture-layout-matrix)。17个`Matrix…`结构变体最终全部PASS；动态让位／无解恢复／生命周期／多设备／连续客流用同批其它case补足，不能把16分类当作16test。`layout-recipes.jsonl`保留BUILT、失败与实际PASS历史；root另核16个交互图代表配方与最终批实际PASS记录坐标／rotation／设备／slot完全一致，见`root-final2-audit.json`。四旋转单客不等于四种满队压力，边缘规划与容量上限是分层证据，不声称穷举无限布局。

普通Editor代表补证`agent-manual/Corridor-20261023-042217`亦实际PASS，未将NUnit改名为manual。capture0／真实PlayerLoop／正式Decor／真实Nav，受控注册阻挡身体用于隔离让位条件，不能当成只摆家具自然形成该人群。04:22:44Z原已Paid Visit `…00000001`实际从(1.4,0,2.5)让到(.496527,0,2.5)，活动request6／generation6／phase1，Order Preparing、Barista WaitingToDeliver；Pause实际冻结。04:22:51Z同body／原slot0／原token仍持有，实际恢复(1.4,0,2.5)、request0／phase0，Order ReadyForPickup、Barista Idle且实际CM距离0；04:22:58Z正常取杯／Completed／实际离店及容量、杯、员工全部排空。root已查看两张1600×1600原生图并核同帧JSON，功能PASS与Owner观感分开。

该native `operations.log`记录GameView index8／sizeCount12、EditorOptions False/None与原Scene 5/5恢复且未保存Scene。工具显式经public Search API初始化后才Play，`M019-startup PASS`只表示此工具启动路径未观察到异常，不冒称任意自然启动都已重验。`native-draft/os-exit.json`的ExitCode实际为null（未缓存进程Handle），保持UNKNOWN，不填写OS0／不为该记录重复启动；helper已正常请求Exit(0)，root随后于04:25:03Z独立elevated只读CIM核完整Editor路径进程0，见`root-final-editor-readback.json`。Editor尾部原生JobTempAlloc warning保留在log，不转换为业务失败或“无warning”。

最终清理见`delivery-corridor/cleanup-receipt.json`，root另于04:29:46Z读回`root-cleanup-readback.json`：保护Scene／prefab共9/9原hash不变、六runtime／四永久test仍匹配最终freeze，旧资产目录diff0。P7材质仅已证明的两处native空字段空白及一处_Color浮点序列化变化，原bytes／HEAD／exact patch保存在`cleanup/`，Editor0后精确恢复原bytes并再hash。只将本轮owned helper `.cs`与`.meta`两文件先归档到`native-draft/source/`、核hash后移除，root重核归档一致且Assets原路径不存在；新永久家具矩阵test及meta保留。原GameView8/12、EditorOptions False/None与Scene raw5/5已由helper实际读回；Main原五处native空m_Name空格原样保留。root清理后04:29:48Z再作elevated完整Editor路径CIM，进程0；未再启动Unity，main tracked clean、用户既有untracked保留。七份living Docs／原Task9报告同步，本worktree仍未提交，不commit／push／merge／跨Phase。

## Owner批准补修：排队挡送杯（过程与失败历史）

静态检查显示EnsurePickupQueue排除服务侧点，却未完整证明队列占位后CM到交付侧及返回仍可通行；Runner在无法获取交付路线时保留WaitingToDeliver，可能与等咖啡的静态队列互等。当前只确认源码缺口，截图具体运行原因尚须实际复现。用户已批准小范围修复与测试，收银2秒不改；业务与安全要求见原Design§4.2、Test Cases P-005／009扩展及原Task9追加项。Engineering唯一C#/Unity执行者，root仅Docs，QA独立窄检查；可信RED、新修后GREEN／直接回归／native均按实际完成后填入，不提前记PASS。

首个可信行为RED `delivery-corridor/reservation-red.xml` **0P/1F/0S**，2026-10-23 UTC 01:55:47–56、9.0529695秒，root独立解析，QA另核有效。`SingleSideQueuePreservesActualDeliveryAndReturnCorridor`在旧runtime用正式Decor搭Shared窄通道：CM work=(-1.5,0,2.5)到唯一侧(2.5,0,2.5)原native可达；真实顾客付款后的wait assignment=(1.4,0,2.5)截断该线，员工已Prepared却持续WaitingToDeliver、原Order仍Preparing，失败于应实际Ready的断言，而非fixture准备／Reserve前置失败。原case该日志是assignment而非actual pose，未独立断言实际站稳／request0，因此不能单凭它认定静态身体是唯一原因；后续增强case补足。空场native也不等于完整身体证书。真实返回CM、领取与原token断言尚未执行；末尾Disabled／Blocked是teardown，不当作运行期明确Blocked功能，单客不覆盖多人／生命周期。

第一小步 `delivery-corridor/reservation-green.xml` **1P/0F/0S**，01:59:23–26Z、3.4008294秒、CLI0，root独立读XML。CFC在获得CM work上下文后，使用原完整Nav／身体障碍验证候选等候链仍留出至少一条交付＋返回路线，并在原head两侧作有限偏移；Controller只提供内部绑定上下文。相同fixture的wait assignment变为(1.4,0,.3)，实际发生DeliveryArrived／Returned、OrderReady／员工Idle，随后正常ResumeCollection、Completed／全部容量0且原tokens不变；整个case通过。真实站稳／CM pose的增强断言、已有队伍让位／无空间Blocked、直接回归与新增布局矩阵尚待后续，不将此1P当作整套修复完成。

后续`yield-blocked-red.xml` **0P/3F/0S**、3.6105504秒，root独立读XML。两个CF受控Nav case首先失败于Acquire返回True而非预期False，但后续发现短墙外有合法替代路线，因此这两项前提不足，不能记成“必须让位”的可信产品RED，也不能据此声称Nav穿人或完整gate错误；需封闭实际绕路、重新在旧runtime确认。wholeowner单侧case真实Prepared后仍WaitingToDeliver，失败于应明确Blocked，此项行为RED有效；显式恢复后续尚未执行。首启动仅fixture漏Capacity using而编译失败，保留`yield-blocked-compile-error.log`，编译失败不计行为测试。

首轮`yield-blocked-green.xml`实际 **1P/3F/0S**、5.5518796秒，文件名不代表GREEN。预留case增强actual CustomerArrived／request0／pose及CM实际距离后PASS；wholeowner已真实Prepared→Blocked且UI原因正确，但fixture错误要求已经实际离CR的Counter token仍未Released，需改为原Total／Pickup仍持有及全部原token集合保持。两个CF尚受上述可绕行前提影响。保留原结果，修测试前提不放松生产Nav／capacity规则；尚无完整让位／Blocked恢复PASS。

修正fixture前提后的`yield-blocked-green2.xml` **3P/0F/0S**，02:22:32–40Z、7.7496109秒，root独立读XML。两项CF以真实到位owned等待身体、边界窄廊及生产内部CM context验证实际让位／员工Nav到侧再回CM／客人回原slot、Pause／原tokens／不允许新reserve抢slot，以及无safe空间不移动／原slot及容量保持；这层没有建立真实Order，不能冒称完整付费链。wholeowner单侧实际Prepared→明确Blocked及UI原因→移除owned外体→显式Resume→DeliveryArrived／Returned／正常领取离场，原单／收费完成一次／Prepared一次及原tokens断言通过。原Counter token按真实离位已Released正确，不再要求其保持占用。工程将保全此draft，针对修正后的CF前提再补旧门禁有效RED，然后恢复draft；相关直接回归／布局矩阵／QA尚未完成。

已补修正前提的旧gate `actual-stationary-red.xml` **0P/2F/0S**，02:23:49–50Z、.7780509秒、CLI2，root独立解析：真实站稳waiter／封到Floor边界的长墙条件下，旧gate仍授lease；不声称实际人物已穿过。只切回本轮旧方法、保留编译所需内部CM配置入口，Editor0后逐字恢复draft，不回退此前P14实现。恢复后直接`yield-direct.xml` **25P/0F/0S**，02:25:16–38Z、21.9587354秒、CLI0，root核CF13＋Side6＋Recovery6。

该draft仍非最终：root源码检查发现全局任意Queued／无关Agent.hasPath均会提前返回，可能使繁忙CR背景下继续静默等候；仅逐个忽略一位waiter规划也不能处理同通道两位同时封路。已要求补对应focused并修正，不以25P盖过这些边界。返程目前仍走原native直返，工程将先补return-route行为RED，再让实际移动消费所验证的固定返程；不能以另一条返程存在代替实际所走路径。独立QA正在核此draft，矩阵尚待执行。

原QA独立确认两项Important（I1全局无关Queued／移动导致互等、I2两位共同封路无法逐位处理），另要求真实付费owned-yield整合链、复位remaining-route门禁及入口禁站。初次`two-waiters-red.xml`只因单客helper错误地等待所有访客Queued而setup失败，不计行为RED；只等待新客自身到位后，`two-waiters-red2.xml` **0P/1F/0S**、.8749224秒，真实两位候客、CR背景及无关移动前提成立，未启动yield所以临时hold未生效。修正为有界候选通道规划时只排除合格waiters、实际逐位全body移动，相关交通判定及逐位安全复位后，`two-waiters-green.xml` **3P/0F/0S**，02:33:59–34:02Z、3.0722803秒、CLI0，root读XML；两位实际移动／原slot与token的逐客断言还将增强纳最终cohort。该受控CF层仍无Order；wholeowner paid-yield、return detour、矩阵与QA最终关闭尚待后续。

Returning内部route两项可信Edit RED `return-route-red.xml` **0P/2F/0S**，02:37:25Z、.769134秒，旧runtime都直接发送CM而非指定首leg；分别覆盖逐段最终完成和Returning显式恢复。最小内部opt-in接线后`return-route-green.xml` **60P/0F/0S**，02:40:01Z、.3175199秒，root读XML；这是受控port的route消费／checkpoint恢复及直接相关既有case，不是60个新test或真实Nav家具摆法。wholeowner／L-07真实往返仍须后续验证，原QA窄复核同步进行。

真实owned-yield整合补证依次保留`wholeowner-yield.xml` **3P/1F**、`wholeowner-yield2.xml` **0P/1F**、`wholeowner-yield3.xml` **0P/1F**。第一批增强两位候客实际离位／逐位复位／原token断言通过，但整合case因fixture重建Nav改变binding revision，未到Prepared；第二次控制外体落在原CM支撑家具内，正式walkability正确拒绝；第三次已实际Paid→Prepared→顾客让位→DeliveryArrived→真实返CM→原slot复位且收费／制作各一次，最终测试围挡仍封出口而FAIL。三次均不冒充整合PASS，不放松Nav／出口算法。fixture改为只注册实际可站的自有外体、保留Nav revision，并在验证真实往返后移除这些测试围挡。

`held-ready-red.xml`实际 **1P/1F/0S**，02:55:03–07Z、4.6588371秒，root独立读取。`SingleSidePaidOwnerActuallyYieldsThroughDeliveryReturnThenCollectsWithOriginalTokens`完整链通过，含正常领取、实际出口及全部容量归零／原token不变。另一项`SingleSideHeldReadyStartsSafeCollectionBeforeActualMachineReturn`在真实Ready、phase2已让位、原delivery lease实际释放、员工Returning未到CM且双方完整剩余路线安全时，正式领取仍因yield标志被拒绝，是可信兼容性RED；须修复安全的yield→collection ownership转换，不能因让位新增CM硬门禁。此Shared case为单侧安全窗口，不冒称第三侧测试。QA还保留moving pickup当前位置不在原廊但剩余路线未来交叉的临时交通分类问题；两项尚待修正／新证据／最终窄复核。

I3最小内部修正后`held-ready-green.xml` **4P/0F/0S**，02:57:49–58Z、9.1224173秒，root独立解析：实际paid-yield全链、held Ready安全早取、原第三侧领取及增强两waiter case均通过。领取候选先核员工实际剩余返程，成功取得lease时才原子退休该顾客的yield-return ownership；原FIFO／slot／capacity保持，不直接删除安全guard。原QA确认原子转换本身有效，但另发现多人复位全被临时拒绝时仍清deliveryAcquired，可能遗弃phase2；须补实际全候选暂拒后的恢复，不能把单客早取和无交通两人复位相加当作该组合已验证。

I1 future crossing首批`future-crossing-red.xml` **0P/2F**、.4861357秒失败于交叉前提：同向路线未达到既有冲突角度，不是产品RED。改用真实相向／交叉及完整owned Expanded路线后，`pending-traffic-red.xml` **1P/2F/0S**，03:05:01–03Z、1.8207078秒，root独立读XML。两项交叉case均已证明当前身体不在交付廊、真实活动request未来会冲突，却被设为永久送杯Blocked，可信RED成立。多人复位case该批PASS，但交通到home时z=1.130789，只挡slot0，slot1仍可先复位，未实际触发所有候选同时拒绝；需加强前提，不提前关闭QA新Important。此阶段I1／多人复位／矩阵／最终回归及独立QA仍未关闭。

加强两原slot同时暂拒前提后，`restore-pending-red.xml` **0P/1F/0S**，03:08:00–02Z、2.1727862秒，真实traffic z=1.570000，两位phase2／request0及全部复位候选拒绝均成立，交通腾空后仍未复位，是可信遗弃RED。最小修正共享真实活动request的完整剩余交通检查、保留待恢复ownership，静态无法复位时进入既有明确Blocked；`pending-traffic-green.xml` **5P/0F/0S**，03:10:16–22Z、6.2688868秒，root独立解析。未来交叉完成／Cancel、两位暂拒后真实复位、held Ready早取及真实付费yield链均通过。该阶段不将未完成的独立窄review／最终cohort／家具矩阵记PASS。

独立原QA已核03:09:48Z draft及`pending-traffic-green`实际5P：未来交叉／多人复位Important在本次窄范围闭合，无新增C/I；相关活动request／完整剩余route、有限缓存等待、静态复位Blocked及显式Resume保留，入口候选已加既有InEntrance guard。provider null与测试Ready／Completed先后顺序的信息边界按Minor保留；这项源码review不替代矩阵／最终回归／普通Editor执行。

Owner随后追加“改好后设计各种家具摆法并实际测试”，原Test Cases已列L-01～L-16分类矩阵（外露侧／四向／长柜／L角／窄廊／边界／多设备，以及队满／穿行／让位／无解／生命周期）。新增`CafeFurnitureLayoutMatrixTests.cs`以partial复用原Side场景准备，正式Decor构建并导出实际recipe。初次`matrix-pilot.xml`为**0 case**、CLI0，selector括号未匹配，log明确No tests executed，不算PASS。按method重新选择后的`matrix-three-sides.xml` **0P/4F/0S**，03:23:06–10Z、3.7689264秒，均在fixture家具Confirm未生效时失败，尚未形成有效业务布局／recipe，不是经营流程RED。后续须修正正式操作准备，再按真实运行配方和断言逐项映射结果；预期无法服务的布局也要验证拒绝原因与恢复，不把摆法数量当作PASS数量。结果表完成前仍为NOT_RUN。

矩阵后续`matrix-three-sides2.xml` **0P/4F**、4.8628757秒：已确认家具与三侧BUILT，但Done同Step末服务combo才Ready、下一Step尚未发布接待许可；首个直接TrySpawn被拒，非收费或送杯失约。fixture只等待正常一帧并断实际admission许可后，`matrix-three-sides3.xml` **4P/0F/0S**，03:30:34–41Z、6.8973026秒，L-02四向真实单链全PASS。`matrix-rotations.xml` **8P/0F/0S**，03:31:55–32:11Z、15.7758234秒，L-03长柜四种真实rotation／slot.1与L-05单侧四向均全链通过。`matrix-corner-edge.xml` **4P/0F/0S**，03:34:51–35:00Z、9.5662189秒，L-01／04／07及08+09通过。三批16个唯一case，root独立读XML，原QA另核recipe与真实CM／slot cup parent／实际出口／原Total Released／业务三milestone各一次；单客矩阵的多人Facing、lease并发、边缘满队压力仍须最终同源既有case分层补证，不冒称这16例独立证明。

`yield-lifecycle.xml` **2P/0F/0S**，03:36:57–37:02Z、5.8673078秒，root读XML：真实Paid让位请求已移动>.1m时Pause八帧，组件disable退休原request并保same body／原tokens，正式Resume后全链完成；另一分支在真实让位中正式改CM rotation＋changed Done，旧epoch未领body／order／owned request清理，原tokens Released，主动Pause保留，恢复后员工实际Idle。L-15为实际phase1窗口，不引用普通Working／Carry生命周期替代。L-16首批`matrix-disconnected.xml` **0P/1F**、1.9921519秒，使用尚未发布几何的CFC getter证明外露侧而前提为0，尚未证明产品断路处理失败；改用正式layout resolver验证实际外露侧后再跑，原失败保留。L-12与最终直接cohort仍待。

L-16中间历史保持：`matrix-disconnected2.log`仅fixture错误访问不存在的SurfaceSlotCatalog而编译失败，无XML；`matrix-disconnected3.xml` **0P/1F**、2.0394049秒，在invalid Main尚未创建Capacity时fixture空引用；`matrix-disconnected4.xml` **0P/1F**、1.9798354秒，断路拒绝前提已过，但recipe读取尚未绑定岗位时空引用。均不冒称产品RED。按实际未初始化／未绑定状态记录后，`matrix-counter-working-red.xml` **1P/1F/0S**，03:45:19–24Z、5.2525743秒：L-16正式四侧外露孤岛、明确拒绝／无建单／未创建ledger，正式移柜开口后同布局实际完整单链通过；L-12真实Cashier仍Working的第二CR顾客有原Pick-up预留，首单Prepared却立刻永久送杯Blocked，可信RED成立。后续只在现有协调层增加内部只读的当前有效收银服务owner事实，用于判断有限临时占路；原完整身体／route gate仍保留，收银2秒不改，不能把AnyQueued当作将来一定腾空。

CR Working首修`counter-working-green.xml`实际 **3P/1F**、3.9853006秒：有限Working已正确等待，但Cashier同Employee.Step先完成、canonical ServiceFinished尚未下次service.Tick消费时，Barista gate过早失去交通事实。`counter-finish-boundary.xml` **0P/1F**、2.5177431秒确认真实Queued／request0／无native path交接窗口；不是取消。只增加`CafeLoopService.HasQueuedCashierCompletion`内部纯查询，要求当前Live／!Blocked／Matches Work-Token的真实ServiceFinished，消费即失效，不用observed历史／宽限计时。`counter-working-green2.xml`仍 **3P/1F**、5.688955秒，已过Working／完成交接／第二付款并实际转队，失败在后续持续人工横墙与多人动态的另一个有界规划阻塞；没有安全完整绕路证据，不据此放松planner。L-12隔离目标改为第二原body实际离CR保护区后移除自有测试横墙，再验证原Prepared无需额外Resume正常完成；持续横墙保留为L-14明确Blocked与显式恢复分支另测。

`counter-working-green3.xml` **4P/0F/0S**，03:54:51–57Z、5.7872274秒，root独立解析：L-12真实Working／canonical完成交接／实际CR腾位及两原单收费2／Prepared2／Delivery2／原token全排空通过，future crossing完成／取消与CR背景双waiter继续PASS。最终6个runtime文件的窄baseline增量与hash见`review-current-runtime.patch`／`review-current-runtime.json`；并非HEAD全部P14差异。最终直接回归、持续横墙分支、普通Editor与新provider独立窄复核仍在进行。

该候选最终直接`final-edit.xml` **254P/0F/0S**，03:58:22–33Z、11.4323611秒、CLI0，root读XML并分组P14全部140／P12直接47／P13直接67，6源hash匹配freeze；四个本轮永久test另存`root-test-source-freeze.json`。随后`final-play.xml`实际 **162P/1F/0S**、163总，03:59:28–04:03:15Z、226.9116345秒、CLI1／Editor2，不能标最终GREEN。旧P12／P13／Nav63全部PASS，P14为99P/1F（自然长项未在此批）；唯一新L-14持续横墙分支已过明确Prepared Blocked、排自有墙／显式Resume、首单DeliveryArrived／Returned和第二DeliveryArrived，之后第二Returning在实际pose(-.066162,0,1.760894)、离店客2(1.658905,0,.039806)时IncompletePath。EFC对已选返程下一段的前置检查失败即拒绝，Runner立即Blocked；尚需记录完整native leg与真实moving traffic证据，不能先认定静态无路或改P11预算。自然／普通Editor暂停推进，先查这一项，不用162P覆盖唯一FAIL。

返程可信RED `return-traffic-red.xml` **0P/1F/0S**，04:06:36–42Z、6.0154336秒，root核完整证书：原固定returnRoute已实际到第6点，下一CM target(3.5,0,-3.5)有完整native；唯一裁拒为真实活动离店客2 clear=False／moving=True／exit目标(.5,0,-3.35)，静止Cashier clear=True。最小修正仅P14已选return route在发下一段前用现有完整交通事实等待，保留同一target／handle／generation／revision，Pause不查询／不请求，退休清pending；交通结束后原MoveTo完整门禁仍可明确拒绝，不改P11／已发预算／side。`return-traffic-edit-green.xml` **62P/0F**、.340161秒和`return-traffic-play-green.xml` **4P/0F**、04:10:15–25Z、9.5439137秒通过原L-14及held Ready／yield disable／Done；62是直接port cohort而非62个新test。新增两项Edit核pending同leg、Pause／Cancel重入及最终CM回执。

新返程候选`final2-edit.xml` **256P/0F/0S**，04:11:56–12:07Z、11.0918331秒、CLI0，root读XML；P14全部142／P12直接47／P13直接67。当前6源hash为`final2-source-freeze.json`，本轮窄增量`review-final2-runtime.patch`，四个永久test另存`root-test2-source-freeze.json`。原254／162P1F／旧20样本和原freeze保留。`final2-play`、自然1、独立最终窄review及ordinary Editor仍在执行，不提前记整体通过。

> 最新Owner长柜反馈已复现并修正等候Facing：正式RED0P2F→focused GREEN11P，最终直接Edit252／Play133全部PASS；同近似布局普通Editor确认后客朝前位、实际前移为队首后朝服务点。仅修改朝向，安全转弯站位保持。下方此前八项PASS保留原源码范围；Owner视觉尚未验收。

## Owner新布局：长柜Pick-up队形（技术验证通过，Owner visual待验）

Owner已关闭Unity并明确要求按截图复制尝试。图片未给Grid坐标，Agent以普通Main正式Decor构建近似布局：连续grid X1..7／Y5、CR在左端、Pick-up靠右、CM后排；完整蓝图及设备地址见原operations。`LongQueue-20261023-004134`实测两位Paid均PickupWaiting／Arrived=True／request0：slot0=(2.5,0,-.6)，slot1=(1.31213546,0,-.6000001)，两实际forward分别约(.05568,0,.99845)／(.05320,0,.99858)。第二位应沿slot链向前位+X，实际dot仅.05320；root已查看同次native图，能重现横向站位统一朝柜台。不是把Counter客或已领取离场者算成Pickup。raw结果文案残留“3位”，实际仅2位，summary已明确纠正，原文不修改。

`pickup-formation-diagnostic/diagnosis-summary.json`与各run原log保留：首轮`LongQueue-20261023-003538`150墙钟秒仅推进17.31游戏秒、无业务failure，工具watchdog INCOMPLETE；第二轮`...004022`仅读probe错误地按static反射instance RouteClearance而中止，属工具错误；第三轮普通capture0、临时runInBackground及Editor PlayerLoop／Repaint驱动后取到上述真实站稳现场，退出0，未注入业务／Actor pose／游戏delta。三个run保护文件均保留原值；原用户`Logs/Editor.log`72406bytes在启动前另存`pickup-formation-diagnostic/user-Editor-20261023-002853Z.log`并留hash／时间戳。

原规划同帧队首正后方首步(2.5,0,-1.7)各isLegal层及含head prefix的fullRoute均True，但未证明完整直行链可满足所需物理站位数。旧CounterQueuePlanner已批准可回溯选择更长安全分支，故本轮不将其判为规划bug，不改旧Counter算法或强塞一条未经验证的直线。明确遗漏是CFC初次入队及前移都固定`-pickupOutward`，而已有Counter按前slot朝向。root已向Owner说明小步范围：仅CFC private owned PickupWaitTarget、两处Send入口，以及真实Actor站稳／前移／90°布局tests；队首／实际领取侧／站位／FIFO／容量／lease／Nav安全沿原规则。以下分别保留实际RED、修复及最终验证。

正式`pickup-facing-red.xml` **0P/2F/0S**，00:44:41–43Z、1.9860166秒、进程exit2；两项均因真实站稳slot1 forward向前位dot仅.078357／.084780而失败，不是fixture前置失败。最小产品变化为CFC私有`PickupWaitTarget(assignment,owner)`：队首沿原`-pickupOutward`，后客读取该Visit所属PickupOwner的前SlotIndex目标，初次转队与每次前移两处Send消费；不新增public API／序列化字段，也不改planner／positions／actual collection target／旧Counter逻辑。

`pickup-facing-green.xml`整个CF **11P/0F/0S**，00:45:57–00:46:02Z、4.9542325秒、退出0；原9＋新增`PickupBendFacesPreviousCustomerAndUpdatesAfterActualAdvance`／`RotatedPickupBendFacesPreviousCustomerAndUpdatesAfterActualAdvance`。新case在controlled CFC port fixture出生三真实Actor、经原planner/native到位，验证折线／朝向；再经真service到位、模拟上层领取授权后实际离位，两余客FIFO／slot0,1／generation递增／实际前移位置及新头尾Facing全部验证。90°覆盖实际service坐标和outward重新规划，属于受控Nav fixture的Pickup geometry，不冒称完整Decor Grid旋转或CafeLoop真实付款。完整Paid现象来自上述普通Editor两人证据，修后同蓝图另证。独立QA已核patch与RED／GREEN，无C/I；最终直接cohort及native前不预记CLOSED。

本轮最终源码冻结见`pickup-formation-diagnostic/source-freeze.json`：CFC于2026-10-23 00:45:33Z、SHA256 `e49b06e03cc6bc8c6612190cbddff684de02e71d5bc632fc4af67eff1029832a`，CF tests于00:44:08Z、SHA256 `88c0ad4930fc5339adae5928b6aa807441ec0c6fd19a1b624aaa98aa2d1a409a`；独立于上次Shared修复源。root已读`approved-facing-only.patch`，只新增private helper及两处调用，无旧Counter改动。

最终直接Edit `pickup-facing-edit.xml` **252P/0F/0S**，00:47:44–55Z、10.8409066秒、退出0，P14全部138＋P12 Counter／Admission／Route／Clock47＋P13 Binding／Lifecycle／Runner67。Play实际filter留在`final-play-filter.txt`：`pickup-facing-play.xml` **132P/0F/0S**，00:48:49–00:51:36Z、167.6242027秒、退出0；原自然Continued另按未改180秒case预算执行，`pickup-facing-natural.xml` **1P/0F/0S**，00:52:17–00:54:59Z、161.4695488秒、退出0。root独立读三份XML并按fullname核两批Play为**133个唯一case全部PASS**，focused11不重复累计。范围为P14、P12flow36、P13Main11／Animation5、NavLayout11，不是完整项目suite；此前Shared源的334／169、四组普通Editor八项不能扩称为新CFC源最终PASS。

本轮新样本`final-facing-task9-real20-Normal.tsv`／`Fast.tsv`各20行、20独立Visit／Order／原Total token，CashierFinished／Prepared／FinalDeliveryArrival每行均1、实际出口字段全部存在；root独立核对。独立窄QA已核最终源码hash、最小patch、RED／GREEN及最终Edit252／Play132，结论**CLOSED，无残留Critical／Important**；自然Continued由执行者与root另核，普通Editor及Owner视觉不包含在该review结论内。

修后普通Editor `LongQueue-20261023-005629` **Agent PASS、OS exit0**：capture0、真实Main Decor／付款／Nav，修前后FIRST_BLUEPRINT七个slot坐标逐项一致。初次两位Paid／PickupWaiting／Arrived／request0，head朝服务dot=.997909；原第二位朝前一slot的实际dot=1（修前仅.05320）。正式ResumeCollection后，原首客Completed并实际Exiting离开服务位；同一原第二位真实前移至slot0、Arrived／request0，转回服务朝向dot=.996758。此时第三位仍PickupAdvancing，不冒称第三位已站稳。`pickup-formation-diagnostic/native-facts.json`与该run原operations保留身份、位置和方向；不是直接设置Actor pose或Order完成。

root已查看同run[后客朝前位近景](../outputs/phase14/agent-manual/LongQueue-20261023-005629/long-counter-settled-pickup-closeup.png)与[原第二位前移成为队首](../outputs/phase14/agent-manual/LongQueue-20261023-005629/long-counter-original-follower-now-head-closeup.png)，均1600×1600原生、targetTexture=null，使用正式Camera pan／zoom；另保留与[修前全景](../outputs/phase14/agent-manual/LongQueue-20261023-004134/long-counter-settled-pickup-formation.png)同1600×720的[修后全景](../outputs/phase14/agent-manual/LongQueue-20261023-005629/long-counter-settled-pickup-formation.png)。截图是近似蓝图单帧，不能代表Owner原布局逐格相同或动画观感验收；本次没有把物理站位改成直线。

最终清理见`pickup-formation-diagnostic/cleanup-receipt.json`：12项原保护文件保持启动前字节，CFC为批准的新hash；CFC／CF tests最终freeze两项一致，root独立核当前13项与receipt相同、两个freeze一致。临时Assets helper `.cs/.meta`先copy／hash读回后仅移除原两文件；root核归档hash2/2、源均不存在。原Scene5/5／旧P12/P13 HEAD4/4保持，GameView8/12、EditorOptions False/None实际恢复。P7唯一native自动空白／float精度差异先备bytes与patch、证明后定点还原，旧资产状态空；main checkout tracked状态空。root于00:59:40Z另用完整Editor路径的只读CIM查进程0，原用户Editor.log归档SHA与原receipt一致。Docs／此次CFC与tests diff-check通过；未commit／push／PR／merge，无下一Phase或完整项目suite声明。

状态（2026-10-22）：**Owner委托的四组功能代测完成：M-007＋010、M-017、M-019、M-021功能覆盖PASS，本轮普通Editor八项功能记录通过；只留下三类visual供Owner判断。** 本轮没有修改正式产品／永久tests／Scene／prefab，没有重跑全部manual或NUnit suite；此前当前源码直接Edit334／Play169及Shared竖屏.832秒证据按原运行复用，不冒称本轮重跑。独立窄review CLOSED仍针对原Shared修复。工具中止历史、证据分层及本轮精确清理见下节；历史超时无XML仍INCOMPLETE／计数UNKNOWN。Owner视觉尚未验收，Phase尚未Completed。批准的规则见 [Design](superpowers/specs/2026-10-19-phase-14-integrated-takeout-loop-design.md)，执行进度见 [Implementation Plan](superpowers/plans/2026-10-19-phase-14-integrated-takeout-loop.md)，操作见 [Beginner Guide](Phase14_Beginner_Guide.md)。

## Owner委托：四组功能代测，仅保留visual（功能完成）

Owner本轮明确要求“这几个项目，你帮我测测看吧，只留下visual给我”。范围为Guide四组：M-007＋010、M-017、M-019、M-021。沿原Task9复用已批准用例和临时普通Editor验证工具，不修改玩法／正式Scene／prefab，不重跑全部manual或完整项目suite。当前同源Shared竖屏.832秒证据保留复用；补测两species持杯与Pause、多侧领取、多设备／中Slot及真实Done、两尺寸UI事件／Console、已领取者跨Done保留与主动Pause。

本轮普通Editor结果按实际完成更新；不把自动NUnit结果冒充普通Editor代测。功能测试由Agent执行；Owner仅留下手杯／动画观感、桌杯位置外观、横竖UI可读性／遮挡三类visual，原Owner历史FAIL保留，不要求Owner再搭功能故障现场。

| 功能组 | 当前结果及本轮证据 | 留给Owner的visual |
| --- | --- | --- |
| M-007＋010 | 两species `Carry-20261022-122304`实际领取前空手、领取后单手杯／Carry+Walk、Pause同杯冻结、正常离场／容量排空均PASS。`Sides-20261022-123212`实际原North交付lease仍占时East领取PASS；root核JSON员工距原侧.0291448m≤.08、同原lease／collection lease、Completed及手杯1；正常移除owned blockers后排空。Shared首客.832秒使用上节同源普通Editor证据；当前同源自动范围覆盖单侧退让／FIFO，不冒称本轮重跑Shared或全部NUnit。 | 两种持杯动作自然程度、杯与手的位置 |
| M-017 | `G17-20261022-123420`普通空Main、新GUID、修正五组配方正式Decor PASS；实际Cashier station=`16fe3cc7...`、Barista station=`31c36865...`，客人与Cashier同CR，桌杯parent=`slot.1`、world=(2.42,.42,-1.38)。首单正常离场排空后，合法旋转长柜并Done退休旧epoch，新单仍送当前中Slot。root已读操作／结果并查看两张原生1600×720图；G17旋转前已排空，未领取桌杯跨Done清除由M-021另证。 | 长柜台中间Slot／旋转后桌杯外观 |
| M-019 | `ReviewUI-20261022-123922`两次独立Stop／Play的1600×720、1080×1920均PASS，普通Editor退出0。实际Ready桌杯、只读UI文字与Snapshot相等；对可见按钮做eligible／topmost Raycast及EventSystem click，验证Pause→1×→2×→Pause→1×、Decor／无修改Done、实际Camera pan／zoom，订单数和epoch不变。root已读两尺寸结果／事实并查看营业截图；不冒称OS mouse或手机touch。 | 横竖屏文字、按钮及遮挡观感 |
| M-021 | `ReviewDone-20261022-124141`正常1×和fresh主动Pause两分支PASS，退出0。一个已领取Exiting＋另一未领时，正式新增格(0,0)家具绿色Confirm／Done，旧单／桌杯／未领清除；原Actor／ID／同杯／原Total保留，旧epoch1→2，before==after=(2.497394,0,1.493631)，员工ID不变。原客随后正常离场／Total Released／全部容量0、员工实际回岗，Resume Arrivals后实际生新客。另Stop／Play主动Pause→真实修改Done仍Paused，再点可见按钮恢复1×。root已读结果和continuity日志。 | 不另要求Owner搭建功能现场 |

工具前置历史保留：`Sides-20261022-122502`在frame5生成前TrySpawn拒绝，当时未采全门禁flag，具体原因UNKNOWN，不算PASS；后续仅读门禁、等实际接待状态后，相同blocker位置单次TrySpawn成功。`Sides-20261022-122942`已真实Ready，却因截图前Screen暂为411×1341、不是请求的1600×1600而中止（下一failure截图即1600×1600），记BLOCKED。仅在临时helper补门禁就绪检查、失败空Order诊断及≤5秒实际尺寸等待，正式游戏逻辑未改。上述最终Sides成功保留同一物理阻挡场景，三轮原log均保留，进程exit0不替代case成功。

UI首轮`ReviewUI-20261022-123630`记BLOCKED：临时工具错误地引用隐藏legacy `pauseButton`，eligible检查拒绝，未发出点击。现有P8R／MainCafe正式合同是可见`normalButton`在运行时暂停、再次点击回1×，2×也先暂停再回1×；修正仅测试工具引用和期望，不激活隐藏控件、不修改Panel。两轮UI原证据分列，最终成功不删除原工具失败。

本轮最终成功五个ordinary Editor组（Carry／Sides／G17／ReviewUI／ReviewDone）分别退出0；root独立读五份exit.txt及各results.tsv，八项功能记录均PASS、非PASS0，startup工具检查另列不增加功能分母。UI最后成功组实际runtime Console Error／Exception／Assert／Warning header均0，Editor log未见原`CafeLoopPlaySessionTests.cs`的CS0618；不因此抹去旧工具失败或其他历史legacy warning。全部按钮是实际eligible与topmost Raycast后的Editor EventSystem事件，Camera为正式API，非OS mouse／touch代测。截图仅代表帧，动画自然程度仍由Owner看；Windows Editor不替代手机设备验收。

本轮最终清理见`outputs/phase14/owner-functional-review/cleanup-receipt.json`：root另查Unity进程0，13项保护文件／最终源码hash全部等启动前；临时helper两Assets源不存在，归档hash读回一致，旧Art／UI／Settings／ProjectSettings／Validation／Prefabs tracked diff0，main checkout tracked diff0。成功组均实际恢复EditorOptions False/None、GameView index8/count12、未保存Scene。清理脚本曾因syntax error未执行归档，但其后仅删除了本任务生成的两Assets helper；最后一次copy input仍完整未改。Editor0时从该input恢复同两文件、核等、归档并hash读回后精确移除，receipt保留此顺序；归档hash证明最终恢复源与保留input一致，不冒称首次删除前已测得hash。没有正式产品或运行证据丢失，恢复时未启动Unity。

Guide已把Owner任务缩为三项visual并提供本轮八个可直接打开的图片链接；root逐项确认图片存在，查看两种持杯、长柜原位／旋转后、横竖营业／Decor原生图。Done／容量／身份／Pause功能不再要求Owner重搭。原已批准设计、玩法及版本管理授权未变，未commit／push／merge。

## Owner反馈：SharedPickup首客转队停顿（技术验证完成，Owner visual待验）

Owner在1080×1920、`Phase14SharedPickup`观察首客点单后停在CR，截图为Order1／Visit0001 Preparing、Barista Working余2.2秒，Counter仍含0001/0002。正常`Phase14CafeLoop`另一现象已由Owner确认是前面有人时先到Pick-up队尾再前移，符合FIFO，不列为缺陷。

受控普通Editor使用原Shared布局、自然AutoSpawn、capture0、1x，分别实测1080×1920和1600×720；没有直接建单、移动Actor或调用mutator探针。证据在`outputs/phase14/agent-manual/PortraitDiag-20261022-033045/`：竖屏付款后约.150秒、横屏约.102秒已实际离CR超过.1m，因此并非业务上等待Ready才发移动；但两者均在实际(-3.457,-.949)附近、距原CR约.552m停约3秒，尚未满足原身体离位阈值，所以Counter仍占用。标题Waiting属于后客0002，不是首客停顿的原因；采集帧delta约.006秒，运动预算.2667秒，也不支持低FPS截断假设。

追加唯一竖屏只读probe在`PortraitProbe-20261022-033456/operations.log`实证：同一移动request5持续有效、CFC pending=false／无业务failure，driver卡在raw corner1(-3.45,-.95)；朝该点的约(.00647,-.00072)位移在静态avoidance阶段被裁成近零，下一完整腿的认证为false。NoProgress从.500增长至2.503秒；game10.272时service generation1→2、RetryCount0→1，新native path删除原卡住corner后恢复，game10.644物理离CR、11.663才Ready。故确认是无进展超时触发的一次内部重算，时间恰与3秒制作接近，不能说由咖啡Ready解锁。该阶段尚未拆分候选的两层拒绝来源，后续正式RED补足如下。

诊断阶段产品代码／Scene未改，清理见`outputs/phase14/shared-early-pickup-diagnostic/cleanup-receipt.json`：Editor0、临时两文件归档移除、保护文件11/11及旧资产diff0、GameView8/12与False/None恢复。追加probe退出0；首双cycle的OS退出码因句柄未保留为UNKNOWN，正常自清理及Editor0已确认，不冒称exit0。以上UTC证据文件名是本地2026-10-21晚间的运行。

沿Owner此前“有问题先修”继续原Task9最小修复。新增`CafeCustomerCarryTests.SharedFirstPaidCustomerReleasesCounterWhileBaristaWorksWithoutNavigationRetry`的`shared-corner-red.xml`正式 **0P/1F/0S**，2.348秒：实际CR释放为付款后3.85游戏秒、retry=1且Barista已Carrying，失败不是仅验证>.1m起步。只读证书拆分证明：从实际pose直达下一corner，NavMesh不挡但完整guard只批准约(.00277,.01020)；原唯一outward候选(-3.528517,-.941276)完整guard通过，但NavMesh.Raycast拒绝。因此是原拐点完整skin不可达、唯一绕角候选越出NavMesh的组合。最小private修复限`NavMeshMovementDriver`原Arrival范围内按1／.5／.25／.125四个offset依次试，仍须原`World.CanTraverseStraight`完整Nav＋guard认证，成功才锁定adjusted corner；无安全候选沿原3秒／唯一retry，不放松任何安全阈值或提前释放Counter。

runtime冻结于`2026-10-22 03:45:59Z`，新增测试最后于03:47:16Z补同request引用的SameAs断言。root独立解析`shared-corner-green.xml`：**34P/0F/0S**（P11真实Movement33＋新增Shared1），03:46:22–26Z、4.0249333秒；原客在付款后.8333282游戏秒真实释放Counter，同request4／retry0／generation1，Barista仍Working余2.150001秒。focused34早于SameAs测试断言补充，不冒称测试文件最终同hash；最终直接cohort如下。

| 最终直接范围 | 结果 | XML与UTC |
| --- | --- | --- |
| Edit：P11全部82＋P14全部138＋P12相关47＋P13相关67 | **334P/0F/0S，CLI0** | `shared-corner-edit.xml`，03:48:37–47Z，9.9796851秒 |
| Play：P11 Movement33／Scene转角5＋NavLayout11＋P12flow36＋P13Main11／Animation5＋P14除自然Continued的67 | **168P/0F/0S，CLI0** | `shared-corner-play.xml`，03:49:35–03:52:46Z，190.8164016秒 |
| Play：原自然到店／恢复领取／排空Continued，原180秒case预算 | **1P/0F/0S，CLI0** | `shared-corner-natural.xml`，03:53:46–03:56:25Z，158.6745272秒 |

最终两批Play合计**169个唯一case全部PASS**，root按fullname去重核对168＋1，无重叠；focused34包含在其中，不重复累加。最终源码hash见`shared-corner-source-freeze.json`：driver `20191b6b3441a367cd86187e3290be6dea46efcb294f28c9d7625c80bdaece3c`，carry test `1843fd29da120ada23c32b694374c5452ae46ab59be6a8fbff5cffe24bb26ace`。

最终Play中的新Shared case包含SameAs及实际wait Arrived断言：原客付款后.833252游戏秒释放Counter，实际离原CR距离1.011542m、retry0／generation1，Barista仍Working余2.150001秒。Normal与Fast两个真实20单case本轮也执行，`shared-corner-real20-Normal.tsv`／`Fast.tsv`各20行、20独立Visit／Order／原Total token，三项实际业务里程碑每行均1；原入口／身体出口与最终排空断言通过。两份测试表不代替普通Editor手测。

同一次独立窄QA已核实际patch、RED、focused34、Edit334及最终Play168，结论**CLOSED，无Critical／Important**。四个固定候选保持当前实际pose的完整认证，最终目标／corner推进／retry／移动预算未改。两项Minor如实保留：新case的实际离CR距离目前打印而非独立数值assert，原Counter释放实现及已有容量tests未改；仅出现无进展时触发的`LogSharedCornerCertificate`保留为永久只读失败诊断，非运行时product helper。故不宣称这项单case独立证明提前释放防护或整个后续取餐路径全程零retry；自然Continued的PASS由执行者与root另核，不冒称reviewer复核该项。本轮不是完整项目suite，也不代填Owner视觉验收。

最终普通Editor单竖屏组`outputs/phase14/agent-manual/PortraitProbe-20261022-035755/`：1080×1920、targetTexture=null、capture0、自然1×／原AutoSpawn及后续客人均保留。首客Create8.187545642→CounterReleased9.019677346（**.832131704游戏秒**），实际距原CR .992961m，原service request5／generation1／retry0，Barista Working余2.159036秒；此时Preparing／Cup0，13.045070047才Ready。root已查看同帧`shared-timing-0-preparing.png`，Counter只剩0002、Pickup0001与Working2.1秒显示一致，不能仅由截图推导request。原首客随后正常Completed、19.981826743离场，原三项reservation均Released；未改Actor或直接提交订单／Arrived。该组只补本次Shared首客问题，非重跑全部22manual，也不评价未出现的另一species手杯观感。结果、时序TSV与Console原文同目录保留。

最终清理`outputs/phase14/shared-corner-native-draft/cleanup-receipt.json`已核：普通Editor进程退出0，root另查Unity进程0；两临时Assets helper逐字归档并核hash后精确移除，root核archive存在／hash一致／source不存在。原GameView index8/count12、EditorOptions False/None实际恢复；Main、旧P12/P13 Scene/meta、两P14 Scene、两P14顾客prefab及CFC/Service共11项hash均等本轮启动前，driver／test最终hash亦未变。只出现已知P7 Invalid材质native空格／float精度差异，经原bytes与patch备份、精确证明后定点还原；`P7-auto-proof.json`及after hash保留，旧Art/UI/Settings/ProjectSettings/Validation/Prefabs tracked diff0。root另跑Docs与driver diff-check通过，main checkout tracked无改动；本轮未commit／push／PR／merge。

## Pick-up多侧交互追加修正（技术验证完成，Owner待复测）

本节为多侧追加的历史源码证据：runtime写入截止`2026-10-21 04:42:02Z`（CFC），checkpoint test为04:31:10Z、Status fixture为04:25:24Z；临时诊断已撤回。root已独立解析以下同源XML；最新Shared修复另见上节：

| 范围 | 结果 | 文件与执行时间（UTC） |
| --- | --- | --- |
| 直接Edit（P14全部138＋P8/P12/P13相关151） | **289P/0F/0S，CLI0** | `pickup-sides-final4-edit.xml`；04:47:22–04:47:31，9.995秒。 |
| 直接Play主批（排除自然Continued） | **129P/0F/0S，CLI0** | `pickup-sides-final4-play.xml`；04:48:51–04:51:42，171.639秒。 |
| 原自然Continued单项（修正后最终源） | **1P/0F/0S，CLI0** | `pickup-sides-collection-corridor-green.xml`7项中的该唯一方法；实际163.351秒，整批04:42:31–04:45:27，176.252秒。 |
| 普通Editor多侧组 | **Agent客观PASS** | `agent-manual/Sides-20261021-045308`；非batch、非NUnit；原delivery lease仍占，客人由第三侧领取，随后正常离店／排空。 |

Play主批为Nav11、P12Flow36、P13Animation5/Main11、P14Carry6/Flow9/Decor10/Recovery6/Integration8/Regression16/Side4/Status7。与同源自然case合并为**130个唯一fullname、130PASS、重复0**；不累计focused另外6项或此前重叠结果，不声称本轮完整项目suite／设备验证。`pickup-sides-final4-real20-Normal.tsv`／`Fast.tsv`各20行、20唯一Visit／Order／原Total token，bad milestone0；实际最大出口误差分别0／.0003162m。此前各轮固定TSV在新run前另存，本批不使用旧样本冒充。

同一独立窄review已对最终源码、两份final4 XML、自然case及两档原始TSV复核，**CLOSED，无残留Critical/Important**。Minor保留：retired-id guard的现有拒绝断言同时受其他门禁约束，仅有源码核对，不能称为独立自动隔离证明。普通Editor／cleanup及Owner观感不纳入这个自动证据结论。

普通Editor组于04:53:08Z–04:53:41Z执行：原Customer South侧以正式registered、无身体重叠blocker占用；Ready时Barista实际在North(2.5,3.5)。Completed时同原queue／leaseActor仍持North，员工实际(2.471670,3.5)、距原侧.02832985m≤arrival .08m，顾客实际抵达East(3.500269,2.494148)，不是只证明“尚未回CM”。采集期间Pause，原Order从Ready→Completed一次，桌杯1→0、真实CustomerHeldCup0→1，Pause保留；正常移除owned blocker并正式Resume后同客实际出口、原Total释放、杯随Actor销毁、三capacity0、员工Idle且无重复Order。

Root已查看[Ready画面](../outputs/phase14/agent-manual/Sides-20261021-045308/pickup-ready-delivery-side-occupied.png)及[第三侧领取画面](../outputs/phase14/agent-manual/Sides-20261021-045308/pickup-third-side-collected-with-delivery-lease.png)，并独立读取同名JSON、operations.log和results.tsv。画面可见桌杯有→无；人物背向／HUD局部遮挡，手杯在本组仅由真实object／状态记录证明，不称图中手杯清楚可见。清晰两species持杯正视图仍是下方旧Carry组，未重复全部22 manual或两species新native；Owner动画观感待本人确认。临时工具的公开Search初始化仅为启动前置，不另计一项产品manual完成。

最终清理见[receipt](../outputs/phase14/pickup-sides-final-cleanup.json)：普通Editor退出码0、完整Editor路径进程0；临时helper `.cs/.meta`先逐字归档并核hash，再仅移除原两文件。Root独立核归档hash2/2、原文件均不存在，九份Scene／prefab当前SHA与receipt9/9一致，旧P12/P13四Scene/meta直接与HEAD blob4/4一致；Main及两P14 Scene保留既有持杯接线。GameView恢复index8/count12、EditorOptions False/None；唯一P7材质自动空白／float精度副作用已备份后精确还原、diff空，旧Art/UI/Settings/ProjectSettings未改变。Root另核main checkout tracked clean，本worktree改动保持未提交。nonMain diff-check无错误，Main保留原五个Unity原生空字段空格例外；原host shutdown引擎提醒不隐去，不声称所有日志warning=0。Unity运行权已归还，无新完整项目suite／CI／commit／push／merge或下一Phase。

<details>
<summary>本轮RED、超时定位与修正历史（展开）</summary>

第二冻结快照runtime写入截止`2026-10-21 04:20:37Z`（EmployeeFlowController），永久test最后修正为04:25:24Z；全部临时阶段诊断已撤回。直接Edit `pickup-sides-final2-edit.xml` **289P/0F/0S**、CLI0，10.413秒（04:26:24Z–04:26:34Z），含原286范围及新增3项delivery-route checkpoint测试；root独立解析。该快照Play仍有下述4F，故不是最终完成结论；后续最终直接Play分129项与自然Continued1项执行，实际结果另列，不沿用含主动reserve探针的定位GREEN。

旧分离fixture的独立时序`collection-independent-baseline.xml`1P/0F/0S证明Ready后2帧可领取且员工仍Returning；不把等待一概归因于Idle门禁。旧`collection-before-return-red.xml`1P/1F中共享case在TrySpawn前置失败，未到时序，不能充当产品RED，已移除此探索case。Owner明确要求每个外露可达侧可交互，原固定role-pair不足以提供动态备用侧；已批准沿原plan追加实现。

首批domain RED：`pickup-sides-edit-red.xml` **0P/9F/0S**，0.224秒。编译可通过的旧行为stub下，1–4候选/四种旋转多Slot外露边与不同侧并持测试均实际失败；这证明候选/占位行为缺失，不声称真实人物移动已重现。首次test编译setup错误另存`pickup-sides-edit-compile-error.log`，无XML不计行为RED。正在最小实现；实际替代侧到达、仅一侧等待和直接回归在有效执行前均NOT_RUN。

真实场景RED：`pickup-sides-play-red.xml` **0P/1F/0S**，3.500秒；实际registered、无身体重叠的blocker挡默认Customer侧，原顾客真实到Counter后`TryReservePickup`仍拒绝“没有合法且完整可达的Pick-up等候位”。因此候选侧必须贯通最初wait头位及完整路线，不能只改最终领取位置。此为真实Scene/业务门禁RED，前一次fixture不存在API的编译中止另存，不计行为RED。

首轮候选实现的Edit：`pickup-sides-edit-green.xml` **52P/0F/0S**（新增Side9、既有Pickup26及P8 Resolver17），CLI0。首轮完整流程`pickup-sides-play-green.xml`仍 **0P/1F/0S**，文件名不代表GREEN；顾客已能预留备用wait头位，但员工在送达前遇到实际路线冲突，尚未Ready。后续诊断与修正须单列，不能用domain PASS代替真实送达／领取。

本轮窄review另要求验证：旧首选侧是外露孤岛而备用侧可完整服务时，P14 readiness不能仍被旧双anchor门禁拦住；静态仅一侧及两侧中一侧被身体占用时，Preparing顾客不得堵住唯一交付侧。两类尚无有效GREEN，列为待修／待验证，不预记PASS。

动态首项`pickup-sides-dynamic-green.xml` **1P/0F/0S**、CLI0，2.483秒。员工派发前新增既有remaining route冲突检查，并让Preparing顾客使用独立合法wait头位；原wholeowner case实际经过备用wait、Ready、第三侧领取、持杯/Pause及原Total实际出口释放。此为该项修后证据，尚不代表静态孤岛、单侧边界或最终直接cohort已通过；最终lease保留断言随边界cohort另核。

后续`pickup-sides-readiness-red.xml` **1P/3F/0S**、CLI0，4.156秒：dynamic项加入严格lease断言后仍PASS，Completed时Barista实际距交付点0.1000018m且原侧lease仍持有。静态孤岛项在正式Decor Confirm后`CurrentReadiness.CanOpenForBusiness=false`，是待修产品RED；单侧项误设Ready时顾客必已到wait、双侧项尚未成功spawn，均为fixture前置问题，不作为产品RED。三种失败分别诊断，不靠放松身体或路径规则让fixture通过。

边界最终`pickup-sides-boundaries-green.xml` **4P/0F/0S**、CLI0，7.889秒，root已独立读取XML与方法输出：原默认侧受阻且Barista仍持交付侧时，队首从第三侧领取并实际离店；正式家具围出旧North孤岛／radius不可用时，domain与owned Nav readiness均true，备用侧实际完成一单；静态单侧先等员工真实腾位再领取；两侧中一侧被registered body占用时，wait头不堵唯一交付侧，实际完成并排空。静态修正只由P14显式开启`CafeLayoutRuntime`／`LayoutReadinessEvaluator`／`NavigationLayoutAdapter`全部候选模式，旧默认pair接口与安全半径／预算保留。中间`pickup-sides-readiness-green.xml`仍2P/2F（文件名不代表通过），剩余失败是Done接待首帧及会重复调用TrySpawn的fixture前置，未据此放松产品门禁。

直接Edit `pickup-sides-edit-regression.xml` **286P/0F/0S**、CLI0，10.240秒（02:57:16Z–02:57:26Z），root／同一QA均独立读取：P14全部135、P8 Resolver17/Readiness27、P13 Binding24/Runner38/Lifecycle5/Legacy7、P12 Counter26/Admission4/Route3。纯lease case中retired-id两拒绝断言同时处于slot gap／已有deliveryOwner条件，不能单独称为新guard的自动证明；guard本身只读复核正确，属于Minor oracle限制。

随后直接Play `pickup-sides-play-regression.log`于02:58:23Z启动，**1200秒超时、CLI1、未生成XML**，03:18:24Z退出后host精确Editor／worker均0。该批实际执行但未完成，记INCOMPLETE／PASS-FAIL-SKIP计数UNKNOWN，不记0P/0F或NOT_RUN；旧P12 soak曾输出released ledger records=720，不能由该局部日志推断整个class／cohort通过。进程计算／响应与USB扫描也不代表业务断言推进。产品源码未因未知原因改动；先对20单1x及两个相关旧method加稀疏阶段观察，用300秒有限诊断定位，不提高原预算或降低断言。普通Editor补证在定位／有效回归之后执行。

有限定位`pickup-sides-localize.xml` **3P/0F/0S**、CLI0，20.019秒（03:20:43Z–03:21:03Z）：旧P12 `ThreeCustomersShrinkToTwoSlotsDrainAndRecover`／`WorldDisableKeepsVisitAndEnableRevalidatesActualPose`及P14 `CompletesTwentyRealOrdersAndDrainsAllCapacityAtOneX`均通过；后者18.802秒，20个真实出口／Order、三capacity、杯／handles／requests排空，原断言未降低。临时官方test callback及Twenty稀疏观测见`pickup-sides-localize-progress.log`，未复现首cohort超时，原因仍UNKNOWN，不能据此改产品或把原批改记PASS。后续只把相关直接范围分成两批300秒观测，不再盲跑原整批。test原固定`task9-real20-Normal.tsv`在此run成功时已被新样本覆盖，未提前另存是本轮证据限制；旧XML／log保留，新样本另存`pickup-sides-localize-real20-Normal.tsv`，不将它当成旧Task9样本。

旧类分批`pickup-sides-legacy-localize.xml` **47P/0F/0S**、CLI0，50.239秒（03:23:07Z–03:23:57Z）：`NavigationLayoutIntegrationTests`11及`CustomerQueueFlowTests`36全部有效结果，root已独立解析。它与剩余P14／P13范围分开记录，不把前面3项重叠定位再加总。

剩余批`pickup-sides-play-remaining.log`300秒超时、CLI1、无最终XML。官方start/end已定位旧`CashierNeverStartsWithSlotOnEarlierPickupRemainingPath`失败、`ContinuedArrivalsFillThenResumeCollectionAndDrain`默认180秒超时，之后五杯方法开始时整批到限。窄`pickup-sides-five-localize.xml`1P/1F进一步区分：Cashier的`unsafeStart=false`安全断言PASS，仅旧固定布局未再出现`explainedWait`；五杯方法实际5delivery／3显示／FIFO排空PASS，预规划9槽，排除“静态少于5槽”的猜测。

仅通过正式Decor Preview／Confirm／Done将该Cashier test的支撑从(6,6)移到(5,6)，保留真实route冲突证明、安全等待、未付款及同head恢复全部原断言。`pickup-sides-crossing-highload.xml` **1P/1F/0S**，182.915秒：Cashier此时PASS；Continued仍默认180000ms超时，未提高上限。阶段观测证明frame持续／paused=false，首单已真实退出；held-wave在12实际客人、8 Counter到位、4当前delivery时停于第5杯付款前的安全分配门禁，员工均Idle、无Blocked。150秒后正常Resume已开始actual drain，但被用例总180秒中断；Timeout后的TearDown NRE保留，不倒推为此前停顿原因。下一步只读比较原第5wait目标与经合法空交互侧的完整body-safe路线，区分有限规划漏路和合法空间反压；未证实前不改P11／planner预算、不删除五杯断言。

因果证据`pickup-sides-fifth-route-red.xml` **0P/1F/0S**、CLI1，54.918秒（03:45:44Z–03:46:39Z）：真实Counter head(-2.500164,.499536)到第5wait slot(3.6,-3.505)原`BuildRoute`拒绝；同一全量13身体／owned障碍不变，既有East正式侧(3.5,2.5)两段完整owned native path均通过原clearance .911检查，其他N/S/W不能通过，随后正式`TryReservePickup`仍false。由此证明是存在安全绕路但规划遗漏，而非只需降低“5杯”门槛；所有五杯、安全、FIFO、实际出口断言保留。

最小修正仅新增CFC私有`BuildPickupWaitRoute`：原planner拒绝后，限P14实际wait分配／移动／前移及其有限replan，以同一全量障碍逐个验证最多4个正式侧的两段完整native路径，再保留原wait goal交现有逐段执行。原`BuildRoute`、入店／Counter／领取服务位／exit路径及旧Planner／P11默认语义不改，24／32／3预算不增；不引入员工多段框架。带主动reserve的诊断探针须撤回，最终原Continued由真实service自然预留、实际移动再记GREEN。

定位GREEN `pickup-sides-fifth-route-green.xml` **1P/0F/0S**、CLI0，165.482秒（03:50:31Z–03:53:16Z）：14名wave顾客、5delivery/fullTrue、无Blocked、自然恢复领取、16个实际出口（含prefix与恢复客）、FIFO／exactly-once／三capacity排空／fresh interval均通过，explicitResume=False。实际visit7曾以East侧(3.5,2.5)为当前Nav目的地，余下route仍是原wait goal(3.6,-2.405)，证明真实逐段消费而非仅改readonly查询。该run含主动reserve探针，故只记定位GREEN；随后所有FIFTH_QUERY／phase／Until／callback／Twenty诊断已撤回，最终自然版另列。

最终runtime最后修改`2026-10-21 03:49:34.196238Z`。`pickup-sides-final-edit.xml` **286P/0F/0S**、CLI0，10.973秒（03:55:42Z–03:55:53Z），对应此runtime及不变的Edit用例；后续仅Play测试fixture时钟隔离修正，不改变产品源或Edit断言。

无诊断的`pickup-sides-final-play.log`再次300秒超时无XML，仍不记PASS。最小边界观测`pickup-sides-clock-boundary-progress.log`独立证明：NavLayout末项结束scale0／delta0／pausedFalse，P12 WorldDisable正常PASS后仍scale0，随后旧P13 `BothSpeciesCarryKeepsUpperPoseAndRealWalkWithOwnedCup`开始scale0并停在scaled `WaitForSeconds(.2f)`，无END。原60秒观察到限、无XML；这是真实可复现的test fixture时钟依赖，不能归咎CFC搜索，也不将各历史批事后改判。

仅`EmployeeAnimationTests.cs`新增SetUp保存原scale并设1x、Cleanup恢复原scale，原Carry骨姿／真实腿动画／Pause精度／倍速ratio及原capture finally全保留，生产GameTimeService／P11 shutdown failclosed=0不动。`pickup-sides-clock-boundary-green.xml` **13P/0F/0S**、CLI0，5.200秒（04:04:18Z–04:04:23Z）：同样继承scale0的Nav11＋P12WorldDisable1＋P13BothSpecies1全部完成，最后scale仍0。observer已撤回；剩余最终129与原自然Continued1按同runtime分别执行，不能用定位有探针结果替代。

`pickup-sides-final-play-green.xml`实际 **126P/3F/0S**、CLI1，174.982秒（04:05:46Z–04:08:41Z）；文件名不表示GREEN。排除自然Continued后的129项均有终态，之前clock hang已消除，但三项回归仍FAIL：`NoChangeAndCancelledPreviewPreserveOrderAndCheckpoint`取消preview后员工Layout changed Blocked；`FirstRegisterSecondMachineHaveSafeQueueAndMiddleSlotDelivery`真实布局流程等待超时；`PendingExitSegmentFreezesOnPauseAndRetiresEachCollectedVisitOnce`未观察到原fixture要求的Pending状态。三者先分别定位最早违约点，保留原安全／checkpoint／Pause／exactly-once断言，不预判全是fixture。自然Continued及普通Editor补证顺延，不能把其他126P与旧诊断GREEN相加冒称最终130P。本批两档真实20单样本已分别归档`pickup-sides-final-real20-Normal.tsv`／`Fast.tsv`。

最小只读诊断`pickup-sides-three-fail-diagnostic.xml` **0P/3F/0S**，13.564秒，确认三种原因而非同一超时：DF进入前已是flag=True，但正式捕获仍`MultiSidePickup=False`／revision2；Cancel无变化，Exit首次刷新为True／revision3后旧员工任务Layout changed Blocked。M017原ordinal正确，两合法侧的完整native路径都穿过原Cashier身体；同一全量两障碍／clearance .911的有限planner确有安全三段绕路，但员工尚未消费，不能只拿顾客planner结果放行。PendingExit的first实际退出frame12168早于second领取frame12189，旧距出口<.7m窗口不足以覆盖新增wait到service段；两客业务Completed／真实排空不替代Pending冻结覆盖。修正限policy首发前验证、P14 delivery腿实际安全绕行及测试更早的真实窗口，不改ordinal／身体阈值／原Pending断言。首次诊断编译错误的无XMLsetup日志另存，不计产品RED。

四文件最小patch及测试clock修正后，`pickup-sides-route-checkpoint-green.xml` **20P/0F/0S**（旧17＋新3）、0.255秒；新三方法验证route复制、末段request才能交付、Pause缓存到达、恢复当前段、旧中段回执拒绝、Cancel／Prepared新attempt、24段上限和最终side不变。只对P14 delivery腿消费实际native逐段路线，旧return保持单段。`pickup-sides-three-fix-green.xml`仍 **2P/1F/0S**、6.633秒：取消preview后的原订单/checkpoint可继续，M017原ordinal布局真实送达、领取、员工Idle／容量排空均PASS；PendingExit提早暂停仍未建立所需Pending，故不记修好。后者需正式guard下先让第二客实际接近service，再按原近出口窗口恢复领取；不改产品Pending逻辑、不注入Actor／route／callback。

Pending正式物理头前置`pickup-sides-pending-physical-head.xml` **1P/0F/0S**、CLI0，3.230秒（04:24:57Z–04:25:00Z）：先等待first实际腾位、second成为真实队首且Ready，再在HoldCollection期间通过正式`TryMoveToPickupService`安排真实接近，仍由原门禁阻止自动收取。frame1743 first近出口且second真实到service，1744实际PendingExit；原request0／generation11／segments0／replans0及6帧Pause冻结、两客实际出口／全部capacity0断言均通过。这是受控前置的Pending不变量验证，不声称Hold工具自身会自动提前接近。中间过早把canonicalCollected当物理头已腾位的前置失败另存，未改变产品物理释放合同；两条PENDING_STAGE临时观测撤回后再执行最终直接回归。

`pickup-sides-final2-play.xml` **125P/4F/0S**、CLI1，172.719秒（04:27:18Z–04:30:11Z）。四项全为旧`CafeEmployeeRecoveryTests`：Actor／Prepared／Working disable及CommittedDelivery disable；其正式fixture只设置员工integrated＋外部gate，Customer仍legacy、无P14 side lease，新增Environment逐段query却错误要求其具有CFC lease，最早返回IncompletePath。是新增检查作用域回归，不能改旧测试来绕过。最小兼容修仅以当前实际选择并复制的owned route事实限定新逐段门禁，旧默认单段保留原P11安全；selected P14 route仍逐段全量验证。修后需要重新执行直接范围，旧125P不充当当前同源结果。

兼容域修正只读`HasSelectedDeliveryRoute`限定当前integrated／active handle／实际复制的route；Retire及新attempt清route即false。`pickup-sides-route-compat-edit.xml` **20P/0F/0S**、0.199秒，保留原17并加默认false／选择后true／Cancel及新attempt false断言；`pickup-sides-route-compat-play.xml` **11P/0F/0S**、CLI0，11.919秒（04:32:07Z–04:32:19Z），包含旧Recovery6（前四FAIL全部通过）、Side4、原M0171。没有更改旧fixture绕过失败，已选择的P14每段仍检查原lease／全障碍。最终runtime及checkpoint test本次写入04:31:10Z，需最终直接cohort重新绑定。

原自然`pickup-sides-final-natural.xml` **0P/1F/0S**、CLI1，61.499秒（04:38:22Z–04:39:23Z），无主动reserve、原默认180秒。full5／14位真实wave及安全接待等待门禁已成立；恢复领取后V7活动路径仍需经过East(3.5,2.5)才到原wait goal(3.6,-2.405)，队首V2却同时取得该侧领取target。V7真实到East仅差.000436m后，其下一段看到V2的新owned target占当前点而明确Blocked，导致实际排空断言FAIL。是领取侧选择漏查其他客人剩余路线，不能将full5已成立当整个用例PASS，也不能忽略新lease让后客穿过。修正仅在collection候选取得lease／发请求前检查实际完整route及其他moving pickup的remaining corridor，冲突时等待或换合法侧；不改FIFO／预算／P11／原实流断言。

collection gate修后`pickup-sides-collection-corridor-green.xml` **7P/0F/0S**、CLI0，176.252秒（04:42:31Z–04:45:27Z）；包含原自然Continued1（163.351秒）、Side4、Pending1、Cashier1。自然版原默认180秒、capture0、原正式service、无主动reserve探针，full14／paid7／delivery5且blockedFalse；恢复后16个原客（含prefix与恢复客）真实离店、FIFO／原token／三capacity0／projection／freshInterval全部通过，explicitResume=False。此为最终CFC写入04:42:02Z之后证据，不用旧含探针GREEN替代；另外6项与主cohort重叠，不额外加计。最终主cohort需在同源刷新后才可与该唯一自然case合并为130个不同Play方法。

</details>

本轮永久文件范围（相对项目根目录，同组省略重复目录）：

| Files | 用途 |
| --- | --- |
| `Assets/Scripts/Layout/InteractionAnchorResolver.cs`、`LayoutReadinessEvaluator.cs`、`Assets/Scripts/Decoration/CafeLayoutRuntime.cs`、`Assets/Scripts/Navigation/NavigationLayoutAdapter.cs` | 枚举外露侧，并仅对P14启用全部候选的domain／owned Nav营业检查。 |
| `Assets/Scripts/Customers/PickupQueueService.cs`、`CustomerFlowController.cs` | 按侧lease、独立wait头、真实备用侧移动，以及拥挤wait目标的有限安全绕行。 |
| `Assets/Scripts/Employees/EmployeeStationBindingResolver.cs`、`EmployeeFlowController.cs`、`EmployeeTaskRunner.cs` | 可达侧绑定、稳定selected target、仅P14 delivery的实际逐段安全绕行及最终送达校验；旧默认调用与return保留。 |
| `Assets/Scripts/CafeLoop/CafeLoopController.cs`、`CafeLoopValidationController.cs` | 记录／释放实际交付侧，阻挡工具跟随实际送达目标。 |
| `Assets/Tests/EditMode/Phase14/PickupSidesTests.cs`、`CafeEmployeeCheckpointTests.cs`、`Assets/Tests/PlayMode/EditorSceneLoading/Phase14/CafePickupSideTests.cs`、`CafeStatusAndToolsTests.cs` | 新候选／占位／四类真实边界与送达route身份；旧Cashier crossing场景仅用正式Decor重建前提，PendingExit用正式port建立真实窗口，不删安全断言。 |
| `Assets/Tests/PlayMode/EditorSceneLoading/Phase13/EmployeeAnimationTests.cs` | 独立拥有并还原测试clock，避免合法暂停状态让scaled等待永不完成；不改动画或生产时间规则。 |
| `Docs/AnimalCafe_Project_Design.md`、`AnimalCafe_Development_Roadmap.md`、P14 design／test cases／implementation plan／Beginner Guide／本报告 | 同步已批准规则、实际覆盖和手测步骤；原Task9内部report继续记录本轮，不另建第二套plan。 |

临时callback／诊断探针／普通Editor helper均已从Assets精确撤回，原始证据／helper归档保留在outputs；它们不属于产品文件。正式Scene／prefab接线沿用已验收的顾客持杯版本，本轮未手改Scene或旧动画资产。

## Owner M-007反馈补修（2026-10-20，技术验证完成）

当前结果：直接Edit **166P/0F/0S**、Play **71P/0F/0S**，CLI均0，独立窄review **CLOSED，无残留Critical/Important**。普通Editor两species真实持杯/Pause/实际出口客观PASS；以下定向/回归counts重叠，不累加，历史失败保留。

- `customer-carry-edit-regression.xml`166P＝P14全部126（含两DomainReload方法和builder9）+P13Legacy7+P12Admission4/Counter26/Route3，9.928秒。该log截图目标文件CS0618=0。
- `customer-carry-play-regression.xml`71P＝CC5+CF9+DF10+ER6+P13Animation5+P12QueueFlow36，61.616秒；无跳过，不冒称当前P14全部Play或完整项目suite。
- 前序定向`customer-carry-prefab-green.xml`9P（完整rig接线/幂等/原body保护）、`customer-carry-behavior-final-green.xml`5P（两species持杯生命周期、early Pickup、两缺配置诊断/真实出口）均在上述cohort覆盖。

本次runtime C#最后修改为`2026-10-21 01:26:04 UTC`、永久tests为01:26:22 UTC（Owner本地仍为2026-10-20）；正式prefab/Scene接线由随后builder生成，Edit166/Play71在该最终接线后执行。普通Editor临时helper单独归档，不并入产品源码冻结。

- 截图warning：定位为`CafeLoopPlaySessionTests.cs`旧Unity Find API的CS0618；仅换FindAny及无SortMode重载，不屏蔽warning，原断言保留。`customer-carry-find-api-green.xml`真实DomainReload开/关两方法 **2P/0F/0S**、CLI0，每方法各两次真实Play服务与实际出口；编译log该文件CS0618为0。其他旧P11/P12/P13测试仍有既有obsolete warning，不声称全项目warning=0。
- 成功点单后立即去Pick-up：现有`CreatePaidOrder`已同次调用MovePickup且不依赖Ready；`customer-carry-behavior-red.xml`中`SuccessfulCreateMovesActualCustomerBeforeBaristaFinishesWorking`实际PASS，成功Create同次MovingToPickup，真实位置离Counter超过.1m时Order仍Preparing、Barista Working且剩余工作大于0。无需为已有正确行为修改Service。
- 顾客持杯离店：同批两species行为测试均在“canonical领取后缺CustomerHeldCup”断言真实FAIL，总计 **1P/2F/0S**。初版实现的`customer-carry-behavior-green.xml`仍 **1P/2F/0S**（文件名不代表结果）：P11顾客模型缺所查P13手部socket，cup未创建。只读兼容检查发现4个socket缺失，后续分类还发现head_socket在P13 skinning bones列表，未证明其有无非零权重；两次探索均0P/1F，原始XML保留。因此不靠忽略路径/猜测坐标兼容，最终两份新增P14顾客prefab复用accepted P13完整模型/Carry rig，移除EmployeeVisualPresenter，保留原Nav/body配置；旧P11/P13资产不改。其正式缺资源RED0P/1F到最终builder9P/0F，实际两species行为另以最终5P证明。
- 初版接线`customer-carry-wiring-green.xml` **8P/0F/0S**、CLI0，验证两appearance资源、安装幂等和旧Scene保护。新增test漏using导致的首次无XML编译中止另存`customer-carry-wiring-compile-error.log`；修test import后才有该有效结果。此8P不证明P11顾客模型具有P13 socket。
- 窄review发现缺视觉配置会静默空手退出，与约定诊断不一致；`customer-carry-diagnostic-red.xml`两项真实缺appearance/缺Cup prefab输入均实际离店，但应有的一次诊断为0，故 **0P/2F/0S**。仅补可见诊断及canonical事实先于表现的防护，不把未经重现的抛异常链写成实际故障。
- 最终5项真实行为：两species在实际世界移动时匹配既有Carry上身骨姿、腿部变化与杯子手部位置，无碰撞；Pause冻结，Blocked/NavigationActor组件及整个GameObject停用恢复、正式家具修改Done保留同一杯/原Actor/原Total，全部旧Order清空后仍实际出口并释放原token，杯随原Actor清理。两缺配置各一次warning且Completed/真实出口成立；早转队实测Barista尚余2.9游戏秒时顾客已离Counter约.12m。

普通Editor原组`agent-manual/Carry-20261021-013144/`两species状态均PASS，但1600×720首段背向镜头遮杯，只作状态证据。补组`Carry-20261021-013509/`正常沿原exit路线行进>.8m且自然转向镜头后，在1600×1600 Game view采集，沿用原Camera public API及其clamp，不改Actor位姿/CameraSettings；实际speed1.2、朝向dot约.9036。Root已查看[Shiba](../outputs/phase14/agent-manual/Carry-20261021-013509/customer-shiba-front-paused-carry.png)和[Westie](../outputs/phase14/agent-manual/Carry-20261021-013509/customer-westie-front-paused-carry.png)，手中直立杯清楚可见；两组均实际领取、Pause、同杯保留、真实出口/容量排空。此为Agent客观证据，Owner动画观感仍待本人复测。

本次原始XML/log均在`outputs/phase14/`；已按上述最终runtime/tests/prefab/Scene接线执行，不沿用旧完整suite作同源结论。

最终收尾见`outputs/phase14/customer-carry-final-cleanup.json`：host精确Editor路径进程0，临时helper/source meta已归档并精确移除，EditorOptions False/None、GameView index8/count12恢复；旧P12/P13四Scene/meta原字节保留，Main及两P14 Scene保留本次新顾客prefab引用。旧Art/UI/Settings/ProjectSettings无tracked diff，source/docs diff-check无error。两组捕获期间Console无Error/Exception/Assert；退出后的raw Editor log仍有JobTempAlloc引擎提醒，未将其隐藏或声称全部host日志无warning。修改保持未提交，Owner M-007仍待复测。

## Source与执行边界

- Workspace：`E:/Unity/Project/AnimalCafe/.worktrees/phase-14-integrated-takeout-loop`。
- Branch：`codex/phase-14-integrated-takeout-loop`；base/HEAD：`8c1582f7388191ff155d63b824b69c10d47cff3c`。结果针对本worktree未提交修改，不代表main或已发布build。
- Unity Editor：`6000.5.5f1`；Unity CLI：`1.0.0-beta.12`；Windows Editor测试。每次最多一个Unity Editor test process，常驻CLI helper与Editor分开识别。
- 源码边界：原Task9 runtime于`2026-10-20 08:35:02 UTC`冻结，test于08:48:59 UTC冻结；其第五完整Play为1319P/0F/5S、CLI0，08:53:38Z–09:11:56Z，受影响Edit254 PASS；完整Edit2723更早于guard／Nav修正。随后本次M-006／011仅新增CustomerFlow私有短暂让路门禁，runtime于15:37:05 UTC、永久tests于15:38:48 UTC冻结，修后受影响Edit157／Play90均PASS，未重复完整suite。旧完整结果是历史源码证据，不能冒称本次修后同源完整回归。
- 后续M-017仅修改多设备fixture配方并新增三个ordinal组合，focused四组合4P/0F；M-019仅修临时代测工具首次Play前的公开Search API初始化。运行时代码未再改变；旧P14全部54的合并Play计数属于新增这三个fixture之前，不冒称当前全部57项已重新整批执行。
- Sandbox首轮在WMI／Licensing初始化失败，未生成NUnit XML，不计行为RED。后续本worktree使用获准的normal host执行；未改依赖或安装Editor。
- 尚未commit／push／创建PR／merge，未运行CI或进入下一Phase。

## 自动验证记录

下表是各Task当时源码的focused／直接回归结果，重复用例不相加为独立覆盖总数；后续修改由Phase末完整suite及受影响直接回归确认，并分别注明源码边界。所有XML／log位于本worktree `outputs/phase14/`。

| Task | 行为RED及关键修复 | 当时最终GREEN：PASS / FAIL / SKIP | XML |
| --- | --- | --- | --- |
| 1 合同 | 编译后的16项中14项FAIL；身份与immutable snapshot补齐 | Edit 47 / 0 / 0 | `task1-green.xml` |
| 2 Pick-up queue | 初始21项FAIL；另补真实前移／新尾客门禁 | Edit 54 / 0 / 0 | `task2-advance-green.xml` |
| 3 顾客与容量 | Edit 5项FAIL、Play 4项FAIL；修复canonical已领取窗口与Counter计划站位冲突 | Edit 76 / 0 / 0；Play 43 / 0 / 0 | `task3-edit-final-green.xml`、`task3-play-final-green.xml` |
| 4 员工checkpoint | Edit 14项FAIL、Play 5项FAIL；补serialized模式及disable／恢复 | Edit 60 / 0 / 0；Play 17 / 0 / 0 | `task4-edit-final-green.xml`、`task4-play-final-green.xml` |
| 5 业务service | 初始24项FAIL；追加checkpoint、同步Pause与恢复重入RED | Edit 157 / 0 / 0 | `task5-reentry-final-green.xml` |
| 6 owner／杯显示 | Edit 13项FAIL、Play 4项FAIL；单一驱动与真实单客全流程 | Edit 32 / 0 / 0；Play 21 / 0 / 0；P12直接回归36 / 0 / 0 | `task6-edit-final-green.xml`、`task6-play-final-green.xml`、`task6-p12-green.xml` |
| 7 Done生命周期 | Edit 3项FAIL；真实Main首次Done门禁与旧delivery lease追加RED | Edit 76 / 0 / 0；Play 38 / 0 / 0 | `task7-edit-final-green.xml`、`task7-play-final-green.xml` |
| 8 installer／UI／工具 | 实际RED覆盖旧builder／验证copy、身体路线预留／前移、相向出口、HUD高度与Held文字；最终截图与兼容清理已核对 | Edit 43 / 0 / 0；Play 41 / 0 / 0 | `task8-edit-final-green.xml`、`task8-play-final-green.xml` |
| 9 完整覆盖与全suite | Edit覆盖52项firstPASS；真实Play 13P/3F暴露付款前路径门禁，窄回归46P；真实20单1×／2×通过；Grid／完整owner恢复2P、四次真实Play session的2项测试PASS；实际旧epoch菜单RED后2P；I-UX-01及Scene原生空白修正定向Edit126P／Play10P | 当时完整Play1319P/0F/5S；受影响Edit254P/0F/0S；原独立review无未关闭Critical／Important | `task9-edit-coverage.xml`、`task9-reserved-path-green.xml`、`task9-play-coverage-2.xml`（6P/1 fixture F）、`task9-grid-lifecycle.xml`、`task9-play-sessions-final.xml`、`task9-final-gaps-red.xml`（4P/1F）／`task9-replay-green.xml`、`task9-feedback-*-green.xml` |
| M-006／011后续窄修复 | 真实waypoint到达后因活动身体暂占下一段而立即Blocked，正式0P/2F；仅在未发request阶段等待实际腾位，取消后回原有限重算 | 受影响Edit157/0/0；Play90/0/0；窄QA CLOSED | `high-load-waypoint-red-behavior.xml`、`high-load-waypoint-green.xml`、`high-load-cohort-edit.xml`、`high-load-cohort-play.xml` |
| M-017／019后续验证修正 | 旧配方第二CR占入口的输入RED0P/1F；第二CR移(3,3)，四ordinal组合；工具在EditMode公开Search首次初始化后才Play，首次自然delay45秒BLOCKED保留 | 四组合Play4/0/0；普通Editor两项Agent客观PASS；窄QA CLOSED，runtime未改 | `m017-recorded-recipe-red.xml`、`m017-recipe-combinations.xml`、`agent-manual/G17-20261020-164313/` |

### 原Task9 Phase末回归与修复（M-006／011后续修复前）

| 执行／源码边界 | PASS / FAIL / SKIP | 证据与结论 |
| --- | --- | --- |
| 完整Edit，Customer disabled guard前 | 2723 / 0 / 0 | `full-edit.xml`，CLI0，1962.15秒，2026-10-20 05:45:54Z–06:18:36Z；P14全部124 PASS，原P8R冷加载字节稳定项PASS。 |
| 完整Play首轮，guard前 | 1315 / 1 / 5 | `full-play-first.xml`，CLI1／Editor2，1087.59秒，06:20:40Z–06:38:48Z；P14全部49 PASS，旧Touch停用装修组件后timeScale预期1、实际0。 |
| Customer停用行为RED | 1 / 2 / 0 | `task9-disabled-customer-red.xml`，CLI1；新增真实owner测试抓到disabled Customer仍启用Nav门禁，旧Touch原样再次FAIL；已有Visit保留／恢复firstPASS。 |
| guard后直接Play | 76 / 0 / 0 | `task9-disabled-customer-play-green.xml`，CLI0；P14全部51、旧Touch整组23、P11 shutdown2；旧Touch和原P11安全Pause均PASS。 |
| guard后直接Edit | 172 / 0 / 0 | `task9-disabled-customer-edit-green.xml`，CLI0，06:49:31Z–06:49:39Z；P14全部124、P12 Counter26／Admission4／Route3／Session1／Clock14。 |
| 完整Play第二轮，guard后／Nav修复前、旧动画采样 | 1317 / 1 / 5 | `full-play-second.xml`，CLI1／Editor2，1088.94秒，06:51:44Z–07:09:53Z；P14全部51与原Touch PASS，旧P13动画短wall窗口比例FAIL。 |
| 原动画单项／诊断整class | 1 / 0 / 0；5 / 0 / 0 | `task9-animation-original-repro.xml`、`task9-animation-window-diagnostic.xml`，CLI均0；不以单项PASS替代完整FAIL。 |
| 动画采样fixture修正后直接Play | 56 / 0 / 0 | `task9-animation-clock-green.xml`，CLI0，80.48秒，07:16:11Z–07:17:32Z；P13动画5＋全部P14 Play51。 |
| 完整Play第三轮，guard后／Nav修复前、动画fixture | 1316 / 2 / 5 | `full-play-third.xml`，CLI1／Editor2，1092.86秒，07:18:24Z–07:36:37Z；动画clock PASS，旧P13 carry计时与P14自然等待窗口断言FAIL；两项fixture已修并精确2P／相关cohort67P，第四轮完整结果另列。 |
| 两fixture修正后精确／相关Play | 2 / 0 / 0；67 / 0 / 0 | `task9-third-fixture-green.xml`、`task9-final-play-cohort.xml`，CLI均0；cohort为全部P14 51＋P13 MainCafe11＋动画5，07:45:35Z–07:47:22Z，106.63秒。 |
| 完整Play第四轮，低柜台认证修复前 | 1317 / 1 / 5 | `full-play-fourth.xml`，CLI1／Editor2，1095.14秒，07:49:04Z–08:07:19Z；P14全部51 PASS，两项fixture修复PASS；旧P11 detour出现MovementTimeout。 |
| Nav修复后受影响Edit | 254 / 0 / 0 | `task9-nav-edit-green.xml`，CLI0；P11全部82／P14全部124／P12直接48，最终runtime同源。 |
| Nav修复后相关Play | 224 / 1 / 1 | `task9-nav-play-first.xml`，CLI1；Nav真实recorded＋全部detour、P14全部51／P12全部81 PASS；唯一旧P13 carry wall计量FAIL。 |
| carry预算计量修正后相关Play | 67 / 0 / 0 | `task9-carry-budget-green.xml`，CLI0，109.19秒；P13 Main11／动画5／P14全部51。 |
| 最终完整Play第五轮，冻结runtime／test | **1319 / 0 / 5** | `full-play.xml`，CLI0，1097.7854724秒，08:53:38Z–09:11:56Z；1324总，inconclusive0，P14全部51 PASS／无SKIP。 |

Customer根因：旧`PrepareBusinessLayout`停用Customer flow以隔离Input／Time测试，P14 owner仍直接调用其Step，缺少disabled guard，导致初始化Nav业务门禁。仅在integrated Step的delta校验后检查`isActiveAndEnabled`，保留legacy、原Touch断言及P11 shutdown failclosed。新测试重读同一Capacity的当前ledger，验证原Visit身体／身份／时钟／Token／State保持，恢复后实际完成并排空。原Engineering窄复核无残留Critical／Important。完整Edit2723明确属于guard前快照；按已批准Task9风险范围以172项直接Edit补验，不将其冒称同源完整Edit。

动画fixture：第二轮唯一失败`EmployeeAnimationTests.PauseAndFastScaleWorkingExactlyOnce`的wall速率比3.54796863超出原1.7–2.3，Pause drift0。原样重跑与诊断均PASS；诊断capture0／maxDelta0.3333，normal advance0.208728716／game0.250631571／wall0.250236034，fast advance0.4166656／game0.500143766／wall0.2500534，wall速率比1.99766445、每scaled秒rate比1.00033712。当前证据支持Presenter只消费一次scaled clock；second单次异常的唯一触发原因仍UNKNOWN。仅该旧test改用固定capture帧时间、切速drain并采相同30帧，`finally`恢复capture；原Pause零delta／.001及1.7–2.3保留，新增实际scaled比2／每scaled秒rate一致／Working／Normal／speed1断言，不手动Animator.Update、不改产品。修后实测normal advance0.4166668／delta0.5000002、fast advance0.8333337／delta1.00000036，Pause drift0。原QA仅对此fixture窄复核CLOSED。

第三轮失败另记：`EmployeeMainCafeTests.RealGameTimeScalesWorkCarryAndFreezesCarrier`的Normal工作1.9979／2.9977秒、Fast1.0003／1.5002秒，真实相同路线距离1.9987／1.9974m；carry总时1.6660／1.0393秒，比0.6238535略超原0.5±0.12。静态发现仅1×在提交路径后Pause12帧再计时，2×立即计时；尚不能确认单次超差唯一触发。`CashierNeverStartsWithSlotOnEarlierPickupRemainingPath`四次start无unsafeStart，但没有自然出现特定wait；原fixture未显式建立路径冲突前提，不能据此认定产品门禁或状态坏。两原项不变focused重跑2 PASS，CLI0；仅据此不抹去完整FAIL。修复范围限真实fixture／测量前置，产品、Nav预算与阈值不动。P13两档都Pause12帧并确认真实PathComplete后恢复测完整路线，capture0／原ratio／Pause精度／实际送达回岗均保留。P14用正式clock固定合法1秒输入，在同Snapshot同步窗口只读证实真实active request／remainingRoute与候选位按production clearance冲突，未Paid／Order／reservation且非Blocked；同head随后正式Working，unsafeStart监听保留。精确2P后相关cohort67P，原QA窄复核两项CLOSED；不新增业务API、不挪Actor或写Route。历史单次异常唯一触发仍UNKNOWN。
低柜台兼容问题：第四轮唯一FAIL是旧`Phase11NavigationSceneTests.DetourAtVariableFrameRateMustArrive`。原selector三次只读诊断均PASS，不抹去完整FAIL。另从记录的合法位置`(.4999816,0,-1.959028)`创建真实native Actor后，正式移动在首个静态投影清零，重现MovementTimeout（`task9-p11-fresh-recorded-diagnostic.xml`，0P/1F，CLI1）。低Box高.42，胶囊底轴y=.45，完整skin半径.46；旧私有认证忽略高度且拒绝跨侧区域，无法证明实际合法切向移动。必要修复仅限`NavigationCollisionGuard.cs`的私有连续几何证明及P11 Edit／Scene regression；完整半径、skin、预算、到站与恢复规则保持。正式Edit RED17P/2F、Play RED0P/1F（均CLI1）可信；修后`task9-nav-edit-green.xml`254P/0F/0S、CLI0，08:35:46Z–08:35:57Z，含P11全部82／P14全部124／P12直接48。原Engineering与QA窄复核CLOSED、无残留Critical／Important；QA另核实际Native及四档detour均PASS。相关真实Play cohort为224P/1F/1S（226总），`task9-nav-play-first.xml`，CLI1／Editor2，08:37:16Z–08:45:51Z，515.21秒；P14全部51、P12全部81、P11 Movement33／Layout11／Scene39中的38均PASS，唯一SKIP为旧P11 opt-in capture。新增Recorded Native以retry0实际Arrived，原15／30／144／variable detour均PASS。唯一FAIL为旧P13 carry墙钟测试，详见下文；不将整cohort写成GREEN。最终完整Play中新增Recorded Native与原variable detour均PASS、retry0，整轮1319P/0F/5S。此处是新合法起点的同姿态机制复现，不冒称原完整suite的全部时序已重放。

真实20单：1×／2×各20位的实际收银、Prepared、最终送达、正确领取、实际出口及原Total释放，已在前两次完整Play和focused用例通过；额外Orders恰20／全Completed／owner唯一／Paid↔seen及最后容量、杯、handle、Nav requests排空均由真实owner断言。最终第五轮已重新写入并核对[1×逐客receipt](../outputs/phase14/task9-real20-Normal.tsv)、[2×逐客receipt](../outputs/phase14/task9-real20-Fast.tsv)；Normal于09:09:54Z、Fast于09:10:05Z写入本轮新receipt；各20独立Visit／Order／原Total token、三个阶段各1，最大出口偏差.00031623m／0m，均小于原.08m。对应两个NUnit用例与`P14_REAL20` trace均PASS，实际出口各20，三capacity／杯／handles／requests全部0。使用既有Shiba／Westie prefab，未声称新增正式Species。40E／24P的实际method与证据层级见[Test Cases](superpowers/specs/2026-10-19-phase-14-integrated-takeout-loop-test-cases.md)，不把fake port GREEN当作真实导航闭环。

本次相关cohort的旧P13 carry FAIL有新的直接证据：Normal墙钟1.6661秒、motion budget1.66610241；Fast墙钟1.3472秒、scaled2.23580837、budget1.83580923，出现1个长帧（unscaled .5626621、scaled .6666667）。World原合同将该帧运动预算限制到约.2666667，超出的.4 scaled不补走，但deadline仍消费完整scaled时间。raw wall比.808613658因此不能单独判定2x错误。原QA窄核后批准仅test计量修正：同帧Σmin(delta,MaxSubstep×MaxSubsteps)除以固定所选倍率1／2，比较原.5±.12；不丢弃stall、不按实测speed／distance归一化、不改产品或预算。原raw wall／scaled／budget／cap日志、capture0、Pause与真实Delivered／空手／回岗断言保持；正确名称为budget等效运行时间，不能据此声称卡顿时wall一定减半。此前完整第三轮的单次异常唯一原因仍UNKNOWN，不用本次证据回填历史。修后`task9-carry-budget-green.xml`67P/0F/0S，CLI0，08:49:26Z–08:51:15Z，109.19秒；P13 Main11／动画5／P14全部51。两档budget等效时间1.66596735／.918051362，ratio约.551，原阈值与真实断言通过；本run capFrames0，不冒称强制stall实测。原QA读取patch与实际XML后Scoped CLOSED。最终完整Play1319P/0F/5S、CLI0，其中该真实carry测试继续PASS；本轮carry capFrames0，不冒称强制stall验证。

Task8追加FIFO证据：`task8-pickup-fifo-red.xml` 为0 PASS / 1 FAIL；`task8-pickup-fifo-green.xml` 为26 PASS / 0 FAIL / 0 SKIP。真实五客drain发现后客获得仍在前移的付费前客前方空位；修复限制候选必须在所有paid前客owned Slot之后，仍允许后方合法并行前移。该纯queue结果不单独证明五杯完整runtime。

最终完整Play的5项SKIP已按本轮XML逐项核对，均为既有opt-in截图／几何audit；P14无SKIP：

| 原有test | SKIP原因 |
| --- | --- |
| `P8RCashRegisterSideIndicatorTests.CashIndicator_NativeOverlayCapture_WhenExplicitlyEnabled_WritesUniqueReviewGallery` | 未启用`ANIMALCAFE_CASH_SIDE_CAPTURE=1`原生截图。 |
| `P8RCompactChromeTests.SpacingAudit_WhenRequested_ExportGeometry` | opt-in spacing geometry audit。 |
| `P8RReadinessSafeAreaTests.ReadinessChecklist_CaptureNativeExamplesWhenRequested` | opt-in native Game View evidence。 |
| `P8RUiEnhancementSceneTests.NativeUiExamples_WhenRequested_CaptureActualGameView` | opt-in real Editor Game View capture。 |
| `Phase11NavigationSceneTests.CaptureRepresentativeCanvasAndCamera` | 未启用`ANIMALCAFE_P11_CAPTURE=1`原生截图。 |
## Scene、UI与manual准备

首次完整EditMode（`full-edit-pre-feedback.xml`）：**2722 PASS / 1 FAIL / 0 SKIP**，CLI1／Editor2，1958.36秒。唯一失败为旧P8R `AuthoredCompleteUi_ColdAndRepeatedFrameLoadKeepsSerializedBytesStable`：原Task8清尾空格使MainCafe新增5个组件的空`m_Name:`偏离Unity保存的`m_Name: `，冷加载后字节不同。测试自有source／loaded副本只有这5处空白差异，无几何或业务变化。保留Unity原生序列化、保持原测试；合并菜单反馈修复后重新跑完整Edit／Play。该历史结果不算完整PASS，也不覆盖最终源码。

MainCafe接入一个P14 owner，复用原Customer／Employee／Time／Navigation与HUD，旧业务写入口在P14模式隔离。独立P14分离／共享Scene由Editor API创建；具体坐标见Beginner Guide；最终 `task8-play-final-green.xml` 41 PASS / 0 FAIL / 0 SKIP，含两fixture五杯／三杯补显示／paid FIFO领取／实际离店及三容量归零、Tools7（含pending出口Pause）、Main partial11、P14顾客7／闭环6、原UI输入10。早期 `task8-direct-play-green.xml` 26 PASS / 4 FAIL以及Main/UI 21 PASS / 1 FAIL均保留；错误test patch已纠正，真实相向出口缺陷已用P14发起前安全门禁修复，未改P11。最终native `task8-native-final-green.xml` 1 PASS / 0 FAIL / 0 SKIP、wrapper0，Editor退出后原P12/P13 Scene+meta字节全部恢复。Task8手工清Main新增空白的做法后来触发P8R冷加载失败，当前已恢复Unity原生的5个空`m_Name: `字段，保持Scene字节稳定；nonMain diff-check通过，这5处原生空格是明确记录的例外。原P13独立Scene保持原受控派单模式及资产内容，完整Edit曾确认兼容；最终完整Play已通过；Editor退出后，原P12／P13 Scene＋meta literal4/4核对一致。

代表真实Game view截图：[1600×720](../outputs/phase14/task8-screens-height-fit/status-1600x720.png)、[1080×1920](../outputs/phase14/task8-screens-height-fit/status-1080x1920.png)。原Time／Decor实际raycast与真实五杯完整drain在同一native用例PASS；root及独立UX均已view_image，横屏13.5px诊断文字可辨认。独立Engineering完成，Critical／Important／Minor均0；QA核对40E／24P，未发现新增Critical／Important，3项manual旧文案Minor已改且原reviewer复核CLOSED。UX的I-UX-01已修复：菜单统一向Console发布`[P14 tool]`真实结果，PrintSnapshot独立打印并保留最近反馈；`task9-feedback-red.xml`0P/3F后，`task9-feedback-edit-green.xml`126P/0F/0S（全部P14 Edit124+原P8R稳定性2）及`task9-feedback-play-green.xml`10P/0F/0S，CLI均0。原UX reviewer读取源码／Guide／XML后将I-UX-01复核CLOSED；独立review没有未关闭Critical／Important。最终完整Play1319P/0F/5S及最终源受影响Edit254P/0F/0S已通过；Owner视觉验收另记。原design的三项文档finding关闭不替代本轮实现review。

## Owner与platform结果

### Agent Editor场景代测（2026-10-20，执行已结束）

首轮代测结果：18项功能流程中16 PASS，M-006／011的高负载恢复分支BLOCKED；四项客观检查中M-007／021 PASS、M-017 BLOCKED、M-019的UI子项PASS但Editor启动SearchDB异常待查。原B的Barista Blocked与Collect的第6顾客Blocked尚不能归为同一根因；M-017实际新GUID绑定后无可用队列站位，未达实际送杯／旋转前置。Owner随后授权先修复M-006／011，当前进度见下方同名小节及Guide。以下保留真实执行历史和helper前置修正，不能只取历史某条PASS代表全部闭合。Owner观感／真实操作验收仍NOT_RUN，Phase14尚未Completed。

Owner在本chat明确授权代测。继续使用原M-001–022前置与预期，不重建test matrix。M-001–006、008–016、018、020、022为Agent功能流程；M-007、017、019、021另做客观验证与原生画面准备，Owner感官／真实操作验收仍单列NOT_RUN。

执行方式：仅新增Editor代测脚本`Assets/Editor/Phase14/Phase14AgentPlaythrough.cs`，普通graphics Editor实际Enter／Exit Play，调用正式Decor／Time／验证菜单／组件启停并观察真实业务与native导航；不运行NUnit test methods、直接Create／Complete订单、注入Arrived或挪Actor。界面事件是Editor工具触发，不能声称OS鼠标／touch手测。native GameView PNG及逐项操作记录写入`outputs/phase14/agent-manual/`，结果回填Guide的Agent列。初始各项NOT_RUN，不借历史自动PASS充当新增代测结果。M-001 pilot现为Agent PASS：`pilot-20261020-134537/results.tsv`与`operations.log`记录13:45:49Z自动Paused且LastExplicit为空，正式preview／Confirm／Done后13:46:01Z实际Normal、真实顾客Order与两员工；无显式Play选择。root已查看[正式Decor](../outputs/phase14/agent-manual/pilot-20261020-134537/m001-01-confirmed-decor.png)与[自动营业](../outputs/phase14/agent-manual/pilot-20261020-134537/m001-02-automatic-business.png)两张真实1600×720 GameView。Editor自然退出后5份Scene／meta与本次原bytes一致、旧资产diff0。其余项目待本次实际操作，不借pilot代表通过。

pilot启动期记录UnityEditor.Search.SearchDatabase IndexationOnStartup异常，发生在代测Play前；运行期Console另行核对，暂不据此填写M-019全Console清洁。首次helper Camera名称冲突的编译失败、额外启动的项目锁窗口分别留log，不计产品manual FAIL。

B组`B-20261020-135605`实际操作13:56Z–13:59Z：首单Completed后实际离场、末次身体采样恰在出口、同一原Total Released并排空；四阶段Pause各至少2现实秒、位置／动画／工作remaining／订单均未推进。M-007客观Working→Carry→Ready→空手Return→Idle通过，16张真实时间序列PNG及working图已保存，root已查看working／carry／return代表帧；Owner观感仍NOT_RUN。M-004观察到7次新Cashier handle均对应实际Arrived的Counter队首，真实ServiceFinished约2游戏秒，Counter实际最多≥3，Agent PASS。M-002离场途中Total仍Used的逐帧检查将在后续自然单补足；M-008的2×子项尚未执行，不提前写整项PASS。

B组后续Hold Collection并持续自动到店时，Barista在当前delivery4已成立后的Returning阶段进入Blocked（尚未第五次送达），最终visits12／orders7／completed1／Counter6／Pickup6／可见3／delivery4，等待180秒后正常Stop并保留GROUP-B BLOCKED。M-006 Agent BLOCKED继续查实际failure；M-003／005／011尚未达到操作前置，未借首单或旧NUnit填写PASS。下一步最小只读snapshot／native图诊断该现场，正常领取的20单分支独立执行。此前结果与编译失败log均保留。
后续`BD-20261020-140200`只读有限probe在fresh直接Hold Collection前提下达到16位顾客／11个delivery／3可见杯，两员工Idle，没有复现Blocked。`probe-outcome.txt`明确NOT_REPRODUCED；raw wait-timeout仅为诊断截止，不是产品失败。它与原B“首单＋Pause＋Drain后再Hold”的prefix不同，不能用此结果关闭原B的UNKNOWN。

独立`20-20261020-140920`普通Editor正常自动领取，14:10:40Z M-003 Agent PASS：20个不同Visit／Order／原Total token、20行实际最后身体采样离场记录，均Completed＋同一原Token Released；排空后三capacity／杯／delivery／handles为0。root重新解析20行，unique各20、badExitState0。这里是last-sample departure observations，非新增exact callback receipt证明。新增实时ledger检查未见途中提前释放：仍在路途中的注册身体不得Released；已到真实出口范围并注销后允许Unity Destroy帧末延迟。结合B首个1×完整单，M-002 Agent PASS。Cashier实际约2游戏秒、Barista约3游戏秒的start／finished记录及2×/TimeScale2已核，结合B四阶段≥2现实秒Pause不推进，M-008 Agent PASS。M-005仍需Hold至少3名后再Resume的完整步骤，不只借正常20次序观察升级。
M-022按本次授权实际运行正式installer两次；启动前备当前P14 Main及独立P12／P13 Scene＋meta原bytes，逐次比较，只允许测试临时写入，最终恢复当前未提交基线，不还原Main到旧HEAD。M-020保存并恢复原Enter Play Mode设置。无依赖安装、产品规则修改、全suite重复、commit或Phase完成授权。

`Fill-20261020-141443`按首单正常完成及排空后再Hold的前序，已实际达到delivery5／可见3；root查看`m006-five-real-deliveries.png`，HUD显示Waiting／预留站位已不可达。后续helper错误地等到逻辑Total满额，未按M-011允许的有效空间不足条件继续Resume，180秒后停止；停时Counter8、Pickup5、两员工Idle、无failure，原Scene5/5恢复。该GROUP-Fill BLOCKED属于代测流程等待条件过强，不能算Barista或产品Blocked，也不关闭原B未知原因。M-005／006补显示与M-011恢复部分待正式Resume分支完成；已有第五杯图只证明积累阶段。

`Collect-20261020-142130`正常Hold Collection的下一次独立操作于14:22:11Z发现真实顾客Blocked，保存状态及native后停止。具体是第6顾客（Visit后缀00000006），generation9／request0，最终目标`(3.6,0,-3.505)`，记录路径由`(2.499319,0,3.591964)`到该目标；员工Cashier Idle、Barista Returning，均failure为空。本次不能称Barista Blocked，也不能据此断言与原B同一根因。高负载领取／补杯／恢复分支仍BLOCKED，继续其他独立项；不反复随机重跑或用较少入店人数关闭该记录。

`C-20261020-142250/results.tsv`与操作记录已由root核对：M-009、M-010 Agent PASS。真实已注册送达障碍造成Prepared／Preparing Blocked，移除并Resume两次后原key实际Ready，Working handle仍仅一个；真实已注册回岗障碍造成Returning Blocked，顾客在员工仍Blocked时实际领取Completed，随后移除／恢复只回岗并全排空。两项均保存Blocked原生图及状态。
M-019的UI／native客观子项通过，root已查看本次[竖屏](../outputs/phase14/agent-manual/C-20261020-142250/m019-native-input-1080x1920.png)与[横屏](../outputs/phase14/agent-manual/C-20261020-142250/m019-native-input-1600x720.png)：实际1080×1920／1600×720，正常Camera pan／zoom，eligible且topmost raycast的时间／Decor／Done Editor事件，当前UI与readonly Snapshot匹配，无修改Done不改变epoch／订单数。它不是OS鼠标／touch体验；因普通Editor启动阶段多次出现SearchDatabase ArgumentOutOfRangeException，M-019不填写无条件全项PASS。运行期与启动期异常分开，Owner可读性和操作感仍NOT_RUN。C组退出后原Editor options／GameView index8与count12／Scene5/5均已恢复。

`D1-20261020-142722`在Cancel Preview前停止：脚本拟选柜台支撑，实际pointer选中上层Pick-up functional Preview。root已看stopped native，属于点击前置不成立，非产品异常；不以之前无修改Done子段填写整项M-012 PASS。原始结果保留，Scene及设置恢复，后续只允许一次真实可见侧面hit修正；不伪造hit或直接BeginExisting绕过界面入口。

`D2-20261020-143220`已记录M-013／M-016 Agent PASS：同一实际已领取且出口Blocked的顾客跨两次正式CM旋转Confirm／Done，原Actor／ID／位置／原Total保留，其他顾客、旧Order／delivery清空，同两位员工身份不变，主动Pause仍生效。正式Store必要CM后无效Done保持门禁；正式放回CM并Confirm／Done后自动Normal，不显式点Play。M-021仅主动Pause客观分支通过；root查看本次[Done前](../outputs/phase14/agent-manual/D2-20261020-143220/m021-before-done-00-f2066.png)和[Done后](../outputs/phase14/agent-manual/D2-20261020-143220/m021-after-done-04-f2322.png)，保留者画面位置连续，另一个未取餐者清除。所存3／5张间隔采样图不替代同步事件顺序证明，也尚未覆盖原M-021的1×进入Decor分支。Owner闪动／观感仍NOT_RUN。
本组后续先等新订单再移出口blocker的代测顺序导致新客安全前置无法达成，60秒后保存Dump／native并正常退出，Scene5/5恢复；M-014实际恢复离店和M-015旧callback分支尚未完成，未因M-013通过而自动填写PASS。下一次小分支先正式移blocker／Resume让原客实际退出，再等新订单与Replay；不改变业务或跳过安全门禁。

`E-20261020-143413` M-018 Agent PASS，root已核对结果与末次Snapshot／cleanup：实际Working停用owner组件保存remaining与capacity，恢复继续；Prepared阶段停用Barista Actor保持原位，重新启用并Resume继续送达；实际Collected客人Actor停用时原Total不释放，恢复后原客实际退出。原session、两员工ID与一单保持，未重复制作已完成阶段，最终容量／杯／delivery／handles排空。原Scene5/5、Editor options、GameView恢复；不声称停用等同离店。

`D1-20261020-143538`完成一次点击前置修正后M-012、M-005 Agent PASS。脚本先用真实classifier确认可见柜台侧面为Furniture，再queued pointer选中原支撑，正常Move Preview→Cancel→Done；无修改Done及Cancel均保持epoch、三份原Order、delivery及一个实际手持杯。随后正式Resume Collection，三名顾客依原paid／physical head顺序只领取自己订单，全部Completed、实际离店及原Total释放，最终排空，cleanup verified。该结果满足M-005原“至少3名”前置；不关闭原B及Collect的高负载Blocked，不以三客成功替代M-006五杯恢复或M-011满站位恢复。

`D2-20261020-143653`已完成顺序修正后的M-014／M-015 Agent PASS：原已领取且跨两次Done保留者在正式Remove／Resume后实际退出、原Total释放；随后真实新epoch订单已存在，Replay日志明确`source=old-epoch`及完整实际token／actor／handle／request／generation／revision，当前新单事实不变。之前D2 timeout作为代测步骤顺序问题保留。
本次也补M-021正常1×分支：有未完成订单时进入Decor，正式旋转CM／Confirm／Done，epoch清场、自动Normal，保存before／after四张间隔采样图，root已看末张。本分支没有仍在场的已领取保留者；其身份与位置连续来自本组前面的主动Pause分支，两种实际前置分别记录。M-021客观部分通过，Owner观感及精确同步门禁顺序证据的边界不变。退出后Scene5/5及Editor／GameView设置恢复。

`FOn-20261020-143840`完成第一轮真实完整单后，helper使用的delayCall未再次进入Play，第二轮未执行，不算产品FAIL或整项M-020 PASS。关闭本工具拥有的Editor后以单独ordinary Editor恢复；`recovery.txt`明确原False／None、GameView8／12及Scene5/5已核对。helper仅改为SessionState＋Editor.update推进，再执行原两种Domain Reload各两轮，不以四个不同进程冒充同Editor重复Play。

后续`FOn-20261020-144258`、`FOff-20261020-144441`各在同一普通Editor完成两次实际Play，M-020 Agent PASS，root已读取四条PASS和cleanup。每轮新的实际session、初始Order／delivery／杯为空、一套owner／UI、同轮实际两位员工；真实一客完成收银／制作／领取／原身体离店及原Total释放，最后三容量／杯／delivery／handles归零。root也查看On第二轮native，实际只余两员工。两组分别使用None／DisableDomainReload，最后均读回最初False／None、GameView index8／count12、Scene5/5；启动SearchDB异常仍独列，不据此声称全部Editor Console无Error。

`G17-20261020-144646` M-017 Agent BLOCKED。从fresh Main在员工出生前，经正式Catalogue／Preview／guard／Confirm完成Guide五支撑、CR×2、CM×2、1×3支撑90°及中间slot.1的Pick-up，Done自动Normal成功；新生成设备GUID的ordinal实际选择第二CR`374b…`与第二CM`1946…`，不同于固定ID自动fixture。40秒无顾客生成，HUD为“营业中／队伍没有可用站位”，两员工Idle且无failure；root已看[现场原生图](../outputs/phase14/agent-manual/G17-20261020-144646/operation-stopped-native.png)。不强改GUID／Actor／站位，不重复随机布局尝试。实际交付中间Slot、旋转后新杯及Owner观感未达执行前置，不能用P-017固定ID自动PASS补填；当前也不足以认定单一代码根因。退出后Scene5/5及原Editor／GameView恢复。

`I22-20261020-144841`两次实际installer已逐次保存并验证全部五份raw bytes不变。后续代测错误要求旧HUD整体inactive；root核对正式installer明确复用该HUD／Status给P14只读UI，仅停用旧controller并隐藏旧Buttons，所以此断言属于helper错误，不是产品FAIL。旧结果及现场保留，只修正一次oracle为旧controller停用、旧派单Buttons隐藏、共享HUD由唯一P14 view使用，再测正式legacy调用拒绝及独立P13受控入口；不改产品。

`I22-20261020-145018`完成M-022剩余项，整体Agent PASS：Main只有一套owner／UI及两员工，旧controller关闭、旧收银／制作按钮隐藏，同一HUD由P14只读view复用，正式TryStartTask／TryLetFrontLeave明确拒绝。两次实际installer的逐次raw5/5证据沿用前一run，本run不重复安装。随后同Editor进入未改写的独立P13 Scene，无P14 owner，eligible且topmost raycast的真实Barista Task按钮启动Working→Carry→实际delivery；root已看[独立P13送达图](../outputs/phase14/agent-manual/I22-20261020-145018/m022-independent-p13-controlled-delivery.png)。最终cleanup记录False／None、GameView8／12、Scene5/5恢复，无产品规则修改或Git publication。

最终清理已完成：临时helper及meta在`git ls-files`均为空，确认仅属本次untracked文件，逐字归档到[代测source及SHA256清单](../outputs/phase14/agent-manual/source/sha256.tsv)后仅移除这两个Assets路径；root重新计算归档hash与清单一致、确认两源均不存在。helper SHA256为`4C1B46B3F62B7261E68A097F22E16D0C810B274FB2D863A008E852AC835EBB01`，meta为`BA279730BD3A378447E35AE8B85383F3D754D459D65401C8F88521A59880B944`。全部raw日志／PNG／baseline保留，未删除其他源码或资产。runner核对完整Editor路径进程0；root重新核对当前Main及P12／P13 Scene+meta与本轮baseline5/5一致、旧资产tracked diff0、主目录tracked干净。产品runtime最后修改仍为08:35:02.5114502Z，本轮代测未改产品或原NUnit tests；清理后没有再运行Unity或完整suite。

### M-006／M-011受阻定位与修复（本次窄修复完成）

Owner已明确要求“如果有问题那就修复先”。定位开始时，实际第6个Pick-up目标、途中路径与完整身体安全的差别尚需诊断；已有五杯自动测试在第5客即Hold Arrivals，未覆盖持续到店达到有效站位不足后的恢复。因此先在既有`CafeStatusAndToolsTests.cs`增加只读诊断／真实行为测试，根因RED成立后才最小修改产品。角色大小、完整skin、到站容差、移动／恢复预算及正式Done规则保持；不改GUID／挪Actor／伪造Arrived，不用减少人数隐藏原现场。M-006／011在诊断中保持BLOCKED，最终真实恢复补测通过后才更新Agent PASS；以下保留全过程。M-017与Editor SearchDB异常不纳入此次窄修复。

静态假设核对：`high-load-slot-diagnostic.xml`、`high-load-recorded-geometry-diagnostic.xml`各为诊断1 PASS，不是bug修复GREEN。前者实际生成11个折返位（z=-.7），与历史直排至-3.505再转向第6位的几何不同；后者两历史literal点分别解析至x=3.55／z=3.55，与原点约.05／.042m，原radius＋skin=.46完整capsule对本World注册solids均无hit。因此不支持预设“目标静态非法”，没有据此改产品。原B日志已精确确认delivery4后Returning→Blocked；下一步记录首次受阻同帧顾客／员工完整remaining路径及真实callback失败，区分双方走廊竞争。独立QA完成只读链路分析，尚未作修复通过结论。

首次连续真实行为分支于15:08:02Z–15:10:35Z完成，XML1 PASS／0 FAIL，152.5877047秒，产品未改。现场13位实际顾客、delivery5／可见3、Counter8＋Pickup5，出现安全Pick-up分配Waiting；正式Resume Collection后排空及新到店间隔通过，`explicitResume=False`且未捕获Blocked。本次虽规划到历史x3.6尾位，但并未实际分配给第6位，不能关闭历史现场；完整正式回归还要加入noUnexpectedBlocked、原Token实际离店、一次付款／制作及FIFO强断言。也发现该case在Setup固定1/60加载Scene后才切capture0，与原ordinary Editor从启动即capture0不同；后续合法1秒到店输入实验需从加载前保持capture0，区分初始化队形和调度时序，不能用本次偶然PASS代替根因修复。

合法1秒到店输入的强化分支`high-load-one-second-first-pass.xml`为1 PASS／0 FAIL，148.291372秒；从Scene加载前使用capture0，保留正式付款／制作／FIFO、原Total存续及实际离店回执断言，实际13客／5delivery后普通Waiting并恢复排空。仍未分配历史第6个尾位，也未复现Blocked，因此不是产品修复GREEN。随后普通graphics Editor原生启动的`high-load-native-startup.xml`为0 PASS／1 FAIL，152.0135581秒；高负载与恢复已实际执行且未Blocked，唯一失败是预期15条离店回执、实际14条：最后一客已到出口、注销并释放原Total，但Unity的Destroy尚待帧末，测试即停止收集。仅修正test收尾为记录最后实际事实并等待一个真实帧，再核对exact回执，不修改产品或伪造销毁。两份原始结果均保留。

为区分初始化与先前操作时序，本轮临时恢复已逐字核对hash的原普通Editor代测helper，保持原四次Pause／首单排空／默认随机到店；仅增加首次Blocked前的只读路径ring及同帧原样转交的真实callback记录，并在M-006／011后结束。此次临时恢复及最终再次清理另记，不改变前一轮已归档清理事实。

`B-20261020-152911`已在首次Blocked停止：frame9341的request37真实返回Arrived／None，第6位Pick-up顾客（Visit7，首单已离场）实际到达`(.407882,0,1.5)`；同帧Barista持request41从`(1.609194,0,3.5)`返回`(.5,0,3.5)`。本次支持Collect的“绕行点Arrived之后下一segment同步受阻”链路，不能称原B员工Returning失败已同因关闭。helper异常收尾另有缺陷：第一次捕获Blocked后下一Update仍再次执行同一首Blocked拦截，导致FailureEvidence尚未推进即Stop，所以本次未采到Visit Failure／Replans／counterfactual；保留真实前后callback／body ring，不把缺失字段描述为已采到。Scene5/5与原Editor／GameView设置已恢复。后续使用最小稳定行为RED确认临时移动身体占路的等待条件，不继续无界随机重跑。

可信行为RED：`high-load-waypoint-red-behavior.xml`实际0 PASS／2 FAIL，CLI1／Editor2；两case均由真实planner生成绕行，客人实际到达`(1.999427,0,1.595822)`后，下个目标`(4,0,1.1)`仍被正在正式MoveTo腾位的完整身体`(4.057775,0,1.332942)`暂占，即同步Blocked“入店：通道被占用”，Segments2／Replans1，准确失败于应保留未发candidate普通等待的行为断言。此前首次fixture两候选间仅.8006m、不足合法出生前置的0P2F另存，不能冒充业务RED。最小修复在CustomerFlow私有逻辑增加短暂等待：未发下一request时检查真实活动腾位身体，全body实际清空才发出；不改变P11和已发请求的失败／预算，取消或停住仍占位则回原有限重算流程，不每帧A*自动恢复。原QA仅窄复核该patch和真实RED/GREEN。

初步GREEN `high-load-waypoint-green.xml`为CustomerFlow全class9 PASS／0 FAIL／0 SKIP、2.874秒、CLI0；同实际窗口现为MovingToPickup／request0／Segments2／Replans0，暂停不推进，活动body真实离开后原客到站；另case取消body仍占最终goal后回原有限重算、明确Blocked并保留token。最终test另加pending确实退出的断言，不放宽原失败条件。当前private patch之外无共享Navigation改动。修后受影响Edit `high-load-cohort-edit.xml`为157 PASS／0 FAIL／0 SKIP、8.6626132秒、CLI0，实际含P14全部124及P12 Counter26／Admission4／Route3。

最终合并Play `high-load-cohort-play.xml`为90 PASS／0 FAIL／0 SKIP、264.5252999秒、CLI0，15:40:00Z–15:44:25Z；P14全部54（Customer9／Decoration10／EmployeeRecovery6／Integration8／Regression13／StatusTools8）＋P12 CustomerQueue36。两个新case及取消后pending=false均PASS；continued分支149.576881秒，13位真实wave／delivery5／full=True／blocked=False，15个原Token实际出口回执，正确补显示、FIFO、全排空与freshInterval=True／explicitResume=False均PASS。原独立QA读取最终源码与两XML后将本次窄review CLOSED，无Critical／Important；普通Editor M-006／011另测，不以NUnit回填Agent／Owner。历史完整suite不冒充本次修后完整验证。

最终普通Editor补测`B-20261020-154541`正常退出：保留默认随机到店、四阶段Pause及首单实际离店／排空前序，再持续到店至真实安全分配门禁。root直接核对results.tsv的M-006／011 PASS并查看[实际五delivery／三可见杯](../outputs/phase14/agent-manual/B-20261020-154541/m006-five-deliveries-three-visible.png)：HUD Cups3/5，Cashier Idle／h0，Waiting原因是前客remaining route占用预留位，Barista Returning且无failure。此处验证有效空间门禁，不称Total／Counter／Pickup三逻辑额度全部满额。当前Counter head未付款，正常Resume Collection后按原paid FIFO领取、补显示、实际离场／原Total释放并全排空；恢复默认到店后首次间隔2.0377378游戏秒，随后新客亦排空。实际12行离场观察保留，未调用显式Resume Blocked，没有新的Blocked；本次不是原B历史员工失败的逐帧重放，其单次确切原因仍UNKNOWN。产品修复机制有真实0P/2F→GREEN及本轮完整普通Editor恢复证据，不抹去原失败历史。

本run于15:48:04.836Z核对False／None、GameView index8／count12、Scene5/5恢复。最终收尾见`outputs/phase14/high-load-final-cleanup.txt`：完整Editor路径进程0、旧资产tracked diff0。helper／meta逐字归档到`outputs/phase14/agent-manual/source/high-load-fix-20261020/`后，仅移除两个已证实untracked的Assets源；root独立重算SHA256分别为`3BE7F5D43DA9F7D73800A2170F6565ED690F3B2C15955B23AD399A2C27B71188`／`BA279730BD3A378447E35AE8B85383F3D754D459D65401C8F88521A59880B944`，两源均不存在；五Scene／meta与本run原baseline hash全部一致，主目录tracked干净，nonMain diff-check通过。当前CustomerFlow源码SHA256为`37DB41985C7D511658FA7AA24CEA6F3CE54F85786824C6B9A0D926BCBA08AFC0`。当前18项Agent功能流程均PASS，四项客观结果与Owner NOT_RUN边界不变。

本次回归范围裁定：修改仅限P14私有未发segment门禁，原P11／P13实现未变；全部P14及P12顾客队列直接回归、关联Edit、正式0P/2F→GREEN、独立窄QA与原前序普通Editor恢复均已通过，无新修改或失败需要扩大测试。因此本次不重复全suite；Phase收尾的完整验证与Owner验收仍单列，M-017及SearchDB启动异常继续待处理。

### M-017／M-019定位与修正（本次修正完成）

Owner已授权“先修复”。M-017只读定位到手测配方前置：原第二CR支撑Grid(3,2)、设备朝向0的Customer anchor为Grid(3,1)，落在Main入口origin(3,0)的2×2 EntranceClearance及完整身体扩边内。设备GUID排序正确选中该CR后，CustomerFlow.BuildQueue的InEntrance正确拒绝队首，故0站位；固定ID自动fixture只选原CR，未覆盖这个组合。当前不据此改ordinal或放开入口保护；先保留相同记录GUID下“应正常接待”失败，再修正测试配方与Guide，在同GUID及CR／CM不同ordinal组合下验证真实服务、slot.1杯与旋转。

M-019启动时序假设已有本机二进制证据：`UserSettings/Search.index`缺失，原helper在executeMethod末尾立即设置isPlaying=true。只读本机6000.5.5f1 QuickSearchModule IL与[官方6000.5参考源码](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.5/Modules/QuickSearch/Editor/Indexing/SearchDatabase.cs#L303)一致：默认索引首次创建在isPlayingOrWillChangePlaymode时直接返回，随后EnumerateAll仍读取空s_DBs的第0项，吻合原异常栈；SearchInit在delayCall才启动。最小修正先限代测工具：保持EditMode，等待自然delayCall与实际默认索引／数据库就绪后才EnterPlay，超时明确失败。不删除Library、不关闭Search、不自行造索引或屏蔽异常，普通Editor启动与原UI结果另行核对。具体成因最终仍待受控冷启动验证。

M-017已用历史完整GUID确定性验证旧配方：`m017-recorded-recipe-red.xml`实际0 PASS／1 FAIL／0 SKIP、1.7044719秒、CLI1／Editor2，选中CR374b…／CM1946…、CounterSlots0，失败于已发布配方应能接待的断言；这是配方输入RED，入口安全拒绝本身正确。仅将第二CR支撑由Grid(3,2)改至(3,3)，其他设备／slot／朝向不变；`m017-recipe-combinations.xml`为4 PASS／0 FAIL／0 SKIP、6.8676643秒、CLI0。原CR／主CM（保留yaw37刚性root）、原CR／第二CM、第二CR／主CM及历史GUID第二CR／第二CM均真实CounterSlots8、中slot.1杯、顾客离店、员工Idle及容量0。未改runtime或设备排序，Guide配方已同步；普通Editor新GUID与M-019冷启动验证待补。

首次M-019受控cold启动`G17-20261020-163902`明确未成功：外部launch receipt记录Search.index不存在，保持EditMode等候45秒后BLOCKED，未请求Play，M-017尚未执行；自己的自然delayCall观察也未发生，因此不能称索引缓存坏或自然初始化已通过。Scene5/5及原设置恢复，未删除／改造缓存。该Hidden executeMethod宿主仍有真实Editor.update，却未推进delayCall；下一步修正工具前置为在EditMode经Unity公开`SearchService.EnumerateDatabases()`执行一次真实首次使用初始化，观察实际文件／数据库存在后才进入Play。此API会让Unity创建本地默认索引，不称“被动自然创建”，不直接调用内部Setup／Create或自行写索引，不关闭Search或吞异常；公开API调用失败须明确BLOCKED，不无界fallback。最终结果待补。

最终普通Editor `G17-20261020-164313`，CLI0：外部`m017-m019-public-init-launch.txt`先记录Search.index=False。16:43:13.870Z EditMode索引不存在／db0；16:43:15.416Z在非compiling／updating时经公开API首次访问；15.437Z实际index=True／db1／returned1／errors0后才请求Play。文件由Unity生成287bytes，保留为本机正常UserSettings；没有手写index、删除Library、关闭Search或反射调用内部Setup。自然delay仍未观测到，Domain Reload后只读db0也原样保留；不能把公开API初始化改称自然delay成功或Unity Search通用缺陷修复。完整Application console的Error／Exception／Assert为0，原Search／ArgumentOutOfRange异常未再出现；raw native log仍保留原host Licensing Access token诊断，不称全部原生日志无Error。M-019启动子项Agent PASS，结合先前未改UI的C组横／竖屏及Snapshot／界面事件证据，M-019 Agent客观项PASS。

同run的M-017通过正式Catalogue／Preview／Confirm摆修正配方，使用正常新GUID实际选中第二CR4442…与第二CM03b4…；Cashier工作位(-.5,0,.5)、Barista(-3.5,0,3.5)，未修改GUID或挪Actor。首单真实杯位于slot.1，world(2.42,.42,-1.38)，正常旋转长支撑并Confirm／Done后新epoch新单仍落当前中间module。root已直接查看[旋转前中间杯](../outputs/phase14/agent-manual/G17-20261020-164313/m017-actual-middle-module-cup.png)及[正常旋转后新杯](../outputs/phase14/agent-manual/G17-20261020-164313/m017-after-normal-support-rotation.png)，均实际1600×720，完整results.tsv记录M017-objective PASS。原0站位历史保留，但根因已明确为旧Guide配方前置；未改产品ordinal、入口保护或碰撞规则。

独立QA读取四组合红绿、最终helper公开API版及本run完整Console／操作记录后，将M-017／019窄review CLOSED，无Critical／Important。run于16:44:03.948Z恢复原False／None、GameView8／12及Scene5/5；最终临时helper归档与清理另记。本次runtime未变，仅fixture／工具／Guide修正，受影响四组合与实际两项补测已覆盖；不再重复其他手测组或全suite。Owner观感／真实操作及Phase收尾完整验证仍单列。

最终清理receipt为`outputs/phase14/m017-m019-final-cleanup.txt`：完整Editor路径进程0、旧资产tracked diff0、原Scene5/5与设置恢复。helper／meta归档到`agent-manual/source/m017-m019-public-search-init/`后仅移除两个owned untracked Assets源；root独立确认copy的SHA256分别为`7DCE52878D826000AEE950996151AD8A39594D4C43721BBB875F7B25F0DCA345`／`BA279730BD3A378447E35AE8B85383F3D754D459D65401C8F88521A59880B944`、源不存在2/2、Scene baseline hash一致5/5、主目录tracked干净及nonMain diff-check通过。Unity生成的真实Search.index287bytes保持gitignored本机状态；CustomerFlow hash仍为`37DB41985C7D511658FA7AA24CEA6F3CE54F85786824C6B9A0D926BCBA08AFC0`，本轮未改任何runtime代码。当前Guide有22项Agent PASS，其中4项仅客观范围；22项Owner仍NOT_RUN。

| 验证范围 | 状态 |
| --- | --- |
| 18项Agent功能代测 | 18 PASS（M-006、M-011本次窄修复后补测） |
| 4项Agent客观检查 | M-007、M-017、M-019、M-021 PASS（仅客观范围） |
| M-001–M-022 Owner manual | NOT_RUN |
| Player build／真实Input手测 | NOT_RUN |
| Android／iOS／device safe area | NOT_RUN |
| CI | NOT_RUN |

手测操作与复位入口见Beginner Guide和Test Cases。只记录实际执行者与观察，不将自动截图或NUnit结果填写为Owner PASS。Owner完成manual、Critical／Important修复及必要复核前，Roadmap不得标Phase14 Completed。

## 已知限制与测试副作用

- 日志保留既有obsolete API warning及Editor退出期诊断，XML PASS不等于日志完全无warning；新增未处理runtime exception必须单独检查并修复。
- 测试曾自动重存URP配置与P7 Invalid材质。已核对源原本干净、Editor退出后仅还原对应自动变更；P14 source全部保留。Task8后的P12／P13独立Scene+meta已与原literal baseline核对并还原；第五完整Play后Editor完整路径进程0；仅P7已知三行自动材质差异保存为`task9-after-fifth-full-material.patch`后精确还原。最终Art／UI／Settings／ProjectSettings／旧Validation tracked diff为0，P12／P13 Scene＋meta literal4/4与baseline一致。nonMain diff-check通过，Main仅5处原生空`m_Name: `字段尾空格例外。主目录tracked files保持干净，P14修改全部留在E盘worktree。
- 没有改变角色尺寸、P11移动／恢复预算、完整碰撞skin或原模型动画；低柜台私有连续几何认证已按正式RED／GREEN修复并窄复核关闭。临时工具障碍走正常NavWorld注册与移除，失败会明确说明，不能冒充实际堵路。
- Hold Collection的五杯准备在首客开始领取前开启；晚切换不撤销已发移动或释放仍被真实身体占用的共享服务点，先Resume完成领取，再重新Play准备。该晚切换情形未单独实测；不声称任意时刻开启均能立即腾出lease。
- 本阶段无economy／价格／收入／退款、Inventory、Save、营业日或下个Phase功能。
