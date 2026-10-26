using System;
using System.Linq;
using AnimalCafe.Core.Time;
using AnimalCafe.Customers;
using AnimalCafe.Decoration;
using AnimalCafe.Employees;
using AnimalCafe.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AnimalCafe.EditorTools.Phase13
{
    public static class Phase13EmployeeTaskSceneSetup
    {
        public const string ScenePath = "Assets/Scenes/Validation/Phase13EmployeeTasks.unity";
        private const string MainPath = "Assets/Scenes/MainCafe.unity";
        [MenuItem("Tools/AnimalCafe/Phase 13/Create Employee Task Validation Scene")]
        public static void BuildValidationScene() => Build(true);
        [MenuItem("Tools/AnimalCafe/Phase 13/Install Employee Tasks in MainCafe")]
        public static void BuildMainCafe() => Build(false);
        private static void Build(bool validation)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play before building P13 scenes");
            for (var index = 0; index < SceneManager.sceneCount; index++)
                if (SceneManager.GetSceneAt(index).isDirty) throw new InvalidOperationException("Save your dirty Scene before building P13");
            var target = validation ? ScenePath : MainPath;
            var previous = SceneManager.GetActiveScene();
            // Copy through AssetDatabase first: an already-open P12 Scene is never our edit target.
            if (validation && !System.IO.File.Exists(target) &&
                !AssetDatabase.CopyAsset("Assets/Scenes/Validation/Phase12CustomerQueue.unity", target))
                throw new InvalidOperationException("Could not create the independent P13 Scene copy");
            var loaded = SceneManager.GetSceneByPath(target);
            // P13 is derived from the already accepted P12 business scene. Never rebuild P11/P12.
            var scene = loaded.IsValid() && loaded.isLoaded ? loaded : EditorSceneManager.OpenScene(target, OpenSceneMode.Additive);
            var close = !(loaded.IsValid() && loaded.isLoaded);
            try
            {
                SceneManager.SetActiveScene(scene);
                var decoration = All<DecorationModeController>(scene).Single();
                var customers = All<CustomerFlowController>(scene).Single();
                if (validation)
                {
                    customers.AutoSpawn = false;
                    var oldHud = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "P12 Validation HUD");
                    if (oldHud != null) UnityEngine.Object.DestroyImmediate(oldHud.gameObject);
                    // Keep the existing P12 seed component, with no HUD. It seeds only this validation Scene at Play.
                }
                var root = scene.GetRootGameObjects().SingleOrDefault(value => value.name == "P13 Employees");
                if (root == null) root = new GameObject("P13 Employees");
                var flow = root.GetComponent<EmployeeFlowController>() ?? root.AddComponent<EmployeeFlowController>();
                var display = root.GetComponent<PickupCupDisplay>() ?? root.AddComponent<PickupCupDisplay>();
                var time = All<GameTimeService>(scene).Single();
                Set(flow, "cupPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Phase4/Prefabs/PF_Item_CeramicCup_01.prefab"));
                flow.Configure(All<NavigationWorld>(scene).Single(), All<NavigationLayoutAdapter>(scene).Single(), Read<CafeLayoutRuntime>(decoration, "layoutRuntime"),
                    decoration, time, customers, Read<Transform>(decoration, "gridRoot"), Prefab("Shiba"), Prefab("Westie"), display);
                var controller = root.GetComponent<EmployeeTaskValidationController>() ?? root.AddComponent<EmployeeTaskValidationController>();
                controller.Configure(flow, customers, time);
                BuildHud(root, controller);
                Set(controller, "decoration", decoration); Set(controller, "bridge", All<NavigationDecorationBridge>(scene).Single()); Set(controller, "cups", display);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, target)) throw new InvalidOperationException("Could not save P13 Scene");
                Debug.Log("AC_P13_SCENE_READY " + target);
            }
            finally
            {
                if (close) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }
        private static NavigationActor Prefab(string species) => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Phase13/Employees/" + species + "/PF_" + species + "_Employee.prefab").GetComponent<NavigationActor>();
        private static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
        private static T Read<T>(UnityEngine.Object owner, string field) where T : UnityEngine.Object => (T)new SerializedObject(owner).FindProperty(field).objectReferenceValue;
        private static void Set(UnityEngine.Object owner, string field, UnityEngine.Object value)
        { var data = new SerializedObject(owner); data.FindProperty(field).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo(); }
        private static void BuildHud(GameObject root, EmployeeTaskValidationController controller)
        {
            // Reuse the same Canvas and children when reinstalling; no duplicate listeners/EventSystem.
            var hud = root.transform.Find("P13 Task HUD");
            if (hud == null)
            {
                var go = new GameObject("P13 Task HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                go.transform.SetParent(root.transform, false); hud = go.transform;
                var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 45;
                var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(360, 640); scaler.matchWidthOrHeight = .5f;
                var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image)); panel.transform.SetParent(hud, false);
                var rect = panel.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = new Vector2(1, 0); rect.pivot = new Vector2(.5f, 0);
                rect.anchoredPosition = new Vector2(0, 8); rect.sizeDelta = new Vector2(-16, 148);
                panel.GetComponent<Image>().color = new Color(.08f, .1f, .12f, .93f); panel.GetComponent<Image>().raycastTarget = false;
                var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Phase5/Fonts/NotoSansSC-Regular.otf");
                var status = Label(panel.transform, "Status", font); status.fontSize = 10; status.alignment = TextAnchor.UpperLeft;
                var sr = status.rectTransform; sr.anchorMin = new Vector2(0, .3f); sr.anchorMax = Vector2.one; sr.offsetMin = new Vector2(6, 4); sr.offsetMax = new Vector2(-6, -4);
                MakeButton(panel.transform, "Cashier Task", "收银服务", font, 0);
                MakeButton(panel.transform, "Barista Task", "制作咖啡", font, .5f);
            }
            var statusText = hud.Find("Panel/Status").GetComponent<Text>();
            // P14复用此Canvas；旧installer不能覆盖只读HUD的字号策略。
            if(All<AnimalCafe.CafeLoop.CafeLoopStatusView>(root.scene).Length==0)
            { statusText.resizeTextForBestFit = true; statusText.resizeTextMinSize = 8; statusText.resizeTextMaxSize = 10; }
            Set(controller, "hud", hud.gameObject); Set(controller, "status", statusText);
            Set(controller, "cashierButton", hud.Find("Panel/Cashier Task").GetComponent<Button>());
            Set(controller, "baristaButton", hud.Find("Panel/Barista Task").GetComponent<Button>());
            // Repair existing generated HUDs as well as first creation / 修复已存在的旧布局。
            ConfigureCaption(hud.Find("Panel/Cashier Task"), "收银服务");
            ConfigureCaption(hud.Find("Panel/Barista Task"), "制作咖啡");
        }
        private static Text Label(Transform parent, string name, Font font)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>(); text.font = font; text.color = Color.white; text.raycastTarget = false; return text;
        }
        private static void MakeButton(Transform parent, string name, string caption, Font font, float left)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(left, 0); rect.anchorMax = new Vector2(left + .5f, 0);
            rect.pivot = new Vector2(.5f, 0); rect.offsetMin = new Vector2(6, 4); rect.offsetMax = new Vector2(-6, 42);
            go.GetComponent<Image>().color = new Color(.25f, .38f, .48f);
            Label(go.transform, "Label", font); ConfigureCaption(go.transform, caption);
        }
        private static void ConfigureCaption(Transform button, string caption)
        {
            var text = button.Find("Label").GetComponent<Text>(); text.text = caption; text.fontSize = 16; text.alignment = TextAnchor.MiddleCenter;
            var rect = text.rectTransform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
