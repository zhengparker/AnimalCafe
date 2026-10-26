using System;
using System.Collections.Generic;
using System.Linq;
using AnimalCafe.Capacity;
using AnimalCafe.Customers;
using AnimalCafe.Employees;
using UnityEngine;
using UnityEngine.UI;

namespace AnimalCafe.CafeLoop
{
    public sealed class CafeLoopViewSnapshot
    {
        public CafeLoopSnapshot Business { get; }
        public IReadOnlyList<QueueAssignment> Counter { get; }
        public IReadOnlyList<QueueAssignment> Pickup { get; }
        public IReadOnlyList<EmployeeTaskSnapshot> Employees { get; }
        public IReadOnlyList<CapacitySnapshot> Capacities { get; }
        public int VisibleCups { get; }
        public string Reception { get; }
        public CafeLoopViewSnapshot(CafeLoopSnapshot business,IReadOnlyList<QueueAssignment> counter=null,
            IReadOnlyList<QueueAssignment> pickup=null,IReadOnlyList<EmployeeTaskSnapshot> employees=null,
            IReadOnlyList<CapacitySnapshot> capacities=null,int visibleCups=0,string reception="")
        {
            Business=business ?? throw new ArgumentNullException(nameof(business));
            Counter=new List<QueueAssignment>(counter ?? Array.Empty<QueueAssignment>()).AsReadOnly();
            Pickup=new List<QueueAssignment>(pickup ?? Array.Empty<QueueAssignment>()).AsReadOnly();
            Employees=new List<EmployeeTaskSnapshot>(employees ?? Array.Empty<EmployeeTaskSnapshot>()).AsReadOnly();
            Capacities=new List<CapacitySnapshot>(capacities ?? Array.Empty<CapacitySnapshot>()).AsReadOnly();
            VisibleCups=visibleCups; Reception=reception ?? "";
        }
    }
    public sealed class CafeLoopStatusView:MonoBehaviour
    {
        [SerializeField] private CafeLoopController owner;
        [SerializeField] private Text status;
        [SerializeField] private GameObject hud;
        public string CurrentText => status==null?"":status.text;
        private CafeLoopController subscribed;
        public void Bind(CafeLoopController value)
        { Unbind(); owner=value; Subscribe(); Refresh(); }
        private void OnEnable() { Subscribe(); Refresh(); }
        private void OnDisable() => Unbind();
        private void OnDestroy() => Unbind();
        private void Subscribe()
        { if(!isActiveAndEnabled || owner==null || subscribed==owner) return; subscribed=owner; subscribed.SnapshotChanged+=OnSnapshot; }
        private void Unbind() { if(subscribed!=null) subscribed.SnapshotChanged-=OnSnapshot; subscribed=null; }
        private void OnSnapshot(CafeLoopSnapshot _) => Refresh();
        private void Refresh()
        {
            if(owner==null || status==null) return;
            status.text=Format(owner.ViewSnapshot);
            if(hud!=null) hud.SetActive(!owner.ViewSnapshot.Reception.StartsWith("Decoration",StringComparison.Ordinal));
        }
        public static string Format(CafeLoopViewSnapshot value)
        {
            if(value==null) return "等待P14接线 / Waiting for cafe";
            var business=value.Business;
            var lines=new List<string>{Trim(value.Reception,70),
                "Epoch "+business.Epoch+"  Cups "+value.VisibleCups+"/"+business.Delivery.Count+"  Completed "+business.Orders.Values.Count(o=>o.State==AnimalCafe.Orders.OrderState.Completed),
                string.Join("  ",value.Capacities.Select(c=>(c.Kind==CapacityKind.TotalCustomers?"Total":c.Kind==CapacityKind.CounterQueue?"Counter":"Pickup")+" "+c.Used+"/"+c.Limit)),
                "Counter "+Queue(value.Counter,3),"Pickup "+Queue(value.Pickup,5)};
            foreach(var employee in value.Employees)
            {
                var work=business.Employees.FirstOrDefault(w=>w.Role==employee.Role);
                lines.Add(employee.Role+" "+employee.State+" h"+employee.HandleId+
                    (work.Order.HasValue?" O"+work.Order.Value.OrderId:"")+" "+employee.RemainingWorkSeconds.ToString("F1")+"s");
            }
            foreach(var pair in business.Orders.Where(p=>p.Value.State!=AnimalCafe.Orders.OrderState.Completed).OrderBy(p=>p.Key.OrderId).Take(2))
                lines.Add("O "+Short(pair.Key.SessionId)+"/"+pair.Key.Epoch+"/"+pair.Key.OrderId+" @"+Short(pair.Value.CustomerId)+" "+pair.Value.State);
            if(!string.IsNullOrEmpty(business.Failure)) lines.Add(Trim(business.Failure,70));
            return string.Join("\n",lines);
        }
        private static string Queue(IReadOnlyList<QueueAssignment> values,int max) => values.Count==0?"—":
            string.Join(" ",values.Take(max).Select((v,i)=>(i+1)+":"+Short(v.VisitId)))+(values.Count>max?" +"+(values.Count-max):"");
        private static string Short(string value) => string.IsNullOrEmpty(value)?"—":value.Substring(Math.Max(0,value.Length-4));
        private static string Trim(string value,int max) => string.IsNullOrEmpty(value)?"":value.Length<=max?value:value.Substring(0,max)+"…";
    }
}
