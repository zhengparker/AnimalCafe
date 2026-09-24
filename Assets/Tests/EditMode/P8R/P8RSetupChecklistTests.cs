using System;
using System.Reflection;
using AnimalCafe.Layout;
using AnimalCafe.UI.Feedback;
using AnimalCafe.UI.P8R;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AnimalCafe.Tests.EditMode.P8R
{
    public sealed class P8RSetupChecklistTests
    {
        private GameObject root;
        private P8RAppearance appearance;
        private ValidationMessageView view;

        [SetUp]
        public void SetUp()
        {
            appearance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<P8RAppearance>("Assets/UI/P8R/P8RAppearance.asset"));
            root = new GameObject("Checklist", typeof(RectTransform), typeof(Image));
            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(root.transform, false);
            view = root.AddComponent<ValidationMessageView>();
            typeof(ValidationMessageView).GetField("appearance", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(view, appearance);
            view.Configure(label.GetComponent<TMP_Text>());
            view.SetDecorationMode(true);
        }

        [TearDown]
        public void TearDown() { Object.DestroyImmediate(root); Object.DestroyImmediate(appearance); }

        [Test]
        public void MissingStations_ShowThreeActionableRowsWithoutErrorSummary()
        {
            view.ShowReadiness(Report(false, 0, 0, 0, 0, 0, 0));
            Assert.That(view.IsDetailsExpanded, Is.True);
            Assert.That(view.CurrentMessage, Does.Not.Contain("Setup checklist").And.Not.Contain("0/3"));
            foreach (var name in new[] { "Cash Register", "Coffee Machine", "Pickup Point" })
                Assert.That(view.CurrentMessage, Does.Contain(name));
            Assert.That(view.CurrentMessage, Does.Contain("To place").And.Not.Contain("Can't open"));
        }

        [Test]
        public void ValidTypesWithoutConnectedCombination_DoNotReportOverallReady()
        {
            view.ShowReadiness(Report(false, 1, 1, 1, 1, 1, 1));
            Assert.That(view.CurrentMessage, Does.Contain("Connect").And.Not.Contain("3/3"));
            Assert.That(view.CurrentMessage, Does.Not.Contain("Ready to open"));
        }

        [Test]
        public void ReadyLayout_RemainsVisibleWithoutPanelTitleOrDisclosure()
        {
            view.ShowReadiness(Report(true, 1, 1, 1, 1, 1, 1));
            Assert.That(view.IsVisible, Is.True);
            Assert.That(view.IsDetailsExpanded, Is.True);
            Assert.That(view.CurrentMessage, Does.Contain("Cash Register").And.Contain("Coffee Machine").And.Contain("Pickup Point"));
            Assert.That(root.GetComponent<Image>().enabled, Is.False);
            Assert.That(root.GetComponentsInChildren<Button>(), Is.Empty);
            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
                Assert.That(graphic.raycastTarget, Is.False);
        }
        [Test]
        public void MixedInstances_CheckValidTypeAndRetainAdjustmentCount()
        {
            view.ShowReadiness(Report(false, 2, 1, 1, 0, 0, 0));
            Assert.That(view.CurrentMessage, Does.Contain("1 more needs adjustment").And.Contain("Adjust"));
        }

        [Test]
        public void RepeatedReports_ReuseThreeStatusImagesAndUpdateConfirmedStates()
        {
            view.ShowReadiness(Report(false, 0, 0, 0, 0, 0, 0));
            var icons = root.GetComponentsInChildren<Image>();
            Assert.That(icons.Length, Is.EqualTo(6), "Root, three status images, translucent fill and opaque border.");
            var label = root.transform.Find("ChecklistRow0/Label").GetComponent<TMP_Text>();
            Assert.That(label.fontSharedMaterial, Is.SameAs(appearance.Font.material));
            var fill = root.transform.Find("ChecklistPanelFill").GetComponent<Image>();
            var border = root.transform.Find("ChecklistPanelBorder").GetComponent<Image>();
            Assert.That(fill.color.a, Is.EqualTo(.75f));
            Assert.That(border.color.a, Is.EqualTo(1f));
            Assert.That(border.fillCenter, Is.False);
            Assert.That(border.sprite, Is.SameAs(P8RButtonLayout.BorderSprite(appearance.Sprite("panel_cream"))));
            foreach (var icon in icons)
                if (icon != root.GetComponent<Image>()) Assert.That(icon.sprite, Is.Not.Null);
            view.ShowReadiness(Report(false, 1, 1, 1, 0, 0, 0));
            Assert.That(root.GetComponentsInChildren<Image>(), Is.EqualTo(icons));
            var ready = root.transform.Find("ChecklistRow0/Status").GetComponent<Image>().sprite;
            var adjust = root.transform.Find("ChecklistRow1/Status").GetComponent<Image>().sprite;
            var empty = root.transform.Find("ChecklistRow2/Status").GetComponent<Image>().sprite;
            Assert.That(ready, Is.Not.SameAs(adjust));
            Assert.That(empty, Is.Not.SameAs(ready).And.Not.SameAs(adjust));
            view.ShowReadiness(Report(true, 1, 1, 1, 1, 1, 1));
            Assert.That(view.IsDetailsExpanded, Is.True);
        }
        [Test]
        public void PreviewNote_DoesNotChangeConfirmedRowsOrDisclosure()
        {
            view.ShowReadiness(Report(false, 1, 1, 0, 0, 0, 0));
            var before = view.CurrentMessage;
            var diagnostic = view.FullReadinessMessage;
            view.SetPreviewPending(true);
            Assert.That(view.CurrentMessage, Is.EqualTo(before), "Preview cannot mutate the confirmed list.");
            view.SetPreviewPending(false);
            Assert.That(view.CurrentMessage, Is.EqualTo(before));
            Assert.That(view.FullReadinessMessage, Is.EqualTo(diagnostic));
        }

        [Test]
        public void NormalMode_ReadyHides_AllValidButDisconnectedStillPrompts_AndDecorRestoresRows()
        {
            view.SetDecorationMode(false);
            view.ShowReadiness(Report(true, 1, 1, 1, 1, 1, 1));
            Assert.That(view.IsVisible, Is.False);
            Assert.That(view.CurrentMessage, Is.Empty);
            view.SetDecorationMode(true);
            Assert.That(view.IsVisible, Is.True);
            Assert.That(root.transform.Find("ChecklistRow2").gameObject.activeSelf, Is.True);
            view.SetDecorationMode(false);
            Assert.That(view.IsVisible, Is.False);
            view.ShowReadiness(Report(false, 1, 1, 1, 1, 1, 1));
            Assert.That(view.IsVisible, Is.True);
            Assert.That(view.CurrentMessage, Is.EqualTo("Setup incomplete · Enter Decor to finish"));
            Assert.That(root.transform.Find("ChecklistRow0").gameObject.activeSelf, Is.False);
        }

        private static LayoutReadinessReport Report(bool connected, int ct, int cv, int mt, int mv, int pt, int pv)
        {
            return Construct<LayoutReadinessReport>(connected, Array.Empty<StationReadiness>(), Array.Empty<LayoutReadinessFailure>(),
                Construct<LayoutReadinessSummary>(ct, cv), Construct<LayoutReadinessSummary>(mt, mv), Construct<LayoutReadinessSummary>(pt, pv));
        }
        private static T Construct<T>(params object[] args) => (T)Activator.CreateInstance(typeof(T),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, args, null);
    }
}
