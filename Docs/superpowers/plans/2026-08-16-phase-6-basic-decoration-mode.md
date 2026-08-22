# AnimalCafe Phase 6 — Basic Decoration Mode Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在正式 `MainCafe` 中实现 mobile Touch-first 的基础 Decoration Mode，让玩家通过 Counter Catalogue 安全地放置、拖动、旋转、确认、取消和收起 Floor Furniture。

**Architecture:** `CafeLayout` 继续作为正式 data Source of Truth；新的纯 C# `DecorationSession` 只保存一个 active Preview，并在 `Confirm` 时调用 atomic Layout transaction。Scene representation、Preview/Grid visuals、Touch routing 和 uGUI views 通过小型 runtime components 连接；Phase 6 Editor builder 只负责 deterministic assets、Validation Scene 与 MainCafe migration。

**Tech Stack:** Unity `6000.5.5f1`、C#、uGUI、TextMeshPro、Unity Input System、NUnit EditMode / PlayMode、Phase 2 Layout、Phase 4 Furniture Content、Phase 5 UI Foundation。

## Global Constraints

- Source spec: `Docs/superpowers/specs/2026-08-16-phase-6-basic-decoration-mode-design.md`。
- Test source: `Docs/superpowers/specs/2026-08-16-phase-6-basic-decoration-mode-test-cases.md`。
- 正式 production scene 只使用 `Assets/Scenes/MainCafe.unity`；Validation Scene 不进入 player Build Settings。
- 正式 input 只按 mobile `Touch Input` 设计；Mouse 仅作 Unity Editor mapping。
- `Portrait 1080 × 1920` 为主要 reference；Landscape 只保证功能可用且无裁切。
- Preview 不修改正式 Layout；每件家具单独 `Confirm / Cancel`。
- Phase 6 Catalogue 只有 Counter `1 × 1`、`1 × 2`、`1 × 3`、`2 × 3`，无限使用，无价格、库存和解锁。
- Work Table 资产保留但不进入 Phase 6 Catalogue。
- Cash Register、Coffee Machine、Window、Wall、Surface Slot、Save / Load 不在本 Phase 实现。
- runtime assembly 不引用 `UnityEditor`。
- 按 TDD 执行：先观察正确 RED，再写 minimal implementation，再观察 GREEN。
- 不 commit、push、merge 或删除 branch / worktree；Studio Owner 使用 GitHub Desktop 管理版本控制。
- 每个 Task 完成后只提交 evidence 和 diff 给 Studio Owner review，不自动跨越下一 gate。

## Planned File Structure

### Runtime domain

- `Assets/Scripts/Layout/CafeLayout.cs` — 增加 non-mutating placement validation 与 atomic position + rotation update。
- `Assets/Scripts/Layout/LayoutBounds.cs` — 区分完整 `8 × 8` buildable bounds 与当前 unlocked regions。
- `Assets/Scripts/Layout/LayoutReservationType.cs` — 增加 generic blocked reservation，不改变 Entrance contract。
- `Assets/Scripts/Layout/PlacementResult.cs` — 区分 out-of-bounds、locked、blocked 与 Entrance failures。
- `Assets/Scripts/Decoration/DecorationSessionState.cs` — Decoration lifecycle states。
- `Assets/Scripts/Decoration/FurniturePlacementPreview.cs` — immutable active Preview snapshot。
- `Assets/Scripts/Decoration/DecorationSession.cs` — one-preview transaction owner；Begin / Move / Rotate / Confirm / Cancel / Store。
- `Assets/Scripts/Decoration/PlacementFeedbackMapper.cs` — Phase 2 failure reason → Phase 6 player-facing feedback key。

### Runtime content and scene

- `Assets/Scripts/Decoration/DecorationCatalogueAsset.cs` — feature catalogue，entry 引用 Definition + thumbnail，不复制 footprint。
- `Assets/Scripts/Decoration/FurnitureSceneRegistry.cs` — Instance ID → one formal Scene representation。
- `Assets/Scripts/Decoration/FurniturePreviewView.cs` — suspended Preview transform / material state。
- `Assets/Scripts/Decoration/GridHighlightView.cs` — subtle `8 × 8` Grid 与 active footprint cells。

### Runtime input and orchestration

- `Assets/Scripts/Decoration/Input/DecorationTouchFrame.cs` — deterministic Touch snapshot。
- `Assets/Scripts/Decoration/Input/IDecorationTouchSource.cs` — testable input interface。
- `Assets/Scripts/Decoration/Input/InputSystemDecorationTouchSource.cs` — Input System Touch adapter。
- `Assets/Scripts/Decoration/Input/DecorationTouchRouter.cs` — UI / Furniture / Camera gesture ownership。
- `Assets/Scripts/Decoration/DecorationCameraDriver.cs` — Scene pan、Pinch zoom、edge auto-pan requests。
- `Assets/Scripts/Decoration/CafeLayoutRuntime.cs` — 构建并持有本次运行的 `8 × 8 CafeLayout`、Entrance reservation 与 initial furniture。
- `Assets/Scripts/Decoration/DecorationModeController.cs` — lifecycle、raycast、session、views、Pause 与 UI integration owner。

### Runtime UI

- `Assets/Scripts/UI/Decoration/DecorationCatalogueView.cs` — tile binding / selection。
- `Assets/Scripts/UI/Decoration/DecorationCatalogueTileView.cs` — thumbnail、name、size label、disabled state。
- `Assets/Scripts/UI/Decoration/DecorationActionBarView.cs` — Store / Rotate / Cancel / Confirm。
- `Assets/Scripts/UI/Decoration/DecorationStoreModalView.cs` — Store confirmation presentation、one-shot confirm and explicit dismissal events。

### Editor assets and integration

- `Assets/Editor/Phase6/Phase6DecorationAssetPaths.cs` — canonical Phase 6 paths。
- `Assets/Editor/Phase6/Phase6DecorationAssetBuilder.cs` — preset Prefabs、Definitions、Catalogue、thumbnails、UI assets。
- `Assets/Editor/Phase6/Phase6DecorationSceneSetup.cs` — idempotent Validation Scene + MainCafe migration。
- `Assets/Editor/Phase6/Phase6DecorationValidator.cs` — assets、scene、Build Settings、runtime assembly contracts。
- `Assets/Scenes/Validation/Phase6DecorationMode.unity` — boundary / Touch / multi-cell validation scene。
- `Assets/Art/Phase6/Definitions/FD_CounterPreset_1x2.asset`、`Assets/Art/Phase6/Definitions/FD_CounterPreset_1x3.asset`、`Assets/Art/Phase6/Definitions/FD_CounterPreset_2x3.asset` — player-visible placeholder Definitions；`1x1` 复用 Phase 4 Definition。
- `Assets/Art/Phase6/Catalogues/FC_Phase6Production.asset` — Phase 4 production content + Phase 6 Counter presets 的 runtime lookup catalogue。
- `Assets/Art/Phase6/Prefabs/PF_CounterPreset_1x2.prefab`、`Assets/Art/Phase6/Prefabs/PF_CounterPreset_1x3.prefab`、`Assets/Art/Phase6/Prefabs/PF_CounterPreset_2x3.prefab` — one-root multi-cell presets。
- `Assets/UI/Phase6/Thumbnails/*.png` — deterministic transparent thumbnails。
- `Assets/UI/Phase6/Prefabs/PF_UI_DecorationCatalogue.prefab`、`PF_UI_DecorationActionBar.prefab`、`PF_UI_DecorationStoreModal.prefab` — Phase 5-based feature UI。

### Tests

- `Assets/Tests/EditMode/Phase6/CafeLayoutPreviewValidationTests.cs`
- `Assets/Tests/EditMode/Phase6/DecorationSessionTests.cs`
- `Assets/Tests/EditMode/Phase6/DecorationCatalogueTests.cs`
- `Assets/Tests/EditMode/Phase6/Phase6DecorationAssetBuilderTests.cs`
- `Assets/Tests/EditMode/Phase6/Phase6DecorationValidatorTests.cs`
- `Assets/Tests/EditMode/Phase6/Phase6MainCafeMigrationTests.cs`
- `Assets/Tests/PlayMode/Phase6DecorationScenePlayModeTests.cs`
- `Assets/Tests/PlayMode/Phase6DecorationTouchPlayModeTests.cs`
- `Assets/Tests/PlayMode/Phase6DecorationUiPlayModeTests.cs`
- `Assets/Tests/PlayMode/EditorSceneLoading/Phase6DecorationRealTouchTests.cs`

---

### Task 1: Non-mutating Layout validation and atomic update

**Files:**
- Modify: `Assets/Scripts/Layout/CafeLayout.cs`
- Create: `Assets/Scripts/Layout/LayoutBounds.cs`
- Modify: `Assets/Scripts/Layout/LayoutReservationType.cs`
- Modify: `Assets/Scripts/Layout/PlacementResult.cs`
- Test: `Assets/Tests/EditMode/Phase6/CafeLayoutPreviewValidationTests.cs`
- Regression test: `Assets/Tests/EditMode/CafeLayoutTests.cs`
- Regression test: `Assets/Tests/EditMode/Phase4/LayoutReservationTests.cs`

**Interfaces:**
- Consumes: existing `FurnitureDefinitionCatalog`、`GridPosition`、`FurnitureRotation`、`PlacementResult`。
- Produces:

```csharp
public PlacementResult ValidateFurniturePlacement(
    string definitionId,
    GridPosition position,
    FurnitureRotation rotation,
    string ignoredInstanceId = null);

public PlacementResult UpdateFurniturePlacement(
    string instanceId,
    GridPosition position,
    FurnitureRotation rotation);

public IReadOnlyList<GridPosition> GetFurnitureFootprintCells(
    string definitionId,
    GridPosition position,
    FurnitureRotation rotation);

public readonly struct LayoutBounds
{
    public GridPosition Origin { get; }
    public GridSize Size { get; }
    public bool Contains(GridPosition position);
}
```

- `ValidateFurniturePlacement` 不修改 `FurnitureInstances`、`occupantByCell` 或 `OccupiedCellCount`。
- `UpdateFurniturePlacement` 在一次成功 operation 中同时更新 position + rotation，并保留 Instance ID。
- 新 constructor overload `CafeLayout(GridSettings, FurnitureDefinitionCatalog, LayoutBounds)` 用于 Phase 6 `8 × 8`；旧 constructor 保持已有 tests / consumers 的 compatibility。
- failure ordering 固定为 `OutOfLayoutBounds` → `LockedCell` → reservation (`ReservedEntranceClearance` / `Blocked`) → `Overlap`。

- [ ] **Step 1: Add test folder metadata through Unity and write failing validation tests**

覆盖 `P6-GRID-001–009`、`P6-VAL-001–008`、`P6-PRV-010`。增加一个 `8 × 8 LayoutBounds`、部分 unlocked region、generic blocked reservation 与 Entrance reservation，证明四类 reason 可以稳定区分。核心 assertions：

```csharp
var beforeCount = layout.OccupiedCellCount;
var result = layout.ValidateFurniturePlacement(
    "counter.preset.2x3",
    new GridPosition(2, 2),
    FurnitureRotation.Degrees90);

Assert.That(result.Succeeded, Is.True);
Assert.That(layout.OccupiedCellCount, Is.EqualTo(beforeCount));
Assert.That(layout.FurnitureInstances, Is.Empty);
Assert.That(
    layout.GetFurnitureFootprintCells(
        "counter.preset.2x3",
        new GridPosition(2, 2),
        FurnitureRotation.Degrees90),
    Has.Count.EqualTo(6));
```

- [ ] **Step 2: Run focused EditMode test and observe correct RED**

Run Unity EditMode filter:

```text
AnimalCafe.Tests.Phase6.CafeLayoutPreviewValidationTests
```

Expected RED: compile failure because the three planned `CafeLayout` methods do not exist。

- [ ] **Step 3: Implement shared candidate-cell validation**

Refactor the existing private cell loop into the public non-mutating entry point。`ignoredInstanceId` 必须先验证：null 表示 new placement；非 null 必须是有效 Instance ID 且必须存在，否则返回 `InstanceNotFound`。`GetFurnitureFootprintCells` 返回 read-only cells and throws only for unknown Definition / invalid rotation，不能暴露 mutable internal collections。Inside `LayoutBounds` but outside every unlocked region returns `LockedCell`；outside bounds returns `OutOfLayoutBounds`。`LayoutReservationType.Blocked` returns `Blocked`，Entrance keeps `ReservedEntranceClearance`。

- [ ] **Step 4: Implement atomic update**

`MoveFurniture` 和 `RotateFurniture` 保持 compatibility，并委托给 `UpdateFurniturePlacement`：

```csharp
public PlacementResult UpdateFurniturePlacement(
    string instanceId,
    GridPosition position,
    FurnitureRotation rotation)
{
    ValidateInstanceId(instanceId);
    FurnitureInstance.ValidateRotation(rotation);
    if (!furnitureInstancesById.TryGetValue(instanceId, out var current))
        return PlacementResult.Failure(PlacementFailureReason.InstanceNotFound);

    return ReplaceFurniturePlacement(current, position, rotation);
}
```

- [ ] **Step 5: Run focused GREEN and existing Layout regression**

Run new filter plus existing `CafeLayoutTests`、`GridPlacementTests`、`FurnitureInstanceTests`。Expected: all passed；failed / skipped / inconclusive `0`。

- [ ] **Step 6: Review checkpoint**

Report exact methods、test counts and diff。Do not begin Task 2 until Layout API review passes。

---

### Task 2: Decoration transaction domain

**Files:**
- Create: `Assets/Scripts/Decoration/DecorationSessionState.cs`
- Create: `Assets/Scripts/Decoration/FurniturePlacementPreview.cs`
- Create: `Assets/Scripts/Decoration/DecorationSession.cs`
- Create: `Assets/Scripts/Decoration/PlacementFeedbackMapper.cs`
- Test: `Assets/Tests/EditMode/Phase6/DecorationSessionTests.cs`

**Interfaces:**

```csharp
public enum DecorationSessionState
{
    Closed,
    BrowsingCatalogue,
    PreviewingNewFurniture,
    EditingExistingFurniture,
    ConfirmingStore
}

public sealed class FurniturePlacementPreview
{
    public string DefinitionId { get; }
    public string SourceInstanceId { get; }
    public GridPosition OriginalPosition { get; }
    public FurnitureRotation OriginalRotation { get; }
    public GridPosition ProposedPosition { get; }
    public FurnitureRotation ProposedRotation { get; }
    public PlacementResult PlacementResult { get; }
    public bool IsNew => SourceInstanceId == null;
}

public sealed class DecorationSession
{
    public DecorationSessionState State { get; }
    public FurniturePlacementPreview ActivePreview { get; }
    public void Enter();
    public void Exit();
    public void BeginNew(string definitionId, GridPosition position);
    public PlacementResult BeginExisting(string instanceId);
    public PlacementResult MovePreview(GridPosition position);
    public PlacementResult RotatePreview();
    public PlacementResult ConfirmPreview();
    public void CancelPreview();
    public bool BeginStoreConfirmation();
    public void DismissStoreConfirmation();
    public PlacementResult ConfirmStore();
}
```

- [ ] **Step 1: Write failing state and transaction tests**

Map `P6-LC-001–005`、`P6-PRV-003–008`、`P6-TXN-001–012`。Test exact state after every method；assert Layout remains unchanged before Confirm；assert double Confirm / Store is idempotent。

- [ ] **Step 2: Run focused test and observe correct RED**

Filter `AnimalCafe.Tests.Phase6.DecorationSessionTests`。Expected RED: missing types。

- [ ] **Step 3: Implement immutable Preview replacement**

Every move / rotation creates a new `FurniturePlacementPreview` with the latest result。`BeginExisting` reads current Layout snapshot；`BeginNew` validates without creating an Instance ID。

- [ ] **Step 4: Implement one-preview state machine**

`BeginNew` / `BeginExisting` first call `CancelPreview` when another Preview exists。`Exit` calls `CancelPreview` and ends in `Closed`。Blank taps remain controller behavior and do not add a domain transition。

- [ ] **Step 5: Implement atomic Confirm and Store**

- new Confirm: `FurnitureInstance.CreateNew` then `CafeLayout.PlaceFurniture`；
- existing Confirm: `CafeLayout.UpdateFurniturePlacement`；
- Store confirm: `CafeLayout.RemoveFurniture` only from `ConfirmingStore`；
- success returns to `BrowsingCatalogue`；failure retains active Preview and specific result；
- a second request after success performs no additional mutation。

- [ ] **Step 6: Map placement reasons without UI legality duplication**

`PlacementFeedbackMapper` returns stable keys：

```csharp
public enum PlacementFeedbackKey
{
    None,
    Occupied,
    OutsideUnlockedArea,
    Locked,
    EntranceClearance,
    UnsupportedSurface,
    MissingInstance
}
```

The mapper switches only on `PlacementFailureReason`；it does not inspect Grid cells。

- [ ] **Step 7: Run focused GREEN and Task 1 regression**

Expected: all Task 1–2 focused EditMode tests pass；no Layout mutation before Confirm。

- [ ] **Step 8: Review checkpoint**

Review state chart、public interfaces、double-submit evidence and absence of UnityEngine dependencies in pure domain files。

---

### Task 3: Phase 6 Catalogue data, presets and deterministic thumbnails

**Files:**
- Create: `Assets/Scripts/Decoration/DecorationCatalogueAsset.cs`
- Create: `Assets/Editor/Phase6/Phase6DecorationAssetPaths.cs`
- Create: `Assets/Editor/Phase6/Phase6DecorationAssetBuilder.cs`
- Create assets under `Assets/Art/Phase6/Definitions/`、`Assets/Art/Phase6/Prefabs/`、`Assets/UI/Phase6/Thumbnails/`
- Test: `Assets/Tests/EditMode/Phase6/DecorationCatalogueTests.cs`
- Test: `Assets/Tests/EditMode/Phase6/Phase6DecorationAssetBuilderTests.cs`

**Interfaces:**

```csharp
[Serializable]
public sealed class DecorationCatalogueEntry
{
    [SerializeField] private FurnitureDefinitionAsset definition;
    [SerializeField] private Sprite thumbnail;
    public FurnitureDefinitionAsset Definition => definition;
    public Sprite Thumbnail => thumbnail;
}

[CreateAssetMenu(menuName = "AnimalCafe/Decoration Catalogue")]
public sealed class DecorationCatalogueAsset : ScriptableObject
{
    [SerializeField] private List<DecorationCatalogueEntry> entries;
    public IReadOnlyList<DecorationCatalogueEntry> Entries => entries;
}
```

- [ ] **Step 1: Write failing Catalogue and builder tests**

Cover `P6-CAT-001–012`。Assert exact approved definitions；Work Table and non-Floor assets absent；thumbnail non-null；duplicate IDs rejected；builder twice preserves GUIDs and entry count。

- [ ] **Step 2: Run focused RED**

Expected RED: missing Catalogue types and Phase 6 assets。

- [ ] **Step 3: Implement runtime Catalogue references**

Entries only reference `FurnitureDefinitionAsset` and `Sprite`。Tile size must read `Definition.FootprintWidth / FootprintDepth`；no serialized duplicate footprint or inventory field。

- [ ] **Step 4: Build multi-cell Prefabs without stretching**

Create one root per preset with child instances of the approved Counter model visual：2 children for `1 × 2`、3 for `1 × 3`、6 for `2 × 3`。Root transform scale `1,1,1`；definition footprints exact；placement surface Floor；function None。Inspect `PF_Validation_Counter_1x3_01` first and reuse its contract when valid。

- [ ] **Step 5: Generate thumbnails deterministically**

Editor builder uses one hidden preview Scene、orthographic Camera、transparent target、fixed lighting、fixed framing derived from renderer bounds and fixed output dimensions。PNG import settings set `TextureType.Sprite`、alpha enabled、no mipmaps。Second run overwrites bytes at the same paths and keeps `.meta` GUIDs。

- [ ] **Step 6: Build Phase 6 Catalogue asset**

Order Decoration entries exactly `1 × 1`、`1 × 2`、`1 × 3`、`2 × 3`。Reuse Phase 4 `FD_Furniture_CounterModule_01.asset` for `1 × 1`；do not copy or modify Work Table Definition。Build `FC_Phase6Production.asset` as the complete runtime lookup catalogue containing the existing Phase 4 production Definitions plus the three new Counter Definitions；the Decoration UI still exposes only its four approved entries。

- [ ] **Step 7: Run builder twice, then focused GREEN and Phase 4 validators**

Expected: deterministic assets、four entries、no duplicate IDs、Phase 4 production validator still green。

- [ ] **Step 8: Visual checkpoint**

Show all four thumbnails side-by-side and Prefab bounds / footprint overlay。Studio Owner checks recognizable shape、consistent framing and absence of stretched models。

---

### Task 4: Scene registry, Preview and Grid highlight

**Files:**
- Create: `Assets/Scripts/Decoration/DecorationGridSpace.cs`
- Create: `Assets/Scripts/Decoration/FurnitureSceneRegistry.cs`
- Create: `Assets/Scripts/Decoration/FurniturePreviewView.cs`
- Create: `Assets/Scripts/Decoration/GridHighlightView.cs`
- Modify: `Assets/Scripts/Content/FurnitureContentCatalog.cs`
- Test: `Assets/Tests/PlayMode/Phase6DecorationScenePlayModeTests.cs`

**Interfaces:**

```csharp
public readonly struct DecorationGridSpace
{
    public GridSettings Settings { get; }
    public LayoutBounds Bounds { get; }
    public DecorationGridSpace(GridSettings settings, LayoutBounds bounds);
    public Vector3 GetCellCenterLocal(GridPosition cell, float height = 0f);
    public Vector3 GetFootprintCenterLocal(
        IReadOnlyList<GridPosition> cells,
        float height = 0f);
    public Quaternion GetLocalRotation(FurnitureRotation rotation);
}

public sealed class FurnitureSceneRegistry : MonoBehaviour
{
    public void Configure(
        FurnitureContentCatalog contentCatalog,
        Transform root,
        DecorationGridSpace gridSpace);
    public void Rebuild(IReadOnlyList<FurnitureInstance> instances);
    public bool TryGet(string instanceId, out GameObject representation);
    public bool SetRepresentationVisible(string instanceId, bool visible);
    public bool TryGetInstanceId(Component hitComponent, out string instanceId);
    public void Remove(string instanceId);
    public IReadOnlyList<FurnitureSceneIssue> LastIssues { get; }
}

public sealed class FurniturePreviewView : MonoBehaviour
{
    public void Configure(
        Transform root,
        DecorationGridSpace gridSpace,
        AnimalCafeUiTheme theme);
    public void Show(GameObject prefab, IReadOnlyList<GridPosition> cells);
    public void SetPlacement(
        IReadOnlyList<GridPosition> currentCells,
        FurnitureRotation rotation,
        float hoverHeight);
    public void SetValidity(bool valid);
    public void Hide();
}

public sealed class GridHighlightView : MonoBehaviour
{
    public void Configure(
        Transform root,
        DecorationGridSpace gridSpace,
        Material materialTemplate,
        AnimalCafeUiTheme theme);
    public void ShowGrid(GridSettings settings);
    public void ShowFootprint(IReadOnlyList<GridPosition> cells, bool valid);
    public void ClearFootprint();
    public void HideGrid();
}
```

- [ ] **Step 1: Write failing Scene sync and visual lifecycle tests**

Cover the Task 4 component-owned slices of `P6-SYNC-001–009`、`P6-GRID-010`、`P6-VAL-001–010`。Use lightweight runtime materials created by test fixture；assert one representation per Instance ID、correct southwest-origin Grid-to-world mapping、multi-cell pivot centering、current-cell rotation updates and all visuals clear on disable。Controller / UI / MainCafe-owned completion timing、copy、Confirm state and production lifecycle remain explicitly owned by Tasks 6–8；Task 4 must not claim those deferred slices as complete。

- [ ] **Step 2: Run focused PlayMode RED**

Expected RED: missing Scene view components。

- [ ] **Step 3: Implement idempotent registry**

Add a read-only `FurnitureContentCatalog.TryGetDefinitionAsset` lookup without changing its existing atomic `BuildRuntimeCatalog` contract。`Rebuild` removes representations for absent IDs、updates matching IDs and creates missing IDs per Definition asset。Missing Definition / Prefab and duplicate Instance ID each produce one specific structured recoverable issue；other valid Instances continue rebuilding and no empty permanent object is created。Reverse lookup maps a hit child Component back to its formal Instance ID for Task 7 selection。

- [ ] **Step 4: Implement Preview transform and hover state**

Preview is a separately owned clone with colliders / selection hooks disabled。Every placement update receives the current footprint cells so existing rotated furniture and later rotations keep the centered prefab pivot without recreating the clone。Rotation uses exact `FurnitureRotation` mapping；vertical hover is presentation-only；Grid position remains the transaction coordinate。

- [ ] **Step 5: Implement Grid and footprint visuals**

Create pooled cell visuals for exact `8 × 8` Grid plus a separate reusable footprint pool that can remain visible outside bounds。The configured roots represent the Layout southwest corner；for the centered Phase 4 Floor, Task 8 places them at local `(-4, 0, -4)` with unit scale。Active cells use Phase 5 `Accent` / `Destructive` theme colors plus distinct non-color geometry marks；all generated visuals have no enabled Collider。Use an injected world-material template with `MaterialPropertyBlock` overrides；do not mutate Phase 4 Floor / Grid meshes or shared material assets。

- [ ] **Step 6: Run focused GREEN and interruption cases**

Disable registry / Preview / Grid roots during active state。Expected: no stale objects、no duplicated cells after re-enable。

- [ ] **Step 7: Visual checkpoint**

Capture `1 × 1`、`1 × 3`、`2 × 3` at valid and invalid positions in Portrait framing for Studio Owner review。

---

### Task 5: Mobile Touch routing, Pinch and edge auto-pan

**Files:**
- Create: `Assets/Scripts/Decoration/Input/DecorationTouchFrame.cs`
- Create: `Assets/Scripts/Decoration/Input/IDecorationTouchSource.cs`
- Create: `Assets/Scripts/Decoration/Input/InputSystemDecorationTouchSource.cs`
- Create: `Assets/Scripts/Decoration/Input/DecorationTouchRouter.cs`
- Create: `Assets/Scripts/Decoration/DecorationCameraDriver.cs`
- Test: `Assets/Tests/PlayMode/Phase6DecorationTouchPlayModeTests.cs`

**Interfaces:**

```csharp
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

public enum DecorationGestureOwner { None, Ui, Furniture, Camera, Pinch }
public enum DecorationTouchHitKind { None, Ui, Furniture, Scene }

public readonly struct DecorationTouchPoint
{
    public DecorationTouchPoint(
        int touchId,
        Vector2 position,
        Vector2 delta,
        InputTouchPhase phase);
    public int TouchId { get; }
    public Vector2 Position { get; }
    public Vector2 Delta { get; }
    public InputTouchPhase Phase { get; }
    public bool IsActive { get; }
    public bool IsTerminal { get; }
}

// Current-frame-only immutable view over a source-owned reusable buffer.
public readonly ref struct DecorationTouchFrame
{
    public DecorationTouchFrame(
        int frameNumber,
        ReadOnlySpan<DecorationTouchPoint> touches);
    public int FrameNumber { get; }
    public ReadOnlySpan<DecorationTouchPoint> Touches { get; }
    public int ActiveTouchCount { get; }
}

public interface IDecorationTouchSource
{
    DecorationTouchFrame ReadFrame();
}

public sealed class InputSystemDecorationTouchSource :
    MonoBehaviour,
    IDecorationTouchSource
{
    public DecorationTouchFrame ReadFrame();
}

public readonly struct DecorationTouchHit
{
    public DecorationTouchHit(
        DecorationTouchHitKind kind,
        string furnitureInstanceId = null);
    public DecorationTouchHitKind Kind { get; }
    public string FurnitureInstanceId { get; }
}

public interface IDecorationTouchHitClassifier
{
    DecorationTouchHit ClassifyBegan(int touchId, Vector2 screenPosition);
}

public readonly struct DecorationTouchRoutingResult
{
    public DecorationGestureOwner Owner { get; }
    public DecorationTouchHit OriginHit { get; }
    public bool TapReleased { get; }
    public bool FurnitureDragRequested { get; }
    public Vector2 FurnitureDragScreenPosition { get; }
    public bool CameraPanRequested { get; }
    public Vector2 CameraPanDelta { get; }
    public bool PinchZoomRequested { get; }
    public float PinchDistanceDelta { get; }
}

public sealed class DecorationTouchRouter
{
    public const int NoTouchId = -1;
    public DecorationTouchRouter(
        float dragThresholdPixels,
        float furnitureDragOffsetPixels);
    public DecorationGestureOwner Owner { get; }
    public int PrimaryTouchId { get; }
    public int SecondaryTouchId { get; }
    public bool IsDragging { get; }
    public bool IsSuppressingUntilAllTouchesUp { get; }
    public DecorationTouchRoutingResult ProcessFrame(
        DecorationTouchFrame frame,
        IDecorationTouchHitClassifier hitClassifier);
    public void Reset();
}

public sealed class DecorationCameraDriver : MonoBehaviour
{
    public bool IsEdgeAutoPanning { get; }
    public float EdgeZonePixels { get; set; }
    public AnimationCurve NormalizedSpeedCurve { get; set; }
    public float MaxEdgeSpeedPixelsPerSecond { get; set; }
    public void Configure(CafeCameraController cameraController);
    public void ApplyScenePan(Vector2 screenDelta);
    public void ApplyPinchZoom(float pinchDistanceDelta);
    public Vector2 ApplyFurnitureEdgeAutoPan(
        DecorationGestureOwner owner,
        bool isDragging,
        Vector2 pointerPosition,
        Rect cameraPixelRect,
        Rect safeArea,
        bool isOverExcludedUiOrModal);
    public void StopEdgeAutoPan();
    public static Vector2 CalculateEdgeAutoPanScreenDelta(
        Rect cameraPixelRect,
        Rect safeArea,
        Vector2 pointerPosition,
        float edgeZonePixels,
        AnimationCurve normalizedSpeedCurve,
        float maxSpeedPixelsPerSecond,
        float unscaledDeltaTime);
}
```

- [ ] **Step 1: Write failing ownership tests**

Cover the Task 5 contract-level slices of `P6-IN-001–015`。Assert the primary `Began` is classified exactly once；a `None` hit suppresses ownership until all active Touches release；gesture origin owns until release；the maximum observed straight-line `Vector2.Distance(pressPosition, currentPosition) <= threshold` stays tap-eligible while the first value `> threshold` latches drag permanently；Furniture and Camera commands are mutually exclusive；a UI-owned primary never promotes to Pinch；two pointers、unknown terminal IDs and all `Ended` / `Canceled` orders clean independently；same/stale frame processing never repeats a command。Real EventSystem / Scene / transaction completion remains owned by Tasks 7 and 9 and must not be claimed here。

- [ ] **Step 2: Run focused RED**

Expected RED: missing Touch interfaces / router。

- [ ] **Step 3: Implement deterministic Touch snapshots**

Use Input System 1.19 `EnhancedTouch.Touch.activeTouches`, not direct `Touchscreen.current` polling。`InputSystemDecorationTouchSource.OnEnable` acquires exactly one balanced `EnhancedTouchSupport.Enable()` and `OnDisable` releases exactly that ownership。Copy `touchId`、phase、screen position and delta in source order once per `Time.frameCount` into a reusable buffer exposed as a current-frame `ReadOnlySpan`；preserve `Ended` / `Canceled` and filter only `None`。Do not use LINQ / `ToArray` / per-frame list wrappers、`Mouse.current` or `TouchSimulation` in production。Mouse remains only an injected Editor / test mapping through `IDecorationTouchSource`。

- [ ] **Step 4: Implement ownership router**

All transitions occur only through `ProcessFrame`。Owner is selected from one injected classifier at the primary `Began` and never reclassified while moving across UI / Scene。A primary `None` hit produces no owner/command and suppresses all other Touches until the screen is clear。Process each snapshot as a batch: resolve tracked terminal phases before new `Began`, then process new `Began` in snapshot/source-array order and calculate commands。Any distinct second Touch promotes an existing Furniture / Camera owner to `Pinch` regardless of its screen region because the approved first-down owner already owns the whole gesture；a UI-owned primary never promotes。Promotion latches the gesture as non-tap, freezes single-finger output and establishes the distance baseline without Confirm / Cancel。If the primary becomes terminal, `ActiveTouchCount > 0` always enters suppression and `ActiveTouchCount == 0` fully clears。If only the secondary is terminal, restore/rebase the primary first；a new second `Began` in that same snapshot may immediately establish a new zero-command Pinch baseline。Unknown/repeated terminal and `Reset` are idempotent。A duplicate or stale frame returns current state with no repeated command。

- [ ] **Step 5: Implement Camera driver**

`DecorationCameraDriver` has no `Update` and reads no input directly。It only delegates approved pan / pinch requests to `CafeCameraController.ApplyPan` / `ApplyZoom` so existing Camera bounds remain authoritative。Furniture-owned, threshold-latched drag may request edge auto-pan inside `camera.pixelRect ∩ Screen.safeArea` and outside Bottom Sheet / Modal / other UI hit regions。Use the injected normalized curve, clamp its output and cap the final vector magnitude at `maxSpeedPixelsPerSecond * unscaledDeltaTime`；empty intersections、zero delta time、wrong owner、UI / Modal、zone exit、release、disable、Cancel or Confirm stop immediately with no stored velocity or Coroutine。Tests assert final Camera world direction and existing bounds, not only raw delta signs。

- [ ] **Step 6: Add touch drag offset and snapping contract tests**

The router emits `FurnitureDragScreenPosition = primary.Position + Vector2.up * furnitureDragOffsetPixels` only after drag latches。Task 7 projects that visible coordinate to Floor and snaps it；Task 5 must not create Grid、raycasts、session mutations or Preview changes。

- [ ] **Step 7: Run focused GREEN and Phase 5 pointer regression**

Use pure router / Camera math tests plus isolated `InputTestFixture` tests with only the fixture-owned virtual `Touchscreen`。Prove same-frame source caching、real `touchId` / delta / phase、same-frame short Touch surfacing across EnhancedTouch polling frames、two stable IDs and balanced disable / enable without global Input System reset。Run the new focused filter, Phase 5 `UiPointerBoundaryTests` and `Phase5PointerBoundaryPlayModeTests`。Task 9 owns the real EventSystem + `InputSystemUIInputModule` suite and both suite-order runs。

- [ ] **Step 8: Tuning checkpoint**

Reuse `CameraSettings.DragThresholdPixels` as the single threshold source。Task 7 owns the serialized furniture drag offset passed to the router；`DecorationCameraDriver` owns serialized edge zone、normalized speed curve and maximum speed with safe minimum validation。Do not finalize any provisional value until Task 10 manual playtest。

---

### Task 6: Catalogue, action bar and Store Modal UI

**Files:**
- Create: `Assets/Scripts/UI/Decoration/DecorationCatalogueView.cs`
- Create: `Assets/Scripts/UI/Decoration/DecorationCatalogueTileView.cs`
- Create: `Assets/Scripts/UI/Decoration/DecorationActionBarView.cs`
- Create: `Assets/Scripts/UI/Decoration/DecorationStoreModalView.cs`
- Modify: `Assets/Editor/Phase6/Phase6DecorationAssetPaths.cs`
- Modify: `Assets/Editor/Phase6/Phase6DecorationAssetBuilder.cs`
- Modify: `Assets/Tests/EditMode/Phase6/Phase6DecorationAssetBuilderTests.cs`
- Create: `Assets/Tests/EditMode/Phase6/Phase6DecorationUiPrefabTests.cs`
- Create: `Assets/Tests/PlayMode/Phase6DecorationUiPlayModeTests.cs`
- Create through `Phase6DecorationAssetBuilder`: `Assets/UI/Phase6/Fonts/NotoSansSC-Phase6 SDF.asset`
- Create through `Phase6DecorationAssetBuilder`: `Assets/UI/Phase6/Prefabs/PF_UI_DecorationCatalogue.prefab`
- Create through `Phase6DecorationAssetBuilder`: `Assets/UI/Phase6/Prefabs/PF_UI_DecorationActionBar.prefab`
- Create through `Phase6DecorationAssetBuilder`: `Assets/UI/Phase6/Prefabs/PF_UI_DecorationStoreModal.prefab`
- Unity-generated `.meta` files for only the new Task 6 scripts、tests、folders and assets

**Interfaces:**

```csharp
public sealed class DecorationCatalogueView : MonoBehaviour
{
    public event Action<FurnitureDefinitionAsset> Selected;

    public bool IsCatalogueVisible { get; }
    public bool IsCollapsed { get; }

    public void Configure(
        IUiPointerOwnershipRegistrar pointerOwnership,
        UiTransitionRunner transitionRunner);
    public void Bind(DecorationCatalogueAsset catalogue);
    public void ShowCatalogue();
    public void ShowCollapsedHandle();
    public void Hide();
}

public sealed class DecorationCatalogueTileView : MonoBehaviour
{
    public FurnitureDefinitionAsset Definition { get; }
    public bool IsInteractable { get; }

    public void Configure(IUiPointerOwnershipRegistrar pointerOwnership);
    public void Bind(
        DecorationCatalogueEntry entry,
        Action<FurnitureDefinitionAsset> selected);
    public void Clear();
}

public sealed class DecorationActionBarView : MonoBehaviour
{
    public event Action RotateRequested;
    public event Action ConfirmRequested;
    public event Action CancelRequested;
    public event Action StoreRequested;

    public bool IsVisible { get; }

    public void Configure(
        IUiPointerOwnershipRegistrar pointerOwnership,
        UiTransitionRunner transitionRunner);
    public void Show(bool canStore, bool canConfirm, PlacementFeedbackKey feedback);
    public void Hide();
}

public sealed class DecorationStoreModalView : MonoBehaviour
{
    public event Action ConfirmRequested;
    public event Action DismissRequested;

    public bool IsOpen { get; }

    public void Configure(
        UiNavigationCoordinator navigation,
        UiPauseCoordinator pause,
        UiPointerBoundary pointerBoundary,
        UiTransitionRunner transitionRunner);
    public void Show(FurnitureDefinitionAsset definition);
    public bool TryHandleBack();
    public void CloseForOwnerShutdown();
}
```

No Task 6 binding/view-model layer is added. `DecorationCatalogueEntry` remains the exact Task 3 Definition + thumbnail contract；`FurnitureDefinitionAsset` remains the source for display name、footprint and Prefab validity；`PlacementFeedbackKey` remains the action-bar input. The views expose presentation events only，and Task 7 remains the single state/transaction coordinator.

**State-to-UI contract:**

| Domain/presentation state | Catalogue | Action bar | Store Modal | Required behavior |
| --- | --- | --- | --- | --- |
| enter / `BrowsingCatalogue` expanded | visible expanded `Furniture Catalogue` with four tiles | hidden | hidden | this is the initial and post-Confirm/post-Cancel state；no price、stock count or unlock badge |
| `BrowsingCatalogue` collapsed | only the partially exposed `Catalogue` handle | hidden | hidden | explicit handle tap expands；collapse is presentation state only，not a new `DecorationSessionState` |
| `PreviewingNewFurniture` | hidden | `Rotate / Cancel / Confirm`；Store GameObject inactive | hidden | `Confirm` is enabled only for `PlacementFeedbackKey.None`；drag release and tile selection never auto-Confirm |
| `EditingExistingFurniture` | hidden | `Store / Rotate / Cancel / Confirm` | hidden | Store is visible only with a stable existing Instance；Confirm remains explicit and legality-driven |
| `ConfirmingStore` | hidden | remains underneath but cannot receive input | blocking Modal visible | opening performs no removal；Modal buttons are `Cancel / Store` |
| Store Modal dismissed | hidden | restored to the same existing Preview | hidden | emit one `DismissRequested`；return to `EditingExistingFurniture` with no Layout、Preview、rotation or occupancy mutation |

Successful Confirm or Cancel reopens the expanded Catalogue exactly once. Cancel removes a new Preview or restores the existing Preview through `DecorationSession`；the UI performs no Layout mutation. Store confirm emits one request only；Task 7 calls the existing atomic domain operation. A new explicit selection emits once；Task 7 calls `BeginNew` / `BeginExisting` and the existing session auto-cancels the prior Preview before replacement.

**Canonical copy for Task 6 tests:**

- Approved English UI labels remain `Furniture Catalogue`、`Catalogue`、`Store`、`Rotate`、`Cancel` and `Confirm`；tile names come directly from the four canonical Definition display names and footprint labels use `1 × 1`、`1 × 2`、`1 × 3`、`2 × 3`.
- Approved example feedback is exact：`None = ""`；`Occupied = "这里已有家具"`；`OutsideUnlockedArea = "超出可装修区域"`；`Locked = "这个区域尚未解锁"`；`EntranceClearance = "入口区域不能放置家具"`. Current provisional canonical fallbacks are `Blocked = "这里不能放置家具"`；`UnsupportedSurface = "此处不支持落地家具"`；`MissingInstance = "家具状态已变化，请重新选择"`；Task 10 may polish only these provisional phrases without changing their keys/semantics.
- Store Modal provisional canonical copy is title `Store furniture?`，body `This removes it from the current layout. You can place it again from the catalogue.`，buttons `Cancel / Store`.
- Tests lock exact consistency for this implementation. Task 10 manual review may polish copy without changing transaction semantics；Task 6 does not invent price、inventory、unlock or sale language.

**Phase 5 reuse and compatibility boundary:**

- Reuse tokens from `Assets/Scripts/UI/Foundation/AnimalCafeUiTheme.cs` and `Assets/UI/Phase5/Theme/AnimalCafeUiTheme.asset`.
- Reuse unscaled/Reduced Motion behavior from `Assets/Scripts/UI/Foundation/UiTransitionRunner.cs`，localized wrapping from `Assets/Scripts/UI/Components/SafeAreaContainer.cs`，and ownership from `Assets/Scripts/UI/Foundation/UiPointerBoundary.cs`.
- Wrap `Assets/Scripts/UI/Components/AnimalCafeModalView.cs` and may use `Assets/UI/Phase5/Prefabs/PF_UI_Modal.prefab` as the visual/component source. `AnimalCafeModalView.Confirmed` fires before close and it exposes no dismiss event，so the Task 6 wrapper must latch before forwarding Confirm and own Cancel/Back dismissal notification.
- Do not attach `Assets/Scripts/UI/Components/AnimalCafeBottomSheetView.cs` or directly reuse `Assets/UI/Phase5/Prefabs/PF_UI_BottomSheet.prefab` for the Catalogue lifecycle：that ordinary component requires a full-screen `OutsideButton`，which would consume Scene gestures outside the feature sheet. Reuse its layout/theme/motion conventions only.
- The Task 6 TMP font asset uses the Phase 5 OTF source but has its own atlas/material subassets. Reuse only Phase 5 typography `fontSize`、`fontStyle` and `lineSpacing` values；every Task 6 `TMP_Text.font` is `DecorationUiFont` and its `fontSharedMaterial`/atlas belong to that same local font asset. Never assign the Phase 5 Theme font/material to Task 6 text or mutate the Phase 5 OTF、SDF asset、Theme or TMP settings.

- [ ] **Step 1: Write the complete failing Task 6 tests before production code**

Write `Phase6DecorationUiPrefabTests` for generated paths/hierarchy and `Phase6DecorationUiPlayModeTests` for runtime binding、events、state presentation、motion and Modal ownership. Cover only the Task 6 consumer/presentation portions of `P6-CAT-004–008`、`P6-CAT-012`、`P6-UI-001–012` and `P6-TXN-008–012`. Rebind、select、Cancel/Confirm return and reopen the real Catalogue repeatedly；assert one tile and one owned callback per canonical entry，no stale Definition and no duplicate confirm/dismiss handler.

Invoke the actual serialized Buttons through `Button.onClick.Invoke()`，including while their GameObjects are hidden/inactive and while Buttons are disabled. Prove a tile emits only when `IsInteractable && Definition != null`. Prove action handlers require `IsVisible`，Confirm additionally requires stored `canConfirm`，and Store additionally requires stored `canStore`. Confirm、Cancel and Store share one terminal-action latch per `DecorationActionBarView.Show`；an ineligible direct invocation emits nothing and does not consume the latch；an eligible terminal action consumes it；Rotate may repeat before a terminal action；only the next `Show` resets it，not `Hide`. Apply the same eligible-only，one-shot-until-next-`Show` rule to Store Modal Confirm versus dismiss/Back.

- [ ] **Step 2: Run focused RED**

Expected RED is missing Task 6 view types、new builder path constants or generated Prefabs/font. An unrelated compile failure、Task 5 regression、broken fixture or pre-existing asset mutation is not accepted as Task 6 RED. Record the exact filter、failure count and XML path.

- [ ] **Step 3: Implement deterministic Catalogue binding and invalid fallback**

Bind `DC_Phase6Decoration.asset` in its stable entry order：`furniture.counter.module.01`、`counter.preset.1x2`、`counter.preset.1x3`、`counter.preset.2x3`. Each tile reads its own entry's thumbnail and Definition display name/footprint；there is no copied catalogue DTO and no Task 3 contract change.

Pool tile instances under the Catalogue content root. Before every bind，`Clear` removes only the listener owned by that tile，resets image/text/disabled state and returns unused pooled tiles inactive；never call `Button.onClick.RemoveAllListeners`. Bind exactly one stored callback to each active valid tile. Its private click handler checks `IsInteractable && Definition != null` before invoking the stored callback；`IsInteractable` must be false when the tile/Button is disabled or inactive so direct `onClick.Invoke()` cannot bypass the guard. Rebinding/reopening is idempotent and does not instantiate a fifth canonical tile.

Any missing Definition、Definition Prefab or thumbnail produces a visibly nonblank warning/placeholder and a disabled tile with a stable specific diagnostic；it never emits `Selected`. Missing Definition may use the generic `Unavailable` fallback because no approved display name exists. The builder still rejects each missing reference with the existing Task 3-specific validation；runtime fallback prevents a null exception or blank tile and does not make invalid content shippable.

- [ ] **Step 4: Implement the exact Catalogue/action presentation table**

`ShowCatalogue` always means expanded；`ShowCollapsedHandle` leaves a partially exposed 48 × 48-or-larger handle；`Hide` is used for both Preview states. `DecorationActionBarView.Show` stores `canStore`/`canConfirm`，sets `IsVisible`，resets its one terminal-action latch，makes Store inactive for new Preview，shows it for existing Preview，sets Confirm interactability and renders only the supplied `PlacementFeedbackKey` copy. `Hide` sets `IsVisible = false` but does not reset the latch. Private handlers re-check state instead of trusting `Button.interactable` or active hierarchy：Rotate requires visible and may repeat while no terminal action has fired；Cancel requires visible；Confirm requires visible + `canConfirm`；Store requires visible + `canStore`. Confirm/Cancel/Store latch only immediately before one eligible event is emitted；after that no action event emits until the next `Show`. It contains no placement legality logic. Interrupted show/hide/reopen leaves one usable presentation and one callback per Button.

- [ ] **Step 5: Implement Store Modal view and input ownership**

Wrap the existing `AnimalCafeModalView` in `DecorationStoreModalView`；do not modify the Phase 5 component. Use a `UiView` with `UiViewKind.Modal`、`UiPausePolicy.ContinueGame` and `UiOutsideDismissPolicy.NotDismissible` because Task 7 owns the Decoration session Pause. `AnimalCafeModalView.ConfigureLifecycle` acquires `UiPointerBoundary.AcquireSceneBlock()` while open and releases it on confirm、dismiss、disable、destroy or owner shutdown. Modal Confirm、Cancel and `TryHandleBack` first require `IsOpen` and top-Modal eligibility；only then do they consume one shared completion latch immediately before emitting `ConfirmRequested` or `DismissRequested`. Hidden/inactive/closed direct Button invocation emits nothing and does not consume the latch. Repeated or mixed eligible requests emit once；only the next `Show` resets the latch. `CloseForOwnerShutdown` closes/releases without emitting or resetting it. The full-screen Modal blocker is required，but outside tap does not dismiss.

Catalogue and action bar must not call `AcquireSceneBlock`，because Scene drag/pan remains available outside their visible regions. Add and configure the Task 6-owned `DecorationPointerBoundaryEventHook` adapter on each visible raycast background/root and each tile、handle、action Button、Modal Button and Modal blocker；it forwards to the protected `IUiPointerOwnershipRegistrar`. This same-named-file adapter is required because Unity persists the protected `UiPointerBoundaryEventHook`（declared inside `UiPointerBoundary.cs`）with `m_Script: {fileID: 0}` when authored into a prefab. Do not create a full-screen Catalogue `OutsideButton` or any invisible full-screen raycast target：only the visible expanded sheet、collapsed handle and action bar claim UI hits.

Task 7 classifies the primary raw Touch `Began` synchronously with one `EventSystem.RaycastAll` using its screen position and a `PointerEventData` owned by the EventSystem；a hit from an active `GraphicRaycaster` is UI. It must not copy raw Input System `touchId` into `PointerEventData.pointerId` or the composite `UiPointerBoundary` dictionary. Task 6 exposes `IsCatalogueVisible`/`IsCollapsed`、action-bar `IsVisible` and Modal `IsOpen`；Task 7 passes `uiRaycastHit || modal.IsOpen` to edge-auto-pan exclusion. A close/dismiss release cannot fall through because the Task 5 router latched UI ownership at `Began` until all relevant Touches release，while the Modal scene block protects boundary-aware legacy consumers during the open lifetime.

- [ ] **Step 6: Extend the Phase 6 builder and build deterministic feature assets**

Add exact constants for `UiRootFolderPath`、`UiPrefabFolderPath`、`UiFontFolderPath`、`DecorationUiFontPath` and the three Prefab paths to `Phase6DecorationAssetPaths`，and include them in generated-path validation. Extend `Phase6DecorationAssetBuilder.BuildAll` with an idempotent UI build step after the existing Task 3 catalogue/content work；do not call or rewrite `Phase5UiAssetBuilder.BuildAll`.

Build the complete Task 6 candidate set before publishing any Task 6 live asset：one transient in-memory `TMP_FontAsset` with its transient Material/atlas objects and three inactive Prefab roots with every serialized reference assigned. Validate font coverage、font/material/atlas ownership、required view components、hierarchy、touch targets、raycast scope、copy and Phase 5 dependencies against these candidates. Destroy candidates in `finally`. Candidate validation failure touches no live Task 6 file. Do not claim the earlier Task 3 portion of `BuildAll` is part of this UI transaction.

Publish is a bounded Editor transaction，not an impossible crash-proof filesystem claim. Immediately before publish，copy every existing Task 6 live asset and `.meta` byte-for-byte to a unique `Library/AnimalCafe/Phase6Task6BuildBackup/<run-id>/` folder and record each main GUID；record the Task 6 font Material/atlas local IDs plus subasset `(type,name,localId)` count/order，and each Prefab root view-component local ID. On first build，create fixed live paths in a fixed subasset creation order. On rebuild，update the existing font main object/Material/atlas objects in place without remove/re-add，and reconcile each existing Prefab through `PrefabUtility.LoadPrefabContents` using stable unique hierarchy names without replacing its root view component. Save only after all publish operations succeed. If a builder/publish exception or post-publish identity check occurs in the running Editor process，restore backed-up bytes/metas (or delete only newly created targets)，refresh，and rethrow；always delete the Library backup afterward. No guarantee is claimed for an OS/Editor crash that prevents rollback code from running.

Builder tests first establish a successful live set，then snapshot live bytes/metas、main GUIDs、font Material/atlas local IDs、Prefab view-component local IDs and subasset counts/order. An internal Editor-only publish fault injected after the first live write must restore every snapshot exactly. A normal success run twice must preserve the same identities/count/order and deterministic bytes. Both failed and successful runs also snapshot and prove unchanged bytes for `Assets/UI/Phase5/Fonts/NotoSansSC-Regular.otf`、`Assets/UI/Phase5/Fonts/NotoSansSC-Regular SDF.asset`、`Assets/UI/Phase5/Theme/AnimalCafeUiTheme.asset` and their `.meta` files.

Create the Task 6-local static TMP font candidate from the existing licensed Phase 5 OTF source. Populate the canonical Task 6 copy plus Definition names/footprints，then set it static. Every Task 6 `TMP_Text` uses this exact `DecorationUiFont` and that font's own `fontSharedMaterial`/atlas；copy only the Phase 5 Theme typography size、style and line-spacing values. `Phase5UiFontCoverage.FindMissingUnicodeScalars` must return zero for every Task 6 string，and no runtime dynamic atlas population is allowed.

Build Canvas-less/EventSystem-less Prefabs at the three fixed paths. Reuse `AnimalCafeUiTheme` colors、sizes、button roles、Light Frost/Solid surfaces，body/label minimum sizes/styles/line spacing，`SafeAreaContainer.ConfigureLocalizedText` behavior and `UiTransitionRunner` (`Time.unscaledDeltaTime` + Reduced Motion). Do not create Strong Frost ownership or assign the Phase 5 Theme font/material. Successful rebuilds preserve the fixed live identity contract above and do not duplicate listeners or references.

Responsive contracts are exact：reference Portrait `1080 × 1920` with safe rect `(24,96,1032,1740)`；small `720 × 1280`；tall `1080 × 2400`；Landscape fallback `2400 × 1080` with safe rect `(96,48,2208,984)`. Essential controls remain inside Safe Area，tiles/action/modal controls are at least `48 × 48`，30–50% longer test strings wrap without shrinking below Phase 5 body/label baselines，and no essential action clips. Valid/invalid and enabled/disabled states use text plus icon/shape/pattern，never color alone.

- [ ] **Step 7: Run focused GREEN and Phase 5 UI regression**

Run and record these exact filters separately so EditMode prefab evidence is not presented as runtime evidence：

- EditMode: `AnimalCafe.Tests.EditMode.Phase6.Phase6DecorationUiPrefabTests`
- EditMode Task 3 regressions: `AnimalCafe.Tests.EditMode.Phase6.DecorationCatalogueTests` and `AnimalCafe.Tests.EditMode.Phase6.Phase6DecorationAssetBuilderTests`
- EditMode Phase 5 regressions: `AnimalCafe.Tests.Phase5.AnimalCafeUiThemeTests`、`AnimalCafe.Tests.Phase5.UiPointerBoundaryTests` and `AnimalCafe.Tests.Phase5.UiTransitionAndStrongFrostTests`
- PlayMode focused: `AnimalCafe.Tests.PlayMode.Phase6DecorationUiPlayModeTests`
- PlayMode Phase 5 regressions: `AnimalCafe.Tests.PlayMode.Phase5ReusableComponentsPlayModeTests`、`AnimalCafe.Tests.PlayMode.Phase5ContainerNavigationPlayModeTests`、`AnimalCafe.Tests.PlayMode.Phase5PointerBoundaryPlayModeTests`、`AnimalCafe.Tests.PlayMode.Phase5ResponsiveLayoutPlayModeTests` and `AnimalCafe.Tests.PlayMode.Phase5FeedbackPlayModeTests`
- Phase 6 compile/ownership regressions after Task 5 is GREEN: `AnimalCafe.Tests.PlayMode.Phase6DecorationTouchPlayModeTests` and `AnimalCafe.Tests.PlayMode.Phase6DecorationScenePlayModeTests`

`timeScale = 0` tests must still bind/select、collapse/expand、Rotate/Cancel/Confirm and open/dismiss the Modal；all modified global time/input state is restored in `finally`. The current pre-Phase-6 baseline recorded in the task ledger is full Editor PlayMode `121/121` passed；the former `116/120` Phase 5 order-sensitivity result is historical and superseded. Task 6 focused filters neither re-prove nor contradict `121/121`，and Task 9 owns fresh full-suite and suite-order evidence.

Every case ID in this Task is reported as **Task 6 consumer/presentation portion only**. Even when focused Task 6 assertions pass，the case-level report remains `PARTIAL` until Task 9 supplies integrated real-input/full-suite evidence and Task 10 supplies its required visual/manual evidence；Task 7/8 dependencies remain separately identified below.

- [ ] **Step 8: Visual checkpoint**

Capture these exact manual-review artifacts without treating screenshots as automated proof：

- `outputs/phase6-task6-visual/catalogue-expanded-1080x1920.png`
- `outputs/phase6-task6-visual/catalogue-collapsed-1080x1920.png`
- `outputs/phase6-task6-visual/preview-new-valid-1080x1920.png`
- `outputs/phase6-task6-visual/preview-existing-invalid-1080x1920.png`
- `outputs/phase6-task6-visual/store-modal-1080x1920.png`
- `outputs/phase6-task6-visual/catalogue-small-720x1280.png`
- `outputs/phase6-task6-visual/catalogue-tall-1080x2400.png`
- `outputs/phase6-task6-visual/catalogue-landscape-2400x1080.png`
- `outputs/phase6-task6-visual/feedback-valid-invalid-grayscale-1080x1920.png`

Review the four canonical thumbnails side by side for correct preset、orientation、transparent background and understandable relative footprint；review Safe Area、long-copy wrapping、collapsed-handle exposure、non-color cues and Modal hierarchy. Thumbnail/content artistic acceptance、copy polish and tuning remain Task 10 manual decisions.

**Case/evidence ownership:**

| Cases | Task 6 evidence | Honest deferral |
| --- | --- | --- |
| `P6-CAT-004`、`P6-CAT-012` | Prefab binding + component bind/rebind/reopen/direct-callback counts | Task 9 production EventSystem/reopen flow；Task 10 visual acceptance |
| `P6-CAT-005` | repeated component selection keeps tile present and shows no inventory UI | unique committed Instance: Task 7；integrated repeat flow: Task 9；manual: Task 10 |
| `P6-CAT-006–008` | builder rejection + disabled nonblank component fallback | Preview/commit guard: Task 7；production validator: Task 8；integrated/manual: Tasks 9–10 |
| `P6-UI-001–004` | Prefab/component layout fixtures at the four exact dimensions | `MainCafe`: Task 8；integrated/visual: Tasks 9–10 |
| `P6-UI-005–007` | local-font coverage、long text、48 × 48 targets、component transition recovery | integrated interruption: Task 9；device feel: Task 10 |
| `P6-UI-008` | exact component Sprite-to-Definition binding | integrated Catalogue: Task 9；framing/relative-size acceptance: Task 10 |
| `P6-UI-009–010` | copy mapping、guarded Confirm state、text + shape/icon structure | same-cycle drag: Task 7；integrated/grayscale: Tasks 9–10 |
| `P6-UI-011–012` | component Reduced Motion and `timeScale = 0` | real-input/full-suite: Task 9；device performance/manual: Task 10 |
| `P6-TXN-008–012` | Modal component block/dismiss/one-shot events and Store hidden for new | domain/wiring: Tasks 2/7；integrated double-input/manual: Tasks 9–10 |

No new pure state model is justified. Existing Task 1–2 EditMode suites remain the pure/domain evidence；Task 6 adds generated-Prefab/EditMode、component-runtime/PlayMode and preliminary named visual evidence as separate layers. Task 10 owns integrated visual/manual acceptance，so Task 6 never closes the case-level IDs.

---

### Task 7: Runtime DecorationModeController integration

**Files:**
- Create: `Assets/Scripts/Decoration/CafeLayoutRuntime.cs`
- Create: `Assets/Scripts/Decoration/DecorationModeController.cs`
- Modify: `Assets/Scripts/Interaction/SceneInteractionController.cs`
- Test: `Assets/Tests/PlayMode/Phase6DecorationScenePlayModeTests.cs`
- Test: `Assets/Tests/PlayMode/Phase6DecorationTouchPlayModeTests.cs`
- Approved Task 2 correction: modify `Assets/Scripts/Decoration/DecorationSession.cs` and test `Assets/Tests/EditMode/Phase6/DecorationSessionTests.cs`，without changing the public `DecorationSession` API。
- Dispatch brief: `.superpowers/sdd/2026-08-16-phase-6-basic-decoration-mode/task-7-brief.md`。

**Interfaces:**

```csharp
public sealed class DecorationModeController : MonoBehaviour
{
    public bool IsOpen { get; }
    public DecorationSessionState State { get; }
    public void EnterDecorationMode();
    public void ExitDecorationMode();
    public void CancelActivePreview();
}

public sealed class CafeLayoutRuntime : MonoBehaviour
{
    public CafeLayout Layout { get; }
    public void Initialize();
}
```

The controller also owns private serialized references to the one Task 8 HUD mode-toggle `Button` and its `TMP_Text` label。It installs/removes one runtime listener without adding a public toggle API：closed click enters，open click exits，and success、rollback、disable/destroy synchronize working labels `Decoration` / provisional-localizable `Done`。Task 8 authors the Safe Area Secondary Button and assigns these refs；Task 10 owns final copy acceptance。

Private serialized startup refs in this same controller file connect `CafeLayoutRuntime`、the same FC/DC assets、Game Time/Camera/Touch/Scene interaction、Task 4 registry/Preview/Grid components plus canonical roots/material/theme，Task 6 views and HUD refs；no new public state/adapter type。Before entry can occur，startup runs `CafeLayoutRuntime.Initialize()`，constructs `DecorationGridSpace`，configures the existing components/shared services and rebuilds the authoritative initial Layout into an initially empty representation root。A missing dependency stays closed with no partial formal clone/UI ownership；Task 8 supplies/validates refs and owns actual `MainCafe` timing evidence。

- [ ] **Step 1: Write failing end-to-end controller tests**

Cover Task 7's component/controller slices of `P6-LC-001–008`、`P6-PRV-001–010`、`P6-GRID-005–009`、`P6-TXN-001–012`、`P6-IN-001–015` and `P6-SYNC-002–010`。Use a fake Touch source and fake `IGameTimeService`，but one real shared `UiPauseCoordinator` and real runtime views where integration matters。Task 4 already owns reusable registry/Preview/Grid slices；Task 8 owns actual `MainCafe` `P6-SYNC-001` and real Scene reload `P6-SYNC-011`。Write the complete named RED matrix from `task-7-brief.md` before production changes。

- [ ] **Step 2: Run focused RED**

Expected RED: missing controller and integration bindings。

- [ ] **Step 3: Configure lifecycle ownership**

Controller creates / receives `DecorationSession`、uses one persistent `PauseGame` `UiView`、acquires one `IUiPauseHandle` on enter、releases only its handle on exit / disable / destroy and calls `TryRestorePendingSpeed` according to the Phase 5 contract。The controller and Store Modal receive the exact same `UiPauseCoordinator`、`UiPointerBoundary`、`UiNavigationCoordinator` and `UiTransitionRunner` instances；the Modal remains `ContinueGame`。Nested Pause evidence must acquire both `PauseGame` owners from that one coordinator，not from separately-created coordinators。Enter is candidate-first/atomic：do not publish `IsOpen` or show Grid/UI until Pause/input/Camera/session dependencies succeed；Pause rejection or any later enter failure rolls back session、Camera、input/Pause handles、subscriptions and visuals，then permits retry。Exit、disable and destroy share one idempotent cleanup path；pending speed restore is retried only while closed。

`CafeLayoutRuntime.Initialize` is candidate-first：build the runtime catalogue、`GridSettings(1f)`、`LayoutBounds(0,0,8,8)`、one unlocked Interior `8 × 8` region、then call the configured Phase 4 `EntrancePortalAuthoring.CreateReservation()` and require `entrance.main`、`EntranceClearance`、origin `(3,0)`、size `2 × 2`。Restore/place the initial Counter only after those candidates exist，then publish `Layout`。Its serialized technical defaults are stable ID `00000000000000000000000000000001`、Definition `furniture.counter.module.01`、position `(2,3)` and `Degrees0`。Failure publishes no partial Layout and may retry；repeated success returns the same Layout reference with no duplicates。Bootstrap、controller、registry and Store Definition/Prefab lookup share the same `FC_Phase6Production.asset` instance through private/injected read-only seams in the two new files；do not create another content map or expand the approved public API。A separate Task 7 component reconstruction may prove approved initial-state reset，but actual Scene reload and production formal representation remain Task 8；no Save API/file is added。

The injected `DecorationGridSpace` remains authoritative。Task 8 places its production southwest root at centered-Floor local `(-4,0,-4)`、identity rotation and unit scale；Task 7 uses only a private inverse world-to-Grid helper and does not expand Task 4 APIs。Use inverse root transform、configured cell size and per-axis `FloorToInt` containing-cell semantics；exact boundaries enter the positive adjacent cell，and parallel/behind-plane rays return no Grid hit。Task 7 also owns one sanitized provisional hover height for Preview presentation Y only；Task 10 tunes it。

- [ ] **Step 4: Route Catalogue and furniture selection**

Catalogue selection calls `BeginNew` at Camera-center nearest Grid cell and keeps the nearest invalid cell visible instead of searching elsewhere。At the primary Touch `Began`, classify exactly once in this order：open Modal / active `GraphicRaycaster` result from synchronous `EventSystem.RaycastAll`；raw Floor-projected cell inside the complete active Preview footprint；visible formal furniture Physics hit resolved through `FurnitureSceneRegistry.TryGetInstanceId`；configured Floor/Scene；otherwise `None`。Do not pass raw Input System `touchId` into `PointerEventData.pointerId` / `UiPointerBoundary` or depend on prior-frame `IsPointerOverGameObject` state。

The active-footprint rule is required for both collider-free new Preview and hidden existing Preview，and intentionally wins over formal furniture underneath an invalid overlap。For the remaining Physics stage，sort the one ray's hits by distance，resolve/deduplicate only visible registered representation children，choose the nearest formal ID and break exact-distance ties by stable ID ordinal；formal furniture wins over Floor behind it，and only absence of any formal resolution allows configured Scene fallback。An active Preview hit keeps the current transaction and does not call `BeginExisting` again；a distinct visible formal ID outside that footprint calls `BeginExisting` once and domain auto-cancels the prior Preview。The Task 5 owner remains latched，so drag release over another furniture never switches selection。Blank tap with an active Preview does nothing；without an active Preview it clears ordinary selection without inventing an extra Catalogue-expand path。Blank-Scene drag remains Camera-only。

For an existing-furniture Preview, Task 7 hides the formal clone only through `FurnitureSceneRegistry.SetRepresentationVisible(instanceId, false)`; it must not mutate registry-owned GameObjects directly. The visibility override is transient and is not persisted by the registry.

- [ ] **Step 5: Route drag, rotation and transactions**

Construct the existing `DecorationTouchRouter` with `CameraSettings.DragThresholdPixels` as the only threshold source；do not add another tuning field。Furniture-owned drag uses two coordinate meanings：raw finger for current UI/Modal raycast exclusion and edge-zone/Safe Area checks；Task 5's upward-offset coordinate for Floor projection → Grid position → `MovePreview` → Preview/highlight/action update。Sanitize one immutable offset (`NaN`、infinity、negative → `0`)，pass it to the router and subtract that exact value when deriving raw finger；offset never shifts UI or edge-zone hit regions。Apply edge Camera pan first，then reproject the same offset point in that frame；Pinch freezes Preview movement and immediately stops edge pan until the single-finger owner resumes。

Rotate remains exactly one call to existing `DecorationSession.RotatePreview()`，but its authorized Task 2 internal correction preserves the prior visual center：apply `(oldFootprintSize - newFootprintSize) / 2` to the origin，truncate exact half-cell ties toward zero，validate only that nearest candidate and publish one immutable Preview。Do not search for a distant valid cell；near bounds stays visible/invalid；four rotations restore exact snapped position/rotation。No public Task 2 API grows。

Each eligible view event calls exactly one domain operation；the controller then applies this exact state-to-view table instead of letting individual callbacks toggle unrelated objects：

| Trigger / domain result | Required session state | Catalogue / action / Modal | Preview、highlight and registry |
| --- | --- | --- | --- |
| Enter → `session.Enter()` | `BrowsingCatalogue` | bind if needed；`ShowCatalogue()` expanded exactly once；action `Hide()`；Modal `CloseForOwnerShutdown()` | clear stale Preview/highlight，show Grid；no transaction rebuild |
| valid Catalogue tile → `BeginNew` | `PreviewingNewFurniture` | Catalogue `Hide()`；action `Show(false, result.Succeeded, mappedFeedback)`；Modal closed | create/sync one Preview + complete footprint highlight；formal registry unchanged |
| existing tap → `BeginExisting` success | `EditingExistingFurniture` | Catalogue `Hide()`；action `Show(true, result.Succeeded, mappedFeedback)`；Modal closed | if replacing a prior hidden Preview，rebuild once first；then hide only selected formal representation through `SetRepresentationVisible` and sync one Preview/highlight |
| existing tap → `BeginExisting` failure | `BrowsingCatalogue` | action hide；Modal close；expanded Catalogue exactly once | clear Preview/highlight；rebuild once only if a prior representation had been hidden |
| `MovePreview` / `RotatePreview` success | unchanged new/existing Preview state | Catalogue hidden；action `Show(!preview.IsNew, true, None)`；Modal closed | sync Preview + valid highlight；no registry rebuild |
| `MovePreview` / `RotatePreview` failure | unchanged new/existing Preview state | Catalogue hidden；action `Show(existingSourceStillPresent, false, mappedFeedback)`；Modal closed | keep/sync invalid Preview + complete invalid highlight；no registry rebuild |
| `ConfirmPreview` success | `BrowsingCatalogue` | close Modal；action hide；expanded Catalogue exactly once | destroy Preview/highlight；`FurnitureSceneRegistry.Rebuild` authoritative Layout exactly once |
| `ConfirmPreview` failure | unchanged new/existing Preview state | Catalogue hidden；close Modal；action `Show(existingSourceStillPresent, false, mappedFeedback)` | keep/sync invalid Preview/highlight；no registry rebuild；new `Show` opens one corrected retry window |
| Cancel → `CancelPreview` | `BrowsingCatalogue` | close Modal without dismiss event；action hide；expanded Catalogue exactly once | destroy Preview/highlight；registry rebuild exactly once to restore authoritative formal representations |
| Store request → `BeginStoreConfirmation` accepted | `ConfirmingStore` | Catalogue hidden；leave the already-shown action bar underneath without another `Show`/latch reset；Modal `Show(definition)` exactly once | keep Preview/highlight；no Layout mutation or registry rebuild |
| Store Modal dismiss/Back → `DismissStoreConfirmation` | `EditingExistingFurniture` | close Modal；Catalogue hidden；action `Show(true, preview.PlacementResult.Succeeded, mappedFeedback)` once | keep the same Preview/highlight；no registry rebuild；this `Show` creates the next eligible action window |
| Store confirm → `ConfirmStore` success | `BrowsingCatalogue` | ensure Modal closed；action hide；expanded Catalogue exactly once | destroy Preview/highlight；registry rebuild once after removal so the absent representation stays absent |
| Store confirm → `ConfirmStore` failure，then `DismissStoreConfirmation` | `EditingExistingFurniture` | close Modal；Catalogue hidden；action `Show(existingSourceStillPresent, false, mappedFeedback)` | retain/sync Preview + invalid highlight；no registry rebuild；Store stays unavailable when source Instance is missing |
| Exit / disable / destroy → `session.Exit()` | `Closed` | Catalogue/action hide；Modal `CloseForOwnerShutdown()` with no dismiss callback | destroy Preview/highlight/Grid；rebuild once if a formal representation was hidden；reset Touch/Camera ownership and release only this controller's Pause handle |

`existingSourceStillPresent` means an existing Preview whose stable source Instance still resolves in the authoritative Layout；new Preview always passes `false`. Each table row is applied once per accepted operation. Only Enter、successful Confirm、Cancel、successful Store and failed existing selection call expanded `ShowCatalogue()`，and each does so exactly once. Runtime tests assert every observable state/event/visibility/Layout result；because sealed idempotent views/registry do not expose invocation counters，indistinguishable exact call counts are recorded as source-review assertions rather than fabricated behavior-test evidence。Failure rows keep actionable feedback visible and never rebuild unchanged Layout data. Cleanup is controller-owned；views never destroy Preview/highlight or call `FurnitureSceneRegistry`.

Controller callbacks must gate missing/closed/wrong-state operations before calling the domain；the domain's convenience `Success()` when no Preview exists is not an accepted UI operation。

- [ ] **Step 6: Protect existing Scene interaction and Camera**

Add an owner-safe disposable suppression handle to `SceneInteractionController`。Reject null owners；each acquire returns an independent token lease and repeated dispose is harmless。The first lease releases/clears active and pending Scene pointer ownership through the existing cleanup path。While any lease remains，`LateUpdate` still drains terminal input/`UiPointerBoundary` state but never registers presses、selects or clears selection，and direct `TrySelectAt` is blocked。Only the final release sets `ignoreUntilFreshPress`，which drops the current/tail UI release until a later fresh mouse/touch-compatible `PointerPressed`。This closes the same-frame exit-release leak while preserving normal mode and independent owners。The controller holds one handle for the Decoration lifetime。

The controller also records the existing `CafeCameraController.enabled` state，disables its legacy `Update` input consumption while Decoration is open，continues calling public `ApplyPan` / `ApplyZoom` through `DecorationCameraDriver` and restores the exact prior enabled state on exit / disable / destroy。This prevents one physical gesture from being consumed by both legacy Mouse / Camera input and Decoration Touch routing。

- [ ] **Step 7: Run focused GREEN and normal-mode regression**

Run the exact focused and named regression filters in `task-7-brief.md` separately。Verify exiting restores Phase 0 Camera、selection、Time controls and Phase 5 UI behavior；record XML totals/paths in `task-7-report.md`。Task 7 does not claim production `MainCafe`、real Touch、Scene reload、full-suite/order、Standalone、visual or manual completion；those remain Tasks 8–11。

- [ ] **Step 8: Architecture checkpoint**

Review that controller coordinates but does not reimplement Layout legality、UI navigation、Camera bounds or content lookup。

---

### Task 8: Idempotent Scene setup, MainCafe migration and validator

**Status:** `ready for independent re-review round 2；implementation not yet authorized`。Task 7 report、focused/named GREEN and independent review gate are complete；Task 8 starts only after this corrected round-2 brief/plan/test contract is independently accepted。A later Task 7 regression is not Task 8 RED。The exact dispatch contract is `.superpowers/sdd/2026-08-16-phase-6-basic-decoration-mode/task-8-brief.md`。

**Files:**
- Create: `Assets/Editor/Phase6/Phase6DecorationSceneSetup.cs` and `.meta`
- Create: `Assets/Editor/Phase6/Phase6DecorationValidator.cs` and `.meta`
- Modify: `Assets/Scenes/MainCafe.unity` through Unity Editor APIs only
- Create: `Assets/Scenes/Validation/Phase6DecorationMode.unity` and Unity-generated `.meta` through Unity Editor APIs only
- Test: `Assets/Tests/EditMode/Phase6/Phase6DecorationValidatorTests.cs` and `.meta`
- Test: `Assets/Tests/EditMode/Phase6/Phase6MainCafeMigrationTests.cs` and `.meta`
- Test: `Assets/Tests/PlayMode/EditorSceneLoading/Phase6DecorationMainCafeSceneTests.cs` and `.meta`
- Migrate narrowly: `Assets/Tests/PlayMode/Phase0PlayModeTests.cs`
- Migrate narrowly: `Assets/Tests/EditMode/Phase4/Phase4AssetValidatorTests.cs`
- Migrate narrowly: `Assets/Tests/EditMode/Phase4/Phase4ValidationSceneSetupTests.cs`
- Migrate narrowly: `Docs/Phase4_Beginner_Guide.md`
- Delete after the Step 6 consumer scan passes: `Assets/Editor/Phase4/MainCafeManualReviewFixtureSetup.cs` and `.meta`
- Delete after the Step 6 consumer scan passes: `Assets/Art/Phase4/Materials/M_TEMP_ManualReviewCube_Static.mat` and `.meta`
- Delete after the Step 6 consumer scan passes: `Assets/Art/Phase4/Materials/M_TEMP_ManualReviewCube_Moving.mat` and `.meta`
- Retain: `Assets/Scripts/Diagnostics/ManualReviewPingPongMover.cs` and `Assets/Tests/PlayMode/Phase4/ManualReviewPingPongMoverTests.cs` with their `.meta` files；Phase 5 still consumes/serializes the mover
- Evidence/report: `.superpowers/sdd/2026-08-16-phase-6-basic-decoration-mode/task-8-report.md`、`outputs/phase6-task8-*`

**Interfaces:**

```csharp
public static class Phase6DecorationSceneSetup
{
    public static void ConfigureMainCafe();
    public static void ConfigureValidationScene();
}

public static class Phase6DecorationValidator
{
    public static Phase6DecorationValidationReport ValidateAll();
}
```

Editor-internal test-only contract additionally exposes the immutable dependency resolver/fault/save observer seams，the production Validation candidate factory，the production owned reconciler and one pure candidate validator through existing `InternalsVisibleTo(AnimalCafe.EditModeTests)`。These helpers are the same implementations used by production，add no public/runtime API and every test candidate closes in outer `finally`。Internal tests supplement but never replace public setup transaction tests。

- [ ] **Step 1: Write failing Editor contract tests**

After independent Task 8 round-2 re-review，write the exact RED matrix from `task-8-brief.md`。Parameterize transaction cases over both `ConfigureMainCafe` and `ConfigureValidationScene`：dirty-target refusal；safe injected missing dependency without moving real assets；unrelated dirty asset/dirty Scene、ordered mixed Selection with active index and open-order preservation；`BeforeMutation`/`BeforeSave`/`AfterSave` exact `.unity`/`.meta` rollback；first-publish Validation deletion；second-run `0 BeforeSave / 0 SaveScene / 0 AfterSave`；success/fault candidate+backup cleanup；unknown same-name/wrong Prefab/unexplained owned-child refusal。Selection keeps raw refs for assets/unrelated-Scene objects and stores target GameObject/Component `GlobalObjectId` for post-reopen resolution；success and every fault restore a retained target selection in the exact array order/active index。Selecting the approved TEMP root or any descendant refuses before mutation with a deselect-and-retry message。

For Validation hostile cases，outer `finally` protects both targets、Build Settings、Selection and open Scene setup；temporarily publish/seed the canonical Validation path with each hostile candidate，close it，call the real public `ConfigureValidationScene` and require byte/meta/object/state preservation。Restore original target bytes/meta or absence afterward。Internal candidate factory/reconciler/validator tests are additional only；the public transaction remains authoritative。Clear every static seam and close every candidate in teardown。

- [ ] **Step 2: Run focused RED**

Expected RED: only missing Task 8 setup/validator/Scene-loading boundaries and the current pre-migration MainCafe state。`ConfigureMainCafe_CurrentFixtureStateIsExpectedRed` may characterize that first RED only and is removed before GREEN。A broken/dirty fixture、Task 7 regression、moved real dependency、teardown pollution or unrelated failure is not acceptable RED。

- [ ] **Step 3: Implement validator before migration**

Implement immutable stable issues with `AssetPath`、`ObjectPath`、`Message`。Deduplicate exact full tuples and sort by AssetPath ordinal、ObjectPath ordinal、stable code、Message ordinal。Cover missing/duplicate/mislocated Main Camera、Directional Light、`Phase0_Runtime`、GameTimeService、MouseCameraInput、CafeCameraController、SceneInteractionController；environment、Decoration owner、Grid roots、UI Root/exact Canvas inventory、direct-child EventSystem/InputSystemUIInputModule、`UnexpectedStandaloneInputModule`、Validation TimePanel/TimeControlPanel/buttons/Theme/TMP/GameTimeService wiring、reference-only hierarchy/text/font/transform/zero-gameplay-binding inventory、HUD/content/Prefab/thumbnail、mismatched FC/DC、serialized formal representation、temporary/initial content、Build Settings、runtime `UnityEditor` and runtime Save-writer/API boundaries。Reuse Phase 4/5 validators and `ValidateDecorationCatalogue`。One pure internal candidate-Scene validator is called pre-save and reused by public `ValidateAll()` after reopen；both are read-only and never setup/build/save/repair。

- [ ] **Step 4: Implement idempotent Validation Scene setup**

Validation first-publish is wholly Task 8-owned，while MainCafe base remains allow-listed/validated。Build the full exact Validation manifest without calling `Phase0SceneSetup`：`Main Camera` owns Camera/AudioListener/URP only at `(-10,10,-10)`、Euler `(35.264,45,0)`、orthographic size `7`；`Directional Light` owns Light/URP at `(0,3,0)`、Euler `(50,-30,0)`、intensity `2`；identity `Phase0_Runtime` owns exactly one GameTimeService、MouseCameraInput、CafeCameraController and SceneInteractionController。Serialize `DefaultCameraSettings` on `MouseCameraInput.settings` and `CafeCameraController.settings`；bind CafeCameraController and SceneInteractionController to Main Camera and the same MouseCameraInput。Add canonical P4 Environment/Entrance、one Phase6 owner with Task4/5/7 components and one PF_UI_Root/exact three Canvases/layers。

Validation owns UI Root's Scene additions：direct HUD Layer child `TimePanel` with `TimeControlPanel`；Pause/Normal/Fast Buttons at X `-110/0/110` with exact `Pause/1x/2x` TMP labels、Theme Primary styling and Validation GameTimeService/button refs；direct UI Root child EventSystem with EventSystem/InputSystemUIInputModule/package actions and zero Standalone；Decoration Safe Area/Task6 UI。It also owns identity `Phase6_ContractReferences` with `BlockedArea_ReferenceOnly (4.25,0.05,-0.5)` and `LockedArea_ReferenceOnly (4.25,0.05,1.5)`；each child has only Transform、TextMeshPro and its required MeshRenderer，exact `Blocked - Reference Only`/`Locked - Reference Only` text，no Collider/Layout/registry/controller/input/reflection。Pure `CafeLayout` tests retain blocked/locked legality authority。Healthy second reconcile preserves all owned IDs、Prefab sources、wiring、labels and zero-gameplay-binding inventory；unknown roots/children/wrong Prefabs refuse rather than delete。Floor instance alone overrides `GridOverlay` inactive；never add Validation to Build Settings。

- [ ] **Step 5: Implement idempotent MainCafe migration**

Both public commands use one exact transaction order：caller/dependency preflight → exact target bytes/meta backup → `BeforeMutation` → first owned mutation → pure candidate validation → only if dirty `BeforeSave` → at most one observed `SaveScene` → `AfterSave` → target reopen/public validation → caller Scene order/active/ordered Selection/dirty-state restoration → success backup cleanup。Selection snapshots exact array order/active index；assets and unrelated-Scene entries keep refs，target entries resolve saved GlobalObjectIds after reopen。Unresolved retained target selection fails/rolls back；selected TEMP root/descendant refuses before mutation。A healthy second run skips all three save-stage events and `SaveScene`。On failure restore exact bytes/meta or delete first-publish Validation `.unity`+`.meta`，targeted import only，restore caller state，then cleanup。After successful validation+caller restore，backup cleanup failure emits a warning and retains diagnostics without rolling back the valid Scene。After reopen compare hierarchy path、GlobalObjectId/localID、nearest Prefab GUID/path/source localID、counts and refs。No global SaveAssets/SaveAssetIfDirty、broad Refresh or Build Settings writer。

Instantiate the actual canonical P4 hierarchy: `P4_Environment` identity；Floor `(0,0,0)`；Back-left `(0,0.5,4)`；Back-right `(4,0.5,0)` Y90 with Window `(-0.5,0.5,-0.061)`；Entrance `(0,0,-4)`。Keep source GUIDs `ae71a0726a504f24b8d97d7e1f4b15fd`、`e9324ba340ec5634591234b9c38befd0`、`3b0e2d354fbc57e4eb64d7c9c48c63ca`、`c99128042b5e8c04b837af3f4d42ae5c`、`f5a18fb1ec2e47c4cb018a16ca3a97b9` and only override the Floor instance `GridOverlay` inactive。

Author one identity `Phase6_DecorationRuntime` with `DecorationSpaceRoot (-4,0,-4)` and identity `GridVisualRoot`、empty serialized `FurnitureRepresentationRoot`、`FurniturePreviewRoot` children。Assign the final Task 7 private startup refs so `Initialize → Configure registry/Preview/Grid/views → Rebuild` produces exactly one formal Counter `(2,3)` before entry；do not serialize the Counter or depend on Script Execution Order。Use one shared `FC_Phase6Production.asset` across bootstrap/controller/registry/Store，one `DC_Phase6Decoration.asset` for Catalogue and one controller-owned shared set of Pause/Navigation/Pointer/Transition services。Do not preplace Work Table、Cash Register or Coffee Machine。

Reuse `PF_UI_Root` GUID `f2fb88287e92d864997d99874d6dfdaa` with one UI Root、exact `HUD Canvas`/`Screen Canvas`/`Toast Canvas`、one EventSystem/InputSystemUIInputModule and zero Standalone。Package `DefaultInputActions` GUID `ca9f5fa95ffab41fb9a615ab714db018` remains valid。Keep existing `HUD Layer/TimePanel` untouched；add sibling `HUD Layer/Decoration Safe Area` from `PF_UI_SafeArea` GUID `f60e1cacdc594b84e98eab28d3070167` with top-right `DecorationModeButton` from `PF_UI_Button_Secondary_Default` GUID `9c746f33a5758cf41bad68f12aedbeff` and assign Button/TMP refs。Place Task 6 Catalogue/Action under Panel Layer and Store Modal under Modal Layer。Healthy Phase 5 children are allow-listed，not unexplained Task 8 children。Controller owns one listener and `Decoration`/provisional `Done`；no adapter。Never hand-edit YAML。

- [ ] **Step 6: Remove P4 temporary fixture only after consumer scan**

Define zero consumer as no current compile/type/path reference、no live serialized GUID/script/Prefab/Scene reference and no active Beginner Guide instruction；historical reports/specs/plans do not block deletion and are not rewritten。Migrate the four approved test/guide consumers，then delete only setup utility/two materials with `.meta`。Retain `ManualReviewPingPongMover` and its test because Phase 5 still has live compile/serialized consumers。The conflicting Roadmap sentence is Task 11 documentation debt，not edited in Task 8。Add regression evidence for every removed/retained path。

- [ ] **Step 7: Run setup twice, validator and Phase 4 / 5 migration regression**

Run the exact brief list including new Task 8 fixtures、`CafeLayoutPreviewValidationTests`、`DecorationSessionTests`、one combined `Phase6DecorationScenePlayModeTests` entry、actual Touch router/Camera/source fixtures、`Phase6DecorationUiPrefabTests`、`Phase6DecorationUiPlayModeTests`、Task 3 assets、migrated Phase 0/4 and Phase 5 validator/setup/MainCafe/Scene-loading suites。Runtime source scan forbids a Save writer/API；actual unload/reload snapshots only `Application.persistentDataPath/AnimalCafe/Phase6` before/after without creating/deleting/repairing it or scanning the whole persistentDataPath。Expected issues `0`、one enabled MainCafe、Validation absent from Build Settings and failed/skipped/inconclusive `0`。No full-suite/Standalone/real-Touch completion claim。

- [ ] **Step 8: Scene checkpoint**

Open MainCafe and Validation Scene outside Play Mode。Review target-specific ownership、Hierarchy counts、exact Prefab links/transforms、Floor-only inactive GridOverlay instance override、empty serialized representation root、one UI Root/exactly three Canvases/direct-child EventSystem、Validation TimePanel wiring and both visible reference-only labels with zero gameplay bindings。Capture `maincafe-closed-1080x1920.png`、direct-controller `maincafe-decoration-open-direct-1080x1920.png`、`validation-overview-1080x1920.png`、Hierarchy/Prefab-link screenshot and Scene inspection notes。Direct open is not real Touch；defer Task 9–11 ownership exactly as the brief states。

---

### Task 9: Real Touch, responsive and interruption PlayMode coverage

**Files:**
- Create: `Assets/Tests/PlayMode/EditorSceneLoading/Phase6DecorationRealTouchTests.cs`
- Extend: `Assets/Tests/PlayMode/Phase6DecorationTouchPlayModeTests.cs`
- Extend: `Assets/Tests/PlayMode/Phase6DecorationUiPlayModeTests.cs`

**Interfaces:**
- Consumes completed runtime / Scene contracts from Tasks 1–8。
- Produces focused real Touch evidence for `P6-IN-*`、`P6-UI-*` and recovery cases。

- [ ] **Step 1: Write real Touch tests before final input hardening**

Use `InputTestFixture` / Input System Touch device and actual EventSystem。Test press / drag / release ordering、second pointer join、UI-to-Scene drag、drag release over furniture、Safe Area edge exclusions and Modal block。

- [ ] **Step 2: Run focused RED against current Task 8 integration**

Expected RED must correspond to missing / incorrect real-device behavior，not a broken test fixture。Record exact failed assertion and input trace。

- [ ] **Step 3: Apply minimal input hardening**

Change only the smallest runtime boundary responsible for each RED。Do not add test-only current-device ownership、alternate mouse paths or global Input System resets without evidence。

- [ ] **Step 4: Add responsive fixtures**

Exercise reference / small / tall Portrait、Landscape、Safe Area and long localized strings。Assert actions visible and raycastable；Grid / Preview remains inside intended Scene view。

- [ ] **Step 5: Add interruption matrix**

Interrupt during Catalogue transition、Furniture drag、Pinch、invalid Preview、Store Modal and exit。Assert Pause、pointer owners、blockers、Previews and UI views clean exactly once。

- [ ] **Step 6: Run focused GREEN repeatedly in both orders**

Run Phase 6 focused Touch alone and after Phase 5 real input tests。Both orders must pass；report any order dependence rather than masking it。

- [ ] **Step 7: QA checkpoint**

Provide XML、logs、test counts and the named input-order evidence before full regression。

---

### Task 10: Studio Owner manual mobile-feel tuning

**Files:**
- Create: `outputs/phase6-manual-review/Phase6_P6-M-001-M030_Manual_Review.md`
- Create or update: `outputs/phase6-manual-review/AnimalCafe_P6_Manual_Review.xlsx`
- Modify only after explicit per-round approval: serialized tuning values on Phase 6 Prefab / Theme-linked assets。

**Interfaces:**
- Uses manual cases `P6-M-001–030` exactly as written。
- Produces accepted values for hover height、touch offset、drag threshold、edge zone / speed、Bottom Sheet height、Grid opacity / intensity and transitions。

- [ ] **Step 1: Generate manual review sheet from approved IDs**

Columns: ID、Scene / resolution、precondition、action、expected、actual、PASS / FAIL / BLOCKED、evidence path、notes。No pre-filled PASS values。

- [ ] **Step 2: Run pre-manual focused smoke**

Focused Phase 6 EditMode / PlayMode / validator must be green before requesting hands-on review。

- [ ] **Step 3: Studio Owner runs P6-M-001–030**

Use production MainCafe except cases explicitly requiring Validation Scene。Record actual touch feel and Console results。

- [ ] **Step 4: Convert failed feel cases into named adjustment rounds**

Each round changes one parameter group only：drag / offset、edge auto-pan、Bottom Sheet、Grid visual or transitions。Add / update a failing automated contract when behavior changes；then rerun affected focused tests。

- [x] **Step 5: Obtain explicit manual acceptance**

All cases must be PASS or a named limitation explicitly accepted by Studio Owner。Do not infer acceptance from silence。

- [x] **Step 6: Manual checkpoint**

Summarize accepted values、remaining limitations、player-visible result and Console state。

---

### Task 11: Fresh full regression and Phase closeout evidence

**Files:**
- Create outputs under `outputs/phase6-manual-review/` for final XML / logs / closeout summary
- Modify after evidence: `Docs/AnimalCafe_Development_Roadmap.md`
- Modify after evidence: `Docs/superpowers/plans/2026-08-16-phase-6-basic-decoration-mode.md` checkbox states

**Interfaces:**
- Consumes all approved Task evidence and Studio Owner manual acceptance。
- Produces exact completion evidence；does not authorize commit / push / merge。

- [x] **Step 1: Run fresh full EditMode**

Use Unity `6000.5.5f1`。Record XML / log、total、passed、failed、skipped、inconclusive、start / end time。

- [x] **Step 2: Run fresh full Editor PlayMode**

Report initial result honestly。If canonical asset import / generation changes the first result，investigate and rerun only after recording cause and corrective action。

- [x] **Step 3: Run fresh standalone runtime suite**

Verify player assembly、MainCafe loading and mobile-compatible input path；no `UnityEditor` dependency。

- [x] **Step 4: Run Phase 4 and Phase 6 validators**

Expected issues `0`；record exact valid asset counts and Scene contracts。

- [x] **Step 5: Review diff and working tree scope**

List every created / modified / removed path。Separate pre-existing user changes (`.gitignore`、`AnimalCafe.slnx`、apron / Westie docs and any later user work) from Phase 6 paths。Do not stage unrelated files。

- [x] **Step 6: Request independent code review**

Invoke `superpowers:requesting-code-review`。Resolve actionable findings through focused failing tests；rerun affected regression。

- [x] **Step 7: Write closeout summary**

Include player-visible result、automated counts、manual P6-M result、known limitations、performance observations、files / assets changed and GitHub Desktop handoff。

- [x] **Step 8: Update Roadmap only after all gates pass**

Mark Phase 6 `Completed` only after approved spec、TDD、review、fresh full regression and Studio Owner manual acceptance。Also correct the stale Phase 4 closeout sentence that says Task 8 should delete `ManualReviewPingPongMover` and its regression test：record that Task 8 removed only the obsolete MainCafe setup/materials while retaining the mover/test for live Phase 5 consumers。Set Current Next Step to Phase 7 design gate；do not begin Phase 7 automatically。

## Final execution boundary

Implementation may begin only after Studio Owner approves this plan and chooses an execution workflow。At execution start：

1. inspect current branch / worktree and unrelated local changes；
2. invoke `superpowers:using-git-worktrees` only if an isolated worktree is approved and required；
3. invoke `superpowers:test-driven-development` before Task 1 implementation；
4. execute one Task at a time with reviewer checkpoints；
5. do not commit、push、merge、clean branches or delete worktrees unless Studio Owner separately authorizes that exact action。
