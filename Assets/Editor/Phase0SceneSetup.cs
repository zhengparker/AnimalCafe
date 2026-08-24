using System;
using System.Linq;
using AnimalCafe.Camera;
using AnimalCafe.Core.Time;
using AnimalCafe.Input;
using AnimalCafe.Interaction;
using AnimalCafe.UI;
using AnimalCafe.UI.Components;
using AnimalCafe.UI.Foundation;
using AnimalCafe.EditorTools.Phase5;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AnimalCafe.EditorTools
{
    /// <summary>
    /// 可重复运行的 Phase 0 scene authoring tool。
    /// Idempotent Phase 0 scene authoring tool.
    /// </summary>
    public static class Phase0SceneSetup
    {
        private const string ScenePath = "Assets/Scenes/MainCafe.unity";
        private const string SettingsPath = "Assets/Config/DefaultCameraSettings.asset";
        private const string RuntimeRootName = "Phase0_Runtime";
        private const string LegacyCanvasName = "Phase0_TimeControls";

        [MenuItem("AnimalCafe/Phase 0/Configure Scene")]
        public static void ConfigurePhase0Scene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var mainCameraObject = GameObject.Find("Main Camera");
            if (mainCameraObject == null)
            {
                throw new InvalidOperationException(
                    "MainCafe must contain a GameObject named 'Main Camera'.");
            }

            var mainCamera = mainCameraObject.GetComponent<UnityEngine.Camera>();
            if (mainCamera == null)
            {
                throw new InvalidOperationException(
                    "'Main Camera' must contain a Camera component.");
            }

            RemoveLegacyDemoObjects(scene);
            Phase5UiAssetBuilder.BuildAll();
            var settings = GetOrCreateCameraSettings();
            ConfigureCamera(mainCamera);
            ConfigureRuntime(scene, mainCamera, settings);
            var uiRoot = FindOrCreatePhase5UiRoot(scene);
            ConfigureTimeControls(scene, uiRoot.transform);
            EnsureEventSystem(scene, uiRoot.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Phase0SceneSetup] MainCafe configured successfully.");
        }

        private static CameraSettings GetOrCreateCameraSettings()
        {
            EnsureFolder("Assets", "Config");
            var settings = AssetDatabase.LoadAssetAtPath<CameraSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<CameraSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }

            settings.PanSpeed = 0.02f;
            settings.ZoomSpeed = 0.75f;
            settings.PositionMin = new Vector2(-12f, -12f);
            settings.PositionMax = new Vector2(-8f, -8f);
            settings.MinOrthographicSize = 4f;
            settings.MaxOrthographicSize = 12f;
            settings.DragThresholdPixels = 6f;
            EditorUtility.SetDirty(settings);
            return settings;
        }

        private static void ConfigureCamera(UnityEngine.Camera mainCamera)
        {
            mainCamera.orthographic = true;
            mainCamera.orthographicSize = 7f;
            mainCamera.transform.SetPositionAndRotation(
                new Vector3(-10f, 10f, -10f),
                Quaternion.Euler(35.264f, 45f, 0f));
        }

        private static void ConfigureRuntime(
            Scene scene,
            UnityEngine.Camera mainCamera,
            CameraSettings settings)
        {
            var root = FindOrCreateOwnedRoot(scene, RuntimeRootName);
            var mouseInput = GetOrAdd<MouseCameraInput>(root);
            mouseInput.DragThresholdPixels = settings.DragThresholdPixels;
            SetObjectReference(mouseInput, "settings", settings);

            var cameraController = GetOrAdd<CafeCameraController>(root);
            SetObjectReference(cameraController, "targetCamera", mainCamera);
            SetObjectReference(cameraController, "settings", settings);
            SetObjectReference(cameraController, "inputSourceBehaviour", mouseInput);

            GetOrAdd<GameTimeService>(root);

            var interaction = GetOrAdd<SceneInteractionController>(root);
            SetObjectReference(interaction, "targetCamera", mainCamera);
            SetObjectReference(interaction, "inputSourceBehaviour", mouseInput);
        }

        private static void RemoveLegacyDemoObjects(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (string.Equals(
                    root.name,
                    "Phase0_Demo",
                    StringComparison.Ordinal))
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        private static void ConfigureTimeControls(Scene scene, Transform uiRoot)
        {
            RemoveNamedObjects(scene, LegacyCanvasName);
            var runtimeRoot = FindOrCreateOwnedRoot(scene, RuntimeRootName);
            var service = runtimeRoot.GetComponent<GameTimeService>();
            var rightRail = uiRoot.GetComponentsInChildren<TimeControlPanel>(true)
                .SingleOrDefault(panel => panel.name == "RightRail");
            if (rightRail != null)
            {
                RemoveNamedObjects(scene, "TimePanel");
                SetObjectReference(rightRail, "gameTimeService", service);
                return;
            }

            var theme = AssetDatabase.LoadAssetAtPath<AnimalCafeUiTheme>(Phase5UiAssetPaths.ThemePath)
                ?? throw new InvalidOperationException("Phase 5 UI theme is missing.");
            var hudLayer = uiRoot.Find("HUD Canvas/HUD Layer")
                ?? throw new InvalidOperationException("Phase 5 UI Root is missing its HUD Layer.");
            var panelObject = FindOrCreateUiObject(hudLayer, "TimePanel");
            var panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, 24f);
            panelRect.sizeDelta = new Vector2(330f, 64f);

            var pause = CreateButton(panelObject.transform, "PauseButton", "Pause", -110f, theme);
            var normal = CreateButton(panelObject.transform, "NormalButton", "1x", 0f, theme);
            var fast = CreateButton(panelObject.transform, "FastButton", "2x", 110f, theme);
            var panel = GetOrAdd<TimeControlPanel>(panelObject);
            SetObjectReference(panel, "gameTimeService", service);
            SetObjectReference(panel, "pauseButton", pause);
            SetObjectReference(panel, "normalButton", normal);
            SetObjectReference(panel, "fastButton", fast);
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string label,
            float x,
            AnimalCafeUiTheme theme)
        {
            var buttonObject = FindOrCreateUiObject(parent, name);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(96f, 48f);
            rect.anchoredPosition = new Vector2(x, 0f);
            var image = GetOrAdd<Image>(buttonObject);
            var roundedSprite = AssetDatabase.LoadAllAssetsAtPath(Phase5UiAssetPaths.RoundedSpritePath)
                .OfType<Sprite>()
                .FirstOrDefault();
            image.sprite = roundedSprite;
            image.type = roundedSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = theme.Colors.Accent;
            var button = GetOrAdd<Button>(buttonObject);
            button.targetGraphic = image;
            var shadow = GetOrAdd<Shadow>(buttonObject);
            shadow.effectColor = new Color(0.20f, 0.12f, 0.06f, 0.24f);
            shadow.effectDistance = new Vector2(0f, -6f);
            shadow.useGraphicAlpha = true;
            GetOrAdd<AnimalCafeButtonView>(buttonObject)
                .Configure(theme, UiButtonRole.Primary, button, image);

            var textObject = FindOrCreateUiObject(buttonObject.transform, "Label");
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            foreach (var legacyText in textObject.GetComponents<Text>())
            {
                UnityEngine.Object.DestroyImmediate(legacyText);
            }

            var text = GetOrAdd<TextMeshProUGUI>(textObject);
            text.text = label;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.fontSize = theme.Typography.Label.FontSize;
            text.font = theme.Typography.Label.FontAsset;
            text.raycastTarget = false;
            return button;
        }

        private static void EnsureEventSystem(Scene scene, Transform uiRoot)
        {
            var transforms = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .ToArray();
            var systems = transforms
                .Select(transform => transform.GetComponent<EventSystem>())
                .Where(system => system != null)
                .ToArray();
            var namedObjects = transforms
                .Where(transform => transform.name == "EventSystem")
                .Select(transform => transform.gameObject)
                .Distinct()
                .ToArray();
            var eventSystem = systems.FirstOrDefault()?.gameObject
                ?? namedObjects.FirstOrDefault()
                ?? new GameObject("EventSystem");
            foreach (var duplicate in systems.Select(system => system.gameObject)
                         .Concat(namedObjects)
                         .Where(candidate => candidate != eventSystem)
                         .Distinct())
            {
                UnityEngine.Object.DestroyImmediate(duplicate);
            }

            eventSystem.transform.SetParent(uiRoot, false);

            GetOrAdd<EventSystem>(eventSystem);
            var oldModule = eventSystem.GetComponent<StandaloneInputModule>();
            if (oldModule != null)
            {
                UnityEngine.Object.DestroyImmediate(oldModule);
            }

            GetOrAdd<InputSystemUIInputModule>(eventSystem);
        }

        private static GameObject FindOrCreatePhase5UiRoot(Scene scene)
        {
            var roots = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(transform => transform.name == "UI Root")
                .ToArray();
            var uiRoot = roots.FirstOrDefault()?.gameObject;
            foreach (var duplicate in roots.Skip(1))
            {
                UnityEngine.Object.DestroyImmediate(duplicate.gameObject);
            }

            if (uiRoot == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Phase5UiAssetPaths.UiRootPrefabPath)
                    ?? throw new InvalidOperationException("Phase 5 UI Root prefab is missing.");
                uiRoot = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                uiRoot.name = "UI Root";
            }

            GetOrAdd<UiGraphicRegistration>(uiRoot);
            uiRoot.SetActive(true);
            return uiRoot;
        }

        private static void RemoveNamedObjects(Scene scene, string name)
        {
            foreach (var target in scene.GetRootGameObjects()
                         .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                         .Where(transform => transform.name == name)
                         .Select(transform => transform.gameObject)
                         .ToArray())
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static GameObject FindOrCreateOwnedRoot(
            Scene scene,
            string name)
        {
            GameObject canonical = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (!string.Equals(
                        root.name,
                        name,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (canonical == null)
                {
                    canonical = root;
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(root);
            }

            canonical ??= new GameObject(name);
            if (canonical.scene != scene)
            {
                SceneManager.MoveGameObjectToScene(canonical, scene);
            }

            canonical.SetActive(true);
            return canonical;
        }

        private static GameObject FindOrCreateUiObject(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null)
            {
                return child.gameObject;
            }

            var created = new GameObject(name, typeof(RectTransform));
            created.transform.SetParent(parent, false);
            return created;
        }

        private static T GetOrAdd<T>(GameObject gameObject) where T : Component
        {
            var components = gameObject.GetComponents<T>();
            if (components.Length == 0)
            {
                return gameObject.AddComponent<T>();
            }

            var canonical = components[0];
            for (var index = 1; index < components.Length; index++)
            {
                UnityEngine.Object.DestroyImmediate(components[index]);
            }

            return canonical;
        }

        private static void SetObjectReference(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object value)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"{target.GetType().Name} has no serialized property '{propertyName}'.");
            }

            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void EnsureFolder(string parent, string name)
        {
            var path = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}
