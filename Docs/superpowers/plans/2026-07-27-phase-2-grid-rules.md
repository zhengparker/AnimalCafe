# AnimalCafe Phase 2 Grid Rules Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. Do not dispatch subagents unless the user explicitly asks for subagents.

**Goal:** 在纯 C# `CafeLayout` 中建立可靠的 Grid Occupancy，并以 atomic transaction 支持 furniture place、move、rotate 和 remove。

**Architecture:** `CafeLayout` 是 Layout instances 与 `GridPosition → Furniture Instance ID` Occupancy 的唯一 mutation owner。每个 operation 先完整计算并验证 candidate cells，只有全部合法才一次提交；expected gameplay rejection 返回 `PlacementResult`，programming error 保留 exception。

**Tech Stack:** Unity `6000.5.5f1`、C#、NUnit、Unity Test Framework `1.7.0`、Git worktree。

## Global Constraints

- Source of Truth：`Docs/superpowers/specs/2026-07-27-phase-2-grid-rules-design.md`。
- Branch：`codex/phase-2`。
- Worktree：`E:\Unity\Project\AnimalCafe\.worktrees\phase-2`。
- Phase 2 domain 必须保持纯 C#，不能引用 `GameObject`、`Transform`、`MonoBehaviour`、`ScriptableObject` 或 Scene。
- Phase 2 只执行 floor-grid occupancy；不实现 wall、furniture-surface、Mouse placement、preview、UI、Scene rendering、Save 或 pathfinding。
- `CafeLayout` 是正式 Layout 与 Occupancy 的唯一 mutation 入口；不能保留绕过 occupancy validation 的 public mutation path。
- Expected gameplay rejection 返回 `PlacementResult`；`null`、invalid ID、unknown Definition 和 invalid enum 等 programming errors 保留 exception。
- 每个 behavior 使用严格 RED → GREEN：先看到 test 因缺少目标 behavior 而失败，再写最小 implementation。
- Unity batch tests 前必须确认 interactive Unity Editor 已关闭。
- 如果 Unity Licensing Client 阻止 result XML 生成，该次运行既不算 pass 也不算 fail；停止并报告，不能继续假装 baseline clean。
- 不修改 `Assets/Scenes/MainCafe.unity`。
- 不手写 Unity `.meta` GUID；让 Unity import 新 `.cs` files 后生成对应 `.meta`。
- 不 stage `.slnx`、`Library`、`Logs`、`Temp` 或 test-result artifacts。
- 用户通过 GitHub Desktop 管理 commit 和 push；Codex 不 commit、不 push、不 merge、不删除 branch/worktree。
- 每份 Phase spec 和 Beginner Guide 开头先用中学生能理解的语言与具体例子说明 Phase 作用。

## File Map

### Create

```text
Assets/Scripts/Layout/PlacementResult.cs
Assets/Tests/EditMode/GridPlacementTests.cs
Docs/Phase2_Beginner_Guide.md
```

Unity import 后保留对应：

```text
Assets/Scripts/Layout/PlacementResult.cs.meta
Assets/Tests/EditMode/GridPlacementTests.cs.meta
```

### Modify

```text
Assets/Scripts/Layout/CafeLayout.cs
Assets/Scripts/Layout/FurnitureInstance.cs
Assets/Tests/EditMode/CafeLayoutTests.cs
Assets/Tests/EditMode/FurnitureDefinitionCatalogTests.cs
Docs/AnimalCafe_Development_Roadmap.md
```

### Verify Without Modification

```text
Assets/Scripts/Layout/FurnitureDefinition.cs
Assets/Scripts/Layout/FurnitureDefinitionCatalog.cs
Assets/Scripts/Layout/GridPosition.cs
Assets/Scripts/Layout/GridSize.cs
Assets/Scripts/Layout/LayoutRegion.cs
Assets/Scenes/MainCafe.unity
Assets/Tests/EditMode/GridValueTests.cs
Assets/Tests/EditMode/FurnitureDefinitionTests.cs
Assets/Tests/EditMode/FurnitureInstanceTests.cs
Assets/Tests/EditMode/Phase0SceneCleanupTests.cs
Assets/Tests/PlayMode/Phase0PlayModeTests.cs
```

`FurnitureDefinitionCatalog.GetRequired(string)` 和 `GridSize.Rotate(FurnitureRotation)` 已提供 Phase 2 所需接口，不为“看起来更整齐”而修改它们。

## Final Public Interfaces

```csharp
namespace AnimalCafe.Layout
{
    public enum PlacementFailureReason
    {
        None = 0,
        OutOfUnlockedRegion = 1,
        Overlap = 2,
        InstanceNotFound = 3,
        InstanceAlreadyPlaced = 4
    }

    public sealed class PlacementResult
    {
        public bool Succeeded { get; }
        public PlacementFailureReason FailureReason { get; }

        public static PlacementResult Success();
        public static PlacementResult Failure(PlacementFailureReason reason);
    }
}
```

```csharp
public sealed class CafeLayout
{
    public int OccupiedCellCount { get; }

    public PlacementResult PlaceFurniture(FurnitureInstance instance);
    public PlacementResult MoveFurniture(string instanceId, GridPosition newPosition);
    public PlacementResult RotateFurniture(
        string instanceId,
        FurnitureRotation newRotation);
    public PlacementResult RemoveFurniture(string instanceId);
    public bool TryGetOccupant(GridPosition position, out string instanceId);
}
```

`AddFurnitureInstance(FurnitureInstance)` 会被删除，避免 caller 绕过 placement validation。

`FurnitureInstance` 在 Move/Rotate task 新增 internal replacement interface：

```csharp
internal FurnitureInstance WithPlacement(
    GridPosition position,
    FurnitureRotation rotation);
```

它返回一个新 immutable instance，并保持原 `InstanceId` 和 `DefinitionId`。

---

### Task 0: 取得 Fresh Baseline

**Files:**
- Generate only: `Logs/Phase2BaselineEditMode.xml`
- Generate only: `Logs/Phase2BaselineEditMode.log`
- Generate only: `Logs/Phase2BaselinePlayMode.xml`
- Generate only: `Logs/Phase2BaselinePlayMode.log`

**Interfaces:**
- Consumes: 当前 `codex/phase-2` worktree，尚无 production implementation。
- Produces: 可核对 counts 的 fresh EditMode 和 PlayMode baseline。

- [ ] **Step 1: 确认 worktree 与 Unity 状态**

Run:

```powershell
git status --short --branch
Get-Process Unity -ErrorAction SilentlyContinue
```

Expected:

```text
branch 是 codex/phase-2
只有已批准的 spec、plan、Roadmap documentation changes
没有 interactive Unity process
```

- [ ] **Step 2: 运行 EditMode baseline**

Run:

```powershell
& 'E:\Unity\Hub\Editor\6000.5.5f1\Editor\Unity.exe' `
  -batchmode -nographics `
  -projectPath 'E:\Unity\Project\AnimalCafe\.worktrees\phase-2' `
  -runTests -testPlatform EditMode `
  -testResults 'E:\Unity\Project\AnimalCafe\.worktrees\phase-2\Logs\Phase2BaselineEditMode.xml' `
  -logFile 'E:\Unity\Project\AnimalCafe\.worktrees\phase-2\Logs\Phase2BaselineEditMode.log'
```

Expected: XML exists and reports `116` passed, failed `0`, skipped `0`, inconclusive `0`.

- [ ] **Step 3: 处理没有 XML 的情况**

Run:

```powershell
Test-Path -LiteralPath 'Logs\Phase2BaselineEditMode.xml'
Select-String -Path 'Logs\Phase2BaselineEditMode.log' `
  -Pattern 'Licensing|Timed-out|reconnect|Test run finished'
```

Expected:

- XML exists：读取 XML counts 后继续。
- XML missing 且 log 显示 Licensing timeout：停止 implementation，向用户报告并请求允许在可访问 Licensing Client 的环境重试。
- XML exists 但有 failed/skipped/inconclusive：停止 implementation，先按 `superpowers:systematic-debugging` 调查 baseline。

- [ ] **Step 4: 运行 PlayMode baseline**

Run:

```powershell
& 'E:\Unity\Hub\Editor\6000.5.5f1\Editor\Unity.exe' `
  -batchmode -nographics `
  -projectPath 'E:\Unity\Project\AnimalCafe\.worktrees\phase-2' `
  -runTests -testPlatform PlayMode `
  -testResults 'E:\Unity\Project\AnimalCafe\.worktrees\phase-2\Logs\Phase2BaselinePlayMode.xml' `
  -logFile 'E:\Unity\Project\AnimalCafe\.worktrees\phase-2\Logs\Phase2BaselinePlayMode.log'
```

Expected: XML exists and reports `18` passed, failed `0`, skipped `0`, inconclusive `0`.

- [ ] **Step 5: Baseline checkpoint**

Record exact XML counts in the execution notes. Do not stage `Logs` or XML files.

---

### Task 1: Placement Result 与 Immutable Replacement

**Files:**
- Create: `Assets/Scripts/Layout/PlacementResult.cs`
- Test: `Assets/Tests/EditMode/GridPlacementTests.cs`

**Interfaces:**
- Consumes: existing pure C# Layout assembly。
- Produces: `PlacementResult`、`PlacementFailureReason`。

- [ ] **Step 1: 创建 failing tests**

Create `Assets/Tests/EditMode/GridPlacementTests.cs` with:

```csharp
using System;
using AnimalCafe.Layout;
using NUnit.Framework;

namespace AnimalCafe.Tests
{
    public sealed class GridPlacementTests
    {
        [Test]
        public void PlacementResult_SuccessHasNoFailureReason()
        {
            var result = PlacementResult.Success();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.FailureReason, Is.EqualTo(PlacementFailureReason.None));
        }

        [TestCase(PlacementFailureReason.OutOfUnlockedRegion)]
        [TestCase(PlacementFailureReason.Overlap)]
        [TestCase(PlacementFailureReason.InstanceNotFound)]
        [TestCase(PlacementFailureReason.InstanceAlreadyPlaced)]
        public void PlacementResult_FailureStoresReason(
            PlacementFailureReason reason)
        {
            var result = PlacementResult.Failure(reason);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(reason));
        }

        [Test]
        public void PlacementResult_FailureRejectsNone()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => PlacementResult.Failure(PlacementFailureReason.None));
        }

        [Test]
        public void PlacementResult_FailureRejectsUnknownReason()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => PlacementResult.Failure((PlacementFailureReason)99));
        }
    }
}
```

- [ ] **Step 2: 运行 focused test 并确认 RED**

Run EditMode with:

```powershell
-testFilter 'AnimalCafe.Tests.GridPlacementTests'
```

Expected: compile/test failure because `PlacementResult` and `PlacementFailureReason` do not exist.

- [ ] **Step 3: 实现 `PlacementResult`**

Create:

```csharp
using System;

namespace AnimalCafe.Layout
{
    public enum PlacementFailureReason
    {
        None = 0,
        OutOfUnlockedRegion = 1,
        Overlap = 2,
        InstanceNotFound = 3,
        InstanceAlreadyPlaced = 4
    }

    public sealed class PlacementResult
    {
        public bool Succeeded { get; }
        public PlacementFailureReason FailureReason { get; }

        private PlacementResult(
            bool succeeded,
            PlacementFailureReason failureReason)
        {
            Succeeded = succeeded;
            FailureReason = failureReason;
        }

        public static PlacementResult Success()
        {
            return new PlacementResult(true, PlacementFailureReason.None);
        }

        public static PlacementResult Failure(PlacementFailureReason reason)
        {
            if (reason == PlacementFailureReason.None ||
                !Enum.IsDefined(typeof(PlacementFailureReason), reason))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(reason),
                    reason,
                    "Failure reason must be a known non-None value.");
            }

            return new PlacementResult(false, reason);
        }
    }
}
```

- [ ] **Step 4: 运行 focused tests 并确认 GREEN**

Expected: all Task 1 `GridPlacementTests` pass。

- [ ] **Step 5: Task 1 checkpoint**

Run `git diff --check` and review only Task 1 files. Do not commit.

---

### Task 2: Place、Footprint、Unlocked Regions 与 Occupancy

**Files:**
- Modify: `Assets/Scripts/Layout/CafeLayout.cs`
- Modify: `Assets/Tests/EditMode/GridPlacementTests.cs`
- Modify: `Assets/Tests/EditMode/CafeLayoutTests.cs`

**Interfaces:**
- Consumes: `PlacementResult`、`GridSize.Rotate(...)`、`FurnitureDefinitionCatalog.GetRequired(...)`。
- Produces: `PlaceFurniture(...)`、`TryGetOccupant(...)`、`OccupiedCellCount` 和内部 occupancy validation。

- [ ] **Step 1: 把 Phase 1 temporary tests 改成 Phase 2 public entry**

In `CafeLayoutTests.cs`:

- replace legal `AddFurnitureInstance(instance)` setup calls with:

```csharp
layout.AddRegion(CreateRegion("region.main", 0, 0, 20, 20));
Assert.That(layout.PlaceFurniture(instance).Succeeded, Is.True);
```

- delete `CafeLayout_AllowsSamePositionBecauseOccupancyIsPhase2`。
- delete `CafeLayout_AllowsInstanceOutsideRegionsBecausePlacementIsPhase2`。
- rename `CafeLayout_AddsInstanceWithKnownDefinition` to `CafeLayout_PlacesInstanceWithKnownDefinitionInsideUnlockedRegion`。
- update null, unknown-definition and repeated-place tests to call `PlaceFurniture`。

Expected repeated-place assertion:

```csharp
var secondResult = layout.PlaceFurniture(original);

Assert.That(secondResult.Succeeded, Is.False);
Assert.That(
    secondResult.FailureReason,
    Is.EqualTo(PlacementFailureReason.InstanceAlreadyPlaced));
Assert.That(layout.FurnitureInstances, Is.EqualTo(new[] { original }));
```

- [ ] **Step 2: 添加 place、bounds、rotation 和 overlap failing tests**

Append tests covering these exact cases:

```csharp
[TestCase(FurnitureRotation.Degrees0, 2, 3)]
[TestCase(FurnitureRotation.Degrees90, 3, 2)]
[TestCase(FurnitureRotation.Degrees180, 2, 3)]
[TestCase(FurnitureRotation.Degrees270, 3, 2)]
public void Place_NonSquareFootprintOccupiesExpectedCells(
    FurnitureRotation rotation,
    int expectedWidth,
    int expectedHeight)
```

For each case:

- create a `2 × 3` definition；
- unlock a `10 × 10` region；
- place at `(2, 3)`；
- assert `OccupiedCellCount == expectedWidth * expectedHeight`；
- assert every `(2 + x, 3 + y)` candidate is owned by the instance；
- assert the cell immediately after width is not occupied。

Also add:

```csharp
Place_OneByOneOccupiesOneCell
Place_ExactlyTouchesEveryRegionBoundary
Place_OneCellPastRightBoundaryFailsWithoutMutation
Place_OneCellPastTopBoundaryFailsWithoutMutation
Place_PartlyOutsideRegionFailsWithoutMutation
Place_InNegativeCoordinateRegionSucceeds
Place_CanSpanAdjacentUnlockedRegions
Place_CannotSpanOneCellLockedGap
Place_OverlappingRegionsDoNotDuplicateCells
Place_WithNoUnlockedRegionsFailsWithoutMutation
Place_AdjacentFurnitureSucceeds
Place_OneCellOverlapFailsWithoutMutation
Place_RepeatedInstanceReturnsAlreadyPlacedWithoutMutation
Place_UnknownDefinitionThrowsWithoutMutation
OccupancyQuery_ReturnsOwnerAndDoesNotExposeDictionary
```

Use these assertions for every rejected place:

```csharp
Assert.That(result.Succeeded, Is.False);
Assert.That(layout.FurnitureInstances, Is.Empty);
Assert.That(layout.OccupiedCellCount, Is.Zero);
Assert.That(layout.TryGetOccupant(candidateCell, out _), Is.False);
```

- [ ] **Step 3: 运行 focused tests 并确认 RED**

Expected: compile failure because the new `CafeLayout` APIs do not exist。

- [ ] **Step 4: 建立 occupancy fields 与 queries**

Add:

```csharp
private readonly Dictionary<GridPosition, string> occupantByCell;

public int OccupiedCellCount => occupantByCell.Count;
```

Initialize in constructor:

```csharp
occupantByCell = new Dictionary<GridPosition, string>();
```

Add:

```csharp
public bool TryGetOccupant(
    GridPosition position,
    out string instanceId)
{
    return occupantByCell.TryGetValue(position, out instanceId);
}
```

- [ ] **Step 5: 实现 safe footprint calculation**

Add private method:

```csharp
private bool TryGetFootprintCells(
    FurnitureDefinition definition,
    GridPosition position,
    FurnitureRotation rotation,
    out List<GridPosition> cells)
{
    var size = definition.Footprint.Rotate(rotation);
    cells = new List<GridPosition>(size.Width * size.Height);

    for (var y = 0; y < size.Height; y++)
    {
        for (var x = 0; x < size.Width; x++)
        {
            var cellX = (long)position.X + x;
            var cellY = (long)position.Y + y;

            if (cellX < int.MinValue || cellX > int.MaxValue ||
                cellY < int.MinValue || cellY > int.MaxValue)
            {
                cells.Clear();
                return false;
            }

            cells.Add(new GridPosition((int)cellX, (int)cellY));
        }
    }

    return true;
}
```

The `long` calculation prevents `int` overflow from wrapping a footprint to the opposite side of the Grid。

- [ ] **Step 6: 实现 unlocked containment 与 candidate validation**

Add:

```csharp
private bool IsCellUnlocked(GridPosition cell)
{
    foreach (var region in unlockedRegions)
    {
        var right = (long)region.Origin.X + region.Size.Width;
        var top = (long)region.Origin.Y + region.Size.Height;

        if (cell.X >= region.Origin.X &&
            (long)cell.X < right &&
            cell.Y >= region.Origin.Y &&
            (long)cell.Y < top)
        {
            return true;
        }
    }

    return false;
}
```

Add validator:

```csharp
private PlacementResult ValidateCandidateCells(
    IReadOnlyList<GridPosition> cells,
    string ignoredInstanceId = null)
{
    foreach (var cell in cells)
    {
        if (!IsCellUnlocked(cell))
        {
            return PlacementResult.Failure(
                PlacementFailureReason.OutOfUnlockedRegion);
        }

        if (occupantByCell.TryGetValue(cell, out var occupantId) &&
            !string.Equals(
                occupantId,
                ignoredInstanceId,
                StringComparison.Ordinal))
        {
            return PlacementResult.Failure(
                PlacementFailureReason.Overlap);
        }
    }

    return PlacementResult.Success();
}
```

- [ ] **Step 7: 实现 atomic Place**

Replace `AddFurnitureInstance` with:

```csharp
public PlacementResult PlaceFurniture(FurnitureInstance instance)
{
    if (instance == null)
    {
        throw new ArgumentNullException(nameof(instance));
    }

    if (!definitionCatalog.TryGet(instance.DefinitionId, out var definition))
    {
        throw new ArgumentException(
            $"Unknown Furniture Definition ID '{instance.DefinitionId}'.",
            nameof(instance));
    }

    if (furnitureInstancesById.ContainsKey(instance.InstanceId))
    {
        return PlacementResult.Failure(
            PlacementFailureReason.InstanceAlreadyPlaced);
    }

    if (!TryGetFootprintCells(
        definition,
        instance.Position,
        instance.Rotation,
        out var cells))
    {
        return PlacementResult.Failure(
            PlacementFailureReason.OutOfUnlockedRegion);
    }

    var validation = ValidateCandidateCells(cells);
    if (!validation.Succeeded)
    {
        return validation;
    }

    furnitureInstancesById.Add(instance.InstanceId, instance);
    furnitureInstances.Add(instance);

    foreach (var cell in cells)
    {
        occupantByCell.Add(cell, instance.InstanceId);
    }

    return PlacementResult.Success();
}
```

- [ ] **Step 8: 运行 focused tests 并确认 GREEN**

Run `GridPlacementTests` and `CafeLayoutTests` together。

Expected: all Phase 1 aggregate tests plus Task 2 place tests pass。

- [ ] **Step 9: Task 2 checkpoint**

Run `git diff --check` and verify `rg -n "AddFurnitureInstance" Assets` returns no production/test callers。

---

### Task 3: Move 与 Rotate Atomic Transactions

**Files:**
- Modify: `Assets/Scripts/Layout/CafeLayout.cs`
- Modify: `Assets/Scripts/Layout/FurnitureInstance.cs`
- Modify: `Assets/Tests/EditMode/GridPlacementTests.cs`

**Interfaces:**
- Consumes: occupancy validation、`FurnitureInstance.WithPlacement(...)`。
- Produces: `MoveFurniture(...)`、`RotateFurniture(...)` with rollback。

- [ ] **Step 1: 添加 move failing tests**

Add:

```text
Move_ToEmptyUnlockedPositionSucceeds
Move_ReleasesEveryOldCellAndOccupiesEveryNewCell
Move_ToCurrentPositionIsIdempotent
Move_CanReuseSomeOfItsOwnCells
Move_OutOfRegionPreservesExactOldState
Move_IntoLockedGapPreservesExactOldState
Move_OverlapPreservesExactOldState
Move_FailureDoesNotBlockNextLegalMove
Move_UnknownValidInstanceReturnsInstanceNotFound
Move_InvalidInstanceIdThrows
```

For each rollback test, capture:

```csharp
var original = layout.FurnitureInstances.Single();
var oldOwnedCells = GetOwnedCells(layout, original.InstanceId, searchBounds);
var oldOccupiedCount = layout.OccupiedCellCount;
```

Then assert:

```csharp
Assert.That(layout.FurnitureInstances.Single(), Is.SameAs(original));
Assert.That(layout.OccupiedCellCount, Is.EqualTo(oldOccupiedCount));
Assert.That(
    GetOwnedCells(layout, original.InstanceId, searchBounds),
    Is.EquivalentTo(oldOwnedCells));
```

- [ ] **Step 2: 添加 rotate failing tests**

Add:

```text
Rotate_NonSquareFurnitureUpdatesEveryOccupiedCell
Rotate_CanReuseItsOwnCells
Rotate_ToCurrentRotationIsIdempotent
Rotate_OutOfRegionPreservesOldRotationAndCells
Rotate_OverlapPreservesOldRotationAndCells
Rotate_UnknownValidInstanceReturnsInstanceNotFound
Rotate_InvalidInstanceIdThrows
Rotate_InvalidRotationThrowsWithoutMutation
```

- [ ] **Step 3: 运行 focused tests 并确认 RED**

Expected: compile failure because `MoveFurniture` and `RotateFurniture` do not exist。

- [ ] **Step 4: 实现 immutable replacement**

Add to `FurnitureInstance`:

```csharp
internal FurnitureInstance WithPlacement(
    GridPosition position,
    FurnitureRotation rotation)
{
    ValidateRotation(rotation);
    return new FurnitureInstance(
        InstanceId,
        DefinitionId,
        position,
        rotation);
}
```

This internal method is verified through the public Move/Rotate tests; the separate EditMode assembly does not call internal production methods directly。

- [ ] **Step 5: 实现 shared replacement transaction**

Add:

```csharp
private PlacementResult ReplaceFurniturePlacement(
    FurnitureInstance current,
    GridPosition position,
    FurnitureRotation rotation)
{
    var definition = definitionCatalog.GetRequired(current.DefinitionId);
    var candidate = current.WithPlacement(position, rotation);

    if (!TryGetFootprintCells(
        definition,
        candidate.Position,
        candidate.Rotation,
        out var candidateCells))
    {
        return PlacementResult.Failure(
            PlacementFailureReason.OutOfUnlockedRegion);
    }

    var validation = ValidateCandidateCells(
        candidateCells,
        current.InstanceId);

    if (!validation.Succeeded)
    {
        return validation;
    }

    ReleaseCellsOwnedBy(current.InstanceId);

    var index = furnitureInstances.FindIndex(item =>
        string.Equals(
            item.InstanceId,
            current.InstanceId,
            StringComparison.Ordinal));

    furnitureInstances[index] = candidate;
    furnitureInstancesById[current.InstanceId] = candidate;

    foreach (var cell in candidateCells)
    {
        occupantByCell.Add(cell, candidate.InstanceId);
    }

    return PlacementResult.Success();
}
```

Add:

```csharp
private void ReleaseCellsOwnedBy(string instanceId)
{
    var cellsToRelease = new List<GridPosition>();

    foreach (var pair in occupantByCell)
    {
        if (string.Equals(
            pair.Value,
            instanceId,
            StringComparison.Ordinal))
        {
            cellsToRelease.Add(pair.Key);
        }
    }

    foreach (var cell in cellsToRelease)
    {
        occupantByCell.Remove(cell);
    }
}
```

- [ ] **Step 6: 实现 Move**

```csharp
public PlacementResult MoveFurniture(
    string instanceId,
    GridPosition newPosition)
{
    ValidateInstanceId(instanceId);

    if (!furnitureInstancesById.TryGetValue(
        instanceId,
        out var current))
    {
        return PlacementResult.Failure(
            PlacementFailureReason.InstanceNotFound);
    }

    return ReplaceFurniturePlacement(
        current,
        newPosition,
        current.Rotation);
}
```

- [ ] **Step 7: 实现 Rotate**

```csharp
public PlacementResult RotateFurniture(
    string instanceId,
    FurnitureRotation newRotation)
{
    ValidateInstanceId(instanceId);

    if (!furnitureInstancesById.TryGetValue(
        instanceId,
        out var current))
    {
        return PlacementResult.Failure(
            PlacementFailureReason.InstanceNotFound);
    }

    return ReplaceFurniturePlacement(
        current,
        current.Position,
        newRotation);
}
```

`WithPlacement` validates `newRotation` before any mutation。

- [ ] **Step 8: 运行 focused tests 并确认 GREEN**

Expected: all move/rotate tests pass and every failed transaction preserves object reference, position, rotation and occupancy。

- [ ] **Step 9: Task 3 checkpoint**

Run `git diff --check`. Do not commit。

---

### Task 4: Remove、Release Safety 与 Consistency

**Files:**
- Modify: `Assets/Scripts/Layout/CafeLayout.cs`
- Modify: `Assets/Tests/EditMode/GridPlacementTests.cs`

**Interfaces:**
- Consumes: `ReleaseCellsOwnedBy(...)` 和 stable-ID validation。
- Produces: `RemoveFurniture(...)` 和完整 consistency tests。

- [ ] **Step 1: 添加 remove failing tests**

Add:

```text
Remove_DeletesInstanceAndEveryOwnedCell
Remove_DoesNotChangeOtherFurniture
Remove_RepeatedCallReturnsInstanceNotFound
Remove_RepeatedCallNeverReleasesOtherFurniture
Remove_FreesCellsForNewFurniture
Remove_UnknownValidInstanceReturnsInstanceNotFound
Remove_InvalidInstanceIdThrowsWithoutMutation
```

- [ ] **Step 2: 添加 consistency failing tests**

Add:

```text
Consistency_EveryOccupiedOwnerExistsInLayout
Consistency_EveryInstanceOwnsItsFullRotatedFootprint
Consistency_OccupiedCountEqualsSumOfPlacedFootprints
Consistency_RejectedOperationsPreserveInstanceAndOccupancySnapshots
Consistency_AllMutationPathsRemainSceneIndependent
Consistency_LayoutDomainFieldsContainNoUnityOrSceneReferences
```

The source-boundary reflection set must include:

```csharp
typeof(PlacementResult),
typeof(FurnitureInstance),
typeof(FurnitureDefinitionCatalog),
typeof(LayoutRegion),
typeof(CafeLayout)
```

- [ ] **Step 3: 运行 focused tests 并确认 RED**

Expected: compile failure because `RemoveFurniture` does not exist。

- [ ] **Step 4: 实现 Remove**

```csharp
public PlacementResult RemoveFurniture(string instanceId)
{
    ValidateInstanceId(instanceId);

    if (!furnitureInstancesById.TryGetValue(
        instanceId,
        out var instance))
    {
        return PlacementResult.Failure(
            PlacementFailureReason.InstanceNotFound);
    }

    ReleaseCellsOwnedBy(instanceId);
    furnitureInstancesById.Remove(instanceId);
    furnitureInstances.Remove(instance);

    return PlacementResult.Success();
}
```

- [ ] **Step 5: 运行 focused tests 并确认 GREEN**

Expected: all place/move/rotate/remove and consistency tests pass。

- [ ] **Step 6: 运行 full EditMode regression**

Expected: all EditMode tests pass；failed、skipped、inconclusive all `0`。

- [ ] **Step 7: Task 4 checkpoint**

Run:

```powershell
rg -n "UnityEngine|GameObject|Transform|MonoBehaviour|ScriptableObject|Scene" `
  Assets/Scripts/Layout
git diff --check
```

Expected: no forbidden production reference；only intentional words in comments/messages if any。

---

### Task 5: Independent Review Fixes 与 Regression Hardening

**Files:**
- Review: `Assets/Scripts/Layout/CafeLayout.cs`
- Review: `Assets/Scripts/Layout/FurnitureInstance.cs`
- Review: `Assets/Scripts/Layout/PlacementResult.cs`
- Review: `Assets/Tests/EditMode/GridPlacementTests.cs`
- Review: `Assets/Tests/EditMode/CafeLayoutTests.cs`

**Interfaces:**
- Consumes: final Phase 2 public API。
- Produces: documented pass/fail review result before final regression。

- [ ] **Step 1: Review against every spec rule**

Check:

```text
half-open boundary math
negative coordinates
int overflow at extreme GridPosition
adjacent/overlapping regions
one-cell locked gaps
self-overlap during move/rotate
failed-operation object identity
repeated Place/Move/Rotate/Remove
unknown definition and invalid ID exception boundaries
read-only public state
ordinal stable-ID comparison
no bypass around PlaceFurniture
no Unity/Scene references
```

- [ ] **Step 2: Stop if review finds a gap**

If any checklist item is not covered, do not improvise a fix inside this review task. Record the exact missing invariant, add a new named TDD task to this plan with its failing test and minimal implementation, then obtain the normal review checkpoint before continuing。

Expected when this Task passes: every checklist item maps to an existing named automated test and production path。

- [ ] **Step 3: Run full EditMode regression**

Expected: all EditMode tests pass；failed、skipped、inconclusive all `0`。

- [ ] **Step 4: Run full PlayMode regression**

Expected: all PlayMode tests pass；failed、skipped、inconclusive all `0`。

- [ ] **Step 5: Task 5 checkpoint**

Record exact fresh EditMode/PlayMode counts. Do not claim completion yet。

---

### Review Hardening Tasks

- **Task 5A — right-boundary regression:** add a direct near-`int.MaxValue` right-boundary test so the existing overflow-safe region arithmetic has named regression coverage。
- **Task 5B — ordinal comparer evidence:** add direct tests for `CafeLayout` region/instance lookup dictionaries and `FurnitureDefinitionCatalog`, proving stable IDs use ordinal rather than culture-sensitive comparison。

---

### Task 6: Beginner Guide、Documentation 与 User Manual Handoff

**Files:**
- Create: `Docs/Phase2_Beginner_Guide.md`
- Modify: `Docs/AnimalCafe_Development_Roadmap.md`
- Verify: `Docs/superpowers/specs/2026-07-27-phase-2-grid-rules-design.md`

**Interfaces:**
- Consumes: verified final behavior and exact XML counts。
- Produces: beginner-readable guide and manual acceptance instructions。

- [ ] **Step 1: 写 Beginner Guide 的通俗开头**

Start with:

```markdown
# AnimalCafe Phase 2 Beginner Guide

> 这是一份面向 Unity 和 coding 初学者的 educational note。
> 它只解释 Phase 2 的 Grid Rules。

## 1. 先用一个简单例子说明 Phase 2

把咖啡厅地面想成方格纸，每件家具会盖住一个或多个格子。

如果一个 `2 × 1` 柜台已经占了两个格子，新桌子就不能盖住其中任何一格。
如果玩家把柜台移动到店铺外面，程序必须拒绝这次移动，并让柜台留在原来的位置。

Phase 2 建立的就是这名“摆放管理员”。它只负责判断规则，不负责显示家具或让鼠标拖动家具。
```

- [ ] **Step 2: 完成 Beginner Guide**

Use these exact sections:

```text
1. 先用一个简单例子说明 Phase 2
2. Phase 1 和 Phase 2 的区别
3. Grid Cell、Footprint 和 Occupancy
4. Place、Move、Rotate、Remove
5. Transaction 和 Rollback
6. 正常 Tests
7. Bug / Edge Tests
8. Phase 2 Files
9. Unity Manual Test
10. Phase 2 没有做什么
11. Beginner Glossary
12. 完成状态和下一步
```

Every technical term must have a one-sentence Chinese explanation and one concrete example。

- [ ] **Step 3: 更新 Roadmap 为 In Review**

Add Phase 2 status/evidence only after fresh automated results exist:

```text
状态：In Review
Automated evidence：fresh EditMode/PlayMode exact counts
Manual evidence：等待用户
```

Do not mark `Completed`。

- [ ] **Step 4: Final automated verification**

Run fresh full EditMode and PlayMode suites after all documentation/source changes and inspect XML counts。

- [ ] **Step 5: Static verification**

Run:

```powershell
git diff --check
rg -n -i "TBD|TODO|implement later|待定|稍后实现" `
  Docs/Phase2_Beginner_Guide.md `
  Docs/superpowers/specs/2026-07-27-phase-2-grid-rules-design.md
rg -n "AddFurnitureInstance" Assets
rg -n "UnityEngine|GameObject|Transform|MonoBehaviour|ScriptableObject" `
  Assets/Scripts/Layout
git status --short --branch
```

Expected:

- no placeholders；
- no `AddFurnitureInstance` bypass；
- no forbidden Layout-domain references；
- only approved Phase 2 files changed；
- `.slnx` and generated artifacts remain unstaged/untracked as appropriate。

- [ ] **Step 6: Provide user manual test plan**

User opens:

```text
E:\Unity\Project\AnimalCafe\.worktrees\phase-2
```

Manual acceptance:

1. Test Runner → EditMode → Run All；全部绿色。
2. 展开 Grid placement tests，确认 bounds、overlap、move rollback、rotate rollback、repeated remove、consistency groups 存在。
3. 打开 `Assets/Scenes/MainCafe.unity`；确认没有新 Grid、furniture、preview 或 UI。
4. 清空 Console，进入 Play Mode。
5. 测试 mouse pan、zoom、Pause、`1x`、`2x`。
6. 退出 Play Mode；Console 没有红色 error。
7. Test Runner → PlayMode → Run All；全部绿色。
8. 阅读 `Docs/Phase2_Beginner_Guide.md`，确认解释可理解。

- [ ] **Step 7: Stop at user approval gate**

Do not merge、delete worktree、delete branch、mark Roadmap Completed、commit or push。

---

## Post-Approval Integration Plan

Only after the user explicitly approves Phase 2 manual acceptance:

1. 用户在 GitHub Desktop commit/push Phase 2。
2. 用户批准 merge 后，把 `codex/phase-2` merge 到 local `main`。
3. 在 merged `main` 上 fresh run full EditMode and PlayMode。
4. Confirm failed/skipped/inconclusive all `0`。
5. Update Roadmap Phase 2 to `Completed` with automated/manual/merge evidence。
6. 用户处理 final documentation commit/push。
7. 用户明确批准 cleanup 后，删除 Phase 2 worktree 和 branch。

Codex 不自动执行这些 destructive/integration actions。
