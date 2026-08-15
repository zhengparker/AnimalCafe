using System;
using System.IO;
using System.Linq;
using AnimalCafe.Diagnostics;
using AnimalCafe.Input;
using AnimalCafe.Interaction;
using AnimalCafe.UI.Components;
using AnimalCafe.UI.Feedback;
using AnimalCafe.UI.Foundation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AnimalCafe.EditorTools.Phase5
{
    /// <summary>
    /// Builds the isolated Phase 5 UI validation scene through Unity Editor APIs.
    /// 通过 Unity Editor API 构建隔离的 Phase 5 UI validation scene。
    /// </summary>
    public static class Phase5UiFoundationSceneSetup
    {
        private const int RecipeVersion = 34;
        public const string ScenePath = Phase5UiAssetPaths.ValidationScenePath;

        [MenuItem("AnimalCafe/Phase 5/Build UI Foundation Validation Scene")]
        public static void BuildSceneFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) BuildScene();
        }

        public static void BuildScene()
        {
            Phase5UiAssetBuilder.BuildAll();
            EditorBuildSettings.scenes = EditorBuildSettings.scenes
                .Where(entry => entry.path != ScenePath).ToArray();
            EnsureFolder(Path.GetDirectoryName(ScenePath)?.Replace('\\', '/'));

            if (HasCurrentRecipe()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Phase5UiFoundation";
            var root = new GameObject("Phase5UiFoundationRoot");
            root.AddComponent<Phase5UiFoundationSceneMarker>().Configure(RecipeVersion);
            var camera = CreateWorldFixtures(root.transform);
            var uiRoot = InstantiatePrefab(Phase5UiAssetPaths.UiRootPrefabPath, root.transform, "UI Root");
            uiRoot.AddComponent<UiGraphicRegistration>();
            CreateEventSystem(uiRoot.transform);
            var sceneInteraction = CreateSceneInteraction(root.transform, camera);
            CreateUiFixtures(uiRoot.transform, root.transform, camera, sceneInteraction.input, sceneInteraction.controller);

            var theme = AssetDatabase.LoadAssetAtPath<AnimalCafeUiTheme>(Phase5UiAssetPaths.ThemePath);
            var report = Phase5UiFoundationValidator.Validate(scene, theme);
            if (!report.IsValid)
            {
                throw new InvalidOperationException("Phase 5 UI scene validation failed: " +
                    string.Join(" | ", report.Issues));
            }

            if (!EditorSceneManager.SaveScene(scene, ScenePath, false))
                throw new InvalidOperationException("Unity could not save Phase 5 UI validation scene.");
        }

        private static UnityEngine.Camera CreateWorldFixtures(Transform parent)
        {
            var camera = new GameObject("Main Camera", typeof(UnityEngine.Camera));
            camera.transform.SetParent(parent, false);
            camera.transform.position = new Vector3(0f, 4f, -8f);
            camera.transform.rotation = Quaternion.Euler(20f, 0f, 0f);

            var coffeeMachine = GameObject.CreatePrimitive(PrimitiveType.Cube);
            coffeeMachine.name = "Selectable Coffee Machine";
            coffeeMachine.transform.SetParent(parent, false);
            // Keep the world-selection fixture in the left reserved review area so
            // the visual-only Component Gallery does not cover an otherwise clear tap.
            coffeeMachine.transform.localPosition = new Vector3(-1.7f, 0.5f, 0f);
            coffeeMachine.transform.localScale = new Vector3(1.5f, 1f, 1f);
            coffeeMachine.AddComponent<ColorSelectable>().Configure(
                coffeeMachine.GetComponent<Renderer>());

            var mover = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mover.name = "Scaled Time Mover";
            mover.transform.SetParent(parent, false);
            mover.transform.localPosition = new Vector3(-2f, 3f, -1f);
            mover.AddComponent<ManualReviewPingPongMover>().Configure(
                new Vector3(-2f, 3f, -1f), new Vector3(2f, 3f, -1f), 1f);
            return camera.GetComponent<UnityEngine.Camera>();
        }

        private static (MouseCameraInput input, SceneInteractionController controller) CreateSceneInteraction(
            Transform parent,
            UnityEngine.Camera camera)
        {
            var controllerObject = new GameObject("Scene Interaction Controller");
            controllerObject.transform.SetParent(parent, false);
            var input = controllerObject.AddComponent<MouseCameraInput>();
            var controller = controllerObject.AddComponent<SceneInteractionController>();
            controller.Configure(
                camera,
                input,
                new UiPointerBoundary());
            return (input, controller);
        }

        private static void CreateEventSystem(Transform parent)
        {
            var eventSystem = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            eventSystem.transform.SetParent(parent, false);
            var inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            var actions = EnsureInputActions();
            inputModule.actionsAsset = actions;
            inputModule.point = CreateReference(actions, "UI", "Point");
            inputModule.leftClick = CreateReference(actions, "UI", "Click");
            inputModule.scrollWheel = CreateReference(actions, "UI", "ScrollWheel");
            inputModule.submit = CreateReference(actions, "UI", "Submit");
            inputModule.cancel = CreateReference(actions, "UI", "Cancel");
        }

        private static InputActionAsset EnsureInputActions()
        {
            var existing = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                Phase5UiAssetPaths.ValidationInputActionsPath);
            if (existing != null) return existing;
            var actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var ui = actions.AddActionMap("UI");
            ui.AddAction("Point", InputActionType.PassThrough).AddBinding("<Pointer>/position");
            ui.AddAction("Click", InputActionType.PassThrough).AddBinding("<Pointer>/press");
            ui.AddAction("ScrollWheel", InputActionType.PassThrough).AddBinding("<Pointer>/scroll");
            ui.AddAction("Submit", InputActionType.Button).AddBinding("<Keyboard>/enter");
            ui.AddAction("Cancel", InputActionType.Button).AddBinding("<Keyboard>/escape");
            var absolutePath = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                Phase5UiAssetPaths.ValidationInputActionsPath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(absolutePath))
            {
                AssetDatabase.DeleteAsset(Phase5UiAssetPaths.ValidationInputActionsPath);
                foreach (var actionName in new[] { "Point", "Click", "ScrollWheel", "Submit", "Cancel" })
                    AssetDatabase.DeleteAsset(InputReferencePath(actionName));
            }
            File.WriteAllText(absolutePath, actions.ToJson());
            UnityEngine.Object.DestroyImmediate(actions);
            AssetDatabase.ImportAsset(
                Phase5UiAssetPaths.ValidationInputActionsPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                Phase5UiAssetPaths.ValidationInputActionsPath)
                ?? throw new InvalidOperationException("Phase 5 UI Input Actions import failed.");
        }

        private static InputActionReference CreateReference(
            InputActionAsset actions,
            string mapName,
            string actionName)
        {
            var action = actions.FindActionMap(mapName).FindAction(actionName);
            var path = InputReferencePath(actionName);
            var reference = AssetDatabase.LoadAssetAtPath<InputActionReference>(path);
            if (reference != null) return reference;
            reference = InputActionReference.Create(action);
            AssetDatabase.CreateAsset(reference, path);
            return reference;
        }

        private static string InputReferencePath(string actionName) =>
            actionName switch
            {
                "Point" => Phase5UiAssetPaths.ValidationPointReferencePath,
                "Click" => Phase5UiAssetPaths.ValidationClickReferencePath,
                "ScrollWheel" => Phase5UiAssetPaths.ValidationScrollWheelReferencePath,
                "Submit" => Phase5UiAssetPaths.ValidationSubmitReferencePath,
                "Cancel" => Phase5UiAssetPaths.ValidationCancelReferencePath,
                _ => throw new ArgumentOutOfRangeException(nameof(actionName), actionName, null)
            };

        private static bool HasCurrentRecipe()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null
                || Phase5UiAssetPaths.ValidationInputReferencePaths.Any(path =>
                    AssetDatabase.LoadAssetAtPath<InputActionReference>(path) == null))
                return false;

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var marker = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Phase5UiFoundationSceneMarker>(true))
                .SingleOrDefault();
            if (marker == null || marker.RecipeVersion != RecipeVersion) return false;
            var theme = AssetDatabase.LoadAssetAtPath<AnimalCafeUiTheme>(Phase5UiAssetPaths.ThemePath);
            return Phase5UiFoundationValidator.Validate(scene, theme).IsValid;
        }

        private static void CreateUiFixtures(
            Transform uiRoot,
            Transform sceneRoot,
            UnityEngine.Camera camera,
            MouseCameraInput sceneInput,
            SceneInteractionController sceneInteraction)
        {
            var theme = AssetDatabase.LoadAssetAtPath<AnimalCafeUiTheme>(Phase5UiAssetPaths.ThemePath)
                ?? throw new InvalidOperationException("Phase 5 UI theme is missing.");
            var hud = Find(uiRoot, "HUD Canvas/HUD Layer");
            var panel = Find(uiRoot, "Screen Canvas/Panel Layer");
            var modalLayer = Find(uiRoot, "Screen Canvas/Modal Layer");
            var toast = Find(uiRoot, "Toast Canvas/Toast Layer");
            Stretch((RectTransform)hud);
            Stretch((RectTransform)panel);
            Stretch((RectTransform)modalLayer);
            Stretch((RectTransform)toast);

            var safeArea = InstantiatePrefab(Phase5UiAssetPaths.SafeAreaPrefabPath, hud, "Safe Area");
            Stretch(safeArea.GetComponent<RectTransform>());
            var pages = CreateReviewPages(panel, theme);
            var responsiveCard = CreateResponsiveInfoCard(pages.responsive.transform);
            var gallery = CreateUiObject("Component Gallery", pages.buttons.transform);
            gallery.GetComponent<RectTransform>().sizeDelta = new Vector2(900f, 880f);
            CreateButtonGallery(gallery.transform, theme);
            var longLabel = AddLabel(
                responsiveCard.transform,
                theme,
                "Long Localized Label",
                "Coffee Bean 库存与 syrup 插孔设置以及口味确认并保存，Confirm Coffee Machine 咖啡机 Flavor 口味选择",
                16f);
            longLabel.rectTransform.anchoredPosition = new Vector2(0f, -30f);
            longLabel.rectTransform.sizeDelta = new Vector2(720f, 150f);

            var coffeeMachineHint = AddLabel(
                pages.navigation.transform,
                theme,
                "Coffee Machine Hint",
                "Coffee Machine — Tap to select",
                16f);
            coffeeMachineHint.alignment = TextAlignmentOptions.Center;
            coffeeMachineHint.rectTransform.anchoredPosition = new Vector2(-285f, 40f);
            coffeeMachineHint.rectTransform.sizeDelta = new Vector2(320f, 54f);
            coffeeMachineHint.textWrappingMode = TextWrappingModes.NoWrap;
            coffeeMachineHint.raycastTarget = false;

            var tooltip = InstantiatePrefab(Phase5UiAssetPaths.TooltipPrefabPath, pages.feedback.transform, "Tooltip Fixture");
            var tooltipRect = tooltip.GetComponent<RectTransform>();
            CenterFeedbackMessage(tooltipRect);
            tooltip.GetComponent<TooltipView>().SetBackgroundVisible(false);
            var validation = InstantiatePrefab(
                Phase5UiAssetPaths.ValidationMessagePrefabPath,
                pages.feedback.transform,
                "Validation Message Fixture");
            CenterFeedbackMessage(validation.GetComponent<RectTransform>());
            var toastFixture = InstantiatePrefab(Phase5UiAssetPaths.ToastPrefabPath, toast, "Toast Fixture");
            var toastRect = toastFixture.GetComponent<RectTransform>();
            toastRect.anchorMin = toastRect.anchorMax = new Vector2(0.5f, 1f);
            toastRect.pivot = new Vector2(0.5f, 1f);
            toastRect.anchoredPosition = new Vector2(0f, -320f);
            var bottomSheet = InstantiatePrefab(
                Phase5UiAssetPaths.BottomSheetPrefabPath,
                modalLayer,
                "Bottom Sheet Fixture");
            ConfigureBottomSheetEvidenceLayout(bottomSheet);
            var feedbackButtons = CreateFeedbackControls(
                pages.feedback.transform,
                toastFixture,
                tooltip,
                validation,
                bottomSheet);
            CreateReviewFixtures(
                uiRoot, sceneRoot, pages, modalLayer, hud, safeArea.transform, theme,
                camera, sceneInput, sceneInteraction, validation.GetComponent<ValidationMessageView>(),
                feedbackButtons.bottomSheet, bottomSheet.GetComponent<AnimalCafeBottomSheetView>(),
                feedbackButtons.controller, tooltip.GetComponent<TooltipView>());
            Find(pages.feedback.transform, "Feedback Controls").SetAsLastSibling();
            foreach (var graphic in uiRoot.GetComponentsInChildren<Graphic>(true))
            {
                graphic.SetAllDirty();
            }
            Canvas.ForceUpdateCanvases();
        }

        private static void CenterFeedbackMessage(RectTransform rect)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            // Review pages reserve 360 px above and 72 px below. Offset by half
            // their difference so the message lands at the full Game View center.
            rect.anchoredPosition = new Vector2(0f, 144f);
        }

        private static GameObject CreateResponsiveInfoCard(Transform parent)
        {
            var card = CreateUiObject("Responsive Info Card", parent);
            var rect = card.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 144f);
            rect.sizeDelta = new Vector2(820f, 520f);
            var image = card.AddComponent<Image>();
            image.color = new Color(0.96f, 0.91f, 0.80f, 0.96f);
            image.raycastTarget = false;
            return card;
        }

        private static (GameObject buttons, GameObject panels, GameObject navigation, GameObject feedback,
            GameObject responsive, Button[] selectors) CreateReviewPages(Transform parent, AnimalCafeUiTheme theme)
        {
            var header = CreateUiObject("Review Header", parent);
            var headerRect = header.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = Vector2.one;
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.sizeDelta = new Vector2(0f, 280f);
            var buttons = CreatePage(parent, "Buttons Page");
            var panels = CreatePage(parent, "Panels Page");
            var navigation = CreatePage(parent, "Navigation Page");
            var feedback = CreatePage(parent, "Feedback Page");
            var responsive = CreatePage(parent, "Responsive Motion Page");
            var selectorNames = new[] { "Buttons", "Panels", "Navigation", "Feedback", "Responsive Motion" };
            var pageLabels = new[] { "Buttons", "Panels", "Navigation", "Feedback", "Responsive & Motion" };
            var pages = new[] { buttons, panels, navigation, feedback, responsive };
            var selectors = new Button[selectorNames.Length];
            for (var index = 0; index < selectorNames.Length; index++)
            {
                selectors[index] = CreateEvidenceButton(header.transform, selectorNames[index] + " Page Selector", 0f);
                SetButtonLabel(selectors[index], pageLabels[index]);
                var rect = (RectTransform)selectors[index].transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.1f + 0.2f * index, 0.25f);
                rect.sizeDelta = new Vector2(190f, 56f);
                rect.anchoredPosition = Vector2.zero;
                pages[index].SetActive(index == 0);
            }
            header.transform.SetAsLastSibling();
            return (buttons, panels, navigation, feedback, responsive, selectors);
        }

        private static GameObject CreatePage(Transform parent, string name)
        {
            var page = CreateUiObject(name, parent);
            var rect = page.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(56f, 72f);
            rect.offsetMax = new Vector2(-56f, -360f);
            return page;
        }

        private static void CreateReviewFixtures(
            Transform uiRoot,
            Transform sceneRoot,
            (GameObject buttons, GameObject panels, GameObject navigation, GameObject feedback,
                GameObject responsive, Button[] selectors) pages,
            Transform modalLayer,
            Transform hud,
            Transform safeArea,
            AnimalCafeUiTheme theme,
            UnityEngine.Camera camera,
            MouseCameraInput sceneInput,
            SceneInteractionController sceneInteraction,
            ValidationMessageView validation,
            Button openBottomSheet,
            AnimalCafeBottomSheetView bottomSheet,
            Phase5UiFoundationFeedbackController feedback,
            TooltipView tooltip)
        {
            var panelStage = CreatePanelPreviewStage(pages.panels.transform, theme);
            var solidPanel = InstantiatePrefab(Phase5UiAssetPaths.SolidPanelPrefabPath, panelStage.transform, "Solid Panel Fixture");
            var lightPanel = InstantiatePrefab(Phase5UiAssetPaths.LightFrostPanelPrefabPath, panelStage.transform, "Light Frost Panel Fixture");
            var strongPanel = InstantiatePrefab(Phase5UiAssetPaths.StrongFrostPanelPrefabPath, panelStage.transform, "Strong Frost Panel Fixture");
            foreach (var panel in new[] { solidPanel, lightPanel, strongPanel })
            {
                var rect = panel.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(0f, -20f);
                rect.sizeDelta = new Vector2(480f, 250f);
            }
            lightPanel.SetActive(false);
            strongPanel.SetActive(false);
            var panelTitle = AddLabel(panelStage.transform, theme, "Panel Preview Title", "Solid Panel", 22f);
            panelTitle.alignment = TextAlignmentOptions.Center;
            panelTitle.rectTransform.anchoredPosition = new Vector2(0f, 130f);
            panelTitle.rectTransform.sizeDelta = new Vector2(520f, 42f);
            var panelStatus = AddLabel(panelStage.transform, theme, "Panel Preview Status", "Current: Solid", 16f);
            panelStatus.alignment = TextAlignmentOptions.Center;
            panelStatus.rectTransform.anchoredPosition = new Vector2(0f, -145f);
            panelStatus.rectTransform.sizeDelta = new Vector2(520f, 36f);
            var modalObject = InstantiatePrefab(Phase5UiAssetPaths.ModalPrefabPath, modalLayer, "Modal Fixture");
            var secondModalObject = InstantiatePrefab(Phase5UiAssetPaths.ModalPrefabPath, modalLayer, "Second Modal Fixture");
            Stretch(modalObject.GetComponent<RectTransform>());
            Stretch(secondModalObject.GetComponent<RectTransform>());
            secondModalObject.transform.Find("Content/Title").GetComponent<TMP_Text>().text =
                "Confirm second action?";
            secondModalObject.transform.Find("Content/Body").GetComponent<TMP_Text>().text =
                "Close this dialog to return to the first confirmation.";
            var pause = CreateReviewButton(pages.navigation.transform, "Pause Game Button", -220f, -140f);
            var resume = CreateReviewButton(pages.navigation.transform, "Continue Game Button", 0f, -140f);
            var reduced = CreateEvidenceButton(pages.responsive.transform, "Reduced Motion Toggle", 0f);
            reduced.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 340f);
            var secondStrong = CreateEvidenceButton(pages.panels.transform, "Open Second Strong Frost Button", 180f);
            secondStrong.gameObject.SetActive(false);
            var repair = CreateReviewButton(pages.feedback.transform, "Validation Repair Button", -330f, -360f);
            var openModal = CreateReviewButton(pages.navigation.transform, "Open Modal Button", 220f, -140f);
            var safeConfirm = CreateEvidenceButton(safeArea, "Safe Area Confirm Button", 0f);
            safeConfirm.GetComponent<RectTransform>().anchorMin =
                safeConfirm.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0.5f);
            safeConfirm.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -390f);
            safeConfirm.gameObject.SetActive(false);

            var responsiveCard = Find(pages.responsive.transform, "Responsive Info Card");
            var reducedStatus = AddLabel(responsiveCard, theme, "Reduced Motion Status", "Reduced Motion: Off", 18f);
            reducedStatus.fontStyle = FontStyles.Bold;
            reducedStatus.alignment = TextAlignmentOptions.Center;
            reducedStatus.rectTransform.anchoredPosition = new Vector2(0f, 150f);
            reducedStatus.rectTransform.sizeDelta = new Vector2(720f, 48f);
            reducedStatus.textWrappingMode = TextWrappingModes.NoWrap;
            var safeStatus = AddLabel(safeArea, theme, "Safe Area Status", "Safe Area: Ready", 16f);
            safeStatus.rectTransform.anchorMin = safeStatus.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            safeStatus.rectTransform.anchoredPosition = new Vector2(0f, -300f);
            safeStatus.rectTransform.sizeDelta = new Vector2(720f, 48f);
            safeStatus.textWrappingMode = TextWrappingModes.NoWrap;
            safeStatus.gameObject.SetActive(false);

            var frostDiagnostics = CreateUiObject("Frost Lease Diagnostics", pages.panels.transform);
            Stretch(frostDiagnostics.GetComponent<RectTransform>());
            var diagnosticsGroup = frostDiagnostics.AddComponent<CanvasGroup>();
            diagnosticsGroup.alpha = 0f;
            diagnosticsGroup.blocksRaycasts = false;
            diagnosticsGroup.interactable = false;
            var secondStrongObject = InstantiatePrefab(
                Phase5UiAssetPaths.StrongFrostPanelPrefabPath, frostDiagnostics.transform, "Second Strong Frost Fixture");
            secondStrongObject.SetActive(false);
            var occlusion = CreateEvidenceButton(pages.navigation.transform, "World Occlusion Test Button", 0f);
            occlusion.gameObject.SetActive(false);
            var showSolid = CreateReviewButton(pages.panels.transform, "Show Solid Panel Button", -330f, -260f);
            var showLight = CreateReviewButton(pages.panels.transform, "Show Light Frost Panel Button", -110f, -260f);
            var showStrong = CreateReviewButton(pages.panels.transform, "Show Strong Frost Panel Button", 110f, -260f);
            var forceFallback = CreateReviewButton(pages.panels.transform, "Force Frost Fallback Button", 330f, -260f);
            var handleBack = CreateReviewButton(
                modalObject.transform.Find("Content"), "Handle Back Button", -110f, -120f);
            var openSecondModal = CreateReviewButton(
                modalObject.transform.Find("Content"), "Open Second Modal Button", 0f, -40f);
            var toastBurst = CreateReviewButton(pages.feedback.transform, "Show Toast Burst Button", -110f, -360f);
            var longPressTooltip = CreateReviewButton(pages.feedback.transform, "Long Press Tooltip Button", 110f, -360f);
            var closeTooltip = CreateReviewButton(pages.feedback.transform, "Close Tooltip Button", 330f, -360f);
            PlaceViewportButton(repair, 0.15f, 0.22f);
            PlaceViewportButton(toastBurst, 0.38f, 0.22f);
            PlaceViewportButton(longPressTooltip, 0.62f, 0.22f);
            PlaceViewportButton(closeTooltip, 0.85f, 0.22f);
            var interruptReopen = CreateReviewButton(
                modalObject.transform.Find("Content"), "Interrupt And Reopen Button", 110f, -120f);
            var toastBurstStatus = AddLabel(pages.feedback.transform, theme, "Toast Burst Status", "Toast burst: Ready", 16f);
            toastBurstStatus.rectTransform.anchorMin = toastBurstStatus.rectTransform.anchorMax =
                new Vector2(0.5f, 0.32f);
            toastBurstStatus.rectTransform.anchoredPosition = Vector2.zero;
            toastBurstStatus.rectTransform.sizeDelta = new Vector2(720f, 48f);
            toastBurstStatus.textWrappingMode = TextWrappingModes.NoWrap;
            toastBurstStatus.fontSize = 18f;
            toastBurstStatus.fontStyle = FontStyles.Bold;
            toastBurstStatus.color = theme.Colors.Surface;
            var reviewController = sceneRoot.gameObject.AddComponent<Phase5UiFoundationReviewController>();
            reviewController.Configure(
                camera,
                sceneInput,
                sceneInteraction,
                sceneRoot.Find("Selectable Coffee Machine"),
                occlusion,
                uiRoot.GetComponentsInChildren<Button>(true),
                pause,
                resume,
                reduced,
                reducedStatus,
                secondStrong,
                secondStrongObject,
                openModal,
                modalObject.GetComponent<AnimalCafeModalView>(),
                openBottomSheet,
                bottomSheet,
                repair,
                validation,
                safeConfirm,
                safeStatus,
                showSolid,
                showLight,
                showStrong,
                forceFallback,
                solidPanel,
                lightPanel,
                strongPanel,
                panelTitle,
                panelStatus,
                handleBack,
                openSecondModal,
                secondModalObject.GetComponent<AnimalCafeModalView>(),
                toastBurst,
                toastBurstStatus,
                feedback,
                longPressTooltip,
                closeTooltip,
                tooltip,
                interruptReopen,
                pages.selectors,
                new[] { pages.buttons, pages.panels, pages.navigation, pages.feedback, pages.responsive });
        }

        private static GameObject CreatePanelPreviewStage(Transform parent, AnimalCafeUiTheme theme)
        {
            var stage = CreateUiObject("Panel Preview Stage", parent);
            var stageRect = stage.GetComponent<RectTransform>();
            stageRect.anchorMin = stageRect.anchorMax = new Vector2(0.5f, 0.5f);
            stageRect.anchoredPosition = new Vector2(0f, 70f);
            stageRect.sizeDelta = new Vector2(600f, 400f);

            CreateBackdrop(stage.transform, "Panel Backdrop Wood", new Color(0.30f, 0.20f, 0.10f, 1f),
                new Vector2(-200f, 0f), new Vector2(200f, 400f));
            CreateBackdrop(stage.transform, "Panel Backdrop Sage", theme.Colors.Accent,
                new Vector2(0f, 0f), new Vector2(200f, 400f));
            CreateBackdrop(stage.transform, "Panel Backdrop Cream", theme.Colors.Surface,
                new Vector2(200f, 0f), new Vector2(200f, 400f));
            return stage;
        }

        private static void CreateBackdrop(Transform parent, string name, Color color, Vector2 position, Vector2 size)
        {
            var backdrop = CreateUiObject(name, parent);
            var rect = backdrop.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = backdrop.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private static Button CreateReviewButton(Transform parent, string name, float x, float y)
        {
            var button = CreateEvidenceButton(parent, name, x);
            button.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, y);
            return button;
        }

        private static void PlaceViewportButton(Button button, float anchorX, float anchorY)
        {
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(anchorX, anchorY);
            rect.anchoredPosition = Vector2.zero;
        }

        private static void ConfigureBottomSheetEvidenceLayout(GameObject bottomSheet)
        {
            Stretch(bottomSheet.GetComponent<RectTransform>());
            var outside = bottomSheet.transform.Find("OutsideButton") as RectTransform
                ?? throw new InvalidOperationException("Bottom Sheet fixture requires OutsideButton.");
            Stretch(outside);
            outside.SetAsFirstSibling();
            var outsideGraphic = outside.GetComponent<Image>()
                ?? throw new InvalidOperationException("Bottom Sheet OutsideButton requires an Image.");
            outsideGraphic.raycastTarget = true;

            var content = bottomSheet.transform.Find("Content") as RectTransform
                ?? throw new InvalidOperationException("Bottom Sheet fixture requires Content.");
            content.anchorMin = Vector2.zero;
            content.anchorMax = new Vector2(1f, 0.55f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            content.SetAsLastSibling();
            PrefabUtility.RecordPrefabInstancePropertyModifications(bottomSheet.GetComponent<RectTransform>());
            PrefabUtility.RecordPrefabInstancePropertyModifications(outside);
            PrefabUtility.RecordPrefabInstancePropertyModifications(outsideGraphic);
            PrefabUtility.RecordPrefabInstancePropertyModifications(content);
        }

        private static (Button toast, Button tooltip, Button validation, Button bottomSheet,
            Phase5UiFoundationFeedbackController controller) CreateFeedbackControls(
            Transform parent,
            GameObject toast,
            GameObject tooltip,
            GameObject validation,
            GameObject bottomSheet)
        {
            var controls = CreateUiObject("Feedback Controls", parent);
            var controlsRect = controls.GetComponent<RectTransform>();
            controlsRect.anchorMin = new Vector2(0f, 1f);
            controlsRect.anchorMax = Vector2.one;
            controlsRect.pivot = new Vector2(0.5f, 1f);
            controlsRect.sizeDelta = new Vector2(0f, 160f);
            var toastButton = CreateNormalizedEvidenceButton(controls.transform, "Show Toast Button", 0.15f);
            var tooltipButton = CreateNormalizedEvidenceButton(controls.transform, "Show Tooltip Button", 0.38f);
            var validationButton = CreateNormalizedEvidenceButton(controls.transform, "Show Validation Error Button", 0.62f);
            var bottomSheetButton = CreateNormalizedEvidenceButton(controls.transform, "Open Bottom Sheet Button", 0.85f);
            var controller = controls.AddComponent<Phase5UiFoundationFeedbackController>();
            controller.Configure(
                toastButton,
                tooltipButton,
                validationButton,
                bottomSheetButton,
                toast.GetComponent<ToastView>(),
                tooltip.GetComponent<TooltipView>(),
                validation.GetComponent<ValidationMessageView>(),
                bottomSheet);
            return (toastButton, tooltipButton, validationButton, bottomSheetButton, controller);
        }

        private static Button CreateEvidenceButton(Transform parent, string name, float x)
        {
            var button = InstantiatePrefab(
                Phase5UiAssetPaths.Root + "/Prefabs/PF_UI_Button_Primary_Default.prefab",
                parent,
                name);
            button.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, 0f);
            var component = button.GetComponent<Button>();
            var actionLabel = ActionLabel(name);
            if (actionLabel != null) SetButtonLabel(component, actionLabel);
            return component;
        }

        private static void SetButtonLabel(Button button, string label)
        {
            var text = button.GetComponentInChildren<TMP_Text>(true)
                ?? throw new InvalidOperationException("Validation button requires a TMP label: " + button.name);
            text.text = label;
        }

        private static string ActionLabel(string name) => name switch
        {
            "Show Toast Button" => "Show Toast",
            "Show Tooltip Button" => "Show Tooltip",
            "Show Validation Error Button" => "Show Validation Error",
            "Open Bottom Sheet Button" => "Open Bottom Sheet",
            "Pause Game Button" => "Pause Game",
            "Continue Game Button" => "Continue Game",
            "Reduced Motion Toggle" => "Toggle Reduced Motion",
            "Open Second Strong Frost Button" => "Open Second Strong Frost",
            "Validation Repair Button" => "Repair Validation",
            "Open Modal Button" => "Open Modal",
            "Safe Area Confirm Button" => "Confirm Safe Area",
            "World Occlusion Test Button" => "Test World Occlusion",
            "Show Solid Panel Button" => "Show Solid Panel",
            "Show Light Frost Panel Button" => "Show Light Frost Panel",
            "Show Strong Frost Panel Button" => "Show Strong Frost Panel",
            "Force Frost Fallback Button" => "Force Frost Fallback",
            "Handle Back Button" => "Handle Back",
            "Open Second Modal Button" => "Open Second Modal",
            "Show Toast Burst Button" => "Show Toast Burst",
            "Long Press Tooltip Button" => "Long Press Tooltip",
            "Close Tooltip Button" => "Close Tooltip",
            "Interrupt And Reopen Button" => "Interrupt And Reopen",
            _ => null
        };

        private static Button CreateNormalizedEvidenceButton(Transform parent, string name, float anchorX)
        {
            var button = CreateEvidenceButton(parent, name, 0f);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(anchorX, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            return button;
        }

        private static void CreateButtonGallery(Transform parent, AnimalCafeUiTheme theme)
        {
            var headings = new[] { "Default", "Pressed Preview", "Disabled" };
            for (var column = 0; column < headings.Length; column++)
            {
                var heading = AddLabel(parent, theme,
                    headings[column] + " Column Heading", headings[column], 16f);
                heading.alignment = TextAlignmentOptions.Center;
                heading.rectTransform.anchoredPosition = new Vector2(-240f + column * 240f, 150f);
                heading.rectTransform.sizeDelta = new Vector2(200f, 40f);
            }
            var index = 0;
            foreach (var prefabPath in Phase5UiAssetPaths.ButtonPrefabPaths)
            {
                var button = InstantiatePrefab(prefabPath, parent, "Gallery " + Path.GetFileNameWithoutExtension(prefabPath));
                var column = index % 3;
                var row = index / 3;
                button.GetComponent<RectTransform>().anchoredPosition = new Vector2(
                    -240f + column * 240f,
                    80f - row * 92f);
                index++;
            }
        }

        private static TextMeshProUGUI AddLabel(
            Transform parent,
            AnimalCafeUiTheme theme,
            string name,
            string content,
            float size)
        {
            var label = CreateUiObject(name, parent);
            var text = label.AddComponent<TextMeshProUGUI>();
            text.font = theme.Typography.Body.FontAsset;
            text.fontSize = size;
            text.text = content;
            text.color = theme.Colors.Text;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            return text;
        }

        private static GameObject InstantiatePrefab(string path, Transform parent, string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path)
                ?? throw new InvalidOperationException("Missing Phase 5 prefab: " + path);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = name;
            instance.transform.SetParent(parent, false);
            return instance;
        }

        private static Transform Find(Transform root, string path)
        {
            return root.Find(path) ?? throw new InvalidOperationException("Missing UI root path: " + path);
        }

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            var instance = new GameObject(name, typeof(RectTransform));
            instance.transform.SetParent(parent, false);
            return instance;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void EnsureFolder(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            var current = "Assets";
            foreach (var segment in path.Substring("Assets/".Length).Split('/'))
            {
                var next = current + "/" + segment;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segment);
                current = next;
            }
        }
    }
}
