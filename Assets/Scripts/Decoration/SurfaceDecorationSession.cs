using System;
using System.Collections.Generic;
using AnimalCafe.Content;
using AnimalCafe.Layout;

namespace AnimalCafe.Decoration
{
    public sealed class SurfaceDecorationSession
    {
        private readonly RoomSurfaceLayout confirmedLayout;
        private readonly Dictionary<string, StyleBinding> stylesById;
        private readonly string wainscotingNoneStyleId;
        private PreviewState activeState;

        public SurfacePreviewTransaction ActivePreview => CreateActiveView();

        public SurfaceDecorationSession(
            RoomSurfaceLayout confirmedLayout,
            IEnumerable<SurfaceStyleDefinitionAsset> styles)
        {
            this.confirmedLayout = confirmedLayout ??
                throw new ArgumentNullException(nameof(confirmedLayout));
            if (styles == null)
            {
                throw new ArgumentNullException(nameof(styles));
            }

            stylesById = new Dictionary<string, StyleBinding>(StringComparer.Ordinal);
            string noneStyleId = null;
            foreach (var style in styles)
            {
                if (style == null)
                {
                    throw new ArgumentException(
                        "Surface styles cannot contain null entries.",
                        nameof(styles));
                }

                ValidateStyleDefinition(style, nameof(styles));
                if (stylesById.ContainsKey(style.StyleId))
                {
                    throw new ArgumentException(
                        "Surface style IDs must be unique.",
                        nameof(styles));
                }

                var binding = new StyleBinding(
                    style.StyleId,
                    style.Kind,
                    style.IsNoneOption);
                stylesById.Add(binding.Id, binding);
                if (binding.IsNone)
                {
                    if (noneStyleId != null)
                    {
                        throw new ArgumentException(
                            "Exactly one Wainscoting None style is required.",
                            nameof(styles));
                    }

                    noneStyleId = binding.Id;
                }
            }

            if (noneStyleId == null)
            {
                throw new ArgumentException(
                    "Exactly one Wainscoting None style is required.",
                    nameof(styles));
            }

            wainscotingNoneStyleId = noneStyleId;
        }

        public SurfaceSessionResult BeginWall(string surfaceId)
        {
            if (activeState != null &&
                (activeState.Scope != SurfaceEditScope.Wall || HasActiveChanges()))
            {
                return SurfaceSessionResult.Failure(
                    SurfaceSessionFailure.ActivePreviewMustFinish);
            }

            if (!TryGetWall(surfaceId))
            {
                return SurfaceSessionResult.Failure(SurfaceSessionFailure.UnknownTarget);
            }

            activeState = new PreviewState(
                confirmedLayout.CaptureSnapshot(),
                SurfaceEditScope.Wall,
                surfaceId,
                SurfaceStyleKind.Paint,
                null);
            return SurfaceSessionResult.Success();
        }

        public SurfaceSessionResult BeginWholeRoomFloor()
        {
            var beginGate = CheckBeginGate();
            if (!beginGate.Succeeded)
            {
                return beginGate;
            }

            activeState = new PreviewState(
                confirmedLayout.CaptureSnapshot(),
                SurfaceEditScope.WholeRoomFloor,
                null,
                SurfaceStyleKind.Floor,
                null);
            return SurfaceSessionResult.Success();
        }

        public SurfaceSessionResult BeginSingleGridFloor(GridPosition position)
        {
            var beginGate = CheckBeginGate();
            if (!beginGate.Succeeded)
            {
                return beginGate;
            }

            if (!confirmedLayout.TryGetFloor(position, out _))
            {
                return SurfaceSessionResult.Failure(SurfaceSessionFailure.UnknownTarget);
            }

            activeState = new PreviewState(
                confirmedLayout.CaptureSnapshot(),
                SurfaceEditScope.SingleGridFloor,
                null,
                SurfaceStyleKind.Floor,
                position);
            return SurfaceSessionResult.Success();
        }

        public SurfaceSessionResult SelectStyle(string styleId)
        {
            if (activeState == null)
            {
                return SurfaceSessionResult.Failure(SurfaceSessionFailure.NoActivePreview);
            }

            if (styleId == null || !stylesById.TryGetValue(styleId, out var style))
            {
                return SurfaceSessionResult.Failure(SurfaceSessionFailure.UnknownStyle);
            }

            if (!StyleMatchesPreview(style, activeState))
            {
                return SurfaceSessionResult.Failure(SurfaceSessionFailure.WrongStyleKind);
            }

            PushUndo(activeState);
            activeState.ArmedStyleId = style.Id;
            ApplySelectedStyle(activeState, style);
            return SurfaceSessionResult.Success();
        }

        public SurfaceSessionResult SelectFloorGrid(GridPosition position)
        {
            if (activeState == null)
            {
                return SurfaceSessionResult.Failure(SurfaceSessionFailure.NoActivePreview);
            }

            if (activeState.Scope != SurfaceEditScope.SingleGridFloor)
            {
                return SurfaceSessionResult.Failure(
                    SurfaceSessionFailure.ActivePreviewMustFinish);
            }

            if (!activeState.ProposedLayout.TryGetFloor(position, out _))
            {
                return SurfaceSessionResult.Failure(SurfaceSessionFailure.UnknownTarget);
            }

            if (activeState.ArmedStyleId == null)
            {
                activeState.SelectedFloorPosition = position;
                return SurfaceSessionResult.Success();
            }

            PushUndo(activeState);
            activeState.SelectedFloorPosition = position;
            activeState.ProposedLayout.ReplaceFloor(new FloorTileAppearance(
                position,
                activeState.ArmedStyleId,
                activeState.ArmedRotation));
            return SurfaceSessionResult.Success();
        }

        public SurfaceSessionResult RotateFloor()
        {
            if (activeState == null)
            {
                return SurfaceSessionResult.Failure(SurfaceSessionFailure.NoActivePreview);
            }

            if (activeState.Scope == SurfaceEditScope.Wall)
            {
                return SurfaceSessionResult.Failure(SurfaceSessionFailure.WrongStyleKind);
            }

            if (activeState.Scope == SurfaceEditScope.WholeRoomFloor &&
                activeState.ArmedStyleId == null)
            {
                return SurfaceSessionResult.Success();
            }

            PushUndo(activeState);
            activeState.ArmedRotation = NextRotation(activeState.ArmedRotation);

            if (activeState.Scope == SurfaceEditScope.WholeRoomFloor)
            {
                activeState.ProposedLayout.ReplaceAllFloors(
                    activeState.ArmedStyleId,
                    activeState.ArmedRotation);
            }
            else
            {
                var position = activeState.SelectedFloorPosition.Value;
                if (activeState.ArmedStyleId == null)
                {
                    activeState.ProposedLayout.TryGetFloor(position, out var current);
                    activeState.ArmedStyleId = current.StyleId;
                }

                activeState.ProposedLayout.ReplaceFloor(new FloorTileAppearance(
                    position,
                    activeState.ArmedStyleId,
                    activeState.ArmedRotation));
            }

            return SurfaceSessionResult.Success();
        }

        public SurfaceSessionResult ApplyAll()
        {
            if (activeState == null)
            {
                return SurfaceSessionResult.Failure(SurfaceSessionFailure.NoActivePreview);
            }

            if (activeState.Scope == SurfaceEditScope.Wall)
            {
                return SurfaceSessionResult.Failure(SurfaceSessionFailure.WrongStyleKind);
            }

            if (activeState.ArmedStyleId == null)
            {
                return SurfaceSessionResult.Success();
            }

            PushUndo(activeState);
            var style = stylesById[activeState.ArmedStyleId];
            activeState.ProposedLayout.ReplaceAllFloors(
                style.Id,
                activeState.ArmedRotation);

            return SurfaceSessionResult.Success();
        }

        public bool UndoLast()
        {
            if (activeState == null || activeState.UndoStates.Count == 0)
            {
                return false;
            }

            var previous = activeState.UndoStates.Pop();
            activeState.ProposedLayout = RoomSurfaceLayout.FromSnapshot(previous.Snapshot);
            activeState.SelectedFloorPosition = previous.SelectedFloorPosition;
            activeState.ArmedStyleId = previous.ArmedStyleId;
            activeState.ArmedRotation = previous.ArmedRotation;
            return true;
        }

        public SurfaceSessionResult Confirm()
        {
            if (activeState == null)
            {
                return SurfaceSessionResult.Failure(SurfaceSessionFailure.NoActivePreview);
            }

            confirmedLayout.ApplySnapshot(activeState.ProposedLayout.CaptureSnapshot());
            activeState = null;
            return SurfaceSessionResult.Success();
        }

        public void Cancel()
        {
            activeState = null;
        }

        private SurfacePreviewTransaction CreateActiveView()
        {
            if (activeState == null)
            {
                return null;
            }

            return new SurfacePreviewTransaction(
                activeState.Scope,
                activeState.TargetWallSurfaceId,
                activeState.SelectedFloorPosition,
                activeState.ArmedStyleId,
                activeState.ArmedRotation,
                activeState.UndoStates.Count > 0,
                HasActiveChanges(),
                GetUsingStyleId(activeState),
                activeState.ArmedStyleId,
                GetUsingWallBaseStyleId(activeState),
                GetPreviewWallBaseStyleId(activeState),
                GetUsingWallWainscotingStyleId(activeState),
                GetPreviewWallWainscotingStyleId(activeState),
                activeState.BaselineSnapshot,
                activeState.ProposedLayout.CaptureSnapshot());
        }

        private string GetUsingStyleId(PreviewState state)
        {
            if (state.Scope == SurfaceEditScope.SingleGridFloor &&
                state.SelectedFloorPosition.HasValue &&
                confirmedLayout.TryGetFloor(
                    state.SelectedFloorPosition.Value,
                    out var floor))
            {
                return floor.StyleId;
            }

            if (state.Scope != SurfaceEditScope.Wall ||
                !confirmedLayout.TryGetWall(state.TargetWallSurfaceId, out var wall))
            {
                return null;
            }

            return state.ArmedStyleId != null &&
                stylesById.TryGetValue(state.ArmedStyleId, out var armed) &&
                armed.Kind == SurfaceStyleKind.Wainscoting
                ? wall.WainscotingStyleId ?? wainscotingNoneStyleId
                : wall.BaseStyleId;
        }

        private string GetUsingWallBaseStyleId(PreviewState state)
        {
            return TryGetWallPair(state, out var confirmed, out _)
                ? confirmed.BaseStyleId
                : null;
        }

        private string GetPreviewWallBaseStyleId(PreviewState state)
        {
            if (!TryGetWallPair(state, out var confirmed, out var proposed) ||
                string.Equals(confirmed.BaseStyleId, proposed.BaseStyleId, StringComparison.Ordinal))
            {
                return null;
            }

            return proposed.BaseStyleId;
        }

        private string GetUsingWallWainscotingStyleId(PreviewState state)
        {
            return TryGetWallPair(state, out var confirmed, out _)
                ? confirmed.WainscotingStyleId ?? wainscotingNoneStyleId
                : null;
        }

        private string GetPreviewWallWainscotingStyleId(PreviewState state)
        {
            if (!TryGetWallPair(state, out var confirmed, out var proposed) ||
                string.Equals(
                    confirmed.WainscotingStyleId,
                    proposed.WainscotingStyleId,
                    StringComparison.Ordinal))
            {
                return null;
            }

            return proposed.WainscotingStyleId ?? wainscotingNoneStyleId;
        }

        private bool TryGetWallPair(
            PreviewState state,
            out WallAppearance confirmed,
            out WallAppearance proposed)
        {
            confirmed = default;
            proposed = default;
            return state.Scope == SurfaceEditScope.Wall &&
                confirmedLayout.TryGetWall(state.TargetWallSurfaceId, out confirmed) &&
                state.ProposedLayout.TryGetWall(state.TargetWallSurfaceId, out proposed);
        }

        private bool HasActiveChanges()
        {
            if (activeState == null)
            {
                return false;
            }

            if (activeState.Scope == SurfaceEditScope.Wall)
            {
                return TryGetWallPair(activeState, out var confirmed, out var proposed) &&
                    (!string.Equals(confirmed.BaseStyleId, proposed.BaseStyleId,
                        StringComparison.Ordinal) ||
                    !string.Equals(confirmed.WainscotingStyleId,
                        proposed.WainscotingStyleId,
                        StringComparison.Ordinal));
            }

            foreach (var confirmedFloor in confirmedLayout.FloorTiles)
            {
                if (!activeState.ProposedLayout.TryGetFloor(
                        confirmedFloor.Key,
                        out var proposedFloor) ||
                    !string.Equals(
                        confirmedFloor.Value.StyleId,
                        proposedFloor.StyleId,
                        StringComparison.Ordinal) ||
                    confirmedFloor.Value.Rotation != proposedFloor.Rotation)
                {
                    return true;
                }
            }

            return false;
        }

        private SurfaceSessionResult CheckBeginGate()
        {
            return activeState == null
                ? SurfaceSessionResult.Success()
                : SurfaceSessionResult.Failure(
                    SurfaceSessionFailure.ActivePreviewMustFinish);
        }

        private bool TryGetWall(string surfaceId)
        {
            if (string.IsNullOrEmpty(surfaceId))
            {
                return false;
            }

            try
            {
                return confirmedLayout.TryGetWall(surfaceId, out _);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static void ValidateStyleDefinition(
            SurfaceStyleDefinitionAsset style,
            string paramName)
        {
            try
            {
                WallMountedInstance.ValidateId(style.StyleId, paramName);
            }
            catch (ArgumentException exception)
            {
                throw new ArgumentException(
                    "Surface style ID has an invalid format.",
                    paramName,
                    exception);
            }

            if (!Enum.IsDefined(typeof(SurfaceStyleKind), style.Kind))
            {
                throw new ArgumentException(
                    "Surface style kind must be defined.",
                    paramName);
            }

            if (style.IsNoneOption)
            {
                if (style.Kind != SurfaceStyleKind.Wainscoting ||
                    style.Material != null ||
                    style.Thumbnail == null)
                {
                    throw new ArgumentException(
                        "None must be a Wainscoting style with an icon and no Material.",
                        paramName);
                }

                return;
            }

            if (style.Material == null || style.Thumbnail == null)
            {
                throw new ArgumentException(
                    "Normal Surface styles require Material and Sprite assets.",
                    paramName);
            }
        }

        private static bool StyleMatchesPreview(
            StyleBinding style,
            PreviewState preview)
        {
            if (preview.Scope != SurfaceEditScope.Wall)
            {
                return style.Kind == SurfaceStyleKind.Floor;
            }

            return (IsBaseKind(style.Kind) && !style.IsNone) ||
                style.Kind == SurfaceStyleKind.Wainscoting;
        }

        private static void ApplySelectedStyle(
            PreviewState preview,
            StyleBinding style)
        {
            switch (preview.Scope)
            {
                case SurfaceEditScope.Wall:
                    preview.ProposedLayout.TryGetWall(
                        preview.TargetWallSurfaceId,
                        out var wall);
                    preview.ProposedLayout.ReplaceWall(
                        CreateWallAppearance(wall, style));
                    break;
                case SurfaceEditScope.WholeRoomFloor:
                    preview.ProposedLayout.ReplaceAllFloors(
                        style.Id,
                        preview.ArmedRotation);
                    break;
                case SurfaceEditScope.SingleGridFloor:
                    var position = preview.SelectedFloorPosition.Value;
                    preview.ProposedLayout.ReplaceFloor(new FloorTileAppearance(
                        position,
                        style.Id,
                        preview.ArmedRotation));
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static WallAppearance CreateWallAppearance(
            WallAppearance current,
            StyleBinding style)
        {
            return style.Kind == SurfaceStyleKind.Wainscoting
                ? new WallAppearance(
                    current.SurfaceId,
                    current.BaseStyleId,
                    style.IsNone ? null : style.Id)
                : new WallAppearance(
                    current.SurfaceId,
                    style.Id,
                    current.WainscotingStyleId);
        }

        private static void PushUndo(PreviewState state)
        {
            state.UndoStates.Push(new UndoState(
                state.ProposedLayout.CaptureSnapshot(),
                state.SelectedFloorPosition,
                state.ArmedStyleId,
                state.ArmedRotation));
        }

        private static bool IsBaseKind(SurfaceStyleKind kind)
        {
            return kind == SurfaceStyleKind.Paint || kind == SurfaceStyleKind.Wallpaper;
        }

        private static SurfaceRotation NextRotation(SurfaceRotation rotation)
        {
            switch (rotation)
            {
                case SurfaceRotation.Degrees0:
                    return SurfaceRotation.Degrees90;
                case SurfaceRotation.Degrees90:
                    return SurfaceRotation.Degrees180;
                case SurfaceRotation.Degrees180:
                    return SurfaceRotation.Degrees270;
                case SurfaceRotation.Degrees270:
                    return SurfaceRotation.Degrees0;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(rotation),
                        rotation,
                        "Rotation must be a known value.");
            }
        }

        private sealed class PreviewState
        {
            public RoomSurfaceLayout ProposedLayout;
            public readonly RoomSurfaceSnapshot BaselineSnapshot;
            public readonly SurfaceEditScope Scope;
            public readonly string TargetWallSurfaceId;
            public readonly SurfaceStyleKind WallLayer;
            public GridPosition? SelectedFloorPosition;
            public string ArmedStyleId;
            public SurfaceRotation ArmedRotation;
            public readonly Stack<UndoState> UndoStates = new Stack<UndoState>();

            public PreviewState(
                RoomSurfaceSnapshot baselineSnapshot,
                SurfaceEditScope scope,
                string targetWallSurfaceId,
                SurfaceStyleKind wallLayer,
                GridPosition? selectedFloorPosition)
            {
                BaselineSnapshot = baselineSnapshot ?? throw new ArgumentNullException(nameof(baselineSnapshot));
                ProposedLayout = RoomSurfaceLayout.FromSnapshot(BaselineSnapshot);
                Scope = scope;
                TargetWallSurfaceId = targetWallSurfaceId;
                WallLayer = wallLayer;
                SelectedFloorPosition = selectedFloorPosition;
                ArmedRotation = SurfaceRotation.Degrees0;
            }
        }

        private readonly struct UndoState
        {
            public RoomSurfaceSnapshot Snapshot { get; }
            public GridPosition? SelectedFloorPosition { get; }
            public string ArmedStyleId { get; }
            public SurfaceRotation ArmedRotation { get; }

            public UndoState(
                RoomSurfaceSnapshot snapshot,
                GridPosition? selectedFloorPosition,
                string armedStyleId,
                SurfaceRotation armedRotation)
            {
                Snapshot = snapshot;
                SelectedFloorPosition = selectedFloorPosition;
                ArmedStyleId = armedStyleId;
                ArmedRotation = armedRotation;
            }
        }

        private readonly struct StyleBinding
        {
            public string Id { get; }
            public SurfaceStyleKind Kind { get; }
            public bool IsNone { get; }

            public StyleBinding(string id, SurfaceStyleKind kind, bool isNone)
            {
                Id = id;
                Kind = kind;
                IsNone = isNone;
            }
        }
    }
}
