using System;
using System.Reflection;
using System.Collections;
using System.Linq;
using AnimalCafe.Core.Time;
using AnimalCafe.UI.Foundation;
using AnimalCafe.Core.Events;
using AnimalCafe.Navigation;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.TestTools;
namespace AnimalCafe.Tests.EditMode.Phase11
{
    public sealed class NavigationResumeBlockTests
    {
        GameObject root; GameTimeService time;
        [SetUp] public void Setup() { root=new GameObject("time"); time=root.AddComponent<GameTimeService>(); typeof(GameTimeService).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(time,null); time.SetFast(); }
        [TearDown] public void Cleanup() { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Time.timeScale=1; }
        IDisposable Block(object owner=null)
        {
            var method=typeof(GameTimeService).GetMethod("AcquireResumeBlock");
            Assert.That(method,Is.Not.Null,"Task6 resume block API");
            return (IDisposable)method.Invoke(time,new[]{owner??new object(),"路径被挡住了，请进入装修调整。"});
        }
        UiPauseCoordinator Coordinator(out IUiPauseHandle handle)
        {
            var coordinator=new UiPauseCoordinator(time);
            handle=coordinator.Acquire(new UiView("Decoration",UiViewKind.MainPanel,UiPausePolicy.PauseGame,UiOutsideDismissPolicy.NotDismissible));
            return coordinator;
        }
        [Test] public void BlockedTrySetSpeedCannotResume()
        { using(Block()) { Assert.That(time.TrySetSpeed(GameSpeed.Fast),Is.False); Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Paused)); Assert.That(UnityEngine.Time.timeScale,Is.Zero); } }
        [Test] public void OneOwnerCannotReleaseAnother()
        { var first=Block(new object()); var second=Block(new object()); first.Dispose(); first.Dispose(); Assert.That(time.TrySetSpeed(GameSpeed.Normal),Is.False); second.Dispose(); Assert.That(time.TrySetSpeed(GameSpeed.Normal),Is.True); }
        [Test] public void SameOwnerHandlesAreIndependent()
        { var owner=new object(); var first=Block(owner); var second=Block(owner); first.Dispose(); first.Dispose(); Assert.That(time.IsResumeBlocked,Is.True); second.Dispose(); Assert.That(time.IsResumeBlocked,Is.False); }
        [Test] public void ExplicitPauseBeforeDecorationRemainsPaused()
        { time.SetPaused(); Coordinator(out var handle); handle.Dispose(); Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Paused)); }
        [Test] public void ReleaseDoesNotAutoRun()
        { var block=Block(); block.Dispose(); Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Paused)); }
        [Test] public void DecorationReleaseCannotRunOneFrame()
        { var coordinator=Coordinator(out var handle); var block=Block(); handle.Dispose(); Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Paused)); Assert.That(coordinator.TryRestorePendingSpeed(),Is.False); block.Dispose(); Assert.That(coordinator.TryRestorePendingSpeed(),Is.True); Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Fast)); }
        [Test] public void ExplicitPauseRetiresPendingFastRestore()
        { var coordinator=Coordinator(out var handle); var block=Block(); handle.Dispose(); time.SetPaused(); block.Dispose(); Assert.That(coordinator.TryRestorePendingSpeed(),Is.False); Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Paused)); }
        [Test] public void LaterExplicitSpeedRetiresPendingRestore()
        { var coordinator=Coordinator(out var handle); var block=Block(); handle.Dispose(); block.Dispose(); time.SetNormal(); Assert.That(coordinator.TryRestorePendingSpeed(),Is.False); Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Normal)); }
        [Test] public void LaterExplicitSpeedRetiresRestoreBeforeSpeedNotification()
        {
            var coordinator=Coordinator(out var handle); var block=Block(); handle.Dispose(); block.Dispose();
            bool? restored=null; Action<GameSpeedChangedEvent> listener=e=> { if(e.Current==GameSpeed.Normal) restored=coordinator.TryRestorePendingSpeed(); };
            GameEventBus.GameSpeedChanged+=listener;
            try { time.SetNormal(); Assert.That(restored,Is.False); Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Normal)); }
            finally { GameEventBus.GameSpeedChanged-=listener; }
        }
        [Test] public void ReentryPreservesOriginalFastAfterNavigationPause()
        { var coordinator=Coordinator(out var handle); var block=Block(); handle.Dispose(); var next=coordinator.Acquire(new UiView("Repair",UiViewKind.MainPanel,UiPausePolicy.PauseGame,UiOutsideDismissPolicy.NotDismissible)); block.Dispose(); next.Dispose(); Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Fast)); }
        [Test] public void FailedRunDoesNotChangeRememberedSpeed()
        { var block=Block(); time.SetNormal(); block.Dispose(); time.TogglePaused(); Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Fast)); }
        [Test] public void PauseNotificationCannotReenterFastBeforeBlockExists()
        {
            bool? accepted=null;
            Action<GameSpeedChangedEvent> listener=e=> { if(e.Current==GameSpeed.Paused) accepted=time.TrySetSpeed(GameSpeed.Fast); };
            GameEventBus.GameSpeedChanged+=listener;
            try { using(Block()) { Assert.That(accepted,Is.False); Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Paused)); } }
            finally { GameEventBus.GameSpeedChanged-=listener; }
        }
        [Test] public void LastReleaseOnlyNotifiesAvailabilityAndRetainsPause()
        {
            var block=Block(); var calls=0;
            time.ResumeAvailabilityChanged+=()=> { calls++; Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Paused)); };
            block.Dispose(); block.Dispose(); Assert.That(calls,Is.EqualTo(1));
        }
        [Test] public void DisabledBridgeLeavesSurvivingWorldBlockedUntilValidated()
        {
            var world=root.AddComponent<NavigationWorld>(); var adapter=root.AddComponent<NavigationLayoutAdapter>();
            adapter.Configure(null,null,null,null,null,world,true);
            var bridge=root.AddComponent<NavigationDecorationBridge>(); bridge.Configure(adapter); bridge.ConfigureTime(time);
            typeof(NavigationDecorationBridge).GetMethod("OnDisable",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(bridge,null);
            Assert.That(world.LayoutAvailable,Is.False); Assert.That(time.IsResumeBlocked,Is.True);
            UnityEngine.Object.DestroyImmediate(bridge);
            Assert.That(time.TrySetSpeed(GameSpeed.Fast),Is.False);
            world.ReleaseValidatedResumeBlock(); Assert.That(time.IsResumeBlocked,Is.True);
            world.SetLayoutAvailable(true); world.ReleaseValidatedResumeBlock();
            Assert.That(time.IsResumeBlocked,Is.False); Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Paused));
        }
    }
    public sealed class NavigationResumeLifecycleTests
    {
        [UnityTest] public IEnumerator TwoPlaySessionsWithoutDomainReloadReleaseAllSceneOwners()
        {
            var enabled=EditorSettings.enterPlayModeOptionsEnabled; var options=EditorSettings.enterPlayModeOptions;
            var setup=EditorSceneManager.GetSceneManagerSetup();
            Exception failure=null;
            EditorSettings.enterPlayModeOptionsEnabled=true;
            EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
            try
            {
                EditorSceneManager.OpenScene("Assets/Scenes/Validation/Phase11Navigation.unity",OpenSceneMode.Single);
                for(var run=0;run<2;run++)
                {
                    yield return new EnterPlayMode(false); yield return null; yield return null; yield return null;
                    try
                    {
                        var time=UnityEngine.Object.FindFirstObjectByType<GameTimeService>();
                        var bridge=UnityEngine.Object.FindFirstObjectByType<NavigationDecorationBridge>();
                        Assert.That(time.IsResumeBlocked,Is.False,"New Play must not inherit old owners");
                        Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Normal));
                        Assert.That(bridge.Adapter.CurrentReadiness.CanResume,Is.True);
                        bridge.enabled=false; Assert.That(time.IsResumeBlocked,Is.True);
                        Debug.Log("P11_NO_DOMAIN_RELOAD_PLAY_PASS "+(run+1));
                    }
                    catch(Exception error) { failure=error; }
                    yield return new ExitPlayMode();
                    Assert.That(Resources.FindObjectsOfTypeAll<TMPro.TMP_FontAsset>().Any(f=>f.name=="Navigation resume reason runtime font"),Is.False);
                    Assert.That(Resources.FindObjectsOfTypeAll<Material>().Any(m=>m.name=="Navigation resume reason runtime material"),Is.False);
                    Assert.That(Resources.FindObjectsOfTypeAll<Texture2D>().Any(t=>t.name=="Navigation resume reason runtime atlas"),Is.False);
                    if(failure!=null) break;
                }
            }
            finally
            {
                EditorSettings.enterPlayModeOptions=options; EditorSettings.enterPlayModeOptionsEnabled=enabled;
                if(setup.Length>0) EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            }
            if(failure!=null) throw failure;
        }
    }
}
