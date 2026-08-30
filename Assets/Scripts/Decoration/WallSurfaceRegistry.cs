using System;
using System.Collections.Generic;
using AnimalCafe.Layout;
using UnityEngine;

namespace AnimalCafe.Decoration
{
    /// <summary>Maps stable wall Surface IDs to their Scene-only render views.</summary>
    public sealed class WallSurfaceRegistry : MonoBehaviour
    {
        private readonly Dictionary<string, WallSurfaceView> viewsBySurfaceId =
            new Dictionary<string, WallSurfaceView>(StringComparer.Ordinal);

        public void Register(WallSurfaceView view)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            var surfaceId = view.SurfaceId;
            LayoutStableId.Validate(surfaceId, nameof(view));
            PurgeDestroyedViews();

            if (viewsBySurfaceId.TryGetValue(surfaceId, out var existing))
            {
                if (existing != null)
                {
                    throw new InvalidOperationException($"Duplicate Wall Surface ID '{surfaceId}'.");
                }

                viewsBySurfaceId.Remove(surfaceId);
            }

            viewsBySurfaceId.Add(surfaceId, view);
        }

        public bool TryGet(string surfaceId, out WallSurfaceView view)
        {
            if (!LayoutStableId.IsValid(surfaceId) ||
                !viewsBySurfaceId.TryGetValue(surfaceId, out view))
            {
                view = null;
                return false;
            }

            if (view != null)
            {
                return true;
            }

            viewsBySurfaceId.Remove(surfaceId);
            return false;
        }

        public void RenderConfirmed(RoomSurfaceLayout layout)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            PurgeDestroyedViews();
            foreach (var view in viewsBySurfaceId.Values)
            {
                view.RenderConfirmed(layout);
            }
        }

        public void RenderPreview(SurfacePreviewTransaction preview)
        {
            if (preview == null) throw new ArgumentNullException(nameof(preview));
            PurgeDestroyedViews();
            foreach (var view in viewsBySurfaceId.Values) view.RenderPreview(preview);
        }

        public void ClearPreview()
        {
            PurgeDestroyedViews();
            foreach (var view in viewsBySurfaceId.Values) view.ClearPreview();
        }

        public void SetSelectedSurface(string surfaceId)
        {
            PurgeDestroyedViews();
            foreach (var pair in viewsBySurfaceId)
            {
                pair.Value.SetSelected(string.Equals(pair.Key, surfaceId, StringComparison.Ordinal));
            }
        }

        public void ClearSelection() => SetSelectedSurface(null);

        private void PurgeDestroyedViews()
        {
            var destroyedIds = new List<string>();
            foreach (var pair in viewsBySurfaceId)
            {
                if (pair.Value == null)
                {
                    destroyedIds.Add(pair.Key);
                }
            }

            foreach (var surfaceId in destroyedIds)
            {
                viewsBySurfaceId.Remove(surfaceId);
            }
        }

    }
}
