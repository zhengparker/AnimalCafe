using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AnimalCafe.Content;
using AnimalCafe.Decoration;
using AnimalCafe.Layout;
using AnimalCafe.Navigation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;
namespace AnimalCafe.Tests.EditMode.Phase11
{
    public sealed class NavigationLayoutTests
    {
        readonly List<GameObject> objects=new List<GameObject>();
        byte[] originalNavigationSettings;
        UnityEngine.Object navigationSettingsObject; string originalNavigationSettingsJson; bool navigationSettingsWasDirty;
        const string NavigationSettingsPath="ProjectSettings/NavMeshAreas.asset";
        NavigationLayoutAdapter adapter; NavigationWorld world; NavigationDecorationBridge bridge;
        CafeLayoutRuntime runtime; FurnitureContentCatalog catalog; BoxCollider floor; Transform grid;
        [SetUp] public void Setup()
        {
            // Unity persists CreateSettings in EditMode; restore the exact pre-test bytes in finally.
            originalNavigationSettings=System.IO.File.ReadAllBytes(NavigationSettingsPath);
            navigationSettingsObject=Unsupported.GetSerializedAssetInterfaceSingleton("NavMeshProjectSettings");
            originalNavigationSettingsJson=EditorJsonUtility.ToJson(navigationSettingsObject);
            navigationSettingsWasDirty=EditorUtility.IsDirty(navigationSettingsObject);
            grid=New("Grid").transform;
            catalog=AssetDatabase.LoadAssetAtPath<FurnitureContentCatalog>("Assets/Art/Phase6/Catalogues/FC_Phase6Production.asset");
            var entrance=New("Entrance").AddComponent<EntrancePortalAuthoring>(); Set(entrance,"entranceId","entrance.main"); Set(entrance,"originX",3);
            runtime=New("Runtime").AddComponent<CafeLayoutRuntime>(); Set(runtime,"contentCatalog",catalog); Set(runtime,"entrancePortal",entrance); runtime.Initialize();
            floor=New("Floor").AddComponent<BoxCollider>(); floor.center=new Vector3(4,-.1f,4); floor.size=new Vector3(8,.2f,8);
            world=New("World").AddComponent<NavigationWorld>(); world.enabled=false;
            adapter=New("Adapter").AddComponent<NavigationLayoutAdapter>(); adapter.Configure(runtime,catalog,grid,floor,Array.Empty<Collider>(),world,false);
            bridge=New("Bridge").AddComponent<NavigationDecorationBridge>(); bridge.Configure(adapter); ConfigureRoots(grid,grid);
        }
        [TearDown] public void Cleanup()
        {
            try
            {
                if(adapter!=null) { adapter.ReleaseOwnedNavigation(); Object.DestroyImmediate(adapter.gameObject); }
                if(world!=null) Object.DestroyImmediate(world.gameObject);
                foreach(var go in objects) if(go!=null) Object.DestroyImmediate(go); objects.Clear();
            }
            finally
            {
                // Restore the in-memory settings too, otherwise Unity's deferred save overwrites
                // the restored bytes after teardown. Preserve any pre-existing dirty state.
                if(navigationSettingsObject!=null)
                {
                    EditorJsonUtility.FromJsonOverwrite(originalNavigationSettingsJson,navigationSettingsObject);
                    if(navigationSettingsWasDirty) EditorUtility.SetDirty(navigationSettingsObject);
                    else EditorUtility.ClearDirty(navigationSettingsObject);
                }
                if(originalNavigationSettings!=null) System.IO.File.WriteAllBytes(NavigationSettingsPath,originalNavigationSettings);
            }
        }
        [Test] public void PreviewDoesNotChangeRevision()
        {
            adapter.RefreshConfirmedLayout(); var before=adapter.Revision;
            var session=new DecorationSession(runtime.Layout); session.Enter(); session.BeginExisting(runtime.Layout.FurnitureInstances.Single().InstanceId);
            session.MovePreview(new GridPosition(5,4)); session.RotatePreview();
            // A hidden representation and a ghost have no authority over the confirmed domain.
            var ghost=New("Preview"); ghost.AddComponent<BoxCollider>(); ghost.transform.position=new Vector3(6,0,4); ghost.SetActive(false);
            Assert.That(adapter.RefreshConfirmedLayout(),Is.False); Assert.That(adapter.Revision,Is.EqualTo(before));
        }
        [Test] public void AppearanceDoesNotRebuild()
        {
            Assert.That(adapter.RebuildAndValidate().CanResume,Is.True); var revision=adapter.Revision; var builds=adapter.BakeCount;
            runtime.RecalculateReadiness(); New("Floor appearance").AddComponent<BoxCollider>();
            Assert.That(adapter.RefreshConfirmedLayout(),Is.False); Assert.That(adapter.RebuildAndValidate().CanResume,Is.True);
            Assert.That(adapter.Revision,Is.EqualTo(revision)); Assert.That(adapter.BakeCount,Is.EqualTo(builds));
        }
        [Test] public void ConfirmAndStoreChangeRevisionExactlyOnce()
        {
            adapter.RefreshConfirmedLayout(); var before=adapter.Revision; Commit(new GridPosition(0,4));
            Assert.That(adapter.RefreshConfirmedLayout(),Is.True); Assert.That(adapter.Revision,Is.EqualTo(before+1)); Assert.That(adapter.RefreshConfirmedLayout(),Is.False);
            var session=new DecorationSession(runtime.Layout); session.Enter(); session.BeginExisting(runtime.Layout.FurnitureInstances.Single().InstanceId); session.BeginStoreConfirmation();
            Assert.That(session.ConfirmStore().Succeeded,Is.True); Assert.That(adapter.RefreshConfirmedLayout(),Is.True); Assert.That(adapter.Revision,Is.EqualTo(before+2));
        }
        [Test] public void ObsoleteBakeCannotPublish()
        {
            adapter.RefreshConfirmedLayout(); var old=adapter.Revision;
            Set(adapter,"BeforePublish",(Action)(()=> { Set(adapter,"BeforePublish",null); Commit(new GridPosition(0,4)); adapter.RefreshConfirmedLayout(); }));
            Assert.That(adapter.RebuildAndValidate().CanResume,Is.False); Assert.That(adapter.PublishedRevision,Is.EqualTo(-1));
            Assert.That(adapter.Revision,Is.EqualTo(old+1)); Assert.That(world.LayoutAvailable,Is.False);
            Assert.That(adapter.RebuildAndValidate().CanResume,Is.True);
        }
        [Test] public void RebuildRemovesOnlyOwnedNavMesh()
        {
            var settingsCount=NavMesh.GetSettingsCount();
            var settings=NavMesh.GetSettingsByIndex(0); settings.agentRadius=.45f;
            var data=NavMeshBuilder.BuildNavMeshData(settings,new List<NavMeshBuildSource>{new NavMeshBuildSource { shape=NavMeshBuildSourceShape.Box, size=new Vector3(4,.2f,4),transform=Matrix4x4.TRS(new Vector3(30,-.1f,30),Quaternion.identity,Vector3.one)}},new Bounds(new Vector3(30,0,30),new Vector3(8,4,8)),Vector3.zero,Quaternion.identity);
            var instance=NavMesh.AddNavMeshData(data);
            try { adapter.RebuildAndValidate(); Commit(new GridPosition(0,4)); adapter.RebuildAndValidate(); adapter.ReleaseOwnedNavigation(); Object.DestroyImmediate(adapter.gameObject); Assert.That(instance.valid,Is.True); Assert.That(NavMesh.GetSettingsCount(),Is.EqualTo(settingsCount)); Assert.That(NavMesh.SamplePosition(new Vector3(30,0,30),out _,.2f,NavMesh.AllAreas),Is.True); }
            finally { instance.Remove(); Object.DestroyImmediate(data); }
        }
        [Test] public void LayoutUnavailableNeverRetriesOrRecovers()
        {
            var service=new NavigationService(new NavigationSettings()); var driver=new FakeNavigationDriver { BeginFailure=NavigationFailure.LayoutUnavailable }; service.Register("a",driver);
            MovementResult? result=null; service.MoveTo("a",new NavigationTarget(Vector3.right),r=>result=r,new NavigationTarget(Vector3.left));
            Assert.That(driver.BeginPositions.Count,Is.EqualTo(1)); Assert.That(result.Value.RetryCount,Is.Zero); Assert.That(result.Value.Reason,Is.EqualTo(NavigationFailure.LayoutUnavailable));
        }
        [TestCase(false)] [TestCase(true)] public void OptionalBlockedStationPassesButEssentialConnectionFails(bool essentialBreak)
        {
            // Use actual P8 report constructors with explicit valid station anchors; real NavMesh decides connectivity.
            var stations=new[]{ Station("cash",LayoutStationType.CashRegister,1,2),Station("coffee",LayoutStationType.CoffeeMachine,1,5),Station("pickup",LayoutStationType.PickUpPoint,2,5),Station("optional",LayoutStationType.CoffeeMachine,6,5) };
            var summary=Construct<LayoutReadinessSummary>(1,1);
            Set(runtime,"currentReadiness",Construct<LayoutReadinessReport>(true,stations,Array.Empty<LayoutReadinessFailure>(),summary,summary,summary));
            var wall=New("Break").AddComponent<BoxCollider>(); wall.center=essentialBreak?new Vector3(4,.75f,3.5f):new Vector3(6.5f,.75f,5.5f); wall.size=essentialBreak?new Vector3(10,1.5f,.2f):new Vector3(.6f,1.5f,.6f);
            adapter.Configure(runtime,catalog,grid,floor,new Collider[]{wall},world,true);
            var ready=adapter.RebuildAndValidate(); var diagnostic=new NavMeshPath(); NavMesh.CalculatePath(new Vector3(1.5f,0,2.5f),new Vector3(1.5f,0,5.5f),NavMesh.AllAreas,diagnostic); Assert.That(ready.CanResume,Is.EqualTo(!essentialBreak),ready.Reason+" solids="+string.Join(";",((System.Collections.Generic.List<NavigationSolidPose>)typeof(NavigationLayoutAdapter).GetField("snapshot",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(adapter)).Select(p=>p.FurnitureId+":"+p.Position+":"+p.Size))+" wall="+wall.bounds+" path="+string.Join(";",diagnostic.corners.Select(p=>p.ToString())));
            Assert.That(ready.StationFailures,Does.ContainKey(essentialBreak?"cash":"optional"));
        }
        [Test] public void OverlappingForeignFloorCannotRestoreEssentialConnection()
        {
            var settings=NavMesh.GetSettingsByIndex(0); settings.agentRadius=.45f; settings.agentHeight=1.3f; settings.agentClimb=.1f;
            settings.overrideVoxelSize=true; settings.voxelSize=.025f; settings.overrideTileSize=true; settings.tileSize=128; settings.minRegionArea=.01f;
            var foreign=NavMeshBuilder.BuildNavMeshData(settings,new List<NavMeshBuildSource>{new NavMeshBuildSource { shape=NavMeshBuildSourceShape.Box,
                size=new Vector3(8,.2f,8),transform=Matrix4x4.TRS(new Vector3(4,-.1f,4),Quaternion.identity,Vector3.one)}},new Bounds(new Vector3(4,-.1f,4),new Vector3(10,8.2f,10)),Vector3.zero,Quaternion.identity);
            var instance=NavMesh.AddNavMeshData(foreign);
            try
            {
                // Foreign floor spans both sides of the wall but must not make this owned bake ready.
                OptionalBlockedStationPassesButEssentialConnectionFails(true);
                Assert.That(instance.valid,Is.True);
                var from=new Vector3(1.5f,0,2.5f); var to=new Vector3(1.5f,0,5.5f);
                var foreignPath=new NavMeshPath();
                Assert.That(NavMesh.CalculatePath(from,to,new NavMeshQueryFilter {agentTypeID=settings.agentTypeID,areaMask=NavMesh.AllAreas},foreignPath),Is.True);
                Assert.That(foreignPath.status,Is.EqualTo(NavMeshPathStatus.PathComplete));
                var ownedFilter=(NavMeshQueryFilter)typeof(NavigationLayoutAdapter).GetProperty("OwnedFilter",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(adapter);
                var ownedPath=new NavMeshPath(); NavMesh.CalculatePath(from,to,ownedFilter,ownedPath);
                Assert.That(ownedFilter.agentTypeID,Is.Not.EqualTo(settings.agentTypeID));
                Assert.That(ownedPath.status,Is.Not.EqualTo(NavMeshPathStatus.PathComplete));
                Assert.That(NavMesh.SamplePosition(new Vector3(1.5f,0,3.5f),out _,.2f,new NavMeshQueryFilter {agentTypeID=settings.agentTypeID,areaMask=NavMesh.AllAreas}),Is.True);
            }
            finally { instance.Remove(); Object.DestroyImmediate(foreign); }
        }
        [TestCase("missing")] [TestCase("furniture")] [TestCase("mounted")] [TestCase("nonuniform")]
        public void UnverifiableRepresentationRootsFailClosed(string scenario)
        {
            var furniture=New("Formal furniture root").transform; var mounted=New("Formal mounted root").transform;
            if(scenario=="furniture") furniture.position=Vector3.right;
            if(scenario=="mounted") mounted.rotation=Quaternion.Euler(0,20,0);
            if(scenario=="nonuniform") { grid.localScale=new Vector3(2,1,1); furniture.localScale=grid.localScale; mounted.localScale=grid.localScale; }
            ConfigureRoots(scenario=="missing"?null:furniture,scenario=="missing"?null:mounted);
            var ready=adapter.RebuildAndValidate();
            Assert.That(ready.CanResume,Is.False); Assert.That(world.LayoutAvailable,Is.False); Assert.That(adapter.PublishedRevision,Is.EqualTo(-1));
        }
        [Test] public void CommonTranslatedYawRootsMatchRealPrefabPose()
        {
            grid.SetPositionAndRotation(new Vector3(20,0,10),Quaternion.Euler(0,90,0));
            floor.transform.SetPositionAndRotation(grid.position,grid.rotation);
            var furniture=New("Formal furniture root").transform; furniture.SetPositionAndRotation(grid.position,grid.rotation);
            var mounted=New("Formal mounted root").transform; mounted.SetPositionAndRotation(grid.position,grid.rotation); ConfigureRoots(furniture,mounted);
            Assert.That(adapter.RebuildAndValidate().CanResume,Is.True);
            var item=runtime.Layout.FurnitureInstances.Single(); var session=new DecorationSession(runtime.Layout); session.Enter(); session.BeginExisting(item.InstanceId);
            var candidate=adapter.FurnitureCandidate(session.ActivePreview).First(); catalog.TryGetDefinitionAsset(item.DefinitionId,out var definition);
            var actual=Object.Instantiate(definition.Prefab,furniture,false); objects.Add(actual);
            var space=new DecorationGridSpace(runtime.Layout.GridSettings,new LayoutBounds(new GridPosition(0,0),new GridSize(8,8)));
            actual.transform.localPosition=space.GetFootprintCenterLocal(runtime.Layout.GetFurnitureFootprintCells(item.DefinitionId,item.Position,item.Rotation));
            actual.transform.localRotation=space.GetLocalRotation(item.Rotation); actual.transform.localScale=definition.Prefab.transform.localScale;
            var collider=actual.GetComponentsInChildren<BoxCollider>(true).First();
            Assert.That(Vector3.Distance(candidate.Position,collider.transform.TransformPoint(collider.center)),Is.LessThan(.0001f));
            Assert.That(Quaternion.Angle(candidate.Rotation,collider.transform.rotation),Is.LessThan(.001f));
        }
        void ConfigureRoots(Transform furniture,Transform mounted)
            => adapter.ConfigureRepresentationRoots(furniture,mounted);
        StationReadiness Station(string id,LayoutStationType type,int x,int y)
        {
            var anchors=new[]{new InteractionAnchor(InteractionRole.Employee,new GridPosition(x,y),CardinalDirection.North),new InteractionAnchor(InteractionRole.Customer,new GridPosition(x,y),CardinalDirection.South)};
            return Construct<StationReadiness>(type,id,"support","slot",new ResolvedStationAnchors(anchors),new[]{0},Array.Empty<LayoutReadinessFailure>());
        }
        static T Construct<T>(params object[] args) => (T)Activator.CreateInstance(typeof(T),BindingFlags.Instance|BindingFlags.NonPublic,null,args,null);
        string[] Snapshot() => runtime.Layout.FurnitureInstances.Select(x=>x.InstanceId+"|"+x.DefinitionId+"|"+x.Position.X+","+x.Position.Y+"|"+x.Rotation).ToArray();
        void Commit(GridPosition position) { var session=new DecorationSession(runtime.Layout); session.Enter(); session.BeginExisting(runtime.Layout.FurnitureInstances.Single().InstanceId); Assert.That(session.MovePreview(position).Succeeded,Is.True); Assert.That(session.ConfirmPreview().Succeeded,Is.True); runtime.RecalculateReadiness(); }
        GameObject New(string name) { var go=new GameObject(name); objects.Add(go); return go; }
        static void Set(object owner,string name,object value) => owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(owner,value);
    }
}
