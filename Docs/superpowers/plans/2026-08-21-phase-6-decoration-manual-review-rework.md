# Phase 6 Decoration Manual Review Rework Implementation Plan

> **Required sub-skill:** Use `superpowers:test-driven-development` for every behavior change and `superpowers:systematic-debugging` for every unexpected failure. Before claiming completion, use `superpowers:verification-before-completion`.

**Goal:** Resolve the accepted P6-M-001–005 manual-review findings: a visible game-time indicator and non-overlapping vertical time rail, sliding Catalogue, furniture-relative floating actions, reliable Mouse decoration gestures, and English-first UI that remains localization-ready.

**Architecture:** Preserve the existing `DecorationSession`, `DecorationTouchRouter`, `DecorationCameraDriver`, Task 6 view events, and Task 9 pointer ownership rules. Add a small read-only time indicator and a Mouse-to-semantic-gesture adapter. Route Touch and Mouse through the same approved decoration command path, with one device family owning a gesture until terminal. Generate all persistent UI and Scene changes through the existing candidate-first builder/setup transaction; never hand-edit serialized YAML.

**Tech Stack:** Unity 6000.5.5f1, C#, UGUI, TextMeshPro, Input System, NUnit/Unity Test Framework, existing Phase 5 foundation services and Phase 6 builder/setup/validator.

**Spec:** `Docs/superpowers/specs/2026-08-21-phase-6-decoration-manual-review-rework-design.md` (confirmed; SHA-256 `59A5CB78CA32BF12DA92AEFC7CCD0B2BC7714BFF9C83C6CCD7EA6B7CCF1732A6`).

## Global constraints

- Work only in `E:\Unity\Project\AnimalCafe\.worktrees\phase-6` on `codex/phase-6-basic-decoration-mode`.
- Do not stage, commit, push, merge, edit the progress ledger, or change branch/worktree state.
- Before the first test edit, create an exact rollback snapshot and manifest for every approved source, test, Prefab, Scene, and `.meta` path. Preserve GUIDs and local file IDs.
- Use `apply_patch` for C#/Markdown edits. Use the public Phase 6 builder/setup APIs for Prefabs and Scenes. No YAML hand edits.
- Do not change `Packages/`, `ProjectSettings/`, Build Settings, the shared Phase 5 theme, or the Phase 6 TMP font in this round.
- Do not use `-nographics`; earlier RenderTexture evidence showed it is incompatible with the builder fixtures.
- Never use broad `AssetDatabase.SaveAssets()` or broad `AssetDatabase.Refresh()` as a repair shortcut.
- Keep Task 10 manual-result cells blank until the user reruns the affected manual checks.
- English is the authored copy for this phase. All new TMP fields must use the existing Chinese-capable font asset, flexible layout, and semantic text roles so localization can be added later without redesign.
- A credible RED outside the approved files, a persistent GUID/local-ID change, a dirty unrelated Scene, or a snapshot mismatch is a stop-and-report gate.
- Sanitize Unity logs so any `-accessToken` value is `[REDACTED]` before evidence is retained or quoted.

## Approved file map

### New runtime files

- `Assets/Scripts/UI/GameTimeStatusIndicator.cs` + `.meta` — read-only rotating visual driven by current game speed.
- `Assets/Scripts/Decoration/Input/MouseDecorationInputSource.cs` + `.meta` — converts Mouse press/move/release and wheel into the semantic input consumed by Decoration Mode.

### Runtime files to modify

- `Assets/Scripts/UI/TimeControlPanel.cs` — expose a decoration pause lock without changing `GameTimeService` ownership.
- `Assets/Scripts/UI/Decoration/DecorationActionBarView.cs` — retain existing events but present a compact floating icon group.
- `Assets/Scripts/UI/Decoration/DecorationCatalogueView.cs` — explicit Hidden/Expanded/Collapsed states and slide transitions.
- `Assets/Scripts/Decoration/FurniturePreviewView.cs` — expose read-only world render bounds for screen anchoring; no placement mutation.
- `Assets/Scripts/Decoration/DecorationModeController.cs` — coordinate input family, time rail lock, catalogue state, and floating action anchor.

### Editor production files to modify

- `Assets/Editor/Phase6/Phase6DecorationAssetBuilder.cs`
- `Assets/Editor/Phase6/Phase6DecorationSceneSetup.cs`
- `Assets/Editor/Phase6/Phase6DecorationValidator.cs`

### Generated assets

- `Assets/UI/Phase6/Prefabs/PF_UI_DecorationCatalogue.prefab`
- `Assets/UI/Phase6/Prefabs/PF_UI_DecorationActionBar.prefab`
- `Assets/Scenes/MainCafe.unity`
- `Assets/Scenes/Validation/Phase6DecorationMode.unity`

The existing `.meta` files for generated assets must remain byte-identical.

### Tests to modify

- `Assets/Tests/EditMode/Phase6/Phase6DecorationUiPrefabTests.cs`
- `Assets/Tests/EditMode/Phase6/Phase6DecorationAssetBuilderTests.cs`
- `Assets/Tests/EditMode/Phase6/Phase6DecorationValidatorTests.cs`
- `Assets/Tests/EditMode/Phase6/Phase6MainCafeMigrationTests.cs`
- `Assets/Tests/PlayMode/Phase6DecorationTouchPlayModeTests.cs`
- `Assets/Tests/PlayMode/Phase6DecorationUiPlayModeTests.cs`
- `Assets/Tests/PlayMode/Phase6DecorationScenePlayModeTests.cs`
- `Assets/Tests/PlayMode/EditorSceneLoading/Phase6DecorationRealTouchTests.cs`
- `Assets/Tests/PlayMode/EditorSceneLoading/Phase6DecorationMainCafeSceneTests.cs`

Phase 5 tests may be changed only if a reproducible test-harness defect blocks an otherwise valid regression and the user gives a separate explicit approval.

## Task 1: Freeze the round and prove the starting point

**Artifacts:** `outputs/phase6-manual-review/rounds/t10-r03-decoration-ui-input-rework/`

- [ ] Record branch, HEAD, Unity version, spec hash, plan hash, `git status --short`, staged paths, running Unity processes, and exact approved path inventory.
- [ ] Copy exact pre-edit bytes for all approved files into `rollback/`; record SHA-256, size, GUID, and local IDs where applicable.
- [ ] Record protected hashes for MainCafe, Validation, Theme, Camera settings, Grid settings, Build Settings, `Packages/`, and `ProjectSettings/`.
- [ ] Refuse to proceed if any approved path is missing from the rollback manifest or cannot be restored losslessly.
- [ ] Run nonzero pre-round baselines: Phase 6 UI Prefab, AssetBuilder, Validator, UI PlayMode, RealTouch, Scene, MainCafe Scene; Phase 5 Time controls, pointer, responsive layout, real input, and MainCafe Mouse fixtures.
- [ ] Require every baseline XML to have `failed=0`, `skipped=0`, and `inconclusive=0`. Preserve sanitized logs.

## Task 2: Add the game-time indicator and vertical Safe Area rail

**Files:** `GameTimeStatusIndicator.cs`, `TimeControlPanel.cs`, builder/setup/validator, UI/Scene tests.

- [ ] Add EditMode/PlayMode RED tests proving the right rail order is exactly `DecorOrDone`, `TimeStatusIndicator`, `Pause`, `Normal`, `Fast`, with shared width, spacing, and Safe Area containment at 720×1280, 1080×1920, 1080×2400, and 2400×1080.
- [ ] Add RED tests proving the rail remains top-level and is not covered by Catalogue Expanded, Collapsed handle, floating actions, or modal presentation.
- [ ] Add a RED test for a read-only indicator contract:

```csharp
public sealed class GameTimeStatusIndicator : MonoBehaviour
{
    public void Configure(IGameTimeService gameTimeService, RectTransform rotatingVisual);
    public void Refresh(float unscaledDeltaTime);
}
```

- [ ] Assert Pause produces zero angular movement, Normal produces the configured base angular movement, and Fast produces a larger deterministic movement. Assert the indicator never calls `TrySetSpeed`.
- [ ] Add RED tests for `TimeControlPanel.SetDecorationPauseLock(bool)`: while Decoration Mode is open, Pause stays selected and Normal/Fast are disabled; cleanup restores normal interactability without forcing a speed.
- [ ] Implement the smallest indicator and pause-lock behavior. Use `Time.unscaledDeltaTime` only for display motion; the displayed speed comes from `IGameTimeService.CurrentSpeed`.
- [ ] Update the builder to create the indicator and vertical rail from existing Theme tokens. Use English labels/tooltips, the existing TMP font, and at least 48×48 logical targets.
- [ ] Update setup/validator to wire the same `GameTimeService` to Time controls and indicator and to reject rail order, overlap, font, or reference drift.
- [ ] Run focused GREEN tests, then the full affected Time/UI fixtures.

## Task 3: Add a Mouse semantic input adapter

**Files:** `MouseDecorationInputSource.cs`, controller, touch/scene/real-input tests.

- [ ] Add RED unit tests for an allocation-stable adapter that produces one synthetic pointer ID and the sequence Began → Moved/Stationary → Ended or Canceled.
- [ ] Add RED tests for thresholds: a short left click remains a tap; movement past threshold becomes a drag; release always terminates; losing focus/disable emits or performs equivalent cancellation and clears ownership.
- [ ] Add RED tests for Mouse wheel zoom and finite/clamped delta. Wheel input must call the existing camera zoom path, not a second camera implementation.
- [ ] Define the narrow interface used by the controller:

```csharp
public interface IMouseDecorationInputSource : IDecorationTouchSource
{
    float ReadScrollDelta();
    bool HasActivePointer { get; }
    void Reset();
}
```

- [ ] Implement the adapter with one reused point buffer; do not allocate a new list every frame.
- [ ] Add controller RED tests for device-family arbitration: the family that begins a gesture owns it until terminal; Touch wins only when no Mouse gesture is active; no mid-drag switching or duplicate domain command.
- [ ] Feed Mouse semantic frames into the existing `DecorationTouchRouter`. Preserve approved selection priority, raw/offset drag behavior, placement validation, edge pan, and pointer-boundary suppression.
- [ ] Route wheel zoom through `DecorationCameraDriver.ApplyPinchZoom` using a documented conversion constant. Do not re-enable normal `CafeCameraController` input while Decoration Mode owns the camera.
- [ ] Add real MainCafe Mouse tests: click existing furniture selects it, left-drag moves preview, blank-floor left-drag pans camera, wheel zooms, UI consumes input, and Touch/Mouse transitions cleanly after terminal.
- [ ] Run focused GREEN tests and both Phase 5/Phase 6 Mouse/Touch regression fixtures.

## Task 4: Make Catalogue an explicit sliding state machine

**Files:** `DecorationCatalogueView.cs`, builder, UI tests.

- [ ] Add RED tests for the exact public states `Hidden`, `Expanded`, and `Collapsed`; keep `ShowCatalogue`, `ShowCollapsedHandle`, and `Hide` as compatibility entry points.
- [ ] Add RED transition tests: Expanded slides up from below the Safe Area; Collapsed slides down and leaves only the handle; Hidden is noninteractive and blocks no raycasts.
- [ ] Add reversal tests for expand→collapse and collapse→expand mid-transition. There must be no frame with two usable Catalogue owners and no UI pass-through gap.
- [ ] Assert transition duration uses unscaled time. Reduced Motion resolves immediately to the correct terminal position and CanvasGroup state.
- [ ] Implement a single transition owner that updates `RectTransform.anchoredPosition`, alpha, interactability, and raycast blocking atomically.
- [ ] Update authored geometry for all four canonical sizes. The collapsed handle must not overlap the right rail.
- [ ] Add controller RED tests: entering mode shows Expanded; selecting new/existing furniture auto-collapses; Confirm/Cancel returns to Expanded; Done/cleanup produces Hidden; modal leaves Catalogue noninteractive.
- [ ] Run focused GREEN UI tests and the full Phase 6 UI suite twice to detect transition-order instability.

## Task 5: Replace the bottom Action Bar with furniture-relative floating actions

**Files:** `FurniturePreviewView.cs`, `DecorationActionBarView.cs`, builder, controller, validator, tests.

- [ ] Add RED tests for `FurniturePreviewView.TryGetWorldBounds(out Bounds bounds)`. It must aggregate active preview renderers without exposing mutable renderer collections.
- [ ] Add RED tests for a screen-space anchor API:

```csharp
public void SetPresentation(
    DecorationActionPresentation presentation,
    Vector2 preferredScreenPoint,
    Rect safeArea);
```

`DecorationActionPresentation` distinguishes New and Existing furniture while preserving the existing Store/Rotate/Cancel/Confirm events.

- [ ] Add RED tests for exact button sets and stable order:
  - New: Cancel, Rotate, Confirm.
  - Existing: Store, Cancel, Rotate, Confirm.
- [ ] Add RED tests proving icon-only buttons retain semantic English tooltip/accessibility labels; each target is at least 48×48 logical units, translucent but readable, and Confirm disabled state is visible and noninteractive.
- [ ] Add RED geometry tests: preferred anchor is the selected furniture's upper-right screen corner; the group clamps into Safe Area; flips left/down when needed; never overlaps the right rail, Catalogue handle, or modal.
- [ ] Implement `TryGetWorldBounds`, world-to-screen projection in the controller, and screen-space placement in the view. Recompute while preview or camera moves; perform no layout/domain allocation when the snapped cell and projected anchor are unchanged.
- [ ] Update the generated ActionBar Prefab to a compact icon group; preserve the existing Prefab GUID and view event surface. Do not add a World-space Canvas.
- [ ] Keep Store as a request that opens the existing confirmation modal. Confirm/Cancel/Rotate retain their current domain meanings and Task 9 owner gate.
- [ ] Add real Touch and Mouse tests for each action set, modal flow, invalid placement, Safe Area edges, camera movement, and no world pass-through.
- [ ] Run focused GREEN tests, full Scene/UI/RealTouch suites, and pointer-adapter/controller-gate regressions.

## Task 6: Integrate the complete controller lifecycle

**Files:** `DecorationModeController.cs`, setup/validator, scene tests.

- [ ] Add a parameterized RED matrix for Enter, Done, explicit Exit, Disable, and Destroy from Catalogue, new preview, existing preview, dragging, and Store modal states.
- [ ] On Enter: acquire the existing pause lease, lock Normal/Fast controls, show the indicator/rail, show Catalogue Expanded, reset both input sources, and leave normal camera input disabled.
- [ ] During preview: Catalogue Collapsed, floating actions visible and tracked, exact preview validity reflected, and only the active device family mutates the router.
- [ ] On Confirm/Cancel: terminate pointer/edge-pan state, hide floating actions, and return Catalogue to Expanded.
- [ ] On Store modal: keep the correct view ownership, suppress domain input, and return to the existing preview on dismiss.
- [ ] On every cleanup path: reset Touch and Mouse sources, release pointer ownership, stop edge pan, hide Catalogue/actions/modal/grid/preview, unlock Time controls, dispose the exact pause lease once, and restore the exact pre-entry speed.
- [ ] Assert repeated cleanup is a no-op and failed Enter rolls back every acquired resource.
- [ ] Run the complete Scene controller suite until two independent launches are GREEN with no skipped/inconclusive tests.

## Task 7: Regenerate Prefabs and Scenes transactionally

**Files:** builder/setup/validator, generated Prefabs/Scenes, EditMode transaction tests.

- [ ] Expand Prefab manifest RED tests for rail, indicator, Catalogue slide roots, floating actions, icons, TMP roles, component inventories, sibling order, RectTransforms, and prefab-local references.
- [ ] Expand validator RED tests for missing/wrong components, references, positions, order, fonts, active state, raycast ownership, and unknown children/components in each new owned subtree.
- [ ] Expand setup transaction RED tests for both targets: dependency preflight, BeforeMutation, BeforeSave, AfterSave, dirty target refusal, missing dependency, caller Scene/order/Selection preservation, rollback, first publish, and second-run zero-save.
- [ ] Build candidates and publish only through `Phase6DecorationAssetBuilder.BuildAll()`; run the byte/GUID/local-ID idempotency test twice.
- [ ] Configure MainCafe and Validation only through their public setup APIs. Do not use a temporary authoring test unless separately approved.
- [ ] Verify MainCafe remains the sole enabled Build Settings Scene and Validation remains absent.
- [ ] Run full AssetBuilder, UI Prefab, Validator, and migration/setup suites. Re-hash all protected files immediately after every builder/setup launch.

## Task 8: Full automated regression and visual evidence

- [ ] Run final focused suites: Mouse adapter, time indicator/rail, Catalogue state machine, floating actions, controller lifecycle, Prefab contract, validator, setup transaction, and actual MainCafe Scene.
- [ ] Run Phase 6 named regressions: router, camera driver, input source, Scene controller, UI, RealTouch, MainCafe Scene, layout preview, and `DecorationSession`.
- [ ] Run Phase 5 regressions: Time controls, pointer boundary, responsive layout, real Touch/input, UI foundation, MainCafe Mouse, theme, and UI asset acceptance.
- [ ] Run the Task 9 same-process composite/order matrix so device, EventSystem, pointer, and EnhancedTouch cleanup remain stable in both directions.
- [ ] Require nonzero totals and zero failed/skipped/inconclusive for every authoritative XML. A single intermittent failure restarts the relevant frozen-source sequence after diagnosis.
- [ ] Generate before/after captures at 720×1280, 1080×1920, 1080×2400, and 2400×1080 for:
  - right rail and indicator at Pause/Normal/Fast;
  - Catalogue Expanded/Collapsed/Hidden;
  - new and existing floating action layouts near center and Safe Area edges;
  - Mouse drag preview and invalid placement;
  - Store modal.
- [ ] If no existing capture entry point exists, stop and request approval for one exact temporary helper + `.meta`; delete it immediately afterward and prove zero references/residue.
- [ ] Visually inspect every capture for clipping, overlap, contrast, correct z-order, readable icons/tooltips, and consistent alignment with Decor/Done.

## Task 9: Independent review and manual acceptance

- [ ] Write a round report containing hashes, RED→GREEN evidence, all corrections, protected-state checks, known limitations, and exact rerun instructions. Status remains `awaiting independent Engineering, Art/UX, and QA review`.
- [ ] Obtain independent Engineering review for input arbitration, lifecycle, allocations, pause restoration, transactions, and source scope.
- [ ] Obtain independent Art/UX review for right-rail composition, indicator readability, Catalogue motion, floating-action placement, and four-size Safe Area behavior.
- [ ] Obtain independent QA review for test honesty, real Mouse/Touch paths, cleanup, evidence closure, and manual-sheet integrity.
- [ ] After all reviews report zero Critical/Important findings, ask the user to rerun P6-M-001–005 using the rebuilt MainCafe.
- [ ] Record the user's actual observations and verdicts without pre-filling or inferring PASS.
- [ ] Keep full Chinese localization deferred. Record an explicit future-phase localization item covering string tables, locale switching, translated copy review, CJK fallback coverage, and four-size layout verification.
- [ ] Task 10 and Phase 6 remain incomplete until the user accepts the affected manual checks. Task 11 must not start early.

## Final verification checklist

- [ ] `git diff --check` is clean for all round-owned C#/Markdown files; any pre-existing serialized whitespace is listed separately.
- [ ] No staged files, no commit/push, no runner-created `InitTestScene`, no temporary capture/helper files, and no live Unity process.
- [ ] No unauthorized changes under `Packages/`, `ProjectSettings/`, Build Settings, shared Theme/font, or unrelated Scenes/Prefabs.
- [ ] Every new `.meta` GUID is unique; every existing asset `.meta` and GUID is unchanged.
- [ ] All report XML/log/capture references exist and hashes match the final frozen source.
- [ ] The design spec and this plan remain unchanged during implementation unless the user explicitly approves a documented correction.

## Execution handoff

Recommended execution is inline in the current task, one numbered Task at a time, with a checkpoint after each RED→GREEN group. No implementation begins until the user explicitly confirms this plan. If the user wants delegated execution, they must explicitly request subagents; shared Prefab/Scene generation remains sequential even then.
