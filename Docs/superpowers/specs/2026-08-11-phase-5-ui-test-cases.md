# AnimalCafe Phase 5 — UI Test Cases

> 状态：Pre-development Test Design，等待 Studio Owner approval
>
> 对应设计：`Docs/superpowers/specs/2026-08-11-phase-5-ui-architecture-design-system-design.md`
>
> 原则：这些 test cases 必须在 production implementation 前批准；开发中只能补充新发现的 cases，不能静默删除或放宽已批准的 expected result。

## 1. 如何阅读与记录

- `AT`：Automated EditMode，测试不依赖完整 Scene 的规则与 data。
- `IT`：Integration PlayMode / Editor，测试真实 Canvas、EventSystem、TMP、Scene 与 components 的组合。
- `RT`：Regression，保护 Phase 0–4 已完成行为与 assets。
- `MT`：Manual，检查视觉、Touch 手感、animation、可读性和人类体验。
- 状态只能是 `Not Run / Passed / Failed / Blocked`。
- Automated evidence：Test Runner XML、Console log、测试总数与失败数。
- Manual evidence：指定截图或短视频；涉及性能时另附 Profiler capture / 数值记录。
- `Blocked` 必须写明 blocker，不能当作 Passed。

## 2. Automated EditMode Cases

| ID | 前置条件 | 操作 | 预期结果 | 覆盖风险 | Evidence |
|---|---|---|---|---|---|
| AT-001 | 新建完整 `AnimalCafeUiTheme` fixture | 运行 theme validation | 所有 required semantic colors、text styles、spacing、radius、materials、motion 和 sizes 均存在 | Prefab 缺 token 后显示异常 | EditMode XML |
| AT-002 | Theme 缺少 required font/material | 运行 validation | 返回明确 issue code 与 asset path，不抛 exception | 缺资源静默失败 | EditMode XML |
| AT-003 | Theme 的 Body=15 或 Label=13 | 运行 validation | 分别报告小于 `16/14` baseline | 小字不可读 | EditMode XML |
| AT-004 | Theme touch target=47×48 | 运行 validation | 报告小于 `48×48` | Touch target 太小 | EditMode XML |
| AT-005 | Button role/state matrix fixture | 枚举 roles 与 states | 正好覆盖 3 roles × 3 states，无 Hover requirement | 状态遗漏或 Windows UX 回流 | EditMode XML |
| AT-006 | Navigation coordinator 空状态 | 请求 Back | 返回 NotHandled，HUD 不受影响 | Back 误关 HUD | EditMode XML |
| AT-007 | Panel A active | 打开 Panel B | A 被替换/关闭，只保留 B 为 main Panel | 多个主面板重叠 | EditMode XML |
| AT-008 | Panel + Bottom Sheet active | 请求 Back | 只关闭最上层 Bottom Sheet，Panel 保留 | Back 顺序错误 | EditMode XML |
| AT-009 | Panel + Modal A + Modal B | 连续请求 Back | 依次关闭 B、A、Panel | Modal stack 死路 | EditMode XML |
| AT-010 | Critical Modal active | 请求 outside dismiss | 不关闭；Confirm/Cancel 仍可用 | 误触放弃关键选择 | EditMode XML |
| AT-011 | Ordinary Bottom Sheet active | 请求 outside dismiss | Sheet 关闭且返回 Handled | 无法退出普通 Sheet | EditMode XML |
| AT-012 | 已销毁或重复关闭的 view handle | 请求 close 两次 | 安全清理 stale state，无 exception | 销毁后引用崩溃 | EditMode XML |
| AT-013 | Game speed 为 Fast | 获取第一个 `PauseGame` reason | 通过 `IGameTimeService` 进入 Paused 并记住 Fast | UI pause 丢失原速度 | EditMode XML |
| AT-014 | 两个 pause reasons active | 释放其中一个 | 仍保持 Paused | 关闭内层窗口意外恢复 | EditMode XML |
| AT-015 | 原速度 Fast、最后一个 reason 释放 | 释放最后一个 reason | 恢复 Fast，不固定恢复 Normal | 玩家速度设置被覆盖 | EditMode XML |
| AT-016 | `ContinueGame` view active | 打开/关闭 view | 游戏速度不变 | 普通查看资源意外暂停 | EditMode XML |
| AT-017 | Pause reason owner 被 disable/destroy | 通知 coordinator | reason 被释放；无永久暂停 | Scene change 永久暂停 | EditMode XML |
| AT-018 | Toast queue 为空 | enqueue 普通 Toast | 成为当前 Toast | 提示不显示 | EditMode XML |
| AT-019 | 相同 type+content 连续入队 | enqueue 两次 | 合并为一个记录并更新合并信息 | Toast 刷屏 | EditMode XML |
| AT-020 | 不同 Toast 连续入队 | 完成当前 Toast | 按 FIFO 显示下一个 | 顺序混乱 | EditMode XML |
| AT-021 | 普通 Toast 已超过 expiry | 推进 queue | 过期项被丢弃 | 陈旧提示误导玩家 | EditMode XML |
| AT-022 | Important error 请求作为 Toast | enqueue | 被拒绝或升级为持久 feedback contract | 重要错误静默消失 | EditMode XML |
| AT-023 | pointer 0 从 UI press | move 到 Scene 再 release | 整个 gesture 均报告 UI owned | drag 结束穿透场景 | EditMode XML |
| AT-024 | pointer 0 从 Scene press | move 到 UI | 直到 release 均保持 Scene owned，UI 不抢同一次 gesture | gesture 中途换 owner | EditMode XML |
| AT-025 | pointer 0 release 后 | 开始新 gesture | ownership 被清除并可重新分配 | 输入永久锁死 | EditMode XML |
| AT-026 | 两个 pointer IDs | 分别注册 ownership | 每个 pointer 独立追踪 | Multi-touch 相互污染 | EditMode XML |
| AT-027 | Modal blocker active | 查询 Scene input permission | 所有 Scene pointers 返回 blocked | Modal 下仍可操作场景 | EditMode XML |
| AT-028 | Toast active，无 Modal | 查询 pointer ownership | Toast 不取得 ownership | Toast 挡住输入 | EditMode XML |
| AT-029 | motion normal | 读取 durations | 与 Theme provisional values 一致 | 动画数值散落 | EditMode XML |
| AT-030 | Reduced Motion hook enabled | 请求 transition settings | 非必要动画缩短/跳过，最终状态仍相同 | Accessibility hook 无效 | EditMode XML |
| AT-031 | Strong Frost 已有一个 owner | 第二个 Panel 请求 Strong Frost | 第二个使用 Light fallback 或等待，不出现两个 blur owner | 多重 blur 性能下降 | EditMode XML |
| AT-032 | Strong Frost unsupported/disabled | 请求 Strong Frost | 返回 readable Light Frost fallback | 低端设备不可读 | EditMode XML |
| AT-033 | Safe Area rect 输入正常/极端 inset | 计算 anchors | 输出保持在 0–1 且无负尺寸 | notch 裁切或反转 | EditMode XML |
| AT-034 | localized label 长 30–50% | 运行 layout validation fixture | 使用扩容/换行/滚动规则，不低于最小字体 | 长文字重叠 | EditMode XML |
| AT-035 | runtime assembly metadata | 检查 references | 有 uGUI 与 `Unity.TextMeshPro`，没有 `UnityEditor` | runtime build 引用 Editor | EditMode XML |
| AT-036 | Phase 5 asset paths | 运行 asset validator | Theme、Prefabs、materials、font assets 和 validation scene 位于批准路径且唯一 | 资源漂移/重复 | EditMode XML |

## 3. Integration Cases

| ID | 前置条件 | 操作 | 预期结果 | 覆盖风险 | Evidence |
|---|---|---|---|---|---|
| IT-001 | Phase 5 validation scene | Load scene | 正好一个 UI Root、3 Canvas、4 logical layers、1 EventSystem | hierarchy 重复 | PlayMode XML |
| IT-002 | Validation scene | 检查 sorting / raycasters | HUD < Screen < Toast；Panel < Modal；仅需要的 layers 接收 raycast | 层级顺序错误 | PlayMode XML |
| IT-003 | 9 个 Button variants | 模拟 press/release/disable | 3 roles × 3 states 正确切换；Disabled 不触发 action | 按钮状态错误 | PlayMode XML |
| IT-004 | Solid/Light/Strong panels | 依次打开 | variant material 正确；Strong 同时最多一个 | Prefab 与 Theme 不一致 | PlayMode XML |
| IT-005 | 真实 EventSystem、Button 覆盖可选物 | 点击 Button | Button action 一次；Scene selection 为 null | UI 点击穿透 | PlayMode XML |
| IT-006 | Bottom Sheet 覆盖部分 Scene | 点击 outside 关闭 | Sheet 关闭；同一次 release 不选择 world object | 关闭点击穿透 | PlayMode XML |
| IT-007 | pointer 在 UI 内 press | 拖到 world 后 release | 不选择 world object | drag 穿透 | PlayMode XML |
| IT-008 | pointer 完全在 world | tap selectable | world object 正常被选择 | boundary 误挡场景 | PlayMode XML |
| IT-009 | Modal + lower Button + world object | 点击 lower Button/world | 二者均不响应 | blocker 漏挡 | PlayMode XML |
| IT-010 | Modal active | 点击 Modal Confirm | 只触发 Confirm 一次并关闭 top Modal | 重复 callback/错误层关闭 | PlayMode XML |
| IT-011 | Toast 显示在 Button 上方 | 点击下面 Button | Button 正常响应 | Toast raycastTarget 挡操作 | PlayMode XML |
| IT-012 | Panel + 2 Modals | 连续触发 shared Back | B→A→Panel 顺序关闭，HUD 保留 | Back stack 错误 | PlayMode XML |
| IT-013 | Ordinary Bottom Sheet | outside tap 与 Back 各测试一次 | 两种方式都能退出 | mobile 退出死路 | PlayMode XML |
| IT-014 | Critical Modal | outside tap、Back、Cancel | outside 不关；Back contract按配置；Cancel 明确关闭 | 关键操作误关 | PlayMode XML |
| IT-015 | `PauseGame` Panel + scaled mover | 打开 Panel、等待 realtime | mover 停止；UI transition 完成 | Pause 时 UI 也冻结 | PlayMode XML |
| IT-016 | game Fast + nested PauseGame views | 依次开关 | 关闭内层仍暂停，全部关闭恢复 Fast | nested pause 恢复错误 | PlayMode XML |
| IT-017 | ContinueGame Bottom Sheet + mover | 打开并等待 | mover 继续，Sheet 可操作 | 查看面板误暂停 | PlayMode XML |
| IT-018 | Toast queue 3 条含重复项 | 推进 unscaled time | 合并重复并按顺序显示，Paused 时仍推进 | Toast queue/时钟错误 | PlayMode XML |
| IT-019 | Tooltip info action | tap info | Tooltip 出现；无需 Hover | Touch 无法获得说明 | PlayMode XML |
| IT-020 | Validation Message fixture | 提交无效值再修正 | 具体原因持续显示；修正后消失 | 错误信息瞬间消失 | PlayMode XML |
| IT-021 | Portrait reference | Force Canvas update | 无重叠、关键 controls 位于 Safe Area | reference layout 崩坏 | Screenshot + XML |
| IT-022 | Smaller/tall portrait | 切换 GameView fixture | 无关键裁切，layout 自适应 | aspect ratio 崩坏 | Screenshot + XML |
| IT-023 | Landscape fixture | 切换 landscape | 功能可用、可关闭，无关键裁切 | 横屏死路 | Screenshot + XML |
| IT-024 | 30–50% long labels + mixed CJK/Latin | Force layout | 无截断关键含义、无 overlap、字体不低于 baseline | localization overflow | Screenshot + XML |
| IT-025 | Simulated Safe Area insets | 应用 top/bottom/side insets | HUD、Back、confirm controls 均在 safe rect 内 | notch/home indicator 遮挡 | Screenshot + XML |
| IT-026 | Strong Frost unsupported flag | 打开 Strong panel | 自动显示 Light fallback，内容和 controls 相同 | fallback 功能分叉 | PlayMode XML |
| IT-027 | view 在 animation 中被 disable | disable/destroy | raycast、pause reason、pointer ownership 全部清理 | 中断后锁死 | PlayMode XML |
| IT-028 | MainCafe migrated UI | Load MainCafe | 只有一个 EventSystem/UI Root/Time panel contract，Console 无 error | migration duplication | PlayMode XML |
| IT-029 | MainCafe Time controls | 依次 press Pause/Normal/Fast | `GameTimeService` 速度与事件保持正确 | legacy time regression | PlayMode XML |
| IT-030 | validation scene in player-compatible PlayMode assembly | run tests | 不依赖 `UnityEditor`，可进入 standalone suite | Editor-only 泄漏 | Player PlayMode XML |

## 4. Regression Cases

| ID | 前置条件 | 操作 | 预期结果 | 覆盖风险 | Evidence |
|---|---|---|---|---|---|
| RT-001 | Phase 5 worktree baseline | 运行完整 EditMode suite | 全部既有 tests Passed，0 Failed/Skipped/Inconclusive（除非批准的已知项） | Phase 0–4 domain regression | XML + summary |
| RT-002 | Phase 5 worktree baseline | 运行完整 Editor PlayMode suite | 全部既有 tests Passed | Scene/runtime regression | XML + summary |
| RT-003 | player-compatible test build | 运行 standalone PlayMode suite | 全部 player-compatible tests Passed | Editor/Player 差异 | XML + player log |
| RT-004 | MainCafe | Load/Unload twice | 无 duplicate service、EventSystem、UI Root 或 stale pause/input state | Scene reload 泄漏 | PlayMode XML |
| RT-005 | existing selection fixture | UI 不覆盖时 tap world | selection/deselection events 与 Phase 0 一致 | 新 boundary 误挡 scene | PlayMode XML |
| RT-006 | existing camera input fixtures | pan/zoom/rotate/tap tests | 原有 camera behavior 通过 | UI integration 破坏 camera | Existing suite XML |
| RT-007 | existing GameTime tests | Pause/Normal/Fast/duplicate owner/unsupported speed | 所有既有 contract 保持 | time service regression | Existing suite XML |
| RT-008 | Phase 1–2 layout tests | 运行完整相关 suites | 全部通过 | UI 改动污染 domain | Existing suite XML |
| RT-009 | Phase 3 validators | 运行 production validator | `3/3 valid, 0 issues` 或当时批准 baseline | visual pipeline regression | Validator log |
| RT-010 | Phase 4 validators | 运行 production validator | `5/5 valid, 0 issues` 或当时批准 baseline | furniture architecture regression | Validator log |
| RT-011 | Build Settings isolation tests | 运行 EditMode/PlayMode checks | MainCafe 与 validation scenes scope 保持批准 contract | validation scene 污染 build | XML |
| RT-012 | runtime asmdef | 运行 assembly boundary tests | PlayMode/runtime 无 `UnityEditor` reference | mobile build failure | XML |
| RT-013 | MainCafe legacy cleanup/setup | 连续运行 deterministic setup 两次 | 结果 idempotent，不复制 UI / services | builder 重复生成 | EditMode XML |
| RT-014 | repository files | `git diff --check` + missing reference scan | 无 whitespace errors、missing scripts/assets | serialized asset 损坏 | command log |

## 5. Manual Acceptance Cases

### 5.1 执行环境

- 首轮：Unity Editor Game View，portrait `1080 × 1920` reference。
- 补充：smaller portrait、tall portrait、landscape 与 Safe Area simulator fixtures。
- Input：Touch simulation 优先；Mouse 只模拟单指 Touch，不评价 Hover。
- Scene：`Assets/Scenes/Validation/Phase5UiFoundation.unity`；最后在 `MainCafe` 做 regression spot-check。

| ID | 前置条件 | 操作 | 预期结果 | 覆盖风险 | Evidence |
|---|---|---|---|---|---|
| MT-001 | 打开 Phase5 validation scene，portrait | 截取完整画面 | A1 cream/warm wood/sage 统一，画面没有 debug 杂物 | 风格不一致 | Full-screen screenshot |
| MT-002 | 9 个 Button variants 可见 | 逐个查看 Default/Pressed/Disabled | 角色和状态一眼可区分；Disabled 仍可读 | 状态只靠细微颜色 | Screenshot + short video |
| MT-003 | Touch simulation | 依次点击所有 buttons 边缘和中心 | `48×48` target 容易点中，无误触邻近 control | 点击区域太小 | Short video |
| MT-004 | Primary/Secondary/Destructive 同屏 | 不看说明判断用途 | Primary 最突出，Destructive 明确但不过度抢眼 | 行为层级混乱 | Screenshot + owner note |
| MT-005 | Solid/Light/Strong panels | 依次切换 | 三种材质差异清楚，文字在复杂背景上始终可读 | Frost 降低可读性 | 3 screenshots |
| MT-006 | Strong Frost panel | 在动态/复杂场景背景打开 | 能看出背景模糊且无明显闪烁；关闭后正常 | 真模糊 artifacts | Short video |
| MT-007 | Strong Frost active | 请求第二个 Strong panel | 同时仍只有一个真实 blur，fallback 不突兀 | 双 blur 过重 | Short video |
| MT-008 | 强制 low-quality fallback | 打开相同 panel | 内容、按钮位置和功能不变，只降低效果 | fallback 变成另一页面 | Before/after screenshots |
| MT-009 | 普通 Bottom Sheet | tap entry、outside、再用 Back | 动画自然；outside/Back 都能关闭 | Sheet 退出不直观 | Short video |
| MT-010 | Critical Modal | 点击 outside、下层按钮、Cancel | outside 和下层无反应；Cancel 明确退出 | critical Modal 漏挡 | Short video |
| MT-011 | Panel + two Modals | 连续按 Back | 每次只关最上层，顺序容易理解，HUD 保留 | Back 关错层 | Short video |
| MT-012 | UI 覆盖可选咖啡机 fixture | 点击 UI、拖出 UI、outside-close | 三种 UI gestures 都不误选咖啡机 | click-through | Short video |
| MT-013 | UI 全部关闭 | 点击咖啡机 fixture | Scene selection 正常立即响应 | boundary 过度阻挡 | Short video |
| MT-014 | PauseGame panel + moving fixture | 打开、操作 UI、关闭 | 世界停止但 UI 流畅；关闭恢复原速度 | Pause 冻结 UI/恢复错 | Short video |
| MT-015 | ContinueGame Bottom Sheet + moving fixture | 打开并观察 | 世界继续运行，Sheet 仍容易阅读操作 | 普通 UI 意外暂停 | Short video |
| MT-016 | 触发 3 条 Toast，含重复项 | 观察完整队列 | 不刷屏、不挡操作、停留时间足够阅读 | 提示过快/遮挡 | Short video |
| MT-017 | Toast 显示时 | 点击 Toast 后方可见 control | control 正常响应 | Toast 阻挡 input | Short video |
| MT-018 | Tooltip fixtures | tap info 与 long press | 两种 Touch-safe 入口可发现、可关闭、文字可读 | 无 Hover 后信息不可达 | Short video |
| MT-019 | Validation fixture | 提交空值/无效值再修正 | 错误原因具体、位置明确、修正后消失 | 只显示红色/原因模糊 | Screenshots |
| MT-020 | portrait 1080×1920 | 打开每种 container | 无 overlap/cutoff；主操作单手容易触达 | portrait baseline 失败 | Screenshot set |
| MT-021 | smaller portrait | 重复核心流程 | 无关键 control 被挤出；必要内容可滚动 | 小屏死路 | Screenshot + video |
| MT-022 | tall portrait | 重复核心流程 | layout 不被不自然拉长，底部操作仍在 Safe Area | tall aspect 失衡 | Screenshot |
| MT-023 | landscape | 打开/关闭 Panel、Modal、Sheet | 功能完整，无关键裁切；无需达到独立精修画面 | 横屏不可用 | Screenshot + video |
| MT-024 | 模拟 notch/home inset | 重复核心流程 | HUD、Back、Confirm/Cancel 不被遮挡 | Safe Area 失败 | Screenshot set |
| MT-025 | 30–50% long labels | 查看全部 components | 关键含义完整，无文字互压，无极小字号 | localization overflow | Screenshot set |
| MT-026 | 中文 + Latin mixed fixture | 查看 Heading/Body/Label | 字重、baseline、fallback 一致，无缺字方框 | font coverage 不足 | Screenshot |
| MT-027 | 正常 motion | 连续操作 Button/Modal/Sheet/Toast | 快而清楚，不拖沓、不突然；close 稍快合理 | motion 手感差 | Short video + owner note |
| MT-028 | Reduced Motion fixture | 重复 MT-027 | motion 明显减少但状态变化仍清楚 | reduced motion 只是摆设 | Short video |
| MT-029 | animation 进行中 | 快速 Back、disable/reopen | 无卡死、闪烁、透明 blocker 或永久暂停 | 中断状态损坏 | Short video + Console screenshot |
| MT-030 | MainCafe | 操作 Pause/Normal/Fast | 外观接入新 foundation，原功能完全一致 | legacy UI migration regression | Short video |
| MT-031 | MainCafe | UI 开关后选择/取消选择 Scene objects | 选择行为正常，无一次点击触发两件事 | MainCafe input regression | Short video |
| MT-032 | Profiler + validation scene | 比较 Solid、Light、Strong、fallback | 记录 CPU/GPU/frame time、batches、overdraw observation；无第二 Strong | 性能成本未知 | Profiler captures + table |
| MT-033 | Console 清空 | 完成 MT-001–032 核心流程 | 无 unexpected Error/Exception；Warning 有解释 | 隐藏 runtime 错误 | Console screenshot/log |
| MT-034 | 关闭/重载 validation scene 两次 | 再操作 UI 与 Scene | 无永久 pause、stale blocker、重复 EventSystem/Root | lifecycle 泄漏 | Short video + Hierarchy screenshots |

## 6. Execution Gates

1. **Pre-development approval：**Studio Owner 批准本 test design 与 implementation plan。
2. **TDD gate：**每个 implementation task 先运行对应 failing automated test，再写最小 production code。
3. **Task gate：**focused tests Passed 后才进入下一 task；不以“以后一起修”跳过失败。
4. **Automated completion gate：**AT、IT、RT 全部 Passed，且保留 XML/log evidence。
5. **Manual gate：**MT-001–034 逐项记录状态与 evidence；任何 Failed 必须修复或经 Studio Owner 明确接受为已知限制。
6. **Phase completion gate：**完整 regression、validator、visual QA 和 Studio Owner final acceptance 均通过。

## 7. Traceability

- Theme/components：AT-001–005、AT-029–036、IT-003–004、IT-021–026、MT-001–008、MT-020–028。
- Navigation/Back：AT-006–012、IT-010–014、MT-009–011。
- Input boundary：AT-023–028、IT-005–011、IT-027、MT-012–013、MT-017、MT-029、MT-031。
- Pause：AT-013–017、IT-015–017、IT-027、MT-014–015、MT-029、MT-034。
- Feedback：AT-018–022、IT-018–020、MT-016–019。
- Compatibility/performance：IT-028–030、RT-001–014、MT-030–034。
