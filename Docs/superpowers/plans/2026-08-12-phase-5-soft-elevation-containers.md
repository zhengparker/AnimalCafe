# Phase 5 Soft Elevation Containers Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the approved Soft Elevation visual hierarchy and make MT-009/010 manually testable without changing Frost behavior.

**Architecture:** Extend the deterministic Phase5 asset builder so all generated prefabs own their decorative layers and serialized references. Runtime views keep navigation/lifecycle responsibility; the validation scene controller only supplies evidence copy and action wiring. Decorative uGUI Graphics are explicitly non-raycastable.

**Tech Stack:** Unity 6.0, uGUI, TextMeshPro, Input System, NUnit EditMode/PlayMode.

## Global Constraints

- No true blur shader in this phase.
- Preserve 48×48 touch targets, role colors, 25% pressed darkening, and 97% pressed scale.
- Do not commit, push, merge, or clean unrelated work without explicit authorization.
- Use Unity Editor API builders; do not hand-edit `.prefab` or `.unity` YAML.

---

### Task 1: Decorative Elevation Layers

**Files:**
- Modify: `Assets/Editor/Phase5/Phase5UiAssetBuilder.cs`
- Modify: `Assets/Scripts/UI/Components/AnimalCafeButtonView.cs`
- Test: `Assets/Tests/EditMode/Phase5/Phase5UiAssetAcceptanceTests.cs`
- Test: `Assets/Tests/PlayMode/Phase5ReusableComponentsPlayModeTests.cs`

**Interfaces:**
- Produces generated children named `Elevation Shadow` and `Top Highlight` with `Graphic.raycastTarget == false`.
- Preserves existing `AnimalCafeButtonView.Configure(...)` public API.

- [ ] Write EditMode tests for decoration names, sibling ordering, non-raycast behavior, rounded sprite, and unchanged touch targets.
- [ ] Write/strengthen real Touch test proving pressed color/scale and click delivery remain unchanged.
- [ ] Run focused tests and verify RED because decoration children do not exist.
- [ ] Add minimal generated shadow/highlight layers and runtime pressed shadow compression.
- [ ] Run focused tests and verify GREEN.

### Task 2: Structured Bottom Sheet

**Files:**
- Modify: `Assets/Editor/Phase5/Phase5UiAssetBuilder.cs`
- Modify: `Assets/Scripts/UI/Components/AnimalCafeBottomSheetView.cs`
- Modify: `Assets/Editor/Phase5/Phase5UiFoundationSceneSetup.cs`
- Test: `Assets/Tests/EditMode/Phase5/Phase5UiAssetAcceptanceTests.cs`
- Test: `Assets/Tests/PlayMode/Phase5ContainerNavigationPlayModeTests.cs`

**Interfaces:**
- Generated hierarchy: `OutsideButton`, `Content`, `Content/Drag Handle`, `Content/Title`, `Content/Body`, `Content/CancelButton`, `Content/ConfirmButton`.
- `AnimalCafeBottomSheetView.ConfigureActions(Button cancel, Button confirm, Action onConfirm)` binds actions idempotently.

- [ ] Write EditMode anatomy/layout/raycast tests.
- [ ] Write real input tests for outside, Cancel, Confirm, Back, reopen, and lower-world visibility.
- [ ] Run focused tests and verify RED on missing hierarchy/API.
- [ ] Implement the minimum prefab anatomy and idempotent action wiring.
- [ ] Rebuild the validation scene and run focused GREEN.

### Task 3: Critical and Nested Modal Recovery

**Files:**
- Modify: `Assets/Editor/Phase5/Phase5UiAssetBuilder.cs`
- Modify: `Assets/Scripts/UI/Components/AnimalCafeModalView.cs`
- Modify: `Assets/Editor/Phase5/Phase5UiFoundationSceneSetup.cs`
- Test: `Assets/Tests/EditMode/Phase5/Phase5UiAssetAcceptanceTests.cs`
- Test: `Assets/Tests/PlayMode/Phase5ContainerNavigationPlayModeTests.cs`

**Interfaces:**
- Generated hierarchy includes `Blocker`, `Content/Title`, `Content/Body`, `Content/CancelButton`, `Content/ConfirmButton`.
- Critical primary modal remains `NotDismissible`; secondary modal is dismissible and closes independently.

- [ ] Write anatomy and non-raycast decoration RED tests.
- [ ] Write real input RED tests for outside blocking, lower-control blocking, Cancel, second-modal Cancel, and primary recovery.
- [ ] Implement minimal builder/view wiring without bypassing `UiNavigationCoordinator`.
- [ ] Rebuild scene and run focused GREEN.

### Task 4: Verification and Manual Handoff

**Files:**
- Modify: `Assets/Tests/EditMode/Phase5/Phase5UiFoundationReadabilityTests.cs`
- Modify: `outputs/phase5-manual-review/AnimalCafe_P5_Manual_Review.xlsx`

- [ ] Run MT-001–005 focused contracts.
- [ ] Run Bottom Sheet and Modal real-input suites.
- [ ] Run cumulative `AnimalCafe.Tests.Phase5` EditMode and PlayMode.
- [ ] Verify Build Settings and runtime assembly boundaries.
- [ ] Record MT-001–005 Passed, MT-008 Passed, MT-009/010 Not Run after fix; do not pre-pass owner review.
- [ ] Hand off exact MT-001–005 spot-check and MT-009/010 retest steps.
