using System;
using System.Linq;
using System.Reflection;
using AnimalCafe.Decoration;
using AnimalCafe.EditorTools.Phase8;
using AnimalCafe.Layout;
using AnimalCafe.UI.Components;
using AnimalCafe.UI.Decoration;
using AnimalCafe.UI.Feedback;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AnimalCafe.Tests.EditMode.Phase8
{
    public sealed class Phase8FeedbackReviewTests
    {
        const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
        const string Cash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string Blocked = "dddddddddddddddddddddddddddddddd";
        const string Unreachable = "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";

        [Test]
        public void PickUpWithoutAnchor_ActionBarExplainsAdjacentCellRequirement()
        {
            var root = new GameObject("Feedback", typeof(RectTransform));
            try
            {
                var text = root.AddComponent<TextMeshProUGUI>();
                var action = root.AddComponent<DecorationActionBarView>();
                typeof(DecorationActionBarView).GetField("feedbackLabel", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(action, text);
                action.Show(false, false, PlacementFeedbackMapper.Map(
                    FunctionalSurfacePlacementResult.Failure(FunctionalSurfacePlacementFailureReason.NoValidInteractionAnchor)));
                Assert.That(text.text, Does.Contain("adjacent"),
                    "A generic blocked message hides why a free counter Slot cannot accept Pick-up.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void ReadinessMapping_MixedFailuresRetainTheirOwnStationRoleAndCellWithoutRawIds()
        {
            var report = MixedFailureReport();
            var message = PlacementFeedbackMapper.GetPlayerMessage(report);
            var lines = message.Split('\n');
            Assert.That(lines.Single(line => line.Contains("(2, -1)")),
                Does.Contain("收银机").And.Contain("客人").And.Contain("超出"));
            Assert.That(lines.Single(line => line.Contains("(6, 6)")),
                Does.Contain("收银机").And.Contain("员工").And.Contain("阻挡")
                    .And.Not.Contain("无法到达").And.Not.Contain("(32, 1)"));
            Assert.That(lines.Single(line => line.Contains("(32, 1)")),
                Does.Contain("收银机").And.Contain("客人").And.Contain("无法到达")
                    .And.Not.Contain("阻挡"));
            Assert.That(message, Does.Not.Contain(Cash).And.Not.Contain(Blocked)
                .And.Not.Contain(Unreachable).And.Not.Contain(new string('1', 32))
                .And.Not.Contain(new string('4', 32)).And.Not.Contain(new string('2', 32))
                .And.Not.Contain("slot.center").And.Not.Contain("Employee").And.Not.Contain("Customer"));
        }

        [TestCase("surface")]
        [TestCase("furniture")]
        [TestCase("readiness")]
        public void PlayerMessageMapping_DiagnosticIdsDoNotBecomeVisibleText(string kind)
        {
            var ids = new[] { Cash, "slot.0" };
            var message = kind switch
            {
                "surface" => PlacementFeedbackMapper.GetPlayerMessage(
                    FunctionalSurfacePlacementResult.Failure(FunctionalSurfacePlacementFailureReason.SlotOccupied), ids),
                "furniture" => PlacementFeedbackMapper.GetPlayerMessage(
                    PlacementResult.Failure(PlacementFailureReason.Overlap), ids),
                _ => PlacementFeedbackMapper.GetPlayerMessage(LayoutReadinessFailureCode.AnchorBlocked, ids)
            };

            Assert.That(message, Does.Contain(kind == "readiness" ? "阻挡" : "占用"));
            Assert.That(message, Does.Not.Contain(Cash).And.Not.Contain("slot.0"));
        }

        [Test]
        public void ReadinessView_KeepsRawIdsForDiagnosticsWithoutDisplayingThem()
        {
            var root = new GameObject("Readiness diagnostics", typeof(RectTransform), typeof(TextMeshProUGUI));
            try
            {
                var view = root.AddComponent<ValidationMessageView>();
                view.Configure(root.GetComponent<TextMeshProUGUI>());

                view.ShowReadiness(MixedFailureReport());

                Assert.That(view.DiagnosticIds, Is.EquivalentTo(new[]
                    { Cash, Blocked, Unreachable, new string('1', 32), new string('4', 32), new string('2', 32), "slot.center" }));
                Assert.That(view.CurrentMessage, Does.Contain("(6, 6)").And.Contain("员工").And.Contain("阻挡"));
                Assert.That(view.CurrentMessage, Does.Not.Contain(Cash).And.Not.Contain(Blocked)
                    .And.Not.Contain(Unreachable).And.Not.Contain("slot.center"));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void ConfiguredReadinessFont_CoversRuntimeMessagesAndDiagnosticCharacters()
        {
            WithConfiguredCopy(scene =>
            {
                var label = FindView(scene).GetComponentInChildren<TMP_Text>(true);
                var corpus = string.Join(" ", Enum.GetValues(typeof(FunctionalSurfacePlacementFailureReason))
                    .Cast<FunctionalSurfacePlacementFailureReason>()
                    .Select(reason => PlacementFeedbackMapper.GetPlayerMessage(FunctionalSurfacePlacementResult.Failure(reason))))
                    + string.Join(" ", Enum.GetValues(typeof(LayoutReadinessFailureCode))
                        .Cast<LayoutReadinessFailureCode>().Select(code => PlacementFeedbackMapper.GetPlayerMessage(code)))
                    + "布局已准备好，可以营业但暂时不能营业 收银机 咖啡机 取餐点 员工 客人 承托 设备 位置 格子 "
                    + "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789,.:;/-_()[]（）：，。";
                var missing = corpus.Where(c => !char.IsWhiteSpace(c) && !label.font.HasCharacter(c)).Distinct().ToArray();
                Assert.That(missing, Is.Empty, "Missing glyphs: " + new string(missing));
                Assert.That(label.font.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Static),
                    "Runtime must not mutate the shared font atlas to repair missing characters.");
            });
        }

        [Test]
        public void ConfiguredReadiness_LongDiagnosticListWrapsAndCanScrollToLastIssue()
        {
            WithConfiguredCopy(scene =>
            {
                var view = FindView(scene);
                view.ShowStatus(string.Join("\n", Enumerable.Range(0, 12).Select(i =>
                    "设备 " + i + " " + new string('a', 32) + "：互动位置被阻挡 / " + new string('b', 32) + " / slot.0")));
                Canvas.ForceUpdateCanvases();
                var label = view.GetComponentInChildren<TMP_Text>(true);
                var scroll = view.GetComponent<ScrollRect>();
                Assert.That(scroll, Is.Not.Null, "Long feedback must remain accessible, not overflow or be truncated.");
                Assert.That(scroll.viewport.GetComponent<RectMask2D>(), Is.Not.Null);
                Assert.That(label.textWrappingMode, Is.EqualTo(TextWrappingModes.Normal));
                var preferred = label.GetPreferredValues(label.text, scroll.viewport.rect.width, Mathf.Infinity);
                Assert.That(preferred.x, Is.LessThanOrEqualTo(scroll.viewport.rect.width + 1));
                Assert.That(scroll.content.rect.height, Is.GreaterThanOrEqualTo(preferred.y - 1));
                Assert.That(((RectTransform)view.transform).rect.height, Is.LessThanOrEqualTo(200));
                Assert.That(scroll.vertical, Is.True);
                scroll.verticalNormalizedPosition = 0;
                Canvas.ForceUpdateCanvases();
                Assert.That(scroll.content.anchoredPosition.y, Is.GreaterThan(0), "The last issue must be reachable by scrolling.");
                view.Clear();
                Assert.That(scroll.enabled, Is.False, "An invisible message must not consume scene input.");
            });
        }

        [Test]
        public void ConfiguredReadiness_RemainsInsideTopOfScreenInsteadOfBottomSheet()
        {
            WithConfiguredCopy(scene =>
            {
                var view = FindView(scene);
                view.ShowStatus(string.Join("\n", Enumerable.Range(0, 12)
                    .Select(index => "设备 " + index + "：互动位置被阻挡 " + new string('a', 32))));
                Canvas.ForceUpdateCanvases();
                var canvas = view.GetComponentInParent<Canvas>();
                var canvasRect = (RectTransform)canvas.transform;
                var corners = new Vector3[4];
                ((RectTransform)view.transform).GetWorldCorners(corners);
                var local = corners.Select(point => canvasRect.InverseTransformPoint(point)).ToArray();
                Assert.That(local.Min(point => point.y), Is.GreaterThanOrEqualTo(canvasRect.rect.center.y),
                    "Persistent readiness belongs above the scene, not inside the bottom 190px sheet.");
                Assert.That(local.Max(point => point.y), Is.LessThanOrEqualTo(canvasRect.rect.yMax));
                Assert.That(local.Min(point => point.x), Is.GreaterThanOrEqualTo(canvasRect.rect.xMin));
                Assert.That(local.Max(point => point.x), Is.LessThanOrEqualTo(canvasRect.rect.xMax));
            });
        }

        [TestCase("sibling-order")]
        [TestCase("safe-area-component")]
        public void ConfigureReadiness_ParentLayerRepairPersistsAfterSceneReload(string damage)
        {
            WithConfiguredCopy(scene =>
            {
                var layer = FindView(scene).transform.parent;
                Assert.That(layer.name, Is.EqualTo("Phase8_FeedbackLayer"));
                if (damage == "sibling-order")
                {
                    layer.SetAsLastSibling();
                    Assert.That(layer.GetSiblingIndex(), Is.GreaterThan(0),
                        "Fixture must move the feedback layer behind another Screen Canvas child.");
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(layer.GetComponent<SafeAreaContainer>());
                    Assert.That(layer.GetComponent<SafeAreaContainer>(), Is.Null);
                }

                var path = scene.path;
                EditorSceneManager.MarkSceneDirty(scene);
                Assert.That(EditorSceneManager.SaveScene(scene, path), Is.True);
                Assert.That(EditorSceneManager.CloseScene(scene, true), Is.True);

                typeof(Phase8SceneSetup).GetMethod("ConfigureScene", PrivateStatic)
                    .Invoke(null, new object[] { path, false });

                var reopened = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                var repairedLayer = FindView(reopened).transform.parent;
                if (damage == "sibling-order")
                    Assert.That(repairedLayer.GetSiblingIndex(), Is.Zero,
                        "The configured sibling order must persist on disk, not only in memory.");
                else
                    Assert.That(repairedLayer.GetComponent<SafeAreaContainer>(), Is.Not.Null,
                        "The repaired SafeAreaContainer must persist after reopening the Scene.");
            });
        }

        static LayoutReadinessReport MixedFailureReport()
        {
            object Call(string method, params object[] args) => typeof(LayoutReadinessEvaluatorTests)
                .GetMethod(method, PrivateStatic).Invoke(null, args);
            var scenario = Call("CreateScenario", false, false);
            Call("AddRegion", scenario, "region.main", 0, 0, 20, 10);
            Call("AddRegion", scenario, "region.isolated", 30, 0, 5, 5);
            Call("AddEntrance", scenario, "entrance.main", 0, 0);
            Call("PlaceSupport", scenario, new string('1', 32), 2, 0);
            Call("PlaceMounted", scenario, Cash, "equipment.cash-register", new string('1', 32));
            Call("PlaceSupport", scenario, new string('4', 32), 6, 5);
            Call("PlaceMounted", scenario, Blocked, "equipment.cash-register", new string('4', 32));
            var cafe = (CafeLayout)scenario.GetType().GetProperty("CafeLayout").GetValue(scenario);
            cafe.AddReservation(new LayoutReservation("blocked.employee", LayoutReservationType.Blocked,
                new GridPosition(6, 6), new GridSize(1, 1)));
            Call("PlaceSupport", scenario, new string('2', 32), 32, 2);
            Call("PlaceMounted", scenario, Unreachable, "equipment.cash-register", new string('2', 32));
            return (LayoutReadinessReport)Call("Evaluate", scenario);
        }

        static ValidationMessageView FindView(Scene scene) => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<ValidationMessageView>(true)).Single();

        static void WithConfiguredCopy(Action<Scene> action)
        {
            var path = "Assets/__Phase8FeedbackReview_" + Guid.NewGuid().ToString("N") + ".unity";
            var original = SceneManager.GetActiveScene();
            Assert.That(AssetDatabase.CopyAsset(Phase8AssetPaths.MainCafeScenePath, path), Is.True);
            try
            {
                Phase8AssetBuilder.BuildAssets();
                typeof(Phase8SceneSetup).GetMethod("ConfigureScene", PrivateStatic)
                    .Invoke(null, new object[] { path, false });
                var scene = SceneManager.GetSceneByPath(path);
                if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                action(scene);
            }
            finally
            {
                var scene = SceneManager.GetSceneByPath(path);
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                AssetDatabase.DeleteAsset(path);
                if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            }
        }
    }
}
