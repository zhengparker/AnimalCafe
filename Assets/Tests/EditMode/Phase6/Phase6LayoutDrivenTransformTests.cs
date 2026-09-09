using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using AnimalCafe.EditorTools.Phase6;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AnimalCafe.Tests.EditMode.Phase6
{
    public sealed class Phase6LayoutDrivenTransformTests
    {
        [TestCase("horizontal")]
        [TestCase("fitter")]
        public void CanonicalLayoutRebuild_DoesNotBecomeAuthoredTransformDrift(string kind)
        {
            using var fixture = new LayoutFixture(kind);
            fixture.Rebuild();
            Assert.That(fixture.Instance.drivenByObject, Is.Not.Null,
                "Fixture must exercise a real UGUI layout driver.");
            Assert.That(fixture.HasDrift(), Is.False,
                "Canonical layout results are not authored prefab overrides.");
        }

        [TestCase("horizontal")]
        [TestCase("fitter")]
        public void CanonicalLayout_AfterRootReactivationBeforeRebuild_IsNotTransformDrift(string kind)
        {
            using var fixture = new LayoutFixture(kind);
            fixture.Rebuild();
            Assert.That(fixture.Instance.drivenByObject, Is.Not.Null,
                "The lifecycle boundary must begin with a real layout calculation.");
            Assert.That(fixture.HasDrift(), Is.False);

            fixture.Root.SetActive(false);
            Assert.That(fixture.Instance.drivenByObject, Is.Null,
                "Deactivation must clear the actual UGUI tracker.");
            fixture.Root.SetActive(true);
            // Do not rebuild here: inspect the enabled-but-not-yet-rebuilt window.
            // 此处不重建，准确覆盖重新启用后、下一次布局计算前的窗口。
            Assert.That(fixture.Instance.gameObject.activeInHierarchy, Is.True);
            Assert.That(fixture.Instance.drivenByObject, Is.Null,
                "The test must observe the cleared tracker before layout repopulates it.");
            Assert.That(fixture.HasDrift(), Is.False,
                "Reactivating an unchanged canonical layout must not become authored drift.");
        }

        [TestCase("horizontal")]
        [TestCase("fitter")]
        public void CanonicalLayout_InactivePrefabSourceAfterSceneReload_IsNotTransformDrift(string kind)
        {
            using var fixture = new LayoutFixture(kind, true);
            Assert.That(fixture.Source.transform.root.gameObject.activeSelf, Is.False,
                "The prefab source must match the inactive UI-root lifecycle in the real Scenes.");
            Assert.That(fixture.Root.activeInHierarchy, Is.True,
                "The owned Scene instance must override the inactive prefab root to active.");
            fixture.Rebuild();
            Assert.That(fixture.Instance.drivenByObject, Is.Not.Null,
                "Save only after a real layout calculation, without synthetic rect edits.");
            Assert.That(fixture.HasDrift(), Is.False);

            fixture.SaveAndReload();
            Assert.That(fixture.Source.transform.root.gameObject.activeSelf, Is.False);
            Assert.That(fixture.Root.activeInHierarchy, Is.True,
                "Reload must retain the active Scene-instance override.");
            Assert.That(fixture.HasDrift(), Is.False,
                "Saving and reloading a canonical layout must not become authored drift. "
                + $"activeInHierarchy={fixture.Instance.gameObject.activeInHierarchy}, "
                + $"drivenBy={fixture.Instance.drivenByObject}, "
                + $"anchors={fixture.Instance.anchorMin}/{fixture.Instance.anchorMax}, "
                + $"position={fixture.Instance.anchoredPosition}, size={fixture.Instance.sizeDelta}");
        }

        [TestCase("horizontal", "m_AnchoredPosition.x", "17")]
        [TestCase("fitter", "m_SizeDelta.y", "317")]
        public void NonDrivenAuthoredOverride_IsRejectedAfterDisablingLayout(
            string kind, string propertyPath, string value)
        {
            using var fixture = new LayoutFixture(kind);
            fixture.Rebuild();
            if (kind == "horizontal")
                fixture.Root.GetComponent<HorizontalLayoutGroup>().enabled = false;
            else
                fixture.Instance.GetComponent<ContentSizeFitter>().enabled = false;
            Assert.That(fixture.Instance.drivenByObject, Is.Null,
                "An editable transform must no longer be controlled by layout.");

            fixture.Instance.anchorMin = fixture.Source.anchorMin;
            fixture.Instance.anchorMax = fixture.Source.anchorMax;
            fixture.Instance.pivot = fixture.Source.pivot;
            fixture.Instance.sizeDelta = fixture.Source.sizeDelta;
            fixture.Instance.anchoredPosition3D = fixture.Source.anchoredPosition3D;
            fixture.Instance.localRotation = fixture.Source.localRotation;
            fixture.Instance.localScale = fixture.Source.localScale;
            Assert.That(fixture.HasDrift(), Is.False,
                "Negative control: source geometry must pass before the editable override.");

            var expected = float.Parse(value, CultureInfo.InvariantCulture);
            if (propertyPath == "m_AnchoredPosition.x")
                fixture.Instance.anchoredPosition = new Vector2(expected, fixture.Instance.anchoredPosition.y);
            else
                fixture.Instance.sizeDelta = new Vector2(fixture.Instance.sizeDelta.x, expected);
            PrefabUtility.RecordPrefabInstancePropertyModifications(fixture.Instance);
            var actual = propertyPath == "m_AnchoredPosition.x"
                ? fixture.Instance.anchoredPosition.x : fixture.Instance.sizeDelta.y;
            Assert.That(actual, Is.EqualTo(expected), "The fixture must retain the actual editable value.");
            var authored = (PrefabUtility.GetPropertyModifications(fixture.Root)
                    ?? Array.Empty<PropertyModification>()).SingleOrDefault(modification =>
                    modification.target == fixture.Source
                    && modification.propertyPath == propertyPath);
            Assert.That(authored, Is.Not.Null, "The editable override must actually be recorded.");
            Assert.That(float.Parse(authored.value, CultureInfo.InvariantCulture), Is.EqualTo(expected),
                "Do not mistake an overwritten layout value for the supplied override.");
            Assert.That(fixture.HasDrift(), Is.True,
                "Editable, non-driven transform drift must still be rejected.");
        }

        [TestCase("horizontal")]
        [TestCase("fitter")]
        public void LayoutDriverConfigurationDrift_IsStillRejected(string kind)
        {
            using var fixture = new LayoutFixture(kind);
            if (kind == "horizontal")
                fixture.Root.GetComponent<HorizontalLayoutGroup>().spacing += 13f;
            else
                fixture.Instance.GetComponent<ContentSizeFitter>().verticalFit =
                    ContentSizeFitter.FitMode.MinSize;
            fixture.Rebuild();
            Assert.That(fixture.HasDrift(), Is.True,
                "Only a canonical driver's calculated output may differ from prefab geometry.");
        }

        [TestCase("horizontal")]
        [TestCase("fitter")]
        public void TrackerCleared_LayoutDriverConfigurationDrift_IsStillRejected(string kind)
        {
            using var fixture = new LayoutFixture(kind);
            fixture.Rebuild();
            Assert.That(fixture.Instance.drivenByObject, Is.Not.Null);
            if (kind == "horizontal")
                fixture.Root.GetComponent<HorizontalLayoutGroup>().spacing += 13f;
            else
                fixture.Instance.GetComponent<ContentSizeFitter>().verticalFit =
                    ContentSizeFitter.FitMode.MinSize;
            fixture.Root.SetActive(false);
            fixture.Root.SetActive(true);
            Assert.That(fixture.Instance.drivenByObject, Is.Null);
            Assert.That(fixture.HasDrift(), Is.True,
                "A cleared tracker must not hide a noncanonical layout configuration.");
        }

        [Test]
        public void TrackerCleared_ChildInactiveInBothSourceAndInstance_IsNotOwnedByParentLayout()
        {
            using var fixture = new LayoutFixture("horizontal", sourceChildInactive: true);
            fixture.Rebuild();
            Assert.That(fixture.Instance.drivenByObject, Is.Null);
            Assert.That(fixture.Source.gameObject.activeSelf, Is.False);
            Assert.That(fixture.Instance.gameObject.activeSelf, Is.False);
            fixture.Instance.anchoredPosition = new Vector2(17f, fixture.Instance.anchoredPosition.y);
            Assert.That(fixture.Instance.anchoredPosition.x, Is.EqualTo(17f));
            Assert.That(fixture.HasDrift(), Is.True,
                "Parent layout does not own a child whose own activeSelf is false.");
        }

        [TestCase(false, true)]
        [TestCase(true, false)]
        public void TrackerCleared_ChildIgnoredByEitherSourceOrInstance_IsNotOwnedByParentLayout(
            bool sourceIgnores, bool instanceIgnores)
        {
            using var fixture = new LayoutFixture("horizontal", sourceIgnoreLayout: sourceIgnores);
            fixture.Rebuild();
            fixture.Instance.GetComponent<LayoutElement>().ignoreLayout = instanceIgnores;
            fixture.Root.SetActive(false);
            fixture.Root.SetActive(true);
            Assert.That(fixture.Source.GetComponent<LayoutElement>().ignoreLayout, Is.EqualTo(sourceIgnores));
            Assert.That(fixture.Instance.GetComponent<LayoutElement>().ignoreLayout, Is.EqualTo(instanceIgnores));
            Assert.That(fixture.Instance.drivenByObject, Is.Null);
            fixture.Instance.anchoredPosition = new Vector2(17f, fixture.Instance.anchoredPosition.y);
            Assert.That(fixture.Instance.anchoredPosition.x, Is.EqualTo(17f));
            Assert.That(fixture.HasDrift(), Is.True,
                "Both the source and instance child must participate in their parent layouts.");
        }

        [Test]
        public void UnrelatedNonNullTracker_DoesNotFallBackToCanonicalParentLayout()
        {
            using var fixture = new LayoutFixture("horizontal");
            fixture.Rebuild();
            fixture.Root.SetActive(false);
            fixture.Root.SetActive(true);
            Assert.That(fixture.Instance.drivenByObject, Is.Null);
            var unrelatedTracker = new DrivenRectTransformTracker();
            try
            {
                // The tracker owner is deliberately not the adjacent canonical LayoutGroup.
                // 真实 tracker 的 owner 刻意不是旁边的标准 LayoutGroup。
                unrelatedTracker.Add(fixture.Root, fixture.Instance,
                    DrivenTransformProperties.AnchoredPositionX);
                fixture.Instance.anchoredPosition = new Vector2(17f, fixture.Instance.anchoredPosition.y);
                Assert.That(fixture.Instance.drivenByObject, Is.SameAs(fixture.Root));
                Assert.That(fixture.Instance.anchoredPosition.x, Is.EqualTo(17f));
                Assert.That(fixture.HasDrift(), Is.True,
                    "A different non-null driver must not inherit another component's ownership.");
            }
            finally
            {
                unrelatedTracker.Clear();
            }
        }

        [TestCase("size")]
        [TestCase("pivot")]
        public void NonDrivenTransformPropertyDrift_IsStillRejected(string property)
        {
            using var fixture = new LayoutFixture("horizontal");
            if (property == "size") fixture.Instance.sizeDelta += new Vector2(9f, 0f);
            else fixture.Instance.pivot = new Vector2(.3f, .7f);
            fixture.Rebuild();
            Assert.That(fixture.HasDrift(), Is.True,
                "A layout driver must not exempt properties it does not calculate.");
        }

        private sealed class LayoutFixture : IDisposable
        {
            private readonly string folder;
            private readonly Scene preview;
            private readonly Scene originalActive;
            private readonly string scenePath;
            private Scene ownedScene;
            public GameObject Root { get; private set; }
            public RectTransform Instance { get; private set; }
            public RectTransform Source { get; }

            public LayoutFixture(string kind, bool inactivePrefabSourceInSavedScene = false,
                bool sourceChildInactive = false, bool? sourceIgnoreLayout = null)
            {
                originalActive = SceneManager.GetActiveScene();
                folder = "Assets/__Phase6LayoutDriver_" + Guid.NewGuid().ToString("N");
                AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
                preview = EditorSceneManager.NewPreviewScene();
                var candidate = new GameObject("LayoutRoot", typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(candidate, preview);
                ((RectTransform)candidate.transform).sizeDelta = new Vector2(240f, 100f);
                var child = new GameObject("Target", typeof(RectTransform));
                child.transform.SetParent(candidate.transform, false);
                var target = (RectTransform)child.transform;
                target.anchorMin = target.anchorMax = new Vector2(.5f, .5f);
                target.sizeDelta = kind == "horizontal" ? new Vector2(48f, 48f) : new Vector2(40f, 480f);
                if (kind == "horizontal")
                {
                    var layout = candidate.AddComponent<HorizontalLayoutGroup>();
                    layout.spacing = 8f;
                    layout.childAlignment = TextAnchor.MiddleCenter;
                    layout.childControlWidth = layout.childControlHeight = false;
                    layout.childForceExpandWidth = layout.childForceExpandHeight = false;
                }
                else
                {
                    var fitter = child.AddComponent<ContentSizeFitter>();
                    fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                    fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                }
                if (sourceIgnoreLayout.HasValue)
                    child.AddComponent<LayoutElement>().ignoreLayout = sourceIgnoreLayout.Value;
                if (sourceChildInactive) child.SetActive(false);
                if (inactivePrefabSourceInSavedScene) candidate.SetActive(false);
                var path = folder + "/Layout.prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(candidate, path);
                UnityEngine.Object.DestroyImmediate(candidate);
                Source = (RectTransform)prefab.transform.Find("Target");
                var instanceScene = preview;
                if (inactivePrefabSourceInSavedScene)
                {
                    // Create an owned empty asset; never save the caller's Untitled Scene.
                    // 仅创建本测试的空 Scene，绝不保存调用者未命名的场景。
                    scenePath = folder + "/LayoutScene.unity";
                    var createScene = typeof(EditorSceneManager).GetMethod("CreateSceneAsset",
                        BindingFlags.Static | BindingFlags.NonPublic, null,
                        new[] { typeof(string), typeof(bool) }, null);
                    if (createScene == null || !(bool)createScene.Invoke(null,
                            new object[] { scenePath, false }))
                        throw new InvalidOperationException("Could not create the owned layout Scene.");
                    ownedScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                    instanceScene = ownedScene;
                }
                Root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, instanceScene);
                Instance = (RectTransform)Root.transform.Find("Target");
                if (inactivePrefabSourceInSavedScene)
                {
                    Root.SetActive(true);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(Root);
                }
            }

            public void SaveAndReload()
            {
                Assert.That(ownedScene.IsValid() && ownedScene.isLoaded, Is.True);
                EditorSceneManager.MarkSceneDirty(ownedScene);
                Assert.That(EditorSceneManager.SaveScene(ownedScene, scenePath), Is.True);
                Assert.That(EditorSceneManager.CloseScene(ownedScene, true), Is.True);
                ownedScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                Root = ownedScene.GetRootGameObjects().Single();
                Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(Root),
                    Is.SameAs(Source.transform.root.gameObject),
                    "The owned empty Scene must reload the exact fixture prefab instance.");
                Instance = (RectTransform)Root.transform.Find("Target");
            }

            public void Rebuild() => LayoutRebuilder.ForceRebuildLayoutImmediate(
                Instance.GetComponent<ContentSizeFitter>() != null
                    ? Instance : (RectTransform)Root.transform);

            public bool HasDrift()
            {
                var before = CaptureReadOnlyState();
                var result = (bool)typeof(Phase6DecorationValidator)
                    .GetMethod("HasPrefabOwnedTransformDrift", BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, new object[] { Instance, Source });
                Assert.That(CaptureReadOnlyState(), Is.EqualTo(before),
                    "Validation must not rebuild layout, change fields/trackers, or dirty any Scene.");
                return result;
            }

            private object[] CaptureReadOnlyState() => new object[]
            {
                EditorJsonUtility.ToJson(Root),
                EditorJsonUtility.ToJson(Instance.gameObject),
                Root.GetComponentsInChildren<Component>(true).Select(component =>
                    EditorJsonUtility.ToJson(component)).ToArray(),
                Source.transform.root.GetComponentsInChildren<Component>(true).Select(component =>
                    EditorJsonUtility.ToJson(component)).ToArray(),
                Instance.drivenByObject,
                Root.activeInHierarchy,
                Instance.gameObject.activeInHierarchy,
                Root.scene.isDirty,
                EditorUtility.IsDirty(Root),
                EditorUtility.IsDirty(Instance),
                EditorUtility.IsDirty(Source),
                SceneManager.GetActiveScene().handle,
                Enumerable.Range(0, SceneManager.sceneCount).Select(index =>
                {
                    var scene = SceneManager.GetSceneAt(index);
                    return $"{scene.handle}:{scene.isLoaded}:{scene.isDirty}";
                }).ToArray()
            };

            public void Dispose()
            {
                if (ownedScene.IsValid() && ownedScene.isLoaded)
                    EditorSceneManager.CloseScene(ownedScene, true);
                if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
                if (originalActive.IsValid() && originalActive.isLoaded)
                    SceneManager.SetActiveScene(originalActive);
                AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
