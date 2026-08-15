# Phase 5 Manual Review — MT001–MT010 Acceptance and Button Guide

Date: 2026-08-13
Reviewer: Studio Owner
Scene: `Assets/Scenes/Validation/Phase5UiFoundation.unity`
## Acceptance record

The Studio Owner manually reviewed MT001 through MT010 and explicitly confirmed that all ten cases pass.

| Test | Result |
|---|---|
| MT001 | PASS |
| MT002 | PASS |
| MT003 | PASS |
| MT004 | PASS |
| MT005 | PASS |
| MT006 | PASS |
| MT007 | PASS |
| MT008 | PASS |
| MT009 | PASS |
| MT010 | PASS |

This is the manual acceptance record. Automated test totals are separate evidence and do not replace the Studio Owner result above.

## Persistent page selectors

| Button label | Location | Purpose |
|---|---|---|
| Buttons | Header | Opens the button role/state gallery. |
| Panels | Header | Opens the Solid, Light Frost, Strong Frost, and fallback comparison page. |
| Navigation | Header | Opens pause, modal, Back, interruption, and world/UI input-isolation fixtures. |
| Feedback | Header | Opens Toast, Tooltip, Validation Message, and Bottom Sheet fixtures. |
| Responsive & Motion | Header | Opens Reduced Motion, Safe Area, and long localized text fixtures. |

## Buttons tab

The 3 x 3 gallery is a visual state matrix, not nine different gameplay commands.

| Column | Meaning |
|---|---|
| Default | Normal reusable button. These samples can be pressed to inspect the live pressed response. |
| Pressed Preview | Persisted visual sample showing how the role looks while pressed. It is a preview, not a second command. |
| Disabled | Disabled visual sample. It must not respond to input. |

Rows are `Primary`, `Secondary`, and `Destructive` roles.

## Panels tab

| Button label | Purpose |
|---|---|
| Show Solid Panel | Shows the opaque Solid panel preview. |
| Show Light Frost Panel | Shows the routine lightweight frost preview. |
| Show Strong Frost Panel | Shows the stronger frost preview reserved for higher-emphasis surfaces. |
| Force Frost Fallback | Requests Strong while Strong is unavailable and proves the readable Light Frost fallback. |
| Open Second Strong Frost | Hidden diagnostic control used by automated ownership tests; it is not part of the normal manual UI. |

## Navigation tab

| Button label | Purpose |
|---|---|
| Pause Game | Acquires the validation pause reason and pauses game time. |
| Continue Game | Releases that pause reason and resumes game time. |
| Open Modal | Opens the critical `Discard changes?` Modal. |
| Test World Occlusion | Covers the selectable world target and proves a UI click does not select the world behind it. Its position is intentionally tied to the world target rather than the action-button grid. |
| Handle Back | Sends the shared Back action. It closes only the current top dismissible container. |
| Interrupt And Reopen | Disables the first Modal during its lifecycle, then re-enables/reopens it to test cleanup and recovery. |
| Open Second Modal | Appears inside the first Modal and opens a second Modal above it. |
| Cancel | Closes the current Modal without confirming. |
| Discard | Confirms the destructive action and closes the current Modal. |

The second Modal can dismiss back to the first. The first critical Modal deliberately requires `Cancel` or `Discard` rather than an outside click.

## Feedback tab

| Button label | Purpose |
|---|---|
| Show Toast | Shows one short non-blocking `Saved` Toast. |
| Show Tooltip | Opens the touch-accessible Tooltip. |
| Show Validation Error | Shows a persistent, specific validation reason. |
| Open Bottom Sheet | Opens the `Order details` Bottom Sheet. Game time continues behind this sheet. |
| Repair Validation | Marks the sample input valid and clears the validation error. |
| Show Toast Burst | Enqueues three Toast requests and demonstrates consecutive duplicate merging. |
| Long Press Tooltip | Hold for about 0.5 seconds to open the long-press Tooltip path. |
| Close Tooltip | Closes the currently open Tooltip. |
| Cancel (Bottom Sheet) | Closes the Bottom Sheet without confirming. |
| Confirm (Bottom Sheet) | Confirms the sheet action and closes it. |

The Bottom Sheet is dismissible through Cancel, Confirm, outside click, or Back; all routes return to the Feedback page.

## Responsive & Motion tab

| Button label | Purpose |
|---|---|
| Toggle Reduced Motion | Toggles the Reduced Motion sample status between On and Off. |
| Confirm Safe Area | Proves a critical control remains reachable inside the simulated Safe Area. |

The long mixed CJK/Latin label is a wrapping and localization stress fixture, not a button.

## Layout audit — pending visual cleanup

- Header selectors: consistent five-column row.
- Buttons gallery: consistent 3 x 3 matrix.
- Panels: the four 180 px buttons currently use 180 px center spacing, so their edges touch. Add a visible horizontal gap.
- Navigation: actions currently use unrelated x/y placements and should be reorganized into a clear grid plus a separate world-occlusion test area.
- Feedback: the four primary actions are consistent; the repair and advanced Toast/Tooltip controls should use a second aligned row.
- Responsive & Motion: only two actions with distinct purposes; keep them centered and label their status areas clearly.
