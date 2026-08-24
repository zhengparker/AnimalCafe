using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using AnimalCafe.Content;
using AnimalCafe.Decoration;
using AnimalCafe.EditorTools.Phase5;
using AnimalCafe.EditorTools.Phase6;
using AnimalCafe.Layout;
using AnimalCafe.UI.Decoration;
using AnimalCafe.UI.Foundation;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace AnimalCafe.Tests.EditMode.Phase6
{
    public sealed class Phase6DecorationAssetBuilderTests
    {
        [Test]
        public void BuildAll_CreatesExactMultiCellPrefabCompositionAndDefinitionContracts()
        {
            Phase6DecorationAssetBuilder.BuildAll();

            AssertPreset(Phase6DecorationAssetPaths.Counter1x2DefinitionPath,
                Phase6DecorationAssetPaths.Counter1x2PrefabPath, 1, 2, 2);
            AssertPreset(Phase6DecorationAssetPaths.Counter1x3DefinitionPath,
                Phase6DecorationAssetPaths.Counter1x3PrefabPath, 1, 3, 3);
            AssertPreset(Phase6DecorationAssetPaths.Counter2x3DefinitionPath,
                Phase6DecorationAssetPaths.Counter2x3PrefabPath, 2, 3, 6);
        }

        [Test]
        public void BuildAll_ReusesPhase4CounterWithoutChangingItsGuidOrBytes()
        {
            var definitionGuid = AssetDatabase.AssetPathToGUID(
                Phase6DecorationAssetPaths.Counter1x1DefinitionPath);
            var prefabGuid = AssetDatabase.AssetPathToGUID(
                Phase6DecorationAssetPaths.Counter1x1PrefabPath);
            var definitionHash = HashAsset(Phase6DecorationAssetPaths.Counter1x1DefinitionPath);
            var prefabHash = HashAsset(Phase6DecorationAssetPaths.Counter1x1PrefabPath);

            Phase6DecorationAssetBuilder.BuildAll();

            Assert.That(AssetDatabase.AssetPathToGUID(
                Phase6DecorationAssetPaths.Counter1x1DefinitionPath), Is.EqualTo(definitionGuid));
            Assert.That(AssetDatabase.AssetPathToGUID(
                Phase6DecorationAssetPaths.Counter1x1PrefabPath), Is.EqualTo(prefabGuid));
            Assert.That(HashAsset(Phase6DecorationAssetPaths.Counter1x1DefinitionPath),
                Is.EqualTo(definitionHash));
            Assert.That(HashAsset(Phase6DecorationAssetPaths.Counter1x1PrefabPath),
                Is.EqualTo(prefabHash));
        }

        [Test]
        public void BuildAll_CreatesDeterministicSpriteThumbnailsWithApprovedImportSettings()
        {
            Phase6DecorationAssetBuilder.BuildAll();
            var firstHashes = Phase6DecorationAssetPaths.ThumbnailPaths
                .Select(HashAsset).ToArray();

            Phase6DecorationAssetBuilder.BuildAll();
            var secondHashes = Phase6DecorationAssetPaths.ThumbnailPaths
                .Select(HashAsset).ToArray();

            Assert.That(secondHashes, Is.EqualTo(firstHashes));
            Assert.That(secondHashes.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(4),
                "Different Counter footprints must not produce the same blank thumbnail bytes.");
            foreach (var path in Phase6DecorationAssetPaths.ThumbnailPaths)
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                Assert.That(importer, Is.Not.Null, path);
                Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite), path);
                Assert.That(importer.alphaIsTransparency, Is.True, path);
                Assert.That(importer.mipmapEnabled, Is.False, path);
                Assert.That(texture.width, Is.EqualTo(Phase6DecorationAssetBuilder.ThumbnailSize), path);
                Assert.That(texture.height, Is.EqualTo(Phase6DecorationAssetBuilder.ThumbnailSize), path);
                Assert.That(sprite, Is.Not.Null, path);
                AssertThumbnailHasVisiblePixels(path);
            }
        }

        [Test]
        public void BuildAll_TwicePreservesEveryGeneratedGuidAndCatalogueCounts()
        {
            Phase6DecorationAssetBuilder.BuildAll();
            var firstGuids = CaptureGuids();
            var firstDecorationCount = LoadDecorationCatalogue().Entries.Count;
            var firstProductionCount = LoadProductionCatalogue().BuildRuntimeCatalog()
                .Definitions.Count;

            Phase6DecorationAssetBuilder.BuildAll();
            var secondGuids = CaptureGuids();

            Assert.That(secondGuids, Is.EqualTo(firstGuids));
            Assert.That(LoadDecorationCatalogue().Entries.Count, Is.EqualTo(firstDecorationCount));
            Assert.That(firstDecorationCount, Is.EqualTo(4));
            Assert.That(LoadProductionCatalogue().BuildRuntimeCatalog().Definitions.Count,
                Is.EqualTo(firstProductionCount));
            Assert.That(firstProductionCount, Is.EqualTo(7));
        }

        [Test]
        public void BuildAll_UiSuccessTwicePreservesBytesGuidsSubassetOrderAndViewLocalIds()
        {
            Phase6DecorationAssetBuilder.BuildAll();
            var first = CaptureUiSnapshot();

            Phase6DecorationAssetBuilder.BuildAll();
            var second = CaptureUiSnapshot();

            AssertUiSnapshot(second, first);
        }

        [Test]
        public void BuildAll_FirstRepairAfterFreshImportPreservesCanonicalFontBytesAndLookupFlags()
        {
            var fontPath = Phase6DecorationAssetPaths.DecorationUiFontPath;
            AssetDatabase.ImportAsset(fontPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var importedFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
            var beforeFlags = CaptureFeatureLookupFlags(importedFont);
            Assert.That(beforeFlags, Has.Count.EqualTo(2517),
                "The real Task 6 font must exercise every current lookup flag.");
            var beforeBytes = File.ReadAllBytes(Path.GetFullPath(fontPath));

            var actionRoot = PrefabUtility.LoadPrefabContents(
                Phase6DecorationAssetPaths.DecorationActionBarPrefabPath);
            try
            {
                actionRoot.transform.Find("ActionPanel").GetComponent<Image>().material = null;
                PrefabUtility.SaveAsPrefabAsset(actionRoot,
                    Phase6DecorationAssetPaths.DecorationActionBarPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(actionRoot);
            }

            try
            {
                Phase6DecorationAssetBuilder.BuildAll();
                AssetDatabase.ImportAsset(fontPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

                var afterFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
                var afterFlags = CaptureFeatureLookupFlags(afterFont);
                var afterBytes = File.ReadAllBytes(Path.GetFullPath(fontPath));
                AssertFeatureLookupFlagsEqual(afterFlags, beforeFlags,
                    "The first BuildAll after a fresh import must not mutate lookup flags.");
                Assert.That(afterBytes, Is.EqualTo(beforeBytes),
                    "The first BuildAll after a fresh import must preserve canonical font bytes.");
            }
            finally
            {
                Phase6DecorationAssetBuilder.BuildAll();
            }
        }

        [Test]
        public void PublishedUiFont_UsesOnlyCanonicalNoneLookupFlags()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                Phase6DecorationAssetPaths.DecorationUiFontPath);
            Assert.That(font, Is.Not.Null);
            var flags = CaptureFeatureLookupFlags(font);
            Assert.That(flags, Has.Count.EqualTo(2517),
                "The published font contract must cover every current kerning record.");

            var enumType = typeof(UnityEngine.TextCore.LowLevel.FontFeatureLookupFlags);
            var validMask = Enum.GetValues(enumType).Cast<object>()
                .Aggregate(0L, (mask, value) => mask | Convert.ToInt64(value));
            Assert.That(validMask, Is.EqualTo(260));
            foreach (var item in flags)
            {
                var value = (long)item.Value;
                Assert.That(value & ~validMask, Is.Zero,
                    $"Published font lookup flags contain undefined bits at {item.Key}.");
                Assert.That(value,
                    Is.EqualTo((long)UnityEngine.TextCore.LowLevel.FontFeatureLookupFlags.None),
                    $"Phase 6 generated kerning uses the canonical None contract at {item.Key}.");
            }
        }

        [Test]
        public void BuildAll_UiCandidateFailureTouchesNoLiveTask6UiAsset()
        {
            Phase6DecorationAssetBuilder.BuildAll();
            var before = CaptureUiSnapshot();
            try
            {
                SetPrivateStaticBuilderField("UiCandidateValidationFaultForTests", true);
                Assert.That(
                    () => Phase6DecorationAssetBuilder.BuildAll(),
                    Throws.TypeOf<InvalidOperationException>()
                        .With.Message.Contains("candidate"));
                AssertUiSnapshot(CaptureUiSnapshot(), before);
            }
            finally
            {
                SetPrivateStaticBuilderField("UiCandidateValidationFaultForTests", false);
            }
        }

        [Test]
        public void BuildAll_UiCandidateFailureDoesNotFlushUnrelatedDirtyAssets()
        {
            Phase6DecorationAssetBuilder.BuildAll();
            var protectedPaths = new[]
            {
                Phase5UiAssetPaths.ThemePath,
                Phase6DecorationAssetPaths.DecorationUiFontPath
            };
            var expectedBytes = CaptureBytesAndMetas(protectedPaths);
            var theme = AssetDatabase.LoadAssetAtPath<AnimalCafeUiTheme>(
                Phase5UiAssetPaths.ThemePath);
            var originalColors = theme.Colors;
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                Phase6DecorationAssetPaths.DecorationUiFontPath);
            var originalFontName = font.name;
            try
            {
                var colors = theme.Colors;
                colors.Warning = Color.magenta;
                theme.Colors = colors;
                EditorUtility.SetDirty(theme);

                font.name = "Unsaved Task6 UI Font Drift";
                EditorUtility.SetDirty(font);
                Assert.That(EditorUtility.IsDirty(theme), Is.True);
                Assert.That(EditorUtility.IsDirty(font), Is.True);

                SetPrivateStaticBuilderField("UiCandidateValidationFaultForTests", true);
                Assert.That(() => Phase6DecorationAssetBuilder.BuildAll(),
                    Throws.TypeOf<InvalidOperationException>().With.Message.Contains("candidate"));
                AssertBytesAndMetas(CaptureBytesAndMetas(protectedPaths), expectedBytes);
            }
            finally
            {
                var cleanupErrors = new List<string>();
                try
                {
                    SetPrivateStaticBuilderField("UiCandidateValidationFaultForTests", false);
                }
                catch (Exception exception)
                {
                    cleanupErrors.Add("Fault reset: " + exception.Message);
                }

                try
                {
                    theme.Colors = originalColors;
                    EditorUtility.SetDirty(theme);
                    AssetDatabase.SaveAssetIfDirty(theme);
                }
                catch (Exception exception)
                {
                    cleanupErrors.Add("Theme state restore: " + exception.Message);
                }

                try
                {
                    font.name = originalFontName;
                    EditorUtility.SetDirty(font);
                    AssetDatabase.SaveAssetIfDirty(font);
                }
                catch (Exception exception)
                {
                    cleanupErrors.Add("Task 6 font state restore: " + exception.Message);
                }

                try
                {
                    RestoreBytesAndMetasWithTargetedImport(expectedBytes);
                }
                catch (Exception exception)
                {
                    cleanupErrors.Add("Byte restore: " + exception.Message);
                }

                Assert.That(cleanupErrors, Is.Empty, string.Join("\n", cleanupErrors));
            }

            Assert.That(EditorUtility.IsDirty(theme), Is.False,
                "The protected Theme must not leak dirty state into later tests.");
            Assert.That(EditorUtility.IsDirty(font), Is.False,
                "The Task 6 UI font must not leak dirty state into later tests.");
            AssertBytesAndMetas(CaptureBytesAndMetas(protectedPaths), expectedBytes);
        }

        [Test]
        public void BuildAll_UiPublishFaultAfterFirstWriteRollsBackEveryByteMetaAndIdentity()
        {
            Phase6DecorationAssetBuilder.BuildAll();
            var before = CaptureUiSnapshot();
            try
            {
                SetPrivateStaticBuilderField("UiPublishFaultAfterWriteForTests", 1);
                Assert.That(
                    () => Phase6DecorationAssetBuilder.BuildAll(),
                    Throws.TypeOf<InvalidOperationException>()
                        .With.Message.Contains("publish fault"));
                AssertUiSnapshot(CaptureUiSnapshot(), before);
            }
            finally
            {
                SetPrivateStaticBuilderField("UiPublishFaultAfterWriteForTests", -1);
            }
        }

        [Test]
        public void BuildAll_UiPostPublishValidationFaultRollsBackEveryByteMetaAndIdentity()
        {
            Phase6DecorationAssetBuilder.BuildAll();
            var before = CaptureUiSnapshot();
            try
            {
                SetPrivateStaticBuilderField("UiPostPublishValidationFaultForTests", true);
                Assert.That(
                    () => Phase6DecorationAssetBuilder.BuildAll(),
                    Throws.TypeOf<InvalidOperationException>()
                        .With.Message.Contains("post-publish"));
                AssertUiSnapshot(CaptureUiSnapshot(), before);
            }
            finally
            {
                SetPrivateStaticBuilderField("UiPostPublishValidationFaultForTests", false);
            }
        }

        [Test]
        public void BuildAll_UiCandidateFailureOnAbsentTargetsLeavesNoTask6FontOrPrefabFolders()
        {
            WithTemporarilyAbsentUiFolders(() =>
            {
                try
                {
                    SetPrivateStaticBuilderField("UiCandidateValidationFaultForTests", true);
                    Assert.That(() => Phase6DecorationAssetBuilder.BuildAll(),
                        Throws.TypeOf<InvalidOperationException>());
                    AssertUiTargetFoldersAbsent();
                }
                finally
                {
                    SetPrivateStaticBuilderField("UiCandidateValidationFaultForTests", false);
                }
            });
        }

        [Test]
        public void BuildAll_FirstPublishFaultLeavesNoTask6UiTargetOrFolderMetaResidue()
        {
            WithTemporarilyAbsentUiFolders(() =>
            {
                try
                {
                    SetPrivateStaticBuilderField("UiPublishFaultAfterWriteForTests", 1);
                    Assert.That(() => Phase6DecorationAssetBuilder.BuildAll(),
                        Throws.TypeOf<InvalidOperationException>()
                            .With.Message.Contains("publish fault"));
                    AssertUiTargetFoldersAbsent();
                    Assert.That(File.Exists(Path.GetFullPath(
                        Phase6DecorationAssetPaths.UiFontFolderPath + ".meta")), Is.False);
                    Assert.That(File.Exists(Path.GetFullPath(
                        Phase6DecorationAssetPaths.UiPrefabFolderPath + ".meta")), Is.False);
                }
                finally
                {
                    SetPrivateStaticBuilderField("UiPublishFaultAfterWriteForTests", -1);
                }
            });
        }

        [Test]
        public void BuildAll_RepairsValidUiDriftInPlaceAndRestoresDeterministicSnapshot()
        {
            Phase6DecorationAssetBuilder.BuildAll();
            var expected = CaptureUiSnapshot();
            var restorationBytes = expected.BytesAndMetas.ToDictionary(
                item => item.Key,
                item => item.Value.ToArray(),
                StringComparer.Ordinal);
            try
            {
                var actionRoot = PrefabUtility.LoadPrefabContents(
                    Phase6DecorationAssetPaths.DecorationActionBarPrefabPath);
                try
                {
                    actionRoot.transform.Find("FeedbackToast/Message")
                        .GetComponent<TMP_Text>().text = "Valid drift";
                    PrefabUtility.SaveAsPrefabAsset(actionRoot,
                        Phase6DecorationAssetPaths.DecorationActionBarPrefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(actionRoot);
                }

                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    Phase6DecorationAssetPaths.DecorationUiFontPath);
                var material = font.material;
                font.name = "Valid Drift Font";
                material.color = Color.magenta;
                var atlas = AssetDatabase.LoadAllAssetsAtPath(
                    Phase6DecorationAssetPaths.DecorationUiFontPath)
                    .OfType<Texture2D>().Single();
                atlas.SetPixel(0, 0, Color.magenta);
                atlas.Apply(updateMipmaps: false, makeNoLongerReadable: false);
                EditorUtility.SetDirty(font);
                EditorUtility.SetDirty(material);
                EditorUtility.SetDirty(atlas);
                AssetDatabase.SaveAssetIfDirty(font);
                AssetDatabase.SaveAssetIfDirty(material);
                AssetDatabase.SaveAssetIfDirty(atlas);

                Phase6DecorationAssetBuilder.BuildAll();
                AssertUiSnapshot(CaptureUiSnapshot(), expected);
            }
            finally
            {
                RestoreBytesAndMetasWithoutBuilder(restorationBytes);
            }
        }

        [Test]
        public void BuildAll_RemovesStalePersistentButtonCallbacksWithoutChangingLocalIds()
        {
            Phase6DecorationAssetBuilder.BuildAll();
            var path = Phase6DecorationAssetPaths.DecorationActionBarPrefabPath;
            var expectedBytes = CaptureBytesAndMetas(new[] { path });
            var canonical = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var expectedViewIdentity = CaptureObjectIdentity(
                canonical.GetComponent<DecorationActionBarView>());
            var expectedButtonIdentity = CaptureObjectIdentity(canonical.transform
                .Find("ActionPanel/ConfirmButton").GetComponent<Button>());
            try
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var confirm = root.transform.Find("ActionPanel/ConfirmButton")
                        .GetComponent<Button>();
                    UnityEventTools.AddPersistentListener(confirm.onClick, confirm.Select);
                    EditorUtility.SetDirty(confirm);
                    Assert.That(PrefabUtility.SaveAsPrefabAsset(root, path), Is.Not.Null);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }

                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(path).transform
                    .Find("ActionPanel/ConfirmButton").GetComponent<Button>()
                    .onClick.GetPersistentEventCount(), Is.EqualTo(1));

                Phase6DecorationAssetBuilder.BuildAll();
                var repaired = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(repaired.transform.Find("ActionPanel/ConfirmButton")
                    .GetComponent<Button>().onClick.GetPersistentEventCount(), Is.Zero);
                Assert.That(CaptureObjectIdentity(repaired.GetComponent<DecorationActionBarView>()),
                    Is.EqualTo(expectedViewIdentity));
                Assert.That(CaptureObjectIdentity(repaired.transform
                        .Find("ActionPanel/ConfirmButton").GetComponent<Button>()),
                    Is.EqualTo(expectedButtonIdentity));
            }
            finally
            {
                RestoreBytesAndMetasWithTargetedImport(expectedBytes);
            }
        }

        [Test]
        public void ValidatePublishedUiSet_RejectsMissingScriptOnNestedChild()
        {
            var path = Phase6DecorationAssetPaths.DecorationActionBarPrefabPath;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                root.SetActive(true);
                var message = root.transform.Find("FeedbackToast/Message").gameObject;
                message.SetActive(false);
                Assert.That(root.transform.Find("ActionPanel/ConfirmButton").gameObject.activeSelf,
                    Is.True, "The fixture must contain active and inactive nested children.");
                var hook = message.AddComponent<DecorationPointerBoundaryEventHook>();
                var serialized = new SerializedObject(hook);
                var script = serialized.FindProperty("m_Script");
                Assert.That(script, Is.Not.Null,
                    "Unity must expose m_Script for the legal in-memory fixture.");
                script.objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(message),
                    Is.EqualTo(1), "Unity did not recognize the legal child missing-script fixture.");
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root),
                    Is.Zero, "The fixture must prove a root-only scan is insufficient.");

                var exception = InvokePrivateBuilderMethod(
                    "ValidateNoMissingScriptsRecursively", root);
                Assert.That(exception, Is.TypeOf<InvalidOperationException>());
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [TestCase("copy")]
        [TestCase("background")]
        [TestCase("theme")]
        public void ValidatePublishedUiSet_RejectsCanonicalPrefabDrift(string drift)
        {
            Phase6DecorationAssetBuilder.BuildAll();
            var root = PrefabUtility.LoadPrefabContents(
                Phase6DecorationAssetPaths.DecorationActionBarPrefabPath);
            try
            {
                var panel = root.transform.Find("ActionPanel");
                if (drift == "copy")
                {
                    root.transform.Find("FeedbackToast/Message")
                        .GetComponent<TMP_Text>().text = "Wrong copy";
                }
                else if (drift == "background")
                {
                    panel.GetComponent<Image>().raycastTarget = true;
                }
                else
                {
                    var theme = AssetDatabase.LoadAssetAtPath<AnimalCafeUiTheme>(
                        Phase5UiAssetPaths.ThemePath);
                    panel.GetComponent<Image>().material = theme.Materials.LightFrost;
                }

                PrefabUtility.SaveAsPrefabAsset(root,
                    Phase6DecorationAssetPaths.DecorationActionBarPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            try
            {
                var exception = InvokePrivateBuilderMethod("ValidatePublishedUiSet");
                Assert.That(exception, Is.TypeOf<InvalidOperationException>());
            }
            finally
            {
                Phase6DecorationAssetBuilder.BuildAll();
            }
        }

        [TestCase("font-material")]
        [TestCase("font-atlas")]
        [TestCase("material-texture")]
        public void ValidatePublishedUiSet_RejectsBrokenTmpOwnershipReferences(string broken)
        {
            Phase6DecorationAssetBuilder.BuildAll();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                Phase6DecorationAssetPaths.DecorationUiFontPath);
            var material = AssetDatabase.LoadAllAssetsAtPath(
                    Phase6DecorationAssetPaths.DecorationUiFontPath)
                .OfType<Material>().Single();
            var atlas = AssetDatabase.LoadAllAssetsAtPath(
                    Phase6DecorationAssetPaths.DecorationUiFontPath)
                .OfType<Texture2D>().Single();
            var externalMaterial = AssetDatabase.LoadAllAssetsAtPath(
                    "Assets/UI/Phase5/Fonts/NotoSansSC-Regular SDF.asset")
                .OfType<Material>().First();
            var externalAtlas = AssetDatabase.LoadAllAssetsAtPath(
                    "Assets/UI/Phase5/Fonts/NotoSansSC-Regular SDF.asset")
                .OfType<Texture2D>().First();
            try
            {
                if (broken == "material-texture")
                {
                    material.mainTexture = externalAtlas;
                    EditorUtility.SetDirty(material);
                }
                else
                {
                    var serialized = new SerializedObject(font);
                    serialized.Update();
                    if (broken == "font-material")
                    {
                        serialized.FindProperty("m_Material").objectReferenceValue =
                            externalMaterial;
                    }
                    else
                    {
                        serialized.FindProperty("m_AtlasTextures")
                            .GetArrayElementAtIndex(0).objectReferenceValue = externalAtlas;
                    }

                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(font);
                }

                AssetDatabase.SaveAssetIfDirty(
                    broken == "material-texture" ? material : font);
                var exception = InvokePrivateBuilderMethod("ValidateLiveUiFont", font);
                Assert.That(exception, Is.TypeOf<InvalidOperationException>());
            }
            finally
            {
                var serialized = new SerializedObject(font);
                serialized.Update();
                serialized.FindProperty("m_Material").objectReferenceValue = material;
                var atlases = serialized.FindProperty("m_AtlasTextures");
                atlases.arraySize = 1;
                atlases.GetArrayElementAtIndex(0).objectReferenceValue = atlas;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                material.mainTexture = atlas;
                EditorUtility.SetDirty(font);
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssetIfDirty(font);
                AssetDatabase.SaveAssetIfDirty(material);
                Phase6DecorationAssetBuilder.BuildAll();
            }
        }

        [Test]
        public void BuildAll_UiFailedAndSuccessfulRunsNeverChangeProtectedPhase5FontThemeBytes()
        {
            var protectedPaths = new[]
            {
                "Assets/UI/Phase5/Fonts/NotoSansSC-Regular.otf",
                "Assets/UI/Phase5/Fonts/NotoSansSC-Regular SDF.asset",
                "Assets/UI/Phase5/Theme/AnimalCafeUiTheme.asset",
                "Assets/UI/Phase5/Resources/TMP Settings.asset"
            };
            var before = CaptureBytesAndMetas(protectedPaths);

            Phase6DecorationAssetBuilder.BuildAll();
            AssertBytesAndMetas(CaptureBytesAndMetas(protectedPaths), before);
            try
            {
                SetPrivateStaticBuilderField("UiPublishFaultAfterWriteForTests", 1);
                Assert.That(
                    () => Phase6DecorationAssetBuilder.BuildAll(),
                    Throws.TypeOf<InvalidOperationException>());
            }
            finally
            {
                SetPrivateStaticBuilderField("UiPublishFaultAfterWriteForTests", -1);
            }

            AssertBytesAndMetas(CaptureBytesAndMetas(protectedPaths), before);
            Phase6DecorationAssetBuilder.BuildAll();
            AssertBytesAndMetas(CaptureBytesAndMetas(protectedPaths), before);
        }

        [Test]
        public void ProductionLookupCatalogue_RetainsPhase4FurnitureAndAddsThreePresets()
        {
            Phase6DecorationAssetBuilder.BuildAll();
            var ids = LoadProductionCatalogue().BuildRuntimeCatalog().Definitions
                .Select(definition => definition.Id);

            Assert.That(ids, Is.EqualTo(new[]
            {
                "furniture.work-table.01",
                "furniture.counter.module.01",
                "equipment.coffee-machine.01",
                "equipment.cash-register.01",
                "counter.preset.1x2",
                "counter.preset.1x3",
                "counter.preset.2x3"
            }));
        }

        [Test]
        public void ValidateCatalogue_DuplicateIdsReportBothAssetPaths()
        {
            Phase6DecorationAssetBuilder.BuildAll();
            var first = AssetDatabase.LoadAssetAtPath<FurnitureDefinitionAsset>(
                Phase6DecorationAssetPaths.Counter1x2DefinitionPath);
            var second = AssetDatabase.LoadAssetAtPath<FurnitureDefinitionAsset>(
                Phase6DecorationAssetPaths.Counter1x3DefinitionPath);
            var serialized = new SerializedObject(second);
            var originalId = serialized.FindProperty("definitionId").stringValue;
            serialized.FindProperty("definitionId").stringValue = first.DefinitionId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            try
            {
                Assert.That(
                    () => Phase6DecorationAssetBuilder.ValidateUniqueDefinitionIds(
                        new[] { first, second }),
                    Throws.TypeOf<ArgumentException>().With.Message.Contains(
                            Phase6DecorationAssetPaths.Counter1x2DefinitionPath)
                        .And.Message.Contains(
                            Phase6DecorationAssetPaths.Counter1x3DefinitionPath));
            }
            finally
            {
                serialized.FindProperty("definitionId").stringValue = originalId;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        [Test]
        public void PublishProductionCatalogue_InvalidCandidatePreservesLiveAsset()
        {
            Phase6DecorationAssetBuilder.BuildAll();
            var path = Phase6DecorationAssetPaths.ProductionCataloguePath;
            var guid = AssetDatabase.AssetPathToGUID(path);
            var hash = HashAsset(path);
            var expectedIds = LoadProductionCatalogue().BuildRuntimeCatalog().Definitions
                .Select(definition => definition.Id).ToArray();
            var duplicate = AssetDatabase.LoadAssetAtPath<FurnitureDefinitionAsset>(
                Phase6DecorationAssetPaths.Counter1x2DefinitionPath);
            try
            {
                var exception = InvokePrivateBuilderMethod(
                    "PublishProductionCatalogue",
                    (object)new FurnitureDefinitionAsset[] { duplicate, duplicate });
                Assert.That(exception, Is.TypeOf<ArgumentException>());
                Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
                Assert.That(HashAsset(path), Is.EqualTo(hash));
                Assert.That(LoadProductionCatalogue().BuildRuntimeCatalog().Definitions
                    .Select(definition => definition.Id), Is.EqualTo(expectedIds));
            }
            finally
            {
                Phase6DecorationAssetBuilder.BuildAll();
            }
        }

        [Test]
        public void PublishDecorationCatalogue_InvalidCandidatePreservesLiveAsset()
        {
            Phase6DecorationAssetBuilder.BuildAll();
            var path = Phase6DecorationAssetPaths.DecorationCataloguePath;
            var guid = AssetDatabase.AssetPathToGUID(path);
            var hash = HashAsset(path);
            var expectedIds = LoadDecorationCatalogue().Entries
                .Select(entry => entry.Definition.DefinitionId).ToArray();
            var definition = AssetDatabase.LoadAssetAtPath<FurnitureDefinitionAsset>(
                Phase6DecorationAssetPaths.Counter1x2DefinitionPath);
            try
            {
                var exception = InvokePrivateBuilderMethod(
                    "PublishDecorationCatalogue",
                    new FurnitureDefinitionAsset[] { definition },
                    new Sprite[] { null });
                Assert.That(exception, Is.TypeOf<InvalidOperationException>());
                Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
                Assert.That(HashAsset(path), Is.EqualTo(hash));
                Assert.That(LoadDecorationCatalogue().Entries
                    .Select(entry => entry.Definition.DefinitionId), Is.EqualTo(expectedIds));
            }
            finally
            {
                Phase6DecorationAssetBuilder.BuildAll();
            }
        }

        [TestCase("lattice", "lattice")]
        [TestCase("duplicate-id", "unique")]
        [TestCase("rotation", "rotation")]
        [TestCase("height", "surface Y")]
        [TestCase("clearance", "above")]
        public void ValidateCounterPresetContract_RejectsBrokenGeneratedContract(
            string brokenContract,
            string expectedMessage)
        {
            Phase6DecorationAssetBuilder.BuildAll();
            var root = PrefabUtility.LoadPrefabContents(
                Phase6DecorationAssetPaths.Counter1x2PrefabPath);
            var definition = ScriptableObject.CreateInstance<FurnitureDefinitionAsset>();
            try
            {
                SetDefinition(definition, "counter.preset.fixture", root, 1, 2);
                var slots = root.GetComponentsInChildren<SurfaceSlotMarker>(true)
                    .OrderBy(slot => slot.SlotId, StringComparer.Ordinal).ToArray();
                if (brokenContract == "lattice")
                    slots[0].transform.localPosition += Vector3.right * 0.25f;
                else if (brokenContract == "duplicate-id")
                    SetSlotId(slots[1], slots[0].SlotId);
                else if (brokenContract == "rotation")
                    slots[0].transform.localRotation = Quaternion.Euler(0f, 15f, 0f);
                else if (brokenContract == "height")
                    slots[0].transform.localPosition = new Vector3(
                        slots[0].transform.localPosition.x,
                        0.5f,
                        slots[0].transform.localPosition.z);
                else if (brokenContract == "clearance")
                    root.GetComponent<BoxCollider>().size += Vector3.up * 0.2f;

                Assert.That(
                    () => Phase6DecorationAssetBuilder.ValidateCounterPresetContract(definition),
                    Throws.TypeOf<InvalidOperationException>().With.Message.Contains(
                        expectedMessage));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void AssertPreset(
            string definitionPath,
            string prefabPath,
            int width,
            int depth,
            int expectedVisualCount)
        {
            var definition = AssetDatabase.LoadAssetAtPath<FurnitureDefinitionAsset>(definitionPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(definition, Is.Not.Null, definitionPath);
            Assert.That(prefab, Is.Not.Null, prefabPath);
            Assert.That(definition.Prefab, Is.SameAs(prefab));
            Assert.That(definition.FootprintWidth, Is.EqualTo(width));
            Assert.That(definition.FootprintDepth, Is.EqualTo(depth));
            Assert.That(definition.AllowedPlacementSurfaces, Is.EqualTo(PlacementSurfaceType.Floor));
            Assert.That(definition.FunctionType, Is.EqualTo(FurnitureFunctionType.None));
            Assert.That(prefab.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(prefab.transform.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(prefab.transform.localScale, Is.EqualTo(Vector3.one));

            var visuals = prefab.GetComponentsInChildren<Transform>(true)
                .Where(transform => transform != prefab.transform &&
                    transform.name.StartsWith("CounterVisual_", StringComparison.Ordinal))
                .ToArray();
            Assert.That(visuals, Has.Length.EqualTo(expectedVisualCount));
            Assert.That(visuals.All(visual => visual.localScale == Vector3.one), Is.True);
            Assert.That(prefab.GetComponentsInChildren<SurfaceSlotMarker>(true),
                Has.Length.EqualTo(width * depth));

            var slots = prefab.GetComponentsInChildren<SurfaceSlotMarker>(true)
                .OrderBy(slot => slot.SlotId, StringComparer.Ordinal).ToArray();
            for (var index = 0; index < slots.Length; index++)
            {
                var expectedX = index % width - (width - 1) * 0.5f;
                var expectedZ = index / width - (depth - 1) * 0.5f;
                Assert.That(slots[index].SlotId, Is.EqualTo($"slot.{index}"));
                Assert.That(slots[index].transform.localPosition,
                    Is.EqualTo(new Vector3(expectedX, 0.72f, expectedZ)));
                Assert.That(slots[index].transform.localRotation, Is.EqualTo(Quaternion.identity));
            }

            var rendererBounds = GetRootLocalRendererBounds(prefab);
            Assert.That(rendererBounds.size.x, Is.EqualTo(width).Within(0.03f));
            Assert.That(rendererBounds.size.z, Is.EqualTo(depth).Within(0.03f));
            var collider = prefab.GetComponent<BoxCollider>();
            Assert.That(collider, Is.Not.Null);
            Assert.That(collider.size.x, Is.EqualTo(width).Within(0.03f));
            Assert.That(collider.size.z, Is.EqualTo(depth).Within(0.03f));
            Assert.That(collider.bounds.max.y, Is.LessThanOrEqualTo(0.7201f));
        }

        private static Bounds GetRootLocalRendererBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers, Is.Not.Empty);
            var firstBounds = renderers[0].bounds;
            var minimum = new Vector3(float.PositiveInfinity, float.PositiveInfinity,
                float.PositiveInfinity);
            var maximum = new Vector3(float.NegativeInfinity, float.NegativeInfinity,
                float.NegativeInfinity);
            foreach (var renderer in renderers)
            {
                var bounds = renderer.bounds;
                foreach (var x in new[] { bounds.min.x, bounds.max.x })
                foreach (var y in new[] { bounds.min.y, bounds.max.y })
                foreach (var z in new[] { bounds.min.z, bounds.max.z })
                {
                    var local = root.transform.InverseTransformPoint(new Vector3(x, y, z));
                    minimum = Vector3.Min(minimum, local);
                    maximum = Vector3.Max(maximum, local);
                }
            }

            var result = firstBounds;
            result.SetMinMax(minimum, maximum);
            return result;
        }

        private static Dictionary<string, string> CaptureGuids()
        {
            return Phase6DecorationAssetPaths.GeneratedAssetPaths.ToDictionary(
                path => path,
                AssetDatabase.AssetPathToGUID,
                StringComparer.Ordinal);
        }

        private static Dictionary<string, int> CaptureFeatureLookupFlags(
            TMP_FontAsset font)
        {
            Assert.That(font, Is.Not.Null);
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            var serialized = new SerializedObject(font);
            var property = serialized.GetIterator();
            var enterChildren = true;
            while (property.Next(enterChildren))
            {
                enterChildren = true;
                if (property.name == "m_FeatureLookupFlags")
                {
                    result.Add(property.propertyPath, property.intValue);
                }
            }

            return result;
        }

        private static void AssertFeatureLookupFlagsEqual(
            IReadOnlyDictionary<string, int> actual,
            IReadOnlyDictionary<string, int> expected,
            string message)
        {
            Assert.That(actual.Keys, Is.EquivalentTo(expected.Keys), message);
            foreach (var item in expected)
            {
                Assert.That(actual[item.Key], Is.EqualTo(item.Value),
                    $"{message} Property: {item.Key}");
            }
        }

        private static void WithTemporarilyAbsentUiFolders(Action body)
        {
            const string fontBackup = "Assets/UI/Phase6/Fonts_Task6TestBackup";
            const string prefabBackup = "Assets/UI/Phase6/Prefabs_Task6TestBackup";
            var fontMoved = false;
            var prefabMoved = false;
            try
            {
                Assert.That(AssetDatabase.IsValidFolder(fontBackup), Is.False, fontBackup);
                Assert.That(AssetDatabase.IsValidFolder(prefabBackup), Is.False, prefabBackup);
                var fontError = AssetDatabase.MoveAsset(
                    Phase6DecorationAssetPaths.UiFontFolderPath, fontBackup);
                Assert.That(fontError, Is.Empty);
                fontMoved = true;
                var prefabError = AssetDatabase.MoveAsset(
                    Phase6DecorationAssetPaths.UiPrefabFolderPath, prefabBackup);
                Assert.That(prefabError, Is.Empty);
                prefabMoved = true;
                body();
            }
            finally
            {
                var restoreErrors = new List<string>();
                if (prefabMoved)
                {
                    try
                    {
                        AssetDatabase.DeleteAsset(Phase6DecorationAssetPaths.UiPrefabFolderPath);
                        var error = AssetDatabase.IsValidFolder(prefabBackup)
                            ? AssetDatabase.MoveAsset(prefabBackup,
                                Phase6DecorationAssetPaths.UiPrefabFolderPath)
                            : "Prefab backup folder is missing.";
                        if (!string.IsNullOrEmpty(error))
                        {
                            restoreErrors.Add("Prefab restore: " + error);
                        }
                    }
                    catch (Exception exception)
                    {
                        restoreErrors.Add("Prefab restore: " + exception.Message);
                    }
                }

                if (fontMoved)
                {
                    try
                    {
                        AssetDatabase.DeleteAsset(Phase6DecorationAssetPaths.UiFontFolderPath);
                        var error = AssetDatabase.IsValidFolder(fontBackup)
                            ? AssetDatabase.MoveAsset(fontBackup,
                                Phase6DecorationAssetPaths.UiFontFolderPath)
                            : "Font backup folder is missing.";
                        if (!string.IsNullOrEmpty(error))
                        {
                            restoreErrors.Add("Font restore: " + error);
                        }
                    }
                    catch (Exception exception)
                    {
                        restoreErrors.Add("Font restore: " + exception.Message);
                    }
                }

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Assert.That(restoreErrors, Is.Empty, string.Join("\n", restoreErrors));
            }
        }

        private static void AssertUiTargetFoldersAbsent()
        {
            Assert.That(AssetDatabase.IsValidFolder(
                Phase6DecorationAssetPaths.UiFontFolderPath), Is.False);
            Assert.That(AssetDatabase.IsValidFolder(
                Phase6DecorationAssetPaths.UiPrefabFolderPath), Is.False);
            foreach (var path in new[]
            {
                Phase6DecorationAssetPaths.DecorationUiFontPath,
                Phase6DecorationAssetPaths.DecorationCataloguePrefabPath,
                Phase6DecorationAssetPaths.DecorationActionBarPrefabPath,
                Phase6DecorationAssetPaths.DecorationStoreModalPrefabPath
            })
            {
                Assert.That(AssetDatabase.LoadMainAssetAtPath(path), Is.Null, path);
                Assert.That(File.Exists(Path.GetFullPath(path)), Is.False, path);
                Assert.That(File.Exists(Path.GetFullPath(path + ".meta")), Is.False,
                    path + ".meta");
            }
        }

        private static UiAssetSnapshot CaptureUiSnapshot()
        {
            var paths = new[]
            {
                Phase6DecorationAssetPaths.DecorationUiFontPath,
                Phase6DecorationAssetPaths.DecorationCataloguePrefabPath,
                Phase6DecorationAssetPaths.DecorationActionBarPrefabPath,
                Phase6DecorationAssetPaths.DecorationStoreModalPrefabPath
            };
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                Phase6DecorationAssetPaths.DecorationUiFontPath);
            Assert.That(font, Is.Not.Null);
            var subassets = AssetDatabase.LoadAllAssetsAtPath(
                    Phase6DecorationAssetPaths.DecorationUiFontPath)
                .Where(item => item is TMP_FontAsset
                    || item is Material
                    || item is Texture2D)
                .Select(item => CaptureObjectIdentity(item))
                .ToArray();
            var rootComponents = new[]
            {
                CapturePrefabViewIdentity<DecorationCatalogueView>(
                    Phase6DecorationAssetPaths.DecorationCataloguePrefabPath),
                CapturePrefabViewIdentity<DecorationActionBarView>(
                    Phase6DecorationAssetPaths.DecorationActionBarPrefabPath),
                CapturePrefabViewIdentity<DecorationStoreModalView>(
                    Phase6DecorationAssetPaths.DecorationStoreModalPrefabPath)
            };
            return new UiAssetSnapshot(
                CaptureBytesAndMetas(paths),
                paths.ToDictionary(path => path, AssetDatabase.AssetPathToGUID,
                    StringComparer.Ordinal),
                subassets,
                rootComponents);
        }

        private static string CapturePrefabViewIdentity<T>(string path) where T : Component
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(root, Is.Not.Null, path);
            var component = root.GetComponent<T>();
            Assert.That(component, Is.Not.Null, path);
            return CaptureObjectIdentity(component);
        }

        private static string CaptureObjectIdentity(UnityEngine.Object item)
        {
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                item, out string guid, out long localId), Is.True, item.name);
            return $"{item.GetType().FullName}|{item.name}|{guid}|{localId}";
        }

        private static Dictionary<string, byte[]> CaptureBytesAndMetas(IEnumerable<string> paths)
        {
            var snapshot = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (var path in paths)
            {
                foreach (var candidate in new[] { path, path + ".meta" })
                {
                    Assert.That(File.Exists(Path.GetFullPath(candidate)), Is.True, candidate);
                    snapshot.Add(candidate, File.ReadAllBytes(Path.GetFullPath(candidate)));
                }
            }

            return snapshot;
        }

        private static void AssertBytesAndMetas(
            IReadOnlyDictionary<string, byte[]> actual,
            IReadOnlyDictionary<string, byte[]> expected)
        {
            Assert.That(actual.Keys, Is.EquivalentTo(expected.Keys));
            foreach (var path in expected.Keys)
            {
                Assert.That(actual[path], Is.EqualTo(expected[path]), path);
            }
        }

        private static void RestoreBytesAndMetasWithoutBuilder(
            IReadOnlyDictionary<string, byte[]> expected)
        {
            var errors = new List<string>();
            foreach (var item in expected.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                try
                {
                    File.WriteAllBytes(Path.GetFullPath(item.Key), item.Value);
                }
                catch (Exception exception)
                {
                    errors.Add($"Restore write failed for {item.Key}: {exception.Message}");
                }
            }

            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            catch (Exception exception)
            {
                errors.Add("AssetDatabase refresh failed: " + exception.Message);
            }

            foreach (var item in expected.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                try
                {
                    var fullPath = Path.GetFullPath(item.Key);
                    if (!File.Exists(fullPath))
                    {
                        errors.Add("Restored file is missing: " + item.Key);
                        continue;
                    }

                    if (!File.ReadAllBytes(fullPath).SequenceEqual(item.Value))
                    {
                        errors.Add("Restored bytes differ: " + item.Key);
                    }
                }
                catch (Exception exception)
                {
                    errors.Add($"Restore verification failed for {item.Key}: {exception.Message}");
                }
            }

            Assert.That(errors, Is.Empty, string.Join("\n", errors));
        }

        private static void RestoreBytesAndMetasWithTargetedImport(
            IReadOnlyDictionary<string, byte[]> expected)
        {
            var errors = new List<string>();
            foreach (var item in expected.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                try
                {
                    File.WriteAllBytes(Path.GetFullPath(item.Key), item.Value);
                }
                catch (Exception exception)
                {
                    errors.Add($"Restore write failed for {item.Key}: {exception.Message}");
                }
            }

            foreach (var assetPath in expected.Keys
                .Where(path => !path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.Ordinal))
            {
                try
                {
                    AssetDatabase.ImportAsset(assetPath,
                        ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                }
                catch (Exception exception)
                {
                    errors.Add($"Targeted import failed for {assetPath}: {exception.Message}");
                }
            }

            foreach (var item in expected.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                try
                {
                    var fullPath = Path.GetFullPath(item.Key);
                    if (!File.Exists(fullPath))
                    {
                        errors.Add("Restored file is missing: " + item.Key);
                    }
                    else if (!File.ReadAllBytes(fullPath).SequenceEqual(item.Value))
                    {
                        errors.Add("Restored bytes differ: " + item.Key);
                    }
                }
                catch (Exception exception)
                {
                    errors.Add($"Restore verification failed for {item.Key}: {exception.Message}");
                }
            }

            Assert.That(errors, Is.Empty, string.Join("\n", errors));
        }

        private static void AssertUiSnapshot(UiAssetSnapshot actual, UiAssetSnapshot expected)
        {
            AssertBytesAndMetas(actual.BytesAndMetas, expected.BytesAndMetas);
            Assert.That(actual.Guids, Is.EqualTo(expected.Guids));
            Assert.That(actual.FontSubassets, Is.EqualTo(expected.FontSubassets));
            Assert.That(actual.PrefabViewComponents, Is.EqualTo(expected.PrefabViewComponents));
        }

        private static void SetPrivateStaticBuilderField(string name, object value)
        {
            var field = typeof(Phase6DecorationAssetBuilder).GetField(
                name,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(null, value);
        }

        private static DecorationCatalogueAsset LoadDecorationCatalogue()
        {
            return AssetDatabase.LoadAssetAtPath<DecorationCatalogueAsset>(
                Phase6DecorationAssetPaths.DecorationCataloguePath);
        }

        private static FurnitureContentCatalog LoadProductionCatalogue()
        {
            return AssetDatabase.LoadAssetAtPath<FurnitureContentCatalog>(
                Phase6DecorationAssetPaths.ProductionCataloguePath);
        }

        private static string HashAsset(string assetPath)
        {
            var fullPath = Path.GetFullPath(assetPath);
            using (var stream = File.OpenRead(fullPath))
            using (var hash = SHA256.Create())
            {
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", string.Empty);
            }
        }

        private static Exception InvokePrivateBuilderMethod(
            string methodName,
            params object[] arguments)
        {
            var method = typeof(Phase6DecorationAssetBuilder).GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            try
            {
                method.Invoke(null, arguments);
                Assert.Fail($"{methodName} must reject the invalid candidate.");
                return null;
            }
            catch (TargetInvocationException exception)
            {
                return exception.InnerException;
            }
        }

        private static void AssertThumbnailHasVisiblePixels(string assetPath)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(texture.LoadImage(File.ReadAllBytes(Path.GetFullPath(assetPath))),
                    Is.True, assetPath);
                Assert.That(texture.GetPixels32().Any(pixel => pixel.a > 0), Is.True,
                    $"{assetPath} must contain at least one visible pixel.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void SetDefinition(
            FurnitureDefinitionAsset definition,
            string definitionId,
            GameObject prefab,
            int width,
            int depth)
        {
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("definitionId").stringValue = definitionId;
            serialized.FindProperty("displayName").stringValue = "Counter Fixture";
            serialized.FindProperty("prefab").objectReferenceValue = prefab;
            serialized.FindProperty("footprintWidth").intValue = width;
            serialized.FindProperty("footprintDepth").intValue = depth;
            serialized.FindProperty("allowedPlacementSurfaces").intValue =
                (int)PlacementSurfaceType.Floor;
            serialized.FindProperty("functionType").intValue = (int)FurnitureFunctionType.None;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSlotId(SurfaceSlotMarker slot, string slotId)
        {
            var serialized = new SerializedObject(slot);
            serialized.FindProperty("slotId").stringValue = slotId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private sealed class UiAssetSnapshot
        {
            public UiAssetSnapshot(
                IReadOnlyDictionary<string, byte[]> bytesAndMetas,
                IReadOnlyDictionary<string, string> guids,
                IReadOnlyList<string> fontSubassets,
                IReadOnlyList<string> prefabViewComponents)
            {
                BytesAndMetas = bytesAndMetas;
                Guids = guids;
                FontSubassets = fontSubassets;
                PrefabViewComponents = prefabViewComponents;
            }

            public IReadOnlyDictionary<string, byte[]> BytesAndMetas { get; }
            public IReadOnlyDictionary<string, string> Guids { get; }
            public IReadOnlyList<string> FontSubassets { get; }
            public IReadOnlyList<string> PrefabViewComponents { get; }
        }
    }
}
