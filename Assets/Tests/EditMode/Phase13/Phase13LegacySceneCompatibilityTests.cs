using System;
using System.IO;
using System.Linq;
using AnimalCafe.Core.Time;
using AnimalCafe.CafeLoop;
using AnimalCafe.Customers;
using AnimalCafe.Employees;
using AnimalCafe.EditorTools.Phase8;
using AnimalCafe.EditorTools.Phase12;
using AnimalCafe.Navigation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnimalCafe.Tests.EditMode.Phase13
{
    public sealed class Phase13LegacySceneCompatibilityTests
    {
        private const string Main = "Assets/Scenes/MainCafe.unity";
        [TestCase("valid", false)]
        [TestCase("unknown", true)]
        [TestCase("duplicate-p13", true)]
        [TestCase("missing-core", true)]
        [TestCase("name-only", true)]
        [TestCase("wrong-owner", true)]
        public void Phase8CanvasGateAcceptsOnlyTheOwnedEmployeeHud(string variant, bool rejected)
        {
            var setup = EditorSceneManager.GetSceneManagerSetup(); var disk = File.ReadAllBytes(Main);
            try
            {
                var scene = EditorSceneManager.OpenScene(Main, OpenSceneMode.Single);
                var controller = All<EmployeeTaskValidationController>(scene).Single();
                var hud = Ref<GameObject>(controller, "hud");
                if (variant == "unknown") SceneManager.MoveGameObjectToScene(new GameObject("Unknown extra Canvas", typeof(Canvas)), scene);
                if (variant == "duplicate-p13") UnityEngine.Object.Instantiate(hud, controller.transform);
                if (variant == "missing-core") UnityEngine.Object.DestroyImmediate(All<Canvas>(scene).Single(canvas => canvas.name == "HUD Canvas").gameObject);
                if (variant == "name-only") UnityEngine.Object.DestroyImmediate(controller);
                if (variant == "wrong-owner")
                {
                    var data = new SerializedObject(controller); data.FindProperty("hud").objectReferenceValue = All<Canvas>(scene).Single(canvas => canvas.name == "Toast Canvas").gameObject;
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                var issues = Phase8Validator.ValidateOpenScene().Issues;
                Assert.That(issues.Any(issue => issue.Code == "P8-SCENE-CANVAS"), Is.EqualTo(rejected), variant);
                if (!rejected) Assert.That(issues, Is.Empty, string.Join("; ", issues.Select(issue => issue.Code)));
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
                Assert.That(File.ReadAllBytes(Main), Is.EqualTo(disk), "Validation never saves the corruption");
            }
        }

        [Test] public void Phase12ValidationCopyExcludesEmployeesAndPreservesLoadedMainCafe()
        {
            var setup = EditorSceneManager.GetSceneManagerSetup(); var mainBytes = File.ReadAllBytes(Main);
            var p12Bytes = File.ReadAllBytes(Phase12CustomerQueueSceneSetup.ScenePath);
            var p12Meta = File.ReadAllBytes(Phase12CustomerQueueSceneSetup.ScenePath + ".meta");
            const string parked = "Assets/Tests/EditMode/Phase13/CompatParkedP12.unity";
            Assert.That(File.Exists(parked), Is.False);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Assert.That(AssetDatabase.MoveAsset(Phase12CustomerQueueSceneSetup.ScenePath, parked), Is.Empty);
            try
            {
                var main = EditorSceneManager.OpenScene(Main, OpenSceneMode.Single);
                var employees = All<EmployeeFlowController>(main).Single(); var roots = main.GetRootGameObjects();
                Phase12CustomerQueueSceneSetup.CreateValidationScene();
                Assert.That(main.IsValid() && main.isLoaded, Is.True, "Keep the user's clean loaded MainCafe");
                Assert.That(main.path, Is.EqualTo(Main)); Assert.That(main.GetRootGameObjects(), Is.EqualTo(roots));
                Assert.That(All<EmployeeFlowController>(main).Single(), Is.SameAs(employees));
                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(main)); Assert.That(main.isDirty, Is.False);
                Assert.That(File.ReadAllBytes(Main), Is.EqualTo(mainBytes));
                var validation = EditorSceneManager.OpenScene(Phase12CustomerQueueSceneSetup.ScenePath, OpenSceneMode.Additive);
                Assert.That(All<EmployeeFlowController>(validation), Is.Empty);
                Assert.That(All<EmployeeTaskValidationController>(validation), Is.Empty);
                Assert.That(All<PickupCupDisplay>(validation), Is.Empty);
                Assert.That(All<CafeLoopController>(validation),Is.Empty,"Independent P12 must not retain the copied P14 owner");
                Assert.That(All<CafeLoopValidationController>(validation),Is.Empty);
                Assert.That(All<CafeLoopStatusView>(validation),Is.Empty);
                Assert.That(All<Canvas>(validation).Any(canvas => canvas.name == "P13 Task HUD"), Is.False);
                Assert.That(All<CustomerFlowController>(validation).Length, Is.EqualTo(1));
                Assert.That(All<CustomerFlowController>(validation).Single().AutoSpawn, Is.True);
                Assert.That(new SerializedObject(All<CustomerFlowController>(validation).Single()).FindProperty("integratedMode").boolValue,Is.False);
                Assert.That(All<CustomerQueueValidationController>(validation).Length, Is.EqualTo(1));
                Assert.That(All<NavigationWorld>(validation).Length, Is.EqualTo(1));
                Assert.That(All<GameTimeService>(validation).Length, Is.EqualTo(1));
                Assert.That(All<NavigationLayoutAdapter>(validation).Single().EnforceBusinessReadiness, Is.True);
                Assert.That(All<Canvas>(validation).Count(canvas => canvas.name == "P12 Validation HUD"), Is.EqualTo(1));
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (File.Exists(Phase12CustomerQueueSceneSetup.ScenePath)) Assert.That(AssetDatabase.DeleteAsset(Phase12CustomerQueueSceneSetup.ScenePath), Is.True);
                Assert.That(AssetDatabase.MoveAsset(parked, Phase12CustomerQueueSceneSetup.ScenePath), Is.Empty);
                if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
                Assert.That(File.ReadAllBytes(Phase12CustomerQueueSceneSetup.ScenePath), Is.EqualTo(p12Bytes));
                Assert.That(File.ReadAllBytes(Phase12CustomerQueueSceneSetup.ScenePath + ".meta"), Is.EqualTo(p12Meta));
                Assert.That(File.ReadAllBytes(Main), Is.EqualTo(mainBytes));
            }
        }
        private static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
        private static T Ref<T>(UnityEngine.Object owner, string name) where T : UnityEngine.Object => (T)new SerializedObject(owner).FindProperty(name).objectReferenceValue;
    }
}
