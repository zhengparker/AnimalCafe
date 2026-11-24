using System;
using System.IO;
using System.Linq;
using AnimalCafe.BusinessDay;
using AnimalCafe.CafeLoop;
using AnimalCafe.Customers;
using AnimalCafe.EditorTools.Phase14;
using AnimalCafe.UI.Components;
using AnimalCafe.UI.Foundation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AnimalCafe.EditorTools.Phase15
{
    public static class Phase15BusinessDaySceneSetup
    {
        public const string ScenePath = "Assets/Scenes/Validation/Phase15BusinessDay.unity";
        public const string MainPath = "Assets/Scenes/MainCafe.unity";
        [MenuItem("Tools/AnimalCafe/Phase 15/Create Business Day Validation Scene")]
        public static void BuildValidationScene() => Build(ScenePath, true);
        [MenuItem("Tools/AnimalCafe/Phase 15/Install Business Day in MainCafe")]
        public static void InstallInMainCafe() => Build(MainPath, false);
        private static void Build(string path, bool validation)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play before installing P15.");
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save dirty Scenes before installing P15.");
            // 独立复制已验证P14；不运行会重建MainCafe/prefab的旧builder。
            if (validation && !File.Exists(path) && !AssetDatabase.CopyAsset(Phase14CafeLoopSceneSetup.ScenePath, path))
                throw new InvalidOperationException("Could not copy independent P15 validation Scene.");
            var previous = SceneManager.GetActiveScene();
            var loaded = SceneManager.GetSceneByPath(path); var close = !(loaded.IsValid() && loaded.isLoaded);
            var scene = close ? EditorSceneManager.OpenScene(path, OpenSceneMode.Additive) : loaded;
            try
            {
                SceneManager.SetActiveScene(scene);
                var loop = All<CafeLoopController>(scene).Single();
                var customers = All<CustomerFlowController>(scene).Single();
                var owners = All<BusinessDayController>(scene);
                if (owners.Length > 1) throw new InvalidOperationException("Duplicate Business Day owners require manual repair.");
                var owner = owners.SingleOrDefault() ?? loop.gameObject.AddComponent<BusinessDayController>();
                Set(owner, "loop", loop); Number(owner, "openSeconds", validation ? 60 : 28800);
                Number(owner, "restSeconds", validation ? 15 : 7200); Flag(owner, "validationMode", validation);
                Flag(customers, "requireAdmissionPolicy", true);
                var serviceView = All<CafeLoopStatusView>(scene).Single();
                serviceView.SetDiagnosticPresentation(validation);
                EditorUtility.SetDirty(serviceView);
                var serviceText = (Text)new SerializedObject(serviceView).FindProperty("status").objectReferenceValue;
                var safe = serviceText.GetComponentInParent<SafeAreaContainer>();
                if (safe == null) throw new InvalidOperationException("P14 SafeArea must exist before P15 installation.");
                var panel = safe.transform.Find("P15 Readonly Status");
                if (panel == null)
                {
                    panel = new GameObject("P15 Readonly Status", typeof(RectTransform), typeof(Image)).transform;
                    panel.SetParent(safe.transform, false);
                }
                var rect = (RectTransform)panel;
                rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1);
                rect.offsetMin = new Vector2(8, -184); rect.offsetMax = new Vector2(-8, -68);
                var theme = AssetDatabase.LoadAssetAtPath<AnimalCafeUiTheme>("Assets/UI/Phase5/Theme/AnimalCafeUiTheme.asset");
                var rounded = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Phase5/Sprites/S_UI_RoundedRect.asset");
                if (theme == null || rounded == null || theme.Typography.Body.FontAsset == null)
                    throw new InvalidOperationException("P5 theme, rounded sprite and body source font are required.");
                // Static TMP assets keep their source GUID even when the runtime source reference is empty.
                // 静态TMP资源使用既有source GUID查找同一字体，不修改font或theme。
                var fontData = new SerializedObject(theme.Typography.Body.FontAsset);
                var bodyFont = AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath(fontData.FindProperty("m_SourceFontFileGUID").stringValue));
                if (bodyFont == null) throw new InvalidOperationException("P5 body source font is unavailable.");
                var horizontal = validation ? 8 : Mathf.RoundToInt(theme.Spacing.Small);
                var vertical = validation ? 6 : Mathf.RoundToInt(theme.Spacing.ExtraSmall);
                var background = panel.GetComponent<Image>();
                background.color = validation ? new Color(.08f, .1f, .12f, .93f) : theme.Colors.Surface;
                background.sprite = validation ? null : rounded;
                background.type = validation ? Image.Type.Simple : Image.Type.Sliced;
                background.raycastTarget = false;
                // 长Closing原因需要真实换行扩展，不缩小字体或吞掉停止原因。
                var layout = panel.GetComponent<VerticalLayoutGroup>() ?? panel.gameObject.AddComponent<VerticalLayoutGroup>();
                // ExecuteAlways布局组件在编辑态会改写Rect并弄脏Scene；由View仅在Play启用。
                layout.enabled = false;
                layout.padding = new RectOffset(horizontal, horizontal, vertical, vertical); layout.childControlWidth = true; layout.childControlHeight = true;
                layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
                var fit = panel.GetComponent<ContentSizeFitter>() ?? panel.gameObject.AddComponent<ContentSizeFitter>();
                fit.enabled = false;
                fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained; fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                var label = panel.Find("Status");
                if (label == null) { label = new GameObject("Status", typeof(RectTransform), typeof(Text)).transform; label.SetParent(panel, false); }
                var text = label.GetComponent<Text>();
                text.font = validation ? serviceText.font : bodyFont;
                text.fontSize = validation ? 14 : Mathf.RoundToInt(theme.Typography.Body.FontSize);
                text.lineSpacing = validation ? 1 : 1 + theme.Typography.Body.LineSpacing / theme.Typography.Body.FontSize;
                text.color = validation ? serviceText.color : theme.Colors.Text;
                text.alignment = TextAnchor.UpperLeft; text.raycastTarget = false;
                text.resizeTextForBestFit = false; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
                text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
                text.rectTransform.offsetMin = new Vector2(horizontal, vertical); text.rectTransform.offsetMax = new Vector2(-horizontal, -vertical);
                var views = All<BusinessDayStatusView>(scene);
                if (views.Length > 1) throw new InvalidOperationException("Duplicate Business Day views require manual repair.");
                var view = views.SingleOrDefault() ?? loop.gameObject.AddComponent<BusinessDayStatusView>();
                Set(view, "status", text); Set(view, "topControls", All<AnimalCafe.UI.TimeControlPanel>(scene).Single().transform);
                view.Bind(owner);
                EditorUtility.SetDirty(view); EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, path)) throw new InvalidOperationException("P15 Scene save failed.");
                Debug.Log("AC_P15_SCENE_READY " + path);
            }
            finally
            { if (close) EditorSceneManager.CloseScene(scene, true); if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); }
        }
        private static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
        private static void Set(UnityEngine.Object owner, string name, UnityEngine.Object value)
        { var data = new SerializedObject(owner); data.FindProperty(name).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo(); }
        private static void Number(UnityEngine.Object owner, string name, double value)
        { var data = new SerializedObject(owner); data.FindProperty(name).doubleValue = value; data.ApplyModifiedPropertiesWithoutUndo(); }
        private static void Flag(UnityEngine.Object owner, string name, bool value)
        { var data = new SerializedObject(owner); data.FindProperty(name).boolValue = value; data.ApplyModifiedPropertiesWithoutUndo(); }
    }
}

