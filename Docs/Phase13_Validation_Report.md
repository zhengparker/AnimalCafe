# Phase 13 — Employee Task Flow Validation

> 状态：原P13本地技术交付、完整自动验证及10项委托代测通过；Owner 已确认M-001～004、M-006、M-009、M-016手测PASS，结合10项已授权代测（M-006重叠），16项manual均已有通过记录。Owner新增批准台面降低30cm至42cm，游戏内调整与追加回归已完成：最终完整PlayMode 1267 PASS / 0 FAIL / 5原有opt-in SKIP；完整EditMode 2599 PASS（微小位移修复前），最终P11 Edit增量77 PASS。本地最终review结论见本页末尾；版本管理收尾与P14授权另行处理。
> 日期：2026-10-14。结果以本页链接的最终 XML 为准；NOT_RUN / SKIP 不表示 PASS。

工作区：`E:/Unity/Project/AnimalCafe/.worktrees/phase-13-employee-task-flow`。
Branch：`codex/phase-13-employee-task-flow`；基线：`302f3e969da667f3f389820d303503865b1b0d63`，包含 P12 合并提交。

## 已批准的范围

- 两名独立员工：Cashier 在顾客所选 CR 的 Employee Side 待命；Barista 在 CM 的 Employee Anchor 待命。
- Cashier working 2 个游戏秒；Barista working 3 个游戏秒，随后上半身 Carry、下半身实际 Walk，持杯到 Pick-up 后立即放下，再空手实际返回 CM。
- 放杯是交付成功点；Returning 仍忙，返抵后才 Idle。桌面每个正式 Surface Slot 最多显示三杯，三角形排列，第四杯后仍可成功交付。
- Pause/Decor、取消、正式布局变化、停用与销毁均处理任务及 callback 所有权。员工不参与 Customer reset；NavigationWorld 保持唯一位移执行者。
- 本阶段使用受控任务，真实 Customer/Order 服务整合属于 P14。

批准依据：[Design](superpowers/specs/2026-10-13-phase-13-employee-task-flow-design.md)、[Test Cases](superpowers/specs/2026-10-13-phase-13-employee-task-flow-test-cases.md)、[Implementation Plan](superpowers/plans/2026-10-13-phase-13-employee-task-flow.md)。38 项 E、18 项 P、16 项 manual 是需求场景 ID，不等同于 NUnit case 数量。

## Task focused 验证记录

以下是各 Task 当时的源码快照及相关回归，不能相加作为最终 suite 数量。最终全量结果另行记录。

保留的本地 `.log` 仅遮蔽 Unity CLI 自动附加的 access-token 参数；测试断言、结果与 XML 未改。

| Task | 结果与证据（`outputs/phase13/`） | 独立审查 |
| --- | --- | --- |
| 1 binding | RED 16F/8P；GREEN 24P；相关回归 44P。`task1-green.xml`、`task1-regression.xml` | Spec/Quality PASS |
| 2 runner | 初始 RED 32F；修复 placement 重入后 runner 38P + binding 24P，共 62P；相关回归 29P。`task2-fix1-green.xml`、`task2-fix1-regression.xml` | Important R1 已修复，原 reviewer 复审 PASS |
| 3 cup display | 补充行为 RED 6F；GREEN 19P；相关回归 30P。`task3-green.xml`、`task3-regression.xml` | Spec/Quality PASS |
| 4 animation/assets | 资产 GREEN 5P、动画 GREEN 5P；P11 资产回归 8P、动画回归 4P。`task4-regression-play.xml` 共 9P，另 5 项是 P13 动画复跑 | Spec/Quality PASS；Owner 视觉待验收 |
| 5 real flow | RED 8F；GREEN 8P；最终相关 Play 39P=P11 Movement 31+P13 Flow 8，P12 Admission Edit 4P。`task5-regression-play.xml`、`task5-regression-edit.xml` | Spec/Quality PASS |
| 6 lifecycle | 基础验证 Edit 43P、Play 32P；其后 R1 最小修复验证 Edit 67P=Lifecycle 5+Runner 38+Binding 24。`task6-final-verified-play.xml` 是 R1 前快照；`task6-fix1-green.xml` 绑定该修复 | Important R1 已修复，原 reviewer 复审 PASS |
| 7 Scene/HUD/manual 入口 | 最终 Edit 9P；补实际 MainCafe 四顾客同场后 Play 5P，`task7-fix1-final-play.xml`。直接 Play 97P，含 P13 4、P12 81、P8R 11、P11 1；`task7-green-direct-play.xml` 对应 Builder/Capture 最后调整前快照 | Important R1 覆盖缺口已补，原 reviewer 复审 PASS |

Task 3 首次 17F 是缺少类型的合同 RED，未将其写成 17 个独立行为故障。EditMode 的销毁 fixture 曾错误期待运行时 OnDestroy；已改为 Clear 验证，真实外部 Slot 杯清理由 Task 5 PlayMode 覆盖。

保留中间失败证据：放杯 port 重入、Walk 导入压缩差异、FBX 骨骼导致杯世界尺寸放大、正常 LayoutChanged 合同回归、停用当帧残留运动通知、enable 后立即 Retry、Working 中销毁、CR 更新先后顺序，以及失败原因被 rebind 清空。最终状态以最后的 focused 及 Phase 全量 XML 为准。

## Phase QA 补充验证

独立 QA 发现三处自动化覆盖缺口：真实员工 1x/2x 与 Carry Pause、实际 Preview Cancel、顾客同场的 Confirm reset。原实现者补充真实场景 tests，未修改产品代码。首轮为 1P/2F，后两项是 fixture 的实际秒等待与 MainCafe gate 时序问题；修正后 `phase-qa-supplement-green.xml` 为 3P，`phase-qa-supplement-regression.xml` 为 18P（Decor 11 + MainCafe 7）。原 QA scoped 复审 PASS，三项全部 CLOSED。

实测工作时间（现实秒）：Cashier 1x / 2x 为 1.9980 / 1.0001，Barista 为 2.9981 / 1.5001。两次约 2m 的 Carry 分别耗时 1.6659 / 0.9183 秒；12 个暂停采样检查身体、骨骼、动画时间、handle、remaining、手杯实例及桌杯计数冻结。实际 MainCafe Confirm 清理 4 个旧顾客 visit，保留同两名员工并实际走约 1.499m 到新 anchor，然后完成新任务。上述数字是该 focused 运行采样，不能替代 Owner 视觉验收。

## 主要文件与用途

| 文件 / 目录 | 用途 |
| --- | --- |
| `Assets/Scripts/Employees/` | binding、单任务状态机、真实导航接线、员工动画、手杯 / 桌面杯及验证 HUD |
| `Assets/Scripts/Navigation/NavigationWalkPresenter.cs` | 仅增加 employee opt-in；既有角色仍使用原 Walk / Hold |
| `Assets/Art/Phase13/`、`ArtSource/Phase13/Tools/ExportEmployeeAnimations.py` | 独立员工素材、Controller / Mask / Socket 与可重跑 exporter；保留原角色及 Cup 资产 |
| `Assets/Editor/Phase13/` | 用 Editor API 生成员工资源、独立 Scene，以及幂等接线 MainCafe |
| `Assets/Editor/Phase8/Phase8Validator.cs`、`Assets/Editor/Phase12/Phase12CustomerQueueSceneSetup.cs` | 精确识别 P13 owned HUD；在临时副本中生成不含员工的 P12 验证 Scene，保留原 MainCafe |
| `Assets/Scenes/Validation/Phase13EmployeeTasks.unity`、`Assets/Scenes/MainCafe.unity` | 独立验证入口与 MainCafe 增量接线，保留既有 P12 / 时间 / Decor / Camera |
| `Assets/Tests/EditMode/Phase13/`、`Assets/Tests/PlayMode/EditorSceneLoading/Phase13/` | 状态 / callback / 杯 / 资源 / 真实导航 / 装修 / 同场 / 真正重复 Play 的自动验证 |
| `Assets/Tests/PlayMode/EditorSceneLoading/Phase8MainCafeSceneTests.cs`、`Phase6DecorationMainCafeSceneTests.cs` | 两项旧 MainCafe baseline 精确接纳第四个 P13 HUD，验证唯一 owner / Flow，保留其他既有断言 |
| `Docs/Phase13_Beginner_Guide.md` | 不写代码即可执行的16项 Owner manual，含恢复步骤 |

实际 PNG 曾发现任务按钮文字空白：Label 的 `anchorMin` 未归零，文字被裁切。已补可信 RED（4 个可见 glyph 预期，实际 0），修正新建及既有 HUD 的双向 stretch；Builder 3P、相关 Play 10P，包含横竖屏文字几何与原时间 / Decor 控件回归。原图保留为 `p13-before-caption-fix-*.png`；最终三张图均经原 Art reviewer 检查，ART-01 CLOSED。

修复后的真实 graphics Editor 截图已采集：`final-capture-caption.xml` 1P / 0F / 0S，包含实际 Camera 与 Overlay Canvas、1080×1920。查看 [working](../outputs/phase13/p13-actual-working.png)、[持杯](../outputs/phase13/p13-actual-carrying.png)、[三杯](../outputs/phase13/p13-actual-three-cups.png)。单帧只证明当时画面，动作连贯、手持观感与完整交互仍由 Owner manual 判断。

## 历史：42cm调整前的Phase自动验证

| 项目 | 状态 |
| --- | --- |
| 完整 EditMode | **PASS：2594P / 0F / 0S**，包含 P13 103 项；[XML](../outputs/phase13/final-edit.xml)。首轮 2582P / 5F 已保留为 `final-edit-first.xml`，不能替代此最终结果 |
| 完整 PlayMode | **PASS：1253P / 0F / 5S**，总1258项，包含 P13 32项全 PASS；[最终 XML](../outputs/phase13/final-play.xml)，CLI exit0、inconclusive0。五项旧 opt-in SKIP 见下表 |
| E/P 需求场景逐项 coverage | 56 个 ID 已逐项映射；三处补测经原 QA 复审 CLOSED，最终 suite 中全部 P13 103 Edit + 32 Play PASS；需求 ID 与 NUnit case 不一一计数 |
| Domain Reload 开/关，各两次真实 Play | PASS：最终 `final-edit.xml` 包含两个真实 session tests，各执行两次真实 Enter/Exit；早期单独 receipt `task7-green-edit-sessions.xml` 的第三项是 Builder 测试，共3P |
| 代表性实际运行截图 | PASS：`final-capture-caption.xml` 1P，实际三张图；之前 batch 无文件、首次 graphics shader 等待后状态变化、旧图 caption 缺陷均保留历史证据 |
| Phase Engineering / QA / Art 技术审查 | 技术 PASS；QA 三个 Important、Art ART-01 已 CLOSED，两个 Editor 兼容修复经独立 Engineering scoped review PASS，两个旧 Play baseline 经原 QA scoped review PASS；0 open findings |

## 全量发现的 Editor 兼容问题

首轮完整 EditMode 的5个失败均在旧 P8 Scene Validator：其固定3个核心 Canvas 的检查尚未识别 P13 自己的 HUD。已仅识别真实 controller 引用的 owned Canvas，仍拒绝无关 / 重复 / 冒名 / 错 owner Canvas 和缺失核心 Canvas。另发现旧 P12 validation Builder 从 MainCafe 直接 Save As 会带入 P13 root，并改变已打开 MainCafe 的 Scene 身份；现改为在 Editor API 临时副本中隔离构建，保留原 MainCafe 对象、路径、激活状态和文件内容。

新增兼容 cases 可信 RED 为 5P / 2F，修复后 7P；定向重跑原失败方法及兼容 cases，`phase-compat-direct-green.xml` 为 15P / 0F / 0S（兼容 7 + P8R 1 + P8 Validator 7），独立 Engineering scoped review PASS。最终完整 EditMode 已 2594P / 0F / 0S，包含全部7项兼容 cases。

完整 EditMode 测试生成的31项旧资产差异已分别保存为 `final-edit-generated-noise.patch`、`final-edit-final-generated-noise.patch` 与 paths 清单，并在 Editor 退出后精确恢复；MainCafe 与 P13 Scene 的最终 hash 均保持不变。旧 P12 Scene 不作为本阶段交付修改。

首轮完整 PlayMode 的两个失败均是旧测试精确要求三个 Canvas，而已批准的 P13 HUD 是第四个 Canvas。修复仅更新这两个测试的精确名称集合，并检查真实唯一 controller / Flow / HUD 引用；保留其余旧断言。产品代码与 Scene 不变。五项 SKIP 是旧阶段明确 opt-in 的截图 / 几何审计，不是 P13 自动 case。

最终完整 PlayMode 已 **1253P / 0F / 5S**，耗时784.38秒，P13 由 Animation5、Decoration11、Flow8、MainCafe8组成。首轮 `final-play-first.xml` 的1251P / 2F / 5S保留为历史失败证据，不与最终结果相加。完整 EditMode 耗时1836.99秒，2594P / 0F / 0S，两个 suite 的 inconclusive 均0。

两项旧 baseline 加全部8项 P13 MainCafe focused 验证为 **10P / 0F / 0S**，见 [XML](../outputs/phase13/phase-canvas-baseline-green.xml)。此前完整 EditMode 的产品、Editor 源码与资源未改；两份 PlayMode 测试的变化由最终完整 PlayMode 重跑覆盖。

最终冻结的产品、资产、Scene 与测试81个文件记录在 [SHA-256 manifest](../outputs/phase13/final-source-manifest.json)。独立 Engineering / QA / Art 及每轮 scoped 修复报告保存在同一 worktree 的 `.superpowers/sdd/2026-10-13-phase-13-employee-task-flow/`；报告只对应所列源码快照，Owner manual 结果另记。

最终完整 PlayMode 明确跳过的旧 opt-in 项目如下；已核对与首轮为同一五项：

| Case（省略 namespace） | SKIP 原因 |
| --- | --- |
| `P8RCashRegisterSideIndicatorTests.CashIndicator_NativeOverlayCapture_WhenExplicitlyEnabled_WritesUniqueReviewGallery` | 需显式设置 `ANIMALCAFE_CASH_SIDE_CAPTURE=1` |
| `P8RCompactChromeTests.SpacingAudit_WhenRequested_ExportGeometry` | opt-in spacing geometry audit |
| `P8RReadinessSafeAreaTests.ReadinessChecklist_CaptureNativeExamplesWhenRequested` | opt-in 原生 Game View 截图 |
| `P8RUiEnhancementSceneTests.NativeUiExamples_WhenRequested_CaptureActualGameView` | opt-in 实际 Editor Game View 截图 |
| `Phase11NavigationSceneTests.CaptureRepresentativeCanvasAndCamera` | 需 graphics Editor 和 `ANIMALCAFE_P11_CAPTURE=1` |

P13 自己的代表性实际截图已单独执行 PASS；这不把上述五项 SKIP 改写为 PASS。

最终 Editor 已退出，仅 Unity CLI auth broker 留在运行。完整 PlayMode 两轮产生的一份旧 Phase7 material 浮点 / 空白差异分别保存为 `final-play-material-noise.patch`、`final-play-final-material-noise.patch`，随后仅恢复该文件。最终81个 manifest 文件 hash 全部匹配，MainCafe / P13 Scene 均未被测试改变；ProjectSettings、Packages 与原 P11 / P12 validation Scene 无交付修改。代码 / Markdown 的 `git diff --check` 通过；Unity 生成 Scene 原有空字符串行的空白未手工改写。

## 本轮委托的10项场景代测

Owner 于2026-10-14明确委托 M-005～008、M-010～015。以下是新一轮真实 Windows graphics Editor 场景 API 操作与跨帧检查，不是物理鼠标点击，不以此前自动测试的 PASS 代填。M-015 另用实际 EnterPlayMode / ExitPlayMode 执行。

| ID | 结果 | 实际执行与观察 | 有效证据 |
| --- | --- | --- | --- |
| M-005 | **PASS（Codex 场景代测）** | 两角色1×/2×；收银2.001/1.002秒，制作3.005/1.505秒，送杯1.835/0.924秒。working与carrying各暂停2.054现实秒，身体位置/朝向、骨骼、handle、剩余时间、手杯/桌杯冻结；暂停点击被拒绝，恢复后完成 | D1 |
| M-006 | **PASS（Codex 场景代测）** | working/carrying × 直接退出Decor/旋转Preview后Cancel共4分支；保留原handle、remaining、手杯/桌杯，再完成原任务 | D1 |
| M-007 | **PASS（Codex 场景代测）** | working中两次CM旋转Confirm；carrying中实际移动CM支撑桌及已有杯的Pick-up支撑桌。旧未交付杯清理、原员工实际回位；已交付同一个杯instance随原module移动，局部坐标保持 | D1 |
| M-008 | **PASS（Codex 场景代测）** | 分别Store CM/Pick-up→补回→Retry→▶→原员工实际回位并新交付；另在(4,4)实际Confirm柜台堵路，显示Blocked，Store障碍后同样恢复并交付。未跳过任何分支 | D3两case |
| M-010 | **PASS（Codex 场景代测，runtime/Console技术范围）** | 三轮共17条Application runtime audit均0 Error/Assert/Exception、0 Warning，清理/退出也在监测内；M-015 Editor日志无异常匹配。3个graphics启动日志各保留1条既有Licensing诊断，见下文，不声称全部原生日志零Error | D1/D2/D3/D4及log audit |
| M-011 | **PASS（Codex 场景代测）** | 两员工working各连点3次；Barista carrying/returning各连点3次，handle不变、Busy可见；同帧核对Cashier handle/Outcome不受另一岗位点击影响，最后两者独立成功 | D2 |
| M-012 | **PASS（Codex 场景代测）** | 在(1,6)/(3,6)/(5,6)实际Confirm第二组三设备；记录真实完整ID与不可变binding；逐一Store原CM/Pick-up/CR，实际回位并成功新任务，Cashier与P12所选CR一致 | D3 |
| M-013 | **PASS（Codex 场景代测）** | Cashier working、Barista working/carrying/returning各调用ContextMenu取消两次。旧handle立即退休；交付前无新增杯，交付后已放杯和Succeeded保留；原员工实际回位，不复活旧任务 | D1 |
| M-014 | **PASS（Codex 场景代测）** | 分别停用Barista GameObject和EmployeeFlowController至少1.05现实秒；原位、原实例/ID、World注册保留，旧手杯清理。两轮均用重叠新角色实际Register=false验证身体保护，再启用并实际回位 | D2 |
| M-015 | **PASS（Codex 场景代测）** | Domain Reload开/关各两次真实Play/Stop，共4次；每次制作一杯、新session、仅两员工/一套HUD/初始0杯，旧actor在Exit后释放。Editor原设置False/None及原Scene setup已恢复 | D4两case |

有效证据按源码快照组合，不能把带失败的早期整份XML写成全PASS：

- **D1**：[首轮场景XML](../outputs/phase13/delegated-first-play.xml)，5P / 4F / 0S；采用其中未被后续修改的M-005/006/007/013四项PASS。首轮M-014由D2补强结果替代。
- **D2**：[定向复跑1 XML](../outputs/phase13/delegated-repair1-play.xml)，2P / 3F / 0S；采用M-011/014的PASS，其他三项由D3替代。
- **D3**：[定向复跑2 XML](../outputs/phase13/delegated-repair2-play.xml)，3P / 0F / 0S；M-008两个case及M-012全部通过。
- **D4**：[本轮真实Play session XML](../outputs/phase13/delegated-m015-edit.xml)，2P / 0F / 0S；四条`REAL_PLAY_SESSION`及两条`EDITOR_RESTORED`均已核对。

首轮与复跑1失败均已定位为操作脚本时序/反馈断言问题并保留证据：修复后漏恢复时间、同帧读旧Idle、最后一次有效点击覆盖Busy，以及▶后再次Retry使导航重验证重新暂停。修正只涉及验证脚本；旧PASS四项及其helpers未改，另外两项补强后定向重跑，最后三项按真实恢复顺序通过。**指南已明确Retry后检查Pause并点▶/1×**，无需改变游戏规则。

新增纯验证文件为`Assets/Tests/PlayMode/EditorSceneLoading/Phase13/EmployeeDelegatedSceneChecks.cs`及Unity生成meta；最终SHA256为`1C8240E933D61C224A3E4DC968F0F72FF651E500E9A9913BE8997A49139422C6`。原81个冻结产品/资源/旧测试文件全部hash匹配，Scene、Packages、ProjectSettings未改；此前完整suite是新增代测脚本前的产品验证快照，本轮仅执行上述场景及session检查，不为新增验证脚本/文档重复全suite。

实际画面：[送杯暂停](../outputs/phase13/delegated-first-m005-paused-carry.png)、[实际Confirm堵路](../outputs/phase13/delegated-repair2-m008-confirmed-block.png)、[第二组三设备](../outputs/phase13/delegated-repair2-m012-second-set.png)。截图仅辅助技术状态核对，不能替Owner接受动画或手持观感。

[日志审计](../outputs/phase13/delegated-log-audit.txt)保留全部17条runtime audit。三个graphics原生日志各有既存启动诊断`Licensing::Module Error: Access token is unavailable; failed to update`，此前`final-capture-caption.log`等也有同项；未执行登录/激活，Editor仍正常运行及退出。NUnit操作脚本断言失败另列在D1/D2，不能据runtime audit=0隐藏它们。[退出检查](../outputs/phase13/delegated-exit-check.json)和root独立进程核对均确认Editor=0，仅CLI auth broker；原Editor设置和Scene已恢复。

## Owner 与其他边界

2026-10-14 Owner 委托的 **M-005～008、M-010～015共10项已PASS（Codex场景代测）**。随后Owner明确确认 **M-001～004及M-006均PASS**（M-006为Owner复验）；**M-009、M-016现已由Owner明确确认手测PASS**。代测不声称实际鼠标点击，也不把旧自动测试receipt改名作为新证据。

Owner另提出working手部低于台面的视觉问题。最初72cm柜台与抬手姿态仅做Blender静态对比，未改正式动画；随后Owner比较降低20/30cm版本，选择降低30cm，并明确授权在P13 branch中试调与充分回归。当前目标台面/Slot/Collider为42cm；保留原working动画、角色与设备尺寸。已确认的人工PASS作为当时快照保留，新比例的Unity视觉接受单独记录，不由旧PASS或静态预览替代。见下方新增回归。

working、手持姿势、杯子摆放和实际操作手感仍由 Owner 判断。[Beginner Guide](Phase13_Beginner_Guide.md) 已提供实际菜单、Inspector helper 和布置步骤。

Android/iOS Player 与真机：**NOT_RUN**。Cloud CI、commit、push、PR、merge：**NOT_RUN**。本计划未授权这些操作。

M-009/M-016的Owner手测验收已关闭；本地最终review见文末。未执行Git集成，不自动进入P14。

## 附录：逐项自动覆盖

以下保留独立 QA focused / scoped 复审快照；最终完整 suite 的实际结果以上表为准。Art 后续只修正按钮 Label 并追加可见文字测试，QA 已核的三个行为分支未改。

**附录中的“current final NOT_RUN / pending”和16项manual NOT_RUN均是委托前QA的历史快照标注。最终自动与本轮10项委托代测状态见上方；Owner已确认M-001～004及M-006 PASS，M-009/M-016最新Owner手测PASS见正文。**

2026-10-14. Companion: `phase-qa-review.md`. **38 E + 18 P = 56 scenario IDs**, not test count. Every ID is mapped below. Initial audit found 5 IDs with mandatory branches missing, grouped as QA-01..03. **Scoped fix rereview now CLOSED all three findings**: see `phase-qa-fix-review.md`. E-011/E-030/E-031/P-007/P-010 rows below are updated to the current two-test-file snapshot; the original missing-branch findings remain preserved as historical in `phase-qa-review.md`. Other initial-audit rows are retained without a second whole-phase review. Focused PASS means the named methods were found Passed in the cited XML snapshot; it is not full aggregate acceptance. Final aggregate suites remain **NOT_RUN/pending at scoped rereview**, and all16 Owner manual remain NOT_RUN.

### Legend and exact test namespaces

- `B` = `AnimalCafe.Tests.EditMode.Phase13.EmployeeBindingTests`: actual layout evaluator + injected path predicate, no NavigationWorld movement.
- `R` = `AnimalCafe.Tests.EditMode.Phase13.EmployeeTaskRunnerTests`: actual runner with `EmployeeTaskEnvironmentFake` ports, synthetic callbacks/delta, no real movement/cup.
- `L` = `AnimalCafe.Tests.EditMode.Phase13.EmployeeLifecycleTests`: same fake-port runner tests.
- `D` = `AnimalCafe.Tests.EditMode.Phase13.PickupCupDisplayTests`: actual GameObjects/Cup prefab + supplied confirmed-slot resolver, no real navigation.
- `A` = `AnimalCafe.Tests.EditMode.Phase13.EmployeeAssetTests`: real imported assets, static assertions and sampled clips.
- `S` = `AnimalCafe.Tests.EditMode.Phase13.EmployeePlaySessionTests`: actual Editor Play transitions, despite EditMode runner/XML.
- `G` = `AnimalCafe.Tests.EditMode.Phase13.Phase13SceneBuilderTests`: actual Editor Scene builder APIs.
- `V` = `AnimalCafe.Tests.PlayMode.Phase13.EmployeeAnimationTests`: real Animator/rig; directly supplied motion, no real NavigationWorld displacement.
- `F` = `AnimalCafe.Tests.PlayMode.Phase13.EmployeeFlowTests`: real P12 validation Scene/layout/NavMesh, P13 controller installed by fixture; actual NavigationWorld movement.
- `C` = `AnimalCafe.Tests.PlayMode.Phase13.EmployeeDecorationTests`: same real Scene/navigation, plus actual Decoration APIs; no Customers spawned in these fixtures.
- `I` = `AnimalCafe.Tests.PlayMode.Phase13.EmployeeMainCafeTests`: actual saved P13 validation Scene or actual MainCafe as explicitly named in each row.
- `N` = `AnimalCafe.Tests.PlayMode.Phase11.NavigationAnimationTests`: real default P11 actors/navigation fixture.

Receipt shorthand, all in `outputs/phase13/`, all listed snapshots 0 Failed / 0 Skipped:

| Key | XML | Whole receipt counts; do not sum overlapping suites |
| --- | --- | --- |
| b1 | task1-green.xml | 24 Passed |
| r2 | task2-fix1-green.xml | 62 Passed including B/R |
| d3 | task3-green.xml | 19 Passed |
| a4 | task4-asset-green.xml | 5 Passed |
| v4 | task4-animation-green.xml | 5 Passed |
| n4 | task4-regression-play.xml | 9 Passed including N |
| f5 | task5-green-final.xml | 8 Passed |
| c6 | task6-final-verified-play.xml | 32 Passed; P13 F/C named methods plus direct regression |
| l6 | task6-fix1-green.xml | 67 Passed; current B/R/L, including FailedReasonSurvivesRebindAndActualRecoveryUntilNextStart |
| s7 | task7-final-edit.xml | 9 Passed; current S/G and related tests |
| i7 | task7-fix1-final-play.xml | 5 Passed; includes actual MainCafe four-Customer method |
| qfix | phase-qa-supplement-regression.xml | 18 Passed / 0 Failed / 0 Skipped; Decor11 + Main7; both frozen changed fixtures |
| qgreen | phase-qa-supplement-green.xml | 3 Passed / 0 Failed / 0 Skipped; exact three finding methods |

Root owns sole runner. Reviewer ran no Unity tests. The 97-Passed task7-green-direct-play.xml predates latest test/builder/capture adjustments; it is historical direct regression, not current final suite. QA-01..03 are now closed by scoped rereview. Root may append final aggregate XML counts after serial execution.

### EditMode requirement scenarios

| ID | Concrete class.method(s) | What assertions prove / boundaries | Receipt and execution status |
| --- | --- | --- | --- |
| E-001 | F.CreatesTwoUniqueEmployeesAtSafeWorkAnchors; C.ConfirmedEditRetiresOldTaskAndRepeatedConfirmKeepsEmployeeIdentity; R.ConfiguredDurationAndIndependentEmployeesConsumeScaledDeltaOnce | Real two unique IDs/roles/register-only identity, immutable snapshots, stable bodies/IDs across Confirm; independence unit ports. IDs are employee-prefixed, distinct from Customers; real co-run in I also uses six distinct actors. | Focused PASS f5/c6/l6; current final NOT_RUN |
| E-002 | B.SelectedRegisterWinsRegardlessOfEnumerationOrder; R.CashierCompletesAfterExactlyTwoGameSeconds; F.CreatesTwoUniqueEmployeesAtSafeWorkAnchors; I.MainCafeStaffAndFourCustomersShareSelectedRegister | CR choice/employee target, exact game seconds with fake delta, real success/Idle, actual MainCafe same CR. Actual real elapsed-time branch now covered by I.RealGameTimeScalesWorkCarryAndFreezesCarrier (qfix; E-011). | Focused PASS b1/l6/f5/i7; current final NOT_RUN |
| E-003 | R.BaristaRequiresPickupArrivedThenActualReturn; F.RealBaristaPlacesCupOnlyAfterPickupArrived; F.FiveRealDeliveriesKeepThreeCupsAndReturnIdle | 3 game seconds at unit boundary, actual held cup, real pickup arrival/placement, Returning busy, real CM final pose/Idle. | Focused PASS l6/f5/c6; current final NOT_RUN |
| E-004 | R.BusyEmployeeRejectsSecondHandle; R.BaristaRequiresPickupArrivedThenActualReturn; F.RealBaristaPlacesCupOnlyAfterPickupArrived; I.HudControlsRemainClickableAcrossAspectRatios | Second start/binding rejected, handle stable; Returning rejected, no task queue; real busy button feedback. | Focused PASS l6/f5/i7; current final NOT_RUN |
| E-005 | R.ConfiguredDurationAndIndependentEmployeesConsumeScaledDeltaOnce; C.DestroyedActorRetiresOnlyItsOwnedTask; I.MainCafeStaffAndFourCustomersShareSelectedRegister | Fake independent progress/cancel, actual destroyed Barista does not remove Cashier, both actual MainCafe tasks run with Customers. | Focused PASS l6/c6/i7; current final NOT_RUN |
| E-006 | B.SelectedRegisterWinsRegardlessOfEnumerationOrder; B.EmptySelectedRegisterUsesP12OrdinalChoice; B.BaristaChoosesFirstReachablePair; B.BaristaSkipsCoffeeMachineWhenNoPickupHasCompleteRoundTrip; C.CashierWaitsForP12NewRegisterWhenEmployeeStepsFirst | Reversed enumeration, stable chosen pair/round trip, no stale P12-selected CR use after ordering changes. | Focused PASS b1/l6/c6; current final NOT_RUN |
| E-007 | B.MissingOrInvalidSelectedRegisterNeverFallsBackToAnotherRegister; B.InvalidStationIsSkippedEvenWhenPathQueryAcceptsItsPosition; B.ReturnPathMustAlsoBeComplete; B.BindingRejectsIncompleteRoleContract; B.InvalidResolverInputReturnsExplicitFailure | Explicit missing/invalid station/anchor/input failures and complete route prerequisite; path predicate injected. Actual movement failure is F.BlockedCarrierStopsWithoutLosingCup. | Focused PASS b1/l6/f5; current final NOT_RUN |
| E-008 | R.LayoutInvalidationRetiresOldCallbacksAndRequiresFreshBinding; C.MissingStationIsReadableAndRepairReturnsActualBody; F.RemovedFormalPickupCannotReceiveCupThroughStaleRepresentation | No stale target success, retired handle, no alternate station switch, actual station failure/repair. | Focused PASS l6/c6/f5; current final NOT_RUN |
| E-009 | R.CashierCompletesAfterExactlyTwoGameSeconds; R.BaristaRequiresPickupArrivedThenActualReturn; R.AllSynchronousLegsCompleteOnlyOnce | Segmented 1.99+.01 and 2.99+.01, success once, large later delta no second cleanup/task, duplicate callback no double delivery. | Focused PASS l6; fake delta/ports; current final NOT_RUN |
| E-010 | R.PauseFreezesWorkAndRejectsNewStart; R.PausedArrivalDefersDelivery; R.PausedArrivalCancelledBeforeResumeCannotPlaceCup; F.RealBaristaPlacesCupOnlyAfterPickupArrived | Gate/zero delta preserves time; deferred delivery not released by zero delta, once only when allowed; actual work Pause and start rejection. | Focused PASS l6/f5; carrying real Pause integration now Passed qfix (QA-01 CLOSED); current final NOT_RUN |
| E-011 | I.RealGameTimeScalesWorkCarryAndFreezesCarrier; R.ConfiguredDurationAndIndependentEmployeesConsumeScaledDeltaOnce; V.PauseAndFastScaleWorkingExactlyOnce | Actual saved P13 Scene production Update/GameTime uses captureDeltaTime0 and unscaled real frame measurement. Cashier Normal/Fast1.9980/1.0001s, Barista2.9980/1.5001s; asserts 2/1 and3/1.5 real seconds with bounded frame tolerance. No injected flow delta or fake timer. Initial split-only evidence gap QA-01 is now closed. | **QA-01 CLOSED / focused PASS qgreen/qfix**; current full aggregate pending |
| E-012 | R.InvalidDeltaThrowsWithoutChangingState; R.InvalidDurationCannotCreateRunner | Negative/NaN/+Infinity delta and zero/negative/NaN/+Infinity duration rejected, unchanged snapshot. | Focused PASS l6; current final NOT_RUN |
| E-013 | R.SynchronousCompletionCannotOverwriteNextLeg; R.AllSynchronousLegsCompleteOnlyOnce; R.SynchronousMoveRejectionBlocksWithoutClaimingArrival; R.RecoveredIsNotDelivered | Synchronous success/failed/rejected paths cannot overwrite next leg/claim arrival; one cleanup. | Focused PASS l6; synthetic callback environment; current final NOT_RUN |
| E-014 | R.LayoutInvalidationRetiresOldCallbacksAndRequiresFreshBinding; R.StaleSessionCannotPlaceCup; R.WrongRequestOrActorCannotCompleteDelivery; L.RepeatedRevisionInvalidationCleansOldHandleOnceAndRejectsOldCallback | Actual runner ignores retired generation/old request/actor/revision callbacks; none mutates current task or places cup. StaleSession name here actually exercises handle/generation within one runner; see E-033 for real session composition. | Focused PASS l6; current final NOT_RUN |
| E-015 | R.RecoveredIsNotDelivered; R.WrongRequestOrActorCannotCompleteDelivery; F.BlockedCarrierStopsWithoutLosingCup | Recovered/Failed/Cancelled/LayoutChanged synthetic terminal callbacks do not deliver; actual bounded obstruction cannot deliver. Intermediate navigation waypoints are internal P11 movement, not Employee port Arrived; this is composed with shared navigation regression, not a fake intermediate actor movement claim. | Focused PASS l6/f5/c6; current final NOT_RUN |
| E-016 | R.ReentrantCancelCleansOnce; R.DisposeRetiresTaskAndRecoveryCannotRestart; R.AllSynchronousLegsCompleteOnlyOnce | Repeat cancel/dispose and stale duplicate callbacks, one cleanup/cancel/placement; retired disabled cannot restart. | Focused PASS l6; current final NOT_RUN |
| E-017 | R.ReentrantCancelCleansOnce; R.ConfiguredDurationAndIndependentEmployeesConsumeScaledDeltaOnce; C.FlowDisableCancelsOwnedTaskAndReenableReturnsSameBody; C.MissingStationIsReadableAndRepairReturnsActualBody | Cancel work/carry ownership at unit boundary, actual cancellation/hand cleanup and bounded physical return or station Blocked. Explicit manual player Cancel across phases remains M-013 NOT_RUN. | Focused PASS l6/c6; current final NOT_RUN |
| E-018 | R.ReturnFailurePreservesDeliveredOutcome; R.SynchronousCompletionCannotOverwriteNextLeg; C.DisableDoesNotRevokeDeliveredTableCup | Failed/Recovered return is Blocked while delivered outcome/table cup remains; cancel during Returning does not revoke success. | Focused PASS l6/c6; current final NOT_RUN |
| E-019 | R.ReturnFailurePreservesDeliveredOutcome; L.RepeatedSameRevisionSuspensionHasBoundedAutomaticResume; L.FailedReasonSurvivesRebindAndActualRecoveryUntilNextStart; C.MissingStationIsReadableAndRepairReturnsActualBody | Explicit retry only, finite same-revision resume, failure reason persists until Start; actual original body returns from unchanged position to valid target. | Focused PASS l6/c6; current final NOT_RUN |
| E-020 | R.PauseFreezesWorkAndRejectsNewStart; R.LayoutInvalidationRetiresOldCallbacksAndRequiresFreshBinding; I.HudControlsRemainClickableAcrossAspectRatios; C.MissingStationIsReadableAndRepairReturnsActualBody | Reject paused, busy, missing binding/station; actual HUD Paused/Busy feedback, Decor HUD hidden. No formal production dispatch system added. | Focused PASS l6/i7/c6; current final NOT_RUN |
| E-021 | D.TenDeliveriesShowOnlyThreeCups; F.FiveRealDeliveriesKeepThreeCupsAndReturnIdle | Exact visible/object counts 1/2/3/3..10; actual five real cycles delivery and return with only three cups. | Focused PASS d3/f5/c6; current final NOT_RUN |
| E-022 | D.ThreeOffsetsAreTriangular; I.RotatedMultiSlotTableKeepsCupModule | Non-collinear actual three offsets and exact triangle, three real deliveries to selected module. | Focused PASS d3/i7; current final NOT_RUN; Owner shape/readability M-004 NOT_RUN |
| E-023 | D.DifferentModulesDoNotShareCups; I.RotatedMultiSlotTableKeepsCupModule | Separate addresses/supports own counts, no cup in neighboring slot after three actual deliveries; no extra pickup synthesized. | Focused PASS d3/i7; current final NOT_RUN |
| E-024 | R.RecoveredIsNotDelivered; R.FailedVisualCannotCommitSuccess; F.BlockedCarrierStopsWithoutLosingCup; F.RemovedFormalPickupCannotReceiveCupThroughStaleRepresentation | Failed/not-arrived real movement leaves zero display/delivered; false visual result cannot commit. | Focused PASS l6/f5/c6; current final NOT_RUN |
| E-025 | D.TenDeliveriesShowOnlyThreeCups; D.RuntimeVisualCannotRegisterBusinessOrBlockNavigation; F.FiveRealDeliveriesKeepThreeCupsAndReturnIdle | Fourth/fifth actual tasks complete, Cup has no business/navigation component/collider effect. P10 capacity not directly recounted in this employee delivery test; actual Cup assets register no capacity entry and P12 direct capacity tests remain separate. | Focused PASS d3/f5/c6; current final NOT_RUN |
| E-026 | C.UnchangedDecorationResumesSameTask; C.UnchangedDecorationPreservesCarrierAndRequest; L.SameRevisionSuspensionRetainsHandleCupAndResubmitsOnlyOnceWhenReady | No-Confirm open/close keeps same handle/remaining or held cup, actual delivery resumes. Explicit Preview Cancel is now exercised by this strengthened method (qfix; E-030); original c6 snapshot covered open/close only. | Focused PASS c6/l6; current final NOT_RUN |
| E-027 | C.ConfirmedEditRetiresOldTaskAndRepeatedConfirmKeepsEmployeeIdentity; R.LayoutInvalidationRetiresOldCallbacksAndRequiresFreshBinding; L.RepeatedRevisionInvalidationCleansOldHandleOnceAndRejectsOldCallback | Repeated actual Confirm retires task/hand, same actors/IDs, no teleport on rebind, actual final work pose; fake old callback clean once. | Focused PASS c6/l6; current final NOT_RUN |
| E-028 | D.MovedSupportKeepsLocalPlacement; D.RefreshReparentsToReplacementAndCleansOnlyInvalidModule; I.RotatedMultiSlotTableKeepsCupModule | Actual Cup hierarchy follows moved/rotated support/slot and replacement, rotated grid integration gives correct selected module. I test rigidly rotates initial whole fixture; post-delivery furniture Confirm move/rotate is composed with D and manual M-007/M-016, not claimed as run in I. | Focused PASS d3/i7; current final NOT_RUN |
| E-029 | D.DestroyedSupportIsRemovedOnRefresh; D.RefreshReparentsToReplacementAndCleansOnlyInvalidModule; D.SaturatedDisplayStillValidatesConfirmedSlot; C.RemovedPickupRejectsStaleSceneObjectAndRefreshesOwnedCup | Specific invalid module cleans own cups; other address preserved; stale actual representation cannot accept formal removal; employee start rejected. | Focused PASS d3/c6; current final NOT_RUN |
| E-030 | C.UnchangedDecorationResumesSameTask (strengthened); C.UnchangedDecorationPreservesCarrierAndRequest | Actual legal changed CM rotate Preview is explicitly Cancelled; formal address/rotation/revision/completed version/binding identity/handle/remaining and pre-existing cup object/parent persist. Done resumes same task to another real delivery and actual CM return. Initial no-preview gap QA-02 is now closed. | **QA-02 CLOSED / focused PASS qgreen/qfix**; current full aggregate pending |
| E-031 | I.MainCafeConfirmedDecorResetsFourCustomersAndPreservesActiveStaff; C.ConfirmedEditRetiresOldTaskAndRepeatedConfirmKeepsEmployeeIdentity | Actual MainCafe4Customers+2employees, active Cashier/carrying Barista, real legal Confirm and Done; old Customer visits/actors removed while same employee objects/IDs/roles/proxies/dimensions remain. Old hand/task retired, no stale table cup, actual return to changed anchor, fresh task delivers. Initial composed-only reset gap QA-03 is now closed. | **QA-03 CLOSED / focused PASS qgreen/qfix**; current full aggregate pending |
| E-032 | R.DisposeRetiresTaskAndRecoveryCannotRestart; C.FlowDisableCancelsOwnedTaskAndReenableReturnsSameBody; C.DisabledBodyCannotBeBypassed; C.DestroyedActorRetiresOnlyItsOwnedTask; C.DisableDoesNotRevokeDeliveredTableCup | Old task retired, actual same-body recovery, disabled body still rejects overlap registration, other employee survives destroy, placed cup retained. | Focused PASS l6/c6; current final NOT_RUN |
| E-033 | R.StaleSessionCannotPlaceCup; R.DisposeRetiresTaskAndRecoveryCannotRestart; S.RepeatedPlayStartsOneFreshSessionWithDomainReload; S.RepeatedPlayStartsOneFreshSessionWithoutDomainReload | Unit retired-handle and disposed-runner callbacks plus real fresh sessions and exit cleanup. S exits with real return navigation in flight after delivery. No claim that R's misleading method name itself creates a new controller session, or that S explicitly injects old callback into new session. | Focused PASS l6/s7; composed session safety evidence; current final NOT_RUN |
| E-034 | D.FailedInstantiationDoesNotCommitCount; D.InvalidVisualRollsBackTemporaryClone; D.MissingConfigurationAndDefaultAddressFailClearly; R.FailedVisualCannotCommitSuccess; R.FailedHeldVisualCannotStartDelivery; F.MissingCupConfigurationCannotReportDeliverySuccess | Actual display component tests first/second/third failure atomic count/clone rollback, fake runner false visual means no success; actual Scene null Cup fail and Cashier independence. Visual-create failure is tested in D rather than induced after a real Navigation arrival. | Focused PASS d3/l6/f5/c6; split component/integration proof; current final NOT_RUN |
| E-035 | R.ReentrantCancelCleansOnce; R.ReentrantDisposeDuringPlacementCannotReviveTask; R.ReentrantLayoutChangeDuringPlacementCannotUseClearedBinding; R.ReentrantCancelDuringPlacementCannotOverrideRecoveryOrCommitSuccess | Cleanup/cancel/placement reentry retires before outside code; both true/false placement results covered; no new overlapping task/stale return/success. | Focused PASS l6; current final NOT_RUN |
| E-036 | R.PausedArrivalDefersDelivery; R.PausedArrivalCancelledBeforeResumeCannotPlaceCup; L.SameRevisionSuspensionRetainsHandleCupAndResubmitsOnlyOnceWhenReady | Unexpected synthetic Arrived waits for positive allowed game delta and resumes once; cancelled pending arrival never delivers. Real pause-carrier integration now Passed qfix (QA-01 CLOSED). | Focused PASS l6; current final NOT_RUN |
| E-037 | B.DeletedPickupRepresentationCannotKeepBindingAlive; D.SaturatedDisplayStillValidatesConfirmedSlot; F.RemovedFormalPickupCannotReceiveCupThroughStaleRepresentation; C.RemovedPickupRejectsStaleSceneObjectAndRefreshesOwnedCup | Formal Layout/ID/Slot membership overrides live old Scene object, including saturation; actual flow refuses stale delivery and clears invalid display. | Focused PASS b1/d3/f5/c6; current final NOT_RUN |
| E-038 | I.EmptyCafeRemainsEditableUntilStationsAreReady; G.MainCafeInstallPreservesExistingWiringAndAutoSpawn | Actual saved MainCafe empty/editable/no actors, seed valid -> two staff, delete CM -> existing gate blocks tasks but Decor remains available and identities persist; builder leaves old components intact. | Focused PASS i7/s7; current final NOT_RUN |

### PlayMode requirement scenarios

| ID | Concrete class.method(s) | Scene/evidence and limits | Receipt and execution status |
| --- | --- | --- | --- |
| P-001 | F.CreatesTwoUniqueEmployeesAtSafeWorkAnchors; C.DisabledBodyCannotBeBypassed; I.MainCafeStaffAndFourCustomersShareSelectedRegister | Actual safe anchors/facing/walkability, unique stable actor IDs and separation; actual overlap registration rejection protects body. No special weakened physics fixture. Initial unsafe spawn refusal also source-reviewed; Owner framing/species look M-001 pending. | Focused PASS f5/c6/i7; current final NOT_RUN |
| P-002 | V.WorkingCannotBeOverwrittenByMotionZero; V.BothSpeciesCarryKeepsUpperPoseAndRealWalkWithOwnedCup; A.EmployeeAssetsContainWorkAndCarrySocket; R.CashierCompletesAfterExactlyTwoGameSeconds; R.BaristaRequiresPickupArrivedThenActualReturn | Real Animator Working ownership and animated imported work loop; durations exact at runner game-time boundary. V fixture with synthetic controller explicitly not imported animation evidence; A and second V cover real assets. Whole-look Owner M-002 pending. | Focused PASS v4/a4/l6; actual real-time branch now Passed qfix (QA-01 CLOSED) |
| P-003 | V.BothSpeciesCarryKeepsUpperPoseAndRealWalkWithOwnedCup; F.RealBaristaPlacesCupOnlyAfterPickupArrived; A.CarryMaskExcludesRootPelvisAndLegs | V compares real imported upper Carry pose/animated leg/cup socket while supplying motion; F proves actual NavigationWorld displacement/held visual. Combined proof, not a claim V moves an actor. | Focused PASS v4/f5/a4/c6; current final NOT_RUN; Owner M-003 NOT_RUN |
| P-004 | F.BlockedCarrierStopsWithoutLosingCup | Actual stationary blocker at delivery, >=.909 body distance, repeated stopped frames select Hold lower layer with Carry weight one, held cup retained until finite termination, no delivered/table cup. | Focused PASS f5/c6; current final NOT_RUN |
| P-005 | F.RealBaristaPlacesCupOnlyAfterPickupArrived; F.FiveRealDeliveriesKeepThreeCupsAndReturnIdle; I.MainCafeStaffAndFourCustomersShareSelectedRegister | Actual correct pickup arrival, hand gone/table count increases, Returning busy, actual CM pose then Idle; real normal-walk return composed with V layer-off assertions. | Focused PASS f5/c6/i7; current final NOT_RUN |
| P-006 | F.FiveRealDeliveriesKeepThreeCupsAndReturnIdle; D.TenDeliveriesShowOnlyThreeCups | Five actual work/deliver/return cycles; exactly three scene cup children; 10 component deliveries no hidden overflow. | Focused PASS f5/c6/d3; current final NOT_RUN |
| P-007 | I.RealGameTimeScalesWorkCarryAndFreezesCarrier; V.PauseAndFastScaleWorkingExactlyOnce; F.RealBaristaPlacesCupOnlyAfterPickupArrived | Actual production Scene/time/work and NavigationWorld carrier integrated: both1x/2x work durations, comparable >1m real route, carry elapsed ratio.5511 and distance ratio.9994, rejects4x scaling. Pause checks12 actual Update frames after current-frame drain: position/facing/handle/time/held cup/table count/Animator and rig transforms frozen, then resume/deliver once/actual return. Initial QA-01 missing integration is closed; this is technical sampling, not Owner visual or long soak acceptance. | **QA-01 CLOSED / focused PASS qgreen/qfix**; current full aggregate pending |
| P-008 | C.MissingStationIsReadableAndRepairReturnsActualBody; C.ConfirmedEditRetiresOldTaskAndRepeatedConfirmKeepsEmployeeIdentity; F.RemovedFormalPickupCannotReceiveCupThroughStaleRepresentation; R.RecoveredIsNotDelivered | Actual station deletion/revision cancellation and stale object no delivery; terminal Recovered meaning tested with fake callback, actual obstruction additionally F.BlockedCarrierStopsWithoutLosingCup. | Focused PASS c6/f5/l6; current final NOT_RUN |
| P-009 | I.MainCafeStaffAndFourCustomersShareSelectedRegister; I.StaffAndCustomersShareSelectedRegister | First actually loads MainCafe; second actual validation Scene. Four real admissions + two employees, shared CR/opposite sides, per-frame actor separation, controlled FIFO leave, both tasks/delivered/actual CM return. Existing P12 admission/Total/continuous solid guards remain distinct direct-regression contracts; no claim the helper itself asserts every ledger. Original assigned real-MainCafe coverage finding CLOSED by exact new test. | **Focused PASS i7 (5/0/0)**; original task-7-fix1-review confirms closure; current final NOT_RUN |
| P-010 | I.MainCafeConfirmedDecorResetsFourCustomersAndPreservesActiveStaff; C.UnchangedDecorationResumesSameTask; C.UnchangedDecorationPreservesCarrierAndRequest; C.ConfirmedEditRetiresOldTaskAndRepeatedConfirmKeepsEmployeeIdentity; C.MissingStationIsReadableAndRepairReturnsActualBody | Real no-change/explicit Preview Cancel/Confirm/delete navigation preserved. New actual MainCafe Confirm resets4Customers only after Done while preserving same2employee objects/IDs/roles/body parameters; retires old hand/task with no stale delivery, real1.499m return to changed CM anchor and a new successful task. Initial QA-03 integration gap now closed; QA-02 also closed. | **QA-03 CLOSED / focused PASS qgreen/qfix**, existing branches Passed c6/qfix; current full aggregate pending |
| P-011 | D.RuntimeVisualCannotRegisterBusinessOrBlockNavigation; D.UnsupportedCupCannotActivateBusinessOrNavigationComponents; V.BothSpeciesCarryKeepsUpperPoseAndRealWalkWithOwnedCup; G.BuildIsIdempotentAndProtectsOtherScenes | Table cups have no Collider/NavMeshObstacle/MonoBehaviour; hand colliders disabled; source Cup collider remains enabled; builder checks original bytes. Actual navigation deliveries have cups without obstruction. | Focused PASS d3/v4/s7/f5; current final NOT_RUN |
| P-012 | V.DefaultNavigationPresenterStillSelectsWalkHold; A.EmployeePreservesNavigationBodyAndAcceptedWalk; N.WalkStopsWhenGuardBlocks; N.PauseFreezesPoseAndTimeout; N.FastIsTwiceNotFourTimes | Default direct presenter behavior + imported P11 Walk sample parity/body/material sizes; real P11 default motion collision/Pause/speed unchanged. P12 co-run and direct regression receipts separate. | Focused PASS v4/a4/n4/i7; current final NOT_RUN |
| P-013 | V.BothSpeciesCarryKeepsUpperPoseAndRealWalkWithOwnedCup; V.ReenableResyncsWithoutDuplicateMotionOwnershipOrInactivePlay; A.EmployeeAssetsContainWorkAndCarrySocket; C.FlowDisableCancelsOwnedTaskAndReenableReturnsSameBody; C.DisabledBodyCannotBeBypassed | Both species socket/mask/material/prop assertions, repeated actor visual enable, actual flow/body disable guards and actual return; no stale task resurrection. Whole look remains Owner M-014. | Focused PASS v4/a4/c6; current final NOT_RUN |
| P-014 | A.EmployeeAssetsContainWorkAndCarrySocket; A.EmployeePreservesNavigationBodyAndAcceptedWalk; A.CarryMaskExcludesRootPelvisAndLegs | Real clip/action endpoint length/fps, loop seam, full baked Carry paths/socket, accepted Walk sample parity, model scale/body/material contract. Exporter is read-only/static-reviewed and writes employee-only destinations; no Blender launch by QA, no claim QA independently watched the export process or resampled mother files. | Focused PASS a4; source/static export boundary checked; current final NOT_RUN |
| P-015 | S.RepeatedPlayStartsOneFreshSessionWithDomainReload; S.RepeatedPlayStartsOneFreshSessionWithoutDomainReload | **Actual EnterPlayMode/ExitPlayMode twice each setting**, new IDs, two actors/one controller/HUD, 0 old cups at entry, actual delivery, old actor gone after Exit, settings and original Scene setup restored with log assertions. S code handles true reload local-variable reset via Editor SessionState checkpoints. Not same-Play SetActive simulation. | Focused PASS s7; real-session logs in XML; Owner M-015 NOT_RUN; current final NOT_RUN |
| P-016 | I.RotatedMultiSlotTableKeepsCupModule; D.MovedSupportKeepsLocalPlacement; D.DifferentModulesDoNotShareCups | Actual complete floor/grid/representation fixture rigidly rotates 37 degrees, real three deliveries to multiSlot slot.1 with exact local triangle/no neighbor cup; component moving/rotating support preserves local/world transform. Post-delivery legal Decor Confirm tabletop move/rotate visual still Owner M-016, composed automatic component/scene proof. | Focused PASS i7/d3; current final NOT_RUN |
| P-017 | F.MissingCupConfigurationCannotReportDeliverySuccess; F.MissingVisualCannotReportDeliverySuccess; R.FailedVisualCannotCommitSuccess; D.InvalidVisualRollsBackTemporaryClone; D.FailedInstantiationDoesNotCommitCount | Real Scene configuration/missing presenter fail gives Failed reason/no Delivered/no table cup; Cashier still starts. Display visual creation rollback first/second/third covered with actual component in EditMode, runner false-placement result separately, rather than artificial factory injected into production. | Focused PASS f5/c6/l6/d3; split proof; current final NOT_RUN |
| P-018 | I.HudControlsRemainClickableAcrossAspectRatios; G.BuildIsIdempotentAndProtectsOtherScenes; G.FirstBuildNeverRepurposesAnAlreadyOpenP12Scene; G.MainCafeInstallPreservesExistingWiringAndAutoSpawn; S.RepeatedPlayStartsOneFreshSessionWithoutDomainReload | Real native 1080x1920 and 1600x720, event raycasts + executed buttons Busy/Paused/Decor show/hide; repeated builder preserves legacy component JSON/source bytes/open Scene identity and refuses dirty/Play builds; actual Exit session cleans actors/restores settings. Screenshots only opt-in technical branch, no acceptance inference. | Focused PASS i7/s7; current final NOT_RUN |

### 历史：委托前的Owner manual readiness and status

`Docs/Phase13_Beginner_Guide.md` has explicit preparation/actions/expected/reset for all IDs, verified statically against actual helper menus/builder/seed. No Owner observation was performed by this QA.

| ID | Actual entry/helper and remaining Owner observation | Status |
| --- | --- | --- |
| M-001 | P13 validation Scene; seeded CR/CM anchors and camera views | NOT_RUN |
| M-002 | Cashier/Barista buttons and remaining seconds; visible work | NOT_RUN |
| M-003 | Real complete cup delivery/return; attachment and whole look | NOT_RUN |
| M-004 | Five cycles; visible triangle and no stray cup | NOT_RUN |
| M-005 | Existing time ▶/2x plus task buttons; freeze/acceleration; mandatory auto QA-01 CLOSED; Owner observation separate | NOT_RUN |
| M-006 | Decor Done and actual Preview × cancel; mandatory auto QA-02 CLOSED; Owner observation separate | NOT_RUN |
| M-007 | CM rotate or support move legal Confirm; same identities, actual return/table follow | NOT_RUN |
| M-008 | Fixed CM/Pick-up Store/repair; counter at `(4,4)` only when vacated; Retry Blocked Employees | NOT_RUN |
| M-009 | Actual MainCafe seed, Enable Customer Arrivals, Let Front Customer Leave, real Confirm; mandatory auto QA-03 CLOSED; Owner observation separate | NOT_RUN |
| M-010 | Console baseline/new exception observation across manual suite | NOT_RUN |
| M-011 | Repeated buttons at work/carry/return and both tasks | NOT_RUN |
| M-012 | Additional supports `(1,6)/(3,6)/(5,6)`, HUD actual IDs vs P12 CR; remove/repair chosen device | NOT_RUN |
| M-013 | Context menu Cancel Barista/Cashier Task, repeat each relevant phase | NOT_RUN |
| M-014 | Runtime actor Active or flow Enabled; same-body protection/return | NOT_RUN |
| M-015 | Record/restore Editor Enter Play Mode options; two actual sessions each setting | NOT_RUN |
| M-016 | MainCafe empty/editable, counter.preset.1x3 `(5,5)` middle slot.1; move `(5,2)` + 90deg if safe; Game view two sizes | NOT_RUN |

Player/device/cloud CI/git publication remain NOT_RUN. Technical graphics capture cannot fill Owner results. Guide menu and coordinate checks are readiness only; an unsafe Preview correctly rejected by body protection is not an executed Confirm test.



## P13 追加：柜台降低30cm的游戏内试调

Owner于2026-10-14在静态对比后选择42cm台面，并授权同一E盘P13 branch实施和充分回归。单体及1×2、1×3、2×3 preset、验证长柜台共用42cm mesh/slot/collider；保持原footprint、root scale、顶部8cm及底部10cm，设备不继承非均匀缩放。Catalogue四张缩略图同步更新。原始WorkTable/CR输入hash不变；原CM/CR/cup、员工和动画、MainCafe/P13场景共17项保护检查无变化。

生产修改包括Phase4 Blender生成脚本、Phase4柜台目标及定向rebuild、Phase6 preset定向rebuild及slot验证、生成的柜台资产；完整回归另外发现收银提示高度校准与NavMesh近拐点提前转向问题，追加最小修复。没有修改员工动作、导航参数、设备尺寸、Save identities或手写Scene/Prefab YAML。

| 验证 | 当前证据 |
| --- | --- |
| 旧柜台RED | `counter-height-red.xml`：5项均因72cm不符42cm失败，证明高度差可被检出 |
| 资源/builder focused | `counter-height-focused-edit.xml`：60 PASS / 0 FAIL / 0 SKIP |
| 真场景 focused | `counter-height-native-play.xml`：3 PASS / 0 FAIL / 0 SKIP；CM/CR四方向贴台及原尺寸、MainCafe双任务、四次交付/三杯cap |
| 完整EditMode | `counter-height-full-edit-retry.xml`：2599 PASS / 0 FAIL / 0 SKIP（近拐点主要修复后、追加微小位移修复前快照）；最终导航增量回归另列 |
| 完整PlayMode首次 | `counter-height-full-play.xml`：1261 PASS / 4 FAIL / 5 原有 opt-in SKIP；历史失败已修复，最终重跑见下文 |
| 独立Engineering/QA检查 | 资源/比例通过；导航两处隐藏阈值已补RED/GREEN，独立review Critical/Important CLOSED，无新blocker |
| Unity截图 | `counter-height-reviewed-play.xml`：3 PASS，6张1080×1920真实GameView截图，PNG尺寸与Screen/Camera比例已验证；默认全图已复核，供Owner判断 |
| Owner验收范围 | 42cm方案已批准；本轮M-009/M-016手测PASS。其他未单独报告的视觉细节不推断；复查入口保留在Beginner Guide |

首次`counter-height-focused-play.xml`保留0 PASS/3 FAIL：两项停在batch ScreenCapture无文件，一项为90°后lossyScale浮点精确相等误判。改为native截图及1e-5 scale容差，保留独立尺寸断言；不是通过降低设备尺寸/放宽贴台高度掩盖错误。第一次native3项通过，但截图比例不合格，后续独立修正采集尺寸；不将有图文件等同视觉证据合格。

早期manual PASS保留其当时快照；M-009/M-016已在本轮调整后由Owner明确确认手测PASS。此次追加不代表P14或发布。

正确比例截图：[CM/CR working整体](../outputs/phase13/counter-height-reviewed/validation-working.png)、[三杯与台面](../outputs/phase13/counter-height-reviewed/validation-three-cups.png)、[MainCafe working](../outputs/phase13/counter-height-reviewed/main-cafe-working.png)。近景图会裁到右侧CM，观感审阅优先使用上述全图。

首次完整EditMode超时后，先确认无Editor进程、17项受保护资源无变化，再对23份旧Phase7材质与两份旧验证场景的测试临时改动逐份备份并回读SHA256，恢复这些本轮开始时干净的文件；未丢弃P13或用户改动。备份与清单保存在`outputs/phase13/timeout-test-side-effects-backup/`和`counter-height-recovery-backups.json`。旧锁文件确认无人持有且可独占打开后，备份并仅清理`Temp/UnityLockfile`，项目可重新打开。该运行不计为产品测试FAIL或PASS。

### 完整回归追加修复

首次完整PlayMode的4项失败逐项定位：收银提示仍用旧1.4m地面校准，现改1.1m；拖拽测试由真实Slot推导台面及边角；P12边界fixture从真实capsule下轴心采样，保留原skin范围；M007装修后回位在距NavMesh拐点不足1mm时提前转向，位移被边界裁为零，现只有下一整段通过完整guards才允许近拐点提前推进，否则走完原拐点。原CollisionSkin、Epsilon、ArrivalDistance、重试及timeout均未改变。

证据保留：counter-height-m007-diagnostic.xml及followup-diagnostic.xml记录失败复现；后者19项中17 PASS、M007仍FAIL、1原有截图SKIP，证明前3项修复。counter-height-corner-green.xml随后32 PASS/0 FAIL/0 SKIP，包含M007完整两个阶段与全部31项P11 Movement。临时只读诊断代码已移除，trace文本保留。完整EditMode使用3600秒时限重跑，2599 PASS / 0 FAIL / 0 SKIP。独立review随后指出小于1e-5/1e-6米位移的隐藏归一化/投影阈值，补充针对性RED/GREEN验证已完成；本次完整Edit结果绑定追加微小位移修复前快照。最终完整PlayMode已完成，见最终结果。

追加微小位移验证：`counter-height-micro-red.xml`为0 PASS/2 FAIL（非零中间拐点方向被Unity.normalized清零）；`counter-height-micro-driver-only.xml`为1 PASS/1 FAIL（5e-7m最后一步仍被StaticProjection清零）；完整最小修复后counter-height-micro-green.xml为34 PASS/0 FAIL/0 SKIP（M007两个阶段、原31项Movement、新2项边界）。两项新fixture仅固定corner序列，真实World/NavMesh/注册身体/guards执行，并验证下一整腿仍被真实身体挡住；不冒充自然bake的M007路径。修复只使用非零length显式方向，StaticClear、CapsuleCast、完整skin、sweep、最终endpoint和第二障碍检查均保留。独立review确认Critical/Important CLOSED，最终P11 Edit增量 `counter-height-final-nav-edit.xml`：77 PASS / 0 FAIL / 0 SKIP；最终完整PlayMode 1267 PASS / 0 FAIL / 5原有opt-in SKIP。

最终运行冻结清单：outputs/phase13/counter-height-final-source-manifest.json共116份代码/资产文件。原17项设备/cup/员工/动画/场景保护hash无变化。第二轮完整PlayMode counter-height-final-play.xml为1266 PASS / 1 FAIL / 5原有opt-in SKIP；唯一FAIL为PauseAndFastScaleWorkingExactlyOnce在暂停切换同帧提前采样，normalizedTime差0.001141。测试只先drain切换帧并断言deltaTime=0，原.001精度及speed ratio保留，生产动画未改；专项7项及最终完整重跑均已通过。完整Edit的2599项绑定追加微小位移修复前快照；该修复后整个P11 Edit组77项和movement/M007组34项均PASS，未把两次不同快照混称同一次全量运行。完整Edit另留下31份此前干净的旧材质、UI、验证场景临时写入，已逐份备份回读SHA后恢复；备份清单counter-height-final-edit-side-effects.json，未恢复任何P13或用户修改。

最后一项动画采样修复专项：counter-height-pause-final.xml 7 PASS / 0 FAIL / 0 SKIP。保留原.001暂停精度与1.7～2.3倍速比；先drain已确定running delta的切换帧，再断言Time.deltaTime=0取基线。实际日志runningDelta=0.0011705003、pausedDelta=0、normalizedDrift=0。生产Animator/员工代码无修改；两个真实场景暂停测试也PASS。独立审查确认测试采样时序问题。最终单次完整PlayMode重跑使用counter-height-final-play-verified.xml（1267 PASS / 0 FAIL / 5原有opt-in SKIP，1272 total，981.73秒，CLI exit 0），新116文件冻结清单counter-height-verified-source-manifest.json；旧的1266P/1F/5S XML保留。

### 42cm修改文件索引

| 文件/范围 | 用途 |
| --- | --- |
| `ArtSource/Phase4/Tools/BuildPhase4ProductionAssets.py`、Phase4 `.blend/.fbx` | 保留底部10cm与顶部8cm，压缩中段，将台面72cm降到42cm |
| `Assets/Editor/Phase4/Phase4ProductionAssetBuilder.cs`、`Assets/Editor/Phase6/Phase6DecorationAssetBuilder.cs` | 定向重建柜台、Slot/collider/preset及缩略图，保持设备原尺寸 |
| `Assets/Art/Phase4/Prefabs`柜台、`Assets/Art/Phase6/Prefabs`柜台、4张缩略图 | 游戏中实际使用的新柜高与Catalogue预览 |
| `Assets/Scripts/UI/Decoration/CashRegisterSideIndicatorView.cs` | CR提示高度随台面重新校准 |
| `Assets/Scripts/Navigation/NavMeshMovementDriver.cs`、`NavigationCollisionGuard.cs` | 修复近拐点过早转向及极小合法位移被清零，保留碰撞政策 |
| `Assets/Tests/EditMode/Phase13/CounterHeightRegressionTests.cs`、PlayMode `EmployeeCounterHeightTests.cs` | 五类柜台资源合同、CM/CR四方向贴台及尺寸、MainCafe任务与三杯cap |
| 旧Phase4/6资源测试、P8R拖拽、P12桌边fixture、P11 movement、P13 animation测试 | 更新真实台面采样与边界回归；暂停先消费切换帧再取稳定基线 |

复查入口见 [Beginner Guide的42cm段落](Phase13_Beginner_Guide.md#42cm-台面试调后的观感复查2026-10-14)。模型尺寸断言和截图不代替Owner的新比例观感判断，原M-009/M-016已按Owner最新指示记录手测PASS。
### 42cm最终交付结果（2026-10-14）

本次降低30cm的开发与回归完成。最终完整PlayMode在全部生产修复及暂停测试采样修复后执行：[原始XML](../outputs/phase13/counter-height-final-play-verified.xml)，1267 PASS / 0 FAIL / 5 SKIP。五项SKIP均为已有、需要显式启用的截图/几何导出用例；本次三项真实场景及截图专项均已通过，不计作SKIP。

完整EditMode [2599 PASS / 0 FAIL](../outputs/phase13/counter-height-full-edit-retry.xml) 绑定微小位移修复前代码；该修复后的整个P11 Edit组 [77 PASS / 0 FAIL](../outputs/phase13/counter-height-final-nav-edit.xml) 及movement/M007组 [34 PASS / 0 FAIL](../outputs/phase13/counter-height-micro-green.xml) 补齐增量验证。暂停专项 [7 PASS / 0 FAIL](../outputs/phase13/counter-height-pause-final.xml)。这些是不同运行，不合并伪称同一全量快照。

最终116项源码/资产冻结清单回读0 mismatch；17项CM/CR/cup/员工/动画/场景保护清单回读0 mismatch。Unity已退出；唯一旧Phase7临时材质已备份、核对SHA后恢复，全部P13及用户修改保留。最终日志仅移除CLI自动凭据参数，XML结果未修改。

Owner已批准42cm方案，并明确确认本轮M-009/M-016手测PASS；不把这两项扩写为其他未报告的视觉细节结论。本次无commit/push/PR/merge。

## Owner最终手测记录与review（2026-10-14）

Owner明确指示：“那9和16就按我手测通过记录吧，更新文档，做一下P13最后的review。”因此更新如下，不要求重复已完成的手测。

| 项目 | 当前结果 | 证据与边界 |
| --- | --- | --- |
| M-009：MainCafe顾客/员工同场、FIFO及装修reset | **PASS（Owner手测）** | Owner上述明确确认；未提供逐步时序日志，不补造观测数值 |
| M-016：多Slot杯子跟随、横竖屏真实操作 | **PASS（Owner手测）** | Owner上述明确确认；另明确反馈柜台及Catalogue手动拖拽均正常 |
| 原M-001～004、M-006 | PASS（Owner） | 保留原确认；M-006与代测重叠 |
| 原M-005～008、M-010～015 | PASS（10项Codex场景代测） | 保留真实场景API代测边界，不声称鼠标操作；去重后16项全部覆盖 |
| computer-use追加尝试 | **BLOCKED（历史尝试）** | [记录](../outputs/phase13/computer-use-m009-m016/attempt-report.md)：实际点选成功，但工具拖拽未完成，原因未定；不作为游戏FAIL，也不改记PASS |

本次最终review为当前未提交源码及资产的只读复核和既有XML/manifest回读；不为docs-only改动重复Unity全量运行。Engineering及QA/技术美术独立审查均已完成，结论如下。


### 最终review结论：本地技术审查通过

- **Engineering — PASS**：独立reviewer检查全部Employees runtime、P13 Editor builders/exporter、Navigation与CR提示变更、P4/P6高度适配、P8/P12兼容及相关tests。未发现可操作的Critical、Important或Minor问题。任务generation/callback退休、实际到达后交付、返岗与忙碌状态、三杯视觉上限及完整碰撞guards符合已批准合同。
- **QA／技术美术 — PASS**：独立reviewer逐case解析XML，核对coverage的90个不同映射方法均有Passed记录；P13子集108 Edit及44 Play均通过（Edit仍绑定对应历史快照）。42cm五类柜台及三项真实场景专项覆盖尺寸、Slot、设备四方向贴台、第四杯交付与三杯cap。直接复核working/three-cups默认截图，未发现新增技术视觉问题。不替代Owner未逐项报告的审美判断。
- **Production／文档 — PASS**：Guide、Report、Plan和Roadmap统一记录Owner M-009/M-016 PASS；保留代测与Owner来源、历史失败、computer-use BLOCKED、5项原有opt-in SKIP及版本管理边界。相关本地Markdown链接静态检查0失效。

**开放finding：Critical 0 / Important 0 / 新Minor 0。结论：P13本地开发、已约定manual验收及最终技术review完成，可交由Owner进行版本管理收尾。** 这不是main merge、发布或P14启动授权。

审查绑定：`codex/phase-13-employee-task-flow`，HEAD/base `302f3e969da667f3f389820d303503865b1b0d63`，实际对象为未提交tracked和untracked实现；不能以HEAD..HEAD空diff冒充review。当前116项源码/资产与`counter-height-verified-source-manifest.json`逐项一致，17项保护资源亦一致。root另行回读XML与hash，均与独立审查一致。

| 最终证据回读 | 实际结果 | 快照范围 |
| --- | --- | --- |
| `counter-height-final-play-verified.xml` | 1267 PASS / 0 FAIL / 5 SKIP；1272 total | 全部最终代码及测试修复之后；XML根状态为Skipped:Ignored，保留五项旧opt-in跳过 |
| `counter-height-full-edit-retry.xml` | 2599 PASS / 0 FAIL / 0 SKIP | 最后微小位移修复前的完整Edit，不能称为最终代码同一次全量 |
| `counter-height-final-nav-edit.xml` | 77 PASS / 0 FAIL / 0 SKIP | 微小位移修复后整个P11 Edit增量 |
| `counter-height-micro-green.xml` | 34 PASS / 0 FAIL / 0 SKIP | M007、原31项Movement及新增两项微小位移边界 |
| `counter-height-pause-final.xml` | 7 PASS / 0 FAIL / 0 SKIP | Pause取样时序修复后的专项 |
| `counter-height-reviewed-play.xml` | 3 PASS / 0 FAIL / 0 SKIP | 真实场景及正确比例GameView截图 |

本轮没有重新执行Unity；上表为既有原始XML的当前回读，源码hash未漂移。Android/iOS真机、Player build、cloud CI未验证；真实Customer/Order/pay/pick-up整合属于P14，不纳入本次关闭范围。未commit、push、PR或merge。

### 版本管理交接（2026-10-14）

上述“未commit、push、PR或merge”为最终review完成时的记录。Owner随后明确授权将P13提交并推送至`codex/phase-13-employee-task-flow`，创建目标为`main`的PR，先不merge。提交包含实现、资产、设计/验收文档及原始XML和引用截图；临时日志、备份及无关材质序列化变化不纳入。版本管理结果以Git提交及对应PR状态为准，保留手动Unity CI流程。
