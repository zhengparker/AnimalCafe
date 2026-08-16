# Task 10 Implementation Report

## Initial inspection

- Plan Task 10 requires migration through `Phase0SceneSetup` and preservation of
  `TimeControlPanel` contracts (IT-028, IT-029, RT-004 through RT-007, RT-013).
- MainCafe currently has root-level `Phase0_TimeControls` and `EventSystem`.
  Its time controls use legacy `UnityEngine.UI.Text`; `Phase0_Runtime` already
  owns the Game Time, mouse input, camera, and scene interaction services.
- The approved migration target is one canonical Phase 5 `UI Root` using TMP
  and the Phase 5 theme, with the legacy time-control behavior retained.

## TDD status

- EditMode RED: `Phase5Task10-Edit-Red.xml` was `0/2`, correctly failing
  because legacy MainCafe had no canonical `UI Root`.
- EditMode GREEN: `Phase5Task10-Edit-Green2.xml` was `2/2`. The deterministic
  setup now migrates time controls under a single Phase 5 root, using TMP and
  the Phase 5 theme while retaining the existing runtime services.
- PlayMode RED exposed the real world-selection path separately from the time
  controls. The final real virtual-Mouse test uses an EventSystem-confirmed UI
  clear screen point, then proves Physics raycast, MouseCameraInput, and
  SceneInteractionController selection without directly invoking a button.
- PlayMode GREEN: `Phase5Task10-Play-Green3.xml` was `2/2`: Pause/Normal/Fast
  change the existing `GameTimeService`, and a world tap outside UI selects a
  `ColorSelectable`.
- Phase 0 regression: `Phase5Task10-Phase0PlayRegression.xml` was `26/26`,
  with the old legacy-root assertion intentionally migrated to the one
  canonical `UI Root` contract.

## Implementation scope

- `Phase0SceneSetup` reuses the canonical Phase 5 `PF_UI_Root` prefab, removes
  `Phase0_TimeControls`, creates TMP/themed buttons in `HUD Canvas/HUD Layer`,
  and moves/deduplicates the existing EventSystem beneath that root.
- Existing `Phase0_Runtime`, `GameTimeService`, `MouseCameraInput`, camera,
  and `SceneInteractionController` stay as their original single instances.
- Consecutive setup runs are covered by the EditMode migration test, which
  asserts stable singleton inventory. No manual case is claimed: MT-030,
  MT-031, and MT-034 remain prepared for Studio Owner review.

## Fix Round 1 review coverage

- Runtime reload coverage (RT-004): `MainCafe` is loaded after a paused,
  in-progress pointer gesture. The first physical release on the new scene
  cannot complete a stale selection, a fresh UI click works, time returns to
  Normal, and there remains exactly one UI Root, EventSystem, Game Time,
  MouseCameraInput, and SceneInteractionController.
- Real-input selection coverage (RT-005): a virtual Mouse selects and clears a
  world object at EventSystem-confirmed UI-clear points. The test asserts both
  `SelectionChanged` payloads and count; a migrated Pause-button click does
  not select or deselect that world object.
- Time contract coverage (IT-029): virtual Mouse Pause, Normal, and Fast
  clicks assert one ordered `GameSpeedChanged` event per speed change, proving
  no duplicate listener delivery.
- Build Settings hardening: EditMode asserts `MainCafe` is the sole enabled
  production scene.

## Fix Round 1 evidence

- Migration EditMode: `3/3` passed.
- Original MainCafe real-input PlayMode: `2/2` passed.
- Reload, selection/event isolation, and Game Time event PlayMode: `3/3`
  passed.
- Complete Phase 0 PlayMode regression: `26/26` passed.
- All listed suites report zero failed, skipped, and inconclusive tests.
