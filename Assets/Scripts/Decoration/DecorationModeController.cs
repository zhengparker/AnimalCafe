using System;
using System.Collections.Generic;
using System.Linq;
using AnimalCafe.Camera;
using AnimalCafe.Content;
using AnimalCafe.Core.Time;
using AnimalCafe.Decoration.Input;
using AnimalCafe.Interaction;
using AnimalCafe.Layout;
using AnimalCafe.UI;
using AnimalCafe.UI.Decoration;
using AnimalCafe.UI.Foundation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AnimalCafe.Decoration
{
    /// <summary>
    /// Coordinates the one-scene Decoration Mode transaction and its mobile input.
    /// </summary>
    public sealed class DecorationModeController : MonoBehaviour,
        IDecorationTouchHitClassifier
    {
        private const string ClosedHudLabel = "Decoration";
        private const string OpenHudLabel = "Done";

        [Header("Runtime data")]
        [SerializeField] private CafeLayoutRuntime layoutRuntime;
        [SerializeField] private FurnitureContentCatalog contentCatalog;
        [SerializeField] private DecorationCatalogueAsset catalogueAsset;
        [SerializeField] private SurfaceStyleCatalogueAsset floorStyleCatalogue;
        [SerializeField] private SurfaceStyleCatalogueAsset wallpaperStyleCatalogue;
        [SerializeField] private SurfaceStyleCatalogueAsset paintStyleCatalogue;
        [SerializeField] private SurfaceStyleCatalogueAsset wainscotingStyleCatalogue;
        [SerializeField] private WallMountedCatalogueAsset wallDecorCatalogue;
        [SerializeField] private WallMountedCatalogueAsset windowCatalogue;
        [SerializeField] private WallSurfaceAuthoring[] phase7WallAuthoring =
            Array.Empty<WallSurfaceAuthoring>();
        [SerializeField] private WallMountedSeedAuthoring[] phase7MountedSeeds =
            Array.Empty<WallMountedSeedAuthoring>();

        [Header("Camera and world")]
        [SerializeField] private UnityEngine.Camera targetCamera;
        [SerializeField] private CameraSettings cameraSettings;
        [SerializeField] private CafeCameraController cameraController;
        [SerializeField] private SceneInteractionController sceneInteraction;
        [SerializeField] private Collider floorCollider;
        [SerializeField] private Transform gridRoot;
        [SerializeField] private Transform furnitureRepresentationRoot;
        [SerializeField] private Transform furniturePreviewRoot;
        [SerializeField] private Transform gridVisualRoot;
        [SerializeField] private Material gridMaterialTemplate;
        [SerializeField] private AnimalCafeUiTheme uiTheme;
        [SerializeField] private FurnitureSceneRegistry sceneRegistry;
        [SerializeField] private FurniturePreviewView previewView;
        [SerializeField] private GridHighlightView gridView;
        [SerializeField] private DecorationCameraDriver cameraDriver;

        [Header("Decoration UI")]
        [SerializeField] private DecorationCatalogueView catalogueView;
        [SerializeField] private DecorationActionBarView actionBarView;
        [SerializeField] private DecorationStoreModalView storeModalView;
        [SerializeField] private DecorationModeTabsView modeTabsView;
        [SerializeField] private DecorationFloorRangeView floorRangeView;
        [SerializeField] private DecorationExitModalView exitModalView;
        [SerializeField] private Button decorationModeButton;
        [SerializeField] private TMP_Text decorationModeButtonLabel;
        [SerializeField] private TimeControlPanel timeControlPanel;

        [Header("Shared services")]
        [SerializeField] private MonoBehaviour gameTimeServiceBehaviour;
        [SerializeField] private MonoBehaviour touchSourceBehaviour;
        [SerializeField] private MouseDecorationInputSource mouseSourceBehaviour;

        [Header("Provisional mobile feel")]
        [SerializeField, Min(0f)] private float furnitureDragOffsetPixels;
        [SerializeField, Min(0f)] private float furnitureHoverHeight = 0.35f;

        // Tests inject pure interfaces here; production uses the serialized behaviours above.
        private IGameTimeService gameTimeServiceOverride;
        private IDecorationTouchSource touchSourceOverride;
        private IMouseDecorationInputSource mouseSourceOverride;
        private DecorationGridSpace gridSpace;

        private readonly UiView modeView = new UiView(
            "decoration.mode",
            UiViewKind.MainPanel,
            UiPausePolicy.PauseGame,
            UiOutsideDismissPolicy.NotDismissible);
        private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();
        private readonly Dictionary<string, float> formalHitDistances =
            new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Vector3[] actionPresentationCorners = new Vector3[4];
        private readonly Vector3[] previewHitCorners = new Vector3[8];

        private DecorationSession session;
        private SurfaceDecorationSession surfaceSession;
        private WallMountedDecorationSession wallMountedSession;
        private readonly Dictionary<string, SurfaceStyleDefinitionAsset> phase7StylesById =
            new Dictionary<string, SurfaceStyleDefinitionAsset>(StringComparer.Ordinal);
        private readonly Dictionary<string, WallMountedDefinitionAsset> phase7WallDefinitionsById =
            new Dictionary<string, WallMountedDefinitionAsset>(StringComparer.Ordinal);
        private WallMountedLayout phase7WallMountedLayout;
        private RoomSurfaceLayout phase7RoomSurfaceLayout;
        private string selectedWallTarget;
        private string wallMountedDisplaySurfaceId;
        private WallSlotPosition wallMountedDisplayPosition;
        [SerializeField] private WallMountedPreviewView wallMountedProjectionView;
        [SerializeField] private WallMountedSceneRegistry wallMountedSceneRegistry;
        [SerializeField] private WallSurfaceRegistry wallSurfaceRegistry;
        [SerializeField] private FloorSurfaceGridView floorSurfaceGridView;
        [SerializeField] private Material projectionValidMaterial;
        [SerializeField] private Material projectionInvalidMaterial;
        [SerializeField] private WallOcclusionFadeView wallOcclusionFadeView;
        private UiPauseCoordinator pauseCoordinator;
        private UiPointerBoundary pointerBoundary;
        private UiNavigationCoordinator navigationCoordinator;
        private UiTransitionRunner transitionRunner;
        private DecorationTouchRouter touchRouter;
        private IDecorationTouchSource touchSource;
        private IMouseDecorationInputSource mouseSource;
        private PointerDeviceFamily activePointerDeviceFamily;
        private IUiPauseHandle pauseHandle;
        private IDisposable sceneInputSuppressionHandle;
        private UiViewHandle modeViewHandle;
        private string hiddenSourceInstanceId;
        private string hiddenWallMountedSourceInstanceId;
        private string previewDefinitionId;
        private bool furnitureDragScreenPositionInitialized;
        private Vector2 lastFurnitureDragScreenPosition;
        private bool isOpen;
        private bool isEntering;
        private bool isCleaningUp;
        private bool cleanupRequired;
        private bool cameraEnabledBeforeEnter;
        private bool cameraStateCaptured;
        private bool viewEventsSubscribed;
        private bool hudListenerInstalled;
        private bool runtimeBootstrapComplete;
        private bool viewsConfigured;
        private bool catalogueBound;
        private bool phase7CatalogueBound;
        private IReadOnlyList<DecorationCategoryModel> phase7CatalogueCategories;
        private RectTransform nonSurfaceActionHost;
        private DecorationModeKind activeMode = DecorationModeKind.Furniture;
        private SurfaceEditScope floorRange = SurfaceEditScope.WholeRoomFloor;
        private GridPosition? selectedFloorTarget;
        private float sanitizedFurnitureDragOffsetPixels;
        private float sanitizedFurnitureHoverHeight;
        private EventSystem uiPointerEventSystem;
        private PointerEventData uiPointerEventData;

        public bool IsOpen => isOpen;
        public DecorationModeKind ActiveMode => activeMode;
        public SurfaceEditScope FloorRange => floorRange;
        public GridPosition? SelectedFloorTarget => selectedFloorTarget;
        public SurfacePreviewTransaction ActiveSurfacePreview => surfaceSession?.ActivePreview;
        public WallMountedPlacementPreview ActiveWallMountedPreview => wallMountedSession?.ActivePreview;
        public event Action ExitDiscardConfirmationRequested;

        public void ConfigurePhase7Runtime(
            RoomSurfaceLayout roomSurfaceLayout,
            IEnumerable<SurfaceStyleDefinitionAsset> surfaceStyles,
            WallMountedLayout wallMountedLayout,
            IEnumerable<WallMountedDefinitionAsset> wallMountedDefinitions)
        {
            var styles = surfaceStyles?.ToArray()
                ?? throw new ArgumentNullException(nameof(surfaceStyles));
            var definitions = wallMountedDefinitions?.ToArray()
                ?? throw new ArgumentNullException(nameof(wallMountedDefinitions));
            surfaceSession = new SurfaceDecorationSession(
                roomSurfaceLayout ?? throw new ArgumentNullException(nameof(roomSurfaceLayout)),
                styles);
            wallMountedSession = new WallMountedDecorationSession(
                wallMountedLayout ?? throw new ArgumentNullException(nameof(wallMountedLayout)),
                definitions);
            phase7WallMountedLayout = wallMountedLayout;
            phase7RoomSurfaceLayout = roomSurfaceLayout;
            phase7StylesById.Clear();
            foreach (var style in styles) phase7StylesById.Add(style.StyleId, style);
            phase7WallDefinitionsById.Clear();
            foreach (var definition in definitions)
                phase7WallDefinitionsById.Add(definition.DefinitionId, definition);
        }

        public bool InitializePhase7RuntimeIfConfigured()
        {
            if (surfaceSession != null && wallMountedSession != null)
            {
                return true;
            }

            if (layoutRuntime == null
                || phase7WallAuthoring == null
                || phase7WallAuthoring.Length != 2
                || phase7WallAuthoring.Any(item => item == null)
                || floorStyleCatalogue == null
                || wallpaperStyleCatalogue == null
                || paintStyleCatalogue == null
                || wainscotingStyleCatalogue == null
                || wallDecorCatalogue == null
                || windowCatalogue == null)
            {
                return false;
            }

            var initialFloor = floorStyleCatalogue.Entries.FirstOrDefault(item => item != null);
            var initialWall = paintStyleCatalogue.Entries.FirstOrDefault(item =>
                item != null && !item.IsNoneOption);
            if (initialFloor == null || initialWall == null)
            {
                return false;
            }

            layoutRuntime.InitializePhase7Layouts(
                "room.main",
                phase7WallAuthoring,
                initialWall.StyleId,
                initialFloor.StyleId);
            var styles = floorStyleCatalogue.Entries
                .Concat(wallpaperStyleCatalogue.Entries)
                .Concat(paintStyleCatalogue.Entries)
                .Concat(wainscotingStyleCatalogue.Entries);
            var definitions = wallDecorCatalogue.Entries.Concat(windowCatalogue.Entries);
            ConfigurePhase7Runtime(
                layoutRuntime.RoomSurfaceLayout,
                styles,
                layoutRuntime.WallMountedLayout,
                definitions);
            SeedAuthoredWallMountedItems();
            return true;
        }

        public void ConfigurePhase7Ui(
            DecorationModeTabsView tabs,
            DecorationFloorRangeView rangeView,
            DecorationExitModalView exitModal)
        {
            UnsubscribePhase7Ui();
            modeTabsView = tabs;
            floorRangeView = rangeView;
            exitModalView = exitModal;
            pointerBoundary ??= new UiPointerBoundary();
            if (exitModalView != null)
            {
                exitModalView.Configure(pointerBoundary);
                exitModalView.gameObject.SetActive(false);
            }
            SubscribePhase7Ui();
            SetPhase7ChromeVisible(isOpen);
            modeTabsView?.SetActive(activeMode);
            floorRangeView?.SetSelected(floorRange);
        }

        public void ConfigurePhase7Scene(
            IEnumerable<WallSurfaceAuthoring> wallAuthoring,
            WallMountedPreviewView projectionView,
            WallSurfaceRegistry surfaceRegistry = null,
            FloorSurfaceGridView floorGridView = null,
            WallMountedSceneRegistry mountedSceneRegistry = null)
        {
            phase7WallAuthoring = wallAuthoring?.ToArray()
                ?? throw new ArgumentNullException(nameof(wallAuthoring));
            if (phase7WallAuthoring.Any(item => item == null))
            {
                throw new ArgumentException(
                    "Wall authoring cannot contain null entries.",
                    nameof(wallAuthoring));
            }
            wallMountedProjectionView = projectionView
                ?? throw new ArgumentNullException(nameof(projectionView));
            wallSurfaceRegistry = surfaceRegistry;
            floorSurfaceGridView = floorGridView;
            wallMountedSceneRegistry = mountedSceneRegistry ?? wallMountedSceneRegistry;
            if (phase7RoomSurfaceLayout != null)
            {
                wallSurfaceRegistry?.RenderConfirmed(phase7RoomSurfaceLayout);
                floorSurfaceGridView?.RenderConfirmed(phase7RoomSurfaceLayout);
            }
        }

        public void ConfigurePhase7Catalogue(
            DecorationCatalogueView view,
            IReadOnlyList<DecorationCategoryModel> categories)
        {
            catalogueView = view ?? throw new ArgumentNullException(nameof(view));
            phase7CatalogueCategories = categories
                ?? throw new ArgumentNullException(nameof(categories));
            BindCatalogueForActiveMode();
            phase7CatalogueBound = true;
            catalogueBound = true;
        }

        public bool TryHandleSceneTap(DecorationTouchHit hit)
        {
            if (!AcceptsSceneHit(activeMode, hit.Kind))
            {
                return false;
            }

            switch (activeMode)
            {
                case DecorationModeKind.Floor:
                    if (!hit.FloorPosition.HasValue || surfaceSession == null)
                    {
                        return false;
                    }
                    if (surfaceSession.ActivePreview == null)
                    {
                        if (floorRange != SurfaceEditScope.SingleGridFloor)
                        {
                            return false;
                        }
                        var began = surfaceSession.BeginSingleGridFloor(hit.FloorPosition.Value);
                        if (!began.Succeeded)
                        {
                            return false;
                        }
                    }
                    else if (surfaceSession.ActivePreview.Scope != SurfaceEditScope.SingleGridFloor)
                    {
                        return false;
                    }
                    selectedFloorTarget = hit.FloorPosition.Value;
                    var floorSelected = surfaceSession.SelectFloorGrid(hit.FloorPosition.Value).Succeeded;
                    if (floorSelected)
                    {
                        RefreshSurfacePreviewViews();
                        ShowPhase7ActionForActivePreview();
                    }
                    return floorSelected;

                case DecorationModeKind.Wall:
                    if (surfaceSession == null || string.IsNullOrEmpty(hit.SurfaceId))
                    {
                        return false;
                    }

                    var beginWall = surfaceSession.BeginWall(hit.SurfaceId);
                    if (!beginWall.Succeeded)
                    {
                        return false;
                    }

                    selectedWallTarget = hit.SurfaceId;
                    wallSurfaceRegistry?.SetSelectedSurface(hit.SurfaceId);
                    RefreshSurfaceCatalogueState();
                    UpdateWallOcclusionFade(hit.SurfaceId);
                    ShowPhase7ActionForActivePreview();
                    return true;

                case DecorationModeKind.WallDecor:
                    if (hit.Kind != DecorationTouchHitKind.WallMounted
                        || string.IsNullOrEmpty(hit.TargetId)
                        || wallMountedSession?.ActivePreview != null)
                    {
                        return false;
                    }
                    var existing = wallMountedSession.BeginExisting(hit.TargetId);
                    if (existing.Succeeded)
                    {
                        HideWallMountedSource(hit.TargetId);
                        UpdateWallMountedProjection();
                        ShowPhase7ActionForActivePreview();
                    }
                    return existing.Succeeded;

                default:
                    return false;
            }
        }

        public bool TryBeginWallPreview(
            string surfaceId,
            SurfaceStyleKind layer,
            string styleId)
        {
            if (activeMode != DecorationModeKind.Wall
                || surfaceSession == null)
            {
                return false;
            }

            if (surfaceSession.ActivePreview == null)
            {
                var begin = surfaceSession.BeginWall(surfaceId);
                if (!begin.Succeeded)
                {
                    return false;
                }
            }
            else if (!string.Equals(
                         surfaceSession.ActivePreview.TargetWallSurfaceId,
                         surfaceId,
                         StringComparison.Ordinal))
            {
                return false;
            }

            var select = surfaceSession.SelectStyle(styleId);
            if (select.Succeeded)
            {
                RefreshSurfacePreviewViews();
                ShowPhase7ActionForActivePreview();
                return true;
            }

            return false;
        }

        public bool TrySelectCatalogueItem(DecorationCatalogueItemModel item)
        {
            if (item == null)
            {
                return false;
            }

            if (activeMode == DecorationModeKind.Furniture
                && item.Kind == DecorationCatalogueItemKind.Furniture
                && item.FurnitureDefinition != null)
            {
                var before = session?.State;
                HandleCatalogueSelected(item.FurnitureDefinition);
                return session != null
                    && session.State == DecorationSessionState.PreviewingNewFurniture
                    && session.State != before;
            }

            if (activeMode == DecorationModeKind.Floor
                && item.Kind == DecorationCatalogueItemKind.Floor
                && phase7StylesById.ContainsKey(item.ItemId))
            {
                if (surfaceSession?.ActivePreview == null)
                {
                    var begin = floorRange == SurfaceEditScope.WholeRoomFloor
                        ? surfaceSession.BeginWholeRoomFloor()
                        : selectedFloorTarget.HasValue
                            ? surfaceSession.BeginSingleGridFloor(selectedFloorTarget.Value)
                            : default;
                    if (!begin.Succeeded)
                    {
                        return false;
                    }
                }
                var selected = surfaceSession.SelectStyle(item.ItemId).Succeeded;
                if (selected)
                {
                    RefreshSurfacePreviewViews();
                    ShowPhase7ActionForActivePreview();
                }
                return selected;
            }

            if (activeMode == DecorationModeKind.Wall
                && item.Kind == DecorationCatalogueItemKind.WallSurface
                && selectedWallTarget != null
                && phase7StylesById.TryGetValue(item.ItemId, out var wallStyle))
            {
                return TryBeginWallPreview(
                    selectedWallTarget,
                    SurfaceStyleKind.Paint,
                    wallStyle.StyleId);
            }

            if (activeMode == DecorationModeKind.WallDecor
                && item.Kind == DecorationCatalogueItemKind.WallMounted
                && phase7WallDefinitionsById.ContainsKey(item.ItemId)
                && phase7WallMountedLayout != null)
            {
                return TryFindVisibleWallMountedStart(
                        item.ItemId,
                        out var preferredSurfaceId,
                        out var preferredPosition)
                    && TryBeginWallMountedPreview(
                        item.ItemId,
                        preferredSurfaceId,
                        preferredPosition);
            }

            return false;
        }

        public void CancelActivePhase7Preview()
        {
            surfaceSession?.Cancel();
            wallMountedSession?.CancelPreview();
            RestoreHiddenWallMountedSource();
            wallMountedProjectionView?.ClearPreview();
            wallMountedDisplaySurfaceId = null;
            wallMountedDisplayPosition = default;
            wallSurfaceRegistry?.ClearPreview();
            floorSurfaceGridView?.ClearPreview();
            floorSurfaceGridView?.ClearSelectionFeedback();
            wallSurfaceRegistry?.ClearSelection();
            selectedWallTarget = null;
            RefreshSurfaceCatalogueState();
            wallOcclusionFadeView?.RestoreAllFades();
            if (isOpen && activeMode == DecorationModeKind.Floor)
            {
                ApplyFloorFurnitureFade();
            }
        }

        public bool TryBeginWallMountedPreview(
            string definitionId,
            string preferredSurfaceId,
            WallSlotPosition preferredPosition)
        {
            if (activeMode != DecorationModeKind.WallDecor
                || wallMountedSession == null
                || HasAnyActivePreview())
            {
                return false;
            }

            try
            {
                wallMountedSession.BeginNew(
                    definitionId,
                    preferredSurfaceId,
                    preferredPosition);
                var began = wallMountedSession.ActivePreview != null;
                if (began)
                {
                    wallMountedDisplaySurfaceId = wallMountedSession.ActivePreview.SurfaceId;
                    wallMountedDisplayPosition = wallMountedSession.ActivePreview.Position;
                    UpdateWallMountedProjection();
                    ShowPhase7ActionForActivePreview();
                }
                return began;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        public bool TryHandleSceneDrag(DecorationTouchHit currentHit)
        {
            if (activeMode != DecorationModeKind.WallDecor
                || wallMountedSession?.ActivePreview == null)
            {
                return false;
            }

            var surfaceId = currentHit.Kind == DecorationTouchHitKind.WallSlot
                ? currentHit.SurfaceId
                : null;
            var position = currentHit.WallSlotPosition ?? default;
            var result = wallMountedSession.MovePreview(surfaceId, position);
            if (!string.IsNullOrEmpty(surfaceId))
            {
                wallMountedDisplaySurfaceId = surfaceId;
                wallMountedDisplayPosition = position;
            }
            UpdateWallMountedProjection();
            ShowPhase7ActionForActivePreview();
            return result.Succeeded;
        }

        public bool TryConfirmPhase7Preview()
        {
            if (activeMode == DecorationModeKind.WallDecor
                && wallMountedSession?.ActivePreview != null)
            {
                var preview = wallMountedSession.ActivePreview;
                var beforeIds = phase7WallMountedLayout.CaptureSnapshot().Instances
                    .Select(item => item.InstanceId).ToArray();
                var result = wallMountedSession.ConfirmPreview();
                if (result.Succeeded)
                {
                    wallMountedProjectionView?.ClearPreview();
                    wallMountedDisplaySurfaceId = null;
                    wallMountedDisplayPosition = default;
                    wallOcclusionFadeView?.RestoreAllFades();
                    var instanceId = preview.InstanceId;
                    if (instanceId == null)
                    {
                        instanceId = phase7WallMountedLayout.CaptureSnapshot().Instances
                            .Select(item => item.InstanceId).Single(id => !beforeIds.Contains(id));
                    }
                    SynchronizeWallMountedRepresentation(instanceId);
                }
                return result.Succeeded;
            }

            if ((activeMode == DecorationModeKind.Floor
                    || activeMode == DecorationModeKind.Wall)
                && surfaceSession?.ActivePreview != null)
            {
                var confirmedPreview = surfaceSession.ActivePreview;
                var confirmedStyleId = confirmedPreview.PreviewStyleId;
                var result = surfaceSession.Confirm();
                if (result.Succeeded && phase7RoomSurfaceLayout != null)
                {
                    wallOcclusionFadeView?.RestoreAllFades();
                    wallSurfaceRegistry?.RenderConfirmed(phase7RoomSurfaceLayout);
                    floorSurfaceGridView?.RenderConfirmed(phase7RoomSurfaceLayout);
                    floorSurfaceGridView?.ClearSelectionFeedback();
                    if (confirmedPreview.Scope == SurfaceEditScope.Wall)
                    {
                        selectedWallTarget = confirmedPreview.TargetWallSurfaceId;
                        catalogueView?.SetSurfaceStates(
                            GetConfirmedWallStyleIds(confirmedPreview.TargetWallSurfaceId), null);
                    }
                    else
                    {
                        catalogueView?.SetSurfaceState(
                            confirmedPreview.Scope == SurfaceEditScope.WholeRoomFloor ? null : confirmedStyleId,
                            null);
                    }

                    if (activeMode == DecorationModeKind.Floor)
                    {
                        ApplyFloorFurnitureFade();
                    }
                }
                return result.Succeeded;
            }

            return false;
        }

        private void SeedAuthoredWallMountedItems()
        {
            if (phase7MountedSeeds == null || phase7WallMountedLayout == null) return;
            foreach (var seed in phase7MountedSeeds.Where(item => item != null))
            {
                if (!phase7WallMountedLayout.TryGetInstance(seed.InstanceId, out _))
                {
                    var result = phase7WallMountedLayout.Place(new WallMountedInstance(
                        seed.InstanceId, seed.DefinitionId, seed.SurfaceId,
                        seed.Position, seed.Footprint));
                    if (!result.Succeeded)
                        throw new InvalidOperationException($"Invalid wall-mounted seed '{seed.InstanceId}': {result.FailureReason}.");
                }
                if (wallMountedSceneRegistry != null &&
                    !wallMountedSceneRegistry.TryGet(seed.InstanceId, out _))
                    wallMountedSceneRegistry.Register(seed.InstanceId, seed.gameObject);
            }
        }

        private void SynchronizeWallMountedRepresentation(string instanceId)
        {
            if (phase7WallMountedLayout == null || wallMountedSceneRegistry == null ||
                !phase7WallMountedLayout.TryGetInstance(instanceId, out var item)) return;
            if (!wallMountedSceneRegistry.TryGet(instanceId, out var representation))
            {
                if (!phase7WallDefinitionsById.TryGetValue(item.DefinitionId, out var definition)) return;
                representation = Instantiate(definition.Prefab);
                representation.name = $"WallMounted_{instanceId}";
                wallMountedSceneRegistry.Register(instanceId, representation);
            }
            var authoring = phase7WallAuthoring.First(x => string.Equals(x.SurfaceId, item.SurfaceId, StringComparison.Ordinal));
            representation.transform.SetParent(authoring.transform, false);
            var localMountPoint = new Vector3(
                (item.Position.Column + item.Footprint.Width * .5f) * authoring.SlotSize - authoring.Columns * authoring.SlotSize * .5f,
                item.Position.Row * authoring.SlotSize, 0f);
            representation.transform.SetPositionAndRotation(
                authoring.GetWallMountedWorldPosition(
                    localMountPoint,
                    WallSurfaceAuthoring.WallMountedPlaneEpsilon),
                authoring.transform.rotation * Quaternion.Euler(0f, 180f, 0f));
            representation.transform.localScale = Vector3.one;
            representation.SetActive(true);
            // A just-instantiated Collider otherwise remains at its prefab-space pose
            // until the next physics step, so the first real click can hit the wall behind it.
            Physics.SyncTransforms();
            if (string.Equals(
                    hiddenWallMountedSourceInstanceId,
                    instanceId,
                    StringComparison.Ordinal))
            {
                hiddenWallMountedSourceInstanceId = null;
            }
        }

        private void HideWallMountedSource(string instanceId)
        {
            RestoreHiddenWallMountedSource();
            if (wallMountedSceneRegistry != null
                && wallMountedSceneRegistry.TryGet(instanceId, out var representation))
            {
                representation.SetActive(false);
                hiddenWallMountedSourceInstanceId = instanceId;
            }
        }

        private void RestoreHiddenWallMountedSource()
        {
            if (string.IsNullOrEmpty(hiddenWallMountedSourceInstanceId))
            {
                return;
            }

            if (wallMountedSceneRegistry != null
                && wallMountedSceneRegistry.TryGet(
                    hiddenWallMountedSourceInstanceId,
                    out var representation))
            {
                representation.SetActive(true);
            }
            hiddenWallMountedSourceInstanceId = null;
        }

        private void RefreshSurfacePreviewViews()
        {
            var preview = surfaceSession?.ActivePreview;
            if (preview == null) return;
            wallSurfaceRegistry?.RenderPreview(preview);
            floorSurfaceGridView?.RenderPreview(preview);
            floorSurfaceGridView?.RenderSelectionFeedback(
                preview.Scope == SurfaceEditScope.SingleGridFloor
                    ? preview.SelectedFloorPosition
                    : null,
                preview.PreviewedFloorPositions);
            if (preview.Scope == SurfaceEditScope.Wall)
                SetWallSurfacePreviewStates(preview);
            else
                catalogueView?.SetSurfaceState(
                    preview.Scope == SurfaceEditScope.WholeRoomFloor ? null : preview.UsingStyleId,
                    preview.PreviewStyleId);
        }

        private void UpdateWallMountedProjection()
        {
            if (wallMountedProjectionView == null
                || wallMountedSession?.ActivePreview == null)
            {
                return;
            }

            var preview = wallMountedSession.ActivePreview;
            var authoring = phase7WallAuthoring.FirstOrDefault(item =>
                string.Equals(item.SurfaceId, preview.SurfaceId, StringComparison.Ordinal));
            var displayPreview = preview;
            if (authoring == null)
            {
                authoring = phase7WallAuthoring.FirstOrDefault(item =>
                    string.Equals(item.SurfaceId, wallMountedDisplaySurfaceId, StringComparison.Ordinal));
                if (authoring == null)
                {
                    wallMountedProjectionView.ClearPreview();
                    return;
                }
                displayPreview = preview.WithPlacement(
                    wallMountedDisplaySurfaceId,
                    wallMountedDisplayPosition,
                    WallPlacementResult.Failure(preview.FailureReason));
            }
            else
            {
                wallMountedDisplaySurfaceId = preview.SurfaceId;
                wallMountedDisplayPosition = preview.Position;
            }

            var placement = preview.IsValid
                ? WallPlacementResult.Success()
                : WallPlacementResult.Failure(preview.FailureReason);
            wallMountedProjectionView.ShowWallPreview(
                displayPreview,
                authoring,
                preview.IsValid,
                PlacementFeedbackMapper.Map(placement),
                phase7WallDefinitionsById.TryGetValue(preview.DefinitionId, out var definition)
                    ? definition.Prefab
                    : null);
            UpdateWallOcclusionFade(preview.SurfaceId);
        }

        private bool TryFindVisibleWallMountedStart(
            string definitionId,
            out string surfaceId,
            out WallSlotPosition position)
        {
            surfaceId = null;
            position = default;
            if (!phase7WallDefinitionsById.TryGetValue(definitionId, out var definition)
                || phase7WallMountedLayout == null)
                return false;

            var footprint = new WallFootprint(
                definition.FootprintWidth,
                definition.FootprintHeight);
            var found = false;
            var bestVisibilityPenalty = int.MaxValue;
            var bestViewportDistance = float.PositiveInfinity;
            foreach (var authoring in (phase7WallAuthoring ?? Array.Empty<WallSurfaceAuthoring>())
                .Where(item => item != null)
                .OrderBy(item => item.SurfaceId, StringComparer.Ordinal))
            {
                for (var column = 0; column <= authoring.Columns - footprint.Width; column++)
                {
                    for (var row = 0; row <= authoring.Rows - footprint.Height; row++)
                    {
                        var candidate = new WallSlotPosition(column, row);
                        if (!phase7WallMountedLayout.ValidatePlacement(
                                definitionId,
                                authoring.SurfaceId,
                                candidate,
                                footprint).Succeeded)
                            continue;

                        var localCenter = new Vector3(
                            -authoring.Columns * authoring.SlotSize * .5f
                                + (column + footprint.Width * .5f) * authoring.SlotSize,
                            (row + footprint.Height * .5f) * authoring.SlotSize,
                            0f);
                        var worldCenter = authoring.GetWallMountedWorldPosition(
                            localCenter,
                            WallSurfaceAuthoring.WallMountedPlaneEpsilon);
                        var viewport = targetCamera != null
                            ? targetCamera.WorldToViewportPoint(worldCenter)
                            : new Vector3(.5f, .6f, 1f);
                        var visible = viewport.z > 0f
                            && viewport.x >= .05f && viewport.x <= .95f
                            && viewport.y >= .05f && viewport.y <= .95f;
                        var visibilityPenalty = visible ? 0 : 1;
                        var viewportDistance = new Vector2(
                            viewport.x - .5f,
                            viewport.y - .6f).sqrMagnitude;
                        if (!found
                            || visibilityPenalty < bestVisibilityPenalty
                            || (visibilityPenalty == bestVisibilityPenalty
                                && viewportDistance < bestViewportDistance))
                        {
                            found = true;
                            bestVisibilityPenalty = visibilityPenalty;
                            bestViewportDistance = viewportDistance;
                            surfaceId = authoring.SurfaceId;
                            position = candidate;
                        }
                    }
                }
            }
            if (!found)
            {
                foreach (var surface in phase7WallMountedLayout.Surfaces
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    for (var column = 0; column <= surface.Value.ColumnCount - footprint.Width; column++)
                    for (var row = 0; row <= surface.Value.RowCount - footprint.Height; row++)
                    {
                        var candidate = new WallSlotPosition(column, row);
                        if (!phase7WallMountedLayout.ValidatePlacement(
                                definitionId,
                                surface.Key,
                                candidate,
                                footprint).Succeeded)
                            continue;
                        surfaceId = surface.Key;
                        position = candidate;
                        return true;
                    }
                }
            }
            return found;
        }

        private void UpdateWallOcclusionFade(string surfaceId)
        {
            if (wallOcclusionFadeView == null)
            {
                return;
            }

            try
            {
                var authoring = phase7WallAuthoring?.FirstOrDefault(item => item != null &&
                    string.Equals(item.SurfaceId, surfaceId, StringComparison.Ordinal));
                var renderer = authoring?.GetComponentInChildren<Renderer>(true);
                if (renderer == null)
                {
                    wallOcclusionFadeView.RestoreAllFades();
                    return;
                }
                wallOcclusionFadeView.SetNonDecorationBlockerRoot(
                    floorCollider != null ? floorCollider.transform : null);
                wallOcclusionFadeView.ConfigureTarget(renderer);
                wallOcclusionFadeView.FadeBlockersForTarget();
            }
            catch
            {
                wallOcclusionFadeView.RestoreAllFades();
                throw;
            }
        }

        private void ApplyFloorFurnitureFade()
        {
            if (activeMode != DecorationModeKind.Floor
                || wallOcclusionFadeView == null
                || sceneRegistry == null
                || layoutRuntime?.Layout == null)
            {
                return;
            }

            var representationRoots = new List<Transform>();
            foreach (var instance in layoutRuntime.Layout.FurnitureInstances)
            {
                if (instance != null
                    && sceneRegistry.TryGet(instance.InstanceId, out var representation)
                    && representation != null
                    && representation.activeInHierarchy)
                {
                    representationRoots.Add(representation.transform);
                }
            }

            wallOcclusionFadeView.FadeRepresentations(representationRoots);
        }

        public static bool AcceptsSceneHit(
            DecorationModeKind mode,
            DecorationTouchHitKind hit)
        {
            return mode switch
            {
                DecorationModeKind.Furniture => hit == DecorationTouchHitKind.Furniture
                    || hit == DecorationTouchHitKind.Scene,
                DecorationModeKind.Floor => hit == DecorationTouchHitKind.FloorGrid,
                DecorationModeKind.Wall => hit == DecorationTouchHitKind.WallSurface,
                DecorationModeKind.WallDecor => hit == DecorationTouchHitKind.WallSlot
                    || hit == DecorationTouchHitKind.WallMounted,
                _ => false
            };
        }

        public bool TryChangeMode(DecorationModeKind mode)
        {
            if (!Enum.IsDefined(typeof(DecorationModeKind), mode) || HasAnyActivePreview())
            {
                return false;
            }

            wallOcclusionFadeView?.RestoreAllFades();
            wallSurfaceRegistry?.ClearSelection();
            selectedWallTarget = null;
            activeMode = mode;
            if (activeMode == DecorationModeKind.Floor)
            {
                ApplyFloorFurnitureFade();
            }
            AttachActionBarForActiveMode();
            modeTabsView?.SetActive(mode);
            if (floorRangeView != null)
                floorRangeView.gameObject.SetActive(isOpen && mode == DecorationModeKind.Floor);
            BindCatalogueForActiveMode();
            catalogueView?.ShowCatalogue();
            catalogueView?.SetSheetState(
                DecorationSheetState.Expanded,
                hasActivePreview: false);
            RefreshTargetSelectionInstruction();
            return true;
        }

        public bool TryRequestExit()
        {
            if (HasAnyActivePreview())
            {
                ExitDiscardConfirmationRequested?.Invoke();
                exitModalView?.Show();
                return false;
            }

            ExitDecorationMode();
            return true;
        }

        public bool TrySelectFloorRange(SurfaceEditScope range)
        {
            if (activeMode != DecorationModeKind.Floor
                || HasAnyActivePreview()
                || (range != SurfaceEditScope.WholeRoomFloor
                    && range != SurfaceEditScope.SingleGridFloor))
            {
                return false;
            }

            floorRange = range;
            selectedFloorTarget = null;
            floorSurfaceGridView?.ClearSelectionFeedback();
            floorRangeView?.SetSelected(range);
            RefreshSurfaceCatalogueState();
            RefreshTargetSelectionInstruction();
            return true;
        }

        public bool TrySelectFloorTarget(GridPosition target)
        {
            if (activeMode != DecorationModeKind.Floor
                || floorRange != SurfaceEditScope.SingleGridFloor
                || HasAnyActivePreview()
                || target.X < 0 || target.X >= 8
                || target.Y < 0 || target.Y >= 8)
            {
                return false;
            }

            selectedFloorTarget = target;
            RefreshSurfaceCatalogueState();
            RefreshTargetSelectionInstruction();
            return true;
        }

        public DecorationSessionState State =>
            session?.State ?? DecorationSessionState.Closed;

        private void Awake()
        {
            TryInitializeStartupRuntime();
        }

        private void OnEnable()
        {
            InstallHudListener();
            SyncHudLabel();
        }

        private void Update()
        {
            if (!isOpen)
            {
                if (pauseCoordinator != null && pauseCoordinator.HasPendingRestore)
                {
                    pauseCoordinator.TryRestorePendingSpeed();
                }

                return;
            }

            if (touchRouter == null)
            {
                return;
            }

            var frame = ReadActivePointerFrame();
            if (activePointerDeviceFamily == PointerDeviceFamily.None)
            {
                ApplyIdleMouseWheelZoom();
                return;
            }

            var ownerBefore = touchRouter.Owner;
            var result = touchRouter.ProcessFrame(
                frame,
                (IDecorationTouchHitClassifier)this);

            if (ownerBefore == DecorationGestureOwner.None
                && result.OriginHit.Kind == DecorationTouchHitKind.Furniture
                && touchRouter.PrimaryTouchId != DecorationTouchRouter.NoTouchId)
            {
                HandleFurnitureBegan(result.OriginHit.FurnitureInstanceId);
            }

            RouteTouchResultForActiveMode(result);
            if (ownerBefore != DecorationGestureOwner.Pinch
                && touchRouter.Owner != DecorationGestureOwner.Pinch)
            {
                UpdateActionPresentation();
            }
            if (touchRouter.Owner == DecorationGestureOwner.None
                && !touchRouter.IsSuppressingUntilAllTouchesUp)
            {
                activePointerDeviceFamily = PointerDeviceFamily.None;
            }
        }

        public void RouteTouchResultForActiveMode(DecorationTouchRoutingResult result)
        {
            if (result.TapReleased
                && result.OriginHit.Kind != DecorationTouchHitKind.None
                && !AcceptsSceneHit(activeMode, result.OriginHit.Kind))
            {
                return;
            }

            if (result.SceneDragRequested
                && activeMode != DecorationModeKind.WallDecor)
            {
                return;
            }

            switch (activeMode)
            {
                case DecorationModeKind.Furniture:
                    HandleFurnitureFrame(result);
                    break;
                case DecorationModeKind.Floor:
                    HandleFloorFrame(result);
                    break;
                case DecorationModeKind.Wall:
                    HandleWallFrame(result);
                    break;
                case DecorationModeKind.WallDecor:
                    HandleWallMountedFrame(result);
                    break;
            }
        }

        private void OnDisable()
        {
            CleanupDecorationMode();
            RemoveHudListener();
            SyncHudLabel();
        }

        private void OnDestroy()
        {
            CleanupDecorationMode();
            UnsubscribePhase7Ui();
            RemoveHudListener();
        }

        public void EnterDecorationMode()
        {
            if (!isActiveAndEnabled || isOpen || isEntering)
            {
                return;
            }

            isEntering = true;
            activeMode = DecorationModeKind.Furniture;
            AttachActionBarForActiveMode();
            floorRange = SurfaceEditScope.WholeRoomFloor;
            SetPhase7ChromeVisible(true);
            modeTabsView?.SetActive(activeMode);
            floorRangeView?.SetSelected(floorRange);
            selectedFloorTarget = null;
            selectedWallTarget = null;
            cleanupRequired = true;
            try
            {
                EnsureRuntimeDependencies();
                mouseSource?.Reset();
                activePointerDeviceFamily = PointerDeviceFamily.None;

                // Pause first so a rejected request leaves normal Scene input untouched.
                pauseHandle = pauseCoordinator.Acquire(modeView);
                timeControlPanel.SetDecorationPauseLock(true);
                sceneInputSuppressionHandle = sceneInteraction.AcquireInputSuppression(this);

                cameraEnabledBeforeEnter = cameraController.enabled;
                cameraStateCaptured = true;
                cameraController.enabled = false;

                if (!viewsConfigured)
                {
                    ConfigureViews();
                    viewsConfigured = true;
                }
                SubscribeViewEvents();
                if (!catalogueBound)
                {
                    catalogueView.Bind(catalogueAsset);
                    catalogueBound = true;
                }
                if (phase7CatalogueBound)
                {
                    BindCatalogueForActiveMode();
                }

                session.Enter();
                modeViewHandle = navigationCoordinator.OpenMainPanel(modeView);
                ClearPreviewPresentation();
                storeModalView.CloseForOwnerShutdown();
                actionBarView.Hide();
                catalogueView.ShowCatalogue();
                if(phase7CatalogueBound)catalogueView.SetSheetState(DecorationSheetState.Expanded,false);
                gridView.ShowGrid(layoutRuntime.Layout.GridSettings);

                isOpen = true;
                SyncHudLabel();
            }
            catch
            {
                CleanupDecorationMode();
                SyncHudLabel();
                throw;
            }
            finally
            {
                isEntering = false;
            }
        }

        public void ExitDecorationMode()
        {
            CleanupDecorationMode();
            SyncHudLabel();
        }

        public void CancelActivePreview()
        {
            if (!isOpen
                || session?.ActivePreview == null
                || (session.State != DecorationSessionState.PreviewingNewFurniture
                    && session.State != DecorationSessionState.EditingExistingFurniture
                    && session.State != DecorationSessionState.ConfirmingStore))
            {
                return;
            }

            cameraDriver.StopEdgeAutoPan();
            storeModalView.CloseForOwnerShutdown();
            session.CancelPreview();
            sceneRegistry.Rebuild(layoutRuntime.Layout.FurnitureInstances);
            hiddenSourceInstanceId = null;
            ClearPreviewPresentation();
            actionBarView.Hide();
            catalogueView.ShowCatalogue();
        }

        DecorationTouchHit IDecorationTouchHitClassifier.ClassifyBegan(
            int touchId,
            Vector2 screenPosition)
        {
            return ClassifyPrimaryBegan(screenPosition);
        }

        DecorationTouchHit IDecorationTouchHitClassifier.ClassifyCurrent(
            int touchId,
            Vector2 screenPosition)
        {
            return ClassifyPrimaryBegan(screenPosition);
        }

        private void HandleHudToggleClicked()
        {
            if (!isActiveAndEnabled
                || decorationModeButton == null
                || !decorationModeButton.isActiveAndEnabled
                || !decorationModeButton.gameObject.activeInHierarchy
                || !decorationModeButton.interactable)
            {
                return;
            }

            try
            {
                if (isOpen)
                {
                    TryRequestExit();
                }
                else
                {
                    EnterDecorationMode();
                }
            }
            finally
            {
                SyncHudLabel();
            }
        }

        private void InstallHudListener()
        {
            if (decorationModeButton == null)
            {
                hudListenerInstalled = false;
                return;
            }

            decorationModeButton.onClick.RemoveListener(HandleHudToggleClicked);
            decorationModeButton.onClick.AddListener(HandleHudToggleClicked);
            hudListenerInstalled = true;
        }

        private void RemoveHudListener()
        {
            if (hudListenerInstalled && decorationModeButton != null)
            {
                decorationModeButton.onClick.RemoveListener(HandleHudToggleClicked);
            }

            hudListenerInstalled = false;
        }

        private void SyncHudLabel()
        {
            if (decorationModeButtonLabel != null)
            {
                decorationModeButtonLabel.text = isOpen ? OpenHudLabel : ClosedHudLabel;
            }
        }

        private void EnsureRuntimeDependencies()
        {
            if (layoutRuntime == null
                || contentCatalog == null
                || catalogueAsset == null
                || targetCamera == null
                || cameraSettings == null
                || cameraController == null
                || sceneInteraction == null
                || floorCollider == null
                || gridRoot == null
                || sceneRegistry == null
                || previewView == null
                || gridView == null
                || cameraDriver == null
                || catalogueView == null
                || actionBarView == null
                || storeModalView == null)
            {
                throw new InvalidOperationException(
                    "DecorationModeController references are incomplete.");
            }

            var gameTimeService = ResolveGameTimeService();
            touchSource = ResolveTouchSource();
            mouseSource = ResolveMouseSource();
            if (gameTimeService == null || touchSource == null || mouseSource == null)
            {
                throw new InvalidOperationException(
                    "DecorationModeController requires shared time, Touch, and Mouse services.");
            }

            if (!runtimeBootstrapComplete && !TryInitializeStartupRuntime())
            {
                throw new InvalidOperationException(
                    "Decoration runtime bootstrap references are incomplete.");
            }

            layoutRuntime.Initialize();
            InitializePhase7RuntimeIfConfigured();
            if (layoutRuntime.Layout == null)
            {
                throw new InvalidOperationException("CafeLayout runtime initialization failed.");
            }

            if (gridSpace.Settings == null)
            {
                gridSpace = new DecorationGridSpace(
                    layoutRuntime.Layout.GridSettings,
                    new LayoutBounds(new GridPosition(0, 0), new GridSize(8, 8)));
            }

            session ??= new DecorationSession(layoutRuntime.Layout);
            pointerBoundary ??= new UiPointerBoundary();
            navigationCoordinator ??= new UiNavigationCoordinator();
            transitionRunner ??= new UiTransitionRunner(() => false);
            pauseCoordinator ??= new UiPauseCoordinator(gameTimeService);

            sanitizedFurnitureDragOffsetPixels =
                SanitizeNonNegative(furnitureDragOffsetPixels);
            sanitizedFurnitureHoverHeight = SanitizeNonNegative(furnitureHoverHeight);
            touchRouter = new DecorationTouchRouter(
                cameraSettings.DragThresholdPixels,
                sanitizedFurnitureDragOffsetPixels);
        }

        private bool TryInitializeStartupRuntime()
        {
            if (runtimeBootstrapComplete)
            {
                return true;
            }

            // Validate the entire candidate before CafeLayoutRuntime may publish Layout.
            var gameTimeService = ResolveGameTimeService();
            var startupTouchSource = ResolveTouchSource();
            var startupMouseSource = ResolveMouseSource();
            if (layoutRuntime == null
                || contentCatalog == null
                || catalogueAsset == null
                || gameTimeService == null
                || startupTouchSource == null
                || startupMouseSource == null
                || targetCamera == null
                || cameraSettings == null
                || cameraController == null
                || sceneInteraction == null
                || floorCollider == null
                || gridRoot == null
                || furnitureRepresentationRoot == null
                || furniturePreviewRoot == null
                || gridVisualRoot == null
                || gridMaterialTemplate == null
                || uiTheme == null
                || sceneRegistry == null
                || previewView == null
                || gridView == null
                || cameraDriver == null
                || catalogueView == null
                || actionBarView == null
                || storeModalView == null
                || decorationModeButton == null
                || decorationModeButtonLabel == null
                || timeControlPanel == null)
            {
                return false;
            }

            if (!layoutRuntime.UsesContentCatalog(contentCatalog))
            {
                return false;
            }

            layoutRuntime.Initialize();
            InitializePhase7RuntimeIfConfigured();
            var layout = layoutRuntime.Layout;
            if (layout == null)
            {
                return false;
            }

            var candidateGridSpace = new DecorationGridSpace(
                layout.GridSettings,
                new LayoutBounds(new GridPosition(0, 0), new GridSize(8, 8)));

            session ??= new DecorationSession(layout);
            pointerBoundary ??= new UiPointerBoundary();
            navigationCoordinator ??= new UiNavigationCoordinator();
            transitionRunner ??= new UiTransitionRunner(() => false);
            pauseCoordinator ??= new UiPauseCoordinator(gameTimeService);
            touchSource = startupTouchSource;
            mouseSource = startupMouseSource;

            // Rebuild is deliberately last: a failed Configure step publishes no formal clone.
            sceneRegistry.Configure(
                contentCatalog,
                furnitureRepresentationRoot,
                candidateGridSpace);
            previewView.Configure(furniturePreviewRoot, candidateGridSpace, uiTheme);
            gridView.Configure(
                gridVisualRoot,
                candidateGridSpace,
                gridMaterialTemplate,
                uiTheme);
            ConfigureViews();
            if (!phase7CatalogueBound)
            {
                if(floorStyleCatalogue!=null&&wallpaperStyleCatalogue!=null&&paintStyleCatalogue!=null&&wainscotingStyleCatalogue!=null&&wallDecorCatalogue!=null&&windowCatalogue!=null)
                {
                    phase7CatalogueCategories=DecorationCatalogueModelBuilder.Build(catalogueAsset,floorStyleCatalogue,wallpaperStyleCatalogue,paintStyleCatalogue,wainscotingStyleCatalogue,wallDecorCatalogue,windowCatalogue);
                    phase7CatalogueBound=true;catalogueView.BindCategories(phase7CatalogueCategories.Where(category=>category.CategoryId=="furniture").ToArray(),item=>TrySelectCatalogueItem(item));
                }
                else catalogueView.Bind(catalogueAsset);
            }
            sceneRegistry.Rebuild(layout.FurnitureInstances);
            if (!sceneRegistry.TryGet(
                    CafeLayoutRuntime.InitialInstanceId,
                    out var initialRepresentation)
                || initialRepresentation == null
                || !initialRepresentation.activeInHierarchy)
            {
                sceneRegistry.Rebuild(Array.Empty<FurnitureInstance>());
                return false;
            }

            gridSpace = candidateGridSpace;
            ConfigurePhase7SceneViewsIfAvailable();
            viewsConfigured = true;
            catalogueBound = true;
            runtimeBootstrapComplete = true;
            catalogueView.Hide();
            actionBarView.Hide();
            storeModalView.CloseForOwnerShutdown();
            SetPhase7ChromeVisible(false);
            return true;
        }

        private IGameTimeService ResolveGameTimeService()
        {
            return gameTimeServiceOverride
                ?? gameTimeServiceBehaviour as IGameTimeService;
        }

        private IDecorationTouchSource ResolveTouchSource()
        {
            return touchSourceOverride
                ?? touchSourceBehaviour as IDecorationTouchSource;
        }

        private IMouseDecorationInputSource ResolveMouseSource()
        {
            return mouseSourceOverride ?? mouseSourceBehaviour;
        }

        private DecorationTouchFrame ReadActivePointerFrame()
        {
            if (activePointerDeviceFamily == PointerDeviceFamily.Touch)
            {
                return touchSource.ReadFrame();
            }

            if (activePointerDeviceFamily == PointerDeviceFamily.Mouse)
            {
                return mouseSource.ReadFrame();
            }

            var touchFrame = touchSource.ReadFrame();
            if (HasNonTerminalPointer(touchFrame))
            {
                activePointerDeviceFamily = PointerDeviceFamily.Touch;
                return touchFrame;
            }

            if (mouseSource != null)
            {
                var mouseFrame = mouseSource.ReadFrame();
                if (mouseFrame.Touches.Length > 0)
                {
                    activePointerDeviceFamily = PointerDeviceFamily.Mouse;
                    return mouseFrame;
                }
            }

            return touchFrame;
        }

        private static bool HasNonTerminalPointer(DecorationTouchFrame frame)
        {
            for (var index = 0; index < frame.Touches.Length; index++)
            {
                if (!frame.Touches[index].IsTerminal)
                {
                    return true;
                }
            }

            return false;
        }

        private void ApplyIdleMouseWheelZoom()
        {
            if (mouseSource == null || mouseSource.HasActivePointer)
            {
                return;
            }

            var scrollDelta = mouseSource.ReadScrollDelta();
            if (scrollDelta != 0f)
            {
                cameraDriver.ApplyPinchZoom(scrollDelta);
            }
        }

        private void ConfigureViews()
        {
            catalogueView.Configure(pointerBoundary, transitionRunner);
            AttachActionBarForActiveMode();
            actionBarView.Configure(pointerBoundary, transitionRunner);
            storeModalView.Configure(
                navigationCoordinator,
                pauseCoordinator,
                pointerBoundary,
                transitionRunner);
            cameraDriver.Configure(cameraController);
            if (modeTabsView != null || floorRangeView != null || exitModalView != null)
                ConfigurePhase7Ui(modeTabsView, floorRangeView, exitModalView);
        }

        private void ConfigurePhase7SceneViewsIfAvailable()
        {
            if (phase7RoomSurfaceLayout == null || phase7WallAuthoring == null || phase7WallAuthoring.Length != 2)
                return;
            var styles=floorStyleCatalogue.Entries.Concat(wallpaperStyleCatalogue.Entries)
                .Concat(paintStyleCatalogue.Entries).Concat(wainscotingStyleCatalogue.Entries);
            var lookup=new SurfaceStyleLookup(styles);
            if(wallSurfaceRegistry!=null)
            {
                foreach(var authoring in phase7WallAuthoring)
                {
                    var renderer=authoring.transform.Find("WallVisual")?.GetComponent<Renderer>()
                        ?? authoring.GetComponentInChildren<Renderer>();
                    if(renderer==null)continue;
                    var view=authoring.gameObject.GetComponent<WallSurfaceView>()??authoring.gameObject.AddComponent<WallSurfaceView>();
                    view.Configure(authoring,renderer,lookup);
                    if(!wallSurfaceRegistry.TryGet(authoring.SurfaceId,out _))wallSurfaceRegistry.Register(view);
                }
                wallSurfaceRegistry.RenderConfirmed(phase7RoomSurfaceLayout);
            }
            if(floorSurfaceGridView!=null&&floorCollider!=null)
            {
                var template=floorCollider.GetComponentInChildren<Renderer>();
                if(template!=null){floorSurfaceGridView.Configure(gridRoot,gridSpace,template,.01f,lookup);if(projectionValidMaterial!=null)floorSurfaceGridView.ConfigureSelectionFeedback(projectionValidMaterial);floorSurfaceGridView.RenderConfirmed(phase7RoomSurfaceLayout);}
            }
            if(wallMountedProjectionView!=null&&projectionValidMaterial!=null&&projectionInvalidMaterial!=null)
                wallMountedProjectionView.Configure(wallMountedProjectionView.transform,projectionValidMaterial,projectionInvalidMaterial);
        }

        private void SubscribeViewEvents()
        {
            UnsubscribeViewEvents();
            catalogueView.Selected += HandleCatalogueSelected;
            catalogueView.StateChanged += HandleCatalogueStateChanged;
            actionBarView.RotateRequested += HandleRotateRequested;
            actionBarView.UndoLastRequested += HandleUndoLastRequested;
            actionBarView.ApplyAllRequested += HandleApplyAllRequested;
            actionBarView.ConfirmRequested += HandleConfirmRequested;
            actionBarView.CancelRequested += HandleCancelRequested;
            actionBarView.StoreRequested += HandleStoreRequested;
            storeModalView.ConfirmRequested += HandleStoreConfirmRequested;
            storeModalView.DismissRequested += HandleStoreDismissRequested;
            viewEventsSubscribed = true;
        }

        private void UnsubscribeViewEvents()
        {
            if (!viewEventsSubscribed)
            {
                return;
            }

            if (catalogueView != null)
            {
                catalogueView.Selected -= HandleCatalogueSelected;
                catalogueView.StateChanged -= HandleCatalogueStateChanged;
            }

            if (actionBarView != null)
            {
                actionBarView.RotateRequested -= HandleRotateRequested;
                actionBarView.UndoLastRequested -= HandleUndoLastRequested;
                actionBarView.ApplyAllRequested -= HandleApplyAllRequested;
                actionBarView.ConfirmRequested -= HandleConfirmRequested;
                actionBarView.CancelRequested -= HandleCancelRequested;
                actionBarView.StoreRequested -= HandleStoreRequested;
            }

            if (storeModalView != null)
            {
                storeModalView.ConfirmRequested -= HandleStoreConfirmRequested;
                storeModalView.DismissRequested -= HandleStoreDismissRequested;
            }

            viewEventsSubscribed = false;
        }

        private void SubscribePhase7Ui()
        {
            if (modeTabsView != null)
            {
                modeTabsView.ModeRequested -= TryChangeMode;
                modeTabsView.ModeRequested += TryChangeMode;
            }
            if (floorRangeView != null)
            {
                floorRangeView.RangeRequested -= TrySelectFloorRange;
                floorRangeView.RangeRequested += TrySelectFloorRange;
            }
            if (exitModalView != null)
            {
                exitModalView.ContinueEditingRequested -= HandleContinueEditingRequested;
                exitModalView.ContinueEditingRequested += HandleContinueEditingRequested;
                exitModalView.DiscardChangesRequested -= HandleDiscardChangesRequested;
                exitModalView.DiscardChangesRequested += HandleDiscardChangesRequested;
            }
        }

        private void UnsubscribePhase7Ui()
        {
            if (modeTabsView != null)
            {
                modeTabsView.ModeRequested -= TryChangeMode;
            }
            if (floorRangeView != null)
            {
                floorRangeView.RangeRequested -= TrySelectFloorRange;
            }
            if (exitModalView != null)
            {
                exitModalView.ContinueEditingRequested -= HandleContinueEditingRequested;
                exitModalView.DiscardChangesRequested -= HandleDiscardChangesRequested;
            }
        }

        private void HandleContinueEditingRequested()
        {
            // The modal owns closing itself. Live transactions remain untouched.
        }

        private void HandleDiscardChangesRequested()
        {
            CancelActivePhase7Preview();
            if (session?.ActivePreview != null)
            {
                CancelActivePreview();
            }
            ExitDecorationMode();
        }

        private void HandleCatalogueStateChanged(DecorationCatalogueState state)
        {
            if (!isOpen || session?.ActivePreview == null)
            {
                return;
            }

            if (state == DecorationCatalogueState.Expanded)
            {
                actionBarView.Hide();
            }
            else if (state == DecorationCatalogueState.Collapsed
                     && (session.State == DecorationSessionState.PreviewingNewFurniture
                         || session.State == DecorationSessionState.EditingExistingFurniture))
            {
                ShowActionForActivePreview();
            }
        }

        private void HandleCatalogueSelected(FurnitureDefinitionAsset definition)
        {
            if (!isOpen
                || session.State != DecorationSessionState.BrowsingCatalogue
                || definition == null
                || definition.Prefab == null
                || !TryProjectScreenToGrid(targetCamera.pixelRect.center, out var position))
            {
                return;
            }

            cameraDriver.StopEdgeAutoPan();
            storeModalView.CloseForOwnerShutdown();
            session.BeginNew(definition.DefinitionId, position);
            hiddenSourceInstanceId = null;
            catalogueView.ShowCollapsedHandle();
            SyncActivePreviewPresentation();
            ShowActionForActivePreview();
        }

        private void HandleFurnitureBegan(string instanceId)
        {
            if (!isOpen
                || string.IsNullOrEmpty(instanceId)
                || session.State == DecorationSessionState.ConfirmingStore)
            {
                return;
            }

            var current = session.ActivePreview;
            if (current != null
                && !current.IsNew
                && string.Equals(
                    current.SourceInstanceId,
                    instanceId,
                    StringComparison.Ordinal))
            {
                return;
            }

            cameraDriver.StopEdgeAutoPan();
            storeModalView.CloseForOwnerShutdown();
            if (hiddenSourceInstanceId != null)
            {
                sceneRegistry.Rebuild(layoutRuntime.Layout.FurnitureInstances);
                hiddenSourceInstanceId = null;
            }

            var result = session.BeginExisting(instanceId);
            if (!result.Succeeded)
            {
                ClearPreviewPresentation();
                actionBarView.Hide();
                catalogueView.ShowCatalogue();
                return;
            }

            if (!sceneRegistry.SetRepresentationVisible(instanceId, false))
            {
                session.CancelPreview();
                ClearPreviewPresentation();
                actionBarView.Hide();
                catalogueView.ShowCatalogue();
                return;
            }

            hiddenSourceInstanceId = instanceId;
            catalogueView.ShowCollapsedHandle();
            SyncActivePreviewPresentation();
            ShowActionForActivePreview();
        }

        private void ApplyPreviewMove(GridPosition position)
        {
            if (!CanMutatePreview())
            {
                return;
            }

            position = ClampPreviewPositionToBounds(position);
            if (session.ActivePreview.ProposedPosition == position)
            {
                return;
            }

            var result = session.MovePreview(position);
            SyncActivePreviewPresentation();
            ShowActionForResult(result);
        }

        private GridPosition ClampPreviewPositionToBounds(GridPosition position)
        {
            var preview = session?.ActivePreview;
            if (preview == null)
            {
                return position;
            }

            var footprint = layoutRuntime.Layout.GetFurnitureFootprintCells(
                preview.DefinitionId,
                new GridPosition(0, 0),
                preview.ProposedRotation);
            var maximumX = 0;
            var maximumY = 0;
            for (var index = 0; index < footprint.Count; index++)
            {
                maximumX = Math.Max(maximumX, footprint[index].X);
                maximumY = Math.Max(maximumY, footprint[index].Y);
            }

            var bounds = gridSpace.Bounds;
            var maximumPositionX = checked(
                bounds.Origin.X + bounds.Size.Width - 1 - maximumX);
            var maximumPositionY = checked(
                bounds.Origin.Y + bounds.Size.Height - 1 - maximumY);
            return new GridPosition(
                Math.Clamp(position.X, bounds.Origin.X, maximumPositionX),
                Math.Clamp(position.Y, bounds.Origin.Y, maximumPositionY));
        }

        private void HandleRotateRequested()
        {
            if (activeMode == DecorationModeKind.Floor
                && surfaceSession?.ActivePreview != null)
            {
                surfaceSession.RotateFloor();
                RefreshSurfacePreviewViews();
                return;
            }
            if (activeMode != DecorationModeKind.Furniture)
            {
                return;
            }

            if (!CanMutatePreview())
            {
                return;
            }

            if (!CanAcceptActionBarRequest())
            {
                ShowActionForActivePreview();
                return;
            }

            cameraDriver.StopEdgeAutoPan();
            var result = session.RotatePreview();
            SyncActivePreviewPresentation();
            ShowActionForResult(result);
        }

        private void HandleUndoLastRequested()
        {
            if (activeMode != DecorationModeKind.Floor
                || surfaceSession?.ActivePreview == null)
            {
                return;
            }

            surfaceSession.UndoLast();
            RefreshSurfacePreviewViews();
            ShowPhase7ActionForActivePreview();
        }

        private void HandleApplyAllRequested()
        {
            if ((activeMode != DecorationModeKind.Floor
                    && activeMode != DecorationModeKind.Wall)
                || surfaceSession?.ActivePreview == null)
            {
                return;
            }

            surfaceSession.ApplyAll();
            RefreshSurfacePreviewViews();
            ShowPhase7ActionForActivePreview();
        }

        private void HandleConfirmRequested()
        {
            if (activeMode != DecorationModeKind.Furniture)
            {
                if (TryConfirmPhase7Preview())
                {
                    actionBarView?.Hide();
                    if (activeMode == DecorationModeKind.WallDecor)
                    {
                        // Match Furniture: once the ghost becomes a committed scene
                        // object, leave the scene unobstructed for an immediate reselect.
                        catalogueView?.ShowCollapsedHandle();
                        catalogueView?.SetSheetState(
                            DecorationSheetState.CompactPreview,
                            hasActivePreview: false);
                    }
                    else
                    {
                        catalogueView?.ShowCatalogue();
                        catalogueView?.SetSheetState(
                            DecorationSheetState.Expanded,
                            hasActivePreview: false);
                    }
                }
                return;
            }

            if (!CanMutatePreview())
            {
                return;
            }

            if (!CanAcceptActionBarRequest())
            {
                ShowActionForActivePreview();
                return;
            }

            cameraDriver.StopEdgeAutoPan();
            var result = session.ConfirmPreview();
            if (!result.Succeeded)
            {
                SyncActivePreviewPresentation();
                catalogueView.ShowCollapsedHandle();
                storeModalView.CloseForOwnerShutdown();
                ShowActionForResult(result);
                return;
            }

            storeModalView.CloseForOwnerShutdown();
            actionBarView.Hide();
            ClearPreviewPresentation();
            sceneRegistry.Rebuild(layoutRuntime.Layout.FurnitureInstances);
            hiddenSourceInstanceId = null;
            // Keep the catalogue compact after placement so the committed furniture
            // remains immediately selectable for another adjustment.
            catalogueView.ShowCollapsedHandle();
        }

        private void HandleCancelRequested()
        {
            if (activeMode != DecorationModeKind.Furniture)
            {
                CancelActivePhase7Preview();
                actionBarView?.Hide();
                catalogueView?.ShowCatalogue();
                catalogueView?.SetSheetState(
                    DecorationSheetState.Expanded,
                    hasActivePreview: false);
                return;
            }

            if (!CanMutatePreview())
            {
                return;
            }

            if (!CanAcceptActionBarRequest())
            {
                ShowActionForActivePreview();
                return;
            }

            CancelActivePreview();
        }

        private void HandleStoreRequested()
        {
            if (activeMode == DecorationModeKind.WallDecor)
            {
                var wallPreview = wallMountedSession?.ActivePreview;
                if (!isOpen || wallPreview == null || !wallPreview.IsExisting
                    || wallPreview.IsStoreConfirmationPending
                    || !CanAcceptActionBarRequest())
                {
                    return;
                }

                if (!wallMountedSession.BeginStoreConfirmation())
                {
                    return;
                }

                if (!phase7WallDefinitionsById.TryGetValue(
                        wallPreview.DefinitionId, out var wallDefinition))
                {
                    wallMountedSession.DismissStoreConfirmation();
                    ShowPhase7ActionForActivePreview();
                    return;
                }

                catalogueView?.Hide();
                catalogueView?.SetSheetState(
                    DecorationSheetState.Hidden,
                    hasActivePreview: true);
                storeModalView.ShowWallMounted(wallDefinition);
                return;
            }

            var preview = session?.ActivePreview;
            if (!isOpen
                || session.State != DecorationSessionState.EditingExistingFurniture
                || preview == null
                || preview.IsNew)
            {
                return;
            }

            if (!CanAcceptActionBarRequest())
            {
                ShowActionForActivePreview();
                return;
            }

            cameraDriver.StopEdgeAutoPan();
            if (!session.BeginStoreConfirmation())
            {
                return;
            }

            if (!contentCatalog.TryGetDefinitionAsset(
                preview.DefinitionId,
                out var definition))
            {
                session.DismissStoreConfirmation();
                ShowActionForActivePreview();
                return;
            }

            catalogueView.Hide();
            storeModalView.Show(definition);
        }

        private void HandleStoreDismissRequested()
        {
            if (activeMode == DecorationModeKind.WallDecor
                && wallMountedSession?.ActivePreview?.IsStoreConfirmationPending == true)
            {
                wallMountedSession.DismissStoreConfirmation();
                catalogueView?.ShowCollapsedHandle();
                catalogueView?.SetSheetState(
                    DecorationSheetState.CompactPreview,
                    hasActivePreview: true);
                ShowPhase7ActionForActivePreview();
                return;
            }

            if (!isOpen
                || session.State != DecorationSessionState.ConfirmingStore
                || session.ActivePreview == null)
            {
                return;
            }

            cameraDriver.StopEdgeAutoPan();
            session.DismissStoreConfirmation();
            catalogueView.ShowCollapsedHandle();
            ShowActionForActivePreview();
        }

        private void HandleStoreConfirmRequested()
        {
            if (activeMode == DecorationModeKind.WallDecor
                && wallMountedSession?.ActivePreview?.IsStoreConfirmationPending == true)
            {
                var instanceId = wallMountedSession.ActivePreview.InstanceId;
                var wallStoreResult = wallMountedSession.ConfirmStore();
                if (!wallStoreResult.Succeeded)
                {
                    wallMountedSession.DismissStoreConfirmation();
                    ShowPhase7ActionForActivePreview();
                    return;
                }

                wallMountedProjectionView?.ClearPreview();
                wallMountedDisplaySurfaceId = null;
                wallMountedDisplayPosition = default;
                wallMountedSceneRegistry?.Remove(instanceId, destroyRepresentation: true);
                if (string.Equals(
                        hiddenWallMountedSourceInstanceId,
                        instanceId,
                        StringComparison.Ordinal))
                {
                    hiddenWallMountedSourceInstanceId = null;
                }
                actionBarView?.Hide();
                catalogueView?.ShowCatalogue();
                catalogueView?.SetSheetState(
                    DecorationSheetState.Expanded,
                    hasActivePreview: false);
                return;
            }

            if (!isOpen
                || session.State != DecorationSessionState.ConfirmingStore
                || session.ActivePreview == null)
            {
                return;
            }

            cameraDriver.StopEdgeAutoPan();
            var result = session.ConfirmStore();
            if (!result.Succeeded)
            {
                session.DismissStoreConfirmation();
                catalogueView.ShowCollapsedHandle();
                SyncActivePreviewPresentation();
                ShowActionForResult(result);
                return;
            }

            actionBarView.Hide();
            ClearPreviewPresentation();
            sceneRegistry.Rebuild(layoutRuntime.Layout.FurnitureInstances);
            hiddenSourceInstanceId = null;
            catalogueView.ShowCatalogue();
        }

        private bool CanMutatePreview()
        {
            return isOpen
                && session?.ActivePreview != null
                && (session.State == DecorationSessionState.PreviewingNewFurniture
                    || session.State == DecorationSessionState.EditingExistingFurniture);
        }

        private bool CanAcceptActionBarRequest()
        {
            if (touchRouter == null)
            {
                return true;
            }

            return touchRouter.Owner == DecorationGestureOwner.None
                || touchRouter.Owner == DecorationGestureOwner.Ui;
        }

        private void ShowActionForActivePreview()
        {
            var preview = session.ActivePreview;
            ShowActionForResult(preview.PlacementResult);
        }

        private void ShowPhase7ActionForActivePreview()
        {
            if (actionBarView == null)
            {
                return;
            }

            var canConfirm = activeMode == DecorationModeKind.WallDecor
                ? wallMountedSession?.ActivePreview?.CanConfirm == true
                : surfaceSession?.ActivePreview?.HasChanges == true;
            var existing = activeMode == DecorationModeKind.WallDecor
                && wallMountedSession?.ActivePreview?.IsExisting == true;
            var feedback = PlacementFeedbackKey.None;
            if (activeMode == DecorationModeKind.WallDecor
                && wallMountedSession?.ActivePreview is { } wallPreview
                && !wallPreview.IsValid)
            {
                feedback = PlacementFeedbackMapper.Map(
                    WallPlacementResult.Failure(wallPreview.FailureReason));
            }
            AttachActionBarForActiveMode();
            actionBarView.SetModeActions(activeMode, existing);
            if (activeMode == DecorationModeKind.Floor)
            {
                actionBarView.SetFloorUtilityActionsEnabled(
                    surfaceSession?.ActivePreview?.Scope == SurfaceEditScope.SingleGridFloor);
            }
            actionBarView.Show(existing, canConfirm, feedback);
            var keepSurfaceCatalogueExpanded = activeMode == DecorationModeKind.Floor
                || activeMode == DecorationModeKind.Wall;
            if (keepSurfaceCatalogueExpanded)
            {
                catalogueView?.ShowCatalogue();
                catalogueView?.SetSheetState(
                    DecorationSheetState.Expanded,
                    hasActivePreview: true);
            }
            else
            {
                catalogueView?.ShowCollapsedHandle();
                catalogueView?.SetSheetState(
                    DecorationSheetState.CompactPreview,
                    hasActivePreview: true);
            }
            UpdateActionPresentation();
        }

        private void RefreshTargetSelectionInstruction()
        {
            if (actionBarView == null || HasAnyActivePreview())
            {
                return;
            }

            AttachActionBarForActiveMode();
            if (activeMode == DecorationModeKind.Wall
                && string.IsNullOrEmpty(selectedWallTarget))
            {
                actionBarView.ShowInstruction(PlacementFeedbackKey.SelectWallTarget);
                return;
            }

            if (activeMode == DecorationModeKind.Floor
                && floorRange == SurfaceEditScope.SingleGridFloor
                && !selectedFloorTarget.HasValue)
            {
                actionBarView.ShowInstruction(PlacementFeedbackKey.SelectFloorGridTarget);
                return;
            }

            actionBarView.Hide();
        }

        private void AttachActionBarForActiveMode()
        {
            if (actionBarView == null)
            {
                return;
            }

            var surfaceHost = catalogueView?.SurfaceFooterHost;
            if (nonSurfaceActionHost == null
                && actionBarView.transform.parent is RectTransform currentHost
                && currentHost != surfaceHost)
            {
                nonSurfaceActionHost = currentHost;
            }

            var isSurfaceMode = activeMode == DecorationModeKind.Floor
                || activeMode == DecorationModeKind.Wall;
            var targetHost = isSurfaceMode ? surfaceHost : nonSurfaceActionHost;
            if (targetHost != null)
            {
                actionBarView.AttachToHost(targetHost);
            }
        }

        private void BindCatalogueForActiveMode()
        {
            if (catalogueView == null || phase7CatalogueCategories == null)
            {
                return;
            }

            var expectedKind = activeMode switch
            {
                DecorationModeKind.Furniture => DecorationCatalogueItemKind.Furniture,
                DecorationModeKind.Floor => DecorationCatalogueItemKind.Floor,
                DecorationModeKind.Wall => DecorationCatalogueItemKind.WallSurface,
                DecorationModeKind.WallDecor => DecorationCatalogueItemKind.WallMounted,
                _ => DecorationCatalogueItemKind.Furniture
            };
            var filtered = phase7CatalogueCategories
                .Where(category => category != null
                    && category.Items.Any(item => item.Kind == expectedKind))
                .Select(category => new DecorationCategoryModel(
                    category.CategoryId,
                    category.DisplayName,
                    category.Items.Where(item => item.Kind == expectedKind).ToArray()))
                .ToArray();
            catalogueView.BindCategories(filtered, item => TrySelectCatalogueItem(item));
            RefreshSurfaceCatalogueState();
        }

        private void RefreshSurfaceCatalogueState()
        {
            if (catalogueView == null) return;
            var preview = surfaceSession?.ActivePreview;
            if (preview != null)
            {
                if (preview.Scope == SurfaceEditScope.Wall)
                    SetWallSurfacePreviewStates(preview);
                else
                    catalogueView.SetSurfaceState(
                        preview.Scope == SurfaceEditScope.WholeRoomFloor ? null : preview.UsingStyleId,
                        preview.PreviewStyleId);
                return;
            }
            if (activeMode == DecorationModeKind.Wall
                && !string.IsNullOrEmpty(selectedWallTarget))
            {
                catalogueView.SetSurfaceStates(
                    GetConfirmedWallStyleIds(selectedWallTarget), null);
                return;
            }
            if (activeMode == DecorationModeKind.Floor
                && floorRange == SurfaceEditScope.SingleGridFloor
                && selectedFloorTarget.HasValue
                && phase7RoomSurfaceLayout != null
                && phase7RoomSurfaceLayout.TryGetFloor(selectedFloorTarget.Value, out var floor))
            {
                catalogueView.SetSurfaceState(floor.StyleId, null);
                return;
            }
            catalogueView.SetSurfaceState(null, null);
        }

        private IReadOnlyList<string> GetConfirmedWallStyleIds(string surfaceId)
        {
            if (phase7RoomSurfaceLayout == null
                || string.IsNullOrEmpty(surfaceId)
                || !phase7RoomSurfaceLayout.TryGetWall(surfaceId, out var wall))
                return Array.Empty<string>();

            var usingIds = new List<string>(2) { wall.BaseStyleId };
            var wainscotingId = wall.WainscotingStyleId;
            if (string.IsNullOrEmpty(wainscotingId))
            {
                wainscotingId = phase7StylesById.Values
                    .FirstOrDefault(style => style != null
                        && style.Kind == SurfaceStyleKind.Wainscoting
                        && style.IsNoneOption)
                    ?.StyleId;
            }
            if (!string.IsNullOrEmpty(wainscotingId))
                usingIds.Add(wainscotingId);
            return usingIds;
        }

        private void SetWallSurfacePreviewStates(SurfacePreviewTransaction preview)
        {
            if (catalogueView == null)
            {
                return;
            }

            var usingIds = new HashSet<string>(
                GetConfirmedWallStyleIds(preview.TargetWallSurfaceId),
                StringComparer.Ordinal);
            var previewIds = new HashSet<string>(StringComparer.Ordinal);
            if (!string.IsNullOrEmpty(preview.PreviewWallBaseStyleId))
            {
                previewIds.Add(preview.PreviewWallBaseStyleId);
            }
            if (!string.IsNullOrEmpty(preview.PreviewWallWainscotingStyleId))
            {
                previewIds.Add(preview.PreviewWallWainscotingStyleId);
            }

            catalogueView.SetSurfaceStates(usingIds, null);
            foreach (var tile in catalogueView
                         .GetComponentsInChildren<DecorationCatalogueTileView>(true))
            {
                tile.SetSurfaceState(
                    usingIds.Contains(tile.ItemId),
                    previewIds.Contains(tile.ItemId));
            }
        }

        private void ShowActionForResult(PlacementResult result)
        {
            var preview = session.ActivePreview;
            if (preview == null)
            {
                return;
            }

            var existingSourceStillPresent = !preview.IsNew
                && layoutRuntime.Layout.TryGetFurnitureInstance(
                    preview.SourceInstanceId,
                    out _);
            AttachActionBarForActiveMode();
            actionBarView.SetModeActions(activeMode, existingSourceStillPresent);
            actionBarView.Show(
                existingSourceStillPresent,
                result.Succeeded,
                PlacementFeedbackMapper.Map(result));
            UpdateActionPresentation();
        }

        private void UpdateActionPresentation()
        {
            if (actionBarView == null || !actionBarView.IsVisible)
            {
                return;
            }

            Bounds bounds;
            DecorationActionPresentation presentation;
            if (activeMode == DecorationModeKind.WallDecor
                && wallMountedSession?.ActivePreview is { } wallPreview)
            {
                if (!TryGetWallMountedPreviewBounds(out bounds))
                {
                    return;
                }
                presentation = wallPreview.IsExisting
                    ? DecorationActionPresentation.Existing
                    : DecorationActionPresentation.New;
            }
            else
            {
                var preview = session?.ActivePreview;
                if (preview == null || !previewView.TryGetWorldBounds(out bounds))
                {
                    return;
                }
                presentation = preview.IsNew
                    ? DecorationActionPresentation.New
                    : DecorationActionPresentation.Existing;
            }

            var preferred = GetActionPresentationPreferredPoint(bounds);
            if (preferred.x == float.MinValue)
            {
                return;
            }

            var safeArea = GetActionPresentationSafeArea();
            actionBarView.SetPresentation(
                presentation,
                preferred,
                safeArea);
        }

        private bool TryGetWallMountedPreviewBounds(out Bounds bounds)
        {
            var ghost = wallMountedProjectionView?.CurrentGhost;
            var renderers = ghost != null
                ? ghost.GetComponentsInChildren<Renderer>(true)
                    .Where(item => item.enabled && item.gameObject.activeInHierarchy)
                    .ToArray()
                : Array.Empty<Renderer>();
            if (renderers.Length == 0)
            {
                bounds = default;
                return false;
            }

            bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }
            return true;
        }

        private Vector2 GetActionPresentationPreferredPoint(Bounds bounds)
        {
            var preferred = new Vector2(float.MinValue, float.MinValue);
            for (var index = 0; index < 8; index++)
            {
                var corner = new Vector3(
                    (index & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (index & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (index & 4) == 0 ? bounds.min.z : bounds.max.z);
                var screen = targetCamera.WorldToScreenPoint(corner);
                if (screen.z < 0f)
                {
                    continue;
                }

                preferred.x = Mathf.Max(preferred.x, screen.x);
                preferred.y = Mathf.Max(preferred.y, screen.y);
            }

            if (preferred.x == float.MinValue)
            {
                return preferred;
            }

            return preferred + new Vector2(8f, 8f);
        }

        private Rect GetActionPresentationSafeArea()
        {
            var safeArea = Screen.safeArea;
            if (safeArea.width <= 0f || safeArea.height <= 0f)
            {
                safeArea = targetCamera.pixelRect;
            }

            var rail = decorationModeButton != null
                ? decorationModeButton.transform.parent as RectTransform
                : null;
            if (rail != null
                && rail.gameObject.activeInHierarchy
                && TryGetScreenRect(rail, out var railRect))
            {
                safeArea.xMax = Mathf.Max(
                    safeArea.xMin,
                    Mathf.Min(safeArea.xMax, railRect.xMin - 16f));
            }

            if (catalogueView != null
                && catalogueView.State == DecorationCatalogueState.Collapsed
                && catalogueView.CollapsedHandleRect != null
                && catalogueView.CollapsedHandleRect.gameObject.activeInHierarchy
                && TryGetScreenRect(catalogueView.CollapsedHandleRect, out var handleRect))
            {
                ExcludeVerticalObstacle(ref safeArea, handleRect, preferUpperRegion: true);
            }

            if (storeModalView != null
                && storeModalView.IsOpen
                && storeModalView.ContentRect != null
                && storeModalView.ContentRect.gameObject.activeInHierarchy
                && TryGetScreenRect(storeModalView.ContentRect, out var modalRect))
            {
                var lowerHeight = Mathf.Max(0f, modalRect.yMin - 16f - safeArea.yMin);
                var upperHeight = Mathf.Max(0f, safeArea.yMax - modalRect.yMax - 16f);
                ExcludeVerticalObstacle(
                    ref safeArea,
                    modalRect,
                    preferUpperRegion: upperHeight >= lowerHeight);
            }
            return safeArea;
        }

        private bool TryGetScreenRect(RectTransform rect, out Rect screenRect)
        {
            var canvas = rect != null ? rect.GetComponentInParent<Canvas>() : null;
            var eventCamera = canvas != null
                && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.rootCanvas.worldCamera ?? targetCamera
                : null;
            if (rect == null)
            {
                screenRect = default;
                return false;
            }

            rect.GetWorldCorners(actionPresentationCorners);
            var minimum = RectTransformUtility.WorldToScreenPoint(
                eventCamera, actionPresentationCorners[0]);
            var maximum = minimum;
            for (var index = 1; index < actionPresentationCorners.Length; index++)
            {
                var point = RectTransformUtility.WorldToScreenPoint(
                    eventCamera, actionPresentationCorners[index]);
                minimum = Vector2.Min(minimum, point);
                maximum = Vector2.Max(maximum, point);
            }

            screenRect = Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
            return !float.IsNaN(screenRect.xMin)
                && !float.IsNaN(screenRect.yMin)
                && !float.IsInfinity(screenRect.xMin)
                && !float.IsInfinity(screenRect.yMin);
        }

        private static void ExcludeVerticalObstacle(
            ref Rect safeArea,
            Rect obstacle,
            bool preferUpperRegion)
        {
            if (!safeArea.Overlaps(obstacle))
            {
                return;
            }

            if (preferUpperRegion)
            {
                safeArea.yMin = Mathf.Min(
                    safeArea.yMax,
                    Mathf.Max(safeArea.yMin, obstacle.yMax + 16f));
            }
            else
            {
                safeArea.yMax = Mathf.Max(
                    safeArea.yMin,
                    Mathf.Min(safeArea.yMax, obstacle.yMin - 16f));
            }
        }

        private void SyncActivePreviewPresentation()
        {
            var preview = session?.ActivePreview;
            if (preview == null)
            {
                ClearPreviewPresentation();
                return;
            }

            var cells = layoutRuntime.Layout.GetFurnitureFootprintCells(
                preview.DefinitionId,
                preview.ProposedPosition,
                preview.ProposedRotation);
            if (!string.Equals(
                previewDefinitionId,
                preview.DefinitionId,
                StringComparison.Ordinal))
            {
                if (!contentCatalog.TryGetPrefab(preview.DefinitionId, out var prefab)
                    || prefab == null)
                {
                    throw new InvalidOperationException(
                        $"Furniture Definition '{preview.DefinitionId}' has no shared Prefab.");
                }

                previewView.Show(prefab, cells);
                previewDefinitionId = preview.DefinitionId;
            }

            previewView.SetPlacement(
                cells,
                preview.ProposedRotation,
                sanitizedFurnitureHoverHeight);
            previewView.SetValidity(preview.PlacementResult.Succeeded);
            gridView.ShowFootprint(cells, preview.PlacementResult.Succeeded);
        }

        private void ClearPreviewPresentation()
        {
            if (previewView != null)
            {
                previewView.Hide();
            }
            if (gridView != null)
            {
                gridView.ClearFootprint();
            }
            previewDefinitionId = null;
        }

        private void RouteTouchResult(DecorationTouchRoutingResult result)
        {
            if (result.Owner == DecorationGestureOwner.Pinch)
            {
                furnitureDragScreenPositionInitialized = false;
                cameraDriver.StopEdgeAutoPan();
                if (result.PinchZoomRequested)
                {
                    cameraDriver.ApplyPinchZoom(result.PinchDistanceDelta);
                }

                return;
            }

            if (result.FurnitureDragRequested)
            {
                var rawFinger = result.FurnitureDragScreenPosition
                    - Vector2.up * sanitizedFurnitureDragOffsetPixels;
                var pointerMoved = !furnitureDragScreenPositionInitialized
                    || rawFinger != lastFurnitureDragScreenPosition;
                lastFurnitureDragScreenPosition = rawFinger;
                furnitureDragScreenPositionInitialized = true;
                var excluded = storeModalView.IsOpen || IsUiAt(rawFinger);
                cameraDriver.ApplyFurnitureEdgeAutoPan(
                    result.Owner,
                    touchRouter.IsDragging,
                    rawFinger,
                    targetCamera.pixelRect,
                    Screen.safeArea,
                    excluded);

                if (pointerMoved
                    && !excluded
                    && TryProjectScreenToGrid(
                        result.FurnitureDragScreenPosition,
                        out var position))
                {
                    ApplyPreviewMove(position);
                }

                return;
            }

            furnitureDragScreenPositionInitialized = false;
            cameraDriver.StopEdgeAutoPan();
            if (result.CameraPanRequested)
            {
                cameraDriver.ApplyScenePan(result.CameraPanDelta);
            }

            if (result.TapReleased
                && result.OriginHit.Kind == DecorationTouchHitKind.Scene
                && session.ActivePreview == null)
            {
                sceneInteraction.ClearSelection();
            }
        }

        private void HandleFurnitureFrame(DecorationTouchRoutingResult result) =>
            RouteTouchResult(result);

        private void HandleFloorFrame(DecorationTouchRoutingResult result)
        {
            if (result.TapReleased)
            {
                TryHandleSceneTap(result.OriginHit);
                return;
            }
            if (result.CameraPanRequested)
            {
                cameraDriver.ApplyScenePan(result.CameraPanDelta);
            }
        }

        private void HandleWallFrame(DecorationTouchRoutingResult result)
        {
            if (result.TapReleased)
            {
                TryHandleSceneTap(result.OriginHit);
                return;
            }
            if (result.CameraPanRequested)
            {
                cameraDriver.ApplyScenePan(result.CameraPanDelta);
            }
        }

        private void HandleWallMountedFrame(DecorationTouchRoutingResult result)
        {
            if (result.TapReleased)
            {
                TryHandleSceneTap(result.OriginHit);
                return;
            }
            if (result.SceneDragRequested)
            {
                TryHandleSceneDrag(result.CurrentHit);
                return;
            }
            if (result.CameraPanRequested)
            {
                cameraDriver.ApplyScenePan(result.CameraPanDelta);
            }
        }

        private bool HasAnyActivePreview()
        {
            return session?.ActivePreview != null
                || surfaceSession?.ActivePreview != null
                || wallMountedSession?.ActivePreview != null;
        }

        private DecorationTouchHit ClassifyPrimaryBegan(Vector2 screenPosition)
        {
            if (storeModalView != null && storeModalView.IsOpen)
            {
                return new DecorationTouchHit(DecorationTouchHitKind.Ui);
            }

            if (IsUiAt(screenPosition))
            {
                return new DecorationTouchHit(DecorationTouchHitKind.Ui);
            }

            if (targetCamera == null)
            {
                return new DecorationTouchHit(DecorationTouchHitKind.Scene);
            }

            var modeRay = targetCamera.ScreenPointToRay(screenPosition);
            if (activeMode == DecorationModeKind.Floor)
            {
                if (floorCollider != null
                    && floorCollider.Raycast(modeRay, out _, Mathf.Infinity)
                    && TryProjectScreenToGrid(screenPosition, out var floorPosition))
                {
                    return new DecorationTouchHit(
                        DecorationTouchHitKind.FloorGrid,
                        targetId: $"floor.{floorPosition.X}.{floorPosition.Y}",
                        floorPosition: floorPosition);
                }

                return new DecorationTouchHit(DecorationTouchHitKind.Scene);
            }

            if (activeMode == DecorationModeKind.Wall
                || activeMode == DecorationModeKind.WallDecor)
            {
                var wallHits = Physics.RaycastAll(
                    modeRay,
                    Mathf.Infinity,
                    ~0,
                    QueryTriggerInteraction.Collide);
                Array.Sort(wallHits, CompareRaycastHitsByDistance);
                for (var index = 0; index < wallHits.Length; index++)
                {
                    if (activeMode == DecorationModeKind.WallDecor
                        && wallMountedSceneRegistry != null
                        && wallMountedSceneRegistry.TryGetInstanceId(
                            wallHits[index].collider,
                            out var mountedInstanceId))
                    {
                        return new DecorationTouchHit(
                            DecorationTouchHitKind.WallMounted,
                            targetId: mountedInstanceId);
                    }

                    var authoring = wallHits[index].collider
                        .GetComponentInParent<WallSurfaceAuthoring>();
                    if (authoring == null)
                    {
                        continue;
                    }

                    if (activeMode == DecorationModeKind.Wall)
                    {
                        return new DecorationTouchHit(
                            DecorationTouchHitKind.WallSurface,
                            targetId: authoring.SurfaceId,
                            surfaceId: authoring.SurfaceId);
                    }

                    if (wallMountedSession?.ActivePreview == null)
                    {
                        return new DecorationTouchHit(DecorationTouchHitKind.Scene);
                    }

                    var local = authoring.transform.InverseTransformPoint(wallHits[index].point);
                    var column = Mathf.FloorToInt(
                        (local.x + authoring.Columns * authoring.SlotSize * 0.5f)
                        / authoring.SlotSize);
                    var row = Mathf.FloorToInt(local.y / authoring.SlotSize);
                    if (column >= 0 && column < authoring.Columns
                        && row >= 0 && row < authoring.Rows)
                    {
                        var slot = new WallSlotPosition(column, row);
                        return new DecorationTouchHit(
                            DecorationTouchHitKind.WallSlot,
                            targetId: $"{authoring.SurfaceId}:{column}:{row}",
                            surfaceId: authoring.SurfaceId,
                            wallSlotPosition: slot);
                    }

                    return new DecorationTouchHit(DecorationTouchHitKind.Scene);
                }

                return new DecorationTouchHit(DecorationTouchHitKind.Scene);
            }

            var preview = session?.ActivePreview;
            if (preview != null && IsScreenPointInsideActivePreview(screenPosition))
            {
                return new DecorationTouchHit(
                    DecorationTouchHitKind.Furniture,
                    preview.SourceInstanceId);
            }

            if (preview != null
                && TryProjectScreenToGrid(screenPosition, out var previewCell))
            {
                var previewCells = layoutRuntime.Layout.GetFurnitureFootprintCells(
                    preview.DefinitionId,
                    preview.ProposedPosition,
                    preview.ProposedRotation);
                for (var index = 0; index < previewCells.Count; index++)
                {
                    if (previewCells[index] == previewCell)
                    {
                        return new DecorationTouchHit(
                            DecorationTouchHitKind.Furniture,
                            preview.SourceInstanceId);
                    }
                }
            }

            var ray = targetCamera.ScreenPointToRay(screenPosition);
            if (TryGetVisibleFurnitureHit(ray, out var visibleInstanceId))
            {
                return new DecorationTouchHit(
                    DecorationTouchHitKind.Furniture,
                    visibleInstanceId);
            }

            // The formal grid owns selection inside an occupied cell. A neighbouring
            // furniture collider may visually overlap that screen area, especially for
            // differently sized counters, but it must not steal the smaller item's cell.
            if (TryProjectScreenToGrid(screenPosition, out var occupiedCell)
                && layoutRuntime.Layout.TryGetOccupant(
                    occupiedCell,
                    out var occupiedInstanceId)
                && sceneRegistry.TryGet(occupiedInstanceId, out var occupiedRepresentation)
                && occupiedRepresentation != null
                && occupiedRepresentation.activeInHierarchy)
            {
                return new DecorationTouchHit(
                    DecorationTouchHitKind.Furniture,
                    occupiedInstanceId);
            }

            var hits = Physics.RaycastAll(ray, Mathf.Infinity, ~0, QueryTriggerInteraction.Collide);
            Array.Sort(hits, CompareRaycastHitsByDistance);
            formalHitDistances.Clear();
            var configuredFloorHit = false;
            for (var index = 0; index < hits.Length; index++)
            {
                var hit = hits[index];
                if (hit.collider == floorCollider)
                {
                    configuredFloorHit = true;
                }

                if (!sceneRegistry.TryGetInstanceId(hit.collider, out var instanceId)
                    || !sceneRegistry.TryGet(instanceId, out var representation)
                    || representation == null
                    || !representation.activeInHierarchy)
                {
                    continue;
                }

                if (!formalHitDistances.TryGetValue(instanceId, out var priorDistance)
                    || hit.distance < priorDistance)
                {
                    formalHitDistances[instanceId] = hit.distance;
                }
            }

            string bestInstanceId = null;
            var bestDistance = float.PositiveInfinity;
            foreach (var pair in formalHitDistances)
            {
                var distanceOrder = pair.Value.CompareTo(bestDistance);
                if (distanceOrder < 0
                    || (distanceOrder == 0
                        && (bestInstanceId == null
                            || string.CompareOrdinal(pair.Key, bestInstanceId) < 0)))
                {
                    bestInstanceId = pair.Key;
                    bestDistance = pair.Value;
                }
            }

            if (bestInstanceId != null)
            {
                return new DecorationTouchHit(
                    DecorationTouchHitKind.Furniture,
                    bestInstanceId);
            }

            // Some generated furniture visuals have a collider silhouette that does not
            // cover the logical cell centre from every camera angle. When the floor was
            // hit, use the authoritative non-overlapping layout footprint as a fallback.
            if (configuredFloorHit
                && TryProjectScreenToGrid(screenPosition, out var formalCell))
            {
                var instances = layoutRuntime.Layout.FurnitureInstances;
                for (var instanceIndex = 0; instanceIndex < instances.Count; instanceIndex++)
                {
                    var instance = instances[instanceIndex];
                    if (!sceneRegistry.TryGet(instance.InstanceId, out var representation)
                        || representation == null
                        || !representation.activeInHierarchy)
                    {
                        continue;
                    }

                    var cells = layoutRuntime.Layout.GetFurnitureFootprintCells(
                        instance.DefinitionId,
                        instance.Position,
                        instance.Rotation);
                    for (var cellIndex = 0; cellIndex < cells.Count; cellIndex++)
                    {
                        if (cells[cellIndex] == formalCell)
                        {
                            return new DecorationTouchHit(
                                DecorationTouchHitKind.Furniture,
                                instance.InstanceId);
                        }
                    }
                }
            }

            return configuredFloorHit
                ? new DecorationTouchHit(DecorationTouchHitKind.Scene)
                : default;
        }

        private bool TryGetVisibleFurnitureHit(Ray ray, out string instanceId)
        {
            instanceId = null;
            var bestDistance = float.PositiveInfinity;
            var instances = layoutRuntime.Layout.FurnitureInstances;
            for (var instanceIndex = 0; instanceIndex < instances.Count; instanceIndex++)
            {
                var candidateId = instances[instanceIndex].InstanceId;
                if (!sceneRegistry.TryGet(candidateId, out var representation)
                    || representation == null
                    || !representation.activeInHierarchy)
                {
                    continue;
                }

                var renderers = representation.GetComponentsInChildren<Renderer>(true);
                for (var rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    var renderer = renderers[rendererIndex];
                    if (renderer == null
                        || !renderer.enabled
                        || !renderer.gameObject.activeInHierarchy
                        || !renderer.bounds.IntersectRay(ray, out var distance))
                    {
                        continue;
                    }

                    var distanceOrder = distance.CompareTo(bestDistance);
                    if (distanceOrder < 0
                        || (distanceOrder == 0
                            && (instanceId == null
                                || string.CompareOrdinal(candidateId, instanceId) < 0)))
                    {
                        instanceId = candidateId;
                        bestDistance = distance;
                    }
                }
            }

            return instanceId != null;
        }

        private bool IsScreenPointInsideActivePreview(Vector2 screenPosition)
        {
            if (targetCamera == null
                || previewView == null
                || !previewView.TryGetWorldBounds(out var bounds))
            {
                return false;
            }

            var min = bounds.min;
            var max = bounds.max;
            previewHitCorners[0] = new Vector3(min.x, min.y, min.z);
            previewHitCorners[1] = new Vector3(min.x, min.y, max.z);
            previewHitCorners[2] = new Vector3(min.x, max.y, min.z);
            previewHitCorners[3] = new Vector3(min.x, max.y, max.z);
            previewHitCorners[4] = new Vector3(max.x, min.y, min.z);
            previewHitCorners[5] = new Vector3(max.x, min.y, max.z);
            previewHitCorners[6] = new Vector3(max.x, max.y, min.z);
            previewHitCorners[7] = new Vector3(max.x, max.y, max.z);

            var screenMin = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var screenMax = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (var index = 0; index < previewHitCorners.Length; index++)
            {
                var projected = targetCamera.WorldToScreenPoint(previewHitCorners[index]);
                if (projected.z <= 0f)
                {
                    return false;
                }

                screenMin = Vector2.Min(screenMin, projected);
                screenMax = Vector2.Max(screenMax, projected);
            }

            const float paddingPixels = 8f;
            screenMin -= Vector2.one * paddingPixels;
            screenMax += Vector2.one * paddingPixels;
            return screenPosition.x >= screenMin.x
                && screenPosition.x <= screenMax.x
                && screenPosition.y >= screenMin.y
                && screenPosition.y <= screenMax.y;
        }

        private bool IsUiAt(Vector2 screenPosition)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            uiRaycastResults.Clear();
            if (!ReferenceEquals(uiPointerEventSystem, eventSystem)
                || uiPointerEventData == null)
            {
                uiPointerEventSystem = eventSystem;
                uiPointerEventData = new PointerEventData(eventSystem);
            }
            else
            {
                uiPointerEventData.Reset();
            }

            uiPointerEventData.position = screenPosition;
            eventSystem.RaycastAll(uiPointerEventData, uiRaycastResults);
            for (var index = 0; index < uiRaycastResults.Count; index++)
            {
                if (uiRaycastResults[index].module is GraphicRaycaster raycaster
                    && raycaster.isActiveAndEnabled
                    && raycaster.gameObject.activeInHierarchy)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryProjectScreenToGrid(
            Vector2 screenPosition,
            out GridPosition position)
        {
            if (targetCamera == null)
            {
                position = default;
                return false;
            }

            return TryRayToGrid(targetCamera.ScreenPointToRay(screenPosition), out position);
        }

        private bool TryRayToGrid(Ray ray, out GridPosition position)
        {
            if (gridRoot == null || gridSpace.Settings == null)
            {
                position = default;
                return false;
            }

            var plane = new Plane(gridRoot.up, gridRoot.position);
            if (!plane.Raycast(ray, out var distance) || distance < 0f)
            {
                position = default;
                return false;
            }

            var local = gridRoot.InverseTransformPoint(ray.GetPoint(distance));
            var cellSize = gridSpace.Settings.CellSize;
            position = new GridPosition(
                checked(gridSpace.Bounds.Origin.X + Mathf.FloorToInt(local.x / cellSize)),
                checked(gridSpace.Bounds.Origin.Y + Mathf.FloorToInt(local.z / cellSize)));
            return true;
        }

        private void CleanupDecorationMode()
        {
            if (isCleaningUp || !cleanupRequired)
            {
                return;
            }

            isCleaningUp = true;
            try
            {
                isOpen = false;
                CancelActivePhase7Preview();
                cameraDriver?.StopEdgeAutoPan();
                touchRouter?.Reset();
                mouseSource?.Reset();
                activePointerDeviceFamily = PointerDeviceFamily.None;
                UnsubscribeViewEvents();
                if (storeModalView != null)
                {
                    storeModalView.CloseForOwnerShutdown();
                }
                if (exitModalView != null)
                {
                    exitModalView.Close();
                }
                SetPhase7ChromeVisible(false);
                if (catalogueView != null)
                {
                    catalogueView.Hide();
                }
                if (actionBarView != null)
                {
                    actionBarView.Hide();
                }
                ClearPreviewPresentation();
                if (gridView != null)
                {
                    gridView.HideGrid();
                }
                if (timeControlPanel != null)
                {
                    timeControlPanel.SetDecorationPauseLock(false);
                }

                if (hiddenSourceInstanceId != null
                    && sceneRegistry != null
                    && layoutRuntime != null
                    && layoutRuntime.Layout != null)
                {
                    sceneRegistry.Rebuild(layoutRuntime.Layout.FurnitureInstances);
                }

                hiddenSourceInstanceId = null;
                session?.Exit();
                if (modeViewHandle != null)
                {
                    modeViewHandle.Close();
                    modeViewHandle = null;
                }
                else if (modeView.IsOpen)
                {
                    modeView.Close();
                }

                if (pauseHandle != null)
                {
                    pauseHandle.Dispose();
                    pauseHandle = null;
                    pauseCoordinator?.TryRestorePendingSpeed();
                }

                sceneInputSuppressionHandle?.Dispose();
                sceneInputSuppressionHandle = null;

                if (cameraStateCaptured && cameraController != null)
                {
                    cameraController.enabled = cameraEnabledBeforeEnter;
                }

                cameraStateCaptured = false;
                touchRouter = null;
                touchSource = null;
                mouseSource = null;
                cleanupRequired = false;
            }
            finally
            {
                isCleaningUp = false;
                SyncHudLabel();
            }
        }

        private void SetPhase7ChromeVisible(bool visible)
        {
            if (modeTabsView != null)
                modeTabsView.gameObject.SetActive(visible);
            if (floorRangeView != null)
                floorRangeView.gameObject.SetActive(visible && activeMode == DecorationModeKind.Floor);
            if (!visible && exitModalView != null)
                exitModalView.gameObject.SetActive(false);
        }

        private static float SanitizeNonNegative(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value)
                ? Mathf.Max(0f, value)
                : 0f;
        }

        private enum PointerDeviceFamily
        {
            None,
            Touch,
            Mouse
        }

        private static int CompareRaycastHitsByDistance(RaycastHit left, RaycastHit right)
        {
            return left.distance.CompareTo(right.distance);
        }
    }
}
