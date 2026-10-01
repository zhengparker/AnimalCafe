using System.Collections.Generic;
using System.Linq;
using AnimalCafe.Orders;
using UnityEditor;
using UnityEngine;

namespace AnimalCafe.EditorTools.Phase9
{
    public sealed class OrderDomainDebugWindow : EditorWindow
    {
        private Vector2 scrollPosition;
        private bool clearInputFocusOnNextGui;

        internal OrderDebugSession Session { get; private set; }
        internal string CustomerId { get; set; }
        internal string ProductId { get; set; }
        internal string EmployeeId { get; set; }
        internal long OrderId { get; set; }
        internal string FailureReason { get; set; }
        internal OrderResult LastResult { get; private set; }
        internal IReadOnlyList<OrderSnapshot> Orders => Session.Service.GetOrders();
        internal IReadOnlyList<long> WaitingOrderIds => Session.Service.GetWaitingOrderIds();

        [MenuItem("Window/AnimalCafe/Phase 9 Order Debug")]
        private static void Open()
        {
            GetWindow<OrderDomainDebugWindow>("Phase 9 Order Debug");
        }

        private void OnEnable()
        {
            // Domain Reload 开启或关闭时都在 Play 边界清理临时资料。
            // Clear temporary data at Play boundaries with either Domain Reload setting.
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
            if (Session == null) Session = new OrderDebugSession();
            else Session.Reset();
            CustomerId = "C1";
            ProductId = "coffee";
            EmployeeId = "E1";
            OrderId = 1;
            FailureReason = "diagnostic.stop";
            LastResult = null;
            scrollPosition = Vector2.zero;
            clearInputFocusOnNextGui = true;
            Repaint();
        }

        internal OrderResult RunCreate() => LastResult = Session.Service.Create(CustomerId, ProductId);
        internal OrderResult RunClaimNext() => LastResult = Session.Service.ClaimNext(EmployeeId);
        internal OrderResult RunStartPreparation() => LastResult = Session.Service.StartPreparation(OrderId, EmployeeId);
        internal OrderResult RunMarkReadyForPickup() => LastResult = Session.Service.MarkReadyForPickup(OrderId, EmployeeId);
        internal OrderResult RunSimulateCustomerCollection() => LastResult = Session.Service.Complete(OrderId, CustomerId);
        internal OrderResult RunFail() => LastResult = Session.Service.Fail(OrderId, FailureReason);

        private void OnGUI()
        {
            if (clearInputFocusOnNextGui)
            {
                // 在本窗口绘制前结束输入编辑，让重置后的值立即显示。
                // Clear this window's editing focus before drawing the reset values.
                GUI.FocusControl(null);
                clearInputFocusOnNextGui = false;
            }

            EditorGUILayout.HelpBox("P9 Debug / 临时模拟数据；不是真实营业", MessageType.Info);
            CustomerId = EditorGUILayout.TextField("Customer ID", CustomerId);
            ProductId = EditorGUILayout.TextField("Product ID", ProductId);
            EmployeeId = EditorGUILayout.TextField("Employee ID", EmployeeId);
            OrderId = EditorGUILayout.LongField("Order ID", OrderId);
            FailureReason = EditorGUILayout.TextField("Failure reason", FailureReason);

            if (GUILayout.Button("Create")) RunCreate();
            if (GUILayout.Button("Claim Next")) RunClaimNext();
            if (GUILayout.Button("Start Preparation")) RunStartPreparation();
            if (GUILayout.Button("Mark Ready For Pickup")) RunMarkReadyForPickup();
            if (GUILayout.Button("Simulate Customer Collection")) RunSimulateCustomerCollection();
            if (GUILayout.Button("Fail")) RunFail();
            if (GUILayout.Button("Reset Session")) ResetSession();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Last result", EditorStyles.boldLabel);
            if (LastResult == null) EditorGUILayout.LabelField("(none)");
            else if (LastResult.Succeeded)
                EditorGUILayout.LabelField("Success", "Order #" + LastResult.Order.OrderId);
            else EditorGUILayout.LabelField("Error", LastResult.FailureReason.ToString());

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Waiting FIFO", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(WaitingOrderIds.Count == 0
                ? "(empty)"
                : string.Join(" → ", WaitingOrderIds.Select(id => id.ToString())));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("All orders", EditorStyles.boldLabel);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.LabelField("Order ID | State | Customer ID | Product ID | Claimant | Reason");
            foreach (var order in Orders)
            {
                EditorGUILayout.LabelField(string.Format("#{0} | {1} | {2} | {3} | {4} | {5}",
                    order.OrderId, order.State, order.CustomerId, order.ProductId,
                    order.ClaimantId ?? "-", order.TerminationReason ?? "-"));
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
