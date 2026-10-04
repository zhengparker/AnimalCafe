#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AnimalCafe.Content;
using AnimalCafe.Layout;
using AnimalCafe.Navigation;
using AnimalCafe.Decoration;
using AnimalCafe.Core.Time;
using AnimalCafe.Core.Events;
using AnimalCafe.UI.Decoration;
using UnityEngine.EventSystems;
using System;
using Object=UnityEngine.Object;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AnimalCafe.Tests.PlayMode.Phase11Integration
{
    public sealed class Phase11NavigationSceneTests
    {
        private NavigationValidationController controller;
        private float oldCapture;
        private UnityEngine.InputSystem.InputActionAsset[] ownedInputAssets=Array.Empty<UnityEngine.InputSystem.InputActionAsset>();
        private static T Field<T>(object owner,string name) => (T)owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(owner);
        private static void SetField(object owner,string name,object value) => owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(owner,value);
        [UnitySetUp] public IEnumerator Open()
        {
            oldCapture=Time.captureDeltaTime; Time.captureDeltaTime=1f/60;
            var scene=EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/Validation/Phase11Navigation.unity",new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; yield return null;
            ownedInputAssets=EditorSceneLoading.Phase8SceneInputTestCleanup.CaptureAssets(scene);
            Assert.That(ownedInputAssets,Is.Not.Empty,"Capture loaded UI actions for fixture cleanup");
            controller=Object.FindFirstObjectByType<NavigationValidationController>();
            Assert.That(controller,Is.Not.Null); Assert.That(controller.Ready,Is.True,controller.StatusText);
            Assert.That(controller.Actors.Count,Is.EqualTo(8));
        }
        [UnityTest] public IEnumerator CaptureRepresentativeCanvasAndCamera()
        {
            if (Environment.GetEnvironmentVariable("ANIMALCAFE_P11_CAPTURE") != "1")
                Assert.Ignore("Opt-in representative ScreenCapture; set ANIMALCAFE_P11_CAPTURE=1 in a graphics Editor.");
            var time = Object.FindFirstObjectByType<GameTimeService>();
            controller.RunScenario("straight");
            yield return new WaitForSecondsRealtime(.7f);
            yield return CaptureScreen("final-fix-normal-screen-v2.png");
            controller.CancelAll();
            var decor = Object.FindFirstObjectByType<DecorationModeController>();
            var bridge = Object.FindFirstObjectByType<NavigationDecorationBridge>();
            // Real solid furniture Confirm blocks the Coffee employee station.
            decor.EnterDecorationMode();
            var runtime=Object.FindFirstObjectByType<CafeLayoutRuntime>();
            var coffee=runtime.CurrentReadiness.Stations.Single(s=>s.FunctionType==LayoutStationType.CoffeeMachine);
            Assert.That(coffee.Anchors.TryGetAnchor(InteractionRole.Employee,out var anchor),Is.True);
            Object.FindFirstObjectByType<DecorationCatalogueView>().GetComponentsInChildren<DecorationCatalogueTileView>(true)
                .Single(t=>t.ItemId=="furniture.counter.module.01"&&t.gameObject.activeInHierarchy).GetComponent<Button>().onClick.Invoke();
            ConfirmFurnitureAt(decor,Field<DecorationSession>(decor,"session"),anchor.Position);
            decor.ExitDecorationMode();
            Assert.That(time.CurrentSpeed, Is.EqualTo(GameSpeed.Paused));
            Assert.That(time.IsResumeBlocked, Is.True);
            Assert.That(time.TrySetSpeed(GameSpeed.Fast), Is.False);
            yield return CaptureScreen("final-fix-blocked-screen-v2.png");
        }

        private static IEnumerator CaptureScreen(string filename)
        {
            var camera = UnityEngine.Camera.main;
            Assert.That(camera, Is.Not.Null);
            Assert.That(camera.targetTexture, Is.Null);
            Assert.That(Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                .Any(c => c.isActiveAndEnabled && c.renderMode == RenderMode.ScreenSpaceOverlay), Is.True);
            Canvas.ForceUpdateCanvases();
            var deadline = Time.realtimeSinceStartup + 45;
            while (UnityEditor.ShaderUtil.anythingCompiling && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(UnityEditor.ShaderUtil.anythingCompiling, Is.False);
            yield return null; yield return null;
            var path = Path.GetFullPath("outputs/phase11/" + filename);
            Assert.That(File.Exists(path), Is.False, "Keep earlier capture evidence");
            ScreenCapture.CaptureScreenshot(path, 1);
            deadline = Time.realtimeSinceStartup + 8;
            while (!File.Exists(path) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(File.Exists(path), Is.True, "Actual Canvas+camera capture unavailable; no camera-only substitution");
            TestContext.WriteLine("P11_SCREEN_CAPTURE " + filename + " " + Screen.width + "x" + Screen.height + " batch=" + Application.isBatchMode);
        }

        [UnityTest] public IEnumerator NewScenarioResetsSubmittedCountWithResults()
        {
            controller.RunScenario("straight");
            Assert.That(controller.SubmittedCount, Is.GreaterThan(0));
            controller.CancelAll();
            Assert.That(controller.Results, Is.Not.Empty);
            controller.RunScenario("straight");
            Assert.That(controller.SubmittedCount, Is.EqualTo(1), "Only the new staging request is submitted");
            Assert.That(controller.Results, Is.Empty);
            controller.CancelAll();
            yield return null;
        }

        [UnityTest] public IEnumerator I001_ActualBusinessSceneHasSingleOwnersAndReadyCombination()
        {
            var bridge=Object.FindFirstObjectByType<NavigationDecorationBridge>();
            Assert.That(bridge,Is.Not.Null); Assert.That(bridge.EnforceBusinessReadiness,Is.True);
            Assert.That(Object.FindFirstObjectByType<CafeLayoutRuntime>().CurrentReadiness.CanOpenForBusiness,Is.True);
            Assert.That(bridge.Adapter.CurrentReadiness.CanResume,Is.True,bridge.Adapter.CurrentReadiness.Reason);
            Assert.That(Object.FindObjectsByType<GameTimeService>(FindObjectsSortMode.None).Length,Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<DecorationModeController>(FindObjectsSortMode.None).Length,Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length,Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<UnityEngine.Camera>(FindObjectsSortMode.None).Length,Is.EqualTo(1));
            Assert.That(Field<UnityEngine.AI.NavMeshDataInstance>(controller,"ownedDataInstance").valid,Is.False);
            Assert.That(controller.World.RegisteredActors.Count,Is.EqualTo(8));
            yield return null;
        }
        [UnityTest] public IEnumerator RootDriftBlocksExitAndRepairRestoresOriginalFastOrExplicitPause()
        {
            var decor=Object.FindFirstObjectByType<DecorationModeController>();
            var bridge=Object.FindFirstObjectByType<NavigationDecorationBridge>();
            var time=Object.FindFirstObjectByType<GameTimeService>();
            var representation=Field<Transform>(decor,"furnitureRepresentationRoot"); var original=representation.position;
            time.SetFast(); decor.EnterDecorationMode();
            representation.position+=Vector3.right*.1f; decor.ExitDecorationMode();
            Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Paused)); Assert.That(time.IsResumeBlocked,Is.True);
            Assert.That(time.TrySetSpeed(GameSpeed.Fast),Is.False);
            yield return null; // Validation LateUpdate puts the single visible reason below the P8 bar.
            var reasonLabels=Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None).Where(t=>t.name=="NavigationResumeBlockReason").ToArray();
            Assert.That(reasonLabels.Length,Is.EqualTo(1),"The P8 reason stays visible; duplicate fixture diagnostics are hidden");
            foreach(var label in reasonLabels)
            {
                Assert.That(label.isActiveAndEnabled,Is.True);
                Assert.That(label.text,Is.EqualTo(NavigationDecorationBridge.BlockedMessage));
                Assert.That(label.font.HasCharacters(label.text),Is.True,"Every Chinese reason glyph must be available");
            }
            yield return null; Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Paused));
            representation.position=original; decor.EnterDecorationMode(); decor.ExitDecorationMode();
            Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Fast));
            decor.EnterDecorationMode(); representation.position+=Vector3.right*.1f; decor.ExitDecorationMode();
            time.SetPaused(); representation.position=original;
            Assert.That(bridge.ResumeAfterRepair(),Is.True);
            yield return null; Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Paused));
            time.SetNormal(); decor.EnterDecorationMode(); decor.ExitDecorationMode();
            Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Normal));
        }
        [UnityTest] public IEnumerator I005_I006_I007_RealFurnitureConfirmBlocksCoffeeStationThenRepairRestoresFastAndFreshMove()
        {
            var decor=Object.FindFirstObjectByType<DecorationModeController>();
            var bridge=Object.FindFirstObjectByType<NavigationDecorationBridge>();
            var time=Object.FindFirstObjectByType<GameTimeService>();
            var runtime=Object.FindFirstObjectByType<CafeLayoutRuntime>();
            var coffee=runtime.CurrentReadiness.Stations.Single(s=>s.FunctionType==LayoutStationType.CoffeeMachine);
            Assert.That(coffee.Anchors.TryGetAnchor(InteractionRole.Employee,out var anchor),Is.True);
            var grid=Field<Transform>(decor,"gridRoot");
            var target=grid.TransformPoint(new Vector3(anchor.Position.X+.5f,0,anchor.Position.Y+.5f));
            var actor=controller.Actors[7]; var original=actor.transform.position;
            time.SetFast(); decor.EnterDecorationMode();
            var previousIds=runtime.Layout.FurnitureInstances.Select(f=>f.InstanceId).ToArray();
            var tile=Object.FindFirstObjectByType<DecorationCatalogueView>().GetComponentsInChildren<DecorationCatalogueTileView>(true)
                .Single(t=>t.ItemId=="furniture.counter.module.01" && t.gameObject.activeInHierarchy);
            tile.GetComponent<Button>().onClick.Invoke();
            var session=Field<DecorationSession>(decor,"session");
            ConfirmFurnitureAt(decor,session,anchor.Position);
            var blocker=runtime.Layout.FurnitureInstances.Single(f=>!previousIds.Contains(f.InstanceId));
            Assert.That(blocker.Position,Is.EqualTo(anchor.Position));
            Assert.That(runtime.FunctionalSurfaceLayout.MountedInstances.Any(m=>m.InstanceId==coffee.InstanceId),Is.True,"Device stays installed");
            TestContext.WriteLine("REAL_FURNITURE_BLOCK furniture.counter.module.01 cell="+anchor.Position.X+","+anchor.Position.Y+" coffee="+coffee.InstanceId);
            var runningEvents=0;
            Action<GameSpeedChangedEvent> listener=e=> { if(e.Current!=GameSpeed.Paused) runningEvents++; };
            GameEventBus.GameSpeedChanged+=listener;
            try
            {
                decor.ExitDecorationMode();
                Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Paused)); Assert.That(Time.timeScale,Is.Zero);
                Assert.That(bridge.Adapter.CurrentReadiness.CanResume,Is.False);
                Assert.That(time.TrySetSpeed(GameSpeed.Normal),Is.False); Assert.That(time.TrySetSpeed(GameSpeed.Fast),Is.False);
                Assert.That(runningEvents,Is.Zero,"No transient running frame or speed event on blocked exit");
                Assert.That(actor.transform.position,Is.EqualTo(original));
                yield return null;
                Assert.That(actor.transform.position,Is.EqualTo(original)); Assert.That(Time.timeScale,Is.Zero);
                var readiness=Field<AnimalCafe.UI.Feedback.ValidationMessageView>(decor,"validationMessageView");
                Assert.That(readiness.IsVisible,Is.True,"Real blocked layout shows the original P8 readiness banner");
                var visibleReason=Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None).Single(t=>t.name=="NavigationResumeBlockReason"&&t.isActiveAndEnabled);
                Assert.That(ScreenRect(visibleReason.rectTransform).Overlaps(ScreenRect((RectTransform)readiness.transform)),Is.False,"Real P8 readiness banner must not cover the Chinese navigation reason");
                Assert.That(ScreenRect(visibleReason.rectTransform).Overlaps(UnityEngine.Camera.main.pixelRect),Is.False);
                decor.EnterDecorationMode();
                InvokePrivate(decor,"HandleFurnitureBegan",blocker.InstanceId);
                ConfirmFurnitureAt(decor,session,new GridPosition(0,6));
                decor.ExitDecorationMode();
                Assert.That(bridge.Adapter.CurrentReadiness.CanResume,Is.True,bridge.Adapter.CurrentReadiness.Reason);
                Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Fast));
                Assert.That(actor.transform.position,Is.EqualTo(original),"Repair must not relocate the actor");
                MovementResult? fresh=null;
                Assert.That(controller.World.Service.MoveTo(actor.ActorId,new NavigationTarget(target),r=>fresh=r).Accepted,Is.True);
                for(var i=0;i<2400&&!fresh.HasValue;i++) yield return null;
                Assert.That(fresh.HasValue,Is.True); Assert.That(fresh.Value.Status,Is.EqualTo(MovementStatus.Arrived),fresh.Value.Reason+" start="+original+" final="+actor.transform.position+" target="+target+" corners="+string.Join(";",actor.Agent.path.corners.Select(p=>p.ToString())));
                Assert.That(Vector3.Distance(actor.transform.position,target),Is.LessThanOrEqualTo(.08f));
                TestContext.WriteLine("REAL_FURNITURE_REPAIR moved blocker to 0,6; fresh actual-start MoveTo arrived at "+target);
            }
            finally { GameEventBus.GameSpeedChanged-=listener; }
        }
        private static void ConfirmFurnitureAt(DecorationModeController decor,DecorationSession session,GridPosition cell)
        {
            Assert.That(session.ActivePreview,Is.Not.Null);
            var result=session.MovePreview(cell); Assert.That(result.Succeeded,Is.True,"Furniture placement must be legal");
            InvokePrivate(decor,"SyncActivePreviewPresentation"); InvokePrivate(decor,"ShowActionForResult",result);
            var button=Object.FindFirstObjectByType<DecorationActionBarView>().GetComponentsInChildren<Button>(true).Single(b=>b.name=="ConfirmButton");
            Assert.That(button.isActiveAndEnabled&&button.interactable,Is.True); button.onClick.Invoke();
            Assert.That(session.ActivePreview,Is.Null,"Actual controller Confirm must commit");
        }
        [UnityTest] public IEnumerator Teardown_DisablingDecorationWithPendingCounterDoesNotRebuildOrResume()
        {
            var decor=Object.FindFirstObjectByType<DecorationModeController>();
            var bridge=Object.FindFirstObjectByType<NavigationDecorationBridge>();
            var adapter=bridge.Adapter; var time=Object.FindFirstObjectByType<GameTimeService>();
            var session=Field<DecorationSession>(decor,"session");
            var runtime=Object.FindFirstObjectByType<CafeLayoutRuntime>();
            time.SetFast(); decor.EnterDecorationMode();
            var counterTile=Object.FindFirstObjectByType<DecorationCatalogueView>()
                .GetComponentsInChildren<DecorationCatalogueTileView>(true)
                .Single(t=>t.ItemId=="furniture.counter.module.01"&&t.gameObject.activeInHierarchy);
            var previousIds=runtime.Layout.FurnitureInstances.Select(f=>f.InstanceId).ToArray();
            counterTile.GetComponent<Button>().onClick.Invoke();
            ConfirmFurnitureAt(decor,session,new GridPosition(0,6));
            Assert.That(adapter.PublishedRevision,Is.LessThan(adapter.Revision),"Confirmed layout must await rebuild");
            var confirmed=runtime.Layout.FurnitureInstances.Single(f=>!previousIds.Contains(f.InstanceId));
            InvokePrivate(decor,"HandleFurnitureBegan",confirmed.InstanceId);
            Assert.That(session.ActivePreview,Is.Not.Null,"Leave a second preview open at teardown");
            var bakeCount=adapter.BakeCount;
            var scene=decor.gameObject.scene;
            var existingSolids=Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(go=>go.name=="Navigation confirmed solids"&&go.scene.IsValid()&&go.scene.path==scene.path)
                .ToArray();

            // Disabling the owner must clean up; it must not bake or spawn scene geometry.
            decor.enabled=false;
            Assert.That(adapter.BakeCount,Is.EqualTo(bakeCount),"Teardown must not rebuild pending confirmed furniture");
            var newSolids=Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(go=>go.name=="Navigation confirmed solids"&&go.scene.IsValid()&&go.scene.path==scene.path&&!existingSolids.Contains(go))
                .ToArray();
            Assert.That(newSolids,Is.Empty,"Teardown must not create a new confirmed-solids root");
            Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Paused));
            Assert.That(time.TrySetSpeed(GameSpeed.Fast),Is.False,"A live business bridge must keep the world blocked");
            yield return null;
            Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Paused));
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest] public IEnumerator Teardown_DestroyedBridgeBeforeDecorationDisableDoesNotLogMissingReference()
        {
            var decor=Object.FindFirstObjectByType<DecorationModeController>();
            var bridge=Object.FindFirstObjectByType<NavigationDecorationBridge>();
            var time=Object.FindFirstObjectByType<GameTimeService>();
            time.SetFast(); decor.EnterDecorationMode();
            Object.Destroy(bridge); yield return null;
            Assert.That(bridge==null,Is.True,"The stored Decoration reference now points to a destroyed Unity Object");
            decor.enabled=false;
            Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Paused));
            Assert.That(time.TrySetSpeed(GameSpeed.Fast),Is.False);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }
        private static void InvokePrivate(object target,string name,params object[] args)
            => target.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(target,args);

        [UnityTest] public IEnumerator ValidationHudReservesCameraAndLeavesP8ControlsRaycastable()
        {
            Canvas.ForceUpdateCanvases(); yield return null;
            var status=Field<TMPro.TMP_Text>(controller,"statusLabel");
            var dock=(RectTransform)status.transform.parent;
            var camera=UnityEngine.Camera.main;
            Assert.That(ScreenRect(dock).Overlaps(camera.pixelRect),Is.False,"Fixture dock must not cover the camera viewport");
            foreach(var name in new[]{"straight","detour","crossing","narrow-wait","same-target","recovery","crowd8","blocked85","Pause","1x","2x","Cancel requests","DecorationModeButton"})
            {
                var button=Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b=>b.name==name);
                AssertRaycastable(button);
            }
            controller.RunScenario("crowd8"); yield return null; Canvas.ForceUpdateCanvases(); status.ForceMeshUpdate();
            Assert.That(controller.SubmittedCount,Is.EqualTo(8)); Assert.That(status.isTextOverflowing,Is.False,"All crowd rows must fit");
            var cameraControl=Field<AnimalCafe.Camera.CafeCameraController>(controller,"cameraController");
            var pose=camera.transform.position; var size=camera.orthographicSize;
            cameraControl.ApplyPan(new Vector2(10,0)); cameraControl.ApplyZoom(1);
            Assert.That(camera.transform.position,Is.Not.EqualTo(pose)); Assert.That(camera.orthographicSize,Is.Not.EqualTo(size));
            controller.CancelAll();
            var decor=Object.FindFirstObjectByType<DecorationModeController>();
            var bridge=Object.FindFirstObjectByType<NavigationDecorationBridge>();
            decor.EnterDecorationMode(); yield return null;
            Assert.That(dock.gameObject.activeInHierarchy,Is.False,"Hide diagnostics during actual furniture editing");
            AssertRaycastable(Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b=>b.name=="DecorationModeButton"));
            bridge.Adapter.enabled=false; decor.ExitDecorationMode(); yield return null; Canvas.ForceUpdateCanvases();
            var reason=Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None).Single(t=>t.name=="NavigationResumeBlockReason"&&t.isActiveAndEnabled);
            Assert.That(reason.text,Is.EqualTo(NavigationDecorationBridge.BlockedMessage)); reason.ForceMeshUpdate();
            Assert.That(reason.isTextOverflowing,Is.False); Assert.That(reason.font.HasCharacters(reason.text),Is.True);
            Assert.That(ScreenRect(reason.rectTransform).Overlaps(ScreenRect(dock)),Is.False);
            Assert.That(ScreenRect(reason.rectTransform).Overlaps(camera.pixelRect),Is.False);
            Assert.That(ScreenRect(status.rectTransform).yMin,Is.GreaterThanOrEqualTo(0));
        }
        private static Rect ScreenRect(RectTransform transform)
        {
            var corners=new Vector3[4]; transform.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x,corners[0].y,corners[2].x,corners[2].y);
        }
        private static void AssertRaycastable(Button button)
        {
            var rect=ScreenRect((RectTransform)button.transform);
            Assert.That(rect.xMin>=0&&rect.yMin>=0&&rect.xMax<=Screen.width+.1f&&rect.yMax<=Screen.height+.1f,Is.True,button.name+" must be on screen");
            var hits=new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=rect.center},hits);
            Assert.That(hits.Count,Is.GreaterThan(0),button.name);
            Assert.That(hits[0].gameObject.transform.IsChildOf(button.transform),Is.True,button.name+" is occluded by "+hits[0].gameObject.name);
        }
        [UnityTest] public IEnumerator I010_I013_DisableWorldGateAndOtherOwnerCannotBeReleased()
        {
            var bridge=Object.FindFirstObjectByType<NavigationDecorationBridge>();
            var time=Object.FindFirstObjectByType<GameTimeService>();
            var poses=controller.Actors.Select(a=>a.transform.position).ToArray();
            bridge.enabled=false;
            Assert.That(controller.World.LayoutAvailable,Is.False); Assert.That(time.IsResumeBlocked,Is.True);
            Assert.That(time.TrySetSpeed(GameSpeed.Fast),Is.False);
            yield return null;
            for(var i=0;i<poses.Length;i++) Assert.That(controller.Actors[i].transform.position,Is.EqualTo(poses[i]));
            bridge.enabled=true;
            using(time.AcquireResumeBlock(new object(),"another owner"))
            { Assert.That(bridge.ResumeAfterRepair(),Is.False); Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Paused)); }
            Assert.That(bridge.ResumeAfterRepair(),Is.True);
            Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Paused));
        }
        [UnityTest] public IEnumerator P022_MainCafePassiveAndZeroActorsKeepsLegacyFast()
        {
            EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/MainCafe.unity",new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; yield return null;
            var bridge=Object.FindFirstObjectByType<NavigationDecorationBridge>();
            Assert.That(bridge,Is.Not.Null); Assert.That(bridge.EnforceBusinessReadiness,Is.False);
            Assert.That(bridge.Adapter.EnforceBusinessReadiness,Is.False); Assert.That(bridge.Adapter.World.RegisteredActors.Count,Is.Zero);
            var time=Object.FindFirstObjectByType<GameTimeService>(); var decor=Object.FindFirstObjectByType<DecorationModeController>();
            time.SetFast(); decor.EnterDecorationMode(); decor.ExitDecorationMode();
            Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Fast)); Assert.That(time.IsResumeBlocked,Is.False);
        }
        [UnityTest] public IEnumerator I013_InvalidStartupRetainsSurvivingWorldGate()
        {
            UnityEngine.Events.UnityAction<Scene,LoadSceneMode> mutate=(scene,mode)=>
            {
                if(scene.path!="Assets/Scenes/Validation/Phase11Navigation.unity") return;
                var decor=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DecorationModeController>(true)).Single();
                Field<Transform>(decor,"furnitureRepresentationRoot").position+=Vector3.right*.1f;
            };
            SceneManager.sceneLoaded+=mutate;
            EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/Validation/Phase11Navigation.unity",new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; yield return null; yield return null;
            SceneManager.sceneLoaded-=mutate;
            var fixture=Object.FindFirstObjectByType<NavigationValidationController>();
            var bridge=Object.FindFirstObjectByType<NavigationDecorationBridge>();
            var time=Object.FindFirstObjectByType<GameTimeService>();
            Assert.That(fixture.Ready,Is.False); Assert.That(fixture.World.LayoutAvailable,Is.False);
            Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Paused)); Assert.That(time.IsResumeBlocked,Is.True);
            Object.Destroy(bridge); yield return null;
            Assert.That(time.TrySetSpeed(GameSpeed.Fast),Is.False,"World retains block after bridge and startup handles are released");
        }
        [UnityTearDown] public IEnumerator Close()
        {
            Time.timeScale=1; Time.captureDeltaTime=oldCapture;
            // Release this scene's cached controls before later input fixtures reset / 先释放场景输入缓存。
            var scene=SceneManager.GetActiveScene();
            if(scene.IsValid() && scene.isLoaded &&
                (scene.path=="Assets/Scenes/Validation/Phase11Navigation.unity" || scene.path=="Assets/Scenes/MainCafe.unity"))
            {
                var empty=SceneManager.CreateScene("Phase11Cleanup"); SceneManager.SetActiveScene(empty);
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            EditorSceneLoading.Phase8SceneInputTestCleanup.DisposeReleasedAssets(ownedInputAssets);
            ownedInputAssets=Array.Empty<UnityEngine.InputSystem.InputActionAsset>();
        }
        [UnityTest] public IEnumerator Fix1_DisabledAdapterCannotResumeEvenBeforeUpdate()
        {
            var decor=Object.FindFirstObjectByType<DecorationModeController>(); var bridge=Object.FindFirstObjectByType<NavigationDecorationBridge>();
            var time=Object.FindFirstObjectByType<GameTimeService>(); time.SetFast(); decor.EnterDecorationMode();
            var runningEvents=0; Action<GameSpeedChangedEvent> listener=e=> { if(e.Current!=GameSpeed.Paused) runningEvents++; };
            GameEventBus.GameSpeedChanged+=listener;
            try
            {
                bridge.Adapter.enabled=false; decor.ExitDecorationMode();
                Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Paused)); Assert.That(Time.timeScale,Is.Zero);
                Assert.That(bridge.ResumeAfterRepair(),Is.False); Assert.That(runningEvents,Is.Zero);
                Assert.That(controller.World.LayoutAvailable,Is.False);
                bridge.Adapter.enabled=true; Assert.That(bridge.ResumeAfterRepair(),Is.True);
                Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Fast));
            }
            finally { GameEventBus.GameSpeedChanged-=listener; }
            yield return null;
        }
        [UnityTest] public IEnumerator DisabledWorldRejectsImmediateExitAndRepairUntilReenabled()
        {
            var bridge=Object.FindFirstObjectByType<NavigationDecorationBridge>();
            var time=Object.FindFirstObjectByType<GameTimeService>();
            var world=controller.World; var actor=controller.Actors[7];
            var runtime=Object.FindFirstObjectByType<CafeLayoutRuntime>();
            var coffee=runtime.CurrentReadiness.Stations.Single(s=>s.FunctionType==LayoutStationType.CoffeeMachine);
            Assert.That(coffee.Anchors.TryGetAnchor(InteractionRole.Employee,out var anchor),Is.True);
            var grid=Field<Transform>(Object.FindFirstObjectByType<DecorationModeController>(),"gridRoot");
            var target=grid.TransformPoint(new Vector3(anchor.Position.X+.5f,0,anchor.Position.Y+.5f));
            var start=actor.transform.position; var callbacks=0; MovementResult oldResult=default;
            time.SetFast();
            Assert.That(world.Service.MoveTo(actor.ActorId,new NavigationTarget(target),r=>{callbacks++; oldResult=r;}).Accepted,Is.True);
            world.enabled=false;
            Assert.That(bridge.PrepareExit(),Is.False,"A disabled coordinator cannot validate an exit");
            Assert.That(bridge.ResumeAfterRepair(),Is.False,"Repair cannot release a disabled coordinator's gate");
            Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Paused));
            Assert.That(time.IsResumeBlocked,Is.True);
            Assert.That(time.TrySetSpeed(GameSpeed.Fast),Is.False);
            Assert.That(world.LayoutAvailable,Is.False);
            Assert.That(callbacks,Is.EqualTo(1));
            Assert.That(oldResult.Status,Is.EqualTo(MovementStatus.LayoutChanged));
            Assert.That(actor.transform.position,Is.EqualTo(start));
            yield return null;
            Assert.That(callbacks,Is.EqualTo(1));
            Assert.That(actor.transform.position,Is.EqualTo(start));
            world.enabled=true;
            Assert.That(bridge.ResumeAfterRepair(),Is.True);
            Assert.That(time.IsResumeBlocked,Is.False);
            Assert.That(actor.transform.position,Is.EqualTo(start),"Repair must keep the actual start");
            time.SetFast();
            MovementResult? fresh=null;
            Assert.That(world.Service.MoveTo(actor.ActorId,new NavigationTarget(target),r=>fresh=r).Accepted,Is.True);
            for(var i=0;i<2400&&!fresh.HasValue;i++) yield return null;
            Assert.That(fresh.HasValue,Is.True,"A fresh request must terminate after repair");
            Assert.That(fresh.Value.Status,Is.EqualTo(MovementStatus.Arrived),fresh.Value.Reason.ToString());
            Assert.That(Vector3.Distance(actor.transform.position,target),Is.LessThanOrEqualTo(.08f));
            Assert.That(callbacks,Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator DisabledWorldUpdateClosesGateAndTerminatesRequestOnce()
        {
            var bridge=Object.FindFirstObjectByType<NavigationDecorationBridge>();
            var time=Object.FindFirstObjectByType<GameTimeService>();
            var world=controller.World; var actor=controller.Actors[7];
            var start=actor.transform.position; var callbacks=0; MovementResult oldResult=default;
            var runtime=Object.FindFirstObjectByType<CafeLayoutRuntime>();
            var coffee=runtime.CurrentReadiness.Stations.Single(s=>s.FunctionType==LayoutStationType.CoffeeMachine);
            Assert.That(coffee.Anchors.TryGetAnchor(InteractionRole.Employee,out var anchor),Is.True);
            var grid=Field<Transform>(Object.FindFirstObjectByType<DecorationModeController>(),"gridRoot");
            var target=grid.TransformPoint(new Vector3(anchor.Position.X+.5f,0,anchor.Position.Y+.5f));
            time.SetFast();
            Assert.That(world.Service.MoveTo(actor.ActorId,new NavigationTarget(target),
                r=>{callbacks++; oldResult=r;}).Accepted,Is.True);
            Assert.That(callbacks,Is.Zero,"The request must still be active when the world is disabled");
            world.enabled=false;
            yield return null; // Bridge.Update must fail closed without an explicit exit call.
            Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Paused));
            Assert.That(time.IsResumeBlocked,Is.True);
            Assert.That(world.LayoutAvailable,Is.False);
            Assert.That(callbacks,Is.EqualTo(1));
            Assert.That(oldResult.Status,Is.EqualTo(MovementStatus.LayoutChanged));
            Assert.That(actor.transform.position,Is.EqualTo(start));
            Assert.That(bridge.ResumeAfterRepair(),Is.False);
            yield return null;
            Assert.That(callbacks,Is.EqualTo(1));
            Assert.That(actor.transform.position,Is.EqualTo(start));
        }
        [UnityTest] public IEnumerator Fix1_LostFloorStopsRunningWorld()
        {
            var bridge=Object.FindFirstObjectByType<NavigationDecorationBridge>(); var time=Object.FindFirstObjectByType<GameTimeService>();
            time.SetFast(); Object.Destroy(controller.Floor); yield return null; yield return null;
            Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Paused)); Assert.That(Time.timeScale,Is.Zero);
            Assert.That(controller.World.LayoutAvailable,Is.False); Assert.That(controller.Ready,Is.False);
            Assert.That(bridge.ResumeAfterRepair(),Is.False);
        }
        [UnityTest] public IEnumerator Fix1_LostTimeReferenceUsesLastValidOwnerUntilRepair()
        {
            var bridge=Object.FindFirstObjectByType<NavigationDecorationBridge>(); var time=Object.FindFirstObjectByType<GameTimeService>();
            time.SetFast(); SetField(bridge,"gameTimeService",null); yield return null;
            Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Paused)); Assert.That(Time.timeScale,Is.Zero);
            Assert.That(controller.World.LayoutAvailable,Is.False); Assert.That(bridge.ResumeAfterRepair(),Is.False);
            bridge.ConfigureTime(time); Assert.That(bridge.ResumeAfterRepair(),Is.True);
            Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Paused)); Assert.That(time.IsResumeBlocked,Is.False);
        }
        [UnityTest] public IEnumerator Fix1_DestroyedTimeOwnerCannotKeepWorldReady()
        {
            var bridge=Object.FindFirstObjectByType<NavigationDecorationBridge>();
            Object.Destroy(Object.FindFirstObjectByType<GameTimeService>()); yield return null; yield return null;
            Assert.That(Object.FindObjectsByType<GameTimeService>(FindObjectsSortMode.None),Is.Empty);
            Assert.That(controller.World.LayoutAvailable,Is.False); Assert.That(controller.Ready,Is.False);
            Assert.That(bridge.ResumeAfterRepair(),Is.False);
            var positions=controller.Actors.Select(a=>a.transform.position).ToArray(); controller.World.Step(1);
            Assert.That(controller.Actors.Select(a=>a.transform.position),Is.EqualTo(positions));
        }
        [UnityTest] public IEnumerator Fix1_InvalidStartupRadiusCannotBeOmitted() => InvalidStartupActor(false);
        [UnityTest] public IEnumerator Fix1_InvalidStartupPositionCannotBeOmitted() => InvalidStartupActor(true);
        [UnityTest] public IEnumerator Fix1_RepairedStartupRadiusValidatesAllEightActualPoses() => InvalidStartupActor(false,true);
        private IEnumerator InvalidStartupActor(bool invalidPosition,bool repair=false)
        {
            UnityEngine.Events.UnityAction<Scene,LoadSceneMode> mutate=(scene,mode)=>
            {
                if(scene.path!="Assets/Scenes/Validation/Phase11Navigation.unity") return;
                var fixture=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<NavigationValidationController>(true)).Single();
                if(invalidPosition) fixture.Actors[0].transform.position=new Vector3(100,0,100);
                else fixture.Actors[0].Agent.radius=.2f;
            };
            SceneManager.sceneLoaded+=mutate;
            try
            {
                EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/Validation/Phase11Navigation.unity",new LoadSceneParameters(LoadSceneMode.Single));
                yield return null; yield return null; yield return null;
            }
            finally { SceneManager.sceneLoaded-=mutate; }
            var fixture=Object.FindFirstObjectByType<NavigationValidationController>(); var bridge=Object.FindFirstObjectByType<NavigationDecorationBridge>();
            var time=Object.FindFirstObjectByType<GameTimeService>(); var failedStart=fixture.Actors[0].transform.position;
            Assert.That(fixture.Ready,Is.False); Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Paused));
            Assert.That(fixture.World.RegisteredActors.Count,Is.EqualTo(7));
            for(var i=0;i<2;i++) { Assert.That(bridge.ResumeAfterRepair(),Is.False); Assert.That(time.IsResumeBlocked,Is.True); }
            Assert.That(fixture.Actors[0].transform.position,Is.EqualTo(failedStart));
            if(repair)
            {
                var positions=fixture.Actors.Select(a=>a.transform.position).ToArray();
                fixture.Actors[0].Agent.radius=.45f; // Test repairs the bad authoring; runtime must never change it.
                Assert.That(bridge.ResumeAfterRepair(),Is.True);
                Assert.That(fixture.World.RegisteredActors.Count,Is.EqualTo(8)); Assert.That(fixture.Ready,Is.True);
                Assert.That(fixture.Actors.Select(a=>a.transform.position),Is.EqualTo(positions));
                Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Paused)); Assert.That(time.IsResumeBlocked,Is.False);
            }
            Object.Destroy(bridge); yield return null;
            Assert.That(time.TrySetSpeed(GameSpeed.Fast),Is.False,"The surviving world must retain the failed required actor");
        }
        private IEnumerator RunButton(string id,int minimumRequests)
        {
            var button=Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b=>b.name==id);
            button.onClick.Invoke();
            Assert.That(controller.SubmittedCount,Is.GreaterThan(0));
            for(var frame=0;frame<7200&&controller.Busy;frame++)
            {
                var before=controller.Actors.Select(a=>a.transform.position).ToArray();
                yield return null;
                if(id=="crossing")
                {
                    var separation=before[0]-before[1];
                    var relative=(controller.Actors[0].transform.position-before[0])-(controller.Actors[1].transform.position-before[1]);
                    var t=relative.sqrMagnitude<1e-12f?0:Mathf.Clamp01(-Vector3.Dot(separation,relative)/relative.sqrMagnitude);
                    Assert.That((separation+t*relative).magnitude,Is.GreaterThanOrEqualTo(.899f),"Independent continuous crossing separation");
                }
                for(var i=0;i<before.Length;i++)
                    Assert.That(Vector3.Distance(before[i],controller.Actors[i].transform.position),Is.LessThanOrEqualTo(1.2f*Time.deltaTime+.002f),"No scenario teleport");
            }
            Assert.That(controller.Busy,Is.False,controller.StatusText);
            Assert.That(controller.SubmittedCount,Is.GreaterThanOrEqualTo(minimumRequests),controller.StatusText);
            Assert.That(controller.Results.Count,Is.EqualTo(controller.SubmittedCount));
            Assert.That(controller.StatusText,Does.Contain("complete"));
            foreach(var result in controller.Results) Assert.That(result.RetryCount,Is.LessThanOrEqualTo(1));
        }
        [UnityTest] public IEnumerator StraightButtonArrives() { yield return RunButton("straight",2); Assert.That(controller.Results.Last().Status,Is.EqualTo(MovementStatus.Arrived)); Capture("straight"); }
        [UnityTest] public IEnumerator DetourButtonArrives() { yield return RunButton("detour",2); Assert.That(controller.Results.Last().Status,Is.EqualTo(MovementStatus.Arrived)); }
        [UnityTest] public IEnumerator CrossingButtonTerminates() { yield return RunButton("crossing",4); Assert.That(controller.Results.Skip(2).All(r=>r.Status==MovementStatus.Arrived),Is.True,controller.StatusText); }
        [UnityTest] public IEnumerator NarrowWaitButtonTerminates() { yield return RunButton("narrow-wait",4); }
        [UnityTest] public IEnumerator SameTargetButtonTerminates() { yield return RunButton("same-target",4); Assert.That(controller.Results.Count(r=>r.Status==MovementStatus.Failed),Is.GreaterThanOrEqualTo(1)); }
        [UnityTest] public IEnumerator RecoveryButtonWalksToFallback() { yield return RunButton("recovery",2); Assert.That(controller.Results.Last().Status,Is.EqualTo(MovementStatus.Recovered)); Assert.That(controller.Results.Last().OriginalFailure,Is.Not.EqualTo(NavigationFailure.None)); }
        [UnityTest] public IEnumerator CrowdEightButtonTerminates() { yield return RunButton("crowd8",8); Capture("crowd8"); }
        [UnityTest] public IEnumerator Point85ButtonRejectsInsideNarrowCorridor() { yield return RunButton("blocked85",2); Assert.That(controller.Results.Last().Status,Is.EqualTo(MovementStatus.Failed)); }
        [UnityTest] public IEnumerator DetourAt30FpsMustArrive() => ProbeButtonAtFrameRate("detour",30);
        [UnityTest] public IEnumerator DetourAt15FpsMustArrive() => ProbeButtonAtFrameRate("detour",15);
        [UnityTest] public IEnumerator DetourAt144FpsMustArrive() => ProbeButtonAtFrameRate("detour",144);
        [UnityTest] public IEnumerator DetourAtVariableFrameRateMustArrive() => ProbeButtonAtFrameRate("detour",0);
        [UnityTest] public IEnumerator CrowdEightAt30FpsKeepsStartsAndBodiesSafe() => ProbeButtonAtFrameRate("crowd8",30);
        [UnityTest] public IEnumerator CrowdEightAt15FpsKeepsStartsAndBodiesSafe() => ProbeButtonAtFrameRate("crowd8",15);
        private IEnumerator ProbeButtonAtFrameRate(string id,int framesPerSecond)
        {
            // 每个 UnitySetUp 已加载独立场景；真实按钮以指定模拟帧率运行。
            Time.captureDeltaTime=framesPerSecond==0 ? 0 : 1f/framesPerSecond;
            var realDeadline=Time.realtimeSinceStartup+120f;
            var simulatedSeconds=0f;
            Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b=>b.name==id).onClick.Invoke();
            Assert.That(controller.SubmittedCount,Is.GreaterThan(0),ProbeState(id,framesPerSecond));
            try
            {
                for(var frame=0;controller.Busy&&simulatedSeconds<60f&&Time.realtimeSinceStartup<realDeadline;frame++)
                {
                    var before=controller.Actors.Select(a=>a.transform.position).ToArray();
                    yield return null;
                    simulatedSeconds+=Time.deltaTime;
                    var after=controller.Actors.Select(a=>a.transform.position).ToArray();
                    if(id=="crowd8")
                        for(var a=0;a<before.Length;a++) for(var b=a+1;b<before.Length;b++)
                        {
                            var separation=before[a]-before[b]; var relative=(after[a]-before[a])-(after[b]-before[b]);
                            var t=relative.sqrMagnitude<1e-12f?0:Mathf.Clamp01(-Vector3.Dot(separation,relative)/relative.sqrMagnitude);
                            if((separation+t*relative).magnitude<.899f)
                                Assert.Fail("Continuous pair separation "+controller.Actors[a].ActorId+"/"+controller.Actors[b].ActorId+
                                    " frame="+frame+" "+ProbeState(id,framesPerSecond));
                        }
                    for(var i=0;i<before.Length;i++)
                        if(Vector3.Distance(before[i],after[i])>1.2f*Time.deltaTime+.002f)
                            Assert.Fail("No teleport actor="+controller.Actors[i].ActorId+" frame="+frame+" "+ProbeState(id,framesPerSecond));
                }
                Assert.That(controller.Busy,Is.False,"simulated="+simulatedSeconds+" realtimeRemaining="+(realDeadline-Time.realtimeSinceStartup)+" "+ProbeState(id,framesPerSecond));
                Assert.That(controller.Results.Count,Is.EqualTo(controller.SubmittedCount),ProbeState(id,framesPerSecond));
                if(id=="detour")
                    Assert.That(controller.Results.Last().Status,Is.EqualTo(MovementStatus.Arrived),ProbeState(id,framesPerSecond));
                else
                {
                    Assert.That(controller.SubmittedCount,Is.EqualTo(8),ProbeState(id,framesPerSecond));
                    Assert.That(controller.Results.All(r=>r.Status==MovementStatus.Arrived || r.Status==MovementStatus.Failed),Is.True,
                        ProbeState(id,framesPerSecond));
                    Assert.That(controller.Results.All(r=>r.Reason!=NavigationFailure.InvalidStart),Is.True,
                        "Approved movement cannot invalidate a retry start. "+ProbeState(id,framesPerSecond));
                }
            }
            finally
            {
                TestContext.WriteLine("P11_MANUAL_PROBE_FINAL "+ProbeState(id,framesPerSecond));
            }
        }
        private string ProbeState(string id,int fps)
            => id+" fps="+(fps==0?"variable":fps.ToString())+" dt="+Time.deltaTime+" status="+controller.StatusText+
               " actors="+string.Join(";",controller.Actors.Select(a=>a.ActorId+"="+a.transform.position.ToString("F9")))+
               " results="+string.Join(";",controller.Results.Select(r=>r.ActorId+":"+r.Status+"/"+r.Reason+"/retry="+r.RetryCount+
                   "/original="+r.OriginalFailure));
        [UnityTest] public IEnumerator CancelDestroyAndUnloadCompleteOnce()
        {
            var a=controller.Actors[0]; var b=controller.Actors[1]; var c=controller.Actors[2];
            var callbacks=new int[3]; MovementResult? other=null;
            var first=controller.World.Service.MoveTo(a.ActorId,new NavigationTarget(new Vector3(-9,0,-4)),r=>callbacks[0]++);
            controller.World.Service.MoveTo(b.ActorId,new NavigationTarget(new Vector3(-6.4f,0,-4)),r=>callbacks[1]++);
            controller.World.Service.MoveTo(c.ActorId,new NavigationTarget(new Vector3(7,0,-4)),r=>{callbacks[2]++;other=r;});
            Assert.That(controller.World.Service.Cancel(first.RequestId),Is.True);
            Assert.That(controller.World.Service.Cancel(first.RequestId),Is.False);
            Object.Destroy(b.gameObject); yield return null;
            Assert.That(callbacks[0],Is.EqualTo(1)); Assert.That(callbacks[1],Is.EqualTo(1));
            for(var i=0;i<2400&&!other.HasValue;i++) yield return null;
            Assert.That(other.HasValue,Is.True); Assert.That(other.Value.Status,Is.EqualTo(MovementStatus.Arrived));
            var unloaded=0; controller.World.Service.MoveTo(c.ActorId,new NavigationTarget(new Vector3(6,0,4)),r=>unloaded++);
            var scene=SceneManager.GetActiveScene(); var empty=SceneManager.CreateScene("UnloadEvidence"); SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(scene);
            Assert.That(unloaded,Is.EqualTo(1)); Assert.That(callbacks,Is.EqualTo(new[]{1,1,1}));
        }
        [UnityTest] public IEnumerator OriginalCounterAnchorsArriveAndFaceWithoutOffset()
        {
            // Original domain anchors are adjacent cell centers, with no P11 safety offset.
            foreach(var longCounter in new[]{false,true})
            {
                var definition=new FurnitureDefinition("counter","Counter",new GridSize(1,longCounter?3:1),PlacementSurfaceType.Floor);
                var layout=new CafeLayout(new GridSettings(1),new FurnitureDefinitionCatalog(new[]{definition}));
                layout.AddRegion(new LayoutRegion("room",new GridPosition(0,0),new GridSize(20,20),LayoutZoneType.Interior));
                var cell=longCounter?new GridPosition(8,7):new GridPosition(12,9);
                Assert.That(layout.PlaceFurniture(FurnitureInstance.Restore("7f17d8fa59f64be0a6689666ce4a28d2","counter",cell,FurnitureRotation.Degrees0)).Succeeded,Is.True);
                var slots=new SurfaceSlotCatalog(new[]{new SurfaceSlotDefinition("counter","slot",new GridPosition(0,longCounter?1:0))});
                var anchors=new InteractionAnchorResolver().ResolvePickUp(new PickUpPointInstance("af17d8fa59f64be0a6689666ce4a28d2",new SurfaceSlotAddress("7f17d8fa59f64be0a6689666ce4a28d2","slot")),layout,slots,p=>p.X!=cell.X);
                Assert.That(anchors.TryGetAnchor(InteractionRole.Employee,out var anchor),Is.True);
                var target=new Vector3(anchor.Position.X-8,0,anchor.Position.Y-8);
                var actor=controller.Actors[longCounter?0:1]; MovementResult? result=null;
                controller.World.Service.MoveTo(actor.ActorId,new NavigationTarget(target,Vector3.left),r=>result=r);
                for(var frame=0;frame<3600&&!result.HasValue;frame++) yield return null;
                Assert.That(result.HasValue,Is.True); Assert.That(result.Value.Status,Is.EqualTo(MovementStatus.Arrived),result.Value.Reason.ToString());
                Assert.That(Vector3.Distance(actor.transform.position,target),Is.LessThanOrEqualTo(.08f));
                Assert.That(Vector3.Angle(actor.transform.forward,Vector3.left),Is.LessThanOrEqualTo(5));
                yield return null;
                Assert.That(actor.GetComponent<NavigationWalkPresenter>().ActualSpeed,Is.LessThanOrEqualTo(.05f));
                Assert.That(actor.ModelAnimator.GetCurrentAnimatorStateInfo(0).IsName("Hold"),Is.True);
                Capture(longCounter?"counter-1x3":"counter-1x1");
            }
        }
                [UnityTest] public IEnumerator VisualHeadingMatchesActorForward()
        {
            controller.World.enabled=false;
            foreach(var actor in controller.Actors.Take(2))
            {
                var bones=actor.ModelRoot.GetComponentsInChildren<Transform>();
                var head=bones.Single(t=>t.name=="head"); var pelvis=bones.Single(t=>t.name=="pelvis");
                var front=head.position-pelvis.position; front.y=0;
                Debug.Log("P11_VISUAL_FORWARD "+actor.ActorId+" head-pelvis="+front.ToString("F5")+" actor="+actor.transform.forward);
            }
            foreach(var actor in controller.Actors.Take(2))
            {
                var bones=actor.ModelRoot.GetComponentsInChildren<Transform>();
                var front=bones.Single(t=>t.name=="head").position-bones.Single(t=>t.name=="pelvis").position; front.y=0;
                Assert.That(Vector3.Dot(front.normalized,actor.transform.forward),Is.GreaterThan(.8f),"Authored visual front must match navigation heading");
            }
            yield return null;
        }
        [UnityTest] public IEnumerator WalkCycleKeepsProxyAndRootStableAtAllHeadings()
        {
            controller.World.enabled=false;
            foreach(var actor in controller.Actors.Take(2))
            {
                var presenter=actor.GetComponent<NavigationWalkPresenter>(); var position=actor.transform.position;
                var renderer=actor.GetComponentInChildren<SkinnedMeshRenderer>();
                Assert.That(renderer.bounds.size.y,Is.InRange(1.2f,1.8f),"Actual runtime Hold size");
                Assert.That(BakedWorldBounds(renderer).size.y,Is.InRange(1.27f,1.34f),"Compensated Hold vertices match source height");
                var rootPosition=actor.ModelRoot.localPosition; var rootScale=actor.ModelRoot.localScale; var rootRotation=actor.ModelRoot.localRotation;
                presenter.SetMotion(1.2f);
                Assert.That(actor.ModelAnimator.speed,Is.EqualTo(1.2f*presenter.WalkDuration/presenter.TravelPerCycle).Within(.0001f));
                var animator=actor.ModelAnimator; animator.Play("Walk",0,0); animator.Update(0);
                var bones=actor.ModelRoot.GetComponentsInChildren<Transform>();
                var firstPose=bones.Select(t=>t.localRotation).ToArray();
                for(var frame=0;frame<=60;frame++)
                {
                    // Sample the full imported cycle; the actor position and proxy dimensions stay fixed.
                    var baked=BakedWorldBounds(renderer);
                    Assert.That(baked.size.y,Is.InRange(1.20f,1.4f),actor.ActorId+" runtime Walk height");
                    Assert.That(baked.min.y-actor.transform.position.y,Is.InRange(-.04f,.08f),"Feet stay near floor");
                    actor.transform.rotation=Quaternion.Euler(0,frame*6,0); // Fixture samples all headings with movement disabled.
                    Assert.That(actor.transform.position,Is.EqualTo(position));
                    Assert.That(Quaternion.Angle(actor.ModelRoot.localRotation,rootRotation),Is.LessThan(.001f),"Animator must not overwrite authored visual yaw");
                    Assert.That(actor.ModelRoot.localPosition,Is.EqualTo(rootPosition)); Assert.That(actor.ModelRoot.localScale,Is.EqualTo(rootScale));
                    Assert.That(actor.Proxy.radius,Is.EqualTo(.45f)); Assert.That(actor.Proxy.height,Is.EqualTo(1.30f));
                    Assert.That(Vector3.Distance(actor.Proxy.transform.lossyScale,Vector3.one),Is.LessThan(.00001f));
                    if(frame==60) for(var i=0;i<bones.Length;i++) Assert.That(Quaternion.Angle(firstPose[i],bones[i].localRotation),Is.LessThan(3f),"Loop seam bone: "+bones[i].name);
                    animator.Update(presenter.WalkDuration/60/animator.speed);
                }
                presenter.SetMotion(0); animator.Update(.1f);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Hold"),Is.True);
            }
            Capture("authored-fixture"); yield return null;
        }
        private static Bounds BakedWorldBounds(SkinnedMeshRenderer renderer)
        {
            var mesh=new Mesh();
            try { renderer.BakeMesh(mesh,true); var vertices=mesh.vertices; var matrix=renderer.localToWorldMatrix;
                var bounds=new Bounds(matrix.MultiplyPoint3x4(vertices[0]),Vector3.zero);
                foreach(var v in vertices) bounds.Encapsulate(matrix.MultiplyPoint3x4(v)); return bounds; }
            finally { Object.DestroyImmediate(mesh); }
        }
        private static void Capture(string name)
        {
            var camera=Object.FindFirstObjectByType<UnityEngine.Camera>(); var oldTarget=camera.targetTexture; var oldRect=camera.rect;
            var oldPosition=camera.transform.position; var oldRotation=camera.transform.rotation; var oldSize=camera.orthographicSize;
            if(name.StartsWith("counter-")) { var focus=name=="counter-1x1" ? new Vector3(4,.4f,1) : new Vector3(0,.4f,0); camera.transform.position=focus+new Vector3(5,4,-6); camera.transform.LookAt(focus); camera.orthographicSize=2.8f; }
            var target=new RenderTexture(1200,900,24); var oldActive=RenderTexture.active;
            var image=new Texture2D(1200,900,TextureFormat.RGB24,false);
            try
            {
                camera.rect=new Rect(0,0,1,1); camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
                image.ReadPixels(new Rect(0,0,1200,900),0,0); image.Apply();
                Directory.CreateDirectory("outputs/phase11"); File.WriteAllBytes("outputs/phase11/task4-"+name+".png",image.EncodeToPNG());
            }
            finally { camera.targetTexture=oldTarget; camera.rect=oldRect; camera.transform.SetPositionAndRotation(oldPosition,oldRotation); camera.orthographicSize=oldSize; RenderTexture.active=oldActive; Object.DestroyImmediate(image); Object.DestroyImmediate(target); }
        }
    }
}
#endif
