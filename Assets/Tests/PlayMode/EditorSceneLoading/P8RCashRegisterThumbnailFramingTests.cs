#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using AnimalCafe.Decoration;
using AnimalCafe.UI.Decoration;
using AnimalCafe.UI.P8R;
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
    /// <summary>
    /// Verifies the one-item Cash Register framing without changing approved card geometry.
    /// 验证只放大收银机可见主体，不改已批准的卡片尺寸。
    /// </summary>
    public sealed class P8RCashRegisterThumbnailFramingTests
    {
        private const string CashRegisterId = "equipment.cash-register.01";
        private const string CoffeeMachineId = "equipment.coffee-machine.01";
        private const string CounterId = "furniture.counter.module.01";
        private const string CataloguePath = "Assets/UI/P8R/DC_P8RFurniture.asset";

        private Scene ownedScene;
        private float previousTimeScale;
        private Vector2? previousLogicalViewport;
        private P8RReferenceLayoutTests.NativeScreenSize screen;
        private P8RReferenceLayoutTests.RealGameViewSize gameView;

        [SetUp]
        public void RecordRuntimeBoundary()
        {
            ownedScene = default;
            previousTimeScale = Time.timeScale;
            previousLogicalViewport = P8RMobileMetrics.EditorLogicalViewportOverride;
            screen = null;
            gameView = null;
        }

        [UnityTearDown]
        public IEnumerator ReleaseOwnedScene()
        {
            if (gameView != null)
            {
                gameView.Dispose();
                gameView = null;
                yield return null;
            }

            if (screen != null)
            {
                screen.Dispose();
                screen = null;
                yield return null;
            }

            if (ownedScene.IsValid() && ownedScene.isLoaded)
            {
                var inputAssets = Phase8SceneInputTestCleanup.CaptureAssets(ownedScene);
                foreach (var controller in ownedScene.GetRootGameObjects()
                             .SelectMany(root => root.GetComponentsInChildren<DecorationModeController>(true)))
                {
                    controller.enabled = false;
                }

                SceneManager.SetActiveScene(SceneManager.CreateScene("P8RCashThumbnailCleanup"));
                var unload = SceneManager.UnloadSceneAsync(ownedScene);
                while (unload != null && !unload.isDone)
                {
                    yield return null;
                }

                Phase8SceneInputTestCleanup.DisposeReleasedAssets(inputAssets);
            }

            P8RMobileMetrics.EditorLogicalViewportOverride = previousLogicalViewport;
            Time.timeScale = previousTimeScale;
            yield return null;
        }

        [UnityTest]
        public IEnumerator CashRegister_EnlargesActualSubjectWithSamePixels_AndReleasesOnlyItsRuntimeFrame()
        {
            var pixels = new Vector2(480f, 854f);
            var capture = Environment.GetEnvironmentVariable("ANIMALCAFE_P8R_CASH_THUMBNAIL_CAPTURE") == "1";
            if (capture)
            {
                gameView = new P8RReferenceLayoutTests.RealGameViewSize();
                gameView.Resize(pixels);
            }
            else
            {
                screen = new P8RReferenceLayoutTests.NativeScreenSize();
                screen.Resize(pixels);
            }

            yield return new WaitForSecondsRealtime(.3f);
            P8RMobileMetrics.EditorLogicalViewportOverride = pixels / 1.5f;
            ownedScene = EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/MainCafe.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;
            yield return null;
            Assert.That(new Vector2(Screen.width, Screen.height), Is.EqualTo(pixels),
                "The Cash test must render at the requested native Game View size.");
            Assert.That(new Vector2(UnityEngine.Camera.main.pixelWidth, UnityEngine.Camera.main.pixelHeight),
                Is.EqualTo(pixels), "The MainCafe camera must render the same native pixel size.");

            var controller = Find<DecorationModeController>();
            controller.EnterDecorationMode();
            var catalogueView = Find<DecorationCatalogueView>();
            catalogueView.ShowCatalogue();
            yield return new WaitForSecondsRealtime(.3f);
            Canvas.ForceUpdateCanvases();

            var tile = catalogueView.GetComponentsInChildren<DecorationCatalogueTileView>(true)
                .Single(candidate => candidate.ItemId == CashRegisterId
                    && candidate.gameObject.activeInHierarchy);
            var image = Field<Image>(tile, "thumbnailImage");
            var catalogue = AssetDatabase.LoadAssetAtPath<DecorationCatalogueAsset>(CataloguePath);
            var cashEntry = catalogue.Entries.Single(entry => entry.Definition.DefinitionId == CashRegisterId);
            var coffeeEntry = catalogue.Entries.Single(entry => entry.Definition.DefinitionId == CoffeeMachineId);
            var counterEntry = catalogue.Entries.Single(entry => entry.Definition.DefinitionId == CounterId);
            var cashSource = cashEntry.Thumbnail;
            var cashSourceRect = cashSource.rect;
            var logicalUnit = P8RMobileMetrics.For(tile).Units(1f);

            AssertApprovedGeometry(tile, logicalUnit);
            Assert.That(image.preserveAspect, Is.True);
            Assert.That(image.rectTransform.localScale, Is.EqualTo(Vector3.one),
                "Framing must not fake the fix by scaling the approved 52 x 34 Image Rect.");

            var framedCash = image.sprite;
            var subjectSize = MeasureVisibleSubject(image, cashSource, logicalUnit);
            Assert.That(subjectSize.x, Is.GreaterThanOrEqualTo(20f),
                "The real Cash Register pixels must be at least 20 logical units wide.");
            Assert.That(subjectSize.y, Is.GreaterThanOrEqualTo(24f),
                "The real Cash Register pixels must be at least 24 logical units tall.");
            Assert.That(framedCash, Is.Not.SameAs(cashSource),
                "Cash Register needs a presentation-only framed Sprite, not a changed source Sprite.");
            Assert.That(framedCash.texture, Is.SameAs(cashSource.texture),
                "The framed Sprite must reuse the exact original Texture2D pixels.");

            ScrollTileToViewportCentre(catalogueView, tile);
            yield return CaptureNativeIfRequested(
                "outputs/p8r-cash-thumbnail-framing-20260914/480x854/cash-register-framed-native2.png",
                image.GetComponentInParent<Canvas>().rootCanvas);

            tile.Bind(Item(cashEntry, DecorationCatalogueItemKind.CashRegister), null);
            Assert.That(image.sprite, Is.SameAs(framedCash),
                "Rebinding the unchanged Cash source must reuse its runtime framed Sprite.");

            tile.Bind(Item(coffeeEntry, DecorationCatalogueItemKind.CoffeeMachine), null);
            yield return null;
            Assert.That(image.sprite, Is.SameAs(coffeeEntry.Thumbnail),
                "Coffee Machine must retain its original thumbnail Sprite.");
            Assert.That(framedCash == null, Is.True,
                "The Cash-only runtime Sprite must be released when the tile is rebound.");
            Assert.That(cashSource, Is.Not.Null,
                "Releasing presentation framing must never destroy the source Sprite.");
            Assert.That(cashSource.texture, Is.Not.Null,
                "Releasing presentation framing must never destroy the source Texture2D.");

            tile.Bind(Item(counterEntry, DecorationCatalogueItemKind.Furniture), null);
            Assert.That(image.sprite, Is.SameAs(counterEntry.Thumbnail),
                "Counter thumbnails must not inherit Cash Register framing after reuse.");
            AssertApprovedGeometry(tile, logicalUnit);

            Assert.DoesNotThrow(() => tile.Bind(
                new DecorationCatalogueItemModel(CashRegisterId, "Cash Register", null,
                    DecorationCatalogueItemKind.CashRegister, false, cashEntry.Definition), null));
            Assert.That(image.sprite, Is.Null,
                "A missing Cash thumbnail must fall back to the existing empty-image behavior.");
            Assert.That(image.enabled, Is.False);

            tile.Bind(Item(cashEntry, DecorationCatalogueItemKind.CashRegister), null);
            var secondFrame = image.sprite;
            Assert.That(secondFrame, Is.Not.SameAs(cashSource));
            Assert.That(secondFrame, Is.Not.SameAs(framedCash),
                "A released runtime frame must not be reused after rebinding away.");
            Object.Destroy(tile.gameObject);
            yield return null;
            Assert.That(secondFrame == null, Is.True,
                "Destroying the tile must release its owned runtime framed Sprite.");
            Assert.That(cashSource, Is.Not.Null);
            Assert.That(cashSource.texture, Is.Not.Null);
            Assert.That(cashSource.rect, Is.EqualTo(cashSourceRect),
                "Presentation framing must leave the imported source Sprite rect unchanged.");
        }

        private static DecorationCatalogueItemModel Item(
            DecorationCatalogueEntry entry, DecorationCatalogueItemKind kind)
        {
            return new DecorationCatalogueItemModel(entry.Definition.DefinitionId,
                entry.Definition.DisplayName, entry.Thumbnail, kind, false, entry.Definition);
        }

        private static void ScrollTileToViewportCentre(
            DecorationCatalogueView catalogue, DecorationCatalogueTileView tile)
        {
            var scroll = catalogue.VerticalScroll;
            scroll.StopMovement();
            var tileRect = (RectTransform)tile.transform;
            var viewport = scroll.viewport;
            var centre = viewport.InverseTransformPoint(tileRect.TransformPoint(tileRect.rect.center));
            scroll.content.anchoredPosition += new Vector2(0f, viewport.rect.center.y - centre.y);
            Canvas.ForceUpdateCanvases();
        }

        private static IEnumerator CaptureNativeIfRequested(string relativePath, Canvas canvas)
        {
            if (Environment.GetEnvironmentVariable("ANIMALCAFE_P8R_CASH_THUMBNAIL_CAPTURE") != "1")
            {
                yield break;
            }

            Assert.That(Application.isBatchMode, Is.False,
                "The optional Cash Register evidence must use a normal Editor Game View.");
            var path = Path.GetFullPath(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            Assert.That(File.Exists(path), Is.False, "Never overwrite earlier review evidence.");
            yield return new WaitForSecondsRealtime(.3f);
            var shaderDeadline = Time.realtimeSinceStartup + 45f;
            while (ShaderUtil.anythingCompiling && Time.realtimeSinceStartup < shaderDeadline)
            {
                yield return null;
            }

            Assert.That(ShaderUtil.anythingCompiling, Is.False,
                "Do not capture temporary shader-compilation placeholders.");
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(UnityEngine.Camera.main.targetTexture, Is.Null);
            ScreenCapture.CaptureScreenshot(path, 1);
            var deadline = Time.realtimeSinceStartup + 8f;
            while (!File.Exists(path) && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(File.Exists(path), Is.True,
                "Native capture unavailable; do not substitute a Camera RenderTexture capture.");
            var capture = new Texture2D(2, 2);
            try
            {
                Assert.That(capture.LoadImage(File.ReadAllBytes(path)), Is.True);
                Assert.That(new Vector2Int(capture.width, capture.height),
                    Is.EqualTo(new Vector2Int(480, 854)));
                Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            }
            finally
            {
                Object.Destroy(capture);
            }
        }

        private static Vector2 MeasureVisibleSubject(Image image, Sprite source, float logicalUnit)
        {
            Assert.That(image.sprite, Is.Not.Null);
            Assert.That(image.sprite.texture, Is.SameAs(source.texture));
            var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(decoded.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(source))), Is.True);
                var alphaBounds = AlphaBounds(decoded);
                var spriteRect = image.sprite.rect;
                Assert.That(spriteRect.xMin, Is.LessThanOrEqualTo(alphaBounds.xMin));
                Assert.That(spriteRect.yMin, Is.LessThanOrEqualTo(alphaBounds.yMin));
                Assert.That(spriteRect.xMax, Is.GreaterThanOrEqualTo(alphaBounds.xMax));
                Assert.That(spriteRect.yMax, Is.GreaterThanOrEqualTo(alphaBounds.yMax),
                    "Framing may remove transparent padding, but it must retain every visible subject pixel.");

                var imageRect = image.rectTransform.rect;
                var spriteAspect = spriteRect.width / spriteRect.height;
                var imageAspect = imageRect.width / imageRect.height;
                var drawnSize = spriteAspect > imageAspect
                    ? new Vector2(imageRect.width, imageRect.width / spriteAspect)
                    : new Vector2(imageRect.height * spriteAspect, imageRect.height);
                return new Vector2(
                    drawnSize.x * alphaBounds.width / spriteRect.width / logicalUnit,
                    drawnSize.y * alphaBounds.height / spriteRect.height / logicalUnit);
            }
            finally
            {
                Object.Destroy(decoded);
            }
        }

        private static Rect AlphaBounds(Texture2D texture)
        {
            var pixels = texture.GetPixels32();
            var minX = texture.width;
            var minY = texture.height;
            var maxX = -1;
            var maxY = -1;
            for (var y = 0; y < texture.height; y++)
            {
                for (var x = 0; x < texture.width; x++)
                {
                    if (pixels[y * texture.width + x].a < 16) continue;
                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                }
            }

            Assert.That(maxX, Is.GreaterThanOrEqualTo(minX), "The approved source needs visible pixels.");
            return Rect.MinMaxRect(minX, minY, maxX + 1, maxY + 1);
        }

        private static void AssertApprovedGeometry(DecorationCatalogueTileView tile, float logicalUnit)
        {
            var card = (RectTransform)tile.transform;
            var well = (RectTransform)tile.transform.Find("ThumbnailWell");
            var image = Field<Image>(tile, "thumbnailImage");
            var caption = Field<TMP_Text>(tile, "nameLabel");
            Assert.That(Vector2.Distance(card.rect.size / logicalUnit, new Vector2(68f, 84f)),
                Is.LessThan(.05f));
            Assert.That(Vector2.Distance(well.rect.size / logicalUnit, new Vector2(58f, 40f)),
                Is.LessThan(.05f));
            Assert.That(Vector2.Distance(image.rectTransform.rect.size / logicalUnit,
                new Vector2(52f, 34f)), Is.LessThan(.05f));
            Assert.That(caption.fontSize / logicalUnit, Is.EqualTo(11.5f).Within(.05f));
        }

        private T Find<T>() where T : Component => ownedScene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();

        private static T Field<T>(object owner, string name) => (T)owner.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    }
}
#endif
