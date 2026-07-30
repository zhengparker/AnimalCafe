# Phase 0 Review Hardening Design

> 状态：Implementation、automated verification 与用户 manual acceptance 已完成；等待用户 commit
>
> 日期：2026-07-30
>
> Work Type：Code / Test / Documentation
>
> 实施位置：`main`

## 1. 背景

Phase 0、Phase 1 和 Phase 2 的 merge 前 review 没有发现 Critical issue，但在 Phase 0 找到三项 Important hardening：

1. `Phase0SceneSetup` 依赖 `GameObject.Find`，无法发现 inactive roots，也不会统一处理同名 duplicate roots。
2. selected `ColorSelectable` 被 disabled 后，`SceneInteractionController.CurrentSelection` 可能保留 stale reference。
3. `MouseCameraInput` 的真实 Input System press / drag / release 与同 frame cache contract 缺少 integration tests。

Phase 2 production placement engine review clean，但 `codex/phase-2` 基于较早的 `0777a02`。当前 `main` 已包含之后的 Phase 0 game-time ownership hardening，因此 Phase 0 review fixes 完成并进入 `main` 后，必须把最新 `main` 同步进 Phase 2，再进行 fresh regression。

## 2. Goal

在不扩大 Phase 0 gameplay scope 的前提下：

- 让 Phase 0 Scene setup 对 inactive 和 duplicate owned roots 保持确定性与 idempotency；
- 让 disabled 或 destroyed selection 都能安全清理；
- 用真实 Input System integration tests 保护 tap-vs-drag 和同 frame cache contract；
- 同步 Phase 0 documentation 与 fresh test evidence；
- 为 Phase 2 整合最新 `main` 建立明确 gate。

## 3. Not Included

本次不实现：

- Phase 2 Grid placement code；
- Phase 3 visual style、Models、Prefab pipeline 或 asset production；
- 新 input device、touch input、controller input 或 input rebinding；
- 新 selection UI、outline、tooltip 或 furniture interaction；
- Scene runtime architecture 重构；
- Save、pathfinding、decoration UI 或 Customer AI；
- commit、push、merge 或 branch/worktree deletion。

## 4. Working Tree Safety

本次直接在 `main` 实施，但必须保护用户已有修改：

- `.gitignore`
- `AnimalCafe.slnx`

这些 files 不属于本次 scope，不编辑、不 stage、不还原，也不纳入 handoff file list。

所有 source、test 和 documentation changes 必须使用精确 file paths 检查，不使用 broad staging。

## 5. Scene Ownership Hardening

### 5.1 Canonical Root Rule

以下 Scene-owned objects 各自只能保留一个 canonical object：

- `Phase0_Runtime`
- `Phase0_TimeControls`
- `EventSystem`

查找逻辑必须：

1. 只枚举当前 loaded `MainCafe` Scene 的 root objects；
2. 包含 inactive roots；
3. 不采用跨 Scene 的 global `GameObject.Find`；
4. 如果不存在，则创建一个 canonical root；
5. 如果存在一个或多个，则稳定保留第一个 Scene root，并删除其余同名 duplicates；
6. 将 canonical root 设为 active，确保后续 component configuration 可运行。

“第一个”以 `Scene.GetRootGameObjects()` 返回的 Scene hierarchy 顺序为准。本次不引入新的 persistent identifier。

### 5.2 Component Normalization

canonical objects 继续使用现有 component configuration 与 serialized-reference
configuration；`GetOrAdd<T>` 会收敛 setup-owned duplicate components，只保留 hierarchy
中第一个 component：

- `Phase0_Runtime`
  - `MouseCameraInput`
  - `CafeCameraController`
  - `SceneInteractionController`
  - `GameTimeService`
- `Phase0_TimeControls`
  - `Canvas`
  - `CanvasScaler`
  - `GraphicRaycaster`
  - `TimeControlPanel`
- `EventSystem`
  - `EventSystem`
  - `InputSystemUIInputModule`

重复 root 被删除时，其 children 和 components 一并删除。setup 不迁移 duplicate root 上的未知 user content，因为这些 names 是 Phase 0 setup-owned contracts；保留未知内容会让 ownership 结果不确定。

### 5.3 Idempotency Tests

EditMode tests 必须覆盖：

- inactive `Phase0_Runtime` 被复用并重新设为 active；
- duplicate `Phase0_Runtime` 被收敛为一个；
- inactive和 duplicate `Phase0_TimeControls` 被收敛为一个；
- inactive 和 duplicate `EventSystem` 被收敛为一个；
- setup 连续运行两次后，每个 owned root 和关键 component 仍正好一个；
- `Phase0_Demo` cleanup regression 保持通过；
- test 完成后恢复正式 `MainCafe` Scene，不能把 synthetic duplicates 保存到项目。

## 6. Selection Lifecycle Hardening

### 6.1 Valid Selection

`SceneInteractionController` 继续拥有 `CurrentSelection`。如果 current selection 同时是 Unity component，则只有满足以下条件时才有效：

- Unity object 尚未 destroyed；
- component `isActiveAndEnabled`；
- component 所在 `GameObject.activeInHierarchy`。

如果 selection 不是 Unity object，则保持现有 interface-level behavior，不额外假设 lifecycle。

### 6.2 Cleanup Behavior

在读取新 input 前，controller 检查 current selection：

- destroyed：清空 reference；
- disabled 或 inactive：调用 `Deselect()`，清空 reference；
- 发布一次 `GameEventBus.SelectionChanged(previous, null)`；
- 后续 frames 不重复发布相同 cleanup event。

`ColorSelectable.OnDisable()` 继续负责恢复 visual color，但不反向引用或查找 `SceneInteractionController`。这样 selection state ownership 仍集中在 controller，避免双向 dependency。

### 6.3 Tests

PlayMode tests 必须覆盖：

- selected object disabled 后，下一 frame selection 变为 null；
- selected GameObject inactive 后安全清理；
- selected object destroyed 后安全清理；
- cleanup event 恰好一次，previous/new references 正确；
- cleanup 后重新 enable 并再次点击仍可重新选择；
- controller 自身 disabled 时现有 `ClearSelection()` behavior 保持通过。

## 7. Mouse Input Integration Hardening

### 7.1 Test Strategy

使用 Unity Input System test support 创建 virtual `Mouse`，驱动真实 `MouseCameraInput.ReadFrame()`。tests 不通过 mock 重写 production behavior。

每个 test 必须清理：

- virtual input device；
- pressed button state；
- created GameObjects；
- `Time.timeScale`；
- `GameEventBus` test subscriptions/state。

### 7.2 Required Contracts

PlayMode tests 必须验证：

- press 和 release 未超过 threshold 时，release frame 的 `TapReleased == true`；
- movement 超过 threshold 后 release，`TapReleased == false`；
- drag distance 一旦超过 threshold，即使 pointer 回到 press position，也不能重新变成 tap；
- 同一 Unity frame 连续两次调用 `ReadFrame()` 返回相同 cached values；
- Pause（`Time.timeScale == 0`）时 input 仍可被读取；
- scroll 和 pointer delta contract 不因新增 tests 或 cleanup 改变。

如果 tests 全部直接通过，则本项只增加 regression tests，不修改 `MouseCameraInput`。只有观察到正确 RED failure 时，才做最小 production fix。

UI-click suppression 不在当前 `MouseCameraInput` contract 中；Phase 0 interaction 目前依赖 raycast/selectable result。本次不新增 EventSystem pointer-over-UI filtering，以免未经设计改变现有 input routing。

## 8. ColorSelectable Warning Contract

Approved Phase 0 design 要求 Renderer 或 material 不支持 selection color 时输出明确 warning 并保持 Scene 可运行。

本次补充 tests：

- missing Renderer：保留现有 safe disable/error behavior；
- material 同时缺少 `_BaseColor` 和 `_Color`：输出一次明确 warning，不执行无效 color write，并保持 Scene 可运行。

production fix 只处理 warning 与 safe no-op，不引入新 material、shader 或 fallback color system。

## 9. Documentation

更新：

- `Docs/AnimalCafe_Development_Roadmap.md`
- `Docs/Phase0_Beginner_Guide.md`
- `Docs/superpowers/specs/2026-07-30-phase-0-game-time-owner-hardening-design.md`

要求：

- 将旧 `16 / 16` baseline 与之后增加的 tests 清楚区分；
- 记录本次 fresh EditMode / PlayMode evidence；
- 把已经实现并验证的 game-time owner hardening status 更新为完成；
- Phase 0 保持 `Completed`，并把本次工作记录为 completed-phase hardening，而不是新 Phase；
- Phase 2 保持 `In Review`；
- 不提前宣布 Phase 2 merge 或 `Completed`。

## 10. Planned Files

### Production

- Modify: `Assets/Editor/Phase0SceneSetup.cs`
- Modify: `Assets/Scripts/Interaction/SceneInteractionController.cs`
- Modify only if a failing test requires it: `Assets/Scripts/Input/MouseCameraInput.cs`
- Modify: `Assets/Scripts/Interaction/ColorSelectable.cs`

### Tests

- Modify: `Assets/Tests/EditMode/Phase0SceneCleanupTests.cs`
- Modify: `Assets/Tests/PlayMode/Phase0PlayModeTests.cs`
- Modify only if required for Input System test APIs: `Assets/Tests/PlayMode/AnimalCafe.PlayModeTests.asmdef`

### Documentation

- Add: `Docs/superpowers/specs/2026-07-30-phase-0-review-hardening-design.md`
- Add after design approval: `Docs/superpowers/plans/2026-07-30-phase-0-review-hardening.md`
- Modify: `Docs/AnimalCafe_Development_Roadmap.md`
- Modify: `Docs/Phase0_Beginner_Guide.md`
- Modify: `Docs/superpowers/specs/2026-07-30-phase-0-game-time-owner-hardening-design.md`

## 11. TDD Order

1. Scene ownership RED tests。
2. Minimal Scene canonicalization implementation。
3. Focused EditMode GREEN。
4. Selection lifecycle RED tests。
5. Minimal controller cleanup implementation。
6. Focused PlayMode GREEN。
7. Mouse Input System integration tests；确认哪些是 regression-pass，哪些暴露真实 RED。
8. 仅对真实 RED 做最小 production fix。
9. ColorSelectable warning RED test 与 minimal fix。
10. Focused tests、full EditMode、full PlayMode。
11. Static scans、`git diff --check` 和 documentation consistency review。

## 12. Manual Acceptance

用户使用 Unity `6000.5.5f1` 打开 main：

1. 打开 `Assets/Scenes/MainCafe.unity`。
2. 确认 Hierarchy 中只有一个 `Phase0_Runtime`、一个 `Phase0_TimeControls`、一个 `EventSystem`。
3. 进入 Play Mode。
4. 验证 mouse pan、wheel zoom、selection、点击空白取消选择。
5. 验证 Pause、`1x`、`2x`。
6. 验证 Console 无 unexpected error/warning。
7. 在 Test Runner 中确认 full EditMode 和 PlayMode 结果。

用户确认后，自行使用 GitHub Desktop commit 和 push。Codex 不 commit、不 push。

## 13. Phase 2 Integration Gate

只有 P0 hardening：

- automated verification 通过；
- 用户 manual acceptance 通过；
- 用户已将 P0 changes commit 到 `main`；

才开始同步 P2。

同步 P2 时：

1. 先确认 `codex/phase-2` 当前 dirty files；
2. 保护已有的 Roadmap 与 Phase 2 Beginner Guide edits；
3. 将最新 `main` merge 到 `codex/phase-2`；
4. 解决冲突时保留 P0 hardening 与 P2 placement rules 两边的 approved behavior；
5. fresh 运行 focused P0、focused P2、full EditMode 和 full PlayMode；
6. 运行 static scans 与 `git diff --check`；
7. 停在用户 Phase 2 manual acceptance gate；
8. 不 merge P2，不标记 `Completed`，不开始 Phase 3。

## 14. Completion Gate

本次第 1–5 项只有在以下条件全部满足后才完成：

- review findings 有对应 automated coverage；
- P0 focused 和 full suites 全部通过；
- 用户完成 P0 manual acceptance；
- P0 changes 已由用户进入 `main`；
- P2 已整合最新 `main`；
- P2 integrated branch 的 focused/full suites 全部通过；
- P2 Roadmap 和 Beginner Guide corrections 被保留；
- Phase 2 仍正确停在 manual acceptance / merge gate；
- 没有修改、覆盖或提交用户原有 `.gitignore` 与 `AnimalCafe.slnx` changes。
