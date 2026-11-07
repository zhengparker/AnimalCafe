# Parker Game Studio Project Profile

## Identity

- Project: AnimalCafe
- Concept: 一款以动物经营咖啡厅为主题、自动经营与主动管理并存、由角色关系和人生变化驱动故事的经营游戏。
- Lifecycle stage: Production，按照 dependency-driven Roadmap 逐 Phase 开发。

## Technology

- Engine: Unity 6
- Languages: C#
- Important versions: Unity `6000.5.5f1`
- Target platforms: 正式目标为 Android 与 iOS；Windows / Unity Editor 用于当前开发与验证，以 Game Design 为准。
- Input methods: 当前用 Windows mouse 验证；正式移动平台使用 touch，具体适配按 Roadmap 执行。

## Sources of Truth

- Game Design: `Docs/AnimalCafe_Project_Design.md`
- Roadmap: `Docs/AnimalCafe_Development_Roadmap.md`
- Phase Development Process: `Docs/AnimalCafe_Phase_Development_Process.md`
- Specs: `Docs/superpowers/specs/`
- Plans: `Docs/superpowers/plans/`

## Creative Direction

- Design pillars: 自动经营与主动管理并存；角色驱动；有策略但不过度惩罚；复杂系统保持可读。
- Explicit non-goals: 不设置玩家咖啡厅破产、强制关店或经营失败 Game Over；不把 Open Design Questions 当作已确认需求。
- Art / Camera / UI: 固定 3/4 isometric-like Camera，不旋转；当前通过 Windows mouse 验证，并按 Game Design 为 Android / iOS touch 保留兼容空间。
- Audio: 尚未进入正式 Audio production；按 Roadmap Phase 启用 Audio Department。

## Production and Authority

- Current phase or milestone: 以 `Docs/AnimalCafe_Development_Roadmap.md` 的 live status 为准；开始任何 Phase 前先读取并确认。
- Development process: 从 Phase 6 剩余收尾开始，默认执行 `Docs/AnimalCafe_Phase_Development_Process.md` 的“Task focused、Phase 集中验收”流程。
- Completed foundations: 不从本 Profile 猜测；读取 Roadmap、approved guides、tests 和当前 Git 状态。
- Next approval gate: 以 Roadmap 的 `Current Next Step` 和当前 approved spec/plan 状态为准。
- Version-control authority: Codex 不 commit、push、merge 或删除 branch/worktree；用户使用 GitHub Desktop 处理。
- Release authority: 用户拥有最终 Go / No-Go 和发布决定权。

## Verification

- Automated tests: Task 阶段运行 focused tests 和直接相关 regression；完整 EditMode、PlayMode、integration regression 集中在 Phase 收尾运行。
- Review: Engineering、QA 和必要的 Art/UX 默认在 Phase 收尾各进行一次；Critical/Important 阻挡，Minor 默认进入 polish backlog。
- Evidence: 普通 Phase 使用精简 evidence；只有 release、Save/migration、高风险 transaction 或 Studio Owner 明确要求时才启用逐文件 hash/closure 等重型 evidence。
- Manual playtest: 视觉和交互行为必须由用户完成 Unity Play Mode 与 Console 验收。
- Compatibility targets: 当前自动与人工证据以 Windows Editor 为主；Android / iOS 真机证据单独记录，不由 Editor PASS 代替。

## Collaboration

- User experience level: Unity 和 coding 初学者；解释 reusable foundation、placeholder、Editor 操作和测试概念。
- Communication language: 一般交流、spec、plan、review、test plan 和 beginner guide 使用中文；保留 English technical identifiers。
- Documentation style: 短步骤、明确 files、risks、likely bugs、normal tests、bug tests 和 acceptance criteria。
- Available skills: Parker Game Studio 可按需调用当前环境中可用的 `superpowers` skills；调用 skill 不扩大权限。

## Project Guardrails

- Confirmed constraints: 先读 Game Design；Roadmap 管 Phase；未批准不得跨 Phase；完成工作变更前说明 rework、migration、risk 和 regression；不执行 Superseded spec/plan。
- Open decisions: 只从 `AnimalCafe_Project_Design.md` 的 `Open Design Questions` 读取，不在本 Profile 重复或擅自解决。
