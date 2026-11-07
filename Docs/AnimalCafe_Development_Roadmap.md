# AnimalCafe Development Roadmap

> **当前 P15 为 In Review，PR #15 已创建并保持 Open。** 最新精简 EditMode 1388/1388 PASS，PlayMode 90/90 PASS，其中 P15 为 68 项 EditMode 和 77 项 PlayMode。原 20 单压力用例及本轮停止自动来客后残留预留的故障均已通过 regression。旧 distinct actual-exit 保留 NOT_REPRODUCED／NOT_PROVEN。正式 Completed、CI／merge 和 merged-main 状态由后续流程记录，Owner 本轮授权 Agent 提交／发布分支，最新要求保持 PR 未合并。PR 为 [#15](https://github.com/zhengparker/AnimalCafe/pull/15)。见[最新验证报告](Phase15_Validation_Report.md)。

> **历史 P15（2026-11-07 UTC，修复前）：IN_PROGRESS。** 原固定 1 秒来客、2x 的 20 单用例已修复并在精简回归通过；EditMode 1388/1388 PASS，PlayMode 88/89 PASS（P15 自身 76/76 PASS）。唯一新失败是 MainCafe：第 9 位尚未进店的收银站位预留挡住送杯规划路线，第 8 位未完成，已停止；不关闭 P15、不进入 PR。见[本轮验证](Phase15_Validation_Report.md)。

> **前一轮 P15（2026-11-07 UTC）：IN_PROGRESS，精简回归未全过，开发关闭／PR 条件未满足。** 最新 Edit1388/1388 PASS；Play88/89 PASS（P15自身76/76 PASS）。唯一失败是 2x＋固定1秒来客连续20单中，guest20点单队列前移Blocked；第一内部拒绝原因仍UNKNOWN。同轮正常随机Main9/9真实离店排空，不覆盖该FAIL。已停止，不自动重跑／修复，不commit／push／创建PR，不进入P16。具体见[最新Validation Report](Phase15_Validation_Report.md)。下方旧快照均保留其来源与历史范围。


> **最新修复与限定验证（2026-11-04，Owner已批准并完成）：中间第2块 Pick-up 布局的卡住问题通过复现回归。** 新增窄 admission readiness：尚未出生/未获权的新客，真实候选首段会挡住合法已取餐客人的固定出口时，沿原身份及有限累计预算暂缓；出口真实腾空后正常重证并继续。普通候位、员工工作、碰撞/付款/容量/P15营业时间保持原合同。修正 Prepared/共同提交检查、回调重入与临时 Nav 缺失的生命周期边界。最终冻结源码 **Core654/654 PASS**（原636测试字节后缀完整保留＋新增18项）、**实景短回归18/18 PASS**（截图布局1＋未Ready先转队1＋P15接待门禁16）。截图布局使用合法固定1游戏秒来客输入，120游戏秒时已有12位取餐；随后真实排空到 **15/15订单完成、顾客0、员工Idle、容量归零**，观察到5位新客被暂缓后全部自然恢复。未手设Arrived/付款/完成，未用恢复工具掩盖停滞。独立SOURCE review C0/I0/M0。证据：[Core654](../outputs/phase15/service-traffic-coordination-20261030/admission-exit-readiness-final-verified-first/edit/test-summary.json)、[实景18](../outputs/phase15/service-traffic-coordination-20261030/admission-exit-readiness-final-verified-first/play/test-summary.json)、[实际排空记录](../outputs/phase15/photo-pickup-wait-20261103/readiness-final-native-output.txt)、[源码与结果](../outputs/phase15/photo-pickup-wait-20261103/readiness-final-results.json)。两批输入/源码/Scene/资产恢复零差异，Editor/worker0；OS handles UNKNOWN。**此前两项旧case失败仍保留：SingleSideQueuePreservesActualDeliveryAndReturnCorridor 的路线失败，以及 CounterTailCannotCommitAcrossEarlierPendingExitSuffix 的测试前提未捕获；精确修前对照也同样失败，本轮未扩展修复或冒充PASS。P15当前仍IN_PROGRESS，完整suite、长GUI/Owner/device/CI/merge等原验收缺口不变。** 无Scene保存、Git提交或进入P16。

> **上轮截图复现历史（2026-11-03；当时尚未修完）：Owner确认 Pick-up 在中间第2块。** 已修正未分配收银尾位误挡候位，以及原阶段交接已捕获 waiter 的资源资格迁移。当前 Core **636/636 PASS**；真实截图布局已有3位取餐、前2位实际离店，但第3位仍因后来获权的入口路线占用出口而 Blocked，实景为 **FAIL**。直接短回归 **17/19 PASS、2 FAIL**；两项在精确修前对照中同样失败，保留原结果。拟新增“仅暂缓会挡已取餐客人出口的未出生来客”的窄 producer gate，**待 Owner 批准，未实施**。详见[提案与范围](../outputs/phase15/photo-pickup-wait-20261103/admission-exit-hold-proposal.md)、[Core636](../outputs/phase15/service-traffic-coordination-20261030/photo-captured-bound-handoff-full-core-green-first/edit/test-summary.json)、[原决定时序实景FAIL](../outputs/phase15/service-traffic-coordination-20261030/photo-middle-first-freeze-timeline-first/play/test-summary.json)、[短回归19项](../outputs/phase15/service-traffic-coordination-20261030/photo-middle-two-fixes-direct-regression-first/play/test-summary.json)、[修前两项对照](../outputs/phase15/service-traffic-coordination-20261030/photo-middle-two-regressions-pre-task-control-first/play/test-summary.json)。修前对照结束后两份当前源码已恢复并核对；无 Scene 保存、Git写入或 P15正式closure。

> **最新参数修改（2026-11-03，Owner已批准并完成）：自动来客间隔改为0–10游戏秒。** 默认均匀随机，平均5游戏秒；Pause不放行零秒来客，成功接待后才抽下一轮，每次更新最多接纳一位，原容量／路径／营业门禁继续生效。产品改动仅 `CustomerSpawnClock.Draw` 的抽样公式；5个直接相关测试文件同步换算固定1／2／3秒输入，保留原测试负载及断言。新边界先在旧公式上得到预期RED，再验证 **Edit15/15 PASS、直接Play18/18 PASS、原短实景1/1 PASS**（含零秒真实接待、容量、帧率／2x及P15截止门禁）。初批实景filter的namespace未匹配，明确NOT_RUN；随后仅补正确FQN单项并通过，未重复其余18项。证据：[计时15项](../outputs/phase15/service-traffic-coordination-20261030/spawn-zero-ten-short-green-first/edit/test-summary.json)、[短回归18项](../outputs/phase15/service-traffic-coordination-20261030/spawn-zero-ten-short-green-first/play/test-summary.json)、[原实景1项](../outputs/phase15/service-traffic-coordination-20261030/spawn-zero-ten-native-scene-short-first/play/test-summary.json)。全部已结束批次输入／资产恢复零差异、Editor/worker0；OS handles UNKNOWN。未重跑其他固定间隔长fixture或完整suite。P15原专项NOT_PROVEN与最终验收缺口继续保留，不宣告正式Completed。

> **最新定向验证（2026-11-03）：未 Ready 先去 Pick-up 候位的现有规则已验证，无产品代码变更。** 仅新增两项测试：业务层保留前客已取杯但未实际 Exit 的事实，后客 Preparing 时仍请求转队；真实双顾客场景中，前客 Ready、未取杯且真实身体留在店内，后客在 Preparing 时已有真实 Native 移动，随后真实到达 Pick-up 候位，最后两单正常完成并排空。相关业务 class **31/31 PASS**，新增实景 **1/1 PASS**；现有断言和原测试行未删除或修改，未调整 Scene、游戏时间、容量、付款或碰撞规则。证据：[业务31项](../outputs/phase15/service-traffic-coordination-20261030/pickup-before-ready-business-short-first/edit/test-summary.json)、[双顾客实景](../outputs/phase15/service-traffic-coordination-20261030/pickup-before-ready-business-short-first/play/tests.xml)。本次证明现有 Validation 布局的上述行为；没有重现或确定 Owner 实际摆法的等待原因。此前“当前 Returning 受阻＋顾客同时等待”专项仍 NOT_PROVEN，当前源完整 PlayMode、长时间 GUI 与正式 P15 验收仍未完成，不宣告全部通过。本轮不追加长回归。

> **上一验证快照（2026-11-02；Source93；按 Owner 要求缩短验证）：P15 仍 IN_PROGRESS，本轮不再追加测试。** 当前修复完成后，完整 ServiceTrafficCoordinator class **634/634 PASS**（不是完整 EditMode）；修复涵盖已完成顾客移动后的单次有界唤醒，以及完整路径含一个静止点的合法保护要求。随后仅执行一次原 Native 保护性补测：**1 Inconclusive / NOT_PROVEN**，原因是未观察到 B 的同一任务当前 Returning selected-home Bound/live 状态；实际日志中 A、B 均出现 DeliveryArrived 与 Returned。该前提缺口未关闭，不能计为 PASS，也不等于已证明新的产品故障。本轮到此结束，不自动扩展 fixture、重跑或增加长时验收。两批输入与资产恢复零差异，Editor/worker 0；OS handles UNKNOWN。证据：[当前 Core634](../outputs/phase15/service-traffic-coordination-20261030/task6-complete-single-corner-runtime-full-core-green-first/edit/test-summary.json)、[单项 Native 未证实](../outputs/phase15/service-traffic-coordination-20261030/task6-single-corner-checkpoint-native-fast-final-first/play/tests.xml)。此前 Source86 完整 EditMode **3658/3658 PASS** 已结束，但早于本轮 Core 修改，不冒充当前源全量结果；此前 Main120秒、Mixed60真实离店及原五项 PASS 也保留各自 source 范围。当前源完整 PlayMode、TM01–07 / Main15分钟＋Validation10分钟 GUI，以及 Owner/8h/device/CI/merge 验收仍未完成；不宣告 P15 Completed，不进入 P16。


> 状态：Approved Roadmap
>
> 类型：Dependency-driven, milestone-based development roadmap
>
> 当前开发环境：Windows / Unity `6000.5.5f1`（Editor 与开发验证环境，不是 release target）
>
> 正式目标平台：Android 与 iOS
>
> 更新日期：2026-10-26（Owner 已批准按 4.2 审查结论调整路线；各 Phase 的具体 spec／plan 仍逐阶段批准）

## 1. 文档用途

本文档把 `AnimalCafe_Project_Design.md` 中的长期 Game Design 拆成能够单独设计、批准、开发和测试的小型 development phases。

- `AnimalCafe_Project_Design.md` 是长期游戏规则的 Source of Truth。
- 本文档定义 implementation 顺序、system dependencies、risks 和 phase gates。
- `AnimalCafe_Phase_Development_Process.md` 定义所有 Phase 的默认开发、测试、review 和 evidence 流程；从 Phase 6 剩余收尾立即生效。
- Phase 0 保持已完成状态；Phase 1–52 采用 dependency-driven 小阶段。
- 每个 Phase 只承担一个主要技术风险，并拥有独立 design、implementation plan 和 tests。
- 每个 Phase 使用独立的 `Docs/PhaseN_Beginner_Guide.md` 作为 beginner educational note；不能把多个 Phase 的教学内容混入同一个 guide。
- 每份 Phase spec 和 Beginner Guide 的开头必须先用通俗、适合中学生理解的语言说明“这个 Phase 是做什么的”，并至少提供一个具体例子，再进入 architecture、technical rules 或 test details。
- 每个 Phase 开始前需要用户批准 design 和 implementation plan。
- 未通过当前 Phase gate 时，不开始下一个 Phase。
- 每完成一个 Major Milestone，重新 review 后续 roadmap，允许根据实际 playtest 调整远期 Phase。
- Open Design Questions 不能直接作为 implementation requirements；进入相关 Phase 前必须先作决定。
- Roadmap 不使用周数或 deadline，以可验证 milestone 为进度依据。

### 1.1 Superseded Documentation

旧版大型 Phase 1 Core Cafe Loop 文档已从当前 documentation set 清理；其历史仍可从 Git history 查询，但不再作为当前执行依据。

新 implementation 从本文档的 **Phase 1 — Layout Data Model** 开始。

---

## 2. Engineering Ownership 与 Approval

设计协作采用以下职责：

- 用户提供游戏目标、体验方向、创意偏好，并批准或否决方案。
- 软件总工程师负责提出 architecture、system boundaries、implementation order 和 tests。
- 每份方案必须说明：
  - 为什么现在做；
  - 为什么不放到更早或更晚；
  - 主要难点；
  - 推荐解决方式；
  - 风险和可能出现的 bugs；
  - automated、integration 和 manual tests。
- 如果用户想法会产生明显 scope、architecture、testing 或体验风险，软件总工程师应直接说明并提出更安全的替代方案。
- Approval 只授权已展示的 scope，不自动授权扩大范围、commit 或 push。

---

## 3. Development Strategy

AnimalCafe 使用 **Dependency-driven Small Phases + Playable Milestones**。

```text
一个小 Phase
→ 一个主要风险
→ 一份 design
→ 一份 implementation plan
→ 一组 focused tests
→ 一次 approval gate
```

多个小 Phase 共同形成一个玩家可感知的 Major Milestone。

### 3.1 Phase 开发节奏

每个 Phase 默认采用 `AnimalCafe_Phase_Development_Process.md`：

```text
Phase 开始：一次 design + test cases + implementation plan approval
→ Task 开发：focused RED/GREEN + 直接相关 regression
→ Phase 收尾：一次完整 regression + Engineering / QA / Production / 必要的 Art review
→ Studio Owner 手工验收
→ 更新 report、guide 和 Roadmap
```

Task 不是独立 release。除非涉及 shared architecture、Save/migration、input ownership、Scene transaction 或其他高风险变更，不为每个 Task 重复完整回归、跨部门 review、截图和 hash closure。

### 3.2 为什么不是先完成整个装修系统

装修系统必须先提供最小 Layout foundation，避免 Cafe Loop 把 Cash Register、Coffee Machine 和 Pick-up 写死在 Scene 中。

但完整装修、扩建、多楼层和 Atmosphere 不能全部提前开发。没有真实 Cafe Loop 时，无法准确知道 functional furniture、NPC paths、queue 和 expansion 的实际需求，容易 over-engineering。

因此采用：

```text
最小 Layout Foundation
→ 最小 Functional Furniture
→ Core Cafe Loop
→ 根据真实 gameplay 扩展完整 Decoration 与 Store Expansion
```

### 3.3 核心 Dependency Chain

```text
Layout Data
→ Grid Rules
→ Visual Style / Asset Pipeline
→ Core Models / UI Foundation
→ Basic Decoration
→ Functional Stations
→ Orders and Capacity
→ Navigation
→ Customers and Employees
→ Integrated Cafe Loop
→ Business Day / Economy / Save
→ Application Shell / Minimal Offline Foundation（P18A）
→ Recipe / Inventory / Idle Management Loop Validation（P20A）
→ Character Models / Characters / Events
→ 最小 Interior Content / Progression / Expansion
→ 按闭环试玩价值选择扩展内容；高级结构、Exterior、Dine-in、Multi-floor 与完整特色商品延期
→ Full-system Offline Progression Integration
→ UI / VFX / Asset Optimization
→ Mobile UI Adaptation
→ Android / iOS Platform Adaptation
→ Mobile Release Preparation
```

---

## 4. Roadmap Overview

### 4.1 Work Type Legend

- **Unity**：主要完成 C#、Scene、Prefab、data、services 和 automated tests。
- **Model**：主要制作 Models、Rig、Animation、Materials、Textures 或 visual assets。
- **Unity + Model**：同时包含资产制作和 Unity import、Prefab、Collider、Anchor、LOD 与 Scene 验证。
- **Unity + UI**：Gameplay logic 与对应 feature UI 同时完成。
- **UI/UX + Unity**：主要进行 UI architecture、visual design、navigation、usability 和 Unity integration。
- **Online + Unity**：涉及 account、Cloud data、network failure 和 Unity client integration。
- **Platform + Unity**：涉及目标平台 input、lifecycle、build、performance 和 Unity integration。

所有 Model phases 最终仍需要在 Unity 中进行 import 和 technical validation；`Model` 表示主要生产工作发生在 DCC / visual asset workflow，而不是表示完全不打开 Unity。

| Phase | Development Part | Work Type | Major Milestone |
|---:|---|---|---|
| 0 | Project Foundation — Completed | Unity | Foundation Prototype |
| 1 | Layout Data Model | Unity | Layout Foundation |
| 2 | Grid Occupancy & Placement Rules | Unity | Layout Foundation |
| 3 | Visual Style & Asset Pipeline Foundation | Unity + Model | Visual Foundation |
| 4 | Core Architecture & Functional Furniture Models | Unity + Model | Visual Foundation |
| 5 | UI Architecture & Design System | UI/UX + Unity | Visual Foundation |
| 6 | Basic Decoration Mode | Unity + UI | Layout Foundation |
| 7 | Interior Walls & Surface Customization | Unity + UI | Layout Foundation |
| 8 | Functional Furniture & Layout Readiness | Unity | Layout Foundation |
| 9 | Order Domain | Unity | Core Cafe Loop |
| 10 | Capacity & Reservation | Unity | Core Cafe Loop |
| 11 | Navigation & Movement Recovery | Unity | Core Cafe Loop |
| 12 | Customer Spawn & Counter Queue | Unity | Core Cafe Loop |
| 13 | Employee Task Flow | Unity | Core Cafe Loop |
| 14 | Integrated Takeout Cafe Loop | Unity + UI | Core Cafe Loop Prototype |
| 15 | Business Day Lifecycle | Unity + UI | Management MVP |
| 16 | Economy & Daily Report | Unity + UI | Management MVP |
| 17 | Save Foundation | Unity | Management MVP |
| 18 | Application Shell & Loading | UI/UX + Unity | Management MVP |
| 18A | Minimal Offline Foundation | Unity + UI | First Leave / Return Slice |
| 19 | Recipe & Menu | Unity + UI | Management MVP |
| 20 | Inventory, Reservation & Restocking | Unity + UI | Playable Management MVP |
| 20A | Idle Management Loop Validation | Unity + UI | Validated Idle Management MVP |
| 21 | Core Character Models & Animation Pipeline | Unity + Model | Character Vertical Slice |
| 22 | Staff Identity & Work Capabilities | Unity + UI | Character Vertical Slice |
| 23 | Customer Identity & Returning Visits | Unity + UI | Character Vertical Slice |
| 24 | Traits, Mood & Relationships | Unity + UI | Character Vertical Slice |
| 25 | Events & Player Decisions | Unity + UI | Character-driven Vertical Slice |
| 26 | Interior Furniture Model Set — Small Core Set First | Unity + Model | Cafe Growth |
| 27 | Progression & Unlocks | Unity + UI | Cafe Growth |
| 28 | Store Expansion | Unity + UI | Cafe Growth |
| 29 | Advanced Wall Construction & Room Editing — Deferred | Unity + UI | Cafe Growth Extension |
| 30 | Exterior Model Set — Deferred | Unity + Model | Exterior Decoration Extension |
| 31 | Exterior Zone & Facade — Deferred | Unity + Model | Exterior Decoration Extension |
| 32 | Outdoor Decoration Placement — Deferred | Unity + UI | Exterior Decoration Extension |
| 33 | Entrance Route & Exterior Validation — With Exterior | Unity | Exterior Decoration Extension |
| 34 | Seating & Dine-in Service — Deferred | Unity + UI | Cafe Growth Extension |
| 35 | Atmosphere & Themes — Full System Deferred | Unity + UI | Advanced Decoration Extension |
| 36 | Special Rooms & Multi-floor — Deferred | Unity + Model | Advanced Decoration Extension |
| 37 | Coffee Bean Visual Asset Set — Deferred | Model | Signature Content Extension |
| 38 | Coffee Bean Exploration — Deferred | Unity + UI | Signature Content Extension |
| 39 | Syrup & Add-on Visual Asset Set — Deferred | Model | Signature Content Extension |
| 40 | Syrup & Add-on Gameplay — Deferred | Unity + UI | Signature Content Extension |
| 41 | Bakery Visual Asset Set — Deferred | Model | Signature Content Extension |
| 42 | Bakery Gameplay — Deferred | Unity + UI | Signature Content Extension |
| 43 | Merchandise Visual Asset Set — Deferred | Model | Signature Content Extension |
| 44 | Merchandise Gameplay — Deferred | Unity + UI | Signature Content Extension |
| 45 | Offline Progression | Unity + UI | Feature-complete Alpha |
| 46 | Online Identity & Cloud Save — Optional | Online + Unity | Optional Online Services |
| 47 | UI/UX Integration & Accessibility | UI/UX + Unity | Release Preparation |
| 48 | VFX Production & Integration | Unity + Model | Release Preparation |
| 49 | Final Model Replacement & Asset Optimization | Unity + Model | Release Preparation |
| 50 | Mobile UI Adaptation | UI/UX + Unity | Mobile UI Ready |
| 51 | Android & iOS Platform Adaptation | Platform + Unity | Device-ready Builds |
| 52 | Mobile Release Preparation | Platform + Unity | Android & iOS Release Candidate |

### 4.2 挂机玩法适配审查（2026-10-26）

**依据与权限。** Owner 已确认长期体验为「安排经营 → 自动服务与积累 → 回来看成果和角色变化 → 作出选择、装修与成长 → 放心离开」，完整规则见 [Game Design 1.5、3.5 和第 5 节](AnimalCafe_Project_Design.md)。逐项审查后，Owner 已进一步指示「根据这些结论，调整 roadmap，然后继续开发 P15」：因此本节的提前验证与扩展延期现落实为开发路线，并进入 P15 设计准备。这个批准不等于 P18A／P20A 的具体 implementation spec 已批准，也不更改既有验收结果。

**审查结论。** 没有发现因挂机方向而必须删除整个 P0–P14 的依据。自动服务、可靠 Order、Capacity、Layout 与 Navigation 正是挂机基础。需要去掉的是对玩家反复操作的依赖；需要延后的主要是非最小闭环必需的完整内容。`可延后` 不表示相违背或已取消；`调整` 不表示现有代码已经修改；`保留` 不表示正式 closeout 或所有未来行为已经验证。

#### 4.2.1 已有 P0–P14 与 P8R

| Phase | 审查判断 | 保留价值与需要控制的范围 |
|---|---|---|
| P0 Project Foundation | 保留；时间合同待调整 | Camera、selection 和 input boundary 继续使用；现有 Pause／2x 不直接代表长营业周期或离线时间。 |
| P1 Layout Data Model | 保留 | 稳定布局数据与 ID 支持装修、扩张、Save 和离线能力判断。 |
| P2 Occupancy & Placement | 保留 | 有效布局与安全 transaction 是无人值守前提，不能绕过占用检查。 |
| P3 Visual Style & Asset Pipeline | 保留 | 动物观赏与装修依赖一致视觉；不因挂机而重建已验证 pipeline。 |
| P4 Architecture & Furniture Models | 保留 | 功能设备、Surface Slots 和 Anchors 支持自动经营与成长。 |
| P5 UI Architecture & Design System | 保留 | 复用管理、故事和摘要界面基础；不为尚未验证内容预建更多通用组件。 |
| P6 Basic Decoration | 保留；经营衔接待调整 | 装修是核心回访行为；暂停与确认修改后的旧业务处理须与未来时间／经济规则衔接。 |
| P7 Walls & Surface Customization | 保留 | 个性表达仍有价值；无需为了挂机重做墙面系统或提前完整结构编辑。 |
| P8 Functional Furniture & Readiness | 保留 | 自动营业仍必须使用有效设备和路径，不能自动开店就忽略 readiness。 |
| P8R Existing Feature Enhancement | 保留；不必继续扩展 | 已有输入与操作改进可复用；更多目录／时间控件 polish 不应阻塞挂机闭环验证。 |
| P9 Order Domain | 保留 | 唯一状态 authority、FIFO 和去重是前台与离线共用的业务基础。 |
| P10 Capacity & Reservation | 保留 | 限流与实际释放避免超容量和重复使用资源；summary 不逐帧跑 NPC 也不能忽略有效服务能力。 |
| P11 Navigation & Recovery | 保留；长期恢复政策待调整 | 路径、身体保护与有界失败必须保留；不能仅以玩家回来点 Retry 作为完整挂机恢复方案。 |
| P12 Spawn & Counter Queue | 保留；接待门禁待调整 | 打烊关闭新接待，已有顾客继续服务；活跃 Scene 的来客时钟不等于离线结算器。 |
| P13 Employee Task Flow | 保留；长期恢复政策待调整 | 自动派工、制作与送杯正好吻合方向；无需增加逐单指挥。 |
| P14 Integrated Cafe Loop | 保留；接入自动日循环 | 当前已有自动外带闭环；新增工作在周期、业务持久化与长时间离开后恢复，不重做逐单服务。 |

#### 4.2.2 P15–P52 逐项审查

| Phase | 审查判断 | 对挂机闭环的用途与建议 |
|---|---|---|
| P15 Business Day Lifecycle | 调整 | 自动营业→打烊排空→休息→下一周期；不要求 Start／Report／Next Day 点击。明确周期与 Pause／2x 的关系。 |
| P16 Economy & Daily Report | 调整 | 真实自动入账，报告可稍后查看；日报不作为领取奖励或开店 gate。 |
| P17 Save Foundation | 保留；为 P18A 提供持久化 | 保留周期状态、时间与结算去重所需信息；具体 schema 与最小结算范围须 spec 批准。 |
| P18 Application Shell & Loading | 调整 | 默认安全恢复并进入 Cafe；Boot／loading／recovery 必要，Title／Continue 不必成为每次启动的操作门槛。 |
| P18A Minimal Offline Foundation | 新增，首次挂机验证 | 在真实 Save／Economy 后处理结算区间、active order 交接、周期与去重，不等完整内容生产。 |
| P19 Recipe & Menu | 保留 | 玩家预先配置经营方向，系统自动选取可售商品，不要求逐单配置。 |
| P20 Inventory & Restocking | 调整；由 P20A 验证挂机闭环 | transaction 必要；缺货、基础供应与补货不能让普通经营依赖高频上线，自动化方式仍待定。 |
| P20A Idle Management Loop Validation | 新增，资源与成长闭环 | 在实际 Menu／Inventory 下复核 Offline，并验证一次可持久化的收益消费和回访体验。 |
| P21 Character Models & Animation | 保留；先审计资产缺口 | P13／P14 已有实际动物工作与 Carry，避免按旧 Capsule 假设重复生产；扩展 Species／Rig 能力仍有价值。 |
| P22 Staff Identity & Capabilities | 保留 | 员工能力和安排产生管理选择；自动分工为默认，逐单指定为可选优化。 |
| P23 Returning Customers | 保留 | 熟客与来访记录让玩家回来有值得关注的变化。 |
| P24 Traits, Mood & Relationships | 保留；建议小样验证 | 用少量可感知差异先验证角色吸引力，完整关系规则不必一次铺满。 |
| P25 Events & Player Decisions | 保留；明确非阻塞 | 重要决定暂存，其他经营继续；不强迫整点上线，故事不因错过时机永久丢失。 |
| P26 Interior Furniture Models | 部分可延后 | 小型装修消费出口有价值；完整产品展示家具不必成为最小挂机体验前置。 |
| P27 Progression & Unlocks | 保留；复用 P20A 最小成长反馈 | 早期先验证一次「收益→有意义改善」；完整 Strategy branches 仍在本 Phase，具体规则按 spec 批准。 |
| P28 Store Expansion | 保留；完整规模可延后 | 提供成长目标，最小循环不要求大量扩建区块。 |
| P29 Advanced Wall Construction | 可延后 | 不与挂机冲突，但路径／migration 成本高，不是最小闭环前置。 |
| P30 Exterior Models | 可延后 | 支持长期个性表达，无需阻塞挂机玩法验证。 |
| P31 Exterior Zone & Facade | 可延后 | 保留长期外观成长，早期可使用已有入口。 |
| P32 Outdoor Decoration | 可延后 | 非挂机 MVP 必需；不能提前开放可堵路物体却推迟安全验证。 |
| P33 Entrance Route & Validation | 与户外功能成套保留 | 一旦开放户外障碍与路径编辑，就必须同步验证入口安全；不能只取消验证部分。 |
| P34 Seating & Dine-in | 可延后 | 自动外带足够验证最小循环；未来点单、送餐和清桌同样须默认自动。 |
| P35 Atmosphere & Themes | 可延后完整数值系统 | 装修个性表达仍有价值，不要求为了单件最优数值摆不喜欢的家具。 |
| P36 Special Rooms & Multi-floor | 可延后 | 内容、导航、Camera 与 Save 成本高，不是挂机必需能力。 |
| P37 Coffee Bean Assets | 完整套装可延后 | 与 P38 所需最小可读视觉配套，不作为核心挂机前置。 |
| P38 Coffee Bean Exploration | 保留；完整系统可延后 | 派遣→离开→回来发现适合挂机；派遣不能使基础咖啡厅无人可工作，参数尚待设计。 |
| P39 Syrup & Add-on Assets | 可延后 | 与 P40 内容配套，不是基础循环前置。 |
| P40 Syrup & Add-on Gameplay | 保留；可延后 | 按菜单／设备配置自动制作，不要求每杯手动选择口味。 |
| P41 Bakery Assets | 可延后 | 新产品内容，不是最小挂机闭环必需。 |
| P42 Bakery Gameplay | 可延后 | 未来共用库存、自动制作与离线规则，不引入必须盯守的生产步骤。 |
| P43 Merchandise Assets | 可延后 | 与 P44 配套的长期内容。 |
| P44 Merchandise Gameplay | 可延后 | 支持商品与经营选择，补货不能成为频繁上线负担。 |
| P45 Offline Progression | 调整：P18A／P20A 先验证，本阶段完整整合 | 不等全部特色内容完成；负责所有实际开放系统、cap、时钟、防重复与恢复的完整离线整合。 |
| P46 Online Identity & Cloud Save | 保持可选 | 账号、Cloud 和网络服务不是本地挂机前提，继续遵守原 Activation Gate。 |
| P47 UI/UX Integration | 保留；关键 UX 随功能验证 | 返回摘要、待处理故事与自动进入体验不能等到总整合才首次出现。 |
| P48 VFX | 正式制作可延后 | 收益／状态的基本可读反馈随功能交付，完整 polish 不阻塞玩法验证。 |
| P49 Model Replacement & Optimization | 保留 | 正式资产按缺口替换，避免重复已完成资产；发布性能要求仍必要。 |
| P50 Mobile UI | 保留；建议早期有限小屏验证 | 核心回访和管理操作应早试用；完整适配仍由本 Phase 收尾。 |
| P51 Android & iOS Adaptation | 保留；建议提前生命周期 feasibility | 挂起、终止、返回与存档恢复是核心风险；早期窄验证不等于正式双平台验收。 |
| P52 Mobile Release | 保留 | release、device、balance、Save migration 与商店准备必要；不代表全部可选扩展都必须首发。 |

#### 4.2.3 有证据的冲突与回归要求

以下为静态 source／设计审查，不是 runtime failure 报告：

1. **时间口径。** `GameTimeService.TrySetSpeed` 写入 Unity `Time.timeScale`，`CafeLoopController.Update` 使用 `Time.deltaTime`。现有 Pause／2x 合同应保留；不能未经决定就把它用于八小时现实周期。未来覆盖 Pause／2x／装修、跨营业边界、suspend／resume、clock rollback 与 large jump。
2. **Blocked 扩大影响。** `CustomerFlowController.Step` 使用任一顾客 `Blocked` 限制队伍前移与新接待，`CafeLoopController.ResumeBlockedFlow` 提供显式恢复；现有 test 也要求 Blocked 不能自动 retry。这是既有保护规则，不是本轮确认的 bug。未来区分临时等待、有条件的有界再验证与永久封路；检查无人值守时不会无限 retry、重复收费、泄漏容量或虚构服务完成，并如实记录受影响时段。
3. **装修 cleanup 与打烊不同。** `CafeLoopController.OnChangedLayoutExitPreparing` 和 `CafeLoopService.ClearOrdersForDecoration` 会退休旧 epoch、重建 Order store 并清理旧付款集合。不能直接用于每日打烊，否则与「服务已接待客人」冲突。未来覆盖收银提交、Working、Prepared、Ready、已领取离店与 Blocked 时打烊；既有订单、商品、reservation 与可统计的业务事实不能被抹去。已批准装修行为本轮不改，接入 Economy／Save 前须设计其兼容规则。
4. **活跃 spawn 不补发积压。** `CustomerSpawnClock.Tick`／`CompleteAdmission` 只保留一个到期机会，长帧剩余时间不补发。不能把数小时直接传给现有 `Step` 当作完整离线结算；未来比较分段／整段离开、跨营业／休息边界及库存／容量约束。
5. **规划中的交互 gate。** 原 P15 将 Report 放在生命周期边界，P18 列出 Title／Continue；这些尚不是已实现的强制点击，需在 spec 前消除歧义。报告未读、故事待选和重复打开报告，不得阻止正常经营或改变已结算数据。
6. **P21 旧资产假设。** P13／P14 已使用 Shiba／Westie、working／carry 与顾客持杯表现。先盘点可复用资产和实际缺口，再确定 P21 范围，避免重新生产同一成果；未来仅对新 Rig／Species 与受影响动画做针对性验证。

复核入口：[GameTimeService](../Assets/Scripts/Core/Time/GameTimeService.cs)、[CafeLoopController](../Assets/Scripts/CafeLoop/CafeLoopController.cs)、[CafeLoopService](../Assets/Scripts/CafeLoop/CafeLoopService.cs)、[CustomerFlowController](../Assets/Scripts/Customers/CustomerFlowController.cs)、[CustomerSpawnClock](../Assets/Scripts/Customers/CustomerSpawnClock.cs)、[P14 Service Tests](../Assets/Tests/EditMode/Phase14/CafeLoopServiceTests.cs)。

#### 4.2.4 已采用的开发顺序与实施边界

保留原 P0–P52 编号、既有成果与依赖记录；增加 P18A 与 P20A 两个有明确消费者的小阶段，避免把第一轮挂机验证拖到 P45。**以下路线已按 Owner 指示采用；每阶段具体参数、design／plan 和实现仍按原 approval gate：**

1. P15 先确定自动循环、时间政策和日边界；短测试周期可以验证状态转换，但不能宣称已经完成八小时无人值守测试。
2. P16 建立真实收入与可回看记录；不要求领取或关闭报告才能继续。
3. P17–P18 完成可靠 Save／load 和自动进入 Cafe 后，执行 P18A：基础 Coffee／真实 Economy／周期状态的最小「离开→返回→恢复周期→只结算一次」vertical slice，并纳入窄 mobile suspend／resume feasibility。不提前全套产品与故事，不声称 Editor 代表真机通过。
4. P19–P20 接入真实菜单、库存与基础供应后，执行 P20A：复核含资源约束的 Offline 与前台一致性，使用一个已存在资产／基础改善作为收益消费出口，试玩完整回访闭环。此处只抽取最小成长反馈；不提前完整 P26 内容库或 P27 Strategy branches。具体改善、价格及 transaction 由 P20A spec 决定。
5. P21–P25 用少量正式角色、熟客变化与一个非阻塞故事验证回访吸引力；不预设玩家已选择角色陪伴优先或经营优化优先。
6. P26 先做小型核心家具集；P27–P28 接入已验证的成长与扩建。P29–P44 完整扩展移到挂机／角色闭环验收后按价值选择，不再是 P45 或首个可发布版本的强制前置；保留各自内部安全依赖。P45 只整合届时实际开放的系统，不要求先实现所有延期内容。

**实际执行路线：** P15 → P16 → P17 → P18 → P18A → P19 → P20 → P20A → P21–P25 → P26 核心集 → P27–P28 → P45 当前已开放系统整合 → P47–P52 发布准备。P29–P44 是后续扩展队列；如在 release 前选中某项，则同时完成对应资产、业务、Save、Offline 和安全验证，不因跳过相邻编号就跳过真实依赖。P46 继续保持按需求启用的可选服务。

P47 的必要 UI／摘要与 P50–P51 的核心小屏／生命周期 feasibility 随对应功能早验证；最终跨系统整合、双平台测试和 release acceptance 仍在原阶段。原 Milestone 名称和编号保留历史意义，不把延期扩展的全部完成作为基础挂机版本的隐含 gate。

明确不必增加：逐杯 QTE、普通订单反复派工、强制 Start／Next Day／领奖、未读报告锁店、必须赶整点的故事、只为维持营业而高频补货，以及无实际缺口的资产或 foundation 重做。它们与“放心离开”相违背，或没有当前最小闭环的必要性。

本轮 Design／Production／Engineering／QA 使用当前文档与源代码交叉静态复核，文档与实现差距已记录；未作新的正式 Phase closeout。长期无人值守 runtime、Offline settlement、Unity regression 与 mobile device acceptance 本轮均 `NOT_RUN`。文档验证只检查覆盖、引用、措辞与 Git diff；不把旧测试结果当作新玩法验证。

---

## 5. Global Phase Gates

每个 Phase 的具体 tests 不同，但必须满足以下共同 gate。

### 5.1 Design Gate

- Goal、Included 和 Not Included 明确。
- File responsibilities 和 system interfaces 明确。
- Open Design Questions 已解决，或明确不在本 Phase 处理。
- 难点、风险、常见 bugs 和 recovery strategy 已记录。
- 用户批准 design 后才写 implementation plan。

### 5.2 Implementation Gate

- 使用小步 implementation，优先采用 failing test → minimal implementation → passing test。
- 每个 Task 完成后运行 focused tests 和直接相关 regression；完整 regression 留到 Phase 收尾。
- 不把多个未验证 subsystem 同时接入 Scene。
- 缺失 dependency 时 fail clearly，不允许静默产生错误状态。
- Runtime data 不以 UI 或临时 Scene reference 作为唯一可信来源。

### 5.3 Verification Gate

- 当前 Phase focused tests 全部通过。
- Phase 收尾时运行一次完整 EditMode、PlayMode 和必要 integration regression；不在每个 Task 后重复运行所有旧 Phase tests。
- Integration tests 覆盖本 Phase 与直接 dependencies。
- 必要的人工 Play Mode checklist 完成。
- Console 没有未处理 error。
- 如果修改持久数据，Save / Load、invalid data 和 migration tests 通过。
- Engineering、QA 和 Production 默认在 Phase 收尾各 review 一次；只有明显视觉变化才需要 Art/UX review。
- Critical 和 Important 必须修复；Minor 默认进入 polish backlog，不阻挡 Phase。
- docs-only 修正只做静态验证，不重新运行 Unity。
- Roadmap 只在全部 gate 通过后记录 `Completed` evidence。

### 5.4 Feature UI 与 Functional Feedback Rule

- 每个 gameplay Phase 同时交付完成该功能所需的可用 UI，不把所有页面推迟到 Phase 47。
- Feature UI 必须复用 Phase 5 的 Design System 和 navigation rules。
- 每个功能同时交付用于理解状态的 functional feedback，例如 placement validity、Order ready、Coins change、Unlock 或 Error reason。
- Phase 47 负责全局 UI/UX integration、onboarding 和 accessibility；Phase 48 负责正式 VFX，不负责第一次补齐缺失的核心反馈。

---

# Milestone A — Visual & Layout Foundation

## Phase 0 — Project Foundation

> 状态：Completed
>
> 完成日期：2026-07-25
>
> 验证环境：Unity `6000.5.5f1` / Windows
>
> 原始验收结果（2026-07-25）：16 / 16 Play Mode tests passed；`MainCafe` Scene load 与 Console error scan passed；Camera pan、zoom、bounds、isometric baseline、selection feedback、Pause、`1x` 和 `2x` 已完成人工验收。
>
> Completed-phase hardening evidence（2026-07-30）：Game Time owner hardening 后 baseline 为 21 / 21 PlayMode；merge 前 review hardening fresh full EditMode `116 / 116`、fresh full PlayMode `31 / 31` passed，failed、skipped、inconclusive 均为 `0`。新增 Scene-owned root canonicalization、disabled / inactive / destroyed selection cleanup、真实 Input System mouse integration coverage，以及 single-warning、可恢复的 missing-material handling。鼠标在 UGUI 控件上按下并松开时，pointer release 不会再穿透到世界中的 selection。
>
> Completed-phase hardening manual evidence（2026-07-30）：用户确认 `MainCafe` Hierarchy 中 `Phase0_Runtime`、`Phase0_TimeControls`、`EventSystem` 各一个；Camera pan / zoom、Pause / `1x` / `2x` 正常；Console clean；Test Runner EditMode `116 / 116`、PlayMode `31 / 31` 全部通过。

### Goal

提供所有后续 Scene interaction 和 runtime systems 共用的基础。

### Completed Scope

- 固定斜俯视 Camera、pan、zoom 和 bounds。
- Mouse camera input，并保留未来 touch input boundary。
- Scene selection 与 visual feedback。
- Pause、`1x` 和 `2x` Game Time。
- Event Bus、runtime assembly 和基础 Play Mode tests。
- `Phase0_Runtime`、`Phase0_TimeControls` 与 `EventSystem` inactive / duplicate root repair。
- Disabled、inactive 或 destroyed selectable 的安全 selection cleanup。
- 真实 mouse press / drag / release、same-frame cache 与 Pause input regression。

### Regression Requirement

原始 16-test acceptance evidence 保留为历史记录；当前 regression baseline 是 full EditMode `116` tests 与 full PlayMode `31` tests。后续 Phase 必须保持所有现有 tests 通过，并在 test count 改变时记录 fresh totals。

---

## Phase 1 — Layout Data Model

> 状态：Completed
>
> 验证环境：Unity `6000.5.5f1` / Windows
>
> Automated evidence（2026-07-27）：review fix 后 fresh final EditMode `116 / 116` passed；fresh final PlayMode `18 / 18` passed；两轮均为 failed `0`、skipped `0`、inconclusive `0`。Layout Domain source boundary scan、旧 Phase 1 runtime scan、orphan metadata scan 与 `git diff --check` 通过；Unity test drift 已恢复，generated worktree `.slnx` 已删除。
>
> Manual evidence（2026-07-27）：用户完成 spec §18 Scene、PlayMode 和 Test Runner checks；Scene 无 demo cubes、旧模型或临时 floor，controls 与 mouse input 无 Console error，Console clean。用户已明确批准 merge。
>
> Merge evidence（2026-07-27）：`codex/phase1-layout-data-model` 已 fast-forward merge 到本地 `main`。merged `main` fresh EditMode `116 / 116` passed；fresh PlayMode `18 / 18` passed；failed、skipped、inconclusive 均为 `0`。

### Goal

建立不依赖 Unity Scene GameObject 的咖啡厅布局数据。

### Scope

- `CafeLayout`
- `FurnitureDefinition`
- `FurnitureInstance`
- Stable furniture instance ID
- Grid position、rotation 和 footprint
- 已解锁 Grid cells / regions
- Layout Zone type：`Interior`、`Exterior` 与未来 special area
- Placement surface compatibility 的扩展字段
- Definition lookup 与 data validation

### Why Now

后续 Decoration、Functional Furniture 和 Save 都依赖稳定 Layout data。先建立数据边界，可以避免 Scene 成为唯一真相。

### Main Difficulty 与 Solution

难点是区分家具类型和玩家放置的家具实例：

```text
FurnitureDefinition = 某种家具是什么
FurnitureInstance = 玩家放在具体位置的那一件家具
```

Definition 保存静态规则；Instance 只保存 stable ID、definition ID、位置、旋转和玩家状态。

### Risks / Likely Bugs

- Definition ID 与 Instance ID 混用。
- Scene object reference 被错误写进纯数据。
- Duplicate stable IDs。
- Invalid rotation 或 footprint 被接受。

### Tests

- Furniture instance IDs 唯一。
- Definition lookup 成功与失败行为明确。
- Position、rotation 和 footprint data 正确。
- Invalid definition、rotation 和 footprint 被拒绝。
- Layout domain tests 不需要加载 `MainCafe` Scene。

### Not Included

Grid occupancy、UI、Scene placement、functional anchors、Save file，以及实际 Exterior gameplay。

---

## Phase 2 — Grid Occupancy & Placement Rules

状态：Completed

验证环境：Unity 6000.5.5f1 / Windows

Automated evidence（2026-07-27）：fresh final EditMode 184 / 184 passed；fresh final PlayMode 18 / 18 passed；failed、skipped、inconclusive 均为 0。Placement/transaction/consistency、source boundary、旧 Phase regression 与 git diff check 已通过。

Latest-main integrated evidence（2026-07-30）：Phase 0 Scene cleanup 1 / 1、Phase 0 PlayMode 25 / 25、GridPlacementTests 67 / 67、full EditMode 184 / 184、full PlayMode 31 / 31 passed；failed、skipped、inconclusive 均为 0。Layout production source 没有 UnityEngine reference，Assets 中没有遗留 AddFurnitureInstance call，conflict marker 与 git diff check 均通过。

PR hardening Option A fresh full regression evidence（2026-07-30，状态仍为 In Review）：`Logs/Phase2PrHardeningOptionAFullEditMode.xml` 为 EditMode `191 / 191` passed；`Logs/Phase2PrHardeningOptionAFullPlayMode.xml` 为 PlayMode `35 / 35` passed；failed、skipped、inconclusive 均为 `0`。覆盖 Phase 0 与 Phase 1 regression。真实 `InputSystemUIInputModule`、Canvas、`GraphicRaycaster` 与 virtual Mouse tests 保护 UI 不穿透到 world selection；`FurnitureDefinition` footprint 超过 `1024` cells 会在 placement 前被拒绝，避免错误的巨大数据创建很长的 cell list。

PR hardening Option A manual evidence（2026-07-30）：用户在 `MainCafe` Play Mode 中创建 temporary `ColorSelectable` Cube，确认点击 `Pause`、`1x`、`2x` 后 Cube selection 一直保持；Camera pan / zoom 正常；Console clean。退出 Play Mode 后 temporary Cube 自动移除。

Manual evidence（2026-07-30）：用户确认 EditMode `184 / 184`、PlayMode `31 / 31` 全部通过；`GridPlacementTests` categories 完整；`MainCafe` 没有提前出现 Phase 2 可见 Grid、furniture、preview 或 UI；mouse pan、wheel zoom、Pause、`1x`、`2x` 正常；Console clean；Beginner Guide 可理解。

Merged-main evidence（2026-07-31）：GitHub PR #1 已 merge 到 `main`，merge commit 为 `abf6729`。在该 merge commit 的独立 worktree 中运行 fresh full regression：EditMode `191 / 191`、PlayMode `35 / 35` passed；failed、skipped、inconclusive 均为 `0`。Phase 2 的 design、implementation、review、manual acceptance、merge 与 merged-main regression gates 已全部通过，因此状态更新为 `Completed`。

### Goal

用纯规则判断家具能否放置、移动、旋转或移除。

### Scope

- Grid bounds
- Unlocked regions
- Footprint rotation
- Occupy / release
- Overlap detection
- Placement transaction
- Move / rotate / remove validation

### Why After Phase 1

Occupancy 必须操作稳定的 Layout data。先证明规则正确，Phase 6 的 visual placement 才不会混合 UI bugs 与数据 bugs。

### Main Difficulty 与 Solution

移动家具使用 transaction：

```text
保留旧状态
→ 验证完整新位置
→ 全部合法才 commit
→ 失败则保持旧状态
```

### Risks / Likely Bugs

- Grid boundary off-by-one。
- `90°` 旋转后 footprint 宽高错误。
- Move 失败后旧 cells 没有恢复。
- 同一个 Instance 重复占格或重复释放。

### Tests

- `1×1`、矩形和非正方形 footprint。
- `0° / 90° / 180° / 270°` rotation。
- Bounds、locked region 和 overlap rejection。
- Failed move 保留旧位置。
- Repeated remove / release 安全。
- Occupancy 与 Layout instances 数量一致。

### Not Included

Mouse placement、preview、Scene rendering、pathfinding。

---

## Phase 3 — Visual Style & Asset Pipeline Foundation

### Goal

建立所有正式 Models、Materials、Textures 和 Unity Prefabs 共用的视觉方向与技术标准。

### Status

`Completed` — implementation、PR review hardening、Studio Owner manual
Camera/readability acceptance、source license/use-right confirmation、GitHub
merge 与 merged-main regression gates 均已通过。PR #2 merge commit 为
`fb410d3`。

Latest review-hardening and merged-main automated evidence (2026-08-03):

- full EditMode: `312 / 312` passed; failed/skipped/inconclusive all `0`;
- full PlayMode: `55 / 55` passed; failed/skipped/inconclusive all `0`;
- production benchmark validator: `3 / 3` Prefabs valid, `0 issues`;
- focused AssetPipeline EditMode: `111 / 111` passed; focused readability
  Scene EditMode: `15 / 15` passed; focused AssetReadability PlayMode: `9 / 9`
  passed;
- validation Scene now uses pale-yellow `#F2E6B8`, scene-only `SMAA High`, a
  dedicated teal Character Scale Reference, and the three Blender original-color
  packed Base Color Textures downscaled to project-relative `512 × 512` sRGB PNGs;
- Camera-projected bounds tests now use exact Landscape `1920 × 1080` and
  Portrait `1170 × 2532` aspects at size `4`. Character Scale Reference local
  `(1.75, 0, 2.00)` stays within `0.01` safe margin and does not overlap either
  station; BatchDisplay local `(-30, 0, 30)` remains outside both viewports;
- exact-aspect focused evidence is EditMode `20 / 20` and PlayMode `14 / 14`
  passed; no temporary Portrait Camera-size workaround remains;
- the approved Size 4 trial now uses orthographic size `4` by default; focused
  EditMode `18 / 18` and focused PlayMode `12 / 12` prove all single-display
  renderer bounds remain inside the real Camera viewport with `0.01` margin;
- `MainCafe.unity` remains unchanged; PlayMode cleanup restored Build Settings
  to its single enabled `Assets/Scenes/MainCafe.unity` entry.

Known limitation: this is a benchmark pipeline and readability baseline only.
It is not the Phase 4 formal asset set, gameplay, placement, or runtime
integration.

### Deferred Formal Camera Contract

- Formal gameplay framing is **Portrait-first**.
- Gameplay zoom is one continuous factor from `1.0x` through `3.0x`, not three
  preset Camera sizes. Android / iOS runtime uses pinch zoom over the same
  logical range; Unity Editor mouse wheel remains a development test mapping only.
- Exact base orthographic size, Portrait framing and adaptive aspect behavior
  remain deliberately open until formal Portrait/mobile Camera work. The P3
  size `4` / `7` / `12` readability checks are visual proxies, not final zoom
  stops and not approval of an exact gameplay base.
- Phase 50 owns Portrait/mobile presentation, Safe Area and UI-versus-Camera
  gesture priority, including selection of the exact base framing. Phase 51
  owns Android / iOS pinch and device integration; both platforms must reuse
  the same `1.0x`–`3.0x` logical envelope rather than fork gameplay Camera rules.
- Any formal Camera change must preserve Phase 0 pan / zoom semantics、Camera
  bounds and existing input / UI regressions through fresh Phase 0 tests；Editor
  mouse fixtures remain regression tools rather than player-facing requirements.

### Scope

- Art direction、color palette 和 shape language
- Unity / DCC tool scale convention
- Pivot、forward direction、rotation 和 naming
- Source files、FBX export 和 Unity import rules
- Material、texture、shader 和 folder conventions
- Collider、LOD 和 mobile asset budgets
- Prefab assembly 与 asset validation checklist
- Unity primitives 的 placeholder color / meaning rules

### Why After Grid Rules

Grid cell、footprint 和 rotation 已稳定，Model standards 才能使用真实空间约束；更早决定容易因 Grid 改变而返工。

### Main Difficulty 与 Solution

视觉风格和技术规格必须同时成立。先制作少量 benchmark assets，验证从 source file 到 Unity Prefab 的完整 pipeline，再批准大量生产。

### Risks / Likely Bugs

- Source scale 与 Unity scale 不一致。
- Pivot 和 forward direction 在不同 Models 中不统一。
- Materials 或 textures 使用 machine-specific paths。
- Art style 很漂亮但 footprint、Collider 或 mobile budget 不可用。

### Tests

- Benchmark asset import scale、pivot 和 forward validation。
- Material / texture references 完整。
- Naming 与 folder validator。
- Collider 和 LOD policy checks。
- Unity Editor / URP development compatibility，以及 Android / iOS shader compatibility expansion gate。
- Mobile budget baseline。
- Source → export → import → prefab 流程可重复。

### Not Included

大量正式 Models、角色 Rig、完整 UI 页面和 gameplay implementation。

---

## Phase 4 — Core Architecture & Functional Furniture Models

### Status

`Completed` — 2026-08-08。Approved spec、TDD implementation、code review、Studio Owner `M01–M88` manual acceptance 与 final regression gates 均已完成。Merge-preparation fresh Unity `6000.5.5f1` evidence（2026-08-11）：Full EditMode `570 / 570`、Editor PlayMode `62 / 62`、Windows standalone PlayMode `62 / 62` passed；failed、skipped、inconclusive 均为 `0`；PlayMode assembly 不引用 `UnityEditor`；P3 benchmark validator `3 / 3 valid`、P4 production validator `5 / 5 valid`，issues `0`。临时 `TEMP_P4_ManualReviewFixtures_DELETE_LATER` cubes 会保留到 MainCafe 有正式画面时再删除，并同步删除两个 temporary materials、setup utility、mover 与对应 regression tests。

### Goal

制作第一批符合正式规格的建筑结构和核心功能家具。

### Scope

- 与 `8 × 8` Grid 对齐的浅黄色 Floor、两面固定金黄色 Wall、开放边界 Entrance Portal 和一个默认 Window；使用 approved 配色 B。
- 每面 Wall 提供 8 columns × 2 rows 的 `1 m × 1 m` Wall Slots；Window 使用固定 `1 × 1` Wall Footprint，与未来 Wall Decoration 共享 occupancy 且暂时不能自由旋转。
- Entrance Portal 使用稳定 ID，并在内侧保留可行走但禁止家具占用的 `2 × 2` Entrance Clearance Zone。
- Work Table、`1 × 1` Counter Module、Coffee Machine、Cash Register 和 Ceramic Cup visual。
- `1 × 1 Counter Module` 提供一个主要 Surface Slot；`1 × 3 Counter` authoring fixture 证明一个完整 Instance 可提供三个独立 Surface Slots。
- Cash Register 使用 `vintage computer monitor 3d model.glb` 的清理、缩放和优化版本，必须标识互相相反的 Employee Side 与 Customer Side；Customer Side 同时提供未来 Queue 的初始方向。
- Coffee Machine 使用 `+Z` 正面作为员工 Interaction Side；Coffee Machine 与 Cash Register 可在 Surface Slot 上按 `90°` 旋转。
- Ceramic Cup 是后续制作与 Pick-up 流程使用的临时商品视觉，不创建 `FurnitureDefinition Asset`，也不作为永久家具占用 Surface Slot。
- Source files、Models、Materials、Colliders 和 Unity Prefabs
- Accurate footprint、surface 和 anchor markers
- 建立所有未来家具共用的 Unity `FurnitureDefinition Asset` authoring 入口。Studio Owner 可在 Inspector 中为每件家具填写 stable ID、Display Name、Prefab、`Footprint Width × Depth`、Placement Surface 和 Function Type；例如大桌子填写 `2 × 3`，长沙发填写 `1 × 3`。
- 建立 `FurnitureDefinition Asset → FurnitureDefinition → Prefab` mapping 与 catalogue registration。Grid footprint 是明确的 gameplay data，不根据 Model bounds 或 Collider 自动猜测。

### Why Before Decoration

Basic Decoration 和 Functional Furniture 必须用真实或生产级尺寸验证 footprint、pivot、Surface 和 Interaction Anchors。

### Main Difficulty 与 Solution

先制作一个小而完整的 vertical asset set，不追求全游戏数量。每个 Model 同时通过 visual review 和 technical validation。

### Risks / Likely Bugs

- Model 视觉尺寸与 Grid footprint 不一致。
- Definition 填错 `2 × 3` / `3 × 2`，或漏掉 Prefab、ID、surface，导致 preview 与实际占格不一致。
- Entrance、counter 或 machine pivot 导致 placement 偏移。
- Collider 阻挡原本可用的 Interaction Anchor。
- Prefab 修改覆盖 source import data。
- Floor、Furniture Surface 与 Wall Slot occupancy 被错误混在一起。
- Cash Register 的 Customer / Employee sides 填反，或 Window 与 Wall Decoration 错误重叠。

### Tests

- Scale、pivot、rotation 和 footprint。
- Inspector authoring 支持 `1 × 1`、`2 × 3`、`1 × 3` 等合法 footprint；拒绝零值、负值、过大 footprint、duplicate ID 和 missing Prefab。
- `2 × 3` furniture 旋转 90° / 270° 后占用 `3 × 2`，旋转 0° / 180° 后保持 `2 × 3`。
- Work Table、Counter、Coffee Machine 和 Cash Register 的第一批正式 Definition 能加载正确 Prefab、footprint、Placement Surface 和 Function Type；Ceramic Cup 只作为已验证的商品视觉加载。
- `1 × 1` Counter 提供一个 Surface Slot；`1 × 3` Counter fixture 提供三个稳定且独立的 Surface Slots。
- Cash Register 的 Customer Side 与 Employee Side 必须互相相反；Coffee Machine `+Z` 正面必须正确。
- `8 × 8` Floor、两面各 `8 × 2` Wall Slots、默认 Window 和 `2 × 2` Entrance Clearance fixture 正确。
- Collider 与 visual bounds。
- Window wall attachment、Wall Footprint、overlap、out-of-bounds 与 cross-corner rejection。
- Functional furniture surface 和 anchor markers。
- NavMesh obstacle smoke test。
- Missing material / texture / reference scan。
- Scene 中的 primitive 可被对应正式 Prefab 安全替换。
- 正常、invalid、boundary、regression 与 Studio Owner manual acceptance 全部按 approved P4 spec 留存证据。

### Downstream Ownership

- Phase 5 建立 UI foundation；Phase 6 实现 Floor Furniture 的 Decoration Mode、selection、preview、confirm 与 cancel。
- Phase 7 复用 Wall Slot contract，实现 Window 与其他 Wall-mounted objects 的移动、增加、移除及 Surface customization。
- Phase 8 将 Function Type、Surface Slots 与 Interaction directions 接入 Layout Readiness。
- Phase 11–14 分别负责 Navigation、Customer Queue、Employee tasks 与完整基础 Coffee Loop。
- Phase 17 建立 Save / Load；Phase 29 实现 Entrance 或真实 Door / Window Opening 的高级结构移动与 migration；Phase 33 验证 Exterior Spawn → Entrance route；Phase 48 制作正式 Entrance VFX。

---

## Phase 5 — UI Architecture & Design System

### Goal

建立所有功能 UI 共用的结构、视觉语言和 input boundary。

### Scope

- UI technology 与 assembly boundary
- Canvas / panel architecture
- Reusable button、panel、text、icon 和 modal components
- Typography、color 和 spacing tokens
- Notification、tooltip 和 validation-message patterns
- Resolution scaling
- Touch-first interaction；Mouse only as Unity Editor development / test mapping
- UI input blocking 和 Scene input boundary
- Safe Area、accessibility 和 localization expansion points

### Confirmed Discovery Baseline

以下内容是 Phase 5 design discovery 中已经确认、必须带入正式 design spec 的 foundation baseline；它们不替代独立 spec / implementation plan approval，也不授权提前实现未来 feature UI：

- Runtime 使用 `uGUI`；Phase 5 新建 runtime text components 使用 `TextMeshPro`。Future Unity Editor tools 可以独立使用 `UI Toolkit`，但同一 runtime game screen 不混用两套 UI systems。
- 一个 `UI Root` 管理三个 Canvas、四个 logical layers：`HUD Canvas / HUD Layer`、`Screen Canvas / Panel Layer + Modal Layer`、`Toast Canvas / Toast Layer`。普通窗口不自行增加 Canvas；只有 profiling evidence 才允许调整拆分。
- Phase 5 core library 限于 Button、Panel、Modal、Bottom Sheet、Text Style、Icon Container、Input Blocker、Toast、Tooltip、Validation Message 和 Safe Area Container。Future gameplay Phase 按真实需求增加 Tab、List、Slot、Resource Row 等 feature components。
- 一次 pointer interaction 只能属于 UI 或 Scene；用于关闭 UI 的同一次点击不得继续传给 Scene。Modal 阻止下层 UI 与 Scene，Toast 默认不接收点击。
- Figma 负责视觉目标，approved design spec 负责设计合同，Unity Prefab / Material / Shader / animation / C# 负责 runtime；统一 `AnimalCafeUiTheme` 对照 Figma Variables。
- Phase 5 reference baseline 为 portrait `1080 × 1920`，Landscape 必须功能可用且无裁切；正式 mobile presentation 仍由 Phase 50 finalization。
- 视觉 baseline 为 cream、warm wood、sage；常驻 UI 使用 Light Frost，重要大型面板可使用单一 Strong Frost，低性能配置必须回退到可读的 Light Frost。
- Minimum touch target baseline 为 `48 × 48` logical pixels；正文 / 小标签 baseline 分别不小于 `16 / 14`；主要字体 baseline 为 `Noto Sans SC`。
- Toast queue / duplicate merge、Touch-safe Tooltip、specific Validation Message、Reduced Motion expansion point 和 long-localized-label fixtures 属于 foundation contract。

Phase 5 的长期合同与 Provisional / Future Finalization ownership 统一保存在 canonical Phase 5 Spec。任何 provisional baseline 调整都必须同步 approved spec、Unity Theme / Prefabs 和 tests。

### Why Before Decoration UI

Decoration Mode 是第一个 UI-heavy system。先建立 reusable system，避免每个 gameplay feature 自建不兼容的 buttons、panels 和 modals。

### Main Difficulty 与 Solution

先建立小型 component library 和 interaction rules，不制作所有最终页面。Feature phases 负责使用这些 components 实现自己的 UI。

### Risks / Likely Bugs

- UI click 穿透到 Scene。
- Modal 打开后底层 UI 仍可操作。
- Text overflow 或不同 resolution 下重叠。
- Editor Mouse fixture 与正式 Touch rules 意外形成两套 behavior。

### Tests

- Multi-resolution scaling。
- Modal input blocking。
- UI / Scene click separation。
- Pause 时 UI 仍可操作。
- Text overflow 和 long-label fixtures。
- Minimum touch-target boundary。
- Theme component visual consistency。

### Not Included

Title Screen、完整 feature pages、正式 icons、tutorial 和 mobile-specific layout。

### Status

`Completed` — Phase 5 approved design、TDD implementation、review hardening、Studio Owner `MT001–MT034` manual acceptance、PR #4 merge 与 merged-main regression gates 均已完成。

Merged-main evidence（2026-08-15）：PR #4 merge commit 为 `1ac7064`；fresh post-merge rerun 为 EditMode `690 / 690`、Editor PlayMode `121 / 121`、Windows standalone PlayMode `103 / 103` passed，failed、skipped、inconclusive 均为 `0`。首次 post-merge EditMode run 因两个 canonical TextMeshPro shader 的 SHA-256 mismatch 出现 `62` 个 setup failures；资源恢复后完整 rerun 全部通过，两个 shader 当前内容与 Git index 一致。

---

## Phase 6 — Basic Decoration Mode

### Goal

让玩家在 Scene 中安全地放置和调整普通家具。

### Scope

- 进入 Decoration Mode 自动 Pause。
- Furniture catalogue placeholder。
- Catalogue item 读取 Phase 4 的 `FurnitureDefinition Asset`，而不是在 UI 或 placement code 中重复写 footprint。
- Placement preview。
- Placement preview 按 Definition 的 `Footprint Width × Depth` 显示完整占格；旋转时同步交换 width / depth。
- Select、move、rotate、confirm、cancel 和 store。
- Valid / invalid visual feedback。
- Layout data 与 Scene representation 同步。

### Why After Phase 2

UI 只调用已测试的 placement rules，不自己决定合法性。

### Main Difficulty 与 Solution

Preview 使用临时 placement state，不直接修改正式 Layout。只有 Confirm 才提交 transaction。

### Risks / Likely Bugs

- Preview 意外写入正式 Layout。
- Cancel 后家具丢失。
- UI click 穿透到 Scene。
- Scene representation 与 Layout data 不一致。
- Decoration Mode 退出后 Game Time 未恢复到预期状态。

### Tests

- Preview 不改变正式 Layout。
- Confirm 只提交一次。
- Cancel 恢复原位置。
- Illegal placement 不能确认。
- `2 × 3`、`1 × 3` 等 multi-cell furniture 必须检查全部 cells；任意一格 blocked、locked 或 overlap 都不能 Confirm。
- Rotate、move、store 后 data 与 Scene 一致。
- Decoration Mode 强制 Pause。
- UI interaction 不触发 Scene placement。
- 人工检查 preview、颜色反馈和 beginner 操作流程。

### Not Included

经营功能、anchors、路径验证、家具商店、Atmosphere。

---

## Phase 7 — Interior Walls & Surface Customization

### Status

`Completed — merged to main`（2026-09-01；post-merge remediation locally verified 2026-09-02）。Approved design、TDD implementation、review amendments、Engineering/QA closeout 与 Studio Owner `MT-001–MT-034` manual acceptance 均已完成；Owner decision 为 `GO`。PR #6 已以 merge commit `925213af6132597592aa60d815434259b18b8ed1` 合入 `main`。Latest fresh local evidence：standalone committed-state preflight `1/1`、EditMode `1451/1451`、PlayMode `638/638`，failed/skipped/inconclusive 均为 `0`；post-merge remediation 修复了 Windows staged Scene rollback/backup retention 与 `SceneAsset` selection restore、Wall base explicit Kind lookup、duplicate-root continuation reporting、Wall Decor drag Preview/Fade reuse，以及 destroyed blocker-root fade cleanup。Remediation 的 commit/push closeout 尚待单独授权。

Known scope：不建造、移动或删除墙；Window 不切割真实 wall opening；confirmed Window 只在当前 runtime session 存在，reload 后清除；本 Phase 未宣称 player build evidence。

### Goal

在固定墙体结构中，让玩家安全更换室内墙面与地面风格，并放置 Wall Decoration。

### Scope

- 选择稳定 wall segment、wall surface 或 floor region。
- Paint、Wallpaper、Wainscoting 和 Floor Surface definitions。
- Surface preview、confirm 和 cancel transaction。
- Wall Decoration 与 Window attachment、move 和 remove；Wall-mounted objects 使用 author-defined fixed footprint，暂时不提供自由旋转。
- Door / window opening compatibility；P4 wall-mounted Window 不切割 Wall geometry，并与其他 Wall Decoration 共用 Slot occupancy。
- Camera 遮挡时的墙面编辑显示。
- Stable Surface ID 与未来 Save contract。
- 明确排除建墙、拆墙和移动墙。

### Why After Basic Decoration

Phase 6 已证明 selection、preview、confirm、cancel 和 UI input boundary。墙面系统可以复用这些 interaction，而不重新实现 Decoration Mode。

### Why Before Functional Furniture

墙体和 Surface 会影响 Furniture attachment、Interaction Anchor validity、Camera 可读性和经营路径。先稳定 Surface contract，Phase 8 才能安全验证功能家具。

### Main Difficulty 与 Solution

墙面外观和墙体结构分离保存。Paint、Wallpaper 和 Wainscoting 只修改 Surface appearance，不改变 wall geometry、Door、Window、Grid Occupancy 或 Navigation。所有修改使用 preview transaction，确认后才写入 Layout data。

### Risks / Likely Bugs

- 修改一面墙时意外改变整个 room。
- Wallpaper pattern 在转角、重复区间或门窗周围断裂和拉伸。
- Wall Decoration 在更换墙面后丢失或漂移。
- Camera 无法看清背面墙，导致选错 Surface。
- Surface ID 因 Scene rebuild 改变，未来 Save 无法恢复。
- Floor Surface 被错误当作实体 furniture，占用 Grid 或阻挡 NavMesh。

### Tests

- 单个 wall segment、连续 surface 和 floor region selection boundary。
- Paint、Wallpaper、Wainscoting 和 Floor Surface preview / confirm / cancel。
- Cancel 不修改正式 Layout。
- Door / window openings 保持位置与可通行性。
- Window 与 Wall Decoration attachment、move、remove、Slot overlap rejection 和 surface-change persistence。
- Stable Surface ID 在 Scene rebuild 后保持一致。
- Surface appearance 不改变 Grid Occupancy 或 NavMesh。
- Camera fade / hide mode 下仍选择正确 Surface。
- 多种 resolution 下 UI 可读且不穿透 Scene。
- 人工 Play Mode 检查 pattern seam、corner、opening 和 Camera readability。

### Not Included

- 建造、移动或删除墙。
- 改变 Room boundary。
- 移动真实 Door / Window openings；P4 wall-mounted Window 的移动属于本 Phase。
- Atmosphere 数值和完整 Theme bonus。

### Future Consumer

Store Expansion 稳定后，高级结构编辑可复用 Surface ID、attachment 和 validation rules，另行设计建墙、拆墙、Room detection、NavMesh rebuild 与冲突修复。

---

## Phase 8 — Functional Furniture & Layout Readiness

### Status

`Completed — merged to main`（2026-09-21）。Studio Owner 已确认朋友 review 完成，并授权 PR #7 merge、Phase 8 完成、本地 fast-forward 与归档后清理；以上步骤均已完成。本次合并包含已批准并实现的 P8R UI / input / feedback enhancements；merge commit 为 `8ee0026247c2c3200bd1bc9ac471b7007e4a4f32`。Merged-main fresh 回归覆盖 2110 个不同 EditMode 用例（1755 full-suite PASS + 355 独立补测 PASS），PlayMode 1015 PASS / 0 FAIL / 2 opt-in screenshot SKIP；所有 inconclusive 为 0。完整报告、Owner 决定及归档位置统一见 `Docs/Phase8_Beginner_Guide.md` 第 37 节。

Current Next Step（2026-09-28）：独立 Phase 8R 优化已通过 PR #8 合并并完成工程收尾，下一步可进入 Phase 9 Order Domain 的 design / test cases / implementation plan 准备。Phase 9 实施仍须 Owner 批准；最新证据与验收边界见 `Phase8R_PR8_Review_2026-09-28.md` 的收尾记录。

#### 历史 gate 快照（2026-09-09）

以下旧的 In Progress、Current Next Step 和 manual Pending 均描述当时状态；不覆盖上方 2026-09-21 收尾决定，也不补造未执行的逐项验收结果。

`In Progress`（2026-09-09）— 五项 bug 修复与前三项 UX 保留，后续批准的 UX2 选项 2–5 与半透明光感 footprint 已实现并技术核验，交回 **Ready for manual review**。完整 Editor PlayMode **813/813**（707 core + 106 Scene/Input）、全部 Phase 8 EditMode + SurfaceSession **235/235 PASS**，failed/skipped/inconclusive 均 0、exit 0；focused asset/font 8/8 已含在 235。独立 code 与 7 张视觉技术复核无未处理 Critical/Important；新增 7 项 Owner manual 全部 **Pending**，最终 XML 与步骤见 `Docs/Phase8_Beginner_Guide.md` 8.5。选项 1 保留 icon，Exit 不动。M1–M17 历史 Owner PASS、M18 technical PASS 保留；M6 incompatible Slot 子项仍未覆盖，不自动标为 Phase Completed。

下表保留 UX2 前的历史 gate 证据；UX2 使用上段与 Guide 8.5 的 fresh evidence。本轮未执行 full EditMode、standalone Player 或 Android/iOS 真机；窄屏为 320 logical pixels runtime Prefab fixture。Footprint/font authoring 完成后的 1,393 个非 C# Assets + ProjectSettings 在 full regression 前后 hash 一致、changed/missing/unexpected 为 0；本轮仍有已批准的 shader/material、两份 footprint Prefab 与 P8 static font 修改，没有 Scene/ProjectSettings 改动。人工结果独立记录。

| Gate | Status |
|---|---|
| Automated | **2026-09-09 修复验证 PASS**：完整 PlayMode `781/781`（core679 + Scene102），直接 EditMode `83/83`，原 Touch control `26/26`。未重跑 full EditMode / Player。2026-09-04 full EditMode `1,689/1,689`、Windows Player `619/619` 仍是历史 baseline，不替代本轮验证；XML 见 Guide 第6节。 |
| Engineering | **PASS** final independent department decision。 |
| QA | **PASS** final independent department decision；P8-M-018 guide 可观察性 Minor 已修正。 |
| Production | **PASS** final focused re-review；`0 Critical / 0 Important / 0 Minor`。 |
| Studio Owner Manual / 授权代测 | **17 项 Owner manual PASS + 1 项 Codex technical PASS / 0 FAIL**：M1–M17 已由用户确认；M18 代测不计为 Owner 人工执行。M6 incompatible 子项仍未覆盖。 |

2026-09-08 历史 M6 / 玩家提示修复：EditMode75/75、Core374/374、直接Scene15/15 PASS，已获 Owner 复测接受；当时旧 Touch 顺序问题仍开放。2026-09-09 已确认是前序 Scene fixture 的 InputAction runtime cache 未清理，并以无诊断原始26-case control及完整PlayMode验证修复。最新独立 code review 无未处理 Critical / Important；新修复的手感尚待 targeted manual review。

Current Next Step：按 Guide 8.5 逐项人工复测 Catalogue 记忆/继续添加、readiness 摘要/详情、CR/CM 最近屏幕中心的合法 Slot、Floor 范围/真实格数及四路径半透明 footprint，7 项当前均 Pending。Pick-up 起点不变，Whole Room Undo 仍禁用，readiness 发布合同不变；白色取餐 icon/模型与入口蓝区保留。不自动批准 Phase 8R 或扩展系统。

2026-09-09 红绿灯式 footprint 配色历史见 Guide **8.7**：brightness 6 / saturation 3 / light intensity 1.5，opacity 0.45 / softness 0.12 / bounds / depth 沿用。该轮 focused 15/15、PlayMode 84/84 保留为历史；最新 Preview authority 为 **8.8**：普通家具/CR/CM 保留原材质，墙饰仅 Preview 向室内额外悬浮 20 cm，Confirm 回原贴墙位置、底高与真实占格不变。直接 PlayMode **301/301 PASS**，failed/skipped/inconclusive 0、exit 0；三张近景独立技术复核无阻挡，**Ready for manual review**。8 项受保护文件 hash 不变，既有 dirty Invalid material 原样保留。Owner 四项新增 manual 与视觉偏好 Pending；本轮未执行 full Phase / EditMode / Player / 手机验证。

### Goal

让经营系统通过 furniture capabilities 和 interaction anchors 使用布局，而不是依赖固定 Scene object names。

### Scope

Phase 8 只提供 functional placement、automatic anchors 和 layout-readiness contracts。它不加入 cafe day loop、NPC movement、Order、Queue、NavMesh / pathfinding agents、economy 或 Save。

- Furniture capability definitions。
- Cash Register、Coffee Machine 和 Pick-up Surface。
- Employee / customer interaction anchors。
- Pick-up Surface Slot。
- Anchor rotation 与 validity。
- Layout readiness report。
- 缺少必要功能或路径准备条件时禁止营业。

### Accepted / Deferred Minor

九项 accepted/deferred Minor 集中记录在 `.superpowers/sdd/2026-09-02-phase-8-functional-furniture-layout-readiness/task-10-report.md`。它们是已知 test / diagnostic / polish limitations，不是隐藏 blocker，也不会自动成为 Phase 8R scope。

### Why Before Cafe Loop

Order 和 NPC systems 从一开始就使用可移动 functional furniture contract，避免之后实现装修时重写固定 stations。

### Main Difficulty 与 Solution

Anchors 使用 furniture-local coordinates。家具位置或旋转变化后重新计算 world position，并重新验证 occupancy 与 navigation sampling。

### Risks / Likely Bugs

- 家具旋转后 anchor 没有旋转。
- Anchor 落在家具、墙体或 locked cell 内。
- 旧 anchor 在家具移动后仍被 NPC 使用。
- 错误提示只说“布局无效”，没有指出原因。

### Tests

- 四个 rotation 下的 anchor coordinates。
- Blocked anchor 被标记 invalid。
- 缺少 Cash Register、Coffee Machine 或 Pick-up 时不能营业。
- Furniture move 后旧 anchors invalidated。
- Readiness report 包含具体 furniture 和 failure reason。
- 人工检查 Scene gizmos / feedback 与 data 一致。

### Milestone Gate

玩家能装修基础空间；功能家具由通用 capability 和 anchors 描述；还没有 Customer 经营。

---

## Phase 8R — Existing Feature Enhancement（现有功能优化）

### Status

`Merged — engineering closeout complete; ready for Phase 9 design`（2026-09-28）。用户逐项确认的现有功能优化已交付；PR #8 合入 main，merge commit `0417d627a3e355206fb4a6310c41690d6a9fdec2`。Owner 已授权 full review、merge、本地 fast-forward 与本地 branch 清理；这些步骤均已完成。此状态不补造逐项人工或实体手机 PASS，也不代表 Phase 9 implementation 已批准。

Phase 8 继续保持 `Completed`。此前随 PR #7 交付的 `P8R` UI / input / feedback enhancements 属于已完成成果；本节描述的是其后的独立优化阶段，不重命名已有代码、assets 或历史测试编号。

### Goal

在 Phase 9 前，继续优化 Phase 1–8 已有功能的使用体验与稳定性，以经过确认的优化项、实际改进和回归验证作为交付结果，为进入 `Milestone B — Core Cafe Loop` 建立稳定基线。

### Confirmed Scope

- 仅优化、修复或打磨 Phase 1–8 已存在的功能与行为，不加入任何新的 system 或 feature，也不以“小功能增强”的名义扩展范围。
- Review 用于识别已有功能的实际问题，并形成有优先级的优化清单；每项需明确当前问题、预期改善和验收方法，由 Owner 确认后实施。
- 保持已完成 Phase 的状态和既有功能边界；若某项优化需要调整已确认的玩家规则或接口合同，先说明影响、返工或迁移需求、风险及回归范围，再取得单独批准。

### Enhancement Areas

已交付：双按钮时间控制和1x/2x/Pause/Decor状态标识、Normal summary与Decor checklist、原样式透明panel、occupied Store提示、CR/CM分类帮助、目录tabs/fold和category/footer统一spacing、家具与功能设施拖动边界，以及直接相关Editor保存隔离和输入修正。逐次批准的规则见 `Phase8R_UI_Enhancement_Design.md`；历史Minor backlog不自动扩大本轮范围。

### Tests 与 Manual Acceptance

最终回归：EditMode全量加隔离补跑覆盖2123个不同用例全部PASS；PlayMode 1036 PASS、0 FAIL、4项opt-in SKIP；另行启用原生截图1/1 PASS，并检查1080×1920关键状态。合并树与已验证分支树完全相同，本轮只有收尾文档变化，不重复Unity全量测试。Owner交互反馈和收尾授权保留，不将其转换为未记录的整套manual checklist；实体Android/iOS、极短横屏和中文字体视觉限制仍保留，详见收尾记录。

### Not Included

- 任何新 system 或 feature。
- Phase 9 及后续 Core Cafe Loop 内容。
- Customer、Employee、Order、Queue 或正式 Navigation gameplay。
- 未经单独批准的视觉重设计或未来高级 Decoration 功能。

### Milestone Gate

工程与集成基线可用于Phase 9 design准备，无已确认的Critical/Important产品阻塞。正式实施前仍需完成并批准Phase 9 design、test cases和implementation plan；视觉/设备验收边界继续明确保留，不以自动化结果替代Owner人工验收。Phase 9的Order Domain定位与后续阶段顺序不变。

---

# Milestone B — Core Cafe Loop

## Phase 9 — Order Domain

### Goal

建立 Customer、Cashier 和 Barista 共用的唯一 Order state authority。

### Scope

- Unique Order ID
- Order state machine
- FIFO waiting queue
- Create、claim、transition、complete 和 fail
- Duplicate-operation protection

### Why Now

Order 是完整 Cafe Loop 的业务核心，先以纯逻辑验证，不混入 NPC 或 NavMesh。

### Main Difficulty 与 Solution

所有 state changes 只能通过 `OrderService`，不允许不同 controllers 直接修改 Order state。

### Risks / Likely Bugs

- Duplicate claim / completion。
- Illegal state transitions。
- FIFO 顺序错误。
- Failed Order 重新进入 queue。

### Tests

- IDs 唯一递增。
- FIFO claim。
- 同一 Order 不能 claim 或 complete 两次。
- Invalid transition 不改变 state。
- Completed 和 Failed 是 terminal states。
- Order list 和 waiting queue 保持一致。

---

## Phase 10 — Capacity & Reservation

Status（2026-09-29）：`In Progress`。Owner 已批准 design、automatic/manual test cases 与 implementation plan，并要求在现有 `codex/phase-9-order-domain` 分支继续开发，可使用 sub-agents。基线 `94cc48d`；不等待 main merge，不替 P9 自动 closeout。详见 `Docs/superpowers/plans/2026-09-29-phase-10-capacity-reservation.md`。

Implementation / verification（2026-09-29）：容量domain与临时Debug窗口已实现；最终Engineering及QA整体review均PASS，无开放Critical/Important/Minor。完整EditMode原始2008 PASS / 355 SKIP，经精确对应的独立补跑后有效2363 PASS；完整PlayMode1042 PASS / 4 opt-in SKIP / 0 FAIL。M-001…010通过（Codex获授权代跑，M-009拖动由Owner实际补验）；Owner接受委托验收并授权review后commit/push到同一P9分支。开发和验收完成，main merge与正式closeout未执行，故Status仍In Progress。证据与操作见 `Docs/Phase10_Beginner_Guide.md` 和原P10 test cases。

### Goal

统一管理 active customers、Counter Queue 和 Pick-up 的容量及预留。

### Scope

- Total customer capacity
- Counter Queue capacity
- Pick-up capacity
- Reserve / occupy / release
- Atomic capacity transactions

Approved rules（2026-09-29）：Total=`floor(已铺室内Floor格数/4)`，Pick-up=Total，Counter=`ceil(Total*50%)`；家具不扣格。入店前原子预留三项各1，离开对应位置/店铺后分别释放；正确取走商品即 Completed，不自动释放容量。面积下降保留已有顾客和预约，允许暂时超额、暂停新申请，正常释放后恢复。

### Why Separate From Orders

Capacity bugs 会造成负数、超员、reservation leak 和系统死锁，必须独立测试。

### Main Difficulty 与 Solution

Service 返回明确 reservation token / ownership，只有 owner 可以完成或释放对应 reservation。

### Risks / Likely Bugs

- 新 Reserve 造成或扩大超额，或计数低于 zero；合法缩容导致的暂时 Used > Limit 必须保留旧名额，不能误当损坏清除。
- Reserve 失败却部分修改状态。
- Repeated release。
- Customer 离开后 reservation 未释放。

### Tests

- 每种 capacity 的 upper / lower bounds。
- Failed reserve 不改变任何 count。
- Repeated release idempotent。
- Pick-up 满时拒绝新 reservation。
- Release 后可以再次 reserve。
- Reservation ownership 正确。
- 面积公式、Floor来源去重/bounds、缩容保留旧预约、已有Reserved仍可Occupy、三项有余量后恢复。
- 旧token重复Release不得影响后来取得的新名额；Order Completed与实际离位/离店分开。

---

## Phase 11 — Navigation & Movement Recovery

**状态（2026-10-04）：开发与验收完成，M-001～M-011 全部 Owner PASS；Owner 已授权 PR #9 合并与本地收尾。** Owner 已确认新版 walk、避让、时间控制、recovery、装修碰撞与堵路修复、MainCafe 回归，并补充“11也完成了”关闭柜台站位与转向验收。最新 review、完整回归、已知限制见 [合并前 Review](Phase9_Phase11_Merge_Review.md)；合并结果以 [PR #9](https://github.com/zhengparker/AnimalCafe/pull/9) 为准。P12 未开始。

此前自动证据：原版完整 PlayMode 1102 PASS / 0 FAIL / 5 opt-in SKIP，指定 Integration 27 PASS / 0 FAIL / 1 SKIP；完整 Edit 原始 2432 PASS / 1 FAIL，唯一失败由批准的精确补测解决。后续 walk 1.5 倍 focused Edit 8 / Play 8 PASS；crowd8 / detour 修复 focused Edit 14 PASS、直接 Play 66 PASS / 0 FAIL / 1 SKIP；红色 footprint / teardown focused Play 14 PASS / 0 FAIL / 0 SKIP。各组覆盖重叠，保留为历史，不合计为最新完整回归；最新结果以合并前 Review 为准。

- 独立验证场景：`Assets/Scenes/Validation/Phase11Navigation.unity`，8 个原尺寸角色、真实 P8 装修与恢复营业 gate。
- MainCafe 仅接入 passive Navigation，零营业 NPC，保留既有装修/时间行为；正式 Customer / Employee 接入时须显式开启 business readiness。
- 操作与验收：[Beginner Guide](Phase11_Beginner_Guide.md)；准确自动结果、SKIP 和实现决策：[验证报告](Phase11_Validation_Report.md)。
- 本阶段不接入 P9 Order、P10 Capacity、Customer queue、Employee tasks、经济、Save、多楼层或新动画。

### Goal

提供 Customer 和 Employee 共用的可靠 NavMesh movement。

### Scope

- Destination sampling
- Path validation
- Arrival detection
- Scaled movement
- Timeout、retry、recovery point
- Safe cleanup callback
- Furniture layout change invalidation

### Why Before Characters

Customer 和 Employee state machines 只需要处理 movement result，不重复实现 NavMesh failure logic。

### Main Difficulty 与 Solution

移动前验证 destination 和 complete path；第一次 timeout 重新计算一次；第二次进入 configured recovery；无法 recovery 时返回明确 failure。

### Risks / Likely Bugs

- Agent 不在 NavMesh。
- Destination unreachable。
- Pause 时 timeout 继续累计。
- Layout change 后沿旧 path 穿过家具。
- Agent 永久卡住或不断 retry。

### Tests

- Valid path arrival。
- Invalid destination immediate failure。
- Pause 不推进 timeout。
- `2x` 正确影响 movement。
- 只允许一次 retry。
- Recovery 成功和失败路径。
- Layout changed 时 current path invalidated。
- Cleanup callback 只调用一次。

---

## Phase 12 — Customer Spawn & Counter Queue

状态：`In Progress — Direct Regression PASS / Owner Follow-up Pending`（2026-10-09）。到期保留一位与安全同向跟随已实现；Owner追加批准并完成20fps原生路径提交恢复。仅在完整路径被Agent拒后重验真实起点与World碰撞保护，原地重新绑定并重试一次；无瞬移、身体/skin/容差/速度变化。受影响直接Play112 PASS/0 FAIL/0 SKIP，P11/P12 Edit126 PASS/0 FAIL/0 SKIP；完整30游戏分钟循环与真实场景20fps通过。此前109 PASS/1 FAIL为修复前证据，M1～M8的8/8 Owner PASS与桌角Owner PASS保留为历史快照。本轮只补默认六到八位连续进客与队首离开后的前移视觉观察，不重做整套。Decor reset、容量/FIFO保持，Phase最终验收待收尾；全项目完整suite/Player/真机本轮NOT_RUN。工作区 `.worktrees/phase-12-customer-queue`；无新工具/资源迁移/commit/push/PR/merge，不进入P13/P14。见 [P12 Validation Report](Phase12_Validation_Report.md)。

P10交接（2026-09-29）：使用 random interval 安排顾客到店，容量恢复不批量补齐或补发积压顾客；interval 分布与恢复计时策略在本Phase设计。接入P10三项入店预留，验证真实站位/可达性及离位/离店事件；不要以逻辑额度通过替代队伍空间验证。

### Goal

独立完成 Customer 的进入、排队、前移和安全离场。

### Scope

- Customer spawn interval
- Entrance / exit
- Counter Queue slots
- Queue order 和 forward movement
- Total / queue capacity integration
- Spawn pause / resume

### Why Before Employees

先验证 Customer lifecycle 与 physical queue，不同时加入收银、制作和 Order ownership。

### Main Difficulty 与 Solution

Queue order 由一个 queue authority 管理；Customer 不自行决定 slot。Slot change 通过明确 assignment 推进。

### Risks / Likely Bugs

- Queue 顺序交换。
- 前方离开后后方不前移。
- Spawn failure 留下 ghost capacity。
- 同时生成超过上限。

### Tests

- Customer 总数和 Queue 不超过 capacity。
- Queue 保持 arrival order。
- Front slot 释放后正确前移。
- 满容量暂停 spawn，释放后恢复。
- Invalid spawn location 不生成半成品 Customer。
- Failed / removed Customer 正确释放 capacity。

---

## Phase 13 — Employee Task Flow

**状态（2026-10-14）：本地开发及16项manual验收完成。Owner手测M-001～004、M-006、M-009、M-016 PASS；10项委托场景代测PASS（M-006重叠）。42cm台面已实施：最终完整PlayMode1267 PASS / 0 FAIL / 5原有opt-in SKIP；完整EditMode2599 PASS（微小位移修复前），最终P11 Edit增量77 PASS。最终独立Engineering及QA/技术美术review通过，无开放Critical/Important；证据边界见Validation Report。**

开发位于 `E:/Unity/Project/AnimalCafe/.worktrees/phase-13-employee-task-flow`，branch `codex/phase-13-employee-task-flow`，基线为本地已确认的 P12 merge commit `302f3e9`。本阶段 focused 验证、完整 suite、独立审查与人工验收分别记录在 [P13 Validation Report](Phase13_Validation_Report.md)；入口和16项 manual 见 [P13 Beginner Guide](Phase13_Beginner_Guide.md)。本地交付不代表已合入main；版本管理收尾与P14启动仍由Owner另行决定。

### Goal

让固定 Cashier 和 Barista 可靠执行抽象工作任务。

### Scope

- Employee state machine
- Station capability lookup
- Cashier service task
- Barista preparation / delivery task
- Move、work、return idle
- Scaled work duration
- Task cancellation 和 recovery

### Why Before Integration

Employee workflow 先用 controlled tasks 验证，Phase 14 再连接真实 Customer 和 Order。

### Main Difficulty 与 Solution

Employee 一次拥有一个 task handle；task 负责声明 station capability、target anchors、duration 和 cleanup。

### Risks / Likely Bugs

- 同一 Employee 同时领取多个 tasks。
- Station 消失后 task 永久等待。
- Pause 时 work timer 继续。
- Failure 后 Employee 无法回到 Idle。

### Tests

- 一次只执行一个 task。
- 找到正确 capability station。
- Missing station 明确失败。
- Pause / `1x` / `2x` work timing。
- Task cancellation 只 cleanup 一次。
- Failure 后 Employee 可返回 Idle。

---

## Phase 14 — Integrated Takeout Cafe Loop

**Git／证据更新（2026-10-26）：** 当前本地 main 为 PR #14 merge commit `f55d4eb685f32e4dcc30fb64b753b45b695a6a3b`。本地 closeout receipt 记录该源 merged-main focused Edit153／Play33，0F/0S，且 `phaseCompleted=NOT_CHANGED`；见 `outputs/p14-closeout-20261026/final-verification.json` 与 `merged-main-regression.json`。本轮没有重跑测试，也不据此补填 Owner manual、全套 regression、device 或正式 Completed。以下保留实现时历史，旧 worktree／未 merge 描述不作为当前 Git 状态。

**历史状态（2026-10-23）：自然单列、P14一秒收银已实现，最终同源直接Edit267／Play171全部PASS，0F/0S。** 制作3秒、来客1～5秒、旧P13默认2秒保持。19原家具／rapid变体按真实Paid请求2／4／6人，短队明确CAPACITY_LIMITED且无slot不收费；前移／原token排空与2个controlled恢复case通过，独立QA已关闭自动化问题。普通Editor与清理结果见Validation Report；Owner只补新队形／动作观感，旧三项visual PASS保持。旧256／164保留2秒历史，Phase当时尚未Completed／未跨Phase。

批准文件：[Design](superpowers/specs/2026-10-19-phase-14-integrated-takeout-loop-design.md)、[Test Cases](superpowers/specs/2026-10-19-phase-14-integrated-takeout-loop-test-cases.md)、[Implementation Plan](superpowers/plans/2026-10-19-phase-14-integrated-takeout-loop.md)。实际结果见 [Validation Report](Phase14_Validation_Report.md)，手测入口见 [Beginner Guide](Phase14_Beginner_Guide.md)。历史工作区为`E:/Unity/Project/AnimalCafe/.worktrees/phase-14-integrated-takeout-loop`，branch为`codex/phase-14-integrated-takeout-loop`；当时尚未commit／push／merge，无自动CI授权。当前 merge／清理事实以上方 2026-10-26 receipt 为准。

### Goal

整合第一个完整、自动运行的外带经营循环。

### Scope

```text
Customer spawn
→ Counter Queue
→ Cashier payment
→ Order creation
→ Barista FIFO preparation
→ Pick-up delivery
→ Correct Customer collection
→ Customer exit
```

- 两名固定岗位员工：Cashier 和 Barista。
- 一种基础 Coffee，不使用 Inventory。
- Read-only status UI。
- 营业中 functional furniture protection。

### Why After Phases 5–9

Order、Capacity、Movement、Customer 和 Employee 已分别通过 tests，integration failures 可以缩小到 system boundaries。

### Main Difficulty 与 Solution

使用 stable Customer ID、Order ID、reservation ownership 和 task handles 连接各系统，不以 Scene object equality 作为长期身份。

### Risks / Likely Bugs

- Customer 领取错误 Order。
- Pick-up reservation leak。
- Duplicate payment / delivery / collection。
- Employee failure 阻塞全部 Queue。
- 营业中移动必要家具导致 runtime references 失效。

### Tests

- 至少连续完成 20 个 automated Orders。
- FIFO 与 Order ownership。
- Duplicate prevention。
- Capacity full → pause → recovery。
- Movement / task failure cleanup。
- Pause、`1x`、`2x` 控制完整循环。
- 营业中必要 furniture 不允许直接移动，或必须先打烊。
- Status UI 与 runtime data 一致。
- Console 无未处理 error。

### Milestone Gate

完成 **Core Cafe Loop Prototype**：咖啡厅可以在有效玩家布局中自动完成连续外带服务。

---

# Milestone C — Playable Management MVP

## Phase 15 — Business Day Lifecycle

最新展示修正（2026-10-27）：Main使用既有P5玩家状态卡，Validation保留黑色详细诊断；Edit59 PASS、定向Play122 PASS／1原有SKIP／0FAIL，独立review与两Scene横竖屏UI检查通过。UI后3172Assets零差异；原七项manual和旧完整suite保持源边界，正式closure仍No。见[Validation Report第17节](Phase15_Validation_Report.md#17-main-玩家界面与-validation-诊断分离2026-10-27)。

后续执行（2026-10-27）：HUD遮挡修复及定向Play122 PASS／0 FAIL／1原有SKIP、独立review通过。原批准M01–M04 Validation本轮已补测PASS，结合此前M05出口／M06／M07正常UI，原七项均有Agent通过记录；扩展Guide的Main完整装修、直接付款集合仍有缺口，Debugger仍UNKNOWN。本轮Assets3172零变化、无代码改动或自动suite重跑。旧完整Edit2832／Play1445＋5 SKIP属HUD修复前source；Owner观感／8h／device／CI／VCS未验收，正式closure／merge readiness为No，不进入P16。见[Validation Report第16节](Phase15_Validation_Report.md#16-m01m04-validation-补测与范围澄清)。

Execution（2026-10-27）：Tasks1–6、HUD修复及本地验证已实现；Main28800／7200秒、独立Scene60／15标Validation。原批准七项均有Agent manual通过记录，本轮只补M01–M04，不伪称全部重测。Guide扩展Main／完整付款集合仍缺，DebuggerUNKNOWN，Owner观感／device／CI／版本管理待处理，Phase未正式Completed。见[Agent报告](Phase15_Agent_Playtest_Report.md)、[Guide4.1](Phase15_Beginner_Guide.md#41-原批准范围与补充检查)。

### Goal

建立自动 Open、Closing、Closed / Rest 与下一营业周期的明确边界；报告是可查看的记录，不是必须确认的生命周期状态。

### Scope

- 有效店面自动开始经营，不要求Start；P15是运行中foundation，重新Play重建Day1。已有Save恢复周期属于P17／P18A交接目标，当前未实现。
- Business clock；明确现实时间、周期时间与现有 Pause／2x 的关系。
- Automatic close：停新客，已接待顾客继续正常服务与离店。
- End-of-day drain / cleanup：保留订单与付款事实，不直接调用装修清场。
- Closed / Rest：可查看、装修和安排经营，休息结束自动开始下一周期。
- Day transition 去重，不等待报告阅读或 Next Day 点击。

Owner 已确认正式默认：现实营业28800秒，真实Closing排空后再休息7200秒；Pause／2x／装修暂停不改变周期。真实Blocked保持Closing，修复并排空后再Rest，不跳过旧单。真实装修修改保留P14清场但记录LayoutInterrupted，不当正常服务完成。Early close 不作为本阶段入口。完整 Offline／Economy／Save 不因本节更新自动进入 P15 scope。

### Why Before Economy

收入和统计必须明确属于哪个营业日，Save 也需要稳定 day boundary。

### Risks / Likely Bugs

- Closed / Rest 时仍接待新 Customer。
- 打烊误删 active Orders、reservation 或历史付款事实。
- 周期时间与已批准的 Pause／2x／后台政策不一致。
- Day number 增加两次。
- 报告未读或玩家不在线导致永远无法开下一天。

### Tests

- Closed / Rest 不接待新客；Closing 继续现有服务，休息阶段仍允许管理 UI。
- 自动 open / close / rest / next-cycle transitions，无 Start／Report／Next Day 点击 gate。
- Pause／2x／装修仅影响Scene，周期保持独立现实经过时间；Rest不依赖点击或解除Pause。
- Closing drain／日结边界只提交一次，不使用装修清场取消已接待订单。
- 收银提交、Working、Prepared、Ready、已领取离店与真实 Blocked 时的日边界。
- Day number 每次只增加一次，休息时间不产生正常接待收益。

---

## Phase 16 — Economy & Daily Report

### Goal

记录基础 Coins、Product price、Revenue 和每日经营结果。

### Scope

- Coins balance
- Base product price
- Payment transaction
- Daily metrics
- Daily Report

2026-10-26 已确认约束：收益按真实 transaction 自动入账，Daily Report 可稍后查看，不需要领取／确认才继续营业。前台和未来离线摘要共用业务事实，跨日／装修不能清除未归档的统计依据；具体 ledger 与历史保留由 spec 定义。完整离线收益实现不因本条自动加入 P16。

### Why After Day Lifecycle

Daily Report 依赖可靠的 day start / end，不能使用不明确的统计清零时机。

### Risks / Likely Bugs

- Order 重复收费。
- Failed Order 错误计入完成收入。
- Report 与实际 transactions 不一致。
- UI refresh 改变 Coins。

### Tests

- 每个 Order 只支付一次。
- Transaction sum 等于 Coins change。
- Report Order count / Revenue 准确。
- 下一天 metrics 正确 reset。
- UI 是 read-only projection。
- Failed / recovered Order 遵守已批准 payment rule。

---

## Phase 17 — Save Foundation

### Goal

可靠保存 Layout、day、coins 和后续系统可扩展的 versioned game state。

### Scope

- Save schema 与 `saveVersion`
- Stable IDs
- Atomic write
- Backup 与 recovery
- Load validation
- Layout、day、coins 和当前必要状态

2026-10-26 设计要求：Save 需支持恢复当前自动营业／休息周期，不能重启就重新开一整天。离开／返回时间、最后已结算位置与防重复所需的最小数据，应在 spec 中明确。本阶段为 P18A 提供真实持久化边界；不在 P17 同时实现完整 Offline engine。

### Why Now

从此处开始持久状态快速增加。更早设计会缺少真实需求，更晚加入会迫使多个系统同时重构。

### Main Difficulty 与 Solution

只保存纯数据和 stable IDs；先写 temporary file，验证成功后再替换正式 Save；保留最近有效 backup。

### Risks / Likely Bugs

- 保存 Scene object references。
- 写入中断破坏唯一 Save。
- Unknown Definition ID 导致家具静默消失。
- Load 重复生成 instances。

### Tests

- Save / Load round trip。
- Corrupted newest Save 恢复 backup。
- Write failure 不覆盖旧 Save。
- Unknown ID 显示明确错误。
- Repeated Load 不重复生成 data。
- Save version mismatch 走明确 migration / rejection path。

---

## Phase 18 — Application Shell & Loading

### Goal

建立从启动游戏到安全进入 Cafe 的完整 application flow。

### Scope

- Boot scene / initialization state
- Splash / logo presentation
- 默认恢复有效 Save 并自动进入 Cafe，不要求每次点击 Continue。
- Settings 与必要的首次创建／存档恢复入口。
- Title Screen、手动 Continue 与 Return to Title 可作为可选导航，不作为正常回访必经步骤。
- Loading screen 与 progress / status feedback
- Save selection、load failure 和 recovery UI
- Scene transition 和 duplicate-load protection
- First-launch flow

### Why After Save Foundation

自动恢复、首次创建与 recovery 必须连接真实 versioned Save。更早实现只能产生无法验证的假流程。

### Main Difficulty 与 Solution

使用明确 Boot state machine，初始化 services、读取 Save、加载 Scene 和进入 gameplay 各有单一 ownership；重复操作返回同一个 active transition。

### Risks / Likely Bugs

- 重复点击造成 Scene 加载两次。
- Load 失败后停留在不可恢复黑屏。
- Return to Title 保留旧 runtime objects。
- Loading UI 完成但 gameplay 尚未初始化。

### Tests

- 自动恢复有效 Save，以及首次启动／No-Save states。
- Corrupted / incompatible Save recovery。
- Duplicate-load protection。
- Loading failure recovery。
- Scene initialization order。
- 如保留 Return to Title，验证 cleanup；不强制增加此入口。
- First-launch persistence。
- Manual launch-to-game smoke test。

### Not Included

Online login、Cloud Save、正式 VFX 和 mobile platform lifecycle。

---

## Phase 18A — Minimal Offline Foundation

### Goal

让基础咖啡厅第一次完成真实「离开→返回→看到自动经营成果」，验证挂机承诺；例如退出一段时间，再打开后只入账一次，并正确恢复营业或休息状态。

### Scope

- 使用 P15 自动日循环、P16 基础 Coffee Economy、P17 versioned Save 与 P18 自动恢复入口。
- Leave／return timestamps、已批准的最小 Offline cap、时钟回退／异常跳跃与结算去重。
- 基础 Coffee 的纯数据 summary；不在后台逐帧重放所有 NPC／NavMesh／动画。
- 明确离开时在途订单、真实 Blocked、当前有效布局与服务能力的处理，不能把所有旧订单直接改成 Completed。
- 前台与 summary 的结算区间互斥：已付款／消耗／交付事实不重放，结果、Coins、周期与最后结算位置一致提交。保存中断或进程被终止后仍可恢复且不重复收益。
- 此 slice 暂无正式 Inventory，仅使用 spec 明示的受控基础商品／服务能力假设，不代表无限原料、最终收益平衡或永远有效的路径／员工；P20A 必须重新验证真实资源约束。
- 正确扣除休息，跨多个已批准周期，恢复实际当前状态。
- 简短非阻塞回访摘要，收益自动入账，重复打开报告不结算第二次。
- 一个受控目标移动平台的 suspend／resume／终止后恢复 feasibility；设备与可执行范围在本阶段 spec 确认。未执行时记 `NOT_RUN`，不得用 Editor 结果替代。

### Dependencies / Not Included

依赖 P15–P18 的可验证实际成果，不只依赖文档。暂不处理尚未加入的正式 Menu／Inventory、多商品、多员工、Traits、重大故事、扩展产品或完整双平台适配；这些功能出现时分别扩展，不提供绕过未来库存的隐藏收益路径。

### Tests / Gate

- 同一离开区间重复启动、恢复或读报告，只结算一次；Save 中断恢复不重复入账。
- 相同输入产生确定结果；时间回退、异常大跳跃和 cap 边界有明确处理。
- 跨打烊／休息／开店，休息不产生正常接待收益，不能以重启跳过休息。
- 活跃订单、Blocked 与无效布局符合批准政策；没有虚构完成或错误资源／容量能力。
- Owner 试玩离开再返回，能看到真实成果并理解原因。运行／Editor／device evidence 分列，具体 gate 由本阶段 spec 固定。

---

## Phase 19 — Recipe & Menu

### Goal

定义 Products、Recipes、required equipment 和当前可售 Menu。

### Scope

- Product Definition
- Recipe Definition
- Equipment requirements
- Menu enable / disable
- Product availability query

### Why Before Inventory

Inventory 必须依据 Recipe 检查和预留材料；先定义商品需求，再管理材料。

### Risks / Likely Bugs

- Recipe references missing Product / Ingredient。
- Disabled Product 仍被 Customer 选择。
- Equipment 忙碌被误判为设备不存在。
- Menu 出现 duplicate Product。

### Tests

- Definition reference validation。
- Missing required equipment 时 unavailable。
- Busy equipment 仍允许 Order 进入 FIFO。
- Disabled Product 不可点单。
- Menu 无 duplicate entries。
- Availability result 包含明确 reason。

---

## Phase 20 — Inventory, Reservation & Restocking

### Goal

让 Order、Recipe 和 Inventory 使用一致、可恢复的材料 transaction。

### Scope

- Ingredient inventory
- Available / reserved / consumed
- Payment-time reservation
- Preparation-time consumption
- Recoverable failure handling
- Basic restocking
- Minimum-income basic supply

2026-10-26 已确认约束：基础经营不能要求玩家频繁回来补货。保留真实库存与 reservation，不制造免费补齐所有商品的假库存；最小供应、缺货后可售菜单和补货策略如何共同维持可恢复经营，仍由本 Phase spec 决定。自动补货价格／频率／阈值未定。

### Why After Orders, Save and Recipe

Inventory transaction 依赖稳定 Order states、Recipe requirements 和 persistence。

### Main Difficulty 与 Solution

每个 reservation 绑定 Order ID；所有 reserve、consume 和 release 使用原子 transaction；总量必须满足守恒检查。

### Risks / Likely Bugs

- 多个 Orders 使用同一材料。
- Reserved inventory 永久泄漏。
- Retry 导致重复消耗。
- Save / Load 后 available + reserved 不一致。

### Tests

- Insufficient inventory 阻止付款。
- Multiple Orders 不重复预留。
- Consume 只执行一次。
- Recoverable equipment / path failure 保持 reservation。
- Final cancellation 正确释放。
- Restock 后 Product 恢复销售。
- Save / Load 前后 inventory 总量守恒。
- Minimum supply 不可囤积或直接出售。

### Milestone Gate

完成 **Playable Management MVP**：玩家可以装修基础店面、营业多个 days、管理 Menu 和 Inventory，并可靠 Save / Load。

P20 完成的是带 Menu／Inventory 的经营基础；下一步必须执行 P20A，验证资源约束加入后挂机与成长闭环仍成立。不把 P18A 早期无正式库存的结果当作本阶段 Offline PASS，也不据此追溯改变既有 gate 结果。

---

## Phase 20A — Idle Management Loop Validation

### Goal

验证玩家能放心离开、回来了解变化，并把收益投入一次有意义的改善，形成第一个可试玩挂机经营闭环。

### Scope

- P18A 离线能力接入 P19 Menu／Recipe 与 P20 Inventory／reservation／基础供应，复用同一业务 authority。
- 对比前台与离线的商品可用性、付款、材料消耗、周期与缺货恢复政策。
- 一个最小收益消费出口：优先复用已有家具／设备资产或一个基础改善；具体对象、成本与效果由本阶段 spec 批准。
- 改善后能在经营／回访中观察到可理解的效果；交易、库存或布局变化具备必要的 Save／恢复边界。
- 短回访：看成果→作出一个选择→观察反馈→离开；不要求读完报告、清空待办或领取收益才能继续。
- 在有限手机屏幕／已批准设备上验证核心回访操作；最终完整 P50／P51 验收仍独立。

### Not Included

完整 P26 家具库、P27 Strategy branches／复杂 unlock graph、完整 NPC 故事、多楼层、堂食与特色商品均不提前。最小成长入口不是另一套货币或购买 service；后续 P26／P27 必须复用、扩展已验证的业务。

### Tests / Gate

- 正常、缺货、最低供应、已有 reservation、Blocked 与自动跨日均不绕过真实业务规则。
- 重复回访／恢复／购买不重复入账、扣款、消耗或创建家具／改善。
- 玩家不必高频补货或逐单操作来维持已批准的最低基础运营。
- Owner 能完成「离开→回来看到成果→选择改善→看到影响」的手工试玩，明确记录反馈与未满足目标。
- 完成 **Validated Idle Management MVP** 后再扩大角色与内容量；不以自动化 PASS 代替玩法验收。

---

# Milestone D — Character-driven Vertical Slice

## Phase 21 — Core Character Models & Animation Pipeline

### Goal

建立可扩展的动物角色 Model、Rig、Animation 和 Unity character Prefab pipeline。

### Scope

- Reference animal body proportions
- 第一批 Customer、Cashier 和 Barista Models
- Shared / compatible Rig strategy
- Idle、walk、work 和 carry animations
- Character pivot、Collider 和 NavMeshAgent standards
- Held-item / interaction anchors
- Character Materials 和 Prefabs
- Animation import / controller conventions

### Why Before Character Systems

原规划允许 Phase 9–14 用 Capsule 验证逻辑；实际 P13／P14 已有动物 Model、working／carry 和顾客持杯表现。先复用这些成果并盘点缺口，在 Staff identity 和更多 Species 差异进入正式体验前，证明角色视觉与 animation pipeline 可扩展，不重新制作已完成内容。

### Main Difficulty 与 Solution

不同 Species 体型可能无法直接共享 Rig。先完成一个 reference body 和少量可变比例，再验证 animation reuse；特殊体型后续使用明确 variation strategy。

### Risks / Likely Bugs

- Root motion 与 NavMeshAgent 冲突。
- Collider、Agent radius 与视觉体型不一致。
- Carry anchor 导致杯子漂浮。
- Rig 或 animation import settings 不一致。
- 新 Species 破坏已验证的 animation。

### Tests

- Model scale、pivot 和 forward direction。
- Rig / animation import validation。
- Idle / walk / work / carry state transitions。
- NavMesh movement 与 animation synchronization。
- Collider / Agent bounds。
- Held-item anchor。
- Missing bones、materials 和 clips scan。
- Unity Editor development profiling baseline，并为 Phase 51 Android / iOS device budgets 保留可测量指标。

### Not Included

全部 Species、Breed variants、完整 facial animation 和所有正式 costumes。

---

## Phase 22 — Staff Identity & Work Capabilities

### Goal

加入稳定 Staff identity、多员工与基础工作能力。

### Scope

- Character ID
- Species、Breed、display name、appearance
- Staff capabilities
- Multiple employees
- Task assignment
- Basic staff details

### Why After Stable Cafe Loop

先有可靠 task flow，再加入多人分配，避免同时调试基础任务和调度。

### Risks / Likely Bugs

- 两名 Staff 领取同一个 task。
- Character ID 在 Load 后改变。
- 缺少 capability 的 Staff 接受任务。
- 移除 Staff 破坏 active Order。

### Tests

- Stable ID across Save / Load。
- Task ownership unique。
- Capability filtering。
- Staff add / remove recovery。
- 多 Staff 不破坏 FIFO 和 Inventory。

---

## Phase 23 — Customer Identity & Returning Visits

### Goal

让 Customer 可以成为被玩家记住的 returning character。

### Scope

- Stable Customer ID
- Preferences
- Visit history
- Returning-customer selection
- Customer profile

### Why Before Traits

Traits、Mood 和 Relationships 都需要可靠确认“这是同一个 Customer”。

### Risks / Likely Bugs

- 同一 unique Customer 同时出现两次。
- Visit count 重复增加。
- Returning selection 永远偏向少数角色。
- Preferences 在 Load 后丢失。

### Tests

- Stable ID 与 visit history。
- 同一 unique Customer concurrency protection。
- Returning selection boundaries。
- Preferences persistence。
- Random regular Customer 可升级为 returning Customer。

---

## Phase 24 — Traits, Mood & Relationships

### Goal

加入克制、可读且可保存的角色差异。

### Scope

- Small approved Trait set
- Mood
- Cafe relationship
- Character-to-character relationship
- Hidden Trait discovery
- Bounded behavior modifiers

### Why After Identity

所有变化必须绑定 stable Character ID，且需要真实经营行为作为观察来源。

### Main Difficulty 与 Solution

Species 和 Breed 只提供概率倾向；Traits 使用有上下限的 modifiers，不直接硬编码完整行为结果。

### Risks / Likely Bugs

- Trait modifiers 无限叠加。
- Breed 被写成绝对性格。
- Mood 每帧更新造成快速漂移。
- Relationship 超出范围。

### Tests

- Modifier bounds。
- 相同 Breed 仍可生成不同 Traits。
- Mood / Relationship clamping。
- Hidden Trait discovery conditions。
- Save / Load persistence。
- Character modifiers 不破坏 Capacity、Order 和 Inventory invariants。

---

## Phase 25 — Events & Player Decisions

### Goal

让经营状态和角色关系产生简单、可选择的事件。

### Scope

- Event definitions
- Trigger conditions
- Pending event
- Player choices
- Outcomes
- Small chained events

### Why Last in Character Milestone

Events 会读取 Order、Economy、Character、Mood 和 Relationships，必须建立在稳定 state systems 上。

### Risks / Likely Bugs

- Event 重复触发。
- Choice outcome 应用两次。
- Pending Event 阻塞普通经营。
- Save / Load 后条件重新触发。

### Tests

- Trigger conditions。
- Duplicate prevention。
- Choice applies once。
- Outcome data correctness。
- Pending event persistence。
- Event 不阻塞 cafe loop。

### Milestone Gate

完成 **Character-driven Vertical Slice**：玩家能经营咖啡厅，并逐渐认识具有差异的 Staff 和 returning Customers。一次离开／返回应能看到绑定 stable Character ID 的真实变化；pending story 可保存且不锁住其他经营，重复 resume／选择不重复产生 outcome。P22–P25 随功能接入已存在的 Save／summary，不等 P45 才首次验证角色离线变化。

---

# Milestone E — Cafe Growth 与 Advanced Decoration

> 路线更新（2026-10-26）：P26 先做小型核心资产，P27–P28 属于主路线。P29–P36 的完整高级装修、户外、堂食、Atmosphere 与多楼层作为延期扩展保留；下文描述功能内部依赖，不代表全部必须在 P45 前完成。开放某项扩展时，其安全验证不能省略。

## Phase 26 — Interior Furniture Model Set

### Goal

制作支持成长、扩建、Dine-in 和主题装修的第一批正式室内家具。

### Scope

- Tables、chairs、shelves 和 lights
- Plants 和 decorative furniture
- Basic theme starter set
- Bakery / Merchandise 基础 display furniture
- Source files、Models、Materials、Colliders 和 Prefabs
- 每件新家具都创建对应的 `FurnitureDefinition Asset`，填写 ID、Display Name、Prefab、Footprint Width、Footprint Depth 和 Placement Surface，并注册到 content catalogue。
- Furniture catalogue thumbnails / icons 的生产边界

### Why After Management MVP

真实 Cafe Loop 和 Decoration playtest 已提供尺寸、通道、可读性和互动需求，避免提前制作大量无法使用的家具。

### Main Difficulty 与 Solution

以小型 modular set 覆盖多种布局，不追求大量单件。每件家具必须完成 `Model → Prefab → FurnitureDefinition Asset → catalogue registration`，并通过 footprint、pathfinding 和 camera readability，才能进入 content catalogue。大桌子、长沙发等 multi-cell furniture 的 footprint 由 content author 在 Inspector 明确填写，不从 Model 自动推断。

### Risks / Likely Bugs

- 家具细节在固定 Camera 下不可读。
- Table / chair footprint 与 Dine-in anchors 不兼容。
- Decorative Collider 造成不必要堵路。
- Theme pieces 只能组合成单一布局。

### Tests

- Scale、pivot、footprint 和 rotation。
- Definition / Prefab mapping、duplicate ID、missing reference、invalid footprint 与 catalogue registration validation。
- 抽查 `2 × 3` table、`1 × 3` sofa 等 multi-cell furniture，确认 Inspector 数据、placement preview 和实际 occupied cells 一致。
- Collider / NavMesh behavior。
- Camera-distance readability。
- Table / chair anchor compatibility。
- Material / texture / reference scan。
- Modular combination fixtures。
- Unity Editor development profiling baseline，并为 Phase 51 Android / iOS device budgets 保留可测量指标。

---

## Phase 27 — Progression & Unlocks

衔接要求（2026-10-26）：复用 P20A 已验证的消费／改善 transaction，新增 unlock 与成长状态同步接入 Save／Offline，不能在 P45 才首次处理；完整 Strategy branches 仍在本 Phase。

### Goal

让经营结果转化为 equipment、menu 和 strategy choices。

### Scope

- Unlock conditions
- Equipment / Menu unlocks
- Strategy points
- Affordable / Premium 基础 branches
- Mixed build support

### Why Now

只有核心经营与角色体验稳定后，才能判断 upgrade 是否真正有价值。

### Risks / Likely Bugs

- Reward 重复领取。
- Locked content 可被绕过。
- Strategy route 形成意外永久互斥。
- Upgrade 破坏最低收益保障。

### Tests

- Unlock idempotency。
- Locked-content enforcement。
- Persistence。
- Strategy combinations。
- Minimum-income regression。

---

## Phase 28 — Store Expansion

衔接要求（2026-10-26）：扩建结果同步更新有效布局、服务能力、Save 与既有 Offline summary 的消费者；本阶段先验证，P45 负责全系统整合。

P10交接（2026-09-29）：扩建后的正式Floor面积更新三项顾客容量。当前P10以已确认Interior区域代表铺好地面；若本Phase支持“先解锁、后铺设”，必须提供独立confirmed Floor来源或保证区域/地板原子发布，不能仅解锁就增加容量。

### Goal

允许玩家购买新的矩形 Grid 区域并扩大可用店面。

### Scope

- Expansion definitions
- Cost transaction
- Region unlock
- Occupancy integration
- Camera bounds update
- Expanded-layout validation

### Why After Progression

Expansion 需要明确 cost、unlock source 和 gameplay value。

### Risks / Likely Bugs

- Cost 重复扣除。
- Locked cells 仍可放置。
- 新区域与旧区域断开。
- Camera bounds 未更新。

### Tests

- Purchase transaction idempotency。
- Locked / unlocked placement。
- Region adjacency / connectivity。
- Camera bounds。
- Save / Load unlocked regions。

---

## Phase 29 — Advanced Wall Construction & Room Editing

### Goal

在 Store Expansion 稳定后，让玩家建造、移动或删除室内墙体，并安全调整房间边界。

### Scope

- Build、move 和 remove wall segments。
- Grid-aligned wall thickness 和 corner / junction rules。
- Door / window opening placement 与 compatibility。
- 开放边界 Entrance Portal 的移动、`2 × 2` Clearance Zone 重定位与 existing-layout conflict report。
- Room detection 和 room boundary rebuild。
- Surface ID preservation / migration。
- Existing furniture、Wall Decoration 和 functional anchor conflict report。
- NavMesh、Camera occlusion 和 business readiness rebuild。
- Confirm / cancel transaction 与 conflict recovery。

### Why After Store Expansion

结构编辑依赖稳定的 unlocked regions、Camera bounds、Save、Decoration、Surface 和经营路径。更早实现会让每次 Expansion 都重新设计墙体与 Room ownership。

### Why Before Exterior Systems

Interior 与 Exterior 都需要明确 Building envelope、Door 和 Entrance boundary。先稳定室内结构编辑，Exterior Facade 才能安全复用墙体、Opening 和 attachment contracts。

### Main Difficulty 与 Solution

整个结构修改使用 staged transaction：先在 preview layout 中重建 Room、Surface、Occupancy、Anchors 和 paths，生成 conflict report；只有玩家确认且营业 readiness 通过后，才替换正式 Layout。无法迁移的对象必须明确列出，不能静默删除。

### Risks / Likely Bugs

- 拆墙后 Room detection 合并或分割错误。
- Corner、junction 和 wall thickness 产生缝隙或重叠。
- Door / Window 失去宿主墙面。
- Entrance 移动后 Clearance Zone 与 Furniture、Room boundary 或营业路径冲突。
- Surface ID 改变导致 Wallpaper 或 Wall Decoration 丢失。
- Furniture 或 functional anchor 被新墙覆盖。
- NavMesh 尚未 rebuild 就恢复营业。
- Cancel 后正式 Layout 已被部分修改。

### Tests

- Build / move / remove wall segment transactions。
- Straight、corner、T-junction 和 enclosed-room fixtures。
- Door / Window host compatibility。
- Entrance relocation、Clearance conflict 与 stable Entrance ID preservation。
- Room split / merge detection。
- Surface appearance 和 Wall Decoration migration。
- Furniture / anchor / path conflict reporting。
- Cancel 保持正式 Layout 完全不变。
- Confirm 后 Occupancy、NavMesh、Camera 和 readiness 一致。
- Save / Load 与旧固定墙体 Save migration。
- 人工 Play Mode 检查墙体 seams、Camera occlusion 和修复提示。

### Not Included

- 多楼层与楼梯。
- Exterior Facade 和户外区域。
- Atmosphere 数值。

---

## Phase 30 — Exterior Model Set

### Goal

制作 Exterior Facade、Outdoor Decoration 和入口区域需要的正式 Models。

### Scope

- Exterior wall modules
- Doors、windows、signs 和 awnings
- Outdoor lights、planters 和 benches
- Bicycle、blackboard menu 和 doorway mat
- Garden pieces 和基础 seasonal decoration
- Exterior Materials、Colliders、attachments 和 Prefabs

### Why Before Exterior Gameplay

Exterior attachment、visibility、Collider 和 entrance-path rules 必须用真实尺寸验证，不能只依赖 Cubes。

### Main Difficulty 与 Solution

Facade modules 和 free-placement decorations 使用不同 contracts。分别建立 wall attachment fixtures 和 ground-placement fixtures，并保持 entrance corridor 可读。

### Risks / Likely Bugs

- Facade seams、scale 或 attachment offsets。
- Outdoor Collider 阻挡入口。
- Flat decoration 错误成为 obstacle。
- Exterior assets 在 Camera 角度下被建筑遮挡。

### Tests

- Module alignment 和 attachment points。
- Exterior footprint / rotation。
- Collider / obstacle policy。
- Entrance corridor smoke test。
- Camera visibility。
- Material / texture / reference scan。
- Save-compatible stable definition IDs。

---

## Phase 31 — Exterior Zone & Facade

### Goal

让玩家改变店铺建筑外观，并在实际 Exterior Scene 中看到结果。

### Scope

- Exterior zone data
- Exterior Scene renderer
- 外墙颜色和材质
- 店铺招牌
- 门窗
- 遮阳棚
- 外墙灯饰
- Facade attachment surfaces
- Exterior appearance Save / Load

### Why After Store Expansion

Exterior 是可解锁店铺区域的一部分，需要复用 Phase 28 的 region ownership、unlock 和 expansion rules。

### Main Difficulty 与 Solution

Facade elements 不使用普通地面 Grid placement。墙面、门窗、招牌和遮阳棚使用明确 attachment surfaces 和 stable attachment IDs；Exterior renderer 根据 Layout data 重建实际外观。

### Risks / Likely Bugs

- 更换外墙时门和入口位置发生意外变化。
- Sign 或 awning 附着到不兼容 Surface。
- UI 中选择已保存，但 Scene 仍显示默认外观。
- Load 后 Exterior attachment 丢失。

### Tests

- Interior 与 Exterior zones 保持分离。
- Facade、sign、door、window 和 awning 的选择正确显示。
- 不兼容 attachment 被拒绝。
- Exterior renderer 与 Layout data 一致。
- Save / Load 保留全部外观选择。
- 外观修改不改变 Entrance stable ID 和位置。

### Not Included

自由摆放户外家具、Customer exterior route、户外座位功能、Atmosphere 数值和天气系统。

---

## Phase 32 — Outdoor Decoration Placement

### Goal

让玩家在 Exterior ground 和兼容外部 surfaces 上自由摆放装饰。

### Scope

- Exterior ground Grid
- Exterior-only / Interior-only compatibility
- 花盆、自行车、黑板菜单和长椅
- 门口地毯、户外灯饰和节日装饰
- 小花园装饰
- Move、rotate、store、confirm 和 cancel
- Outdoor placement preview
- Outdoor furniture Save / Load

### Why After Exterior Facade

先建立稳定 Exterior zone 和 attachment surfaces，再复用 Phase 2 的 occupancy rules 与 Phase 6 的 Decoration Mode rules。

### Main Difficulty 与 Solution

不同装饰使用不同 placement surfaces 和 navigation behavior。Flat decorations 可以不阻挡 Navigation；实体家具根据 validated footprint 和 obstacle rules 参与路径检查。

### Risks / Likely Bugs

- Interior-only furniture 被放到 Exterior。
- Preview 与正式 occupancy 不一致。
- 门口地毯错误生成 obstacle。
- Remove 后 Exterior cells 没有释放。
- Outdoor furniture 在 Load 后位置或 rotation 改变。

### Tests

- Interior / Exterior compatibility。
- Ground、wall 和 doorway attachment rules。
- Rotation、footprint、overlap 和 bounds。
- Cancel 不修改正式 Layout。
- Remove 正确释放 occupancy。
- Flat decoration 不错误阻挡 Navigation。
- Save / Load 保留 Outdoor furniture。
- Scene representation 与 Exterior Layout data 一致。

### Not Included

Customer path readiness、可使用的户外座位、Atmosphere bonus 和外部经营功能。

---

## Phase 33 — Entrance Route & Exterior Validation

### Goal

把 Exterior decoration 与 Customer arrival 和开店 readiness 安全连接。

### Scope

- Exterior Customer Spawn Point
- Street / approach path
- Required entrance corridor
- `Spawn → Entrance` route validation
- Exterior layout change 后的 path refresh
- Invalid layout explanation
- Decoration Mode pause / resume integration
- Business opening readiness integration

### Why After Outdoor Placement

只有实际存在可移动户外家具后，才能正确验证入口阻挡、Collider 和 NavMesh 更新问题。

### Main Difficulty 与 Solution

Layout data 先检查 required corridor，再由 Navigation layer 验证完整 path。退出 Decoration Mode 时 invalidates old paths、重新验证，并只在 readiness 通过后允许营业。

### Risks / Likely Bugs

- 花盆、长椅或 Collider 堵住入口。
- NavMesh 尚未刷新就恢复 Customer movement。
- Customer 继续使用 Layout 改变前的旧 path。
- 错误提示无法指出阻挡区域。
- Decoration Mode 中 movement 没有安全暂停。

### Tests

- `Spawn → Entrance` 存在完整 path。
- 阻挡 required corridor 的 placement 被拒绝或使营业 readiness 失败。
- Error report 指出具体 blocked area / furniture。
- Layout change invalidates old paths。
- Decoration Mode 暂停 Customer movement。
- 退出 Decoration Mode 后重新验证再恢复。
- 多名 Customers 可连续从 Exterior 进入店铺。
- Console 无 NavMesh、Collider 或 missing-reference error。

### Milestone Gate

完成 **Exterior Decoration Milestone**：玩家可以改变实际店铺外观、摆放户外装饰，Customer 能安全经过玩家设计的 Exterior 区域进入店铺。

---

## Phase 34 — Seating & Dine-in Service

### Goal

在保留 Takeout 的同时加入座位、送餐、用餐和清理。

### Scope

- Tables / chairs
- Seat reservation
- Dine-in customer choice
- Table delivery
- Dining duration
- Table cleanup
- Phase 32 已允许摆放的 Outdoor tables / chairs 在本 Phase 获得实际 seat reservation、delivery、dining 和 cleanup 功能。

### Why After Expansion

Dine-in 需要更大空间，也用于验证 Expansion 是否提供真实 gameplay value。

### Risks / Likely Bugs

- Seat 重复预留。
- Takeout 被 Dine-in 完全替代。
- 商品送到错误 Table。
- Cleanup failure 永久占用座位。

### Tests

- Seat reservation ownership。
- Takeout / Dine-in coexistence。
- Correct table delivery。
- Cleanup release。
- Invalid dine-in layout 不阻塞 Takeout。
- Capacity、Navigation 和 Save regression。

---

## Phase 35 — Atmosphere & Themes

### Goal

让装修产生可读但不强迫单一最优布局的经营影响。

### Scope

- Atmosphere values
- Theme tags
- Diminishing returns
- Theme combinations
- Customer preference reaction

### Why After Real Decoration Playtest

需要先观察玩家实际如何摆放家具，才能设计不会鼓励重复堆叠的算法。

### Risks / Likely Bugs

- 同一家具无限叠加最优。
- Theme bonus 重复计算。
- Preview 与实际 Atmosphere 不同。
- Decoration 变成强制数值作业。

### Tests

- Diminishing returns。
- Bonus duplicate prevention。
- Deterministic calculation。
- UI 与 data 一致。
- 多种风格达到相近可行结果的 balance checks。

---

## Phase 36 — Special Rooms & Multi-floor

### Goal

支持后厨、露台、VIP 区和受控多楼层空间。

### Scope

- Preset special regions
- Floor identity
- Controlled entrances
- Stairs / floor transitions
- Floor navigation
- Camera floor switching

### Why Late

多 NavMesh surfaces、跨层 paths、Camera 和 Save complexity 很高，单层系统必须先稳定。

### Risks / Likely Bugs

- NPC 卡在楼层 transition。
- Camera 显示错误 floor。
- 不同楼层 Grid occupancy 混合。
- Save 丢失 floor ID。

### Tests

- Floor-specific occupancy。
- Cross-floor route。
- Unreachable floor readiness failure。
- Camera switching。
- Multi-floor Save / Load。
- Single-floor old Save compatibility。

---

# Milestone F — Signature Content

> 路线更新（2026-10-26）：P37–P44 是延期内容队列。先验证挂机与角色闭环，再按实际回访价值选择；可以在初版发布后逐步加入。资产与对应 gameplay、Save／Offline 适配必须成套交付，未选中的内容不阻塞 P45 或 release。

## Phase 37 — Coffee Bean Visual Asset Set

### Goal

制作 Coffee Bean discovery、collection、packaging 和 drinks 需要的视觉资产。

### Scope

- Bean / bag / package Models
- Quality / rarity visual language
- Collection presentation assets
- Drink presentation variations
- Icons / thumbnails production specifications
- Unity Prefabs 和 Materials

### Why Before Coffee Bean Gameplay

探索奖励必须在固定 Camera 和 UI 中可辨认；先确定 visual vocabulary，gameplay data 才能绑定稳定 asset IDs。

### Risks / Likely Bugs

- 不同 quality 在实际视角下无法区分。
- Asset variants 与 gameplay definitions 数量不一致。
- Icon、Model 和 product ID 错配。

### Tests

- Stable asset-definition mapping。
- Camera / UI readability。
- Variant completeness。
- Missing material / texture scan。
- Prefab scale 和 packaging footprint。
- Performance baseline。

---

## Phase 38 — Coffee Bean Exploration

### Goal

加入员工派遣、Coffee Bean discovery、quality、flavor 和 collection。

### Why First

这是最能体现 AnimalCafe 特色的独立成长系统，并直接使用 Staff、Product、Inventory 和 Progression。

### Risks / Likely Bugs

- 派遣 Staff 仍参与 Cafe work。
- 时间奖励重复领取。
- Random result 超出配置。
- Duplicate discovery 规则不一致。

### Tests

- Dispatch / return state。
- Staff availability。
- Result bounds 与 deterministic seeded tests。
- Duplicate discovery behavior。
- Collection 与 Save / Load。

---

## Phase 39 — Syrup & Add-on Visual Asset Set

### Goal

制作 Add-on devices、flavor indicators、drink variations 和 UI icons。

### Why Before Syrup Gameplay

Add-on 是永久装备而 Syrup 是消耗资源，视觉上必须明确区分，避免玩家误解。

### Risks / Likely Bugs

- Flavor variants 只靠难以辨认的微小颜色差。
- Add-on Model 与 slot ID 错配。
- Drink variation 数量失控。

### Tests

- Add-on / Syrup visual distinction。
- Slot / asset ID mapping。
- Flavor readability。
- Material / texture / reference scan。
- Variant budget check。

---

## Phase 40 — Syrup & Add-on Gameplay

### Goal

加入共用 Syrup inventory、Add-on slots 和 flavored drinks。

### Why Separate From Coffee Beans

两者都会修改 Recipe 和 Menu；分开加入可以准确定位 product-generation bugs。

### Risks / Likely Bugs

- Add-on 被错误消耗。
- 每种 flavor 错误建立独立 Syrup inventory。
- Slot replacement 丢失永久 unlock。
- Fusion recipe 在普通条件下可用。

### Tests

- Shared Syrup consumption。
- Add-on permanence。
- Slot equip / replace。
- Single-flavor normal rule。
- Fusion conditions。
- Save / Load。

---

## Phase 41 — Bakery Visual Asset Set

### Goal

制作 Bakery equipment、trays、display assets 和第一批 products。

### Why Before Bakery Gameplay

Bakery 会同时出现 production 与 display states，Model set 必须先证明两种状态清楚可读。

### Risks / Likely Bugs

- Raw / preparing / ready products 难以区分。
- Tray、display 和 product scale 不一致。
- Bakery equipment anchors 不可达。

### Tests

- State readability。
- Equipment anchor validation。
- Product / tray / display scale。
- Asset-definition mapping。
- Material / reference scan。
- Scene performance fixture。

---

## Phase 42 — Bakery Gameplay

### Goal

加入少量 Bakery recipes、equipment 和与 Coffee 共用的 ingredients。

### Why Separate

Bakery 会改变 production time、equipment queue、shared Milk 和 throughput，需要独立 balance 与 regression。

### Risks / Likely Bugs

- Shared Milk 重复预留。
- Bakery task 占用错误 equipment。
- Display inventory 与 production inventory 不一致。

### Tests

- Recipe / equipment validation。
- Shared ingredient reservations。
- Equipment queue。
- Production / display inventory。
- Out-of-stock recovery。
- Economy 和 Save integration。

---

## Phase 43 — Merchandise Visual Asset Set

### Goal

制作 Merchandise products、packages、displays 和 UI presentation assets。

### Why Before Merchandise Gameplay

Merchandise 不进入制作流程，视觉和 Prefab contract 必须与 food products 分离。

### Risks / Likely Bugs

- Merchandise 被错误配置为 preparation item。
- Display footprint 与 shelf capacity 不匹配。
- 小物件在 Camera 下不可辨认。

### Tests

- Merchandise category validation。
- Display / shelf compatibility。
- Camera readability。
- Asset-definition mapping。
- Material / reference scan。
- Performance budget。

---

## Phase 44 — Merchandise Gameplay

### Goal

加入不需要制作的进货商品和 Customer purchase behavior。

### Why Last in Signature Content

Merchandise 依赖成熟的 Customer budget、interest、Relationship 和 Economy，但不应污染 food-production pipeline。

### Risks / Likely Bugs

- Purchase 扣库存两次。
- Customer budget 变为负数。
- Merchandise 错误进入 Barista task queue。

### Tests

- Restocking。
- Purchase transaction。
- Budget bounds。
- Interest / relationship modifiers。
- Merchandise 不创建 preparation task。
- Save / Load。

---

# Milestone G — Offline 与 Release

## Phase 45 — Offline Progression

### Goal

整合所有实际已开放系统的 Offline summary、自动跨营业／休息周期和回访报告。P18A／P20A 已先验证最小离开／返回体验，本阶段扩大实际发布范围的覆盖，不重新实现第二套结算 authority。

### Scope

- Leave / return timestamps
- Offline cap
- Summary simulation
- Inventory / Economy changes
- Pending major events
- Automatic closing / rest / reopening boundaries，多周期推进与去重。
- Offline Report

### Why Near the End

完整 Offline Mode 必须总结全部实际已开放 gameplay rules，因此最终整合留在后期。首次挂机验证已提前到 P18A，资源与回访成长验证在 P20A；随后随正式角色与选中内容扩展。延期 P29–P44 不是本阶段隐含依赖；未来开放扩展时，同步补齐它的结算与恢复覆盖。

### Main Difficulty 与 Solution

不在后台运行完整 Scene。使用纯数据 simulation，并复用 Active Mode 的 business rules 与 transaction services。

### Risks / Likely Bugs

- 修改设备时间获得异常收益。
- Offline 绕过 Inventory 或 Capacity。
- 绕过休息、超出批准离线时长，或重复跨日造成异常收益；自动跨多个合法周期本身不是错误。
- Major Event 阻塞全部收益。

### Tests

- Same input → deterministic result。
- Offline time cap。
- Clock rollback / large jump protection。
- Inventory / Economy invariants。
- Automatic closing / rest / reopening boundaries；报告未读也能正常跨日，休息不产生正常接待收益。
- Pending major events。
- Offline Save recovery。

### Milestone Gate

完成 **Feature-complete Alpha**：本次批准发布范围内的核心 gameplay 与 Offline 均已存在，后续进入整合／优化。未选中的 P29–P44 仍是未来扩展，不把未实现扩展伪记为完成；新增核心系统必须重新 review 发布范围与依赖。

---

## Phase 46 — Online Identity & Cloud Save — Optional

### Goal

在产品需求确认后，为 Guest/local profile 提供可选平台账号绑定和 Cloud Save。

### Activation Gate

本 Phase 默认不实施。只有用户明确批准 Cloud Save、跨设备恢复、排行榜、好友或跨平台账号需求后，才设计具体方案。

### Recommended Default Scope

- Guest / local profile
- Optional platform-account binding
- Cloud Save upload / download
- Save conflict detection
- User-controlled conflict resolution
- Offline fallback
- Sign-out / unlink safety
- Privacy and data-deletion entry points

### Why After Stable Save and Offline

Cloud synchronization 必须建立在稳定 Save schema、versioning、migration 和 offline conflict rules 上。

### Main Difficulty 与 Solution

不要默认自建 password authentication。优先使用目标平台 identity，并让 local Save 在没有网络或账号时仍可使用。

### Risks / Likely Bugs

- Cloud 覆盖较新的 local Save。
- 同一账号在多设备产生冲突。
- Sign-out 导致本地进度丢失。
- Network retry 重复上传或下载。
- Privacy / deletion flow 不完整。

### Tests

- Guest-only gameplay。
- Link / unlink / sign-out。
- Newer-local、newer-cloud 和 equal-version conflicts。
- Offline launch 和 reconnect。
- Retry idempotency。
- Corrupted-cloud fallback。
- Account deletion / local-data policy checks。
- Platform sandbox integration tests。

---

## Phase 47 — UI/UX Integration & Accessibility

### Goal

统一所有 feature UI、navigation、onboarding 和 accessibility，形成完整可用体验。

### Scope

- Final information hierarchy
- Cross-feature navigation
- Tutorial / onboarding
- Formal icons、copy 和 transitions
- Error-message consistency
- Accessibility settings
- Localization-ready layouts
- Mobile accessibility input、screen reader labels 和 platform Back behavior
- UI sounds
- User-testing fixes
- Finalize Phase 5 provisional typography hierarchy、Display Font need、formal icons、localization behavior、Reduced Motion 和 global feedback consistency；结果同步回 Phase 5 Design System contract。

### Why After Feature Complete

所有真实页面和 workflows 已存在，才能统一 navigation 和信息层级；更早进行 full polish 会因功能变化反复返工。

### Risks / Likely Bugs

- Tutorial 与最终 rules 不一致。
- Modal / back navigation 形成死路。
- Long text 和 localization overflow。
- Accessibility settings 只改变视觉、不改变交互。

### Tests

- Complete navigation map。
- New-player first-day usability。
- Back / cancel / modal flows。
- Long-text / localization fixtures。
- Color / text / input accessibility。
- Error recovery journeys。
- Multiple resolutions。
- UI regression automation 与 manual checklist。

---

## Phase 48 — VFX Production & Integration

### Goal

在已有 functional feedback 上加入正式、可扩展并符合性能预算的 VFX。

### Scope

- Coffee steam 和 preparation feedback
- Order / payment / coin effects
- Trait discovery 和 unlock celebration
- Ambient 和 seasonal particles
- Screen / panel transitions
- VFX quality settings
- Mobile-safe variants

### Why After Stable Gameplay

Functional phases 已证明何时需要 feedback；正式 VFX 可以绑定稳定 events，不必随着 state rules 重写。

### Risks / Likely Bugs

- VFX 重复触发。
- Pause 后 particles / timing 错误。
- Effects 遮挡 characters 或 UI。
- Overdraw、particle count 和 mobile cost 超出预算。

### Tests

- Event-to-effect one-shot mapping。
- Pause / resume behavior。
- Camera / UI occlusion。
- Missing VFX reference fallback。
- Particle / overdraw performance fixtures。
- Quality-level switching。
- Mobile-safe material compatibility。

---

## Phase 49 — Final Model Replacement & Asset Optimization

### Goal

清除剩余 primitives 和临时视觉资产，并统一、优化全部正式 Models。

### Scope

- Placeholder inventory 和 replacement
- Remaining production Models
- LOD、texture compression 和 material consolidation
- Collider cleanup
- Definition / Prefab reference validation
- Android / iOS memory budgets；Windows Editor 只保留 development profiling baseline
- Final art consistency pass

### Why Before Release Preparation

核心 Models 已分批完成，本 Phase 只处理遗漏和整体优化，不承担第一次制作所有资产的高风险工作。

### Risks / Likely Bugs

- Replacement 改变 footprint、pivot 或 anchors。
- Definition IDs 改变导致旧 Save 丢失家具。
- Material consolidation 破坏外观。
- Optimization 导致 Collider 或 LOD popping。

### Tests

- Zero unintended primitives / placeholders。
- Stable Definition IDs。
- Footprint、pivot、anchor 和 Collider regression。
- Missing reference scan。
- Save migration fixtures。
- LOD / material visual QA。
- Memory、load time 和 frame-time budgets。

---

## Phase 50 — Mobile UI Adaptation

### Goal

将完整 UI system 和 Camera presentation 适配为可用、可读、Touch-first 的 Android / iOS presentation。

### Scope

- Touch target sizes
- Safe Area
- Mobile aspect ratios
- Portrait-first base Camera framing and adaptive aspect presentation
- Continuous `1.0x`–`3.0x` zoom presentation and UI / Camera gesture priority
- Small-screen panel variants
- Gesture / UI conflict handling
- Mobile text scale
- Virtual keyboard flows
- Platform Back behavior
- Background / foreground UI state restoration contract
- Finalize Phase 5 provisional portrait layout、Safe Area、touch targets、mobile aspect behavior、gesture priority 和 mobile Strong Frost performance / fallback budget。

### Why Before Platform Adaptation

先验证共享 mobile UI 与 interaction presentation，再分别处理 Android / iOS lifecycle、build 和 native integration，避免同时调试 layout 与 platform issues。

### Risks / Likely Bugs

- Touch gesture 与 ScrollView / Camera 冲突。
- Notch、camera cutout 或 home indicator 遮挡 controls。
- Small-screen Modal 无法关闭。
- Landscape 或 foldable aspect ratio 发生裁切。
- Strong Frost 在目标设备超出 frame / battery budget。

### Tests

- Target mobile resolutions、aspect ratios 和 Safe Areas。
- Minimum touch targets。
- Gesture priority。
- Long text / virtual keyboard。
- Platform Back contract fixtures。
- Background / foreground UI state contract。
- No-hover complete workflows。
- Strong Frost quality / fallback profiling fixture。
- Mobile UI manual usability test。

### Milestone Gate

所有 core workflows 都有不依赖 Hover / Mouse / Keyboard 的可用 mobile presentation，并通过 UI / Camera gesture 与 Safe Area checklist。

---

## Phase 51 — Android & iOS Platform Adaptation

### Goal

在不分叉 core gameplay rules 的情况下，把共享 mobile presentation 接入 Android 与 iOS platform lifecycle 和 device builds。

### Scope

- Android / iOS touch pan、pinch zoom 和 tap integration
- Real-device two-finger Pinch during an active Decoration Furniture Preview；
  migrated from historical Phase 6 case `P6-M-023`
- Map both platforms to the shared continuous `1.0x`–`3.0x` zoom envelope
- Android system Back and iOS in-app Back contract
- Background / foreground lifecycle
- Suspend / resume state recovery
- Android / iOS local Save paths and permissions
- Platform-specific Safe Area integration
- Device build configuration and signing placeholders
- Android and iOS development builds
- Platform-specific crash / log collection path

### Why After Mobile UI Adaptation

Phase 50 先证明共享 Touch UI 和 gesture rules；本 Phase 只处理 platform integration，不为 Android 与 iOS 建立两套 gameplay architecture。

### Risks / Likely Bugs

- Android Back 与 Panel / Modal stack 不一致。
- iOS background / resume 丢失 UI state。
- 不同平台 Save path 或 permission behavior 分叉。
- Touch 与 native gesture / system area 冲突。
- 两个平台使用不同 gameplay data rules。

### Tests

- Android / iOS core touch workflows。
- On real Android and iOS devices，start an active Decoration Furniture drag，add
  a second finger and Pinch；Camera zooms within the shared envelope，Furniture
  remains pending，and no Confirm、Cancel or Store action fires。
- Android system Back 与 shared Back contract。
- Platform Safe Area integration。
- Background / foreground persistence。
- Save path、permission 和 corrupted-data recovery。
- Development build smoke tests on both platforms。
- Android 与 iOS 使用相同 gameplay data rules。

---

## Phase 52 — Mobile Release Preparation

### Goal

把 Android 与 iOS development builds 整理为稳定、易理解、性能合格并可提交商店审核的 Mobile Release Candidates。

### Scope

- Tutorial / onboarding final pass
- UI / UX、audio、animation 和 feedback polish
- Content、economy 和 progression balance pass
- Accessibility and localization release checks
- Mobile performance、memory、thermal 和 battery profiling
- Strong Frost final device quality / fallback matrix
- Save migrations、fresh install 和 upgrade install
- Android App Bundle / iOS archive release builds
- Store metadata、privacy declarations 和 permission review
- Device matrix、OS-version compatibility 和 bug triage

### Why Last

共享 gameplay、mobile UI、Android 和 iOS integration 都已验证后，才冻结 release behavior、performance budgets、store configuration 和 compatibility matrix。

### Risks / Likely Bugs

- Late polish 引入 regression。
- Android / iOS release configuration 不一致。
- Long-session memory、thermal 或 battery 超出预算。
- Upgrade installation 或 migration 破坏 Save。
- Store permission / privacy declaration 与实际 behavior 不一致。

### Tests

- Full automated regression。
- New-player first-day mobile usability test。
- Long-session soak、thermal、memory 和 battery profile。
- Android / iOS device matrix and OS-version coverage。
- Fresh install / upgrade install / Save migration recovery。
- Background / foreground and interruption recovery。
- Release build smoke test and store preflight checklist。
- Android 与 iOS gameplay data parity。

### Milestone Gate

产生通过 release、device、performance、privacy 和 store checklist 的 Android 与 iOS Release Candidates。

---

## 6. Major Milestone Review Gates

| Milestone | 完成 Phase | Review 重点 |
|---|---:|---|
| Foundation Prototype | 0 | 基础 interaction 与 time 是否稳定 |
| Visual Foundation | 5 | Asset pipeline、第一批正式 Models 与 UI foundation 是否可重复扩展 |
| Layout Foundation | 8 | Layout、Surface 与 functional furniture contracts 是否足够支持经营，但未 over-engineer |
| Core Cafe Loop Prototype | 14 | 自动外带服务是否稳定、可读、无卡死 |
| First Leave / Return Slice | 18A | 基础 Coffee 的真实离开／返回、自动周期、收益去重与有限移动生命周期 feasibility |
| Playable Management MVP | 20 | 多日经营、Application Shell、Menu、Inventory 与 Save 是否形成闭环 |
| Validated Idle Management MVP | 20A | 含资源约束的 Offline、非阻塞摘要与一次有意义改善，是否让玩家放心离开并愿意回来 |
| Character-driven Vertical Slice | 25 | 正式角色 pipeline 与角色差异是否真实影响体验 |
| Core Cafe Growth | 28 | 小型核心家具、Progression 与扩建是否形成可保存且支持 Offline 的成长循环 |
| Exterior Decoration（延期扩展） | 33 | 如开放 Exterior，assets、placement 与 entrance 安全验证一起交付 |
| Cafe Growth Extension（延期扩展） | 36 | 如开放 Dine-in／高级装修／多楼层，相关能力与 Save／Offline 是否互相支持 |
| Signature Content（延期扩展） | 44 | 仅对已选中特色内容验收；增加选择而非高频维护负担 |
| Feature-complete Alpha | 45 | 当前发布范围的核心 rules 是否支持 Offline summary，不要求延期内容全部完成 |
| Optional Online Services | 46 | 如启用，Cloud conflicts、offline fallback 和账号安全是否可靠 |
| Release Visual & UX Integration | 49 | UI、VFX、Models 和 performance 是否达到 release preparation 标准 |
| Mobile UI Ready | 50 | Touch layout、Safe Area、gesture priority 和 mobile workflows 是否完整 |
| Android / iOS Device-ready Builds | 51 | Platform lifecycle、Save paths、Back behavior 和 development builds 是否一致 |
| Android & iOS Release Candidate | 52 | Stability、store readiness、device performance、battery 和 release checklist |

每个 Milestone 完成后：

1. 进行完整 regression 和 manual playtest。
2. 记录玩家可见的优点、摩擦和新 risks。
3. Re-review 后续 Phase 顺序和 scope。
4. 必要时更新 Game Design 与 Roadmap。
5. 未经 approval 不自动开始下一 Milestone。

---

## 7. Current Next Step

**最新进展（2026-11-02；15:12Z）：P15仍IN_PROGRESS。** 当前Source61完整Core618／618及真实Main＋原rapid／Working四项5／5 Native PASS。Main原120秒5自然来客／5真实Exit、全token释放、drain=True、无Blocked；送杯／返程及producer-preview接线阻断已验证修复，保原Frozen／预算。12seed＋Photo4共16布局运行中；后续Long4、mixed60Exit、轻记录Main性能、完整suite、GUI和独立whole review仍待，详见[Validation Report](Phase15_Validation_Report.md)。无Git／CI／merge／正式closure或P16。

**历史阶段记录（2026-11-02；00:26Z 收据）：P15仍IN_PROGRESS。** Core532／532 PASS，CFC实际接线已编译；原Photo四组仍4 FAIL，真实Exit为6格两档各4、7格两档各2。新增原始记录显示尚未acquire的第3客离店遇new-frozen-owner，正核实际handoff／phase age／planning状态；模型通过不算经营完成。当前Core786／CFCFD055／TestsBBFC；R17／性能完整窗口／最终suite／Main／Validation／final review仍开放。详见Phase15 Validation Report；无正式closure／CI／Git／P16。

**历史阶段记录（2026-11-01；23:52Z 收据）：P15仍IN_PROGRESS。** Core528／528 PASS，真实lease、Nav暂缺、原ArrivalTolerance及opaque／精确几何边界已定向闭合；原Fast经营仍FAIL，Completed1／3live，首次离店实际拒绝链仍在取证。1x canonical successor CFCconsumer正在实现，Core通过不代表经营完成。当前Core612C5446／CFCE5BE008A／TestsB9FF043E；Photo矩阵／R17／性能完整窗口／最终suite／Main／Validation／final review仍开放。详见Phase15 Validation Report；不正式closure／CI／Git／P16。

**历史阶段记录（2026-11-01；23:25Z 收据）：P15仍IN_PROGRESS。** Core512为510 PASS／2 canonical successor 功能反例 FAIL；原2x尚未获权取餐准备重证及真实body／claim三项模型通过，但原照片Fast仍失败，第一次strict leg尚未发出、snapshot变化后被拒绝，继续核对实际own claim认证。1x canonical successor Core草案独立审查中、CFC未接线。Core94E036／Tests7D337／CFC95DA8598。R17、性能完整窗口、最终suite、Main／Validation及最终review仍开放，完整状态见Phase15 Validation Report；无正式closure／CI／Git／P16。

**历史阶段记录（2026-11-01；21:33Z 收据）：P15仍IN_PROGRESS。** 完整Core478通过；照片2x错误取消已消除，但真实离店仍等到原预算耗尽，腾空／阶段交接事件链正在定位；照片1x已记录原new-frozen-owner拒绝。12seed已加入、NOT_RUN；性能计数取得2项行为RED待接线。恢复5／5、中央两档各60、普通长柜台6／7格两档各20的旧PASS保留source范围。当前Core44F2／CFC95DA；照片四组、性能、最终suite、Main／Validation、最终review仍开放，不正式closure／CI／Git／P16。

**历史阶段记录（2026-11-01；20:45Z 收据）：P15仍IN_PROGRESS。** 共同层884模型通过；CFC接线后原1x及固定1秒／3秒来客2x均各20真实Exit并排空，M3两项通过。四项故障／恢复检查仍被已退休失败记录的测试取证方式阻断，原断言尚未走完；正补原Fail时观察。原八布局Native运行中；完整回归、性能、Main／Validation及最终review仍开放，不进入CI／Git／P16／正式closure。详见[Validation Report](Phase15_Validation_Report.md)。

**历史阶段记录（2026-11-01；20:21Z 收据）：P15仍IN_PROGRESS。** 共同层相关模型884／884 PASS（完整Core464／464，原820缺失0），CFC取餐接线尚未接入；离店恢复5项Native此前PASS；原1x连续20单已真实完成并排空，2x已证实当前取餐任务仍Accepted，但同ActiveIntent已Terminal StaleContext，consumer持续暂缓；后续随机时序重跑20单PASS不覆盖该故障；固定1秒／3秒来客均已捕获同一首次失效：他人到达增加Counter全局Version，当前顾客自身权属未变。真实Queue9项及新增2项strict兼容测试已纳入884全部通过；pre-pump消费接线草案待独立审查与Native验证。M3跨Queue精确通知已取得真实RED→修后2／2Native PASS，实际释放唤醒保持，同Queue Version变化保守处理。最终suite、扩展回归与性能、Main／Validation真实窗口及最终review仍未完成。证据与历史见[Validation Report](Phase15_Validation_Report.md)，不进入P16／CI／Git／正式closure。

**历史阶段记录（2026-11-01；17:34Z 收据）：P15仍IN_PROGRESS。** collected Exit安全重绑的5项真实Native通过；queued strict freshness修复后Core429／429 PASS、局部review C0／I0。原20单两档仍FAIL：1x 7入店／6Exit后counter gap停滞；2x新顾客入店仍遇到另一处未完成旧腿。M3为SKIP／NOT_PROVEN，已定位运动期间准入准备被重新撤销，未改通知规则。继续原计划修复与验证；最终suite／扩展回归／真实Main与Validation窗口／最终review未完成。精确证据及历史见[Validation Report](Phase15_Validation_Report.md)，不进入P16／CI／Git／正式closure。

**历史阶段记录（2026-11-01；16:45Z 收据）：P15仍IN_PROGRESS。** 原20单两档重测FAIL；1x已定位同一未发出返程遇到外部counter claim退休的snapshot边界，2x原依赖失效仍取证。Carry已修复真实ActorUnavailable未Blocked的问题，但显式恢复仍FAIL。员工停用的availability hold与独立Agent故障反测PASS；无关counter精确唤醒前提未成立，仍SKIP／NOT_PROVEN。当前完整suite、扩展布局／性能、Main／Validation真实窗口及独立review未完成。证据及历史状态见[Validation Report](Phase15_Validation_Report.md)；不进入P16／CI／Git／正式closure。

**历史阶段记录（2026-11-01；15:29Z 收据）：P15仍IN_PROGRESS。** 相关820模型、HUD29＋5、营业周期68及交接检查12项Native通过；有限取餐已真实WAIT→原lease释放→同源一次提交／实际Exit，原双侧及三客通过。最新16 Native为15P／1员工停用反测FAIL（暂停／Terminal前提正取证）。持续20单曾3／5入店后停接客，现首次忙端点的延后guard定向PASS，原两档20单尚待重测；Carry3失败仍开放。精确唤醒／生命周期／Exit／兼容、扩展布局与性能、最终suite、Main／Validation窗口及review未完成。逐源码证据见[Validation Report](Phase15_Validation_Report.md)，保留历史FAIL／SKIP，不进入P16／CI／Git／正式closure。

**历史阶段记录（2026-11-01；11:40Z 收据）：服务交通重构 IN_PROGRESS，P15 未完成。** work index 与相关14组模型757／757 PASS，真实冷历史枚举量1001→1；这尚不证明最终性能。两项静态布局 Native 仍2 FAIL，已分别定位到取餐占用目标选择与 snapshot 变化后的旧请求失效，按原方案分别处理。实际 drain／Closing admission、metadata／精确唤醒、其余 Exit、兼容测试、布局／性能、最终 suite、Main／Validation 手测及 CI／merge 仍未收口。证据见[Validation Report](Phase15_Validation_Report.md)。不进入P16，不commit／push／merge。

**历史阶段记录（2026-11-01；11:19Z 收据）：服务交通重构 IN_PROGRESS，P15 未完成。** 员工重建修复相关 Edit739／739 PASS；单侧完整经营离店流程及收银员重建的定向 Native 已通过。两项静态站位组合仍 FAIL，正在按实际拒绝点分别处理。当前计数6项为5 PASS／1 FAIL，证明历史1000条记录仍放大每次处理扫描量，work index 尚未接入。metadata／精确唤醒、实际 Exit 恢复、drain／正式 UI、扩展布局、性能、最终完整 suite 及 Main／Validation 手测仍开放；各轮源码与原始结果见[Validation Report](Phase15_Validation_Report.md)。历史 FAIL／SKIP 保留，不进入P16，不commit／push／merge。

**历史阶段记录（2026-11-01；09:40Z 收据）：服务交通重构 IN_PROGRESS，P15 未完成。** 当前 Source16 `D19B78E...` 相关 Edit727／727 PASS，覆盖退休 payload 清理与 callback／未完成腿的 pending；work index、实际 drain 接线仍待实现。此前路径版本 Source16 `51911F...` 的 Native13 为7 PASS／6 FAIL：已能声明有限让位选项并进入真实 BeginYield 准备，完整单侧经营链仍失败。单项诊断确认首个让位落点被占用后，24个原候选在 queries0 时整项 Terminal，扩大 Core 失败范围的提案触发两项旧保护回归，已完整撤回；当前准备提交前空位筛选方案；两项独立布局失败保留。此前 Edit710／Native28 PASS＋2 SKIP等结果仅对应原 capture-only 源，SKIP不算通过。其余 pickup metadata／精确唤醒、实际 Exit 恢复、drain／正式 UI、扩展布局、性能、最终完整 suite 与 Main／Validation 手测均未收口。下方636项及Main FAIL是历史源码快照，不替代当前验收。当前证据和 source binding 见[Validation Report](Phase15_Validation_Report.md)。不进入P16，不commit／push／merge。

**当前开发（2026-10-30）：Owner已批准服务交通重构的实现及测试。** [统一服务交通设计与Regression](superpowers/specs/2026-10-30-service-traffic-coordination-design.md)采用保留NavMesh、集中空间承诺／等待依赖、限制候位侵占必要通路的方向；列出20组regression责任及7项manual。[Implementation plan](superpowers/plans/2026-10-30-service-traffic-coordination.md)已建立，按原Main反例→统一许可→逐入口迁移→新源regression执行。下方原Main FAIL与P15 IN_PROGRESS保持，未执行的新验证仍NOT_RUN；不跨Phase，不用旧suite或文档review代替新源验收。

**执行进展（2026-10-31）：仍在实现及定向验证，P15未完成。** 顾客 Queue 的 canonical 目标绑定及出生前取消／恢复衔接，已通过当前14-class回归636项（包含379项模型）、7个真实Native场景和独立限定review（C0／I0／M0）。其中新增恢复用例完成实际RED→GREEN，同一请求累计查询1→2，仅发布一次出生。此前参数门禁导致0入店、以及临时拒绝后请求停住的失败记录均保留。当前开始有限服务侧选择的测试与接入；最近连续经营场景在C101源码仍为3入店／1离店FAIL，BA33未重跑，不以7个场景PASS替代连续经营通过。新源完整suite、长跑、性能、Main及Validation手动验收仍待执行；旧source证据不覆盖后续实现。

**最新Main普通UI验收（2026-10-30本地）：FAIL，P15仍IN_PROGRESS。** 正常目录确认三项Ready后Done自动接待，但首单Barista在Prepared阶段Blocked／IncompletePath、Completed=0；Pause／2x／缩放仍响应。精确原因UNKNOWN，本轮只测试和留证，没有修复或保存Scene。3176Assets／12source／14Scenes当前差异0，现场保留Play＋游戏Pause，不算退出release。先处理这一Main经营验收缺口，再决定正式收尾；此前相关suite／Validation窗口PASS不覆盖本次失败，不进入P16。见[Main补测报告](../outputs/phase15/reservation-rule-20261029-01/manual-main-complete/report.md)。

Owner已明确搁置历史“整个游戏无响应”，因其未再出现；首因保持UNKNOWN，不作为当前追查事项。此次Main经营阻塞时Pause／2x／缩放均有响应，不能将两种症状归为同因。

**历史已批准修复记录（2026-10-27）：** Owner确认CustomerFlowController与复现测试两文件范围，正在按TDD修复临时交通误入Blocked；真实静态堵路仍须安全显式恢复，当前未宣称GREEN或原全游戏无响应已解决。

**此前相关suite与Validation窗口（2026-10-30）：PASS，不覆盖上方Main失败。** CFC C8F521／EFC71744D同源相关Edit387PASS、Play299PASS／0FAIL／6项明确opt-in SKIP，旧302用例缺失0，重点30项及原八种长柜台各20真实离店全PASS；中央柜台两个controlled-clock用例各60真实离店。普通Editor真实中央柜台装修后618.975现实秒至Day7／Completed66，观察到真实Rest全容量0、自动重开及各按钮／镜头／横竖屏响应，Console0warning／0error；独立review C0/I0、3176Assets／源码／Scenes差异0。保留有限额外寻路开销和历史wholeGame首因UNKNOWN，不以本次窗口覆盖历史FAIL或旧源码61-real-clock／完整suite。下一步按既有流程进行Owner观感及CI／Git／merge收尾，不自动进入P16；8h／设备等限制另列。见[Validation Report](Phase15_Validation_Report.md)和[当前GUI报告](../outputs/phase15/reservation-rule-20261029-01/manual-detour-final/report.md)。

**此前 Next Step（2026-10-27）：Main玩家UI／Validation诊断分离修正已验证，等待Owner观感，并继续既有P15收尾边界，不进入P16。** 当前Edit59 PASS、定向Play122 PASS／1原有SKIP／0FAIL，独立review与限定真实UI检查通过；原七项manual、旧完整suite按各自source保留。Main完整正常装修、直接付款集合、Debugger UNKNOWN、8h／device／CI／VCS／merged-main仍未收口；正式closure为No。见[最新报告第17节](Phase15_Validation_Report.md#17-main-玩家界面与-validation-诊断分离2026-10-27)。

**此前 Next Step（2026-10-27）：原批准M01–M04 Validation补测完成，原七项均有Agent通过记录；处理明确保留的Debugger UNKNOWN及补充集成检查、必要Owner观感和Phase交付门槛，不进入P16。** Main正常装修开店、直接完整付款集合未完成；当前源定向122 PASS／0 FAIL／1原有SKIP，旧完整suite保持修复前source边界。本轮Assets3172零差异，无产品／测试代码或Scene修改。Owner观感、8h／device、CI及版本管理未验证，正式closure／merge readiness为No。详见[Agent报告](Phase15_Agent_Playtest_Report.md)及[Validation Report第16节](Phase15_Validation_Report.md#16-m01m04-validation-补测与范围澄清)。

**历史 Next Step（P14，2026-10-23）：本轮自然单列＋P14收银1秒已完成技术验证，Owner仅确认Guide中新队形与前移观感。** 受影响直接Edit267／Play171均PASS，19原变体／真实人数上限／自然20单两档与2个controlled恢复分列；普通Editor六人及自然三单完成，临时helper／317protected／15冻结源／Editor0已核对。原三项visual PASS保持。实际人数表、独立review和清理见P14 Guide／Report。制作3秒、来客1～5秒与P13两秒保持；Phase最终完整suite／Player／真机仍另行收尾，未提交／未进入下一Phase。

**历史记录（P13，2026-10-14）：开发、追加回归及16项manual验收已完成；最终review通过，下一步由Owner决定版本管理收尾。** M-009/M-016已按Owner明确指示记录手测PASS，computer-use未完成尝试单独保留。工作位于E盘项目内P13 worktree；证据见 [P13 Validation Report](Phase13_Validation_Report.md)，操作入口见 [Beginner Guide](Phase13_Beginner_Guide.md)。本轮未commit / push / PR / merge，未进入P14。以下条目保留历史状态。

**历史 Next Step（P12，2026-10-09）：20fps修复与受影响回归已通过，补连续来客/前移视觉观察。** 直接Play112 PASS、P11/P12 Edit126 PASS，原失败的排空与30游戏分钟循环通过。真实场景覆盖20fps，原到期计时与安全同向并发回归保持。仅补默认Play等待六到八位、让队首离开两三次的人工观察；M1～M8与桌角既有Owner PASS保留为历史快照。全项目完整suite/Player/真机本轮NOT_RUN，无commit/push/PR/merge。Owner已重新授权提交、push当前branch并创建合并到main的PR；merge与branch/worktree清理未授权，不进入P13。以下P9–P11描述为历史交接。

**历史 Next Step（2026-10-04）：按 Owner 授权完成 review、push、PR #9 merge、本地 main fast-forward 和分支清理。** 本次交付包含 P9–P11、已接受的角色材质调整和当前分类重新展开目录的修复；review、RED/GREEN、完整回归与人工验证边界见 [P9–P11 Merge Review](Phase9_Phase11_Merge_Review.md)，远端合并结果见 [PR #9](https://github.com/zhengparker/AnimalCafe/pull/9)。P9/P10 已有验收记录，P11 M-001～M-011 全部 Owner PASS；本次 Decor 补充人工回归尚未记录。MainCafe 仍为 passive 接线、零营业 NPC。合并后的后续阶段由 Owner 决定，P12 未开始。

**历史 Next Step（2026-09-29，P10 交付时）：将已通过最终review与验收的P10交付到同一P9分支，之后等待Owner决定main merge或后续阶段。** P10自动回归、M-001…010及Engineering/QA整体review均PASS；Owner当时已授权commit/push至 `codex/phase-9-order-domain`。P10提交基于P9 `94cc48d7d8c6b43261b96e73bf2d2b6541fafbf4`，不另开分支。P9/P10的main merge、正式closeout、Roadmap Completed未在这份历史记录中完成；不将旧main视为包含这些工作。下方2026-09-28及更早的“下一步”描述保留为历史记录。

**Phase 1 — Layout Data Model** 已完成 implementation、automated verification、manual acceptance、merge 和 merged-main regression，状态为 `Completed`。

**Phase 2 — Grid Occupancy & Placement Rules** 已完成 approved design、implementation、automated verification、review、manual acceptance、merge 和 merged-main regression，状态为 `Completed`。

**Phase 3 — Visual Style & Asset Pipeline Foundation** 已完成 approved design、implementation、review hardening、manual acceptance、merge 和 merged-main regression，状态为 `Completed`。

- merged-main post-merge evidence（2026-08-03）：EditMode `312 / 312`、PlayMode `55 / 55` passed，failed、skipped、inconclusive 均为 `0`；production validator 为 `3 / 3` valid、`0 issues`；Build Settings/MainCafe isolation 继续由 automated suites 覆盖。
- Studio Owner 已确认全部三件 user-provided / Tripo-generated benchmark assets 具备用于开发与商业发布所需的使用权，license/use-right gate 为 `Passed`；Camera/readability manual acceptance 为 `Passed`。
- **Phase 4 — Core Architecture & Functional Furniture Models** 已完成 approved design、TDD implementation、review、Studio Owner `M01–M88` manual acceptance 与 final regression，状态为 `Completed`。Merge-preparation evidence（2026-08-11）：EditMode `570 / 570`、Editor PlayMode `62 / 62`、Windows standalone PlayMode `62 / 62` passed；PlayMode assembly 不引用 `UnityEditor`；P3 validator `3 / 3 valid`、P4 validator `5 / 5 valid`，issues `0`。
- **Phase 5 — UI Architecture & Design System** 已完成 approved design、TDD implementation、review hardening、Studio Owner `MT001–MT034` manual acceptance、PR #4 merge 与 merged-main regression，状态为 `Completed`。Merged-main evidence（2026-08-15）：EditMode `690 / 690`、Editor PlayMode `121 / 121`、Windows standalone PlayMode `103 / 103` passed；failed、skipped、inconclusive 均为 `0`。
- **Phase 6 — Basic Decoration Mode** 已完成 approved design、TDD implementation、independent review、Studio Owner manual acceptance 与 fresh full regression，状态为 `Completed`。Merge-preparation evidence（2026-08-22）：EditMode `1136 / 1136`、Editor PlayMode `446 / 446` passed，failed、skipped、inconclusive 均为 `0`；Windows standalone build 为 `Success`，无 C# warning/error；Studio Owner applicable manual set 为 `29 / 29 PASS`。`P6-M-023` 的真实 Android + iOS two-finger Pinch 已按 scope decision 移至 Phase 51，不计入 Phase 6 denominator。
- Phase 6 清理只删除 obsolete Phase 4 MainCafe manual-review setup 与两份 temporary materials；`ManualReviewPingPongMover` 及其 regression test 因仍有 Phase 5 live consumers 而保留。
- **历史 Next Step（2026-09-28）：Phase 8 已 Completed；Phase 8R 已合并并完成工程收尾，当时可进入Phase 9 Order Domain设计准备。** PR #8、全量回归、本地fast-forward、证据归档及P8R本地branch/worktree清理已完成；见 `Docs/Phase8R_PR8_Review_2026-09-28.md`。当时Phase 9代码实施尚未批准；最新授权与交接以上方2026-09-29记录为准。历史人工与设备验收限制不因收尾而消失，Phase 8历史证据继续见 `Docs/Phase8_Beginner_Guide.md` 第37节及15.6节。
- 不执行旧版 Phase 1 Core Cafe Loop plan。
- 历史 P10 scope：当时只执行获批容量domain与诊断工具，真实cafe day loop、Customer/NPC movement、排队/生成、NavMesh、economy和Save按各自Phase开展。当前 P11 Navigation 进展以上方最新记录为准。

**历史阶段记录（当时，2026-10-30）：P15修复IN_PROGRESS。** 后来实际离店抢占较早clear/unissued pickup route的缺口已有Native RED→GREEN；补真实canonical未转移目标反例并局部修正后6P0F，独立review C0/I0。产品增量仅CustomerFlowController与对应测试，CFC280C2297...60320F。新真实时间流程又在尚未装修的Day1 Closing记录到员工交付到站复核FAIL，具体predicate仍UNKNOWN；短multi-substep1P、120秒0P0F1INCONCLUSIVE不能覆盖该FAIL。继续带只读到站指标的完整受控复测，不放宽到站／碰撞／时间，不以旧387Edit／288Play快照冒充新源完整通过。原manual7 NOT_RUN／manual8 FAIL、Owner／CI／Git／正式closure门槛仍保留。
