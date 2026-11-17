# Phase 15 — Business Day Lifecycle Design

> 日期：2026-10-26。
> 状态：Approved for Implementation（2026-10-26）。Owner 已明确选择 D1／D2／D3，并在查看配套 plan／测试范围后指示“好的，用sub agent开工”。执行仍受 plan 的范围、手工验收与版本管理边界约束。
> 当前 source baseline：main `f55d4eb685f32e4dcc30fb64b753b45b695a6a3b`；Unity `6000.5.5f1`。

## 1. 这个 Phase 是做什么的

让咖啡厅自己按营业周期工作：到点不再接新顾客，已经进店的客人继续买咖啡并离开，员工回到岗位后开始休息；休息结束自动开下一天，不需要玩家点击。

例子：打烊时，一位顾客还在柜台，另一位正在等咖啡。系统拒绝新客，但继续完成这两位的服务。最后一位真正离店、员工回位、订单任务与容量排空后才进入休息。昨天完成的订单保留为历史，不为了“清场”而删除。

来源：[Game Design 1.5／2.3／3.5](../../AnimalCafe_Project_Design.md)、[Roadmap P15 与 4.2](../../AnimalCafe_Development_Roadmap.md)、[Phase Process](../../AnimalCafe_Phase_Development_Process.md)、[P14 Design](2026-10-19-phase-14-integrated-takeout-loop-design.md)。Owner 当前指令优先于旧 P15 手动开店／Report／Next Day 思路。

## 2. Included / Not Included

包括：自动 Open／Closing／Rest、独立接待许可、真实排空判断、天数去重、阶段／剩余时间／阻塞原因 UI、独立 P15 Scene、focused TDD 与必要旧行为回归。

不包括：Coins、Revenue、Daily Report 正文、Save 文件、退出后离线收益、跨进程恢复、多商品、多员工排班、自动补货、真实故障的全新自动恢复策略。P16／P17／P18A 分别接入 Economy／持久化／最小 Offline；P15 不能宣称完整后台挂机已完成。

既有 P14 场景没有 P15 component 时保持原行为。P15 用正式默认时长，也提供仅供验证的短时参数；短时通过不等于实际运行八小时或真机后台通过。

## 3. 已确认规则与政策

已确认：不要求 Start／Next Day／Report 点击；打烊停止新客，服务所有已接待者（含未付款者）；休息结束自动进入下一轮；真实无效路径不瞬移、不伪造完成、不擅自取消已付款订单；玩家主动 Pause 和既有身体／路线保护不能被自动开店解除。

以下三项已收到 Owner 对本 chat 问题的明确答复；不再重复询问：

| 决定 | Owner 已选择 | 未选方案与影响 | 当前状态 |
|---|---|---|---|
| D1 时间口径 | 现实营业 8 小时（28800 秒）＋休息 2 小时（7200 秒）；Scene Pause／2x 不改变周期时间 | scaled cycle 或八小时包含休息均未选择 | CONFIRMED |
| D2 收尾／堵路 | Closing 等真实排空；堵路明确停留，沿现有恢复入口修复后继续；从实际排空时开始完整休息 | 固定日历继续跨日并保留旧单不在最小 P15 | CONFIRMED |
| D3 装修中断 | 保留既有 P14 确认真实修改的清场，P15 明确记录 LayoutInterrupted，不能计作全部服务完成 | 不增加 Closing 时禁止确认装修的新规则 | CONFIRMED |

D1 的两小时已明确确认；八小时只指正常营业段，不含 Closing 排空。D2 延续现有安全边界，故障时可能长期停止下一周期；这是 Owner 已选择的当前边界，不能写成无人值守恢复已经解决。

P15 只负责运行中周期与 Scene 协调。真正应用挂起／退出后的时间结算和 Save 恢复由 P17／P18A 实施；不把恢复后的长时间差一次传给现有 NPC Step 来假装离线服务。运行中采用独立现实经过时间，Pause／2x／装修暂停只影响 Scene。

现有 2x 仍会加快现场制作和移动，不是只有动画变快；由此产生的实际吞吐与未来 Offline 倍率差异需在 P16／P18A 明确，不能在 P15 偷加收益倍率、偷偷削减订单或改写已有工作时长。

## 4. Approaches

1. **复用现有 scaled GameTime（未选）。** 改动较小，但会让 Pause／2x 影响八小时周期，与已确认 D1 不符。
2. **独立周期时钟＋现有服务协调（采用）。** 营业／休息使用独立 elapsed time，Scene 继续沿原 GameTime；同一个 Cafe Loop driver 在服务前应用截止边界、服务后验证排空，避免 Update 顺序竞态。
3. **固定 UTC 排班并跨日结算。** 适合未来 Offline，但现在没有 Save／Economy 和跨日旧单政策，不能在 P15 提前整套实现；由 P18A 的真实持久化交接再设计。

依据 Owner 对 D1／D2 的选择采用方案 2。纯逻辑注入单调经过时间，正式运行时读取 Unity realtime clock；不接 DateTime／UTC 排班、不做本机时间修改检测以外的完整 Offline engine。具体接口在配套 plan 固定。

## 5. 生命周期与先后顺序

初次有效配置进入 Day 1／Open，接待仍须通过 readiness、玩家 Pause、真实容量和路径门禁。`Open` 是计划营业状态，不代表实际总能接待；必要依赖缺失、暂停或路径失败需单列原因。

每帧只有一个 Cafe Loop driver：

1. 读取有效周期时间，先处理 Open 截止，必要时进入 Closing。
2. 同步应用新接待许可，截止同帧的自动来客和直接 `TrySpawn()` 都必须拒绝。入口还须查询同一周期时钟的实时截止许可，不能仅依赖上一次 driver 写下的 bool，见第 6 节。
3. 沿原 P14 driver 推进顾客、收银、制作、交付、领取、真实离店和员工返回。
4. 在该次推进后采集真实 drain facts；不使用前一帧旧 snapshot。
5. Closing 满足全部排空条件后，一次性进入 Rest，使用本次实际排空观察时刻作为两小时休息起点。
6. Rest 到期自动进入下一 Open，Day 只加一次；保留主动 Pause／无效布局门禁，不强行接待。

不得通过两个独立 Update 再推进一次 Cafe Loop，也不依赖未验证的组件执行先后顺序。大时间差不允许略过 Closing 的真实服务；不补发几千名 Scene 顾客。测试可以直接注入时间，不必真实等待几小时。

Rest 到期但布局无效或仍 Pause：计划周期进入下一 Open，实际 admission 继续受原门禁限制。不能通过“自动开店”偷偷解除暂停或忽略 Blocked。运行中大时间差只推进到需要真实处理的边界，不循环补算多个没有实际服务的营业日；这种离线补算由 P18A 设计。

重复配置、同帧回调、disable／enable 不得增加新 Day、绕过休息或重复提交 transition；组件缺失／disabled 必须明确关闭新接待并显示不可用，不能将 null 当已排空。缺少 P15 component 的旧场景默认行为不受影响；配置了但失败的 P15 不能静默降回无周期营业。

## 6. Admission 与 Service 分开

当前 `CafeLoopController.CanProgress` 同时用于业务服务，不能在 Closing 将其整体设为 false，否则已有顾客也无法完成服务。

建议新增 `SetAdmissionEnabled(bool enabled)`：未接 P15 时保留原默认。实际 customer admission = 原 `CanProgress` AND 独立接待许可 AND 本次请求通过周期 admission policy。setter 立即同步原 `SetIntegratedAdmissionAllowed`，每次 Step 保留合取条件；setter 不能覆盖 P15 policy 的拒绝。

当前 `CustomerFlowController.TrySpawn()` 已验证 integrated admission bool，但该 bool 是缓存，尚不能证明周期截止安全。P15 需增加一个窄的、每次接待前执行的只读 policy 查询；自动来客与直接 `TrySpawn()` 必须走同一查询。即使本帧 driver 尚未运行，只要周期已到截止，也必须拒绝，不 reserve／收费／生成角色。查询不推进 Phase、不发事件、不清理业务；driver 随后发布 Closing。

**缺失组件不能退回旧模式。** 入口层 `CustomerFlowController` 增一个 serialized `requireAdmissionPolicy`（名称为设计建议），旧 Scene 默认 false，P15 Scene builder 明确设为 true。P15 controller 注册唯一有效 policy；要求为 true 但 provider 尚未注册、已缺失、disabled 或 destroyed 时拒绝新接待并显示原因，不依靠缺失组件自身来关门。解绑只撤销对应 owner 的 provider，不能清除新注册者。require=false 的旧 P14 场景继续按原合同运行。

只设 `AutoSpawn=false` 仅能关自动调度，不能作为正式闭店安全门禁。P15 不覆盖原 validation 对 AutoSpawn 的选择，也不能让手动验证按钮绕过已启用的 policy。

Closing 保持所有既有服务进度与恢复规则；Rest 无顾客可服务，员工仍保留原身份与岗位。禁止调用 Hold／Dispose／ClearOrdersForDecoration 来表示打烊或下一天。

## 7. 排空条件：不知道就不能算完成

由现有业务 owner 聚合只读事实，不增加第二套 Order／Capacity authority。

| 条件 | 证据与要求 |
|---|---|
| 顾客实际离店 | `customers.Snapshot` 的 live visits 为零，包括 disabled／Blocked 身体；订单 Completed 不等于顾客已离店 |
| Counter／Pick-up 无队伍 | 两种真实 assignments 都为空 |
| 容量真正释放 | 三项 `CapacitySnapshot.Used = Reserved + Occupied` 均为零；缺少 ledger 不当作零 |
| 无待交付商品 | 逻辑 Delivery 为空；可见桌杯为空是额外表现检查，不能代替逻辑事实 |
| 员工实际返岗 | 两岗位都有真实 enabled Actor、有效 binding，Idle／HandleId 0 且实际到达各自工作位；复用既有到达／速度／朝向容差，不自行放宽 |
| 业务无待处理工作 | Service 无 cashier／barista、待消费 callback、pickup／exit recovery、blocked collection、未完成订单或尚未退出的 collected 身份 |
| 生命周期稳定 | 当前没有装修、布局清场、未完成配置或 disposed owner |

**历史 Completed Orders、PaidVisitIds 不必为空。** 它们是未来日报／Economy 的事实，不应每晚清掉；`Failed` 也不等于“已服务完成”。P15 不新增失败赔偿或取消政策。

`CafeLoopService` 提供窄只读 pending query；`EmployeeFlowController` 提供真实返岗 query。不能解析中文 Status 字符串作为排空规则，也不允许 getter 隐藏发起工作或清理。

装修沿既有 P14 规则处理。P15 以同一个 layout change version 在破坏旧业务前记录一次 `LayoutInterrupted`；只查看、取消 Preview、重复 Done 或重复回调不能重复记录。中断不增加成功订单数，不宣称所有已接待者已服务，不虚构退款；如果原 cleanup 失败，保留中断及真实失败并禁止 Rest。

装修后仍等待保留的真实离场者、员工返岗、全部容量释放及布局清场完成。实际 drain 成立后可以 Rest，但当日结束原因记录为 LayoutInterrupted；正常无中断为 CompletedDrain。最新一次结束记录在下一 Open 后仍可只读查看，不能因 Day 加一立刻丢失中断事实；完整经营 ledger 与更长期历史由 P16／P17 扩展。

## 8. UI 与验证 Scene

使用现有 P5 UI 风格，显示 Day、Open／Closing／Rest、计划剩余时间及不能继续的真实原因；UI refresh 只读，不新增 Open／Close／Next Day／领奖按钮。

2026-10-27 Owner 已批准展示边界修正：MainCafe 使用现有 P5 theme、圆角、字体及间距显示简洁玩家状态；黑色 P14／P15 诊断 HUD 仅在 Validation 显示。Main 不显示 Epoch、订单／顾客 ID、内部任务 handle、LayoutInterrupted enum／计数等工程信息；上轮装修中断事实用玩家文案保留，Closing 真实原因转换为可理解提示。正式8h／2h、真实排空、readiness、Pause／2x／Decor合同不变。既有共享Canvas不能整体关闭，以免同时隐藏正式营业状态。对应 Task5／6 follow-up 的实现、TDD及范围见当前工作区 `player-hud-brief.md`；修正未复测前，旧HUD manual与自动结果不能冒充新展示源已通过。

新增独立 `Assets/Scenes/Validation/Phase15BusinessDay.unity`：复用 P14 已验证布局与角色，安装 P15 component 和短时验证配置。由 Editor setup tool 创建和接线，不能手工改 YAML；不调用写 MainCafe 的 P14 builder，不自动改旧 P14 Scene／prefab。

P15 Scene 使用短时配置时，UI 明确标为 Validation；正式默认时长保持 D1 批准值。MainCafe 的正式接入另作为明确 task 与验收，不能把独立验证 Scene 成功当作 MainCafe 已可玩 P15。

## 9. 批准的文件与用途（现已按Task实现）

| 文件 | 用途与影响 |
|---|---|
| 新 `Assets/Scripts/BusinessDay/BusinessDayContracts.cs` | Phase、配置、不可变 snapshot 与 drain result |
| 新 `Assets/Scripts/BusinessDay/BusinessDayService.cs` | 无 Scene 依赖的时间／阶段／天数转换，接收受控时间与事实 |
| 新 `Assets/Scripts/BusinessDay/BusinessDayController.cs` | 连接 Service 与原 Cafe Loop driver，发布只读状态 |
| 新 `Assets/Scripts/BusinessDay/BusinessDayStatusView.cs` | 阶段／计时／原因 UI |
| 修改 `Assets/Scripts/CafeLoop/CafeLoopController.cs` | 独立 admission、before／after driver 接点与 drain 聚合 |
| 修改 `Assets/Scripts/CafeLoop/CafeLoopService.cs` | 只读 pending query；保留历史数据与旧恢复语义 |
| 修改 `Assets/Scripts/Customers/CustomerFlowController.cs` | 单一接待入口的实时 policy 查询与 serialized opt-in；旧 Scene 默认关闭新要求 |
| 修改 `Assets/Scripts/Employees/EmployeeFlowController.cs` | 窄返岗查询，复用真实 binding／导航条件 |
| 新 `Assets/Editor/Phase15/Phase15BusinessDaySceneSetup.cs` | 独立 P15 Scene 创建、短时接线；MainCafe 接入需单列 task |
| 新 `Assets/Tests/EditMode/Phase15/` 下 focused tests | 时间、转换、排空、接待边界与旧默认行为 |
| 新 `Assets/Tests/PlayMode/EditorSceneLoading/Phase15/` 下 integration tests | 真实 Scene、顾客离店、员工返回、Pause／2x、Rest 与下一轮 |
| 新 Scene 与 Unity 生成的 `.meta` | 保留 GUID；不重建旧资产 metadata |

现有 Runtime／EditMode／EditorSceneLoading PlayMode asmdef 已覆盖相应目录，不预建新的 assembly、event bus、scheduler 或 framework。实际文件数／签名由批准后的 implementation plan 固定。

## 10. Rework、风险与验证

本阶段会给已完成 P14 driver／Service 和 P13 Employee owner 增窄接口，需回归旧无 P15 场景、连续服务、Pause／2x、手动来客、Blocked 恢复与装修生命周期。保留旧公共接口默认值，不能同时重构队伍算法、Nav 身体保护、资产大小或工作时长。

当前无正式 Save schema migration；P17 才定义持久化。不得因此把业务历史直接清空，或在 P15 自建临时 Save／Offline 旁路。现有主目录有未提交文档与其他 untracked 资料，不能重置；实现时使用 E 盘项目内独立 worktree，携带本轮明确相关文档，其他资料保留原位置。

配套 [Test Cases](2026-10-26-phase-15-business-day-test-cases.md) 定义 normal／boundary／failure／recovery 覆盖。Task 只跑 focused RED→GREEN 与直接 regression；完整 EditMode／PlayMode／必要 Scene integration、独立 review 和 Owner 手测集中在 Phase 收尾。先用短周期验证，再按实际授权记录长时间与 device 结果。

当前状态：D1／D2／D3 与配套 implementation plan 已获 Owner 批准，用 sub-agent 开工。执行记录以工作区 plan／ledger／Validation Report 为准；MainCafe 接入、正式长时间运行及 device 验收按真实结果记录。独立 builder-check 曾指出“截止前缓存许可”和“缺失组件如何 fail closed”两项 Important；第 5–6 节及 E03／E25 已补齐并经原 reviewer 窄复核关闭。这只是设计层面的复核，不是实现验证。

2026-10-27实现与验证：Main正式28800／7200秒、原readiness保持；独立Scene60／15标Validation。最新完整Edit2832 PASS，独立完整Play1445 PASS／5 opt-in SKIP，本地自动gate通过；后续Editor／test修正的source binding与全部历史见[Validation Report](../../Phase15_Validation_Report.md)。Engineering／QA／Production 技术复核已完成（Critical 0／Important 0）；Owner M01–M07、真实8小时／device、CI／VCS／merge／merged-main 均 NOT_RUN。Phase 未正式 Completed，不进入P16。重新Play重建Day1，Save／退出后Offline未实现。操作见[Guide](../../Phase15_Beginner_Guide.md)。
