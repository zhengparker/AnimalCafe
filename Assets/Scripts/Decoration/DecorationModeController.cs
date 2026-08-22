using System;
using System.Collections.Generic;
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
        private string previewDefinitionId;
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
        private float sanitizedFurnitureDragOffsetPixels;
        private float sanitizedFurnitureHoverHeight;
        private EventSystem uiPointerEventSystem;
        private PointerEventData uiPointerEventData;

        public bool IsOpen => isOpen;

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

            RouteTouchResult(result);
            UpdateActionPresentation();
            if (touchRouter.Owner == DecorationGestureOwner.None)
            {
                activePointerDeviceFamily = PointerDeviceFamily.None;
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
            RemoveHudListener();
        }

        public void EnterDecorationMode()
        {
            if (!isActiveAndEnabled || isOpen || isEntering)
            {
                return;
            }

            isEntering = true;
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

                session.Enter();
                modeViewHandle = navigationCoordinator.OpenMainPanel(modeView);
                ClearPreviewPresentation();
                storeModalView.CloseForOwnerShutdown();
                actionBarView.Hide();
                catalogueView.ShowCatalogue();
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
                    ExitDecorationMode();
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
            catalogueView.Bind(catalogueAsset);
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
            viewsConfigured = true;
            catalogueBound = true;
            runtimeBootstrapComplete = true;
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
            if (touchFrame.Touches.Length > 0)
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
            actionBarView.Configure(pointerBoundary, transitionRunner);
            storeModalView.Configure(
                navigationCoordinator,
                pauseCoordinator,
                pointerBoundary,
                transitionRunner);
            cameraDriver.Configure(cameraController);
        }

        private void SubscribeViewEvents()
        {
            UnsubscribeViewEvents();
            catalogueView.Selected += HandleCatalogueSelected;
            actionBarView.RotateRequested += HandleRotateRequested;
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
            }

            if (actionBarView != null)
            {
                actionBarView.RotateRequested -= HandleRotateRequested;
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

        private void HandleConfirmRequested()
        {
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
            actionBarView.Show(
                existingSourceStillPresent,
                result.Succeeded,
                PlacementFeedbackMapper.Map(result));
            UpdateActionPresentation();
        }

        private void UpdateActionPresentation()
        {
            var preview = session?.ActivePreview;
            if (preview == null
                || actionBarView == null
                || !actionBarView.IsVisible
                || !previewView.TryGetWorldBounds(out var bounds))
            {
                return;
            }

            var preferred = GetActionPresentationPreferredPoint(bounds);
            if (preferred.x == float.MinValue)
            {
                return;
            }

            var safeArea = GetActionPresentationSafeArea();
            actionBarView.SetPresentation(
                preview.IsNew
                    ? DecorationActionPresentation.New
                    : DecorationActionPresentation.Existing,
                preferred,
                safeArea);
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
            previewView?.Hide();
            gridView?.ClearFootprint();
            previewDefinitionId = null;
        }

        private void RouteTouchResult(DecorationTouchRoutingResult result)
        {
            if (result.Owner == DecorationGestureOwner.Pinch)
            {
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
                var excluded = storeModalView.IsOpen || IsUiAt(rawFinger);
                cameraDriver.ApplyFurnitureEdgeAutoPan(
                    result.Owner,
                    touchRouter.IsDragging,
                    rawFinger,
                    targetCamera.pixelRect,
                    Screen.safeArea,
                    excluded);

                if (!excluded
                    && TryProjectScreenToGrid(
                        result.FurnitureDragScreenPosition,
                        out var position))
                {
                    ApplyPreviewMove(position);
                }

                return;
            }

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

            if (targetCamera == null)
            {
                return default;
            }

            var ray = targetCamera.ScreenPointToRay(screenPosition);
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
                cameraDriver?.StopEdgeAutoPan();
                touchRouter?.Reset();
                mouseSource?.Reset();
                activePointerDeviceFamily = PointerDeviceFamily.None;
                UnsubscribeViewEvents();
                storeModalView?.CloseForOwnerShutdown();
                catalogueView?.Hide();
                actionBarView?.Hide();
                ClearPreviewPresentation();
                gridView?.HideGrid();
                timeControlPanel?.SetDecorationPauseLock(false);

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
