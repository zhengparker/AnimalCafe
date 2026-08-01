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

## Original-color packed texture audit and export (2026-08-01)

- Blender `5.2.0 LTS` opened each authoritative source in background mode for a
  read-only audit. The audit did not save any `.blend` file.
- Each source contains exactly one Mesh, one Material, one Material slot and one
  packed sRGB image. An Image Texture node feeds Principled BSDF Base Color;
  the original Material values are Metallic `0` and Roughness `0.5`.
- Stored external image paths were empty or unavailable, so production color is
  recovered only from the readable packed bytes and decoded pixels.
- `Tools/ExportBenchmarkTextures.py` validates those exact facts, verifies the
  authoritative source SHA-256 before and after export, scales each `2048 ×
  2048` image to `512 × 512`, and writes project-relative PNG files without
  saving the source.
- Packed-image and production PNG evidence:

| Asset | Packed image | Packed SHA-256 | Production Texture | PNG SHA-256 |
|---|---|---|---|---|
| Work Table | `wooden+dresser+3d+model_basecolor.jpg` | `543320DD8BF4390F929EEB11AC37B9EE2F9FA11593966096A8B5DCAF4380B20E` | `Assets/Art/VisualPipeline/Benchmarks/Textures/T_Benchmark_WorkTable_BaseColor_01.png` | `CD5A860F5F0FB2555A86154792B56C4BD0463B1D55472074BFF648C23B85AF48` |
| Coffee Machine | `espresso+machine+3d+model_basecolor.jpg` | `F7AD780EAE77EC316F23685B5B4325955AE5783DF57C930478B31AEEBD394270` | `Assets/Art/VisualPipeline/Benchmarks/Textures/T_Benchmark_CoffeeMachine_BaseColor_01.png` | `A2C217E9738A621B382CF92B20D8FBC90583514D399973FC92B2B37E57E0CDF2` |
| Ceramic Cup | `green+mug+3d+model_basecolor.jpg` | `9C0D9D71512734983BA3CF7C90122CF6ACAFE32E8FE51B3E058D3B9B77CAA09D` | `Assets/Art/VisualPipeline/Benchmarks/Textures/T_Benchmark_CeramicCup_BaseColor_01.png` | `3929353AA07E3C6775E6B33F97AED82BEF0A5D1DA134A13BD04FCC9E8196EF22` |

- Visual review of the decoded images identified Work Table orange wood/black
  details, Coffee Machine pale blue/white/black regions, and Ceramic Cup muted
  green. These original colors replace the earlier pure-color benchmark palette.

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
