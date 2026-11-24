# Pick-up exit eligibility repair

Status: Owner approved 2026-10-28, reply “修复吧” to the two-file scope below. This records the approved conversation; no further approval gate is needed for its ordinary implementation.

## Intent / 目的

后台经营不应依赖“收银队首暂时无人”来容纳取餐队伍。顾客取得取餐服务侧时，该侧也须具备安全离店路线。修复这两项 eligibility，保持真实身体、安全检查和既有显式恢复规则。

## Evidence and limits

Baseline production CFC5BC45055 / Flow9A972C / RunnerB8B438; latest accepted matrix remains FAIL: original8 7P1F, controlled2 2P. Quantized Long7 snapshot Full12 yields no original planner route; removing only the named C12 Counter front body yields a left boundary route, independently clear against the other11 but blocked by C12. This is a reconstructed, quantized fixture, not the historical original decision or proof of global no-path. See `outputs/phase15/owned-service-route-repair/quantized-exit-query/report.md`.

Source confirms `EnsurePickupQueue` plans while the caller occupies Counter head, then excludes that caller in `PickupPlanCanExit`; later customers reuse `counterWaitSlots[0]`. Collection currently verifies the route to its chosen side and staff traffic, but not that chosen side's exit. Early `Any(side)` eligibility does not bind final side selection.

## Approved behavior

1. In existing Pick-up geometry eligibility, preserve the valid Counter head's possible standing area. Use existing customer radii and legal arrival tolerance to represent a continuous occupancy envelope. Do not globally add a new obstacle or change physical/NavMesh dimensions. Preserve the calling customer's ability to leave Counter normally.
2. Before collection-side lease acquisition/publication, check the actual candidate side can safely reach the existing exit, using the original bounded planner and full applicable bodies/targets/leases. Reject an unsafe candidate before publication; another eligible side may succeed. Preserve existing actual owned-route and employee traffic checks.
3. These are eligibility rules, not future exit ownership: no cached future exit lease, new traffic manager, automatic retry of Blocked, or unconditional permanent WAIT. Unavailable candidates remain refusals; current strict finite proofs are not loosened.

Implementation clarification from diagnostic-runtime-02: rule1's valid Counter-head envelope must also constrain rule2's actual selected-side exit eligibility, including the final expanded path. Otherwise early Any(side) can pass using a different side while final raw-native planning depends on Counter head staying empty. The captured frame810 upper-side route passed within0.05 of Counter head and that head was actually occupied at frame1669. Reuse the same head/formula and original bounded query; no NULL semantic change or future ownership is implied. An unsafe raw path does not forbid that side permanently: a safe route found within the unchanged planner remains eligible. This may conservatively refuse additional candidates; normal actual collection/exit and full physical-blocker rejection remain required checks.

## Scope

- Product: `Assets/Scripts/Customers/CustomerFlowController.cs` only.
- Tests: `Assets/Tests/PlayMode/EditorSceneLoading/Phase14/CafeLongCounterStallTests.cs` only.
- Update living design, roadmap, validation report, beginner guide and implementation plan with the real resulting behavior and evidence.
- No Scene/prefab/meta/Save migration; no other runtime files, navigation behavior, collision settings, capacity limits, payment/FIFO/tokens or formal 28800/7200 cycle changes. No Git/CI/push/merge/install/cleanup actions.

## Approaches

The approved recommendation is the two local eligibility checks above. A future exit cache would add a new multi-phase ownership lifecycle; it is outside this scope. Expanding the planner budget has no supporting evidence and would not explain the lost Counter-head occupancy assumption. Existing bounded planning is retained.

## Risks / acceptance

Effective usable Pick-up slots may decrease or layout eligibility may refuse a previously accepted arrangement. Legal arrival offsets and different configured customer radii must be accounted for without sampled-point safety claims. Construction failures and collection refusals may retry through existing callers; record their query/decision cost and ensure no new per-frame navigation observer or unconditional wait is added. Preserve normal nonconflicting acceptance and actual later progress, not just a refusal assertion.

Verify meaningful product RED then focused positive/negative/lifecycle tests, old static Blocked plus explicit recovery, original finite-proof/identity cases, independent task review, and original8 with unchanged real20-exit assertions. Complete applicable existing P14/P15 integration gates once the key matrix is green. Main input failure, historical same-cause, device/Owner/manual/CI/merge and formal Phase closure remain separate evidence boundaries.
