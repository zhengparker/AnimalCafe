# AnimalCafe Phase 5 — UI Architecture & Design System Design

> 状态：Design 已逐节确认，等待 Studio Owner 审阅本书面规格
>
> 日期：2026-08-11
>
> Roadmap Phase：Phase 5 — UI Architecture & Design System
>
> 正式目标平台：Android + iOS（Touch-first）

## 1. 用简单的话说明这个 Phase

Phase 5 不是制作咖啡机、背包或装修页面，而是先制作这些页面以后都会使用的“UI 积木和交通规则”。

例如，玩家以后点击装修按钮时，游戏应知道：面板从哪一层打开、是否暂停游戏、返回键先关闭什么、按钮多大、点击是否会穿透到场景，以及错误信息应该怎样显示。Phase 5 先统一这些基础规则，后续功能才不需要各做一套互不兼容的 UI。

## 2. Goal 与玩家可见结果

建立一个小型、可复用、适合长期扩展的 mobile UI foundation：

- 所有 runtime UI 使用同一套 `uGUI`、层级、Theme 和交互规则。
- 按钮、面板、Modal、Bottom Sheet 和提示信息拥有一致的外观与行为。
- UI 点击不会误操作场景；Modal 打开时，下层内容不能被操作。
- Portrait 是主要设计方向；Landscape 至少保持可操作、无关键内容裁切。
- 未来功能可以复用基础组件，而不需要修改 Phase 5 来预先猜测全部页面。

## 3. Scope Boundary

### 3.1 Phase 5 包含

- Runtime UI technology 与 assembly boundary。
- `UI Root`、Canvas 与 logical layer architecture。
- 小型 reusable component library。
- `AnimalCafeUiTheme` 与基础 visual tokens。
- Panel / Modal / Bottom Sheet navigation rules。
- UI / Scene pointer ownership 与 input blocking。
- 集中的 Pause Policy。
- Toast、Tooltip 与 Validation Message behavior。
- Resolution scaling、Safe Area、长文字和 accessibility expansion points。
- Light Frost / Strong Frost 的质量等级与 fallback contract。
- Phase 0 UI compatibility 和明确的 migration boundary。
- Automated tests、integration tests 与 manual visual QA fixtures。

### 3.2 Phase 5 不包含

- Coffee Machine、Coffee Bean、Syrup、Inventory、Recipe 或 Decoration 等完整 feature pages。
- Decoration placement、家具旋转、Wall Decoration 或 Pick-up Point gameplay。
- Title Screen、tutorial / onboarding、正式 icon set 或最终 UI copy。
- Dark Mode、多个完整 UI themes 或场景装修 theme。
- 最终 Android / iOS device layout、native lifecycle、build、store release 或真实 Haptic 效果。
- 正式 UI sound、完整 accessibility polish 或 localization production pass。
- Runtime `UI Toolkit` 或 Windows release UX。
- 将整个 Figma 页面作为图片导入 Unity。

Coffee Machine 与 Decoration Mode 的已确认 UX 想法继续保存在 Game Design 中，但不是本 Phase 的 implementation deliverable。

## 4. Source of Truth 与变更顺序

发生冲突时，依次使用以下依据：

1. `Docs/AnimalCafe_Project_Design.md`：长期游戏规则。
2. `Docs/AnimalCafe_Development_Roadmap.md`：Phase scope 与顺序。
3. 本 approved design spec：Phase 5 architecture 与 behavior contract。
4. `Docs/Phase5_UI_Decision_Log.md`：confirmed / provisional 决策记录。
5. Figma：视觉目标与可交互 prototype 参考。
6. Unity Theme、Prefabs、Materials、Shader、animation 与 C#：runtime implementation。

Figma 与 Unity 不互相自动替代。视觉调整获得批准后，应同步 Figma、spec、`AnimalCafeUiTheme`、相关 Prefabs 和 tests。

## 5. Technology 与 Assembly Boundary

### 5.1 Runtime UI

- Runtime UI 使用 `uGUI`。
- Phase 5 新建文字组件使用 `TextMeshPro`。
- Future Unity Editor tools 可以独立使用 `UI Toolkit`，但同一个 runtime game screen 不混用两套 UI systems。
- Phase 5 代码继续位于现有 `AnimalCafe.Runtime` assembly；tests 分别位于现有 EditMode 与 PlayMode test assemblies。
- UI presentation 不拥有 gameplay rules。UI 通过窄接口或公开 commands 请求现有 systems 执行动作。

### 5.2 主要职责

- `AnimalCafeUiTheme`：统一保存颜色、文字、spacing、corner radius、material、motion duration 与质量 fallback references。
- UI presentation coordinator：维护当前 main Panel、Modal stack、Bottom Sheet 和 Back 顺序。
- UI pointer boundary：决定一次 pointer gesture 属于 UI 还是 Scene，并保持该归属直到 gesture 结束。
- UI pause coordinator：根据所有打开 UI 的 Pause Policy 计算是否暂停，不让单个窗口直接互相覆盖游戏速度。
- Toast queue：排队、合并重复提示并丢弃过期的普通消息。
- Reusable views：只负责显示、状态和发出用户意图，不包含 Coffee、Inventory 或 Decoration business logic。

这些是职责边界，不要求为每一句职责建立复杂 framework。Implementation plan 应优先选择少量、易读、可测试的 classes。

## 6. UI Root 与 Layer Architecture

Scene 中只有一个受管理的 `UI Root`，默认包含三个 Canvas、四个 logical layers：

| Canvas | Logical Layer | 用途 |
|---|---|---|
| `HUD Canvas` | `HUD Layer` | 常驻状态和主要入口 |
| `Screen Canvas` | `Panel Layer` | 普通 Panel 与 Bottom Sheet |
| `Screen Canvas` | `Modal Layer` | 必须先处理的 Modal 与 Input Blocker |
| `Toast Canvas` | `Toast Layer` | 非交互式 Toast，始终位于普通 UI 上方 |

规则：

- 普通窗口不得自行增加 Canvas。
- Sorting order 由 Root 集中配置，不由 feature page 任意选择。
- `Toast Layer` 默认 `raycastTarget = false`，不能挡住游戏操作。
- Modal 的 Input Blocker 必须挡住下层 UI 和 Scene。
- 只有 profiling evidence 显示 Canvas rebuild 或 overdraw 成为真实瓶颈时，才能调整 Canvas 拆分。

## 7. Reusable Component Contract

### 7.1 Button

Button visual roles：

- `Primary`：当前流程的主要确认动作。
- `Secondary`：普通选择、返回或次要动作。
- `Destructive`：删除、放弃等有明显损失风险的动作。

每个 role 必须支持：`Default / Pressed / Disabled`。不制作或依赖 Hover。Disabled 状态不只改变颜色，还必须保持足够可辨识度并停止 input。

### 7.2 Panel

Panel visual variants：

- `Solid`：最高可读性或无需显示背景时使用。
- `Light Frost`：HUD 与常用小型面板的默认选择。
- `Strong Frost`：重要大型面板的增强效果，同时最多一个 active。

### 7.3 Modal 与 Input Blocker

- Modal 位于 `Modal Layer`，打开时阻止下面的 UI 与 Scene input。
- Critical Modal 必须提供明确 Confirm / Cancel，不依赖 outside tap。
- Modal 可以叠加，但 Back 永远只关闭最上层一个。
- Modal 自己声明 Pause Policy；不得直接写 `Time.timeScale`。

### 7.4 Bottom Sheet

- 从屏幕底部出现，是 mobile 常用的内容容器。
- Ordinary Bottom Sheet 可以通过 outside tap 或 Back 关闭。
- Critical choice 不使用 outside tap 作为唯一退出方式。
- 本 Phase 只制作通用容器和示例内容，不制作未来 gameplay 页面。

### 7.5 Text Style 与 Icon Container

- Text styles 至少包括 `Heading / Body / Label`。
- Phase 5 font baseline 为 `Noto Sans SC`；Heading 使用较粗 weight。
- Body baseline 不小于 `16`，小 Label baseline 不小于 `14`。
- Icon Container 提供 `24 / 32` logical size placeholders 与一致 alignment。
- Phase 5 使用清楚的 placeholder icons；正式 icon family 在 Phase 47 完成。

### 7.6 Feedback Components

- `Toast`：短暂、非阻塞、默认不可点击。
- `Tooltip`：通过明确 info action 或 long press 打开，不依赖 Hover。
- `Validation Message`：留在相关 control 附近直到问题解决，并说明具体原因；不能只靠红色表达错误。
- `Safe Area Container`：为 notch、camera cutout 和 home indicator 保留统一入口；最终 device tuning 属于 Phase 50/51。

未来 feature 真正需要时，再增加 `Tab / List / Slot / Resource Row` 等组件；Phase 5 不提前实现空框架。

## 8. Navigation、Back 与显示规则

- 同一时间最多一个 main Panel。
- 打开新的 main Panel 时，coordinator 负责关闭或替换旧 Panel，不能由页面互相寻找。
- Modal 可以位于 main Panel 上方；Toast 可以显示在所有普通层上方。
- Back 顺序：最上层 Modal → Bottom Sheet / main Panel → 无动作。HUD 不因 Back 消失。
- Ordinary Bottom Sheet 可以 outside tap dismiss；critical Modal 只能通过明确 action 结束。
- 打开与关闭完成后，UI focus / selection 不得留在已经 inactive 或销毁的 object 上。
- Android system Back 与 iOS in-app Back 的 platform mapping 在 Phase 51 接入，但都必须调用同一个 shared Back contract。

## 9. UI / Scene Input Boundary

现有 `SceneInteractionController` 会在 tap release 时检查 `EventSystem.current.IsPointerOverGameObject()`。这能处理基础情况，但不足以保证“从 UI 开始、在 UI 外结束”的 drag，或“点击关闭面板后同一次 release 穿透到 Scene”。

Phase 5 contract：

- 一次 pointer gesture 从 press 到 release 只能属于 UI 或 Scene。
- gesture 如果从 UI 开始，直到结束都属于 UI，即使手指移出 control。
- 用于关闭 Panel、Modal 或 Bottom Sheet 的同一次 gesture 不得继续选择 Scene object。
- Modal active 时，所有 Scene pointer input 被阻止。
- Toast 默认不取得 pointer ownership。
- Mouse 只作为 Unity Editor 中 Touch 的 test mapping；不得产生另一套正式 Mouse UX。
- Multi-touch 预留独立 pointer ID；camera pan / pinch 的完整优先级在 Phase 50/51 finalization。

Implementation 不应让每个 Scene controller 各自猜 UI 状态；应提供一个共享、可查询的 input boundary，现有 `SceneInteractionController` 通过它决定是否处理 tap。

## 10. Pause Policy

每个可打开的 Panel / Modal 声明以下 policy 之一：

- `ContinueGame`：UI 打开时游戏继续，例如未来咖啡机资源 Bottom Sheet。
- `PauseGame`：UI 打开时暂停，例如未来 Decoration Mode。

规则：

- 所有 pause reasons 由一个 coordinator 集中计算。
- 只要仍有一个 active `PauseGame` reason，关闭其他窗口不能恢复游戏。
- UI pause 必须通过现有 `IGameTimeService` / `GameTimeService` contract 改变速度，不直接写 `Time.timeScale`。
- 当第一个 UI pause reason 出现时，保存进入 UI 前的有效 `GameSpeed`；最后一个 reason 消失时恢复该速度，而不是永远恢复 `Normal`。
- 游戏处于 Paused 时，UI 使用 unscaled time，按钮、Back、Modal 和 Toast 仍可操作。
- Existing Pause / Normal / Fast controls 的 player behavior 必须保留；具体 migration 在 implementation plan 中明确列出并测试。

## 11. Visual Tokens 与 Theme

### 11.1 A1 Visual Direction

- 主色方向：cream、warm wood、sage。
- UI 以柔和圆角、清晰边界和 mostly matte 表面为主。
- Frost 是层次增强，不是可读性或功能成立的前提。
- 当前只制作一个 UI theme；Theme data structure 保留未来 accessibility variant 的扩展空间。
- 场景 Decoration theme 是另一套内容概念，不属于 `AnimalCafeUiTheme`。

### 11.2 Token Groups

`AnimalCafeUiTheme` 至少集中管理：

- semantic colors：background、surface、text、accent、disabled、warning / destructive。
- typography：Heading、Body、Label font asset、size、weight 与 line spacing。
- spacing：一套有限、可复用的 padding / gap values。
- corner radius 与 borders。
- component materials：Solid、Light Frost、Strong Frost 与 fallback。
- motion durations。
- minimum touch target 与 icon container sizes。

Components 读取 semantic token，不把相同颜色和尺寸散落复制在多个 Prefab 中。

## 12. Frost、Performance 与 Fallback

- `Light Frost` 使用低成本的半透明、tint、border 与柔和 highlight 模拟磨砂，不要求实时看到模糊后的场景。
- `Strong Frost` 可以显示真实 background blur，但同一时间最多一个 active。
- Strong Frost 应采用 shared / downsampled background capture，而不是每个 Panel 各自重复模糊整屏。
- Blur 更新频率、resolution 和 sample count 必须可配置。
- 不支持或性能不足时自动使用可读的 Light Frost fallback；功能、文字和点击区域不得改变。
- Phase 5 建立 effect 与 fallback contract 并做 Editor / representative fixture 验证；最终 Android / iOS device budget 在 Phase 50–52 确定。

## 13. Motion 与 Feedback Timing

以下是允许 playtest 调整的 provisional baselines：

| Motion | Baseline |
|---|---:|
| Button press | `0.08–0.12 s` |
| Bottom Sheet open | `0.22 s` |
| Modal open | `0.18 s` |
| Toast fade-in | `0.16 s` |
| Toast default stay | `2.5 s` |

- Close animation 可以比 open 稍快。
- UI motion 使用 unscaled time。
- 所有重要状态必须在 animation 被跳过时仍正确。
- Theme 保留 `Reduced Motion` hook；最终选项在 Phase 47 完成。
- Phase 5 只提供 UI sound 与 Haptic 的调用 hook，不提供正式 sounds 或 device vibration。正式 UI sound 属于 Phase 47，Android / iOS Haptic 属于 Phase 51。

Toast rules：

- 同时只展示一个主要 Toast，其余排队。
- 相同类型、相同内容的连续 Toast 合并，避免刷屏。
- 普通低优先级 Toast 过期后可以丢弃；重要错误不得静默丢失，应改用 Validation Message 或 Modal。

## 14. Resolution、Safe Area 与文字扩展

- Phase 5 reference resolution：portrait `1080 × 1920`。
- 使用 Canvas Scaler、anchors、Layout Groups 和 content-driven sizing，而不是只针对一个画面写死位置。
- Landscape 必须功能可用、主要 controls 可见且没有关键内容裁切，但本 Phase 不制作独立 polished landscape composition。
- Minimum touch target：`48 × 48` logical pixels；视觉图形可以更小，但实际可点击区域不能更小。
- 测试约 `30–50%` 更长的 localized labels。
- 长文字优先扩容、换行或允许内容滚动，不无限缩小字号。
- 颜色不能是状态的唯一表达方式；Disabled、error 与 selection 至少还使用 shape、icon、copy 或 opacity distinction。
- Safe Area foundation 现在建立，最终 mobile aspect 与 device tuning 在 Phase 50；platform integration 在 Phase 51。

## 15. Legacy UI Compatibility 与 Migration

现有 Phase 0 `TimeControlPanel` 使用三个 uGUI Button 连接 `GameTimeService`，文字仍可能使用 legacy `Unity UI Text`。Phase 5 不静默重写已完成系统。

Implementation plan 必须在写代码前列出其中一种明确方案：

1. 保留 legacy panel behavior，只让它接入新的 Root / Theme / input contract；或
2. 用等价 TMP presentation 替换视觉 Prefab，同时保留相同 `GameTimeService` behavior。

无论选择哪种：

- Pause / Normal / Fast 的行为与事件不能回退。
- 不能出现两个 active `GameTimeService` owner。
- Scene selection、camera input、Phase 1–4 scenes 与 existing automated tests 必须继续通过。
- Migration 必须是独立、可回退的小步骤，不能夹带 feature UI。

## 16. Error Handling 与 Recovery

- 缺少 required UI reference 时，component 应输出带 context 的明确 error 并安全停止该 component，不产生连续 exception spam。
- Theme 或 optional material 缺失时，优先使用 readable solid fallback。
- Coordinator 收到重复 close、无效 Panel handle 或已经销毁的 view 时，应安全忽略并清理 stale state。
- Scene change / object disable 时必须释放 pointer ownership 与 pause reason，避免永久无法点击或永久暂停。
- Animation 中断后，最终 active state、raycast state 和 layer state 必须一致。

## 17. Verification Contract

### 17.1 EditMode tests

- Theme token completeness 与 semantic references。
- Navigation / Back stack rules。
- One-main-Panel invariant。
- Nested Pause Policy：最后一个 pause reason 释放后才恢复进入前速度。
- Toast ordering、duplicate merge 与 expiry。
- Motion / duration values 接受 provisional configuration。
- Minimum touch target validation helpers。

### 17.2 PlayMode integration tests

- 使用真实 Canvas、GraphicRaycaster、EventSystem 与 TMP components。
- 点击 UI Button 不会触发 Scene selection。
- outside tap 关闭 Bottom Sheet 时，同一次 tap 不穿透到 Scene。
- 从 UI 开始并拖到 Scene 的 gesture 仍属于 UI。
- Modal 阻止 lower UI 与 Scene；关闭后恢复正确交互。
- Toast 不阻挡下面的合法 input。
- Paused 时 Button、Back、Modal 与 Toast animation 仍可用。
- Multiple Modal / Panel close order 与 pause restoration。
- 现有 Time controls 和 Scene selection regression。

### 17.3 Layout 与 visual fixtures

- Portrait `1080 × 1920` reference screenshot / manual comparison。
- 至少一组 smaller portrait、一组 tall portrait 与一组 landscape functional fixture。
- Safe Area inset simulation。
- `30–50%` long-label fixtures 与中文 / Latin mixed text。
- Primary / Secondary / Destructive 的三种 states 可辨识。
- Solid / Light Frost / Strong Frost fallback 可读性。
- Minimum `48 × 48` touch target inspection。

### 17.4 Performance checks

- Strong Frost 同时最多一个 active。
- Strong Frost off / on / fallback 的 frame-time 与 overdraw comparison。
- Canvas rebuild 没有因普通窗口创建额外 Canvas。
- 最终 device thresholds 不在 Phase 5 假定；结果记录并交给 Phase 50–52 finalization。

## 18. Acceptance Criteria

Phase 5 只有在以下条件全部满足后才可完成：

- 一个 `UI Root`、三个 Canvas、四个 logical layers 按 contract 工作。
- Core component library 可在独立 test/demo fixture 中重复使用。
- Button 3 roles × 3 states、三种 Panel variants 和反馈 components 可见且可操作。
- UI / Scene pointer ownership、Modal blocking、Back 和 Pause Policy 通过 tests。
- Portrait reference、Landscape functional、Safe Area 与 long-text fixtures 通过。
- Light / Strong Frost fallback 保持可读且没有功能差异。
- Existing Phase 0–4 regression suites 通过。
- Beginner Guide 说明如何在 Unity 中安全使用这些基础组件。
- Spec、Decision Log、Roadmap、Figma reference 与 implementation 保持一致。
- Studio Owner 完成 manual visual / interaction acceptance。

## 19. Provisional Baselines 的最终归属

- **Phase 47 — UI/UX Integration & Accessibility：**最终 typography hierarchy、Display Font need、formal icons、localization behavior、Reduced Motion、UI sounds 与整体 feedback consistency。
- **Phase 50 — Mobile UI Adaptation：**最终 portrait layout、Safe Area、touch targets、gesture priority、mobile aspect ratios 与 Strong Frost mobile budget。
- **Phase 51 — Android & iOS Platform Adaptation：**platform Back、lifecycle、device builds、native Safe Area 与 Haptic implementation。
- **Phase 52 — Mobile Release Preparation：**最终 device matrix、performance、memory、thermal、battery 与 store-ready release quality。

这些 Phase 可以调整 provisional 数值，但必须同步更新 Figma、这份 design contract、Unity Theme / Prefabs 和 tests，不能只在代码里悄悄改变。

## 20. Approval Gate 与下一步

本文件批准后，下一步才是单独编写 Phase 5 implementation plan。Implementation plan 必须：

- 先盘点现有 Scene、Prefabs、fonts、Materials、Shader 与 tests。
- 列出具体新增 / 修改 files 及每个 file 的用途。
- 将 implementation 拆成小步 TDD tasks，并明确 legacy UI migration choice。
- 在任何 production code 开发前，完整列出并设计 Phase 5 automated test cases、integration / regression test cases 与 manual test cases；每个 case 都必须写明前置条件、操作、预期结果和覆盖的风险。
- 将 manual test cases 编号，形成可逐项执行、记录 Passed / Failed / Blocked 和 evidence 的验收清单。
- 说明每一步的 regression、manual QA 与 rollback 方法。
- 再次获得 Studio Owner approval 后才开始 Unity implementation。

开发环境还必须遵守以下顺序：

1. Studio Owner 批准 design spec。
2. 完成 implementation plan、全部 automated / manual test-case design，并由 Studio Owner 批准。
3. 从当时经过验证的目标 branch 创建独立的 `codex/phase-5-ui-architecture` development branch。
4. 在独立 worktree folder 中 checkout 该 branch；不得直接在主工作目录开发 Phase 5。
5. 验证 worktree 的 branch、HEAD、clean / known-dirty baseline、Unity project path 与现有 regression baseline。
6. 才能按照 approved plan 开始 TDD implementation。

Branch 或 worktree 的创建不包含 commit、push、merge 或删除权限；这些动作仍需分别获得 Studio Owner 明确授权。

批准本 design spec 不授权 commit、push，也不授权实现 Phase 6 或未来 feature UI。
