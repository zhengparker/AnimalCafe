using System;
using System.IO;
using System.Linq;
using AnimalCafe.CafeLoop;
using AnimalCafe.Customers;
using AnimalCafe.Decoration;
using AnimalCafe.Employees;
using AnimalCafe.Navigation;
using AnimalCafe.EditorTools.Phase13;
using AnimalCafe.EditorTools.Phase14;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace AnimalCafe.Tests.EditMode.Phase14
{
    public sealed class Phase14SceneBuilderTests
    {
        private SceneSetup[] setup;
        [SetUp] public void Setup() { setup=EditorSceneManager.GetSceneManagerSetup(); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single); }
        [TearDown] public void Cleanup()
        { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single); if(setup.Length>0) EditorSceneManager.RestoreSceneManagerSetup(setup); }
        [Test] public void InstallerIsIdempotentAndPreservesExistingLayoutAndRoster()
        {
            var scene=EditorSceneManager.OpenScene(Phase14CafeLoopSceneSetup.MainPath,OpenSceneMode.Single);
            var customers=All<CustomerFlowController>(scene).Single(); var employees=All<EmployeeFlowController>(scene).Single();
            var runtime=All<CafeLayoutRuntime>(scene).Single(); var runtimeJson=EditorJsonUtility.ToJson(runtime);
            var canvases=All<Canvas>(scene); var auto=customers.AutoSpawn;
            Phase14CafeLoopSceneSetup.BuildMainCafe(); Assert.That(All<CafeLoopController>(scene).Length,Is.EqualTo(1)); var owner=All<CafeLoopController>(scene).Single();
            Phase14CafeLoopSceneSetup.BuildMainCafe(); Assert.That(All<CafeLoopController>(scene).Single(),Is.SameAs(owner));
            Assert.That(owner.name,Is.EqualTo("P14 Cafe Loop")); Assert.That(All<CafeLoopValidationController>(scene).Length,Is.EqualTo(1));
            Assert.That(All<CafeLoopStatusView>(scene).Length,Is.EqualTo(1));
            Assert.That(All<CustomerFlowController>(scene).Single(),Is.SameAs(customers)); Assert.That(customers.AutoSpawn,Is.EqualTo(auto));
            Assert.That(All<EmployeeFlowController>(scene).Single(),Is.SameAs(employees));
            Assert.That(EditorJsonUtility.ToJson(runtime),Is.EqualTo(runtimeJson)); CollectionAssert.AreEquivalent(canvases,All<Canvas>(scene));
            Assert.That(All<UnityEngine.EventSystems.EventSystem>(scene).Length,Is.EqualTo(1));
            Assert.That(All<EmployeeTaskValidationController>(scene).Single().enabled,Is.False);
            Assert.That(new SerializedObject(customers).FindProperty("integratedMode").boolValue,Is.True);
            Assert.That(All<Canvas>(scene).Single(c=>c.name=="P13 Task HUD").GetComponentsInChildren<Button>(true).All(b=>!b.gameObject.activeSelf),Is.True);
        }
        [Test] public void InstallerRejectsDirtySceneWithoutSavingOwnerWork()
        {
            var scene=EditorSceneManager.OpenScene(Phase14CafeLoopSceneSetup.MainPath,OpenSceneMode.Single);
            var bytes=File.ReadAllBytes(scene.path); var marker=new GameObject("Owner unsaved marker"); EditorSceneManager.MarkSceneDirty(scene);
            Assert.Throws<InvalidOperationException>(()=>Phase14CafeLoopSceneSetup.BuildMainCafe());
            Assert.Throws<InvalidOperationException>(()=>Phase14CafeLoopSceneSetup.BuildValidationScene());
            Assert.That(marker!=null && scene.isDirty,Is.True); Assert.That(File.ReadAllBytes(scene.path),Is.EqualTo(bytes));
        }
        [Test] public void InstallerWiresBothCustomerCarryAppearancesFromAcceptedAssets()
        {
            Phase14CafeLoopSceneSetup.BuildMainCafe();
            var scene=EditorSceneManager.OpenScene(Phase14CafeLoopSceneSetup.MainPath,OpenSceneMode.Single);
            var data=new SerializedObject(All<CustomerFlowController>(scene).Single());
            Assert.That(data.FindProperty("collectedCupPrefab").objectReferenceValue,Is.Not.Null);
            var appearances=data.FindProperty("collectedCarryAppearances"); Assert.That(appearances.arraySize,Is.EqualTo(2));
            foreach(var species in new[]{"Shiba","Westie"})
            {
                var matches=Enumerable.Range(0,appearances.arraySize).Select(appearances.GetArrayElementAtIndex)
                    .Where(p=>((NavigationActor)p.FindPropertyRelative("CustomerPrefab").objectReferenceValue).ActorId==species).ToArray();
                Assert.That(matches.Length,Is.EqualTo(1));
                var controller=(RuntimeAnimatorController)matches[0].FindPropertyRelative("CarryController").objectReferenceValue;
                Assert.That(controller,Is.Not.Null); Assert.That(controller.name,Does.Contain(species));
                CollectionAssert.IsSubsetOf(new[]{"Walk","Carry"},controller.animationClips.Select(c=>c.name).Distinct().ToArray());
                var animator=(UnityEditor.Animations.AnimatorController)controller;
                Assert.That(animator.layers.Length,Is.EqualTo(2)); Assert.That(animator.layers[1].avatarMask,Is.Not.Null);
                Assert.That(matches[0].FindPropertyRelative("CupLocalScale").vector3Value.sqrMagnitude,Is.GreaterThan(0));
            }
        }
        [Test] public void DedicatedCustomerPrefabsKeepAcceptedRigBodyAndNoEmployeePresenter()
        {
            Phase14CafeLoopSceneSetup.BuildMainCafe();
            foreach(var species in new[]{"Shiba","Westie"})
            {
                var path="Assets/Art/Phase14/Customers/PF_"+species+"_P14Customer.prefab";
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path); Assert.That(prefab,Is.Not.Null,path);
                var customer=prefab.GetComponent<NavigationActor>();
                var employee=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Phase13/Employees/"+species+"/PF_"+species+"_Employee.prefab").GetComponent<NavigationActor>();
                Assert.That(prefab.GetComponent<EmployeeVisualPresenter>(),Is.Null);
                Assert.That(prefab.GetComponent<NavigationWalkPresenter>(),Is.Not.Null);
                Assert.That(customer.ActorId,Is.EqualTo(species));
                Assert.That(JsonUtility.ToJson(customer.Settings),Is.EqualTo(JsonUtility.ToJson(employee.Settings)));
                Assert.That(customer.Proxy.radius,Is.EqualTo(employee.Proxy.radius)); Assert.That(customer.Proxy.height,Is.EqualTo(employee.Proxy.height));
                Assert.That(customer.Proxy.center,Is.EqualTo(employee.Proxy.center)); Assert.That(customer.Proxy.direction,Is.EqualTo(employee.Proxy.direction));
                Assert.That(customer.Proxy.contactOffset,Is.EqualTo(employee.Proxy.contactOffset)); Assert.That(customer.Proxy.isTrigger,Is.EqualTo(employee.Proxy.isTrigger));
                Assert.That(customer.Agent.radius,Is.EqualTo(employee.Agent.radius)); Assert.That(customer.Agent.height,Is.EqualTo(employee.Agent.height));
                Assert.That(customer.Agent.speed,Is.EqualTo(employee.Agent.speed)); Assert.That(customer.Agent.acceleration,Is.EqualTo(employee.Agent.acceleration));
                Assert.That(customer.Agent.angularSpeed,Is.EqualTo(employee.Agent.angularSpeed)); Assert.That(customer.Agent.baseOffset,Is.EqualTo(employee.Agent.baseOffset));
                Assert.That(customer.Agent.stoppingDistance,Is.EqualTo(employee.Agent.stoppingDistance)); Assert.That(customer.Agent.enabled,Is.EqualTo(employee.Agent.enabled));
                var controller=(UnityEditor.Animations.AnimatorController)customer.ModelAnimator.runtimeAnimatorController;
                Assert.That(controller,Is.SameAs(employee.ModelAnimator.runtimeAnimatorController));
                Assert.That(controller.layers[1].defaultWeight,Is.Zero,"Uncollected customers use original Walk/Hold, no Carry");
                var paths=controller.animationClips.Where(c=>c.name=="Walk" || c.name=="Carry")
                    .SelectMany(AnimationUtility.GetCurveBindings).Select(b=>b.path).Where(p=>p.Length>0).Distinct().ToArray();
                Assert.That(paths.All(p=>customer.ModelAnimator.transform.Find(p)!=null),Is.True,"Full accepted rig, including attachment/skinning paths");
                var bytes=File.ReadAllBytes(path); var meta=File.ReadAllBytes(path+".meta");
                Phase14CafeLoopSceneSetup.BuildMainCafe();
                Assert.That(File.ReadAllBytes(path),Is.EqualTo(bytes)); Assert.That(File.ReadAllBytes(path+".meta"),Is.EqualTo(meta));
            }
        }
        [Test] public void ValidationInstallPreservesIndependentP12AndP13Assets()
        {
            var paths=new[]{"Assets/Scenes/Validation/Phase12CustomerQueue.unity","Assets/Scenes/Validation/Phase13EmployeeTasks.unity"};
            var bytes=paths.Select(File.ReadAllBytes).ToArray();
            Phase14CafeLoopSceneSetup.BuildValidationScene(); Phase14CafeLoopSceneSetup.BuildSharedValidationScene();
            foreach(var path in new[]{Phase14CafeLoopSceneSetup.ScenePath,Phase14CafeLoopSceneSetup.SharedScenePath})
            {
                Assert.That(File.Exists(path),Is.True,path); var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                Assert.That(All<CafeLoopController>(scene).Length,Is.EqualTo(1));
                Assert.That(All<CafeLoopValidationController>(scene).Length,Is.EqualTo(1)); EditorSceneManager.CloseScene(scene,true);
            }
            for(var i=0;i<paths.Length;i++) Assert.That(File.ReadAllBytes(paths[i]),Is.EqualTo(bytes[i]),paths[i]);
        }
        [Test] public void LegacyEmployeeReinstallCannotOverwriteReadonlyHudSettings()
        {
            Phase14CafeLoopSceneSetup.BuildMainCafe(); var scene=EditorSceneManager.OpenScene(Phase14CafeLoopSceneSetup.MainPath,OpenSceneMode.Single);
            Assert.That(All<CafeLoopStatusView>(scene).Length,Is.EqualTo(1));
            var text=All<EmployeeTaskValidationController>(scene).Single().transform.Find("P13 Task HUD/Panel/Status").GetComponent<Text>();
            Assert.That(text.resizeTextForBestFit,Is.False);
            var font=text.fontSize; var rect=EditorJsonUtility.ToJson(text.rectTransform);
            Phase13EmployeeTaskSceneSetup.BuildMainCafe(); Phase13EmployeeTaskSceneSetup.BuildMainCafe();
            Assert.That(text.resizeTextForBestFit,Is.False,"Legacy installer must preserve P14 readability");
            Assert.That(text.fontSize,Is.EqualTo(font)); Assert.That(EditorJsonUtility.ToJson(text.rectTransform),Is.EqualTo(rect));
            Assert.That(All<EmployeeTaskValidationController>(scene).Single().enabled,Is.False);
            Assert.That(new SerializedObject(All<CustomerFlowController>(scene).Single()).FindProperty("integratedMode").boolValue,Is.True);
        }
        [Test] public void ViewSnapshotCopiesMutableInputLists()
        {
            var queue=new System.Collections.Generic.List<QueueAssignment>{new QueueAssignment("visit-a",0,2,Vector3.zero,true)};
            var value=new CafeLoopViewSnapshot(new CafeLoopSnapshot("view",1),queue,reception:"Paused",visibleCups:3);
            queue.Clear(); Assert.That(value.Counter.Count,Is.EqualTo(1)); Assert.That(value.Counter[0].VisitId,Is.EqualTo("visit-a"));
            Assert.That(value.Reception,Is.EqualTo("Paused")); Assert.That(value.VisibleCups,Is.EqualTo(3));
        }
        [Test] public void ReadonlyHudUsesHeightFitAndSurvivesLegacyReinstall()
        {
            Phase14CafeLoopSceneSetup.BuildMainCafe(); var scene=EditorSceneManager.OpenScene(Phase14CafeLoopSceneSetup.MainPath,OpenSceneMode.Single);
            var scaler=All<Canvas>(scene).Single(c=>c.name=="P13 Task HUD").GetComponent<CanvasScaler>();
            Assert.That(scaler.matchWidthOrHeight,Is.EqualTo(1),"A portrait reference must not consume most of a landscape screen");
            Assert.That(scaler.referenceResolution,Is.EqualTo(new Vector2(360,640)));
            Phase13EmployeeTaskSceneSetup.BuildMainCafe(); Phase13EmployeeTaskSceneSetup.BuildMainCafe();
            Assert.That(scaler.matchWidthOrHeight,Is.EqualTo(1));
        }
        [Test] public void ValidationMutationMenusCannotChangeSerializedArrivalsOutsidePlay()
        {
            Phase14CafeLoopSceneSetup.BuildMainCafe(); var scene=EditorSceneManager.OpenScene(Phase14CafeLoopSceneSetup.MainPath,OpenSceneMode.Single);
            var customer=All<CustomerFlowController>(scene).Single(); var tools=All<CafeLoopValidationController>(scene).Single();
            customer.AutoSpawn=true; tools.HoldArrivals(); Assert.That(customer.AutoSpawn,Is.True);
            customer.AutoSpawn=false; tools.ResumeArrivals(); Assert.That(customer.AutoSpawn,Is.False);
        }
        private static T[] All<T>(Scene scene) where T:Component => scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();
    }
}
