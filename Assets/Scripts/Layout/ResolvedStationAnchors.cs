using System;
using System.Collections.Generic;

namespace AnimalCafe.Layout
{
    public sealed class ResolvedStationAnchors
    {
        private static readonly ResolvedStationAnchors EmptySnapshot =
            new ResolvedStationAnchors(Array.Empty<InteractionAnchor>());

        private readonly Dictionary<InteractionRole, InteractionAnchor> anchorsByRole;

        public static ResolvedStationAnchors Empty => EmptySnapshot;
        public IReadOnlyList<InteractionAnchor> Anchors { get; }

        public ResolvedStationAnchors(IEnumerable<InteractionAnchor> anchors)
        {
            if (anchors == null)
            {
                throw new ArgumentNullException(nameof(anchors));
            }

            var snapshot = new List<InteractionAnchor>();
            anchorsByRole = new Dictionary<InteractionRole, InteractionAnchor>();
            foreach (var anchor in anchors)
            {
                if (anchorsByRole.ContainsKey(anchor.Role))
                {
                    throw new ArgumentException(
                        $"Resolved anchors contain duplicate role '{anchor.Role}'.",
                        nameof(anchors));
                }

                anchorsByRole.Add(anchor.Role, anchor);
                snapshot.Add(anchor);
            }

            Anchors = snapshot.AsReadOnly();
        }

        public bool TryGetAnchor(InteractionRole role, out InteractionAnchor anchor)
        {
            if (!Enum.IsDefined(typeof(InteractionRole), role))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(role),
                    role,
                    "Interaction role must be a defined value.");
            }

            return anchorsByRole.TryGetValue(role, out anchor);
        }
    }
}
