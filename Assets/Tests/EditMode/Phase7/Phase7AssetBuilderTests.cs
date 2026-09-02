using System.Linq;
using AnimalCafe.Content;
using AnimalCafe.EditorTools.Phase7;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using AnimalCafe.UI.Decoration;
using System.IO;
using System.Security.Cryptography;
using UnityEngine.UI;

namespace AnimalCafe.Tests.EditMode.Phase7
{
    public sealed class Phase7AssetBuilderTests
    {
        [Test]
        public void BuildOrUpdateAssets_CreatesLitNormalMappedWallFinishMaterials()
        {
            Phase7SurfaceAssetBuilder.BuildOrUpdateAssets();

            var catalogues = new[]
            {
                Phase7AssetPaths.PaintCataloguePath,
                Phase7AssetPaths.WallpaperCataloguePath,
                Phase7AssetPaths.WainscotingCataloguePath
            };
            foreach (var cataloguePath in catalogues)
            foreach (var entry in AssetDatabase.LoadAssetAtPath<SurfaceStyleCatalogueAsset>(cataloguePath).Entries)
            {
                if (entry.IsNoneOption) continue;
                var material = entry.Material;
                Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"), entry.StyleId);
                Assert.That(material.GetTexture("_BumpMap"), Is.Not.Null,
                    entry.StyleId + " must keep visible surface relief under the fixed isometric camera.");
                Assert.That(material.IsKeywordEnabled("_NORMALMAP"), Is.True, entry.StyleId);
                Assert.That(material.GetFloat("_Smoothness"), Is.InRange(.05f, .35f), entry.StyleId);
                var normalPath = AssetDatabase.GetAssetPath(material.GetTexture("_BumpMap"));
                var importer = AssetImporter.GetAtPath(normalPath) as TextureImporter;
                Assert.That(importer, Is.Not.Null, entry.StyleId);
                Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.NormalMap), entry.StyleId);
                Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Repeat), entry.StyleId);
            }

            foreach (var path in new[]
                     {
                         "Assets/Art/Phase7/Materials/M_WallBody_Architectural.mat",
                         "Assets/Art/Phase7/Materials/M_WallCornerDepth.mat"
                     })
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                Assert.That(material, Is.Not.Null, path);
                Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"), path);
            }
        }

        [Test]
        public void BuildOrUpdateAssets_WainsNormalMapsUseOnlyAuthoredLuminanceGradients()
        {
            // Catches the diagonal crosshatch regression caused by mixing procedural fine/broad waves
            // into an authored Wainscoting texture before generating its normal map.
            Phase7SurfaceAssetBuilder.BuildOrUpdateAssets();

            AssertAuthoredOnlyWainsNormal(
                "Assets/Art/Phase7/Materials/M_Wainscoting_SagePlain_02.mat",
                "Assets/Art/Phase7/Textures/T_WallNormal_wainscoting_sage_plain.png");
            AssertAuthoredOnlyWainsNormal(
                "Assets/Art/Phase7/Materials/M_Wainscoting_WarmWhiteRail_01.mat",
                "Assets/Art/Phase7/Textures/T_WallNormal_wainscoting_warm_white_rail.png");
        }

        [Test]
        public void BuildOrUpdateAssets_UsesConservativeWainsNormalAndReliefStrength()
        {
            // Catches a regression where the Wains layer returns to fence-like exaggerated relief.
            Phase7SurfaceAssetBuilder.BuildOrUpdateAssets();

            foreach (var path in new[]
                     {
                         "Assets/Art/Phase7/Materials/M_Wainscoting_SagePlain_02.mat",
                         "Assets/Art/Phase7/Materials/M_Wainscoting_WarmWhiteRail_01.mat"
                     })
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                Assert.That(material, Is.Not.Null, path);
                Assert.That(material.GetFloat("_BumpScale"), Is.EqualTo(.22f).Within(.0001f), path);
                Assert.That(material.GetFloat("_Parallax"), Is.EqualTo(.05f).Within(.0001f), path);
            }
        }

        [Test]
        public void BuildOrUpdateAssets_PreservesAuthoredMountedThumbnailBytes()
        {
            var catalogue = AssetDatabase.LoadAssetAtPath<WallMountedCatalogueAsset>(
                Phase7AssetPaths.WallMountedProductionCataloguePath);
            var before = catalogue.Entries.ToDictionary(
                entry => entry.DefinitionId,
                entry => File.ReadAllBytes(AssetDatabase.GetAssetPath(entry.Thumbnail)));

            Phase7SurfaceAssetBuilder.BuildOrUpdateAssets();

            foreach (var entry in catalogue.Entries)
                Assert.That(File.ReadAllBytes(AssetDatabase.GetAssetPath(entry.Thumbnail)),
                    Is.EqualTo(before[entry.DefinitionId]), entry.DefinitionId);
        }

        [Test]
        public void AuthoredMountedThumbnail_UsesOutwardFacingFramingAndShowsPaintingArtwork()
        {
            Phase7SurfaceAssetBuilder.BuildOrUpdateAssets();
            var catalogue = AssetDatabase.LoadAssetAtPath<WallMountedCatalogueAsset>(
                Phase7AssetPaths.WallMountedProductionCataloguePath);
            var painting = catalogue.Entries.Single(entry =>
                entry.DefinitionId == "wall-decor.shiba-painting.01");
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(texture.LoadImage(File.ReadAllBytes(
                    AssetDatabase.GetAssetPath(painting.Thumbnail))), Is.True);
                var colourfulCenterPixels = 0;
                var sampledCenterPixels = 0;
                for (var y = texture.height / 5; y < texture.height * 4 / 5; y++)
                for (var x = texture.width * 3 / 10; x < texture.width * 7 / 10; x++)
                {
                    var colour = texture.GetPixel(x, y);
                    if (colour.a < .1f) continue;
                    sampledCenterPixels++;
                    var max = Mathf.Max(colour.r, Mathf.Max(colour.g, colour.b));
                    var min = Mathf.Min(colour.r, Mathf.Min(colour.g, colour.b));
                    if (max - min > .12f) colourfulCenterPixels++;
                }
                Assert.That(sampledCenterPixels, Is.GreaterThan(1000));
                Assert.That(colourfulCenterPixels / (float)sampledCenterPixels,
                    Is.GreaterThan(.18f),
                    "The painting thumbnail must show the supplied Shiba artwork, not the blank back of the model.");
            }
            finally { Object.DestroyImmediate(texture); }
        }

        [Test]
        public void AuthoredMountedThumbnails_ShowMountedPrefabWithTransparentBackground()
        {
            // The item keeps the approved mounted in-game viewing angle, while the
            // exported catalogue art contains no wall, floor, or opaque black backdrop.
            Phase7SurfaceAssetBuilder.BuildOrUpdateAssets();
            var wallDecor = AssetDatabase.LoadAssetAtPath<WallMountedCatalogueAsset>(
                Phase7AssetPaths.WallMountedProductionCataloguePath);
            var windows = AssetDatabase.LoadAssetAtPath<WallMountedCatalogueAsset>(
                Phase7AssetPaths.WindowCataloguePath);

            foreach (var entry in wallDecor.Entries.Concat(windows.Entries))
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    Assert.That(texture.LoadImage(File.ReadAllBytes(
                        AssetDatabase.GetAssetPath(entry.Thumbnail))), Is.True, entry.DefinitionId);

                    var borderPixels = 0;
                    var transparentBorderPixels = 0;
                    var visibleItemPixels = 0;
                    const int border = 24;
                    for (var y = 0; y < texture.height; y++)
                    for (var x = 0; x < texture.width; x++)
                    {
                        var colour = texture.GetPixel(x, y);
                        if (x < border || x >= texture.width - border ||
                            y < border || y >= texture.height - border)
                        {
                            borderPixels++;
                            if (colour.a < .05f) transparentBorderPixels++;
                        }

                        if (x >= texture.width / 5 && x < texture.width * 4 / 5 &&
                            y >= texture.height / 5 && y < texture.height * 4 / 5)
                        {
                            if (colour.a > .1f) visibleItemPixels++;
                        }
                    }

                    Assert.That(transparentBorderPixels / (float)borderPixels, Is.GreaterThan(.80f),
                        entry.DefinitionId + " must export the mounted-angle item on a transparent background.");
                    Assert.That(visibleItemPixels, Is.GreaterThan(350),
                        entry.DefinitionId + " must show the mounted prefab, not an empty transparent image.");
                }
                finally { Object.DestroyImmediate(texture); }
            }
        }

        [Test]
        public void BuildOrUpdateAssets_CreatesDeterministicProductionCataloguesAndSprites()
        {
            Phase7SurfaceAssetBuilder.BuildOrUpdateAssets();
            var first=Fingerprint(Phase7AssetPaths.Root,Phase7AssetPaths.UiRoot);
            Phase7SurfaceAssetBuilder.BuildOrUpdateAssets();
            Assert.That(Fingerprint(Phase7AssetPaths.Root,Phase7AssetPaths.UiRoot),Is.EqualTo(first));

            AssertCatalogue(Phase7AssetPaths.PaintCataloguePath, SurfaceStyleKind.Paint, 3);
            AssertCatalogue(Phase7AssetPaths.WallpaperCataloguePath, SurfaceStyleKind.Wallpaper, 2);
            AssertCatalogue(Phase7AssetPaths.WainscotingCataloguePath, SurfaceStyleKind.Wainscoting, 3);
            AssertCatalogue(Phase7AssetPaths.FloorCataloguePath, SurfaceStyleKind.Floor, 3);
            AssertKindMetadata(Phase7AssetPaths.FloorCataloguePath,"OneGrid",1f,1f);
            AssertKindMetadata(Phase7AssetPaths.WallpaperCataloguePath,"FullWall",1f,0f);
            AssertKindMetadata(Phase7AssetPaths.WainscotingCataloguePath,"WaistReference",1f,.65f);
            AssertKindMetadata(Phase7AssetPaths.PaintCataloguePath,"NotApplicable",0f,0f);

            var none = AssetDatabase.LoadAssetAtPath<SurfaceStyleDefinitionAsset>(
                Phase7AssetPaths.WainscotingNoneDefinitionPath);
            Assert.That(none, Is.Not.Null);
            Assert.That(none.IsNoneOption, Is.True);
            Assert.That(none.Material, Is.Null);
            Assert.That(none.Thumbnail, Is.Not.Null);
            Assert.That(AssetDatabase.GetMainAssetTypeAtPath(
                AssetDatabase.GetAssetPath(none.Thumbnail)), Is.EqualTo(typeof(Texture2D)));

            var mounted = AssetDatabase.LoadAssetAtPath<WallMountedCatalogueAsset>(
                Phase7AssetPaths.WallMountedProductionCataloguePath);
            Assert.That(mounted, Is.Not.Null);
            Assert.That(mounted.Entries.Count, Is.EqualTo(3));
            Assert.That(mounted.Entries.All(entry => entry.Prefab != null && entry.Thumbnail != null), Is.True);
            Assert.That(mounted.Entries.All(entry => entry.MaxVisualDepth <= 0.35f), Is.True);
            foreach(var entry in mounted.Entries)
            {
                var colliders=entry.Prefab.GetComponentsInChildren<Collider>(true);
                Assert.That(colliders,Is.Not.Empty,entry.DefinitionId);
                Assert.That(colliders.All(collider=>collider.isTrigger),Is.True,entry.DefinitionId+" must be selection-only trigger geometry");
                Assert.That(entry.Prefab.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true),Is.Empty);
            }

            AssertPrefabGraph<DecorationCatalogueView>(Phase7AssetPaths.CataloguePrefabPath);
            AssertPrefabGraph<DecorationActionBarView>(Phase7AssetPaths.ActionBarPrefabPath);
            AssertPrefabGraph<DecorationExitModalView>(Phase7AssetPaths.ExitModalPrefabPath);
            var noneTexture=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try
            {
                Assert.That(noneTexture.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(none.Thumbnail))),Is.True);
                var pixels=noneTexture.GetPixels32();
                Assert.That(pixels.Where((pixel,index)=>{var x=index%noneTexture.width;var y=index/noneTexture.width;return System.Math.Abs(x-y)<3||System.Math.Abs((noneTexture.width-1-x)-y)<3;})
                    .Count(pixel=>pixel.r>pixel.g*1.5f),Is.GreaterThan(noneTexture.width));
                var ringPixels=pixels.Where((pixel,index)=>{var x=index%noneTexture.width;var y=index/noneTexture.width;var dx=x-(noneTexture.width-1)*.5f;var dy=y-(noneTexture.height-1)*.5f;var radius=System.Math.Sqrt(dx*dx+dy*dy);return radius>19&&radius<25;})
                    .Count(pixel=>pixel.r>pixel.g*1.5f);
                Assert.That(ringPixels,Is.GreaterThan(noneTexture.width*2),"None icon must contain a readable crossed-circle ring, not only an X.");
            }
            finally{Object.DestroyImmediate(noneTexture);}

            var cataloguePrefab=AssetDatabase.LoadAssetAtPath<GameObject>(Phase7AssetPaths.CataloguePrefabPath);
            var catalogueView=cataloguePrefab.GetComponentInChildren<DecorationCatalogueView>(true);
            var catalogueSo=new SerializedObject(catalogueView);
            foreach(var property in new[]{"verticalScroll","categoryContent","categoryRowTemplate","categoryTileTemplate"})
                Assert.That(catalogueSo.FindProperty(property).objectReferenceValue,Is.Not.Null,"Phase7 production catalogue "+property);
            var tile=(DecorationCatalogueTileView)catalogueSo.FindProperty("categoryTileTemplate").objectReferenceValue;
            var tileRect=(RectTransform)tile.transform;
            Assert.That(tileRect.rect.width,Is.InRange(96f,144f));Assert.That(tileRect.rect.height,Is.InRange(96f,144f));

        }

        [Test]
        public void BuildOrUpdateAssets_AuthorsIntegratedPaperSheetTabsAndCompactCards()
        {
            Phase7SurfaceAssetBuilder.BuildOrUpdateAssets();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Phase7AssetPaths.CataloguePrefabPath);
            var catalogue = prefab.GetComponentInChildren<DecorationCatalogueView>(true);
            var tabs = catalogue.GetComponentInChildren<DecorationModeTabsView>(true);
            Assert.That(tabs, Is.Not.Null,
                "Mode tabs must belong to the Bottom Sheet prefab instead of a separate screen-bottom runtime root.");

            var verticalPanel = catalogue.transform.Find("ExpandedSheet/Phase7CategoryCatalogue/VerticalScroll")
                ?.GetComponent<Image>();
            Assert.That(verticalPanel, Is.Not.Null);
            Assert.That(verticalPanel.color, Is.Not.EqualTo(Color.white),
                "The catalogue viewport must not cover the approved paper panel with a plain white Image.");

            var so = new SerializedObject(catalogue);
            Assert.That(so.FindProperty("collapsedAnchoredPosition").vector2Value,
                Is.EqualTo(new Vector2(0f, -490f)),
                "Collapsed Bottom Sheet and its attached tabs must settle near the screen bottom.");
            Assert.That(so.FindProperty("hiddenAnchoredPosition").vector2Value.y,
                Is.LessThan(-520f),
                "The hidden state must remain below the collapsed handle position.");
            var collapsedRoot = (GameObject)so.FindProperty("collapsedRoot").objectReferenceValue;
            Assert.That(((RectTransform)collapsedRoot.transform).anchoredPosition.y,
                Is.EqualTo(668f).Within(.01f),
                "The expand handle needs a local offset that keeps it visible after the shared sheet collapse.");
            var rowTemplate = (GameObject)so.FindProperty("categoryRowTemplate").objectReferenceValue;
            var horizontal = rowTemplate.GetComponent<ScrollRect>().content.GetComponent<HorizontalLayoutGroup>();
            Assert.That(horizontal.spacing, Is.EqualTo(8f).Within(.01f),
                "Catalogue cards need a compact horizontal rhythm.");
            Assert.That(horizontal.childForceExpandWidth, Is.False,
                "Force-expand distributes spare viewport width between cards and recreates the large visual gaps.");
            Assert.That(horizontal.childForceExpandHeight, Is.False);
            Assert.That(horizontal.childControlWidth, Is.True);
            Assert.That(horizontal.childControlHeight, Is.True);
            var contentFitter = horizontal.GetComponent<ContentSizeFitter>();
            Assert.That(contentFitter, Is.Not.Null,
                "Horizontal content must size itself from the cards instead of remaining a fixed 900 px row.");
            Assert.That(contentFitter.horizontalFit, Is.EqualTo(ContentSizeFitter.FitMode.PreferredSize));
            var tile = (DecorationCatalogueTileView)so.FindProperty("categoryTileTemplate").objectReferenceValue;
            var size = ((RectTransform)tile.transform).sizeDelta;
            Assert.That(Mathf.Abs(size.x - size.y), Is.LessThanOrEqualTo(4f));
            Assert.That(size.x, Is.EqualTo(128f).Within(.01f));
            var tileImage = tile.GetComponent<Image>();
            Assert.That(tileImage.type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(tileImage.sprite, Is.Not.Null);
            Assert.That(tileImage.sprite.border.sqrMagnitude, Is.GreaterThan(0f));
            Assert.That(tileImage.color.r, Is.GreaterThan(tileImage.color.b),
                "Cards should use the approved warm paper colour.");
            var usingCheck = tile.transform.Find("UsingCheck").GetComponent<Image>();
            Assert.That(usingCheck.raycastTarget, Is.False);
            Assert.That(((RectTransform)usingCheck.transform).sizeDelta,
                Is.EqualTo(new Vector2(44f, 44f)));
            var checkLabel = usingCheck.GetComponentInChildren<TMPro.TMP_Text>(true);
            Assert.That(checkLabel, Is.Not.Null);
            Assert.That(checkLabel.text, Is.EqualTo("✓"));
            Assert.That(checkLabel.color.g, Is.GreaterThan(checkLabel.color.r * 1.25f));
            var previewOutline = tile.transform.Find("PreviewOutline").GetComponent<Image>();
            Assert.That(previewOutline.raycastTarget, Is.False);
            Assert.That(previewOutline.sprite, Is.Not.Null);
            Assert.That(previewOutline.type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(previewOutline.color.r, Is.GreaterThan(previewOutline.color.g));
            var noneOverlay = tile.transform.Find("NoneIcon").GetComponent<Image>();
            Assert.That(noneOverlay.raycastTarget, Is.False);
            Assert.That(noneOverlay.enabled, Is.False,
                "The crossed-circle is already authored into the None thumbnail; a default white overlay must not cover it.");
            var expandedPanel = catalogue.transform.Find("ExpandedSheet").GetComponent<Image>();
            Assert.That(expandedPanel.sprite, Is.Not.Null,
                "The Bottom Sheet panel itself needs a rounded sprite; rounded cards alone are not enough.");
            Assert.That(expandedPanel.type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(expandedPanel.sprite.border.sqrMagnitude, Is.GreaterThan(0f));
            Assert.That(catalogue.transform.Find("ExpandedSheet/Title"), Is.Null,
                "Only the legacy large ExpandedSheet Title must be removed.");
            var collapsedHandleLabel = catalogue.transform.Find("CollapsedHandle/Label")
                ?.GetComponent<TMPro.TMP_Text>();
            Assert.That(collapsedHandleLabel, Is.Not.Null,
                "Collapsed handle needs its own persistent label.");
            Assert.That(collapsedHandleLabel.text, Is.EqualTo("Catalogue"));

            var footerProperty = so.FindProperty("surfaceFooterHost");
            Assert.That(footerProperty, Is.Not.Null);
            var surfaceFooter = footerProperty.objectReferenceValue as RectTransform;
            Assert.That(surfaceFooter, Is.Not.Null);
            Assert.That(surfaceFooter.name, Is.EqualTo("SurfaceFooterHost"));
            Assert.That(surfaceFooter.parent, Is.SameAs(catalogue.transform),
                "Footer must share the moving Sheet root instead of being hidden with ExpandedSheet content.");
            Assert.That(tabs.transform.parent, Is.SameAs(catalogue.transform));
            Assert.That(expandedPanel.transform.parent, Is.SameAs(catalogue.transform));
            Assert.That(tabs.transform.GetSiblingIndex(), Is.GreaterThan(surfaceFooter.GetSiblingIndex()),
                "Tabs must remain visually in front of the footer.");
            var tabsRect = (RectTransform)tabs.transform;
            var expandedRect = (RectTransform)expandedPanel.transform;
            Assert.That(expandedRect.rect.height - tabsRect.anchoredPosition.y,
                Is.InRange(0f, 24f), "Folder Tabs and Sheet top must remain gapless.");
        }

        [Test]
        public void BuildOrUpdateAssets_PinsSurfaceFooterAtSheetBottomAndReservesCatalogueContent()
        {
            Phase7SurfaceAssetBuilder.BuildOrUpdateAssets();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Phase7AssetPaths.CataloguePrefabPath);
            var catalogue = prefab.GetComponentInChildren<DecorationCatalogueView>(true);
            var so = new SerializedObject(catalogue);
            var footer = (RectTransform)so.FindProperty("surfaceFooterHost").objectReferenceValue;
            var contentHost = catalogue.transform
                .Find("ExpandedSheet/Phase7CategoryCatalogue") as RectTransform;

            Assert.That(footer.anchoredPosition.y, Is.EqualTo(24f).Within(.01f),
                "Surface Confirm/Cancel must sit at the Bottom Sheet bottom, not in the screen centre.");
            Assert.That(footer.rect.height, Is.GreaterThanOrEqualTo(128f),
                "The Floor footer needs two non-overlapping rows with fixed internal margins.");
            Assert.That(contentHost, Is.Not.Null);
            Assert.That(contentHost.offsetMin.y, Is.GreaterThanOrEqualTo(160f),
                "Catalogue rows must reserve room above the two-row Surface footer.");
        }

        [Test]
        public void BuildOrUpdateAssets_ProvidesEveryDeclaredFloorFooterActionAsARealButton()
        {
            Phase7SurfaceAssetBuilder.BuildOrUpdateAssets();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Phase7AssetPaths.ActionBarPrefabPath);
            var actionBar = prefab.GetComponentInChildren<DecorationActionBarView>(true);
            var so = new SerializedObject(actionBar);
            var readableProperty = so.FindProperty("useReadableActionLabels");
            Assert.That(readableProperty, Is.Not.Null,
                "Phase 7 needs an explicit readable-label switch so the completed Phase 6 icon prefab stays compatible.");
            Assert.That(readableProperty.boolValue, Is.True);
            foreach (var propertyName in new[]
                     {
                         "undoLastButton", "applyAllButton", "rotateButton", "cancelButton", "confirmButton"
                     })
            {
                var button = so.FindProperty(propertyName).objectReferenceValue as Button;
                Assert.That(button, Is.Not.Null,
                    $"Floor footer declares {propertyName}, so its prefab reference must not be empty.");
                Assert.That(button.GetComponentInChildren<TMPro.TMP_Text>(true), Is.Not.Null,
                    $"{propertyName} must be an ordinary readable text button.");
            }
        }

        [Test]
        public void BuildOrUpdateAssets_ProvidesFurnitureRotateIconWithTextFallbackMetadata()
        {
            Phase7SurfaceAssetBuilder.BuildOrUpdateAssets();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Phase7AssetPaths.ActionBarPrefabPath);
            var actionBar = prefab.GetComponentInChildren<DecorationActionBarView>(true);
            var so = new SerializedObject(actionBar);
            var rotate = so.FindProperty("rotateButton").objectReferenceValue as Button;
            Assert.That(rotate, Is.Not.Null);

            var icon = rotate.transform.Find("Icon")?.GetComponent<Image>();
            Assert.That(icon, Is.Not.Null,
                "Phase 7 compact Furniture actions need a real Rotate icon; the legacy R remains fallback only.");
            Assert.That(icon.sprite, Is.Not.Null);
            Assert.That(icon.raycastTarget, Is.False);
            Assert.That(((RectTransform)icon.transform).sizeDelta, Is.EqualTo(new Vector2(32f, 32f)));
            Assert.That(rotate.transform.Find("Tooltip")?.GetComponentInChildren<TMPro.TMP_Text>(true)?.text,
                Is.EqualTo("Rotate"),
                "The icon button still needs the beginner-readable Rotate semantic label.");
        }

        [Test]
        public void BuildOrUpdateAssets_StretchesEveryActionLabelAcrossItsRoundedButton()
        {
            Phase7SurfaceAssetBuilder.BuildOrUpdateAssets();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Phase7AssetPaths.ActionBarPrefabPath);
            var actionBar = prefab.GetComponentInChildren<DecorationActionBarView>(true);
            var so = new SerializedObject(actionBar);
            foreach (var propertyName in new[]
                     {
                         "storeButton", "undoLastButton", "applyAllButton",
                         "rotateButton", "cancelButton", "confirmButton"
                     })
            {
                var button = (Button)so.FindProperty(propertyName).objectReferenceValue;
                var image = button.image;
                var label = button.transform.Find("Label") as RectTransform;
                Assert.That(image.sprite, Is.Not.Null, propertyName);
                Assert.That(image.type, Is.EqualTo(Image.Type.Sliced), propertyName);
                Assert.That(image.sprite.border.sqrMagnitude, Is.GreaterThan(0f), propertyName);
                Assert.That(label, Is.Not.Null, propertyName);
                Assert.That(label.anchorMin, Is.EqualTo(Vector2.zero), propertyName);
                Assert.That(label.anchorMax, Is.EqualTo(Vector2.one), propertyName);
                Assert.That(label.offsetMin, Is.EqualTo(Vector2.zero), propertyName);
                Assert.That(label.offsetMax, Is.EqualTo(Vector2.zero), propertyName);
            }
        }

        [Test]
        public void BuildOrUpdateAssets_UsesTruncateWithoutRequestingMissingEllipsisGlyph()
        {
            Phase7SurfaceAssetBuilder.BuildOrUpdateAssets();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Phase7AssetPaths.CataloguePrefabPath);
            var catalogue = prefab.GetComponentInChildren<DecorationCatalogueView>(true);
            var so = new SerializedObject(catalogue);
            var tile = (DecorationCatalogueTileView)so.FindProperty("categoryTileTemplate").objectReferenceValue;
            var name = tile.transform.Find("Name").GetComponent<TMPro.TMP_Text>();

            Assert.That(name.overflowMode, Is.EqualTo(TMPro.TextOverflowModes.Truncate),
                "Ellipsis requests U+2026, which is absent from the Phase 6 Noto Sans SC font asset.");
        }

        [Test]
        public void BuildOrUpdateAssets_WallpaperAndWainscotingThumbnailsShowAuthoredTexturePatterns()
        {
            Phase7SurfaceAssetBuilder.BuildOrUpdateAssets();
            var catalogues = new[]
            {
                AssetDatabase.LoadAssetAtPath<SurfaceStyleCatalogueAsset>(Phase7AssetPaths.WallpaperCataloguePath),
                AssetDatabase.LoadAssetAtPath<SurfaceStyleCatalogueAsset>(Phase7AssetPaths.WainscotingCataloguePath)
            };

            foreach (var definition in catalogues.SelectMany(x => x.Entries).Where(x => !x.IsNoneOption))
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    Assert.That(texture.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(definition.Thumbnail))),
                        Is.True, definition.StyleId);
                    Assert.That(texture.GetPixels32().Distinct().Count(), Is.GreaterThan(16),
                        definition.StyleId + " thumbnail must show the authored pattern, not a two-color placeholder swatch.");
                    Assert.That(texture.width, Is.LessThanOrEqualTo(256), definition.StyleId);
                    Assert.That(texture.height, Is.LessThanOrEqualTo(256), definition.StyleId);
                }
                finally
                {
                    Object.DestroyImmediate(texture);
                }
            }
        }

        [Test]
        public void BuildOrUpdateAssets_IntakesFiveFormalMountedAssetsWithoutProductionPlaceholders()
        {
            Phase7SurfaceAssetBuilder.BuildOrUpdateAssets();

            var decor = AssetDatabase.LoadAssetAtPath<WallMountedCatalogueAsset>(
                Phase7AssetPaths.WallMountedProductionCataloguePath);
            var windows = AssetDatabase.LoadAssetAtPath<WallMountedCatalogueAsset>(
                Phase7AssetPaths.WindowCataloguePath);
            Assert.That(decor.Entries.Select(x => x.DefinitionId), Is.EqualTo(new[]
            {
                "wall-decor.monitor.01", "wall-decor.shiba-painting.01", "wall-decor.wood-shelf.01"
            }));
            Assert.That(windows.Entries.Select(x => x.DefinitionId), Is.EqualTo(new[]
            {
                "window.canonical.phase4", "window.tall-glass.1x2.01"
            }));
            Assert.That(decor.Entries.Concat(windows.Entries).All(x =>
                x.Prefab != null && x.Thumbnail != null &&
                !x.Prefab.name.Contains("PLACEHOLDER")), Is.True);

            foreach (var definition in decor.Entries.Concat(windows.Entries))
            {
                var root = definition.Prefab.transform;
                Assert.That(root.localPosition, Is.EqualTo(Vector3.zero), definition.DefinitionId);
                Assert.That(root.localRotation, Is.EqualTo(Quaternion.identity), definition.DefinitionId);
                Assert.That(root.localScale, Is.EqualTo(Vector3.one), definition.DefinitionId);
                Assert.That(definition.Prefab.GetComponentsInChildren<Renderer>(true), Is.Not.Empty);
                var bounds = Encapsulate(definition.Prefab.GetComponentsInChildren<Renderer>(true));
                Assert.That(bounds.min.y, Is.EqualTo(0f).Within(.015f), definition.DefinitionId);
                Assert.That(bounds.min.z, Is.GreaterThanOrEqualTo(-.015f), definition.DefinitionId);
                Assert.That(bounds.size.x, Is.LessThanOrEqualTo(definition.FootprintWidth + .03f), definition.DefinitionId);
                Assert.That(bounds.size.y, Is.LessThanOrEqualTo(definition.FootprintHeight + .03f), definition.DefinitionId);
                Assert.That(bounds.size.z, Is.LessThanOrEqualTo(.35f + .015f), definition.DefinitionId);
                var colliders = definition.Prefab.GetComponentsInChildren<Collider>(true);
                Assert.That(colliders, Is.Not.Empty, definition.DefinitionId);
                Assert.That(colliders.All(x => x.isTrigger), Is.True, definition.DefinitionId);
                Assert.That(definition.Prefab.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true), Is.Empty);
                Assert.That(definition.Prefab.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            }
            var monitorBounds=Encapsulate(decor.Entries.Single(x=>x.DefinitionId=="wall-decor.monitor.01").Prefab.GetComponentsInChildren<Renderer>(true));
            Assert.That(monitorBounds.size.x,Is.GreaterThan(.8f),"Monitor depth limiting must not uniformly shrink its 1x1 front face.");
            Assert.That(monitorBounds.size.y,Is.GreaterThan(.5f),"Monitor authored Z-up front must be wrapper-rotated into wall-local Y-up.");

            StringAssert.DoesNotContain("wall-decor.placeholder",
                File.ReadAllText(Phase7AssetPaths.MainCafeScenePath));
            Assert.That(File.Exists(Phase7AssetPaths.ProvenanceManifestPath), Is.True);
            var manifest = File.ReadAllText(Phase7AssetPaths.ProvenanceManifestPath);
            foreach (var hash in Phase7FormalAssetIntake.SourceSha256.Values)
                StringAssert.Contains(hash, manifest);
            foreach (var hash in Phase7FormalAssetIntake.DerivedSha256.Values)
                StringAssert.Contains(hash, manifest);
        }

        [Test]
        public void BuildOrUpdateAssets_PreservesMeshUvAndPublishesPaintingAndGlassMaterialContracts()
        {
            Phase7SurfaceAssetBuilder.BuildOrUpdateAssets();
            var decor=AssetDatabase.LoadAssetAtPath<WallMountedCatalogueAsset>(Phase7AssetPaths.WallMountedProductionCataloguePath);
            var windows=AssetDatabase.LoadAssetAtPath<WallMountedCatalogueAsset>(Phase7AssetPaths.WindowCataloguePath);
            foreach(var definition in decor.Entries.Concat(windows.Entries))
            {
                var filters=definition.Prefab.GetComponentsInChildren<MeshFilter>(true);
                Assert.That(filters,Is.Not.Empty,definition.DefinitionId);
                Assert.That(filters.All(x=>x.sharedMesh!=null&&x.sharedMesh.vertexCount>0),Is.True,definition.DefinitionId+" must retain meshes");
                Assert.That(filters.Any(x=>x.sharedMesh.uv.Length>0),Is.True,definition.DefinitionId+" must retain at least one authored UV stream");
                Assert.That(definition.Prefab.GetComponentsInChildren<Renderer>(true).SelectMany(x=>x.sharedMaterials).All(x=>x!=null),Is.True,definition.DefinitionId);
            }
            var painting=decor.Entries.Single(x=>x.DefinitionId=="wall-decor.shiba-painting.01");
            var portrait=AssetDatabase.LoadAssetAtPath<Texture2D>(Phase7AssetPaths.TextureFolder+"/T_WallDecor_ShibaPortrait_v01.png");
            Assert.That(painting.Prefab.GetComponentsInChildren<Renderer>(true).SelectMany(x=>x.sharedMaterials)
                .Any(x=>x.GetTexture("_BaseMap")==portrait),Is.True,"Painting must bind the supplied portrait texture.");
            var portraitMaterial=painting.Prefab.GetComponentsInChildren<Renderer>(true).SelectMany(x=>x.sharedMaterials).Single(x=>x!=null&&x.name.Contains("ShibaPortrait"));
            Assert.That(portraitMaterial.GetFloat("_Surface"),Is.EqualTo(1f));
            Assert.That(portraitMaterial.GetFloat("_ZWrite"),Is.EqualTo(0f));
            Assert.That(portraitMaterial.renderQueue,Is.GreaterThanOrEqualTo(3000));
            Assert.That(portraitMaterial.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"),Is.True);
            foreach(var window in windows.Entries)
                Assert.That(window.Prefab.GetComponentsInChildren<Renderer>(true).SelectMany(x=>x.sharedMaterials)
                    .Any(x=>x!=null&&x.name.Contains("Glass")&&x.renderQueue>=3000),Is.True,window.DefinitionId+" needs explicit transparent glass material.");
        }

        [Test]
        public void FormalRebuild_UsesOnlyPortableRepositoryAuthorityInputs()
        {
            var inputs=Phase7FormalAssetIntake.RepositoryAuthorityPaths;
            Assert.That(inputs,Is.Not.Empty);
            Assert.That(inputs.All(path=>path.StartsWith("Assets/",System.StringComparison.Ordinal)||path.StartsWith("ArtSource/",System.StringComparison.Ordinal)),Is.True);
            Assert.That(inputs.All(File.Exists),Is.True);
            Assert.That(inputs.Any(path=>path.StartsWith(@"E:\",System.StringComparison.OrdinalIgnoreCase)),Is.False);
            Assert.DoesNotThrow(()=>Phase7FormalAssetIntake.Build());
        }

        private static Bounds Encapsulate(Renderer[] renderers)
        {
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static void AssertAuthoredOnlyWainsNormal(string materialPath, string normalPath)
        {
            const int normalSize = 128;
            const float slopeStrength = 7.5f;
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            Assert.That(material, Is.Not.Null, materialPath);
            var sourcePath = AssetDatabase.GetAssetPath(material.GetTexture("_BaseMap"));
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var actual = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(source.LoadImage(File.ReadAllBytes(sourcePath)), Is.True, sourcePath);
                Assert.That(actual.LoadImage(File.ReadAllBytes(normalPath)), Is.True, normalPath);
                Assert.That(actual.width, Is.EqualTo(normalSize), normalPath);
                Assert.That(actual.height, Is.EqualTo(normalSize), normalPath);
                var actualPixels = actual.GetPixels32();
                var maximumChannelError = 0;
                long totalChannelError = 0;
                var sampledChannels = 0;

                for (var y = 0; y < normalSize; y += 7)
                for (var x = 0; x < normalSize; x += 7)
                {
                    var left = SampleAuthoredHeight(source, (x + normalSize - 1) % normalSize, y, normalSize);
                    var right = SampleAuthoredHeight(source, (x + 1) % normalSize, y, normalSize);
                    var down = SampleAuthoredHeight(source, x, (y + normalSize - 1) % normalSize, normalSize);
                    var up = SampleAuthoredHeight(source, x, (y + 1) % normalSize, normalSize);
                    var normal = new Vector3(
                        (left - right) * slopeStrength,
                        (down - up) * slopeStrength,
                        1f).normalized;
                    var expected = (Color32)new Color(
                        normal.x * .5f + .5f,
                        normal.y * .5f + .5f,
                        normal.z * .5f + .5f,
                        1f);
                    var observed = actualPixels[y * normalSize + x];
                    foreach (var error in new[]
                             {
                                 System.Math.Abs(observed.r - expected.r),
                                 System.Math.Abs(observed.g - expected.g),
                                 System.Math.Abs(observed.b - expected.b)
                             })
                    {
                        maximumChannelError = System.Math.Max(maximumChannelError, error);
                        totalChannelError += error;
                        sampledChannels++;
                    }
                }

                Assert.That(maximumChannelError, Is.LessThanOrEqualTo(2),
                    normalPath + " must be derived from authored luminance only, without procedural crosshatch.");
                Assert.That(totalChannelError / (double)sampledChannels, Is.LessThanOrEqualTo(.5d),
                    normalPath + " authored-gradient mean error.");
            }
            finally
            {
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(actual);
            }
        }

        private static float SampleAuthoredHeight(Texture2D source, int x, int y, int size)
        {
            return source.GetPixelBilinear((x + .5f) / size, (y + .5f) / size).grayscale;
        }
        private static string Fingerprint(params string[] roots)
        {
            using var sha=SHA256.Create();
            return string.Join("\n",roots.SelectMany(root=>Directory.GetFiles(root,"*",SearchOption.AllDirectories))
                .OrderBy(path=>path,System.StringComparer.Ordinal)
                .Select(path=>path+"="+System.BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path)))));
        }

        private static void AssertPrefabGraph<T>(string path) where T : Component
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab,Is.Not.Null,path);
            Assert.That(prefab.GetComponentInChildren<T>(true),Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<UnityEngine.Camera>(true),Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<Component>(true)
                .Any(component=>component!=null&&component.GetType().Name.Contains("RenderTexture")),Is.False);
        }

        private static void AssertCatalogue(string path, SurfaceStyleKind kind, int count)
        {
            var catalogue = AssetDatabase.LoadAssetAtPath<SurfaceStyleCatalogueAsset>(path);
            Assert.That(catalogue, Is.Not.Null, path);
            Assert.That(catalogue.Kind, Is.EqualTo(kind));
            Assert.That(catalogue.Entries.Count, Is.EqualTo(count));
            Assert.That(catalogue.Entries.All(entry => entry != null && entry.Thumbnail != null), Is.True);
            var ids = catalogue.Entries.Select(entry => entry.StyleId).ToArray();
            Assert.That(ids, Is.EqualTo(ids.OrderBy(id => id, System.StringComparer.Ordinal).ToArray()));
        }

        private static void AssertKindMetadata(string path,string mapping,float width,float height)
        {
            var catalogue=AssetDatabase.LoadAssetAtPath<SurfaceStyleCatalogueAsset>(path);
            foreach(var entry in catalogue.Entries)
            {
                var so=new SerializedObject(entry);var property=so.FindProperty("verticalMapping");
                Assert.That(property,Is.Not.Null,entry.StyleId+" vertical mapping metadata");
                Assert.That(property.enumNames[property.enumValueIndex],Is.EqualTo(mapping),entry.StyleId);
                Assert.That(so.FindProperty("worldTileWidthMeters").floatValue,Is.EqualTo(width).Within(.001f),entry.StyleId);
                Assert.That(so.FindProperty("worldTileHeightMeters").floatValue,Is.EqualTo(height).Within(.001f),entry.StyleId);
            }
        }
    }
}
