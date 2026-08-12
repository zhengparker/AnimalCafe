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
        private const int RecipeVersion = 8;
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
            coffeeMachine.transform.localPosition = new Vector3(-1.5f, 0.5f, 0f);
            coffeeMachine.transform.localScale = new Vector3(1.5f, 1f, 1f);
            coffeeMachine.AddComponent<ColorSelectable>().Configure(
                coffeeMachine.GetComponent<Renderer>());

            var mover = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mover.name = "Scaled Time Mover";
            mover.transform.SetParent(parent, false);
            mover.transform.localPosition = new Vector3(-2f, 0.5f, -1f);
            mover.AddComponent<ManualReviewPingPongMover>().Configure(
                new Vector3(-2f, 0.5f, -1f), new Vector3(2f, 0.5f, -1f), 1f);
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
            var toast = Find(uiRoot, "Toast Canvas/Toast Layer");
            Stretch((RectTransform)hud);
            Stretch((RectTransform)panel);
            Stretch((RectTransform)toast);

            var safeArea = InstantiatePrefab(Phase5UiAssetPaths.SafeAreaPrefabPath, hud, "Safe Area");
            Stretch(safeArea.GetComponent<RectTransform>());
            var gallery = CreateUiObject("Component Gallery", panel);
            gallery.GetComponent<RectTransform>().sizeDelta = new Vector2(900f, 880f);
            AddLabel(gallery.transform, theme, "Gallery Title", "Component Gallery / UI Components", 28f);
            CreateButtonGallery(gallery.transform);
            var longLabel = AddLabel(
                gallery.transform,
                theme,
                "Long Localized Label",
                "Coffee Bean 库存与 syrup 插孔设置以及口味确认并保存，Confirm Coffee Machine 咖啡机 Flavor 口味选择",
                16f);
            longLabel.rectTransform.anchoredPosition = new Vector2(0f, -240f);
            longLabel.rectTransform.sizeDelta = new Vector2(760f, 160f);

            var tooltip = InstantiatePrefab(Phase5UiAssetPaths.TooltipPrefabPath, panel, "Tooltip Fixture");
            tooltip.GetComponent<RectTransform>().anchoredPosition = new Vector2(-180f, -560f);
            var validation = InstantiatePrefab(
                Phase5UiAssetPaths.ValidationMessagePrefabPath,
                panel,
                "Validation Message Fixture");
            validation.GetComponent<RectTransform>().anchoredPosition = new Vector2(180f, -560f);
            var toastFixture = InstantiatePrefab(Phase5UiAssetPaths.ToastPrefabPath, toast, "Toast Fixture");
            toastFixture.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -120f);
            var bottomSheet = InstantiatePrefab(
                Phase5UiAssetPaths.BottomSheetPrefabPath,
                panel,
                "Bottom Sheet Fixture");
            ConfigureBottomSheetEvidenceLayout(bottomSheet);
            var feedbackButtons = CreateFeedbackControls(
                panel,
                toastFixture,
                tooltip,
                validation,
                bottomSheet);
            CreateReviewFixtures(
                uiRoot, sceneRoot, panel, hud, safeArea.transform, theme,
                camera, sceneInput, sceneInteraction, validation.GetComponent<ValidationMessageView>(),
                feedbackButtons.bottomSheet, bottomSheet.GetComponent<AnimalCafeBottomSheetView>(),
                feedbackButtons.controller, tooltip.GetComponent<TooltipView>());
            foreach (var graphic in uiRoot.GetComponentsInChildren<Graphic>(true))
            {
                graphic.SetAllDirty();
            }
            Canvas.ForceUpdateCanvases();
        }

        private static void CreateReviewFixtures(
            Transform uiRoot,
            Transform sceneRoot,
            Transform panel,
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
            var solidPanel = InstantiatePrefab(Phase5UiAssetPaths.SolidPanelPrefabPath, panel, "Solid Panel Fixture");
            var lightPanel = InstantiatePrefab(Phase5UiAssetPaths.LightFrostPanelPrefabPath, panel, "Light Frost Panel Fixture");
            var strongPanel = InstantiatePrefab(Phase5UiAssetPaths.StrongFrostPanelPrefabPath, panel, "Strong Frost Panel Fixture");
            var modalObject = InstantiatePrefab(Phase5UiAssetPaths.ModalPrefabPath, panel, "Modal Fixture");
            var secondModalObject = InstantiatePrefab(Phase5UiAssetPaths.ModalPrefabPath, panel, "Second Modal Fixture");
            var pause = CreateEvidenceButton(panel, "Pause Game Button", -360f);
            var resume = CreateEvidenceButton(panel, "Continue Game Button", -180f);
            var reduced = CreateEvidenceButton(panel, "Reduced Motion Toggle", 0f);
            var secondStrong = CreateEvidenceButton(panel, "Open Second Strong Frost Button", 180f);
            var repair = CreateEvidenceButton(panel, "Validation Repair Button", 360f);
            var openModal = CreateEvidenceButton(panel, "Open Modal Button", 0f);
            openModal.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -100f);
            var safeConfirm = CreateEvidenceButton(safeArea, "Safe Area Confirm Button", 0f);
            safeConfirm.GetComponent<RectTransform>().anchorMin =
                safeConfirm.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0.88f);

            var reducedStatus = AddLabel(panel, theme, "Reduced Motion Status", "Reduced Motion: Off", 16f);
            reducedStatus.rectTransform.anchoredPosition = new Vector2(0f, -165f);
            var safeStatus = AddLabel(safeArea, theme, "Safe Area Status", "Safe Area: Ready", 16f);
            safeStatus.rectTransform.anchorMin = safeStatus.rectTransform.anchorMax = new Vector2(0.5f, 0.82f);
            safeStatus.rectTransform.anchoredPosition = Vector2.zero;

            var secondStrongObject = InstantiatePrefab(
                Phase5UiAssetPaths.StrongFrostPanelPrefabPath, panel, "Second Strong Frost Fixture");
            secondStrongObject.SetActive(false);
            var occlusion = CreateEvidenceButton(hud, "World Occlusion Test Button", 0f);
            var showSolid = CreateReviewButton(panel, "Show Solid Panel Button", -360f, -260f);
            var showLight = CreateReviewButton(panel, "Show Light Frost Panel Button", -180f, -260f);
            var showStrong = CreateReviewButton(panel, "Show Strong Frost Panel Button", 0f, -260f);
            var forceFallback = CreateReviewButton(panel, "Force Frost Fallback Button", 180f, -260f);
            var handleBack = CreateReviewButton(panel, "Handle Back Button", 360f, -260f);
            var openSecondModal = CreateReviewButton(
                modalObject.transform.Find("Content"), "Open Second Modal Button", 0f, 180f);
            var toastBurst = CreateReviewButton(panel, "Show Toast Burst Button", -180f, -360f);
            var longPressTooltip = CreateReviewButton(panel, "Long Press Tooltip Button", 0f, -360f);
            var closeTooltip = CreateReviewButton(panel, "Close Tooltip Button", 180f, -360f);
            var interruptReopen = CreateReviewButton(panel, "Interrupt And Reopen Button", 360f, -360f);
            var toastBurstStatus = AddLabel(panel, theme, "Toast Burst Status", "Toast burst: Ready", 16f);
            toastBurstStatus.rectTransform.anchoredPosition = new Vector2(0f, -430f);
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
                handleBack,
                openSecondModal,
                secondModalObject.GetComponent<AnimalCafeModalView>(),
                toastBurst,
                toastBurstStatus,
                feedback,
                longPressTooltip,
                closeTooltip,
                tooltip,
                interruptReopen);
        }

        private static Button CreateReviewButton(Transform parent, string name, float x, float y)
        {
            var button = CreateEvidenceButton(parent, name, x);
            button.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, y);
            return button;
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
            Stretch(controls.GetComponent<RectTransform>());
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
            return button.GetComponent<Button>();
        }

        private static Button CreateNormalizedEvidenceButton(Transform parent, string name, float anchorX)
        {
            var button = CreateEvidenceButton(parent, name, 0f);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(anchorX, 0.12f);
            rect.anchoredPosition = Vector2.zero;
            return button;
        }

        private static void CreateButtonGallery(Transform parent)
        {
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
