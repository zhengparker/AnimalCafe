# Phase 6 Studio Owner manual review — P6-M-001–030

Status: `accepted — 29 / 29 applicable cases PASS`

Final Studio Owner acceptance（2026-08-22）：`P6-M-001–022` and
`P6-M-024–030` all passed after the approved UI/input rework and follow-up
polish. `P6-M-023` is excluded from this denominator and remains scheduled for
real Android + iOS device verification in Phase 51. No other manual limitation
was accepted for Phase 6.

Scope decision（2026-08-22）：`P6-M-023` is not part of the Phase 6 acceptance
denominator。Its physical two-finger device acceptance moved to Phase 51 — Android
& iOS Platform Adaptation。The row remains only as a traceability record。

Execution rule: run the production `MainCafe` Scene with eligible physical Touch
unless a row explicitly calls for an Editor/RenderTexture composition check. Record
the actual resolution and device used. Existing simulated Touch and screenshots do
not substitute for physical Touch feel or physical Safe Area acceptance.

The detailed row columns below remain the reusable execution worksheet. The
final acceptance record above is authoritative for Phase 6 closeout.

| ID | Scene / resolution / device | precondition | action | expected | actual | PASS / FAIL / BLOCKED | evidence path | notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| P6-M-001 | MainCafe / Windows / record actual resolution / Mouse | Game Time running at a known speed; Decoration Mode closed. | Click `Decor`; observe the full right rail, Catalogue entry motion and Grid; then click `Done`. | Confirm the rail order is `Done / indicator / Pause / 1× / 2×`; Pause is visibly selected, `1× / 2×` are disabled, Catalogue is Expanded, and `Done` restores the exact pre-entry speed without overlap or clipping. |  |  |  |  |
| P6-M-002 | MainCafe / Windows / record actual resolution / Mouse | Decoration Mode open; Catalogue Expanded; no active Preview. | Inspect all four Counter tiles and the current player-visible copy; select one tile and hover each floating action symbol. | Confirm recognizable previews, consistent angle, clear `1 × 1 / 1 × 2 / 1 × 3 / 2 × 3` footprints, readable English copy, and exact English tooltips `Store / Cancel / Rotate / Confirm` where each action is available; do not infer Chinese-localization acceptance. |  |  |  |  |
| P6-M-003 | MainCafe / Windows / record actual resolution / Mouse | Fresh Expanded Catalogue; no active Preview. | Select `1 × 1`, left-drag it across multiple cells, use Mouse Wheel once, then click Rotate and Confirm. | Confirm Catalogue collapses to its visible handle; Preview follows Mouse with visible footprint/placement feedback; Wheel affects only Camera zoom; floating actions stay near the Preview without covering the right rail; Confirm creates exactly one formal furniture. |  |  |  |  |
| P6-M-004 | MainCafe / Windows / record actual resolution / Mouse | Fresh Expanded Catalogue; no active Preview. | Select `1 × 2`, left-drag it, Rotate to `2 × 1`, move again and Confirm. | Confirm the two-cell highlight and model move together, rotation updates to `2 × 1`, the floating actions remain reachable and non-overlapping, and Confirm produces one unstretched formal instance. |  |  |  |  |
| P6-M-005 | MainCafe / Windows / record actual resolution / Mouse | Fresh Expanded Catalogue; no active Preview. | Select `1 × 3`, left-drag it, Rotate to `3 × 1`, move again and Confirm. | Confirm the three-cell highlight and model move together, rotation updates immediately, the floating actions remain reachable and non-overlapping, and Confirm produces one unstretched formal instance. |  |  |  |  |
| P6-M-006 | MainCafe / Portrait / eligible Touch device | Fresh Catalogue state; no active Preview. | Select and place `2 × 3`；Rotate once；move and Confirm。 | Confirm six-cell highlight、rotation to `3 × 2` and readable footprint in Portrait。 |  |  |  |  |
| P6-M-007 | MainCafe / record actual / eligible Touch device | Initial `1 × 1 Counter Module` formal furniture present; no active Preview. | Tap initial `1 × 1 Counter Module`。 | Confirm immediate suspended Preview and compact action bar。 |  |  |  |  |
| P6-M-008 | MainCafe / record actual / physical Touch required | Each of the four sizes is available in a fresh Preview state. | Drag every size。 | Confirm finger does not hide furniture / footprint and Preview position feels connected to Touch。 |  |  |  |  |
| P6-M-009 | MainCafe / record actual / eligible Touch device | Active Preview on a valid cell. | Inspect valid placement feedback。 | Confirm green plus non-color cue is clear without overpowering Floor / furniture art。 |  |  |  |  |
| P6-M-010 | MainCafe / record actual / eligible Touch device | Active Preview; another formal furniture is available for overlap. | Overlap another furniture。 | Confirm red plus non-color cue、specific `这里已有家具` message and disabled Confirm。 |  |  |  |  |
| P6-M-011 | MainCafe / record actual / physical Touch required | Each asymmetric size is available in a fresh Preview state. | Drag every asymmetric size toward multiple Floor edges。 | Confirm the full footprint remains on the Floor and stops at the nearest legal edge cell；it must not visually or logically move outside the decoration area。 |  |  |  |  |
| P6-M-012 | MainCafe / record actual / eligible Touch device | Active multi-cell Preview; Entrance Clearance visible. | Drag a multi-cell preset into the Entrance zone。 | Confirm placement is blocked and reason is specific。 |  |  |  |  |
| P6-M-013 | MainCafe / record actual / eligible Touch device | Active Preview positioned near an obstacle. | Rotate from valid to invalid and back。 | Confirm center does not jump far、highlight updates immediately and no hidden Confirm occurs。 |  |  |  |  |
| P6-M-014 | MainCafe / record actual / eligible Touch device | Initial Counter formal furniture present; record its original position / rotation. | Move and rotate initial Counter；Cancel。 | Confirm exact original position / rotation returns。 |  |  |  |  |
| P6-M-015 | MainCafe / record actual / eligible Touch device | Fresh Catalogue state; no active Preview. | Select a new preset；move；Cancel。 | Confirm it disappears completely and Catalogue returns。 |  |  |  |  |
| P6-M-016 | MainCafe / record actual / eligible Touch device | Furniture A and B are selectable; no active Preview. | Edit A then tap B。 | Confirm A automatically returns / disappears as applicable and B becomes the only active Preview。 |  |  |  |  |
| P6-M-017 | MainCafe / record actual / eligible Touch device | Run once with an active Preview and once with no Preview. | With active Preview, tap blank Floor。Without Preview, tap blank Floor。 | With active Preview, confirm nothing is cancelled。Without Preview, confirm ordinary selection clears。 |  |  |  |  |
| P6-M-018 | MainCafe / record actual / eligible Touch device | Existing formal furniture selected in active Preview. | Tap Store；while Modal is open, tap its blocking backdrop directly over a selectable lower-layer furniture or Floor to dismiss；confirm that release does not pass through；reopen and confirm Store。 | Confirm the blocking backdrop owns the dismiss input：lower Catalogue / Action Bar / Scene receives no selection、movement、Preview restart or action；fresh input remains usable；final Store removes only the selected furniture。 |  |  |  |  |
| P6-M-019 | MainCafe / record actual / eligible Touch device | Existing furniture moved but not confirmed. | Exit Decoration Mode。 | Confirm original position returns and no warning Modal appears。 |  |  |  |  |
| P6-M-020 | MainCafe / record actual / eligible Touch device | Decoration Mode open; multiple changes ready to Confirm. | Confirm multiple changes；exit and re-enter Decoration Mode。 | Confirm the current runtime Layout remains。 |  |  |  |  |
| P6-M-021 | MainCafe / record actual / physical Touch required | Decoration Mode open; no active Preview; blank Floor visible. | Single-finger drag from blank Floor。 | Confirm only Camera moves。 |  |  |  |  |
| P6-M-022 | MainCafe / record actual / physical Touch required | Decoration Mode open; selectable furniture visible. | Single-finger drag from Furniture。 | Confirm Furniture moves and Camera stays stable except approved edge auto-pan。 |  |  |  |  |
| P6-M-023 | Real Android + iOS devices / Phase 51 | Active Furniture drag in progress. | Add second finger and Pinch。 | Confirm Camera zooms、Furniture stays pending and no Confirm / Cancel / Store occurs。 | Not run in Phase 6；moved by Studio Owner scope decision on 2026-08-22。 | N/A — moved to Phase 51 | Phase 51 device evidence | Excluded from the Phase 6 acceptance denominator；historical ID retained for traceability。 |
| P6-M-024 | MainCafe / record actual / physical Touch required | Each furniture size available; each usable viewport edge identified. | Drag each furniture size near all usable viewport edges。 | Confirm direction、activation zone、speed curve and stop behavior feel controllable。 |  |  |  |  |
| P6-M-025 | MainCafe / physical device Safe Area / physical Touch required | Catalogue and action bar can be opened; active Preview available. | Exercise Catalogue / action bar transitions near device edges。 | Confirm Catalogue / action bar transitions do not hide furniture or essential actions；Safe Area does not cause edge-pan misfires。 |  |  |  |  |
| P6-M-026 | MainCafe / `1080 × 1920`, `720 × 1280`, `1080 × 2400` / record runner or device | Fresh equivalent state at each Portrait size. | Check reference、small and tall Portrait。 | Confirm no clipping、overlap or unreachable controls。 |  |  |  |  |
| P6-M-027 | MainCafe / `2400 × 1080` / record runner or device | Fresh Landscape fallback state. | Review the full Decoration action flow in Landscape。 | Confirm all actions remain usable and readable without requiring final Landscape polish。 |  |  |  |  |
| P6-M-028 | MainCafe / record actual / Studio Owner | Do not read implementation notes before this case. | Select、drag、rotate、confirm、cancel and store；record any confusing label or hidden state。 | Confirm it is clear how to perform every action without implementation notes。 |  |  |  |  |
| P6-M-029 | MainCafe / record actual / eligible Touch device | Canonical initial Layout; Game Time running. | Enter / exit twice；perform Confirm、Cancel and Store across sessions。 | Confirm no duplicate UI、Grid、Furniture、permanent Pause or broken input。 |  |  |  |  |
| P6-M-030 | MainCafe / record actual / eligible Touch device | Fresh mixed-placement session; Console open for final review. | Complete a mixed placement session and resume Game Time。 | Console must contain no unexpected Error / Exception or unexplained Warning。 |  |  |  |  |

## Supplementary Reduced Motion observation

This is not a new `P6-M` row or denominator. During `P6-M-001`, record whether
the MainCafe Catalogue, action-bar and Store-Modal motion causes discomfort,
delays understanding or makes ownership ambiguous. In a separate fresh Editor
session, compare ordinary and immediate-settle presentation with the existing
`Reduced Motion Toggle` in `Assets/Scenes/Validation/Phase5UiFoundation.unity`.
Do not save the Validation Scene.

| context | observation | evidence path | notes |
| --- | --- | --- | --- |
| MainCafe baseline motion |  |  |  |
| Phase5 Validation Reduced Motion comparison |  |  |  |

## Manual tuning record

Accepted values or observations begin blank. Any value changed after acceptance
requires the affected focused tests and relevant manual cases to be rerun.

| tuning field | accepted value / observation | affected manual cases | evidence path | notes |
| --- | --- | --- | --- | --- |
| Preview hover height |  | P6-M-003–016, P6-M-022–024 |  |  |
| touch drag offset |  | P6-M-008, P6-M-011, P6-M-022–024 |  |  |
| drag threshold |  | P6-M-003–008, P6-M-021–024 |  |  |
| edge auto-pan zone |  | P6-M-011, P6-M-024–025 |  |  |
| edge auto-pan speed curve / maximum speed |  | P6-M-011, P6-M-024 |  |  |
| Bottom Sheet collapsed / action height |  | P6-M-001–002, P6-M-007, P6-M-025–027 |  |  |
| Grid line opacity |  | P6-M-001, P6-M-009–012 |  |  |
| valid / invalid intensity |  | P6-M-009–013 |  |  |
| transition timing |  | P6-M-001, P6-M-015, P6-M-018, P6-M-025, P6-M-028 |  |  |
| Portrait Camera framing and furniture readability |  | P6-M-003–008, P6-M-020, P6-M-026 |  |  |
