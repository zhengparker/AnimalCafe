# P15 Collection / Reserve Preparation Design

Status: LOCAL_COMPACT_COMPLETED: frozen537 source64D04E8A; Edit1459 PASS, original180-case Play179 PASS /0 FAIL /1 existing D2 SKIP, P15 77/77PASS. Native Main/Long fixed cases passed. New public draft models withdrawn as PREM_NOT_PROVEN; old1148 Core bytes intact. Exact receipt local-compact-final-collection-exit-handoffs-20261116/result.json. No CI/commit/push/merge/P16/formal closure.

## Intent / 目标

A real Ready pickup head must not fail merely because another real CQ head has a fully Prepared, uncommitted Reserve whose route crosses the collection service endpoint. Preserve the original route priority while allowing the original collection preparation to wait for the real reservation/working/paid movement lifecycle.

Actual counterexamples: DCD0784118577F1DA4B48B3D23ED16936C7517F468F6CB4606F0FD924A7E4A46. Long reaches three completed orders before owner-not-independent; Photo encounters new-frozen-owner while its original Frozen set contains only the Barista. Both blockers are Prepared pickup-wait sources with acquired sequence zero and no Native leg. They are not independent release promises.

## Approaches / 取舍

1. Change global route priority or treat Prepared as independently moving. Rejected: this changes established ownership and would manufacture a WAIT promise.
2. Rebuild only the collector primary path. Rejected: the actual refusal is protection of the fixed collection endpoint against the other complete primary, not stale primary geometry.
3. Withhold only the original, authenticated collection preparation for the same concrete PQS producer. Chosen as an implementation decision under the user's authorization to fix the remaining CI failures without repeated confirmation.

## Scope / 修改范围

- ServiceTrafficContracts.cs: internal readonly producer readiness and observation DTOs; old callers default to no opt-in.
- ServiceTrafficCoordinator.cs: exact registration, bounded preparation debt, real lifecycle authentication and cleanup; existing final proof remains authoritative.
- CustomerFlowController.cs: actual Ready collection and original uncommitted Reserve producer reader, followed by actual published reservation / original parking / paid movement lineage.
- ServiceTrafficCoordinatorTests.cs: public CQ/PQS/Order/Commit/Bind models and negative lifecycle controls.
- Existing long-counter Native tests: reproduce and verify the real application wiring. Temporary observations are removed before the frozen regression.

## Required behavior / 必须保持

- Authenticate the collector's current paid, arrived FIFO-head reservation, original actor life/business/admission, actual Ready seal and current CollectionLease preview.
- Initially authenticate one real same-PQS CQ head at slot zero, arrived, with a complete Prepared unacquired Reserve and its actual current producer/preview. Generic or foreign-PQS proposals cannot opt in.
- A Reserve preview can be regenerated during normal refresh. Its disappearance or an old cookie's Consumed value does not prove publication. Follow actual published reservation and original parking / working / paid-source lineage.
- Retain the same receiver Entry, key, first frame, sequence, queries, candidate indices and original Frozen owners/resources. Add no WAIT edge or movement/parking permission.
- Use the existing finite preparation limit of 30 seconds. Preserve the smaller existing debt, including the observed Photo debt of about 29.4 seconds. Pause freezes debt; repeated refresh and phase changes never refill it. Expiry is terminal and cannot auto-retry.
- Real source invalidation, actor replacement, layout/epoch change, wrong Queue or failed callback cannot become a positive readiness result. Guard callback reentry and restore inspection latches in finally.
- After actual progression, prove every current body, canonical claim, route and endpoint again before the real joint collection mutation or Native issue.
- No Scene/save migration, new dependency, collider/radius/epsilon change, global scheduling change, production query-budget increase or enlarged business-test timeout.

## Actual declared-candidate transition / 实际候选切换

`D3FD458FF004355A1CB6E6AA959BE7816464FF74904A59DBCE0DD618A35FEA36` captures the ordinary collection factory changing from candidate 0 to 2 on the same actual cursor. Actor, phase, generation, phase sequence, selection, issued/pending movement and starting pose are unchanged. The old factory is no longer current, while Core retains its old readiness descriptor and rejects the new descriptor because preparation debt has already started.

The existing successful `TryAdvanceCandidate` is the authority that ends the old candidate. On that exact success, retire only that candidate's collection descriptor and producer-preparation references. Preserve the original finite-debt-start flag, remaining debt, Entry/key/age/sequence/query ledger and Frozen owners/resources. Allow the existing CFC derived current factory to register after that legal cleanup. A same-candidate replacement, invalid advance or terminal source cannot use this transition. New candidate proof and actual collection joint commit remain mandatory.

This is a lifecycle repair in ServiceTrafficCoordinator.cs and public Core tests. It does not expand CFC currentness, Ready-cup validity, scheduling, route ownership or candidate budget.

## Actual moving body covers the initial service-exit goal / 初始出口被真实移动身体覆盖

After the candidate-factory repair, `4F4EBBB53F65CEC8223F96FDB42012C2EF75F8BAD094D5A84A690356BFAE11D5` proves all 20 Long customers complete and actually exit. Photo instead exhausts the unchanged 16384 query budget before its first order. Original events record 8092 queries spent through candidates 0–13, followed by exhaustion in candidate 14's current service-exit role. The original graph has no completed route; this is not a finishable-path or cache-accounting claim.

At the first recorded candidate refusal, the actual incoming customer's body is about 0.699 from the current service-exit goal, within its 0.911 clearance. The current early goal-obstacle check can consume alternate families while that actual body is moving. The smaller repair candidate reuses existing unpaid eligibility Hold/coalescing only after a genuine goal-occupied refusal: one authenticated Bound source covers the goal, the same formal member's static counterpart is actually complete, and every other current declared/static/local/parking constraint remains clear. Unknown, unbound, replaced or static obstruction keeps the original failure path. It introduces no new metadata protocol, fake refusal, rights, WAIT edge, query allowance, timeout or automatic terminal retry.

ServiceTrafficCoordinator.cs and its public tests are the implementation scope. A precise public RED must precede the product change, then the original Photo and Long Native cases and final frozen regression. Existing Hold clears computed jobs and reuses only its already-authorized raw positive Native memo; it does not promise to preserve those jobs.

## Verification / 验收

First prove the old owner-not-independent / new-frozen-owner failures using actual public claims and prepared mutations. Then cover real Reserve-to-paid movement progression, existing Frozen debt, pause/expiry, wrong identity/PQS/source, provider exception/reentry and final full-current-proof rejection. Run the original two long-counter cases and the previously repaired Side/Main controls, followed by the frozen compact regression and source-bound full manual CI. A skipped precondition remains NOT_PROVEN.
