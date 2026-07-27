using System;
using AnimalCafe.Camera;
using AnimalCafe.Core.Time;
using AnimalCafe.Input;
using AnimalCafe.Interaction;
using AnimalCafe.UI;
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
        private const string CanvasName = "Phase0_TimeControls";

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
            var settings = GetOrCreateCameraSettings();
            ConfigureCamera(mainCamera);
            ConfigureRuntime(mainCamera, settings);
            ConfigureTimeControls();
            EnsureEventSystem();

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
            UnityEngine.Camera mainCamera,
            CameraSettings settings)
        {
            var root = FindOrCreateRoot(RuntimeRootName);
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

        private static void ConfigureTimeControls()
        {
            var canvasObject = GameObject.Find(CanvasName);
            if (canvasObject == null)
            {
                canvasObject = new GameObject(
                    CanvasName,
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler),
                    typeof(GraphicRaycaster));
            }

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1024f, 768f);

            var panelObject = FindOrCreateUiObject(canvasObject.transform, "TimePanel");
            var panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, 24f);
            panelRect.sizeDelta = new Vector2(330f, 64f);

            var pause = CreateButton(panelObject.transform, "PauseButton", "Pause", -110f);
            var normal = CreateButton(panelObject.transform, "NormalButton", "1x", 0f);
            var fast = CreateButton(panelObject.transform, "FastButton", "2x", 110f);
            var panel = GetOrAdd<TimeControlPanel>(panelObject);
            var service = GameObject.Find(RuntimeRootName).GetComponent<GameTimeService>();
            SetObjectReference(panel, "gameTimeService", service);
            SetObjectReference(panel, "pauseButton", pause);
            SetObjectReference(panel, "normalButton", normal);
            SetObjectReference(panel, "fastButton", fast);
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string label,
            float x)
        {
            var buttonObject = FindOrCreateUiObject(parent, name);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(96f, 48f);
            rect.anchoredPosition = new Vector2(x, 0f);
            var image = GetOrAdd<Image>(buttonObject);
            image.color = new Color(0.16f, 0.2f, 0.25f, 0.95f);
            var button = GetOrAdd<Button>(buttonObject);
            button.targetGraphic = image;

            var textObject = FindOrCreateUiObject(buttonObject.transform, "Label");
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = GetOrAdd<Text>(textObject);
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.fontSize = 22;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return button;
        }

        private static void EnsureEventSystem()
        {
            var eventSystem = GameObject.Find("EventSystem");
            if (eventSystem == null)
            {
                eventSystem = new GameObject("EventSystem");
            }

            GetOrAdd<EventSystem>(eventSystem);
            var oldModule = eventSystem.GetComponent<StandaloneInputModule>();
            if (oldModule != null)
            {
                UnityEngine.Object.DestroyImmediate(oldModule);
            }

            GetOrAdd<InputSystemUIInputModule>(eventSystem);
        }

        private static GameObject FindOrCreateRoot(string name)
        {
            return GameObject.Find(name) ?? new GameObject(name);
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
            return gameObject.GetComponent<T>() ?? gameObject.AddComponent<T>();
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
