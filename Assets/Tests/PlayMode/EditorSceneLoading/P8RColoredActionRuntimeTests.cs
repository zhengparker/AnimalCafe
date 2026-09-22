#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using AnimalCafe.Decoration;
using AnimalCafe.UI.Decoration;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AnimalCafe.Tests.PlayMode.EditorSceneLoading
{
    public sealed class P8RColoredActionRuntimeTests
    {
        private const string ScenePath = "Assets/Scenes/MainCafe.unity";
        private const string ColoredRoot = "Assets/UI/P8R/ActionIcons/action_";
        private const string LegacyRoot = "Assets/UI/P8R/RefinedB/Icons/";
        private Scene ownedScene;

        [UnityTearDown]
        public IEnumerator ReleaseOwnedScene()
        {
            if (!ownedScene.IsValid() || !ownedScene.isLoaded) yield break;
            var inputAssets = Phase8SceneInputTestCleanup.CaptureAssets(ownedScene);
            foreach (var controller in ownedScene.GetRootGameObjects()
                         .SelectMany(root => root.GetComponentsInChildren<DecorationModeController>(true)))
                controller.enabled = false;
            var cleanup = SceneManager.CreateScene("P8RColoredActionRuntimeCleanup");
            SceneManager.SetActiveScene(cleanup);
            var unload = SceneManager.UnloadSceneAsync(ownedScene);
            while (unload != null && !unload.isDone) yield return null;
            Phase8SceneInputTestCleanup.DisposeReleasedAssets(inputAssets);
        }

        [UnityTest]
        public IEnumerator MainCafe_ActionsConsumeColoredAppearanceAcrossEntryReopenAndCrampedLayout()
        {
            ownedScene = EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;
            yield return null;

            var controller = Find<DecorationModeController>();
            var catalogue = Find<DecorationCatalogueView>();
            var hudButton = Field<Button>(controller, "decorationModeButton");
            AssertAction(hudButton, "decorate", iconOnly: true);

            controller.EnterDecorationMode();
            yield return null;
            Canvas.ForceUpdateCanvases();
            AssertAction(hudButton, "exit", iconOnly: true);

            var pickup = Field<Button>(catalogue, "pickUpPointButton");
            Assert.That(pickup.interactable, Is.True, "The enabled Furniture Pickup action must use cocoa art.");
            AssertAction(pickup, "pickup", iconOnly: false);
            var expectedSize = ((RectTransform)pickup.transform).rect.size;

            // Reopening and re-enabling are real refresh paths which must replace a stale serialized icon.
            // 重开与重新启用都必须从 Appearance 恢复图标，不能继续显示 prefab 中的旧引用。
            SeedLegacy(pickup, "pickup");
            catalogue.Hide();
            catalogue.ShowCatalogue();
            yield return null;
            AssertAction(pickup, "pickup", iconOnly: false);

            SeedLegacy(pickup, "pickup");
            catalogue.enabled = false;
            catalogue.enabled = true;
            yield return null;
            AssertAction(pickup, "pickup", iconOnly: false);

            var catalogueRect = (RectTransform)catalogue.transform;
            catalogueRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                Mathf.Min(catalogueRect.rect.height, 900f));
            Canvas.ForceUpdateCanvases();
            yield return null;
            AssertAction(pickup, "pickup", iconOnly: false);
            Assert.That(((RectTransform)pickup.transform).rect.size, Is.EqualTo(expectedSize),
                "Responsive refresh must not change the approved Pickup button size.");
            P8RCompleteFlowTests.AssertTextIconGroup(pickup);
        }

        private T Find<T>() where T : Object => ownedScene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();

        private static T Field<T>(object owner, string name) => (T)owner.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);

        private static void SeedLegacy(Button button, string action)
        {
            var legacy = AssetDatabase.LoadAssetAtPath<Sprite>(LegacyRoot + action + "_cocoa.png");
            Assert.That(legacy, Is.Not.Null, action + " legacy fixture");
            button.transform.Find("Icon").GetComponent<Image>().sprite = legacy;
        }

        private static void AssertAction(Button button, string action, bool iconOnly)
        {
            var icon = button.transform.Find("Icon").GetComponent<Image>();
            Assert.That(AssetDatabase.GetAssetPath(icon.sprite),
                Is.EqualTo(ColoredRoot + action + "_color.png"), action + " runtime icon");
            Assert.That(icon.color, Is.EqualTo(Color.white), action + " icon tint");
            Assert.That(icon.type, Is.EqualTo(Image.Type.Simple), action + " icon type");
            Assert.That(icon.preserveAspect, Is.True, action + " preserve aspect");
            Assert.That(icon.raycastTarget, Is.False, action + " decorative icon raycast");
            Assert.That(icon.gameObject.activeSelf, Is.True, action + " icon visible");
            var label = button.transform.Find("Label").GetComponent<TMP_Text>();
            Assert.That(label.gameObject.activeSelf, Is.EqualTo(!iconOnly), action + " label visibility");
            if (action == "pickup") Assert.That(label.text, Is.EqualTo("Pickup Point"));
        }
    }
}
#endif
