# M-009 / M-016 computer-use attempt

Date: 2026-10-14. Workspace: E:/Unity/Project/AnimalCafe/.worktrees/phase-13-employee-task-flow.

User requested actual computer-use repeat after reporting both seemed okay. This is not an unconditional Owner PASS declaration.

Method: Windows computer-use skill, @oai/sky, real Unity window and pointer input. No Unity test API or scripted gameplay substituted for manual interactions.

Observed: opened MainCafe through Project browser; entered Play; initial missing-device state has no employees; opened Decor; Catalogue single counter and 1x3 choices worked, and real checkmark confirmed placements; CR card click created its preview. Console visible counters remained 0 error / 0 warning during this limited attempt.

Blocker: repeated supported sky.drag calls on the visible counter and on Catalogue content did not move the counter or scroll the list. Ordinary UI buttons worked. Wheel scroll moved the camera; source inspection shows Catalogue scrollSensitivity=0, so wheel is not evidence of a drag failure. Root cause of unsuccessful drag is UNKNOWN: do not claim product regression or proven tool defect.

M-009: BLOCKED during MainCafe preparation. Four-customer coexistence/FIFO/decoration reset sequence NOT_RUN this attempt.
M-016: PARTIAL observations only (incomplete shop -> Decor, counter placement). Middle slot/cups/move/rotation and both-aspect active task UI NOT_RUN this attempt. Overall BLOCKED, not PASS.

Evidence: [main-cafe-ui-attempt.png](main-cafe-ui-attempt.png) is an actual Windows Unity screenshot taken during this attempt. No code or assets changed or Scene saved. Exited own Play run and returned to original Phase13EmployeeTasks scene. Runtime test layout was not saved. Existing automated PASS evidence stays separate.

## Later Owner acceptance

Owner subsequently confirmed both manual drags work normally, then explicitly requested M-009 and M-016 be recorded as Owner manual PASS. This supersedes the pending Owner acceptance status, not the BLOCKED computer-use attempt above. No claim that the tool completed those cases or that its input issue was diagnosed.
