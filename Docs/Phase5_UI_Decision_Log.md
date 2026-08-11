# AnimalCafe Phase 5 UI Decision Log

> **Purpose:** 保存 Phase 5 brainstorming 中已经确认的 UI foundation rules，并明确区分 `Confirmed` 与 `Provisional`。本文件不是 implementation authorization；Phase 5 仍需独立 design spec、implementation plan 和 Studio Owner approval。

## Scope Discipline

- 当前 Phase 只设计、批准和实现 Roadmap 明确列出的 Phase 5 scope。
- Future Feature Concept 可以保存，但不自动成为 Phase 5 deliverable。
- Coffee Bean、Syrup Add-on、Decoration placement、Wall Decoration、Pick-up Point 和正式 mobile adaptation 由各自 Roadmap Phase 负责。

## Confirmed Phase 5 Foundation

- Runtime UI technology：`uGUI`。
- Phase 5 新建 runtime text components 使用 `TextMeshPro`；现有 Phase 0 legacy `Unity UI Text` 不静默迁移，迁移范围必须在 approved Phase 5 spec 中单独列明并保留 regression coverage。
- Future Unity Editor tools 可以独立使用 `UI Toolkit`；同一个 runtime game screen 不混用两套 UI systems。
- 正式 runtime target 为 Android + iOS，UI 不依赖 Hover、精细 Mouse、Keyboard 或 Windows-specific workflow。Mouse 仅作为 Unity Editor development / test mapping。
- UI hierarchy：一个 `UI Root`、三个 Canvas、四个 logical layers。
  - `HUD Canvas` → `HUD Layer`
  - `Screen Canvas` → `Panel Layer`、`Modal Layer`
  - `Toast Canvas` → `Toast Layer`
- 普通窗口不得自行增加 Canvas；只有 profiling evidence 才允许调整 Canvas 拆分。
- Phase 5 core components：Button、Panel、Modal、Bottom Sheet、Text Style、Icon Container、Input Blocker、Toast、Tooltip、Validation Message、Safe Area Container。
- Runtime Button visual roles 为 `Primary / Secondary / Destructive`，每种 role 都支持 `Default / Pressed / Disabled`；正式 UX 不制作或依赖 Hover。
- UI / Scene input isolation：一次 pointer interaction 只能属于 UI 或 Scene；用于关闭 UI 的同一次点击不得继续传给 Scene。
- Modal 阻止下层 UI 与 Scene；Toast 默认不接收点击。
- 新组件由未来 gameplay Phase 按实际需求增加，并继续复用 Phase 5 tokens、layers 和 input rules。
- Figma 负责视觉目标；approved design spec 负责设计合同；Unity Prefab、Material、Shader、animation 和 C# 负责实际 runtime behavior。
- Unity 使用统一 `AnimalCafeUiTheme` 对照 Figma Variables；不把整个 Figma 页面作为图片导入 Unity。

## Provisional Baselines

这些内容允许根据 playtest、localization、device testing 和 profiling 统一调整。

### Visual Direction

- A1 palette：cream、warm wood、sage。
- 小型或常驻 UI 使用 `Light Frost`；重要大型面板可以使用 `Strong Frost`。
- 同时最多一个真实 background blur；低性能配置自动回退到 Light Frost。

### Motion

- Button press：约 `0.08–0.12 s`。
- Bottom Sheet open：约 `0.22 s`。
- Modal open：约 `0.18 s`。
- Toast fade-in：约 `0.16 s`；默认停留约 `2.5 s`。
- 关闭动画可以稍快；保留 `Reduced Motion` 扩展点。

### Resolution and Touch

- Phase 5 reference resolution：portrait `1080 × 1920`。
- Landscape 必须保持功能可用和无裁切，但 Phase 5 不制作独立的最终 landscape presentation。
- Minimum touch target：`48 × 48` logical pixels。
- 正文 baseline 不小于 `16`；小标签 baseline 不小于 `14`。
- 长文字优先扩容或换行，不无限缩小字体；测试约 30–50% 更长的 localized labels。

### Typography

- Phase 5 primary UI font baseline：`Noto Sans SC`。
- 中文和 Latin 先共用同一字体 family；标题使用较粗 weight。
- 暂不增加独立 Display Font。

### Feedback

- Toast 排队显示；重复提示合并；过旧的普通提示可以丢弃。
- Tooltip 在 Touch 上通过明确的 info action 或 long press 触发，不依赖 Hover。
- Validation Message 持续到问题解决，并说明具体原因；不能只依赖颜色。

## Future Finalization Ownership

- **Phase 47 — UI/UX Integration & Accessibility:** final typography hierarchy、Display Font need、icons、localization behavior、accessibility、Reduced Motion、global feedback consistency。
- **Phase 50 — Mobile UI Adaptation:** final portrait layout、Safe Area、touch targets、gesture priority、mobile aspect ratios 和 mobile blur budget。
- **Phase 51 — Android & iOS Platform Adaptation:** Android / iOS lifecycle、platform Save paths、permissions、device builds 和 platform-specific validation。
- **Phase 52 — Mobile Release Preparation:** Android / iOS release polish、store-ready builds、device matrix、performance、memory、battery 和 final release checklist。

任何 Provisional baseline 在对应 finalization Phase 调整时，都必须同步更新 Figma、design spec、Unity Theme / Prefabs 和相关 tests。
