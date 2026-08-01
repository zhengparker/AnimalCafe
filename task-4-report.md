# Phase 3 Task 4 — Collider, Missing References, Batch Validation Menu

## Scope

- Added Collider validation to the Editor-only benchmark Prefab validator.
- Added deterministic batch validation for the three approved benchmark paths.
- Added the read-only manual menu item `AnimalCafe/Validation/Validate Benchmark Assets`.
- Added real-Prefab EditMode regression tests. No `ArtSource`, production benchmark assets, `MainCafe`, spec, or plan files were changed.

## TDD evidence

- RED: the new batch tests first failed to compile because `BenchmarkAssetValidator.ValidateAllBenchmarks()` did not exist (`CS0117`, recorded in `Task4-RED-EditMode.log`).
- GREEN focused contract fixture: `40` passed, `0` failed, `0` inconclusive, `0` skipped.
- GREEN whole AssetPipeline fixture suite: `67` passed, `0` failed, `0` inconclusive, `0` skipped.

## Implemented contracts

- Only exact `BoxCollider`, `SphereCollider`, and `CapsuleCollider` types are permitted; `MeshCollider` and all other Collider types report `InvalidColliderType`.
- Collider count uses the asset-kind budget. Trigger Colliders report `TriggerColliderNotAllowed`.
- Collider validation compares `Collider.bounds` with combined visible `Renderer.bounds` in world space. It permits a `0.05 m` envelope but rejects below-floor bounds below `Y = -0.005 m`.
- A missing enabled Renderer Material reports both the specific `MissingMaterial` issue and the batch-friendly `MissingReference` issue.
- Batch paths are fixed and ordered as Work Table, Coffee Machine, Ceramic Cup. Missing Prefabs retain one `MissingReference` issue per path; invalid assets are all aggregated in the same immutable report.
- The menu only logs results and selects an existing invalid asset. It does not modify Prefabs or import settings.

## Fresh regression evidence

| Suite | Passed | Failed | Inconclusive | Skipped |
|---|---:|---:|---:|---:|
| AssetPipeline EditMode | 67 | 0 | 0 | 0 |
| Full EditMode | 258 | 0 | 0 | 0 |
| Full PlayMode | 35 | 0 | 0 | 0 |

Result XML files are intentionally generated evidence and are not part of this commit.
