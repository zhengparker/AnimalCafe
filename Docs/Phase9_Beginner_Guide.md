# Phase 9 订单调试窗口：新手操作指南

本窗口只模拟订单规则，不会让 MainCafe 开始真实营业。窗口顶部会显示“P9 Debug / 临时模拟数据；不是真实营业”。关闭窗口、点击 **Reset Session**，或进入/退出 Play Mode 时，临时订单都会清空。

## 本次开发位置

- Unity project：`E:\Unity\Project\AnimalCafe\.worktrees\phase-9-order-domain`
- Branch：`codex/phase-9-order-domain`
- 开发基线：`c80f208`；P9 实现位于上述开发分支。请从这个 project folder 打开 Unity，确保看到本次窗口。

## 打开与准备

1. 用 Unity `6000.5.5f1` 打开批准的 Phase 9 checkout，等 Console 编译完成。已有未保存的 Scene 或 Prefab 工作请先自行处理；本窗口不会替你保存。
2. 打开 **Window > AnimalCafe > Phase 9 Order Debug**，同时打开 Console。建议用已保存的 `Assets/Scenes/MainCafe.unity` 做人工检查。
3. 默认输入是 **Customer ID = C1**、**Product ID = coffee**、**Employee ID = E1**、**Order ID = 1**、**Failure reason = diagnostic.stop**。Order ID 是可编辑的 `long` 数值；**Claim Next** 成功后也不会替你改它。
4. 除生命周期用例 P9-M-009/010 外，先进入 Play Mode，再点 **Reset Session**。每个用例开始前再点一次 Reset。

按钮按顺序对应 `OrderService` 的命令：**Create** 创建、**Claim Next** 领取最早等待订单、**Start Preparation** 开始制作、**Mark Ready For Pickup** 标记可取、**Simulate Customer Collection** 用当前 Customer ID 取餐、**Fail** 用当前 Failure reason 标记失败。输入可以故意留空，用来观察错误码。

每次点击后看 **Last result** 的 `Success` 或 `Error`，再看 **All orders** 中的 `State`、`Customer ID`、`Claimant`、`Reason`，以及 **Waiting FIFO** 的顺序。错误应留在窗口中，Console 不应新增 Error/Exception。此窗口的按钮在 Pause/1x/2x 下仍是诊断命令；它们不代表正式游戏的暂停权限规则。

## 逐项 Manual / GUI 验收

详细步骤、预期值与结果沿用 [Phase 9 原测试用例](superpowers/specs/2026-09-28-phase-9-order-domain-test-cases.md)，不另建结果目录。Owner 已于 2026-09-28 授权 Codex 使用 computer use 在真实 Unity 窗口代跑功能性验收；Codex 操作不表示 Owner 本人亲测。M-010 的 Camera 拖动另由 Owner 于 2026-09-29 人工补核，结果分别记录执行者。

| Case | 要检查的重点 |
|---|---|
| P9-M-001 | 正常流程、错误顾客取餐被拒、正确顾客完成 |
| P9-M-002 | FIFO 顺序、同一员工忙碌时不能再领 |
| P9-M-003 | 非法跳步、错误员工不能推进订单 |
| P9-M-004 | Failed 从等待队列移除、员工可再领取 |
| P9-M-005 | Completed/Failed 终态不能重复操作 |
| P9-M-006 | 空输入与无效 Order ID 被拒，之后仍可正常操作 |
| P9-M-007 | 旧订单结束不影响员工正在处理的新订单 |
| P9-M-008 | Pause/1x/2x 等待后订单不会自动推进 |
| P9-M-009 | Reset、关闭重开、进出 Play，以及 Reload Domain 开/关 |
| P9-M-010 | MainCafe 原有 Camera、时间控制和 Decor 操作 smoke |

P9-M-009 需要在 **Edit > Project Settings > Editor > Enter Play Mode Settings** 中检查 Reload Domain 两种设置，结束后恢复原设置。本版本实际显示 `When entering Play Mode` 下拉选项，原值是 `Reload Domain and Scene`。实际 Play 切换不能由自动测试或直接调用回调代替。P9-M-010 比较开窗前后的 MainCafe 操作，不要保存临时改动。

实际 GUI 截图已保存在 `outputs/phase9/gui-2026-09-28/`：包括默认空窗口、ReadyForPickup、WrongCustomer 且状态不变、Play 边界清空、Reset 缺陷及修复、MainCafe smoke 与 Console。逐项文件名见原测试用例的结果记录。

## 自动验证结果（更新至 2026-09-29，America/Toronto）

当前状态：**M-001…010 全部 PASS，GUI、最终 review 与修复后的完整回归已完成。** 最新 focused 132+3 PASS；最终 EditMode 2255 个唯一 PASS；全量 PlayMode 1039 PASS、0 FAIL、4 个既有可选 SKIP。M-001…009 和 M-010 其他子项由 Codex 于 2026-09-28 实测；Camera 拖动由 Owner 于 2026-09-29 确认。本记录为推送前验证，交付分支为 `codex/phase-9-order-domain`；Owner 授权已记录，远端提交由交付时核验。main merge、阶段 closeout 与 Roadmap 更新未执行。

### 2026-09-29 当前版本验证

以下本次测试均包含 Reset 焦点修复；已完成部分的 XML 已独立核对。

| 范围 | 实际结果 | 证据 |
|---|---|---|
| P9 focused EditMode | 132/132 PASS，0 FAIL/SKIP | `outputs/phase9/pre-push-2026-09-29-edit.xml` |
| P9 focused PlayMode | 3/3 PASS，0 FAIL/SKIP | `outputs/phase9/pre-push-2026-09-29-play.xml` |
| 最终全量 EditMode | 2255 个唯一测试全部有 PASS 证据，0 FAIL，无缺漏 | `outputs/phase9/pre-push-2026-09-29-full-edit.xml` + 本次三组隔离补跑；汇总 `pre-push-2026-09-29-edit-verification.json` |
| 最终全量 PlayMode | 1039 PASS，0 FAIL，4 个既有可选 SKIP | `outputs/phase9/pre-push-2026-09-29-full-play.xml` |

本次 full EditMode 原始 XML 为 1900 PASS / 355 SKIP / 0 FAIL。三个新进程 `pre-push-2026-09-29-isolated-maincafe.xml`、`pre-push-2026-09-29-isolated-validator.xml`、`pre-push-2026-09-29-isolated-scene.xml` 分别取得 160、194、1 PASS，均 0 FAIL/SKIP。按 Ordinal exact fullname 合并 2255 个唯一 PASS，隔离补跑恰好覆盖原 355 SKIP，无缺漏或额外项；原始 SKIP 保留。

本次 PlayMode 的四个 SKIP 名称与原因和历史报告完全一致，均为既有 P8R 可选截图/spacing 检查；P9 的 3 项全部通过。最终 Engineering/QA review 无新增代码或覆盖问题，修复后的完整 regression gate 已通过。

最终汇总为 `outputs/phase9/pre-push-2026-09-29-verification.json`：7 份报告均无 FAIL；32 项 baseline 文件 hash 未变；tracked Assets/Packages/ProjectSettings 的 staged/unstaged diff 均为空；Unity test 进程已退出。测试生成的资源差异已备份并恢复，运行记录保留在本地。

### 2026-09-28 历史自动验证

下表是基线 `c80f208` 加当时未提交 P9 文件的历史结果，执行时间早于 Reset 焦点修复。该修复另有实际 GUI GREEN 和上方的新 focused 结果；不能把下表旧全量结果写成修复后重跑结果。

| 范围 | 实际结果 | 证据 |
|---|---|---|
| P9 focused EditMode | 132 PASS，0 FAIL/SKIP（126 domain + 6 Editor） | `outputs/phase9/task4-edit-green2.xml` |
| P9 focused PlayMode | 3 PASS，0 FAIL/SKIP | `outputs/phase9/task4-play-green2.xml` |
| 全项目 EditMode | 2255 个唯一测试均有 PASS 证据，0 FAIL，无缺漏 | 完整运行 + 三组干净进程补跑，见下方 |
| 全项目 PlayMode | 1039 PASS，0 FAIL，4 个可选 SKIP | `outputs/phase9/full-play.xml` |
| Engineering / QA | 原实现代码审查及用例映射通过；GUI 发现的 Reset 焦点显示问题已修复并复测；M-001…010 GUI 验收完成 | 完整 case ID 与执行状态见原测试用例第 7、9 节 |

完整 EditMode 的 `full-edit-final.xml` 为 1900 PASS / 0 FAIL / 355 SKIP。前序测试留下 dirty `AssetPipelineReadability` Scene，触发了保护调用者工作的 guard。随后分别在新进程运行 `edit-isolated-maincafe.xml`（160 PASS）、`edit-isolated-validator.xml`（194 PASS）和 `edit-isolated-scene.xml`（1 PASS）。按 exact test fullname 合并后，覆盖原清单全部 2255 项，没有额外或遗漏名称。原始 SKIP 记录保留，没有改写为 PASS；汇总为 `outputs/phase9/final-verification.json`。

PlayMode 的 4 项 SKIP 都是既有 P8R 可选检查：Cash Register 原生截图、spacing geometry audit、Readiness 原生截图、UI Enhancement 原生截图。它们需要显式开关或真实 Game View；P9 的 3 项 PlayMode 全部通过。

首轮完整 EditMode 因原设定的 20 分钟上限而中止，没有 XML，未计入通过结果；提高运行上限后的完整测试已完成。当次自动测试产生的资源差异已另存并恢复，tracked Assets 无差异，当时 P9 文件 hash 与测试前一致，测试进程已退出。本轮后续 GUI 验证发现并修复了 Reset 焦点显示问题，因此保留上述自动结果的版本边界。

## GUI 执行结果与剩余事项

- **M-001…008：PASS。** Codex 已通过真实窗口点击与输入验证正常流程、错误身份、FIFO、失败恢复、终态保护、无效输入、旧单结束不释放新单员工，以及 Pause/1x/2x 下状态不自动变化。
- **M-009：PASS。** Reset 后有焦点字段仍显示旧文字的问题已修复；Customer、Employee、Order ID 的实际 GUI 复测通过。Reload Domain 开启时完整序列通过；Owner 停靠 Project Settings 后，Codex 选择 `Reload Scene only`，连续两轮验证 Reset、关闭重开、进入/退出 Play 后清空、默认字段、Last result=(none)，随后 Create 均仅生成首单 #1。两轮结束再次确认关闭配置，再恢复原来的 `Reload Domain and Scene`。设置选择和恢复都有实际 UI 截图；测试期间磁盘值未反映 OFF，不能用磁盘值替代这些 GUI 证据。
- **M-010：PASS。** Codex 于 2026-09-28 验证 P9 使用前后的缩放、Pause/1x/2x、Decor→Counter 1x1 Preview→Cancel→Done 均通过，原布局保留。当时工具发送的 Camera 拖动前后都未观察到位移，原因仍未确认。Owner 于 2026-09-29 回应本 chat 的工具使用前后拖动对照请求，回复“能移动的”，确认前后移动正常，以这条人工确认补齐 Camera 子项。本次没有新增截图或 Console 检查，也不表示 Codex 成功拖动。
- **2026-09-28 环境检查记录。** 当时已恢复 1x 并退出 Play；M-009 补测后 P9 已 Reset 并关闭，Project Settings 保留为停靠 tab，设置已恢复为 `Reload Domain and Scene`。最终 Console 为 0/0/0，Scene 无 dirty 星号；23:47 只读复核 MainCafe、EditorSettings、两个 Packages 文件的 SHA256 与测试前相同，tracked Assets/Packages/ProjectSettings 的 staged/unstaged diff 均为空。最终证据为 M09-restored-reload-settings.png、M09-final-edit-console.png。Owner 的 Camera 确认没有提供新的 GUI 环境检查，上述记录保留原日期；最新自动验证另见上方。
- 编译期间出现过既有 `Phase7SurfaceAssetBuilder.cs:456` 的 CS0618；该文件与 HEAD 相同。进入 Play 时 Console 自动清空，执行者未手动 Clear；warning 历史保留在测试记录，未写成全程没有 warning。
- Task 3 的 MarkReadyForPickup/Complete 没有各自独立的初始 RED 记录，已在原测试用例第 9 节如实披露；四个操作已有最终行为测试通过证据。

## 本次文件范围

- `Assets/Scripts/Orders/`：OrderService 和四个数据/结果类型，负责订单状态、权限、FIFO、ID 与不可变查询。
- `Assets/Editor/Phase9/`：独立临时 session 和手工验收窗口。
- `Assets/Tests/EditMode/Phase9/`、`Assets/Tests/PlayMode/Phase9/`：核心规则、窗口及帧/时间边界测试。
- `Docs/AnimalCafe_Project_Design.md`：已批准的“正确顾客实际取走才 Completed”规则；本指南与 P9 design/test/plan 同步实际状态。

交付代码位于基线 `c80f208` 之后的 `codex/phase-9-order-domain` 开发分支。本记录为推送前验证；Owner 已于 2026-09-29 授权最后 review 及提交、推送该分支，远端提交由交付时核验。`outputs/` 和 `.superpowers/` 中的运行记录保留用于核对，不属于生产代码提交范围。main merge、阶段 closeout、Roadmap 更新与后续 Phase 尚未执行。
