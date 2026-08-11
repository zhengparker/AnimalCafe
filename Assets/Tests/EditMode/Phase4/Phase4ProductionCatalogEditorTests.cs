using System.Collections.Generic;
using System.Linq;
using AnimalCafe.Content;
using NUnit.Framework;
using UnityEditor;

namespace AnimalCafe.Tests.EditMode.Phase4
{
    public sealed class Phase4ProductionCatalogEditorTests
    {
        [Test]
        public void ProductionCatalog_ResolvesEveryApprovedPrefabPath()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<FurnitureContentCatalog>(
                "Assets/Art/Phase4/Catalogues/FC_Phase4Production.asset");
            var expectedPrefabs = new Dictionary<string, string>
            {
                ["furniture.counter.module.01"] =
                    "Assets/Art/Phase4/Prefabs/PF_Furniture_CounterModule_01.prefab",
                ["equipment.coffee-machine.01"] =
                    "Assets/Art/Phase4/Prefabs/PF_Equipment_CoffeeMachine_01.prefab",
                ["equipment.cash-register.01"] =
                    "Assets/Art/Phase4/Prefabs/PF_Equipment_CashRegister_01.prefab",
                ["furniture.work-table.01"] =
                    "Assets/Art/Phase4/Prefabs/PF_Furniture_WorkTable_01.prefab"
            };

            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.BuildRuntimeCatalog().Definitions
                .Select(definition => definition.Id),
                Is.EquivalentTo(expectedPrefabs.Keys));
            foreach (var expected in expectedPrefabs)
            {
                Assert.That(catalog.TryGetPrefab(expected.Key, out var prefab), Is.True);
                Assert.That(AssetDatabase.GetAssetPath(prefab), Is.EqualTo(expected.Value));
            }
        }
    }
}
