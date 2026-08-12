# Task 9 Implementation Report

## Scope

- Added a deterministic validation scene at
  `Assets/Scenes/Validation/Phase5UiFoundation.unity`, generated only through
  `Phase5UiFoundationSceneSetup` Unity Editor APIs.
- Added a stable Phase 5 UI validator with issue codes plus asset/object paths.
- Added an isolated component gallery, selectable Coffee Machine fixture,
  scaled-time mover, long mixed CJK/Latin text, Safe Area fixture, and feedback
  controls for Toast, Tooltip, validation reason, and Bottom Sheet review.
- The validation scene is not added to production Build Settings. `MainCafe`
  and Task 10 were not modified.

## Validator contract

- Detects duplicate `UI Root`, named Canvas, and `EventSystem` objects.
- Detects missing logical layers, missing theme tokens/fonts, controls below
  `48x48`, incorrect Toast raycast policy, and more than one resolved Strong
  Frost owner.
- Every issue has a stable `Phase5UiFoundationIssueCode`, `AssetPath`, and
  `ObjectPath` so a failing scene can be located without inference.

## TDD evidence

- Validator RED first failed at compile time because its report, issue code and
  validator types did not exist. GREEN: `Phase5Task9-Validator-Green6.xml`,
  `4/4` passed, `0` failed/skipped/inconclusive.
- Scene builder RED first failed because `Phase5UiFoundationSceneSetup` did not
  exist. GREEN: `Phase5Task9-Scene-Final3.xml`, `3/3` passed, `0`
  failed/skipped/inconclusive.
- Generated-scene PlayMode RED first reached the missing feedback controls.
  GREEN: `Phase5Task9-Play-Green4.xml`, `2/2` passed, `0`
  failed/skipped/inconclusive.
- Scene regeneration is required after builder changes and before PlayMode
  evidence; the final sequence followed this dependency.

## Final automated evidence

- Focused validator: `4/4` passed.
- Focused deterministic scene setup/idempotency/build-settings scope: `3/3`
  passed.
- Focused generated-scene PlayMode interaction evidence: `2/2` passed.
- Cumulative Phase 5 EditMode: `75/75` passed.
- Cumulative Phase 5 PlayMode: `41/41` passed.
- Relevant real-UI regression: `5/5` passed.
- All final suites report `0` failed, skipped, and inconclusive.

## Generated-artifact cleanup

- Unity test-time serialization changed the canonical TMP font asset. The
  changed version was backed up under ignored SDD backups (SHA-256
  `730053AA...`) and the tracked canonical font was restored from `HEAD` before
  staging. The generated worktree `.slnx` was removed; no ProjectSettings
  content diff remained.

## Ready for Manual Review

The scene is ready for Studio Owner validation using the manual cases in
`Docs/superpowers/specs/2026-08-11-phase-5-ui-test-cases.md` (especially
MT-001 through MT-029). No manual review has been claimed or performed here.

## Fix Round 1 — review remediation complete

- The validator now traverses nested generated scene hierarchy and reports full
  stable object paths for duplicate UI Root and missing logical layers.
- The validation-scene canonical asset contract now includes the scene plus its
  scene-owned Input System action asset; BuildScene removes every Build Settings
  entry for the validation scene, including disabled entries.
- The generated scene now contains a persisted real selection chain and a
  scene-owned `InputSystemUIInputModule` action asset. The virtual-Touch test
  exercises `Touchscreen` → Input System → EventSystem → GraphicRaycaster →
  actual Button listener → ToastView, without direct `onClick.Invoke`.
- A generated-hierarchy UI registration lifecycle hook refreshes graphics one
  frame after scene load; the real-input test observes registration and does not
  mutate UI to make it pass.

## Fix Round 1 final automated evidence

- Review contract: `4/4` passed (`Phase5Task9Fix1-Review-Green18.xml`).
- Destroyed Strong Frost owner regression: `1/1` passed
  (`Phase5Task9Fix1-StrongOwner-Green.xml`).
- Validator token-path regression: `5/5` passed
  (`Phase5Task9Fix1-ValidatorRegressionGreen.xml`).
- Virtual Touch to Input System to EventSystem to GraphicRaycaster to Button to
  Toast: `1/1` passed (`Phase5Task9Fix1-Touch-Green7.xml`).
- Cumulative Phase 5 EditMode: `81/81` passed; PlayMode: `42/42` passed.
- Real UI regression: `5/5` passed; pointer-boundary and real world selection
  regression: `14/14` passed. Every final suite reported zero failed, skipped,
  or inconclusive tests.

`StrongFrostLease` now safely reclaims destroyed `Behaviour` owners. Theme
issue paths preserve `Typography/*`, `Colors/*`, and `Materials/*`; issue
`ToString()` includes code, asset path, object path, and message.

Generated test noise was removed before staging: `EditorBuildSettings.asset`,
the canonical TMP font, and the worktree `.slnx` are restored or removed and
are not part of this change.
