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
- `Assets/Scripts/UI/Decoration/DecorationStoreModalController.cs` — Store confirmation and dismissal。

### Editor assets and integration

- `Assets/Editor/Phase6/Phase6DecorationAssetPaths.cs` — canonical Phase 6 paths。
- `Assets/Editor/Phase6/Phase6DecorationAssetBuilder.cs` — preset Prefabs、Definitions、Catalogue、thumbnails、UI assets。
- `Assets/Editor/Phase6/Phase6DecorationSceneSetup.cs` — idempotent Validation Scene + MainCafe migration。
- `Assets/Editor/Phase6/Phase6DecorationValidator.cs` — assets、scene、Build Settings、runtime assembly contracts。
- `Assets/Scenes/Validation/Phase6DecorationMode.unity` — boundary / Touch / multi-cell validation scene。
- `Assets/Art/Phase6/Definitions/FD_Counter_Preset_1x2.asset`、`1x3`、`2x3` — player-visible placeholder Definitions；`1x1` 复用 Phase 4 Definition。
- `Assets/Art/Phase6/Catalogues/FC_Phase6Production.asset` — Phase 4 production content + Phase 6 Counter presets 的 runtime lookup catalogue。
- `Assets/Art/Phase6/Prefabs/PF_Counter_Preset_1x2.prefab`、`1x3`、`2x3` — one-root multi-cell presets。
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
- Create: `Assets/Scripts/Decoration/FurnitureSceneRegistry.cs`
- Create: `Assets/Scripts/Decoration/FurniturePreviewView.cs`
- Create: `Assets/Scripts/Decoration/GridHighlightView.cs`
- Test: `Assets/Tests/PlayMode/Phase6DecorationScenePlayModeTests.cs`

**Interfaces:**

```csharp
public sealed class FurnitureSceneRegistry : MonoBehaviour
{
    public void Configure(FurnitureContentCatalog contentCatalog, Transform root);
    public void Rebuild(IReadOnlyList<FurnitureInstance> instances);
    public bool TryGet(string instanceId, out GameObject representation);
    public void Remove(string instanceId);
}

public sealed class FurniturePreviewView : MonoBehaviour
{
    public void Show(GameObject prefab, IReadOnlyList<GridPosition> cells);
    public void SetPlacement(GridPosition position, FurnitureRotation rotation, float hoverHeight);
    public void SetValidity(bool valid);
    public void Hide();
}

public sealed class GridHighlightView : MonoBehaviour
{
    public void ShowGrid(GridSettings settings);
    public void ShowFootprint(IReadOnlyList<GridPosition> cells, bool valid);
    public void ClearFootprint();
    public void HideGrid();
}
```

- [ ] **Step 1: Write failing Scene sync and visual lifecycle tests**

Cover `P6-SYNC-001–009`、`P6-GRID-010`、`P6-VAL-001–010`。Use lightweight runtime materials created by test fixture；assert one representation per Instance ID and all visuals clear on disable。

- [ ] **Step 2: Run focused PlayMode RED**

Expected RED: missing Scene view components。

- [ ] **Step 3: Implement idempotent registry**

`Rebuild` removes representations for absent IDs、updates matching IDs、creates missing IDs from `FurnitureContentCatalog.TryGetPrefab`。Missing Definition / Prefab logs one specific recoverable issue and never creates empty permanent objects。

- [ ] **Step 4: Implement Preview transform and hover state**

Preview is a separately owned clone with colliders / selection hooks disabled。Rotation uses exact `FurnitureRotation` mapping；vertical hover is presentation-only；Grid position remains the transaction coordinate。

- [ ] **Step 5: Implement Grid and footprint visuals**

Create pooled cell visuals for exact `8 × 8` Grid。Active cells use Phase 5 theme-derived valid / invalid colors plus non-color mark component；no mutation to Phase 4 Floor mesh or material asset。

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
public enum DecorationGestureOwner { None, Ui, Furniture, Camera, Pinch }

public readonly struct DecorationTouchFrame
{
    public IReadOnlyList<DecorationTouchPoint> Touches { get; }
}

public readonly struct DecorationTouchPoint
{
    public int PointerId { get; }
    public Vector2 Position { get; }
    public Vector2 Delta { get; }
    public TouchPhase Phase { get; }
}

public interface IDecorationTouchSource
{
    DecorationTouchFrame ReadFrame();
}

public sealed class DecorationTouchRouter
{
    public DecorationGestureOwner Owner { get; }
    public void Begin(int pointerId, DecorationGestureOwner owner);
    public bool TryPromoteToPinch(int secondPointerId);
    public void Release(int pointerId);
    public void Reset();
}
```

- [ ] **Step 1: Write failing ownership tests**

Cover `P6-IN-001–015`。Assert gesture origin owns until release；drag release cannot become tap；two pointers clean independently；UI regions suppress edge auto-pan。

- [ ] **Step 2: Run focused RED**

Expected RED: missing Touch interfaces / router。

- [ ] **Step 3: Implement deterministic Touch snapshots**

Read `Touchscreen.current` through Input System。Copy pointer ID、phase、position and delta once per frame so consumers share one snapshot。No runtime `UnityEditor` code；Mouse fallback is injected only by Editor / tests through `IDecorationTouchSource`。

- [ ] **Step 4: Implement ownership router**

Owner is selected at first pointer down from UI hit / furniture hit / blank Scene hit。It cannot change until release except explicit promotion to `Pinch` when a second Touch joins。Promotion preserves active furniture Preview and prevents Confirm / Cancel。

- [ ] **Step 5: Implement Camera driver**

Reuse `CafeCameraController.ApplyPan` and `ApplyZoom`；do not duplicate Camera bounds。Furniture-owned edge auto-pan produces a capped screen delta only when pointer is inside approved Scene edge zones and outside Safe Area / UI exclusions。

- [ ] **Step 6: Add touch drag offset and snapping contract tests**

Visible Preview coordinate equals pointer position plus configured screen-space upward offset projected to Floor。The snapped candidate is computed from visible Preview, not the hidden finger point。

- [ ] **Step 7: Run focused GREEN and Phase 5 pointer regression**

Run `Phase5PointerBoundaryPlayModeTests` together with new Touch filter。Expected: all pass；no UI / Scene click-through。

- [ ] **Step 8: Tuning checkpoint**

Expose serialized drag threshold、offset、edge zone、speed curve and max speed with safe min constraints。Do not finalize values until Task 10 manual playtest。

---

### Task 6: Catalogue, action bar and Store Modal UI

**Files:**
- Create: `Assets/Scripts/UI/Decoration/DecorationCatalogueView.cs`
- Create: `Assets/Scripts/UI/Decoration/DecorationCatalogueTileView.cs`
- Create: `Assets/Scripts/UI/Decoration/DecorationActionBarView.cs`
- Create: `Assets/Scripts/UI/Decoration/DecorationStoreModalController.cs`
- Create prefabs under `Assets/UI/Phase6/Prefabs/`
- Test: `Assets/Tests/PlayMode/Phase6DecorationUiPlayModeTests.cs`

**Interfaces:**

```csharp
public sealed class DecorationCatalogueView : MonoBehaviour
{
    public event Action<FurnitureDefinitionAsset> Selected;
    public void Bind(DecorationCatalogueAsset catalogue);
    public void ShowCatalogue();
    public void ShowCollapsedHandle();
}

public sealed class DecorationActionBarView : MonoBehaviour
{
    public event Action RotateRequested;
    public event Action ConfirmRequested;
    public event Action CancelRequested;
    public event Action StoreRequested;
    public void Show(bool canStore, bool canConfirm, PlacementFeedbackKey feedback);
    public void Hide();
}
```

- [ ] **Step 1: Write failing UI binding and navigation tests**

Cover `P6-CAT-004–005`、`P6-UI-001–012`、`P6-TXN-008–012`。Assert four tiles、48 × 48 minimum targets、long labels、Safe Area、Pause-time interaction and Modal blocking。

- [ ] **Step 2: Run focused RED**

Expected RED: missing views and Prefabs。

- [ ] **Step 3: Implement Catalogue tile pooling**

`Bind` clears old listeners and reuses / creates exactly one tile per entry。Tile reads Definition name / footprint and thumbnail；missing asset creates disabled tile with specific Validation Message，not blank content。

- [ ] **Step 4: Implement Catalogue / action bar switching**

Reuse Phase 5 Bottom Sheet navigation / transitions。New Preview hides Store；existing Preview shows Store；invalid Preview disables Confirm and displays mapped copy。Confirm / Cancel returns to Catalogue exactly once。

- [ ] **Step 5: Implement Store Modal controller**

Reuse `AnimalCafeModalView` and Phase 5 scene block。Confirm invokes one controller callback；dismiss leaves editing state unchanged；repeated taps cannot invoke twice。

- [ ] **Step 6: Build Phase 5-themed feature Prefabs**

Use existing `AnimalCafeUiTheme` tokens、Noto Sans SC、Button roles、Safe Area and Reduced Motion contract。No new Canvas、EventSystem or Strong Frost owner unless profiling / design requires it；default to Light Frost / Solid feature surfaces。

- [ ] **Step 7: Run focused GREEN and Phase 5 UI regression**

Run new UI suite plus Phase 5 reusable components、navigation、feedback、responsive and real input filters。

- [ ] **Step 8: Visual checkpoint**

Show Portrait Catalogue、active valid Preview、active invalid Preview and Store Modal at reference / small / tall Portrait and Landscape fallback。

---

### Task 7: Runtime DecorationModeController integration

**Files:**
- Create: `Assets/Scripts/Decoration/CafeLayoutRuntime.cs`
- Create: `Assets/Scripts/Decoration/DecorationModeController.cs`
- Modify: `Assets/Scripts/Interaction/SceneInteractionController.cs`
- Test: `Assets/Tests/PlayMode/Phase6DecorationScenePlayModeTests.cs`
- Test: `Assets/Tests/PlayMode/Phase6DecorationTouchPlayModeTests.cs`

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

- [ ] **Step 1: Write failing end-to-end controller tests**

Cover `P6-LC-001–008`、`P6-PRV-001–009`、`P6-SYNC-001–011`。Use fakes for Touch source and Pause service，real runtime views where integration matters。

- [ ] **Step 2: Run focused RED**

Expected RED: missing controller and integration bindings。

- [ ] **Step 3: Configure lifecycle ownership**

Controller creates / receives `DecorationSession`、acquires one `IUiPauseHandle` on enter、releases only its handle on exit / disable / destroy、calls `TryRestorePendingSpeed` according to Phase 5 coordinator contract。

`CafeLayoutRuntime.Initialize` builds once per Scene run from `FC_Phase6Production.asset`：`LayoutBounds(0,0,8,8)`、one unlocked Interior `8 × 8` region、the approved `2 × 2` Entrance reservation and one serialized stable initial `1 × 1 Counter Module` placement。Repeated `Initialize` returns the same runtime Layout；Scene reload creates the approved initial state again and writes no Save file。

- [ ] **Step 4: Route Catalogue and furniture selection**

Catalogue selection calls `BeginNew` at Camera-center nearest Grid cell。Furniture tap resolves Instance ID from `FurnitureSceneRegistry` and calls `BeginExisting`；if another Preview exists, domain auto-cancels first。

- [ ] **Step 5: Route drag, rotation and transactions**

Furniture-owned drag projects offset Touch to Floor plane → Grid position → `MovePreview` → update Preview / highlight / action bar。Rotate / Confirm / Cancel / Store events call exactly one domain operation and then rebuild formal representations when needed。

- [ ] **Step 6: Protect existing Scene interaction and Camera**

While Decoration Mode open, ordinary world selection must not process Decoration-owned pointers。Do not delete existing selection behavior；gate it through the shared pointer boundary / controller state so normal mode remains unchanged。

- [ ] **Step 7: Run focused GREEN and normal-mode regression**

Verify Decoration works and exiting restores Phase 0 Camera、selection、Time controls and Phase 5 UI behavior。

- [ ] **Step 8: Architecture checkpoint**

Review that controller coordinates but does not reimplement Layout legality、UI navigation、Camera bounds or content lookup。

---

### Task 8: Idempotent Scene setup, MainCafe migration and validator

**Files:**
- Create: `Assets/Editor/Phase6/Phase6DecorationSceneSetup.cs`
- Create: `Assets/Editor/Phase6/Phase6DecorationValidator.cs`
- Modify: `Assets/Scenes/MainCafe.unity` through Unity Editor setup tool
- Create: `Assets/Scenes/Validation/Phase6DecorationMode.unity` through Unity Editor setup tool
- Delete after the Step 6 consumer scan passes: `Assets/Editor/Phase4/MainCafeManualReviewFixtureSetup.cs`
- Delete after the Step 6 consumer scan passes: `Assets/Scripts/Diagnostics/ManualReviewPingPongMover.cs`
- Delete after the Step 6 consumer scan passes: `Assets/Tests/PlayMode/Phase4/ManualReviewPingPongMoverTests.cs`
- Delete after the Step 6 consumer scan passes: `Assets/Art/Phase4/Materials/M_TEMP_ManualReviewCube_Static.mat`
- Delete after the Step 6 consumer scan passes: `Assets/Art/Phase4/Materials/M_TEMP_ManualReviewCube_Moving.mat`
- Test: `Assets/Tests/EditMode/Phase6/Phase6DecorationValidatorTests.cs`
- Test: `Assets/Tests/EditMode/Phase6/Phase6MainCafeMigrationTests.cs`

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

- [ ] **Step 1: Write failing Editor contract tests**

Cover `P6-AUTH-001–010`。Assert canonical environment Prefab GUIDs、one Decoration owner、one initial `1 × 1 Counter` Layout entry、no temporary P4 fixture、Validation Scene disabled in Build Settings。

- [ ] **Step 2: Run focused RED**

Expected RED: missing setup / validator and unchanged MainCafe fixture state。

- [ ] **Step 3: Implement validator before migration**

Issue codes must identify duplicate / missing owner、Catalogue、Grid root、UI reference、Definition、thumbnail、Prefab、Build Settings scope、runtime Editor reference and environment Prefab drift。Report exact asset / Scene path。

- [ ] **Step 4: Implement idempotent Validation Scene setup**

Use Phase 4 environment Prefabs；create empty、occupied、blocked、locked、Entrance and edge fixtures；bind the same runtime Prefabs / Catalogue used by MainCafe。Run twice and assert canonical counts unchanged。

- [ ] **Step 5: Implement idempotent MainCafe migration**

Add Phase 6 runtime owner、Grid / representation roots、feature UI and initial `1 × 1 Counter` Layout binding under canonical Phase 5 / production hierarchy。Never hand-edit YAML；use Unity Editor APIs / live Editor tooling。

- [ ] **Step 6: Remove P4 temporary fixture only after consumer scan**

Before deletion, search exact asset GUID / type references in Scenes、Prefabs、Editor tools and tests。Delete only items whose sole consumer is `TEMP_P4_ManualReviewFixtures_DELETE_LATER` and add a regression assertion for every removed path。Retain anything still consumed by Phase 4 validation。

- [ ] **Step 7: Run setup twice, validator and Phase 4 / 5 migration regression**

Expected: issues `0`；one canonical hierarchy；Build Settings production scope unchanged。

- [ ] **Step 8: Scene checkpoint**

Open MainCafe and Validation Scene outside Play Mode。Review Hierarchy counts、Prefab links、no overrides drift and no unrelated serialized churn。

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

- [ ] **Step 5: Obtain explicit manual acceptance**

All cases must be PASS or a named limitation explicitly accepted by Studio Owner。Do not infer acceptance from silence。

- [ ] **Step 6: Manual checkpoint**

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

- [ ] **Step 1: Run fresh full EditMode**

Use Unity `6000.5.5f1`。Record XML / log、total、passed、failed、skipped、inconclusive、start / end time。

- [ ] **Step 2: Run fresh full Editor PlayMode**

Report initial result honestly。If canonical asset import / generation changes the first result，investigate and rerun only after recording cause and corrective action。

- [ ] **Step 3: Run fresh standalone runtime suite**

Verify player assembly、MainCafe loading and mobile-compatible input path；no `UnityEditor` dependency。

- [ ] **Step 4: Run Phase 4 and Phase 6 validators**

Expected issues `0`；record exact valid asset counts and Scene contracts。

- [ ] **Step 5: Review diff and working tree scope**

List every created / modified / removed path。Separate pre-existing user changes (`.gitignore`、`AnimalCafe.slnx`、apron / Westie docs and any later user work) from Phase 6 paths。Do not stage unrelated files。

- [ ] **Step 6: Request independent code review**

Invoke `superpowers:requesting-code-review`。Resolve actionable findings through focused failing tests；rerun affected regression。

- [ ] **Step 7: Write closeout summary**

Include player-visible result、automated counts、manual P6-M result、known limitations、performance observations、files / assets changed and GitHub Desktop handoff。

- [ ] **Step 8: Update Roadmap only after all gates pass**

Mark Phase 6 `Completed` only after approved spec、TDD、review、fresh full regression and Studio Owner manual acceptance。Set Current Next Step to Phase 7 design gate；do not begin Phase 7 automatically。

## Final execution boundary

Implementation may begin only after Studio Owner approves this plan and chooses an execution workflow。At execution start：

1. inspect current branch / worktree and unrelated local changes；
2. invoke `superpowers:using-git-worktrees` only if an isolated worktree is approved and required；
3. invoke `superpowers:test-driven-development` before Task 1 implementation；
4. execute one Task at a time with reviewer checkpoints；
5. do not commit、push、merge、clean branches or delete worktrees unless Studio Owner separately authorizes that exact action。
