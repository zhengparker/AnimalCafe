# P15 Collection / Reserve Preparation Implementation Plan

> Current local checkpoint: LOCAL_COMPACT_COMPLETED: frozen537 source64D04E8A; Edit1459 PASS, original180-case Play179 PASS /0 FAIL /1 existing D2 SKIP, P15 77/77PASS. Native Main/Long fixed cases passed. New public draft models withdrawn as PREM_NOT_PROVEN; old1148 Core bytes intact. Exact receipt local-compact-final-collection-exit-handoffs-20261116/result.json. No CI/commit/push/merge/P16/formal closure.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Preserve a real Ready collection head while one authenticated same-PQS Reserve producer progresses, without adding a WAIT owner or permission.

**Architecture:** Add one internal producer descriptor and readonly observation. The coordinator owns bounded preparation debt; CFC supplies actual collection / reservation / working / paid lineage. All current proof and real joint mutation remain mandatory.

**Tech Stack:** Unity 6000.5.5f1, existing C# runtime, NUnit EditMode and real Unity PlayMode.

**Spec:** Docs/superpowers/specs/2026-11-15-p15-collection-reserve-preparation-design.md

## Global Constraints

- Existing 30-second finite preparation limit; min with earlier debt, no refresh/reset.
- Preserve Entry/key/frame/sequence/query/candidate/Frozen identity. No new WAIT edge, independent-body promise, Native permission or terminal retry.
- Same concrete PQS, actual paid-arrived FIFO head and actual Ready collection factory.
- Actual uncommitted CQ-head Reserve initially; publication and later paid movement must have real same-origin receipts.
- No Scene/save/physics-size/epsilon/production-query-budget/business-timeout/global-ordering changes.
- Root alone edits Assets and runs Unity; other agents provide outputs-only patches/reviews. Verify Editor exit before every Assets mutation.

## Review Focus

- A normal refresh replaces the Reserve preview without actually publishing it.
- Photo already has a Barista Frozen set and less than 30 seconds remaining.
- Actual reservation exists before the accepted paid task has transferred or issued Native movement.
- Callback changes either producer, pauses the coordinator, replaces an actor or reenters preparation.
- A genuine original producer progresses but the final current route is still blocked.

### Task 1: contracts, inert registration and exact RED

**Files:** ServiceTrafficContracts.cs; ServiceTrafficCoordinator.cs; Assets/Tests/EditMode/Phase14/ServiceTrafficCoordinatorTests.cs.

**Interfaces:**
- TrafficCollectionReserveReadiness(Intent, ActorLife, BusinessOwner, Admission, PickupOwner, ReadySeal, IsCurrent, Read).
- Read takes the original producer key, original preparation decision and optional producer seal, and returns TrafficCollectionReserveObservation.
- Observation contains CollectorClaim, CollectionPreview, ProducerSeal, Uncommitted, PublishedReservation, ParkingSource and PaidSource.
- TrySetCollectionReserveReadiness(expected, readiness) initially only registers the descriptor on the exact unacquired source; the inert seam must not implement waiting or scheduling.

- [x] Add the readonly contracts and inert registration.
- [x] Add ReadyCollectionHeadDefersActualUncommittedReserveWithoutNewFrozenOrPermission using public OrderService Ready, actual CQ/PQS mutations and complete Prepared source proofs.
- [x] Run that exact EditMode selector. Require the original owner-not-independent failure after every setup assertion; a compile error or failed geometric premise is not RED.

### Task 2: bounded coordinator behavior and actual CFC lineage

**Files:** ServiceTrafficCoordinator.cs; CustomerFlowController.cs; ServiceTrafficCoordinatorTests.cs.

**Interfaces:**
- Coordinator registration above remains the only opt-in. Existing consumers default to null.
- CFC binds actual ordinary PickupFamilyPreparation and actual CollectionReleasePrerequisite separately through their existing identity/current-preview contracts.
- CFC marks the reservation origin only in the real Reserve joint publication, captures a genuine cashier task in normal producer refresh, and reads actual parking / paid-source lineage without mutating inside the Core reader.

- [x] Implement the narrow preparation hold before the original failure, preserving the original whole proof frontier and frozen/debt ledger.
- [x] Debit the existing limit during normal Tick; freeze during pause; expiry is terminal.
- [ ] Authenticate every real publication / paid movement transition; no actor-ID-only inference, preview-consumption inference or added Frozen dependency.
- [x] Fence every reached external callback and restore inspection latches in finally. Clear transient references on discard, retirement, terminal failure, reset and close.
- [ ] Extend the public models for the original Barista Frozen debt, pause/expiry, real Reserve-to-paid Native progression, foreign Queue/life/source, invalid Ready, callback failure/reentry and final current-body rejection.
- [ ] Run focused models, then the whole Core class. Existing business, raw-cache, no-null-cache and budget controls remain required.

### Task 2b: actual candidate lifecycle discovered in Native

**Files:** ServiceTrafficCoordinator.cs; ServiceTrafficCoordinatorTests.cs.

- [x] Reproduce a real current collection hold, genuine bounded geometry AlternateAllowed, public declared candidate advance and new-factory registration. Require the old descriptor/currentness rejection before the repair.
- [x] On only the existing successful TryAdvanceCandidate, clear obsolete collection descriptor/preparation; keep original started debt and all original ledger/Frozen state. Permit registration only after that legitimate cleanup; same-candidate replacement stays refused.
- [ ] Verify no debt refill, invalid/terminal advance refusal and actual final fresh collection lease commit, then the whole related Core class and original Native layouts.

### Task 2c: genuine initial goal blockage and existing Hold

**Files:** ServiceTrafficCoordinator.cs; ServiceTrafficCoordinatorTests.cs.

- [x] Public RED: actual same-member completed static service-exit proof, real CQ claim and strict Bound source cover only the current service-exit goal. The current early spatial refusal must not consume the next declared family while that source is actually moving.
- [x] Prefer the existing genuine spatial Hold and Native coalescing protocol; preserve unknown/unbound/static/foreign-source behavior. Do not fabricate a refusal or add a general pending-motion protocol.
- [ ] Verify actual Native checkpoint/full-current-proof recovery and negative controls, then both original Native layouts. Preserve query limits, rights and timeout settings.

### Task 2d: original saved collection source remains on the full primary

**Files:** ServiceTrafficCoordinator.cs; ServiceTrafficCoordinatorTests.cs.

- [ ] Reproduce the exact saved Reserve → real publication → strict Paid source, then endpoint-clear / full-primary-blocked movement. Contrast with an unsaved source that retains the original rejection.
- [ ] Invoke only the existing saved-source protocol at full-primary blockage; keep existing Frozen resources and finite debt. Do not infer an original receipt from the current actor ID.
- [ ] Verify actual multi-leg Complete → next Prepared → Bound transitions, actual final clearance and fresh current collection lease.

### Task 2e: original owned Exit continuity

**Files:** ServiceTrafficCoordinator.cs; ServiceTrafficCoordinatorTests.cs; original long-counter Native tests.

- [ ] Public RED: an earlier genuinely Bound actual-exit source is omitted from the first-freeze selected-route eligibility. Preserve its Source/life/proof and real multi-leg receipt sequence.
- [ ] Extend only the authenticated original owned-source preparation protocol; an unissued Prepared leg remains ineligible as independent Native movement.
- [ ] Distinguish the latest Long snapshot-changed rejection after ten completions from the earlier new-Frozen rejection. Capture the exact currentness failure before proposing a repair.

### Task 3: Native wiring, cleanup and review

**Files:** Existing long-counter Native tests and temporary observations; Phase15 validation report and beginner guide.

- [ ] Run the original UserLongCounterSevenFast and UserPhotoCounterSevenNormal through real service/exit, without extending their original success windows.
- [ ] If a new failure appears, capture its original layer and repair only the demonstrated cause; do not retry until one random run succeeds.
- [ ] Preserve the already-proved original Side/Main cases. P13 uses the documented fixed two-support preparation and all original four-customer/Carry/Working/Decor assertions.
- [ ] Remove only the exact temporary observations; retain product fixes and useful failure-only diagnostics.
- [ ] Review the final source diff and source-bound receipts. Run the frozen compact union of original CI failures, the original 115 baseline and new navigation controls, plus related EditMode tests.

### Task 4: update PR and run full CI

- [ ] Update final documentation with actual PASS/FAIL/SKIP/NOT_PROVEN results and exact source/XML identities.
- [ ] Explicitly stage intended files only, commit and push the authorized PR15 branch. Never merge or advance to P16.
- [ ] Tell the user that local repair/validation is ready before manually dispatching full CI for the exact new PR head.
- [ ] Follow CI to completion, inspect raw NUnit artifacts and required gate. Do not substitute a wrapper exit or selected tests for full CI evidence.
