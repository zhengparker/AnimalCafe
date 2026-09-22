# Phase 8 Functional Furniture & Layout Readiness Implementation Plan

> 当前状态（2026-09-21）：Phase 8 **Completed — merged to main**。PR #7 已 merge，Owner 已批准整体收尾，本地同步、post-merge 回归与归档后清理均完成；最终状态见 Beginner Guide 第 37 节。以下 Task 实施记录及 checkbox 保留当时快照，不作为重新实施已完成功能的指令。

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:test-driven-development` for each implementation Task. Execute inline unless the Studio Owner explicitly authorizes sub-agents. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让玩家在现有 Decoration Mode 中把多个 Cash Registers、Coffee Machines 和 Pick-up Points 放到兼容 Surface Slots，并让纯 C# readiness system 判断当前布局是否至少有一组完整、可达的营业功能点。

**Architecture:** `CafeLayout` 继续拥有 Floor furniture；新的 `FunctionalSurfaceLayout` 拥有 Surface Slot occupancy 和所有 functional instances。Prefab markers 在初始化时转换成纯 C# `SurfaceSlotCatalog` snapshot；`InteractionAnchorResolver`、`GridReachabilityEvaluator` 和 `LayoutReadinessEvaluator` 只读取 domain data，不扫描 Scene object names。

**Tech Stack:** Unity `6000.5.5f1`、C#、NUnit EditMode / PlayMode tests、URP `17.5.0`、现有 `AnimalCafe.Runtime` / `AnimalCafe.Editor` assemblies。

**Spec:** `Docs/superpowers/specs/2026-09-02-phase-8-functional-furniture-layout-readiness-design.md`

**Test Cases:** `Docs/superpowers/specs/2026-09-02-phase-8-functional-furniture-layout-readiness-test-cases.md`

## Global Constraints

- Windows mouse first；future iOS touch compatibility 保留，不做 iOS device acceptance。
- Cash Register / Coffee Machine 只允许 `PlacementSurfaceType.FurnitureSurface`，玩家不手动指定 anchors。
- Pick-up Point 使用兼容 `1 × 1 Surface Slot`、不显示 Rotate、自动选择 anchors。
- 每种必要功能至少一个 valid instance，且至少存在一组同时满足两套独立连通网络的 CR / CM / Pick-up，才 `CanOpenForBusiness`：顾客网络要求 Entrance、Cash Register customer anchor 与 Pick-up customer anchor 连通；员工网络要求 Cash Register employee anchor、Coffee Machine employee anchor 与 Pick-up employee anchor 连通。Pick-up 的两个角色 anchor 可以共用同一个 Grid cell。
- 多余 invalid instances 只产生 Warning；没有完整有效组合时产生 Blocking failure。
- 支撑家具 Move / Rotate 时 bindings 跟随；支撑家具仍有 mounted content 时禁止 Store。
- 不实现 NPC、Order、Queue、Unity NavMesh、Save / Load、家具商店、全面 UI visual redesign 或 Phase 8R polish。
- 保留所有用户无关修改；未经单独授权不 commit、merge、删除 branch/worktree。
- 每个 Task 只跑 focused tests 和 direct regression；完整 regression 集中在 Phase final closeout。

## Branch and Workspace Gate

Plan 批准后、Task 1 开始前：

```text
Local branch: codex/phase-8-functional-furniture
Remote branch: origin/codex/phase-8-functional-furniture
Worktree: E:\Unity\Project\AnimalCafe\.worktrees\phase-8-functional-furniture
Base: fresh origin/main
```

- 创建前重新核对 `main == origin/main` 和 `.worktrees/` 被 ignore。
- 只把批准的 Phase 8 Roadmap、spec、test cases 和本 plan 带入 worktree。
- 使用 Unity `6000.5.5f1` 对 worktree 跑 baseline EditMode / PlayMode；失败时停止并由 Studio Owner 决定调查还是继续。

---

### Task 1: Surface Slot Snapshot and Stable Domain IDs

**Files:**

- Create: `Assets/Scripts/Layout/SurfaceSlotAddress.cs`
- Create: `Assets/Scripts/Layout/SurfaceSlotDefinition.cs`
- Create: `Assets/Scripts/Layout/SurfaceSlotCatalog.cs`
- Create: `Assets/Scripts/Layout/SurfaceMountedInstance.cs`
- Create: `Assets/Scripts/Layout/PickUpPointInstance.cs`
- Modify: `Assets/Scripts/Layout/StableId.cs`
- Modify: `Assets/Scripts/Content/FurnitureContentCatalog.cs`
- Test: `Assets/Tests/EditMode/Phase8/SurfaceSlotDomainTests.cs`
- Test: `Assets/Tests/EditMode/Phase8/SurfaceSlotCatalogTests.cs`

**Interfaces:**

- Consumes: `FurnitureInstance.InstanceId`、`FurnitureDefinition.Id`、`SurfaceSlotMarker.SlotId`、Prefab marker local position。
- Produces:

```csharp
public readonly struct SurfaceSlotAddress : IEquatable<SurfaceSlotAddress>
{
    public string SupportFurnitureInstanceId { get; }
    public string SlotId { get; }
}

public sealed class SurfaceSlotDefinition
{
    public string SupportDefinitionId { get; }
    public string SlotId { get; }
    public GridPosition LocalCell { get; }
}

public sealed class SurfaceSlotCatalog
{
    public bool TryGet(string supportDefinitionId, string slotId,
        out SurfaceSlotDefinition definition);
    public IReadOnlyList<SurfaceSlotDefinition> GetForSupport(string supportDefinitionId);
}

public sealed class SurfaceMountedInstance
{
    public string InstanceId { get; }
    public string DefinitionId { get; }
    public SurfaceSlotAddress Address { get; }
    public FurnitureRotation Rotation { get; }
}

public sealed class PickUpPointInstance
{
    public string InstanceId { get; }
    public SurfaceSlotAddress Address { get; }
}
```

- `FurnitureContentCatalog.BuildSurfaceSlotCatalog(GridSettings settings)` converts markers to `LocalCell`; marker positions must resolve to exactly one support-footprint cell or initialization fails with the definition and Slot IDs。
- `StableId.NewSurfaceMountedInstanceId()` and `StableId.NewPickUpPointInstanceId()` reuse the existing 32-character lowercase stable-ID format but remain separately named factories。

- [ ] **Step 1: Write failing address, ID, marker conversion and duplicate-Slot tests**

Cover `P8-E-N-001` and the Slot-definition prerequisites for `P8-E-B-001/002`。Example assertion:

```csharp
Assert.That(new SurfaceSlotAddress(supportId, "slot.0"),
    Is.EqualTo(new SurfaceSlotAddress(supportId, "slot.0")));
Assert.That(catalog.GetForSupport("furniture.counter.long.01")
    .Select(slot => slot.SlotId),
    Is.EqualTo(new[] { "slot.0", "slot.1", "slot.2" }));
```

- [ ] **Step 2: Run focused EditMode tests and record trustworthy RED**

Run in Unity Test Runner: EditMode filter `AnimalCafe.Tests.EditMode.Phase8.SurfaceSlotDomainTests,AnimalCafe.Tests.EditMode.Phase8.SurfaceSlotCatalogTests`。Expected: compile/test failure only because Phase 8 types and factories do not exist。

- [ ] **Step 3: Implement minimal immutable value objects and catalogue conversion**

Validate null/blank IDs, duplicate `(SupportDefinitionId, SlotId)` keys, off-cell markers and markers outside the rotated-neutral support footprint。Sort snapshots by definition ID then Slot ID for deterministic tests。

- [ ] **Step 4: Run focused GREEN and Phase 4 marker regression**

Run focused filters above plus `Phase4MarkerContractTests` and `Phase4ProductionAssetTests`。Expected: all pass, zero skipped/inconclusive。

- [ ] **Step 5: Stop for Task review**

Report exact files, RED cause, GREEN counts and direct regression. Do not commit unless Studio Owner authorizes it。

### Task 2: FunctionalSurfaceLayout and Atomic Occupancy

**Files:**

- Create: `Assets/Scripts/Layout/FunctionalSurfacePlacementFailureReason.cs`
- Create: `Assets/Scripts/Layout/FunctionalSurfacePlacementResult.cs`
- Create: `Assets/Scripts/Layout/FunctionalSurfaceLayout.cs`
- Test: `Assets/Tests/EditMode/Phase8/FunctionalSurfaceLayoutTests.cs`
- Test: `Assets/Tests/EditMode/Phase8/OccupiedSupportFurnitureTests.cs`

**Interfaces:**

- Consumes: Task 1 types、`CafeLayout.TryGetFurnitureInstance`、`FurnitureDefinitionCatalog`、`SurfaceSlotCatalog`。
- Produces:

```csharp
public sealed class FunctionalSurfaceLayout
{
    public IReadOnlyList<SurfaceMountedInstance> MountedInstances { get; }
    public IReadOnlyList<PickUpPointInstance> PickUpPoints { get; }
    public FunctionalSurfacePlacementResult PlaceMounted(SurfaceMountedInstance instance);
    public FunctionalSurfacePlacementResult MoveMounted(string instanceId, SurfaceSlotAddress address);
    public FunctionalSurfacePlacementResult RotateMounted(string instanceId, FurnitureRotation rotation);
    public FunctionalSurfacePlacementResult RemoveMounted(string instanceId);
    public FunctionalSurfacePlacementResult PlacePickUp(PickUpPointInstance instance);
    public FunctionalSurfacePlacementResult MovePickUp(string instanceId, SurfaceSlotAddress address);
    public FunctionalSurfacePlacementResult RemovePickUp(string instanceId);
    public bool TryGetOccupant(SurfaceSlotAddress address, out string instanceId);
    public IReadOnlyList<string> GetContentIdsForSupport(string supportFurnitureInstanceId);
}
```

`DecorationSession` will call `FunctionalSurfaceLayout.GetContentIdsForSupport` before Floor Store; `CafeLayout` does not take ownership of or gain dependencies on surface content, and there is no automatic cascade delete。

- [ ] **Step 1: Write failing normal, multiple-instance, invalid occupancy and Store-blocking tests**

Cover `P8-E-N-002–009`、`P8-E-I-001–006`、`P8-E-B-001/002` and `P8-E-R-005–008`。Assert failed mutations preserve counts, addresses and original occupants。

- [ ] **Step 2: Run focused RED**

Run EditMode filter `FunctionalSurfaceLayoutTests,OccupiedSupportFurnitureTests`。Expected: Phase 8 layout API missing; pre-existing `CafeLayout` tests remain compilable。

- [ ] **Step 3: Implement candidate-first atomic mutation**

Every mutation validates definition type、support instance、Slot existence/compatibility、occupancy and rotation before publishing list/dictionary changes。Return result values; do not throw for normal player-invalid placement。

- [ ] **Step 4: Run GREEN and direct `CafeLayout` regression**

Run Task tests plus `CafeLayoutTests`、`GridPlacementTests` and `LayoutReservationTests`。Expected: all pass。

- [ ] **Step 5: Stop for Task review without commit**

### Task 3: Preview Transactions for Mounted Equipment and Pick-up Points

**Files:**

- Create: `Assets/Scripts/Decoration/FunctionalSurfacePlacementPreview.cs`
- Create: `Assets/Scripts/Decoration/FunctionalSurfaceDecorationSession.cs`
- Modify: `Assets/Scripts/Decoration/DecorationSession.cs`
- Modify: `Assets/Scripts/Decoration/FurniturePlacementPreview.cs`
- Test: `Assets/Tests/EditMode/Phase8/FunctionalSurfaceDecorationSessionTests.cs`
- Modify test: `Assets/Tests/EditMode/Phase6/DecorationSessionTests.cs`

**Interfaces:**

```csharp
public enum FunctionalSurfacePreviewKind { MountedEquipment, PickUpPoint }

public sealed class FunctionalSurfacePlacementPreview
{
    public FunctionalSurfacePreviewKind Kind { get; }
    public string InstanceId { get; }
    public string DefinitionId { get; }
    public SurfaceSlotAddress Address { get; }
    public FurnitureRotation Rotation { get; }
    public bool IsNew { get; }
    public FunctionalSurfacePlacementResult Validation { get; }
    public bool CanConfirm { get; }
}

public sealed class FunctionalSurfaceDecorationSession
{
    public FunctionalSurfacePlacementPreview ActivePreview { get; }
    public FunctionalSurfacePlacementResult BeginCreateMounted(string definitionId,
        SurfaceSlotAddress address);
    public FunctionalSurfacePlacementResult BeginCreatePickUp(SurfaceSlotAddress address);
    public FunctionalSurfacePlacementResult BeginMoveMounted(string instanceId);
    public FunctionalSurfacePlacementResult BeginMovePickUp(string instanceId);
    public FunctionalSurfacePlacementResult MovePreview(SurfaceSlotAddress address);
    public FunctionalSurfacePlacementResult RotatePreview();
    public FunctionalSurfacePlacementResult Confirm();
    public void Cancel();
    public FunctionalSurfacePlacementResult ConfirmStore();
}
```

- Pick-up `RotatePreview()` returns an explicit unsupported-action result and never changes preview。
- New Cancel removes only preview；existing Cancel restores original address/rotation because confirmed layout was never mutated。

- [ ] **Step 1: Write failing preview lifecycle and recovery tests**

Cover `P8-E-R-001–006/008` and Pick-up no-Rotate rule。Include a confirm-time conflict injected after Preview validation to prove atomic failure。

- [ ] **Step 2: Run focused RED**

Run EditMode filter `FunctionalSurfaceDecorationSessionTests`。Expected: missing session/preview types。

- [ ] **Step 3: Implement preview-only state and Confirm delegation**

Follow existing `DecorationSession` / `WallMountedDecorationSession` patterns without copying UI logic into the domain session。

- [ ] **Step 4: Run GREEN and Floor/Wall transaction regression**

Run new tests plus `DecorationSessionTests`、`WallMountedDecorationSessionTests`、`SurfaceDecorationSessionTests`。Expected: all pass。

- [ ] **Step 5: Stop for Task review without commit**

### Task 4: Automatic Interaction Anchors

**Files:**

- Create: `Assets/Scripts/Layout/InteractionRole.cs`
- Create: `Assets/Scripts/Layout/InteractionAnchor.cs`
- Create: `Assets/Scripts/Layout/ResolvedStationAnchors.cs`
- Create: `Assets/Scripts/Layout/InteractionAnchorResolver.cs`
- Create: `Assets/Scripts/Content/FunctionalDirectionCatalog.cs`
- Modify: `Assets/Scripts/Content/FurnitureContentCatalog.cs`
- Test: `Assets/Tests/EditMode/Phase8/InteractionAnchorResolverTests.cs`
- Test: `Assets/Tests/EditMode/Phase8/FunctionalDirectionCatalogTests.cs`

**Interfaces:**

```csharp
public enum InteractionRole { Employee, Customer }

public readonly struct InteractionAnchor
{
    public InteractionRole Role { get; }
    public GridPosition Position { get; }
    public CardinalDirection Facing { get; }
}

public sealed class InteractionAnchorResolver
{
    public ResolvedStationAnchors ResolveMounted(SurfaceMountedInstance instance,
        CafeLayout cafeLayout, SurfaceSlotCatalog slots, FunctionalDirectionCatalog directions);
    public ResolvedStationAnchors ResolvePickUp(PickUpPointInstance instance,
        CafeLayout cafeLayout, SurfaceSlotCatalog slots, Func<GridPosition, bool> isWalkable);
}
```

- Cash Register directions come from `CashRegisterSideMarker.ReadSidesFrom` snapshot。
- Coffee Machine direction comes from the existing authored Forward marker contract snapshot。
- Pick-up candidates use North → East → South → West; opposite pair first, distinct pair second, one shared cell last。

- [ ] **Step 1: Write failing four-rotation and automatic Pick-up tests**

Cover `P8-E-N-010–012`、`P8-E-I-007–010` and support Move/Rotate anchor recalculation。Use exact GridPosition assertions for all rotations。

- [ ] **Step 2: Run focused RED**

Run EditMode filter `InteractionAnchorResolverTests,FunctionalDirectionCatalogTests`。Expected: missing resolver/snapshot types。

- [ ] **Step 3: Implement pure coordinate transforms**

Keep Unity `Transform` reading inside content snapshot creation. Resolver uses `GridPosition`、`CardinalDirection.Rotate`、support placement and Slot local cell only。

- [ ] **Step 4: Run GREEN and Phase 4 direction regression**

Run Task tests plus `FurnitureFunctionContractTests`、`Phase4MarkerContractTests` and production asset direction tests。

- [ ] **Step 5: Stop for Task review without commit**

### Task 5: Grid Reachability and Multi-station Readiness Report

**Files:**

- Create: `Assets/Scripts/Layout/GridReachabilityEvaluator.cs`
- Create: `Assets/Scripts/Layout/LayoutReadinessFailure.cs`
- Create: `Assets/Scripts/Layout/StationReadiness.cs`
- Create: `Assets/Scripts/Layout/LayoutReadinessSummary.cs`
- Create: `Assets/Scripts/Layout/LayoutReadinessReport.cs`
- Create: `Assets/Scripts/Layout/LayoutReadinessEvaluator.cs`
- Modify: `Assets/Scripts/Layout/CafeLayout.cs`
- Test: `Assets/Tests/EditMode/Phase8/GridReachabilityEvaluatorTests.cs`
- Test: `Assets/Tests/EditMode/Phase8/LayoutReadinessEvaluatorTests.cs`

**Interfaces:**

```csharp
public enum LayoutReadinessSeverity { Warning, Blocking }
public enum LayoutReadinessFailureCode
{
    MissingCashRegister, MissingCoffeeMachine, MissingPickUpPoint,
    MissingSupportFurniture, MissingSurfaceSlot, DuplicateSurfaceOccupancy,
    AnchorOutOfBounds, AnchorBlocked, AnchorUnreachable,
    NoCompleteReachableServiceCombination
}

public sealed class GridReachabilityEvaluator
{
    public bool IsReachable(GridPosition start, GridPosition target,
        Func<GridPosition, bool> isWalkable);
}

public sealed class LayoutReadinessReport
{
    public bool CanOpenForBusiness { get; }
    public IReadOnlyList<StationReadiness> Stations { get; }
    public IReadOnlyList<LayoutReadinessFailure> Failures { get; }
    public LayoutReadinessSummary CashRegisters { get; }
    public LayoutReadinessSummary CoffeeMachines { get; }
    public LayoutReadinessSummary PickUpPoints { get; }
}

public sealed class LayoutReadinessEvaluator
{
    public LayoutReadinessReport Evaluate(CafeLayout cafeLayout,
        FunctionalSurfaceLayout functionalLayout,
        SurfaceSlotCatalog slots,
        FunctionalDirectionCatalog directions);
}
```

`CafeLayout` exposes only the read-only Grid queries required by reachability:

```csharp
public bool IsInsideUnlockedRegion(GridPosition position);
public bool HasReservation(GridPosition position, LayoutReservationType type);
```

- BFS uses an iterative `Queue<GridPosition>` and a visited set; neighbor order North → East → South → West。
- EntranceClearance reservation cells are walkable route origins but remain unavailable for furniture placement。
- Station and failure output sorts by function type、instance ID、failure code for stable tests/UI。

- [ ] **Step 1: Write failing normal, invalid, boundary and corrupt-binding tests**

Cover `P8-E-N-013–016`、`P8-E-I-005–013`、`P8-E-B-003` and `P8-E-R-009/010`。Include one valid service combination plus invalid extras, and individually valid stations separated into disconnected components。

- [ ] **Step 2: Run focused RED**

Run EditMode filter `GridReachabilityEvaluatorTests,LayoutReadinessEvaluatorTests`。Expected: missing evaluator/report APIs。

- [ ] **Step 3: Implement iterative reachability and aggregate-all reporting**

Do not short-circuit after the first invalid station. Build all station results, connected-component membership and summaries before computing `CanOpenForBusiness`。

- [ ] **Step 4: Run GREEN and layout regressions**

Run Task tests plus Phase 1/2 `CafeLayout`、reservation and placement suites。Expected: all pass without performance timeout。

- [ ] **Step 5: Stop for Task review without commit**

### Task 6: Runtime Views, Pick-up Indicator and Scene Registries

**Files:**

- Create: `Assets/Scripts/Decoration/SurfaceMountedSceneRegistry.cs`
- Create: `Assets/Scripts/Decoration/SurfaceMountedPreviewView.cs`
- Create: `Assets/Scripts/Decoration/PickUpPointIndicatorView.cs`
- Create: `Assets/Scripts/Decoration/InteractionAnchorDebugView.cs`
- Modify: `Assets/Scripts/Decoration/CafeLayoutRuntime.cs`
- Create assets: `Assets/UI/Phase8/Prefabs/PF_UI_PickUpPointIndicator.prefab`
- Create assets: `Assets/UI/Phase8/Prefabs/PF_UI_FunctionalSurfacePreview.prefab`
- Test: `Assets/Tests/PlayMode/Phase8FunctionalSurfaceViewPlayModeTests.cs`

**Interfaces:**

```csharp
public sealed class CafeLayoutRuntime : MonoBehaviour
{
    public FunctionalSurfaceLayout FunctionalSurfaceLayout { get; private set; }
    public LayoutReadinessReport CurrentReadiness { get; }
    public void RecalculateReadiness();
}

public sealed class SurfaceMountedSceneRegistry : MonoBehaviour
{
    public void Rebuild(IReadOnlyList<SurfaceMountedInstance> instances);
}

public sealed class PickUpPointIndicatorView : MonoBehaviour
{
    public void Rebuild(IReadOnlyList<PickUpPointInstance> points, bool decorationModeVisible);
    public void ShowPreview(FunctionalSurfacePlacementPreview preview);
    public void HidePreview();
}
```

- Pick-up mesh is an inverted square pyramid above the Slot, with a separate 1 × 1 footprint visual and existing valid/invalid colors。
- Debug anchors render only in the Phase 8 validation review state, never normal MainCafe non-debug state。

- [ ] **Step 1: Write failing view rebuild, follow-transform and visibility tests**

Cover domain/view identity、multiple instances、support move/rotate、indicator exit/re-entry and debug-only visibility。

- [ ] **Step 2: Run focused PlayMode RED**

Run PlayMode filter `Phase8FunctionalSurfaceViewPlayModeTests`。Expected: missing views/prefabs or assertions failing before implementation。

- [ ] **Step 3: Implement registries and minimal Phase 7-consistent visuals**

Use confirmed domain lists as the only rebuild input. Destroy/recreate only owned representation children; do not scan arbitrary Scene names for business identity。

- [ ] **Step 4: Run GREEN and `FurnitureSceneRegistry` / Wall view regression**

Run Task filter plus Phase 6 furniture Scene and Phase 7 wall-mounted Scene tests。

- [ ] **Step 5: Stop for technical review; mark visual acceptance Pending**

Do not claim indicator appearance accepted until P8-M-006。

### Task 7: Furniture Tab Rows and Pick-up Point Button

**Files:**

- Modify: `Assets/Scripts/Decoration/DecorationCatalogueAsset.cs`
- Modify: `Assets/Scripts/UI/Decoration/DecorationCatalogueModels.cs`
- Modify: `Assets/Scripts/UI/Decoration/DecorationCatalogueView.cs`
- Modify: `Assets/Scripts/UI/Decoration/DecorationCatalogueTileView.cs`
- Modify: `Assets/Scripts/UI/Decoration/DecorationActionBarView.cs`
- Create asset: `Assets/UI/Phase8/Prefabs/PF_UI_Phase8DecorationCatalogue.prefab`
- Create asset: `Assets/UI/Phase8/Prefabs/PF_UI_Phase8DecorationActionBar.prefab`
- Test: `Assets/Tests/EditMode/Phase8/Phase8CatalogueTests.cs`
- Test: `Assets/Tests/PlayMode/Phase8DecorationCataloguePlayModeTests.cs`

**Interfaces:**

```csharp
public enum DecorationCatalogueItemKind
{
    Furniture, CashRegister, CoffeeMachine, PickUpPoint,
    Floor, WallSurface, WallMounted
}

public sealed class DecorationCatalogueView : MonoBehaviour
{
    public event Action PickUpPointRequested;
}
```

- Model builder partitions `DecorationCatalogueAsset.Entries` by `AllowedPlacementSurfaces` and `FunctionType`。
- Floor `FunctionType.None` remains Furniture row；FurnitureSurface CashRegister / CoffeeMachine enter their named rows；Pick-up is one action button, not a fake definition/card row。

- [ ] **Step 1: Write failing model, prefab hierarchy and input-boundary tests**

Cover `P8-P-N-001` and `P8-P-I-001` prerequisites。Assert row order `Furniture, Cash Register, Coffee Machine` and one Pick-up button。

- [ ] **Step 2: Run focused EditMode/PlayMode RED**

Run `Phase8CatalogueTests,Phase8DecorationCataloguePlayModeTests`。Expected: enum/model/prefab rows missing。

- [ ] **Step 3: Implement rows by reusing existing card construction and layout values**

Do not introduce a second Catalogue framework. Bind Pick-up button through the same UI pointer boundary used by current cards/actions。

- [ ] **Step 4: Run GREEN and Phase 6/7 Catalogue regression**

Run Task tests plus `DecorationCatalogueTests`、`Phase7CatalogueTests`、Phase 6/7 UI prefab tests。

- [ ] **Step 5: Stop for Task review; visual acceptance Pending**

### Task 8: DecorationModeController Integration and Player Feedback

**Files:**

- Modify: `Assets/Scripts/Decoration/DecorationModeController.cs`
- Modify: `Assets/Scripts/Decoration/Input/DecorationTouchFrame.cs`
- Modify: `Assets/Scripts/Decoration/Input/DecorationTouchRouter.cs`
- Modify: `Assets/Scripts/Decoration/PlacementFeedbackMapper.cs`
- Modify: `Assets/Scripts/UI/Feedback/ValidationMessageView.cs`
- Modify: `Assets/Scripts/Decoration/CafeLayoutRuntime.cs`
- Test: `Assets/Tests/EditMode/Phase8/Phase8ControllerContractTests.cs`
- Test: `Assets/Tests/PlayMode/Phase8FunctionalSurfaceInteractionPlayModeTests.cs`
- Modify test: `Assets/Tests/PlayMode/Phase6DecorationTouchPlayModeTests.cs`
- Modify test: `Assets/Tests/PlayMode/Phase7WallMountedTouchPlayModeTests.cs`

**Interfaces:**

- Add `FunctionalSurface` hit/gesture ownership without changing existing Furniture / Wall / UI priority。
- Controller owns one active editing flow at a time and delegates mutations to the correct session。
- Readiness recalculates after confirmed domain mutation only; Preview uses candidate validation and never publishes a current report。
- Feedback maps stable failure codes to Chinese player-readable text while retaining identifiers for diagnostics。

- [ ] **Step 1: Write failing end-to-end controller tests**

Cover CR/CM Preview→Move→Rotate→Confirm/Cancel/Store, Pick-up create multiple→Move→Confirm/Cancel/Store, invalid Confirm, pointer cancellation, UI non-leak and occupied-support Store blocking。

- [ ] **Step 2: Run focused RED**

Run `Phase8ControllerContractTests,Phase8FunctionalSurfaceInteractionPlayModeTests`。Expected: missing controller bindings and hit kind handling。

- [ ] **Step 3: Implement minimal controller orchestration**

Keep business validation out of `DecorationModeController`; it coordinates sessions、views、action-bar visibility and feedback only。

- [ ] **Step 4: Run GREEN and direct input/UI regression**

Run Task tests plus Phase 6 touch/UI and Phase 7 wall/surface touch/UI suites。Expected: all pass, no duplicate event subscription。

- [ ] **Step 5: Stop for Task review without commit**

### Task 9: Phase 8 Assets, Validation Scene and MainCafe Wiring

**Files:**

- Create: `Assets/Editor/Phase8/Phase8AssetPaths.cs`
- Create: `Assets/Editor/Phase8/Phase8AssetBuilder.cs`
- Create: `Assets/Editor/Phase8/Phase8SceneSetup.cs`
- Create: `Assets/Editor/Phase8/Phase8Validator.cs`
- Create: `Assets/Scenes/Validation/Phase8FunctionalFurniture.unity`
- Modify: `Assets/Scenes/MainCafe.unity`
- Create/modify generated Phase 8 prefabs, materials, sprites and `.meta` files under `Assets/UI/Phase8/`
- Test: `Assets/Tests/EditMode/Phase8/Phase8AssetBuilderTests.cs`
- Test: `Assets/Tests/EditMode/Phase8/Phase8ValidatorTests.cs`
- Test: `Assets/Tests/EditMode/Phase8/Phase8MainCafeMigrationTests.cs`
- Test: `Assets/Tests/PlayMode/EditorSceneLoading/Phase8MainCafeSceneTests.cs`
- Test: `Assets/Tests/PlayMode/EditorSceneLoading/Phase8ValidationScenePlayModeTests.cs`

**Interfaces:**

```csharp
public static class Phase8AssetBuilder
{
    [MenuItem("Tools/AnimalCafe/Phase 8/Build Assets")]
    public static void BuildAssets();
}

public static class Phase8SceneSetup
{
    [MenuItem("Tools/AnimalCafe/Phase 8/Configure Validation Scene")]
    public static void ConfigureValidationScene();
    [MenuItem("Tools/AnimalCafe/Phase 8/Configure MainCafe")]
    public static void ConfigureMainCafe();
}

public static class Phase8Validator
{
    public static Phase8ValidationReport ValidateOpenScene();
}
```

- Builders and setup are idempotent; run twice yields one runtime owner、one of each registry/view/UI root and stable asset references。
- MainCafe uses production CR / CM definitions already created in Phase 4; no replacement models。

- [ ] **Step 1: Write failing asset, validator, migration and real-Scene tests**

Cover `P8-P-R-002`、Scene wiring、production definitions、Catalogue rows、indicator/debug boundaries and domain/view counts。

- [ ] **Step 2: Run focused RED before generating assets**

Run Phase 8 builder/validator/migration filters。Expected: paths/assets/components missing, not unrelated fixture failure。

- [ ] **Step 3: Implement builders and run them through Unity Editor tooling**

Use builder APIs for Scene/prefab serialization; do not hand-edit `.unity` or `.prefab` YAML。Run Build Assets, Configure Validation Scene and Configure MainCafe twice each。

- [ ] **Step 4: Run focused GREEN and Phase 6/7 MainCafe regressions**

Run Phase 8 EditMode + EditorSceneLoading filters, then Phase 6/7 MainCafe migration and Scene tests。Expected: all pass; validator issues `0`。

- [ ] **Step 5: Stop for Task review without commit**

### Task 10: Phase Verification, Reviews and Studio Owner Manual Gate

**2026-09-21 closeout authority：** Owner 已确认朋友 review 完成并明确授权 merge / Phase 8 completion / post-merge / local cleanup；merge commit `8ee0026247c2c3200bd1bc9ac471b7007e4a4f32`。最终 regression 和清理结果见 Beginner Guide 第 37 节；下面的 2026-09-04 表格及 manual gate 描述为历史。

> **Current evidence status (2026-09-04):** Task 10 / Phase 8 are `In Progress`. Automated Verification、Engineering、QA 与 Production final review 均为 `PASS`；Studio Owner manual `P8-M-001–P8-M-018` 全部 `Pending / 未执行`，是唯一下一道 gate。Do not mark Phase 8 `Completed` or advance to Phase 8R before manual acceptance passes.

### Finalized automation evidence

| Scope | Passed | Failed | Skipped | Inconclusive | Status |
|---|---:|---:|---:|---:|---|
| Phase 8 focused EditMode | 163 | 0 | 0 | 0 | PASS |
| Phase 8 focused core PlayMode | 58 | 0 | 0 | 0 | PASS |
| Phase 8 focused EditorSceneLoading | 7 | 0 | 0 | 0 | PASS |
| Full EditMode | 1637 | 0 | 0 | 0 | PASS |
| Full core PlayMode | 624 | 0 | 0 | 0 | PASS |
| Full EditorSceneLoading | 79 | 0 | 0 | 0 | PASS |
| Full real StandaloneWindows64 | 596 | 0 | 0 | 0 | PASS |

The finalized XML/log evidence is recorded in `.superpowers/sdd/2026-09-02-phase-8-functional-furniture-layout-readiness/task-10-report.md`. Historical RED/interrupted attempts remain diagnostic evidence only and do not alter this finalized automation result.

**Files:**

- Create: `Docs/Phase8_Beginner_Guide.md`
- Modify: `Docs/AnimalCafe_Development_Roadmap.md`
- Modify: `Docs/superpowers/plans/2026-09-02-phase-8-functional-furniture-layout-readiness.md`
- Record results in: `Docs/superpowers/specs/2026-09-02-phase-8-functional-furniture-layout-readiness-test-cases.md`

**Interfaces:**

- No new production API. This Task closes evidence only after implementation is feature-complete。
- Beginner Guide explains goal、before/after、files、tests、manual steps、known limitations and next Phase 8R gate in middle-school-readable Chinese。

- [x] **Step 1: Run complete Phase 8 focused suites**

Run all tests under `Assets/Tests/EditMode/Phase8/`、Phase 8 PlayMode and Phase 8 EditorSceneLoading。Record exact passed/failed/skipped/inconclusive counts and result locations。

- [x] **Step 2: Run fresh full EditMode regression**

Run the full EditMode assembly once from the Phase 8 worktree using Unity `6000.5.5f1`。Expected: zero failed/skipped/inconclusive；record actual total rather than predicting it。

- [x] **Step 3: Run fresh full PlayMode regression**

Run the full PlayMode and required EditorSceneLoading assemblies serially。Expected: zero failed/skipped/inconclusive；separate test-runner/fixture failures from production failures。

- [x] **Step 4: Complete Engineering, QA and Production review**

Engineering checks architecture、atomicity、Scene wiring、performance and compatibility. QA checks P8-E/P cases、direct regression、manual readiness and accessibility. Critical/Important block; Minor moves to Phase 8R/polish backlog unless it affects acceptance。

Current status：Engineering final review `PASS`（`0/0/0`）；QA focused re-review `PASS`（`0/0/0`，P8-M-018 Minor resolved）；Production focused re-review `PASS`（`0/0/0`）。Studio Owner manual `P8-M-001–P8-M-018` 是唯一下一道 gate。

- [x] **Step 5: Prepare manual review state and Beginner Guide**

Provide exact Unity Hub project path、Unity version、Scene/menu paths and P8-M-001–P8-M-018 steps. Prepare only representative screenshots needed to find UI/indicators/debug state。

- [x] **Step 6: Record Studio Owner manual decisions and approved M18 exception**

Do not mark Manual PASS from automated evidence. Record each result; visual acceptance for Pick-up indicator and Catalogue remains Pending until the Studio Owner reviews it。

执行记录：2026-09-08 M1–M17 Owner PASS；M18 按 Owner 授权由 Codex 技术代测 PASS，不能称为 18 项 Owner 亲测。2026-09-21 Owner 确认朋友 review 后作出整体完成决定；不追补未执行的逐项结果，不声称 mobile device acceptance。

- [x] **Step 7: Fix accepted blocking findings with focused RED/GREEN**

For each Critical/Important finding, diagnose first, add/adjust a focused regression test, implement the smallest fix, rerun affected suites and reviewer re-check only the original finding plus impact boundary。

- [x] **Step 8: Run final fresh full regression after fixes**

Required only if production code/assets changed after Steps 2–3. Record final exact counts and known limitations。

- [x] **Step 9: Update Roadmap and guide only after all gates pass**

Mark Phase 8 Completed only when automated、review and Studio Owner manual gates all pass. Set Current Next Step to `Phase 8R — Decoration & Functional Layout Review & Polish` design gate；do not define the deferred Phase 8R detail items here。

2026-09-21：按 Owner 整体收尾批准与已记录的 M18 技术代测例外，Phase 8 已标记 Completed。PR #7 merge 后 fresh 回归覆盖 2110 个不同 EditMode 用例，PlayMode 1015 PASS / 0 FAIL / 2 opt-in screenshot SKIP；所有 inconclusive 为 0。旧 worktree 资料校验归档、本地 main 同步及本地 branch/worktree 清理完成；不补造历史未覆盖子项或手机验收结果。Current Next Step 为等待 Owner 确认独立 Phase 8R design gate 的具体范围。

- [x] **Step 10: Request separate version-control decision**

Present changed files、tests、manual evidence、known limitations and suggested commit/PR text. Do not commit、push、open PR、merge or clean the branch/worktree without current Studio Owner authorization。

2026-09-21 Owner 已明确授权 PR #7 merge、完成记录及 post-merge 同步、归档后清理本地 Phase 8 branch；不授权删除远端 branch 或自动开始下一 Phase。

## Plan Self-review Checklist

- [x] Every confirmed design section maps to at least one Task and test case。
- [x] Exact interface names are consistent across Tasks 1–10。
- [x] No Task implements NPC、Order、Queue、NavMesh、Save、store economy or Phase 8R polish。
- [x] Multiple functional points、automatic anchors、Pick-up no-Rotate、occupied-support Store block and atomic recovery are explicitly covered。
- [x] Automated verification and P8-M-001–P8-M-018 remain separate gates。
- [x] Branch/worktree/GitHub operations occur only after plan approval and under the stated authority gates。
