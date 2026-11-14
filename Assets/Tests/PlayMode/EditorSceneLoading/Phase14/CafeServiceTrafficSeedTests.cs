#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
using AnimalCafe.UI.Decoration;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace AnimalCafe.Tests.PlayMode.Phase14
{
    public sealed partial class CafePickupSideTests
    {
        // Named wrappers avoid UnityTest parameter-source discovery ambiguity.
        [UnityTest] public IEnumerator R17NativeSeed1501701OpenFourSides() => R17Native(1501701);
        [UnityTest] public IEnumerator R17NativeSeed1501702MiddleSlotZero() => R17Native(1501702);
        [UnityTest] public IEnumerator R17NativeSeed1501703MiddleSlotRotated() => R17Native(1501703);
        [UnityTest] public IEnumerator R17NativeSeed1501704AdjacentCorner() => R17Native(1501704);
        [UnityTest] public IEnumerator R17NativeSeed1501705CounterDetour() => R17Native(1501705);
        [UnityTest] public IEnumerator R17NativeSeed1501706FloorEdge() => R17Native(1501706);
        [UnityTest] public IEnumerator R17NativeSeed1501707RapidSingleEast() => R17Native(1501707);
        [UnityTest] public IEnumerator R17NativeSeed1501708RapidMiddleSlot() => R17Native(1501708);
        [UnityTest] public IEnumerator R17NativeSeed1501709DisconnectedReject() => R17Native(1501709);
        [UnityTest] public IEnumerator R17NativeSeed1501710PhotoLongSeven() => R17Native(1501710);
        [UnityTest] public IEnumerator R17NativeSeed1501711EntrancePlacementReject() => R17Native(1501711);
        [UnityTest] public IEnumerator R17NativeSeed1501712PhysicalWallCornerInset() => R17Native(1501712);

        private sealed class R17Witness
        {
            internal int Seed;
            internal R17DeliveryFailureWitness DeliveryWitness;
            internal string Directory, Stage = "start", Support, Slot = "slot.0";
            internal bool Tracking, AssertionsReached, RapidPaidOverlapProven;
            internal readonly List<string> Visits = new List<string>();
            internal readonly Dictionary<string, NavigationActor> Actors = new Dictionary<string, NavigationActor>();
            internal readonly Dictionary<string, Vector3> CounterTargets = new Dictionary<string, Vector3>();
            internal readonly Dictionary<string, CapacityToken[]> Tokens = new Dictionary<string, CapacityToken[]>();
            internal readonly Dictionary<string, CapacityToken> Totals = new Dictionary<string, CapacityToken>();
            internal readonly Dictionary<string, CafeOrderKey> Orders = new Dictionary<string, CafeOrderKey>();
            internal readonly Dictionary<string, int> Payments = new Dictionary<string, int>();
            internal readonly Dictionary<string, int> Preparations = new Dictionary<string, int>();
            internal readonly Dictionary<string, int> Deliveries = new Dictionary<string, int>();
            internal readonly HashSet<string> CounterArrivals = new HashSet<string>();
            internal readonly HashSet<string> PickupArrivals = new HashSet<string>();
            internal readonly HashSet<string> Collected = new HashSet<string>();
            internal readonly HashSet<string> Exits = new HashSet<string>();
            internal readonly List<CafeActualExitObserver> Observers = new List<CafeActualExitObserver>();
            internal readonly List<string> Errors = new List<string>();
            internal readonly List<string> Facts = new List<string>();
        }

        // Seed1702/1707 opt-in only: immutable/stored facts, never a policy/geometry replay.
        private sealed class R17DeliveryFailureWitness
        {
            private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            private readonly CafePickupSideTests test; private readonly R17Witness witness;
            private readonly List<Action> restore = new List<Action>(); private readonly List<string> errors = new List<string>();
            private object core, employee, sourceEntry; private TrafficIntentKey sourceKey;
            private string firstWaiting, lastSample, preFail, postFail, snapshotChanged; private bool bound, failureLocked, finished, stopped;
            private int samples, discardedSamples, candidateChanges, lastFrame = -1; private object lastDecision;
            internal R17DeliveryFailureWitness(CafePickupSideTests test, R17Witness witness)
            {
                this.test = test; this.witness = witness;
                Safe(() =>
                {
                    core = Read(test.customers, "traffic");
                    if (core == null || !ReferenceEquals(core, Read(test.owner, "traffic"))) throw new InvalidOperationException("Shared Core unavailable");
                    employee = ((IEnumerable)Read(test.employees, "employees")).Cast<object>().Single(value => (EmployeeRole)Read(value, "Role") == EmployeeRole.Barista);
                    var corridor = typeof(CustomerFlowController).GetField("CorridorObserver", Flags);
                    var priorCorridor = (Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string>)corridor.GetValue(test.customers);
                    Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string> installedCorridor = (op,id,phase,position,route,obstacles,result) =>
                    {
                        if (!finished) Safe(() =>
                        {
                            if (postFail != null || op != "traffic.delivery-yield-failure-witness" || string.IsNullOrEmpty(result) || !Bind(result, false)) return;
                            postFail = "POST_FAIL_ORIGINAL_CALLBACK prior PlanningFailed; cleared Jobs/path do not prove original geometry\n" + result + "\n" + Capture();
                        });
                        priorCorridor?.Invoke(op,id,phase,position,route,obstacles,result);
                    };
                    corridor.SetValue(test.customers, installedCorridor);
                    restore.Add(() => Restore(corridor, test.customers, installedCorridor, priorCorridor));
                    var wait = typeof(ServiceTrafficCoordinator).GetField("WaitFailureObserver", Flags);
                    var priorWait = (Action<string>)wait.GetValue(core);
                    Action<string> installedWait = message =>
                    {
                        if (!finished) Safe(() =>
                        {
                            if (preFail != null || message == null || !message.StartsWith("WAIT_ORIGINAL ", StringComparison.Ordinal) || !Bind(message, true)) return;
                            preFail = "PRE_FAIL_ORIGINAL_CALLBACK no predicate replay\n" + message + "\n" + Capture(message);
                        });
                        priorWait?.Invoke(message);
                    };
                    wait.SetValue(core, installedWait); restore.Add(() => Restore(wait, core, installedWait, priorWait));
                    var snapshotHook = typeof(ServiceTrafficCoordinator).GetField("SnapshotChangedObserver", Flags);
                    var priorSnapshot = (Action<TrafficRequest,TrafficWorldSnapshot>)snapshotHook.GetValue(core);
                    Action<TrafficRequest,TrafficWorldSnapshot> installedSnapshot = (request,current) =>
                    {
                        if (!finished) Safe(() =>
                        {
                            if (snapshotChanged != null || !BindSnapshot(request)) return;
                            snapshotChanged = "ORIGINAL_SNAPSHOT_CHANGED_PRE_FAIL request_ref=" + Ref(request) + " suppliedCurrent_ref=" + Ref(current) +
                                " storedCurrent_ref=" + Ref(Read(core,"snapshot")) + " entryRequestSame=True individualCurrent=NOT_CAPTURED individualSnapshotCurrent=NOT_CAPTURED\n" +
                                "CALLBACK_REQUEST " + Members(request,"Intent","RequestedFrame","Start","Target","ResourceId","NativeStamp") +
                                "\nCALLBACK_OLD_WORLD " + World(request.Snapshot) + "\nCALLBACK_CURRENT_WORLD " + World(current) + "\n" + Capture();
                        });
                        priorSnapshot?.Invoke(request,current);
                    };
                    snapshotHook.SetValue(core,installedSnapshot); restore.Add(() => Restore(snapshotHook,core,installedSnapshot,priorSnapshot));
                });
            }
            private void Safe(Action action)
            { if (stopped) return; try { action(); } catch (Exception error) { errors.Add(error.ToString()); stopped = true; } }
            private object Read(object value, string name)
            {
                if (value == null) return null;
                var field = value.GetType().GetField(name, Flags) ?? value.GetType().GetField("<" + name + ">k__BackingField", Flags);
                if (field != null) return field.GetValue(value);
                var detail = "UNKNOWN_FIELD " + value.GetType().Name + "." + name; if (!errors.Contains(detail)) errors.Add(detail); return null;
            }
            private static string Ref(object value) => value == null ? "NULL" : value.GetType().Name + "#" + System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
            private static string Key(TrafficIntentKey key) => key.Session + "/e" + key.Epoch + "/" + key.ActorId + "/" + key.IntentId + "/g" + key.Generation + "/r" + key.LayoutRevision + "/" + key.Context;
            private static string Value(object value)
            {
                if (value == null) return "NULL"; if (value is TrafficIntentKey key) return Key(key);
                if (value is Vector3 point) return point.ToString("F9"); if (value is TrafficNativeStamp stamp) return stamp.RequestId + "/" + stamp.LegGeneration;
                if (value is IEnumerable<Vector3> points) return "[" + string.Join(";", points.Select(position => position.ToString("F9"))) + "]";
                if (value is IEnumerable<int> indices) return "[" + string.Join(",",indices) + "]";
                if (value is CafeOrderKey order) return order.SessionId + "/" + order.Epoch + "/" + order.OrderId;
                if (value is NavigationTarget target) return target.Position.ToString("F9");
                if (value is QueueAssignment assignment) return assignment.VisitId + "/slot" + assignment.SlotIndex + "/g" + assignment.Generation + "/arrived=" + assignment.Arrived + "@" + assignment.Position.ToString("F9");
                return value is string || value.GetType().IsPrimitive || value.GetType().IsEnum ? value.ToString() : Ref(value);
            }
            private string Members(object value, params string[] names) => value == null ? "NULL" : Ref(value) + " " + string.Join(" ", names.Select(name => name + "=" + Value(Read(value,name))));
            private string Fields(object value) => value == null ? "NULL" : Ref(value) + " " + string.Join(" ", value.GetType().GetFields(Flags).Select(field => field.Name + "=" + Value(field.GetValue(value))));
            private void Restore(FieldInfo field, object owner, object installed, object prior)
            { if (!ReferenceEquals(field.GetValue(owner),installed)) throw new InvalidOperationException("Observer replacement preserved: " + field.Name); field.SetValue(owner,prior); }
            private bool Bind(string original, bool wait)
            {
                if (!ReferenceEquals(core, Read(test.customers,"traffic"))) throw new InvalidOperationException("Core replaced");
                var entries = (IDictionary)Read(core,"entries"); var cursors = (IDictionary)Read(test.customers,"trafficCandidates");
                if (failureLocked && (!entries.Contains(sourceKey) || !ReferenceEquals(entries[sourceKey],sourceEntry))) throw new InvalidOperationException("Failure-locked Entry replaced or removed");
                foreach (DictionaryEntry pair in entries)
                {
                    var key = (TrafficIntentKey)pair.Key;
                    if (key.ActorId != (string)Read(employee,"Id") || key.IntentId != "employee-pickup-journey") continue;
                    if (failureLocked && !key.Equals(sourceKey)) continue;
                    if (original != null && !original.Contains((wait ? " intent=" : "key=") + Key(key) + (wait ? " previous=" : ";"))) continue;
                    if (original == null && (!cursors.Contains(key) || Value(Read(cursors[key],"Role")) != "Delivery" || (bool)Read(pair.Value,"Retired"))) continue;
                    if (bound && (!key.Equals(sourceKey) || !ReferenceEquals(sourceEntry,pair.Value)))
                    {
                        // Sampling is not a failure receipt; never attach another candidate's history to the first actual failure.
                        discardedSamples += samples; candidateChanges++; samples = 0;
                        firstWaiting = lastSample = null; lastDecision = null; lastFrame = -1;
                    }
                    sourceKey = key; sourceEntry = pair.Value; bound = true;
                    if (original != null) failureLocked = true; // Only the original exact-key callback locks failure provenance.
                    return true;
                }
                return false;
            }
            private bool BindSnapshot(TrafficRequest request)
            {
                if (request == null || request.Intent.ActorId != (string)Read(employee,"Id") || request.Intent.IntentId != "employee-pickup-journey") return false;
                if (!ReferenceEquals(core,Read(test.customers,"traffic"))) throw new InvalidOperationException("Snapshot callback Core replaced");
                var entries = (IDictionary)Read(core,"entries"); var key = request.Intent;
                if (!entries.Contains(key) || !ReferenceEquals(Read(entries[key],"Request"),request)) throw new InvalidOperationException("Original snapshot callback request/Entry mismatch");
                var actualEntry = entries[key];
                if (failureLocked && !key.Equals(sourceKey)) return false;
                if (failureLocked && !ReferenceEquals(actualEntry,sourceEntry)) throw new InvalidOperationException("Failure-locked snapshot Entry replaced");
                if (bound && (!key.Equals(sourceKey) || !ReferenceEquals(actualEntry,sourceEntry)))
                {
                    discardedSamples += samples; candidateChanges++; samples = 0;
                    firstWaiting = lastSample = null; lastDecision = null; lastFrame = -1;
                }
                sourceKey = key; sourceEntry = actualEntry; bound = true; failureLocked = true;
                return true;
            }
            internal void Sample()
            {
                if (finished || postFail != null || snapshotChanged != null) return;
                Safe(() =>
                {
                    if (!Bind(null,false)) return;
                    var decision = Read(sourceEntry,"Decision") as TrafficDecision; var frame = (int)Read(core,"queryFrame");
                    if (frame == lastFrame && ReferenceEquals(decision,lastDecision)) return;
                    lastFrame = frame; lastDecision = decision; samples++;
                    lastSample = "SAMPLED_NOT_ORIGINAL_PREDICATE queryFrame=" + frame + "\n" + Capture();
                    if (firstWaiting == null && decision?.Kind == TrafficDecisionKind.WaitingForTraffic) firstWaiting = lastSample;
                });
            }
            private string World(TrafficWorldSnapshot world) => world == null ? "NULL" : Ref(world) + " v=" + world.Version + "/" + world.BodyVersion +
                "\nBODIES " + string.Join("\n", world.Bodies.Select(value => Members(value,"Owner","ActorLife","Position","Radius","SafetyMargin","Enabled","ActorActive","IndependentlyProgressing","RemainingSeconds"))) +
                "\nPARKING " + string.Join("\n", world.Parking.Select(value => Members(value,"Owner","CanonicalOwner","OwnerVersion","ResourceId","Position","Radius","SafetyMargin","BusinessOwner","ReleaseObligation","SemanticCheckpoint")));
            private string RequestProofFacts(TrafficRequest request) => request == null ? "NULL" :
                "ROOT_SELECTED_JOURNEY TrafficRequest.SelectedJourney=" + Value(request.SelectedJourney) + " requirementsCount=" + request.Requirements.Count +
                " freshProducerRequestLocal=NOT_CAPTURED freshRequirementsLocal=NOT_CAPTURED\n" +
                string.Join("\n", request.Requirements.Select((requirement,index) => "REQUIREMENT[" + index + "] " + Members(requirement,
                    "ResourceId","Start","Goal","Clearance","SelectedJourney","AvoidProposedParking","Obstacles")));
            private string Entry(object entry)
            {
                var request = Read(entry,"Request") as TrafficRequest; var leg = Read(entry,"Leg"); var pending = Read(entry,"PendingHandoff");
                var jobs = Read(entry,"Jobs") as IEnumerable;
                return Members(entry,"Key","BirthOrder","FirstFrame","RequestSequence","AcquiredSequence","Queries","FiniteLimit","WaitRemaining","Parking","Prepared","Queued","Active","Retired","Discarded","CurrentPath","Journey","JourneyPermit","JourneyRequest","PreparationIdentityCaptured","PreparationBody","PreparationClaims") +
                    "\nDECISION " + Members(Read(entry,"Decision"),"Intent","Kind","Reason","FailureDisposition","Permit","RequestSequence","AcquiredSequence") +
                    "\nREQUEST " + Members(request,"Intent","RequestedFrame","Start","Target","ResourceId","Radius","SafetyMargin","NativeStamp","FiniteSeconds","IndependentlyProgressing") +
                    "\nORIGINAL_REQUEST_PROOFS " + RequestProofFacts(request) +
                    "\nLEG " + Fields(leg) + "\nHANDOFF " + Members(pending,"Handle","Source","ActorLife","Remaining","PreparedSuccessor","OriginalClaim","OriginalClaims") +
                    "\nJOBS index=" + Read(entry,"JobIndex") + " " + (jobs == null ? "UNKNOWN" : string.Join("\n",jobs.Cast<object>().Select(job => Members(job,"start","goal","started","complete","current","next","ExpandedPath","Waypoints","LegEnds")))) +
                    "\nOLD_WORLD " + World(request?.Snapshot) + "\nJOURNEY_WORLD " + World((Read(entry,"JourneyRequest") as TrafficRequest)?.Snapshot);
            }
            private string ExactCoreEntry(string label,object boxed,IDictionary entries)
            {
                if (boxed == null) return label + " NULL_INTENT";
                if (!(boxed is TrafficIntentKey key)) { errors.Add("UNKNOWN intent type " + label); return label + " UNKNOWN_INTENT_TYPE " + Ref(boxed); }
                if (!entries.Contains(key)) return label + " key=" + Key(key) + " NOT_REGISTERED";
                var actual = entries[key]; var pending = Read(actual,"PendingHandoff"); var dependencies = Read(pending,"Dependencies") as IDictionary;
                var detail = label + " key=" + Key(key) + " registeredEntry=" + Ref(actual) + " retired=" + Value(Read(actual,"Retired")) + "\n" + Entry(actual) +
                    "\n" + label + " HANDOFF_STORED_FIELDS " + Fields(pending);
                if (dependencies != null)
                {
                    if (dependencies.Count > 32) errors.Add("UNKNOWN handoff dependency cap32 " + label);
                    detail += "\n" + label + " HANDOFF_DEPENDENCIES " + string.Join(";",dependencies.Keys.Cast<object>().Take(32).Select(dependent =>
                        Ref(dependent) + "/key=" + Value(Read(dependent,"Key")) + "/resources=" + string.Join(",",((IEnumerable)dependencies[dependent]).Cast<string>())));
                }
                return detail;
            }
            private string PaidCounterFacts(object visit,IDictionary entries)
            {
                var id = Read(visit,"Id") as string; var paid = Read(visit,"PaidPickup"); var preparation = Read(paid,"Preparation");
                var pending = Read(visit,"PendingTraffic"); var counter = Read(test.customers,"queue");
                var counterEntries = Read(counter,"entries") as IEnumerable;
                var service = Read(test.owner,"service"); var paidOrders = Read(service,"paid") as IDictionary;
                var orderRows = Read(Read(service,"orders"),"orders") as IDictionary;
                var actualOrder = paidOrders != null && paidOrders.Contains(id) ? paidOrders[id] : null;
                var order = actualOrder is CafeOrderKey orderKey && orderRows != null && orderRows.Contains(orderKey.OrderId) ? orderRows[orderKey.OrderId] : null;
                var pickupMoves = Read(service,"pickupMoves") as IEnumerable;
                return "\nPAID_COUNTER_ORIGINAL_RAW visit=" + id + " visitRef=" + Ref(visit) + " freshTransferLocals=NOT_CAPTURED predicates=NOT_REPLAYED" +
                    "\nPAID_TASK " + Fields(paid) + "\nPAID_PREPARATION " + Fields(preparation) + "\nPAID_PREPARATION_SOURCE " + Members(Read(preparation,"Source"),"Intent","Kind","Reason","FailureDisposition","Permit","RequestSequence","AcquiredSequence") + "\nPAID_PARKING " + Fields(Read(paid,"Parking")) +
                    "\nPAID_COUNTER_CAPTURE " + Fields(Read(preparation,"CounterClaim")) + "\nPAID_PREVIEW " + Fields(Read(preparation,"Preview")) +
                    "\nVISIT_SELECTION " + Fields(Read(visit,"TrafficSelection")) + "\nPENDING_RECEIPT " + Fields(pending) +
                    "\nSERVICE_PAID_ORDER " + Value(actualOrder) + " orderRow=" + Members(order,"OrderId","CustomerId","ProductId","ClaimantId","State","TerminationReason") +
                    " pickupMoveMembership=" + (pickupMoves == null ? "UNKNOWN" : pickupMoves.Cast<string>().Contains(id).ToString()) +
                    "\nCOUNTER_CURRENT_RAW " + Members(counter,"Version","preparationRevision","exiting") + " ownRows=" +
                        (counterEntries == null ? "UNKNOWN" : string.Join(";",counterEntries.Cast<object>().Where(row => (string)Read(row,"Id") == id).Select(Fields))) +
                    "\n" + ExactCoreEntry("PAID_ACTIVE_CORE",Read(paid,"ActiveIntent"),entries) +
                    "\n" + ExactCoreEntry("COUNTER_PREDECESSOR_CORE",Read(pending,"Intent"),entries);
            }
            private string Capture(string original = null)
            {
                var entries = (IDictionary)Read(core,"entries"); var cursors = (IDictionary)Read(test.customers,"trafficCandidates");
                var pending = new Queue<TrafficIntentKey>(); var seen = new HashSet<TrafficIntentKey>(); var graph = new List<string>(); pending.Enqueue(sourceKey);
                if (original != null) foreach (TrafficIntentKey key in entries.Keys)
                    if (original.Contains(" owner=" + Key(key) + " proof=")) pending.Enqueue(key);
                // Original live paid intents join only the existing bounded raw graph, never failure/source authority.
                var livePaidVisits = (IDictionary)Read(test.customers,"visits");
                foreach (var actualPaidVisit in livePaidVisits.Values.Cast<object>())
                    if (Read(Read(actualPaidVisit,"PaidPickup"),"ActiveIntent") is TrafficIntentKey actualPaidIntent) pending.Enqueue(actualPaidIntent);
                while (pending.Count > 0 && seen.Count < 32)
                {
                    var key = pending.Dequeue(); if (!seen.Add(key)) continue;
                    if (!entries.Contains(key)) { graph.Add("OWNER_NO_CORE_ENTRY " + Key(key)); continue; }
                    var entry = entries[key]; graph.Add("GRAPH " + Key(key) + "\n" + Entry(entry));
                    var frozen = Read(entry,"FrozenOwners") as IEnumerable; if (frozen != null) foreach (TrafficIntentKey owner in frozen) pending.Enqueue(owner);
                    graph.Add("FROZEN_OWNERS " + (frozen == null ? "NULL" : string.Join(";",frozen.Cast<TrafficIntentKey>().Select(Key))));
                    var resources = Read(entry,"FrozenResources") as IDictionary;
                    graph.Add("FROZEN_RESOURCES " + (resources == null ? "NULL" : string.Join(";",resources.Keys.Cast<TrafficIntentKey>().Select(owner => Key(owner) + "={" + string.Join(",",((IEnumerable)resources[owner]).Cast<string>()) + "}"))));
                    var blockers = (Read(entry,"WaitingBlockers") as IEnumerable)?.Cast<TrafficBlocker>().ToArray() ?? Array.Empty<TrafficBlocker>();
                    graph.Add("WAITING " + string.Join(";",blockers.Select(block => Key(block.Owner) + "/" + block.ResourceId + "/" + block.Proof)));
                    foreach (var block in blockers) pending.Enqueue(block.Owner);
                    var decision = Read(entry,"Decision") as TrafficDecision;
                    if (decision != null) { graph.Add("DECISION_BLOCKERS " + string.Join(";",decision.Blockers.Select(block => Key(block.Owner) + "/" + block.ResourceId + "/" + block.Proof))); foreach (var block in decision.Blockers) pending.Enqueue(block.Owner); }
                }
                if (pending.Count > 0) { var detail = "UNKNOWN graph cap32"; if (!errors.Contains(detail)) errors.Add(detail); }
                var cursor = cursors.Contains(sourceKey) ? cursors[sourceKey] : null; var context = Read(cursor,"DeliveryContext"); var runner = Read(employee,"Runner");
                var visits = (IDictionary)Read(test.customers,"visits");
                var visitFacts = string.Join("\n",visits.Values.Cast<object>().Select(visit => Members(visit,"Id","Actor","State","ResumeState","Generation","RequestId",
                    "CounterArrived","PickupAssigned","PickupInTransit","PickupArrived","ServiceArrived","HasCollected","QueueLeft","PickupDeparted","Assignment","ReservedPickup","TrafficMovePending","PendingTraffic") + PaidCounterFacts(visit,entries)));
                return "key=" + Key(sourceKey) + " entry=" + Ref(sourceEntry) + " registered=" + (entries.Contains(sourceKey) && ReferenceEquals(entries[sourceKey],sourceEntry)) +
                    "\nCORE " + Members(core,"session","epoch","layoutRevision","queryFrame","queriesPerFrame","queriesPerIntent","paused","closed","committing") +
                    "\nCURSOR " + Members(cursor,"Key","ActorLife","Origin","Role","CurrentIndex","Source","DeliveryOwner","DeliveryPreview","PickupPreparation","OriginalDeliveryPlan") +
                    "\nPREVIEW " + Fields(Read(cursor,"DeliveryPreview")) + "\nORIGINAL_PLAN " + Fields(Read(cursor,"OriginalDeliveryPlan")) +
                    "\nCONTEXT " + Fields(context) + "\nSTAMP " + Fields(Read(context,"stamp")) +
                    "\nEMPLOYEE " + Fields(employee) + "\nTOKEN " + Fields(Read(employee,"AcceptedToken")) + "\nCHECKPOINT " + Fields(Read(employee,"Checkpoint")) +
                    "\nRUNNER " + Fields(runner) + "\nMOVE_LEG " + Fields(Read(runner,"moveLeg")) +
                    "\nLIVE_VISITS_RAW " + visitFacts + "\nCURRENT_WORLD " + World(Read(core,"snapshot") as TrafficWorldSnapshot) + "\n" + string.Join("\n",graph);
            }
            internal void Finish()
            {
                if (finished) return; finished = true;
                foreach (var action in restore.AsEnumerable().Reverse()) try { action(); } catch (Exception error) { errors.Add(error.ToString()); }
                var status = errors.Count > 0 ? "UNKNOWN" : preFail != null || postFail != null || snapshotChanged != null ? "OBSERVED_CALLBACK" : "NOT_OBSERVED";
                File.WriteAllText(Path.Combine(witness.Directory,"delivery-first-failure-witness.txt"), "seed=" + witness.Seed + " status=" + status + " failureLocked=" + failureLocked + " samples=" + samples + " discardedSamples=" + discardedSamples + " candidateChanges=" + candidateChanges + " errors=" + string.Join(";",errors) +
                    "\nFIRST_WAITING " + (firstWaiting ?? "NOT_OBSERVED") + "\nLAST_SAME_ENTRY " + (lastSample ?? "NOT_OBSERVED") +
                    "\nORIGINAL_PRE_FAIL " + (preFail ?? "NOT_OBSERVED") + "\nORIGINAL_POST_FAIL " + (postFail ?? "NOT_OBSERVED") +
                    "\nORIGINAL_SNAPSHOT_CHANGED " + (snapshotChanged ?? "NOT_OBSERVED"));
                if (errors.Count > 0) throw new InvalidOperationException("R17 delivery witness UNKNOWN: " + string.Join(";",errors));
            }
        }

        private static string R17Expected(int seed) => seed == 1501709 ? "ExplicitDisconnectedReject"
            : seed == 1501711 ? "PlacementRejectOnly" : "ExpectedRunnable";

        private IEnumerator R17Native(int seed)
        {
            var savedRandom = UnityEngine.Random.state;
            var r = new R17Witness { Seed = seed, Directory = Path.Combine("outputs", "phase15", "r17-native12",
                seed.ToString(), DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")) };
            Directory.CreateDirectory(r.Directory);
            Action<CafeLoopSnapshot> observe = null;
            Action<CafeTaskEvent> task = null;
            var priorCleanup = controlledHandoffCleanup;
            var priorFailureSink = parkingEndpointWitnessCleanup;
            Action<string> cleanup = null;
            Action failureSink = null;
            var ownFinalized = false; var priorControlledCalled = false; var postSinkCalled = false;
            var cleanupFailures = new List<Exception>();
            var receiptErrorCount = -1;
            void CleanupError(string stage, Exception error)
            {
                cleanupFailures.Add(error);
                var detail = "UNKNOWN R17 cleanup " + stage + ": " + error.GetType().Name + ": " + error.Message;
                r.Errors.Add(detail); TestContext.WriteLine(detail);
            }
            void CleanupAttempt(string stage, Action action)
            {
                try { action(); } catch (Exception error) { CleanupError(stage, error); }
            }
            void WriteReceipt(string reason)
            {
                receiptErrorCount = cleanupFailures.Count;
                var status = cleanupFailures.Count > 0 ? "UNKNOWN" : r.AssertionsReached ? "ASSERTIONS_REACHED" : "NOT_PROVEN";
                CleanupAttempt("receipts", () => File.WriteAllText(Path.Combine(r.Directory, "receipts.txt"),
                    "seed=" + seed + " expected=" + R17Expected(seed) + " status=" + status + " cleanup=" + reason +
                    " assertionsReached=" + r.AssertionsReached + " rapidPaidOverlapProven=" + r.RapidPaidOverlapProven + " stage=" + r.Stage +
                    "\nvisits=" + string.Join(";", r.Visits) + "\nactualExits=" + string.Join(";", r.Exits) +
                    "\nerrors=" + string.Join("\n", r.Errors) + "\n" + string.Join("\n", r.Facts)));
            }
            void FinalizeR17(string reason)
            {
                if (ownFinalized) return;
                ownFinalized = true; r.Tracking = false; // Teardown destruction never counts as actual Exit.
                CleanupAttempt("delivery-witness", () => r.DeliveryWitness?.Finish());
                CleanupAttempt("snapshot-detach", () => { if (observe != null && owner != null) owner.SnapshotChanged -= observe; });
                CleanupAttempt("task-detach", () => { if (task != null && employees != null) employees.IntegratedTaskEvent -= task; });
                foreach (var observer in r.Observers)
                    CleanupAttempt("receipt-disarm", () => { if (observer != null) observer.Receipt = null; });
                CleanupAttempt("random-restore", () => UnityEngine.Random.state = savedRandom);
                CleanupAttempt("final-recipe", () => R17WriteRecipe(r, "final-witness"));
                WriteReceipt(reason);
            }
            cleanup = reason =>
            {
                FinalizeR17(reason);
                if (priorControlledCalled) return;
                priorControlledCalled = true;
                if (ReferenceEquals(controlledHandoffCleanup, cleanup)) controlledHandoffCleanup = priorCleanup;
                else CleanupError("controlled-hook-replaced", new InvalidOperationException("Replacement preserved; R17 cannot restore another subscriber"));
                CleanupAttempt("prior-controlled", () => priorCleanup?.Invoke(reason));
                // No throw here: Base Cleanup must still unload the scene and restore inputs/time.
            };
            failureSink = () =>
            {
                if (postSinkCalled) return;
                postSinkCalled = true;
                if (ReferenceEquals(parkingEndpointWitnessCleanup, failureSink)) parkingEndpointWitnessCleanup = priorFailureSink;
                else CleanupError("post-hook-replaced", new InvalidOperationException("Replacement preserved; R17 cannot restore another subscriber"));
                try { CleanupAttempt("prior-post-sink", () => priorFailureSink?.Invoke()); }
                finally
                {
                    if (ReferenceEquals(parkingEndpointWitnessCleanup, failureSink)) parkingEndpointWitnessCleanup = priorFailureSink;
                }
                if (!ownFinalized || !priorControlledCalled)
                    CleanupError("missing-pre-cleanup", new InvalidOperationException("R17 pre-unload cleanup did not complete"));
                if (cleanupFailures.Count != receiptErrorCount) WriteReceipt("post-mandatory-UNKNOWN");
                if (cleanupFailures.Count == 1) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(cleanupFailures[0]).Throw();
                if (cleanupFailures.Count > 1) throw new AggregateException("R17 cleanup UNKNOWN; mandatory teardown completed", cleanupFailures);
            };
            controlledHandoffCleanup = cleanup; // Leave the safe prior-forwarder until Base Cleanup791, even after normal finally.
            parkingEndpointWitnessCleanup = failureSink; // Base Cleanup794 runs after scene/input/time restoration.
            try
            {
                UnityEngine.Random.InitState(seed);
                yield return MatrixStart(); // Exact existing support/equipment recipe; no synthetic scene.
                r.Stage = "formal-authoring";
                if (seed == 1501711)
                {
                    yield return R17PlacementReject(r);
                    r.AssertionsReached = true;
                    yield break;
                }
                yield return R17BuildPickup(r);
                R17WriteRecipe(r, "confirmed-before-exit");
                if (seed == 1501709)
                {
                    yield return R17DisconnectedReject(r);
                    r.AssertionsReached = true;
                    yield break;
                }
                Find<DecorationModeController>().ExitDecorationMode();
                yield return Until(() => owner.CanProgress && employees.Snapshot.All(e => e.State == EmployeeState.Idle));
                Assert.That(time.TrySetSpeed(GameSpeed.Normal), Is.True);
                var livePickup = Find<CafeLayoutRuntime>().FunctionalSurfaceLayout.PickUpPoints.Single();
                var sideCount = customers.GetPickupServiceTargets(livePickup.InstanceId).Count;
                var expectedSides = seed == 1501701 ? 4 : seed == 1501707 ? 1 : seed == 1501705 || seed == 1501706 ? 3 : 2;
                if (seed == 1501710) Assert.That(sideCount, Is.GreaterThan(0), "Photo recipe does not freeze a numeric Native-side count");
                else Assert.That(sideCount, Is.EqualTo(expectedSides), "Frozen recipe exposed-side count");
                r.Facts.Add("serviceSides=" + string.Join(";", customers.GetPickupServiceTargets(livePickup.InstanceId).Select(value => value.Position.ToString("F6"))));
                r.Tracking = true;
                observe = _ => R17Observe(r);
                task = e => R17Task(r, e);
                owner.SnapshotChanged += observe;
                employees.IntegratedTaskEvent += task;
                R17WriteRecipe(r, "normal-runtime-ready");
                if ((seed == 1501702 || seed == 1501707 || seed == 1501708) && Environment.GetCommandLineArgs().Contains("-r17DeliveryFailureWitness")) r.DeliveryWitness = new R17DeliveryFailureWitness(this,r);
                if (seed == 1501707 || seed == 1501708) yield return R17RapidPair(r);
                else
                {
                    // 第二位必须在第一位真实 Exit 之后出生 / Actual Exit separates the sequential visits.
                    for (var index = 0; index < 2; index++)
                    {
                        tools.HoldCollection();
                        yield return AwaitTrafficAdmission("R17 asynchronous admission seed=" + seed);
                        var id = R17CaptureBirth(r);
                        yield return R17Ready(r, id);
                        tools.ResumeCollection();
                        yield return R17Wait(r, () => r.Exits.Contains(id) && R17Drained(), "sequential-real-exit-" + index);
                    }
                }
                R17Observe(r);
                Assert.That(r.Errors, Is.Empty, string.Join("\n", r.Errors));
                Assert.That(r.Visits.Count, Is.EqualTo(2), "Runnable seeds require two real orders and two real exits");
                CollectionAssert.AreEquivalent(r.Visits, r.Exits);
                CollectionAssert.AreEquivalent(r.Visits, r.CounterArrivals);
                CollectionAssert.AreEquivalent(r.Visits, r.PickupArrivals);
                CollectionAssert.AreEquivalent(r.Visits, r.Collected);
                CollectionAssert.AreEqual(r.Visits, owner.Snapshot.PaidVisitIds, "Cumulative actual Paid FIFO");
                Assert.That(owner.Snapshot.Orders.Count, Is.EqualTo(2));
                Assert.That(owner.Snapshot.Orders.Values.All(o => o.State == OrderState.Completed), Is.True);
                foreach (var id in r.Visits)
                {
                    Assert.That(owner.Snapshot.Orders.Values.Count(o => o.CustomerId == id), Is.EqualTo(1));
                    Assert.That(r.Orders.ContainsKey(id) && owner.Snapshot.Orders.ContainsKey(r.Orders[id]), Is.True, "Original order key retained");
                    Assert.That(r.Payments.TryGetValue(id, out var paid) ? paid : 0, Is.EqualTo(1));
                    Assert.That(r.Preparations.TryGetValue(id, out var prepared) ? prepared : 0, Is.EqualTo(1));
                    Assert.That(r.Deliveries.TryGetValue(id, out var delivered) ? delivered : 0, Is.EqualTo(1));
                    var reservations = customers.Capacity.GetReservations().Where(value => value.OwnerId == id).ToArray();
                    CollectionAssert.AreEqual(r.Tokens[id], reservations.Select(value => value.Token));
                    Assert.That(reservations.All(value => value.State == ReservationState.Released), Is.True);
                }
                Assert.That(R17Drained(), Is.True);
                Assert.That(customers.CounterAssignments, Is.Empty);
                Assert.That(customers.PickupAssignments, Is.Empty);
                Assert.That(Find<PickupCupDisplay>().VisibleOrders, Is.Empty);
                Assert.That(employees.AreEmployeesAtWorkStations(out var reason), Is.True, reason);
                r.Facts.Add("drain isDrained=" + owner.GetDrainSnapshot().IsDrained + " reason=" + owner.GetDrainSnapshot().Reason);
                r.AssertionsReached = true;
            }
            finally
            {
                FinalizeR17("finally");
            }
        }

        private IEnumerator R17BuildPickup(R17Witness r)
        {
            var runtime = Find<CafeLayoutRuntime>();
            var decor = Find<DecorationModeController>();
            var seed = r.Seed;
            GridPosition pickupPosition;
            if (seed == 1501710)
            {
                // Copy the actual PhotoSeven authoring sequence, including its second CM rotation.
                var cash = runtime.Layout.FurnitureInstances.Single(f => f.Position == new GridPosition(1, 5));
                var machine = runtime.Layout.FurnitureInstances.Single(f => f.Position == new GridPosition(4, 6));
                yield return UserMoveCounterSupport(cash.InstanceId, new GridPosition(7, 4));
                yield return UserMoveCounterSupport(machine.InstanceId, new GridPosition(1, 7));
                var equipment = runtime.FunctionalSurfaceLayout.MountedInstances.Single(m => m.DefinitionId == "equipment.coffee-machine.01");
                Assert.That(decor.TryBeginExistingFunctionalSurfacePreview(FunctionalSurfacePreviewKind.MountedEquipment, equipment.InstanceId), Is.True);
                Assert.That(decor.TryRotateFunctionalSurfacePreview(), Is.True);
                Assert.That(decor.TryConfirmFunctionalSurfacePreview(), Is.True);
                Assert.That(runtime.FunctionalSurfaceLayout.MountedInstances.Single(m => m.InstanceId == equipment.InstanceId).Rotation, Is.EqualTo(FurnitureRotation.Degrees180));
                for (var x = 1; x < 7; x++) yield return MatrixFurniture("furniture.counter.module.01", new GridPosition(x, 4), 0);
                pickupPosition = new GridPosition(1, 4);
            }
            else if (seed == 1501712)
            {
                var machine = runtime.Layout.FurnitureInstances.Single(f => f.Position == new GridPosition(4, 6));
                yield return UserMoveCounterSupport(machine.InstanceId, new GridPosition(4, 3));
                Assert.That(runtime.FunctionalSurfaceLayout.MountedInstances.Single(m => m.DefinitionId == "equipment.coffee-machine.01").Rotation, Is.EqualTo(FurnitureRotation.Degrees90));
                pickupPosition = new GridPosition(6, 6);
                yield return MatrixFurniture("furniture.counter.module.01", pickupPosition, 0);
                yield return MatrixFurniture("furniture.counter.module.01", new GridPosition(6, 7), 0);
                yield return MatrixFurniture("furniture.counter.module.01", new GridPosition(7, 6), 0);
                // Existing physical Main walls only; no invented wall/collider or anchor injection.
                var transforms = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
                var north = transforms.Single(t => t.name == "P4_Wall_BackLeft");
                var east = transforms.Single(t => t.name == "P4_Wall_BackRight");
                Assert.That(north.position.z, Is.EqualTo(4f).Within(.001f));
                Assert.That(east.position.x, Is.EqualTo(4f).Within(.001f));
                Assert.That(Quaternion.Angle(north.rotation, Quaternion.identity), Is.LessThanOrEqualTo(.001f));
                Assert.That(Quaternion.Angle(east.rotation, Quaternion.Euler(0, 90, 0)), Is.LessThanOrEqualTo(.001f));
                var grid = transforms.Single(t => t.name == "DecorationSpaceRoot");
                Assert.That(grid.position.x, Is.EqualTo(-4f).Within(.001f));
                Assert.That(grid.position.z, Is.EqualTo(-4f).Within(.001f));
                r.Facts.Add("physicalWalls north=" + north.position.ToString("F6") + "/" + north.eulerAngles.ToString("F6") + " east=" + east.position.ToString("F6") + "/" + east.eulerAngles.ToString("F6") + " grid=" + grid.position.ToString("F6"));
            }
            else if (seed == 1501702 || seed == 1501703 || seed == 1501708)
            {
                yield return MatrixFurniture("counter.preset.1x3", new GridPosition(3, 3), seed == 1501703 ? 1 : 0);
                var confirmed = runtime.Layout.FurnitureInstances.Single(f => f.DefinitionId == "counter.preset.1x3");
                Assert.That(confirmed.Rotation, Is.EqualTo(seed == 1501703 ? FurnitureRotation.Degrees90 : FurnitureRotation.Degrees0));
                // RotatePreview may adjust origin; select/read the confirmed instance, never assume (3,3).
                pickupPosition = confirmed.Position;
                r.Slot = "slot.1";
            }
            else
            {
                pickupPosition = seed == 1501706 ? new GridPosition(7, 3) : seed == 1501709 ? new GridPosition(5, 5) : new GridPosition(4, 4);
                yield return MatrixFurniture("furniture.counter.module.01", pickupPosition, 0);
                if (seed == 1501704 || seed == 1501705)
                {
                    yield return MatrixFurniture("furniture.counter.module.01", new GridPosition(4, 5), 0);
                    yield return MatrixFurniture("furniture.counter.module.01", seed == 1501704 ? new GridPosition(5, 4) : new GridPosition(5, 5), 0);
                }
                if (seed == 1501707)
                    for (var side = 0; side < 4; side++) if (side != 1) yield return MatrixFurniture("furniture.counter.module.01", MatrixNeighbours[side], 0);
                if (seed == 1501709)
                    for (var x = 3; x <= 7; x++) for (var y = 3; y <= 7; y++)
                        if (x == 3 || x == 7 || y == 3 || y == 7) yield return MatrixFurniture("furniture.counter.module.01", new GridPosition(x, y), 0);
            }
            var support = runtime.Layout.FurnitureInstances.Single(f => f.Position == pickupPosition);
            r.Support = support.InstanceId;
            Assert.That(decor.TryBeginFunctionalSurfacePreview(DecorationCatalogueItemKind.PickUpPoint, "pickup.point", new SurfaceSlotAddress(r.Support, r.Slot)), Is.True);
            Assert.That(decor.TryConfirmFunctionalSurfacePreview(), Is.True);
            Assert.That(runtime.FunctionalSurfaceLayout.PickUpPoints.Single().Address, Is.EqualTo(new SurfaceSlotAddress(r.Support, r.Slot)));
        }

        private IEnumerator R17RapidPair(R17Witness r)
        {
            tools.HoldCollection();
            yield return AwaitTrafficAdmission();
            var first = R17CaptureBirth(r);
            yield return R17Wait(r, () => owner.Snapshot.PaidVisitIds.Contains(first), "first-real-paid");
            yield return null; // Real Paid precedes the second arrival opportunity; never same-frame pair birth.
            yield return R17AwaitOriginalRapidAdmission(r, first);
            var second = R17CaptureBirth(r);
            yield return R17Ready(r, second);
            yield return R17Ready(r, first);
            Assert.That(owner.HoldCollection, Is.True);
            Assert.That(r.Exits, Is.Empty, "Rapid coverage cannot be replaced by first Exit then another birth");
            Assert.That(r.Collected, Is.Empty, "Both real Paid customers remain held before rapid overlap proof");
            Assert.That(r.Visits.All(id => owner.Snapshot.PaidVisitIds.Contains(id) && customers.Snapshot.Any(value => value.VisitId == id)), Is.True,
                "NOT_PROVEN: rapid seeds require two actual Paid live customers before collection release");
            r.RapidPaidOverlapProven = true;
            r.Facts.Add("rapidActualPaidOverlap frame=" + UnityEngine.Time.frameCount + " visits=" + string.Join(";", r.Visits));
            tools.ResumeCollection();
            yield return R17Wait(r, () => r.Exits.Count == 2 && R17Drained(), "rapid-pair-drain");
        }

        private IEnumerator R17AwaitOriginalRapidAdmission(R17Witness r, string first)
        {
            // Read only actual pending producer/Core state. Only normal frames run the owner ready batch.
            // false 不是 safety receipt；不手动 pump/query/commit，不重建 key，不在 Terminal 后再次 TrySpawn。
            var coordinator = MovementRead(customers, "traffic") as ServiceTrafficCoordinator;
            Assert.That(coordinator, Is.Not.Null, "NOT_PROVEN: original traffic source unavailable");
            var entries = (IDictionary)MovementRead(coordinator, "entries");
            var queue = (CounterQueueService)MovementRead(customers, "queue");
            var before = (long)MovementRead(customers, "nextVisit");
            var customerSession = (string)MovementRead(customers, "session");
            var epoch = (long)MovementRead(customers, "trafficEpoch");
            var unbornId = customerSession + "-" + (before + 1).ToString("D8");
            object anchor = null, entry = null, frozenOwners = null, frozenResources = null;
            var key = default(TrafficIntentKey); var firstFrame = 0; var sequence = 0L;
            var spentQueries = 0; var finiteLimit = 0f; var waitRemaining = 0f;
            int[] attempted = null; string lastState = null;
            r.Stage = "rapid-original-pending-admission";

            TrafficDecision ObservePending()
            {
                Assert.That(MovementRead(customers, "traffic"), Is.SameAs(coordinator), "NOT_PROVEN: coordinator replaced");
                Assert.That(MovementRead(customers, "session"), Is.EqualTo(customerSession));
                Assert.That(MovementRead(customers, "trafficEpoch"), Is.EqualTo(epoch));
                Assert.That(MovementRead(customers, "nextVisit"), Is.EqualTo(before), "Unborn identity cannot advance before commit");
                var current = MovementRead(customers, "pendingAdmissionAnchor");
                if (current == null)
                {
                    Assert.That(anchor, Is.Null, "NOT_PROVEN: original unborn anchor disappeared without canonical birth");
                    Assert.That(entries.Keys.Cast<TrafficIntentKey>().Any(value => value.ActorId == unbornId && value.IntentId == "counter-admission"), Is.False,
                        "NOT_PROVEN: an unanchored admission source exists; do not poll or revive it");
                    return null; // Ordinary prechecks have not created a source yet; no refusal classification follows.
                }
                var currentKey = (TrafficIntentKey)MovementRead(current, "Key");
                Assert.That(currentKey.ActorId, Is.EqualTo(unbornId));
                Assert.That(currentKey.IntentId, Is.EqualTo("counter-admission"));
                Assert.That(MovementRead(current, "Coordinator"), Is.SameAs(coordinator));
                if (anchor == null)
                {
                    anchor = current; key = currentKey; firstFrame = (int)MovementRead(current, "FirstFrame");
                    r.Facts.Add("rapidOriginalOpportunity key=" + key.Session + "/" + key.Epoch + "/" + key.ActorId + "/" + key.IntentId + "/g" + key.Generation +
                        "/r" + key.LayoutRevision + "/" + key.Context + " firstFrame=" + firstFrame + " submitted=" + MovementRead(current, "Submitted"));
                }
                Assert.That(current, Is.SameAs(anchor)); Assert.That(currentKey, Is.EqualTo(key));
                Assert.That(MovementRead(current, "Before"), Is.EqualTo(before));
                Assert.That(MovementRead(current, "FirstFrame"), Is.EqualTo(firstFrame));
                if (!(bool)MovementRead(current, "Submitted"))
                {
                    // An occupied original endpoint may retain this same unborn opportunity without a Core source.
                    // 尚未提交仍锁定同一anchor/key/出生帧；只让原公开TrySpawn与正常帧推进，不改身份或预算。
                    Assert.That(entry, Is.Null, "A submitted original source cannot regress to unsubmitted");
                    Assert.That(entries.Keys.Cast<TrafficIntentKey>().Any(value => value.ActorId == unbornId && value.IntentId == "counter-admission"), Is.False);
                    Assert.That(customers.Snapshot.Any(value => value.VisitId == unbornId), Is.False);
                    Assert.That(customers.CounterAssignments.Any(value => value.VisitId == unbornId), Is.False);
                    Assert.That(customers.Capacity.GetReservations().Any(value => value.OwnerId == unbornId), Is.False);
                    Assert.That(world.RegisteredActors.Any(value => value.ActorId == unbornId), Is.False);
                    Assert.That(((IDictionary)MovementRead(world.Service, "actors")).Contains(unbornId), Is.False);
                    Assert.That(((IDictionary)MovementRead(world.Service, "byActor")).Contains(unbornId), Is.False, "No Native movement before canonical birth");
                    Assert.That(owner.Snapshot.Orders.Values.Any(value => value.CustomerId == unbornId), Is.False);
                    Assert.That(owner.Snapshot.PaidVisitIds.Contains(unbornId), Is.False);
                    return null; // No Core verdict is inferred; keep the original 6000-frame/dual-Paid contract.
                }
                Assert.That((bool)MovementRead(current, "Submitted"), Is.True, "NOT_PROVEN: anchor has no submitted Core source");
                Assert.That(entries.Contains(currentKey), Is.True, "NOT_PROVEN: actual Core source missing");
                if (entry == null)
                {
                    entry = entries[key];
                    finiteLimit = (float)MovementRead(entry, "FiniteLimit");
                    attempted = ((HashSet<int>)MovementRead(entry, "AttemptedIndices")).ToArray();
                    r.Facts.Add("rapidOriginalAnchor key=" + key.Session + "/" + key.Epoch + "/" + key.ActorId + "/" + key.IntentId + "/g" + key.Generation +
                        "/r" + key.LayoutRevision + "/" + key.Context + " firstFrame=" + firstFrame);
                }
                Assert.That(current, Is.SameAs(anchor)); Assert.That(currentKey, Is.EqualTo(key));
                Assert.That(entries[key], Is.SameAs(entry), "Original Core Entry cannot be replaced");
                Assert.That(MovementRead(current, "Before"), Is.EqualTo(before));
                Assert.That(MovementRead(current, "FirstFrame"), Is.EqualTo(firstFrame));
                Assert.That(MovementRead(entry, "FirstFrame"), Is.EqualTo(firstFrame));
                Assert.That(MovementRead(entry, "FiniteLimit"), Is.EqualTo(finiteLimit));
                var currentSequence = (long)MovementRead(entry, "RequestSequence");
                if (sequence == 0 && currentSequence != 0) sequence = currentSequence; // Normal first ready assignment.
                Assert.That(currentSequence, Is.EqualTo(sequence), "Original request sequence cannot reset");
                var queries = coordinator.QueriesConsumed(key);
                Assert.That(queries, Is.GreaterThanOrEqualTo(spentQueries), "Spent query accounting cannot renew"); spentQueries = queries;
                CollectionAssert.AreEquivalent(attempted, (HashSet<int>)MovementRead(entry, "AttemptedIndices"));
                var currentFrozen = MovementRead(entry, "FrozenOwners");
                if (frozenOwners != null) Assert.That(currentFrozen, Is.SameAs(frozenOwners), "Original frozen dependency set cannot disappear");
                if (currentFrozen != null)
                {
                    var remaining = (float)MovementRead(entry, "WaitRemaining");
                    if (frozenOwners != null)
                    {
                        Assert.That(currentFrozen, Is.SameAs(frozenOwners));
                        Assert.That(MovementRead(entry, "FrozenResources"), Is.SameAs(frozenResources));
                        Assert.That(remaining, Is.LessThanOrEqualTo(waitRemaining), "Original frozen wait budget cannot renew");
                    }
                    frozenOwners = currentFrozen; frozenResources = MovementRead(entry, "FrozenResources"); waitRemaining = remaining;
                }
                var decision = coordinator.GetDecision(key);
                var state = decision.Kind + "/" + decision.FailureDisposition;
                if (state != lastState)
                { r.Facts.Add("rapidPending frame=" + UnityEngine.Time.frameCount + " state=" + state + " reason=" + decision.Reason + " sequence=" + currentSequence + " spentQueries=" + queries); lastState = state; }
                Assert.That(decision.Intent, Is.EqualTo(key));
                Assert.That(decision.FailureDisposition, Is.EqualTo(TrafficFailureDisposition.None), "NOT_PROVEN: stopped source, no retry or SafeLimited fallback");
                Assert.That(decision.Kind == TrafficDecisionKind.DeferredByBudget || decision.Kind == TrafficDecisionKind.WaitingForTraffic || decision.Kind == TrafficDecisionKind.Granted, Is.True,
                    "NOT_PROVEN: only authentic pending/prepared states may reach another original TrySpawn");
                Assert.That(MovementRead(entry, "Retired"), Is.EqualTo(false));
                Assert.That(MovementRead(entry, "AcquiredSequence"), Is.EqualTo(0L));
                Assert.That(MovementRead(entry, "EverCanonicalAttempt"), Is.EqualTo(false));
                Assert.That(MovementRead(entry, "Leg"), Is.Null); Assert.That(MovementRead(entry, "JourneyPermit"), Is.Null);
                Assert.That(customers.Snapshot.Any(value => value.VisitId == unbornId), Is.False);
                Assert.That(customers.CounterAssignments.Any(value => value.VisitId == unbornId), Is.False);
                Assert.That(customers.Capacity.GetReservations().Any(value => value.OwnerId == unbornId), Is.False);
                Assert.That(world.RegisteredActors.Any(value => value.ActorId == unbornId), Is.False);
                Assert.That(((IDictionary)MovementRead(world.Service, "actors")).Contains(unbornId), Is.False);
                Assert.That(((IDictionary)MovementRead(world.Service, "byActor")).Contains(unbornId), Is.False, "No Native movement before canonical birth");
                Assert.That(owner.Snapshot.Orders.Values.Any(value => value.CustomerId == unbornId), Is.False);
                Assert.That(owner.Snapshot.PaidVisitIds.Contains(unbornId), Is.False);
                return decision;
            }

            for (var frame = 0; frame < 6000; frame++)
            {
                R17Observe(r); Assert.That(r.Errors, Is.Empty, string.Join("\n", r.Errors));
                Assert.That(owner.HoldCollection, Is.True);
                Assert.That(owner.Snapshot.PaidVisitIds.Contains(first) && customers.Snapshot.Any(value => value.VisitId == first) && !r.Collected.Contains(first) && !r.Exits.Contains(first), Is.True,
                    "NOT_PROVEN: first Paid customer must remain live throughout second admission");
                Assert.That(customers.Snapshot.All(value => value.State != CustomerVisitState.Blocked), Is.True, customers.Status);
                Assert.That(employees.Snapshot.All(value => value.State != EmployeeState.Blocked), Is.True, owner.Snapshot.Failure);
                var preparedSource = ObservePending(); // Fail on Terminal/missing/replaced source BEFORE another TrySpawn.
                var grantedBeforeCall = preparedSource != null && preparedSource.Kind == TrafficDecisionKind.Granted && preparedSource.AcquiredSequence == 0;
                var originalVisits = customers.Snapshot.Select(value => value.VisitId).ToArray();
                var originalActors = world.RegisteredActors.ToArray(); var originalQueueVersion = queue.Version;
                var originalTokens = customers.Capacity.GetReservations().Select(value => value.Token).ToArray();
                var originalReservationFacts = customers.Capacity.GetReservations().Select(value => value.OwnerId + "/" + value.Kind + "/" + value.State).ToArray();
                var originalPaid = owner.Snapshot.PaidVisitIds.ToArray(); var originalOrders = owner.Snapshot.Orders.Keys.ToArray();
                if (customers.TrySpawn())
                {
                    Assert.That(grantedBeforeCall, Is.True, "NOT_PROVEN: birth lacked the original observed Granted source");
                    Assert.That(coordinator.TryGetCommittedJourney(key, out var committedSource), Is.True);
                    Assert.That(committedSource, Is.SameAs(preparedSource), "Exact observed original grant must own canonical birth");
                    Assert.That(committedSource.AcquiredSequence, Is.GreaterThan(0));
                    Assert.That(entries[key], Is.SameAs(entry)); Assert.That(MovementRead(entry, "FirstFrame"), Is.EqualTo(firstFrame));
                    Assert.That(MovementRead(entry, "RequestSequence"), Is.EqualTo(sequence));
                    Assert.That(coordinator.QueriesConsumed(key), Is.GreaterThanOrEqualTo(spentQueries));
                    Assert.That(MovementRead(entry, "EverCanonicalAttempt"), Is.EqualTo(true));
                    Assert.That(MovementRead(customers, "pendingAdmissionAnchor"), Is.Null);
                    Assert.That(MovementRead(customers, "nextVisit"), Is.EqualTo(before + 1));
                    Assert.That(customers.Snapshot.Where(value => !originalVisits.Contains(value.VisitId)).Select(value => value.VisitId), Is.EqualTo(new[] { unbornId }));
                    r.Facts.Add("rapidOriginalCanonicalBirth frame=" + UnityEngine.Time.frameCount + " id=" + unbornId + " firstFrame=" + firstFrame + " sequence=" + sequence +
                        " acquired=" + committedSource.AcquiredSequence + " spentQueries=" + coordinator.QueriesConsumed(key) + " firstStillPaidHeld=" + first);
                    yield break;
                }
                CollectionAssert.AreEqual(originalVisits, customers.Snapshot.Select(value => value.VisitId));
                CollectionAssert.AreEqual(originalActors, world.RegisteredActors); Assert.That(queue.Version, Is.EqualTo(originalQueueVersion));
                CollectionAssert.AreEqual(originalTokens, customers.Capacity.GetReservations().Select(value => value.Token));
                CollectionAssert.AreEqual(originalReservationFacts, customers.Capacity.GetReservations().Select(value => value.OwnerId + "/" + value.Kind + "/" + value.State));
                CollectionAssert.AreEqual(originalPaid, owner.Snapshot.PaidVisitIds); CollectionAssert.AreEqual(originalOrders, owner.Snapshot.Orders.Keys);
                ObservePending(); // false may be Deferred/Waiting; it never proves a safety/capacity refusal.
                yield return null;
            }
            Assert.Fail("NOT_PROVEN: original rapid admission did not commit within the existing fixture window; no sequential fallback or source restart");
        }

        private string R17CaptureBirth(R17Witness r)
        {
            var id = customers.Snapshot.Single(v => !r.Visits.Contains(v.VisitId)).VisitId;
            var actor = world.RegisteredActors.Single(value => value.ActorId == id);
            r.Visits.Add(id); r.Actors.Add(id, actor);
            r.CounterTargets.Add(id, customers.CounterAssignments.Single(value => value.VisitId == id).Position);
            r.Tokens.Add(id, customers.Capacity.GetReservations().Where(value => value.OwnerId == id).Select(value => value.Token).ToArray());
            var total = customers.Capacity.GetReservations().Single(value => value.OwnerId == id && value.Kind == CapacityKind.TotalCustomers).Token;
            r.Totals.Add(id, total);
            r.Facts.Add("birth frame=" + UnityEngine.Time.frameCount + " id=" + id + " actor=" + actor.GetEntityId() + " position=" + actor.transform.position.ToString("F6"));
            r.Facts.Add("originalTokens " + id + " " + string.Join(";", r.Tokens[id].Select(value => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value))) + " total=" + System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(total));
            var observer = actor.gameObject.AddComponent<CafeActualExitObserver>();
            r.Observers.Add(observer); observer.VisitId = id;
            observer.Receipt = (visit, pose) =>
            {
                if (!r.Tracking) return;
                var released = customers.Capacity.GetReservations().Single(value => ReferenceEquals(value.Token, total)).State == ReservationState.Released;
                var completed = owner.Snapshot.Orders.Values.SingleOrDefault(value => value.CustomerId == visit)?.State == OrderState.Completed;
                var nearExit = Vector3.Distance(pose, CafeLoopRuntimeProof.Field<Transform>(customers, "exit").position) <= .081f;
                if (visit != id || !nearExit || !released || !completed || !r.Exits.Add(visit)) r.Errors.Add("invalid/duplicate actual Exit receipt: " + visit);
                r.Facts.Add("actualExit frame=" + UnityEngine.Time.frameCount + " id=" + visit + " pose=" + pose.ToString("F6") + " originalTotalReleased=" + released + " completed=" + completed);
            };
            return id;
        }

        private IEnumerator R17Ready(R17Witness r, string id)
        {
            yield return R17Wait(r, () => r.PickupArrivals.Contains(id) && owner.Snapshot.Orders.Values.Any(o => o.CustomerId == id && o.State == OrderState.ReadyForPickup)
                && employees.Snapshot.All(e => e.State == EmployeeState.Idle), "ready-with-real-arrival-" + id);
            var delivery = owner.Snapshot.Delivery.Single(value => value.VisitId == id);
            Assert.That(delivery.Address, Is.EqualTo(new SurfaceSlotAddress(r.Support, r.Slot)));
            employees.TryGetBinding(EmployeeRole.Barista, out var binding);
            var actor = world.RegisteredActors.Single(value => value.ActorId == employees.Snapshot.Single(e => e.Role == EmployeeRole.Barista).ActorId);
            Assert.That(Vector3.Distance(actor.transform.position, binding.WorkTarget.Position), Is.LessThanOrEqualTo(.081f));
            var registry = Find<FurnitureSceneRegistry>(); Assert.That(registry.TryGet(r.Support, out var support), Is.True);
            var marker = support.GetComponentsInChildren<SurfaceSlotMarker>().Single(value => value.SlotId == r.Slot).transform;
            var key = owner.Snapshot.Orders.Single(pair => pair.Value.CustomerId == id).Key;
            Assert.That(Find<PickupCupDisplay>().VisibleOrders[key].transform.parent, Is.SameAs(marker));
        }

        private IEnumerator R17Wait(R17Witness r, Func<bool> done, string stage)
        {
            r.Stage = stage;
            yield return Until(() => { R17Observe(r); return r.Errors.Count != 0 || done(); });
            Assert.That(r.Errors, Is.Empty, string.Join("\n", r.Errors));
            Assert.That(done(), Is.True, "seed=" + r.Seed + " stage=" + stage);
        }

        private void R17Observe(R17Witness r)
        {
            r.DeliveryWitness?.Sample();
            r.Collected.UnionWith(owner.Snapshot.CollectedExitVisitIds);
            foreach (var pair in owner.Snapshot.Orders.Where(value => r.Visits.Contains(value.Value.CustomerId)))
            {
                var id = pair.Value.CustomerId;
                if (r.Orders.TryGetValue(id, out var original) && !original.Equals(pair.Key)) r.Errors.Add("Original order key replaced: " + id);
                else if (!r.Orders.ContainsKey(id))
                { r.Orders.Add(id, pair.Key); r.Facts.Add("originalOrder " + id + " key=" + pair.Key.SessionId + "/" + pair.Key.Epoch + "/" + pair.Key.OrderId + " product=" + pair.Value.ProductId); }
            }
            foreach (var row in customers.CafeSnapshot.Where(value => r.Visits.Contains(value.VisitId)))
            {
                if (row.ExitArrived)
                {
                    // CafeSnapshot retains an Exit row after unregister; only the original OnDestroy receipt records Exits.
                    // 历史行不伪造真实离店回执；仍核原Total与原到站pose，等待实际销毁callback。
                    if (customers.Snapshot.Any(value => value.VisitId == row.VisitId)) r.Errors.Add("Exit snapshot still owns a live visit: " + row.VisitId);
                    var releasedTotal = customers.Capacity.GetReservations().Single(value => ReferenceEquals(value.Token, r.Totals[row.VisitId]));
                    if (releasedTotal.State != ReservationState.Released || Vector3.Distance(row.Position, CafeLoopRuntimeProof.Field<Transform>(customers, "exit").position) > .081f)
                        r.Errors.Add("Historical Exit lacks original released Total/actual exit pose: " + row.VisitId);
                    continue;
                }
                var actor = world.RegisteredActors.Single(value => value.ActorId == row.VisitId);
                if (!ReferenceEquals(actor, r.Actors[row.VisitId])) r.Errors.Add("Actor life replaced: " + row.VisitId);
                if (row.CounterArrived && !r.CounterArrivals.Contains(row.VisitId))
                {
                    if (Vector3.Distance(actor.transform.position, r.CounterTargets[row.VisitId]) <= .081f)
                    { r.CounterArrivals.Add(row.VisitId); r.Facts.Add("actualCounterArrival " + row.VisitId + " " + actor.transform.position.ToString("F6")); }
                }
                var assignment = customers.PickupAssignments.FirstOrDefault(value => value.VisitId == row.VisitId);
                if (row.PickupArrived && assignment.VisitId == row.VisitId && assignment.Arrived && !r.PickupArrivals.Contains(row.VisitId) && Vector3.Distance(actor.transform.position, assignment.Position) <= .081f)
                { r.PickupArrivals.Add(row.VisitId); r.Facts.Add("actualPickupArrival " + row.VisitId + " generation=" + assignment.Generation + " slot=" + assignment.SlotIndex + " pose=" + actor.transform.position.ToString("F6")); }
                var total = customers.Capacity.GetReservations().Single(value => ReferenceEquals(value.Token, r.Totals[row.VisitId]));
                if (total.State == ReservationState.Released && Vector3.Distance(actor.transform.position, CafeLoopRuntimeProof.Field<Transform>(customers, "exit").position) > .081f)
                    r.Errors.Add("Total released away from actual Exit: " + row.VisitId);
            }
            var paid = owner.Snapshot.PaidVisitIds.Where(r.Visits.Contains).ToArray();
            var queue = customers.PickupAssignments.Select(value => value.VisitId).ToArray();
            var activePaid = paid.Where(queue.Contains).ToArray();
            if (!activePaid.SequenceEqual(queue.Where(paid.Contains))) r.Errors.Add("Actual paid pickup FIFO changed");
        }

        private void R17Task(R17Witness r, CafeTaskEvent e)
        {
            if (!r.Tracking || (e.Kind != CafeTaskEventKind.ServiceFinished && e.Kind != CafeTaskEventKind.Prepared && e.Kind != CafeTaskEventKind.DeliveryArrived)) return;
            var id = e.Checkpoint.VisitId;
            var counts = e.Kind == CafeTaskEventKind.ServiceFinished ? r.Payments : e.Kind == CafeTaskEventKind.Prepared ? r.Preparations : r.Deliveries;
            counts[id] = counts.TryGetValue(id, out var value) ? value + 1 : 1;
            r.Facts.Add("task " + e.Kind + " visit=" + id + " actor=" + e.ActorId + " handle=" + e.HandleId + " request=" + e.RequestId);
            if (e.Kind != CafeTaskEventKind.DeliveryArrived) return;
            var actual = world.RegisteredActors.Single(actor => actor.ActorId == e.ActorId);
            if (!employees.TryGetDeliveryTarget(out var target) || Vector3.Distance(actual.transform.position, target.Position) > .081f)
            { r.Errors.Add("DeliveryArrived without actual target arrival: " + id); return; }
            // Geometry corroboration only, following MatrixFinish; never substituted for actual movement.
            if (r.Seed == 1501705)
            {
                employees.TryGetBinding(EmployeeRole.Barista, out var binding);
                var native = adapter.GetCompletePath(binding.WorkTarget.Position, target.Position);
                if (native == null || native.Count <= 2) r.Errors.Add("Counter-corner recipe has no complete cornered Native path");
                r.Facts.Add("postArrivalNativeGeometry " + string.Join(";", native ?? Array.Empty<Vector3>()));
            }
        }

        private bool R17Drained() => customers.Snapshot.Count == 0 && owner.Snapshot.Delivery.Count == 0
            && customers.Capacity != null && customers.Capacity.GetCapacities().All(value => value.Used == 0)
            && employees.Snapshot.All(value => value.State == EmployeeState.Idle) && owner.GetDrainSnapshot().IsDrained;

        private IEnumerator R17DisconnectedReject(R17Witness r)
        {
            var runtime = Find<CafeLayoutRuntime>(); var decor = Find<DecorationModeController>();
            var catalog = CafeLoopRuntimeProof.Field<FurnitureContentCatalog>(decor, "contentCatalog").BuildSurfaceSlotCatalog(runtime.Layout.GridSettings);
            var exposed = new InteractionAnchorResolver().ResolvePickUpCandidates(runtime.FunctionalSurfaceLayout.PickUpPoints.Single(), runtime.Layout, catalog,
                point => runtime.Layout.IsInsideUnlockedRegion(point) && !runtime.Layout.TryGetOccupant(point, out _));
            Assert.That(exposed.Count, Is.EqualTo(4), "Exposed but disconnected, not zero exposed sides");
            decor.ExitDecorationMode(); r.Stage = "await-explicit-disconnected-refusal";
            var terminal = false;
            for (var frame = 0; frame < 6000 && !terminal; frame++)
            {
                Assert.That(customers.TrySpawn(), Is.False, "Disconnected layout must never publish a real admission");
                Assert.That(customers.Snapshot, Is.Empty); Assert.That(owner.Snapshot.Orders, Is.Empty);
                Assert.That(owner.Snapshot.PaidVisitIds, Is.Empty); Assert.That(customers.CounterAssignments, Is.Empty);
                if (customers.Capacity != null) Assert.That(customers.Capacity.GetReservations(), Is.Empty);
                // Reject a concrete terminal reason; a first-frame deferred preparation cannot pass.
                terminal = !adapter.CurrentReadiness.CanResume && adapter.CurrentReadiness.Reason == "No reachable essential service combination"
                    && adapter.CurrentReadiness.StationFailures.Values.Contains("No connected essential service combination")
                    && time.IsResumeBlocked && !owner.CanProgress && !string.IsNullOrEmpty(owner.ViewSnapshot.Reception);
                if (!terminal) yield return null;
            }
            Assert.That(terminal, Is.True, "No explicit terminal disconnected receipt; preparation/UNKNOWN is not PASS");
            Assert.That(customers.Capacity, Is.Null, "Invalid initial business must not allocate an admission ledger");
            r.Facts.Add("explicitReject reason=" + adapter.CurrentReadiness.Reason + " resume=" + time.ResumeBlockReason + " reception=" + owner.ViewSnapshot.Reception);
            // Intentionally no repair: this wrapper covers only the independently frozen reject classification.
        }

        private IEnumerator R17PlacementReject(R17Witness r)
        {
            var decor = Find<DecorationModeController>(); var runtime = Find<CafeLayoutRuntime>();
            var before = R17ConfirmedLayout();
            var asset = AssetDatabase.FindAssets("t:FurnitureDefinitionAsset").Select(guid => AssetDatabase.LoadAssetAtPath<FurnitureDefinitionAsset>(AssetDatabase.GUIDToAssetPath(guid)))
                .Single(value => value.DefinitionId == "counter.preset.1x2");
            Assert.That(decor.TrySelectCatalogueItem(new DecorationCatalogueItemModel(asset.DefinitionId, asset.DisplayName, null, DecorationCatalogueItemKind.Furniture, false, asset)), Is.True);
            var session = CafeLoopRuntimeProof.Field<DecorationSession>(decor, "session");
            var entrance = runtime.Layout.Reservations.Single(value => value.Type == LayoutReservationType.EntranceClearance);
            var result = session.MovePreview(entrance.Origin);
            Assert.That(result.Succeeded, Is.False); Assert.That(result.FailureReason, Is.EqualTo(PlacementFailureReason.ReservedEntranceClearance));
            Assert.That(runtime.Layout.GetFurnitureFootprintCells(asset.DefinitionId, entrance.Origin, FurnitureRotation.Degrees0).Count, Is.EqualTo(2));
            var confirm = typeof(DecorationModeController).GetMethod("HandleConfirmRequested", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(confirm, Is.Not.Null); confirm.Invoke(decor, null); // Same controller-confirm fixture as Matrix entrance rejection.
            Assert.That(session.ActivePreview, Is.Not.Null); Assert.That(R17ConfirmedLayout(), Is.EqualTo(before));
            Assert.That(customers.Snapshot, Is.Empty); Assert.That(owner.Snapshot.Orders, Is.Empty); Assert.That(owner.Snapshot.PaidVisitIds, Is.Empty);
            r.Stage = "placement-reject-only"; r.Facts.Add("placementReject definition=" + asset.DefinitionId + " entrance=" + entrance.Origin + " reason=" + result.FailureReason + " trafficCoverage=NONE");
            decor.CancelActivePreview(); Assert.That(session.ActivePreview, Is.Null);
            R17WriteRecipe(r, "placement-reject-only");
            yield return null;
        }

        private string R17ConfirmedLayout()
        {
            var runtime = Find<CafeLayoutRuntime>();
            return string.Join(";", runtime.Layout.FurnitureInstances.OrderBy(value => value.InstanceId, StringComparer.Ordinal)
                .Select(value => value.InstanceId + "/" + value.DefinitionId + "/" + value.Position.X + "," + value.Position.Y + "/" + (int)value.Rotation));
        }

        private void R17WriteRecipe(R17Witness r, string stage)
        {
            // Each failing seed retains its exact confirmed recipe and stage independently.
            var runtime = Find<CafeLayoutRuntime>();
            var mounted = runtime.FunctionalSurfaceLayout.MountedInstances.Select(value => value.InstanceId + "/" + value.DefinitionId + "/" + value.Address.SupportFurnitureInstanceId + "/" + value.Address.SlotId + "/surfaceRotation=" + value.Rotation);
            var pickup = runtime.FunctionalSurfaceLayout.PickUpPoints.Select(value => value.InstanceId + "/" + value.Address.SupportFurnitureInstanceId + "/" + value.Address.SlotId);
            File.WriteAllText(Path.Combine(r.Directory, stage + ".txt"), "seed=" + r.Seed + " expected=" + R17Expected(r.Seed) +
                "\narrivalMode=" + (r.Seed == 1501709 || r.Seed == 1501711 ? "NoOperatingArrival" : r.Seed == 1501707 || r.Seed == 1501708 ? "AfterActualPaid" : "SequentialAfterActualExit") +
                "\nmethod=" + TestContext.CurrentContext.Test.FullName + "\nscene=" + scene.path + "\nfurniture=" + R17ConfirmedLayout() +
                "\nmounted=" + string.Join(";", mounted) + "\npickup=" + string.Join(";", pickup));
        }
    }
}
#endif
