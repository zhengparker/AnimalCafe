using System;
using System.IO;
using System.Linq;
using AnimalCafe.Customers;
using AnimalCafe.Employees;
using AnimalCafe.EditorTools.Phase13;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnimalCafe.Tests.EditMode.Phase13
{
    public sealed class Phase13SceneBuilderTests
    {
        [Test] public void FirstBuildNeverRepurposesAnAlreadyOpenP12Scene()
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();
            const string parked = "Assets/Tests/EditMode/Phase13/Task7ParkedValidation.unity";
            var sourceBytes = File.ReadAllBytes("Assets/Scenes/Validation/Phase12CustomerQueue.unity");
            Assert.That(File.Exists(parked), Is.False, "Never overwrite an existing test asset");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Assert.That(AssetDatabase.MoveAsset(Phase13EmployeeTaskSceneSetup.ScenePath, parked), Is.Empty);
            try
            {
                var original = EditorSceneManager.OpenScene("Assets/Scenes/Validation/Phase12CustomerQueue.unity", OpenSceneMode.Single);
                var roots = original.GetRootGameObjects();
                Phase13EmployeeTaskSceneSetup.BuildValidationScene();
                Assert.That(original.path, Is.EqualTo("Assets/Scenes/Validation/Phase12CustomerQueue.unity"), "An open P12 Scene must keep its identity");
                Assert.That(original.GetRootGameObjects(), Is.EqualTo(roots), "No P13 root may be added to the original P12 Scene");
                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(original)); Assert.That(original.isDirty, Is.False);
                var created = EditorSceneManager.OpenScene(Phase13EmployeeTaskSceneSetup.ScenePath, OpenSceneMode.Additive);
                AssertCaptions(created); EditorSceneManager.CloseScene(created, true);
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                // Delete only the fresh test-generated copy, then restore the parked original through Editor APIs.
                if (File.Exists(Phase13EmployeeTaskSceneSetup.ScenePath)) Assert.That(AssetDatabase.DeleteAsset(Phase13EmployeeTaskSceneSetup.ScenePath), Is.True);
                Assert.That(AssetDatabase.MoveAsset(parked, Phase13EmployeeTaskSceneSetup.ScenePath), Is.Empty);
                if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
                Assert.That(File.ReadAllBytes("Assets/Scenes/Validation/Phase12CustomerQueue.unity"), Is.EqualTo(sourceBytes));
            }
        }
        [Test] public void MainCafeInstallPreservesExistingWiringAndAutoSpawn()
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainCafe.unity", OpenSceneMode.Single);
                var original = scene.GetRootGameObjects().Where(root => root.name != "P13 Employees")
                    .SelectMany(root => root.GetComponentsInChildren<Component>(true)).Where(component => component != null)
                    .ToDictionary(component => component, component => EditorJsonUtility.ToJson(component));
                var customer = All<CustomerFlowController>(scene).Single(); Assert.That(customer.AutoSpawn, Is.True);
                var active = SceneManager.GetActiveScene();
                Phase13EmployeeTaskSceneSetup.BuildMainCafe(); Phase13EmployeeTaskSceneSetup.BuildMainCafe();
                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(active));
                Assert.That(All<EmployeeFlowController>(scene).Length, Is.EqualTo(1));
                Assert.That(All<CustomerFlowController>(scene).Single(), Is.SameAs(customer)); Assert.That(customer.AutoSpawn, Is.True);
                AssertCaptions(scene);
                foreach (var pair in original) Assert.That(EditorJsonUtility.ToJson(pair.Key), Is.EqualTo(pair.Value), pair.Key.name + " / " + pair.Key.GetType().Name);
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }
        [Test] public void BuildIsIdempotentAndProtectsOtherScenes()
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var protectedPaths = new[] { "Assets/Scenes/Validation/Phase11Navigation.unity", "Assets/Scenes/Validation/Phase12CustomerQueue.unity", "Assets/Art/Phase4/Prefabs/PF_Item_CeramicCup_01.prefab" };
            var before = protectedPaths.Select(File.ReadAllBytes).ToArray();
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Phase13EmployeeTaskSceneSetup.BuildValidationScene();
                Assert.That(File.Exists(Phase13EmployeeTaskSceneSetup.ScenePath), Is.True, "Builder must publish a standalone P13 scene");
                Phase13EmployeeTaskSceneSetup.BuildValidationScene();
                var scene = EditorSceneManager.OpenScene(Phase13EmployeeTaskSceneSetup.ScenePath, OpenSceneMode.Single);
                Assert.That(All<EmployeeFlowController>(scene).Length, Is.EqualTo(1));
                Assert.That(All<PickupCupDisplay>(scene).Length, Is.EqualTo(1));
                Assert.That(All<UnityEngine.EventSystems.EventSystem>(scene).Length, Is.EqualTo(1));
                Assert.That(All<CustomerFlowController>(scene).Single().AutoSpawn, Is.False);
                AssertCaptions(scene);
                Assert.That(new SerializedObject(All<EmployeeFlowController>(scene).Single()).FindProperty("cupPrefab").objectReferenceValue,
                    Is.EqualTo(AssetDatabase.LoadAssetAtPath<GameObject>(protectedPaths[2])));
                var marker = new GameObject("Owner unsaved work"); EditorSceneManager.MarkSceneDirty(scene);
                Assert.Throws<InvalidOperationException>(() => Phase13EmployeeTaskSceneSetup.BuildValidationScene());
                Assert.That(marker != null && scene.isDirty, Is.True);
                Assert.Throws<InvalidOperationException>(() => Phase13EmployeeTaskSceneSetup.BuildMainCafe());
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
                for (var i = 0; i < protectedPaths.Length; i++) Assert.That(File.ReadAllBytes(protectedPaths[i]), Is.EqualTo(before[i]), protectedPaths[i]);
            }
        }
        private static void AssertCaptions(Scene scene)
        {
            var hud = All<EmployeeTaskValidationController>(scene).Single().transform.Find("P13 Task HUD/Panel");
            foreach (var entry in new[] { (name: "Cashier Task", caption: "收银服务"), (name: "Barista Task", caption: "制作咖啡") })
            {
                var label = hud.Find(entry.name + "/Label").GetComponent<UnityEngine.UI.Text>();
                Assert.That(label.text, Is.EqualTo(entry.caption));
                Assert.That(label.rectTransform.anchorMin, Is.EqualTo(Vector2.zero), "New and existing HUD captions must both be full-stretch");
                Assert.That(label.rectTransform.anchorMax, Is.EqualTo(Vector2.one));
                Assert.That(label.rectTransform.offsetMin, Is.EqualTo(Vector2.zero)); Assert.That(label.rectTransform.offsetMax, Is.EqualTo(Vector2.zero));
            }
        }
        private static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
    }
}
