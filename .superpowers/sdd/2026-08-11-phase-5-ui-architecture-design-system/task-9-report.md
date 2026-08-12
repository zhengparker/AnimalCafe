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

## Fix Round 2 — strict production-scene remediation

- Exact scene inventory validation now reports stable missing, duplicate, and
  unexpected codes for UI Root, the three approved Canvases, the four approved
  logical layers, and EventSystem.
- Canonical asset validation is non-mutating, has deliberately broken negative
  fixtures, and includes the generated validation Scene, Input Actions, and all
  five InputActionReference assets.
- A recipe marker makes an unchanged validation-scene build byte-stable. The
  scene plus its owned input assets are fingerprinted after two builds.
- One runtime review controller creates the shared `UiPointerBoundary` used by
  Scene selection, all real uGUI Button hooks, Modal, and Bottom Sheet. Virtual
  Mouse evidence proves world selection, UI click-through suppression, and
  UI-to-world drag suppression against an intentionally occluded selectable.
- All four feedback controls are exercised through Input System → EventSystem →
  GraphicRaycaster → real Button listeners. No callback shortcut is used by
  the Round 2 suite.
- Pause/Continue, Reduced Motion status, second Strong Frost fallback, Modal,
  Bottom Sheet outside-close, validation repair, and Safe Area critical-control
  fixtures are executable. Disabling the review controller releases its Pause
  lifecycle and refreshes pointer ownership.

## Fix Round 2 TDD evidence

- Strict inventory/canonical/hash RED: `Phase5Task9Fix2-Contract-Red.xml`,
  `0/8` passed. GREEN: `Phase5Task9Fix2-Contract-Green2.xml`, `8/8` passed.
- Shared-boundary/real-input RED: `Phase5Task9Fix2-RealInput-Red.xml`, `1/3`
  passed. GREEN: `Phase5Task9Fix2-RealInput-Green.xml`, `3/3` passed.
- Self-review lifecycle/canonical RED: Edit `8/9`, Play `3/5`. Final focused
  GREEN: `Phase5Task9Fix2-SelfReview-EditGreen2.xml`, `9/9`; and
  `Phase5Task9Fix2-SelfReview-PlayGreen2.xml`, `5/5`. All final focused suites
  report zero failed, skipped, or inconclusive tests.

Fresh cumulative Phase 5, integration, regression, and Phase 0 evidence is
intentionally deferred until Task 10 stops writing the shared worktree. This
report does not claim cumulative readiness yet.

## Fix Round 2 final validation-scene coverage (recipe v8)

- Added executable validation-only controls for the remaining manual matrix:
  Solid/Light/Strong switching, forced Strong fallback, shared Back, critical
  Modal outside blocking, two-Modal stack order, three-Toast burst with
  duplicate merge evidence, tap/long-press/close Tooltip paths, transition
  interruption/reopen, and reload-state cleanup.
- Back input uses one shared edge-latched entry, so one physical Back closes
  exactly one top container. The second-Modal entry is a real raycastable
  control inside the first Modal while the second Modal remains its sibling in
  the shared Modal layer.
- Bottom Sheet outside input waits for the unscaled open transition and actual
  Canvas registration; the test then raycasts and clicks a genuinely uncovered
  outside region. No test-time UI repair is performed.

Fresh final focused evidence:

- Round 2 EditMode contract and generated-scene rebuild:
  `Phase5Task9Fix2-v8-EditGreen.xml`, `9/9` passed.
- Extended remaining-manual-fixture real input:
  `Phase5Task9Fix2-v8-ExtendedGreen.xml`, `1/1` passed.
- Full Round 2 generated-scene PlayMode:
  `Phase5Task9Fix2-v8-PlayGreen.xml`, `7/7` passed.
- Updated canonical-path regression:
  `Phase5Task9Fix2-AssetPathsGreen.xml`, `1/1` passed.
- Cumulative Phase 5 EditMode after the canonical-path update:
  `Phase5Task9Fix2-CumulativeEdit2.xml`, `93/93` passed.
- Final cumulative Phase 5 PlayMode after fixture isolation:
  `Phase5Task9Fix2-CumulativePlay7.xml`, `54/54` passed.
- Pointer-boundary class regression with loaded-scene isolation:
  `Phase5Task9Fix2-PointerClass4.xml`, `14/14` passed (the Unity filter's
  runnable count).
- Every listed final focused suite reports zero failed, skipped, or
  inconclusive tests.
