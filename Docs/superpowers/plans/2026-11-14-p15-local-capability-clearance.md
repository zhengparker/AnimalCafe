# P15 Local Capability Clearance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** A single complete capability path must obey each declared obstacle clearance without changing actor collision policy.

**Architecture:** Add immutable local geometric conditions to TrafficRouteRequirement. Reuse the existing RouteJob, query scheduler, complete-path checks and metadata revalidation; local conditions create no body, claim or standing right. This slice does not change queue publication or add a second planner.

**Tech Stack:** Unity 6000.5.5f1, C#, NUnit, existing Native test runner; no new package or assembly.

**Spec:** [Service traffic design](../specs/2026-10-30-service-traffic-coordination-design.md), especially sections on complete proofs, simultaneous preparation and bounded cost; [original implementation constraints](2026-10-30-service-traffic-coordination.md). Native evidence: 7345359CEAF8582A94F95B795CAAE00353DD13EF36B27F1A981BF88AC0D061BA and 184A22E2EEDA13E8FC29BB3EE10B7863EFB7BDCB0FF0E1A7FC5DFD845694D5B8. The latter disproved the straight reference-lane prototype, which was reverted.

## Global Constraints

- 当前每帧 Native query / proof-step budget 为 128，同一 intent query 总预算为 16384；不改变预算、age、sequence 或有限等待债务。
- 保留 actor radius、collision skin、epsilon、arrival/sample tolerance、真实碰撞与完整端点/连接线检查。
- 只增加能力路径的局部几何条件；不建立未来身体、停车权利或无限 WAIT，不自动重试 Terminal。
- P15 Open 28800 seconds / Rest 7200 seconds 与付款、容量、FIFO不变；不修改 Scene、存档或 GUID。
- 本轮已授权修复并运行 CI；PR #15 保持未 merge。旧文档的禁止 push/CI 不覆盖 Owner 后续明确授权。
- 模型 PASS 不代表 Native 场景或完整 CI PASS。已知五人、连续包络及其余旧失败仍分别记录。

## Review Focus

1. The same polyline must satisfy both ordinary and local clearance (Task 1 joint-detour / closed-box cases).
2. A local declaration changing on the same unacquired intent invalidates its old proof without resetting spent queries (Task 1 metadata case).
3. Invalid local coordinates or clearance fail before calling Native (Task 1 six invalid cases).
4. Caller array mutation cannot rewrite a retained requirement (Task 1 immutable-copy control).
5. Completed proof reuse, unfinished frontiers and strict-leg revalidation all retain the local union (Task 2 audit and focused tests).

---

### Task 1: Contract seam and public model RED

**Files:** Modify `Assets/Scripts/Navigation/ServiceTrafficContracts.cs`; test `Assets/Tests/EditMode/Phase14/ServiceTrafficCoordinatorTests.cs`.

**Interfaces:** `readonly struct TrafficRouteObstacle(Vector3 position, float clearance)` exposes Position/Clearance. `TrafficRouteRequirement.LocalObstacles` is a copied read-only list; the final constructor argument is `IEnumerable<TrafficRouteObstacle> localObstacles = null` and defaults to empty.

- [x] Add the immutable contract seam and 11 public request/refresh models; do not consume the new field in Core yet.
- [x] Run only the LocalRouteObstacles cases with the existing run-native.ps1 EditMode filter. Expected: the joint-path, closed-box, six invalid and metadata cases fail for the ignored constraint; default-empty and copy controls pass. Compilation failure does not count as behavioral RED.
- [x] Bind the XML and exact three source hashes before implementing behavior.

### Task 2: Complete obstacle union and identity

**Files:** Modify `Assets/Scripts/Navigation/ServiceTrafficCoordinator.cs`; retain Task 1 tests.

**Interfaces:** Private `RequirementObstacles(TrafficRouteRequirement)` returns the ordinary Obstacles at the original Clearance plus each LocalObstacle at its own Clearance. Do not flatten clearances to one maximum.

- [x] Use this union for initial jobs, JobBaseObstacles, completed prepared-parking proofs and retained strict-leg checks.
- [x] Include local values/order in SameRequirements and finite/positive validation. Preserve existing schema-only refresh semantics; geometry changes trigger real revalidation.
- [x] Verify no request-copy helper drops LocalObstacles and no query/BuildNodes/Rights/CanProgress/budget behavior changes.
- [x] Run the same 11 models as part of the complete ServiceTrafficCoordinatorTests and NavigationServiceTests once (896 total). Investigate any new failure before broadening.
- [ ] Obtain independent diff review and record the actual compiled source/XML. Commit the coherent slice with truthful validation status; do not claim P15 completion.

### Follow-up boundary

CustomerFlowController integration needs a separate, explicit finite candidate/publication design. A queue must not be replaced after any real owner/row is published, and candidate changes cannot renew the intent budget. First identify the Five failure on the original pre-clear hook. Only then define and test same-source geometry planning and real PQS binding; do not silently broaden this contract-only slice into that lifecycle change.

Self-review: interfaces, defaults, numerical constraints and model/native evidence separation are fixed above. All five review inputs map to a task. There are no Scene or dependency changes.

## Executed receipts (2026-11-14)

- Behavioral RED: 11 cases = 9 FAIL / 2 PASS; XML SHA256 `6CAE1DA9B0A6793213167EEE615C7B3FD1BA9F01D48227B6B13A12AD77F2DE5D`.
- GREEN: 896 / 896 Core + NavigationService, no skip; XML SHA256 `2475FB170409F24A4FAEA430365717936DFDEF88D8B619B04F41A9B46ABA5500`. This includes all 11 local-clearance cases.
- Compiled Core `84C4CD08354363945D2BDC3DA2F7F462E68B6E02F743AE510169DECB6E1AD978`; Contracts `8C452F9648AD7FFCF062C3C27C75F96CD46EB15D649F14EA8DF004C5D14AD6D4`; model `D43DEF5941624CEF77BCCEE4D8C28315DADA64EFD99ABB2CF7BBFD24E4D3F137`.
- Independent static review C0/I0; process-release receipt: 0 Editor workers, unchanged 3192 Assets / 469 sources / 3 inputs / 28 scenes. Native OS handles remain UNKNOWN.
- Ruling: run the same 11 plus old885 in one896 GREEN batch, instead of duplicating the 11 in a separate launch; identical assertions and less import overhead.
- Commit remains pending; CFC producer integration and full CI/P15 acceptance are separate open work.
