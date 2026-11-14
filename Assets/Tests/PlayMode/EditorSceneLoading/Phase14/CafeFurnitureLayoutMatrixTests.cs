#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using AnimalCafe.CafeLoop;
using AnimalCafe.Capacity;
using AnimalCafe.Content;
using AnimalCafe.Core.Time;
using AnimalCafe.Customers;
using AnimalCafe.Decoration;
using AnimalCafe.Employees;
using AnimalCafe.Layout;
using AnimalCafe.Navigation;
using AnimalCafe.Orders;
using AnimalCafe.Tests.PlayMode.EditorSceneLoading;
using AnimalCafe.UI.Decoration;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AnimalCafe.Tests.PlayMode.Phase14
{
    // 复用原场景fixture；只走正式Decor Preview/Confirm与真实经营，不新增产品测试接口。
    public sealed partial class CafePickupSideTests
    {
        public static readonly int[] MatrixOrientations={0,1,2,3};
        public static readonly int[] MatrixRapidRecipes={0,1};
        private bool matrixObserving;
        private bool exitDependencyObserved;
        private bool controlledRecoveryStaged;
        [UnityTest] public IEnumerator ControlledPendingCollectedExitRecoveryUsesIntegratedBoundaryPlan()
            => MatrixEntranceRejectsFurnitureAndFloorEdgeKeepsOwnedWaitsInside();
        private readonly System.Collections.Generic.Queue<string> matrixTrafficHistory=new System.Collections.Generic.Queue<string>();
        private static readonly GridPosition[] MatrixNeighbours={new GridPosition(4,5),new GridPosition(5,4),new GridPosition(4,3),new GridPosition(3,4)};

        [UnityTest] public IEnumerator MatrixOpenFourSidesCompleteRealRoundTrip()
        {
            yield return MatrixStart();yield return MatrixFurniture("furniture.counter.module.01",new GridPosition(4,4),0);
            var pickup=Find<CafeLayoutRuntime>().Layout.FurnitureInstances.Single(f=>f.Position==new GridPosition(4,4));
            yield return MatrixFinish(pickup.InstanceId,"slot.0",4,"L-01",0);
        }
        [UnityTest] public IEnumerator MatrixRapidArrivalsRetainFiniteTrafficAndDrain([ValueSource(nameof(MatrixRapidRecipes))] int recipe)
        {
            yield return MatrixStart();
            if(recipe==0)
            {
                yield return MatrixFurniture("counter.preset.1x3",new GridPosition(3,3),0);
                var pickup=Find<CafeLayoutRuntime>().Layout.FurnitureInstances.Single(f=>f.DefinitionId=="counter.preset.1x3");
                yield return MatrixFinish(pickup.InstanceId,"slot.1",2,"L-03-rapid",0,rapid:true);
            }
            else
            {
                yield return MatrixFurniture("furniture.counter.module.01",new GridPosition(4,4),0);
                var pickup=Find<CafeLayoutRuntime>().Layout.FurnitureInstances.Single(f=>f.Position==new GridPosition(4,4));
                for(var side=0;side<4;side++)if(side!=1)yield return MatrixFurniture("furniture.counter.module.01",MatrixNeighbours[side],0);
                yield return MatrixFinish(pickup.InstanceId,"slot.0",1,"L-05-rapid",1,rapid:true);
            }
        }
        [UnityTest] public IEnumerator MatrixAdjacentCornerSidesCompleteRealRoundTrip()
        {
            yield return MatrixStart();yield return MatrixFurniture("furniture.counter.module.01",new GridPosition(4,4),0);
            var pickup=Find<CafeLayoutRuntime>().Layout.FurnitureInstances.Single(f=>f.Position==new GridPosition(4,4));
            yield return MatrixFurniture("furniture.counter.module.01",new GridPosition(4,5),0);
            yield return MatrixFurniture("furniture.counter.module.01",new GridPosition(5,4),0);
            yield return MatrixFinish(pickup.InstanceId,"slot.0",2,"L-04",0);
        }
        [UnityTest] public IEnumerator MatrixCounterCornerRequiresActualDetourAndReturn()
        {
            yield return MatrixStart();yield return MatrixFurniture("furniture.counter.module.01",new GridPosition(4,4),0);
            var pickup=Find<CafeLayoutRuntime>().Layout.FurnitureInstances.Single(f=>f.Position==new GridPosition(4,4));
            yield return MatrixFurniture("furniture.counter.module.01",new GridPosition(4,5),0);
            yield return MatrixFurniture("furniture.counter.module.01",new GridPosition(5,5),0);
            yield return MatrixFinish(pickup.InstanceId,"slot.0",3,"L-07",0,true);
        }
        [UnityTest] public IEnumerator MatrixEntranceRejectsFurnitureAndFloorEdgeKeepsOwnedWaitsInside()
        {
            yield return MatrixStart();var decor=Find<DecorationModeController>();var runtime=Find<CafeLayoutRuntime>();
            var session=CafeLoopRuntimeProof.Field<DecorationSession>(decor,"session");var original=runtime.Layout.FurnitureInstances.First();
            Assert.That(session.BeginExisting(original.InstanceId).Succeeded,Is.True);
            var entrance=runtime.Layout.Reservations.Single(r=>r.Type==LayoutReservationType.EntranceClearance);
            var invalid=session.MovePreview(entrance.Origin);Assert.That(invalid.Succeeded,Is.False);
            Assert.That(invalid.FailureReason,Is.EqualTo(PlacementFailureReason.ReservedEntranceClearance));
            typeof(DecorationModeController).GetMethod("HandleConfirmRequested",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(decor,null);
            Assert.That(session.ActivePreview,Is.Not.Null);Assert.That(runtime.Layout.FurnitureInstances.Single(f=>f.InstanceId==original.InstanceId).Position,Is.EqualTo(original.Position));
            typeof(DecorationModeController).GetMethod("HandleCancelRequested",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(decor,null);
            yield return MatrixFurniture("furniture.counter.module.01",new GridPosition(7,3),0);
            var pickup=runtime.Layout.FurnitureInstances.Single(f=>f.Position==new GridPosition(7,3));
            yield return MatrixFinish(pickup.InstanceId,"slot.0",3,"L-08+L-09",0);
        }
        [UnityTest] public IEnumerator MatrixExposedButDisconnectedSidesBlockWithoutChargeThenFormalRepairServes()
        {
            yield return MatrixStart();var runtime=Find<CafeLayoutRuntime>();var decor=Find<DecorationModeController>();
            yield return MatrixFurniture("furniture.counter.module.01",new GridPosition(5,5),0);
            var pickup=runtime.Layout.FurnitureInstances.Single(f=>f.Position==new GridPosition(5,5));
            for(var x=3;x<=7;x++)for(var y=3;y<=7;y++)
                if(x==3 || x==7 || y==3 || y==7)yield return MatrixFurniture("furniture.counter.module.01",new GridPosition(x,y),0);
            Assert.That(decor.TryBeginFunctionalSurfacePreview(DecorationCatalogueItemKind.PickUpPoint,"pickup.point",new SurfaceSlotAddress(pickup.InstanceId,"slot.0")),Is.True);
            Assert.That(decor.TryConfirmFunctionalSurfacePreview(),Is.True);
            // A failed PrepareExit releases owned NavMesh data. Prove exposed geometry
            // with the formal resolver, rather than calling the walkable-only live selector.
            var surfaceCatalog=CafeLoopRuntimeProof.Field<FurnitureContentCatalog>(decor,"contentCatalog").BuildSurfaceSlotCatalog(runtime.Layout.GridSettings);
            var exposed=new InteractionAnchorResolver().ResolvePickUpCandidates(runtime.FunctionalSurfaceLayout.PickUpPoints.Single(),runtime.Layout,surfaceCatalog,
                p=>runtime.Layout.IsInsideUnlockedRegion(p) && !runtime.Layout.TryGetOccupant(p,out _));
            Assert.That(exposed.Count,Is.EqualTo(4),"Not the zero-exposed-side case");
            decor.ExitDecorationMode();for(var f=0;f<5;f++)yield return null;
            Assert.That(owner.CanProgress,Is.False);Assert.That(customers.TrySpawn(),Is.False);Assert.That(owner.Snapshot.Orders,Is.Empty);
            Assert.That(customers.Snapshot,Is.Empty);Assert.That(customers.Capacity,Is.Null,"Invalid initial business never creates an admission ledger");Assert.That(time.IsResumeBlocked,Is.True);
            Assert.That(time.ResumeBlockReason,Is.Not.Empty);Assert.That(owner.ViewSnapshot.Reception,Is.Not.Empty);
            TestContext.WriteLine("DISCONNECTED_VISIBLE_GATE resume="+time.ResumeBlockReason+" reception="+owner.ViewSnapshot.Reception);
            WriteMatrixRecipe("L-16",0,"EXPECTED_DISCONNECTED_GATE");
            decor.EnterDecorationMode();yield return null;
            var wall=runtime.Layout.FurnitureInstances.Single(f=>f.Position==new GridPosition(3,5));
            var session=CafeLoopRuntimeProof.Field<DecorationSession>(decor,"session");
            Assert.That(session.BeginExisting(wall.InstanceId).Succeeded,Is.True);Assert.That(session.MovePreview(new GridPosition(0,7)).Succeeded,Is.True);
            yield return MatrixConfirmFurniture();
            // Keep the same mounted register/support identity; widen the repair aisle by one fixed grid cell.
            // 原断开gate与门洞修复已验证；通过同一正式Decor路径给Counter envelope留出空间，非坐标搜索。
            var register=runtime.FunctionalSurfaceLayout.MountedInstances.Single(m=>m.DefinitionId=="equipment.cash-register.01");
            Assert.That(session.BeginExisting(register.Address.SupportFurnitureInstanceId).Succeeded,Is.True);
            Assert.That(session.MovePreview(new GridPosition(0,5)).Succeeded,Is.True);
            yield return MatrixConfirmFurniture();
            yield return MatrixFinish(pickup.InstanceId,"slot.0",4,"L-16",1,false,true);
        }

        [UnityTest] public IEnumerator MatrixThreeSidesHaveRealDeliveryAndReturn([ValueSource(nameof(MatrixOrientations))] int rotation)
        {
            yield return MatrixStart();
            yield return MatrixFurniture("furniture.counter.module.01",new GridPosition(4,4),0);
            var pickup=Find<CafeLayoutRuntime>().Layout.FurnitureInstances.Single(f=>f.Position==new GridPosition(4,4));
            yield return MatrixFurniture("furniture.counter.module.01",MatrixNeighbours[rotation],0);
            yield return MatrixFinish(pickup.InstanceId,"slot.0",3,"L-02",rotation);
        }
        [UnityTest] public IEnumerator MatrixMiddleSlotRotationsHaveRealDeliveryAndReturn([ValueSource(nameof(MatrixOrientations))] int rotation)
        {
            yield return MatrixStart();
            yield return MatrixFurniture("counter.preset.1x3",new GridPosition(3,3),rotation);
            var pickup=Find<CafeLayoutRuntime>().Layout.FurnitureInstances.Single(f=>f.DefinitionId=="counter.preset.1x3");
            yield return MatrixFinish(pickup.InstanceId,"slot.1",2,"L-03",rotation);
        }
        [UnityTest] public IEnumerator MatrixSingleSideOrientationsHaveRealDeliveryAndReturn([ValueSource(nameof(MatrixOrientations))] int rotation)
        {
            yield return MatrixStart();
            yield return MatrixFurniture("furniture.counter.module.01",new GridPosition(4,4),0);
            var pickup=Find<CafeLayoutRuntime>().Layout.FurnitureInstances.Single(f=>f.Position==new GridPosition(4,4));
            for(var side=0;side<4;side++) if(side!=rotation) yield return MatrixFurniture("furniture.counter.module.01",MatrixNeighbours[side],0);
            yield return MatrixFinish(pickup.InstanceId,"slot.0",1,"L-05",rotation);
        }

        private IEnumerator MatrixStart()
        {
            matrixTrafficHistory.Clear();
            exitDependencyObserved=false;
            controlledRecoveryStaged=false;
            var old=scene;var oldInputs=inputs;
            SceneManager.SetActiveScene(SceneManager.CreateScene("corridor matrix switch"));
            yield return SceneManager.UnloadSceneAsync(old);Phase8SceneInputTestCleanup.DisposeReleasedAssets(oldInputs);
            scene=EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/MainCafe.unity",new LoadSceneParameters(LoadSceneMode.Single));yield return null;
            owner=Find<CafeLoopController>();customers=Find<CustomerFlowController>();employees=Find<EmployeeFlowController>();
            tools=Find<CafeLoopValidationController>();world=Find<NavigationWorld>();adapter=Find<NavigationLayoutAdapter>();time=Find<GameTimeService>();
            inputs=Phase8SceneInputTestCleanup.CaptureAssets(scene);deliveryStarts.Clear();tools.HoldArrivals();tools.HoldCollection();
            Assert.That(world.RegisteredActors,Is.Empty,"All formal furniture authoring precedes actual staff/customer birth");
            var runtime=Find<CafeLayoutRuntime>();var decor=Find<DecorationModeController>();decor.EnterDecorationMode();yield return null;
            var initial=runtime.Layout.FurnitureInstances.Single();var session=CafeLoopRuntimeProof.Field<DecorationSession>(decor,"session");
            Assert.That(session.BeginExisting(initial.InstanceId).Succeeded,Is.True);Assert.That(session.MovePreview(new GridPosition(1,5)).Succeeded,Is.True);
            yield return MatrixConfirmFurniture();
            yield return MatrixFurniture("furniture.counter.module.01",new GridPosition(4,6),0);
            var machineSupport=runtime.Layout.FurnitureInstances.Single(f=>f.Position==new GridPosition(4,6));
            var catalog=CafeLoopRuntimeProof.Field<FurnitureContentCatalog>(decor,"contentCatalog");
            var slot=catalog.BuildSurfaceSlotCatalog(runtime.Layout.GridSettings).GetForSupport(initial.DefinitionId).First().SlotId;
            Assert.That(decor.TryBeginFunctionalSurfacePreview(DecorationCatalogueItemKind.CashRegister,"equipment.cash-register.01",new SurfaceSlotAddress(initial.InstanceId,slot)),Is.True);
            Assert.That(decor.TryConfirmFunctionalSurfacePreview(),Is.True);yield return null;
            Assert.That(decor.TryBeginFunctionalSurfacePreview(DecorationCatalogueItemKind.CoffeeMachine,"equipment.coffee-machine.01",new SurfaceSlotAddress(machineSupport.InstanceId,slot)),Is.True);
            Assert.That(decor.TryRotateFunctionalSurfacePreview(),Is.True);Assert.That(decor.TryConfirmFunctionalSurfacePreview(),Is.True);yield return null;
        }
        private IEnumerator MatrixFurniture(string definition,GridPosition position,int rotation)
        {
            var decor=Find<DecorationModeController>();
            var asset=AssetDatabase.FindAssets("t:FurnitureDefinitionAsset").Select(g=>AssetDatabase.LoadAssetAtPath<FurnitureDefinitionAsset>(AssetDatabase.GUIDToAssetPath(g))).Single(a=>a.DefinitionId==definition);
            Assert.That(decor.TrySelectCatalogueItem(new DecorationCatalogueItemModel(asset.DefinitionId,asset.DisplayName,null,DecorationCatalogueItemKind.Furniture,false,asset)),Is.True);
            var session=CafeLoopRuntimeProof.Field<DecorationSession>(decor,"session");
            Assert.That(session.MovePreview(position).Succeeded,Is.True,"Matrix furniture target must really be legal");
            for(var i=0;i<rotation;i++) Assert.That(session.RotatePreview().Succeeded,Is.True);
            yield return MatrixConfirmFurniture();
        }
        private IEnumerator MatrixConfirmFurniture()
        {
            yield return null;
            var decor=Find<DecorationModeController>();
            // API-layer fixture: the Controller still checks placement/navigation guards.
            // Direct session preview setup does not refresh the ActionBar's UI latch.
            var confirm=typeof(DecorationModeController).GetMethod("HandleConfirmRequested",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            Assert.That(confirm,Is.Not.Null);confirm.Invoke(decor,null);
            Assert.That(CafeLoopRuntimeProof.Field<DecorationSession>(decor,"session").ActivePreview,Is.Null,"Controller Confirm retains all placement/navigation guards");yield return null;
        }
        private IEnumerator MatrixFinish(string supportId,string slot,int sideCount,string layoutId,int rotation,bool requireDetour=false,bool pickupAlreadyMounted=false,bool rapid=false)
        {
            var runtime=Find<CafeLayoutRuntime>();var decor=Find<DecorationModeController>();
            if(!pickupAlreadyMounted)
            {Assert.That(decor.TryBeginFunctionalSurfacePreview(DecorationCatalogueItemKind.PickUpPoint,"pickup.point",new SurfaceSlotAddress(supportId,slot)),Is.True);
                Assert.That(decor.TryConfirmFunctionalSurfacePreview(),Is.True);}
            decor.ExitDecorationMode();
            yield return Until(()=>owner.CanProgress && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            // The combo can become ready at the end of an owner Step; admission is
            // published at the start of the next normal positive-delta Step.
            yield return null;
            Assert.That(CafeLoopRuntimeProof.Field<bool>(customers,"integratedAdmissionAllowed"),Is.True,"Admission policy published after normal owner update");
            var pickup=runtime.FunctionalSurfaceLayout.PickUpPoints.Single();var sides=customers.GetPickupServiceTargets(pickup.InstanceId);
            WriteMatrixRecipe(layoutId,rotation,"BUILT");Assert.That(sides.Count,Is.EqualTo(sideCount));
            var paid=0;var prepared=0;var delivered=0;var actualDetour=false;
            Action<CafeTaskEvent> count=e=>{if(e.Kind==CafeTaskEventKind.ServiceFinished)paid++;if(e.Kind==CafeTaskEventKind.Prepared)prepared++;if(e.Kind==CafeTaskEventKind.DeliveryArrived)
                {delivered++;employees.TryGetBinding(EmployeeRole.Barista,out var bound);Assert.That(employees.TryGetDeliveryTarget(out var target),Is.True);
                    var actual=world.RegisteredActors.Single(a=>a.ActorId==e.ActorId);Assert.That(Vector3.Distance(actual.transform.position,target.Position),Is.LessThanOrEqualTo(.081f));
                    var native=adapter.GetCompletePath(bound.WorkTarget.Position,target.Position);actualDetour=native!=null && native.Count>2;
                    TestContext.WriteLine("MATRIX_ACTUAL_DELIVERY target="+target.Position+" native="+string.Join(";",native??Array.Empty<Vector3>()));}};
            employees.IntegratedTaskEvent+=count;
            var firstBlock=false;Action<CafeTaskEvent> captureBlock=e=>{
                if(firstBlock || e.Kind!=CafeTaskEventKind.Blocked)return;firstBlock=true;
                TestContext.WriteLine("FIRST_BLOCK_HISTORY "+string.Join("\n",matrixTrafficHistory));
                TestContext.WriteLine("FIRST_BLOCK "+e.Failure+" "+MatrixTrafficDescription());
                DiagnoseCollectionHandoff();
            };
            employees.IntegratedTaskEvent+=captureBlock;
            Action originalHandoffWitness=null;Action originalLegQueryWitness=null;Action originalCollectionFailureWitness=null;
            try
            {
                originalHandoffWitness=BeginMatrixOriginalHandoff();
                originalLegQueryWitness=BeginMatrixActualLegQueryWitness();
                originalCollectionFailureWitness=BeginMatrixCollectionFailureWitness();
                // Same unborn proposal, normal bounded driver; keep the original canonical-birth assertion.
                // 等待原路线分帧准备，不出生额外顾客或绕过许可。
                yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000);
                Assert.That(customers.TrySpawn(),Is.True,customers.Status);var id=customers.Snapshot.Single().VisitId;
                var tokens=customers.Capacity.GetReservations().Where(r=>r.OwnerId==id).Select(r=>r.Token).ToArray();
                var total=customers.Capacity.GetReservations().Single(r=>r.OwnerId==id && r.Kind==CapacityKind.TotalCustomers).Token;
                yield return Until(()=>owner.Snapshot.Delivery.Count==1 && employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista).State==EmployeeState.Idle && customers.PickupAssignments.Single().Arrived);
                employees.TryGetBinding(EmployeeRole.Barista,out var binding);var barista=world.RegisteredActors.Single(a=>a.ActorId==employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista).ActorId);
                Assert.That(Vector3.Distance(barista.transform.position,binding.WorkTarget.Position),Is.LessThanOrEqualTo(.081f));
                Assert.That(owner.Snapshot.Delivery.Single().Address,Is.EqualTo(new SurfaceSlotAddress(supportId,slot)));
                var registry=Find<FurnitureSceneRegistry>();Assert.That(registry.TryGet(supportId,out var support),Is.True);
                var module=support.GetComponentsInChildren<SurfaceSlotMarker>().Single(m=>m.SlotId==slot).transform;
                var cup=Find<PickupCupDisplay>().VisibleOrders.Single().Value;Assert.That(cup.transform.parent,Is.SameAs(module));
                var body=world.RegisteredActors.Single(a=>a.ActorId==id);var actualExit=false;var tracking=true;
                if(requireDetour)Assert.That(actualDetour,Is.True,"L07 must really use a cornered native path, not an open straight line");
                var plannedWaits=CafeLoopRuntimeProof.Field<Vector3[]>(CafeLoopRuntimeProof.Field<PickupQueueService>(customers,"pickupQueue"),"waitSlots");
                foreach(var point in plannedWaits)
                {Assert.That(adapter.IsPointWalkable(point),Is.True);Assert.That((bool)typeof(CustomerFlowController).GetMethod("InEntrance",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(customers,new object[]{point}),Is.False);}
                var observer=body.gameObject.AddComponent<CafeActualExitObserver>();observer.VisitId=id;
                observer.Receipt=(visit,pose)=>{if(!tracking)return;Assert.That(Vector3.Distance(pose,CafeLoopRuntimeProof.Field<Transform>(customers,"exit").position),Is.LessThanOrEqualTo(.081f));
                    Assert.That(customers.Capacity.GetReservations().Single(r=>ReferenceEquals(r.Token,total)).State,Is.EqualTo(ReservationState.Released));actualExit=true;};
                try{tools.ResumeCollection();yield return Until(()=>actualExit && customers.Capacity.GetCapacities().All(c=>c.Used==0) && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));}
                finally{tracking=false;}
                Assert.That(owner.Snapshot.Orders.Values.Single().State,Is.EqualTo(OrderState.Completed));
                Assert.That(paid,Is.EqualTo(1));Assert.That(prepared,Is.EqualTo(1));Assert.That(delivered,Is.EqualTo(1));
                CollectionAssert.AreEqual(tokens,customers.Capacity.GetReservations().Where(r=>r.OwnerId==id).Select(r=>r.Token));WriteMatrixRecipe(layoutId,rotation,"PASS");
                employees.IntegratedTaskEvent-=count;
                yield return MatrixPaidCohorts(layoutId,rotation,rapid);
            }
            finally{originalCollectionFailureWitness?.Invoke();originalLegQueryWitness?.Invoke();originalHandoffWitness?.Invoke();employees.IntegratedTaskEvent-=count;employees.IntegratedTaskEvent-=captureBlock;}
        }

        // One original collection failure/role in the selected repaired-island cohort; no diagnostic queries.
        // 只转发原query，并在原ProcessReady结束/CFC推进候选前读结果；不重放任何guard。
        private Action BeginMatrixCollectionFailureWitness()
        {
            if(!Environment.GetCommandLineArgs().Contains("-matrixCollectionFailure") ||
                TestContext.CurrentContext.Test.Name!="MatrixExposedButDisconnectedSidesBlockWithoutChargeThenFormalRepairServes")return null;
            const System.Reflection.BindingFlags f=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
            object Read(object value,string name)=>value==null?null:(value.GetType().GetField(name,f)??value.GetType().GetField("<"+name+">k__BackingField",f))?.GetValue(value);
            string Ref(object value)=>value==null?"NULL":value.GetType().Name+"#"+System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
            string P(Vector3 value)=>value.ToString("F9");
            string Path(System.Collections.Generic.IEnumerable<Vector3> values)=>values==null?"NULL":string.Join(";",values.Select(P));
            string Key(TrafficIntentKey k)=>k.Session+"/e"+k.Epoch+"/"+k.ActorId+"/"+k.IntentId+"/g"+k.Generation+"/r"+k.LayoutRevision+"/"+k.Context;
            string Requirements(TrafficRequest r)=>r==null?"NULL":string.Join(";",r.Requirements.Select(q=>q.ResourceId+"/"+P(q.Start)+"->"+P(q.Goal)+
                "/clearance="+q.Clearance+"/selected="+q.SelectedJourney+"/avoidParking="+q.AvoidProposedParking+"/obstacles=["+Path(q.Obstacles)+"]"));
            var observed=customers;object core=null;
            System.Reflection.FieldInfo queryField=null,corridorField=null,readyField=null;
            Func<Vector3,Vector3,System.Collections.Generic.IReadOnlyList<Vector3>> original=null,wrapped=null;
            Delegate corridorPrior=null,corridorInstalled=null,readyPrior=null,readyInstalled=null;
            var cleanupPrior=parkingEndpointWitnessCleanup;var storedPrior=parkingEndpointStoredEnd;
            Action cleanupInstalled=null,storedInstalled=null;var errors=new System.Collections.Generic.List<string>();
            string watched=null,preflight=null,record=null,lastQuery=null,firstGrant=null;
            object lastEntry=null,lastJob=null;TrafficRequest lastRequest=null;var lastFrame=-1;var lastRole="NOT_OBSERVED";
            var finished=false;var queryRestored=false;var corridorRestored=false;var readyRestored=false;var cleanupRestored=false;var storedRestored=false;
            var originalCalls=0;var matchedCalls=0;var grantedBatches=0;var preparedBatches=0;
            void Error(string value){if(errors.Count<8)errors.Add(value);}
            string EntryText(object entry)
            {
                var r=Read(entry,"Request") as TrafficRequest;var d=Read(entry,"Decision") as TrafficDecision;
                var pending=Read(entry,"PendingHandoff");
                return "entry="+Ref(entry)+" key="+(r==null?"NULL":Key(r.Intent))+" decision="+d?.Kind+"/"+d?.Reason+"/"+d?.FailureDisposition+
                    " seq="+Read(entry,"RequestSequence")+" acq="+Read(entry,"AcquiredSequence")+" queries="+Read(entry,"Queries")+
                    " queued="+Read(entry,"Queued")+" prepared="+Read(entry,"Prepared")+" retired="+Read(entry,"Retired")+" jobIndex="+Read(entry,"JobIndex")+
                    " pending="+Ref(pending)+" remaining="+Read(pending,"Remaining")+" source="+Ref(Read(pending,"Source"))+
                    " request="+Ref(r)+" start="+(r==null?"NULL":P(r.Start))+" target="+(r==null?"NULL":P(r.Target))+" roles=["+Requirements(r)+"]";
            }
            Action finish=()=>
            {
                if(finished)return;finished=true;
                string final="UNKNOWN";
                try
                {
                    var visits=Read(observed,"visits") as IDictionary;var visit=visits!=null && watched!=null && visits.Contains(watched)?visits[watched]:null;
                    var pending=Read(visit,"PendingTraffic");var journey=Read(pending,"Intent");var entries=Read(core,"entries") as IDictionary;
                    var previous=entries!=null && journey!=null && entries.Contains(journey)?entries[journey]:null;
                    var candidates=Read(observed,"trafficCandidates") as IDictionary;
                    var cursor=candidates?.Values.Cast<object>().FirstOrDefault(c=>Read(c,"Key") is TrafficIntentKey k && k.ActorId==watched && k.IntentId=="pickup-service");
                    var key=Read(cursor,"Key");var entry=entries!=null && key!=null && entries.Contains(key)?entries[key]:null;
                    final="watched="+(watched??"NONE")+" state="+Read(visit,"State")+" resume="+Read(visit,"ResumeState")+" failure="+Read(visit,"Failure")+
                        " requestId="+Read(visit,"RequestId")+" pending="+Ref(pending)+" marked="+Read(pending,"Marked")+" handoff="+Ref(Read(pending,"Handle"))+
                        " predecessor=["+EntryText(previous)+"] cursor="+Ref(cursor)+" currentIndex="+Read(cursor,"CurrentIndex")+
                        " source="+Ref(Read(cursor,"Source"))+" preparation="+Ref(Read(cursor,"PickupPreparation"))+" release="+Ref(Read(cursor,"CollectionRelease"))+" successor=["+EntryText(entry)+"]";
                }
                catch(Exception e){Error("final-retained-facts/"+e.GetType().Name);}
                try{if(ReferenceEquals(parkingEndpointWitnessCleanup,cleanupInstalled)){parkingEndpointWitnessCleanup=cleanupPrior;cleanupRestored=true;}
                    else if(ReferenceEquals(parkingEndpointWitnessCleanup,cleanupPrior))cleanupRestored=true;else Error("UNKNOWN cleanup owner replaced; preserved");}catch(Exception e){Error("cleanup-chain/"+e.GetType().Name);}
                try{if(ReferenceEquals(parkingEndpointStoredEnd,storedInstalled)){parkingEndpointStoredEnd=storedPrior;storedRestored=true;}
                    else if(ReferenceEquals(parkingEndpointStoredEnd,storedPrior))storedRestored=true;else Error("UNKNOWN stored-end owner replaced; preserved");}catch(Exception e){Error("stored-chain/"+e.GetType().Name);}
                try{if(queryField!=null && original!=null){var current=queryField.GetValue(core);if(ReferenceEquals(current,wrapped)){queryField.SetValue(core,original);queryRestored=true;}
                    else if(ReferenceEquals(current,original))queryRestored=true;else Error("UNKNOWN query owner replaced; preserved");}}catch(Exception e){Error("query-restore/"+e.GetType().Name);}
                try{if(corridorField!=null){var current=corridorField.GetValue(observed);if(ReferenceEquals(current,corridorInstalled)){corridorField.SetValue(observed,corridorPrior);corridorRestored=true;}
                    else if(ReferenceEquals(current,corridorPrior))corridorRestored=true;else Error("UNKNOWN corridor owner replaced; preserved");}}catch(Exception e){Error("corridor-restore/"+e.GetType().Name);}
                try{if(readyField!=null){var current=readyField.GetValue(null);if(ReferenceEquals(current,readyInstalled)){readyField.SetValue(null,readyPrior);readyRestored=true;}
                    else if(ReferenceEquals(current,readyPrior))readyRestored=true;else Error("UNKNOWN ready observer replaced; preserved");}}catch(Exception e){Error("ready-restore/"+e.GetType().Name);}
                try
                {
                    TestContext.WriteLine("MATRIX_COLLECTION_FAILURE status="+(errors.Count>0?"UNKNOWN":record!=null?"OBSERVED":"NOT_OBSERVED")+
                        " cap=1 originalCalls="+originalCalls+" matchedCalls="+matchedCalls+" grantedBatches="+grantedBatches+" preparedBatches="+preparedBatches+
                        " queryRestored="+queryRestored+" corridorRestored="+corridorRestored+" readyRestored="+readyRestored+" cleanupRestored="+cleanupRestored+" storedRestored="+storedRestored+
                        " addedNativeQueries=0 guardOrPlannerReplay=0 sameReturnReference=True originalExceptionsRethrown=True errors="+errors.Count);
                    TestContext.WriteLine("MATRIX_COLLECTION_PREFLIGHT "+(preflight??"NOT_OBSERVED"));
                    if(record!=null)TestContext.WriteLine(record);
                    else if(firstGrant!=null)TestContext.WriteLine(firstGrant);
                    TestContext.WriteLine("MATRIX_COLLECTION_FINAL_RETAINED "+final);
                    foreach(var error in errors)TestContext.WriteLine("MATRIX_COLLECTION_FAILURE_ERROR "+error);
                }
                catch(Exception){}
            };
            try
            {
                cleanupInstalled=(Action)Delegate.Combine(finish,cleanupPrior);parkingEndpointWitnessCleanup=cleanupInstalled;
                storedInstalled=(Action)Delegate.Combine(finish,storedPrior);parkingEndpointStoredEnd=storedInstalled;
                core=Read(observed,"traffic");if(core==null)throw new InvalidOperationException("Original Core missing");
                queryField=core.GetType().GetField("getCompletePath",f);corridorField=typeof(CustomerFlowController).GetField("CorridorObserver",f);
                var cost=typeof(NavigationLayoutAdapter).Assembly.GetType("AnimalCafe.Navigation.NavigationCostObservation");readyField=cost?.GetField("ProcessReadyElapsed",f);
                original=queryField?.GetValue(core) as Func<Vector3,Vector3,System.Collections.Generic.IReadOnlyList<Vector3>>;
                if(original==null || corridorField==null || readyField==null)throw new InvalidOperationException("Existing original query/observers unavailable");
                wrapped=(from,to)=>
                {
                    originalCalls++;object matched=null,job=null;TrafficRequest request=null;string facts=null;var frame=Time.frameCount;var role="NOT_PROVEN";
                    if(!finished && record==null && watched!=null)
                    try
                    {
                        var entries=(IDictionary)Read(core,"entries");
                        foreach(var candidate in entries.Values)
                        {
                            var r=Read(candidate,"Request") as TrafficRequest;
                            if(r==null || r.Intent.ActorId!=watched || r.Intent.IntentId!="pickup-service" || !(bool)Read(candidate,"Queued") || (bool)Read(candidate,"Retired"))continue;
                            var jobs=Read(candidate,"Jobs") as IList;var index=(int)Read(candidate,"JobIndex");
                            if(jobs==null || index<0 || index>=jobs.Count)continue;
                            var current=jobs[index];var preferred=Read(current,"preferredJob");
                            if(preferred!=null && !(bool)Read(current,"preferredFinished"))current=preferred;
                            Vector3 a,b;
                            if(!(bool)Read(current,"started")){a=(Vector3)Read(current,"start");b=(Vector3)Read(current,"goal");}
                            else{var nodes=Read(current,"nodes") as IList;var at=(int)Read(current,"current");var next=(int)Read(current,"next");
                                if(nodes==null || at<0 || next<0 || at>=nodes.Count || next>=nodes.Count)continue;a=(Vector3)nodes[at];b=(Vector3)nodes[next];}
                            if(a!=from || b!=to)continue;
                            if(matched!=null){Error("UNKNOWN multiple original job/query matches");matched=null;request=null;break;}
                            matched=candidate;job=current;request=r;
                            var offset=!(bool)Read(candidate,"Parking") || r.PlanPrimaryRoute?1:0;
                            role=offset==1 && index==0?r.ResourceId:index-offset>=0 && index-offset<r.Requirements.Count?r.Requirements[index-offset].ResourceId:"UNKNOWN_ROLE";
                            facts="key="+Key(r.Intent)+" entry="+Ref(candidate)+" request="+Ref(r)+" job="+Ref(current)+" jobIndex="+index+" role="+role+
                                " roleStart="+P((Vector3)Read(current,"start"))+" roleGoal="+P((Vector3)Read(current,"goal"))+" queryFrom="+P(from)+" queryTo="+P(to)+
                                " queries="+Read(candidate,"Queries")+" candidate="+Read(candidate,"CurrentIndex")+" seq="+Read(candidate,"RequestSequence")+
                                " snapshot="+r.Snapshot.Version+"/"+r.Snapshot.BodyVersion+" started="+Read(current,"started")+" current="+Read(current,"current")+" next="+Read(current,"next")+
                                " jobObstacles=["+string.Join(";",((IEnumerable)Read(current,"obstacles")).Cast<object>().Select(o=>P((Vector3)Read(o,"Position"))+"/"+Read(o,"Clearance")))+"] roles=["+Requirements(r)+"]";
                        }
                    }
                    catch(Exception e){Error("query-before/"+e.GetType().Name);matched=null;request=null;}
                    void Capture(System.Collections.Generic.IReadOnlyList<Vector3> result,Exception exception)
                    {
                        if(request==null || finished || record!=null)return;
                        try{lastEntry=matched;lastJob=job;lastRequest=request;lastFrame=frame;lastRole=role;matchedCalls++;
                            lastQuery="MATRIX_COLLECTION_ORIGINAL_QUERY frame="+frame+" "+facts+" rawRef="+Ref(result)+" rawCount="+(result==null?-1:result.Count)+" raw=["+Path(result)+"] originalException="+
                                (exception==null?"NONE":exception.GetType().FullName+"/"+exception.Message);}
                        catch(Exception e){Error("query-after/"+e.GetType().Name);}
                    }
                    System.Collections.Generic.IReadOnlyList<Vector3> result;
                    try{result=original(from,to);}catch(Exception e){Capture(null,e);throw;}
                    Capture(result,null);return result;
                };
                corridorPrior=corridorField.GetValue(observed) as Delegate;
                Action<string,string,string,Vector3,System.Collections.Generic.IReadOnlyList<Vector3>,Vector3[],string> corridor=(operation,id,other,target,path,obstacles,result)=>
                {
                    if(finished || watched!=null || !matrixObserving || operation!="traffic.collection-prerequisite-preflight" || string.IsNullOrEmpty(id))return;
                    try{watched=id;var visits=(IDictionary)Read(observed,"visits");var visit=visits[id];var pending=Read(visit,"PendingTraffic");var source=Read(pending,"Source") as TrafficDecision;
                        preflight="frame="+Time.frameCount+" original="+result+" watched="+id+" state="+Read(visit,"State")+" pending="+Ref(pending)+" marked="+Read(pending,"Marked")+
                            " source="+Ref(source)+" sourceKey="+(source==null?"NULL":Key(source.Intent))+" handle="+Ref(Read(pending,"Handle"))+" observerTarget="+P(target);}
                    catch(Exception e){Error("preflight/"+e.GetType().Name);}
                };
                corridorInstalled=Delegate.Combine(corridorPrior,corridor);
                readyPrior=readyField.GetValue(null) as Delegate;
                Action<int,long> ready=(frame,ticks)=>
                {
                    if(finished || record!=null || watched==null || !matrixObserving)return;
                    try
                    {
                        if(!ReferenceEquals(Read(observed,"traffic"),core)){Error("UNKNOWN Core replaced");return;}
                        foreach(var entry in ((IDictionary)Read(core,"entries")).Values)
                        {
                            var r=Read(entry,"Request") as TrafficRequest;var d=Read(entry,"Decision") as TrafficDecision;
                            if(r==null || d==null || r.Intent.ActorId!=watched || r.Intent.IntentId!="pickup-service" || (bool)Read(entry,"Retired"))continue;
                            preparedBatches++;
                            if(d.Kind==TrafficDecisionKind.Granted){grantedBatches++;if(firstGrant==null)firstGrant="MATRIX_COLLECTION_ORIGINAL_GRANTED frame="+frame+" "+EntryText(entry);continue;}
                            if(d.FailureDisposition==TrafficFailureDisposition.None)continue;
                            var exact=ReferenceEquals(entry,lastEntry) && ReferenceEquals(r,lastRequest) && lastFrame==frame;
                            record="MATRIX_COLLECTION_ORIGINAL_FAILURE frame="+frame+" roleStatus="+(exact?"EXACT_ORIGINAL_QUERY":"NOT_PROVEN_NO_SAME_REQUEST_FRAME_QUERY")+
                                " originalRole="+(exact?lastRole:"NOT_PROVEN")+" "+EntryText(entry)+" lastJob="+Ref(lastJob)+" lastJobComplete="+Read(lastJob,"complete")+
                                " lastJobStarted="+Read(lastJob,"started")+" lastJobCurrent="+Read(lastJob,"current")+" lastJobNext="+Read(lastJob,"next")+
                                " lastExpanded=["+Path(Read(lastJob,"ExpandedPath") as Vector3[])+"]\n"+(lastQuery??"MATRIX_COLLECTION_ORIGINAL_QUERY NOT_OBSERVED");break;
                        }
                    }
                    catch(Exception e){Error("original-ready/"+e.GetType().Name);}
                };
                readyInstalled=Delegate.Combine(readyPrior,ready);
                queryField.SetValue(core,wrapped);corridorField.SetValue(observed,corridorInstalled);readyField.SetValue(null,readyInstalled);
                TestContext.WriteLine("MATRIX_COLLECTION_FAILURE_INSTALL status=INSTALLED selected=Disconnected cap=1 originalDelegateWrapper=True originalReadyObserver=True addedQueries=0 cleanupBeforeUnload=True");
            }
            catch(Exception e){Error("install/"+e.GetType().Name+"/"+e.Message);finish();}
            return finish;
        }

        // One selected floor-edge case only. Forward the original delegate once, return its exact reference.
        // 仅记录原始actual-leg query；不重查Nav，不调用planner/权限predicate，不改变返回值/异常。
        private Action BeginMatrixActualLegQueryWitness()
        {
            if(!Environment.GetCommandLineArgs().Contains("-matrixActualLegQuery") ||
                TestContext.CurrentContext.Test.Name!="MatrixEntranceRejectsFurnitureAndFloorEdgeKeepsOwnedWaitsInside")return null;
            const System.Reflection.BindingFlags f=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
            object Read(object value,string name)=>value==null?null:(value.GetType().GetField(name,f)??value.GetType().GetField("<"+name+">k__BackingField",f))?.GetValue(value);
            string Ref(object value)=>value==null?"NULL":value.GetType().Name+"#"+System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
            string P(Vector3 point)=>point.ToString("F9");
            string Path(System.Collections.Generic.IEnumerable<Vector3> path)=>path==null?"NULL":string.Join(";",path.Select(P));
            string Key(TrafficIntentKey k)=>k.Session+"/e"+k.Epoch+"/"+k.ActorId+"/"+k.IntentId+"/g"+k.Generation+"/r"+k.LayoutRevision+"/"+k.Context;
            var records=new System.Collections.Generic.List<string>();var errors=new System.Collections.Generic.List<string>();
            var latest=new System.Collections.Generic.Dictionary<string,(int Frame,string Text)>();
            var observed=customers;object core=null;System.Reflection.FieldInfo queryField=null,corridorField=null;
            Func<Vector3,Vector3,System.Collections.Generic.IReadOnlyList<Vector3>> original=null,wrapped=null;
            Delegate corridorPrior=null,corridorInstalled=null;var cleanupPrior=parkingEndpointWitnessCleanup;Action cleanupInstalled=null;
            var finished=false;var queryRestored=false;var corridorRestored=false;var cleanupRestored=false;var calls=0;var captured=0;var exact=0;
            void Error(string value){if(errors.Count<8)errors.Add(value);}
            Action finish=()=>
            {
                if(finished)return;finished=true;
                try{if(ReferenceEquals(parkingEndpointWitnessCleanup,cleanupInstalled)){parkingEndpointWitnessCleanup=cleanupPrior;cleanupRestored=true;}
                    else if(ReferenceEquals(parkingEndpointWitnessCleanup,cleanupPrior))cleanupRestored=true;else Error("UNKNOWN cleanup owner replaced");}catch(Exception e){Error("cleanup-chain/"+e.GetType().Name);}
                try{if(queryField!=null && original!=null){var current=queryField.GetValue(core);if(ReferenceEquals(current,wrapped)){queryField.SetValue(core,original);queryRestored=true;}
                    else if(ReferenceEquals(current,original))queryRestored=true;else Error("UNKNOWN query owner replaced; preserved");}}catch(Exception e){Error("query-restore/"+e.GetType().Name);}
                try{if(corridorField!=null){var current=corridorField.GetValue(observed);if(ReferenceEquals(current,corridorInstalled)){corridorField.SetValue(observed,corridorPrior);corridorRestored=true;}
                    else if(ReferenceEquals(current,corridorPrior))corridorRestored=true;else Error("UNKNOWN corridor owner replaced; preserved");}}catch(Exception e){Error("corridor-restore/"+e.GetType().Name);}
                try
                {
                    TestContext.WriteLine("MATRIX_LEG_QUERY status="+(errors.Count>0?"UNKNOWN":exact>0?"OBSERVED":"NOT_OBSERVED")+
                        " originalCalls="+calls+" capturedActualLegQueries="+captured+" failureRecords="+records.Count+" cap=8 exactAssociations="+exact+
                        " queryRestored="+queryRestored+" corridorRestored="+corridorRestored+" cleanupRestored="+cleanupRestored+
                        " errors="+errors.Count+" addedNativeQueries=0 plannerOrPolicyReplay=0 sameReturnReference=True originalExceptionsRethrown=True");
                    foreach(var record in records)TestContext.WriteLine(record);
                    foreach(var error in errors)TestContext.WriteLine("MATRIX_LEG_QUERY_ERROR "+error);
                }
                catch(Exception){}finally{latest.Clear();}
            };
            try
            {
                cleanupInstalled=(Action)Delegate.Combine(finish,cleanupPrior);parkingEndpointWitnessCleanup=cleanupInstalled;
                core=Read(observed,"traffic");if(core==null)throw new InvalidOperationException("original Core unavailable");
                queryField=core.GetType().GetField("getCompletePath",f);corridorField=typeof(CustomerFlowController).GetField("CorridorObserver",f);
                original=queryField?.GetValue(core) as Func<Vector3,Vector3,System.Collections.Generic.IReadOnlyList<Vector3>>;
                if(original==null || corridorField==null)throw new InvalidOperationException("original delegate/hook unavailable");
                wrapped=(from,to)=>
                {
                    calls++;object entry=null,leg=null;TrafficRequest request=null;string beforeFacts=null;var frame=Time.frameCount;
                    if(!finished)
                    try
                    {
                        var entries=Read(core,"entries") as System.Collections.IDictionary;
                        var matches=entries.Values.Cast<object>().Where(e=>!(bool)Read(e,"Retired") && (bool)Read(e,"Queued") &&
                            Read(e,"Leg")!=null && (bool)Read(Read(e,"Leg"),"NeedsQuery") && Read(e,"Request") is TrafficRequest q && q.Start==from && q.Target==to).ToArray();
                        if(matches.Length==1)
                        {
                            entry=matches[0];leg=Read(entry,"Leg");request=(TrafficRequest)Read(entry,"Request");
                            var proof=Read(leg,"Proof") as TrafficRouteProof;var snapshot=Read(core,"snapshot") as TrafficWorldSnapshot;
                            var actor=Read(leg,"ActorLife") as NavigationActor;
                            beforeFacts="key="+Key(request.Intent)+" entry="+Ref(entry)+" request="+Ref(request)+" leg="+Ref(leg)+" actorLife="+Ref(actor)+
                                " source="+Ref(Read(entry,"JourneyPermit"))+" acquired="+Read(entry,"AcquiredSequence")+" sequence="+Read(entry,"RequestSequence")+
                                " queries="+Read(entry,"Queries")+" firstFrame="+Read(entry,"FirstFrame")+" queued="+Read(entry,"Queued")+
                                " legIndex="+Read(leg,"Index")+" generation="+Read(leg,"Generation")+" stamp="+Read(leg,"Stamp")+
                                " committed="+Read(leg,"Committed")+" bound="+Read(leg,"Bound")+" completed="+Read(leg,"Completed")+
                                " needsQuery="+Read(leg,"NeedsQuery")+" tolerance="+Read(leg,"ArrivalTolerance")+
                                " targetSampleDistance="+(actor==null?"UNKNOWN":actor.Settings.TargetSampleDistance.ToString("R"))+
                                " snapshot="+snapshot?.Version+"/"+snapshot?.BodyVersion+" resource="+request.ResourceId+
                                " radius="+request.Radius+" safety="+request.SafetyMargin+" from="+P(from)+" to="+P(to)+
                                " cachedPathBefore=["+Path(Read(leg,"Path") as Vector3[])+"] suffix=["+Path(Read(leg,"Suffix") as Vector3[])+"]"+
                                " proofResource="+proof?.ResourceId+" proofWaypoints=["+Path(proof?.Waypoints)+"] proofExpanded=["+Path(proof?.ExpandedPath)+"] proofEnds=["+(proof==null?"UNKNOWN":string.Join(",",proof.LegEnds))+"]";
                        }
                        else if(matches.Length>1)Error("UNKNOWN multiple original actual-leg owners for query input");
                    }
                    catch(Exception e){Error("query-input-capture/"+e.GetType().Name);}
                    void Capture(System.Collections.Generic.IReadOnlyList<Vector3> raw,Exception exception)
                    {
                        if(finished || request==null)return;
                        try
                        {
                            if(!ReferenceEquals(Read(entry,"Request"),request) || !ReferenceEquals(Read(entry,"Leg"),leg)){Error("UNKNOWN source changed inside original query");return;}
                            if(!latest.ContainsKey(request.Intent.ActorId) && latest.Count>=16){Error("UNKNOWN live-capture cap16 exceeded");return;}
                            var endpoints=raw!=null && raw.Count>0;
                            latest[request.Intent.ActorId]=(frame,"MATRIX_ORIGINAL_ACTUAL_LEG_QUERY frame="+frame+" "+beforeFacts+
                                " rawRef="+Ref(raw)+" rawCount="+(raw==null?-1:raw.Count)+" raw=["+Path(raw)+"]"+
                                " firstDistance="+(endpoints?Vector3.Distance(raw[0],from).ToString("R"):"UNKNOWN")+
                                " lastDistance="+(endpoints?Vector3.Distance(raw[raw.Count-1],to).ToString("R"):"UNKNOWN")+
                                " originalException="+(exception==null?"NONE":exception.GetType().FullName+"/"+exception.Message));captured++;
                        }
                        catch(Exception e){Error("query-output-capture/"+e.GetType().Name);}
                    }
                    System.Collections.Generic.IReadOnlyList<Vector3> result;
                    try{result=original(from,to);}catch(Exception e){Capture(null,e);throw;}
                    Capture(result,null);return result;
                };
                corridorPrior=corridorField.GetValue(observed) as Delegate;
                Action<string,string,string,Vector3,System.Collections.Generic.IReadOnlyList<Vector3>,Vector3[],string> corridor=(operation,id,other,target,path,obstacles,result)=>
                {
                    if(finished || records.Count>=8 || operation!="route.blocked" || string.IsNullOrEmpty(id) || result==null || !result.Contains("actual-leg-proof-failed"))return;
                    try
                    {
                        if(latest.TryGetValue(id,out var originalQuery) && originalQuery.Frame==Time.frameCount)
                        {exact++;records.Add(originalQuery.Text+" associatedOriginalRouteBlocked="+result+" failureFrame="+Time.frameCount+" association=SameActorAndOriginalFrame");}
                        else{records.Add("MATRIX_ORIGINAL_ACTUAL_LEG_QUERY association=NOT_PROVEN actor="+id+" failureFrame="+Time.frameCount+" reason="+result);Error("No same-frame original query: cached-leg or uncaptured branch remains UNKNOWN");}
                    }
                    catch(Exception e){Error("route-block-association/"+e.GetType().Name);}
                };
                corridorInstalled=Delegate.Combine(corridorPrior,corridor);
                queryField.SetValue(core,wrapped);corridorField.SetValue(observed,corridorInstalled);
                TestContext.WriteLine("MATRIX_LEG_QUERY_INSTALL status=INSTALLED selected=FloorEdge cap=8 existingDelegateWrapper=True addedQueries=0 cleanupFallback=True");
            }
            catch(Exception e){Error("INSTALL_FAILED/"+e.GetType().Name+"/"+e.Message);finish();}
            return finish;
        }

        // Default off. Observe the cohort head's ORIGINAL traffic preflight stored handoff only.
        // 只读原字段；不重算IsCurrent/claim policy，不查询路径，不刷新或修改游戏。
        private Action BeginMatrixOriginalHandoff()
        {
            if(!Environment.GetCommandLineArgs().Contains("-matrixOriginalHandoff"))return null;
            const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
            object Read(object value,string name)
            {
                if(value==null)return null;
                var field=value.GetType().GetField(name,flags)??value.GetType().GetField("<"+name+">k__BackingField",flags);
                if(field==null)throw new MissingFieldException(value.GetType().Name,name);
                return field.GetValue(value);
            }
            string Ref(object value)=>value==null?"NULL":value.GetType().Name+"#"+System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
            string Key(TrafficIntentKey key)=>key.Session+"/e"+key.Epoch+"/"+key.ActorId+"/"+key.IntentId+"/g"+key.Generation+"/r"+key.LayoutRevision+"/"+key.Context;
            var records=new System.Collections.Generic.List<string>();var errors=new System.Collections.Generic.List<string>();
            var seen=new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            System.Reflection.FieldInfo hook=null;Delegate prior=null,installed=null;object core=null;
            var observedCustomers=customers;var cleanupPrior=parkingEndpointWitnessCleanup;Action cleanupInstalled=null;
            string watched=null;var finished=false;var restored=false;var cleanupRestored=false;
            Action finish=()=>
            {
                if(finished)return;finished=true;
                // Root teardown invokes this first even when nested IEnumerator failure skips MatrixRun.finally.
                // 先恢复本观察的fallback链；原cleanup仍按原顺序/原异常语义执行。
                try
                {
                    if(cleanupInstalled!=null)
                    {
                        if(ReferenceEquals(parkingEndpointWitnessCleanup,cleanupInstalled))
                        {parkingEndpointWitnessCleanup=cleanupPrior;cleanupRestored=true;}
                        else if(!ReferenceEquals(parkingEndpointWitnessCleanup,cleanupPrior))errors.Add("UNKNOWN: cleanup chain replaced externally");
                    }
                }
                catch(Exception error){errors.Add("cleanup-chain/"+error.GetType().Name);}
                try
                {
                    if(hook!=null && installed!=null)
                    {
                        if(ReferenceEquals(hook.GetValue(observedCustomers),installed)){hook.SetValue(observedCustomers,prior);restored=true;}
                        else errors.Add("UNKNOWN: hook replaced externally; replacement preserved");
                    }
                }
                catch(Exception error){errors.Add("cleanup/"+error.GetType().Name);}
                try
                {
                    TestContext.WriteLine("MATRIX_ORIGINAL_HANDOFF status="+(errors.Count>0?"UNKNOWN":records.Count>0?"OBSERVED":"NOT_OBSERVED")+
                        " records="+records.Count+" cap=8 watched="+(watched??"NONE")+" restored="+restored+" cleanupRestored="+cleanupRestored+" errors="+errors.Count+
                        " predicatesNOT_REEVALUATED=IsCurrentPhaseHandoff,TrafficReceiptCurrent,Claim.IsCurrent,AreSame,CanProgress;queries=0;refresh=0");
                    foreach(var record in records)TestContext.WriteLine(record);
                    foreach(var error in errors)TestContext.WriteLine("MATRIX_ORIGINAL_HANDOFF_ERROR "+error);
                }
                catch(Exception){} // Diagnostic cleanup/output must not replace the original test/game result.
            };
            try
            {
                // Finish-first multicast preserves every prior cleanup, including its original failure semantics.
                cleanupInstalled=(Action)Delegate.Combine(finish,cleanupPrior);parkingEndpointWitnessCleanup=cleanupInstalled;
                core=Read(observedCustomers,"traffic");
                hook=typeof(CustomerFlowController).GetField("CorridorObserver",flags);
                if(hook==null || core==null)throw new InvalidOperationException("Original traffic/hook unavailable");
                prior=hook.GetValue(observedCustomers) as Delegate;
                Action<string,string,string,Vector3,System.Collections.Generic.IReadOnlyList<Vector3>,Vector3[],string> observe=(operation,id,other,target,path,obstacles,result)=>
                {
                    if(finished || errors.Count>0 || records.Count>=8 || !matrixObserving || operation!="traffic.collection-prerequisite-preflight" || string.IsNullOrEmpty(id))return;
                    try
                    {
                        if(watched==null)watched=id;if(id!=watched)return;
                        if(!ReferenceEquals(Read(observedCustomers,"traffic"),core))throw new InvalidOperationException("Core replaced during original cohort");
                        var visits=Read(observedCustomers,"visits") as IDictionary;
                        var visit=visits!=null && visits.Contains(id)?visits[id]:null;
                        if(visit==null)throw new InvalidOperationException("Observed visit disappeared");
                        var pending=Read(visit,"PendingTraffic");var source=Read(pending,"Source") as TrafficDecision;
                        var handle=Read(pending,"Handle");var actor=Read(visit,"Actor");var admission=Read(visit,"Admission");
                        var entries=Read(core,"entries") as IDictionary;object entry=null;
                        var intent=Read(pending,"Intent");
                        if(intent!=null && entries!=null && entries.Contains(intent))entry=entries[intent];
                        var state=Read(entry,"PendingHandoff");var decision=Read(entry,"Decision") as TrafficDecision;
                        var request=Read(entry,"Request") as TrafficRequest;var leg=Read(entry,"Leg");
                        var snapshot=Read(core,"snapshot") as TrafficWorldSnapshot;
                        var body=snapshot?.Bodies.FirstOrDefault(value=>value.Owner.ActorId==id);
                        var claims=Read(pending,"Claims") as IEnumerable;
                        var claimsText=claims==null?"NULL":string.Join(";",claims.Cast<object>().Select(binding=>
                        {
                            var fact=Read(binding,"Fact") as TrafficParkingFact;
                            return Ref(binding)+"/kind="+Read(binding,"Kind")+"/fact="+Ref(fact)+"/resource="+fact?.ResourceId+
                                "/owner="+Ref(fact?.CanonicalOwner)+"/ownerVersion="+fact?.OwnerVersion+"/checkpoint="+Ref(fact?.SemanticCheckpoint)+
                                "/counterEntry="+Ref(Read(binding,"CounterEntry"))+"/pickupEntry="+Ref(Read(binding,"PickupEntry"))+"/token="+Ref(Read(binding,"Token"));
                        }));
                        var stateText="visit="+id+" phase="+Read(visit,"State")+" resume="+Read(visit,"ResumeState")+" generation="+Read(visit,"Generation")+
                            " actor="+Ref(actor)+" admission="+Ref(admission)+" pending="+Ref(pending)+" marked="+Read(pending,"Marked")+
                            " nativeGeneration="+Read(pending,"NativeGeneration")+" handle="+Ref(handle)+" source="+Ref(source)+
                            " sourceKey="+(source==null?"NULL":Key(source.Intent))+" sourceKind="+source?.Kind+" sourceAcq="+source?.AcquiredSequence+
                            " actorSamePending="+ReferenceEquals(actor,Read(pending,"Actor"))+" admissionSamePending="+ReferenceEquals(admission,Read(pending,"Admission"))+
                            " total="+Ref(Read(pending,"Total"))+" claims=["+claimsText+"] core="+Ref(core)+" closed="+Read(core,"closed")+
                            " entry="+Ref(entry)+" key="+(intent is TrafficIntentKey key?Key(key):"NULL")+
                            " entryKind="+decision?.Kind+" entryReason="+decision?.Reason+" disposition="+decision?.FailureDisposition+
                            " queued="+Read(entry,"Queued")+" prepared="+Read(entry,"Prepared")+" active="+Read(entry,"Active")+" retired="+Read(entry,"Retired")+
                            " firstFrame="+Read(entry,"FirstFrame")+" queries="+Read(entry,"Queries")+" waitRemaining="+Read(entry,"WaitRemaining")+
                            " finiteLimit="+Read(entry,"FiniteLimit")+" requestFiniteSeconds="+request?.FiniteSeconds+
                            " journeyPermit="+Ref(Read(entry,"JourneyPermit"))+" sourceSameJourney="+ReferenceEquals(source,Read(entry,"JourneyPermit"))+
                            " coreHandoff="+Ref(state)+" remaining="+Read(state,"Remaining")+" coreHandle="+Ref(Read(state,"Handle"))+
                            " handleSameCore="+(state!=null && ReferenceEquals(handle,Read(state,"Handle")))+
                            " sourceSameCore="+(state!=null && ReferenceEquals(source,Read(state,"Source")))+
                            " actorSameCore="+(state!=null && ReferenceEquals(actor,Read(state,"ActorLife")))+
                            " leg="+Ref(leg)+" completed="+Read(leg,"Completed")+" bound="+Read(leg,"Bound")+" legGeneration="+Read(leg,"Generation")+
                            " bodyKey="+(body==null?"NULL":Key(body.Owner))+" bodyLifeSame="+(body!=null && ReferenceEquals(body.ActorLife,actor))+
                            " snapshot="+snapshot?.Version+"/"+snapshot?.BodyVersion;
                        if(!seen.Add(stateText))return;
                        records.Add("MATRIX_ORIGINAL_HANDOFF_PRE_TRAFFIC_COLLECTION frame="+Time.frameCount+" sceneTime="+Time.time+
                            " captureDelta="+Time.captureDeltaTime+" speed="+time.CurrentSpeed+" event="+operation+" original="+result+" target="+target.ToString("F9")+" "+stateText);
                    }
                    catch(Exception error){errors.Add("capture/"+error.GetType().Name+":"+error.Message);}
                };
                installed=Delegate.Combine(prior,observe);hook.SetValue(observedCustomers,installed);
                TestContext.WriteLine("MATRIX_ORIGINAL_HANDOFF_INSTALL status=INSTALLED event=traffic.collection-prerequisite-preflight cap=8 cleanupFallback=True");
            }
            catch(Exception error){errors.Add("install/"+error.GetType().Name+":"+error.Message);}
            return finish;
        }

        // Grow real paid customers through 2/4/6 requests on the SAME legal recipe.
        // 不注入Order/slot/pose；真实物理上限不足时保留人数及CAPACITY_LIMITED证据。
        private IEnumerator MatrixPaidCohorts(string layoutId,int rotation,bool rapid)
        {
            tools.HoldCollection();
            var queue=CafeLoopRuntimeProof.Field<PickupQueueService>(customers,"pickupQueue");
            var slots=CafeLoopRuntimeProof.Field<Vector3[]>(queue,"waitSlots");
            var ids=new System.Collections.Generic.List<string>();
            string unpaid=null;var capabilityLimited=false;
            object heldEntry=null,heldAnchor=null,heldCursor=null;ServiceTrafficCoordinator heldCore=null;TrafficIntentKey heldKey=default;
            var heldQueries=0;object heldFirstFrame=null,heldSequence=null;
            const System.Reflection.BindingFlags fields=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
            object Read(object value,string name)=>value?.GetType().GetField(name,fields)?.GetValue(value);
            bool ActualUnpaidEligibilityRefusal(string id)
            {
                var visits=(IDictionary)Read(customers,"visits");if(!visits.Contains(id))return false;
                var visit=visits[id];var anchor=Read(visit,"PendingReservationAnchor");var core=Read(customers,"traffic") as ServiceTrafficCoordinator;
                if(anchor==null || core==null || !(bool)Read(anchor,"Submitted") || !(Read(anchor,"Key") is TrafficIntentKey key))return false;
                var entries=(IDictionary)Read(core,"entries");if(!entries.Contains(key))return false;
                var entry=entries[key];var source=Read(entry,"Decision") as TrafficDecision;
                if(!(Read(entry,"UnpaidEligibilityHeld") is bool held) || !held || Read(entry,"UnpaidEligibility")==null ||
                    source==null || source.Kind!=TrafficDecisionKind.DeferredByBudget || source.FailureDisposition!=TrafficFailureDisposition.None || source.AcquiredSequence!=0 ||
                    source.Reason!="unpaid-eligibility/endpoint-occupied" && source.Reason!="unpaid-eligibility/route-capability-unavailable")return false;
                return ReferenceEquals(Read(anchor,"Visit"),visit) && ReferenceEquals(Read(anchor,"Coordinator"),core) &&
                    employees.Snapshot.All(employee=>employee.State==EmployeeState.Idle && employee.HandleId==0) &&
                    customers.PickupAssignments.All(row=>row.Arrived && Vector3.Distance(world.RegisteredActors.Single(body=>body.ActorId==row.VisitId).transform.position,slots[row.SlotIndex])<=.081f) &&
                    (CustomerVisitState)Read(visit,"State")==CustomerVisitState.Queued && (bool)Read(visit,"CounterArrived") && !(bool)Read(visit,"PickupAssigned") &&
                    customers.CounterAssignments.Any(row=>row.VisitId==id && row.Arrived) && !owner.Snapshot.PaidVisitIds.Contains(id) &&
                    !owner.Snapshot.Orders.Values.Any(order=>order.CustomerId==id) && !queue.TryGetReservation(id,out _) &&
                    customers.Capacity.GetReservations().Any(r=>r.OwnerId==id && r.Kind==CapacityKind.TotalCustomers && r.State==ReservationState.Occupied) &&
                    customers.Capacity.GetReservations().Any(r=>r.OwnerId==id && r.Kind==CapacityKind.PickUp && r.State==ReservationState.Reserved);
            }
            var heldTimeoutObserved=false;
            void ObserveHeldTimeout()
            {
                if(heldTimeoutObserved || !capabilityLimited || !Environment.GetCommandLineArgs().Contains("-matrixUnpaidHeldTimeout"))return;
                heldTimeoutObserved=true;
                try
                {
                    TestContext.WriteLine(TwentyTrafficFailureTrace.CounterGapCurrent(world,customers,storedOnly:true));
                    string Ref(object value)=>value==null?"NULL":value.GetType().Name+"#"+System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
                    object Raw(object value,string name)=>value==null?null:(value.GetType().GetField(name,fields)??value.GetType().GetField("<"+name+">k__BackingField",fields))?.GetValue(value);
                    string Assignment(object value)=>value is QueueAssignment row?row.VisitId+"/"+row.SlotIndex+"/g"+row.Generation+"/arrived="+row.Arrived+"@"+row.Position.ToString("F6"):"NULL";
                    string Key(TrafficIntentKey key)=>key.Session+"/e"+key.Epoch+"/"+key.ActorId+"/"+key.IntentId+"/g"+key.Generation+"/r"+key.LayoutRevision+"/"+key.Context;
                    string Set(object value)=>value is IEnumerable values?string.Join(",",values.Cast<object>().Select(item=>item?.ToString()??"NULL")):"NULL";
                    string Bodies(TrafficWorldSnapshot snapshot)=>snapshot==null?"NULL":string.Join(";",snapshot.Bodies.Select(body=>Key(body.Owner)+"@"+body.Position.ToString("F6")+"/active="+body.ActorActive+"/independent="+body.IndependentlyProgressing));
                    string Roles(TrafficRequest request)=>request==null?"NULL":string.Join(";",request.Requirements.Select(role=>role.ResourceId+":"+role.Start.ToString("F6")+"->"+role.Goal.ToString("F6")));
                    var visits=Read(customers,"visits") as IDictionary;var visit=visits!=null && visits.Contains(unpaid)?visits[unpaid]:null;
                    var core=Read(customers,"traffic");var entries=Read(core,"entries") as IDictionary;
                    var entry=entries!=null && entries.Contains(heldKey)?entries[heldKey]:null;
                    var cursors=Read(customers,"trafficCandidates") as IDictionary;var cursor=cursors!=null && cursors.Contains(heldKey)?cursors[heldKey]:null;
                    var anchor=Read(visit,"PendingReservationAnchor");var decision=Read(heldEntry,"Decision") as TrafficDecision;
                    var request=Read(heldEntry,"Request") as TrafficRequest;var facts=Read(heldEntry,"UnpaidEligibilityFacts") as TrafficRequest;
                    var snapshot=Read(core,"snapshot") as TrafficWorldSnapshot;var jobs=Read(heldEntry,"Jobs") as IList;
                    var wakes=Read(core,"wakeups") as IEnumerable;
                    var hasWake=wakes!=null && wakes.Cast<object>().Any(item=>item is TrafficIntentKey key && key.Equals(heldKey));
                    var claim=Read(heldAnchor,"CounterClaim");var counter=Read(heldAnchor,"Counter");var counterEntries=Read(counter,"entries") as IList;
                    var claimEntry=Read(claim,"entry");var actor=Read(visit,"Actor") as NavigationActor;
                    var counterText=counterEntries==null?"NULL":string.Join(";",counterEntries.Cast<object>().Select(row=>Read(row,"Id")+"/slot="+Read(row,"Slot")+"/g="+Read(row,"Generation")+"@"+Read(row,"Position")+"/arrived="+Read(row,"Arrived")+"/moving="+Read(row,"Moving")+"/needs="+Read(row,"NeedsReplan")));
                    TestContext.WriteLine("MATRIX_UNPAID_HELD_TIMEOUT status="+(heldEntry!=null && heldAnchor!=null && heldCursor!=null?"OBSERVED":"UNKNOWN")+
                        " cap=1 frame="+Time.frameCount+" unpaid="+unpaid+" key="+Key(heldKey)+" sameCore="+ReferenceEquals(core,heldCore)+
                        " sameEntry="+ReferenceEquals(entry,heldEntry)+" sameAnchor="+ReferenceEquals(anchor,heldAnchor)+" sameCursor="+ReferenceEquals(cursor,heldCursor)+
                        " visit="+Ref(visit)+" state="+Read(visit,"State")+" failure="+Read(visit,"Failure")+" requestId="+Read(visit,"RequestId")+
                        " paidTask="+Ref(Read(visit,"PaidPickup"))+" decision="+decision?.Kind+"/"+decision?.Reason+"/"+decision?.FailureDisposition+
                        " sourceSeq="+decision?.RequestSequence+" sourceAcquired="+decision?.AcquiredSequence+
                        " retired="+Read(heldEntry,"Retired")+" discarded="+Read(heldEntry,"Discarded")+" queued="+Read(heldEntry,"Queued")+
                        " prepared="+Read(heldEntry,"Prepared")+" active="+Read(heldEntry,"Active")+" producerHeld="+Read(heldEntry,"ProducerPreviewHeld")+" hasWake="+hasWake+
                        " eligibility="+Ref(Read(heldEntry,"UnpaidEligibility"))+" held="+Read(heldEntry,"UnpaidEligibilityHeld")+" queryFault="+Read(heldEntry,"UnpaidEligibilityQueryFault")+
                        " originalFirst="+heldFirstFrame+" first="+Read(heldEntry,"FirstFrame")+" originalSeq="+heldSequence+" seq="+Read(heldEntry,"RequestSequence")+
                        " originalQueries="+heldQueries+" queries="+Read(heldEntry,"Queries")+" queryLimit="+Read(core,"queriesPerIntent")+
                        " finiteLimit="+Read(heldEntry,"FiniteLimit")+" waitRemaining="+Read(heldEntry,"WaitRemaining")+
                        " candidate="+Read(heldEntry,"CurrentIndex")+"/"+Read(heldEntry,"CandidateCount")+" attempts="+Set(Read(heldEntry,"AttemptedIndices"))+
                        " roundAttempts="+Set(Read(heldEntry,"UnpaidEligibilityAttempts"))+" job="+Read(heldEntry,"JobIndex")+"/"+(jobs==null?-1:jobs.Count)+
                        " cursorIndex="+Read(heldCursor,"CurrentIndex")+" cursorAttempts="+Set(Read(heldCursor,"AttemptedIndices"))+
                        " cursorRoundAttempts="+Set(Read(heldCursor,"UnpaidEligibilityAttempts"))+" cursorRound="+Ref(Read(heldCursor,"UnpaidEligibilityRound"))+
                        " cursorSource="+Ref(Read(heldCursor,"Source"))+" entrySource="+Ref(decision)+" staticInvalid="+Set(Read(heldCursor,"StaticInvalidIndices"))+
                        " anchorSubmitted="+Read(heldAnchor,"Submitted")+" anchorCursor="+Read(heldAnchor,"Cursor")+" side="+Read(heldAnchor,"SideIndex")+
                        " anchorGeneration="+Read(heldAnchor,"Generation")+" visitGeneration="+Read(visit,"Generation")+" phaseSequence="+Read(visit,"TrafficPhaseSequence")+
                        " anchorStart="+Read(heldAnchor,"Start")+" actualPose="+(actor==null?"NULL":actor.transform.position.ToString("F6"))+
                        " counterClaim="+Ref(claim)+" counterAssignment="+Assignment(Raw(claim,"Assignment"))+" capturedCounterVersion="+Raw(claim,"CapturedOwnerVersion")+
                        " sameCounter="+ReferenceEquals(counter,Read(customers,"queue"))+" sameClaimOwner="+ReferenceEquals(counter,Read(claim,"owner"))+
                        " claimEntryStillPresent="+(counterEntries!=null && counterEntries.Cast<object>().Any(row=>ReferenceEquals(row,claimEntry)))+
                        " claimFlags="+Read(claim,"arrived")+"/"+Read(claim,"moving")+"/"+Read(claim,"needsReplan")+"/"+Read(claim,"ownExiting")+
                        " claimRevision="+Read(claim,"preparationRevision")+" counterRevision="+Read(counter,"preparationRevision")+" counterExiting="+Read(counter,"exiting")+
                        " currentCounter=["+counterText+"]"+
                        " anchorEligibility="+Ref(Read(heldAnchor,"Eligibility"))+" cursorEligibility="+Ref(Read(heldCursor,"UnpaidEligibility"))+
                        " requestStart="+request?.Start.ToString("F6")+" target="+request?.Target.ToString("F6")+
                        " requestRoles=["+Roles(request)+"] heldRoles=["+Roles(facts)+"] requestBodies=["+Bodies(request?.Snapshot)+"] heldBodies=["+Bodies(facts?.Snapshot)+"] currentBodies=["+Bodies(snapshot)+"]"+
                        " tokens=["+string.Join(";",customers.Capacity.GetReservations().Where(token=>token.OwnerId==unpaid).Select(token=>token.Kind+"/"+token.State))+"] observationAddedQueries=0 guardReplay=0");
                }
                catch(Exception error){TestContext.WriteLine("MATRIX_UNPAID_HELD_TIMEOUT status=UNKNOWN "+error.GetType().Name+":"+error.Message);}
            }
            var originalTokens=new System.Collections.Generic.Dictionary<string,CapacityToken[]>();
            var exited=new System.Collections.Generic.HashSet<string>();var observing=true;matrixObserving=true;
            void ObserveActualExit(string id)
            {
                var body=world.RegisteredActors.Single(a=>a.ActorId==id);var total=customers.Capacity.GetReservations().Single(r=>r.OwnerId==id && r.Kind==CapacityKind.TotalCustomers).Token;
                var observer=body.gameObject.AddComponent<CafeActualExitObserver>();observer.VisitId=id;
                observer.Receipt=(visit,pose)=>{
                    if(!observing || !matrixObserving)return;
                    Assert.That(Vector3.Distance(pose,CafeLoopRuntimeProof.Field<Transform>(customers,"exit").position),Is.LessThanOrEqualTo(.081f));
                    Assert.That(customers.Capacity.GetReservations().Single(r=>ReferenceEquals(r.Token,total)).State,Is.EqualTo(ReservationState.Released));
                    Assert.That(owner.Snapshot.Orders.Values.Single(o=>o.CustomerId==visit).State,Is.EqualTo(OrderState.Completed));
                    Assert.That(exited.Add(visit),Is.True,"Exactly one real near-exit receipt per original body");
                };
            }
            try
            {
            foreach(var requested in new[]{2,4,6})
            {
                var target=Math.Min(requested,slots.Length);
                while(ids.Count<target && !capabilityLimited)
                {
                    if(!rapid)yield return MatrixSettled(()=>employees.Snapshot.All(e=>e.State==EmployeeState.Idle) &&
                        customers.PickupAssignments.All(a=>a.Arrived && Vector3.Distance(a.Position,slots[a.SlotIndex])<.001f),"before controlled next arrival");
                    yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000);
                    Assert.That(customers.TrySpawn(),Is.True,customers.Status);
                    var id=customers.Snapshot.Single(v=>!originalTokens.ContainsKey(v.VisitId)).VisitId;
                    originalTokens.Add(id,customers.Capacity.GetReservations().Where(r=>r.OwnerId==id).Select(r=>r.Token).ToArray());
                    ObserveActualExit(id);
                    // The same finite body must either really arrive Paid or prove a typed prepay spatial refusal.
                    // 只接受真实业务资格拒绝，不把普通budget Deferred、未到位或Terminal当作安全容量已满。
                    yield return MatrixSettled(()=>customers.PickupAssignments.Any(a=>a.VisitId==id && a.Arrived) || ActualUnpaidEligibilityRefusal(id),"new Paid arrival or actual safe-capacity refusal");
                    if(ActualUnpaidEligibilityRefusal(id))
                    {
                        unpaid=id;capabilityLimited=true;heldCore=(ServiceTrafficCoordinator)Read(customers,"traffic");
                        heldAnchor=Read(((IDictionary)Read(customers,"visits"))[id],"PendingReservationAnchor");heldKey=(TrafficIntentKey)Read(heldAnchor,"Key");
                        heldEntry=((IDictionary)Read(heldCore,"entries"))[heldKey];heldCursor=((IDictionary)Read(customers,"trafficCandidates"))[heldKey];
                        heldQueries=(int)Read(heldEntry,"Queries");heldFirstFrame=Read(heldEntry,"FirstFrame");heldSequence=Read(heldEntry,"RequestSequence");
                        Assert.That(customers.TryReservePickup(id,out _),Is.False,"The original actual owner refused unsafe future Exit capability before payment");
                        var originalPose=world.RegisteredActors.Single(body=>body.ActorId==id).transform.position;
                        for(var frame=0;frame<90;frame++)
                        {
                            yield return null;Assert.That(ActualUnpaidEligibilityRefusal(id),Is.True,"Unchanged held geometry cannot become a free grant or a generic Terminal");
                            Assert.That(((IDictionary)Read(heldCore,"entries"))[heldKey],Is.SameAs(heldEntry));
                            Assert.That(Read(((IDictionary)Read(customers,"visits"))[id],"PendingReservationAnchor"),Is.SameAs(heldAnchor));
                            Assert.That(((IDictionary)Read(customers,"trafficCandidates"))[heldKey],Is.SameAs(heldCursor));
                            Assert.That(Read(heldEntry,"Queries"),Is.EqualTo(heldQueries));Assert.That(Read(heldEntry,"FirstFrame"),Is.EqualTo(heldFirstFrame));
                            Assert.That(Read(heldEntry,"RequestSequence"),Is.EqualTo(heldSequence));
                            Assert.That(world.RegisteredActors.Single(body=>body.ActorId==id).transform.position,Is.EqualTo(originalPose));
                            CollectionAssert.AreEqual(originalTokens[id],customers.Capacity.GetReservations().Where(r=>r.OwnerId==id).Select(r=>r.Token));
                        }
                        TestContext.WriteLine("MATRIX_SAFE_CAPACITY_REFUSAL unpaid="+id+" actualPaid="+ids.Count+" requested="+requested+
                            " key="+heldKey.Session+"/e"+heldKey.Epoch+"/"+heldKey.ActorId+"/"+heldKey.IntentId+"/g"+heldKey.Generation+
                            " originalFrame="+heldFirstFrame+" originalSequence="+heldSequence+" spentQueries="+heldQueries+" noPaidOrOrder=True sameBodyAndTokens=True");
                        break;
                    }
                    ids.Add(id);
                }
                if(capabilityLimited)target=ids.Count; // Observed refusal, not a guessed geometric limit.
                // Temporary yield is permitted; only test permanent chain after all
                // assigned targets have returned to their original physical slots.
                yield return MatrixSettled(()=>employees.Snapshot.All(e=>e.State==EmployeeState.Idle) &&
                    customers.PickupAssignments.Count==target && customers.PickupAssignments.All(a=>a.Arrived &&
                    Vector3.Distance(a.Position,slots[a.SlotIndex])<.001f),"stable cohort after employee return");
                var stable=customers.PickupAssignments.ToArray();
                CollectionAssert.AreEqual(ids,stable.Select(a=>a.VisitId),"Real paid FIFO, independent of facing");
                Assert.That(owner.Snapshot.Orders.Values.Count(o=>ids.Contains(o.CustomerId)),Is.EqualTo(target));
                Assert.That(stable.Select(a=>a.SlotIndex).Distinct().Count(),Is.EqualTo(target));
                var poses=stable.Select(a=>world.RegisteredActors.Single(body=>body.ActorId==a.VisitId).transform.position).ToArray();
                for(var i=0;i<target;i++)
                {
                    Assert.That(stable[i].Generation,Is.GreaterThan(0));
                    Assert.That(Vector3.Distance(poses[i],slots[i]),Is.LessThanOrEqualTo(.081f),"Actual stable body, not just assignment/Facing");
                    CollectionAssert.AreEqual(originalTokens[ids[i]],customers.Capacity.GetReservations().Where(r=>r.OwnerId==ids[i]).Select(r=>r.Token));
                    for(var j=0;j<i;j++)Assert.That(Vector3.Distance(poses[i],poses[j]),Is.GreaterThanOrEqualTo(.939f),"1.1m slots with independent .08m arrival error per body");
                    if(i==0)continue;
                    var planned=(slots[i]-slots[i-1]).normalized;var actual=(poses[i]-poses[i-1]).normalized;
                    Assert.That(Vector3.Dot(planned,actual),Is.GreaterThan(.98f),"World-space queue direction must match actual stable positions");
                    Assert.That(Vector3.Distance(slots[i],slots[i-1]),Is.InRange(1.099f,1.376f));
                    var body=world.RegisteredActors.Single(a=>a.ActorId==ids[i]);
                    Assert.That(Vector3.Dot(body.transform.forward,(poses[i-1]-poses[i]).normalized),Is.GreaterThan(.9f),"Facing is separate from queue geometry");
                    var incoming=i==1?CafeLoopRuntimeProof.Field<Vector3>(customers,"pickupOutward"):(slots[i-1]-slots[i-2]).normalized;
                    if(Vector3.Dot(incoming,planned)<.99f)
                        AssertOriginalForwardRejection(layoutId,rotation,i,slots,incoming);
                }
                var status=target==requested?"PAID_WAITERS_VERIFIED":"CAPACITY_LIMITED";
                var receipt=new PaidCohortRecord{layoutId=layoutId,utc=DateTime.UtcNow.ToString("O"),variant=rotation,requested=requested,actual=target,physicalSlots=slots.Length,status=status,poses=poses,slots=slots};
                var json=JsonUtility.ToJson(receipt);var path=Path.Combine("outputs","phase14","single-file","paid-cohorts.jsonl");
                Directory.CreateDirectory(Path.GetDirectoryName(path));File.AppendAllText(path,json+Environment.NewLine);TestContext.WriteLine("PAID_COHORT "+json);
            }
            if(unpaid==null && slots.Length<6 && customers.TrySpawn())
            {
                unpaid=customers.Snapshot.Single(v=>!ids.Contains(v.VisitId)).VisitId;
                originalTokens.Add(unpaid,customers.Capacity.GetReservations().Where(r=>r.OwnerId==unpaid).Select(r=>r.Token).ToArray());
                ObserveActualExit(unpaid);
                yield return MatrixSettled(()=>customers.Snapshot.Any(v=>v.VisitId==unpaid && v.State==CustomerVisitState.Queued),"unpaid physical-capacity witness");
                var orderCount=owner.Snapshot.Orders.Count;
                Assert.That(customers.TryReservePickup(unpaid,out _),Is.False,"Physical queue is really full");
                for(var f=0;f<90;f++)yield return null;
                Assert.That(owner.Snapshot.Orders.Count,Is.EqualTo(orderCount));
                Assert.That(owner.Snapshot.Orders.Values.Any(o=>o.CustomerId==unpaid),Is.False,"No charge before a physical wait reservation");
            }
            var followers=customers.PickupAssignments.Skip(1).ToArray();
            var previous=followers.ToDictionary(a=>a.VisitId,a=>world.RegisteredActors.Single(b=>b.ActorId==a.VisitId).transform.position);
            var advancedIds=new System.Collections.Generic.HashSet<string>();
            var advanced=false;Action<CafeLoopSnapshot> watch=s=>{
                StageControlledPendingRecovery();
                ObserveFiniteExitDependency();
                foreach(var old in followers)
                {
                    if(advancedIds.Contains(old.VisitId))continue;
                    var now=customers.PickupAssignments.FirstOrDefault(a=>a.VisitId==old.VisitId);
                    if(now.VisitId==null || now.SlotIndex>=old.SlotIndex || !now.Arrived)continue;
                    if(Vector3.Distance(now.Position,slots[now.SlotIndex])>.001f)continue; // A temporary yield/service arrival is not stable queue advance.
                    if(!customers.Snapshot.Any(v=>v.VisitId==old.VisitId && v.State==CustomerVisitState.PickupWaiting))continue;
                    var entries=CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(customers,"visits");var entry=entries[old.VisitId];
                    if((long)entry.GetType().GetField("RequestId").GetValue(entry)!=0 || (int)entry.GetType().GetField("DeliveryYieldPhase").GetValue(entry)!=0)continue;
                    var pose=world.RegisteredActors.Single(b=>b.ActorId==old.VisitId).transform.position;
                    Assert.That(Vector3.Distance(pose,slots[now.SlotIndex]),Is.LessThanOrEqualTo(.081f));
                    Assert.That(now.Generation,Is.GreaterThan(old.Generation));
                    Assert.That(Vector3.Distance(pose,previous[old.VisitId]),Is.GreaterThan(.9f));advanced=true;advancedIds.Add(old.VisitId);
                }
            };
            owner.SnapshotChanged+=watch;
            try
            {
                tools.ResumeCollection();
                yield return MatrixSettled(()=>customers.Snapshot.Count==0 && employees.Snapshot.All(e=>e.State==EmployeeState.Idle) &&
                    customers.Capacity.GetCapacities().All(c=>c.Used==0) && exited.Count==originalTokens.Count,"complete cohort actual drain and last end-of-frame destruction receipt",ObserveHeldTimeout);
                Assert.That(followers.Length==0 || advanced,Is.True,"Collection must cause actual follower advance");
                if(capabilityLimited)
                {
                    Assert.That(((IDictionary)Read(heldCore,"entries"))[heldKey],Is.SameAs(heldEntry),"Actual earlier-owner release must resume the same original prepay source");
                    Assert.That(Read(heldEntry,"FirstFrame"),Is.EqualTo(heldFirstFrame));Assert.That(Read(heldEntry,"RequestSequence"),Is.EqualTo(heldSequence));
                    Assert.That((int)Read(heldEntry,"Queries"),Is.GreaterThanOrEqualTo(heldQueries),"Spent Native queries never reset");
                    Assert.That(owner.Snapshot.PaidVisitIds.Contains(unpaid),Is.True);
                    Assert.That(owner.Snapshot.Orders.Values.Single(order=>order.CustomerId==unpaid).State,Is.EqualTo(OrderState.Completed));
                    Assert.That(exited.Contains(unpaid),Is.True,"The uncharged held body must later receive real service and physically Exit, never be removed by fixture cleanup");
                    TestContext.WriteLine("MATRIX_SAFE_CAPACITY_RECOVERED unpaid="+unpaid+" sameEntryAndAge=True originalTokens=True actualCompletedAndExit=True");
                }
                foreach(var pair in originalTokens)
                {
                    var records=customers.Capacity.GetReservations().Where(r=>r.OwnerId==pair.Key).ToArray();
                    CollectionAssert.AreEqual(pair.Value,records.Select(r=>r.Token));
                    Assert.That(records.All(r=>r.State==ReservationState.Released),Is.True);
                    Assert.That(owner.Snapshot.Orders.Values.Single(o=>o.CustomerId==pair.Key).State,Is.EqualTo(OrderState.Completed));
                }
                Assert.That(owner.Snapshot.Delivery,Is.Empty);Assert.That(Find<PickupCupDisplay>().VisibleOrders,Is.Empty);
                CollectionAssert.AreEquivalent(originalTokens.Keys,exited,"Every original body physically reached the exit before releasing its original total token");
                if(TestContext.CurrentContext.Test.Name.StartsWith("ControlledPendingCollectedExitRecovery"))
                    Assert.That(controlledRecoveryStaged,Is.True,"Controlled ring-nil/Grid-complete recovery boundary must actually occur");
                TestContext.WriteLine("PAID_COHORT_DRAIN "+layoutId+"/"+rotation+" actual="+ids.Count+" extraUnpaid="+(unpaid!=null)+" advanced="+advanced+" originalTokensReleased=True");
            }
            finally {owner.SnapshotChanged-=watch;}
            }
            finally {observing=false;matrixObserving=false;}
        }

        private void AssertOriginalForwardRejection(string layoutId,int rotation,int index,Vector3[] slots,Vector3 incoming)
        {
            var records=CafeLoopRuntimeProof.Field<System.Collections.IEnumerable>(customers,"pickupPlanningRejections");
            const System.Reflection.BindingFlags fields=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            object match=null;
            foreach(var record in records)
            {
                T Value<T>(string name)=>(T)record.GetType().GetField(name,fields).GetValue(record);
                var prefix=Value<System.Collections.Generic.IReadOnlyList<Vector3>>("Prefix");
                if(prefix.Count!=index || Vector3.Distance(Value<Vector3>("Previous"),slots[index-1])>.001f ||
                    Vector3.Distance(Value<Vector3>("Direction"),incoming)>.001f ||
                    Vector3.Distance(Value<Vector3>("Point"),slots[index-1]+incoming*1.1f)>.001f ||
                    Enumerable.Range(0,index).Any(p=>Vector3.Distance(prefix[p],slots[p])>.001f))continue;
                match=record;break;
            }
            Assert.That(match,Is.Not.Null,"Every actual stable turn needs the original rejected forward gate on its FINAL selected prefix");
            var reason=(string)match.GetType().GetField("Reason",fields).GetValue(match);
            Assert.That(new[]{"isLegal","duplicate-spacing","canReach"},Does.Contain(reason));
            TestContext.WriteLine("SINGLE_FILE_TURN layout="+layoutId+" variant="+rotation+" slot="+index+" originalForward="+
                (slots[index-1]+incoming*1.1f).ToString("F6")+" originalGate="+reason+" finalPrefixMatched=True");
        }

        private IEnumerator MatrixSettled(Func<bool> done,string stage,Action onTimeout=null)
        {
            string Describe()=>stage+" staff="+string.Join(";",employees.Snapshot.Select(e=>e.Role+"/"+e.State+"/"+e.Failure))+
                " customers="+string.Join(";",customers.Snapshot.Select(v=>v.VisitId+"/"+v.State+"@"+v.Position.ToString("F6")))+
                " slots="+string.Join(";",customers.PickupAssignments.Select(a=>a.VisitId+"/"+a.SlotIndex+"/"+a.Generation+"/"+a.Arrived+"@"+a.Position.ToString("F6")))+
                " bodies="+string.Join(";",world.RegisteredActors.Select(a=>a.ActorId+"@"+a.transform.position.ToString("F6")+"->"+a.Agent.destination.ToString("F6")+"/path="+a.Agent.pathStatus+"/has="+a.Agent.hasPath));
            for(var frame=0;frame<6000 && !done();frame++)
            {
                matrixTrafficHistory.Enqueue(MatrixTrafficDescription());while(matrixTrafficHistory.Count>8)matrixTrafficHistory.Dequeue();
                if(frame%600==0)TestContext.WriteLine("COHORT_STATE "+Describe());
                if(employees.Snapshot.Any(e=>e.State==EmployeeState.Blocked) || customers.Snapshot.Any(v=>v.State==CustomerVisitState.Blocked))
                {
                    // Existing opt-in cold probe also observes the original immediate Blocked failure point.
                    onTimeout?.Invoke();
                    if(TestContext.CurrentContext.Test.Name.Contains("FloorEdge")) DiagnoseFloorEdgeExitCertificate();
                    var entries=CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(customers,"visits");
                    foreach(System.Collections.DictionaryEntry pair in entries)
                    {
                        var value=pair.Value;var type=value.GetType();
                        object Read(string name)=>type.GetField(name).GetValue(value);
                        TestContext.WriteLine("COHORT_FAILURE_PRIVATE "+pair.Key+" Request="+Read("RequestId")+" PendingPickup="+Read("PendingPickupSegment")+
                            " PendingExit="+Read("PendingExitSegment")+" Segments="+Read("Segments")+" Replans="+Read("Replans")+
                            " Yield="+Read("DeliveryYieldPhase")+" Failure="+Read("Failure")+" Final="+((NavigationTarget)Read("FinalTarget")).Position+
                            " Route="+string.Join(";",(System.Collections.Generic.Queue<Vector3>)Read("Route")??new System.Collections.Generic.Queue<Vector3>()));
                    }
                    Assert.Fail(Describe());
                }
                var bodies=world.RegisteredActors.ToArray();var before=bodies.Select(b=>b.transform.position).ToArray();
                ObserveDeliveryStart();yield return null;
                for(var i=0;i<bodies.Length;i++)
                {
                    if(bodies[i]==null)continue;var delta=bodies[i].transform.position-before[i];
                    Assert.That(delta.magnitude,Is.LessThanOrEqualTo(bodies[i].Settings.MaxSpeed*Time.deltaTime+.003f));
                    Assert.That(adapter.IsPointWalkable(bodies[i].transform.position),Is.True);
                    for(var j=0;j<i;j++)
                    {
                        if(bodies[j]==null)continue;var relative=before[i]-before[j];var change=delta-(bodies[j].transform.position-before[j]);
                        var t=change.sqrMagnitude<1e-12f?0:Mathf.Clamp01(-Vector3.Dot(relative,change)/change.sqrMagnitude);
                        Assert.That((relative+change*t).magnitude,Is.GreaterThanOrEqualTo(.899f),"Continuous real full-body separation");
                    }
                }
            }
            var completed=done();if(!completed)onTimeout?.Invoke();
            Assert.That(completed,Is.True,Describe());
        }

        private string MatrixTrafficDescription()
        {
            var entries=CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(customers,"visits");
            var values=new System.Collections.Generic.List<string>();
            foreach(System.Collections.DictionaryEntry pair in entries)
            {
                var value=pair.Value;object Read(string name)=>value.GetType().GetField(name).GetValue(value);
                values.Add(pair.Key+"/"+Read("State")+"@"+((NavigationActor)Read("Actor")).transform.position.ToString("F6")+" request="+Read("RequestId")+
                    " segment="+((NavigationTarget)Read("CurrentSegmentTarget")).Position.ToString("F6")+" route="+string.Join(";",(System.Collections.Generic.Queue<Vector3>)Read("Route")??new System.Collections.Generic.Queue<Vector3>())+
                    " canCollect="+customers.CanCollectPickup((string)pair.Key));
            }
            return "frame="+Time.frameCount+" holdCollection="+owner.HoldCollection+" orders="+string.Join(";",owner.Snapshot.Orders.Select(o=>o.Value.CustomerId+"/"+o.Value.State))+
                " delivery="+string.Join(";",owner.Snapshot.Delivery.Select(d=>d.VisitId))+" visits="+string.Join(" | ",values);
        }
        private void DiagnoseCollectionHandoff()
        {
            var entries=CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(customers,"visits");
            var activeExits=customers.Snapshot.Where(v=>v.State==CustomerVisitState.Exiting && (long)entries[v.VisitId].GetType().GetField("RequestId").GetValue(entries[v.VisitId])>0).ToArray();
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var obstacles=typeof(CustomerFlowController).GetMethod("RouteObstacles",flags);var plan=typeof(CustomerFlowController).GetMethod("PlanPickupExit",flags);
            foreach(var front in customers.Snapshot.Where(v=>v.State==CustomerVisitState.PickupService))
            {
                var body=world.RegisteredActors.Single(a=>a.ActorId==front.VisitId);var occupied=(Vector3[])obstacles.Invoke(customers,new object[]{front.VisitId,null,false});
                var without=occupied.Where(p=>!activeExits.Any(v=>p==v.Position)).ToArray();var destination=CafeLoopRuntimeProof.Field<Transform>(customers,"exit").position;
                var actual=plan.Invoke(customers,new object[]{body.transform.position,destination,occupied,body.Settings});
                var selectionOnly=plan.Invoke(customers,new object[]{body.transform.position,destination,without,body.Settings});
                TestContext.WriteLine("COLLECTION_HANDOFF actualExitPlan="+(actual!=null)+" removingOnlyAcceptedExitBodyPlan="+(selectionOnly!=null)+" actualCanCollect="+customers.CanCollectPickup(front.VisitId)+" canonicalReady="+
                    owner.Snapshot.Orders.Values.Any(o=>o.CustomerId==front.VisitId && o.State==OrderState.ReadyForPickup)+" realDelivery="+owner.Snapshot.Delivery.Any(d=>d.VisitId==front.VisitId));
                foreach(var older in activeExits)
                {
                    var actor=world.RegisteredActors.Single(a=>a.ActorId==older.VisitId);var value=entries[older.VisitId];var target=(NavigationTarget)value.GetType().GetField("CurrentSegmentTarget").GetValue(value);
                    var others=(Vector3[])obstacles.Invoke(customers,new object[]{older.VisitId,null,false});
                    var clearance=actor.Settings.AgentRadius+world.RegisteredActors.Max(a=>a.Settings.AgentRadius)+actor.Settings.CollisionSkin+actor.Settings.Epsilon;
                    TestContext.WriteLine("COLLECTION_PREDECESSOR actualRequest="+value.GetType().GetField("RequestId").GetValue(value)+" independentlyClearRemaining="+
                        CustomerRoutePlanner.IsClear(adapter.GetCompletePath(actor.transform.position,target.Position),others,clearance)+" pose="+actor.transform.position.ToString("F6")+" target="+target.Position.ToString("F6"));
                }
            }
        }

        private void StageControlledPendingRecovery()
        {
            if(controlledRecoveryStaged || !TestContext.CurrentContext.Test.Name.StartsWith("ControlledPendingCollectedExitRecovery"))return;
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var entries=CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(customers,"visits");
            foreach(System.Collections.DictionaryEntry pair in entries)
            {
                var entry=pair.Value;var type=entry.GetType();
                if((CustomerVisitState)type.GetField("State").GetValue(entry)!=CustomerVisitState.Exiting ||
                    (long)type.GetField("RequestId").GetValue(entry)==0)continue;
                var actor=(NavigationActor)type.GetField("Actor").GetValue(entry);var target=(NavigationTarget)type.GetField("FinalTarget").GetValue(entry);
                var occupied=(Vector3[])typeof(CustomerFlowController).GetMethod("RouteObstacles",flags).Invoke(customers,new object[]{pair.Key,null,false});
                var ring=typeof(CustomerFlowController).GetMethod("BuildRoute",flags).Invoke(customers,new object[]{actor.transform.position,target.Position,pair.Key,actor.Settings,null,false});
                var full=typeof(CustomerFlowController).GetMethod("PlanPickupExit",flags).Invoke(customers,new object[]{actor.transform.position,target.Position,occupied,actor.Settings});
                if(ring!=null || full==null)continue;
                var oldRequest=(long)type.GetField("RequestId").GetValue(entry);var oldGeneration=(int)type.GetField("Generation").GetValue(entry);var pose=actor.transform.position;
                typeof(CustomerFlowController).GetMethod("HoldVisit",flags).Invoke(customers,new object[]{entry,false});
                Assert.That((long)type.GetField("RequestId").GetValue(entry),Is.Zero);
                Assert.That((int)type.GetField("Generation").GetValue(entry),Is.EqualTo(oldGeneration+1));
                type.GetField("State").SetValue(entry,CustomerVisitState.Exiting);type.GetField("PendingExitSegment").SetValue(entry,true);type.GetField("Route").SetValue(entry,null);
                Assert.That(actor.transform.position,Is.EqualTo(pose));controlledRecoveryStaged=true;
                TestContext.WriteLine("CONTROLLED_PENDING_EXIT_RECOVERY HoldVisit=True retiredRequest="+oldRequest+" originalRingNil=True completeGridPlan=True RouteSetNil=True originalPose/visit/order/tokensPreserved=True");
                return;
            }
        }

        [UnityTest] public IEnumerator ControlledPendingCollectedExitNextLegKeepsRouteAndBudgetDuringIndependentPickup()
        {
            var tracking=true;var exits=new System.Collections.Generic.HashSet<string>();
            void Receipt(string id)
            {
                var actor=world.RegisteredActors.Single(a=>a.ActorId==id);var total=customers.Capacity.GetReservations().Single(r=>r.OwnerId==id && r.Kind==CapacityKind.TotalCustomers).Token;
                var observer=actor.gameObject.AddComponent<CafeActualExitObserver>();observer.VisitId=id;observer.Receipt=(visit,pose)=>{
                    if(!tracking)return;Assert.That(Vector3.Distance(pose,CafeLoopRuntimeProof.Field<Transform>(customers,"exit").position),Is.LessThanOrEqualTo(.081f));
                    Assert.That(customers.Capacity.GetReservations().Single(r=>ReferenceEquals(r.Token,total)).State,Is.EqualTo(ReservationState.Released));Assert.That(exits.Add(visit),Is.True);};
            }
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            try
            {
                yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000);
                Assert.That(customers.TrySpawn(),Is.True);var first=customers.Snapshot.Single().VisitId;Receipt(first);
                yield return Until(()=>owner.Snapshot.Delivery.Count==1 && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
                yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000);
                Assert.That(customers.TrySpawn(),Is.True);var second=customers.Snapshot.Single(v=>v.VisitId!=first).VisitId;Receipt(second);
                yield return Until(()=>owner.Snapshot.PaidVisitIds.Contains(second) && customers.Snapshot.Any(v=>v.VisitId==second && v.State==CustomerVisitState.MovingToPickup));
                tools.ResumeCollection();yield return Until(()=>customers.Snapshot.Any(v=>v.VisitId==first && v.State==CustomerVisitState.Exiting));
                // Controlled owner-step boundary: the independently accepted native pickup keeps moving.
                CafeLoopRuntimeProof.Set(owner,"stepping",true);
                var entries=CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(customers,"visits");var front=entries[first];var type=front.GetType();
                typeof(CustomerFlowController).GetMethod("HoldVisit",flags).Invoke(customers,new object[]{front,false});
                type.GetField("State").SetValue(front,CustomerVisitState.Exiting);type.GetField("PendingExitSegment").SetValue(front,true);type.GetField("Route").SetValue(front,null);
                var actor=world.RegisteredActors.Single(a=>a.ActorId==first);var goal=((NavigationTarget)type.GetField("FinalTarget").GetValue(front)).Position;
                var other=entries[second];var otherType=other.GetType();var otherActor=world.RegisteredActors.Single(a=>a.ActorId==second);
                var originalPose=otherActor.transform.position;System.Collections.Generic.Queue<Vector3> selected=null;
                var certificate=typeof(CustomerFlowController).GetMethod("CanWaitForIndependentPickupExitTraffic",flags);
                var liveRequest=(long)otherType.GetField("RequestId").GetValue(other);
                var liveGeneration=otherType.GetField("Generation").GetValue(other);
                var nativeRequests=CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(world.Service,"byId");
                Assert.That(liveRequest,Is.GreaterThan(0),"FIXTURE: independent pickup must still have its real accepted request");
                for(var frame=0;frame<600 && selected==null;frame++)
                {
                    Assert.That(otherType.GetField("RequestId").GetValue(other),Is.EqualTo(liveRequest),"FIXTURE: do not switch the independent request");
                    Assert.That(otherType.GetField("Generation").GetValue(other),Is.EqualTo(liveGeneration));
                    Assert.That(nativeRequests.Contains(liveRequest),Is.True,"FIXTURE: original accepted request must remain live");
                    // Keep the original 600-frame bound; selection starts only after the same native request really moved.
                    // 不换request、不改位置；真实移动前不采纳第0帧candidate，仍使用原有界窗口。
                    if(Vector3.Distance(originalPose,otherActor.transform.position)<=.1f){yield return null;continue;}
                    var occupied=(Vector3[])typeof(CustomerFlowController).GetMethod("RouteObstacles",flags).Invoke(customers,new object[]{first,null,false});
                    var without=occupied.Where(p=>Vector3.Distance(p,otherActor.transform.position)>.001f).ToArray();
                    var candidate=(System.Collections.Generic.IReadOnlyList<Vector3>)typeof(CustomerFlowController).GetMethod("PlanPickupExit",flags).Invoke(customers,new object[]{actor.transform.position,goal,without,actor.Settings});
                    var clearance=(float)typeof(CustomerFlowController).GetMethod("RouteClearance",flags).Invoke(customers,new object[]{actor.Settings});
                    var related=false;
                    if(candidate!=null && !CustomerRoutePlanner.IsClear(adapter.GetCompletePath(actor.transform.position,candidate[0]),occupied,clearance))
                    {
                        var proposed=new System.Collections.Generic.Queue<Vector3>(candidate);type.GetField("Route").SetValue(front,proposed);
                        try{related=(bool)certificate.Invoke(customers,new[]{front});if(related)selected=proposed;}
                        finally{type.GetField("Route").SetValue(front,null);}
                    }
                    if(!related)yield return null;
                }
                Assert.That(selected,Is.Not.Null,"Real moving pickup must obstruct the complete counterfactual next leg");
                Assert.That(Vector3.Distance(originalPose,otherActor.transform.position),Is.GreaterThan(.1f),"Independent current request really moved while owner dispatch was held");
                var accepted=otherType.GetField("RequestId").GetValue(other);var moving=otherType.GetField("State").GetValue(other);
                type.GetField("Route").SetValue(front,selected);Assert.That((bool)certificate.Invoke(customers,new[]{front}),Is.True);
                try{otherType.GetField("RequestId").SetValue(other,0L);Assert.That((bool)certificate.Invoke(customers,new[]{front}),Is.False);}finally{otherType.GetField("RequestId").SetValue(other,accepted);}
                try{otherType.GetField("State").SetValue(other,CustomerVisitState.Blocked);Assert.That((bool)certificate.Invoke(customers,new[]{front}),Is.False);}finally{otherType.GetField("State").SetValue(other,moving);}
                var allOccupied=(Vector3[])typeof(CustomerFlowController).GetMethod("RouteObstacles",flags).Invoke(customers,new object[]{first,null,false});
                var alternate=(System.Collections.Generic.IReadOnlyList<Vector3>)typeof(CustomerFlowController).GetMethod("PlanPickupExit",flags).Invoke(customers,new object[]{actor.transform.position,goal,allOccupied,actor.Settings});
                Assert.That(alternate,Is.Not.Null,"The old RED replanned safely; this case specifies selected-leg wait/budget policy, not an unsafe old dispatch");
                var physicalClearance=(float)typeof(CustomerFlowController).GetMethod("RouteClearance",flags).Invoke(customers,new object[]{actor.Settings});
                Assert.That(CustomerRoutePlanner.IsClear(adapter.GetCompletePath(actor.transform.position,alternate[0]),allOccupied,physicalClearance),Is.True);
                type.GetField("Route").SetValue(front,new System.Collections.Generic.Queue<Vector3>(alternate));
                try{Assert.That((bool)certificate.Invoke(customers,new[]{front}),Is.False,"An unrelated accepted moving body cannot delay this fully safe selected next leg");}
                finally{type.GetField("Route").SetValue(front,selected);}
                type.GetField("Route").SetValue(front,selected);var segments=type.GetField("Segments").GetValue(front);var replans=type.GetField("Replans").GetValue(front);
                var crossing=(bool)typeof(CustomerFlowController).GetMethod("ExitRouteConflictsWithEarlierRequest",flags).Invoke(customers,new[]{front});
                var departing=(bool)typeof(CustomerFlowController).GetMethod("CanWaitForDepartingExitBody",flags).Invoke(customers,new[]{front});
                Assert.That(crossing || departing,Is.False,"Both old earlier-Exit gates must be false so this reaches the independent-pickup next-leg seam");
                TestContext.WriteLine("CONTROLLED_NEXT_EXIT_LEG_PRECONDITION blockerState="+moving+" acceptedRequest="+accepted+
                    " actualNativeDisplacement="+Vector3.Distance(originalPose,otherActor.transform.position)+" nextPathFullGuard=False exactIndependentCertificate=True oldEarlierExitGates=False Request0/Blocked=False");
                typeof(CustomerFlowController).GetMethod("SendRouteSegment",flags).Invoke(customers,new[]{front,(object)CustomerVisitState.Exiting});
                TestContext.WriteLine("CONTROLLED_NEXT_EXIT_LEG_READBACK pending="+type.GetField("PendingExitSegment").GetValue(front)+" request="+type.GetField("RequestId").GetValue(front)+
                    " segments="+segments+"->"+type.GetField("Segments").GetValue(front)+" replans="+replans+"->"+type.GetField("Replans").GetValue(front));
                Assert.That(type.GetField("State").GetValue(front),Is.EqualTo(CustomerVisitState.Exiting));Assert.That((bool)type.GetField("PendingExitSegment").GetValue(front),Is.True);
                Assert.That((long)type.GetField("RequestId").GetValue(front),Is.Zero);Assert.That(type.GetField("Route").GetValue(front),Is.SameAs(selected));
                Assert.That(type.GetField("Segments").GetValue(front),Is.EqualTo(segments));Assert.That(type.GetField("Replans").GetValue(front),Is.EqualTo(replans));
                TestContext.WriteLine("CONTROLLED_NEXT_EXIT_LEG HoldVisit/OwnerStepGuard=True actualPickupMoved=True actualNextPathFullGuard=False independentCertificate=True Request0/Blocked/UnrelatedMovingBody=False originalRoute/segment/replanBudgetRetained=True");
                CafeLoopRuntimeProof.Set(owner,"stepping",false);
                yield return Until(()=>customers.Snapshot.Count==0 && exits.Count==2 && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
                Assert.That(owner.Snapshot.Orders.Count,Is.EqualTo(2));Assert.That(owner.Snapshot.Orders.Values.All(o=>o.State==OrderState.Completed),Is.True);
                Assert.That(customers.Capacity.GetCapacities().All(c=>c.Used==0),Is.True);
            }
            finally{tracking=false;CafeLoopRuntimeProof.Set(owner,"stepping",false);}
        }

        private void ObserveFiniteExitDependency()
        {
            if(exitDependencyObserved)return;
            var entries=CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(customers,"visits");
            var method=typeof(CustomerFlowController).GetMethod("CanWaitForIndependentPickupExitTraffic",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            foreach(System.Collections.DictionaryEntry pair in entries)
            {
                var entry=pair.Value;var type=entry.GetType();
                if(!(bool)type.GetField("PendingExitSegment").GetValue(entry) || !(bool)method.Invoke(customers,new[]{entry}))continue;
                exitDependencyObserved=true;Assert.That((bool)type.GetField("HasCollected").GetValue(entry),Is.True);
                var active=entries.Values.Cast<object>().Where(v=>(long)v.GetType().GetField("RequestId").GetValue(v)>0).ToArray();
                var requests=active.Select(v=>v.GetType().GetField("RequestId").GetValue(v)).ToArray();
                var states=active.Select(v=>v.GetType().GetField("State").GetValue(v)).ToArray();
                var targets=customers.PickupAssignments.Select(a=>a.Position).ToArray();
                // Synchronous authority counterexamples; no actual native cancellation, pose or slot injection.
                try{foreach(var v in active)v.GetType().GetField("RequestId").SetValue(v,0L);Assert.That((bool)method.Invoke(customers,new[]{entry}),Is.False);}
                finally{for(var i=0;i<active.Length;i++)active[i].GetType().GetField("RequestId").SetValue(active[i],requests[i]);}
                try{foreach(var v in active)v.GetType().GetField("State").SetValue(v,CustomerVisitState.Blocked);Assert.That((bool)method.Invoke(customers,new[]{entry}),Is.False);}
                finally{for(var i=0;i<active.Length;i++)active[i].GetType().GetField("State").SetValue(active[i],states[i]);}
                Assert.That((bool)method.Invoke(customers,new[]{entry}),Is.True);
                CollectionAssert.AreEqual(targets,customers.PickupAssignments.Select(a=>a.Position));
                TestContext.WriteLine("FINITE_COLLECTED_EXIT_DEPENDENCY actualPendingCollected=True independentlyClearAcceptedBody=True Request0/Blocked=False restored=True originalFutureSlotsUnchanged=True actualDispatchStillFullGuard=True");
            }
        }

        private void DiagnoseFloorEdgeExitCertificate()
        {
            var head=customers.Snapshot.FirstOrDefault(v=>v.State==CustomerVisitState.Blocked);if(head.VisitId==null)return;
            var actor=world.RegisteredActors.Single(a=>a.ActorId==head.VisitId);
            var occupied=(Vector3[])typeof(CustomerFlowController).GetMethod("RouteObstacles",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)
                .Invoke(customers,new object[]{head.VisitId,null,false});
            var clearance=actor.Settings.AgentRadius+world.RegisteredActors.Max(a=>a.Settings.AgentRadius)+actor.Settings.CollisionSkin+actor.Settings.Epsilon;
            var corners=new[]{actor.transform.position,new Vector3(3.5f,0,-3.53f),new Vector3(.5f,0,-3.53f),CafeLoopRuntimeProof.Field<Transform>(customers,"exit").position};
            var certificate=true;
            for(var i=1;i<corners.Length;i++)
            {
                var path=adapter.GetCompletePath(corners[i-1],corners[i]);var clear=CustomerRoutePlanner.IsClear(path,occupied,clearance);
                TestContext.WriteLine("EDGE_EXIT_CERTIFICATE leg="+i+" complete="+(path!=null)+" fullBodyAndSlotsClear="+clear+" clearance="+clearance+
                    " native="+string.Join(";",path??Array.Empty<Vector3>()));certificate&=path!=null && clear;
            }
            TestContext.WriteLine("EDGE_EXIT_CERTIFICATE actualFullGuard="+certificate+" hypotheticalDistanceAlone=False");
        }

        [Serializable] private sealed class PaidCohortRecord
        {
            public string layoutId,status,utc;public int variant,requested,actual,physicalSlots;public Vector3[] poses,slots;
        }

        [Serializable] private sealed class MatrixRecipe
        {
            public string caseId,method,status,utc,selectedCashier,selectedMachine;
            public int variant,gridWidth,gridHeight;public float cellSize;
            public MatrixFurnitureRecord[] furniture;public MatrixStationRecord[] stations;public Vector3[] serviceSides;
        }
        [Serializable] private sealed class MatrixFurnitureRecord {public string instanceId,definitionId;public int gridX,gridY,rotation;}
        [Serializable] private sealed class MatrixStationRecord {public string instanceId,definitionId,supportId,slotId;public int rotation;}
        private void WriteMatrixRecipe(string id,int variant,string status)
            => ExportLayoutRecipe(scene,id,variant,status);
        internal static void ExportLayoutRecipe(Scene scene,string id,int variant,string status)
        {
            T FindInRecipe<T>() where T:Component=>scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).Single();
            var runtime=FindInRecipe<CafeLayoutRuntime>();var employees=FindInRecipe<EmployeeFlowController>();var customers=FindInRecipe<CustomerFlowController>();
            employees.TryGetBinding(EmployeeRole.Cashier,out var cash);employees.TryGetBinding(EmployeeRole.Barista,out var machine);
            var floor=runtime.RoomSurfaceLayout.FloorTiles.Keys;var pickup=runtime.FunctionalSurfaceLayout.PickUpPoints.Single();
            var value=new MatrixRecipe{caseId=id,variant=variant,method=TestContext.CurrentContext.Test.FullName,status=status,utc=DateTime.UtcNow.ToString("O"),
                gridWidth=floor.Max(p=>p.X)-floor.Min(p=>p.X)+1,gridHeight=floor.Max(p=>p.Y)-floor.Min(p=>p.Y)+1,cellSize=runtime.Layout.GridSettings.CellSize,
                selectedCashier=cash?.WorkStationId??"",selectedMachine=machine?.WorkStationId??"",
                furniture=runtime.Layout.FurnitureInstances.OrderBy(f=>f.InstanceId,StringComparer.Ordinal).Select(f=>new MatrixFurnitureRecord{instanceId=f.InstanceId,definitionId=f.DefinitionId,gridX=f.Position.X,gridY=f.Position.Y,rotation=(int)f.Rotation}).ToArray(),
                stations=runtime.FunctionalSurfaceLayout.MountedInstances.Select(m=>new MatrixStationRecord{instanceId=m.InstanceId,definitionId=m.DefinitionId,supportId=m.Address.SupportFurnitureInstanceId,slotId=m.Address.SlotId,rotation=(int)m.Rotation})
                    .Concat(new[]{new MatrixStationRecord{instanceId=pickup.InstanceId,definitionId="pickup.point",supportId=pickup.Address.SupportFurnitureInstanceId,slotId=pickup.Address.SlotId}}).ToArray(),
                serviceSides=customers.GetPickupServiceTargets(pickup.InstanceId).Select(t=>t.Position).ToArray()};
            var json=JsonUtility.ToJson(value);var path=Path.Combine("outputs","phase14","delivery-corridor","layout-recipes.jsonl");Directory.CreateDirectory(Path.GetDirectoryName(path));File.AppendAllText(path,json+Environment.NewLine);
            TestContext.WriteLine("LAYOUT_RECIPE "+json);
        }
    }
}
#endif
