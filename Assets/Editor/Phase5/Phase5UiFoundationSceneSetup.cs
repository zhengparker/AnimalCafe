using System;
using System.IO;
using AnimalCafe.Diagnostics;
using AnimalCafe.Interaction;
using AnimalCafe.UI.Components;
using AnimalCafe.UI.Feedback;
using AnimalCafe.UI.Foundation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
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
        public const string ScenePath = "Assets/Scenes/Validation/Phase5UiFoundation.unity";

        [MenuItem("AnimalCafe/Phase 5/Build UI Foundation Validation Scene")]
        public static void BuildSceneFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) BuildScene();
        }

        public static void BuildScene()
        {
            Phase5UiAssetBuilder.BuildAll();
            EnsureFolder(Path.GetDirectoryName(ScenePath)?.Replace('\\', '/'));

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Phase5UiFoundation";
            var root = new GameObject("Phase5UiFoundationRoot");
            CreateWorldFixtures(root.transform);
            CreateEventSystem(root.transform);
            var uiRoot = InstantiatePrefab(Phase5UiAssetPaths.UiRootPrefabPath, root.transform, "UI Root");
            CreateUiFixtures(uiRoot.transform);

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

        private static void CreateWorldFixtures(Transform parent)
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
        }

        private static void CreateEventSystem(Transform parent)
        {
            var eventSystem = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            eventSystem.transform.SetParent(parent, false);
        }

        private static void CreateUiFixtures(Transform uiRoot)
        {
            var theme = AssetDatabase.LoadAssetAtPath<AnimalCafeUiTheme>(Phase5UiAssetPaths.ThemePath)
                ?? throw new InvalidOperationException("Phase 5 UI theme is missing.");
            var hud = Find(uiRoot, "HUD Canvas/HUD Layer");
            var panel = Find(uiRoot, "Screen Canvas/Panel Layer");
            var toast = Find(uiRoot, "Toast Canvas/Toast Layer");

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
            CreateFeedbackControls(
                panel,
                toastFixture,
                tooltip,
                validation,
                bottomSheet);
        }

        private static void CreateFeedbackControls(
            Transform parent,
            GameObject toast,
            GameObject tooltip,
            GameObject validation,
            GameObject bottomSheet)
        {
            var controls = CreateUiObject("Feedback Controls", parent);
            controls.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -760f);
            var toastButton = CreateEvidenceButton(controls.transform, "Show Toast Button", -270f);
            var tooltipButton = CreateEvidenceButton(controls.transform, "Show Tooltip Button", -90f);
            var validationButton = CreateEvidenceButton(controls.transform, "Show Validation Error Button", 90f);
            var bottomSheetButton = CreateEvidenceButton(controls.transform, "Open Bottom Sheet Button", 270f);
            controls.AddComponent<Phase5UiFoundationFeedbackController>().Configure(
                toastButton,
                tooltipButton,
                validationButton,
                bottomSheetButton,
                toast.GetComponent<ToastView>(),
                tooltip.GetComponent<TooltipView>(),
                validation.GetComponent<ValidationMessageView>(),
                bottomSheet);
        }

        private static Button CreateEvidenceButton(Transform parent, string name, float x)
        {
            var button = InstantiatePrefab(
                Phase5UiAssetPaths.ButtonPrefabPaths[0],
                parent,
                name);
            button.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, 0f);
            return button.GetComponent<Button>();
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
