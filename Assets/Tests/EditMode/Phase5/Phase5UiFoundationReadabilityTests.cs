using System.Collections.Generic;
using System.Linq;
using AnimalCafe.EditorTools.Phase5;
using AnimalCafe.UI.Foundation;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AnimalCafe.Tests.Phase5
{
    public sealed class Phase5UiFoundationReadabilityTests
    {
        private static readonly (string Selector, string Page)[] Pages =
        {
            ("Buttons Page Selector", "Buttons Page"),
            ("Panels Page Selector", "Panels Page"),
            ("Navigation Page Selector", "Navigation Page"),
            ("Feedback Page Selector", "Feedback Page"),
            ("Responsive Motion Page Selector", "Responsive Motion Page")
        };

        private static readonly (string Button, string Label)[] ActionLabels =
        {
            ("Show Toast Button", "Show Toast"),
            ("Show Tooltip Button", "Show Tooltip"),
            ("Show Validation Error Button", "Show Validation Error"),
            ("Open Bottom Sheet Button", "Open Bottom Sheet"),
            ("Pause Game Button", "Pause Game"),
            ("Continue Game Button", "Continue Game"),
            ("Reduced Motion Toggle", "Toggle Reduced Motion"),
            ("Open Second Strong Frost Button", "Open Second Strong Frost"),
            ("Validation Repair Button", "Repair Validation"),
            ("Open Modal Button", "Open Modal"),
            ("Safe Area Confirm Button", "Confirm Safe Area"),
            ("World Occlusion Test Button", "Test World Occlusion"),
            ("Show Solid Panel Button", "Show Solid Panel"),
            ("Show Light Frost Panel Button", "Show Light Frost Panel"),
            ("Show Strong Frost Panel Button", "Show Strong Frost Panel"),
            ("Force Frost Fallback Button", "Force Frost Fallback"),
            ("Handle Back Button", "Handle Back"),
            ("Open Second Modal Button", "Open Second Modal"),
            ("Show Toast Burst Button", "Show Toast Burst"),
            ("Long Press Tooltip Button", "Long Press Tooltip"),
            ("Close Tooltip Button", "Close Tooltip"),
            ("Interrupt And Reopen Button", "Interrupt And Reopen")
        };

        [Test]
        public void BuildScene_CanonicalFontContainsAsciiColon()
        {
            Phase5UiFoundationSceneSetup.BuildScene();

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                Phase5UiAssetPaths.TmpFontAssetPath);

            Assert.That(font, Is.Not.Null);
            Assert.That(font.HasCharacter(':'), Is.True,
                "The canonical TMP font must contain U+003A for normal status text.");
        }

        [Test]
        public void BuildScene_CanonicalFontContainsQuestionMarkAndSheetExposesRunningWorldEvidence()
        {
            Phase5UiFoundationSceneSetup.BuildScene();
            var scene = OpenValidationScene();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                Phase5UiAssetPaths.TmpFontAssetPath);
            Assert.That(font.HasCharacter('?'), Is.True,
                "Modal titles require U+003F without TMP replacement warnings.");

            var mover = Find(scene, "Scaled Time Mover");
            Assert.That(mover.transform.localPosition.y, Is.GreaterThanOrEqualTo(2.5f),
                "The scaled-time mover must remain visible above an open Bottom Sheet.");
            var status = Find(scene, "Bottom Sheet Game Time Status").GetComponent<TMP_Text>();
            Assert.That(status.text, Is.EqualTo("Game continues behind this sheet"));
            Assert.That(status.raycastTarget, Is.False);
        }

        [Test]
        public void BuildScene_CanonicalFontContainsEveryRuntimeToastCharacter()
        {
            Phase5UiFoundationSceneSetup.BuildScene();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                Phase5UiAssetPaths.TmpFontAssetPath);

            foreach (var character in
                     "Saved / 已保存Queue continues / 队列继续3 requests -> 2 Toasts shown; Saved merged x2" +
                     "Coffee Machine — Tap to select")
                Assert.That(font.HasCharacter(character), Is.True,
                    $"The canonical TMP font is missing runtime Toast character U+{(int)character:X4} '{character}'.");
        }

        [Test]
        public void BuildScene_ToastUsesTopPresentationAndBurstStatusIsReadableAboveActions()
        {
            Phase5UiFoundationSceneSetup.BuildScene();
            var scene = OpenValidationScene();
            var toast = Find(scene, "Toast Fixture").GetComponent<RectTransform>();
            var status = Find(scene, "Toast Burst Status").GetComponent<TMP_Text>();
            var feedbackPage = Find(scene, "Feedback Page").transform;
            var advancedActionBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                feedbackPage, Find(scene, "Show Toast Burst Button").transform);
            var statusBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                feedbackPage, status.transform);

            Assert.That(toast.anchorMin, Is.EqualTo(new Vector2(0.5f, 1f)));
            Assert.That(toast.anchorMax, Is.EqualTo(new Vector2(0.5f, 1f)));
            Assert.That(toast.pivot, Is.EqualTo(new Vector2(0.5f, 1f)));
            Assert.That(toast.anchoredPosition.y, Is.LessThanOrEqualTo(-300f),
                "Toast belongs below the persistent tab header, not at screen center.");
            Assert.That(statusBounds.min.y, Is.GreaterThan(advancedActionBounds.max.y),
                "Toast burst status must sit above its advanced action row.");
            Assert.That((status.fontStyle & FontStyles.Bold) != 0, Is.True);
            Assert.That(status.fontSize, Is.GreaterThanOrEqualTo(18f));
        }

        [Test]
        public void BuildScene_FeedbackMessagesShareExactGameViewCenterAndTooltipStartsFullyHidden()
        {
            Phase5UiFoundationSceneSetup.BuildScene();
            var scene = OpenValidationScene();
            var tooltip = Find(scene, "Tooltip Fixture");
            var tooltipRect = tooltip.GetComponent<RectTransform>();
            var validationRect = Find(scene, "Validation Message Fixture").GetComponent<RectTransform>();

            foreach (var rect in new[] { tooltipRect, validationRect })
            {
                Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)));
                Assert.That(rect.anchorMax, Is.EqualTo(new Vector2(0.5f, 0.5f)));
                Assert.That(rect.pivot, Is.EqualTo(new Vector2(0.5f, 0.5f)));
                Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(0f, 144f)),
                    $"{rect.name} must compensate for the page header and land at exact Game View center.");
            }
            Assert.That(tooltip.GetComponent<Image>().enabled, Is.False);
            Assert.That(tooltip.transform.Find("Content").gameObject.activeSelf, Is.False);
        }

        [Test]
        public void BuildScene_TooltipPersistsCloseBackgroundPolicyAcrossSceneReload()
        {
            Phase5UiFoundationSceneSetup.BuildScene();
            var scene = OpenValidationScene();
            var tooltip = Find(scene, "Tooltip Fixture").GetComponents<MonoBehaviour>()
                .Single(component => component.GetType().FullName == "AnimalCafe.UI.Feedback.TooltipView");
            var serializedTooltip = new SerializedObject(tooltip);
            var policy = serializedTooltip.FindProperty("hideBackgroundWhenClosed");

            Assert.That(policy, Is.Not.Null,
                "Tooltip close-background policy must be serialized, not temporary builder memory.");
            Assert.That(policy.boolValue, Is.True,
                "Reloaded Tooltip must remove its complete panel background when closed.");
        }

        [Test]
        public void BuildScene_CreatesNamedPageSelectorsAndOnlyShowsButtonsInitially()
        {
            Phase5UiFoundationSceneSetup.BuildScene();
            var scene = OpenValidationScene();

            foreach (var (selectorName, pageName) in Pages)
            {
                var selector = Find(scene, selectorName);
                Assert.That(selector, Is.Not.Null, selectorName);
                Assert.That(selector.GetComponent<Button>(), Is.Not.Null, selectorName);
                Assert.That(Find(scene, pageName), Is.Not.Null, pageName);
            }

            var visiblePages = Pages
                .Where(page => Find(scene, page.Page).activeSelf)
                .Select(page => page.Page)
                .ToArray();
            Assert.That(visiblePages, Is.EqualTo(new[] { "Buttons Page" }));
            Assert.That(Find(scene, "World Occlusion Test Button").activeInHierarchy, Is.False,
                "World Occlusion is a Navigation diagnostic and must never appear on the initial Buttons page.");
            Assert.That(Find(scene, "World Occlusion Test Button").transform.IsChildOf(
                Find(scene, "Navigation Page").transform), Is.True);
            Assert.That(Find(scene, "Safe Area Confirm Button").activeInHierarchy, Is.False,
                "Safe Area confirmation belongs exclusively to Responsive Motion.");
            Assert.That(Find(scene, "Safe Area Status").activeInHierarchy, Is.False,
                "Safe Area status belongs exclusively to Responsive Motion.");
        }

        [Test]
        public void BuildScene_ActionButtonsUseTheirActualActionLabels()
        {
            Phase5UiFoundationSceneSetup.BuildScene();
            var scene = OpenValidationScene();

            foreach (var (buttonName, expectedLabel) in ActionLabels)
            {
                var label = Find(scene, buttonName).GetComponentInChildren<TMP_Text>(true);
                Assert.That(label, Is.Not.Null, buttonName + " requires a visible TMP label.");
                Assert.That(label.text, Is.EqualTo(expectedLabel), buttonName);
                Assert.That(label.text, Is.Not.EqualTo("Primary"), buttonName);
            }
        }

        [Test]
        public void BuildScene_StatusLabelsUseHorizontalRegionsForNormalStatusText()
        {
            Phase5UiFoundationSceneSetup.BuildScene();
            var scene = OpenValidationScene();

            foreach (var statusName in new[]
            {
                "Reduced Motion Status", "Safe Area Status", "Toast Burst Status"
            })
            {
                var status = Find(scene, statusName).GetComponent<TMP_Text>();
                Assert.That(status, Is.Not.Null, statusName);
                Assert.That(status.rectTransform.rect.width, Is.GreaterThanOrEqualTo(600f),
                    statusName + " must reserve horizontal space instead of wrapping one character per line.");
            }
        }

        [Test]
        public void BuildScene_ReservesButtonsForGalleryAndPlacesLocalizedCopyOnResponsivePage()
        {
            Phase5UiFoundationSceneSetup.BuildScene();
            var scene = OpenValidationScene();
            var buttonsPage = Find(scene, "Buttons Page");
            var responsivePage = Find(scene, "Responsive Motion Page");
            var header = Find(scene, "Review Header");

            Assert.That(buttonsPage, Is.Not.Null);
            Assert.That(responsivePage, Is.Not.Null);
            Assert.That(header, Is.Not.Null, "The persistent selector header must remain.");
            foreach (var selectorName in Pages.Select(page => page.Selector))
            {
                Assert.That(Find(scene, selectorName).transform.IsChildOf(header.transform), Is.True,
                    selectorName + " must remain in the top selector header.");
            }

            foreach (var redundantName in new[] { "Review Title", "Gallery Title", "Long Localized Label" })
            {
                Assert.That(buttonsPage.GetComponentsInChildren<Transform>(true)
                        .Any(transform => transform.name == redundantName),
                    Is.False, "Buttons Page must not contain redundant " + redundantName + ".");
            }
            Assert.That(Find(scene, "Review Title"), Is.Null, "Do not generate a redundant review title.");
            Assert.That(Find(scene, "Gallery Title"), Is.Null, "Do not generate a redundant gallery title.");
            Assert.That(Find(scene, "Component Gallery").GetComponentsInChildren<Button>(true), Has.Length.EqualTo(9),
                "The Buttons page must retain its nine-button gallery.");

            var longLabel = Find(scene, "Long Localized Label").GetComponent<TMP_Text>();
            Assert.That(longLabel.transform.IsChildOf(responsivePage.transform), Is.True,
                "Long localized copy belongs exclusively to Responsive Motion.");
            Assert.That(longLabel.rectTransform.rect.width, Is.GreaterThanOrEqualTo(720f));
            Assert.That(longLabel.rectTransform.rect.width, Is.GreaterThan(longLabel.rectTransform.rect.height * 3f),
                "The localized copy needs a readable horizontal region, not a tall narrow column.");
            Assert.That(longLabel.textWrappingMode, Is.EqualTo(TextWrappingModes.Normal));
        }

        [Test]
        public void BuildScene_ResponsiveContentUsesReadableCardAndSafeAreaControlsStayBelowHeader()
        {
            Phase5UiFoundationSceneSetup.BuildScene();
            var scene = OpenValidationScene();
            var card = Find(scene, "Responsive Info Card");
            Assert.That(card, Is.Not.Null, "Responsive page requires one readable central information card.");
            var cardImage = card.GetComponent<Image>();
            var longLabel = Find(scene, "Long Localized Label").GetComponent<TMP_Text>();
            var reducedStatus = Find(scene, "Reduced Motion Status").GetComponent<TMP_Text>();
            var safeStatus = Find(scene, "Safe Area Status").GetComponent<TMP_Text>();
            var safeConfirm = Find(scene, "Safe Area Confirm Button").GetComponent<RectTransform>();

            Assert.That(cardImage, Is.Not.Null);
            Assert.That(cardImage.raycastTarget, Is.False);
            Assert.That(cardImage.color.r + cardImage.color.g + cardImage.color.b, Is.GreaterThan(2.4f),
                "Responsive information needs a light surface behind dark Theme text.");
            Assert.That(longLabel.transform.IsChildOf(card.transform), Is.True);
            Assert.That(reducedStatus.transform.IsChildOf(card.transform), Is.True);
            Assert.That(longLabel.color.r + longLabel.color.g + longLabel.color.b, Is.LessThan(1f));
            Assert.That(reducedStatus.color.r + reducedStatus.color.g + reducedStatus.color.b, Is.LessThan(1f));

            Assert.That(safeStatus.rectTransform.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(safeStatus.rectTransform.anchoredPosition.y, Is.LessThan(-260f));
            Assert.That(safeConfirm.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(safeConfirm.anchoredPosition.y, Is.LessThan(-330f),
                "Confirm Safe Area must use the visible action strip below the card, never the persistent header.");
        }

        [Test]
        public void BuildScene_ButtonsGalleryExplainsStatesAndPressedPreviewIsNotInteractive()
        {
            Phase5UiFoundationSceneSetup.BuildScene();
            var scene = OpenValidationScene();
            var gallery = Find(scene, "Component Gallery");

            foreach (var heading in new[] { "Default", "Pressed Preview", "Disabled" })
            {
                var label = gallery.GetComponentsInChildren<TMP_Text>(true)
                    .SingleOrDefault(text => text.text == heading);
                Assert.That(label, Is.Not.Null, $"Buttons gallery requires the '{heading}' column heading.");
            }

            foreach (var button in gallery.GetComponentsInChildren<Button>(true))
            {
                var state = button.name.EndsWith("_Pressed")
                    ? UiButtonState.Pressed
                    : button.name.EndsWith("_Disabled")
                        ? UiButtonState.Disabled
                        : UiButtonState.Default;
                Assert.That(button.interactable, Is.EqualTo(state == UiButtonState.Default), button.name);
            }
        }

        [Test]
        public void BuildScene_PanelsPageHasOneReadablePreviewStageAndExplicitStatus()
        {
            Phase5UiFoundationSceneSetup.BuildScene();
            var scene = OpenValidationScene();
            var stage = Find(scene, "Panel Preview Stage");

            Assert.That(stage, Is.Not.Null);
            var stageRect = stage.GetComponent<RectTransform>().rect;
            Assert.That(stageRect.width, Is.GreaterThanOrEqualTo(560f));
            Assert.That(stageRect.height, Is.GreaterThanOrEqualTo(360f));
            foreach (var backdrop in new[] { "Panel Backdrop Wood", "Panel Backdrop Sage", "Panel Backdrop Cream" })
                Assert.That(Find(scene, backdrop).transform.IsChildOf(stage.transform), Is.True, backdrop);

            Assert.That(Find(scene, "Solid Panel Fixture").activeSelf, Is.True);
            Assert.That(Find(scene, "Light Frost Panel Fixture").activeSelf, Is.False);
            Assert.That(Find(scene, "Strong Frost Panel Fixture").activeSelf, Is.False);
            Assert.That(Find(scene, "Panel Preview Title").GetComponent<TMP_Text>().text, Is.EqualTo("Solid Panel"));
            Assert.That(Find(scene, "Panel Preview Status").GetComponent<TMP_Text>().text, Is.EqualTo("Current: Solid"));
            Assert.That(Find(scene, "Open Second Strong Frost Button").activeInHierarchy, Is.False,
                "The technical Strong-lease probe must not obstruct the manual Panel preview.");
        }

        [Test]
        public void BuildScene_PanelActionsUseOneRowWithVisibleGaps()
        {
            Phase5UiFoundationSceneSetup.BuildScene();
            var scene = OpenValidationScene();

            AssertAlignedRowWithMinimumGap(scene, 24f,
                "Show Solid Panel Button",
                "Show Light Frost Panel Button",
                "Show Strong Frost Panel Button",
                "Force Frost Fallback Button");
        }

        [Test]
        public void BuildScene_NavigationAndModalDiagnosticActionsUseCenteredRows()
        {
            Phase5UiFoundationSceneSetup.BuildScene();
            var scene = OpenValidationScene();

            AssertAlignedRowWithMinimumGap(scene, 24f,
                "Pause Game Button", "Continue Game Button", "Open Modal Button");
            AssertAlignedRowWithMinimumGap(scene, 24f,
                "Handle Back Button", "Interrupt And Reopen Button");

            var lowerRow = new[] { "Handle Back Button", "Interrupt And Reopen Button" }
                .Select(name => Find(scene, name).GetComponent<RectTransform>())
                .ToArray();
            Assert.That(lowerRow[0].anchoredPosition.x, Is.EqualTo(-lowerRow[1].anchoredPosition.x).Within(0.1f),
                "The lower Navigation row must be centered as a pair.");
        }

        [Test]
        public void BuildScene_FeedbackAdvancedActionsUseOneRowWithVisibleGaps()
        {
            Phase5UiFoundationSceneSetup.BuildScene();
            var scene = OpenValidationScene();

            AssertAlignedRowWithMinimumGap(scene, 24f,
                "Validation Repair Button",
                "Show Toast Burst Button",
                "Long Press Tooltip Button",
                "Close Tooltip Button");
        }

        [Test]
        public void BuildScene_FeedbackAdvancedActionsUseViewportRelativeLandscapeSafeAnchors()
        {
            Phase5UiFoundationSceneSetup.BuildScene();
            var scene = OpenValidationScene();
            var names = new[]
            {
                "Validation Repair Button", "Show Toast Burst Button",
                "Long Press Tooltip Button", "Close Tooltip Button"
            };
            var expectedX = new[] { 0.15f, 0.38f, 0.62f, 0.85f };

            for (var index = 0; index < names.Length; index++)
            {
                var rect = Find(scene, names[index]).GetComponent<RectTransform>();
                Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(expectedX[index], 0.22f)), names[index]);
                Assert.That(rect.anchorMax, Is.EqualTo(rect.anchorMin), names[index]);
                Assert.That(rect.anchoredPosition, Is.EqualTo(Vector2.zero), names[index]);
            }

            var status = Find(scene, "Toast Burst Status").GetComponent<RectTransform>();
            Assert.That(status.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.32f)));
            Assert.That(status.anchorMax, Is.EqualTo(status.anchorMin));
        }

        [Test]
        public void BuildScene_ModalRootsAndBlockersStretchAcrossEveryViewport()
        {
            Phase5UiFoundationSceneSetup.BuildScene();
            var scene = OpenValidationScene();

            foreach (var modalName in new[] { "Modal Fixture", "Second Modal Fixture" })
            {
                var modal = Find(scene, modalName).GetComponent<RectTransform>();
                var blocker = modal.transform.Find("Blocker").GetComponent<RectTransform>();
                AssertStretched(modal, modalName);
                AssertStretched(blocker, modalName + "/Blocker");
            }
        }

        [Test]
        public void BuildScene_ContainersOwnTheOverlayAndFeedbackHasNoBlankDefaultFixtures()
        {
            Phase5UiFoundationSceneSetup.BuildScene();
            var scene = OpenValidationScene();
            var modalLayer = Find(scene, "Modal Layer");
            Assert.That(modalLayer, Is.Not.Null);

            foreach (var containerName in new[]
            {
                "Bottom Sheet Fixture", "Modal Fixture", "Second Modal Fixture"
            })
            {
                Assert.That(Find(scene, containerName).transform.IsChildOf(modalLayer.transform), Is.True,
                    containerName + " must render above every ordinary review control.");
            }

            Assert.That(Find(scene, "Tooltip Fixture").GetComponent<Image>().enabled, Is.False,
                "Tooltip background must stay hidden until its action opens it.");
            Assert.That(Find(scene, "Validation Message Fixture").GetComponent<Image>().enabled, Is.False,
                "Validation background must stay hidden until an error is requested.");

            var modalContent = Find(scene, "Modal Fixture").transform.Find("Content");
            var title = RectTransformUtility.CalculateRelativeRectTransformBounds(
                modalContent, modalContent.Find("Title"));
            var second = RectTransformUtility.CalculateRelativeRectTransformBounds(
                modalContent, modalContent.Find("Open Second Modal Button"));
            Assert.That(title.Intersects(second), Is.False,
                "Open Second Modal must not cover the primary modal title.");
        }

        [Test]
        public void BuildScene_CoffeeMachineIsVisibleAndClearlyLabeledInPortraitReview()
        {
            Phase5UiFoundationSceneSetup.BuildScene();
            var scene = OpenValidationScene();
            var camera = Find(scene, "Main Camera").GetComponent<UnityEngine.Camera>();
            var coffeeMachine = Find(scene, "Selectable Coffee Machine");
            camera.aspect = 1080f / 1920f;

            var viewport = camera.WorldToViewportPoint(coffeeMachine.transform.position);
            Assert.That(viewport.z, Is.GreaterThan(0f));
            Assert.That(viewport.x, Is.InRange(0.16f, 0.42f),
                "Coffee Machine must be fully visible in the left portrait review area.");
            Assert.That(viewport.y, Is.InRange(0.25f, 0.72f));

            var hint = Find(scene, "Coffee Machine Hint").GetComponent<TMP_Text>();
            Assert.That(hint.text, Does.Contain("Coffee Machine").And.Contain("Tap to select"));
            Assert.That(hint.raycastTarget, Is.False,
                "The hint must not intercept the world-selection gesture.");
        }

        [Test]
        public void BuildScene_ModalInterruptionControlsRemainInsidePrimaryModalContent()
        {
            Phase5UiFoundationSceneSetup.BuildScene();
            var scene = OpenValidationScene();
            var modalContent = Find(scene, "Modal Fixture").transform.Find("Content");

            Assert.That(Find(scene, "Handle Back Button").transform.IsChildOf(modalContent), Is.True,
                "Handle Back must remain clickable while the primary Modal blocker is active.");
            Assert.That(Find(scene, "Interrupt And Reopen Button").transform.IsChildOf(modalContent), Is.True,
                "Interrupt And Reopen must remain clickable while the primary Modal blocker is active.");
        }

        private static Scene OpenValidationScene() => EditorSceneManager.OpenScene(
            Phase5UiFoundationSceneSetup.ScenePath,
            OpenSceneMode.Single);

        private static void AssertAlignedRowWithMinimumGap(Scene scene, float minimumGap, params string[] names)
        {
            var parent = Find(scene, names[0]).transform.parent;
            var rects = names
                .Select(name => Find(scene, name).GetComponent<RectTransform>())
                .Select(rect => (rect, bounds: RectTransformUtility.CalculateRelativeRectTransformBounds(parent, rect)))
                .OrderBy(item => item.bounds.center.x)
                .ToArray();

            Assert.That(rects.Select(item => item.bounds.center.y).Distinct().Count(), Is.EqualTo(1),
                string.Join(", ", names) + " must share one horizontal baseline.");
            for (var index = 1; index < rects.Length; index++)
            {
                var previousRight = rects[index - 1].bounds.max.x;
                var currentLeft = rects[index].bounds.min.x;
                Assert.That(currentLeft - previousRight, Is.GreaterThanOrEqualTo(minimumGap),
                    $"{rects[index - 1].rect.name} and {rects[index].rect.name} need at least {minimumGap}px spacing.");
            }
        }

        private static void AssertStretched(RectTransform rect, string message)
        {
            Assert.That(rect.anchorMin, Is.EqualTo(Vector2.zero), message);
            Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one), message);
            Assert.That(rect.offsetMin, Is.EqualTo(Vector2.zero), message);
            Assert.That(rect.offsetMax, Is.EqualTo(Vector2.zero), message);
        }

        private static GameObject Find(Scene scene, string name) => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .SingleOrDefault(transform => transform.name == name)?.gameObject;
    }
}
