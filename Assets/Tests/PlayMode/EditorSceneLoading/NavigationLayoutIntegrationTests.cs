#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using AnimalCafe.Content;
using AnimalCafe.Decoration;
using AnimalCafe.Decoration.Input;
using AnimalCafe.Layout;
using AnimalCafe.Navigation;
using AnimalCafe.UI.Decoration;
using AnimalCafe.UI.Foundation;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;
namespace AnimalCafe.Tests.PlayMode.EditorSceneLoading
{
    public sealed class NavigationLayoutIntegrationTests
    {
        NavigationLayoutAdapter adapter; NavigationWorld world; NavigationDecorationBridge bridge;
        DecorationModeController controller; CafeLayoutRuntime runtime; Transform grid; float savedScale,savedCapture;
        private UnityEngine.InputSystem.InputActionAsset[] ownedInputAssets=Array.Empty<UnityEngine.InputSystem.InputActionAsset>();
        [UnitySetUp] public IEnumerator Setup()
        {
            savedScale=Time.timeScale; savedCapture=Time.captureDeltaTime;
            var scene=EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/MainCafe.unity",new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; yield return null; yield return null;
            ownedInputAssets=Phase8SceneInputTestCleanup.CaptureAssets(scene);
            Assert.That(ownedInputAssets,Is.Not.Empty,"Capture loaded UI actions for fixture cleanup");
            controller=Object.FindFirstObjectByType<DecorationModeController>(); runtime=Object.FindFirstObjectByType<CafeLayoutRuntime>();
            controller.EnterDecorationMode(); grid=Field<Transform>(controller,"gridRoot");
            var floor=new GameObject("Task5 explicit floor").AddComponent<BoxCollider>(); floor.transform.SetPositionAndRotation(grid.position,grid.rotation); floor.center=new Vector3(4,-.1f,4); floor.size=new Vector3(8,.2f,8);
            world=new GameObject("Task5 World").AddComponent<NavigationWorld>(); world.enabled=false;
            adapter=new GameObject("Task5 Adapter").AddComponent<NavigationLayoutAdapter>();
            adapter.Configure(runtime,Field<FurnitureContentCatalog>(controller,"contentCatalog"),grid,floor,Array.Empty<Collider>(),world,false,
                Field<System.Collections.Generic.Dictionary<string,WallMountedDefinitionAsset>>(controller,"phase7WallDefinitionsById").Values.ToArray(),Field<WallSurfaceAuthoring[]>(controller,"phase7WallAuthoring"));
            bridge=new GameObject("Task5 Bridge").AddComponent<NavigationDecorationBridge>(); bridge.Configure(adapter); Set(controller,"navigationBridge",bridge);
            adapter.ConfigureRepresentationRoots(Field<Transform>(controller,"furnitureRepresentationRoot"),Field<Transform>(controller,"surfaceMountedRepresentationRoot"));
            Assert.That(adapter.RebuildAndValidate().CanResume,Is.True);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(controller!=null) Set(controller,"navigationBridge",null);
            if(adapter!=null) Object.Destroy(adapter.gameObject); if(world!=null) Object.Destroy(world.gameObject);
            yield return null; Time.timeScale=savedScale; Time.captureDeltaTime=savedCapture;
            // Release this scene's cached controls before later input fixtures reset / 先释放场景输入缓存。
            var scene=SceneManager.GetSceneByPath("Assets/Scenes/MainCafe.unity");
            if(scene.IsValid() && scene.isLoaded)
            {
                var empty=SceneManager.CreateScene("NavigationLayoutCleanup"); SceneManager.SetActiveScene(empty);
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            Phase8SceneInputTestCleanup.DisposeReleasedAssets(ownedInputAssets);
            ownedInputAssets=Array.Empty<UnityEngine.InputSystem.InputActionAsset>();
        }
        [UnityTest] public IEnumerator RealFurnitureConfirmRejectsBeforeCommitThenConfirmStoreInvalidateAndFreshMoveWorks()
        {
            Select("furniture.counter.module.01"); var session=Field<DecorationSession>(controller,"session"); Refresh(session.MovePreview(new GridPosition(5,5)));
            var pose=adapter.FurnitureCandidate(session.ActivePreview).First(); var position=pose.Position; position.y=grid.position.y;
            var actor=Actor(position); Assert.That(world.Register(actor),Is.True,"Actor should start on explicit confirmed floor");
            var before=Snapshot(); var preview=session.ActivePreview; var revision=adapter.Revision;
            var action=Object.FindFirstObjectByType<DecorationActionBarView>(); Button(action,"ConfirmButton").onClick.Invoke();
            Assert.That(Snapshot(),Is.EqualTo(before)); Assert.That(session.ActivePreview,Is.SameAs(preview)); Assert.That(adapter.Revision,Is.EqualTo(revision));
            Assert.That(Field<string>(controller,"currentEditingMessage"),Is.EqualTo(NavigationDecorationBridge.ActorOverlapMessage));
            MovementResult? old=null, dirty=null;
            world.Service.MoveTo(actor.ActorId,new NavigationTarget(grid.TransformPoint(new Vector3(6,0,2))),r=> { old=r; world.Service.MoveTo(actor.ActorId,new NavigationTarget(grid.TransformPoint(new Vector3(6,0,2))),x=>dirty=x,new NavigationTarget(position)); });
            Refresh(session.MovePreview(new GridPosition(0,4))); Button(action,"ConfirmButton").onClick.Invoke();
            Assert.That(session.ActivePreview,Is.Null); Assert.That(adapter.Revision,Is.EqualTo(revision+1)); Assert.That(world.LayoutAvailable,Is.False);
            Assert.That(old.Value.Status,Is.EqualTo(MovementStatus.LayoutChanged)); Assert.That(dirty.Value.Reason,Is.EqualTo(NavigationFailure.LayoutUnavailable)); Assert.That(dirty.Value.RetryCount,Is.Zero);
            Assert.That(adapter.RebuildAndValidate().CanResume,Is.True); Assert.That(actor.transform.position,Is.EqualTo(position));
            MovementResult? fresh=null; world.Service.MoveTo(actor.ActorId,new NavigationTarget(grid.TransformPoint(new Vector3(6,0,2))),r=>fresh=r);
            Assert.That(fresh.HasValue,Is.False); Time.timeScale=1; Time.captureDeltaTime=1f/60;
            for(var i=0;i<600 && !fresh.HasValue;i++) { yield return null; world.Step(Time.deltaTime); }
            Assert.That(fresh.HasValue,Is.True); Assert.That(fresh.Value.Status,Is.EqualTo(MovementStatus.Arrived),fresh.Value.Reason.ToString());
            Time.timeScale=0;
            var added=runtime.Layout.FurnitureInstances.Single(x=>!before.Any(line=>line.StartsWith(x.InstanceId+"|")));
            Refresh(session.BeginExisting(added.InstanceId)); Button(action,"StoreButton").onClick.Invoke();
            var modal=Object.FindFirstObjectByType<DecorationStoreModalView>(); Field<Button>(modal,"confirmButton").onClick.Invoke();
            Assert.That(Snapshot(),Is.EqualTo(before)); Assert.That(adapter.Revision,Is.EqualTo(revision+2)); Assert.That(world.LayoutAvailable,Is.False);
        }
        [UnityTest] public IEnumerator FurniturePreviewTurnsRedOverStationaryActorAndGreenAfterMovingAway()
        {
            Select("furniture.counter.module.01"); var session=Field<DecorationSession>(controller,"session");
            Refresh(session.MovePreview(new GridPosition(5,5)));
            var position=adapter.FurnitureCandidate(session.ActivePreview).First().Position; position.y=grid.position.y;
            var actor=Actor(position); Assert.That(world.Register(actor),Is.True);
            var before=Snapshot(); var revision=adapter.Revision; var bakes=adapter.BakeCount;
            Invoke(controller,"ApplyPreviewMove",new GridPosition(0,4));
            AssertFurnitureFootprint(true);
            Invoke(controller,"ApplyPreviewMove",new GridPosition(5,5));
            Assert.That(session.ActivePreview.PlacementResult.Succeeded,Is.True,"Domain placement remains valid; the actor blocks navigation placement");
            AssertFurnitureFootprint(false);
            var preview=session.ActivePreview;
            Button(Object.FindFirstObjectByType<DecorationActionBarView>(),"ConfirmButton").onClick.Invoke();
            Assert.That(session.ActivePreview,Is.SameAs(preview)); Assert.That(Snapshot(),Is.EqualTo(before));
            Assert.That(Field<string>(controller,"currentEditingMessage"),Is.EqualTo(NavigationDecorationBridge.ActorOverlapMessage));
            AssertFurnitureFootprint(false);
            Invoke(controller,"ApplyPreviewMove",new GridPosition(0,4));
            AssertFurnitureFootprint(true);
            Assert.That(actor.transform.position,Is.EqualTo(position)); Assert.That(Snapshot(),Is.EqualTo(before));
            Assert.That(adapter.Revision,Is.EqualTo(revision)); Assert.That(adapter.BakeCount,Is.EqualTo(bakes));
            yield return null;
        }

        [UnityTest] public IEnumerator PreviewRootDriftTurnsRedWithoutSuspendingNavigation()
        {
            Select("furniture.counter.module.01"); var session=Field<DecorationSession>(controller,"session");
            Refresh(session.MovePreview(new GridPosition(5,5))); AssertFurnitureFootprint(true);
            var formal=Field<Transform>(controller,"furnitureRepresentationRoot"); var original=formal.position;
            var before=Snapshot(); var revision=adapter.Revision; var bakes=adapter.BakeCount;
            var published=adapter.PublishedRevision; var readiness=adapter.CurrentReadiness;
            Assert.That(world.LayoutAvailable,Is.True);
            try
            {
                formal.position+=Vector3.right;
                Invoke(controller,"SyncActivePreviewPresentation");
                Assert.That(world.LayoutAvailable,Is.True,"Preview validation must not suspend navigation");
                Assert.That(adapter.CurrentReadiness,Is.SameAs(readiness)); Assert.That(adapter.PublishedRevision,Is.EqualTo(published));
                AssertFurnitureFootprint(false);
                Assert.That(Snapshot(),Is.EqualTo(before)); Assert.That(adapter.Revision,Is.EqualTo(revision));
                Assert.That(adapter.BakeCount,Is.EqualTo(bakes));
            }
            finally { formal.position=original; Invoke(controller,"SyncActivePreviewPresentation"); }
            AssertFurnitureFootprint(true); yield return null;
        }

        [UnityTest] public IEnumerator MissingPreviewGeometryTurnsRedWithoutSuspendingNavigation()
        {
            Select("furniture.counter.module.01"); var session=Field<DecorationSession>(controller,"session");
            Refresh(session.MovePreview(new GridPosition(5,5))); AssertFurnitureFootprint(true);
            var position=grid.TransformPoint(new Vector3(6,0,2)); var actor=Actor(position);
            Assert.That(world.Register(actor),Is.True);
            var catalog=Field<FurnitureContentCatalog>(adapter,"contentCatalog"); var preview=session.ActivePreview;
            var before=Snapshot(); var revision=adapter.Revision; var bakes=adapter.BakeCount;
            var published=adapter.PublishedRevision; var readiness=adapter.CurrentReadiness;
            Assert.That(world.LayoutAvailable,Is.True);
            try
            {
                // Only navigation geometry becomes unavailable; the visible preview and domain stay valid.
                // 仅移除导航 catalog，验证展示降级不会暂停营业或修改 domain。
                Set(adapter,"contentCatalog",null);
                Assert.That(session.ActivePreview.PlacementResult.Succeeded,Is.True);
                Assert.DoesNotThrow(()=>Invoke(controller,"SyncActivePreviewPresentation"),
                    "Unavailable candidate geometry should produce an invalid footprint, not break preview refresh");
                AssertFurnitureFootprint(false);
                Assert.That(world.LayoutAvailable,Is.True);
                Assert.That(adapter.CurrentReadiness,Is.SameAs(readiness)); Assert.That(adapter.PublishedRevision,Is.EqualTo(published));
                Assert.That(adapter.Revision,Is.EqualTo(revision)); Assert.That(adapter.BakeCount,Is.EqualTo(bakes));
                Assert.That(actor.transform.position,Is.EqualTo(position)); Assert.That(session.ActivePreview,Is.SameAs(preview));
                Assert.That(Snapshot(),Is.EqualTo(before));
            }
            finally { Set(adapter,"contentCatalog",catalog); Invoke(controller,"SyncActivePreviewPresentation"); }
            AssertFurnitureFootprint(true); yield return null;
        }

        [UnityTest] public IEnumerator RotationRejectsBeforeMutation()
        {
            // Same UI guard is reached for Rotate + Confirm, using confirmed prefab geometry.
            var original=runtime.Layout.FurnitureInstances.Single(); var session=Field<DecorationSession>(controller,"session");
            Refresh(session.BeginExisting(original.InstanceId)); Refresh(session.MovePreview(new GridPosition(5,5)));
            var action=Object.FindFirstObjectByType<DecorationActionBarView>(); Button(action,"RotateButton").onClick.Invoke();
            var candidate=adapter.FurnitureCandidate(session.ActivePreview).First(); var position=candidate.Position; position.y=grid.position.y;
            var actor=Actor(position); Assert.That(world.Register(actor),Is.True); var before=Snapshot();
            Button(action,"ConfirmButton").onClick.Invoke(); Assert.That(Snapshot(),Is.EqualTo(before)); Assert.That(session.ActivePreview,Is.Not.Null);
            Assert.That(Field<string>(controller,"currentEditingMessage"),Is.EqualTo(NavigationDecorationBridge.ActorOverlapMessage)); yield return null;
        }
        [UnityTest] public IEnumerator MountedConfirmAndAttachedSupportUseAuthoredSolidBeforeCommit()
        {
            var originalCatalog=Field<FurnitureContentCatalog>(controller,"contentCatalog");
            var catalog=Object.Instantiate(originalCatalog);
            var entries=Field<System.Collections.Generic.List<FurnitureDefinitionAsset>>(catalog,"entries").ToList();
            var original=entries.First(x=>x.FunctionType==FurnitureFunctionType.CoffeeMachine);
            var definition=Object.Instantiate(original); var prefab=Object.Instantiate(original.Prefab); prefab.SetActive(false);
            prefab.transform.position=new Vector3(1000,0,1000);
            var extra=prefab.AddComponent<BoxCollider>(); extra.center=new Vector3(0,.2f,1.2f); extra.size=new Vector3(.6f,.5f,.6f);
            try
            {
            Set(definition,"prefab",prefab); entries[entries.IndexOf(original)]=definition; Set(catalog,"entries",entries);
            Set(controller,"contentCatalog",catalog); Set(runtime,"contentCatalog",catalog);
            Set(Field<FurnitureSceneRegistry>(controller,"sceneRegistry"),"contentCatalog",catalog);
            Set(Field<SurfaceMountedSceneRegistry>(controller,"surfaceMountedSceneRegistry"),"contentCatalog",catalog);
            Set(adapter,"contentCatalog",catalog);
            var support=runtime.Layout.FurnitureInstances.Single();
            var slot=catalog.BuildSurfaceSlotCatalog(runtime.Layout.GridSettings).GetForSupport(support.DefinitionId).First();
            var session=Field<FunctionalSurfaceDecorationSession>(controller,"functionalSurfaceSession");
            Assert.That(session.BeginCreateMounted(definition.DefinitionId,new SurfaceSlotAddress(support.InstanceId,slot.SlotId)).Succeeded,Is.True);
            var candidate=adapter.MountedCandidate(session.ActivePreview).Last(); var start=candidate.Position; start.y=grid.position.y;
            var actor=Actor(start); Assert.That(world.Register(actor),Is.True);
            var before=runtime.FunctionalSurfaceLayout.MountedInstances.Select(x=>x.InstanceId).ToArray(); var preview=session.ActivePreview;
            Invoke(controller,"RefreshFunctionalSurfacePreviewViews");
            Assert.That(preview.CanConfirm,Is.True);
            AssertMountedFootprint(false);
            Assert.That(controller.TryConfirmFunctionalSurfacePreview(),Is.False);
            Assert.That(runtime.FunctionalSurfaceLayout.MountedInstances.Select(x=>x.InstanceId),Is.EqualTo(before)); Assert.That(session.ActivePreview,Is.SameAs(preview));
            Assert.That(Field<string>(controller,"currentEditingMessage"),Is.EqualTo(NavigationDecorationBridge.ActorOverlapMessage));
            Assert.That(controller.TryRotateFunctionalSurfacePreview(),Is.True);
            AssertMountedFootprint(true);
            Assert.That(actor.transform.position,Is.EqualTo(start));
            // Restore the original candidate for the existing attached-support guard checks.
            for(var turn=0;turn<3;turn++) Assert.That(controller.TryRotateFunctionalSurfacePreview(),Is.True);
            AssertMountedFootprint(false);
            // Fixture commits the mounted domain entry so the next furniture candidate must carry it.
            Assert.That(session.Confirm().Succeeded,Is.True);
            var furniture=new DecorationSession(runtime.Layout,runtime.FunctionalSurfaceLayout); furniture.Enter(); furniture.BeginExisting(support.InstanceId);
            Assert.That(adapter.FurnitureCandidate(furniture.ActivePreview).Any(x=>x.FurnitureId==runtime.FunctionalSurfaceLayout.MountedInstances.Single().InstanceId),Is.True);
            Assert.That(bridge.CanConfirmFurniture(furniture.ActivePreview,out var reason),Is.False); Assert.That(reason,Is.EqualTo(NavigationDecorationBridge.ActorOverlapMessage));
            }
            finally
            {
            // Restore source references before scene teardown; none of these temporary assets are saved.
            Set(controller,"contentCatalog",originalCatalog); Set(runtime,"contentCatalog",originalCatalog); Set(adapter,"contentCatalog",originalCatalog);
            Set(Field<FurnitureSceneRegistry>(controller,"sceneRegistry"),"contentCatalog",originalCatalog);
            Set(Field<SurfaceMountedSceneRegistry>(controller,"surfaceMountedSceneRegistry"),"contentCatalog",originalCatalog);
            Object.Destroy(prefab); Object.Destroy(definition); Object.Destroy(catalog);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator SolidWallConfirmRejectsBeforeCommitAndStoreInvalidates()
        {
            Assert.That(controller.TryChangeMode(DecorationModeKind.WallDecor),Is.True);
            var definitions=Field<System.Collections.Generic.Dictionary<string,WallMountedDefinitionAsset>>(controller,"phase7WallDefinitionsById");
            var original=definitions.Values.First(); var definition=Object.Instantiate(original);
            var prefab=new GameObject("Task5 wall solid prefab"); prefab.SetActive(false); var box=prefab.AddComponent<BoxCollider>(); box.center=new Vector3(0,.65f,.25f); box.size=new Vector3(.5f,.6f,.3f);
            try
            {
            Set(definition,"prefab",prefab); definitions[definition.DefinitionId]=definition;
            var authored=Field<WallSurfaceAuthoring[]>(controller,"phase7WallAuthoring");
            var session=new WallMountedDecorationSession(runtime.WallMountedLayout,definitions.Values); Set(controller,"wallMountedSession",session); Set(adapter,"wallDefinitions",definitions.Values.ToArray());
            var surface=authored.First(); session.BeginNew(definition.DefinitionId,surface.SurfaceId,new WallSlotPosition(2,0));
            var pose=adapter.WallCandidate(session.ActivePreview).Single();
            var start=pose.Position+surface.transform.forward*.35f; start.y=grid.position.y;
            // Wall authoring's front can face outward: choose the same solid's interior floor side.
            if(!NavMesh.SamplePosition(start,out _,.05f,NavMesh.AllAreas)) start=pose.Position-surface.transform.forward*.35f;
            start.y=grid.position.y; var actor=Actor(start); Assert.That(world.Register(actor),Is.True,"wall candidate="+pose.Position+" actor="+start);
            var before=runtime.WallMountedLayout.CaptureSnapshot().Instances.Select(x=>x.InstanceId).ToArray(); var revision=adapter.Revision;
            Invoke(controller,"UpdateWallMountedProjection");
            Assert.That(session.ActivePreview.IsValid,Is.True);
            AssertWallFootprint(false);
            Assert.That(controller.TryConfirmPhase7Preview(),Is.False); Assert.That(session.ActivePreview,Is.Not.Null);
            Assert.That(runtime.WallMountedLayout.CaptureSnapshot().Instances.Select(x=>x.InstanceId),Is.EqualTo(before));
            Assert.That(Field<string>(controller,"currentEditingMessage"),Is.EqualTo(NavigationDecorationBridge.ActorOverlapMessage));
            Assert.That(controller.TryHandleSceneDrag(new DecorationTouchHit(DecorationTouchHitKind.WallSlot,
                surfaceId:surface.SurfaceId,wallSlotPosition:new WallSlotPosition(5,0))),Is.True);
            AssertWallFootprint(true); Assert.That(actor.transform.position,Is.EqualTo(start));
            Assert.That(controller.TryConfirmPhase7Preview(),Is.True); Assert.That(adapter.Revision,Is.EqualTo(revision+1));
            var id=runtime.WallMountedLayout.CaptureSnapshot().Instances.Single(x=>!before.Contains(x.InstanceId)).InstanceId;
            session.BeginExisting(id); Assert.That(session.BeginStoreConfirmation(),Is.True); Invoke(controller,"HandleStoreConfirmRequested");
            Assert.That(runtime.WallMountedLayout.CaptureSnapshot().Instances.Select(x=>x.InstanceId),Is.EqualTo(before)); Assert.That(adapter.Revision,Is.EqualTo(revision+2));
            }
            finally { definitions[original.DefinitionId]=original; Set(adapter,"wallDefinitions",definitions.Values.ToArray()); Object.Destroy(prefab); Object.Destroy(definition); }
            yield return null;
        }
        [UnityTest] public IEnumerator SameRevisionReleaseTerminatesOnceBeforeReentrantStart()
        {
            var actor=Actor(grid.TransformPoint(new Vector3(6,0,6))); Assert.That(world.Register(actor),Is.True);
            var position=actor.transform.position; var calls=0; MovementResult? dirty=null;
            world.Service.MoveTo(actor.ActorId,new NavigationTarget(grid.TransformPoint(new Vector3(6,0,2))),r=>
            { calls++; Assert.That(r.Status,Is.EqualTo(MovementStatus.LayoutChanged)); world.Service.MoveTo(actor.ActorId,new NavigationTarget(position),x=>dirty=x,new NavigationTarget(position)); });
            var revision=adapter.Revision; adapter.ReleaseOwnedNavigation(); adapter.ReleaseOwnedNavigation(); world.Step(1);
            Assert.That(adapter.Revision,Is.EqualTo(revision)); Assert.That(calls,Is.EqualTo(1)); Assert.That(dirty.Value.Reason,Is.EqualTo(NavigationFailure.LayoutUnavailable));
            Assert.That(dirty.Value.RetryCount,Is.Zero); Assert.That(actor.transform.position,Is.EqualTo(position)); yield return null;
        }
        [UnityTest] public IEnumerator NewActorCannotBindOnlyForeignFloor()
        {
            var foreign=ForeignFloor(out var instance); var start=grid.TransformPoint(new Vector3(9,0,6)); var actor=Actor(start);
            try
            {
                Assert.That(NavMesh.SamplePosition(start,out _,.05f,new NavMeshQueryFilter {agentTypeID=NavMesh.GetSettingsByIndex(0).agentTypeID,areaMask=NavMesh.AllAreas}),Is.True);
                Assert.That(world.Register(actor),Is.False,"Only foreign data covers this unchanged start");
                Assert.That(actor.Agent.enabled,Is.False); Assert.That(actor.transform.position,Is.EqualTo(start)); Assert.That(instance.valid,Is.True);
            }
            finally { instance.Remove(); Object.Destroy(foreign); }
            yield return null;
        }
        [UnityTest] public IEnumerator RebuildCannotRebindExistingActorToForeignFloor()
        {
            var start=grid.TransformPoint(new Vector3(6,0,6)); var actor=Actor(start); Assert.That(world.Register(actor),Is.True);
            var foreign=ForeignFloor(out var instance); var floor=Field<BoxCollider>(adapter,"floor"); floor.size=new Vector3(3,.2f,3);
            try
            {
                Assert.That(adapter.RebuildAndValidate().CanResume,Is.False,"Shrunk owned floor no longer covers actor");
                Assert.That(world.LayoutAvailable,Is.False); Assert.That(actor.Agent.enabled,Is.False); Assert.That(actor.transform.position,Is.EqualTo(start)); Assert.That(instance.valid,Is.True);
                Assert.That(NavMesh.SamplePosition(start,out _,.05f,new NavMeshQueryFilter {agentTypeID=NavMesh.GetSettingsByIndex(0).agentTypeID,areaMask=NavMesh.AllAreas}),Is.True);
            }
            finally { instance.Remove(); Object.Destroy(foreign); }
            yield return null;
        }
        NavMeshData ForeignFloor(out NavMeshDataInstance instance)
        {
            var settings=NavMesh.GetSettingsByIndex(0); settings.agentRadius=.45f; settings.agentHeight=1.3f;
            var data=NavMeshBuilder.BuildNavMeshData(settings,new System.Collections.Generic.List<NavMeshBuildSource>{new NavMeshBuildSource { shape=NavMeshBuildSourceShape.Box,
                size=new Vector3(16,.2f,16),transform=grid.localToWorldMatrix*Matrix4x4.Translate(new Vector3(4,-.1f,4))}},new Bounds(grid.TransformPoint(new Vector3(4,0,4)),new Vector3(20,8,20)),Vector3.zero,Quaternion.identity);
            instance=NavMesh.AddNavMeshData(data); return data;
        }
        [UnityTest] public IEnumerator RootDriftRejectsConfirmAsConfigurationErrorAndRepairRevalidates()
        {
            Select("furniture.counter.module.01"); var session=Field<DecorationSession>(controller,"session"); Refresh(session.MovePreview(new GridPosition(5,5)));
            var before=Snapshot(); var preview=session.ActivePreview; var revision=adapter.Revision;
            var formal=Field<Transform>(controller,"furnitureRepresentationRoot"); var original=formal.position;
            formal.position+=Vector3.right;
            var action=Object.FindFirstObjectByType<DecorationActionBarView>(); Button(action,"ConfirmButton").onClick.Invoke();
            Assert.That(Snapshot(),Is.EqualTo(before)); Assert.That(session.ActivePreview,Is.SameAs(preview)); Assert.That(world.LayoutAvailable,Is.False);
            var reason=Field<string>(controller,"currentEditingMessage"); Assert.That(reason,Does.Contain("representation roots")); Assert.That(reason,Is.Not.EqualTo(NavigationDecorationBridge.ActorOverlapMessage));
            formal.position=original;
            Assert.That(adapter.RebuildAndValidate().CanResume,Is.True); Assert.That(adapter.Revision,Is.EqualTo(revision)); Assert.That(world.LayoutAvailable,Is.True);
            yield return null;
        }
        void AssertFurnitureFootprint(bool valid)
        {
            var view=Field<GridHighlightView>(controller,"gridView");
            var root=Field<Transform>(view,"visualRoot");
            var fills=root.GetComponentsInChildren<Renderer>().Where(r=>r.name=="Fill" && r.transform.parent.name.StartsWith("FootprintCell_")).ToArray();
            Assert.That(fills,Is.Not.Empty);
            var theme=Field<AnimalCafeUiTheme>(controller,"uiTheme");
            foreach(var fill in fills)
            {
                AssertRendererColor(fill,valid?theme.Colors.Accent:theme.Colors.Destructive);
                Assert.That(fill.transform.parent.Find("GeometryMark/ValidDiamond").gameObject.activeSelf,Is.EqualTo(valid));
                Assert.That(fill.transform.parent.Find("GeometryMark/InvalidBarA").gameObject.activeSelf,Is.EqualTo(!valid));
                Assert.That(fill.transform.parent.Find("GeometryMark/InvalidBarB").gameObject.activeSelf,Is.EqualTo(!valid));
            }
        }
        void AssertMountedFootprint(bool valid)
        {
            var view=Field<SurfaceMountedPreviewView>(controller,"surfaceMountedPreviewView");
            Assert.That(view.CurrentFootprint,Is.Not.Null);
            var theme=Field<AnimalCafeUiTheme>(controller,"uiTheme");
            var renderers=view.CurrentFootprint.GetComponentsInChildren<Renderer>(); Assert.That(renderers,Is.Not.Empty);
            foreach(var renderer in renderers) AssertRendererColor(renderer,valid?theme.Colors.Accent:theme.Colors.Destructive);
        }
        void AssertWallFootprint(bool valid)
        {
            var view=Field<WallMountedPreviewView>(controller,"wallMountedProjectionView");
            Assert.That(view.CurrentProjection,Is.Not.Null);
            var material=Field<Material>(view,valid?"validMaterial":"invalidMaterial");
            var color=material.HasProperty("_BaseColor")?material.GetColor("_BaseColor"):material.color;
            AssertRendererColor(view.CurrentProjection.GetComponent<Renderer>(),color);
            Assert.That(view.CurrentProjection.name,Is.EqualTo(valid?"WallProjection_ValidCheck":"WallProjection_InvalidCross"));
        }
        static void AssertRendererColor(Renderer renderer,Color expected)
        {
            var block=new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
            var baseColor=block.GetColor("_BaseColor"); var color=block.GetColor("_Color");
            var materialColor=renderer.sharedMaterial.HasProperty("_BaseColor")
                ?renderer.sharedMaterial.GetColor("_BaseColor"):renderer.sharedMaterial.color;
            var actual=block.isEmpty?materialColor:baseColor;
            Assert.That(actual==expected || !block.isEmpty && color==expected,Is.True,
                renderer.name+" footprint color must be "+expected+", actual _BaseColor="+actual+", _Color="+color);
        }
        string[] Snapshot() => runtime.Layout.FurnitureInstances.Select(x=>x.InstanceId+"|"+x.DefinitionId+"|"+x.Position.X+","+x.Position.Y+"|"+x.Rotation).ToArray();
        NavigationActor Actor(Vector3 position)
        {
            var go=new GameObject("Task5 actor"); go.SetActive(false); go.transform.position=position;
            var agent=go.AddComponent<NavMeshAgent>(); agent.enabled=false; agent.radius=.45f; agent.height=1.3f;
            var capsule=go.AddComponent<CapsuleCollider>(); capsule.radius=.45f; capsule.height=1.3f; capsule.center=Vector3.up*.65f;
            var actor=go.AddComponent<NavigationActor>(); actor.Configure("task5-actor",agent,capsule,null,null); go.SetActive(true); return actor;
        }
        void Refresh(PlacementResult result) { Invoke(controller,"SyncActivePreviewPresentation"); Invoke(controller,"ShowActionForResult",result); }
        static void Select(string id) => Object.FindFirstObjectByType<DecorationCatalogueView>().GetComponentsInChildren<DecorationCatalogueTileView>(true).Single(x=>x.ItemId==id && x.gameObject.activeInHierarchy).GetComponent<Button>().onClick.Invoke();
        static Button Button(Component root,string name) => root.GetComponentsInChildren<Button>(true).Single(x=>x.name==name);
        static T Field<T>(object owner,string name) => (T)owner.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(owner);
        static void Set(object owner,string name,object value) => owner.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(owner,value);
        static void Invoke(object owner,string name,params object[] args) => owner.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(owner,args);
    }
}
#endif
