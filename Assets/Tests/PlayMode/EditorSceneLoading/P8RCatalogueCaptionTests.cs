#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using AnimalCafe.Decoration;
using AnimalCafe.UI.Decoration;
using AnimalCafe.UI.P8R;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AnimalCafe.Tests.PlayMode.EditorSceneLoading
{
    /// <summary>Real-screen captions must render their complete two-line names without shrinking.
    /// 使用真实 Screen 尺寸验证完整名称，不把 label.text 完整误当成字形全部可见。</summary>
    public sealed class P8RCatalogueCaptionTests
    {
        private Scene ownedScene;
        private float previousTimeScale;
        private Vector2? previousLogicalViewport;

        [SetUp]
        public void RecordBoundary()
        {
            previousTimeScale = Time.timeScale;
            previousLogicalViewport = P8RMobileMetrics.EditorLogicalViewportOverride;
        }

        [UnityTearDown]
        public IEnumerator ReleaseScene()
        {
            if (ownedScene.IsValid() && ownedScene.isLoaded)
            {
                var inputAssets = Phase8SceneInputTestCleanup.CaptureAssets(ownedScene);
                foreach (var controller in ownedScene.GetRootGameObjects()
                             .SelectMany(root => root.GetComponentsInChildren<DecorationModeController>(true)))
                    controller.enabled = false;
                SceneManager.SetActiveScene(SceneManager.CreateScene("P8RCaptionCleanup"));
                var unload = SceneManager.UnloadSceneAsync(ownedScene);
                while (unload != null && !unload.isDone) yield return null;
                Phase8SceneInputTestCleanup.DisposeReleasedAssets(inputAssets);
            }
            P8RMobileMetrics.EditorLogicalViewportOverride = previousLogicalViewport;
            Time.timeScale = previousTimeScale;
            yield return null;
        }

        [UnityTest]
        public IEnumerator SmallPhone_ShibaCaptionKeepsEveryGlyphAcrossStateRebinding()
        {
            yield return CheckCaption(new Vector2(480, 854), 1.5f);
        }

        [UnityTest]
        public IEnumerator ReferencePhone_ShibaCaptionKeepsEveryGlyphAcrossStateRebinding()
        {
            yield return CheckCaption(new Vector2(1080, 1920), 3f);
        }

        private IEnumerator CheckCaption(Vector2 pixels, float density)
        {
            using (var screen = new P8RReferenceLayoutTests.NativeScreenSize())
            {
                screen.Resize(pixels);
                P8RMobileMetrics.EditorLogicalViewportOverride = pixels / density;
                ownedScene = EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/MainCafe.unity",
                    new LoadSceneParameters(LoadSceneMode.Single));
                yield return null; yield return null; yield return null;
                Assert.That(new Vector2(Screen.width, Screen.height), Is.EqualTo(pixels));
                var controller = Find<DecorationModeController>();
                controller.EnterDecorationMode();
                Assert.That(controller.TryChangeMode(DecorationModeKind.WallDecor), Is.True);
                Find<DecorationCatalogueView>().ShowCatalogue();
                yield return Settle();

                var tile = Find<DecorationCatalogueView>().GetComponentsInChildren<DecorationCatalogueTileView>()
                    .Single(item => item.ItemId == "wall-decor.shiba-painting.01");
                var model = Field<DecorationCatalogueItemModel>(tile, "boundItem");
                var label = Field<TMP_Text>(tile, "nameLabel");
                var thumbnail = Field<Image>(tile, "thumbnailImage");
                var well = tile.transform.Find("ThumbnailWell").GetComponent<Image>();
                var tileGeometry = Geometry((RectTransform)tile.transform);
                var wellGeometry = Geometry(well.rectTransform);
                var thumbnailGeometry = Geometry(thumbnail.rectTransform);
                var font = label.font;
                var fontSize = label.fontSize;
                var thumbnailSprite = thumbnail.sprite;
                var thumbnailColor = thumbnail.color;
                var wellColor = well.color;
                var cardColor = tile.GetComponent<Button>().image.color;
                var units = P8RMobileMetrics.For(tile).Units(1);
                var tileRect = (RectTransform)tile.transform;
                Assert.That(tileRect.rect.width / units, Is.EqualTo(68f).Within(.05f),
                    "The approved compact card must use the 68-logical width.");
                Assert.That(tileRect.rect.height / units, Is.EqualTo(84f).Within(.05f),
                    "The approved compact card must use the 84-logical height while retaining its caption and thumbnail.");
                Assert.That(Vector2.Distance(label.rectTransform.rect.size / units, new Vector2(62f, 34f)), Is.LessThan(.05f),
                    "Named captions must release vertical space while retaining the approved horizontal width.");
                Assert.That(Vector2.Distance(well.rectTransform.rect.size / units, new Vector2(58f, 40f)), Is.LessThan(.05f),
                    "The thumbnail backing must receive the redistributed card height.");
                Assert.That(Vector2.Distance(thumbnail.rectTransform.rect.size / units, new Vector2(52f, 34f)), Is.LessThan(.05f),
                    "The thumbnail preview must receive the redistributed card height.");
                AssertContained(tileRect, well.rectTransform, units * .05f, "Thumbnail backing inside card");
                AssertContained(well.rectTransform, thumbnail.rectTransform, units * .05f, "Thumbnail inside backing");
                var wellInTile = RectTransformUtility.CalculateRelativeRectTransformBounds(tileRect, well.rectTransform);
                var captionInTile = RectTransformUtility.CalculateRelativeRectTransformBounds(tileRect, label.rectTransform);
                Assert.That(wellInTile.min.y, Is.GreaterThanOrEqualTo(captionInTile.max.y - units * .05f),
                    "The thumbnail backing must not cover the readable caption reservation.");
                AssertCaption(label, density);

                // Rebind the real model, then cycle using/preview state and disable/enable the view.
                // 不修改文案或共享资产；只操作当前 Play Mode scene 的实例。
                foreach (var state in new[] { Vector2Int.zero, Vector2Int.one, new Vector2Int(0, 1) })
                {
                    tile.Bind(model, null);
                    tile.SetSurfaceState(state.x != 0, state.y != 0);
                    tile.enabled = false; tile.enabled = true;
                    yield return Settle();
                    AssertCaption(label, density);
                    AssertGeometryUnchanged((RectTransform)tile.transform, tileGeometry);
                    AssertGeometryUnchanged(well.rectTransform, wellGeometry);
                    AssertGeometryUnchanged(thumbnail.rectTransform, thumbnailGeometry);
                    Assert.That(label.font, Is.SameAs(font));
                    Assert.That(label.fontSize, Is.EqualTo(fontSize));
                    Assert.That(thumbnail.sprite, Is.SameAs(thumbnailSprite));
                    Assert.That(thumbnail.color, Is.EqualTo(thumbnailColor));
                    Assert.That(well.color, Is.EqualTo(wellColor));
                    Assert.That(tile.GetComponent<Button>().image.color, Is.EqualTo(cardColor));
                }
            }
        }

        private static void AssertCaption(TMP_Text label, float density)
        {
            label.ForceMeshUpdate(true, true);
            Assert.That(label.text, Is.EqualTo("Shiba Painting"), "The source name must remain complete.");
            Assert.That(label.enableAutoSizing, Is.False, "Caption fit cannot shrink the approved font.");
            Assert.That(label.fontSize * label.GetComponentInParent<Canvas>().rootCanvas.scaleFactor / density,
                Is.EqualTo(11.5f).Within(.05f));
            Assert.That(label.textInfo.lineCount, Is.EqualTo(2));
            Assert.That(label.isTextTruncated, Is.False, "A full label.text does not prove all glyphs were rendered.");
            var visible = new string(label.textInfo.characterInfo.Take(label.textInfo.characterCount)
                .Where(character => character.isVisible).Select(character => character.character).ToArray());
            Assert.That(visible, Is.EqualTo("ShibaPainting"), "The final g must render, not be silently truncated.");
            Assert.That(label.GetPreferredValues(label.text, label.rectTransform.rect.width, Mathf.Infinity).y,
                Is.LessThanOrEqualTo(label.rectTransform.rect.height + .1f));
        }

        private T Find<T>() where T : Component => ownedScene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();
        private static T Field<T>(object owner, string name) => (T)owner.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
        private static Vector2[] Geometry(RectTransform rect) =>
            new[] { rect.anchorMin, rect.anchorMax, rect.pivot, rect.anchoredPosition, rect.sizeDelta };
        private static void AssertGeometryUnchanged(RectTransform rect, Vector2[] expected)
        {
            var actual = Geometry(rect);
            var fields = new[] { "anchorMin", "anchorMax", "pivot", "anchoredPosition", "sizeDelta" };
            Assert.That(actual.Length, Is.EqualTo(expected.Length));
            for (var i = 0; i < actual.Length; i++)
            {
                // Normalized anchors stay stricter; positions/sizes allow only subpixel Canvas rounding.
                // 比例坐标仍用严格容差；位置与尺寸仅容许 0.001 Canvas unit 的浮点往返误差。
                var tolerance = i < 3 ? .000001f : .001f;
                Assert.That(actual[i].x, Is.EqualTo(expected[i].x).Within(tolerance), rect.name + "." + fields[i] + ".x");
                Assert.That(actual[i].y, Is.EqualTo(expected[i].y).Within(tolerance), rect.name + "." + fields[i] + ".y");
            }
        }
        private static void AssertContained(RectTransform outer, RectTransform inner, float tolerance, string label)
        {
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(outer, inner);
            Assert.That(bounds.min.x, Is.GreaterThanOrEqualTo(outer.rect.xMin - tolerance), label + " left");
            Assert.That(bounds.max.x, Is.LessThanOrEqualTo(outer.rect.xMax + tolerance), label + " right");
            Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(outer.rect.yMin - tolerance), label + " bottom");
            Assert.That(bounds.max.y, Is.LessThanOrEqualTo(outer.rect.yMax + tolerance), label + " top");
        }
        private static IEnumerator Settle() { yield return new WaitForSecondsRealtime(.3f); Canvas.ForceUpdateCanvases(); }
    }
}
#endif
