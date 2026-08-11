# AnimalCafe Phase 4 — Core Architecture & Functional Furniture Models Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** 建立正式 `FurnitureDefinition Asset` authoring、Entrance / Surface / Wall contracts、第一批 production assets 与可重复验证 Scene，让后续 P6–P14 能使用稳定数据而不把家具写死在 Scene 中。

**Architecture:** 现有 `AnimalCafe.Layout` 继续拥有 engine-independent domain data 与 atomic Floor placement；新增 Unity `ScriptableObject` adapter 和 small marker components 负责 Inspector authoring 与 Prefab spatial contract。Floor、Furniture Surface 与 Wall Slot 使用独立数据 owners；Editor validator 与 deterministic builders 生成 production content 和 validation fixture，PlayMode 只做 integration smoke，不提前实现 Decoration UI 或 NPC gameplay。

**Tech Stack:** Unity `6000.5.5f1`、C#、URP `17.5.0`、NUnit / Unity Test Framework、Blender `5.2.0 LTS`、Git worktrees、Windows PowerShell。

## Global Constraints

- Source of Truth：`Docs/AnimalCafe_Project_Design.md`；Phase scope：`Docs/AnimalCafe_Development_Roadmap.md`；approved spec：`Docs/superpowers/specs/2026-08-03-phase-4-core-architecture-functional-furniture-design.md`。
- Branch：`codex/phase-4`；worktree：`E:\Unity\Project\AnimalCafe\.worktrees\phase-4`；remote：`origin/codex/phase-4`。
- Branch 创建前，Studio Owner 先通过 GitHub Desktop commit / push 已批准的 Project Design、Roadmap、P4 spec 与本 plan；执行者验证这些文档已经存在于 `origin/main`。执行者不得替用户完成这次 commit。
- 在 P4 first RED 前必须记录 verified `origin/main` hash、worktree hash、EditMode、PlayMode 与 P3 validator baseline。
- Studio Owner 使用 GitHub Desktop 管理 commits；执行者不得自动 commit、merge、delete branch/worktree。每个 task 只报告建议 commit message。
- 保留 main checkout 中用户拥有的 `.gitignore` 与 `AnimalCafe.slnx` 修改；不得复制、覆盖或 stage。
- 所有 behavior changes 使用 TDD：correct RED → minimal GREEN → refactor → focused + regression GREEN。
- Model metres、Floor Footprint、Collider、Surface Slots 与 Wall Footprint 是独立 contracts；不得自动互推。
- Unity production Prefab root position `(0,0,0)`、rotation identity、scale `(1,1,1)`；bottom-center pivot；Unity forward `+Z`。
- Cash Register source 使用 `Blender Model Item/vintage computer monitor 3d model.glb`；旧 `pos terminal 3d model.glb` 保留但不使用。
- Cash Register target size 约 `0.43 × 0.45 × 0.26 m`、LOD0 ≤ `6000` triangles、Base Color target `512²` 且 maximum `1024²`。
- Floor 为 `8 × 8`；Entrance Clearance 为 `2 × 2`；每面 Wall 为 `8 columns × 2 rows`，Slot `1 m × 1 m`，physical height 约 `3 m`。
- 配色 B：Floor `#F8E9A8`、Back-left Wall `#D2A642`、Back-right Wall `#C7952E`，最终以 URP Camera manual review 为准。
- P4 不实现 P5 UI foundation、P6 Decoration UI、P7 player Wall editing、P8 full readiness、P9–14 Cafe Loop、P17 Save、P29 structure editing、P33 exterior routing 或 P48 formal VFX。

---

## File Structure Locked by This Plan

### Runtime domain — one responsibility per file

- Create `Assets/Scripts/Layout/FurnitureFunctionType.cs` — minimal `None / CoffeeMachine / CashRegister` enum。
- Modify `Assets/Scripts/Layout/FurnitureDefinition.cs` — add compatible Function Type overload/property without breaking the four-argument constructor。
- Create `Assets/Scripts/Layout/CardinalDirection.cs` — four grid-cardinal local directions and 90-degree rotation helper。
- Create `Assets/Scripts/Layout/CashRegisterSides.cs` — immutable opposite-side validation and Queue outward direction。
- Create `Assets/Scripts/Layout/LayoutReservationType.cs` — `EntranceClearance` only。
- Create `Assets/Scripts/Layout/LayoutReservation.cs` — stable reserved rectangular Floor region。
- Modify `Assets/Scripts/Layout/PlacementResult.cs` — add `ReservedEntranceClearance` failure reason。
- Modify `Assets/Scripts/Layout/CafeLayout.cs` — own reservations and reject furniture atomically before overlap。
- Create `Assets/Scripts/Layout/WallSlotPosition.cs` — immutable column / row value。
- Create `Assets/Scripts/Layout/WallFootprint.cs` — positive Width / Height without furniture rotation semantics。
- Create `Assets/Scripts/Layout/WallMountedInstance.cs` — stable wall item state。
- Create `Assets/Scripts/Layout/WallSurfaceLayout.cs` — independent Wall occupancy owner。

### Unity authoring components

- Create `Assets/Scripts/Content/FurnitureDefinitionAsset.cs` — Inspector fields and conversion to runtime Definition。
- Create `Assets/Scripts/Content/FurnitureContentCatalog.cs` — Definition Asset list、duplicate validation、runtime Definition / Prefab lookup。
- Create `Assets/Scripts/Content/SurfaceSlotMarker.cs` — stable local Surface Slot ID and gizmo。
- Create `Assets/Scripts/Content/CashRegisterSideMarker.cs` — typed Employee / Customer marker and gizmo。
- Create `Assets/Scripts/Content/CashRegisterSideType.cs` — Employee / Customer enum。
- Create `Assets/Scripts/Content/WallSurfaceAuthoring.cs` — stable Surface ID、columns、rows、Slot size / origin。
- Create `Assets/Scripts/Content/EntrancePortalAuthoring.cs` — stable Entrance ID and exact `2 × 2` reserved region authoring。
- Create `Assets/Scripts/Content/WallMountedDefinitionAsset.cs` — Window fixed Wall Footprint / Prefab authoring。

### Editor production and validation

- Create `Assets/Editor/Phase4/Phase4AssetIssueCode.cs`。
- Create `Assets/Editor/Phase4/Phase4AssetValidationIssue.cs`。
- Create `Assets/Editor/Phase4/Phase4AssetValidationReport.cs`。
- Create `Assets/Editor/Phase4/Phase4AssetValidator.cs` — definitions、markers、technical asset、environment checks。
- Create `Assets/Editor/Phase4/Phase4ProductionAssetBuilder.cs` — deterministic production Models / Materials / Prefabs / Definition Assets / catalogue。
- Create `Assets/Editor/Phase4/Phase4ValidationSceneSetup.cs` — idempotent P4 validation Scene and fixtures。
- Create `Assets/Editor/Phase4/Phase4ValidationMenu.cs` — explicit build / validate menu items and summary logging。

### Tests

- Create `Assets/Tests/EditMode/Phase4/FurnitureFunctionContractTests.cs`。
- Create `Assets/Tests/EditMode/Phase4/LayoutReservationTests.cs`。
- Create `Assets/Tests/EditMode/Phase4/WallSurfaceLayoutTests.cs`。
- Create `Assets/Tests/EditMode/Phase4/FurnitureDefinitionAssetTests.cs`。
- Create `Assets/Tests/EditMode/Phase4/FurnitureContentCatalogTests.cs`。
- Create `Assets/Tests/EditMode/Phase4/Phase4MarkerContractTests.cs`。
- Create `Assets/Tests/EditMode/Phase4/Phase4AssetValidatorTests.cs`。
- Create `Assets/Tests/EditMode/Phase4/Phase4ProductionAssetTests.cs`。
- Create `Assets/Tests/EditMode/Phase4/Phase4ValidationSceneSetupTests.cs`。
- Create `Assets/Tests/PlayMode/Phase4/Phase4EnvironmentIntegrationTests.cs`。
- Create `Assets/Tests/PlayMode/Phase4/Phase4BuildSettingsIsolationTests.cs`。

### Production content

- Create `ArtSource/Phase4/Blender/SM_Furniture_CounterModule_01.blend`。
- Create `ArtSource/Phase4/Blender/SM_Equipment_CashRegister_01.blend`。
- Create production FBX / Texture / Material / Prefab / Asset content under `Assets/Art/Phase4/` with Unity `.meta` files。
- Create `Assets/Scenes/Validation/Phase4CoreArchitecture.unity` through the deterministic builder, not hand-edit serialized YAML。
- Create `Docs/Phase4_Beginner_Guide.md` after behavior and menu paths are final。

---

### Task 0: Create Isolated P4 Worktree and Prove a Clean Baseline

**Files:**
- No project file edits。
- Evidence output only: Unity Test Framework XML / logs outside tracked Assets。

**Interfaces:**
- Consumes: verified `origin/main` after Phase 3 merge。
- Produces: isolated `codex/phase-4` worktree and recorded baseline counts for every later task。

- [x] **Step 1: Invoke the required worktree skill**

Read and follow `superpowers:using-git-worktrees` before any branch or worktree command. Resolve `git rev-parse --git-dir`、`--git-common-dir`、current branch、status and existing worktrees first。

- [x] **Step 2: Verify main and fetch remote state**

Run from `E:\Unity\Project\AnimalCafe`：

```powershell
git status --short
git branch --show-current
git rev-parse HEAD
git fetch origin
git rev-parse origin/main
git worktree list --porcelain
```

Expected：main checkout remains on `main`; user-owned `.gitignore` / `AnimalCafe.slnx` changes remain untouched; Phase 3 post-merge commit and the Studio Owner-approved P4 Design / Roadmap / spec / plan documents exist on `origin/main`。If those docs are still uncommitted or not pushed, stop at this gate and ask the Studio Owner to commit / push them through GitHub Desktop; do not create a branch from stale `origin/main`。

- [x] **Step 3: Create the isolated local branch / worktree**

```powershell
git worktree add -b codex/phase-4 'E:\Unity\Project\AnimalCafe\.worktrees\phase-4' origin/main
```

Expected：new worktree reports branch `codex/phase-4`; original checkout stays on `main`。

- [x] **Step 4: Create the remote branch explicitly authorized by the Studio Owner**

Run inside the P4 worktree：

```powershell
git push -u origin codex/phase-4
```

Expected：`origin/codex/phase-4` points to the verified starting hash。This push does not authorize later implementation pushes without a fresh request。

- [x] **Step 5: Run full baseline EditMode**

```powershell
& 'E:\Unity\Hub\Editor\6000.5.5f1\Editor\Unity.exe' -batchmode -nographics `
  -projectPath 'E:\Unity\Project\AnimalCafe\.worktrees\phase-4' `
  -runTests -testPlatform EditMode `
  -testResults "$env:TEMP\animalcafe-p4-baseline-editmode.xml" `
  -logFile "$env:TEMP\animalcafe-p4-baseline-editmode.log"
```

Expected：fresh XML exists; all tests pass; failed / skipped / inconclusive are zero。Licensing timeout without XML is not a test result and must be retried outside the sandbox when approved。

- [x] **Step 6: Run full baseline PlayMode**

```powershell
& 'E:\Unity\Hub\Editor\6000.5.5f1\Editor\Unity.exe' -batchmode -nographics `
  -projectPath 'E:\Unity\Project\AnimalCafe\.worktrees\phase-4' `
  -runTests -testPlatform PlayMode `
  -testResults "$env:TEMP\animalcafe-p4-baseline-playmode.xml" `
  -logFile "$env:TEMP\animalcafe-p4-baseline-playmode.log"
```

Expected：all PlayMode tests pass; the P3 production validator tests remain GREEN。

- [x] **Step 7: Record the gate**

Report exact hashes、counts、XML paths、limitations and `git status --short`。Do not commit。Suggested checkpoint label：`P4 baseline verified`。

---

### Task 1: Add Minimal Furniture Function and Direction Domain

**Files:**
- Create: `Assets/Scripts/Layout/FurnitureFunctionType.cs`
- Create: `Assets/Scripts/Layout/CardinalDirection.cs`
- Create: `Assets/Scripts/Layout/CashRegisterSides.cs`
- Modify: `Assets/Scripts/Layout/FurnitureDefinition.cs`
- Test: `Assets/Tests/EditMode/Phase4/FurnitureFunctionContractTests.cs`

**Interfaces:**
- Consumes: existing `FurnitureRotation` and `FurnitureDefinition` validation。
- Produces: `FurnitureDefinition.FunctionType`、`CardinalDirection.Rotate(FurnitureRotation)`、`CashRegisterSides.EmployeeSide / CustomerSide / QueueDirection`。

- [x] **Step 1: Write RED tests for backward-compatible Function Type**

```csharp
[Test]
public void LegacyConstructor_DefaultsFunctionTypeToNone()
{
    var definition = new FurnitureDefinition(
        "furniture.counter.module.01", "Counter", new GridSize(1, 1),
        PlacementSurfaceType.Floor);

    Assert.That(definition.FunctionType, Is.EqualTo(FurnitureFunctionType.None));
}

[Test]
public void FiveArgumentConstructor_PreservesCashRegisterType()
{
    var definition = new FurnitureDefinition(
        "equipment.cash-register.01", "Cash Register", new GridSize(1, 1),
        PlacementSurfaceType.FurnitureSurface, FurnitureFunctionType.CashRegister);

    Assert.That(definition.FunctionType, Is.EqualTo(FurnitureFunctionType.CashRegister));
}
```

- [x] **Step 2: Run focused tests and confirm RED**

Run EditMode filtered to `AnimalCafe.Tests.Phase4.FurnitureFunctionContractTests`。Expected：compile failure because `FurnitureFunctionType` / new overload do not exist。

- [x] **Step 3: Implement the minimal compatible enum and overload**

```csharp
public enum FurnitureFunctionType
{
    None = 0,
    CoffeeMachine = 1,
    CashRegister = 2
}
```

Keep the existing four-argument constructor and delegate to the new overload with `None`; validate with `Enum.IsDefined`。

- [x] **Step 4: Write RED tests for direction rotation and opposite sides**

```csharp
[TestCase(CardinalDirection.North, FurnitureRotation.Degrees90, CardinalDirection.East)]
[TestCase(CardinalDirection.East, FurnitureRotation.Degrees90, CardinalDirection.South)]
public void Rotate_ReturnsExpectedCardinalDirection(
    CardinalDirection direction, FurnitureRotation rotation, CardinalDirection expected)
{
    Assert.That(direction.Rotate(rotation), Is.EqualTo(expected));
}

[Test]
public void CashRegisterSides_ExposesCustomerSideAsQueueDirection()
{
    var sides = new CashRegisterSides(CardinalDirection.South, CardinalDirection.North);
    Assert.That(sides.QueueDirection, Is.EqualTo(CardinalDirection.North));
}
```

- [x] **Step 5: Run RED, implement immutable side validation, run GREEN**

`CashRegisterSides` constructor must reject same or perpendicular sides and expose `Rotate(FurnitureRotation)` returning a new value。Run focused tests, existing `FurnitureDefinitionTests`, `GridValueTests` and `FurnitureDefinitionCatalogTests`。

- [x] **Step 6: Review checkpoint**

Report files and fresh counts。Do not commit。Suggested commit message for Studio Owner：`feat: add furniture function direction contracts`。

---

### Task 2: Add Atomic Entrance Clearance Reservations to CafeLayout

**Files:**
- Create: `Assets/Scripts/Layout/LayoutReservationType.cs`
- Create: `Assets/Scripts/Layout/LayoutReservation.cs`
- Modify: `Assets/Scripts/Layout/PlacementResult.cs`
- Modify: `Assets/Scripts/Layout/CafeLayout.cs`
- Test: `Assets/Tests/EditMode/Phase4/LayoutReservationTests.cs`
- Regression: `Assets/Tests/EditMode/GridPlacementTests.cs`, `Assets/Tests/EditMode/CafeLayoutTests.cs`

**Interfaces:**
- Consumes: `GridPosition`、`GridSize`、existing atomic placement transactions。
- Produces: `CafeLayout.AddReservation(LayoutReservation)`、`Reservations` read-only view、`PlacementFailureReason.ReservedEntranceClearance`。

- [x] **Step 1: Write RED value-object tests**

```csharp
[Test]
public void EntranceReservation_ContainsAllFourCells()
{
    var reservation = new LayoutReservation(
        "entrance.main", LayoutReservationType.EntranceClearance,
        new GridPosition(3, 0), new GridSize(2, 2));

    Assert.That(reservation.Contains(new GridPosition(3, 0)), Is.True);
    Assert.That(reservation.Contains(new GridPosition(4, 1)), Is.True);
    Assert.That(reservation.Contains(new GridPosition(5, 1)), Is.False);
}
```

Add null / malformed ID、unknown type、overflow-safe bounds and non-`2 × 2` P4 fixture validator cases without hard-coding all future reservations to `2 × 2` in the general value object。

- [x] **Step 2: Run RED and implement `LayoutReservation`**

Use `long` when calculating right / top boundaries。Expose immutable Id、Type、Origin、Size and `Contains(GridPosition)`。

- [x] **Step 3: Write RED CafeLayout transaction tests**

```csharp
[Test]
public void PlaceFurniture_IntersectingEntranceClearance_IsRejectedAtomically()
{
    var layout = CreateUnlockedEightByEightLayout();
    layout.AddReservation(CreateEntranceClearance(new GridPosition(3, 0)));

    var result = layout.PlaceFurniture(
        CreateInstance("instance.counter", "furniture.counter", new GridPosition(4, 1)));

    Assert.That(result.FailureReason,
        Is.EqualTo(PlacementFailureReason.ReservedEntranceClearance));
    Assert.That(layout.OccupiedCellCount, Is.Zero);
    Assert.That(layout.FurnitureInstances, Is.Empty);
}
```

Add Move / Rotate failure fixtures proving original instance and occupied cells remain unchanged；add a query proving reservation does not mark cells as furniture occupancy or non-walkable。

- [x] **Step 4: Run RED and implement minimal CafeLayout reservation ownership**

Add private list / dictionary, read-only view and duplicate-ID validation。Check reservations after unlocked-region validation and before overlap so the reason remains specific。

- [x] **Step 5: Run focused and full P2 regression**

Expected：new reservation suite GREEN; all existing placement tests unchanged; existing constructor behavior unchanged when no reservations exist。

- [x] **Step 6: Review checkpoint**

Do not commit。Suggested commit message：`feat: reserve entrance clearance cells`。

---

### Task 3: Implement Independent Wall Slot Occupancy Domain

**Files:**
- Create: `Assets/Scripts/Layout/WallSlotPosition.cs`
- Create: `Assets/Scripts/Layout/WallFootprint.cs`
- Create: `Assets/Scripts/Layout/WallMountedInstance.cs`
- Create: `Assets/Scripts/Layout/WallSurfaceLayout.cs`
- Test: `Assets/Tests/EditMode/Phase4/WallSurfaceLayoutTests.cs`

**Interfaces:**
- Consumes: stable ID validation patterns but no `CafeLayout` occupancy dictionary。
- Produces: `WallSurfaceLayout.TryPlace / TryMove / TryRemove` with immutable state and explicit wall failure reasons local to this subsystem。

- [x] **Step 1: Write RED tests for positive Wall values**

```csharp
[Test]
public void OneByTwoFootprint_OccupiesSameColumnAcrossBothRows()
{
    var wall = new WallSurfaceLayout("wall.back-right", 8, 2);
    var item = new WallMountedInstance(
        "window.01", "window.basic.01", new WallSlotPosition(3, 0),
        new WallFootprint(1, 2));

    Assert.That(wall.TryPlace(item).Succeeded, Is.True);
    Assert.That(wall.TryGetOccupant(new WallSlotPosition(3, 0), out _), Is.True);
    Assert.That(wall.TryGetOccupant(new WallSlotPosition(3, 1), out _), Is.True);
}
```

- [x] **Step 2: Write RED boundary / atomic tests**

Cover zero / negative columns、rows、footprints；`1 × 2` starting at row 1；`2 × 1` starting at column 7；overlap；cross-surface attempt；duplicate item ID；failed move preserving origin；remove releasing exact owner；repeated remove safe failure。

- [x] **Step 3: Run RED and implement minimal domain**

Use a private `Dictionary<WallSlotPosition,string>` owned only by `WallSurfaceLayout`。Do not reference `CafeLayout` or `FurnitureRotation`。A Wall item has no rotation field in P4。

- [x] **Step 4: Run focused GREEN and mutation regression**

Assert Floor layout occupancy remains unchanged in a fixture holding both owners。Expected：all focused cases GREEN with no coupling between owners。

- [x] **Step 5: Review checkpoint**

Do not commit。Suggested commit message：`feat: add wall slot occupancy domain`。

---

### Task 4: Build Unity Furniture Definition Authoring and Catalogue Adapter

**Files:**
- Create: `Assets/Scripts/Content/FurnitureDefinitionAsset.cs`
- Create: `Assets/Scripts/Content/FurnitureContentCatalog.cs`
- Test: `Assets/Tests/EditMode/Phase4/FurnitureDefinitionAssetTests.cs`
- Test: `Assets/Tests/EditMode/Phase4/FurnitureContentCatalogTests.cs`

**Interfaces:**
- Consumes: Task 1 `FurnitureFunctionType` and existing runtime catalogue。
- Produces: `FurnitureDefinitionAsset.ToRuntimeDefinition()`、`FurnitureContentCatalog.BuildRuntimeCatalog()`、`TryGetPrefab(string, out GameObject)`。

- [x] **Step 1: Write RED conversion tests using serialized fields**

```csharp
[Test]
public void ToRuntimeDefinition_PreservesInspectorAuthoredValues()
{
    var asset = ScriptableObject.CreateInstance<FurnitureDefinitionAsset>();
    SetSerialized(asset, "definitionId", "equipment.cash-register.01");
    SetSerialized(asset, "displayName", "Cash Register");
    SetSerialized(asset, "footprintWidth", 1);
    SetSerialized(asset, "footprintDepth", 1);
    SetSerialized(asset, "allowedPlacementSurfaces", PlacementSurfaceType.FurnitureSurface);
    SetSerialized(asset, "functionType", FurnitureFunctionType.CashRegister);

    var runtime = asset.ToRuntimeDefinition();
    Assert.That(runtime.FunctionType, Is.EqualTo(FurnitureFunctionType.CashRegister));
}
```

Add exact tests for missing Prefab、zero / negative / oversized sizes、invalid enum values and source Asset non-mutation。

- [x] **Step 2: Run RED and implement beginner-readable ScriptableObject**

Use `[CreateAssetMenu(menuName = "AnimalCafe/Content/Furniture Definition")]` and `[Min(1)]` for Width / Depth, while retaining runtime validation as authority。Expose read-only properties for validator use。

- [x] **Step 3: Write RED catalogue mapping tests**

```csharp
[Test]
public void BuildRuntimeCatalog_MapsSameStableIdToDefinitionAndPrefab()
{
    var content = CreateContentCatalog(CreateValidCounterAsset());
    var runtime = content.BuildRuntimeCatalog();

    Assert.That(runtime.GetRequired("furniture.counter.module.01").Footprint,
        Is.EqualTo(new GridSize(1, 1)));
    Assert.That(content.TryGetPrefab("furniture.counter.module.01", out var prefab), Is.True);
    Assert.That(prefab, Is.Not.Null);
}
```

Add duplicate ID、null entry、failed build does not partially cache and deterministic list-order cases。

- [x] **Step 4: Implement minimal adapter and run GREEN**

Keep Unity `GameObject` references out of `FurnitureDefinition`。Build runtime Definition list first, validate all entries, then publish lookup snapshots atomically。

- [x] **Step 5: Run full existing catalogue regression**

Expected：existing pure C# catalogue behavior remains unchanged。

- [x] **Step 6: Review checkpoint**

Do not commit。Suggested commit message：`feat: add furniture definition asset authoring`。

---

### Task 5: Add Prefab Spatial Authoring Components

**Files:**
- Create: `Assets/Scripts/Content/SurfaceSlotMarker.cs`
- Create: `Assets/Scripts/Content/CashRegisterSideType.cs`
- Create: `Assets/Scripts/Content/CashRegisterSideMarker.cs`
- Create: `Assets/Scripts/Content/WallSurfaceAuthoring.cs`
- Create: `Assets/Scripts/Content/EntrancePortalAuthoring.cs`
- Create: `Assets/Scripts/Content/WallMountedDefinitionAsset.cs`
- Test: `Assets/Tests/EditMode/Phase4/Phase4MarkerContractTests.cs`

**Interfaces:**
- Consumes: stable IDs、Task 1 directions、Task 2 reservation and Task 3 Wall values。
- Produces: authoring data readable by Task 6 validator and Task 8 scene builder。

- [x] **Step 1: Write RED component tests**

```csharp
[Test]
public void SurfaceSlotMarker_ExposesStableLocalId()
{
    var go = new GameObject("SurfaceSlot_0");
    var marker = go.AddComponent<SurfaceSlotMarker>();
    SetSerialized(marker, "slotId", "slot.0");
    Assert.That(marker.SlotId, Is.EqualTo("slot.0"));
}

[Test]
public void EntranceAuthoring_CreatesExactTwoByTwoReservation()
{
    var portal = CreateEntranceAuthoring("entrance.main", new GridPosition(3, 0));
    Assert.That(portal.CreateReservation().Size, Is.EqualTo(new GridSize(2, 2)));
}
```

- [x] **Step 2: Add RED tests for Cash Register sides and Wall defaults**

Require exactly one Employee and one Customer marker at opposite cardinal local directions。Require Wall `Columns = 8`、`Rows = 2`、`SlotSize = 1f` only in the production fixture validator, not in the general reusable component constructor。

- [x] **Step 3: Implement minimal components and gizmos**

Use `OnDrawGizmosSelected` only; no runtime update loop。Markers contain data and visual editor hints, not gameplay state。Do not add a QueueDirection marker。

- [x] **Step 4: Run focused tests and compile PlayMode assembly**

Expected：components serialize and expose data; no marker adds Renderer、Collider or runtime behavior inadvertently。

- [x] **Step 5: Review checkpoint**

Do not commit。Suggested commit message：`feat: add phase 4 spatial authoring markers`。

---

### Task 6: Implement a Dedicated Phase 4 Validator

**Files:**
- Create: `Assets/Editor/Phase4/Phase4AssetIssueCode.cs`
- Create: `Assets/Editor/Phase4/Phase4AssetValidationIssue.cs`
- Create: `Assets/Editor/Phase4/Phase4AssetValidationReport.cs`
- Create: `Assets/Editor/Phase4/Phase4AssetValidator.cs`
- Create: `Assets/Editor/Phase4/Phase4ValidationMenu.cs`
- Test: `Assets/Tests/EditMode/Phase4/Phase4AssetValidatorTests.cs`

**Interfaces:**
- Consumes: Tasks 4–5 authoring objects and existing P3 Mesh / Material validation lessons。
- Produces: `Phase4AssetValidator.ValidateAll()` and `ValidateFurnitureDefinition(FurnitureDefinitionAsset)` with searchable issue codes。

- [x] **Step 1: Write RED issue/report tests**

```csharp
[Test]
public void MissingPrefab_ReportsSpecificAssetAndIssueCode()
{
    var definition = CreateDefinitionWithoutPrefab("equipment.cash-register.01");
    var report = Phase4AssetValidator.ValidateFurnitureDefinition(definition);

    Assert.That(report.Issues.Select(issue => issue.Code),
        Does.Contain(Phase4AssetIssueCode.MissingPrefab));
    Assert.That(report.Issues.Single().AssetPath, Does.Contain("cash-register"));
}
```

- [x] **Step 2: Add RED contract matrices**

Create focused fixtures for Definition IDs / sizes / surface / Function Type；Counter Slot count / duplicate ID / inactive descendant / bounds；Coffee Forward；Cash Register opposite sides；Wall dimensions / overlap / cross-corner；Entrance `2 × 2` / bounds；technical Model / Material / Shader / Texture / triangles / Collider / root transform / unexpected Camera / Light / mesh。

- [x] **Step 3: Implement validator in small private methods**

Required public issue codes include：

```csharp
MissingPrefab,
InvalidDefinition,
DuplicateDefinitionId,
InvalidSurfaceSlot,
InvalidCoffeeMachineForward,
InvalidCashRegisterSides,
InvalidEntrance,
InvalidWallSurface,
InvalidWallPlacement,
TechnicalAssetContract,
MissingReference
```

Use `Mesh.GetIndexCount(subMesh)` for triangle totals so non-readable Meshes work。Inspect full inactive descendant trees with `GetComponentsInChildren<T>(true)`。Do not call or weaken `BenchmarkAssetValidator`。

- [x] **Step 4: Run RED / GREEN per validator family**

Do not implement all checks before observing focused RED。Run one fixture family at a time, then full `Phase4AssetValidatorTests` and P3 validator suites。

- [x] **Step 5: Add menu summary**

Menu items：

```text
AnimalCafe/Phase 4/Validate Production Content
AnimalCafe/Phase 4/Build Validation Scene
```

Log exact valid / invalid asset counts and issue list；do not modify invalid assets。

- [x] **Step 6: Review checkpoint**

Do not commit。Suggested commit message：`test: add phase 4 production validation`。

---

### Task 7: Produce Formal Counter and Cash Register Sources

**Files:**
- Create: `ArtSource/Phase4/Blender/SM_Furniture_CounterModule_01.blend`
- Create: `ArtSource/Phase4/Blender/SM_Equipment_CashRegister_01.blend`
- Create through export: `Assets/Art/Phase4/Models/SM_Furniture_CounterModule_01.fbx`
- Create through export: `Assets/Art/Phase4/Models/SM_Equipment_CashRegister_01.fbx`
- Create: `Assets/Editor/Phase4/Phase4ProductionAssetBuilder.cs`
- Test: `Assets/Tests/EditMode/Phase4/Phase4ProductionAssetTests.cs`

**Interfaces:**
- Consumes: approved Work Table source、new vintage GLB、Task 6 technical rules。
- Produces: repeatable production Blender / FBX / Texture inputs for Prefab assembly。

- [x] **Step 1: Write RED production-path tests**

```csharp
[Test]
public void ProductionInputs_UseApprovedCashRegisterAndNotOldPosSource()
{
    Assert.That(Phase4ProductionAssetBuilder.CashRegisterRawSourcePath,
        Does.EndWith("vintage computer monitor 3d model.glb"));
    Assert.That(Phase4ProductionAssetBuilder.CashRegisterRawSourcePath,
        Does.Not.Contain("pos terminal"));
}
```

Add expected production FBX names、size rules and exact texture maximum cases。

- [x] **Step 2: Create Counter Blender source from approved Work Table source**

Use Blender `5.2.0 LTS`。Studio Owner approved controlled non-uniform scaling on 2026-08-04 after the authoritative Work Table source was measured at approximately `0.781529 × 0.650000 × 0.781529 m` in Unity axes and proved unable to reach both targets uniformly。Scale the derivative by controlled per-axis factors to approximately `1.00 × 0.72 × 1.00 m`；Apply Scale so the saved object and Unity root return to `1,1,1`；bottom-center origin；forward `+Y` in Blender for Unity export contract；preserve approved visual material source；reopen the saved `.blend` and remeasure。

- [x] **Step 3: Create Cash Register Blender source from the approved GLB**

Import only the high-detail terminal mesh；remove raw Cube、Camera、Light；uniform-scale to approximately `0.43 × 0.45 × 0.26 m`；bottom-center origin；choose Employee screen as forward；optimize only if triangle count exceeds `6000` after cleanup；resize Base Color to `512²` without overwriting the raw GLB；save and reopen `.blend`。

- [x] **Step 4: Export production FBX / textures**

Use Blender Z-up and FBX `Forward -Z / Up Y` so Unity root forward is `+Z`。Export only intended meshes; do not embed Camera / Light。Keep raw GLB unchanged。

- [x] **Step 5: Run automated technical validation**

Expected Counter and Cash Register bounds within approved tolerances；Cash Register ≤ `6000` triangles；texture ≤ `1024²` and target `512²`；root scale one after Unity import。

- [x] **Step 6: Art / Technical Art review checkpoint**

Provide before / after dimensions、triangles、texture size、materials and preview renders。Do not commit。Suggested commit message：`art: add phase 4 counter and cash register sources`。

---

### Task 8: Build Production Prefabs, Definition Assets and Catalogue

**Files:**
- Modify: `Assets/Editor/Phase4/Phase4ProductionAssetBuilder.cs`
- Create through builder: content under `Assets/Art/Phase4/Materials/`, `Prefabs/`, `Definitions/`, `Catalogues/`
- Test: `Assets/Tests/EditMode/Phase4/Phase4ProductionAssetTests.cs`

**Interfaces:**
- Consumes: Tasks 4–7。
- Produces: formal Work Table、Counter、Coffee Machine、Cash Register Definitions；Cup visual registration；Window Definition；one Furniture Content Catalogue。

- [x] **Step 1: Write RED expected-content test**

```csharp
[Test]
public void BuildProductionContent_CreatesExactlyApprovedFurnitureDefinitions()
{
    Phase4ProductionAssetBuilder.BuildProductionContent();
    var ids = LoadFurnitureDefinitionIds();
    Assert.That(ids, Is.EquivalentTo(new[] {
        "furniture.work-table.01",
        "furniture.counter.module.01",
        "equipment.coffee-machine.01",
        "equipment.cash-register.01"
    }));
    Assert.That(ids, Does.Not.Contain("item.ceramic-cup.01"));
}
```

- [x] **Step 2: Run RED and implement idempotent material / Prefab assembly**

Create production variants without renaming or overwriting P3 benchmark Prefabs。Add one Counter `SurfaceSlotMarker`；Coffee uses exactly one validated Forward；Cash Register gets exactly one Employee and one Customer marker with opposite directions。

- [x] **Step 3: Build Definition Assets and catalogue atomically**

Use exact Footprints / surfaces / Function Types from spec。If any required Prefab is missing or invalid, abort before publishing catalogue changes。

- [x] **Step 4: Create long-Counter fixture**

Create validation-only `1 × 3` parent fixture with one stable instance identity and `slot.0 / slot.1 / slot.2` markers。Do not add it to final content catalogue unless a formal Definition is explicitly approved later。

- [x] **Step 5: Run builder twice and compare results**

Expected：no duplicate Assets、Definitions、markers or catalogue entries；stable GUID-bearing Unity Assets remain at same paths。

- [x] **Step 6: Run Phase 4 and P3 validators**

Expected：all P4 production content valid; P3 benchmark validator still `3 / 3` valid。

- [x] **Step 7: Review checkpoint**

Do not commit。Suggested commit message：`feat: build phase 4 furniture content`。

---

### Task 9: Build the Fixed 8×8 Environment and Validation Scene

**Files:**
- Modify: `Assets/Editor/Phase4/Phase4ValidationSceneSetup.cs`
- Create through builder: `Assets/Scenes/Validation/Phase4CoreArchitecture.unity`
- Create through builder: Floor / Wall / Window / Entrance Prefabs and Materials under `Assets/Art/Phase4/Environment/`
- Test: `Assets/Tests/EditMode/Phase4/Phase4ValidationSceneSetupTests.cs`

**Interfaces:**
- Consumes: Tasks 2–6 and palette B。
- Produces: deterministic Scene fixture with stable names / IDs consumed by PlayMode tests and manual QA。

- [x] **Step 1: Write RED scene-shape tests**

```csharp
[Test]
public void ConfigureScene_CreatesExactEnvironmentContract()
{
    Phase4ValidationSceneSetup.ConfigureOpenSceneForTests();

    Assert.That(Find("P4_Floor_8x8"), Is.Not.Null);
    Assert.That(Find("P4_Wall_BackLeft").GetComponent<WallSurfaceAuthoring>().Columns, Is.EqualTo(8));
    Assert.That(Find("P4_Wall_BackRight").GetComponent<WallSurfaceAuthoring>().Rows, Is.EqualTo(2));
    Assert.That(Find("P4_Entrance").GetComponent<EntrancePortalAuthoring>()
        .CreateReservation().Size, Is.EqualTo(new GridSize(2, 2)));
}
```

- [x] **Step 2: Add RED idempotence / isolation tests**

Run configure twice and assert exactly one root、Floor、two Walls、Window、Entrance、Camera and fixture group。Assert no MainCafe Scene modification and no Build Settings mutation。

- [x] **Step 3: Implement environment Materials and geometry**

Build Floor aligned to `8 × 8` cells；two visible walls only；physical wall height about `3 m`；Wall authoring origin places lower slots at `0.5 m`；Window on Back-right lower center；Entrance at open front with exact `2 × 2` overlay。Use simple functional Entrance visual, not formal VFX。

- [x] **Step 4: Add fixture groups**

Include three adjacent Counter modules、one `1 × 3` Counter fixture、Coffee Machine at four rotations、Cash Register at four rotations、valid / invalid Wall items and overlap / corner examples。Keep invalid fixtures clearly separated and disabled by default where they would fail production validation。

- [x] **Step 5: Run scene tests and save deterministically**

Expected：setup twice yields identical counts and stable IDs；Scene saves only after validation succeeds。

- [x] **Step 6: Review checkpoint**

Do not commit。Suggested commit message：`feat: add phase 4 validation environment`。

---

### Task 10: Add PlayMode Integration and Build Settings Isolation

**Files:**
- Create: `Assets/Tests/PlayMode/Phase4/Phase4EnvironmentIntegrationTests.cs`
- Create: `Assets/Tests/PlayMode/Phase4/Phase4BuildSettingsIsolationTests.cs`
- Reuse pattern from: `Assets/Tests/PlayMode/AssetReadability/AssetPipelineReadabilityBuildSettingsScope.cs`

**Interfaces:**
- Consumes: saved Task 9 validation Scene and production content。
- Produces: runtime smoke evidence without permanently editing Build Settings。

- [x] **Step 1: Write RED PlayMode environment tests**

```csharp
[UnityTest]
public IEnumerator Phase4Scene_LoadsWithExactStableEnvironment()
{
    yield return LoadPhase4ValidationScene();
    Assert.That(GameObject.Find("P4_Floor_8x8"), Is.Not.Null);
    Assert.That(Object.FindObjectsByType<WallSurfaceAuthoring>(FindObjectsSortMode.None).Length,
        Is.EqualTo(2));
    LogAssert.NoUnexpectedReceived();
}
```

Add tests for production catalogue / Prefab resolution、Entrance Collider not covering clearance、Floor selection raycast layer and no missing scripts。

- [x] **Step 2: Implement temporary Build Settings scope**

Copy the proven disposal pattern, not the P3 names。Add Phase4 Scene only for the test lifetime and restore the exact prior Build Settings list in `Dispose` / `finally`。

- [x] **Step 3: Run focused PlayMode RED / GREEN**

Expected：tests fail before scene/build scope exists, then pass with no persistent Build Settings change。

- [x] **Step 4: Run all PlayMode regression**

Expected：Phase 0 controls、MainCafe、P3 readability and new P4 tests all GREEN。

- [x] **Step 5: Review checkpoint**

Do not commit。Suggested commit message：`test: verify phase 4 scene integration`。

---

### Task 11: Execute Complete Automated Matrix and Harden Review Findings

**Files:**
- Modify only focused files named by a reproduced failing case。
- Test: all P4 EditMode / PlayMode suites and existing regression suites。

**Interfaces:**
- Consumes: Tasks 1–10 complete implementation。
- Produces: traceable coverage map for approved `N01–N69` and `B01–B112`。

- [x] **Step 1: Create a coverage table inside the test report, not new behavior**

Map each approved ID to an automated test method or manual gate。No approved ID may disappear merely because multiple assertions share one method。

- [x] **Step 2: Run focused P4 EditMode**

Use `-testFilter AnimalCafe.Tests.Phase4` and fresh XML。Expected：all P4 EditMode tests GREEN。

- [x] **Step 3: Run full EditMode**

Expected：all existing + P4 tests pass；failed / skipped / inconclusive zero。

- [x] **Step 4: Run focused and full PlayMode**

Expected：P4 focused and full PlayMode pass；Build Settings restored exactly。

- [x] **Step 5: Run production validators**

Run P3 benchmark and P4 production validation。Expected：P3 `3 / 3` valid and all approved P4 production assets valid with zero issues。

- [x] **Step 6: Request independent code / asset review**

Use `superpowers:requesting-code-review` after implementation is complete。Reproduce every finding before changing code；use `superpowers:receiving-code-review` for unclear or questionable feedback。Any bugfix returns to correct RED → GREEN。

- [x] **Step 7: Review checkpoint**

Do not commit。Suggested commit message：`test: complete phase 4 regression coverage`。

---

### Task 12: Studio Owner Manual Acceptance and Documentation Closeout

**Files:**
- Create: `Docs/Phase4_Beginner_Guide.md`
- Modify after acceptance only: `Docs/AnimalCafe_Development_Roadmap.md`
- Modify if durable design changed during playtest: `Docs/AnimalCafe_Project_Design.md`
- Modify if approved implementation differs: `Docs/superpowers/specs/2026-08-03-phase-4-core-architecture-functional-furniture-design.md`

**Interfaces:**
- Consumes: stable final menu paths、Scene、assets and test evidence。
- Produces: beginner runbook、manual `M01–M88` record and accurate project truth。

- [x] **Step 1: Write the beginner runbook with exact actions**

The guide must include：

```text
Open the P4 worktree in Unity 6000.5.5f1
Run AnimalCafe/Phase 4/Build Validation Scene
Run AnimalCafe/Phase 4/Validate Production Content
Open Assets/Scenes/Validation/Phase4CoreArchitecture.unity
Clear Console
Enter Play Mode
Follow grouped M01–M88 checks
Exit Play Mode and rerun validators
```

Explain Inspector identifiers in Chinese while preserving English field names。

- [x] **Step 2: Confirm Cash Register use rights**

Studio Owner must explicitly record permission for development and future commercial release。Without this, Art gate is Blocked even if tests pass。

- [x] **Step 3: Execute grouped manual checks**

Record `Passed / Failed / Blocked / justified Not Applicable` for：Inspector、Floor / Wall / Entrance、Counter、Coffee Machine、Cash Register、Window、Camera / palette、MainCafe regression and Console。One operation may satisfy multiple IDs, but evidence must list all IDs covered。

- [x] **Step 4: Fix any manual failure through RED / GREEN**

Visual-only failures require a validator fixture or explicit before / after screenshot when automation is not meaningful。Rerun the affected group and full relevant regression。

- [x] **Step 5: Run verification-before-completion**

Invoke `superpowers:verification-before-completion`。Collect fresh final EditMode XML、PlayMode XML、P3 validator、P4 validator、Git status and manual record。Do not claim complete from older output。

- [x] **Step 6: Update Roadmap only after the Studio Owner accepts**

Set Phase 4 `Completed` only when all required automated / manual / rights gates pass。Record exact counts、known limitations and next gate。Do not start Phase 5 automatically。

- [x] **Step 7: Final handoff**

List modified / new files、binary assets、tests、manual evidence、remaining limitations and suggested commit groups。Do not commit、push、merge、delete branch or remove worktree without a new explicit request。

Suggested documentation commit message for Studio Owner：`docs: complete phase 4 handoff`。

---

## Execution Checkpoints

The Studio Owner reviews after：

1. Task 0 branch / baseline evidence；
2. Tasks 1–3 domain contracts；
3. Tasks 4–6 Unity authoring / validator；
4. Tasks 7–9 production assets / environment；
5. Tasks 10–11 full automation / review；
6. Task 12 manual acceptance / closeout。

No checkpoint authorizes later commit、push、merge or cleanup。

## Spec Coverage Map

- Spec §§5.2–5.3 → Tasks 1、4。
- Spec §5.4 Floor → Task 2；Surface → Task 5；Wall → Task 3。
- Spec §6 Environment → Tasks 5、9、10。
- Spec §7 assets → Tasks 7–9。
- Spec §§8–9 data flow / errors → Tasks 4–6。
- Spec §§10–12 TDD matrices → Tasks 1–11。
- Spec §13 manual acceptance → Task 12。
- Spec §14 branch gate → Task 0。
- Spec §§15–17 boundaries / risks / completion → Global Constraints、Tasks 11–12。
