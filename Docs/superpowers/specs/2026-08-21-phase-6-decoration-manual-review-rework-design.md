# Phase 6 Decoration Manual Review Rework Design

**Date:** 2026-08-21  
**Status:** Studio Owner approved in chat; awaiting written-spec review and implementation plan  
**Lifecycle:** QA / Playtest finding → approved UI/Input redesign → TDD planning  
**Lead:** Game Design / UX  
**Required reviews:** Engineering, Art, QA & Player Research

## 1. Purpose

Task 10 manual review of `P6-M-001–005` exposed player-visible problems that
cannot be closed by documentation alone:

- the player cannot visually prove whether Game Time is paused;
- the existing bottom Time Controls are covered by the Decoration Catalogue;
- the Catalogue changes state without a clear slide-in / slide-out motion;
- the fixed bottom Action Bar is separated from the furniture being edited;
- the production Decoration flow supports Touch but not the project's current
  Windows-first Mouse interaction requirement;
- English remains the implementation language for this phase, while the UI must
  preserve future Localization and Chinese layout capability.

This design reopens only the affected Phase 5/6 UI and input contracts. It does
not accept `P6-M-001–005`; those cases must be rerun after implementation.

## 2. Confirmed Studio Owner decisions

1. Use a screen-space floating action group that follows the selected furniture.
2. New furniture shows `Cancel / Rotate / Confirm`.
3. Existing furniture shows `Store / Cancel / Rotate / Confirm`.
4. Store still requires its existing confirmation Modal.
5. Use simple high-contrast symbols rather than a bottom text Action Bar.
6. Add a rotating Game Time status indicator.
7. Place `Decor / Done`, the indicator and `Pause / 1× / 2×` in one consistent
   right-side vertical Safe Area rail.
8. Decoration Mode forces Pause: `1× / 2×` are visibly disabled while open, and
   `Done` restores the exact speed that was active before entry.
9. Catalogue uses visible slide-in / slide-out motion and automatically collapses
   while a furniture Preview is active.
10. Windows Mouse must support furniture dragging, blank-Floor Camera dragging,
    Mouse Wheel zoom and all Decoration UI actions.
11. Phase 6 player-visible copy remains English. Full Localization is deferred;
    TMP, flexible layout and Chinese-capable font contracts remain preserved.

## 3. Explicit non-goals

- No full Localization framework or language selector.
- No redesign of the shared Theme or general Phase 5 visual identity.
- No change to CafeLayout placement rules, furniture footprint rules, Save data,
  Definition IDs, Prefab identities or Scene ownership.
- No Camera rotation.
- No hand editing of `.unity`, `.prefab` or TMP Font YAML.
- No emoji-font dependency. Icons must use generated UI geometry or explicitly
  validated TMP glyphs.
- No automatic PASS for physical Touch, physical Safe Area or any manual case.

## 4. Player-visible layout

### 4.1 Right-side HUD rail

One Safe Area-owned rail is anchored to the upper-right corner. From top to
bottom it contains:

1. `Decor` while closed or `Done` while open;
2. the Game Time rotating indicator;
3. `Pause`;
4. `1×`;
5. `2×`.

All elements use the same width, spacing, alignment and minimum `48 × 48`
logical target contract. The rail must remain reachable in the four canonical
compositions:

- `720 × 1280`;
- `1080 × 1920`;
- `1080 × 2400`;
- `2400 × 1080`.

Catalogue, collapsed handle, floating actions and Modal may not cover the rail.

### 4.2 Game Time indicator

The indicator uses game-time-scaled rotation:

- `Pause`: angle remains unchanged;
- `1×`: normal rotation speed;
- `2×`: visibly twice the normal speed.

The indicator is informational and not itself a Button. The active speed Button
has a distinct selected state. While Decoration Mode owns Pause, `Pause` is
selected and `1× / 2×` are disabled rather than silently changing timeScale.

### 4.3 Catalogue Bottom Sheet

Catalogue has three presentation states:

- `Expanded`: the full sheet slides upward from below the viewport;
- `Collapsed`: the sheet slides downward and leaves only the `Catalogue` handle;
- `Hidden`: sheet and handle are both outside the viewport and non-interactable.

State rules:

- entering Decoration Mode opens `Expanded`;
- selecting new or existing furniture moves to `Collapsed`;
- successful Confirm or Cancel returns to `Expanded`;
- Store Modal keeps Catalogue collapsed/hidden and owns input;
- exiting Decoration Mode moves to `Hidden`;
- a transition interrupted by the opposite request reverses from the current
  position without flashing, duplicate callbacks or an intermediate raycast gap;
- Reduced Motion settles immediately to the same final state.

Motion uses unscaled time so Decoration Pause cannot freeze UI transitions.

### 4.4 Floating furniture actions

The existing Action Bar becomes a compact screen-space action group. It is not a
World-space Canvas.

The controller supplies the active Preview's world bounds. The view projects an
upper-right anchor into the target Canvas, then clamps the full action group to
the Safe Area. If the preferred side lacks room, the group flips to the
upper-left or moves inward. It must never scale with Camera zoom.

New furniture displays:

```text
Cancel  Rotate  Confirm
```

Existing furniture displays:

```text
Store  Cancel  Rotate  Confirm
```

Presentation rules:

- symbol-first visual treatment with a translucent surface;
- high-contrast glyph/geometry and clear disabled state;
- each target at least `48 × 48` logical units;
- English Mouse hover tooltip: `Store`, `Cancel`, `Rotate`, `Confirm`;
- Store is absent, not merely disabled, for new furniture;
- Confirm is visibly disabled for invalid placement;
- hidden actions have zero raycast ownership;
- Modal remains above actions and blocks all lower layers.

## 5. Input architecture

### 5.1 Shared semantic routing

Touch remains the multi-contact source. A new Mouse Decoration adapter produces
one synthetic contact compatible with the existing Decoration gesture routing.
The controller processes one active device family at a time:

1. active physical Touch owns the frame;
2. otherwise Mouse may own the frame;
3. switching device family is allowed only after the current gesture reaches a
   terminal state.

This preserves the existing `DecorationTouchRouter` ownership model rather than
duplicating placement rules for Mouse.

### 5.2 Mouse contract

- left press on furniture or the active Preview owns Furniture;
- left drag after threshold moves the Preview;
- left press on blank Floor owns Camera;
- blank-Floor left drag pans Camera;
- Mouse Wheel zooms Camera;
- left press on UI owns UI and never reaches Scene/Furniture;
- release/cancel clears ownership exactly once;
- Mouse furniture drag may use the same Preview offset policy as Touch only when
  explicitly configured; it must not inherit an unsuitable finger offset by
  accident.

The normal `CafeCameraController` remains disabled while Decoration Mode is open.
Decoration Camera changes continue through `DecorationCameraDriver`, preventing
simultaneous normal-camera and Decoration-camera movement.

## 6. Runtime responsibilities

### `GameTimeStatusIndicator`

- reads `IGameTimeService.CurrentSpeed`;
- rotates using game-scaled delta;
- owns no game-time mutation;
- exposes deterministic state for tests.

### `TimeControlPanel`

- retains Button-to-`GameTimeService` binding;
- exposes active/disabled visual state;
- accepts a Decoration pause lock without changing the underlying pause-owner
  contract;
- restores ordinary interactivity after the final pause owner exits.

### `MouseDecorationInputSource`

- reads Input System Mouse controls once per frame;
- emits one reusable synthetic-contact frame without per-frame allocations;
- preserves press origin, drag threshold, delta and terminal phase;
- provides no direct Layout, Camera or UI mutation.

### `DecorationModeController`

- selects Touch or Mouse source at a clean gesture boundary;
- keeps all domain operations in the existing Session/Controller path;
- supplies active Preview bounds to the floating action view;
- requests Catalogue state transitions from Session state changes;
- requests and releases the Decoration time-control lock atomically.

### `DecorationActionBarView`

- retains existing action events and completion latch;
- owns screen-space projection, Safe Area clamping, side flipping, icon/tooltip
  presentation and raycast lifecycle;
- does not mutate Session, Layout or Camera.

### `DecorationCatalogueView`

- owns `Expanded / Collapsed / Hidden` presentation;
- owns reversible anchored-position transition and interaction gating;
- does not decide gameplay state.

## 7. Expected file and asset scope

### New runtime files

- `Assets/Scripts/UI/GameTimeStatusIndicator.cs` plus `.meta`;
- `Assets/Scripts/Decoration/Input/MouseDecorationInputSource.cs` plus `.meta`.

### Existing runtime/editor files likely modified

- `Assets/Scripts/UI/TimeControlPanel.cs`;
- `Assets/Scripts/UI/Decoration/DecorationActionBarView.cs`;
- `Assets/Scripts/UI/Decoration/DecorationCatalogueView.cs`;
- `Assets/Scripts/Decoration/DecorationModeController.cs`;
- `Assets/Editor/Phase6/Phase6DecorationAssetBuilder.cs`;
- `Assets/Editor/Phase6/Phase6DecorationSceneSetup.cs`;
- `Assets/Editor/Phase6/Phase6DecorationValidator.cs`.

### Generated assets/scenes likely republished through public tools

- `Assets/UI/Phase6/Prefabs/PF_UI_DecorationActionBar.prefab`;
- `Assets/UI/Phase6/Prefabs/PF_UI_DecorationCatalogue.prefab`;
- `Assets/Scenes/MainCafe.unity`;
- `Assets/Scenes/Validation/Phase6DecorationMode.unity`.

Existing `.meta` identity must remain stable. Exact implementation scope must be
frozen in the implementation plan before the first test edit. No unrelated Phase
5 Prefab is authorized merely because the HUD rail consumes Phase 5 Time Controls.

## 8. TDD and verification design

### 8.1 Required RED groups

1. Mouse furniture drag for `1 × 1`, `1 × 2`, `1 × 3`, `2 × 3`.
2. Mouse Furniture-vs-Camera origin ownership and UI no-pass-through.
3. Touch priority and clean device-family handoff.
4. Time indicator Pause/`1×`/`2×` motion and selected state.
5. Decoration pause lock disables `1× / 2×` and restores exact previous speed.
6. Right rail exact order, Safe Area containment and no Catalogue overlap.
7. Floating actions show three/four targets, track bounds, flip/clamp and preserve
   callbacks/disabled Confirm/Modal priority.
8. Catalogue `Expanded / Collapsed / Hidden`, automatic state changes, reversal,
   unscaled timing, Reduced Motion and raycast ownership.
9. Builder/setup second-run byte/idempotency and validator drift detection.

### 8.2 Required regression groups

- current Task 9 real Touch, pointer ownership, Camera, UI and order suites;
- Phase 6 Scene/controller/session/layout suites;
- Phase 6 UI Prefab, Builder, Validator and setup transactions;
- Phase 5 Time Controls, UI foundation, pointer and responsive suites;
- actual `MainCafe` load/reload/no-Save tests;
- four canonical resolution rendering and real top-raycast checks.

### 8.3 Manual re-review

After automated and independent Engineering/Art/QA gates pass, the Studio Owner
reruns `P6-M-001–005`. Existing results remain findings, not accepted verdicts:

- `P6-M-001`: needs revision because Pause was not observable and Time Controls
  were covered;
- `P6-M-002`: layout looked acceptable, but Chinese verification is deferred to
  Localization and must not be claimed from this English-only pass;
- `P6-M-003–005`: failed because Mouse could rotate but could not drag furniture;
  the bottom Action Bar also failed the desired spatial relationship.

## 9. Failure and recovery rules

- Missing Mouse, indicator, Canvas, Camera or required view references fail startup
  before partial UI publication.
- Input-source disable, Mode exit, `OnDisable` and `OnDestroy` terminate active
  Mouse/Touch ownership, edge-pan and UI transitions.
- A failed Confirm leaves Preview/actions visible and Catalogue collapsed.
- Cancel restores the exact existing-furniture transform or removes a new Preview,
  then reopens Catalogue.
- If the projected action anchor is behind Camera or invalid, actions use a safe
  fallback near the Preview's last valid screen position; they never remain at an
  unrelated stale furniture position.
- Builder/setup failures use the existing candidate-first transaction and rollback
  rules; no partial Prefab/Scene publication is accepted.

## 10. Risks and mitigations

| Risk | Mitigation |
| --- | --- |
| Mouse moves Furniture and Camera together | press-origin ownership plus one semantic router |
| Mouse and Touch steal the same gesture | device-family lock until terminal frame |
| Floating actions jitter or leave Safe Area | projected bounds, deterministic clamp/flip, four-size tests |
| UI click moves Scene | existing pointer boundary plus real EventSystem top-raycast tests |
| Bottom Sheet flashes during reversal | single owned transition, current-position reversal, final-state raycast gate |
| Decoration exits at wrong speed | preserve and restore the exact pre-entry speed through pause ownership |
| Icon glyph is missing | generated geometry or explicit static-font glyph and rendered-geometry tests |
| Completed Phase 5 behavior regresses | no Phase 5 Prefab redesign; mandatory Phase 5 focused regressions |
| Future Chinese copy clips | TMP-only copy, flexible RectTransforms, no English-only fixed sizing |

## 11. Acceptance gate

Implementation may begin only after:

1. the Studio Owner reviews this written design;
2. an implementation plan freezes exact files, snapshot/rollback scope, RED tests,
   public Builder/setup commands and review gates;
3. the Studio Owner approves that plan.

Task 10 remains incomplete until the revised build passes independent
Engineering/Art/QA review and the Studio Owner reruns and accepts the affected
manual cases. Task 11 remains blocked until Task 10 acceptance.
