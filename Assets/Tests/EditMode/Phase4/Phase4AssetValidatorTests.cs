using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AnimalCafe.Content;
using AnimalCafe.EditorTools.Phase4;
using AnimalCafe.Layout;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AnimalCafe.Tests.Phase4
{
    public sealed class Phase4AssetValidatorTests
    {
        private const string ValidateAllFixtureFolder =
            "Assets/Tests/Phase4ValidatorValidateAllFixture";
        private const string MultiIssueFixtureFolder =
            "Assets/Tests/Phase4ValidatorMultiIssueFixture";
        private const string WallDefinitionFixtureFolder =
            "Assets/Tests/Phase4ValidatorWallDefinitionFixture";
        private const string NestedPrefabFixtureFolder =
            "Assets/Tests/Phase4ValidatorNestedPrefabFixture";
        private const string TechnicalPathFixtureFolder =
            "Assets/Tests/Phase4ValidatorTechnicalPathFixture";
        private const string WallTechnicalFixtureFolder =
            "Assets/Tests/Phase4ValidatorWallTechnicalFixture";

        [Test]
        public void Report_IssuesRejectMutationAndDefensivelyCopyInput()
        {
            var source = new List<Phase4AssetValidationIssue>
            {
                new Phase4AssetValidationIssue(
                    Phase4AssetIssueCode.MissingPrefab,
                    "Assets/Definitions/DA_Broken.asset",
                    "Prefab is missing.")
            };
            var report = new Phase4AssetValidationReport(0, 1, source);
            source.Clear();

            var mutableView = report.Issues as IList<Phase4AssetValidationIssue>;

            Assert.That(report.Issues.Count, Is.EqualTo(1));
            Assert.That(mutableView, Is.Not.Null,
                "The read-only collection should still be inspectable as IList for this mutation guard.");
            Assert.Throws<System.NotSupportedException>(() => mutableView.Add(
                new Phase4AssetValidationIssue(
                    Phase4AssetIssueCode.InvalidDefinition,
                    "Assets/Definitions/DA_Another.asset",
                    "Must not be added.")));
            Assert.Throws<System.NotSupportedException>(() => mutableView[0] =
                new Phase4AssetValidationIssue(
                    Phase4AssetIssueCode.InvalidDefinition,
                    "Assets/Definitions/DA_Replaced.asset",
                    "Must not replace the original issue."));
            Assert.That(report.Issues.Count, Is.EqualTo(1));
            Assert.That(report.Issues[0].Code, Is.EqualTo(Phase4AssetIssueCode.MissingPrefab));
        }

        [Test]
        public void Technical_OnePrefabWithMultipleBrokenReferences_PreservesEveryActionableIssue()
        {
            AssetDatabase.DeleteAsset(MultiIssueFixtureFolder);
            EnsureAssetFolder(MultiIssueFixtureFolder);
            var mesh = CreateTriangleMesh("MultiIssueMesh");
            var meshPath = $"{MultiIssueFixtureFolder}/MultiIssueMesh.asset";
            AssetDatabase.CreateAsset(mesh, meshPath);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader, Is.Not.Null);
            var validMaterial = new Material(shader);
            var validTexture = new Texture2D(1, 1);
            validMaterial.SetTexture("_BaseMap", validTexture);
            var validMaterialPath = $"{MultiIssueFixtureFolder}/MAT_Valid.mat";
            AssetDatabase.CreateAsset(validMaterial, validMaterialPath);
            var missingTextureMaterial = new Material(shader);
            missingTextureMaterial.SetTexture("_BaseMap", null);
            var missingTextureMaterialPath = $"{MultiIssueFixtureFolder}/MAT_MissingTexture.mat";
            AssetDatabase.CreateAsset(missingTextureMaterial, missingTextureMaterialPath);

            var root = new GameObject("PF_MultipleBrokenReferences");
            AddRendererChild(root, "MissingMeshRenderer", null, validMaterial);
            AddRendererChild(root, "MissingMaterialRenderer", mesh, null);
            AddRendererChild(root, "MissingTextureRenderer", mesh, missingTextureMaterial);
            root.AddComponent<BoxCollider>();
            var missingScriptNode = new GameObject("MissingScriptNode");
            missingScriptNode.transform.SetParent(root.transform, false);
            var temporaryMarker = missingScriptNode.AddComponent<SurfaceSlotMarker>();
            var markerScript = MonoScript.FromMonoBehaviour(temporaryMarker);
            var markerGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(markerScript));
            var prefabPath = $"{MultiIssueFixtureFolder}/PF_MultipleBrokenReferences.prefab";
            FurnitureDefinitionAsset definition = null;

            try
            {
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Object.DestroyImmediate(root);
                root = null;

                var yamlPath = System.IO.Path.GetFullPath(prefabPath);
                var yaml = System.IO.File.ReadAllText(yamlPath);
                Assert.That(yaml, Does.Contain(markerGuid));
                yaml = yaml.Replace(markerGuid, "11111111111111111111111111111111");
                System.IO.File.WriteAllText(yamlPath, yaml);
                AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceSynchronousImport);

                var savedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                Assert.That(savedPrefab, Is.Not.Null);
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                    savedPrefab.transform.Find("MissingScriptNode").gameObject), Is.EqualTo(1));
                definition = CreateDefinition(
                    "furniture.other.01",
                    "Broken References",
                    savedPrefab,
                    1,
                    1,
                    PlacementSurfaceType.Floor,
                    FurnitureFunctionType.None);

                var report = Phase4AssetValidator.ValidateFurnitureDefinition(definition);
                var messages = report.Issues.Select(issue => issue.Message).ToArray();

                Assert.That(messages.Any(message => message.Contains("MissingMeshRenderer") &&
                    message.Contains("Mesh")), Is.True);
                Assert.That(messages.Any(message => message.Contains("MissingMaterialRenderer") &&
                    message.Contains("Material")), Is.True);
                Assert.That(messages.Any(message => message.Contains("MAT_MissingTexture") &&
                    message.Contains("_BaseMap")), Is.True);
                Assert.That(messages.Any(message => message.Contains("MissingScriptNode") &&
                    message.Contains("missing script")), Is.True);
                Assert.That(messages.Distinct().Count(), Is.EqualTo(messages.Length));
            }
            finally
            {
                if (definition != null)
                {
                    Object.DestroyImmediate(definition);
                }

                if (root != null)
                {
                    Object.DestroyImmediate(root);
                }

                AssetDatabase.DeleteAsset(MultiIssueFixtureFolder);
                AssetDatabase.Refresh();
            }
        }

        [Test]
        public void Technical_RealFurniturePrefabIssueUsesPrefabPathAndDefinitionContext()
        {
            AssetDatabase.DeleteAsset(TechnicalPathFixtureFolder);
            EnsureAssetFolder(TechnicalPathFixtureFolder);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            var texture = new Texture2D(1, 1);
            material.SetTexture("_BaseMap", texture);
            var materialPath = $"{TechnicalPathFixtureFolder}/MAT_Valid.mat";
            AssetDatabase.CreateAsset(material, materialPath);
            var root = new GameObject("PF_PathFurniture");
            AddRendererChild(root, "MissingMesh", null, material);
            root.AddComponent<BoxCollider>();
            var prefabPath = $"{TechnicalPathFixtureFolder}/PF_PathFurniture.prefab";
            var definitionPath = $"{TechnicalPathFixtureFolder}/DA_PathFurniture.asset";

            try
            {
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                var definition = CreateDefinition(
                    "furniture.review-path.01",
                    "Path Furniture",
                    prefab,
                    1,
                    1,
                    PlacementSurfaceType.Floor,
                    FurnitureFunctionType.None);
                AssetDatabase.CreateAsset(definition, definitionPath);
                AssetDatabase.SaveAssets();

                var report = Phase4AssetValidator.ValidateFurnitureDefinition(definition);
                var missingMesh = report.Issues.Single(issue =>
                    issue.Code == Phase4AssetIssueCode.MissingReference &&
                    issue.Message.Contains("MissingMesh") &&
                    issue.Message.Contains("must reference a Mesh"));

                Assert.That(missingMesh.AssetPath, Is.EqualTo(prefabPath));
                Assert.That(missingMesh.Message, Does.Contain("furniture.review-path.01"));
                Assert.That(missingMesh.Message, Does.Contain(definitionPath));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(texture);
                AssetDatabase.DeleteAsset(TechnicalPathFixtureFolder);
                AssetDatabase.Refresh();
            }
        }

        [Test]
        public void Technical_SameNamedSiblingFailuresRemainSeparateByHierarchyIdentity()
        {
            var root = new GameObject("PF_SameNamedSiblings");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            var texture = new Texture2D(1, 1);
            material.SetTexture("_BaseMap", texture);
            AddRendererChild(root, "SameName", null, material);
            AddRendererChild(root, "SameName", null, material);
            root.AddComponent<BoxCollider>();
            var definition = CreateDefinition(
                "furniture.same-name.01",
                "Same Name",
                root,
                1,
                1,
                PlacementSurfaceType.Floor,
                FurnitureFunctionType.None);

            try
            {
                var missingMeshIssues = Phase4AssetValidator
                    .ValidateFurnitureDefinition(definition)
                    .Issues
                    .Where(issue =>
                        issue.Code == Phase4AssetIssueCode.MissingReference &&
                        issue.Message.Contains("SameName") &&
                        issue.Message.Contains("Mesh"))
                    .ToArray();

                Assert.That(missingMeshIssues.Length, Is.EqualTo(2));
                Assert.That(missingMeshIssues.Select(issue => issue.Message).Distinct().Count(),
                    Is.EqualTo(2));
                Assert.That(missingMeshIssues.Any(issue => issue.Message.Contains("SameName[0]")),
                    Is.True);
                Assert.That(missingMeshIssues.Any(issue => issue.Message.Contains("SameName[1]")),
                    Is.True);
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void Definition_MissingPrefab_ReportsSpecificAssetAndIssueCode()
        {
            var definition = CreateDefinition(
                "equipment.cash-register.01",
                "Cash Register",
                null,
                1,
                1,
                PlacementSurfaceType.FurnitureSurface,
                FurnitureFunctionType.CashRegister);

            try
            {
                var report = Phase4AssetValidator.ValidateFurnitureDefinition(definition);

                Assert.That(report.Issues.Select(issue => issue.Code),
                    Does.Contain(Phase4AssetIssueCode.MissingPrefab));
                Assert.That(report.Issues.Single().AssetPath, Does.Contain("cash-register"));
                Assert.That(report.ValidAssetCount, Is.Zero);
                Assert.That(report.InvalidAssetCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        [TestCase(null, "Cash Register", 1, 1,
            PlacementSurfaceType.FurnitureSurface, FurnitureFunctionType.CashRegister)]
        [TestCase("Bad ID", "Cash Register", 1, 1,
            PlacementSurfaceType.FurnitureSurface, FurnitureFunctionType.CashRegister)]
        [TestCase("equipment.cash-register.01", null, 1, 1,
            PlacementSurfaceType.FurnitureSurface, FurnitureFunctionType.CashRegister)]
        [TestCase("equipment.cash-register.01", "Cash Register", 0, 1,
            PlacementSurfaceType.FurnitureSurface, FurnitureFunctionType.CashRegister)]
        [TestCase("equipment.cash-register.01", "Cash Register", 33, 32,
            PlacementSurfaceType.FurnitureSurface, FurnitureFunctionType.CashRegister)]
        [TestCase("equipment.cash-register.01", "Cash Register", 1, 1,
            (PlacementSurfaceType)0, FurnitureFunctionType.CashRegister)]
        [TestCase("equipment.cash-register.01", "Cash Register", 1, 1,
            PlacementSurfaceType.FurnitureSurface, (FurnitureFunctionType)999)]
        public void Definition_InvalidAuthoredValues_ReportInvalidDefinition(
            string definitionId,
            string displayName,
            int width,
            int depth,
            PlacementSurfaceType surfaces,
            FurnitureFunctionType functionType)
        {
            var prefab = new GameObject("PF_CashRegister");
            var definition = CreateDefinition(
                definitionId,
                displayName,
                prefab,
                width,
                depth,
                surfaces,
                functionType);

            try
            {
                var report = Phase4AssetValidator.ValidateFurnitureDefinition(definition);

                Assert.That(report.Issues.Select(issue => issue.Code),
                    Does.Contain(Phase4AssetIssueCode.InvalidDefinition));
                Assert.That(report.InvalidAssetCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void DefinitionCollection_DuplicateIds_ReportBothAssetsInvalid()
        {
            var firstPrefab = new GameObject("PF_Counter_A");
            var secondPrefab = new GameObject("PF_Counter_B");
            var first = CreateDefinition(
                "furniture.counter.module.01",
                "Counter A",
                firstPrefab,
                1,
                1,
                PlacementSurfaceType.Floor,
                FurnitureFunctionType.None);
            var second = CreateDefinition(
                "furniture.counter.module.01",
                "Counter B",
                secondPrefab,
                1,
                1,
                PlacementSurfaceType.Floor,
                FurnitureFunctionType.None);

            try
            {
                var report = Phase4AssetValidator.ValidateAll(
                    new List<FurnitureDefinitionAsset> { first, second });

                Assert.That(report.ValidAssetCount, Is.Zero);
                Assert.That(report.InvalidAssetCount, Is.EqualTo(2));
                Assert.That(report.Issues.Count(issue =>
                    issue.Code == Phase4AssetIssueCode.DuplicateDefinitionId), Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(firstPrefab);
                Object.DestroyImmediate(secondPrefab);
            }
        }

        [Test]
        public void ValidateAll_ScansWallAndEntrancePrefabAuthoringWithoutMutation()
        {
            AssetDatabase.DeleteAsset(ValidateAllFixtureFolder);
            EnsureAssetFolder(ValidateAllFixtureFolder);
            var root = new GameObject("PF_InvalidEnvironment");
            AddWallSurface(root, "wall.back-right", 7, 2, 1f);
            AddEntrance(root, "", 7, 7);
            var prefabPath = $"{ValidateAllFixtureFolder}/PF_InvalidEnvironment.prefab";

            try
            {
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Object.DestroyImmediate(root);
                root = null;

                var report = Phase4AssetValidator.ValidateAll();

                Assert.That(report.Issues.Select(issue => issue.Code),
                    Does.Contain(Phase4AssetIssueCode.InvalidWallSurface));
                Assert.That(report.Issues.Select(issue => issue.Code),
                    Does.Contain(Phase4AssetIssueCode.InvalidEntrance));

                var savedRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                Assert.That(savedRoot.GetComponent<WallSurfaceAuthoring>().Columns, Is.EqualTo(7));
                Assert.That(savedRoot.GetComponent<EntrancePortalAuthoring>().EntranceId, Is.Empty);
            }
            finally
            {
                if (root != null)
                {
                    Object.DestroyImmediate(root);
                }

                AssetDatabase.DeleteAsset(ValidateAllFixtureFolder);
                AssetDatabase.Refresh();
            }
        }

        private static void EnsureAssetFolder(string folderPath)
        {
            var current = "Assets";
            foreach (var segment in folderPath.Substring("Assets/".Length).Split('/'))
            {
                var next = $"{current}/{segment}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segment);
                }

                current = next;
            }
        }

        [Test]
        public void Counter_MissingRequiredSurfaceSlot_ReportsInvalidSurfaceSlot()
        {
            var prefab = new GameObject("PF_Counter_Module");
            var definition = CreateDefinition(
                "furniture.counter.module.01",
                "Counter Module",
                prefab,
                1,
                1,
                PlacementSurfaceType.Floor,
                FurnitureFunctionType.None);

            try
            {
                var report = Phase4AssetValidator.ValidateFurnitureDefinition(definition);

                Assert.That(report.Issues.Select(issue => issue.Code),
                    Does.Contain(Phase4AssetIssueCode.InvalidSurfaceSlot));
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void Counter_LongFixture_IncludesInactiveDescendantSlots()
        {
            var prefab = new GameObject("PF_Counter_Long");
            var definition = CreateDefinition(
                "furniture.counter.long.01",
                "Long Counter",
                prefab,
                1,
                3,
                PlacementSurfaceType.Floor,
                FurnitureFunctionType.None);
            AddSurfaceSlot(prefab, "slot.0", new Vector3(0f, 0.5f, -1f));
            AddSurfaceSlot(prefab, "slot.1", new Vector3(0f, 0.5f, 0f));
            var inactive = AddSurfaceSlot(prefab, "slot.2", new Vector3(0f, 0.5f, 1f));
            inactive.gameObject.SetActive(false);

            try
            {
                var report = Phase4AssetValidator.ValidateFurnitureDefinition(definition);

                Assert.That(report.Issues.Select(issue => issue.Code),
                    Has.No.Member(Phase4AssetIssueCode.InvalidSurfaceSlot));
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(prefab);
            }
        }

        [TestCase("duplicate")]
        [TestCase("bottom")]
        [TestCase("outside")]
        public void Counter_InvalidSlotIdOrBounds_ReportsInvalidSurfaceSlot(string invalidCase)
        {
            var prefab = new GameObject("PF_Counter_Long");
            var definition = CreateDefinition(
                "furniture.counter.long.01",
                "Long Counter",
                prefab,
                1,
                3,
                PlacementSurfaceType.Floor,
                FurnitureFunctionType.None);

            AddSurfaceSlot(prefab, "slot.0", new Vector3(0f, 0.5f, -1f));
            AddSurfaceSlot(
                prefab,
                invalidCase == "duplicate" ? "slot.0" : "slot.1",
                invalidCase == "bottom" ? Vector3.zero : new Vector3(0f, 0.5f, 0f));
            AddSurfaceSlot(
                prefab,
                "slot.2",
                invalidCase == "outside"
                    ? new Vector3(0f, 0.5f, 2f)
                    : new Vector3(0f, 0.5f, 1f));

            try
            {
                var report = Phase4AssetValidator.ValidateFurnitureDefinition(definition);

                Assert.That(report.Issues.Select(issue => issue.Code),
                    Does.Contain(Phase4AssetIssueCode.InvalidSurfaceSlot));
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(prefab);
            }
        }

        [TestCase("wrong-surface")]
        [TestCase("wrong-function")]
        [TestCase("overlapping-slots")]
        [TestCase("slot-does-not-fit")]
        public void Counter_CompleteFamilyContract_RejectsInvalidMatrix(string invalidCase)
        {
            var prefab = new GameObject("PF_Counter_Contract");
            var width = invalidCase == "overlapping-slots" ? 2 : 1;
            var definition = CreateDefinition(
                "furniture.counter.contract.01",
                "Counter Contract",
                prefab,
                width,
                1,
                invalidCase == "wrong-surface"
                    ? PlacementSurfaceType.FurnitureSurface
                    : PlacementSurfaceType.Floor,
                invalidCase == "wrong-function"
                    ? FurnitureFunctionType.CoffeeMachine
                    : FurnitureFunctionType.None);
            AddSurfaceSlot(
                prefab,
                "slot.0",
                invalidCase == "slot-does-not-fit"
                    ? new Vector3(0.3f, 0.5f, 0f)
                    : new Vector3(-0.5f, 0.5f, 0f));
            if (width == 2)
            {
                AddSurfaceSlot(prefab, "slot.1", new Vector3(-0.4f, 0.5f, 0f));
            }

            try
            {
                var report = Phase4AssetValidator.ValidateFurnitureDefinition(definition);

                Assert.That(report.Issues.Select(issue => issue.Code),
                    Does.Contain(Phase4AssetIssueCode.InvalidSurfaceSlot));
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void Counter_ColliderExtendingAboveSurfaceSlot_ReportsInvalidSurfaceSlot()
        {
            var productionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase4ProductionAssetBuilder.CounterPrefabPath);
            Assert.That(productionPrefab, Is.Not.Null);

            var prefab = Object.Instantiate(productionPrefab);
            prefab.name = productionPrefab.name;
            var definition = CreateDefinition(
                "furniture.counter.module.01",
                "Counter Module",
                prefab,
                1,
                1,
                PlacementSurfaceType.Floor,
                FurnitureFunctionType.None);

            try
            {
                var slot = prefab.GetComponentInChildren<SurfaceSlotMarker>(true);
                var collider = prefab.GetComponent<BoxCollider>();
                Assert.That(slot, Is.Not.Null);
                Assert.That(collider, Is.Not.Null);

                var slotLocalY = prefab.transform
                    .InverseTransformPoint(slot.transform.position).y;
                var validColliderTop = collider.center.y + collider.size.y * 0.5f;
                Assert.That(validColliderTop, Is.EqualTo(slotLocalY).Within(0.0001f),
                    "The production Counter collider should end exactly at the Surface Slot plane.");

                collider.size = new Vector3(
                    collider.size.x,
                    collider.size.y + 0.2f,
                    collider.size.z);

                var report = Phase4AssetValidator.ValidateFurnitureDefinition(definition);

                Assert.That(report.Issues.Select(issue => issue.Code),
                    Does.Contain(Phase4AssetIssueCode.InvalidSurfaceSlot));
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(prefab);
            }
        }

        [TestCase("valid")]
        [TestCase("missing-slot")]
        [TestCase("duplicate-slot")]
        [TestCase("wrong-surface-function")]
        public void WorkTable_RequiresExactlyOneSurfaceSlotAndFloorNoneContract(string testCase)
        {
            var prefab = new GameObject("PF_WorkTable");
            var definition = CreateDefinition(
                "furniture.work-table.01",
                "Work Table",
                prefab,
                1,
                1,
                testCase == "wrong-surface-function"
                    ? PlacementSurfaceType.FurnitureSurface
                    : PlacementSurfaceType.Floor,
                testCase == "wrong-surface-function"
                    ? FurnitureFunctionType.CoffeeMachine
                    : FurnitureFunctionType.None);
            if (testCase != "missing-slot")
            {
                AddSurfaceSlot(prefab, "surface.0", new Vector3(0f, 0.5f, 0f));
            }

            if (testCase == "duplicate-slot")
            {
                AddSurfaceSlot(prefab, "surface.1", new Vector3(0f, 0.5f, 0f));
            }

            try
            {
                var report = Phase4AssetValidator.ValidateFurnitureDefinition(definition);
                var familyIssues = report.Issues.Select(issue => issue.Code);

                if (testCase == "valid")
                {
                    Assert.That(familyIssues,
                        Has.No.Member(Phase4AssetIssueCode.InvalidSurfaceSlot));
                }
                else
                {
                    Assert.That(familyIssues,
                        Does.Contain(Phase4AssetIssueCode.InvalidSurfaceSlot));
                }
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void Coffee_OnePositiveZForwardMarker_PassesForwardContract()
        {
            var prefab = new GameObject("PF_CoffeeMachine");
            var definition = CreateDefinition(
                "equipment.coffee-machine.01",
                "Coffee Machine",
                prefab,
                1,
                1,
                PlacementSurfaceType.FurnitureSurface,
                FurnitureFunctionType.CoffeeMachine);
            AddForwardMarker(prefab, new Vector3(0f, 0.2f, 0.3f), Quaternion.identity);

            try
            {
                var report = Phase4AssetValidator.ValidateFurnitureDefinition(definition);

                Assert.That(report.Issues.Select(issue => issue.Code),
                    Has.No.Member(Phase4AssetIssueCode.InvalidCoffeeMachineForward));
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(prefab);
            }
        }

        [TestCase("missing")]
        [TestCase("duplicate-inactive")]
        [TestCase("wrong-direction")]
        [TestCase("wrong-surface")]
        [TestCase("customer-side")]
        public void Coffee_InvalidForwardContract_ReportsSpecificIssue(string invalidCase)
        {
            var prefab = new GameObject("PF_CoffeeMachine");
            var definition = CreateDefinition(
                "equipment.coffee-machine.01",
                "Coffee Machine",
                prefab,
                1,
                1,
                invalidCase == "wrong-surface"
                    ? PlacementSurfaceType.Floor
                    : PlacementSurfaceType.FurnitureSurface,
                FurnitureFunctionType.CoffeeMachine);

            if (invalidCase != "missing")
            {
                AddForwardMarker(
                    prefab,
                    invalidCase == "wrong-direction"
                        ? new Vector3(0f, 0.2f, -0.3f)
                        : new Vector3(0f, 0.2f, 0.3f),
                    invalidCase == "wrong-direction"
                        ? Quaternion.Euler(0f, 180f, 0f)
                        : Quaternion.identity);
            }

            if (invalidCase == "duplicate-inactive")
            {
                var duplicate = AddForwardMarker(
                    prefab,
                    new Vector3(0f, 0.2f, 0.3f),
                    Quaternion.identity);
                duplicate.gameObject.SetActive(false);
            }

            if (invalidCase == "customer-side")
            {
                var side = new GameObject("UnexpectedCustomerSide");
                side.transform.SetParent(prefab.transform, false);
                var marker = side.AddComponent<CashRegisterSideMarker>();
                SetSerialized(marker, "sideType", CashRegisterSideType.Customer);
                SetSerialized(marker, "localDirection", CardinalDirection.North);
            }

            try
            {
                var report = Phase4AssetValidator.ValidateFurnitureDefinition(definition);

                Assert.That(report.Issues.Select(issue => issue.Code),
                    Does.Contain(Phase4AssetIssueCode.InvalidCoffeeMachineForward));
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(prefab);
            }
        }

        [TestCase("wrong-function")]
        [TestCase("two-cell-footprint")]
        [TestCase("model-outside-slot")]
        public void Coffee_SingleSlotSurfaceFunctionMatrix_ReportsFamilyIssue(string invalidCase)
        {
            var prefab = new GameObject("PF_CoffeeMachine_Matrix");
            var definition = CreateDefinition(
                "equipment.coffee-machine.matrix",
                "Coffee Machine Matrix",
                prefab,
                invalidCase == "two-cell-footprint" ? 2 : 1,
                1,
                PlacementSurfaceType.FurnitureSurface,
                invalidCase == "wrong-function"
                    ? FurnitureFunctionType.None
                    : FurnitureFunctionType.CoffeeMachine);
            AddForwardMarker(prefab, new Vector3(0f, 0.2f, 0.3f), Quaternion.identity);
            Mesh mesh = null;
            if (invalidCase == "model-outside-slot")
            {
                mesh = CreateWideTriangleMesh("CoffeeOutsideSlotMesh");
                AddRendererChild(prefab, "WideCoffeeModel", mesh, null);
            }

            try
            {
                var report = Phase4AssetValidator.ValidateFurnitureDefinition(definition);

                Assert.That(report.Issues.Select(issue => issue.Code),
                    Does.Contain(Phase4AssetIssueCode.InvalidCoffeeMachineForward));
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(prefab);
                if (mesh != null)
                {
                    Object.DestroyImmediate(mesh);
                }
            }
        }

        [Test]
        public void CashRegister_OneOppositePairIncludingInactiveMarker_PassesSideContract()
        {
            var prefab = new GameObject("PF_CashRegister");
            var definition = CreateDefinition(
                "equipment.cash-register.01",
                "Cash Register",
                prefab,
                1,
                1,
                PlacementSurfaceType.FurnitureSurface,
                FurnitureFunctionType.CashRegister);
            AddCashSide(prefab, CashRegisterSideType.Employee, CardinalDirection.South);
            var customer = AddCashSide(
                prefab,
                CashRegisterSideType.Customer,
                CardinalDirection.North);
            customer.gameObject.SetActive(false);

            try
            {
                var report = Phase4AssetValidator.ValidateFurnitureDefinition(definition);

                Assert.That(report.Issues.Select(issue => issue.Code),
                    Has.No.Member(Phase4AssetIssueCode.InvalidCashRegisterSides));
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(prefab);
            }
        }

        [TestCase("missing")]
        [TestCase("duplicate")]
        [TestCase("same")]
        [TestCase("perpendicular")]
        [TestCase("undefined-direction")]
        [TestCase("undefined-side")]
        [TestCase("wrong-surface")]
        public void CashRegister_InvalidSideContract_ReportsSpecificIssue(string invalidCase)
        {
            var prefab = new GameObject("PF_CashRegister");
            var definition = CreateDefinition(
                "equipment.cash-register.01",
                "Cash Register",
                prefab,
                1,
                1,
                invalidCase == "wrong-surface"
                    ? PlacementSurfaceType.Floor
                    : PlacementSurfaceType.FurnitureSurface,
                FurnitureFunctionType.CashRegister);

            AddCashSide(
                prefab,
                invalidCase == "undefined-side"
                    ? (CashRegisterSideType)999
                    : CashRegisterSideType.Employee,
                invalidCase == "undefined-direction"
                    ? (CardinalDirection)999
                    : CardinalDirection.South);
            if (invalidCase != "missing")
            {
                AddCashSide(
                    prefab,
                    CashRegisterSideType.Customer,
                    invalidCase == "same"
                        ? CardinalDirection.South
                        : invalidCase == "perpendicular"
                            ? CardinalDirection.East
                            : CardinalDirection.North);
            }

            if (invalidCase == "duplicate")
            {
                AddCashSide(prefab, CashRegisterSideType.Employee, CardinalDirection.South);
            }

            try
            {
                var report = Phase4AssetValidator.ValidateFurnitureDefinition(definition);

                Assert.That(report.Issues.Select(issue => issue.Code),
                    Does.Contain(Phase4AssetIssueCode.InvalidCashRegisterSides));
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(prefab);
            }
        }

        [TestCase("wrong-function")]
        [TestCase("two-cell-footprint")]
        [TestCase("model-outside-slot")]
        public void CashRegister_SingleSlotSurfaceFunctionMatrix_ReportsFamilyIssue(string invalidCase)
        {
            var prefab = new GameObject("PF_CashRegister_Matrix");
            var definition = CreateDefinition(
                "equipment.cash-register.matrix",
                "Cash Register Matrix",
                prefab,
                invalidCase == "two-cell-footprint" ? 2 : 1,
                1,
                PlacementSurfaceType.FurnitureSurface,
                invalidCase == "wrong-function"
                    ? FurnitureFunctionType.None
                    : FurnitureFunctionType.CashRegister);
            AddCashSide(prefab, CashRegisterSideType.Employee, CardinalDirection.South);
            AddCashSide(prefab, CashRegisterSideType.Customer, CardinalDirection.North);
            Mesh mesh = null;
            if (invalidCase == "model-outside-slot")
            {
                mesh = CreateWideTriangleMesh("CashOutsideSlotMesh");
                AddRendererChild(prefab, "WideCashModel", mesh, null);
            }

            try
            {
                var report = Phase4AssetValidator.ValidateFurnitureDefinition(definition);

                Assert.That(report.Issues.Select(issue => issue.Code),
                    Does.Contain(Phase4AssetIssueCode.InvalidCashRegisterSides));
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(prefab);
                if (mesh != null)
                {
                    Object.DestroyImmediate(mesh);
                }
            }
        }

        [Test]
        public void Wall_TwoUniqueEightByTwoSurfaces_PassSurfaceContract()
        {
            var leftObject = new GameObject("BackLeftWall");
            var rightObject = new GameObject("BackRightWall");
            var left = AddWallSurface(leftObject, "wall.back-left", 8, 2, 1f);
            var right = AddWallSurface(rightObject, "wall.back-right", 8, 2, 1f);

            try
            {
                var report = Phase4AssetValidator.ValidateWallContent(
                    new[] { left, right },
                    new WallMountedInstance[0]);

                Assert.That(report.Issues.Select(issue => issue.Code),
                    Has.No.Member(Phase4AssetIssueCode.InvalidWallSurface));
                Assert.That(report.Issues.Select(issue => issue.Code),
                    Has.No.Member(Phase4AssetIssueCode.InvalidWallPlacement));
            }
            finally
            {
                Object.DestroyImmediate(leftObject);
                Object.DestroyImmediate(rightObject);
            }
        }

        [TestCase("unknown-surface")]
        [TestCase("out-of-bounds")]
        public void Wall_CountsEverySurfaceAndPlacementWithInvalidPlacementInInvalidCount(
            string invalidCase)
        {
            var wallObject = new GameObject("BackWall");
            var wall = AddWallSurface(wallObject, "wall.back", 8, 2, 1f);
            var items = new[]
            {
                new WallMountedInstance(
                    "poster.valid",
                    "wall-art.poster",
                    "wall.back",
                    new WallSlotPosition(0, 0),
                    new WallFootprint(1, 1)),
                new WallMountedInstance(
                    "poster.invalid",
                    "wall-art.poster",
                    invalidCase == "unknown-surface" ? "wall.unknown" : "wall.back",
                    invalidCase == "out-of-bounds"
                        ? new WallSlotPosition(7, 1)
                        : new WallSlotPosition(1, 0),
                    invalidCase == "out-of-bounds"
                        ? new WallFootprint(2, 1)
                        : new WallFootprint(1, 1))
            };

            try
            {
                var report = Phase4AssetValidator.ValidateWallContent(
                    new[] { wall },
                    items);

                Assert.That(report.AssetCount, Is.EqualTo(3));
                Assert.That(report.ValidAssetCount, Is.EqualTo(2));
                Assert.That(report.InvalidAssetCount, Is.EqualTo(1));
                Assert.That(report.Issues.Count(issue =>
                    issue.Code == Phase4AssetIssueCode.InvalidWallPlacement), Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(wallObject);
            }
        }

        [Test]
        public void Wall_WithDefinitionsAndReferences_CountsEveryValidatedObjectExactlyOnce()
        {
            var wallObject = new GameObject("BackWall");
            var wall = AddWallSurface(wallObject, "wall.back", 8, 2, 1f);
            var wallArtPrefab = new GameObject("PF_WallArt");
            var wallArtMesh = CreateTriangleMesh("WallArtMesh");
            var wallArtShader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(wallArtShader, Is.Not.Null);
            var wallArtMaterial = new Material(wallArtShader);
            var wallArtTexture = new Texture2D(1, 1);
            wallArtMaterial.SetTexture("_BaseMap", wallArtTexture);
            AddRendererChild(
                wallArtPrefab,
                "WallArtModel",
                wallArtMesh,
                wallArtMaterial);
            var wallArtCollider = wallArtPrefab.AddComponent<BoxCollider>();
            wallArtCollider.center = new Vector3(0f, 0.5f, 0f);
            wallArtCollider.size = Vector3.one;
            var validDefinition = CreateWallDefinition(
                "wall-art.poster",
                "Poster",
                wallArtPrefab,
                1,
                1);
            var invalidDefinition = CreateWallDefinition(
                "wall-art.missing-prefab",
                "Missing Prefab",
                null,
                1,
                1);
            var items = new[]
            {
                new WallMountedInstance(
                    "poster.valid",
                    "wall-art.poster",
                    "wall.back",
                    new WallSlotPosition(0, 0),
                    new WallFootprint(1, 1)),
                new WallMountedInstance(
                    "poster.unknown-definition",
                    "wall-art.unknown",
                    "wall.back",
                    new WallSlotPosition(1, 0),
                    new WallFootprint(1, 1)),
                new WallMountedInstance(
                    "poster.wrong-footprint",
                    "wall-art.poster",
                    "wall.back",
                    new WallSlotPosition(2, 0),
                    new WallFootprint(2, 1)),
                new WallMountedInstance(
                    "poster.out-of-bounds",
                    "wall-art.poster",
                    "wall.back",
                    new WallSlotPosition(7, 1),
                    new WallFootprint(1, 2))
            };

            try
            {
                var report = Phase4AssetValidator.ValidateWallContent(
                    new[] { wall },
                    new[] { validDefinition, invalidDefinition },
                    items);

                Assert.That(report.AssetCount, Is.EqualTo(7));
                Assert.That(report.ValidAssetCount, Is.EqualTo(3));
                Assert.That(report.InvalidAssetCount, Is.EqualTo(4));
                Assert.That(report.Issues.Count(issue =>
                    issue.Code == Phase4AssetIssueCode.InvalidWallPlacement), Is.EqualTo(3));
                Assert.That(report.Issues.Any(issue =>
                    issue.AssetPath.Contains("wall-art.missing-prefab") &&
                    issue.Code == Phase4AssetIssueCode.MissingPrefab), Is.True);
                Assert.That(report.Issues.Any(issue =>
                    issue.Message.Contains("wrong-footprint") &&
                    issue.Message.Contains("1 x 1") &&
                    issue.Message.Contains("2 x 1")), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(validDefinition);
                Object.DestroyImmediate(invalidDefinition);
                Object.DestroyImmediate(wallArtPrefab);
                Object.DestroyImmediate(wallArtMaterial);
                Object.DestroyImmediate(wallArtTexture);
                Object.DestroyImmediate(wallArtMesh);
                Object.DestroyImmediate(wallObject);
            }
        }

        [TestCase("missing-mesh", Phase4AssetIssueCode.MissingReference, "Mesh")]
        [TestCase("missing-material", Phase4AssetIssueCode.MissingReference, "Material")]
        [TestCase("missing-texture", Phase4AssetIssueCode.MissingReference, "_BaseMap")]
        [TestCase("missing-collider", Phase4AssetIssueCode.TechnicalAssetContract, "Collider")]
        [TestCase("missing-script", Phase4AssetIssueCode.MissingReference, "missing script")]
        public void WallDefinition_NonNullPrefabRunsTechnicalValidationAndUsesPrefabPath(
            string invalidCase,
            Phase4AssetIssueCode expectedCode,
            string expectedMessagePart)
        {
            AssetDatabase.DeleteAsset(WallTechnicalFixtureFolder);
            EnsureAssetFolder(WallTechnicalFixtureFolder);
            var mesh = CreateTriangleMesh("WallTechnicalMesh");
            AssetDatabase.CreateAsset(
                mesh,
                $"{WallTechnicalFixtureFolder}/WallTechnicalMesh.asset");
            var texture = new Texture2D(1, 1) { name = "WallTechnicalTexture" };
            AssetDatabase.CreateAsset(
                texture,
                $"{WallTechnicalFixtureFolder}/WallTechnicalTexture.asset");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader, Is.Not.Null);
            var validMaterial = new Material(shader) { name = "MAT_WallTechnicalValid" };
            validMaterial.SetTexture("_BaseMap", texture);
            AssetDatabase.CreateAsset(
                validMaterial,
                $"{WallTechnicalFixtureFolder}/MAT_WallTechnicalValid.mat");
            var missingTextureMaterial = new Material(shader)
            {
                name = "MAT_WallTechnicalMissingTexture"
            };
            missingTextureMaterial.SetTexture("_BaseMap", null);
            AssetDatabase.CreateAsset(
                missingTextureMaterial,
                $"{WallTechnicalFixtureFolder}/MAT_WallTechnicalMissingTexture.mat");

            var root = new GameObject("PF_WallTechnical");
            AddRendererChild(
                root,
                "WallTechnicalModel",
                invalidCase == "missing-mesh" ? null : mesh,
                invalidCase == "missing-material"
                    ? null
                    : invalidCase == "missing-texture"
                        ? missingTextureMaterial
                        : validMaterial);
            if (invalidCase != "missing-collider")
            {
                var collider = root.AddComponent<BoxCollider>();
                collider.center = new Vector3(0f, 0.5f, 0f);
                collider.size = Vector3.one;
            }

            string markerGuid = null;
            if (invalidCase == "missing-script")
            {
                var scriptNode = new GameObject("WallMissingScriptNode");
                scriptNode.transform.SetParent(root.transform, false);
                var marker = scriptNode.AddComponent<SurfaceSlotMarker>();
                var markerScript = MonoScript.FromMonoBehaviour(marker);
                markerGuid = AssetDatabase.AssetPathToGUID(
                    AssetDatabase.GetAssetPath(markerScript));
            }

            var prefabPath = $"{WallTechnicalFixtureFolder}/PF_WallTechnical.prefab";
            var definitionPath = $"{WallTechnicalFixtureFolder}/DA_WallTechnical.asset";

            try
            {
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                var definition = CreateWallDefinition(
                    "wall-art.technical-review",
                    "Technical Review",
                    prefab,
                    1,
                    1);
                AssetDatabase.CreateAsset(definition, definitionPath);
                AssetDatabase.SaveAssets();
                if (invalidCase == "missing-script")
                {
                    var yamlPath = System.IO.Path.GetFullPath(prefabPath);
                    var yaml = System.IO.File.ReadAllText(yamlPath);
                    Assert.That(yaml, Does.Contain(markerGuid));
                    yaml = yaml.Replace(markerGuid, "22222222222222222222222222222222");
                    System.IO.File.WriteAllText(yamlPath, yaml);
                    AssetDatabase.ImportAsset(
                        prefabPath,
                        ImportAssetOptions.ForceSynchronousImport);
                }

                var report = Phase4AssetValidator.ValidateWallContent(
                    new WallSurfaceAuthoring[0],
                    new[] { definition },
                    new WallMountedInstance[0]);
                var issue = report.Issues.FirstOrDefault(candidate =>
                    candidate.Code == expectedCode &&
                    candidate.Message.Contains(expectedMessagePart));

                Assert.That(report.AssetCount, Is.EqualTo(1));
                Assert.That(report.ValidAssetCount, Is.Zero);
                Assert.That(report.InvalidAssetCount, Is.EqualTo(1));
                Assert.That(issue, Is.Not.Null);
                Assert.That(issue.AssetPath, Is.EqualTo(prefabPath));
                Assert.That(issue.Message, Does.Contain("wall-art.technical-review"));
                Assert.That(issue.Message, Does.Contain(definitionPath));
            }
            finally
            {
                Object.DestroyImmediate(root);
                AssetDatabase.DeleteAsset(WallTechnicalFixtureFolder);
                AssetDatabase.Refresh();
            }
        }

        [Test]
        public void ValidateAll_NoArgumentsScansWallMountedDefinitionAssetsInExactCounts()
        {
            AssetDatabase.DeleteAsset(WallDefinitionFixtureFolder);
            var before = Phase4AssetValidator.ValidateAll();
            EnsureAssetFolder(WallDefinitionFixtureFolder);
            var definition = CreateWallDefinition(
                "wall-art.review-missing-prefab",
                "Review Missing Prefab",
                null,
                1,
                1);
            var definitionPath =
                $"{WallDefinitionFixtureFolder}/DA_ReviewMissingPrefab.asset";

            try
            {
                AssetDatabase.CreateAsset(definition, definitionPath);
                AssetDatabase.SaveAssets();

                var after = Phase4AssetValidator.ValidateAll();

                Assert.That(after.AssetCount, Is.EqualTo(before.AssetCount + 1));
                Assert.That(after.ValidAssetCount, Is.EqualTo(before.ValidAssetCount));
                Assert.That(after.InvalidAssetCount, Is.EqualTo(before.InvalidAssetCount + 1));
                Assert.That(after.Issues.Any(issue =>
                    issue.AssetPath == definitionPath &&
                    issue.Code == Phase4AssetIssueCode.MissingPrefab), Is.True);
            }
            finally
            {
                AssetDatabase.DeleteAsset(WallDefinitionFixtureFolder);
                AssetDatabase.Refresh();
            }
        }

        [Test]
        public void ValidateAll_NestedPrefabSourceComponentsAreCountedExactlyOnce()
        {
            AssetDatabase.DeleteAsset(NestedPrefabFixtureFolder);
            var before = Phase4AssetValidator.ValidateAll();
            EnsureAssetFolder(NestedPrefabFixtureFolder);
            var childRoot = new GameObject("PF_EnvironmentChild");
            AddWallSurface(childRoot, "wall.review-nested", 8, 2, 1f);
            AddEntrance(childRoot, "entrance.review-nested", 0, 0);
            var childPath = $"{NestedPrefabFixtureFolder}/PF_EnvironmentChild.prefab";
            var parentPath = $"{NestedPrefabFixtureFolder}/PF_EnvironmentParent.prefab";
            GameObject parentRoot = null;

            try
            {
                PrefabUtility.SaveAsPrefabAsset(childRoot, childPath);
                Object.DestroyImmediate(childRoot);
                childRoot = null;

                var childAsset = AssetDatabase.LoadAssetAtPath<GameObject>(childPath);
                parentRoot = new GameObject("PF_EnvironmentParent");
                var nestedInstance = PrefabUtility.InstantiatePrefab(childAsset) as GameObject;
                Assert.That(nestedInstance, Is.Not.Null);
                nestedInstance.transform.SetParent(parentRoot.transform, false);
                PrefabUtility.SaveAsPrefabAsset(parentRoot, parentPath);
                Object.DestroyImmediate(parentRoot);
                parentRoot = null;

                var after = Phase4AssetValidator.ValidateAll();

                Assert.That(after.AssetCount, Is.EqualTo(before.AssetCount + 2),
                    "The child WallSurfaceAuthoring and EntrancePortalAuthoring must not be counted again through the parent nested prefab.");
                Assert.That(after.ValidAssetCount, Is.EqualTo(before.ValidAssetCount + 2));
                Assert.That(after.InvalidAssetCount, Is.EqualTo(before.InvalidAssetCount));
            }
            finally
            {
                if (childRoot != null)
                {
                    Object.DestroyImmediate(childRoot);
                }

                if (parentRoot != null)
                {
                    Object.DestroyImmediate(parentRoot);
                }

                AssetDatabase.DeleteAsset(NestedPrefabFixtureFolder);
                AssetDatabase.Refresh();
            }
        }

        [TestCase("distinct-overrides")]
        [TestCase("duplicate-wall-id")]
        public void ValidateAll_RepeatedNestedInstancesPreserveEachInstanceAndOverrides(
            string testCase)
        {
            AssetDatabase.DeleteAsset(NestedPrefabFixtureFolder);
            var before = Phase4AssetValidator.ValidateAll();
            EnsureAssetFolder(NestedPrefabFixtureFolder);
            var childRoot = new GameObject("PF_RepeatedEnvironmentChild");
            AddWallSurface(childRoot, "wall.child-source", 8, 2, 1f);
            AddEntrance(childRoot, "entrance.child-source", 0, 0);
            var childPath =
                $"{NestedPrefabFixtureFolder}/PF_RepeatedEnvironmentChild.prefab";
            var parentPath =
                $"{NestedPrefabFixtureFolder}/PF_RepeatedEnvironmentParent.prefab";
            GameObject parentRoot = null;

            try
            {
                PrefabUtility.SaveAsPrefabAsset(childRoot, childPath);
                Object.DestroyImmediate(childRoot);
                childRoot = null;
                var childAsset = AssetDatabase.LoadAssetAtPath<GameObject>(childPath);
                parentRoot = new GameObject("PF_RepeatedEnvironmentParent");

                var first = PrefabUtility.InstantiatePrefab(childAsset) as GameObject;
                var second = PrefabUtility.InstantiatePrefab(childAsset) as GameObject;
                Assert.That(first, Is.Not.Null);
                Assert.That(second, Is.Not.Null);
                first.name = "EnvironmentInstanceA";
                second.name = "EnvironmentInstanceB";
                first.transform.SetParent(parentRoot.transform, false);
                second.transform.SetParent(parentRoot.transform, false);
                var firstWall = first.GetComponent<WallSurfaceAuthoring>();
                var secondWall = second.GetComponent<WallSurfaceAuthoring>();
                var firstEntrance = first.GetComponent<EntrancePortalAuthoring>();
                var secondEntrance = second.GetComponent<EntrancePortalAuthoring>();
                SetSerialized(firstWall, "surfaceId", "wall.instance-a");
                SetSerialized(
                    secondWall,
                    "surfaceId",
                    testCase == "duplicate-wall-id"
                        ? "wall.instance-a"
                        : "wall.instance-b");
                SetSerialized(firstEntrance, "entranceId", "entrance.instance-a");
                SetSerialized(secondEntrance, "entranceId", "entrance.instance-b");
                PrefabUtility.RecordPrefabInstancePropertyModifications(firstWall);
                PrefabUtility.RecordPrefabInstancePropertyModifications(secondWall);
                PrefabUtility.RecordPrefabInstancePropertyModifications(firstEntrance);
                PrefabUtility.RecordPrefabInstancePropertyModifications(secondEntrance);
                PrefabUtility.SaveAsPrefabAsset(parentRoot, parentPath);
                Object.DestroyImmediate(parentRoot);
                parentRoot = null;

                var after = Phase4AssetValidator.ValidateAll();

                Assert.That(after.AssetCount, Is.EqualTo(before.AssetCount + 4));
                if (testCase == "distinct-overrides")
                {
                    Assert.That(after.ValidAssetCount, Is.EqualTo(before.ValidAssetCount + 4));
                    Assert.That(after.InvalidAssetCount, Is.EqualTo(before.InvalidAssetCount));
                }
                else
                {
                    Assert.That(after.ValidAssetCount, Is.EqualTo(before.ValidAssetCount + 2));
                    Assert.That(after.InvalidAssetCount, Is.EqualTo(before.InvalidAssetCount + 2));
                    Assert.That(after.Issues.Count(issue =>
                        issue.Code == Phase4AssetIssueCode.InvalidWallSurface &&
                        issue.AssetPath == parentPath), Is.EqualTo(2));
                }
            }
            finally
            {
                if (childRoot != null)
                {
                    Object.DestroyImmediate(childRoot);
                }

                if (parentRoot != null)
                {
                    Object.DestroyImmediate(parentRoot);
                }

                AssetDatabase.DeleteAsset(NestedPrefabFixtureFolder);
                AssetDatabase.Refresh();
            }
        }

        [TestCase("dimensions")]
        [TestCase("slot-size")]
        [TestCase("duplicate-id")]
        public void Wall_InvalidProductionSurface_ReportsSpecificIssue(string invalidCase)
        {
            var leftObject = new GameObject("BackLeftWall");
            var rightObject = new GameObject("BackRightWall");
            var left = AddWallSurface(
                leftObject,
                "wall.back-left",
                invalidCase == "dimensions" ? 7 : 8,
                2,
                invalidCase == "slot-size" ? 0.5f : 1f);
            var right = AddWallSurface(
                rightObject,
                invalidCase == "duplicate-id" ? "wall.back-left" : "wall.back-right",
                8,
                2,
                1f);

            try
            {
                var report = Phase4AssetValidator.ValidateWallContent(
                    new[] { left, right },
                    new WallMountedInstance[0]);

                Assert.That(report.Issues.Select(issue => issue.Code),
                    Does.Contain(Phase4AssetIssueCode.InvalidWallSurface));
            }
            finally
            {
                Object.DestroyImmediate(leftObject);
                Object.DestroyImmediate(rightObject);
            }
        }

        [TestCase("overlap")]
        [TestCase("out-of-bounds")]
        [TestCase("cross-corner")]
        public void Wall_InvalidPlacement_ReportsSpecificIssue(string invalidCase)
        {
            var leftObject = new GameObject("BackLeftWall");
            var rightObject = new GameObject("BackRightWall");
            var left = AddWallSurface(leftObject, "wall.back-left", 8, 2, 1f);
            var right = AddWallSurface(rightObject, "wall.back-right", 8, 2, 1f);
            var items = new List<WallMountedInstance>
            {
                new WallMountedInstance(
                    "window.01",
                    "window.basic.01",
                    "wall.back-right",
                    new WallSlotPosition(3, 0),
                    new WallFootprint(1, 1))
            };

            if (invalidCase == "overlap")
            {
                items.Add(new WallMountedInstance(
                    "poster.01",
                    "wall-art.poster.01",
                    "wall.back-right",
                    new WallSlotPosition(3, 0),
                    new WallFootprint(1, 1)));
            }
            else if (invalidCase == "out-of-bounds")
            {
                items.Add(new WallMountedInstance(
                    "sign.01",
                    "wall-art.sign.01",
                    "wall.back-right",
                    new WallSlotPosition(7, 0),
                    new WallFootprint(2, 1)));
            }
            else
            {
                items.Add(new WallMountedInstance(
                    "corner-art.01",
                    "wall-art.corner.01",
                    "wall.missing-corner",
                    new WallSlotPosition(0, 0),
                    new WallFootprint(1, 1)));
            }

            try
            {
                var report = Phase4AssetValidator.ValidateWallContent(
                    new[] { left, right },
                    items);

                Assert.That(report.Issues.Select(issue => issue.Code),
                    Does.Contain(Phase4AssetIssueCode.InvalidWallPlacement));
            }
            finally
            {
                Object.DestroyImmediate(leftObject);
                Object.DestroyImmediate(rightObject);
            }
        }

        [Test]
        public void Entrance_StableTwoByTwoReservationInsideLayout_PassesContract()
        {
            var portalObject = new GameObject("EntrancePortal");
            var portal = AddEntrance(portalObject, "entrance.main", 3, 0);

            try
            {
                var report = Phase4AssetValidator.ValidateEntrances(
                    new[] { portal },
                    new GridSize(8, 8));

                Assert.That(portal.CreateReservation().Size, Is.EqualTo(new GridSize(2, 2)));
                Assert.That(report.Issues.Select(issue => issue.Code),
                    Has.No.Member(Phase4AssetIssueCode.InvalidEntrance));
            }
            finally
            {
                Object.DestroyImmediate(portalObject);
            }
        }

        [TestCase("missing-id")]
        [TestCase("duplicate-id")]
        [TestCase("out-of-bounds")]
        [TestCase("blocking-collider")]
        public void Entrance_InvalidContract_ReportsSpecificIssue(string invalidCase)
        {
            var firstObject = new GameObject("EntrancePortal");
            var first = AddEntrance(
                firstObject,
                invalidCase == "missing-id" ? "" : "entrance.main",
                invalidCase == "out-of-bounds" ? 7 : 3,
                invalidCase == "out-of-bounds" ? 7 : 0);
            var portals = new List<EntrancePortalAuthoring> { first };
            GameObject secondObject = null;
            if (invalidCase == "duplicate-id")
            {
                secondObject = new GameObject("EntrancePortalDuplicate");
                portals.Add(AddEntrance(secondObject, "entrance.main", 0, 0));
            }

            if (invalidCase == "blocking-collider")
            {
                var collider = firstObject.AddComponent<BoxCollider>();
                collider.center = Vector3.zero;
                collider.size = new Vector3(2f, 1f, 2f);
            }

            try
            {
                var report = Phase4AssetValidator.ValidateEntrances(
                    portals,
                    new GridSize(8, 8));

                Assert.That(report.Issues.Select(issue => issue.Code),
                    Does.Contain(Phase4AssetIssueCode.InvalidEntrance));
            }
            finally
            {
                Object.DestroyImmediate(firstObject);
                if (secondObject != null)
                {
                    Object.DestroyImmediate(secondObject);
                }
            }
        }

        [Test]
        public void Technical_ValidPrefab_PassesWithoutMutatingSourceObjects()
        {
            using (var fixture = new TechnicalFixture(1, false))
            {
                var originalMesh = fixture.MeshFilter.sharedMesh;
                var originalMaterial = fixture.Renderer.sharedMaterial;
                var originalCollider = fixture.Collider;
                var originalRootPosition = fixture.Root.transform.localPosition;

                var report = Phase4AssetValidator.ValidateFurnitureDefinition(
                    fixture.Definition);

                Assert.That(report.Issues.Select(issue => issue.Code),
                    Has.No.Member(Phase4AssetIssueCode.TechnicalAssetContract));
                Assert.That(report.Issues.Select(issue => issue.Code),
                    Has.No.Member(Phase4AssetIssueCode.MissingReference));
                Assert.That(fixture.MeshFilter.sharedMesh, Is.SameAs(originalMesh));
                Assert.That(fixture.Renderer.sharedMaterial, Is.SameAs(originalMaterial));
                Assert.That(fixture.Root.GetComponent<BoxCollider>(), Is.SameAs(originalCollider));
                Assert.That(fixture.Root.transform.localPosition, Is.EqualTo(originalRootPosition));
            }
        }

        [TestCase("missing-mesh", Phase4AssetIssueCode.MissingReference)]
        [TestCase("missing-material", Phase4AssetIssueCode.MissingReference)]
        [TestCase("missing-texture", Phase4AssetIssueCode.MissingReference)]
        [TestCase("wrong-shader", Phase4AssetIssueCode.TechnicalAssetContract)]
        [TestCase("oversized-texture", Phase4AssetIssueCode.TechnicalAssetContract)]
        [TestCase("missing-collider", Phase4AssetIssueCode.TechnicalAssetContract)]
        [TestCase("trigger-collider", Phase4AssetIssueCode.TechnicalAssetContract)]
        [TestCase("mesh-collider", Phase4AssetIssueCode.TechnicalAssetContract)]
        [TestCase("outside-collider", Phase4AssetIssueCode.TechnicalAssetContract)]
        [TestCase("displaced-pivot", Phase4AssetIssueCode.TechnicalAssetContract)]
        [TestCase("root-transform", Phase4AssetIssueCode.TechnicalAssetContract)]
        [TestCase("inactive-camera", Phase4AssetIssueCode.TechnicalAssetContract)]
        [TestCase("light", Phase4AssetIssueCode.TechnicalAssetContract)]
        [TestCase("raw-cube", Phase4AssetIssueCode.TechnicalAssetContract)]
        public void Technical_InvalidPrefab_ReportsSearchableIssue(
            string invalidCase,
            Phase4AssetIssueCode expectedCode)
        {
            using (var fixture = new TechnicalFixture(1, false))
            {
                if (invalidCase == "missing-mesh")
                {
                    fixture.MeshFilter.sharedMesh = null;
                }
                else if (invalidCase == "missing-material")
                {
                    fixture.Renderer.sharedMaterial = null;
                }
                else if (invalidCase == "missing-texture")
                {
                    fixture.Material.SetTexture("_BaseMap", null);
                }
                else if (invalidCase == "wrong-shader")
                {
                    fixture.Material.shader = Shader.Find("Universal Render Pipeline/Unlit");
                }
                else if (invalidCase == "oversized-texture")
                {
                    fixture.ReplaceTexture(new Texture2D(1025, 1));
                }
                else if (invalidCase == "missing-collider")
                {
                    Object.DestroyImmediate(fixture.Collider);
                    fixture.Collider = null;
                }
                else if (invalidCase == "trigger-collider")
                {
                    fixture.Collider.isTrigger = true;
                }
                else if (invalidCase == "mesh-collider")
                {
                    Object.DestroyImmediate(fixture.Collider);
                    fixture.Collider = null;
                    fixture.Root.AddComponent<MeshCollider>().sharedMesh = fixture.Mesh;
                }
                else if (invalidCase == "outside-collider")
                {
                    fixture.Collider.center = new Vector3(3f, 0.05f, 0f);
                }
                else if (invalidCase == "displaced-pivot")
                {
                    var vertices = fixture.Mesh.vertices;
                    for (var index = 0; index < vertices.Length; index++)
                    {
                        vertices[index] += Vector3.up * 0.2f;
                    }

                    fixture.Mesh.vertices = vertices;
                    fixture.Mesh.RecalculateBounds();
                    fixture.Collider.center += Vector3.up * 0.2f;
                }
                else if (invalidCase == "root-transform")
                {
                    fixture.Root.transform.localScale = new Vector3(2f, 1f, 1f);
                }
                else if (invalidCase == "inactive-camera")
                {
                    var child = new GameObject("RawCamera");
                    child.transform.SetParent(fixture.Root.transform, false);
                    child.AddComponent<UnityEngine.Camera>();
                    child.SetActive(false);
                }
                else if (invalidCase == "light")
                {
                    var child = new GameObject("RawLight");
                    child.transform.SetParent(fixture.Root.transform, false);
                    child.AddComponent<Light>();
                }
                else if (invalidCase == "raw-cube")
                {
                    var child = new GameObject("Cube");
                    child.transform.SetParent(fixture.Root.transform, false);
                }

                var report = Phase4AssetValidator.ValidateFurnitureDefinition(
                    fixture.Definition);

                Assert.That(report.Issues.Select(issue => issue.Code),
                    Does.Contain(expectedCode));
                if (invalidCase == "displaced-pivot")
                {
                    Assert.That(report.Issues.Any(issue =>
                            issue.Code == Phase4AssetIssueCode.TechnicalAssetContract &&
                            issue.Message.Contains("bottom-center")),
                        Is.True);
                }
            }
        }

        [Test]
        public void Technical_NonReadableOverBudgetMesh_ReportsWithoutReadAccess()
        {
            using (var fixture = new TechnicalFixture(6001, true))
            {
                Assert.That(fixture.Mesh.isReadable, Is.False);

                var report = Phase4AssetValidator.ValidateFurnitureDefinition(
                    fixture.Definition);

                Assert.That(report.Issues.Select(issue => issue.Code),
                    Does.Contain(Phase4AssetIssueCode.TechnicalAssetContract));
            }
        }

        [TestCase(
            "ValidateProductionContent",
            "AnimalCafe/Phase 4/Validate Production Content")]
        [TestCase(
            "BuildValidationScene",
            "AnimalCafe/Phase 4/Build Validation Scene")]
        public void Menu_RegistersExactRequiredItem(string methodName, string expectedMenuPath)
        {
            var method = typeof(Phase4ValidationMenu).GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            var attribute = method.GetCustomAttributes(typeof(MenuItem), false)
                .Cast<MenuItem>()
                .Single();

            Assert.That(attribute.menuItem, Is.EqualTo(expectedMenuPath));
        }

        [Test]
        public void MainCafeManualReviewFixtureSetup_RegistersExactInteractiveMenuItem()
        {
            var method = typeof(MainCafeManualReviewFixtureSetup).GetMethod(
                "Apply",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);

            var attributes = method.GetCustomAttributes(typeof(MenuItem), false)
                .Cast<MenuItem>()
                .ToArray();

            Assert.That(attributes, Has.Length.EqualTo(1));
            Assert.That(
                attributes[0].menuItem,
                Is.EqualTo("AnimalCafe/Phase 4/Add MainCafe Manual Review Cubes"));
        }

        [Test]
        public void Menu_SummaryContainsExactCountsAndIssueList()
        {
            var report = new Phase4AssetValidationReport(
                2,
                1,
                new[]
                {
                    new Phase4AssetValidationIssue(
                        Phase4AssetIssueCode.MissingPrefab,
                        "Assets/Art/Phase4/Definitions/DA_Broken.asset",
                        "Furniture Definition must reference a Prefab.")
                });

            var summary = Phase4ValidationMenu.FormatSummary(report);

            Assert.That(summary, Does.Contain("valid=2 invalid=1 issues=1"));
            Assert.That(summary, Does.Contain(
                "[MissingPrefab] Assets/Art/Phase4/Definitions/DA_Broken.asset: " +
                "Furniture Definition must reference a Prefab."));
        }

        private sealed class TechnicalFixture : System.IDisposable
        {
            public TechnicalFixture(int triangleCount, bool makeNonReadable)
            {
                Root = new GameObject("PF_WorkTable");
                Mesh = CreateMesh(triangleCount, makeNonReadable);
                MeshFilter = Root.AddComponent<MeshFilter>();
                MeshFilter.sharedMesh = Mesh;
                Renderer = Root.AddComponent<MeshRenderer>();

                var shader = Shader.Find("Universal Render Pipeline/Lit");
                Assert.That(shader, Is.Not.Null, "URP Lit shader must be available to the real validator test.");
                Material = new Material(shader);
                Texture = new Texture2D(1, 1);
                Material.SetTexture("_BaseMap", Texture);
                if (Material.HasProperty("_Surface"))
                {
                    Material.SetFloat("_Surface", 0f);
                }

                Renderer.sharedMaterial = Material;
                Collider = Root.AddComponent<BoxCollider>();
                Collider.center = new Vector3(0f, 0.5f, 0f);
                Collider.size = Vector3.one;
                Definition = CreateDefinition(
                    "furniture.work-table.01",
                    "Work Table",
                    Root,
                    1,
                    1,
                    PlacementSurfaceType.Floor,
                    FurnitureFunctionType.None);
            }

            public GameObject Root { get; }
            public FurnitureDefinitionAsset Definition { get; }
            public Mesh Mesh { get; }
            public MeshFilter MeshFilter { get; }
            public MeshRenderer Renderer { get; }
            public Material Material { get; }
            public Texture2D Texture { get; private set; }
            public BoxCollider Collider { get; set; }

            public void ReplaceTexture(Texture2D replacement)
            {
                Object.DestroyImmediate(Texture);
                Texture = replacement;
                Material.SetTexture("_BaseMap", Texture);
            }

            public void Dispose()
            {
                Object.DestroyImmediate(Definition);
                Object.DestroyImmediate(Root);
                Object.DestroyImmediate(Material);
                Object.DestroyImmediate(Texture);
                Object.DestroyImmediate(Mesh);
            }

            private static Mesh CreateMesh(int triangleCount, bool makeNonReadable)
            {
                var mesh = new Mesh { name = "TechnicalFixtureMesh" };
                mesh.vertices = new[]
                {
                    new Vector3(-0.5f, 0f, -0.5f),
                    new Vector3(0.5f, 0f, -0.5f),
                    new Vector3(0f, 1f, 0.5f)
                };
                var indices = new int[triangleCount * 3];
                for (var index = 0; index < indices.Length; index++)
                {
                    indices[index] = index % 3;
                }

                mesh.SetIndices(indices, MeshTopology.Triangles, 0);
                mesh.RecalculateBounds();
                if (makeNonReadable)
                {
                    mesh.UploadMeshData(true);
                }

                return mesh;
            }
        }

        private static EntrancePortalAuthoring AddEntrance(
            GameObject root,
            string entranceId,
            int originX,
            int originY)
        {
            var portal = root.AddComponent<EntrancePortalAuthoring>();
            SetSerialized(portal, "entranceId", entranceId);
            SetSerialized(portal, "originX", originX);
            SetSerialized(portal, "originY", originY);
            return portal;
        }

        private static WallSurfaceAuthoring AddWallSurface(
            GameObject root,
            string surfaceId,
            int columns,
            int rows,
            float slotSize)
        {
            var authoring = root.AddComponent<WallSurfaceAuthoring>();
            SetSerialized(authoring, "surfaceId", surfaceId);
            SetSerialized(authoring, "columns", columns);
            SetSerialized(authoring, "rows", rows);
            SetSerialized(authoring, "slotSize", slotSize);
            return authoring;
        }

        private static CashRegisterSideMarker AddCashSide(
            GameObject root,
            CashRegisterSideType sideType,
            CardinalDirection direction)
        {
            var sideObject = new GameObject($"{sideType}Side");
            sideObject.transform.SetParent(root.transform, false);
            var marker = sideObject.AddComponent<CashRegisterSideMarker>();
            SetSerialized(marker, "sideType", sideType);
            SetSerialized(marker, "localDirection", direction);
            return marker;
        }

        private static Transform AddForwardMarker(
            GameObject root,
            Vector3 localPosition,
            Quaternion localRotation)
        {
            var marker = new GameObject("ForwardMarker");
            marker.transform.SetParent(root.transform, false);
            marker.transform.localPosition = localPosition;
            marker.transform.localRotation = localRotation;
            return marker.transform;
        }

        private static SurfaceSlotMarker AddSurfaceSlot(
            GameObject root,
            string slotId,
            Vector3 localPosition)
        {
            var slotObject = new GameObject($"SurfaceSlot_{slotId}");
            slotObject.transform.SetParent(root.transform, false);
            slotObject.transform.localPosition = localPosition;
            var marker = slotObject.AddComponent<SurfaceSlotMarker>();
            SetSerialized(marker, "slotId", slotId);
            return marker;
        }

        private static MeshRenderer AddRendererChild(
            GameObject root,
            string name,
            Mesh mesh,
            Material material)
        {
            var child = new GameObject(name);
            child.transform.SetParent(root.transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        private static Mesh CreateTriangleMesh(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, 0f, -0.5f),
                new Vector3(0.5f, 0f, -0.5f),
                new Vector3(0f, 1f, 0.5f)
            };
            mesh.SetIndices(new[] { 0, 1, 2 }, MeshTopology.Triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateWideTriangleMesh(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.vertices = new[]
            {
                new Vector3(-0.75f, 0f, -0.25f),
                new Vector3(0.75f, 0f, -0.25f),
                new Vector3(0f, 1f, 0.25f)
            };
            mesh.SetIndices(new[] { 0, 1, 2 }, MeshTopology.Triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static FurnitureDefinitionAsset CreateDefinition(
            string definitionId,
            string displayName,
            GameObject prefab,
            int width,
            int depth,
            PlacementSurfaceType surfaces,
            FurnitureFunctionType functionType)
        {
            var definition = ScriptableObject.CreateInstance<FurnitureDefinitionAsset>();
            SetSerialized(definition, "definitionId", definitionId);
            SetSerialized(definition, "displayName", displayName);
            SetSerialized(definition, "prefab", prefab);
            SetSerialized(definition, "footprintWidth", width);
            SetSerialized(definition, "footprintDepth", depth);
            SetSerialized(definition, "allowedPlacementSurfaces", surfaces);
            SetSerialized(definition, "functionType", functionType);
            return definition;
        }

        private static WallMountedDefinitionAsset CreateWallDefinition(
            string definitionId,
            string displayName,
            GameObject prefab,
            int width,
            int height)
        {
            var definition = ScriptableObject.CreateInstance<WallMountedDefinitionAsset>();
            SetSerialized(definition, "definitionId", definitionId);
            SetSerialized(definition, "displayName", displayName);
            SetSerialized(definition, "prefab", prefab);
            SetSerialized(definition, "footprintWidth", width);
            SetSerialized(definition, "footprintHeight", height);
            return definition;
        }

        private static void SetSerialized(Object target, string propertyName, object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            Assert.That(property, Is.Not.Null, $"Missing serialized field {propertyName}.");

            if (value is string stringValue)
            {
                property.stringValue = stringValue;
            }
            else if (value is int intValue)
            {
                property.intValue = intValue;
            }
            else if (value is float floatValue)
            {
                property.floatValue = floatValue;
            }
            else if (value is Object objectValue)
            {
                property.objectReferenceValue = objectValue;
            }
            else if (value == null && property.propertyType == SerializedPropertyType.String)
            {
                property.stringValue = null;
            }
            else if (value == null && property.propertyType == SerializedPropertyType.ObjectReference)
            {
                property.objectReferenceValue = null;
            }
            else if (value is System.Enum enumValue)
            {
                property.intValue = System.Convert.ToInt32(enumValue);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
