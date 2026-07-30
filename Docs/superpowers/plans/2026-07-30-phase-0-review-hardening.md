# Phase 0 Review Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Harden the completed Phase 0 foundation before Phase 2 merge and Phase 3 planning by fixing Scene ownership, stale selection lifecycle, real mouse-input coverage, and documentation drift.

**Architecture:** `Phase0SceneSetup` remains the editor-only owner of the three Phase 0 Scene roots and canonicalizes them from loaded-scene root enumeration. `SceneInteractionController` remains the sole selection-state owner and invalidates disabled/destroyed Unity selections. `MouseCameraInput` keeps its current device-independent output; real Input System tests protect the adapter contract before any production change is considered.

**Tech Stack:** Unity `6000.5.5f1`, C#、URP `17.5.0`、Input System `1.19.0`、Unity Test Framework `1.7.0`、NUnit、EditMode / PlayMode tests

## Global Constraints

- Work directly on `main`; do not create another feature worktree for this hardening.
- Preserve the user's existing `.gitignore` and `AnimalCafe.slnx` changes exactly; do not edit, restore, stage, or include them in the handoff.
- Do not commit, push, merge, delete branches, or start Phase 3; the user owns GitHub Desktop operations.
- Follow strict TDD for behavior changes: add one failing test, verify the expected RED, add the minimal production change, verify GREEN.
- Keep Phase 0/1 existing behavior and Phase 2 placement behavior unchanged.
- `Phase0_Runtime`, `Phase0_TimeControls`, and `EventSystem` are setup-owned Scene root contracts.
- Phase 2 remains `In Review` until user manual acceptance, merge, and merged-main regression.
- Approved design: `Docs/superpowers/specs/2026-07-30-phase-0-review-hardening-design.md`.

## File Map

- `Assets/Editor/Phase0SceneSetup.cs`: canonicalizes Phase 0 owned roots and configures their components/references.
- `Assets/Tests/EditMode/Phase0SceneCleanupTests.cs`: proves inactive/duplicate root cleanup and setup idempotency while restoring `MainCafe`.
- `Assets/Scripts/Interaction/SceneInteractionController.cs`: owns and validates current selection lifecycle.
- `Assets/Scripts/Interaction/ColorSelectable.cs`: performs safe visual feedback and warns when no usable material color property exists.
- `Assets/Tests/PlayMode/Phase0PlayModeTests.cs`: selection, material-warning, Camera, Game Time, Scene-smoke, and mouse integration tests.
- `Assets/Tests/PlayMode/AnimalCafe.PlayModeTests.asmdef`: references Input System test support only if compilation proves it is required.
- `Docs/AnimalCafe_Development_Roadmap.md`: records completed-phase hardening and fresh evidence without completing Phase 2.
- `Docs/Phase0_Beginner_Guide.md`: explains the canonical roots and manual regression checks.
- `Docs/superpowers/specs/2026-07-30-phase-0-game-time-owner-hardening-design.md`: corrects the already-implemented hardening status.
- `Docs/superpowers/specs/2026-07-30-phase-0-review-hardening-design.md`: approved source design; only consistency corrections are allowed.

---

### Task 1: Canonicalize Phase 0 Scene-owned roots

**Files:**
- Modify: `Assets/Tests/EditMode/Phase0SceneCleanupTests.cs`
- Modify: `Assets/Editor/Phase0SceneSetup.cs`

**Interfaces:**
- Consumes: `Phase0SceneSetup.ConfigurePhase0Scene()`
- Produces: `private static GameObject FindOrCreateOwnedRoot(Scene scene, string name)`
- Produces: `ConfigureRuntime(Scene, Camera, CameraSettings)`, `ConfigureTimeControls(Scene)`, `EnsureEventSystem(Scene)`

- [ ] **Step 1: Expand the EditMode fixture with inactive and duplicate owned roots**

Before the first setup call in `ConfigurePhase0Scene_RemovesLegacyDemoAndRemainsIdempotent`, create:

```csharp
var inactiveRuntime = new GameObject("Phase0_Runtime");
inactiveRuntime.SetActive(false);
inactiveRuntime.AddComponent<MouseCameraInput>();
inactiveRuntime.AddComponent<MouseCameraInput>();
new GameObject("Phase0_Runtime");

var inactiveCanvas = new GameObject("Phase0_TimeControls");
inactiveCanvas.SetActive(false);
new GameObject("Phase0_TimeControls");

var inactiveEventSystem = new GameObject("EventSystem");
inactiveEventSystem.SetActive(false);
new GameObject("EventSystem");
```

After two setup calls, assert both root count and active/component normalization:

```csharp
AssertCanonicalRoot<MouseCameraInput>(
    configuredScene,
    "Phase0_Runtime");
AssertCanonicalRoot<Canvas>(
    configuredScene,
    "Phase0_TimeControls");
AssertCanonicalRoot<EventSystem>(
    configuredScene,
    "EventSystem");
```

Add this helper:

```csharp
private static void AssertCanonicalRoot<T>(
    Scene scene,
    string objectName)
    where T : Component
{
    var matches = scene.GetRootGameObjects()
        .Where(root => string.Equals(
            root.name,
            objectName,
            System.StringComparison.Ordinal))
        .ToArray();

    Assert.That(matches, Has.Length.EqualTo(1));
    Assert.That(matches[0].activeSelf, Is.True);
    Assert.That(matches[0].GetComponents<T>(), Has.Length.EqualTo(1));
}
```

Add `using System.Linq;`, `using AnimalCafe.Input;`, `using UnityEngine.EventSystems;`, and `using UnityEngine.UI;`.

- [ ] **Step 2: Run the focused test and verify RED**

Run:

```powershell
& 'E:\Unity\Hub\Editor\6000.5.5f1\Editor\Unity.exe' `
  -batchmode -nographics `
  -projectPath 'E:\Unity\Project\AnimalCafe' `
  -runTests -testPlatform EditMode `
  -testFilter 'AnimalCafe.Tests.Phase0SceneCleanupTests' `
  -testResults 'E:\Unity\Project\AnimalCafe\Logs\ReviewHardeningSceneRed.xml' `
  -logFile 'E:\Unity\Project\AnimalCafe\Logs\ReviewHardeningSceneRed.log'
```

Expected: test fails because inactive roots are not found by `GameObject.Find`, leaving duplicate named roots.

- [ ] **Step 3: Implement loaded-scene canonical root selection**

Change the three configuration calls:

```csharp
ConfigureRuntime(scene, mainCamera, settings);
ConfigureTimeControls(scene);
EnsureEventSystem(scene);
```

Replace `FindOrCreateRoot` with:

```csharp
private static GameObject FindOrCreateOwnedRoot(
    Scene scene,
    string name)
{
    GameObject canonical = null;
    foreach (var root in scene.GetRootGameObjects())
    {
        if (!string.Equals(
                root.name,
                name,
                StringComparison.Ordinal))
        {
            continue;
        }

        if (canonical == null)
        {
            canonical = root;
            continue;
        }

        UnityEngine.Object.DestroyImmediate(root);
    }

    canonical ??= new GameObject(name);
    if (canonical.scene != scene)
    {
        SceneManager.MoveGameObjectToScene(canonical, scene);
    }

    canonical.SetActive(true);
    return canonical;
}
```

Update `ConfigureRuntime`, `ConfigureTimeControls`, and `EnsureEventSystem` to call `FindOrCreateOwnedRoot(scene, ...)`. Pass the canonical runtime root into the time-control configuration instead of calling `GameObject.Find`:

```csharp
var runtimeRoot = FindOrCreateOwnedRoot(scene, RuntimeRootName);
var service = runtimeRoot.GetComponent<GameTimeService>();
```

Replace `GetOrAdd<T>` with a single-component normalizer so a malformed
canonical root cannot retain duplicate setup-owned components:

```csharp
private static T GetOrAdd<T>(GameObject gameObject)
    where T : Component
{
    var components = gameObject.GetComponents<T>();
    if (components.Length == 0)
    {
        return gameObject.AddComponent<T>();
    }

    var canonical = components[0];
    for (var index = 1; index < components.Length; index++)
    {
        UnityEngine.Object.DestroyImmediate(components[index]);
    }

    return canonical;
}
```

- [ ] **Step 4: Run focused EditMode GREEN**

Run the Step 2 command with result/log names `ReviewHardeningSceneGreen`.

Expected: focused test passes; restored `Assets/Scenes/MainCafe.unity` has no diff.

- [ ] **Step 5: Verify task boundary**

Run:

```powershell
git diff --check -- Assets/Editor/Phase0SceneSetup.cs Assets/Tests/EditMode/Phase0SceneCleanupTests.cs
git diff -- Assets/Scenes/MainCafe.unity
```

Expected: no whitespace errors and no Scene diff.

Do not commit. Record these exact files for the user’s later GitHub Desktop handoff.

---

### Task 2: Clear disabled and destroyed selections

**Files:**
- Modify: `Assets/Tests/PlayMode/Phase0PlayModeTests.cs`
- Modify: `Assets/Scripts/Interaction/SceneInteractionController.cs`

**Interfaces:**
- Consumes: `ISelectable`, `SceneInteractionController.CurrentSelection`, `GameEventBus.SelectionChanged`
- Produces: `private void ClearInvalidSelection()`

- [ ] **Step 1: Add disabled-selection RED test**

Add:

```csharp
[UnityTest]
public IEnumerator Interaction_DisabledSelectionClearsOnce()
{
    var fixture = CreateInteractionFixture();
    var events = new List<SelectionChangedEvent>();
    GameEventBus.SelectionChanged += events.Add;

    try
    {
        fixture.Interaction.TrySelectAt(
            fixture.Camera.WorldToScreenPoint(
                fixture.Selectable.transform.position));
        events.Clear();

        fixture.Selectable.enabled = false;
        yield return null;
        yield return null;

        Assert.That(fixture.Interaction.CurrentSelection, Is.Null);
        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0].Previous, Is.SameAs(fixture.Selectable));
        Assert.That(events[0].Current, Is.Null);
    }
    finally
    {
        GameEventBus.ResetForTests();
        fixture.Dispose();
    }
}
```

Create a test-only `InteractionFixture` helper in the test file that owns a Camera, controller, cube, and `ColorSelectable`; its `Dispose()` destroys only those GameObjects.

- [ ] **Step 2: Run the focused test and verify RED**

Run PlayMode with:

```text
-testFilter AnimalCafe.Tests.PlayMode.Phase0PlayModeTests.Interaction_DisabledSelectionClearsOnce
```

Expected: `CurrentSelection` remains the disabled `ColorSelectable`.

- [ ] **Step 3: Implement minimal invalid-selection cleanup**

Replace `ClearDestroyedSelection` calls with `ClearInvalidSelection`:

```csharp
private void ClearInvalidSelection()
{
    if (CurrentSelection is UnityEngine.Object unityObject
        && unityObject == null)
    {
        var previous = unityObject;
        CurrentSelection = null;
        GameEventBus.PublishSelectionChanged(previous, null);
        return;
    }

    if (CurrentSelection is not Behaviour behaviour
        || (behaviour.isActiveAndEnabled
            && behaviour.gameObject.activeInHierarchy))
    {
        return;
    }

    var previous = CurrentSelection;
    previous?.Deselect();
    CurrentSelection = null;
    GameEventBus.PublishSelectionChanged(
        previous as UnityEngine.Object,
        null);
}
```

For destroyed Unity objects, capture the C# Unity-object wrapper before clearing;
Unity's overloaded null comparison may report it as null, but the event still
receives the same destroyed object reference. Do not publish repeated events on
later frames.

- [ ] **Step 4: Add inactive, destroyed, and re-enable coverage**

Add separate tests:

```csharp
Interaction_InactiveGameObjectClearsSelectionOnce()
Interaction_DestroyedSelectionClearsWithoutException()
Interaction_ReenabledSelectableCanBeSelectedAgain()
```

Each test must use the real controller and raycast selection. The re-enable test must assert `CurrentSelection` is the selectable object after the second click.

- [ ] **Step 5: Run focused PlayMode GREEN**

Run:

```text
-testFilter AnimalCafe.Tests.PlayMode.Phase0PlayModeTests
```

Expected: all Phase 0 PlayMode tests pass with no unexpected logs.

Do not commit.

---

### Task 3: Add real Input System integration coverage

**Files:**
- Modify: `Assets/Tests/PlayMode/Phase0PlayModeTests.cs`
- Modify only if compilation requires it: `Assets/Tests/PlayMode/AnimalCafe.PlayModeTests.asmdef`
- Modify only after a genuine RED behavior failure: `Assets/Scripts/Input/MouseCameraInput.cs`

**Interfaces:**
- Consumes: `MouseCameraInput.ReadFrame()`, `CameraInputFrame`
- Produces: test-only `MouseCameraInputIntegrationTests : InputTestFixture`

- [ ] **Step 1: Add Input System test assembly reference**

Add to the PlayMode asmdef references only if the compiler cannot resolve `InputTestFixture`:

```json
"Unity.InputSystem.TestFramework"
```

Do not add any package dependency; Input System `1.19.0` is already installed.

- [ ] **Step 2: Add a real virtual-mouse fixture**

Add imports:

```csharp
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
```

Add a separate test class:

```csharp
public sealed class MouseCameraInputIntegrationTests : InputTestFixture
{
    private Mouse mouse;
    private GameObject inputObject;
    private MouseCameraInput input;

    public override void Setup()
    {
        base.Setup();
        mouse = InputSystem.AddDevice<Mouse>();
        inputObject = new GameObject("MouseCameraInputFixture");
        input = inputObject.AddComponent<MouseCameraInput>();
        input.DragThresholdPixels = 6f;
    }

    public override void TearDown()
    {
        Time.timeScale = 1f;
        Object.DestroyImmediate(inputObject);
        base.TearDown();
    }
}
```

- [ ] **Step 3: Add press/release and drag tests**

Use `Set(mouse.position, ...)`, `Press(mouse.leftButton)`, `Release(mouse.leftButton)`, and `yield return null` between Unity frames. Add:

```csharp
MouseInput_ClickReleaseProducesTap()
MouseInput_DragReleaseNeverProducesTap()
MouseInput_ReturningToPressPositionAfterDragStillDoesNotTap()
```

For each test, assert literal `TapReleased`, `PanDelta`, and `PointerPosition` values from the release frame.

- [ ] **Step 4: Add same-frame cache and Pause tests**

Add:

```csharp
MouseInput_TwoConsumersReceiveSameFrameValues()
MouseInput_PauseStillReadsPointerAndTap()
```

The cache test must call `ReadFrame()` twice without yielding and compare all four `CameraInputFrame` fields. The Pause test sets `Time.timeScale = 0f`, performs press/release across rendered frames, and asserts the release still reports a tap.

- [ ] **Step 5: Run focused tests and classify results**

Run:

```text
-testFilter AnimalCafe.Tests.PlayMode.MouseCameraInputIntegrationTests
```

Expected:

- If all tests pass, retain tests only and do not change `MouseCameraInput`.
- If a test fails on the intended contract, retain the RED XML/log, make the smallest production change, and rerun to GREEN.
- If a test errors because the test API or asmdef is wrong, correct the fixture; this is not a production RED.

- [ ] **Step 6: Verify existing Phase 0 PlayMode regression**

Run the full `Phase0PlayModeTests` and `MouseCameraInputIntegrationTests`.

Expected: all focused tests pass and `Time.timeScale` returns to `1f`.

Do not commit.

---

### Task 4: Enforce ColorSelectable material-warning contract

**Files:**
- Modify: `Assets/Tests/PlayMode/Phase0PlayModeTests.cs`
- Modify: `Assets/Scripts/Interaction/ColorSelectable.cs`

**Interfaces:**
- Consumes: `ColorSelectable.Select()`
- Produces: safe `TryInitializeRenderer()` failure for null/unsupported materials

- [ ] **Step 1: Replace the contradictory late-renderer test with the approved contract**

Replace `ColorSelectable_RecoversWhenRendererBecomesAvailableAfterAwake` with two tests:

```csharp
[Test]
public void ColorSelectable_MissingMaterialWarnsAndDoesNotSelect()
{
    var gameObject = new GameObject("MissingMaterial");
    gameObject.AddComponent<MeshRenderer>();
    var selectable = gameObject.AddComponent<ColorSelectable>();

    LogAssert.Expect(
        LogType.Warning,
        "[ColorSelectable] Renderer material must expose _BaseColor or _Color.");
    selectable.Select();

    Assert.That(selectable.IsSelected, Is.False);
    Object.DestroyImmediate(gameObject);
}
```

The second test uses an available shader/material with `_BaseColor` or `_Color` and proves normal Select/Deselect behavior still works.

- [ ] **Step 2: Run the missing-material test and verify RED**

Expected: warning is absent and `IsSelected` becomes true.

- [ ] **Step 3: Implement safe initialization failure**

In `TryInitializeRenderer()`:

```csharp
var material = targetRenderer.sharedMaterial;
if (material == null)
{
    Debug.LogWarning(
        "[ColorSelectable] Renderer material must expose _BaseColor or _Color.",
        this);
    return false;
}

if (material.HasProperty(BaseColorId))
{
    activeColorProperty = BaseColorId;
    originalColor = material.GetColor(BaseColorId);
}
else if (material.HasProperty(ColorId))
{
    activeColorProperty = ColorId;
    originalColor = material.GetColor(ColorId);
}
else
{
    Debug.LogWarning(
        "[ColorSelectable] Renderer material must expose _BaseColor or _Color.",
        this);
    return false;
}
```

Set `isInitialized = true` only after a usable property is selected. Keep the component alive so a later valid Renderer/material can recover.

- [ ] **Step 4: Run focused PlayMode GREEN**

Run all `ColorSelectable_*` and `Interaction_*` tests.

Expected: all pass; exactly the expected warning appears.

- [ ] **Step 5: Run full main regression**

Run full EditMode and PlayMode to:

```text
Logs/ReviewHardeningFullEditMode.xml
Logs/ReviewHardeningFullPlayMode.xml
```

Read each XML root and confirm total = passed, failed/skipped/inconclusive = `0`.

Do not commit.

---

### Task 5: Update documents, complete P0 manual gate, then integrate main into P2

**Files:**
- Modify: `Docs/AnimalCafe_Development_Roadmap.md`
- Modify: `Docs/Phase0_Beginner_Guide.md`
- Modify: `Docs/superpowers/specs/2026-07-30-phase-0-game-time-owner-hardening-design.md`
- Modify: `Docs/superpowers/specs/2026-07-30-phase-0-review-hardening-design.md`
- Preserve in P2: `.worktrees/phase-2/Docs/AnimalCafe_Development_Roadmap.md`
- Preserve in P2: `.worktrees/phase-2/Docs/Phase2_Beginner_Guide.md`

**Interfaces:**
- Consumes: fresh XML totals from Task 4 and user manual acceptance
- Produces: latest-main Phase 2 integration candidate still marked `In Review`

- [ ] **Step 1: Update P0 documentation with exact evidence**

Record:

- original Phase 0 acceptance was `16/16`;
- game-time owner hardening raised the prior baseline to `21/21`;
- this hardening’s actual fresh totals from Task 4;
- Scene canonicalization, invalid-selection cleanup, input integration coverage, and material warning;
- game-time hardening spec status as implemented/verified.

Do not invent totals before reading XML.

- [ ] **Step 2: Run documentation consistency scans**

Run:

```powershell
rg -n "16 / 16|21 / 21|等待用户书面确认|Phase 2.*Completed|下一步是.*Phase 2" Docs
git diff --check
```

Resolve only stale statements within this hardening scope.

- [ ] **Step 3: Present P0 manual acceptance**

Ask the user to open main in Unity and verify:

1. One `Phase0_Runtime`, one `Phase0_TimeControls`, one `EventSystem`.
2. Mouse pan and wheel zoom.
3. Selection, selection switching, and blank-click deselection.
4. Pause, `1x`, `2x`.
5. Test Runner totals match the fresh XML.
6. Console contains no unexpected error/warning.

Stop until the user explicitly accepts.

- [ ] **Step 4: Hand off exact main files for user commit**

Show:

```powershell
git status --short --branch
git diff --check
git diff --name-only
```

Explicitly exclude `.gitignore` and `AnimalCafe.slnx` from the requested GitHub Desktop commit. Do not commit or push.

Stop until the user confirms the P0 changes are committed on `main`.

- [ ] **Step 5: Protect P2 dirty documentation before integration**

In `.worktrees/phase-2`, verify the only expected dirty files are:

```text
Docs/AnimalCafe_Development_Roadmap.md
Docs/Phase2_Beginner_Guide.md
```

Do not stash, discard, overwrite, or auto-commit them. If any additional unexpected dirty file exists, stop and report it.

- [ ] **Step 6: Integrate latest main into P2**

After the user confirms P0 is committed:

```powershell
git -c safe.directory='E:/Unity/Project/AnimalCafe/.worktrees/phase-2' `
  -C 'E:\Unity\Project\AnimalCafe\.worktrees\phase-2' merge main
```

If Git refuses because the two documentation files overlap, stop before mutation and ask the user whether to commit the intended P2 docs first via GitHub Desktop. Do not use autostash, reset, checkout, or file restoration.

- [ ] **Step 7: Verify the integrated P2 candidate**

Run:

- focused Phase 0 EditMode cleanup tests;
- focused Phase 0 PlayMode tests;
- `GridPlacementTests`;
- full EditMode;
- full PlayMode;
- forbidden Unity-reference scan in `Assets/Scripts/Layout`;
- `rg -n "AddFurnitureInstance" Assets`;
- `git diff --check`.

Read all fresh XML totals. Expected non-pass counts are `0`; do not assume the final totals because P0 tests are being added.

- [ ] **Step 8: Stop at the Phase 2 manual gate**

Report:

- exact files changed on P2;
- fresh focused/full totals;
- preserved Roadmap and Beginner Guide edits;
- any generated files that must remain excluded;
- exact manual steps from `Docs/Phase2_Beginner_Guide.md`.

Do not merge P2, mark it `Completed`, delete its branch/worktree, or begin Phase 3.

---

## Final Verification Checklist

- [ ] Every production behavior change was preceded by a test observed failing for the intended reason.
- [ ] Focused Scene ownership tests pass.
- [ ] Focused selection lifecycle tests pass.
- [ ] Real mouse integration tests pass.
- [ ] ColorSelectable warning tests pass.
- [ ] Full main EditMode and PlayMode pass with zero non-pass results.
- [ ] User completes P0 manual acceptance.
- [ ] User commits P0 changes to `main`.
- [ ] P2 preserves its two intended documentation edits.
- [ ] P2 integrates latest `main`.
- [ ] Integrated P2 focused/full tests pass with zero non-pass results.
- [ ] `.gitignore` and `AnimalCafe.slnx` remain outside Codex-authored changes.
- [ ] Phase 2 remains `In Review`; Phase 3 has not started.
