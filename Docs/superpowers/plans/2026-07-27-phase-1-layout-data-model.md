# AnimalCafe Phase 1 Layout Data Model Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在不依赖 Unity Scene GameObject 的情况下建立可验证的 Cafe layout data，并清除 Phase 0 demo-only Scene 内容，同时保留全部正式 Phase 0 能力。

**Architecture:** Layout domain 使用 `AnimalCafe.Layout` namespace 下的纯 C# immutable values、definitions、instances、catalog 和 layout aggregate；只使用明确 methods 修改 `CafeLayout`。EditMode tests 验证全部 data contracts，PlayMode tests 负责 Phase 0 regression 与干净的 `MainCafe` smoke test。

**Tech Stack:** Unity `6000.5.5f1`、C#、NUnit / Unity Test Framework `1.7.0`、Unity Input System `1.19.0`、Git worktree。

## Global Constraints

- Source of Truth：`Docs/superpowers/specs/2026-07-26-phase-1-layout-data-model-design.md`。
- 新开发必须从整理且 clean 的 `main` 创建 `codex/phase1-layout-data-model`，不能继续修改旧 `codex/phase1-core-loop`。
- 用户 manual Play Mode 批准前不 merge、不 push、不删除新 branch/worktree。
- 暂时保留 remote `origin/codex/phase1-core-loop` 作为备份。
- Phase 1 domain 不继承 `MonoBehaviour` 或 `ScriptableObject`，不保存任何 `UnityEngine.Object` reference。
- Phase 1 不实现 occupancy、overlap、placement transaction、Scene spawning、Save、Customer 或 cafe loop。
- Phase 0 Camera、input、selection runtime、Event Bus、Pause / `1x` / `2x` 保持可用。
- 正式 `MainCafe` 删除 demo cubes、test mover 和 demo materials，不新增 floor。
- 每个 production behavior 按 failing test → RED → minimal implementation → GREEN。
- Unity batch tests 前必须关闭 interactive Unity Editor。
- 不把 `.slnx`、Temp、Logs、Library 或 test result artifacts 加入 commit。
- 每个 checkpoint 只 commit 本 Task files；不 push。
- 每个 checkpoint commit 前，使用 `git add <Task Files 中的确切 paths>`（新增 Unity assets 同时添加对应 `.meta`），再运行 `git diff --cached --name-only`；如果 staged list 出现 Task 外文件，停止并取消该文件的 staging。

## File Map

### Create

```text
Assets/Scripts/Layout/
├── CafeLayout.cs
├── FurnitureDefinition.cs
├── FurnitureDefinitionCatalog.cs
├── FurnitureInstance.cs
├── FurnitureRotation.cs
├── GridPosition.cs
├── GridSettings.cs
├── GridSize.cs
├── LayoutRegion.cs
├── LayoutZoneType.cs
├── PlacementSurfaceType.cs
└── StableId.cs

Assets/Tests/EditMode/
├── AnimalCafe.EditModeTests.asmdef
├── GridValueTests.cs
├── FurnitureDefinitionTests.cs
├── FurnitureInstanceTests.cs
├── FurnitureDefinitionCatalogTests.cs
├── CafeLayoutTests.cs
└── Phase0SceneCleanupTests.cs
```

Unity 生成的对应 `.meta` files 与 assets 一起保留。

### Modify

```text
Assets/Editor/Phase0SceneSetup.cs
Assets/Scenes/MainCafe.unity
Assets/Tests/PlayMode/Phase0PlayModeTests.cs
Docs/Phase1_Beginner_Guide.md
Docs/AnimalCafe_Development_Roadmap.md
```

### Delete

```text
Assets/Materials/Phase0Blue.mat
Assets/Materials/Phase0Blue.mat.meta
Assets/Materials/Phase0Green.mat
Assets/Materials/Phase0Green.mat.meta
Assets/Materials/Phase0Orange.mat
Assets/Materials/Phase0Orange.mat.meta
Assets/Scripts/Testing/TimeTestMover.cs
Assets/Scripts/Testing/TimeTestMover.cs.meta
```

如果 `Assets/Scripts/Testing` 删除脚本后为空，同时删除该 folder 和 `Assets/Scripts/Testing.meta`。

---

### Task 0: 建立安全的新 Phase 1 worktree 与验证基线

**Files:**
- Verify only: current `main`
- Verify only: old `.worktrees/phase1-core-loop`
- Create at execution time: `.worktrees/phase1-layout-data-model`

**Interfaces:**
- Consumes: clean `main` containing approved Design、Roadmap、spec 和 plan。
- Produces: isolated `codex/phase1-layout-data-model` worktree。

- [ ] **Step 1: 要求用户先在 GitHub Desktop commit 当前 main 文档**

必须包含当前批准的：

```text
Docs/AnimalCafe_Project_Design.md
Docs/AnimalCafe_Development_Roadmap.md
Docs/superpowers/specs/2026-07-26-phase-1-layout-data-model-design.md
Docs/superpowers/plans/2026-07-27-phase-1-layout-data-model.md
Docs/superpowers/specs/2026-07-25-phase-1-core-cafe-loop-design.md
Docs/superpowers/plans/2026-07-25-phase-1-core-cafe-loop.md
```

执行者随后运行：

```powershell
git status --short --branch
```

Expected: `main` 没有 tracked 或 untracked changes。

- [ ] **Step 2: 验证旧 Phase 1 remote backup**

```powershell
git branch -r --contains 7349b93
```

Expected: 输出包含：

```text
origin/codex/phase1-core-loop
```

如果不包含，停止，不删除旧 worktree/local branch。

- [ ] **Step 3: 使用 worktree skill 创建新 isolated worktree**

执行时先完整读取 `superpowers:using-git-worktrees`，然后创建：

```powershell
git worktree add ".worktrees/phase1-layout-data-model" `
  -b "codex/phase1-layout-data-model" main
```

Expected:

```text
.worktrees/phase1-layout-data-model
codex/phase1-layout-data-model
```

- [ ] **Step 4: 在新 worktree 运行 Phase 0 baseline tests**

关闭 interactive Unity，然后在新 worktree 运行：

```powershell
& 'E:\Unity\Hub\Editor\6000.5.5f1\Editor\Unity.exe' -batchmode -nographics `
  -projectPath . -runTests -testPlatform PlayMode `
  -testResults Temp/Phase1BaselinePlayMode.xml `
  -logFile Temp/Phase1BaselinePlayMode.log
```

Expected: 原 Phase 0 `16 / 16` passed，failed/skipped/inconclusive 均为 `0`。

- [ ] **Step 5: 清理旧 local worktree，但保留 remote branch**

确认 Unity 没有打开旧 worktree。解析并核对绝对目标：

```text
E:\Unity\Project\AnimalCafe\.worktrees\phase1-core-loop
```

旧 worktree 当前只允许存在已知的 generated `phase1-core-loop.slnx`。删除该文件前再次运行 `git status --short`；如果出现其他内容，停止并报告。

删除 `.slnx` 后执行：

```powershell
git worktree remove ".worktrees/phase1-core-loop"
git branch -D "codex/phase1-core-loop"
git worktree list
```

Expected:

- local old worktree 不再存在；
- local old branch 删除；
- remote `origin/codex/phase1-core-loop` 仍存在；
- 新 worktree 保持正常。

---

### Task 1: 建立 Grid values、Rotation 与 EditMode test boundary

**Files:**
- Create: `Assets/Tests/EditMode/AnimalCafe.EditModeTests.asmdef`
- Create: `Assets/Tests/EditMode/GridValueTests.cs`
- Create: `Assets/Scripts/Layout/GridPosition.cs`
- Create: `Assets/Scripts/Layout/GridSettings.cs`
- Create: `Assets/Scripts/Layout/GridSize.cs`
- Create: `Assets/Scripts/Layout/FurnitureRotation.cs`
- Create: `Assets/Scripts/Layout/LayoutZoneType.cs`
- Create: `Assets/Scripts/Layout/PlacementSurfaceType.cs`

**Interfaces:**
- Produces:
  - `readonly struct GridPosition : IEquatable<GridPosition>`
  - `sealed class GridSettings`
  - `readonly struct GridSize : IEquatable<GridSize>`
  - `enum FurnitureRotation`
  - `GridSize GridSize.Rotate(FurnitureRotation rotation)`
  - `enum LayoutZoneType`
  - `[Flags] enum PlacementSurfaceType`

- [ ] **Step 1: 创建 EditMode test assembly**

`AnimalCafe.EditModeTests.asmdef`：

```json
{
  "name": "AnimalCafe.EditModeTests",
  "rootNamespace": "AnimalCafe.Tests",
  "references": [
    "AnimalCafe.Runtime",
    "AnimalCafe.Editor"
  ],
  "optionalUnityReferences": [
    "TestAssemblies"
  ],
  "includePlatforms": [
    "Editor"
  ],
  "autoReferenced": false
}
```

- [ ] **Step 2: 写 Grid/Rotation failing tests**

`GridValueTests.cs` 至少包含这些 parameterized tests：

```csharp
[TestCase(0, 1)]
[TestCase(1, 0)]
[TestCase(-1, 1)]
[TestCase(1, -1)]
public void GridSize_InvalidDimensionThrows(int width, int height)
{
    Assert.Throws<ArgumentOutOfRangeException>(
        () => new GridSize(width, height));
}

[TestCase(0f)]
[TestCase(-1f)]
[TestCase(float.NaN)]
[TestCase(float.PositiveInfinity)]
[TestCase(float.NegativeInfinity)]
public void GridSettings_InvalidCellSizeThrows(float cellSize)
{
    Assert.Throws<ArgumentOutOfRangeException>(
        () => new GridSettings(cellSize));
}

[TestCase(FurnitureRotation.Degrees0, 2, 3)]
[TestCase(FurnitureRotation.Degrees90, 3, 2)]
[TestCase(FurnitureRotation.Degrees180, 2, 3)]
[TestCase(FurnitureRotation.Degrees270, 3, 2)]
public void GridSize_RotationReturnsExpectedSize(
    FurnitureRotation rotation,
    int expectedWidth,
    int expectedHeight)
{
    var result = new GridSize(2, 3).Rotate(rotation);
    Assert.That(result, Is.EqualTo(new GridSize(expectedWidth, expectedHeight)));
}

[Test]
public void GridSize_InvalidRotationThrows()
{
    Assert.Throws<ArgumentOutOfRangeException>(
        () => new GridSize(2, 3).Rotate((FurnitureRotation)999));
}
```

另加 equality/hash、negative GridPosition 和 four rotations round-trip tests，对应 spec `G01–G15`、`R01–R06`。

- [ ] **Step 3: 运行 RED**

```powershell
& 'E:\Unity\Hub\Editor\6000.5.5f1\Editor\Unity.exe' -batchmode -nographics `
  -projectPath . -runTests -testPlatform EditMode `
  -testFilter 'AnimalCafe.Tests.GridValueTests' `
  -testResults Temp/Phase1Task1Red.xml `
  -logFile Temp/Phase1Task1Red.log
```

Expected: compiler FAIL，因为 Layout types 尚不存在。

- [ ] **Step 4: 实现最小 value types**

`FurnitureRotation.cs`：

```csharp
namespace AnimalCafe.Layout
{
    public enum FurnitureRotation
    {
        Degrees0 = 0,
        Degrees90 = 90,
        Degrees180 = 180,
        Degrees270 = 270
    }
}
```

`GridSize.Rotate` 使用 exhaustive switch：

```csharp
public GridSize Rotate(FurnitureRotation rotation)
{
    switch (rotation)
    {
        case FurnitureRotation.Degrees0:
        case FurnitureRotation.Degrees180:
            return this;
        case FurnitureRotation.Degrees90:
        case FurnitureRotation.Degrees270:
            return new GridSize(Height, Width);
        default:
            throw new ArgumentOutOfRangeException(
                nameof(rotation),
                rotation,
                "Rotation must be 0, 90, 180, or 270 degrees.");
    }
}
```

`GridPosition` 和 `GridSize` 实现 typed equality、`Equals(object)`、`GetHashCode()`、`==`、`!=`。`GridSettings` 拒绝非 finite 或小于等于 `0` 的值。

Enums：

```csharp
public enum LayoutZoneType
{
    Interior = 0,
    Exterior = 1
}

[Flags]
public enum PlacementSurfaceType
{
    None = 0,
    Floor = 1 << 0,
    Wall = 1 << 1,
    FurnitureSurface = 1 << 2
}
```

- [ ] **Step 5: 运行 GREEN**

运行 `GridValueTests`，再运行全部 EditMode tests。

Expected: spec `G01–G15`、`R01–R06` 全部 PASS。

- [ ] **Step 6: checkpoint commit**

只 stage Task 1 files：

```powershell
git commit -m "feat: add phase 1 grid value types"
```

---

### Task 2: FurnitureDefinition 与 surface validation

**Files:**
- Create: `Assets/Scripts/Layout/FurnitureDefinition.cs`
- Create: `Assets/Tests/EditMode/FurnitureDefinitionTests.cs`

**Interfaces:**
- Consumes: `GridSize`、`PlacementSurfaceType`。
- Produces:
  - `FurnitureDefinition(string id, string displayName, GridSize footprint, PlacementSurfaceType allowedPlacementSurfaces)`
  - immutable `Id`、`DisplayName`、`Footprint`、`AllowedPlacementSurfaces`
  - `internal static void ValidateDefinitionId(string id, string paramName)`

- [ ] **Step 1: 写 Definition failing tests**

使用 parameterized invalid IDs：

```csharp
[TestCase("")]
[TestCase("   ")]
[TestCase("Furniture.Counter")]
[TestCase("furniture counter")]
[TestCase("furniture/counter")]
[TestCase("furniture\\counter")]
public void FurnitureDefinition_InvalidIdThrows(string id)
{
    Assert.Throws<ArgumentException>(
        () => new FurnitureDefinition(
            id,
            "Counter",
            new GridSize(2, 1),
            PlacementSurfaceType.Floor));
}
```

增加：

```csharp
[Test]
public void FurnitureDefinition_UnknownSurfaceFlagThrows()
{
    Assert.Throws<ArgumentOutOfRangeException>(
        () => new FurnitureDefinition(
            "furniture.counter.basic",
            "Counter",
            new GridSize(2, 1),
            (PlacementSurfaceType)128));
}

[Test]
public void FurnitureDefinition_HasNoUnityObjectFields()
{
    var fields = typeof(FurnitureDefinition).GetFields(
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    Assert.That(
        fields.Any(field => typeof(UnityEngine.Object)
            .IsAssignableFrom(field.FieldType)),
        Is.False);
}
```

覆盖 spec `D01–D13`。

- [ ] **Step 2: 运行 RED**

运行 `FurnitureDefinitionTests`。

Expected: compiler FAIL，因为 `FurnitureDefinition` 尚不存在。

- [ ] **Step 3: 实现最小 Definition**

ID validation 使用：

```csharp
private static readonly Regex DefinitionIdPattern =
    new Regex(
        "^[a-z0-9][a-z0-9._-]*$",
        RegexOptions.CultureInvariant);
```

Surface validation：

```csharp
var allKnownSurfaces =
    PlacementSurfaceType.Floor |
    PlacementSurfaceType.Wall |
    PlacementSurfaceType.FurnitureSurface;

if (allowedPlacementSurfaces == PlacementSurfaceType.None ||
    (allowedPlacementSurfaces & ~allKnownSurfaces) != 0)
{
    throw new ArgumentOutOfRangeException(
        nameof(allowedPlacementSurfaces));
}
```

Null ID/name 使用 `ArgumentNullException`；empty、whitespace 或 pattern mismatch 使用 `ArgumentException`。不要 trim 或静默修正输入。

- [ ] **Step 4: 运行 GREEN**

运行 `FurnitureDefinitionTests` 与全部 EditMode tests。

Expected: `D01–D13` PASS。

- [ ] **Step 5: checkpoint commit**

```powershell
git commit -m "feat: define phase 1 furniture data"
```

---

### Task 3: Stable ID 与 FurnitureInstance

**Files:**
- Create: `Assets/Scripts/Layout/StableId.cs`
- Create: `Assets/Scripts/Layout/FurnitureInstance.cs`
- Create: `Assets/Tests/EditMode/FurnitureInstanceTests.cs`

**Interfaces:**
- Consumes: `FurnitureDefinition.ValidateDefinitionId`、`GridPosition`、`FurnitureRotation`。
- Produces:
  - `string StableId.NewFurnitureInstanceId()`
  - `bool StableId.IsValidFurnitureInstanceId(string value)`
  - `FurnitureInstance.CreateNew(...)`
  - `FurnitureInstance.Restore(...)`

- [ ] **Step 1: 写 Stable ID failing tests**

```csharp
[Test]
public void StableId_OneThousandGeneratedIdsAreUniqueAndValid()
{
    var ids = new HashSet<string>(StringComparer.Ordinal);
    for (var index = 0; index < 1000; index++)
    {
        var id = StableId.NewFurnitureInstanceId();
        Assert.That(StableId.IsValidFurnitureInstanceId(id), Is.True);
        Assert.That(ids.Add(id), Is.True);
    }
}

[TestCase(null)]
[TestCase("")]
[TestCase("7f17d8fa-59f6-4be0-a668-9666ce4a28d2")]
[TestCase("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
public void StableId_InvalidValueIsRejected(string value)
{
    Assert.That(StableId.IsValidFurnitureInstanceId(value), Is.False);
}
```

- [ ] **Step 2: 写 Instance failing tests**

```csharp
[Test]
public void FurnitureInstance_CreateNewUsesUniqueStableId()
{
    var first = FurnitureInstance.CreateNew(
        "furniture.counter.basic",
        new GridPosition(1, 2),
        FurnitureRotation.Degrees90);
    var second = FurnitureInstance.CreateNew(
        "furniture.counter.basic",
        new GridPosition(1, 2),
        FurnitureRotation.Degrees90);

    Assert.That(first.InstanceId, Is.Not.EqualTo(second.InstanceId));
    Assert.That(first.DefinitionId, Is.EqualTo(second.DefinitionId));
}

[Test]
public void FurnitureInstance_RestoreRejectsInvalidRotation()
{
    var id = StableId.NewFurnitureInstanceId();
    Assert.Throws<ArgumentOutOfRangeException>(
        () => FurnitureInstance.Restore(
            id,
            "furniture.counter.basic",
            new GridPosition(0, 0),
            (FurnitureRotation)999));
}
```

加入 reflection test，确认 Instance 不包含 `UnityEngine.Object`、DisplayName 或 GridSize field。覆盖 `I01–I13`。

- [ ] **Step 3: 运行 RED**

运行 `FurnitureInstanceTests`。

Expected: compiler FAIL，因为 StableId/Instance 尚不存在。

- [ ] **Step 4: 实现 StableId**

```csharp
public static string NewFurnitureInstanceId()
{
    return Guid.NewGuid().ToString("N");
}

public static bool IsValidFurnitureInstanceId(string value)
{
    return value != null &&
           value.Length == 32 &&
           Guid.TryParseExact(value, "N", out _) &&
           string.Equals(
               value,
               value.ToLowerInvariant(),
               StringComparison.Ordinal);
}
```

- [ ] **Step 5: 实现 immutable FurnitureInstance**

`CreateNew` 调用 private constructor；`Restore` 保留传入 ID。Constructor 顺序：

1. validation instance ID；
2. validation definition ID；
3. validation rotation；
4. assign immutable properties。

Rotation validation 使用 explicit switch，不能依赖 `Enum.IsDefined` 的 boxing 行为。

- [ ] **Step 6: 运行 GREEN**

运行 `FurnitureInstanceTests` 与全部 EditMode tests。

Expected: `I01–I13` PASS。

- [ ] **Step 7: checkpoint commit**

```powershell
git commit -m "feat: add stable furniture instances"
```

---

### Task 4: Definition Catalog、LayoutRegion 与 CafeLayout aggregate

**Files:**
- Create: `Assets/Scripts/Layout/FurnitureDefinitionCatalog.cs`
- Create: `Assets/Scripts/Layout/LayoutRegion.cs`
- Create: `Assets/Scripts/Layout/CafeLayout.cs`
- Create: `Assets/Tests/EditMode/FurnitureDefinitionCatalogTests.cs`
- Create: `Assets/Tests/EditMode/CafeLayoutTests.cs`

**Interfaces:**
- Consumes: Tasks 1–3 types。
- Produces:
  - `FurnitureDefinitionCatalog(IEnumerable<FurnitureDefinition>)`
  - `TryGet(string, out FurnitureDefinition)`
  - `GetRequired(string)`
  - `LayoutRegion(string id, GridPosition origin, GridSize size, LayoutZoneType zoneType)`
  - `CafeLayout(GridSettings, FurnitureDefinitionCatalog)`
  - `AddRegion(LayoutRegion)`
  - `AddFurnitureInstance(FurnitureInstance)`
  - `TryGetFurnitureInstance(string, out FurnitureInstance)`

- [ ] **Step 1: 写 Catalog failing tests**

```csharp
[Test]
public void Catalog_DuplicateDefinitionIdThrowsWithId()
{
    var first = CreateDefinition("furniture.counter.basic");
    var duplicate = CreateDefinition("furniture.counter.basic");

    var exception = Assert.Throws<ArgumentException>(
        () => new FurnitureDefinitionCatalog(
            new[] { first, duplicate }));

    StringAssert.Contains("furniture.counter.basic", exception.Message);
}

[Test]
public void Catalog_DefensivelyCopiesInput()
{
    var definitions = new List<FurnitureDefinition>
    {
        CreateDefinition("furniture.counter.basic")
    };
    var catalog = new FurnitureDefinitionCatalog(definitions);

    definitions.Clear();

    Assert.That(
        catalog.TryGet("furniture.counter.basic", out _),
        Is.True);
}
```

在临时切换 `CultureInfo.CurrentCulture` 后重复 lookup，确认 ordinal behavior。覆盖 `C01–C10`。

- [ ] **Step 2: 写 Region 与 Layout failing tests**

```csharp
[Test]
public void CafeLayout_RejectsInstanceWithUnknownDefinitionWithoutMutation()
{
    var catalog = new FurnitureDefinitionCatalog(new[]
    {
        CreateDefinition("furniture.counter.basic")
    });
    var layout = new CafeLayout(new GridSettings(1f), catalog);
    var unknown = FurnitureInstance.CreateNew(
        "furniture.unknown",
        new GridPosition(0, 0),
        FurnitureRotation.Degrees0);

    var exception = Assert.Throws<ArgumentException>(
        () => layout.AddFurnitureInstance(unknown));

    StringAssert.Contains("furniture.unknown", exception.Message);
    Assert.That(layout.FurnitureInstances, Is.Empty);
}

[Test]
public void CafeLayout_AllowsSamePositionBecauseOccupancyIsPhase2()
{
    var layout = CreateLayoutWithCounterDefinition();
    layout.AddFurnitureInstance(CreateCounterAt(0, 0));
    layout.AddFurnitureInstance(CreateCounterAt(0, 0));

    Assert.That(layout.FurnitureInstances.Count, Is.EqualTo(2));
}
```

加入 duplicate Region/Instance、invalid zone、read-only collections、outside-region acceptance 和 no-scene-load tests。覆盖 `Z01–Z08`、`L01–L13`。

- [ ] **Step 3: 运行 RED**

运行 `FurnitureDefinitionCatalogTests` 和 `CafeLayoutTests`。

Expected: compiler FAIL，因为 Catalog/Region/Layout 尚不存在。

- [ ] **Step 4: 实现 immutable Catalog**

内部使用：

```csharp
Dictionary<string, FurnitureDefinition>(
    StringComparer.Ordinal)
```

Constructor 逐项拒绝 null 和 duplicate；对外 `Definitions` 返回构造时生成的 `ReadOnlyCollection<FurnitureDefinition>`。

`TryGet` 和 `GetRequired` 在 lookup 前复用 Definition ID validation；`GetRequired` 的 `KeyNotFoundException` message 包含 ID。

- [ ] **Step 5: 实现 LayoutRegion**

验证：

- null ID → `ArgumentNullException`
- whitespace ID → `ArgumentException`
- invalid `LayoutZoneType` → `ArgumentOutOfRangeException`
- GridSize 已在自己的 constructor 保证合法

- [ ] **Step 6: 实现 CafeLayout**

内部维护：

```csharp
List<LayoutRegion>
Dictionary<string, LayoutRegion>
List<FurnitureInstance>
Dictionary<string, FurnitureInstance>
```

只在所有 validation 成功后修改 list/dictionary，保证 failed Add 不产生 partial mutation。

对外 collections 使用只读 wrapper，不返回 mutable `List<T>`。

- [ ] **Step 7: 运行 GREEN 与完整 Domain suite**

运行全部 EditMode tests。

Expected:

- `G01–G15`
- `R01–R06`
- `D01–D13`
- `I01–I13`
- `Z01–Z08`
- `C01–C10`
- `L01–L13`
- `B01–B11`

全部 PASS。

- [ ] **Step 8: checkpoint commit**

```powershell
git commit -m "feat: add cafe layout data model"
```

---

### Task 5: 清除 Phase 0 demo-only Scene 内容

**Files:**
- Modify: `Assets/Editor/Phase0SceneSetup.cs`
- Modify: `Assets/Tests/PlayMode/Phase0PlayModeTests.cs`
- Create: `Assets/Tests/EditMode/Phase0SceneCleanupTests.cs`
- Modify: `Assets/Scenes/MainCafe.unity`
- Delete: Phase 0 demo materials、`TimeTestMover.cs` 及对应 metadata

**Interfaces:**
- Consumes: existing Phase 0 runtime setup。
- Produces:
  - `ConfigurePhase0Scene()` 不生成 demo objects/materials。
  - setup 发现旧 `Phase0_Demo` 时删除。
  - selection/time tests 使用 test-local fixtures。

- [ ] **Step 1: 将 MainCafe smoke test 改成 cleanup failing test**

把原 `MainCafe_LoadsWithRequiredPhase0Objects` 中 demo assertions 改为：

```csharp
Assert.That(GameObject.Find("Phase0_Demo"), Is.Null);
Assert.That(GameObject.Find("Selectable_Blue"), Is.Null);
Assert.That(GameObject.Find("Selectable_Green"), Is.Null);
Assert.That(GameObject.Find("Time_Test_Mover"), Is.Null);
```

继续断言：

```csharp
Assert.That(runtimeRoot, Is.Not.Null);
Assert.That(runtimeRoot.GetComponent<GameTimeService>(), Is.Not.Null);
Assert.That(runtimeRoot.GetComponent<MouseCameraInput>(), Is.Not.Null);
Assert.That(runtimeRoot.GetComponent<CafeCameraController>(), Is.Not.Null);
Assert.That(
    runtimeRoot.GetComponent<SceneInteractionController>(),
    Is.Not.Null);
Assert.That(canvas, Is.Not.Null);
```

- [ ] **Step 2: 将 selection Scene assertion 改为 test-local fixture**

在 test 中：

```csharp
var selectableObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
var selectable = selectableObject.AddComponent<ColorSelectable>();

selectable.Select();
Assert.That(selectable.IsSelected, Is.True);

UnityEngine.Object.DestroyImmediate(selectableObject);
```

`ColorSelectable` 会在第一次 `Select()` 时从当前 object 找到 Renderer；不要为测试新增 production-only `Configure` API。

- [ ] **Step 3: 写 setup migration failing test**

在 `Phase0SceneCleanupTests.cs` 中备份 tracked Scene bytes；test 的 `try` block 打开 Scene、保存临时 `Phase0_Demo`、运行 setup 并断言清除；`finally` block 必须恢复原 Scene bytes 并执行 `AssetDatabase.Refresh()`：

```csharp
[Test]
public void ConfigurePhase0Scene_RemovesLegacyDemoAndRemainsIdempotent()
{
    const string scenePath = "Assets/Scenes/MainCafe.unity";
    var originalBytes = File.ReadAllBytes(scenePath);
    try
    {
        var scene = EditorSceneManager.OpenScene(
            scenePath,
            OpenSceneMode.Single);
        new GameObject("Phase0_Demo");
        EditorSceneManager.SaveScene(scene);

        Phase0SceneSetup.ConfigurePhase0Scene();
        Phase0SceneSetup.ConfigurePhase0Scene();

        Assert.That(GameObject.Find("Phase0_Demo"), Is.Null);
        Assert.That(CountNamedObjects("Phase0_Runtime"), Is.EqualTo(1));
        Assert.That(CountNamedObjects("Phase0_TimeControls"), Is.EqualTo(1));
        Assert.That(CountNamedObjects("EventSystem"), Is.EqualTo(1));
    }
    finally
    {
        File.WriteAllBytes(scenePath, originalBytes);
        AssetDatabase.Refresh();
    }
}
```

`CountNamedObjects` 使用 `Resources.FindObjectsOfTypeAll<GameObject>()`，只统计属于 loaded Scene 且 name exact match 的 objects。即使 assertion 失败，也必须通过 `finally` 恢复 Scene，避免 automated test 修改用户的 tracked asset。

- [ ] **Step 4: 运行 RED**

运行 Phase 0 PlayMode focused tests。

Expected: MainCafe smoke FAIL，因为当前 Scene 仍包含 demo root/cubes。

- [ ] **Step 5: 修改 Phase0SceneSetup**

删除：

```text
using AnimalCafe.Testing;
DemoRootName creation path
ConfigureDemoObjects()
ConfigureSelectableCube()
FindOrCreatePrimitive()（如果不再有其他调用）
GetOrCreateMaterial()（如果不再有其他调用）
```

在 `ConfigurePhase0Scene()` 中，在配置 runtime 前执行：

```csharp
RemoveLegacyDemoObjects();
```

实现：

```csharp
private static void RemoveLegacyDemoObjects()
{
    var demoRoot = GameObject.Find("Phase0_Demo");
    if (demoRoot != null)
    {
        UnityEngine.Object.DestroyImmediate(demoRoot);
    }
}
```

- [ ] **Step 6: 删除 demo-only assets**

只删除 File Map 明确列出的 materials、TimeTestMover 和 metadata。删除后使用 `rg` 确认 production/tests 不再引用：

```text
Phase0Blue
Phase0Green
Phase0Orange
TimeTestMover
AnimalCafe.Testing
```

- [ ] **Step 7: 重新生成 MainCafe**

关闭 interactive Unity，执行现有 `Phase0SceneSetup.ConfigurePhase0Scene` editor entry point，使 tracked `MainCafe.unity` 清除 demo root。

确认 Scene 中不存在：

```text
Phase0_Demo
Selectable_Blue
Selectable_Green
Time_Test_Mover
old Phase 1 roots
floor objects
```

- [ ] **Step 8: 运行 GREEN**

运行完整 PlayMode suite。

Expected:

- 原 Phase 0 16-test behavior 继续覆盖；
- smoke assertions 改为正式 Scene 无 demo；
- `P01–P12`、`B12–B15` PASS；
- Console 无 unexpected error。

- [ ] **Step 9: checkpoint commit**

```powershell
git commit -m "refactor: remove phase 0 demo scene content"
```

---

### Task 6: 完整验证、文档和 manual test handoff

**Files:**
- Create: `Docs/Phase1_Beginner_Guide.md`
- Modify: `Docs/AnimalCafe_Development_Roadmap.md`
- Verify: all Phase 1 and Phase 0 files

**Interfaces:**
- Consumes: Tasks 1–5 passing implementation。
- Produces: exact automated evidence 和用户 manual checklist。

- [ ] **Step 1: 更新 Beginner Guide**

记录：

- 正式 Scene 不再包含 demo cubes/mover。
- Selection regression 使用 test-local fixture。
- MainCafe 应包含和不应包含的 objects。
- EditMode Layout test groups。
- PlayMode Phase 0 regression。
- 用户 manual checklist 与 spec 第 18 节一致。

- [ ] **Step 2: 先运行 focused EditMode tests**

```powershell
& 'E:\Unity\Hub\Editor\6000.5.5f1\Editor\Unity.exe' -batchmode -nographics `
  -projectPath . -runTests -testPlatform EditMode `
  -testFilter 'AnimalCafe.Tests' `
  -testResults Temp/Phase1FinalEditMode.xml `
  -logFile Temp/Phase1FinalEditMode.log
```

读取 XML，记录 exact total/passed/failed/skipped/inconclusive。

- [ ] **Step 3: 运行完整 PlayMode tests**

```powershell
& 'E:\Unity\Hub\Editor\6000.5.5f1\Editor\Unity.exe' -batchmode -nographics `
  -projectPath . -runTests -testPlatform PlayMode `
  -testResults Temp/Phase1FinalPlayMode.xml `
  -logFile Temp/Phase1FinalPlayMode.log
```

读取 XML，不只依赖 process exit code。要求 non-pass counts 全为 `0`。

- [ ] **Step 4: 验证 domain/Scene boundary**

运行 source scan：

```text
Assets/Scripts/Layout 不出现：
MonoBehaviour
ScriptableObject
GameObject
Transform
UnityEngine.Object
UnityEngine.SceneManagement
```

验证所有 EditMode Layout tests 在未加载 `MainCafe` 时通过。

- [ ] **Step 5: 验证 repository hygiene**

```powershell
git diff --check
git status --short --branch
```

确认：

- `.slnx`、Temp、Logs、Library、test XML 未 staged。
- 没有旧 Phase 1 runtime files。
- 没有无关 Camera/input changes。
- 没有空 folder 或 orphan metadata。

- [ ] **Step 6: 独立 review**

使用 `superpowers:requesting-code-review` 检查：

- spec coverage；
- invalid data boundary；
- exceptions；
- collection immutability；
- ID confusion；
- Phase 0 regression；
- deletion scope；
- tests 是否只验证 mock 而未验证实际 contract。

修复 review finding 时仍使用 focused failing regression test。

- [ ] **Step 7: 最终 checkpoint commit**

只 stage guide、Roadmap completion evidence 和 review fixes：

```powershell
git commit -m "docs: record phase 1 layout verification"
```

Roadmap 此时状态只能写 `In Review`，不能提前写 `Completed`。

- [ ] **Step 8: 停止并交给用户 manual test**

向用户提供：

- 修改/删除/新增 files；
- EditMode exact counts；
- PlayMode exact counts；
- branch/worktree path；
- spec 第 18 节 manual test checklist；
- 明确说明尚未 merge。

- [ ] **Step 9: 仅在用户明确批准后完成 branch**

用户 manual test 通过后才：

1. 使用 `superpowers:finishing-a-development-branch`；
2. 把 Roadmap 从 `In Review` 更新为 `Completed` 并记录 evidence；
3. 再次运行必要 tests；
4. merge `codex/phase1-layout-data-model` 到 `main`；
5. 验证 merged `main`；
6. 删除新 Phase 1 worktree；
7. 删除已 merge local branch；
8. 询问用户是否删除 remote old `codex/phase1-core-loop`，不自动删除 remote backup。
