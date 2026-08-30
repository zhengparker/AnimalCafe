# AnimalCafe Phase 7 — Interior Walls & Surface Customization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在现有 Decoration Mode 中加入 Floor、Wall 与 Wall Decor modes，让玩家安全预览并确认 Surface customization，以及放置、移动和 Store Wall Decor / Window。

**Architecture:** 保留 `CafeLayout` 与 Phase 6 Furniture flow，不重写已完成的 Floor Furniture transaction。新增 `RoomSurfaceLayout` 管理 stable Surface appearance，`WallMountedLayout` 聚合现有 `WallSurfaceLayout` 并提供跨墙 atomic placement，两个独立 Preview sessions 管理 Surface 与 wall-mounted transactions；`DecorationModeController` 只负责 lifecycle 与 Mode routing。现有 Bottom Sheet 升级为 Category rows + raised Mode Tabs，Scene views 只消费 confirmed/proposed snapshots，不直接写 Layout。

**Tech Stack:** Unity `6000.5.5f1`、C#、URP `17.5.0`、uGUI、TextMeshPro、Unity Input System、NUnit EditMode / PlayMode、Phase 2 Layout、Phase 4 Wall contracts、Phase 5 UI Foundation、Phase 6 Decoration Mode。

**Spec:** `Docs/superpowers/specs/2026-08-24-phase-7-interior-walls-surface-customization-design.md`

**Current status:** Phase 7 implementation、review amendments、final automated regression 与 Studio Owner manual acceptance 均已完成。2026-08-29 的剩余动作仅为已单独授权的 feature branch commit、push 与 merge PR；不授权 merge 或 branch/worktree cleanup。

## Global Constraints

- Production Scene 仍为 `Assets/Scenes/MainCafe.unity`；Phase 7 Validation Scene 不进入 player Build Settings。
- Runtime assembly 不引用 `UnityEditor`。
- Mobile Touch 是正式 interaction contract；Mouse 只作为 Unity Editor test mapping。
- 四个 Mode Tabs 固定为 `Furniture / Floor / Wall / Wall Decor`；Mode hit testing 互相隔离。
- Surface Preview、Wall-mounted Preview、Undo、Cancel 不得修改 confirmed Layout；只有 Confirm 提交。
- Wall 使用 one-target multi-layer transaction：Paint/Wallpaper 共用 Base slot，Wainscoting 独立；Wall 不提供 Apply All，Confirm / Cancel 原子提交或恢复完整墙面组合。
- Floor Single Grid 必须显示 Selected Grid highlight 与所有 Previewed Grids 的 Scene check；Floor ArmedStyle、Rotate、Undo Last 与 Apply All 保留。
- Floor / Wall actions 使用 Bottom Sheet 内 fixed footer；Compact Preview 保留 Tabs + footer，使用 `0.16s` transition，footer 不得脱离 Sheet hierarchy。
- Wainscoting 不得出现 procedural crosshatch 或 fence-like independent shadow；Wall Decor ghost 必须使用真实 prefab 且垂直地面、平行 target Wall。
- Paint 或 Wallpaper 二选一作为 Wall Base；Wainscoting 是 optional overlay；Wall Decor / Window 不因 Surface change 被移除。
- Floor texture tile 对应 `1 m × 1 m`；Wall texture 横向 tile 对应 `1 m`；import wrap mode 必须为 Repeat。
- Wainscoting world height 使用 project-approved shared waist reference；normalized cutoff 从 canonical wall 与该 shared waist reference 派生，不得由不同 texture 自行改变。
- Wall-mounted footprint 使用 integer `Width × Height`；初始墙为 `8 × 2` Slots；不能 overlap、out of bounds 或 cross-corner。
- 普通 Wall Decor visual depth 不超过 `0.35 m`，不占 Floor Grid、不阻挡 Navigation、不提供 Rotate。
- 首批素材无限使用，无 price、currency、inventory count 或 unlock。
- Save / Load implementation 属于 Phase 17；Phase 7 只建立 stable IDs 与 serializable snapshot contract。
- 按 TDD 执行可信 RED → minimal GREEN → direct regression；完整 regression 集中在 Phase 收尾。
- 不 commit、push、merge、删除 branch / worktree；Studio Owner 使用 GitHub Desktop 管理版本控制。
- 用户现有 `.gitignore`、`AnimalCafe.slnx`、Westie / apron docs 与其他无关改动必须保留。

## Planned File Structure

### Runtime domain

- `Assets/Scripts/Layout/SurfaceRotation.cs` — Floor visual rotation `0 / 90 / 180 / 270`。
- `Assets/Scripts/Layout/WallAppearance.cs` — one Wall 的 Base + optional Wainscoting confirmed value。
- `Assets/Scripts/Layout/FloorTileAppearance.cs` — one Floor Grid 的 style + rotation confirmed value。
- `Assets/Scripts/Layout/RoomSurfaceLayout.cs` — current Room 的 Wall / Floor appearance Source of Truth。
- `Assets/Scripts/Layout/RoomSurfaceSnapshot.cs` — production `[Serializable]` data-only Room Surface snapshot；使用 serializable ordered Wall/Floor entries，不是 test-only wrapper，也不实现 Phase 17 UI/storage。
- `Assets/Scripts/Layout/WallMountedLayout.cs` — multiple `WallSurfaceLayout` aggregation、global Instance lookup 与跨墙 atomic move。
- `Assets/Scripts/Layout/WallMountedLayoutSnapshot.cs` — production `[Serializable]` data-only Wall-mounted snapshot；使用 serializable ordered Surface/Instance attachment entries 重建 occupancy，不是 test-only wrapper，也不实现 Phase 17 UI/storage。
- `Assets/Scripts/Layout/WallSurfaceLayout.cs` — 增加 non-mutating validation / footprint query，不破坏 Phase 4 APIs。

### Runtime content

- `Assets/Scripts/Content/SurfaceStyleDefinitionAsset.cs` — Surface kind、Material、thumbnail、None contract。
- `Assets/Scripts/Content/SurfaceStyleCatalogueAsset.cs` — Wallpaper / Paint / Wainscoting / Floor category entries。
- `Assets/Scripts/Content/WallMountedCatalogueAsset.cs` — Wall Decor / Windows category entries。
- `Assets/Scripts/Content/WallMountedDefinitionAsset.cs` — 增加 thumbnail 与 max visual depth authoring metadata；保留 existing Window compatibility。

### Runtime transactions

- `Assets/Scripts/Decoration/DecorationModeKind.cs` — four Mode values。
- `Assets/Scripts/Decoration/SurfaceEditScope.cs` — Wall、Whole Room Floor、Single Grid Floor。
- `Assets/Scripts/Decoration/SurfaceSessionResult.cs` — Surface transaction failure/result contract。
- `Assets/Scripts/Decoration/SurfacePreviewTransaction.cs` — proposed snapshots、Wall Base/Wainscoting states、Floor selected/armed/previewed states 与 undo view。
- `Assets/Scripts/Decoration/SurfaceDecorationSession.cs` — Wall multi-layer Begin / Select / atomic Confirm / Cancel；Floor Select / Rotate / ApplyAll / Undo / Confirm / Cancel。
- `Assets/Scripts/Decoration/WallMountedPlacementPreview.cs` — immutable wall-mounted Preview。
- `Assets/Scripts/Decoration/WallMountedDecorationSession.cs` — new / existing / move-across-wall / Store transaction。

### Runtime scene and UI

- `Assets/Scripts/Decoration/WallSurfaceRegistry.cs` — stable Surface ID → authoring / renderer lookup。
- `Assets/Scripts/Decoration/WallSurfaceView.cs` — layered Wall MaterialPropertyBlock preview / confirmed rendering。
- `Assets/Scripts/Decoration/FloorSurfaceGridView.cs` — 64 render-only tiles + selected highlight / preview checks；不替换 canonical Floor Collider。
- `Assets/Scripts/Decoration/WallMountedSceneRegistry.cs` — Instance ID → confirmed Scene representation。
- `Assets/Scripts/Decoration/WallMountedPreviewView.cs` — wall-local real-prefab ghost、depth offset、Valid / Invalid projection。
- `Assets/Scripts/Decoration/WallOcclusionFadeView.cs` — reversible blocker fade。
- `Assets/Scripts/Decoration/DecorationModeController.cs` — Mode routing 与 shared lifecycle integration。
- `Assets/Scripts/Decoration/Input/DecorationTouchRouter.cs` — Mode-aware Scene hit ownership。
- `Assets/Scripts/UI/Decoration/DecorationCatalogueModels.cs` — Category / Item presentation DTOs。
- `Assets/Scripts/UI/Decoration/DecorationModeTabsView.cs` — raised tabs、active-front sibling order。
- `Assets/Scripts/UI/Decoration/DecorationCatalogueView.cs` — vertical categories + horizontal item rows + three snap states + Surface footer host。
- `Assets/Scripts/UI/Decoration/DecorationCatalogueTileView.cs` — Furniture name-only 与 Surface image-only states。
- `Assets/Scripts/UI/Decoration/DecorationActionBarView.cs` — Mode-aware actions；Surface Modes attach to Catalogue footer，Wall matrix excludes Apply All。
- `Assets/Scripts/UI/Decoration/DecorationExitModalView.cs` — Continue Editing / Discard Changes。

### Editor integration and assets

- `Assets/Editor/Phase7/Phase7AssetPaths.cs`
- `Assets/Editor/Phase7/Phase7SurfaceAssetBuilder.cs`
- `Assets/Editor/Phase7/Phase7DecorationSceneSetup.cs`
- `Assets/Editor/Phase7/Phase7Validator.cs`
- `Assets/Scenes/Validation/Phase7InteriorWalls.unity`
- `Assets/Art/Phase7/Shaders/SH_WallSurfaceLayered.shader`
- `Assets/Art/Phase7/Definitions/` — Surface / Wall-mounted definitions。
- `Assets/Art/Phase7/Catalogues/` — Surface and Wall-mounted catalogues。
- `Assets/UI/Phase7/Prefabs/` — upgraded Bottom Sheet、Action Bar、Exit Modal。
- `Assets/UI/Phase7/Thumbnails/` — deterministic previews；formal model thumbnails are regenerated after model intake。

### Tests

- `Assets/Tests/EditMode/Phase7/RoomSurfaceLayoutTests.cs`
- `Assets/Tests/EditMode/Phase7/WallMountedLayoutTests.cs`
- `Assets/Tests/EditMode/Phase7/SurfaceDecorationSessionTests.cs`
- `Assets/Tests/EditMode/Phase7/WallMountedDecorationSessionTests.cs`
- `Assets/Tests/EditMode/Phase7/Phase7CatalogueTests.cs`
- `Assets/Tests/EditMode/Phase7/Phase7AssetBuilderTests.cs`
- `Assets/Tests/EditMode/Phase7/Phase7ValidatorTests.cs`
- `Assets/Tests/EditMode/Phase7/Phase7MainCafeMigrationTests.cs`
- `Assets/Tests/PlayMode/Phase7DecorationUiPlayModeTests.cs`
- `Assets/Tests/PlayMode/Phase7SurfaceScenePlayModeTests.cs`
- `Assets/Tests/PlayMode/Phase7WallMountedTouchPlayModeTests.cs`
- `Assets/Tests/PlayMode/EditorSceneLoading/Phase7MainCafeSceneTests.cs`

---

### Task 1: Stable Room Surface data model

**Files:**
- Create: `Assets/Scripts/Layout/SurfaceRotation.cs`
- Create: `Assets/Scripts/Layout/WallAppearance.cs`
- Create: `Assets/Scripts/Layout/FloorTileAppearance.cs`
- Create: `Assets/Scripts/Layout/RoomSurfaceLayout.cs`
- Create: `Assets/Scripts/Layout/RoomSurfaceSnapshot.cs`
- Test: `Assets/Tests/EditMode/Phase7/RoomSurfaceLayoutTests.cs`

**Interfaces:**

```csharp
public enum SurfaceRotation { Degrees0, Degrees90, Degrees180, Degrees270 }

public readonly struct WallAppearance
{
    public string SurfaceId { get; }
    public string BaseStyleId { get; }
    public string WainscotingStyleId { get; } // null means No Wainscoting
}

public readonly struct FloorTileAppearance
{
    public GridPosition Position { get; }
    public string StyleId { get; }
    public SurfaceRotation Rotation { get; }
}

[Serializable]
public sealed class WallAppearanceSnapshotEntry
{
    public string SurfaceId;
    public string BaseStyleId;
    public string WainscotingStyleId;
}

[Serializable]
public sealed class FloorTileAppearanceSnapshotEntry
{
    public int X;
    public int Y;
    public string StyleId;
    public SurfaceRotation Rotation;
}

[Serializable]
public sealed class RoomSurfaceSnapshot
{
    public string RoomId;
    public List<WallAppearanceSnapshotEntry> Walls;
    public List<FloorTileAppearanceSnapshotEntry> FloorTiles;
}

// Walls use SurfaceId ordinal order；FloorTiles use deterministic GridPosition order.

public sealed class RoomSurfaceLayout
{
    public string RoomId { get; }
    public IReadOnlyDictionary<string, WallAppearance> Walls { get; }
    public IReadOnlyDictionary<GridPosition, FloorTileAppearance> FloorTiles { get; }
    public bool TryGetWall(string surfaceId, out WallAppearance value);
    public bool TryGetFloor(GridPosition position, out FloorTileAppearance value);
    public void ReplaceWall(WallAppearance value);
    public void ReplaceFloor(FloorTileAppearance value);
    public void ReplaceAllFloors(string styleId, SurfaceRotation rotation);
    public RoomSurfaceSnapshot CaptureSnapshot();
    public static RoomSurfaceLayout FromSnapshot(RoomSurfaceSnapshot snapshot);
}
```

- [x] **Step 1: Write RED tests** covering stable ID validation、two Walls、64 Floor tiles、No Wainscoting null contract、per-tile rotation、whole-room replace、read-only collection exposure，以及 `CaptureSnapshot → serialize/deserialize → RoomSurfaceLayout.FromSnapshot` value equivalence。Snapshot 包含 RoomId、按 SurfaceId ordinal 排序的 WallAppearance entries 与按 deterministic GridPosition order 排序的 exactly 64 FloorTileAppearance entries；重复 capture 的 ordered entries 与 serialized text 一致。
- [x] **Step 2: Run** EditMode filter `AnimalCafe.Tests.Phase7.RoomSurfaceLayoutTests`。可信 RED：缺少 `RoomSurfaceSnapshot` contract 时产生 `CS0246`。
- [x] **Step 3: Implement minimal immutable value types、defensive dictionaries and the production `[Serializable]` data-only `RoomSurfaceSnapshot`.** Constructor 与 `FromSnapshot` 共享 normal validation。
- [x] **Step 4: Prove failed validation is non-mutating / atomic** with before/after inputs；invalid snapshot never returns or exposes a partial Layout；`ReplaceAllFloors` updates exactly 64 existing keys。
- [x] **Step 5: Run snapshot focused GREEN** for `CaptureSnapshot → serialize/deserialize → FromSnapshot`；RoomId、ordered Walls、ordered 64 Floors、styles and rotations value-equivalent。
- [x] **Step 6: Run focused GREEN** plus `AnimalCafe.Tests.Phase4.WallSurfaceLayoutTests` and `AnimalCafe.Tests.CafeLayoutTests`。Final focused `47/47`；direct regressions `24/24` and `31/31`；failed / skipped / inconclusive `0`。
- [x] **Step 7: Review checkpoint** — independent Task review Approved after three test-coverage findings were fixed；stable IDs、snapshot validation and null Wainscoting semantics match the frozen matrix。

---

### Task 2: Cross-wall non-mutating validation and atomic Wall-mounted layout

**Files:**
- Modify: `Assets/Scripts/Layout/WallSurfaceLayout.cs`
- Modify: `Assets/Scripts/Layout/WallMountedInstance.cs`
- Create: `Assets/Scripts/Layout/WallMountedLayout.cs`
- Create: `Assets/Scripts/Layout/WallMountedLayoutSnapshot.cs`
- Test: `Assets/Tests/EditMode/Phase7/WallMountedLayoutTests.cs`
- Regression: `Assets/Tests/EditMode/Phase4/WallSurfaceLayoutTests.cs`

**Interfaces:**

```csharp
public WallPlacementResult ValidatePlacement(
    WallMountedInstance item,
    string ignoredItemId = null);
public IReadOnlyList<WallSlotPosition> GetFootprintSlots(WallMountedInstance item);

// Internal immutable replacement used only after destination validation.
internal WallMountedInstance WithPlacement(
    string surfaceId, WallSlotPosition position);

[Serializable]
public sealed class WallMountedSurfaceSnapshotEntry
{
    public string SurfaceId;
    public int Columns;
    public int Rows;
}

[Serializable]
public sealed class WallMountedInstanceSnapshotEntry
{
    public string InstanceId;
    public string DefinitionId;
    public string SurfaceId;
    public int Column;
    public int Row;
    public int FootprintWidth;
    public int FootprintHeight;
}

[Serializable]
public sealed class WallMountedLayoutSnapshot
{
    public List<WallMountedSurfaceSnapshotEntry> Surfaces;
    public List<WallMountedInstanceSnapshotEntry> Instances;
}

// Surface entries include stable SurfaceId and Slot dimensions.
// Instance entries include InstanceId、DefinitionId、SurfaceId、Slot and Footprint.
// Surfaces and Instances use deterministic stable-ID ordinal order.

public sealed class WallMountedLayout
{
    public IReadOnlyDictionary<string, WallSurfaceLayout> Surfaces { get; }
    public WallPlacementResult ValidatePlacement(
        string definitionId, string surfaceId, WallSlotPosition position,
        WallFootprint footprint, string ignoredInstanceId = null);
    public WallPlacementResult Place(WallMountedInstance item);
    public WallPlacementResult Move(
        string instanceId, string destinationSurfaceId, WallSlotPosition position);
    public WallPlacementResult Remove(string instanceId);
    public bool TryGetInstance(string instanceId, out WallMountedInstance item);
    public WallMountedLayoutSnapshot CaptureSnapshot();
    public static WallMountedLayout FromSnapshot(WallMountedLayoutSnapshot snapshot);
}
```

- [x] **Step 1: Write RED tests** for all frozen `AT-011–AT-022` and `AT-066` behaviors, including all five footprint matrices、atomic rollback and serializable snapshot rebuild。
- [x] **Step 2: Run** `AnimalCafe.Tests.Phase7.WallMountedLayoutTests`。可信 RED：缺少 `WallMountedLayout` / snapshot contracts 时产生 `CS0246`。
- [x] **Step 3: Extract `WallSurfaceLayout.ValidatePlacement`**；`TryPlace` and `TryMove` delegate to it，Phase 4 result ordering preserved。
- [x] **Step 4: Implement immutable cross-surface candidate and atomic Move.** Destination validates first；exact ordered source/destination state restores on false、partial mutation or exception。
- [x] **Step 5: Implement production `[Serializable]` data-only `WallMountedLayoutSnapshot` and atomic `FromSnapshot`.** All candidates validate before Layout exposure；invalid input produces no partial occupancy。
- [x] **Step 6: Run snapshot focused GREEN** for deterministic capture/serialize/deserialize/rebuild and invalid duplicate/attachment/overlap cases。
- [x] **Step 7: Run focused GREEN** plus all `WallSurfaceLayoutTests` and Phase 4 Wall validator tests。Final focused `108/108`；regressions `24/24` and `91/91`；failed/skipped/inconclusive `0`。
- [x] **Step 8: Review checkpoint** — independent review Approved after mutable-alias、global uniqueness、exact rollback and frozen coverage findings were fixed。

---

### Task 3: Surface definitions and typed Catalogues

**Files:**
- Create: `Assets/Scripts/Content/SurfaceStyleDefinitionAsset.cs`
- Create: `Assets/Scripts/Content/SurfaceStyleCatalogueAsset.cs`
- Create: `Assets/Scripts/Content/WallMountedCatalogueAsset.cs`
- Modify: `Assets/Scripts/Content/WallMountedDefinitionAsset.cs`
- Create: `Assets/Scripts/UI/Decoration/DecorationCatalogueModels.cs`
- Test: `Assets/Tests/EditMode/Phase7/Phase7CatalogueTests.cs`

**Interfaces:**

```csharp
public enum SurfaceStyleKind { Paint, Wallpaper, Wainscoting, Floor }

public sealed class SurfaceStyleDefinitionAsset : ScriptableObject
{
    public string StyleId { get; }
    public string DisplayName { get; }
    public SurfaceStyleKind Kind { get; }
    public Material Material { get; }
    public Sprite Thumbnail { get; }
    public bool IsNoneOption { get; }
}

public enum DecorationCatalogueItemKind { Furniture, Floor, WallSurface, WallMounted }
public sealed class DecorationCategoryModel
{
    public string CategoryId { get; }
    public string DisplayName { get; }
    public IReadOnlyList<DecorationCatalogueItemModel> Items { get; }
}
```

- [x] **Step 1: Write tests** requiring exact categories `Furniture`、`Floor`、`Wallpaper`、`Paint`、`Wainscoting`、`Wall Decor`、`Windows` and stable ordering。
- [x] **Step 2: Add invalid-contract tests:** Wainscoting-only None、required Material/Sprite、Prefab/thumbnail/footprint、finite depth `<= 0.35f`、stable IDs and deterministic issue codes。
- [x] **Step 3: Run** `AnimalCafe.Tests.Phase7.Phase7CatalogueTests`。Original chronological RED was blocked by Licensing；post-implementation sensitivity and review-fix RED are preserved and not mislabelled。
- [x] **Step 4: Implement typed assets and pure defensive read-only presentation DTO mapping.** No `SerializeReference`；Task 9 retains production asset migration/validator ownership。
- [x] **Step 5: Run focused GREEN** plus Phase 6 Catalogue and Phase 4 Window regressions。Final focused `13/13`；regressions `8/8` and `22/22`；failed/skipped/inconclusive `0`。
- [x] **Step 6: Review checkpoint** — independent review Approved；no price、quantity、unlock or Save fields；production asset evidence explicitly deferred to Task 9。

---

### Task 4: Surface Preview transaction and undo

> Baseline complete. Its Wall-layer-specific `BeginWall(surfaceId, layer)` and Wall ApplyAll behavior are superseded only by Task 12. Existing Floor behavior remains the baseline for Task 13.

**Files:**
- Create: `Assets/Scripts/Decoration/SurfaceEditScope.cs`
- Create: `Assets/Scripts/Decoration/SurfaceSessionResult.cs`
- Create: `Assets/Scripts/Decoration/SurfacePreviewTransaction.cs`
- Create: `Assets/Scripts/Decoration/SurfaceDecorationSession.cs`
- Test: `Assets/Tests/EditMode/Phase7/SurfaceDecorationSessionTests.cs`

**Interfaces:**

```csharp
public enum SurfaceEditScope { Wall, WholeRoomFloor, SingleGridFloor }
public enum SurfaceSessionFailure
{
    None, NoActivePreview, ActivePreviewMustFinish,
    UnknownTarget, UnknownStyle, WrongStyleKind
}

public readonly struct SurfaceSessionResult
{
    public bool Succeeded { get; }
    public SurfaceSessionFailure FailureReason { get; }
}

public sealed class SurfaceDecorationSession
{
    public SurfacePreviewTransaction ActivePreview { get; }
    public SurfaceSessionResult BeginWall(string surfaceId, SurfaceStyleKind layer);
    public SurfaceSessionResult BeginWholeRoomFloor();
    public SurfaceSessionResult BeginSingleGridFloor(GridPosition position);
    public SurfaceSessionResult SelectStyle(string styleId);
    public SurfaceSessionResult SelectFloorGrid(GridPosition position);
    public SurfaceSessionResult RotateFloor();
    public SurfaceSessionResult ApplyAll();
    public bool UndoLast();
    public SurfaceSessionResult Confirm();
    public void Cancel();
}
```

- [x] **Step 1: Write RED tests** for frozen `AT-035–AT-050` plus Task 4 portions of `AT-051/052`，including Wall semantic Using/Preview IDs and immutable point-in-time Preview views。
- [x] **Step 2: Add transaction tests** proving confirmed Layout unchanged before Confirm；Cancel/Undo complete；Apply All one step；atomic `ApplySnapshot` preserves stable Room/Wall identities。
- [x] **Step 3: Run** `AnimalCafe.Tests.Phase7.SurfaceDecorationSessionTests` and observe missing-type RED；review fixes additionally preserve three trustworthy RED sequences。
- [x] **Step 4: Implement session-owned copied snapshots and complete undo state.** Exposed `SurfacePreviewTransaction` is an immutable defensive view；Confirm uses one aggregate atomic state swap。
- [x] **Step 5: Enforce range/target/style gates** with non-mutation；constructor validates and freezes typed style bindings。
- [x] **Step 6: Run focused GREEN** and RoomSurfaceLayout regression。Final Task 4 `27/27`、Task 1 `51/51`；failed/skipped/inconclusive `0`；independent review Approved。

---

### Task 5: Wall-mounted Preview transaction

**Files:**
- Create: `Assets/Scripts/Decoration/WallMountedPlacementPreview.cs`
- Create: `Assets/Scripts/Decoration/WallMountedDecorationSession.cs`
- Modify: `Assets/Scripts/Decoration/PlacementFeedbackMapper.cs`
- Test: `Assets/Tests/EditMode/Phase7/WallMountedDecorationSessionTests.cs`

**Interfaces:**

```csharp
public sealed class WallMountedDecorationSession
{
    public WallMountedPlacementPreview ActivePreview { get; }
    public void BeginNew(string definitionId, string preferredSurfaceId,
        WallSlotPosition preferredPosition);
    public WallPlacementResult BeginExisting(string instanceId);
    public WallPlacementResult MovePreview(string surfaceId, WallSlotPosition position);
    public WallPlacementResult ConfirmPreview();
    public void CancelPreview();
    public bool BeginStoreConfirmation();
    public WallPlacementResult ConfirmStore();
}
```

- [x] **Step 1: Write RED tests** mirroring Phase 6 Furniture lifecycle without Rotate：direct nearest deterministic Slot、drag、cross-wall drag、invalid corner gap、Confirm、Cancel、existing move and Store。
- [x] **Step 2: Add nearest-slot tie tests:** Manhattan distance first，then stable Surface ID ordinal，then Column，then Row；this prevents Camera-corner nondeterminism。
- [x] **Step 3: Run** `AnimalCafe.Tests.Phase7.WallMountedDecorationSessionTests` and observe missing-type RED。
- [x] **Step 4: Implement immutable Preview and session** using `WallMountedLayout.ValidatePlacement`; never temporarily remove the confirmed source instance during drag。
- [x] **Step 5: Extend feedback mapping** to exact keys `WallOverlap`、`WallOutOfBounds`、`WallCrossCorner`、`WallSurfaceMissing`；Confirm disabled for every failure。
- [x] **Step 6: Run focused GREEN** plus Phase 6 `DecorationSessionTests`，confirm Furniture rotation and Store behavior are unchanged。

---

### Task 6: Multi-Mode Catalogue, raised Tabs and Action Bar

> Baseline complete. Surface Action Bar placement and Wall button matrix are superseded by Task 14；Furniture / Wall Decor lifecycle remains unchanged。

**Files:**
- Create: `Assets/Scripts/Decoration/DecorationModeKind.cs`
- Create: `Assets/Scripts/UI/Decoration/DecorationModeTabsView.cs`
- Modify: `Assets/Scripts/UI/Decoration/DecorationCatalogueView.cs`
- Modify: `Assets/Scripts/UI/Decoration/DecorationCatalogueTileView.cs`
- Modify: `Assets/Scripts/UI/Decoration/DecorationActionBarView.cs`
- Create: `Assets/Scripts/UI/Decoration/DecorationExitModalView.cs`
- Test: `Assets/Tests/PlayMode/Phase7DecorationUiPlayModeTests.cs`
- Regression: `Assets/Tests/PlayMode/Phase6DecorationUiPlayModeTests.cs`

**Interfaces:**

```csharp
public enum DecorationSheetState { Hidden, Expanded, CompactPreview, TabsOnly }

public sealed class DecorationModeTabsView : MonoBehaviour
{
    public event Action<DecorationModeKind> Selected;
    public DecorationModeKind ActiveMode { get; }
    public void SetActive(DecorationModeKind mode);
}

public void BindCategories(
    IReadOnlyList<DecorationCategoryModel> categories,
    Action<DecorationCatalogueItemModel> selected);
public void SetSheetState(DecorationSheetState state, bool hasActivePreview);
public void SetSurfaceState(string usingItemId, string previewItemId);
```

- [x] **Step 1: Write UI RED tests** for four Tabs、default Furniture、active Tab highest sibling index、48 px minimum hit target、three snap states and refusal of TabsOnly while Preview active。
- [x] **Step 2: Add Catalogue RED tests** for vertical Category rows、horizontal ScrollRects、partial-next-card viewport、Furniture name-only、Surface image-only、central Using check、Preview outline and Wainscoting None icon。
- [x] **Step 3: Add Action RED tests** for Floor、Wall、Furniture、new Wall-mounted and existing Wall-mounted exact button matrices；critical actions cannot be hidden in overflow。
- [x] **Step 4: Run** `AnimalCafe.Tests.Phase7.Phase7DecorationUiPlayModeTests` and observe missing API / hierarchy RED。
- [x] **Step 5: Implement views incrementally** using Phase 5 Theme colors and Figma node `38:3` as visual reference；direction-lock nested ScrollRects so horizontal card swipe does not move vertical categories。
- [x] **Step 6: Implement exit modal** with only `Continue Editing` and `Discard Changes`; closing it cannot leak the same pointer into Scene。
- [x] **Step 7: Run focused GREEN** plus complete Phase 6 UI tests；record any intentional prefab hierarchy migration in Task 9 rather than weakening old assertions。

---

### Task 7: Surface rendering, Wall projection and reversible fade

> Baseline complete. Floor Scene markers、Wainscoting normal/shadow correction and real-prefab ghost pose are amended by Tasks 13、15、16。

**Files:**
- Create: `Assets/Art/Phase7/Shaders/SH_WallSurfaceLayered.shader`
- Create: `Assets/Scripts/Decoration/WallSurfaceRegistry.cs`
- Create: `Assets/Scripts/Decoration/WallSurfaceView.cs`
- Create: `Assets/Scripts/Decoration/FloorSurfaceGridView.cs`
- Create: `Assets/Scripts/Decoration/WallMountedSceneRegistry.cs`
- Create: `Assets/Scripts/Decoration/WallMountedPreviewView.cs`
- Create: `Assets/Scripts/Decoration/WallOcclusionFadeView.cs`
- Test: `Assets/Tests/PlayMode/Phase7SurfaceScenePlayModeTests.cs`

**Interfaces:**

```csharp
public void RenderConfirmed(RoomSurfaceLayout layout);
public void RenderPreview(SurfacePreviewTransaction preview);
public void ClearPreview();

public void ShowWallPreview(
    WallMountedPlacementPreview preview,
    WallSurfaceAuthoring surface,
    bool isValid,
    PlacementFeedbackKey feedback);
public void RestoreAllFades();
```

- [x] **Step 1: Write RED Scene tests** requiring Wallpaper horizontal tiling equals Wall columns、vertical tiling `1`、Wainscoting height matches the project-approved shared waist reference、Floor 64 render tiles、per-grid UV rotation and no added Collider / Nav obstacle。
- [x] **Step 2: Add projection tests** for green + check Valid、red + cross Invalid、exact Slot footprint size、no z-fighting and no projection on a different Surface ID。
- [x] **Step 3: Add fade recovery tests** proving target stays opaque、only ray blockers fade、mode exit / disable / exception cleanup restores original MaterialPropertyBlocks and opacity。
- [x] **Step 4: Run** `AnimalCafe.Tests.Phase7.Phase7SurfaceScenePlayModeTests` and observe missing components / shader RED。
- [x] **Step 5: Implement layered Wall shader** with Base color/map、optional Wainscoting map and normalized cutoff derived from the canonical wall and project-approved shared waist reference；use MaterialPropertyBlock so source Materials remain unchanged。
- [x] **Step 6: Implement Floor visual tiles** as render-only children under the canonical Floor；the existing single Floor Collider and Grid coordinate mapping remain authoritative。
- [x] **Step 7: Run focused GREEN** and Phase 4 environment + Phase 6 Scene regression；capture one representative screenshot only after tests pass。

---

### Task 8: Mode routing, input isolation and controller integration

**Files:**
- Modify: `Assets/Scripts/Decoration/DecorationModeController.cs`
- Modify: `Assets/Scripts/Decoration/Input/DecorationTouchRouter.cs`
- Modify: `Assets/Scripts/Decoration/Input/DecorationTouchFrame.cs`
- Modify: `Assets/Scripts/Decoration/CafeLayoutRuntime.cs`
- Test: `Assets/Tests/PlayMode/Phase7WallMountedTouchPlayModeTests.cs`
- Regression: `Assets/Tests/PlayMode/Phase6DecorationTouchPlayModeTests.cs`

**Interfaces:**

```csharp
public DecorationModeKind ActiveMode { get; }
public bool TryChangeMode(DecorationModeKind mode);
public bool TryRequestExit();

public enum DecorationSceneHitKind
{
    None, FloorGrid, Furniture, WallSurface, WallSlot, WallMounted
}
```

- [x] **Step 1: Write RED routing tests** proving each Mode accepts only its declared hit kinds；unsupported Scene hits are ignored and never cancel Preview。
- [x] **Step 2: Add gesture tests:** Floor short tap selects Grid、Floor drag pans Camera、Wall tap selects whole Surface、Wall-mounted drag crosses Walls、UI pointer never reaches Scene。
- [x] **Step 3: Add lifecycle tests:** enter defaults Furniture；same-session Tab memory；active Preview blocks Tab/range/target switch；exit opens Discard modal；Pause and Camera state always restore once。
- [x] **Step 4: Run** Phase 7 touch filter and observe missing Mode routing RED。
- [x] **Step 5: Integrate sessions through focused private handlers** `HandleFurnitureFrame`、`HandleFloorFrame`、`HandleWallFrame`、`HandleWallMountedFrame`; shared controller owns only lifecycle and delegates Mode behavior。
- [x] **Step 6: Initialize `RoomSurfaceLayout` and `WallMountedLayout`** from canonical MainCafe authoring IDs without altering the source Scene during Preview。
- [x] **Step 7: Run focused GREEN** plus full Phase 6 Touch / Scene tests；failed / skipped / inconclusive must be `0` before Task 9。

---

### Task 9: Deterministic assets, prefab migration and validators

**Files:**
- Create: `Assets/Editor/Phase7/Phase7AssetPaths.cs`
- Create: `Assets/Editor/Phase7/Phase7SurfaceAssetBuilder.cs`
- Create: `Assets/Editor/Phase7/Phase7DecorationSceneSetup.cs`
- Create: `Assets/Editor/Phase7/Phase7Validator.cs`
- Modify: `Docs/Phase7_Wall_Surface_Texture_Authoring_Guide.md`
- Create: `Assets/Scenes/Validation/Phase7InteriorWalls.unity`
- Create: `Assets/UI/Phase7/Prefabs/PF_UI_Phase7DecorationCatalogue.prefab`
- Create: `Assets/UI/Phase7/Prefabs/PF_UI_Phase7DecorationActionBar.prefab`
- Create: `Assets/UI/Phase7/Prefabs/PF_UI_Phase7DecorationExitModal.prefab`
- Modify: `Assets/Scenes/MainCafe.unity` through the idempotent setup tool only
- Test: `Assets/Tests/EditMode/Phase7/Phase7AssetBuilderTests.cs`
- Test: `Assets/Tests/EditMode/Phase7/Phase7ValidatorTests.cs`
- Test: `Assets/Tests/EditMode/Phase7/Phase7MainCafeMigrationTests.cs`

**Interfaces:**

```csharp
public static void BuildOrUpdateAssets();
public static void ConfigureValidationScene();
public static void MigrateMainCafe();
public static Phase7ValidationReport ValidateAll();
```

- [x] **Step 1: Write idempotency RED tests** that run builder and setup twice and assert one of each Catalogue、Prefab、controller、registry、two Wall Surface IDs and one Floor Surface root。
- [x] **Step 2: Write validator RED tests** for texture Repeat、one-grid dimensions metadata、Material kind、Wainscoting None、cutoff derived from the canonical wall and project-approved shared waist reference、Surface IDs、Wall Slots、depth `<=0.35 m`、thumbnail presence and Build Settings exclusion。
- [x] **Step 3: Run Phase 7 builder / validator filters** and observe missing builder RED。
- [x] **Step 4: Extend the authoring guide and build Floor assets.** Add the approved `1 m × 1 m`、four-edge seamless、same-rotation repeat and rotated-boundary rules to `Docs/Phase7_Wall_Surface_Texture_Authoring_Guide.md`，then build the three approved Floor textures / Materials under `Assets/Art/Phase7/Textures` and `Materials`；create deterministic thumbnails for all existing Surface styles。
- [x] **Step 5: Build UI prefabs from the approved Figma structure** and migrate MainCafe references without replacing canonical Phase 4 Floor、Walls、Window、Entrance or Phase 6 Furniture roots。
- [x] **Step 6: Use labeled placeholder fixtures** for the three Wall Decor production definitions until Studio Owner models arrive；the canonical Phase 4 Window remains functional until the formal Window visual is integrated。
- [x] **Step 7: Run focused GREEN** and call validator twice；second run must produce zero asset or Scene diff。

---

### Task 10: Formal model intake and Art integration gate

**Files:**
- Create after user delivery: `Assets/Art/Phase7/Prefabs/PF_WallDecor_1x1_01.prefab`
- Create after user delivery: `Assets/Art/Phase7/Prefabs/PF_WallDecor_2x1_01.prefab`
- Create after user delivery: `Assets/Art/Phase7/Prefabs/PF_WallDecor_1x2_01.prefab`
- Create after user delivery: `Assets/Art/Phase7/Prefabs/PF_Window_01.prefab`
- Create: matching Definitions and thumbnails under `Assets/Art/Phase7/Definitions` and `Assets/UI/Phase7/Thumbnails`
- Modify: `Assets/Art/Phase7/Catalogues/WMC_Phase7Production.asset`
- Test: `Assets/Tests/EditMode/Phase7/Phase7ValidatorTests.cs`

**Interfaces:** Prefabs preserve author model source, use one stable root, wall plane at local `z = 0`, visible bounds inside declared Width / Height, visual depth `<= 0.35 m`, selection Collider only, no Navigation obstacle。

- [x] **Step 1: Inspect delivered models** for scale、orientation、Materials、texture licenses and visible bounds before copying or modifying them。
- [x] **Step 2: Create Prefab wrappers** and correct Pivot in the wrapper transform rather than destructively editing source Mesh。
- [x] **Step 3: Add selection Colliders** on the selectable layer；exclude them from character Navigation / physical blocking layers。
- [x] **Step 4: Author mounted thumbnails** from the five production Prefabs inside one deterministic warm in-game wall vignette；bind the committed `256×256` Sprites to Definitions and Catalogue entries。The normal builder/runtime never starts a thumbnail Camera or RenderTexture；Validator rejects missing、hash-drifted、black-border or non-warm-backdrop assets。Owner-approved scope contains three Wall Decor and two Windows。
- [x] **Step 5: Run Phase 7 validator and focused Scene tests.** Production Catalogue/MainCafe no longer references placeholders; explicit `TEST_ONLY` validation fixtures remain outside Build Settings。
- [x] **Step 6: Prepare side-by-side in-game stills** for Studio Owner visual acceptance；technical validation cannot approve appearance on the Owner's behalf。

---

### Task 11: MainCafe integration, responsive verification and Phase closeout

**Files:**
- Test: `Assets/Tests/PlayMode/EditorSceneLoading/Phase7MainCafeSceneTests.cs`
- Modify/Create: `Docs/Phase7_Beginner_Guide.md`
- Modify after acceptance: `Docs/AnimalCafe_Development_Roadmap.md`
- Update: this plan's task checkboxes and evidence notes

- [x] **Step 1: Run production-scene tests** for exact canonical IDs、four Tabs、Surface rendering、Wall-mounted registry、Window persistence、Entrance and Phase 6 Furniture preservation。
- [x] **Step 2: Run responsive UI tests** at portrait reference、narrow portrait、tall portrait、landscape and safe-area insets；verify raised Active Tab remains front、nested scrolling direction lock and Confirm / Cancel visibility in Compact Preview。
- [x] **Step 3: Run full EditMode and PlayMode suites once** according to `Docs/AnimalCafe_Phase_Development_Process.md`；read Unity XML and report passed / failed / skipped / inconclusive counts separately。
- [x] **Step 4: Run Engineering review** for transaction atomicity、stable IDs、runtime/editor boundary、Material leaks and cleanup on disable / exception。
- [x] **Step 5: Run QA review** for normal、invalid、boundary、recovery、cross-Mode input and Phase 4 / 6 regressions；Critical and Important block acceptance。
- [x] **Step 6: Prepare one baseline manual Play Mode checklist** covering four Tabs、snap states、Floor whole/single、rotation/undo、Wall layers、No Wainscoting、Wall Decor cross-wall drag、Store、fade and exit discard。The amended checklist is owned by Task 17。
- [x] **Step 7: Studio Owner manual acceptance.** Studio Owner completed MT-001–MT-034 and confirmed `34/34 PASS`、`GO` on 2026-08-29。
- [x] **Step 8: Fix accepted findings with focused regression, then rerun final full regression once.** Final Phase 6 migration `127/127`、fresh full EditMode `1443/1443`、fresh full PlayMode `625/625`；failed/skipped/inconclusive `0`。
- [x] **Step 9: Update Beginner Guide and Roadmap** with exact evidence and known limitations；Phase 7 implementation and acceptance are Complete，merge remains a separate Studio Owner-controlled gate。

---

### Task 12: Wall multi-layer transaction amendment

**Files:**
- Modify: `Assets/Scripts/Decoration/SurfaceDecorationSession.cs`
- Modify: `Assets/Scripts/Decoration/SurfacePreviewTransaction.cs`
- Modify: `Assets/Scripts/Decoration/DecorationModeController.cs`
- Test: `Assets/Tests/EditMode/Phase7/SurfaceDecorationSessionTests.cs`
- Test: `Assets/Tests/PlayMode/Phase7SurfaceScenePlayModeTests.cs`

**Interfaces:**

```csharp
public SurfaceSessionResult BeginWall(string surfaceId);
public bool HasChanges { get; }
public string UsingWallBaseStyleId { get; }
public string PreviewWallBaseStyleId { get; }
public string UsingWallWainscotingStyleId { get; }
public string PreviewWallWainscotingStyleId { get; }
// ApplyAll() returns WrongStyleKind while Scope == Wall.
```

- [x] **Step 1: Replace AT-035–AT-040 with focused failing tests.** Add exact tests named `WallPreview_AllowsBaseAndWainscotingInOneTransaction`、`WallPreview_RetargetsOnlyBeforeChanges`、`WallPreview_SelectingOriginalCombinationClearsHasChanges` and `WallPreview_ConfirmIsAtomicAcrossBothLayers`。

```csharp
Assert.That(session.BeginWall("wall.back-left").Succeeded, Is.True);
Assert.That(session.SelectStyle("wallpaper.cream-floral").Succeeded, Is.True);
Assert.That(session.SelectStyle("wainscoting.sage-plain").Succeeded, Is.True);
Assert.That(session.ActivePreview.HasChanges, Is.True);
Assert.That(confirmed.TryGetWall("wall.back-left", out var before), Is.True);
Assert.That(before.BaseStyleId, Is.Not.EqualTo("wallpaper.cream-floral"));
```

- [x] **Step 2: Run focused EditMode and observe RED.** Close every Unity Editor using this worktree, then run:

```powershell
& 'E:\Unity\Hub\Editor\6000.5.5f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'E:\Unity\Project\AnimalCafe\.worktrees\phase-7-interior-walls' -runTests -testPlatform EditMode -testFilter 'AnimalCafe.Tests.Phase7.SurfaceDecorationSessionTests' -testResults 'Temp\p7-task12-red.xml' -logFile 'Temp\p7-task12-red.log' -quit
```

Expected RED: old `BeginWall(surfaceId, layer)` contract cannot compile against the new test, or the old single-layer `StyleMatchesPreview` rejects the second Wall layer. Record XML/log; do not weaken the test.

- [x] **Step 3: Implement the minimal session state.** Capture one baseline `WallAppearance` on `BeginWall`、choose Base versus Wainscoting from selected style Kind、compute `HasChanges` from both slots、allow no-change retarget、reject changed retarget and reject Wall `ApplyAll`。

```csharp
public SurfaceSessionResult BeginWall(string surfaceId)
{
    if (activeState != null &&
        (activeState.Scope != SurfaceEditScope.Wall || HasActiveChanges()))
        return SurfaceSessionResult.Failure(
            SurfaceSessionFailure.ActivePreviewMustFinish);
    return StartWallPreviewFromConfirmedSnapshot(surfaceId);
}
```

- [x] **Step 4: Bind Controller and Catalogue states.** Selecting a Wall calls `BeginWall(surfaceId)` immediately；footer Cancel is enabled，Confirm binds to `HasChanges`；Base and Wainscoting rows receive separate Using/Preview IDs；changed target cannot switch Mode/Wall。

- [x] **Step 5: Run focused GREEN and direct regression.** Run Task 12 filter above, then `AnimalCafe.Tests.Phase7.RoomSurfaceLayoutTests` and the Wall portions of `Phase7SurfaceScenePlayModeTests`。Expected: failed/skipped/inconclusive `0`；read XML rather than relying on process exit alone。

- [x] **Step 6: Review checkpoint.** Inspect atomic rollback、no-op Confirm gate、Current/Preview lifecycle and Phase 6 Furniture isolation；record findings in this Task without commit/push。

---

### Task 13: Floor Single Grid selected / armed / previewed feedback

**Files:**
- Modify: `Assets/Scripts/Decoration/SurfacePreviewTransaction.cs`
- Modify: `Assets/Scripts/Decoration/SurfaceDecorationSession.cs`
- Modify: `Assets/Scripts/Decoration/FloorSurfaceGridView.cs`
- Modify: `Assets/Scripts/Decoration/DecorationModeController.cs`
- Test: `Assets/Tests/EditMode/Phase7/SurfaceDecorationSessionTests.cs`
- Test: `Assets/Tests/PlayMode/Phase7SurfaceScenePlayModeTests.cs`
- Test: `Assets/Tests/PlayMode/Phase7WallMountedTouchPlayModeTests.cs`

**Interfaces:**

```csharp
public GridPosition? SelectedFloorPosition { get; }
public string ArmedStyleId { get; }
public IReadOnlyList<GridPosition> PreviewedFloorPositions { get; }

public void RenderSelectionFeedback(
    GridPosition? selected,
    IReadOnlyList<GridPosition> previewed);
public void ClearSelectionFeedback();
```

- [x] **Step 1: Write RED tests for AT-043–AT-048 and IT-021 feedback.** Assert selected-only before style、armed style after selection、ordered unique Previewed positions after taps、Undo removes only the last operation state and Cancel/Confirm leave zero marker objects。

```csharp
session.BeginSingleGridFloor(new GridPosition(0, 0));
session.SelectStyle("floor.warm-wood");
session.SelectFloorGrid(new GridPosition(1, 0));
CollectionAssert.AreEqual(
    new[] { new GridPosition(0, 0), new GridPosition(1, 0) },
    session.ActivePreview.PreviewedFloorPositions);
```

- [x] **Step 2: Run focused RED.** Close the worktree Editor and run both commands。Expected RED: Previewed positions API and Scene feedback objects are absent。

```powershell
& 'E:\Unity\Hub\Editor\6000.5.5f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'E:\Unity\Project\AnimalCafe\.worktrees\phase-7-interior-walls' -runTests -testPlatform EditMode -testFilter 'AnimalCafe.Tests.Phase7.SurfaceDecorationSessionTests' -testResults 'Temp\p7-task13-red-edit.xml' -logFile 'Temp\p7-task13-red-edit.log' -quit
& 'E:\Unity\Hub\Editor\6000.5.5f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'E:\Unity\Project\AnimalCafe\.worktrees\phase-7-interior-walls' -runTests -testPlatform PlayMode -testFilter 'AnimalCafe.Tests.PlayMode.Phase7SurfaceScenePlayModeTests' -testResults 'Temp\p7-task13-red-play.xml' -logFile 'Temp\p7-task13-red-play.log' -quit
```

- [x] **Step 3: Implement derived Previewed positions.** Compare proposed Floor entries with the transaction baseline using deterministic GridPosition order；do not serialize marker state or create a second Source of Truth。

- [x] **Step 4: Implement render-only Scene feedback.** Add one selected outline child and small green check child per previewed tile using existing Phase 7 valid-feedback Material；no Collider、NavMeshObstacle or input raycast target。

- [x] **Step 5: Route feedback lifecycle.** Every selection/style/rotate/undo rerenders feedback；Confirm、Cancel、Mode exit and `OnDisable` call `ClearSelectionFeedback()` exactly once。

- [x] **Step 6: Run focused GREEN.** Run `SurfaceDecorationSessionTests`、`Phase7SurfaceScenePlayModeTests` and `Phase7WallMountedTouchPlayModeTests` filters；expected failed/skipped/inconclusive `0` and no Phase 6 camera-drag regression。

---

### Task 14: Surface footer, Compact animation and Catalogue label

**Files:**
- Modify: `Assets/Scripts/UI/Decoration/DecorationCatalogueView.cs`
- Modify: `Assets/Scripts/UI/Decoration/DecorationActionBarView.cs`
- Modify: `Assets/Scripts/Decoration/DecorationModeController.cs`
- Modify: `Assets/Editor/Phase7/Phase7DecorationSceneSetup.cs`
- Regenerate: `Assets/UI/Phase7/Prefabs/PF_UI_Phase7DecorationCatalogue.prefab`
- Regenerate: `Assets/UI/Phase7/Prefabs/PF_UI_Phase7DecorationActionBar.prefab`
- Test: `Assets/Tests/PlayMode/Phase7DecorationUiPlayModeTests.cs`
- Test: `Assets/Tests/EditMode/Phase7/Phase7AssetBuilderTests.cs`
- Test: `Assets/Tests/EditMode/Phase7/Phase7MainCafeMigrationTests.cs`

**Interfaces:**

```csharp
public RectTransform SurfaceFooterHost { get; }
public void AttachToHost(RectTransform host);
public void SetModeActions(DecorationModeKind mode, bool existing);
// Wall visible labels: Cancel, Confirm. Floor retains five labels.
```

- [x] **Step 1: Write UI/prefab RED tests.** Assert one action view、Surface Footer under Phase 7 Catalogue Sheet、Wall exact labels `Cancel/Confirm`、Floor exact five labels、`CollapsedHandle/Label.text == "Catalogue"`、active Tabs and footer share the Sheet moving root。

```csharp
actionBar.SetModeActions(DecorationModeKind.Wall, existing: false);
CollectionAssert.AreEqual(
    new[] { "Cancel", "Confirm" },
    actionBar.VisibleActionLabels);
Assert.That(catalogue.transform.Find("CollapsedHandle/Label")
    .GetComponent<TMPro.TMP_Text>().text, Is.EqualTo("Catalogue"));
```

- [x] **Step 2: Run PlayMode/EditMode RED.** Close the worktree Editor and run both commands；expected RED: Wall still exposes Apply All、action view is a Canvas sibling or collapsed label is missing。

```powershell
& 'E:\Unity\Hub\Editor\6000.5.5f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'E:\Unity\Project\AnimalCafe\.worktrees\phase-7-interior-walls' -runTests -testPlatform PlayMode -testFilter 'AnimalCafe.Tests.Phase7.Phase7DecorationUiPlayModeTests' -testResults 'Temp\p7-task14-red-play.xml' -logFile 'Temp\p7-task14-red-play.log' -quit
& 'E:\Unity\Hub\Editor\6000.5.5f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'E:\Unity\Project\AnimalCafe\.worktrees\phase-7-interior-walls' -runTests -testPlatform EditMode -testFilter 'AnimalCafe.Tests.EditMode.Phase7.Phase7AssetBuilderTests' -testResults 'Temp\p7-task14-red-edit.xml' -logFile 'Temp\p7-task14-red-edit.log' -quit
```

- [x] **Step 3: Implement one reusable action view and two hosts.** Reparent the same `DecorationActionBarView` to `SurfaceFooterHost` for Floor/Wall and the existing non-Surface host for Furniture/Wall Decor；replace listeners rather than stacking duplicates。

- [x] **Step 4: Fix hierarchy and transition.** Builder scopes legacy-title cleanup to `ExpandedSheet/Title` only；ensures `CollapsedHandle/Label`；Tabs、content、footer move on one root using exact `0.16f` duration；Compact hides rows/cards but not footer。

- [x] **Step 5: Regenerate with the idempotent Phase 7 builder.** Run builder/setup once in Unity Editor or approved CLI automation, save generated prefabs, run it a second time and assert zero additional asset/Scene diff。

- [x] **Step 6: Run focused GREEN and Phase 6 UI regression.** `Phase7DecorationUiPlayModeTests`、`Phase7AssetBuilderTests`、`Phase7MainCafeMigrationTests` and `Phase6DecorationUiPlayModeTests` must report failed/skipped/inconclusive `0`。

---

### Task 15: Wainscoting normal and shadow correction

**Files:**
- Modify: `Assets/Editor/Phase7/Phase7SurfaceAssetBuilder.cs`
- Modify: `Assets/Editor/Phase7/Phase7DecorationSceneSetup.cs`
- Modify: `Assets/Scripts/Decoration/WallSurfaceView.cs`
- Regenerate: `Assets/Art/Phase7/Textures/T_WallNormal_wainscoting_*.png`
- Regenerate: `Assets/Art/Phase7/Materials/M_Wainscoting_*.mat`
- Test: `Assets/Tests/EditMode/Phase7/Phase7AssetBuilderTests.cs`
- Test: `Assets/Tests/PlayMode/Phase7SurfaceScenePlayModeTests.cs`

**Interfaces:** no new public gameplay API；builder owns deterministic authored-normal generation and Scene setup owns renderer shadow flags。

- [x] **Step 1: Write RED objective tests.** Generated Wains normals must not include the procedural `fine * .65 + broad * .35` contribution；Wains finish/rail/baseboard renderers use `ShadowCastingMode.Off`，architectural Wall body remains `On`，and no added Collider/Nav component exists。

```csharp
Assert.That(wainsRenderer.shadowCastingMode,
    Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off));
Assert.That(wallBodyRenderer.shadowCastingMode,
    Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.On));
```

- [x] **Step 2: Run RED.** Close the worktree Editor and run both commands；expected RED from current procedural normal and Wains renderers casting independent shadows。

```powershell
& 'E:\Unity\Hub\Editor\6000.5.5f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'E:\Unity\Project\AnimalCafe\.worktrees\phase-7-interior-walls' -runTests -testPlatform EditMode -testFilter 'AnimalCafe.Tests.EditMode.Phase7.Phase7AssetBuilderTests' -testResults 'Temp\p7-task15-red-edit.xml' -logFile 'Temp\p7-task15-red-edit.log' -quit
& 'E:\Unity\Hub\Editor\6000.5.5f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'E:\Unity\Project\AnimalCafe\.worktrees\phase-7-interior-walls' -runTests -testPlatform PlayMode -testFilter 'AnimalCafe.Tests.PlayMode.Phase7SurfaceScenePlayModeTests' -testResults 'Temp\p7-task15-red-play.xml' -logFile 'Temp\p7-task15-red-play.log' -quit
```

- [x] **Step 3: Implement minimal material/build change.** `BuildTileableNormalTexture` receives an explicit procedural contribution；Wainscoting passes `0f` and uses authored luminance gradients only。Set initial Wains normal strength to conservative `0.22f` and relief/parallax contribution to `0.05f`；these values may only be visually tuned without reintroducing crosshatch。

- [x] **Step 4: Disable independent Wains shadow casting.** Keep thin visual finish/rail/baseboard renderers collider-free but `ShadowCastingMode.Off`；the shared architectural Wall body continues to cast/receive room lighting。

- [x] **Step 5: Rebuild assets twice and run GREEN.** Second builder run must be idempotent；run `Phase7AssetBuilderTests` and `Phase7SurfaceScenePlayModeTests` with zero failed/skipped/inconclusive。

---

### Task 16: Wall Decor real-prefab wall-local ghost pose

**Files:**
- Modify: `Assets/Scripts/Decoration/WallMountedPreviewView.cs`
- Modify if lookup needs correction: `Assets/Scripts/Decoration/DecorationModeController.cs`
- Test: `Assets/Tests/PlayMode/Phase7SurfaceScenePlayModeTests.cs`
- Test: `Assets/Tests/PlayMode/Phase7WallMountedTouchPlayModeTests.cs`
- Test: `Assets/Tests/EditMode/Phase7/Phase7ValidatorTests.cs`

**Interfaces:**

```csharp
public void ShowWallPreview(
    WallMountedPlacementPreview preview,
    WallSurfaceAuthoring surface,
    bool isValid,
    PlacementFeedbackKey feedback,
    GameObject previewPrefab);
```

- [x] **Step 1: Write parameterized RED tests for all five production entries.** Assert `CurrentGhost` uses each bound prefab、contains visible renderers、`Vector3.Dot(ghost.up, Vector3.up) > 0.99f`、front is parallel/opposed to wall normal、renderer height is not collapsed onto the Floor plane and ghost center stays at projection height。

```csharp
Assert.That(Vector3.Dot(view.CurrentGhost.transform.up, Vector3.up),
    Is.GreaterThan(0.99f));
Assert.That(Mathf.Abs(Vector3.Dot(
    view.CurrentGhost.transform.forward,
    wall.transform.forward)), Is.GreaterThan(0.99f));
```

- [x] **Step 2: Run PlayMode RED.** Close the worktree Editor and run both commands；expected RED must reproduce at least one current production ghost whose rendered pose is floor-like or not wall-local。

```powershell
& 'E:\Unity\Hub\Editor\6000.5.5f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'E:\Unity\Project\AnimalCafe\.worktrees\phase-7-interior-walls' -runTests -testPlatform PlayMode -testFilter 'AnimalCafe.Tests.PlayMode.Phase7SurfaceScenePlayModeTests' -testResults 'Temp\p7-task16-red-scene.xml' -logFile 'Temp\p7-task16-red-scene.log' -quit
& 'E:\Unity\Hub\Editor\6000.5.5f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'E:\Unity\Project\AnimalCafe\.worktrees\phase-7-interior-walls' -runTests -testPlatform PlayMode -testFilter 'AnimalCafe.Tests.PlayMode.Phase7WallMountedTouchPlayModeTests' -testResults 'Temp\p7-task16-red-touch.xml' -logFile 'Temp\p7-task16-red-touch.log' -quit
```

- [x] **Step 3: Compose pose from Wall local axes.** Instantiate the prefab root, then set position from the confirmed projection point and rotation from the target Wall transform plus the single approved front-facing half turn；never derive orientation from Floor hit data or renderer bounds after placement。

```csharp
var wallFacing = surface.transform.rotation * Quaternion.Euler(0f, 180f, 0f);
CurrentGhost.transform.SetPositionAndRotation(worldPosition, wallFacing);
```

- [x] **Step 4: Preserve preview safety.** Remove ghost Collider/NavMeshObstacle instances、keep real Renderer/Materials、do not alter prefab assets、footprint projection、valid/invalid state or drag ownership。

- [x] **Step 5: Run focused GREEN.** Five prefab pose matrix、valid/invalid projection、cross-wall drag and validator tests all pass with failed/skipped/inconclusive `0`。

---

### Task 17: Amended regression and Studio Owner review package

**Files:**
- Update: `Docs/superpowers/specs/2026-08-24-phase-7-interior-walls-surface-customization-test-cases.md`
- Update: `Docs/Phase7_Manual_Test_Results.md`
- Update evidence only: this plan's Task 12–17 checkboxes and counts
- Modify only after acceptance: `Docs/AnimalCafe_Development_Roadmap.md`

**Interfaces:** documentation/evidence only；no new runtime API。

- [x] **Step 1: Run direct regression after each Task.** Preserve each RED/GREEN XML/log and record passed/failed/skipped/inconclusive separately；do not overwrite first-failure evidence。

- [x] **Step 2: Run final EditMode and PlayMode suites once.** Close the worktree Editor and run both commands。Every unexpected failure blocks manual review readiness。

```powershell
unity test . --mode EditMode --output 'TestResults\Phase7Amendment\Task17\task17-final-full-editmode.xml' --editor-path 'E:\Unity\Hub\Editor\6000.5.5f1\Editor\Unity.exe' --timeout 3600
unity test . --mode PlayMode --output 'TestResults\Phase7Amendment\Task17\task17-final-post-edit-full-playmode.xml' --editor-path 'E:\Unity\Hub\Editor\6000.5.5f1\Editor\Unity.exe' --timeout 600
```

- [x] **Step 3: Recheck the eight frozen regressions.** Bottom Sheet/card/title、Tabs/gap/animation、Wall/Wains depth、Current/Preview lifecycle、Wall Decor ghost/footprint/drag、thumbnails、no initial temporary Window、runtime-session-only confirmed Window。

- [x] **Step 4: Prepare manual steps MT-001–MT-034.** Mark formal Floor/Wall/Wall-mounted visual cases READY but unaccepted；do not prefill PASS/FAIL or claim visual quality。

**Automated closeout evidence (2026-08-27):** fresh full EditMode `1438/1438`；fresh full PlayMode `599/599`；failed/skipped/inconclusive `0`；Phase 7 focused EditMode `299/299`。The 273-file audit restored non-semantic Unity serializer churn exactly and retains only the approved Phase 7 Catalogue prefab delta。Task 17 is ready for Step 5 only；all manual result cells and Owner decision remain blank，Roadmap unchanged。

- [x] **Step 5: Studio Owner Manual Acceptance.** Studio Owner ran production MainCafe、completed MT-001–MT-034 and recorded `GO`；visual quality、fade opacity、card sizing and material feel are accepted。

- [x] **Step 6: If findings exist, write focused RED first and return to the owning Task.** 2026-08-27 Wall-mounted floating finding reproduced on both walls (`0/2`) and all five production prefabs (`0/5`) before implementation；Base Wall contact GREEN `2/2` + `5/5`，direct regressions Wall Mounted `47/47`、Surface `38/38`、MainCafe `16/16`，fresh full PlayMode `600/600` with failed/skipped/inconclusive `0`。Owner review may resume；only explicit Owner acceptance permits Roadmap Phase 7 completion update。

**Final closeout evidence (2026-08-29):** Studio Owner manual review `34/34 PASS`，decision `GO`。A final compatibility audit exposed one stale Phase 6 migration guard that rejected the canonical Phase 7 scene-owned `SurfaceFooterHost/FloorRange` subtree；a focused RED was added before the minimal compatibility fix。Final Phase 6 migration `127/127`、Phase 7 MainCafe migration `30/30`、fresh full EditMode `1443/1443`、fresh full PlayMode `625/625`，failed/skipped/inconclusive 均为 `0`。Engineering、QA 与 Production closeout have no open Critical/Important finding。

---

### Task 20: Later-decision UI/Bug self-audit and mounted-thumbnail correction

**Files:**
- Modify: `Assets/Editor/Phase7/Phase7FormalAssetIntake.cs`
- Modify: `Assets/Editor/Phase7/Phase7Validator.cs`
- Modify: `Assets/Tests/EditMode/Phase7/Phase7AssetBuilderTests.cs`
- Modify: `Assets/Tests/EditMode/Phase7/Phase7ValidatorTests.cs`
- Replace: five committed Sprites under `Assets/UI/Phase7/Thumbnails`
- Update: approved spec、test matrix and manual result sheet

**Interfaces:** Catalogue continues to consume ordinary Sprite references；no runtime Camera、RenderTexture or new gameplay API。

- [x] **Step 1: Re-audit against later decisions.** Treat the newest interaction/UI amendments as authoritative；keep Studio Owner visual acceptance separate from automated confidence。
- [x] **Step 2: Write RED thumbnail evidence.** Reject opaque black borders and require the mounted item to remain visible within a shared warm wall presentation。
- [x] **Step 3: Replace all five previews.** Use the real production prefab、light `3/4` view、warm wall、baseboard and contact shadow；remove Blender-style black presentation。
- [x] **Step 4: Remove automatic GPU rebake from normal builder.** Keep the approved PNGs as authored/versioned assets；builder binds them and Validator owns explicit failure for missing/hash/backdrop drift。This avoids the reproduced Unity 6 native shutdown crash from the P7 preview-render path。
- [x] **Step 5: Add validator regression.** A temporary valid `256×256` black PNG must produce `P7-MOUNTED-THUMBNAIL-BACKDROP` and restore the original bytes in `finally`。
- [x] **Step 6: Run focused and Phase 7 regressions.** Record exact XML counts、process exit、static diff checks and visual inspection of all five PNGs。
- [x] **Step 7: Present the phone-readable contact sheet and audit findings.** Do not pre-approve Studio Owner visual review。

**Automated self-audit evidence (2026-08-28):** thumbnail presentation GREEN `1/1` and normal Unity exit；black-backdrop Validator RED `18/19` with only the new case failing, then focused GREEN `1/1`；AssetBuilder `15/15`、clean Validator `1/1`、Decoration UI `52/52`、Surface Scene `38/38`、Wall Mounted touch `50/50`。Production MainCafe audit exposed the fixed inner-margin defect (`15/16`, actual inset `9.237625 px`)；after increasing the authored horizontal inset, focused GREEN `1/1` and full MainCafe GREEN `16/16`。All listed GREEN reports have failed/skipped/inconclusive `0`。Five PNGs passed direct visual inspection and are combined in `outputs/phase7-self-audit/Phase7_Mounted_Thumbnail_ContactSheet.png`；Studio Owner visual acceptance remains pending。

## Execution Gate

Historical execution gate: Tasks 12–17 originally required Studio Owner approval and did not themselves authorize version-control actions。The Studio Owner subsequently completed manual acceptance and explicitly authorized final review/debug、commit、push and creation of a merge PR on 2026-08-29。Merge、branch/worktree deletion and cleanup remain unauthorized。
