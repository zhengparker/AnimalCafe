# Phase 2 PR Review Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prevent UGUI clicks from changing world selection and reject Furniture Definitions whose footprint exceeds `1024 cells` before placement allocates cell lists.

**Architecture:** UI blocking stays in `SceneInteractionController.Update()` so the explicit `TrySelectAt(Vector2)` API remains unchanged. Furniture safety is enforced at the Definition boundary with `long` area arithmetic and one reusable named maximum; `GridSize` and Layout regions remain unrestricted.

**Tech Stack:** Unity `6000.5.5f1`, C#, NUnit, Unity Test Framework, UGUI `EventSystem`, pure EditMode Layout tests.

## Global Constraints

- Work only in `E:\Unity\Project\AnimalCafe\.worktrees\phase-2` on `codex/phase-2`.
- Do not directly modify `main`.
- Use strict RED → GREEN TDD independently for each behavior.
- Furniture footprint maximum is exactly `1024 cells`.
- Calculate footprint area as `(long)Width * Height`.
- UI blocking applies only to runtime input routing; `TrySelectAt(Vector2)` remains an explicit world-selection API.
- Do not add an `IUiPointerBlocker`, Input Action Map refactor, touch pointer IDs, Decoration UI, placement preview, Save, or Pathfinding.
- Do not stage, commit, push, merge, resolve GitHub comments, or delete a branch.
- Do not use Unity `-quit`.
- Do not update automated test counts until fresh XML exists.

## File Map

### Modify

```text
Assets/Scripts/Layout/FurnitureDefinition.cs
Assets/Tests/EditMode/FurnitureDefinitionCatalogTests.cs
Assets/Scripts/Interaction/SceneInteractionController.cs
Assets/Tests/PlayMode/Phase0PlayModeTests.cs
Docs/AnimalCafe_Development_Roadmap.md
Docs/Phase0_Beginner_Guide.md
Docs/Phase2_Beginner_Guide.md
```

### Verify without modification

```text
Assets/Scripts/Layout/GridSize.cs
Assets/Scripts/Layout/CafeLayout.cs
Assets/Scenes/MainCafe.unity
ProjectSettings/
Packages/
```

---

### Task 1: Reject Oversized Furniture Definitions

**Files:**

- Modify: `Assets/Tests/EditMode/FurnitureDefinitionCatalogTests.cs`
- Modify: `Assets/Scripts/Layout/FurnitureDefinition.cs`

**Interfaces:**

- Consumes: `GridSize(int width, int height)`, `FurnitureDefinition(...)`.
- Produces: `public const int MaxFootprintCellCount = 1024`; oversized Definition construction throws `ArgumentOutOfRangeException` for `footprint`.

- [ ] **Step 1: Add a test helper that accepts footprint dimensions**

Keep the existing one-argument helper and add an overload:

```csharp
private static FurnitureDefinition CreateDefinition(
    string id,
    int width,
    int height)
{
    return new FurnitureDefinition(
        id,
        "Test Furniture",
        new GridSize(width, height),
        PlacementSurfaceType.Floor);
}
```

Change the existing helper to delegate:

```csharp
private static FurnitureDefinition CreateDefinition(string id)
{
    return CreateDefinition(id, 1, 1);
}
```

- [ ] **Step 2: Write normal and boundary tests before production**

Add:

```csharp
[TestCase(32, 32)]
[TestCase(1, 1024)]
[TestCase(1024, 1)]
public void Definition_FootprintAtMaximumCellCountSucceeds(
    int width,
    int height)
{
    var definition = CreateDefinition(
        $"furniture.max.{width}x{height}",
        width,
        height);

    Assert.That(
        (long)definition.Footprint.Width * definition.Footprint.Height,
        Is.EqualTo(FurnitureDefinition.MaxFootprintCellCount));
}

[TestCase(1, 1025)]
[TestCase(1025, 1)]
[TestCase(int.MaxValue, 1)]
[TestCase(int.MaxValue, int.MaxValue)]
public void Definition_FootprintAboveMaximumCellCountThrows(
    int width,
    int height)
{
    var exception = Assert.Throws<ArgumentOutOfRangeException>(
        () => CreateDefinition(
            $"furniture.oversized.{width}x{height}",
            width,
            height));

    Assert.That(exception.ParamName, Is.EqualTo("footprint"));
    StringAssert.Contains("1024", exception.Message);
}
```

- [ ] **Step 3: Verify focused RED**

Run EditMode without `-quit`:

```powershell
& 'E:\Unity\Hub\Editor\6000.5.5f1\Editor\Unity.exe' `
  -batchmode -nographics `
  -projectPath 'E:\Unity\Project\AnimalCafe\.worktrees\phase-2' `
  -runTests -testPlatform EditMode `
  -testFilter 'AnimalCafe.Tests.FurnitureDefinitionCatalogTests' `
  -testResults 'E:\Unity\Project\AnimalCafe\.worktrees\phase-2\Logs\Phase2PrHardeningFootprintRed.xml' `
  -logFile 'E:\Unity\Project\AnimalCafe\.worktrees\phase-2\Logs\Phase2PrHardeningFootprintRed.log'
```

Expected RED:

- compile failure because `MaxFootprintCellCount` does not exist is acceptable only for the first test-first run; or
- oversized Definition construction does not throw.

Do not modify production before this evidence exists.

- [ ] **Step 4: Add the Definition boundary**

In `FurnitureDefinition`, add:

```csharp
public const int MaxFootprintCellCount = 1024;
```

After positive footprint validation and before surface validation, add:

```csharp
var footprintCellCount =
    (long)footprint.Width * footprint.Height;
if (footprintCellCount > MaxFootprintCellCount)
{
    throw new ArgumentOutOfRangeException(
        nameof(footprint),
        footprint,
        $"Furniture footprint must contain at most {MaxFootprintCellCount} cells; actual area was {footprintCellCount}.");
}
```

Do not add the maximum to `GridSize` or `CafeLayout`.

- [ ] **Step 5: Verify focused GREEN**

Run the same fixture and write:

```text
Logs/Phase2PrHardeningFootprintGreen.xml
Logs/Phase2PrHardeningFootprintGreen.log
```

Require every test passed and failed/skipped/inconclusive all `0`.

- [ ] **Step 6: Task 1 static check**

Run:

```powershell
git diff --check -- Assets/Scripts/Layout/FurnitureDefinition.cs Assets/Tests/EditMode/FurnitureDefinitionCatalogTests.cs
git diff -- Assets/Scripts/Layout/FurnitureDefinition.cs Assets/Tests/EditMode/FurnitureDefinitionCatalogTests.cs
```

Confirm the diff contains only the constant, `long` area validation, helper, and tests.

---

### Task 2: Block UGUI Taps Before World Selection

**Files:**

- Modify: `Assets/Tests/PlayMode/Phase0PlayModeTests.cs`
- Modify: `Assets/Scripts/Interaction/SceneInteractionController.cs`

**Interfaces:**

- Consumes: `CameraInputFrame`, `ICameraInputSource`, `EventSystem.current.IsPointerOverGameObject()`, `SceneInteractionController.TrySelectAt(Vector2)`.
- Produces: runtime taps over UGUI do not call world selection; explicit `TrySelectAt` behavior is unchanged.

- [ ] **Step 1: Add test namespaces**

Ensure the test file imports:

```csharp
using UnityEngine.EventSystems;
```

Production imports:

```csharp
using UnityEngine.EventSystems;
```

- [ ] **Step 2: Make the existing camera input fixture deliver one queued frame**

Replace its fixed default return with:

```csharp
public CameraInputFrame NextFrame { get; set; }

public CameraInputFrame ReadFrame()
{
    var frame = NextFrame;
    NextFrame = default;
    return frame;
}
```

This remains test-only and ensures one queued tap is consumed once.

- [ ] **Step 3: Add a real EventSystem test module**

Add to the PlayMode test file:

```csharp
public sealed class PointerOverUiInputModuleTestFixture : BaseInputModule
{
    public bool PointerOverUi { get; set; }

    public override bool IsPointerOverGameObject(int pointerId)
    {
        return PointerOverUi;
    }

    public override void Process()
    {
    }
}
```

This uses a real `EventSystem` call path while providing deterministic pointer-over-UI state.

- [ ] **Step 4: Expose the test input source through InteractionFixture**

Add `CameraInputTestFixture inputSource` to the fixture constructor and:

```csharp
public CameraInputTestFixture InputSource { get; }
```

Pass the existing `inputSource` created by `CreateInteractionFixture()`.

- [ ] **Step 5: Write the failing test that preserves selection**

Add:

```csharp
[UnityTest]
public IEnumerator Interaction_UiTapPreservesCurrentWorldSelection()
{
    var fixture = CreateInteractionFixture();
    var eventSystemObject = new GameObject("InteractionTestEventSystem");
    var eventSystem = eventSystemObject.AddComponent<EventSystem>();
    var inputModule =
        eventSystemObject.AddComponent<PointerOverUiInputModuleTestFixture>();
    var events = new List<SelectionChangedEvent>();
    GameEventBus.SelectionChanged += events.Add;

    try
    {
        yield return null;
        Assert.That(EventSystem.current, Is.SameAs(eventSystem));

        fixture.Interaction.TrySelectAt(
            fixture.Camera.WorldToScreenPoint(
                fixture.Selectable.transform.position));
        events.Clear();
        inputModule.PointerOverUi = true;
        fixture.InputSource.NextFrame = new CameraInputFrame(
            Vector2.zero,
            0f,
            true,
            new Vector2(-1000f, -1000f));

        yield return null;

        Assert.That(
            fixture.Interaction.CurrentSelection,
            Is.SameAs(fixture.Selectable));
        Assert.That(fixture.Selectable.IsSelected, Is.True);
        Assert.That(events, Is.Empty);
    }
    finally
    {
        GameEventBus.ResetForTests();
        Object.DestroyImmediate(eventSystemObject);
        fixture.Dispose();
    }
}
```

If an existing `EventSystem.current` is present, fail with a clear fixture assertion rather than deleting or modifying an unrelated Scene object.

- [ ] **Step 6: Write the failing test that blocks selection behind UI**

Add:

```csharp
[UnityTest]
public IEnumerator Interaction_UiTapDoesNotSelectWorldObjectBehindUi()
{
    var fixture = CreateInteractionFixture();
    var eventSystemObject = new GameObject("InteractionTestEventSystem");
    var eventSystem = eventSystemObject.AddComponent<EventSystem>();
    var inputModule =
        eventSystemObject.AddComponent<PointerOverUiInputModuleTestFixture>();

    try
    {
        yield return null;
        Assert.That(EventSystem.current, Is.SameAs(eventSystem));

        inputModule.PointerOverUi = true;
        fixture.InputSource.NextFrame = new CameraInputFrame(
            Vector2.zero,
            0f,
            true,
            fixture.Camera.WorldToScreenPoint(
                fixture.Selectable.transform.position));

        yield return null;

        Assert.That(fixture.Interaction.CurrentSelection, Is.Null);
        Assert.That(fixture.Selectable.IsSelected, Is.False);
    }
    finally
    {
        Object.DestroyImmediate(eventSystemObject);
        fixture.Dispose();
    }
}
```

- [ ] **Step 7: Add no-EventSystem regression coverage**

Add a PlayMode test that asserts `EventSystem.current` is null for its isolated fixture, queues a tap at the selectable, yields one frame, and confirms the object is selected. This proves missing UI infrastructure does not block world input.

- [ ] **Step 8: Verify focused RED**

Run PlayMode without `-quit`:

```powershell
& 'E:\Unity\Hub\Editor\6000.5.5f1\Editor\Unity.exe' `
  -batchmode -nographics `
  -projectPath 'E:\Unity\Project\AnimalCafe\.worktrees\phase-2' `
  -runTests -testPlatform PlayMode `
  -testFilter 'AnimalCafe.Tests.PlayMode.Phase0PlayModeTests' `
  -testResults 'E:\Unity\Project\AnimalCafe\.worktrees\phase-2\Logs\Phase2PrHardeningUiRed.xml' `
  -logFile 'E:\Unity\Project\AnimalCafe\.worktrees\phase-2\Logs\Phase2PrHardeningUiRed.log'
```

Expected RED:

- UI-preserve test clears the current selection; and/or
- UI-behind test selects the world object.

RED must be a behavior assertion failure, not an EventSystem fixture error.

- [ ] **Step 9: Implement the minimal runtime boundary**

Change only runtime tap routing:

```csharp
var inputFrame = inputSource.ReadFrame();
if (inputFrame.TapReleased
    && (EventSystem.current == null
        || !EventSystem.current.IsPointerOverGameObject()))
{
    TrySelectAt(inputFrame.PointerPosition);
}
```

Do not add this check inside `TrySelectAt()`.

- [ ] **Step 10: Verify focused GREEN**

Run the same fixture and write:

```text
Logs/Phase2PrHardeningUiGreen.xml
Logs/Phase2PrHardeningUiGreen.log
```

Require every test passed and all non-pass counts `0`.

- [ ] **Step 11: Task 2 static check**

Run:

```powershell
git diff --check -- Assets/Scripts/Interaction/SceneInteractionController.cs Assets/Tests/PlayMode/Phase0PlayModeTests.cs
git diff -- Assets/Scripts/Interaction/SceneInteractionController.cs Assets/Tests/PlayMode/Phase0PlayModeTests.cs
```

Confirm explicit `TrySelectAt()` remains unchanged.

---

### Task 3: Full Regression, Documentation, and Handoff

**Files:**

- Modify: `Docs/AnimalCafe_Development_Roadmap.md`
- Modify: `Docs/Phase0_Beginner_Guide.md`
- Modify: `Docs/Phase2_Beginner_Guide.md`

**Interfaces:**

- Consumes: fresh focused/full XML from Tasks 1–2.
- Produces: current evidence and beginner manual checks without marking P2 `Completed`.

- [ ] **Step 1: Run full EditMode regression**

Write:

```text
Logs/Phase2PrHardeningFullEditMode.xml
Logs/Phase2PrHardeningFullEditMode.log
```

Require failed/skipped/inconclusive all `0`.

- [ ] **Step 2: Run full PlayMode regression**

Write:

```text
Logs/Phase2PrHardeningFullPlayMode.xml
Logs/Phase2PrHardeningFullPlayMode.log
```

Require failed/skipped/inconclusive all `0`.

- [ ] **Step 3: Update Roadmap with actual evidence**

In Phase 0 hardening evidence, add that UI pointer releases over UGUI no longer reach world selection.

In Phase 2 `In Review` evidence, record:

- the actual fresh EditMode total from XML;
- the actual fresh PlayMode total from XML;
- oversized footprint Definition rejection;
- P0/P1 regression;
- all non-pass counts `0`.

Do not predict totals and do not mark P2 `Completed`.

- [ ] **Step 4: Update beginner guides**

`Phase0_Beginner_Guide.md`:

- explain simply that clicking a UI button does not click the Scene behind it;
- add a manual check: select a world object, click Pause/1x/2x, confirm selection does not change;
- update exact automated counts from fresh XML.

`Phase2_Beginner_Guide.md`:

- explain that a Furniture Definition may cover at most `1024 cells`;
- explain this protects against incorrect data and memory exhaustion;
- update exact automated counts from fresh XML;
- keep status `In Review`.

- [ ] **Step 5: Run final scans**

Run:

```powershell
rg -n -i "TBD|TODO|implement later|待定|稍后实现" Docs/Phase0_Beginner_Guide.md Docs/Phase2_Beginner_Guide.md Docs/superpowers/specs/2026-07-30-phase-2-pr-review-hardening-design.md
git diff --check
git status --short --branch
```

Also confirm:

- `Assets/Scenes/MainCafe.unity` has no diff;
- `ProjectSettings/` and `Packages/` have no diff;
- no generated `.slnx` is added;
- only approved production, tests, specs/plans, and guide/Roadmap files changed.

- [ ] **Step 6: Stop before GitHub writes**

Do not stage, commit, push, reply, resolve, merge, or delete the branch. Give the user:

- exact focused/full counts;
- manual UI check;
- GitHub Desktop file list;
- the unresolved PR thread URL and a draft response, without posting it.
