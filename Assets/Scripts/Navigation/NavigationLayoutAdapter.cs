using System;
using System.Collections.Generic;
using System.Linq;
using AnimalCafe.Content;
using AnimalCafe.Decoration;
using AnimalCafe.Layout;
using UnityEngine;
using UnityEngine.AI;

namespace AnimalCafe.Navigation
{
    // Detached world-space geometry: preview GameObjects are never authoritative.
    public readonly struct NavigationSolidPose : IEquatable<NavigationSolidPose>
    {
        public string FurnitureId { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public Vector3 Size { get; }
        public NavigationSolidPose(string furnitureId, Vector3 position, Quaternion rotation, Vector3 size)
        { FurnitureId = furnitureId; Position = position; Rotation = rotation; Size = size; }
        public bool Equals(NavigationSolidPose other) => FurnitureId == other.FurnitureId && Position.Equals(other.Position) && Rotation.Equals(other.Rotation) && Size.Equals(other.Size);
        public override bool Equals(object obj) => obj is NavigationSolidPose other && Equals(other);
        public override int GetHashCode() => Position.GetHashCode();
    }
    public sealed class NavigationReadiness
    {
        public bool CanResume { get; }
        public IReadOnlyDictionary<string, string> StationFailures { get; }
        public string Reason { get; }
        public NavigationReadiness(bool canResume, IDictionary<string,string> failures, string reason)
        { CanResume = canResume; StationFailures = new System.Collections.ObjectModel.ReadOnlyDictionary<string,string>(new Dictionary<string,string>(failures)); Reason = reason; }
    }

    public sealed class NavigationLayoutAdapter : MonoBehaviour
    {
        [SerializeField] private CafeLayoutRuntime layoutRuntime;
        [SerializeField] private FurnitureContentCatalog contentCatalog;
        [SerializeField] private Transform gridRoot;
        [SerializeField] private Transform furnitureRepresentationRoot;
        [SerializeField] private Transform surfaceMountedRepresentationRoot;
        [SerializeField] private BoxCollider floor;
        [SerializeField] private Collider[] walls = Array.Empty<Collider>();
        [SerializeField] private Transform[] confirmedFurnitureRoots = Array.Empty<Transform>();
        [SerializeField] private WallMountedDefinitionAsset[] wallDefinitions = Array.Empty<WallMountedDefinitionAsset>();
        [SerializeField] private WallSurfaceAuthoring[] wallAuthoring = Array.Empty<WallSurfaceAuthoring>();
        [SerializeField] private NavigationWorld world;
        [SerializeField] private bool enforceBusinessReadiness = true;
        private List<NavigationSolidPose> snapshot;
        private List<string> structure;
        private NavMeshData ownedData;
        private int? ownedAgentType;
        private NavMeshQueryFilter OwnedFilter => new NavMeshQueryFilter { agentTypeID=ownedAgentType ?? -1, areaMask=NavMesh.AllAreas };
        private NavMeshDataInstance ownedInstance;
        private GameObject geometryRoot;
        private bool rebuilding;
        private string captureFailure;
        public int Revision { get; private set; }
        public int PublishedRevision { get; private set; } = -1;
        public int BakeCount { get; private set; }
        public NavigationWorld World => world;
        public bool EnforceBusinessReadiness => enforceBusinessReadiness;
        public NavigationReadiness CurrentReadiness { get; private set; } = Result(false, "LayoutUnavailable");
        private DecorationGridSpace Grid => new DecorationGridSpace(layoutRuntime.Layout.GridSettings, new LayoutBounds(new GridPosition(0,0),new GridSize(8,8)));
        // A synchronous builder today; revision is still checked at publication, including reentrant callers.
        internal Action BeforePublish;

        public void Configure(CafeLayoutRuntime runtime, FurnitureContentCatalog catalog, Transform root,
            BoxCollider ground, Collider[] boundary, NavigationWorld navigationWorld, bool requireBusiness,
            WallMountedDefinitionAsset[] mountedDefinitions = null, WallSurfaceAuthoring[] authoredWalls = null,
            Transform[] fixedConfirmedRoots = null)
        {
            layoutRuntime=runtime; contentCatalog=catalog; gridRoot=root; floor=ground; walls=boundary ?? Array.Empty<Collider>();
            world=navigationWorld; enforceBusinessReadiness=requireBusiness;
            wallDefinitions=mountedDefinitions ?? Array.Empty<WallMountedDefinitionAsset>(); wallAuthoring=authoredWalls ?? Array.Empty<WallSurfaceAuthoring>();
            confirmedFurnitureRoots=fixedConfirmedRoots ?? Array.Empty<Transform>();
        }
        // Explicit roots from the same controller/registries; call before the first refresh/Confirm.
        public void ConfigureRepresentationRoots(Transform furnitureRoot, Transform mountedRoot)
        {
            furnitureRepresentationRoot=furnitureRoot; surfaceMountedRepresentationRoot=mountedRoot;
            if(snapshot!=null) RefreshConfirmedLayout();
        }
        internal bool ValidateRepresentationRoots(out string reason)
        {
            if(CheckRepresentationRoots(out reason)) return true;
            captureFailure=reason; PublishedRevision=-1; CurrentReadiness=Result(false,reason); world?.SuspendLayout(Revision);
            return false;
        }
        internal bool CheckRepresentationRoots(out string reason)
        {
            reason="";
            if(layoutRuntime==null) return true; // Geometry-only authored fixtures have no domain views.
            if(gridRoot==null || furnitureRepresentationRoot==null || surfaceMountedRepresentationRoot==null)
                reason="Navigation representation root configuration is missing";
            else
            {
                var grid=gridRoot.localToWorldMatrix;
                // Existing placement/anchor contracts are one metre, upright and unscaled.
                // Reject scale/shear rather than approximating a different formal registry pose.
                if(!UprightUnitMatrix(grid) || !SameMatrix(grid,furnitureRepresentationRoot.localToWorldMatrix) ||
                    !SameMatrix(grid,surfaceMountedRepresentationRoot.localToWorldMatrix))
                    reason="Navigation representation roots must match gridRoot (translation/yaw, unit scale)";
            }
            return reason.Length==0;
        }
        internal bool ValidateBusinessConfiguration(out string reason)
        {
            reason="";
            if(!isActiveAndEnabled) reason="Navigation adapter is disabled";
            else if(world==null || !world.isActiveAndEnabled || floor==null || !floor.enabled || !floor.gameObject.activeInHierarchy)
                reason="Navigation world or floor configuration is unavailable";
            else if(!enforceBusinessReadiness || layoutRuntime==null || layoutRuntime.Layout==null ||
                layoutRuntime.FunctionalSurfaceLayout==null || contentCatalog==null || gridRoot==null)
                reason="Navigation business configuration is unavailable";
            // Pure check: bridge holds the time block before suspending callback-producing requests.
            return reason.Length==0 && CheckRepresentationRoots(out reason);
        }
        private static bool UprightUnitMatrix(Matrix4x4 matrix)
        {
            var x=matrix.MultiplyVector(Vector3.right); var y=matrix.MultiplyVector(Vector3.up); var z=matrix.MultiplyVector(Vector3.forward);
            const float tolerance=.00001f;
            return Mathf.Abs(x.magnitude-1)<tolerance && Mathf.Abs(z.magnitude-1)<tolerance &&
                Vector3.Distance(y,Vector3.up)<tolerance && Mathf.Abs(Vector3.Dot(x,z))<tolerance &&
                Vector3.Distance(Vector3.Cross(x,y),z)<tolerance;
        }
        private static bool SameMatrix(Matrix4x4 a,Matrix4x4 b)
        {
            for(var i=0;i<16;i++) if(Mathf.Abs(a[i]-b[i])>.00001f) return false;
            return true;
        }
        public bool RefreshConfirmedLayout()
        {
            if (!ValidateRepresentationRoots(out _)) return false;
            if (world == null || floor == null) { captureFailure="LayoutUnavailable"; world?.SuspendLayout(Revision); CurrentReadiness=Result(false,captureFailure); return false; }
            var nextStructure = new List<string>(); List<NavigationSolidPose> next;
            try { next=Capture(nextStructure); captureFailure=null; }
            catch(InvalidOperationException exception)
            { captureFailure=exception.Message; CurrentReadiness=Result(false,captureFailure); world.SuspendLayout(Revision); return false; }
            if (snapshot != null && snapshot.SequenceEqual(next) && structure.SequenceEqual(nextStructure)) return false;
            snapshot=next; structure=nextStructure; Revision=checked(Revision+1);
            CurrentReadiness=Result(false,"LayoutUnavailable");
            world.SuspendLayout(Revision); // BEFORE completion callbacks can submit new requests.
            return true;
        }
        public NavigationReadiness RebuildAndValidate()
        {
            if (rebuilding) return Result(false,"Rebuild already in progress");
            RefreshConfirmedLayout();
            if (captureFailure != null || snapshot == null || world == null || floor == null) return CurrentReadiness=Result(false,captureFailure ?? "LayoutUnavailable");
            // A failed actor/configuration validation may be repaired without a layout mutation.
            // Only successful publications may use the cached readiness result.
            if (PublishedRevision == Revision && CurrentReadiness.CanResume) return CurrentReadiness;
            rebuilding=true;
            try
            {
                world.SuspendLayout(Revision);
                var revision=Revision; var captured=snapshot.ToArray();
                var sources=new List<NavMeshBuildSource>();
                for (var i=0;i<captured.Length;i++) sources.Add(new NavMeshBuildSource { shape=NavMeshBuildSourceShape.Box,
                    transform=Matrix4x4.TRS(captured[i].Position,captured[i].Rotation,Vector3.one), size=captured[i].Size, area=0 });
                // Each adapter has its own runtime agent type. Static and Agent queries cannot
                // accidentally borrow an overlapping NavMesh belonging to another world.
                if(!ownedAgentType.HasValue) ownedAgentType=NavMesh.CreateSettings().agentTypeID;
                var settings=NavMesh.GetSettingsByID(ownedAgentType.Value);
                world.SetOwnedAgentType(ownedAgentType.Value);
                settings.agentRadius=.45f; settings.agentHeight=1.30f; settings.agentClimb=.1f;
                settings.overrideVoxelSize=true; settings.voxelSize=.025f; settings.overrideTileSize=true; settings.tileSize=128; settings.minRegionArea=.01f;
                var bounds=floor.bounds; bounds.Expand(new Vector3(2,8,2));
                var candidate=NavMeshBuilder.BuildNavMeshData(settings,sources,bounds,Vector3.zero,Quaternion.identity); BakeCount++;
                if(candidate==null) return CurrentReadiness=Result(false,"NavMesh build failed");
                BeforePublish?.Invoke(); RefreshConfirmedLayout();
                if(revision!=Revision || captureFailure!=null) { DestroyOwned(candidate); return CurrentReadiness=Result(false,captureFailure ?? "Obsolete bake discarded"); }
                // Drivers were disabled before removing our data. Never remove another world's instance.
                if(ownedInstance.valid) ownedInstance.Remove(); if(ownedData!=null) DestroyOwned(ownedData);
                ownedData=candidate; ownedInstance=NavMesh.AddNavMeshData(candidate);
                if(geometryRoot!=null) { geometryRoot.SetActive(false); DestroyOwned(geometryRoot); }
                geometryRoot=new GameObject("Navigation confirmed solids"); geometryRoot.hideFlags=HideFlags.HideInHierarchy;
                var colliders=new List<Collider>();
                foreach(var pose in captured.Skip(1))
                {
                    var go=new GameObject(pose.FurnitureId); go.layer=2; go.transform.SetParent(geometryRoot.transform);
                    go.transform.SetPositionAndRotation(pose.Position,pose.Rotation);
                    var box=go.AddComponent<BoxCollider>(); box.size=pose.Size; colliders.Add(box);
                }
                Physics.SyncTransforms(); world.SetGeometry(colliders,revision);
                var failures=new Dictionary<string,string>();
                var actorsValid=world.ValidateAndRebindActors(out var actorReason);
                var businessValid=!enforceBusinessReadiness || ValidateBusiness(failures);
                var canResume=actorsValid && businessValid;
                CurrentReadiness=new NavigationReadiness(canResume,failures,!actorsValid?actorReason:!businessValid?"No reachable essential service combination":"");
                PublishedRevision=revision;
                world.SetLayoutAvailable(canResume);
                return CurrentReadiness;
            }
            finally { rebuilding=false; }
        }

        public IReadOnlyList<NavigationSolidPose> FurnitureCandidate(FurniturePlacementPreview preview)
        {
            var result=new List<NavigationSolidPose>(); if(preview==null) return result;
            var pose=FurnitureMatrix(preview.DefinitionId,preview.ProposedPosition,preview.ProposedRotation);
            AddFurniture(result,preview.SourceInstanceId ?? "candidate",preview.DefinitionId,pose);
            if(layoutRuntime.FunctionalSurfaceLayout!=null)
                foreach(var item in layoutRuntime.FunctionalSurfaceLayout.MountedInstances.Where(x=>x.Address.SupportFurnitureInstanceId==preview.SourceInstanceId))
                    AddMounted(result,item.InstanceId,item.DefinitionId,item.Address,item.Rotation,pose,preview.DefinitionId);
            return result;
        }
        public IReadOnlyList<NavigationSolidPose> MountedCandidate(FunctionalSurfacePlacementPreview preview)
        {
            var result=new List<NavigationSolidPose>();
            if(preview!=null && preview.Kind==FunctionalSurfacePreviewKind.MountedEquipment)
                AddMounted(result,preview.InstanceId ?? "candidate",preview.DefinitionId,preview.Address,preview.Rotation);
            return result;
        }
        public IReadOnlyList<NavigationSolidPose> WallCandidate(WallMountedPlacementPreview preview)
        {
            var result=new List<NavigationSolidPose>();
            if(preview!=null) AddWall(result,preview.InstanceId ?? "candidate",preview.DefinitionId,preview.SurfaceId,preview.Position,preview.Footprint);
            return result;
        }
        private List<NavigationSolidPose> Capture(List<string> structural)
        {
            var result=new List<NavigationSolidPose>(); AddCollider(result,"floor",floor,floor.transform.localToWorldMatrix);
            foreach(var wall in walls) if(wall!=null && wall.enabled && !wall.isTrigger) AddCollider(result,"wall:"+wall.GetEntityId(),wall,wall.transform.localToWorldMatrix);
            foreach(var root in confirmedFurnitureRoots) if(root!=null) AddPrefab(result,"fixed:"+root.GetEntityId(),root.gameObject,root.localToWorldMatrix);
            if(layoutRuntime?.Layout==null) return result;
            foreach(var item in layoutRuntime.Layout.FurnitureInstances.OrderBy(x=>x.InstanceId,StringComparer.Ordinal))
            {
                var start=result.Count; AddFurniture(result,item.InstanceId,item.DefinitionId,FurnitureMatrix(item.DefinitionId,item.Position,item.Rotation));
                if(result.Count>start) structural.Add(item.InstanceId+"|"+item.DefinitionId+"|"+item.Position+"|"+item.Rotation);
            }
            if(layoutRuntime.FunctionalSurfaceLayout!=null)
                foreach(var item in layoutRuntime.FunctionalSurfaceLayout.MountedInstances.OrderBy(x=>x.InstanceId,StringComparer.Ordinal))
                { AddMounted(result,item.InstanceId,item.DefinitionId,item.Address,item.Rotation); structural.Add(item.InstanceId+"|"+item.DefinitionId+"|"+item.Address.SupportFurnitureInstanceId+"|"+item.Address.SlotId+"|"+item.Rotation); }
            if(layoutRuntime.WallMountedLayout!=null)
                foreach(var item in layoutRuntime.WallMountedLayout.Surfaces.Values.SelectMany(x=>x.MountedItems).OrderBy(x=>x.InstanceId,StringComparer.Ordinal))
                { var start=result.Count; AddWall(result,item.InstanceId,item.DefinitionId,item.SurfaceId,item.Position,item.Footprint); if(result.Count>start) structural.Add(item.InstanceId+"|"+item.DefinitionId+"|"+item.SurfaceId+"|"+item.Position); }
            if(layoutRuntime.CurrentReadiness!=null)
                foreach(var station in layoutRuntime.CurrentReadiness.Stations.OrderBy(x=>x.InstanceId,StringComparer.Ordinal))
                {
                    structural.Add(station.InstanceId+"|"+station.FunctionType+"|"+station.IsValid);
                    foreach(var anchor in station.Anchors.Anchors.OrderBy(x=>x.Role))
                        structural.Add(station.InstanceId+"|"+anchor.Role+"|"+anchor.Position.X+","+anchor.Position.Y+"|"+anchor.Facing+"|"+WorldAnchor(anchor).ToString("R"));
                }
            return result;
        }
        private Matrix4x4 FurnitureMatrix(string definitionId,GridPosition position,FurnitureRotation rotation)
        {
            if(contentCatalog==null || !contentCatalog.TryGetDefinitionAsset(definitionId,out var definition) || definition.Prefab==null)
                throw new InvalidOperationException("Missing furniture geometry: "+definitionId);
            var cells=layoutRuntime.Layout.GetFurnitureFootprintCells(definitionId,position,rotation);
            return gridRoot.localToWorldMatrix*Matrix4x4.TRS(Grid.GetFootprintCenterLocal(cells),Grid.GetLocalRotation(rotation),definition.Prefab.transform.localScale);
        }
        private void AddFurniture(List<NavigationSolidPose> result,string id,string definitionId,Matrix4x4 pose)
        {
            if(!contentCatalog.TryGetDefinitionAsset(definitionId,out var definition) || definition.Prefab==null) throw new InvalidOperationException("Missing furniture geometry: "+definitionId);
            AddPrefab(result,id,definition.Prefab,pose);
        }
        private void AddMounted(List<NavigationSolidPose> result,string id,string definitionId,SurfaceSlotAddress address,FurnitureRotation rotation,Matrix4x4? supportPose=null,string supportDefinition=null)
        {
            if(!layoutRuntime.Layout.TryGetFurnitureInstance(address.SupportFurnitureInstanceId,out var support)) throw new InvalidOperationException("Missing support: "+id);
            supportDefinition=supportDefinition ?? support.DefinitionId;
            if(!contentCatalog.TryGetDefinitionAsset(supportDefinition,out var definition)) throw new InvalidOperationException("Missing support definition");
            var slot=definition.Prefab.GetComponentsInChildren<SurfaceSlotMarker>(true).Single(x=>x.SlotId==address.SlotId).transform;
            var matrix=supportPose ?? FurnitureMatrix(support.DefinitionId,support.Position,support.Rotation);
            var local=definition.Prefab.transform.worldToLocalMatrix.MultiplyPoint3x4(slot.position);
            if(!contentCatalog.TryGetDefinitionAsset(definitionId,out var equipment) || equipment.Prefab==null) throw new InvalidOperationException("Missing mounted geometry: "+definitionId);
            AddPrefab(result,id,equipment.Prefab,Matrix4x4.TRS(matrix.MultiplyPoint3x4(local),matrix.rotation*Grid.GetLocalRotation(rotation),Vector3.Scale(gridRoot.lossyScale,equipment.Prefab.transform.localScale)));
        }
        private void AddWall(List<NavigationSolidPose> result,string id,string definitionId,string surfaceId,WallSlotPosition position,WallFootprint footprint)
        {
            var definition=wallDefinitions.SingleOrDefault(x=>x!=null && x.DefinitionId==definitionId);
            var wall=wallAuthoring.SingleOrDefault(x=>x!=null && x.SurfaceId==surfaceId);
            if(definition==null || definition.Prefab==null || wall==null) throw new InvalidOperationException("Missing wall geometry: "+id);
            var local=new Vector3((position.Column+footprint.Width*.5f)*wall.SlotSize-wall.Columns*wall.SlotSize*.5f,position.Row*wall.SlotSize,0);
            var rotation=wall.transform.rotation*Quaternion.Euler(0,180,0);
            AddPrefab(result,id,definition.Prefab,Matrix4x4.TRS(wall.GetWallMountedWorldPosition(local,WallSurfaceAuthoring.WallMountedPlaneEpsilon),rotation,wall.transform.lossyScale));
        }
        private static void AddPrefab(List<NavigationSolidPose> result,string id,GameObject prefab,Matrix4x4 rootPose)
        {
            foreach(var collider in prefab.GetComponentsInChildren<Collider>(true))
                if(collider.enabled && !collider.isTrigger)
                    AddCollider(result,id,collider,rootPose*prefab.transform.worldToLocalMatrix*collider.transform.localToWorldMatrix);
        }
        private static void AddCollider(List<NavigationSolidPose> result,string id,Collider collider,Matrix4x4 matrix)
        {
            Vector3 center,size;
            if(collider is BoxCollider box) { center=box.center; size=box.size; }
            else if(collider is SphereCollider sphere)
            {
                var scale=matrix.lossyScale; var radius=sphere.radius*Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z));
                result.Add(new NavigationSolidPose(id,matrix.MultiplyPoint3x4(sphere.center),Quaternion.identity,Vector3.one*radius*2)); return;
            }
            else if(collider is CapsuleCollider capsule)
            {
                var scale=matrix.lossyScale; scale=new Vector3(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z));
                var radial=Mathf.Max(scale[(capsule.direction+1)%3],scale[(capsule.direction+2)%3])*capsule.radius;
                size=Vector3.one*radial*2; size[capsule.direction]=Mathf.Max(capsule.height*scale[capsule.direction],radial*2);
                result.Add(new NavigationSolidPose(id,matrix.MultiplyPoint3x4(capsule.center),matrix.rotation,size)); return;
            }
            else if(collider is MeshCollider mesh && mesh.sharedMesh!=null) { center=mesh.sharedMesh.bounds.center; size=mesh.sharedMesh.bounds.size; }
            else throw new InvalidOperationException("Unsupported confirmed collider: "+collider.GetType().Name);
            // Conservative transformed bounds also cover nonuniform scale/shear; yaw boxes stay exact.
            var rotation=matrix.rotation; var inverse=Quaternion.Inverse(rotation); var extent=Vector3.zero;
            for(var x=-1;x<=1;x+=2) for(var y=-1;y<=1;y+=2) for(var z=-1;z<=1;z+=2)
            { var v=inverse*matrix.MultiplyVector(Vector3.Scale(size*.5f,new Vector3(x,y,z))); extent=Vector3.Max(extent,new Vector3(Mathf.Abs(v.x),Mathf.Abs(v.y),Mathf.Abs(v.z))); }
            result.Add(new NavigationSolidPose(id,matrix.MultiplyPoint3x4(center),rotation,extent*2));
        }
        private Vector3 WorldAnchor(InteractionAnchor anchor) => gridRoot.TransformPoint(Grid.GetCellCenterLocal(anchor.Position));
        private bool ValidateBusiness(Dictionary<string,string> failures)
        {
            var report=layoutRuntime?.CurrentReadiness;
            if(report==null) return false;
            var viable=new List<StationReadiness>();
            foreach(var station in report.Stations)
            {
                if(!station.IsValid) { failures[station.InstanceId]="P8 station unavailable"; continue; }
                if(station.Anchors.Anchors.Any(a=>!SampleAnchor(WorldAnchor(a),out _))) { failures[station.InstanceId]="Anchor blocked at actor radius"; continue; }
                viable.Add(station);
            }
            var entrance=gridRoot.TransformPoint(Grid.GetCellCenterLocal(new GridPosition(3,0)));
            bool Connected(StationReadiness a,InteractionRole ar,StationReadiness b,InteractionRole br)
                => a.Anchors.TryGetAnchor(ar,out var aa) && b.Anchors.TryGetAnchor(br,out var ba) && Path(WorldAnchor(aa),WorldAnchor(ba));
            var participating=new HashSet<string>();
            foreach(var cash in viable.Where(s=>s.FunctionType==LayoutStationType.CashRegister))
            foreach(var coffee in viable.Where(s=>s.FunctionType==LayoutStationType.CoffeeMachine))
            foreach(var pickup in viable.Where(s=>s.FunctionType==LayoutStationType.PickUpPoint))
            {
                if(!cash.Anchors.TryGetAnchor(InteractionRole.Customer,out var customer) || !Path(entrance,WorldAnchor(customer)) ||
                   !Connected(cash,InteractionRole.Customer,pickup,InteractionRole.Customer) ||
                   !Connected(cash,InteractionRole.Employee,coffee,InteractionRole.Employee) ||
                   !Connected(coffee,InteractionRole.Employee,pickup,InteractionRole.Employee)) continue;
                participating.Add(cash.InstanceId); participating.Add(coffee.InstanceId); participating.Add(pickup.InstanceId);
            }
            foreach(var station in viable) if(!participating.Contains(station.InstanceId)) failures[station.InstanceId]="No connected essential service combination";
            return report.CanOpenForBusiness && participating.Count>0;
        }
        private bool SampleAnchor(Vector3 position,out Vector3 sampled)
        {
            sampled=position;
            if(!NavMesh.SamplePosition(position,out var hit,.15f,OwnedFilter) || Mathf.Abs(hit.position.y-position.y)>.05f) return false;
            foreach(var solid in snapshot.Skip(1))
            {
                var inverse=Quaternion.Inverse(solid.Rotation); var from=inverse*(position+Vector3.up*.65f-solid.Position); var to=inverse*(hit.position+Vector3.up*.65f-solid.Position);
                var bounds=new Bounds(Vector3.zero,solid.Size);
                if(bounds.Contains(from)||bounds.Contains(to)) return false;
                var delta=to-from;
                if(delta.sqrMagnitude>1e-10f && bounds.IntersectRay(new Ray(from,delta.normalized),out var distance) && distance<=delta.magnitude) return false;
            }
            sampled=hit.position; return true;
        }
        private bool Path(Vector3 from,Vector3 to)
        {
            if(!SampleAnchor(from,out var a)||!SampleAnchor(to,out var b)) return false;
            var path=new NavMeshPath(); return NavMesh.CalculatePath(a,b,OwnedFilter,path) && path.status==NavMeshPathStatus.PathComplete;
        }
        private static NavigationReadiness Result(bool available,string reason) => new NavigationReadiness(available,new Dictionary<string,string>(),reason);
        private static void DestroyOwned(UnityEngine.Object value) { if(Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
        private void OnDestroy() => ReleaseOwnedNavigation();
        public void ReleaseOwnedNavigation()
        {
            if(world!=null) world.SuspendLayout(Revision);
            if(ownedInstance.valid) ownedInstance.Remove(); if(ownedData!=null) DestroyOwned(ownedData); if(geometryRoot!=null) DestroyOwned(geometryRoot);
            if(ownedAgentType.HasValue) { NavMesh.RemoveSettings(ownedAgentType.Value); ownedAgentType=null; }
            ownedData=null; geometryRoot=null; PublishedRevision=-1; CurrentReadiness=Result(false,"LayoutUnavailable");
        }
    }
}
