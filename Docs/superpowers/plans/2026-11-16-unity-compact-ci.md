# Unity compact CI implementation record

**Goal:** Push tested P15 repairs to existing PR15 and make Unity CI use the exact approved local compact scope.

**Architecture:** Two versioned selector files are shared by local CLI and pinned GameCI. A Python standard-library verifier checks both actual NUnit reports. Existing manual authentication and one-activation boundaries remain.

**Tech Stack:** Unity6000.5.5f1, existing GameCI v4.3.2 pin, Python3, GitHub Actions.

**User specification:** 2026-11-16 spoken request approved pushing the repaired P15 branch and replacing routine full CI with the same compact regression as local. Keep PR open; no merge.

- [x] Inspect branch/PR15/current manual workflow and frozen local source64D04E8A.
- [x] Copy exact local4Edit/100Play selectors to `Tools/Testing` and verify byte parity.
- [x] Make compact the default and eligible required check; retain full and diagnosis boundaries.
- [x] Verify failure/missing/inconclusive/skip/discovery/coverage behavior with offline tests and actual1459/180 NUnit XML; no new Unity run required for CI-only edits.
- [x] Independent review, including RED then repair for duplicate/fullname and missing-selector acceptance.
Publication: commit/push is authorized; final remote head readback is recorded in PR15 and the local publication receipt. Do not merge or claim cloud CI success.
