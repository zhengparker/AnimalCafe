using System;
using System.Collections;
using AnimalCafe.Decoration;
using AnimalCafe.UI.Foundation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AnimalCafe.UI.Decoration
{
    public enum DecorationActionPresentation
    {
        New,
        Existing
    }

    /// <summary>
    /// Owns the four decoration actions and their shared completion latch.
    /// 管理四个 decoration action 及共用的一次性 completion latch。
    /// </summary>
    public sealed class DecorationActionBarView : MonoBehaviour
    {
        [SerializeField] private AnimalCafe.UI.P8R.P8RAppearance appearance;
        private const float TransitionDuration = 0.12f;
        private const float ToastTransitionDuration = 0.16f;
        private const float ToastStayDuration = 1.8f;
        private const float ToastHiddenOffset = 28f;
        private const float CompactPanelWidth = 160f;
        private const float StorePanelWidth = 216f;
        private const float CompactButtonSize = 48f;
        private const float SurfaceButtonHeight = 52f;
        private const float SurfaceUtilityButtonWidth = 104f;
        private const float SurfacePrimaryButtonWidth = 136f;
        private const float FloorActionRowOffset = -32f;
        private const float ActionSpacing = 8f;

        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform presentationRoot;
        [SerializeField] private Button storeButton;
        [SerializeField] private Button undoLastButton;
        [SerializeField] private Button applyAllButton;
        [SerializeField] private Button rotateButton;
        [SerializeField] private Image rotateIcon;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button confirmButton;
        [SerializeField] private TMP_Text feedbackLabel;
        [SerializeField] private GameObject feedbackStateShape;
        [SerializeField] private RectTransform feedbackRoot;
        [SerializeField] private CanvasGroup feedbackCanvasGroup;
        [SerializeField] private bool useReadableActionLabels;

        private UiTransitionRunner transitionRunner;
        private Coroutine transitionCoroutine;
        private Coroutine feedbackCoroutine;
        private Vector2 feedbackVisiblePosition;
        private bool feedbackPositionInitialized;
        private bool editingFeedbackVisible;
        private float previewScreenX;
        private bool hasDeferredPresentation;
        private DecorationActionPresentation deferredPresentation;
        private Vector2 deferredPreferredPoint;
        private Rect deferredSafeArea;
        private Rect? deferredAvoidScreenRect;
        private Coroutine deferredPresentationCoroutine;
        private bool canStore;
        private bool canConfirm;
        private bool terminalConsumed;
        private bool usesSurfaceFooterPresentation;
        private DecorationModeKind currentMode = DecorationModeKind.Furniture;
        private RectTransform instructionHost, instructionHud;
        private AnimalCafe.UI.Feedback.ValidationMessageView instructionReadiness;
        private bool persistentInstruction, instructionModalCovered, refreshingMobileLayout;
        private bool refreshingInstructionLayout, lastInstructionVisible;
        private Rect lastInstructionBounds;
        private readonly Vector3[] instructionCorners = new Vector3[4];
        public event Action InstructionPresentationChanged;
        public RectTransform VisibleInstructionRect => this != null && isActiveAndEnabled && persistentInstruction && !instructionModalCovered
            && feedbackRoot != null && feedbackRoot.gameObject.activeInHierarchy ? feedbackRoot : null;

        private void NotifyInstructionPresentationChanged()
        {
            if (this == null) { lastInstructionVisible = false; return; }
            var visible = VisibleInstructionRect != null;
            var bounds = default(Rect);
            if (visible)
            {
                feedbackRoot.GetWorldCorners(instructionCorners);
                bounds = Rect.MinMaxRect(instructionCorners[0].x, instructionCorners[0].y,
                    instructionCorners[2].x, instructionCorners[2].y);
            }
            if (visible == lastInstructionVisible && bounds == lastInstructionBounds) return;
            lastInstructionVisible = visible; lastInstructionBounds = bounds;
            InstructionPresentationChanged?.Invoke();
        }

        public void ConfigureInstructionPresentation(RectTransform host, RectTransform hud,
            AnimalCafe.UI.Feedback.ValidationMessageView readiness)
        {
            if (appearance == null || host == null || feedbackRoot == null) return;
            instructionHost = host; instructionHud = hud; instructionReadiness = readiness;
            if (feedbackRoot.parent != host) feedbackRoot.SetParent(host, false);
            RefreshInstructionLayout();
        }

        public void SetInstructionModalCovered(bool covered)
        {
            if (this == null) return;
            if (instructionModalCovered == covered) return;
            instructionModalCovered = covered;
            if (persistentInstruction && feedbackRoot != null && feedbackCanvasGroup != null)
            {
                var visible = isActiveAndEnabled && !covered;
                feedbackRoot.gameObject.SetActive(visible);
                feedbackCanvasGroup.alpha = visible ? 1 : 0;
            }
            NotifyInstructionPresentationChanged();
        }

        public void RefreshInstructionLayout()
        {
            if (this == null || !isActiveAndEnabled || appearance == null || !persistentInstruction || instructionHost == null || feedbackRoot == null || feedbackLabel == null || refreshingInstructionLayout) return;
            refreshingInstructionLayout = true;
            var metrics = AnimalCafe.UI.P8R.P8RMobileMetrics.For(this);
            var width = Mathf.Min(metrics.Units(520), instructionHost.rect.width - metrics.Units(16));
            var top = metrics.Units(8);
            foreach (var obstruction in new[] { instructionHud,
                instructionReadiness != null && instructionReadiness.IsVisible ? (RectTransform)instructionReadiness.transform : null })
            {
                if (obstruction == null || !obstruction.gameObject.activeInHierarchy) continue;
                top = Mathf.Max(top, instructionHost.rect.yMax - instructionHost.InverseTransformPoint(
                    obstruction.TransformPoint(new Vector2(0, obstruction.rect.yMin))).y);
            }
            feedbackLabel.fontSize = metrics.Units(14);
            feedbackLabel.textWrappingMode = TextWrappingModes.Normal; feedbackLabel.maxVisibleLines = 2;
            feedbackLabel.richText = false;
            var textHeight = feedbackLabel.GetPreferredValues(feedbackLabel.text, width - metrics.Units(52), Mathf.Infinity).y;
            var height = Mathf.Max(metrics.Units(36), textHeight + metrics.Units(12));
            feedbackRoot.anchorMin = feedbackRoot.anchorMax = feedbackRoot.pivot = new Vector2(.5f, 1);
            feedbackRoot.sizeDelta = new Vector2(width, height);
            feedbackVisiblePosition = new Vector2(0, -top - metrics.Units(8)); feedbackPositionInitialized = true;
            feedbackRoot.anchoredPosition = feedbackVisiblePosition;
            var copy = feedbackLabel.rectTransform;
            copy.anchorMin = Vector2.zero; copy.anchorMax = Vector2.one;
            copy.offsetMin = new Vector2(metrics.Units(40), metrics.Units(6));
            copy.offsetMax = new Vector2(-metrics.Units(12), -metrics.Units(6));
            if (feedbackStateShape != null && feedbackStateShape.transform is RectTransform state)
            {
                state.anchorMin = state.anchorMax = state.pivot = new Vector2(0, 1);
                state.anchoredPosition = new Vector2(metrics.Units(10), -metrics.Units(8));
                state.sizeDelta = Vector2.one * metrics.Units(20);
            }
            foreach (var graphic in feedbackRoot.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            feedbackCanvasGroup.blocksRaycasts = false; feedbackCanvasGroup.interactable = false;
            feedbackRoot.SetAsLastSibling(); feedbackRoot.gameObject.SetActive(!instructionModalCovered);
            feedbackCanvasGroup.alpha = instructionModalCovered ? 0 : 1;
            NotifyInstructionPresentationChanged();
            refreshingInstructionLayout = false;
        }

        private void RefreshMobilePresentation()
        {
            if (appearance == null || refreshingMobileLayout) return;
            refreshingMobileLayout = true;
            try
            {
                if (presentationRoot != null)
                {
                    if (usesSurfaceFooterPresentation) ApplyP8RSurfaceFooter(presentationRoot, currentMode);
                    else ApplyP8RFloatingPresentation(presentationRoot, currentMode, canStore);
                }
                RefreshInstructionLayout();
            }
            finally { refreshingMobileLayout = false; }
        }
        private void OnEnable()
        {
            AnimalCafe.UI.P8R.P8RMobileMetrics.Changed += RefreshMobilePresentation;
            RefreshMobilePresentation();
        }
        private void OnRectTransformDimensionsChange() => RefreshMobilePresentation();

        public event Action RotateRequested;
        public event Action UndoLastRequested;
        public event Action ApplyAllRequested;
        public event Action ConfirmRequested;
        public event Action CancelRequested;
        public event Action StoreRequested;

        public bool IsVisible { get; private set; }
        public bool HasOverflowActions => false;
        public string[] VisibleActionLabels { get; private set; } = Array.Empty<string>();
        public void AttachToHost(RectTransform host)
        {
            if (host == null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            if (transform.parent != host)
            {
                transform.SetParent(host, false);
            }

            if (transform is RectTransform rect)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.pivot = Vector2.one * 0.5f;
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = Vector2.zero;
                rect.localScale = Vector3.one;
            }
        }

        public void SetModeActions(DecorationModeKind mode, bool existing)
        {
            currentMode = mode;
            IsVisible = true;
            terminalConsumed = false;
            EnsureOwnListeners();
            var labels = mode == DecorationModeKind.Floor ? new[] { "Undo Last", "Rotate", "Apply All", "Cancel", "Confirm" } :
                mode == DecorationModeKind.Wall ? new[] { "Cancel", "Confirm" } :
                mode == DecorationModeKind.Furniture ? (existing ? new[] { "Store", "Cancel", "Rotate", "Confirm" } : new[] { "Cancel", "Rotate", "Confirm" }) :
                (existing ? new[] { "Store", "Cancel", "Confirm" } : new[] { "Cancel", "Confirm" });
            VisibleActionLabels = labels;
            Set(undoLastButton, Array.IndexOf(labels, "Undo Last") >= 0); Set(applyAllButton, Array.IndexOf(labels, "Apply All") >= 0);
            Set(storeButton, Array.IndexOf(labels, "Store") >= 0); Set(rotateButton, Array.IndexOf(labels, "Rotate") >= 0);
            Set(cancelButton, true); Set(confirmButton, true);
            ApplyModePresentation(mode, existing);
        }

        /// <summary>
        /// Applies the Task 7 Furniture-tab action presentation without controller wiring.
        /// 只设置 Task 7 Furniture tab 的 action 呈现，不连接业务 controller。
        /// </summary>
        public void SetCatalogueItemActions(DecorationCatalogueItemKind kind, bool existing)
        {
            if (kind != DecorationCatalogueItemKind.Furniture
                && kind != DecorationCatalogueItemKind.CashRegister
                && kind != DecorationCatalogueItemKind.CoffeeMachine
                && kind != DecorationCatalogueItemKind.PickUpPoint)
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind,
                    "Only Furniture-tab item kinds have a Task 7 action presentation.");
            }

            currentMode = DecorationModeKind.Furniture;
            IsVisible = true;
            terminalConsumed = false;
            EnsureOwnListeners();
            var isPickUp = kind == DecorationCatalogueItemKind.PickUpPoint;
            var labels = isPickUp
                ? (existing ? new[] { "Store", "Cancel", "Confirm" } : new[] { "Cancel", "Confirm" })
                : (existing ? new[] { "Store", "Cancel", "Rotate", "Confirm" } : new[] { "Cancel", "Rotate", "Confirm" });
            VisibleActionLabels = labels;
            Set(undoLastButton, false);
            Set(applyAllButton, false);
            Set(storeButton, Array.IndexOf(labels, "Store") >= 0);
            Set(rotateButton, Array.IndexOf(labels, "Rotate") >= 0);
            Set(cancelButton, true);
            Set(confirmButton, true);
            ApplyModePresentation(DecorationModeKind.Furniture, existing);
        }

        public void SetFloorUtilityActionsEnabled(bool enabled)
        {
            SetInteractable(undoLastButton, enabled);
            SetInteractable(rotateButton, enabled);
            SetInteractable(applyAllButton, enabled);
            RefreshP8RUtilityIcon(undoLastButton, "undo");
            RefreshP8RUtilityIcon(rotateButton, "rotate");
            RefreshP8RUtilityIcon(applyAllButton, "apply_all");
        }

        private static void Set(Button button, bool visible) { if (button != null) button.gameObject.SetActive(visible); }
        private static void SetInteractable(Button button, bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }

        public void SetPresentation(
            DecorationActionPresentation presentation,
            Vector2 preferredScreenPoint,
            Rect safeArea,
            Rect? avoidScreenRect = null)
        {
            // Keep an already pressed compact target under its pointer through sheet reflow.
            // 不改变pointer ownership；释放后仍走已有presentation刷新。
            if (appearance != null && (IsPressed(storeButton) || IsPressed(cancelButton)
                || IsPressed(rotateButton) || IsPressed(confirmButton)))
            {
                hasDeferredPresentation = true;
                deferredPresentation = presentation;
                deferredPreferredPoint = preferredScreenPoint;
                deferredSafeArea = safeArea;
                deferredAvoidScreenRect = avoidScreenRect;
                return;
            }
            hasDeferredPresentation = false;
            previewScreenX = preferredScreenPoint.x;
            if (editingFeedbackVisible) PositionEditingFeedback();
            if (usesSurfaceFooterPresentation)
            {
                return;
            }

            var showStore = presentation == DecorationActionPresentation.Existing && canStore;
            if (storeButton != null)
            {
                storeButton.gameObject.SetActive(showStore);
            }

            var rect = presentationRoot != null
                ? presentationRoot
                : transform as RectTransform;
            if (rect == null)
            {
                return;
            }

            ApplyCompactPresentation(rect, currentMode, showStore);

            var localPreferred = preferredScreenPoint;
            var localSafeArea = safeArea;
            var localAvoid = avoidScreenRect;
            if (rect.parent is RectTransform parentRect)
            {
                var canvas = rect.GetComponentInParent<Canvas>();
                var eventCamera = canvas != null
                    && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? canvas.rootCanvas.worldCamera
                    : null;
                if (TryScreenToAnchoredPoint(
                        rect, parentRect, eventCamera, preferredScreenPoint, out var anchoredPreferred)
                    && TryScreenToAnchoredPoint(
                        rect, parentRect, eventCamera, safeArea.min, out var anchoredMinimum)
                    && TryScreenToAnchoredPoint(
                        rect, parentRect, eventCamera, safeArea.max, out var anchoredMaximum))
                {
                    localPreferred = anchoredPreferred;
                    localSafeArea = Rect.MinMaxRect(
                        Mathf.Min(anchoredMinimum.x, anchoredMaximum.x),
                        Mathf.Min(anchoredMinimum.y, anchoredMaximum.y),
                        Mathf.Max(anchoredMinimum.x, anchoredMaximum.x),
                        Mathf.Max(anchoredMinimum.y, anchoredMaximum.y));
                    if (avoidScreenRect.HasValue
                        && TryScreenToAnchoredPoint(rect, parentRect, eventCamera, avoidScreenRect.Value.min, out var avoidMin)
                        && TryScreenToAnchoredPoint(rect, parentRect, eventCamera, avoidScreenRect.Value.max, out var avoidMax))
                        localAvoid = Rect.MinMaxRect(Mathf.Min(avoidMin.x, avoidMax.x), Mathf.Min(avoidMin.y, avoidMax.y),
                            Mathf.Max(avoidMin.x, avoidMax.x), Mathf.Max(avoidMin.y, avoidMax.y));
                }
            }

            var size = rect.rect.size;
            if (size.x <= 0f || size.y <= 0f)
            {
                size = rect.sizeDelta;
            }
            var point = new Vector2(
                ClampAxis(
                    localPreferred.x,
                    localSafeArea.xMin + size.x * rect.pivot.x,
                    localSafeArea.xMax - size.x * (1f - rect.pivot.x)),
                ClampAxis(
                    localPreferred.y,
                    localSafeArea.yMin + size.y * rect.pivot.y,
                    localSafeArea.yMax - size.y * (1f - rect.pivot.y)));
            if (appearance != null && localAvoid.HasValue)
                point = AvoidWorldPresentation(point, size, rect.pivot, localSafeArea, localAvoid.Value);
            rect.anchoredPosition = point;
        }

        private Vector2 AvoidWorldPresentation(Vector2 point, Vector2 size, Vector2 pivot, Rect safeArea, Rect obstacle)
        {
            var worldBounds = obstacle;
            var gap = AnimalCafe.UI.P8R.P8RMobileMetrics.For(this).Units(8);
            obstacle = Rect.MinMaxRect(obstacle.xMin - gap, obstacle.yMin - gap, obstacle.xMax + gap, obstacle.yMax + gap);
            var fromPivot = Vector2.Scale(size, pivot);
            if (!new Rect(point - fromPivot, size).Overlaps(obstacle)) return point;
            var oppositePivot = size - fromPivot;
            var candidates = new[] {
                new Vector2(point.x, obstacle.yMax + fromPivot.y),
                new Vector2(point.x, obstacle.yMin - oppositePivot.y),
                new Vector2(obstacle.xMin - oppositePivot.x, point.y),
                new Vector2(obstacle.xMax + fromPivot.x, point.y) };
            // Prefer above/below the complete support + sign; sides are only a fallback.
            // 整行先尝试物体上下方；候选必须同时避开世界标记与固定 UI。
            for (var pass = 0; pass < 2; pass++)
            {
                var best = point; var distance = float.PositiveInfinity;
                for (var index = pass * 2; index < pass * 2 + 2; index++)
                {
                    var candidate = candidates[index];
                    var row = new Rect(candidate - fromPivot, size);
                    if (row.xMin < safeArea.xMin - .01f || row.xMax > safeArea.xMax + .01f
                        || row.yMin < safeArea.yMin - .01f || row.yMax > safeArea.yMax + .01f || row.Overlaps(worldBounds)) continue;
                    var delta = (candidate - point).sqrMagnitude;
                    if (delta < distance) { best = candidate; distance = delta; }
                }
                if (!float.IsPositiveInfinity(distance)) return best;
            }
            // Do not escape the fixed UI safe region if no complete row can fit.
            return point;
        }

        private static bool IsPressed(Button button) => button != null
            && (button.GetComponent<DecorationPointerBoundaryEventHook>()?.HasActivePress ?? false);

        private void ApplyModePresentation(DecorationModeKind mode, bool existing)
        {
            usesSurfaceFooterPresentation = mode == DecorationModeKind.Floor
                || mode == DecorationModeKind.Wall;
            var panel = presentationRoot != null
                ? presentationRoot
                : transform as RectTransform;
            if (panel == null)
            {
                return;
            }

            panel.anchorMin = Vector2.one * 0.5f;
            panel.anchorMax = Vector2.one * 0.5f;
            panel.pivot = Vector2.one * 0.5f;
            panel.anchoredPosition = Vector2.zero;
            panel.localScale = Vector3.one;

            if (usesSurfaceFooterPresentation)
            {
                ApplySurfaceFooterPresentation(panel, mode);
                return;
            }

            ApplyCompactPresentation(panel, mode, existing);
        }

        private void ApplySurfaceFooterPresentation(RectTransform panel, DecorationModeKind mode)
        {
            if (appearance != null)
            {
                ApplyP8RSurfaceFooter(panel, mode);
                return;
            }
            var isFloor = mode == DecorationModeKind.Floor;
            panel.anchoredPosition = new Vector2(0f, isFloor ? FloorActionRowOffset : 0f);
            SetActionSibling(undoLastButton, 0);
            SetActionSibling(rotateButton, isFloor ? 1 : 0);
            SetActionSibling(applyAllButton, 2);
            SetActionSibling(cancelButton, isFloor ? 3 : 0);
            SetActionSibling(confirmButton, isFloor ? 4 : 1);

            ConfigureSurfaceButton(undoLastButton, isFloor ? "撤销上一步" : "Undo Last", SurfaceUtilityButtonWidth, false);
            ConfigureSurfaceButton(rotateButton, "Rotate", SurfaceUtilityButtonWidth, false);
            var applyWidth = isFloor ? 128f : SurfaceUtilityButtonWidth;
            ConfigureSurfaceButton(applyAllButton, isFloor ? "铺满整个房间" : "Apply All", applyWidth, false);
            ConfigureSurfaceButton(cancelButton, "Cancel", SurfacePrimaryButtonWidth, false);
            ConfigureSurfaceButton(confirmButton, isFloor ? "确认本次修改" : "Confirm", SurfacePrimaryButtonWidth, true);
            SetRotateIconVisible(false);

            var visibleButtonCount = isFloor ? 5 : 2;
            var width = isFloor
                ? SurfaceUtilityButtonWidth * 2f + applyWidth + SurfacePrimaryButtonWidth * 2f
                  + ActionSpacing * (visibleButtonCount - 1)
                : SurfacePrimaryButtonWidth * 2f + ActionSpacing;
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, SurfaceButtonHeight);
        }

        private void ApplyP8RSurfaceFooter(RectTransform panel, DecorationModeKind mode)
        {
            var isFloor = mode == DecorationModeKind.Floor;
            var buttons = isFloor ? new[] { undoLastButton, rotateButton, applyAllButton, cancelButton, confirmButton }
                : new[] { cancelButton, confirmButton };
            var width = transform.parent is RectTransform host && host.rect.width > 0 ? host.rect.width
                : AnimalCafe.UI.P8R.P8RMobileMetrics.For(this).Units(320);
            var layout = AnimalCafe.UI.P8R.P8RSurfaceFooterLayout.Measure(this, width, appearance,
                FindPrimaryLabel(cancelButton), isFloor);
            var horizontal = panel.GetComponent<HorizontalLayoutGroup>(); if (horizontal != null) horizontal.enabled = false;
            panel.anchorMin = panel.anchorMax = new Vector2(.5f, 0);
            panel.pivot = new Vector2(.5f, 0); panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(width, layout.Height);
            var metrics = AnimalCafe.UI.P8R.P8RMobileMetrics.For(this);
            for (var i = 0; i < buttons.Length; i++)
            {
                var button = buttons[i]; if (button == null) continue;
                var slot = isFloor ? i + 2 : i;
                SetActionSibling(button, i);
                ConfigureSurfaceButton(button, string.Empty, layout.Widths[slot], button == confirmButton);
                var rect = (RectTransform)button.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0); rect.pivot = Vector2.one * .5f;
                rect.anchoredPosition = layout.Centers[slot];
                rect.sizeDelta = new Vector2(layout.Widths[slot], layout.RowHeight);
                var label = FindPrimaryLabel(button);
                if (label != null) { label.fontSize = metrics.Units(14); label.color = AnimalCafe.UI.P8R.P8RAppearance.Cocoa; }
                if (isFloor && layout.CompactUtilityIcons && i < 3)
                {
                    var action = i == 0 ? "undo" : i == 1 ? "rotate" : "apply_all";
                    EnsureP8RUtilityIcon(button);
                    appearance.Button(button, action, iconOnly: true);
                    AnimalCafe.UI.P8R.P8RButtonLayout.IconButton(button);
                }
                if (isFloor)
                    AnimalCafe.UI.P8R.P8RButtonLayout.SurfaceButton(button, layout.CompactUtilityIcons && i < 3);
            }
        }

        private static Image EnsureP8RUtilityIcon(Button button)
        {
            if (button == null) return null;
            var iconTransform = button.transform.Find("Icon");
            if (iconTransform == null)
            {
                var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconObject.layer = button.gameObject.layer;
                iconObject.transform.SetParent(button.transform, false);
                iconTransform = iconObject.transform;
            }
            var icon = iconTransform.GetComponent<Image>() ?? iconTransform.gameObject.AddComponent<Image>();
            icon.raycastTarget = false;
            return icon;
        }

        private void RefreshP8RUtilityIcon(Button button, string action)
        {
            if (appearance == null || button == null) return;
            var icon = button.transform.Find("Icon")?.GetComponent<Image>();
            if (icon == null) return;
            appearance.Paint(icon, action + (button.interactable ? "_cocoa" : "_muted"), false);
            icon.raycastTarget = false;
        }

        private void ApplyReadableFloatingPresentation(
            RectTransform panel,
            DecorationModeKind mode,
            bool showStore)
        {
            SetFloorUtilityActionsEnabled(true);
            ConfigureSurfaceButton(storeButton, "Store", SurfaceUtilityButtonWidth, false);
            ConfigureSurfaceButton(rotateButton, "Rotate", SurfaceUtilityButtonWidth, false);
            ConfigureSurfaceButton(cancelButton, "Cancel", SurfacePrimaryButtonWidth, false);
            ConfigureSurfaceButton(confirmButton, "Confirm", SurfacePrimaryButtonWidth, true);

            var ordered = new System.Collections.Generic.List<Button>(4);
            if (showStore && storeButton != null && storeButton.gameObject.activeSelf)
            {
                ordered.Add(storeButton);
            }
            if (cancelButton != null && cancelButton.gameObject.activeSelf)
            {
                ordered.Add(cancelButton);
            }
            if (mode == DecorationModeKind.Furniture
                && rotateButton != null
                && rotateButton.gameObject.activeSelf)
            {
                ordered.Add(rotateButton);
            }
            if (confirmButton != null && confirmButton.gameObject.activeSelf)
            {
                ordered.Add(confirmButton);
            }

            for (var index = 0; index < ordered.Count; index++)
            {
                SetActionSibling(ordered[index], index);
            }

            var width = 0f;
            foreach (var button in ordered)
            {
                width += button == cancelButton || button == confirmButton
                    ? SurfacePrimaryButtonWidth
                    : SurfaceUtilityButtonWidth;
            }
            if (ordered.Count > 1)
            {
                width += ActionSpacing * (ordered.Count - 1);
            }
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, SurfaceButtonHeight);
        }

        private void ApplyCompactPresentation(
            RectTransform panel,
            DecorationModeKind mode,
            bool showStore)
        {
            if (appearance != null)
            {
                ApplyP8RFloatingPresentation(panel, mode, showStore);
                return;
            }
            SetFloorUtilityActionsEnabled(true);
            ConfigureCompactButton(storeButton, "□");
            ConfigureCompactButton(rotateButton, "R");
            ConfigureCompactButton(cancelButton, "×");
            ConfigureCompactButton(confirmButton, "✓");
            ConfigureCompactButton(undoLastButton, "Undo");
            ConfigureCompactButton(applyAllButton, "All");
            SetRotateIconVisible(mode == DecorationModeKind.Furniture);

            var ordered = new System.Collections.Generic.List<Button>(4);
            if (showStore && storeButton != null && storeButton.gameObject.activeSelf)
            {
                ordered.Add(storeButton);
            }
            if (cancelButton != null && cancelButton.gameObject.activeSelf)
            {
                ordered.Add(cancelButton);
            }
            if (mode == DecorationModeKind.Furniture
                && rotateButton != null
                && rotateButton.gameObject.activeSelf)
            {
                ordered.Add(rotateButton);
            }
            if (confirmButton != null && confirmButton.gameObject.activeSelf)
            {
                ordered.Add(confirmButton);
            }

            for (var index = 0; index < ordered.Count; index++)
            {
                SetActionSibling(ordered[index], index);
            }

            panel.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                ordered.Count * CompactButtonSize + Mathf.Max(0, ordered.Count - 1) * ActionSpacing);
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, CompactButtonSize);
        }

        private void ApplyP8RFloatingPresentation(RectTransform panel, DecorationModeKind mode, bool showStore)
        {
            SetFloorUtilityActionsEnabled(true);
            RefreshP8RButtons();
            var ordered = new System.Collections.Generic.List<Button>();
            if (showStore && storeButton != null && storeButton.gameObject.activeSelf) ordered.Add(storeButton);
            if (cancelButton != null && cancelButton.gameObject.activeSelf) ordered.Add(cancelButton);
            if (mode == DecorationModeKind.Furniture && rotateButton != null && rotateButton.gameObject.activeSelf)
                ordered.Add(rotateButton);
            if (confirmButton != null && confirmButton.gameObject.activeSelf) ordered.Add(confirmButton);
            // Smaller faces move inward while each separate 48-unit touch root stays fixed.
            // 只收紧可见外观；48-unit点击区保留原位置，不互相覆盖。
            var metrics = AnimalCafe.UI.P8R.P8RMobileMetrics.For(this);
            var size = metrics.Units(48);
            // Subpixel separation keeps touching roots disjoint after Canvas coordinate conversion.
            // 只保留1/64单位的浮点边界保护，不增加可见的大间距。
            var edgeGuard = metrics.Units(1f / 64f);
            // Keep a subpixel inset at the outermost faces in the four-action group.
            // 四按钮最外侧底板也保留微小内边距，避免坐标换算后越出点击区。
            var inwardStep = metrics.Units(6f - 1f / 64f);
            var horizontal = panel.GetComponent<HorizontalLayoutGroup>();
            if (horizontal != null)
            {
                horizontal.enabled = true; horizontal.spacing = edgeGuard;
                horizontal.childControlWidth = false; horizontal.childControlHeight = false;
                horizontal.childForceExpandWidth = false; horizontal.childForceExpandHeight = false;
            }
            panel.pivot = Vector2.one * .5f;
            for (var i = 0; i < ordered.Count; i++)
            {
                var button = ordered[i];
                SetActionSibling(button, i);
                ((RectTransform)button.transform).sizeDelta = Vector2.one * size;
                var faceOffset = ((ordered.Count - 1) * .5f - i) * inwardStep;
                AnimalCafe.UI.P8R.P8RButtonLayout.ActionFace(button, true, faceOffset);
                SetTooltipEnabled(button, false);
            }
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                ordered.Count * size + Mathf.Max(0, ordered.Count - 1) * edgeGuard);
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size);
        }

        private void RefreshP8RButtons()
        {
            if (appearance == null) return;
            appearance.Button(storeButton, "store", "destructive", true);
            appearance.Button(cancelButton, "cancel", "secondary", true);
            appearance.Button(rotateButton, "rotate", "secondary", true);
            appearance.Button(confirmButton, "confirm", "primary", true);
        }

        private void ConfigureSurfaceButton(
            Button button,
            string label,
            float width,
            bool primary)
        {
            if (button == null)
            {
                return;
            }

            var presentationIcon = button.transform.Find("Icon");
            if (presentationIcon != null) presentationIcon.gameObject.SetActive(false);

            var rect = button.transform as RectTransform;
            if (rect != null)
            {
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, SurfaceButtonHeight);
            }
            if (button.image != null && appearance == null)
            {
                var normalColor = primary
                    ? new Color(0.28f, 0.43f, 0.31f, 1f)
                    : new Color(1f, 0.91f, 0.72f, 1f);
                button.image.color = Color.white;
                var colors = button.colors;
                colors.normalColor = normalColor;
                colors.highlightedColor = primary
                    ? new Color(0.33f, 0.49f, 0.36f, 1f)
                    : new Color(1f, 0.94f, 0.80f, 1f);
                colors.pressedColor = primary
                    ? new Color(0.22f, 0.36f, 0.26f, 1f)
                    : new Color(0.91f, 0.80f, 0.61f, 1f);
                colors.selectedColor = colors.highlightedColor;
                colors.disabledColor = new Color(0.55f, 0.54f, 0.50f, 1f);
                colors.colorMultiplier = 1f;
                colors.fadeDuration = 0.1f;
                button.colors = colors;
            }

            if (appearance != null)
            {
                AnimalCafe.UI.P8R.P8RButtonLayout.ActionFace(button, false);
                var action = button == undoLastButton ? "undo" : button == applyAllButton ? "apply_all"
                    : button == rotateButton ? "rotate" : button == confirmButton ? "confirm" : "cancel";
                appearance.Button(button, action, primary ? "primary" : "secondary");
                if (presentationIcon != null) presentationIcon.gameObject.SetActive(false);
            }
            var text = FindPrimaryLabel(button);
            if (text != null)
            {
                StretchPrimaryLabel(text);
                text.gameObject.SetActive(true);
                text.text = appearance != null ? appearance.Text(button == undoLastButton ? "action.undo" : button == applyAllButton ? "action.apply_all"
                    : button == rotateButton ? "action.rotate" : button == confirmButton ? "action.apply" : "action.cancel") : label;
                text.fontSize = appearance != null
                    ? AnimalCafe.UI.P8R.P8RMobileMetrics.For(button).Units(14)
                    : 16f;
                text.enableWordWrapping = false;
                text.overflowMode = TextOverflowModes.Truncate;
                text.color = primary
                    ? new Color(1f, 0.97f, 0.90f, 1f)
                    : new Color(0.22f, 0.16f, 0.11f, 1f);
            }
            SetTooltipVisible(button, false);
        }

        private static void ConfigureCompactButton(Button button, string label)
        {
            if (button == null)
            {
                return;
            }

            var rect = button.transform as RectTransform;
            if (rect != null)
            {
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, CompactButtonSize);
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, CompactButtonSize);
            }
            if (button.image != null)
            {
                var normalColor = new Color(0.28f, 0.43f, 0.31f, 1f);
                button.image.color = Color.white;
                var colors = button.colors;
                colors.normalColor = normalColor;
                colors.highlightedColor = new Color(0.33f, 0.49f, 0.36f, 1f);
                colors.pressedColor = new Color(0.22f, 0.36f, 0.26f, 1f);
                colors.selectedColor = colors.highlightedColor;
                colors.disabledColor = new Color(0.55f, 0.54f, 0.50f, 1f);
                colors.colorMultiplier = 1f;
                colors.fadeDuration = 0.1f;
                button.colors = colors;
            }

            var text = FindPrimaryLabel(button);
            if (text != null)
            {
                text.text = label;
                text.fontSize = 14f;
                text.enableWordWrapping = false;
                text.overflowMode = TextOverflowModes.Truncate;
                text.color = new Color(1f, 0.97f, 0.90f, 1f);
            }
            SetTooltipEnabled(button, true);
        }

        private static TMP_Text FindPrimaryLabel(Button button)
        {
            return button != null
                ? button.transform.Find("Label")?.GetComponent<TMP_Text>()
                : null;
        }

        private void SetRotateIconVisible(bool compactFurniture)
        {
            var hasIcon = rotateIcon != null;
            if (rotateIcon != null)
            {
                rotateIcon.gameObject.SetActive(compactFurniture);
                rotateIcon.raycastTarget = false;
            }

            var label = FindPrimaryLabel(rotateButton);
            if (label != null)
            {
                label.gameObject.SetActive(!compactFurniture || !hasIcon);
                if (compactFurniture && !hasIcon)
                {
                    label.text = "R";
                }
            }
        }

        private static void StretchPrimaryLabel(TMP_Text text)
        {
            if (text == null || text.rectTransform == null)
            {
                return;
            }

            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private static void SetTooltipVisible(Button button, bool visible)
        {
            SetTooltipEnabled(button, visible);
            var tooltip = button != null ? button.transform.Find("Tooltip") : null;
            if (tooltip != null)
            {
                tooltip.gameObject.SetActive(visible);
            }
        }

        private static void SetTooltipEnabled(Button button, bool enabled)
        {
            if (button == null)
            {
                return;
            }

            foreach (var hook in button.GetComponentsInChildren<DecorationPointerBoundaryEventHook>(true))
            {
                hook.SetTooltipEnabled(enabled);
            }
        }

        private static bool TryScreenToAnchoredPoint(
            RectTransform rect,
            RectTransform parentRect,
            UnityEngine.Camera eventCamera,
            Vector2 screenPoint,
            out Vector2 anchoredPoint)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect, screenPoint, eventCamera, out var localPoint))
            {
                anchoredPoint = default;
                return false;
            }

            var anchor = new Vector2(
                Mathf.Lerp(rect.anchorMin.x, rect.anchorMax.x, rect.pivot.x),
                Mathf.Lerp(rect.anchorMin.y, rect.anchorMax.y, rect.pivot.y));
            var anchorReference = Vector2.Scale(parentRect.rect.size, anchor)
                + parentRect.rect.min;
            anchoredPoint = localPoint - anchorReference;
            return true;
        }

        private static float ClampAxis(float value, float minimum, float maximum)
        {
            return minimum <= maximum
                ? Mathf.Clamp(value, minimum, maximum)
                : (minimum + maximum) * 0.5f;
        }

        public void Configure(
            IUiPointerOwnershipRegistrar pointerBoundary,
            UiTransitionRunner runner)
        {
            if (pointerBoundary == null)
            {
                throw new ArgumentNullException(nameof(pointerBoundary));
            }

            transitionRunner = runner ?? throw new ArgumentNullException(nameof(runner));
            EnsureOwnListeners();
            foreach (var hook in GetComponentsInChildren<DecorationPointerBoundaryEventHook>(true))
            {
                hook.Configure(pointerBoundary);
            }
        }

        public void Show(
            bool canStore,
            bool canConfirm,
            PlacementFeedbackKey feedback)
        {
            transform.SetAsLastSibling();
            EnsureOwnListeners();
            this.canStore = canStore;
            this.canConfirm = canConfirm;
            terminalConsumed = false;
            IsVisible = true;
            if (presentationRoot != null)
            {
                presentationRoot.gameObject.SetActive(true);
            }
            if (storeButton != null)
            {
                storeButton.gameObject.SetActive(canStore);
                storeButton.interactable = canStore;
            }

            if (confirmButton != null)
            {
                confirmButton.interactable = canConfirm;
            }

            // Paint the current enabled state before assigning the final face/icon offsets.
            // 先确定可用状态，再统一刷新外观，避免普通IconButton重置收紧偏移。
            if (!usesSurfaceFooterPresentation && presentationRoot != null)
            {
                ApplyCompactPresentation(presentationRoot, currentMode, canStore);
            }

            PresentFeedback(feedback);
            SetInteraction(true);
            BeginTransition(visible: true);
        }

        public void ShowInstruction(PlacementFeedbackKey feedback)
        {
            transform.SetAsLastSibling();
            terminalConsumed = false;
            IsVisible = true;
            if (presentationRoot != null)
            {
                presentationRoot.gameObject.SetActive(false);
            }
            else
            {
                Set(storeButton, false);
                Set(undoLastButton, false);
                Set(applyAllButton, false);
                Set(rotateButton, false);
                Set(cancelButton, false);
                Set(confirmButton, false);
            }

            PresentPersistentFeedback(feedback);
            SetInteraction(true);
            BeginTransition(visible: true);
        }

        public void Hide()
        {
            IsVisible = false;
            if (this == null) return;
            CancelDeferredPresentation();
            HideFeedbackImmediately();
            SetInteraction(false);
            BeginTransition(visible: false);
        }

        /// <summary>Preview feedback remains visible until its owner updates or ends editing.</summary>
        public void ShowEditingFeedback(string message, bool isInvalid)
        {
            if (appearance != null)
            {
                // P8R uses the footprint and action states; keep diagnostics without another notice panel.
                // 保留诊断文字供检查，不再额外显示预览说明框。
                HideFeedbackImmediately();
                if (feedbackLabel != null) feedbackLabel.text = message;
                return;
            }
            // Reuse the existing persistent instruction path; unrelated Toasts keep their duration.
            // 复用持久提示，不改变其他 Toast 的停留时间。
            PresentPersistentFeedback(PlacementFeedbackKey.Blocked);
            editingFeedbackVisible = true;
            if (feedbackLabel != null)
            {
                feedbackLabel.text = message;
                feedbackLabel.richText = false;
                feedbackLabel.textWrappingMode = TextWrappingModes.Normal;
                feedbackLabel.fontSize = appearance != null ? 32f : 24f;
                feedbackLabel.color = new Color(.18f, .15f, .12f);
                feedbackLabel.alignment = TextAlignmentOptions.MidlineLeft;
                if (feedbackRoot != null && feedbackLabel.rectTransform != feedbackRoot)
                {
                    var parent = feedbackRoot.parent as RectTransform;
                    var wideLandscape = appearance != null && parent != null && Screen.width > Screen.height * 1.8f;
                    if (wideLandscape) feedbackLabel.fontSize = 28f;
                    var width = parent != null ? Mathf.Min(720f, Mathf.Max(240f, parent.rect.width - 40f)) : 720f;
                    if (wideLandscape) width = Mathf.Min(660f, Mathf.Max(420f, parent.rect.width * .28f));
                    feedbackRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                    var height = Mathf.Max(64f, feedbackLabel.GetPreferredValues(message,
                        width - 60f, Mathf.Infinity).y);
                    feedbackLabel.rectTransform.anchorMin = new Vector2(0, .5f);
                    feedbackLabel.rectTransform.anchorMax = new Vector2(1, .5f);
                    feedbackLabel.rectTransform.offsetMin = new Vector2(40f, -height * .5f);
                    feedbackLabel.rectTransform.offsetMax = new Vector2(-20f, height * .5f);
                    feedbackLabel.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
                    feedbackRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height + 24f);
                    // Keep current editing beside the lower catalogue controls, away from top readiness.
                    // 只移动本次编辑提示；Hide 会恢复既有 instruction/Toast 的 authored position。
                    PositionEditingFeedback();
                }
            }
            feedbackStateShape?.SetActive(isInvalid);
        }

        private void PositionEditingFeedback()
        {
            if (feedbackRoot == null || !(feedbackRoot.parent is RectTransform parent)) return;
            var x = parent.rect.center.x;
            if (appearance != null && Screen.width > Screen.height * 1.8f)
            {
                // Use the empty side opposite the live preview, updated by existing presentation events.
                // 跟随已有 preview 更新选择另一侧，不新增 Update/polling 或改变相机。
                x = previewScreenX >= Screen.width * .5f
                    ? parent.rect.xMin + 24f + feedbackRoot.rect.width * feedbackRoot.pivot.x
                    : parent.rect.xMax - 24f - feedbackRoot.rect.width * (1f - feedbackRoot.pivot.x);
            }
            feedbackRoot.position = parent.TransformPoint(new Vector3(x,
                parent.rect.yMin + 300f + feedbackRoot.rect.height * feedbackRoot.pivot.y, 0));
        }

        private void PresentFeedback(PlacementFeedbackKey feedback)
        {
            var text = GetFeedbackText(feedback);
            if (feedbackLabel != null)
            {
                feedbackLabel.text = text;
            }

            feedbackStateShape?.SetActive(feedback != PlacementFeedbackKey.None);
            if (feedbackRoot == null || feedbackCanvasGroup == null)
            {
                return;
            }

            if (feedbackCoroutine != null)
            {
                StopCoroutine(feedbackCoroutine);
                feedbackCoroutine = null;
            }

            if (feedback == PlacementFeedbackKey.None)
            {
                HideFeedbackImmediately();
                return;
            }

            if (!feedbackPositionInitialized)
            {
                feedbackVisiblePosition = feedbackRoot.anchoredPosition;
                feedbackPositionInitialized = true;
            }
            feedbackCoroutine = StartCoroutine(ShowFeedbackToast());
        }

        private void PresentPersistentFeedback(PlacementFeedbackKey feedback)
        {
            persistentInstruction = feedback != PlacementFeedbackKey.None;
            if (feedbackCoroutine != null)
            {
                StopCoroutine(feedbackCoroutine);
                feedbackCoroutine = null;
            }

            var text = GetFeedbackText(feedback);
            if (feedbackLabel != null)
            {
                feedbackLabel.text = text;
            }
            feedbackStateShape?.SetActive(feedback != PlacementFeedbackKey.None);
            if (feedbackRoot == null || feedbackCanvasGroup == null)
            {
                return;
            }

            if (!feedbackPositionInitialized)
            {
                feedbackVisiblePosition = feedbackRoot.anchoredPosition;
                feedbackPositionInitialized = true;
            }
            feedbackRoot.anchoredPosition = feedbackVisiblePosition;
            var visible = feedback != PlacementFeedbackKey.None
                && (instructionHost == null || isActiveAndEnabled && !instructionModalCovered);
            feedbackRoot.gameObject.SetActive(visible);
            feedbackCanvasGroup.alpha = visible ? 1f : 0f;
            feedbackCanvasGroup.blocksRaycasts = false;
            feedbackCanvasGroup.interactable = false;
            RefreshInstructionLayout();
        }

        private IEnumerator ShowFeedbackToast()
        {
            feedbackRoot.gameObject.SetActive(true);
            feedbackCanvasGroup.blocksRaycasts = false;
            feedbackCanvasGroup.interactable = false;
            var hiddenPosition = feedbackVisiblePosition + Vector2.up * ToastHiddenOffset;
            for (var elapsed = 0f; elapsed < ToastTransitionDuration;
                 elapsed += Time.unscaledDeltaTime)
            {
                var progress = Mathf.Clamp01(elapsed / ToastTransitionDuration);
                feedbackRoot.anchoredPosition = Vector2.Lerp(hiddenPosition, feedbackVisiblePosition, progress);
                feedbackCanvasGroup.alpha = progress;
                yield return null;
            }

            feedbackRoot.anchoredPosition = feedbackVisiblePosition;
            feedbackCanvasGroup.alpha = 1f;
            var stayElapsed = 0f;
            while (stayElapsed < ToastStayDuration)
            {
                stayElapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            feedbackCoroutine = null;
            HideFeedbackImmediately();
        }

        private void HideFeedbackImmediately()
        {
            persistentInstruction = false;
            editingFeedbackVisible = false;
            if (feedbackCoroutine != null)
            {
                StopCoroutine(feedbackCoroutine);
                feedbackCoroutine = null;
            }

            if (feedbackCanvasGroup != null)
            {
                feedbackCanvasGroup.alpha = 0f;
                feedbackCanvasGroup.blocksRaycasts = false;
                feedbackCanvasGroup.interactable = false;
            }

            if (feedbackRoot != null)
            {
                if (feedbackPositionInitialized)
                {
                    feedbackRoot.anchoredPosition = feedbackVisiblePosition;
                }
                feedbackRoot.gameObject.SetActive(false);
            }
            NotifyInstructionPresentationChanged();
        }

        private void HandleRotate()
        {
            if (IsEligible() && !terminalConsumed)
            {
                RotateRequested?.Invoke();
            }
        }
        private void HandleUndoLast() { if (IsEligible() && !terminalConsumed) UndoLastRequested?.Invoke(); }
        private void HandleApplyAll() { if (IsEligible() && !terminalConsumed) ApplyAllRequested?.Invoke(); }

        private void HandleConfirm()
        {
            if (IsEligible() && canConfirm && !terminalConsumed)
            {
                terminalConsumed = true;
                ConfirmRequested?.Invoke();
            }
        }

        private void HandleCancel()
        {
            if (IsEligible() && !terminalConsumed)
            {
                terminalConsumed = true;
                CancelRequested?.Invoke();
            }
        }

        private void HandleStore()
        {
            if (IsEligible() && canStore && !terminalConsumed)
            {
                terminalConsumed = true;
                StoreRequested?.Invoke();
            }
        }

        private bool IsEligible()
        {
            return IsVisible && isActiveAndEnabled && gameObject.activeInHierarchy;
        }

        private string GetFeedbackText(PlacementFeedbackKey feedback)
        {
            if (appearance != null) return feedback == PlacementFeedbackKey.None ? string.Empty : appearance.Text("feedback." + feedback);
            switch (feedback)
            {
                case PlacementFeedbackKey.None:
                    return string.Empty;
                case PlacementFeedbackKey.Occupied:
                    return "Space already occupied";
                case PlacementFeedbackKey.OutsideUnlockedArea:
                    return "Outside decoration area";
                case PlacementFeedbackKey.Locked:
                    return "Area not unlocked";
                case PlacementFeedbackKey.Blocked:
                    return "Furniture cannot be placed here";
                case PlacementFeedbackKey.EntranceClearance:
                    return "Keep the entrance clear";
                case PlacementFeedbackKey.UnsupportedSurface:
                    return "Furniture cannot stand here";
                case PlacementFeedbackKey.MissingInstance:
                    return "Furniture changed. Select it again.";
                case PlacementFeedbackKey.WallOverlap:
                    return "Wall space already occupied";
                case PlacementFeedbackKey.WallOutOfBounds:
                    return "Outside wall area";
                case PlacementFeedbackKey.WallCrossCorner:
                    return "Place the item fully on one wall";
                case PlacementFeedbackKey.WallSurfaceMissing:
                    return "Wall surface unavailable";
                case PlacementFeedbackKey.SelectWallTarget:
                    return "Select a wall to edit";
                case PlacementFeedbackKey.SelectFloorGridTarget:
                    return "Select a floor grid to edit";
                case PlacementFeedbackKey.NoValidInteractionAnchor:
                    return "Pick-up needs a free adjacent cell";
                default:
                    return string.Empty;
            }
        }

        private void EnsureOwnListeners()
        {
            ReplaceListener(storeButton, HandleStore);
            ReplaceListener(undoLastButton, HandleUndoLast);
            ReplaceListener(applyAllButton, HandleApplyAll);
            ReplaceListener(rotateButton, HandleRotate);
            ReplaceListener(cancelButton, HandleCancel);
            ReplaceListener(confirmButton, HandleConfirm);
            foreach (var hook in GetComponentsInChildren<DecorationPointerBoundaryEventHook>(true))
            {
                hook.PresentationPressChanged -= HandlePresentationPressChanged;
                hook.PresentationPressChanged += HandlePresentationPressChanged;
            }
        }

        private void HandlePresentationPressChanged()
        {
            if (this == null || !hasDeferredPresentation || !IsVisible || !isActiveAndEnabled
                || IsPressed(storeButton) || IsPressed(cancelButton) || IsPressed(rotateButton) || IsPressed(confirmButton)) return;
            if (deferredPresentationCoroutine == null) deferredPresentationCoroutine = StartCoroutine(FlushDeferredPresentation());
        }

        private IEnumerator FlushDeferredPresentation()
        {
            yield return null; // Complete the current UI click before moving its target.
            deferredPresentationCoroutine = null;
            if (hasDeferredPresentation && IsVisible && isActiveAndEnabled)
                SetPresentation(deferredPresentation, deferredPreferredPoint, deferredSafeArea, deferredAvoidScreenRect);
        }

        private void CancelDeferredPresentation()
        {
            hasDeferredPresentation = false;
            // Scene teardown may call Hide after this native component was destroyed.
            // Scene 卸载允许旧 owner 完成 managed 清理，不再访问已销毁的 Unity 对象。
            if (this == null)
            {
                deferredPresentationCoroutine = null;
                return;
            }
            if (deferredPresentationCoroutine != null) StopCoroutine(deferredPresentationCoroutine);
            deferredPresentationCoroutine = null;
            foreach (var hook in GetComponentsInChildren<DecorationPointerBoundaryEventHook>(true)) hook.ClearPresentationPresses();
        }

        private void OnDisable()
        {
            AnimalCafe.UI.P8R.P8RMobileMetrics.Changed -= RefreshMobilePresentation;
            CancelDeferredPresentation();
            if (instructionHost != null && feedbackRoot != null)
            {
                feedbackRoot.gameObject.SetActive(false);
                if (feedbackCanvasGroup != null) feedbackCanvasGroup.alpha = 0;
            }
            NotifyInstructionPresentationChanged();
        }

        private static void ReplaceListener(Button target, UnityEngine.Events.UnityAction action)
        {
            if (target == null)
            {
                return;
            }

            target.onClick.RemoveListener(action);
            target.onClick.AddListener(action);
        }

        private static void SetActionSibling(Button button, int index)
        {
            if (button != null)
            {
                button.transform.SetSiblingIndex(index);
            }
        }

        private void SetInteraction(bool enabled)
        {
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.blocksRaycasts = enabled;
            canvasGroup.interactable = enabled;
        }

        private void BeginTransition(bool visible)
        {
            if (this == null) return;
            if (canvasGroup == null || transitionRunner == null || !isActiveAndEnabled)
            {
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = visible ? 1f : 0f;
                }

                return;
            }

            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
            }

            transitionCoroutine = StartCoroutine(RunTransition(visible));
        }

        private IEnumerator RunTransition(bool visible)
        {
            yield return transitionRunner.Run(canvasGroup, visible, TransitionDuration);
            transitionCoroutine = null;
        }

        private void OnDestroy()
        {
            InstructionPresentationChanged = null;
            IsVisible = false;
            SetInteraction(false);
            CancelDeferredPresentation();
            foreach (var hook in GetComponentsInChildren<DecorationPointerBoundaryEventHook>(true)) hook.PresentationPressChanged -= HandlePresentationPressChanged;
            HideFeedbackImmediately();
            storeButton?.onClick.RemoveListener(HandleStore);
            undoLastButton?.onClick.RemoveListener(HandleUndoLast);
            applyAllButton?.onClick.RemoveListener(HandleApplyAll);
            rotateButton?.onClick.RemoveListener(HandleRotate);
            cancelButton?.onClick.RemoveListener(HandleCancel);
            confirmButton?.onClick.RemoveListener(HandleConfirm);
            if (instructionHost != null && feedbackRoot != null) Destroy(feedbackRoot.gameObject);
        }
    }

}
