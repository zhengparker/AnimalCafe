# Phase 11 Navigation：新手验证指南

**状态（2026-09-30）：Phase 11 开发与验收完成，M-001～M-011 全部 Owner PASS。** Owner 在确认 M-001～M-010 后补充“11也完成了”，M-011 柜台站位与转向验收已记录。以下操作保留供参考，无需重复测试。成果位于现有 `codex/phase-9-order-domain` 分支；Owner 已授权 review 后 push 和创建 PR，尚未 merge main，P12 未开始。最新 P9–P11 review、完整回归和交付状态统一见 [合并前 Review](Phase9_Phase11_Merge_Review.md)；此前的逐次测试与实现决策保留在 [验证报告](Phase11_Validation_Report.md)。

此前于 2026-09-30，已按 Owner 授权把两只原版 `.blend` 的 walk 手脚摆动幅度试调为 **1.5 倍**，并重新导入 Unity。源文件已备份；只改 8 条上下肢旋转 curve，模型尺寸、frame 1～21、FPS 和行走速度不变。该次 focused EditMode **8 PASS**、PlayMode **8 PASS**，均 0 FAIL / 0 SKIP。新版 M-002 已包含在 Owner 对 M-001～M-010 的通过确认中。

**已完成的复测：** Owner 确认 **1x → crowd8** 和独立 Play 的 **1x → detour** 均 PASS。自动测试的 crowd8 为 8 人全到达；拥堵时仍允许有界失败，出现失败请保留角色、reason 和 retry 信息。M-003 的 **crossing** 也已包含在 Owner 最新的通过确认中。

最终代表截图为 `outputs/phase11/final-fix-normal-screen-v2.png` 和 `final-fix-blocked-screen-v2.png`，均是实际 `1080×1920` Canvas＋Camera。验证面板位于底部，中文阻挡原因在 P8 readiness 横幅下完整显示。M-009 的手动 PASS 来自 Owner 本次实测确认；这些截图保留为辅助证据。

## 1. 打开验证场景

1. 用 Unity `6000.5.5f1` 打开 `E:\Unity\Project\AnimalCafe\.worktrees\phase-9-order-domain`。先保存自己的编辑，等待编译结束。
2. 在 Project 打开 `Assets/Scenes/Validation/Phase11Navigation.unity`，选择 **Game** 标签，然后点 Editor 顶部的 **Play**。
3. 等底部出现 `Ready: P8 business and Navigation validated`。场景有 8 个 Shiba / Westie 原尺寸角色，并接入真实 P8 装修、家具和时间控制。
4. 同时打开 **Window > General > Console**。出现红色 error、角色瞬移或持续明显穿模时，记录 scenario、状态文字和截图。

每个独立例子建议先退出 Play 再进入，恢复原始角色位置和本次临时装修。不要在 Play 中保存场景。`Cancel requests` 只取消当前请求，角色留在当前位置。

## 2. 底部 scenario 按钮

角色先步行到 staging（准备位置），再执行目标路线。当前例子未结束时，请等待或点 `Cancel requests`，再运行下一个。

| 按钮 | 应看到的结果 |
|---|---|
| straight | 直线路线结束为 `Arrived`。 |
| detour | 绕过实体障碍，结束为 `Arrived`。 |
| crossing | 两个角色交叉通过并到达，不能整个人互穿。 |
| narrow-wait | 1 m 窄通道相遇可等待；不能强行穿过对方，受阻应有限结束。 |
| same-target | 两个角色争同一位置，至少一个明确 `Failed`，不能占同一个身体位置。 |
| recovery | 原目标被占，先等待、从当前位置 retry 一次，再步行去备用点，结束为 `Recovered`。 |
| crowd8 | 8 个角色同时移动；观察避让与每个请求的最终结果，不能只看是否安静停住。 |
| blocked85 | 0.85 m 窄通道目标应明确 `Failed`，不穿墙、不缩小身体。 |

状态中 `retry=1` 表示只重试一次；`Failed` 是这个请求未到达，不能当作到达成功。`LayoutChanged` 表示已确认装修使旧请求失效，需要重新发起 scenario。若显示 `Staging blocked`，说明准备路线已受阻，先重开 Play 做独立检查。

运行 `straight` 时分别点 **Pause / 1x / 2x**：Pause 应同时停止位移、walk 和 timeout；2x 应同时加快位移与动作。可拖动/缩放 Camera 查看角色，但不要手动改角色 Transform。

## 3. 装修碰撞与堵路/修复

1. 在独立 Play 中运行 **crowd8**，让右侧角色步行进入右侧 P8 装修区域（不要手动拖角色 Transform），再进入原有 **Decoration** 入口。角色应暂停。
2. 选实体 Counter，将 Preview 放到装修有效区域内的暂停角色身上再 Confirm。占用角色身体位置时 footprint 应变红，并显示 **“这里有角色，请换个位置。”**；Confirm 应被拒绝并保留 Preview 和原位置，移开后 footprint 应恢复绿色，Cancel 后正式布局不变。Owner 已确认拒绝/Cancel 功能及红色 footprint 复测 PASS。若角色仍在装修区域外，不能把普通越界拒绝当作本项通过。
3. **M-009 请另开一次 Play，从原布局开始。** 先选 **2x**，再进入 Decoration。在右侧 P8 的 8×8 Grid 中，选择 `furniture.counter.module.01`（1×1 Counter），放到 **(4,4)** 后 Confirm。这是 Coffee Machine 所在 Counter **(4,3)** 北边紧邻的一格，即它的 Employee 站位。坐标从西南角 **(0,0)** 起算，X 向东、Y 向北。保留原设备及三个支撑 Counter。
4. 退出 Decoration。应立即保持 Pause，顶部时间栏下完整显示 **“路径被挡住了，请进入装修调整。”**；1x/2x 不能绕过。底部诊断不会盖住 Camera；进入 Decoration 时诊断自动隐藏。
5. 再进入 Decoration，选中刚添加的 Counter，将同一件家具移动到空闲格 **(0,6)** 并 Confirm，再退出。应恢复原来的 **2x**；如果自己明确选了 Pause，就仍保持 Pause。不要直接改角色或表示层 Transform。
6. Codex 自动用例 `I005_I006_I007_RealFurnitureConfirmBlocksCoffeeStationThenRepairRestoresFastAndFreshMove` 已通过上述真实 transaction，并让 Westie_8 从未被改动的实际位置 `(9.2,0,-7)` 发出新 MoveTo，到达原 Coffee Anchor `(7.5,0,4.5)`。M-009 的堵路/修复逻辑已由 Codex 自动验证 PASS；Owner 随后实测并确认 PASS。此前 Codex GUI 拖拽未可靠完成的记录仍保留。

仅堵住一个非必需 station 而仍有完整可用组合时，允许继续营业。若没有成功构造必要路线堵塞，本项记 **NOT_RUN/BLOCKED** 并记录布局，不能只凭无报错记 PASS。旧 `task7-*` 截图通过禁用 adapter 展示 UI；新的 `final-fix-blocked-screen-v2.png` 使用上述真实家具 Confirm，截图仍不能代替手动输入验收。

## 4. 原 Counter 站位观察

普通 scenario 没有单独的 Counter 按钮。当前只有一个**受限观察入口**：退出 Play，打开 **Window > General > Test Runner > PlayMode**，搜索 `OriginalCounterAnchorsArriveAndFaceWithoutOffset`，选择该用例并点 **Run Selected**，切到 **Game**。完整名称为 `AnimalCafe.Tests.PlayMode.Phase11Integration.Phase11NavigationSceneTests.OriginalCounterAnchorsArriveAndFaceWithoutOffset`。

该测试用原 InteractionAnchorResolver 计算 `1×1 / 1×3 Counter` 的站位，不加外移偏移，并通过真实 World Update 步行和转向；但 fixture 固定每帧模拟 `1/60 s`，实际播放快慢取决于 Editor 帧率，且测试结束后自动卸载场景。因此它是受限的辅助观察入口。**M-011 已由 Owner 确认 PASS**；此结果来自 Owner 的“11也完成了”，不以绿色断言或静态截图替代人工验收。

保留模型约 **1.30 m** 原尺寸。到达、转身、避让时轻微短暂的尾巴/手臂/边缘穿插可接受；持续明显嵌入墙或柜台、整个人穿过对方不可接受。不要为达到零 Mesh 交叠而自行缩小角色或把所有站位向外挪。

## 5. Owner 检查表

| ID | 检查内容 | 当前结果 |
|---|---|---|
| M-001 | 两种角色比例、材质、约 1.30 m 高度正常 | PASS（Owner） |
| M-002 | walk 自然，停下不原地走，没有瞬移 | PASS（Owner，含 1.5 幅度版） |
| M-003 | crossing / crowd8 避让，不整个人互穿 | PASS（Owner，含 crossing / crowd8） |
| M-004 | narrow-wait 等待及最终失败可理解，不穿人 | PASS（Owner，2026-09-30） |
| M-005 | 只有轻微短暂边缘穿插，没有持续明显嵌入实体 | PASS（Owner，含 A detour / B blocked85） |
| M-006 | Pause / 1x / 2x 的动画、位移、等待一致 | PASS（Owner） |
| M-007 | recovery 从当前位置 retry 一次，再走到备用点 | PASS（Owner） |
| M-008 | 家具不能 Confirm 到角色身上，Cancel 不改正式布局 | PASS（Owner，含新增红色 footprint 复测） |
| M-009 | 真实必要路线堵塞阻止恢复，修复后恢复，提示清楚 | PASS（Owner 实测；Codex 自动验证也已通过） |
| M-010 | 退出当前 Play，另开 `Assets/Scenes/MainCafe.unity`：原装修、Camera 拖拽/缩放、选择、时间控制正常，Console 无新增 error | PASS（Owner） |
| M-011 | 原尺寸角色在原 Counter Anchor 停步转向，站位合理 | PASS（Owner，2026-09-30） |

M-001～M-011 手动验收已全部完成，既有技术 review 与自动测试证据见验证报告。后续如发现问题，可记录 scenario、状态文字及 Game View / Console 截图。最新完整回归、已授权的 push／PR 交付与合并边界见 [合并前 Review](Phase9_Phase11_Merge_Review.md)；尚未 merge main，P12 未开始。
