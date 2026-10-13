using UnityEngine;
namespace AnimalCafe.Customers
{
    public enum CustomerVisitState { Entering, Queued, Advancing, Exiting, Blocked, Removed }
    public readonly struct QueueAssignment
    {
        public string VisitId { get; }
        public int SlotIndex { get; }
        public int Generation { get; }
        public Vector3 Position { get; }
        public bool Arrived { get; }
        public QueueAssignment(string id, int slot, int generation, Vector3 position, bool arrived)
        { VisitId=id; SlotIndex=slot; Generation=generation; Position=position; Arrived=arrived; }
    }
    public readonly struct CustomerVisitSnapshot
    {
        public string VisitId { get; }
        public CustomerVisitState State { get; }
        public Vector3 Position { get; }
        public CustomerVisitSnapshot(string id, CustomerVisitState state, Vector3 position)
        { VisitId=id; State=state; Position=position; }
    }
}
