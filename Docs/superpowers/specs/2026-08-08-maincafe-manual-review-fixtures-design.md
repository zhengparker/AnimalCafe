# MainCafe Temporary Manual Review Fixtures — Design

**Date:** 2026-08-08  
**Phase:** Phase 4 manual acceptance support  
**Status:** Awaiting Studio Owner written-spec approval

## Purpose

Add two clearly temporary cubes to `Assets/Scenes/MainCafe.unity` so a beginner can complete M76–M78 and M83–M84 without creating runtime objects manually.

The fixtures exist only until MainCafe has formal gameplay visuals. They are not furniture, catalogue content, Grid occupancy, or permanent game presentation.

## Scene Structure

MainCafe will contain one root named:

`TEMP_P4_ManualReviewFixtures_DELETE_LATER`

Its children are:

- `ReviewCube_Moving`: moves continuously between two fixed world-space endpoints.
- `ReviewCube_Static`: remains stationary as a second Camera reference.

The root name is the deletion contract: when MainCafe gains formal visuals, deleting this root removes both Scene fixtures. The temporary mover script can then be removed after confirming no remaining references.

## Behaviour

Both cubes:

- use an opaque URP-compatible material with different readable colors;
- have a `BoxCollider`;
- have `ColorSelectable`, so clicking either selects it and clicking the background deselects it;
- are positioned inside the current MainCafe Camera bounds with enough separation to make pan and zoom visible.

`ReviewCube_Moving` additionally uses `ManualReviewPingPongMover`:

- moves left and right between two serialized endpoints;
- uses `Time.deltaTime`;
- stops when Pause sets `Time.timeScale = 0`;
- moves at normal speed at 1x;
- moves approximately twice as far over the same real-time interval at 2x;
- does not use physics, NavMesh, Grid placement, or furniture systems.

## Files

- Modify `Assets/Scenes/MainCafe.unity` to add the temporary root and two cubes.
- Add `Assets/Scripts/Diagnostics/ManualReviewPingPongMover.cs` for the moving cube.
- Add `Assets/Art/Phase4/Materials/M_TEMP_ManualReviewCube_Moving.mat` and `M_TEMP_ManualReviewCube_Static.mat` for distinct temporary colors.
- Add `Assets/Editor/Phase4/MainCafeManualReviewFixtureSetup.cs` to create/refresh the exact temporary Scene structure through Unity Editor APIs instead of hand-editing Scene YAML.
- Modify `Assets/Tests/PlayMode/Phase0PlayModeTests.cs` for Scene structure, selection, Pause, 1x, and 2x coverage.
- Modify `Docs/Phase4_Beginner_Guide.md` with exact M76–M84 review steps.
- Update `outputs/phase4-manual-review/AnimalCafe_P4_Manual_Review_Updated.xlsx` after implementation and verification.

## Manual Review Flow

1. Open `Assets/Scenes/MainCafe.unity` and enter Play Mode.
2. Confirm both cubes are visible.
3. Press Pause and confirm the moving cube stops.
4. Press 1x and confirm it resumes at normal speed.
5. Press 2x and confirm it visibly moves faster.
6. Pan and zoom while using both cubes as visual references.
7. Click each cube and then the empty background to confirm select/deselect behavior.
8. Exit Play Mode and confirm the two committed review fixtures remain in the Scene while runtime movement/selection state resets.

## Automated Acceptance

- The root and exactly two named cube fixtures exist in MainCafe.
- Both cubes have Renderer, `BoxCollider`, and `ColorSelectable`.
- Only the moving cube has `ManualReviewPingPongMover`.
- Pause produces no movement over a real-time observation window.
- 1x produces forward movement.
- 2x produces materially more movement than 1x over the same real-time window.
- MainCafe still loads without missing scripts or unexpected errors.
- Full EditMode and Full PlayMode suites remain green.

## Cleanup Contract

This is intentionally temporary content. A future MainCafe visual implementation must:

1. replace these review purposes with formal visible/selectable gameplay objects;
2. delete `TEMP_P4_ManualReviewFixtures_DELETE_LATER` from MainCafe;
3. remove both `M_TEMP_ManualReviewCube_*.mat` assets;
4. remove `MainCafeManualReviewFixtureSetup.cs` so the deleted fixtures cannot be accidentally rebuilt;
5. remove `ManualReviewPingPongMover.cs` only after verifying it has no remaining Scene or Prefab references;
6. update the related manual-review documentation and tests in the same change.

No automatic cleanup date or Phase is assumed; removal is triggered when formal MainCafe visuals make the fixtures unnecessary.
