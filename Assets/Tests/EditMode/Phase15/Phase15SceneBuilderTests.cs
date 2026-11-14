using System;
using System.IO;
using System.Linq;
using AnimalCafe.BusinessDay;
using AnimalCafe.CafeLoop;
using AnimalCafe.Customers;
using AnimalCafe.EditorTools.Phase14;
using AnimalCafe.EditorTools.Phase15;
using AnimalCafe.UI.Components;
using AnimalCafe.UI.Foundation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AnimalCafe.Tests.EditMode.Phase15
{
    public sealed class Phase15SceneBuilderTests
    {
        private SceneSetup[] setup;
        [SetUp] public void Setup()
        { setup = EditorSceneManager.GetSceneManagerSetup(); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
        [TearDown] public void Cleanup()
        { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup); }

        [Test] public void SceneBuilderInstallsOneDayOwnerAndRequiredPolicy()
        {
            Phase15BusinessDaySceneSetup.BuildValidationScene();
            Assert.That(File.Exists(Phase15BusinessDaySceneSetup.ScenePath), Is.True, "P15 builder must create its independent scene");
            var scene = EditorSceneManager.OpenScene(Phase15BusinessDaySceneSetup.ScenePath, OpenSceneMode.Single);
            var owner = All<BusinessDayController>(scene).Single();
            var view = All<BusinessDayStatusView>(scene).Single();
            var data = new SerializedObject(owner);
            Assert.That(data.FindProperty("loop").objectReferenceValue, Is.SameAs(All<CafeLoopController>(scene).Single()));
            Assert.That(new SerializedObject(All<CustomerFlowController>(scene).Single()).FindProperty("requireAdmissionPolicy").boolValue, Is.True);
            Phase15BusinessDaySceneSetup.BuildValidationScene();
            Assert.That(All<BusinessDayController>(scene).Single(), Is.SameAs(owner));
            Assert.That(All<BusinessDayStatusView>(scene).Single(), Is.SameAs(view));
            var label = (Text)new SerializedObject(view).FindProperty("status").objectReferenceValue;
            Assert.That(AssetDatabase.GetAssetPath(label.font), Is.EqualTo("Assets/UI/Phase5/Fonts/NotoSansSC-Regular.otf"));
            Assert.That(label.GetComponentInParent<SafeAreaContainer>(), Is.Not.Null);
            Assert.That(label.GetComponentInParent<CanvasScaler>().matchWidthOrHeight, Is.EqualTo(1));
            Assert.That(label.GetComponentInParent<Canvas>().GetComponentsInChildren<Graphic>(true)
                .Where(g => g.gameObject.activeInHierarchy).All(g => !g.raycastTarget), Is.True);
            Assert.That(label.GetComponentsInChildren<Selectable>(true), Is.Empty);
        }

        [Test] public void ValidationDurationsAreMarkedAndMainRemainsUnchanged()
        {
            var paths = new[] { Phase15BusinessDaySceneSetup.MainPath, Phase14CafeLoopSceneSetup.ScenePath,
                Phase14CafeLoopSceneSetup.SharedScenePath, "Assets/Art/Phase14/Customers/PF_Shiba_P14Customer.prefab",
                "Assets/Art/Phase14/Customers/PF_Westie_P14Customer.prefab" };
            var before = paths.Select(File.ReadAllBytes).ToArray();
            Phase15BusinessDaySceneSetup.BuildValidationScene();
            Assert.That(File.Exists(Phase15BusinessDaySceneSetup.ScenePath), Is.True);
            var scene = EditorSceneManager.OpenScene(Phase15BusinessDaySceneSetup.ScenePath, OpenSceneMode.Single);
            var data = new SerializedObject(All<BusinessDayController>(scene).Single());
            Assert.That(data.FindProperty("openSeconds").doubleValue, Is.EqualTo(60));
            Assert.That(data.FindProperty("restSeconds").doubleValue, Is.EqualTo(15));
            Assert.That(All<BusinessDayStatusView>(scene).Single().CurrentText, Does.Contain("Validation"));
            for (var i = 0; i < paths.Length; i++) Assert.That(File.ReadAllBytes(paths[i]), Is.EqualTo(before[i]), paths[i]);
            var defaults = new GameObject("Production defaults").AddComponent<BusinessDayController>();
            data = new SerializedObject(defaults);
            Assert.That(data.FindProperty("openSeconds").doubleValue, Is.EqualTo(28800));
            Assert.That(data.FindProperty("restSeconds").doubleValue, Is.EqualTo(7200));
        }

        [Test] public void BuilderRejectsDirtySceneWithoutOverwritingOwnerWork()
        {
            var scene = SceneManager.GetActiveScene();
            var marker = new GameObject("Unsaved owner work"); EditorSceneManager.MarkSceneDirty(scene);
            var before = File.ReadAllBytes(Phase15BusinessDaySceneSetup.MainPath);
            Assert.Throws<InvalidOperationException>(() => Phase15BusinessDaySceneSetup.BuildValidationScene());
            Assert.Throws<InvalidOperationException>(() => Phase15BusinessDaySceneSetup.InstallInMainCafe());
            Assert.That(marker != null && scene.isDirty, Is.True);
            Assert.That(File.ReadAllBytes(Phase15BusinessDaySceneSetup.MainPath), Is.EqualTo(before));
        }

        [Test] public void MainPlayerCardUsesThemeAndHidesOnlyServiceDiagnosticsOnRepeatedSetup()
        {
            Phase15BusinessDaySceneSetup.InstallInMainCafe();
            var scene = EditorSceneManager.OpenScene(Phase15BusinessDaySceneSetup.MainPath, OpenSceneMode.Single);
            var owner = All<BusinessDayController>(scene).Single();
            var view = All<BusinessDayStatusView>(scene).Single();
            Phase15BusinessDaySceneSetup.InstallInMainCafe();
            Assert.That(All<BusinessDayController>(scene).Single(), Is.SameAs(owner));
            Assert.That(All<BusinessDayStatusView>(scene).Single(), Is.SameAs(view));
            Assert.That(All<UnityEngine.EventSystems.EventSystem>(scene), Has.Length.EqualTo(1));
            var theme = AssetDatabase.LoadAssetAtPath<AnimalCafeUiTheme>("Assets/UI/Phase5/Theme/AnimalCafeUiTheme.asset");
            var text = (Text)new SerializedObject(view).FindProperty("status").objectReferenceValue;
            var background = text.transform.parent.GetComponent<Image>();
            Assert.That(background.color, Is.EqualTo(theme.Colors.Surface), "Main must use the actual P5 Surface token");
            Assert.That(background.sprite, Is.SameAs(AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Phase5/Sprites/S_UI_RoundedRect.asset")));
            Assert.That(background.type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(text.color, Is.EqualTo(theme.Colors.Text));
            Assert.That(text.fontSize, Is.EqualTo(Mathf.RoundToInt(theme.Typography.Body.FontSize)));
            Assert.That(text.transform.parent.GetComponent<VerticalLayoutGroup>().padding.left, Is.EqualTo(Mathf.RoundToInt(theme.Spacing.Small)));
            Assert.That(text.gameObject.activeInHierarchy, Is.True, "Do not hide the shared Canvas ancestor");
            var service = (Text)new SerializedObject(All<CafeLoopStatusView>(scene).Single()).FindProperty("status").objectReferenceValue;
            Assert.That(service.gameObject.activeInHierarchy && service.enabled, Is.False, "Main must hide P14 diagnostic text");
            var serviceBackground = service.transform.parent.Find("P14 Readonly Background").GetComponent<Image>();
            Assert.That(serviceBackground.gameObject.activeInHierarchy && serviceBackground.enabled, Is.False);
        }

        [Test] public void PlayerTextKeepsPhaseCountdownAndHonestInterruptedHistoryWithoutInternalFields()
        {
            var day = new BusinessDayService(new BusinessDaySettings(), 0);
            var open = BusinessDayStatusView.Format(day.GetSnapshot(0), false);
            Assert.That(open, Does.Contain("Day 1").And.Contain("Open").And.Contain("08h 00m").And.Contain("营业条件"));
            Assert.That(open, Does.Not.Contain("Open 8h / Rest 2h").And.Not.Contain("LayoutInterrupted").And.Not.Contain("Cycle phase"));
            day.RecordLayoutInterruption(1); day.BeforeService(28800);
            day.AfterService(28800, new BusinessDayDrainResult(false, "容量ledger缺失 / Capacity ledger unavailable"));
            Assert.That(BusinessDayStatusView.Format(day.GetSnapshot(28800), false), Does.Contain("Closing").And.Contain("接待容量暂不可用").And.Not.Contain("ledger"));
            day.AfterService(28800, new BusinessDayDrainResult(true, ""));
            var rest = BusinessDayStatusView.Format(day.GetSnapshot(28800), false);
            Assert.That(rest, Does.Contain("Rest").And.Contain("02h 00m").And.Contain("上轮营业因装修调整中断").And.Not.Contain("LayoutInterrupted").And.Not.Contain("CompletedDrain"));
            Assert.That(BusinessDayStatusView.Format(null, false), Does.Contain("准备中").And.Not.Contain("Open"));
        }

        [TestCase("逻辑Delivery尚未领取 / Logical delivery pending", "等待饮品领取")]
        [TestCase("可见桌杯尚未清空 / Visible cups pending", "等待取餐台清空")]
        [TestCase("顾客尚未实际离店，包括disabled或Blocked / Live customer visits", "等待顾客离店")]
        [TestCase("Counter或Pick-up队伍尚未排空 / Queue assignments pending", "等待队伍疏散")]
        [TestCase("容量尚未释放 / Capacity reservations pending", "等待接待名额释放")]
        [TestCase("业务任务、callback或恢复尚未完成 / Pending business work", "等待当前服务完成")]
        [TestCase("装修或布局生命周期尚未就绪 / Layout lifecycle unavailable: failure", "等待装修或通路恢复")]
        [TestCase("服务配置不可用 / Service configuration unavailable", "服务设施暂不可用")]
        [TestCase("经营owner不可用 / Cafe owner unavailable", "营业状态暂不可用")]
        [TestCase("Delivery Blocked: route unavailable", "通路受阻")]
        [TestCase("unrecognized O123 epoch99 diagnostic", "服务尚未完成，请检查店内状态")]
        public void PlayerClosingReasonUsesUnderstandableMessageWhileValidationRetainsRaw(string raw, string player)
        {
            var day = new BusinessDayService(new BusinessDaySettings(1, 1), 0);
            day.BeforeService(1); day.AfterService(1, new BusinessDayDrainResult(false, raw));
            Assert.That(BusinessDayStatusView.Format(day.GetSnapshot(1), false), Does.Contain("Closing").And.Contain(player).And.Not.Contain(raw));
            Assert.That(BusinessDayStatusView.Format(day.GetSnapshot(1), true), Does.Contain(raw).And.Contain("Validation"));
        }

        [Test] public void StatusFormatShowsRealPhaseReasonAndInterruptedHistory()
        {
            var day = new BusinessDayService(new BusinessDaySettings(60, 15), 0);
            Assert.That(BusinessDayStatusView.Format(day.GetSnapshot(0), true), Does.Contain("Day 1").And.Contain("Open").And.Contain("01:00").And.Contain("Validation"));
            day.RecordLayoutInterruption(1); day.BeforeService(60);
            day.AfterService(60, new BusinessDayDrainResult(false, "Delivery Blocked: route unavailable"));
            var snapshot = day.GetSnapshot(60);
            var formatted = BusinessDayStatusView.Format(snapshot, true);
            Assert.That(formatted, Does.Contain("Closing").And.Contain("Delivery Blocked: route unavailable").And.Contain("LayoutInterrupted"));
            day.AfterService(61, new BusinessDayDrainResult(true, ""));
            Assert.That(BusinessDayStatusView.Format(day.GetSnapshot(61), true), Does.Contain("Rest").And.Contain("00:15").And.Contain("LayoutInterrupted"));
            day.BeforeService(76);
            Assert.That(BusinessDayStatusView.Format(day.GetSnapshot(76), false), Does.Contain("Day 2").And.Contain("上轮营业因装修调整中断").And.Not.Contain("Validation"));
            Assert.That(snapshot.Phase, Is.EqualTo(BusinessDayPhase.Closing));
            Assert.That(BusinessDayStatusView.Format(new BusinessDayService(new BusinessDaySettings(), 0).GetSnapshot(0), false), Does.Contain("08h 00m"));
            Assert.That(BusinessDayStatusView.Format(null, false), Does.Contain("准备中"));
        }
        [TestCase(false,CafeServicePresentationKind.Waiting,"等待通路")]
        [TestCase(true,CafeServicePresentationKind.Waiting,"等待通路")]
        [TestCase(false,CafeServicePresentationKind.Planning,"规划")]
        [TestCase(true,CafeServicePresentationKind.Planning,"规划")]
        [TestCase(false,CafeServicePresentationKind.Blocked,"服务受阻")]
        [TestCase(true,CafeServicePresentationKind.Blocked,"服务受阻")]
        public void PlayerTypedServiceActivityIsReadableInOpenAndClosingWithoutInternalIds(bool closing,CafeServicePresentationKind kind,string expected)
        {
            const string raw="internal-key O123 epoch99 employee-secret resource/lane";
            var service=new BusinessDayService(new BusinessDaySettings(10,5),0);var now=closing?10:0;
            if(closing){service.BeforeService(now);service.AfterService(now,new BusinessDayDrainResult(false,raw));}
            var snapshot=service.GetSnapshot(now);var before=snapshot.TransitionSequence;
            var text=BusinessDayStatusView.Format(snapshot,false,new CafeServicePresentation(kind));
            Assert.That(service.GetSnapshot(now).TransitionSequence,Is.EqualTo(before));
            Assert.That(text,Does.Contain(closing?"Closing":"Open").And.Contain("营业条件"));
            Assert.That(text,Does.Not.Contain(raw).And.Not.Contain("O123").And.Not.Contain("epoch99").And.Not.Contain("employee-secret").And.Not.Contain("resource/lane"));
            Assert.That(text,Does.Not.Contain("WaitingForTraffic").And.Not.Contain("DeferredByBudget"));
            Assert.That(text,Does.Contain(expected),"PRODUCT: typed service activity must reach the existing formal player card");
        }

        [TestCase(false)] [TestCase(true)]
        public void PlayerTypedPlanningKeepsAnHonestPauseQualifier(bool closing)
        {
            var service=new BusinessDayService(new BusinessDaySettings(10,5),0);var now=closing?10:0;
            if(closing){service.BeforeService(now);service.AfterService(now,new BusinessDayDrainResult(false,"Pending business work"));}
            var snapshot=service.GetSnapshot(now);
            var text=BusinessDayStatusView.Format(snapshot,false,new CafeServicePresentation(CafeServicePresentationKind.Planning,true));
            Assert.That(text,Does.Contain("暂停"),"PRODUCT: a paused planning intent must not imply ongoing Scene progress");
            Assert.That(text,Does.Contain("规划"));Assert.That(service.GetSnapshot(now).TransitionSequence,Is.EqualTo(snapshot.TransitionSequence));
        }

        [Test] public void PlayerRestSuppressesTypedServiceActivityWhileValidationPreservesRaw()
        {
            const string raw="Blocked original-key O123 epoch99 resource/lane";
            var service=new BusinessDayService(new BusinessDaySettings(10,5),0);service.BeforeService(10);
            service.AfterService(10,new BusinessDayDrainResult(false,raw));var closing=service.GetSnapshot(10);
            var presentation=new CafeServicePresentation(CafeServicePresentationKind.Blocked,true,false);
            Assert.That(BusinessDayStatusView.Format(closing,true,presentation),Is.EqualTo(BusinessDayStatusView.Format(closing,true)),"Validation keeps the exact original raw diagnostics branch");
            service.AfterService(10,new BusinessDayDrainResult(true,""));var rest=service.GetSnapshot(10);
            var text=BusinessDayStatusView.Format(rest,false,presentation);
            Assert.That(text,Does.Contain("Rest").And.Not.Contain("服务受阻").And.Not.Contain("规划").And.Not.Contain("等待通路"));
            Assert.That(service.GetSnapshot(10).TransitionSequence,Is.EqualTo(rest.TransitionSequence));
            Assert.That(rest.RemainingSeconds,Is.EqualTo(5));
        }

        private static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
    }
}
