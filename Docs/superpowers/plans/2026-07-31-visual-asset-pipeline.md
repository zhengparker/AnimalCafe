# AnimalCafe Visual Asset Pipeline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Status:** Studio Owner approved（包含 `1.30 m` Character Scale Reference、Work Table `0.90 × 0.90 × 0.65 m`、Coffee Machine `0.65 × 0.50 × 0.62 m`）。

**Goal:** 建立可重复的 Tripo → Blender → FBX → Unity → Prefab pipeline，并用三个 benchmark assets 证明 scale、pivot、forward、naming、Material、Collider、LOD、mobile budget 与 Camera readability contract 可执行。

**Architecture:** validation code 只存在于 `AnimalCafe.Editor` assembly，通过明确的 issue codes 检查真实 imported FBX 与 Prefab；EditMode tests 先用故意错误的真实 Unity fixtures 观察 RED，再实现最小 validator。独立 readability Scene 和 PlayMode tests 只消费通过 validator 的 Prefabs，不修改 `MainCafe.unity`，也不改变 Phase 1/2 Layout contracts。

**Tech Stack:** Unity `6000.5.5f1`、C#、URP `17.5.0`、Unity Test Framework `1.7.0`、NUnit、Tripo、Blender、FBX。

## Global Constraints

- Written spec：`Docs/superpowers/specs/2026-07-31-phase-3-visual-asset-pipeline-design.md`。
- `1 Grid cell = 1 Unity world unit = 1 meter`；不得修改 `GridSettings.CellSize` contract。
- 视觉方向使用 `A2 + P1`：圆润但有功能细节；奶油色、暖木色、鼠尾草绿与少量蜂蜜黄。
- Work Table target：`0.90 × 0.90 × 0.65 m`，约 `±5%`。
- Coffee Machine target：`0.65 × 0.50 × 0.62 m`，约 `±10%`。
- Ceramic Cup target：`0.14 × 0.14 × 0.16 m`，约 `±10%`。
- Furniture-to-character visual reference：标准角色高度 `1.30 m`；P3 只建立 reference，不自动修改现有柴犬 Mesh、Rig 或 Animation。
- Unity final contract：root position/rotation 为 zero，root scale 为 one，底面中心 pivot，最低点 `Y = 0`，forward 为 `+Z`。
- Blender source：`Z Up`、front `-Y`；FBX：`Forward -Z`、`Up Y`。axis/export errors follow the applicable source contract; protected original LOD0 follows the Studio Owner contract below.
- Tripo Raw export 不能直接成为 production Prefab。一般 pipeline 的 editable source 必须由单独批准的 source contract 定义；本 Phase 3 的三个 benchmark 必须服从下面的 Studio Owner original-LOD0 contract。
- Studio Owner original-LOD0 contract (2026-08-01)：the three user-re-supplied original Blender inputs are the authoritative byte-identical LOD0 sources. Copy Raw bytes to `Blender/`, verify matching SHA-256 before export, and preserve that byte equality after export. Original topology and normals are accepted benchmark facts. Coffee LOD1 alone may be an independently edited simplified derivative. Required axis/dimension adaptation belongs only on the Prefab Visual child/import metadata; root stays identity. A protected LOD0 shape/topology/pivot/forward issue stops for Studio Owner direction; Blender editing is limited to Coffee LOD1 or a future separately approved editable source.
- Owner-approved validator/test boundary：Work Table、Coffee Machine LOD0 和 Ceramic Cup 均为 `6,000` triangles pass、`6,001` triangles fail；Coffee LOD1 仍须 `<= 2,500` 且 `<= 60%` of LOD0。这个 budget change 不改变 original LOD0 的 byte-identical contract。
- Studio Owner original-color override (2026-08-01)：read-only audit confirmed each authoritative Blender Material drives Principled Base Color from one packed sRGB image. Repeatably extract and downscale those images to project-relative `512 × 512` production Textures; use one original-color Material per furniture asset. Coffee LOD0/LOD1 share the same Material and Texture. This overrides the earlier pure-color P1 treatment for these three benchmarks only.
- Shader 只使用 Opaque URP `Lit`；Texture 最大 `512 × 512`；禁止 custom Shader、透明 benchmark Material 与 `MeshCollider`。
- Triangle budget：Table ≤ `6,000`；Machine LOD0 ≤ `6,000`、LOD1 ≤ `2,500` 且 ≤ LOD0 的 `60%`；Cup ≤ `6,000`。
- Material slots：Table ≤ `2`；Machine ≤ `3`；Cup ≤ `1`。
- Collider count：Table ≤ `3`；Machine ≤ `2`；Cup ≤ `1`；只允许 primitive Collider，且 `isTrigger = false`。
- Camera validation：SolidColor background `#F2E6B8`；scene-only `UniversalAdditionalCameraData` with `SMAA High` and Camera `Post Processing` enabled；current P3 proxy starts at orthographic size `4`，fixed `1920 × 1080` at `1x`/`Fit` for clarity/Material review，size `7`、`12` as distance proxy samples；`6x` pixel magnification is not a pass criterion；mobile portrait reference `1170 × 2532`。这些 proxy 不锁定正式 base framing 或 zoom presets；不得修改 global URP/Quality settings。
- Batch baseline：同时显示 Table、Machine、Cup 各 `20` 个，共 `60` 个实例。
- 不制作大量正式 Models，不加入 gameplay、Decoration Mode、Interaction Anchors、Save、UI 或 pathfinding。
- 不修改 `MainCafe.unity`。
- Whitespace gate 必须区分 authored source/text 与 Unity-generated YAML：前者使用 scoped `git diff --check` 并必须 clean；后者单独运行、记录和人工检查，不为了消除 Unity serializer 产生的 warning 而机械格式化。
- Codex 默认不 commit、push、merge 或删除 branch/worktree；只有 Studio Owner 对当前 rollout 明确授权时，才执行授权范围内的 local commit。Push/merge 仍需单独授权。

---

## File Structure

### Source and production assets

```text
ArtSource/VisualPipeline/Benchmarks/
├─ Raw/
│  ├─ WorkTable/
│  ├─ CoffeeMachine/
│  └─ CeramicCup/
├─ Blender/
│  ├─ SM_Benchmark_WorkTable_01.blend
│  ├─ SM_Benchmark_CoffeeMachine_01.blend
│  └─ SM_Benchmark_CeramicCup_01.blend
├─ Tools/
│  └─ ExportBenchmarkTextures.py
└─ AssetProvenance.md

Assets/Art/VisualPipeline/Benchmarks/
├─ Models/
├─ Materials/
│  ├─ M_Benchmark_WorkTableOriginal_01.mat
│  ├─ M_Benchmark_CoffeeMachineOriginal_01.mat
│  ├─ M_Benchmark_CeramicCupOriginal_01.mat
│  └─ M_Benchmark_CharacterReferenceAccent_01.mat
├─ Prefabs/
└─ Textures/
   ├─ T_Benchmark_WorkTable_BaseColor_01.png
   ├─ T_Benchmark_CoffeeMachine_BaseColor_01.png
   └─ T_Benchmark_CeramicCup_BaseColor_01.png
```

### Validator and tests

```text
Assets/Editor/AssetPipeline/
├─ BenchmarkAssetKind.cs
├─ BenchmarkAssetIssueCode.cs
├─ BenchmarkAssetValidationIssue.cs
├─ BenchmarkAssetValidationReport.cs
├─ BenchmarkAssetRules.cs
├─ BenchmarkAssetValidator.cs
├─ BenchmarkAssetValidationMenu.cs
└─ AssetPipelineReadabilitySceneSetup.cs

Assets/Tests/EditMode/AssetPipeline/
├─ BenchmarkAssetTestFactory.cs
├─ BenchmarkAssetValidatorContractTests.cs
├─ BenchmarkAssetRenderingBudgetTests.cs
└─ AssetPipelineReadabilitySceneSetupTests.cs

Assets/Tests/PlayMode/AssetReadability/
└─ AssetPipelineReadabilityTests.cs

Assets/Scenes/Validation/
└─ AssetPipelineReadability.unity

Docs/
└─ VisualAssetPipeline_Beginner_Guide.md
```

`ArtSource/` 不在 Unity `Assets/` 内，防止 raw files 和 `.blend` 被 Unity 自动导入。`.meta` files 由 Unity 创建，不手工伪造。

---

### Task 1: Validation Vocabulary 与 Real Fixture Harness

**Files:**
- Create: `Assets/Editor/AssetPipeline/BenchmarkAssetKind.cs`
- Create: `Assets/Editor/AssetPipeline/BenchmarkAssetIssueCode.cs`
- Create: `Assets/Editor/AssetPipeline/BenchmarkAssetValidationIssue.cs`
- Create: `Assets/Editor/AssetPipeline/BenchmarkAssetValidationReport.cs`
- Create: `Assets/Editor/AssetPipeline/BenchmarkAssetRules.cs`
- Create: `Assets/Tests/EditMode/AssetPipeline/BenchmarkAssetTestFactory.cs`
- Create: `Assets/Tests/EditMode/AssetPipeline/BenchmarkAssetValidatorContractTests.cs`

**Interfaces:**
- Produces: `BenchmarkAssetKind`、`BenchmarkAssetIssueCode`、`BenchmarkAssetValidationIssue`、`BenchmarkAssetValidationReport`、`BenchmarkAssetRules.For(BenchmarkAssetKind)`。
- Test factory produces real temporary Prefab assets under `Assets/Tests/Generated/AssetPipeline/` and deletes them in test teardown。

- [ ] **Step 1: Capture fresh baseline before any implementation**

Run full EditMode and PlayMode suites from Unity Test Runner. Record exact passed/failed/skipped/inconclusive counts in the implementation handoff.

Expected: existing merged-main baseline remains green. If the baseline is not green, stop and diagnose before adding P3 code.

- [ ] **Step 2: Create the failing contract test first**

Write `BenchmarkAssetValidatorContractTests.cs` with this first behavior test before creating production types:

```csharp
using NUnit.Framework;
using System;
using System.Collections.Generic;
using AnimalCafe.EditorTools.AssetPipeline;

namespace AnimalCafe.Tests.EditMode.AssetPipeline
{
    public sealed class BenchmarkAssetValidatorContractTests
    {
        [Test]
        public void ValidationReport_WithNoIssuesIsValidAndDoesNotExposeMutableState()
        {
            var source = new List<BenchmarkAssetValidationIssue>();
            var report = new BenchmarkAssetValidationReport(source);

            source.Add(new BenchmarkAssetValidationIssue(
                BenchmarkAssetIssueCode.InvalidName,
                "Assets/Invalid.prefab",
                "Invalid name."));

            Assert.That(report.IsValid, Is.True);
            Assert.That(report.Issues, Is.Empty);
            Assert.That(
                report.Issues,
                Is.Not.InstanceOf<IList<BenchmarkAssetValidationIssue>>());
        }
    }
}
```

This test catches two real breaks: the report retaining a caller-owned mutable list, and callers being able to mutate validator results. Approved size and budget values are not tested as constants; Tasks 2–4 exercise them through real pass/fail asset behavior.

- [ ] **Step 3: Run focused EditMode and confirm correct RED**

Run in Unity Test Runner:

```text
EditMode
→ AnimalCafe.Tests.EditMode.AssetPipeline.BenchmarkAssetValidatorContractTests
```

Expected: compile failure because the `AssetPipeline` validation types do not exist. A typo or unrelated assembly error is not an acceptable RED.

- [ ] **Step 4: Add the minimal validation vocabulary**

Create:

```csharp
namespace AnimalCafe.EditorTools.AssetPipeline
{
    public enum BenchmarkAssetKind
    {
        WorkTable,
        CoffeeMachine,
        CeramicCup
    }
}
```

Create issue codes used throughout later tasks:

```csharp
namespace AnimalCafe.EditorTools.AssetPipeline
{
    public enum BenchmarkAssetIssueCode
    {
        InvalidAssetPath,
        InvalidName,
        RootTransformNotIdentity,
        BoundsOutsideTolerance,
        BelowGround,
        InvalidForwardMarker,
        MissingMesh,
        TriangleBudgetExceeded,
        MaterialSlotBudgetExceeded,
        MissingMaterial,
        InvalidShader,
        TransparentMaterial,
        TextureBudgetExceeded,
        InvalidColliderType,
        ColliderBudgetExceeded,
        TriggerColliderNotAllowed,
        ColliderOutsideModelBounds,
        MissingLodGroup,
        MissingLod1,
        LodTriangleBudgetExceeded,
        LodReductionInsufficient,
        MissingReference
    }
}
```

`BenchmarkAssetValidationIssue` contains immutable `Code`, `AssetPath`, and `Message`. `BenchmarkAssetValidationReport` exposes a read-only `Issues` collection and `IsValid => Issues.Count == 0`; callers cannot mutate the internal list.

- [ ] **Step 5: Implement the exact approved rule table**

Use `Vector3(width, height, depth)`:

```csharp
public static BenchmarkAssetRules For(BenchmarkAssetKind kind)
{
    switch (kind)
    {
        case BenchmarkAssetKind.WorkTable:
            return new BenchmarkAssetRules(
                new Vector3(0.90f, 0.65f, 0.90f),
                0.05f, 6000, 0, 0f, 2, 3, false);
        case BenchmarkAssetKind.CoffeeMachine:
            return new BenchmarkAssetRules(
                new Vector3(0.65f, 0.62f, 0.50f),
                0.10f, 6000, 2500, 0.60f, 3, 2, true);
        case BenchmarkAssetKind.CeramicCup:
            return new BenchmarkAssetRules(
                new Vector3(0.14f, 0.16f, 0.14f),
                0.10f, 6000, 0, 0f, 1, 1, false);
        default:
            throw new System.ArgumentOutOfRangeException(nameof(kind), kind, null);
    }
}
```

- [ ] **Step 6: Run focused GREEN**

Expected: `ValidationReport_WithNoIssuesIsValidAndDoesNotExposeMutableState` passes.

- [ ] **Step 7: Add the real fixture factory**

`BenchmarkAssetTestFactory` must:

- create real `GameObject`, `Mesh`, `MeshRenderer`, `MeshFilter`, Material, Collider and Prefab assets through Unity Editor APIs;
- accept literal bounds and triangle counts supplied by each test;
- create a child named `ForwardMarker` at local `(0, 0.05, 0.25)` with identity rotation;
- save only under `Assets/Tests/Generated/AssetPipeline/`;
- delete that exact generated folder in teardown through `AssetDatabase.DeleteAsset`;
- never add cleanup methods to production classes.

- [ ] **Step 8: Task 1 checkpoint**

Run focused tests, the scoped authored-source whitespace check from Task 7 Step 4, and `git status --short`. Confirm no runtime assembly or `MainCafe.unity` change. Do not commit.

---

### Task 2: Naming、Transform、Bounds、Pivot 与 Forward Validator

**Files:**
- Create: `Assets/Editor/AssetPipeline/BenchmarkAssetValidator.cs`
- Modify: `Assets/Tests/EditMode/AssetPipeline/BenchmarkAssetValidatorContractTests.cs`
- Modify: `Assets/Tests/EditMode/AssetPipeline/BenchmarkAssetTestFactory.cs`

**Interfaces:**
- Consumes: Task 1 rule table and reports。
- Produces: `BenchmarkAssetValidator.ValidatePrefab(string assetPath, BenchmarkAssetKind kind)`。

- [ ] **Step 1: Write failing behavior tests**

Add named tests using real saved Prefabs:

```text
ValidatePrefab_ApprovedWorkTableReturnsNoStructuralIssues
ValidatePrefab_PathOutsideBenchmarkFolderReportsInvalidAssetPath
ValidatePrefab_NameWithSpacesReportsInvalidName
ValidatePrefab_WrongPrefixReportsInvalidName
ValidatePrefab_NonAsciiNameReportsInvalidName
ValidatePrefab_RootScaleUsedForCorrectionReportsRootTransformNotIdentity
ValidatePrefab_RootRotationUsedForCorrectionReportsRootTransformNotIdentity
ValidatePrefab_BoundsInsideTolerancePass
ValidatePrefab_WidthAboveToleranceReportsBoundsOutsideTolerance
ValidatePrefab_HeightBelowToleranceReportsBoundsOutsideTolerance
ValidatePrefab_VisibleBoundsBelowZeroReportsBelowGround
ValidatePrefab_MissingForwardMarkerReportsInvalidForwardMarker
ValidatePrefab_ForwardMarkerBehindOriginReportsInvalidForwardMarker
ValidatePrefab_ForwardMarkerRotatedAwayFromPositiveZReportsInvalidForwardMarker
ValidatePrefab_MultipleBreaksReportsEveryIssue
```

Use literal expectations, for example:

```csharp
Assert.That(
    report.Issues.Select(issue => issue.Code),
    Does.Contain(BenchmarkAssetIssueCode.RootTransformNotIdentity));
```

- [ ] **Step 2: Run focused RED**

Expected: compile failure because `BenchmarkAssetValidator` does not exist.

- [ ] **Step 3: Implement path and naming validation**

Accepted Prefab path pattern:

```text
Assets/Art/VisualPipeline/Benchmarks/Prefabs/PF_Benchmark_<Kind>_01.prefab
```

Map kinds to exact filenames:

```text
WorkTable     → PF_Benchmark_WorkTable_01.prefab
CoffeeMachine → PF_Benchmark_CoffeeMachine_01.prefab
CeramicCup    → PF_Benchmark_CeramicCup_01.prefab
```

Reject spaces, non-ASCII characters, wrong prefixes, wrong suffixes and paths outside the approved Prefabs folder.

- [ ] **Step 4: Implement real Prefab loading and transform validation**

Use `PrefabUtility.LoadPrefabContents(assetPath)` inside `try/finally`, and always call `PrefabUtility.UnloadPrefabContents(root)`.

Identity tolerance:

```csharp
const float TransformTolerance = 0.0001f;
```

Validate root local position against `Vector3.zero`, local rotation against `Quaternion.identity`, and local scale against `Vector3.one`.

- [ ] **Step 5: Implement visible bounds and floor alignment**

Calculate a root-local combined bounds from every enabled child `Renderer`. Do not use Collider bounds as visual dimensions.

Per axis acceptance:

```text
minimum = target × (1 - tolerance)
maximum = target × (1 + tolerance)
```

Floor tolerance:

```csharp
const float FloorToleranceMeters = 0.005f;
```

Report `BelowGround` if visible minimum Y is less than `-0.005f`; report `BoundsOutsideTolerance` if the minimum Y is above `+0.005f` or any size axis is outside its approved range.

- [ ] **Step 6: Implement the forward marker check**

Require one child named `ForwardMarker`:

- its root-local Z position must be greater than `0.01f`;
- its root-local forward must match `Vector3.forward` within `1°`;
- it must have no Renderer, MeshFilter or Collider;
- automated validation proves the declared forward contract;
- manual Camera review later proves the visible front actually agrees with the marker.

- [ ] **Step 7: Run focused GREEN and refactor issue collection**

All Task 2 tests pass. Refactor only duplicate issue-addition code; rerun focused tests after refactor.

- [ ] **Step 8: Task 2 checkpoint**

Run focused EditMode tests, the scoped authored-source whitespace check from Task 7 Step 4, and confirm all intentionally broken fixtures report every expected issue without throwing.

---

### Task 3: Mesh、Material、Texture 与 LOD Budget Validator

**Files:**
- Modify: `Assets/Editor/AssetPipeline/BenchmarkAssetValidator.cs`
- Create: `Assets/Tests/EditMode/AssetPipeline/BenchmarkAssetRenderingBudgetTests.cs`
- Modify: `Assets/Tests/EditMode/AssetPipeline/BenchmarkAssetTestFactory.cs`

**Interfaces:**
- Extends `ValidatePrefab(...)` with real Mesh、Material、Texture、Shader and LOD checks。

- [ ] **Step 1: Write failing rendering-budget tests**

```text
Rendering_ApprovedOpaqueUrpLitSharedMaterialPasses
Rendering_MissingMeshReportsMissingMesh
Rendering_WorkTableAt6000TrianglesPasses
Rendering_CeramicCupAt6000TrianglesPasses
Lod_MachineAt6000Lod0TrianglesPasses
Rendering_TableAbove6000TrianglesReportsTriangleBudgetExceeded
Rendering_MachineAbove6000TrianglesReportsTriangleBudgetExceeded
Rendering_CupAbove6000TrianglesReportsTriangleBudgetExceeded
Rendering_MissingMaterialReportsMissingMaterial
Rendering_TooManyMaterialSlotsReportsMaterialSlotBudgetExceeded
Rendering_NonUrpLitShaderReportsInvalidShader
Rendering_TransparentSurfaceReportsTransparentMaterial
Rendering_Texture512Passes
Rendering_Texture1024ReportsTextureBudgetExceeded
Lod_MachineWithTwoValidLevelsPasses
Lod_MachineWithoutLodGroupReportsMissingLodGroup
Lod_MachineWithoutSecondLevelReportsMissingLod1
Lod_MachineLod1Above2500ReportsLodTriangleBudgetExceeded
Lod_MachineLod1AboveSixtyPercentReportsLodReductionInsufficient
Lod_TableAndCupDoNotRequireLodGroup
```

- [ ] **Step 2: Run focused RED**

Expected: tests fail because the validator does not report rendering-budget issues.

- [ ] **Step 3: Implement triangle and renderer checks**

Count real triangles from each unique Mesh used by the level being validated:

```csharp
triangles += mesh.triangles.Length / 3;
```

Do not count the same Mesh repeatedly merely because multiple Renderers reference it inside one Prefab level. Missing Mesh references report `MissingMesh` and validation continues.

- [ ] **Step 4: Implement Material slot and Shader checks**

Count non-null `sharedMaterials` slots used by enabled Renderers. Do not access `.material`, which would instantiate copies.

Require:

```text
material.shader.name == "Universal Render Pipeline/Lit"
_Surface == 0  // Opaque
```

Any null slot reports `MissingMaterial`. Count unique shared Material assets separately in the final report so duplicated embedded materials are visible during real-asset review.

- [ ] **Step 5: Implement Texture budget checks**

Inspect textures referenced by the Material through `ShaderUtil.GetPropertyCount` and texture properties. For every non-null project Texture, require both width and height ≤ `512`.

Tests must use real generated `Texture2D` assets at literal `512 × 512` and `1024 × 1024`; do not mock TextureImporter.

- [ ] **Step 6: Implement Coffee Machine LOD checks**

Require one `LODGroup` with at least two non-empty levels. Determine triangles from the renderers assigned to LOD0 and LOD1.

Acceptance:

```text
LOD0 triangles ≤ 6000
LOD1 triangles ≤ 2500
LOD1 triangles / LOD0 triangles ≤ 0.60
```

Reject null renderers and renderers reused in both levels when reuse prevents a meaningful reduction.

- [ ] **Step 7: Run focused GREEN and mutation check**

Mentally mutate each maximum, Shader name, `_Surface`, and LOD ratio branch. Confirm a named test fails for each realistic wrong change.

- [ ] **Step 8: Task 3 checkpoint**

Run all `AssetPipeline` EditMode tests and the scoped authored-source whitespace check from Task 7 Step 4. Do not commit.

---

### Task 4: Collider、Missing References 与 Batch Validation Menu

**Files:**
- Modify: `Assets/Editor/AssetPipeline/BenchmarkAssetValidator.cs`
- Create: `Assets/Editor/AssetPipeline/BenchmarkAssetValidationMenu.cs`
- Modify: `Assets/Tests/EditMode/AssetPipeline/BenchmarkAssetValidatorContractTests.cs`

**Interfaces:**
- Produces: `BenchmarkAssetValidator.ValidateAllBenchmarks()` and menu item `AnimalCafe/Validation/Validate Benchmark Assets`。

- [ ] **Step 1: Write failing Collider and batch tests**

```text
Collider_ApprovedPrimitiveCollidersPass
Collider_MeshColliderReportsInvalidColliderType
Collider_TooManyCollidersReportsColliderBudgetExceeded
Collider_TriggerReportsTriggerColliderNotAllowed
Collider_BoundsFarOutsideVisibleModelReportsColliderOutsideModelBounds
References_MissingRendererMaterialReportsMissingReference
BatchValidation_ReturnsIssuesForAllThreeAssetsWithoutStoppingEarly
BatchValidation_MissingExpectedPrefabReportsMissingReference
```

- [ ] **Step 2: Run focused RED**

Expected: tests fail because Collider and batch rules are not implemented.

- [ ] **Step 3: Implement simple Collider validation**

Allowed concrete types:

```csharp
typeof(BoxCollider)
typeof(SphereCollider)
typeof(CapsuleCollider)
```

Reject every other Collider type, including `MeshCollider`. Require `isTrigger == false`.

Use the kind-specific count maximum. A Collider may approximate the Model; report `ColliderOutsideModelBounds` only when its world bounds extend more than `0.05 m` beyond the combined visible bounds on any axis or below `Y = -0.005 m`.

- [ ] **Step 4: Implement batch validation**

Validate these exact paths in deterministic order:

```text
PF_Benchmark_WorkTable_01.prefab
PF_Benchmark_CoffeeMachine_01.prefab
PF_Benchmark_CeramicCup_01.prefab
```

Return one combined read-only report; never stop after the first invalid asset.

- [ ] **Step 5: Add the manual menu entry**

Menu behavior:

- run `ValidateAllBenchmarks()`;
- log one concise line per issue with asset path and issue code;
- log a green summary only when zero issues exist;
- select the first invalid asset in Project view when possible;
- never modify import settings or Prefabs automatically.

- [ ] **Step 6: Run focused GREEN**

Confirm approved fixtures pass, broken fixtures return all issue codes, and no fixture cleanup leaks into `Assets/Tests/Generated/`.

- [ ] **Step 7: Full EditMode regression checkpoint**

Run all EditMode tests. Expected: every previous P0/P1/P2 test plus new AssetPipeline tests passes; failed/skipped/inconclusive are all `0`.

Do not commit.

---

### Task 5: Produce and Validate the Three Benchmark Assets

**Files:**
- Create: `ArtSource/VisualPipeline/Benchmarks/Raw/WorkTable/*`
- Create: `ArtSource/VisualPipeline/Benchmarks/Raw/CoffeeMachine/*`
- Create: `ArtSource/VisualPipeline/Benchmarks/Raw/CeramicCup/*`
- Create: `ArtSource/VisualPipeline/Benchmarks/Blender/*.blend`
- Create: `ArtSource/VisualPipeline/Benchmarks/AssetProvenance.md`
- Create: `Assets/Art/VisualPipeline/Benchmarks/Models/*.fbx`
- Create: `Assets/Art/VisualPipeline/Benchmarks/Materials/*.mat`
- Create repeatably from packed source images: `Assets/Art/VisualPipeline/Benchmarks/Textures/*`
- Create: `ArtSource/VisualPipeline/Benchmarks/Tools/ExportBenchmarkTextures.py`
- Create: `Assets/Art/VisualPipeline/Benchmarks/Prefabs/*.prefab`

**Interfaces:**
- Consumes: approved validator and all Global Constraints。
- Produces: the three validated benchmark Prefabs used by Task 6。

- [ ] **Step 1: Record provenance before production use**

For each asset, `AssetProvenance.md` records:

```text
Asset name
Generation date
Tool: Tripo
Prompt or user-owned reference description
Raw exported filename and format
User-confirmed license/use-right status
Third-party logo or protected-character check: none
Authoritative byte-identical Blender source
Production FBX file
```

Do not put secrets, account details or private Tripo URLs in the file.

- [ ] **Step 2: Preserve raw export outside Unity Assets**

Save each Tripo raw export only in its matching `ArtSource/.../Raw/<Asset>/` folder. Do not drag raw files or `.blend` files into `Assets/`.

- [ ] **Step 3: Preserve each Studio Owner-approved LOD0 source in Blender**

For Work Table, Coffee Machine and Ceramic Cup independently:

1. byte-copy each user-re-supplied Raw input to the authoritative Blender path;
2. verify Raw and authoritative SHA-256 equality before export;
3. preserve byte equality between the Raw input and authoritative LOD0 source after export;
4. retain original dimensions, pivot, orientation, materials, normals and topology as accepted benchmark facts;
5. adapt only the Unity Prefab Visual child/import metadata if approved bounds require it;
6. keep the Prefab root at identity;
7. create Coffee LOD1 as a separate simplified derivative while preserving the LOD0 byte-identical contract.

- [ ] **Step 4: Create Coffee Machine LOD1 in Blender**

LOD1 must be a deliberately simplified mesh, not a duplicate of LOD0. Preserve body, front panel and large silhouette; remove buttons, seams and small decorations first.

Before export, record Blender triangle counts and confirm LOD1 ≤ `2,500` and ≤ `60%` of LOD0.

- [ ] **Step 5: Export production FBX**

Use:

```text
Selected Objects: On
Object Types: Mesh only
Export coordinate conversion: FBX only; authoritative source remains byte-identical
Forward: -Z Forward
Up: Y Up
Add Leaf Bones: Off
Embed Textures: Off
```

Export only production meshes to the approved `Models/` filenames.

- [ ] **Step 6: Configure Unity import without Transform compensation**

For each FBX:

- Scale Factor `1`;
- do not repair size using Prefab root scale;
- import Materials without generating duplicated embedded `.mat` files;
- normals use the accepted original source result;
- Read/Write remains disabled unless a named later feature proves it is required;
- inspect imported bounds and triangle counts in Unity.

- [ ] **Step 7: Extract original Base Color Textures and create URP Lit Materials**

Create only:

```text
M_Benchmark_WorkTableOriginal_01.mat
M_Benchmark_CoffeeMachineOriginal_01.mat
M_Benchmark_CeramicCupOriginal_01.mat
M_Benchmark_CharacterReferenceAccent_01.mat
```

First run a read-only Blender audit. It must prove each authoritative source has one Material slot and one packed sRGB Base Color image linked to Principled BSDF. Use `ExportBenchmarkTextures.py` to extract each image without saving the `.blend`, downscale to `512 × 512`, and verify source hashes are unchanged before/after.

Furniture Materials use Opaque URP Lit、white Base Color tint、`Metallic = 0`、`Smoothness = 0.5` and their exact project-relative Texture. Character Scale Reference uses the dedicated teal `#157A78` accent Material with no Texture. Do not create per-Prefab copies.

- [ ] **Step 8: Assemble Prefabs**

Each Prefab:

- root uses identity Transform;
- child Mesh renderer uses production FBX;
- `ForwardMarker` exists at positive local Z and matches the visible front;
- primitive Colliders approximately enclose the object;
- Coffee Machine has valid `LODGroup` with LOD0 and LOD1;
- Coffee Machine LOD0 and LOD1 reference the same Coffee original-color Material and Texture;
- no gameplay scripts or Interaction Anchors are added.

- [ ] **Step 9: Run validator and treat every failure as RED evidence**

Use `AnimalCafe/Validation/Validate Benchmark Assets`.

For each issue, fix the correct layer:

```text
protected LOD0 shape/topology/pivot/forward → stop and request Studio Owner direction
required axis/dimension adaptation           → Unity Prefab Visual child/import metadata only
Coffee LOD1 or separately approved source    → Blender editing only within that source contract
Shader/Material sharing          → Unity Material
Collider/LOD assembly            → Unity Prefab
naming/path                      → file/folder location
```

Do not suppress an issue or loosen a budget merely to make validation green. Any proposed contract change returns to Studio Owner approval.

- [ ] **Step 10: Confirm focused GREEN**

Run all AssetPipeline EditMode tests and the real batch validator. Expected: zero issues for all three production Prefabs.

- [ ] **Step 11: Task 5 checkpoint**

Record actual dimensions, triangles, Materials, Texture sizes, Collider counts and LOD counts for each asset. Run the scoped authored-source whitespace check from Task 7 Step 4, record the separate Unity-YAML result, and inspect `git status --short`. Do not commit.

---

### Task 6: Camera Readability Scene、Batch Baseline 与 PlayMode Smoke Tests

**Files:**
- Create: `Assets/Editor/AssetPipeline/AssetPipelineReadabilitySceneSetup.cs`
- Create: `Assets/Tests/EditMode/AssetPipeline/AssetPipelineReadabilitySceneSetupTests.cs`
- Create: `Assets/Tests/PlayMode/AssetReadability/AssetPipelineReadabilityTests.cs`
- Create through the approved setup tool: `Assets/Scenes/Validation/AssetPipelineReadability.unity`

**Interfaces:**
- Consumes: the three validated benchmark Prefabs。
- Produces: idempotent validation Scene and runtime smoke evidence without modifying `MainCafe.unity`。

- [ ] **Step 1: Write failing setup tests**

```text
Setup_CreatesDedicatedValidationScene
Setup_CreatesOneOrthographicIsometricCamera
Setup_CreatesOneSingleAssetDisplayRoot
Setup_SingleDisplayUsesTwoSeparateCenteredTabletopStations
Setup_CharacterReferenceDoesNotOverlapRightStationInCameraView
Setup_AllSingleDisplayRenderersFitInsideSizeFourCameraViewport
Setup_CreatesOneCharacterScaleReferenceAtOnePointThreeMeters
Setup_CreatesTwentyInstancesOfEachBenchmarkInBatchRoot
Setup_RepeatedRunDoesNotDuplicateObjects
Setup_DoesNotModifyMainCafeScene
```

The fixture snapshots the `MainCafe.unity` dependency hash before and after setup and expects it to remain identical.

- [ ] **Step 2: Run setup tests and confirm RED**

Expected: compile failure because `AssetPipelineReadabilitySceneSetup` does not exist.

- [ ] **Step 3: Implement minimal idempotent setup tool**

Create menu item:

```text
AnimalCafe/Validation/Build Asset Readability Scene
```

The tool creates or replaces only `Assets/Scenes/Validation/AssetPipelineReadability.unity` and builds:

```text
AssetReadabilityRoot
├─ CameraRoot
│  └─ Main Camera
├─ SingleAssetDisplay
│  ├─ PF_Benchmark_WorkTable_01
│  ├─ PF_Benchmark_CoffeeMachine_01
│  ├─ PF_Benchmark_WorkTable_01
│  ├─ PF_Benchmark_CeramicCup_01
│  └─ CharacterScaleReference_1_30m
└─ BatchDisplay
   ├─ WorkTables_20
   ├─ Machines_20
   └─ Cups_20
```

Camera uses the accepted fixed rotation from the project foundation, is orthographic, starts at the current P3 proxy size `4`, clears to SolidColor `#F2E6B8`, and has scene-specific `UniversalAdditionalCameraData` with `SMAA High` plus Camera `Post Processing` enabled so SMAA executes. At fixed `1920 × 1080`, `1x`/`Fit` is the primary proxy clarity/Material view; `6x` only magnifies rendered pixels and is not a pass criterion. Size `4`、`7`、`12` are proxy samples across the anticipated continuous `1.0x`–`3.0x` zoom envelope, not formal gameplay presets and not approval of an exact base framing. P3 validates asset silhouette、Material、Texture and aliasing only; it does not implement the formal zoom input/controller. Do not modify global URP or Quality settings. `SingleAssetDisplay` uses two instances of the same Work Table Prefab: Coffee Machine is centered on the left tabletop and Ceramic Cup is centered on the right tabletop. At size `4`, every relevant Renderer must remain inside the real Camera viewport with `0.01` safe margin for both `1920 × 1080` and `1170 × 2532`. Character Scale Reference uses local `(1.75, 0, 2.00)` and must not overlap either station in either aspect. BatchDisplay uses local `(-30, 0, 30)` and none of its Renderer bounds may enter either viewport. No runtime placement system is introduced.

`CharacterScaleReference_1_30m` is a simple Editor-generated silhouette/reference object with visible bounds exactly `1.30 m` high, bottom at `Y = 0`, and no Collider、Rig、Animation or gameplay script. It uses the dedicated teal `#157A78` URP Lit Material, must contrast clearly against the pale-yellow background, and is not counted among the `60` benchmark batch instances.

- [ ] **Step 4: Run setup GREEN**

Run the setup tests twice. Expected: identical object counts and no `MainCafe.unity` change.

- [ ] **Step 5: Write PlayMode smoke tests first**

```text
ReadabilityScene_LoadsWithoutMissingBenchmarkReferences
ReadabilityScene_ContainsExactlySixtyBatchInstances
ReadabilityScene_CharacterScaleReferenceIsOnePointThreeMetersHigh
ReadabilityScene_CameraIsOrthographicAndUsesSizeFour
ReadabilityScene_UsesTwoSeparateCenteredTabletopStations
ReadabilityScene_CharacterReferenceDoesNotOverlapRightStationInCameraView
ReadabilityScene_AllSingleDisplayRenderersFitInsideSizeFourCameraViewport
ReadabilityScene_AllRenderersUseUrpLitMaterials
ReadabilityScene_CoffeeMachineHasTwoValidLodLevels
ReadabilityScene_ProducesNoUnexpectedErrorLogs
```

- [ ] **Step 6: Run PlayMode RED**

Expected: tests fail until the generated Scene is added to the test fixture and contains the exact validated structure.

- [ ] **Step 7: Make minimal Scene/test integration GREEN**

Load the validation Scene by asset path in the test setup, yield one frame, then assert real objects and renderers. Do not mock Scene loading, Prefabs, Materials or LODGroup.

- [ ] **Step 8: Run full automated regression**

Run fresh full EditMode and PlayMode suites. Record exact counts; failed/skipped/inconclusive must all be `0`.

- [ ] **Step 9: Studio Owner manual Camera review**

The user checks:

1. pale-yellow `#F2E6B8` background is present; the color itself is expected, and only clipping、washout or lost readability is a failure;
2. scene-only antialiasing is `SMAA High`, Camera `Post Processing` is enabled, and the dedicated teal Character Scale Reference clearly contrasts with the background;
3. the left table contains only the centered Coffee Machine, the right table contains only the centered Ceramic Cup, and neither furniture item is hidden by the other;
4. Work Table reads as orange wood/black, Coffee Machine as pale blue/white/black, and Ceramic Cup as muted green, matching Blender originals;
5. orthographic size `4`: main details, Material differences and visible front;
6. size `7`: immediate distinction among Table, Machine and Cup;
7. size `12`: Table and Machine remain recognizable; Cup retains stable silhouette;
8. `1.30 m` Character Scale Reference makes Work Table、Machine and Cup proportions readable together;
9. Coffee Machine visibly fits on Work Table with remaining surface space;
10. Coffee Machine LOD switch has no obvious size/position/Material/Texture jump;
11. Game view `1920 × 1080` is readable;
12. portrait `1170 × 2532` reference framing does not hide the objects;
13. batch display has no pink Material, missing Mesh, abnormal Collider or Console error.

Manual aesthetic/readability acceptance cannot be replaced by automated bounds tests.

- [ ] **Step 10: Task 6 checkpoint**

Record Studio Owner result as `Approved` or list exact revisions. Do not mark Roadmap Completed yet and do not commit.

---

### Task 7: Beginner Guide、Roadmap Evidence 与 Final Gate

**Files:**
- Create: `Docs/VisualAssetPipeline_Beginner_Guide.md`
- Modify only after fresh evidence: `Docs/AnimalCafe_Development_Roadmap.md`
- Verify: `Docs/superpowers/specs/2026-07-31-phase-3-visual-asset-pipeline-design.md`
- Verify: `Docs/superpowers/plans/2026-07-31-visual-asset-pipeline.md`

**Interfaces:**
- Consumes: exact automated counts, validator report, actual asset metrics and Studio Owner manual result。
- Produces: beginner handoff and truthful Roadmap state。

- [ ] **Step 1: Write the Beginner Guide with a concrete opening example**

Use these exact sections:

```text
1. 用三个家具解释这条 pipeline
2. Tripo、Blender、FBX、Unity 和 Prefab 分别做什么
3. Grid 尺寸和 Model 尺寸
4. Pivot、Forward 和 Transform
5. Naming 与 Folder
6. Material、Texture 和 Shader
7. Collider 是透明的简单包围盒
8. Triangle、LOD 与 Mobile Budget
9. Validator 的 RED 与 GREEN
10. Camera Readability Manual Test
11. Phase 3 没有做什么
12. Beginner Glossary
13. 完成证据和下一步
```

开头必须说明：Collider 大致包住物体，方便点击和碰撞判断；Grid Occupancy 判断地面格子，两者不是同一件事。

- [ ] **Step 2: Insert only verified facts**

The guide records actual, not target-only, metrics for each asset:

```text
actual bounds
LOD0/LOD1 triangles
Material slots
Texture paths, sRGB import and `512 × 512` maximum
original-color visual expectations
readability background、reference contrast and scene-only SMAA
Collider types/count
validator result
Camera manual result
```

Do not copy Tripo account data or private URLs.

- [ ] **Step 3: Update Roadmap to In Review**

Only after fresh automated GREEN, record:

```text
Status: In Review
Automated evidence: exact EditMode and PlayMode counts
Asset validation: 3/3 benchmark Prefabs valid
Manual evidence: exact Studio Owner status
Known limitations: benchmark pipeline only; not Phase 4 formal asset set
```

Do not mark `Completed` before Studio Owner acceptance.

- [ ] **Step 4: Final static checks**

Run:

```powershell
git diff --check a934d0f -- '*.cs' '*.py' '*.md' '*.json' '*.asmdef'
git diff --check a934d0f -- '*.meta' '*.mat' '*.prefab' '*.unity'
$placeholderPattern = @('T'+'BD','T'+'ODO','implement'+' later','稍后'+'实现','待补'+'充') -join '|'
rg -n -i $placeholderPattern `
  Docs/VisualAssetPipeline_Beginner_Guide.md `
  Docs/superpowers/specs/2026-07-31-phase-3-visual-asset-pipeline-design.md `
  Docs/superpowers/plans/2026-07-31-visual-asset-pipeline.md
rg -n "MeshCollider|Standard Shader|Shader Graph" `
  Assets/Art/VisualPipeline/Benchmarks `
  Assets/Editor/AssetPipeline
git status --short --branch
```

The first command is the blocking authored-file gate and must be clean. Record
the second command separately as Unity-generated YAML evidence; inspect it, but
do not mechanically normalize serializer-owned files only to silence whitespace
warnings. Interpret intentional mentions in validator issue enums/tests; do not
treat the presence of a negative test string as a production violation.

- [ ] **Step 5: Final fresh regression**

Run all EditMode and PlayMode tests after documentation and Scene changes. Inspect result XML; process exit alone is not sufficient evidence.

- [ ] **Step 6: Department reviews**

- Art Director: visual consistency and Camera readability recommendation；
- Technical Director: import、Prefab、Editor-only architecture and budget recommendation；
- QA Director: RED/GREEN evidence、regression and manual checklist recommendation；
- Executive Producer: scope、documentation and next-gate recommendation。

Any `Needs Revision` result returns to the responsible task; do not silently expand scope.

- [ ] **Step 7: Stop at Studio Owner acceptance gate**

Present:

- changed files and why；
- actual asset metrics；
- validator results；
- exact automated counts；
- manual Camera evidence；
- known limitations；
- explicit reminder that Phase 4 has not started。

Wait for Studio Owner approval. Codex does not commit, push, merge, delete a branch/worktree or start Phase 4.

---

## TDD Summary

```text
Task 1: rule vocabulary RED → minimal rules GREEN
Task 2: structural Prefab failures RED → transform/bounds/forward validator GREEN
Task 3: rendering budgets RED → Mesh/Material/Texture/LOD validator GREEN
Task 4: Collider/batch failures RED → complete validator GREEN
Task 5: real generated assets fail validator → correct source layer → 3/3 GREEN
Task 6: Scene contract RED → idempotent readability fixture + PlayMode GREEN
Task 7: fresh regression + manual acceptance → Roadmap gate
```

Every implementation task stops at its checkpoint. No production code or asset is created before its corresponding failing test or validator failure has been observed and understood.
