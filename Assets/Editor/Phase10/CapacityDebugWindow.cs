using System.Collections.Generic;
using System.Linq;
using AnimalCafe.Capacity;
using UnityEditor;
using UnityEngine;

namespace AnimalCafe.EditorTools.Phase10
{
    public sealed class CapacityDebugWindow : EditorWindow
    {
        private bool clearInputFocusOnNextGui;

        internal CapacityDebugSession Session { get; private set; }
        internal int FloorCells { get; set; }
        internal string OwnerId { get; set; }
        internal long TokenId { get; set; }
        internal bool IncludeTotal { get; set; }
        internal bool IncludeCounter { get; set; }
        internal bool IncludePickUp { get; set; }
        internal Vector2 ScrollPosition { get; set; }
        internal CapacityResult LastResult { get; private set; }
        internal IReadOnlyList<CapacitySnapshot> Capacities => Session.Service.GetCapacities();
        internal IReadOnlyList<ReservationSnapshot> Reservations => Session.Service.GetReservations();

        [MenuItem("Window/AnimalCafe/Phase 10 Capacity Debug")]
        private static void Open()
        {
            GetWindow<CapacityDebugWindow>("Phase 10 Capacity Debug");
        }

        private void OnEnable()
        {
            // 重复订阅前先解绑，兼容 Domain Reload 开/关。
            // Unsubscribe before subscribing for either Domain Reload setting.
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            ResetSession();
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            Session = null;
            LastResult = null;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode ||
                state == PlayModeStateChange.ExitingPlayMode)
            {
                ResetSession();
            }
        }

        internal void ResetSession()
        {
            if (Session == null) Session = new CapacityDebugSession();
            else Session.Reset();
            FloorCells = 64;
            OwnerId = "V1";
            TokenId = 1;
            IncludeTotal = true;
            IncludeCounter = true;
            IncludePickUp = true;
            LastResult = null;
            ScrollPosition = Vector2.zero;
            clearInputFocusOnNextGui = true;
            Repaint();
        }

        internal CapacityResult RunApplyFloor() =>
            LastResult = Session.Service.UpdateFloorCellCount(FloorCells);

        internal CapacityResult RunReserveAdmission() =>
            LastResult = Session.Service.TryReserveAdmission(OwnerId);

        internal CapacityResult RunReserveSelected()
        {
            var kinds = new List<CapacityKind>(3);
            if (IncludeTotal) kinds.Add(CapacityKind.TotalCustomers);
            if (IncludeCounter) kinds.Add(CapacityKind.CounterQueue);
            if (IncludePickUp) kinds.Add(CapacityKind.PickUp);
            return LastResult = Session.Service.TryReserve(OwnerId, kinds);
        }

        internal CapacityResult RunOccupy() =>
            LastResult = Session.Service.Occupy(FindCurrentToken(), OwnerId);

        internal CapacityResult RunRelease() =>
            LastResult = Session.Service.Release(FindCurrentToken(), OwnerId);

        private CapacityToken FindCurrentToken()
        {
            // 只查本 session 已签发 token；找不到就交 null 给 domain 处理。
            // Only resolve tokens issued in this session; pass null when absent.
            return Reservations.FirstOrDefault(x => x.Token.Id == TokenId)?.Token;
        }

        private void OnGUI()
        {
            if (clearInputFocusOnNextGui)
            {
                GUI.FocusControl(null);
                clearInputFocusOnNextGui = false;
            }

            EditorGUILayout.HelpBox("P10 Debug / 临时容量模拟；不是真实营业、站位或 spawn", MessageType.Info);
            FloorCells = EditorGUILayout.IntField("Floor cells (input)", FloorCells);
            EditorGUILayout.LabelField("Applied floor cells", Session.Service.Limits.FloorCellCount.ToString());
            OwnerId = EditorGUILayout.TextField("Owner ID", OwnerId);
            TokenId = EditorGUILayout.LongField("Token ID", TokenId);
            IncludeTotal = EditorGUILayout.Toggle("TotalCustomers", IncludeTotal);
            IncludeCounter = EditorGUILayout.Toggle("CounterQueue", IncludeCounter);
            IncludePickUp = EditorGUILayout.Toggle("PickUp", IncludePickUp);

            if (GUILayout.Button("Apply Floor")) RunApplyFloor();
            if (GUILayout.Button("Reserve Admission")) RunReserveAdmission();
            if (GUILayout.Button("Reserve Selected")) RunReserveSelected();
            if (GUILayout.Button("Occupy")) RunOccupy();
            if (GUILayout.Button("Release")) RunRelease();
            if (GUILayout.Button("Reset Session")) ResetSession();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Last result", EditorStyles.boldLabel);
            if (LastResult == null) EditorGUILayout.LabelField("(none)");
            else EditorGUILayout.LabelField(LastResult.Succeeded ? "Success" : "Error",
                LastResult.Succeeded
                    ? string.Join(", ", LastResult.Reservations.Select(x => "#" + x.Token.Id))
                    : LastResult.FailureReason.ToString());

            EditorGUILayout.LabelField("CanAdmit", Session.Service.CanAdmit.ToString());
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Capacities", EditorStyles.boldLabel);
            ScrollPosition = EditorGUILayout.BeginScrollView(ScrollPosition);
            EditorGUILayout.LabelField("Kind | Limit | Reserved | Occupied | Used | Available | OverCapacity");
            foreach (var capacity in Capacities)
            {
                EditorGUILayout.LabelField(string.Format("{0} | {1} | {2} | {3} | {4} | {5} | {6}",
                    capacity.Kind, capacity.Limit, capacity.Reserved, capacity.Occupied,
                    capacity.Used, capacity.Available, capacity.OverCapacity));
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("All tokens (including Released)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Token ID | Owner ID | Kind | State");
            foreach (var reservation in Reservations)
            {
                EditorGUILayout.LabelField(string.Format("#{0} | {1} | {2} | {3}",
                    reservation.Token.Id, reservation.OwnerId, reservation.Kind, reservation.State));
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
