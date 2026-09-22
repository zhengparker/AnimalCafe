using System;
using System.Linq;
using System.Reflection;
using AnimalCafe.UI.P8R;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Object = UnityEngine.Object;

namespace AnimalCafe.Tests.EditMode.P8R
{
    public sealed class P8RRefinedBStyleTests
    {
        private const string Root = "Assets/UI/P8R/RefinedB/";
        private static Texture2D Render(string key)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("AnimalCafe.EditorTools.P8R.P8RRefinedBAssets")).FirstOrDefault(t => t != null);
            Assert.That(type, Is.Not.Null, "B requires a shared geometric master, not independent hand-adjusted skins.");
            return (Texture2D)type.GetMethod("Render", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { key });
        }

        [TestCase("panel_cream")]
        [TestCase("button_secondary_normal")]
        [TestCase("tab_idle")]
        public void RefinedB_CreamSurfaceHasCleanCornersAndRestrainedTexture(string key)
        {
            var texture = Render(key);
            try
            {
                var center = texture.GetPixel(192, 192);
                Assert.That(center.r, Is.EqualTo(249f / 255f).Within(.025f));
                Assert.That(center.g, Is.EqualTo(243f / 255f).Within(.025f));
                Assert.That(center.b, Is.EqualTo(232f / 255f).Within(.025f));
                Assert.That(center.a, Is.EqualTo(1));
                Assert.That(texture.GetPixel(0, 0).a, Is.Zero);
                Assert.That(texture.GetPixel(0, 383).a, Is.Zero);
                var samples = Enumerable.Range(100, 150).Select(x => texture.GetPixel(x, 192).r).ToArray();
                Assert.That(samples.Max() - samples.Min(), Is.LessThan(.025f), "Paper texture must not become a visible grain band.");
                // At the straight left edge there is one continuous dark stroke, not a second lower lip.
                var dark = Enumerable.Range(0, 96).Where(x => texture.GetPixel(x, 192).a > .8f && texture.GetPixel(x, 192).r < .65f).ToArray();
                Assert.That(dark.Length, Is.InRange(9, 10), "One strengthened B stroke at 4x source density.");
                Assert.That(dark.Last() - dark.First() + 1, Is.EqualTo(dark.Length));
            }
            finally { Object.DestroyImmediate(texture); }
        }

        [Test]
        public void RefinedB_PressedRetainsFaceGeometryAndDisabledRemainsDistinct()
        {
            var normal = Render("button_primary_normal"); var pressed = Render("button_primary_pressed");
            var disabled = Render("button_disabled");
            try
            {
                Assert.That(normal.GetPixels32().Select(p => p.a), Is.EqualTo(pressed.GetPixels32().Select(p => p.a)),
                    "Pressed must not move or resize the face relative to its icon.");
                var n = normal.GetPixel(192, 192); var p = pressed.GetPixel(192, 192); var d = disabled.GetPixel(192, 192);
                Assert.That(n.r - n.b, Is.GreaterThan(.2f), "Primary is peach.");
                Assert.That(p.g, Is.LessThan(n.g), "Pressed is visibly deeper, with no second lip.");
                Assert.That(d.r - d.b, Is.LessThan(.13f), "Disabled must not resemble the peach selection.");
            }
            finally { Object.DestroyImmediate(normal); Object.DestroyImmediate(pressed); Object.DestroyImmediate(disabled); }
        }

        [Test]
        public void RefinedB_CatalogueCardUsesWarmOatmealOutsideAndKeepsInsetWellUnchanged()
        {
            var card = Render("card_base");
            var well = Render("card_well");
            try
            {
                var outer = card.GetPixel(192, 192);
                Assert.That(outer.r, Is.EqualTo(232f / 255f).Within(.012f));
                Assert.That(outer.g, Is.EqualTo(216f / 255f).Within(.012f));
                Assert.That(outer.b, Is.EqualTo(191f / 255f).Within(.012f),
                    "Every catalogue card needs the approved #E8D8BF warm-oatmeal outer surface.");

                var inset = well.GetPixel(192, 192);
                Assert.That(inset.r, Is.EqualTo(239f / 255f).Within(.012f));
                Assert.That(inset.g, Is.EqualTo(232f / 255f).Within(.012f));
                Assert.That(inset.b, Is.EqualTo(218f / 255f).Within(.012f),
                    "The existing card_well surface is intentionally outside this palette change.");
            }
            finally { Object.DestroyImmediate(card); Object.DestroyImmediate(well); }
        }

        [Test]
        public void RefinedB_ImportedFramesAndRepeatedBakeStayStable()
        {
            var paths = AnimalCafe.EditorTools.P8R.P8RRefinedBAssets.Keys.Select(key => Root + key + ".png").ToArray();
            foreach (var path in paths)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear), path);
                Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
                Assert.That(importer.mipmapEnabled, Is.False);
                Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None));
                Assert.That(importer.spriteBorder, Is.EqualTo(Vector4.one * 96));
                Assert.That(AssetDatabase.LoadAssetAtPath<Sprite>(path).rect.size, Is.EqualTo(new Vector2(384, 384)));
                foreach (var platform in new[] { "Standalone", "Android", "iPhone" })
                    Assert.That(importer.GetPlatformTextureSettings(platform).overridden, Is.False, platform);
            }
            var files = paths.SelectMany(path => new[] { path, path + ".meta" }).ToArray();
            var bytes = files.ToDictionary(path => path, System.IO.File.ReadAllBytes);
            var times = files.ToDictionary(path => path, System.IO.File.GetLastWriteTimeUtc);
            AnimalCafe.EditorTools.P8R.P8RRefinedBAssets.BuildApproved();
            foreach (var path in paths) Assert.That(EditorUtility.IsDirty(AssetImporter.GetAtPath(path)), Is.False, "No-op bake dirties " + path);
            foreach (var path in files)
            {
                Assert.That(System.IO.File.ReadAllBytes(path), Is.EqualTo(bytes[path]), path);
                Assert.That(System.IO.File.GetLastWriteTimeUtc(path), Is.EqualTo(times[path]), "No-op bake rewrites " + path);
            }
        }

        [Test]
        public void RefinedB_PanelsAndNoticesHaveAReadableTwoAndHalfUnitOutline()
        {
            foreach (var key in new[] { "panel_cream", "notice_preview", "notice_error" })
            {
                var texture = Render(key);
                try
                {
                    var stroke = Enumerable.Range(0, 96).Count(x => texture.GetPixel(x, 192).a > .8f && texture.GetPixel(x, 192).r < .7f);
                    Assert.That(stroke, Is.EqualTo(10), key + " needs a consistent 2.5 logical outline.");
                }
                finally { Object.DestroyImmediate(texture); }
            }
        }

        [Test]
        public void RefinedB_FixedPickupUsesPrimaryHierarchyWithoutChangingItsLabel()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UI/P8R/Prefabs/PF_UI_P8RCatalogue.prefab");
            var button = root.GetComponentsInChildren<Button>(true).Single(b => b.name == "PickUpPointButton");
            Assert.That(AssetDatabase.GetAssetPath(button.image.sprite), Is.EqualTo(Root + "button_primary_normal.png"));
            Assert.That(AssetDatabase.GetAssetPath(button.spriteState.pressedSprite), Is.EqualTo(Root + "button_primary_pressed.png"));
            var label = button.GetComponentInChildren<TMP_Text>(true);
            Assert.That(label.fontStyle.HasFlag(FontStyles.Bold), Is.True);
            Assert.That(label.text, Is.EqualTo("Pickup Point"));
        }

        [Test]
        public void RefinedB_ActualCatalogueTabsKeepSelectedEmphasisAcrossAllModes()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UI/P8R/Prefabs/PF_UI_P8RCatalogue.prefab"));
            try
            {
                var tabs = root.GetComponentInChildren<AnimalCafe.UI.Decoration.DecorationModeTabsView>(true);
                var fields = new[] { "furnitureButton", "floorButton", "wallButton", "wallDecorButton" };
                var modes = new[] { AnimalCafe.Decoration.DecorationModeKind.Furniture, AnimalCafe.Decoration.DecorationModeKind.Floor,
                    AnimalCafe.Decoration.DecorationModeKind.Wall, AnimalCafe.Decoration.DecorationModeKind.WallDecor };
                for (var selected = 0; selected < fields.Length; selected++)
                {
                    tabs.SetActive(modes[selected]);
                    for (var i = 0; i < fields.Length; i++)
                    {
                        var button = (Button)tabs.GetType().GetField(fields[i], BindingFlags.NonPublic | BindingFlags.Instance).GetValue(tabs);
                        var label = button.GetComponentInChildren<TMP_Text>(true);
                        Assert.That(label.fontStyle.HasFlag(FontStyles.Bold), Is.EqualTo(i == selected), fields[i] + " in " + modes[selected]);
                    }
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void RefinedB_TabEmphasisResetsWhenSelectionChanges()
        {
            var appearance = AssetDatabase.LoadAssetAtPath<P8RAppearance>("Assets/UI/P8R/P8RAppearance.asset");
            var go = new GameObject("B tab", typeof(RectTransform), typeof(Image), typeof(Button));
            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(go.transform, false);
            try
            {
                var label = labelObject.GetComponent<TMP_Text>(); var button = go.GetComponent<Button>();
                appearance.Tab(button, "furniture", true);
                Assert.That(label.fontStyle.HasFlag(FontStyles.Bold), Is.True, "Selected tab needs B's stronger hierarchy.");
                appearance.Tab(button, "furniture", false);
                Assert.That(label.fontStyle.HasFlag(FontStyles.Bold), Is.False, "Deselection must not retain stale bold emphasis.");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void RefinedB_AppearanceAndButtonStatesUseTheSameNewPack()
        {
            var appearance = AssetDatabase.LoadAssetAtPath<P8RAppearance>("Assets/UI/P8R/P8RAppearance.asset");
            foreach (var key in new[] { "panel_cream", "panel_light", "panel_inset", "card_base", "card_well", "card_preview_outline",
                "tab_idle", "tab_selected", "tab_unavailable", "notice_info", "notice_success", "notice_warning", "notice_error", "notice_preview" })
                Assert.That(AssetDatabase.GetAssetPath(appearance.Sprite(key)), Does.StartWith(Root), key + " must not keep an old textured frame.");
            var go = new GameObject("B button", typeof(RectTransform), typeof(Image), typeof(Button));
            try
            {
                var button = go.GetComponent<Button>();
                foreach (var role in new[] { "primary", "secondary", "destructive" })
                {
                    appearance.Button(button, "confirm", role, iconOnly: true);
                    foreach (var sprite in new[] { button.image.sprite, button.spriteState.highlightedSprite, button.spriteState.pressedSprite,
                        button.spriteState.selectedSprite, button.spriteState.disabledSprite })
                        Assert.That(AssetDatabase.GetAssetPath(sprite), Does.StartWith(Root), role + " state must not fall back to old artwork.");
                }
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
