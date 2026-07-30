# Phase 0 Game Time Owner Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prevent a duplicate `GameTimeService` from changing global game time or publishing misleading speed events.

**Architecture:** Keep the existing static `activeOwner` design. Add one owner guard at the start of `TrySetSpeed()` so only the registered owner can change `CurrentSpeed`, `Time.timeScale`, or publish `GameSpeedChanged`; duplicates return `false` with a deterministic warning and never auto-promote.

**Tech Stack:** Unity `6000.5.5f1`, C#, NUnit, Unity Test Framework PlayMode tests.

## Global Constraints

- Work only on `main`.
- Preserve the user's existing `.gitignore` and `AnimalCafe.slnx` changes.
- Do not modify Scene, Prefab, Phase 1 Layout, or Phase 2 worktree files.
- Use strict RED → GREEN TDD.
- Do not commit, push, merge, or delete a branch.
- Do not use Unity `-quit`.

## File Map

- Modify `Assets/Tests/PlayMode/Phase0PlayModeTests.cs`: reproduce duplicate-owner behavior and protect lifecycle/event contracts.
- Modify `Assets/Scripts/Core/Time/GameTimeService.cs`: reject speed changes from non-owner instances.
- Verify `Docs/superpowers/specs/2026-07-30-phase-0-game-time-owner-hardening-design.md`: approved behavior contract; no further content change required.

---

### Task 1: Reproduce and Fix Duplicate Time Ownership

**Files:**

- Modify: `Assets/Tests/PlayMode/Phase0PlayModeTests.cs`
- Modify: `Assets/Scripts/Core/Time/GameTimeService.cs`

**Interfaces:**

- Consumes: `GameTimeService.TrySetSpeed(GameSpeed speed)`, `GameTimeService.CurrentSpeed`, `GameEventBus.GameSpeedChanged`, `GameEventBus.ResetForTests()`, `UnityEngine.Time.timeScale`.
- Produces: Existing `TrySetSpeed` signature remains unchanged; non-owner calls return `false` without mutation.

- [ ] **Step 1: Add the event namespace**

Add:

```csharp
using AnimalCafe.Core.Events;
```

- [ ] **Step 2: Write the failing duplicate-mutation test**

Add a PlayMode test that creates the owner first and duplicate second:

```csharp
[Test]
public void GameTime_DuplicateCannotChangeTimeOrPublishEvent()
{
    GameEventBus.ResetForTests();
    var ownerObject = new GameObject("OwnerGameTimeService");
    var duplicateObject = new GameObject("DuplicateGameTimeService");

    try
    {
        var owner = ownerObject.AddComponent<GameTimeService>();
        var duplicate = duplicateObject.AddComponent<GameTimeService>();
        var eventCount = 0;
        GameEventBus.GameSpeedChanged += _ => eventCount++;

        Assert.That(owner.TrySetSpeed(GameSpeed.Fast), Is.True);
        Assert.That(Time.timeScale, Is.EqualTo(2f));
        Assert.That(eventCount, Is.EqualTo(1));

        LogAssert.Expect(
            LogType.Warning,
            "[GameTimeService] Ignored speed change from duplicate instance.");
        Assert.That(duplicate.TrySetSpeed(GameSpeed.Paused), Is.False);

        Assert.That(duplicate.CurrentSpeed, Is.EqualTo(GameSpeed.Normal));
        Assert.That(Time.timeScale, Is.EqualTo(2f));
        Assert.That(eventCount, Is.EqualTo(1));
    }
    finally
    {
        Object.DestroyImmediate(duplicateObject);
        Object.DestroyImmediate(ownerObject);
        GameEventBus.ResetForTests();
    }
}
```

The production change that makes this test pass is an `activeOwner != this` guard before any speed validation or mutation.

- [ ] **Step 3: Write the failing destroy-order test**

Add:

```csharp
[Test]
public void GameTime_DestroyingDuplicateDoesNotAffectOwner()
{
    var ownerObject = new GameObject("OwnerGameTimeService");
    var duplicateObject = new GameObject("DuplicateGameTimeService");

    try
    {
        var owner = ownerObject.AddComponent<GameTimeService>();
        duplicateObject.AddComponent<GameTimeService>();

        Assert.That(owner.TrySetSpeed(GameSpeed.Fast), Is.True);
        Object.DestroyImmediate(duplicateObject);

        Assert.That(owner.CurrentSpeed, Is.EqualTo(GameSpeed.Fast));
        Assert.That(Time.timeScale, Is.EqualTo(2f));
    }
    finally
    {
        if (duplicateObject != null)
        {
            Object.DestroyImmediate(duplicateObject);
        }

        Object.DestroyImmediate(ownerObject);
    }
}
```

- [ ] **Step 4: Write the failing no-auto-promotion test**

Add:

```csharp
[Test]
public void GameTime_DuplicateDoesNotAutoPromoteAfterOwnerIsDestroyed()
{
    var ownerObject = new GameObject("OwnerGameTimeService");
    var duplicateObject = new GameObject("DuplicateGameTimeService");

    try
    {
        var owner = ownerObject.AddComponent<GameTimeService>();
        var duplicate = duplicateObject.AddComponent<GameTimeService>();

        Assert.That(owner.TrySetSpeed(GameSpeed.Fast), Is.True);
        Object.DestroyImmediate(ownerObject);
        Assert.That(Time.timeScale, Is.EqualTo(1f));

        LogAssert.Expect(
            LogType.Warning,
            "[GameTimeService] Ignored speed change from duplicate instance.");
        Assert.That(duplicate.TrySetSpeed(GameSpeed.Paused), Is.False);
        Assert.That(duplicate.CurrentSpeed, Is.EqualTo(GameSpeed.Normal));
        Assert.That(Time.timeScale, Is.EqualTo(1f));
    }
    finally
    {
        if (ownerObject != null)
        {
            Object.DestroyImmediate(ownerObject);
        }

        Object.DestroyImmediate(duplicateObject);
    }
}
```

- [ ] **Step 5: Run focused PlayMode tests and verify RED**

Run Unity batchmode without `-quit`, filtering:

```text
AnimalCafe.Tests.PlayMode.Phase0PlayModeTests
```

Write:

```text
Logs/Phase0GameTimeOwnerRed.xml
Logs/Phase0GameTimeOwnerRed.log
```

Expected RED on old production code:

- duplicate `TrySetSpeed(GameSpeed.Paused)` returns `true` instead of `false`;
- it changes `Time.timeScale` and/or publishes a second event;
- the expected duplicate warning is missing.

The RED must be a behavior assertion failure, not a compile error or unrelated Console error.

- [ ] **Step 6: Implement the minimal owner guard**

At the beginning of `GameTimeService.TrySetSpeed`, before `IsSupported(speed)`, add:

```csharp
if (activeOwner != this)
{
    Debug.LogWarning(
        "[GameTimeService] Ignored speed change from duplicate instance.");
    return false;
}
```

Do not destroy duplicates, auto-promote them, or add a new ownership framework.

- [ ] **Step 7: Run focused PlayMode tests and verify GREEN**

Run the same filtered suite and write:

```text
Logs/Phase0GameTimeOwnerGreen.xml
Logs/Phase0GameTimeOwnerGreen.log
```

Require:

- every focused test passed;
- failed, skipped, and inconclusive are all `0`;
- no unexpected Console error or warning.

- [ ] **Step 8: Run full regression**

Run full EditMode:

```text
Logs/Phase0GameTimeOwnerFullEditMode.xml
Logs/Phase0GameTimeOwnerFullEditMode.log
```

Run full PlayMode:

```text
Logs/Phase0GameTimeOwnerFullPlayMode.xml
Logs/Phase0GameTimeOwnerFullPlayMode.log
```

Require all tests passed and all non-pass counts `0`.

- [ ] **Step 9: Perform final repository checks**

Run:

```powershell
git diff --check
git status --short --branch
git diff -- Assets/Scripts/Core/Time/GameTimeService.cs Assets/Tests/PlayMode/Phase0PlayModeTests.cs
```

Confirm:

- production/test diff contains only the approved owner guard and tests;
- `.gitignore` and `AnimalCafe.slnx` remain untouched by this task;
- P2 worktree cleanup remains separate;
- no Scene, Prefab, ProjectSettings, generated `.slnx`, or unrelated code changed.

- [ ] **Step 10: Stop before Git operations**

Do not stage, commit, push, merge, or delete anything. Give the user exact test counts and GitHub Desktop file guidance.
