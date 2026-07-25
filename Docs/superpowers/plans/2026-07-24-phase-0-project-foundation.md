# AnimalCafe Phase 0 — Project Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在 `MainCafe` scene 建立可扩展、可测试的 Camera、mouse input、selection feedback 和 Pause / `1x` / `2x` Game Time foundation。

**Architecture:** Runtime code 放在独立 `AnimalCafe.Runtime` assembly 中，以 interface 隔离 input 和 game time。Camera 与 interaction 消费设备无关 input，跨系统状态通过小型 event bus 通知；scene 由可重复运行的 Editor setup tool 组装，Play Mode tests 通过 public contracts 验证行为。

**Tech Stack:** Unity 6.0 `6000.5.5f1`、C#、Universal Render Pipeline `17.5.0`、Input System `1.19.0`、Unity Test Framework `1.7.0`、NUnit。

## Global Constraints

- 当前开发与验证平台为 Windows；未来目标兼容 iOS。
- Mouse wheel 对应未来 pinch，left-button drag 对应未来 single-finger drag，tap 与 drag 必须通过 screen-space threshold 区分。
- Camera 固定斜俯视，只允许 pan 和 orthographic zoom。
- Pause 时 Camera、selection 和 UI 仍可操作。
- Game speed 只允许 `0x`、`1x` 和 `2x`。
- Selection feedback 只使用 placeholder object 变色，不制作正式 UI art。
- 不实现 Customer AI、Orders、Economy、Inventory、Save system 或真正的 touch input。
- 不引入新的 Unity package、dependency injection framework、service locator 或复杂 state machine。
- 保留当前工作区所有未提交的 scene、Docs、models 和 ProjectSettings changes。
- 未收到明确指令前不执行 Git commit。

## Planned File Map

### Create

- `Assets/Scripts/AnimalCafe.Runtime.asmdef`：将 Phase 0 runtime code 组成可被 tests 引用的独立 assembly。
- `Assets/Scripts/Core/Events/GameEvents.cs`：定义 selection 与 game-speed event data。
- `Assets/Scripts/Core/Events/GameEventBus.cs`：发布和订阅 Phase 0 跨系统 events。
- `Assets/Scripts/Core/Time/GameSpeed.cs`：定义唯一允许的三种 game speeds。
- `Assets/Scripts/Core/Time/IGameTimeService.cs`：game-time contract。
- `Assets/Scripts/Core/Time/GameTimeService.cs`：验证和应用 `Time.timeScale`。
- `Assets/Scripts/Input/CameraInputFrame.cs`：设备无关的单帧 Camera / tap input data。
- `Assets/Scripts/Input/ICameraInputSource.cs`：input adapter contract。
- `Assets/Scripts/Input/MouseCameraInput.cs`：mouse adapter 与 tap / drag 判断。
- `Assets/Scripts/Camera/CameraSettings.cs`：Camera `ScriptableObject` config。
- `Assets/Scripts/Camera/CafeCameraController.cs`：pan、zoom 和 bounds。
- `Assets/Scripts/Interaction/ISelectable.cs`：selectable contract。
- `Assets/Scripts/Interaction/ColorSelectable.cs`：selected color feedback。
- `Assets/Scripts/Interaction/SceneInteractionController.cs`：raycast 与单选状态。
- `Assets/Scripts/UI/TimeControlPanel.cs`：Pause / `1x` / `2x` buttons。
- `Assets/Scripts/Testing/TimeTestMover.cs`：按 scaled delta time 来回移动。
- `Assets/Editor/Phase0SceneSetup.cs`：以 Unity Editor API 可重复配置 `MainCafe`，避免手写 scene YAML。
- `Assets/Config/DefaultCameraSettings.asset`：默认 Camera 参数。
- `Assets/Tests/PlayMode/AnimalCafe.PlayModeTests.asmdef`：Play Mode test assembly。
- `Assets/Tests/PlayMode/Phase0PlayModeTests.cs`：Phase 0 自动 tests。

### Modify

- `Assets/Scenes/MainCafe.unity`：保留现有内容，增加 runtime root、测试对象、Canvas 和 component references。
- `ProjectSettings/EditorBuildSettings.asset`：将启动 scene 从 `SampleScene` 改为 `MainCafe`。
- `Docs/AnimalCafe_Development_Roadmap.md`：Phase 0 通过全部 gate 后，将 Phase 0 标记为完成并记录验证结果。

---

### Task 1: Runtime Assembly、Events 与 Game Time

**Files:**

- Create: `Assets/Scripts/AnimalCafe.Runtime.asmdef`
- Create: `Assets/Scripts/Core/Events/GameEvents.cs`
- Create: `Assets/Scripts/Core/Events/GameEventBus.cs`
- Create: `Assets/Scripts/Core/Time/GameSpeed.cs`
- Create: `Assets/Scripts/Core/Time/IGameTimeService.cs`
- Create: `Assets/Scripts/Core/Time/GameTimeService.cs`
- Create: `Assets/Tests/PlayMode/AnimalCafe.PlayModeTests.asmdef`
- Create: `Assets/Tests/PlayMode/Phase0PlayModeTests.cs`

**Interfaces:**

- Produces: `GameSpeed` enum with `Paused = 0`, `Normal = 1`, `Fast = 2`.
- Produces: `IGameTimeService.CurrentSpeed`, `IGameTimeService.TrySetSpeed(GameSpeed speed)`.
- Produces: `GameTimeService.SetPaused()`, `SetNormal()`, `SetFast()` for UI buttons.
- Produces: `GameEventBus.SelectionChanged` and `GameEventBus.GameSpeedChanged`.

- [ ] **Step 1: Create runtime and test assembly definitions**

`Assets/Scripts/AnimalCafe.Runtime.asmdef`:

```json
{
  "name": "AnimalCafe.Runtime",
  "rootNamespace": "AnimalCafe",
  "references": ["Unity.InputSystem"],
  "autoReferenced": true
}
```

`Assets/Tests/PlayMode/AnimalCafe.PlayModeTests.asmdef`:

```json
{
  "name": "AnimalCafe.PlayModeTests",
  "rootNamespace": "AnimalCafe.Tests",
  "references": ["AnimalCafe.Runtime", "UnityEngine.TestRunner", "UnityEditor.TestRunner"],
  "optionalUnityReferences": ["TestAssemblies"],
  "includePlatforms": [],
  "excludePlatforms": [],
  "autoReferenced": false
}
```

- [ ] **Step 2: Write failing Game Time tests**

Add to `Phase0PlayModeTests.cs`:

```csharp
using AnimalCafe.Core.Time;
using NUnit.Framework;
using UnityEngine;

namespace AnimalCafe.Tests.PlayMode
{
    public sealed class Phase0PlayModeTests
    {
        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
        }

        [Test]
        public void GameTime_AcceptsOnlySupportedSpeeds()
        {
            var gameObject = new GameObject("GameTimeService");
            var service = gameObject.AddComponent<GameTimeService>();

            Assert.That(service.TrySetSpeed(GameSpeed.Fast), Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(2f));
            Assert.That(service.TrySetSpeed((GameSpeed)3), Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(2f));

            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void GameTime_PauseSetsTimeScaleToZero()
        {
            var gameObject = new GameObject("GameTimeService");
            var service = gameObject.AddComponent<GameTimeService>();

            service.SetPaused();

            Assert.That(service.CurrentSpeed, Is.EqualTo(GameSpeed.Paused));
            Assert.That(Time.timeScale, Is.Zero);
            Object.DestroyImmediate(gameObject);
        }
    }
}
```

- [ ] **Step 3: Run tests and confirm RED**

Run:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.5.5f1\Editor\Unity.exe' `
  -batchmode -nographics -projectPath 'E:\Unity\Project\AnimalCafe' `
  -runTests -testPlatform PlayMode `
  -testResults 'E:\Unity\Project\AnimalCafe\Logs\Phase0Tests.xml' `
  -logFile 'E:\Unity\Project\AnimalCafe\Logs\Phase0Tests.log'
```

Expected: compilation failure because `GameTimeService` and related types do not exist.

- [ ] **Step 4: Implement minimal Game Time and event contracts**

Use these exact public types:

```csharp
namespace AnimalCafe.Core.Time
{
    public enum GameSpeed { Paused = 0, Normal = 1, Fast = 2 }

    public interface IGameTimeService
    {
        GameSpeed CurrentSpeed { get; }
        bool TrySetSpeed(GameSpeed speed);
    }
}
```

`GameTimeService` must:

- initialize to `Normal`;
- accept only the three defined enum values;
- set `Time.timeScale` to the numeric enum value;
- return `false` and log a warning without changing state for other values;
- call `GameEventBus.PublishGameSpeedChanged(previous, current)` only when state changes;
- restore `Time.timeScale = 1f` in `OnDestroy()` when it is the active owner.

`GameEvents.cs` exposes immutable `SelectionChangedEvent` and `GameSpeedChangedEvent` structs. `GameEventBus.cs` exposes `Action<T>` events, publish methods, and a `ResetForTests()` method that clears static listeners.

- [ ] **Step 5: Run tests and confirm GREEN**

Run the Task 1 Unity command again.

Expected: both Game Time tests pass; Unity exits with code `0`.

- [ ] **Step 6: Review checkpoint**

Check `Logs/Phase0Tests.log` for `error CS`, `NullReferenceException`, and unhandled exceptions. Do not commit.

---

### Task 2: Device-independent Input 与 Mouse Adapter

**Files:**

- Create: `Assets/Scripts/Input/CameraInputFrame.cs`
- Create: `Assets/Scripts/Input/ICameraInputSource.cs`
- Create: `Assets/Scripts/Input/MouseCameraInput.cs`
- Modify: `Assets/Tests/PlayMode/Phase0PlayModeTests.cs`

**Interfaces:**

- Produces: `CameraInputFrame(Vector2 panDelta, float zoomDelta, bool tapReleased, Vector2 pointerPosition)`.
- Produces: `ICameraInputSource.ReadFrame()`.
- Consumes: `CameraSettings.DragThresholdPixels`.
- Consumes: Unity Input System `Mouse.current`.

- [ ] **Step 1: Write failing tap-versus-drag tests**

Add tests against pure helper methods:

```csharp
[TestCase(3f, 6f, true)]
[TestCase(6f, 6f, true)]
[TestCase(6.1f, 6f, false)]
public void MouseInput_TapDependsOnDragDistance(
    float dragDistance, float threshold, bool expected)
{
    Assert.That(MouseCameraInput.IsTapDistance(dragDistance, threshold), Is.EqualTo(expected));
}
```

- [ ] **Step 2: Run the focused suite and confirm RED**

Run the Task 1 Unity command.

Expected: compilation failure because `MouseCameraInput` does not exist.

- [ ] **Step 3: Implement input data and adapter**

Use:

```csharp
public readonly struct CameraInputFrame
{
    public Vector2 PanDelta { get; }
    public float ZoomDelta { get; }
    public bool TapReleased { get; }
    public Vector2 PointerPosition { get; }
}

public interface ICameraInputSource
{
    CameraInputFrame ReadFrame();
}
```

`MouseCameraInput` receives the shared `CameraSettings` reference, then tracks pointer-down position and whether its `DragThresholdPixels` was exceeded. `ReadFrame()` returns:

- drag delta only while left mouse is held and threshold has been exceeded;
- scroll Y as zoom delta;
- `TapReleased = true` only when left mouse is released without exceeding threshold;
- an empty frame when no mouse device exists.

`IsTapDistance(float dragDistance, float threshold)` must be public static, clamp negative threshold to zero, and use `<=`.

- [ ] **Step 4: Run tests and confirm GREEN**

Run the Unity test command.

Expected: Game Time and tap-distance tests all pass.

- [ ] **Step 5: Review checkpoint**

Confirm `MouseCameraInput` only translates hardware input and contains no Camera movement or raycast code. Do not commit.

---

### Task 3: Camera Config、Pan、Zoom 与 Bounds

**Files:**

- Create: `Assets/Scripts/Camera/CameraSettings.cs`
- Create: `Assets/Scripts/Camera/CafeCameraController.cs`
- Modify: `Assets/Tests/PlayMode/Phase0PlayModeTests.cs`

**Interfaces:**

- Consumes: `ICameraInputSource.ReadFrame()`.
- Produces: `CafeCameraController.Configure(Camera camera, CameraSettings settings, ICameraInputSource inputSource)`.
- Produces: `CafeCameraController.ApplyPan(Vector2 screenDelta)` and `ApplyZoom(float scrollDelta)`.
- Produces: `CafeCameraController.ClampToBounds()`.

- [ ] **Step 1: Write failing Camera bounds tests**

Create a Camera and runtime `CameraSettings` instance. Configure:

```csharp
settings.PositionMin = new Vector2(-5f, -4f);
settings.PositionMax = new Vector2(5f, 4f);
settings.MinOrthographicSize = 4f;
settings.MaxOrthographicSize = 10f;
```

Tests must set the Camera beyond each position and zoom boundary, call `ClampToBounds()`, then assert X/Z and `orthographicSize` are within the configured ranges.

- [ ] **Step 2: Run tests and confirm RED**

Expected: compilation failure because Camera types do not exist.

- [ ] **Step 3: Implement `CameraSettings`**

Create a `ScriptableObject` with `[CreateAssetMenu]` and serialized fields:

```csharp
float panSpeed = 0.02f;
float zoomSpeed = 0.2f;
Vector2 positionMin = new(-12f, -10f);
Vector2 positionMax = new(12f, 10f);
float minOrthographicSize = 4f;
float maxOrthographicSize = 12f;
float dragThresholdPixels = 6f;
```

Expose public properties so tests and Editor setup can configure values. `OnValidate()` normalizes reversed min/max values and clamps sizes to positive values.

- [ ] **Step 4: Implement `CafeCameraController`**

Requirements:

- require a Unity Camera and use orthographic projection;
- resolve `ICameraInputSource` from a serialized `MonoBehaviour`;
- use unscaled input behavior so Pause does not block it;
- convert screen drag into world X/Z pan using Camera right and flattened forward vectors;
- invert drag direction so the scene follows the pointer;
- apply zoom to `orthographicSize`;
- clamp X, Z and orthographic size after every change;
- keep Camera Y and rotation unchanged;
- log a clear error and disable itself if Camera, config, or input source is missing.

- [ ] **Step 5: Run tests and confirm GREEN**

Expected: all current tests pass and Camera bounds cases cover both min and max.

- [ ] **Step 6: Review checkpoint**

Confirm `CafeCameraController` never calls `Mouse.current`; only `MouseCameraInput` knows about mouse hardware. Do not commit.

---

### Task 4: Selection Contract、Raycast 与 Color Feedback

**Files:**

- Create: `Assets/Scripts/Interaction/ISelectable.cs`
- Create: `Assets/Scripts/Interaction/ColorSelectable.cs`
- Create: `Assets/Scripts/Interaction/SceneInteractionController.cs`
- Modify: `Assets/Tests/PlayMode/Phase0PlayModeTests.cs`

**Interfaces:**

- Produces: `ISelectable.IsSelected`, `Select()`, `Deselect()`.
- Produces: `SceneInteractionController.CurrentSelection`.
- Produces: `SceneInteractionController.TrySelectAt(Vector2 screenPosition)` and `ClearSelection()`.
- Consumes: Camera, `ICameraInputSource`, `GameEventBus`.

- [ ] **Step 1: Write failing selection tests**

Tests create two cubes with Colliders and `ColorSelectable`. Assert:

- selecting A sets `A.IsSelected`;
- selecting B clears A and selects B;
- `ClearSelection()` clears B;
- calling `Select()` twice is idempotent;
- `Deselect()` restores the original renderer color.

Add a Camera at `(0, 0, -10)`, point it toward the cubes, configure
`SceneInteractionController`, convert cube A with
`camera.WorldToScreenPoint(cubeA.transform.position)`, call
`TrySelectAt(screenPoint)`, and assert `CurrentSelection` is cube A's
`ColorSelectable`. Call `TrySelectAt(new Vector2(-1000f, -1000f))` and assert
`CurrentSelection` is null.

Use a dedicated test material per object and destroy it during teardown.

- [ ] **Step 2: Run tests and confirm RED**

Expected: compilation failure because selection types do not exist.

- [ ] **Step 3: Implement `ISelectable` and `ColorSelectable`**

```csharp
public interface ISelectable
{
    bool IsSelected { get; }
    void Select();
    void Deselect();
}
```

`ColorSelectable` must use `MaterialPropertyBlock` so selection does not instantiate or permanently modify shared materials. Cache the renderer’s original `_BaseColor`, apply serialized `selectedColor`, restore the original value, and safely deselect in `OnDisable()`.

- [ ] **Step 4: Implement `SceneInteractionController`**

Requirements:

- consume only `TapReleased` frames;
- raycast from the configured Camera using pointer screen position;
- find `ISelectable` on the hit object or its parent;
- switch selection in the order “deselect previous, select next”;
- clicking empty or non-selectable space calls `ClearSelection()`;
- publish one selection-changed event only when the selected object actually changes;
- clear a disabled/destroyed selection without throwing;
- disable with a clear Console error if Camera or input source is missing.

- [ ] **Step 5: Run tests and confirm GREEN**

Expected: selection, switching, deselection and color restoration tests pass.

- [ ] **Step 6: Review checkpoint**

Confirm interaction code does not know the concrete feedback type; it only calls `ISelectable`. Do not commit.

---

### Task 5: Time-controlled Mover、UI 与 Reproducible Scene Setup

**Files:**

- Create: `Assets/Scripts/Testing/TimeTestMover.cs`
- Create: `Assets/Scripts/UI/TimeControlPanel.cs`
- Create: `Assets/Editor/Phase0SceneSetup.cs`
- Create through Editor API: `Assets/Config/DefaultCameraSettings.asset`
- Modify through Editor API: `Assets/Scenes/MainCafe.unity`
- Modify through Editor API: `ProjectSettings/EditorBuildSettings.asset`
- Modify: `Assets/Tests/PlayMode/Phase0PlayModeTests.cs`

**Interfaces:**

- Produces: `TimeTestMover.Configure(Vector3 pointA, Vector3 pointB, float unitsPerSecond)`.
- Produces: `TimeControlPanel.Configure(GameTimeService service, Button pause, Button normal, Button fast)`.
- Consumes: `GameTimeService.SetPaused()`, `SetNormal()`, `SetFast()`.

- [ ] **Step 1: Write failing mover speed test**

Use `[UnityTest]` and real-time waits:

```csharp
[UnityTest]
public IEnumerator TimeMover_FastMovesFartherThanNormal()
{
    var serviceObject = new GameObject("GameTimeService");
    var service = serviceObject.AddComponent<GameTimeService>();
    var moverObject = new GameObject("TimeTestMover");
    var mover = moverObject.AddComponent<TimeTestMover>();
    mover.Configure(Vector3.zero, Vector3.right * 10f, 1f);

    service.SetNormal();
    mover.ResetToStart();
    yield return new WaitForSecondsRealtime(0.25f);
    var normalDistance = mover.transform.position.x;

    service.SetFast();
    mover.ResetToStart();
    yield return new WaitForSecondsRealtime(0.25f);
    var fastDistance = mover.transform.position.x;

    Assert.That(fastDistance, Is.GreaterThan(normalDistance * 1.7f));
    Object.Destroy(serviceObject);
    Object.Destroy(moverObject);
}

[UnityTest]
public IEnumerator TimeMover_PauseStopsMovement()
{
    var serviceObject = new GameObject("GameTimeService");
    var service = serviceObject.AddComponent<GameTimeService>();
    var moverObject = new GameObject("TimeTestMover");
    var mover = moverObject.AddComponent<TimeTestMover>();
    mover.Configure(Vector3.zero, Vector3.right * 10f, 1f);

    service.SetPaused();
    var start = mover.transform.position;
    yield return new WaitForSecondsRealtime(0.15f);

    Assert.That(Vector3.Distance(start, mover.transform.position), Is.LessThan(0.001f));
    Object.Destroy(serviceObject);
    Object.Destroy(moverObject);
}
```

- [ ] **Step 2: Run tests and confirm RED**

Expected: compilation failure because `TimeTestMover` does not exist.

- [ ] **Step 3: Implement `TimeTestMover`**

Use `Vector3.MoveTowards(transform.position, target, unitsPerSecond * Time.deltaTime)`. Swap endpoints when within `0.01f`. Validate speed is non-negative. Expose `ResetToStart()` for deterministic tests.

- [ ] **Step 4: Implement `TimeControlPanel`**

Register three button listeners in `OnEnable()` and remove them in `OnDisable()`. Each handler calls exactly one `GameTimeService` method. Missing service or buttons logs one clear error and disables the panel.

- [ ] **Step 5: Implement the idempotent Editor setup tool**

`Phase0SceneSetup.ConfigurePhase0Scene()` must:

1. open `Assets/Scenes/MainCafe.unity`;
2. preserve existing `CafeFloor`, `Directional Light` and `Global Volume`;
3. configure `Main Camera` as orthographic at a fixed angled top-down transform;
4. create or update `DefaultCameraSettings.asset`;
5. create one `Phase0_Runtime` root containing input, Camera, time and interaction components;
6. create or update two selectable cubes and one moving test cube with distinct names;
7. create or update a simple Screen Space Overlay Canvas with Pause / `1x` / `2x` buttons;
8. wire every serialized reference using `SerializedObject`;
9. avoid duplicate objects when run more than once;
10. save `MainCafe` and make it the only enabled build scene;
11. throw a clear `InvalidOperationException` when required existing scene objects are missing.

Expose both a Unity menu item `AnimalCafe/Phase 0/Configure Scene` and a public static method usable with `-executeMethod`.

- [ ] **Step 6: Run the scene setup in Unity batch mode**

Run:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.5.5f1\Editor\Unity.exe' `
  -batchmode -quit -projectPath 'E:\Unity\Project\AnimalCafe' `
  -executeMethod AnimalCafe.EditorTools.Phase0SceneSetup.ConfigurePhase0Scene `
  -logFile 'E:\Unity\Project\AnimalCafe\Logs\Phase0SceneSetup.log'
```

Expected: Unity exits with code `0`; `MainCafe.unity`, Camera config and build settings are updated.

- [ ] **Step 7: Re-run the setup to prove idempotency**

Run the same command again.

Expected: Unity exits with code `0`; scene contains exactly one runtime root, one Canvas, two selectable objects and one time-test mover.

- [ ] **Step 8: Run Play Mode tests and confirm GREEN**

Run the full Unity test command.

Expected: mover Pause and relative-speed tests pass with all earlier tests.

- [ ] **Step 9: Review checkpoint**

Inspect `Phase0SceneSetup.log` and `Phase0Tests.log`. Confirm there are no compilation errors, missing scripts, missing serialized references or unhandled exceptions. Do not commit.

---

### Task 6: Full Completion Gate 与 Roadmap Update

**Files:**

- Modify only after verification: `Docs/AnimalCafe_Development_Roadmap.md`
- Verify: all Phase 0 runtime, scene, config and test files

**Interfaces:**

- Consumes: the complete Phase 0 scene and test suite.
- Produces: verified Phase 0 completion evidence.

- [ ] **Step 1: Run a clean Unity import and full Play Mode suite**

Close any interactive Unity instance that has the project locked, then run the full Unity test command from Task 1.

Expected: Unity exits with code `0`; every test in `Phase0PlayModeTests` passes.

- [ ] **Step 2: Scan the Unity logs**

Run:

```powershell
rg -n "error CS|NullReferenceException|MissingReferenceException|Unhandled|AssertionException" `
  Logs/Phase0SceneSetup.log Logs/Phase0Tests.log
```

Expected: no matching unhandled error. Expected warning tests must use `LogAssert.Expect` so they do not appear as unexplained failures.

- [ ] **Step 3: Perform manual Play Mode checks**

Open `MainCafe`, enter Play Mode, then verify in order:

1. left-drag pans Camera;
2. releasing after a drag does not select an object;
3. mouse wheel zooms;
4. repeated pan and zoom remain inside bounds;
5. tap selects a cube and changes its color;
6. tapping the second cube restores the first cube;
7. tapping empty floor clears selection;
8. Pause stops the moving cube;
9. Camera, selection and buttons still work while paused;
10. `2x` is visibly faster than `1x`;
11. Console has no unhandled error.

- [ ] **Step 4: Update Roadmap completion evidence**

Only after Steps 1–3 pass, change Phase 0 status in `Docs/AnimalCafe_Development_Roadmap.md` from planned to completed and add a short verification note containing:

- Unity version `6000.5.5f1`;
- automated Play Mode test count and pass result;
- manual gate result;
- completion date `2026-07-24`.

- [ ] **Step 5: Review all changes without staging**

Run:

```powershell
git status --short
git diff --check
git diff --stat
```

Expected:

- no whitespace errors;
- only planned Phase 0 files plus pre-existing user changes are present;
- no `Library`, `Temp`, generated IDE files, logs or secrets are tracked.

- [ ] **Step 6: Final handoff**

Report:

- files created and modified;
- automated test results and location of XML/log evidence;
- manual checks completed and any checks that still require the user;
- exact Unity steps for replaying the Phase 0 demo;
- Git remains uncommitted unless the user separately requests commit.
