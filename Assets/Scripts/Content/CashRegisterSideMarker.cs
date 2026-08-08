using System;
using AnimalCafe.Layout;
using UnityEngine;

namespace AnimalCafe.Content
{
    public sealed class CashRegisterSideMarker : MonoBehaviour
    {
        [SerializeField] private CashRegisterSideType sideType;
        [SerializeField] private CardinalDirection localDirection;

        public CashRegisterSideType SideType => sideType;
        public CardinalDirection LocalDirection => localDirection;

        public static CashRegisterSides ReadSidesFrom(GameObject root)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            CashRegisterSideMarker employeeMarker = null;
            CashRegisterSideMarker customerMarker = null;
            var markers = root.GetComponentsInChildren<CashRegisterSideMarker>(true);

            foreach (var marker in markers)
            {
                if (marker.sideType == CashRegisterSideType.Employee)
                {
                    if (employeeMarker != null)
                    {
                        throw new ArgumentException(
                            "Cash Register root must have exactly one Employee side marker.",
                            nameof(root));
                    }

                    employeeMarker = marker;
                }
                else if (marker.sideType == CashRegisterSideType.Customer)
                {
                    if (customerMarker != null)
                    {
                        throw new ArgumentException(
                            "Cash Register root must have exactly one Customer side marker.",
                            nameof(root));
                    }

                    customerMarker = marker;
                }
                else
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(sideType),
                        marker.sideType,
                        "Cash Register side marker must use a defined side type.");
                }
            }

            if (employeeMarker == null || customerMarker == null)
            {
                throw new ArgumentException(
                    "Cash Register root must have one Employee and one Customer side marker.",
                    nameof(root));
            }

            return new CashRegisterSides(
                employeeMarker.localDirection,
                customerMarker.localDirection);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = sideType == CashRegisterSideType.Employee
                ? Color.green
                : Color.yellow;

            var position = transform.position;
            Gizmos.DrawLine(position, position + ToWorldDirection() * 0.3f);
        }

        private Vector3 ToWorldDirection()
        {
            switch (localDirection)
            {
                case CardinalDirection.North:
                    return transform.forward;
                case CardinalDirection.East:
                    return transform.right;
                case CardinalDirection.South:
                    return -transform.forward;
                case CardinalDirection.West:
                    return -transform.right;
                default:
                    return Vector3.zero;
            }
        }
    }
}
