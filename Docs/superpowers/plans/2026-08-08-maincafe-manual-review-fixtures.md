# MainCafe Temporary Manual Review Fixtures Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Add two explicitly temporary, selectable MainCafe cubes so the Studio Owner can visibly verify Pause, 1x, 2x, Camera pan/zoom, and select/deselect behavior.

**Architecture:** A small runtime `ManualReviewPingPongMover` owns only scaled-time local movement. A Phase 4 Editor setup utility deterministically creates the Scene root, two cubes, and two temporary materials through Unity APIs. PlayMode tests cover the mover in isolation and the committed MainCafe integration; documentation and the manual-review workbook remain the acceptance source.

**Tech Stack:** Unity 6000.5.5f1, C#, NUnit/Unity Test Framework, URP Lit materials, UnityEditor scene APIs, `@oai/artifact-tool` for `.xlsx` updates.

## Global Constraints

- Work only in `E:\Unity\Project\AnimalCafe\.worktrees\phase-4` on `codex/phase-4`.
- Do not modify the main checkout.
- Do not commit or push without separate explicit Studio Owner authorization.
- The Scene root name is exactly `TEMP_P4_ManualReviewFixtures_DELETE_LATER`.
- The children are exactly `ReviewCube_Moving` and `ReviewCube_Static`.
- Both cubes are visible, collidable, and selectable; only the moving cube moves.
- Movement uses `Time.deltaTime`; it must stop at Pause and respond to 1x/2x.
- Fixtures do not register in furniture catalogues, Grid occupancy, NavMesh, or production gameplay content.
- Scene changes must be produced through Unity Editor APIs, never by hand-editing `.unity` YAML.
- The temporary root, two materials, setup utility, mover, related tests, and documentation are removed together after formal MainCafe visuals replace their review purpose.

## File Structure

- Create `Assets/Scripts/Diagnostics/ManualReviewPingPongMover.cs`: scaled-time local ping-pong movement with a small testable configuration API.
- Create `Assets/Scripts/Diagnostics/ManualReviewPingPongMover.cs.meta`: Unity-generated script metadata.
- Create `Assets/Editor/Phase4/MainCafeManualReviewFixtureSetup.cs`: deterministic material and MainCafe fixture creation.
- Create `Assets/Editor/Phase4/MainCafeManualReviewFixtureSetup.cs.meta`: Unity-generated editor-script metadata.
- Create `Assets/Art/Phase4/Materials/M_TEMP_ManualReviewCube_Moving.mat`: temporary coral/orange URP Lit material.
- Create `Assets/Art/Phase4/Materials/M_TEMP_ManualReviewCube_Static.mat`: temporary sage/green URP Lit material.
- Modify `Assets/Scenes/MainCafe.unity`: add the temporary root and two configured cubes.
- Create `Assets/Tests/PlayMode/Phase4/ManualReviewPingPongMoverTests.cs`: isolated scaled-time movement coverage.
- Modify `Assets/Tests/PlayMode/Phase0PlayModeTests.cs`: committed MainCafe structure, time-control, and selection coverage.
- Modify `Docs/Phase4_Beginner_Guide.md`: replace runtime Cube creation with the committed fixture workflow.
- Modify `outputs/phase4-manual-review/AnimalCafe_P4_Manual_Review_Updated.xlsx`: update M76–M78 and M83–M84 re-review instructions after verification.

---

### Task 1: Scaled-Time Ping-Pong Mover

**Files:**
- Create: `Assets/Tests/PlayMode/Phase4/ManualReviewPingPongMoverTests.cs`
- Create: `Assets/Scripts/Diagnostics/ManualReviewPingPongMover.cs`

**Interfaces:**
- Produces: `AnimalCafe.Diagnostics.ManualReviewPingPongMover`
- Produces: `void Configure(Vector3 localPointA, Vector3 localPointB, float unitsPerSecond)`
- Produces: `void ResetToStart()`
- Produces: read-only `LocalPointA`, `LocalPointB`, and `UnitsPerSecond` properties.

- [x] **Step 1: Write the failing PlayMode movement test**

Create a test that configures endpoints far enough apart to avoid reaching the end during the observation window:

```csharp
[UnityTest]
public IEnumerator MovementUsesScaledTimeForPauseNormalAndFast()
{
    var fixture = new GameObject("ManualReviewMoverFixture");
    var mover = fixture.AddComponent<ManualReviewPingPongMover>();
    mover.Configure(Vector3.zero, Vector3.right * 10f, 1f);

    try
    {
        Time.timeScale = 0f;
        mover.ResetToStart();
        yield return new WaitForSecondsRealtime(0.15f);
        Assert.That(fixture.transform.localPosition.x, Is.EqualTo(0f).Within(0.001f));

        Time.timeScale = 1f;
        mover.ResetToStart();
        yield return new WaitForSecondsRealtime(0.2f);
        var normalDistance = fixture.transform.localPosition.x;

        Time.timeScale = 2f;
        mover.ResetToStart();
        yield return new WaitForSecondsRealtime(0.2f);
        var fastDistance = fixture.transform.localPosition.x;

        Assert.That(normalDistance, Is.GreaterThan(0.05f));
        Assert.That(fastDistance, Is.GreaterThan(normalDistance * 1.7f));
    }
    finally
    {
        Time.timeScale = 1f;
        Object.DestroyImmediate(fixture);
    }
}
```

- [x] **Step 2: Run the focused test and verify RED**

Run Unity 6000.5.5f1 with:

```powershell
& 'E:\Unity\Hub\Editor\6000.5.5f1\Editor\Unity.exe' `
  -projectPath 'E:\Unity\Project\AnimalCafe\.worktrees\phase-4' `
  -runTests -testPlatform PlayMode `
  -testFilter 'AnimalCafe.Tests.PlayMode.Phase4.ManualReviewPingPongMoverTests.MovementUsesScaledTimeForPauseNormalAndFast' `
  -testResults 'outputs\phase4-manual-review\manual-review-mover-red.xml' `
  -logFile 'outputs\phase4-manual-review\manual-review-mover-red.log'
```

Expected: compilation/test failure because `ManualReviewPingPongMover` does not exist.

- [x] **Step 3: Implement the minimal mover**

Use local positions so moving the temporary root preserves the path:

```csharp
namespace AnimalCafe.Diagnostics
{
    public sealed class ManualReviewPingPongMover : MonoBehaviour
    {
        [SerializeField] private Vector3 localPointA = new(-2f, 0.5f, -1f);
        [SerializeField] private Vector3 localPointB = new(2f, 0.5f, -1f);
        [SerializeField, Min(0.01f)] private float unitsPerSecond = 1f;
        private Vector3 target;

        public Vector3 LocalPointA => localPointA;
        public Vector3 LocalPointB => localPointB;
        public float UnitsPerSecond => unitsPerSecond;

        private void Awake() => ResetToStart();

        private void Update()
        {
            transform.localPosition = Vector3.MoveTowards(
                transform.localPosition,
                target,
                unitsPerSecond * Time.deltaTime);
            if ((transform.localPosition - target).sqrMagnitude <= 0.000001f)
            {
                target = target == localPointB ? localPointA : localPointB;
            }
        }

        public void Configure(Vector3 pointA, Vector3 pointB, float speed)
        {
            localPointA = pointA;
            localPointB = pointB;
            unitsPerSecond = Mathf.Max(0.01f, speed);
            ResetToStart();
        }

        public void ResetToStart()
        {
            transform.localPosition = localPointA;
            target = localPointB;
        }
    }
}
```

- [x] **Step 4: Run the focused test and verify GREEN**

Expected: `1/1 Passed`, no unexpected Console messages, and `Time.timeScale` restored to 1.

- [x] **Step 5: Review gate**

Inspect only the mover and its test. Do not commit; record the diff and wait for the next task checkpoint.

---

### Task 2: Deterministic MainCafe Fixture Construction

**Files:**
- Create: `Assets/Editor/Phase4/MainCafeManualReviewFixtureSetup.cs`
- Create: `Assets/Art/Phase4/Materials/M_TEMP_ManualReviewCube_Moving.mat`
- Create: `Assets/Art/Phase4/Materials/M_TEMP_ManualReviewCube_Static.mat`
- Modify: `Assets/Scenes/MainCafe.unity`
- Modify: `Assets/Tests/PlayMode/Phase0PlayModeTests.cs`

**Interfaces:**
- Consumes: `ManualReviewPingPongMover.Configure(Vector3, Vector3, float)`.
- Produces: `MainCafeManualReviewFixtureSetup.Apply()` as the Unity command-line entry point.
- Produces: exact root/child names used by documentation and tests.

- [x] **Step 1: Extend the existing MainCafe PlayMode test for RED structure coverage**

Inside `MainCafe_LoadsWithRequiredPhase0Objects`, add exact assertions:

```csharp
var reviewRoot = GameObject.Find("TEMP_P4_ManualReviewFixtures_DELETE_LATER");
Assert.That(reviewRoot, Is.Not.Null);
Assert.That(reviewRoot.transform.childCount, Is.EqualTo(2));

var moving = reviewRoot.transform.Find("ReviewCube_Moving");
var stationary = reviewRoot.transform.Find("ReviewCube_Static");
Assert.That(moving, Is.Not.Null);
Assert.That(stationary, Is.Not.Null);
Assert.That(moving.GetComponent<ManualReviewPingPongMover>(), Is.Not.Null);
Assert.That(stationary.GetComponent<ManualReviewPingPongMover>(), Is.Null);

foreach (var cube in new[] { moving, stationary })
{
    Assert.That(cube.GetComponent<MeshRenderer>(), Is.Not.Null);
    Assert.That(cube.GetComponent<BoxCollider>(), Is.Not.Null);
    Assert.That(cube.GetComponent<ColorSelectable>(), Is.Not.Null);
}
```

- [x] **Step 2: Run the focused MainCafe test and verify RED**

Expected: failure because `TEMP_P4_ManualReviewFixtures_DELETE_LATER` is absent.

- [x] **Step 3: Implement the Editor setup utility**

`Apply()` must:

1. open `Assets/Scenes/MainCafe.unity` with `EditorSceneManager.OpenScene(..., OpenSceneMode.Single)`;
2. delete any existing exact temporary root;
3. create or update two URP Lit materials at the exact paths;
4. create a zeroed root;
5. create primitive cubes and parent them to the root;
6. name/configure the moving cube at local point A `(-2, 0.5, -1)`, point B `(2, 0.5, -1)`, speed `1.5`;
7. place the static cube at `(0.5, 0.5, 2)`;
8. add `ColorSelectable` to both cubes;
9. assign the moving material color `(0.92, 0.36, 0.20, 1)` and static material color `(0.32, 0.62, 0.42, 1)`;
10. save the Scene and assets.

Core construction pattern:

```csharp
var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
cube.name = name;
cube.transform.SetParent(root.transform, false);
cube.transform.localPosition = localPosition;
cube.GetComponent<MeshRenderer>().sharedMaterial = material;
cube.AddComponent<ColorSelectable>();
```

Use `Shader.Find("Universal Render Pipeline/Lit")`; throw `InvalidOperationException` if unavailable. Refuse to save if the opened Scene path is not the exact MainCafe path.

- [x] **Step 4: Execute the setup through Unity**

Run without `-batchmode` because this machine's headless entitlement is unavailable:

```powershell
& 'E:\Unity\Hub\Editor\6000.5.5f1\Editor\Unity.exe' `
  -projectPath 'E:\Unity\Project\AnimalCafe\.worktrees\phase-4' `
  -executeMethod 'AnimalCafe.EditorTools.Phase4.MainCafeManualReviewFixtureSetup.Apply' `
  -logFile 'outputs\phase4-manual-review\build-maincafe-review-fixtures.log' `
  -quit
```

Expected log: exact root created, exactly two children saved, no missing shader/material/component error.

- [x] **Step 5: Re-run the focused MainCafe test and verify GREEN**

Expected: structure/component assertions pass and MainCafe reports no missing scripts.

- [x] **Step 6: Determinism check**

Run `Apply()` a second time and confirm there is still one exact root with two exact children and no duplicate materials.

- [x] **Step 7: Review gate**

Inspect the Scene in Unity Camera/Game view and confirm both cubes are initially visible and visually distinct. Do not mark M76–M84 Passed yet.

---

### Task 3: MainCafe Time and Selection Integration

**Files:**
- Modify: `Assets/Tests/PlayMode/Phase0PlayModeTests.cs`

**Interfaces:**
- Consumes: MainCafe `Phase0_Runtime/GameTimeService`.
- Consumes: `ManualReviewPingPongMover.ResetToStart()`.
- Consumes: both Scene `ColorSelectable` components.

- [x] **Step 1: Write the failing integration tests**

Add one movement test using the committed MainCafe moving cube:

```csharp
[UnityTest]
public IEnumerator MainCafeReviewMoverRespondsToPauseNormalAndFast()
{
    yield return SceneManager.LoadSceneAsync("MainCafe");
    yield return null;
    var service = GameObject.Find("Phase0_Runtime").GetComponent<GameTimeService>();
    var mover = GameObject.Find("ReviewCube_Moving")
        .GetComponent<ManualReviewPingPongMover>();

    service.SetPaused();
    mover.ResetToStart();
    yield return new WaitForSecondsRealtime(0.15f);
    Assert.That(mover.transform.localPosition, Is.EqualTo(mover.LocalPointA));

    service.SetNormal();
    mover.ResetToStart();
    yield return new WaitForSecondsRealtime(0.2f);
    var normalDistance = Vector3.Distance(mover.LocalPointA, mover.transform.localPosition);

    service.SetFast();
    mover.ResetToStart();
    yield return new WaitForSecondsRealtime(0.2f);
    var fastDistance = Vector3.Distance(mover.LocalPointA, mover.transform.localPosition);
    Assert.That(fastDistance, Is.GreaterThan(normalDistance * 1.7f));
}
```

Add a selection contract test:

```csharp
[UnityTest]
public IEnumerator MainCafeReviewCubesAreSelectableAndResettable()
{
    yield return SceneManager.LoadSceneAsync("MainCafe");
    yield return null;
    foreach (var name in new[] { "ReviewCube_Moving", "ReviewCube_Static" })
    {
        var selectable = GameObject.Find(name).GetComponent<ColorSelectable>();
        selectable.Select();
        Assert.That(selectable.IsSelected, Is.True);
        selectable.Deselect();
        Assert.That(selectable.IsSelected, Is.False);
    }
}
```

Ensure each test restores `Time.timeScale = 1f` and unloads/resets MainCafe through the existing test isolation pattern.

- [x] **Step 2: Run both tests and verify their initial result**

The tests may already pass after Task 2; if so, temporarily remove the mover or `ColorSelectable` in a disposable Scene copy to prove the assertions fail, then restore the generated Scene before continuing. Do not alter the production MainCafe asset for the RED proof.

- [x] **Step 3: Make only the minimum fixture/configuration correction required**

If a test fails, correct only the matching setup value or component assignment in `MainCafeManualReviewFixtureSetup.Apply()`, rebuild MainCafe, and avoid changes to GameTimeService, Camera, or selection production logic.

- [x] **Step 4: Run the Phase0 + RealUi PlayMode fixtures**

Expected: all selected tests pass, including the recently fixed RealUi Input isolation tests.

- [x] **Step 5: Review gate**

Record the focused XML path and exact count. Do not commit without explicit authorization.

---

### Task 4: Beginner Guide and Manual Workbook

**Files:**
- Modify: `Docs/Phase4_Beginner_Guide.md`
- Modify: `outputs/phase4-manual-review/AnimalCafe_P4_Manual_Review_Updated.xlsx`

**Interfaces:**
- Consumes exact root/child names from Task 2.
- Produces beginner-readable M76–M84 manual steps and updated review evidence.

- [x] **Step 1: Update the guide**

Replace the temporary runtime Cube creation instructions with:

```text
Open MainCafe and enter Play Mode. Use ReviewCube_Moving to check Pause, 1x, and 2x. Use both ReviewCube_Moving and ReviewCube_Static as Camera pan/zoom references. Click each cube, then click empty background, to check select/deselect. These objects live under TEMP_P4_ManualReviewFixtures_DELETE_LATER and will be removed after formal MainCafe visuals are available.
```

Keep M76–M78 and M83–M84 explicitly assigned to MainCafe, then tell the reviewer to return to `Phase4CoreArchitecture` afterward.

- [x] **Step 2: Update guide contract tests before changing the guide**

Change the existing beginner-guide assertion to require all three exact fixture names and the future-removal statement; run it RED against the old guide, then update the guide and rerun GREEN.

- [x] **Step 3: Re-read the Studio Owner workbook before editing**

Use `@oai/artifact-tool`, render `Review Routing`, and preserve any new Studio Owner statuses/comments. Update only M76–M78 and M83–M84 instructions/evidence plus the Codex Diagnosis technical evidence. Do not mark these manual items Passed for the Studio Owner.

- [x] **Step 4: Render and verify all workbook sheets**

Confirm no formula errors, no clipped key text, and status totals still reconcile to 88 items. Remove builder scripts, previews, junctions, and inspect sidecars after export.

---

### Task 5: Full Verification and Handoff

**Files:**
- Verify all files listed above.
- Do not modify unrelated dirty files.

**Interfaces:**
- Produces final XML/log evidence and the Studio Owner manual re-review handoff.

- [x] **Step 1: Run focused tests**

Expected: mover, MainCafe fixture, time-control, selection, guide-contract, and RealUi isolation tests all pass with zero failures.

- [x] **Step 2: Run Full EditMode**

Write results to `outputs/phase4-manual-review/maincafe-fixtures-full-editmode.xml`; expected zero failures.

- [x] **Step 3: Run Full PlayMode three times**

Write `maincafe-fixtures-full-playmode-run1.xml`, `run2.xml`, and `run3.xml`; expected `66 + newly added tests`, zero failures in every run and no `statePtr` exception.

- [x] **Step 4: Verify Scene and Git isolation**

Confirm:

```powershell
git branch --show-current
git rev-parse --show-toplevel
git diff --check
git -C E:\Unity\Project\AnimalCafe status --short --branch
```

Expected: current branch `codex/phase-4`, root is the phase-4 worktree, MainCafe changes exist only in that worktree, and main contains no new fixture changes.

- [x] **Step 5: Studio Owner manual review**

Ask the Studio Owner to complete M76–M78 and M83–M84 using the two committed cubes. M79–M82 remain as previously reviewed unless new evidence contradicts them.

- [x] **Step 6: Commit gate**

Do not stage, commit, or push. Present the exact changed-file list and test evidence, then wait for explicit authorization.
