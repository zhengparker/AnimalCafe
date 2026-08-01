# Phase 3 Benchmark Asset Provenance

This file records asset provenance facts and explicitly does not make a legal
license determination. The Studio Owner must confirm use rights before any
release or broader production use.

## Source receipt and recovery record (2026-08-01)

- The initially supplied Blender inputs were not retained as raw copies before
  an early cleanup attempt. Only the Work Table input was recoverable from the
  workstation at that point.
- The Studio Owner then re-supplied all three original Blender inputs. Before
  any subsequent cleanup, they were copied unchanged into the `Raw/` folders
  listed below and verified by SHA-256.
- The separately recovered Work Table input is also retained for traceability
  at `Raw/WorkTable/SM_Benchmark_WorkTable_01_original.blend` (SHA-256
  `B38CA2037E36D408E6C1CBA865D4ECE17E64D0C86F46185E987CB6516BE56E4A`).
- `Raw/` is read-only source evidence for this task. Under the 2026-08-01
  Studio Owner override, the matching `Blender/` files are byte-identical
  authoritative copies used for LOD0 export rather than rebuilt derivatives.

## Studio Owner original-LOD0 override (2026-08-01)

- The Studio Owner selected each user-re-supplied Raw `.blend` as the
  authoritative LOD0 source for this benchmark task.
- Before every FBX export, the Raw file is copied byte-for-byte to the matching
  `Blender/SM_Benchmark_*.blend` path and both SHA-256 values must match.
- No LOD0 cleanup, rebuild, normalization, decimation, transform application,
  or save is permitted. The original Raw topology warnings are accepted for
  these benchmark LOD0 assets.
- Coffee Machine LOD1 alone is an in-memory simplified derivative; it is not
  saved into the authoritative Coffee source file.
- Required Unity axis/dimension adaptation belongs on the Prefab `Visual`
  child/import metadata only. Prefab roots remain identity.

## Work Table

- Asset name: `SM_Benchmark_WorkTable_01`
- Generation date: Not provided
- Supplied file timestamp: 2026-07-31
- Tool: Tripo (indicated by the supplied mesh and material identifiers)
- Prompt or user-owned reference description: no prompt/reference was supplied
- Raw original Blender input: `Raw/WorkTable/SM_Benchmark_WorkTable_01_user_resupplied_original.blend`
- Raw SHA-256: `CDA670B6DEAF309225E1636AA3B07EEBECC6D7D8497939027BC05659C156F60A`
- User-confirmed license/use-right status: pending explicit Studio Owner confirmation
- Third-party logo or protected-character check: none observed in the Studio Owner-supplied input
- Authoritative byte-identical Blender source: `Blender/SM_Benchmark_WorkTable_01.blend`
- Production FBX file: `Assets/Art/VisualPipeline/Benchmarks/Models/SM_Benchmark_WorkTable_01.fbx`

## Coffee Machine

- Asset name: `SM_Benchmark_CoffeeMachine_01`
- Generation date: Not provided
- Supplied file timestamp: 2026-07-31
- Tool: Tripo (indicated by the supplied mesh and material identifiers)
- Prompt or user-owned reference description: no prompt/reference was supplied
- Raw original Blender input: `Raw/CoffeeMachine/SM_Benchmark_CoffeeMachine_01_user_resupplied_original.blend`
- Raw SHA-256: `1798013B314421693587470C25D9C5FFBD397F5995695965C5C86089ED6094B5`
- User-confirmed license/use-right status: pending explicit Studio Owner confirmation
- Third-party logo or protected-character check: none observed in the Studio Owner-supplied input
- Authoritative byte-identical Blender source: `Blender/SM_Benchmark_CoffeeMachine_01.blend`
- Production FBX file: `Assets/Art/VisualPipeline/Benchmarks/Models/SM_Benchmark_CoffeeMachine_01.fbx`

## Ceramic Cup

- Asset name: `SM_Benchmark_CeramicCup_01`
- Generation date: Not provided
- Supplied file timestamp: 2026-07-31
- Tool: Tripo (indicated by the supplied mesh and material identifiers)
- Prompt or user-owned reference description: no prompt/reference was supplied
- Raw original Blender input: `Raw/CeramicCup/SM_Benchmark_CeramicCup_01_user_resupplied_original.blend`
- Raw SHA-256: `D949A038F600B4DCFE2E81468C4A4B47266C53429316859DBCDC33B45A061A37`
- User-confirmed license/use-right status: pending explicit Studio Owner confirmation
- Third-party logo or protected-character check: none observed in the Studio Owner-supplied input
- Authoritative byte-identical Blender source: `Blender/SM_Benchmark_CeramicCup_01.blend`
- Production FBX file: `Assets/Art/VisualPipeline/Benchmarks/Models/SM_Benchmark_CeramicCup_01.fbx`
