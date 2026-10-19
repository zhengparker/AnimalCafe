using System;
using System.Collections;
using System.Linq;
using AnimalCafe.Core.Time;
using AnimalCafe.Employees;
using AnimalCafe.EditorTools.Phase13;
using AnimalCafe.Navigation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace AnimalCafe.Tests.EditMode.Phase13
{
    public sealed class EmployeePlaySessionTests
    {
        [Serializable] private sealed class SavedScene { public string path; public bool loaded, active; }
        [Serializable] private sealed class SavedScenes { public SavedScene[] scenes; }
        private const string Key = "AnimalCafe.P13.Task7.TrueReload.";
        [UnityTest] public IEnumerator RepeatedPlayStartsOneFreshSessionWithDomainReload()
        {
            // UTF restores an IEnumerator program counter, not its locals, across true Domain Reload.
            // Keep only test checkpoint/restoration data in Editor SessionState (never product state).
            SessionState.SetBool(Key + "enabled", EditorSettings.enterPlayModeOptionsEnabled);
            SessionState.SetInt(Key + "options", (int)EditorSettings.enterPlayModeOptions);
            SessionState.SetFloat(Key + "capture", Time.captureDeltaTime);
            SessionState.SetString(Key + "scenes", JsonUtility.ToJson(new SavedScenes { scenes = EditorSceneManager.GetSceneManagerSetup()
                .Select(value => new SavedScene { path = value.path, loaded = value.isLoaded, active = value.isActive }).ToArray() }));
            SessionState.SetInt(Key + "run", 0); SessionState.SetString(Key + "actor", "");
            EditorSettings.enterPlayModeOptionsEnabled = true; EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.None;
            EditorSceneManager.OpenScene(Phase13EmployeeTaskSceneSetup.ScenePath, OpenSceneMode.Single);
            try
            {
                while (SessionState.GetInt(Key + "run", 0) < 2)
                {
                    yield return new EnterPlayMode();
                    Time.captureDeltaTime = 1f / 60;
                    EmployeeFlowController flow = null; Exception failure = null;
                    for (var frame = 0; frame < 600; frame++)
                    {
                        flow = UnityEngine.Object.FindFirstObjectByType<EmployeeFlowController>();
                        if (flow != null && flow.Snapshot.All(value => value.State == EmployeeState.Idle)) break;
                        yield return null;
                    }
                    try
                    {
                        Assert.That(flow, Is.Not.Null); Assert.That(flow.Snapshot.Count, Is.EqualTo(2));
                        Assert.That(flow.Snapshot.All(value => value.State == EmployeeState.Idle), Is.True);
                        Assert.That(UnityEngine.Object.FindObjectsByType<EmployeeFlowController>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
                        Assert.That(UnityEngine.Object.FindObjectsByType<EmployeeTaskValidationController>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
                        Assert.That(UnityEngine.Object.FindObjectsByType<NavigationActor>(FindObjectsSortMode.None).Length, Is.EqualTo(2));
                        Assert.That(UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Count(value => value.name == "P13 Task HUD"), Is.EqualTo(1));
                        Assert.That(UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Any(value => value.name.StartsWith("PickupCup_")), Is.False);
                        var id = flow.Snapshot[0].ActorId; Assert.That(id, Is.Not.EqualTo(SessionState.GetString(Key + "actor", "")));
                        SessionState.SetString(Key + "actor", id);
                        Assert.That(flow.TryStartTask(EmployeeRole.Barista), Is.True);
                    }
                    catch (Exception error) { failure = error; }
                    if (failure == null)
                    {
                        for (var frame = 0; frame < 1800 && !flow.Snapshot.Single(value => value.Role == EmployeeRole.Barista).Delivered; frame++) yield return null;
                        try { Assert.That(flow.Snapshot.Single(value => value.Role == EmployeeRole.Barista).Delivered, Is.True); }
                        catch (Exception error) { failure = error; }
                    }
                    SessionState.SetInt(Key + "run", SessionState.GetInt(Key + "run", 0) + 1);
                    TestContext.WriteLine("REAL_PLAY_SESSION domainReload=True run=" + SessionState.GetInt(Key + "run", 0) + " actor=" + SessionState.GetString(Key + "actor", ""));
                    yield return new ExitPlayMode();
                    Assert.That(Resources.FindObjectsOfTypeAll<NavigationActor>().Any(value => value.ActorId == SessionState.GetString(Key + "actor", "")), Is.False);
                    if (failure != null) throw failure;
                }
            }
            finally
            {
                Time.captureDeltaTime = SessionState.GetFloat(Key + "capture", 0);
                EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)SessionState.GetInt(Key + "options", 0);
                EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool(Key + "enabled", false);
                var restored = JsonUtility.FromJson<SavedScenes>(SessionState.GetString(Key + "scenes", "{}"));
                if (restored?.scenes != null && restored.scenes.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(restored.scenes
                    .Select(value => new SceneSetup { path = value.path, isLoaded = value.loaded, isActive = value.active }).ToArray());
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Assert.That(EditorSettings.enterPlayModeOptions, Is.EqualTo((EnterPlayModeOptions)SessionState.GetInt(Key + "options", 0)));
                Assert.That(EditorSettings.enterPlayModeOptionsEnabled, Is.EqualTo(SessionState.GetBool(Key + "enabled", false)));
                var actual = EditorSceneManager.GetSceneManagerSetup();
                Assert.That(actual.Select(value => value.path + ":" + value.isLoaded + ":" + value.isActive),
                    Is.EqualTo(restored.scenes.Select(value => value.path + ":" + value.loaded + ":" + value.active)));
                TestContext.WriteLine("EDITOR_RESTORED trueReload settings=" + EditorSettings.enterPlayModeOptionsEnabled + "/" + EditorSettings.enterPlayModeOptions + " scenes=" + string.Join("|", actual.Select(value => value.path)));
                SessionState.EraseString(Key + "scenes"); SessionState.EraseString(Key + "actor");
            }
        }
        [UnityTest] public IEnumerator RepeatedPlayStartsOneFreshSessionWithoutDomainReload() => Sessions(false);
        private IEnumerator Sessions(bool domainReload)
        {
            var enabled = EditorSettings.enterPlayModeOptionsEnabled; var options = EditorSettings.enterPlayModeOptions;
            var setup = EditorSceneManager.GetSceneManagerSetup(); var capture = Time.captureDeltaTime;
            Exception failure = null; string previous = null;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = domainReload ? EnterPlayModeOptions.None : EnterPlayModeOptions.DisableDomainReload;
            try
            {
                EditorSceneManager.OpenScene(Phase13EmployeeTaskSceneSetup.ScenePath, OpenSceneMode.Single);
                for (var run = 0; run < 2; run++)
                {
                    yield return new EnterPlayMode(domainReload); Time.captureDeltaTime = 1f / 60;
                    EmployeeFlowController flow = null;
                    for (var frame = 0; frame < 600; frame++)
                    {
                        flow = UnityEngine.Object.FindFirstObjectByType<EmployeeFlowController>();
                        if (flow != null && flow.Snapshot.All(value => value.State == EmployeeState.Idle)) break;
                        yield return null;
                    }
                    try
                    {
                        Assert.That(flow, Is.Not.Null); Assert.That(flow.Snapshot.Count, Is.EqualTo(2));
                        Assert.That(flow.Snapshot.All(value => value.State == EmployeeState.Idle), Is.True);
                        Assert.That(UnityEngine.Object.FindObjectsByType<EmployeeFlowController>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
                        Assert.That(UnityEngine.Object.FindObjectsByType<EmployeeTaskValidationController>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
                        Assert.That(UnityEngine.Object.FindObjectsByType<NavigationActor>(FindObjectsSortMode.None).Length, Is.EqualTo(2));
                        Assert.That(UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Count(value => value.name == "P13 Task HUD"), Is.EqualTo(1));
                        Assert.That(UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Any(value => value.name.StartsWith("PickupCup_")), Is.False);
                        var id = flow.Snapshot[0].ActorId; Assert.That(id, Is.Not.EqualTo(previous)); previous = id;
                        Assert.Throws<InvalidOperationException>(() => Phase13EmployeeTaskSceneSetup.BuildValidationScene());
                        Assert.Throws<InvalidOperationException>(() => Phase13EmployeeTaskSceneSetup.BuildMainCafe());
                        Assert.That(flow.TryStartTask(EmployeeRole.Barista), Is.True);
                    }
                    catch (Exception error) { failure = error; }
                    if (failure == null)
                    {
                        for (var frame = 0; frame < 1800 && !flow.Snapshot.Single(value => value.Role == EmployeeRole.Barista).Delivered; frame++) yield return null;
                        try { Assert.That(flow.Snapshot.Single(value => value.Role == EmployeeRole.Barista).Delivered, Is.True); }
                        catch (Exception error) { failure = error; }
                    }
                    TestContext.WriteLine("REAL_PLAY_SESSION domainReload=" + domainReload + " run=" + (run + 1) + " actor=" + previous);
                    yield return new ExitPlayMode();
                    Assert.That(Resources.FindObjectsOfTypeAll<NavigationActor>().Any(value => value.ActorId == previous), Is.False, "Old actor released after actual ExitPlayMode");
                    if (failure != null) break;
                }
            }
            finally
            {
                Time.captureDeltaTime = capture; EditorSettings.enterPlayModeOptions = options; EditorSettings.enterPlayModeOptionsEnabled = enabled;
                if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Assert.That(EditorSettings.enterPlayModeOptions, Is.EqualTo(options)); Assert.That(EditorSettings.enterPlayModeOptionsEnabled, Is.EqualTo(enabled));
                Assert.That(EditorSceneManager.GetSceneManagerSetup().Select(value => value.path + ":" + value.isLoaded + ":" + value.isActive),
                    Is.EqualTo(setup.Select(value => value.path + ":" + value.isLoaded + ":" + value.isActive)));
                TestContext.WriteLine("EDITOR_RESTORED noReload settings=" + EditorSettings.enterPlayModeOptionsEnabled + "/" + EditorSettings.enterPlayModeOptions + " scenes=" + string.Join("|", EditorSceneManager.GetSceneManagerSetup().Select(value => value.path)));
            }
            if (failure != null) throw failure;
        }
    }
}
