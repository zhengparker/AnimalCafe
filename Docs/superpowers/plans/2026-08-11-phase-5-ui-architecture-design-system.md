# AnimalCafe Phase 5 — UI Architecture & Design System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:using-git-worktrees` before execution, then use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 建立 Android + iOS Touch-first 的 uGUI foundation，使后续 feature UI 共用 Theme、components、layers、navigation、Pause、feedback 和 UI/Scene input boundary。

**Architecture:** 一个 `UI Root` 管理三个 Canvas 与四个 logical layers；小型 coordinators 分别负责 navigation、pause、pointer ownership 和 Toast queue。视觉数据集中在 `AnimalCafeUiTheme`，真实 uGUI/TMP Prefabs 通过 deterministic Editor builder 生成并由 validator 检查；Phase 5 不包含 gameplay feature pages。

**Tech Stack:** Unity `6000.5.5f1`、uGUI `2.5.0`、TextMeshPro、Input System `1.19.0`、URP `17.5.0`、Unity Test Framework `1.7.0`、NUnit。

## Global Constraints

- 正式目标平台：Android + iOS；Touch-first；Mouse 只用于 Unity Editor test mapping；无 Hover UX。
- 新 runtime text 使用 TextMeshPro；同一个 runtime screen 不混用 UI Toolkit。
- 只有一个 UI Root、三个 Canvas、四个 logical layers；普通窗口不增加 Canvas。
- Button roles：Primary / Secondary / Destructive；states：Default / Pressed / Disabled。
- 同时最多一个 main Panel；Strong Frost 同时最多一个 active，并必须有 Light fallback。
- 一次 pointer gesture 只属于 UI 或 Scene；关闭 UI 的同一次 gesture 不穿透 Scene。
- UI 不拥有 gameplay rules；Pause 通过 existing `IGameTimeService` / `GameTimeService`，不直接写 `Time.timeScale`。
- Portrait reference `1080 × 1920`；Landscape functional；minimum touch target `48 × 48` logical pixels。
- Body / Label baseline 不小于 `16 / 14`；font baseline `Noto Sans SC`。
- Phase 5 不实现 Coffee Machine、Syrup、Inventory、Decoration 或其他 feature pages。
- 不 commit、push、merge 或删除 branch/worktree，除非 Studio Owner 对该动作明确授权。
- Test design：`Docs/superpowers/specs/2026-08-11-phase-5-ui-test-cases.md`；任何 production code 前先批准全部 test cases。

---

## File Structure Locked by This Plan

### Runtime foundation

- Create `Assets/Scripts/UI/Foundation/AnimalCafeUiTheme.cs` — Theme ScriptableObject 与 semantic tokens。
- Create `Assets/Scripts/UI/Foundation/UiThemeTypes.cs` — Button/Panel/text/motion enums 和 serializable token structs。
- Create `Assets/Scripts/UI/Foundation/UiLayer.cs` — 四个 logical layer identifiers。
- Create `Assets/Scripts/UI/Foundation/UiRoot.cs` — Root references、layer lookup 与 required-reference validation。
- Create `Assets/Scripts/UI/Foundation/UiView.cs` — Panel/Modal/Bottom Sheet 共用 lifecycle、Pause Policy 与 close contract。
- Create `Assets/Scripts/UI/Foundation/UiNavigationCoordinator.cs` — one-main-Panel、modal stack、outside dismiss 与 shared Back。
- Create `Assets/Scripts/UI/Foundation/UiPauseCoordinator.cs` — reason-based Pause 与 previous-speed restoration。
- Create `Assets/Scripts/UI/Foundation/UiPointerBoundary.cs` — per-pointer gesture ownership 与 global Scene blocking。
- Create `Assets/Scripts/UI/Foundation/UiTransitionRunner.cs` — unscaled-time transitions 与 Reduced Motion hook。
- Create `Assets/Scripts/UI/Foundation/StrongFrostLease.cs` — single-owner Strong Frost 与 fallback decision。

### Reusable views and feedback

- Create `Assets/Scripts/UI/Components/AnimalCafeButtonView.cs` — Button role/state Theme binding。
- Create `Assets/Scripts/UI/Components/AnimalCafePanelView.cs` — Solid/Light/Strong variant binding。
- Create `Assets/Scripts/UI/Components/AnimalCafeModalView.cs` — Modal + blocker behavior。
- Create `Assets/Scripts/UI/Components/AnimalCafeBottomSheetView.cs` — ordinary outside-dismiss container。
- Create `Assets/Scripts/UI/Components/AnimalCafeTextStyle.cs` — TMP Heading/Body/Label binding。
- Create `Assets/Scripts/UI/Components/SafeAreaContainer.cs` — safe rect anchors。
- Create `Assets/Scripts/UI/Feedback/ToastMessage.cs` — Toast immutable value contract。
- Create `Assets/Scripts/UI/Feedback/ToastQueue.cs` — queue/merge/expiry pure rules。
- Create `Assets/Scripts/UI/Feedback/ToastView.cs` — non-raycast Toast presentation。
- Create `Assets/Scripts/UI/Feedback/TooltipView.cs` — info/long-press presentation hook。
- Create `Assets/Scripts/UI/Feedback/ValidationMessageView.cs` — persistent specific validation feedback。

### Existing runtime modifications

- Modify `Assets/Scripts/Interaction/SceneInteractionController.cs` — consult `UiPointerBoundary` instead of relying only on release-time `IsPointerOverGameObject()`。
- Modify `Assets/Scripts/UI/TimeControlPanel.cs` — preserve Pause/Normal/Fast behavior while accepting Theme/new Root integration; use existing service contract。
- Modify `Assets/Scripts/AnimalCafe.Runtime.asmdef` — add `Unity.TextMeshPro` reference only if compilation proves it is required。

### Editor production and validation

- Create `Assets/Editor/Phase5/Phase5UiAssetPaths.cs` — approved asset paths in one place。
- Create `Assets/Editor/Phase5/Phase5UiAssetBuilder.cs` — deterministic Theme/material/Prefab construction。
- Create `Assets/Editor/Phase5/Phase5UiValidationSceneSetup.cs` — deterministic validation scene builder。
- Create `Assets/Editor/Phase5/Phase5UiValidator.cs` — hierarchy, Theme, Prefab, font, touch target and Strong Frost contract checks。
- Create `Assets/Editor/Phase5/Phase5UiValidationMenu.cs` — explicit Build/Validate menu actions。
- Modify `Assets/Editor/Phase0SceneSetup.cs` — migrate MainCafe to one UI Root without duplicating Phase 0 runtime services。

### Generated assets

- Create `Assets/UI/Phase5/Theme/AnimalCafeUiTheme.asset`。
- Create `Assets/UI/Phase5/Fonts/` — approved Noto Sans SC source/license and TMP Font Asset; exact source file must pass license gate before import。
- Create `Assets/UI/Phase5/Materials/M_UI_Solid.mat`。
- Create `Assets/UI/Phase5/Materials/M_UI_LightFrost.mat`。
- Create `Assets/UI/Phase5/Materials/M_UI_StrongFrost.mat` and readable fallback reference。
- Create reusable Prefabs under `Assets/UI/Phase5/Prefabs/` for UI Root, Buttons, Panels, Modal, Bottom Sheet, Toast, Tooltip, Validation Message and Safe Area fixture。
- Create `Assets/Scenes/Validation/Phase5UiFoundation.unity` through the deterministic builder, not by hand-editing Unity YAML。

### Tests and docs

- Create focused EditMode tests under `Assets/Tests/EditMode/Phase5/` grouped as Theme, Navigation, Pause, Pointer, Toast and Validator tests。
- Create focused PlayMode tests under `Assets/Tests/PlayMode/Phase5/` grouped as Components, InputBoundary, PauseNavigation, Feedback, Layout and SceneIntegration tests。
- Modify existing Phase 0 tests only where the approved one-UI-Root migration changes the expected hierarchy; do not weaken behavior assertions。
- Create `Docs/Phase5_Beginner_Guide.md` — beginner-readable component usage and troubleshooting。
- Update `Docs/Phase5_UI_Decision_Log.md`、design spec、test-case status and Roadmap only after verified results。

## Public Contracts Locked for Implementation

```csharp
public enum UiLayer { Hud, Panel, Modal, Toast }
public enum UiButtonRole { Primary, Secondary, Destructive }
public enum UiButtonState { Default, Pressed, Disabled }
public enum UiPanelStyle { Solid, LightFrost, StrongFrost }
public enum UiTextStyle { Heading, Body, Label }
public enum UiPausePolicy { ContinueGame, PauseGame }
public enum UiPointerOwner { None, Ui, Scene }

public interface IUiBackHandler { bool TryHandleBack(); }
public interface IUiPauseHandle : System.IDisposable { }
public interface IUiPointerBoundary
{
    UiPointerOwner GetOwner(int pointerId);
    bool IsSceneInputAllowed(int pointerId);
}
```

`UiPauseCoordinator` consumes `IGameTimeService`; `SceneInteractionController` consumes `IUiPointerBoundary`. Concrete MonoBehaviours may expose `Configure(...)` methods for deterministic tests, following existing project style.

---

### Task 0: Approve Tests, Create Isolated Branch/Worktree and Prove Baseline

**Files:**
- Review: design spec, this plan and test-case document
- No production files change

**Cases:** Pre-development gate; RT-001–014 baseline

- [ ] **Step 1: Obtain written approval for all three documents**

Record Studio Owner approval for design spec, implementation plan and test cases. Approval does not include commit/push.

- [ ] **Step 2: Resolve how approved uncommitted documents enter the worktree**

Preferred: request explicit authorization for a docs-only commit on the target branch. If denied, recreate only the approved document diffs inside the new worktree with `apply_patch`; never copy unrelated dirty changes.

- [ ] **Step 3: Load required worktree skill and inspect current state**

Use `superpowers:using-git-worktrees`. Record current branch, HEAD, all worktrees, `git status --short`, and candidate target path before mutation.

- [ ] **Step 4: Create isolated branch and worktree**

Create branch `codex/phase-5-ui-architecture` from the approved, verified target HEAD and checkout under the repository's established worktree parent (expected `.worktrees/phase-5-ui-architecture`, but verify the existing convention first).

- [ ] **Step 5: Verify isolation**

Inside the worktree, confirm branch name, HEAD, absolute Unity project path, known-clean/known-dirty state, and absence of unrelated user changes.

- [ ] **Step 6: Run baseline matrix**

Run full EditMode, Editor PlayMode, player-compatible PlayMode, Phase 3 validator, Phase 4 validator and `git diff --check`. Expected: results equal the last approved baseline or any difference is investigated before code.

- [ ] **Step 7: Stop at checkpoint**

Present evidence and request Studio Owner permission to begin Task 1. Do not commit or push unless separately authorized.

### Task 1: Add Theme Types and Validation Before Assets

**Files:** Theme types/runtime theme; `Assets/Tests/EditMode/Phase5/AnimalCafeUiThemeTests.cs`

**Cases:** AT-001–005, AT-029–035

**Produces:** enums and `AnimalCafeUiTheme.Validate(List<string>)`; no Prefabs yet.

- [ ] Write failing tests for complete tokens, missing token, `16/14`, `48×48`, 3×3 Button matrix and motion values.
- [ ] Run only `AnimalCafeUiThemeTests`; verify failures are caused by missing Phase 5 types.
- [ ] Implement the enums/token structs and minimal ScriptableObject validation using semantic fields, not feature-specific fields.
- [ ] Run focused EditMode tests; expected all Task 1 tests Passed.
- [ ] Run runtime assembly boundary test; add `Unity.TextMeshPro` asmdef reference only if needed.
- [ ] Review diff; request authorization before any proposed focused commit.

### Task 2: Implement Navigation and View Lifecycle Rules

**Files:** `UiView.cs`, `UiNavigationCoordinator.cs`, `Assets/Tests/EditMode/Phase5/UiNavigationCoordinatorTests.cs`

**Cases:** AT-006–012

**Consumes:** `UiPausePolicy`; **Produces:** `OpenMainPanel`, `PushModal`, `OpenBottomSheet`, `TryHandleBack`, `RequestOutsideDismiss`, safe close handles.

- [ ] Write one failing test per AT-006–012, including destroyed and duplicate-close handles.
- [ ] Verify focused suite fails before production implementation.
- [ ] Implement a single main Panel reference, modal stack and explicit Bottom Sheet reference; HUD is never registered as closable navigation content.
- [ ] Make critical/ordinary dismiss behavior data-driven on the view contract.
- [ ] Run focused tests and `git diff --check`; inspect all lifecycle cleanup branches.
- [ ] Stop for review/commit authorization checkpoint.

### Task 3: Implement Reason-based Pause Coordination

**Files:** `UiPauseCoordinator.cs`, `Assets/Tests/EditMode/Phase5/UiPauseCoordinatorTests.cs`, later PlayMode fixture

**Cases:** AT-013–017

**Consumes:** `IGameTimeService`; **Produces:** `IUiPauseHandle Acquire(object owner)` and safe `Release`/owner cleanup.

- [ ] Write a fake `IGameTimeService` and failing tests for Fast→Paused→Fast, nested reasons, ContinueGame and owner cleanup.
- [ ] Verify failures before implementation.
- [ ] Implement first-reason previous-speed capture and last-reason restoration without direct `Time.timeScale` writes.
- [ ] Ensure duplicate disposal is idempotent and destroyed owner cleanup cannot resume while another reason exists.
- [ ] Run Task 3 tests plus existing GameTime tests.
- [ ] Stop for review/commit authorization checkpoint.

### Task 4: Implement Pointer Ownership and Scene Boundary

**Files:** `UiPointerBoundary.cs`, modify `SceneInteractionController.cs`, EditMode pointer tests, PlayMode input tests

**Cases:** AT-023–028, IT-005–011

**Consumes:** pointer IDs/EventSystem events; **Produces:** `IUiPointerBoundary` query used by Scene interaction.

- [ ] Write failing pure tests for UI-start, Scene-start, release clearing, two pointer IDs, Modal block and Toast non-ownership.
- [ ] Write failing real EventSystem tests for click, outside-close and UI-to-Scene drag.
- [ ] Implement pointer registration methods invoked by Root/UI event hooks and a global Scene block count.
- [ ] Modify `SceneInteractionController.Configure` with an optional/testable boundary dependency while preserving no-UI and no-EventSystem selection behavior.
- [ ] Run AT-023–028, IT-005–011 and existing real UI pointer tests.
- [ ] Stop for review/commit authorization checkpoint.

### Task 5: Implement Toast and Persistent Feedback Rules

**Files:** Toast value/queue/view, Tooltip/Validation views, feedback EditMode/PlayMode tests

**Cases:** AT-018–022, IT-018–020

**Produces:** `ToastQueue.Enqueue`, `TryGetCurrent`, `CompleteCurrent`, expiry/merge rules; views contain no gameplay logic.

- [ ] Write failing queue tests for first item, FIFO, duplicate merge, expiry and important-error rejection.
- [ ] Implement immutable Toast identity/priority and queue rules.
- [ ] Write real view tests proving unscaled timing and `raycastTarget=false`.
- [ ] Implement minimal Toast/Tooltip/Validation views with specific message text input.
- [ ] Run feedback suites while game time is Paused.
- [ ] Stop for review/commit authorization checkpoint.

### Task 6: Implement Reusable Components and Transitions

**Files:** Button, Panel, Modal, Bottom Sheet, TextStyle, TransitionRunner and StrongFrostLease; component tests

**Cases:** AT-030–032, IT-003–004, IT-010, IT-013–014, IT-026–027

**Consumes:** Theme/navigation/pause/pointer contracts; **Produces:** reusable components only.

- [ ] Write failing tests for Theme binding, Disabled input, critical dismiss, Strong lease/fallback, unscaled transition and interrupted cleanup.
- [ ] Implement minimal view components with serialized references validated once and explicit safe fallback.
- [ ] Implement transitions with unscaled time; final active/raycast state must be correct when skipped/interrupted.
- [ ] Implement Strong Frost lease independent of shader details.
- [ ] Run focused PlayMode tests including interruption during animation.
- [ ] Stop for review/commit authorization checkpoint.

### Task 7: Implement Safe Area and Responsive Layout Foundation

**Files:** `SafeAreaContainer.cs`, layout tests and fixtures

**Cases:** AT-033–034, IT-021–025

**Produces:** normalized safe rect anchor calculation and deterministic layout fixtures.

- [ ] Write failing safe-rect tests for normal, top/bottom/side and extreme insets.
- [ ] Implement clamped normalized anchor calculation without device-specific assumptions.
- [ ] Build test fixtures for reference, small, tall, landscape and 30–50% long labels.
- [ ] Verify Body/Label never drop below baseline and critical controls remain inside safe rect.
- [ ] Capture automated screenshots where supported; leave aesthetic judgment to MT cases.
- [ ] Stop for review/commit authorization checkpoint.

### Task 8: Build Theme, Materials, TMP Font and Prefabs Deterministically

**Files:** Phase 5 Editor builder/path files; generated assets under `Assets/UI/Phase5/`

**Cases:** AT-001–005, AT-036, IT-001–004

- [ ] Verify Noto Sans SC source and redistribution license; record source/license beside the font asset. Stop if license evidence is absent.
- [ ] Write failing builder idempotency/path tests before generating assets.
- [ ] Implement deterministic builder for Theme, materials and Prefabs; rerunning updates canonical assets without duplicates.
- [ ] Build 3×3 Buttons, three Panel variants and feedback/container Prefabs using TMP and Theme references.
- [ ] Use placeholder vector/simple icons only; do not import full Figma screenshots.
- [ ] Run builder twice, validator tests and missing-reference scan.
- [ ] Stop for visual review/commit authorization checkpoint.

### Task 9: Build Validation Scene and Phase 5 Validator

**Files:** validation scene setup/menu/validator; Phase 5 validator and scene tests; generated `Phase5UiFoundation.unity`

**Cases:** AT-036, IT-001–027, MT-001–029, MT-032–034

- [ ] Write failing validator tests for duplicate Root/Canvas/EventSystem, missing layers/tokens/font, small touch targets, raycast mistakes and multiple Strong Frost owners.
- [ ] Implement validator with stable issue codes and asset/object paths.
- [ ] Implement deterministic validation scene containing component gallery, selectable world fixture, moving scaled-time fixture, long text, Safe Area and feedback controls.
- [ ] Run scene builder twice and prove idempotency.
- [ ] Run focused EditMode/PlayMode matrix and confirm validation scene is not unintentionally added to production Build Settings.
- [ ] Stop for Studio Owner validation-scene review.

### Task 10: Migrate MainCafe and Preserve Phase 0 Contracts

**Files:** modify `Phase0SceneSetup.cs`, `TimeControlPanel.cs`, MainCafe generated hierarchy, Phase 0/Phase 5 scene tests

**Cases:** IT-028–029, RT-004–007, RT-013, MT-030–031, MT-034

- [ ] Choose the spec-approved migration: preserve TimeControlPanel behavior and move/rebuild its presentation under the single Phase 5 Root with TMP/Theme.
- [ ] Update failing hierarchy tests to expect the approved new canonical names without weakening singleton assertions.
- [ ] Implement idempotent scene migration; never duplicate `GameTimeService`, camera input, Scene interaction or EventSystem.
- [ ] Run setup twice; verify Pause/Normal/Fast and world selection behavior.
- [ ] Run complete Phase 0 tests before proceeding.
- [ ] Stop for MainCafe manual spot-check and commit authorization checkpoint.

### Task 11: Run Complete Automated, Integration and Regression Matrix

**Files:** no planned production changes; fixes must return to owning task/tests

**Cases:** AT-001–036, IT-001–030, RT-001–014

- [ ] Run complete EditMode suite and save XML/log evidence.
- [ ] Run complete Editor PlayMode suite and save XML/log evidence.
- [ ] Run player-compatible/standalone PlayMode suite and save player log/evidence.
- [ ] Run Phase 3 and Phase 4 production validators and record exact totals/issues.
- [ ] Run build-settings isolation, assembly boundary, missing-reference scan and `git diff --check`.
- [ ] Compare results with Task 0 baseline; investigate every delta.
- [ ] Request code review using `superpowers:requesting-code-review`; fix findings through failing tests.
- [ ] Present results; do not claim completion if any required case is Failed/Blocked.

### Task 12: Execute Numbered Manual Acceptance

**Files:** test-case status/evidence record only; production fixes return to owning tasks

**Cases:** MT-001–034

- [ ] Prepare exact Unity project/worktree path, validation scene and resolution fixtures for Studio Owner.
- [ ] Walk Studio Owner through MT-001–034 in order; record Passed/Failed/Blocked and evidence path for each ID.
- [ ] For every Failed case, reproduce, add/strengthen automated coverage where possible, fix, rerun focused and regression tests, then repeat that MT case.
- [ ] For any Blocked case, record concrete blocker and do not count it as acceptance.
- [ ] Obtain Studio Owner explicit final manual acceptance only after all required cases Passed or a named limitation is explicitly accepted.

### Task 13: Documentation and Phase Gate Closeout

**Files:** `Docs/Phase5_Beginner_Guide.md`, Decision Log, design spec, test cases, Roadmap

**Cases:** documentation evidence and all prior gates

- [ ] Write beginner guide: what each component does, concrete example, safe usage, common errors, where Figma ends and Unity begins.
- [ ] Record final automated totals, validator results, manual case results and provisional values actually used.
- [ ] Update Decision Log/spec only for approved changes; map future finalization to Phase 47/50/51/52.
- [ ] Run final documentation link/path scan and `git diff --check`.
- [ ] Use `superpowers:verification-before-completion` before any completion claim.
- [ ] Present exact changed files and evidence; separately ask what the Studio Owner wants to do with commit/push/PR/worktree. Do not infer authorization.

---

## Execution Checkpoints

- Gate A：design spec + plan + test cases approved。
- Gate B：isolated branch/worktree + clean verified baseline。
- Gate C：Tasks 1–5 logic contracts and focused tests passed。
- Gate D：Tasks 6–9 component library, assets, validation scene and visual review passed。
- Gate E：MainCafe migration + full automated/regression matrix passed。
- Gate F：MT-001–034 manual acceptance passed。
- Gate G：documentation and final verification completed；integration action remains separately authorized。

## Spec Coverage Map

- Technology/assembly：Tasks 1, 8, 11。
- UI Root/layers：Tasks 6, 8, 9, 10。
- Components/Theme/Frost：Tasks 1, 6, 8, 9。
- Navigation/Back：Task 2, 6, 9。
- Pointer boundary：Task 4, 9, 10。
- Pause Policy：Task 3, 6, 9, 10。
- Feedback：Task 5, 8, 9。
- Resolution/Safe Area/localization/accessibility hooks：Tasks 1, 6, 7, 9。
- Legacy compatibility：Task 10, 11。
- Automated/manual verification：Tasks 0, 11, 12。
- Beginner documentation/finalization ownership：Task 13。

## Pre-execution Decision

No implementation begins until the Studio Owner approves this plan and the separate test-case document. After approval, Task 0 creates the branch/worktree using `superpowers:using-git-worktrees`; that operation is not performed while these documents are still under review.
